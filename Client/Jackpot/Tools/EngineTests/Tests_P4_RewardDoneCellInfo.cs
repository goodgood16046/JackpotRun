using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15/#16) — REWARD_DONE 화면(보상 메시지·다음 스테이지
    // 프리뷰·현재 능력치) + 셀 정보 탭(EXP/점수 분해 + 영향 퍽) 검증. Tests_S4.cs/Tests_P3_AugLevel.cs의
    // 기존 노드 테스트는 이 슬라이스에서 phase 기대값을 RewardDone으로 갱신했다(전수 대응) — 이 파일은
    // RewardDoneView/CellInfoView 산출값 자체의 손계산 검증 + ProceedToStage 액션 전용.

    // ── A. ProceedToStage(웹 proceedToStage) — RewardDone → Spin 게이트 ──────────────────────────
    internal static class Tests_P4_ProceedToStage
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var rc = new RunController("novice", "basic", "", 42L, stat);
            rc.State.Phase = RunPhase.RewardDone;
            rc.State.RewardMessage = "테스트 메시지";

            var events = rc.Do(new ProceedToStage());
            t.Eq(1, events.Count, "[proceed] 단일 이벤트");
            t.Eq("STAGE_STARTED", events[0].type, "[proceed] STAGE_STARTED");
            t.Eq(RunPhase.Spin, rc.State.Phase, "[proceed] Phase → Spin");
            t.Eq("", rc.State.RewardMessage, "[proceed] RewardMessage 소거");

            // 잘못된 phase에서 시도 → 거부(다른 상태 변경 없음).
            rc.State.Phase = RunPhase.NodeSelect;
            var rejected = rc.Do(new ProceedToStage());
            t.Eq(1, rejected.Count, "[proceed:wrong-phase] 단일 이벤트");
            t.Eq("REJECTED", rejected[0].type, "[proceed:wrong-phase] REJECTED");
            t.Eq("PHASE_NOT_REWARD_DONE", rejected[0].reason, "[proceed:wrong-phase] reason");
            t.Eq(RunPhase.NodeSelect, rc.State.Phase, "[proceed:wrong-phase] Phase 변경 없음");
        }
    }

    // ── B. 노드 해소 → RewardDone 전이 + 보상 메시지(웹 _enterRewardDone 대응 문구) ──────────────
    internal static class Tests_P4_RewardMessages
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();

            // REST — 웹 game.js:1633 "🛌 휴식 — 코인 +12 획득"(astral 제외 문구로 이식).
            var restRun = S4TestHelpers.NewRun(1L);
            restRun.Phase = RunPhase.NodeSelect;
            restRun.NodeOptions.Add(NodeKind.Rest);
            NodeEvents.ChooseNode(restRun, 0, stat);
            t.Eq(RunPhase.RewardDone, restRun.Phase, "[msg:rest] Phase → RewardDone");
            t.Eq("휴식 — 코인 +12 획득", restRun.RewardMessage, "[msg:rest] RewardMessage 문구");

            // SHOP — 구매 없이 나가기: 웹 game.js:2516 "🛒 상점을 둘러봤어요 (구매 없음)".
            var shopRun = S4TestHelpers.NewRun(2L);
            shopRun.Phase = RunPhase.NodeSelect;
            shopRun.NodeOptions.Add(NodeKind.Shop);
            NodeEvents.ChooseNode(shopRun, 0, stat);
            t.Eq(RunPhase.EventShop, shopRun.Phase, "[msg:shop-empty] 상점 진입");
            var leaveEmpty = Shop.Leave(shopRun);
            t.Eq("SHOP_LEFT", leaveEmpty[0].type, "[msg:shop-empty] SHOP_LEFT");
            t.Eq(RunPhase.RewardDone, shopRun.Phase, "[msg:shop-empty] Phase → RewardDone");
            t.Eq("상점을 둘러봤어요 (구매 없음)", shopRun.RewardMessage, "[msg:shop-empty] RewardMessage 문구");

            // SHOP — 1개 구매 후 나가기: ShopBoughtLabels가 조합된 문구로 반영.
            var shopBuyRun = S4TestHelpers.NewRun(3L);
            shopBuyRun.Phase = RunPhase.NodeSelect;
            shopBuyRun.NodeOptions.Add(NodeKind.Shop);
            NodeEvents.ChooseNode(shopBuyRun, 0, stat);
            shopBuyRun.Coins = 10_000; // 가격 무관하게 구매 성공시키기 위한 넉넉한 코인
            var boughtEntry = shopBuyRun.ShopOffer[0];
            var expectedName = (boughtEntry.kind == 'A' || boughtEntry.kind == 'R')
                ? Perks.ById(boughtEntry.id)?.name ?? boughtEntry.id
                : Items.ById(boughtEntry.id)?.name ?? boughtEntry.id;
            var buyEv = Shop.Buy(shopBuyRun, 0);
            t.Eq("SHOP_PURCHASED", buyEv[0].type, "[msg:shop-bought] 구매 성공");
            t.Eq(1, shopBuyRun.ShopBoughtLabels.Count, "[msg:shop-bought] ShopBoughtLabels 1건 누적");
            t.Eq(expectedName, shopBuyRun.ShopBoughtLabels[0], "[msg:shop-bought] 라벨=이름(이모지 없음, 엔진 문자열 규약)");
            Shop.Leave(shopBuyRun);
            t.Eq(RunPhase.RewardDone, shopBuyRun.Phase, "[msg:shop-bought] Phase → RewardDone");
            t.Eq("상점에서 구매: " + expectedName, shopBuyRun.RewardMessage, "[msg:shop-bought] RewardMessage 문구");

            // GAMBLE — 승/패 메시지 형태(RNG 결과에 따라 둘 중 하나).
            var gambleRun = S4TestHelpers.NewRun(4L);
            gambleRun.Phase = RunPhase.NodeSelect;
            gambleRun.NodeOptions.Add(NodeKind.Gamble);
            gambleRun.Coins = 10;
            NodeEvents.ChooseNode(gambleRun, 0, stat);
            t.Eq(RunPhase.RewardDone, gambleRun.Phase, "[msg:gamble] Phase → RewardDone");
            bool isWinMsg = gambleRun.RewardMessage == "도박 성공 — 코인 2배!";
            bool isLoseMsg = gambleRun.RewardMessage == "도박 실패 — 코인 유지";
            t.True(isWinMsg || isLoseMsg, "[msg:gamble] RewardMessage는 성공/실패 문구 중 하나");

            // AUGLEVEL 후보 없음 — 웹 game.js:1622 "강화할 증강이 없어요" 그대로.
            var noCandRun = S4TestHelpers.NewRun(5L);
            noCandRun.Perks.Add("study");
            noCandRun.PerkLevels["study"] = 3;
            noCandRun.Phase = RunPhase.NodeSelect;
            noCandRun.NodeOptions.Add(NodeKind.AugLevel);
            NodeEvents.ChooseNode(noCandRun, 0, stat);
            t.Eq(RunPhase.RewardDone, noCandRun.Phase, "[msg:auglv-empty] Phase → RewardDone");
            t.Eq("강화할 증강이 없어요", noCandRun.RewardMessage, "[msg:auglv-empty] RewardMessage 문구");

            // DEVICE 노드 확정(TakeDevice)은 웹 deviceNodeTake처럼 RewardDone을 건너뛴다 — 예외 확인.
            var devRun = S4TestHelpers.NewRun(6L);
            devRun.Phase = RunPhase.NodeSelect;
            devRun.NodeOptions.Add(NodeKind.Device);
            devRun.PendingDeviceDrop = "dev_flame";
            NodeEvents.ChooseNode(devRun, 0, stat);
            t.Eq(RunPhase.DeviceNode, devRun.Phase, "[msg:device] 오퍼 단계");
            NodeEvents.TakeDevice(devRun, equip: true);
            t.Eq(RunPhase.Spin, devRun.Phase, "[msg:device] TakeDevice는 RewardDone을 건너뛰고 곧장 Spin");
        }
    }

    // ── C. RewardDoneView.NextPreview — 손계산 대조 ──────────────────────────────────────────────
    internal static class Tests_P4_NextPreview_HandCalc
    {
        public static void Run(TestCtx t)
        {
            // stage=1, novice(quotaMul×0.92), 퍽/저주/장치 없음, 보스 아님.
            // Formulas.Quota(1)=110. QuotaMulFor(1)=1.0(비보스). prop=1.0(bsp=0). quota=(long)(110*0.92)=101.
            // SpinsPerStage=5(bonusSpins=0). Bosses.Spins(1)=0. EffSpins=max(5+0+0,3)=5.
            var run1 = S4TestHelpers.NewRun(10L, "novice", "basic", "");
            run1.Stage = 1;
            var np1 = RewardDoneView.NextPreview(run1);
            t.Eq(1, np1.stage, "[preview:s1] stage");
            t.Eq(101L, np1.quota, "[preview:s1] quota 손계산(110×0.92=101.2→101)");
            t.Eq(5, np1.spins, "[preview:s1] spins");
            t.Eq("", np1.bossId, "[preview:s1] 비보스 스테이지 → bossId 빈 문자열");

            // stage=5(보스 "finals" — (5/5-1)%4=0), novice, 퍽 없음.
            // Formulas.Quota(5)=150. finals quotaMul=1.0. baseSpins=5, bsp=Bosses.Spins(5)=1(finals bonusSpins).
            // prop=(5+1)/5=1.2. quota=(long)(150*0.92*1.0*1.2)=(long)165.6=165.
            // EffSpins=max(5+0+1,3)=6.
            var run5 = S4TestHelpers.NewRun(11L, "novice", "basic", "");
            run5.Stage = 5;
            var np5 = RewardDoneView.NextPreview(run5);
            t.Eq(5, np5.stage, "[preview:s5] stage");
            t.Eq(165L, np5.quota, "[preview:s5] quota 손계산(150×0.92×1.2=165.6→165)");
            t.Eq(6, np5.spins, "[preview:s5] spins(기본5+보스+1)");
            t.Eq("finals", np5.bossId, "[preview:s5] 보스 id=finals(순환 인덱스 0)");
        }
    }

    // ── D. RewardDoneView.CurrentStats — 손계산 대조(대표 퍽 2종: study/cherry_up) ─────────────────
    internal static class Tests_P4_CurrentStats_HandCalc
    {
        public static void Run(TestCtx t)
        {
            // 퍽 없음 — 전부 무보정이라 rows/symExp/symScore/tags 전부 빈 리스트여야 한다(novice의
            // quotaMul만 유일한 비1.0 값이라 "요구 EXP" 행 1개만 나온다 — mul() 헬퍼가 항상 표시함).
            var baseRun = S4TestHelpers.NewRun(20L, "novice", "basic", "");
            var baseStats = RewardDoneView.CurrentStats(baseRun);
            t.Eq(1, baseStats.rows.Count, "[stats:base] novice quotaMul(0.92)만 유일한 행");
            t.Eq("요구 EXP", baseStats.rows[0].label, "[stats:base] 행 라벨=요구 EXP");
            t.Eq("×0.92", baseStats.rows[0].value, "[stats:base] 값=×0.92");
            t.True(baseStats.rows[0].up, "[stats:base] 요구 EXP 하락은 up=true(낮을수록 이득)");
            t.Eq(0, baseStats.symExp.Count, "[stats:base] symExp 없음");

            // study(expMul×1.10) + cherry_up(perSymbolExp.cherry+2) 보유 — gambler(캐릭터 효과 없음)로
            // 캐릭터 기여를 배제하고 퍽 2종의 순수 기여만 손계산.
            var run = S4TestHelpers.NewRun(21L, "gambler", "basic", "");
            run.Perks.Add("study");
            run.Perks.Add("cherry_up");
            var stats = RewardDoneView.CurrentStats(run);

            var expMulRow = stats.rows.FirstOrDefault(r => r.label == "모든 EXP");
            t.True(expMulRow != null, "[stats:perk] '모든 EXP' 행 존재(study expMul×1.10)");
            if (expMulRow != null)
            {
                t.Eq("×1.1", expMulRow.value, "[stats:perk] study expMul 손계산 ×1.1(끝0 제거 표기)");
                t.True(expMulRow.up, "[stats:perk] expMul>1 → up=true");
            }

            t.Eq(1, stats.symExp.Count, "[stats:perk] symExp 1건(cherry_up)");
            t.Eq("체리+2", stats.symExp.FirstOrDefault(), "[stats:perk] cherry_up 손계산: 체리+2");
            t.Eq(0, stats.symScore.Count, "[stats:perk] symScore 없음(두 퍽 다 perSymbolScore 미사용)");
            t.Eq(0, stats.tags.Count, "[stats:perk] tags 없음");
        }
    }

    // ── E. CellInfoView.Build — EXP/점수 분해 손계산 + 영향 퍽 델타 ────────────────────────────────
    internal static class Tests_P4_CellInfo_HandCalc
    {
        public static void Run(TestCtx t)
        {
            NoLastSpin(t);
            BaseBreakdown(t);
            AffectingPerk(t);
        }

        // Opus 2차검수 필수①(2026-08-09) — CellInfoView가 LastCells(raw)가 아니라 LastCellsFinal(Cell
        // 객체, Evaluate 이후 최종)을 읽도록 바뀌어 손계산 테스트도 이 필드를 채운다. 심볼 id →
        // SymInfo 변환은 CellInfoView.Build/실제 엔진과 동일하게 Symbols.ById만 쓴다(중복 불가).
        private static Cell CellOf(string symId, string tag = "") => new Cell(Symbols.ById(symId), tag);

        private static void NoLastSpin(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(30L);
            t.True(CellInfoView.Build(run, 0) == null, "[cellinfo:none] 직전 스핀 없으면 null(웹 cellInfo 초반 가드)");
        }

        // 퍽/저주/장치 전혀 없는 상태 — 심볼 기본값 그대로 분해되는지 확인(가운데 칸=index2, reel=5).
        private static void BaseBreakdown(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(31L, "novice", "basic", "");
            run.LastCellsFinal.Clear();
            run.LastCellsFinal.AddRange(new[] { CellOf("cherry"), CellOf("book"), CellOf("star"), CellOf("gem"), CellOf("crown") });
            run.LastSpinNo = 0;
            run.LastMods = ModsBuilder.Build("basic", "novice", System.Array.Empty<string>(), System.Array.Empty<string>(), "");

            // cell0 = cherry(비중앙, 첫칸) — baseExp=3, 보정 전부 0 → cellExp=3.
            var c0 = CellInfoView.Build(run, 0);
            t.True(c0 != null, "[cellinfo:base] cell0 산출됨");
            t.Eq(3L, c0.baseExp, "[cellinfo:base] cell0 baseExp=3(cherry.exp)");
            t.Eq(0, c0.perSymExp, "[cellinfo:base] cell0 perSymExp=0(퍽 없음)");
            t.Eq(3L, c0.cellExp, "[cellinfo:base] cell0 cellExp=3(가운데 아님, 배수 없음)");
            t.Eq(0L, c0.cellScore, "[cellinfo:base] cell0 cellScore=0(cherry.score=0)");
            t.True(c0.isFirst, "[cellinfo:base] cell0 isFirst=true(spinNo=0)");
            t.True(!c0.isCenter, "[cellinfo:base] cell0 isCenter=false");

            // cell2 = star(가운데) — baseExp=8, centerMul=1.0(퍽 없음) → cellExp=8.
            var c2 = CellInfoView.Build(run, 2);
            t.True(c2.isCenter, "[cellinfo:base] cell2 isCenter=true(reel=5 → idx==2)");
            t.Eq(8L, c2.baseExp, "[cellinfo:base] cell2 baseExp=8(star.exp)");
            t.Eq(1.0, c2.centerMul, "[cellinfo:base] cell2 centerMul=1.0(퍽 없음)");
            t.Eq(8L, c2.cellExp, "[cellinfo:base] cell2 cellExp=8");

            // cell4 = crown(희귀) — baseExp=20, score=50.
            var c4 = CellInfoView.Build(run, 4);
            t.Eq(20L, c4.baseExp, "[cellinfo:base] cell4 baseExp=20(crown.exp)");
            t.Eq(50L, c4.baseScore, "[cellinfo:base] cell4 baseScore=50(crown.score)");
            t.Eq(50L, c4.cellScore, "[cellinfo:base] cell4 cellScore=50(보정 없음)");

            // 태그 텍스트/배지 등 표시 보조 필드도 최소 확인(널 아님).
            t.True(c0.sym.tags != null && c0.sym.tags.Contains("생명"), "[cellinfo:base] cell0 sym.tags에 '생명' 포함(cherry 원본 태그)");
        }

        // cherry_up(perSymbolExp.cherry+2) 보유 시 cell0(cherry) 분해값과 affecting 목록에 반영되는지.
        private static void AffectingPerk(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(32L, "novice", "basic", "");
            run.Perks.Add("cherry_up");
            run.LastCellsFinal.Clear();
            run.LastCellsFinal.AddRange(new[] { CellOf("cherry"), CellOf("book"), CellOf("star"), CellOf("gem"), CellOf("crown") });
            run.LastSpinNo = 0;
            run.LastMods = ModsBuilder.Build("basic", "novice", run.Perks, System.Array.Empty<string>(), "");

            var c0 = CellInfoView.Build(run, 0);
            t.Eq(2, c0.perSymExp, "[cellinfo:affect] cell0 perSymExp=2(cherry_up)");
            t.Eq(5L, c0.cellExp, "[cellinfo:affect] cell0 cellExp=5(기본3+퍽2)");

            var cherryUpEntry = c0.affecting.FirstOrDefault(a => a.id == "cherry_up");
            t.True(cherryUpEntry != null, "[cellinfo:affect] cell0 affecting에 cherry_up 포함");
            if (cherryUpEntry != null)
            {
                t.Eq("증강", cherryUpEntry.kind, "[cellinfo:affect] cherry_up kind=증강");
                t.True(cherryUpEntry.effect.Contains("체리EXP +2"), "[cellinfo:affect] cherry_up effect에 '체리EXP +2' 포함",
                    "실제: " + cherryUpEntry.effect);
            }

            // star 칸(cell2)에는 cherry_up이 아무 영향도 안 준다 — affecting에서 제외돼야 함.
            var c2 = CellInfoView.Build(run, 2);
            t.True(!c2.affecting.Any(a => a.id == "cherry_up"), "[cellinfo:affect] cell2(star)에는 cherry_up 미노출(무관한 심볼)");
        }
    }

    // ── F. Opus 2차검수 필수⑥ 테스트 보강 4건(2026-08-09) ─────────────────────────────────────────
    // 손계산이 아니라 실제 RunController.Do(new Spin(...))를 태워, CellInfoView가 진짜 엔진 산출물과
    // 어긋나지 않는지 확인한다(HandCalc 그룹은 하드코딩 상태 기반 — 이 그룹은 RNG 기반 실전 검증).

    // F-1. "클린" 스핀(세트·잭팟·해골·특수효과 전혀 없음)에서는 칸별 cellExp 합이 result.exp/gained와
    // 정확히 일치해야 한다(SpinResolver.Evaluate의 per-cell 누적 루프, SpinResolver.cs:337-351과
    // CellInfoView의 per-cell 공식이 완전히 같은 식이라 — 세트/잭팟/인접/희귀버스트 등 "칸에
    // 귀속되지 않는" 보너스가 전혀 없는 스핀에서는 잔차가 0이어야 정상이다).
    internal static class Tests_P4_CellInfo_RealSpinReconciliation
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            int cleanFound = 0;
            for (long seed = 1; seed < 4000 && cleanFound < 5; seed++)
            {
                var rc = new RunController("gambler", "basic", "", seed, stat);
                var events = rc.Do(new Spin(SpinMode.N));
                if (events.Count == 0 || events[0].type != "SPIN_RESULT") continue; // 클리어/거부 등은 스킵
                var outcome = events[0].spin;
                var res = outcome.result;
                if (res == null) continue;

                bool clean = res.bestSetCount < 2 && res.jackpotSym == null && res.skulls == 0;
                if (clean)
                    foreach (var c in res.cells)
                        if (c.sym.special != Sp.NONE) { clean = false; break; }
                if (!clean) continue;

                var run = rc.State;
                long sum = 0;
                for (int i = 0; i < run.LastCellsFinal.Count; i++)
                {
                    var info = CellInfoView.Build(run, i);
                    t.True(info != null, $"[real-spin seed={seed}] cell{i} 산출됨");
                    if (info != null) sum += info.cellExp;
                }
                t.Eq(res.exp, sum, $"[real-spin seed={seed}] 칸별 cellExp 합 == result.exp(클린 스핀, 잔차 0)");
                t.Eq(outcome.gained, sum, $"[real-spin seed={seed}] 칸별 cellExp 합 == outcome.gained");
                cleanFound++;
            }
            t.True(cleanFound >= 3, $"[real-spin] 4000시드 내 클린 스핀 최소 3건 확보(실제={cleanFound})");
        }
    }

    // F-2. 폭탄/자석 포함 스핀 — LastCellsFinal이 릴에 실제로 보이는 "최종" 상태(폭탄 제거 → empty,
    // 자석 복사 → 이웃 심볼)를 정확히 반영하는지 확인. Opus 2차검수 필수①/②의 핵심 시나리오.
    internal static class Tests_P4_CellInfo_BombMagnetFinalCells
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            bool bombVerified = false, magnetVerified = false;
            for (long seed = 1; seed < 6000 && !(bombVerified && magnetVerified); seed++)
            {
                var rc = new RunController("gambler", "basic", "", seed, stat);
                rc.Do(new Spin(SpinMode.N));
                var run = rc.State;
                if (run.LastCellsFinal.Count == 0) continue;

                if (!bombVerified)
                {
                    for (int i = 0; i < run.LastCellsFinal.Count; i++)
                    {
                        if (run.LastCellsFinal[i].sym.id != "empty" || run.LastCellsFinal[i].tag != "💥") continue;
                        // raw(LastCells, Evaluate 이전)에는 이 칸이 empty가 아니었어야 한다 — 폭탄이
                        // "이웃"을 지운 것이지 원래부터 빈칸이 아니었음을 확인(LastCells/LastCellsFinal
                        // 분리의 존재 이유 자체를 증명).
                        t.True(i < run.LastCells.Count && run.LastCells[i] != "empty",
                            $"[bomb seed={seed}] raw LastCells[{i}]는 empty가 아님(Evaluate 이전 원본)");
                        var info = CellInfoView.Build(run, i);
                        t.True(info != null, $"[bomb seed={seed}] 빈 칸 정보 산출됨");
                        if (info != null)
                        {
                            t.True(info.empty, $"[bomb seed={seed}] empty=true");
                            t.Eq(0L, info.cellExp, $"[bomb seed={seed}] cellExp=0(제거된 칸)");
                            t.True(info.specials.Contains("폭탄으로 제거된 빈 칸 (EXP 0)"),
                                $"[bomb seed={seed}] specials에 폭탄 안내 포함", string.Join("|", info.specials));
                        }
                        bombVerified = true;
                        break;
                    }
                }
                if (!magnetVerified)
                {
                    for (int i = 0; i < run.LastCellsFinal.Count; i++)
                    {
                        if (run.LastCellsFinal[i].tag != "🧲") continue;
                        var info = CellInfoView.Build(run, i);
                        t.True(info != null, $"[magnet seed={seed}] 정보 산출됨");
                        if (info != null)
                        {
                            t.True(!info.empty, $"[magnet seed={seed}] 자석 복사칸은 empty 아님(실 심볼로 복사됨)");
                            t.True(info.specials.Contains("자석으로 복사된 칸"),
                                $"[magnet seed={seed}] specials에 자석 안내 포함", string.Join("|", info.specials));
                        }
                        magnetVerified = true;
                        break;
                    }
                }
            }
            t.True(bombVerified, "[bomb] 6000시드 내 폭탄 제거 칸 최소 1건 확보·검증");
            t.True(magnetVerified, "[magnet] 6000시드 내 자석 복사 칸 최소 1건 확보·검증");
        }
    }

    // F-3. MANIP(재굴림 등) 이후 LastMods/LastCellsFinal이 "그 순간"으로 실제 갱신되는지 — 스핀과
    // MANIP 사이에 퍽을 추가해 LastMods가 stale 캐시가 아니라 재계산됨을 값으로 증명한다.
    internal static class Tests_P4_CellInfo_ManipUpdatesCache
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var rc = new RunController("gambler", "basic", "dev_reroll", 777L, stat);
            rc.Do(new Spin(SpinMode.N));
            var run = rc.State;
            t.True(run.LastMods != null, "[manip] 스핀 직후 LastMods 세팅됨");
            t.Eq(1.0, run.LastMods.expMul, "[manip] 스핀 시점엔 퍽 없음 → expMul 기본값 1.0");
            t.True(run.LastCellsFinal.Count > 0, "[manip] 스핀 직후 LastCellsFinal 세팅됨");

            // study(expMul×1.10) 퍽을 스핀과 MANIP 사이에 추가 — HandleManip이 호출 시점 run.Perks를
            // 다시 읽어 mods를 재계산한다는 것을 값 변화로 증명(참조만 바뀌는 걸로는 증명 안 됨).
            run.Perks.Add("study");
            run.Coins = 100;
            var ev = rc.Do(new DeviceCmd("dev_reroll"));
            t.Eq("DEVICE_MANIP_RESULT", ev[0].type, "[manip] dev_reroll 성공");
            var res = ev[0].spin.result;
            t.True(res != null, "[manip] 결과 SpinResult 확보");

            t.Eq(1.10, run.LastMods.expMul, "[manip] MANIP 이후 LastMods가 새 퍽(study) 반영해 재계산됨(1.0→1.10)");
            t.Eq(res.cells.Count, run.LastCellsFinal.Count, "[manip] LastCellsFinal 개수가 이벤트 result.cells와 일치");
            for (int i = 0; i < res.cells.Count; i++)
            {
                t.Eq(res.cells[i].sym.id, run.LastCellsFinal[i].sym.id,
                    $"[manip] LastCellsFinal[{i}].sym.id == 이벤트 result.cells[{i}](웹 game.js:1286 파리티)");
                t.Eq(res.cells[i].tag, run.LastCellsFinal[i].tag, $"[manip] LastCellsFinal[{i}].tag 일치");
            }
        }
    }

    // F-3-2. WEB_PARITY_DESIGN.md §2-(W) "신규 발견"(2026-08-09 Opus 2차검수, P4-3 슬라이스에서 해소) —
    // DeviceActions.HandleManip이 조작 대상을 run.LastCellsFinal(웹 통합 manip()의 `r.lastCells` =
    // Evaluate 이후 최종 칸)에서 복원하는지, 폭탄으로 실제로 빈칸이 된 자리를 dev_pin/dev_copy가
    // "화면에 보이는 대로"(빈칸) 취급하는지 실제 폭탄 스핀으로 검증한다. 이 수정 전에는 run.LastCells
    // (raw, Evaluate 이전 원본 심볼)에서 복원해 폭탄이 지운 칸의 "원래 심볼"이 되살아나는 파리티 차이가
    // 있었다.
    internal static class Tests_P4_3_ManipUsesFinalCells
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            bool pinVerified = Verify(t, stat, "dev_pin", 3, "pin");
            bool copyVerified = Verify(t, stat, "dev_copy", 5, "copy");
            t.True(pinVerified, "[manip-final:pin] 6000시드 내 검증 케이스 최소 1건 확보");
            t.True(copyVerified, "[manip-final:copy] 6000시드 내 검증 케이스 최소 1건 확보");
        }

        // 장치를 처음부터 장착한 채(다른 F-3 테스트와 동일하게 RunController 3번째 인자로 직접 지정 —
        // 별도 RunState 재구성 없이 동일 RNG 스트림으로 "찾기"와 "검증"을 한 번에 한다) 1회 스핀해 폭탄이
        // 실제로 칸을 지웠는지 찾고, 그 자리에서 곧바로 dev_pin/dev_copy를 발동해 결과 칸을 확인한다.
        private static bool Verify(TestCtx t, IReadOnlyDictionary<string, long> stat, string deviceId, int cost, string tag)
        {
            int nPhaseOk = 0, nBombFound = 0, nRejected = 0;
            string lastRejectReason = "";
            for (long seed = 1; seed < 6000; seed++)
            {
                var rc = new RunController("gambler", "basic", deviceId, seed, stat);
                rc.Do(new Spin(SpinMode.N));
                var run = rc.State;
                if (run.Phase != RunPhase.Spin || run.LastCellsFinal.Count == 0) continue;
                run.Coins = System.Math.Max(run.Coins, 100); // 코인 부족은 이 테스트의 관찰 대상이 아니다(셀 기준만 검증).
                nPhaseOk++;

                int bombIdx = -1;
                for (int i = 0; i < run.LastCellsFinal.Count; i++)
                {
                    if (run.LastCellsFinal[i].sym.id != "empty" || run.LastCellsFinal[i].tag != "💥") continue;
                    // raw(LastCells, Evaluate 이전)에는 이 칸이 empty가 아니었어야 한다 — 폭탄이 실제로
                    // 뭔가를 지웠다는 전제(F-2 Tests_P4_CellInfo_BombMagnetFinalCells와 동일 검사).
                    if (i < run.LastCells.Count && run.LastCells[i] != "empty") { bombIdx = i; break; }
                }
                if (bombIdx < 0) continue;
                nBombFound++;

                if (deviceId == "dev_pin")
                {
                    var ev = DeviceActions.Handle(run, deviceId, bombIdx + 1); // 폭탄이 지운 칸을 그대로 "고정"
                    if (ev.Count == 0 || ev[0].type == "REJECTED" || ev[0].spin?.result == null)
                    { nRejected++; lastRejectReason = ev.Count > 0 ? ev[0].reason : "no-event"; continue; }
                    var cells = ev[0].spin.result.cells;
                    t.Eq("empty", cells[bombIdx].sym.id,
                        $"[manip-final:{tag} seed={seed}] dev_pin으로 고정한 칸이 화면 표시대로(빈칸) 유지됨 — " +
                        "raw(LastCells)를 썼다면 폭탄 이전 원본 심볼이 되살아났을 것");
                    return true;
                }
                else
                {
                    int dst = bombIdx + 1 < run.LastCellsFinal.Count ? bombIdx + 1 : bombIdx - 1;
                    if (dst < 0) continue;
                    // 폭탄이 지운 칸(bombIdx)을 인접 칸(dst)에 "복사" — 화면대로면 dst도 빈칸이 돼야 한다.
                    var ev = DeviceActions.Handle(run, deviceId, bombIdx + 1);
                    if (ev.Count == 0 || ev[0].type == "REJECTED" || ev[0].spin?.result == null)
                    { nRejected++; lastRejectReason = ev.Count > 0 ? ev[0].reason : "no-event"; continue; }
                    var cells = ev[0].spin.result.cells;
                    t.Eq("empty", cells[dst].sym.id,
                        $"[manip-final:{tag} seed={seed}] dev_copy가 폭탄으로 빈 칸을 그대로(빈칸) 복사함 — " +
                        "raw(LastCells)를 썼다면 폭탄 이전 원본 심볼이 복사됐을 것");
                    return true;
                }
            }
            t.Report($"manip-final:{tag} diag", $"phaseOk={nPhaseOk} bombFound={nBombFound} rejected={nRejected} lastReason={lastRejectReason}");
            return false;
        }
    }

    // F-4. EVENT 10종 표 결과의 RewardMessage 정확한 문구 — EventRewardMessage가 coinsDelta를
    // scoreDelta보다 먼저 조립한다는 실제 필드 순서까지 정확히 검증(순서를 잘못 가정하면 이 테스트가
    // 바로 깨진다 — NodeEvents.cs:170-171 실제 순서 그대로).
    internal static class Tests_P4_EventRewardMessage
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            VerifyRoll(t, stat, 0, "이벤트 — 코인+15");
            VerifyRoll(t, stat, 1, "이벤트 — 점수+200");
            VerifyRoll(t, stat, 2, "이벤트 — 코인+30");
            VerifyRoll(t, stat, 3, "이벤트 — 코인+12 · 점수+100"); // coinsDelta가 scoreDelta보다 먼저 조립됨
        }

        private static void VerifyRoll(TestCtx t, IReadOnlyDictionary<string, long> stat, int targetRoll, string expected)
        {
            for (long seed = 1; seed < 3000; seed++)
            {
                var run = S4TestHelpers.NewRun(seed);
                run.Phase = RunPhase.NodeSelect;
                run.NodeOptions.Add(NodeKind.Event);
                var ev = NodeEvents.ChooseNode(run, 0, stat);
                if (ev[0].eventRoll != targetRoll) continue;
                t.Eq(expected, run.RewardMessage, $"[event-msg roll={targetRoll}] RewardMessage 정확한 문구");
                return;
            }
            t.Fail($"[event-msg roll={targetRoll}]", "3000시드 내 해당 roll을 찾지 못함");
        }
    }
}
