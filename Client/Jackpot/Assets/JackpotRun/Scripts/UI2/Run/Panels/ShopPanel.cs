using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Data;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // EventShop 페이즈 패널 — ENGINE_PORT_DESIGN.md S7 Run/Panels/ShopPanel.cs. 오퍼 행 스태거 팝인
    // (0.08s, OutBack), 가격 pill, 코인 부족 시 구매 버튼 흔들림 피드백, 리롤/나가기("RunView 연출"·
    // "PerkOfferPanel/ShopPanel" 지시). 이관 원본: Scripts/UI/RunPanels.cs의 BuildShop/BuildShopRow.
    public sealed class ShopPanel : MonoBehaviour
    {
        private const float RowStagger = 0.08f;
        private const float RowPopDuration = 0.28f;
        private const float ShakeAmplitude = 8f; // 설계 미명시 — 코인 부족 흔들림 기본값
        private const float ShakeDuration = 0.25f;
        // S12c §6 — 시트 슬라이드업 "0.24s cubic-bezier(.2,.9,.3,1)" → OutCubic 근사(설계 명시).
        private const float SheetSlideDuration = 0.24f;

        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform rowsContent;
        [SerializeField] private RectTransform rowTemplate; // 자식 경로 계약: Icon/Name/Desc/PriceButton/PriceLabel
        [SerializeField] private Text emptyText;
        [SerializeField] private Button rerollButton;
        [SerializeField] private Text rerollButtonLabel;
        [SerializeField] private Button leaveButton;
        [SerializeField] private RectTransform cardRect; // S12c §6 — 시트(Card) 슬라이드업 대상
        [SerializeField] private CanvasGroup dimGroup; // S12c §6 — 배경 딤 페이드(BuildSheetChrome.dimGroup)

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (dimGroup != null) dimGroup.alpha = 0f;
        }

        public void Show(RunState run, Action<int> onBuy, Action onReroll, Action onLeave)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            // S8 항목⑤: astral 이모지(🛒🔁🪙 등)는 렌더링되지 않는다 — 한글 라벨만 사용.
            if (titleText != null) titleText.text = $"상점 · 코인 {NumberFormat.Comma(run.Coins)}";
            if (emptyText != null) emptyText.gameObject.SetActive(run.ShopOffer.Count == 0);

            if (rerollButtonLabel != null) rerollButtonLabel.text = $"리롤 ({Shop.RerollCost}코인)";
            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveAllListeners();
                rerollButton.onClick.AddListener(() => onReroll());
            }
            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveAllListeners();
                leaveButton.onClick.AddListener(() => onLeave());
            }

            var rows = BuildRows(run, onBuy);
            if (firstShow) StartCoroutine(EnterRoutine(rows));
            else
            {
                if (dimGroup != null) dimGroup.alpha = 1f;
                if (cardRect != null) cardRect.anchoredPosition = Vector2.zero;
                foreach (var r in rows) r.localScale = Vector3.one;
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        // S12c §6 — 시트 슬라이드업(0.24s OutCubic) + 배경 딤 페이드 동시 재생, 완료 후 행 스태거 팝인.
        private IEnumerator EnterRoutine(List<RectTransform> rows)
        {
            if (cardRect != null) cardRect.anchoredPosition = new Vector2(0f, -cardRect.rect.height);
            if (dimGroup != null) StartCoroutine(UiTween.FadeRoutine(dimGroup, 0f, 1f, SheetSlideDuration));
            if (cardRect != null)
                yield return UiTween.MoveRoutine(cardRect, cardRect.anchoredPosition, Vector2.zero, SheetSlideDuration, UiTween.Ease.OutCubic);

            yield return PopInRoutine(rows);
        }

        private IEnumerator PopInRoutine(List<RectTransform> rows)
        {
            foreach (var r in rows) if (r != null) r.localScale = Vector3.zero;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null) StartCoroutine(UiTween.ScaleRoutine(rows[i], Vector3.zero, Vector3.one, RowPopDuration, UiTween.Ease.OutBack));
                if (i < rows.Count - 1) yield return new WaitForSeconds(RowStagger);
            }
        }

        private List<RectTransform> BuildRows(RunState run, Action<int> onBuy)
        {
            var result = new List<RectTransform>();
            if (rowsContent == null || rowTemplate == null) return result;

            for (int i = rowsContent.childCount - 1; i >= 0; i--)
            {
                var child = rowsContent.GetChild(i);
                if (child == rowTemplate) continue;
                Destroy(child.gameObject);
            }

            for (int i = 0; i < run.ShopOffer.Count; i++)
            {
                int idx = i;
                var entry = run.ShopOffer[i];

                var row = Instantiate(rowTemplate, rowsContent);
                row.gameObject.SetActive(true);
                row.name = "ShopEntry_" + entry.id;
                result.Add(row);

                PCat cat = entry.kind == 'A' ? PCat.AUGMENT : entry.kind == 'R' ? PCat.RELIC : PCat.ITEM;
                var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get(CatalogIdOf(cat, entry.id)));

                string emoji, name, desc;
                if (entry.kind == 'A' || entry.kind == 'R')
                {
                    var perk = Perks.ById(entry.id);
                    emoji = perk?.emoji ?? "❔"; name = perk?.name ?? entry.id; desc = perk?.desc ?? "";
                }
                else
                {
                    var item = Items.ById(entry.id);
                    emoji = item?.emoji ?? "❔"; name = item?.name ?? entry.id; desc = item?.desc ?? "";
                }

                var icon = row.Find("Content/IconSlot/Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
                var iconEmoji = row.Find("Content/IconSlot/IconEmoji")?.GetComponent<Text>();
                if (iconEmoji != null) { iconEmoji.text = emoji; iconEmoji.gameObject.SetActive(sprite == null); }

                var nameText = row.Find("Content/InfoCol/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = $"{KindLabel(entry.kind)} {name}";
                var descText = row.Find("Content/InfoCol/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = desc;

                bool affordable = run.Coins >= entry.price;
                var priceButton = row.Find("Content/PriceButton")?.GetComponent<Button>();
                var priceImage = priceButton != null ? priceButton.GetComponent<Image>() : null;
                var priceLabel = row.Find("Content/PriceButton/PriceLabel")?.GetComponent<Text>();
                if (priceLabel != null) priceLabel.text = NumberFormat.Comma(entry.price);
                if (priceImage != null) priceImage.color = affordable ? UiKit.Accent : UiKit.Card;
                if (priceLabel != null) priceLabel.color = affordable ? UiKit.Bg : UiKit.TextSecondary;
                if (priceButton != null)
                {
                    priceButton.onClick.RemoveAllListeners();
                    var buttonRt = priceButton.GetComponent<RectTransform>();
                    priceButton.onClick.AddListener(() =>
                    {
                        if (affordable) onBuy(idx);
                        else if (buttonRt != null) StartCoroutine(UiTween.ShakeRoutine(buttonRt, ShakeAmplitude, ShakeDuration));
                    });
                }
            }
            return result;
        }

        private static string CatalogIdOf(PCat cat, string id)
        {
            switch (cat)
            {
                case PCat.AUGMENT: return "aug_" + id;
                case PCat.RELIC: return "rel_" + id;
                case PCat.CURSE: return "cur_" + id;
                case PCat.ITEM: return "item_" + id;
                default: return id;
            }
        }

        private static string KindLabel(char kind) => kind == 'A' ? "[증강]" : kind == 'R' ? "[유물]" : "[아이템]";
    }
}
