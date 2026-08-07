using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 상점 6칸 오퍼 1건 — "A:<id>:<price>"/"R:<id>:<price>"/"I:<id>:<price>"(Kotlin pendingOptions CSV
    // 직렬화, 02_service.md §4-A)를 타입 있는 값으로 재설계(설계 원칙 1 — CSV 이식 금지). kind: 'A'=증강,
    // 'R'=유물, 'I'=아이템.
    public sealed class ShopEntry
    {
        public char kind;
        public string id;
        public int price;
    }

    // 상점(EVENT_SHOP) — 02_service.md §4 그대로(판매 기능 없음, §4-E). 오퍼 생성/가격/리롤/구매 처리.
    // Kotlin SlotV2Engine의 pickAugments/pickRelics/pickPerksByTier/gatedPool/unlockedPerks/perkGate/
    // perkUnlocked(L825-1700 부근)와 SlotV2Service의 freshShopOffer/handleShop(L1248-1419, L1629-1681)을
    // 전사한다. "정답 원본" 지시(ENGINE_PORT_DESIGN.md 작업 지시)에 따라 이 함수들을 재정의하지 않고
    // 그대로 옮겼다 — NodeEvents.cs(증강/유물 노드 티어 픽)도 이 파일의 해금 게이트 헬퍼를 그대로 재사용한다.
    public static class Shop
    {
        // 웹 파리티 P3-4 Opus 2차검수 필수③(WEB_PARITY_DESIGN.md §2) — 이전엔 정액(Kotlin SHOP_REROLL
        // L1400)이었지만 웹 game.js:2363 `shopReroll()`은 `max(2, 6 + shopRerollDelta)`로 vip 등
        // 증강에 반응한다. 기본값은 그대로 6(BaseRerollCost)이고, 실사용은 RerollCostFor(run)를 거친다.
        public const int BaseRerollCost = 6;
        private const double EventPrismRate = 0.12;       // EVENT_PRISM_RATE(Kotlin L1249)

        private static int AugPrice(Tier t) => t switch
        {
            Tier.SILVER => 14,
            Tier.GOLD => 24,
            _ => 36, // PRISM
        };

        // ══════════════════════════════════════════════════════════════════
        // 웹 파리티 P3-4 Opus 2차검수 필수③ — 상점 5필드(shopPriceMul/itemPriceMul/itemCapBonus/
        // shopSlotBonus/shopRerollDelta) 실제 배선. 이 5필드는 Kotlin 원본에 없는 웹 전용 신규
        // 기능(discount/thrifty/item_bag/vip 증강)이라 이 파일이 직접 mods를 계산한다. Perks+PhasePerks
        // 결합 + ApplyItemMods(PhaseItems)만 적용하는 것은 ItemUse.InstantQuota/GameSession.
        // PreviewQuotaSpins와 동일한 "스핀 밖 스냅샷" 근사 패턴(ApplyPassiveDevice 미적용) — 현재
        // 어떤 장치도 이 5필드를 건드리지 않아 실질 차이는 없다(신규 장치 추가 시 재검토 필요).
        // ══════════════════════════════════════════════════════════════════
        internal static Mods ShopMods(RunState run)
        {
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            return ModsBuilder.ApplyItemMods(
                ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                run.PhaseItems);
        }

        // 웹 game.js:2327 `pm = max(0.4, ascMods(r.asc).shopPriceMul * (sm.shopPriceMul||1) * receiptMul)`
        // — 승천(ascMods)·심화 영수증(receiptMul)은 P6/P7 미구현이라 생략(각각 곱연산 항이라 나중에
        // 그대로 끼워 넣을 수 있다, 삭제 아님·주석 표기).
        private static double ShopPriceMul(Mods mods) => Math.Max(0.4, mods.shopPriceMul);

        // 웹 game.js:2328 `itemPm = max(0.4, pm * (sm.itemPriceMul||1))`.
        private static double ItemPriceMul(Mods mods) => Math.Max(0.4, ShopPriceMul(mods) * mods.itemPriceMul);

        // 웹 game.js:2330 `slot = max(0, min(3, (sm.shopSlotBonus||0) + cartBonus))` — cartBonus(🛒장바구니,
        // 심화 전용)는 P7 미구현이라 생략.
        private static int ShopSlotBonus(Mods mods) => Math.Max(0, Math.Min(3, mods.shopSlotBonus));

        // 웹 game.js:2363 `cost = max(2, 6 + shopRerollDelta)`.
        public static int RerollCostFor(RunState run) => Math.Max(2, BaseRerollCost + ShopMods(run).shopRerollDelta);

        // JS Math.round(양수)는 항상 반올림(0.5는 위로) — C# 기본 Math.Round(은행원 반올림)와 달라
        // MidpointRounding.AwayFromZero로 맞춘다(가격은 항상 양수라 이 옵션이 JS와 동치).
        private static int RoundPrice(double v) => Math.Max(1, (int)Math.Round(v, MidpointRounding.AwayFromZero));

        // ══════════════════════════════════════════════════════════════════
        // 해금 게이트 — 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #13, §2 "퍽 레벨 해금") 전면 개편.
        // 기존 Kotlin 전공연구(Schools.SchoolReq/SchoolResearch)·AccountLevel·StatReq AND 게이트는
        // 폐기했다 — 웹 engine.js의 실사용 오퍼 함수(pickPerksByTier, engine.js:1213-1241)는 그런
        // "해금" 개념 자체가 없다(PERK_FAMILY 랭크 순차 게이팅만 있고, 이는 "한 오퍼 안에서 같은
        // 계열이 겹치지 않게 하는" 표시 순서 규칙이라 Shop.PickPerksByTier의 기존 별도 알고리즘과
        // 무관 — 이번 슬라이스는 "해금 여부" 축만 다룬다). 대신 웹 `_augPool`/`_relicPool`
        // (game.js:234-235 `!a.unlockLevel || lvl >= a.unlockLevel`)과 동일하게 "unlockLevel이 없으면
        // 항상 개방, 있으면 PlayerLevel 게이트만" 규칙으로 단순화한다 — 대상은 신규 8종(증강4·유물4)
        // 뿐이고 나머지 154종은 전면 개방이다. Schools.cs 자체는 삭제하지 않고 게이트 연결만 끊었다
        // (§2 결정 로그 "삭제 범위가 크면 파일 정리는 보류" 지시).
        //
        // playerLevel 값은 stat["playerLevel"] 키로 읽는다 — GameSession이 런 시작 시
        // Profile.SetStat("playerLevel", Profile.PlayerLevel)로 최신값을 스냅샷해 두므로(달성 업적
        // lv20/lv40용 "1런 지연" 스냅샷과는 별개 타이밍, GameSession.cs 주석 참조) 런 도중 게이트
        // 판정에 항상 현재 레벨이 반영된다.
        // ══════════════════════════════════════════════════════════════════
        internal static bool PerkUnlocked(Perk p, IReadOnlyDictionary<string, long> stat)
        {
            if (p == null) return false;
            if (p.unlockLevel <= 0) return true;
            long lvl = 1L;
            if (stat != null) stat.TryGetValue("playerLevel", out lvl);
            if (lvl <= 0) lvl = 1L;
            return lvl >= p.unlockLevel;
        }

        internal static List<Perk> UnlockedPerks(IReadOnlyList<Perk> pool, IReadOnlyDictionary<string, long> stat)
        {
            var list = new List<Perk>();
            for (int i = 0; i < pool.Count; i++) if (PerkUnlocked(pool[i], stat)) list.Add(pool[i]);
            return list;
        }

        // gatedPool — unlockLevel 미해금만 제외(웹 파리티 P3-4). 8종 외 154종은 항상 통과하므로
        // "전부 잠김" 데드엔드는 이제 도달 불가하지만, stat 미전달(방어적 기본값) 시엔 원본 그대로
        // 반환하는 기존 관례를 유지한다.
        internal static List<Perk> GatedPool(IReadOnlyList<Perk> pool, IReadOnlyDictionary<string, long> stat)
        {
            if (stat == null || stat.Count == 0) return new List<Perk>(pool);
            return UnlockedPerks(pool, stat);
        }

        // ── favoredSymbol / majorFavoredCat (Kotlin L1611-1616, Service L119-123) ──
        private static readonly string[] FavoredEmojis = { "🍒", "📘", "⭐", "💎", "👑", "☠" };

        internal static string FavoredSymbol(IEnumerable<string> held)
        {
            string best = null;
            int bestCount = -1;
            foreach (var e in FavoredEmojis)
            {
                int c = 0;
                foreach (var id in held)
                {
                    var p = Perks.ById(id);
                    if (p != null && !string.IsNullOrEmpty(p.desc) && p.desc.Contains(e)) c++;
                }
                if (c > bestCount) { bestCount = c; best = e; }
            }
            return bestCount > 0 ? best : null;
        }

        internal static bool HasDevice(RunState run, string id) => run.Device == id || run.Device2 == id;

        internal static string MajorFavoredCat(RunState run) =>
            HasDevice(run, "dev_major") ? FavoredSymbol(run.Perks) : null;

        // ── 스테이지 진행도 티어 가중 (pickAugments 전용, Kotlin L1562-1573) ──
        private static (double s, double g, double p) TierWeights(int stage)
        {
            if (stage <= 3) return (78.0, 22.0, 0.0);
            if (stage <= 6) return (50.0, 42.0, 8.0);
            if (stage <= 9) return (35.0, 50.0, 15.0);
            return (22.0, 53.0, 25.0);
        }

        private static Tier RollTier(Rng rng, int stage)
        {
            var (s, g, p) = TierWeights(stage);
            double r = rng.NextDouble() * (s + g + p); // RNG 호출 1회, 매번 소비
            if (r < s) return Tier.SILVER;
            if (r < s + g) return Tier.GOLD;
            return Tier.PRISM;
        }

        // ── pickAugments / pickRelics / pickItems (Kotlin L1588-1604, L1004) ──
        internal static List<Perk> PickAugments(Rng rng, int stage, IReadOnlyCollection<string> held, int n, IReadOnlyDictionary<string, long> stat)
        {
            var src = GatedPool(Perks.Augments, stat);
            var used = new HashSet<string>(held);
            var outp = new List<Perk>();
            int guard = 0;
            while (outp.Count < n && guard++ < 60)
            {
                var tier = RollTier(rng, stage);
                var byTier = src.Where(p => p.tier == tier && !used.Contains(p.id)).ToList();
                var pick = rng.PickOrDefault(byTier);
                if (pick == null)
                {
                    var any = src.Where(p => !used.Contains(p.id)).ToList();
                    pick = rng.PickOrDefault(any);
                    if (pick == null) break;
                }
                outp.Add(pick);
                used.Add(pick.id);
            }
            return outp;
        }

        internal static List<Perk> PickRelics(Rng rng, IReadOnlyCollection<string> held, int n, IReadOnlyDictionary<string, long> stat)
        {
            var pool = GatedPool(Perks.Relics, stat).Where(p => !held.Contains(p.id)).ToList();
            rng.Shuffle(pool);
            return pool.Take(n).ToList();
        }

        internal static List<ItemDef> PickItems(Rng rng, int n)
        {
            var pool = new List<ItemDef>(Items.All);
            rng.Shuffle(pool);
            return pool.Take(n).ToList();
        }

        // ══════════════════════════════════════════════════════════════════
        // pickPerksByTier (Kotlin L1654-1700) — 티어 통일 3택. 노드(NodeEvents.cs 증강/유물 오퍼)와 상점
        // 둘 다 원본은 이 함수를 공유하지 않지만("상점=pickAugments/pickRelics", "노드=pickPerksByTier"),
        // 이 파일이 두 함수 모두를 정답 원본 그대로 보관하고 NodeEvents.cs가 이 함수를 호출한다.
        // ⚠️ dev_syllabus 정보성 힌트(tierOddsHint, L1318-1324)는 파워에 영향 없는 정보 텍스트라 이번
        // 슬라이스 범위에서 제외한다(S6 UI로 이관, Fable 승인 2026-07-31). 세트 시너지 5% off-tier 주입은
        // 실제 오퍼 분포를 바꾸는 게임플레이 요소라 이식했다 — pickPerksByTier 자체가 아니라 Kotlin
        // offerPerks(Service.kt L1281-1295)가 pickPerksByTier 호출 *이후*에 적용하는 별도 단계라, 이
        // 함수 밖(NodeEvents.OfferPerks)에서 동일한 위치에 적용한다. SetSynergyAug가 그 이식분.
        // dev_major favoredCat 편향은 포함(파워/분포에 실질 영향 있는 부분이라 원본 함수 시그니처에
        // 이미 포함돼 있어 누락 시 픽 분포가 달라짐).
        // ══════════════════════════════════════════════════════════════════
        internal static List<Perk> PickPerksByTier(
            Rng rng, IReadOnlyList<Perk> rawPool, int stage, IReadOnlyCollection<string> held, bool forceRare,
            string favoredCat, IReadOnlyDictionary<string, long> stat, bool bossClear, Tier? forceTier)
        {
            var pool = GatedPool(rawPool, stat);
            var avail = pool.Where(p => !held.Contains(p.id)).ToList();
            if (avail.Count == 0) return new List<Perk>();

            int Cnt(Tier t) => avail.Count(p => p.tier == t);

            Tier tier;
            if (forceTier.HasValue)
            {
                tier = forceTier.Value;
            }
            else if (bossClear)
            {
                tier = Tier.PRISM;
            }
            else
            {
                int silverW = forceRare ? 0 : Math.Max(12 - stage, 2);
                int goldW = 4 + stage * 2;
                var weights = new List<(Tier t, int w)>
                {
                    (Tier.SILVER, Cnt(Tier.SILVER) > 0 ? silverW : 0),
                    (Tier.GOLD, Cnt(Tier.GOLD) > 0 ? goldW : 0),
                };
                int total = weights.Sum(w => w.w);
                if (total <= 0)
                {
                    var nonPrism = avail.FirstOrDefault(p => p.tier != Tier.PRISM);
                    tier = nonPrism != null ? nonPrism.tier : rng.Pick(avail).tier;
                }
                else
                {
                    int x = rng.Next(total);
                    tier = weights.First(w => w.w > 0).t;
                    foreach (var (t, w) in weights)
                    {
                        if (w <= 0) continue;
                        if (x < w) { tier = t; break; }
                        x -= w;
                    }
                }
            }

            var used = new HashSet<string>(held);
            var outp = new List<Perk>();
            void Take(Perk p) { if (p != null) { outp.Add(p); used.Add(p.id); } }

            var fav = FavoredSymbol(held);
            List<Perk> tierPool;
            if (tier == Tier.PRISM)
            {
                var g = pool.Where(p => p.tier == Tier.PRISM).ToList();
                tierPool = g.Count > 0 ? g : rawPool.Where(p => p.tier == Tier.PRISM).ToList();
            }
            else
            {
                tierPool = pool.Where(p => p.tier == tier).ToList();
            }

            // [원본 이탈 — Fable 승인 2026-08-03] 티어 풀 소진 시 잔여 후보 전체로 폴백 — PickAugments의
            // "any" 폴백과 동일 패턴. (신규 프로필: BASE 22종 전부 SILVER + %3 스테이지 GOLD 강제 → 오퍼가
            // 통째로 비어 증강/유물 노드가 EVENT로 조용히 대체되던 문제. ENGINE_PORT_DESIGN.md S16 §A)
            if (!tierPool.Any(p => !used.Contains(p.id))) tierPool = avail;

            var cat = string.IsNullOrWhiteSpace(favoredCat) ? null : favoredCat;
            if (cat != null)
                Take(rng.PickOrDefault(tierPool.Where(p => !used.Contains(p.id) && p.desc.Contains(cat)).ToList()));
            if (fav != null && fav != cat)
                Take(rng.PickOrDefault(tierPool.Where(p => !used.Contains(p.id) && p.desc.Contains(fav)).ToList()));

            int guard = 0;
            while (outp.Count < 3 && guard++ < 80)
            {
                var candidates = tierPool.Where(p => !used.Contains(p.id)).ToList();
                var pick = rng.PickOrDefault(candidates);
                if (pick == null) break;
                Take(pick);
            }
            rng.Shuffle(outp);
            return outp;
        }

        // ══════════════════════════════════════════════════════════════════
        // setSynergyAug (Kotlin L655-670) — 플레이어가 짓는 중인(requires 1개+ 보유·미완성) 세트들의
        // 미보유 requires 중 cat(기본 AUGMENT)이고 exclude에 없는 후보에서, 가장 근접한(미보유 requires
        // 최소) 세트 우선으로 1개 추첨. Sets.cs(S2b)엔 SetEffect 데이터 테이블만 있고 이 결합 로직 자체는
        // 없어(Sets.cs 파일 헤더에 setSynergyAug/setSynergyName 언급 없음, 2026-07-31 확인) 이 파일에
        // 새로 이식한다 — Fable 후속 지시(2026-07-31) 반영, Sets.All을 유일 소스로 참조만 한다.
        //
        // RNG 소비: 후보 세트를 "미보유 requires 개수" 오름차순(가까운 세트 우선, 동점은 Sets.All 선언
        // 순서로 안정정렬 — Kotlin sortedBy와 동일한 안정성)으로 순회하며, 세트마다 최대 1회
        // PickOrDefault를 시도한다. 그 세트의 missingAug 후보 목록이 비어있으면 RNG를 소비하지 않고
        // (Rng.PickOrDefault 계약) 다음 세트로 넘어간다 — Kotlin의 `filter{}.randomOrNull(rng)` 반복 호출과
        // 소비 패턴이 동일하다.
        // ══════════════════════════════════════════════════════════════════
        internal static Perk SetSynergyAug(IReadOnlyCollection<string> held, IReadOnlyCollection<string> exclude, Rng rng, PCat cat = PCat.AUGMENT)
        {
            var heldSet = held as HashSet<string> ?? new HashSet<string>(held);
            var excludeSet = exclude as HashSet<string> ?? new HashSet<string>(exclude);

            var candidateBySet = Sets.All
                .Where(s => s.requires.Any(r => heldSet.Contains(r)) && !s.requires.All(r => heldSet.Contains(r)))
                .Select(s => (set: s, missing: s.requires.Count(r => !heldSet.Contains(r))))
                .OrderBy(x => x.missing) // 안정 정렬 — 동점은 Sets.All 선언순서 유지(Kotlin sortedBy와 동일)
                .ToList();

            foreach (var (s, _) in candidateBySet)
            {
                var missingAug = s.requires
                    .Where(r => !heldSet.Contains(r) && !excludeSet.Contains(r))
                    .Select(Perks.ById)
                    .Where(p => p != null && p.cat == cat)
                    .ToList();
                var pick = rng.PickOrDefault(missingAug);
                if (pick != null) return pick;
            }
            return null;
        }

        // ── 상점 오퍼 생성 (freshShopOffer, Kotlin L1404-1419 + 웹 game.js:2322-2339 5필드 배선) ──
        public static List<ShopEntry> FreshOffer(RunState run, IReadOnlyDictionary<string, long> stat)
        {
            var held = new HashSet<string>(run.Perks);
            var rng = run.Rng;
            bool allowPrism = rng.NextDouble() < EventPrismRate;
            var mods = ShopMods(run);
            double pm = ShopPriceMul(mods);
            double itemPm = ItemPriceMul(mods);
            int slot = ShopSlotBonus(mods);

            List<Perk> GatePrism(List<Perk> list)
            {
                if (allowPrism) return list.Take(2).ToList();
                var noPrism = list.Where(p => p.tier != Tier.PRISM).ToList();
                return (noPrism.Count > 0 ? noPrism : list).Take(2).ToList();
            }

            var augs = GatePrism(PickAugments(rng, run.Stage, held, 4, stat))
                .Select(p => new ShopEntry { kind = 'A', id = p.id, price = RoundPrice(AugPrice(p.tier) * pm) });
            var relics = GatePrism(PickRelics(rng, held, 4, stat))
                .Select(p => new ShopEntry { kind = 'R', id = p.id, price = RoundPrice(p.price * pm) });
            // 웹 game.js:2339 `E.pickItems(this.rng, 2 + slot, ...)` — 상품칸 기본 2 + shopSlotBonus(vip 등).
            var items = PickItems(rng, 2 + slot)
                .Select(i => new ShopEntry { kind = 'I', id = i.id, price = RoundPrice(i.coinCost * itemPm) });

            var all = augs.Concat(relics).Concat(items).ToList();
            rng.Shuffle(all);
            return all;
        }

        // ── 구매/리롤/나가기 (handleShop L1629-1681, 02_service.md §4) ──
        public static List<RunEvent> Buy(RunState run, int index)
        {
            if (run.Phase != RunPhase.EventShop) return RunEvents.Rejected("PHASE_NOT_SHOP");
            if (index < 0 || index >= run.ShopOffer.Count) return RunEvents.Rejected("INVALID_INDEX");
            var entry = run.ShopOffer[index];
            // 웹 파리티 P3-4 Opus 2차검수 웹 이탈 정리⑤(game.js:2350 shopBuy 최우선 가드) — 프리즘잉크는
            // 런당 1회만 구매 가능. 코인/가방 체크보다 먼저(웹과 동일 순서).
            if (entry.id == "prism_ink" && run.PrismInkBought) return RunEvents.Rejected("PRISM_INK_ALREADY_BOUGHT");
            if (run.Coins < entry.price) return RunEvents.Rejected("INSUFFICIENT_COINS");
            bool isItem = entry.kind != 'A' && entry.kind != 'R';
            // 웹 game.js:2301 `_giveItem` cap = 3 + itemCapBonus(item_bag 등) — ItemUse.EffectiveSlots로 위임.
            if (isItem && run.Items.Count >= ItemUse.EffectiveSlots(run)) return RunEvents.Rejected("BAG_FULL");

            run.Coins -= entry.price;
            run.UsedCmds.Add("RUNSHOP"); // 런 끝까지 보존(StageFlow.ClearStage의 usedCmds 리셋 예외 목록)
            if (entry.id == "prism_ink") run.PrismInkBought = true;
            if (isItem) run.Items.Add(entry.id);
            else run.Perks.Add(entry.id); // 증강/유물 구매는 즉시 영구 추가(대기 없음, §4-D)
            run.ShopOffer.RemoveAt(index); // 구매 후에도 상점 유지, 산 것만 제거(§4-D)

            return RunEvents.One(new RunEvent
            {
                type = "SHOP_PURCHASED", shopBought = entry, shopOffer = run.ShopOffer, coinsDelta = -entry.price,
            });
        }

        public static List<RunEvent> Reroll(RunState run, IReadOnlyDictionary<string, long> stat)
        {
            if (run.Phase != RunPhase.EventShop) return RunEvents.Rejected("PHASE_NOT_SHOP");
            int cost = RerollCostFor(run);
            if (run.Coins < cost) return RunEvents.Rejected("INSUFFICIENT_COINS");
            run.Coins -= cost;
            var offer = FreshOffer(run, stat);
            run.ShopOffer.Clear();
            run.ShopOffer.AddRange(offer);
            return RunEvents.One(new RunEvent { type = "SHOP_REROLLED", shopOffer = run.ShopOffer, coinsDelta = -cost });
        }

        public static List<RunEvent> Leave(RunState run)
        {
            if (run.Phase != RunPhase.EventShop) return RunEvents.Rejected("PHASE_NOT_SHOP");
            run.ShopOffer.Clear();
            run.Phase = RunPhase.Spin;
            return RunEvents.One(new RunEvent { type = "SHOP_LEFT" });
        }
    }
}
