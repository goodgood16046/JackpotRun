using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // P7-2 골든 테스트 — 심화모드 2/4 슬라이스, WEB_PARITY_DESIGN.md §1-A #19(심볼증강21·심볼유물15·
    // 정비소11·전공아키타입6) + §2-(AA) 선행 blocker(장치/아이템 경로 DeepMode 적용 + LockedNext
    // 센티널 왕복). 범위: Content/SymPerks.cs(데이터+symPerkMods 집계)·Content/Archetypes.cs·
    // Content/RepairServices.cs·Run/RepairShop.cs·Run/DeepRunHooks.cs(DeepPenalty 전체식·
    // BuildPouchBias·ApplyDeepMods·CheckArchetypeChange)·Run/SpinResolver.cs(RollCells/RollCellOne·
    // CellsFromIds 안전 왕복·Evaluate 아키타입 곱)만 검증한다 — 잭팟태그 실제발동/피버/오퍼(POUCH
    // 2-step)/UI 보드는 P7-3/P7-4(범위 밖).
    internal static class Tests_P7_2_SymPerks
    {
        public static void Run(TestCtx t)
        {
            SymAugmentsGolden(t);
            SymRelicsGolden(t);
            SymAugLevelsGolden(t);
            SymPerkModsRepairMulStacking(t);
            SymPerkModsTagBuffStacking(t);
            SymPerkModsPenaltyMulThreshold(t);
            SymPerkModsAddBoostAndLegendSeal(t);
            SymPerkModsLeveledPerk(t);
            SymPerkModsIgnoresNonSymIds(t);

            ArchetypeThresholdsAndTiebreak(t);
            ArchetypeModsBonusValues(t);
            EvaluateArchetypeFamilyExpMul(t);
            EvaluateArchetypeUpgradedFamilyExpMul(t);
            EvaluateArchetypeScoreMul(t);
            EvaluateArchetypeCoinMul(t);
            EvaluateArchetypeSkullBranchExpMul(t);
            DeepPenaltyFullFormula(t);

            RepairPhaseGate(t);
            RepairAddBasicSuccessAndPity(t);
            RepairAddHighSuccessAndPity(t);
            RepairAddRareSuccessAndPity(t);
            RepairRemoveSuccessAndNoChange(t);
            RepairSwapWithSymPerkSideEffects(t);
            RepairUpgradeSuccessAndNonUpgradable(t);
            RepairPurifyWithSideEffects(t);
            SkullPenaltyMulFromSymPerkAppliesInEvaluate(t);
            RepairExpandCompressTagbuff(t);
            TagbuffPurchaseAffectsEvaluate(t);
            RepairCurseCleanse(t);
            RepairInsufficientCoinsRejectsWithoutCharge(t);
            RepairInvalidPouchRejectsWithoutCharge(t);
            RepairPriceDiscount(t);
            RepairTriggersArchetypeChangeEvent(t);
            CheckArchetypeChangeResetsOnEmptyPouch(t);

            BlockerCellsFromIdsRoundTrip(t);
            BlockerPeekThenSpinKeeps5Cells(t);
            BlockerPeekUsesPouchOnly(t);
            BlockerManipRerollUsesPouchOnly(t);
            BlockerGamblerRerollUsesPouchOnly(t);
            BlockerRetakeFormUsesPouchOnly(t);

            DeepAutoplaySmokeWithRepairs(t);
        }

        // ══════════════════════════════════════════════════════════════════
        // 심볼증강 21 + 심볼유물 15 골든 (Symbols.cs를 보지 않고 data.js를 다시 읽어 옮긴 독립 대조축)
        // ══════════════════════════════════════════════════════════════════
        private static void SymAugmentsGolden(TestCtx t)
        {
            t.Eq(21, SymPerks.Augments.Length, "SYM_AUGMENTS 21종");
            t.Eq(6, SymPerks.Augments.Count(p => p.tier == "SILVER"), "실버 6종");
            t.Eq(7, SymPerks.Augments.Count(p => p.tier == "GOLD"), "골드 7종");
            t.Eq(8, SymPerks.Augments.Count(p => p.tier == "PRISM"), "프리즘 8종");
            t.Eq(21, SymPerks.Augments.Select(p => p.id).Distinct().Count(), "id 전부 유일");

            AssertAug(t, "sa_tidy", "🧹", "심볼정리", "SILVER", "repairMul");
            AssertAug(t, "sa_basic_research", "🌱", "기본연구", "SILVER", "addBoost");
            AssertAug(t, "sa_tag_sense", "🏷️", "태그감각", "SILVER", "tagBuff");
            AssertAug(t, "sa_safe_compress", "🛟", "안전압축", "SILVER", "penaltyMul");
            AssertAug(t, "sa_use_empty", "▫", "빈칸활용", "SILVER", "emptyScore");
            AssertAug(t, "sa_purify_class", "🕊️", "정화수업", "SILVER", "repairMul");
            AssertAug(t, "sa_compress", "🗜️", "심볼압축", "GOLD", "penaltyMul");
            AssertAug(t, "sa_tag_major", "🎓", "태그전공", "GOLD", "tagBuff");
            AssertAug(t, "sa_lab_regular", "🔬", "연구실단골", "GOLD", "repairMul");
            AssertAug(t, "sa_rare_research", "🔮", "희귀연구", "GOLD", "addBoost");
            AssertAug(t, "sa_swap_expert", "🔁", "교체전문가", "GOLD", "swapCoin");
            AssertAug(t, "sa_expand_build", "📦", "확장빌드", "GOLD", "skullPenaltyMul");
            AssertAug(t, "sa_purify_expert", "✨", "정화전문가", "GOLD", "purifyToBasic");
            AssertAug(t, "sp_deckslot", "🎰", "덱빌딩슬롯", "PRISM", "shopLab");
            AssertAug(t, "sp_compress_major", "🗜️", "압축전공", "PRISM", "boundOverride");
            AssertAug(t, "sp_expand_major", "📦", "확장전공", "PRISM", "boundOverride");
            AssertAug(t, "sp_solo_major", "🎯", "단일전공", "PRISM", "tagBuff");
            AssertAug(t, "sp_rare_hunt", "🏹", "희귀사냥", "PRISM", "addBoost");
            AssertAug(t, "sp_lab_addict", "⚗️", "연구실중독", "PRISM", "shopLab");
            AssertAug(t, "sp_legend_collect", "👑", "전설수집", "PRISM", "addBoost");
            AssertAug(t, "sp_purified_world", "🕊️", "정화된세계", "PRISM", "purifyToBasic");

            // 수치 스팟체크(대표 5개) — 웹 data.js eff 그대로.
            t.EqTol(0.9, SymPerks.Get("sa_tidy").eff.Mul.Value, "sa_tidy.eff.mul");
            TestHelpers.CollectionEq(t, new[] { "remove" }, SymPerks.Get("sa_tidy").eff.Kinds, "sa_tidy.eff.kinds");
            t.EqTol(1.15, SymPerks.Get("sp_deckslot").eff.QuotaMul.Value, "sp_deckslot.eff.quotaMul");
            t.Eq(true, SymPerks.Get("sp_deckslot").eff.AlwaysRepair.Value, "sp_deckslot.eff.alwaysRepair");
            t.Eq(21, SymPerks.Get("sp_compress_major").eff.Min.Value, "sp_compress_major.eff.min");
            t.EqTol(27, SymPerks.Get("sp_compress_major").eff.ExtraPenaltyLe.Value, "sp_compress_major.eff.extraPenaltyLe");
            t.EqTol(1.10, SymPerks.Get("sp_compress_major").eff.ExtraMul.Value, "sp_compress_major.eff.extraMul");
            t.Eq(48, SymPerks.Get("sp_expand_major").eff.Max.Value, "sp_expand_major.eff.max");
            t.EqTol(0.90, SymPerks.Get("sp_solo_major").eff.TagCap.Value, "sp_solo_major.eff.tagCap");
        }

        private static void AssertAug(TestCtx t, string id, string emoji, string name, string tier, string hook)
        {
            var p = SymPerks.Get(id);
            t.True(p != null, $"[aug] {id} 존재");
            t.Eq(emoji, p.emoji, $"[aug] {id}.emoji");
            t.Eq(name, p.name, $"[aug] {id}.name");
            t.Eq(tier, p.tier, $"[aug] {id}.tier");
            t.Eq(hook, p.eff.Hook, $"[aug] {id}.eff.hook");
        }

        private static void SymRelicsGolden(TestCtx t)
        {
            t.Eq(15, SymPerks.Relics.Length, "SYM_RELICS 15종");
            t.Eq(7, SymPerks.Relics.Count(p => p.tier == "SILVER"), "실버 7종");
            t.Eq(8, SymPerks.Relics.Count(p => p.tier == "GOLD"), "골드 8종");
            t.True(SymPerks.Relics.All(p => p.tier != "PRISM"), "유물엔 프리즘 없음");
            t.Eq(15, SymPerks.Relics.Select(p => p.id).Distinct().Count(), "id 전부 유일");

            AssertRel(t, "sr_sorter", "🗂️", "심볼분류함", "SILVER", "rewardBonus");
            AssertRel(t, "sr_shredder", "🪓", "낡은파쇄기", "GOLD", "autoShred");
            AssertRel(t, "sr_notebook", "📓", "연구노트", "SILVER", "repairMul");
            AssertRel(t, "sr_compass", "🧭", "태그나침반", "GOLD", "tagBuff");
            AssertRel(t, "sr_scale", "⚖️", "균형저울", "GOLD", "score");
            AssertRel(t, "sr_brush", "🖌️", "정화붓", "SILVER", "purifyCoin");
            AssertRel(t, "sr_empty_manual", "📄", "빈칸설명서", "SILVER", "emptyExp");
            AssertRel(t, "sr_rare_case", "🧰", "희귀표본상자", "GOLD", "rareFirstScore");
            AssertRel(t, "sr_legend_seal", "🔏", "전설봉인함", "GOLD", "legendSeal");
            AssertRel(t, "sr_copier", "📑", "심볼복사판", "GOLD", "bossCopy");
            AssertRel(t, "sr_lab_key", "🗝", "연구실열쇠", "SILVER", "shopLab");
            AssertRel(t, "sr_compress_contract", "📜", "압축계약서", "GOLD", "compressScore");
            AssertRel(t, "sr_big_bag", "🎒", "확장가방", "GOLD", "boundDelta");
            AssertRel(t, "sr_swap_receipt", "🧾", "교체영수증", "SILVER", "repairRefund");
            AssertRel(t, "sr_stat_table", "📊", "낡은통계표", "SILVER", "display");

            t.Eq(6, SymPerks.Get("sr_big_bag").eff.MaxDelta.Value, "sr_big_bag.eff.maxDelta");
            t.EqTol(0.8, SymPerks.Get("sr_big_bag").eff.SkullPenaltyMul.Value, "sr_big_bag.eff.skullPenaltyMul");
            t.EqTol(36, SymPerks.Get("sr_big_bag").eff.WhenTotalGe.Value, "sr_big_bag.eff.whenTotalGe");
            t.EqTol(0.25, SymPerks.Get("sr_swap_receipt").eff.Frac.Value, "sr_swap_receipt.eff.frac");
        }

        private static void AssertRel(TestCtx t, string id, string emoji, string name, string tier, string hook)
        {
            var p = SymPerks.Get(id);
            t.True(p != null, $"[rel] {id} 존재");
            t.Eq(emoji, p.emoji, $"[rel] {id}.emoji");
            t.Eq(name, p.name, $"[rel] {id}.name");
            t.Eq(tier, p.tier, $"[rel] {id}.tier");
            t.Eq(hook, p.eff.Hook, $"[rel] {id}.eff.hook");
        }

        private static void SymAugLevelsGolden(TestCtx t)
        {
            var expected = new[]
            {
                "sa_tag_sense", "sa_use_empty", "sa_basic_research", "sa_tag_major",
                "sa_rare_research", "sa_compress", "sa_expand_build", "sp_solo_major",
            };
            t.Eq(8, SymPerks.AugLevels.Count, "SYM_AUG_LEVELS 8종");
            foreach (var id in expected) t.True(SymPerks.IsLevelable(id), $"[auglvl] {id} 레벨업 가능");
            t.True(!SymPerks.IsLevelable("sa_tidy"), "[auglvl] 미등록 심볼증강은 레벨업 불가");
            t.True(!SymPerks.IsLevelable("sr_sorter"), "[auglvl] 유물은 전부 레벨업 불가");

            t.EqTol(0.03, SymPerks.AugLevels["sa_tag_sense"][2].Pct.Value, "sa_tag_sense Lv2 pct");
            t.EqTol(0.03, SymPerks.AugLevels["sa_tag_sense"][3].Pct.Value, "sa_tag_sense Lv3 pct");
            t.Eq(10, SymPerks.AugLevels["sa_use_empty"][2].Score.Value, "sa_use_empty Lv2 score");
            t.Eq(1, SymPerks.AugLevels["sa_basic_research"][2].Basic.Value, "sa_basic_research Lv2 basic");
            t.EqTol(0.05, SymPerks.AugLevels["sa_tag_major"][2].Pct.Value, "sa_tag_major Lv2 pct");
            t.EqTol(0.20, SymPerks.AugLevels["sa_rare_research"][2].RareChance.Value, "sa_rare_research Lv2 rareChance");
            t.EqTol(0.30, SymPerks.AugLevels["sa_rare_research"][3].RareChance.Value, "sa_rare_research Lv3 rareChance");
            t.EqTol(0.05, SymPerks.AugLevels["sa_compress"][2].MulSub.Value, "sa_compress Lv2 mulSub");
            t.EqTol(0.05, SymPerks.AugLevels["sa_expand_build"][3].MulSub.Value, "sa_expand_build Lv3 mulSub");
            t.EqTol(0.05, SymPerks.AugLevels["sp_solo_major"][2].Pct.Value, "sp_solo_major Lv2 pct");
        }

        // ══════════════════════════════════════════════════════════════════
        // symPerkMods 집계 — 대표 조합 손계산(웹 engine.js symPerkMods 그대로 재현했는지 검증)
        // ══════════════════════════════════════════════════════════════════
        private static void SymPerkModsRepairMulStacking(TestCtx t)
        {
            var perks = new List<string> { "sa_tidy", "sa_purify_class", "sa_lab_regular" };
            var sp = SymPerks.ComputeMods(perks, new Dictionary<string, int>(), null);
            t.EqTol(0.9, sp.RepairMul["remove"], "[symPerkMods] remove == 0.9(sa_tidy만)");
            t.EqTol(0.8, sp.RepairMul["purify"], "[symPerkMods] purify == 0.8(sa_purify_class만)");
            t.EqTol(0.7, sp.RepairMul["*"], "[symPerkMods] * == 0.7(sa_lab_regular)");

            var run = MakeDeepRun(90001L);
            run.Perks.AddRange(perks);
            t.EqTol(0.9 * 0.7, RepairShop.PriceMul(run, "remove"), "[RepairShop] remove 가격배수 = *(0.7)×remove(0.9)");
            t.EqTol(0.8 * 0.7, RepairShop.PriceMul(run, "purify"), "[RepairShop] purify 가격배수 = *(0.7)×purify(0.8)");
            t.EqTol(0.7, RepairShop.PriceMul(run, "upgrade"), "[RepairShop] upgrade 가격배수 = *(0.7)만(전용 할인 없음)");
        }

        private static void SymPerkModsTagBuffStacking(TestCtx t)
        {
            // pouch: 생명(cherry)5·학습(book)10·코인(coin)3 -> mostCommonTag == "학습"(10, 최댓값).
            var pouch = new Dictionary<string, int> { { "cherry", 5 }, { "book", 10 }, { "coin", 3 } };
            t.Eq("학습", Pouch.MostCommonTag(pouch), "[pouch] mostCommonTag == 학습");

            var perks = new List<string> { "sa_tag_sense", "sa_tag_major", "sp_solo_major" };
            var sp = SymPerks.ComputeMods(perks, pouch, null);
            t.EqTol(0.05 + 0.20 + 0.30, sp.DeepTagMul["학습"], "[symPerkMods] 최다태그(학습) 누적 = 0.05+0.20+0.30");
            t.EqTol(-0.05 - 0.30, sp.DeepTagMul["생명"], "[symPerkMods] 타태그(생명) 누적 = -0.05-0.30");
            t.EqTol(-0.05 - 0.30, sp.DeepTagMul["코인"], "[symPerkMods] 타태그(코인) 누적 = -0.05-0.30");
            t.EqTol(0.90, sp.TagCap, "[symPerkMods] tagCap == 0.90(sp_solo_major)");
        }

        private static void SymPerkModsPenaltyMulThreshold(TestCtx t)
        {
            var perks = new List<string> { "sa_safe_compress", "sa_compress" }; // whenTotalLe 28 / 27
            var pouch28 = new Dictionary<string, int> { { "cherry", 28 } };
            var sp28 = SymPerks.ComputeMods(perks, pouch28, null);
            t.EqTol(0.6, sp28.PenaltyMul, "[symPerkMods] total=28 -> sa_safe_compress만 적용(0.6)");

            var pouch27 = new Dictionary<string, int> { { "cherry", 27 } };
            var sp27 = SymPerks.ComputeMods(perks, pouch27, null);
            t.EqTol(0.6 * 0.5, sp27.PenaltyMul, "[symPerkMods] total=27 -> 둘 다 적용(0.6×0.5)");
        }

        private static void SymPerkModsAddBoostAndLegendSeal(TestCtx t)
        {
            var perks = new List<string> { "sp_legend_collect", "sr_legend_seal" };
            var sp = SymPerks.ComputeMods(perks, new Dictionary<string, int>(), null);
            t.Eq(1, sp.LegendWeight, "[symPerkMods] legendWeight == 1");
            t.EqTol(1.20, sp.BossQuotaMul, "[symPerkMods] bossQuotaMul == 1.20");
            t.True(sp.LegendSeal, "[symPerkMods] legendSeal == true");
        }

        private static void SymPerkModsLeveledPerk(TestCtx t)
        {
            var perks = new List<string> { "sa_tag_sense" };
            var pouch = new Dictionary<string, int> { { "cherry", 10 } }; // mostCommonTag == 생명
            var levels = new Dictionary<string, int> { { "sa_tag_sense", 3 } };
            var sp = SymPerks.ComputeMods(perks, pouch, levels);
            t.EqTol(0.05 + 0.03 + 0.03, sp.DeepTagMul["생명"], "[symPerkMods] sa_tag_sense Lv3 == 0.05+0.03+0.03");

            var spLv1 = SymPerks.ComputeMods(perks, pouch, null); // levels 생략 -> Lv1(원본)
            t.EqTol(0.05, spLv1.DeepTagMul["생명"], "[symPerkMods] levels 생략 -> Lv1(가산 없음)");
        }

        // perkIds에 일반 증강/유물/저주 id가 섞여도 symPerkMods는 무시(SYM_PERK_BY_ID 미등록) — 웹과
        // 동일 구조라 RunState.Perks/PerkLevels를 그대로 공유해도 안전함을 확인.
        private static void SymPerkModsIgnoresNonSymIds(TestCtx t)
        {
            var perks = new List<string> { "sa_tidy", "overdrive", "fate_bell", "hex_allornothing" };
            var sp = SymPerks.ComputeMods(perks, new Dictionary<string, int>(), null);
            t.EqTol(0.9, sp.RepairMul["remove"], "[symPerkMods] 일반 증강/저주 id 섞여도 sa_tidy만 반영");
            t.Eq(1, sp.RepairMul.Count, "[symPerkMods] repairMul 키 1개만(다른 일반 id는 완전 무시)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 계열 아키타입(전공) — 임계·단일전공·보너스 수치
        // ══════════════════════════════════════════════════════════════════
        private static void ArchetypeThresholdsAndTiebreak(TestCtx t)
        {
            var empty = Archetypes.PouchArchetype(new Dictionary<string, int>());
            t.True(empty.Family == null, "[arch] 빈 주머니 -> family=null");
            t.Eq(0, empty.Tier, "[arch] 빈 주머니 -> tier=0");

            // share 정확히 0.25(경계, >= 포함) -> tier1. 필러는 "star"(tags=["콤보"]) — "book"을 쓰면
            // book이 "tag:학습" 계열로도 집계돼(TagCounts) 의도와 달리 book 계열이 최다가 돼버린다
            // (book 아키타입 ref="tag:학습"이라 familyCount가 순수 id 매칭이 아니라 태그 합산이라는
            // 점을 테스트 픽스처에도 반영해야 함 — Content/Archetypes.cs ARCHETYPES 선언 참조).
            var atT1 = new Dictionary<string, int> { { "cherry", 25 }, { "star", 75 } };
            var archT1 = Archetypes.PouchArchetype(atT1);
            t.Eq("cherry", archT1.Family, "[arch] share==0.25 -> cherry 활성");
            t.EqTol(0.25, archT1.Share, "[arch] share==0.25");
            t.Eq(1, archT1.Tier, "[arch] share==0.25 -> tier1(>=)");

            var belowT1 = new Dictionary<string, int> { { "cherry", 24 }, { "star", 76 } };
            var archBelow = Archetypes.PouchArchetype(belowT1);
            t.Eq(0, archBelow.Tier, "[arch] share==0.24 -> tier0(미달)");

            // share 정확히 0.40(경계) -> tier2.
            var atT2 = new Dictionary<string, int> { { "cherry", 40 }, { "star", 60 } };
            var archT2 = Archetypes.PouchArchetype(atT2);
            t.Eq("cherry", archT2.Family, "[arch] share==0.40 -> cherry 활성");
            t.Eq(2, archT2.Tier, "[arch] share==0.40 -> tier2(>=)");

            // 동률 -> 선언 순서(cherry > book) 우선.
            var tie = new Dictionary<string, int> { { "cherry", 10 }, { "star", 10 } };
            var archTie = Archetypes.PouchArchetype(tie);
            t.Eq("cherry", archTie.Family, "[arch] 동률(0.5=0.5) -> 선언순서 우선(cherry)");

            // book 계열은 tag:학습(book+tome 동치) — cherry를 낮게 둬 book이 명확히 최다가 되도록.
            var bookFam = new Dictionary<string, int> { { "book", 5 }, { "tome", 20 }, { "cherry", 10 } };
            var archBook = Archetypes.PouchArchetype(bookFam);
            t.Eq("book", archBook.Family, "[arch] book 계열(book+tome 합산=25/35≈0.71) 최다(cherry 10/35≈0.29)");
        }

        private static void ArchetypeModsBonusValues(TestCtx t)
        {
            var cherryT1 = new ArchetypeState { Family = "cherry", Tier = 1, Metric = "exp" };
            var m1 = Archetypes.ArchetypeMods(cherryT1);
            t.EqTol(0.15, m1.ExpMul["cherry"], "[archMods] exp t1 == +15%");
            t.Eq(0, m1.ScoreMul.Count, "[archMods] exp계열은 scoreMul 없음");
            t.EqTol(1.0, m1.SkullPenaltyMul, "[archMods] cherry는 skullPenaltyMul 무관");

            var cherryT2 = new ArchetypeState { Family = "cherry", Tier = 2, Metric = "exp" };
            t.EqTol(0.30, Archetypes.ArchetypeMods(cherryT2).ExpMul["cherry"], "[archMods] exp t2 == +30%");

            var gemT1 = new ArchetypeState { Family = "gem", Tier = 1, Metric = "score" };
            t.EqTol(0.15, Archetypes.ArchetypeMods(gemT1).ScoreMul["gem"], "[archMods] score t1 == +15%");
            var gemT2 = new ArchetypeState { Family = "gem", Tier = 2, Metric = "score" };
            t.EqTol(0.30, Archetypes.ArchetypeMods(gemT2).ScoreMul["gem"], "[archMods] score t2 == +30%");

            var coinT1 = new ArchetypeState { Family = "coin", Tier = 1, Metric = "coin" };
            t.EqTol(0.10, Archetypes.ArchetypeMods(coinT1).CoinMul["coin"], "[archMods] coin t1 == +10%");
            var coinT2 = new ArchetypeState { Family = "coin", Tier = 2, Metric = "coin" };
            t.EqTol(0.20, Archetypes.ArchetypeMods(coinT2).CoinMul["coin"], "[archMods] coin t2 == +20%");

            var skullT1 = new ArchetypeState { Family = "skull", Tier = 1, Metric = "exp" };
            t.EqTol(1.0, Archetypes.ArchetypeMods(skullT1).SkullPenaltyMul, "[archMods] 강령학파 t1은 skullPenaltyMul 무영향");
            var skullT2 = new ArchetypeState { Family = "skull", Tier = 2, Metric = "exp" };
            var msk = Archetypes.ArchetypeMods(skullT2);
            t.EqTol(0.30, msk.ExpMul["skull"], "[archMods] 강령학파 t2도 exp +30%");
            t.EqTol(0.5, msk.SkullPenaltyMul, "[archMods] 강령학파 t2 -> skullPenaltyMul 0.5 병행");

            var inactive = new ArchetypeState { Family = "cherry", Tier = 0, Metric = "exp" };
            var mInactive = Archetypes.ArchetypeMods(inactive);
            t.Eq(0, mInactive.ExpMul.Count, "[archMods] tier0(비활성) -> 빈 맵");
            t.EqTol(1.0, mInactive.SkullPenaltyMul, "[archMods] tier0 -> skullPenaltyMul 1.0");
        }

        // ── SpinResolver.Evaluate 아키타입 곱 반영 지점 — 손계산 대조(rng는 DICE 없어 미소비) ──────
        private static Cell C(string id) => new Cell(Symbols.ById(id));

        private static void EvaluateArchetypeFamilyExpMul(TestCtx t)
        {
            // cherry(3)+cherry_ripe(6)+book(6)+star(8)+gem(1) = 24. cherry_ripe는 UpgradeParent로
            // "cherry" 계열에 편입(웹 famBase) — 직접 base id와 상위계열 id 둘 다 곱을 받는지 확인.
            var raw = new List<Cell> { C("cherry"), C("cherry_ripe"), C("book"), C("star"), C("gem") };
            var rng = new Rng(1);

            var baseline = SpinResolver.Evaluate(rng, raw, new Mods(), 1, 5, false);
            t.Eq(24L, baseline.exp, "[evaluate-arch] 아키타입 없음 -> exp=24");

            var mods = new Mods();
            mods.deepFamilyExpMul["cherry"] = 0.30; // t2
            var withArch = SpinResolver.Evaluate(rng, raw, mods, 1, 5, false);
            // cherry: 3*1.3=3.9, cherry_ripe: 6*1.3=7.8, 나머지 불변 -> 3.9+7.8+6+8+1=26.7 -> (long)26.
            t.Eq(26L, withArch.exp, "[evaluate-arch] cherry 계열(base+상위) 30% 반영 -> exp=26");
        }

        private static void EvaluateArchetypeUpgradedFamilyExpMul(TestCtx t)
        {
            // 상위계열 id만 있어도(base 없이) 곱이 적용되는지 별도 확인 — cherry_ripe만 단독.
            // magnet/bomb 등 special 심볼은 Evaluate 앞단(폭탄제거/자석복사)에서 셀 자체를 바꿔버려
            // 손계산을 오염시키므로 피하고, special=NONE인 값심볼(crown)로 채운다.
            var raw = new List<Cell> { C("cherry_ripe"), C("book"), C("star"), C("gem"), C("crown") };
            var rng = new Rng(2);
            var mods = new Mods();
            mods.deepFamilyExpMul["cherry"] = 0.15; // t1
            var res = SpinResolver.Evaluate(rng, raw, mods, 1, 5, false);
            // cherry_ripe: 6*1.15=6.9, book6+star8+gem1+crown20 = 35. 총 6.9+35=41.9 -> (long)41.
            t.Eq(41L, res.exp, "[evaluate-arch] cherry_ripe 단독도 cherry 계열 곱 적용");
        }

        private static void EvaluateArchetypeScoreMul(TestCtx t)
        {
            var raw = new List<Cell> { C("cherry"), C("book"), C("star"), C("gem"), C("crown") };
            var rng = new Rng(3);
            var baseline = SpinResolver.Evaluate(rng, raw, new Mods(), 1, 5, false);
            t.Eq(65L, baseline.score, "[evaluate-arch] 아키타입 없음 -> score=0+0+0+15+50=65");

            var mods = new Mods();
            mods.deepFamilyScoreMul["gem"] = 0.30; // t2 보석상
            var res = SpinResolver.Evaluate(rng, raw, mods, 1, 5, false);
            // gem: 15*1.3=19.5, crown50 불변 -> 19.5+50=69.5 -> (long)69.
            t.Eq(69L, res.score, "[evaluate-arch] gem 계열 30% -> score=69");
        }

        private static void EvaluateArchetypeCoinMul(TestCtx t)
        {
            var raw = new List<Cell> { C("coin_bag"), C("coin_bag"), C("coin_bag"), C("coin_bag"), C("coin_bag") };
            var rng = new Rng(4);
            var baseline = SpinResolver.Evaluate(rng, raw, new Mods(), 1, 5, false);
            t.Eq(15, baseline.coins, "[evaluate-arch] 아키타입 없음 -> coins=5×3=15");

            var mods = new Mods();
            mods.deepFamilyCoinMul["coin"] = 0.10; // t1 조폐국(coin_bag은 UpgradeParent로 coin 계열)
            var res = SpinResolver.Evaluate(rng, raw, mods, 1, 5, false);
            // 5 × (3×1.10) = 16.5 -> (int)16.
            t.Eq(16, res.coins, "[evaluate-arch] coin 계열 10% -> coins=16");
        }

        private static void EvaluateArchetypeSkullBranchExpMul(TestCtx t)
        {
            var raw = new List<Cell> { C("skull"), C("skull"), C("cherry"), C("book"), C("star") };
            var rng = new Rng(5);
            var modsBase = new Mods { skullExp = 10 }; // 해골빌드(스컬 EXP 가산 활성 -> 보너스 분기)
            var baseline = SpinResolver.Evaluate(rng, raw, modsBase, 1, 5, false);
            // cherry3+book6+star8 + skull(se=10)×2 = 17+20 = 37. skullBonusPer=10>0 -> 페널티 없음.
            t.Eq(37L, baseline.exp, "[evaluate-arch] 해골빌드(아키타입 없음) -> exp=37");

            var modsArch = new Mods { skullExp = 10 };
            modsArch.deepFamilyExpMul["skull"] = 0.30; // t2 강령학파
            var res = SpinResolver.Evaluate(rng, raw, modsArch, 1, 5, false);
            // skull: se=10×1.3=13, ×2=26. 17+26=43.
            t.Eq(43L, res.exp, "[evaluate-arch] 강령학파 t2 -> 해골 EXP 가산분에도 30% 적용, exp=43");
        }

        private static void DeepPenaltyFullFormula(TestCtx t)
        {
            var run = MakeDeepRun(90101L);
            run.Stage = 5; // 보스 스테이지(stage%5==0)
            run.Perks.AddRange(new[] { "sp_legend_collect", "sr_legend_seal" });
            // 시작덱 total=30 -> compressionPenalty=1.0, deepCompressExtra=0 -> base=1.0.
            // sp.penaltyMul=1(둘 다 penaltyMul 무관) -> mul=max(1,1+(1-1)*1)=1.
            // boss=true, sp.bossQuotaMul=1.20 != 1 -> legendSeal true -> bq=1+(1.20-1)*0.75=1.15 -> mul=1.15.
            // EarlyQuotaMul(5)=1.0 -> 최종 1.15.
            t.EqTol(1.15, DeepRunHooks.DeepPenalty(run), "[DeepPenalty] bossQuotaMul(1.20)+legendSeal(25%감쇄) 반영 = 1.15");
        }

        // ══════════════════════════════════════════════════════════════════
        // 정비소 11 서비스 — 실행(성공/거부/코인). Opus 2차검수 필수② — RepairShop.Execute는
        // RunPhase.EventShop에서만 통과한다(Shop.Buy와 동일 관례) — 모든 성공 케이스는
        // MakeDeepRunInShop(Phase.EventShop 세팅)을 쓴다.
        // ══════════════════════════════════════════════════════════════════
        private static void RepairPhaseGate(TestCtx t)
        {
            var run = MakeDeepRun(90200L); // MakeDeepRun 기본 Phase == Spin(§0 blocker 테스트들과 공유하는 관례).
            run.Coins = 100;
            var rejected = RunControllerRepairBuy(run, "sv_expand", null);
            t.Eq("REJECTED", rejected[0].type, "[repair:phase] Spin 단계 -> 거부(Shop.Buy와 동일 관례)");
            t.Eq("PHASE_NOT_SHOP", rejected[0].reason, "[repair:phase] 사유");
            t.Eq(100, run.Coins, "[repair:phase] 거부 시 코인 미차감");
            t.Eq(0, run.DeepTotalMaxDelta, "[repair:phase] 거부 시 상태 무변경");

            run.Phase = RunPhase.EventShop;
            var ok = RunControllerRepairBuy(run, "sv_expand", null);
            t.Eq("REPAIR_DONE", ok[0].type, "[repair:phase] EventShop 단계 -> 성공");
        }

        private static void RepairAddBasicSuccessAndPity(TestCtx t)
        {
            var run = MakeDeepRunInShop(90201L);
            run.Coins = 100;
            var events = RunControllerRepairBuy(run, "sv_add_basic", new RepairArgs { Id = "magnet" });
            t.Eq("REPAIR_DONE", events[0].type, "[repair:add_basic] 성공");
            t.Eq(6, run.Pouch["magnet"], "[repair:add_basic] magnet 1->6(+5)");
            t.Eq(92, run.Coins, "[repair:add_basic] 코인 100-8=92(할인 없음)");
            t.True(run.DeepPity != null && run.DeepPity.Id == "magnet", "[repair:add_basic] deepPity == magnet");

            // 대상 미지정 -> 변화 없음으로 거부(웹도 동일하게 알 수 없는 서비스로 폴백).
            var run2 = MakeDeepRunInShop(90202L);
            run2.Coins = 100;
            var rejected = RunControllerRepairBuy(run2, "sv_add_basic", new RepairArgs());
            t.Eq("REJECTED", rejected[0].type, "[repair:add_basic] 대상 없음 -> 거부");
            t.Eq(100, run2.Coins, "[repair:add_basic] 거부 시 코인 미차감");
        }

        // [권장④] sv_add_high/sv_add_rare 실행 테스트 — sv_add_basic과 동일 패턴, 대상 희귀도만 다름.
        private static void RepairAddHighSuccessAndPity(TestCtx t)
        {
            var run = MakeDeepRunInShop(90216L);
            run.Coins = 100;
            var ok = RunControllerRepairBuy(run, "sv_add_high", new RepairArgs { Id = "cherry_ripe" });
            t.Eq("REPAIR_DONE", ok[0].type, "[repair:add_high] 성공");
            t.Eq(3, run.Pouch["cherry_ripe"], "[repair:add_high] cherry_ripe 0->3(+3, 고급 희귀도)");
            t.Eq(85, run.Coins, "[repair:add_high] 코인 100-15=85(할인 없음)");
            t.True(run.DeepPity != null && run.DeepPity.Id == "cherry_ripe", "[repair:add_high] deepPity == cherry_ripe");
        }

        private static void RepairAddRareSuccessAndPity(TestCtx t)
        {
            var run = MakeDeepRunInShop(90217L);
            run.Coins = 100;
            var ok = RunControllerRepairBuy(run, "sv_add_rare", new RepairArgs { Id = "wild" });
            t.Eq("REPAIR_DONE", ok[0].type, "[repair:add_rare] 성공");
            t.Eq(1, run.Pouch["wild"], "[repair:add_rare] wild 0->1(+1, 희귀 희귀도)");
            t.Eq(70, run.Coins, "[repair:add_rare] 코인 100-30=70(할인 없음)");
            t.True(run.DeepPity != null && run.DeepPity.Id == "wild", "[repair:add_rare] deepPity == wild");
        }

        private static void RepairRemoveSuccessAndNoChange(TestCtx t)
        {
            var run = MakeDeepRunInShop(90203L);
            run.Coins = 100;
            var ok = RunControllerRepairBuy(run, "sv_remove", new RepairArgs { Id = "magnet" });
            t.Eq("REPAIR_DONE", ok[0].type, "[repair:remove] 성공(보유 1개만 있어도 있는 만큼만 제거)");
            t.True(!run.Pouch.ContainsKey("magnet"), "[repair:remove] magnet 완전 제거");
            t.Eq(88, run.Coins, "[repair:remove] 코인 100-12=88");

            var again = RunControllerRepairBuy(run, "sv_remove", new RepairArgs { Id = "magnet" });
            t.Eq("REJECTED", again[0].type, "[repair:remove] 이미 없는 대상 재시도 -> 거부(변화없음)");
            t.Eq("REPAIR_NO_CHANGE", again[0].reason, "[repair:remove] 사유");
            t.Eq(88, run.Coins, "[repair:remove] 거부 시 코인 미차감(2차)");
        }

        private static void RepairSwapWithSymPerkSideEffects(TestCtx t)
        {
            var run = MakeDeepRunInShop(90204L);
            run.Coins = 100;
            run.Perks.AddRange(new[] { "sa_swap_expert", "sr_swap_receipt" }); // +3코인 · 25% 환급
            var ok = RunControllerRepairBuy(run, "sv_swap", new RepairArgs { From = "cherry", To = "star" });
            t.Eq("REPAIR_DONE", ok[0].type, "[repair:swap] 성공");
            t.Eq(3, run.Pouch["cherry"], "[repair:swap] cherry 6->3");
            t.Eq(7, run.Pouch["star"], "[repair:swap] star 4->7");
            t.Eq(30, Pouch.Total(run.Pouch), "[repair:swap] 총량 유지(30)");
            // 가격 18(할인 없음) -> 100-18=82, +swapCoin(3)=85, +환급 round(18*0.25=4.5,AwayFromZero)=5 -> 90.
            t.Eq(90, run.Coins, "[repair:swap] 코인 100-18+3+5=90(교체전문가+교체영수증 병행)");
        }

        private static void RepairUpgradeSuccessAndNonUpgradable(TestCtx t)
        {
            var run = MakeDeepRunInShop(90205L);
            run.Coins = 100;
            var ok = RunControllerRepairBuy(run, "sv_upgrade", new RepairArgs { Id = "cherry" });
            t.Eq("REPAIR_DONE", ok[0].type, "[repair:upgrade] 성공");
            t.Eq(1, run.Pouch["cherry"], "[repair:upgrade] cherry 6->1(5 이동)");
            t.Eq(5, run.Pouch["cherry_ripe"], "[repair:upgrade] cherry_ripe 0->5");
            t.True(run.DeepPity != null && run.DeepPity.Id == "cherry_ripe", "[repair:upgrade] deepPity == cherry_ripe(상위계열)");

            var run2 = MakeDeepRunInShop(90206L);
            run2.Coins = 100;
            var rejected = RunControllerRepairBuy(run2, "sv_upgrade", new RepairArgs { Id = "star" }); // 업그레이드 매핑 없음
            t.Eq("REJECTED", rejected[0].type, "[repair:upgrade] 매핑 없는 대상 -> 거부(변화없음)");
            t.Eq(100, run2.Coins, "[repair:upgrade] 거부 시 코인 미차감");
        }

        private static void RepairPurifyWithSideEffects(TestCtx t)
        {
            var run = MakeDeepRunInShop(90207L);
            run.Coins = 100;
            run.Perks.AddRange(new[] { "sr_brush", "sa_purify_expert" }); // 정화붓+3코인 · 정화전문가(랜덤기본승격)
            var ok = RunControllerRepairBuy(run, "sv_purify", null);
            t.Eq("REPAIR_DONE", ok[0].type, "[repair:purify] 성공");
            t.True(!run.Pouch.ContainsKey("skull"), "[repair:purify] 해골 3->0 전부 정화");
            t.True(!run.Pouch.ContainsKey("empty"), "[repair:purify] 정화전문가가 빈칸을 전부 기본심볼로 재승격");
            t.Eq(30, Pouch.Total(run.Pouch), "[repair:purify] 총량 유지(30)");
            t.Eq(83, run.Coins, "[repair:purify] 코인 100-20(가격)+3(정화붓)=83");
        }

        // Opus 2차검수 필수① — 심볼퍽 skullPenaltyMul(sa_expand_build 0.7/sr_big_bag 0.8)이
        // DeepRunHooks.ApplyDeepMods를 거쳐 mods.skullPenaltyMul에 실제로 곱해지는지 손계산 확인.
        private static void SkullPenaltyMulFromSymPerkAppliesInEvaluate(TestCtx t)
        {
            var run = MakeDeepRun(90218L);
            run.Pouch.Clear();
            run.Pouch["cherry"] = 40; // 확장빌드 조건(총량>=33) 충족.
            run.Perks.Add("sa_expand_build"); // whenTotalGe:33, mul:0.7
            var mods = new Mods();
            DeepRunHooks.ApplyDeepMods(mods, run);
            t.EqTol(0.7, mods.skullPenaltyMul, "[symPerk:skullPenaltyMul] sa_expand_build(총량40>=33) -> mods.skullPenaltyMul=0.7");

            // 해골 3개 등장 스핀 손계산: 해골빌드 아님(skullExp=0)이라 페널티 분기 — Formulas.SKULL_PENALTY(3)
            // × skulls(3) × skullPenaltyMul(0.7) = 6.3 -> exp에서 차감(다른 항 없게 나머지 칸도 skull로 채움).
            var raw = new List<Cell> { C("skull"), C("skull"), C("skull"), C("skull"), C("skull") };
            var res = SpinResolver.Evaluate(new Rng(9), raw, mods, 1, 5, false);
            // 5개 전부 skull: se=mods.skullExp(0)+perSkullExp(0)=0 -> exp 가산 없음. skullBonusPer=0(>0 아님)
            // -> 페널티 분기: pen = 5 × 3 × 0.7 = 10.5 -> exp = 0 - 10.5 = -10.5 -> Max(.,0) = 0(음수 클램프).
            // 페널티 감쇄 효과를 수치로 확인하려면 클램프에 안 걸리는 조합이 필요 — cherry 1개 섞어 베이스 확보.
            var raw2 = new List<Cell> { C("skull"), C("skull"), C("skull"), C("skull"), C("cherry") };
            var res2 = SpinResolver.Evaluate(new Rng(9), raw2, mods, 1, 5, false);
            // cherry exp=3, skull 4개 페널티 = 4×3×0.7=8.4 -> exp = 3-8.4 = -5.4 -> Max(.,0)=0(여전히 클램프).
            // 감쇄가 실제로 수치를 바꾼다는 것은 skullPenaltyMul=1(perk 없음) 대비로 직접 대조한다.
            var modsNoPerk = new Mods();
            var runNoPerk = MakeDeepRun(90219L);
            runNoPerk.Pouch.Clear();
            runNoPerk.Pouch["cherry"] = 40; // perk 없음 -> skullPenaltyMul 여전히 1.0
            DeepRunHooks.ApplyDeepMods(modsNoPerk, runNoPerk);
            t.EqTol(1.0, modsNoPerk.skullPenaltyMul, "[symPerk:skullPenaltyMul] perk 없으면 1.0(대조군)");
            var resNoPerk = SpinResolver.Evaluate(new Rng(9), raw2, modsNoPerk, 1, 5, false);
            // pen = 4×3×1.0=12 -> exp = 3-12 = -9 -> Max(.,0)=0. 둘 다 0으로 클램프돼 exp만으로는 구분이
            // 안 되므로, 페널티 적용 "직전" 값을 검증하려면 클램프가 걸리지 않을 만큼 값심볼을 더 채운다.
            var raw3 = new List<Cell> { C("skull"), C("skull"), C("star"), C("gem"), C("crown") };
            // star8+gem1+crown20=29, skull 2개: perk 있으면 pen=2×3×0.7=4.2 -> exp=29-4.2=24.8 -> (long)24.
            // perk 없으면 pen=2×3×1.0=6 -> exp=29-6=23 -> (long)23.
            var resWithPerk = SpinResolver.Evaluate(new Rng(9), raw3, mods, 1, 5, false);
            var resNoPerk2 = SpinResolver.Evaluate(new Rng(9), raw3, modsNoPerk, 1, 5, false);
            t.Eq(24L, resWithPerk.exp, "[symPerk:skullPenaltyMul] sa_expand_build 보유 -> 해골 페널티 0.7배 감쇄(exp=24)");
            t.Eq(23L, resNoPerk2.exp, "[symPerk:skullPenaltyMul] perk 없음 대조군(exp=23)");
        }

        private static void RepairExpandCompressTagbuff(TestCtx t)
        {
            var run = MakeDeepRunInShop(90208L);
            run.Coins = 100;
            var e1 = RunControllerRepairBuy(run, "sv_expand", null);
            t.Eq("REPAIR_DONE", e1[0].type, "[repair:expand] 성공");
            t.Eq(5, run.DeepTotalMaxDelta, "[repair:expand] 상한 델타 +5");
            t.Eq(85, run.Coins, "[repair:expand] 코인 100-15=85");

            var e2 = RunControllerRepairBuy(run, "sv_compress", null);
            t.Eq("REPAIR_DONE", e2[0].type, "[repair:compress] 성공");
            t.Eq(-5, run.DeepTotalMinDelta, "[repair:compress] 하한 델타 -5");
            t.EqTol(0.05, run.DeepCompressExtra, "[repair:compress] 요구EXP +5%");
            t.Eq(60, run.Coins, "[repair:compress] 코인 85-25=60");

            var e3 = RunControllerRepairBuy(run, "sv_tagbuff", new RepairArgs { Tag = "생명" });
            t.Eq("REPAIR_DONE", e3[0].type, "[repair:tagbuff] 성공");
            t.EqTol(0.10, run.DeepTagBuff["생명"], "[repair:tagbuff] 생명 태그 +10%");
            t.Eq(38, run.Coins, "[repair:tagbuff] 코인 60-22=38");

            var rejectedTag = RunControllerRepairBuy(run, "sv_tagbuff", new RepairArgs());
            t.Eq("REJECTED", rejectedTag[0].type, "[repair:tagbuff] 태그 미지정 -> 거부");
            t.Eq("REPAIR_TAG_REQUIRED", rejectedTag[0].reason, "[repair:tagbuff] 사유");
        }

        // Opus 2차검수 필수③ — sv_tagbuff(22코인) 구매로 쌓인 run.DeepTagBuff가 실제로 Evaluate()의
        // 셀 EXP에 반영되는지 손계산 확인(웹 engine.js:679-683 소비 지점).
        private static void TagbuffPurchaseAffectsEvaluate(TestCtx t)
        {
            var run = MakeDeepRunInShop(90220L);
            run.Coins = 100;
            var ok = RunControllerRepairBuy(run, "sv_tagbuff", new RepairArgs { Tag = "생명" }); // cherry 태그
            t.Eq("REPAIR_DONE", ok[0].type, "[tagbuff:evaluate] 구매 성공");
            t.EqTol(0.10, run.DeepTagBuff["생명"], "[tagbuff:evaluate] 생명 태그 +10% 누적");

            var mods = new Mods();
            DeepRunHooks.ApplyDeepMods(mods, run);
            t.EqTol(0.10, mods.deepTagMul["생명"], "[tagbuff:evaluate] mods.deepTagMul[생명] == 0.10(ApplyDeepMods 배선)");

            // 체리 5개(전부 "생명" 태그, exp3) — 잭팟(bestCount==reel==5) + 세트보너스(SetExp[5]=100)까지
            // 겹쳐 절삭(long 캐스트) 경계를 확실히 넘는 큰 델타를 만든다(단일/2장 조합은 소수점 가산분이
            // 정수 절삭에 묻혀 baseline과 동일하게 나올 수 있음 — 아래 손계산 참조).
            var raw = new List<Cell> { C("cherry"), C("cherry"), C("cherry"), C("cherry"), C("cherry") };
            var baseline = SpinResolver.Evaluate(new Rng(11), raw, new Mods(), 1, 5, false);
            // per-cell 3×5=15 + 세트보너스(SetExp[5]=100) = 115 -> exp=115*1+0=115 -> +잭팟(cherry=120) = 235.
            t.Eq(235L, baseline.exp, "[tagbuff:evaluate] 태그버프 없음 -> exp=235(세트100+잭팟120 포함 손계산)");

            var withBuff = SpinResolver.Evaluate(new Rng(11), raw, mods, 1, 5, false);
            // per-cell (3×1.10)×5=16.5 + 세트보너스(태그버프 무관, 100) = 116.5 -> exp=116.5 -> +잭팟120 = 236.5
            // -> (long)236. baseline(235)과 정수로 명확히 구분됨.
            t.Eq(236L, withBuff.exp, "[tagbuff:evaluate] 태그버프 있으면 생명 태그(체리) EXP +10% 반영 -> exp=236");
            t.True(withBuff.exp > baseline.exp, "[tagbuff:evaluate] 태그버프 있으면 생명 태그 셀 EXP 증가");
        }

        private static void RepairCurseCleanse(TestCtx t)
        {
            var run = MakeDeepRunInShop(90209L);
            run.Coins = 100;
            var unavailable = RunControllerRepairBuy(run, "curse_cleanse", null);
            t.Eq("REJECTED", unavailable[0].type, "[repair:curse] 저주 없으면 거부(Execute 자체 게이트)");
            t.Eq("REPAIR_NO_CURSE", unavailable[0].reason, "[repair:curse] 사유(웹 applyShopService와 동일 경로)");
            t.True(!RepairShop.IsAvailable(run, RepairServices.ById("curse_cleanse")), "[repair:curse] IsAvailable(목록 표시용 헬퍼)도 false");

            run.Curses.Add("curse_a");
            run.Curses.Add("curse_b");
            var ok = RunControllerRepairBuy(run, "curse_cleanse", null);
            t.Eq("REPAIR_DONE", ok[0].type, "[repair:curse] 저주 보유 시 성공");
            t.Eq("curse_b", ok[0].curseRemovedId, "[repair:curse] 가장 최근(마지막) 저주 제거");
            t.Eq(1, run.Curses.Count, "[repair:curse] 저주 1개 남음");
            t.Eq("curse_a", run.Curses[0], "[repair:curse] curse_a 유지");
            t.Eq(65, run.Coins, "[repair:curse] 코인 100-35=65");
        }

        private static void RepairInsufficientCoinsRejectsWithoutCharge(TestCtx t)
        {
            var run = MakeDeepRunInShop(90210L);
            run.Coins = 0;
            var before = new Dictionary<string, int>(run.Pouch);
            var rejected = RunControllerRepairBuy(run, "sv_add_basic", new RepairArgs { Id = "magnet" });
            t.Eq("REJECTED", rejected[0].type, "[repair:coins] 코인 부족 -> 거부");
            t.Eq("INSUFFICIENT_COINS", rejected[0].reason, "[repair:coins] 사유");
            t.Eq(0, run.Coins, "[repair:coins] 코인 미차감");
            t.True(PouchEqualsForTest(before, run.Pouch), "[repair:coins] 주머니 무변경");
        }

        private static void RepairInvalidPouchRejectsWithoutCharge(TestCtx t)
        {
            var run = MakeDeepRunInShop(90211L);
            run.Pouch.Clear();
            foreach (var kv in new Dictionary<string, int> { { "cherry", 10 }, { "book", 10 }, { "star", 5 }, { "gem", 5 }, { "coin", 3 }, { "skull", 3 }, { "magnet", 1 } })
                run.Pouch[kv.Key] = kv.Value; // 정확히 7종(최소 종류) — magnet 제거 시 6종으로 위반.
            run.Coins = 100;
            var before = new Dictionary<string, int>(run.Pouch);
            var rejected = RunControllerRepairBuy(run, "sv_remove", new RepairArgs { Id = "magnet" });
            t.Eq("REJECTED", rejected[0].type, "[repair:invalid] 종류<7 위반 -> 거부");
            t.True(rejected[0].reason.StartsWith("REPAIR_INVALID_POUCH"), "[repair:invalid] 사유 접두사");
            t.Eq(100, run.Coins, "[repair:invalid] 코인 미차감");
            t.True(PouchEqualsForTest(before, run.Pouch), "[repair:invalid] 주머니 무변경(원자적 거부)");
        }

        private static void RepairPriceDiscount(TestCtx t)
        {
            var run = MakeDeepRunInShop(90212L);
            run.Perks.Add("sa_lab_regular"); // repairMul["*"]=0.7
            var sv = RepairServices.ById("sv_expand"); // price 15
            t.Eq(11, RepairShop.Price(run, sv), "[repair:price] 15×0.7=10.5 -> AwayFromZero 반올림 11");
        }

        private static void RepairTriggersArchetypeChangeEvent(TestCtx t)
        {
            var run = MakeDeepRunInShop(90213L);
            run.Coins = 100;
            // 시작덱 cherry=6/30=0.2(미달). +5하면 11/35=0.314... -> ArchT1(0.25) 이상으로 발동.
            var events = RunControllerRepairBuy(run, "sv_add_basic", new RepairArgs { Id = "cherry" });
            t.Eq(2, events.Count, "[repair:arch] REPAIR_DONE + ARCHETYPE_CHANGED 2건");
            t.Eq("REPAIR_DONE", events[0].type, "[repair:arch] 1번째 REPAIR_DONE");
            t.Eq("ARCHETYPE_CHANGED", events[1].type, "[repair:arch] 2번째 ARCHETYPE_CHANGED");
            t.Eq("cherry", events[1].archFamily, "[repair:arch] cherry 발동");
            t.Eq(1, events[1].archTier, "[repair:arch] tier1");
            t.True(!events[1].archUpgraded, "[repair:arch] 신규 발동(승급 아님)");
            t.Eq("cherry", run.DeepArchFamily, "[repair:arch] run.DeepArchFamily 갱신");
            t.Eq(1, run.DeepArchTier, "[repair:arch] run.DeepArchTier 갱신");

            // 변화 없는 구매(expand)는 ARCHETYPE_CHANGED를 추가하지 않는다.
            var noChange = RunControllerRepairBuy(run, "sv_expand", null);
            t.Eq(1, noChange.Count, "[repair:arch] 전공 변화 없으면 REPAIR_DONE만");
        }

        // 빈 주머니에서도 CheckArchetypeChange가 스테일 상태를 남기지 않고 정상 리셋하는지 확인
        // (Opus 2차검수 필수⑤ — 웹 `if (r.pouch)`는 빈 객체도 truthy라 pouchArchetype({})까지 흘러가
        // family=null/tier=0으로 자연히 리셋된다).
        private static void CheckArchetypeChangeResetsOnEmptyPouch(TestCtx t)
        {
            var run = MakeDeepRun(90221L); // cherry6 등 시작덱 total=30 -> cherry share=6/30=0.2(미달, tier0).
            run.Pouch.Clear();
            run.Pouch["cherry"] = 25;
            run.Pouch["star"] = 75; // cherry share=25/100=0.25 -> tier1 발동.
            DeepRunHooks.CheckArchetypeChange(run);
            t.Eq("cherry", run.DeepArchFamily, "[arch:empty] 사전 조건 — cherry 전공 발동 상태");

            run.Pouch.Clear(); // 완전히 빈 주머니(극단 케이스).
            var ev = DeepRunHooks.CheckArchetypeChange(run);
            t.True(ev == null, "[arch:empty] 빈 주머니 -> 소멸은 무음(이벤트 없음)");
            t.True(run.DeepArchFamily == null, "[arch:empty] DeepArchFamily 스테일 값 없이 null로 리셋");
            t.Eq(0, run.DeepArchTier, "[arch:empty] DeepArchTier 0으로 리셋");
        }

        // ══════════════════════════════════════════════════════════════════
        // §0 선행 blocker — LockedNext 센티널 왕복 + PEEK/MANIP/도박꾼재굴림/재시험 pouch 경유
        // ══════════════════════════════════════════════════════════════════
        private static void BlockerCellsFromIdsRoundTrip(TestCtx t)
        {
            var ids = new List<string> { "empty", "cherry", "random", "unknown_bogus_id", "book" };
            var rng = new Rng(42);
            var pouch = new Dictionary<string, int> { { "cherry", 1 }, { "book", 1 } };

            var cells = SpinResolver.CellsFromIds(ids, rng, pouch);
            t.Eq(5, cells.Count, "[blocker] rng/pouch 제공 -> 5칸 전부 보존(드롭 없음)");
            t.Eq("empty", cells[0].sym.id, "[blocker] empty 센티널 복원");
            t.Eq("cherry", cells[1].sym.id, "[blocker] 일반 id 정상 복원");
            t.True(cells[2].sym.id == "cherry" || cells[2].sym.id == "book", "[blocker] random -> 실심볼로 해석");
            t.Eq("empty", cells[3].sym.id, "[blocker] 미지 id는 EmptySym으로 대체(드롭 아님)");
            t.Eq("book", cells[4].sym.id, "[blocker] 일반 id 정상 복원(2)");

            var cellsNoRng = SpinResolver.CellsFromIds(ids); // rng/pouch 생략(기존 호출부 호환)
            t.Eq(5, cellsNoRng.Count, "[blocker] rng/pouch 생략해도 5칸 보존");
            t.Eq("empty", cellsNoRng[2].sym.id, "[blocker] rng/pouch 생략 시 random -> 안전하게 빈칸 대체");
        }

        private static void BlockerPeekThenSpinKeeps5Cells(TestCtx t)
        {
            var run = MakeDeepRun(90301L);
            run.Device = "dev_oracle";
            run.Pouch.Clear();
            run.Pouch["empty"] = 200;
            run.Pouch["cherry"] = 1;

            var peek = DeviceActions.Handle(run, "dev_oracle", null);
            t.Eq("DEVICE_PEEK", peek[0].type, "[blocker] PEEK 성공");
            t.Eq(Formulas.REEL, run.LockedNext.Count, "[blocker] PEEK 직후 LockedNext == REEL(5)칸");
            t.True(run.LockedNext.Contains("empty"), "[blocker] empty 압도적 비중이면 최소 1칸은 empty(사실상 확실)");

            var outcome = SpinResolver.ResolveSpin(run, SpinMode.N);
            t.True(!outcome.rejected, "[blocker] LockedNext 소비 스핀 정상 처리(거부 아님)");
            t.Eq(Formulas.REEL, outcome.result.cells.Count, "[blocker] empty 섞인 LockedNext 소비 후에도 5칸 유지(핵심 회귀 확인)");
        }

        private static readonly HashSet<string> CherryBookOnly = new HashSet<string> { "cherry", "book" };

        private static void BlockerPeekUsesPouchOnly(TestCtx t)
        {
            var run = MakeDeepRun(90302L);
            run.Device = "dev_oracle";
            run.Pouch.Clear();
            run.Pouch["cherry"] = 10;
            run.Pouch["book"] = 10;

            for (int i = 0; i < 40; i++)
            {
                run.UsedCmds.Clear();
                run.LockedNext.Clear();
                var events = DeviceActions.Handle(run, "dev_oracle", null);
                t.Eq("DEVICE_PEEK", events[0].type, $"[blocker-peek i={i}] PEEK 성공");
                foreach (var id in run.LockedNext)
                    t.True(CherryBookOnly.Contains(id), $"[blocker-peek i={i}] 예언 결과 {id}는 주머니(cherry/book) 안에서만 나와야 함");
            }
        }

        private static void BlockerManipRerollUsesPouchOnly(TestCtx t)
        {
            for (int i = 0; i < 20; i++)
            {
                var run = MakeDeepRun(90400L + i);
                run.Device = "dev_reroll";
                run.Pouch.Clear();
                run.Pouch["cherry"] = 10;
                run.Pouch["book"] = 10;
                run.Coins = 100;
                run.Stage = 1; run.SpinIndex = 1; run.LastSpinNo = 0;
                run.LastCells.AddRange(new[] { "cherry", "book", "cherry", "book", "cherry" });
                run.LastCellsFinal.AddRange(SpinResolver.CellsFromIds(run.LastCells));

                var events = DeviceActions.Handle(run, "dev_reroll", null);
                t.True(events[0].type == "DEVICE_MANIP_RESULT" || events[0].type == "STAGE_CLEARED" || events[0].type == "GAME_OVER",
                    $"[blocker-manip i={i}] 결과 타입(실제 {events[0].type})");
                var res = events[0].spin.result;
                t.True(res != null, $"[blocker-manip i={i}] SpinResult 존재");
                foreach (var c in res.cells)
                    t.True(CherryBookOnly.Contains(c.sym.id), $"[blocker-manip i={i}] 재굴림 결과 {c.sym.id}는 주머니 안에서만 나와야 함");
            }
        }

        private static void BlockerGamblerRerollUsesPouchOnly(TestCtx t)
        {
            for (int i = 0; i < 20; i++)
            {
                var run = MakeDeepRun(90500L + i);
                run.CharId = "gambler";
                run.Device = "";
                run.Pouch.Clear();
                run.Pouch["cherry"] = 10;
                run.Pouch["book"] = 10;
                run.Coins = 100;
                run.Stage = 1; run.SpinIndex = 1; run.LastSpinNo = 0;
                run.LastCells.AddRange(new[] { "cherry", "book", "cherry", "book", "cherry" });
                run.LastCellsFinal.AddRange(SpinResolver.CellsFromIds(run.LastCells));

                var events = DeviceActions.GamblerReroll(run, fromPost: false);
                t.True(events[0].type == "DEVICE_MANIP_RESULT" || events[0].type == "STAGE_CLEARED" || events[0].type == "GAME_OVER",
                    $"[blocker-gambler i={i}] 결과 타입(실제 {events[0].type})");
                var res = events[0].spin.result;
                foreach (var c in res.cells)
                    t.True(CherryBookOnly.Contains(c.sym.id), $"[blocker-gambler i={i}] 도박꾼 재굴림 결과 {c.sym.id}는 주머니 안에서만 나와야 함");
            }
        }

        private static void BlockerRetakeFormUsesPouchOnly(TestCtx t)
        {
            for (int i = 0; i < 20; i++)
            {
                var run = MakeDeepRun(90600L + i);
                run.Pouch.Clear();
                run.Pouch["cherry"] = 10;
                run.Pouch["book"] = 10;
                run.Items.Add("retake_form");
                run.Stage = 1; run.SpinIndex = 1; run.LastSpinNo = 0;
                run.LastCells.AddRange(new[] { "cherry", "book", "cherry", "book", "cherry" });
                run.LastCellsFinal.AddRange(SpinResolver.CellsFromIds(run.LastCells));

                var events = ItemUse.Use(run, "retake_form", new Dictionary<string, long>());
                t.Eq("ITEM_USED", events[0].type, $"[blocker-retake i={i}] 재시험 처리");
                var res = events[0].spin.result;
                foreach (var c in res.cells)
                    t.True(CherryBookOnly.Contains(c.sym.id), $"[blocker-retake i={i}] 재시험 결과 {c.sym.id}는 주머니 안에서만 나와야 함");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 심화 자동플레이 스모크 확장 — 정비소(RepairBuy) 액션을 섞은 장기 플레이(예외/NaN/음수 없음).
        // ══════════════════════════════════════════════════════════════════
        private static readonly HashSet<string> KnownEventTypesDeepP72 = new HashSet<string>
        {
            "REJECTED", "SPIN_RESULT", "STAGE_CLEARED", "REVIVED", "POST_SPIN", "GAME_OVER",
            "DEVICE_MANIP_RESULT", "NODE_RESOLVED", "PERK_OFFER", "PERK_GRANTED", "PERK_HELD",
            "RETAKE_EMPTY", "SHOP_OFFER", "SHOP_PURCHASED", "SHOP_REROLLED", "SHOP_LEFT",
            "ITEM_USED", "DEVICE_ARMED", "DEVICE_PEEK", "RUN_STARTED",
            "DEVICE_OFFER", "PERK_LEVELED", "STAGE_STARTED", "BOSS_PHASE2",
            "REPAIR_DONE", "ARCHETYPE_CHANGED",
            // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스) — POUCH 오퍼 v3/2-step 커밋/
            // REST·GAMBLE 심화 2택/3스테이지 연계 보너스 신규 이벤트.
            "POUCH_OFFER", "POUCH_COST_OFFER", "POUCH_REMOVE_OFFER", "DEEP_CHOICE_OFFER",
        };

        private static void DeepAutoplaySmokeWithRepairs(TestCtx t)
        {
            var localRng = new Rng(777);
            for (long seed = 6001; seed < 6004; seed++)
            {
                var rc = new RunController("novice", "basic", "", seed, S4TestHelpers.GenerousStat(), asc: 0, deep: true);
                int guard = 0;
                int shopStep = 0;
                while (rc.State.Phase != RunPhase.GameOver && guard < 20_000)
                {
                    guard++;
                    IReadOnlyList<RunEvent> events;
                    switch (rc.State.Phase)
                    {
                        case RunPhase.Spin:
                            events = rc.Do(new Spin(SpinMode.N));
                            break;
                        case RunPhase.PostSpin:
                            events = rc.Do(new Continue());
                            break;
                        case RunPhase.NodeSelect:
                            events = rc.Do(new ChooseNode(0));
                            break;
                        case RunPhase.EventAugment:
                        case RunPhase.EventRelic:
                        case RunPhase.EventAugLevel:
                        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스) — POUCH 2-step/
                        // REST·GAMBLE 심화 2택/3스테이지 연계 보너스도 PickOffer(index) 단일 액션.
                        case RunPhase.EventPouch:
                        case RunPhase.EventPouchCost:
                        case RunPhase.EventPouchRemove:
                        case RunPhase.EventRestDeep:
                        case RunPhase.EventGambleDeep:
                        case RunPhase.EventSynAugBonus:
                            events = rc.Do(new PickOffer(0));
                            break;
                        case RunPhase.EventShop:
                            // Opus 2차검수 필수② — RepairBuy는 RunPhase.EventShop에서만 통과한다(Shop.Buy와
                            // 동일 관례). 정비소 경로를 실사하려면 반드시 이 phase를 경유해야 하므로, 상점
                            // 진입 시(shopStep==0) 가끔(10% 근사) BuyOffer 대신 sv_expand를 사서 RepairShop
                            // 성공 경로까지 태운다(targetPick 없어 결정론적으로 호출 가능).
                            if (shopStep == 0)
                            {
                                if (rc.State.Coins >= 15 && localRng.Next(10) == 0)
                                    events = rc.Do(new RepairBuy("sv_expand"));
                                else
                                    events = rc.Do(new BuyOffer(0));
                                shopStep = 1;
                            }
                            else { events = rc.Do(new LeaveShop()); shopStep = 0; }
                            break;
                        case RunPhase.DeviceNode:
                            events = rc.Do(new TakeDevice(true));
                            break;
                        case RunPhase.RewardDone:
                            events = rc.Do(new ProceedToStage());
                            break;
                        default:
                            throw new InvalidOperationException($"[deep-repair-smoke seed={seed}] 처리 불가 Phase=" + rc.State.Phase);
                    }
                    foreach (var e in events)
                        t.True(KnownEventTypesDeepP72.Contains(e.type), $"[deep-repair-smoke seed={seed}] 알 수 없는 RunEvent.type={e.type}");
                }
                t.Eq(RunPhase.GameOver, rc.State.Phase, $"[deep-repair-smoke seed={seed}] 예외 없이 게임오버까지 진행(stage={rc.State.Stage})");
                t.True(rc.State.Score >= 0, $"[deep-repair-smoke seed={seed}] Score 음수 아님");
                t.True(rc.State.Coins >= 0, $"[deep-repair-smoke seed={seed}] Coins 음수 아님");
                t.True(!double.IsNaN(rc.State.Score), $"[deep-repair-smoke seed={seed}] Score NaN 아님");
                foreach (var kv in rc.State.Pouch)
                    t.True(kv.Value >= 0, $"[deep-repair-smoke seed={seed}] Pouch[{kv.Key}]={kv.Value} 음수 아님");
            }
        }

        // ── 공용 헬퍼 ─────────────────────────────────────────────────────
        private static RunState MakeDeepRun(long seed)
        {
            var run = S4TestHelpers.NewRun(seed); // Phase == RunPhase.Spin(§0 blocker/PEEK/MANIP 테스트 기본값).
            run.DeepMode = true;
            foreach (var kv in Pouch.NewStartPouch()) run.Pouch[kv.Key] = kv.Value;
            return run;
        }

        // Opus 2차검수 필수② — RepairShop.Execute는 RunPhase.EventShop에서만 통과한다. 정비소 성공
        // 테스트는 전부 이 헬퍼로 시작한다(MakeDeepRun + Phase 전환).
        private static RunState MakeDeepRunInShop(long seed)
        {
            var run = MakeDeepRun(seed);
            run.Phase = RunPhase.EventShop;
            return run;
        }

        // RunController 없이 RepairShop.Execute를 직접 호출(대부분 테스트는 순수 RunState로 충분) —
        // RunController.Do(new RepairBuy(...)) 배선 자체는 DeepAutoplaySmokeWithRepairs가 검증한다.
        private static List<RunEvent> RunControllerRepairBuy(RunState run, string serviceId, RepairArgs args) =>
            RepairShop.Execute(run, serviceId, args);

        private static bool PouchEqualsForTest(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a) { if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false; }
            return true;
        }
    }
}
