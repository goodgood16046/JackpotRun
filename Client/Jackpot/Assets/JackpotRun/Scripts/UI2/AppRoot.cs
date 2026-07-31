using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;

namespace JackpotRun.UI2
{
    // 씬 엔트리 — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 AppRoot.cs: "프로필 로드, ScreenRouter 보유,
    // GameSession 수명주기(기존 로직 이관)". UiSceneBuilder(메뉴 "JackpotRun/Build UI Scene")가 씬에
    // 정확히 1개 생성하고 router/menuView/pickView 등 모든 참조를 [SerializeField]로 와이어링한다.
    //
    // 기존 런타임 코드생성 부트스트랩(Scripts/UI/JackpotRunApp.cs)은 이 컴포넌트가 씬에 있으면
    // 아무것도 하지 않는다(JackpotRunApp.Bootstrap 가드, 이 파일과 짝을 이루는 유일한 기존 파일
    // 수정 지점).
    //
    // [이번 슬라이스 범위] RunView/DexView는 S7b — 여기서는 자리표시 화면(빈 루트)으로 전환만 하고
    // GameSession은 정상적으로 생성·보유한다(RunScreen(구)과 동일하게 GameSession.Do(...)를 호출할
    // 실제 뷰가 S7b에서 이 세션을 이어받는다).
    public sealed class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        [SerializeField] private ScreenRouter router;
        [SerializeField] private MenuView menuView;
        [SerializeField] private PickView pickView;

        public ScreenRouter Router => router;
        public MenuView Menu => menuView;
        public PickView Pick => pickView;
        public PlayerProfile Profile { get; private set; }

        /// <summary>진행 중인 게임 런 — StartRun에서 새로 생성된다. S7b RunView가 이어받아 쓴다.</summary>
        public GameSession Session { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Profile = ProfileStore.Load();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ShowMenu();
        }

        public void ShowMenu()
        {
            if (router == null) return;
            router.Show(ScreenRouter.ScreenId.Menu);
        }

        public void ShowPick()
        {
            if (router == null) return;
            router.Show(ScreenRouter.ScreenId.Pick);
        }

        /// <summary>DexView는 S7b — 이번 슬라이스는 자리표시 화면으로만 전환한다.</summary>
        public void ShowDex()
        {
            if (router == null) return;
            router.Show(ScreenRouter.ScreenId.Dex);
        }

        /// <summary>PickView "시작 예약" → 실제 런 시작(기존 JackpotRunApp.ShowRun 이관). devId는
        /// ""(장치 없이)도 허용. RunView(S7b)가 없는 이번 슬라이스는 세션 생성 + 자리표시 화면
        /// 전환까지만 수행한다 — GameSession 자체는 완전히 살아있어 다음 슬라이스가 바로 이어받는다.</summary>
        public void StartRun(string charId, string machineId, string deviceId)
        {
            Session = new GameSession(charId, machineId, deviceId ?? string.Empty);
            if (router != null) router.Show(ScreenRouter.ScreenId.Run);
        }

        /// <summary>런 종료(게임오버 → 메뉴 복귀 등, S7b RunView가 호출) — 세션을 비우고 프로필을
        /// 디스크에서 다시 읽는다(GameSession.Do가 GAME_OVER 시 이미 저장을 마쳤으므로 최신
        /// 업적/베스트기록을 메뉴·픽 화면에 즉시 반영하기 위함).</summary>
        public void EndRun()
        {
            Session = null;
            Profile = ProfileStore.Load();
            ShowMenu();
        }
    }
}
