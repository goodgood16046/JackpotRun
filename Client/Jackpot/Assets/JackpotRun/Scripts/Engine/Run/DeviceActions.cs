using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 능동 장치: dev_coin 투입, MANIP 4종(dev_reroll/pin/copy/swap), dev_oracle PEEK, 보조 슬롯(SECONDARY_MUL
    // 약화), 스테이지당 1회 마커(dev.id 규약 — SpinResolver.cs 파일 끝 훅 주석), gambler 무료 재굴림(GREROL).
    // Kotlin SlotV2Service의 handleDevice(L1684-1751)/handleManipulator(L1815-1893)/
    // handleGamblerReroll(L1760-1812)를 전사. 02_service.md §7-C·§9.
    public static class DeviceActions
    {
        private const int ManipCostLow = 3;   // dev_reroll/dev_pin
        private const int ManipCostHigh = 5;  // dev_copy/dev_swap
        private const int DevCoinCost = 5;
        private const int DevBellMaxDeficit = 25;

        // Device.needsArg 메타 복원(S4 백로그) — SlotV2Engine.kt DEVICES 선언의 needsArg=true 4종
        // (dev_pin/dev_copy/dev_swap/dev_holdfile, L1045-1058). DeviceDef 계약엔 필드가 없어(Devices.cs
        // 헤더 주석) 이 파일에서 로컬 상수화한다 — Devices.cs는 수정하지 않는다.
        private static readonly HashSet<string> NeedsArgIds = new HashSet<string> { "dev_pin", "dev_copy", "dev_swap", "dev_holdfile" };
        public static bool DeviceNeedsArg(string deviceId) => NeedsArgIds.Contains(deviceId);

        // ══════════════════════════════════════════════════════════════════
        // Handle — DeviceCmd(deviceId, arg) 단일 진입점. dev_holdfile/dev_retake는 NodeEvents.HoldAugment/
        // Retake 전용(증강/유물 노드 상태에서만 의미가 있음) — 여기로 오면 안내성 거부만 반환.
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> Handle(RunState run, string deviceId, int? arg)
        {
            var dev = Devices.ById(deviceId);
            if (dev == null) return RunEvents.Rejected("DEVICE_UNKNOWN");
            bool isMain = run.Device == dev.id;
            bool isSecondary = !isMain && run.Device2 == dev.id;
            if (!isMain && !isSecondary) return RunEvents.Rejected("DEVICE_NOT_EQUIPPED");

            // POST_SPIN(§3-C 3단계 만회기회) — MANIP은 스핀 소모 없이 직전 결과 확정(fromPost=true).
            // WEB_PARITY P1 ③ Opus 1차검수 수정A(2026-08-07): dev_bell(kind=INSTANT)은 MANIP이 아니라
            // StageFlow.HandleFailure의 bellReady 게이트(부족≤25)로 POST_SPIN에 도달하므로 별도 분기가
            // 필요하다 — 웹 emergencyBell()(game.js:1326-1331)과 동일하게 칸 선택 없이 즉시 강제클리어.
            if (run.Phase == RunPhase.PostSpin)
            {
                if (dev.id == "dev_bell") return HandlePostSpinBell(run);
                if (dev.kind != "MANIP") return RunEvents.Rejected("POST_SPIN_ONLY_MANIP_OR_GAMBLER");
                return HandleManip(run, dev, arg, fromPost: true);
            }
            if (run.Phase != RunPhase.Spin) return RunEvents.Rejected("PHASE_NOT_SPIN");

            if (run.UsedCmds.Contains(dev.id)) return RunEvents.Rejected("DEVICE_ALREADY_USED"); // 스테이지당 1회(§9-A)

            switch (dev.kind)
            {
                case "MANIP": return HandleManip(run, dev, arg, fromPost: false);
                case "PEEK": return HandlePeek(run, dev);
                default:
                    switch (dev.id)
                    {
                        case "dev_coin": return HandleDevCoin(run, isSecondary);
                        case "dev_bell": return HandleDevBell(run);
                        case "dev_holdfile": return RunEvents.Rejected("USE_HOLD_AUGMENT_ACTION");
                        case "dev_retake": return RunEvents.Rejected("USE_RETAKE_ACTION");
                        default: return RunEvents.Rejected("DEVICE_NOT_SUPPORTED"); // dev_syllabus/dev_major: 정보/패시브, 명령 없음
                    }
            }
        }

        // ── dev_coin(🪙투입, ARMED) — Kotlin L1723-1732 ──
        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:1304-1310 insertCoin()) — A9+ 쿨다운
        // (stage < DevCdUntil이면 거부, 성공 시 DevCdUntil = stage+2)만 이 메서드가 추가로 처리한다.
        private static List<RunEvent> HandleDevCoin(RunState run, bool isSecondary)
        {
            if (run.Asc >= 9 && run.Stage < run.DevCdUntil) return RunEvents.Rejected("DEVICE_COOLDOWN");
            if (run.Coins < DevCoinCost) return RunEvents.Rejected("INSUFFICIENT_COINS");
            run.Coins -= DevCoinCost;
            if (isSecondary)
            {
                double fullMul = Devices.ById("dev_coin").fx["expMul"]; // 1.3 — Devices.cs가 단일 소스
                double secMul = 1.0 + (fullMul - 1.0) * Formulas.SECONDARY_MUL;
                run.PendingNextExpMul *= secMul;
            }
            else
            {
                run.ArmItems.Add("dev_coin"); // ApplyItemMods의 "dev_coin" case가 다음 스핀에 expMul*=1.3 적용
            }
            run.UsedCmds.Add("dev_coin");
            if (run.Asc >= 9) run.DevCdUntil = run.Stage + 2; // 웹 game.js:1309
            return RunEvents.One(new RunEvent { type = "DEVICE_ARMED", deviceId = "dev_coin", secondary = isSecondary });
        }

        // ── dev_bell(🔔비상, INSTANT — 다음 스핀 강제클리어 예약) — Kotlin L1733-1740 ──
        private static List<RunEvent> HandleDevBell(RunState run)
        {
            // [원본 버그 유지] 이 임계값 확인용 mods는 device/phasePerks를 생략한다(Kotlin L1734-1735) —
            // 실제 스핀 시점 재계산(§2)과 다를 수 있으나 원문 그대로 이식(정보성 게이트일 뿐 최종 판정은
            // dev_bell 발동 시 SpinResolver가 실제 mods로 다시 계산함, S3 기이식).
            var mods = ModsBuilder.ApplyItemMods(ModsBuilder.Build(run.MachineId, run.CharId, run.Perks, run.Curses, "", levels: run.PerkLevels), run.PhaseItems);
            long quota = SpinResolver.QuotaOf(run.Stage, mods, run.Asc, run.BossPhase2, DeepRunHooks.DeepPenalty(run));
            long shortfall = quota - run.StageExp;
            if (shortfall > DevBellMaxDeficit) return RunEvents.Rejected("DEV_BELL_DEFICIT_TOO_HIGH");
            run.ArmItems.Add("dev_bell");
            run.UsedCmds.Add("dev_bell");
            return RunEvents.One(new RunEvent { type = "DEVICE_ARMED", deviceId = "dev_bell" });
        }

        // ── dev_bell POST_SPIN 즉시강제클리어 — 웹 emergencyBell()(game.js:1326-1331) 대응 ──────────
        // WEB_PARITY P1 ③ Opus 1차검수 수정A. SPIN 단계의 HandleDevBell(위)은 "다음 스핀에 반영"용
        // ArmItems 예약이고, 이건 완전히 별개 경로다 — 스핀을 거치지 않고 StageExp를 quota로 채워
        // 곧바로 클리어시킨다(ItemUse.cs의 grad_ring/gold_grad_bell 즉시클리어 패턴과 동일한 구성 —
        // result=null인 SpinOutcome을 만들어 StageFlow.ClearStage를 그대로 재사용). 웹처럼 usedCmds
        // 마커는 없다(성공하면 장치 자체가 파괴돼 재사용 자체가 불가능해지므로 불필요 — game.js 원문도
        // 마커를 쓰지 않는다). 조건 미충족(부족>25)이면 SPIN 단계와 동일한 거부 사유를 재사용한다.
        private static List<RunEvent> HandlePostSpinBell(RunState run)
        {
            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var mods = ModsBuilder.ApplyItemMods(
                ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels),
                run.PhaseItems);
            long quota = SpinResolver.QuotaOf(run.Stage, mods, run.Asc, run.BossPhase2, DeepRunHooks.DeepPenalty(run));
            long deficit = quota - run.StageExp;
            if (deficit > DevBellMaxDeficit) return RunEvents.Rejected("DEV_BELL_DEFICIT_TOO_HIGH");

            int spins = SpinResolver.EffSpins(run, mods);
            run.StageExp = quota; // 웹 r.stageExp = r.quota
            run.Device = "";      // 1회 파괴(웹 r.device = "")

            var outcome = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = null, gained = 0,
                newExp = run.StageExp, newScore = run.Score, newCoins = run.Coins,
                newSpinIndex = run.SpinIndex, quota = quota, spins = spins, destroyDevice = true,
            };
            var clear = StageFlow.ClearStage(run, outcome);
            return RunEvents.One(StageFlow.BuildClearEvent(outcome, clear, "dev_bell"));
        }

        // ── dev_oracle/dev_syllabus 등 PEEK(🔮예언, 다음 스핀 확정) — Kotlin L1707-1717 ──
        // 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:1313-1317 oracle()) — A9+ 쿨다운은
        // dev_oracle 전용(웹 `r.device !== "dev_oracle"` 게이트 그대로 — dev_syllabus는 웹에 대응 없는
        // Unity 전용 장치라 쿨다운 대상이 아니다).
        private static List<RunEvent> HandlePeek(RunState run, DeviceDef dev)
        {
            if (dev.id == "dev_oracle" && run.Asc >= 9 && run.Stage < run.DevCdUntil)
                return RunEvents.Rejected("DEVICE_COOLDOWN");
            // [원본 버그 유지 — 신규 발견] PEEK 미리보기는 device/phasePerks를 생략하고 REEL을 고정으로 쓴다
            // (dev_subreel의 6칸 확장을 무시) — SlotV2Service.kt L1709-1711 그대로.
            var mods = ModsBuilder.ApplyItemMods(ModsBuilder.Build(run.MachineId, run.CharId, run.Perks, run.Curses, "", levels: run.PerkLevels), run.PhaseItems);
            AscRunHooks.ApplyRunAscMods(mods, run);
            // 웹 파리티 P7-2(§1-A #19 B) — DeepRunHooks.ApplyDeepMods(아키타입 주입)는 여기서 호출하지
            // 않는다. HandlePeek는 Evaluate()를 부르지 않아(웹 oracle()의 미리보기 EXP 표시용 evaluate
            // 호출도 Unity는 이식하지 않음, 아래 raw는 LockedNext에만 쓰임) deepFamilyExpMul/ScoreMul/
            // CoinMul을 아무도 안 읽는다 — 넣어봐야 죽은 계산이라 5곳(ResolveSpin/HandleManip/
            // GamblerReroll/UseRetakeForm/timeline_ticket) 중 Evaluate를 실제로 호출하는 곳에만 둔다.
            // 웹 파리티 P7-2 blocker(§0, WEB_PARITY_DESIGN.md §2-(AA)) — 웹 oracle()(game.js:1320)은
            // `this._roll(...)`(심화면 주머니 추출)을 타고 `this._pityRoll(...)`로 그 굴림에서 직접
            // deepPity를 소진한다(MANIP/도박꾼재굴림/재시험과 달리 PEEK만 예외적으로 pity 대상 — §0
            // blocker 설명 그대로). 이전에는 항상 RollRaw(일반 가중추첨)라 심화 런에서도 주머니 밖
            // 72종 전체에서 예언 결과가 나올 수 있었다.
            var raw = SpinResolver.RollCells(run, mods, Formulas.REEL, run.SeedNext);
            if (run.DeepMode) raw = DeepRunHooks.ApplyDeepPity(run, raw);
            run.LockedNext.Clear();
            run.LockedNext.AddRange(raw.Select(c => c.sym.id));
            run.UsedCmds.Add(dev.id);
            if (dev.id == "dev_oracle")
            {
                run.UsedCmds.Add("RUNORACLE"); // 런 끝까지 보존(bld_jackpot_seer류 도전 판정용)
                if (run.Asc >= 9) run.DevCdUntil = run.Stage + 2; // 웹 game.js:1317
            }
            return RunEvents.One(new RunEvent { type = "DEVICE_PEEK", deviceId = dev.id, peekCells = raw.Select(c => c.sym.id).ToList() });
        }

        // ══════════════════════════════════════════════════════════════════
        // MANIP — 직전 스핀 결과 조작(재굴림/고정/복사/교체). 스핀 소모 없이 net-adjust(SpinResolver.cs
        // 파일 끝 훅 주석 계약). Kotlin handleManipulator(L1815-1893).
        // ══════════════════════════════════════════════════════════════════
        private static List<RunEvent> HandleManip(RunState run, DeviceDef dev, int? argN, bool fromPost)
        {
            if (run.LastCells.Count == 0 || run.LastSpinNo < 0) return RunEvents.Rejected("NO_LAST_SPIN");
            // 웹 파리티 P4-3(WEB_PARITY_DESIGN.md §2-(W) "신규 발견" — 2026-08-09 Opus 2차검수가 P4-2에서
            // 발견해 다음 슬라이스로 넘긴 항목): 웹 통합 manip()의 `let cells = r.lastCells.map(...)`
            // (game.js:1238)은 `r.lastCells`가 항상 "최종" 칸(res.cells, Evaluate 이후 — 폭탄 제거·자석
            // 복사·성장 전부 반영, 모든 스핀 경로가 `r.lastCells = res.cells`로 채움)이라 화면에 실제로
            // 보이는 칸에서 조작을 시작한다. Unity의 run.LastCells는 반대로 "재굴림 입력용 원시 스냅샷"
            // (Evaluate 이전 — 폭탄/자석 미반영)이라, 이전 슬라이스까지는 폭탄/자석 스핀 직후 MANIP을
            // 쓰면 "화면에 보이는 빈칸"이 아니라 "그 뒤에 있던 원본 심볼"을 조작하는 파리티 차이가 있었다
            // — run.LastCellsFinal(웹 r.lastCells 대응, 이미 List&lt;Cell&gt; 형태라 CellsFromIds 변환도
            // 불필요)에서 복원하도록 정정한다(dev_pin 고정/dev_copy 복사 대상이 이제 릴 표시와 일치).
            if (run.LastCellsFinal.Count == 0) return RunEvents.Rejected("LAST_CELLS_UNAVAILABLE");
            bool needsArg = dev.id == "dev_pin" || dev.id == "dev_copy" || dev.id == "dev_swap";
            if (needsArg && (argN == null || argN < 1)) return RunEvents.Rejected("ARG_REQUIRED");
            int cost = (dev.id == "dev_reroll" || dev.id == "dev_pin") ? ManipCostLow
                     : (dev.id == "dev_copy" || dev.id == "dev_swap") ? ManipCostHigh : 0;
            if (run.Coins < cost) return RunEvents.Rejected("INSUFFICIENT_COINS");

            // [원본 버그 유지 — 신규 발견] MANIP 재평가 mods는 device도 phasePerks도 반영하지 않는다
            // (SlotV2Service.kt L1834,L1836 — buildMods 호출에 deviceId 인자 없음 + perkList(run)만, +
            // phasePerkList(run) 없음). 도박꾼 무료재굴림(handleGamblerReroll)은 둘 다 포함해 대칭이 아니다.
            // 33종 세트 중 reqDevice가 MANIP 장치(dev_reroll/pin/copy/swap)를 가리키는 항목은 없어 device
            // 생략의 실질 영향은 없지만, phasePerks(broken_prism 임시 프리즘) 생략은 실제로 결과를 바꾼다.
            var preModsM = ModsBuilder.Build(run.MachineId, run.CharId, run.Perks, run.Curses, "", levels: run.PerkLevels);
            var mCtx = BuildRunCtx(run, run.LastSpinNo, ModsBuilder.SpinsPerStage(preModsM), SpinResolver.QuotaOf(run.Stage, preModsM, run.Asc, run.BossPhase2, DeepRunHooks.DeepPenalty(run)));
            var mods0 = ModsBuilder.Build(run.MachineId, run.CharId, run.Perks, run.Curses, "", mCtx, run.PerkLevels);
            var mods = ModsBuilder.ApplyItemMods(mods0, run.PhaseItems);
            AscRunHooks.ApplyRunAscMods(mods, run);
            DeepRunHooks.ApplyDeepMods(mods, run);
            int spins = SpinResolver.EffSpins(run, mods);
            long quota = SpinResolver.QuotaOf(run.Stage, mods, run.Asc, run.BossPhase2, DeepRunHooks.DeepPenalty(run));

            // 위 §신규 발견 주석 그대로 — LastCellsFinal(이미 List<Cell>)을 얕은 복사해 조작 대상으로
            // 삼는다. Cell은 불변(readonly 필드)이라 리스트만 복사하면 원본 run.LastCellsFinal을 건드리지
            // 않고 인덱스 교체(dev_pin/dev_copy/dev_swap)를 안전하게 할 수 있다.
            var raw = new List<Cell>(run.LastCellsFinal);
            int n = raw.Count;
            switch (dev.id)
            {
                // 웹 파리티 P7-2 blocker(§0) — 심화 런에서 재굴림/고정도 RollCellOne(주머니 추출)을 탄다
                // (pity/growNext는 웹 manip()도 태우지 않는다 — §0 blocker 설명 그대로, RollOne 그대로면
                // 심화 런에서도 72종 전체에서 다시 뽑히는 회귀가 있었다).
                case "dev_reroll":
                    for (int i = 0; i < n; i++) raw[i] = SpinResolver.RollCellOne(run, mods);
                    break;
                case "dev_pin":
                {
                    int keep = Clamp(argN.Value - 1, 0, n - 1);
                    for (int i = 0; i < n; i++) if (i != keep) raw[i] = SpinResolver.RollCellOne(run, mods);
                    break;
                }
                case "dev_copy":
                {
                    int src = Clamp(argN.Value - 1, 0, n - 1);
                    int dst = (src + 1 < n) ? src + 1 : src - 1;
                    if (dst >= 0 && dst < n) raw[dst] = new Cell(raw[src].sym, raw[src].tag);
                    break;
                }
                case "dev_swap":
                {
                    int idx = Clamp(argN.Value - 1, 0, n - 1);
                    string target = BestValueId(raw) ?? "star";
                    raw[idx] = new Cell(Symbols.ById(target));
                    break;
                }
                default:
                    return RunEvents.Rejected("DEVICE_NOT_SUPPORTED");
            }

            // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B): hasPrism/capMul(총배율 캡) 제거 — 웹 engine.js에는
            // 해당 캡이 없다. Evaluate/ApplyBoss 시그니처도 이에 맞춰 축소됨(SpinResolver.cs 주석 참조).
            var res = SpinResolver.Evaluate(run.Rng, raw, mods, run.LastSpinNo, spins, false);
            long gained = res.exp;
            var boss = Bosses.For(run.Stage);
            if (boss != null)
                gained = SpinResolver.ApplyBoss(boss, gained, res, run.LastSpinNo, spins).gained;
            gained = (long)(gained * 0.9); // MANIP 페널티 EXP -10%(공통 4종)

            long newExp = Math.Max(run.StageExp - run.LastGain + gained, 0);
            long newScore = Math.Max(run.Score - run.LastScoreGain + res.score, 0);
            long newCoins = Math.Max(run.Coins - run.LastCoinGain + res.coins - cost, 0);
            int manipSet4 = res.bestSetCount >= 4 ? 1 : 0;
            int manipAdj = (mods.adjacentSameExp != 0 && AdjPairCount(res.cells) > 0) ? 1 : 0;

            // 상태 커밋(클리어/게임오버 분기 이전에 먼저 반영 — ForceGameOver가 run.Score를 직접 읽음).
            run.StageExp = newExp;
            run.Score = newScore;
            run.Coins = newCoins;
            run.LastCells.Clear();
            run.LastCells.AddRange(raw.Select(c => c.sym.id));
            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16, 웹 game.js:1286 통합 manip() `r.lastMods =
            // mods`/`r.lastCells = res.cells`) — MANIP 재계산도 셀 정보 탭 캐시를 갱신한다. LastCells는
            // raw(재굴림 입력) 그대로 두고, LastCellsFinal만 res.cells(Evaluate 이후 최종 칸)로 채운다.
            run.LastMods = mods;
            run.LastCellsFinal.Clear(); run.LastCellsFinal.AddRange(res.cells);
            run.LastNotes.Clear(); if (res.notes != null) run.LastNotes.AddRange(res.notes);
            run.LastGain = gained;
            run.LastScoreGain = res.score;
            run.LastCoinGain = res.coins;
            run.RunSet4 = Math.Max(run.RunSet4 - run.LastSet4 + manipSet4, 0);
            run.RunAdjPairs = Math.Max(run.RunAdjPairs - run.LastAdjPairs + manipAdj, 0);
            run.LastSet4 = manipSet4;
            run.LastAdjPairs = manipAdj;
            run.UsedCmds.Add(dev.id); // 스테이지당 1회(§9-C)
            run.RunJackpots += res.jackpotSym != null ? 1 : 0;
            run.RunBestSpin = Math.Max(run.RunBestSpin, gained);
            run.RunRerolled = true;

            var outcome = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = res, gained = gained,
                newExp = newExp, newScore = newScore, newCoins = newCoins, newSpinIndex = run.SpinIndex,
                quota = quota, spins = spins,
            };

            if (newExp >= quota)
            {
                var clear = StageFlow.ClearStage(run, outcome);
                return RunEvents.One(StageFlow.BuildClearEvent(outcome, clear, dev.id));
            }
            if (fromPost || run.LastSpinNo + 1 >= spins)
            {
                var fail = StageFlow.ForceGameOver(run, quota - newExp);
                return RunEvents.One(new RunEvent { type = "GAME_OVER", spin = outcome, failure = fail, deviceId = dev.id });
            }
            run.Phase = RunPhase.Spin;
            return RunEvents.One(new RunEvent { type = "DEVICE_MANIP_RESULT", spin = outcome, deviceId = dev.id });
        }

        // ══════════════════════════════════════════════════════════════════
        // 도박꾼(gambler) 무료 재굴림 — 장치 무관, 점수 패널티 없음, 스테이지당 1회("GREROL" 마커).
        // Kotlin handleGamblerReroll(L1760-1812). MANIP과 달리 device+phasePerks를 전부 반영한다(대칭 아님,
        // 위 HandleManip 주석 참조).
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> GamblerReroll(RunState run, bool fromPost)
        {
            if (run.Phase != RunPhase.Spin && run.Phase != RunPhase.PostSpin) return RunEvents.Rejected("PHASE_INVALID");
            if (run.CharId != "gambler") return RunEvents.Rejected("NOT_GAMBLER");
            if (run.UsedCmds.Contains("GREROL")) return RunEvents.Rejected("ALREADY_USED");
            if (run.LastCells.Count == 0 || run.LastSpinNo < 0) return RunEvents.Rejected("NO_LAST_SPIN");
            if (run.LastCellsFinal.Count == 0) return RunEvents.Rejected("LAST_CELLS_UNAVAILABLE");

            var combinedPerks = new List<string>(run.Perks);
            combinedPerks.AddRange(run.PhasePerks);
            var preMods0 = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, levels: run.PerkLevels);
            var rrCtx = BuildRunCtx(run, run.LastSpinNo, ModsBuilder.SpinsPerStage(preMods0), SpinResolver.QuotaOf(run.Stage, preMods0, run.Asc, run.BossPhase2, DeepRunHooks.DeepPenalty(run)));
            var mods0 = ModsBuilder.Build(run.MachineId, run.CharId, combinedPerks, run.Curses, run.Device, rrCtx, run.PerkLevels);
            var mods = ModsBuilder.ApplyItemMods(mods0, run.PhaseItems);
            var devEq = Devices.ById(run.Device);
            if (devEq != null && devEq.kind == "PASSIVE") mods = ModsBuilder.ApplyPassiveDevice(mods, devEq.id);
            AscRunHooks.ApplyRunAscMods(mods, run);
            DeepRunHooks.ApplyDeepMods(mods, run);
            int spins = SpinResolver.EffSpins(run, mods);
            long quota = SpinResolver.QuotaOf(run.Stage, mods, run.Asc, run.BossPhase2, DeepRunHooks.DeepPenalty(run));

            // 웹 파리티 P4-3 — 통합 manip()이 gambler "재굴림" 분기도 함께 타므로(game.js:1240-1245)
            // 여기도 HandleManip과 동일하게 LastCellsFinal에서 복원한다(전체 재굴림이라 셀 값 자체는
            // 무관하지만 원본 소스를 일치시켜 둔다 — 위 HandleManip §신규 발견 주석 참조).
            // 웹 파리티 P7-2 blocker(§0) — RollCellOne(주머니 추출)로 전환(HandleManip과 동일 근거).
            var raw = new List<Cell>(run.LastCellsFinal);
            for (int i = 0; i < raw.Count; i++) raw[i] = SpinResolver.RollCellOne(run, mods);

            // 웹 파리티 P2(WEB_PARITY_DESIGN §2-B): hasPrism/capMul(총배율 캡) 제거 — 웹 engine.js에는
            // 해당 캡이 없다. Evaluate/ApplyBoss 시그니처도 이에 맞춰 축소됨(SpinResolver.cs 주석 참조).
            var res = SpinResolver.Evaluate(run.Rng, raw, mods, run.LastSpinNo, spins, false);
            long gained = res.exp;
            var boss = Bosses.For(run.Stage);
            if (boss != null)
                gained = SpinResolver.ApplyBoss(boss, gained, res, run.LastSpinNo, spins).gained;

            long newExp = Math.Max(run.StageExp - run.LastGain + gained, 0);
            long newScore = Math.Max(run.Score - run.LastScoreGain + res.score, 0);
            long newCoins = Math.Max(run.Coins - run.LastCoinGain + res.coins, 0);
            int rrSet4 = res.bestSetCount >= 4 ? 1 : 0;
            int rrAdj = (mods.adjacentSameExp != 0 && AdjPairCount(res.cells) > 0) ? 1 : 0;

            run.StageExp = newExp;
            run.Score = newScore;
            run.Coins = newCoins;
            run.LastCells.Clear();
            run.LastCells.AddRange(raw.Select(c => c.sym.id));
            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #16) — 도박꾼 무료 재굴림도 셀 정보 탭 캐시를 갱신
            // (웹 game.js:1240-1242,1286 — "재굴림" cmd는 MANIP 장치와 같은 통합 manip() 함수를 타므로
            // 도박꾼/장치 구분 없이 동일하게 r.lastMods를 갱신한다 — Unity가 두 함수로 나눈 구조라도
            // 파리티는 "둘 다 갱신"이 맞다. 대조적으로 재시험(ItemUse.UseRetakeForm)은 웹 `_freeReroll()`
            // (game.js:1214-1224)이 별도 함수라 r.lastMods를 전혀 건드리지 않는다 — 그쪽은 의도적으로
            // LastMods를 갱신하지 않는다, ItemUse.cs 주석 참조).
            run.LastMods = mods;
            run.LastCellsFinal.Clear(); run.LastCellsFinal.AddRange(res.cells);
            run.LastNotes.Clear(); if (res.notes != null) run.LastNotes.AddRange(res.notes);
            run.LastGain = gained;
            run.LastScoreGain = res.score;
            run.LastCoinGain = res.coins;
            run.RunSet4 = Math.Max(run.RunSet4 - run.LastSet4 + rrSet4, 0);
            run.RunAdjPairs = Math.Max(run.RunAdjPairs - run.LastAdjPairs + rrAdj, 0);
            run.LastSet4 = rrSet4;
            run.LastAdjPairs = rrAdj;
            run.UsedCmds.Add("GREROL");
            run.RunJackpots += res.jackpotSym != null ? 1 : 0;
            run.RunBestSpin = Math.Max(run.RunBestSpin, gained);
            run.RunRerolled = true;

            var outcome = new SpinOutcome
            {
                rejected = false, mode = SpinMode.N, result = res, gained = gained,
                newExp = newExp, newScore = newScore, newCoins = newCoins, newSpinIndex = run.SpinIndex,
                quota = quota, spins = spins,
            };

            if (newExp >= quota)
            {
                var clear = StageFlow.ClearStage(run, outcome);
                return RunEvents.One(StageFlow.BuildClearEvent(outcome, clear, "GREROL"));
            }
            if (fromPost || run.LastSpinNo + 1 >= spins)
            {
                var fail = StageFlow.ForceGameOver(run, quota - newExp);
                return RunEvents.One(new RunEvent { type = "GAME_OVER", spin = outcome, failure = fail, deviceId = "GREROL" });
            }
            run.Phase = RunPhase.Spin;
            return RunEvents.One(new RunEvent { type = "DEVICE_MANIP_RESULT", spin = outcome, deviceId = "GREROL" });
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────────────────
        // runCtxOf(Kotlin L71-78)와 동일 — SpinResolver.cs의 private RunCtxOf를 재사용할 수 없어(수정 금지
        // 파일) 그대로 복제한다.
        private static RunCtx BuildRunCtx(RunState run, int spinIndex, int spinsPerStage, long quota) => new RunCtx
        {
            stage = run.Stage, spinIndex = spinIndex, spinsPerStage = spinsPerStage,
            stageExp = run.StageExp, quota = quota,
            growthStack = run.GrowthStack, snowStack = run.SnowStack,
            curseCount = run.Curses.Count, unluckyGauge = run.UnluckyGauge,
            boss = Bosses.For(run.Stage) != null,
            coins = run.Coins, // 웹 파리티 P3-4 — SpinResolver.RunCtxOf와 동일(bankrupt 캐릭터).
        };

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        private static readonly HashSet<string> ValueSymIds = new HashSet<string>(
            Symbols.ValueIds.Select(s => Symbols.BySym(s).id));

        private static int AdjPairCount(IReadOnlyList<Cell> cells)
        {
            int p = 0;
            for (int i = 0; i < cells.Count - 1; i++)
            {
                var a = cells[i].sym; var b = cells[i + 1].sym;
                if (a.id == b.id && ValueSymIds.Contains(a.id)) p++;
            }
            return p;
        }

        // bestValueId(Kotlin L1753-1756) — HashMap tie-break이 비결정적이라 SpinResolver.cs §11-7 관례대로
        // Symbols.ValueIds 선언순서(cherry,star,book,gem,crown)로 결정론화한다.
        // [스펙-Kotlin 불일치 — 의도된 결정론화, 보고 대상]
        private static readonly string[] BestValuePriority = Symbols.ValueIds.Select(s => Symbols.BySym(s).id).ToArray();

        private static string BestValueId(IReadOnlyList<Cell> cells)
        {
            var counts = new Dictionary<string, int>();
            foreach (var c in cells)
                if (ValueSymIds.Contains(c.sym.id)) counts[c.sym.id] = counts.TryGetValue(c.sym.id, out var n) ? n + 1 : 1;
            string best = null;
            int bestCount = 0;
            foreach (var id in BestValuePriority)
                if (counts.TryGetValue(id, out var c) && c > bestCount) { bestCount = c; best = id; }
            return best;
        }
    }
}
