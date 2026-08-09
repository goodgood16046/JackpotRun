using System;
using System.Collections;
using JackpotRun.Core;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // NodeSelect 페이즈 패널 — ENGINE_PORT_DESIGN.md S7 Run/Panels/NodePanel.cs. 스테이지 클리어 등급
    // 배너(드롭+점수 CountUp) 후 1.0s 뒤 노드 3택 카드 패널이 슬라이드업 한다("RunView 연출" 지시 그대로).
    // 이관 원본: Scripts/UI/RunPanels.cs의 BuildNodeSelect(레이아웃은 버리고 NodeKindInfo/카드 구성 로직만).
    public sealed class NodePanel : MonoBehaviour
    {
        private const float BannerFadeDuration = 0.15f;
        private const float BannerDropDuration = 0.4f; // 설계 미명시 — OutBack 드롭 길이 기본값
        private const float ScoreCountUpDuration = 0.5f; // 설계 미명시
        private const float PostBannerDelay = 1.0f; // 설계 명시: "1.0s 후 노드 패널 슬라이드업"
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(설계 명시).
        private const float CardSlideDuration = 0.24f;
        private const float BannerRestY = -160f;
        private const float BannerStartY = 240f;

        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private RectTransform bannerRect;
        [SerializeField] private Text bannerGradeText;
        [SerializeField] private Text bannerScoreText;
        [SerializeField] private Text bannerSubText;
        [SerializeField] private RectTransform cardRect; // 슬라이드업 대상(카드 패널 전체)
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private RectTransform cardTemplate;
        [SerializeField] private CanvasGroup dimGroup; // S14 §E — 배경 딤(scrim 자체의 CanvasGroup) 페이드

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 renderStageClear ui.js:1615-1676) — 카드
        // 스크롤 영역(chrome.cardCol) 맨 위, "다음 노드를 선택하세요" 제목 위에 삽입한다(뜬 배너와 달리
        // 이 영역은 이미 스크롤 가능해 가변 높이 콘텐츠를 안전하게 담을 수 있다 — 배너 자체 구조/애니메이션은
        // 건드리지 않는다는 §7 재해석 원칙 유지).
        [Header("클리어 상세 — 2바(EXP%·사용 스핀)")]
        [SerializeField] private Text expDetailLabel;
        [SerializeField] private RectTransform expDetailBarFill;
        [SerializeField] private Text spinsDetailLabel;
        [SerializeField] private RectTransform spinsDetailBarFill;

        [Header("클리어 상세 — 마지막 스핀 5칸 + 획득 내역")]
        [SerializeField] private Text[] lastCellTexts = Array.Empty<Text>(); // 고정 5칸(Formulas.REEL)
        [SerializeField] private Text lastGainText;
        [SerializeField] private Text lastNotesText;

        [Header("클리어 상세 — 누적 총점 + 점수 상세 토글")]
        [SerializeField] private Text totalScoreText;
        [SerializeField] private Button detailToggleButton;
        [SerializeField] private Text detailToggleLabel;
        [SerializeField] private RectTransform detailRowsRoot;
        [SerializeField] private RectTransform detailRowsContent;
        [SerializeField] private RectTransform detailRowTemplate; // Inner/Label·Value(RewardDonePanel과 동일 관례)

        private bool _detailExpanded;

        private Coroutine _routine;

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (detailRowTemplate != null) detailRowTemplate.gameObject.SetActive(false);
            if (bannerGroup != null) bannerGroup.alpha = 0f;
            if (dimGroup != null) dimGroup.alpha = 0f;
            _detailExpanded = false;
            if (detailRowsRoot != null) detailRowsRoot.gameObject.SetActive(false);
            if (detailToggleButton != null) detailToggleButton.onClick.AddListener(ToggleDetail);
        }

        // onShake — 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) stageClearFx의 화면 흔들림 escalation.
        // NodePanel은 ReelView를 직접 참조하지 않으므로(컴포넌트 분리 유지) RunView가 배너 연출과 같은
        // 타이밍에 재생되도록 콜백으로 넘긴다(clear.gradeTier 인자로 즉시 호출).
        public void Show(ClearOutcome clear, RunState run, Action<int> onChoose, Action<int> onShake = null)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);
            BuildCards(run, onChoose);
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16) — 매번 접힌 상태로 다시 연다(웹도 매 클리어
            // 화면 진입마다 cb-calc가 display:none으로 새로 렌더됨, ui.js:1671).
            _detailExpanded = false;
            if (detailRowsRoot != null) detailRowsRoot.gameObject.SetActive(false);
            if (detailToggleLabel != null) detailToggleLabel.text = "▼ 점수 상세";
            BuildClearDetail(clear, run);

            if (_routine != null) StopCoroutine(_routine);
            if (firstShow) _routine = StartCoroutine(EnterRoutine(clear, onShake));
            else SnapToRest();
        }

        private void ToggleDetail()
        {
            _detailExpanded = !_detailExpanded;
            if (detailRowsRoot != null) detailRowsRoot.gameObject.SetActive(_detailExpanded);
            if (detailToggleLabel != null) detailToggleLabel.text = _detailExpanded ? "▲ 점수 상세 닫기" : "▼ 점수 상세";
        }

        public void Hide()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            gameObject.SetActive(false);
        }

        private void SnapToRest()
        {
            if (bannerGroup != null) bannerGroup.alpha = 0f;
            if (cardRect != null) cardRect.anchoredPosition = Vector2.zero;
            if (dimGroup != null) dimGroup.alpha = 1f;
        }

        private IEnumerator EnterRoutine(ClearOutcome clear, Action<int> onShake)
        {
            // S12c §6 — 하단 고정 앵커(BuildSheetChrome) 시트라 오프스크린 시작 위치는 카드 자신의
            // 높이(rect.height)만큼 아래(translateY(100%) 재해석) — 고정 매직넘버 대신 실측값 사용.
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (bannerGroup != null) bannerGroup.alpha = 0f;

            if (clear != null)
            {
                PopulateBanner(clear);
                // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹 stageClearFx ui.js:1701-1707 그대로) —
                // clear/perfect는 즉시, fanfare(tier>=4)는 130ms 후, 아니면 win(tier>=2)은 150ms 후,
                // 보스면 boss가 70ms 후에 추가로 겹쳐 난다(웹 setTimeout 3종 그대로 지연 코루틴으로 재현).
                bool perfect = clear.gradeTier == 6;
                SoundKit.Sfx(perfect ? "perfect" : "clear");
                if (clear.gradeTier >= 4) StartCoroutine(DelayedSfx("fanfare", 0.13f));
                else if (clear.gradeTier >= 2) StartCoroutine(DelayedSfx("win", 0.15f));
                if (clear.boss) StartCoroutine(DelayedSfx("boss", 0.07f));

                if (bannerRect != null) bannerRect.anchoredPosition = new Vector2(0f, BannerStartY);
                // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16, 웹 stageClearFx ui.js:1700-1731) — 등급
                // escalation: 화면 흔들림(콜백, RunView가 ReelView.PlayClearShake로 연결) + 색종이 개수를
                // 등급 tier에 비례해 늘린다(fx_clear 프리팹 반복 재생으로 근사 — 정확한 24/40/58/78/104
                // 카운트는 웹 전용 CSS 파티클 수라 1:1 이식 대상 아님, §작업 지시 "근사").
                onShake?.Invoke(clear.gradeTier);
                StartCoroutine(PlayConfettiEscalation(clear.gradeTier));
                yield return UiTween.FadeRoutine(bannerGroup, 0f, 1f, BannerFadeDuration);
                if (bannerRect != null)
                    yield return UiTween.MoveRoutine(bannerRect, bannerRect.anchoredPosition,
                        new Vector2(0f, BannerRestY), BannerDropDuration, UiTween.Ease.OutBack);
                // 등급 배지 펄스 — 배너가 안착하는 순간 한 번 팝(고티어일수록 크게).
                if (bannerGradeText != null) StartCoroutine(PulseGradeText(clear.gradeTier));
                if (bannerScoreText != null)
                    yield return UiTween.CountUpRoutine(0, clear.gainedScore, ScoreCountUpDuration,
                        v => bannerScoreText.text = $"+{NumberFormat.Comma(v)}점", UiTween.Ease.OutCubic);
                yield return new WaitForSeconds(PostBannerDelay);
            }

            // S14 §E / S12c §6 — "1.0s 후 노드 패널 슬라이드업 + 배경 딤 페이드"(동시 재생, 0.24s OutCubic).
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, CardSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero,
                    CardSlideDuration, UiTween.Ease.OutCubic);
            _routine = null;
        }

        // 웹 24/40/58/78/104(1~5단계)·perfect=30 색종이 개수를 "fx_clear 반복 재생 횟수"로 근사(0.08s
        // 스태거) — 정확한 파티클 총량 이식이 아니라 "고티어일수록 더 화려하다"는 체감만 재현한다.
        // Opus 2차검수 LOW④(2026-08-09) — 배열은 tier 1~5(index 0~4) 5개만 쓴다(gradeTier가 6이면
        // perfect 분기로 빠져 이 배열을 아예 읽지 않음 — 예전엔 index5(값5)가 죽은 원소였다).
        // perfect(웹 raw 개수 30)는 tier1(24)보다 살짝 많고 tier2(40)보다 훨씬 적다 — 5단계 중 가장
        // 화려한 연출이 아니라 tier1~2 사이 강도다(웹 clear-fx.perfect가 색종이 대신 cf-ring/cf-burst
        // 전용 연출로 화려함을 대신 낸다, ui.js:1728). 그래서 예전 값 3(tier3과 동급)은 과했다 — tier1
        // 다음으로 낮춰 웹 원본 비율에 맞춘다.
        private static readonly int[] ConfettiBurstsByTier = { 1, 2, 3, 4, 5 }; // index=tier-1(1~5)
        private const int ConfettiBurstsPerfect = 1;
        private const float ConfettiBurstStagger = 0.08f;

        private IEnumerator PlayConfettiEscalation(int gradeTier)
        {
            int bursts = gradeTier == 6 ? ConfettiBurstsPerfect
                : ConfettiBurstsByTier[Mathf.Clamp(gradeTier, 1, 5) - 1];
            for (int i = 0; i < bursts; i++)
            {
                FxKit.I?.Play(FxId.Clear, bannerRect);
                if (i < bursts - 1) yield return new WaitForSeconds(ConfettiBurstStagger);
            }
        }

        private static IEnumerator DelayedSfx(string name, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            SoundKit.Sfx(name);
        }

        private const float GradePulsePeakScale = 1.28f; // 설계 미명시 — "펄스" 지시의 팝 강도 기본값
        private const float GradePulseUpDuration = 0.12f;
        private const float GradePulseDownDuration = 0.18f;

        private IEnumerator PulseGradeText(int gradeTier)
        {
            var rt = bannerGradeText.rectTransform;
            // 고티어일수록 살짝 더 크게(perfect/tier5 최대) — 웹 .cfr-big 그라디언트 강조와 같은 취지.
            float peak = gradeTier >= 5 ? GradePulsePeakScale : 1f + (GradePulsePeakScale - 1f) * (gradeTier / 5f);
            rt.localScale = Vector3.one;
            yield return UiTween.ScaleRoutine(rt, Vector3.one, Vector3.one * peak, GradePulseUpDuration, UiTween.Ease.OutQuad);
            if (rt == null) yield break;
            yield return UiTween.ScaleRoutine(rt, rt.localScale, Vector3.one, GradePulseDownDuration, UiTween.Ease.OutBack);
        }

        // 웹 파리티 P4 — 등급 tier별 배지 색(ui.js CSS .cchip.grade.g1~g5/perfect 그라디언트를 단색으로
        // 근사, WEB_PARITY_DESIGN.md §1-A #16). g1/g2=초록, g3=파랑, g4=보라, g5/perfect=골드로 escalate.
        private static Color GradeTierColor(int tier)
        {
            switch (tier)
            {
                case 1:
                case 2: return UiKit.Green;
                case 3: return UiKit.Blue;
                case 4: return UiKit.Purple;
                default: return UiKit.Accent; // 5, 6(perfect)
            }
        }

        private void PopulateBanner(ClearOutcome clear)
        {
            if (bannerGradeText != null)
            {
                bannerGradeText.text = clear.grade;
                bannerGradeText.color = GradeTierColor(clear.gradeTier);
            }
            if (bannerScoreText != null) bannerScoreText.text = "+0점";
            if (bannerSubText != null)
            {
                // S8 항목⑤: 🎉/🌈(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
                // Opus 검수 반영(2026-08-07): 빚문서는 점수(gainedScore)만 0이고 코인(clearCoin)은
                // 정상 지급된다(웹 game.js:1416-1420) — "무보상"이라고 하면 코인도 안 나오는 것처럼
                // 오해되므로 "점수 보상 0"으로 문구를 좁혔다.
                string debt = clear.inDebt ? " (빚 상환 중·점수 보상 0)" : "";
                string prism = clear.nextNodeForcedPrism ? " · 다음 프리즘 확정" : "";
                // 웹 파리티 P4-3(웹 clear-sub, ui.js:1668) — "🪙 코인 N (+M) · 남은 스핀 N · 다음 STAGE N"
                // 순서 그대로 남은 스핀·다음 스테이지를 추가(코인/빚/프리즘 문구는 기존 그대로 유지).
                bannerSubText.text = $"스테이지 {clear.clearedStage} 클리어 · 코인+{NumberFormat.Comma(clear.clearCoin)} · " +
                    $"남은 스핀 {clear.leftSpins} · 다음 STAGE {clear.clearedStage + 1}{debt}{prism}";
            }
        }

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 renderStageClear ui.js:1627-1671) — 2바(달성
        // EXP%·사용 스핀) + 마지막 스핀 5칸/획득 내역 + 누적 총점 + "점수 상세" 토글(stage×50·초과×2·
        // 남은스핀×100·보스·연승 분해). 스크롤 카드 영역 상단에 삽입 — 뜬 배너 자체는 건드리지 않는다.
        private void BuildClearDetail(ClearOutcome clear, RunState run)
        {
            // Opus 2차검수 필수⑤ 계열(MED) — Show(clear=null, ...)로 불려도(예: _lastClear 캐시 미스)
            // 죽지 않게 방어(EnterRoutine의 기존 `if (clear != null)` 가드와 동일 원칙).
            if (clear == null || run == null) return;

            long quota = clear.quotaAtClear;
            long stageExp = clear.stageExpAtClear;
            // Opus 2차검수 필수⑤(MED) — 웹 pct(라벨 "(N%)")는 클램프하지 않는다("320%"까지 그대로
            // 표시). expFill(바 너비)만 100으로 클램프한다(ui.js:1628-1629 `const pct = ...; const
            // expFill = Math.min(100, pct);`) — 이전엔 라벨까지 100%로 잘려 보였다.
            int expPctRaw = quota > 0 ? Mathf.RoundToInt((float)(stageExp / (double)quota * 100.0)) : 100;
            int expFillPct = Mathf.Min(100, expPctRaw);
            int spinPct = clear.totalSpins > 0 ? Mathf.Clamp(Mathf.RoundToInt(clear.usedSpins / (float)clear.totalSpins * 100f), 0, 100) : 100;

            if (expDetailBarFill != null) expDetailBarFill.anchorMax = new Vector2(expFillPct / 100f, 1f);
            if (expDetailLabel != null)
                expDetailLabel.text = $"총 경험치 / 요구  {NumberFormat.Comma(stageExp)} / {NumberFormat.Comma(quota)} ({expPctRaw}%)";

            if (spinsDetailBarFill != null) spinsDetailBarFill.anchorMax = new Vector2(spinPct / 100f, 1f);
            if (spinsDetailLabel != null)
                spinsDetailLabel.text = $"사용 스핀 / 최대  {clear.usedSpins} / {clear.totalSpins}";

            // 마지막 스핀 5칸(LastCellsFinal — 폭탄 제거/자석 복사 등 반영된 "화면에 실제로 보인" 최종
            // 칸, §2-(W) LastCellsFinal 도입 근거 그대로) — S8 항목⑤: astral 이모지는 렌더링되지 않아
            // 한글 이름으로 대체(CellInfoSheet 제목 관례와 동일).
            var cells = run.LastCellsFinal;
            for (int i = 0; i < lastCellTexts.Length; i++)
            {
                if (lastCellTexts[i] == null) continue;
                bool has = i < cells.Count;
                lastCellTexts[i].gameObject.SetActive(has);
                if (!has) continue;
                var c = cells[i];
                string tag = string.IsNullOrEmpty(c.tag) ? "" : "\n" + TextSanitize.StripAstral(c.tag);
                lastCellTexts[i].text = TextSanitize.StripAstral(c.sym.name) + tag;
            }
            if (lastGainText != null)
                lastGainText.text = $"이 스핀에서 +{NumberFormat.Comma(clear.lastSpinGain)} EXP 획득";
            if (lastNotesText != null)
            {
                var notes = run.LastNotes;
                lastNotesText.text = (notes != null && notes.Count > 0)
                    ? TextSanitize.StripAstral(string.Join(" · ", notes))
                    : "기본 심볼 EXP만 · 특수 효과 없음";
            }

            if (totalScoreText != null) totalScoreText.text = $"누적 총점수 {NumberFormat.Comma(run.Score)}";

            BuildDetailRows(clear);
        }

        // "점수 상세" 토글 내용 — 웹 bd(ui.js:1619-1625) row 구성 그대로: 스테이지 보너스(고정 표시) →
        // 초과 EXP(있을 때만) → 남은 스핀(있을 때만) → 보스 보너스(보스일 때만) → 연승 보너스(있을
        // 때만) → "= 클리어 점수"(합계, 빚문서면 "0 (빚문서)"). Formulas 공개 상수를 UI에서 직접 읽는
        // 것은 RewardDoneView 등 기존 표시 전용 계산부와 동일 관례.
        private void BuildDetailRows(ClearOutcome clear)
        {
            if (detailRowsContent == null || detailRowTemplate == null) return;
            for (int i = detailRowsContent.childCount - 1; i >= 0; i--)
            {
                var child = detailRowsContent.GetChild(i);
                if (child == detailRowTemplate) continue;
                Destroy(child.gameObject);
            }

            long sBase = clear.clearedStage * 50L;
            long sLeft = clear.leftover * Formulas.SCORE_PER_LEFTOVER;
            long sSpins = clear.leftSpins * Formulas.SCORE_PER_LEFTSPIN;
            long sBoss = clear.boss ? Formulas.BOSS_CLEAR_SCORE : 0L;

            AddDetailRow($"스테이지 보너스 ({clear.clearedStage}×50)", "+" + NumberFormat.Comma(sBase), false);
            if (sLeft > 0) AddDetailRow($"초과 EXP ({NumberFormat.Comma(clear.leftover)}×2)", "+" + NumberFormat.Comma(sLeft), false);
            if (sSpins > 0) AddDetailRow($"남은 스핀 ({clear.leftSpins}×100)", "+" + NumberFormat.Comma(sSpins), false);
            if (clear.boss)
            {
                var boss = Bosses.For(clear.clearedStage);
                AddDetailRow($"보스 보너스 ({TextSanitize.StripAstral(boss?.name ?? "보스")})", "+" + NumberFormat.Comma(sBoss), false);
            }
            if (clear.streakBonus > 0) AddDetailRow("연승 보너스", "+" + NumberFormat.Comma(clear.streakBonus), false);
            string total = clear.inDebt ? "0 (빚문서)" : "+" + NumberFormat.Comma(clear.clearScore + clear.streakBonus);
            AddDetailRow("= 클리어 점수", total, true);
        }

        private void AddDetailRow(string label, string value, bool emphasize)
        {
            var row = Instantiate(detailRowTemplate, detailRowsContent);
            row.gameObject.SetActive(true);
            var labelText = row.Find("Inner/Label")?.GetComponent<Text>();
            var valueText = row.Find("Inner/Value")?.GetComponent<Text>();
            if (labelText != null) { labelText.text = label; labelText.color = emphasize ? UiKit.TextPrimary : UiKit.TextSecondary; }
            if (valueText != null) { valueText.text = value; valueText.color = emphasize ? UiKit.Accent : UiKit.TextSecondary; }
        }

        private void BuildCards(RunState run, Action<int> onChoose)
        {
            if (cardsContent == null || cardTemplate == null) return;
            for (int i = cardsContent.childCount - 1; i >= 0; i--)
            {
                var child = cardsContent.GetChild(i);
                if (child == cardTemplate) continue;
                Destroy(child.gameObject);
            }

            for (int i = 0; i < run.NodeOptions.Count; i++)
            {
                int idx = i;
                var (emoji, title, desc) = NodeKindInfo(run.NodeOptions[i]);

                var card = Instantiate(cardTemplate, cardsContent);
                card.gameObject.SetActive(true);
                card.name = "NodeCard_" + idx;

                var headText = card.Find("Content/Head")?.GetComponent<Text>();
                if (headText != null) headText.text = $"{emoji} {title}";
                var bodyText = card.Find("Content/Body")?.GetComponent<Text>();
                if (bodyText != null) bodyText.text = desc;

                var btn = card.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => onChoose(idx));
                }
            }
        }

        // S8 항목⑤: astral 이모지(✨🛡️🛒🎲🌑 등)는 레거시 Text에서 렌더링되지 않는다 — BMP 기호로 대체.
        private static (string emoji, string title, string desc) NodeKindInfo(NodeKind k)
        {
            switch (k)
            {
                case NodeKind.Augment: return ("★", "증강", "증강 3종 중 1개를 선택합니다.");
                case NodeKind.Relic: return ("◆", "유물", "유물 3종 중 1개를 선택합니다.");
                case NodeKind.Shop: return ("▲", "상점", "코인으로 증강·유물·아이템을 구매합니다.");
                // WEB_PARITY P1 ④: 코인 8 → 12(웹 game.js:1633).
                case NodeKind.Rest: return ("☕", "휴식", "코인 +12를 즉시 받습니다.");
                case NodeKind.Gamble: return ("♠", "도박", "보유 코인 전부를 걸고 50% 확률로 2배 또는 전부를 잃습니다.");
                case NodeKind.Event: return ("❓", "이벤트", "무작위 보상 이벤트가 발생합니다.");
                // WEB_PARITY P1 ④: 코인 15 → 30(웹 game.js:1673).
                case NodeKind.Curse: return ("●", "저주", "저주 1개를 받는 대신 코인 +30을 받습니다.");
                case NodeKind.Risk: return ("⚠", "위험", "프리즘/골드 증강과 저주를 동시에 받습니다.");
                // WEB_PARITY P1 ④: 보스 클리어 후에만 등장(RollNextNodes 신규 4번째 옵션).
                case NodeKind.Device: return ("■", "장치", "무작위 미보유 장치 1개를 오퍼합니다 — 장착 또는 코인 +15.");
                // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — AUGMENT 노드가 확률(기본10%+pity)로
                // 교체된 결과(웹 ui.js:1023 AUGLEVEL 헤더 "⬆️ 증강 강화").
                case NodeKind.AugLevel: return ("⬆", "증강 강화", "보유 증강 1개를 레벨업합니다 (Lv3 상한).");
                // 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #19/#20) — 심화 런 전용 노드 4종(StageFlow.
                // RollDeepNodes만 생성, 일반 런엔 절대 등장하지 않음). 웹 ui.js POUCH 오퍼 헤더/§9.2 J3
                // JACKPOT 헤더 문구를 그대로 옮겼다(선택 시 EventPouch/EventAugment/EventRelic 오퍼
                // 패널로 이어진다 — 카드 확정은 그 패널이 담당, 여기는 3택 카드 라벨만). S8 항목⑤:
                // 웹 원문 이모지(🎒🎰)는 astral(U+1F300+)이라 레거시 Text가 렌더링 못 한다 — 기존
                // NodeKindInfo 관례(★◆▲☕♠ 등 전부 BMP)와 동일하게 BMP 기호로 대체.
                case NodeKind.Pouch: return ("◈", "심볼 주머니", "특수 심볼 카드를 덱에 추가합니다(기본 심볼 제거 비용).");
                case NodeKind.Jackpot: return ("✪", "잭팟 노드", "최다 잭팟 태그 기준 특수 심볼을 오퍼합니다.");
                case NodeKind.SymAug: return ("✨", "심볼 증강", "심볼증강 · 관련 증강 중 1개를 선택합니다.");
                case NodeKind.SymRel: return ("◆", "심볼 유물", "심볼유물 · 관련 유물 중 1개를 선택합니다.");
                default: return ("❔", k.ToString(), "");
            }
        }
    }
}
