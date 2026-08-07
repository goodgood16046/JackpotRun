using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // P3-3 슬라이스(WEB_PARITY_DESIGN.md §1-A #12) 골든 테스트 — 증강 레벨업(AUG_LEVELS 12종) +
    // AUGLEVEL 노드(10%+pity, 상한20%) 검증. 웹 engine.js:19-33(AUG_LEVELS)·368-375(buildMods 델타
    // 루프)·game.js:1501-1507(노드 확률)·2142-2145(픽 처리) 손계산 대조.
    // Engine/Content/AugLevels.cs · Engine/Run/{Mods,RunState,StageFlow,NodeEvents}.cs 검증.

    // ── ① AUG_LEVELS 12종 골든 델타 — 웹 engine.js:20-31 손계산 그대로 ModsBuilder.Build로 대조 ─────
    internal static class Tests_P3_AugLevel_GoldenDeltas
    {
        public static void Run(TestCtx t)
        {
            // (id, level, 실제 값 조회자, Lv1/Lv2/Lv3 기대값) — Lv1은 Perks.cs 기존 fx(무보정), Lv2/3는
            // AugLevels.cs 델타 누적 적용 결과. 배수형은 EqTol(부동소수 누적오차 허용), 가산형은 정수 Eq.
            AssertMul("study", "expMul", 1.10, 1.15005, 1.19950215, t);
            AssertMul("greed", "expMul", 1.25, 1.32, 1.40052, t);
            AssertMul("polymath", "expMul", 1.20, 1.26, 1.32048, t);
            AssertAddSymExp("cherry_up", Sym.Cherry, 2, 3, 4, t);
            AssertAddSymExp("book_up", Sym.Book, 2, 3, 4, t);
            AssertAddSymExp("star_up", Sym.Star, 2, 3, 4, t);
            AssertFlatExp("diligence", 3, 5, 7, t);
            AssertMul("set_sense", "setExpMul", 1.3, 1.4001, 1.4995071, t);
            AssertMul("coin_luck", "coinMul", 1.3, 1.4001, 1.4995071, t);
            AssertSkullExp("skull_study", 6, 8, 10, t);
            AssertAddSymScore("gem_polish", Sym.Gem, 10, 13, 16, t);
            AssertMul("lucky", "rareWeightMul", 1.2, 1.248, 1.29792, t);
        }

        private static Mods Build(string perkId, int? level)
        {
            var levels = level.HasValue ? new Dictionary<string, int> { [perkId] = level.Value } : null;
            return ModsBuilder.Build("basic", "novice", new List<string> { perkId }, Array.Empty<string>(), "", null, levels);
        }

        private static void AssertMul(string id, string field, double lv1, double lv2, double lv3, TestCtx t)
        {
            double Get(Mods m) => field switch
            {
                "expMul" => m.expMul,
                "setExpMul" => m.setExpMul,
                "coinMul" => m.coinMul,
                "rareWeightMul" => m.rareWeightMul,
                _ => throw new InvalidOperationException(field),
            };
            t.EqTol(lv1, Get(Build(id, null)), $"[auglv-golden] {id} Lv1(levels 미지정) {field}={lv1}");
            t.EqTol(lv1, Get(Build(id, 1)), $"[auglv-golden] {id} Lv1(명시) {field}={lv1}");
            t.EqTol(lv2, Get(Build(id, 2)), $"[auglv-golden] {id} Lv2 {field}={lv2}(손계산: Lv1×Lv2델타)");
            t.EqTol(lv3, Get(Build(id, 3)), $"[auglv-golden] {id} Lv3 {field}={lv3}(손계산: Lv1×Lv2델타×Lv3델타, 누적)");
        }

        private static void AssertAddSymExp(string id, Sym sym, int lv1, int lv2, int lv3, TestCtx t)
        {
            int Get(Mods m) => m.perSymbolExp.TryGetValue(sym, out var v) ? v : 0;
            t.Eq(lv1, Get(Build(id, null)), $"[auglv-golden] {id} Lv1 perSymbolExp.{sym}={lv1}");
            t.Eq(lv2, Get(Build(id, 2)), $"[auglv-golden] {id} Lv2 perSymbolExp.{sym}={lv2}");
            t.Eq(lv3, Get(Build(id, 3)), $"[auglv-golden] {id} Lv3 perSymbolExp.{sym}={lv3}");
        }

        private static void AssertAddSymScore(string id, Sym sym, int lv1, int lv2, int lv3, TestCtx t)
        {
            int Get(Mods m) => m.perSymbolScore.TryGetValue(sym, out var v) ? v : 0;
            t.Eq(lv1, Get(Build(id, null)), $"[auglv-golden] {id} Lv1 perSymbolScore.{sym}={lv1}");
            t.Eq(lv2, Get(Build(id, 2)), $"[auglv-golden] {id} Lv2 perSymbolScore.{sym}={lv2}");
            t.Eq(lv3, Get(Build(id, 3)), $"[auglv-golden] {id} Lv3 perSymbolScore.{sym}={lv3}");
        }

        private static void AssertFlatExp(string id, int lv1, int lv2, int lv3, TestCtx t)
        {
            t.Eq(lv1, Build(id, null).flatExp, $"[auglv-golden] {id} Lv1 flatExp={lv1}");
            t.Eq(lv2, Build(id, 2).flatExp, $"[auglv-golden] {id} Lv2 flatExp={lv2}");
            t.Eq(lv3, Build(id, 3).flatExp, $"[auglv-golden] {id} Lv3 flatExp={lv3}");
        }

        private static void AssertSkullExp(string id, int lv1, int lv2, int lv3, TestCtx t)
        {
            t.Eq(lv1, Build(id, null).skullExp, $"[auglv-golden] {id} Lv1 skullExp={lv1}");
            t.Eq(lv2, Build(id, 2).skullExp, $"[auglv-golden] {id} Lv2 skullExp={lv2}");
            t.Eq(lv3, Build(id, 3).skullExp, $"[auglv-golden] {id} Lv3 skullExp={lv3}");
        }
    }

    // ── ② Lv3 상한 — levels에 4/5 같은 과대값을 넣어도 Lv3와 동일해야 함(AUG_LEVELS엔 2/3 키만 존재) ──
    internal static class Tests_P3_AugLevel_Lv3Clamp
    {
        public static void Run(TestCtx t)
        {
            var lv3 = ModsBuilder.Build("basic", "novice", new List<string> { "study" }, Array.Empty<string>(), "", null,
                new Dictionary<string, int> { ["study"] = 3 });
            var lv5 = ModsBuilder.Build("basic", "novice", new List<string> { "study" }, Array.Empty<string>(), "", null,
                new Dictionary<string, int> { ["study"] = 5 });
            var lv99 = ModsBuilder.Build("basic", "novice", new List<string> { "study" }, Array.Empty<string>(), "", null,
                new Dictionary<string, int> { ["study"] = 99 });
            t.EqTol(lv3.expMul, lv5.expMul, "[auglv-clamp] levels=5는 Lv3와 동일 결과(Lv4/5 델타 자체가 없음)");
            t.EqTol(lv3.expMul, lv99.expMul, "[auglv-clamp] levels=99도 Lv3와 동일 결과");
        }
    }

    // ── ③ 미등록 id(프리즘 등)는 levels가 있어도 무영향 — AugLevels.IsLevelable(id)==false 스킵 ──
    internal static class Tests_P3_AugLevel_UnregisteredIdIgnored
    {
        public static void Run(TestCtx t)
        {
            t.True(!AugLevels.IsLevelable("prism_diploma"), "[auglv-unregistered] 프리즘은 AUG_LEVELS 미등록");
            var withLevel = ModsBuilder.Build("basic", "novice", new List<string> { "prism_diploma" }, Array.Empty<string>(), "", null,
                new Dictionary<string, int> { ["prism_diploma"] = 3 });
            var withoutLevel = ModsBuilder.Build("basic", "novice", new List<string> { "prism_diploma" }, Array.Empty<string>(), "", null, null);
            t.EqTol(withoutLevel.expMul, withLevel.expMul, "[auglv-unregistered] 미등록 증강은 levels 지정해도 결과 불변(expMul)");
            t.EqTol(withoutLevel.scoreMul, withLevel.scoreMul, "[auglv-unregistered] 미등록 증강은 levels 지정해도 결과 불변(scoreMul)");
        }
    }

    // ── ④ 레벨업 반영 스핀 결과(Lv0 vs Lv3) — study 보유, 다른 조건 동일한 두 mods를 직접 대조 ────────
    internal static class Tests_P3_AugLevel_SpinDeltaLv0VsLv3
    {
        public static void Run(TestCtx t)
        {
            var perks = new List<string> { "study" };
            var lv1Mods = ModsBuilder.Build("basic", "novice", perks, Array.Empty<string>(), "", null,
                new Dictionary<string, int>()); // 빈 딕셔너리 = 전부 Lv1 취급(TryGetValue 실패 → 기본 1)
            var lv3Mods = ModsBuilder.Build("basic", "novice", perks, Array.Empty<string>(), "", null,
                new Dictionary<string, int> { ["study"] = 3 });

            t.EqTol(1.10, lv1Mods.expMul, "[auglv-spindelta] Lv1 study expMul=1.10");
            t.EqTol(1.19950215, lv3Mods.expMul, "[auglv-spindelta] Lv3 study expMul=1.19950215");
            t.True(lv3Mods.expMul > lv1Mods.expMul, "[auglv-spindelta] Lv3 expMul이 Lv1보다 큼(체감 가능한 차이)");

            // 동일 100 EXP 원점수를 곱했을 때 실사용부 차이가 실제로 벌어지는지(정수 내림 비교로 확인).
            long baseExp = 100;
            long lv1Applied = (long)(baseExp * lv1Mods.expMul);
            long lv3Applied = (long)(baseExp * lv3Mods.expMul);
            t.Eq(110L, lv1Applied, "[auglv-spindelta] Lv1: 100 EXP × 1.10 = 110(내림)");
            t.Eq(119L, lv3Applied, "[auglv-spindelta] Lv3: 100 EXP × 1.19950215 = 119(내림)");
        }
    }

    // ── ⑤ AUGLEVEL 노드 확률(pity) — 구조적 불변식(발동 시 리셋 0.10 / 미발동 시 +0.02 상한0.20) ──────
    // RNG 실제값을 손계산하지 않고도(비결정적 예측 대신) 코드의 pity 갱신 규칙 자체가 매 클리어마다
    // 정확히 지켜지는지를 검증한다 — 어떤 시드를 넣어도 100% 성립해야 하는 불변식.
    internal static class Tests_P3_AugLevel_NodeRollPityInvariant
    {
        public static void Run(TestCtx t)
        {
            RunSeed(t, 55555L);
            RunSeed(t, 909090L);
        }

        private static void RunSeed(TestCtx t, long seed)
        {
            var run = S4TestHelpers.NewRun(seed);
            run.Perks.Add("study"); // AUG_LEVELS 등록 & Lv1(<3) — LevelableHeld가 매 클리어 계속 유지.
            int triggeredCount = 0;

            for (int i = 0; i < 40; i++)
            {
                double before = run.AugLevelChance;
                var outcome = MakeInstantClearOutcome(run);
                var clear = StageFlow.ClearStage(run, outcome);
                bool hasAugLevel = clear.nodeOptions.Contains(NodeKind.AugLevel);
                bool hasAugment = clear.nodeOptions.Contains(NodeKind.Augment);

                if (hasAugLevel)
                {
                    triggeredCount++;
                    t.True(!hasAugment, $"[auglv-pity seed={seed} i={i}] 발동 시 AUGMENT 자리를 AUGLEVEL이 대체(둘 다 있으면 안 됨)");
                    t.EqTol(0.10, run.AugLevelChance, $"[auglv-pity seed={seed} i={i}] 발동 후 pity 0.10으로 리셋");
                }
                else
                {
                    t.True(hasAugment, $"[auglv-pity seed={seed} i={i}] 미발동이면 AUGMENT 자리 유지");
                    double expected = Math.Min(0.20, before + 0.02);
                    t.EqTol(expected, run.AugLevelChance, $"[auglv-pity seed={seed} i={i}] 미발동 시 +0.02(상한0.20) — before={before}");
                }
                t.True(run.AugLevelChance >= 0.10 && run.AugLevelChance <= 0.20, $"[auglv-pity seed={seed} i={i}] pity는 항상 [0.10,0.20] 구간");
            }

            // 40회 클리어 동안 최소 1회는 발동해야 정상(10%→20%로 누적되는 pity라 통계적으로 사실상 필연 —
            // 실패 시 RNG 배선 자체가 끊어졌을 가능성이 높다는 신호).
            t.True(triggeredCount > 0, $"[auglv-pity seed={seed}] 40회 클리어 중 AUGLEVEL이 최소 1회는 등장(pity 누적 확인)");
        }

        private static SpinOutcome MakeInstantClearOutcome(RunState run) => new SpinOutcome
        {
            rejected = false, mode = SpinMode.N, result = null, gained = 0,
            newExp = 10_000_000, newScore = run.Score, newCoins = run.Coins,
            newSpinIndex = 1, quota = 100, spins = 5,
        };
    }

    // ── ⑥ 레벨업 가능 보유증강 없으면 AUGLEVEL 절대 미등장 + pity 자체가 갱신되지 않음(게이트 확인) ──
    internal static class Tests_P3_AugLevel_NodeRollGatedByLevelableHeld
    {
        public static void Run(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(31337L);
            run.Perks.Add("preview"); // AUG_LEVELS 미등록 증강만 보유
            double initialChance = run.AugLevelChance;

            for (int i = 0; i < 30; i++)
            {
                var outcome = new SpinOutcome
                {
                    rejected = false, mode = SpinMode.N, result = null, gained = 0,
                    newExp = 10_000_000, newScore = run.Score, newCoins = run.Coins,
                    newSpinIndex = 1, quota = 100, spins = 5,
                };
                var clear = StageFlow.ClearStage(run, outcome);
                t.True(!clear.nodeOptions.Contains(NodeKind.AugLevel), $"[auglv-gate i={i}] 레벨업 가능 증강 없으면 AUGLEVEL 절대 미등장");
                t.True(clear.nodeOptions.Contains(NodeKind.Augment), $"[auglv-gate i={i}] AUGMENT 자리 그대로 유지");
            }
            t.Eq(initialChance, run.AugLevelChance, "[auglv-gate] 게이트가 막으면 pity(AugLevelChance) 자체가 갱신되지 않음(30회 클리어 후에도 그대로)");
        }
    }

    // ── ⑦ ChooseNode(AUGLEVEL) → PickOffer 흐름 — RNG 배선 없이 노드 선택/픽 로직만 직접 검증 ───────
    internal static class Tests_P3_AugLevel_ChooseAndPickFlow
    {
        public static void Run(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(7L);
            run.Phase = RunPhase.NodeSelect;
            run.Perks.Add("study");
            run.Perks.Add("greed"); // 후보 2개 — 3장 캡 없이 전부 오퍼되는지 확인(웹 game.js:1622 전량 오퍼).
            run.NodeOptions.Clear();
            run.NodeOptions.Add(NodeKind.Shop);
            run.NodeOptions.Add(NodeKind.AugLevel);
            run.NodeOptions.Add(NodeKind.Rest);

            var stat = new Dictionary<string, long>();
            var chooseEvents = NodeEvents.ChooseNode(run, 1, stat);

            t.Eq(1, chooseEvents.Count, "[auglv-flow] ChooseNode(AUGLEVEL) → 단일 PERK_OFFER 이벤트");
            t.Eq("PERK_OFFER", chooseEvents[0].type, "[auglv-flow] type == PERK_OFFER");
            t.Eq(RunPhase.EventAugLevel, run.Phase, "[auglv-flow] Phase → EventAugLevel");
            t.Eq(2, run.PerkOfferIds.Count, "[auglv-flow] 레벨업 가능 보유증강 2개 전부 오퍼(3장 이하라 캡 미적용)");
            t.True(run.PerkOfferIds.Contains("study") && run.PerkOfferIds.Contains("greed"), "[auglv-flow] study/greed 둘 다 후보");

            int idx = run.PerkOfferIds.IndexOf("study");
            var pickEvents = NodeEvents.PickOffer(run, idx);
            t.Eq(1, pickEvents.Count, "[auglv-flow] PickOffer → 단일 이벤트");
            var ev = pickEvents[0];
            t.Eq("PERK_LEVELED", ev.type, "[auglv-flow] type == PERK_LEVELED(퍽 신규획득 PERK_GRANTED 아님)");
            t.Eq("study", ev.perkId, "[auglv-flow] perkId == study");
            t.Eq(1, ev.perkLevelBefore, "[auglv-flow] before == 1");
            t.Eq(2, ev.perkLevelAfter, "[auglv-flow] after == 2");
            t.Eq(2, run.PerkLevels["study"], "[auglv-flow] RunState.PerkLevels 반영");
            t.Eq(1, run.Perks.Count(id => id == "study"), "[auglv-flow] 레벨업은 perks에 새 항목을 추가하지 않음(중복 없음)");
            t.Eq(1, run.PerkLevels.TryGetValue("greed", out var glv) ? glv : 1, "[auglv-flow] greed는 그대로 Lv1(영향 없음)");
            t.Eq(RunPhase.Spin, run.Phase, "[auglv-flow] 픽 후 Phase → Spin");
            t.Eq(0, run.PerkOfferIds.Count, "[auglv-flow] PerkOfferIds 소진");
        }
    }

    // ── ⑦b AUGLEVEL 오퍼 3장 캡 — Opus 1차검수 필수(2026-08-08, §2-(M)) ────────────────────────────
    // PerkOfferPanel이 320px 고정 카드 3장 전용이라 웹처럼 전량 오퍼하면 4장 이상일 때 화면 밖으로
    // 잘린다 — 3장 이하는 전량, 4장 이상은 run.Rng.Shuffle로 앞 3장만 선발.
    internal static class Tests_P3_AugLevel_OfferCap
    {
        public static void Run(TestCtx t)
        {
            TwoOrThreeCandidatesKeepAll(t);
            FiveCandidatesCapToThree(t);
        }

        private static List<RunEvent> OfferAugLevel(RunState run, IEnumerable<string> perkIds)
        {
            run.Phase = RunPhase.NodeSelect;
            foreach (var id in perkIds) run.Perks.Add(id);
            run.NodeOptions.Clear();
            run.NodeOptions.Add(NodeKind.AugLevel);
            return NodeEvents.ChooseNode(run, 0, new Dictionary<string, long>());
        }

        // 3장 이하 전량 유지 — 정확히 2개, 그리고 캡 경계값인 정확히 3개 둘 다 확인.
        private static void TwoOrThreeCandidatesKeepAll(TestCtx t)
        {
            var run2 = S4TestHelpers.NewRun(101L);
            OfferAugLevel(run2, new[] { "study", "greed" });
            t.Eq(2, run2.PerkOfferIds.Count, "[auglv-cap] 후보 2개(<3)면 전량 오퍼(캡 미적용)");
            t.True(run2.PerkOfferIds.Contains("study") && run2.PerkOfferIds.Contains("greed"), "[auglv-cap] 2개 후보 둘 다 오퍼");

            var run3 = S4TestHelpers.NewRun(102L);
            OfferAugLevel(run3, new[] { "study", "greed", "polymath" });
            t.Eq(3, run3.PerkOfferIds.Count, "[auglv-cap] 후보 정확히 3개(경계) → 전량 오퍼(캡 미적용)");
            t.True(new[] { "study", "greed", "polymath" }.All(id => run3.PerkOfferIds.Contains(id)),
                "[auglv-cap] 3개 후보 전부 오퍼");
        }

        // 5장 보유 시 3장 선발(고정 시드) — 선발 결과가 원래 후보 부분집합이자 전부 레벨업 가능 대상인지,
        // 그리고 같은 시드/상황이면 재현되는지(pool.Add 순서·RNG 소비 시점이 결정론적인지 확인).
        private static void FiveCandidatesCapToThree(TestCtx t)
        {
            var allFive = new[] { "study", "greed", "polymath", "cherry_up", "book_up" };

            var run = S4TestHelpers.NewRun(4242L);
            var events = OfferAugLevel(run, allFive);

            t.Eq(1, events.Count, "[auglv-cap5] ChooseNode → 단일 이벤트");
            t.Eq("PERK_OFFER", events[0].type, "[auglv-cap5] type == PERK_OFFER");
            t.Eq(3, run.PerkOfferIds.Count, "[auglv-cap5] 후보 5개 → 3장으로 캡");
            t.Eq(3, run.PerkOfferIds.Distinct().Count(), "[auglv-cap5] 선발된 3장은 서로 중복 없음");
            foreach (var id in run.PerkOfferIds)
            {
                t.True(allFive.Contains(id), $"[auglv-cap5] 선발된 {id}는 원래 5개 후보의 부분집합");
                t.True(AugLevels.IsLevelable(id), $"[auglv-cap5] 선발된 {id}는 레벨업 가능 증강(AUG_LEVELS 등록)");
            }

            // 고정 시드 재현성 — 동일 시드·동일 상황이면 동일 3장이 선발되어야 한다(RunState 생성 직후
            // 첫 RNG 소비가 이 셔플이라 다른 부수 RNG 소비 없이 결정론적으로 재현 가능).
            var runRepro = S4TestHelpers.NewRun(4242L);
            OfferAugLevel(runRepro, allFive);
            var setA = new HashSet<string>(run.PerkOfferIds);
            var setB = new HashSet<string>(runRepro.PerkOfferIds);
            t.True(setA.SetEquals(setB), "[auglv-cap5] 동일 시드로 재구성하면 동일 3장이 선발됨(재현성)");

            // 다른 시드는 (통계적으로) 다른 3장이 나올 수 있음을 최소 1개 이상 확인 — 셔플이 실제로
            // RNG를 쓰고 있는지(고정 순서로 앞 3개만 자르는 게 아닌지) 회귀 방지.
            bool anyDiffer = false;
            for (long seed = 1L; seed <= 20L; seed++)
            {
                var runOther = S4TestHelpers.NewRun(seed * 999983L);
                OfferAugLevel(runOther, allFive);
                if (!setA.SetEquals(new HashSet<string>(runOther.PerkOfferIds))) { anyDiffer = true; break; }
            }
            t.True(anyDiffer, "[auglv-cap5] 다른 시드 20개 중 최소 1개는 다른 3장 선발(셔플이 실제 RNG를 소비함)");
        }
    }

    // ── ⑧ Lv3 도달 후 재오퍼 방어 — 후보가 0개면 무보상 NODE_RESOLVED(EVENT 테이블 폴백 아님) ───────
    internal static class Tests_P3_AugLevel_NoCandidatesDefensiveFallback
    {
        public static void Run(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(8L);
            run.Perks.Add("study");
            run.PerkLevels["study"] = 3; // 이미 Lv3(레벨업 불가)
            run.Phase = RunPhase.NodeSelect;
            run.NodeOptions.Clear();
            run.NodeOptions.Add(NodeKind.AugLevel);

            var stat = new Dictionary<string, long>();
            var events = NodeEvents.ChooseNode(run, 0, stat);

            t.Eq(1, events.Count, "[auglv-nocand] 단일 이벤트");
            t.Eq("NODE_RESOLVED", events[0].type, "[auglv-nocand] EVENT 테이블 폴백이 아니라 무보상 NODE_RESOLVED");
            t.True(events[0].node == NodeKind.AugLevel, "[auglv-nocand] node 필드는 AugLevel 유지");
            t.Eq(RunPhase.Spin, run.Phase, "[auglv-nocand] Phase → Spin으로 즉시 복귀");
            t.Eq(0, run.PerkOfferIds.Count, "[auglv-nocand] PerkOfferIds 미생성");
        }
    }
}
