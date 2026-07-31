using System;
using System.Collections.Generic;
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

        // Kotlin composeStat과 달리 파생키(accountLevel 등)를 미리 얹지 않고 profile.Stats를 "참조"로
        // 그대로 넘긴다(RunController.cs 헤더 "UI 계약 주의" 2번 — 런 중 StatTracker가 같은 딕셔너리를
        // 갱신해야 원본처럼 같은 런 안에서 해금 판정이 최신 상태를 본다). Shop.PerkGate가 쓰는
        // Formulas.AccountLevel(stat)은 achievements 인자 없이 호출되므로(Shop.cs 확인) bestStage/
        // bossClears/runs/bld_*/bc_*/cstage_*/mstage_* 원시 키만으로 충분히 계산되고, lic_*/accountLevel
        // 같은 파생키 사전계산은 필요 없다 — ComposeStat을 매 런마다 새로 만들 필요가 없다.
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
            Controller = new RunController(charId, machineId, deviceId, seed, Profile.Stats);
            StatTracker.Apply(Profile, Controller.State, Controller.LaunchEvents, _scratch);
        }

        // 모든 상호작용의 단일 진입점(RunScreen 전용) — RunController.Do를 감싸 StatTracker 공급 +
        // GAME_OVER 시 기록 갱신(StatTracker.ApplyGameOverTracking이 bestScore/bestStage/runs/totalScore를
        // 이미 갱신)·AchievementEngine.Evaluate·ProfileStore.Save까지 처리한다.
        public IReadOnlyList<RunEvent> Do(RunAction action)
        {
            var events = Controller.Do(action);
            StatTracker.Apply(Profile, Controller.State, events, _scratch);

            LastNewAchievements = Array.Empty<AchDef>();
            bool gameOver = false;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].type == "GAME_OVER") { gameOver = true; break; }
            }

            if (gameOver)
            {
                LastNewAchievements = AchievementEngine.Evaluate(Profile);
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
