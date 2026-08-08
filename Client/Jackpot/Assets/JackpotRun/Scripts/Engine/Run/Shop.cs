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
        // — 승천(ascMods)은 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18)로 배선했다. 심화 영수증
        // (receiptMul)은 P7 미구현이라 여전히 생략(곱연산 항이라 나중에 그대로 끼워 넣을 수 있다).
        private static double ShopPriceMul(RunState run, Mods mods) =>
            Math.Max(0.4, AscMods.Get(run.Asc).ShopPriceMul * mods.shopPriceMul);

        // 웹 game.js:2328 `itemPm = max(0.4, pm * (sm.itemPriceMul||1))`.
        private static double ItemPriceMul(RunState run, Mods mods) => Math.Max(0.4, ShopPriceMul(run, mods) * mods.itemPriceMul);

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
        // pickPerksByTier — 웹 파리티 P3.5(WEB_PARITY_DESIGN.md §2-(T) 후속①②) 전면 재작성. 웹
        // engine.js:1213-1241 pickPerksByTier를 리터럴 포팅 + PERK_FAMILY 랭크 게이팅(신규, §2-(T)
        // 후속①) + dev_major favoredCat(Unity 전용 확장 — 아래 별도 각주).
        //
        // 예전 구현(Kotlin SlotV2Engine.kt L1654-1700 계열, 스테이지 가중 SILVER/GOLD/PRISM 확률 롤
        // TierWeights/RollTier + forceRare + "티어 풀 소진 시 avail 전체로 폴백")은 이 함수의 실사용
        // 경로에서 이미 죽은 코드였다 — NodeEvents.OfferPerks가 항상 forceTier를 확정해서 넘기므로
        // else 분기(가중 롤)엔 애초에 도달할 수 없었고, forceRare(불운 게이지 만땅)는 그 죽은 분기의
        // silverW를 0으로 만드는 것 말곤 하는 일이 없어 사실상 아무 효과가 없었다(버그). 웹 구조와도
        // 맞지 않아 전부 제거한다 — 상점 전용 Shop.PickAugments/PickRelics가 쓰는 TierWeights/RollTier는
        // 별개 함수라 영향 없다(그쪽은 웹 game.js 실사용 상점(2322-2339)이 이 함수 대신 offerPerks를
        // 직접 쓰는 것과 이미 갈라져 있는 별도 기술부채 — 이번 슬라이스 범위 밖, 보고 대상).
        //
        // pool은 이미 unlockLevel 게이트를 통과한 상태로 전달돼야 한다(웹 game.js:234-235 _augPool()/
        // _relicPool()과 동일하게 "호출자가 먼저 거른다" 계약 — 웹 pickPerksByTier 자체엔 게이트 개념이
        // 없다). 호출부(NodeEvents.OfferPerks)가 Shop.GatedPool을 먼저 돌려서 넘긴다.
        //
        // [dev_major favoredCat — Unity 전용, 웹에 대응 없음] 이 장치의 desc("주력 계열 증강 등장확률
        // 소폭↑")가 유일한 실효과라 완전히 들어내면 장치가 no-op이 된다. dev_holdfile/dev_retake와
        // 동일한 통합 원칙(미장착 시엔 웹과 RNG 소비 순서 100% 동일, 장착 시에만 추가 소비)으로 유지 —
        // family/tier-purity 게이팅을 통과한 후보 중에서만 우선 픽하도록 재배선했다(예전엔 FavoredSymbol
        // (held)이 dev_major 장착 여부와 *무관하게* 항상 추가로 한 번 더 소비되는 버그성 코드가 있었다
        // — 모든 오퍼에서 웹에 없는 RNG 소비가 매번 끼어 있었다는 뜻이라, 이번 슬라이스에서 완전히
        // 제거했다. 이제 favoredCat이 null(=dev_major 미장착이거나 RELIC 노드)이면 이 블록 자체가
        // RNG를 전혀 소비하지 않는다).
        // ══════════════════════════════════════════════════════════════════
        internal static List<Perk> PickPerksByTier(
            Rng rng, IReadOnlyList<Perk> pool, IReadOnlyCollection<string> held,
            Tier? forceTier, bool bossClear = false, string favoredCat = null)
        {
            var taken = new HashSet<string>(held);
            var avail = pool.Where(p => !taken.Contains(p.id)).ToList();
            if (avail.Count == 0) return new List<Perk>();

            // 웹 engine.js:1218 `let tier = forceTier || (bossClear ? "PRISM" : "SILVER")`. 웹
            // offerPerks도 항상 forceTier를 확정해서 넘기므로 bossClear 분기는 웹 자신도 실사용
            // 경로에서 도달하지 않는 죽은 파라미터다 — 시그니처 패리티를 위해 그대로 남긴다.
            Tier tier = forceTier ?? (bossClear ? Tier.PRISM : Tier.SILVER);

            // 웹 engine.js:1219-1228 — 티어순수 단계형 폴백(PRISM→GOLD→SILVER, 각 단계 풀이 있으면
            // 그 자리에서 멈춘다). 예전 Unity는 여기서 avail 전체(타 티어 혼용)로 폴백했었다
            // (ENGINE_PORT_DESIGN.md S16 §A, 2026-08-03 승인) — 그 근거였던 "BASE 22종 게이트로
            // 대부분 풀이 텅 빔" 문제는 §2-(P) 슬라이스가 게이트 자체를 단순화(unlockLevel 있는 8종만
            // 제외, 나머지 154/162종 상시개방)하며 이미 해소됐다. 이번 슬라이스에서 웹 기준 단계형
            // 폴백으로 되돌린다(§2-(T) 후속② 정렬) — "3개 못 채우면 적게 제시(타티어로 메우지 않음)"
            // 원칙 그대로.
            var tierPool = pool.Where(p => p.tier == tier && !taken.Contains(p.id)).ToList();
            if (tierPool.Count == 0)
            {
                Tier[] fallbackOrder = tier == Tier.PRISM
                    ? new[] { Tier.GOLD, Tier.SILVER }
                    : tier == Tier.GOLD ? new[] { Tier.SILVER } : Array.Empty<Tier>();
                foreach (var lower in fallbackOrder)
                {
                    var candidate = pool.Where(p => p.tier == lower && !taken.Contains(p.id)).ToList();
                    if (candidate.Count > 0) { tierPool = candidate; tier = lower; break; }
                }
            }

            // ── 패밀리 게이팅(신규, 웹 engine.js:1229-1233) — 같은 계열은 "보유한 같은 패밀리 개수+1"
            // 랭크만 후보(약→강 순차 해금), 오퍼 1개당 같은 패밀리는 1개만. initialHeld(이 오퍼 시작
            // 시점의 보유분)만 기준 — 이번 오퍼에서 새로 뽑은 것은 랭크 카운트에 반영하지 않는다
            // (usedFams가 같은 패밀리 중복 픽 자체를 이미 막으므로 결과는 웹의 "initialHeld 클로저
            // 참조"와 동치 — 매 후보 평가마다 다시 reduce하는 웹 구현 대신 Dictionary로 한 번만 집계).
            var famCounts = new Dictionary<string, int>();
            foreach (var id in held)
            {
                var fam = PerkFamily.FamOf(id).Fam;
                famCounts.TryGetValue(fam, out var c);
                famCounts[fam] = c + 1;
            }
            bool Eligible(Perk p)
            {
                var (fam, rank) = PerkFamily.FamOf(p.id);
                famCounts.TryGetValue(fam, out var c);
                return rank == c + 1;
            }

            var outp = new List<Perk>();
            var usedFams = new HashSet<string>();

            if (!string.IsNullOrWhiteSpace(favoredCat))
            {
                var favPick = rng.PickOrDefault(tierPool
                    .Where(p => !taken.Contains(p.id) && Eligible(p) && p.desc.Contains(favoredCat)).ToList());
                if (favPick != null)
                {
                    outp.Add(favPick);
                    taken.Add(favPick.id);
                    usedFams.Add(PerkFamily.FamOf(favPick.id).Fam);
                }
            }

            // 웹 engine.js:1234-1239 — family-gated 랜덤 채움(guard<120, 웹과 동일 상한).
            int guard = 0;
            while (outp.Count < 3 && guard++ < 120)
            {
                var cand = tierPool.Where(p => !taken.Contains(p.id) && Eligible(p) && !usedFams.Contains(PerkFamily.FamOf(p.id).Fam)).ToList();
                if (cand.Count == 0) break;
                var pick = rng.Pick(cand);
                taken.Add(pick.id);
                usedFams.Add(PerkFamily.FamOf(pick.id).Fam);
                outp.Add(pick);
            }
            rng.Shuffle(outp); // 웹 engine.js:1240 `return rng.shuffle(out);` — out.length<3이어도 무조건 셔플.
            return outp;
        }

        // ══════════════════════════════════════════════════════════════════
        // setSynergyAug — 웹 engine.js:1170-1192 setSynergyPick 리터럴 포팅(Kotlin L655-670 원류).
        // 플레이어가 짓는 중인(requires 1개+ 보유·미완성) 세트들의 미보유 requires 중 exclude에 없는
        // *증강* 후보에서, 가장 근접한(미보유 requires 최소) 세트 우선으로 1개 추첨. Sets.cs(S2b)엔
        // SetEffect 데이터 테이블만 있고 이 결합 로직 자체는 없어(Sets.cs 파일 헤더에 setSynergyAug/
        // setSynergyName 언급 없음, 2026-07-31 확인) 이 파일에 새로 이식한다 — Fable 후속 지시
        // (2026-07-31) 반영, Sets.All을 유일 소스로 참조만 한다.
        //
        // [cat 매개변수 — 웹 engine.js:1172-1173 그대로] "cat: 시그니처 패리티용(미사용). Kotlin 도
        // 노드 cat 과 무관하게 항상 AUGMENT 조각만 주입(이름이 setSynergyAug 인 이유) — perk(id) 는
        // 전체에서 찾되 cat==AUGMENT 만 채택." 웹 setSynergyPick 본문은 실제로 cat 인자를 단 한 번도
        // 읽지 않고 `augById = Map(AUGMENTS...)`로 고정한다 — 즉 RELIC 노드 오퍼라도 5% 시너지
        // 주입 조각은 항상 AUGMENT일 수 있다(RELIC이 아님). 이 함수도 cat 인자를 매개변수로만
        // 남기고(호출부 시그니처 패리티) 내부에서는 무조건 PCat.AUGMENT로 필터한다 — 웹 파리티
        // P3.5(§2-(T) 후속②) 정렬: 예전 Unity는 node 종류로 실제 필터링 카테고리를 갈라(AUGMENT
        // 노드→AUGMENT만, RELIC 노드→RELIC만) 웹과 다르게 동작했다.
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
                    .Where(p => p != null && p.cat == PCat.AUGMENT) // 웹과 동일 — cat 인자 미사용, 항상 AUGMENT
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
            double pm = ShopPriceMul(run, mods);
            double itemPm = ItemPriceMul(run, mods);
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
            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — 웹 game.js:2358/2492 `r.shopBought.push(...)`.
            // Shop.Leave가 REWARD_DONE 메시지("🛒 상점에서 구매: ...") 조립에 소비한다.
            run.ShopBoughtLabels.Add(EntryName(entry));

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
            // 웹 파리티 P4 — 웹 game.js:2514-2518 shopExit(): 구매 이력이 있으면 "🛒 상점에서 구매: ..."
            // 없으면 "🛒 상점을 둘러봤어요 (구매 없음)".
            string msg = run.ShopBoughtLabels.Count > 0
                ? "상점에서 구매: " + string.Join(" · ", run.ShopBoughtLabels)
                : "상점을 둘러봤어요 (구매 없음)";
            RewardFlow.Enter(run, msg);
            return RunEvents.One(new RunEvent { type = "SHOP_LEFT" });
        }

        // ShopBoughtLabels/REWARD_DONE 메시지 조립용 이름 조회 — RunView.ShopEntryLabel(UI 로그, emoji
        // 포함)과 같은 데이터 소스지만 엔진 산출 문자열 규약대로 이모지를 쓰지 않는다.
        private static string EntryName(ShopEntry entry)
        {
            if (entry.kind == 'A' || entry.kind == 'R')
                return Perks.ById(entry.id)?.name ?? entry.id;
            return Items.ById(entry.id)?.name ?? entry.id;
        }
    }
}
