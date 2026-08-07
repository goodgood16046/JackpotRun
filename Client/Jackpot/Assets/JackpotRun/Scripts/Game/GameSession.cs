using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.Game
{
    // 런 수명주기 접착 계층 — ENGINE_PORT_DESIGN.md S6. RunController(순수 엔진, JackpotRun.Engine)와
    // PlayerProfile(S5 저장 어댑터, ProfileStore.cs)을 하나로 묶어 RunScreen(UI)이 "액션 보내고 이벤트
    // 받기"만 신경 쓰면 되게 한다. UnityEngine 의존은 ProfileStore.Load/Save 경유뿐이고, 이 파일 자체는
    // System.DateTime만 사용한다(엔진은 순수 유지 — 설계 원칙 6, seed는 이 파일에서만 시각 기반 생성).
    public sealed class GameSession
    {
        public RunController Controller { get; }

        // Kotlin composeStat과 달리 파생키를 미리 얹지 않고 profile.Stats를 "참조"로 그대로 넘긴다
        // (RunController.cs 헤더 "UI 계약 주의" 2번 — 런 중 StatTracker가 같은 딕셔너리를 갱신해야
        // 원본처럼 같은 런 안에서 해금 판정이 최신 상태를 본다). Shop.PerkGate가 쓰는
        // Formulas.AccountLevel(stat)은 achievements 인자 없이 호출되므로(Shop.cs 확인) bestStage/
        // bossClears/runs/bld_*/bc_*/cstage_*/mstage_* 원시 키만으로 충분히 계산된다 — ComposeStat이
        // 얹는 유일한 파생키(distinctCharS10, WEB_PARITY P3-2로 lic_dev_*/bldCat_*/accountLevel은
        // 제거됨, AchievementEngine.cs 헤더 각주 참조)의 사전계산도 필요 없어 ComposeStat을 매 런마다
        // 새로 만들 필요가 없다.
        public PlayerProfile Profile { get; }

        public RunState State => Controller.State;

        // GAME_OVER 직후 AchievementEngine.Evaluate가 반환한 신규 달성 업적 — 그 외 시점엔 항상 빈 리스트
        // (GameOverPanel이 "새 업적 목록"을 보여줄 때 참조).
        public IReadOnlyList<AchDef> LastNewAchievements { get; private set; } = Array.Empty<AchDef>();

        private readonly StatTracker.RunScratch _scratch = new StatTracker.RunScratch();

        public GameSession(string charId, string machineId, string deviceId)
        {
            Profile = ProfileStore.Load();
            // seed는 이 파일(Unity 어댑터)에서만 시각 기반 생성 — 엔진(RunController/Rng)은 순수 유지
            // (ENGINE_PORT_DESIGN.md S6 지시 "seed=현재틱").
            long seed = DateTime.UtcNow.Ticks;
            // WEB_PARITY P1 ④ Opus 1차검수 수정④(2026-08-07): ownedDeviceIds — RunState.OwnedDeviceIds를
            // 시드할 때 Profile.OwnedDevices(영구지급분)만이 아니라 Profile.UnlockedDevices()(=OwnedDevices
            // ∪ 업적으로 해금된 장치, PlayerProfile.IsDeviceUnlocked 참조)를 기준으로 삼는다 —
            // 장치 해금의 진짜 2원 판정과 일치시켜, 업적으로만 해금되고 아직 OwnedDevices에 등록되지 않은
            // 장치까지 "미보유 추첨 풀"에서 정확히 제외한다(안 그러면 이미 쓸 수 있는 장치가 EVENT-6/
            // DEVICE 노드에서 다시 뽑히는 허탕이 날 수 있다).
            Controller = new RunController(charId, machineId, deviceId, seed, Profile.Stats, deviceId2: "",
                ownedDeviceIds: Profile.UnlockedDevices().Select(d => d.id).ToList());
            StatTracker.Apply(Profile, Controller.State, Controller.LaunchEvents, _scratch);
        }

        // 모든 상호작용의 단일 진입점(RunScreen 전용) — RunController.Do를 감싸 StatTracker 공급 +
        // GAME_OVER 시 기록 갱신(StatTracker.ApplyGameOverTracking이 bestScore/bestStage/runs/totalScore를
        // 이미 갱신)·AchievementEngine.Evaluate·PlayerLevelTracker.ApplyRunEnd(웹 파리티 P3 #9, 런종료
        // XP/레벨업)·ProfileStore.Save까지 처리한다.
        public IReadOnlyList<RunEvent> Do(RunAction action) => FinishAction(Controller.Do(action));

        // WEB_PARITY P1 ⑤: RunController.GiveUp()은 RunAction이 아니라 별도 공개 메서드(자발적
        // 포기 — SPIN/POST_SPIN 어느 시점이든 즉시 결산)라 Do(RunAction)를 그대로 못 태운다. 그래도
        // 결과 후처리(StatTracker 반영·업적 평가·저장)는 Do(...)와 동일해야 하므로 같은 FinishAction을
        // 공유한다.
        public IReadOnlyList<RunEvent> DoGiveUp() => FinishAction(Controller.GiveUp());

        private IReadOnlyList<RunEvent> FinishAction(IReadOnlyList<RunEvent> events)
        {
            StatTracker.Apply(Profile, Controller.State, events, _scratch);

            LastNewAchievements = Array.Empty<AchDef>();
            RunEvent gameOverEvent = null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].type == "GAME_OVER") { gameOverEvent = events[i]; break; }
            }

            if (gameOverEvent != null)
            {
                LastNewAchievements = AchievementEngine.Evaluate(Profile);
                // 웹 파리티 P3(#9, 웹 game.js:2617-2625) — StatTracker.Apply(카운터)→AchievementEngine.
                // Evaluate(신규업적수 확정) 다음 순서로 런 종료 XP를 부여한다(웹과 동일 순서). 자발적
                // 포기(FailureOutcome.Voluntary)도 웹처럼 예외 없이 XP를 받는다.
                PlayerLevelTracker.ApplyRunEnd(Profile, Controller.State, gameOverEvent.failure, LastNewAchievements.Count);
                ProfileStore.Save(Profile);
            }

            return events;
        }

        // ── HUD 미리보기 전용(진행바/남은 스핀 표시) — 실제 스핀 판정에는 전혀 관여하지 않는다.
        // ItemUse.InstantQuota/StageFlow.ClearStage의 clearCoinBonus 계산과 동일한 근사 패턴(device+
        // phasePerks는 반영하되 RunCtx 조건부 신규 증강 8종은 미반영) — 정확한 값은 SpinResolver.
        // ResolveSpin 내부(3단계 mods 재계산)에서만 산출되고 여기서는 재현하지 않는다.
        public (long quota, int spins) PreviewQuotaSpins()
        {
            var run = State;
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var mods = ModsBuilder.ApplyItemMods(
                ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device),
                run.PhaseItems);
            long quota = SpinResolver.QuotaOf(run.Stage, mods);
            int spins = SpinResolver.EffSpins(run, mods);
            return (quota, spins);
        }
    }
}
