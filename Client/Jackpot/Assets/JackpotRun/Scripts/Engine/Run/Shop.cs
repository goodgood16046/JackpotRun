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
        public const int RerollCost = 6;                 // SHOP_REROLL(Kotlin L1400) — 정액, 증가 없음(§4-C)
        private const double EventPrismRate = 0.12;       // EVENT_PRISM_RATE(Kotlin L1249)

        private static int AugPrice(Tier t) => t switch
        {
            Tier.SILVER => 14,
            Tier.GOLD => 24,
            _ => 36, // PRISM
        };

        // ══════════════════════════════════════════════════════════════════
        // 해금 게이트 — perkGate/perkUnlocked/gatedPool/unlockedPerks (Kotlin L825-914).
        // Schools.cs(S2b)의 BasePerkIds/SchoolReq/PerkGateOverrides/SchoolResearch를 단일 소스로 결합만
        // 한다(재정의 금지 — S4 백로그 "Schools.BasePerkIds 사용"). Formulas.AccountLevel(S1)을 그대로
        // 재사용하며 achievements 인자는 null(=482종 미반영, S1 주석/S4 백로그 명시 제약 — S5가 채운다).
        // 死코드 정리(S4 백로그): 여기서는 Unlocks.Meets(StatReq 기반)만 쓰고 Formulas.MeetsReq(튜플 기반,
        // 미사용 死코드)는 호출하지 않는다.
        // ══════════════════════════════════════════════════════════════════
        internal static UnlockGate PerkGate(Perk p)
        {
            if (Schools.BasePerkIds.Contains(p.id))
                return new UnlockGate { minLevel = 0, req = Array.Empty<StatReq>(), school = "" };
            if (Schools.PerkGateOverrides.TryGetValue(p.id, out var over))
                return over;

            var baseGate = Schools.SchoolReq.TryGetValue(p.school ?? "", out var b)
                ? b
                : new UnlockGate { minLevel = 0, req = Array.Empty<StatReq>(), school = "" };
            switch (p.tier)
            {
                case Tier.PRISM:
                    return new UnlockGate { minLevel = Math.Max(baseGate.minLevel + 4, 12), req = baseGate.req, school = baseGate.school };
                case Tier.SILVER:
                    return new UnlockGate { minLevel = Math.Max(baseGate.minLevel - 2, 2), req = baseGate.req, school = baseGate.school };
                default: // GOLD
                    return baseGate;
            }
        }

        internal static bool SchoolResearchDone(string school, IReadOnlyDictionary<string, long> stat)
        {
            if (string.IsNullOrEmpty(school) || !Schools.SchoolResearch.TryGetValue(school, out var r)) return false;
            long v = (stat != null && stat.TryGetValue(r.key, out var vv)) ? vv : 0L;
            return v >= r.threshold;
        }

        internal static bool PerkUnlocked(Perk p, IReadOnlyDictionary<string, long> stat)
        {
            if (p == null) return false;
            if (stat != null && stat.TryGetValue("seen_" + p.id, out var seen) && seen > 0) return true;
            var g = PerkGate(p);
            if (p.tier != Tier.PRISM && SchoolResearchDone(g.school, stat)) return true;
            if (Formulas.AccountLevel(stat) < g.minLevel) return false;
            return Unlocks.Meets(g.req, stat);
        }

        internal static List<Perk> UnlockedPerks(IReadOnlyList<Perk> pool, IReadOnlyDictionary<string, long> stat)
        {
            var list = new List<Perk>();
            for (int i = 0; i < pool.Count; i++) if (PerkUnlocked(pool[i], stat)) list.Add(pool[i]);
            return list;
        }

        // gatedPool — 미해금 제외, 전부 잠겼으면 BasePerkIds만 폴백(그마저 없으면 원본 그대로, 데드엔드 방지).
        internal static List<Perk> GatedPool(IReadOnlyList<Perk> pool, IReadOnlyDictionary<string, long> stat)
        {
            if (stat == null || stat.Count == 0) return new List<Perk>(pool);
            var unlocked = UnlockedPerks(pool, stat);
            if (unlocked.Count > 0) return unlocked;
            var baseOnly = pool.Where(p => Schools.BasePerkIds.Contains(p.id)).ToList();
            return baseOnly.Count > 0 ? baseOnly : new List<Perk>(pool);
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

        // ── 상점 오퍼 생성 (freshShopOffer, Kotlin L1404-1419) ──
        public static List<ShopEntry> FreshOffer(RunState run, IReadOnlyDictionary<string, long> stat)
        {
            var held = new HashSet<string>(run.Perks);
            var rng = run.Rng;
            bool allowPrism = rng.NextDouble() < EventPrismRate;

            List<Perk> GatePrism(List<Perk> list)
            {
                if (allowPrism) return list.Take(2).ToList();
                var noPrism = list.Where(p => p.tier != Tier.PRISM).ToList();
                return (noPrism.Count > 0 ? noPrism : list).Take(2).ToList();
            }

            var augs = GatePrism(PickAugments(rng, run.Stage, held, 4, stat))
                .Select(p => new ShopEntry { kind = 'A', id = p.id, price = AugPrice(p.tier) });
            var relics = GatePrism(PickRelics(rng, held, 4, stat))
                .Select(p => new ShopEntry { kind = 'R', id = p.id, price = p.price });
            var items = PickItems(rng, 2)
                .Select(i => new ShopEntry { kind = 'I', id = i.id, price = i.coinCost });

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
            if (run.Coins < entry.price) return RunEvents.Rejected("INSUFFICIENT_COINS");
            bool isItem = entry.kind != 'A' && entry.kind != 'R';
            if (isItem && run.Items.Count >= ItemUse.ItemSlots) return RunEvents.Rejected("BAG_FULL");

            run.Coins -= entry.price;
            run.UsedCmds.Add("RUNSHOP"); // 런 끝까지 보존(StageFlow.ClearStage의 usedCmds 리셋 예외 목록)
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
            if (run.Coins < RerollCost) return RunEvents.Rejected("INSUFFICIENT_COINS");
            run.Coins -= RerollCost;
            var offer = FreshOffer(run, stat);
            run.ShopOffer.Clear();
            run.ShopOffer.AddRange(offer);
            return RunEvents.One(new RunEvent { type = "SHOP_REROLLED", shopOffer = run.ShopOffer, coinsDelta = -RerollCost });
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
