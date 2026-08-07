using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 화면 하단 토스트 — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 ToastManager.cs. 큐 기반으로 한 번에
    // 1개만 표시하고, Show() 연속 호출은 순서대로 재생된다. ScreenRouter가 씬에 1개 보유(Router.Toast)
    // 하고, 임의 뷰가 `appRoot.Router.Toast.Show("...")`로 사용한다. 자리(root)와 텍스트는
    // UiSceneBuilder가 미리 만들어 두고 [SerializeField]로 와이어링한다(런타임 코드생성 없음).
    public sealed class ToastManager : MonoBehaviour
    {
        private const float FadeDuration = 0.18f;
        private const float DefaultHold = 1.6f;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text label;

        private readonly Queue<(string message, float hold)> _queue = new Queue<(string, float)>();
        private Coroutine _pump;

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        /// <summary>토스트를 큐에 넣는다. 이미 재생 중이면 이전 토스트가 끝난 뒤 이어서 나온다.</summary>
        public void Show(string message, float hold = DefaultHold)
        {
            if (string.IsNullOrEmpty(message) || group == null || label == null) return;
            _queue.Enqueue((message, Mathf.Max(0.1f, hold)));
            if (_pump == null) _pump = StartCoroutine(Pump());
        }

        /// <summary>큐를 비우고 즉시 숨긴다(화면 전환 등으로 더 이상 의미 없는 토스트를 취소할 때).</summary>
        public void Clear()
        {
            _queue.Clear();
            if (_pump != null)
            {
                StopCoroutine(_pump);
                _pump = null;
            }
            if (group != null) group.alpha = 0f;
        }

        private IEnumerator Pump()
        {
            while (_queue.Count > 0)
            {
                var (message, hold) = _queue.Dequeue();
                label.text = message;
                yield return UiTween.FadeRoutine(group, 0f, 1f, FadeDuration);
                yield return new WaitForSeconds(hold);
                yield return UiTween.FadeRoutine(group, 1f, 0f, FadeDuration);
            }
            _pump = null;
        }
    }
}
