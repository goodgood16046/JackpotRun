using System.Collections;
using UnityEngine;

namespace JackpotRun.UI2
{
    // Intro 씬(Title/Login/Menu/Pick/Dex/Rank)의 씬 소유자 — ENGINE_PORT_DESIGN.md S8 "영속 계층": "각 씬은
    // IntroSceneRoot/PlaySceneRoot MonoBehaviour가 자기 씬의 뷰를 [SerializeField]로 들고, Awake에서
    // AppRoot.Instance에 자기를 등록한다(역방향 참조만)". UiSceneBuilder.BuildIntroScene이 씬에
    // 정확히 1개 생성하고 router/화면 6종을 와이어링한다(Rank는 S15 추가).
    //
    // S12: 첫 화면이 Title로 바뀌었다(이전엔 여기서 닉네임 유무를 미리 판정해 Login/Menu 중 하나로
    // 바로 진입했다) — 닉네임 판정은 이제 TitleView의 시작 버튼을 누른 "이후"로 미뤘다(웹 단독판
    // renderIntro→exitIntro 흐름 그대로).
    public sealed class IntroSceneRoot : MonoBehaviour
    {
        [SerializeField] private ScreenRouter router;
        [SerializeField] private TitleView titleView;
        [SerializeField] private LoginView loginView;
        [SerializeField] private MenuView menuView;
        [SerializeField] private PickView pickView;
        [SerializeField] private DexView dexView;
        [SerializeField] private RankView rankView; // S15: 글로벌 랭킹

        // 오로라 배경(S12 §5 "배경") — 캔버스 최하단에 항상 떠 있어 화면 전환과 무관하게 이
        // SceneRoot가 직접 호스트로 돌린다(개별 화면 뷰는 전환마다 SetActive(false)되어 루프가
        // 끊긴다).
        [SerializeField] private RectTransform auroraRect;

        public ScreenRouter Router => router;
        public TitleView Title => titleView;
        public LoginView Login => loginView;
        public MenuView Menu => menuView;
        public PickView Pick => pickView;
        public DexView Dex => dexView;
        public RankView Rank => rankView;

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
            if (auroraRect != null) StartCoroutine(AuroraLoop());
        }

        /// <summary>항상 Title부터 보여준다(설계 S12 "IntroSceneRoot 첫 화면을 Title로") — 닉네임
        /// 유무 판정은 이제 TitleView.OnStartClicked가 담당한다.</summary>
        public void ShowInitialScreen()
        {
            if (router == null) return;
            router.Show(ScreenRouter.ScreenId.Title);
        }

        // 오로라(§5) — CSS `animation: aurora 16s ease-in-out infinite alternate`: 16s에 걸쳐
        // scale 1.02→1.08 + 미세 이동, 그다음 16s에 걸쳐 역방향(alternate) — 왕복 코루틴 2개를
        // 무한 반복해 재현한다. ease-in-out은 UiTween에 대응 이징이 없어 OutQuad로 근사(§7 규칙).
        private IEnumerator AuroraLoop()
        {
            const float period = 16f;
            const float fromScale = 1.02f, toScale = 1.08f;
            var fromPos = new Vector2(-22f, -19f); // CSS translate3d(-2%,-1%,0) × 1080/1920 근사
            var toPos = new Vector2(22f, 38f);     // CSS translate3d(2%,2%,0)

            while (true)
            {
                yield return UiTween.FloatRoutine(0f, 1f, period, t01 => ApplyAurora(t01, fromScale, toScale, fromPos, toPos), UiTween.Ease.OutQuad);
                if (auroraRect == null) yield break;
                yield return UiTween.FloatRoutine(1f, 0f, period, t01 => ApplyAurora(t01, fromScale, toScale, fromPos, toPos), UiTween.Ease.OutQuad);
                if (auroraRect == null) yield break;
            }
        }

        private void ApplyAurora(float t01, float fromScale, float toScale, Vector2 fromPos, Vector2 toPos)
        {
            if (auroraRect == null) return;
            float s = Mathf.Lerp(fromScale, toScale, t01);
            auroraRect.localScale = new Vector3(s, s, 1f);
            auroraRect.anchoredPosition = Vector2.Lerp(fromPos, toPos, t01);
        }
    }
}
