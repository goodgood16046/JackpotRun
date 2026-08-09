using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // TestCtx는 True/Eq/EqTol만 제공한다(False 없음, Program.cs 확인) — 이 파일에서만 쓰는 작은 편의
    // 확장. 원본 TestCtx는 다른 모든 테스트 파일이 공유하는 하네스라 손대지 않는다.
    internal static class TestCtxExt
    {
        public static void False(this TestCtx t, bool condition, string label) => t.True(!condition, label);
    }

    // P7-3 골든/단위 테스트 — 심화모드 3/4 슬라이스, WEB_PARITY_DESIGN.md §1-A #19(잭팟태그·피버·자동
    // 소멸·POUCH 오퍼 v3 2-step·심화 노드 풀·퍼펙트 드로우·3스테이지 연계 보너스). 웹 engine.js:768-870
    // (잭팟태그 evaluate)·1725-1863(offerSymbolRewards)·game.js:960-1138(스핀 후속)·1439-1494(노드
    // 풀)·1516-1542(자동소멸)·1890-2037(POUCH 2-step)·2152-2184(3스테이지 연계) 대조.
    internal static class Tests_P7_3_JackpotFeverOffer
    {
        private static readonly Rng UnusedRng = new Rng(1);

        public static void Run(TestCtx t)
        {
            // ── §9.0 J1 잭팟 태그 3단계 ──
            JackpotTagCombo(t);
            JackpotTagReachWithBellAmplifiers(t);
            JackpotTagJackpotWithCrownSignal(t);
            JackpotTagDedupWithSameSymbolJackpot(t);
            JackpotTagIsolatedFromNormalMode(t);

            // ── 증폭 심볼(환호·대폭죽·슬롯조각·잭팟마법봉) ──
            AmplifierCheerBoostsComboExp(t);
            AmplifierBigBoomAddsScore(t);
            AmplifierSlotShardPromotesToCombo(t);
            AmplifierJackpotWandPromotesToCombo(t);

            // ── 리치 bias(§9.0 J1) ──
            ReachBiasAppliesMultiplierAndConsumes(t);

            // ── 피버 게이지(§9.1 J2) ──
            FeverChargeOnly(t);
            FeverTriggerAtMax(t);
            FeverEffectDuringActiveSpins(t);
            FeverEndsAtZeroSpins(t);
            FeverJackpotDoublesScore(t);

            // ── 승격/보정 심볼(§9.2 J3) ──
            PromotionBellTicket(t);
            PromotionJackpotTicketRunLimit(t);
            PromotionReachMarkProbability(t);
            PromotionRetryReelStageLimit(t);
            PromotionJackpotCrownStageLimit(t);

            // ── 배치F P6 퍼펙트 드로우 ──
            PerfectDrawTriggersOncePerStage(t);
            PerfectDrawRequiresAllRealCells(t);

            // ── 심볼퍽 희귀표본상자(rareFirstScore) ──
            RareFirstScoreOnNewRareSymbol(t);

            // ── §3 V3P3 자동 소멸 ──
            AutoDecayForewarnAtStage14(t);
            AutoDecayRemovesBaseSymbolAtStage15Plus(t);
            AutoDecaySkipsWhenNoBaseTargets(t);

            // ── §2 V3P2 POUCH 오퍼 v3 티어 시퀀스 ──
            OfferBoss5ForcesPrismThenGold(t);
            Offer3xForcesGoldThenSilver(t);
            OfferForcePrismFirstOverridesNormal(t);
            OfferCardCostStructure(t);

            // ── POUCH 오퍼 2-step 커밋(RunController 경유) ──
            PouchCommitSilverRemovesOneBase(t);
            PouchCommitCurseIsFree(t);
            PouchCommitSkipGrantsCoins(t);
            PouchCommitPrismCostRemoveFlow(t);
            PouchCommitPrismCostCurseFlow(t);
            PouchCommitRollbackOnInvalidRemainsUnchanged(t);

            // ── 심화 노드 풀 분포 ──
            DeepNodePoolAlwaysStartsWithPouch(t);
            DeepNodePoolJackpotOnlyFromStage3(t);
            DeepNodePoolCurseRiskOnlyFromStage6(t);
            DeepNodePoolNeverUsesNormalPool(t);

            // ── 3스테이지 연계 보너스 ──
            SynAugBonusOffersMatchingTagSymbol(t);

            // ── Opus 2차검수(P7-3) 필수③ — 노드 진입 4종 전용 테스트 ──
            EnterSymAugOrRelOfferAndPickRoundTrip(t);
            EnterJackpotNodeCandidatePoolCoinIncludedCrownExcluded(t);
            PickRestDeepChoosesCoinOrPurify(t);
            PickGambleDeepChoosesCoinOrSym(t);

            // ── 심화 자동플레이 스모크 확장(전 신기능 통과·불변식) ──
            DeepAutoplaySmokeAllNewFeatures(t);
        }

        // ══════════════════════════════════════════════════════════════════
        // 헬퍼
        // ══════════════════════════════════════════════════════════════════
        private static Cell C(string id) => new Cell(Symbols.ById(id));

        private static List<Cell> Cells(params string[] ids) => ids.Select(C).ToList();

        private static Mods DeepMods() => new Mods { deepMode = true };

        private static RunState MakeDeepRun(long seed)
        {
            var run = S4TestHelpers.NewRun(seed);
            run.DeepMode = true;
            foreach (var kv in Pouch.NewStartPouch()) run.Pouch[kv.Key] = kv.Value;
            return run;
        }

        // ══════════════════════════════════════════════════════════════════
        // §9.0 J1 잭팟 태그 3단계 — 웹 engine.js:812-865
        // ══════════════════════════════════════════════════════════════════
        private static void JackpotTagCombo(TestCtx t)
        {
            var cells = Cells("coin", "coin_bag", "coin", "star", "gem"); // coin 태그 3(coin×2·coin_bag×1)
            var resNormal = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            var resDeep = SpinResolver.Evaluate(UnusedRng, cells, DeepMods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);

            t.Eq((string)null, resNormal.jackpotStage, "[jtag-combo] 일반모드는 잭팟태그 완전 격리(jackpotStage null)");
            t.Eq("combo", resDeep.jackpotStage, "[jtag-combo] coin 태그 3개 → combo");
            t.Eq("coin", resDeep.jackpotTagHit, "[jtag-combo] 최다 태그=coin");
            t.Eq(resNormal.exp + 8, resDeep.exp, "[jtag-combo] EXP+8(콤보 고정)");
            t.Eq(15, resDeep.feverDelta, "[jtag-combo] feverDelta=15");
            t.True(resDeep.notes.Any(n => n.Contains("콤보")), "[jtag-combo] 콤보 배너 노트 존재");
        }

        private static void JackpotTagReachWithBellAmplifiers(TestCtx t)
        {
            // 종 태그 4개(작은종·울림종·황금종·축제종) — 리치 + 종세트 증폭(작은종+15·황금종+30) + 울림종(+200점수).
            var cells = Cells("small_bell", "echo_bell", "golden_bell", "festival_bell", "star");
            var resNormal = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            var resDeep = SpinResolver.Evaluate(UnusedRng, cells, DeepMods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);

            t.Eq("reach", resDeep.jackpotStage, "[jtag-reach] 종 태그 4개 → reach");
            t.Eq("bell", resDeep.jackpotTagHit, "[jtag-reach] 최다 태그=bell");
            t.Eq(4, resDeep.bellCount, "[jtag-reach] bellCount=4");
            t.True(resDeep.echoTriggered, "[jtag-reach] 울림종 발동(bell 리치)");
            // 리치 점수(300, jackpotSym 없음) + 울림종(200) = 500
            t.Eq(resNormal.score + 500, resDeep.score, "[jtag-reach] 점수+500(리치300+울림종200)");
            // feverDelta = 25(리치) + 15(작은종) + 30(황금종) = 70
            t.Eq(70, resDeep.feverDelta, "[jtag-reach] feverDelta=25+15+30=70");
        }

        private static void JackpotTagJackpotWithCrownSignal(TestCtx t)
        {
            // 왕관 계열 5개(실왕관2+가짜왕관2+잭팟왕관1) — 동일심볼 잭팟은 불성립(crown 실심볼 2개뿐,
            // bestCount<5) 이지만 잭팟태그는 5개 전부 crown 태그라 태그잭팟 발동.
            var cells = Cells("crown", "fake_crown_sym", "jackpot_crown", "crown", "fake_crown_sym");
            var resNormal = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            var resDeep = SpinResolver.Evaluate(UnusedRng, cells, DeepMods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);

            t.Eq((string)null, resDeep.jackpotSym, "[jtag-jackpot] 동일심볼 잭팟 불성립(crown 실심볼 2개뿐)");
            t.Eq("jackpot", resDeep.jackpotStage, "[jtag-jackpot] crown 태그 5개 → 태그잭팟");
            t.Eq(50, resDeep.feverDelta, "[jtag-jackpot] feverDelta=50");
            t.True(resDeep.jackpotCrownSignal, "[jtag-jackpot] 잭팟왕관 신호 발동");
            t.Eq(resNormal.exp + 30, resDeep.exp, "[jtag-jackpot] EXP+30(태그잭팟 고정)");
            t.Eq(resNormal.score + 1500, resDeep.score, "[jtag-jackpot] 점수+1500(태그잭팟 고정)");
        }

        private static void JackpotTagDedupWithSameSymbolJackpot(TestCtx t)
        {
            // 왕관 5개 동일 — 동일심볼 잭팟(exp+520·score+2600)과 태그잭팟(crown 5개)이 동시 성립하지만
            // 태그잭팟 쪽 EXP/점수 지급은 스킵(중복 금지) — jackpotStage 신호·feverDelta는 그대로 반환.
            var cells = Cells("crown", "crown", "crown", "crown", "crown");
            var resNormal = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            var resDeep = SpinResolver.Evaluate(UnusedRng, cells, DeepMods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);

            t.Eq("crown", resDeep.jackpotSym, "[jtag-dedup] 동일심볼 잭팟 성립(crown×5)");
            t.Eq("jackpot", resDeep.jackpotStage, "[jtag-dedup] 태그잭팟 신호도 동시 성립");
            t.Eq(50, resDeep.feverDelta, "[jtag-dedup] feverDelta 신호는 유지(중복 EXP/점수만 금지)");
            // 동일심볼 잭팟 EXP/점수는 mods.deepMode 여부와 무관 — deep이라고 추가 지급되면 안 됨.
            t.Eq(resNormal.exp, resDeep.exp, "[jtag-dedup] EXP 중복 지급 없음(동일심볼 잭팟분만)");
            t.Eq(resNormal.score, resDeep.score, "[jtag-dedup] 점수 중복 지급 없음(동일심볼 잭팟분만)");
        }

        private static void JackpotTagIsolatedFromNormalMode(TestCtx t)
        {
            var cells = Cells("crown", "crown", "crown", "crown", "crown");
            var mods = new Mods(); // deepMode=false(기본값)
            var res = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq((string)null, res.jackpotTagHit, "[jtag-isolation] 일반모드 jackpotTagHit null");
            t.Eq((string)null, res.jackpotStage, "[jtag-isolation] 일반모드 jackpotStage null");
            t.Eq(0, res.feverDelta, "[jtag-isolation] 일반모드 feverDelta 0");
            t.False(res.hasBellFest, "[jtag-isolation] 일반모드 hasBellFest false");
        }

        // ══════════════════════════════════════════════════════════════════
        // 증폭 심볼 — 웹 engine.js:821-827·837-851
        // ══════════════════════════════════════════════════════════════════
        private static void AmplifierCheerBoostsComboExp(TestCtx t)
        {
            var cells = Cells("coin", "coin_bag", "coin", "cheer", "star"); // coin 태그 3 + 환호
            var resNormal = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            var resDeep = SpinResolver.Evaluate(UnusedRng, cells, DeepMods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("combo", resDeep.jackpotStage, "[amp-cheer] 콤보 성립");
            t.Eq(resNormal.exp + 10, resDeep.exp, "[amp-cheer] EXP+10 = floor(8×1.25)");
        }

        private static void AmplifierBigBoomAddsScore(TestCtx t)
        {
            var cells = Cells("coin", "coin_bag", "coin", "big_boom", "star");
            var resNormal = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            var resDeep = SpinResolver.Evaluate(UnusedRng, cells, DeepMods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("combo", resDeep.jackpotStage, "[amp-boom] 콤보 성립");
            t.Eq(resNormal.exp + 8, resDeep.exp, "[amp-boom] EXP+8(콤보 고정, 대폭죽은 EXP 무관)");
            t.Eq(resNormal.score + 500, resDeep.score, "[amp-boom] 점수+500(대폭죽 콤보)");
        }

        private static void AmplifierSlotShardPromotesToCombo(TestCtx t)
        {
            // coin 태그 2개(coin·coin_bag) — 슬롯조각이 최다 태그에 +1 해 3개(콤보)로 승격.
            var cells = Cells("coin", "coin_bag", "slot_shard", "star", "gem");
            var resDeep = SpinResolver.Evaluate(UnusedRng, cells, DeepMods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("combo", resDeep.jackpotStage, "[amp-slotshard] 2개+슬롯조각 → 3개 콤보 승격");
            t.Eq("coin", resDeep.jackpotTagHit, "[amp-slotshard] 승격된 태그=coin");
            t.True(resDeep.notes.Any(n => n.Contains("슬롯조각")), "[amp-slotshard] 슬롯조각 노트 존재");
        }

        private static void AmplifierJackpotWandPromotesToCombo(TestCtx t)
        {
            var cells = Cells("coin", "coin_bag", "jackpot_wand", "star", "gem");
            var resDeep = SpinResolver.Evaluate(UnusedRng, cells, DeepMods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false);
            t.Eq("combo", resDeep.jackpotStage, "[amp-wand] 2개+잭팟마법봉 → 3개 콤보 승격");
            t.True(resDeep.notes.Any(n => n.Contains("잭팟마법봉")), "[amp-wand] 잭팟마법봉 노트 존재");
        }

        // ══════════════════════════════════════════════════════════════════
        // 리치 bias — 웹 game.js:663-672
        // ══════════════════════════════════════════════════════════════════
        private static void ReachBiasAppliesMultiplierAndConsumes(TestCtx t)
        {
            var run = MakeDeepRun(7001L);
            run.ReachBiasTag = "coin";
            run.ReachBiasSpinsLeft = 1;
            var bias = new PouchBias();
            DeepRunHooks.ApplyReachBias(bias, run);
            t.EqTol(1.5, bias.Mul.TryGetValue("coin", out var m1) ? m1 : 0, "[reachbias] coin ×1.5");
            t.EqTol(1.5, bias.Mul.TryGetValue("coin_bag", out var m2) ? m2 : 0, "[reachbias] coin_bag ×1.5(동일 태그)");
            t.False(bias.Mul.ContainsKey("star"), "[reachbias] 무관 심볼 미적용");
            t.Eq((string)null, run.ReachBiasTag, "[reachbias] 1스핀 소진 후 리셋");
            t.Eq(0, run.ReachBiasSpinsLeft, "[reachbias] spinsLeft 0");

            // 미설정 상태는 무영향.
            var run2 = MakeDeepRun(7002L);
            var bias2 = new PouchBias();
            DeepRunHooks.ApplyReachBias(bias2, run2);
            t.True(bias2.IsNoop, "[reachbias] 리치 미발동 상태는 bias 무변화");
        }

        // ══════════════════════════════════════════════════════════════════
        // §9.1 J2 피버 게이지 — 웹 game.js:1069-1138
        // ══════════════════════════════════════════════════════════════════
        private static void FeverChargeOnly(TestCtx t)
        {
            var run = MakeDeepRun(7101L);
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, feverDelta = 15 };
            long gained = 10;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.EqTol(15, run.FeverGauge, "[fever-charge] 게이지 15 충전");
            t.Eq(0, run.FeverSpins, "[fever-charge] 미도달(feverSpins 0)");
        }

        private static void FeverTriggerAtMax(TestCtx t)
        {
            var run = MakeDeepRun(7102L);
            run.FeverGauge = 90;
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, feverDelta = 15 };
            long gained = 10;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.EqTol(0, run.FeverGauge, "[fever-trigger] 90+15>=100 → 리셋");
            t.Eq(3, run.FeverSpins, "[fever-trigger] feverSpins=3");
            t.True(notes.Any(n => n.Contains("피버 타임")), "[fever-trigger] 피버 타임 배너");
            t.EqTol(Pouch.FeverReachFixAmount, mods.feverReachFix, "[fever-trigger] feverReachFix 노출");
        }

        private static void FeverEffectDuringActiveSpins(TestCtx t)
        {
            var run = MakeDeepRun(7103L);
            run.FeverSpins = 2;
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 50, feverDelta = 0 };
            long gained = 100;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.Eq(130L, gained, "[fever-effect] EXP×1.30 = 100+floor(100×0.3)");
            t.Eq(75L, res.score, "[fever-effect] 점수×1.50 = 50+floor(50×0.5)");
            t.Eq(1, run.FeverSpins, "[fever-effect] feverSpins 1 감소");
            t.True(notes.Any(n => n.Contains("피버") && n.Contains("스핀 남음")), "[fever-effect] 잔여 스핀 배너");
        }

        private static void FeverEndsAtZeroSpins(TestCtx t)
        {
            var run = MakeDeepRun(7104L);
            run.FeverSpins = 1;
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, feverDelta = 0 };
            long gained = 0;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.Eq(0, run.FeverSpins, "[fever-end] feverSpins 0으로 종료");
            t.True(notes.Any(n => n.Contains("피버 종료")), "[fever-end] 종료 배너");
            t.EqTol(0.0, mods.feverReachFix, "[fever-end] feverReachFix 리셋");
        }

        private static void FeverJackpotDoublesScore(TestCtx t)
        {
            var run = MakeDeepRun(7105L);
            run.FeverSpins = 2;
            var mods = DeepMods();
            var res = new SpinResult
            {
                cells = Cells("star", "star", "star", "star", "star"),
                score = 1000, feverDelta = 0, jackpotStage = "jackpot", jackpotTagHit = "crown", jackpotSym = null,
            };
            long gained = 0;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            // 웹 game.js:1107/1111 — feverScoreExtra(피버×1.5분)와 fjScoreBonus(피버잭팟×2분)는 둘 다
            // "이번 스핀 원본 점수"(res.score, 이 함수 진입 시점의 고정값=1000)를 기준으로 각각
            // 독립적으로 계산돼 누적 가산된다(복리 아님) — feverScoreExtra=floor(1000×0.5)=500,
            // fjScoreBonus=floor(1000×1.0)=1000 → 최종 1000+500+1000=2500.
            t.EqTol(1000 + 500 + 1000, res.score, "[fever-jackpot] 1000+500(피버)+1000(피버잭팟)=2500(원본 기준 병렬 가산, 복리 아님)");
            t.True(run.FeverJackpotPrism, "[fever-jackpot] 프리즘 보장 플래그");
            t.True(notes.Any(n => n.Contains("피버잭팟")), "[fever-jackpot] 배너 존재");
        }

        // ══════════════════════════════════════════════════════════════════
        // §9.2 J3 승격/보정 심볼
        // ══════════════════════════════════════════════════════════════════
        private static void PromotionBellTicket(TestCtx t)
        {
            var run = MakeDeepRun(7201L);
            run.Pouch["bell_ticket"] = 1;
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, jackpotStage = "reach", jackpotTagHit = "bell", bellCount = 4 };
            long gained = 0;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.Eq(30L, gained, "[promo-bellticket] EXP+30 승격");
            t.Eq(1500L, res.score, "[promo-bellticket] 점수+1500 승격");
            t.Eq(1, run.BellTicketUses, "[promo-bellticket] 사용 카운트 1");
            t.False(run.Pouch.ContainsKey("bell_ticket"), "[promo-bellticket] 소모 후 제거(1개뿐)");
            t.True(run.JackpotPrismPending, "[promo-bellticket] 프리즘 보장 플래그");

            // 런 2회 제한.
            run.Pouch["bell_ticket"] = 5;
            run.BellTicketUses = 2;
            long gained2 = 0;
            var notes2 = new List<string>();
            var res2 = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, jackpotStage = "reach", jackpotTagHit = "bell", bellCount = 4 };
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res2, ref gained2, notes2);
            t.Eq(0L, gained2, "[promo-bellticket] 런 2회 초과 시 미발동");
        }

        private static void PromotionJackpotTicketRunLimit(TestCtx t)
        {
            var run = MakeDeepRun(7202L);
            run.Pouch["jackpot_ticket"] = 1;
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, jackpotStage = "reach", hasJpTicket = true };
            long gained = 0;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.Eq(30L, gained, "[promo-jpticket] EXP+30 승격");
            t.Eq(1, run.JpTicketUses, "[promo-jpticket] 사용 카운트 1");
        }

        private static void PromotionReachMarkProbability(TestCtx t)
        {
            // feverReachFix를 극단값으로 세팅해 RNG와 무관하게 성공/실패를 결정론적으로 검증.
            var runSuccess = MakeDeepRun(7203L);
            var modsSuccess = new Mods { deepMode = true, feverReachFix = 1.0 }; // baseProb=1.3 → 항상 성공
            var resSuccess = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, jackpotStage = "reach", hasReachMark = true };
            long gainedS = 0;
            var notesS = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(runSuccess, modsSuccess, resSuccess, ref gainedS, notesS);
            t.Eq(30L, gainedS, "[promo-reachmark] feverReachFix=1.0 → 항상 성공");
            t.True(runSuccess.ReachMarkUsed, "[promo-reachmark] 스테이지 1회 소진 플래그");

            var runFail = MakeDeepRun(7204L);
            var modsFail = new Mods { deepMode = true, feverReachFix = -0.30 }; // baseProb=0 → 항상 실패
            var resFail = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, jackpotStage = "reach", hasReachMark = true };
            long gainedF = 0;
            var notesF = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(runFail, modsFail, resFail, ref gainedF, notesF);
            t.Eq(0L, gainedF, "[promo-reachmark] baseProb=0 → 항상 실패");
            t.False(runFail.ReachMarkUsed, "[promo-reachmark] 실패 시 미소진");
        }

        private static void PromotionRetryReelStageLimit(TestCtx t)
        {
            var run = MakeDeepRun(7205L);
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, jackpotStage = "reach", hasRetryReel = true };
            long gained = 0;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.True(run.RetryReelPending, "[promo-retryreel] 다음 스핀 예약");
            t.True(run.RetryReelUsed, "[promo-retryreel] 스테이지 1회 소진");

            // 재발동 방지.
            run.RetryReelPending = false;
            var res2 = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, jackpotStage = "reach", hasRetryReel = true };
            long gained2 = 0;
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res2, ref gained2, new List<string>());
            t.False(run.RetryReelPending, "[promo-retryreel] 스테이지 1회 제한으로 재예약 안 됨");
        }

        private static void PromotionJackpotCrownStageLimit(TestCtx t)
        {
            var run = MakeDeepRun(7206L);
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("star", "star", "star", "star", "star"), score = 0, jackpotCrownSignal = true };
            long gained = 0;
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, new List<string>());
            t.True(run.JackpotCrownUsed, "[promo-jpcrown] 스테이지 1회 소진");
            t.True(run.JackpotCrownPending, "[promo-jpcrown] 다음 오퍼 프리즘 보장 대기");
        }

        // ══════════════════════════════════════════════════════════════════
        // 배치F P6 퍼펙트 드로우
        // ══════════════════════════════════════════════════════════════════
        private static void PerfectDrawTriggersOncePerStage(TestCtx t)
        {
            var run = MakeDeepRun(7301L);
            run.Stage = 3;
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("cherry", "cherry_ripe", "cherry", "cherry_ripe", "cherry"), score = 0 };
            long gained = 0;
            var notes = new List<string>();
            long coinsBefore = run.Coins;
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.Eq(coinsBefore + 1, run.Coins, "[perfectdraw] 코인+1(cherry/cherry_ripe 동일 계열)");
            t.Eq(3, run.PerfectDrawStage, "[perfectdraw] 스테이지 마킹");
            t.True(notes.Any(n => n.Contains("퍼펙트 드로우")), "[perfectdraw] 배너 존재");

            // 같은 스테이지 재시도 → 미발동.
            var res2 = new SpinResult { cells = Cells("cherry", "cherry", "cherry", "cherry", "cherry"), score = 0 };
            long gained2 = 0;
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res2, ref gained2, new List<string>());
            t.Eq(coinsBefore + 1, run.Coins, "[perfectdraw] 같은 스테이지 재발동 금지");
        }

        private static void PerfectDrawRequiresAllRealCells(TestCtx t)
        {
            var run = MakeDeepRun(7302L);
            run.Stage = 5;
            var mods = DeepMods();
            long coinsBefore = run.Coins;

            var mixed = new SpinResult { cells = Cells("cherry", "cherry", "star", "cherry", "cherry"), score = 0 };
            long g1 = 0;
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, mixed, ref g1, new List<string>());
            t.Eq(coinsBefore, run.Coins, "[perfectdraw] 계열 불일치 → 미발동");

            var withEmpty = new SpinResult { cells = Cells("cherry", "cherry", "empty", "cherry", "cherry"), score = 0 };
            long g2 = 0;
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, withEmpty, ref g2, new List<string>());
            t.Eq(coinsBefore, run.Coins, "[perfectdraw] 빈칸 포함 → 미발동");
        }

        // ══════════════════════════════════════════════════════════════════
        // 심볼퍽 희귀표본상자
        // ══════════════════════════════════════════════════════════════════
        private static void RareFirstScoreOnNewRareSymbol(TestCtx t)
        {
            var run = MakeDeepRun(7401L);
            run.Perks.Add("sr_rare_case"); // rareFirstScore=300
            t.Eq("희귀", Pouch.RarityOf("wild"), "[rarefirst] wild는 희귀 등급(전제조건)");
            var mods = DeepMods();
            var res = new SpinResult { cells = Cells("wild", "star", "star", "gem", "coin"), score = 100 };
            long gained = 0;
            var notes = new List<string>();
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res, ref gained, notes);
            t.Eq(400L, res.score, "[rarefirst] 희귀 첫 발견 점수+300");
            t.True(notes.Any(n => n.Contains("희귀표본")), "[rarefirst] 배너 존재");

            // 이미 발견한 경우(RunSymCounts에 존재) → 미발동.
            run.RunSymCounts["wild"] = 3;
            var res2 = new SpinResult { cells = Cells("wild", "star", "star", "gem", "coin"), score = 100 };
            long gained2 = 0;
            DeepRunHooks.ProcessDeepSpinFollowups(run, mods, res2, ref gained2, new List<string>());
            t.Eq(100L, res2.score, "[rarefirst] 이미 발견한 희귀는 재지급 없음");
        }

        // ══════════════════════════════════════════════════════════════════
        // §3 V3P3 자동 소멸 — 웹 game.js:1516-1542
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

        private static void AutoDecayForewarnAtStage14(TestCtx t)
        {
            var run = MakeDeepRun(8001L);
            run.Stage = 14;
            var clear = DirectDeepClear(run, 100);
            t.True(run.DecayForewarned, "[decay-forewarn] stage14 클리어 시 예고 플래그 세팅");
            t.True(!string.IsNullOrEmpty(clear.decayBanner), "[decay-forewarn] 예고 배너 존재");
            t.True(clear.decayBanner.Contains("사라집니다"), "[decay-forewarn] 예고 문구");
        }

        private static void AutoDecayRemovesBaseSymbolAtStage15Plus(TestCtx t)
        {
            var run = MakeDeepRun(8002L);
            run.Stage = 15;
            run.DeepStats = new DeepStats(); // MakeDeepRun은 RunController를 거치지 않아 기본 null — 직접 세팅.
            run.Pouch.Clear();
            run.Pouch["cherry"] = 3; // base 대상 — 유일 후보라 결정론적으로 이 심볼이 -1됨
            run.Pouch["skull"] = 5;  // harmful — 절대 제외 대상
            var clear = DirectDeepClear(run, 100);
            t.Eq(2, run.Pouch["cherry"], "[decay-remove] base 심볼 1개 감소");
            t.Eq(5, run.Pouch["skull"], "[decay-remove] harmful 심볼은 불변");
            t.True(clear.decayBanner.Contains("낡아 사라졌습니다"), "[decay-remove] 소멸 배너 문구");
            t.Eq(1, run.DeepStats.AutoDecays, "[decay-remove] AutoDecays 카운터 +1");
        }

        private static void AutoDecaySkipsWhenNoBaseTargets(TestCtx t)
        {
            var run = MakeDeepRun(8003L);
            run.Stage = 16;
            run.Pouch.Clear();
            run.Pouch["skull"] = 3; // base 대상 전무(특수 덱 완성 상태 가정)
            var before = new Dictionary<string, int>(run.Pouch);
            var clear = DirectDeepClear(run, 100);
            t.Eq(before["skull"], run.Pouch["skull"], "[decay-skip] 대상 0개면 무변화");
            t.Eq("", clear.decayBanner, "[decay-skip] 배너 없음");
        }

        // ══════════════════════════════════════════════════════════════════
        // §2 V3P2 POUCH 오퍼 v3 — 웹 engine.js:1725-1863
        // ══════════════════════════════════════════════════════════════════
        private static void OfferBoss5ForcesPrismThenGold(TestCtx t)
        {
            var rng = new Rng(9001L);
            var pouch = Pouch.NewStartPouch();
            // realStage = stage+1 = 5(보스) → 첫 카드 PRISM 보장, 나머지 GOLD. NoCurseAdds로 저주 5%
            // 혼입을 배제해 첫 슬롯 티어를 결정론적으로 만든다(혼입 자체는 별도 관심사 아님).
            var cards = PouchOffer.OfferSymbolRewards(rng, pouch, stage: 4, opts: new PouchOffer.Options { NoCurseAdds = true });
            var special = cards.Where(c => c.Type == PouchCardType.Special).ToList();
            t.True(special.Count >= 1, "[offer-boss5] 특수 카드 최소 1장");
            t.Eq("PRISM", special[0].Tier, "[offer-boss5] 첫 카드 PRISM 보장");
            var skip = cards.Last();
            t.Eq(PouchCardType.Skip, skip.Type, "[offer] 마지막 카드는 항상 skip");
            t.Eq(5, skip.CoinBonus, "[offer] skip 코인+5");
        }

        private static void Offer3xForcesGoldThenSilver(TestCtx t)
        {
            var rng = new Rng(9002L);
            var pouch = Pouch.NewStartPouch();
            // realStage = stage+1 = 3(3배수, 보스아님) → 첫 카드 GOLD 보장(저주 혼입 없게 noCurseAdds).
            var opts = new PouchOffer.Options { NoCurseAdds = true };
            var cards = PouchOffer.OfferSymbolRewards(rng, pouch, stage: 2, opts: opts);
            var special = cards.Where(c => c.Type == PouchCardType.Special).ToList();
            t.Eq("GOLD", special[0].Tier, "[offer-3x] 첫 카드 GOLD 보장");
        }

        private static void OfferForcePrismFirstOverridesNormal(TestCtx t)
        {
            var rng = new Rng(9003L);
            var pouch = Pouch.NewStartPouch();
            // realStage=2(일반) — forcePrismFirst 없으면 PRISM 보장 안 됨. 있으면 첫 카드 PRISM.
            var opts = new PouchOffer.Options { ForcePrismFirst = true, NoCurseAdds = true };
            var cards = PouchOffer.OfferSymbolRewards(rng, pouch, stage: 1, opts: opts);
            var special = cards.Where(c => c.Type == PouchCardType.Special).ToList();
            t.Eq("PRISM", special[0].Tier, "[offer-forceprism] 태그잭팟/피버잭팟/왕관 신호 → 첫 카드 PRISM");
        }

        private static void OfferCardCostStructure(TestCtx t)
        {
            var rng = new Rng(9004L);
            var pouch = Pouch.NewStartPouch();
            var opts = new PouchOffer.Options { ForcePrismFirst = true, NoCurseAdds = true };
            var cards = PouchOffer.OfferSymbolRewards(rng, pouch, stage: 1, opts: opts);
            var prism = cards.First(c => c.Type == PouchCardType.Special && c.Tier == "PRISM");
            t.Eq(2, prism.RemoveN, "[offer-cost] 프리즘 removeN=2");
            t.True(prism.OrCurse, "[offer-cost] 프리즘 저주경로 선택 가능");
            t.False(prism.Free, "[offer-cost] 프리즘은 무료 아님");
        }

        // ══════════════════════════════════════════════════════════════════
        // POUCH 2-step 커밋(RunController 경유) — 웹 game.js:1890-2037
        // ══════════════════════════════════════════════════════════════════
        private static RunController NewDeepController(long seed) =>
            new RunController("novice", "basic", "", seed, S4TestHelpers.GenerousStat(), asc: 0, deep: true);

        private static void GoToPouchOffer(RunController rc)
        {
            // NodeSelect까지 강제 이동(스핀을 반복해 스테이지1을 클리어시키는 대신, 직접 클리어 상태로
            // 세팅 — 이 테스트는 오퍼 커밋 로직만 검증 대상이라 클리어 경로 자체는 관심사가 아니다).
            rc.State.Phase = RunPhase.NodeSelect;
            rc.State.NodeOptions.Clear();
            rc.State.NodeOptions.Add(NodeKind.Pouch);
        }

        private static void PouchCommitSilverRemovesOneBase(TestCtx t)
        {
            var rc = NewDeepController(10001L);
            GoToPouchOffer(rc);
            rc.Do(new ChooseNode(0));
            t.Eq(RunPhase.EventPouch, rc.State.Phase, "[pouch-silver] EventPouch 진입");
            int silverIdx = rc.State.PouchOptions.FindIndex(c => c.Type == PouchCardType.Special && c.Tier == "SILVER");
            t.True(silverIdx >= 0, "[pouch-silver] 실버 카드 존재(9종 심볼 시작덱은 실버 후보 다수)");
            if (silverIdx < 0) return;
            string targetId = rc.State.PouchOptions[silverIdx].Id;
            int cherryBefore = rc.State.Pouch.TryGetValue("cherry", out var cb) ? cb : 0;
            rc.Do(new PickOffer(silverIdx));
            t.Eq(RunPhase.EventPouchRemove, rc.State.Phase, "[pouch-silver] 제거 대상 선택 대기");
            int removeIdx = rc.State.RemoveCandidateIds.IndexOf("cherry");
            t.True(removeIdx >= 0, "[pouch-silver] cherry는 시작덱 base 후보");
            rc.Do(new PickOffer(removeIdx));
            t.Eq(RunPhase.RewardDone, rc.State.Phase, "[pouch-silver] 커밋 후 RewardDone");
            t.Eq(cherryBefore - 1, rc.State.Pouch.TryGetValue("cherry", out var ca) ? ca : 0, "[pouch-silver] cherry -1");
            t.True(rc.State.Pouch.TryGetValue(targetId, out var tv) && tv >= 1, "[pouch-silver] 대상 심볼 +1 이상");
            t.True(rc.State.DeepPity != null && rc.State.DeepPity.Id == targetId, "[pouch-silver] deepPity 예약");
        }

        private static void PouchCommitCurseIsFree(TestCtx t)
        {
            var rc = NewDeepController(10002L);
            GoToPouchOffer(rc);
            rc.Do(new ChooseNode(0));
            int curseIdx = rc.State.PouchOptions.FindIndex(c => c.Type == PouchCardType.Special && c.Tier == "CURSE");
            if (curseIdx < 0) { t.True(true, "[pouch-curse] 이번 시드는 저주 카드 미등장(5% 확률) — 스킵"); return; }
            string curseId = rc.State.PouchOptions[curseIdx].Id;
            int before = rc.State.Pouch.TryGetValue(curseId, out var b) ? b : 0;
            rc.Do(new PickOffer(curseIdx));
            t.Eq(RunPhase.RewardDone, rc.State.Phase, "[pouch-curse] 즉시 커밋(제거 없이)");
            t.Eq(before + 1, rc.State.Pouch.TryGetValue(curseId, out var a) ? a : 0, "[pouch-curse] 무료 +1");
        }

        private static void PouchCommitSkipGrantsCoins(TestCtx t)
        {
            var rc = NewDeepController(10003L);
            GoToPouchOffer(rc);
            rc.Do(new ChooseNode(0));
            int skipIdx = rc.State.PouchOptions.Count - 1;
            long coinsBefore = rc.State.Coins;
            rc.Do(new PickOffer(skipIdx));
            t.Eq(coinsBefore + 5, rc.State.Coins, "[pouch-skip] 코인+5");
            t.Eq(RunPhase.RewardDone, rc.State.Phase, "[pouch-skip] RewardDone 전환");
        }

        private static void PouchCommitPrismCostRemoveFlow(TestCtx t)
        {
            // forcePrismFirst 신호를 직접 주입해 PRISM 카드를 결정론적으로 오퍼에 포함시킨다.
            var rc = NewDeepController(10004L);
            rc.State.JackpotPrismPending = true;
            GoToPouchOffer(rc);
            rc.Do(new ChooseNode(0));
            int prismIdx = rc.State.PouchOptions.FindIndex(c => c.Type == PouchCardType.Special && c.Tier == "PRISM");
            t.True(prismIdx >= 0, "[pouch-prism-remove] forcePrismFirst → PRISM 카드 보장");
            if (prismIdx < 0) return;
            string prismId = rc.State.PouchOptions[prismIdx].Id;
            rc.Do(new PickOffer(prismIdx));
            t.Eq(RunPhase.EventPouchCost, rc.State.Phase, "[pouch-prism-remove] 비용 방식 선택 대기");
            rc.Do(new PickOffer(0)); // cost_remove
            t.Eq(RunPhase.EventPouchRemove, rc.State.Phase, "[pouch-prism-remove] 제거 대상 선택으로 이어짐");
            int removeIdx = rc.State.RemoveCandidateIds.IndexOf("cherry");
            int cherryBefore = rc.State.Pouch["cherry"];
            rc.Do(new PickOffer(removeIdx));
            t.Eq(RunPhase.RewardDone, rc.State.Phase, "[pouch-prism-remove] 커밋 완료");
            t.Eq(cherryBefore - 2, rc.State.Pouch.TryGetValue("cherry", out var cv) ? cv : 0, "[pouch-prism-remove] 프리즘 비용=기본2개 제거");
            t.True(rc.State.Pouch.TryGetValue(prismId, out var pv) && pv >= 1, "[pouch-prism-remove] 프리즘 심볼 획득");
        }

        private static void PouchCommitPrismCostCurseFlow(TestCtx t)
        {
            var rc = NewDeepController(10005L);
            rc.State.JackpotPrismPending = true;
            GoToPouchOffer(rc);
            rc.Do(new ChooseNode(0));
            int prismIdx = rc.State.PouchOptions.FindIndex(c => c.Type == PouchCardType.Special && c.Tier == "PRISM");
            if (prismIdx < 0) return;
            string prismId = rc.State.PouchOptions[prismIdx].Id;
            rc.Do(new PickOffer(prismIdx));
            int skullBefore = rc.State.Pouch.TryGetValue("skull", out var sb) ? sb : 0;
            rc.Do(new PickOffer(1)); // cost_curse
            t.Eq(RunPhase.RewardDone, rc.State.Phase, "[pouch-prism-curse] 즉시 커밋");
            t.Eq(skullBefore + 1, rc.State.Pouch.TryGetValue("skull", out var sa) ? sa : 0, "[pouch-prism-curse] 해골+1");
            t.True(rc.State.Pouch.TryGetValue(prismId, out var pv) && pv >= 1, "[pouch-prism-curse] 프리즘 심볼 획득");
        }

        private static void PouchCommitRollbackOnInvalidRemainsUnchanged(TestCtx t)
        {
            // 총량 정확히 하한(20)인 주머니에서 GOLD 카드(removeN=2, 순변화 -1)를 강행 커밋하면
            // pouchValidate가 총량 미달로 막고 원자적 롤백돼야 한다 — 오퍼 RNG에 의존하지 않도록 카드를
            // 직접 구성한다(PouchOffer.PickCard/PickRemove만 검증 대상, OfferSymbolRewards는 별도 테스트).
            var run = MakeDeepRun(10006L);
            run.Pouch.Clear();
            run.Pouch["cherry"] = 2; run.Pouch["book"] = 3; run.Pouch["star"] = 3;
            run.Pouch["gem"] = 3; run.Pouch["coin"] = 3; run.Pouch["skull"] = 3; run.Pouch["flame"] = 3; // 합 20 = DeckMin
            var before = new Dictionary<string, int>(run.Pouch);
            var validateBefore = Pouch.Validate(run.Pouch);
            t.True(validateBefore.Ok, "[pouch-rollback] 전제조건 — 원본 주머니는 유효");

            // 카드를 직접 구성(오퍼 생성 RNG 우회) — Tier="GOLD"는 실제 cherry_ripe 카탈로그 등급(고급→
            // SILVER)과 무관하게 이 테스트가 검증하려는 "GOLD 비용(removeN=2, 순변화 -1)" 경로만 고정
            // 재현하기 위한 합성 카드다(진짜 티어 산출 로직은 Offer*/OfferCardCostStructure가 별도 검증).
            run.Phase = RunPhase.EventPouch;
            run.PouchOptions.Clear();
            run.PouchOptions.Add(new PouchOfferCard { Type = PouchCardType.Special, Id = "cherry_ripe", Tier = "GOLD", RemoveN = 2, LowRemoveN = 1 });
            PouchOffer.PickCard(run, 0);
            // base 후보 합(cherry+book+star+gem+coin+flame=17, skull은 harmful이라 제외)이 3 이상이라
            // GOLD 완화(LowRemoveN=1) 미적용 — removeN=2 그대로.
            t.Eq(RunPhase.EventPouchRemove, run.Phase, "[pouch-rollback] GOLD → 제거 대상 선택 대기(완화 미적용)");
            int removeIdx = run.RemoveCandidateIds.IndexOf("cherry");
            t.True(removeIdx >= 0, "[pouch-rollback] cherry가 제거 후보");

            PouchOffer.PickRemove(run, removeIdx);
            t.Eq(RunPhase.RewardDone, run.Phase, "[pouch-rollback] 실패해도 RewardDone으로 종료(웹과 동일 — 실패도 노드 해소)");
            t.True(run.RewardMessage.Contains("규칙 위반"), "[pouch-rollback] 실패 메시지");
            foreach (var kv in before)
                t.True(run.Pouch.TryGetValue(kv.Key, out var v) && v == kv.Value, $"[pouch-rollback] {kv.Key} 불변({kv.Value})");
            t.False(run.Pouch.ContainsKey("cherry_ripe"), "[pouch-rollback] 획득 실패 — cherry_ripe 없음");
            t.Eq((PouchPendingSpecial)null, run.PendingSpecial, "[pouch-rollback] PendingSpecial 정리됨");
        }

        // ══════════════════════════════════════════════════════════════════
        // 심화 노드 풀 — 웹 game.js:1439-1494
        // ══════════════════════════════════════════════════════════════════
        private static void DeepNodePoolAlwaysStartsWithPouch(TestCtx t)
        {
            for (long seed = 11001; seed < 11021; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Stage = 2;
                var clear = DirectDeepClear(run, 100);
                t.True(clear.nodeOptions.Count >= 2, $"[deeppool seed={seed}] 최소 2개 노드");
                t.Eq(NodeKind.Pouch, clear.nodeOptions[0], $"[deeppool seed={seed}] 첫 슬롯은 항상 POUCH");
                foreach (var n in clear.nodeOptions)
                    t.True(n != NodeKind.Augment && n != NodeKind.Relic, $"[deeppool seed={seed}] 일반 노드(AUGMENT/RELIC)는 절대 섞이지 않음");
            }
        }

        private static void DeepNodePoolJackpotOnlyFromStage3(TestCtx t)
        {
            // Opus 2차검수(P7-3) [웹 정합] — 게이트 기준은 "방금 클리어한 스테이지"(clearedStage=
            // run.Stage, 웹 `_clearStage()`의 `stage` — nextStage=clearedStage+1이 아니다). stage=2는
            // 정정 전(nextStage=3>=3로 오판정) vs 정정 후(clearedStage=2<3로 정확 배제)가 갈리는
            // 경계값이라 이 테스트가 실제로 버그를 잡아낼 수 있는 지점이다.
            bool sawJackpotAtStage1 = false;
            for (long seed = 12001; seed < 12101; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Stage = 1; // clearedStage=1(<3) — JACKPOT 노드 풀에 없어야 함
                var clear = DirectDeepClear(run, 100);
                if (clear.nodeOptions.Contains(NodeKind.Jackpot)) sawJackpotAtStage1 = true;
            }
            bool sawJackpotAtStage2 = false;
            for (long seed = 12201; seed < 12301; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Stage = 2; // clearedStage=2(<3, 경계값 바로 아래) — JACKPOT 절대 등장 안 함
                var clear = DirectDeepClear(run, 100);
                if (clear.nodeOptions.Contains(NodeKind.Jackpot)) sawJackpotAtStage2 = true;
            }
            bool sawJackpotAtStage3 = false;
            for (long seed = 13001; seed < 13101; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Stage = 3; // clearedStage=3(>=3) — JACKPOT 노드 풀 후보 등장 가능
                var clear = DirectDeepClear(run, 100);
                if (clear.nodeOptions.Contains(NodeKind.Jackpot)) sawJackpotAtStage3 = true;
            }
            t.False(sawJackpotAtStage1, "[deeppool-jackpot] clearedStage=1(<3)에서는 JACKPOT 노드 절대 등장 안 함");
            t.False(sawJackpotAtStage2, "[deeppool-jackpot] clearedStage=2(경계값 바로 아래)에서는 JACKPOT 노드 절대 등장 안 함(정정 전 버그였다면 여기서 등장했을 것)");
            t.True(sawJackpotAtStage3, "[deeppool-jackpot] clearedStage=3(>=3)부터는 JACKPOT 노드 등장 가능(100회 표본)");
        }

        private static void DeepNodePoolCurseRiskOnlyFromStage6(TestCtx t)
        {
            bool sawAtStage4 = false;
            for (long seed = 14001; seed < 14101; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Stage = 4; // clearedStage=4(<6)
                var clear = DirectDeepClear(run, 100);
                if (clear.nodeOptions.Contains(NodeKind.Curse) || clear.nodeOptions.Contains(NodeKind.Risk)) sawAtStage4 = true;
            }
            // Opus 2차검수(P7-3) [웹 정합] 경계값 — clearedStage=5(<6, 경계값 바로 아래)는 정정 전
            // (nextStage=6>=6로 오판정) 버그가 있었다면 여기서 CURSE/RISK가 새 나왔을 지점이다.
            bool sawAtStage5 = false;
            for (long seed = 14201; seed < 14301; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Stage = 5; // clearedStage=5(<6, 경계값 바로 아래)
                var clear = DirectDeepClear(run, 100);
                if (clear.nodeOptions.Contains(NodeKind.Curse) || clear.nodeOptions.Contains(NodeKind.Risk)) sawAtStage5 = true;
            }
            bool sawAtStage6 = false;
            for (long seed = 14401; seed < 14501; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Stage = 6; // clearedStage=6(>=6) — CURSE/RISK 등장 가능
                var clear = DirectDeepClear(run, 100);
                if (clear.nodeOptions.Contains(NodeKind.Curse) || clear.nodeOptions.Contains(NodeKind.Risk)) sawAtStage6 = true;
            }
            t.False(sawAtStage4, "[deeppool-curserisk] clearedStage=4(<6)에서는 CURSE/RISK 노드 절대 등장 안 함");
            t.False(sawAtStage5, "[deeppool-curserisk] clearedStage=5(경계값 바로 아래)에서는 CURSE/RISK 노드 절대 등장 안 함(정정 전 버그였다면 여기서 등장했을 것)");
            t.True(sawAtStage6, "[deeppool-curserisk] clearedStage=6(>=6)부터는 CURSE/RISK 노드 등장 가능(100회 표본)");
        }

        private static void DeepNodePoolNeverUsesNormalPool(TestCtx t)
        {
            for (long seed = 15001; seed < 15011; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Stage = 7;
                var clear = DirectDeepClear(run, 100);
                foreach (var n in clear.nodeOptions)
                    t.True(n == NodeKind.Pouch || n == NodeKind.SymAug || n == NodeKind.SymRel || n == NodeKind.Shop
                        || n == NodeKind.Rest || n == NodeKind.Gamble || n == NodeKind.Event || n == NodeKind.Curse
                        || n == NodeKind.Risk || n == NodeKind.Jackpot || n == NodeKind.AugLevel || n == NodeKind.Device,
                        $"[deeppool-onlydeep seed={seed}] 심화 노드 풀 화이트리스트만 등장(실제: {n})");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 3스테이지 증강 연계 보너스 — 웹 game.js:2152-2184
        // ══════════════════════════════════════════════════════════════════
        private static void SynAugBonusOffersMatchingTagSymbol(TestCtx t)
        {
            var run = MakeDeepRun(16001L);
            run.Stage = 4; // pickPerk 시점 웹 r.stage는 이미 +1돼 있음 — (stage-1)%3==0 && stage-1>0 → stage-1=3
            run.Phase = RunPhase.EventAugment;
            run.PerkOfferIds.Clear();
            run.PerkOfferIds.Add("cherry_up"); // deep:1, dSym:"cherry" → cherry의 1차 태그로 후보 필터링
            var events = NodeEvents.PickOffer(run, 0);
            t.True(run.Perks.Contains("cherry_up"), "[synaug-bonus] 그랜트는 이미 완료됨(오퍼 지연과 무관)");
            t.Eq(RunPhase.EventSynAugBonus, run.Phase, "[synaug-bonus] 연계 보너스 오퍼로 이어짐");
            t.Eq(2, run.PouchOptions.Count, "[synaug-bonus] 특수카드+skip 2장");
            t.Eq(PouchCardType.Skip, run.PouchOptions[1].Type, "[synaug-bonus] 두 번째는 skip");
            t.True(run.PouchOptions[0].Free, "[synaug-bonus] 첫 카드는 무료(연계 선물)");

            var symId = run.PouchOptions[0].Id;
            var symTags = Symbols.ById(symId)?.tags ?? Array.Empty<string>();
            var cherryFirstTag = Symbols.ById("cherry").tags[0]; // "생명" — cherry_up의 dSym="cherry" 참조 계열의 1차 태그
            t.True(symTags.Contains(cherryFirstTag), $"[synaug-bonus] 후보 심볼({symId})이 {cherryFirstTag} 태그를 가짐(dSym=cherry 매칭)");

            long before = run.Pouch.TryGetValue(symId, out var b) ? b : 0;
            var commitEvents = PouchOffer.PickSynAugBonus(run, 0); // 특수카드 픽 → 무료 커밋
            t.Eq(RunPhase.RewardDone, run.Phase, "[synaug-bonus] 커밋 후 RewardDone");
            t.True(run.Pouch.TryGetValue(symId, out var after) && after == before + 1, "[synaug-bonus] 무료로 +1 획득");
            t.True(commitEvents.Any(e => e.type == "NODE_RESOLVED"), "[synaug-bonus] 완료 이벤트 반환");
        }

        // ══════════════════════════════════════════════════════════════════
        // Opus 2차검수(P7-3) 필수③ — 노드 진입 4종 전용 테스트
        // ══════════════════════════════════════════════════════════════════

        // EnterSymAugOrRel — 오퍼 생성 + 픽 왕복. 심볼퍽(fx=null 합성 Perk) 그랜트가 이후 스핀 파이프라인
        // (ModsBuilder.Build → Perks.ById(id)==null → 스킵)을 안전하게 통과하는지 실제로 스핀 1회를
        // 굴려 확인한다(NaN/거부 없음).
        private static void EnterSymAugOrRelOfferAndPickRoundTrip(TestCtx t)
        {
            // AUGMENT 계열.
            {
                var run = MakeDeepRun(18001L);
                run.Stage = 2;
                var events = PouchOffer.EnterSymAugOrRel(run, isAug: true, S4TestHelpers.GenerousStat());
                t.True(events.Count > 0, "[symaugrel-aug] 이벤트 반환");
                if (run.Phase == RunPhase.EventAugment && run.PerkOfferIds.Count > 0)
                {
                    t.Eq(NodeKind.SymAug, events[0].node, "[symaugrel-aug] 오퍼 이벤트 node=SymAug");
                    // EnsureSymPerkCard가 보장하는 심볼퍽 카드를 명시적으로 선택(둘 다 없으면 index 0 폴백).
                    int symIdx = run.PerkOfferIds.ToList().FindIndex(id => SymPerks.Get(id) != null);
                    int pickIdx = symIdx >= 0 ? symIdx : 0;
                    string pickedId = run.PerkOfferIds[pickIdx];
                    NodeEvents.PickOffer(run, pickIdx);
                    t.True(run.Perks.Contains(pickedId), "[symaugrel-aug] 픽한 퍽이 실제로 보유 목록에 반영");
                    // 하류 안전성 — fx=null 합성 Perk(심볼퍽)이 섞여도 스핀이 예외/거부/NaN 없이 진행.
                    run.Phase = RunPhase.Spin;
                    var step = StageFlow.ProcessSpin(run, SpinMode.N);
                    t.True(!step.spin.rejected, "[symaugrel-aug] 퍽 그랜트 직후 스핀이 거부되지 않음");
                    t.True(!double.IsNaN(run.Score) && !double.IsNaN(run.StageExp), "[symaugrel-aug] 스핀 후 NaN 없음");
                }
                else
                {
                    t.Eq(RunPhase.RewardDone, run.Phase, "[symaugrel-aug] 후보 0개면 즉시 RewardDone");
                }
            }
            // RELIC 계열(동일 왕복, isAug=false).
            {
                var run = MakeDeepRun(18002L);
                run.Stage = 2;
                var events = PouchOffer.EnterSymAugOrRel(run, isAug: false, S4TestHelpers.GenerousStat());
                t.True(events.Count > 0, "[symaugrel-rel] 이벤트 반환");
                if (run.Phase == RunPhase.EventRelic && run.PerkOfferIds.Count > 0)
                {
                    t.Eq(NodeKind.SymRel, events[0].node, "[symaugrel-rel] 오퍼 이벤트 node=SymRel");
                    int symIdx = run.PerkOfferIds.ToList().FindIndex(id => SymPerks.Get(id) != null);
                    int pickIdx = symIdx >= 0 ? symIdx : 0;
                    string pickedId = run.PerkOfferIds[pickIdx];
                    NodeEvents.PickOffer(run, pickIdx);
                    t.True(run.Perks.Contains(pickedId), "[symaugrel-rel] 픽한 퍽이 실제로 보유 목록에 반영");
                    run.Phase = RunPhase.Spin;
                    var step = StageFlow.ProcessSpin(run, SpinMode.N);
                    t.True(!step.spin.rejected, "[symaugrel-rel] 퍽 그랜트 직후 스핀이 거부되지 않음");
                    t.True(!double.IsNaN(run.Score) && !double.IsNaN(run.StageExp), "[symaugrel-rel] 스핀 후 NaN 없음");
                }
                else
                {
                    t.Eq(RunPhase.RewardDone, run.Phase, "[symaugrel-rel] 후보 0개면 즉시 RewardDone");
                }
            }
        }

        // EnterJackpotNode — 최다 잭팟태그 후보 풀이 웹 `sym.special !== NONE` 기준을 정확히 따르는지:
        // coin(Sp.COIN, cat=base)은 coin 태그 후보에 포함되지만 crown(Sp.NONE, cat=special)은 crown
        // 태그 후보에서 원천 배제된다.
        private static void EnterJackpotNodeCandidatePoolCoinIncludedCrownExcluded(TestCtx t)
        {
            bool sawCoinCandidate = false;
            for (long seed = 17001; seed < 17101; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Pouch.Clear();
                run.Pouch["coin"] = 5; run.Pouch["coin_bag"] = 2; // coin 태그가 최다(유일) 태그
                var events = PouchOffer.EnterJackpotNode(run);
                t.Eq(NodeKind.Jackpot, events[0].node, $"[jknode seed={seed}] node=Jackpot");
                foreach (var card in run.PouchOptions)
                {
                    if (card.Type != PouchCardType.Special) continue;
                    t.True(card.Id == "coin" || card.Id == "coin_bag", $"[jknode-coin seed={seed}] 후보는 coin 태그 심볼만({card.Id})");
                    if (card.Id == "coin") sawCoinCandidate = true;
                }
            }
            t.True(sawCoinCandidate, "[jknode] coin(Sp.COIN, 값심볼이지만 special!=NONE) 후보 포함 확인(100 시드 표본)");

            bool sawPlainCrown = false;
            bool sawCrownVariant = false;
            for (long seed = 17201; seed < 17301; seed++)
            {
                var run = MakeDeepRun(seed);
                run.Pouch.Clear();
                run.Pouch["crown"] = 2; run.Pouch["fake_crown_sym"] = 3; run.Pouch["jackpot_crown"] = 1; // crown 태그가 최다 태그
                var events = PouchOffer.EnterJackpotNode(run);
                t.Eq(NodeKind.Jackpot, events[0].node, $"[jknode-crown seed={seed}] node=Jackpot");
                foreach (var card in run.PouchOptions)
                {
                    if (card.Type != PouchCardType.Special) continue;
                    if (card.Id == "crown") sawPlainCrown = true;
                    if (card.Id == "fake_crown_sym" || card.Id == "jackpot_crown") sawCrownVariant = true;
                }
            }
            t.False(sawPlainCrown, "[jknode] crown(Sp.NONE, 값심볼) 자체는 후보에서 원천 배제(웹 sym.special 진리값 필터, 100 시드 표본)");
            t.True(sawCrownVariant, "[jknode] fake_crown_sym/jackpot_crown(Sp!=NONE)은 정상적으로 후보 가능(100 시드 표본)");
        }

        // PickRestDeep — 2택(코인/정화) 각 분기 커밋 + node 필드.
        private static void PickRestDeepChoosesCoinOrPurify(TestCtx t)
        {
            var run = MakeDeepRun(19001L);
            run.Pouch["skull"] = 3;
            PouchOffer.EnterRest(run);
            t.Eq(RunPhase.EventRestDeep, run.Phase, "[restdeep] 2택 오퍼 진입");
            t.Eq(2, run.DeepChoiceIds.Count, "[restdeep] 코인/정화 2택");

            long coinsBefore = run.Coins;
            var pickEv = PouchOffer.PickRestDeep(run, 0); // rest_coin
            t.Eq(coinsBefore + 12, run.Coins, "[restdeep] rest_coin → 코인+12");
            t.Eq(RunPhase.RewardDone, run.Phase, "[restdeep] 커밋 후 RewardDone");
            t.Eq(NodeKind.Rest, pickEv[0].node, "[restdeep] node=Rest(코인 분기)");

            var run2 = MakeDeepRun(19002L);
            run2.Pouch["skull"] = 3;
            PouchOffer.EnterRest(run2);
            int skullBefore = run2.Pouch["skull"];
            var pickEv2 = PouchOffer.PickRestDeep(run2, 1); // rest_purify
            t.Eq(skullBefore - 1, run2.Pouch.TryGetValue("skull", out var sa) ? sa : 0, "[restdeep] rest_purify → 해골-1");
            t.Eq(NodeKind.Rest, pickEv2[0].node, "[restdeep] node=Rest(정화 분기)");
        }

        // PickGambleDeep — 2택(코인 도박/심볼 도박) 각 분기 커밋 + node 필드.
        private static void PickGambleDeepChoosesCoinOrSym(TestCtx t)
        {
            var run = MakeDeepRun(19101L);
            run.Coins = 10; // 0이면 win/lose가 결과상 구분 안 됨(0×2=0) — GambleNode 선례와 동일 처방.
            PouchOffer.EnterGamble(run);
            t.Eq(RunPhase.EventGambleDeep, run.Phase, "[gambledeep] 2택 오퍼 진입");
            t.True(run.DeepChoiceIds.Contains("gamble_coin"), "[gambledeep] 코인 도박 항상 포함");
            t.True(run.DeepChoiceIds.Contains("gamble_sym"), "[gambledeep] 시작덱 기본계열 보유 시 심볼 도박도 포함");

            long coinsBefore = run.Coins;
            var pickEv = PouchOffer.PickGambleDeep(run, 0); // gamble_coin
            t.True(run.Coins == coinsBefore || run.Coins == coinsBefore * 2, "[gambledeep] gamble_coin → 코인 2배 또는 유지(손실 없음)");
            t.Eq(NodeKind.Gamble, pickEv[0].node, "[gambledeep] node=Gamble(코인 도박 분기)");

            var run2 = MakeDeepRun(19102L);
            run2.Coins = 10;
            PouchOffer.EnterGamble(run2);
            int symIdx = run2.DeepChoiceIds.IndexOf("gamble_sym");
            t.True(symIdx >= 0, "[gambledeep] gamble_sym 후보 존재(전제조건)");
            var pickEv2 = PouchOffer.PickGambleDeep(run2, symIdx);
            t.Eq(NodeKind.Gamble, pickEv2[0].node, "[gambledeep] node=Gamble(심볼 도박 분기)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 심화 자동플레이 스모크 — 전 신기능(잭팟태그/피버/자동소멸/POUCH 오퍼/노드풀) 실사
        // ══════════════════════════════════════════════════════════════════
        private static readonly HashSet<string> KnownEventTypes = new HashSet<string>
        {
            "REJECTED", "SPIN_RESULT", "STAGE_CLEARED", "REVIVED", "POST_SPIN", "GAME_OVER",
            "DEVICE_MANIP_RESULT", "NODE_RESOLVED", "PERK_OFFER", "PERK_GRANTED", "PERK_HELD",
            "RETAKE_EMPTY", "SHOP_OFFER", "SHOP_PURCHASED", "SHOP_REROLLED", "SHOP_LEFT",
            "ITEM_USED", "DEVICE_ARMED", "DEVICE_PEEK", "RUN_STARTED",
            "DEVICE_OFFER", "PERK_LEVELED", "STAGE_STARTED", "BOSS_PHASE2",
            "REPAIR_DONE", "ARCHETYPE_CHANGED",
            "POUCH_OFFER", "POUCH_COST_OFFER", "POUCH_REMOVE_OFFER", "DEEP_CHOICE_OFFER",
        };

        private static void DeepAutoplaySmokeAllNewFeatures(TestCtx t)
        {
            bool sawPouchOfferAny = false, sawJackpotNodeAny = false, sawFeverAny = false, sawDecayAny = false;
            for (long seed = 20001; seed < 20011; seed++)
            {
                var rc = NewDeepController(seed);
                // Opus 2차검수(P7-3) 필수③ — 항상 index 0만 고르면 노드가 3개일 때 2/3의 경로가 스모크
                // 대상에서 아예 빠진다(예: [POUCH, SYMAUG, SHOP] 순서면 SYMAUG/SHOP이 영원히 미실사).
                // 시드 기반 별도 RNG로 매번 유효 인덱스 중 하나를 균등 무작위 선택 — 게임 자체의
                // rc.State.Rng와는 독립적이라 스핀 결과 재현성에 영향 없음.
                var pickRng = new Rng(seed * 7919 + 13);
                int guard = 0;
                int shopStep = 0;
                bool sawPouchOffer = false, sawJackpotNode = false, sawFever = false, sawDecay = false;
                while (rc.State.Phase != RunPhase.GameOver && guard < 20_000)
                {
                    guard++;
                    IReadOnlyList<RunEvent> events;
                    switch (rc.State.Phase)
                    {
                        case RunPhase.Spin: events = rc.Do(new Spin(SpinMode.N)); break;
                        case RunPhase.PostSpin: events = rc.Do(new Continue()); break;
                        case RunPhase.NodeSelect:
                            int jkIdx = rc.State.NodeOptions.IndexOf(NodeKind.Jackpot);
                            if (jkIdx >= 0) sawJackpotNode = true;
                            int nodeIdx = pickRng.Next(Math.Max(1, rc.State.NodeOptions.Count));
                            events = rc.Do(new ChooseNode(nodeIdx));
                            break;
                        case RunPhase.EventAugment:
                        case RunPhase.EventRelic:
                        case RunPhase.EventAugLevel:
                        case RunPhase.EventPouch:
                        case RunPhase.EventPouchCost:
                        case RunPhase.EventPouchRemove:
                        case RunPhase.EventRestDeep:
                        case RunPhase.EventGambleDeep:
                        case RunPhase.EventSynAugBonus:
                            events = rc.Do(new PickOffer(0));
                            break;
                        case RunPhase.EventShop:
                            if (shopStep == 0) { events = rc.Do(new BuyOffer(0)); shopStep = 1; }
                            else { events = rc.Do(new LeaveShop()); shopStep = 0; }
                            break;
                        case RunPhase.DeviceNode: events = rc.Do(new TakeDevice(true)); break;
                        case RunPhase.RewardDone: events = rc.Do(new ProceedToStage()); break;
                        default: throw new InvalidOperationException($"[deep-smoke-p73 seed={seed}] 처리 불가 Phase=" + rc.State.Phase);
                    }
                    foreach (var e in events)
                    {
                        t.True(KnownEventTypes.Contains(e.type), $"[deep-smoke-p73 seed={seed}] 알 수 없는 RunEvent.type={e.type}");
                        if (e.type == "POUCH_OFFER") sawPouchOffer = true;
                    }
                    if (rc.State.FeverSpins > 0) sawFever = true;
                    if (rc.State.DeepStats != null && rc.State.DeepStats.AutoDecays > 0) sawDecay = true;

                    // 불변식 — 매 스텝.
                    t.True(rc.State.Score >= 0, $"[deep-smoke-p73 seed={seed}] Score 음수 아님");
                    t.True(rc.State.Coins >= 0, $"[deep-smoke-p73 seed={seed}] Coins 음수 아님");
                    t.True(!double.IsNaN(rc.State.Score) && !double.IsNaN(rc.State.StageExp), $"[deep-smoke-p73 seed={seed}] NaN 아님");
                    t.True(rc.State.FeverGauge >= 0 && !double.IsNaN(rc.State.FeverGauge), $"[deep-smoke-p73 seed={seed}] FeverGauge 유효");
                    t.True(rc.State.FeverSpins >= 0, $"[deep-smoke-p73 seed={seed}] FeverSpins 음수 아님");
                    t.True(rc.State.BellTicketUses <= 2, $"[deep-smoke-p73 seed={seed}] 종소리티켓 런 2회 제한 준수");
                    t.True(rc.State.JpTicketUses <= 2, $"[deep-smoke-p73 seed={seed}] 잭팟티켓 런 2회 제한 준수");
                    foreach (var kv in rc.State.Pouch)
                        t.True(kv.Value >= 0, $"[deep-smoke-p73 seed={seed}] Pouch[{kv.Key}] 음수 아님");
                }
                t.Eq(RunPhase.GameOver, rc.State.Phase, $"[deep-smoke-p73 seed={seed}] 예외 없이 게임오버까지 진행(stage={rc.State.Stage})");
                sawPouchOfferAny |= sawPouchOffer; sawJackpotNodeAny |= sawJackpotNode;
                sawFeverAny |= sawFever; sawDecayAny |= sawDecay;
            }
            // 10개 시드·최대 20000틱 장기 플레이 표본 — POUCH 오퍼/JACKPOT 노드는 매 스테이지 클리어마다
            // 등장 기회가 있어(POUCH 고정슬롯·JACKPOT는 dpool 후보) 높은 확률로 실사된다(하드 어서션).
            // 피버는 시작덱에 잭팟태그 심볼이 "coin" 하나뿐이라 게이지 100 도달(콤보 15 기준 7회+) 자체가
            // 여러 스테이지의 우연한 3+coin 드로우에 의존하는 저빈도 이벤트라 — 결정론 단위 테스트
            // (FeverChargeOnly~FeverJackpotDoublesScore 5종)로 이미 충전/발동/효과/종료/피버잭팟을 전부
            // 정확히 검증했으므로, 여기서는 하드 실패 대신 관측 여부만 기록한다(오탐 방지).
            t.True(sawPouchOfferAny, "[deep-smoke-p73] POUCH 오퍼가 최소 1회 실사됨");
            t.True(sawJackpotNodeAny, "[deep-smoke-p73] JACKPOT 노드가 최소 1회 실사됨");
            t.Report("[deep-smoke-p73] 피버 발동 관측", sawFeverAny ? "10시드 중 최소 1회 관측" : "10시드 표본에서 미관측(저빈도 이벤트 — 별도 결정론 단위테스트로 검증됨)");
            t.Report("[deep-smoke-p73] 자동소멸 관측", sawDecayAny ? "10시드 중 최소 1회 관측" : "10시드 표본에서 미관측(stage15+ 도달 필요 — 별도 결정론 단위테스트로 검증됨)");
        }
    }
}
