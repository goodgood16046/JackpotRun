namespace JackpotRun.Engine
{
    // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #11) — 런 종료 시 숙련도(mastery) 누적. 웹 game.js:2627
    // `this._bumpMastery("char", r.charId); this._bumpMastery("mac", r.machineId); if (r.device)
    // this._bumpMastery("dev", r.device);` 그대로 — _gameOver() 맨 끝, playerXp/레벨업 계산 직후에
    // 호출된다(PlayerLevelTracker.ApplyRunEnd와 같은 타이밍). GameSession.FinishAction이 GAME_OVER
    // 감지 시 PlayerLevelTracker.ApplyRunEnd 다음 순서로 이 클래스를 호출한다.
    //
    // StatTracker/PlayerLevelTracker와 같은 패턴(순수 Engine/Profile 계층, PlayerProfile을 직접 변경) —
    // 저장(ProfileStore.Save)은 여전히 엔진 밖 Unity 어댑터 소관(설계 원칙 6, 이 클래스는 저장하지 않음).
    public static class MasteryTracker
    {
        // profile: 갱신 대상. run: 종료 시점의 RunState(CharId/MachineId/Device/Stage/RunBossClears —
        // 웹 r.charId/r.machineId/r.device/r.stage/r.stats.bossClears 그대로 대응). failure: GAME_OVER
        // RunEvent.failure(FailureOutcome) — finalScore를 읽는다(웹 r.finalScore, PlayerLevelTracker가
        // 이미 같은 값을 소비한 뒤라 순서 무관하게 재사용 가능).
        public static void ApplyRunEnd(PlayerProfile profile, RunState run, FailureOutcome failure)
        {
            if (profile == null || run == null || failure == null) return;
            long finalScore = failure.finalScore;
            // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18) — graduatedThisRun/asc를 그대로 전달해
            // MasteryStats.AscMax(char[4] "심화 학기 5 졸업" 마일스톤 등)가 갱신되게 한다.
            bool grad = run.GraduatedThisRun;
            int asc = run.Asc;
            profile.BumpMastery("char", run.CharId, run.Stage, run.RunBossClears, finalScore, grad, asc);
            profile.BumpMastery("mac", run.MachineId, run.Stage, run.RunBossClears, finalScore, grad, asc);
            // 웹 game.js:2627 `if (r.device) this._bumpMastery("dev", r.device)` — 미장착(빈 문자열)이면 스킵.
            if (!string.IsNullOrEmpty(run.Device)) profile.BumpMastery("dev", run.Device, run.Stage, run.RunBossClears, finalScore, grad, asc);
        }
    }
}
