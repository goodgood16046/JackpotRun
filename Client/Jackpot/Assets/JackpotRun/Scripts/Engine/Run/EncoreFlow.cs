using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // ══════════════════════════════════════════════════════════════════════
    // 재미 개편 R5 앵콜(클리어 점수 더블업, 일반 모드 전용) — 웹 game.js `_encore*`/`encoreSpin`/`encoreCashOut`(e6bde8d)
    // + FUN_OVERHAUL_DESIGN §1 R5 "클리어 점수 더블업" 최신본.
    //
    //  · 스테이지를 남은 스핀 ≥1 로 클리어했고 이번 클리어 점수 stake(ClearOutcome.gainedScore, 빚문서면 0)가 >0 이면,
    //    노드로 가기 전에 [정산하고 진행](=ChooseNode) 또는 [앵콜 스핀]을 고를 수 있다.
    //  · 첫 앵콜 스핀 때 stake 를 run 점수에서 빼서 pot=stake 로 건다(점수<stake 면 시작 불가). 앵콜 스핀 1회 = 남은 스핀
    //    1개 소모 + 일반 굴림. 스핀의 EXP·점수는 버리고 코인 수입만 정상 지급.
    //  · 성공(페어 이상 AND 해골 2개 미만): k+=1, pot=floor(stake×(1+0.5k)) (×1.5, ×2, ×2.5 …). BUST(페어 미만 또는 해골 2개↑):
    //    pot=0(판돈 포함 전부 소멸), 앵콜 종료. 정산(EncoreCashOut)하거나 남은 스핀이 0이 되면 score+=pot.
    //    노드 선택(ChooseNode)도 진행 중인 앵콜을 먼저 정산한다.
    //  · 앵콜 중에는 특수스핀·홀드·아이템 불가, 피버 충전/소모 없음, 보스 규칙 미적용, 업적/해골/심볼 통계 추적 없음
    //    (StatTracker 는 ENCORE_SPIN 이벤트를 무시한다 — 통계/도감/불운게이지/RunSymCounts 불변).
    //
    // Unity 구조 주의: 웹은 STAGE_CLEAR 페이즈 동안 spinIndex/spins/quota/phaseItems/stage-1 을 그대로 두고 앵콜을 굴리지만,
    // C# StageFlow.ClearStage 는 클리어 즉시 다음 스테이지 값으로 리셋한다. 그래서 클리어 시점에 필요한 값을 RunState.Encore*
    // 에 스냅샷(OnStageCleared)해 두고, 앵콜 스핀의 mods 를 "클리어한 스테이지" 기준으로 웹과 같은 의미로 다시 만든다.
    // 앵콜 창 = (Phase==NodeSelect && !EncoreDone).
    // ══════════════════════════════════════════════════════════════════════
    internal static class EncoreFlow
    {
        // 앵콜 상태 초기화 + 창 닫기. 스테이지 시작(HoldFlow.ResetForStage)·노드 선택 진입·A10 재시작에서 호출.
        public static void Reset(RunState run)
        {
            run.EncoreK = 0; run.EncorePot = 0; run.EncoreStake = 0;
            run.EncoreActive = false; run.EncoreDone = true; run.EncoreLastBust = false;
            run.EncoreStage = 0; run.EncoreSpinsTotal = 0; run.EncoreSpinsUsed = 0;
            run.EncoreStageExp = 0; run.EncoreQuota = 0; run.EncoreBoss = false; run.EncoreBannedSym = "";
            run.EncorePhaseItems.Clear(); run.EncorePhasePerks.Clear();
        }

        // StageFlow.ClearStage(진짜 클리어)의 "상태 반영" 직전에 호출 — 아직 리셋되기 전의 PhaseItems/PhasePerks/BannedSym 과
        // 클리어 스테이지 컨텍스트를 스냅샷하고 앵콜 창을 연다(일반 모드만). stake = 이번 클리어 점수(빚문서면 0).
        public static void OnStageCleared(RunState run, int clearedStage, int totalSpins, int usedSpins,
                                          long stageExp, long quota, bool boss, long stake)
        {
            Reset(run);
            if (run.DeepMode) return;   // 심화: 앵콜 없음(창 닫힘 유지)
            run.EncoreStake = Math.Max(0, stake);
            run.EncoreDone = false;
            run.EncoreStage = clearedStage;
            run.EncoreSpinsTotal = totalSpins;
            run.EncoreSpinsUsed = usedSpins;
            run.EncoreStageExp = stageExp;
            run.EncoreQuota = quota;
            run.EncoreBoss = boss;
            run.EncoreBannedSym = run.BannedSym ?? "";
            run.EncorePhaseItems.AddRange(run.PhaseItems);
            run.EncorePhasePerks.AddRange(run.PhasePerks);
        }

        public static int Left(RunState run) => Math.Max(0, run.EncoreSpinsTotal - run.EncoreSpinsUsed);

        // 웹 _encoreMul(k): 성공 k번째 팟 배율 = 1 + STEP×k (소수 둘째 자리 반올림).
        public static double Mul(int k) => Math.Round((1.0 + Formulas.ENCORE_STEP * k) * 100.0, MidpointRounding.AwayFromZero) / 100.0;

        // BUST 판정: 페어(ENCORE_MIN_SET개 이상 같은 값심볼) 미만이거나 해골 ENCORE_BUST_SKULLS개 이상.
        internal static bool IsBust(int bestSetCount, int skulls) =>
            bestSetCount < Formulas.ENCORE_MIN_SET || skulls >= Formulas.ENCORE_BUST_SKULLS;

        // 웹 _encoreAvailable(): [앵콜 스핀] 버튼이 유효한가.
        public static bool Available(RunState run) =>
            !run.DeepMode && run.Phase == RunPhase.NodeSelect && !run.EncoreDone && Left(run) >= 1
            && run.EncoreStake > 0 && (run.EncoreActive || run.Score >= run.EncoreStake);

        public static EncoreView View(RunState run)
        {
            if (run.DeepMode) return null;
            bool avail = Available(run);
            return new EncoreView
            {
                available = avail,
                active = run.EncoreActive,
                k = run.EncoreK,
                pot = run.EncorePot,
                stake = run.EncoreStake,
                lastBust = run.EncoreLastBust,
                nextMul = avail ? Mul(run.EncoreK + 1) : (double?)null,
            };
        }

        // 웹 _encoreSettle(): 팟을 점수에 더하고 앵콜 종료. 가산한 팟을 반환. (웹 _encoreSyncSummary 는 C# 에 대응물 없음 —
        // ClearOutcome 은 이벤트에 실린 불변 페이로드이고 UI 는 State.Score/Coins 를 실시간으로 읽는다.)
        public static long Settle(RunState run)
        {
            long pot = run.EncorePot;
            run.Score += pot;
            run.EncorePot = 0; run.EncoreActive = false; run.EncoreDone = true;
            return pot;
        }

        // 진행 중 앵콜 정산(웹 proceedToNodes 의 `if (r.encoreActive) this._encoreSettle()`) + 앵콜 창 닫기.
        // NodeEvents.ChooseNode 가 선택이 확정된 뒤(검증 통과 후) 호출한다. 진행 중이었다면 정산한 팟(≥0)을, 아니면 -1 을 반환.
        public static long CloseOnLeave(RunState run)
        {
            long settled = -1;
            if (run.EncoreActive) settled = Settle(run);
            run.EncoreDone = true;
            return settled;
        }

        // 웹 encoreCashOut(): 진행 중인 앵콜이 있으면 정산. 없으면 거부.
        public static List<RunEvent> CashOut(RunState run)
        {
            if (run.DeepMode) return RunEvents.Rejected("ENCORE_DEEP_MODE");
            if (run.Phase != RunPhase.NodeSelect) return RunEvents.Rejected("PHASE_NOT_NODE_SELECT");
            if (!run.EncoreActive) return RunEvents.Rejected("ENCORE_NOT_ACTIVE");
            long pot = Settle(run);
            return RunEvents.One(new RunEvent { type = "ENCORE_SETTLED", scoreDelta = pot });
        }

        // 웹 encoreSpin(). forcedIds(내부 전용): 테스트가 굴림을 심볼 id 열로 고정할 때만 넘긴다(null=실제 굴림) —
        // 메인 스핀의 LockedNext 와 같은 역할의 최소 접점. 나머지(판돈 차감·BUST/성공·정산)는 항상 동일 경로를 탄다.
        public static List<RunEvent> Spin(RunState run, IReadOnlyList<string> forcedIds = null)
        {
            if (run.DeepMode) return RunEvents.Rejected("ENCORE_DEEP_MODE");
            if (run.Phase != RunPhase.NodeSelect) return RunEvents.Rejected("PHASE_NOT_NODE_SELECT");
            if (run.EncoreDone) return RunEvents.Rejected("ENCORE_ALREADY_DONE");
            if (Left(run) < 1) return RunEvents.Rejected("ENCORE_NO_SPINS_LEFT");
            if (!(run.EncoreStake > 0)) return RunEvents.Rejected("ENCORE_NO_STAKE");
            if (!run.EncoreActive && run.Score < run.EncoreStake) return RunEvents.Rejected("ENCORE_STAKE_SHORT");

            // 첫 앵콜 스핀: 클리어 점수(stake)를 run 점수에서 빼 팟으로 건다.
            if (!run.EncoreActive) { run.Score -= run.EncoreStake; run.EncorePot = run.EncoreStake; run.EncoreActive = true; }
            long potBefore = run.EncorePot;
            int kNew = run.EncoreK + 1;
            double mulApplied = Mul(kNew);

            // 남은 스핀 1개 소모. mods 는 방금 클리어한 스테이지 기준(스냅샷)으로 웹 _mods() 와 같은 의미로 재구성.
            int idx = run.EncoreSpinsUsed;
            run.EncoreSpinsUsed += 1;
            var mods = BuildMods(run, idx);
            List<Cell> raw;
            if (forcedIds != null) raw = SpinResolver.CellsFromIds(forcedIds, run.Rng, run.Pouch);
            else
            {
                var devEq = Devices.ById(run.Device);
                int reel = (devEq != null && devEq.id == "dev_subreel") ? Formulas.REEL + 1 : Formulas.REEL;
                raw = SpinResolver.RollCells(run, mods, reel, false);
            }
            var res = SpinResolver.Evaluate(run.Rng, raw, mods, idx, run.EncoreSpinsTotal, false);
            run.Coins += res.coins;   // 코인 심볼 수입은 정상 지급(EXP·점수는 버림)

            bool bust = IsBust(res.bestSetCount, res.skulls);
            long gained = 0, lostPot = 0, settledPot = 0;
            bool settled = false;
            var notes = new List<string>();
            if (bust)
            {
                lostPot = potBefore;
                run.EncorePot = 0; run.EncoreActive = false; run.EncoreDone = true; run.EncoreLastBust = true;
                notes.Add($"💥 앵콜 BUST! — 팟 {lostPot}점 소멸");
            }
            else
            {
                run.EncoreK = kNew; run.EncoreLastBust = false;
                run.EncorePot = Formulas.MulFloor(run.EncoreStake, mulApplied);
                gained = run.EncorePot - potBefore;
                notes.Add($"🎤 앵콜 성공 ×{FmtMul(mulApplied)} — 팟 {run.EncorePot}점");
                if (Left(run) <= 0)   // 남은 스핀 0 → 자동 정산
                {
                    settledPot = Settle(run);
                    settled = true;
                    notes.Add($"🎤 앵콜 정산 — 팟 {settledPot}점 획득 (판돈 {run.EncoreStake}점 대비 ×{FmtMul(settledPot / (double)run.EncoreStake)})");
                }
            }

            var encoreResult = new EncoreResult
            {
                mulApplied = mulApplied, gained = gained, bust = bust, settled = settled, settledPot = settledPot, lostPot = lostPot,
            };
            run.LastSpin = new LastSpinInfo
            {
                kind = "encore", mode = SpinMode.N, cells = new List<Cell>(res.cells), gained = 0, score = res.score, coins = res.coins,
                feverApplied = false, feverTriggered = false, holdUsed = new List<int>(), encoreResult = encoreResult, bigWinTier = 0,
            };
            var outcome = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = res, gained = 0,
                newExp = run.EncoreStageExp, newScore = run.Score, newCoins = run.Coins, newSpinIndex = run.EncoreSpinsUsed,
                quota = run.EncoreQuota, spins = run.EncoreSpinsTotal,
                encoreResult = encoreResult,
            };
            outcome.notes.AddRange(notes);
            return RunEvents.One(new RunEvent { type = "ENCORE_SPIN", spin = outcome });
        }

        // 웹 `_mods()`(클리어한 스테이지 기준): buildMods(+ctx) → 패시브 장치 → phaseItems → A2/A8 규칙. 클리어 시점 스냅샷
        // (EncoreStage/SpinsTotal/StageExp/Quota/Boss/BannedSym/PhaseItems/PhasePerks)을 쓰고, 나머지(스택·코인·불운게이지·
        // 퍽·저주·장치)는 웹과 같이 현재 값을 쓴다. 일반 모드 전용이라 심화 훅(ApplyDeepMods)은 필요 없다.
        private static Mods BuildMods(RunState run, int spinIndex)
        {
            var perks = new List<string>(run.Perks);
            perks.AddRange(run.EncorePhasePerks);
            var ctx = new RunCtx
            {
                stage = run.EncoreStage, spinIndex = spinIndex, spinsPerStage = run.EncoreSpinsTotal,
                stageExp = run.EncoreStageExp, quota = run.EncoreQuota,
                growthStack = run.GrowthStack, snowStack = run.SnowStack,
                curseCount = run.Curses.Count, unluckyGauge = run.UnluckyGauge,
                boss = run.EncoreBoss, coins = run.Coins,
            };
            var m = ModsBuilder.Build(run.MachineId, run.CharId, perks, run.Curses, run.Device, ctx, run.PerkLevels);
            m = ModsBuilder.ApplyItemMods(m, run.EncorePhaseItems);
            m = ModsBuilder.ApplyPassiveDevice(m, run.Device);
            AscRunHooks.ApplyRunAscMods(m, run.Asc, run.EncoreBannedSym);
            return m;
        }

        private static string FmtMul(double v)
        {
            string s = Math.Round(v, 2, MidpointRounding.AwayFromZero).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return s.TrimEnd('0').TrimEnd('.');
        }
    }
}
