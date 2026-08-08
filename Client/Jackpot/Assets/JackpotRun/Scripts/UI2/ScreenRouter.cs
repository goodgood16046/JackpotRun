using System;
using System.Collections;
using UnityEngine;

namespace JackpotRun.UI2
{
    // 화면 전환 — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 ScreenRouter.cs: "CanvasGroup 페이드(0.18s) +
    // 활성화 토글, Overlay/Toast 관리". AppRoot가 씬에 1개 보유하고, 모든 화면(Menu/Pick/Run/Dex)의
    // 루트+CanvasGroup을 [SerializeField] 배열로 와이어링받는다(UiSceneBuilder가 채운다).
    //
    // ── UiTween 러너 계약 ────────────────────────────────────────────────────────────
    // 이 컴포넌트는 씬에 상주하며(AppRoot 산하, DontDestroyOnLoad 대상 아님 — 씬이 곧 진실이므로
    // 씬 전환 자체가 없다) 화면이 꺼지는 동안에도 자신은 활성 상태를 유지하는 유일한 대상이다.
    // 그래서 화면 전환 도중 끊기면 안 되는 트윈, 또는 특정 뷰의 생存 주기와 무관하게 계속 돌아야
    // 하는 트윈(드문 경우)의 host로 이 라우터를 쓸 수 있다: `UiTween.Fade(router, group, ...)`.
    // 뷰 자신의 로컬 연출(카드 펄스, 캐러셀 등)은 그 뷰 자신을 host로 쓰는 편이 자연스럽다(화면이
    // 완전히 꺼지면 같이 멎어야 자연스러운 연출이기 때문).
    public sealed class ScreenRouter : MonoBehaviour
    {
        public const float FadeDuration = 0.18f;

        public enum ScreenId
        {
            Title, // S12: Intro 씬의 첫 화면(웹 단독판 renderIntro 이식, TitleView.cs)
            Login,
            Menu,
            Pick,
            Dex,
            Rank, // S15: 글로벌 랭킹(RankView.cs)
            LevelRewards, // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15 B) — 레벨 보상 화면(LevelRewardsView.cs)
        }

        [Serializable]
        private struct ScreenEntry
        {
            public ScreenId id;
            public RectTransform root;
            public CanvasGroup group;
        }

        [SerializeField] private ScreenEntry[] screens = Array.Empty<ScreenEntry>();
        [SerializeField] private RectTransform overlayLayer; // 팝업(NodePanel 등, S7b)이 붙는 최상위 레이어
        [SerializeField] private ToastManager toast;

        public RectTransform OverlayLayer => overlayLayer;
        public ToastManager Toast => toast;
        public ScreenId Current { get; private set; }
        public bool IsTransitioning => _transition != null;

        private Coroutine _transition;
        private bool _initialized;

        private void Awake()
        {
            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                if (s.group != null) s.group.alpha = 0f;
                if (s.root != null) s.root.gameObject.SetActive(false);
            }
        }

        /// <summary>대상 화면으로 전환한다 — 현재 화면 페이드아웃(0.18s) → 비활성화 → 대상 활성화 →
        /// 페이드인(0.18s). 첫 호출(아직 화면이 하나도 켜져 있지 않을 때)은 페이드아웃 단계를 건너뛴다.</summary>
        public void Show(ScreenId id)
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionRoutine(id));
        }

        private IEnumerator TransitionRoutine(ScreenId id)
        {
            var to = Find(id);
            if (_initialized)
            {
                var from = Find(Current);
                if (from.root != null && from.root != to.root)
                {
                    yield return UiTween.FadeRoutine(from.group, 1f, 0f, FadeDuration);
                    if (from.root != null) from.root.gameObject.SetActive(false);
                }
            }

            if (to.root != null)
            {
                to.root.gameObject.SetActive(true);
                if (to.group != null) to.group.alpha = 0f;
                yield return UiTween.FadeRoutine(to.group, 0f, 1f, FadeDuration);
            }

            Current = id;
            _initialized = true;
            _transition = null;
        }

        private ScreenEntry Find(ScreenId id)
        {
            for (int i = 0; i < screens.Length; i++)
                if (screens[i].id == id) return screens[i];
            return default;
        }
    }
}
