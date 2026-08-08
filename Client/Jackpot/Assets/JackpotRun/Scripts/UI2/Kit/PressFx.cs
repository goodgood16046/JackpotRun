using JackpotRun.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 버튼 프레스 스케일(0.96) + 비활성 알파 — ENGINE_PORT_DESIGN.md S7 "화면 사양" 공통 규칙
    // "모든 버튼 PressFx". UiKit.Button(...)이 생성하는 모든 버튼에 자동으로 붙는다(수동으로 붙일
    // 경우 같은 GameObject에 Selectable 계열 컴포넌트가 있어야 한다 — 없으면 프레스 스케일은
    // 눌림 감지가 안 되어 동작하지 않고, 비활성 알파만 항상 1로 유지된다).
    //
    // 이 컴포넌트는 자체 CanvasGroup을 하나 소유(없으면 Awake에서 추가)한다 — 부모 CanvasGroup(화면
    // 전환 페이드 등)과 곱해지므로 중첩되어도 안전하다.
    [DisallowMultipleComponent]
    public sealed class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.96f;
        private const float PressDuration = 0.08f;
        private const float ReleaseDuration = 0.12f;
        // pick.css .go:disabled{opacity:.42}(색은 그대로, 알파만) — Fable 육안 검수 수정(2026-07-31):
        // 이 알파가 버튼 비활성 페이드의 유일한 감쇠 지점이다(UiKit.Button.colors.disabledColor는
        // 이제 완전 불투명 — 이중 감쇠로 어두운 배경 위 골드가 탁한 갈색으로 보이던 문제 수정).
        private const float DisabledAlpha = 0.42f;
        private const float AlphaLerpPerSecond = 6f;

        // 비워두면 Awake에서 같은 GameObject의 Selectable(Button 등)을 찾는다. interactable==false일
        // 때만 비활성 알파·프레스 무시가 적용된다 — Selectable이 전혀 없으면 항상 활성 취급.
        [SerializeField] private Selectable target;

        // S13 §E — fx_btn_press는 "골드 버튼만"(설계 표). UiKit.Button이 배경색을 Accent와 비교해
        // SetGold(...)로 채워준다(수동으로 PressFx를 붙이는 호출부는 기본값 false — 골드가 아니면
        // 조용히 아무 것도 재생하지 않는다).
        [SerializeField] private bool isGoldButton;

        // 웹 파리티 P5(WEB_PARITY_DESIGN.md §1-A #17, 웹 ui.js:93-99 전역 클릭 위임) — 웹은
        // `[data-act]` 전체에 "tap" 사운드를 걸되 스핀 버튼(`data-act="spin"`, 메인+특수스핀 4버튼
        // 전부)만 예외로 뺀다(그 자리는 별도 "spin" 사운드가 이미 난다 — 웹 doSpin() 첫 줄
        // `snd.sfx("spin")`). RunView.WireOnce가 spinButton/modeButtons 4개에 SuppressTapSfx()를
        // 호출해 이 예외를 그대로 재현한다.
        private bool _suppressTapSfx;

        public void SuppressTapSfx() => _suppressTapSfx = true;

        private RectTransform _rt;
        private CanvasGroup _cg;
        private Vector3 _baseScale;
        private Coroutine _scaleRoutine;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
            if (target == null) target = GetComponent<Selectable>();
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        private void Update()
        {
            if (target == null || _cg == null) return;
            float targetAlpha = target.interactable ? 1f : DisabledAlpha;
            _cg.alpha = Mathf.MoveTowards(_cg.alpha, targetAlpha, AlphaLerpPerSecond * Time.deltaTime);
        }

        /// <summary>UiKit.Button이 배경색으로 골드 여부를 판정해 호출한다(빌드 시점 1회 고정).</summary>
        public void SetGold(bool gold) => isGoldButton = gold;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target != null && !target.interactable) return;
            RestartScale(_baseScale * PressedScale, PressDuration, UiTween.Ease.OutQuad);
            // 웹 파리티 P5 — "tap" 사운드는 골드 여부와 무관하게 거의 모든 버튼에서 난다(웹은
            // fx_btn_press/진동만 골드 전용이고 tap 사운드는 전역이다 — isGoldButton 밖에서 독립 재생).
            if (!_suppressTapSfx) SoundKit.Sfx("tap");
            if (isGoldButton)
            {
                FxKit.I?.Play(FxId.BtnPress, _rt);
                // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 작업 지시 B "Handheld.Vibrate 훅은 탭
                // 피드백 위치에") — fx_btn_press와 동일하게 "골드 버튼만"(설계 표 그대로, 진동도 주요
                // 액션에만 한정) 탭 시 짧게 진동한다. SettingsSheet.VibeEnabled(PlayerPrefs) 꺼져 있으면
                // 무동작.
                SettingsSheet.SafeVibrate();
            }
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release() => RestartScale(_baseScale, ReleaseDuration, UiTween.Ease.OutBack);

        private void RestartScale(Vector3 to, float duration, UiTween.Ease ease)
        {
            if (_rt == null) return;
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = UiTween.Scale(this, _rt, _rt.localScale, to, duration, ease);
        }
    }
}
