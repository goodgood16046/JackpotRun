using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 스핀 1회 처리 결과의 최종 분기 종류 — 02_service.md §2 step26 + §3-C 실패체인의 귀결점.
    public enum SpinStepKind
    {
        Rejected,   // 특수명령 검증 실패(코인부족/이미사용/최후 타이밍) — 상태 변경 없음
        Continue,   // 스핀 소진/클리어 모두 아님 — 스테이지 계속
        Cleared,    // 즉시 클리어(newExp >= quota)
        Revived,    // 운명의종/보험증서로 스핀 소진 직전 자동 회생 — SPIN 유지
        PostSpin,   // 마지막 스핀 실패, MANIP/도박꾼재굴림 만회 기회 있음 → POST_SPIN 전환(S4가 처리)
        GameOver,   // 만회 수단 없음 → 게임오버
    }

    // 스테이지 클리어 보상/진행 결과 — 02_service.md §3-D/§3-E/§3-F clearStage() 전사분.
    public sealed class ClearOutcome
    {
        public int clearedStage;      // 방금 클리어한 스테이지(증가 전 값)
        public bool boss;             // isBossStage(clearedStage)
        public long leftover;         // newExp - quota (하한 0)
        public int leftSpins;         // spins - newSpinIndex (하한 0)
        public bool lastSpinClear;    // newSpinIndex >= spins
        public bool closeClear;       // leftover <= 10
        public bool fastClear;        // leftSpins >= 2
        public long overPct;          // newExp*100/quota (정수 나눗셈), quota<=0이면 100
        public string grade;          // "✅합격".."💥슬롯파괴자"
        public long gradeBonus;
        public long closeBonus;       // 턱걸이/아슬아슬/막판클리어/연승 합
        public long clearScore;       // Formulas.StageClearScore(...)
        public long gainedScore;      // inDebt면 0, 아니면 clearScore+closeBonus+gradeBonus
        public long clearCoin;        // inDebt면 0, 아니면 (boss?BOSS_COIN:CLEAR_COIN)+mods.clearCoinBonus
        public bool inDebt;
        public List<NodeKind> nodeOptions; // 항상 3개(AUGMENT 필수 + 무작위 2개)
        public bool nextNodeForcedPrism;   // 보스 클리어 직후 = 다음 AUGMENT/RELIC 노드 PRISM 확정(§3-F)
    }

    // 스테이지 실패(폭망) 처리 결과 — 02_service.md §3-C 체인의 귀결.
    public sealed class FailureOutcome
    {
        public string kind; // "FATE_BELL_REVIVE" | "INSURANCE_REVIVE" | "POST_SPIN" | "GAME_OVER"
        public long deficitAtFailure; // quota - newExp (참고용, POST_SPIN/GameOver 메시지 구성에 유용)
        public readonly List<string> manipHints = new List<string>(); // "GAMBLER_REROLL" / "DEVICE:<id>" (POST_SPIN 전용)
        public long finalScore; // kind=="GAME_OVER"일 때만 유효 — §8-A scoreModifier 적용된 최종 점수
    }

    public sealed class SpinStepResult
    {
        public SpinStepKind kind;
        public SpinOutcome spin;      // rejected여도 mode/rejectReason 확인용으로 채워짐
        public ClearOutcome clear;    // kind==Cleared일 때만
        public FailureOutcome failure; // kind가 Revived/PostSpin/GameOver일 때만
    }

    // 스핀 소비 → 클리어/실패 판정 → 스테이지 진행 오케스트레이터.
    // 02_service.md §3(스테이지 진행) 전사: §3-A(스핀수/쿼터는 SpinResolver.EffSpins/QuotaOf 재사용),
    // §3-B(클리어 판정), §3-C(실패체인), §3-D(클리어 보상), §3-E(상태 리셋), §3-F(보스 규칙 일부 —
    // 보너스스핀/쿼터 비례는 §3-A에 이미 반영, 여기서는 BOSS_COIN 보상과 다음노드 프리즘 확정만 담당.
    // per-spin 배율 규칙(finals/strict/luck/grad)은 SpinResolver.ApplyBoss가 담당 — §2 소관).
    public static class StageFlow
    {
        // ── 진입점: 스핀 1회 소비 + 클리어/실패 분기 (02_service.md §2 step26 이후) ──
        public static SpinStepResult ProcessSpin(RunState run, SpinMode mode)
        {
            // Kotlin handleInput의 when(run.state) 라우팅 대응 — 스핀 가능 상태가 아니면 거부.
            // Rejected일 때 spin.result는 null이다(호출측은 kind만 보고 분기할 것).
            if (run.Phase != RunPhase.Spin)
                return new SpinStepResult { kind = SpinStepKind.Rejected, spin = SpinResolver.RejectedOutcome("PHASE_NOT_SPIN") };

            var outcome = SpinResolver.ResolveSpin(run, mode);
            if (outcome.rejected)
                return new SpinStepResult { kind = SpinStepKind.Rejected, spin = outcome };

            // §3-B: 스핀 도중 어느 시점이든 newExp>=quota면 즉시 클리어(스핀 소진 여부 무관, 대기 없음).
            if (outcome.newExp >= outcome.quota)
            {
                var clear = ClearStage(run, outcome);
                return new SpinStepResult { kind = SpinStepKind.Cleared, spin = outcome, clear = clear };
            }

            // 스핀 소진(newIdx>=spins) + 미클리어 → §3-C 실패체인.
            if (outcome.newSpinIndex >= outcome.spins)
            {
                var failure = HandleFailure(run, outcome);
                SpinStepKind kind = failure.kind switch
                {
                    "FATE_BELL_REVIVE" => SpinStepKind.Revived,
                    "INSURANCE_REVIVE" => SpinStepKind.Revived,
                    "POST_SPIN" => SpinStepKind.PostSpin,
                    _ => SpinStepKind.GameOver,
                };
                return new SpinStepResult { kind = kind, spin = outcome, failure = failure };
            }

            return new SpinStepResult { kind = SpinStepKind.Continue, spin = outcome };
        }

        // ── §3-D/§3-E: 클리어 보상 계산 + 스테이지 스코프 상태 리셋 + 다음 노드 3택 준비 ──
        private static ClearOutcome ClearStage(RunState run, SpinOutcome outcome)
        {
            int clearedStage = run.Stage;
            int spins = outcome.spins;
            long quota = outcome.quota;
            long newExp = outcome.newExp;
            int newIdx = outcome.newSpinIndex;

            int leftSpins = Math.Max(spins - newIdx, 0);
            long leftover = Math.Max(newExp - quota, 0);
            bool boss = Formulas.IsBossStage(clearedStage);

            long clearScore = Formulas.StageClearScore(clearedStage, leftover, leftSpins, run.Curses.Count, boss);

            bool lastSpinClear = newIdx >= spins;
            bool closeClear = leftover <= 10;
            bool fastClear = leftSpins >= 2;

            int newGrowthStack = Math.Min(run.GrowthStack + 1, 5);
            int newSnowStack = run.SnowStack;
            if (fastClear) newSnowStack = Math.Min(newSnowStack + 1, 4);
            if (boss) newSnowStack = Math.Max(newSnowStack - 1, 0); // 겹치면 둘 다 순차 적용(Kotlin L840-841과 동일)

            bool inDebt = run.DebtStages > 0;

            // clearCoinBonus만 필요 — Kotlin과 동일하게 ctx 없이(기본 RunCtx) perks+phasePerks로 재구성.
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var mods = ModsBuilder.ApplyItemMods(
                ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device),
                run.PhaseItems);
            long clearCoin = inDebt ? 0 : (boss ? Formulas.BOSS_COIN : Formulas.CLEAR_COIN) + mods.clearCoinBonus;

            long close = 0;
            if (leftover <= 5) close += 300;
            else if (leftover <= 10) close += 150;
            if (newIdx >= spins) close += 200; // 위 조건과 배타적이지 않음(둘 다 성립 시 합산)
            long streakB = Formulas.StreakBonus(clearedStage);
            if (streakB > 0) close += streakB;

            long overPct = quota > 0 ? newExp * 100 / quota : 100;
            string grade; long gradeBonus;
            if (overPct >= 500) { grade = "💥슬롯파괴자"; gradeBonus = 1000; }
            else if (overPct >= 300) { grade = "👹괴물"; gradeBonus = 500; }
            else if (overPct >= 200) { grade = "🌟천재"; gradeBonus = 250; }
            else if (overPct >= 150) { grade = "🎓장학생"; gradeBonus = 120; }
            else if (overPct >= 120) { grade = "✨우수"; gradeBonus = 50; }
            else { grade = "✅합격"; gradeBonus = 0; }

            long gainedScore = inDebt ? 0 : clearScore + close + gradeBonus;
            int nextStage = clearedStage + 1;

            var nodes = RollNextNodes(run.Rng, nextStage);

            // ── 상태 반영 (Kotlin clearStage L872-892) ──
            run.Score = outcome.newScore + gainedScore;
            run.Coins = outcome.newCoins + clearCoin;
            run.Stage = nextStage;
            run.SpinIndex = 0;
            run.StageExp = 0;
            run.FlameNext = false;
            run.SeedNext = false;
            run.ArmItems.Clear();
            run.PhaseItems.Clear();
            run.StageBonusSpins = 0;
            run.UsedCmds.RemoveWhere(cmd => cmd != "RUNSHOP" && cmd != "RUNORACLE");
            run.DebtStages = Math.Max(run.DebtStages - 1, 0);
            run.PhasePerks.Clear();
            run.LastCells.Clear();
            run.LastGain = 0; run.LastScoreGain = 0; run.LastCoinGain = 0; run.LastSpinNo = -1;
            run.PendingNextExpMul = 1.0;
            run.LockedNext.Clear();
            run.DevCooldown = Math.Max(run.DevCooldown - 1, 0); // §9-A: 감소만, set/check 로직은 원본에도 없음
            run.ClosestClear = run.ClosestClear < 0 ? (int)leftover : Math.Min(run.ClosestClear, (int)leftover);
            run.RunLastSpinClears += lastSpinClear ? 1 : 0;
            run.RunCloseClears += closeClear ? 1 : 0;
            run.RunFastClears += fastClear ? 1 : 0;
            run.GrowthStack = newGrowthStack;
            run.SnowStack = newSnowStack;
            run.NodeOptions.Clear();
            run.NodeOptions.AddRange(nodes);
            run.Phase = RunPhase.NodeSelect;

            return new ClearOutcome
            {
                clearedStage = clearedStage,
                boss = boss,
                leftover = leftover,
                leftSpins = leftSpins,
                lastSpinClear = lastSpinClear,
                closeClear = closeClear,
                fastClear = fastClear,
                overPct = overPct,
                grade = grade,
                gradeBonus = gradeBonus,
                closeBonus = close,
                clearScore = clearScore,
                gainedScore = gainedScore,
                clearCoin = clearCoin,
                inDebt = inDebt,
                nodeOptions = nodes,
                nextNodeForcedPrism = boss, // §3-F: bossClear = clearedStage%5==0 → 다음 AUGMENT/RELIC PRISM 확정
            };
        }

        // Kotlin clearStage L868-871: pool=[RELIC,SHOP,REST,GAMBLE,EVENT] (+CURSE,RISK if nextStage>=6)
        // → 셔플 후 2개 추출 → AUGMENT 필수 1개와 합쳐 다시 셔플 = 항상 3개.
        private static readonly NodeKind[] BasePool =
            { NodeKind.Relic, NodeKind.Shop, NodeKind.Rest, NodeKind.Gamble, NodeKind.Event };

        private static List<NodeKind> RollNextNodes(Rng rng, int nextStage)
        {
            var pool = new List<NodeKind>(BasePool);
            if (nextStage >= 6) { pool.Add(NodeKind.Curse); pool.Add(NodeKind.Risk); }
            rng.Shuffle(pool); // pool은 항상 5~7개(never empty) — Rng.Shuffle은 Count<2면 무동작, 안전
            int takeCount = Math.Min(2, pool.Count);
            var extras = pool.GetRange(0, takeCount);
            var nodes = new List<NodeKind> { NodeKind.Augment };
            nodes.AddRange(extras);
            rng.Shuffle(nodes);
            return nodes;
        }

        // ── §3-C: 실패(폭망) 처리 체인 — 순서대로 시도 ──
        private static FailureOutcome HandleFailure(RunState run, SpinOutcome outcome)
        {
            long deficit = outcome.quota - outcome.newExp;

            // 1) 운명의종 — 런 1회, 부족분 <= 15면 자동 스핀+1, 스핀 소진 없이 계속(SPIN 유지).
            if (deficit <= 15 && !run.FateBellUsed && run.Perks.Contains("fate_bell"))
            {
                run.FateBellUsed = true;
                run.StageBonusSpins += 1;
                return new FailureOutcome { kind = "FATE_BELL_REVIVE", deficitAtFailure = deficit };
            }

            // 2) 보험증서 — 1회용, 스핀+2.
            if (run.Survive)
            {
                run.Survive = false;
                run.StageBonusSpins += 2;
                return new FailureOutcome { kind = "INSURANCE_REVIVE", deficitAtFailure = deficit };
            }

            // 3) 만회 기회(POST_SPIN) — MANIP 장치 또는 도박꾼 무료재굴림, 이번 스테이지 미사용.
            //    [설계 계약 결손 — 보고 대상] ContentTypes.cs의 DeviceDef엔 Kotlin Device.cmd 필드가 없다
            //    (Devices.cs 헤더 주석 확인 — "cmd/needsArg/cooldown은 DeviceDef 계약에 필드가 없어 다루지
            //    않는다"). Kotlin은 `dev.cmd !in usedCmds`로 스테이지당 1회를 검사하지만, cmd 문자열이
            //    없어 대신 dev.id를 마커로 쓴다(스테이지당 1회라는 동작 자체는 동일 — 실제 사용 표시는
            //    S4 DeviceActions.cs가 이 마커 규약을 그대로 따라야 함).
            var dev = Devices.ById(run.Device);
            bool manipAvail = dev != null && dev.kind == "MANIP" && !run.UsedCmds.Contains(dev.id);
            bool gamblerReroll = run.CharId == "gambler" && !run.UsedCmds.Contains("GREROL");

            if (manipAvail || gamblerReroll)
            {
                run.Phase = RunPhase.PostSpin;
                var f = new FailureOutcome { kind = "POST_SPIN", deficitAtFailure = deficit };
                if (gamblerReroll) f.manipHints.Add("GAMBLER_REROLL");
                if (manipAvail) f.manipHints.Add("DEVICE:" + dev.id);
                return f;
            }

            // 4) 그 외 — 게임오버.
            return ForceGameOver(run, deficit);
        }

        // S4가 POST_SPIN 만회 시도(MANIP/도박꾼재굴림, SpinResolver.cs 파일 끝 훅 주석 참조)에서도 끝내
        // 클리어시키지 못했을 때 호출할 최종 게임오버 경로. §3-C step4/§8-A(scoreModifier) 전사.
        public static FailureOutcome ForceGameOver(RunState run, long deficitAtFailure)
        {
            run.Phase = RunPhase.GameOver;
            long finalScore = (long)(run.Score * ScoreModifierFor(run.MachineId, run.CharId));
            return new FailureOutcome
            {
                kind = "GAME_OVER",
                deficitAtFailure = deficitAtFailure,
                finalScore = finalScore,
            };
        }

        // ── §8-A: 최종 점수 = run.score(누적 원점수) × scoreModifier(머신×캐릭터), 런 종료 시 1회만 ──
        public static double ScoreModifierFor(string machineId, string charId) =>
            Machines.ById(machineId).scoreMod * Characters.ById(charId).scoreMod;

        // ════════════════════════════════════════════════════════════════════
        // S4 훅 — 노드 선택(NODE_SELECT)·상점 진입(EVENT_SHOP)은 이 슬라이스가 enum 상태(RunPhase/NodeKind)
        // 와 오퍼 생성 지점(ClearOutcome.nodeOptions/nextNodeForcedPrism)만 내려주고 실제 처리는 하지
        // 않는다(작업 지시 "노드 선택/상점 진입은 enum 상태와 훅만 정의"). S4(Shop.cs/RunController.cs)가
        // 구현해야 할 계약:
        //   - NodeKind.Augment/Relic: offerPerks 상당(02_service.md §5-B) — tierForClearedStage(clearedStage)
        //     (Formulas.TierForClearedStage 재사용) + nextNodeForcedPrism이면 무조건 PRISM, 10% 확률
        //     tierUp(Formulas.TierUp), heldAug(dev_holdfile) 우선삽입, 5% 세트시너지 주입 등.
        //   - NodeKind.Shop: RunPhase.EventShop 전환 + freshShopOffer(02_service.md §4) — 6칸 생성/가격/
        //     리롤/구매, ITEM_SLOTS(=3) 가방 여유 확인.
        //   - NodeKind.Rest/Gamble/Event/Curse/Risk: 즉시 계산형(§5 표) — 코인/점수/저주 지급 후 다시
        //     RunPhase.Spin으로 복귀. 이 슬라이스는 손대지 않는다.
        //   - 선택 후 공통: run.Phase를 Spin으로 되돌리기 전에 "AUGMENT/RELIC 즉시 지급은 perks에 추가"
        //     "SHOP은 구매만 즉시 반영, 상점 자체는 유지" 같은 §4/§5 규칙을 그대로 따를 것.
        // ════════════════════════════════════════════════════════════════════
    }
}
