using System;
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
        private const float ScaleInDuration = 0.25f; // 설계 미명시 — 팝업 등장 길이 기본값
        private static readonly string[] NeedsArgIds = { "dev_pin", "dev_copy", "dev_swap" };

        [SerializeField] private Button scrimButton;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Text headText;
        [SerializeField] private Text descText;
        [SerializeField] private RectTransform cellsContent;
        [SerializeField] private RectTransform cellButtonTemplate; // 자식 경로 계약: Label(Text)
        [SerializeField] private Button cancelButton;

        private void Awake()
        {
            if (cellButtonTemplate != null) cellButtonTemplate.gameObject.SetActive(false);
            gameObject.SetActive(false);
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

            gameObject.SetActive(true);
            if (headText != null) headText.text = $"{dev.emoji} {dev.name} — 칸 선택";
            if (descText != null) descText.text = dev.desc;

            BuildCellButtons(run.LastCells.Count, dev.id, onConfirm);

            if (cardRect != null)
            {
                StopAllCoroutines();
                StartCoroutine(UiTween.ScaleRoutine(cardRect, Vector3.zero, Vector3.one, ScaleInDuration, UiTween.Ease.OutBack));
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
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
