using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // S3 골든 테스트 — Run/RunState.cs·Mods.cs·SpinResolver.cs·StageFlow.cs 검증.
    // ENGINE_PORT_DESIGN.md 작업 지시 4항목: ①스핀 재현성·EXP 합산 ②Mods 집계(대표 퍽5·세트게이트1·
    // 캐릭터3) ③스테이지 클리어/폭망 경로 ④미니 시뮬레이션(시드100×자동스핀, 예외0·분포 Report).
    // 기대값은 kotlin-reference/game/SlotV2Engine.kt buildMods(L1730-2026)을 손으로 재계산해 얻었다
    // (Mods.cs 실행 결과를 베껴 넣지 않음 — 순환 검증 방지, S1 규칙과 동일하게 적용).

    internal static class RunTestHelpers
    {
        public static RunState NewRun(long seed, string charId = "novice", string machineId = "basic")
        {
            return new RunState(seed) { CharId = charId, MachineId = machineId, Phase = RunPhase.Spin };
        }
    }

    // ── ① 고정 시드 스핀 시나리오 — 릴 재현성·EXP 합산 (기본 캐릭 novice·기본 머신 basic) ──────────
    internal static class Tests_Run_SpinRepro
    {
        public static void Run(TestCtx t)
        {
            // 동일 시드 두 RunState → 릴 시퀀스·EXP 누적 완전 일치(RunState 자체 재현성 — 원칙 2).
            var runA = RunTestHelpers.NewRun(12345L);
            var runB = RunTestHelpers.NewRun(12345L);
            var cellsA = new List<string>();
            var cellsB = new List<string>();
            long sumGainA = 0, sumGainB = 0;

            for (int i = 0; i < 5; i++)
            {
                var step = StageFlow.ProcessSpin(runA, SpinMode.N);
                t.True(!step.spin.rejected, $"[repro] A 스핀{i} 거부되지 않음");
                cellsA.Add(string.Join(",", runA.LastCells));
                sumGainA += step.spin.gained;
                if (step.kind == SpinStepKind.Cleared || step.kind == SpinStepKind.GameOver) break;
            }
            for (int i = 0; i < 5; i++)
            {
                var step = StageFlow.ProcessSpin(runB, SpinMode.N);
                cellsB.Add(string.Join(",", runB.LastCells));
                sumGainB += step.spin.gained;
                if (step.kind == SpinStepKind.Cleared || step.kind == SpinStepKind.GameOver) break;
            }
            TestHelpers.CollectionEq(t, cellsA, cellsB, "[repro] 동일 시드 → 릴 시퀀스 완전 일치");
            t.Eq(sumGainA, sumGainB, "[repro] 동일 시드 → EXP 누적합 완전 일치");

            // EXP 합산 검증 — 스테이지 유지 중(클리어 전)에는 run.StageExp가 매 스핀 gained의 누적과 같아야 함.
            var runC = RunTestHelpers.NewRun(999L);
            long acc = 0;
            for (int i = 0; i < 3; i++)
            {
                var step = StageFlow.ProcessSpin(runC, SpinMode.N);
                if (step.kind == SpinStepKind.Cleared || step.kind == SpinStepKind.GameOver) break;
                acc += step.spin.gained;
                t.Eq(acc, runC.StageExp, $"[repro] step{i} StageExp == 누적 gained (스핀 gained 계산·상태반영 일관성)");
            }

            // 시드가 다르면 결과도 달라짐 — "항상 같은 값"을 하드코딩해 우연히 통과하는 게 아님을 확인.
            var runD = RunTestHelpers.NewRun(1L);
            var runE = RunTestHelpers.NewRun(2L);
            var stepD = StageFlow.ProcessSpin(runD, SpinMode.N);
            var stepE = StageFlow.ProcessSpin(runE, SpinMode.N);
            bool differs = !runD.LastCells.SequenceEqual(runE.LastCells) || stepD.spin.gained != stepE.spin.gained;
            t.True(differs, "[repro] 시드가 다르면 릴/EXP 결과도 달라짐");

            // LockedNext 경로 — 비어있지 않으면 RNG를 소비하지 않고 그대로 셀을 사용해야 한다(§2 step16).
            // 5칸 전부 crown → 즉시 잭팟(+대량 EXP)으로 이번 스핀에 스테이지가 클리어되므로, StageFlow가
            // 클리어 시점에 run.LastCells를 리셋하기 전의 값인 SpinOutcome.result.cells로 검증한다
            // (run.LastCells는 "다음 스테이지 준비"의 일부로 지워지는 게 Kotlin 원본과 동일한 정상 동작 —
            // §3-E, StageFlow.ClearStage 참조. 이 스핀 자체가 무엇을 굴렸는지는 result가 진실).
            var runF = RunTestHelpers.NewRun(555L);
            runF.LockedNext.AddRange(new[] { "crown", "crown", "crown", "crown", "crown" });
            var stepF = StageFlow.ProcessSpin(runF, SpinMode.N);
            var usedCellIds = stepF.spin.result.cells.Select(c => c.sym.id).ToList();
            TestHelpers.CollectionEq(t, new[] { "crown", "crown", "crown", "crown", "crown" }, usedCellIds,
                "[repro] LockedNext 지정 시 그대로 셀 사용(RNG 미소비, 잭팟 유발 검증 겸용)");
            t.True(stepF.spin.result.jackpotSym == "crown", "[repro] 5칸 전부 crown → 잭팟 발동");
            t.Eq(SpinStepKind.Cleared, stepF.kind, "[repro] 잭팟 대량EXP로 이번 스핀에 즉시 클리어(부수 확인)");
        }
    }

    // ── ② Mods 집계 검증 — 대표 퍽 5종·세트 게이트 1종·캐릭터 3종 (Kotlin buildMods 손계산) ──────────
    internal static class Tests_Run_ModsAggregation
    {
        public static void Run(TestCtx t)
        {
            RepresentativePerks(t);
            SetGate(t);
            Characters(t);
        }

        // 대표 퍽 5종: study(단순 expMul) · cherry_up(perSymbolExp) · center(centerExpMul) ·
        // overdrive(PRISM expMul) · sacrifice(ctx조건부, curseCount 의존 + 무조건 clearCoinBonus).
        // 손계산(캐릭터=novice: quotaMul*=0.92 만 기여):
        //   expMul: 1.0 → ×1.10(study) → ×1.6(overdrive) → ×(1+0.06×1)=1.06(sacrifice, curseCount=1)
        //          = 1.10 × 1.6 × 1.06 = 1.8656
        //   perSymbolExp[cherry] = 2 (cherry_up만)
        //   centerExpMul = 2.0 (center)
        //   clearCoinBonus = -1 (sacrifice, 무조건)
        //   quotaMul = 0.92(novice) × 1.10(hard_exam curse) = 1.012
        //   scoreMul = 1.0 (웹 파리티 P3-4: hard_exam은 패널티 전용화로 scoreMul 보너스가 삭제됨 — 요구치
        //   +10%만 남는다, Perks.cs 저주 헤더 각주)
        private static void RepresentativePerks(TestCtx t)
        {
            var perks = new List<string> { "study", "cherry_up", "center", "overdrive", "sacrifice" };
            var curses = new List<string> { "hard_exam" };
            var ctx = new RunCtx { curseCount = curses.Count };
            var m = ModsBuilder.Build("basic", "novice", perks, curses, "", ctx);

            t.EqTol(1.8656, m.expMul, "[mods5] expMul = study×overdrive×sacrifice(curseCount=1)");
            t.Eq(2, m.perSymbolExp.TryGetValue(Sym.Cherry, out var pse) ? pse : 0, "[mods5] perSymbolExp[cherry]=2(cherry_up)");
            t.EqTol(2.0, m.centerExpMul, "[mods5] centerExpMul=2.0(center)");
            t.Eq(-1, m.clearCoinBonus, "[mods5] clearCoinBonus=-1(sacrifice, 무조건)");
            t.EqTol(1.012, m.quotaMul, "[mods5] quotaMul=novice×hard_exam");
            t.EqTol(1.0, m.scoreMul, "[mods5] scoreMul=hard_exam(구 ×1.20 보너스 폐기, 웹 파리티 P3-4)");

            // sacrifice의 expMul 기여는 curseCount에 비례 — curseCount=0이면 배수 1.0(무효과)이어야 함.
            var ctx0 = new RunCtx { curseCount = 0 };
            var m0 = ModsBuilder.Build("basic", "novice", perks, new List<string>(), "", ctx0);
            t.EqTol(1.10 * 1.6 * 1.0, m0.expMul, "[mods5] sacrifice curseCount=0 → expMul 기여 없음(clearCoinBonus는 여전히 -1)");
            t.Eq(-1, m0.clearCoinBonus, "[mods5] sacrifice clearCoinBonus는 curseCount=0이어도 -1(무조건)");
        }

        // 세트 게이트 1종 — set_orchard(requires=[cherry_up,cherry_farm], reqChar/Machine/Device 없음).
        // 손계산: cherry_up pse(+2) + cherry_farm pse(+4),wmul(×1.3) + set_orchard pse(+3),wmul(×1.25)
        //   perSymbolExp[cherry] = 2+4+3 = 9
        //   symbolWeightMul[cherry] = 1.0(머신 basic weightMul 없음) × 1.3(cherry_farm) × 1.25(set_orchard) = 1.625
        private static void SetGate(TestCtx t)
        {
            var full = new List<string> { "cherry_up", "cherry_farm" };
            var mFull = ModsBuilder.Build("basic", "gambler", full, new List<string>(), "");
            t.Eq(9, mFull.perSymbolExp.TryGetValue(Sym.Cherry, out var pse) ? pse : 0,
                "[setgate] set_orchard 발동 시 perSymbolExp[cherry]=9(2+4+3)");
            t.EqTol(1.625, mFull.symbolWeightMul.TryGetValue(Sym.Cherry, out var wm) ? wm : 1.0,
                "[setgate] set_orchard 발동 시 symbolWeightMul[cherry]=1.625(1.3×1.25)");

            // requires 중 1개만 보유하면 세트 미발동(요구조건 부분충족 거부 확인).
            var partial = new List<string> { "cherry_up" };
            var mPartial = ModsBuilder.Build("basic", "gambler", partial, new List<string>(), "");
            t.Eq(2, mPartial.perSymbolExp.TryGetValue(Sym.Cherry, out var psePartial) ? psePartial : 0,
                "[setgate] cherry_up만 보유 → 세트 미발동, perSymbolExp[cherry]=2(cherry_up만)");
            t.True(!mPartial.symbolWeightMul.ContainsKey(Sym.Cherry),
                "[setgate] cherry_up만 보유 → symbolWeightMul[cherry] 키 자체 없음(wmul 호출 안 됨)");
        }

        // 캐릭터 3종: novice(quotaMul×0.92) · daredevil(ctx분기 3가지) · cultist(퍽루프+후처리 결합).
        private static void Characters(TestCtx t)
        {
            var none = new List<string>();

            var mNovice = ModsBuilder.Build("basic", "novice", none, none, "");
            t.EqTol(0.92, mNovice.quotaMul, "[char] novice quotaMul=0.92, 그 외 무효과");
            t.EqTol(1.0, mNovice.expMul, "[char] novice expMul 무변화");

            // daredevil: expMul×1.1·quotaMul×1.2 기본 + else-if 분기(막스핀 ×1.6 / 남은≤2 ×1.35 / 그 외 없음).
            // 손계산: 1.1 × (막스핀?1.6 : 남은≤2?1.35 : 1.0)
            var ctxLast = new RunCtx { spinIndex = 4, spinsPerStage = 5 }; // IsLastSpin=true
            var mLast = ModsBuilder.Build("basic", "daredevil", none, none, "", ctxLast);
            t.EqTol(1.1 * 1.6, mLast.expMul, "[char] daredevil 막스핀 → expMul=1.1×1.6");
            t.EqTol(1.2, mLast.quotaMul, "[char] daredevil quotaMul=1.2(항상)");

            var ctxNear = new RunCtx { spinIndex = 3, spinsPerStage = 5 }; // IsLastSpin=false, SpinsLeft=2
            var mNear = ModsBuilder.Build("basic", "daredevil", none, none, "", ctxNear);
            t.EqTol(1.1 * 1.35, mNear.expMul, "[char] daredevil 남은스핀2(막스핀아님) → expMul=1.1×1.35");

            var ctxEarly = new RunCtx { spinIndex = 0, spinsPerStage = 5 }; // IsLastSpin=false, SpinsLeft=5
            var mEarly = ModsBuilder.Build("basic", "daredevil", none, none, "", ctxEarly);
            t.EqTol(1.1, mEarly.expMul, "[char] daredevil 첫스핀(막스핀도 남은≤2도 아님) → expMul=1.1만");

            // cultist: 퍽루프에서 skullExp+=3, 저주 2개(hard_exam+frugal_vow) + 후처리 scoreMul×(1+0.08×2).
            // 웹 파리티 P3-4(Perks.cs 저주 헤더 각주) — hard_exam/frugal_vow 둘 다 패널티 전용화되어
            // frugal_vow의 quotaMul 보너스(구 0.88)·hard_exam의 scoreMul 보너스(구 1.20)가 삭제됐다.
            // 손계산: quotaMul = 1.0×1.10(hard_exam, frugal_vow는 quotaMul 없음) = 1.10
            //         coinMul = 1.0×0.6(frugal_vow) = 0.6
            //         scoreMul = 1.0(hard_exam 보너스 없음) × (1+0.08×2)=1.16(후처리) = 1.16
            var curses2 = new List<string> { "hard_exam", "frugal_vow" };
            var mCultist = ModsBuilder.Build("basic", "cultist", none, curses2, "");
            t.Eq(3, mCultist.skullExp, "[char] cultist skullExp=3");
            t.EqTol(1.10, mCultist.quotaMul, "[char] cultist quotaMul=hard_exam(frugal_vow quotaMul 보너스 폐기)");
            t.EqTol(0.6, mCultist.coinMul, "[char] cultist coinMul=frugal_vow");
            t.EqTol(1.16, mCultist.scoreMul, "[char] cultist scoreMul=후처리(1+0.08×2)(hard_exam scoreMul 보너스 폐기)");

            // gambler/honor — buildMods when-block에 케이스 없음(01_engine.md §4 표, 부록A-3) → 완전 무효과.
            var mGambler = ModsBuilder.Build("basic", "gambler", none, none, "");
            t.EqTol(1.0, mGambler.expMul, "[char] gambler buildMods 무효과(expMul)");
            t.EqTol(1.0, mGambler.quotaMul, "[char] gambler buildMods 무효과(quotaMul)");
            var mHonor = ModsBuilder.Build("basic", "honor", none, none, "");
            t.EqTol(1.0, mHonor.expMul, "[char] honor buildMods 무효과(expMul) — 시작 증강은 서비스 레이어(S4) 처리");
        }
    }

    // ── ③ 스테이지 클리어/폭망 경로 — RunState를 직접 조작해 결정론적으로 분기를 강제한다 ──────────
    // (RNG 결과에 기대는 대신, StageExp/Stage/SpinIndex를 사전에 세팅해 클리어/실패가 100% 재현되게 만듦.
    //  로직 자체 검증이 목적이므로 이 방식이 seed 탐색보다 안정적이다.)
    internal static class Tests_Run_StageFlow
    {
        public static void Run(TestCtx t)
        {
            ClearPath(t);
            GameOverPath(t);
            InsuranceRevivePath(t);
            PostSpinPath(t);
        }

        private static void ClearPath(TestCtx t)
        {
            var run = RunTestHelpers.NewRun(1L);
            run.StageExp = 1_000_000; // 어떤 롤이 나와도 quota(stage1) 초과가 보장됨
            int stageBefore = run.Stage;

            var step = StageFlow.ProcessSpin(run, SpinMode.N);

            t.Eq(SpinStepKind.Cleared, step.kind, "[clear] StageExp 선점 → 즉시 클리어");
            t.Eq(stageBefore, step.clear.clearedStage, "[clear] clearedStage=클리어 전 스테이지");
            t.Eq(stageBefore + 1, run.Stage, "[clear] 클리어 후 Stage+1");
            t.Eq(0, run.SpinIndex, "[clear] 클리어 후 SpinIndex=0");
            t.Eq(0L, run.StageExp, "[clear] 클리어 후 StageExp=0");
            t.Eq(RunPhase.NodeSelect, run.Phase, "[clear] 클리어 후 Phase=NodeSelect");
            t.Eq(3, run.NodeOptions.Count, "[clear] 노드 옵션 항상 3개");
            t.True(run.NodeOptions.Contains(NodeKind.Augment), "[clear] 노드 옵션에 AUGMENT 필수 포함");
            t.Eq(0, run.StageBonusSpins, "[clear] StageBonusSpins 리셋");
            t.True(run.ArmItems.Count == 0 && run.PhaseItems.Count == 0, "[clear] armItems/phaseItems 리셋");
        }

        // stage=49(보스 아님, 49%5=4) + StageExp=0 + SpinIndex=마지막(4) → 그 스테이지 quota는 매우 커서
        // (Formulas.Quota는 stage>15부터 1.2배씩 누적곱) 어떤 단일 스핀 결과로도 도달 불가 → 확정 게임오버.
        private static void GameOverPath(TestCtx t)
        {
            var run = RunTestHelpers.NewRun(2L);
            run.Stage = 49;
            run.StageExp = 0;
            run.SpinIndex = 4; // spins=5(보너스/보스 없음) → 마지막 스핀
            t.True(!Formulas.IsBossStage(49), "[gameover] 사전조건: stage49는 보스 아님");

            var step = StageFlow.ProcessSpin(run, SpinMode.N);

            t.Eq(SpinStepKind.GameOver, step.kind, "[gameover] 초대형 쿼터 + 마지막 스핀 → 게임오버");
            t.Eq("GAME_OVER", step.failure.kind, "[gameover] FailureOutcome.kind");
            t.Eq(RunPhase.GameOver, run.Phase, "[gameover] Phase=GameOver");
            double expectedMod = StageFlow.ScoreModifierFor(run.MachineId, run.CharId);
            t.Eq((long)(run.Score * expectedMod), step.failure.finalScore,
                "[gameover] finalScore = run.Score × scoreModifier(머신×캐릭터)");
        }

        private static void InsuranceRevivePath(TestCtx t)
        {
            var run = RunTestHelpers.NewRun(3L);
            run.Survive = true;
            run.Stage = 49; // GameOverPath와 동일 이유로 이번 스핀 클리어는 불가능해야 정확히 회생경로만 탐
            run.StageExp = 0;
            run.SpinIndex = 4;

            var step = StageFlow.ProcessSpin(run, SpinMode.N);

            t.Eq(SpinStepKind.Revived, step.kind, "[insurance] 보험증서 보유 → 회생");
            t.Eq("INSURANCE_REVIVE", step.failure.kind, "[insurance] FailureOutcome.kind");
            t.True(!run.Survive, "[insurance] 보험증서 1회 소모(Survive=false)");
            t.Eq(2, run.StageBonusSpins, "[insurance] StageBonusSpins+2");
            t.True(run.Phase != RunPhase.GameOver, "[insurance] 게임오버로 가지 않음");
        }

        private static void PostSpinPath(TestCtx t)
        {
            var run = RunTestHelpers.NewRun(4L);
            run.Device = "dev_reroll"; // MANIP 장치, 이번 스테이지 미사용
            run.Stage = 49;
            run.StageExp = 0;
            run.SpinIndex = 4;

            var step = StageFlow.ProcessSpin(run, SpinMode.N);

            t.Eq(SpinStepKind.PostSpin, step.kind, "[postspin] MANIP 장치 보유+미사용 → 만회 기회");
            t.Eq("POST_SPIN", step.failure.kind, "[postspin] FailureOutcome.kind");
            t.Eq(RunPhase.PostSpin, run.Phase, "[postspin] Phase=PostSpin");
            t.True(step.failure.manipHints.Contains("DEVICE:dev_reroll"), "[postspin] manipHints에 장치 힌트 포함");
        }
    }

    // ── ④ 미니 시뮬레이션 — 시드 100개 × 상점/장치 없이 자동 스핀(mode=N)만으로 게임오버까지 진행 ──────
    // 노드 선택(AUGMENT 등)은 S4 미구현 범위라 이 시뮬레이션에서는 발동하지 않는다(장치 미장착 +
    // charId=novice라 POST_SPIN도 발생하지 않아야 함 — 발생 시 방어적으로 즉시 게임오버 처리).
    internal static class Tests_Run_Simulation
    {
        public static void Run(TestCtx t)
        {
            int exceptions = 0;
            var stagesReached = new List<int>();

            for (long seed = 1; seed <= 100; seed++)
            {
                try
                {
                    var run = RunTestHelpers.NewRun(seed);
                    int guard = 0;
                    while (run.Phase != RunPhase.GameOver && guard < 20_000)
                    {
                        guard++;
                        var step = StageFlow.ProcessSpin(run, SpinMode.N);
                        if (step.spin.rejected)
                            throw new InvalidOperationException("mode=N인데 거부됨: " + step.spin.rejectReason);

                        if (step.kind == SpinStepKind.Cleared) run.Phase = RunPhase.Spin;
                        else if (step.kind == SpinStepKind.PostSpin)
                        {
                            // 장치 미장착·비-gambler 조합에서는 이론상 도달 불가 — 혹시 도달하면 방어적 종료.
                            StageFlow.ForceGameOver(run, step.failure.deficitAtFailure);
                        }
                    }
                    t.True(run.Phase == RunPhase.GameOver, $"[sim seed={seed}] guard(20000) 내 게임오버 도달");
                    stagesReached.Add(run.Stage);
                }
                catch (Exception ex)
                {
                    exceptions++;
                    t.Fail($"[sim seed={seed}]", "예외 발생: " + ex);
                }
            }

            t.Eq(0, exceptions, "[sim] 시드 100개 자동스핀 런 — 예외 0건");

            if (stagesReached.Count > 0)
            {
                stagesReached.Sort();
                int min = stagesReached[0];
                int max = stagesReached[stagesReached.Count - 1];
                double avg = stagesReached.Average();
                t.Report("[sim] 도달 스테이지 분포", $"n={stagesReached.Count} min=S{min} max=S{max} avg=S{avg:F2}");

                var buckets = new SortedDictionary<int, int>();
                foreach (var s in stagesReached)
                {
                    int bucketStart = (s - 1) / 5 * 5 + 1;
                    buckets[bucketStart] = buckets.TryGetValue(bucketStart, out var c) ? c + 1 : 1;
                }
                foreach (var kv in buckets)
                    t.Report("[sim] 구간별", $"S{kv.Key}~S{kv.Key + 4}: {kv.Value}건");
            }
        }
    }
}
