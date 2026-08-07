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
                            events = rc.Do(new PickOffer(0));
                            break;
                        case RunPhase.EventShop:
                            if (shopStep == 0) { events = rc.Do(new BuyOffer(0)); shopStep = 1; }
                            else { events = rc.Do(new LeaveShop()); shopStep = 0; }
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
    internal static class Tests_S5_AchievementTrigger
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();

            // intro_firstSpin: key=totalSpins, threshold=1.
            profile.Stats["totalSpins"] = 0;
            var newlyBefore = AchievementEngine.Evaluate(profile);
            t.True(!newlyBefore.Any(a => a.id == "intro_firstSpin"), "[ach-trigger] totalSpins=0 — intro_firstSpin 미달성");
            t.True(!profile.AchievedIds.Contains("intro_firstSpin"), "[ach-trigger] totalSpins=0 — AchievedIds에 없음");

            profile.Stats["totalSpins"] = 1;
            var newlyAt = AchievementEngine.Evaluate(profile);
            t.True(newlyAt.Any(a => a.id == "intro_firstSpin"), "[ach-trigger] totalSpins=1 — intro_firstSpin 신규 달성 목록에 포함");
            t.True(profile.AchievedIds.Contains("intro_firstSpin"), "[ach-trigger] totalSpins=1 — AchievedIds에 추가됨");

            // 재평가 시 이미 달성한 업적은 newly에 다시 나오지 않아야 함(bumpAch의 "id !in before" 필터).
            var newlyAgain = AchievementEngine.Evaluate(profile);
            t.True(!newlyAgain.Any(a => a.id == "intro_firstSpin"), "[ach-trigger] 재평가 시 중복 신규달성 없음");

            // 문턱값이 다른 두 번째 업적(spin100, threshold=100)으로 동일 패턴 재확인.
            profile.Stats["totalSpins"] = 99;
            var newly99 = AchievementEngine.Evaluate(profile);
            t.True(!newly99.Any(a => a.id == "spin100"), "[ach-trigger] totalSpins=99 — spin100 미달성");
            profile.Stats["totalSpins"] = 100;
            var newly100 = AchievementEngine.Evaluate(profile);
            t.True(newly100.Any(a => a.id == "spin100"), "[ach-trigger] totalSpins=100 — spin100 신규 달성");
        }
    }

    // ── ③ 면허(lic_*) 달성 → 장치 영구해금 반영 ─────────────────────────────────────────────────
    internal static class Tests_S5_LicenseUnlocksDevice
    {
        public static void Run(TestCtx t)
        {
            var devSafe = Devices.ById("dev_safe");
            t.True(devSafe != null && devSafe.unlockAch == "lic_safe",
                "[lic-unlock] dev_safe.unlockAch == \"lic_safe\" (Devices.cs 전제 확인)");

            var profile = new PlayerProfile();
            profile.Stats["closeClears"] = 4; // lic_safe 조건: closeClears>=5 && bestStage>=6
            profile.Stats["bestStage"] = 6;
            AchievementEngine.Evaluate(profile);
            t.True(!profile.AchievedIds.Contains("lic_safe"), "[lic-unlock] closeClears=4<5 — lic_safe 미달성");
            t.True(!profile.OwnedDevices.Contains("dev_safe"), "[lic-unlock] 미달성 상태에서 dev_safe 미보유");
            t.True(!profile.IsDeviceUnlocked(devSafe), "[lic-unlock] IsDeviceUnlocked == false");

            profile.Stats["closeClears"] = 5;
            var newly = AchievementEngine.Evaluate(profile);
            t.True(profile.AchievedIds.Contains("lic_safe"), "[lic-unlock] closeClears=5>=5 && bestStage=6>=6 — lic_safe 달성");
            t.True(newly.Any(a => a.id == "lic_safe"), "[lic-unlock] lic_safe가 신규달성 목록에 포함");
            t.True(profile.OwnedDevices.Contains("dev_safe"), "[lic-unlock] AchievementEngine.Evaluate가 dev_safe를 OwnedDevices에 반영");
            t.True(profile.IsDeviceUnlocked(devSafe), "[lic-unlock] IsDeviceUnlocked == true (면허 달성 반영 후)");

            // ComposeStat의 파생키 lic_dev_safe도 1로 계산되는지 직접 확인(AchievementEngine.Evaluate가
            // 참조하는 것과 동일한 함수를 호출 — 내부 판정 자체를 다시 검증).
            var composed = AchievementEngine.ComposeStat(profile);
            t.Eq(1L, composed["lic_dev_safe"], "[lic-unlock] ComposeStat 파생키 lic_dev_safe == 1");
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

            // 빈 프로필(최초 실행 상태)도 왕복이 안전한지 확인.
            var emptyDto = ProfileDto.ToDto(new PlayerProfile());
            var emptyRestored = ProfileDto.FromDto(emptyDto);
            t.Eq(0, emptyRestored.Stats.Count, "[dto-roundtrip] 빈 프로필 Stats.Count == 0");
            t.Eq(0, emptyRestored.AchievedIds.Count, "[dto-roundtrip] 빈 프로필 AchievedIds.Count == 0");

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
    internal static class Tests_S5_AccountLevelIntegration
    {
        public static void Run(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["totalSpins"] = 100; // 그 외 키는 전부 비움(파생키만 ComposeStat이 채움)

            var composed = AchievementEngine.ComposeStat(profile);

            // 손 계산 (Formulas.AccountExp, Core/Formulas.cs L150-207 그대로):
            //  ① 마일스톤(bestStage 없음=0): 0
            //  ② bossClears*8 상한120 (bossClears 없음=0): 0
            //  ③ runs*3 상한90 (runs 없음=0): 0
            //  ④ 업적 tier합 — totalSpins 키를 쓰는 482종 중 threshold<=100인 것만 기여:
            //     intro_firstSpin(threshold=1,브론즈=20) + spin100(threshold=100,브론즈=20) = 40
            //     (spin500=500/spin1000=1000/spin5000=5000/rp_spin2000=2000/rp_spin10000=10000은
            //      전부 threshold>100이라 미충족 — Achievements.cs 전수 grep으로 확인된 totalSpins 사용처 6종 중 나머지 4종)
            //  ⑤ bld_/bc_ 접두 키 없음: 0   ⑥ cstage_/mstage_ 접두 키 없음: 0
            //  합계 accountExp = 0+0+0+40+0+0 = 40
            //  level = 1 + floor(sqrt(40/22.0)) = 1 + floor(sqrt(1.8181...)) = 1 + floor(1.3484...) = 1 + 1 = 2
            var achievementExpTable = Achievements.All.Select(a => (a.req[0].key, a.req[0].value, a.tier)).ToList();
            long exp = Formulas.AccountExp(composed, achievementExpTable);
            t.Eq(40L, exp, "[account-level] totalSpins=100 → accountExp = 40 (손계산: intro_firstSpin 20 + spin100 20)");
            t.Eq(2, Formulas.ExpToLevel(exp), "[account-level] exp=40 → level = 1+floor(sqrt(40/22)) = 2 (손계산)");
            t.Eq(2L, composed["accountLevel"], "[account-level] ComposeStat 파생키 accountLevel == 2 (독립 재계산과 일치)");

            // 빈 프로필(전부 0)은 레벨 1(하한)이어야 한다.
            var empty = AchievementEngine.ComposeStat(new PlayerProfile());
            t.Eq(1L, empty["accountLevel"], "[account-level] 빈 프로필 accountLevel == 1 (하한)");
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
        // Shop.PerkUnlocked의 다른 통로(BasePerkIds/SchoolResearchDone/AccountLevel/Unlocks.Meets)로도
        // 우연히 해금되지 않을 만한 고티어 퍽을 고른다 — 빈 stat이면 학교 게이트 minLevel을 넘지 못해
        // 확실히 잠겨 있다.
        private static Perk PickGatedPerk(IReadOnlyList<Perk> pool, IReadOnlyDictionary<string, long> emptyStat)
        {
            foreach (var p in pool)
                if (!Schools.BasePerkIds.Contains(p.id) && !Shop.PerkUnlocked(p, emptyStat))
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
            t.True(Shop.PerkUnlocked(perk, profile.Stats), $"[seen-gate] PERK_GRANTED 후 {perk.id} 그랜드파더 해금됨");
        }

        private static void NodeEventGrantMarksSeen(TestCtx t)
        {
            var profile = new PlayerProfile();
            var run = S4TestHelpers.NewRun(2L);
            var scratch = new StatTracker.RunScratch();
            var relic = PickGatedPerk(Perks.Relics, profile.Stats);
            var aug = PickGatedPerk(Perks.Augments, profile.Stats);

            // case 7(유물 발견)
            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Event, relicGrantedId = relic.id } }, scratch);
            t.True(Shop.PerkUnlocked(relic, profile.Stats), $"[seen-gate] EVENT case7 유물발견 후 {relic.id} 해금됨");

            // case 8(증강 발견, 25% 특별이벤트는 relicGrantedId도 함께 채워지는 케이스까지 커버하도록 augmentGrantedId만 단독 확인)
            StatTracker.Apply(profile, run,
                new List<RunEvent> { new RunEvent { type = "NODE_RESOLVED", node = NodeKind.Event, augmentGrantedId = aug.id } }, scratch);
            t.True(Shop.PerkUnlocked(aug, profile.Stats), $"[seen-gate] EVENT case8 증강발견 후 {aug.id} 해금됨");
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
            t.True(Shop.PerkUnlocked(aug, profile.Stats), $"[seen-gate] 상점 증강구매 후 {aug.id} 해금됨");

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
                            events = rc.Do(new PickOffer(0));
                            break;
                        case RunPhase.EventShop:
                            if (shopStep == 0) { events = rc.Do(new BuyOffer(0)); shopStep = 1; }
                            else { events = rc.Do(new LeaveShop()); shopStep = 0; }
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
        private static void GameOver_Regressions(TestCtx t)
        {
            var profile = new PlayerProfile();
            profile.Stats["closeClears"] = 5; // dev_safe 조건(closeClears>=5 && bestStage>=6)의 절반은 사전 충족
            var run = S4TestHelpers.NewRun(9L);
            run.Stage = 6; // 이번 게임오버로 bestStage>=6이 "지금 막" 충족(사전엔 없었음)
            var scratch = new StatTracker.RunScratch();
            t.True(!profile.AchievedIds.Contains("lic_safe"), "[game-over] 사전 lic_safe 미달성 상태 확인");
            StatTracker.Apply(profile, run, new List<RunEvent> { new RunEvent { type = "GAME_OVER", failure = new FailureOutcome { kind = "GAME_OVER", finalScore = 777 } } }, scratch);
            t.Eq(6L, profile.GetStat("bestStage"), "[game-over] bestStage가 이번 런의 run.Stage로 setMax됨");
            t.True(profile.GetStat("devicesOwned") >= 1,
                "[game-over] M4: AchievedIds에 lic_safe가 없어도 방금 충족된 조건(closeClears+bestStage)만으로 devicesOwned에 dev_safe 포함");

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

    // ── ④ ComposeStat 파생키 5종 (distinctCharS10/bldCat_*/bldTotal/bldAllBasic/bldAllMaster) ──────
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

            var p2 = new PlayerProfile();
            p2.Stats["bld_fast_start"] = 1;
            p2.Stats["bld_model_growth"] = 1; // 성장형 2/5
            p2.Stats["bld_fate_hand"] = 1;    // 운명형 1/5
            var c2 = AchievementEngine.ComposeStat(p2);
            t.Eq(2L, c2["bldCat_성장형"], "[compose-derived] bldCat_성장형 == 2");
            t.Eq(1L, c2["bldCat_운명형"], "[compose-derived] bldCat_운명형 == 1");
            t.Eq(0L, c2["bldCat_역전형"], "[compose-derived] bldCat_역전형 == 0(미보유)");
            t.Eq(3L, c2["bldTotal"], "[compose-derived] bldTotal == 3(전체 합)");
            t.Eq(2L, c2["bldAllBasic"], "[compose-derived] bldAllBasic == 2(1개+ 보유 카테고리 수 — 성장형/운명형)");
            t.Eq(0L, c2["bldAllMaster"], "[compose-derived] bldAllMaster == 0(5/5 완성 카테고리 없음)");

            var p3 = new PlayerProfile();
            foreach (var id in StatTracker.ThemeBuildCategoryIds["성장형"]) p3.Stats[id] = 1;
            var c3 = AchievementEngine.ComposeStat(p3);
            t.Eq(5L, c3["bldCat_성장형"], "[compose-derived] bldCat_성장형 == 5(전부 완성)");
            t.Eq(1L, c3["bldAllMaster"], "[compose-derived] bldAllMaster == 1(성장형 5/5 마스터)");
            t.Eq(1L, c3["bldAllBasic"], "[compose-derived] bldAllBasic == 1");

            // 빈 프로필은 5개 파생키 전부 0.
            var empty = AchievementEngine.ComposeStat(new PlayerProfile());
            t.Eq(0L, empty["distinctCharS10"], "[compose-derived] 빈 프로필 distinctCharS10 == 0");
            t.Eq(0L, empty["bldTotal"], "[compose-derived] 빈 프로필 bldTotal == 0");
            t.Eq(0L, empty["bldAllBasic"], "[compose-derived] 빈 프로필 bldAllBasic == 0");
            t.Eq(0L, empty["bldAllMaster"], "[compose-derived] 빈 프로필 bldAllMaster == 0");
            foreach (var cat in StatTracker.ThemeBuildCategoryIds.Keys)
                t.Eq(0L, empty["bldCat_" + cat], $"[compose-derived] 빈 프로필 bldCat_{cat} == 0");
        }
    }

    // ── M1 회귀: distinctCharS10 게이트를 쓰는 캐릭터 해금이 ComposeStat 경유로 정상 동작하는지 ──────
    internal static class Tests_S5_CharUnlockDerivedKeyGate
    {
        public static void Run(TestCtx t)
        {
            var prodigy = Array.Find(Characters.All, c => c.id == "prodigy");
            t.True(prodigy != null, "[M1] prodigy 캐릭터 존재 확인");
            if (prodigy == null) return;

            var profile = new PlayerProfile();
            // distinctCharS10<7 — 원재료 Stats만으로는 이 키 자체가 없어 항상 미해금이어야 정상.
            profile.Stats["cstage_novice"] = 10;
            t.True(!profile.IsCharUnlocked(prodigy), "[M1] distinctCharS10=1(<7) — prodigy 미해금");

            // distinctCharS10>=7이 되도록 서로 다른 캐릭 7명의 cstage_*를 10 이상으로.
            string[] ids = { "novice", "scholar", "gambler", "farmer", "parttime", "jeweler", "honor" };
            foreach (var id in ids) profile.Stats["cstage_" + id] = 10;
            t.True(profile.IsCharUnlocked(prodigy), "[M1] distinctCharS10=7 — ComposeStat 경유로 prodigy 해금(회귀 확인)");
        }
    }
}
