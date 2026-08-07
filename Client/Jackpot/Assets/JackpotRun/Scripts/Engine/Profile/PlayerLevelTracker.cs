namespace JackpotRun.Engine
{
    // 웹 파리티 P3(#9, WEB_PARITY_DESIGN.md §1-A) — 런 종료 시 플레이어 레벨 XP 부여. 웹 game.js:2617-2625
    // 이식: GAME_OVER 시점에 StatTracker.Apply(카운터 갱신) → AchievementEngine.Evaluate(신규업적 확정)
    // 다음 순서로 호출되어야 한다(웹도 newly.length를 먼저 만든 뒤 runXp 계산). Assets\JackpotRun\Scripts\
    // Game\GameSession.cs(Unity 글루 레이어)가 GAME_OVER RunEvent를 감지한 직후 이 순서로 호출한다.
    //
    // StatTracker/AchievementEngine과 같은 패턴(순수 Engine/Profile 계층, PlayerProfile을 직접 변경) —
    // 저장(ProfileStore.Save)은 여전히 엔진 밖 Unity 어댑터 소관(설계 원칙 6, 이 클래스는 저장하지 않음).
    public static class PlayerLevelTracker
    {
        // profile: 갱신 대상(PlayerXp/PlayerLevel을 직접 변경). run: 종료 시점의 RunState(Stage·
        // RunBossClears 참조 — StageFlow.ClearStage가 이미 채워 둔 런 스코프 카운터). failure: GAME_OVER
        // RunEvent.failure(FailureOutcome) — finalScore를 읽고 PlayerXpGain/LevelBefore/LevelAfter를
        // 채워 되돌려준다(웹 game.js:2622 r.xpGain/r.levelBefore/r.levelAfter 대응, UI가 나중에 읽음).
        // newAchievementCount: 이번 GAME_OVER에서 AchievementEngine.Evaluate가 반환한 신규 달성 업적 수
        // (웹 newly.length) — 호출측이 Evaluate를 먼저 실행해 넘겨야 한다(이 함수는 업적 판정을 하지 않음).
        public static void ApplyRunEnd(PlayerProfile profile, RunState run, FailureOutcome failure, int newAchievementCount)
        {
            if (profile == null || run == null || failure == null) return;

            int levelBefore = Formulas.PlayerLevelFromXp(profile.PlayerXp);
            long xpGain = Formulas.PlayerRunXp(run.Stage, failure.finalScore, run.RunBossClears, newAchievementCount);
            profile.PlayerXp += xpGain;
            profile.PlayerLevel = Formulas.PlayerLevelFromXp(profile.PlayerXp);

            failure.PlayerXpGain = xpGain;
            failure.PlayerLevelBefore = levelBefore;
            failure.PlayerLevelAfter = profile.PlayerLevel;
        }
    }
}
