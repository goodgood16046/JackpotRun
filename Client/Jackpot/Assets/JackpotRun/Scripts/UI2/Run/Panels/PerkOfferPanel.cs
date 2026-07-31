using System;
using System.Collections;
using System.Collections.Generic;
using JackpotRun.Data;
using JackpotRun.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // EventAugment/EventRelic 페이즈 패널 — ENGINE_PORT_DESIGN.md S7 Run/Panels/PerkOfferPanel.cs.
    // 카드 3장 스태거 팝인(0.08s, OutBack), 티어 리본, 시너지 주입 🧩 배지 + 보라 테두리, 보류 카드
    // 🗂️ 배지("RunView 연출"·"PerkOfferPanel/ShopPanel" 지시 그대로). 이관 원본: Scripts/UI/RunPanels.cs의
    // BuildPerkOffer/BuildPerkCard(레이아웃은 버리고 배지·해금 문구·보류/재추첨 로직만).
    public sealed class PerkOfferPanel : MonoBehaviour
    {
        private const float CardStagger = 0.08f; // 설계 명시
        private const float CardPopDuration = 0.28f; // 설계 미명시 — OutBack 팝인 길이 기본값

        // S8 항목⑤: 메달 이모지(🥈🥇🌈)는 astral이라 렌더링되지 않는다 — 카드의 Tier 텍스트 줄이
        // 이미 등급명을 표시하므로 접두어는 제거했다(빈 문자열).
        private static readonly Dictionary<Tier, string> TierMedal = new Dictionary<Tier, string>
        {
            { Tier.SILVER, "" }, { Tier.GOLD, "" }, { Tier.PRISM, "" },
        };

        [SerializeField] private Text titleText;
        [SerializeField] private Text bannerText;
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private RectTransform cardTemplate; // 자식 경로 계약: Icon/Name/Tier/Badges/Desc/PickButton/HoldButton
        [SerializeField] private Button retakeButton;
        [SerializeField] private Text retakeButtonLabel;

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
        }

        public void Show(RunState run, RunEvent offerEvent, Action<int> onPick, Action<int> onHold, Action onRetake)
        {
            bool firstShow = !gameObject.activeSelf;
            gameObject.SetActive(true);

            // S8 항목⑤: astral 이모지 제거 — 한글 라벨만 사용.
            bool isAugment = run.Phase == RunPhase.EventAugment;
            if (titleText != null) titleText.text = isAugment ? "증강 선택" : "유물 선택";

            var banners = new List<string>();
            if (offerEvent != null)
            {
                if (offerEvent.offerBossPrism) banners.Add("보스 클리어! 프리즘 확정");
                if (offerEvent.offerTierBumped) banners.Add("행운! 등급업");
                if (offerEvent.offerHeldIncluded) banners.Add("보류 후보 포함");
                if (offerEvent.offerRetake) banners.Add("재추첨 결과");
            }
            if (bannerText != null)
            {
                bannerText.text = string.Join(" · ", banners);
                bannerText.gameObject.SetActive(banners.Count > 0);
            }

            bool canHold = isAugment && Shop.HasDevice(run, "dev_holdfile") && string.IsNullOrEmpty(run.HeldAug);
            bool canRetake = Shop.HasDevice(run, "dev_retake") && !run.UsedCmds.Contains("RETAKE");
            if (retakeButton != null)
            {
                retakeButton.gameObject.SetActive(canRetake);
                retakeButton.onClick.RemoveAllListeners();
                if (canRetake) retakeButton.onClick.AddListener(() => onRetake());
                if (retakeButtonLabel != null) retakeButtonLabel.text = $"재추첨 ({Formulas.RETAKE_COIN_COST}코인)";
            }

            var cards = BuildCards(run, offerEvent, onPick, canHold ? onHold : null);
            if (firstShow) StartCoroutine(PopInRoutine(cards));
            else foreach (var c in cards) c.localScale = Vector3.one;
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        private IEnumerator PopInRoutine(List<RectTransform> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;
                cards[i].localScale = Vector3.zero;
            }
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null) StartCoroutine(UiTween.ScaleRoutine(cards[i], Vector3.zero, Vector3.one, CardPopDuration, UiTween.Ease.OutBack));
                if (i < cards.Count - 1) yield return new WaitForSeconds(CardStagger);
            }
        }

        private List<RectTransform> BuildCards(RunState run, RunEvent offerEvent, Action<int> onPick, Action<int> onHold)
        {
            var result = new List<RectTransform>();
            if (cardsContent == null || cardTemplate == null) return result;

            for (int i = cardsContent.childCount - 1; i >= 0; i--)
            {
                var child = cardsContent.GetChild(i);
                if (child == cardTemplate) continue;
                Destroy(child.gameObject);
            }

            var ids = run.PerkOfferIds;
            for (int i = 0; i < ids.Count; i++)
            {
                int idx = i;
                var perk = Perks.ById(ids[i]);
                if (perk == null) continue;
                bool heldBadge = offerEvent != null && offerEvent.offerHeldIncluded && idx == 0;
                bool synergyBadge = offerEvent != null && offerEvent.offerSynergyPerkId == perk.id;

                var card = Instantiate(cardTemplate, cardsContent);
                card.gameObject.SetActive(true);
                card.name = "PerkCard_" + perk.id;
                result.Add(card);

                var icon = card.Find("Content/TopRow/IconSlot/Icon")?.GetComponent<Image>();
                var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get(CatalogIdOf(perk.cat, perk.id)));
                if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
                var iconEmoji = card.Find("Content/TopRow/IconSlot/IconEmoji")?.GetComponent<Text>();
                if (iconEmoji != null) { iconEmoji.text = perk.emoji; iconEmoji.gameObject.SetActive(sprite == null); }

                string medal = TierMedal.TryGetValue(perk.tier, out var mk) ? mk : "";
                var nameText = card.Find("Content/TopRow/NameCol/Name")?.GetComponent<Text>();
                if (nameText != null) nameText.text = $"{medal}{perk.name}";

                var tierText = card.Find("Content/TopRow/NameCol/Tier")?.GetComponent<Text>();
                if (tierText != null) tierText.text = TierLabel(perk.tier);

                var badgesText = card.Find("Content/TopRow/NameCol/Badges")?.GetComponent<Text>();
                if (badgesText != null)
                {
                    string badges = (heldBadge ? "[보류] " : "") + (synergyBadge ? "[시너지]" : "");
                    badgesText.text = badges;
                    badgesText.gameObject.SetActive(!string.IsNullOrEmpty(badges));
                }

                var outline = card.GetComponent<Outline>();
                if (outline != null) outline.enabled = synergyBadge; // 보라 테두리(시너지 주입 카드)

                var descText = card.Find("Content/Desc")?.GetComponent<Text>();
                if (descText != null) descText.text = perk.desc;

                var pickBtn = card.Find("Content/ButtonRow/PickButton")?.GetComponent<Button>();
                if (pickBtn != null)
                {
                    pickBtn.onClick.RemoveAllListeners();
                    var cardRt = card;
                    var tierColor = UiKit.TierColor(perk.tier.ToString());
                    pickBtn.onClick.AddListener(() =>
                    {
                        // S7c 연출 훅: "PerkOfferPanel: 카드 선택 시 PerkPick(티어색)."
                        FxKit.I?.Play(FxId.PerkPick, cardRt, tierColor);
                        onPick(idx);
                    });
                }

                var holdBtn = card.Find("Content/ButtonRow/HoldButton")?.GetComponent<Button>();
                if (holdBtn != null)
                {
                    bool show = onHold != null;
                    holdBtn.gameObject.SetActive(show);
                    holdBtn.onClick.RemoveAllListeners();
                    if (show) holdBtn.onClick.AddListener(() => onHold(idx));
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

        private static string TierLabel(Tier t)
        {
            switch (t)
            {
                case Tier.SILVER: return "실버";
                case Tier.GOLD: return "골드";
                default: return "프리즘";
            }
        }
    }
}
