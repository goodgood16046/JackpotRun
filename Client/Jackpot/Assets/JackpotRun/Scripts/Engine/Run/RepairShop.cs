using System;
using System.Collections.Generic;
using System.Linq;

namespace JackpotRun.Engine
{
    // 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 C) — 심화모드 상점 '심볼 정비' 서비스
    // 11종 실행. 웹 engine.js `applyShopService`/`repairBounds`(§2200-2304)와 game.js `_repairPriceMul`/
    // `_repairPrice`/`repairBuy`/`_purifyEmptyToBasic`(§2368-2512)를 하나로 합친 것 — 웹은 순수 함수
    // (applyShopService)+커밋(game.js repairBuy) 2단 구조지만, Unity RunState는 애초에 가변 상태라
    // "state 스냅샷 객체"(웹 `_repairState()`) 없이 run 필드를 직접 읽고 쓴다(DeepRunHooks.DeepPenalty와
    // 동일한 설계 결정).
    //
    // RunPhase/상점 탭 통합(UI)은 P7-4 — 이 파일은 엔진 API(RunController.Do(new RepairBuy(...)))와
    // 그 아래 순수 가격/범위 계산만 제공한다.
    public sealed class RepairArgs
    {
        public string Id;   // addBasic/addHigh/addRare/remove/upgrade 대상 심볼 id
        public string From; // swap 원본 심볼 id
        public string To;   // swap 대상 심볼 id
        public string Tag;  // tagbuff 대상 태그
    }

    public static class RepairShop
    {
        // 심볼증강 할인 반영 가격 배수 — 웹 `_repairPriceMul`(sp.repairMul["*"] × sp.repairMul[kind]).
        public static double PriceMul(RunState run, string kind)
        {
            var sp = SymPerks.ComputeMods(run.Perks, run.Pouch, run.PerkLevels);
            double mul = 1.0;
            if (sp.RepairMul.TryGetValue("*", out var star)) mul *= star;
            if (!string.IsNullOrEmpty(kind) && sp.RepairMul.TryGetValue(kind, out var k)) mul *= k;
            return mul;
        }

        // 실가격(하한 1) — 웹 `_repairPrice`(Math.max(1, Math.round(price*mul))).
        public static int Price(RunState run, RepairServiceDef sv)
        {
            double mul = PriceMul(run, sv.kind);
            return (int)Math.Max(1, Math.Round(sv.price * mul, MidpointRounding.AwayFromZero));
        }

        // 현재 유효 총량 상·하한 + 태그비중 상한 — 웹 `repairBounds(_repairState())`. 정비소 누적 델타
        // (run.DeepTotalMaxDelta/MinDelta) + 심볼퍽 boundMaxDelta/MinDelta/boundMax/boundMin/tagCap 합산.
        public static (int totalMax, int totalMin, double tagMaxRatio) Bounds(RunState run)
        {
            var sp = SymPerks.ComputeMods(run.Perks, run.Pouch, run.PerkLevels);
            double maxDelta = run.DeepTotalMaxDelta + sp.BoundMaxDelta;
            double minDelta = run.DeepTotalMinDelta + sp.BoundMinDelta;
            if (sp.BoundMax != 0) maxDelta = Math.Max(maxDelta, sp.BoundMax - Pouch.DeckMax);
            if (sp.BoundMin != 0) minDelta = Math.Min(minDelta, sp.BoundMin - Pouch.DeckMin);
            double tagMaxRatio = sp.TagCap != 0 ? Math.Max(Pouch.TagMaxRatio, sp.TagCap) : Pouch.TagMaxRatio;
            int totalMax = Pouch.DeckMax + (int)Math.Round(maxDelta, MidpointRounding.AwayFromZero);
            int totalMin = Math.Max(1, Pouch.DeckMin + (int)Math.Round(minDelta, MidpointRounding.AwayFromZero));
            return (totalMax, totalMin, tagMaxRatio);
        }

        // 저주 정화 노출 여부(웹 `repairServices()`의 curseCleanse 필터 — 저주 보유 시만). UI 목록
        // 표시용 헬퍼(P7-4)일 뿐 `Execute`의 하드 게이트로는 쓰지 않는다 — 웹 `applyShopService`
        // 자체는 이 필터를 몰라 아래 curseCleanse case의 `run.Curses.Count==0` 검사가 이미 그 역할을
        // 하므로(REPAIR_NO_CURSE), 여기서 한 번 더 걸면 도달 불가능한 중복 분기가 생긴다.
        public static bool IsAvailable(RunState run, RepairServiceDef sv) =>
            sv.kind != "curseCleanse" || (run.Curses != null && run.Curses.Count > 0);

        // service.kind → Pouch.PouchReward 변환 — 웹 `serviceToReward`. null=심볼 카운트형 아님(expand/
        // compress/tagbuff/curseCleanse).
        private static Pouch.PouchReward ServiceToReward(RepairServiceDef sv, RepairArgs args)
        {
            int n = Math.Max(0, sv.n2);
            switch (sv.kind)
            {
                case "addBasic": case "addHigh": case "addRare":
                    return string.IsNullOrEmpty(args.Id) ? null : new Pouch.PouchReward { Type = Pouch.PouchRewardType.Add, Id = args.Id, N = n };
                case "remove":
                    return string.IsNullOrEmpty(args.Id) ? null : new Pouch.PouchReward { Type = Pouch.PouchRewardType.Remove, Id = args.Id, N = n };
                case "swap":
                    return (!string.IsNullOrEmpty(args.From) && !string.IsNullOrEmpty(args.To) && args.From != args.To)
                        ? new Pouch.PouchReward { Type = Pouch.PouchRewardType.Swap, From = args.From, To = args.To, N = n }
                        : null;
                case "upgrade":
                    return string.IsNullOrEmpty(args.Id) ? null : new Pouch.PouchReward { Type = Pouch.PouchRewardType.Upgrade, Id = args.Id, N = n };
                // 정화 = 해골(skull)→빈칸(empty) swap N(총량 유지·실제 해골<N이면 있는 만큼만) — 웹 그대로.
                case "purify":
                    return new Pouch.PouchReward { Type = Pouch.PouchRewardType.Swap, From = "skull", To = "empty", N = n };
                default:
                    return null;
            }
        }

        private static bool PouchEquals(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a) { if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false; }
            return true;
        }

        // 기본(rarity="기본") 심볼 풀(empty/random 제외) — 정화전문가/정화된세계(purifyToBasic) 승격 대상.
        private static readonly string[] BasicPool = Pouch.Symbols71
            .Where(id => Pouch.RarityOf(id) == "기본" && id != "empty" && id != "random")
            .ToArray();

        // 정화로 생긴 빈칸(empty) n개를 랜덤 기본심볼로 승격(총량 유지) — 웹 `_purifyEmptyToBasic`.
        private static void PurifyEmptyToBasic(RunState run, int n)
        {
            if (BasicPool.Length == 0) return;
            for (int k = 0; k < n; k++)
            {
                if (!run.Pouch.TryGetValue("empty", out var e) || e <= 0) break;
                var gid = BasicPool[run.Rng.Next(BasicPool.Length)];
                run.Pouch["empty"] = e - 1;
                if (run.Pouch["empty"] <= 0) run.Pouch.Remove("empty");
                run.Pouch[gid] = (run.Pouch.TryGetValue(gid, out var g) ? g : 0) + 1;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Execute — 정비 서비스 구매 단일 진입점. 코인 확인 → 엔진 적용(규칙 위반/변화없음 시 거부·
        // 미차감) → 상태 커밋(코인/주머니/델타/태그버프/저주) → 심볼퍽 부수효과(교체 코인·환급·정화
        // 코인·정화전문가 승격) → deepPity 예약(add/upgrade 성공 시) → 전공 발동/승급 감지.
        // ══════════════════════════════════════════════════════════════════
        public static List<RunEvent> Execute(RunState run, string serviceId, RepairArgs args)
        {
            if (run == null || !run.DeepMode) return RunEvents.Rejected("NOT_DEEP_MODE");
            // Opus 2차검수(P7-2, 2026-08-09) 필수② — Shop.Buy/Reroll/Leave와 동일 관례(Shop.cs 3곳
            // `if (run.Phase != RunPhase.EventShop) return RunEvents.Rejected("PHASE_NOT_SHOP");` 그대로).
            // 이전엔 이 게이트가 없어 Spin/PostSpin 등 엉뚱한 phase에서도 정비 구매가 통과됐다.
            if (run.Phase != RunPhase.EventShop) return RunEvents.Rejected("PHASE_NOT_SHOP");
            var sv = RepairServices.ById(serviceId);
            if (sv == null) return RunEvents.Rejected("REPAIR_SERVICE_UNKNOWN");
            args ??= new RepairArgs();

            int price = Price(run, sv);
            if (run.Coins < price) return RunEvents.Rejected("INSUFFICIENT_COINS");

            var reward = ServiceToReward(sv, args);
            string curseRemoved = null;

            if (reward != null)
            {
                var before = new Dictionary<string, int>(run.Pouch);
                var next = Pouch.ApplyReward(before, reward);
                if (PouchEquals(before, next)) return RunEvents.Rejected("REPAIR_NO_CHANGE");
                var bounds = Bounds(run);
                var valid = Pouch.Validate(next, bounds.totalMax, bounds.totalMin, bounds.tagMaxRatio);
                if (!valid.Ok) return RunEvents.Rejected("REPAIR_INVALID_POUCH: " + string.Join(" · ", valid.Errors));

                run.Coins -= price;
                run.Pouch.Clear();
                foreach (var kv in next) run.Pouch[kv.Key] = kv.Value;

                // 배치F P2: 정비 add/upgrade 구매 성공 → 신규심볼 pity 설정(NodeEvents POUCH 오퍼와
                // 동일 가드 — swap/remove는 제외). 웹 game.js:2470-2474 그대로.
                if (sv.kind == "addBasic" || sv.kind == "addHigh" || sv.kind == "addRare" || sv.kind == "upgrade")
                {
                    string pid = sv.kind == "upgrade" ? (Pouch.Upgrade.TryGetValue(args.Id ?? "", out var up) ? up : null) : args.Id;
                    if (!string.IsNullOrEmpty(pid) && pid != "empty" && pid != "random" && Symbols.ById(pid) != null)
                        run.DeepPity = new DeepPityState(pid, 2);
                }

                // 심볼증강/유물 코인 보상 — 교체(교체전문가+3·교체영수증 환급%)·정화(정화붓+N·정화전문가 승격).
                var sp = SymPerks.ComputeMods(run.Perks, run.Pouch, run.PerkLevels);
                if (sv.kind == "swap")
                {
                    if (sp.SwapCoin > 0) run.Coins += (long)sp.SwapCoin;
                    if (sp.RepairRefundFrac > 0)
                    {
                        int refund = (int)Math.Round(price * sp.RepairRefundFrac, MidpointRounding.AwayFromZero);
                        if (refund > 0) run.Coins += refund;
                    }
                }
                if (sv.kind == "purify")
                {
                    if (sp.PurifyCoin > 0) run.Coins += (long)sp.PurifyCoin;
                    if (sp.PurifyToBasic)
                    {
                        int skullBefore = before.TryGetValue("skull", out var sb) ? sb : 0;
                        int skullAfter = next.TryGetValue("skull", out var sa2) ? sa2 : 0;
                        int moved = Math.Max(0, skullBefore - skullAfter);
                        if (moved > 0) PurifyEmptyToBasic(run, moved);
                    }
                }
            }
            else if (sv.kind == "expand")
            {
                run.Coins -= price;
                run.DeepTotalMaxDelta += sv.n2;
            }
            else if (sv.kind == "compress")
            {
                run.Coins -= price;
                run.DeepTotalMinDelta -= sv.n2;
                run.DeepCompressExtra += sv.pct;
            }
            else if (sv.kind == "tagbuff")
            {
                if (string.IsNullOrEmpty(args.Tag)) return RunEvents.Rejected("REPAIR_TAG_REQUIRED");
                run.Coins -= price;
                run.DeepTagBuff[args.Tag] = (run.DeepTagBuff.TryGetValue(args.Tag, out var b) ? b : 0) + sv.pct;
            }
            else if (sv.kind == "curseCleanse")
            {
                if (run.Curses.Count == 0) return RunEvents.Rejected("REPAIR_NO_CURSE");
                run.Coins -= price;
                curseRemoved = run.Curses[run.Curses.Count - 1];
                run.Curses.RemoveAt(run.Curses.Count - 1);
            }
            else
            {
                return RunEvents.Rejected("REPAIR_SERVICE_UNSUPPORTED");
            }

            run.ShopBoughtLabels.Add(sv.emoji + sv.name);
            if (run.DeepStats != null) run.DeepStats.Repairs += 1;

            var events = new List<RunEvent>
            {
                new RunEvent { type = "REPAIR_DONE", repairServiceId = sv.id, repairPrice = price, curseRemovedId = curseRemoved },
            };
            // 배치G: 정비로 주머니 비중 변경 → 전공 발동/승급 즉시 알림(웹 game.js:2494 `_checkArchetype()`,
            // expand/compress/tagbuff처럼 주머니 자체가 안 바뀌어도 무해하게 호출 — 변화 없으면 null).
            var archEvent = DeepRunHooks.CheckArchetypeChange(run);
            if (archEvent != null) events.Add(archEvent);

            return events;
        }
    }
}
