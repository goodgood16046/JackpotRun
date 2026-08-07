using JackpotRun.Game;
using UnityEngine;

namespace JackpotRun.UI2
{
    // Play 씬(런 플레이 단독)의 씬 소유자 — ENGINE_PORT_DESIGN.md S8 "영속 계층"·"전환 흐름":
    // "PlaySceneRoot.Awake → AppRoot.ConsumePendingLaunch() → GameSession 생성 → RunView.Bind(session)
    // → 페이드인". Play 씬은 화면이 RunView 하나뿐이라(오버레이 패널 7종은 RunView가 직접 소유)
    // ScreenRouter를 쓰지 않는다 — Toast/OverlayLayer는 이 루트가 직접 들고 있다.
    public sealed class PlaySceneRoot : MonoBehaviour
    {
        [SerializeField] private RunView runView;
        [SerializeField] private RectTransform overlayLayer;
        [SerializeField] private ToastManager toast;

        public RunView RunView => runView;
        public RectTransform OverlayLayer => overlayLayer;
        public ToastManager Toast => toast;

        private GameSession _pendingBind;

        private void Awake()
        {
            var app = AppRoot.Instance;
            if (app != null) app.RegisterPlay(this);
        }

        // 실제 바인딩은 Start에서 — Awake 단계에서 RunView/패널의 Awake가 아직 안 돌았을 수 있다
        // (IntroSceneRoot와 같은 Awake 순서 경합 방지).
        private void Start()
        {
            _started = true;
            var session = _pendingBind ?? (AppRoot.Instance != null ? AppRoot.Instance.Session : null);
            if (session != null) runView?.Bind(session);
            _pendingBind = null;
        }

        /// <summary>AppRoot.RegisterPlay가 GameSession 생성 직후 호출 — Start에서 RunView에 넘긴다.</summary>
        public void Bind(GameSession session)
        {
            _pendingBind = session;
            if (isActiveAndEnabled && runView != null && _started) runView.Bind(session);
        }

        private bool _started;
    }
}
