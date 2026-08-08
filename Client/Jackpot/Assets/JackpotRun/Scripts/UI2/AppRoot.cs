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

        /// <summary>웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 `let selAsc = 0` 모듈 전역과 동일한
        /// 취지) — 홈 승천 선택기(MenuView)가 조정하는 "다음 런에 쓸 승천 단계" 선택값. 앱 실행 내내
        /// 메모리에만 유지되고(웹처럼 세이브 파일에 영속화하지 않음) 여러 화면(Pick 3단계)을 오가도
        /// 유지되도록 AppRoot에 둔다. 클램프는 소비측(MenuView.RefreshAscSelector/GameSession 생성자)
        /// 이 매번 다시 하므로 여기서는 원시값 그대로 저장한다.</summary>
        public int SelectedAsc { get; set; }

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
            // WEB_PARITY P1 ②: 첫 판 즉시 시작(runs==0) 경로로 들어왔는지 — Play 씬 RunView가 최초
            // 갱신 시 안내 토스트를 1회만 띄우도록 ConsumeFirstRunToast()로 소비한다(웹 game.js:361 토스트 대응).
            public bool firstRunToast;
            // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — StartRun 호출 시점의 SelectedAsc 스냅샷.
            public int asc;
        }

        private PendingLaunchInfo _pending;
        private bool _firstRunToastPending;

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
            RefreshLevelDevices();
            BuildFadeCanvas();
        }

        // 웹 파리티 P3-4 Opus 2차검수 필수② — 웹 game.js:179 `this.profile = this._loadProfile();
        // this._grantLevelDevices();`(Game 생성자) 대응. GameSession도 자기 생성자에서 동일 호출을
        // 하지만(GameSession.cs) 그건 "런 시작" 시점이라, Pick/Dex 화면이 참조하는 AppRoot.Profile은
        // 그때까지 1런(런 시작 전까지) 지연되는 문제가 있었다 — 프로필을 새로 읽는 두 지점(Awake·
        // EndRun) 모두에서 즉시 반영한다. 지급이 실제로 있었을 때만 저장(무변경 시 디스크 쓰기 생략).
        private void RefreshLevelDevices()
        {
            if (Profile == null) return;
            var got = Profile.GrantLevelDevices();
            if (got.Count > 0) ProfileStore.Save(Profile);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 A.5, 웹 game.js:2636 resetData()) — 홈 화면
        // "데이터 초기화" 확인 시트 확정 시 호출된다(MenuView.OnResetConfirmed). Session은 메뉴 화면에서만
        // 진입 가능해 항상 null이지만(방어적으로 한 번 더 비운다), 저장 파일을 지우고 새 프로필을 즉시
        // 만들어 그 자리에서 다시 저장한다(웹처럼 "리셋 즉시 영속화" — 다음 강종에도 리셋 상태 유지).
        // 닉네임(LoginView, PlayerPrefs "jackpotrun_nick")도 함께 지운다 — 웹 resetData()가 로컬스토리지
        // "slotweb_nick"까지 제거하는 것과 동일 파리티(다음 진입은 다시 로그인 화면부터). 랭킹 관련
        // PlayerPrefs(RankingService의 게스트 pid 등)는 웹처럼 그대로 둔다(별도 유지 — 리셋 대상 아님).
        // 웹과 달리 화면 전환은 하지 않는다(이미 메뉴 화면이므로 그대로 머무르고 카드만 새로고침).
        public void ResetProfile()
        {
            Session = null;
            ProfileStore.Delete();
            Profile = ProfileDto.FromDto(new PlayerProfileDto());
            ProfileStore.Save(Profile);
            PlayerPrefs.DeleteKey(LoginView.NickPrefKey);
            PlayerPrefs.Save();
            _introRoot?.Menu?.Refresh();
            Toast?.Show("데이터를 초기화했어요 — 처음부터 시작!");
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
            int asc;
            if (_pending.valid)
            {
                charId = _pending.charId;
                machineId = _pending.machineId;
                deviceId = _pending.deviceId;
                asc = _pending.asc;
                _firstRunToastPending = _pending.firstRunToast;
                _pending.valid = false;
            }
            else
            {
                // 설계 S8: "Play 씬을 직접 열고 Play를 눌렀을 때(에디터 개발 편의): PendingLaunch가
                // 없으면 기본 조합(novice/basic/"")으로 시작하고 경고 로그."
                charId = "novice";
                machineId = "basic";
                deviceId = "";
                asc = 0;
                Debug.LogWarning("[JackpotRun] Play 씬 단독 실행 — PendingLaunch 없음, 기본 조합(novice/basic)으로 시작합니다.");
            }

            Session = new GameSession(charId, machineId, deviceId ?? string.Empty, asc);
            root?.Bind(Session);
        }

        // ── Intro 내비게이션(뷰의 버튼 onClick이 직접 호출) ──────────────────────────────
        public void ShowMenu() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Menu);
        public void ShowPick() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Pick);
        public void ShowDex() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Dex);
        public void ShowLogin() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Login);
        public void ShowRank() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.Rank);
        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 B) — 홈 레벨 카드 탭 → 레벨 보상 화면.
        public void ShowLevelRewards() => _introRoot?.Router?.Show(ScreenRouter.ScreenId.LevelRewards);

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
        /// 이어받는다(설계 S8 "전환 흐름"). asc: 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — 기본값
        /// 0(첫 판 즉시시작 등 asc를 모르는 호출부는 그대로 일반 난이도로 시작).</summary>
        public void StartRun(string charId, string machineId, string deviceId, bool firstRunToast = false, int asc = 0)
        {
            if (_transitioning) return;
            _pending = new PendingLaunchInfo
            {
                charId = charId,
                machineId = machineId,
                deviceId = deviceId ?? string.Empty,
                valid = true,
                firstRunToast = firstRunToast,
                asc = asc,
            };
            StartCoroutine(TransitionToScene(PlaySceneName));
        }

        /// <summary>WEB_PARITY P1 ②: Play 씬 RunView가 런 시작 직후 1회만 소비하는 안내 토스트 플래그
        /// (첫 판 즉시시작 경로로 진입했을 때만 true) — 소비하면 즉시 false로 되돌아간다.</summary>
        public bool ConsumeFirstRunToast()
        {
            bool v = _firstRunToastPending;
            _firstRunToastPending = false;
            return v;
        }

        /// <summary>런 종료(GameOverPanel "메뉴로") — 세션을 비우고 프로필을 디스크에서 다시 읽은 뒤
        /// Intro 씬으로 돌아간다(GameSession.Do가 GAME_OVER 시 이미 저장을 마쳤으므로 여기서는 최신
        /// 업적/베스트기록을 메뉴·픽 화면에 즉시 반영하기 위해 다시 로드만 한다).</summary>
        public void EndRun()
        {
            if (_transitioning) return;
            Session = null;
            Profile = ProfileStore.Load();
            RefreshLevelDevices();
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
