using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // GameOver 페이즈 패널 — ENGINE_PORT_DESIGN.md S7 Run/Panels/GameOverPanel.cs. "GameOver: 딤 0.3s →
    // 패널 스케일인 → 신규 업적 스태거 리스트(0.05s 간격)"(설계 수치 그대로). 이관 원본:
    // Scripts/UI/RunPanels.cs의 BuildGameOver.
    //
    // S14 §E — "화면 채도 0.5로 0.4s 페이드"(게임오버 진입). uGUI에는 saturate() 필터가 없어 S12 §7
    // 재해석 규칙("filter: saturate/brightness → CanvasGroup 알파 또는 색 곱") 그대로, 기존 검정
    // 스크림(dimGroup)의 페이드를 "채도 저하"의 근사로 재사용하고 지속시간만 0.3s→0.4s로 맞췄다
    // (완전한 채도 필터를 새로 만들지 않음 — 재해석 보고 대상).
    public sealed class GameOverPanel : MonoBehaviour
    {
        private const float DimDuration = 0.4f; // S14 §E 명시: "0.4s 페이드"(원래 0.3s에서 갱신)
        private const float ScaleInDuration = 0.35f; // 설계 미명시 — 패널 스케일인 길이 기본값
        private const float AchStagger = 0.05f; // 설계 명시

        // ── 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 C, 웹 renderEnd endxp 블록 ui.js:2117-2124) ──
        private const float XpCountUpDuration = 0.4f; // 설계 미명시 — GainPanel 대문짝 카운트업(0.35s)과 같은 급
        private const float XpBarAnimDuration = 0.4f;
        private const float LevelUpPopDuration = 0.3f; // 설계 미명시 — GainPanel 대문짝 팝인(0.3s)과 동일 관례
        private const float LevelUpPopScaleFrom = 0.7f;

        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Text titleScoreText;
        [SerializeField] private Text finalScoreText;
        [SerializeField] private Text stageReachedText;
        [SerializeField] private Text recordsText;
        [SerializeField] private RectTransform achHeaderRow;
        [SerializeField] private Text achHeaderText;
        [SerializeField] private RectTransform achContent;
        [SerializeField] private RectTransform achRowTemplate; // 자식 경로 계약: Label(Text)
        [SerializeField] private Text achTotalText;

        // ── P4 C — 런종료 XP 블록(웹 endxp) ─────────────────────────────────────────────────
        [SerializeField] private Text xpTopLevelText;      // "플레이어 레벨 Lv.N"
        [SerializeField] private Text xpGainText;          // "+N XP"
        [SerializeField] private RectTransform xpLevelUpRoot; // 레벨업 시에만 노출
        [SerializeField] private Text xpLevelUpText;       // "레벨 업! Lv.A → Lv.B"
        [SerializeField] private RectTransform xpBarFill;  // anchorMax.x = ratio
        [SerializeField] private Image xpBarFillImage;
        [SerializeField] private Text xpNextText;          // "다음 레벨까지 N XP" / "최고 레벨 달성"

        [SerializeField] private Button menuButton;

        private ParticleSystem _gameOverFx; // S7c 연출 훅: 표시 중 fx_gameover 루프
        private Coroutine _xpRoutine;

        private void Awake()
        {
            if (achRowTemplate != null) achRowTemplate.gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
        }

        public void Show(GameSession session, FailureOutcome failure, Action onMenu)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            var run = session.State;
            var profile = session.Profile;
            long finalScore = failure?.finalScore ?? 0;

            // WEB_PARITY P1 ⑤: 자발적 포기(voluntary)면 점수티어 플레이버 타이틀 대신 "포기 — 즉시 결산"
            // 계열 문구로 실패 프레이밍을 생략한다(웹 game.js:2547 "🏁 스테이지 N에서 런을 종료했어요 —
            // 지금까지 점수로 결산!" 대응. astral 이모지 금지·한글만).
            bool voluntary = failure != null && failure.Voluntary;
            var (titleEmoji, titleLabel) = Formulas.ScoreTitle(finalScore);
            if (titleScoreText != null) titleScoreText.text = voluntary ? "포기 — 즉시 결산" : $"{titleEmoji} {titleLabel}";
            if (finalScoreText != null) finalScoreText.text = $"최종 점수 {NumberFormat.Comma(finalScore)}";
            if (stageReachedText != null) stageReachedText.text = $"도달 스테이지 {run.Stage}";
            if (recordsText != null)
                recordsText.text = $"최고점수 {NumberFormat.Comma(profile.BestScore)} · 최고 스테이지 {profile.BestStage} · 통산 런 {profile.Runs}";

            var newAch = session.LastNewAchievements;
            bool hasNew = newAch != null && newAch.Count > 0;
            if (achHeaderRow != null) achHeaderRow.gameObject.SetActive(hasNew);
            // S8 항목⑤: 🏅(astral)는 렌더링되지 않는다 — 한글 라벨만 사용.
            if (achHeaderText != null) achHeaderText.text = hasNew ? $"신규 업적 {newAch.Count}개" : "";
            if (achTotalText != null) achTotalText.text = $"업적 {profile.AchievedIds.Count}/{Achievements.Count}";

            // ── P4 C — 런종료 XP 블록. failure는 kind=="GAME_OVER"일 때만 유효하지만(FailureOutcome
            // 헤더 주석), 이 패널은 항상 그 조건에서만 열리므로(RunView가 GAME_OVER에서만 Show 호출)
            // null 가드만 하고 별도 kind 분기는 두지 않는다 — PlayerXpGain 등은 GAME_OVER가 아니면 항상
            // 기본값 0이라 안전.
            bool leveled = failure != null && failure.PlayerLevelAfter > failure.PlayerLevelBefore;
            var lp = profile.LevelProgress();
            long xpGain = failure?.PlayerXpGain ?? 0L;

            if (xpTopLevelText != null) xpTopLevelText.text = $"플레이어 레벨 Lv.{lp.Level}";
            if (xpLevelUpRoot != null) xpLevelUpRoot.gameObject.SetActive(leveled);
            if (leveled && xpLevelUpText != null && failure != null)
                xpLevelUpText.text = $"레벨 업! Lv.{failure.PlayerLevelBefore} → Lv.{failure.PlayerLevelAfter}";
            if (xpNextText != null)
                xpNextText.text = lp.Max ? "최고 레벨 달성" : $"다음 레벨까지 {NumberFormat.Comma(lp.Need - lp.InLevel)} XP";
            // 웹은 MAX 레벨이라고 바 색을 바꾸지 않는다(무색 — 항상 Accent) — Unity도 그대로 맞춘다.
            if (xpBarFillImage != null) xpBarFillImage.color = UiKit.Accent;

            if (menuButton != null)
            {
                menuButton.onClick.RemoveAllListeners();
                menuButton.onClick.AddListener(() => onMenu());
            }

            // S7c 연출 훅: "GameOverPanel: 표시 중 GameOver 루프." — 이미 재생 중이면 다시 시작하지 않는다.
            if (_gameOverFx == null && cardRect != null) _gameOverFx = FxKit.I?.PlayLoop(FxId.GameOver, cardRect);

            var rows = BuildAchRows(newAch);
            if (_xpRoutine != null) StopCoroutine(_xpRoutine);
            if (firstShow)
            {
                // Opus 2차 검수(P4 1/3) 필수② — XP 블록 연출은 카드가 실제로 보이기 시작한 "뒤"에
                // 재생해야 한다(카드가 scale 0으로 접혀 있는 동안 카운트업/바 채움이 끝나버리면 사용자는
                // 아무 것도 못 본다). EnterRoutine(딤 0.4s+스케일인 0.35s+업적 스태거)이 전부 끝난 뒤에
                // 이어서 재생하도록 하나의 코루틴으로 합쳤다.
                _xpRoutine = StartCoroutine(EnterThenXpRoutine(rows, xpGain, lp, leveled));
            }
            else
            {
                if (dimGroup != null) dimGroup.alpha = 1f;
                if (cardRect != null) cardRect.localScale = Vector3.one;
                foreach (var r in rows) r.localScale = Vector3.one;
                // 이미 카드가 보이는 상태(예: 재입장/갱신)라면 지연 없이 바로 재생.
                _xpRoutine = StartCoroutine(AnimateXpBlockRoutine(xpGain, lp, leveled));
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            _xpRoutine = null; // StopAllCoroutines가 이미 멎였다 — 참조만 정리(재입장 시 stale 판단 방지)
            gameObject.SetActive(false);
            if (_gameOverFx != null)
            {
                FxKit.I?.StopLoop(_gameOverFx);
                _gameOverFx = null;
            }
        }

        // firstShow 전용 — EnterRoutine(딤+스케일인+업적 스태거)이 끝난 뒤에야 XP 연출을 시작한다.
        private IEnumerator EnterThenXpRoutine(List<RectTransform> achRows, long xpGain, Formulas.PlayerLevelProgress lp, bool leveled)
        {
            yield return EnterRoutine(achRows);
            yield return AnimateXpBlockRoutine(xpGain, lp, leveled);
        }

        // P4 C — "+N XP" 카운트업(GainPanel 대문짝과 동일 관례) + XP 바 채움(레벨 유지 시 트윈, 레벨업
        // 시 즉시 반영 + 레벨업 배너 팝인). 웹은 정적으로 한 번에 그리지만(ui.js:2119-2124), 이 프로젝트의
        // 기존 연출 관례(HudView.AnimateExpRoutine·GainPanel.ShowRoutine)를 따라 최소한의 강조를 더했다.
        private IEnumerator AnimateXpBlockRoutine(long xpGain, Formulas.PlayerLevelProgress lp, bool leveled)
        {
            float targetPct = lp.Max ? 1f : (float)lp.Ratio;

            if (xpGainText != null) xpGainText.text = "+0 XP";

            if (leveled)
            {
                // 레벨업 시엔 바가 "새 레벨" 기준으로 다시 시작하므로(이전 레벨의 need와 무관) 트윈 대신
                // 즉시 목표값을 반영하고, 대신 레벨업 배너를 팝인시켜 강조한다(GainPanel 대문짝 팝인과
                // 동일 이징 — OutBack).
                if (xpBarFill != null) xpBarFill.anchorMax = new Vector2(Mathf.Clamp01(targetPct), 1f);
                if (xpLevelUpRoot != null)
                {
                    xpLevelUpRoot.localScale = Vector3.one * LevelUpPopScaleFrom;
                    yield return UiTween.ScaleRoutine(xpLevelUpRoot, Vector3.one * LevelUpPopScaleFrom, Vector3.one,
                        LevelUpPopDuration, UiTween.Ease.OutBack);
                }
            }
            else if (xpBarFill != null)
            {
                // 레벨이 그대로면 need가 이번 런 동안 불변이므로 "gain 전" inLevel을 정확히 역산할 수
                // 있다(HudView.AnimateExpRoutine과 동일한 "이전값→현재값 트윈" 패턴). MAX 레벨(need==0)
                // 이면 gain 전에도 이미 100%였으므로(더 채울 여지가 없다) fromPct도 targetPct(=1)와
                // 같게 고정해 트윈을 사실상 생략한다(Opus 2차검수 필수③ — 예전엔 lp.Need>0이 거짓이라
                // fromPct가 0으로 떨어져 "0→100%" 채움 트윈이 잘못 재생됐다).
                float fromPct;
                if (lp.Max)
                {
                    fromPct = 1f;
                }
                else
                {
                    long fromInLevel = Math.Max(0L, lp.InLevel - xpGain);
                    fromPct = lp.Need > 0 ? Mathf.Clamp01((float)fromInLevel / lp.Need) : 0f;
                }
                xpBarFill.anchorMax = new Vector2(fromPct, 1f);
                yield return UiTween.FloatRoutine(fromPct, targetPct, XpBarAnimDuration,
                    v => { if (xpBarFill != null) xpBarFill.anchorMax = new Vector2(v, 1f); }, UiTween.Ease.OutCubic);
            }

            if (xpGainText != null)
                yield return UiTween.CountUpRoutine(0L, xpGain, XpCountUpDuration,
                    v => { if (xpGainText != null) xpGainText.text = $"+{NumberFormat.Comma(v)} XP"; }, UiTween.Ease.OutCubic);

            _xpRoutine = null;
        }

        private IEnumerator EnterRoutine(List<RectTransform> achRows)
        {
            if (cardRect != null) cardRect.localScale = Vector3.zero;
            foreach (var r in achRows) if (r != null) r.localScale = Vector3.zero;

            yield return UiTween.FadeRoutine(dimGroup, 0f, 1f, DimDuration);
            if (cardRect != null)
                yield return UiTween.ScaleRoutine(cardRect, Vector3.zero, Vector3.one, ScaleInDuration, UiTween.Ease.OutBack);

            for (int i = 0; i < achRows.Count; i++)
            {
                if (achRows[i] != null) StartCoroutine(UiTween.ScaleRoutine(achRows[i], Vector3.zero, Vector3.one, 0.2f, UiTween.Ease.OutBack));
                if (i < achRows.Count - 1) yield return new WaitForSeconds(AchStagger);
            }
        }

        private List<RectTransform> BuildAchRows(IReadOnlyList<AchDef> newAch)
        {
            var result = new List<RectTransform>();
            if (achContent == null || achRowTemplate == null) return result;

            for (int i = achContent.childCount - 1; i >= 0; i--)
            {
                var child = achContent.GetChild(i);
                if (child == achRowTemplate) continue;
                Destroy(child.gameObject);
            }

            if (newAch == null) return result;
            for (int i = 0; i < newAch.Count; i++)
            {
                var a = newAch[i];
                var row = Instantiate(achRowTemplate, achContent);
                row.gameObject.SetActive(true);
                row.name = "Ach_" + a.id;
                result.Add(row);
                var label = row.Find("Label")?.GetComponent<Text>();
                // WEB_PARITY P3-2 — 웹 업적 이모지가 astral이면 Achievements.cs가 emoji=""로 대체한다
                // (S8 항목⑤ 선례 그대로, Achievements.cs 헤더 각주 참조) — 빈 이모지일 때 앞 공백이
                // 남지 않도록 분기한다. name/desc는 데이터 원문을 그대로 보존하므로(웹 문장 중간에
                // 박힌 이모지, 예: "체리 계열(🍒+🍑)") TextSanitize.StripAstral로 표시 직전에만 걸러낸다
                // (Opus 2차 검수 저⑤, Core/TextSanitize.cs 헤더 참조 — 데이터는 미수정).
                if (label != null)
                {
                    string name = TextSanitize.StripAstral(a.name);
                    string desc = TextSanitize.StripAstral(a.desc);
                    label.text = string.IsNullOrEmpty(a.emoji) ? $"{name} — {desc}" : $"{a.emoji} {name} — {desc}";
                }
            }
            return result;
        }
    }
}
