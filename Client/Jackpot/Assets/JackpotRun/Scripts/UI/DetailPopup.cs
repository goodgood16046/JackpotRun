using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI
{
    /// <summary>카탈로그 엔트리 상세 팝업 — 반투명 배경(클릭 시 닫힘) + 중앙 스크롤 카드.</summary>
    public static class DetailPopup
    {
        public static void Show(RectTransform canvasRoot, JackpotRun.Data.CatalogEntry e)
        {
            if (canvasRoot == null || e == null) return;

            var bg = UiFactory.Panel(canvasRoot, "DetailPopupBg", new Color(0f, 0f, 0f, 0.66f));
            UiFactory.Fill(bg);
            var bgBtn = bg.gameObject.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(() => Object.Destroy(bg.gameObject));

            var cardGo = new GameObject("DetailCard", typeof(RectTransform), typeof(Image));
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.SetParent(bg, false);
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(900f, 1500f);
            cardGo.GetComponent<Image>().color = UiFactory.Hex("#151A2E");
            // 카드 위 클릭이 배경 Button까지 전파돼 팝업이 닫히지 않도록 별도 Button으로 이벤트를 소비.
            var cardBlocker = cardGo.AddComponent<Button>();
            cardBlocker.transition = Selectable.Transition.None;

            var cardCol = UiFactory.VGroup(cardRt, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiFactory.Fill(cardCol);

            var scroll = UiFactory.Scroll(cardCol, out var content, vertical: true);
            UiFactory.SizeHint(scroll, flexibleHeight: 1);

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 28, 20);
            vlg.spacing = 14;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildBody(content, e);

            var closeBtn = UiFactory.Button(cardCol, "닫기", new Vector2(0, 84),
                UiFactory.Hex("#2A3048"), UiFactory.Hex("#E8EAF2"), () => Object.Destroy(bg.gameObject));
            UiFactory.SizeHint(closeBtn, preferredHeight: 84);
        }

        private static void BuildBody(RectTransform parent, JackpotRun.Data.CatalogEntry e)
        {
            BuildIcon(parent, e);

            var title = UiFactory.Text(parent, $"{e.emoji} {e.nameKo}", 40, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleCenter, true);

            var metaParts = new List<string>();
            if (!string.IsNullOrEmpty(e.categoryLabel)) metaParts.Add(e.categoryLabel);
            if (!string.IsNullOrEmpty(e.tier)) metaParts.Add(TierLabel(e.tier));
            if (e.price >= 0) metaParts.Add($"가격 {JackpotRun.Core.NumberFormat.Comma(e.price)}");
            if (e.coinCost >= 0) metaParts.Add($"코인 {JackpotRun.Core.NumberFormat.Comma(e.coinCost)}");
            if (e.scoreMod >= 0f) metaParts.Add($"점수보정 ×{JackpotRun.Core.NumberFormat.Fmt(e.scoreMod)}");
            if (metaParts.Count > 0)
            {
                UiFactory.Text(parent, string.Join(" · ", metaParts), 22, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
            }

            if (!string.IsNullOrEmpty(e.descKo))
            {
                UiFactory.Text(parent, e.descKo, 24, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
            }

            if (e.unlockReq != null && e.unlockReq.Length > 0)
            {
                foreach (var req in e.unlockReq)
                {
                    UiFactory.Text(parent, $"해금: {req.stat} ≥ {JackpotRun.Core.NumberFormat.Comma(req.value)}",
                        20, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
                }
            }

            if (e.hasPick && e.pick != null)
            {
                BuildPickSection(parent, e.pick);
            }
        }

        private static string TierLabel(string tier)
        {
            switch (tier)
            {
                case "SILVER": return "🥈 실버";
                case "GOLD": return "🥇 골드";
                case "PRISM": return "🌈 프리즘";
                default: return tier;
            }
        }

        private static void BuildIcon(RectTransform parent, JackpotRun.Data.CatalogEntry e)
        {
            var row = UiFactory.HGroup(parent, 0, new RectOffset(0, 0, 0, 0), false, false);
            UiFactory.SizeHint(row, preferredHeight: 512);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            var sprite = JackpotRun.Data.JackpotCatalog.LoadSprite(e);
            if (sprite != null)
            {
                var img = UiFactory.Image(row, sprite, Color.white);
                img.rectTransform.sizeDelta = new Vector2(512f, 512f);
            }
            else
            {
                var emojiText = UiFactory.Text(row, e.emoji, 220, Color.white, TextAnchor.MiddleCenter);
                emojiText.rectTransform.sizeDelta = new Vector2(512f, 512f);
            }
        }

        private static void BuildPickSection(RectTransform parent, JackpotRun.Data.PickInfo p)
        {
            UiFactory.Text(parent, $"역할: {p.role}", 22, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
            UiFactory.Text(parent, $"효과: {p.eff}", 22, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
            if (!string.IsNullOrEmpty(p.build))
                UiFactory.Text(parent, $"빌드: {p.build}", 20, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
            if (p.tags != null && p.tags.Length > 0)
                UiFactory.Text(parent, $"태그: {string.Join(", ", p.tags)}", 20, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);

            string meters = $"난이도 {JackpotRun.Data.PickMeta.DiffLabel(p.diff)}   고점 {Stars(p.ceiling)}   안정 {Stars(p.stab)}   위험 {Stars(p.risk)}";
            UiFactory.Text(parent, meters, 22, UiFactory.Hex("#FFD23F"), TextAnchor.UpperLeft, true);

            if (p.pros != null && p.pros.Length > 0)
            {
                UiFactory.Text(parent, "장점", 22, UiFactory.Hex("#34D3C0"), TextAnchor.UpperLeft, true);
                UiFactory.Text(parent, string.Join("\n", Prefixed(p.pros, "＋ ")), 20, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
            }
            if (p.cons != null && p.cons.Length > 0)
            {
                UiFactory.Text(parent, "주의", 22, UiFactory.Hex("#FF6B6B"), TextAnchor.UpperLeft, true);
                UiFactory.Text(parent, string.Join("\n", Prefixed(p.cons, "－ ")), 20, UiFactory.Hex("#E8EAF2"), TextAnchor.UpperLeft);
            }
        }

        private static string Stars(int n) => new string('★', Mathf.Max(0, n));

        private static List<string> Prefixed(string[] items, string prefix)
        {
            var list = new List<string>(items.Length);
            foreach (var s in items) list.Add(prefix + s);
            return list;
        }
    }
}
