using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 튜토리얼 3단 — 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 ui.js:1740-1814 TOUR/tutSpot/
    // tutExplainSpin/tutLive + game.js:2633 markTutorialDone). 순수 표시 컴포넌트(RunView가 소유·구동) —
    // 게임 로직은 전혀 건드리지 않는다.
    //
    // 1단(스포트라이트 투어 6스텝): 대상 RectTransform의 화면 위치를 매 스텝 다시 계산해(다른 계층의
    // 컴포넌트를 그대로 하이라이트할 방법이 없어 §7 "코드생성 uGUI로 실현 가능한 방식" 재해석 — 웹의
    // "칸 컷아웃"(진짜 투명 구멍)은 uGUI 표준 머티리얼로는 스텐실 없이 불가능해 "테두리 강조"로 대체,
    // 작업 지시가 명시적으로 허용한 대안) 딤 위에 골드 테두리 프레임을 그 위치에 겹쳐 그린다.
    // action 스텝(마지막, "직접 해보기")만 딤이 클릭을 막지 않는다(웹 tutSpot의 block:!s.action 그대로).
    //
    // 2단(결과 해설)·3단(라이브 안내)은 웹처럼 딤+중앙 카드 배너 1종을 공유한다(tutBanner 공용 함수와
    // 동일 발상).
    public sealed class TutorialOverlay : MonoBehaviour
    {
        // UiSceneBuilder가 점 인디케이터 개수(dotImages 배열 길이)를 이 값에 맞춰 짓는다 — Tour.Length와
        // 반드시 일치해야 하므로 매직넘버 대신 이 상수를 참조한다.
        public const int TourStepCount = 6;

        private AppRoot appRoot => AppRoot.Instance;

        // ── 1단: 스포트라이트 투어 ───────────────────────────────────────────────────────
        private enum TourTarget { Hud, Reels, SpinBtn, Extra, Icons }

        private struct TourStep
        {
            public TourTarget target;
            public string title;
            public string body;
            public bool action;
            public TourStep(TourTarget t, string title, string body, bool action = false)
            { target = t; this.title = title; this.body = body; this.action = action; }
        }

        // 웹 TOUR(ui.js:1741-1748) 전사 — astral 이모지(📊🎰🕹️✨🎒🎮) 제거, <b> 굵게 태그는 legacy Text의
        // richText로 그대로 유지(supportRichText 기본 true). 특수스핀 비용은 Unity 실제 상수/버튼
        // 순서(RunView.ModeOrder: 집중·올인·기도·막판, Formulas.CMD_COST_*)에 맞춰 옮겼다(웹 원문의
        // 🎯1·⏰2·🙏3·🎰4는 Unity 라벨/원가와 매핑이 달라 그대로 베끼면 오정보가 된다).
        private static readonly TourStep[] Tour =
        {
            new TourStep(TourTarget.Hud, "상태 정보",
                "스테이지·점수·코인과 EXP 바예요. <b>스핀(현재/최대) 안에 EXP 바를 요구치까지</b> 채우면 클리어! 동시에 <b>점수를 최대한 많이</b> 모으는 게 목표예요."),
            new TourStep(TourTarget.Reels, "슬롯 5칸",
                "한 줄 슬롯이에요. <b>칸을 탭하면</b> 그 심볼의 효과·영향을 볼 수 있어요. 같은 값심볼 2개↑면 세트 보너스!"),
            new TourStep(TourTarget.SpinBtn, "슬롯 돌리기",
                "이 버튼으로 돌려 EXP를 모아요. <b>정해진 스핀(현재/최대) 안에</b> 요구치를 채우면 클리어! <b>빨리 클리어할수록(남은 스핀·초과 EXP) 점수가 더 올라가요.</b>"),
            new TourStep(TourTarget.Extra, "특수 스핀",
                "집중·올인·기도·막판 — <b>스테이지당 종류별 1회</b>씩 쓰는 모험 스핀! <b>한 판에서 종류별 첫 사용은 무료(코인 0이어도 OK)</b>, 두 번째부터 코인 비용(집중1·막판2·기도3·올인4, 보스 +1)이 들고 버튼에 가격이 표시돼요."),
            new TourStep(TourTarget.Icons, "아이템·장치·상태",
                "아이템 사용 · 장치 발동 · <b>내 빌드(능력치)</b> 확인을 여기서 해요."),
            new TourStep(TourTarget.SpinBtn, "직접 돌려보세요!",
                "이제 <b>[슬롯 돌리기]</b>를 눌러 첫 스핀! 어떤 효과가 났는지 바로 알려드릴게요.", action: true),
        };

        [Header("1단 — 스포트라이트")]
        [SerializeField] private RectTransform spotRoot;
        [SerializeField] private CanvasGroup spotDimGroup;
        [SerializeField] private RectTransform highlightFrame;
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private Text stepLabelText;
        [SerializeField] private Image[] dotImages = System.Array.Empty<Image>();
        [SerializeField] private Text tourTitleText;
        [SerializeField] private Text tourBodyText;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button nextButton;

        [Header("2/3단 — 배너(결과 해설·라이브 안내 공용)")]
        [SerializeField] private RectTransform bannerRoot;
        [SerializeField] private CanvasGroup bannerDimGroup;
        [SerializeField] private Text bannerBodyText;
        [SerializeField] private Button bannerOkButton;

        private RectTransform _hudTarget, _reelsTarget, _spinTarget, _extraTarget, _iconsTarget;

        private bool _active;
        private bool _live;
        private int _tourIndex;
        private readonly HashSet<string> _shownLive = new HashSet<string>();
        // "알겠어요" 버튼 클릭 시 실행할 동작(결과해설 종료→라이브 전환 / 라이브배너 닫기)이 매번 달라
        // 웹 tutBanner(html, btn)의 btn 인자처럼 콜백을 갈아 끼운다.
        private System.Action _bannerOkAction;

        private void Awake()
        {
            if (spotRoot != null) spotRoot.gameObject.SetActive(false);
            if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);
            if (skipButton != null) skipButton.onClick.AddListener(EndTutorial);
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            if (bannerOkButton != null) bannerOkButton.onClick.AddListener(OnBannerOkClicked);
        }

        /// <summary>RunView가 자신이 소유한 HUD/릴/스핀버튼/특수스핀열/아이콘열의 RectTransform을
        /// 넘긴다(다른 컴포넌트 소유물을 직접 들지 않고 참조만 받는 기존 관례 — NodePanel의 onShake
        /// 콜백 위임과 동일 취지).</summary>
        public void SetTargets(RectTransform hud, RectTransform reels, RectTransform spinBtn, RectTransform extra, RectTransform icons)
        {
            _hudTarget = hud; _reelsTarget = reels; _spinTarget = spinBtn; _extraTarget = extra; _iconsTarget = icons;
        }

        public bool Active => _active;

        public void StartTour()
        {
            _active = true;
            _live = false;
            _tourIndex = 0;
            _autoStartCoroutineRunning = false; // 이미 시작했으니 예약된 자동시작 재시도는 더 이상 필요 없음
            _shownLive.Clear();
            if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);
            RenderTourStep();
        }

        // ── Opus 2차검수 LOW① — 자동 시작을 1회성이 아니라 매 액션 배치 후 재검사(웹 render() 매번
        // 평가하는 것과 동일 취지). RunView.PlayRoutine 꼬리에서 NotifyPhase와 나란히 호출한다.
        // 웹 setTimeout(...,420) 재스케줄과 달리 Unity는 코루틴 1개만 유지(_autoStartCoroutineRunning
        // 가드) — 여러 액션이 연달아 들어와도 420ms 타이머가 중복 누적되지 않는다(결과는 동일: 조건이
        // 유지되는 한 420ms 후 시작).
        private bool _autoStartCoroutineRunning;

        public void MaybeAutoStart(RunState run, bool tutDone)
        {
            if (_active || tutDone || run == null || _autoStartCoroutineRunning) return;
            if (run.Stage != 1 || run.SpinIndex != 0 || run.Phase != RunPhase.Spin) return;
            _autoStartCoroutineRunning = true;
            StartCoroutine(AutoStartDelayed(run));
        }

        private IEnumerator AutoStartDelayed(RunState runAtSchedule)
        {
            yield return new WaitForSeconds(0.42f);
            _autoStartCoroutineRunning = false;
            if (this == null || _active) yield break;
            // RunState는 참조 타입이라 대기 동안 필드가 갱신됐다면 여기서 그대로 최신값을 읽는다
            // (웹 setTimeout 콜백의 `if (st && st.phase === PHASE.SPIN && !tut.active)` 재검증과 동일 취지).
            if (runAtSchedule.Stage == 1 && runAtSchedule.SpinIndex == 0 && runAtSchedule.Phase == RunPhase.Spin)
                StartTour();
        }

        /// <summary>화면을 완전히 떠날 때(RunView.HidePanels) — 진행 마킹 없이 그냥 닫는다.</summary>
        public void HideImmediate()
        {
            _active = false;
            if (spotRoot != null) spotRoot.gameObject.SetActive(false);
            if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);
        }

        private void RenderTourStep()
        {
            // 웹 renderTour(ui.js:1753-1759): 대상이 없거나 크기 0이면 다음 스텝으로 건너뛴다.
            while (_tourIndex < Tour.Length && ResolveTarget(Tour[_tourIndex].target) == null)
                _tourIndex++;
            if (_tourIndex >= Tour.Length) { GoLive(); return; }

            var step = Tour[_tourIndex];
            var target = ResolveTarget(step.target);
            if (spotRoot != null) spotRoot.gameObject.SetActive(true);
            if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);

            PositionHighlight(target);

            if (tourTitleText != null) tourTitleText.text = step.title;
            if (tourBodyText != null) tourBodyText.text = step.body;
            if (stepLabelText != null) stepLabelText.text = $"튜토리얼 {_tourIndex + 1}/{Tour.Length}{(step.action ? " · 직접 해보기" : "")}";
            for (int i = 0; i < dotImages.Length; i++)
                if (dotImages[i] != null) dotImages[i].color = i <= _tourIndex ? UiKit.Accent : UiKit.Card;

            // 웹 tutSpot: block=!action — action 스텝만 딤이 클릭을 막지 않아 실제 스핀버튼을 누를 수 있다.
            if (spotDimGroup != null) { spotDimGroup.blocksRaycasts = !step.action; spotDimGroup.alpha = 1f; }
            if (nextButton != null) nextButton.gameObject.SetActive(!step.action);
        }

        private RectTransform ResolveTarget(TourTarget t)
        {
            RectTransform rt = t switch
            {
                TourTarget.Hud => _hudTarget,
                TourTarget.Reels => _reelsTarget,
                TourTarget.SpinBtn => _spinTarget,
                TourTarget.Extra => _extraTarget,
                TourTarget.Icons => _iconsTarget,
                _ => null,
            };
            if (rt == null || !rt.gameObject.activeInHierarchy) return null;
            return rt.rect.width > 0f ? rt : null;
        }

        // 다른 계층(RunView 자신의 HUD/릴 등)의 RectTransform 화면 위치를 spotRoot 로컬 좌표로 변환해
        // highlightFrame을 그 자리·크기에 겹쳐 그린다(§7 "테두리 강조" 대안 — 헤더 주석 참조).
        private void PositionHighlight(RectTransform target)
        {
            if (highlightFrame == null || target == null) return;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(spotRoot, target);
            const float pad = 8f;
            highlightFrame.sizeDelta = new Vector2(bounds.size.x + pad * 2f, bounds.size.y + pad * 2f);
            highlightFrame.anchoredPosition = new Vector2(bounds.center.x, bounds.center.y);

            // 툴팁은 대상이 화면 위쪽 42% 안에 있으면 아래, 아니면 위(웹 tutSpot의 below 판정과 동일
            // 취지 — Unity는 화면 전체 픽셀좌표 대신 spotRoot 로컬중심 y부호로 상/하만 가른다).
            // Opus 2차검수 필수③ — gap이 하이라이트 반높이만 반영하고 툴팁 자신의 반높이를 빼먹어서,
            // 툴팁이 큰 경우(특히 action 스텝의 스핀 버튼 근처) 하이라이트/버튼과 겹칠 수 있었다.
            // 툴팁 반높이도 더하고, 화면 밖으로 나가지 않도록 상하 클램프까지 추가한다.
            if (tooltipRect != null)
            {
                bool below = bounds.center.y > spotRoot.rect.height * 0.08f; // 화면 중상단이면 아래로
                float halfHighlight = highlightFrame.sizeDelta.y * 0.5f;
                float halfTooltip = tooltipRect.rect.height * 0.5f;
                const float margin = 24f;
                float gap = halfHighlight + halfTooltip + margin;
                float y = below ? bounds.center.y - gap : bounds.center.y + gap;

                float screenH = spotRoot.rect.height;
                float minY = -screenH * 0.5f + halfTooltip + margin;
                float maxY = screenH * 0.5f - halfTooltip - margin;
                if (minY <= maxY) y = Mathf.Clamp(y, minY, maxY);
                tooltipRect.anchoredPosition = new Vector2(0f, y);
            }
        }

        private void OnNextClicked()
        {
            _tourIndex++;
            RenderTourStep();
        }

        private void GoLive()
        {
            _live = true;
            if (spotRoot != null) spotRoot.gameObject.SetActive(false);
        }

        // ── 2단: 결과 해설(웹 tutExplainSpin, ui.js:1786-1796) ──────────────────────────────
        // RunView가 실제 스핀 처리 직후(action 스텝에서 발생한 스핀에 한해) 0.26s 지연 후 호출한다
        // (웹 setTimeout(tutExplainSpin, 260) 그대로).
        public void NotifySpinResult(RunState run, long quota, int spins)
        {
            if (!_active || _live || run == null) return;
            // 클리어/게임오버로 곧장 넘어간 경우 — 웹 doSpin 꼬리(ui.js:379-382)의 "라이브 전환" 분기.
            if (run.Phase == RunPhase.NodeSelect || run.Phase == RunPhase.GameOver || run.Phase == RunPhase.RewardDone)
            {
                _live = true;
                if (spotRoot != null) spotRoot.gameObject.SetActive(false);
                return;
            }
            if (_tourIndex < Tour.Length && Tour[_tourIndex].action)
                StartCoroutine(ExplainSpinDelayed(run, quota, spins));
        }

        private IEnumerator ExplainSpinDelayed(RunState run, long quota, int spins)
        {
            yield return new WaitForSeconds(0.26f);
            if (this == null || run == null) yield break;
            // 웹 tutExplainSpin(ui.js:1787) 가드 — 대기 260ms 동안 건너뛰기 등으로 상태가 바뀌었을 수
            // 있어 재확인 없이 그대로 띄우면 이미 끝난 튜토리얼에 배너가 떠 버린다.
            if (!_active || _live) yield break;
            ShowExplainSpin(run, quota, spins);
        }

        private void ShowExplainSpin(RunState run, long quota, int spins)
        {
            if (spotRoot != null) spotRoot.gameObject.SetActive(false);

            var cellsSb = new System.Text.StringBuilder();
            for (int i = 0; i < run.LastCellsFinal.Count; i++)
                cellsSb.Append(TextSanitize.StripAstral(run.LastCellsFinal[i].sym.name));
            string cells = cellsSb.ToString();

            string notes = "";
            if (run.LastNotes != null && run.LastNotes.Count > 0)
            {
                int n = Mathf.Min(4, run.LastNotes.Count);
                var parts = new string[n];
                for (int i = 0; i < n; i++) parts[i] = run.LastNotes[i];
                notes = TextSanitize.StripAstral(string.Join(" · ", parts));
            }

            int used = Mathf.Min(run.SpinIndex, spins);
            int left = Mathf.Max(0, spins - run.SpinIndex);
            string notesLine = string.IsNullOrEmpty(notes) ? "" : $"효과: <b>{notes}</b><br>";
            string body = $"<b>{cells}</b> 가 나왔어요.<br>{notesLine}이번 스핀 <b>+{NumberFormat.Comma(run.LastGain)} EXP</b> → 현재 <b>{NumberFormat.Comma(run.StageExp)}/{NumberFormat.Comma(quota)} EXP</b><br>스핀 <b>{used}/{spins}</b> 사용 · <b>남은 {left}회</b>";
            // 레거시 Text richText는 <br>을 해석하지 못한다(HTML 전용 태그) — 개행문자로 치환.
            body = body.Replace("<br>", "\n");

            ShowBanner(body, OnExplainSpinOk);
        }

        private void OnExplainSpinOk()
        {
            // 웹 tutToFree(ui.js:1797-1800) — 이제부터 라이브(클리어→증강→상점 등 자동 안내).
            _live = true;
            string body = "<b>정해진 스핀(현재/최대) 안에 EXP 바를 채워 클리어</b>! 스핀이 남아도 다 채우면 바로 클리어돼요.\n" +
                "남은 스핀·초과 EXP·보석·왕관이 점수가 되니, 빨리·많이 모아 최대 점수에 도전!\n" +
                "일반 말고 <b>특수 스핀(집중·올인·기도·막판)</b> 으로도 돌릴 수 있어요 — 아래 보라색 버튼! 코인 비용이 들어요. 직접 채워보세요.";
            // Opus 2차검수 필수①(CRITICAL) — 웹 tutBannerOk는 배너를 그냥 닫기만 한다(tutClear()).
            // 여기서 OnBannerOkClicked를 다시 넘기면 그 버튼을 눌렀을 때 OnBannerOkClicked가 자기
            // 자신을 _bannerOkAction으로 다시 호출해 무한 재귀(스택 오버플로)로 이어진다 — null(닫기만).
            ShowBanner(body, null);
        }

        // ── 3단: 라이브 안내(웹 tutLive, ui.js:1802-1814) ───────────────────────────────────
        // RunView가 매 액션 배치 처리 후(HandleEvents 다음) 현재 phase/stage로 호출한다. Unity는
        // NodePanel이 STAGE_CLEAR+NODE_SELECT를 한 화면으로 합쳐 보여주므로(웹은 2단계 화면 전환)
        // 두 안내를 하나로 합쳐 RunPhase.NodeSelect 진입 시 1회 보여준다.
        public void NotifyPhase(RunPhase phase, int stage)
        {
            if (!_active) return;
            // Opus 2차검수 필수②(HIGH) — 웹 render() 꼬리(ui.js:738)의 stage>=2 폴백은 renderPlay()
            // 컨텍스트 안에서만(=실제 스핀 화면일 때만) 평가된다. 이 가드 없이 phase 무관하게 검사하면,
            // 스테이지 클리어 직후 NodeSelect에 진입한 바로 그 순간(stage가 이미 다음 값으로 전진해
            // 있음)에 아직 보여주지도 못한 node/perk/shop/stats 라이브 배너 4종을 곧장 EndTutorial()로
            // 지워버리고 markTutorialDone까지 조기 확정해 버렸다 — Spin/PostSpin(실제 게임플레이 화면)
            // 로 돌아왔을 때만 안전망으로 작동해야 한다(정상 흐름에서는 REWARD_DONE 배너가 먼저
            // EndTutorial을 부르므로 이 분기는 거의 발동하지 않는 순수 안전망).
            if (_live && stage >= 2 && (phase == RunPhase.Spin || phase == RunPhase.PostSpin))
            { EndTutorial(); return; }
            if (!_live) return;

            string key, text;
            switch (phase)
            {
                case RunPhase.NodeSelect:
                    key = "node";
                    text = "<b>스테이지 클리어!</b> 초과 EXP·남은 스핀·연승이 점수로 환산되고 코인도 받았어요. 누적 총점수도 확인!\n" +
                        "<b>보상 선택</b> — 증강(영구 강화) · 유물(추가효과) · 상점(코인으로 구매) · 저주(위험+보상). 처음엔 <b>증강</b> 추천!";
                    break;
                case RunPhase.EventAugment:
                case RunPhase.EventRelic:
                case RunPhase.EventAugLevel:
                    key = "perk";
                    text = "<b>증강 카드</b> — 등급이 <b>실버 &lt; 골드 &lt; 프리즘</b> 순으로 강력! 빌드에 맞게 하나 골라요.";
                    break;
                case RunPhase.EventShop:
                    key = "shop";
                    text = "<b>상점</b> — 코인으로 증강·유물·아이템 구매. 항목을 누르면 구매 확인! 다 보면 [나가기].";
                    break;
                case RunPhase.RewardDone:
                    key = "stats";
                    text = "고른 보상이 <b>능력치</b>에 반영됐어요. 다음 스테이지 정보 확인 후 [시작]!\n<b>이대로 계속 진행해 더 깊은 스테이지에 도전하세요!</b>";
                    break;
                default:
                    return;
            }
            if (_shownLive.Contains(key)) return;
            _shownLive.Add(key);
            // Opus 2차검수 필수①(CRITICAL) — 일반 라이브 배너는 웹 tutBannerOk처럼 닫기만(null).
            // "stats"(REWARD_DONE, 튜토리얼 마지막 단계)만 확인 버튼을 눌렀을 때 실제로 종료·저장한다
            // (EndTutorial 자체를 콜백으로 넘김 — 배너를 보여준 즉시 지우지 않고 사용자가 읽게 둔다;
            // 이전 버전은 ShowBanner 직후 곧장 EndTutorial()을 호출해 배너가 뜨자마자 사라지는 결함이
            // 있었다).
            ShowBanner(text, key == "stats" ? (System.Action)EndTutorial : null);
        }

        private void ShowBanner(string body, System.Action onOk)
        {
            _bannerOkAction = onOk;
            if (bannerBodyText != null) bannerBodyText.text = body;
            if (bannerRoot != null) bannerRoot.gameObject.SetActive(true);
            if (bannerDimGroup != null) bannerDimGroup.alpha = 1f;
        }

        private void OnBannerOkClicked()
        {
            if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);
            _bannerOkAction?.Invoke();
        }

        // 웹 endTutorial(ui.js:1785) — 건너뛰기/자연 종료 공용. 프로필 저장은 엔진 밖(이 컴포넌트가 직접
        // AppRoot.Profile을 갱신 — MenuView/RunView가 이미 appRoot를 이렇게 쓰는 기존 관례와 동일).
        public void EndTutorial()
        {
            _active = false;
            _live = false;
            if (spotRoot != null) spotRoot.gameObject.SetActive(false);
            if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);
            var profile = appRoot != null ? appRoot.Profile : null;
            if (profile != null && !profile.TutDone)
            {
                profile.MarkTutorialDone();
                ProfileStore.Save(profile);
            }
        }
    }
}
