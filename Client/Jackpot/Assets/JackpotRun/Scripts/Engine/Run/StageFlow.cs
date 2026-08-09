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
        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:1395-1397) — A10 승천 최종보스(15)
        // 1페이즈 클리어: 진짜 클리어가 아니라 요구치↑ 후 같은 스테이지 재시작(StageFlow.ClearStage의
        // BossPhase2Restart 분기). 보상/노드/카운터 전부 미반영 — SPIN 페이즈 그대로 유지.
        BossPhase2,
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

        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:1395-1397) — true면 A10 2페이즈 보스
        // 재시작(진짜 클리어 아님). true일 때는 이 필드를 제외한 나머지 전부 기본값(0/false/null)이다 —
        // 호출측은 이 플래그부터 확인할 것(StageFlow.BuildClearEvent가 이미 이 분기를 대신 처리해 준다).
        public bool bossPhase2Restart;

        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스 — 자동 소멸, 웹 game.js:1516-1542
        // `clearSummary.decayBanner`) — 이번 클리어에 자동 소멸 예고/발동이 있었으면 안내 문구, 없으면
        // 빈 문자열. UI(STAGE_CLEAR 보드, P7-4)가 그대로 표시하면 된다.
        public string decayBanner = "";
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
                // 웹 파리티 P6 — A10 2페이즈 보스 재시작은 진짜 클리어가 아니다(위 ClearStage 헤더 주석).
                if (clear.bossPhase2Restart)
                    return new SpinStepResult { kind = SpinStepKind.BossPhase2, spin = outcome, clear = clear };
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

            // ── 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:1395-1397) ──────────────────
            // A10 승천: 최종보스(스테이지15) 1페이즈 클리어는 진짜 클리어가 아니라 요구치↑ 후 같은
            // 스테이지 재시작이다 — 웹 `_clearStage()`가 이 시점에 곧장 `_beginStage()`를 다시 호출하고
            // 리턴하는 것과 동일 파리티(점수/코인/노드/카운터 전부 미반영, 웹 원문에 그런 코드 자체가
            // 없다). Unity는 스핀수/요구치를 스테이지마다 캐시하지 않고 매번 SpinResolver.EffSpins/
            // QuotaOf로 즉석 계산하므로(StageFlow.ClearStage 헤더 주석) `_beginStage()`가 하던 일 중
            // "다시 계산해 둬야 하는" 부분은 없다 — 여기서는 웹이 실제로 리셋하는 "스테이지 시작" 휘발성
            // 필드만 그대로 되돌리면 된다(Unity가 다음 스테이지 진입 시 이미 리셋하는 필드들과 동일
            // 부분집합 — 아래 참조). Score/Coins/Stage/NodeOptions/DebtStages/GrowthStack/SnowStack/
            // RunBossClears 등 "진짜 클리어"에서만 갱신되는 상태는 전혀 건드리지 않는다.
            if (clearedStage == 15 && run.Asc >= 10 && !run.BossPhase2)
            {
                run.BossPhase2 = true;
                run.SpinIndex = 0;
                run.StageExp = 0;
                run.FlameNext = false;
                run.SeedNext = false;
                run.ArmItems.Clear();
                run.PhaseItems.Clear();
                run.StageBonusSpins = 0;
                run.UsedCmds.RemoveWhere(cmd => cmd != "RUNSHOP" && cmd != "RUNORACLE");
                run.PhasePerks.Clear();
                run.LastCells.Clear();
                run.LastGain = 0; run.LastScoreGain = 0; run.LastCoinGain = 0; run.LastSpinNo = -1;
                run.PendingNextExpMul = 1.0;
                run.LockedNext.Clear();
                AscRunHooks.RollBannedSym(run); // 웹 _beginStage() A8 재롤(game.js:425) 그대로.
                run.Phase = RunPhase.Spin;
                return new ClearOutcome { clearedStage = clearedStage, bossPhase2Restart = true };
            }

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

            // 웹 파리티 P6(웹 game.js:1401 `if (stage === 15) { r.graduatedThisRun = true; r._bossPhase2 =
            // false; }`) — 스테이지15 클리어(2페이즈면 여기 도달한 시점=2페이즈 완료) = 졸업 확정.
            // StatTracker.ApplyGameOverTracking이 이 플래그로 PlayerProfile.AscMax/BestAscScore/
            // BestAscLevel/Mastery.AscMax를 런 종료 시점에 갱신한다.
            if (clearedStage == 15) { run.GraduatedThisRun = true; run.BossPhase2 = false; }

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

            // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스, 웹 game.js:1439-1494) — 심화
            // 런(DeepMode)은 완전히 다른 노드 풀(POUCH 고정 + SYMAUG/SYMREL/dpool)을 쓴다. 일반 런은
            // 기존 RollNextNodes 그대로(무회귀). Opus 2차검수 [웹 정합] — RollDeepNodes의 stage 게이트는
            // 방금 클리어한 스테이지(clearedStage) 기준(웹 `_clearStage()`의 `stage`, 재대입 전) —
            // nextStage를 넘기면 JACKPOT/CURSE/RISK 등장이 웹보다 1스테이지 앞당겨진다(정정 근거는
            // RollDeepNodes 헤더 주석 참조).
            var nodes = run.DeepMode ? RollDeepNodes(run, clearedStage, boss) : RollNextNodes(run.Rng, nextStage);

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
            // 심화 런은 RollDeepNodes가 SYMAUG 슬롯을 대상으로 동일 로직을 이미 처리했다(무회귀).
            if (!run.DeepMode && AugLevels.LevelableHeld(run).Count > 0)
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

            // 웹 파리티 P7-3(§3 V3P3, 웹 game.js:1516-1542) — 심화 자동 소멸. clearedStage==14 클리어
            // 시(=다음 스테이지 15 진입 직전) 예고 1회, clearedStage>=15부터 클리어마다 기본 이득 심볼
            // (cat=base && !harmful — 해골/빈칸/저주/특수 제외) 1개 무작위 제거. DECK_MIN 미만도 허용
            // (소멸 전용 경로, 압박 의도). 대상 0개면 스킵(특수 덱 완성 상태).
            string decayBanner = "";
            if (run.DeepMode)
            {
                if (clearedStage == 14 && !run.DecayForewarned)
                {
                    run.DecayForewarned = true;
                    decayBanner = "다음 스테이지부터 기본 이득 심볼이 매 클리어 1개씩 사라집니다";
                }
                else if (clearedStage >= 15)
                {
                    var decayTargets = new List<string>();
                    foreach (var kv in run.Pouch)
                        if (kv.Value > 0 && Pouch.IsAutoDecayTarget(kv.Key))
                            for (int k = 0; k < kv.Value; k++) decayTargets.Add(kv.Key);
                    if (decayTargets.Count > 0)
                    {
                        string picked = decayTargets[run.Rng.Next(decayTargets.Count)];
                        var symInfo = Symbols.ById(picked);
                        int cur = run.Pouch.TryGetValue(picked, out var pc) ? pc : 0;
                        if (cur <= 1) run.Pouch.Remove(picked); else run.Pouch[picked] = cur - 1;
                        DeepRunHooks.CheckArchetypeChange(run); // run.DeepArchFamily/Tier 스냅샷만 갱신(이벤트 채널 없음 — 클리어 경로는 단일 ClearOutcome 반환이라 부가 이벤트를 못 실어보낸다, 다음 정비/스핀에서 자연 재평가됨)
                        decayBanner = $"심화 압력 — 기본 심볼이 낡아 사라졌습니다: {(symInfo != null ? symInfo.emoji + symInfo.name : picked)} 1개 제거 (해로운 심볼은 남습니다)";
                        if (run.DeepStats != null) run.DeepStats.AutoDecays += 1;
                    }
                }
                // §9.2 J3 스테이지 1회 제한 플래그 초기화(리치표식/재도전릴/잭팟왕관 — 스테이지마다 재사용 허용, 웹 game.js:1544-1549).
                run.ReachMarkUsed = false;
                run.RetryReelUsed = false;
                run.JackpotCrownUsed = false;
            }

            // ── 상태 반영 (Kotlin clearStage L872-892) ──
            run.Score = outcome.newScore + gainedScore;
            run.Coins = outcome.newCoins + clearCoin;
            run.Stage = nextStage;
            run.SpinIndex = 0;
            run.StageExp = 0;
            run.FlameNext = false;
            run.SeedNext = false;
            // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — 다음 스테이지 진입(_beginStage() 대응)마다
            // A8 금지 심볼을 재롤(asc<8이면 항상 ""로 리셋).
            AscRunHooks.RollBannedSym(run);
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
                decayBanner = decayBanner,
            };
        }

        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — ClearStage(run, outcome) 호출 뒤 RunEvent를 짓는
        // 공용 헬퍼. clear.bossPhase2Restart면 "STAGE_CLEARED" 대신 "BOSS_PHASE2"(진짜 클리어 아님 —
        // StatTracker.ApplyOne이 클리어 통계를 건너뛰도록 하는 신호)를 낸다. StageFlow.ProcessSpin(주
        // 스핀 경로)은 SpinStepResult.kind로 이미 분기하므로 이 헬퍼를 쓰지 않는다 — DeviceActions.cs/
        // ItemUse.cs의 "스핀을 거치지 않고 직접 ClearStage를 호출"하는 3개 호출부(MANIP·도박꾼재굴림·
        // dev_bell/즉시클리어 아이템) 전용.
        public static RunEvent BuildClearEvent(SpinOutcome outcome, ClearOutcome clear, string deviceId = null)
        {
            if (clear.bossPhase2Restart)
                return new RunEvent { type = "BOSS_PHASE2", spin = outcome, clear = clear, deviceId = deviceId };
            return new RunEvent { type = "STAGE_CLEARED", spin = outcome, clear = clear, deviceId = deviceId };
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

        // ══════════════════════════════════════════════════════════════════════
        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스 — 심화 노드 풀, 웹 game.js:1439-1494)
        // ══════════════════════════════════════════════════════════════════════
        // POUCH 고정 + second(SYMAUG 40%/SYMREL 20%(보스35%)/dpool 셔플 1장) + third(dpool 비중복 1장,
        // second가 SYMAUG/SYMREL이면 dsh[0], dpool 출신이면 dsh[1]) — dpool = SHOP/REST/GAMBLE/EVENT
        // 상시 + stage>=6 CURSE/RISK + stage>=3 JACKPOT + 심볼퍽 shopLabWeight(연구실중독/연구실열쇠)만큼
        // SHOP 추가(최대 4장). SYMAUG 슬롯 + 레벨업 가능 보유증강 있으면 확률로 AUGLEVEL(증강 레벨업)
        // 교체(일반 런과 동일 pity 공식, AUGMENT 대신 SYMAUG 슬롯 대상). sp_deckslot(alwaysRepair)은
        // SHOP 노드 보장(중복 방지, "추가" 옵션). dev_call_bell(연구실호출벨 — 심화 전용 신규 장치,
        // Unity Devices.cs 미이식 — §2 P3-2 결정 로그 "심화 9건 대응 장치 없음" 참조)은 아직 장착
        // 불가능한 id라 이 조건은 현재 항상 false(향후 장치 이식 시 자동 활성화, 웹 정확 전사 유지).
        //
        // Opus 2차검수(P7-3, 2026-08-09) [웹 정합] — stage 게이트 기준 정정: 웹 `_clearStage()`의
        // `stage`는 방금 클리어한 스테이지(=이 함수 호출 시점의 `clearedStage`, `r.stage = stage+1`
        // 재대입 *이전* 값 — pickDevices(rng, stage, ...)도 동일 `stage`를 쓰는 것과 같은 근거)다.
        // 1차 구현은 실수로 `nextStage`(clearedStage+1)를 넘겨 JACKPOT/CURSE/RISK 등장 시점이 웹보다
        // 1스테이지 앞당겨져 있었다 — 호출부를 `clearedStage`로 정정. 일반 런 `RollNextNodes`가 이미
        // `nextStage` 관례를 쓰고 있는 것은 이번에 건드리지 않는다(별도 기존 이탈, §2-(CC) 잔여 이탈
        // 항목으로 기재 — 그쪽은 이 슬라이스 범위 밖).
        private static List<NodeKind> RollDeepNodes(RunState run, int clearedStage, bool boss)
        {
            var sp = SymPerks.ComputeMods(run.Perks, run.Pouch, run.PerkLevels);
            var dpool = new List<NodeKind> { NodeKind.Shop, NodeKind.Rest, NodeKind.Gamble, NodeKind.Event };
            if (clearedStage >= 6) { dpool.Add(NodeKind.Curse); dpool.Add(NodeKind.Risk); }
            if (clearedStage >= 3) dpool.Add(NodeKind.Jackpot);
            if (sp.ShopLabWeight > 0)
            {
                int extra = Math.Min(4, (int)sp.ShopLabWeight);
                for (int k = 0; k < extra; k++) dpool.Add(NodeKind.Shop);
            }
            var dsh = new List<NodeKind>(dpool);
            run.Rng.Shuffle(dsh);

            NodeKind second;
            double roll = run.Rng.NextDouble();
            double relThresh = boss ? 0.35 : 0.20;
            if (roll < 0.40) second = NodeKind.SymAug;
            else if (roll < 0.40 + relThresh) second = NodeKind.SymRel;
            else second = dsh[0];

            bool secondFromPool = second != NodeKind.SymAug && second != NodeKind.SymRel;
            NodeKind? thirdCandidate = secondFromPool
                ? (dsh.Count > 1 ? dsh[1] : (NodeKind?)null)
                : (dsh.Count > 0 ? dsh[0] : (NodeKind?)null);

            var nodes = new List<NodeKind> { NodeKind.Pouch, second };
            if (thirdCandidate.HasValue && thirdCandidate.Value != second) nodes.Add(thirdCandidate.Value);

            if (second == NodeKind.SymAug && AugLevels.LevelableHeld(run).Count > 0)
            {
                double chance = Math.Min(0.6, run.AugLevelChance + run.AugLevelBoost);
                if (run.Rng.NextDouble() < chance)
                {
                    int idx = nodes.IndexOf(NodeKind.SymAug);
                    if (idx >= 0) nodes[idx] = NodeKind.AugLevel;
                    run.AugLevelChance = 0.10;
                }
                else
                {
                    run.AugLevelChance = Math.Min(0.20, run.AugLevelChance + 0.02);
                    // 웹 파리티 P7-3b(WEB_PARITY_DESIGN.md §1-A #19 "Sp 신규 51종") — 🧷안전핀노트
                    // (safepin·fuse), 웹 game.js:1478-1484. AUGLEVEL 미발생 시(이 else 분기) 이번
                    // 스테이지 safepin이 등장했었다면(run.SafePinActive) pity에 +1%p 추가 누적 후 소비.
                    if (run.Pouch.TryGetValue("safepin", out var spn) && spn > 0 && run.SafePinActive)
                    {
                        run.AugLevelChance = Math.Min(0.20, run.AugLevelChance + 0.01);
                        run.Pouch["safepin"] = spn - 1;
                        if (run.Pouch["safepin"] <= 0) run.Pouch.Remove("safepin");
                        // Opus 2차검수(P7-3b) [LOW 일괄] — 웹 game.js:1482 `this._checkArchetype();` 그대로.
                        DeepRunHooks.CheckArchetypeChange(run);
                    }
                }
                run.AugLevelBoost = 0.0;
            }
            // 웹 game.js:1488 `r._safePinActive = false;` — second==SYMAUG 분기 밖(스테이지마다 항상)
            // 무조건 리셋.
            run.SafePinActive = false;

            if (sp.AlwaysRepair && !nodes.Contains(NodeKind.Shop)) nodes.Add(NodeKind.Shop);
            if (boss && run.Device == "dev_call_bell" && !nodes.Contains(NodeKind.Shop)) nodes.Add(NodeKind.Shop);

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
        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:2549-2551 `const mod =
        // E.scoreModifier(...); const am = ascMods(r.asc); const finalScore = Math.floor(r.score * mod *
        // am.scoreMul);`) — 승천 점수 보정(×(1+0.12a))을 여기서 곱한다. asc=0이면 am.scoreMul==1.0
        // (정확히 1.0, 곱해도 무변화)이라 기존 asc 미도입 시절 결과와 완전히 동일하다.
        public static FailureOutcome ForceGameOver(RunState run, long deficitAtFailure)
        {
            run.Phase = RunPhase.GameOver;
            long finalScore = (long)(run.Score * ScoreModifierFor(run.MachineId, run.CharId) * AscMods.Get(run.Asc).ScoreMul);
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
