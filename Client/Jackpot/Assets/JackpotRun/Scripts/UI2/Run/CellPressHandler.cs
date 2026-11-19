using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JackpotRun.UI2
{
    // 릴 칸 탭/롱프레스 구분 — FUN_OVERHAUL_DESIGN F4(홀드). 홀드가 가능한 상태에서는 탭=홀드 토글, 롱프레스(0.45s)=셀 정보로
    // 쓴다(웹 ui.js reelTap/롱프레스 대응). ReelView 가 칸을 만들 때마다 인덱스를 클로저로 묶어 Tap/LongPress 를 건다.
    // 롱프레스가 발동했으면 손을 뗄 때 오는 클릭(OnPointerClick)은 삼킨다 — 정보 시트가 뜬 뒤 홀드가 같이 토글되면 안 된다.
    // 칸의 자식(Icon/Tag/Streak 등)이 먼저 레이캐스트에 맞아도 이벤트는 부모로 버블링되어 이 컴포넌트가 받는다
    // (기존 Button.onClick 탭이 같은 방식으로 동작했다). 포인터가 칸 밖으로 나가면 롱프레스는 취소된다.
    public sealed class CellPressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        public const float LongPressSeconds = 0.45f;

        public Action Tap;
        public Action LongPress;

        private Coroutine _timer;
        private bool _longFired;

        public void OnPointerDown(PointerEventData eventData)
        {
            _longFired = false;
            CancelTimer();
            _timer = StartCoroutine(LongPressRoutine());
        }

        public void OnPointerUp(PointerEventData eventData) => CancelTimer();

        public void OnPointerExit(PointerEventData eventData) => CancelTimer();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_longFired) { _longFired = false; return; }   // 롱프레스 직후의 손 뗌 — 탭으로 취급하지 않는다
            Tap?.Invoke();
        }

        private void OnDisable()
        {
            _timer = null;
            _longFired = false;
        }

        private IEnumerator LongPressRoutine()
        {
            // 실시간 대기 — 잭팟 슬로모(Time.timeScale)에 영향받지 않는다.
            yield return new WaitForSecondsRealtime(LongPressSeconds);
            _timer = null;
            _longFired = true;
            LongPress?.Invoke();
        }

        private void CancelTimer()
        {
            if (_timer != null) { StopCoroutine(_timer); _timer = null; }
        }
    }
}
