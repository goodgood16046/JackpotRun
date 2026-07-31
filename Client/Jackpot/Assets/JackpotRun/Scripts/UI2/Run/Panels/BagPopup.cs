using System;
using System.Collections;
using JackpotRun.Data;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 가방 팝업 — ENGINE_PORT_DESIGN.md S7 Run/Panels/BagPopup.cs. 🎒 버튼으로 아무 때나 열 수 있는
    // 모달(스크림 클릭으로 닫힘) — 페이즈 패널과 달리 RunPhase와 무관하게 독립적으로 뜬다.
    // 이관 원본: Scripts/UI/RunPanels.cs의 ShowBag.
    public sealed class BagPopup : MonoBehaviour
    {
        private const float ScaleInDuration = 0.25f; // 설계 미명시 — 팝업 등장 길이 기본값

        [SerializeField] private Button scrimButton; // 배경 클릭 시 닫힘
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform rowsContent;
        [SerializeField] private RectTransform rowTemplate; // 자식 경로 계약: Icon/IconEmoji/Name/Desc/UseButton
        [SerializeField] private Text emptyText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            gameObject.SetActive(false);
            if (scrimButton != null) scrimButton.onClick.AddListener(Hide);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        public void Show(RunState run, Action<string> onUse)
        {
            gameObject.SetActive(true);
            if (titleText != null) titleText.text = $"🎒 가방 ({run.Items.Count}/{ItemUse.ItemSlots})";
            if (emptyText != null) emptyText.gameObject.SetActive(run.Items.Count == 0);

            BuildRows(run, onUse);

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

        private void BuildRows(RunState run, Action<string> onUse)
        {
            if (rowsContent == null || rowTemplate == null) return;
            for (int i = rowsContent.childCount - 1; i >= 0; i--)
            {
                var child = rowsContent.GetChild(i);
                if (child == rowTemplate) continue;
                Destroy(child.gameObject);
            }

            for (int i = 0; i < run.Items.Count; i++)
            {
                var item = Items.ById(run.Items[i]);
                if (item == null) continue;

                var row = Instantiate(rowTemplate, rowsContent);
                row.gameObject.SetActive(true);
                row.name = "ItemRow_" + i;

                var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get("item_" + item.id));
                var icon = row.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
                var iconEmoji = row.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
                if (iconEmoji != null) { iconEmoji.text = item.emoji; iconEmoji.gameObject.SetActive(sprite == null); }

                var nameText = row.Find("Content/InfoCol/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = item.name;
                var descText = row.Find("Content/InfoCol/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = item.desc;

                string itemId = run.Items[i];
                var useBtn = row.Find("Content/UseButton")?.GetComponent<Button>();
                if (useBtn != null)
                {
                    useBtn.onClick.RemoveAllListeners();
                    useBtn.onClick.AddListener(() =>
                    {
                        onUse(itemId);
                        Hide();
                    });
                }
            }
        }
    }
}
