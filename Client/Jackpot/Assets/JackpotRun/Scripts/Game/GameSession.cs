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
        // bossClears/runs/bld_*/bc_*/cstage_*/mstage_* 원시 키만으로 충분히 계산된다 — 웹 파리티
        // P8(WEB_PARITY_DESIGN.md §2-(HH))로 `AchievementEngine.ComposeStat`이 얹던 마지막 파생키
        // (distinctCharS10)마저 소비처 없는 죽은 계산으로 확인돼 제거됐다 — ComposeStat은 이제
        // `profile.Stats`를 그대로 복사만 하는 항등 변환이라(파생키 자체가 0개), 매 런마다 새로
        // 만들 필요가 원천적으로 없다(AchievementEngine.cs 헤더 각주 참조).
        public PlayerProfile Profile { get; }

        public RunState State => Controller.State;

        // GAME_OVER 직후 AchievementEngine.Evaluate가 반환한 신규 달성 업적 — 그 외 시점엔 항상 빈 리스트
        // (GameOverPanel이 "새 업적 목록"을 보여줄 때 참조).
        public IReadOnlyList<AchDef> LastNewAchievements { get; private set; } = Array.Empty<AchDef>();

        private readonly StatTracker.RunScratch _scratch = new StatTracker.RunScratch();

        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:285-289 startRun(asc,deep)) — asc:
        // 요청된 승천 단계. 해금 상한 클램프(maxAsc = profile.ascMax+1)는 Engine/Profile을 아는 이
        // 어댑터 계층이 담당 — RunController는 [0,ASC_MAX] 방어적 재클램프만 한다(설계 원칙 6).
        // 웹 파리티 P7-1(WEB_PARITY_DESIGN.md §1-A #19, 웹 game.js:285-289 startRun) — deep: 심화모드
        // 진입 여부. "wantDeep이면 asc 강제 0"(useAsc 계산에서 직접 처리 — 웹과 동일 지점) —
        // RunController 생성자도 방어적으로 동일 강제를 한 번 더 하므로(§RunController.cs 헤더 각주)
        // 이중 안전망이다. UI(MenuView 게임모드 선택기)는 아직 심화를 잠가 두므로(P7-4) 이 매개변수는
        // 현재 테스트/향후 UI 배선 진입점으로만 쓰인다.
        public GameSession(string charId, string machineId, string deviceId, int asc = 0, bool deep = false)
        {
            Profile = ProfileStore.Load();
            // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #9/#13, game.js:179 `this._grantLevelDevices()`) —
            // 로드 직후 후반 장치 자동 지급(멱등). Stats["playerLevel"]도 여기서 현재값으로 스냅샷해
            // Shop.PerkUnlocked(퍽 레벨 게이트)가 이번 런 내내 최신 레벨을 보게 한다 — 이 스냅샷은
            // StatTracker.ApplyGameOverTracking이 이번 런 종료 시 다시 "레벨업 직전" 값으로 되돌려
            // 쓰므로(업적 lv20/lv40용 1런 지연 의미는 그대로 보존, StatTracker.cs 헤더 각주 참조) 여기서
            // 미리 갱신해도 그 시점의 기존 동작과 충돌하지 않는다.
            Profile.SetStat("playerLevel", Profile.PlayerLevel);
            Profile.GrantLevelDevices();
            // seed는 이 파일(Unity 어댑터)에서만 시각 기반 생성 — 엔진(RunController/Rng)은 순수 유지
            // (ENGINE_PORT_DESIGN.md S6 지시 "seed=현재틱").
            long seed = DateTime.UtcNow.Ticks;
            // WEB_PARITY P1 ④ Opus 1차검수 수정④(2026-08-07): ownedDeviceIds — RunState.OwnedDeviceIds를
            // 시드할 때 Profile.OwnedDevices(영구지급분)만이 아니라 Profile.UnlockedDevices()(=OwnedDevices
            // ∪ 업적으로 해금된 장치, PlayerProfile.IsDeviceUnlocked 참조)를 기준으로 삼는다 —
            // 장치 해금의 진짜 2원 판정과 일치시켜, 업적으로만 해금되고 아직 OwnedDevices에 등록되지 않은
            // 장치까지 "미보유 추첨 풀"에서 정확히 제외한다(안 그러면 이미 쓸 수 있는 장치가 EVENT-6/
            // DEVICE 노드에서 다시 뽑히는 허탕이 날 수 있다).
            // 웹 game.js:288-289 `maxAsc = Math.max(0,(profile.ascMax??-1)+1); useAsc = Math.max(0,
            // Math.min(maxAsc, Math.floor(asc||0)))`.
            int maxAsc = Profile.MaxPlayableAsc();
            int useAsc = deep ? 0 : Math.Max(0, Math.Min(maxAsc, asc));
            // 웹 파리티 P7-4(WEB_PARITY_DESIGN.md §1-A #19/#20) — profile.symUnlocked 실해금 배선.
            // Profile.EffectiveSymUnlocked()(=Pouch.DefaultUnlocked 58종 ∪ 업적으로 해금된 추가분)을
            // RunState.SymUnlocked 미러로 넘긴다 — 일반 런에서도 항상 채워 두지만(가벼운 계산) 실제로
            // 읽는 곳은 심화 런의 POUCH 오퍼/3스테이지 연계 보너스뿐이다.
            Controller = new RunController(charId, machineId, deviceId, seed, Profile.Stats, deviceId2: "",
                ownedDeviceIds: Profile.UnlockedDevices().Select(d => d.id).ToList(), asc: useAsc, deep: deep,
                symUnlocked: Profile.EffectiveSymUnlocked());
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
                // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #11, 웹 game.js:2627 — playerXp/레벨업 계산
                // 직후, _saveProfile() 직전) — 이번 런에서 사용한 캐릭/머신/장치 숙련도 누적.
                MasteryTracker.ApplyRunEnd(Profile, Controller.State, gameOverEvent.failure);
                // 웹 파리티 P3-4(game.js:179 생성자 호출뿐 아니라 레벨업 직후에도 즉시 반영돼야 다음
                // 런 시작 전에 이미 새 장치를 확인할 수 있다) — 런 종료 시점에도 재호출(멱등).
                Profile.GrantLevelDevices();
                ProfileStore.Save(Profile);
            }

            return events;
        }

        // ── HUD 미리보기 전용(진행바/남은 스핀 표시) — 실제 스핀 판정에는 전혀 관여하지 않는다.
        // ItemUse.InstantQuota/StageFlow.ClearStage의 clearCoinBonus 계산과 동일한 근사 패턴(device+
        // phasePerks는 반영하되 RunCtx 조건부 신규 증강 8종은 미반영) — 정확한 값은 SpinResolver.
        // ResolveSpin 내부(3단계 mods 재계산)에서만 산출되고 여기서는 재현하지 않는다.
        // Opus 2차검수 필수④(2026-08-09, WEB_PARITY_DESIGN.md §1-A #15/#16) — ApplyPassiveDevice
        // 누락으로 dev_reactor(quotaMul×1.15) 장착 런에서 HUD 진행바가 요구 EXP를 15% 낮게 보여주고
        // 있었다. RewardDoneView.NextPreview/CurrentStats(Engine/Run/RewardDoneInfo.cs)와 3곳 모두
        // 동일한 mods 조성(Build→ApplyPassiveDevice→ApplyItemMods)으로 통일한다.
        public (long quota, int spins) PreviewQuotaSpins()
        {
            var run = State;
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var mods = ModsBuilder.ApplyItemMods(
                ModsBuilder.ApplyPassiveDevice(
                    ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                    run.Device),
                run.PhaseItems);
            long quota = SpinResolver.QuotaOf(run.Stage, mods, run.Asc, run.BossPhase2, DeepRunHooks.DeepPenalty(run));
            int spins = SpinResolver.EffSpins(run, mods);
            return (quota, spins);
        }
    }
}
