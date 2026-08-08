using System;
using System.Collections;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // MANIP 칸 선택 팝업 — ENGINE_PORT_DESIGN.md S7 Run/Panels/ManipPickPopup.cs. dev_pin/copy/swap만
    // 칸 번호(1-base) 선택 팝업을 띄우고, 그 외 장치(dev_reroll 등)는 팝업 없이 즉시 확정한다
    // (RunController.cs DeviceCmd 계약 — arg는 이 3종 전용). 이관 원본: Scripts/UI/RunPanels.cs의
    // ShowManipPicker.
    public sealed class ManipPickPopup : MonoBehaviour
    {
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(설계 명시,
        // 이전엔 중앙 팝업 scale-in 0.25s OutBack이었다).
        private const float SheetSlideDuration = 0.24f;
        private static readonly string[] NeedsArgIds = { "dev_pin", "dev_copy", "dev_swap" };

        [SerializeField] private Button scrimButton;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private CanvasGroup dimGroup; // S12c §6 — 배경 딤 페이드(BuildSheetChrome.dimGroup)
        [SerializeField] private Text headText;
        [SerializeField] private Text descText;
        [SerializeField] private RectTransform cellsContent;
        [SerializeField] private RectTransform cellButtonTemplate; // 자식 경로 계약: Label(Text)
        [SerializeField] private Button cancelButton;

        private void Awake()
        {
            if (cellButtonTemplate != null) cellButtonTemplate.gameObject.SetActive(false);
            // Opus 2차검수 필수⑤(2026-08-09) — 여기서 gameObject.SetActive(false)를 부르면 안 된다
            // (기존 결함, CellInfoSheet.cs Awake 주석에 전체 메커니즘 설명). 빌더가 이미 비활성으로 굽는다.
            if (dimGroup != null) dimGroup.alpha = 0f;
            if (scrimButton != null) scrimButton.onClick.AddListener(Hide);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
        }

        /// <summary>dev.id가 칸 인자를 요구하지 않으면 팝업 없이 즉시 onConfirm(dev.id, null)을 호출한다.</summary>
        public void Show(RunState run, DeviceDef dev, Action<string, int?> onConfirm)
        {
            if (dev == null) return;
            if (Array.IndexOf(NeedsArgIds, dev.id) < 0)
            {
                onConfirm(dev.id, null);
                return;
            }

            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);
            if (headText != null) headText.text = $"{dev.emoji} {dev.name} — 칸 선택";
            if (descText != null) descText.text = dev.desc;

            BuildCellButtons(run.LastCells.Count, dev.id, onConfirm);

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

        // S12c §6 — 시트 슬라이드업(0.24s OutCubic) + 배경 딤 페이드 동시 재생.
        private IEnumerator EnterRoutine()
        {
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, SheetSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero, SheetSlideDuration, UiTween.Ease.OutCubic);
        }

        private void BuildCellButtons(int cellCount, string deviceId, Action<string, int?> onConfirm)
        {
            if (cellsContent == null || cellButtonTemplate == null) return;
            for (int i = cellsContent.childCount - 1; i >= 0; i--)
            {
                var child = cellsContent.GetChild(i);
                if (child == cellButtonTemplate) continue;
                Destroy(child.gameObject);
            }

            for (int i = 1; i <= cellCount; i++)
            {
                int n = i;
                var btn = Instantiate(cellButtonTemplate, cellsContent);
                btn.gameObject.SetActive(true);
                btn.name = "Cell_" + n;

                var label = btn.Find("Label")?.GetComponent<Text>();
                if (label != null) label.text = n.ToString();

                var button = btn.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() =>
                    {
                        onConfirm(deviceId, n);
                        Hide();
                    });
                }
            }
        }
    }
}
