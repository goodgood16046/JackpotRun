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

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target != null && !target.interactable) return;
            RestartScale(_baseScale * PressedScale, PressDuration, UiTween.Ease.OutQuad);
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
