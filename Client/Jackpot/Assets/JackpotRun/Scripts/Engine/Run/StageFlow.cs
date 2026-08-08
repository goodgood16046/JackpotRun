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
        public long overPct;          // newExp*100/quota (정수 나눗셈), quota<=0이면 100 — 업적/통계 전용(StatTracker), 점수와 무관.
        // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B / 항목1): 등급은 연출 전용 — 점수에 가산되지 않는다.
        // 웹 ui.js:1684-1698 clearGrade()의 6단계(1~5 + exact=PERFECT)를 한글 라벨로 옮겼다(astral
        // 이모지 금지 — 웹 sub 문구를 그대로 사용). 옛 gradeBonus 필드는 폐기(더 이상 점수에 안 더함).
        public string grade;          // "클리어 성공!".."전설적인 대폭발!!" / "딱 맞춤 — 완벽 클리어!"
        // Opus 검수 반영(2026-08-07) 항목3: 웹 clearGrade()가 반환하는 tier 값 그대로(1~5, PERFECT=6).
        // P4 연출이 grade 문자열을 역파싱하지 않도록 등급을 숫자로도 노출 — grade 문자열은 그대로 유지.
        public int gradeTier;         // 1~5 + PERFECT=6 (웹 ui.js:1685 `tier: 6, key: "perfect"`)
        public long streakBonus;      // Formulas.StreakBonus(clearedStage) — 웹 game.js:1412 streak 가산분
        public long clearScore;       // Formulas.StageClearScore(...) — 웹 stage×50+leftover×2+leftSpins×100+(boss?500:0)
        public long gainedScore;      // inDebt면 0, 아니면 clearScore+streakBonus
        // Opus 검수 반영(2026-08-07): inDebt와 무관하게 항상 CLEAR_COIN+(boss?BOSS_COIN:0)+mods.clearCoinBonus
        // 지급 — 웹 game.js:1416-1420은 gain(점수)만 debt로 0 처리하고 clearCoin 지급줄은 그 조건문
        // 바깥에서 무조건 실행된다(코인은 빚과 무관).
        public long clearCoin;        // CLEAR_COIN+(boss?BOSS_COIN:0)+mods.clearCoinBonus (inDebt 무관, 항상 지급)
        public bool inDebt;
        public List<NodeKind> nodeOptions; // 항상 3개(AUGMENT 필수 + 무작위 2개)
        public bool nextNodeForcedPrism;   // 보스 클리어 직후 = 다음 AUGMENT/RELIC 노드 PRISM 확정(§3-F)

        // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 renderStageClear cs.stageExp/cs.quota/
        // cs.usedSpins/cs.totalSpins) — STAGE_CLEAR 보드의 2바(달성 EXP%·사용 스핀) 표시 전용. run.
        // StageExp/run.Stage는 이 함수 끝의 "상태 반영" 블록에서 이미 리셋/전진되므로 그 이전 값을
        // 별도로 들고 나가야 한다(run 재조회로는 복원 불가).
        public long stageExpAtClear; // outcome.newExp — 클리어 확정 순간의 스테이지 EXP(요구치 이상)
        public long quotaAtClear;    // outcome.quota
        public int usedSpins;        // outcome.newSpinIndex
        public int totalSpins;       // outcome.spins
        public long lastSpinGain;    // outcome.gained — 웹 cs.lastSpinExp(Math.floor(r.lastExpApplied)) 대응
    }

    // 스테이지 실패(폭망) 처리 결과 — 02_service.md §3-C 체인의 귀결.
    public sealed class FailureOutcome
    {
        public string kind; // "FATE_BELL_REVIVE" | "INSURANCE_REVIVE" | "POST_SPIN" | "GAME_OVER"
        public long deficitAtFailure; // quota - newExp (참고용, POST_SPIN/GameOver 메시지 구성에 유용)
        public readonly List<string> manipHints = new List<string>(); // "GAMBLER_REROLL" / "DEVICE:<id>" (POST_SPIN 전용)
        public long finalScore; // kind=="GAME_OVER"일 때만 유효 — §8-A scoreModifier 적용된 최종 점수
        // WEB_PARITY P1 ⑤: 자발적 포기(RunController.GiveUp, 웹 game.js:1228-1231 giveUp(voluntary))로
        // 만든 GAME_OVER면 true — GameOverPanel이 "실패" 프레이밍 대신 "포기 — 즉시 결산" 문구를 쓴다.
        // 만회 수단을 안 써서 도달한 통상 GAME_OVER(kind=="GAME_OVER", RunController.Continue 등)는 false.
        public bool Voluntary;

        // 웹 파리티 P3(#9, 웹 game.js:2618-2622 r.xpGain/r.levelBefore/r.levelAfter) — 런 종료 XP/레벨업
        // 결과. 엔진(Engine/Run)은 Engine/Profile을 참조하지 않으므로(설계 원칙 6) 이 필드는 primitive만
        // 갖고, 실제 계산은 PlayerLevelTracker.ApplyRunEnd(Engine/Profile, GameSession이 GAME_OVER 감지
        // 직후 호출)가 채운다. kind=="GAME_OVER"가 아니면(REVIVE/POST_SPIN) 미사용(전부 기본값 0).
        public long PlayerXpGain;
        public int PlayerLevelBefore;
        public int PlayerLevelAfter;
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
        // 가시성: private → internal (S4, 2026-07-30). 이 파일 하단 "S4 훅" 주석이 "결과를 곧바로
        // StageFlow.ClearStage/HandleFailure로 확정"하라고 S4에 직접 지시하므로, MANIP/도박꾼재굴림
        // (DeviceActions.cs)과 즉시클리어형 아이템(grad_ring/gold_grad_bell, ItemUse.cs)이 스핀을 거치지
        // 않고도 동일한 클리어 보상 계산을 재사용해야 한다. 로직은 한 글자도 바꾸지 않았다 — 접근제한자만
        // 완화(같은 어셈블리 내 다른 Run/*.cs 파일에서 SpinOutcome을 직접 구성해 호출). HandleFailure는
        // MANIP/도박꾼재굴림이 fate_bell/보험증서 회생 체인을 타지 않고 바로 ForceGameOver(이미 public)로
        // 가므로 internal로 열 필요가 없어 private 그대로 둔다.
        internal static ClearOutcome ClearStage(RunState run, SpinOutcome outcome)
        {
            int clearedStage = run.Stage;
            int spins = outcome.spins;
            long quota = outcome.quota;
            long newExp = outcome.newExp;
            int newIdx = outcome.newSpinIndex;
            // Opus 2차검수 필수⑤(2026-08-09) — 웹 cs.lastSpinExp = Math.floor(r.lastExpApplied || 0)
            // 대응. outcome.gained는 벨/즉시클리어 아이템 경로에서 항상 0으로 합성돼(DeviceActions.
            // HandlePostSpinBell 등 result=null/gained=0 SpinOutcome) "+0 EXP 획득"으로 잘못 표시된다 —
            // run.LastGain(=웹 r.lastExpApplied, "마지막 실제 스핀"의 기여분·벨/아이템이 건드리지 않음)을
            // 아래 상태 반영 블록이 0으로 리셋하기 전에 스냅샷해 둔다.
            long lastSpinGainSnapshot = run.LastGain;

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
                ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                run.PhaseItems);
            // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B, 웹 game.js:1419 `C.CLEAR_COIN + (boss ? C.BOSS_COIN : 0)
            // + clearCoinBonus`): 옛 코드(및 kotlin-reference SlotV2Service.kt:843)는 boss일 때 CLEAR_COIN
            // 대신 BOSS_COIN으로 "교체"했지만(삼항 replace), 웹은 보스여도 CLEAR_COIN을 유지한 채
            // BOSS_COIN을 "가산"한다 — 웹 채택 원칙(§0)에 따라 가산식으로 수정(보스 클리어 코인이
            // 12→17로 늘어남).
            // Opus 검수 반영(2026-08-07): inDebt 게이트 제거 — 웹 game.js:1416-1420은 `if
            // (r.debtStages > 0) { gain = 0; ...; debt = true; }` 로 점수(gain)만 0 처리하고, 바로 다음 줄
            // `const clearCoin = ...; r.coins += clearCoin;` 은 그 if문 밖에 있어 debt 여부와 무관하게
            // 항상 실행된다 — 빚문서 상태에서도 클리어 코인은 정상 지급.
            long clearCoin = Formulas.CLEAR_COIN + (boss ? Formulas.BOSS_COIN : 0) + mods.clearCoinBonus;

            // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B / 항목1, 웹 game.js:1412-1418): 클리어 점수 =
            // clearScore(=StageClearScore, 등급보너스/아슬아슬보너스/막판보너스/저주배수 전부 없음) +
            // streakBonus(stage) 뿐이다. 옛 close(300/150/200 턱걸이·막판 보너스)는 웹에 대응 항목이
            // 없어 완전히 제거 — 대신 streakBonus만 그대로 더한다(웹도 이 둘만 더함).
            long streakBonus = Formulas.StreakBonus(clearedStage);
            long gainedScore = inDebt ? 0 : clearScore + streakBonus;

            // overPct(newExp*100/quota, 정수나눗셈)는 StatTracker의 bossOverkillClears/maxOverPct 등
            // 업적 통계 전용 — 점수/등급 계산과 무관하게 그대로 유지한다(작업 지시: 통계는 기존대로 집계).
            long overPct = quota > 0 ? newExp * 100 / quota : 100;

            // 웹 파리티 P2(웹 ui.js:1684-1698 clearGrade): 등급은 순수 연출용 — 점수에 가산되지 않는다.
            // exact(leftover==0)=PERFECT(웹 tier:6). 그 외는 초과율(leftover/quota, newExp가 아니라
            // leftover 기준인 점에 주의 — overPct 필드와는 다른 계산식)로 tier 1~5, 보스는 +1단계(5 상한).
            // astral 이모지 금지 — 웹 sub 문구(한글, ui.js 그대로)를 라벨로 사용.
            // Opus 검수 반영(2026-08-07) 항목3: gradeTier를 웹 clearGrade()의 반환 tier 값 그대로 노출
            // (1~5, PERFECT=6) — P4 연출 코드가 grade 문자열을 역파싱해 등급을 알아내지 않도록 선행 정리.
            int gradeTier;
            string grade;
            if (leftover == 0)
            {
                gradeTier = 6; // 웹 ui.js:1685 `{ tier: 6, key: "perfect", ... }`
                grade = "딱 맞춤 — 완벽 클리어!";
            }
            else
            {
                double overExcessPct = quota > 0 ? leftover / (double)quota * 100.0 : 0.0;
                int tier = overExcessPct < 20.0 ? 1 : overExcessPct < 50.0 ? 2 : overExcessPct < 100.0 ? 3 : overExcessPct < 200.0 ? 4 : 5;
                if (boss) tier = Math.Min(5, tier + 1);
                gradeTier = tier;
                grade = tier switch
                {
                    1 => "클리어 성공!",
                    2 => "훌륭한 클리어!",
                    3 => "엄청난 초과 달성!",
                    4 => "압도적인 오버킬!",
                    _ => "전설적인 대폭발!!",
                };
            }

            int nextStage = clearedStage + 1;

            var nodes = RollNextNodes(run.Rng, nextStage);

            // WEB_PARITY P1 ④: 보스 클리어 → 장치 드랍 + DEVICE 노드 추가(웹 game.js:1438 `if (boss) {
            // const d = E.pickDevices(...)[0]; if (d) drops.push(d); }` + game.js:1499
            // `if (drops.length) nodes.push("DEVICE")`). AUGMENT+무작위2에 "추가"되는 4번째 옵션이라
            // 3택 규칙을 건드리지 않는다 — 미보유 장치가 하나도 없으면(전부 보유) 드랍 자체가 없어
            // 옵션은 그대로 3개.
            // Opus 1차검수 수정③(2026-08-07): rare 가중 추첨(NodeEvents.PickDevice, 웹 pickDevices
            // ±L1296-1309 이식) — clearedStage(=run.Stage, 아직 nextStage로 갱신 전) 기준.
            string deviceDrop = "";
            if (boss)
            {
                var picked = NodeEvents.PickDevice(run.Rng, run.Stage, run.OwnedDeviceIds);
                if (picked != null)
                {
                    deviceDrop = picked.id;
                    nodes.Add(NodeKind.Device);
                }
            }

            // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12, 웹 game.js:1501-1507) — 증강 레벨업 노드
            // 확률(기본10%+pity, 상한20%). 레벨업 가능한 보유 증강(<Lv3)이 있을 때만 AUGMENT 노드를
            // AUGLEVEL로 교체한다(3택 개수는 그대로 — DEVICE처럼 "추가" 옵션이 아니라 "대체"). 촉매/
            // 형광펜 부스트는 해당 아이템이 Unity에 없어 run.AugLevelBoost가 항상 0인 후크로만 존재.
            if (AugLevels.LevelableHeld(run).Count > 0)
            {
                double chance = Math.Min(0.6, run.AugLevelChance + run.AugLevelBoost);
                if (run.Rng.NextDouble() < chance)
                {
                    int augIdx = nodes.IndexOf(NodeKind.Augment);
                    if (augIdx >= 0) nodes[augIdx] = NodeKind.AugLevel;
                    run.AugLevelChance = 0.10;
                }
                else
                {
                    run.AugLevelChance = Math.Min(0.20, run.AugLevelChance + 0.02);
                }
                run.AugLevelBoost = 0.0; // 촉매는 1회성(다음 기회에 소진, 웹 game.js:1506과 동일)
            }

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
            run.RunBossClears += boss ? 1 : 0; // 웹 파리티 P3(#9) — 웹 game.js:1421 r.stats.bossClears 대응
            run.GrowthStack = newGrowthStack;
            run.SnowStack = newSnowStack;
            run.NodeOptions.Clear();
            run.NodeOptions.AddRange(nodes);
            run.PendingDeviceDrop = deviceDrop;
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
                gradeTier = gradeTier,
                streakBonus = streakBonus,
                clearScore = clearScore,
                gainedScore = gainedScore,
                clearCoin = clearCoin,
                inDebt = inDebt,
                nodeOptions = nodes,
                nextNodeForcedPrism = boss, // §3-F: bossClear = clearedStage%5==0 → 다음 AUGMENT/RELIC PRISM 확정
                stageExpAtClear = newExp,
                quotaAtClear = quota,
                usedSpins = newIdx,
                totalSpins = spins,
                lastSpinGain = lastSpinGainSnapshot,
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
        // WEB_PARITY P1 ③ (2026-08-07): 웹 순서로 재배열(game.js:1146-1157) — 기존 Unity 순서
        // (①fate_bell ②보험증서 ③POST_SPIN)를 ①보험증서 ②POST_SPIN(_canRecover 상당, dev_bell 조건
        // 신설) ③fate_bell ④게임오버로 뒤집었다. 보험과 fate_bell을 둘 다 보유한 채 실패하면 이제
        // 보험이 먼저 소진된다(Tests_RunNet.cs 어서션으로 고정).
        private static FailureOutcome HandleFailure(RunState run, SpinOutcome outcome)
        {
            long deficit = outcome.quota - outcome.newExp;

            // 1) 보험증서 — 1회용, 스핀+2 (웹 game.js:1147, 체인 1번째).
            if (run.Survive)
            {
                run.Survive = false;
                run.StageBonusSpins += 2;
                return new FailureOutcome { kind = "INSURANCE_REVIVE", deficitAtFailure = deficit };
            }

            // 2) 만회 기회(POST_SPIN) — MANIP 장치 또는 도박꾼 무료재굴림(이번 스테이지 미사용) 또는
            //    dev_bell 보유&부족≤25(웹 _canRecover, game.js:1205-1211 — bellReady 조건 신설분).
            //    [설계 계약 결손 — 보고 대상] ContentTypes.cs의 DeviceDef엔 Kotlin Device.cmd 필드가 없다
            //    (Devices.cs 헤더 주석 확인 — "cmd/needsArg/cooldown은 DeviceDef 계약에 필드가 없어 다루지
            //    않는다"). Kotlin은 `dev.cmd !in usedCmds`로 스테이지당 1회를 검사하지만, cmd 문자열이
            //    없어 대신 dev.id를 마커로 쓴다(스테이지당 1회라는 동작 자체는 동일 — 실제 사용 표시는
            //    S4 DeviceActions.cs가 이 마커 규약을 그대로 따라야 함).
            var dev = Devices.ById(run.Device);
            bool manipAvail = dev != null && dev.kind == "MANIP" && !run.UsedCmds.Contains(dev.id);
            bool gamblerReroll = run.CharId == "gambler" && !run.UsedCmds.Contains("GREROL");
            bool bellReady = run.Device == "dev_bell" && deficit <= 25;

            if (manipAvail || gamblerReroll || bellReady)
            {
                run.Phase = RunPhase.PostSpin;
                var f = new FailureOutcome { kind = "POST_SPIN", deficitAtFailure = deficit };
                if (gamblerReroll) f.manipHints.Add("GAMBLER_REROLL");
                if (manipAvail) f.manipHints.Add("DEVICE:" + dev.id);
                // [통합 결손 — 보고 대상] dev_bell은 kind=="INSTANT"(ARMED류)라 DeviceActions.Handle의
                // POST_SPIN 분기(kind!="MANIP"이면 거부)를 그대로는 통과하지 못한다 — 이 조건 추가는
                // "_canRecover 상당" 게이트만 웹과 맞춘 것이고, POST_SPIN 진입 후 dev_bell을 실제로
                // 소비하는 액션(웹 emergencyBell(), 항상 즉시클리어)은 이번 P1 범위 밖이라 아직 없다.
                if (bellReady) f.manipHints.Add("DEVICE:dev_bell");
                return f;
            }

            // 3) 운명의종 — 런 1회, 부족분 <= 15면 자동 스핀+1, 스핀 소진 없이 계속(SPIN 유지)
            //    (웹 game.js:1149, 체인 3번째로 하향).
            if (deficit <= 15 && !run.FateBellUsed && run.Perks.Contains("fate_bell"))
            {
                run.FateBellUsed = true;
                run.StageBonusSpins += 1;
                return new FailureOutcome { kind = "FATE_BELL_REVIVE", deficitAtFailure = deficit };
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
