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

        // instantQuota(run)/qOf(stage,mods)와 동일한 손계산 — novice(quotaMul×0.92)·퍽/저주/phaseItems
        // 없음·기본 머신(quotaMul 기여 없음) 전제(순환검증 방지: buildMods를 다시 호출하지 않고 novice의
        // 계수를 직접 곱한다). run.CharId가 "novice"가 아니면 이 헬퍼를 쓰지 말 것.
        public static long HandQuota(RunState run)
        {
            if (run.CharId != "novice") throw new InvalidOperationException("HandQuota는 novice 전용 손계산이다");
            return (long)(Formulas.Quota(run.Stage) * 0.92);
        }
    }

    // ── ① 상점 오퍼 생성 — 고정 시드 재현성 + gatedPool BASE 폴백 ──────────────────────────────────
    internal static class Tests_S4_ShopOffer
    {
        public static void Run(TestCtx t)
        {
            FixedSeedReproducibility(t);
            EntryShapeAndPricing(t);
            GatedPoolLevelGateOnly(t);
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

        // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #13, Shop.cs 헤더 각주) — Schools/AccountLevel 게이트
        // 폐기로 GatedPool은 이제 "unlockLevel 있는 8종만 PlayerLevel로 게이트, 나머지 154종은 항상 개방"
        // 규칙이다. 구 "전부 잠기면 BasePerkIds 폴백" 데드엔드 방어 자체가 이제 도달 불가(레벨 무관하게
        // 154종이 항상 통과하므로) — 새 규칙을 직접 검증하는 테스트로 교체.
        private static readonly string[] LevelGatedAugIds = { "crown_burst", "curse_grad", "extreme_overload", "abyss_lore" };
        private static readonly string[] LevelGatedRelIds = { "crown_monolith", "black_grad_photo", "last_roll", "nameless_cup" };

        private static void GatedPoolLevelGateOnly(TestCtx t)
        {
            // playerLevel 키가 없는(=Lv1 취급) stat — Schools 시절의 "완전히 잠긴" 뷰를 재사용하되 이제는
            // "레벨게이트 8종만 제외"가 기대값이다.
            var lowLevelStat = new Dictionary<string, long> { ["dummy_unrelated_key"] = 1 };
            var gatedAug = Shop.GatedPool(Perks.Augments, lowLevelStat);
            var gatedRelic = Shop.GatedPool(Perks.Relics, lowLevelStat);

            t.Eq(Perks.Augments.Length - LevelGatedAugIds.Length, gatedAug.Count, "[level-gate] Lv1: 증강 풀 = 전체-레벨게이트4");
            t.Eq(Perks.Relics.Length - LevelGatedRelIds.Length, gatedRelic.Count, "[level-gate] Lv1: 유물 풀 = 전체-레벨게이트4");
            foreach (var id in LevelGatedAugIds)
                t.True(!gatedAug.Any(p => p.id == id), $"[level-gate] Lv1: 증강 {id}는 제외됨");
            foreach (var id in LevelGatedRelIds)
                t.True(!gatedRelic.Any(p => p.id == id), $"[level-gate] Lv1: 유물 {id}는 제외됨");
            // 구 Schools 전용게이트였던 퍽(overdrive 등)은 이제 레벨 무관하게 항상 통과해야 한다.
            t.True(gatedAug.Any(p => p.id == "overdrive"), "[level-gate] 구 Schools 게이트 퍽(overdrive)도 항상 개방");

            // PlayerLevel 충분(20) → 전체 풀 그대로.
            var highLevelStat = new Dictionary<string, long> { ["playerLevel"] = 20 };
            var gatedAugHigh = Shop.GatedPool(Perks.Augments, highLevelStat);
            var gatedRelicHigh = Shop.GatedPool(Perks.Relics, highLevelStat);
            t.Eq(Perks.Augments.Length, gatedAugHigh.Count, "[level-gate] Lv20: 증강 풀 = 전체(레벨게이트 전부 통과)");
            t.Eq(Perks.Relics.Length, gatedRelicHigh.Count, "[level-gate] Lv20: 유물 풀 = 전체(레벨게이트 전부 통과)");

            // pickAugments도 Lv1에서는 레벨게이트 4종을 절대 뽑지 않는다.
            var rng = new Rng(555L);
            var picks = Shop.PickAugments(rng, 5, new HashSet<string>(), 40, lowLevelStat);
            t.True(picks.All(p => !LevelGatedAugIds.Contains(p.id)), "[level-gate] pickAugments Lv1: 레벨게이트 4종 미출현");

            // 완전 빈 맵(null과 동치)은 무필터 — 기존 Kotlin stat.isEmpty() 분기 그대로 유지.
            var unfiltered = Shop.GatedPool(Perks.Augments, new Dictionary<string, long>());
            t.Eq(Perks.Augments.Length, unfiltered.Count, "[level-gate] 빈 스탯맵 = 무필터(원본 그대로)");
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
            // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — 상점 나가기도 REWARD_DONE을 거친다.
            t.Eq(RunPhase.RewardDone, run.Phase, "[leave] Phase → RewardDone");
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
            // WEB_PARITY P1 ④: DEVICE 노드(신설) + EVENT 6번 분기(장치획득/전부보유 폴백).
            DeviceNode(t, stat, seed++);
            EventTableDeviceBranch(t, stat);
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
            // 웹 파리티 P4 — 웹 game.js:2185 `_enterRewardDone` 그대로.
            t.Eq(RunPhase.RewardDone, run.Phase, $"[node:{kind}] 선택 후 Phase → RewardDone");
            t.True(!string.IsNullOrEmpty(run.RewardMessage), $"[node:{kind}] RewardMessage 채워짐");
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
            // WEB_PARITY P1 ④: 코인 8 → 12(웹 game.js:1633).
            t.Eq(12, ev[0].coinsDelta, "[node:Rest] 코인 +12");
            t.Eq(12, run.Coins, "[node:Rest] 실제 코인 반영");
            t.Eq(RunPhase.RewardDone, run.Phase, "[node:Rest] Phase → RewardDone");
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
            t.Eq(RunPhase.RewardDone, run.Phase, "[node:Gamble] Phase → RewardDone");

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
            t.Eq(RunPhase.RewardDone, run.Phase, "[node:Event] Phase → RewardDone");
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
            // WEB_PARITY P1 ④: 코인 15 → 30(웹 game.js:1673).
            t.Eq(30, run.Coins, "[node:Curse] 코인 +30");
            t.Eq(RunPhase.RewardDone, run.Phase, "[node:Curse] Phase → RewardDone");
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
            t.Eq(RunPhase.RewardDone, run.Phase, "[node:Risk] Phase → RewardDone");
        }

        // WEB_PARITY P1 ④: DEVICE 노드 신설(웹 game.js:1696 case "DEVICE" + 2523-2529 deviceNodeTake).
        // 오퍼 확정(ChooseNode) → RunPhase.DeviceNode 전환 → TakeDevice(equip)로 장착/코인 중 택1,
        // 어느 쪽이든 OwnedDeviceIds에 영구 반영된다.
        private static void DeviceNode(TestCtx t, IReadOnlyDictionary<string, long> stat, long seed)
        {
            // 장착(equip=true) 경로.
            var runEquip = NodeRun(seed, NodeKind.Device);
            runEquip.PendingDeviceDrop = "dev_flame";
            runEquip.Device = ""; // 미장착 상태에서 시작 — 장착 결과를 명확히 관찰
            var offer = NodeEvents.ChooseNode(runEquip, 0, stat);
            t.Eq("DEVICE_OFFER", offer[0].type, "[node:Device] 오퍼 이벤트");
            t.Eq("dev_flame", offer[0].deviceId, "[node:Device] 오퍼 장치id");
            t.Eq(RunPhase.DeviceNode, runEquip.Phase, "[node:Device] Phase → DeviceNode");

            var takeEquip = NodeEvents.TakeDevice(runEquip, equip: true);
            t.Eq("NODE_RESOLVED", takeEquip[0].type, "[node:Device] 장착 확정 이벤트");
            t.Eq("dev_flame", takeEquip[0].deviceGrantedId, "[node:Device] deviceGrantedId=dev_flame(장착)");
            t.Eq(0, takeEquip[0].coinsDelta, "[node:Device] 장착 시 코인 변화 없음");
            t.Eq("dev_flame", runEquip.Device, "[node:Device] 장착 시 run.Device 교체");
            t.True(runEquip.OwnedDeviceIds.Contains("dev_flame"), "[node:Device] 장착해도 영구 보유(OwnedDeviceIds) 반영");
            t.Eq("", runEquip.PendingDeviceDrop, "[node:Device] 소비 후 PendingDeviceDrop 리셋");
            t.Eq(RunPhase.Spin, runEquip.Phase, "[node:Device] 확정 후 Phase → Spin");

            // 미장착(equip=false, 코인+15) 경로.
            var runCoin = NodeRun(seed + 1, NodeKind.Device);
            runCoin.PendingDeviceDrop = "dev_seal";
            runCoin.Device = "dev_reroll"; // 이미 다른 장치 장착 중 — 코인 선택 시 그대로 유지돼야 함
            NodeEvents.ChooseNode(runCoin, 0, stat);
            var takeCoin = NodeEvents.TakeDevice(runCoin, equip: false);
            t.Eq("dev_seal", takeCoin[0].deviceGrantedId, "[node:Device] deviceGrantedId=dev_seal(미장착)");
            t.Eq(15, takeCoin[0].coinsDelta, "[node:Device] 미장착 시 코인 +15");
            t.Eq(15, runCoin.Coins, "[node:Device] 실제 코인 반영");
            t.Eq("dev_reroll", runCoin.Device, "[node:Device] 미장착 선택 — 기존 장착 장치 그대로 유지");
            t.True(runCoin.OwnedDeviceIds.Contains("dev_seal"), "[node:Device] 미장착이어도 영구 보유 반영");

            // 드랍이 없는 상태(PendingDeviceDrop=="")에서 Device 노드 선택 시 EVENT 테이블로 방어 폴백.
            var runNoDrop = NodeRun(seed + 2, NodeKind.Device);
            runNoDrop.PendingDeviceDrop = "";
            var evNoDrop = NodeEvents.ChooseNode(runNoDrop, 0, stat);
            t.Eq("NODE_RESOLVED", evNoDrop[0].type, "[node:Device] 드랍 없음 → EVENT 폴백(방어)");
            t.Eq(NodeKind.Event, evNoDrop[0].node, "[node:Device] 폴백 이벤트는 node=Event");
            // 이 폴백은 EVENT 테이블 경유(RewardFlow.Enter) — TakeDevice 직행 경로와 달리 RewardDone을 거친다.
            t.Eq(RunPhase.RewardDone, runNoDrop.Phase, "[node:Device] 폴백은 EVENT 경유라 Phase → RewardDone");
        }

        // WEB_PARITY P1 ④: EVENT 10분기표 6번 — 미보유 장치 무작위 1개 지급(장착 중이 없으면 자동
        // 장착), 전부 보유 중이면 코인+15 폴백(웹 game.js:2292 _randomEvent case6 의미 확인 후 이식).
        // roll==6이 나올 때까지 시드를 훑는다(RunNet.RollNextNodesStageGate와 동일한 확률 스모크 관례).
        private static void EventTableDeviceBranch(TestCtx t, IReadOnlyDictionary<string, long> stat)
        {
            (RunState run, RunEvent ev) FindRoll6(long fromSeed, Action<RunState> setup)
            {
                for (long seed = fromSeed; seed < fromSeed + 5000; seed++)
                {
                    var run = NodeRun(seed, NodeKind.Event);
                    setup?.Invoke(run);
                    var result = NodeEvents.ChooseNode(run, 0, stat);
                    if (result[0].eventRoll == 6) return (run, result[0]);
                }
                throw new InvalidOperationException("[event6] 5000회 내 eventRoll==6을 찾지 못함(RNG 분포 회귀 의심)");
            }

            // 장치 획득 경로 — 미보유 장치 존재(신규 런은 OwnedDeviceIds가 비어 있음), 미장착 상태 시작.
            var (runGrant, evGrant) = FindRoll6(9000, r => r.Device = "");
            t.True(!string.IsNullOrEmpty(evGrant.deviceGrantedId), "[event6] 미보유 장치 존재 시 deviceGrantedId 지급됨");
            t.Eq(0, evGrant.coinsDelta, "[event6] 장치 지급 성공 시 코인 폴백 없음");
            t.True(runGrant.OwnedDeviceIds.Contains(evGrant.deviceGrantedId), "[event6] 지급 장치가 OwnedDeviceIds에 영구 반영");
            t.Eq(evGrant.deviceGrantedId, runGrant.Device, "[event6] 미장착 상태였으므로 지급 장치가 자동 장착됨(웹 !r.device 조건)");

            // 전부 보유 상태 — 코인+15 폴백. Devices.All 전체를 OwnedDeviceIds에 미리 채운다.
            var (runFallback, evFallback) = FindRoll6(50000, r => { foreach (var d in Devices.All) r.OwnedDeviceIds.Add(d.id); });
            t.True(string.IsNullOrEmpty(evFallback.deviceGrantedId), "[event6] 전부 보유 → deviceGrantedId 없음(폴백)");
            t.Eq(15, evFallback.coinsDelta, "[event6] 전부 보유 → 코인 +15 폴백");
            t.Eq(Devices.Count, runFallback.OwnedDeviceIds.Count, "[event6] 폴백 — OwnedDeviceIds 변화 없음(전부 보유 유지)");
        }
    }

    // ── WEB_PARITY P1 ④ Opus 1차검수 수정③(2026-08-07) — NodeEvents.PickDevice rare 가중 추첨 ──────
    // 웹 engine.js pickDevices(±L1296-1309) rareChance=min(0.6,0.15+stage*0.03) 이식 회귀. Devices.All은
    // rare=true 8종/rare=false 8종으로 정확히 반씩 나뉘어(Devices.cs) owned가 비어 있으면 어느 쪽
    // 등급을 원하든 후보 풀이 항상 비지 않는다 — 폴백 잡음 없이 rareChance 자체를 순수하게 관측할 수 있다.
    internal static class Tests_S4_DevicePickRareWeight
    {
        public static void Run(TestCtx t)
        {
            RareChanceFormulaAndDistribution(t);
            UnownedFilterAndAllOwnedFallback(t);
            BothBranchesReachableAtLowStage(t);
        }

        private static void RareChanceFormulaAndDistribution(TestCtx t)
        {
            // rareChance = min(0.6, 0.15+stage*0.03) 손계산 3점(사전조건).
            t.EqTol(0.18, Math.Min(0.6, 0.15 + 1 * 0.03), "[pickDevice] stage1: rareChance=0.18(사전조건)");
            t.EqTol(0.51, Math.Min(0.6, 0.15 + 12 * 0.03), "[pickDevice] stage12: rareChance=0.51(사전조건)");
            t.EqTol(0.6, Math.Min(0.6, 0.15 + 50 * 0.03), "[pickDevice] stage50: rareChance 상한 0.6 클램프(사전조건)");

            // 실측: stage50(rareChance 상한고정 0.6) 2000시드, owned 비어있음 → rare 비율이 0.6 근방인지.
            int rareCount = 0;
            const int trials = 2000;
            var emptyOwned = new HashSet<string>();
            for (long seed = 1; seed <= trials; seed++)
            {
                var picked = NodeEvents.PickDevice(new Rng(seed), 50, emptyOwned);
                if (picked.rare) rareCount++;
            }
            double frac = rareCount / (double)trials;
            t.Report("[pickDevice] stage50 rare 비율 실측", $"{rareCount}/{trials}={frac:P1}(기대 0.6 근방)");
            t.True(Math.Abs(frac - 0.6) < 0.06, "[pickDevice] stage50: rare 비율이 rareChance(0.6) 근방(±0.06, n=2000)");
        }

        private static void UnownedFilterAndAllOwnedFallback(TestCtx t)
        {
            // rare 전부 보유 → wantRare=true를 뽑아도 그 등급 후보가 없어 등급무관 미보유 폴백(=non-rare).
            var rareIds = new HashSet<string>(Devices.All.Where(d => d.rare).Select(d => d.id));
            for (long seed = 5000; seed < 5100; seed++)
            {
                var picked = NodeEvents.PickDevice(new Rng(seed), 1, rareIds);
                t.True(picked != null && !rareIds.Contains(picked.id), $"[pickDevice] rare 전부보유(seed={seed}): non-rare로 폴백(rare 미포함)");
            }

            // 전부(16종 모두) 보유 → null(§2-F 결정: 웹의 owned-포함 3차 폴백을 재현하지 않는다).
            var allIds = new HashSet<string>(Devices.All.Select(d => d.id));
            var pickedNone = NodeEvents.PickDevice(new Rng(1L), 1, allIds);
            t.True(pickedNone == null, "[pickDevice] 전부 보유(16/16) → null(호출측이 코인 폴백 처리)");
        }

        private static void BothBranchesReachableAtLowStage(TestCtx t)
        {
            bool sawRare = false, sawNonRare = false;
            var emptyOwned = new HashSet<string>();
            for (long seed = 1; seed <= 200 && !(sawRare && sawNonRare); seed++)
            {
                var picked = NodeEvents.PickDevice(new Rng(seed), 1, emptyOwned);
                if (picked.rare) sawRare = true; else sawNonRare = true;
            }
            t.True(sawRare, "[pickDevice] stage1: 200시드 내 rare 경로 관측(양쪽 다 도달 가능함을 확인)");
            t.True(sawNonRare, "[pickDevice] stage1: 200시드 내 non-rare 경로 관측");
        }
    }

    // ── 🧩 세트 시너지 5% off-tier 주입(NodeEvents.OfferPerks·Shop.SetSynergyAug) — Fable 후속 지시
    // (2026-07-31): 고정 시드 대량 샘플로 발생률이 3%~7% 대역인지, 주입된 퍽이 보유 퍽과 세트를
    // 이루는지 검증한다. 두 시나리오 모두 "held 퍽이 정확히 하나의 미완성 세트에만 속하고, 그 세트의
    // 빠진 조각이 정확히 1개뿐"이 되도록 구성해 injected id를 결정론적으로 예측할 수 있게 했다(주입이
    // 발생하면 항상 그 id여야 함 — 우연히 통과하는 게 아님을 보장).
    //
    // 웹 파리티 P3.5(WEB_PARITY_DESIGN.md §2-(T) 후속②) — 웹 setSynergyPick(engine.js:1175-1192)은
    // cat 인자를 절대 읽지 않고 항상 AUGMENTS에서만 조각을 찾는다("이름이 setSynergyAug인 이유").
    // RELIC 노드 시나리오를 예전 "set_combo(set_sense,set_charm) — set_charm(RELIC) 주입"에서
    // "set_cherry_net(cherry_up,cherry_jam) — cherry_jam(RELIC) 보유 → cherry_up(AUGMENT) 주입"으로
    // 교체했다: 이제 RELIC 노드 오퍼라도 주입 조각이 AUGMENT일 수 있음을 직접 증명하는 시나리오다
    // (예전 페어는 새 규칙에서 injected=0%가 되어 실패 — set_charm이 RELIC이라 cat==AUGMENT 필터에
    // 걸러짐).
    internal static class Tests_S4_SetSynergyInjection
    {
        public static void Run(TestCtx t)
        {
            // AUGMENT 노드 — held=["cherry_up"] → set_orchard(cherry_up,cherry_farm) 진행 중.
            // cherry_farm(GOLD·AUGMENT, PERK_FAMILY 미등록=패밀리게이팅 무관)이 유일한 후보.
            // clearedStage=1(2번째 스테이지 클리어 아님, 3·5의 배수 아님) → baseTier=SILVER가 보통이라
            // GOLD인 cherry_farm이 "원래" 3택에 우연히 섞여 치환이 무산되는 충돌 확률을 최소화했다.
            SampleAndAssert(t, "AUGMENT", NodeKind.Augment, "cherry_up", "cherry_farm");

            // RELIC 노드 — held=["cherry_jam"](RELIC) → set_cherry_net(cherry_up,cherry_jam) 진행 중
            // (cherry_jam은 Sets.All 전체에서 이 세트에만 등장 — "정확히 하나의 미완성 세트" 불변식
            // 자동 성립). 빠진 유일한 후보는 cherry_up(AUGMENT). RELIC 노드의 메인 3택 후보 풀은
            // Perks.Relics로 고정돼 있어 AUGMENT인 cherry_up은 애초에 메인 픽 경로로 뽑힐 수 없다
            // (풀 자체가 다름) — 그래서 오퍼에 cherry_up이 보이면 100% 이 시너지 주입 경로를 통과한
            // 것이다(예전 티어 불일치 회피보다 더 확실한 충돌 회피).
            SampleAndAssert(t, "RELIC", NodeKind.Relic, "cherry_jam", "cherry_up");
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

    // ── Opus 회귀 보강 ① broken_prism 2회 연속 사용 → run.PhasePerks.Count==1(덮어쓰기, Add 아님) ──────
    // ItemUse.cs의 "Kotlin L1496 run.copy(phasePerks = pick) — CSV 단일 필드 대입이라 기존 임시 퍽을
    // 덮어쓴다" 주석대로 Clear()+Add()로 구현됐는지 직접 확인한다.
    internal static class Tests_S4_BrokenPrismOverwrite
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var run = S4TestHelpers.NewRun(4004L);
            run.Items.Add("broken_prism");
            run.Items.Add("broken_prism");

            var ev1 = ItemUse.Use(run, "broken_prism", stat);
            t.Eq("ITEM_USED", ev1[0].type, "[broken-prism] 1회차 사용 성공");
            t.Eq(1, run.PhasePerks.Count, "[broken-prism] 1회차 후 PhasePerks 정확히 1개");
            var firstPick = run.PhasePerks[0];
            t.True(!string.IsNullOrEmpty(firstPick), "[broken-prism] 1회차 주입 id 비어있지 않음");

            var ev2 = ItemUse.Use(run, "broken_prism", stat);
            t.Eq("ITEM_USED", ev2[0].type, "[broken-prism] 2회차 사용 성공");
            t.Eq(1, run.PhasePerks.Count,
                "[broken-prism] 2회 연속 사용해도 여전히 1개(Add 누적이었다면 2가 됐을 것 — 덮어쓰기 확인)");
            t.Eq(0, run.Items.Count, "[broken-prism] 가방 2개 모두 소모됨");

            var safe = new HashSet<string>
            {
                "overdrive", "supernova", "wild_world", "joker", "seed_garden",
                "great_harvest", "jackpot", "mega_jackpot", "gamblers_dice", "key_master", "time_warp",
            };
            t.True(safe.Contains(run.PhasePerks[0]), "[broken-prism] 최종 주입 퍽이 안전 프리즘 목록 소속");
        }
    }

    // ── Opus 회귀 보강 ② Retake 풀 소진 경로 — 코인·RETAKE 마커 롤백 + RETAKE_EMPTY ─────────────────
    // NodeEvents.Retake의 "Kotlin L1364-1369: spent 사본은 offerPerks가 null이면 upsert되지 않고
    // 버려진다 → 코인·RETAKE 마커 원복" 주석대로 구현됐는지 확인한다. 웹 파리티 P3-4로 Schools 기반
    // "BasePerkIds 폴백" 개념 자체가 없어졌으므로(Shop.cs 헤더 각주 — 154종은 항상 개방), 이 테스트는
    // "보유(held)로 avail을 진짜 0으로 만드는" 직접적인 방법으로 소진을 재현한다(레벨게이트 무관하게
    // 성립 — 전체 89종을 다 보유하면 어떤 게이트 규칙이든 avail=0).
    internal static class Tests_S4_RetakeExhaustion
    {
        public static void Run(TestCtx t)
        {
            var lockedStat = new Dictionary<string, long> { ["dummy_unrelated_key"] = 1 };
            var run = S4TestHelpers.NewRun(5005L);
            run.Phase = RunPhase.EventAugment;
            run.Device = "dev_retake";
            run.Coins = 50;
            // 증강 89종 전부를 이미 보유시켜 avail을 완전히 고갈시킨다(게이트 규칙과 무관하게 0을 보장).
            foreach (var p in Perks.Augments) run.Perks.Add(p.id);

            long coinsBefore = run.Coins;
            var ev = NodeEvents.Retake(run, lockedStat);

            t.Eq("RETAKE_EMPTY", ev[0].type, "[retake-empty] 풀 소진 → RETAKE_EMPTY");
            t.Eq(NodeKind.Augment, ev[0].node, "[retake-empty] node 필드");
            t.Eq(coinsBefore, run.Coins, "[retake-empty] 코인 롤백(차감 취소)");
            t.True(!run.UsedCmds.Contains("RETAKE"), "[retake-empty] RETAKE 마커 롤백(재추첨 기회 유지)");
            t.Eq(RunPhase.EventAugment, run.Phase, "[retake-empty] Phase 그대로(EVENT_AUGMENT 유지)");

            // 롤백 확인 후 — 코인이 그대로이므로 재시도(예: 다시 Retake)가 여전히 INSUFFICIENT_COINS가
            // 아니라 같은 RETAKE_EMPTY로 재현되는지도 확인(코인 롤백이 진짜인지 이중 검증).
            var ev2 = NodeEvents.Retake(run, lockedStat);
            t.Eq("RETAKE_EMPTY", ev2[0].type, "[retake-empty] 재시도도 동일하게 RETAKE_EMPTY(코인 정말 롤백됐음 확인)");
            t.Eq(coinsBefore, run.Coins, "[retake-empty] 재시도 후에도 코인 불변");
        }
    }

    // ── Opus 회귀 보강 ③ INSTANT 아이템 효과 실측 대표 8종+ ────────────────────────────────────────
    internal static class Tests_S4_InstantItemEffects
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            SimpleAdditive(t, stat);
            ConditionalAndClearing(t, stat);
            RngBased(t, stat);
            RetakeFormFull(t, stat);
        }

        private static RunState FreshSpinRun(long seed)
        {
            var run = S4TestHelpers.NewRun(seed);
            run.Phase = RunPhase.Spin;
            return run;
        }

        private static IReadOnlyList<RunEvent> UseOne(RunState run, string itemId, IReadOnlyDictionary<string, long> stat)
        {
            run.Items.Add(itemId);
            return ItemUse.Use(run, itemId, stat);
        }

        private static void SimpleAdditive(TestCtx t, IReadOnlyDictionary<string, long> stat)
        {
            var r1 = FreshSpinRun(9001); long c1 = r1.Coins; var e1 = UseOne(r1, "old_coin", stat);
            t.Eq("ITEM_USED", e1[0].type, "[instant:old_coin] 사용 성공");
            t.Eq(c1 + 6, r1.Coins, "[instant:old_coin] 코인 +6");

            var r2 = FreshSpinRun(9002); long c2 = r2.Coins; UseOne(r2, "mini_coupon", stat);
            t.Eq(c2 + 9, r2.Coins, "[instant:mini_coupon] 코인 +9");

            var r3 = FreshSpinRun(9003); long c3 = r3.Coins; UseOne(r3, "price_hack", stat);
            t.Eq(c3 + 18, r3.Coins, "[instant:price_hack] 코인 +18");

            var r4 = FreshSpinRun(9004); long s4 = r4.Score; UseOne(r4, "score_sticker", stat);
            t.Eq(s4 + 150, r4.Score, "[instant:score_sticker] 점수 +150");

            var r5 = FreshSpinRun(9005); int b5 = r5.StageBonusSpins; UseOne(r5, "first_aid", stat);
            t.Eq(b5 + 1, r5.StageBonusSpins, "[instant:first_aid] 스핀 +1");

            var r6 = FreshSpinRun(9006); int b6 = r6.StageBonusSpins; UseOne(r6, "double_aid", stat);
            t.Eq(b6 + 2, r6.StageBonusSpins, "[instant:double_aid] 스핀 +2");

            var r7 = FreshSpinRun(9007); t.True(!r7.Survive, "[instant:insurance_cert] 사용 전 Survive=false");
            UseOne(r7, "insurance_cert", stat);
            t.True(r7.Survive, "[instant:insurance_cert] Survive=true");

            var r8 = FreshSpinRun(9008); long c8 = r8.Coins; UseOne(r8, "debt_note", stat);
            t.Eq(c8 + 30, r8.Coins, "[instant:debt_note] 코인 +30");
            t.Eq(4, r8.DebtStages, "[instant:debt_note] DebtStages=4");

            var r9 = FreshSpinRun(9009); var e9 = UseOne(r9, "dev_battery", stat);
            t.Eq("ITEM_USED", e9[0].type, "[instant:dev_battery] 사용 성공");
            t.True(r9.ArmItems.Contains("dev_coin"), "[instant:dev_battery] armItems에 dev_coin 편성(레버 재사용)");
        }

        private static void ConditionalAndClearing(TestCtx t, IReadOnlyDictionary<string, long> stat)
        {
            // score_calc: score += score*30/100
            var r1 = FreshSpinRun(9101); r1.Score = 1000; UseOne(r1, "score_calc", stat);
            t.Eq(1000 + 1000 * 30 / 100, r1.Score, "[instant:score_calc] 점수 +30%");

            // grad_copy: StageExp += quota*80/100, Score = Score*9/10
            var r2 = FreshSpinRun(9102); r2.Score = 1000;
            long quota2 = S4TestHelpers.HandQuota(r2);
            UseOne(r2, "grad_copy", stat);
            t.Eq(quota2 * 80 / 100, r2.StageExp, "[instant:grad_copy] 게이지 +요구치80%");
            t.Eq(1000L * 9 / 10, r2.Score, "[instant:grad_copy] 점수 ×0.9(10% 페널티)");

            // grad_ring 범위 밖(부족분>20) → 무효과, 클리어 안 됨.
            var r3 = FreshSpinRun(9103);
            long quota3 = S4TestHelpers.HandQuota(r3);
            r3.StageExp = quota3 - 50; // 부족분 50 > 20
            var e3 = UseOne(r3, "grad_ring", stat);
            t.Eq(1, e3.Count, "[instant:grad_ring범위밖] 이벤트 1개(클리어 안 됨)");
            t.Eq("ITEM_USED", e3[0].type, "[instant:grad_ring범위밖] 타입");
            t.Eq(quota3 - 50, r3.StageExp, "[instant:grad_ring범위밖] 부족분>20 → 게이지 불변(무효과)");
            t.Eq(1, r3.Stage, "[instant:grad_ring범위밖] 스테이지 불변(클리어 안 됨)");

            // grad_ring 범위 안(부족분<=20) → 즉시 클리어(StageFlow.ClearStage 경유, 2번째 이벤트).
            var r4 = FreshSpinRun(9104);
            long quota4 = S4TestHelpers.HandQuota(r4);
            r4.StageExp = quota4 - 10; // 부족분 10, 범위 안
            var e4 = UseOne(r4, "grad_ring", stat);
            t.Eq(2, e4.Count, "[instant:grad_ring범위안] 이벤트 2개(ITEM_USED + STAGE_CLEARED)");
            t.Eq("ITEM_USED", e4[0].type, "[instant:grad_ring범위안] 첫 이벤트");
            t.Eq("STAGE_CLEARED", e4[1].type, "[instant:grad_ring범위안] 두번째 이벤트(즉시클리어)");
            t.Eq(2, r4.Stage, "[instant:grad_ring범위안] 클리어로 Stage+1");
            t.Eq(0L, r4.StageExp, "[instant:grad_ring범위안] 클리어 리셋으로 StageExp=0");

            // gold_grad_bell 범위 안(부족분<=50) → 즉시 클리어.
            var r5 = FreshSpinRun(9105);
            long quota5 = S4TestHelpers.HandQuota(r5);
            r5.StageExp = quota5 - 40; // 부족분 40, gold_grad_bell 범위(<=50) 안
            var e5 = UseOne(r5, "gold_grad_bell", stat);
            t.Eq(2, e5.Count, "[instant:gold_grad_bell] 이벤트 2개(즉시클리어)");
            t.Eq("STAGE_CLEARED", e5[1].type, "[instant:gold_grad_bell] 두번째 이벤트");
            t.Eq(2, r5.Stage, "[instant:gold_grad_bell] 클리어로 Stage+1");
        }

        private static void RngBased(TestCtx t, IReadOnlyDictionary<string, long> stat)
        {
            // black_lottery: 50% 골드유물 / 50% 저주. 신규 런 + 관대한 stat → 둘 중 하나는 항상 지급.
            var r1 = FreshSpinRun(9201); var e1 = UseOne(r1, "black_lottery", stat);
            t.Eq("ITEM_USED", e1[0].type, "[instant:black_lottery] 사용 성공");
            bool gotRelic = r1.Perks.Count == 1 && Perks.ById(r1.Perks[0]).cat == PCat.RELIC && Perks.ById(r1.Perks[0]).tier == Tier.GOLD;
            bool gotCurse = r1.Curses.Count == 1;
            t.True(gotRelic || gotCurse, "[instant:black_lottery] 골드유물 또는 저주 중 하나가 실제로 지급됨");
            t.True(!(gotRelic && gotCurse), "[instant:black_lottery] 두 갈래 중 하나만(동시 지급 아님)");

            // devil_contract: 유물+저주 지급 성패와 무관하게 코인은 항상 +25.
            var r2 = FreshSpinRun(9202); long c2 = r2.Coins; var e2 = UseOne(r2, "devil_contract", stat);
            t.Eq("ITEM_USED", e2[0].type, "[instant:devil_contract] 사용 성공");
            t.Eq(c2 + 25, r2.Coins, "[instant:devil_contract] 코인 +25(항상)");
            t.True(r2.Perks.Count <= 1, "[instant:devil_contract] 유물 0~1개");
            t.True(r2.Curses.Count <= 1, "[instant:devil_contract] 저주 0~1개");
            t.True(r2.Perks.Count == 1, "[instant:devil_contract] 신규런+관대한stat → 유물은 사실상 항상 지급");
            t.True(r2.Curses.Count == 1, "[instant:devil_contract] 신규런 → 저주도 사실상 항상 지급(16종 중 미보유 다수)");

            // timeline_ticket: 다음 스핀 결과를 lockedNext로 확정 — 5칸·전부 유효 심볼.
            var r3 = FreshSpinRun(9203); t.Eq(0, r3.LockedNext.Count, "[instant:timeline_ticket] 사용 전 LockedNext 비어있음");
            var e3 = UseOne(r3, "timeline_ticket", stat);
            t.Eq("ITEM_USED", e3[0].type, "[instant:timeline_ticket] 사용 성공");
            t.Eq(Formulas.REEL, r3.LockedNext.Count, "[instant:timeline_ticket] LockedNext 5칸 확정");
            t.True(r3.LockedNext.All(id => Symbols.ById(id) != null), "[instant:timeline_ticket] 전부 유효 심볼id");

            // broken_prism 단발 확인(덮어쓰기 자체는 Tests_S4_BrokenPrismOverwrite에서 별도 검증).
            var r4 = FreshSpinRun(9204); var e4 = UseOne(r4, "broken_prism", stat);
            t.Eq("ITEM_USED", e4[0].type, "[instant:broken_prism] 사용 성공");
            t.Eq(1, r4.PhasePerks.Count, "[instant:broken_prism] PhasePerks 1개 편성");
        }

        // retake_form — 직전 스핀 전체 재굴림, net-adjust 대수적 일관성.
        private static void RetakeFormFull(TestCtx t, IReadOnlyDictionary<string, long> stat)
        {
            var run = FreshSpinRun(9301);
            run.LastCells.AddRange(new[] { "cherry", "book", "star", "gem", "crown" });
            run.LastSpinNo = 0;
            run.SpinIndex = 1;
            long oldStageExp = 15, oldScore = 4, oldCoins = 12;
            long oldLastGain = 15, oldLastScoreGain = 4;
            int oldLastCoinGain = 0;
            run.StageExp = oldStageExp; run.Score = oldScore; run.Coins = oldCoins;
            run.LastGain = oldLastGain; run.LastScoreGain = oldLastScoreGain; run.LastCoinGain = oldLastCoinGain;
            run.Items.Add("retake_form");

            var ev = ItemUse.Use(run, "retake_form", stat);
            t.Eq("ITEM_USED", ev[0].type, "[instant:retake_form] 사용 성공");
            t.True(ev[0].spin != null, "[instant:retake_form] SpinOutcome 페이로드 존재");
            t.Eq(0, run.Items.Count, "[instant:retake_form] 가방에서 소모됨");
            t.True(run.UsedItemThisRun, "[instant:retake_form] UsedItemThisRun=true");

            var res = ev[0].spin.result;
            long expNewExp = Math.Max(oldStageExp - oldLastGain, 0) + res.exp;
            long expNewScore = Math.Max(oldScore - oldLastScoreGain + res.score, 0);
            long expNewCoins = Math.Max(oldCoins - oldLastCoinGain + res.coins, 0);
            t.Eq(expNewExp, run.StageExp, "[instant:retake_form] net-adjust StageExp");
            t.Eq(expNewScore, run.Score, "[instant:retake_form] net-adjust Score");
            t.Eq(expNewCoins, run.Coins, "[instant:retake_form] net-adjust Coins");
            t.Eq(res.exp, run.LastGain, "[instant:retake_form] LastGain 갱신됨");
        }
    }

    // ── Opus 회귀 보강 ④ HoldAugment → 다음 증강 노드에서 offerHeldIncluded==true·0번 칸 보류 id ─────
    internal static class Tests_S4_HoldAugmentCarryover
    {
        public static void Run(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            var run = S4TestHelpers.NewRun(6006L);
            run.Phase = RunPhase.NodeSelect;
            run.Stage = 4; // 5의 배수 아님(보스클리어 프리즘 강제 아님) — 일반 진행
            run.NodeOptions.Add(NodeKind.Augment);
            run.Device = "dev_holdfile";

            var ev1 = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("PERK_OFFER", ev1[0].type, "[hold] 최초 오퍼 생성");
            t.True(!ev1[0].offerHeldIncluded, "[hold] 최초 오퍼는 보류 미포함");

            string holdId = run.PerkOfferIds[0];
            var ev2 = NodeEvents.HoldAugment(run, 0);
            t.Eq("PERK_HELD", ev2[0].type, "[hold] 보류 성공");
            t.Eq(holdId, ev2[0].perkId, "[hold] 보류 이벤트 perkId=0번 칸 id");
            t.Eq(holdId, run.HeldAug, "[hold] run.HeldAug에 저장됨");
            t.Eq(0, run.PerkOfferIds.Count, "[hold] 보류 후 PerkOfferIds 소거");
            // 웹 파리티 P4 — HoldAugment도 다른 노드 해소 분기와 동일하게 RewardDone을 거치도록 확장.
            t.Eq(RunPhase.RewardDone, run.Phase, "[hold] Phase → RewardDone");

            // 다음 증강 노드 재진입.
            run.Phase = RunPhase.NodeSelect;
            run.NodeOptions.Add(NodeKind.Augment);
            var ev3 = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("PERK_OFFER", ev3[0].type, "[hold] 두번째 오퍼 생성");
            t.True(ev3[0].offerHeldIncluded, "[hold] 두번째 오퍼는 offerHeldIncluded=true");
            t.Eq(holdId, ev3[0].perkOfferIds[0], "[hold] 0번 칸이 보류해둔 퍽 id");
            t.Eq(holdId, run.PerkOfferIds[0], "[hold] run.PerkOfferIds[0]도 동일");
            t.Eq("", run.HeldAug, "[hold] 보류 슬롯 소비되어 비워짐");
        }
    }

    // ── Opus 회귀 보강 ⑤ dev_coin 보조슬롯 1.18·dev_oracle PEEK·dev_bell ARMED 각 1건 ────────────────
    internal static class Tests_S4_DeviceEdgeCases
    {
        public static void Run(TestCtx t)
        {
            DevCoinSecondary(t);
            DevOraclePeek(t);
            DevBellArmed(t);
        }

        private static void DevCoinSecondary(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(7001L);
            run.Device = ""; // 메인 미장착
            run.Device2 = "dev_coin";
            run.Coins = 20;
            double oldPending = run.PendingNextExpMul;
            t.Eq(1.0, oldPending, "[dev_coin-sec] 사용 전 PendingNextExpMul=1.0");

            var ev = DeviceActions.Handle(run, "dev_coin", null);
            t.Eq("DEVICE_ARMED", ev[0].type, "[dev_coin-sec] 성공");
            t.True(ev[0].secondary, "[dev_coin-sec] secondary=true");
            t.Eq(15, run.Coins, "[dev_coin-sec] 코인 -5");
            // secondaryMul(1.3) = 1.0 + (1.3-1.0)*SECONDARY_MUL(0.6) = 1.18.
            double expected = 1.0 + (Devices.ById("dev_coin").fx["expMul"] - 1.0) * Formulas.SECONDARY_MUL;
            t.EqTol(1.18, expected, "[dev_coin-sec] 손계산 자체가 1.18(회귀 방지용 상수 확인)");
            t.EqTol(expected, run.PendingNextExpMul, "[dev_coin-sec] PendingNextExpMul 실측 = 1.18 손계산");
            t.Eq(0, run.ArmItems.Count, "[dev_coin-sec] 보조는 armItems 미사용(메인 경로와 다름)");
            t.True(run.UsedCmds.Contains("dev_coin"), "[dev_coin-sec] 스테이지당 1회 마커 기록");
        }

        private static void DevOraclePeek(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(7002L);
            run.Device = "dev_oracle";

            var ev = DeviceActions.Handle(run, "dev_oracle", null);
            t.Eq("DEVICE_PEEK", ev[0].type, "[dev_oracle] 성공");
            t.Eq(Formulas.REEL, ev[0].peekCells.Count, "[dev_oracle] 5칸 미리보기");
            t.True(ev[0].peekCells.All(id => Symbols.ById(id) != null), "[dev_oracle] 전부 유효 심볼id");
            t.True(run.LockedNext.SequenceEqual(ev[0].peekCells), "[dev_oracle] run.LockedNext에 그대로 반영");
            t.True(run.UsedCmds.Contains("dev_oracle"), "[dev_oracle] 스테이지당 1회 마커");
            t.True(run.UsedCmds.Contains("RUNORACLE"), "[dev_oracle] RUNORACLE 마커(런 끝까지 보존용)도 함께 기록");

            // 스테이지당 1회 재확인.
            var ev2 = DeviceActions.Handle(run, "dev_oracle", null);
            t.Eq("REJECTED", ev2[0].type, "[dev_oracle] 같은 스테이지 재사용 거부");
            t.Eq("DEVICE_ALREADY_USED", ev2[0].reason, "[dev_oracle] 거부 사유");
        }

        private static void DevBellArmed(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(7003L);
            run.Device = "dev_bell";
            run.Stage = 1;
            long quota = S4TestHelpers.HandQuota(run);
            run.StageExp = quota - 10; // 부족분 10 <= 25 → 장전 가능

            var ev = DeviceActions.Handle(run, "dev_bell", null);
            t.Eq("DEVICE_ARMED", ev[0].type, "[dev_bell] 부족분<=25 → 장전 성공");
            t.True(run.ArmItems.Contains("dev_bell"), "[dev_bell] armItems에 편성");
            t.True(run.UsedCmds.Contains("dev_bell"), "[dev_bell] 스테이지당 1회 마커");

            var run2 = S4TestHelpers.NewRun(7004L);
            run2.Device = "dev_bell";
            long quota2 = S4TestHelpers.HandQuota(run2);
            run2.StageExp = quota2 - 100; // 부족분 100 > 25 → 거부
            var ev2 = DeviceActions.Handle(run2, "dev_bell", null);
            t.Eq("REJECTED", ev2[0].type, "[dev_bell] 부족분>25 → 거부");
            t.Eq("DEV_BELL_DEFICIT_TOO_HIGH", ev2[0].reason, "[dev_bell] 거부 사유");
            t.Eq(0, run2.ArmItems.Count, "[dev_bell] 거부 시 armItems 불변");
        }
    }

    // ── WEB_PARITY P1 ③ Opus 1차검수 수정A(2026-08-07) — dev_bell POST_SPIN 즉시강제클리어 ──────────
    // DeviceActions.Handle(run,"dev_bell",null)을 POST_SPIN 단계에서 직접 호출(웹 emergencyBell(),
    // game.js:1326-1331 대응) — 칸 선택 없이 StageExp를 quota로 채워 곧바로 클리어, 장치 파괴.
    internal static class Tests_S4_DevBellPostSpin
    {
        public static void Run(TestCtx t)
        {
            SuccessDestroysDeviceAndClears(t);
            RejectedWhenDeficitTooHigh(t);
        }

        private static void SuccessDestroysDeviceAndClears(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(7010L);
            run.Device = "dev_bell";
            run.Phase = RunPhase.PostSpin;
            run.Stage = 1;
            long quota = S4TestHelpers.HandQuota(run);
            run.StageExp = quota - 25; // 부족분 정확히 25(경계, 포함 — deficit<=25)

            var events = DeviceActions.Handle(run, "dev_bell", null);
            t.Eq("STAGE_CLEARED", events[0].type, "[bell-postspin] 즉시 강제클리어");
            t.Eq("dev_bell", events[0].deviceId, "[bell-postspin] deviceId=dev_bell");
            t.Eq("", run.Device, "[bell-postspin] 장치 파괴(1회성, 웹 r.device=\"\")");
            t.Eq(RunPhase.NodeSelect, run.Phase, "[bell-postspin] 클리어 → NodeSelect로 전이");
            t.Eq(2, run.Stage, "[bell-postspin] 스테이지 진행");
            t.True(events[0].clear != null, "[bell-postspin] clear 페이로드 존재");
            t.Eq(quota, events[0].spin.newExp, "[bell-postspin] newExp=quota(정확히 채움, 웹 r.stageExp=r.quota)");
        }

        private static void RejectedWhenDeficitTooHigh(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(7011L);
            run.Device = "dev_bell";
            run.Phase = RunPhase.PostSpin;
            run.Stage = 1;
            long quota = S4TestHelpers.HandQuota(run);
            run.StageExp = quota - 26; // 부족분 26 > 25 → 거부(SPIN단계 HandleDevBell과 동일 사유 재사용)

            var events = DeviceActions.Handle(run, "dev_bell", null);
            t.Eq("REJECTED", events[0].type, "[bell-postspin] 부족분>25 → 거부");
            t.Eq("DEV_BELL_DEFICIT_TOO_HIGH", events[0].reason, "[bell-postspin] 거부 사유");
            t.Eq("dev_bell", run.Device, "[bell-postspin] 거부 시 장치 유지(파괴 안 됨)");
            t.Eq(RunPhase.PostSpin, run.Phase, "[bell-postspin] 거부 시 Phase 유지");
            t.Eq(quota - 26, run.StageExp, "[bell-postspin] 거부 시 StageExp 불변");
        }
    }

    // ── Opus 회귀 보강 ⑥ 확률 실측 3종: 티어 확률표·10% 티어업·EVENT_PRISM_RATE(12%) ────────────────
    internal static class Tests_S4_ProbabilityTables
    {
        public static void Run(TestCtx t)
        {
            TierWeightsTable(t);
            TierUpTenPercent(t);
            EventPrismRate(t);
        }

        // Shop의 private TierWeights(stage) — 직접 호출 불가하므로 PickAugments(n=1)의 결과 티어 분포로
        // 간접 실측한다(매 시행 독립 시드로 rng 오염 없이 티어 1회 롤 + 그 티어에서 1개 픽).
        private static void TierWeightsTable(TestCtx t)
        {
            SampleBracket(t, "stage<=3(78/22/0)", stage: 2, trials: 6000, expSilver: 0.78, expGold: 0.22, expPrism: 0.0);
            SampleBracket(t, "stage>9(22/53/25)", stage: 15, trials: 6000, expSilver: 0.22, expGold: 0.53, expPrism: 0.25);
        }

        private static void SampleBracket(TestCtx t, string label, int stage, int trials, double expSilver, double expGold, double expPrism)
        {
            var stat = S4TestHelpers.GenerousStat();
            int silver = 0, gold = 0, prism = 0;
            for (long seed = 0; seed < trials; seed++)
            {
                var rng = new Rng(800_000L + stage * 100_000L + seed);
                var picks = Shop.PickAugments(rng, stage, new HashSet<string>(), 1, stat);
                if (picks.Count == 0) continue; // 폴백까지 소진되는 극단적 경우 방지(관대한 stat이라 사실상 없음)
                switch (picks[0].tier)
                {
                    case Tier.SILVER: silver++; break;
                    case Tier.GOLD: gold++; break;
                    default: prism++; break;
                }
            }
            int total = silver + gold + prism;
            double rSilver = silver / (double)total, rGold = gold / (double)total, rPrism = prism / (double)total;
            t.Report($"[tier-table:{label}] 실측 분포", $"S={rSilver:P2} G={rGold:P2} P={rPrism:P2} (n={total})");
            t.True(Math.Abs(rSilver - expSilver) <= 0.02, $"[tier-table:{label}] SILVER 비율 ±2%p 이내(실측 {rSilver:P2}, 기대 {expSilver:P2})");
            t.True(Math.Abs(rGold - expGold) <= 0.02, $"[tier-table:{label}] GOLD 비율 ±2%p 이내(실측 {rGold:P2}, 기대 {expGold:P2})");
            t.True(Math.Abs(rPrism - expPrism) <= 0.02, $"[tier-table:{label}] PRISM 비율 ±2%p 이내(실측 {rPrism:P2}, 기대 {expPrism:P2})");
        }

        // NodeEvents.OfferPerks의 "10% 행운! 등급업" — RunEvent.offerTierBumped로 관측 가능.
        // stage=2(clearedStage=1) → baseTier=SILVER 고정이라 TierUp(SILVER)=GOLD, 항상 실제로 등급이
        // 바뀌므로(원본 로직상 up==baseTier인 경우가 없는 조합) offerTierBumped 관측률이 곧 10% 롤 자체다.
        private static void TierUpTenPercent(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            const int trials = 6000;
            int bumped = 0;
            for (long seed = 900_000; seed < 900_000 + trials; seed++)
            {
                var run = S4TestHelpers.NewRun(seed);
                run.Phase = RunPhase.NodeSelect;
                run.Stage = 2; // clearedStage=1 → baseTier=SILVER
                run.NodeOptions.Add(NodeKind.Augment);
                var ev = NodeEvents.ChooseNode(run, 0, stat);
                t.Eq("PERK_OFFER", ev[0].type, $"[tierup seed={seed}] 오퍼 생성 성공");
                if (ev[0].offerTierBumped) bumped++;
            }
            double rate = bumped / (double)trials;
            t.Report("[tierup] 10% 등급업 실측", $"{bumped}/{trials} = {rate:P2}");
            t.True(Math.Abs(rate - 0.10) <= 0.02, $"[tierup] 등급업 발생률 10%±2%p 이내(실측 {rate:P2})");
        }

        // EVENT_PRISM_RATE(private const, 0.12) — freshShopOffer의 "허용" 여부는 직접 노출되지 않으므로,
        // FreshOffer가 소비하는 *첫* RNG 값(allowPrism = rng.NextDouble() < 0.12)을 별도 프로브 Rng로
        // 재현해 시드별로 "임계값 0.12 미만/이상"을 미리 분류한 뒤, 실제 오퍼에 프리즘 증강이 노출되는
        // 비율이 두 그룹 사이에서 뚜렷이(수십 배) 갈리는지로 검증한다. 프로브는 run.Rng와 별개의
        // new Rng(seed) 인스턴스이므로 run의 실제 RNG 소비에는 영향이 없다 — 둘 다 "새로 만들어 아무것도
        // 소비 안 한" 상태에서 시작하므로 seed가 같으면 첫 NextDouble() 값도 반드시 같다(Rng.cs 계약).
        // "false" 그룹은 gatePrism의 anti-dead-end 폴백(4칸 전부 프리즘일 때만, 확률 ≈0.25^4≈0.4%)에서만
        // 노출되므로 매우 낮고, "true" 그룹은 그보다 수십 배 높아야 한다 — 실제 임계값이 0.12에서
        // ±2%p를 벗어나면(예: 0.06이나 0.20) 그룹 경계가 새어 이 대비가 크게 무너진다(설계 문서 주석에
        // 손계산 근거 명시).
        private static void EventPrismRate(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            const int trials = 20000;
            const int stage = 15; // stage>9 고정구간(프리즘 가중치 25%, 매 draw 독립)
            int predTrueCount = 0, predTrueShown = 0;
            int predFalseCount = 0, predFalseShown = 0;

            for (long seed = 2_000_000; seed < 2_000_000 + trials; seed++)
            {
                double probe = new Rng(seed).NextDouble();
                bool predictedAllow = probe < 0.12;

                var run = S4TestHelpers.NewRun(seed);
                run.Stage = stage;
                var offer = Shop.FreshOffer(run, stat);
                bool shown = offer.Any(e => e.kind == 'A' && Perks.ById(e.id).tier == Tier.PRISM);

                if (predictedAllow) { predTrueCount++; if (shown) predTrueShown++; }
                else { predFalseCount++; if (shown) predFalseShown++; }
            }

            double rateTrue = predTrueCount > 0 ? predTrueShown / (double)predTrueCount : 0.0;
            double rateFalse = predFalseCount > 0 ? predFalseShown / (double)predFalseCount : 0.0;
            double overallPredictedTrueFraction = predTrueCount / (double)trials;

            t.Report("[prism-rate] predicted<0.12 그룹 비율(RNG 균등성 sanity)", $"{predTrueCount}/{trials} = {overallPredictedTrueFraction:P2}");
            t.Report("[prism-rate] predicted-true 그룹 프리즘 노출률", $"{predTrueShown}/{predTrueCount} = {rateTrue:P2}");
            t.Report("[prism-rate] predicted-false 그룹 프리즘 노출률", $"{predFalseShown}/{predFalseCount} = {rateFalse:P2}");

            t.True(Math.Abs(overallPredictedTrueFraction - 0.12) <= 0.02,
                $"[prism-rate] 0.12 미만 시드 비율 10~14% 대역(RNG 균등성 확인, 실측 {overallPredictedTrueFraction:P2})");
            t.True(rateFalse <= 0.05, $"[prism-rate] predicted-false 그룹은 거의 노출 안 됨(실측 {rateFalse:P2}, 이론상 ≈0.4%)");
            t.True(rateTrue >= rateFalse * 5.0 && rateTrue > 0.15,
                $"[prism-rate] predicted-true 그룹이 뚜렷이 더 높음(실측 true={rateTrue:P2} false={rateFalse:P2}) " +
                "— EVENT_PRISM_RATE가 0.12에서 크게(±2%p 상당) 벗어나면 이 대비가 무너진다");
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
            "DEVICE_OFFER", // WEB_PARITY P1 ④: DEVICE 노드 오퍼(RunPhase.DeviceNode 진입 이벤트)
            "PERK_LEVELED", // 웹 파리티 P3-3: AUGLEVEL 노드 선택(PickOffer) 결과
            "STAGE_STARTED", // 웹 파리티 P4: RewardDone → Spin(ProceedToStage) 결과
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
                    // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12): AUGLEVEL 노드도 같은 PickOffer(0)
                    // 진입점을 공유한다(NodeEvents.PickOffer가 phase로 분기).
                    case RunPhase.EventAugLevel:
                        events = rc.Do(new PickOffer(0));
                        break;
                    case RunPhase.EventShop:
                        expectSuccess = false; // 코인부족/가방가득 등 정상 거부 가능
                        if (shopStep == 0) { events = rc.Do(new BuyOffer(0)); shopStep = 1; }
                        else { events = rc.Do(new LeaveShop()); shopStep = 0; }
                        break;
                    // WEB_PARITY P1 ④: DEVICE 노드는 RollNextNodes가 항상 셔플된 3개 뒤(마지막 인덱스)에
                    // 붙이므로 이 정책(항상 0번 선택)으로는 실전 도달 불가하지만, 방어적으로 처리해 둔다
                    // (default 분기가 예외를 던지므로 미처리 상태로 남기지 않는다).
                    case RunPhase.DeviceNode:
                        events = rc.Do(new TakeDevice(true));
                        break;
                    // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — "스테이지 N 시작" 탭 즉시 진행.
                    case RunPhase.RewardDone:
                        events = rc.Do(new ProceedToStage());
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

        // Opus 회귀 보강 ⑦ — UseItem/DeviceCmd/RerollShop이 실제로 실행되는 휴리스틱 정책(예외 0 유지
        // 확인용). AutoPlay(단순 정책)와 별개로 유지 — TwoSeedSanity는 좁은 sanity가 목적이라 그대로 둔다.
        // 정책용 난수는 policyRng(System.Random, run.Rng와 무관한 별도 스트림)로 뽑아 엔진 RNG 소비
        // 순서에 전혀 영향을 주지 않는다. actionCounts로 각 액션이 최소 1회 이상 실제 실행됐는지 보고한다.
        private static (bool reachedGameOver, int stage, Dictionary<string, int> actionCounts) AutoPlayRich(RunController rc, int guardMax, long policySeed)
        {
            var policyRng = new Random(unchecked((int)(policySeed ^ (policySeed >> 32))));
            var actionCounts = new Dictionary<string, int>
            {
                ["Spin"] = 0, ["UseItem"] = 0, ["DeviceCmd"] = 0, ["Continue"] = 0,
                ["ChooseNode"] = 0, ["PickOffer"] = 0, ["RerollShop"] = 0, ["BuyOffer"] = 0, ["LeaveShop"] = 0,
            };
            int guard = 0;
            int shopStep = 0; // 0=리롤 시도 → 1=구매 시도 → 2=나가기 → 0(반복)
            while (rc.State.Phase != RunPhase.GameOver && guard < guardMax)
            {
                guard++;
                var phase = rc.State.Phase;
                bool expectSuccess = true;
                string actionName;
                IReadOnlyList<RunEvent> events;
                switch (phase)
                {
                    case RunPhase.Spin:
                        if (rc.State.Items.Count > 0 && policyRng.Next(5) == 0)
                        {
                            actionName = "UseItem"; expectSuccess = false;
                            events = rc.Do(new UseItem(rc.State.Items[0]));
                        }
                        else if (!string.IsNullOrEmpty(rc.State.Device) && policyRng.Next(5) == 0)
                        {
                            actionName = "DeviceCmd"; expectSuccess = false;
                            int? arg = DeviceActions.DeviceNeedsArg(rc.State.Device) ? 1 : (int?)null;
                            events = rc.Do(new DeviceCmd(rc.State.Device, arg));
                        }
                        else
                        {
                            actionName = "Spin";
                            events = rc.Do(new Spin(SpinMode.N));
                        }
                        break;
                    case RunPhase.PostSpin:
                        actionName = "Continue";
                        events = rc.Do(new Continue());
                        break;
                    case RunPhase.NodeSelect:
                        actionName = "ChooseNode";
                        events = rc.Do(new ChooseNode(0));
                        break;
                    case RunPhase.EventAugment:
                    case RunPhase.EventRelic:
                    // 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12): AUGLEVEL 노드도 같은 PickOffer(0)
                    // 진입점을 공유한다(NodeEvents.PickOffer가 phase로 분기).
                    case RunPhase.EventAugLevel:
                        actionName = "PickOffer";
                        events = rc.Do(new PickOffer(0));
                        break;
                    case RunPhase.EventShop:
                        expectSuccess = false; // 코인부족/가방가득 등 정상 거부 가능
                        if (shopStep == 0) { actionName = "RerollShop"; events = rc.Do(new RerollShop()); shopStep = 1; }
                        else if (shopStep == 1) { actionName = "BuyOffer"; events = rc.Do(new BuyOffer(0)); shopStep = 2; }
                        else { actionName = "LeaveShop"; events = rc.Do(new LeaveShop()); shopStep = 0; }
                        break;
                    // WEB_PARITY P1 ④: 위 AutoPlay와 동일 이유로 실전 도달 불가하지만 방어적으로 처리.
                    case RunPhase.DeviceNode:
                        actionName = "TakeDevice";
                        events = rc.Do(new TakeDevice(policyRng.Next(2) == 0));
                        break;
                    // 웹 파리티 P4(WEB_PARITY_DESIGN.md §1-A #15) — "스테이지 N 시작" 탭 즉시 진행.
                    case RunPhase.RewardDone:
                        actionName = "ProceedToStage";
                        events = rc.Do(new ProceedToStage());
                        break;
                    default:
                        throw new InvalidOperationException("AutoPlayRich: 처리 불가 Phase=" + phase);
                }
                actionCounts[actionName] = actionCounts.TryGetValue(actionName, out var c) ? c + 1 : 1;
                foreach (var e in events)
                {
                    if (!KnownEventTypes.Contains(e.type))
                        throw new InvalidOperationException("AutoPlayRich: 알 수 없는 RunEvent.type=" + e.type);
                    if (expectSuccess && e.type == "REJECTED")
                        throw new InvalidOperationException($"AutoPlayRich: phase={phase}·action={actionName}에서 예상치 못한 거부(reason={e.reason})");
                }
            }
            return (rc.State.Phase == RunPhase.GameOver, rc.State.Stage, actionCounts);
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

        // Opus 회귀 보강 ⑦ — device 종류를 seed%4로 순환 배정(무장치/dev_coin/dev_oracle/dev_reroll)해
        // ARMED/PEEK/MANIP 세 갈래 DeviceCmd 경로를 전부 실제 풀런 안에서 지나가게 하고, AutoPlayRich
        // 휴리스틱으로 UseItem/DeviceCmd/RerollShop도 섞는다. 예외 0 유지가 핵심 확인 대상.
        private static void HundredSeedSimulation(TestCtx t)
        {
            var stat = S4TestHelpers.GenerousStat();
            int exceptions = 0;
            var stagesReached = new List<int>();
            var totalActionCounts = new Dictionary<string, int>();
            string[] deviceCycle = { "", "dev_coin", "dev_oracle", "dev_reroll" };

            for (long seed = 1; seed <= 100; seed++)
            {
                try
                {
                    string deviceId = deviceCycle[seed % deviceCycle.Length];
                    var rc = new RunController("novice", "basic", deviceId, seed, stat);
                    var (ok, stage, actionCounts) = AutoPlayRich(rc, 50_000, policySeed: seed);
                    t.True(ok, $"[sim100 seed={seed}] guard(50000) 내 게임오버 도달");
                    stagesReached.Add(stage);
                    foreach (var kv in actionCounts)
                        totalActionCounts[kv.Key] = totalActionCounts.TryGetValue(kv.Key, out var c) ? c + kv.Value : kv.Value;
                }
                catch (Exception ex)
                {
                    exceptions++;
                    t.Fail($"[sim100 seed={seed}]", "예외 발생: " + ex);
                }
            }

            t.Eq(0, exceptions, "[sim100] RunController 풀런(상점·아이템·장치 명령 포함) 시드 100개 — 예외 0건");

            // 액션 커버리지 — 각 액션 경로가 최소 1회 이상 실제로 실행됐는지(휴리스틱이 죽은 코드가
            // 아니었는지) 확인. UseItem/DeviceCmd는 확률적(1/5)이라 100런 합산이면 사실상 항상 여러 번
            // 발동한다.
            foreach (var action in new[] { "Spin", "ChooseNode", "PickOffer", "RerollShop", "BuyOffer", "LeaveShop", "UseItem", "DeviceCmd" })
            {
                int cnt = totalActionCounts.TryGetValue(action, out var c) ? c : 0;
                t.Report("[sim100] 액션 실행 횟수", $"{action}={cnt}");
                t.True(cnt > 0, $"[sim100] {action} 액션이 100런 동안 최소 1회 이상 실제 실행됨");
            }

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

    // ── WEB_PARITY P1 ⑤(2026-08-07) — RunController.GiveUp() 자발적 포기 ────────────────────────────
    // 웹 game.js:1228-1231 giveUp(voluntary=true) 대응. 스핀 잔여 상태(SPIN/POST_SPIN) 어디서든 호출
    // 가능 → 즉시 GameOver 전이·기존 점수공식 그대로 결산·FailureOutcome.Voluntary=true. SPIN도 아니고
    // POST_SPIN도 아닌 상태(NodeSelect 등)에서는 거부된다.
    internal static class Tests_S4_GiveUp
    {
        public static void Run(TestCtx t)
        {
            FromSpinPhase(t);
            FromPostSpinPhase(t);
            RejectedOutsideSpinOrPostSpin(t);
        }

        private static RunController FreshController(long seed) =>
            new RunController("novice", "basic", "", seed, S4TestHelpers.GenerousStat());

        private static void FromSpinPhase(TestCtx t)
        {
            var rc = FreshController(9500L);
            rc.Do(new Spin(SpinMode.N)); // 스핀 잔여 상태를 만들어 점수가 0이 아니게 함
            long scoreBefore = rc.State.Score;
            double expectedMod = StageFlow.ScoreModifierFor(rc.State.MachineId, rc.State.CharId);

            var events = rc.GiveUp();
            t.Eq(1, events.Count, "[giveup:spin] 이벤트 1개");
            t.Eq("GAME_OVER", events[0].type, "[giveup:spin] type=GAME_OVER");
            t.True(events[0].failure != null && events[0].failure.Voluntary, "[giveup:spin] failure.Voluntary=true");
            t.Eq(0L, events[0].failure.deficitAtFailure, "[giveup:spin] deficitAtFailure=0(웹 shortBy=voluntary?0:short)");
            t.Eq(RunPhase.GameOver, rc.State.Phase, "[giveup:spin] Phase → GameOver");
            t.Eq((long)(scoreBefore * expectedMod), events[0].failure.finalScore,
                "[giveup:spin] finalScore = 결산 시점 run.Score × scoreModifier(기존 공식 그대로)");
        }

        private static void FromPostSpinPhase(TestCtx t)
        {
            // MANIP 장치 없이 마지막 스핀에서 실패시켜 POST_SPIN 진입을 강제한다(dev_bell도 없음, deficit
            // 도 15 초과로 fate_bell도 없음 → 통상적으로는 게임오버지만, 여기서는 POST_SPIN을 억지로
            // 재현하기보다 State.Phase를 직접 PostSpin으로 세팅해 "그 상태에서 GiveUp이 허용되는지"만
            // 검증한다 — GiveUp()은 Phase만 확인하므로 이 구성으로 충분하다.
            var rc = FreshController(9501L);
            rc.State.Phase = RunPhase.PostSpin;
            var events = rc.GiveUp();
            t.Eq("GAME_OVER", events[0].type, "[giveup:postspin] type=GAME_OVER");
            t.True(events[0].failure.Voluntary, "[giveup:postspin] failure.Voluntary=true");
            t.Eq(RunPhase.GameOver, rc.State.Phase, "[giveup:postspin] Phase → GameOver");
        }

        private static void RejectedOutsideSpinOrPostSpin(TestCtx t)
        {
            var rc = FreshController(9502L);
            rc.State.Phase = RunPhase.NodeSelect;
            var events = rc.GiveUp();
            t.Eq("REJECTED", events[0].type, "[giveup:invalid-phase] NodeSelect 등에서는 거부");
            t.Eq("PHASE_NOT_SPIN_OR_POST_SPIN", events[0].reason, "[giveup:invalid-phase] reason");
            t.Eq(RunPhase.NodeSelect, rc.State.Phase, "[giveup:invalid-phase] Phase 변경 없음");
        }
    }

    // ── S16 §A / 웹 파리티 P3-4·P3.5 갱신 — 증강/유물 오퍼 티어 풀 소진 폴백(Shop.PickPerksByTier) ────
    // 원래(S16 §A) 재현 경로는 Schools 기반 "BasePerkIds(전부 SILVER)로만 폴백된 게이트 풀"이었다 —
    // 웹 파리티 P3-4로 그 게이트 모델 자체가 폐기돼(154종 항상 개방, Shop.cs 헤더 각주) 더는 재현할
    // 수 없다. "보유(held)로 그 티어를 완전히 고갈시키는" 방식으로 동일 상황(강제 티어에 unheld 후보
    // 0개)을 재현한다.
    // [P3.5 갱신] 폴백 로직 자체가 바뀌었다 — 예전엔 "avail 전체(타티어 혼용)로 대체"였지만(2026-08-03
    // 승인 예외, WEB_PARITY_DESIGN.md §2-(U) 항목②-3에서 웹 기준 단계형(PRISM→GOLD→SILVER) 폴백으로
    // 환원). 이 테스트의 어서션(오퍼가 GOLD 보유분과 무관·GOLD 자체가 없음)은 두 폴백 방식 모두에서
    // 참이라 무수정으로 계속 통과한다(GOLD 소진 시 신·구 폴백 모두 SILVER/PRISM 잔여로 채움).
    internal static class Tests_S4_TierPoolFallback
    {
        public static void Run(TestCtx t)
        {
            TierFallbackViaHeldExhaustion(t, "AUGMENT", NodeKind.Augment, Perks.Augments, seedBase: 8_000_000L);
            TierFallbackViaHeldExhaustion(t, "RELIC", NodeKind.Relic, Perks.Relics, seedBase: 8_100_000L);
            FullPoolExhaustionStillFallsBackToEvent(t);
        }

        // GOLD 티어를 통째로 보유시켜(clearedStage=3 → baseTier=GOLD 강제, Formulas.TierForClearedStage)
        // "강제 티어의 tierPool에 unheld 후보가 0"인 상황을 재현한다 — 폴백이 없다면 오퍼가 통째로 비어
        // EVENT로 샜을 상황. OfferPerks의 "10% 행운 등급업"(GOLD→PRISM)이 걸리면 이 테스트 취지와
        // 무관한 분기로 갈라지므로, 등급업이 걸리지 않은 시드만 골라 검증한다(Rng 결정론이라 재현 가능).
        private static void TierFallbackViaHeldExhaustion(TestCtx t, string label, NodeKind node, Perk[] pool, long seedBase)
        {
            var stat = new Dictionary<string, long> { ["playerLevel"] = 20 }; // 레벨게이트 무관하게 하기 위해 전체 개방
            var goldIds = pool.Where(p => p.tier == Tier.GOLD).Select(p => p.id).ToList();
            t.True(goldIds.Count > 0, $"[tier-fallback:{label}] GOLD 후보 존재");

            RunState foundRun = null;
            RunEvent foundEv = null;

            for (long seed = seedBase; seed < seedBase + 200; seed++)
            {
                var run = S4TestHelpers.NewRun(seed);
                run.Phase = RunPhase.NodeSelect;
                run.Stage = 4; // clearedStage=3 → baseTier=GOLD 강제(%3==0)
                run.NodeOptions.Add(node);
                run.Perks.AddRange(goldIds); // GOLD 전량 보유 → 강제 티어 풀이 unheld 기준 0

                var ev = NodeEvents.ChooseNode(run, 0, stat);
                if (ev[0].type == "PERK_OFFER" && !ev[0].offerTierBumped)
                {
                    foundRun = run;
                    foundEv = ev[0];
                    break;
                }
            }

            t.True(foundRun != null, $"[tier-fallback:{label}] 10% 등급업 안 걸리는 시드를 200개 내에서 확보");
            if (foundRun == null) return;

            t.Eq("PERK_OFFER", foundEv.type, $"[tier-fallback:{label}] GOLD 풀 소진에도 EVENT 아닌 PERK_OFFER");
            t.True(foundRun.PerkOfferIds.Count >= 1, $"[tier-fallback:{label}] 오퍼 최소 1건");
            t.True(foundRun.PerkOfferIds.All(id => !goldIds.Contains(id)),
                $"[tier-fallback:{label}] 오퍼 전원이 GOLD 보유분과 무관(잔여 후보 폴백 풀에서 옴)");
            // Opus 2차검수 권장⑥(WEB_PARITY_DESIGN.md §2-(U)) — 웹 기준 단계형 폴백(tier==GOLD →
            // fallbackOrder=[SILVER]뿐, PRISM은 후보에 없음)이라 "GOLD 없음"보다 강한 "전원 SILVER"까지
            // 단정할 수 있다(PRISM 폴백 자체가 없음 — PRISM은 GOLD보다 상위 티어라 GOLD 소진 시 내려가는
            // 폴백 경로에 아예 포함되지 않는다).
            t.True(foundRun.PerkOfferIds.All(id => Perks.ById(id)?.tier == Tier.SILVER),
                $"[tier-fallback:{label}] 오퍼 전원 SILVER(GOLD 소진 → 단계형 폴백은 SILVER까지만, PRISM 폴백 없음)");
        }

        // avail 자체가 고갈(증강 89종 전부 보유)되면 PickPerksByTier가 즉시 빈 리스트를 반환하는 기존
        // 조기 반환 경로(avail.Count==0, Shop.cs)는 게이트 모델 변경과 무관하게 보존된다 — ChooseNode가
        // 여전히 EVENT 테이블로 폴백(NODE_RESOLVED, PERK_OFFER 아님)하는지 확인.
        private static void FullPoolExhaustionStillFallsBackToEvent(TestCtx t)
        {
            var stat = new Dictionary<string, long> { ["playerLevel"] = 20 };
            var run = S4TestHelpers.NewRun(8_200_000L);
            run.Phase = RunPhase.NodeSelect;
            run.Stage = 4;
            run.NodeOptions.Add(NodeKind.Augment);
            foreach (var p in Perks.Augments) run.Perks.Add(p.id);

            var ev = NodeEvents.ChooseNode(run, 0, stat);
            t.Eq("NODE_RESOLVED", ev[0].type, "[tier-fallback:exhausted] 전 풀 소진 → 기존대로 EVENT 테이블 폴백");
            t.Eq(NodeKind.Event, ev[0].node, "[tier-fallback:exhausted] node 필드 = Event(공용 EVENT 테이블 경유)");
            t.Eq(RunPhase.RewardDone, run.Phase, "[tier-fallback:exhausted] Phase → RewardDone");
        }
    }
}
