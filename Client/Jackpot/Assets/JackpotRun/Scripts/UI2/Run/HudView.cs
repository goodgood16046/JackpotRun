using System.Collections;
using JackpotRun.Core;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 상단 HUD — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 Run/HudView.cs: 스테이지·EXP 진행바·남은 스핀·
    // 코인·점수·저주·불운 게이지·보스 테두리+진입 배너. 이관 원본: Scripts/UI/RunScreen.cs의
    // BuildHud/RefreshHud(레이아웃은 버리고 로직만) + S7 "RunView 연출" 지시(EXP 바 트윈·골드 펄스·
    // 불운 게이지 펄스·보스 적색 틴트+배너)를 얹었다.
    //
    // S14 §D — HUD 피드백 강화: EXP 바 선두 광점 + 80%↑ 골드 펄스(지속) + 초과 시 초록 전환(지속),
    // 남은 스핀 ≤1 적색 점멸, 저주 증가 시 보라 플래시, 보스 진입 시 적색 비네트 펄스 + 0.4s 셰이크
    // (배너 드롭은 S7c부터 이미 있던 것 유지).
    //
    // 연출 수치 중 설계 문서에 명시되지 않은 것(펄스/배너 길이 등)은 이 파일 안에서 합리적 기본값을
    // 상수로 못박아 뒀다 — 명시된 값(EXP CountUp 0.3s)과 헷갈리지 않도록 주석으로 구분.
    public sealed class HudView : MonoBehaviour
    {
        private const float ExpGainDuration = 0.3f; // 설계 명시: "EXP CountUp(0.3s)"
        private const float QuotaPulseDuration = 0.35f; // 설계 미명시 — 골드 펄스 기본값
        private const float BossBannerHold = 1.4f; // 설계 미명시 — 보스 진입 배너 유지 시간
        private const float BossBannerFade = 0.25f;
        private const float GaugePulseDuration = 0.3f; // 설계 미명시 — 불운 게이지 만땅 펄스

        // ── S14 §D 신규 상수 ─────────────────────────────────────────────────────────
        private const float ExpNearQuotaThreshold = 0.8f; // 설계 명시: "목표 근접(≥80%)"
        private const float ExpPulseHalfCycle = 0.4f; // 설계 미명시 — 80%↑ 골드 펄스 주기 절반
        private const float ExpLeadDotPulseHalfCycle = 0.5f; // 설계 미명시 — 선두 광점 깜빡임 주기 절반
        private const float SpinsBlinkHalfCycle = 0.4f; // 설계 명시: "0.8s 주기"의 절반
        private const float CurseFlashDuration = 0.35f; // 설계 미명시 — 저주 증가 보라 플래시 길이
        private const float BossVignetteInDuration = 0.2f; // 설계 미명시
        private const float BossVignetteOutDuration = 0.3f;
        private const float BossVignetteAlpha = 0.5f; // 설계 미명시 — 비네트 펄스 최대 알파
        private const float BossEntryShakeAmplitude = 5f; // 설계 미명시 — 보스 진입 셰이크 진폭
        private const float BossEntryShakeDuration = 0.4f; // 설계 명시: "0.4s 셰이크"
        private const float CoinFlyDuration = 0.5f; // FxPrefabGen.Build_Coin main.duration과 일치(설계 "도착 시")
        private const float CoinPulseInDuration = 0.08f;
        private const float CoinPulseOutDuration = 0.07f;
        private const float CoinPulseScale = 1.15f; // 설계 명시: "scale 1.15"

        [SerializeField] private Text stageText;
        [SerializeField] private Text cursesText;
        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 renderPlay() HUD `st.asc>0`일 때만
        // "🎓 심화 N ×M" 배지 표시, ui.js:704) — astral 이모지 금지(S8 항목⑤), "심화 N ×M" 텍스트만.
        [SerializeField] private Text ascBadgeText;
        [SerializeField] private RectTransform expBarFill; // anchorMax.x를 0..1로 움직인다(RunScreen.cs와 동일 방식)
        [SerializeField] private Image expBarFillImage; // 골드 펄스/초록 전환 대상
        [SerializeField] private Text expBarText;
        [SerializeField] private RectTransform expLeadDot; // S14 §D — 채움 끝점에 붙어 따라다니는 광점(부모=expBarFill)
        [SerializeField] private Text spinsText;
        [SerializeField] private Text coinsText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Outline hudOutline; // 보스 스테이지 적색 틴트(UiKit.AddGlowOutline로 생성)
        [SerializeField] private Image[] unluckyPips = System.Array.Empty<Image>(); // 5칸 고정
        [SerializeField] private CanvasGroup bossBannerGroup;
        [SerializeField] private RectTransform bossBannerRect;
        [SerializeField] private Text bossBannerText;
        [SerializeField] private CanvasGroup bossVignetteGroup; // S14 §D — 보스 진입 적색 비네트 펄스
        // S14 §G — 셰이크는 이 화면(RunScreen) 루트에만 적용. ReelView가 보유한 것과 동일한 참조를
        // HudView도 별도로 갖는다(보스 진입 셰이크가 릴 착지 셰이크와 겹칠 수 있어 완전히 공유하는
        // 조정자를 새로 두진 않았다 — 드물게 겹치면 한쪽이 다른 쪽을 살짝 끊고 재시작할 수 있다는
        // 것을 알려진 한계로 남겨둔다, 보고 대상).
        [SerializeField] private RectTransform runScreenRoot;

        // ── 웹 파리티 P7-4b(WEB_PARITY_DESIGN.md §1-A #19/#20, 웹 archHudChip/feverHudHtml ui.js:38-56) ──
        [SerializeField] private Text archChipText;   // "{emoji}{이름}★"(2차 전공만 별) — 비활성 시 빈 문자열.
        [SerializeField] private Text feverText;      // 대기 중 "피버 N%" / 발동 중 "피버 N스핀 남음"(적색).
        [SerializeField] private RectTransform feverBarBg;   // Opus 2차검수(P7-4b) [경미②] — 일반 런은
        // 통째로 숨긴다(빈 진행바 트랙만 남는 시각적 잔여물 방지 — archChipText/feverText는 "행 유지,
        // 텍스트만 비움" 관례를 쓰지만 이 바는 트랙 자체가 배경색 있는 그래픽이라 빈 칸이 더 도드라짐).
        [SerializeField] private RectTransform feverBarFill; // anchorMax.x = 게이지 비율(0~1). 발동 중엔 100% 고정.

        private string _shownBossId; // 마지막으로 배너를 띄운 보스 id(null=아직 없음/보스 아님) — 재진입 배너 중복 방지
        private int _lastCurseCount = -1; // -1=미초기화(첫 표시에는 플래시하지 않는다)
        private Coroutine _expRoutine;
        private Coroutine _expPulseRoutine;
        private Coroutine _spinsBlinkRoutine;
        private Coroutine _bossBannerRoutine;
        private Coroutine _gaugePulseRoutine;
        private Coroutine _leadDotRoutine;
        private Coroutine _screenShakeRoutine;
        private ParticleSystem _bossFx; // S7c 연출 훅: 보스 스테이지 동안 유지되는 fx_boss 루프 핸들
        private ParticleSystem _ambientFx; // S15 §B 표 "배경(런 화면)" — 은은한 부유 광입자 루프 핸들

        private void Awake()
        {
            if (bossBannerGroup != null) bossBannerGroup.alpha = 0f;
            if (bossVignetteGroup != null) bossVignetteGroup.alpha = 0f;
            if (expLeadDot != null) _leadDotRoutine = StartCoroutine(ExpLeadDotPulseLoop());
        }

        // S15 §B — runScreenRoot는 이미 S14 §G 셰이크 공용으로 와이어링된 기존 참조를 그대로 재사용한다
        // (새 앵커 추가 없음). MenuView의 fx_ui_aura 루프(OnEnable 시작/OnDisable 정지)와 동일 패턴 —
        // PlaySceneRoot가 LoadSceneMode.Single로 씬을 통째로 교체하므로 화면을 떠날 때 OnDisable이
        // 확실히 호출된다(RunView.OnDisable의 기존 주석 참조).
        private void OnEnable()
        {
            if (_ambientFx == null) _ambientFx = FxKit.I?.PlayLoop(FxId.RunAmbient, runScreenRoot);
        }

        private void OnDisable()
        {
            FxKit.I?.StopLoop(_ambientFx);
            _ambientFx = null;
        }

        /// <summary>애니메이션 없이 전체를 즉시 갱신 — 화면 진입 첫 표시(RUN_STARTED 등)에 사용.</summary>
        public void RefreshInstant(RunState run, (long quota, int spins) preview)
        {
            RefreshStageCurses(run);
            RefreshSpinsCoinsScore(run, preview);
            SetExpBarImmediate(run.StageExp, preview.quota);
            RefreshUnluckyGauge(run.UnluckyGauge, false);
            RefreshBossState(run);
            RefreshDeepStatus(run);
        }

        /// <summary>스핀 등으로 EXP/코인/점수/저주/스핀수가 바뀌었을 때 — EXP 바만 0.3s 트윈, 나머지는 즉시.</summary>
        public void RefreshAfterSpin(RunState run, (long quota, int spins) preview, long expBefore)
        {
            RefreshStageCurses(run);
            RefreshSpinsCoinsScore(run, preview);
            RefreshUnluckyGauge(run.UnluckyGauge, true);
            RefreshBossState(run);
            RefreshDeepStatus(run);

            if (_expRoutine != null) StopCoroutine(_expRoutine);
            _expRoutine = StartCoroutine(AnimateExpRoutine(expBefore, run.StageExp, preview.quota));
        }

        // S14 §D — 저주 증가 시 HUD 테두리 보라 플래시(설계 그대로). 최초 표시(초기화 전)에는
        // 비교 기준이 없어 플래시하지 않는다.
        private void RefreshStageCurses(RunState run)
        {
            if (stageText != null) stageText.text = $"스테이지 {run.Stage}";
            // S8 항목⑤: 🌑(astral)는 레거시 Text에서 렌더링되지 않는다 — 한글 라벨만 사용.
            if (cursesText != null) cursesText.text = $"저주 {run.Curses.Count}";

            int count = run.Curses.Count;
            if (_lastCurseCount >= 0 && count > _lastCurseCount) StartCoroutine(CurseFlashRoutine());
            _lastCurseCount = count;

            // 웹 파리티 P6(웹 ui.js:704 `${st.asc>0 ? ...}`) — 승천 런에서만 표시. Opus 2차검수 권장—
            // 빈 문자열 대입 대신 SetActive(false)로 완전히 비활성화한다(HorizontalLayoutGroup은 비활성
            // 자식을 레이아웃에서 제외하므로, 일반 런은 이 칸이 차지하던 폭을 stageText/cursesText가
            // 되돌려 받는다 — 텍스트만 지우면 빈 칸이 자리를 계속 차지했다). astral 🎓 금지, 한글만.
            //
            // 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #19/#20, 웹 ui.js:705 deep-hud "🎒{total}/
            // {totalMax} +{penalty}%") — 승천(asc)과 심화(deepMode)는 RunController가 항상 상호배제
            // 시키므로(deep이면 asc가 무조건 0으로 강제) 이 배지 슬롯 하나를 두 모드가 절대 겹치지
            // 않고 공유할 수 있다 — 새 UI 요소를 추가하는 대신(신규 위젯 최소화 원칙) 기존
            // ascBadgeText를 심화 배지로도 재사용한다. astral 🎒 금지 — NodePanel.Pouch와 동일하게
            // BMP 기호 "◈"로 대체.
            if (ascBadgeText != null)
            {
                if (run.DeepMode)
                {
                    ascBadgeText.gameObject.SetActive(true);
                    int total = Pouch.Total(run.Pouch);
                    var bounds = RepairShop.Bounds(run);
                    double penalty = DeepRunHooks.DeepPenalty(run);
                    string penSuffix = penalty > 1.0 + 1e-9 ? $" +{Mathf.RoundToInt((float)((penalty - 1.0) * 100.0))}%" : "";
                    ascBadgeText.text = $"◈ {total}/{bounds.totalMax}{penSuffix}";
                }
                else
                {
                    bool showAsc = run.Asc > 0;
                    ascBadgeText.gameObject.SetActive(showAsc);
                    if (showAsc) ascBadgeText.text = $"심화 {run.Asc} ×{NumberFormat.Fmt(AscMods.Get(run.Asc).ScoreMul)}";
                }
            }
        }

        // ── 웹 파리티 P7-4b(웹 archHudChip/feverHudHtml, ui.js:38-56) — 전공 칩 + 피버 게이지/발동중 ──
        // 일반 런은 전부 빈 문자열/게이지 0으로 되돌린다(행 자체는 항상 존재 — 빌더 각주 참조).
        private void RefreshDeepStatus(RunState run)
        {
            bool deep = run.DeepMode;

            if (archChipText != null)
            {
                if (deep)
                {
                    var arch = Archetypes.PouchArchetype(run.Pouch);
                    // Opus 2차검수(P7-4b) [중대③] — arch.Emoji가 astral일 수 있다(RunView.ShowPouchBoard와
                    // 동일 근거) — StripAstral로 이모지만 지운다.
                    archChipText.text = arch.Tier > 0
                        ? $"{TextSanitize.StripAstral(arch.Emoji)}{arch.Name}{(arch.Tier >= 2 ? "★" : "")}"
                        : "";
                }
                else archChipText.text = "";
            }

            if (feverText != null)
            {
                if (!deep) { feverText.text = ""; }
                else if (run.FeverSpins > 0)
                {
                    feverText.text = $"피버 {run.FeverSpins}스핀 남음";
                    feverText.color = UiKit.Bad;
                }
                else
                {
                    double pct = Pouch.FeverMax > 0 ? System.Math.Min(100.0, run.FeverGauge / Pouch.FeverMax * 100.0) : 0.0;
                    feverText.text = $"피버 {Mathf.RoundToInt((float)pct)}%";
                    feverText.color = UiKit.TextSecondary;
                }
            }

            if (feverBarBg != null) feverBarBg.gameObject.SetActive(deep);

            if (feverBarFill != null)
            {
                float pct = 0f;
                if (deep)
                {
                    pct = run.FeverSpins > 0 ? 1f
                        : (Pouch.FeverMax > 0 ? Mathf.Clamp01((float)(run.FeverGauge / Pouch.FeverMax)) : 0f);
                }
                feverBarFill.anchorMax = new Vector2(pct, 1f);
                var img = feverBarFill.GetComponent<Image>();
                if (img != null) img.color = run.FeverSpins > 0 ? UiKit.Bad : UiKit.Accent;
            }
        }

        // S14 §D — 남은 스핀 1회 이하일 때 스핀 카운터를 적색으로 점멸(0.8s 주기).
        private void RefreshSpinsCoinsScore(RunState run, (long quota, int spins) preview)
        {
            int spinsLeft = 0;
            if (spinsText != null)
            {
                spinsLeft = Mathf.Max(preview.spins - run.SpinIndex, 0);
                spinsText.text = $"남은 스핀 {spinsLeft}";
            }

            if (spinsText != null)
            {
                if (spinsLeft <= 1)
                {
                    if (_spinsBlinkRoutine == null) _spinsBlinkRoutine = StartCoroutine(SpinsBlinkLoop());
                }
                else if (_spinsBlinkRoutine != null)
                {
                    StopCoroutine(_spinsBlinkRoutine);
                    _spinsBlinkRoutine = null;
                    spinsText.color = UiKit.TextSecondary;
                }
            }

            // Fable 육안 검수 지시(2026-07-31): 코인 라벨이 뭘 뜻하는지 불명확 — 원인은 🪙(U+1FA99) 같은
            // 색상 이모지(astral-plane, 서로게이트 쌍)가 레거시 uGUI Text에서 렌더링되지 않는 것으로
            // 추정된다(⭐☠처럼 BMP 범위 기존 기호 문자는 렌더링됨 — RunView 필드 참고). 이모지에만
            // 의존하지 않고 한글 라벨을 항상 붙인다.
            if (coinsText != null) coinsText.text = $"코인 {NumberFormat.Comma(run.Coins)}";
            if (scoreText != null) scoreText.text = $"점수 {NumberFormat.Comma(run.Score)}";
        }

        private IEnumerator SpinsBlinkLoop()
        {
            if (spinsText == null) yield break;
            while (true)
            {
                yield return UiTween.FloatRoutine(0f, 1f, SpinsBlinkHalfCycle,
                    t => { if (spinsText != null) spinsText.color = Color.Lerp(UiKit.TextSecondary, UiKit.Bad, t); }, UiTween.Ease.Linear);
                if (spinsText == null) yield break;
                yield return UiTween.FloatRoutine(1f, 0f, SpinsBlinkHalfCycle,
                    t => { if (spinsText != null) spinsText.color = Color.Lerp(UiKit.TextSecondary, UiKit.Bad, t); }, UiTween.Ease.Linear);
            }
        }

        // S14 §D — HUD 테두리(hudOutline)를 잠깐 보라색으로 덮었다 원래 상태(보스면 적색 유지, 아니면
        // 비활성)로 복구한다. NearMissFlashRoutine(ReelView)과 동일 패턴.
        private IEnumerator CurseFlashRoutine()
        {
            if (hudOutline == null) yield break;
            bool wasEnabled = hudOutline.enabled;
            Color prevColor = hudOutline.effectColor;
            hudOutline.enabled = true;
            hudOutline.effectColor = UiKit.Purple;
            yield return UiTween.FloatRoutine(0f, 1f, CurseFlashDuration * 0.5f,
                a => SetOutlineAlpha(hudOutline, a), UiTween.Ease.OutQuad);
            if (hudOutline == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0f, CurseFlashDuration * 0.5f,
                a => SetOutlineAlpha(hudOutline, a), UiTween.Ease.OutQuad);
            if (hudOutline == null) yield break;
            hudOutline.effectColor = prevColor;
            hudOutline.enabled = wasEnabled;
        }

        private static void SetOutlineAlpha(Outline outline, float a)
        {
            if (outline == null) return;
            var c = outline.effectColor;
            c.a = a;
            outline.effectColor = c;
        }

        private void SetExpBarImmediate(long exp, long quota)
        {
            float pct = quota > 0 ? Mathf.Clamp01((float)((double)exp / quota)) : 0f;
            if (expBarFill != null) expBarFill.anchorMax = new Vector2(pct, 1f);
            if (expBarText != null) expBarText.text = $"{NumberFormat.Comma(exp)} / {NumberFormat.Comma(quota)}";
            UpdateExpBarStyle(pct);
        }

        // S14 §D — 80%↑ 골드 펄스(지속), 100%↑ 초록 전환(지속). 기본색(UiKit.Good=teal)과 겹치지
        // 않도록 "초과" 상태는 UiKit.Green(#4ade80, 실제 초록)을 쓴다.
        private void UpdateExpBarStyle(float pct)
        {
            if (expBarFillImage == null) return;
            if (pct >= 1f)
            {
                if (_expPulseRoutine != null) { StopCoroutine(_expPulseRoutine); _expPulseRoutine = null; }
                expBarFillImage.color = UiKit.Green;
            }
            else if (pct >= ExpNearQuotaThreshold)
            {
                if (_expPulseRoutine == null) _expPulseRoutine = StartCoroutine(ExpNearQuotaPulseLoop());
            }
            else
            {
                if (_expPulseRoutine != null) { StopCoroutine(_expPulseRoutine); _expPulseRoutine = null; }
                expBarFillImage.color = UiKit.Good;
            }
        }

        private IEnumerator ExpNearQuotaPulseLoop()
        {
            while (true)
            {
                yield return UiTween.FloatRoutine(0f, 1f, ExpPulseHalfCycle,
                    t => { if (expBarFillImage != null) expBarFillImage.color = Color.Lerp(UiKit.Good, UiKit.Accent, t); }, UiTween.Ease.OutQuad);
                if (expBarFillImage == null) yield break;
                yield return UiTween.FloatRoutine(1f, 0f, ExpPulseHalfCycle,
                    t => { if (expBarFillImage != null) expBarFillImage.color = Color.Lerp(UiKit.Good, UiKit.Accent, t); }, UiTween.Ease.OutQuad);
            }
        }

        // S14 §D — EXP 바 "선두에 흐르는 광점". expLeadDot은 빌더가 expBarFill의 우측 끝(anchorMin/Max
        // x=1)에 자식으로 붙여, expBarFill.anchorMax.x가 바뀔 때마다 자동으로 채움 끝점을 따라간다
        // (별도 위치 갱신 코드 불필요) — 여기서는 은은한 알파 깜빡임만 반복한다.
        private IEnumerator ExpLeadDotPulseLoop()
        {
            var img = expLeadDot.GetComponent<Image>();
            if (img == null) yield break;
            while (true)
            {
                yield return UiTween.FloatRoutine(0.4f, 1f, ExpLeadDotPulseHalfCycle,
                    a => { if (img != null) { var c = img.color; c.a = a; img.color = c; } }, UiTween.Ease.OutQuad);
                if (img == null) yield break;
                yield return UiTween.FloatRoutine(1f, 0.4f, ExpLeadDotPulseHalfCycle,
                    a => { if (img != null) { var c = img.color; c.a = a; img.color = c; } }, UiTween.Ease.OutQuad);
            }
        }

        private IEnumerator AnimateExpRoutine(long fromExp, long toExp, long quota)
        {
            bool crossedQuota = quota > 0 && fromExp < quota && toExp >= quota;
            // S7c 연출 훅: "EXP 채움 중 ExpGain" — 바 끝점(expBarFill)에서 1회 재생.
            if (toExp > fromExp) FxKit.I?.Play(FxId.ExpGain, expBarFill);
            yield return UiTween.CountUpRoutine(fromExp, toExp, ExpGainDuration, v => SetExpBarImmediate(v, quota),
                UiTween.Ease.OutCubic);
            if (crossedQuota) PlayQuotaPulse();
            _expRoutine = null;
        }

        /// <summary>S7c 연출 훅: "코인 증가 시 Coin(릴→코인 라벨 flyTo)" — RunView가 스핀 결과의
        /// coinsGained를 이미 알고 있어 여기로 직접 넘긴다(로직 변경 없음, 호출 추가). S14 §C — 파티클
        /// 도착 시(대략 CoinFlyDuration 후) 코인 라벨을 0.15s간 펄스(scale 1.15)한다. S15 §B 표 "코인
        /// 획득" — arcHeight로 포물선 비행을 근사하고(중력은 이 시각적 아크로 대신한다 — FxKit.cs
        /// FlyToRoutine 주석 참조), arrivalBurst(FxId.CoinSpark)로 "도착 시 스파크4"를 정확한 도착
        /// 좌표(코인 라벨)에서 재생한다.</summary>
        public void PlayCoinFx(RectTransform from, int coinsGained)
        {
            if (coinsGained <= 0 || coinsText == null) return;
            FxKit.I?.PlayFlyTo(FxId.Coin, from, coinsText.rectTransform, Mathf.Clamp(coinsGained, 1, 8),
                arrivalBurst: FxId.CoinSpark, arcHeight: 140f);
            StartCoroutine(CoinArrivalPulseRoutine());
        }

        private IEnumerator CoinArrivalPulseRoutine()
        {
            yield return new WaitForSeconds(CoinFlyDuration);
            if (coinsText == null) yield break;
            yield return UiTween.ScaleRoutine(coinsText.transform, Vector3.one, Vector3.one * CoinPulseScale, CoinPulseInDuration, UiTween.Ease.OutBack);
            if (coinsText == null) yield break;
            yield return UiTween.ScaleRoutine(coinsText.transform, Vector3.one * CoinPulseScale, Vector3.one, CoinPulseOutDuration, UiTween.Ease.OutQuad);
        }

        private void PlayQuotaPulse()
        {
            if (expBarFillImage == null) return;
            StartCoroutine(QuotaPulseRoutine());
        }

        // 요구치 도달 "땅" 순간의 1회성 강조 펄스 — UpdateExpBarStyle이 이미 이 시점에 색을
        // UiKit.Green으로 고정해 두므로, 여기서는 그 위에 골드로 한 번 튀었다 되돌아온다(끝나면
        // baseColor=Green으로 복귀 — 지속 초록 상태와 자연스럽게 이어진다).
        private IEnumerator QuotaPulseRoutine()
        {
            var baseColor = expBarFillImage.color;
            var gold = UiKit.Accent;
            yield return UiTween.FloatRoutine(0f, 1f, QuotaPulseDuration * 0.5f,
                t => expBarFillImage.color = Color.Lerp(baseColor, gold, t), UiTween.Ease.OutQuad);
            if (expBarFillImage == null) yield break;
            yield return UiTween.FloatRoutine(1f, 0f, QuotaPulseDuration * 0.5f,
                t => expBarFillImage.color = Color.Lerp(baseColor, gold, t), UiTween.Ease.OutQuad);
        }

        // ── 불운 게이지 🍀 5칸 ────────────────────────────────────────────────────────
        private void RefreshUnluckyGauge(int gauge, bool allowPulse)
        {
            for (int i = 0; i < unluckyPips.Length; i++)
            {
                if (unluckyPips[i] == null) continue;
                unluckyPips[i].color = i < gauge ? UiKit.Accent : UiKit.Card;
            }
            if (allowPulse && gauge >= Formulas.UNLUCKY_MAX && _gaugePulseRoutine == null)
                _gaugePulseRoutine = StartCoroutine(GaugePulseRoutine());
        }

        private IEnumerator GaugePulseRoutine()
        {
            for (int i = 0; i < unluckyPips.Length; i++)
            {
                if (unluckyPips[i] == null) continue;
                yield return UiTween.ScaleRoutine(unluckyPips[i].transform, Vector3.one, Vector3.one * 1.3f,
                    GaugePulseDuration * 0.5f, UiTween.Ease.OutBack);
                yield return UiTween.ScaleRoutine(unluckyPips[i].transform, Vector3.one * 1.3f, Vector3.one,
                    GaugePulseDuration * 0.5f, UiTween.Ease.OutQuad);
            }
            _gaugePulseRoutine = null;
        }

        // ── 보스 스테이지: 테두리 적색 틴트 + 진입 배너 + S14 §D 적색 비네트 펄스 + 0.4s 셰이크 ─────
        private void RefreshBossState(RunState run)
        {
            var boss = Bosses.For(run.Stage);
            if (stageText != null)
                stageText.text = boss != null ? $"스테이지 {run.Stage} · {boss.emoji}{boss.name}" : $"스테이지 {run.Stage}";

            if (hudOutline != null) hudOutline.enabled = boss != null;

            string bossId = boss?.id;
            if (bossId != _shownBossId)
            {
                _shownBossId = bossId;
                if (boss != null)
                {
                    PlayBossBanner(boss);
                    StartCoroutine(BossVignettePulse());
                    PlayScreenShake(BossEntryShakeAmplitude, BossEntryShakeDuration);
                }

                // S7c 연출 훅: "보스 스테이지 Boss 루프(스테이지 종료 시 Stop)" — HUD 영역(자기 자신의
                // RectTransform, hudRoot에 붙어 있다) 기준으로 재생.
                if (boss != null && _bossFx == null) _bossFx = FxKit.I?.PlayLoop(FxId.Boss, (RectTransform)transform);
                else if (boss == null && _bossFx != null)
                {
                    FxKit.I?.StopLoop(_bossFx);
                    _bossFx = null;
                }
            }
        }

        private IEnumerator BossVignettePulse()
        {
            if (bossVignetteGroup == null) yield break;
            yield return UiTween.FadeRoutine(bossVignetteGroup, 0f, BossVignetteAlpha, BossVignetteInDuration);
            if (bossVignetteGroup == null) yield break;
            yield return UiTween.FadeRoutine(bossVignetteGroup, BossVignetteAlpha, 0f, BossVignetteOutDuration);
        }

        // S14 §G — runScreenRoot 셰이크 공용 헬퍼(ReelView.PlayScreenShake와 동일 패턴 — 별도 조정자
        // 없이 각자 자기 셰이크만 취소/재시작한다).
        private void PlayScreenShake(float amplitude, float duration)
        {
            if (runScreenRoot == null) return;
            if (_screenShakeRoutine != null)
            {
                StopCoroutine(_screenShakeRoutine);
                runScreenRoot.anchoredPosition = Vector2.zero;
            }
            _screenShakeRoutine = StartCoroutine(ScreenShakeRoutine(amplitude, duration));
        }

        private IEnumerator ScreenShakeRoutine(float amplitude, float duration)
        {
            yield return UiTween.ShakeRoutine(runScreenRoot, amplitude, duration);
            _screenShakeRoutine = null;
        }

        private void PlayBossBanner(Boss boss)
        {
            if (bossBannerGroup == null || bossBannerText == null) return;
            if (_bossBannerRoutine != null) StopCoroutine(_bossBannerRoutine);
            bossBannerText.text = $"⚠ 보스: {boss.emoji}{boss.name}";
            _bossBannerRoutine = StartCoroutine(BossBannerRoutine());
        }

        private IEnumerator BossBannerRoutine()
        {
            if (bossBannerRect != null) bossBannerRect.anchoredPosition = new Vector2(bossBannerRect.anchoredPosition.x, 40f);
            yield return UiTween.FadeRoutine(bossBannerGroup, 0f, 1f, BossBannerFade);
            if (bossBannerRect != null)
                yield return UiTween.MoveRoutine(bossBannerRect, bossBannerRect.anchoredPosition,
                    new Vector2(bossBannerRect.anchoredPosition.x, 0f), 0.2f, UiTween.Ease.OutBack);
            yield return new WaitForSeconds(BossBannerHold);
            yield return UiTween.FadeRoutine(bossBannerGroup, 1f, 0f, BossBannerFade);
            _bossBannerRoutine = null;
        }

        /// <summary>런 재시작(화면 재진입) 시 내부 캐시 초기화 — 새 런의 첫 보스 진입 배너가 다시 뜨도록.</summary>
        public void ResetForNewRun()
        {
            _shownBossId = null;
            _lastCurseCount = -1;
            if (bossBannerGroup != null) bossBannerGroup.alpha = 0f;
            if (bossVignetteGroup != null) bossVignetteGroup.alpha = 0f;
            if (hudOutline != null) hudOutline.enabled = false;
            if (spinsText != null) spinsText.color = UiKit.TextSecondary;
            if (_spinsBlinkRoutine != null) { StopCoroutine(_spinsBlinkRoutine); _spinsBlinkRoutine = null; }
            if (_bossFx != null)
            {
                FxKit.I?.StopLoop(_bossFx);
                _bossFx = null;
            }
        }
    }
}
