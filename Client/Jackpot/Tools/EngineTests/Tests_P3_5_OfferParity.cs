using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // ══════════════════════════════════════════════════════════════════════
    // 웹 파리티 P3.5 (WEB_PARITY_DESIGN.md §2-(T) 후속①②③ — "퍽 오퍼 알고리즘 웹 완전 동기화") 테스트.
    //   ① PERK_FAMILY 랭크 게이팅 이식 — 손전사 골든(51+45) + 랭크 경계 3종.
    //   ② 오퍼 알고리즘 웹 정렬 — 고정 시드 회귀(티어순수 폴백·10%등급업·forceTier·dev_major).
    //   ③ retake_form ctx 반영 — ctx-조건부 캐릭터(bankrupt)가 재굴림에서 실제로 다르게 계산되는지.
    // ══════════════════════════════════════════════════════════════════════

    // ── ① PERK_FAMILY 51+45=96종 손전사 골든 — data.js:345-375(AUG_FAMILY)·603-621(REL_FAMILY)를
    // PerkFamily.cs를 보지 않고 다시 옮겨 적어 대조한다(Tests_P3_4_ContentGolden.cs와 동일 원칙 —
    // "이후 변경 감지"만 하는 스냅샷과 달리 "지금 값이 웹과 맞는지"를 독립적으로 검증). 키 집합이 정확히
    // 일치해야 한다(누락·과잉 전부 실패).
    internal static class Tests_P3_5_PerkFamilyGolden
    {
        private static readonly (string id, string fam, int rank)[] Golden =
        {
            // 증강 51종
            ("preview", "first_spin", 1), ("morning", "first_spin", 2),
            ("review", "last_spin", 1), ("evening", "last_spin", 2),
            ("diligence", "per_spin", 1), ("note_take", "per_spin", 2),
            ("cherry_up", "cherry_s", 1), ("red_safetynet", "cherry_s", 1),
            ("star_up", "star_s", 1), ("star_up2", "star_s", 2),
            ("gem_polish", "gem_s", 1), ("gem_buff", "gem_s", 2),
            ("combo_note", "setbonus_s", 1), ("set_sense", "setbonus_s", 2),
            ("deep_read", "studytag_s", 1), ("study_tag", "studytag_s", 2),
            ("overheat_formula", "exp_g", 1), ("greed_calc", "exp_g", 2), ("polymath", "exp_g", 3), ("greed", "exp_g", 4),
            ("gem_invest", "gem_g", 1), ("polish_work", "gem_g", 1), ("lapidary", "gem_g", 2),
            ("skull_study", "skull_g", 1), ("necromancer", "skull_g", 2),
            ("bullseye", "center_g", 1), ("center", "center_g", 2), ("focus_fire", "center_g", 3),
            ("mirror", "ends_g", 1), ("twins", "ends_g", 2), ("symmetry", "ends_g", 3),
            ("domino", "chain_g", 1), ("chain", "chain_g", 2),
            ("honor_student", "studytag_g", 1), ("crammer_tag", "studytag_g", 2),
            ("royal_decree", "crown_g", 1), ("crown_seek", "crown_g", 2),
            ("overdrive", "exp_p", 1), ("supernova", "exp_p", 2), ("extreme_overload", "exp_p", 3),
            ("wild_world", "wild_p", 1), ("joker", "wild_p", 2),
            ("seed_garden", "seed_p", 1), ("great_harvest", "seed_p", 2),
            ("mega_jackpot", "jackpot_p", 1), ("jackpot", "jackpot_p", 2), ("crown_burst", "jackpot_p", 3),
            ("black_diploma", "curse_p", 1), ("curse_grad", "curse_p", 2),
            ("discount", "disc_s", 1), ("thrifty", "disc_s", 2),
            // 유물 45종
            ("eraser", "rel_book_s", 1), ("auto_pen", "rel_book_s", 1), ("practice_pad", "rel_book_s", 1), ("old_book", "rel_book_s", 2),
            ("cherry_candy", "rel_cherry_s", 1), ("cherry_press", "rel_cherry_s", 1), ("cherry_jam", "rel_cherry_s", 2), ("cherry_can", "rel_cherry_s", 2),
            ("rusty_coin", "rel_coin_s", 1), ("coin_pouch", "rel_coin_s", 1), ("old_wallet", "rel_coin_s", 1), ("crumpled_coupon", "rel_coin_s", 1),
            ("ruler", "rel_first_s", 1), ("pencil", "rel_first_s", 2),
            ("desk_lamp", "rel_last_s", 1), ("coffee", "rel_last_s", 2),
            ("magnifier", "rel_rare_s", 1), ("mini_scope", "rel_rare_s", 1), ("lucky_eraser", "rel_rare_s", 1),
            ("gem_dust", "rel_gem_s", 1), ("calculator", "rel_gem_s", 2),
            ("library_card", "rel_book_g", 1), ("thick_tome", "rel_book_g", 2),
            ("clover", "rel_exp_g", 1), ("flame_canister", "rel_exp_g", 1), ("hot_handle", "rel_exp_g", 2), ("four_clover", "rel_exp_g", 3), ("greed_goblet", "rel_exp_g", 3), ("charm_relic", "rel_exp_g", 4),
            ("set_charm", "rel_setbonus_g", 1), ("combo_trophy", "rel_setbonus_g", 1),
            ("wide_lens", "rel_center_g", 1), ("focus_ring", "rel_center_g", 2),
            ("black_candle", "rel_skull_g", 1), ("black_report", "rel_skull_g", 1), ("ominous_skull", "rel_skull_g", 2), ("skull_idol", "rel_skull_g", 3),
            ("gem_cert", "rel_gem_g", 1), ("gem_tiara", "rel_gem_g", 2),
            ("gamblers_eye", "rel_rare_g", 1), ("fate_handle", "rel_rare_g", 2), ("crystal_ball", "rel_rare_g", 3),
            ("kings_ledger", "rel_crown_g", 1), ("crown_stand", "rel_crown_g", 2), ("crown_jewel", "rel_crown_g", 3),
        };

        public static void Run(TestCtx t)
        {
            t.Eq(51 + 45, Golden.Length, "[perkfamily-golden] 손전사 테이블 96종(증강51+유물45)");
            t.Eq(Golden.Length, PerkFamily.Map.Count, "[perkfamily-golden] PerkFamily.Map 항목 수 일치");

            foreach (var (id, fam, rank) in Golden)
            {
                t.True(PerkFamily.Map.ContainsKey(id), $"[perkfamily-golden] '{id}' PerkFamily.Map에 존재");
                var actual = PerkFamily.FamOf(id);
                t.Eq(fam, actual.Fam, $"[perkfamily-golden] '{id}' 패밀리키");
                t.Eq(rank, actual.Rank, $"[perkfamily-golden] '{id}' 랭크");
            }

            // 골든에 없는 여분 키가 Map에 남아있으면 실패(Tests_P3_4_ContentGolden과 동일 원칙 — "키 집합이
            // 정확히 일치"까지 검증).
            var goldenIds = new HashSet<string>(Golden.Select(g => g.id));
            foreach (var kv in PerkFamily.Map)
                t.True(goldenIds.Contains(kv.Key), $"[perkfamily-golden] Map의 '{kv.Key}'가 골든 테이블에도 존재");

            // 미등록 id는 (자기 id, 랭크1) 폴백 — 웹 engine.js:15 famOf.
            var fallback = PerkFamily.FamOf("study"); // AUG_FAMILY/REL_FAMILY 어디에도 없는 흔한 증강
            t.Eq("study", fallback.Fam, "[perkfamily-golden] 미등록 id 폴백 — 패밀리=자기 id");
            t.Eq(1, fallback.Rank, "[perkfamily-golden] 미등록 id 폴백 — 랭크1(항상 후보)");
        }
    }

    // ── ① 랭크 게이팅 경계 3종 — Shop.PickPerksByTier를 직접 호출해 웹 engine.js:1229-1239 eligible/
    // usedFams 규칙을 검증한다(NodeEvents 경유 시 10%등급업/시너지주입 RNG가 섞여 관측이 어려워짐).
    internal static class Tests_P3_5_FamilyRankGating
    {
        private const int Trials = 400;

        public static void Run(TestCtx t)
        {
            Rank1OnlyWhenFamilyUnheld(t);
            Rank2OpensAfterHoldingRank1(t);
            ExpGChainThreeRanks(t);
            OneFamilyPerOfferSilver(t);
        }

        // held=[] → SILVER 랭크2 멤버 7종(morning/evening/note_take/star_up2/gem_buff/set_sense/study_tag)은
        // "보유한 같은 패밀리 수+1==랭크" 규칙상 heldCount=0이라 랭크1만 후보 — 절대 등장 불가.
        private static void Rank1OnlyWhenFamilyUnheld(TestCtx t)
        {
            var rank2Silver = new HashSet<string> { "morning", "evening", "note_take", "star_up2", "gem_buff", "set_sense", "study_tag" };
            var seen = new HashSet<string>();
            for (long seed = 1_000_000L; seed < 1_000_000L + Trials; seed++)
            {
                var rng = new Rng(seed);
                var picks = Shop.PickPerksByTier(rng, Perks.Augments, new HashSet<string>(), Tier.SILVER);
                foreach (var p in picks) seen.Add(p.id);
            }
            foreach (var id in rank2Silver)
                t.True(!seen.Contains(id), $"[rank-gate:unheld] 미보유 상태에서 랭크2 '{id}' 절대 미등장(400시드)");
            t.True(seen.Contains("preview") || seen.Contains("review") || seen.Contains("diligence"),
                "[rank-gate:unheld] 랭크1 멤버는 정상 등장(표본 sanity)");
        }

        // held=["preview"](first_spin 랭크1) → heldCount(first_spin)=1 → 랭크2(morning) 개방.
        // preview 자신은 이미 taken이라 재등장 불가.
        private static void Rank2OpensAfterHoldingRank1(TestCtx t)
        {
            var held = new HashSet<string> { "preview" };
            var seen = new HashSet<string>();
            for (long seed = 1_100_000L; seed < 1_100_000L + Trials; seed++)
            {
                var rng = new Rng(seed);
                var picks = Shop.PickPerksByTier(rng, Perks.Augments, held, Tier.SILVER);
                foreach (var p in picks) seen.Add(p.id);
            }
            t.True(seen.Contains("morning"), "[rank-gate:opened] preview 보유 후 랭크2 'morning' 등장(400시드 내)");
            t.True(!seen.Contains("preview"), "[rank-gate:opened] 이미 보유한 preview는 재등장 안 함");
        }

        // exp_g 패밀리(4랭크: overheat_formula→greed_calc→polymath→greed) 순차 개방 확인.
        private static void ExpGChainThreeRanks(TestCtx t)
        {
            // heldCount=1 → 랭크2(greed_calc)만 개방, 랭크3(polymath)·랭크4(greed) 불가.
            var held1 = new HashSet<string> { "overheat_formula" };
            var seen1 = new HashSet<string>();
            for (long seed = 1_200_000L; seed < 1_200_000L + Trials; seed++)
            {
                var rng = new Rng(seed);
                foreach (var p in Shop.PickPerksByTier(rng, Perks.Augments, held1, Tier.GOLD)) seen1.Add(p.id);
            }
            t.True(seen1.Contains("greed_calc"), "[rank-gate:exp_g] 랭크1 보유 후 랭크2 'greed_calc' 등장");
            t.True(!seen1.Contains("polymath"), "[rank-gate:exp_g] 랭크1만 보유 시 랭크3 'polymath' 미등장");
            t.True(!seen1.Contains("greed"), "[rank-gate:exp_g] 랭크1만 보유 시 랭크4 'greed' 미등장");

            // heldCount=2 → 랭크3(polymath)만 개방, 랭크4(greed) 여전히 불가.
            var held2 = new HashSet<string> { "overheat_formula", "greed_calc" };
            var seen2 = new HashSet<string>();
            for (long seed = 1_300_000L; seed < 1_300_000L + Trials; seed++)
            {
                var rng = new Rng(seed);
                foreach (var p in Shop.PickPerksByTier(rng, Perks.Augments, held2, Tier.GOLD)) seen2.Add(p.id);
            }
            t.True(seen2.Contains("polymath"), "[rank-gate:exp_g] 랭크1+2 보유 후 랭크3 'polymath' 등장");
            t.True(!seen2.Contains("greed"), "[rank-gate:exp_g] 랭크1+2만 보유 시 랭크4 'greed' 미등장");
            t.True(!seen2.Contains("overheat_formula") && !seen2.Contains("greed_calc"),
                "[rank-gate:exp_g] 이미 보유한 랭크1·2는 재등장 안 함");
        }

        // 오퍼 1개당 같은 패밀리 1개만(usedFams) — cherry_s 패밀리는 cherry_up·red_safetynet 둘 다
        // 랭크1(heldCount=0이면 둘 다 항상 후보)이라, 게이팅 없이는 한 오퍼에 같이 뽑힐 수 있다.
        private static void OneFamilyPerOfferSilver(TestCtx t)
        {
            int bothSeen = 0;
            for (long seed = 1_400_000L; seed < 1_400_000L + Trials; seed++)
            {
                var rng = new Rng(seed);
                var picks = Shop.PickPerksByTier(rng, Perks.Augments, new HashSet<string>(), Tier.SILVER);
                bool hasCherryUp = picks.Any(p => p.id == "cherry_up");
                bool hasRedSafety = picks.Any(p => p.id == "red_safetynet");
                if (hasCherryUp && hasRedSafety) bothSeen++;
            }
            t.Eq(0, bothSeen, "[rank-gate:one-per-offer] cherry_up·red_safetynet(둘 다 cherry_s 랭크1) 동시 등장 0건(400시드)");
        }
    }

    // ── ② 오퍼 알고리즘 고정 시드 회귀 — NodeEvents.ChooseNode 경유(실사용 경로 그대로: 10%등급업·
    // forceTier·family게이팅·dev_major 전부 관통). 값은 새 알고리즘으로 직접 실행해 재산출했다.
    internal static class Tests_P3_5_OfferFixedSeedRegression
    {
        public static void Run(TestCtx t)
        {
            var stat = new Dictionary<string, long> { ["playerLevel"] = 20 };

            HardcodedGoldenCases(t, stat);

            // 일반 AUGMENT 노드, held=[], stage=2(clearedStage=1→baseTier SILVER 통상) — family게이팅
            // 통과한 3택이 매 시드 재현되는지(결정론) 및 티어순수(전부 SILVER 또는 전부 10%등급업 시
            // GOLD)인지 확인.
            for (long seed = 2_000_000L; seed < 2_000_000L + 5; seed++)
            {
                var run = S4TestHelpers.NewRun(seed);
                run.Phase = RunPhase.NodeSelect;
                run.Stage = 2;
                run.NodeOptions.Add(NodeKind.Augment);
                var runB = S4TestHelpers.NewRun(seed);
                runB.Phase = RunPhase.NodeSelect;
                runB.Stage = 2;
                runB.NodeOptions.Add(NodeKind.Augment);

                var evA = NodeEvents.ChooseNode(run, 0, stat);
                var evB = NodeEvents.ChooseNode(runB, 0, stat);
                t.Eq("PERK_OFFER", evA[0].type, $"[fixed-seed seed={seed}] 오퍼 생성 성공");
                t.Eq(string.Join(",", run.PerkOfferIds), string.Join(",", runB.PerkOfferIds),
                    $"[fixed-seed seed={seed}] 동일 시드 재현성(결정론)");
                // 티어순수 — offerTier가 곧 nodeTier, 그 nodeTier가 아니면 SILVER 폴백뿐이므로 오퍼 전원이
                // 그 티어여야 한다(세트 시너지 주입 마지막 칸은 예외 — offerSynergyPerkId 확인해 제외).
                var mainTier = evA[0].offerTier;
                for (int i = 0; i < run.PerkOfferIds.Count; i++)
                {
                    bool isSynSlot = evA[0].offerSynergyPerkId != null && run.PerkOfferIds[i] == evA[0].offerSynergyPerkId
                                      && i == run.PerkOfferIds.Count - 1;
                    if (isSynSlot) continue;
                    t.Eq(mainTier, Perks.ById(run.PerkOfferIds[i]).tier, $"[fixed-seed seed={seed}] 오퍼[{i}] 티어순수");
                }
            }

            // forceTier(프리즘잉크) — PrismInkActive=true면 nodeTier가 PRISM으로 강제(10%롤과 무관하게).
            {
                var run = S4TestHelpers.NewRun(2_100_001L);
                run.Phase = RunPhase.NodeSelect;
                run.Stage = 2; // baseTier=SILVER
                run.PrismInkActive = true;
                run.NodeOptions.Add(NodeKind.Augment);
                var ev = NodeEvents.ChooseNode(run, 0, stat);
                t.Eq("PERK_OFFER", ev[0].type, "[fixed-seed:prismink] 오퍼 생성 성공");
                t.Eq(Tier.PRISM, ev[0].offerTier, "[fixed-seed:prismink] 강제 PRISM");
                t.True(!run.PrismInkActive, "[fixed-seed:prismink] 사용 후 리셋");
                t.True(run.PerkOfferIds.All(id => Perks.ById(id).tier == Tier.PRISM),
                    "[fixed-seed:prismink] 오퍼 전원 PRISM(티어순수)");
            }

            // bossClear(stage%5==0 클리어) — nodeTier가 PRISM(forceTier 없을 때의 fallback 경로긴 하나
            // 실사용 경로에서 offerTierBumped 관측을 통해 "PRISM에서 시작"했음을 간접 확인).
            {
                var run = S4TestHelpers.NewRun(2_200_001L);
                run.Phase = RunPhase.NodeSelect;
                run.Stage = 6; // clearedStage=5 → baseTier PRISM
                run.NodeOptions.Add(NodeKind.Relic);
                var ev = NodeEvents.ChooseNode(run, 0, stat);
                t.Eq("PERK_OFFER", ev[0].type, "[fixed-seed:boss] 오퍼 생성 성공");
                t.Eq(Tier.PRISM, ev[0].offerTier, "[fixed-seed:boss] 5의 배수 클리어 → PRISM");
            }

            // dev_major favoredCat — Opus 2차검수 권장⑦(WEB_PARITY_DESIGN.md §2-(U)) 반영.
            // (a) 전 슬롯 기준 — favored 픽은 PickPerksByTier 안에서 index 0으로 들어가지만, 함수 끝의
            // `rng.Shuffle(outp)`가 최종 위치를 다시 섞기 때문에 슬롯0만 보면 신호가 크게 희석된다
            // (예전 테스트의 결함). "오퍼 중 어디든" favored 심볼 desc를 포함하는지로 비교해야 한다.
            {
                int withDevMatches = 0, withoutDevMatches = 0;
                const int trials = 300;
                for (long seed = 2_300_000L; seed < 2_300_000L + trials; seed++)
                {
                    var runDev = S4TestHelpers.NewRun(seed, deviceId: "dev_major");
                    runDev.Phase = RunPhase.NodeSelect;
                    runDev.Stage = 2;
                    runDev.Perks.Add("cherry_up"); // desc에 🍒 포함 → FavoredSymbol 후보
                    runDev.NodeOptions.Add(NodeKind.Augment);
                    var evDev = NodeEvents.ChooseNode(runDev, 0, stat);
                    if (evDev[0].type == "PERK_OFFER" &&
                        runDev.PerkOfferIds.Any(id => Perks.ById(id)?.desc.Contains("🍒") ?? false))
                        withDevMatches++;

                    var runNoDev = S4TestHelpers.NewRun(seed);
                    runNoDev.Phase = RunPhase.NodeSelect;
                    runNoDev.Stage = 2;
                    runNoDev.Perks.Add("cherry_up");
                    runNoDev.NodeOptions.Add(NodeKind.Augment);
                    var evNoDev = NodeEvents.ChooseNode(runNoDev, 0, stat);
                    if (evNoDev[0].type == "PERK_OFFER" &&
                        runNoDev.PerkOfferIds.Any(id => Perks.ById(id)?.desc.Contains("🍒") ?? false))
                        withoutDevMatches++;
                }
                t.Report("[dev_major] 오퍼 내 🍒서술 포함율(전 슬롯)", $"장착={withDevMatches}/{trials} 미장착={withoutDevMatches}/{trials}");
                t.True(withDevMatches > withoutDevMatches,
                    $"[dev_major] favoredCat 장착 시 오퍼 어딘가에 favored 심볼을 더 자주 포함(장착{withDevMatches} > 미장착{withoutDevMatches})");
            }

            // (b) 미장착(및 held=[]로 favoredCat이 null로 귀결되는 장착 상태) 시 RNG 완전 미소비 직접
            // 증명 — `Shop.FavoredSymbol([])`은 모든 이모지 카운트가 0이라 항상 null을 반환한다(bestCount
            // 가 0을 못 넘음) — dev_major를 장착해도 held가 비어 있으면 favoredCat 자체가 null이라
            // PickPerksByTier의 favoredCat 분기가 진입조차 하지 않는다. 즉 "장착+held=[]"와 "미장착+
            // held=[]"는 RNG 소비량이 완전히 같아야 하고, 같은 시드라면 오퍼 결과도 완전히 같아야 한다.
            {
                for (long seed = 2_400_000L; seed < 2_400_000L + 40; seed++)
                {
                    var runDevEmpty = S4TestHelpers.NewRun(seed, deviceId: "dev_major");
                    runDevEmpty.Phase = RunPhase.NodeSelect;
                    runDevEmpty.Stage = 2;
                    runDevEmpty.NodeOptions.Add(NodeKind.Augment);
                    var evDevEmpty = NodeEvents.ChooseNode(runDevEmpty, 0, stat);

                    var runNoDevEmpty = S4TestHelpers.NewRun(seed);
                    runNoDevEmpty.Phase = RunPhase.NodeSelect;
                    runNoDevEmpty.Stage = 2;
                    runNoDevEmpty.NodeOptions.Add(NodeKind.Augment);
                    var evNoDevEmpty = NodeEvents.ChooseNode(runNoDevEmpty, 0, stat);

                    t.Eq(evNoDevEmpty[0].type, evDevEmpty[0].type, $"[dev_major:no-rng seed={seed}] 이벤트 타입 동일");
                    t.Eq(string.Join(",", runNoDevEmpty.PerkOfferIds), string.Join(",", runDevEmpty.PerkOfferIds),
                        $"[dev_major:no-rng seed={seed}] held=[]→favoredCat=null → dev_major 장착 여부 무관 오퍼 완전 동일(RNG 미소비 증명)");
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Opus 2차검수 필수④(WEB_PARITY_DESIGN.md §2-(U)) — 고정 시드 오퍼 골든을 "티어 순수성/
        // offerTierBumped 관측"만 하는 구조적 검증에서 실제 퍽 id 배열 하드코딩으로 교체한다. 값은
        // 이번 슬라이스가 완성한 알고리즘(①PERK_FAMILY 랭크 게이팅 ②티어 단계형 폴백 ③forceRare
        // SILVER→GOLD 한정 승급, heldPerk 분기 밖 이동 ④5% 시너지 picks.Count>=2 선검사)을 실제로
        // 실행해 재산출했다(EngineTests 실행 결과 그대로 전사 — 손계산이 아니라 "지금 알고리즘의 확정
        // 출력"을 고정한 회귀 앵커). 티어 결정 로직·family 게이팅·폴백 순서 중 하나라도 바뀌면 이
        // 7케이스 중 최소 하나는 값이 달라져 실패한다.
        // ══════════════════════════════════════════════════════════════════════
        private static void HardcodedGoldenCases(TestCtx t, IReadOnlyDictionary<string, long> stat)
        {
            // 케이스1 — 일반 SILVER 오퍼(held=[], stage=2→clearedStage=1→baseTier SILVER, 10%롤 미당첨).
            AssertOffer(t, stat, "gold1:silver-plain", seed: 4_000_000L, stage: 2, node: NodeKind.Augment,
                held: null, unluckyGauge: 0, prismInk: false,
                expectTier: Tier.SILVER, expectBumped: false,
                expectIds: new[] { "book_up", "fortune_check", "star_up" });

            // 케이스2 — GOLD 강제(stage=4→clearedStage=3→baseTier GOLD) + PERK_FAMILY 랭크게이팅
            // 실사용(held=[overheat_formula] → exp_g 랭크1 보유, 랭크2 "greed_calc"만 개방·랭크3/4 차단).
            AssertOffer(t, stat, "gold2:gold-family", seed: 4_100_000L, stage: 4, node: NodeKind.Augment,
                held: new[] { "overheat_formula" }, unluckyGauge: 0, prismInk: false,
                expectTier: Tier.GOLD, expectBumped: false,
                expectIds: new[] { "skull_study", "honor_student", "polish_work" });

            // 케이스3 — 티어 단계형 폴백(stage=4→baseTier GOLD, GOLD 89종 전량 보유로 소진 → SILVER로
            // 단계 하강). PRISM 폴백은 없음(GOLD→[SILVER]뿐) — 전원 SILVER여야 한다.
            {
                var allGold = Perks.Augments.Where(p => p.tier == Tier.GOLD).Select(p => p.id).ToArray();
                AssertOffer(t, stat, "gold3:fallback", seed: 4_200_001L, stage: 4, node: NodeKind.Augment,
                    held: allGold, unluckyGauge: 0, prismInk: false,
                    expectTier: Tier.SILVER, expectBumped: false,
                    expectIds: new[] { "study", "star_up", "review" });
            }

            // 케이스4 — forceRare(Opus 2차검수 필수②, Fable 결정): 같은 시드에서 게이지0(대조군, 10%롤
            // 미당첨 SILVER)과 게이지5(불운 만땅 → SILVER 노드만 GOLD로 승급)를 나란히 확인한다. 픽
            // 자체는 두 실행 모두 forceRare 판정 *이후* 동일 RNG 지점에서 시작하므로(TierUp은 RNG 미소비)
            // 달라지는 건 오직 후보 티어 풀뿐임을 이 대조로 직접 증명한다.
            AssertOffer(t, stat, "gold4:forceRare-ctrl", seed: 4_300_000L, stage: 2, node: NodeKind.Augment,
                held: null, unluckyGauge: 0, prismInk: false,
                expectTier: Tier.SILVER, expectBumped: false,
                expectIds: new[] { "review", "skull_watch", "cherry_up" });
            AssertOffer(t, stat, "gold4:forceRare-lucky", seed: 4_300_000L, stage: 2, node: NodeKind.Augment,
                held: null, unluckyGauge: 5, prismInk: false,
                expectTier: Tier.GOLD, expectBumped: true,
                expectIds: new[] { "gem_invest", "sacrifice", "insurance" }, expectGaugeReset: true);

            // 케이스5 — forceRare 범위 확정: GOLD 노드는 이미 "희귀 보장" 조건을 만족한 것으로 보고
            // 무승급(PRISM으로 더 승급하지 않는다). stage=4(baseTier GOLD)+게이지5, 10%롤 미당첨.
            AssertOffer(t, stat, "gold5:forceRare-gold-no-bump", seed: 4_500_000L, stage: 4, node: NodeKind.Augment,
                held: null, unluckyGauge: 5, prismInk: false,
                expectTier: Tier.GOLD, expectBumped: false,
                expectIds: new[] { "luck_accum", "early_adapt", "royal_decree" }, expectGaugeReset: true);

            // 케이스6 — forceRare 범위 확정: PRISM 노드(bossClear)도 무승급(이미 최고 티어).
            AssertOffer(t, stat, "gold6:forceRare-prism-no-bump", seed: 4_600_000L, stage: 6, node: NodeKind.Relic,
                held: null, unluckyGauge: 5, prismInk: false,
                expectTier: Tier.PRISM, expectBumped: false,
                expectIds: new[] { "prism_diploma", "set_resonator", "reapers_pact" }, expectGaugeReset: true);

            // 케이스7 — 🗂️보류파일(heldAug="preview", SILVER) 우선순위 회귀 검증(Opus 2차검수 필수①):
            // 불운게이지 만땅(gauge=5)이어도 보류 티어(SILVER)가 GOLD로 승급되면 안 된다 — 0번 칸이
            // 정확히 보류 퍽(preview) 그 자체여야 한다.
            {
                var run = S4TestHelpers.NewRun(4_400_000L);
                run.Phase = RunPhase.NodeSelect;
                run.Stage = 2;
                run.UnluckyGauge = 5;
                run.HeldAug = "preview";
                run.NodeOptions.Add(NodeKind.Augment);
                var ev = NodeEvents.ChooseNode(run, 0, stat);
                t.Eq("PERK_OFFER", ev[0].type, "[gold7:holdfile-priority] 오퍼 생성 성공");
                t.Eq(Tier.SILVER, ev[0].offerTier, "[gold7:holdfile-priority] 보류 티어(SILVER) 유지 — 불운게이지로 GOLD 승급 안 됨");
                t.True(!ev[0].offerTierBumped, "[gold7:holdfile-priority] 보류분은 등급업 표시도 안 함(결정형)");
                t.True(ev[0].offerHeldIncluded, "[gold7:holdfile-priority] offerHeldIncluded=true");
                t.Eq("preview,discount,review", string.Join(",", run.PerkOfferIds), "[gold7:holdfile-priority] 하드코딩 골든");
                t.Eq(0L, run.UnluckyGauge, "[gold7:holdfile-priority] 게이지는 heldPerk 분기라도 오퍼 발생 시 소모");
            }
        }

        private static void AssertOffer(
            TestCtx t, IReadOnlyDictionary<string, long> stat, string label, long seed, int stage, NodeKind node,
            IReadOnlyList<string> held, int unluckyGauge, bool prismInk,
            Tier expectTier, bool expectBumped, string[] expectIds, bool expectGaugeReset = false)
        {
            var run = S4TestHelpers.NewRun(seed);
            run.Phase = RunPhase.NodeSelect;
            run.Stage = stage;
            run.UnluckyGauge = unluckyGauge;
            run.PrismInkActive = prismInk;
            if (held != null) run.Perks.AddRange(held);
            run.NodeOptions.Add(node);

            var ev = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("PERK_OFFER", ev[0].type, $"[{label}] 오퍼 생성 성공");
            t.Eq(expectTier, ev[0].offerTier, $"[{label}] 오퍼 티어");
            t.Eq(expectBumped, ev[0].offerTierBumped, $"[{label}] offerTierBumped");
            t.Eq(string.Join(",", expectIds), string.Join(",", run.PerkOfferIds), $"[{label}] 하드코딩 골든 배열");
            if (expectGaugeReset) t.Eq(0L, run.UnluckyGauge, $"[{label}] 불운게이지 소모(오퍼 발생 시 항상 리셋)");
        }
    }

    // ── ③ retake_form의 RunCtx 반영 — bankrupt(파산 졸업생, ctx.coins 조건부 expMul) 캐릭터로
    // "재굴림 시점 ctx가 실제 run.Coins를 반영하는지"를 직접 증명한다. 웹 파리티 P3.5(§2-(T) 후속③).
    // res.mul(=최종 mods.expMul)·res.preMul(=expMul 적용 전 EXP)을 SpinResult가 그대로 노출하므로
    // RNG 결과를 직접 예측하지 않고도 정확한 어서션이 가능하다 — bankrupt 외 다른 expMul 관여 콘텐츠가
    // 전혀 없는 미니멀 런(퍽·저주·아이템·장치 없음, 머신 basic)이라 res.mul은 항상 1.5 또는 0.8이어야
    // 한다(cliffBurstExpMul/perfectShapeExpMul 등 나머지 배수 전부 1.0으로 무관여).
    internal static class Tests_P3_5_RetakeCtxPropagation
    {
        public static void Run(TestCtx t)
        {
            var stat = new Dictionary<string, long> { ["playerLevel"] = 20 };
            const long seed = 3_000_001L;

            var runBroke = MakeRun(seed, coins: 0);
            var runRich = MakeRun(seed, coins: 20);

            var evBroke = ItemUse.Use(runBroke, "retake_form", stat);
            var evRich = ItemUse.Use(runRich, "retake_form", stat);

            t.Eq("ITEM_USED", evBroke[0].type, "[retake-ctx:broke] 사용 성공");
            t.Eq("ITEM_USED", evRich[0].type, "[retake-ctx:rich] 사용 성공");

            var resBroke = evBroke[0].spin.result;
            var resRich = evRich[0].spin.result;

            // 웹 파리티 P3.5 핵심 어서션 — ctx.coins가 실제 run.Coins를 반영해야 두 값이 갈린다.
            // (수정 전 버그: ctx가 항상 기본값(coins=99, ">=10" 분기)이라 run.Coins==0 이어도 늘 ×0.8
            // 이었다 — 즉 resBroke.mul이 1.5가 아니라 0.8로 나왔을 것이다.)
            t.EqTol(1.5, resBroke.mul, "[retake-ctx:broke] coins=0 → bankrupt expMul ×1.5(ctx 반영 확인)");
            t.EqTol(0.8, resRich.mul, "[retake-ctx:rich] coins=20(>=10) → bankrupt expMul ×0.8");

            // 롤 자체(배수 적용 전 EXP)는 coins와 무관 — 같은 시드라 preMul이 완전히 같아야 공정한 대조.
            t.Eq(resRich.preMul, resBroke.preMul,
                "[retake-ctx] 동일 시드 → preMul(배수 적용 전 EXP) 동일 — coins는 expMul에만 영향");

            // preMul>0인 표본에서만 최종 exp 비율 확인(0이면 배수와 무관하게 둘 다 0이라 비율 무의미).
            if (resBroke.preMul > 0)
                t.True(resBroke.exp > resRich.exp,
                    $"[retake-ctx] preMul>0이면 coins=0(×1.5) 최종 exp가 coins=20(×0.8)보다 큼({resBroke.exp} > {resRich.exp})");
        }

        private static RunState MakeRun(long seed, long coins)
        {
            var run = new RunState(seed) { CharId = "bankrupt", MachineId = "basic", Device = "", Phase = RunPhase.Spin };
            run.LastCells.AddRange(new[] { "cherry", "book", "star", "gem", "crown" });
            run.LastSpinNo = 0;
            run.SpinIndex = 1;
            run.StageExp = 50;
            run.Score = 10;
            run.Coins = coins;
            run.LastGain = 10;
            run.LastScoreGain = 3;
            run.LastCoinGain = 0;
            run.Items.Add("retake_form");
            return run;
        }
    }
}
