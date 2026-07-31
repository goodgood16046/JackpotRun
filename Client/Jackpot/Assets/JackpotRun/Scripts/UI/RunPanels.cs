using System;
using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Data;
using JackpotRun.Engine;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI
{
    /// <summary>
    /// RunScreen 전용 오버레이 빌더 — NodeSelect/PerkOffer(증강·유물)/Shop/PostSpin(만회)/GameOver
    /// 페이즈 패널과 가방·MANIP 칸선택 팝업. 기존 UiFactory/DetailPopup 패턴(스크림+중앙 카드) 그대로.
    /// 모든 Build*는 호출측(RunScreen)이 이미 비워둔 overlay 컨테이너에 채워 넣기만 한다 —
    /// "이벤트 시에만 리빌드" 규칙은 RunScreen이 지킨다(여기서는 매번 새로 그린다는 전제로 단순하게 작성).
    /// </summary>
    public static class RunPanels
    {
        private static readonly Color CardBg = UiFactory.Hex("#1B2138");
        private static readonly Color PanelBg = UiFactory.Hex("#151A2E");
        private static readonly Color Accent = UiFactory.Hex("#FFD23F");
        private static readonly Color Good = UiFactory.Hex("#34D3C0");
        private static readonly Color Bad = UiFactory.Hex("#FF6B6B");
        private static readonly Color Blue = UiFactory.Hex("#5B8CFF");
        private static readonly Color TextMain = UiFactory.Hex("#E8EAF2");
        private static readonly Color TextSub = UiFactory.Hex("#8B93A7");
        private static readonly Color Dark = UiFactory.Hex("#0B0E1A");
        private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.66f);

        // ── S6 설계: 엔진 퍽/아이템 id(무접두) → catalog id(접두). 장치는 이미 "dev_" 접두라 그대로. ──
        public static string CatalogIdOf(PCat cat, string id)
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

        private static readonly Dictionary<Tier, string> TierMedal = new Dictionary<Tier, string>
        {
            { Tier.SILVER, "🥈" }, { Tier.GOLD, "🥇" }, { Tier.PRISM, "🌈" },
        };

        public static string TierLabel(Tier t)
        {
            switch (t)
            {
                case Tier.SILVER: return "실버";
                case Tier.GOLD: return "골드";
                default: return "프리즘";
            }
        }

        // ── 라벨 헬퍼(노트 피드 등에서 공용) ──────────────────────────────
        public static string PerkLabel(string perkId)
        {
            var p = Perks.ById(perkId);
            return p != null ? $"{p.emoji}{p.name}" : (perkId ?? "");
        }

        public static string ItemLabel(string itemId)
        {
            var i = Items.ById(itemId);
            return i != null ? $"{i.emoji}{i.name}" : (itemId ?? "");
        }

        public static string DeviceLabel(string deviceId)
        {
            var d = Devices.ById(deviceId);
            return d != null ? $"{d.emoji}{d.name}" : (deviceId ?? "");
        }

        public static (string emoji, string title, string desc) NodeKindInfo(NodeKind k)
        {
            switch (k)
            {
                case NodeKind.Augment: return ("✨", "증강", "증강 3종 중 1개를 선택합니다.");
                case NodeKind.Relic: return ("🛡️", "유물", "유물 3종 중 1개를 선택합니다.");
                case NodeKind.Shop: return ("🛒", "상점", "코인으로 증강·유물·아이템을 구매합니다.");
                case NodeKind.Rest: return ("☕", "휴식", "코인 +8을 즉시 받습니다.");
                case NodeKind.Gamble: return ("🎲", "도박", "보유 코인 전부를 걸고 50% 확률로 2배 또는 전부를 잃습니다.");
                case NodeKind.Event: return ("❓", "이벤트", "무작위 보상 이벤트가 발생합니다.");
                case NodeKind.Curse: return ("🌑", "저주", "저주 1개를 받는 대신 코인 +15를 받습니다.");
                case NodeKind.Risk: return ("⚠️", "위험", "프리즘/골드 증강과 저주를 동시에 받습니다.");
                default: return ("❔", k.ToString(), "");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 공용 — 전체화면 스크림 + 중앙 카드(DetailPopup과 동일 패턴).
        // ══════════════════════════════════════════════════════════════════
        private static RectTransform BuildScrim(RectTransform parent, string name, bool dismissible, Action onDismiss)
        {
            var scrim = UiFactory.Panel(parent, name, ScrimColor);
            UiFactory.Fill(scrim);
            if (dismissible)
            {
                var btn = scrim.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    onDismiss?.Invoke();
                    UnityEngine.Object.Destroy(scrim.gameObject);
                });
            }
            return scrim;
        }

        private static RectTransform BuildCard(RectTransform scrim, float width, float height)
        {
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.SetParent(scrim, false);
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(width, height);
            cardGo.GetComponent<Image>().color = PanelBg;
            // 카드 위 클릭이 배경 Button까지 전파돼 팝업이 닫히지 않도록 이벤트 소비(DetailPopup과 동일).
            var blocker = cardGo.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            return cardRt;
        }

        private static ScrollRect BuildScrollCard(RectTransform scrim, float width, float height, out RectTransform content)
        {
            var card = BuildCard(scrim, width, height);
            var col = UiFactory.VGroup(card, 0, new RectOffset(0, 0, 0, 0), true, true);
            UiFactory.Fill(col);
            var scroll = UiFactory.Scroll(col, out content, vertical: true);
            UiFactory.SizeHint(scroll, flexibleHeight: 1);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 24, 20);
            vlg.spacing = 14;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return scroll;
        }

        // ══════════════════════════════════════════════════════════════════
        // NodeSelect — 클리어 직후 3택 카드.
        // ══════════════════════════════════════════════════════════════════
        public static void BuildNodeSelect(RectTransform overlay, RunState run, Action<int> onChoose)
        {
            var scrim = BuildScrim(overlay, "NodeSelect", false, null);
            BuildScrollCard(scrim, 940, 1300, out var content);

            UiFactory.Text(content, "다음 노드를 선택하세요", 32, TextMain, TextAnchor.MiddleCenter, true);

            for (int i = 0; i < run.NodeOptions.Count; i++)
            {
                int idx = i;
                var (emoji, title, desc) = NodeKindInfo(run.NodeOptions[i]);

                var cardRt = UiFactory.Panel(content, "NodeCard_" + idx, CardBg);
                UiFactory.SizeHint(cardRt, preferredHeight: 210);
                var cardBtn = cardRt.gameObject.AddComponent<Button>();
                cardBtn.targetGraphic = cardRt.GetComponent<Image>();
                cardBtn.onClick.AddListener(() => onChoose(idx));

                var inner = UiFactory.VGroup(cardRt, 6, new RectOffset(22, 22, 16, 16), true, true);
                UiFactory.Fill(inner);
                var head = UiFactory.Text(inner, $"{emoji} {title}", 28, Accent, TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(head, preferredHeight: 40);
                var body = UiFactory.Text(inner, desc, 20, TextMain, TextAnchor.UpperLeft);
                UiFactory.SizeHint(body, flexibleHeight: 1);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // PerkOffer — 증강/유물 3카드(스프라이트+이름+desc, 배지) + 보류/재추첨.
        // ══════════════════════════════════════════════════════════════════
        public static void BuildPerkOffer(RectTransform overlay, RunState run, RunEvent offerEvent,
            Action<int> onPick, Action<int> onHold, Action onRetake)
        {
            var scrim = BuildScrim(overlay, "PerkOffer", false, null);
            BuildScrollCard(scrim, 980, 1560, out var content);

            bool isAugment = run.Phase == RunPhase.EventAugment;
            UiFactory.Text(content, isAugment ? "✨ 증강 선택" : "🛡️ 유물 선택", 32, TextMain, TextAnchor.MiddleCenter, true);

            var banners = new List<string>();
            if (offerEvent != null)
            {
                if (offerEvent.offerBossPrism) banners.Add("🌈 보스 클리어! 프리즘 확정");
                if (offerEvent.offerTierBumped) banners.Add("🍀 행운! 등급업");
                if (offerEvent.offerHeldIncluded) banners.Add("🗂️ 보류 후보 포함");
                if (offerEvent.offerRetake) banners.Add("🔁 재추첨 결과");
            }
            if (banners.Count > 0)
                UiFactory.Text(content, string.Join(" · ", banners), 20, Accent, TextAnchor.MiddleCenter, true);

            bool canHold = onHold != null && isAugment && Shop.HasDevice(run, "dev_holdfile") && string.IsNullOrEmpty(run.HeldAug);

            var ids = run.PerkOfferIds;
            for (int i = 0; i < ids.Count; i++)
            {
                int idx = i;
                var perk = Perks.ById(ids[i]);
                if (perk == null) continue;
                bool heldBadge = offerEvent != null && offerEvent.offerHeldIncluded && idx == 0;
                bool synergyBadge = offerEvent != null && offerEvent.offerSynergyPerkId == perk.id;
                BuildPerkCard(content, perk, heldBadge, synergyBadge, idx, onPick, canHold ? onHold : null);
            }

            if (onRetake != null && Shop.HasDevice(run, "dev_retake") && !run.UsedCmds.Contains("RETAKE"))
            {
                var retakeBtn = UiFactory.Button(content, $"🔁 재추첨 ({Formulas.RETAKE_COIN_COST}🪙)", new Vector2(0, 78),
                    Blue, Dark, () => onRetake());
                UiFactory.SizeHint(retakeBtn, preferredHeight: 78);
            }
        }

        private static void BuildPerkCard(RectTransform parent, Perk perk, bool heldBadge, bool synergyBadge,
            int idx, Action<int> onPick, Action<int> onHold)
        {
            var cardRt = UiFactory.Panel(parent, "PerkCard_" + perk.id, CardBg);
            UiFactory.SizeHint(cardRt, preferredHeight: onHold != null ? 320 : 270);
            var col = UiFactory.VGroup(cardRt, 8, new RectOffset(20, 20, 16, 16), true, true);
            UiFactory.Fill(col);

            var topRow = UiFactory.HGroup(col, 14, new RectOffset(0, 0, 0, 0), true, true);
            UiFactory.SizeHint(topRow, preferredHeight: 92);

            var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get(CatalogIdOf(perk.cat, perk.id)));
            if (sprite != null)
            {
                var img = UiFactory.Image(topRow, sprite, Color.white);
                UiFactory.SizeHint(img, preferredWidth: 80, preferredHeight: 80);
            }
            else
            {
                var emojiText = UiFactory.Text(topRow, perk.emoji, 52, Color.white, TextAnchor.MiddleCenter);
                UiFactory.SizeHint(emojiText, preferredWidth: 80, preferredHeight: 80);
            }

            var nameCol = UiFactory.VGroup(topRow, 2, new RectOffset(0, 0, 0, 0), true, true);
            UiFactory.SizeHint(nameCol, flexibleWidth: 1);
            string medal = TierMedal.TryGetValue(perk.tier, out var mk) ? mk : "";
            string badges = (heldBadge ? " 🗂️보류" : "") + (synergyBadge ? " 🧩시너지" : "");
            var nameText = UiFactory.Text(nameCol, $"{medal}{perk.name}{badges}", 25, TextMain, TextAnchor.MiddleLeft, true);
            UiFactory.SizeHint(nameText, preferredHeight: 36);
            var tierText = UiFactory.Text(nameCol, TierLabel(perk.tier), 17, TextSub, TextAnchor.MiddleLeft);
            UiFactory.SizeHint(tierText, preferredHeight: 24);

            var descText = UiFactory.Text(col, perk.desc, 20, TextMain, TextAnchor.UpperLeft);
            UiFactory.SizeHint(descText, flexibleHeight: 1);

            var btnRow = UiFactory.HGroup(col, 10, new RectOffset(0, 0, 0, 0), true, true);
            UiFactory.SizeHint(btnRow, preferredHeight: 68);
            var pickBtn = UiFactory.Button(btnRow, "선택", new Vector2(0, 68), Accent, Dark, () => onPick(idx));
            UiFactory.SizeHint(pickBtn, flexibleWidth: 1, preferredHeight: 68);
            if (onHold != null)
            {
                var holdBtn = UiFactory.Button(btnRow, "🗂️보류", new Vector2(0, 68), UiFactory.Hex("#2A3048"), TextMain, () => onHold(idx));
                UiFactory.SizeHint(holdBtn, flexibleWidth: 1, preferredHeight: 68);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Shop — 오퍼 목록 + 가격 + 리롤(6코인) + 나가기.
        // ══════════════════════════════════════════════════════════════════
        public static void BuildShop(RectTransform overlay, RunState run, Action<int> onBuy, Action onReroll, Action onLeave)
        {
            var scrim = BuildScrim(overlay, "Shop", false, null);
            var card = BuildCard(scrim, 980, 1600);
            var outerCol = UiFactory.VGroup(card, 10, new RectOffset(0, 0, 20, 20), true, true);
            UiFactory.Fill(outerCol);

            var title = UiFactory.Text(outerCol, $"🛒 상점 · 🪙{NumberFormat.Comma(run.Coins)}", 30, TextMain, TextAnchor.MiddleCenter, true);
            UiFactory.SizeHint(title, preferredHeight: 56);

            var scroll = UiFactory.Scroll(outerCol, out var content, vertical: true);
            UiFactory.SizeHint(scroll, flexibleHeight: 1);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 8, 8);
            vlg.spacing = 12;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (run.ShopOffer.Count == 0)
                UiFactory.Text(content, "오퍼가 없습니다.", 20, TextSub, TextAnchor.MiddleCenter);

            for (int i = 0; i < run.ShopOffer.Count; i++)
            {
                int idx = i;
                BuildShopRow(content, run.ShopOffer[i], run.Coins, () => onBuy(idx));
            }

            var btnRow = UiFactory.HGroup(outerCol, 14, new RectOffset(24, 24, 4, 4), true, true);
            UiFactory.SizeHint(btnRow, preferredHeight: 90);
            var rerollBtn = UiFactory.Button(btnRow, $"🔁 리롤 ({Shop.RerollCost}🪙)", new Vector2(0, 90), Blue, Dark, () => onReroll());
            UiFactory.SizeHint(rerollBtn, flexibleWidth: 1, preferredHeight: 90);
            var leaveBtn = UiFactory.Button(btnRow, "나가기", new Vector2(0, 90), UiFactory.Hex("#2A3048"), TextMain, () => onLeave());
            UiFactory.SizeHint(leaveBtn, flexibleWidth: 1, preferredHeight: 90);
        }

        private static void BuildShopRow(RectTransform parent, ShopEntry entry, long coins, Action onBuy)
        {
            var rowRt = UiFactory.Panel(parent, "ShopEntry_" + entry.id, CardBg);
            UiFactory.SizeHint(rowRt, preferredHeight: 140);
            var row = UiFactory.HGroup(rowRt, 14, new RectOffset(16, 16, 10, 10), true, true);
            UiFactory.Fill(row);

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

            if (sprite != null)
            {
                var img = UiFactory.Image(row, sprite, Color.white);
                UiFactory.SizeHint(img, preferredWidth: 88, preferredHeight: 88);
            }
            else
            {
                var emojiText = UiFactory.Text(row, emoji, 48, Color.white, TextAnchor.MiddleCenter);
                UiFactory.SizeHint(emojiText, preferredWidth: 88, preferredHeight: 88);
            }

            var infoCol = UiFactory.VGroup(row, 2, new RectOffset(0, 0, 0, 0), true, true);
            UiFactory.SizeHint(infoCol, flexibleWidth: 1);
            var nameText = UiFactory.Text(infoCol, $"{KindLabel(entry.kind)} {name}", 23, TextMain, TextAnchor.MiddleLeft, true);
            UiFactory.SizeHint(nameText, preferredHeight: 32);
            var descText = UiFactory.Text(infoCol, desc, 16, TextSub, TextAnchor.UpperLeft);
            UiFactory.SizeHint(descText, flexibleHeight: 1);

            var affordable = coins >= entry.price;
            var buyBtn = UiFactory.Button(row, $"{NumberFormat.Comma(entry.price)}🪙", new Vector2(150, 84),
                affordable ? Accent : UiFactory.Hex("#2A3048"), affordable ? Dark : TextSub, () => onBuy());
            UiFactory.SizeHint(buyBtn, preferredWidth: 150, preferredHeight: 84);
        }

        private static string KindLabel(char kind) => kind == 'A' ? "✨" : kind == 'R' ? "🛡️" : "🎁";

        // ══════════════════════════════════════════════════════════════════
        // PostSpin — 만회 버튼(GREROL/장치) 또는 포기.
        // ══════════════════════════════════════════════════════════════════
        public static void BuildPostSpin(RectTransform overlay, RunState run, FailureOutcome failure,
            Action<DeviceDef> onManip, Action onGambler, Action onGiveUp)
        {
            var scrim = BuildScrim(overlay, "PostSpin", false, null);
            var card = BuildCard(scrim, 900, 900);
            var col = UiFactory.VGroup(card, 18, new RectOffset(28, 28, 26, 26), true, true);
            UiFactory.Fill(col);

            var head = UiFactory.Text(col, "💥 클리어 실패", 32, Bad, TextAnchor.MiddleCenter, true);
            UiFactory.SizeHint(head, preferredHeight: 46);

            long deficit = failure?.deficitAtFailure ?? 0;
            var sub = UiFactory.Text(col, $"부족 EXP {NumberFormat.Comma(deficit)} — 만회 수단을 사용하거나 포기하세요.",
                20, TextMain, TextAnchor.MiddleCenter);
            UiFactory.SizeHint(sub, preferredHeight: 56);

            var hints = failure != null ? failure.manipHints : null;
            foreach (var hint in hints ?? (IReadOnlyList<string>)Array.Empty<string>())
            {
                if (hint == "GAMBLER_REROLL")
                {
                    var btn = UiFactory.Button(col, "🎲 도박꾼 무료 재굴림", new Vector2(0, 92), Good, Dark, () => onGambler());
                    UiFactory.SizeHint(btn, preferredHeight: 92);
                }
                else if (hint.StartsWith("DEVICE:"))
                {
                    string devId = hint.Substring("DEVICE:".Length);
                    var dev = Devices.ById(devId);
                    if (dev != null)
                    {
                        var btn = UiFactory.Button(col, $"{dev.emoji} {dev.name}", new Vector2(0, 92), Blue, Dark, () => onManip(dev));
                        UiFactory.SizeHint(btn, preferredHeight: 92);
                    }
                }
            }

            var spacer = UiFactory.Panel(col, "Spacer", new Color(0, 0, 0, 0));
            UiFactory.SizeHint(spacer, flexibleHeight: 1);

            var giveUpBtn = UiFactory.Button(col, "포기", new Vector2(0, 84), UiFactory.Hex("#2A3048"), TextMain, () => onGiveUp());
            UiFactory.SizeHint(giveUpBtn, preferredHeight: 84);
        }

        // ══════════════════════════════════════════════════════════════════
        // GameOver — 최종 점수·등급·기록 갱신·새 업적 목록·[메뉴로].
        // ══════════════════════════════════════════════════════════════════
        public static void BuildGameOver(RectTransform overlay, GameSession session, FailureOutcome failure, Action onMenu)
        {
            var scrim = BuildScrim(overlay, "GameOver", false, null);
            BuildScrollCard(scrim, 980, 1560, out var content);

            var run = session.State;
            var profile = session.Profile;
            long finalScore = failure?.finalScore ?? 0;
            var (titleEmoji, titleText) = Formulas.ScoreTitle(finalScore);

            UiFactory.Text(content, "🏁 게임 오버", 34, Bad, TextAnchor.MiddleCenter, true);
            UiFactory.Text(content, $"{titleEmoji} {titleText}", 26, Accent, TextAnchor.MiddleCenter, true);
            UiFactory.Text(content, $"최종 점수 {NumberFormat.Comma(finalScore)}", 30, TextMain, TextAnchor.MiddleCenter, true);
            UiFactory.Text(content, $"도달 스테이지 {run.Stage}", 22, TextSub, TextAnchor.MiddleCenter);
            UiFactory.Text(content,
                $"최고점수 {NumberFormat.Comma(profile.BestScore)} · 최고 스테이지 {profile.BestStage} · 통산 런 {profile.Runs}",
                20, TextSub, TextAnchor.MiddleCenter);

            var newAch = session.LastNewAchievements;
            if (newAch != null && newAch.Count > 0)
            {
                UiFactory.Text(content, $"🏅 신규 업적 {newAch.Count}개", 24, Good, TextAnchor.MiddleLeft, true);
                for (int i = 0; i < newAch.Count; i++)
                {
                    var a = newAch[i];
                    UiFactory.Text(content, $"{a.emoji} {a.name} — {a.desc}", 18, TextMain, TextAnchor.UpperLeft);
                }
            }

            UiFactory.Text(content, $"업적 {profile.AchievedIds.Count}/{Achievements.Count}", 20, TextSub, TextAnchor.MiddleCenter);

            var menuBtn = UiFactory.Button(content, "메뉴로", new Vector2(0, 90), Accent, Dark, () => onMenu());
            UiFactory.SizeHint(menuBtn, preferredHeight: 90);
        }

        // ══════════════════════════════════════════════════════════════════
        // 가방 팝업 — 아이템 사용.
        // ══════════════════════════════════════════════════════════════════
        public static void ShowBag(RectTransform overlay, RunState run, Action<string> onUse)
        {
            var scrim = BuildScrim(overlay, "Bag", true, null);
            var card = BuildCard(scrim, 900, 1200);
            var outerCol = UiFactory.VGroup(card, 10, new RectOffset(0, 0, 20, 20), true, true);
            UiFactory.Fill(outerCol);

            var title = UiFactory.Text(outerCol, $"🎒 가방 ({run.Items.Count}/{ItemUse.ItemSlots})", 28, TextMain, TextAnchor.MiddleCenter, true);
            UiFactory.SizeHint(title, preferredHeight: 54);

            var scroll = UiFactory.Scroll(outerCol, out var content, vertical: true);
            UiFactory.SizeHint(scroll, flexibleHeight: 1);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 8, 8);
            vlg.spacing = 12;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (run.Items.Count == 0)
            {
                UiFactory.Text(content, "가방이 비어 있습니다.", 20, TextSub, TextAnchor.MiddleCenter);
            }
            for (int i = 0; i < run.Items.Count; i++)
            {
                var item = Items.ById(run.Items[i]);
                if (item == null) continue;

                var rowRt = UiFactory.Panel(content, "ItemRow_" + i, CardBg);
                UiFactory.SizeHint(rowRt, preferredHeight: 130);
                var row = UiFactory.HGroup(rowRt, 12, new RectOffset(14, 14, 8, 8), true, true);
                UiFactory.Fill(row);

                var sprite = JackpotCatalog.LoadSprite(JackpotCatalog.Get(CatalogIdOf(PCat.ITEM, item.id)));
                if (sprite != null)
                {
                    var img = UiFactory.Image(row, sprite, Color.white);
                    UiFactory.SizeHint(img, preferredWidth: 72, preferredHeight: 72);
                }
                else
                {
                    var e = UiFactory.Text(row, item.emoji, 44, Color.white, TextAnchor.MiddleCenter);
                    UiFactory.SizeHint(e, preferredWidth: 72, preferredHeight: 72);
                }

                var infoCol = UiFactory.VGroup(row, 2, new RectOffset(0, 0, 0, 0), true, true);
                UiFactory.SizeHint(infoCol, flexibleWidth: 1);
                var nameText = UiFactory.Text(infoCol, item.name, 22, TextMain, TextAnchor.MiddleLeft, true);
                UiFactory.SizeHint(nameText, preferredHeight: 30);
                var descText = UiFactory.Text(infoCol, item.desc, 16, TextSub, TextAnchor.UpperLeft);
                UiFactory.SizeHint(descText, flexibleHeight: 1);

                string itemId = run.Items[i];
                var useBtn = UiFactory.Button(row, "사용", new Vector2(120, 76), Accent, Dark, () =>
                {
                    onUse(itemId);
                    UnityEngine.Object.Destroy(scrim.gameObject);
                });
                UiFactory.SizeHint(useBtn, preferredWidth: 120, preferredHeight: 76);
            }

            var closeBtn = UiFactory.Button(outerCol, "닫기", new Vector2(0, 80), UiFactory.Hex("#2A3048"), TextMain,
                () => UnityEngine.Object.Destroy(scrim.gameObject));
            UiFactory.SizeHint(closeBtn, preferredHeight: 80);
        }

        // ══════════════════════════════════════════════════════════════════
        // MANIP 칸 선택 팝업 — 1~릴수 버튼(dev_reroll은 인자 불필요, 팝업 없이 즉시 확정).
        // ══════════════════════════════════════════════════════════════════
        public static void ShowManipPicker(RectTransform overlay, RunState run, DeviceDef dev, Action<string, int?> onConfirm)
        {
            if (dev.id != "dev_pin" && dev.id != "dev_copy" && dev.id != "dev_swap")
            {
                onConfirm(dev.id, null);
                return;
            }

            int cellCount = run.LastCells.Count;
            var scrim = BuildScrim(overlay, "ManipPicker", true, null);
            var card = BuildCard(scrim, 860, 680);
            var col = UiFactory.VGroup(card, 16, new RectOffset(28, 28, 26, 26), true, true);
            UiFactory.Fill(col);

            var head = UiFactory.Text(col, $"{dev.emoji} {dev.name} — 칸 선택", 27, TextMain, TextAnchor.MiddleCenter, true);
            UiFactory.SizeHint(head, preferredHeight: 40);
            var sub = UiFactory.Text(col, dev.desc, 18, TextSub, TextAnchor.UpperLeft);
            UiFactory.SizeHint(sub, preferredHeight: 76);

            var grid = UiFactory.HGroup(col, 10, new RectOffset(0, 0, 10, 10), true, true);
            UiFactory.SizeHint(grid, preferredHeight: 100);
            for (int i = 1; i <= cellCount; i++)
            {
                int n = i;
                var btn = UiFactory.Button(grid, n.ToString(), new Vector2(0, 96), Accent, Dark, () =>
                {
                    onConfirm(dev.id, n);
                    UnityEngine.Object.Destroy(scrim.gameObject);
                });
                UiFactory.SizeHint(btn, flexibleWidth: 1, preferredHeight: 96);
            }

            var spacer = UiFactory.Panel(col, "Spacer", new Color(0, 0, 0, 0));
            UiFactory.SizeHint(spacer, flexibleHeight: 1);

            var cancelBtn = UiFactory.Button(col, "취소", new Vector2(0, 76), UiFactory.Hex("#2A3048"), TextMain,
                () => UnityEngine.Object.Destroy(scrim.gameObject));
            UiFactory.SizeHint(cancelBtn, preferredHeight: 76);
        }
    }
}
