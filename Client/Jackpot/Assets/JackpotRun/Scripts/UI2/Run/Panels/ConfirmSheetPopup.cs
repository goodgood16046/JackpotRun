using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 범용 2버튼 확인 시트 — WEB_PARITY P1 ⑤(자발적 포기 확인, 웹 ui.js:863-871 giveUpAsk 시트) ·
    // P1 ④(DEVICE 노드 오퍼 — [장착하기]/[코인 +15])가 함께 쓴다. ManipPickPopup/PostSpinPanel과 동일한
    // 바텀시트 골격(BuildSheetChrome)만 재사용하고 내용(제목·설명·버튼 2개 라벨/콜백)은 Show() 호출부가
    // 그때그때 채운다 — 두 기능이 각각 별도 인스턴스로 씬에 존재한다(RunView가 giveUpConfirmPopup /
    // deviceOfferPopup 두 개를 따로 들고 있음, UiSceneBuilder.cs 참조).
    public sealed class ConfirmSheetPopup : MonoBehaviour
    {
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(다른 Run 패널들과 동일).
        private const float SheetSlideDuration = 0.24f;

        [SerializeField] private Button scrimButton;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private Text headText;
        [SerializeField] private Text descText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryButtonLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private Text secondaryButtonLabel;

        private void Awake()
        {
            gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
            if (scrimButton != null) scrimButton.onClick.AddListener(Hide);
        }

        /// <summary>secondaryLabel/onSecondary가 null이면 보조 버튼은 "취소"로만 동작(닫기, 콜백 없음).</summary>
        public void Show(string head, string desc, string primaryLabel, Action onPrimary,
            string secondaryLabel = "취소", Action onSecondary = null)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            if (headText != null) headText.text = head;
            if (descText != null) descText.text = desc;
            if (primaryButtonLabel != null) primaryButtonLabel.text = primaryLabel;
            if (secondaryButtonLabel != null) secondaryButtonLabel.text = secondaryLabel;

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveAllListeners();
                primaryButton.onClick.AddListener(() => { Hide(); onPrimary?.Invoke(); });
            }
            if (secondaryButton != null)
            {
                secondaryButton.onClick.RemoveAllListeners();
                secondaryButton.onClick.AddListener(() => { Hide(); onSecondary?.Invoke(); });
            }

            StopAllCoroutines();
            if (firstShow) StartCoroutine(EnterRoutine());
            else
            {
                if (dimGroup != null) dimGroup.alpha = 1f;
                if (cardRect != null) cardRect.anchoredPosition = Vector2.zero;
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        private IEnumerator EnterRoutine()
        {
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, SheetSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero, SheetSlideDuration, UiTween.Ease.OutCubic);
        }
    }
}
