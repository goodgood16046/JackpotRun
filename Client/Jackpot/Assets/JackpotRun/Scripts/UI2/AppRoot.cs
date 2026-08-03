using System.Collections;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 영속 엔트리 — ENGINE_PORT_DESIGN.md S8 "영속 계층". Intro/Play 두 씬을 넘나드는 유일한
    // DontDestroyOnLoad 싱글턴이다. **씬 뷰를 직렬화 참조로 들지 않는다** — AppRoot 자신은 씬에
    // 존재하지 않고(RuntimeInitializeOnLoadMethod로 자가 생성) [SerializeField] 와이어링 대상이
    // 될 수 없기 때문이기도 하다. 대신 각 씬의 IntroSceneRoot/PlaySceneRoot가 자기 Awake에서
    // AppRoot.Instance에 스스로 등록한다(역방향 참조만 — AppRoot는 등록된 루트를 통해서만 안다).
    //
    // 보유물: Profile(PlayerProfile) · Session(GameSession) · PendingLaunch(씬 전환 핸드오프) ·
    // 전환 페이드용 캔버스(sortingOrder 500, 0.2s 암전→복귀) · 현재 씬의 Toast/OverlayLayer 참조
    // (뷰가 appRoot.Toast/appRoot.OverlayLayer로 접근 — 예전 "appRoot.Router.Toast" 대체).
    //
    // 뷰(MenuView/PickView/DexView/RunView)는 더 이상 [SerializeField] AppRoot를 갖지 않는다 —
    // AppRoot GameObject가 씬에 존재하지 않으므로 SceneBuilder가 그 참조를 와이어링할 방법이 없다.
    // 대신 각 뷰는 `private AppRoot appRoot => AppRoot.Instance;` 계산 프로퍼티로 접근한다(필드명은
    // 그대로 유지해 기존 `appRoot.XXX` 호출부를 건드리지 않는다).
    public sealed class AppRoot : MonoBehaviour
    {
        public const string IntroSceneName = "Intro";
        public const string PlaySceneName = "Play";
        private const float FadeDuration = 0.2f; // 설계 명시: "전환 페이드용 캔버스... 0.2s 암전→복귀"
        private const int FadeSortingOrder = 500; // 설계 명시

        public static AppRoot Instance { get; private set; }

        public PlayerProfile Profile { get; private set; }

        /// <summary>진행 중인 게임 런 — StartRun 핸드오프 이후 Play 씬 등록 시 생성된다.</summary>
        public GameSession Session { get; private set; }

        /// <summary>현재 로드된 씬(Intro 또는 Play)의 토스트/오버레이 레이어 — 각 SceneRoot가 등록 시 채운다.</summary>
        public ToastManager Toast { get; private set; }
        public RectTransform OverlayLayer { get; private set; }

        private IntroSceneRoot _introRoot;
        private PlaySceneRoot _playRoot;

        private struct PendingLaunchInfo
        {
            public string charId;
            public string machineId;
            public string deviceId;
            public bool valid;
        }

        private PendingLaunchInfo _pending;

        private CanvasGroup _fadeGroup;
        private bool _transitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // "어느 씬에서 Play를 눌러도 동작" — 없으면 만든다(설계 S8 "생성" 절).
            if (Instance != null) return;
            var go = new GameObject("AppRoot");
            go.AddComponent<AppRoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Profile = ProfileStore.Load();
            BuildFadeCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 씬 루트 등록(각 씬의 IntroSceneRoot/PlaySceneRoot.Awake가 호출) ──────────────────
        public void RegisterIntro(IntroSceneRoot root)
        {
            _introRoot = root;
            _playRoot = null;
            Toast = root != null ? root.Toast : null;
            OverlayLayer = root != null ? root.OverlayLayer : null;
            // 첫 화면 표시는 IntroSceneRoot.Start가 담당한다 — 여기(Awake 경로)서 부르면
            // 같은 GameObject의 ScreenRouter.Awake가 뒤늦게 전 화면을 비활성화해 검은 화면이 된다.

            // S15: Intro 씬 진입마다(최초 실행 + EndRun 복귀 모두) 최신 BestScore를 랭킹 보드에
            // 올려본다 — host는 AppRoot 자신(DontDestroyOnLoad)이라 Play↔Intro 씬 전환에도 코루틴이
            // 끊기지 않는다. 실제 PUT 여부는 TrySubmitBest 내부의 "이전에 올린 값보다 큰가" 판정이 결정.
            RankingService.TrySubmitBest(this);
        }

        public void RegisterPlay(PlaySceneRoot root)
        {
            _playRoot = root;
            _introRoot = null;
            Toast = root != null ? root.Toast : null;
            OverlayLayer = root != null ? root.OverlayLayer : null;

            string charId, machineId, deviceId;
            if (_pending.valid)
            {
                charId = _pending.charId;
                machineId = _pending.machineId;
                deviceId = _pending.deviceId;
                _pending.valid = false;
            }
            else
            {
                // 설계 S8: "Play 씬을 직접 열고 Play를 눌렀을 때(에디터 개발 편의): PendingLaunch가
                // 없으면 기본 조합(novice/basic/"")으로 시작하고 경고 로그."
                charId = "novice";
                machineId = "basic";
                deviceId = "";
                Debug.LogWarning("[JackpotRun] Play 씬 단독 실행 — PendingLaunch 없음, 기본 조합(novice/basic)으로 시작합니다.");
            }

            Session = new GameSession(charId, machineId, deviceId ?? string.Empty);
            root?.Bind(Session);
        }

        // ── Intro 내비게이션(뷰의 버튼 onClick이 직접 호출) ──────────────────────────────
        public void ShowMenu() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Menu);
        public void ShowPick() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Pick);
        public void ShowDex() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Dex);
        public void ShowLogin() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Login);
        public void ShowRank() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Rank);

        /// <summary>LoginView "시작하기"/"게스트로 시작" → 메뉴로. 닉네임 저장은 LoginView 자신이
        /// PlayerPrefs에 직접 한다(엔진 PlayerProfile은 건드리지 않는다 — 설계 S8 LoginView 절).
        /// S15: 닉네임이 방금 바뀌었을 수 있으므로 랭킹 보드 갱신을 시도한다(TrySubmitBest 내부의
        /// "닉이 달라졌나" 판정이 실제 PUT 여부를 결정 — Opus S15 검수 경미-7 반영).</summary>
        public void CompleteLogin()
        {
            RankingService.TrySubmitBest(this);
            ShowMenu();
        }

        /// <summary>PickView "시작" → 실제 런 시작(구 JackpotRunApp.ShowRun/AppRoot.StartRun 계승).
        /// PendingLaunch를 저장하고 페이드아웃 → Play 씬 로드 → PlaySceneRoot가 세션을 만들어
        /// 이어받는다(설계 S8 "전환 흐름").</summary>
        public void StartRun(string charId, string machineId, string deviceId)
        {
            if (_transitioning) return;
            _pending = new PendingLaunchInfo
            {
                charId = charId,
                machineId = machineId,
                deviceId = deviceId ?? string.Empty,
                valid = true,
            };
            StartCoroutine(TransitionToScene(PlaySceneName));
        }

        /// <summary>런 종료(GameOverPanel "메뉴로") — 세션을 비우고 프로필을 디스크에서 다시 읽은 뒤
        /// Intro 씬으로 돌아간다(GameSession.Do가 GAME_OVER 시 이미 저장을 마쳤으므로 여기서는 최신
        /// 업적/베스트기록을 메뉴·픽 화면에 즉시 반영하기 위해 다시 로드만 한다).</summary>
        public void EndRun()
        {
            if (_transitioning) return;
            Session = null;
            Profile = ProfileStore.Load();
            StartCoroutine(TransitionToScene(IntroSceneName));
        }

        private IEnumerator TransitionToScene(string sceneName)
        {
            _transitioning = true;
            if (_fadeGroup != null) _fadeGroup.blocksRaycasts = true;
            yield return UiTween.FadeRoutine(_fadeGroup, 0f, 1f, FadeDuration);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null; // 새 씬의 SceneRoot.Awake(등록)가 끝나도록 한 프레임 대기

            yield return UiTween.FadeRoutine(_fadeGroup, 1f, 0f, FadeDuration);
            if (_fadeGroup != null) _fadeGroup.blocksRaycasts = false;
            _transitioning = false;
        }

        // ── 전환 페이드 캔버스(설계 S8: sortingOrder 500, DontDestroyOnLoad, 0.2s 암전→복귀) ──────
        private void BuildFadeCanvas()
        {
            var go = new GameObject("TransitionFadeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = FadeSortingOrder;

            var img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;

            _fadeGroup = go.GetComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
            _fadeGroup.interactable = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
