using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // P3 슬라이스 1(플레이어 XP/레벨 코어) 골든 테스트 — WEB_PARITY_DESIGN.md §1-A #9 / 웹 game.js:107-120,
    // 172, 202-254, 2617-2625 정답지 손계산 대조. Engine/Core/Formulas.cs(PlayerXpReq/PlayerLevelFromXp/
    // PlayerRunXp/PlayerSeedXpFromHistory) + Engine/Profile/{PlayerProfile,ProfileDto,PlayerLevelTracker}.cs
    // + Engine/Run/{RunState,StageFlow}.cs(RunBossClears) 검증.

    // ── ① PlayerXpReq — 레벨별 요구 XP 손계산 (웹 game.js:109) ─────────────────────────────────────
    internal static class Tests_PlayerLevel_XpReq
    {
        public static void Run(TestCtx t)
        {
            t.Eq(120L, Formulas.PlayerXpReq(1), "[xpreq] lvl1→2 = 120+(1-1)*60 = 120");
            t.Eq(180L, Formulas.PlayerXpReq(2), "[xpreq] lvl2→3 = 120+60 = 180");
            t.Eq(240L, Formulas.PlayerXpReq(3), "[xpreq] lvl3→4 = 120+120 = 240");
            t.Eq(360L, Formulas.PlayerXpReq(5), "[xpreq] lvl5→6 = 120+240 = 360");
            t.Eq(420L, Formulas.PlayerXpReq(6), "[xpreq] lvl6→7 = 120+300 = 420");
            t.Eq(6060L, Formulas.PlayerXpReq(100), "[xpreq] lvl100→101 = 120+99*60 = 6060 (참조용, MAX에선 안 쓰임)");
        }
    }

    // ── ② PlayerLevelFromXp — 누적 XP→레벨 (웹 game.js:110-115 levelInfo, 순차 차감 누적식) ─────────
    internal static class Tests_PlayerLevel_LevelFromXp
    {
        public static void Run(TestCtx t)
        {
            t.Eq(1, Formulas.PlayerLevelFromXp(0), "[level-from-xp] xp=0 → level 1");
            t.Eq(1, Formulas.PlayerLevelFromXp(119), "[level-from-xp] xp=119 < req(1)=120 → level 1");
            t.Eq(2, Formulas.PlayerLevelFromXp(120), "[level-from-xp] xp=120 == req(1) → level 2 (정확히 소진)");
            t.Eq(2, Formulas.PlayerLevelFromXp(299), "[level-from-xp] xp=299 < 120+180=300 → level 2");
            t.Eq(3, Formulas.PlayerLevelFromXp(300), "[level-from-xp] xp=300 == req(1)+req(2) → level 3");

            // 다중 레벨업 — 손계산: req(1..4)=120+180+240+300=840, req(5)=360.
            // xp=1030 → 840 소진(레벨1→5) 후 rem=190 < 360 → level 5.
            t.Eq(5, Formulas.PlayerLevelFromXp(1030), "[level-from-xp] xp=1030 → 레벨1에서 한번에 5로(다중 레벨업)");

            // 음수 방어(Math.max(0,...) — 웹 game.js:111).
            t.Eq(1, Formulas.PlayerLevelFromXp(-500), "[level-from-xp] 음수 XP는 0 취급 → level 1");

            // PLAYER_LEVEL_MAX=100 상한 — 거대한 XP도 100을 넘지 않는다.
            t.Eq(100, Formulas.PlayerLevelFromXp(50_000_000L), "[level-from-xp] 거대 XP → 상한 100 클램프");
            t.Eq(100, Formulas.PlayerLevelFromXp(long.MaxValue / 2), "[level-from-xp] 극단값도 100 클램프(오버플로 없음)");
        }
    }

    // ── ③ PlayerRunXp — 런 종료 XP 공식 경계값 (웹 game.js:2619) ───────────────────────────────────
    internal static class Tests_PlayerLevel_RunXpFormula
    {
        public static void Run(TestCtx t)
        {
            // 기본항만: 40 + min(20,stage)*12, 그 외 0.
            t.Eq(52L, Formulas.PlayerRunXp(1, 0, 0, 0), "[runxp] stage=1,score=0,boss=0,ach=0 → 40+12=52");
            t.Eq(280L, Formulas.PlayerRunXp(20, 0, 0, 0), "[runxp] stage=20(경계) → 40+20*12=280");

            // stage>20 클램프 — min(20,stage) 이상 넘지 않음(21과 20이 동일해야 함).
            t.Eq(280L, Formulas.PlayerRunXp(21, 0, 0, 0), "[runxp] stage=21 → min(20,21)=20, stage=20과 동일값");
            t.Eq(280L, Formulas.PlayerRunXp(999, 0, 0, 0), "[runxp] stage=999(승천 등 고스테이지) → 여전히 클램프 280");

            // floor(finalScore/250) — 정수나눗셈 절삭 확인(경계값 249/250/999/1000).
            t.Eq(52L, Formulas.PlayerRunXp(1, 249, 0, 0), "[runxp] score=249 → floor(249/250)=0 → 52+0=52");
            t.Eq(53L, Formulas.PlayerRunXp(1, 250, 0, 0), "[runxp] score=250 → floor=1 → 53");
            t.Eq(55L, Formulas.PlayerRunXp(1, 999, 0, 0), "[runxp] score=999 → floor(999/250)=3 → 52+3=55");
            t.Eq(56L, Formulas.PlayerRunXp(1, 1000, 0, 0), "[runxp] score=1000 → floor=4 → 56");

            // 보스클리어×20 / 신규업적×25 가산.
            t.Eq(112L, Formulas.PlayerRunXp(1, 0, 3, 0), "[runxp] bossClearsThisRun=3 → 52+60=112");
            t.Eq(127L, Formulas.PlayerRunXp(1, 0, 0, 3), "[runxp] newAchievementCount=3 → 52+75=127");

            // 복합 — 손계산: 40+20*12(240)+floor(12345/250)=49+2*20(40)+3*25(75) = 40+240+49+40+75 = 444.
            t.Eq(444L, Formulas.PlayerRunXp(20, 12345, 2, 3), "[runxp] 복합(클램프+절삭+보스+업적) 손계산 444");
        }
    }

    // ── ④ PlayerSeedXpFromHistory — 이력 XP 시딩 공식 (웹 game.js:117-120) ─────────────────────────
    internal static class Tests_PlayerLevel_SeedFromHistory
    {
        public static void Run(TestCtx t)
        {
            // 손계산: runs*30 + floor(totalScore/300) + bossClears*15 + bestStage*8
            //        = 10*30 + floor(9000/300)(=30) + 4*15(=60) + 12*8(=96) = 300+30+60+96 = 486.
            t.Eq(486L, Formulas.PlayerSeedXpFromHistory(10, 9000, 4, 12), "[seed-history] 손계산 486");

            // totalScore가 300의 배수가 아닐 때 floor 절삭 확인: 950/300 = 3.166.. → 3.
            t.Eq(33L, Formulas.PlayerSeedXpFromHistory(1, 950, 0, 0), "[seed-history] floor(950/300)=3 → 30+3=33");

            // 전부 0(신규 프로필) → 0.
            t.Eq(0L, Formulas.PlayerSeedXpFromHistory(0, 0, 0, 0), "[seed-history] 이력 없음 → 0");
        }
    }

    // ── ⑤ ProfileDto 마이그레이션 — 기존 세이브 로드 시 1회 시딩(_xpInit 대응) ─────────────────────
    internal static class Tests_PlayerLevel_ProfileDtoMigration
    {
        public static void Run(TestCtx t)
        {
            LegacySaveGetsSeededOnce(t);
            AlreadySeededNeverReseeds(t);
            AlreadyAccruedXpSkipsSeedEvenIfNotFlagged(t);
            FreshEmptyProfileNoSeedButFlagged(t);
        }

        // 이 필드 도입 전 세이브를 흉내: statKeys/values로 runs/bossClears/bestStage만 채우고
        // playerXp/playerXpSeeded는 DTO 기본값(0/false) 그대로 둔다(JsonUtility가 JSON에 없는 필드를
        // C# 필드 초기값으로 남기는 것과 동일 — Unity JsonUtility 없이도 순수 C# 기본값으로 재현 가능).
        private static void LegacySaveGetsSeededOnce(TestCtx t)
        {
            var dto = new PlayerProfileDto
            {
                statKeys = new[] { "runs", "bossClears", "bestStage" },
                statValues = new long[] { 10, 4, 12 },
                totalScore = 9000,
            };
            t.Eq(0L, dto.playerXp, "[dto-migration] 사전조건: 레거시 DTO의 playerXp는 기본값 0");
            t.True(!dto.playerXpSeeded, "[dto-migration] 사전조건: 레거시 DTO의 playerXpSeeded는 기본값 false");

            var restored = ProfileDto.FromDto(dto);

            t.Eq(486L, restored.PlayerXp, "[dto-migration] runs=10,score=9000,boss=4,stage=12 → seedXpFromHistory=486 시딩");
            t.True(restored.PlayerXpSeeded, "[dto-migration] 시딩 후 PlayerXpSeeded=true로 마킹");
            // level: req(1)=120,req(2)=180 소진(300) 후 rem=186 < req(3)=240 → level 3.
            t.Eq(3, restored.PlayerLevel, "[dto-migration] xp=486 → level 3(웹 game.js:193 로드시 재산출)");
        }

        // 이미 seeded=true인 프로필을 다시 저장/로드해도 재시딩되지 않아야 한다(runs가 늘었어도 무관).
        private static void AlreadySeededNeverReseeds(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["runs"] = 10;
            profile.PlayerXp = 486;
            profile.PlayerXpSeeded = true;

            var dto = ProfileDto.ToDto(profile);
            dto.statValues[Array.IndexOf(dto.statKeys, "runs")] = 999; // runs가 늘어도(다음 런 이후 등)
            var restored = ProfileDto.FromDto(dto);

            t.Eq(486L, restored.PlayerXp, "[dto-migration] 이미 seeded=true면 runs 변화와 무관하게 재시딩 안 함");
            t.True(restored.PlayerXpSeeded, "[dto-migration] seeded 플래그 유지");
        }

        // playerXpSeeded는 false지만 이미 playerXp>0(예: 이 필드 도입 후 첫 런에서 정상 누적된 세이브를
        // "이번 세션에서 아직 한 번도 FromDto를 안 거친" 상태로 저장했다가 다음 로드에서 만나는 경우) —
        // 웹과 동일하게 "runs>0 && !(playerXp>0)"만 시딩하므로 이중 지급되면 안 된다.
        private static void AlreadyAccruedXpSkipsSeedEvenIfNotFlagged(TestCtx t)
        {
            var dto = new PlayerProfileDto
            {
                statKeys = new[] { "runs" },
                statValues = new long[] { 1 },
                playerXp = 52, // 첫 런 게임오버로 이미 획득한 runXp(52, 위 ③ stage=1 최소값과 동일)
                playerXpSeeded = false,
            };
            var restored = ProfileDto.FromDto(dto);
            t.Eq(52L, restored.PlayerXp, "[dto-migration] playerXp>0이면 seeded=false여도 재시딩하지 않음(이중지급 방지)");
            t.True(restored.PlayerXpSeeded, "[dto-migration] 이번 로드에서 seeded=true로 확정");
        }

        // runs=0(완전 신규)인 DTO는 시딩할 이력이 없으므로 xp=0 그대로, 플래그만 true로 확정.
        private static void FreshEmptyProfileNoSeedButFlagged(TestCtx t)
        {
            var dto = new PlayerProfileDto();
            var restored = ProfileDto.FromDto(dto);
            t.Eq(0L, restored.PlayerXp, "[dto-migration] runs=0 신규 프로필 → xp=0 유지");
            t.Eq(1, restored.PlayerLevel, "[dto-migration] xp=0 → level 1");
            t.True(restored.PlayerXpSeeded, "[dto-migration] 신규 프로필도 최초 로드 시 flagged=true");
        }
    }

    // ── ⑥ PlayerProfileDto 왕복(매핑 동일성) — PlayerXp/PlayerLevel/PlayerXpSeeded ───────────────
    internal static class Tests_PlayerLevel_ProfileDtoRoundTrip
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["runs"] = 5; // Runs>0이지만 이미 시딩 완료 상태를 흉내(재시딩 방지 확인 겸함)
            profile.PlayerXp = 1030; // ② 다중 레벨업 테스트와 같은 값(레벨5) — 독립 재계산으로 대조
            profile.PlayerXpSeeded = true;
            // ★ 의도적으로 profile.PlayerLevel은 기본값(1)인 채로 둔다(PlayerXp=1030과 불일치) — 만약
            // FromDto가 dto.playerLevel을 그대로 복사(재계산 생략)하는 버그라면 restored.PlayerLevel이
            // 1로 나와 아래 어서션(Formulas.PlayerLevelFromXp(1030)=5)과 어긋나 잡힌다.

            var dto = ProfileDto.ToDto(profile);
            t.Eq(1030L, dto.playerXp, "[dto-roundtrip] ToDto: playerXp 보존");
            t.True(dto.playerXpSeeded, "[dto-roundtrip] ToDto: playerXpSeeded 보존");

            var restored = ProfileDto.FromDto(dto);
            t.Eq(1030L, restored.PlayerXp, "[dto-roundtrip] FromDto: playerXp 왕복 동일");
            t.True(restored.PlayerXpSeeded, "[dto-roundtrip] FromDto: playerXpSeeded 왕복 동일");
            // PlayerLevel은 저장값을 그대로 믿지 않고 로드 시 XP로부터 다시 계산된다(웹과 동일 정책) —
            // Formulas.PlayerLevelFromXp로 독립 재계산해 대조(순환검증 방지).
            t.Eq(Formulas.PlayerLevelFromXp(1030L), restored.PlayerLevel, "[dto-roundtrip] PlayerLevel은 로드 시 XP로부터 재산출됨");
        }
    }

    // ── ⑦ PlayerLevelTracker.ApplyRunEnd — 단발 호출 계약(입출력 필드) 확인 ────────────────────────
    internal static class Tests_PlayerLevel_ApplyRunEndContract
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(1L);
            run.Stage = 20;
            run.RunBossClears = 2;
            var failure = new FailureOutcome { kind = "GAME_OVER", finalScore = 12345 };

            PlayerLevelTracker.ApplyRunEnd(profile, run, failure, newAchievementCount: 3);

            // 위 ③ 복합 테스트와 동일 입력(stage=20,score=12345,boss=2,ach=3) → runXp=444(손계산 동일).
            t.Eq(444L, failure.PlayerXpGain, "[apply-run-end] failure.PlayerXpGain == Formulas.PlayerRunXp 손계산 444");
            t.Eq(444L, profile.PlayerXp, "[apply-run-end] profile.PlayerXp도 동일하게 증가(신규 프로필 0→444)");
            t.Eq(1, failure.PlayerLevelBefore, "[apply-run-end] levelBefore: 적용 전 xp=0 → PlayerLevelFromXp(0)=1");
            // levelAfter 손계산: req(1)=120+req(2)=180=300 소진(rem=144) < req(3)=240 → level 3.
            t.Eq(3, failure.PlayerLevelAfter, "[apply-run-end] levelAfter: xp=444 → level 3");
        }
    }

    internal static class Tests_PlayerLevel_ApplyRunEndLevelBeforeAfter
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(2L);
            run.Stage = 1;
            run.RunBossClears = 0;

            // 1회차: xp 0 → 52(stage=1,score=0,boss=0,ach=0) → level 1 → 1 (52<120, 레벨업 없음).
            var f1 = new FailureOutcome { kind = "GAME_OVER", finalScore = 0 };
            PlayerLevelTracker.ApplyRunEnd(profile, run, f1, newAchievementCount: 0);
            t.Eq(1, f1.PlayerLevelBefore, "[level-before-after] 1회차 시작 레벨 1");
            t.Eq(1, f1.PlayerLevelAfter, "[level-before-after] 1회차 후 xp=52 < req(1)=120 → 레벨 그대로 1");
            t.Eq(52L, profile.PlayerXp, "[level-before-after] 1회차 누적 xp=52");

            // 2회차: 큰 finalScore로 대량 XP 획득 → 여러 레벨 한번에 상승.
            // runXp = 40+12+floor(30000/250=120)+0+0 = 172. 누적 xp = 52+172 = 224.
            // level(224): req(1)=120 소진(rem=104) < req(2)=180 → level 2.
            var f2 = new FailureOutcome { kind = "GAME_OVER", finalScore = 30000 };
            PlayerLevelTracker.ApplyRunEnd(profile, run, f2, newAchievementCount: 0);
            t.Eq(172L, f2.PlayerXpGain, "[level-before-after] 2회차 runXp 손계산 172");
            t.Eq(224L, profile.PlayerXp, "[level-before-after] 2회차 누적 xp=52+172=224");
            t.Eq(1, f2.PlayerLevelBefore, "[level-before-after] 2회차 시작 레벨(1회차 후) = 1");
            t.Eq(2, f2.PlayerLevelAfter, "[level-before-after] 2회차 후 레벨업 → 2");
            t.Eq(2, profile.PlayerLevel, "[level-before-after] profile.PlayerLevel도 동일하게 갱신");

            // null 가드 — failure가 없으면(REVIVE/POST_SPIN처럼 GAME_OVER가 아닌 경우) 아무 것도 하지 않음.
            var before = profile.PlayerXp;
            PlayerLevelTracker.ApplyRunEnd(profile, run, null, 5);
            t.Eq(before, profile.PlayerXp, "[level-before-after] failure==null이면 profile 변경 없음(가드)");
        }
    }

    // ── ⑧ RunState.RunBossClears — 실제 RunController 자동플레이로 증분 검증(합성 이벤트 아님) ────
    // Tests_S5_StatTrackerAutoPlay와 동일한 자동플레이 패턴을 재사용해, StatTracker와 별개로 STAGE_CLEARED
    // 이벤트에서 boss==true를 직접 세어 RunController.State.RunBossClears와 대조한다(교차검증).
    internal static class Tests_PlayerLevel_RunBossClearsAutoplay
    {
        public static void Run(TestCtx t)
        {
            RunOne(t, 777001L);
            RunOne(t, 424242L);
        }

        private static void RunOne(TestCtx tt, long seed)
        {
            var stat = S4TestHelpers.GenerousStat();
            RunController rc;
            try
            {
                rc = new RunController("novice", "basic", "", seed, stat);
            }
            catch (Exception ex)
            {
                tt.Fail($"[boss-clears-autoplay seed={seed}]", "RunController 생성 실패: " + ex);
                return;
            }

            int expectedBossClears = 0;
            RunEvent gameOverEvent = null;
            int guard = 0;
            int shopStep = 0;
            try
            {
                while (rc.State.Phase != RunPhase.GameOver && guard < 50_000)
                {
                    guard++;
                    var phase = rc.State.Phase;
                    IReadOnlyList<RunEvent> events;
                    switch (phase)
                    {
                        case RunPhase.Spin:
                            events = rc.Do(new Spin(SpinMode.N));
                            break;
                        case RunPhase.PostSpin:
                            events = rc.Do(new Continue());
                            break;
                        case RunPhase.NodeSelect:
                            events = rc.Do(new ChooseNode(0));
                            break;
                        case RunPhase.EventAugment:
                        case RunPhase.EventRelic:
                            events = rc.Do(new PickOffer(0));
                            break;
                        case RunPhase.EventShop:
                            if (shopStep == 0) { events = rc.Do(new BuyOffer(0)); shopStep = 1; }
                            else { events = rc.Do(new LeaveShop()); shopStep = 0; }
                            break;
                        default:
                            throw new InvalidOperationException("boss-clears-autoplay: 처리 불가 Phase=" + phase);
                    }
                    foreach (var e in events)
                    {
                        if (e.type == "STAGE_CLEARED" && e.clear != null && e.clear.boss) expectedBossClears++;
                        if (e.type == "GAME_OVER") gameOverEvent = e;
                    }
                }
            }
            catch (Exception ex)
            {
                tt.Fail($"[boss-clears-autoplay seed={seed}]", "자동플레이 중 예외: " + ex);
                return;
            }

            tt.True(rc.State.Phase == RunPhase.GameOver, $"[boss-clears-autoplay seed={seed}] guard(50000) 내 게임오버 도달");
            tt.Eq(expectedBossClears, rc.State.RunBossClears,
                $"[boss-clears-autoplay seed={seed}] RunState.RunBossClears == STAGE_CLEARED(boss=true) 직접 집계");

            // ── 이 자동플레이 결과로 GameSession.FinishAction과 동일한 순서(StatTracker→Achievement→
            //    PlayerLevelTracker)를 재현해 통합 계약을 검증한다(GameSession.cs는 Unity 전용이라 이
            //    dotnet 프로젝트에서 직접 인스턴스화할 수 없음 — Tests_S5.cs 헤더의 동일 각주 참조). ──
            tt.True(gameOverEvent != null && gameOverEvent.failure != null,
                $"[boss-clears-autoplay seed={seed}] GAME_OVER 이벤트/failure 확보");
            if (gameOverEvent?.failure == null) return;

            var profile = new PlayerProfile();
            var scratch = new StatTracker.RunScratch();
            // 런 전체 이벤트를 다시 흘려보내지 않고(이미 소비됨), 최종 상태 기준으로 게임오버 1건만
            // StatTracker에 반영해도 이 통합 계약(순서·결과 필드) 검증에는 충분하다 — 신규업적수는
            // 빈 프로필 기준 AchievementEngine.Evaluate가 실제로 산출한 값을 그대로 쓴다(합성값 아님).
            StatTracker.Apply(profile, rc.State, new List<RunEvent> { gameOverEvent }, scratch);
            var newly = AchievementEngine.Evaluate(profile);
            long expectedXp = Formulas.PlayerRunXp(rc.State.Stage, gameOverEvent.failure.finalScore, rc.State.RunBossClears, newly.Count);
            PlayerLevelTracker.ApplyRunEnd(profile, rc.State, gameOverEvent.failure, newly.Count);

            tt.Eq(expectedXp, gameOverEvent.failure.PlayerXpGain,
                $"[boss-clears-autoplay seed={seed}] ApplyRunEnd 결과 == Formulas.PlayerRunXp 독립 재계산");
            tt.Eq(expectedXp, profile.PlayerXp,
                $"[boss-clears-autoplay seed={seed}] profile.PlayerXp(신규 프로필 0→xpGain)");
            tt.Eq(Formulas.PlayerLevelFromXp(expectedXp), profile.PlayerLevel,
                $"[boss-clears-autoplay seed={seed}] profile.PlayerLevel == PlayerLevelFromXp(누적xp) 독립 재계산");
        }
    }

    // ── ⑨ 자발적 포기(GiveUp)도 예외 없이 XP를 받는다 (WEB_PARITY §2 결정로그(E)/(P1⑤) — 웹 _gameOver
    //     는 voluntary 여부와 무관하게 runXp 블록을 항상 실행) ─────────────────────────────────────
    internal static class Tests_PlayerLevel_VoluntaryGiveUpGetsXp
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var rc = new RunController("novice", "basic", "", 55001L, stat);
            var events = rc.GiveUp();

            t.Eq(1, events.Count, "[voluntary-xp] GiveUp() → GAME_OVER 단일 이벤트");
            var ev = events[0];
            t.Eq("GAME_OVER", ev.type, "[voluntary-xp] type == GAME_OVER");
            t.True(ev.failure != null && ev.failure.Voluntary, "[voluntary-xp] failure.Voluntary == true");

            var profile = new PlayerProfile();
            var newly = AchievementEngine.Evaluate(profile); // 빈 프로필이라 신규 0건일 수 있음(그대로 사용)
            long expectedXp = Formulas.PlayerRunXp(rc.State.Stage, ev.failure.finalScore, rc.State.RunBossClears, newly.Count);
            PlayerLevelTracker.ApplyRunEnd(profile, rc.State, ev.failure, newly.Count);

            t.Eq(expectedXp, ev.failure.PlayerXpGain, "[voluntary-xp] 포기여도 finalScore=0이어도 최소 runXp(stage=1→52) 지급");
            t.True(ev.failure.PlayerXpGain > 0, "[voluntary-xp] 포기여도 XP는 항상 양수(최소 40+12=52)");
        }
    }
}
