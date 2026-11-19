using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    // 재미 개편 U1 — FUN_OVERHAUL_DESIGN §1 R1~R6·F1 의 C# 엔진 이식 검증 (2026-10-28).
    // 웹 정본: 커밋 e6bde8d `public/play/_harness.mjs` 의 `FUN-W1` 블록(R1~R6·F1·STRESS·ISO) 단언값을 그대로 옮겼다.
    //
    // 웹 하니스와의 구조 차이(단언 의미는 동일):
    //  · 웹은 `game._roll` 을 심볼열로 갈아끼워 홀드/피버를 검증하지만, C# 엔진에는 그런 접점이 없다. 메인 스핀은
    //    RunState.LockedNext(예언 확정 칸)로, 앵콜 스핀은 EncoreFlow.Spin 의 내부 forcedIds 로 칸을 고정한다.
    //    홀드는 "fresh 굴림만" 대상이라 LockedNext 로 고정할 수 없으므로 같은 시드의 대조군(홀드 없음)과 비교한다
    //    (홀드는 RNG 소비를 바꾸지 않으므로 대조군의 raw 가 곧 '홀드 없을 때의 fresh 굴림').
    //  · 웹은 quota/spins 를 직접 대입(funRoomy)하지만, C# 은 매번 QuotaOf/EffSpins 로 즉석 계산하므로 "클리어되지 않는
    //    스테이지"를 비보스 41스테이지(요구치 거대) + StageBonusSpins 50 으로 만든다(FunTestHelpers.Roomy).
    //  · 기대값은 웹 하니스의 정확값 단언을 가져오되, 요구치·스핀 수에 의존하는 값은 outcome.quota/spins 에서 손계산
    //    공식으로 도출한다. (구 C# 의 보스 스핀 비례 prop·EXP 후처리 순서·불꽃 여파·기도 임계 이탈은 2026-10-28 웹 채택으로
    //    해소됐다 — WEB_PARITY_DESIGN §2-(II)/(JJ). 아래 Tests_FunW1_WebCross 가 웹 정본 스크립트 값과 교차 단언한다.)
    // ══════════════════════════════════════════════════════════════════════════════════════════

    internal static class FunTestHelpers
    {
        public static RunController NewController(long seed, bool deep = false, string device = "", int asc = 0, string charId = "novice")
            => new RunController(charId, "basic", device, seed, S4TestHelpers.GenerousStat(), asc: asc, deep: deep);

        // 웹 funRoomy: 어떤 스핀 결과로도 클리어되지 않고 스핀이 모자라지 않게 한다 — 비보스 41스테이지(요구치 ≈ 3.5e5) + 보너스 스핀 50.
        public static void Roomy(RunState run, int stage = 41)
        {
            run.Stage = stage;
            run.StageBonusSpins = 50;
        }

        public static void Force(RunState run, params string[] ids)
        {
            run.LockedNext.Clear();
            run.LockedNext.AddRange(ids);
        }

        // 메인 스핀 1회(칸을 고정하려면 ids 를 넘긴다 — LockedNext 경로). 결과 SpinOutcome 반환(거부 포함).
        public static SpinOutcome SpinN(RunController rc, params string[] ids) => SpinM(rc, SpinMode.N, ids);

        public static SpinOutcome SpinM(RunController rc, SpinMode mode, params string[] ids)
        {
            if (ids != null && ids.Length > 0) Force(rc.State, ids);
            var ev = rc.Do(new Spin(mode));
            return ev[0].spin;
        }

        public static List<string> RawIds(RunState run) => run.LastRaw.Select(c => c.sym.id).ToList();

        // TestHelpers.CollectionEq 는 IReadOnlyList 전용이라 IEnumerable 입력(LINQ 결과 등)을 위한 얇은 어댑터.
        public static void SeqEq<T>(TestCtx t, IEnumerable<T> expected, IEnumerable<T> actual, string label)
            => TestHelpers.CollectionEq(t, expected.ToList(), actual.ToList(), label);

        public static readonly string[] Pair0 = { "cherry", "book", "star", "gem", "crown" };   // 세트 없음
        public static readonly string[] Set2 = { "cherry", "cherry", "book", "star", "gem" };
        public static readonly string[] Set3 = { "cherry", "cherry", "cherry", "book", "star" };
        public static readonly string[] Set4 = { "cherry", "cherry", "cherry", "cherry", "book" };
        public static readonly string[] Set5 = { "cherry", "cherry", "cherry", "cherry", "cherry" };

        public static readonly HashSet<string> KnownEvents = new HashSet<string>
        {
            "REJECTED", "SPIN_RESULT", "STAGE_CLEARED", "REVIVED", "POST_SPIN", "GAME_OVER",
            "DEVICE_MANIP_RESULT", "NODE_RESOLVED", "PERK_OFFER", "PERK_GRANTED", "PERK_HELD",
            "RETAKE_EMPTY", "SHOP_OFFER", "SHOP_PURCHASED", "SHOP_REROLLED", "SHOP_LEFT",
            "ITEM_USED", "DEVICE_ARMED", "DEVICE_PEEK", "RUN_STARTED",
            "DEVICE_OFFER", "PERK_LEVELED", "STAGE_STARTED", "BOSS_PHASE2",
            "HOLD_CHANGED", "ENCORE_SPIN", "ENCORE_SETTLED",
        };

        // 첫 스핀으로 스테이지를 클리어해 NodeSelect(=앵콜 창)까지 간다. left = 클리어 후 남은 스핀 수(1~).
        // StageExp 를 크게 줘서 어떤 칸이든 확실히 클리어되게 하고, 클리어 스핀의 칸은 Pair0(세트 없음)로 고정한다.
        public static RunController Cleared(long seed, int left = 4, int stage = 1, bool debt = false, string device = "", int asc = 0, bool phaseItem = false)
        {
            var rc = NewController(seed, device: device, asc: asc);
            var run = rc.State;
            run.Stage = stage;
            int totalSpins = SpinResolver.EffSpins(run, ModsBuilder.Build("basic", "novice", new List<string>(), new List<string>(), ""));
            run.SpinIndex = totalSpins - 1 - left;
            run.StageExp = 100_000;
            if (debt) run.DebtStages = 1;
            if (phaseItem) run.PhaseItems.Add(Items.All.First(i => i.kind == "PHASE").id);
            Force(run, Pair0);
            rc.Do(new Spin(SpinMode.N));
            return rc;
        }

        // 클리어 점수 합계(웹 clearSummary.gain)를 ClearOutcome 과 무관하게 정의대로 재계산 — stage×50 + 초과×2 + 남은스핀×100 + 보스500 + 연승.
        public static long StakeFormula(ClearOutcome c) =>
            c.clearedStage * 50L + c.leftover * 2L + c.leftSpins * 100L + (c.boss ? 500L : 0L) + Formulas.StreakBonus(c.clearedStage);
    }

    // ── R1 보스 곡선 ──────────────────────────────────────────────────────────────────────────
    internal static class Tests_FunW1_R1_Boss
    {
        public static void Run(TestCtx t)
        {
            var fin = Bosses.ById("finals");
            var str = Bosses.ById("strict");
            t.EqTol(1.15, fin.quotaMul, "[R1] finals.quotaMul 1.15");
            t.Eq(1, fin.bonusSpins, "[R1] finals.bonusSpins 1");
            t.Eq("스핀+1 · 요구치 ×1.15 · 막스핀 EXP ×2 · 첫스핀 -10%", fin.desc, "[R1] finals.desc");
            t.EqTol(1.0, str.quotaMul, "[R1] strict.quotaMul 1.0");
            t.Eq("스핀+1 · 같은심볼 3개↑ ×1.15 / 없으면 ×0.75", str.desc, "[R1] strict.desc");
            t.EqTol(1.15, Bosses.QuotaMulFor(5), "[R1] bossQuotaMul(S5=기말고사) 1.15");
            t.EqTol(1.0, Bosses.QuotaMulFor(10), "[R1] bossQuotaMul(S10=꼰대교수) 1.0");

            // ApplyBoss — 웹 harness 정확값.
            var b3 = SpinResolver.ApplyBoss(str, 100, new SpinResult { bestSetCount = 3 }, 2, 6);
            t.Eq(115L, b3.gained, "[R1] strict 3매치 exp(100→115)");
            t.Eq("👨‍🏫 3매치 ×1.15", b3.note.TrimStart(' ', '·'), "[R1] strict 3매치 note");
            t.Eq(115L, SpinResolver.ApplyBoss(str, 100, new SpinResult { bestSetCount = 5 }, 2, 6).gained, "[R1] strict 5매치 exp");
            var b2 = SpinResolver.ApplyBoss(str, 100, new SpinResult { bestSetCount = 2 }, 2, 6);
            t.Eq(75L, b2.gained, "[R1] strict 페어 exp(100→75)");
            t.Eq("👨‍🏫 3매치 없음 ×0.75", b2.note.TrimStart(' ', '·'), "[R1] strict 페어 note");
            t.Eq(37L, SpinResolver.ApplyBoss(str, 33, new SpinResult { bestSetCount = 4 }, 1, 6).gained, "[R1] strict 3매치 floor(33×1.15=37.95)");
            t.Eq(24L, SpinResolver.ApplyBoss(str, 33, new SpinResult { bestSetCount = 0 }, 1, 6).gained, "[R1] strict 페어 floor(33×0.75=24.75)");
            t.Eq(90L, SpinResolver.ApplyBoss(fin, 100, new SpinResult { bestSetCount = 0 }, 0, 6).gained, "[R1] finals 첫스핀 ×0.9");
            t.Eq(200L, SpinResolver.ApplyBoss(fin, 100, new SpinResult { bestSetCount = 0 }, 5, 6).gained, "[R1] finals 막스핀 ×2");
            t.Eq(100L, SpinResolver.ApplyBoss(fin, 100, new SpinResult { bestSetCount = 0 }, 3, 6).gained, "[R1] finals 중간스핀 ×1");

            // 게임 경유: S5(기말고사) 스테이지 요구치 = floor(190 × novice 0.92 × 1.15) = 201 — 웹 game.js:423 공식과 동일
            // (옛 Kotlin 보스 스핀 비례 prop ×1.2 는 2026-10-28 제거: 보스 스핀 +1 은 요구치 비례 없는 순수 보너스).
            var rc = FunTestHelpers.NewController(71001);
            rc.State.Stage = 5;
            var o = FunTestHelpers.SpinN(rc, FunTestHelpers.Pair0);
            t.Eq(201L, o.quota, "[R1] S5 보스 스테이지 quota (190×0.92×1.15=201.02→201, 웹 하니스 floor(quota(5)×quotaMul×1.15)와 동일)");
            t.Eq(6, o.spins, "[R1] S5 보스 스핀 수 6");

            // 게임 경유: S10(꼰대교수) 3매치 스핀은 같은 칸의 비보스(S9) 스핀 대비 ×1.15, 페어는 ×0.75 (동일 시드·고정 칸).
            long Exp(int stage, string[] ids)
            {
                var r = FunTestHelpers.NewController(71002);
                FunTestHelpers.Roomy(r.State, stage);
                r.State.SpinIndex = 1;   // 첫/막 스핀 보정(finals 용)과 무관한 중간 스핀
                return FunTestHelpers.SpinN(r, ids).gained;
            }
            long base3 = Exp(9, FunTestHelpers.Set3), boss3 = Exp(10, FunTestHelpers.Set3);
            t.Eq(Formulas.MulFloor(base3, 1.15), boss3, "[R1] S10 꼰대교수 3매치 실스핀: floor(비보스 gained×1.15)");
            long base2 = Exp(9, FunTestHelpers.Set2), boss2 = Exp(10, FunTestHelpers.Set2);
            t.Eq(Formulas.MulFloor(base2, 0.75), boss2, "[R1] S10 꼰대교수 페어 실스핀: floor(비보스 gained×0.75)");
            t.True(boss3 > base3 && boss2 < base2, "[R1] 방향성: 3매치는 보상(↑), 페어는 감점(↓)");
        }
    }

    // ── R2 요구치 곡선 ────────────────────────────────────────────────────────────────────────
    internal static class Tests_FunW1_R2_Quota
    {
        public static void Run(TestCtx t)
        {
            var normal = new long[] { 90, 105, 125, 145, 190, 200, 240, 285, 340, 400, 475, 560, 660, 780, 1050 };
            var deep = new long[] { 110, 120, 130, 140, 150, 170, 200, 235, 280, 330, 390, 460, 540, 640, 755 };
            for (int s = 1; s <= 15; s++)
            {
                t.Eq(normal[s - 1], Formulas.Quota(s), $"[R2] 일반 표 S{s}");
                t.Eq(deep[s - 1], Formulas.Quota(s, true), $"[R2] 심화 표 보존 S{s}");
                if (s > 1)
                {
                    t.True(normal[s - 1] > normal[s - 2], $"[R2] 일반 표 단조증가 S{s}");
                    t.True(deep[s - 1] > deep[s - 2], $"[R2] 심화 표 단조증가 S{s}");
                }
            }
            t.Eq(1312L, Formulas.Quota(16), "[R2] quota(16)=floor(1050×1.25)");
            t.Eq((long)(1050.0 * 1.25 * 1.25), Formulas.Quota(17), "[R2] quota(17)");
            t.Eq((long)(1050.0 * 1.25 * 1.25 * 1.25 * 1.25 * 1.25), Formulas.Quota(20), "[R2] quota(20)");
            t.Eq(1150L, (long)(920 * Formulas.QUOTA_GROWTH), "[R2] 설계표 값으로 계산해도 S16+ 는 ×1.25 (920→1150 공식)");
            t.Eq((long)(755.0 * 1.2), Formulas.Quota(16, true), "[R2] deep quota(16)=floor(755×1.2)");
            t.Eq(90L, Formulas.Quota(0), "[R2] quota(0) 방어");
            t.EqTol(1.25, Formulas.QUOTA_GROWTH, "[R2] QUOTA_GROWTH");
            t.EqTol(1.2, Formulas.QUOTA_GROWTH_DEEP, "[R2] QUOTA_GROWTH_DEEP");

            // 게임 경유 — 호출부 전부 deep 분기:
            //  ① 첫 스핀 outcome.quota ② REWARD_DONE nextPreview ③ QuotaOf 직접.
            var rcN = FunTestHelpers.NewController(71002);
            var oN = FunTestHelpers.SpinN(rcN, FunTestHelpers.Pair0);
            t.Eq(82L, oN.quota, "[R2] 일반 런 S1 quota = floor(90×0.92)");
            var rcD = FunTestHelpers.NewController(71003, deep: true);
            double penalty = DeepRunHooks.DeepPenalty(rcD.State);
            var oD = FunTestHelpers.SpinN(rcD);
            t.Eq((long)(110 * 0.92 * penalty), oD.quota, "[R2] 심화 런 S1 quota(표 110 기준, deep 분기)");

            var runN = FunTestHelpers.NewController(71004).State; runN.Stage = 6;
            t.Eq(184L, RewardDoneView.NextPreview(runN).quota, "[R2] 일반 nextPreview S6 quota = floor(200×0.92)");
            var runD = FunTestHelpers.NewController(71005, deep: true).State; runD.Stage = 6;
            t.Eq((long)(170 * 0.92 * DeepRunHooks.DeepPenalty(runD)), RewardDoneView.NextPreview(runD).quota, "[R2] 심화 nextPreview S6 quota(표 170, deep 분기)");

            // 아이템/장치 등 나머지 QuotaOf 호출부도 deep 을 넘기는지 — 심화 런의 grad_ring 즉시클리어 판정 기준(InstantQuota)과
            // PostSpin 벨 판정이 일반 표로 새지 않는다: QuotaOf(…, deep:true) 와 동일 값이어야 한다.
            var modsD = ModsBuilder.ApplyItemMods(ModsBuilder.Build(runD.MachineId, runD.CharId, runD.Perks, runD.Curses, runD.Device, levels: runD.PerkLevels), runD.PhaseItems);
            t.Eq(SpinResolver.QuotaOf(6, modsD, 0, false, DeepRunHooks.DeepPenalty(runD), true), RewardDoneView.NextPreview(runD).quota, "[R2] 심화 QuotaOf(deep:true) == nextPreview");
            t.True(SpinResolver.QuotaOf(6, modsD, 0, false, 1.0, true) < SpinResolver.QuotaOf(6, modsD, 0, false, 1.0, false), "[R2] 심화 S6(170) < 일반 S6(200)");
        }
    }

    // ── R3 홀드 ───────────────────────────────────────────────────────────────────────────────
    internal static class Tests_FunW1_R3_Hold
    {
        public static void Run(TestCtx t)
        {
            BasicFlow(t);
            ResetAndGuards(t);
            OracleReleasesHold(t);
            CellOpsOrderAndRange(t);
            FocusAddsCharge(t);
            DeepIsolation(t);
            ReplaceLastRawOnRerolls(t);
        }

        private static void BasicFlow(TestCtx t)
        {
            var rc = FunTestHelpers.NewController(72001);
            FunTestHelpers.Roomy(rc.State);
            t.Eq(1, rc.Hold.charges, "[R3] 초기 charges=1");
            t.Eq(0, rc.Hold.held.Count, "[R3] 초기 held 비어 있음");
            t.Eq(2, rc.Hold.max, "[R3] max=2");

            var ev0 = rc.ToggleHold(0);
            t.Eq("REJECTED", ev0[0].type, "[R3] 첫 스핀 전 홀드 불가");
            t.Eq("HOLD_NO_LAST_SPIN", ev0[0].reason, "[R3] 첫 스핀 전 사유");
            t.Eq(0, rc.Hold.held.Count, "[R3] 첫 스핀 전 held 비어 있음");

            var o1 = FunTestHelpers.SpinN(rc, "crown", "book", "star", "gem", "cherry");
            FunTestHelpers.SeqEq(t, new[] { "crown", "book", "star", "gem", "cherry" }, FunTestHelpers.RawIds(rc.State), "[R3] LastRaw 보관(1스핀)");
            t.Eq(0, o1.holdUsed.Count, "[R3] 홀드 없는 스핀 holdUsed 비어 있음");
            t.Eq(1, rc.Hold.charges, "[R3] 홀드 없는 스핀은 충전 유지");

            t.Eq("HOLD_CHANGED", rc.ToggleHold(0)[0].type, "[R3] 홀드 on 0");
            rc.ToggleHold(2);
            FunTestHelpers.SeqEq(t, new[] { 0, 2 }, rc.Hold.held, "[R3] 2칸 홀드");
            var ev3 = rc.ToggleHold(1);
            t.Eq("HOLD_MAX_REACHED", ev3[0].reason, "[R3] 3번째 칸은 상한(2칸)으로 거부");
            FunTestHelpers.SeqEq(t, new[] { 0, 2 }, rc.Hold.held, "[R3] 거부 후 held 불변");
            rc.ToggleHold(2);
            FunTestHelpers.SeqEq(t, new[] { 0 }, rc.Hold.held, "[R3] 홀드 해제 토글");
            rc.ToggleHold(2);
            FunTestHelpers.SeqEq(t, new[] { 0, 2 }, rc.Hold.held, "[R3] 재홀드(오름차순 유지)");
            t.Eq("HOLD_INDEX_OUT_OF_RANGE", rc.ToggleHold(9)[0].reason, "[R3] 범위 밖 인덱스(9) 거부");
            t.Eq("HOLD_INDEX_OUT_OF_RANGE", rc.ToggleHold(-1)[0].reason, "[R3] 범위 밖 인덱스(-1) 거부");
            FunTestHelpers.SeqEq(t, new[] { 0, 2 }, rc.Hold.held, "[R3] 범위 밖 인덱스는 held 를 바꾸지 않음");

            // 대조군: 같은 시드·같은 첫 스핀, 홀드 없음 → 두 번째 fresh 굴림의 raw.
            var ctl = FunTestHelpers.NewController(72001);
            FunTestHelpers.Roomy(ctl.State);
            FunTestHelpers.SpinN(ctl, "crown", "book", "star", "gem", "cherry");
            var oCtl = FunTestHelpers.SpinN(ctl);
            var ctlRaw = FunTestHelpers.RawIds(ctl.State);
            t.Eq(0, oCtl.holdUsed.Count, "[R3] 대조군 holdUsed 없음");
            t.Eq(1, ctl.Hold.charges, "[R3] 대조군 충전 유지");

            var o2 = FunTestHelpers.SpinN(rc);
            var rawA = FunTestHelpers.RawIds(rc.State);
            t.Eq("crown", rawA[0], "[R3] 홀드 칸 0 = 직전 raw 유지");
            t.Eq("star", rawA[2], "[R3] 홀드 칸 2 = 직전 raw 유지");
            t.Eq(ctlRaw[1], rawA[1], "[R3] 비홀드 칸 1 = 대조군(fresh 굴림) 값 — 홀드는 RNG 소비를 바꾸지 않는다");
            t.Eq(ctlRaw[3], rawA[3], "[R3] 비홀드 칸 3 = 대조군 값");
            t.Eq(ctlRaw[4], rawA[4], "[R3] 비홀드 칸 4 = 대조군 값");
            FunTestHelpers.SeqEq(t, new[] { 0, 2 }, o2.holdUsed, "[R3] holdUsed");
            t.Eq(0, rc.Hold.charges, "[R3] 스핀 후 충전 소모");
            t.Eq(0, rc.Hold.held.Count, "[R3] 스핀 후 홀드 표시 해제");
            FunTestHelpers.SeqEq(t, new[] { 0, 2 }, rc.LastSpin.holdUsed, "[R3] lastSpin.holdUsed");
            t.Eq("spin", rc.LastSpin.kind, "[R3] lastSpin.kind");

            var evNo = rc.ToggleHold(0);
            t.Eq("HOLD_NO_CHARGE", evNo[0].reason, "[R3] 충전 0이면 홀드 불가");
            t.Eq(0, rc.Hold.held.Count, "[R3] 충전 없음 → held 비어 있음");
        }

        private static void ResetAndGuards(TestCtx t)
        {
            // 스테이지 시작 리셋(누적 없음): charges=1·held 비움·LastRaw 비움·CmdUses 0.
            var rc = FunTestHelpers.NewController(72002);
            var run = rc.State;
            FunTestHelpers.Roomy(run);
            FunTestHelpers.SpinN(rc, "cherry", "cherry", "cherry", "cherry", "cherry");
            run.HoldCharges = 3; run.Held.Add(1); run.CmdUses["ALLIN"] = 2;
            HoldFlow.ResetForStage(run);
            t.Eq(1, run.HoldCharges, "[R3] ResetForStage → charges=1 (누적 없음)");
            t.Eq(0, run.Held.Count, "[R3] ResetForStage → held 비움");
            t.Eq(0, run.LastRaw.Count, "[R3] ResetForStage → LastRaw 비움(첫 스핀 전 홀드 불가)");
            t.Eq(0, run.CmdUses["ALLIN"], "[R3] ResetForStage → CmdUses 0");

            // 예언 확정(LockedNext) 중 홀드 불가.
            var rc2 = FunTestHelpers.NewController(72003);
            FunTestHelpers.Roomy(rc2.State);
            FunTestHelpers.SpinN(rc2, "cherry", "cherry", "cherry", "cherry", "cherry");
            FunTestHelpers.Force(rc2.State, "star", "star", "star", "star", "star");
            t.Eq("HOLD_LOCKED_NEXT", rc2.ToggleHold(1)[0].reason, "[R3] 예언 확정 중 홀드 불가");
            t.Eq(0, rc2.Hold.held.Count, "[R3] 예언 중 held 비어 있음");

            // SPIN 이 아닌 페이즈에서는 거부.
            var rc3 = FunTestHelpers.NewController(72008);
            FunTestHelpers.Roomy(rc3.State);
            FunTestHelpers.SpinN(rc3, "cherry", "cherry", "cherry", "cherry", "cherry");
            rc3.State.Phase = RunPhase.NodeSelect;
            t.Eq("PHASE_NOT_SPIN", rc3.ToggleHold(0)[0].reason, "[R3] 비 SPIN 페이즈 토글 거부");
            t.Eq(0, rc3.State.Held.Count, "[R3] 비 SPIN 페이즈 held 불변");
        }

        private static void OracleReleasesHold(TestCtx t)
        {
            // 예언 장치 oracle: 이미 잡은 홀드는 해제되고 충전은 보존된다.
            var rc = FunTestHelpers.NewController(72012, device: "dev_oracle");
            FunTestHelpers.Roomy(rc.State);
            FunTestHelpers.SpinN(rc, "cherry", "book", "star", "gem", "crown");
            rc.ToggleHold(1);
            FunTestHelpers.SeqEq(t, new[] { 1 }, rc.Hold.held, "[R3] oracle 전 홀드 on");
            var ev = rc.Do(new DeviceCmd("dev_oracle"));
            t.Eq("DEVICE_PEEK", ev[0].type, "[R3] oracle 발동");
            t.Eq(0, rc.Hold.held.Count, "[R3] oracle 시 홀드 해제");
            t.Eq(1, rc.Hold.charges, "[R3] oracle 시 충전 보존");
            t.Eq("HOLD_LOCKED_NEXT", rc.ToggleHold(1)[0].reason, "[R3] oracle 확정 후 홀드 불가");
        }

        private static void CellOpsOrderAndRange(TestCtx t)
        {
            // (d) 홀드 → cellOps 순서: 낮은 가치 칸(체리)을 홀드해 지우개가 그 칸을 지운다.
            var run = new RunState(72003) { CharId = "novice", MachineId = "basic", Phase = RunPhase.Spin };
            run.LastRaw.AddRange(SpinResolver.CellsFromIds(new[] { "cherry", "crown", "crown", "crown", "crown" }));
            run.Held.Add(0); run.HoldCharges = 1;
            var raw = SpinResolver.CellsFromIds(new[] { "crown", "crown", "crown", "crown", "crown" });
            var used = HoldFlow.Apply(run, raw);
            FunTestHelpers.SeqEq(t, new[] { 0 }, used, "[R3] 홀드 적용 칸");
            t.Eq("cherry", raw[0].sym.id, "[R3] 홀드 적용(직전 raw 의 체리 유지)");
            SpinResolver.ApplyCellOps(raw, new[] { "eraser_old" }, run.Rng);
            FunTestHelpers.SeqEq(t, new[] { "empty", "crown", "crown", "crown", "crown" }, raw.Select(c => c.sym.id), "[R3] 홀드 적용 후 cellOps(지우개가 홀드된 체리를 지움)");

            // (g) 보조릴 6칸 → 5칸: 범위 밖 홀드는 무시, 나머지만 적용·충전 소모.
            var run5 = new RunState(72006) { CharId = "novice", MachineId = "basic", Phase = RunPhase.Spin };
            run5.LastRaw.AddRange(SpinResolver.CellsFromIds(new[] { "crown", "book", "star", "gem", "cherry", "coin" }));
            run5.Held.Add(5); run5.Held.Add(1); run5.HoldCharges = 1;
            var rawX = SpinResolver.CellsFromIds(new[] { "cherry", "cherry", "cherry", "cherry", "cherry" });
            var usedX = HoldFlow.Apply(run5, rawX);
            FunTestHelpers.SeqEq(t, new[] { 1 }, usedX, "[R3] 범위 밖(5) 무시·나머지(1)만 적용");
            t.Eq("book", rawX[1].sym.id, "[R3] 적용 칸 값");
            t.Eq(0, run5.HoldCharges, "[R3] 충전 소모");

            var run6 = new RunState(72007) { CharId = "novice", MachineId = "basic", Phase = RunPhase.Spin };
            run6.LastRaw.AddRange(SpinResolver.CellsFromIds(new[] { "crown", "book", "star", "gem", "cherry", "coin" }));
            run6.Held.Add(5); run6.HoldCharges = 1;
            var used6 = HoldFlow.Apply(run6, SpinResolver.CellsFromIds(new[] { "cherry", "cherry", "cherry", "cherry", "cherry" }));
            t.Eq(0, used6.Count, "[R3] 전부 범위 밖이면 적용 칸 없음");
            t.Eq(1, run6.HoldCharges, "[R3] 전부 범위 밖이면 충전 소모 없음");

            // 충전 0 이면 held 가 있어도 적용하지 않는다.
            var run0 = new RunState(72009) { CharId = "novice", MachineId = "basic", Phase = RunPhase.Spin };
            run0.LastRaw.AddRange(SpinResolver.CellsFromIds(new[] { "crown", "book", "star", "gem", "cherry" }));
            run0.Held.Add(0); run0.HoldCharges = 0;
            t.Eq(0, HoldFlow.Apply(run0, SpinResolver.CellsFromIds(new[] { "cherry", "cherry", "cherry", "cherry", "cherry" })).Count, "[R3] 충전 0 이면 홀드 미적용");
        }

        private static void FocusAddsCharge(TestCtx t)
        {
            // (e) 집중 특수스핀: 홀드 충전 +1 (+ 첫 사용 무료)
            var rc = FunTestHelpers.NewController(72004);
            FunTestHelpers.Roomy(rc.State);
            long c0 = rc.State.Coins;
            var o = FunTestHelpers.SpinM(rc, SpinMode.Focus, FunTestHelpers.Pair0);
            t.Eq(2, rc.Hold.charges, "[R3] 집중 발동 홀드 충전 +1");
            t.True(o.notes.Any(n => n.Contains("홀드 +1")), "[R3] 집중 노트");
            t.Eq(c0 + o.result.coins, rc.State.Coins, "[R3] 집중 첫 사용 무료(코인 불변)");

            // (e2) 집중 스핀이 홀드도 같이 쓰면: 소모 -1 후 +1 = 유지
            var rc2 = FunTestHelpers.NewController(72014);
            FunTestHelpers.Roomy(rc2.State);
            FunTestHelpers.SpinN(rc2, FunTestHelpers.Pair0);
            rc2.ToggleHold(0);
            var o2 = FunTestHelpers.SpinM(rc2, SpinMode.Focus);
            t.Eq(1, o2.holdUsed.Count, "[R3] 홀드+집중: 홀드 실제 적용");
            t.Eq(1, rc2.Hold.charges, "[R3] 홀드+집중 동시: 충전 1-1+1");
        }

        private static void DeepIsolation(TestCtx t)
        {
            var rc = FunTestHelpers.NewController(72005, deep: true);
            t.True(rc.Hold == null, "[R3] 심화 Hold === null");
            t.True(rc.Fever == null, "[R3] 심화 Fever === null");
            t.True(rc.Encore == null, "[R3] 심화 Encore === null");
            var ev = rc.ToggleHold(0);
            t.Eq("REJECTED", ev[0].type, "[R3] 심화 toggleHold 거부");
            t.Eq("HOLD_DEEP_MODE", ev[0].reason, "[R3] 심화 toggleHold 사유");
            t.Eq(0, rc.State.Held.Count, "[R3] 심화 held 없음");
            t.Eq(0, rc.State.HoldCharges, "[R3] 심화 holdCharges 0");
            FunTestHelpers.SpinN(rc);
            t.Eq(0, rc.State.LastRaw.Count, "[R3] 심화는 LastRaw 스냅샷도 하지 않음");
        }

        private static void ReplaceLastRawOnRerolls(TestCtx t)
        {
            // 재굴림/조작이 만든 결과가 새 '직전 raw' 가 된다(웹 manip/_freeReroll 최소 보완).
            var rc = FunTestHelpers.NewController(72020, device: "dev_reroll");
            FunTestHelpers.Roomy(rc.State);
            rc.State.Coins = 50;
            FunTestHelpers.SpinN(rc, "cherry", "cherry", "cherry", "cherry", "cherry");
            var before = FunTestHelpers.RawIds(rc.State);
            FunTestHelpers.SeqEq(t, new[] { "cherry", "cherry", "cherry", "cherry", "cherry" }, before, "[R3-rr] 스핀 직후 LastRaw");
            rc.ToggleHold(0);
            var ev = rc.Do(new DeviceCmd("dev_reroll"));
            t.Eq("DEVICE_MANIP_RESULT", ev[0].type, "[R3-rr] 재굴림기 발동");
            // LastRaw 는 재굴림 결과(LastCellsFinal 은 Evaluate 이후 최종 칸 — 폭탄/자석이 없으면 raw 와 같다).
            var after = FunTestHelpers.RawIds(rc.State);
            t.Eq(5, after.Count, "[R3-rr] LastRaw 칸 수 유지");
            FunTestHelpers.SeqEq(t, new[] { 0 }, rc.Hold.held, "[R3-rr] 재굴림은 잡아둔 홀드 표시를 건드리지 않음");
            // 재굴림 결과가 raw 로 기록됐는지: LastCells(ids, MANIP 은 raw 그대로 기록)와 일치.
            FunTestHelpers.SeqEq(t, rc.State.LastCells, after, "[R3-rr] LastRaw == MANIP 이 기록한 raw(LastCells)");

            // 재시험(retake_form)도 동일.
            var rc2 = FunTestHelpers.NewController(72021);
            FunTestHelpers.Roomy(rc2.State);
            FunTestHelpers.SpinN(rc2, "cherry", "cherry", "cherry", "cherry", "cherry");
            rc2.State.Items.Add("retake_form");
            var ev2 = rc2.Do(new UseItem("retake_form"));
            t.Eq("ITEM_USED", ev2[0].type, "[R3-rr] 재시험 사용");
            FunTestHelpers.SeqEq(t, rc2.State.LastCells, FunTestHelpers.RawIds(rc2.State), "[R3-rr] 재시험 결과가 새 LastRaw");
        }
    }

    // ── R4 일반 모드 피버 ─────────────────────────────────────────────────────────────────────
    internal static class Tests_FunW1_R4_Fever
    {
        public static void Run(TestCtx t)
        {
            t.Eq(6, Formulas.FEVER_CHARGE.Length, "[R4] FEVER_CHARGE 길이");
            FunTestHelpers.SeqEq(t, new[] { 0, 0, 5, 15, 35, 60 }, Formulas.FEVER_CHARGE, "[R4] FEVER_CHARGE 상수");
            t.EqTol(1.5, Formulas.FEVER_EXP_MUL, "[R4] FEVER_EXP_MUL");
            t.Eq(100, Formulas.FEVER_MAX, "[R4] FEVER_MAX");
            t.Eq(3, Formulas.FEVER_SPINS, "[R4] FEVER_SPINS");

            ChargeTable(t);
            TriggerAndDuring(t);
            OrderWithSafeBossAllin(t);
            CarryAndClear(t);
            KeptOnRerolls(t);
            DeepIsolation(t);
        }

        // 웹 manip()/_freeReroll() 최소 보완: 피버가 적용된 스핀의 재굴림/조작/재시험은 피버 배율을 유지한다.
        // 대조군(피버 없음)과 시드·고정 칸이 같으면 피버는 RNG 를 소비하지 않으므로 재굴림 결과가 동일 →
        // 피버 쪽 gained == MulFloor(대조군 gained, 1.5) (피버는 MANIP 페널티 뒤 마지막 곱).
        private static void KeptOnRerolls(TestCtx t)
        {
            SpinOutcome Manip(bool fever, string charId, string device, Func<RunController, IReadOnlyList<RunEvent>> act, out RunController rcOut)
            {
                var rc = FunTestHelpers.NewController(73040, device: device, charId: charId);
                FunTestHelpers.Roomy(rc.State);
                rc.State.Coins = 50;
                rc.State.NFeverSpins = fever ? 1 : 0;
                var first = FunTestHelpers.SpinN(rc, FunTestHelpers.Set3);
                t.Eq(fever, first.feverApplied, $"[R4-keep] 사전조건: 첫 스핀 feverApplied={fever}");
                t.Eq(fever, rc.State.LastFeverApplied, $"[R4-keep] LastFeverApplied={fever}");
                var ev = act(rc);
                rcOut = rc;
                return ev[0].spin;
            }
            RunController r1, r2;
            // dev_reroll (MANIP)
            var mCtl = Manip(false, "novice", "dev_reroll", rc => rc.Do(new DeviceCmd("dev_reroll")), out r1);
            var mFev = Manip(true, "novice", "dev_reroll", rc => rc.Do(new DeviceCmd("dev_reroll")), out r2);
            t.True(mCtl != null && mFev != null && mCtl.result != null && mFev.result != null, "[R4-keep] dev_reroll 결과 존재");
            t.Eq(Formulas.MulFloor(mCtl.gained, 1.5), mFev.gained, "[R4-keep] dev_reroll: 피버 스핀의 조작은 피버 배율 유지");
            t.Eq(mFev.gained, r2.State.LastGain, "[R4-keep] dev_reroll: LastGain 에 반영");
            // 도박꾼 무료 재굴림
            var gCtl = Manip(false, "gambler", "", rc => rc.Do(new GamblerReroll()), out r1);
            var gFev = Manip(true, "gambler", "", rc => rc.Do(new GamblerReroll()), out r2);
            t.Eq(Formulas.MulFloor(gCtl.gained, 1.5), gFev.gained, "[R4-keep] 도박꾼 재굴림: 피버 배율 유지");
            // 재시험(retake_form)
            var rCtl = Manip(false, "novice", "", rc => { rc.State.Items.Add("retake_form"); return rc.Do(new UseItem("retake_form")); }, out r1);
            var rFev = Manip(true, "novice", "", rc => { rc.State.Items.Add("retake_form"); return rc.Do(new UseItem("retake_form")); }, out r2);
            t.Eq(Formulas.MulFloor(rCtl.gained, 1.5), rFev.gained, "[R4-keep] 재시험: 피버 배율 유지");
            t.Eq(rFev.gained, r2.State.LastGain, "[R4-keep] 재시험: LastGain 에 반영");
        }

        private static void ChargeTable(TestCtx t)
        {
            var rows = new Dictionary<int, string[]>
            {
                [0] = FunTestHelpers.Pair0, [2] = FunTestHelpers.Set2, [3] = FunTestHelpers.Set3, [4] = FunTestHelpers.Set4, [5] = FunTestHelpers.Set5,
            };
            foreach (var kv in rows)
            {
                var rc = FunTestHelpers.NewController(73000 + kv.Key);
                FunTestHelpers.Roomy(rc.State);
                var o = FunTestHelpers.SpinN(rc, kv.Value);
                int want = Formulas.FEVER_CHARGE[Math.Min(5, o.result.bestSetCount)];
                t.Eq(want, rc.Fever.gauge, $"[R4] 충전 best={o.result.bestSetCount}(롤 {kv.Key}): gauge");
                t.True(!o.feverTriggered, $"[R4] 충전 best={o.result.bestSetCount}: 피버 미발동");
                if (kv.Key >= 2) t.Eq(kv.Key, o.result.bestSetCount, $"[R4] 롤 {kv.Key}종 bestSetCount");
            }
            // 정확값(웹 harness 와 동일): 2→+5, 3→+15, 4→+35, 5→+60
            foreach (var (key, want) in new[] { (2, 5), (3, 15), (4, 35), (5, 60) })
            {
                var rc = FunTestHelpers.NewController(73100 + key);
                FunTestHelpers.Roomy(rc.State);
                FunTestHelpers.SpinN(rc, rows[key]);
                t.Eq(want, rc.State.NFeverGauge, $"[R4] {key}매치 게이지 +{want}");
            }
        }

        private static void TriggerAndDuring(TestCtx t)
        {
            // (b) 100 도달: 즉시 3스핀 세팅·게이지 0(넘침 버림)·이번 스핀엔 미적용
            var g = FunTestHelpers.NewController(73010);
            FunTestHelpers.Roomy(g.State);
            g.State.NFeverGauge = 90;   // +60 → 150 ≥ 100 → 넘친 50 버림
            var oT = FunTestHelpers.SpinN(g, FunTestHelpers.Set5);
            t.True(oT.feverTriggered, "[R4] 트리거 feverTriggered");
            t.True(!oT.feverApplied, "[R4] 트리거 스핀엔 feverApplied=false");
            t.Eq(0, g.Fever.gauge, "[R4] 트리거 후 gauge 0");
            t.Eq(3, g.Fever.spins, "[R4] 트리거 후 spins 3");
            t.True(oT.notes.Any(n => n == "🔥 피버 발동! 다음 3스핀 EXP ×1.5"), "[R4] 트리거 노트", string.Join("|", oT.notes));
            t.True(g.LastSpin.feverTriggered, "[R4] lastSpin.feverTriggered");

            // (c) 피버 중: 최종 EXP ×1.5(floor) · 게이지 불충전 · 스핀 차감 · 3스핀 후 종료 후 재충전
            var refRc = FunTestHelpers.NewController(73010);
            FunTestHelpers.Roomy(refRc.State);
            var refOut = FunTestHelpers.SpinN(refRc, FunTestHelpers.Set3);
            var fv = FunTestHelpers.NewController(73010);
            FunTestHelpers.Roomy(fv.State);
            fv.State.NFeverSpins = 3; fv.State.NFeverGauge = 40;
            var fOut = FunTestHelpers.SpinN(fv, FunTestHelpers.Set3);
            t.Eq(Formulas.MulFloor(refOut.gained, 1.5), fOut.gained, "[R4] 피버 스핀 gained = floor(기준×1.5)");
            t.True(fOut.feverApplied, "[R4] feverApplied");
            t.True(!fOut.feverTriggered, "[R4] feverTriggered(피버 중엔 false)");
            t.Eq(40, fv.Fever.gauge, "[R4] 피버 스핀: 게이지 불변(40)");
            t.Eq(2, fv.Fever.spins, "[R4] 피버 스핀: 잔여 2");
            t.True(fOut.notes.Any(n => n == "🔥 피버 ×1.5 (남은 2)"), "[R4] 노트 포맷(fmt2: ×1.5)", string.Join("|", fOut.notes));
            t.True(!fOut.notes.Any(n => n.Contains("×1.50")), "[R4] 노트 fmt2 위반(×1.50) 없음");
            t.True(fv.LastSpin.feverApplied, "[R4] lastSpin.feverApplied");
            FunTestHelpers.SpinN(fv, FunTestHelpers.Set3); FunTestHelpers.SpinN(fv, FunTestHelpers.Set3);
            t.Eq(40, fv.Fever.gauge, "[R4] 3피버 스핀 후 gauge 40");
            t.Eq(0, fv.Fever.spins, "[R4] 3피버 스핀 후 spins 0");
            FunTestHelpers.SpinN(fv, FunTestHelpers.Set3);   // 4번째 = 비피버 → 3매치 +15 충전
            t.Eq(55, fv.Fever.gauge, "[R4] 피버 종료 후 재충전");
        }

        private static void OrderWithSafeBossAllin(TestCtx t)
        {
            // (d) 최종 순서: dev_safe 하한 → 피버. 5해골 스핀의 기본 EXP 는 하한 미만이라 하한이 적용된 뒤 ×1.5.
            RunController MkSafe(bool fever)
            {
                var r = FunTestHelpers.NewController(73011, device: "dev_safe");
                FunTestHelpers.Roomy(r.State);
                r.State.NFeverSpins = fever ? 1 : 0;
                return r;
            }
            var safeRef = FunTestHelpers.SpinN(MkSafe(false), "skull", "skull", "skull", "skull", "skull");
            long floor = (long)(safeRef.quota / (double)safeRef.spins * 0.35);
            t.Eq(floor, safeRef.gained, "[R4] 대조군: dev_safe 하한 적용 gained");
            var safeFev = FunTestHelpers.SpinN(MkSafe(true), "skull", "skull", "skull", "skull", "skull");
            t.Eq(Formulas.MulFloor(floor, 1.5), safeFev.gained, "[R4] dev_safe 하한 뒤 피버 ×1.5");

            // (e) 보스 규칙 뒤 피버: 기말고사(S45) 막스핀 ×2 → 피버 ×1.5
            SpinOutcome MkBoss(bool fever)
            {
                var r = FunTestHelpers.NewController(73012);
                FunTestHelpers.Roomy(r.State, 45);
                r.State.SpinIndex = 55;   // spins = 5 + 50 + 1(보스) = 56 → 마지막 스핀
                r.State.NFeverSpins = fever ? 1 : 0;
                return FunTestHelpers.SpinN(r, FunTestHelpers.Set3);
            }
            var bA = MkBoss(false); var bB = MkBoss(true);
            t.Eq(56, bA.spins, "[R4] 사전조건: 보스 스핀 수 56 → idx55 = 막스핀");
            t.Eq(Formulas.MulFloor(bA.gained, 1.5), bB.gained, "[R4] 보스 막스핀×2 적용 뒤 피버: floor(A×1.5)");
            SpinOutcome noBoss;
            {
                var r = FunTestHelpers.NewController(73012);
                FunTestHelpers.Roomy(r.State, 44);   // 비보스 44: 막스핀 보정 없는 대조군
                r.State.SpinIndex = 54;
                noBoss = FunTestHelpers.SpinN(r, FunTestHelpers.Set3);
            }
            t.Eq(noBoss.gained * 2, bA.gained, "[R4] 보스 막스핀 ×2 확인(비보스 대비)");

            // (f) 특수스핀(올인) 뒤 피버
            SpinOutcome MkAll(bool fever)
            {
                var r = FunTestHelpers.NewController(73013);
                FunTestHelpers.Roomy(r.State);
                r.State.NFeverSpins = fever ? 1 : 0;
                return FunTestHelpers.SpinM(r, SpinMode.Allin, FunTestHelpers.Set3);
            }
            var aA = MkAll(false); var aB = MkAll(true);
            t.Eq(Formulas.MulFloor(aA.gained, 1.5), aB.gained, "[R4] 올인(×2.5) 뒤 피버 ×1.5");
        }

        private static void CarryAndClear(TestCtx t)
        {
            // (g) 스테이지 이월: 다음 스테이지 시작 리셋이 피버를 건드리지 않음
            var gc = FunTestHelpers.NewController(73014);
            gc.State.NFeverGauge = 70; gc.State.NFeverSpins = 2;
            HoldFlow.ResetForStage(gc.State);
            t.Eq(70, gc.Fever.gauge, "[R4] 스테이지 이월 gauge");
            t.Eq(2, gc.Fever.spins, "[R4] 스테이지 이월 spins");

            // (h) 실제 스테이지 클리어 경유 이월: 클리어 스핀 자체에도 피버 적용, 클리어 후 잔여 이월.
            var gx = FunTestHelpers.NewController(73015);
            gx.State.StageExp = 1_000_000;
            gx.State.NFeverSpins = 3;
            var ev = gx.Do(new Spin(SpinMode.N));   // 칸 고정 불필요 — 클리어는 StageExp 로 확정
            t.Eq("STAGE_CLEARED", ev[0].type, "[R4] 클리어 스핀");
            t.True(ev[0].spin.feverApplied, "[R4] 클리어 스핀 자체도 피버 적용");
            t.Eq(2, gx.Fever.spins, "[R4] 클리어 후에도 잔여 피버 이월(2)");
            t.Eq(RunPhase.NodeSelect, gx.State.Phase, "[R4] 클리어 후 NodeSelect(클리어 보드)");
        }

        private static void DeepIsolation(TestCtx t)
        {
            // (i) 심화 무영향: 신규 일반 시스템 필드는 값이 있어도 무시되고(feverApplied false) 기존 심화 피버와 별개.
            var gd = FunTestHelpers.NewController(73016, deep: true);
            FunTestHelpers.Roomy(gd.State);
            gd.State.NFeverSpins = 3; gd.State.NFeverGauge = 99;   // 심화에선 쓰이지 않는 필드 — 값이 있어도 무시돼야 함
            var dBase = FunTestHelpers.NewController(73016, deep: true);
            FunTestHelpers.Roomy(dBase.State);
            var dOut = FunTestHelpers.SpinN(gd);
            var dRef = FunTestHelpers.SpinN(dBase);
            t.True(!dOut.feverApplied, "[R4] 심화 feverApplied=false");
            t.True(!dOut.feverTriggered, "[R4] 심화 feverTriggered=false");
            t.Eq(dRef.gained, dOut.gained, "[R4] 심화 EXP 일반 피버 무영향");
            t.Eq(3, gd.State.NFeverSpins, "[R4] 심화 NFeverSpins 불변");
            t.Eq(99, gd.State.NFeverGauge, "[R4] 심화 NFeverGauge 불변");
            t.True(gd.Fever == null, "[R4] 심화 Fever null");
        }
    }

    // ── R5 앵콜(클리어 점수 더블업) ───────────────────────────────────────────────────────────
    internal static class Tests_FunW1_R5_Encore
    {
        private static IReadOnlyList<RunEvent> Enc(RunController rc, params string[] forced) => EncoreFlow.Spin(rc.State, forced.Length > 0 ? forced : null);

        public static void Run(TestCtx t)
        {
            IsBustTable(t);
            Ladder(t);
            Busts(t);
            AutoSettle(t);
            UnavailableCases(t);
            StatsAndSideSystems(t);
            ContextSnapshot(t);
            PhaseGatesAndReset(t);
            RealRollConsistency(t);
        }

        private static void IsBustTable(TestCtx t)
        {
            t.True(EncoreFlow.IsBust(0, 0), "[R5] bust(0세트,해골0)");
            t.True(EncoreFlow.IsBust(1, 0), "[R5] bust(1세트,해골0) — 페어 미만");
            t.True(!EncoreFlow.IsBust(2, 0), "[R5] 페어는 성공(경계)");
            t.True(!EncoreFlow.IsBust(2, 1), "[R5] 페어+해골1 성공");
            t.True(!EncoreFlow.IsBust(3, 1), "[R5] 해골 1개 3매치는 성공");
            t.True(EncoreFlow.IsBust(3, 2), "[R5] 해골 2개 3매치는 BUST");
            t.True(EncoreFlow.IsBust(5, 2), "[R5] 해골 2개 5매치도 BUST");
            t.True(EncoreFlow.IsBust(2, 3), "[R5] 해골 3개 BUST");
            t.True(EncoreFlow.IsBust(1, 1), "[R5] 페어 없음+해골1 BUST");
            t.EqTol(0.5, Formulas.ENCORE_STEP, "[R5] ENCORE_STEP");
            t.Eq(2, Formulas.ENCORE_BUST_SKULLS, "[R5] ENCORE_BUST_SKULLS");
            t.Eq(2, Formulas.ENCORE_MIN_SET, "[R5] ENCORE_MIN_SET");
            t.EqTol(1.5, EncoreFlow.Mul(1), "[R5] Mul(1)"); t.EqTol(2.0, EncoreFlow.Mul(2), "[R5] Mul(2)"); t.EqTol(2.5, EncoreFlow.Mul(3), "[R5] Mul(3)");
        }

        private static void Ladder(TestCtx t)
        {
            var rc = FunTestHelpers.Cleared(74001, left: 4);
            var run = rc.State;
            // 클리어 점수 = ClearOutcome.gainedScore. Cleared() 는 이벤트를 버리므로 stake 를 상태(뷰)에서 읽고, 아래에서
            // 정의대로(stage×50 + 초과EXP×2 + 남은스핀×100) 재계산해 대조한다.
            var ev0 = rc.Encore;
            long stake = ev0.stake;
            t.Eq(RunPhase.NodeSelect, run.Phase, "[R5] 클리어 → NodeSelect(앵콜 창)");
            t.True(ev0.available, "[R5] available");
            t.True(!ev0.active, "[R5] 초기 inactive");
            t.Eq(0, ev0.k, "[R5] 초기 k=0");
            t.Eq(0L, ev0.pot, "[R5] 초기 pot=0");
            t.True(!ev0.lastBust, "[R5] 초기 lastBust=false");
            t.EqTol(1.5, ev0.nextMul.Value, "[R5] 초기 nextMul=1.5");
            t.True(stake > 0, "[R5] stake > 0");
            t.Eq(4, EncoreFlow.Left(run), "[R5] 남은 스핀 4");
            // stake 구성: stage×50 + 초과EXP×2 + 남은스핀×100 (S1: 연승 0, 보스 아님). 초과EXP = 클리어 직후 EncoreStageExp - EncoreQuota.
            long leftover = run.EncoreStageExp - run.EncoreQuota;
            t.Eq(50L + leftover * 2 + 4 * 100, stake, "[R5] stake 구성: 50 + 초과EXP×2 + 남은스핀×100");
            long S0 = run.Score;
            long coinsB = run.Coins; long expB = run.StageExp; int idxB = run.SpinIndex;

            // 앵콜 1: 성공 → 판돈을 점수에서 빼 pot 으로 걸고 pot = floor(stake×1.5)
            var e1 = Enc(rc, "cherry", "cherry", "cherry", "cherry", "cherry")[0];
            var ls1 = rc.LastSpin; var er1 = ls1.encoreResult; long pot1 = Formulas.MulFloor(stake, 1.5);
            t.Eq("ENCORE_SPIN", e1.type, "[R5] 이벤트 타입");
            t.Eq("encore", ls1.kind, "[R5] lastSpin.kind");
            t.EqTol(1.5, er1.mulApplied, "[R5] 앵콜1 mulApplied");
            t.True(!er1.bust, "[R5] 앵콜1 bust=false");
            t.Eq(S0 - stake, run.Score, "[R5] 판돈을 점수에서 차감(S0 - stake)");
            t.Eq(pot1, rc.Encore.pot, "[R5] 앵콜1 pot = floor(stake×1.5)");
            t.Eq(pot1 - stake, er1.gained, "[R5] 앵콜1 gained = pot 증가분(pot-stake)");
            t.Eq(3, EncoreFlow.Left(run), "[R5] 남은 스핀 -1");
            t.Eq(expB, run.StageExp, "[R5] EXP 버림(다음 스테이지 StageExp 불변)");
            t.Eq(idxB, run.SpinIndex, "[R5] 다음 스테이지 SpinIndex 불변");
            t.Eq(0L, ls1.gained, "[R5] lastSpin.gained=0 (EXP 버림)");
            t.Eq(0, ls1.bigWinTier, "[R5] 앵콜 스핀 bigWinTier=0");
            t.Eq(RunPhase.NodeSelect, run.Phase, "[R5] 앵콜 스핀 phase 유지");
            t.Eq(coinsB + ls1.coins, run.Coins, "[R5] 앵콜 코인 정상 지급");
            var v1 = rc.Encore;
            t.True(v1.available && v1.active && v1.k == 1 && v1.pot == pot1 && v1.stake == stake && !v1.lastBust, "[R5] 앵콜1 후 encore 뷰");
            t.EqTol(2.0, v1.nextMul.Value, "[R5] 앵콜1 후 nextMul=2");

            // 앵콜 2·3: 사다리 1.5 → 2 → 2.5 (stake 대비, 점수는 이미 차감된 상태 유지)
            Enc(rc, "cherry", "cherry", "cherry", "book", "star");
            var er2 = rc.LastSpin.encoreResult;
            t.EqTol(2.0, er2.mulApplied, "[R5] 앵콜2 mulApplied");
            t.Eq(stake * 2, rc.Encore.pot, "[R5] 앵콜2 pot = stake×2");
            t.Eq(stake * 2 - pot1, er2.gained, "[R5] 앵콜2 gained");
            t.Eq(S0 - stake, run.Score, "[R5] 앵콜2 점수 불변");
            t.EqTol(2.5, rc.Encore.nextMul.Value, "[R5] 앵콜2 nextMul=2.5");
            t.Eq(2, rc.Encore.k, "[R5] 앵콜2 k=2");
            Enc(rc, "cherry", "cherry", "book", "star", "gem");
            long pot3 = Formulas.MulFloor(stake, 2.5);
            t.EqTol(2.5, rc.LastSpin.encoreResult.mulApplied, "[R5] 앵콜3 mulApplied=2.5");
            t.Eq(pot3, rc.Encore.pot, "[R5] 앵콜3 pot = floor(stake×2.5)");
            t.Eq(1, EncoreFlow.Left(run), "[R5] 남은 스핀 1");

            // 정산: pot → 점수, 이후 앵콜 불가, phase 는 NodeSelect 유지
            var cash = rc.EncoreCashOut();
            t.Eq("ENCORE_SETTLED", cash[0].type, "[R5] 정산 이벤트");
            t.Eq(pot3, cash[0].scoreDelta, "[R5] 정산 scoreDelta = pot");
            t.Eq(S0 - stake + pot3, run.Score, "[R5] 정산 가산: 점수 = S0 - stake + pot");
            var vc = rc.Encore;
            t.True(!vc.available && !vc.active && vc.k == 3 && vc.pot == 0 && vc.stake == stake && !vc.lastBust && vc.nextMul == null, "[R5] 정산 후 encore 뷰");
            t.Eq(RunPhase.NodeSelect, run.Phase, "[R5] 정산 후 phase NodeSelect 유지");
            long scoreAfter = run.Score;
            var again = rc.EncoreSpin();
            t.Eq("ENCORE_ALREADY_DONE", again[0].reason, "[R5] 정산 후 encoreSpin 불가");
            t.Eq(scoreAfter, run.Score, "[R5] 정산 후 encoreSpin 점수 불변");
            t.Eq("ENCORE_NOT_ACTIVE", rc.EncoreCashOut()[0].reason, "[R5] 이중 정산 방지(진행 중인 앵콜 없음)");
            t.Eq(scoreAfter, run.Score, "[R5] 이중 정산 방지: 점수 불변");
            var nodeEv = rc.Do(new ChooseNode(0));
            t.True(nodeEv[0].type != "ENCORE_SETTLED", "[R5] 정산 완료 후 노드 선택은 추가 정산 이벤트 없음");
            t.True(run.Phase != RunPhase.NodeSelect, "[R5] 정산 뒤 노드 선택 → NodeSelect 이탈");
            t.Eq(scoreAfter, run.Score, "[R5] 노드 이동 시 점수 불변");

            // 노드 선택이 진행 중인 앵콜을 먼저 정산한다 (ENCORE_SETTLED 가 결과 맨 앞).
            var rn = FunTestHelpers.Cleared(74007, left: 4);
            long Sn = rn.State.Score; long stn = rn.Encore.stake;
            Enc(rn, "cherry", "cherry", "cherry", "book", "star");
            long potN = rn.Encore.pot;
            var nn = rn.Do(new ChooseNode(0));
            t.Eq("ENCORE_SETTLED", nn[0].type, "[R5] ChooseNode 가 진행 중 앵콜을 정산(이벤트 선두)");
            t.Eq(potN, nn[0].scoreDelta, "[R5] 정산 scoreDelta");
            t.Eq(Sn - stn + potN, rn.State.Score, "[R5] ChooseNode 정산 점수");
            t.True(nn.Count >= 2 && nn[1].type != "ENCORE_SETTLED", "[R5] 정산 이벤트 뒤에 노드 결과 이벤트");
            t.True(rn.State.Phase != RunPhase.NodeSelect, "[R5] 정산 후 노드 선택 진행");
            t.True(rn.State.EncoreDone && !rn.State.EncoreActive, "[R5] 노드 선택 후 앵콜 종료 상태");

            // 앵콜 미사용 진행: 점수 불변·정산 이벤트 없음
            var rm = FunTestHelpers.Cleared(74008, left: 4);
            long Sm = rm.State.Score;
            var mv = rm.Do(new ChooseNode(0));
            t.True(mv[0].type != "ENCORE_SETTLED", "[R5] 앵콜 안 하고 진행: 정산 이벤트 없음");
            t.Eq(Sm, rm.State.Score, "[R5] 앵콜 안 하고 진행: 점수 불변");

            // 잘못된 노드 인덱스(거부)는 진행 중인 앵콜을 정산하지 않는다(거부=상태 불변).
            var rj = FunTestHelpers.Cleared(74030, left: 4);
            Enc(rj, "cherry", "cherry", "cherry", "book", "star");
            long potJ = rj.Encore.pot; long scoreJ = rj.State.Score;
            var rej = rj.Do(new ChooseNode(99));
            t.Eq("REJECTED", rej[0].type, "[R5] 잘못된 인덱스 노드 선택 거부");
            t.Eq(scoreJ, rj.State.Score, "[R5] 거부 시 앵콜 정산 없음(점수 불변)");
            t.True(rj.Encore.active && rj.Encore.pot == potJ, "[R5] 거부 시 앵콜 진행 상태 유지");
        }

        private static void Busts(TestCtx t)
        {
            // BUST ① 첫 스핀에서 페어 없음 → 판돈(=pot) 소멸·앵콜 종료
            var rc = FunTestHelpers.Cleared(74002);
            long S0 = rc.State.Score; long stake = rc.Encore.stake;
            Enc(rc, "cherry", "book", "star", "gem", "crown");
            var er = rc.LastSpin.encoreResult;
            t.True(er.bust, "[R5] BUST① bust");
            t.Eq(0L, er.gained, "[R5] BUST① gained=0");
            t.EqTol(1.5, er.mulApplied, "[R5] BUST① mulApplied(시도한 배율)=1.5");
            t.Eq(stake, er.lostPot, "[R5] BUST① 소멸 팟 = 판돈");
            t.Eq(0L, rc.Encore.pot, "[R5] BUST① pot=0");
            t.Eq(S0 - stake, rc.State.Score, "[R5] BUST① 판돈 소멸(점수 = S0 - stake)");
            var vb = rc.Encore;
            t.True(!vb.available && !vb.active && vb.k == 0 && vb.pot == 0 && vb.stake == stake && vb.lastBust && vb.nextMul == null, "[R5] BUST① 후 encore 뷰");
            t.Eq("ENCORE_ALREADY_DONE", rc.EncoreSpin()[0].reason, "[R5] BUST 후 앵콜 불가");
            t.Eq("ENCORE_NOT_ACTIVE", rc.EncoreCashOut()[0].reason, "[R5] BUST 후 정산할 팟 없음");
            var node = rc.Do(new ChooseNode(0));
            t.True(node[0].type != "ENCORE_SETTLED", "[R5] BUST 후 노드 선택: 정산 이벤트 없음");
            t.Eq(S0 - stake, rc.State.Score, "[R5] BUST 후 노드 선택 점수 불변");

            // BUST ② 성공 1회 뒤 BUST → 쌓인 pot 전부 소멸 (k 는 성공 횟수라 1 유지)
            var rc2 = FunTestHelpers.Cleared(74012);
            long S2 = rc2.State.Score; long st2 = rc2.Encore.stake;
            Enc(rc2, "cherry", "cherry", "cherry", "book", "star");
            long potB = rc2.Encore.pot;
            t.Eq(Formulas.MulFloor(st2, 1.5), potB, "[R5] BUST② 전 pot = floor(stake×1.5)");
            Enc(rc2, "cherry", "book", "star", "gem", "crown");
            var er2 = rc2.LastSpin.encoreResult;
            t.True(er2.bust, "[R5] BUST② bust");
            t.EqTol(2.0, er2.mulApplied, "[R5] BUST② 시도 배율 2");
            t.Eq(potB, er2.lostPot, "[R5] BUST② 소멸 팟 기록");
            t.Eq(S2 - st2, rc2.State.Score, "[R5] BUST② 점수 = S0 - stake (정산 없음)");
            var v2 = rc2.Encore;
            t.True(!v2.available && !v2.active && v2.k == 1 && v2.pot == 0 && v2.stake == st2 && v2.lastBust && v2.nextMul == null, "[R5] BUST② 후 encore 뷰");

            // BUST: 해골 2개↑ (3매치여도) / 해골 1개 + 3매치는 성공 / 페어는 성공 경계
            var rk = FunTestHelpers.Cleared(74003);
            long Sk = rk.State.Score; long stk = rk.Encore.stake;
            Enc(rk, "skull", "skull", "cherry", "cherry", "cherry");
            t.True(rk.LastSpin.encoreResult.bust, "[R5] BUST(해골 2개, 3매치) bust");
            t.Eq(Sk - stk, rk.State.Score, "[R5] BUST(해골2) 판돈 소멸");
            var rk1 = FunTestHelpers.Cleared(74004);
            Enc(rk1, "skull", "cherry", "cherry", "cherry", "book");
            t.True(!rk1.LastSpin.encoreResult.bust, "[R5] 해골 1개 3매치는 성공");
            var rp = FunTestHelpers.Cleared(74005);
            Enc(rp, "cherry", "cherry", "book", "star", "gem");
            t.True(!rp.LastSpin.encoreResult.bust, "[R5] 페어는 성공(경계)");
        }

        private static void AutoSettle(TestCtx t)
        {
            // 남은 스핀 1 → 첫 성공으로 남은 0 → 자동 정산
            var ra = FunTestHelpers.Cleared(74006, left: 1);
            long Sa = ra.State.Score; long sta = ra.Encore.stake;
            Enc(ra, "cherry", "cherry", "cherry", "book", "star");
            var era = ra.LastSpin.encoreResult; long potA = Formulas.MulFloor(sta, 1.5);
            t.True(era.settled, "[R5] 남은 0 → 자동 정산 settled");
            t.Eq(potA, era.settledPot, "[R5] 자동 정산 settledPot");
            t.Eq(Sa - sta + potA, ra.State.Score, "[R5] 자동 정산 점수 = S0 - stake + pot");
            var va = ra.Encore;
            t.True(!va.available && !va.active && va.k == 1 && va.pot == 0 && va.stake == sta && !va.lastBust && va.nextMul == null, "[R5] 자동 정산 후 encore 뷰");

            // 남은 2 → 성공 1(진행 중) → 성공 2(자동 정산)
            var ra2 = FunTestHelpers.Cleared(74016, left: 2);
            long Sa2 = ra2.State.Score; long sta2 = ra2.Encore.stake;
            Enc(ra2, "cherry", "cherry", "cherry", "book", "star");
            t.True(ra2.Encore.active, "[R5] 남은 2: 1회 후 진행 중(active)");
            Enc(ra2, "cherry", "cherry", "cherry", "book", "star");
            t.True(ra2.LastSpin.encoreResult.settled, "[R5] 남은 2: 2회 후 자동 정산");
            t.Eq(Sa2 - sta2 + sta2 * 2, ra2.State.Score, "[R5] 남은 2: 정산 점수 = S0 - stake + stake×2");

            // 남은 1 에서 BUST: 정산 없음·판돈 소멸·settled=false
            var rb = FunTestHelpers.Cleared(74026, left: 1);
            long Sb = rb.State.Score; long stb = rb.Encore.stake;
            Enc(rb, "cherry", "book", "star", "gem", "crown");
            t.True(rb.LastSpin.encoreResult.bust && !rb.LastSpin.encoreResult.settled, "[R5] 남은 1 BUST: settled=false");
            t.Eq(Sb - stb, rb.State.Score, "[R5] 남은 1 BUST: 판돈 소멸");
        }

        private static void UnavailableCases(TestCtx t)
        {
            // ① 남은 스핀 0
            var g0 = FunTestHelpers.Cleared(74009, left: 0);
            t.True(g0.Encore.stake > 0, "[R5] 남은 스핀 0 인데 stake > 0");
            t.True(!g0.Encore.available, "[R5] 남은 스핀 0 → available=false");
            long S00 = g0.State.Score;
            t.Eq("ENCORE_NO_SPINS_LEFT", g0.EncoreSpin()[0].reason, "[R5] 남은 스핀 0: encoreSpin 거부");
            t.Eq(S00, g0.State.Score, "[R5] 남은 스핀 0: 점수 불변");

            // ② 클리어 점수 0(빚문서)
            var gd = FunTestHelpers.Cleared(74017, left: 4, debt: true);
            t.Eq(0L, gd.Encore.stake, "[R5] 빚문서(클리어 점수 0) → stake=0");
            t.True(!gd.Encore.available, "[R5] stake=0 → available=false");
            long Sd = gd.State.Score; int leftBefore = EncoreFlow.Left(gd.State);
            t.Eq("ENCORE_NO_STAKE", gd.EncoreSpin()[0].reason, "[R5] stake=0: encoreSpin 거부");
            t.Eq(Sd, gd.State.Score, "[R5] stake=0: 점수 불변");
            t.Eq(leftBefore, EncoreFlow.Left(gd.State), "[R5] stake=0: 남은 스핀 소모 없음");

            // ③ 점수 < 판돈
            var gf = FunTestHelpers.Cleared(74010);
            long stf = gf.Encore.stake; gf.State.Score = stf - 1;
            t.True(!gf.Encore.available, "[R5] 점수<판돈 → available=false");
            t.Eq("ENCORE_STAKE_SHORT", gf.EncoreSpin()[0].reason, "[R5] 점수<판돈: encoreSpin 거부");
            t.Eq(stf - 1, gf.State.Score, "[R5] 점수<판돈: 점수 불변");

            // 진행 중에는 점수가 판돈보다 적어도(이미 차감됨) 계속 가능 / 점수 == 판돈이면 시작 가능(차감 후 0)
            var gz = FunTestHelpers.Cleared(74018);
            long stz = gz.Encore.stake; gz.State.Score = stz;
            Enc(gz, "cherry", "cherry", "cherry", "book", "star");
            t.Eq(0L, gz.State.Score, "[R5] 점수=판돈이면 시작 가능(차감 후 0)");
            t.True(gz.Encore.available, "[R5] 진행 중엔 점수<판돈이어도 available");
            Enc(gz, "cherry", "cherry", "cherry", "book", "star");
            t.Eq(2, gz.Encore.k, "[R5] 진행 중 2번째 앵콜 성공");

            // ④ 보스 클리어 점수(보스 +500·연승 보너스)도 판돈에 포함
            var gb = FunTestHelpers.Cleared(74019, left: 3, stage: 5);
            t.True(gb.Encore.stake > 0, "[R5] 보스 stake > 0");
            long sBoss = 500;
            long leftover = gb.State.EncoreStageExp - gb.State.EncoreQuota;
            t.Eq(5 * 50L + leftover * 2 + 3 * 100 + sBoss + Formulas.StreakBonus(5), gb.Encore.stake, "[R5] 보스 판돈 = 합계(250 + 초과×2 + 남은스핀×100 + 보스500 + 연승100)");

            // ⑤ 심화: encore null, encoreSpin/CashOut 거부
            var rd = FunTestHelpers.NewController(74015, deep: true);
            rd.State.Phase = RunPhase.NodeSelect; rd.State.Score = 5000; rd.State.EncoreStake = 300; rd.State.EncoreDone = false;
            t.True(rd.Encore == null, "[R5] 심화 encore null");
            t.Eq("ENCORE_DEEP_MODE", rd.EncoreSpin()[0].reason, "[R5] 심화 encoreSpin 거부");
            t.Eq("ENCORE_DEEP_MODE", rd.EncoreCashOut()[0].reason, "[R5] 심화 encoreCashOut 거부");
            t.Eq(5000L, rd.State.Score, "[R5] 심화 점수 불변");
            // 심화 런이 클리어해도 앵콜 창은 열리지 않는다.
            var rdc = FunTestHelpers.NewController(74115, deep: true);
            rdc.State.StageExp = 1_000_000;
            rdc.Do(new Spin(SpinMode.N));
            t.True(rdc.State.EncoreDone && rdc.State.EncoreStake == 0, "[R5] 심화 클리어: 앵콜 창 닫힘·stake 0");
        }

        private static Dictionary<string, long> Snap(PlayerProfile p) => new Dictionary<string, long>(p.Stats);

        private static void StatsAndSideSystems(TestCtx t)
        {
            // 통계 비오염: 앵콜 스핀 이벤트는 StatTracker 를 거쳐도 프로필 카운터를 건드리지 않고,
            // 런의 해골/심볼/불운게이지/최고기록도 불변이다 (업적 인플레이션 방지).
            var rc = FunTestHelpers.Cleared(74011);
            var run = rc.State;
            var profile = new PlayerProfile();
            var scratch = new StatTracker.RunScratch();
            var before = Snap(profile);
            var symBefore = new Dictionary<string, int>(run.RunSymCounts);
            int unl = run.UnluckyGauge; int jp = run.RunJackpots; long best = run.RunBestSpin;
            var e1 = Enc(rc, "crown", "crown", "crown", "crown", "crown");
            StatTracker.Apply(profile, run, e1, scratch);
            var e2 = Enc(rc, "skull", "skull", "skull", "crown", "crown");
            StatTracker.Apply(profile, run, e2, scratch);
            FunTestHelpers.SeqEq(t, before.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value),
                Snap(profile).OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value), "[R5] 앵콜 스핀은 프로필 통계를 건드리지 않음");
            FunTestHelpers.SeqEq(t, symBefore.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value),
                run.RunSymCounts.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value), "[R5] 앵콜 스핀은 RunSymCounts 를 건드리지 않음");
            t.Eq(unl, run.UnluckyGauge, "[R5] 앵콜 스핀은 불운 게이지를 건드리지 않음");
            t.Eq(jp, run.RunJackpots, "[R5] 앵콜 스핀은 잭팟 카운터를 건드리지 않음(5크라운이어도)");
            t.Eq(best, run.RunBestSpin, "[R5] 앵콜 스핀은 최고 스핀 기록을 건드리지 않음");

            // 앵콜 중 다른 행동 불가: spin/holds 는 NodeSelect 에서 무효
            var gz = FunTestHelpers.Cleared(74013);
            int ixB = gz.State.SpinIndex;
            var sp = gz.Do(new Spin(SpinMode.N));
            t.Eq("REJECTED", sp[0].type, "[R5] NodeSelect 에서 spin 무효");
            t.Eq(ixB, gz.State.SpinIndex, "[R5] NodeSelect 에서 spin 후 SpinIndex 불변");
            t.Eq("PHASE_NOT_SPIN", gz.ToggleHold(0)[0].reason, "[R5] NodeSelect 에서 홀드 무효");

            // 피버 충전/소모 없음 + 홀드 충전 불변
            gz.State.NFeverSpins = 2; gz.State.NFeverGauge = 40; int hc = gz.State.HoldCharges;
            Enc(gz, "cherry", "cherry", "cherry", "cherry", "cherry");
            t.Eq(40, gz.Fever.gauge, "[R5] 앵콜은 피버 gauge 불변");
            t.Eq(2, gz.Fever.spins, "[R5] 앵콜은 피버 spins 불변");
            t.Eq(hc, gz.State.HoldCharges, "[R5] 앵콜은 홀드 충전 불변");
        }

        private static void ContextSnapshot(TestCtx t)
        {
            // 클리어 시점 컨텍스트 스냅샷: 앵콜 스핀 mods 는 클리어한 스테이지 기준이다.
            var rc = FunTestHelpers.Cleared(74040, left: 3, phaseItem: true);
            var run = rc.State;
            t.Eq(1, run.EncoreStage, "[R5] 스냅샷 EncoreStage = 클리어한 스테이지");
            t.Eq(2, run.Stage, "[R5] 진행 상태는 이미 다음 스테이지");
            t.Eq(0, run.PhaseItems.Count, "[R5] PhaseItems 는 클리어 시 리셋");
            t.Eq(1, run.EncorePhaseItems.Count, "[R5] 클리어 스테이지 PhaseItems 스냅샷 보존");
            t.Eq(82L, run.EncoreQuota, "[R5] 스냅샷 EncoreQuota = 클리어 스테이지 요구치(82)");
            t.Eq(5, run.EncoreSpinsTotal, "[R5] 스냅샷 총 스핀 5");
            t.Eq(2, run.EncoreSpinsUsed, "[R5] 스냅샷 사용 스핀 = 총5 - 남은3 = 2");
            t.True(!run.EncoreBoss, "[R5] 스냅샷 EncoreBoss=false(S1)");
            var gb = FunTestHelpers.Cleared(74041, left: 3, stage: 5);
            t.True(gb.State.EncoreBoss, "[R5] 보스 스테이지 스냅샷 EncoreBoss=true");
            t.Eq(5, gb.State.EncoreStage, "[R5] 보스 스냅샷 EncoreStage=5");
            t.Eq(6, gb.State.EncoreSpinsTotal, "[R5] 보스 스냅샷 총 스핀 6");

            // A8 승천: 앵콜 스핀은 "방금 클리어한" 스테이지의 금지 심볼로 굴린다(다음 스테이지 재롤 값이 아님).
            int checkedSpins = 0;
            for (long seed = 1; seed <= 60; seed++)
            {
                var rcA = FunTestHelpers.NewController(75000 + seed, asc: 8);
                var runA = rcA.State;
                string bannedBefore = runA.BannedSym;
                int total = SpinResolver.EffSpins(runA, ModsBuilder.Build("basic", "novice", new List<string>(), new List<string>(), ""));
                runA.SpinIndex = total - 5; runA.StageExp = 100_000;
                FunTestHelpers.Force(runA, FunTestHelpers.Pair0);
                rcA.Do(new Spin(SpinMode.N));
                if (runA.Phase != RunPhase.NodeSelect) continue;
                t.Eq(bannedBefore, runA.EncoreBannedSym, $"[R5-asc8 seed={seed}] 금지 심볼 스냅샷 = 클리어 스테이지 값");
                if (!rcA.Encore.available) continue;
                var ev = rcA.EncoreSpin();
                if (ev[0].type != "ENCORE_SPIN") continue;
                checkedSpins++;
                bool clean = ev[0].spin.result.rawCells.All(c => c.sym.id != bannedBefore);
                t.True(clean, $"[R5-asc8 seed={seed}] 앵콜 굴림에 금지 심볼({bannedBefore}) 없음");
            }
            t.True(checkedSpins > 30, "[R5-asc8] 금지 심볼 검증 표본 충분", $"checked={checkedSpins}");
        }

        private static void PhaseGatesAndReset(TestCtx t)
        {
            // SPIN 페이즈에서는 앵콜 불가
            var rc = FunTestHelpers.NewController(74050);
            t.Eq("PHASE_NOT_NODE_SELECT", rc.EncoreSpin()[0].reason, "[R5] SPIN 페이즈 encoreSpin 거부");
            t.Eq("PHASE_NOT_NODE_SELECT", rc.EncoreCashOut()[0].reason, "[R5] SPIN 페이즈 encoreCashOut 거부");

            // 다음 스테이지 시작 리셋: 앵콜 상태 초기화
            var gr = FunTestHelpers.Cleared(74014);
            Enc(gr, "cherry", "cherry", "cherry", "book", "star");
            gr.Do(new ChooseNode(0));
            HoldFlow.ResetForStage(gr.State);
            var s = gr.State;
            FunTestHelpers.SeqEq(t, new object[] { 0, 0L, 0L, false, true, false },
                new object[] { s.EncoreK, s.EncorePot, s.EncoreStake, s.EncoreActive, s.EncoreDone, s.EncoreLastBust }, "[R5] ResetForStage 앵콜 초기화");
        }

        // 실제 굴림 경로(RunController.EncoreSpin) 일관성 — 시드 200개: BUST 판정·팟 산식이 결과와 일치하고, 성공률이 합리적 범위.
        private static void RealRollConsistency(TestCtx t)
        {
            int tries = 0, busts = 0, bad = 0;
            for (long seed = 1; seed <= 200; seed++)
            {
                var rc = FunTestHelpers.Cleared(76000 + seed, left: 4);
                if (!rc.Encore.available) continue;
                long S0 = rc.State.Score; long stake = rc.Encore.stake;
                var ev = rc.EncoreSpin();
                if (ev[0].type != "ENCORE_SPIN") { bad++; continue; }
                tries++;
                var er = ev[0].spin.encoreResult; var res = ev[0].spin.result;
                bool wantBust = res.bestSetCount < 2 || res.skulls >= 2;
                if (er.bust != wantBust) bad++;
                if (er.bust) { busts++; if (rc.State.Score != S0 - stake || rc.Encore.pot != 0) bad++; }
                else if (rc.Encore.pot != Formulas.MulFloor(stake, 1.5) || rc.State.Score != S0 - stake) bad++;
            }
            t.Eq(0, bad, "[R5-real] 실제 굴림 200시드: BUST 판정/팟/점수 산식 불일치 0건");
            t.True(tries >= 190, "[R5-real] 표본 수", $"tries={tries}");
            double bustRate = tries > 0 ? busts / (double)tries : 1;
            t.True(bustRate > 0.10 && bustRate < 0.60, "[R5-real] 스핀당 BUST 비율이 합리적 범위(설계 시뮬 27.8%)", $"bustRate={bustRate:F3}");
        }
    }

    // ── R6 특수스핀 재설계 ────────────────────────────────────────────────────────────────────
    internal static class Tests_FunW1_R6_SpecialSpins
    {
        public static void Run(TestCtx t)
        {
            t.EqTol(2.5, Formulas.ALLIN_MUL, "[R6] ALLIN_MUL");
            t.Eq(1, Formulas.ALLIN_FAIL_SKULLS, "[R6] ALLIN_FAIL_SKULLS");
            t.EqTol(0.8, Formulas.FOCUS_FLOOR, "[R6] FOCUS_FLOOR");
            t.Eq(1, Formulas.CMD_COST_STEP, "[R6] CMD_COST_STEP");
            t.EqTol(0.85, Devices.ById("dev_subreel").fx["expMul"], "[R6] SUBREEL_EXP_MUL(dev_subreel.fx.expMul)");

            Allin(t);
            Focus(t);
            ProgressiveCost(t);
            Subreel(t);
            NoRareWeightPenalty(t);
        }

        private static void Allin(TestCtx t)
        {
            SpinOutcome Run(string[] ids, SpinMode mode)
            {
                var x = FunTestHelpers.NewController(75001);
                FunTestHelpers.Roomy(x.State);
                return FunTestHelpers.SpinM(x, mode, ids);
            }
            var nBase = Run(FunTestHelpers.Set4, SpinMode.N);
            var nAll = Run(FunTestHelpers.Set4, SpinMode.Allin);
            t.Eq(Formulas.MulFloor(nBase.gained, 2.5), nAll.gained, "[R6] 올인 성공 gained = floor(기준×2.5)");
            var fail = Run(new[] { "cherry", "cherry", "cherry", "cherry", "skull" }, SpinMode.Allin);
            t.Eq(0L, fail.gained, "[R6] 올인 해골1개 실패 gained=0");
            t.True(fail.notes.Any(n => n.Contains("해골 1개↑")), "[R6] 올인 해골1개 실패 노트");
            t.True(nAll.notes.Any(n => n == "🎲올인 성공! EXP ×2.5"), "[R6] 올인 성공 노트(fmt2)", string.Join("|", nAll.notes));
            t.Eq(0L, Run(new[] { "skull", "skull", "cherry", "cherry", "cherry" }, SpinMode.Allin).gained, "[R6] 올인 해골2개 실패");
        }

        private static void Focus(TestCtx t)
        {
            // 하한: floor(per×0.8) — 5해골 스핀(기본 EXP 는 하한 미만)에서 하한이 그대로 gained 가 된다.
            var gf = FunTestHelpers.NewController(75002);
            FunTestHelpers.Roomy(gf.State);
            var fo = FunTestHelpers.SpinM(gf, SpinMode.Focus, "skull", "skull", "skull", "skull", "skull");
            long want = (long)Math.Floor(fo.quota / (double)fo.spins * 0.8 + 1e-9);
            t.Eq(want, fo.gained, "[R6] 집중 하한: floor(quota/spins×0.8)");
            long oldFloor = (long)(fo.quota / (double)fo.spins * 0.6);
            t.True(fo.gained != oldFloor, "[R6] 구 하한(×0.6)과 다름");
            t.True(fo.notes.Contains("🎯집중 — 최소 보장"), "[R6] 집중 최소보장 노트");

            // 하한 초과분은 유지: 5체리(고EXP) 스핀은 하한보다 크므로 기본 EXP 그대로 — N 모드와 동일.
            var a = FunTestHelpers.NewController(75012); FunTestHelpers.Roomy(a.State);
            var b = FunTestHelpers.NewController(75012); FunTestHelpers.Roomy(b.State);
            // 대형 요구치에서는 하한이 매우 커서 항상 하한이 걸린다 → 낮은 요구치(스테이지 1)에서 비교.
            var a1 = FunTestHelpers.NewController(75013);
            var b1 = FunTestHelpers.NewController(75013);
            a1.State.SpinIndex = 1; b1.State.SpinIndex = 1;
            a1.State.StageExp = 0; b1.State.StageExp = 0;
            var oa = FunTestHelpers.SpinM(a1, SpinMode.N, "star", "star", "book", "gem", "cherry");
            var ob = FunTestHelpers.SpinM(b1, SpinMode.Focus, "star", "star", "book", "gem", "cherry");
            long floor1 = (long)Math.Floor(ob.quota / (double)ob.spins * 0.8 + 1e-9);
            t.Eq(Math.Max(oa.gained, floor1), ob.gained, "[R6] 집중: gained = max(기본 EXP, 하한)");
        }

        private static void ProgressiveCost(TestCtx t)
        {
            var gc = FunTestHelpers.NewController(75004);
            var run = gc.State;
            FunTestHelpers.Roomy(run);
            run.Coins = 100;
            t.Eq(0, gc.CmdCost(SpinMode.N), "[R6] cmdCost(N)=0");
            t.Eq(0, gc.CmdCost(SpinMode.Allin), "[R6] cmdCost(ALLIN) 첫 사용 = 0(무료권)");
            t.Eq(0, gc.CmdCost(SpinMode.Focus), "[R6] cmdCost(FOCUS) 첫 사용 = 0");
            long c1 = run.Coins;
            var o1 = FunTestHelpers.SpinM(gc, SpinMode.Allin, "cherry", "book", "star", "gem", "crown");
            t.Eq(c1 + o1.result.coins, run.Coins, "[R6] ALLIN#1 무료(코인 불변)");
            t.Eq(8, gc.CmdCost(SpinMode.Allin), "[R6] cmdCost(ALLIN) 2번째 = 4×2 = 8");
            long c2 = run.Coins;
            var o2 = FunTestHelpers.SpinM(gc, SpinMode.Allin, "cherry", "book", "star", "gem", "crown");
            t.Eq(c2 - 8 + o2.result.coins, run.Coins, "[R6] ALLIN#2 코인 -8");
            t.Eq(8, o2.cmdCost, "[R6] ALLIN#2 outcome.cmdCost");
            t.Eq(12, gc.CmdCost(SpinMode.Allin), "[R6] cmdCost(ALLIN) 3번째 = 12");
            long c3 = run.Coins;
            var o3 = FunTestHelpers.SpinM(gc, SpinMode.Allin, "cherry", "book", "star", "gem", "crown");
            t.Eq(c3 - 12 + o3.result.coins, run.Coins, "[R6] ALLIN#3 코인 -12");
            t.True(!run.UsedCmds.Contains("ALLIN"), "[R6] UsedCmds 에 특수스핀 미기록(반복 허용)");
            t.Eq(3, run.CmdUses["ALLIN"], "[R6] CmdUses.ALLIN=3");
            t.Eq(0, gc.CmdCost(SpinMode.Focus), "[R6] 다른 종류는 독립: FOCUS 첫 사용 무료 유지");
            t.True(run.CmdFreeUsed.Contains("ALLIN"), "[R6] CmdFreeUsed.ALLIN 소진 유지");
            t.True(!run.CmdFreeUsed.Contains("FOCUS"), "[R6] CmdFreeUsed.FOCUS 미소진 유지");
            FunTestHelpers.SpinM(gc, SpinMode.Pray, "cherry", "book", "star", "gem", "crown");   // 기도 첫 사용(무료)
            t.Eq(6, gc.CmdCost(SpinMode.Pray), "[R6] PRAY 2번째 = 3×2 = 6");
            FunTestHelpers.SpinM(gc, SpinMode.Focus, "cherry", "book", "star", "gem", "crown");  // 집중 첫 사용(무료)
            t.Eq(2, gc.CmdCost(SpinMode.Focus), "[R6] FOCUS 2번째 = 1×2 = 2");
            FunTestHelpers.SpinM(gc, SpinMode.Focus, "cherry", "book", "star", "gem", "crown");
            t.Eq(3, gc.CmdCost(SpinMode.Focus), "[R6] FOCUS 3번째 = 3");

            // 스테이지 리셋: 누진 카운터 초기화·무료권은 런 단위라 소진 유지 → 기본가
            HoldFlow.ResetForStage(run);
            t.Eq(0, run.CmdUses["ALLIN"], "[R6] 다음 스테이지 cmdUses 리셋");
            t.Eq(4, gc.CmdCost(SpinMode.Allin), "[R6] 다음 스테이지 ALLIN = 기본 4 (무료권은 소진 유지)");
            t.Eq(1, gc.CmdCost(SpinMode.Focus), "[R6] 다음 스테이지 FOCUS = 기본 1");

            // 보스 스테이지: 보스 +1·상한 5 는 기본가에만 → ×n
            run.Stage = 45;   // 기말고사
            t.Eq(5, gc.CmdCost(SpinMode.Allin), "[R6] 보스 ALLIN 1번째 = min(4+1,5)=5");
            run.CmdUses["ALLIN"] = 1; t.Eq(10, gc.CmdCost(SpinMode.Allin), "[R6] 보스 ALLIN 2번째 = 5×2 = 10 (상한 5는 기본가에만)");
            run.CmdUses["ALLIN"] = 2; t.Eq(15, gc.CmdCost(SpinMode.Allin), "[R6] 보스 ALLIN 3번째 = 15");
            run.Stage = 41; run.CmdUses["ALLIN"] = 0;

            // 코인 부족 차단: 2번째 올인 비용 8 인데 코인 7
            var gp = FunTestHelpers.NewController(75005);
            FunTestHelpers.Roomy(gp.State);
            FunTestHelpers.SpinM(gp, SpinMode.Allin, "cherry", "book", "star", "gem", "crown");
            gp.State.Coins = 7; int idxB = gp.State.SpinIndex;
            var rej = gp.Do(new Spin(SpinMode.Allin));
            t.Eq("REJECTED", rej[0].type, "[R6] 코인 부족 시 발동 차단");
            t.Eq("INSUFFICIENT_COINS", rej[0].reason, "[R6] 코인 부족 사유");
            t.Eq(idxB, gp.State.SpinIndex, "[R6] 차단 시 스핀 진행 없음");
            t.Eq(7L, gp.State.Coins, "[R6] 차단 시 코인 불변");
            t.Eq(1, gp.State.CmdUses["ALLIN"], "[R6] 차단 시 사용 횟수 불변");
            gp.State.Coins = 8;
            var okSpin = FunTestHelpers.SpinM(gp, SpinMode.Allin, "cherry", "book", "star", "gem", "crown");
            t.Eq(0L + okSpin.result.coins, gp.State.Coins, "[R6] 코인 8이면 발동(8 소모)");

            // 최후(LAST): 마지막 스핀에서만
            var gl = FunTestHelpers.NewController(75006);
            FunTestHelpers.Roomy(gl.State);
            int iL = gl.State.SpinIndex;
            var lr = gl.Do(new Spin(SpinMode.Last));
            t.Eq("LAST_NOT_FINAL_SPIN", lr[0].reason, "[R6] LAST 는 마지막 스핀에서만");
            t.Eq(iL, gl.State.SpinIndex, "[R6] LAST 거부 시 진행 없음");
            t.True(!gl.State.CmdFreeUsed.Contains("LAST"), "[R6] LAST 거부 시 무료권 미소진");
            gl.State.SpinIndex = SpinResolver.EffSpins(gl.State, ModsBuilder.Build("basic", "novice", new List<string>(), new List<string>(), "")) - 1;
            var lok = gl.Do(new Spin(SpinMode.Last));
            t.True(lok[0].type != "REJECTED", "[R6] 마지막 스핀 LAST 발동");
        }

        private static void Subreel(TestCtx t)
        {
            t.EqTol(0.85, ModsBuilder.ApplyPassiveDevice(new Mods(), "dev_subreel").expMul, "[R6] dev_subreel expMul");
            t.True(Devices.ById("dev_subreel").desc.Contains("-15%") && !Devices.ById("dev_subreel").desc.Contains("-30%"), "[R6] dev_subreel 설명 -15%");
            var gsr = FunTestHelpers.NewController(75007, device: "dev_subreel");
            FunTestHelpers.Roomy(gsr.State);
            var o = FunTestHelpers.SpinN(gsr);
            t.Eq(6, o.result.cells.Count, "[R6] 보조릴 6칸");
            t.Eq(6, gsr.State.LastRaw.Count, "[R6] 보조릴 LastRaw 6칸(홀드 인덱스 범위)");
        }

        // 집중(FOCUS)의 rareWeightMul ×0.5 패널티 삭제 — 같은 시드에서 N 과 FOCUS/PRAY 의 fresh 굴림 raw 가 항상 같다
        // (패널티가 있으면 희귀(왕관/와일드) 가중이 달라져 시드 300개 중 일부는 raw 가 갈린다).
        private static void NoRareWeightPenalty(TestCtx t)
        {
            int diff = 0, total = 0;
            for (long seed = 1; seed <= 300; seed++)
            {
                var n = FunTestHelpers.NewController(seed); FunTestHelpers.Roomy(n.State);
                var f = FunTestHelpers.NewController(seed); FunTestHelpers.Roomy(f.State);
                var p = FunTestHelpers.NewController(seed); FunTestHelpers.Roomy(p.State);
                FunTestHelpers.SpinN(n);
                FunTestHelpers.SpinM(f, SpinMode.Focus);
                FunTestHelpers.SpinM(p, SpinMode.Pray);
                total++;
                var rn = FunTestHelpers.RawIds(n.State);
                if (!rn.SequenceEqual(FunTestHelpers.RawIds(f.State))) diff++;
                if (!rn.SequenceEqual(FunTestHelpers.RawIds(p.State))) diff++;
            }
            t.Eq(0, diff, $"[R6] 집중/기도 fresh 굴림이 일반 스핀과 동일(rareWeightMul 패널티 삭제, {total}시드)");
        }
    }

    // ── F1 빅윈 티어 · 상태 API 계약 ──────────────────────────────────────────────────────────
    internal static class Tests_FunW1_F1_BigWin
    {
        public static void Run(TestCtx t)
        {
            // 개정 임계 [2,3,5] (웹 harness 최신): 페이스 20 → 40/60/100 에서 NICE/BIG/MEGA.
            FunTestHelpers.SeqEq(t, new[] { 0, 1, 1, 2, 2, 3, 3 },
                new long[] { 39, 40, 59, 60, 99, 100, 1000 }.Select(x => Formulas.BigWinTier(x, 100, 5)), "[F1] tier 경계(페이스 20)");
            t.Eq(0, Formulas.BigWinTier(0, 100, 5), "[F1] tier(0)");
            t.Eq(0, Formulas.BigWinTier(1000, 100, 0), "[F1] spins=0 방어");
            t.Eq(0, Formulas.BigWinTier(1000, 0, 5), "[F1] quota=0 방어");
            FunTestHelpers.SeqEq(t, new[] { 2.0, 3.0, 5.0 }, Formulas.BIGWIN_TIERS, "[F1] BIGWIN_TIERS 상수");

            // 스핀 결과 필드: 5체리(잭팟) → MEGA(≥5), 스테이지 1 요구치 82/5 스핀 → 페이스 16.4
            var rc = FunTestHelpers.NewController(76002);
            var ev = rc.Do(new Spin(SpinMode.N));   // 칸 고정 없이는 tier 가 불확정이므로 아래에서 고정 스핀으로 검증
            var rc2 = FunTestHelpers.NewController(76002);
            FunTestHelpers.Force(rc2.State, FunTestHelpers.Set5);
            var ev2 = rc2.Do(new Spin(SpinMode.N));
            var o = ev2[0].spin;
            t.True(o != null && o.bigWinTier == Formulas.BigWinTier(o.gained, o.quota, o.spins), "[F1] bigWinTier = BigWinTier(gained, quota, spins)");
            t.True(o.bigWinTier == 3, "[F1] 5매치 체리는 MEGA(≥5)", $"tier={o.bigWinTier} gained={o.gained} quota={o.quota} spins={o.spins}");
            t.Eq("STAGE_CLEARED", ev2[0].type, "[F1] 클리어 스핀도 out.bigWinTier 필드 보유");
            t.Eq(3, rc2.LastSpin.bigWinTier, "[F1] lastSpin.bigWinTier");
            t.True(o.holdUsed != null && o.holdUsed.Count == 0, "[F1] out.holdUsed 빈 목록");
            t.True(o.encoreResult == null, "[F1] out.encoreResult null");
            t.True(ev[0].spin.holdUsed != null, "[F1] 일반 스핀 holdUsed 목록 존재");

            // 보스·특수스핀 이후 최종 EXP 기준: 올인 성공 시 티어 판정은 올인 배율 포함 gained 기준.
            var g3 = FunTestHelpers.NewController(76003);
            var o3 = FunTestHelpers.SpinM(g3, SpinMode.Allin, FunTestHelpers.Set3);
            t.Eq(Formulas.BigWinTier(o3.gained, o3.quota, o3.spins), o3.bigWinTier, "[F1] 올인(최종 EXP) 기준 tier");

            // 상태 API 계약 타입
            var g4 = FunTestHelpers.NewController(76004);
            t.True(g4.Hold != null && g4.Hold.held != null, "[F1] Hold 뷰");
            t.True(g4.Fever != null, "[F1] Fever 뷰");
            t.True(g4.Encore != null && !g4.Encore.available && g4.Encore.nextMul == null, "[F1] Encore 뷰(SPIN 페이즈: available=false)");
            t.True(g4.LastSpin == null, "[F1] 스핀 전 LastSpin == null");
        }
    }

    // ── 스트레스: 일반 150런 · 홀드/특수스핀(누진)/앵콜/피버를 무작위로 섞은 풀런 ─────────────────
    internal static class Tests_FunW1_Stress
    {
        public static void Run(TestCtx t)
        {
            int runsOk = 0, encores = 0, busts = 0, holds = 0, feverTrig = 0, specials = 0, settleOnLeave = 0, cashouts = 0;
            for (int s = 0; s < 150; s++)
            {
                var rc = FunTestHelpers.NewController(77000 + s);
                var rng = new Rng(88000 + s);
                int guard = 0, shopStep = 0;
                try
                {
                    while (rc.State.Phase != RunPhase.GameOver && guard++ < 20_000)
                    {
                        var run = rc.State;
                        IReadOnlyList<RunEvent> evs;
                        switch (run.Phase)
                        {
                            case RunPhase.Spin:
                            {
                                if (rc.LastSpin != null && rng.NextDouble() < 0.5)
                                {
                                    rc.ToggleHold(rng.Next(5)); holds++;
                                    if (rng.NextDouble() < 0.4) rc.ToggleHold(rng.Next(5));
                                }
                                var mode = SpinMode.N;
                                if (rng.NextDouble() < 0.25)
                                {
                                    mode = rng.Pick(new[] { SpinMode.Focus, SpinMode.Allin, SpinMode.Pray });
                                    if (run.Coins < rc.CmdCost(mode)) mode = SpinMode.N; else specials++;
                                }
                                evs = rc.Do(new Spin(mode));
                                if (evs[0].spin != null && !evs[0].spin.rejected && evs[0].spin.result != null)
                                {
                                    if (evs[0].spin.feverTriggered) feverTrig++;
                                    t.True(evs[0].spin.bigWinTier >= 0 && evs[0].spin.bigWinTier <= 3, $"[stress seed={s}] bigWinTier 범위");
                                }
                                break;
                            }
                            case RunPhase.PostSpin: evs = rc.Do(new Continue()); break;
                            case RunPhase.NodeSelect:
                            {
                                int n = 0;
                                while (rc.Encore.available && n++ < 8 && rng.NextDouble() < 0.75)
                                {
                                    var ev = rc.EncoreSpin(); encores++;
                                    t.Eq("ENCORE_SPIN", ev[0].type, $"[stress seed={s}] 앵콜 스핀 이벤트");
                                    if (rc.LastSpin.encoreResult.bust) busts++;
                                    if (rc.Encore.active && rng.NextDouble() < 0.7) { rc.EncoreCashOut(); cashouts++; }
                                    t.True(run.Score >= 0 && rc.Encore.pot >= 0, $"[stress seed={s}] 앵콜 후 점수/팟 음수 아님");
                                }
                                if (rc.Encore.active) settleOnLeave++;
                                evs = rc.Do(new ChooseNode(0));
                                break;
                            }
                            case RunPhase.EventAugment:
                            case RunPhase.EventRelic:
                            case RunPhase.EventAugLevel:
                                evs = rc.Do(new PickOffer(0)); break;
                            case RunPhase.EventShop:
                                if (shopStep == 0) { evs = rc.Do(new BuyOffer(0)); shopStep = 1; }
                                else { evs = rc.Do(new LeaveShop()); shopStep = 0; }
                                break;
                            case RunPhase.DeviceNode: evs = rc.Do(new TakeDevice(true)); break;
                            case RunPhase.RewardDone: evs = rc.Do(new ProceedToStage()); break;
                            default: throw new InvalidOperationException("stress: 처리 불가 Phase=" + run.Phase);
                        }
                        foreach (var e in evs)
                            if (!FunTestHelpers.KnownEvents.Contains(e.type))
                                throw new InvalidOperationException("stress: 알 수 없는 RunEvent.type=" + e.type);
                        var r2 = rc.State;
                        if (r2.Score < 0 || r2.Coins < 0) throw new InvalidOperationException($"stress: 음수 Score/Coins {r2.Score}/{r2.Coins}");
                        if (r2.NFeverGauge < 0 || r2.NFeverGauge >= Formulas.FEVER_MAX || r2.NFeverSpins < 0 || r2.NFeverSpins > Formulas.FEVER_SPINS)
                            throw new InvalidOperationException($"stress: 피버 범위 위반 {r2.NFeverGauge}/{r2.NFeverSpins}");
                        if (r2.Held.Count > Formulas.HOLD_MAX || r2.HoldCharges < 0) throw new InvalidOperationException("stress: 홀드 범위 위반");
                    }
                    if (rc.State.Phase == RunPhase.GameOver) runsOk++;
                    else t.Fail($"[stress seed={s}]", "guard 내 종료 실패 phase=" + rc.State.Phase);
                }
                catch (Exception ex)
                {
                    t.Fail($"[stress seed={s}]", "예외: " + ex);
                }
            }
            t.Eq(150, runsOk, "[stress] 일반 150런 전부 종료");
            t.True(encores > 20 && holds > 100 && specials > 50 && feverTrig > 0, "[stress] 앵콜/홀드/특수/피버가 실제로 쓰임",
                $"encores={encores} holds={holds} specials={specials} fever={feverTrig}");
            t.Report("[stress] 사용량", $"앵콜 {encores}회(BUST {busts}, 수동정산 {cashouts}, 노드이탈 시 자동정산 {settleOnLeave}) · 홀드 토글 {holds} · 특수스핀 {specials} · 피버 발동 {feverTrig}");
        }
    }

    // ── 심화 격리: 심화 런에서 신규 일반 시스템이 아무것도 동작하지 않음 (100런) ──────────────────
    internal static class Tests_FunW1_DeepIsolation
    {
        public static void Run(TestCtx t)
        {
            int okRuns = 0;
            for (int s = 0; s < 100; s++)
            {
                var rc = FunTestHelpers.NewController(78000 + s, deep: true);
                int guard = 0, shopStep = 0; string bad = "";
                while (rc.State.Phase != RunPhase.GameOver && guard++ < 20_000)
                {
                    if (rc.Hold != null || rc.Fever != null || rc.Encore != null) { bad = "신규 뷰 non-null"; break; }
                    IReadOnlyList<RunEvent> evs;
                    switch (rc.State.Phase)
                    {
                        case RunPhase.Spin:
                        {
                            rc.ToggleHold(0);
                            evs = rc.Do(new Spin(SpinMode.N));
                            var o = evs[0].spin;
                            if (o != null && !o.rejected && (o.feverApplied || o.feverTriggered || o.holdUsed.Count > 0 || o.encoreResult != null)) bad = "심화 스핀에 신규 시스템 작동";
                            break;
                        }
                        case RunPhase.PostSpin: evs = rc.Do(new Continue()); break;
                        case RunPhase.NodeSelect:
                            rc.EncoreSpin(); rc.EncoreCashOut();
                            evs = rc.Do(new ChooseNode(0));
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
                            evs = rc.Do(new PickOffer(0)); break;
                        case RunPhase.EventShop:
                            if (shopStep == 0) { evs = rc.Do(new BuyOffer(0)); shopStep = 1; }
                            else { evs = rc.Do(new LeaveShop()); shopStep = 0; }
                            break;
                        case RunPhase.DeviceNode: evs = rc.Do(new TakeDevice(true)); break;
                        case RunPhase.RewardDone: evs = rc.Do(new ProceedToStage()); break;
                        default: throw new InvalidOperationException("deep-iso: 처리 불가 Phase=" + rc.State.Phase);
                    }
                    if (bad != "") break;
                    foreach (var e in evs)
                        if (e.type == "ENCORE_SPIN" || e.type == "ENCORE_SETTLED" || e.type == "HOLD_CHANGED") bad = "심화 런에서 신규 이벤트 발생: " + e.type;
                    if (bad != "") break;
                }
                var r = rc.State;
                if (bad == "" && (r.NFeverGauge != 0 || r.NFeverSpins != 0 || r.HoldCharges != 0 || r.Held.Count != 0 || r.EncoreK != 0 || r.EncorePot != 0 || r.EncoreStake != 0 || !r.EncoreDone))
                    bad = $"심화 런에 신규 일반 시스템 상태 변경 (nFever={r.NFeverGauge}/{r.NFeverSpins} hold={r.HoldCharges} encoreK={r.EncoreK})";
                if (bad != "") { t.Fail($"[deep-iso seed={s}]", bad); break; }
                okRuns++;
            }
            t.Eq(100, okRuns, "[deep-iso] 심화 100런 격리");

            // 일반 런은 심화 카운터를 만들지 않는다 (기존 불변식 재확인)
            var gn = FunTestHelpers.NewController(78500);
            t.True(gn.State.DeepStats == null && gn.State.Pouch.Count == 0, "[deep-iso] 일반 런 deepStats/pouch 없음");
        }
    }
    // ── 웹 정본 교차 단언(1차 검수 [중대1·중대2·경미9]) ─────────────────────────────────────────
    // 값의 출처: 웹 `public/play/game.js` 를 그대로 구동하는 스크립트(`Game` + `_roll` 고정, funGame(12345)·novice·basic)의
    // 실제 출력. 동일 칸·동일 상태에서 C# 이 같은 EXP 를 내는지 확인한다.
    //  · A: EXP 후처리 순서 = 보스(applyBossExp) → 특수스핀(applySpecialSpin) → dev_safe 하한 → 피버, 그리고
    //       pendingNextExpMul 은 굴림 전 mods.expMul 에 곱해져 evaluate 안으로 들어간다.
    //  · B: evaluate 의 flameNext = hasFlame — 직전 스핀에 불꽃이 있었으면 다음 스핀 EXP -50%.
    //  · C: 기도 불운 보정 임계 = 페이스 전체(`exp < quota/spins`).
    internal static class Tests_FunW1_WebCross
    {
        private static readonly string[] P0 = FunTestHelpers.Pair0;
        private static readonly string[] S3 = FunTestHelpers.Set3;
        private static readonly string[] S2 = FunTestHelpers.Set2;

        // 웹 web_scen.mjs 의 scen(): 스테이지 설정 + 특수스핀 무료권 소진 + 코인 100 + spinIndex + 피버/보류 배율 + 칸 고정 후 1스핀.
        private static SpinOutcome Scen(int stage, int idx, SpinMode mode, string[] ids, int fever = 0, double pending = 1.0)
        {
            var rc = FunTestHelpers.NewController(12345L);
            var run = rc.State;
            run.Stage = stage; run.Coins = 100;
            foreach (var m in new[] { "FOCUS", "ALLIN", "PRAY", "LAST" }) run.CmdFreeUsed.Add(m);
            run.SpinIndex = idx; run.NFeverSpins = fever; run.PendingNextExpMul = pending;
            FunTestHelpers.Force(run, ids);
            var ev = rc.Do(new Spin(mode));
            t_lastRc = rc;
            return ev[0].spin;
        }
        private static RunController t_lastRc;

        public static void Run(TestCtx t)
        {
            // [중대1] 웹 값(web_scen.mjs 실행 결과): (이름, 스테이지, spinIndex, 모드, 칸, 피버, 웹 gained, 웹 quota, 웹 spins)
            var cases = new (string name, int stage, int idx, SpinMode mode, string[] ids, int fever, long web)[]
            {
                ("S10 strict 무매치 집중", 10, 1, SpinMode.Focus, P0, 0, 49),
                ("S10 strict 3매치 집중", 10, 1, SpinMode.Focus, S3, 0, 49),
                ("S10 strict 3매치 올인", 10, 1, SpinMode.Allin, S3, 0, 117),
                ("S10 strict 페어 올인", 10, 1, SpinMode.Allin, S2, 0, 52),
                ("S5 finals 첫스핀 집중", 5, 0, SpinMode.Focus, S2, 0, 26),
                ("S5 finals 막스핀 최후", 5, 5, SpinMode.Last, S3, 0, 143),
                ("S15 luck 별없음 집중", 15, 1, SpinMode.Focus, new[] { "cherry", "book", "gem", "cherry", "book" }, 0, 128),
                ("S10 strict 3매치 일반+피버", 10, 1, SpinMode.N, S3, 2, 70),
                ("S10 strict 무매치 집중+피버", 10, 1, SpinMode.Focus, P0, 2, 73),
            };
            foreach (var c in cases)
            {
                var o = Scen(c.stage, c.idx, c.mode, c.ids, c.fever);
                t.Eq(c.web, o.gained, $"[web-cross A] {c.name}: 웹 정본 EXP");
            }
            // 웹 quota/spins 도 같다 (prop 제거 후): S10 368/6, S5 201/6, S15 966/6.
            var q10 = Scen(10, 1, SpinMode.N, S3); t.Eq(368L, q10.quota, "[web-cross A] S10 quota 368"); t.Eq(6, q10.spins, "[web-cross A] S10 spins 6");
            t.Eq(201L, Scen(5, 0, SpinMode.N, S2).quota, "[web-cross A] S5 quota 201");
            t.Eq(966L, Scen(15, 1, SpinMode.N, S3).quota, "[web-cross A] S15 quota 966");

            // 다음스핀 EXP 배율(pendingNextExpMul): 굴림 전 mods.expMul 에 곱 → 보스/특수스핀은 그 뒤. 웹 값.
            t.Eq(49L, Scen(10, 1, SpinMode.Focus, P0, 0, 1.3).gained, "[web-cross A] pending×1.3 + S10 strict 무매치 집중 (웹 49)");
            t.Eq(60L, Scen(10, 1, SpinMode.N, S3, 0, 1.3).gained, "[web-cross A] pending×1.3 + S10 strict 3매치 (웹 60)");
            t.Eq(61L, Scen(1, 1, SpinMode.N, S3, 0, 1.5).gained, "[web-cross A] pending×1.5 + S1 3매치 (웹 61)");
            t.Eq(67L, Scen(10, 1, SpinMode.Allin, S2, 0, 1.3).gained, "[web-cross A] pending×1.3 + S10 strict 페어 올인 (웹 67)");
            t.Eq(92L, Scen(1, 4, SpinMode.Last, S3, 0, 1.3).gained, "[web-cross A] pending×1.3 + S1 막스핀 최후 (웹 92)");
            Scen(1, 1, SpinMode.N, S3, 0, 1.5);
            t.Eq(1.0, t_lastRc.State.PendingNextExpMul, "[web-cross A] 보류 배율은 스핀 후 1.0 으로 소비");

            // [중대2] 불꽃 여파 (웹 값: spin1 27·flameNext, spin2 20, 대조군 41).
            var fr = FunTestHelpers.NewController(12345L);
            var o1 = FunTestHelpers.SpinN(fr, "flame", "cherry", "book", "star", "gem");
            t.Eq(27L, o1.gained, "[web-cross B] 불꽃 스핀 gained (웹 27)");
            t.True(fr.State.FlameNext, "[web-cross B] 불꽃이 나온 스핀 뒤 FlameNext=true");
            t.True(o1.result.flameNext, "[web-cross B] SpinResult.flameNext = 불꽃 유무");
            var o2 = FunTestHelpers.SpinN(fr, S3);
            t.Eq(20L, o2.gained, "[web-cross B] 불꽃 직후 스핀은 EXP -50% (웹 20)");
            t.True(o2.notes.Any(n => n.Contains("여파 EXP -50%")), "[web-cross B] 여파 노트");
            t.True(!fr.State.FlameNext, "[web-cross B] 불꽃 없는 스핀 뒤 FlameNext=false (여파는 1스핀)");
            var ctl = FunTestHelpers.NewController(12345L);
            FunTestHelpers.SpinN(ctl, S2);
            t.Eq(41L, FunTestHelpers.SpinN(ctl, S3).gained, "[web-cross B] 대조군(불꽃 없음) 같은 칸 gained (웹 41)");

            // 스테이지 경계: 클리어하면 FlameNext 리셋 (웹 _beginStage `r.flameNext=false`).
            var fc = FunTestHelpers.NewController(12346L);
            fc.State.FlameNext = true; fc.State.StageExp = 100_000;
            var oc = fc.Do(new Spin(SpinMode.N));
            t.Eq("STAGE_CLEARED", oc[0].type, "[web-cross B] 클리어 스핀");
            t.True(!fc.State.FlameNext, "[web-cross B] 스테이지 클리어 시 FlameNext 리셋");

            // 조작/재굴림/재시험: 웹 manip()/_freeReroll() 는 evaluate(…, r.flameNext) 를 쓰고 결과의 hasFlame 으로 flameNext 를 갱신한다.
            var mr = FunTestHelpers.NewController(12347L, device: "dev_reroll");
            FunTestHelpers.Roomy(mr.State);
            mr.State.Coins = 50;
            FunTestHelpers.SpinN(mr, "flame", "cherry", "book", "star", "gem");
            t.True(mr.State.FlameNext, "[web-cross B] 조작 전 FlameNext=true");
            var mev = mr.Do(new DeviceCmd("dev_reroll"));
            t.Eq("DEVICE_MANIP_RESULT", mev[0].type, "[web-cross B] 재굴림기 발동");
            t.True(mev[0].spin.result.notes.Any(n => n.Contains("여파 EXP -50%")), "[web-cross B] 조작 evaluate 에 불꽃 여파 적용(웹 manip: flameNext 전달)");
            t.Eq(mev[0].spin.result.flameNext, mr.State.FlameNext, "[web-cross B] 조작 후 FlameNext = 조작 결과의 불꽃 유무(웹 `r.flameNext = res.hasFlame`)");
            var rr = FunTestHelpers.NewController(12348L);
            FunTestHelpers.Roomy(rr.State);
            FunTestHelpers.SpinN(rr, "flame", "cherry", "book", "star", "gem");
            rr.State.Items.Add("retake_form");
            var rev = rr.Do(new UseItem("retake_form"));
            t.True(rev[0].spin.result.notes.Any(n => n.Contains("여파 EXP -50%")), "[web-cross B] 재시험 evaluate 에 불꽃 여파 적용");
            t.Eq(rev[0].spin.result.flameNext, rr.State.FlameNext, "[web-cross B] 재시험 후 FlameNext = 결과의 불꽃 유무");

            // [경미9] 기도 불운 보정 임계 = 페이스 전체(quota/spins = 82/5 = 16.4). 기본 EXP 10·14 (옛 임계 8 이상, 페이스 미만).
            //   miracle(8%)이 아닌 시드: gained = 기본 + 25. miracle 이면 기본×3.
            int plus25 = 0, miracle = 0;
            foreach (var (ids, baseExp) in new[] { (new[] { "cherry", "book", "gem", "coin", "coin" }, 10L), (new[] { "cherry", "cherry", "coin", "coin", "coin" }, 14L) })
            {
                for (long seed = 1; seed <= 60; seed++)
                {
                    var rc = FunTestHelpers.NewController(9000 + seed);
                    rc.State.SpinIndex = 1;
                    var o = FunTestHelpers.SpinM(rc, SpinMode.Pray, ids);
                    t.Eq(82L, o.quota, "[web-cross C] 기도 사전조건 quota");
                    if (o.prayMiracle) { miracle++; t.Eq(baseExp * 3, o.gained, "[web-cross C] 기적 ×3"); }
                    else { plus25++; t.Eq(baseExp + 25, o.gained, $"[web-cross C] 기본 EXP {baseExp} < 페이스 16.4 → 불운 보정 +25 (옛 임계 ×0.5=8 이면 보정 없음)"); }
                }
            }
            t.True(plus25 > 80 && miracle > 0, "[web-cross C] 표본: 보정 경로와 기적 경로가 모두 실사됨", $"plus25={plus25} miracle={miracle}");
            // 페이스 이상이면 무응답: 기본 EXP 20(book×2+coin×3) ≥ 16.4 → 보정 없음.
            var rcN = FunTestHelpers.NewController(9100L); rcN.State.SpinIndex = 1;
            for (long seed = 1; seed <= 60; seed++)
            {
                var rc = FunTestHelpers.NewController(9200 + seed); rc.State.SpinIndex = 1;
                var o = FunTestHelpers.SpinM(rc, SpinMode.Pray, "book", "book", "coin", "coin", "coin");
                if (!o.prayMiracle) t.Eq(20L, o.gained, "[web-cross C] 기본 EXP 20 ≥ 페이스 → 보정 없음(무응답)");
            }
        }
    }
}
