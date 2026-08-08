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

    // ── ②b PlayerLevelProgressFromXp — 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15, 웹 game.js:110-115
    //     levelInfo 그대로) — 표시용 진행도(level/inLevel/need/ratio/max)가 위 ②의 level 계산과
    //     일관되게 나오는지 + 웹 반환 필드를 손계산으로 대조.
    internal static class Tests_PlayerLevel_ProgressFromXp
    {
        public static void Run(TestCtx t)
        {
            var p0 = Formulas.PlayerLevelProgressFromXp(0);
            t.Eq(1, p0.Level, "[progress] xp=0 → level 1");
            t.Eq(0L, p0.InLevel, "[progress] xp=0 → inLevel 0");
            t.Eq(120L, p0.Need, "[progress] xp=0 → need = req(1) = 120");
            t.EqTol(0.0, p0.Ratio, "[progress] xp=0 → ratio 0");
            t.True(!p0.Max, "[progress] xp=0 → max=false");

            // 레벨 3 진입 직후 일부 — req(1)=120,req(2)=180 소진(300) 후 rem=90 < req(3)=240 → level 3.
            var p1 = Formulas.PlayerLevelProgressFromXp(390);
            t.Eq(3, p1.Level, "[progress] xp=390 → level 3(②와 동일 로직 대조)");
            t.Eq(Formulas.PlayerLevelFromXp(390), p1.Level, "[progress] level == PlayerLevelFromXp(390) 일관성");
            t.Eq(90L, p1.InLevel, "[progress] xp=390 → inLevel = 390-300 = 90");
            t.Eq(240L, p1.Need, "[progress] xp=390 → need = req(3) = 240");
            t.EqTol(90.0 / 240.0, p1.Ratio, "[progress] ratio = 90/240");
            t.Eq(390L, p1.Xp, "[progress] xp 필드는 입력 그대로(음수 방어 후)");

            // MAX 레벨 — need=0, ratio=1(웹 `need ? rem/need : 1`).
            var pMax = Formulas.PlayerLevelProgressFromXp(50_000_000L);
            t.Eq(100, pMax.Level, "[progress] 거대 XP → level 100(MAX)");
            t.Eq(0L, pMax.Need, "[progress] MAX → need 0");
            t.EqTol(1.0, pMax.Ratio, "[progress] MAX → ratio 1(need=0 폴백)");
            t.True(pMax.Max, "[progress] MAX → max=true");

            // 음수 방어 — 웹 Math.max(0,...)과 동일.
            var pNeg = Formulas.PlayerLevelProgressFromXp(-999);
            t.Eq(1, pNeg.Level, "[progress] 음수 xp → level 1");
            t.Eq(0L, pNeg.Xp, "[progress] 음수 xp → Xp 필드도 0으로 클램프");

            // PlayerProfile.LevelProgress() 위임 확인.
            var profile = new PlayerProfile { PlayerXp = 390 };
            var viaProfile = profile.LevelProgress();
            t.Eq(p1.Level, viaProfile.Level, "[progress] PlayerProfile.LevelProgress() level 위임 일치");
            t.Eq(p1.InLevel, viaProfile.InLevel, "[progress] PlayerProfile.LevelProgress() inLevel 위임 일치");
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
            profile.PlayerXpReseed34 = true; // WEB_PARITY P3-2 §2-(L) 재시딩과 이 테스트를 분리(위 각주 참조)

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
                // WEB_PARITY P3-2 §2-(L) 재시딩 마이그레이션(별개 독립 플래그)과 이 테스트(P3-1 seeded
                // 가드 전용)가 서로 간섭하지 않도록 true로 고정한다 — 실제 앱 플로우에서는
                // ProfileStore.Load()가 파일이 없는 최초 실행조차 FromDto(빈 DTO)를 한 번 거치게 해
                // Runs=0/PlayerXp=0 상태로 이 플래그가 먼저 true 확정된 뒤에야 플레이가 시작되므로
                // (ProfileStore.cs 참조), "runs=1·playerXp=52·playerXpReseed34=false"가 동시에 실제
                // 세이브로 존재하는 조합은 나오지 않는다 — 이 테스트가 검증하려는 대상(P3-1 이중시딩
                // 방지)과 무관한 조합까지 재현하지 않기 위해 명시적으로 true로 둔다.
                playerXpReseed34 = true,
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

    // ── ⑤b XP 재시딩 마이그레이션(WEB_PARITY_DESIGN.md §2-(L), 업적 34종 교체 인플레이션 정정) ──────
    // ProfileDto.FromDto의 두 번째(독립) 마이그레이션 블록 — PlayerXpSeeded와 별개의 PlayerXpReseed34
    // 플래그로 딱 한 번만 적용된다. "재산출값이 더 작을 때만 덮어쓴다"는 보수적 규칙과, 그 규칙이
    // ProfileStore.Load()의 "파일 없음도 FromDto(빈 DTO) 경유" 보강과 맞물려 신규 프로필을 절대
    // 잘못 깎지 않는다는 사실(빈 DTO는 Runs=0/PlayerXp=0이라 재시딩값도 0 → 0<0 거짓 → 무변화)을
    // 함께 검증한다.
    internal static class Tests_PlayerLevel_XpReseed34Migration
    {
        public static void Run(TestCtx t)
        {
            InflatedOldSaveGetsClippedOnce(t);
            ReseedNeverIncreasesXp(t);
            AlreadyReseededProfileIsUntouched(t);
            FreshProfileFlagSetImmediatelyNoClip(t);
        }

        // 482종 시절 "신규업적×25" 인플레이션을 흉내: 손계산 재시딩값(270)보다 훨씬 큰 playerXp(5000)를
        // 가진 세이브 — 1회 재시딩으로 270까지 깎이고, 플래그가 true로 확정돼야 한다.
        private static void InflatedOldSaveGetsClippedOnce(TestCtx t)
        {
            var dto = new PlayerProfileDto
            {
                statKeys = new[] { "runs", "bossClears", "bestStage" },
                statValues = new long[] { 5, 2, 10 },
                totalScore = 3000,
                playerXp = 5000,          // 482종 시절 인플레이션 흉내(정상치 대비 크게 부풀어 있음)
                playerXpSeeded = true,     // 이미 §2617-2625 시딩은 끝난 상태(재시딩과는 별개 플래그)
                playerXpReseed34 = false,  // 이 필드 도입 전 세이브 흉내
            };
            t.True(!dto.playerXpReseed34, "[xp-reseed34] 사전조건: 레거시 DTO의 playerXpReseed34는 기본값 false");

            var restored = ProfileDto.FromDto(dto);

            // 손계산: runs*30 + floor(totalScore/300) + bossClears*15 + bestStage*8
            //        = 5*30 + floor(3000/300)(=10) + 2*15(=30) + 10*8(=80) = 150+10+30+80 = 270.
            t.Eq(270L, restored.PlayerXp, "[xp-reseed34] 인플레이션 세이브(5000) → 재시딩값(270)으로 정정(더 작아서 덮어씀)");
            t.True(restored.PlayerXpReseed34, "[xp-reseed34] 재시딩 후 PlayerXpReseed34=true로 마킹");
        }

        // 재시딩값(30)이 현재 playerXp(10)보다 *큰* 경우 — "정당하게 번 XP를 깎지 않는다"는 보수적
        // 규칙이 실제로 지켜지는지(더 작을 때만 덮어씀, 큰 쪽으로는 절대 끌어올리지 않음).
        private static void ReseedNeverIncreasesXp(TestCtx t)
        {
            var dto = new PlayerProfileDto
            {
                statKeys = new[] { "runs" },
                statValues = new long[] { 1 },
                playerXp = 10,             // 재시딩값(1*30=30)보다 작은 실측치
                playerXpSeeded = true,
                playerXpReseed34 = false,
            };
            var restored = ProfileDto.FromDto(dto);
            t.Eq(10L, restored.PlayerXp, "[xp-reseed34] 재시딩값(30)이 더 커도 playerXp를 끌어올리지 않음(그대로 10)");
            t.True(restored.PlayerXpReseed34, "[xp-reseed34] 비교만 하고 플래그는 여전히 true로 확정");
        }

        // 이미 재시딩이 끝난(PlayerXpReseed34=true) 세이브는 이후 runs가 얼마나 바뀌든 다시 깎이지 않는다.
        private static void AlreadyReseededProfileIsUntouched(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["runs"] = 5;
            profile.PlayerXp = 5000;
            profile.PlayerXpSeeded = true;
            profile.PlayerXpReseed34 = true;

            var dto = ProfileDto.ToDto(profile);
            dto.statValues[Array.IndexOf(dto.statKeys, "runs")] = 1; // 재시딩값이 훨씬 작아지도록 조작해도
            var restored = ProfileDto.FromDto(dto);

            t.Eq(5000L, restored.PlayerXp, "[xp-reseed34] 이미 재시딩 완료(true)면 runs 변화와 무관하게 재적용 안 함");
            t.True(restored.PlayerXpReseed34, "[xp-reseed34] 플래그 유지");
        }

        // 빈 DTO(최초 실행, ProfileStore.Load()가 파일없음도 이 경로로 보낸다) — Runs=0/PlayerXp=0이라
        // 재시딩값도 0이고, "0 < 0"이 거짓이라 깎일 값이 없다. 플레이 시작 전에 플래그가 이미 true로
        // 확정된다는 사실이 핵심(브랜드뉴 플레이어가 첫 런 이후 두 번째 로드에서 잘못 깎이지 않는 이유).
        private static void FreshProfileFlagSetImmediatelyNoClip(TestCtx t)
        {
            var restored = ProfileDto.FromDto(new PlayerProfileDto());
            t.Eq(0L, restored.PlayerXp, "[xp-reseed34] 빈 DTO → playerXp 그대로 0");
            t.True(restored.PlayerXpReseed34, "[xp-reseed34] 빈 DTO도 최초 로드 시 flagged=true(플레이 시작 전에 확정)");
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
            // WEB_PARITY P3-2 §2-(L) 재시딩 마이그레이션과 무관한 이 왕복 테스트가 그 새 로직에 걸려
            // PlayerXp가 깎이지 않도록 이미 완료 처리해 둔다(위 AlreadyAccruedXpSkipsSeedEvenIfNotFlagged
            // 테스트의 동일 각주 참조 — 실제 앱 플로우에서는 이미 true였을 조합).
            profile.PlayerXpReseed34 = true;
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

    // ── ⑥b Stats["playerLevel"] 1런 지연 스냅샷 (WEB_PARITY P3-2 작업 지시 3번, 웹 game.js:2578) ──────
    // StatTracker.ApplyGameOverTracking이 PlayerLevelTracker.ApplyRunEnd(XP 부여)보다 *먼저* 실행되는
    // GameSession.FinishAction 순서(StatTracker.Apply→AchievementEngine.Evaluate→PlayerLevelTracker.
    // ApplyRunEnd)를 그대로 재현해, lv20/lv40 업적이 "이번 런에서 막 오른 레벨"이 아니라 "직전까지
    // 쌓인 레벨"로만 판정되는지(=1런 지연) 확인한다.
    internal static class Tests_PlayerLevel_PlayerLevelSnapshotOneRunDelay
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            var scratch = new StatTracker.RunScratch();

            // 런1: 대량 XP로 같은 런 안에서 레벨 20+까지 즉시 도달.
            var run1 = S4TestHelpers.NewRun(31L);
            run1.Stage = 20;
            var failure1 = new FailureOutcome { kind = "GAME_OVER", finalScore = 3_500_000 };
            StatTracker.Apply(profile, run1, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure1 } }, scratch);
            // GameSession 순서상 이 시점엔 아직 PlayerLevelTracker가 실행되지 않았다 — 스냅샷은
            // "런1 시작 레벨"(1)이어야 한다(웹 game.js:2578 "playerLevel 은 1런 지연" 그대로).
            t.Eq(1L, profile.GetStat("playerLevel"), "[playerlevel-delay] 런1 GAME_OVER 시점 스냅샷 == 시작레벨 1(아직 XP 미반영)");
            var newly1 = AchievementEngine.Evaluate(profile);
            t.True(!newly1.Any(a => a.id == "lv20"), "[playerlevel-delay] 런1에서는 lv20 미달성(스냅샷이 아직 1이라)");
            PlayerLevelTracker.ApplyRunEnd(profile, run1, failure1, newly1.Count);
            t.True(profile.PlayerLevel >= 20, "[playerlevel-delay] 런1 XP 반영 후 실제 레벨은 20 이상으로 상승(사전조건 확인)");

            // 런2: 소량 XP만 — 이번 GAME_OVER 스냅샷에는 "런1이 만든" 20+ 레벨이 뒤늦게 반영된다.
            var run2 = S4TestHelpers.NewRun(32L);
            run2.Stage = 1;
            var failure2 = new FailureOutcome { kind = "GAME_OVER", finalScore = 0 };
            StatTracker.Apply(profile, run2, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = failure2 } }, scratch);
            t.True(profile.GetStat("playerLevel") >= 20, "[playerlevel-delay] 런2 GAME_OVER 시점 스냅샷 == 런1이 만든 레벨(20+, 1런 지연으로 이제야 반영)");
            var newly2 = AchievementEngine.Evaluate(profile);
            t.True(newly2.Any(a => a.id == "lv20"), "[playerlevel-delay] 런2에서야 lv20 신규 달성(정확히 한 런 늦게 확정)");
            PlayerLevelTracker.ApplyRunEnd(profile, run2, failure2, newly2.Count);
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
                        // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — "스테이지 N 시작" 탭 즉시 진행.
                        case RunPhase.RewardDone:
                            events = rc.Do(new ProceedToStage());
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
