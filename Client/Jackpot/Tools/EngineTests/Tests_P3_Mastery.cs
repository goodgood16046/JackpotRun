using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // P3-3 슬라이스(WEB_PARITY_DESIGN.md §1-A #11) 골든 테스트 — 숙련도(mastery: char/mac/dev × 5
    // 마일스톤) 검증. 웹 game.js:143-165(MASTERY 표)·166-170(masteryLevel)·217-232(_bumpMastery/
    // masteryOf)·2627(런종료 갱신 호출부) 손계산 대조. Engine/Core/Formulas.cs(MasteryLevel/MasteryTotal)
    // + Engine/Profile/{PlayerProfile,ProfileDto,MasteryTracker}.cs 검증.

    // ── ① char 마일스톤 경계값 — 웹 game.js:144-150 5개 각각 독립 판정 ──────────────────────────────
    internal static class Tests_P3_Mastery_CharMilestoneBoundaries
    {
        public static void Run(TestCtx t)
        {
            // lv1: bestStage>=3
            t.Eq(0, Formulas.MasteryLevel("char", 0, 2, 0, 0, -1), "[mastery-char] bestStage=2 → 0(경계 미달)");
            t.Eq(1, Formulas.MasteryLevel("char", 0, 3, 0, 0, -1), "[mastery-char] bestStage=3 → 1(경계 충족)");
            // lv2: bossClears>=3 (bestStage도 3 이상이라 lv1 동시충족 → 누적 2)
            t.Eq(1, Formulas.MasteryLevel("char", 0, 3, 2, 0, -1), "[mastery-char] bossClears=2 → lv1만(2 미달)");
            t.Eq(2, Formulas.MasteryLevel("char", 0, 3, 3, 0, -1), "[mastery-char] bossClears=3 → lv1+lv2=2");
            // lv3: bestStage>=15(단독 — 다른 조건 전부 미충족이어도 이 마일스톤만 인정)
            t.Eq(2, Formulas.MasteryLevel("char", 0, 15, 0, 0, -1), "[mastery-char] bestStage=15 → lv1(3+)+lv3(15+)=2, bossClears/score/asc 미충족");
            // lv4: bestScore>=50000 (독립 — bestStage=0이어도 별도로 인정됨)
            t.Eq(0, Formulas.MasteryLevel("char", 0, 0, 0, 49999, -1), "[mastery-char] bestScore=49999,나머지 0 → 레벨 0(경계 미달)");
            t.Eq(1, Formulas.MasteryLevel("char", 0, 0, 0, 50000, -1), "[mastery-char] bestScore=50000(경계) → lv4만 단독 충족(다른 축은 0)");
            // lv5: ascMax>=5 — 승천(P6) 미구현이라 기본값 -1이면 영구 미충족.
            t.Eq(0, Formulas.MasteryLevel("char", 0, 0, 0, 0, -1), "[mastery-char] 전부 0/-1 → 레벨 0");
            t.Eq(1, Formulas.MasteryLevel("char", 0, 0, 0, 0, 5), "[mastery-char] ascMax=5(가상값) → lv5만 단독 충족");
            t.Eq(5, Formulas.MasteryLevel("char", 100, 15, 10, 100000, 10), "[mastery-char] 전부 큰 값 → 만렙(5/5)");
        }
    }

    // ── ② mac 마일스톤 경계값 — 웹 game.js:151-157 ──────────────────────────────────────────────
    internal static class Tests_P3_Mastery_MacMilestoneBoundaries
    {
        public static void Run(TestCtx t)
        {
            t.Eq(0, Formulas.MasteryLevel("mac", 2, 0, 0, 0, -1), "[mastery-mac] runs=2 → 0(경계 미달)");
            t.Eq(1, Formulas.MasteryLevel("mac", 3, 0, 0, 0, -1), "[mastery-mac] runs=3 → 1(경계 충족)");
            t.Eq(0, Formulas.MasteryLevel("mac", 0, 7, 0, 0, -1), "[mastery-mac] bestStage=7 → 0(경계 미달)");
            t.Eq(1, Formulas.MasteryLevel("mac", 0, 8, 0, 0, -1), "[mastery-mac] bestStage=8 → 1(경계 충족)");
            t.Eq(0, Formulas.MasteryLevel("mac", 0, 0, 4, 0, -1), "[mastery-mac] bossClears=4 → 0(경계 미달)");
            t.Eq(1, Formulas.MasteryLevel("mac", 0, 0, 5, 0, -1), "[mastery-mac] bossClears=5 → 1(경계 충족)");
            t.Eq(0, Formulas.MasteryLevel("mac", 0, 0, 0, 39999, -1), "[mastery-mac] bestScore=39999 → 0(경계 미달)");
            t.Eq(1, Formulas.MasteryLevel("mac", 0, 0, 0, 40000, -1), "[mastery-mac] bestScore=40000 → 1(경계 충족)");
            // bestStage=15는 lv2(8+)와 lv5(15+) 둘 다 동시 충족 → 2.
            t.Eq(2, Formulas.MasteryLevel("mac", 0, 15, 0, 0, -1), "[mastery-mac] bestStage=15 → lv2+lv5=2(8+와 15+ 둘 다)");
            t.Eq(5, Formulas.MasteryLevel("mac", 100, 15, 10, 100000, -1), "[mastery-mac] 전부 큰 값 → 만렙(5/5, ascMax 무관)");
        }
    }

    // ── ③ dev 마일스톤 경계값 — 웹 game.js:158-164(전부 runs 누적 장착 횟수) ───────────────────────
    internal static class Tests_P3_Mastery_DevMilestoneBoundaries
    {
        public static void Run(TestCtx t)
        {
            t.Eq(0, Formulas.MasteryLevel("dev", 4, 0, 0, 0, -1), "[mastery-dev] runs=4 → 0");
            t.Eq(1, Formulas.MasteryLevel("dev", 5, 0, 0, 0, -1), "[mastery-dev] runs=5 → 1");
            t.Eq(1, Formulas.MasteryLevel("dev", 14, 0, 0, 0, -1), "[mastery-dev] runs=14 → 1(15 미달)");
            t.Eq(2, Formulas.MasteryLevel("dev", 15, 0, 0, 0, -1), "[mastery-dev] runs=15 → 2");
            t.Eq(2, Formulas.MasteryLevel("dev", 29, 0, 0, 0, -1), "[mastery-dev] runs=29 → 2(30 미달)");
            t.Eq(3, Formulas.MasteryLevel("dev", 30, 0, 0, 0, -1), "[mastery-dev] runs=30 → 3");
            t.Eq(3, Formulas.MasteryLevel("dev", 59, 0, 0, 0, -1), "[mastery-dev] runs=59 → 3(60 미달)");
            t.Eq(4, Formulas.MasteryLevel("dev", 60, 0, 0, 0, -1), "[mastery-dev] runs=60 → 4");
            t.Eq(4, Formulas.MasteryLevel("dev", 99, 0, 0, 0, -1), "[mastery-dev] runs=99 → 4(100 미달)");
            t.Eq(5, Formulas.MasteryLevel("dev", 100, 0, 0, 0, -1), "[mastery-dev] runs=100 → 5(만렙)");
            t.Eq(5, Formulas.MasteryLevel("dev", 999, 0, 0, 0, -1), "[mastery-dev] runs=999 → 5(상한 없음, 그래도 5장까지만)");
        }
    }

    // ── ④ MasteryTotal / 미지정 kind 방어 ──────────────────────────────────────────────────────
    internal static class Tests_P3_Mastery_TotalAndUnknownKind
    {
        public static void Run(TestCtx t)
        {
            t.Eq(5, Formulas.MasteryTotal("char"), "[mastery-total] char total=5");
            t.Eq(5, Formulas.MasteryTotal("mac"), "[mastery-total] mac total=5");
            t.Eq(5, Formulas.MasteryTotal("dev"), "[mastery-total] dev total=5");
            t.Eq(0, Formulas.MasteryTotal("unknown"), "[mastery-total] 미지정 kind → 0");
            t.Eq(0, Formulas.MasteryLevel("unknown", 999, 999, 999, 999999, 999), "[mastery-total] 미지정 kind → 레벨도 0");
        }
    }

    // ── ⑤ PlayerProfile.BumpMastery / MasteryOf — 웹 _bumpMastery 누적 규칙(runs+=1, setMax류, dev는 미장착 스킵) ──
    internal static class Tests_P3_Mastery_BumpAndQuery
    {
        public static void Run(TestCtx t)
        {
            var p = new PlayerProfile();

            var zero = p.MasteryOf("char", "novice");
            t.Eq(0, zero.Level, "[mastery-bump] 미기록 캐릭터는 Level=0");
            t.Eq(5, zero.Total, "[mastery-bump] Total=5(char)");

            p.BumpMastery("char", "novice", stage: 3, bossClearsThisRun: 1, finalScore: 1000);
            var s1 = p.Mastery["char"]["novice"];
            t.Eq(1, s1.Runs, "[mastery-bump] 1회차 Runs=1");
            t.Eq(3, s1.BestStage, "[mastery-bump] 1회차 BestStage=3");
            t.Eq(1, s1.BossClears, "[mastery-bump] 1회차 BossClears=1");
            t.Eq(1000L, s1.BestScore, "[mastery-bump] 1회차 BestScore=1000");
            t.Eq(-1, s1.AscMax, "[mastery-bump] AscMax는 승천 미구현이라 항상 -1(갱신 안 함)");
            t.Eq(1, p.MasteryOf("char", "novice").Level, "[mastery-bump] bestStage=3 → char lv1 충족(Level=1)");

            // 2회차 — bestStage는 더 낮지만(setMax 규칙) BestStage 유지, BossClears는 누적(+=), BestScore는 더 낮으면 유지.
            p.BumpMastery("char", "novice", stage: 1, bossClearsThisRun: 2, finalScore: 500);
            var s2 = p.Mastery["char"]["novice"];
            t.Eq(2, s2.Runs, "[mastery-bump] 2회차 Runs=2(누적)");
            t.Eq(3, s2.BestStage, "[mastery-bump] 2회차 BestStage 여전히 3(1<3, setMax 규칙 — 낮아지지 않음)");
            t.Eq(3, s2.BossClears, "[mastery-bump] 2회차 BossClears=1+2=3(누적, setMax 아님)");
            t.Eq(1000L, s2.BestScore, "[mastery-bump] 2회차 BestScore 여전히 1000(500<1000, setMax 규칙)");
            t.Eq(2, p.MasteryOf("char", "novice").Level, "[mastery-bump] bossClears=3 도달 → char lv1+lv2=2");

            // 3회차 — 더 높은 값들로 다시 setMax 갱신 확인.
            p.BumpMastery("char", "novice", stage: 15, bossClearsThisRun: 0, finalScore: 60000);
            var s3 = p.Mastery["char"]["novice"];
            t.Eq(3, s3.Runs, "[mastery-bump] 3회차 Runs=3");
            t.Eq(15, s3.BestStage, "[mastery-bump] 3회차 BestStage=15(setMax 갱신)");
            t.Eq(3, s3.BossClears, "[mastery-bump] 3회차 BossClears 그대로 3(이번 런 0)");
            t.Eq(60000L, s3.BestScore, "[mastery-bump] 3회차 BestScore=60000(setMax 갱신)");
            // 5개 전부: bestStage>=3(o) bossClears>=3(o) bestStage>=15(o) bestScore>=50000(o) ascMax>=5(x, -1) → 4.
            t.Eq(4, p.MasteryOf("char", "novice").Level, "[mastery-bump] 3회차 후 char Level=4(승천 미구현 lv5만 미충족)");

            // 빈 id는 무시(방어) — Mastery 딕셔너리 자체가 오염되지 않아야 함.
            p.BumpMastery("char", "", stage: 99, bossClearsThisRun: 99, finalScore: 999999);
            t.True(!p.Mastery["char"].ContainsKey(""), "[mastery-bump] 빈 id는 BumpMastery가 무시(오염 방지)");

            // 서로 다른 kind는 독립적으로 관리된다 — "novice"라는 같은 문자열이 char/dev에 동시에 있어도 안 섞임.
            p.BumpMastery("dev", "novice", stage: 1, bossClearsThisRun: 0, finalScore: 0);
            t.Eq(3, p.Mastery["char"]["novice"].Runs, "[mastery-bump] kind별 독립 — dev 갱신이 char를 건드리지 않음");
            t.Eq(1, p.Mastery["dev"]["novice"].Runs, "[mastery-bump] dev 쪽은 별도로 Runs=1");
        }
    }

    // ── ⑥ MasteryTracker.ApplyRunEnd — 웹 game.js:2627 그대로(char/mac 항상, dev는 미장착 시 스킵) ────
    internal static class Tests_P3_Mastery_TrackerApplyRunEnd
    {
        public static void Run(TestCtx t)
        {
            DeviceEquippedBumpsAllThree(t);
            NoDeviceSkipsDevMastery(t);
            NullGuardsDoNothing(t);
        }

        private static void DeviceEquippedBumpsAllThree(TestCtx t)
        {
            var p = new PlayerProfile();
            var run = S4TestHelpers.NewRun(1L, charId: "novice", machineId: "basic", deviceId: "dev_bell");
            run.Stage = 5;
            run.RunBossClears = 1;
            var failure = new FailureOutcome { kind = "GAME_OVER", finalScore = 12345 };

            MasteryTracker.ApplyRunEnd(p, run, failure);

            t.Eq(1, p.Mastery["char"]["novice"].Runs, "[mastery-tracker] char(novice) Runs=1");
            t.Eq(5, p.Mastery["char"]["novice"].BestStage, "[mastery-tracker] char BestStage=run.Stage(5)");
            t.Eq(1, p.Mastery["char"]["novice"].BossClears, "[mastery-tracker] char BossClears=run.RunBossClears(1)");
            t.Eq(12345L, p.Mastery["char"]["novice"].BestScore, "[mastery-tracker] char BestScore=failure.finalScore");

            t.Eq(1, p.Mastery["mac"]["basic"].Runs, "[mastery-tracker] mac(basic) Runs=1");
            t.Eq(5, p.Mastery["mac"]["basic"].BestStage, "[mastery-tracker] mac BestStage=5");

            t.True(p.Mastery.ContainsKey("dev") && p.Mastery["dev"].ContainsKey("dev_bell"), "[mastery-tracker] 장치 장착 시 dev도 기록");
            t.Eq(1, p.Mastery["dev"]["dev_bell"].Runs, "[mastery-tracker] dev(dev_bell) Runs=1");
        }

        private static void NoDeviceSkipsDevMastery(TestCtx t)
        {
            var p = new PlayerProfile();
            var run = S4TestHelpers.NewRun(2L, charId: "novice", machineId: "basic", deviceId: "");
            run.Stage = 1;
            var failure = new FailureOutcome { kind = "GAME_OVER", finalScore = 0 };

            MasteryTracker.ApplyRunEnd(p, run, failure);

            t.True(!p.Mastery.ContainsKey("dev") || p.Mastery["dev"].Count == 0, "[mastery-tracker] 장치 미장착이면 dev 축 자체를 건드리지 않음(웹 game.js:2627 if(r.device))");
            t.Eq(1, p.Mastery["char"]["novice"].Runs, "[mastery-tracker] char는 장치 무관하게 갱신됨");
        }

        private static void NullGuardsDoNothing(TestCtx t)
        {
            var p = new PlayerProfile();
            var run = S4TestHelpers.NewRun(3L);
            MasteryTracker.ApplyRunEnd(null, run, new FailureOutcome());
            MasteryTracker.ApplyRunEnd(p, null, new FailureOutcome());
            MasteryTracker.ApplyRunEnd(p, run, null);
            t.Eq(0, p.Mastery.Count, "[mastery-tracker] null 인자는 전부 가드되어 프로필이 변경되지 않음");
        }
    }

    // ── ⑦ ProfileDto 왕복 — Mastery(kind,id)별 병렬 배열 직렬화/역직렬화 동일성 ─────────────────────
    internal static class Tests_P3_Mastery_ProfileDtoRoundTrip
    {
        public static void Run(TestCtx t)
        {
            var p = new PlayerProfile();
            p.BumpMastery("char", "novice", 3, 1, 1000);
            p.BumpMastery("mac", "basic", 8, 5, 40000);
            p.BumpMastery("dev", "dev_bell", 100, 0, 0);
            // dev 쪽은 여러 번 눌러 dev 5레벨 만렙까지 확인(런 100회).
            for (int i = 0; i < 99; i++) p.BumpMastery("dev", "dev_bell", 100, 0, 0);

            var dto = ProfileDto.ToDto(p);
            t.Eq(3, dto.masteryKind.Length, "[mastery-dto] 3개 (kind,id) 행 직렬화(char/mac/dev 각 1개 id)");

            var restored = ProfileDto.FromDto(dto);
            t.Eq(1, restored.Mastery["char"]["novice"].Runs, "[mastery-dto] char(novice).Runs 왕복 동일");
            t.Eq(3, restored.Mastery["char"]["novice"].BestStage, "[mastery-dto] char(novice).BestStage 왕복 동일");
            t.Eq(1, restored.Mastery["char"]["novice"].BossClears, "[mastery-dto] char(novice).BossClears 왕복 동일");
            t.Eq(1000L, restored.Mastery["char"]["novice"].BestScore, "[mastery-dto] char(novice).BestScore 왕복 동일");
            t.Eq(-1, restored.Mastery["char"]["novice"].AscMax, "[mastery-dto] char(novice).AscMax 왕복 동일(-1)");

            t.Eq(5, restored.Mastery["mac"]["basic"].BossClears, "[mastery-dto] mac(basic).BossClears 왕복 동일");
            t.Eq(100, restored.Mastery["dev"]["dev_bell"].Runs, "[mastery-dto] dev(dev_bell).Runs 왕복 동일(100회)");

            t.Eq(5, restored.MasteryOf("dev", "dev_bell").Level, "[mastery-dto] 왕복 후에도 dev 만렙(Level=5) 재계산 정상");
            t.Eq(0, restored.MasteryOf("dev", "no_such_device").Level, "[mastery-dto] 왕복 후 미기록 id는 여전히 Level=0");

            // 빈 프로필 왕복 — masteryKind/Id가 빈 배열이어도 예외 없이 통과해야 함.
            var emptyRestored = ProfileDto.FromDto(new PlayerProfileDto());
            t.Eq(0, emptyRestored.Mastery.Count, "[mastery-dto] 빈 DTO 왕복 시 Mastery도 빈 상태");
        }
    }

    // ── ⑧ 런 종료 갱신 — 자동플레이 교차검증(합성 이벤트 아님, 실제 RunController 풀런) ─────────────
    // GameSession.FinishAction과 동일한 순서(StatTracker→Achievement→PlayerLevelTracker→MasteryTracker)를
    // 재현해(GameSession.cs는 Unity 전용이라 이 dotnet 프로젝트에서 직접 인스턴스화 불가 — 기존
    // Tests_PlayerLevel_RunBossClearsAutoplay와 동일한 이유·동일 패턴), 신규 프로필의 "첫 런"이라는
    // 결정론적 전제 위에서 profile.Mastery가 실제 종료 상태(run.Stage/RunBossClears/finalScore)와
    // 정확히 일치하는지 대조한다.
    internal static class Tests_P3_Mastery_RunEndAutoplayCrossCheck
    {
        public static void Run(TestCtx t)
        {
            RunOne(t, 615001L);
            RunOne(t, 615002L);
        }

        private static void RunOne(TestCtx tt, long seed)
        {
            var stat = S4TestHelpers.GenerousStat();
            var rc = new RunController("novice", "basic", "", seed, stat);

            RunEvent gameOverEvent = null;
            int guard = 0;
            int shopStep = 0;
            while (rc.State.Phase != RunPhase.GameOver && guard < 50_000)
            {
                guard++;
                IReadOnlyList<RunEvent> events;
                switch (rc.State.Phase)
                {
                    case RunPhase.Spin: events = rc.Do(new Spin(SpinMode.N)); break;
                    case RunPhase.PostSpin: events = rc.Do(new Continue()); break;
                    case RunPhase.NodeSelect: events = rc.Do(new ChooseNode(0)); break;
                    case RunPhase.EventAugment:
                    case RunPhase.EventRelic:
                    case RunPhase.EventAugLevel:
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
                        throw new InvalidOperationException("mastery-autoplay: 처리 불가 Phase=" + rc.State.Phase);
                }
                foreach (var e in events) if (e.type == "GAME_OVER") gameOverEvent = e;
            }

            tt.True(rc.State.Phase == RunPhase.GameOver, $"[mastery-autoplay seed={seed}] guard(50000) 내 게임오버 도달");
            tt.True(gameOverEvent != null && gameOverEvent.failure != null, $"[mastery-autoplay seed={seed}] GAME_OVER 이벤트/failure 확보");
            if (gameOverEvent?.failure == null) return;

            var profile = new PlayerProfile();
            var scratch = new StatTracker.RunScratch();
            StatTracker.Apply(profile, rc.State, new List<RunEvent> { gameOverEvent }, scratch);
            var newly = AchievementEngine.Evaluate(profile);
            PlayerLevelTracker.ApplyRunEnd(profile, rc.State, gameOverEvent.failure, newly.Count);
            MasteryTracker.ApplyRunEnd(profile, rc.State, gameOverEvent.failure);

            // 신규 프로필의 첫(유일한) 런이므로 Mastery 원시값은 이 런의 종료 상태와 1:1이어야 한다.
            var charM = profile.Mastery["char"]["novice"];
            tt.Eq(1, charM.Runs, $"[mastery-autoplay seed={seed}] 첫 런 후 char(novice).Runs=1");
            tt.Eq(rc.State.Stage, charM.BestStage, $"[mastery-autoplay seed={seed}] char.BestStage == 종료시점 run.Stage");
            tt.Eq(rc.State.RunBossClears, charM.BossClears, $"[mastery-autoplay seed={seed}] char.BossClears == run.RunBossClears");
            tt.Eq(gameOverEvent.failure.finalScore, charM.BestScore, $"[mastery-autoplay seed={seed}] char.BestScore == failure.finalScore");

            var macM = profile.Mastery["mac"]["basic"];
            tt.Eq(1, macM.Runs, $"[mastery-autoplay seed={seed}] mac(basic).Runs=1");
            tt.Eq(rc.State.Stage, macM.BestStage, $"[mastery-autoplay seed={seed}] mac.BestStage == 종료시점 run.Stage");

            // 독립 재계산(Formulas.MasteryLevel)로 profile.MasteryOf 결과 자체도 교차검증.
            int expectedCharLv = Formulas.MasteryLevel("char", charM.Runs, charM.BestStage, charM.BossClears, charM.BestScore, charM.AscMax);
            tt.Eq(expectedCharLv, profile.MasteryOf("char", "novice").Level, $"[mastery-autoplay seed={seed}] MasteryOf.Level == Formulas.MasteryLevel 독립 재계산");

            // 이번 런에서 장치를 장착했는지는 자동플레이 경로에 달렸으므로 무조건 참을 요구하지 않되,
            // "장착했다면 반드시 dev 축도 기록된다"는 일관성만 확인한다(장치 장착은 노드/이벤트 우연 —
            // NewRun("novice","basic","")은 무장착 시작이라 장착 여부는 런 도중 EVENT-6/DEVICE 노드에 달림).
            if (!string.IsNullOrEmpty(rc.State.Device))
            {
                tt.True(profile.Mastery.ContainsKey("dev") && profile.Mastery["dev"].ContainsKey(rc.State.Device),
                    $"[mastery-autoplay seed={seed}] 종료 시점 장치 장착 중이면 dev 축에도 기록됨({rc.State.Device})");
            }
        }
    }
}
