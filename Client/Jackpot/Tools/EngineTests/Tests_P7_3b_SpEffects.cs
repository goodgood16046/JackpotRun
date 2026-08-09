using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // P7-3b 골든/단위 테스트 — 웹 파리티(WEB_PARITY_DESIGN.md §1-A #19 "Sp 신규 51종 전면 이식") 3/4
    // 슬라이스(§2-(CC))가 남긴 잭팟태그 계열 외 나머지 특수심볼 39종의 실제 효과. 웹 engine.js:566-1083
    // (evaluate 특수심볼 체인)·game.js:768-862(_applyDeepSpinMeta)·892/907-916(temp_wild/fate_vortex
    // 프리훅)·2304-2347(_openShop/_freshShop)·1478-1488(safepin AUGLEVEL pity) 대조.
    internal static class Tests_P7_3b_SpEffects
    {
        private static readonly Rng UnusedRng = new Rng(1);

        public static void Run(TestCtx t)
        {
            // ── evaluate() 즉시효과 — 변환/위치 계열 ──
            PurifyRemovesSkullsUpToCount(t);
            MirrorSwapsEndsExcludingSelf(t);
            CatalystUpgradesLowestRankSymbol(t);
            CatalystApproxWhenNoUpgradeTarget(t);
            WandWildJoinsSetButExcludedFromJackpotGate(t);
            WandWildCappedByRealWilds(t);
            DeepEmptyScoreAndExp(t);
            TargetBoostsBestValueCellByHalf(t);
            Puzzle5FourKindsBonus(t);
            Puzzle5FiveKindsBonusWithExtraReel(t);

            // ── evaluate() 즉시효과 — 저주 계열 ──
            CurseBloodAddsFlatExp(t);
            CurseCandleMultipliesBySkullCount(t);
            CurseCandleZeroesExpWithoutSkulls(t);
            CurseBoomStatisticalBranches(t);

            // ── evaluate() 즉시효과 — instant 5종 ──
            BandageOffsetsOneSkullPenalty(t);
            KnotAddsExpWhenEndsMatch(t);
            EnergyPackBoosts30Percent(t);
            FakeCrownActsLikeCrown(t);
            EvoCoreTransformsOneBaseCell(t);

            // ── evaluate() 즉시효과 — 전설 계열 ──
            Lucky7SevenTimesMultiplier(t);
            PrismSymLegendStableForcesExpMul(t);
            PrismSymStatisticalBranchesWithoutLegendStable(t);

            // ── DeepRunHooks.ProcessDeepSpinFollowups 소비 ──
            GrowNextIsStored(t);
            AlarmAndGearStackPendingExpMul(t);
            CarryExpReservedThenConsumedNextSpin(t);
            ShopFlagsReceiptCouponCart(t);
            ShieldAndExemptFlagsSet(t);
            BatteryReleasesManipDevice(t);
            KitReleasesOrFallsBackToShopSlot(t);
            AugChanceBoostsAugLevelBoost(t);
            AugLevelNextLevelsUpLowestHeld(t);
            SetFragGrantsCoinsOnlyWhenSetFormed(t);
            CurseGaugeUpAddsToUnluckyGauge(t);
            CurseEyeNextIncrementsRewardBonusCapped(t);
            BlackCardAppearanceBumpsUnluckyGaugeOnly(t);
            CrystalConsumedAndPendingIncremented(t);
            SafePinMarksActiveOnly(t);
            TempWildConsumedOnlyWhenAppeared(t);
            FateVortexConsumedOncePerStage(t);

            // ── SpinResolver 굴림/스핀 파이프라인 ──
            GrowNextReplacesOneCellFromPool(t);
            TempWildAlwaysInjectsWildWhenOwned(t);
            FateVortexRerollsAndPicksBetter(t);
            ShackleReducesBossSpinsAndAddsClearCoinBonus(t);
            ShieldBlocksBossPenaltyExemptSkipsRule(t);

            // ── Shop.cs 상점 훅 ──
            ShopReceiptDiscountAppliesToAugmentPrices(t);
            ShopCartBonusIncreasesItemSlots(t);
            ShopCouponTagsOneEntryWith15PercentOff(t);
            ShopBlackCardFreeFirstPurchase(t);
            NodeEventsShopEntryConsumesCrystalAndBlackCard(t);

            // ── StageFlow.cs safepin AUGLEVEL pity ──
            SafePinBoostsAugLevelPityWhenPityFails(t);

            // ── 심화 자동플레이 스모크(신규 효과 관측 카운트 리포트) ──
            DeepAutoplaySmokeObservesNewEffects(t);
        }

        // ══════════════════════════════════════════════════════════════════
        // 헬퍼
        // ══════════════════════════════════════════════════════════════════
        // "empty"는 Symbols.All 카탈로그에 없는 별도 센티널(SpinResolver.EmptySym, internal) —
        // Symbols.ById("empty")가 null을 반환하므로 이 헬퍼가 직접 처리한다.
        private static Cell C(string id) => id == "empty" ? new Cell(SpinResolver.EmptySym) : new Cell(Symbols.ById(id));
        private static List<Cell> Cells(params string[] ids) => ids.Select(C).ToList();
        private static Mods DeepMods() => new Mods { deepMode = true };

        private static RunState MakeDeepRun(long seed)
        {
            var run = S4TestHelpers.NewRun(seed);
            run.DeepMode = true;
            foreach (var kv in Pouch.NewStartPouch()) run.Pouch[kv.Key] = kv.Value;
            // 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #19/#20) — Tests_P7_1_Pouch.MakeDeepRun과 동일
            // 근거(RunController 기본 폴백을 직접 재현).
            run.SymUnlocked.UnionWith(Pouch.DefaultUnlocked);
            return run;
        }

        private static SpinResult EmptyRes() => new SpinResult { cells = new List<Cell>() };

        // ══════════════════════════════════════════════════════════════════
        // evaluate() — 변환/위치 계열
        // ══════════════════════════════════════════════════════════════════
        private static void PurifyRemovesSkullsUpToCount(TestCtx t)
        {
            var cells = Cells("purifier", "purifier", "skull", "skull", "skull");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("empty", res.cells[2].sym.id, "[purify] 앞 해골부터 정화(2번칸)");
            t.Eq("empty", res.cells[3].sym.id, "[purify] 앞 해골부터 정화(3번칸)");
            t.Eq("skull", res.cells[4].sym.id, "[purify] 정화도구 수 초과분은 유지(4번칸)");
            t.Eq(1, res.skulls, "[purify] 남은 해골 1개만 페널티 대상");
            t.True(res.notes.Any(n => n.Contains("정화")), "[purify] 정화 노트");
        }

        private static void MirrorSwapsEndsExcludingSelf(TestCtx t)
        {
            var cells = Cells("cherry", "gem", "mirror_sym", "star", "book");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("book", res.cells[0].sym.id, "[mirror] 1번칸 <- 마지막칸");
            t.Eq("cherry", res.cells[4].sym.id, "[mirror] 마지막칸 <- 1번칸(원본)");
            t.True(res.notes.Any(n => n.Contains("미러")), "[mirror] 미러 노트");

            // 거울 자체가 끝칸이면(소스 제외 규칙) 발동하지 않는다.
            var cellsSelf = Cells("mirror_sym", "cherry", "star", "gem", "book");
            var resSelf = SpinResolver.Evaluate(UnusedRng, cellsSelf, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("mirror_sym", resSelf.cells[0].sym.id, "[mirror:self-end] 거울이 끝칸이면 자기 자신 제외로 미발동");
            t.Eq("book", resSelf.cells[4].sym.id, "[mirror:self-end] 반대쪽도 그대로 유지");
        }

        private static void CatalystUpgradesLowestRankSymbol(TestCtx t)
        {
            var cells = Cells("catalyst", "star", "gem", "empty", "empty");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("gem_cut", res.cells[2].sym.id, "[catalyst] 유일한 업그레이드 대상(gem)이 gem_cut으로 강화");
            t.True(res.notes.Any(n => n.Contains("강화")), "[catalyst] 강화 노트");
        }

        private static void CatalystApproxWhenNoUpgradeTarget(TestCtx t)
        {
            // star/crown은 POUCH_UPGRADE 매핑이 없다(VALUE_IDS엔 속함) — 근사 +3 분기.
            var cells = Cells("catalyst", "star", "crown", "empty", "empty");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("star", res.cells[1].sym.id, "[catalyst:approx] 매핑 대상 없어 셀 변형 없음");
            t.Eq("crown", res.cells[2].sym.id, "[catalyst:approx] 매핑 대상 없어 셀 변형 없음");
            t.True(res.notes.Any(n => n.Contains("촉매(강화 +3)")), "[catalyst:approx] 근사 +3 노트");
        }

        private static void WandWildJoinsSetButExcludedFromJackpotGate(TestCtx t)
        {
            var cells = Cells("magic_wand", "cherry", "cherry", "cherry", "cherry");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq(5, res.bestSetCount, "[wandwild] 마법봉 기여 포함 세트 카운트 5");
            t.Eq((string)null, res.jackpotSym, "[wandwild] 잭팟 게이트에서는 마법봉 기여 제외 -> 잭팟 미발동");
            t.True(res.notes.Any(n => n.Contains("마법봉 와일드")), "[wandwild] 마법봉 노트");
        }

        private static void WandWildCappedByRealWilds(TestCtx t)
        {
            // 실와일드 4개(reel-1) 이미 상한 -> 마법봉 기여 0(cap=0).
            var cells = Cells("wild", "wild", "wild", "wild", "magic_wand");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.False(res.notes.Any(n => n.Contains("마법봉 와일드")), "[wandwild:cap] 실와일드 4개로 상한 -> 마법봉 기여 없음");
        }

        private static void DeepEmptyScoreAndExp(TestCtx t)
        {
            var mods = new Mods { deepEmptyScore = 5, deepEmptyExp = 2 };
            var cells = Cells("empty", "empty", "cherry", "star", "gem");
            var res = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // base exp = cherry3+star8+gem1=12, +emptyN(2)*deepEmptyExp(2)=4 -> 16.
            t.Eq(16L, res.exp, "[deep-empty] EXP = base12 + 2칸*2");
            // base score = gem15, +emptyN(2)*deepEmptyScore(5)=10 -> 25.
            t.Eq(25L, res.score, "[deep-empty] 점수 = base15 + 2칸*5");
            t.True(res.notes.Any(n => n.Contains("빈칸")), "[deep-empty] 빈칸 노트");

            var resNoMod = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq(12L, resNoMod.exp, "[deep-empty:no-mod] mods 미설정이면 무영향(EXP)");
            t.Eq(15L, resNoMod.score, "[deep-empty:no-mod] mods 미설정이면 무영향(점수)");
        }

        private static void TargetBoostsBestValueCellByHalf(TestCtx t)
        {
            var cells = Cells("target", "cherry", "star", "gem", "crown");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // base = 3+8+1+20 = 32, 최고칸=crown(20) -> +10.
            t.Eq(42L, res.exp, "[target] 최고칸(왕관 EXP20) +50%=10 가산");
            t.True(res.notes.Any(n => n.Contains("표적 최고칸")), "[target] 표적 노트");
        }

        private static void Puzzle5FourKindsBonus(TestCtx t)
        {
            var cells = Cells("jigsaw", "cherry", "star", "gem", "book");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.True(res.notes.Any(n => n.Contains("퍼즐 4종 +150점")), "[puzzle5:4kind] 4종 +150점 노트");
        }

        private static void Puzzle5FiveKindsBonusWithExtraReel(TestCtx t)
        {
            // reel=6(dev_subreel 확장 근사) — jigsaw 1칸 + 값심볼 5종 전부.
            var cells = Cells("jigsaw", "cherry", "star", "gem", "book", "crown");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.True(res.notes.Any(n => n.Contains("퍼즐 5종 +300점")), "[puzzle5:5kind] 5종 +300점 노트");
        }

        // ══════════════════════════════════════════════════════════════════
        // evaluate() — 저주 계열
        // ══════════════════════════════════════════════════════════════════
        private static void CurseBloodAddsFlatExp(TestCtx t)
        {
            var cells = Cells("bloodrop", "bloodrop", "cherry", "star", "gem");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // per-cell 기본(8*2=16) + 특수블록 가산(2*2=4) + cherry3+star8+gem1(12) = 32.
            t.Eq(32L, res.exp, "[curse-blood] 피방울 개당 +10(기본8+가산2) 정확 반영");
            t.True(res.notes.Any(n => n.Contains("피방울")), "[curse-blood] 피방울 노트");
        }

        private static void CurseCandleMultipliesBySkullCount(TestCtx t)
        {
            var cells = Cells("black_candle_sym", "skull", "skull", "cherry", "cherry");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // (cherry3*2=6 + set[2]=8 - skull페널티6) * specialMul(1+0.25*2=1.5) = 8*1.5=12.
            t.Eq(12L, res.exp, "[curse-candle] 해골2개 -> ×1.5 배율 손계산");
            t.Eq(3L, res.score, "[curse-candle] 점수는 specialMul 영향 없음(세트[2]=3)");
            t.True(res.notes.Any(n => n.Contains("검은초")), "[curse-candle] 검은초 노트");
        }

        private static void CurseCandleZeroesExpWithoutSkulls(TestCtx t)
        {
            var cells = Cells("black_candle_sym", "cherry", "star", "gem", "book");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq(0L, res.exp, "[curse-candle:no-skull] 해골 없으면 EXP 0");
            t.True(res.notes.Any(n => n.Contains("해골없음")), "[curse-candle:no-skull] 해골없음 노트");
        }

        private static void CurseBoomStatisticalBranches(TestCtx t)
        {
            var cells = Cells("unstable_bomb", "cherry", "star", "gem", "book");
            // base(폭탄없음) = 3+8+1+6 = 18. 성공(대폭발×2)=36, 실패(불발)=0.
            bool sawMisfire = false, sawBoost = false;
            for (long seed = 1; seed <= 200; seed++)
            {
                var rng = new Rng(seed);
                var res = SpinResolver.Evaluate(rng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
                t.True(res.exp == 0 || res.exp == 36, $"[curse-boom seed={seed}] EXP는 0(불발) 또는 36(대폭발×2) 중 하나");
                if (res.exp == 0) sawMisfire = true;
                if (res.exp == 36) sawBoost = true;
            }
            t.True(sawMisfire, "[curse-boom] 200회 표본 중 불발(EXP 0) 최소 1회 관측");
            t.True(sawBoost, "[curse-boom] 200회 표본 중 대폭발(×2) 최소 1회 관측");
        }

        // ══════════════════════════════════════════════════════════════════
        // evaluate() — instant 5종
        // ══════════════════════════════════════════════════════════════════
        private static void BandageOffsetsOneSkullPenalty(TestCtx t)
        {
            var cells = Cells("bandage", "skull", "skull", "cherry", "star");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // base(3+8=11) - 페널티(2*3=6) + 붕대 상쇄(+3) = 8.
            t.Eq(8L, res.exp, "[bandage] 해골 패널티 1개분 상쇄");
            t.True(res.hasBandage, "[bandage] hasBandage 신호");
            t.True(res.notes.Any(n => n.Contains("붕대")), "[bandage] 붕대 노트");
        }

        private static void KnotAddsExpWhenEndsMatch(TestCtx t)
        {
            var cells = Cells("cherry", "knot", "star", "gem", "cherry");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // base(3+0+8+1+3=15) + 세트[2](8) + 매듭(+20) = 43.
            t.Eq(43L, res.exp, "[knot] 양끝 동일 -> +20 가산");
            t.True(res.hasKnot, "[knot] hasKnot 신호");

            var cellsNoMatch = Cells("knot", "cherry", "star", "gem", "book");
            var resNoMatch = SpinResolver.Evaluate(UnusedRng, cellsNoMatch, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.False(resNoMatch.notes.Any(n => n.Contains("매듭")), "[knot:no-match] 양끝 불일치면 노트 없음");
        }

        private static void EnergyPackBoosts30Percent(TestCtx t)
        {
            var cells = Cells("energypack", "cherry", "star", "gem", "book");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // base(3+8+1+6=18) * 1.30 = 23.4 -> 절삭 23.
            t.Eq(23L, res.exp, "[energypack] EXP ×1.30(절삭)");
            t.True(res.hasEnergyPack, "[energypack] hasEnergyPack 신호");
        }

        private static void FakeCrownActsLikeCrown(TestCtx t)
        {
            var cells = Cells("fake_crown_sym", "cherry", "star", "gem", "book");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // base exp(0+3+8+1+6=18) + crown.exp(20) = 38. base score(gem15) + crown.score(50) = 65.
            t.Eq(38L, res.exp, "[fakecrown] 왕관 EXP 가산");
            t.Eq(65L, res.score, "[fakecrown] 왕관 점수 가산");
            t.True(res.hasFakeCrown, "[fakecrown] hasFakeCrown 신호");
        }

        private static void EvoCoreTransformsOneBaseCell(TestCtx t)
        {
            var originalIds = new[] { "evo_core", "cherry", "star", "gem", "book" };
            for (long seed = 1; seed <= 30; seed++)
            {
                var cells = Cells(originalIds);
                var rng = new Rng(seed);
                var res = SpinResolver.Evaluate(rng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
                int changed = 0;
                string changedId = null;
                for (int i = 1; i < res.cells.Count; i++)
                {
                    if (res.cells[i].sym.id != originalIds[i]) { changed++; changedId = res.cells[i].sym.id; }
                }
                t.Eq(1, changed, $"[evocore seed={seed}] 기본 이득 심볼 정확히 1개만 변환");
                t.Eq("special", Pouch.CatOf(changedId), $"[evocore seed={seed}] 변환 결과는 special 카테고리");
                t.Eq("SILVER", Pouch.TierOf(changedId), $"[evocore seed={seed}] 변환 결과는 SILVER 티어");
                t.True(res.hasEvoCore, $"[evocore seed={seed}] hasEvoCore 신호");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // evaluate() — 전설 계열
        // ══════════════════════════════════════════════════════════════════
        private static void Lucky7SevenTimesMultiplier(TestCtx t)
        {
            var cells = Cells("lucky7", "lucky7", "lucky7", "coin", "coin");
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.True(res.lucky7, "[lucky7] lucky7 신호");
            t.Eq(14, res.coins, "[lucky7] 코인 7배(2*7=14)");
            t.True(res.notes.Any(n => n.Contains("럭키7")), "[lucky7] 럭키7 노트");

            // Opus 2차검수(P7-3b) [LOW 일괄] — coin(exp0/score0)만으로는 EXP×7(specialMul 경유)·
            // 점수×7(score 직접 곱)이 실제로 걸리는지 검증할 수 없었다 — 값심볼(cherry/gem) 조합으로
            // 양쪽 모두 손계산 검증.
            var cellsValue = Cells("lucky7", "lucky7", "lucky7", "cherry", "gem");
            var resValue = SpinResolver.Evaluate(UnusedRng, cellsValue, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // base exp = cherry3+gem1 = 4 -> specialMul×7 = 28. base score = gem15 -> 직접 ×7 = 105.
            t.Eq(28L, resValue.exp, "[lucky7:value] EXP ×7(specialMul 경유) 손계산");
            t.Eq(105L, resValue.score, "[lucky7:value] 점수 ×7(직접 곱) 손계산");
        }

        private static void PrismSymLegendStableForcesExpMul(TestCtx t)
        {
            var mods = new Mods { legendStable = true };
            var cells = Cells("prism_sym", "cherry", "star", "gem", "book");
            var res = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            // base(3+8+1+6=18) * 1.5 = 27.
            t.Eq(27L, res.exp, "[prism:legend-stable] 항상 EXP×1.5 최선 분기 고정");
            t.True(res.notes.Any(n => n.Contains("안정")), "[prism:legend-stable] 안정 표기");
        }

        private static void PrismSymStatisticalBranchesWithoutLegendStable(TestCtx t)
        {
            // base(gem 값심볼 자체 score=15 고정 기여 포함) exp=3+8+1+6=18, score=15, coins=0.
            var cells = Cells("prism_sym", "cherry", "star", "gem", "book");
            bool saw40 = false, saw120 = false, saw3coin = false, saw1_5x = false;
            for (long seed = 1; seed <= 400; seed++)
            {
                var rng = new Rng(seed);
                var res = SpinResolver.Evaluate(rng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
                if (res.exp == 58 && res.score == 15 && res.coins == 0) saw40 = true;
                else if (res.exp == 18 && res.score == 135 && res.coins == 0) saw120 = true;
                else if (res.exp == 18 && res.score == 15 && res.coins == 3) saw3coin = true;
                else if (res.exp == 27 && res.score == 15 && res.coins == 0) saw1_5x = true;
            }
            t.True(saw40, "[prism:random] EXP+40 분기 관측");
            t.True(saw120, "[prism:random] 점수+120 분기 관측");
            t.True(saw3coin, "[prism:random] 코인+3 분기 관측");
            t.True(saw1_5x, "[prism:random] EXP×1.5 분기 관측");
        }

        // ══════════════════════════════════════════════════════════════════
        // DeepRunHooks.ProcessDeepSpinFollowups
        // ══════════════════════════════════════════════════════════════════
        private static void GrowNextIsStored(TestCtx t)
        {
            var run = MakeDeepRun(1);
            var res = EmptyRes(); res.growNext = "HIGH";
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq("HIGH", run.GrowNext, "[follow:grow] growNext 저장");
        }

        private static void AlarmAndGearStackPendingExpMul(TestCtx t)
        {
            var run = MakeDeepRun(2);
            run.PendingNextExpMul = 1.0;
            var res = EmptyRes(); res.alarmNext = true; res.gearNext = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.EqTol(1.1 * 1.1, run.PendingNextExpMul, "[follow:alarm-gear] 알람+톱니 누적곱 ×1.21");
        }

        private static void CarryExpReservedThenConsumedNextSpin(TestCtx t)
        {
            // Opus 2차검수(P7-3b) [LOW 일괄] — 이월분은 `gained`(이번 스핀 실적 — RunBestSpin/LastGain
            // 오염 방지)가 아니라 `run.StageExp`에 직접 가산되도록 정정(웹 `r.stageExp += co` 그대로).
            var run = MakeDeepRun(3);
            long stageExpBefore = run.StageExp;
            var res = EmptyRes(); res.carryExp = 25;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(25L, run.CarryOverExp, "[follow:carry] 이번 스핀은 예약만(가산 없음)");
            t.Eq(0L, gained, "[follow:carry] 이번 스핀 gained 무영향");
            t.Eq(stageExpBefore, run.StageExp, "[follow:carry] 예약 시점엔 StageExp도 무영향");

            var res2 = EmptyRes();
            long gained2 = 100;
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res2, ref gained2, notes);
            t.Eq(100L, gained2, "[follow:carry] 다음 스핀 gained는 이월분과 무관(오염 없음)");
            t.Eq(stageExpBefore + 25, run.StageExp, "[follow:carry] StageExp에 이월분 직접 가산");
            t.Eq(0L, run.CarryOverExp, "[follow:carry] 소진 후 0");
        }

        private static void ShopFlagsReceiptCouponCart(TestCtx t)
        {
            var run = MakeDeepRun(4);
            var res = EmptyRes(); res.receiptNext = true; res.couponNext = true; res.cartNext = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.True(run.DeepShopDiscount, "[follow:shop] 영수증 플래그");
            t.True(run.DeepShopCoupon, "[follow:shop] 쿠폰 플래그");
            t.Eq(1, run.DeepShopSlotBonus, "[follow:shop] 장바구니 +1");

            // 상한 2 클램프.
            var res2 = EmptyRes(); res2.cartNext = true;
            var res3 = EmptyRes(); res3.cartNext = true;
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res2, ref gained, notes);
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res3, ref gained, notes);
            t.Eq(2, run.DeepShopSlotBonus, "[follow:shop] 장바구니 상한 2");
        }

        private static void ShieldAndExemptFlagsSet(TestCtx t)
        {
            var run = MakeDeepRun(5);
            var res = EmptyRes(); res.shieldNext = true; res.exemptNext = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.True(run.BossShield, "[follow:boss] 방패 플래그");
            t.True(run.BossExempt, "[follow:boss] 시험지 플래그");
        }

        private static void BatteryReleasesManipDevice(TestCtx t)
        {
            var run = MakeDeepRun(6);
            run.UsedCmds.Add("dev_reroll"); // MANIP 장치 마커
            var res = EmptyRes(); res.batteryNext = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.False(run.UsedCmds.Contains("dev_reroll"), "[follow:battery] MANIP 마커 해제");
        }

        private static void KitReleasesOrFallsBackToShopSlot(TestCtx t)
        {
            var run = MakeDeepRun(7);
            run.UsedCmds.Add("dev_pin");
            var res = EmptyRes(); res.kitNext = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.False(run.UsedCmds.Contains("dev_pin"), "[follow:kit] MANIP 마커 있으면 해제 우선");
            t.Eq(0, run.DeepShopSlotBonus, "[follow:kit] 해제 성공 시 상점칸 폴백 없음");

            var run2 = MakeDeepRun(8);
            var res2 = EmptyRes(); res2.kitNext = true;
            DeepRunHooks.ProcessDeepSpinFollowups(run2, new Mods(), res2, ref gained, notes);
            t.Eq(1, run2.DeepShopSlotBonus, "[follow:kit] 해제 대상 없으면 상점칸 +1 폴백");
        }

        private static void AugChanceBoostsAugLevelBoost(TestCtx t)
        {
            var run = MakeDeepRun(9);
            run.AugLevelBoost = 0.0;
            var res = EmptyRes(); res.augChanceNext = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.EqTol(0.15, run.AugLevelBoost, "[follow:augchance] +15%p 부스트");
        }

        private static void AugLevelNextLevelsUpLowestHeld(TestCtx t)
        {
            var run = MakeDeepRun(10);
            run.Perks.Add("study");
            var res = EmptyRes(); res.augLevelNext = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(2, run.PerkLevels.TryGetValue("study", out var lv) ? lv : 1, "[follow:auglevel] 보유 증강 즉시 레벨업");

            // 레벨업 가능한 증강이 없으면 무효과.
            var run2 = MakeDeepRun(11);
            var res2 = EmptyRes(); res2.augLevelNext = true;
            DeepRunHooks.ProcessDeepSpinFollowups(run2, new Mods(), res2, ref gained, notes);
            t.Eq(0, run2.PerkLevels.Count, "[follow:auglevel:none] 보유 증강 없으면 무효과");
        }

        private static void SetFragGrantsCoinsOnlyWhenSetFormed(TestCtx t)
        {
            var run = MakeDeepRun(12);
            long before = run.Coins;
            var res = EmptyRes(); res.setFrag = true; res.bestSetId = "cherry"; res.bestSetCount = 2;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(before + 2, run.Coins, "[follow:setfrag] 세트 형성(count>=2) 시 코인+2");

            var run2 = MakeDeepRun(13);
            long before2 = run2.Coins;
            var res2 = EmptyRes(); res2.setFrag = true; res2.bestSetId = null; res2.bestSetCount = 0;
            DeepRunHooks.ProcessDeepSpinFollowups(run2, new Mods(), res2, ref gained, notes);
            t.Eq(before2, run2.Coins, "[follow:setfrag:no-set] 세트 미형성(bestSetId null)이면 무효과");

            // Opus 2차검수(P7-3b) [HIGH-1] — bestSetId는 count==1(값심볼이 딱 1개만 나와도 "최다"로
            // 채워짐)이어도 non-null일 수 있다(SpinResolver.Evaluate의 bestId 결정 루프 참조) — 구
            // `bestSetId != null` 게이트는 이 경우도 "세트 형성"으로 오판정해 코인을 지급하던 버그였다.
            // bestSetCount>=2로 정정한 뒤 count==1(세트 아님) 케이스가 실제로 미발동하는지 확인.
            var run3 = MakeDeepRun(14);
            long before3 = run3.Coins;
            var res3 = EmptyRes(); res3.setFrag = true; res3.bestSetId = "cherry"; res3.bestSetCount = 1;
            DeepRunHooks.ProcessDeepSpinFollowups(run3, new Mods(), res3, ref gained, notes);
            t.Eq(before3, run3.Coins, "[follow:setfrag:count1] bestSetCount==1(세트 아님)이면 bestSetId가 있어도 무효과");
        }

        private static void CurseGaugeUpAddsToUnluckyGauge(TestCtx t)
        {
            // Opus 2차검수(P7-3b) [MED-5, Fable 결정] — 저주게이지 가산을 제거했다(Unity UnluckyGauge는
            // 만땅 시 forceRare 실보상이 걸려 있어, 저주 심볼이 게이지를 채워주면 "저주=이득"이라는
            // 부호 역전이 생기기 때문 — 웹은 이 게이지가 순수 표시용이라 무해했음). curseGaugeUp이
            // 있어도 UnluckyGauge가 더 이상 움직이지 않는 것을 확인(회귀 방지 고정).
            var run = MakeDeepRun(14);
            run.UnluckyGauge = 0;
            var res = EmptyRes(); res.curseGaugeUp = 2;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(0, run.UnluckyGauge, "[follow:curse-gauge] [MED-5] 가산 제거 확인(UnluckyGauge 불변)");
        }

        private static void CurseEyeNextIncrementsRewardBonusCapped(TestCtx t)
        {
            var run = MakeDeepRun(15);
            var res = EmptyRes(); res.curseEyeNext = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(1, run.DeepRewardBonus, "[follow:curse-eye] 보상 후보 +1");
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(2, run.DeepRewardBonus, "[follow:curse-eye] 상한 2");
        }

        private static void BlackCardAppearanceBumpsUnluckyGaugeOnly(TestCtx t)
        {
            // Opus 2차검수(P7-3b) [MED-5, Fable 결정] — 검은카드 등장 시 불운게이지 가산도 제거(위
            // CurseGaugeUpAddsToUnluckyGauge와 동일 근거). hasBlackCard 신호 자체는 evaluate 반환
            // 계약(hasShackle/hasFateVortex와 동일한 "구조적 신호만 유지")으로 남아있지만 이 함수는
            // 더 이상 아무 것도 소비하지 않는다 — 실제 효과(1개 무료구매)는 상점 진입 훅 전담.
            var run = MakeDeepRun(16);
            run.Pouch["black_card"] = 1;
            run.UnluckyGauge = 0;
            var res = EmptyRes(); res.hasBlackCard = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(0, run.UnluckyGauge, "[follow:blackcard] [MED-5] 가산 제거 확인(UnluckyGauge 불변)");
            t.Eq(1, run.Pouch["black_card"], "[follow:blackcard] 덱 소비는 상점 진입 시(여기선 미소비)");
        }

        private static void CrystalConsumedAndPendingIncremented(TestCtx t)
        {
            var run = MakeDeepRun(17);
            run.Pouch["crystal"] = 2;
            var res = EmptyRes(); res.hasCrystal = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(1, run.DeepCrystalPending, "[follow:crystal] 예약치 +1");
            t.Eq(1, run.Pouch["crystal"], "[follow:crystal] 덱 -1");
        }

        private static void SafePinMarksActiveOnly(TestCtx t)
        {
            var run = MakeDeepRun(18);
            run.Pouch["safepin"] = 1;
            var res = EmptyRes(); res.hasSafePin = true;
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.True(run.SafePinActive, "[follow:safepin] 등장 마킹");
            t.Eq(1, run.Pouch["safepin"], "[follow:safepin] 여기서는 덱 미소비(StageFlow가 실소비)");
        }

        private static void TempWildConsumedOnlyWhenAppeared(TestCtx t)
        {
            var run = MakeDeepRun(19);
            run.Pouch["temp_wild"] = 1;
            var resNo = EmptyRes(); // hasTempWild=false
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), resNo, ref gained, notes);
            t.Eq(1, run.Pouch["temp_wild"], "[follow:tempwild] 등장 안 하면 미소모");

            var resYes = EmptyRes(); resYes.hasTempWild = true;
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), resYes, ref gained, notes);
            t.False(run.Pouch.ContainsKey("temp_wild"), "[follow:tempwild] 등장하면 소모(0->제거)");
        }

        private static void FateVortexConsumedOncePerStage(TestCtx t)
        {
            // Opus 2차검수(P7-3b) [LOW 일괄] — 스테이지 스코프에서 런 스코프로 통일(RunState.
            // FateVortexUsed/FateVortexConsumed, bool). "한 번 쓰면 런 끝까지 재발동 없음"만 검증.
            var run = MakeDeepRun(20);
            run.Pouch["fate_vortex"] = 1;
            run.FateVortexUsed = true;
            var res = EmptyRes();
            long gained = 0; var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.False(run.Pouch.ContainsKey("fate_vortex"), "[follow:fatevortex] 사용됐으면 1개 소비");
            t.True(run.FateVortexConsumed, "[follow:fatevortex] 소비 기록");

            // 이미 소비된 뒤에는(같은 런 내내) 재호출해도 중복 소비되지 않음(주머니에 다시 채워도).
            run.Pouch["fate_vortex"] = 5;
            DeepRunHooks.ProcessDeepSpinFollowups(run, new Mods(), res, ref gained, notes);
            t.Eq(5, run.Pouch["fate_vortex"], "[follow:fatevortex] 런 내 중복 소비 없음");
        }

        // ══════════════════════════════════════════════════════════════════
        // SpinResolver 굴림/스핀 파이프라인
        // ══════════════════════════════════════════════════════════════════
        private static void GrowNextReplacesOneCellFromPool(TestCtx t)
        {
            var run = MakeDeepRun(21);
            run.GrowNext = "HIGH";
            var raw = Cells("skull", "skull", "skull", "skull", "skull");
            var result = DeepRunHooks.ApplyGrowNext(run, raw);
            t.Eq((string)null, run.GrowNext, "[grownext] 소진 후 null");
            var changed = result.Where(c => c.sym.id != "skull").ToList();
            t.Eq(1, changed.Count, "[grownext:high] 정확히 1칸만 성장 치환");
            t.True(new[] { "cherry", "book", "star" }.Contains(changed[0].sym.id), "[grownext:high] HIGH 풀(체리/책/별) 준수");
            t.Eq("🌱→", changed[0].tag, "[grownext:high] 성장 태그");

            var run2 = MakeDeepRun(22);
            run2.GrowNext = "ANY";
            var raw2 = Cells("skull", "skull", "skull", "skull", "skull");
            var result2 = DeepRunHooks.ApplyGrowNext(run2, raw2);
            var changed2 = result2.Where(c => c.sym.id != "skull").ToList();
            t.Eq(1, changed2.Count, "[grownext:any] 정확히 1칸만 성장 치환");
            t.True(new[] { "cherry", "book", "star", "gem", "coin" }.Contains(changed2[0].sym.id), "[grownext:any] ANY 풀 준수");

            // 일반모드/GrowNext 없음 -> 무영향.
            var run3 = MakeDeepRun(23); run3.DeepMode = false; run3.GrowNext = "HIGH";
            var raw3 = Cells("skull", "skull", "skull", "skull", "skull");
            var result3 = DeepRunHooks.ApplyGrowNext(run3, raw3);
            t.True(result3.All(c => c.sym.id == "skull"), "[grownext:normal-mode] 일반모드는 무영향");
        }

        private static void TempWildAlwaysInjectsWildWhenOwned(TestCtx t)
        {
            // ApplyCellOps(wild_temp 주입)는 Evaluate() "이전"에 raw 배열을 직접 변형한다 — 이후
            // Evaluate 내부에서 폭탄/자석/거울/촉매/진화핵이 같은 칸을 다시 덮어쓸 수 있으므로(웹도
            // applyCellOps→evaluate 순서가 동일해 같은 가능성을 갖는다), "최종 res.cells"가 아니라
            // Evaluate 변형 이전 스냅샷인 res.rawCells로 주입 자체의 무조건 발동을 검증한다.
            for (long seed = 100; seed < 120; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Pouch["temp_wild"] = 3;
                var outcome = SpinResolver.ResolveSpin(run, SpinMode.N);
                t.False(outcome.rejected, $"[tempwild-inject seed={seed}] 스핀 성공");
                t.True(outcome.result.rawCells.Any(c => c.sym.special == Sp.WILD),
                    $"[tempwild-inject seed={seed}] temp_wild 보유 시 매 스핀 무조건 와일드 1칸 주입(cellOp 시점)");
            }
        }

        private static void FateVortexRerollsAndPicksBetter(TestCtx t)
        {
            for (long seed = 200; seed < 210; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Pouch["fate_vortex"] = 1;
                var outcome = SpinResolver.ResolveSpin(run, SpinMode.N);
                t.False(outcome.rejected, $"[fatevortex seed={seed}] 스핀 성공");
                t.True(outcome.notes.Any(n => n.Contains("운명의소용돌이")), $"[fatevortex seed={seed}] 1번째 스핀에서 발동 노트");
                t.True(run.FateVortexUsed, $"[fatevortex seed={seed}] 런 사용 기록");

                // Opus 2차검수(P7-3b) [LOW 일괄] — 런 스코프로 통일했으므로 2번째 스핀(같은 스테이지든
                // 다음 스테이지든)은 재발동 없음.
                var outcome2 = SpinResolver.ResolveSpin(run, SpinMode.N);
                t.False(outcome2.notes.Any(n => n.Contains("운명의소용돌이")), $"[fatevortex seed={seed}] 같은 런 2번째 스핀은 재발동 없음");
            }
        }

        private static void ShackleReducesBossSpinsAndAddsClearCoinBonus(TestCtx t)
        {
            var run = MakeDeepRun(30);
            run.Pouch["shackle"] = 1;
            run.Stage = 10; // "strict" 보스 스테이지

            var mods = new Mods();
            DeepRunHooks.ApplyDeepMods(mods, run);
            t.True(mods.shackleActive, "[shackle] shackleActive 세팅");
            t.Eq(4, mods.clearCoinBonus, "[shackle] 클리어코인+4");

            // Opus 2차검수(P7-3b) [MED-4] — EffSpins가 이제 run.Pouch["shackle"]을 직접 참조하므로
            // (mods 인자와 무관) "제어군"은 mods가 아니라 shackle을 아예 보유하지 않은 별도 run으로
            // 구성해야 한다(같은 run에 mods만 바꿔 비교하면 제어군도 함께 -1이 걸려 버린다).
            var runNoShackle = MakeDeepRun(30);
            runNoShackle.Stage = 10;
            int spinsWithShackle = SpinResolver.EffSpins(run, mods);
            int spinsControl = SpinResolver.EffSpins(runNoShackle, new Mods());
            t.Eq(spinsControl - 1, spinsWithShackle, "[shackle] 보스 스테이지 스핀 -1");

            // [MED-4] 핵심 — ApplyDeepMods를 거치지 않은 "프리뷰/preEffSpins류" mods 스냅샷으로 호출해도
            // 동일한 결과(run.Pouch 직접 참조라 mods 스냅샷에 좌우되지 않음).
            int spinsWithShackleFreshMods = SpinResolver.EffSpins(run, new Mods());
            t.Eq(spinsWithShackle, spinsWithShackleFreshMods,
                "[shackle] ApplyDeepMods 미적용 mods(preEffSpins/프리뷰/DeviceActions/ItemUse 경로 시뮬레이션)로도 동일 스핀수");

            run.Stage = 11; // 보스 아님(run은 여전히 shackle 보유)
            var mods2 = new Mods();
            DeepRunHooks.ApplyDeepMods(mods2, run);
            t.True(mods2.shackleActive, "[shackle:non-boss] shackleActive는 여전히 true(보유 기준)");
            t.Eq(SpinResolver.EffSpins(run, new Mods()), SpinResolver.EffSpins(run, mods2), "[shackle:non-boss] 비보스 스테이지는 스핀 감소 없음(동일 run, mods 스냅샷 무관 동일 결과)");
        }

        private static void ShieldBlocksBossPenaltyExemptSkipsRule(TestCtx t)
        {
            // "luck" 보스(stage15) — ⭐👑🌀 없으면 ×0.8 페널티. LockedNext로 결과 완전 결정.
            var lockedIds = new List<string> { "cherry", "cherry", "gem", "book", "coin" };

            var control = MakeDeepRun(40); control.Stage = 15;
            control.LockedNext.AddRange(lockedIds);
            var outControl = SpinResolver.ResolveSpin(control, SpinMode.N);
            t.True(outControl.notes.Any(n => n.Contains("노희귀")), "[shield-exempt:control] 희귀 없음 페널티 발동 확인");

            var shielded = MakeDeepRun(40); shielded.Stage = 15; shielded.BossShield = true;
            shielded.LockedNext.AddRange(lockedIds);
            var outShielded = SpinResolver.ResolveSpin(shielded, SpinMode.N);
            t.True(outShielded.notes.Any(n => n.Contains("방패")), "[shield] 방패 발동 노트");
            t.False(shielded.BossShield, "[shield] 소비됨");
            t.Eq(outShielded.result.exp, outShielded.gained, "[shield] 페널티 방어 -> 원래 EXP 유지");

            var exempt = MakeDeepRun(40); exempt.Stage = 15; exempt.BossExempt = true;
            exempt.LockedNext.AddRange(lockedIds);
            var outExempt = SpinResolver.ResolveSpin(exempt, SpinMode.N);
            t.True(outExempt.notes.Any(n => n.Contains("감점룰 무시")), "[exempt] 시험지 발동 노트");
            t.False(exempt.BossExempt, "[exempt] 소비됨");
            t.Eq(outExempt.result.exp, outExempt.gained, "[exempt] 감점룰 무시 -> 원래 EXP 유지");
        }

        // ══════════════════════════════════════════════════════════════════
        // Shop.cs 상점 훅
        // ══════════════════════════════════════════════════════════════════
        private static void ShopReceiptDiscountAppliesToAugmentPrices(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var run = MakeDeepRun(50); run.Phase = RunPhase.EventShop; run.Stage = 1;
            run.DeepShopDiscount = true;
            var offer = Shop.FreshOffer(run, stat);
            int checkedCount = 0;
            foreach (var e in offer.Where(e => e.kind == 'A'))
            {
                var perk = Perks.ById(e.id);
                int basePrice = perk.tier == Tier.SILVER ? 14 : perk.tier == Tier.GOLD ? 24 : 36;
                int expected = Math.Max(1, (int)Math.Round(basePrice * 0.9, MidpointRounding.AwayFromZero));
                t.Eq(expected, e.price, $"[shop:receipt] {e.id} 영수증 -10% 가격 반영");
                checkedCount++;
            }
            t.True(checkedCount > 0, "[shop:receipt] 증강 항목 최소 1개 검증");
        }

        private static void ShopCartBonusIncreasesItemSlots(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var run = MakeDeepRun(51); run.Phase = RunPhase.EventShop; run.Stage = 1;
            run.DeepShopSlotBonus = 2;
            var offer = Shop.FreshOffer(run, stat);
            int itemCount = offer.Count(e => e.kind == 'I');
            t.Eq(4, itemCount, "[shop:cart] 기본 2 + 장바구니 2 = 4칸");
        }

        private static void ShopCouponTagsOneEntryWith15PercentOff(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var run = MakeDeepRun(52); run.Phase = RunPhase.EventShop; run.Stage = 1;
            run.DeepShopCoupon = true;
            var offer = Shop.FreshOffer(run, stat);
            t.Eq(1, offer.Count(e => e.couponTag), "[shop:coupon] 정확히 1개 항목에 쿠폰 표시");
        }

        private static void ShopBlackCardFreeFirstPurchase(TestCtx t)
        {
            var run = MakeDeepRun(53);
            run.Phase = RunPhase.EventShop;
            run.BlackCardShopFree = true;
            run.Coins = 0;
            var itemId = Items.All[0].id;
            run.ShopOffer.Add(new ShopEntry { kind = 'I', id = itemId, price = 999 });
            var ev = Shop.Buy(run, 0);
            t.Eq("SHOP_PURCHASED", ev[0].type, "[shop:blackcard] 구매 성공(코인 0이어도)");
            t.Eq(0, run.Coins, "[shop:blackcard] 코인 미차감");
            t.False(run.BlackCardShopFree, "[shop:blackcard] 무료 플래그 소비");
        }

        private static void NodeEventsShopEntryConsumesCrystalAndBlackCard(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var run = MakeDeepRun(54);
            run.Phase = RunPhase.NodeSelect;
            run.NodeOptions.Add(NodeKind.Shop);
            run.DeepCrystalPending = 2;
            run.Pouch["black_card"] = 1;
            NodeEvents.ChooseNode(run, 0, stat);
            t.Eq(RunPhase.EventShop, run.Phase, "[node:shop-entry] 상점 진입");
            t.Eq(2, run.DeepRewardBonus, "[node:shop-entry] 수정구 예약치 -> 보상 후보 이관");
            t.Eq(0, run.DeepCrystalPending, "[node:shop-entry] 예약치 소진");
            t.True(run.BlackCardShopFree, "[node:shop-entry] 검은카드 무료 플래그 세팅");
            t.False(run.Pouch.ContainsKey("black_card"), "[node:shop-entry] 검은카드 덱 소비");
            t.False(run.DeepShopDiscount, "[node:shop-entry] 영수증/쿠폰/장바구니 플래그는 진열 직후 리셋");
        }

        // ══════════════════════════════════════════════════════════════════
        // StageFlow.cs — safepin AUGLEVEL pity
        // ══════════════════════════════════════════════════════════════════
        private static ClearOutcome DirectDeepClear(RunState run, long quota)
        {
            var outcome = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N,
                newExp = quota, newScore = run.Score, newCoins = run.Coins,
                newSpinIndex = 5, quota = quota, spins = 5,
            };
            return StageFlow.ClearStage(run, outcome);
        }

        private static void SafePinBoostsAugLevelPityWhenPityFails(TestCtx t)
        {
            bool found = false;
            for (long seed = 1; seed < 400 && !found; seed++)
            {
                var runA = MakeDeepRun(seed);
                runA.Perks.Add("study");
                runA.Stage = 3;
                runA.AugLevelChance = 0.10;

                var runB = MakeDeepRun(seed);
                runB.Perks.Add("study");
                runB.Stage = 3;
                runB.AugLevelChance = 0.10;
                runB.Pouch["safepin"] = 1;
                runB.SafePinActive = true;

                DirectDeepClear(runA, 100);
                DirectDeepClear(runB, 100);

                if (Math.Abs(runA.AugLevelChance - runB.AugLevelChance) < 1e-9) continue; // 이 시드는 second!=SYMAUG였거나 pity 성공(둘 다 리셋) — 다음 시드로.

                found = true;
                t.EqTol(runA.AugLevelChance + 0.01, runB.AugLevelChance, $"[safepin-pity seed={seed}] pity 실패 시 안전핀노트가 +1%p 추가");
                t.Eq(0, runB.Pouch.TryGetValue("safepin", out var spn) ? spn : 0, $"[safepin-pity seed={seed}] 안전핀노트 소비(덱 제거)");
            }
            t.True(found, "[safepin-pity] 400시드 내 second==SYMAUG && pity실패 분기 최소 1회 발견");
        }

        // ══════════════════════════════════════════════════════════════════
        // 심화 자동플레이 스모크 — 신규 효과 관측 카운트 리포트
        // ══════════════════════════════════════════════════════════════════
        private static RunController NewDeepController(long seed) =>
            new RunController("novice", "basic", "", seed, S4TestHelpers.GenerousStat(), asc: 0, deep: true);

        private static void DeepAutoplaySmokeObservesNewEffects(TestCtx t)
        {
            var counts = new Dictionary<string, int>
            {
                ["정화"] = 0, ["미러"] = 0, ["강화"] = 0, ["마법봉 와일드"] = 0, ["빈칸"] = 0,
                ["표적"] = 0, ["퍼즐"] = 0, ["피방울"] = 0, ["검은초"] = 0, ["대폭발"] = 0, ["불발"] = 0,
                ["붕대"] = 0, ["매듭"] = 0, ["에너지팩"] = 0, ["가짜왕관"] = 0, ["진화핵"] = 0,
                ["럭키7"] = 0, ["프리즘"] = 0, ["알람"] = 0, ["톱니"] = 0, ["모래시계"] = 0,
                ["영수증"] = 0, ["쿠폰"] = 0, ["장바구니"] = 0, ["방패"] = 0, ["시험지"] = 0,
                ["배터리"] = 0, ["정비키트"] = 0, ["형광펜"] = 0, ["복습책"] = 0, ["세트조각"] = 0,
                ["저주눈"] = 0, ["검은카드"] = 0, ["수정구"] = 0, ["안전핀"] = 0, ["임시와일드"] = 0,
                ["운명의소용돌이"] = 0,
            };
            int spinCount = 0;

            // Opus 2차검수(P7-3b) [LOW 일괄] — 기본 시작덱(9종)만으로는 신규 39종이 POUCH 오퍼로 "먼저
            // 획득"돼야만 등장 가능해 짧은 자동플레이 표본에서 관측률이 지나치게 낮았다(직전 제출본은
            // 대부분 0회). 순수 스모크 관측 목적으로 신규 특수심볼을 시작 주머니에 소량 직접 심어
            // 넣는다(밸런스/덱검증 규칙 무관 — 이 스모크 루프는 RepairBuy를 쓰지 않아 Pouch.Validate
            // 상한을 거치지 않는다).
            var seedSymbols = new[]
            {
                "purifier", "mirror_sym", "catalyst", "magic_wand", "target", "jigsaw",
                "bloodrop", "black_candle_sym", "unstable_bomb",
                "bandage", "knot", "energypack", "fake_crown_sym", "evo_core",
                "lucky7", "prism_sym",
                "alarm", "gear", "hourglass", "receipt", "coupon", "cart",
                "shield", "exam_paper", "battery_sym", "repair_kit",
                "highlighter", "review_book", "set_piece",
                "curse_eye", "black_card", "crystal", "safepin", "temp_wild", "fate_vortex", "shackle",
            };

            for (long seed = 30001; seed < 30011; seed++)
            {
                var rc = NewDeepController(seed);
                foreach (var id in seedSymbols)
                    rc.State.Pouch[id] = rc.State.Pouch.TryGetValue(id, out var cur) ? cur + 2 : 2;
                var pickRng = new Rng(seed * 7919 + 13);
                int guard = 0;
                int shopStep = 0;
                while (rc.State.Phase != RunPhase.GameOver && guard < 20_000)
                {
                    guard++;
                    switch (rc.State.Phase)
                    {
                        case RunPhase.Spin:
                            rc.Do(new Spin(SpinMode.N));
                            spinCount++;
                            foreach (var note in rc.State.LastNotes)
                                foreach (var key in counts.Keys.ToList())
                                    if (note.Contains(key)) counts[key]++;
                            break;
                        case RunPhase.PostSpin: rc.Do(new Continue()); break;
                        case RunPhase.NodeSelect:
                        {
                            int nodeIdx = pickRng.Next(Math.Max(1, rc.State.NodeOptions.Count));
                            rc.Do(new ChooseNode(nodeIdx));
                            break;
                        }
                        case RunPhase.EventAugment:
                        case RunPhase.EventRelic:
                        case RunPhase.EventAugLevel:
                        case RunPhase.EventPouch:
                        case RunPhase.EventPouchCost:
                        case RunPhase.EventPouchRemove:
                        case RunPhase.EventRestDeep:
                        case RunPhase.EventGambleDeep:
                        case RunPhase.EventSynAugBonus:
                            rc.Do(new PickOffer(0));
                            break;
                        case RunPhase.EventShop:
                            if (shopStep == 0) { rc.Do(new BuyOffer(0)); shopStep = 1; }
                            else { rc.Do(new LeaveShop()); shopStep = 0; }
                            break;
                        case RunPhase.DeviceNode: rc.Do(new TakeDevice(true)); break;
                        case RunPhase.RewardDone: rc.Do(new ProceedToStage()); break;
                        default: throw new InvalidOperationException($"[deep-smoke-p73b seed={seed}] 처리 불가 Phase=" + rc.State.Phase);
                    }
                    t.True(rc.State.Score >= 0, $"[deep-smoke-p73b seed={seed}] Score 음수 아님");
                    t.True(rc.State.Coins >= 0, $"[deep-smoke-p73b seed={seed}] Coins 음수 아님");
                    t.True(!double.IsNaN(rc.State.Score) && !double.IsNaN(rc.State.StageExp), $"[deep-smoke-p73b seed={seed}] NaN 아님");
                    t.True(rc.State.UnluckyGauge >= 0, $"[deep-smoke-p73b seed={seed}] UnluckyGauge 음수 아님");
                    t.True(rc.State.DeepShopSlotBonus >= 0 && rc.State.DeepShopSlotBonus <= 2, $"[deep-smoke-p73b seed={seed}] DeepShopSlotBonus 상한 준수");
                    t.True(rc.State.DeepRewardBonus >= 0 && rc.State.DeepRewardBonus <= 2, $"[deep-smoke-p73b seed={seed}] DeepRewardBonus 상한 준수");
                    foreach (var kv in rc.State.Pouch)
                        t.True(kv.Value >= 0, $"[deep-smoke-p73b seed={seed}] Pouch[{kv.Key}] 음수 아님");
                }
                t.Eq(RunPhase.GameOver, rc.State.Phase, $"[deep-smoke-p73b seed={seed}] 예외 없이 게임오버까지 진행(stage={rc.State.Stage})");
            }

            t.Report("[deep-smoke-p73b] 총 스핀 수", spinCount.ToString());
            foreach (var kv in counts)
                t.Report($"[deep-smoke-p73b] '{kv.Key}' 관측 횟수", kv.Value.ToString());
        }
    }
}
