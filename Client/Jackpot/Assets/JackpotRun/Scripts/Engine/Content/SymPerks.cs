using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 A) — 심볼증강 21 + 심볼유물 15 + 레벨(Lv1~3
    // 8종) + symPerkMods 집계. 웹 data.js SYM_AUGMENTS/SYM_RELICS/SYM_AUG_LEVELS(§500-597)와 engine.js
    // symPerkMods/effWithLevel(§2060-2183) 그대로 전사.
    //
    // Content 계층(Run/RunState 비의존) — ComputeMods는 perkIds/pouch/levels를 값으로만 받는 순수 함수다
    // (웹 symPerkMods(perkIds, pouch, levels)와 동일한 시그니처 철학). 소유(보유) 저장소는 별도 RunState
    // 필드를 새로 만들지 않고 **기존 RunState.Perks/PerkLevels를 그대로 재사용**한다 — 웹이 실제로 그렇게
    // 동작한다(game.js `_symMods()`가 `E.symPerkMods(r.perks, r.pouch, r.perkLevels)`를 호출, `_heldSymPerks()`도
    // `r.perks.filter(id => SYM_PERK_BY_ID[id])`로 같은 배열을 필터링 — 전용 배열이 없다). id 네임스페이스가
    // sa_/sp_/sr_ 접두로 일반 증강·유물·저주 id와 전역 겹치지 않아 안전하게 공유할 수 있고, `ModsBuilder.Build`의
    // 퍽 루프도 `Perks.ById(symId)`가 null을 반환해 자동으로 건너뛴다(무회귀 확인: Tests_P7_2 참조). [작업
    // 지시서 문면 "RunState.SymPerks(보유)·SymAugLevels(레벨 8종)"에서 이탈 — 웹 정답지 대조 결과 웹 구조를
    // 따르는 편이 파리티에 더 정확해 의도적으로 이렇게 구현했다. 최종 보고에 명시.]
    public sealed class SymPerkEff
    {
        public string Hook;
        public string[] Kinds;          // repairMul.kinds
        public double? Mul;             // repairMul.mul / penaltyMul.mul / skullPenaltyMul.mul
        public double? Frac;            // repairRefund.frac
        public int? Coin;               // swapCoin.coin / purifyCoin.coin
        public string Tag;              // tagBuff.tag ("MOST"|"MAJOR"|"SOLO")
        public double? Pct;             // tagBuff.pct / compressScore.pct / tagbuff류
        public double? Other;           // tagBuff.other
        public double? TagCap;          // tagBuff.tagCap
        public double? WhenTotalLe;
        public double? WhenTotalGe;
        public int? MaxDelta;           // boundDelta.maxDelta
        public int? MinDelta;           // boundDelta.minDelta
        public double? SkullPenaltyMul; // boundDelta.skullPenaltyMul(sr_big_bag 전용, Mul과 별개 필드)
        public int? Max;                // boundOverride.max
        public int? Min;                // boundOverride.min
        public double? ExtraPenaltyLe;  // boundOverride.extraPenaltyLe
        public double? ExtraMul;        // boundOverride.extraMul
        public double? QuotaMul;        // boundOverride.quotaMul(예약) / shopLab.quotaMul
        public int? N;                  // rewardBonus.n / autoShred.n / bossCopy.n
        public int? Basic;              // addBoost.basic
        public double? RareChance;      // addBoost.rareChance
        public int? LegendWeight;       // addBoost.legendWeight
        public double? CurseChance;     // addBoost.curseChance / purifyToBasic.curseChance
        public double? BossQuotaMul;    // addBoost.bossQuotaMul
        public int? Weight;             // shopLab.weight
        public bool? AlwaysRepair;      // shopLab.alwaysRepair
        public int? Score;              // emptyScore.score / rareFirstScore.score
        public int? Exp;                // emptyExp.exp
        public double? ScoreMul;        // purifyToBasic.scoreMul
        public string Kind;             // score.kind ("balance")
        public double? Amount;          // score.amount
        public bool? StatTable;         // display.statTable

        public SymPerkEff Clone() => (SymPerkEff)MemberwiseClone();
    }

    public sealed class SymPerkDef
    {
        public string id;
        public string emoji;
        public string name;
        public string tier; // "SILVER"|"GOLD"|"PRISM"
        public string desc;
        public SymPerkEff eff;
    }

    // 심볼증강 레벨업(Lv2/Lv3) 델타 — 웹 SYM_AUG_LEVELS[id][2|3]. mulSub는 penaltyMul류의 Mul을
    // 감산(더 이득), 나머지는 해당 필드에 가산.
    public sealed class SymAugLevelDelta
    {
        public double? Pct;
        public int? Score;
        public int? Basic;
        public double? RareChance;
        public double? MulSub;
    }

    public static class SymPerks
    {
        // ── 심볼증강 21종(실버6/골드7/프리즘8) — 웹 data.js SYM_AUGMENTS 그대로 ─────────────────
        public static readonly SymPerkDef[] Augments =
        {
            new SymPerkDef { id = "sa_tidy",          emoji = "🧹", name = "심볼정리",   tier = "SILVER", desc = "심볼 제거 비용 -10%",
                eff = new SymPerkEff { Hook = "repairMul", Kinds = new[] { "remove" }, Mul = 0.9 } },
            new SymPerkDef { id = "sa_basic_research", emoji = "🌱", name = "기본연구",   tier = "SILVER", desc = "오퍼 카드 +1장",
                eff = new SymPerkEff { Hook = "addBoost", Basic = 1 } },
            new SymPerkDef { id = "sa_tag_sense",      emoji = "🏷️", name = "태그감각",   tier = "SILVER", desc = "가장 많은 태그 효과 +5%",
                eff = new SymPerkEff { Hook = "tagBuff", Tag = "MOST", Pct = 0.05 } },
            new SymPerkDef { id = "sa_safe_compress",  emoji = "🛟", name = "안전압축",   tier = "SILVER", desc = "총량 28↓ 압축 패널티 감소",
                eff = new SymPerkEff { Hook = "penaltyMul", Mul = 0.6, WhenTotalLe = 28 } },
            new SymPerkDef { id = "sa_use_empty",      emoji = "▫", name = "빈칸활용",   tier = "SILVER", desc = "빈칸 등장 시 점수 +20/개",
                eff = new SymPerkEff { Hook = "emptyScore", Score = 20 } },
            new SymPerkDef { id = "sa_purify_class",   emoji = "🕊️", name = "정화수업",   tier = "SILVER", desc = "해골 제거·정화 비용 -20%",
                eff = new SymPerkEff { Hook = "repairMul", Kinds = new[] { "purify" }, Mul = 0.8 } },

            new SymPerkDef { id = "sa_compress",       emoji = "🗜️", name = "심볼압축",   tier = "GOLD", desc = "총량 27↓ 압축 패널티 절반",
                eff = new SymPerkEff { Hook = "penaltyMul", Mul = 0.5, WhenTotalLe = 27 } },
            new SymPerkDef { id = "sa_tag_major",      emoji = "🎓", name = "태그전공",   tier = "GOLD", desc = "가장 많은 태그 +20%·타 태그 -5%",
                eff = new SymPerkEff { Hook = "tagBuff", Tag = "MAJOR", Pct = 0.20, Other = -0.05 } },
            new SymPerkDef { id = "sa_lab_regular",    emoji = "🔬", name = "연구실단골", tier = "GOLD", desc = "정비 탭 가격 -30%",
                eff = new SymPerkEff { Hook = "repairMul", Kinds = new[] { "*" }, Mul = 0.7 } },
            new SymPerkDef { id = "sa_rare_research",  emoji = "🔮", name = "희귀연구",   tier = "GOLD", desc = "오퍼 골드 확률 +10%p",
                eff = new SymPerkEff { Hook = "addBoost", RareChance = 0.10 } },
            new SymPerkDef { id = "sa_swap_expert",    emoji = "🔁", name = "교체전문가", tier = "GOLD", desc = "심볼 교체 시 코인 +3",
                eff = new SymPerkEff { Hook = "swapCoin", Coin = 3 } },
            new SymPerkDef { id = "sa_expand_build",   emoji = "📦", name = "확장빌드",   tier = "GOLD", desc = "총량 33↑ 해골 패널티 감소",
                eff = new SymPerkEff { Hook = "skullPenaltyMul", Mul = 0.7, WhenTotalGe = 33 } },
            new SymPerkDef { id = "sa_purify_expert",  emoji = "✨", name = "정화전문가", tier = "GOLD", desc = "해골 정화 결과가 랜덤 기본심볼로",
                eff = new SymPerkEff { Hook = "purifyToBasic" } },

            new SymPerkDef { id = "sp_deckslot",       emoji = "🎰", name = "덱빌딩슬롯", tier = "PRISM", desc = "매 스테이지 후 정비 노드·요구 EXP +15%",
                eff = new SymPerkEff { Hook = "shopLab", Weight = 3, QuotaMul = 1.15, AlwaysRepair = true } },
            new SymPerkDef { id = "sp_compress_major", emoji = "🗜️", name = "압축전공",   tier = "PRISM", desc = "총량 최소 21까지·27↓ 요구 급증",
                eff = new SymPerkEff { Hook = "boundOverride", Min = 21, ExtraPenaltyLe = 27, ExtraMul = 1.10 } },
            new SymPerkDef { id = "sp_expand_major",   emoji = "📦", name = "확장전공",   tier = "PRISM", desc = "총량 최대 48까지 (확률 희석)",
                eff = new SymPerkEff { Hook = "boundOverride", Max = 48 } },
            new SymPerkDef { id = "sp_solo_major",     emoji = "🎯", name = "단일전공",   tier = "PRISM", desc = "태그 제한 60→90%·최다 태그 +30%·타 -30%",
                eff = new SymPerkEff { Hook = "tagBuff", Tag = "SOLO", Pct = 0.30, Other = -0.30, TagCap = 0.90 } },
            new SymPerkDef { id = "sp_rare_hunt",      emoji = "🏹", name = "희귀사냥",   tier = "PRISM", desc = "랜덤팩 PRISM 확률 +10%p·희귀 add 시 저주 위험↑",
                eff = new SymPerkEff { Hook = "addBoost", RareChance = 0.6, CurseChance = 0.3 } },
            new SymPerkDef { id = "sp_lab_addict",     emoji = "⚗️", name = "연구실중독", tier = "PRISM", desc = "보상이 정비소(상점) 중심",
                eff = new SymPerkEff { Hook = "shopLab", Weight = 5 } },
            new SymPerkDef { id = "sp_legend_collect", emoji = "👑", name = "전설수집",   tier = "PRISM", desc = "지정슬롯 전설 비중 ↑·보스 요구 EXP +20%",
                eff = new SymPerkEff { Hook = "addBoost", LegendWeight = 1, BossQuotaMul = 1.20 } },
            new SymPerkDef { id = "sp_purified_world", emoji = "🕊️", name = "정화된세계", tier = "PRISM", desc = "해골 정화 가능·저주↓·점수 보너스↓",
                eff = new SymPerkEff { Hook = "purifyToBasic", ScoreMul = 0.9, CurseChance = -1 } },
        };

        // ── 심볼유물 15종(SILVER7/GOLD8) — 프리즘 없음 — 웹 data.js SYM_RELICS 그대로 ─────────────
        public static readonly SymPerkDef[] Relics =
        {
            new SymPerkDef { id = "sr_sorter",           emoji = "🗂️", name = "심볼분류함",   tier = "SILVER", desc = "주머니 보상 선택지 +1",
                eff = new SymPerkEff { Hook = "rewardBonus", N = 1 } },
            new SymPerkDef { id = "sr_shredder",         emoji = "🪓", name = "낡은파쇄기",   tier = "GOLD", desc = "스테이지마다 최저가치 심볼 -1",
                eff = new SymPerkEff { Hook = "autoShred", N = 1 } },
            new SymPerkDef { id = "sr_notebook",         emoji = "📓", name = "연구노트",     tier = "SILVER", desc = "심볼 업그레이드 비용 -20%",
                eff = new SymPerkEff { Hook = "repairMul", Kinds = new[] { "upgrade" }, Mul = 0.8 } },
            new SymPerkDef { id = "sr_compass",          emoji = "🧭", name = "태그나침반",   tier = "GOLD", desc = "가장 많은 태그 효과 +8%",
                eff = new SymPerkEff { Hook = "tagBuff", Tag = "MOST", Pct = 0.08 } },
            new SymPerkDef { id = "sr_scale",            emoji = "⚖️", name = "균형저울",     tier = "GOLD", desc = "태그 60% 근접 시 점수 +12% (근사)",
                eff = new SymPerkEff { Hook = "score", Kind = "balance", Amount = 0.12 } },
            new SymPerkDef { id = "sr_brush",            emoji = "🖌️", name = "정화붓",       tier = "SILVER", desc = "해골 정화 시 코인 +3",
                eff = new SymPerkEff { Hook = "purifyCoin", Coin = 3 } },
            new SymPerkDef { id = "sr_empty_manual",     emoji = "📄", name = "빈칸설명서",   tier = "SILVER", desc = "빈칸 EXP +1/개",
                eff = new SymPerkEff { Hook = "emptyExp", Exp = 1 } },
            new SymPerkDef { id = "sr_rare_case",        emoji = "🧰", name = "희귀표본상자", tier = "GOLD", desc = "희귀 첫 추가 시 점수 +300",
                eff = new SymPerkEff { Hook = "rareFirstScore", Score = 300 } },
            new SymPerkDef { id = "sr_legend_seal",      emoji = "🔏", name = "전설봉인함",   tier = "GOLD", desc = "전설 보유 시 효과 안정 발동·보스 요구 증가분 -25%",
                eff = new SymPerkEff { Hook = "legendSeal" } },
            new SymPerkDef { id = "sr_copier",           emoji = "📑", name = "심볼복사판",   tier = "GOLD", desc = "보스 클리어 시 최다 태그 심볼 +2",
                eff = new SymPerkEff { Hook = "bossCopy", N = 2 } },
            new SymPerkDef { id = "sr_lab_key",          emoji = "🗝", name = "연구실열쇠",   tier = "SILVER", desc = "정비소(상점) 등장↑",
                eff = new SymPerkEff { Hook = "shopLab", Weight = 2 } },
            new SymPerkDef { id = "sr_compress_contract", emoji = "📜", name = "압축계약서",   tier = "GOLD", desc = "총량 27↓ 점수 +20%",
                eff = new SymPerkEff { Hook = "compressScore", Pct = 0.20, WhenTotalLe = 27 } },
            new SymPerkDef { id = "sr_big_bag",          emoji = "🎒", name = "확장가방",     tier = "GOLD", desc = "총량 36↑ 해골 패널티↓·상한 +6",
                eff = new SymPerkEff { Hook = "boundDelta", MaxDelta = 6, SkullPenaltyMul = 0.8, WhenTotalGe = 36 } },
            new SymPerkDef { id = "sr_swap_receipt",     emoji = "🧾", name = "교체영수증",   tier = "SILVER", desc = "심볼 교체 시 25% 코인 환급",
                eff = new SymPerkEff { Hook = "repairRefund", Frac = 0.25 } },
            new SymPerkDef { id = "sr_stat_table",       emoji = "📊", name = "낡은통계표",   tier = "SILVER", desc = "주머니 실효 확률(랜덤칸 반영) 표시 (표시형)",
                eff = new SymPerkEff { Hook = "display", StatTable = true } },
        };

        public static readonly Dictionary<string, SymPerkDef> ById = BuildById();
        private static Dictionary<string, SymPerkDef> BuildById()
        {
            var d = new Dictionary<string, SymPerkDef>();
            for (int i = 0; i < Augments.Length; i++) d[Augments[i].id] = Augments[i];
            for (int i = 0; i < Relics.Length; i++) d[Relics[i].id] = Relics[i];
            return d;
        }

        public static SymPerkDef Get(string id) => (!string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var p)) ? p : null;

        // ── 심볼증강 레벨업(Lv1~3) 8종 — 웹 SYM_AUG_LEVELS 그대로 ───────────────────────────
        public static readonly Dictionary<string, Dictionary<int, SymAugLevelDelta>> AugLevels = new Dictionary<string, Dictionary<int, SymAugLevelDelta>>
        {
            { "sa_tag_sense",      new Dictionary<int, SymAugLevelDelta> { { 2, new SymAugLevelDelta { Pct = 0.03 } }, { 3, new SymAugLevelDelta { Pct = 0.03 } } } },
            { "sa_use_empty",      new Dictionary<int, SymAugLevelDelta> { { 2, new SymAugLevelDelta { Score = 10 } }, { 3, new SymAugLevelDelta { Score = 10 } } } },
            { "sa_basic_research", new Dictionary<int, SymAugLevelDelta> { { 2, new SymAugLevelDelta { Basic = 1 } }, { 3, new SymAugLevelDelta { Basic = 1 } } } },
            { "sa_tag_major",      new Dictionary<int, SymAugLevelDelta> { { 2, new SymAugLevelDelta { Pct = 0.05 } }, { 3, new SymAugLevelDelta { Pct = 0.05 } } } },
            { "sa_rare_research",  new Dictionary<int, SymAugLevelDelta> { { 2, new SymAugLevelDelta { RareChance = 0.20 } }, { 3, new SymAugLevelDelta { RareChance = 0.30 } } } },
            { "sa_compress",       new Dictionary<int, SymAugLevelDelta> { { 2, new SymAugLevelDelta { MulSub = 0.05 } }, { 3, new SymAugLevelDelta { MulSub = 0.05 } } } },
            { "sa_expand_build",   new Dictionary<int, SymAugLevelDelta> { { 2, new SymAugLevelDelta { MulSub = 0.05 } }, { 3, new SymAugLevelDelta { MulSub = 0.05 } } } },
            { "sp_solo_major",     new Dictionary<int, SymAugLevelDelta> { { 2, new SymAugLevelDelta { Pct = 0.05 } }, { 3, new SymAugLevelDelta { Pct = 0.05 } } } },
        };

        public static bool IsLevelable(string id) => !string.IsNullOrEmpty(id) && AugLevels.ContainsKey(id);

        // 레벨(2/3) 델타를 eff 사본에 반영 — 웹 `effWithLevel`. lvl<2면 원본 그대로 반환(사본 아님 —
        // 웹도 `return base`로 원본 참조를 그대로 돌려준다, 호출측이 이를 변형하지 않는 한 안전).
        private static SymPerkEff EffWithLevel(SymPerkDef perk, int lvl)
        {
            var baseEff = perk.eff;
            if (baseEff == null || !IsLevelable(perk.id) || lvl < 2) return baseEff;
            var e = baseEff.Clone();
            var def = AugLevels[perk.id];
            int top = Math.Min(3, lvl);
            for (int L = 2; L <= top; L++)
            {
                if (!def.TryGetValue(L, out var d) || d == null) continue;
                if (d.Pct.HasValue) e.Pct = (e.Pct ?? 0) + d.Pct.Value;
                if (d.Score.HasValue) e.Score = (e.Score ?? 0) + d.Score.Value;
                if (d.Basic.HasValue) e.Basic = (e.Basic ?? 0) + d.Basic.Value;
                if (d.RareChance.HasValue) e.RareChance = (e.RareChance ?? 0) + d.RareChance.Value;
                if (d.MulSub.HasValue) e.Mul = Math.Max(0.0, (e.Mul ?? 1.0) - d.MulSub.Value);
            }
            return e;
        }

        // ══════════════════════════════════════════════════════════════════════
        // symPerkMods 집계 결과 — 웹 engine.js symPerkMods() 반환 객체 그대로(전부 additive·기본 무효).
        // ══════════════════════════════════════════════════════════════════════
        public sealed class SymPerkModsResult
        {
            public readonly Dictionary<string, double> DeepTagMul = new Dictionary<string, double>();
            public double EmptyScore;
            public double EmptyExp;
            public double PenaltyMul = 1.0;
            public double SkullPenaltyMul = 1.0;
            public double BoundMaxDelta;
            public double BoundMinDelta;
            public double BoundMax;
            public double BoundMin;
            public double QuotaMul = 1.0;
            public double BossQuotaMul = 1.0;
            public double ScoreMul = 1.0;
            public double CompressScorePct;
            public readonly Dictionary<string, double> RepairMul = new Dictionary<string, double>();
            public double RepairRefundFrac;
            public double SwapCoin;
            public double PurifyCoin;
            public bool PurifyToBasic;
            public double RewardBonus;
            public double AddBasicDelta;
            public double RareChance;
            public double LegendWeight;
            public double CurseChance;
            public double ShopLabWeight;
            public bool AlwaysRepair;
            public double BossCopyN;
            public double AutoShredN;
            public double RareFirstScore;
            public bool LegendSeal;
            public double BalanceScore;
            public bool StatTable;
            public double TagCap;
            public readonly List<string> ScoreKinds = new List<string>();
        }

        // 웹 `symPerkMods(perkIds, pouch, levels)` 그대로 전사 — 일반 ModsBuilder.Build와 완전히
        // 분리된 순수 집계 함수(perkIds 중 SYM_PERK_BY_ID에 없는 id는 무시 — 일반 증강/유물/저주와
        // 같은 배열을 공유해도 안전한 이유).
        public static SymPerkModsResult ComputeMods(
            IReadOnlyList<string> perkIds, IReadOnlyDictionary<string, int> pouch, IReadOnlyDictionary<string, int> levels)
        {
            var outR = new SymPerkModsResult();
            if (perkIds == null) return outR;

            int total = Pouch.Total(pouch);
            string most = Pouch.MostCommonTag(pouch);

            void AddTag(string tag, double pct)
            {
                if (string.IsNullOrEmpty(tag)) return;
                outR.DeepTagMul[tag] = (outR.DeepTagMul.TryGetValue(tag, out var v) ? v : 0) + pct;
            }

            for (int i = 0; i < perkIds.Count; i++)
            {
                var perk = Get(perkIds[i]);
                if (perk == null || perk.eff == null) continue;
                int lvl = (levels != null && levels.TryGetValue(perkIds[i], out var lv)) ? lv : 1;
                var e = EffWithLevel(perk, lvl);

                switch (e.Hook)
                {
                    case "tagBuff":
                        AddTag(most, e.Pct ?? 0);
                        if (e.Other.HasValue && e.Other.Value != 0)
                        {
                            foreach (var t in Pouch.TagCounts(pouch).Keys)
                                if (t != most) AddTag(t, e.Other.Value);
                        }
                        if (e.TagCap.HasValue) outR.TagCap = Math.Max(outR.TagCap, e.TagCap.Value);
                        break;
                    case "emptyScore":
                        outR.EmptyScore += e.Score ?? 0;
                        break;
                    case "emptyExp":
                        outR.EmptyExp += e.Exp ?? 0;
                        break;
                    case "penaltyMul":
                    {
                        bool okLe = !e.WhenTotalLe.HasValue || total <= e.WhenTotalLe.Value;
                        bool okGe = !e.WhenTotalGe.HasValue || total >= e.WhenTotalGe.Value;
                        if (okLe && okGe) outR.PenaltyMul *= (e.Mul ?? 1);
                        break;
                    }
                    case "skullPenaltyMul":
                    {
                        bool okLe = !e.WhenTotalLe.HasValue || total <= e.WhenTotalLe.Value;
                        bool okGe = !e.WhenTotalGe.HasValue || total >= e.WhenTotalGe.Value;
                        if (okLe && okGe) outR.SkullPenaltyMul *= (e.Mul ?? 1);
                        break;
                    }
                    case "boundDelta":
                        outR.BoundMaxDelta += e.MaxDelta ?? 0;
                        outR.BoundMinDelta += e.MinDelta ?? 0;
                        if (e.SkullPenaltyMul.HasValue && (!e.WhenTotalGe.HasValue || total >= e.WhenTotalGe.Value))
                            outR.SkullPenaltyMul *= e.SkullPenaltyMul.Value;
                        break;
                    case "boundOverride":
                        if (e.Max.HasValue && e.Max.Value != 0) outR.BoundMax = Math.Max(outR.BoundMax, e.Max.Value);
                        if (e.Min.HasValue && e.Min.Value != 0) outR.BoundMin = outR.BoundMin != 0 ? Math.Min(outR.BoundMin, e.Min.Value) : e.Min.Value;
                        if (e.ExtraPenaltyLe.HasValue && total <= e.ExtraPenaltyLe.Value && e.ExtraMul.HasValue)
                            outR.PenaltyMul *= e.ExtraMul.Value;
                        if (e.QuotaMul.HasValue) outR.QuotaMul *= e.QuotaMul.Value; // (미사용 예약, 웹과 동일)
                        break;
                    case "compressScore":
                        if (!e.WhenTotalLe.HasValue || total <= e.WhenTotalLe.Value) outR.CompressScorePct += e.Pct ?? 0;
                        break;
                    case "repairMul":
                        if (e.Kinds != null)
                            foreach (var k in e.Kinds)
                                outR.RepairMul[k] = (outR.RepairMul.TryGetValue(k, out var m) ? m : 1) * (e.Mul ?? 1);
                        break;
                    case "repairRefund":
                        outR.RepairRefundFrac = Math.Max(outR.RepairRefundFrac, e.Frac ?? 0);
                        break;
                    case "swapCoin":
                        outR.SwapCoin += e.Coin ?? 0;
                        break;
                    case "purifyCoin":
                        outR.PurifyCoin += e.Coin ?? 0;
                        break;
                    case "purifyToBasic":
                        outR.PurifyToBasic = true;
                        if (e.ScoreMul.HasValue) outR.ScoreMul *= e.ScoreMul.Value;
                        if (e.CurseChance.HasValue) outR.CurseChance += e.CurseChance.Value;
                        break;
                    case "rewardBonus":
                        outR.RewardBonus += e.N ?? 0;
                        break;
                    case "addBoost":
                        outR.AddBasicDelta += e.Basic ?? 0;
                        if (e.RareChance.HasValue) outR.RareChance = Math.Max(outR.RareChance, e.RareChance.Value);
                        outR.LegendWeight += e.LegendWeight ?? 0;
                        if (e.CurseChance.HasValue) outR.CurseChance += e.CurseChance.Value;
                        if (e.BossQuotaMul.HasValue) outR.BossQuotaMul *= e.BossQuotaMul.Value;
                        break;
                    case "shopLab":
                        outR.ShopLabWeight += e.Weight ?? 0;
                        if (e.AlwaysRepair == true) outR.AlwaysRepair = true;
                        if (e.QuotaMul.HasValue) outR.QuotaMul *= e.QuotaMul.Value;
                        break;
                    case "bossCopy":
                        outR.BossCopyN += e.N ?? 0;
                        break;
                    case "autoShred":
                        outR.AutoShredN += e.N ?? 0;
                        break;
                    case "rareFirstScore":
                        outR.RareFirstScore += e.Score ?? 0;
                        break;
                    case "legendSeal":
                        outR.LegendSeal = true;
                        break;
                    case "score":
                        if (e.Kind == "balance") { outR.BalanceScore += e.Amount ?? 0; outR.ScoreKinds.Add("balance"); }
                        break;
                    case "display":
                        if (e.StatTable == true) outR.StatTable = true;
                        break;
                    default:
                        break;
                }
            }
            return outR;
        }

        // 심화 심볼퍽 보유분(도감/UI 표시용) — 웹 `_heldSymPerks()`. run.Perks 중 sa_/sp_/sr_만 필터.
        public static List<string> HeldFrom(IReadOnlyList<string> perkIds)
        {
            var list = new List<string>();
            if (perkIds == null) return list;
            for (int i = 0; i < perkIds.Count; i++) if (Get(perkIds[i]) != null) list.Add(perkIds[i]);
            return list;
        }
    }
}
