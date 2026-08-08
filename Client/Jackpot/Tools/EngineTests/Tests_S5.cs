using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // S5b 골든 테스트 — Engine/Profile/{PlayerProfile,StatTracker,AchievementEngine,ProfileDto}.cs 검증.
    // 작업 지시 항목: ①고정시드 런 자동플레이 → StatTracker 적용 → 대표 15키+ 카운터 검증
    // ②업적 트리거(임계 직전→직후) ③면허→장치 해금 ④DTO 왕복 ⑤계정 레벨 연동(Formulas.AccountExp).

    // ── ① RunController 고정시드 자동플레이 + StatTracker 카운터 검증 ──────────────────────────────
    // 정책: Tests_S4의 AutoPlay와 동일한 결정론적 선택(Spin(N) 반복·노드/오퍼/상점 0번·PostSpin은 포기)을
    // 쓰되, 매 Do()/생성자 LaunchEvents 직후 StatTracker.Apply를 호출하고, "그 이벤트 자체에서 직접 집계한
    // 기대값"과 PlayerProfile.Stats를 대조한다 — Kotlin 원본 수치를 복붙하지 않고, RunEvent라는 같은 데이터
    // 소스를 StatTracker와는 별도의(더 단순한) 방식으로 다시 세어 교차검증하는 방식(순환검증 방지).
    internal static class Tests_S5_StatTrackerAutoPlay
    {
        private sealed class Expected
        {
            public long TotalSpins, CherryTotal, BookTotal, StarTotal, GemTotal, SkullTotal, CoinTotal;
            public long Set4Plus, Jackpots, CrownJackpots, WildJackpots, AllinBusts;
            public long MaxSkullSpin, MaxCoinSpin, MaxCherrySpin, MaxBookSpin, MaxGemSpin;
            public long BossClears, CloseClears, LastSpinClears, ExactClears;
            public long Runs, ShopBuys, Gambles, PrismPicks;
        }

        public static void Run(TestCtx t)
        {
            RunOne(t, "novice", "basic", "", 777001L);
            RunOne(t, "honor", "basic", "", 424242L);
        }

        private static void RunOne(TestCtx tt, string charId, string machineId, string deviceId, long seed)
        {
            var stat = S4TestHelpers.GenerousStat();
            var profile = new PlayerProfile();
            var scratch = new StatTracker.RunScratch();
            var exp = new Expected();

            RunController rc;
            try
            {
                rc = new RunController(charId, machineId, deviceId, seed, stat);
            }
            catch (Exception ex)
            {
                tt.Fail($"[s5-autoplay seed={seed}]", "RunController 생성 실패: " + ex);
                return;
            }
            Tally(exp, rc.LaunchEvents);
            StatTracker.Apply(profile, rc.State, rc.LaunchEvents, scratch);

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
                        // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12): AUGLEVEL 노드도 같은
                        // PickOffer(0) 진입점을 공유한다(NodeEvents.PickOffer가 phase로 분기).
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
                            throw new InvalidOperationException("AutoPlay: 처리 불가 Phase=" + phase);
                    }
                    Tally(exp, events);
                    StatTracker.Apply(profile, rc.State, events, scratch);
                }
            }
            catch (Exception ex)
            {
                tt.Fail($"[s5-autoplay seed={seed}]", "자동플레이 중 예외: " + ex);
                return;
            }

            tt.True(rc.State.Phase == RunPhase.GameOver, $"[s5-autoplay seed={seed}] guard(50000) 내 게임오버 도달");
            tt.Eq(1L, exp.Runs, $"[s5-autoplay seed={seed}] 자동플레이 자체집계: 게임오버 정확히 1회");

            // ── PlayerProfile.Stats == 이벤트에서 직접 집계한 기대값(대표 20키, "15키+" 요구 충족) ──
            tt.Eq(exp.TotalSpins, profile.GetStat("totalSpins"), $"[s5-autoplay seed={seed}] totalSpins");
            tt.Eq(exp.CherryTotal, profile.GetStat("cherryTotal"), $"[s5-autoplay seed={seed}] cherryTotal");
            tt.Eq(exp.BookTotal, profile.GetStat("bookTotal"), $"[s5-autoplay seed={seed}] bookTotal");
            tt.Eq(exp.StarTotal, profile.GetStat("starTotal"), $"[s5-autoplay seed={seed}] starTotal");
            tt.Eq(exp.GemTotal, profile.GetStat("gemTotal"), $"[s5-autoplay seed={seed}] gemTotal");
            tt.Eq(exp.SkullTotal, profile.GetStat("skullTotal"), $"[s5-autoplay seed={seed}] skullTotal");
            tt.Eq(exp.CoinTotal, profile.GetStat("coinTotal"), $"[s5-autoplay seed={seed}] coinTotal");
            tt.Eq(exp.Set4Plus, profile.GetStat("set4Plus"), $"[s5-autoplay seed={seed}] set4Plus");
            tt.Eq(exp.Jackpots, profile.GetStat("jackpots"), $"[s5-autoplay seed={seed}] jackpots");
            tt.Eq(exp.CrownJackpots, profile.GetStat("crownJackpots"), $"[s5-autoplay seed={seed}] crownJackpots");
            tt.Eq(exp.WildJackpots, profile.GetStat("wildJackpots"), $"[s5-autoplay seed={seed}] wildJackpots");
            tt.Eq(exp.AllinBusts, profile.GetStat("allinBusts"), $"[s5-autoplay seed={seed}] allinBusts (모드 N만 사용 → 0)");
            tt.Eq(exp.MaxSkullSpin, profile.GetStat("maxSkullSpin"), $"[s5-autoplay seed={seed}] maxSkullSpin");
            tt.Eq(exp.MaxCoinSpin, profile.GetStat("maxCoinSpin"), $"[s5-autoplay seed={seed}] maxCoinSpin");
            tt.Eq(exp.MaxCherrySpin, profile.GetStat("maxCherrySpin"), $"[s5-autoplay seed={seed}] maxCherrySpin");
            tt.Eq(exp.MaxBookSpin, profile.GetStat("maxBookSpin"), $"[s5-autoplay seed={seed}] maxBookSpin");
            tt.Eq(exp.MaxGemSpin, profile.GetStat("maxGemSpin"), $"[s5-autoplay seed={seed}] maxGemSpin");
            tt.Eq(exp.BossClears, profile.GetStat("bossClears"), $"[s5-autoplay seed={seed}] bossClears");
            tt.Eq(exp.CloseClears, profile.GetStat("closeClears"), $"[s5-autoplay seed={seed}] closeClears");
            tt.Eq(exp.LastSpinClears, profile.GetStat("lastSpinClears"), $"[s5-autoplay seed={seed}] lastSpinClears");
            tt.Eq(exp.ExactClears, profile.GetStat("exactClears"), $"[s5-autoplay seed={seed}] exactClears");
            tt.Eq(exp.Runs, profile.GetStat("runs"), $"[s5-autoplay seed={seed}] runs");
            tt.Eq(exp.ShopBuys, profile.GetStat("shopBuys"), $"[s5-autoplay seed={seed}] shopBuys");
            tt.Eq(exp.Gambles, profile.GetStat("gambles"), $"[s5-autoplay seed={seed}] gambles");
            tt.Eq(exp.PrismPicks, profile.GetStat("prismPicks"), $"[s5-autoplay seed={seed}] prismPicks");

            tt.True(profile.BestStage >= 1, $"[s5-autoplay seed={seed}] bestStage >= 1");
            tt.True(profile.BestScore >= 0, $"[s5-autoplay seed={seed}] bestScore >= 0");
        }

        // 이벤트 목록에서 "실제 스핀"(deviceId 없음 && spin.result != null — StatTracker.ApplySpinIncrements와
        // 동일 조건)만 골라 대표 카운터를 직접 센다. 이 로직은 StatTracker 내부 구현을 베끼지 않고 RunEvent
        // 원본 필드(res.cells/mode/clear.*)만 본다 — 순환검증이 아니라 같은 원천 데이터의 독립 재계산이다.
        private static void Tally(Expected exp, IReadOnlyList<RunEvent> events)
        {
            foreach (var e in events)
            {
                bool realSpin = string.IsNullOrEmpty(e.deviceId) && e.spin?.result != null;
                if (realSpin && (e.type == "SPIN_RESULT" || e.type == "POST_SPIN" || e.type == "REVIVED" ||
                                  e.type == "STAGE_CLEARED" || e.type == "GAME_OVER"))
                {
                    var res = e.spin.result;
                    exp.TotalSpins++;
                    long CellCount(string id) => res.cells.Count(c => c.sym.id == id);
                    exp.CherryTotal += CellCount("cherry");
                    exp.BookTotal += CellCount("book");
                    exp.StarTotal += CellCount("star");
                    exp.GemTotal += CellCount("gem");
                    exp.SkullTotal += CellCount("skull");
                    exp.CoinTotal += CellCount("coin");
                    if (res.bestSetCount >= 4) exp.Set4Plus++;
                    if (res.jackpotSym != null) exp.Jackpots++;
                    if (res.jackpotSym == "crown") exp.CrownJackpots++;
                    if (res.jackpotSym != null && res.cells.Any(c => c.sym.special == Sp.WILD)) exp.WildJackpots++;
                    if (e.spin.mode == SpinMode.Allin && res.skulls >= 2) exp.AllinBusts++;
                    exp.MaxSkullSpin = Math.Max(exp.MaxSkullSpin, CellCount("skull"));
                    exp.MaxCoinSpin = Math.Max(exp.MaxCoinSpin, CellCount("coin"));
                    exp.MaxCherrySpin = Math.Max(exp.MaxCherrySpin, CellCount("cherry"));
                    exp.MaxBookSpin = Math.Max(exp.MaxBookSpin, CellCount("book"));
                    exp.MaxGemSpin = Math.Max(exp.MaxGemSpin, CellCount("gem"));
                }

                if (e.type == "STAGE_CLEARED" && e.clear != null)
                {
                    if (e.clear.boss) exp.BossClears++;
                    if (e.clear.leftover <= 10) exp.CloseClears++;
                    if (e.clear.lastSpinClear) exp.LastSpinClears++;
                    if (e.spin != null && e.spin.newExp == e.spin.quota) exp.ExactClears++;
                }
                if (e.type == "GAME_OVER") exp.Runs++;
                if (e.type == "NODE_RESOLVED" && e.node == NodeKind.Gamble) exp.Gambles++;
                if (e.type == "SHOP_PURCHASED") exp.ShopBuys++;
                if (e.type == "PERK_GRANTED")
                {
                    var perk = Perks.ById(e.perkId);
                    if (perk != null && perk.tier == Tier.PRISM) exp.PrismPicks++;
                }
            }
        }
    }

    // ── ② 업적 트리거 — 임계 직전(불충족)→직후(충족) ────────────────────────────────────────────
    // WEB_PARITY P3-2(업적 34종 교체) — 구 테스트가 쓰던 intro_firstSpin/spin100(key=totalSpins)는
    // 새 34종에 없다(totalSpins를 req.key로 쓰는 업적이 웹에 없음). 같은 "한 키·다른 임계값" 패턴을
    // 유지하도록 jackpot1(key=jackpots,th=1)/jackpot10(key=jackpots,th=10)으로 교체했다.
    internal static class Tests_S5_AchievementTrigger
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();

            // jackpot1: key=jackpots, threshold=1.
            profile.Stats["jackpots"] = 0;
            var newlyBefore = AchievementEngine.Evaluate(profile);
            t.True(!newlyBefore.Any(a => a.id == "jackpot1"), "[ach-trigger] jackpots=0 — jackpot1 미달성");
            t.True(!profile.AchievedIds.Contains("jackpot1"), "[ach-trigger] jackpots=0 — AchievedIds에 없음");

            profile.Stats["jackpots"] = 1;
            var newlyAt = AchievementEngine.Evaluate(profile);
            t.True(newlyAt.Any(a => a.id == "jackpot1"), "[ach-trigger] jackpots=1 — jackpot1 신규 달성 목록에 포함");
            t.True(profile.AchievedIds.Contains("jackpot1"), "[ach-trigger] jackpots=1 — AchievedIds에 추가됨");

            // 재평가 시 이미 달성한 업적은 newly에 다시 나오지 않아야 함(웹 "!p.unlocked.includes(a.id)" 필터).
            var newlyAgain = AchievementEngine.Evaluate(profile);
            t.True(!newlyAgain.Any(a => a.id == "jackpot1"), "[ach-trigger] 재평가 시 중복 신규달성 없음");

            // 문턱값이 다른 두 번째 업적(jackpot10, threshold=10, 같은 key)으로 동일 패턴 재확인.
            profile.Stats["jackpots"] = 9;
            var newly9 = AchievementEngine.Evaluate(profile);
            t.True(!newly9.Any(a => a.id == "jackpot10"), "[ach-trigger] jackpots=9 — jackpot10 미달성");
            profile.Stats["jackpots"] = 10;
            var newly10 = AchievementEngine.Evaluate(profile);
            t.True(newly10.Any(a => a.id == "jackpot10"), "[ach-trigger] jackpots=10 — jackpot10 신규 달성");
        }
    }

    // ── ③ 업적 달성 → 장치 영구해금 반영(웹 ACH_DEVICE_REWARD) ─────────────────────────────────
    // WEB_PARITY P3-2 — 구 lic_safe(AND 조건 2개, 파생키 lic_dev_safe 경유) 체계를 웹 방식(단일
    // key>=threshold, Devices.cs의 unlockAch가 업적 id를 직접 가리킴)으로 교체했다. cherry100
    // (key=cherryTotal,th=100) → dev_safe로 동일한 검증 뼈대를 재사용한다.
    internal static class Tests_S5_AchievementDeviceReward
    {
        public static void Run(TestCtx t)
        {
            var devSafe = Devices.ById("dev_safe");
            t.True(devSafe != null && devSafe.unlockAch == "cherry100",
                "[ach-device] dev_safe.unlockAch == \"cherry100\" (Devices.cs 전제 확인, 웹 ACH_DEVICE_REWARD)");

            var profile = new PlayerProfile();
            profile.Stats["cherryTotal"] = 99; // cherry100 조건: cherryTotal>=100
            AchievementEngine.Evaluate(profile);
            t.True(!profile.AchievedIds.Contains("cherry100"), "[ach-device] cherryTotal=99<100 — cherry100 미달성");
            t.True(!profile.OwnedDevices.Contains("dev_safe"), "[ach-device] 미달성 상태에서 dev_safe 미보유");
            t.True(!profile.IsDeviceUnlocked(devSafe), "[ach-device] IsDeviceUnlocked == false");

            profile.Stats["cherryTotal"] = 100;
            var newly = AchievementEngine.Evaluate(profile);
            t.True(profile.AchievedIds.Contains("cherry100"), "[ach-device] cherryTotal=100>=100 — cherry100 달성");
            t.True(newly.Any(a => a.id == "cherry100"), "[ach-device] cherry100이 신규달성 목록에 포함");
            t.True(profile.OwnedDevices.Contains("dev_safe"), "[ach-device] AchievementEngine.Evaluate가 dev_safe를 OwnedDevices에 반영");
            t.True(profile.IsDeviceUnlocked(devSafe), "[ach-device] IsDeviceUnlocked == true (업적 달성 반영 후)");

            // 대응 장치가 없는 업적(예: jackpot10엔 매핑이 없다 — jackpot1만 dev_subreel과 매핑됨)은
            // OwnedDevices에 아무것도 추가하지 않아야 한다(회귀 확인 — 범용화된 Evaluate가 무관한
            // 업적까지 장치를 지급하지 않는지).
            var profile2 = new PlayerProfile();
            profile2.Stats["jackpots"] = 10; // jackpot1(th1)·jackpot10(th10) 둘 다 충족, jackpot1→dev_subreel만 지급돼야 함
            AchievementEngine.Evaluate(profile2);
            t.True(profile2.OwnedDevices.Contains("dev_subreel"), "[ach-device] jackpot1 달성 → dev_subreel 지급");
            t.Eq(1, profile2.OwnedDevices.Count, "[ach-device] jackpot10에는 매핑된 장치가 없어 정확히 1개만 지급됨");
        }
    }

    // ── ③b 드랍 전용 장치(unlockAch="") — 업적 경로로는 절대 해금되지 않고, OwnedDevices 직접 추가
    // (런 중 장치 드랍)로만 해금된다(Opus 2차 검수·Fable 결정 4번, Devices.cs 헤더 각주) ───────────
    internal static class Tests_S5_DropOnlyDevicesNeverAchievementUnlocked
    {
        private static readonly string[] DropOnlyIds = { "dev_syllabus", "dev_holdfile", "dev_retake", "dev_major" };

        public static void Run(TestCtx t)
        {
            foreach (var id in DropOnlyIds)
            {
                var dev = Devices.ById(id);
                t.True(dev != null && string.IsNullOrEmpty(dev.unlockAch), $"[drop-only] {id}.unlockAch == \"\" (Devices.cs 전제 확인)");
                if (dev == null) continue;

                // 34종 업적 전부를 큰 여유로 만족시키는 "전지전능" 스탯을 채워도(업적 경로로는) 드랍
                // 전용 장치는 해금되면 안 된다 — unlockAch가 빈 문자열이라 어떤 업적 id와도 매치되지 않는다.
                var profile = new PlayerProfile();
                foreach (var a in Achievements.All)
                {
                    if (a.req == null || a.req.Length == 0) continue;
                    profile.Stats[a.req[0].key] = a.req[0].value + 1000;
                }
                var newly = AchievementEngine.Evaluate(profile);
                t.True(newly.Count > 0, $"[drop-only] {id} 사전조건: 전지전능 스탯으로 실제 업적들이 신규 달성됨(테스트 전제 확인)");
                t.True(!profile.OwnedDevices.Contains(id), $"[drop-only] {id}: 34종 업적을 전부 만족해도 업적 경로로는 OwnedDevices에 추가되지 않음");
                t.True(!profile.IsDeviceUnlocked(dev), $"[drop-only] {id}: IsDeviceUnlocked == false (업적 경로 없음)");

                // 런 중 드랍(NODE_RESOLVED.deviceGrantedId → StatTracker → OwnedDevices 직접 추가)과
                // 동일한 최종 상태 — OwnedDevices에 들어가면 그것만으로 해금된다.
                profile.OwnedDevices.Add(id);
                t.True(profile.IsDeviceUnlocked(dev), $"[drop-only] {id}: OwnedDevices 직접 추가(드랍 경로) 후 IsDeviceUnlocked == true");
            }
        }
    }

    // ── ③c 드랍으로 보유한 드랍 전용 장치도 devicesOwned 스탯에 정상 카운트된다 ──────────────────
    // (StatTracker.ComputeDevicesOwned의 "OwnedDevices 소속 여부"를 unlockAch 공백 체크보다 먼저
    // 보게 한 순서수정 회귀 확인, Opus 2차 검수 필수④ 부수 발견)
    internal static class Tests_S5_DropOnlyDeviceCountsTowardDevicesOwned
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(41L);
            var scratch = new StatTracker.RunScratch();
            profile.OwnedDevices.Add("dev_holdfile"); // 런 중 드랍으로 이미 보유했다고 가정

            long before = profile.GetStat("devicesOwned");
            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = new FailureOutcome { kind = "GAME_OVER", finalScore = 0 } } },
                scratch);
            t.True(profile.GetStat("devicesOwned") > before,
                "[drop-only] 드랍으로 보유한 dev_holdfile(unlockAch=\"\")이 devicesOwned에 정상 카운트됨");
        }
    }

    // ── ④ PlayerProfile ↔ PlayerProfileDto 왕복(매핑 동일성) ───────────────────────────────────
    // JsonUtility(Unity 전용)로 실제 텍스트 직렬화를 왕복하는 검증은 dotnet 콘솔 하네스에서 불가능하다
    // (작업 지시서 "Engine 폴더 밖이므로 dotnet 테스트에는 포함되지 않는다"와 동일 취지) — 순수 C# 매핑
    // 단계(ProfileDto.ToDto/FromDto)만 여기서 검증한다.
    internal static class Tests_S5_ProfileDtoRoundTrip
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["totalSpins"] = 123;
            profile.Stats["cherryTotal"] = 456;
            profile.Stats["bldCat_성장형"] = 2; // 유니코드 키 왕복 확인
            profile.Stats["cstage_novice"] = 12;
            profile.Stats["lic_dev_safe"] = 0; // 파생키가 아니라 실수로 원재료에 섞여도 왕복은 보존돼야 함
            profile.AchievedIds.Add("intro_firstSpin");
            profile.AchievedIds.Add("lic_safe");
            profile.OwnedDevices.Add("dev_safe");
            profile.TotalScore = 98765;
            profile.BestChar = "novice";
            profile.BestMachine = "basic";
            profile.LastPlayedAtUnixMs = 1_700_000_000_000L;
            profile.PinnedChallenge = "ch_disarm";
            profile.LastCombo = "novice,basic,dev_safe,";
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §1-A #16, 웹 profile.tutDone) — PlayerProfile.TutDone.
            profile.MarkTutorialDone();
            // Opus 2차검수(P7-1, 2026-08-09) [LOW]⑤ — 승천(P6)/심화(P7-1) 최고기록 필드도 이 공용
            // 왕복 테스트에서 함께 검증한다(그동안 별도 테스트가 없었음). AscMax=3(0이 아닌 값 —
            // ProfileDto.FromDto의 "(ascMax==0 && graduations==0)→-1" 마이그레이션 가드를 건드리지
            // 않는 값으로 골라 이 테스트의 관심사(원시 왕복)를 그 가드 로직과 분리한다).
            profile.AscMax = 3;
            profile.BestAscScore = 4200;
            profile.BestAscLevel = 3;
            profile.BestDeepScore = 5300;
            profile.BestDeepStage = 11;

            var dto = ProfileDto.ToDto(profile);
            var restored = ProfileDto.FromDto(dto);

            t.Eq(profile.Stats.Count, restored.Stats.Count, "[dto-roundtrip] Stats 키 개수 동일");
            foreach (var kv in profile.Stats)
                t.Eq(kv.Value, restored.GetStat(kv.Key), $"[dto-roundtrip] Stats[{kv.Key}] 왕복 동일");

            t.Eq(profile.AchievedIds.Count, restored.AchievedIds.Count, "[dto-roundtrip] AchievedIds 개수 동일");
            foreach (var id in profile.AchievedIds)
                t.True(restored.AchievedIds.Contains(id), $"[dto-roundtrip] AchievedIds 보존: {id}");

            t.Eq(profile.OwnedDevices.Count, restored.OwnedDevices.Count, "[dto-roundtrip] OwnedDevices 개수 동일");
            foreach (var id in profile.OwnedDevices)
                t.True(restored.OwnedDevices.Contains(id), $"[dto-roundtrip] OwnedDevices 보존: {id}");

            t.Eq(profile.TotalScore, restored.TotalScore, "[dto-roundtrip] TotalScore 동일");
            t.Eq(profile.BestChar, restored.BestChar, "[dto-roundtrip] BestChar 동일");
            t.Eq(profile.BestMachine, restored.BestMachine, "[dto-roundtrip] BestMachine 동일");
            t.Eq(profile.LastPlayedAtUnixMs, restored.LastPlayedAtUnixMs, "[dto-roundtrip] LastPlayedAtUnixMs 동일");
            t.Eq(profile.PinnedChallenge, restored.PinnedChallenge, "[dto-roundtrip] PinnedChallenge 동일");
            t.Eq(profile.LastCombo, restored.LastCombo, "[dto-roundtrip] LastCombo 동일");
            t.True(restored.TutDone, "[dto-roundtrip] TutDone 왕복 보존(true)");
            // 승천(P6)/심화(P7-1) 최고기록 왕복.
            t.Eq(profile.AscMax, restored.AscMax, "[dto-roundtrip] AscMax 동일(P6)");
            t.Eq(profile.BestAscScore, restored.BestAscScore, "[dto-roundtrip] BestAscScore 동일(P6)");
            t.Eq(profile.BestAscLevel, restored.BestAscLevel, "[dto-roundtrip] BestAscLevel 동일(P6)");
            t.Eq(profile.BestDeepScore, restored.BestDeepScore, "[dto-roundtrip] BestDeepScore 동일(P7-1)");
            t.Eq(profile.BestDeepStage, restored.BestDeepStage, "[dto-roundtrip] BestDeepStage 동일(P7-1)");

            // 빈 프로필(최초 실행 상태)도 왕복이 안전한지 확인.
            var emptyDto = ProfileDto.ToDto(new PlayerProfile());
            var emptyRestored = ProfileDto.FromDto(emptyDto);
            t.Eq(0, emptyRestored.Stats.Count, "[dto-roundtrip] 빈 프로필 Stats.Count == 0");
            t.Eq(0, emptyRestored.AchievedIds.Count, "[dto-roundtrip] 빈 프로필 AchievedIds.Count == 0");
            t.True(!emptyRestored.TutDone, "[dto-roundtrip] 빈 프로필 TutDone 기본값 false");
            t.Eq(-1, emptyRestored.AscMax, "[dto-roundtrip] 빈 프로필 AscMax 기본값 -1(미졸업)");
            t.Eq(0L, emptyRestored.BestAscScore, "[dto-roundtrip] 빈 프로필 BestAscScore 기본값 0");
            t.Eq(0, emptyRestored.BestAscLevel, "[dto-roundtrip] 빈 프로필 BestAscLevel 기본값 0");
            t.Eq(0L, emptyRestored.BestDeepScore, "[dto-roundtrip] 빈 프로필 BestDeepScore 기본값 0(P7-1)");
            t.Eq(0, emptyRestored.BestDeepStage, "[dto-roundtrip] 빈 프로필 BestDeepStage 기본값 0(P7-1)");

            // FromDto(null)도 예외 없이 빈 프로필을 반환해야 한다(손상된 저장 파일 방어, ProfileStore.cs 계약).
            var nullRestored = ProfileDto.FromDto(null);
            t.True(nullRestored != null, "[dto-roundtrip] FromDto(null) — 예외 없이 빈 프로필 반환");
        }
    }

    // ── WEB_PARITY P1 ④ Opus 1차검수 수정②(2026-08-07) — 장치 영구 보유 왕복 ──────────────────────
    // NODE_RESOLVED(deviceGrantedId)(DEVICE 노드/EVENT-6) → StatTracker.Apply → PlayerProfile.
    // OwnedDevices 반영 → ProfileDto 저장/로드 후에도 유지되는지 확인한다. GameSession(RunController→
    // StatTracker 브릿지 호출부)은 Unity 어셈블리(Assets/JackpotRun/Scripts/Game)라 순수 C# EngineTests
    // 프로젝트에서 직접 인스턴스화할 수 없다 — 대신 NodeEvents가 실제로 만드는 RunEvent 형태(EVENT-6/
    // DEVICE 노드 둘 다 type="NODE_RESOLVED", deviceGrantedId 채움)를 그대로 손으로 구성해 StatTracker
    // 레벨부터 DTO 왕복까지 검증한다 — GameSession.Do/DoGiveUp은 이 StatTracker.Apply를 그대로 호출할
    // 뿐이라(GameSession.cs 참조) 이 레벨 검증으로 실질 커버리지는 동일하다.
    internal static class Tests_S5_DeviceGrantPersistence
    {
        public static void Run(TestCtx t)
        {
            EventNodeGrantPersistsThroughDto(t);
            DeviceNodeGrantPersistsThroughDto(t);
        }

        // EVENT 10분기표 6번 분기(NodeEvents.ResolveEventTable case 6)의 실제 결과 형태를 재현.
        private static void EventNodeGrantPersistsThroughDto(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(8001L);
            var scratch = new StatTracker.RunScratch();
            t.True(!profile.OwnedDevices.Contains("dev_flame"), "[device-persist:event] 사전조건: 미보유");

            var ev = new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Event, eventRoll = 6, deviceGrantedId = "dev_flame" };
            StatTracker.Apply(profile, run, new List<RunEvent> { ev }, scratch);
            t.True(profile.OwnedDevices.Contains("dev_flame"), "[device-persist:event] StatTracker가 OwnedDevices에 반영");

            var dto = ProfileDto.ToDto(profile);
            var restored = ProfileDto.FromDto(dto);
            t.True(restored.OwnedDevices.Contains("dev_flame"), "[device-persist:event] DTO 저장/로드 왕복 후에도 유지");
        }

        // DEVICE 노드 확정(NodeEvents.TakeDevice)의 실제 결과 형태를 재현 — 장착/미장착 어느 쪽이든
        // deviceGrantedId는 채워진다(NodeEvents.cs TakeDevice 참조).
        private static void DeviceNodeGrantPersistsThroughDto(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(8002L);
            var scratch = new StatTracker.RunScratch();

            var ev = new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Device, deviceGrantedId = "dev_seal", deviceId = "dev_seal" };
            StatTracker.Apply(profile, run, new List<RunEvent> { ev }, scratch);
            t.True(profile.OwnedDevices.Contains("dev_seal"), "[device-persist:node] StatTracker가 OwnedDevices에 반영(장착/미장착 무관)");

            var dto = ProfileDto.ToDto(profile);
            var restored = ProfileDto.FromDto(dto);
            t.True(restored.OwnedDevices.Contains("dev_seal"), "[device-persist:node] DTO 저장/로드 왕복 후에도 유지");
            t.Eq(1, restored.OwnedDevices.Count, "[device-persist:node] 정확히 1개만 보유(중복/누락 없음)");
        }
    }

    // ── ⑤ 계정 레벨 연동(Formulas.AccountExp/ExpToLevel) — 손 계산 골든값 ──────────────────────────
    // WEB_PARITY P3-2 — ComposeStat은 더 이상 "accountLevel" 파생키를 계산하지 않는다(아무 소비처가
    // 없어 제거, AchievementEngine.cs 헤더 각주). Formulas.AccountExp/AccountLevel 함수 자체는 그대로
    // 살아 있으므로(작업 지시 6번), 이 테스트는 ComposeStat 대신 그 함수를 직접 호출해 ④ 컴포넌트가
    // 새 34종 테이블로도 여전히 정확히 동작하는지 확인한다. 구 테스트가 쓰던 totalSpins 키는 새 34종
    // 어디에도 없어(req.key 목록에 없음) jackpots 키(jackpot1 th1·jackpot10 th10, 같은 손계산 합계 40이
    // 나오도록)로 교체했다.
    internal static class Tests_S5_AccountLevelIntegration
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["jackpots"] = 10; // 그 외 키는 전부 비움

            var composed = AchievementEngine.ComposeStat(profile);
            t.True(!composed.ContainsKey("accountLevel"), "[account-level] ComposeStat이 더 이상 accountLevel 파생키를 채우지 않음(소비처 없어 제거)");

            // 손 계산 (Formulas.AccountExp, Core/Formulas.cs L150-207 그대로):
            //  ① 마일스톤(bestStage 없음=0): 0
            //  ② bossClears*8 상한120 (bossClears 없음=0): 0
            //  ③ runs*3 상한90 (runs 없음=0): 0
            //  ④ 업적 tier합 — jackpots=10인 상태에서 새 34종 중 key=="jackpots"인 것만 기여:
            //     jackpot1(threshold=1,브론즈=20) + jackpot10(threshold=10,브론즈=20) = 40
            //     (34종 전부 tier="브론즈" 균일값이라 AchTierExp=20 — Achievements.cs 헤더 각주 참조)
            //  ⑤ bld_/bc_ 접두 키 없음: 0   ⑥ cstage_/mstage_ 접두 키 없음: 0
            //  합계 accountExp = 0+0+0+40+0+0 = 40
            //  level = 1 + floor(sqrt(40/22.0)) = 1 + floor(sqrt(1.8181...)) = 1 + floor(1.3484...) = 1 + 1 = 2
            var achievementExpTable = Achievements.All.Select(a => (a.req[0].key, a.req[0].value, a.tier)).ToList();
            long exp = Formulas.AccountExp(composed, achievementExpTable);
            t.Eq(40L, exp, "[account-level] jackpots=10 → accountExp = 40 (손계산: jackpot1 20 + jackpot10 20)");
            t.Eq(2, Formulas.ExpToLevel(exp), "[account-level] exp=40 → level = 1+floor(sqrt(40/22)) = 2 (손계산)");
            t.Eq(2, Formulas.AccountLevel(composed, achievementExpTable), "[account-level] Formulas.AccountLevel(직접호출) == 2 (독립 재계산과 일치)");

            // 빈 프로필(전부 0)은 레벨 1(하한)이어야 한다.
            var empty = AchievementEngine.ComposeStat(new PlayerProfile());
            t.Eq(1, Formulas.AccountLevel(empty, achievementExpTable), "[account-level] 빈 프로필 AccountLevel == 1 (하한)");
        }
    }

    // ── ⑥ seen_<perkId> 그랜드파더 게이트 추적 (2026-07-31 Fable 후속지시 반영) ────────────────────
    // Shop.PerkUnlocked의 1순위 검사(seen_+id > 0이면 무조건 해금)가 StatTracker의 마킹과 실제로 맞물려
    // 동작하는지 — PERK_GRANTED/NODE_RESOLVED(Event)/SHOP_PURCHASED 3개 사건 각각에서 확인하고, RISK
    // 노드는 원본대로 마킹되지 않아야 함을 함께 검증한다. Shop.PerkUnlocked(internal)를 재사용해 "게이트가
    // 실제로 열리는지"까지 확인한다(단순 Stats 값 확인에 그치지 않음 — StatTracker 내부 구현을 베끼지
    // 않고, StatTracker가 만든 결과를 재사용 가능한 기존 함수로 독립 재확인).
    internal static class Tests_S5_SeenGateTracking
    {
        // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #13, Shop.cs 헤더 각주) — Schools/AccountLevel 게이트가
        // 폐기되면서 "빈 stat에서 잠긴 퍽"은 이제 unlockLevel>0인 8종(증강4·유물4)뿐이다. 이 헬퍼는 여전히
        // "빈 stat(=Lv1)에서 잠긴 퍽 1개"를 고르는 역할이라 자연히 이 8종만 반환한다(seen_ 그랜드파더는
        // 더 이상 없다 — 아래 각 테스트가 "seen_는 마킹되지만 잠김은 유지"로 갱신됨).
        private static Perk PickGatedPerk(IReadOnlyList<Perk> pool, IReadOnlyDictionary<string, long> emptyStat)
        {
            foreach (var p in pool)
                if (!Shop.PerkUnlocked(p, emptyStat))
                    return p;
            throw new InvalidOperationException("빈 stat에서도 전부 해금된 퍽 풀 — 테스트 전제 깨짐");
        }

        public static void Run(TestCtx t)
        {
            PerkGrantedMarksSeen(t);
            NodeEventGrantMarksSeen(t);
            RiskNodeDoesNotMarkSeen(t);
            ShopPurchaseMarksSeenOnlyForPerks(t);
            RetakeOfferAttribution(t);
        }

        private static void PerkGrantedMarksSeen(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(1L);
            var scratch = new StatTracker.RunScratch();
            var perk = PickGatedPerk(Perks.Augments, profile.Stats);

            t.True(!Shop.PerkUnlocked(perk, profile.Stats), $"[seen-gate] {perk.id} 사전 미해금(빈 stat)");
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "PERK_GRANTED", perkId = perk.id } }, scratch);
            t.True(profile.GetStat("seen_" + perk.id) > 0, $"[seen-gate] PERK_GRANTED 후 seen_{perk.id} > 0");
            // 웹 파리티 P3-4 — seen_ 그랜드파더는 폐기됐다(Shop.cs 헤더 각주). {perk.id}는 unlockLevel 퍽이라
            // seen_ 마킹과 무관하게 PlayerLevel 미달이면 계속 잠긴다(레벨업 외엔 해금 수단 없음).
            t.True(!Shop.PerkUnlocked(perk, profile.Stats), $"[seen-gate] PERK_GRANTED 후에도 {perk.id}는 여전히 잠김(그랜드파더 폐기)");
        }

        private static void NodeEventGrantMarksSeen(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(2L);
            var scratch = new StatTracker.RunScratch();
            var relic = PickGatedPerk(Perks.Relics, profile.Stats);
            var aug = PickGatedPerk(Perks.Augments, profile.Stats);

            // case 7(유물 발견) — seen_ 마킹은 그대로 확인하되(StatTracker 책임), 웹 파리티 P3-4로
            // 그랜드파더가 폐기돼 unlockLevel 퍽은 여전히 잠긴 채다(Shop.cs 헤더 각주).
            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Event, relicGrantedId = relic.id } }, scratch);
            t.True(profile.GetStat("seen_" + relic.id) > 0, $"[seen-gate] EVENT case7 유물발견 후 seen_{relic.id} > 0");
            t.True(!Shop.PerkUnlocked(relic, profile.Stats), $"[seen-gate] EVENT case7 이후에도 {relic.id}는 여전히 잠김(그랜드파더 폐기)");

            // case 8(증강 발견, 25% 특별이벤트는 relicGrantedId도 함께 채워지는 케이스까지 커버하도록 augmentGrantedId만 단독 확인)
            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Event, augmentGrantedId = aug.id } }, scratch);
            t.True(profile.GetStat("seen_" + aug.id) > 0, $"[seen-gate] EVENT case8 증강발견 후 seen_{aug.id} > 0");
            t.True(!Shop.PerkUnlocked(aug, profile.Stats), $"[seen-gate] EVENT case8 이후에도 {aug.id}는 여전히 잠김(그랜드파더 폐기)");
        }

        private static void RiskNodeDoesNotMarkSeen(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(3L);
            var scratch = new StatTracker.RunScratch();
            var aug = PickGatedPerk(Perks.Augments, profile.Stats);

            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Risk, augmentGrantedId = aug.id, curseGrantedId = "curse_dummy" } },
                scratch);
            t.Eq(0L, profile.GetStat("seen_" + aug.id), $"[seen-gate] RISK 노드는 {aug.id}를 seen_ 마킹하지 않음(원본 그대로)");
            t.Eq(0L, profile.GetStat("seen_curse_dummy"), "[seen-gate] CURSE 지급도 seen_ 마킹 대상 아님");
        }

        private static void ShopPurchaseMarksSeenOnlyForPerks(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(4L);
            var scratch = new StatTracker.RunScratch();
            var aug = PickGatedPerk(Perks.Augments, profile.Stats);
            var item = Items.All[0];

            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "SHOP_PURCHASED", shopBought = new ShopEntry { kind = 'A', id = aug.id, price = 24 } } },
                scratch);
            t.True(profile.GetStat("seen_" + aug.id) > 0, $"[seen-gate] 상점 증강구매 후 seen_{aug.id} > 0");
            t.True(!Shop.PerkUnlocked(aug, profile.Stats), $"[seen-gate] 상점 증강구매 후에도 {aug.id}는 여전히 잠김(그랜드파더 폐기, 웹 파리티 P3-4)");

            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "SHOP_PURCHASED", shopBought = new ShopEntry { kind = 'I', id = item.id, price = item.coinCost } } },
                scratch);
            t.Eq(0L, profile.GetStat("seen_" + item.id), $"[seen-gate] 아이템 구매는 seen_ 마킹 대상 아님({item.id})");
        }

        // PERK_OFFER.offerRetake로 재추첨 성공/일반 오퍼를 구분해 deviceUses에 정확히 귀속하는지 확인
        // (Fable 후속지시 2번 — RunEvent.offerRetake 필드 추가에 대한 StatTracker 반영).
        private static void RetakeOfferAttribution(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(5L);
            var scratch = new StatTracker.RunScratch();

            long before = profile.GetStat("deviceUses");
            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "PERK_OFFER", node = NodeKind.Augment, offerRetake = false } }, scratch);
            t.Eq(before, profile.GetStat("deviceUses"), "[seen-gate] 최초 노드 오퍼(offerRetake=false)는 deviceUses 미증가");

            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "PERK_OFFER", node = NodeKind.Augment, offerRetake = true } }, scratch);
            t.Eq(before + 1, profile.GetStat("deviceUses"), "[seen-gate] 재추첨 성공 오퍼(offerRetake=true)는 deviceUses +1");

            // RETAKE_EMPTY(재추첨 실패)도 여전히 정확히 잡히는지 회귀 확인.
            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "RETAKE_EMPTY", node = NodeKind.Relic } }, scratch);
            t.Eq(before + 2, profile.GetStat("deviceUses"), "[seen-gate] RETAKE_EMPTY도 deviceUses +1 (회귀 확인)");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // Opus 1차 검수 반영 테스트 보강 (2026-07-31) — H1/M1~M4/L1~L4 수정에 대한 회귀 확인 +
    // 트래킹 표 커버리지 보강. 아래 5개 그룹이 검수 요청 ①~⑤에 각각 대응한다.
    // ══════════════════════════════════════════════════════════════════════════════════════

    // ── ① 장치(dev_pin MANIP) + 특수 스핀명령(집중/올인/기도/최후)을 실제로 쓰는 RunController 자동플레이 ──
    // Tests_S5_StatTrackerAutoPlay와 달리 "Spin(N) 반복"이 아니라 매 스핀마다 MANIP·미사용 특수모드를
    // 적극적으로 우선 시도한다 — 트래킹 표의 deviceUses/pinUses/focusUses/cmdCoin_* 등 실제 RunController
    // 이벤트로만 검증 가능한 행들을 합성 이벤트가 아닌 진짜 게임플레이로 실행시킨다.
    internal static class Tests_S5_DeviceAndSpecialModeAutoplay
    {
        public static void Run(TestCtx t)
        {
            long[] seeds = { 501L, 502L, 503L, 504L, 505L, 506L, 507L, 508L };
            long totalDeviceUses = 0, totalPinUses = 0, totalFocusUses = 0, totalCmdCoinTotal = 0;
            foreach (var seed in seeds)
            {
                var profile = RunOne(t, seed);
                if (profile == null) continue;
                totalDeviceUses += profile.GetStat("deviceUses");
                totalPinUses += profile.GetStat("pinUses");
                totalFocusUses += profile.GetStat("focusUses");
                totalCmdCoinTotal += profile.GetStat("cmdCoinTotal");
            }
            t.True(totalDeviceUses > 0, "[device-autoplay] 시드 합산 deviceUses > 0 (dev_pin MANIP이 실제로 실행됨)");
            t.True(totalPinUses > 0, "[device-autoplay] 시드 합산 pinUses > 0");
            t.True(totalFocusUses > 0, "[device-autoplay] 시드 합산 focusUses > 0 (집중 모드 실제 사용)");
            t.True(totalCmdCoinTotal > 0, "[device-autoplay] 시드 합산 cmdCoinTotal > 0 (특수명령 코인 지출 실제 발생)");
        }

        private static SpinMode ChooseMode(RunState run)
        {
            if (!run.UsedCmds.Contains("FOCUS")) return SpinMode.Focus;
            if (!run.UsedCmds.Contains("ALLIN")) return SpinMode.Allin;
            if (!run.UsedCmds.Contains("PRAY")) return SpinMode.Pray;
            if (!run.UsedCmds.Contains("LAST")) return SpinMode.Last;
            return SpinMode.N;
        }

        private static PlayerProfile RunOne(TestCtx tt, long seed)
        {
            var stat = S4TestHelpers.GenerousStat();
            var profile = new PlayerProfile();
            var scratch = new StatTracker.RunScratch();
            RunController rc;
            try
            {
                // parttime: startCoins=15 (Characters.cs) — 집중/올인/기도/최후 + dev_pin(3코인) 비용을
                // 초반부터 감당할 수 있어야 특수모드/MANIP가 실제로 실행된다(RunController는 해금 여부를
                // 검사하지 않으므로 charId 직접 지정 가능, RunController.cs 헤더 주석 "미해금/미지정 id는
                // BASE_CHAR 폴백" 참조 — parttime은 존재하는 id라 폴백되지 않는다).
                rc = new RunController("parttime", "basic", "dev_pin", seed, stat);
            }
            catch (Exception ex)
            {
                tt.Fail($"[device-autoplay seed={seed}]", "RunController 생성 실패: " + ex);
                return null;
            }
            StatTracker.Apply(profile, rc.State, rc.LaunchEvents, scratch);

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
                        {
                            bool manipEligible = rc.State.LastCells.Count > 0 && !rc.State.UsedCmds.Contains("dev_pin") && rc.State.Coins >= 3;
                            events = manipEligible ? rc.Do(new DeviceCmd("dev_pin", 1)) : rc.Do(new Spin(ChooseMode(rc.State)));
                            if (events.Count > 0 && events[0].type == "REJECTED") events = rc.Do(new Spin(SpinMode.N));
                            break;
                        }
                        case RunPhase.PostSpin:
                            events = rc.Do(new Continue());
                            break;
                        case RunPhase.NodeSelect:
                            events = rc.Do(new ChooseNode(0));
                            break;
                        case RunPhase.EventAugment:
                        case RunPhase.EventRelic:
                        // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12): AUGLEVEL 노드도 같은
                        // PickOffer(0) 진입점을 공유한다(NodeEvents.PickOffer가 phase로 분기).
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
                            throw new InvalidOperationException("device-autoplay: 처리 불가 Phase=" + phase);
                    }
                    StatTracker.Apply(profile, rc.State, events, scratch);
                }
            }
            catch (Exception ex)
            {
                tt.Fail($"[device-autoplay seed={seed}]", "자동플레이 중 예외: " + ex);
                return null;
            }
            tt.True(rc.State.Phase == RunPhase.GameOver, $"[device-autoplay seed={seed}] guard(50000) 내 게임오버 도달");
            return profile;
        }
    }

    // ── ③⑤ 클리어/게임오버 setMax·inc 키 전수 스모크 + M4/L1/L3 회귀 ──────────────────────────────
    // RunState/ClearOutcome/SpinOutcome/FailureOutcome을 직접 구성해(Tests_S4_DeviceManip와 동일한 방식 —
    // 필드가 전부 public이라 RunController 상태기계를 거치지 않고도 StatTracker.Apply를 정밀 가황할 수
    // 있다) ApplyClearTracking/ApplyGameOverTracking의 조건부 분기를 하나씩 확정적으로 가황한다.
    internal static class Tests_S5_ClearGameOverKeySmoke
    {
        public static void Run(TestCtx t)
        {
            BossFinalsGradWithNullResult_L3Regression(t);
            BossStrictLuckWithResult(t);
            ClearInc_RichNoItemNoShopCurseBoss(t);
            AddAch4_NoPrismNoRelicNoGoldBasicOnly(t);
            ClearSetMax_DeviceStagesAndNoCmdNoReroll(t);
            ZeroCoinAndDebtBoss(t);
            GameOver_Regressions(t);
            ItemsUsed_M2_InstantClearSkip(t);
            GraduationsCounter_Stage15Only(t);
        }

        // WEB_PARITY P3-2(업적 34종 "grad1") — 웹 game.js:1401 "if (stage === 15) r.graduatedThisRun =
        // true"를 StatTracker.ApplyClearTracking이 곧바로 "graduations" 카운터 증분으로 이식했는지
        // 확인한다. 14/16 등 인접 스테이지에서는 증분되지 않아야 하고(정확히 15), 보스 여부와 무관하게
        // stage==15면 증분된다(웹 원본도 boss 플래그를 따로 보지 않는다).
        private static void GraduationsCounter_Stage15Only(TestCtx t)
        {
            var profile14 = new PlayerProfile();
            var run14 = S4TestHelpers.NewRun(21L);
            var scratch14 = new StatTracker.RunScratch();
            StatTracker.Apply(profile14, run14, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = NewSpin(result: null), clear = NewClear(14, boss: false) } }, scratch14);
            t.Eq(0L, profile14.GetStat("graduations"), "[grad1] stage=14 클리어는 graduations 미증분");

            var profile15 = new PlayerProfile();
            var run15 = S4TestHelpers.NewRun(22L);
            var scratch15 = new StatTracker.RunScratch();
            StatTracker.Apply(profile15, run15, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = NewSpin(result: null), clear = NewClear(15, boss: true) } }, scratch15);
            t.Eq(1L, profile15.GetStat("graduations"), "[grad1] stage=15 클리어(웹 game.js:1401) → graduations +1");

            var profile16 = new PlayerProfile();
            var run16 = S4TestHelpers.NewRun(23L);
            var scratch16 = new StatTracker.RunScratch();
            StatTracker.Apply(profile16, run16, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = NewSpin(result: null), clear = NewClear(16, boss: false) } }, scratch16);
            t.Eq(0L, profile16.GetStat("graduations"), "[grad1] stage=16 클리어는 graduations 미증분(정확히 15만)");

            // AchievementEngine.Evaluate와 연동해 grad1 업적 자체가 실제로 달성되는지도 확인.
            var achieved = AchievementEngine.Evaluate(profile15);
            t.True(achieved.Any(a => a.id == "grad1"), "[grad1] graduations=1 → grad1 업적 신규 달성");
        }

        private static ClearOutcome NewClear(int stage, bool boss = false, long leftover = 0, bool lastSpinClear = false, long overPct = 100, bool inDebt = false) =>
            new ClearOutcome { clearedStage = stage, boss = boss, leftover = leftover, lastSpinClear = lastSpinClear, overPct = overPct, inDebt = inDebt };

        private static SpinOutcome NewSpin(SpinResult result = null, long newCoins = 100, bool destroyDevice = false, long newExp = 50, long quota = 100) =>
            new SpinOutcome { rejected = false, mode = SpinMode.N, result = result, newExp = newExp, newScore = 0, newCoins = newCoins, newSpinIndex = 5, quota = quota, spins = 5, destroyDevice = destroyDevice };

        private static SpinResult NewResult(int bestSetCount = 0, params string[] cellIds)
        {
            var cells = new List<Cell>();
            foreach (var id in cellIds) cells.Add(new Cell(Symbols.ById(id)));
            return new SpinResult { cells = cells, bestSetCount = bestSetCount, skulls = 0, jackpotSym = null };
        }

        // L3(Opus 1차 검수): null 가드가 switch 전체를 감싸던 예전 버그 — finals/grad는 res 없이도 판정
        // 가능한데 res==null(아이템 즉시클리어)이면 통째로 미집계됐었다. finals=S5, grad=S20으로 확인.
        private static void BossFinalsGradWithNullResult_L3Regression(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(1L);
            var scratch = new StatTracker.RunScratch();
            var clear = NewClear(5, boss: true, lastSpinClear: true); // S5 = finals(Bosses.For 순환)
            var spin = NewSpin(result: null); // 아이템 즉시클리어 등 res 없는 경로
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.Eq(1L, profile.GetStat("bossClear_finals"), "[L3] finals: res==null이어도 bossClear_finals 집계");
            t.Eq(1L, profile.GetStat("bossCounterClear_finals"), "[L3] finals: res==null이어도 막스핀 조건만으로 bossCounterClear_finals 집계");

            var profile2 = new PlayerProfile();
            var run2 = S4TestHelpers.NewRun(2L); // 장치 미장착 = grad 카운터조건(무장치) 충족
            var clear2 = NewClear(20, boss: true);
            var spin2 = NewSpin(result: null);
            StatTracker.Apply(profile2, run2, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin2, clear = clear2 } }, scratch);
            t.Eq(1L, profile2.GetStat("bossClear_grad"), "[L3] grad: res==null이어도 bossClear_grad 집계");
            t.Eq(1L, profile2.GetStat("bossCounterClear_grad"), "[L3] grad: res==null이어도 무장치 조건만으로 bossCounterClear_grad 집계");
        }

        private static void BossStrictLuckWithResult(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(3L);
            var scratch = new StatTracker.RunScratch();
            var clear = NewClear(10, boss: true); // S10 = strict
            var spin = NewSpin(result: NewResult(bestSetCount: 3, "cherry", "cherry", "cherry", "book", "gem"));
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.Eq(1L, profile.GetStat("bossCounterClear_strict"), "[boss-smoke] strict: bestSetCount>=3 → bossCounterClear_strict");

            var profile2 = new PlayerProfile();
            var run2 = S4TestHelpers.NewRun(4L);
            var clear2 = NewClear(15, boss: true); // S15 = luck
            var spin2 = NewSpin(result: NewResult(bestSetCount: 1, "star", "book", "gem", "coin", "skull"));
            StatTracker.Apply(profile2, run2, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin2, clear = clear2 } }, scratch);
            t.Eq(1L, profile2.GetStat("bossCounterClear_luck"), "[boss-smoke] luck: ⭐ 포함 → bossCounterClear_luck");
        }

        private static void ClearInc_RichNoItemNoShopCurseBoss(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(5L);
            run.UsedItemThisRun = false;
            run.Curses.AddRange(new[] { "hard_exam", "cursed_skulls", "speed_test" }); // curseN=3
            var scratch = new StatTracker.RunScratch();
            var clear = NewClear(10, boss: true); // stage>=8(noItemS8) && >=10(noShopS10) && boss(curseBossClears/richBossClears)
            var spin = NewSpin(result: null, newCoins: 60); // coinsAtClear>=50 → richBossClears
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.Eq(1L, profile.GetStat("richBossClears"), "[clear-smoke] richBossClears (boss && coinsAtClear>=50)");
            t.Eq(1L, profile.GetStat("noItemS8"), "[clear-smoke] noItemS8 (stage>=8 && !usedItemThisRun)");
            t.Eq(1L, profile.GetStat("noShopS10"), "[clear-smoke] noShopS10 (stage>=10 && RUNSHOP 없음)");
            t.Eq(1L, profile.GetStat("curseBossClears"), "[clear-smoke] curseBossClears (boss && curseN>=3)");
        }

        private static void AddAch4_NoPrismNoRelicNoGoldBasicOnly(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(6L, "novice", "basic"); // 증강/유물 0개(퍽 미보유) + novice+basic
            var scratch = new StatTracker.RunScratch();
            var clear = NewClear(7, boss: false);
            var spin = NewSpin(result: null);
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.Eq(7L, profile.GetStat("noPrismBestStage"), "[clear-smoke] noPrismBestStage (프리즘 0개)");
            t.Eq(7L, profile.GetStat("noRelicBestStage"), "[clear-smoke] noRelicBestStage (유물 0개)");
            t.Eq(7L, profile.GetStat("noGoldBestStage"), "[clear-smoke] noGoldBestStage (골드+프리즘 0개)");
            t.Eq(7L, profile.GetStat("basicOnlyBestStage"), "[clear-smoke] basicOnlyBestStage (novice+basic)");
        }

        private static void ClearSetMax_DeviceStagesAndNoCmdNoReroll(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(7L, "novice", "basic", "dev_pin");
            run.Device2 = "dev_swap";
            run.RunUsedCmd = false;
            run.RunRerolled = false;
            var scratch = new StatTracker.RunScratch();
            var clear = NewClear(9, boss: false);
            var spin = NewSpin(result: null);
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.Eq(9L, profile.GetStat("dvstage_dev_pin"), "[clear-smoke] dvstage_dev_pin (메인 장치 도달 스테이지)");
            t.Eq(9L, profile.GetStat("dvstage_dev_swap"), "[clear-smoke] dvstage_dev_swap (보조 장치 도달 스테이지)");
            t.Eq(9L, profile.GetStat("noCommandBestStage"), "[clear-smoke] noCommandBestStage (runUsedCmd==0)");
            t.Eq(9L, profile.GetStat("noRerollBestStage"), "[clear-smoke] noRerollBestStage (runRerolled==0)");
        }

        private static void ZeroCoinAndDebtBoss(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(8L);
            run.DebtStages = 2; // clear.inDebt 판정은 ClearOutcome이 이미 계산해 넘긴다는 계약이라 여기선 clear.inDebt를 직접 세팅
            var scratch = new StatTracker.RunScratch();
            var clear = NewClear(5, boss: true, inDebt: true);
            var spin = NewSpin(result: null, newCoins: 0); // coinsAtClear<=0 → zeroCoinClears
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.Eq(1L, profile.GetStat("zeroCoinClears"), "[clear-smoke] zeroCoinClears (coinsAtClear<=0, 보스 무관)");
            t.Eq(1L, profile.GetStat("debtBossClears"), "[clear-smoke] debtBossClears (boss && inDebt)");
        }

        // M4: devicesOwned가 "이번 게임오버 자체가 막 충족시킨" 조건을 즉시 반영하는지.
        // L1: BestChar/BestMachine이 동점(>=)에도 갱신되는지.
        // WEB_PARITY P3-2 — dev_safe의 unlockAch가 구 lic_safe(closeClears>=5 && bestStage>=6, AND
        // 2조건)에서 웹 cherry100(cherryTotal>=100, 단일조건)으로 바뀌었다. "이번 이벤트가 막 조건을
        // 채운다"는 시나리오를 유지하려고, GAME_OVER 이벤트 자체가 실어 온 마지막 스핀(🍒체리 1개)이
        // cherryTotal을 99→100으로 채우게 구성한다 — ApplySpinIncrements(같은 이벤트 처리 안에서 먼저
        // 실행)가 cherryTotal을 갱신한 *뒤에* ApplyGameOverTracking의 ComputeDevicesOwned가 읽으므로,
        // AchievedIds에 cherry100이 아직 없어도 devicesOwned에 dev_safe가 즉시 반영돼야 한다.
        private static void GameOver_Regressions(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["cherryTotal"] = 99; // cherry100 조건(cherryTotal>=100) 직전 상태
            var run = S4TestHelpers.NewRun(9L);
            var scratch = new StatTracker.RunScratch();
            t.True(!profile.AchievedIds.Contains("cherry100"), "[game-over] 사전 cherry100 미달성 상태 확인");
            var spin = NewSpin(result: NewResult(0, "cherry"));
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "GAME_OVER", spin = spin, failure = new FailureOutcome { kind = "GAME_OVER", finalScore = 777 } } }, scratch);
            t.Eq(100L, profile.GetStat("cherryTotal"), "[game-over] cherryTotal이 이번 GAME_OVER 이벤트의 스핀으로 100 도달");
            t.True(profile.GetStat("devicesOwned") >= 1,
                "[game-over] M4: AchievedIds에 cherry100이 없어도 방금 충족된 조건(cherryTotal)만으로 devicesOwned에 dev_safe 포함");

            var profile2 = new PlayerProfile();
            var run2 = S4TestHelpers.NewRun(10L, "gambler", "magnet");
            var scratch2 = new StatTracker.RunScratch();
            StatTracker.Apply(profile2, run2, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = new FailureOutcome { kind = "GAME_OVER", finalScore = 0 } } }, scratch2);
            t.Eq("gambler", profile2.BestChar, "[game-over] L1: finalScore==priorBest(0==0) 동점에도 BestChar 갱신(>= 비교)");
            t.Eq("magnet", profile2.BestMachine, "[game-over] L1: 동점에도 BestMachine 갱신");
        }

        // M2: grad_ring/gold_grad_bell 즉시클리어 동반 시 itemsUsed 미집계(원본 동작) — 그 외 아이템/그
        // 즉시클리어가 없는 경우는 정상 집계되는 대조군까지 확인.
        private static void ItemsUsed_M2_InstantClearSkip(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(11L);
            var scratch = new StatTracker.RunScratch();
            var clearEvent = new RunEvent { type = "STAGE_CLEARED", spin = NewSpin(result: null), clear = NewClear(1) };

            var profileSkip = new PlayerProfile();
            StatTracker.Apply(profileSkip, run, new List<RunEvent> { new RunEvent { type = "ITEM_USED", itemId = "grad_ring" }, clearEvent }, scratch);
            t.Eq(0L, profileSkip.GetStat("itemsUsed"), "[M2] grad_ring 즉시클리어 동반 시 itemsUsed 미증가(원본 동작)");

            var profileNoClear = new PlayerProfile();
            StatTracker.Apply(profileNoClear, run, new List<RunEvent> { new RunEvent { type = "ITEM_USED", itemId = "grad_ring" } }, scratch);
            t.Eq(1L, profileNoClear.GetStat("itemsUsed"), "[M2] 즉시클리어 없이 쓴 grad_ring은 itemsUsed 정상 집계");

            var profileOther = new PlayerProfile();
            StatTracker.Apply(profileOther, run, new List<RunEvent> { new RunEvent { type = "ITEM_USED", itemId = "cram" }, clearEvent }, scratch);
            t.Eq(1L, profileOther.GetStat("itemsUsed"), "[M2] grad_ring/gold_grad_bell 이외 아이템은 예외 대상 아님(STAGE_CLEARED 동반해도 정상 집계)");
        }
    }

    // ── ② 빌드도감(bld_* 25종) 조건별 직접 구성 — H1(bld_pinned_fate) 회귀 포함 ────────────────────
    internal static class Tests_S5_ThemeBuildDirectConstruction
    {
        public static void Run(TestCtx t)
        {
            BldPinnedFate_H1Regression(t);

            // ── 성장형 ──
            AssertBld(t, "runFastClears>=3", "bld_fast_start", () =>
            {
                var run = S4TestHelpers.NewRun(101L);
                run.RunFastClears = 3;
                return (run, NewClear(1), NewSpin());
            });
            AssertBld(t, "stage>=5 && perks>=3", "bld_model_growth", () =>
            {
                var run = S4TestHelpers.NewRun(102L);
                run.Perks.AddRange(new[] { "study", "preview", "review" });
                return (run, NewClear(5), NewSpin());
            });
            AssertBld(t, "🍒 퍽 2개 && stage>=7", "bld_cherry_sprout", () =>
            {
                var run = S4TestHelpers.NewRun(103L);
                run.Perks.AddRange(new[] { "cherry_up", "cherry_farm" });
                return (run, NewClear(7), NewSpin());
            });
            AssertBld(t, "📘 퍽 2개 && stage>=7", "bld_library_start", () =>
            {
                var run = S4TestHelpers.NewRun(104L);
                run.Perks.AddRange(new[] { "book_up", "library" });
                return (run, NewClear(7), NewSpin());
            });
            AssertBld(t, "프리즘 0개 && stage>=10", "bld_foundation", () =>
            {
                var run = S4TestHelpers.NewRun(105L);
                return (run, NewClear(10), NewSpin());
            });

            // ── 운명형 ──
            AssertBld(t, "runPrayWins>=2", "bld_fate_hand", () =>
            {
                var run = S4TestHelpers.NewRun(106L);
                run.RunPrayWins = 2;
                return (run, NewClear(1), NewSpin());
            });
            AssertBld(t, "machine==casino && stage>=10", "bld_dice_grad", () =>
            {
                var run = S4TestHelpers.NewRun(107L, "novice", "casino");
                return (run, NewClear(10), NewSpin());
            });
            AssertBld(t, "runCrowns>=10", "bld_crown_caller", () =>
            {
                var run = S4TestHelpers.NewRun(108L);
                run.RunSymCounts["crown"] = 10;
                return (run, NewClear(1), NewSpin());
            });
            AssertBld(t, "boss && 희귀심볼>=5", "bld_prob_hacker", () =>
            {
                var run = S4TestHelpers.NewRun(109L);
                var res = NewResult(0, "crown", "crown", "wild", "wild", "crown");
                return (run, NewClear(5, boss: true), NewSpin(result: res));
            });
            AssertBld(t, "예언 사용 && 이번런 잭팟", "bld_jackpot_seer", () =>
            {
                var run = S4TestHelpers.NewRun(110L);
                run.UsedCmds.Add("RUNORACLE");
                run.RunJackpots = 1;
                return (run, NewClear(1), NewSpin());
            });

            // ── 역전형 ──
            AssertBld(t, "runLastSpinClears>=3", "bld_cliff_pass", () =>
            {
                var run = S4TestHelpers.NewRun(111L);
                run.RunLastSpinClears = 3;
                return (run, NewClear(1), NewSpin());
            });
            AssertBldWithProfile(t, "통산 closeClears>=5", "bld_heartbreaker", () =>
            {
                var p = new PlayerProfile();
                p.Stats["closeClears"] = 5;
                var run = S4TestHelpers.NewRun(112L);
                return (p, run, NewClear(1), NewSpin());
            });
            AssertBld(t, "막스핀배율 퍽 3개 && boss", "bld_cram_grad", () =>
            {
                var run = S4TestHelpers.NewRun(113L);
                run.Perks.AddRange(new[] { "review", "evening", "coffee" });
                return (run, NewClear(5, boss: true), NewSpin());
            });
            AssertBld(t, "비상졸업벨 && boss", "bld_miracle_cert", () =>
            {
                var run = S4TestHelpers.NewRun(114L);
                return (run, NewClear(5, boss: true), NewSpin(destroyDevice: true));
            });
            AssertBld(t, "stage>=10 && 막스핀클리어", "bld_last_candle", () =>
            {
                var run = S4TestHelpers.NewRun(115L);
                return (run, NewClear(10, lastSpinClear: true), NewSpin());
            });

            // ── 조합형 ──
            AssertBld(t, "machine==magnet && runSet4>=1", "bld_magnet_grad", () =>
            {
                var run = S4TestHelpers.NewRun(116L, "novice", "magnet");
                run.RunSet4 = 1;
                return (run, NewClear(1), NewSpin());
            });
            AssertBld(t, "와일드 포함 잭팟", "bld_wild_puzzle", () =>
            {
                var run = S4TestHelpers.NewRun(117L);
                var res = NewResult(5, "wild", "wild", "wild", "wild", "wild");
                res.jackpotSym = "wild";
                return (run, NewClear(1), NewSpin(result: res));
            });
            // bld_pinned_fate는 위 BldPinnedFate_H1Regression에서 실제 MANIP으로 검증(여기선 생략).
            AssertBld(t, "dev_copy && runSet4>=1", "bld_copy_answer", () =>
            {
                var run = S4TestHelpers.NewRun(118L, "novice", "basic", "dev_copy");
                run.RunSet4 = 1;
                return (run, NewClear(1), NewSpin());
            });
            AssertBld(t, "runAdjPairs>=5", "bld_chain", () =>
            {
                var run = S4TestHelpers.NewRun(119L);
                run.RunAdjPairs = 5;
                return (run, NewClear(1), NewSpin());
            });

            // ── 위험형 ──
            AssertBldWithProfile(t, "통산 skullTotal>=100", "bld_skull_intro", () =>
            {
                var p = new PlayerProfile();
                p.Stats["skullTotal"] = 100;
                var run = S4TestHelpers.NewRun(120L);
                return (p, run, NewClear(1), NewSpin());
            });
            AssertBld(t, "저주3개+ && boss", "bld_black_grad", () =>
            {
                var run = S4TestHelpers.NewRun(121L);
                run.Curses.AddRange(new[] { "hard_exam", "cursed_skulls", "speed_test" });
                return (run, NewClear(5, boss: true), NewSpin());
            });
            AssertBld(t, "해골5개+ 스핀", "bld_ossuary", () =>
            {
                var run = S4TestHelpers.NewRun(122L);
                var res = NewResult(0, "skull", "skull", "skull", "skull", "skull");
                res.skulls = 5;
                return (run, NewClear(1), NewSpin(result: res));
            });
            AssertBld(t, "저주7개+ && stage>=10", "bld_curse_vessel", () =>
            {
                var run = S4TestHelpers.NewRun(123L);
                run.Curses.AddRange(new[] { "hard_exam", "cursed_skulls", "speed_test", "frugal_vow", "tunnel_vision", "late_bloomer", "gem_obsession" });
                return (run, NewClear(10), NewSpin());
            });
            AssertBld(t, "dev_overheat && 저주3개+ && stage>=10", "bld_ominous_overheat", () =>
            {
                var run = S4TestHelpers.NewRun(124L, "novice", "basic", "dev_overheat");
                run.Curses.AddRange(new[] { "hard_exam", "cursed_skulls", "speed_test" });
                return (run, NewClear(10), NewSpin());
            });
        }

        // H1(Opus 1차 검수): dev_pin을 "실제 DeviceActions.Handle"로 사용해 RunScratch.PinUsedThisStage가
        // true가 되는지, 그리고 그 상태로 이 스테이지를 클리어하면 bld_pinned_fate가 달성되는지 확인한다.
        // 이 회귀 전에는 RunScratch가 생성만 되고 아무도 PinUsedThisStage를 true로 만들지 않아
        // bld_pinned_fate가 영구 미달성이었다(→ bdx_master_combo/bdx_total25/bdx_all_master 3종도 도달 불가).
        private static void BldPinnedFate_H1Regression(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(100L, "novice", "basic", "dev_pin");
            run.Stage = 1;
            run.SpinIndex = 1;
            run.LastSpinNo = 0;
            run.LastCells.AddRange(new[] { "cherry", "cherry", "book", "gem", "crown" });
            // 웹 파리티 P4-3 — HandleManip이 LastCellsFinal에서 복원한다(DeviceActions.cs §신규 발견 주석).
            run.LastCellsFinal.AddRange(SpinResolver.CellsFromIds(run.LastCells));
            run.StageExp = 20; run.Score = 5; run.Coins = 30;
            run.LastGain = 20; run.LastScoreGain = 5; run.LastCoinGain = 1;
            var scratch = new StatTracker.RunScratch();

            var manipEvents = DeviceActions.Handle(run, "dev_pin", 1); // 실제 MANIP RunEvent(합성 아님)
            StatTracker.Apply(profile, run, manipEvents, scratch);
            t.True(scratch.PinUsedThisStage, "[H1] dev_pin MANIP 실행 후 RunScratch.PinUsedThisStage == true");

            var clear = NewClear(run.Stage);
            var spin = NewSpin();
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.True(profile.GetStat("bld_pinned_fate") > 0, "[H1] dev_pin 사용 후 클리어 시 bld_pinned_fate 달성(회귀 확인)");
        }

        private static ClearOutcome NewClear(int stage, bool boss = false, bool lastSpinClear = false) =>
            new ClearOutcome { clearedStage = stage, boss = boss, leftover = 0, lastSpinClear = lastSpinClear, overPct = 100 };

        private static SpinOutcome NewSpin(SpinResult result = null, bool destroyDevice = false) =>
            new SpinOutcome { rejected = false, mode = SpinMode.N, result = result, newExp = 50, newScore = 0, newCoins = 100, newSpinIndex = 5, quota = 100, spins = 5, destroyDevice = destroyDevice };

        private static SpinResult NewResult(int bestSetCount, params string[] cellIds)
        {
            var cells = new List<Cell>();
            foreach (var id in cellIds) cells.Add(new Cell(Symbols.ById(id)));
            return new SpinResult { cells = cells, bestSetCount = bestSetCount, skulls = 0, jackpotSym = null };
        }

        private static void AssertBld(TestCtx t, string label, string bldId, Func<(RunState run, ClearOutcome clear, SpinOutcome spin)> setup)
        {
            var (run, clear, spin) = setup();
            var profile = new PlayerProfile();
            var scratch = new StatTracker.RunScratch();
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.True(profile.GetStat(bldId) > 0, $"[bld-direct] {label} → {bldId} 달성");
        }

        private static void AssertBldWithProfile(TestCtx t, string label, string bldId, Func<(PlayerProfile profile, RunState run, ClearOutcome clear, SpinOutcome spin)> setup)
        {
            var (profile, run, clear, spin) = setup();
            var scratch = new StatTracker.RunScratch();
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "STAGE_CLEARED", spin = spin, clear = clear } }, scratch);
            t.True(profile.GetStat(bldId) > 0, $"[bld-direct] {label} → {bldId} 달성");
        }
    }

    // ── ④ ComposeStat 파생키 — distinctCharS10만 남았다(WEB_PARITY P3-2, AchievementEngine.cs 헤더
    // 각주) — bldCat_*/bldTotal/bldAllBasic/bldAllMaster/accountLevel은 소비처가 없어 제거됐다(아래
    // Tests_S5_ComposeStatRemovedDerivedKeys가 그 제거 자체를 회귀 확인한다).
    internal static class Tests_S5_ComposeStatDerivedKeys
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["cstage_novice"] = 10;
            profile.Stats["cstage_scholar"] = 15;
            profile.Stats["cstage_gambler"] = 9; // 미달(<10)
            var composed = AchievementEngine.ComposeStat(profile);
            t.Eq(2L, composed["distinctCharS10"], "[compose-derived] distinctCharS10 == 2 (novice·scholar만 >=10)");

            // 빈 프로필은 distinctCharS10 == 0.
            var empty = AchievementEngine.ComposeStat(new PlayerProfile());
            t.Eq(0L, empty["distinctCharS10"], "[compose-derived] 빈 프로필 distinctCharS10 == 0");
        }
    }

    // ── 제거된 구 파생키 회귀 확인 — WEB_PARITY P3-2로 lic_dev_*/bldCat_*/bldTotal/bldAllBasic/
    // bldAllMaster/accountLevel이 ComposeStat 반환값에서 전부 빠졌는지 직접 확인한다(각 키가 여전히
    // 남아있다면 "실제로 안 쓰이는 파생만 제거" 지시를 어긴 것 — 작업 지시 2번).
    internal static class Tests_S5_ComposeStatRemovedDerivedKeys
    {
        public static void Run(TestCtx t)
        {
            var p = new PlayerProfile();
            p.Stats["bld_fast_start"] = 1;
            p.Stats["closeClears"] = 5;
            p.Stats["bestStage"] = 6; // 구 lic_safe AND 조건(closeClears>=5 && bestStage>=6) 충족 상태로도 확인
            var composed = AchievementEngine.ComposeStat(p);

            t.True(!composed.ContainsKey("lic_dev_safe"), "[compose-removed] lic_dev_safe 파생키 제거 확인");
            t.True(!composed.ContainsKey("bldCat_성장형"), "[compose-removed] bldCat_성장형 파생키 제거 확인");
            t.True(!composed.ContainsKey("bldTotal"), "[compose-removed] bldTotal 파생키 제거 확인");
            t.True(!composed.ContainsKey("bldAllBasic"), "[compose-removed] bldAllBasic 파생키 제거 확인");
            t.True(!composed.ContainsKey("bldAllMaster"), "[compose-removed] bldAllMaster 파생키 제거 확인");
            t.True(!composed.ContainsKey("accountLevel"), "[compose-removed] accountLevel 파생키 제거 확인");

            // 반면 원재료로 넣어둔 bld_fast_start/closeClears/bestStage는 그대로 통과돼야 한다
            // (ComposeStat이 profile.Stats를 복사만 하고 값을 지우지는 않는다는 계약 확인).
            t.Eq(1L, composed["bld_fast_start"], "[compose-removed] 원재료 bld_fast_start는 그대로 통과");
            t.Eq(5L, composed["closeClears"], "[compose-removed] 원재료 closeClears는 그대로 통과");
            t.Eq(6L, composed["bestStage"], "[compose-removed] 원재료 bestStage는 그대로 통과");
        }
    }

    // ── M1 회귀 → 웹 파리티 P3-4로 갱신: prodigy는 더 이상 distinctCharS10(파생키) 게이트를 쓰지 않는다.
    // Characters.cs가 unlockReq(StatReq AND)를 전면 폐기하고 웹 OR 5축(unlockRuns/Score/Stage/Level/Ach)
    // 으로 교체되면서, prodigy도 data.js:161 그대로 unlockStage=9 OR unlockAch="stage10"이 됐다
    // (distinctCharS10 파생키 자체는 AchievementEngine.ComposeStat에 여전히 남아 있지만 — Perks.cs
    // "prodigy unlockReq가 여전히 참조" 각주는 이제 사문화됐다 — 소비처가 없는 죽은 계산일 뿐이다).
    // 이 테스트는 그 이관이 실제로 반영됐는지(OR 두 축 각각 단독으로 충분한지) 확인한다.
    internal static class Tests_S5_CharUnlockDerivedKeyGate
    {
        public static void Run(TestCtx t)
        {
            var prodigy = Array.Find(Characters.All, c => c.id == "prodigy");
            t.True(prodigy != null, "[M1] prodigy 캐릭터 존재 확인");
            if (prodigy == null) return;
            t.Eq(9L, prodigy.unlockStage, "[M1] prodigy.unlockStage == 9 (data.js:161)");
            t.Eq("stage10", prodigy.unlockAch, "[M1] prodigy.unlockAch == \"stage10\" (data.js:161)");

            // distinctCharS10(구 게이트)는 이제 prodigy 해금과 완전히 무관 — 아무리 채워도 미해금 유지.
            var profileOld = new PlayerProfile();
            string[] ids = { "novice", "scholar", "gambler", "farmer", "parttime", "jeweler", "honor" };
            foreach (var id in ids) profileOld.Stats["cstage_" + id] = 10;
            t.True(!profileOld.IsCharUnlocked(prodigy), "[M1] distinctCharS10 파생키는 더 이상 prodigy를 해금하지 않음(죽은 게이트)");

            // 새 OR 모델 — unlockStage 단독 충족.
            var profileStage = new PlayerProfile();
            profileStage.SetMax("bestStage", 9);
            t.True(profileStage.IsCharUnlocked(prodigy), "[M1] bestStage>=9 단독으로 prodigy 해금(OR)");

            // 새 OR 모델 — unlockAch 단독 충족.
            var profileAch = new PlayerProfile();
            profileAch.AchievedIds.Add("stage10");
            t.True(profileAch.IsCharUnlocked(prodigy), "[M1] 업적 stage10 단독으로 prodigy 해금(OR)");

            // 둘 다 미충족이면 잠김.
            var profileLocked = new PlayerProfile();
            t.True(!profileLocked.IsCharUnlocked(prodigy), "[M1] 두 축 모두 미충족이면 prodigy 잠김");
        }
    }
}
