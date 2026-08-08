using System;
using System.Collections.Generic;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // P6 골든 테스트 — 승천(심화 학기) A1~A10, WEB_PARITY_DESIGN.md §1-A #18.
    // 작업 지시 항목: ①ascMods 6축 손계산(a=1/3/5/10 경계) ②단계 규칙 5종(A2/A7/A8/A9/A10, 고정 시드)
    // ③점수 격리(asc 런 후 bestScore 불변·bestAscScore 갱신) ④졸업→ascMax→선택 상한 ⑤자동플레이 하네스
    // asc 대응.
    internal static class Tests_P6_Ascension
    {
        public static void Run(TestCtx t)
        {
            AscModsSixAxis(t);
            QuotaOfWiring(t);
            QuotaOfLiteralGolden(t);
            SkullWeightAddA2(t);
            BannedSymbolA8(t);
            PrismCurseA7(t);
            DeviceCooldownA9(t);
            BossPhase2A10(t);
            ShopPriceAndItemCapWiring(t);
            StartCoinWiring(t);
            FinalScoreScoreMulWiring(t);
            ScoreIsolation(t);
            GraduationAscMaxSelectionCap(t);
            AscMaxLoadGuard(t);
            MasteryAscMaxWiring(t);
            AutoplayHarnessAsc(t);
        }

        // ── ① ascMods(a) 6축 손계산 — 웹 game.js:124-135 그대로(a=0/1/3/5/10 경계 + 상하한 클램프) ──
        private static void AscModsSixAxis(TestCtx t)
        {
            var m0 = AscMods.Get(0);
            t.EqTol(1.0, m0.QuotaMul, "[asc0] quotaMul");
            t.EqTol(1.0, m0.BossQuotaMul, "[asc0] bossQuotaMul");
            t.EqTol(1.0, m0.ShopPriceMul, "[asc0] shopPriceMul");
            t.Eq(0, m0.ItemCapDelta, "[asc0] itemCapDelta");
            t.Eq(0, m0.StartCoinDelta, "[asc0] startCoinDelta");
            t.EqTol(1.0, m0.ScoreMul, "[asc0] scoreMul");

            var m1 = AscMods.Get(1);
            t.EqTol(1.08, m1.QuotaMul, "[asc1] quotaMul = 1+0.08*1");
            t.EqTol(1.0, m1.BossQuotaMul, "[asc1] bossQuotaMul(a<4)=1");
            t.EqTol(1.0, m1.ShopPriceMul, "[asc1] shopPriceMul(a<3)=1");
            t.Eq(0, m1.ItemCapDelta, "[asc1] itemCapDelta(a<5)=0");
            t.Eq(0, m1.StartCoinDelta, "[asc1] startCoinDelta(a<3)=0");
            t.EqTol(1.12, m1.ScoreMul, "[asc1] scoreMul = 1+0.12*1");

            var m3 = AscMods.Get(3);
            t.EqTol(1.24, m3.QuotaMul, "[asc3] quotaMul = 1+0.08*3");
            t.EqTol(1.0, m3.BossQuotaMul, "[asc3] bossQuotaMul(a<4)=1");
            t.EqTol(1.12, m3.ShopPriceMul, "[asc3] shopPriceMul = 1+0.12*(3-2)");
            t.Eq(0, m3.ItemCapDelta, "[asc3] itemCapDelta(a<5)=0");
            t.Eq(-4, m3.StartCoinDelta, "[asc3] startCoinDelta = -min(16,(3-2)*4)");
            t.EqTol(1.36, m3.ScoreMul, "[asc3] scoreMul = 1+0.12*3");

            var m5 = AscMods.Get(5);
            t.EqTol(1.4, m5.QuotaMul, "[asc5] quotaMul = 1+0.08*5");
            t.EqTol(1.12, m5.BossQuotaMul, "[asc5] bossQuotaMul = 1+0.06*(5-3)");
            t.EqTol(1.36, m5.ShopPriceMul, "[asc5] shopPriceMul = 1+0.12*(5-2)");
            t.Eq(-1, m5.ItemCapDelta, "[asc5] itemCapDelta(a>=5)=-1");
            t.Eq(-12, m5.StartCoinDelta, "[asc5] startCoinDelta = -min(16,(5-2)*4)");
            t.EqTol(1.6, m5.ScoreMul, "[asc5] scoreMul = 1+0.12*5");

            var m10 = AscMods.Get(10);
            t.EqTol(1.8, m10.QuotaMul, "[asc10] quotaMul = 1+0.08*10");
            t.EqTol(1.42, m10.BossQuotaMul, "[asc10] bossQuotaMul = 1+0.06*(10-3)");
            t.EqTol(1.96, m10.ShopPriceMul, "[asc10] shopPriceMul = 1+0.12*(10-2)");
            t.Eq(-1, m10.ItemCapDelta, "[asc10] itemCapDelta(a>=5)=-1");
            t.Eq(-16, m10.StartCoinDelta, "[asc10] startCoinDelta = -min(16,(10-2)*4=32)");
            t.EqTol(2.2, m10.ScoreMul, "[asc10] scoreMul = 1+0.12*10 (문서 근사값과 일치)");

            // 클램프 — 웹 `Math.max(0, Math.min(ASC_MAX, Math.floor(asc||0)))`.
            t.EqTol(m10.QuotaMul, AscMods.Get(11).QuotaMul, "[클램프] asc=11 → asc=10과 동일");
            t.EqTol(m10.QuotaMul, AscMods.Get(999).QuotaMul, "[클램프] asc=999 → asc=10과 동일");
            t.EqTol(m0.QuotaMul, AscMods.Get(-5).QuotaMul, "[클램프] asc=-5 → asc=0과 동일");
            t.Eq(10, AscMods.Get(11).A, "[클램프] Result.A도 클램프된 값");
        }

        // ── QuotaOf(stage,mods,asc,bossPhase2) 배선 — 웹 game.js:423 quota 공식 그대로 재현 ──────
        private static void QuotaOfWiring(TestCtx t)
        {
            var mods = ModsBuilder.Build("basic", "novice", new List<string>(), new List<string>(), "");

            // 비보스(stage1) — am.bossQuotaMul 미적용.
            double q1 = HandQuota(1, mods, 5, false);
            t.Eq((long)q1, SpinResolver.QuotaOf(1, mods, 5, false), "[QuotaOf] 비보스 stage1 asc5 손계산 일치");

            // asc=0이면 완전히 기존(P6 이전) 값과 동일 — 2-인자 오버로드와 교차검증.
            t.Eq(SpinResolver.QuotaOf(1, mods), SpinResolver.QuotaOf(1, mods, 0, false), "[QuotaOf] asc=0 기본값 == 2-인자 오버로드");

            // 보스(stage15, "luck", 웹 quotaMul=1.0·bonusSpins=1) — am.bossQuotaMul 적용.
            double q15 = HandQuota(15, mods, 10, false);
            t.Eq((long)q15, SpinResolver.QuotaOf(15, mods, 10, false), "[QuotaOf] 보스 stage15 asc10 손계산 일치(bossQuotaMul 포함)");

            // bossPhase2=true → 위 값에 ×1.3 추가.
            double q15p2 = q15 * 1.3;
            t.Eq((long)q15p2, SpinResolver.QuotaOf(15, mods, 10, true), "[QuotaOf] A10 2페이즈 ×1.3 손계산 일치");

            // 실제 스핀 경로(ResolveSpin)가 run.Asc/run.BossPhase2를 정확히 전달하는지 — 살아있는 RunController로 재확인.
            var rc0 = new RunController("novice", "basic", "", 5001L, S4TestHelpers.GenerousStat(), asc: 0);
            var rc7 = new RunController("novice", "basic", "", 5002L, S4TestHelpers.GenerousStat(), asc: 7);
            var ev0 = rc0.Do(new Spin(SpinMode.N));
            var ev7 = rc7.Do(new Spin(SpinMode.N));
            long expectedQ0 = SpinResolver.QuotaOf(1, mods, 0, false);
            long expectedQ7 = SpinResolver.QuotaOf(1, mods, 7, false);
            t.Eq(expectedQ0, ev0[0].spin.quota, "[QuotaOf 실배선] asc=0 실제 스핀 quota");
            t.Eq(expectedQ7, ev7[0].spin.quota, "[QuotaOf 실배선] asc=7 실제 스핀 quota");
            t.True(ev7[0].spin.quota > ev0[0].spin.quota, "[QuotaOf 실배선] asc7 요구치가 asc0보다 큼");
        }

        // ── Opus 2차검수 — quota 리터럴 기대값(HandQuota 순환검증 해소) ─────────────────────────
        // 위 QuotaOfWiring의 HandQuota 비교는 SpinResolver.QuotaOf와 "같은 수식을 두 번 쓴" 대조라
        // 수식 자체가 틀리면 함께 틀려서 못 잡는다(순환검증) — 이 테스트는 축 조합(stage15·기본Mods·
        // asc10·bossPhase2=true)을 외부에서 독립적으로 손 계산한 정수 리터럴로 고정한다.
        //   baseSpins=5, bsp(luck bonusSpins)=1, prop=6/5=1.2
        //   Quota(15)=755 × mods.quotaMul(1.0) × Bosses.QuotaMulFor(15)(luck=1.0) × 1.2 = 906.0
        //   × am.quotaMul(asc10=1.8) = 1630.8
        //   × am.bossQuotaMul(asc10=1.42, IsBossStage(15)=true) = 2315.736
        //   × 1.3(bossPhase2) = 3010.4568 → (long) = 3010
        private static void QuotaOfLiteralGolden(TestCtx t)
        {
            long q = SpinResolver.QuotaOf(15, new Mods(), 10, true);
            t.Eq(3010L, q, "[QuotaOf 리터럴] stage15·기본Mods·asc10·bossPhase2=true → 3010(외부 손계산 고정값)");

            // 동일 축, bossPhase2=false만 다른 경우도 함께 고정(2315 — 위 손계산 3번째 줄).
            long qNoPhase2 = SpinResolver.QuotaOf(15, new Mods(), 10, false);
            t.Eq(2315L, qNoPhase2, "[QuotaOf 리터럴] stage15·기본Mods·asc10·bossPhase2=false → 2315");
        }

        // SpinResolver.QuotaOf와 완전히 동일한 연산 순서로 손계산(부동소수 재현성 보존 — HandQuota 관례).
        private static double HandQuota(int stage, Mods mods, int asc, bool bossPhase2)
        {
            int baseSpins = ModsBuilder.SpinsPerStage(mods);
            int bsp = Bosses.Spins(stage);
            double prop = (bsp > 0 && baseSpins > 0) ? (double)(baseSpins + bsp) / baseSpins : 1.0;
            double q = Formulas.Quota(stage) * mods.quotaMul * Bosses.QuotaMulFor(stage) * prop;
            var am = AscMods.Get(asc);
            q *= am.QuotaMul;
            if (Formulas.IsBossStage(stage)) q *= am.BossQuotaMul;
            if (bossPhase2) q *= 1.3;
            return q;
        }

        // ── ② A2+ 해골 weightAdd — 웹 game.js:451 그대로 ──────────────────────────────────────
        private static void SkullWeightAddA2(TestCtx t)
        {
            var run3 = S4TestHelpers.NewRun(201L);
            run3.Asc = 3;
            var mods3 = ModsBuilder.Build(run3.MachineId, run3.CharId, run3.Perks, run3.Curses, run3.Device);
            double baseline3 = mods3.weightAdd.TryGetValue(Sym.Skull, out var v3) ? v3 : 0.0; // novice/basic 기본 0
            AscRunHooks.ApplyRunAscMods(mods3, run3);
            double after3 = mods3.weightAdd.TryGetValue(Sym.Skull, out var av3) ? av3 : 0.0;
            t.EqTol(baseline3 + 0.5 * (3 - 1), after3, "[A2] asc=3 → weightAdd.skull += 0.5*(a-1) = +1.0");

            // asc=1(<2)이면 무영향.
            var run1 = S4TestHelpers.NewRun(202L);
            run1.Asc = 1;
            var mods1 = ModsBuilder.Build(run1.MachineId, run1.CharId, run1.Perks, run1.Curses, run1.Device);
            double baseline1 = mods1.weightAdd.TryGetValue(Sym.Skull, out var v1) ? v1 : 0.0;
            AscRunHooks.ApplyRunAscMods(mods1, run1);
            double after1 = mods1.weightAdd.TryGetValue(Sym.Skull, out var av1) ? av1 : 0.0;
            t.EqTol(baseline1, after1, "[A2 대조군] asc=1(<2)이면 weightAdd.skull 무영향");

            // asc=10 → +0.5*9 = +4.5.
            var run10 = S4TestHelpers.NewRun(203L);
            run10.Asc = 10;
            var mods10 = ModsBuilder.Build(run10.MachineId, run10.CharId, run10.Perks, run10.Curses, run10.Device);
            double baseline10 = mods10.weightAdd.TryGetValue(Sym.Skull, out var v10) ? v10 : 0.0;
            AscRunHooks.ApplyRunAscMods(mods10, run10);
            double after10 = mods10.weightAdd.TryGetValue(Sym.Skull, out var av10) ? av10 : 0.0;
            t.EqTol(baseline10 + 4.5, after10, "[A2] asc=10 → weightAdd.skull += 4.5");
        }

        // ── ② A8+ 금지 심볼 — 실제로 등장하지 않는지(고정 시드 3000회 롤) ─────────────────────────
        private static void BannedSymbolA8(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(777L);
            run.Asc = 8;
            run.BannedSym = "cherry";
            var mods = ModsBuilder.Build(run.MachineId, run.CharId, run.Perks, run.Curses, run.Device);
            AscRunHooks.ApplyRunAscMods(mods, run);
            t.EqTol(0.0, mods.symbolWeightMul.TryGetValue(Sym.Cherry, out var wm) ? wm : -1.0, "[A8] symbolWeightMul[cherry]==0으로 대입됨");

            bool sawBanned = false;
            int totalCells = 0;
            for (int i = 0; i < 3000; i++)
            {
                var raw = SpinResolver.RollRaw(run.Rng, mods, Formulas.REEL, false);
                foreach (var c in raw)
                {
                    totalCells++;
                    if (c.sym.id == "cherry") sawBanned = true;
                }
            }
            t.True(!sawBanned, $"[A8] 금지 심볼(cherry)이 {totalCells}칸 롤 동안 한 번도 등장하지 않음");

            // asc=7(<8)이면 배너 자체가 세워지지 않는다(RollBannedSym 계약) — 별도 RunState로 확인.
            var run7 = S4TestHelpers.NewRun(778L);
            run7.Asc = 7;
            AscRunHooks.RollBannedSym(run7);
            t.Eq("", run7.BannedSym, "[A8 대조군] asc=7(<8)이면 BannedSym 항상 빈 문자열");

            var run8 = S4TestHelpers.NewRun(779L);
            run8.Asc = 8;
            AscRunHooks.RollBannedSym(run8);
            t.True(!string.IsNullOrEmpty(run8.BannedSym), "[A8] asc=8이면 BannedSym이 4종 중 하나로 채워짐");
            t.True(run8.BannedSym == "cherry" || run8.BannedSym == "book" || run8.BannedSym == "star" || run8.BannedSym == "gem",
                "[A8] BannedSym은 cherry/book/star/gem 중 하나");
        }

        // ── ② A7+ 프리즘 증강 픽 → 저주 1개 자동 부착 — 웹 game.js:2150 ───────────────────────────
        private static void PrismCurseA7(TestCtx t)
        {
            // overdrive = PRISM 티어 AUGMENT(Perks.cs 확인).
            var run = S4TestHelpers.NewRun(301L);
            run.Phase = RunPhase.EventAugment;
            run.Asc = 7;
            run.PerkOfferIds.Add("overdrive");
            int cursesBefore = run.Curses.Count;
            var events = NodeEvents.PickOffer(run, 0);
            t.True(run.Perks.Contains("overdrive"), "[A7] PRISM 증강 픽이 실제로 반영됨");
            t.Eq(cursesBefore + 1, run.Curses.Count, "[A7] 저주 1개 자동 부착");
            RunEvent granted = null;
            foreach (var e in events) if (e.type == "PERK_GRANTED") granted = e;
            t.True(granted != null && !string.IsNullOrEmpty(granted.curseGrantedId), "[A7] PERK_GRANTED.curseGrantedId 채워짐");

            // 대조군 1: asc=6(<7)이면 저주 미부착.
            var run6 = S4TestHelpers.NewRun(302L);
            run6.Phase = RunPhase.EventAugment;
            run6.Asc = 6;
            run6.PerkOfferIds.Add("overdrive");
            NodeEvents.PickOffer(run6, 0);
            t.Eq(0, run6.Curses.Count, "[A7 대조군] asc=6(<7)이면 저주 미부착");

            // 대조군 2: RELIC 노드 픽(PRISM 유물이라도)은 웹 _pickKind==='AUG' 한정이라 저주 미부착.
            var run9 = S4TestHelpers.NewRun(303L);
            run9.Phase = RunPhase.EventRelic;
            run9.Asc = 9;
            run9.PerkOfferIds.Add("prism_diploma"); // PRISM 티어 RELIC(Perks.cs 확인).
            NodeEvents.PickOffer(run9, 0);
            t.Eq(0, run9.Curses.Count, "[A7 대조군] RELIC 노드는 PRISM이라도 저주 미부착(웹 _pickKind==='AUG' 한정)");

            // 대조군 3: SILVER 증강 픽은 asc>=7이라도 저주 미부착(티어 조건).
            var run7Silver = S4TestHelpers.NewRun(304L);
            run7Silver.Phase = RunPhase.EventAugment;
            run7Silver.Asc = 8;
            run7Silver.PerkOfferIds.Add("early_prep"); // SILVER 티어(ctx-조건부 증강, 확인용으로만 사용).
            NodeEvents.PickOffer(run7Silver, 0);
            t.Eq(0, run7Silver.Curses.Count, "[A7 대조군] SILVER 증강 픽은 저주 미부착");
        }

        // ── ② A9+ 능동장치(코인투입·예언) 쿨다운 stage+2 — 웹 game.js:1306-1317 ───────────────────
        private static void DeviceCooldownA9(TestCtx t)
        {
            // dev_coin(코인투입).
            var run = S4TestHelpers.NewRun(401L);
            run.Device = "dev_coin";
            run.Asc = 9;
            run.Coins = 100;
            run.Stage = 1;

            var ev1 = DeviceActions.Handle(run, "dev_coin", null);
            t.Eq("DEVICE_ARMED", ev1[0].type, "[A9-coin] 1회차 성공(쿨다운 없음)");
            t.Eq(3, run.DevCdUntil, "[A9-coin] 사용 후 DevCdUntil = stage(1)+2");

            run.Stage = 2; // 쿨다운 윈도우 안(2<3) — 클리어 시뮬레이션으로 UsedCmds만 리셋.
            run.UsedCmds.Clear();
            var ev2 = DeviceActions.Handle(run, "dev_coin", null);
            t.Eq("REJECTED", ev2[0].type, "[A9-coin] 쿨다운 중(stage2<DevCdUntil3) 거부");
            t.Eq("DEVICE_COOLDOWN", ev2[0].reason, "[A9-coin] 거부 사유");

            run.Stage = 3; // 쿨다운 해제(3<3 거짓).
            var ev3 = DeviceActions.Handle(run, "dev_coin", null);
            t.Eq("DEVICE_ARMED", ev3[0].type, "[A9-coin] 쿨다운 해제 후 재사용 성공");
            t.Eq(5, run.DevCdUntil, "[A9-coin] 재사용 시 DevCdUntil 갱신 = stage(3)+2");

            // dev_oracle(예언) — 동일 구조.
            var runO = S4TestHelpers.NewRun(402L);
            runO.Device = "dev_oracle";
            runO.Asc = 9;
            runO.Stage = 1;
            var oev1 = DeviceActions.Handle(runO, "dev_oracle", null);
            t.Eq("DEVICE_PEEK", oev1[0].type, "[A9-oracle] 1회차 성공");
            t.Eq(3, runO.DevCdUntil, "[A9-oracle] DevCdUntil = stage+2");
            runO.Stage = 2;
            runO.UsedCmds.Clear();
            var oev2 = DeviceActions.Handle(runO, "dev_oracle", null);
            t.Eq("REJECTED", oev2[0].type, "[A9-oracle] 쿨다운 중 거부");
            t.Eq("DEVICE_COOLDOWN", oev2[0].reason, "[A9-oracle] 거부 사유");

            // 대조군: asc=8(<9)이면 쿨다운 자체가 없음(연속 사용 가능, 스테이지당 1회 제약만).
            var runNoAsc = S4TestHelpers.NewRun(403L);
            runNoAsc.Device = "dev_coin";
            runNoAsc.Asc = 8;
            runNoAsc.Coins = 100;
            runNoAsc.Stage = 1;
            DeviceActions.Handle(runNoAsc, "dev_coin", null);
            runNoAsc.Stage = 2;
            runNoAsc.UsedCmds.Clear();
            var evNoAsc = DeviceActions.Handle(runNoAsc, "dev_coin", null);
            t.Eq("DEVICE_ARMED", evNoAsc[0].type, "[A9 대조군] asc=8(<9)이면 쿨다운 미적용");
        }

        // ── ② A10 최종보스(15) 2페이즈 — 웹 game.js:1395-1397 ───────────────────────────────────
        private static void BossPhase2A10(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(501L);
            run.Stage = 15;
            run.Asc = 10;
            run.Score = 500;
            run.Coins = 50;

            var mods = ModsBuilder.Build(run.MachineId, run.CharId, run.Perks, run.Curses, run.Device);
            long quota1 = SpinResolver.QuotaOf(15, mods, 10, false);
            var outcome1 = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = null, gained = 0,
                newExp = quota1, newScore = run.Score, newCoins = run.Coins, newSpinIndex = 1,
                quota = quota1, spins = 6,
            };
            var clear1 = StageFlow.ClearStage(run, outcome1);

            t.True(clear1.bossPhase2Restart, "[A10] 1페이즈 클리어 → bossPhase2Restart=true");
            t.True(run.BossPhase2, "[A10] run.BossPhase2=true");
            t.Eq(15, run.Stage, "[A10] 같은 스테이지 유지(진행 안 함)");
            t.Eq(RunPhase.Spin, run.Phase, "[A10] Phase는 Spin 유지");
            t.Eq(500L, run.Score, "[A10] 점수 미반영(진짜 클리어 아님)");
            t.Eq(50L, run.Coins, "[A10] 코인 미반영");
            t.True(!run.GraduatedThisRun, "[A10] 1페이즈 완료는 아직 졸업 아님");
            t.True(!string.IsNullOrEmpty(run.BannedSym), "[A10] _beginStage() 재실행으로 A8 금지심볼도 재롤(asc10>=8)");

            long quota2 = SpinResolver.QuotaOf(15, mods, 10, true);
            double q1Hand = HandQuota(15, mods, 10, false);
            double q2Hand = q1Hand * 1.3;
            t.Eq((long)q1Hand, quota1, "[A10] quota1 손계산 일치");
            t.Eq((long)q2Hand, quota2, "[A10] quota2(2페이즈 ×1.3) 손계산 일치");
            t.True(quota2 > quota1, "[A10] 2페이즈 요구치가 1페이즈보다 큼");

            // 2페이즈 완료 → 진짜 졸업(보스 카운트 중복 없이 1회만).
            var outcome2 = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = null, gained = 0,
                newExp = quota2, newScore = run.Score, newCoins = run.Coins, newSpinIndex = 1,
                quota = quota2, spins = 6,
            };
            var clear2 = StageFlow.ClearStage(run, outcome2);
            t.True(!clear2.bossPhase2Restart, "[A10] 2페이즈 클리어 → 진짜 클리어(재시작 아님)");
            t.True(run.GraduatedThisRun, "[A10] 2페이즈 완료 → 졸업 확정");
            t.True(!run.BossPhase2, "[A10] 졸업 확정 시 BossPhase2 리셋");
            t.Eq(16, run.Stage, "[A10] 다음 스테이지(16)로 진행");
            t.Eq(1, run.RunBossClears, "[A10] 보스 카운트 정확히 1회(2페이즈 재시작이 중복 집계하지 않음)");

            // BOSS_PHASE2 이벤트 경로 — StageFlow.ProcessSpin/BuildClearEvent가 STAGE_CLEARED 대신
            // BOSS_PHASE2를 내는지(진짜 스핀 경로로 재확인).
            var run2 = S4TestHelpers.NewRun(502L);
            run2.Stage = 15;
            run2.Asc = 10;
            var mods2 = ModsBuilder.Build(run2.MachineId, run2.CharId, run2.Perks, run2.Curses, run2.Device);
            long q = SpinResolver.QuotaOf(15, mods2, 10, false);
            var manualOutcome = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = null, gained = 0,
                newExp = q, newScore = 0, newCoins = 0, newSpinIndex = 1, quota = q, spins = 6,
            };
            var clearViaHelper = StageFlow.ClearStage(run2, manualOutcome);
            var builtEvent = StageFlow.BuildClearEvent(manualOutcome, clearViaHelper, "dev_bell");
            t.Eq("BOSS_PHASE2", builtEvent.type, "[A10] BuildClearEvent가 bossPhase2Restart일 때 BOSS_PHASE2 타입을 냄");
        }

        // Shop.cs의 private AugPrice(Tier) 미러 — SILVER=14/GOLD=24/PRISM=36(Shop.cs 손확인, 테스트가
        // 오퍼 계산 결과를 되돌려 참조하지 않고 콘텐츠 원본에서 직접 기준가를 산출하기 위함).
        private static long AugPriceForTier(Tier tier) => tier switch
        {
            Tier.SILVER => 14L,
            Tier.GOLD => 24L,
            _ => 36L, // PRISM
        };

        // ShopEntry의 "기준가"(승천/증강 배수 적용 전 원가) — 콘텐츠 정의에서 직접 조회.
        private static long BasePriceOf(ShopEntry entry)
        {
            if (entry.kind == 'A') return AugPriceForTier(Perks.ById(entry.id).tier);
            if (entry.kind == 'R') return Perks.ById(entry.id).price;
            return Items.ById(entry.id).coinCost;
        }

        // Shop.cs RoundPrice 미러 — Math.Max(1, Math.Round(v, AwayFromZero)).
        private static long RoundPriceMirror(double v) => Math.Max(1L, (long)Math.Round(v, MidpointRounding.AwayFromZero));

        // ── 상점가(A3+)·아이템칸(A5+) 배선 — Opus 2차검수 반영: offerA[i].price(시스템 산출값)를
        // "기준가" 대용으로 쓰지 않고, 콘텐츠 원본(BasePriceOf)에서 직접 기대값을 계산해 asc=0/asc=3
        // 양쪽을 독립적으로 검증한다. ─────────────────────────────────────────────────────────
        private static void ShopPriceAndItemCapWiring(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var runA = S4TestHelpers.NewRun(601L);
            runA.Asc = 0;
            var offerA = Shop.FreshOffer(runA, stat);

            var runB = S4TestHelpers.NewRun(601L); // 동일 시드 → 동일 RNG 스트림(asc는 가격에만 영향)
            runB.Asc = 3;
            var offerB = Shop.FreshOffer(runB, stat);

            t.Eq(offerA.Count, offerB.Count, "[상점가] 동일 시드 → 동일 오퍼 개수");
            // novice/basic 무퍽이라 mods.shopPriceMul==mods.itemPriceMul==1.0 — 세 종류(A/R/I) 전부
            // pm = Max(0.4, AscMods.Get(asc).ShopPriceMul) 하나로 통일된다(Shop.ItemPriceMul도 동일 값).
            double pm0 = Math.Max(0.4, AscMods.Get(0).ShopPriceMul);
            double pm3 = Math.Max(0.4, AscMods.Get(3).ShopPriceMul);
            bool anyChecked = false;
            for (int i = 0; i < offerA.Count && i < offerB.Count; i++)
            {
                t.Eq(offerA[i].id, offerB[i].id, $"[상점가] 슬롯{i} 동일 항목(id) — RNG 스트림 asc 무관 확인");
                anyChecked = true;
                long basePrice = BasePriceOf(offerA[i]);
                long expectedPriceA = RoundPriceMirror(basePrice * pm0);
                long expectedPriceB = RoundPriceMirror(basePrice * pm3);
                t.Eq(expectedPriceA, (long)offerA[i].price, $"[상점가] 슬롯{i}({offerA[i].id}) asc=0 가격 = 기준가×1.0");
                t.Eq(expectedPriceB, (long)offerB[i].price, $"[상점가] 슬롯{i}({offerA[i].id}) asc=3 가격 = 기준가×{pm3:0.##}");
            }
            t.True(anyChecked, "[상점가] 최소 1개 항목 가격 비교됨");

            // 아이템칸(A5+) — ItemUse.EffectiveSlots.
            var run0 = S4TestHelpers.NewRun(602L); run0.Asc = 0;
            var run5 = S4TestHelpers.NewRun(603L); run5.Asc = 5;
            int cap0 = ItemUse.EffectiveSlots(run0);
            int cap5 = ItemUse.EffectiveSlots(run5);
            t.Eq(cap0 - 1, cap5, "[아이템칸] A5+ -1 반영(novice/basic 무퍽 기준)");
        }

        // ── 시작 코인(A3+) 배선 — RunController 생성자, 웹 game.js:392 ─────────────────────────
        private static void StartCoinWiring(TestCtx t)
        {
            // parttime(startCoins=15) — RunController.Characters.ById는 unlockRuns 게이트를 보지 않는다
            // (엔진 계층은 해금 판정을 하지 않음, PickView가 별도 처리).
            var rc0 = new RunController("parttime", "basic", "", 701L, S4TestHelpers.GenerousStat(), asc: 0);
            t.Eq(15L, rc0.State.Coins, "[시작코인] asc=0 → startCoins 그대로");

            var rc3 = new RunController("parttime", "basic", "", 702L, S4TestHelpers.GenerousStat(), asc: 3);
            t.Eq(11L, rc3.State.Coins, "[시작코인] asc=3 → 15-4=11 (min(16,(3-2)*4)=4)");

            var rc10 = new RunController("parttime", "basic", "", 703L, S4TestHelpers.GenerousStat(), asc: 10);
            t.Eq(0L, rc10.State.Coins, "[시작코인] asc=10 → max(0,15-16)=0 (하한 클램프)");
        }

        // ── 최종 점수(A1~A10 점수 보정) 배선 — 웹 game.js:2549-2551 `finalScore = floor(score*mod*
        // am.scoreMul)`. StageFlow.ForceGameOver(GAME_OVER·자발적 포기 GiveUp 공용 경로)에 배선됨. ──
        private static void FinalScoreScoreMulWiring(TestCtx t)
        {
            var run0 = S4TestHelpers.NewRun(1101L);
            run0.Asc = 0;
            run0.Score = 50000;
            var f0 = StageFlow.ForceGameOver(run0, 0);
            double mod0 = StageFlow.ScoreModifierFor(run0.MachineId, run0.CharId);
            t.Eq((long)(50000 * mod0 * AscMods.Get(0).ScoreMul), f0.finalScore, "[최종점수] asc=0 → scoreMul=1.0(무변화)");

            var run5 = S4TestHelpers.NewRun(1102L);
            run5.Asc = 5;
            run5.Score = 50000;
            var f5 = StageFlow.ForceGameOver(run5, 0);
            double mod5 = StageFlow.ScoreModifierFor(run5.MachineId, run5.CharId);
            t.Eq((long)(50000 * mod5 * AscMods.Get(5).ScoreMul), f5.finalScore, "[최종점수] asc=5 → ×1.6 반영");
            t.True(f5.finalScore > f0.finalScore, "[최종점수] 동일 원점수라도 asc5 최종점수가 asc0보다 큼");

            var run10 = S4TestHelpers.NewRun(1103L);
            run10.Asc = 10;
            run10.Score = 50000;
            var f10 = StageFlow.ForceGameOver(run10, 0);
            double mod10 = StageFlow.ScoreModifierFor(run10.MachineId, run10.CharId);
            t.Eq((long)(50000 * mod10 * AscMods.Get(10).ScoreMul), f10.finalScore, "[최종점수] asc=10 → ×2.2 반영");
        }

        // ── ③ 점수 격리 — 웹 game.js:2554-2559 그대로 ────────────────────────────────────────
        private static void ScoreIsolation(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["bestScore"] = 1000L;

            var run = S4TestHelpers.NewRun(801L);
            run.Asc = 5;
            run.Score = 50000;
            run.Stage = 7;
            var failure = StageFlow.ForceGameOver(run, 0);
            var scratch = new StatTracker.RunScratch();
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure } }, scratch);

            t.Eq(1000L, profile.GetStat("bestScore"), "[점수격리] asc>0 런은 bestScore 불변");
            t.Eq(failure.finalScore, profile.BestAscScore, "[점수격리] bestAscScore 갱신");
            t.Eq(5, profile.BestAscLevel, "[점수격리] bestAscLevel 갱신");
            t.Eq(failure.finalScore, profile.TotalScore, "[점수격리] totalScore는 웹 실제 동작대로 asc 무관 항상 누적(game.js:2554)");

            // asc=0 런은 정상적으로 bestScore를 갱신(대조군).
            var profile2 = new PlayerProfile();
            var run2 = S4TestHelpers.NewRun(802L);
            run2.Asc = 0;
            run2.Score = 999999; // 확실히 갱신되도록 큰 값
            var failure2 = StageFlow.ForceGameOver(run2, 0);
            StatTracker.Apply(profile2, run2, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure2 } }, new StatTracker.RunScratch());
            t.Eq(failure2.finalScore, profile2.GetStat("bestScore"), "[점수격리 대조군] asc=0 런은 bestScore 정상 갱신");
            t.Eq(0L, profile2.BestAscScore, "[점수격리 대조군] asc=0 런은 bestAscScore 불변(0)");
        }

        // ── ④ 졸업 → ascMax → 선택 상한 ──────────────────────────────────────────────────────
        private static void GraduationAscMaxSelectionCap(TestCtx t)
        {
            var profile = new PlayerProfile();
            t.True(!profile.AscUnlocked(), "[졸업] 초기 프로필은 승천 잠김");
            t.Eq(0, profile.MaxPlayableAsc(), "[졸업] 초기 선택 상한 0(일반만 가능)");

            // 일반 런(asc=0) 스테이지15 클리어 = 졸업 → ascMax=0 확정(작업 지시 원문 요구사항).
            GraduateAndFinish(profile, 901L, asc: 0);
            t.Eq(0, profile.AscMax, "[졸업] 일반 런(asc=0) S15 클리어 → ascMax=0 확정");
            t.True(profile.AscUnlocked(), "[졸업] 이제 승천 선택기 해금(ascMax>=0)");
            t.Eq(1, profile.MaxPlayableAsc(), "[졸업] 선택 상한 = ascMax+1 = 1(심화1까지)");

            // 심화3 졸업 → ascMax=3으로 상승.
            GraduateAndFinish(profile, 902L, asc: 3);
            t.Eq(3, profile.AscMax, "[졸업] 심화3 졸업 → ascMax=3");
            t.Eq(4, profile.MaxPlayableAsc(), "[졸업] 선택 상한 = 4");

            // 이후 심화1 졸업(더 낮은 단계)은 ascMax를 낮추지 않음 — max(prev,asc).
            GraduateAndFinish(profile, 903L, asc: 1);
            t.Eq(3, profile.AscMax, "[졸업] 낮은 asc 졸업은 ascMax를 낮추지 않음(max(prev,asc))");

            t.Eq(profile.AscMax, profile.GetStat("ascMax"), "[졸업] Stats[\"ascMax\"] 카운터가 AscMax와 동기화(asc3/asc5 업적용)");

            // RunController/GameSession 레벨 방어적 클램프(웹 ascMods 내부 클램프와 동일 취지).
            var rcHigh = new RunController("novice", "basic", "", 904L, S4TestHelpers.GenerousStat(), asc: 999);
            t.Eq(10, rcHigh.State.Asc, "[클램프] RunController asc 상한 10(AscMods.AscMax)");
            var rcNeg = new RunController("novice", "basic", "", 905L, S4TestHelpers.GenerousStat(), asc: -5);
            t.Eq(0, rcNeg.State.Asc, "[클램프] RunController asc 하한 0");
        }

        // ── Opus 2차검수 — ascMax 로드 방어(마이그레이션 가드) ────────────────────────────────
        // ProfileDto.FromDto: ascMax==0 && graduations==0이면 -1로 강제 정정(이 필드 도입 전 세이브를
        // JsonUtility가 0으로 채워도 "asc0 졸업 완료"로 오판하지 않게). 정당한 ascMax=0은 반드시
        // graduations>=1을 동반한다(asc0 졸업이 선행돼야 StatTracker가 graduations를 올림).
        private static void AscMaxLoadGuard(TestCtx t)
        {
            // (ascMax=0, graduations=0) → -1 (마이그레이션 이전 세이브의 빈 필드로 판정, 강제 정정).
            var dtoA = new PlayerProfileDto { ascMax = 0, statKeys = new[] { "graduations" }, statValues = new long[] { 0 } };
            var pA = ProfileDto.FromDto(dtoA);
            t.Eq(-1, pA.AscMax, "[ascMax가드] (ascMax=0,graduations=0) → -1로 정정");

            // (ascMax=0, graduations=1) → 0 (정당한 asc0 졸업 — 정정하지 않음).
            var dtoB = new PlayerProfileDto { ascMax = 0, statKeys = new[] { "graduations" }, statValues = new long[] { 1 } };
            var pB = ProfileDto.FromDto(dtoB);
            t.Eq(0, pB.AscMax, "[ascMax가드] (ascMax=0,graduations=1) → 0 그대로(정당한 졸업)");

            // (ascMax=3, graduations=2) → 3 (0이 아니면 가드 자체가 개입하지 않음 — graduations와 무관).
            var dtoC = new PlayerProfileDto { ascMax = 3, statKeys = new[] { "graduations" }, statValues = new long[] { 2 } };
            var pC = ProfileDto.FromDto(dtoC);
            t.Eq(3, pC.AscMax, "[ascMax가드] (ascMax=3,graduations=2) → 3 그대로(0이 아니므로 가드 미개입)");

            // 대조군: graduations 키 자체가 없는(완전 구세이브) 경우도 동일하게 -1로 정정.
            var dtoD = new PlayerProfileDto { ascMax = 0 };
            var pD = ProfileDto.FromDto(dtoD);
            t.Eq(-1, pD.AscMax, "[ascMax가드] statKeys 자체가 없는 구세이브도 -1로 정정");
        }

        private static void GraduateAndFinish(PlayerProfile profile, long seed, int asc)
        {
            var run = S4TestHelpers.NewRun(seed);
            run.Stage = 15;
            run.Asc = asc;
            var mods = ModsBuilder.Build(run.MachineId, run.CharId, run.Perks, run.Curses, run.Device);
            long q = SpinResolver.QuotaOf(15, mods, asc, false);
            var outcome = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = null, gained = 0,
                newExp = q, newScore = 0, newCoins = 0, newSpinIndex = 1, quota = q, spins = 6,
            };
            StageFlow.ClearStage(run, outcome);
            var failure = StageFlow.ForceGameOver(run, 0);
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure } }, new StatTracker.RunScratch());
        }

        // ── 숙련도(Mastery) AscMax 배선 — 웹 game.js:226 `_bumpMastery` ─────────────────────────
        private static void MasteryAscMaxWiring(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(1001L);
            run.Asc = 4;
            run.GraduatedThisRun = true;
            run.Stage = 15;
            var failure = StageFlow.ForceGameOver(run, 0);
            MasteryTracker.ApplyRunEnd(profile, run, failure);

            bool hasCharBag = profile.Mastery.TryGetValue("char", out var charBag);
            t.True(hasCharBag, "[숙련도] char 버킷 생성됨");
            MasteryStats noviceStats = null;
            bool hasNovice = hasCharBag && charBag.TryGetValue("novice", out noviceStats);
            t.True(hasNovice, "[숙련도] novice 항목 생성됨");
            t.Eq(4, noviceStats != null ? noviceStats.AscMax : -999, "[숙련도] 졸업 런의 asc가 MasteryStats.AscMax에 반영");

            // 대조군: graduatedThisRun=false면 AscMax 미갱신(기본값 -1 유지).
            var profile2 = new PlayerProfile();
            var run2 = S4TestHelpers.NewRun(1002L);
            run2.Asc = 6;
            run2.GraduatedThisRun = false;
            var failure2 = StageFlow.ForceGameOver(run2, 0);
            MasteryTracker.ApplyRunEnd(profile2, run2, failure2);
            var noviceStats2 = profile2.Mastery["char"]["novice"];
            t.Eq(-1, noviceStats2.AscMax, "[숙련도 대조군] 미졸업 런은 AscMax 갱신 안 함(기본값 -1 유지)");
        }

        // ── ⑤ 자동플레이 하네스 asc 대응 — BOSS_PHASE2 이벤트를 포함해 예외 없이 동작 ──────────────
        private static readonly HashSet<string> KnownEventTypesAsc = new HashSet<string>
        {
            "REJECTED", "SPIN_RESULT", "STAGE_CLEARED", "REVIVED", "POST_SPIN", "GAME_OVER",
            "DEVICE_MANIP_RESULT", "NODE_RESOLVED", "PERK_OFFER", "PERK_GRANTED", "PERK_HELD",
            "RETAKE_EMPTY", "SHOP_OFFER", "SHOP_PURCHASED", "SHOP_REROLLED", "SHOP_LEFT",
            "ITEM_USED", "DEVICE_ARMED", "DEVICE_PEEK", "RUN_STARTED", "DEVICE_OFFER",
            "PERK_LEVELED", "STAGE_STARTED", "BOSS_PHASE2",
        };

        private static void AutoplayHarnessAsc(TestCtx t)
        {
            for (long seed = 9001; seed < 9006; seed++)
            {
                var rc = new RunController("novice", "basic", "", seed, S4TestHelpers.GenerousStat(), asc: 10);
                t.Eq(10, rc.State.Asc, $"[autoplay-asc seed={seed}] asc 반영 확인");
                bool reachedGameOver = AutoPlayAsc(rc, 20_000);
                t.True(reachedGameOver, $"[autoplay-asc seed={seed}] 예외 없이 게임오버까지 진행(stage={rc.State.Stage})");
            }
        }

        private static bool AutoPlayAsc(RunController rc, int guardMax)
        {
            int guard = 0;
            int shopStep = 0;
            while (rc.State.Phase != RunPhase.GameOver && guard < guardMax)
            {
                guard++;
                var phase = rc.State.Phase;
                IReadOnlyList<RunEvent> events;
                switch (phase)
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
                        events = rc.Do(new PickOffer(0));
                        break;
                    case RunPhase.EventShop:
                        if (shopStep == 0) { events = rc.Do(new BuyOffer(0)); shopStep = 1; }
                        else { events = rc.Do(new LeaveShop()); shopStep = 0; }
                        break;
                    case RunPhase.DeviceNode:
                        events = rc.Do(new TakeDevice(true));
                        break;
                    case RunPhase.RewardDone:
                        events = rc.Do(new ProceedToStage());
                        break;
                    default:
                        throw new InvalidOperationException("AutoPlayAsc: 처리 불가 Phase=" + phase);
                }
                foreach (var e in events)
                {
                    if (!KnownEventTypesAsc.Contains(e.type))
                        throw new InvalidOperationException("AutoPlayAsc: 알 수 없는 RunEvent.type=" + e.type);
                }
            }
            return rc.State.Phase == RunPhase.GameOver;
        }
    }
}
