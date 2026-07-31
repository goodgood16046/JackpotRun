using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JackpotRun.UI
{
    /// <summary>
    /// 잭팟런 UI 부트스트랩. 씬을 건드리지 않고 RuntimeInitializeOnLoadMethod로
    /// EventSystem/Canvas/화면을 자동 생성한다.
    /// </summary>
    public class JackpotRunApp : MonoBehaviour
    {
        public static readonly Color BgColor = UiFactory.Hex("#0B0E1A");

        public JackpotRun.Data.PlayerState Player { get; private set; }

        /// <summary>진행 중인 게임 런(S6) — ShowRun에서 새로 생성되고 화면이 살아있는 동안 유지된다.</summary>
        public JackpotRun.Game.GameSession Session { get; private set; }

        /// <summary>화면들이 최상위 오버레이(팝업 등)를 붙일 때 쓰는 캔버스 루트.</summary>
        public RectTransform CanvasRoot => _canvasRt;

        private Canvas _canvas;
        private RectTransform _canvasRt;
        private RectTransform _screenRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<JackpotRun.UI2.AppRoot>() != null) return; // S7: 씬 기반 AppRoot가 있으면 구 코드생성 부트스트랩은 아무것도 하지 않는다
            if (FindObjectOfType<JackpotRunApp>() != null) return;
            var go = new GameObject("JackpotRunApp");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<JackpotRunApp>();
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildCanvas();
            Player = JackpotRun.Data.DemoData.Demo();
            ShowMenu();
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("JackpotRunCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(canvasGo);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRt = (RectTransform)canvasGo.transform;
        }

        public void ShowMenu()
        {
            ClearScreen();
            _screenRoot = NewScreenRoot("MainMenuScreen");
            MainMenuScreen.Build(_screenRoot, this);
        }

        public void ShowPick()
        {
            ClearScreen();
            _screenRoot = NewScreenRoot("PickScreen");
            PickScreen.Build(_screenRoot, this);
        }

        public void ShowDex()
        {
            ClearScreen();
            _screenRoot = NewScreenRoot("DexScreen");
            DexScreen.Build(_screenRoot, this);
        }

        /// <summary>PickScreen "시작 예약" → 실제 런 시작. devKey는 ""(장치 없이)도 허용.</summary>
        public void ShowRun(string charKey, string macKey, string devKey)
        {
            ClearScreen();
            Session = new JackpotRun.Game.GameSession(charKey, macKey, devKey);
            _screenRoot = NewScreenRoot("RunScreen");
            RunScreen.Build(_screenRoot, this);
        }

        private RectTransform NewScreenRoot(string name)
        {
            var root = UiFactory.Panel(_canvasRt, name, BgColor);
            UiFactory.Fill(root);
            return root;
        }

        private void ClearScreen()
        {
            if (_screenRoot != null) Destroy(_screenRoot.gameObject);
            _screenRoot = null;
        }
    }
}
