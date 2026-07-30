using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI
{
    /// <summary>jackpotdex 포팅 — 카탈로그 브라우저(8 카테고리 탭 + 2열 카드 그리드 + 상세 팝업).</summary>
    public static class DexScreen
    {
        public static void Build(RectTransform parent, JackpotRunApp app)
        {
            new Ctx(parent, app).Init();
        }

        private class Ctx
        {
            private static readonly Dictionary<string, string> TierMark = new Dictionary<string, string>
            {
                { "SILVER", "🥈" }, { "GOLD", "🥇" }, { "PRISM", "🌈" },
            };

            private readonly RectTransform _parent;
            private readonly JackpotRunApp _app;
            private readonly JackpotRun.Data.PlayerState _player;

            private string _cat;
            private readonly Dictionary<string, Image> _tabImages = new Dictionary<string, Image>();
            private RectTransform _gridContent;

            public Ctx(RectTransform parent, JackpotRunApp app)
            {
                _parent = parent;
                _app = app;
                _player = app.Player;
            }

            public void Init()
            {
                var root = UiFactory.VGroup(_parent, 0, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.Fill(root);

                BuildHeader(root);
                BuildStatsRow(root);
                BuildTabsRow(root);
                BuildGrid(root);

                var order = JackpotRun.Data.JackpotCatalog.CategoryOrder;
                SetCategory(order != null && order.Length > 0 ? order[0] : "char");
            }

            // ── 헤더 / 통계 ────────────────────────────────────
            private void BuildHeader(RectTransform root)
            {
                var header = UiFactory.HGroup(root, 16, new RectOffset(24, 24, 16, 8), true, true);
                UiFactory.SizeHint(header, preferredHeight: 90);

                var title = UiFactory.Text(header, "📖 잭팟런 도감", 30, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(title, flexibleWidth: 1);

                var menuBtn = UiFactory.Button(header, "← 메뉴", new Vector2(160, 70),
                    UiFactory.Hex("#2A3048"), UiFactory.Hex("#E8EAF2"), () => _app.ShowMenu());
                UiFactory.SizeHint(menuBtn, preferredWidth: 160);
            }

            private void BuildStatsRow(RectTransform root)
            {
                var row = UiFactory.HGroup(root, 12, new RectOffset(24, 24, 4, 12), true, true);
                UiFactory.SizeHint(row, preferredHeight: 96);

                AddStatTile(row, "🏆 최고", "-");
                AddStatTile(row, "🧗 최고 스테이지", "-");
                AddStatTile(row, "🔁 런", "-");
                AddStatTile(row, "📈 통산", "-");
            }

            private void AddStatTile(RectTransform row, string label, string value)
            {
                var cell = UiFactory.Panel(row, "Stat", UiFactory.Hex("#151A2E"));
                UiFactory.SizeHint(cell, flexibleWidth: 1, preferredHeight: 96);

                var col = UiFactory.VGroup(cell, 2, new RectOffset(8, 8, 10, 10), true, true);
                UiFactory.Fill(col);
                var l = UiFactory.Text(col, label, 16, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
                UiFactory.SizeHint(l, preferredHeight: 24);
                var v = UiFactory.Text(col, value, 26, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleCenter, true);
                UiFactory.SizeHint(v, flexibleHeight: 1);
            }

            // ── 카테고리 탭 ────────────────────────────────────
            private void BuildTabsRow(RectTransform root)
            {
                var scroll = UiFactory.Scroll(root, out var content, vertical: false);
                UiFactory.SizeHint(scroll, preferredHeight: 96);

                var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 10;
                hlg.padding = new RectOffset(20, 20, 8, 8);
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childAlignment = TextAnchor.MiddleLeft;

                var csf = content.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                foreach (var cat in JackpotRun.Data.JackpotCatalog.CategoryOrder)
                {
                    var btnGo = new GameObject("Tab_" + cat, typeof(RectTransform), typeof(Image), typeof(Button));
                    var rt = (RectTransform)btnGo.transform;
                    rt.SetParent(content, false);

                    var img = btnGo.GetComponent<Image>();
                    _tabImages[cat] = img;

                    var btn = btnGo.GetComponent<Button>();
                    btn.targetGraphic = img;
                    string capturedCat = cat;
                    btn.onClick.AddListener(() => SetCategory(capturedCat));
                    UiFactory.SizeHint(btn, preferredWidth: 190, preferredHeight: 96);

                    var col = UiFactory.VGroup(rt, 2, new RectOffset(10, 10, 10, 10), true, true);
                    UiFactory.Fill(col);
                    var titleText = UiFactory.Text(col, JackpotRun.Data.JackpotCatalog.CategoryTitle(cat), 20,
                        UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleCenter, true);
                    UiFactory.SizeHint(titleText, preferredHeight: 32);
                    var countText = UiFactory.Text(col, CountLabel(cat), 16, UiFactory.Hex("#8B93A7"), TextAnchor.MiddleCenter);
                    UiFactory.SizeHint(countText, preferredHeight: 26);
                }
            }

            private string CountLabel(string cat)
            {
                var list = JackpotRun.Data.JackpotCatalog.ByCategory(cat);
                int total = list?.Count ?? 0;
                if (cat == "char") return $"해금 {_player.chars.Count}/{total}";
                if (cat == "mac") return $"해금 {_player.machines.Count}/{total}";
                if (cat == "dev") return $"해금 {_player.ownedDevices.Count}/{total}";
                return $"{total}종";
            }

            private void UpdateTabHighlight()
            {
                foreach (var kv in _tabImages)
                    kv.Value.color = kv.Key == _cat ? UiFactory.Hex("#232A45") : UiFactory.Hex("#151A2E");
            }

            private void SetCategory(string cat)
            {
                _cat = cat;
                UpdateTabHighlight();
                RenderGrid();
            }

            // ── 카드 그리드 ────────────────────────────────────
            private void BuildGrid(RectTransform root)
            {
                var scroll = UiFactory.Scroll(root, out var content, vertical: true);
                UiFactory.SizeHint(scroll, flexibleHeight: 1);
                UiFactory.Grid(content, new Vector2(500, 300), new Vector2(20, 20), 2);
                content.gameObject.GetComponent<GridLayoutGroup>().padding = new RectOffset(20, 20, 20, 20);
                _gridContent = content;
            }

            private void RenderGrid()
            {
                for (int i = _gridContent.childCount - 1; i >= 0; i--)
                    Object.Destroy(_gridContent.GetChild(i).gameObject);

                var entries = JackpotRun.Data.JackpotCatalog.ByCategory(_cat);
                if (entries == null) return;
                foreach (var e in entries) BuildCard(_gridContent, e);
            }

            private void BuildCard(RectTransform parent, JackpotRun.Data.CatalogEntry e)
            {
                bool lockable = _cat == "char" || _cat == "mac" || _cat == "dev";
                bool unlocked = !lockable || IsUnlocked(e);

                var cardGo = new GameObject("Card_" + e.id, typeof(RectTransform), typeof(Image), typeof(Button));
                var cardRt = (RectTransform)cardGo.transform;
                cardRt.SetParent(parent, false);
                var cardImg = cardGo.GetComponent<Image>();
                cardImg.color = UiFactory.Hex("#1B2138");
                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = cardImg;
                btn.onClick.AddListener(() => DetailPopup.Show(_app.CanvasRoot, e));

                var col = UiFactory.VGroup(cardRt, 6, new RectOffset(14, 14, 12, 12), true, true);
                UiFactory.Fill(col);

                var topRow = UiFactory.HGroup(col, 10, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(topRow, preferredHeight: 76);

                var sprite = JackpotRun.Data.JackpotCatalog.LoadSprite(e);
                if (sprite != null)
                {
                    var img = UiFactory.Image(topRow, sprite, Color.white);
                    UiFactory.SizeHint(img, preferredWidth: 72, preferredHeight: 72);
                }
                else
                {
                    var emojiText = UiFactory.Text(topRow, e.emoji, 52, Color.white, TextAnchor.MiddleCenter);
                    UiFactory.SizeHint(emojiText, preferredWidth: 72, preferredHeight: 72);
                }

                string medal = (_cat == "aug" && TierMark.TryGetValue(e.tier ?? "", out var mk)) ? mk : "";
                var nameText = UiFactory.Text(topRow, $"{medal}{e.nameKo}", 24, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(nameText, flexibleWidth: 1);

                var descText = UiFactory.Text(col, e.descKo, 19, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
                UiFactory.SizeHint(descText, flexibleHeight: 1);

                string sub = BuildSubline(e);
                if (!string.IsNullOrEmpty(sub))
                {
                    var subText = UiFactory.Text(col, sub, 18, UiFactory.Hex("#FFD23F"), TextAnchor.UpperLeft);
                    UiFactory.SizeHint(subText, preferredHeight: 26);
                }

                if (!unlocked) BuildLockOverlay(cardRt, e);
            }

            private bool IsUnlocked(JackpotRun.Data.CatalogEntry e)
            {
                if (_cat == "char") return _player.chars.Contains(e.key);
                if (_cat == "mac") return _player.machines.Contains(e.key);
                if (_cat == "dev") return _player.ownedDevices.Contains(e.id);
                return true;
            }

            private string BuildSubline(JackpotRun.Data.CatalogEntry e)
            {
                if (_cat == "rel") return $"가격 {JackpotRun.Core.NumberFormat.Comma(e.price)}";
                if (_cat == "item") return $"코인 {JackpotRun.Core.NumberFormat.Comma(e.coinCost)}";
                if (_cat == "mac" && e.scoreMod >= 0f)
                    return $"점수보정 ×{JackpotRun.Core.NumberFormat.Fmt(e.scoreMod)}";
                if (_cat == "dev")
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(e.command)) parts.Add($"명령 .{e.command}");
                    parts.Add($"쿨다운 {(e.cooldown == -1 ? "-" : JackpotRun.Core.NumberFormat.Comma(e.cooldown))}");
                    if (e.rare) parts.Add("희귀");
                    return string.Join(" · ", parts);
                }
                return "";
            }

            private void BuildLockOverlay(RectTransform cardRt, JackpotRun.Data.CatalogEntry e)
            {
                var overlay = UiFactory.Panel(cardRt, "LockOverlay", new Color(0.043f, 0.055f, 0.102f, 0.45f));
                UiFactory.Fill(overlay);

                var col = UiFactory.VGroup(overlay, 6, new RectOffset(14, 14, 12, 12), true, true);
                UiFactory.Fill(col);

                var lockText = UiFactory.Text(col, "🔒 잠김", 24, UiFactory.Hex("#E8EAF2"), TextAnchor.MiddleCenter, true);
                UiFactory.SizeHint(lockText, preferredHeight: 36);

                if (e.hasPick && e.pick != null && !string.IsNullOrEmpty(e.pick.unlock))
                {
                    var hintText = UiFactory.Text(col, $"해금: {e.pick.unlock}", 16, UiFactory.Hex("#8B93A7"), TextAnchor.UpperLeft);
                    UiFactory.SizeHint(hintText, flexibleHeight: 1);
                }
            }
        }
    }
}
