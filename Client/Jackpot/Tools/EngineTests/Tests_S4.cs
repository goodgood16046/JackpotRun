using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // S4 골든 테스트 — Run/Shop.cs·NodeEvents.cs·ItemUse.cs·DeviceActions.cs·RunController.cs 검증.
    // 작업 지시 항목: ①상점 오퍼 생성(고정시드·gatedPool 폴백) ②구매/리롤 코인흐름 ③MANIP net-adjust
    // 대표 2종 ④INSTANT_CLEAR 캡 ⑤노드 8종 각 1회 ⑥RunController 풀런 자동플레이 2시드 ⑦100시드 시뮬레이션.

    internal static class S4TestHelpers
    {
        // 해금 게이트를 대부분 통과시키는 "관대한" 스탯 뷰 — PlayerProfile(S5)이 없는 이 슬라이스에서
        // 호출측이 직접 구성해 RunController에 넘기는 "해금 스탯 뷰" 예시. 정확한 값은 중요치 않고,
        // 졸업레벨(Formulas.AccountLevel)을 충분히 끌어올려(대부분의 school 게이트 minLevel<=17을 통과)
        // 상점/노드 오퍼가 매번 BASE_PERK_IDS로만 쪼그라들지 않게 한다.
        public static Dictionary<string, long> GenerousStat()
        {
            var d = new Dictionary<string, long>
            {
                ["bestStage"] = 20, ["bossClears"] = 30, ["runs"] = 50,
                ["cherryTotal"] = 5000, ["bookTotal"] = 5000, ["coinTotal"] = 5000,
                ["prayClears"] = 50, ["gambles"] = 50, ["crownTotal"] = 5000, ["jackpots"] = 50,
                ["skullTotal"] = 5000, ["curseMax"] = 20, ["lastSpinClears"] = 50, ["closeClears"] = 50,
                ["prismPicks"] = 50, ["exactClears"] = 50, ["shopBuys"] = 100, ["allinWins"] = 50,
                ["mstage_garden"] = 20, ["seedTotal"] = 100, ["wildTotal"] = 100, ["set4Plus"] = 100,
                ["curseBossClears"] = 20,
            };
            // 졸업레벨을 높이 끌어올리기 위한 합성 숙련메달 키(cstage_*/mstage_*) — Formulas.AccountExp ⑥.
            for (int i = 0; i < 40; i++)
            {
                d["cstage_synthetic" + i] = 20;
                d["mstage_synthetic" + i] = 20;
            }
            return d;
        }

        public static RunState NewRun(long seed, string charId = "novice", string machineId = "basic", string deviceId = "")
        {
            return new RunState(seed) { CharId = charId, MachineId = machineId, Device = deviceId, Phase = RunPhase.Spin };
        }
    }

    // ── ① 상점 오퍼 생성 — 고정 시드 재현성 + gatedPool BASE 폴백 ──────────────────────────────────
    internal static class Tests_S4_ShopOffer
    {
        public static void Run(TestCtx t)
        {
            FixedSeedReproducibility(t);
            EntryShapeAndPricing(t);
            GatedPoolBaseFallback(t);
        }

        private static void FixedSeedReproducibility(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var runA = S4TestHelpers.NewRun(4242L);
            var runB = S4TestHelpers.NewRun(4242L);
            var offerA = Shop.FreshOffer(runA, stat);
            var offerB = Shop.FreshOffer(runB, stat);

            t.Eq(offerA.Count, offerB.Count, "[shop-repro] 동일 시드 → 오퍼 칸수 동일");
            for (int i = 0; i < offerA.Count; i++)
            {
                t.Eq(offerA[i].kind, offerB[i].kind, $"[shop-repro] 칸{i} kind 동일");
                t.Eq(offerA[i].id, offerB[i].id, $"[shop-repro] 칸{i} id 동일");
                t.Eq(offerA[i].price, offerB[i].price, $"[shop-repro] 칸{i} price 동일");
            }

            var runC = S4TestHelpers.NewRun(9999L);
            var offerC = Shop.FreshOffer(runC, stat);
            bool differs = offerA.Count != offerC.Count ||
                           offerA.Where((e, i) => i < offerC.Count && (e.id != offerC[i].id || e.kind != offerC[i].kind)).Any();
            t.True(differs, "[shop-repro] 다른 시드 → 오퍼 내용이 (최소 한 군데는) 달라짐");
        }

        private static void EntryShapeAndPricing(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var run = S4TestHelpers.NewRun(111L);
            var offer = Shop.FreshOffer(run, stat);

            t.True(offer.Count >= 1 && offer.Count <= 6, "[shop-shape] 오퍼는 최대 6칸(증강2+유물2+아이템2)");
            int augCount = offer.Count(e => e.kind == 'A');
            int relicCount = offer.Count(e => e.kind == 'R');
            int itemCount = offer.Count(e => e.kind == 'I');
            t.True(augCount <= 2, "[shop-shape] 증강 칸 최대 2");
            t.True(relicCount <= 2, "[shop-shape] 유물 칸 최대 2");
            t.True(itemCount <= 2, "[shop-shape] 아이템 칸 최대 2");

            foreach (var e in offer)
            {
                switch (e.kind)
                {
                    case 'A':
                    {
                        var p = Perks.ById(e.id);
                        t.True(p != null && p.cat == PCat.AUGMENT, $"[shop-shape] A:{e.id} 는 실제 증강 id");
                        int expected = p.tier == Tier.SILVER ? 14 : p.tier == Tier.GOLD ? 24 : 36;
                        t.Eq(expected, e.price, $"[shop-shape] 증강 {e.id} 가격=티어고정({p.tier})");
                        break;
                    }
                    case 'R':
                    {
                        var p = Perks.ById(e.id);
                        t.True(p != null && p.cat == PCat.RELIC, $"[shop-shape] R:{e.id} 는 실제 유물 id");
                        t.Eq(p.price, e.price, $"[shop-shape] 유물 {e.id} 가격=Perk.price");
                        break;
                    }
                    default:
                    {
                        var it = Items.ById(e.id);
                        t.True(it != null, $"[shop-shape] I:{e.id} 는 실제 아이템 id");
                        t.Eq(it.coinCost, e.price, $"[shop-shape] 아이템 {e.id} 가격=coinCost");
                        break;
                    }
                }
            }
        }

        // 전부 잠긴(사실상 BASE만 통과) 스탯 뷰 → gatedPool이 BasePerkIds로 폴백해야 한다(데드엔드 방지).
        private static void GatedPoolBaseFallback(TestCtx t)
        {
            var lockedStat = new Dictionary<string, long> { ["dummy_unrelated_key"] = 1 }; // 비어있지 않지만 아무 게이트도 못 채움
            var gatedAug = Shop.GatedPool(Perks.Augments, lockedStat);
            var gatedRelic = Shop.GatedPool(Perks.Relics, lockedStat);

            t.True(gatedAug.Count > 0, "[gated-fallback] 증강 폴백 결과 비어있지 않음");
            t.True(gatedAug.All(p => Schools.BasePerkIds.Contains(p.id)), "[gated-fallback] 증강 폴백 = BasePerkIds만");
            t.True(gatedRelic.Count > 0, "[gated-fallback] 유물 폴백 결과 비어있지 않음");
            t.True(gatedRelic.All(p => Schools.BasePerkIds.Contains(p.id)), "[gated-fallback] 유물 폴백 = BasePerkIds만");

            // pickAugments/pickRelics도 폴백 풀 안에서만 뽑아야 한다.
            var rng = new Rng(555L);
            var picks = Shop.PickAugments(rng, 5, new HashSet<string>(), 4, lockedStat);
            t.True(picks.Count > 0, "[gated-fallback] pickAugments도 폴백 풀에서 결과를 낸다");
            t.True(picks.All(p => Schools.BasePerkIds.Contains(p.id)), "[gated-fallback] pickAugments 결과 전부 BasePerkIds");

            // 완전 빈 맵(null과 동치)은 무필터 — Kotlin stat.isEmpty() 분기.
            var unfiltered = Shop.GatedPool(Perks.Augments, new Dictionary<string, long>());
            t.Eq(Perks.Augments.Length, unfiltered.Count, "[gated-fallback] 빈 스탯맵 = 무필터(원본 그대로)");
        }
    }

    // ── ② 구매/리롤 코인 흐름 ──────────────────────────────────────────────────────────────────
    internal static class Tests_S4_ShopEconomy
    {
        public static void Run(TestCtx t)
        {
            BuyFlow(t);
            BagFull(t);
            RerollFlow(t);
            LeaveAndPhaseGuards(t);
        }

        private static RunState ShopRun(long coins)
        {
            var run = S4TestHelpers.NewRun(1L);
            run.Phase = RunPhase.EventShop;
            run.Coins = coins;
            run.ShopOffer.Add(new ShopEntry { kind = 'A', id = "study", price = 14 });
            run.ShopOffer.Add(new ShopEntry { kind = 'I', id = "old_coin", price = 4 });
            return run;
        }

        private static void BuyFlow(TestCtx t)
        {
            var run = ShopRun(20);
            var ev = Shop.Buy(run, 0);
            t.Eq("SHOP_PURCHASED", ev[0].type, "[buy] 성공 시 SHOP_PURCHASED");
            t.Eq(6, run.Coins, "[buy] 코인 -14");
            t.True(run.Perks.Contains("study"), "[buy] 증강 즉시 영구 추가");
            t.Eq(1, run.ShopOffer.Count, "[buy] 산 항목만 목록에서 제거(상점 유지)");
            t.True(run.UsedCmds.Contains("RUNSHOP"), "[buy] RUNSHOP 마커 기록(런 끝까지)");

            // 코인 부족.
            var run2 = ShopRun(5);
            var ev2 = Shop.Buy(run2, 0); // study=14, 보유5
            t.Eq("REJECTED", ev2[0].type, "[buy] 코인 부족 → 거부");
            t.Eq("INSUFFICIENT_COINS", ev2[0].reason, "[buy] 거부 사유");
            t.Eq(5, run2.Coins, "[buy] 거부 시 코인 불변");
            t.Eq(2, run2.ShopOffer.Count, "[buy] 거부 시 오퍼 불변");

            // 인덱스 범위 밖.
            var run3 = ShopRun(100);
            var ev3 = Shop.Buy(run3, 99);
            t.Eq("REJECTED", ev3[0].type, "[buy] 범위 밖 인덱스 거부");
            t.Eq("INVALID_INDEX", ev3[0].reason, "[buy] 범위 밖 사유");
        }

        private static void BagFull(TestCtx t)
        {
            var run = ShopRun(100);
            run.Items.Add("old_coin"); run.Items.Add("old_coin"); run.Items.Add("old_coin"); // ItemSlots=3 가득
            var ev = Shop.Buy(run, 1); // 인덱스1 = 아이템 old_coin
            t.Eq("REJECTED", ev[0].type, "[bagfull] 가방 가득 → 아이템 구매 거부");
            t.Eq("BAG_FULL", ev[0].reason, "[bagfull] 거부 사유");
            t.Eq(100, run.Coins, "[bagfull] 코인 차감 없음");

            // 증강/유물은 가방과 무관 — 가득 차 있어도 구매 가능.
            var ev2 = Shop.Buy(run, 0); // 인덱스0 = 증강 study
            t.Eq("SHOP_PURCHASED", ev2[0].type, "[bagfull] 증강 구매는 가방 상태와 무관하게 성공");
        }

        private static void RerollFlow(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var run = ShopRun(20);
            var before = new List<string>(run.ShopOffer.Select(e => e.kind + ":" + e.id));
            var ev = Shop.Reroll(run, stat);
            t.Eq("SHOP_REROLLED", ev[0].type, "[reroll] 성공 시 SHOP_REROLLED");
            t.Eq(14, run.Coins, "[reroll] 코인 -6(정액)");
            t.True(run.ShopOffer.Count >= 1, "[reroll] 새 오퍼 생성됨");

            var run2 = ShopRun(3); // 6코인 미만
            var ev2 = Shop.Reroll(run2, stat);
            t.Eq("REJECTED", ev2[0].type, "[reroll] 코인 부족 거부");
            t.Eq("INSUFFICIENT_COINS", ev2[0].reason, "[reroll] 거부 사유");
            t.Eq(3, run2.Coins, "[reroll] 거부 시 코인 불변");
        }

        private static void LeaveAndPhaseGuards(TestCtx t)
        {
            var run = ShopRun(10);
            var ev = Shop.Leave(run);
            t.Eq("SHOP_LEFT", ev[0].type, "[leave] SHOP_LEFT");
            t.Eq(RunPhase.Spin, run.Phase, "[leave] Phase → Spin");
            t.Eq(0, run.ShopOffer.Count, "[leave] 오퍼 정리");

            // 상점이 아닌 상태에서 상점 액션 → 전부 거부.
            var spinRun = S4TestHelpers.NewRun(1L);
            t.Eq("REJECTED", Shop.Buy(spinRun, 0)[0].type, "[guard] SPIN 상태에서 Buy 거부");
            t.Eq("REJECTED", Shop.Reroll(spinRun, S4TestHelpers.GenerousStat())[0].type, "[guard] SPIN 상태에서 Reroll 거부");
            t.Eq("REJECTED", Shop.Leave(spinRun)[0].type, "[guard] SPIN 상태에서 Leave 거부");
        }
    }

    // ── ③ MANIP net-adjust 대표 2종(dev_reroll·dev_swap) — 직전 스핀 1개 교체의 대수적 일관성 검증 ──────
    internal static class Tests_S4_DeviceManip
    {
        public static void Run(TestCtx t)
        {
            NetAdjust(t, "dev_reroll", null, 3);
            NetAdjust(t, "dev_swap", 1, 5);
            Guards(t);
        }

        private static void NetAdjust(TestCtx t, string deviceId, int? arg, int expectedCost)
        {
            var run = S4TestHelpers.NewRun(2024L);
            run.Device = deviceId;
            run.Stage = 1; // 보스 아님 — applyBoss 개입 없음(×0.9만 검증하면 됨)
            run.SpinIndex = 1;
            run.LastSpinNo = 0;
            run.LastCells.AddRange(new[] { "cherry", "cherry", "book", "gem", "crown" });
            long oldStageExp = 20, oldScore = 5, oldCoins = 30;
            long oldLastGain = 20, oldLastScoreGain = 5;
            int oldLastCoinGain = 1;
            run.StageExp = oldStageExp; run.Score = oldScore; run.Coins = oldCoins;
            run.LastGain = oldLastGain; run.LastScoreGain = oldLastScoreGain; run.LastCoinGain = oldLastCoinGain;

            var events = DeviceActions.Handle(run, deviceId, arg);
            t.Eq(1, events.Count, $"[manip:{deviceId}] 이벤트 1개");
            var ev = events[0];
            t.True(ev.type == "DEVICE_MANIP_RESULT" || ev.type == "STAGE_CLEARED" || ev.type == "GAME_OVER",
                $"[manip:{deviceId}] 결과는 MANIP/클리어/게임오버 중 하나 (실제: {ev.type})");
            t.True(ev.spin != null, $"[manip:{deviceId}] SpinOutcome 페이로드 존재");
            var oc = ev.spin;

            t.Eq((long)(oc.result.exp * 0.9), oc.gained, $"[manip:{deviceId}] EXP ×0.9 페널티");
            long expNewExp = Math.Max(oldStageExp - oldLastGain + oc.gained, 0);
            long expNewScore = Math.Max(oldScore - oldLastScoreGain + oc.result.score, 0);
            long expNewCoins = Math.Max(oldCoins - oldLastCoinGain + oc.result.coins - expectedCost, 0);
            t.Eq(expNewExp, oc.newExp, $"[manip:{deviceId}] net-adjust newExp");
            t.Eq(expNewScore, oc.newScore, $"[manip:{deviceId}] net-adjust newScore");
            t.Eq(expNewCoins, oc.newCoins, $"[manip:{deviceId}] net-adjust newCoins(비용 {expectedCost} 포함)");
            t.True(run.UsedCmds.Contains(deviceId), $"[manip:{deviceId}] 스테이지당 1회 마커 기록(dev.id)");
            t.True(run.RunRerolled, $"[manip:{deviceId}] RunRerolled=true(런 끝까지)");

            // 스테이지당 1회 — 같은 스테이지에서 재사용 시도는 DeviceActions.Handle 상위 게이트에서 거부.
            if (ev.type == "DEVICE_MANIP_RESULT")
            {
                run.LastCells.Clear();
                run.LastCells.AddRange(new[] { "cherry", "cherry", "book", "gem", "crown" });
                run.LastSpinNo = run.SpinIndex; // 방어적 재설정
                var second = DeviceActions.Handle(run, deviceId, arg);
                t.Eq("REJECTED", second[0].type, $"[manip:{deviceId}] 같은 스테이지 재사용 거부");
                t.Eq("DEVICE_ALREADY_USED", second[0].reason, $"[manip:{deviceId}] 거부 사유");
            }
        }

        private static void Guards(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(1L);
            run.Device = "dev_reroll";
            // 직전 스핀 없음.
            var ev = DeviceActions.Handle(run, "dev_reroll", null);
            t.Eq("REJECTED", ev[0].type, "[manip-guard] 직전 스핀 없으면 거부");
            t.Eq("NO_LAST_SPIN", ev[0].reason, "[manip-guard] 사유");

            // 장착하지 않은 장치.
            run.LastCells.AddRange(new[] { "cherry", "book", "star", "gem", "crown" });
            run.LastSpinNo = 0;
            var ev2 = DeviceActions.Handle(run, "dev_pin", 1);
            t.Eq("REJECTED", ev2[0].type, "[manip-guard] 미장착 장치 거부");
            t.Eq("DEVICE_NOT_EQUIPPED", ev2[0].reason, "[manip-guard] 사유");

            // dev_pin/copy/swap은 인자 필요.
            run.Device = "dev_pin";
            var ev3 = DeviceActions.Handle(run, "dev_pin", null);
            t.Eq("REJECTED", ev3[0].type, "[manip-guard] dev_pin 인자 없으면 거부");
            t.Eq("ARG_REQUIRED", ev3[0].reason, "[manip-guard] 사유");
            t.True(DeviceActions.DeviceNeedsArg("dev_pin"), "[manip-guard] Device.needsArg 복원: dev_pin=true");
            t.True(DeviceActions.DeviceNeedsArg("dev_copy"), "[manip-guard] Device.needsArg 복원: dev_copy=true");
            t.True(DeviceActions.DeviceNeedsArg("dev_swap"), "[manip-guard] Device.needsArg 복원: dev_swap=true");
            t.True(DeviceActions.DeviceNeedsArg("dev_holdfile"), "[manip-guard] Device.needsArg 복원: dev_holdfile=true");
            t.True(!DeviceActions.DeviceNeedsArg("dev_reroll"), "[manip-guard] dev_reroll은 인자 불필요");
        }
    }

    // ── ④ INSTANT_CLEAR_ITEMS 6종 캡 — 스테이지당 1회("ICLEAR" 마커) ──────────────────────────────
    internal static class Tests_S4_InstantClearCap
    {
        public static void Run(TestCtx t)
        {
            var expected = new[] { "answer_sheet", "grad_cert", "grad_copy", "honor_roll", "grad_ring", "gold_grad_bell" };
            foreach (var id in expected)
                t.True(ItemUse.IsInstantClearItem(id), $"[iclear-set] {id} 는 INSTANT_CLEAR_ITEMS 멤버");
            foreach (var id in new[] { "cram", "cheat_sheet", "old_coin", "retake_form", "insurance_cert" })
                t.True(!ItemUse.IsInstantClearItem(id), $"[iclear-set] {id} 는 캡 대상 아님");
            t.Eq(6, expected.Length, "[iclear-set] 총 6종");

            var stat = S4TestHelpers.GenerousStat();
            var run = S4TestHelpers.NewRun(1L);
            run.Items.Add("answer_sheet"); run.Items.Add("answer_sheet"); run.Items.Add("answer_sheet");

            var ev1 = ItemUse.Use(run, "answer_sheet", stat);
            t.Eq("ITEM_USED", ev1[0].type, "[iclear-cap] 1회차 사용 성공");
            t.Eq(2, run.Items.Count, "[iclear-cap] 가방에서 1개 소모");
            t.True(run.UsedCmds.Contains("ICLEAR"), "[iclear-cap] ICLEAR 마커 기록");
            // instantQuota(run)은 buildMods(quotaMul 등, novice=×0.92 포함)를 반영한 실제 쿼터다 —
            // Formulas.Quota(1) 원값이 아니라 mods 적용 후 값을 손으로 재계산해야 한다(순환검증 방지를
            // 위해 buildMods 호출 대신 novice의 quotaMul 계수를 직접 곱했다).
            long noviceQuota = (long)(Formulas.Quota(1) * 0.92);
            long quotaHalf = noviceQuota * 50 / 100;
            t.Eq(quotaHalf, run.StageExp, "[iclear-cap] answer_sheet = 요구치(novice quotaMul 반영) 50% 채움");

            var ev2 = ItemUse.Use(run, "answer_sheet", stat);
            t.Eq("REJECTED", ev2[0].type, "[iclear-cap] 2회차(같은 스테이지) 거부");
            t.Eq("ICLEAR_ALREADY_USED", ev2[0].reason, "[iclear-cap] 거부 사유");
            t.Eq(2, run.Items.Count, "[iclear-cap] 거부 시 가방 불변(소모 안 됨)");
            t.Eq(quotaHalf, run.StageExp, "[iclear-cap] 거부 시 게이지 불변");

            // 클리어 시 usedCmds 리셋(StageFlow.ClearStage 계약, S3 기이식) — 마커 제거를 흉내내 재사용 가능 확인.
            run.UsedCmds.Remove("ICLEAR");
            var ev3 = ItemUse.Use(run, "answer_sheet", stat);
            t.Eq("ITEM_USED", ev3[0].type, "[iclear-cap] 마커 리셋 후(=다음 스테이지) 재사용 성공");
            t.Eq(1, run.Items.Count, "[iclear-cap] 3번째 사본 소모");
        }
    }

    // ── ⑤ 노드 8종 각 1회 진행(AUGMENT/RELIC/SHOP/REST/GAMBLE/EVENT/CURSE/RISK) ────────────────────
    internal static class Tests_S4_Nodes
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            long seed = 3000;

            AugmentOrRelic(t, stat, seed++, NodeKind.Augment);
            AugmentOrRelic(t, stat, seed++, NodeKind.Relic);
            ShopNode(t, stat, seed++);
            RestNode(t, stat, seed++);
            GambleNode(t, stat, seed++);
            EventNode(t, stat, seed++);
            CurseNode(t, stat, seed++);
            RiskNode(t, stat, seed++);
        }

        private static RunState NodeRun(long seed, NodeKind kind, int stage = 7)
        {
            var run = S4TestHelpers.NewRun(seed);
            run.Phase = RunPhase.NodeSelect;
            run.Stage = stage; // clearedStage=stage-1; stage=7 → clearedStage=6(보스 아님, 5의배수 아님) — 일반 진행 확인용
            run.NodeOptions.Add(kind);
            return run;
        }

        private static void AugmentOrRelic(TestCtx t, IReadOnlyDictionary<string, long> stat, long seed, NodeKind kind)
        {
            var run = NodeRun(seed, kind);
            var ev = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq(1, ev.Count, $"[node:{kind}] 이벤트 1개");
            // 풀 소진은 사실상 불가능(신규 런, 보유 0) — PERK_OFFER 확정 기대.
            t.Eq("PERK_OFFER", ev[0].type, $"[node:{kind}] 오퍼 생성");
            t.Eq(kind, ev[0].node, $"[node:{kind}] 이벤트 node 필드");
            t.True(ev[0].perkOfferIds.Count >= 1 && ev[0].perkOfferIds.Count <= 3, $"[node:{kind}] 후보 1~3개");
            var expectedPhase = kind == NodeKind.Augment ? RunPhase.EventAugment : RunPhase.EventRelic;
            t.Eq(expectedPhase, run.Phase, $"[node:{kind}] Phase 전환");
            t.Eq(0, run.NodeOptions.Count, $"[node:{kind}] NodeOptions 소거");

            // 후보 선택까지 이어서 확인(PickOffer).
            var pick = NodeEvents.PickOffer(run, 0);
            t.Eq("PERK_GRANTED", pick[0].type, $"[node:{kind}] PickOffer 성공");
            t.True(run.Perks.Contains(pick[0].perkId), $"[node:{kind}] 선택한 퍽이 영구 보유로 추가됨");
            t.Eq(RunPhase.Spin, run.Phase, $"[node:{kind}] 선택 후 Phase → Spin");
        }

        private static void ShopNode(TestCtx t, IReadOnlyDictionary<string, long> stat, long seed)
        {
            var run = NodeRun(seed, NodeKind.Shop);
            var ev = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("SHOP_OFFER", ev[0].type, "[node:Shop] 오퍼 생성");
            t.Eq(RunPhase.EventShop, run.Phase, "[node:Shop] Phase → EventShop");
            t.True(run.ShopOffer.Count >= 1, "[node:Shop] 상점 칸 생성됨");
        }

        private static void RestNode(TestCtx t, IReadOnlyDictionary<string, long> stat, long seed)
        {
            var run = NodeRun(seed, NodeKind.Rest);
            var ev = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("NODE_RESOLVED", ev[0].type, "[node:Rest] 즉시 해결");
            t.Eq(8, ev[0].coinsDelta, "[node:Rest] 코인 +8");
            t.Eq(8, run.Coins, "[node:Rest] 실제 코인 반영");
            t.Eq(RunPhase.Spin, run.Phase, "[node:Rest] Phase → Spin");
        }

        private static void GambleNode(TestCtx t, IReadOnlyDictionary<string, long> stat, long seed)
        {
            var run = NodeRun(seed, NodeKind.Gamble);
            run.Coins = 10; // 0이면 항상 불발이라 실제 50/50 분기를 타도록 코인 지급
            var ev = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("NODE_RESOLVED", ev[0].type, "[node:Gamble] 즉시 해결");
            bool doubled = run.Coins == 20;
            bool lost = run.Coins == 0;
            t.True(doubled || lost, "[node:Gamble] 결과는 2배 또는 전액소멸 중 하나");
            t.Eq(RunPhase.Spin, run.Phase, "[node:Gamble] Phase → Spin");

            // 코인 0이면 항상 불발.
            var run2 = NodeRun(seed + 500, NodeKind.Gamble);
            run2.Coins = 0;
            var ev2 = NodeEvents.ChooseNode(run2, 0, stat);
            t.Eq(0, ev2[0].coinsDelta, "[node:Gamble] 코인 0 → 불발(변화 없음)");
            t.Eq(0, run2.Coins, "[node:Gamble] 코인 0 유지");
        }

        private static void EventNode(TestCtx t, IReadOnlyDictionary<string, long> stat, long seed)
        {
            var run = NodeRun(seed, NodeKind.Event);
            var ev = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("NODE_RESOLVED", ev[0].type, "[node:Event] 즉시 해결");
            t.Eq(NodeKind.Event, ev[0].node, "[node:Event] node 필드");
            t.True(ev[0].eventRoll >= 0 && ev[0].eventRoll < 10, "[node:Event] eventRoll 0~9 범위");
            t.Eq(RunPhase.Spin, run.Phase, "[node:Event] Phase → Spin");
        }

        private static void CurseNode(TestCtx t, IReadOnlyDictionary<string, long> stat, long seed)
        {
            var run = NodeRun(seed, NodeKind.Curse);
            var ev = NodeEvents.ChooseNode(run, 0, stat);
            // 신규 런(보유 저주 0) → 16종 중 1개는 항상 뽑힘, EVENT 폴백 불필요.
            t.Eq("NODE_RESOLVED", ev[0].type, "[node:Curse] 즉시 해결");
            t.Eq(NodeKind.Curse, ev[0].node, "[node:Curse] node 필드");
            t.True(!string.IsNullOrEmpty(ev[0].curseGrantedId), "[node:Curse] 저주 지급됨");
            t.Eq(1, run.Curses.Count, "[node:Curse] 저주 보유 목록에 추가");
            t.Eq(15, run.Coins, "[node:Curse] 코인 +15");
            t.Eq(RunPhase.Spin, run.Phase, "[node:Curse] Phase → Spin");
        }

        private static void RiskNode(TestCtx t, IReadOnlyDictionary<string, long> stat, long seed)
        {
            var run = NodeRun(seed, NodeKind.Risk);
            var ev = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("NODE_RESOLVED", ev[0].type, "[node:Risk] 즉시 해결(프리즘/골드 증강 + 저주 동시 지급)");
            t.Eq(NodeKind.Risk, ev[0].node, "[node:Risk] node 필드");
            t.True(!string.IsNullOrEmpty(ev[0].augmentGrantedId), "[node:Risk] 증강 지급됨");
            t.True(!string.IsNullOrEmpty(ev[0].curseGrantedId), "[node:Risk] 저주 지급됨");
            t.Eq(1, run.Perks.Count, "[node:Risk] 증강 1개 보유");
            t.Eq(1, run.Curses.Count, "[node:Risk] 저주 1개 보유");
            t.Eq(RunPhase.Spin, run.Phase, "[node:Risk] Phase → Spin");
        }
    }

    // ── 🧩 세트 시너지 5% off-tier 주입(NodeEvents.OfferPerks·Shop.SetSynergyAug) — Fable 후속 지시
    // (2026-07-31): 고정 시드 대량 샘플로 발생률이 3%~7% 대역인지, 주입된 퍽이 보유 퍽과 세트를
    // 이루는지 검증한다. 두 시나리오 모두 "held 퍽이 정확히 하나의 미완성 세트에만 속하고, 그 세트의
    // 빠진 조각이 정확히 1개뿐"이 되도록 구성해 injected id를 결정론적으로 예측할 수 있게 했다(주입이
    // 발생하면 항상 그 id여야 함 — 우연히 통과하는 게 아님을 보장).
    internal static class Tests_S4_SetSynergyInjection
    {
        public static void Run(TestCtx t)
        {
            // AUGMENT 노드 — held=["cherry_up"] → set_orchard(cherry_up,cherry_farm) 진행 중.
            // cherry_farm(GOLD·AUGMENT)이 유일한 후보. clearedStage=1(2번째 스테이지 클리어 아님, 3·5의
            // 배수 아님) → baseTier=SILVER가 보통이라 GOLD인 cherry_farm이 "원래" 3택에 우연히 섞여
            // 치환이 무산되는 충돌 확률을 최소화했다.
            SampleAndAssert(t, "AUGMENT", NodeKind.Augment, "cherry_up", "cherry_farm");

            // RELIC 노드 — held=["set_sense"] → set_combo(set_sense,set_charm) 진행 중.
            // set_charm(GOLD·RELIC)이 유일한 후보.
            SampleAndAssert(t, "RELIC", NodeKind.Relic, "set_sense", "set_charm");
        }

        private static void SampleAndAssert(TestCtx t, string label, NodeKind node, string heldPerkId, string expectedSynId)
        {
            var stat = S4TestHelpers.GenerousStat();
            const int trials = 4000;
            int injected = 0;
            long seedBase = node == NodeKind.Augment ? 500_000L : 700_000L;

            for (long seed = seedBase; seed < seedBase + trials; seed++)
            {
                var run = S4TestHelpers.NewRun(seed);
                run.Phase = RunPhase.NodeSelect;
                run.Stage = 2; // clearedStage=1 → baseTier=SILVER(대부분) — GOLD 후보의 자연 혼입 최소화
                run.NodeOptions.Add(node);
                run.Perks.Add(heldPerkId);

                var ev = NodeEvents.ChooseNode(run, 0, stat);
                t.Eq("PERK_OFFER", ev[0].type, $"[synergy:{label} seed={seed}] 오퍼 생성 성공");

                if (ev[0].offerSynergyPerkId != null)
                {
                    injected++;
                    t.Eq(expectedSynId, ev[0].offerSynergyPerkId,
                        $"[synergy:{label} seed={seed}] 주입된 퍽은 held 퍽이 짓는 세트의 빠진 조각이어야 함");
                    t.True(run.PerkOfferIds.Contains(expectedSynId),
                        $"[synergy:{label} seed={seed}] 실제 오퍼 목록에도 {expectedSynId} 포함");
                }
            }

            double rate = injected / (double)trials;
            t.Report($"[synergy:{label}] 5% 주입 발생률", $"{injected}/{trials} = {rate:P2}");
            t.True(rate >= 0.03 && rate <= 0.07, $"[synergy:{label}] 발생률이 3%~7% 대역 안(실측 {rate:P2})");
        }
    }

    // ── ⑥·⑦ RunController 자동 플레이 — 2시드 sanity + 100시드 시뮬레이션(상점 포함) ────────────────
    internal static class Tests_S4_RunControllerAutoplay
    {
        private static readonly HashSet<string> KnownEventTypes = new HashSet<string>
        {
            "REJECTED", "SPIN_RESULT", "STAGE_CLEARED", "REVIVED", "POST_SPIN", "GAME_OVER",
            "DEVICE_MANIP_RESULT", "NODE_RESOLVED", "PERK_OFFER", "PERK_GRANTED", "PERK_HELD",
            "RETAKE_EMPTY", "SHOP_OFFER", "SHOP_PURCHASED", "SHOP_REROLLED", "SHOP_LEFT",
            "ITEM_USED", "DEVICE_ARMED", "DEVICE_PEEK", "RUN_STARTED",
        };

        // 결정론적 자동 플레이 정책: Spin(N) 반복 → NodeSelect는 항상 0번 선택 → 증강/유물 오퍼도 항상
        // 0번 선택 → 상점은 [0번 구매 시도 1회 → 나가기]를 반복 → PostSpin은 즉시 포기(Continue).
        private static (bool reachedGameOver, int stage, List<string> log) AutoPlay(RunController rc, int guardMax)
        {
            var log = new List<string>();
            int guard = 0;
            int shopStep = 0;
            while (rc.State.Phase != RunPhase.GameOver && guard < guardMax)
            {
                guard++;
                var phase = rc.State.Phase;
                bool expectSuccess = true;
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
                        expectSuccess = false; // 코인부족/가방가득 등 정상 거부 가능
                        if (shopStep == 0) { events = rc.Do(new BuyOffer(0)); shopStep = 1; }
                        else { events = rc.Do(new LeaveShop()); shopStep = 0; }
                        break;
                    default:
                        throw new InvalidOperationException("AutoPlay: 처리 불가 Phase=" + phase);
                }
                foreach (var e in events)
                {
                    if (!KnownEventTypes.Contains(e.type))
                        throw new InvalidOperationException("AutoPlay: 알 수 없는 RunEvent.type=" + e.type);
                    log.Add(e.type);
                    if (expectSuccess && e.type == "REJECTED")
                        throw new InvalidOperationException($"AutoPlay: phase={phase}에서 예상치 못한 거부(reason={e.reason})");
                }
            }
            return (rc.State.Phase == RunPhase.GameOver, rc.State.Stage, log);
        }

        public static void Run(TestCtx t)
        {
            TwoSeedSanity(t);
            HundredSeedSimulation(t);
        }

        private static void TwoSeedSanity(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            RunOne(t, "honor", "basic", "", 111L);
            RunOne(t, "gambler", "basic", "dev_reroll", 222L);

            void RunOne(TestCtx tt, string charId, string machineId, string deviceId, long seed)
            {
                try
                {
                    var rc = new RunController(charId, machineId, deviceId, seed, stat);
                    foreach (var e in rc.LaunchEvents)
                        tt.True(KnownEventTypes.Contains(e.type), $"[autoplay2 seed={seed}] LaunchEvents 타입 알려짐: {e.type}");

                    var (ok, stage, log) = AutoPlay(rc, 50_000);
                    tt.True(ok, $"[autoplay2 seed={seed}] guard 내 게임오버 도달");
                    tt.True(log.Count > 0, $"[autoplay2 seed={seed}] 이벤트 시퀀스 비어있지 않음");
                    tt.True(log.Contains("GAME_OVER"), $"[autoplay2 seed={seed}] 시퀀스 마지막에 GAME_OVER 포함");
                    tt.True(stage >= 1, $"[autoplay2 seed={seed}] 최소 스테이지1 이상 도달");
                }
                catch (Exception ex)
                {
                    tt.Fail($"[autoplay2 seed={seed}]", "예외 발생: " + ex);
                }
            }
        }

        private static void HundredSeedSimulation(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            int exceptions = 0;
            var stagesReached = new List<int>();

            for (long seed = 1; seed <= 100; seed++)
            {
                try
                {
                    var rc = new RunController("novice", "basic", "", seed, stat);
                    var (ok, stage, _) = AutoPlay(rc, 50_000);
                    t.True(ok, $"[sim100 seed={seed}] guard(50000) 내 게임오버 도달");
                    stagesReached.Add(stage);
                }
                catch (Exception ex)
                {
                    exceptions++;
                    t.Fail($"[sim100 seed={seed}]", "예외 발생: " + ex);
                }
            }

            t.Eq(0, exceptions, "[sim100] RunController 풀런(상점 포함) 시드 100개 — 예외 0건");

            if (stagesReached.Count > 0)
            {
                stagesReached.Sort();
                int min = stagesReached[0];
                int max = stagesReached[stagesReached.Count - 1];
                double avg = stagesReached.Average();
                t.Report("[sim100] 도달 스테이지 분포", $"n={stagesReached.Count} min=S{min} max=S{max} avg=S{avg:F2}");

                var buckets = new SortedDictionary<int, int>();
                foreach (var s in stagesReached)
                {
                    int bucketStart = (s - 1) / 5 * 5 + 1;
                    buckets[bucketStart] = buckets.TryGetValue(bucketStart, out var c) ? c + 1 : 1;
                }
                foreach (var kv in buckets)
                    t.Report("[sim100] 구간별", $"S{kv.Key}~S{kv.Key + 4}: {kv.Value}건");
            }
        }
    }
}
