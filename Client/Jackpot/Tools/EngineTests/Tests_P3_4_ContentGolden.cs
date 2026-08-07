using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // 웹 파리티 P3-4 Opus 2차검수 필수⑩(WEB_PARITY_DESIGN.md §2) — 신규 증강9·유물12·저주16(fx 교체분)
    // 손전사 골든 테이블. Tests_Fx.cs의 FNV 스냅샷은 "이후 값이 바뀌면 잡아낸다"(회귀 방지)일 뿐 "지금
    // 값이 웹과 실제로 일치하는가"는 검증하지 않는다(스냅샷은 Perks.cs 자체에서 뽑아낸 값이라 Perks.cs가
    // 처음부터 틀렸으면 스냅샷도 함께 틀린 채 "통과"한다) — 이 파일은 Perks.cs를 보지 않고 public/play/
    // data.js·engine.js를 다시 열어 독립적으로 옮겨 적은 (id,tier,unlockLevel,price,fx) 표로, 두 축이
    // 서로 다른 실수를 잡아낸다(Tests_Ach.cs GoldenTable과 동일 원칙).
    public static class Tests_P3_4_ContentGolden
    {
        // fx 딕셔너리는 인라인 컬렉션 이니셜라이저로 표기(키=Mods fx 점표기, 값=수치).
        private static Dictionary<string, double> Fx(params (string key, double val)[] kv)
        {
            var d = new Dictionary<string, double>();
            foreach (var (key, val) in kv) d[key] = val;
            return d;
        }

        // ── 신규 증강 9종 (data.js:247-337, engine.js:204-208/300-304) ──────────────────────────
        private static readonly (string id, Tier tier, int unlockLevel, Dictionary<string, double> fx)[] AugGolden =
        {
            ("discount", Tier.SILVER, 0, Fx(("shopPriceMul", 0.9))),
            ("thrifty", Tier.SILVER, 0, Fx(("itemPriceMul", 0.8))),
            ("item_bag", Tier.SILVER, 0, Fx(("itemCapBonus", 1))),
            ("vip", Tier.GOLD, 0, Fx(("shopSlotBonus", 1), ("shopRerollDelta", -2))),
            ("refund", Tier.GOLD, 0, Fx()), // 아이템 사용 "keep" 판정은 ItemUse.Use가 직접 처리(fx 없음)
            ("crown_burst", Tier.PRISM, 10, Fx(("perSymbolScore.crown", 100), ("symbolWeightMul.crown", 2.5), ("weightAdd.crown", 2.0))),
            ("curse_grad", Tier.PRISM, 12, Fx(("cond.curseScoreBase", 0.15))),
            ("extreme_overload", Tier.PRISM, 15, Fx(("expMul", 1.9), ("quotaMul", 1.3))),
            ("abyss_lore", Tier.PRISM, 16, Fx(("cond.bossExpMul", 1.5))),
        };

        // ── 신규 유물 12종(전부 PRISM) — 보스클리어 풀 8종(engine.js:332-339) + 레벨해금 4종(306-309) ──
        private static readonly (string id, int price, int unlockLevel, Dictionary<string, double> fx)[] RelGolden =
        {
            ("prism_diploma", 28, 0, Fx(("expMul", 1.40), ("scoreMul", 1.20))),
            ("golden_ratio", 30, 0, Fx(("set3ExpMul", 1.40), ("set4ScoreMul", 1.40))),
            ("starlight_crown", 30, 0, Fx(("perSymbolScore.crown", 60), ("coinMul", 1.30), ("symbolWeightMul.crown", 1.4))),
            ("endless_recess", 36, 0, Fx(("bonusSpins", 1), ("firstSpinExpMul", 1.35), ("lastSpinExpMul", 1.35))),
            ("fortunes_wheel", 34, 0, Fx(("rareBurstExpMul", 1.60), ("rareBurstScoreMul", 1.30), ("rareWeightMul", 1.25))),
            ("set_resonator", 28, 0, Fx(("setExpMul", 1.50), ("twoSetBonusMul", 1.30))),
            ("reapers_pact", 28, 0, Fx(("skullExp", 7), ("perSkullExp", 3), ("scoreMul", 1.15))),
            ("phoenix_thesis", 34, 0, Fx(("cond.quotaPct", 0.5), ("cond.cliffBurstExpMul", 2.0))),
            ("crown_monolith", 28, 10, Fx(("perSymbolScore.crown", 80), ("symbolWeightMul.crown", 1.5))),
            ("black_grad_photo", 26, 12, Fx(("cond.curseScoreBase", 0.12))),
            ("last_roll", 30, 15, Fx(("lastSpinExpMul", 2.0), ("firstSpinExpMul", 0.9))),
            ("nameless_cup", 40, 20, Fx(("scoreMul", 1.6), ("quotaMul", 1.25))),
        };

        // ── 저주 16종 — 웹 engine.js:378-395 buildMods 저주 루프(순수 패널티, 보너스 필드 전부 삭제) ──
        private static readonly (string id, Dictionary<string, double> fx)[] CurseGolden =
        {
            ("hard_exam", Fx(("quotaMul", 1.10))),
            ("cursed_skulls", Fx(("weightAdd.skull", 4.0), ("flatExp", -4))),
            ("speed_test", Fx(("bonusSpins", -1))),
            ("frugal_vow", Fx(("coinMul", 0.6))),
            ("tunnel_vision", Fx(("endsMatchExpMul", 0.5), ("firstSpinExpMul", 0.85))),
            ("late_bloomer", Fx(("firstSpinExpMul", 0.5))),
            ("gem_obsession", Fx(("perSymbolExp.cherry", -2), ("perSymbolExp.book", -2))),
            ("high_stakes", Fx(("quotaMul", 1.08))),
            ("thorny_path", Fx(("weightAdd.skull", 3.0), ("skullExp", -5))),
            ("hex_allornothing", Fx(("setExpMul", 0.5))),
            ("sleep_debt", Fx(("flatExp", -5))),
            ("diploma_pressure", Fx(("quotaMul", 1.12))),
            ("exam_week", Fx(("quotaMul", 1.12))),
            ("blackout", Fx(("weightAdd.skull", 4.0))),
            ("pop_quiz", Fx(("bonusSpins", -1))),
            ("student_debt", Fx(("coinMul", 0.5))),
        };

        public static void Run(TestCtx t)
        {
            t.Check(AugGolden.Length == 9, $"신규 증강 골든 테이블 == 9행 (실제 {AugGolden.Length})");
            t.Check(RelGolden.Length == 12, $"신규 유물 골든 테이블 == 12행 (실제 {RelGolden.Length})");
            t.Check(CurseGolden.Length == 16, $"저주 골든 테이블 == 16행 (실제 {CurseGolden.Length})");

            foreach (var g in AugGolden)
            {
                var p = Perks.ById(g.id);
                if (p == null) { t.Check(false, $"[aug-golden] {g.id}가 Perks.All에 없음"); continue; }
                t.Check(p.cat == PCat.AUGMENT, $"[aug-golden] {g.id}.cat == AUGMENT");
                t.Check(p.tier == g.tier, $"[aug-golden] {g.id}.tier == {g.tier} (실제 {p.tier})");
                t.Check(p.unlockLevel == g.unlockLevel, $"[aug-golden] {g.id}.unlockLevel == {g.unlockLevel} (실제 {p.unlockLevel})");
                CheckFxExact(t, "aug-golden", g.id, g.fx, p.fx);
            }

            foreach (var g in RelGolden)
            {
                var p = Perks.ById(g.id);
                if (p == null) { t.Check(false, $"[rel-golden] {g.id}가 Perks.All에 없음"); continue; }
                t.Check(p.cat == PCat.RELIC, $"[rel-golden] {g.id}.cat == RELIC");
                t.Check(p.tier == Tier.PRISM, $"[rel-golden] {g.id}.tier == PRISM (실제 {p.tier})");
                t.Check(p.price == g.price, $"[rel-golden] {g.id}.price == {g.price} (실제 {p.price})");
                t.Check(p.unlockLevel == g.unlockLevel, $"[rel-golden] {g.id}.unlockLevel == {g.unlockLevel} (실제 {p.unlockLevel})");
                CheckFxExact(t, "rel-golden", g.id, g.fx, p.fx);
            }

            foreach (var g in CurseGolden)
            {
                var p = Perks.ById(g.id);
                if (p == null) { t.Check(false, $"[curse-golden] {g.id}가 Perks.All에 없음"); continue; }
                t.Check(p.cat == PCat.CURSE, $"[curse-golden] {g.id}.cat == CURSE");
                t.Check(p.tier == Tier.GOLD, $"[curse-golden] {g.id}.tier == GOLD (실제 {p.tier})");
                t.Check(p.price == 0, $"[curse-golden] {g.id}.price == 0 (실제 {p.price})");
                CheckFxExact(t, "curse-golden", g.id, g.fx, p.fx);
            }

            // 골든 테이블 자체의 id 중복 0(작성 실수 방지).
            CheckNoDup(t, "aug-golden", AugGolden.Select(x => x.id));
            CheckNoDup(t, "rel-golden", RelGolden.Select(x => x.id));
            CheckNoDup(t, "curse-golden", CurseGolden.Select(x => x.id));
        }

        // fx 완전 일치(golden에 없는 실제 fx 키가 남아있거나, golden에 있는데 실제에 없으면 실패) —
        // "핵심 수치만 대충 맞다"가 아니라 "이 퍽의 fx는 정확히 이 키·값 집합"까지 검증한다.
        private static void CheckFxExact(TestCtx t, string group, string id, Dictionary<string, double> golden, Dictionary<string, double> actual)
        {
            actual ??= new Dictionary<string, double>();
            t.Check(actual.Count == golden.Count,
                $"[{group}] {id}.fx 키 개수 == {golden.Count} (실제 {actual.Count}: {string.Join(",", actual.Keys.OrderBy(k => k))})");
            foreach (var kv in golden)
            {
                if (!actual.TryGetValue(kv.Key, out var v))
                {
                    t.Check(false, $"[{group}] {id}.fx[{kv.Key}] 존재해야 함 (실제엔 없음)");
                    continue;
                }
                t.Check(Math.Abs(v - kv.Value) < 1e-9, $"[{group}] {id}.fx[{kv.Key}] == {kv.Value} (실제 {v})");
            }
        }

        private static void CheckNoDup(TestCtx t, string label, IEnumerable<string> ids)
        {
            var list = ids.ToList();
            var dup = list.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            t.Check(dup.Count == 0, $"{label} 골든 테이블 id 중복 0 (중복: {string.Join(",", dup)})");
        }
    }

    // 웹 파리티 P3-4 Opus 2차검수 필수③(WEB_PARITY_DESIGN.md §2) — 상점 5필드(shopPriceMul/
    // itemPriceMul/itemCapBonus/shopSlotBonus/shopRerollDelta) 실배선 손계산 검증. Shop.cs/ItemUse.cs가
    // 내부적으로 mods를 올바르게 조회해 가격/칸/리롤에 반영하는지 "필드 보유 전/후" 비교로 확인한다.
    internal static class Tests_P3_4_ShopFields
    {
        public static void Run(TestCtx t)
        {
            CheckDiscount(t);
            CheckThrifty(t);
            CheckItemBag(t);
            CheckVip(t);
        }

        // discount: shopPriceMul ×0.9 — 증강/유물/아이템 전부(itemPm = pm×itemPriceMul, itemPriceMul
        // 기본 1.0이라 discount 단독으로도 아이템에 pm=0.9가 그대로 곱해진다, 웹 game.js:2328).
        private static void CheckDiscount(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(70001L);
            run.Perks.Add("discount");
            var stat = S4TestHelpers.GenerousStat();
            var offer = Shop.FreshOffer(run, stat);
            t.True(offer.Count > 0, "[shop-field:discount] 오퍼 비어있지 않음");
            foreach (var e in offer)
            {
                switch (e.kind)
                {
                    case 'A':
                    {
                        var p = Perks.ById(e.id);
                        int baseP = p.tier == Tier.SILVER ? 14 : p.tier == Tier.GOLD ? 24 : 36;
                        int expected = RoundAwayFromZero(baseP * 0.9);
                        t.Eq(expected, e.price, $"[shop-field:discount] AUG {e.id}({p.tier}) price == round({baseP}×0.9)");
                        break;
                    }
                    case 'R':
                    {
                        var p = Perks.ById(e.id);
                        int expected = RoundAwayFromZero(p.price * 0.9);
                        t.Eq(expected, e.price, $"[shop-field:discount] REL {e.id} price == round({p.price}×0.9)");
                        break;
                    }
                    default:
                    {
                        var it = Items.ById(e.id);
                        int expected = RoundAwayFromZero(it.coinCost * 0.9);
                        t.Eq(expected, e.price, $"[shop-field:discount] ITEM {e.id} price == round({it.coinCost}×0.9)");
                        break;
                    }
                }
            }
        }

        // thrifty: itemPriceMul ×0.8 — 증강/유물엔 무영향(shopPriceMul 그대로 1.0), 아이템만 itemPm=0.8.
        private static void CheckThrifty(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(70002L);
            run.Perks.Add("thrifty");
            var stat = S4TestHelpers.GenerousStat();
            var offer = Shop.FreshOffer(run, stat);
            t.True(offer.Count > 0, "[shop-field:thrifty] 오퍼 비어있지 않음");
            foreach (var e in offer)
            {
                switch (e.kind)
                {
                    case 'A':
                    {
                        var p = Perks.ById(e.id);
                        int baseP = p.tier == Tier.SILVER ? 14 : p.tier == Tier.GOLD ? 24 : 36;
                        t.Eq(baseP, e.price, $"[shop-field:thrifty] AUG {e.id}({p.tier}) price 불변(={baseP}, thrifty는 증강 무관)");
                        break;
                    }
                    case 'R':
                    {
                        var p = Perks.ById(e.id);
                        t.Eq(p.price, e.price, $"[shop-field:thrifty] REL {e.id} price 불변(={p.price}, thrifty는 유물 무관)");
                        break;
                    }
                    default:
                    {
                        var it = Items.ById(e.id);
                        int expected = RoundAwayFromZero(it.coinCost * 0.8);
                        t.Eq(expected, e.price, $"[shop-field:thrifty] ITEM {e.id} price == round({it.coinCost}×0.8)");
                        break;
                    }
                }
            }
        }

        // item_bag: itemCapBonus +1 — 가방 상한 3→4.
        private static void CheckItemBag(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(70003L);
            t.Eq(3, ItemUse.EffectiveSlots(run), "[shop-field:item_bag] 미보유 시 3칸(BaseItemSlots)");
            run.Perks.Add("item_bag");
            t.Eq(4, ItemUse.EffectiveSlots(run), "[shop-field:item_bag] 보유 시 4칸(3+1)");
        }

        // vip: shopSlotBonus +1(아이템 상품칸 2→3)·shopRerollDelta -2(리롤 6→4코인).
        private static void CheckVip(TestCtx t)
        {
            var run = S4TestHelpers.NewRun(70004L);
            var stat = S4TestHelpers.GenerousStat();

            t.Eq(6, Shop.RerollCostFor(run), "[shop-field:vip] 미보유 시 리롤 6코인(BaseRerollCost)");
            var baseOffer = Shop.FreshOffer(run, stat);
            t.Eq(2, baseOffer.Count(e => e.kind == 'I'), "[shop-field:vip] 미보유 시 아이템 칸 2");

            run.Perks.Add("vip");
            t.Eq(4, Shop.RerollCostFor(run), "[shop-field:vip] 보유 시 리롤 4코인(6-2, 하한 2 미도달)");
            var vipOffer = Shop.FreshOffer(run, stat);
            t.Eq(3, vipOffer.Count(e => e.kind == 'I'), "[shop-field:vip] 보유 시 아이템 칸 3(2+1)");
        }

        // Shop.cs의 private RoundPrice와 동일한 JS Math.round 근사(반올림 하한 1은 이 테스트 값 범위에서
        // 절대 걸리지 않아 생략 — 최소 아이템가 4코인×0.8=3.2 반올림 3 등 전부 1 초과).
        private static int RoundAwayFromZero(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }
}
