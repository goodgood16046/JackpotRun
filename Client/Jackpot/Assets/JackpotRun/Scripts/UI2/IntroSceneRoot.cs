using UnityEngine;

namespace JackpotRun.UI2
{
    // Intro 씬(Login/Menu/Pick/Dex)의 씬 소유자 — ENGINE_PORT_DESIGN.md S8 "영속 계층": "각 씬은
    // IntroSceneRoot/PlaySceneRoot MonoBehaviour가 자기 씬의 뷰를 [SerializeField]로 들고, Awake에서
    // AppRoot.Instance에 자기를 등록한다(역방향 참조만)". UiSceneBuilder.BuildIntroScene이 씬에
    // 정확히 1개 생성하고 router/화면 4종을 와이어링한다.
    public sealed class IntroSceneRoot : MonoBehaviour
    {
        [SerializeField] private ScreenRouter router;
        [SerializeField] private LoginView loginView;
        [SerializeField] private MenuView menuView;
        [SerializeField] private PickView pickView;
        [SerializeField] private DexView dexView;

        public ScreenRouter Router => router;
        public LoginView Login => loginView;
        public MenuView Menu => menuView;
        public PickView Pick => pickView;
        public DexView Dex => dexView;

        public ToastManager Toast => router != null ? router.Toast : null;
        public RectTransform OverlayLayer => router != null ? router.OverlayLayer : null;

        private void Awake()
        {
            // AppRoot는 RuntimeInitializeOnLoadMethod(BeforeSceneLoad)로 이 씬의 어떤 Awake보다도
            // 먼저 생성되어 있다(설계 S8 "생성" 절) — Instance가 null일 일은 없지만 방어적으로 처리.
            var app = AppRoot.Instance;
            if (app != null) app.RegisterIntro(this);
        }

        // 첫 화면 표시는 Start에서 한다. Awake에서 부르면 같은 GameObject의 ScreenRouter.Awake가
        // 아직 안 돌았을 수 있고, 그 Awake가 "모든 화면 비활성화"를 하므로 방금 켠 화면이 다시 꺼진다
        // (라우터 상태만 Login으로 남고 화면은 검은 채로 보이던 버그).
        private void Start()
        {
            ShowInitialScreen();
        }

        /// <summary>닉네임이 이미 있으면 Login을 건너뛰고 Menu로(설계 S8 LoginView).</summary>
        public void ShowInitialScreen()
        {
            if (router == null) return;
            bool hasNick = !string.IsNullOrEmpty(LoginView.SavedNick());
            router.Show(hasNick ? ScreenRouter.ScreenId.Menu : ScreenRouter.ScreenId.Login);
        }
    }
}
