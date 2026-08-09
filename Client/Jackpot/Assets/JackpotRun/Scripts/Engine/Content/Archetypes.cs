using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 B) — 계열 아키타입(전공) 판정.
    // 웹 engine.js `ARCHETYPES`/`pouchArchetype`/`archetypeMods`(§2005-2058) 그대로 전사.
    // Content 계층(Run/RunState 비의존) — Pouch.cs(Total/FamilyCount)만 참조하는 순수 함수.
    //
    // 6계열: cherry🍒과수원 · book📘도서관장(태그:학습=book+tome) · gem💎보석상 · skull☠강령학파 ·
    // coin🪙조폐국 · flame🔥화력발전. 주머니 계열 비중(share=familyCount/총량)이 임계(ArchT1/T2)
    // 도달 시 발동 — 최대 share 계열 1개만 활성(단일 전공, 동률은 선언 순서 우선).
    //
    // [P7-2 당시 범위 밖 → 웹 파리티 P8에서 실배선 완료] hex_allornothing(⚡일발역전) 저주의 심화
    // 전용 dEff={archetypeMul:0.5}(전공 배율 절반) 재설계 — 웹 game.js:489-496. 저주/증강의 "dEff"
    // (deepMode 전용 효과 오버라이드) 범용 메커니즘 자체는 여전히 Unity 엔진에 없지만(ModsBuilder는
    // fx만 안다), hex_allornothing 1건뿐이라 범용화 대신 `DeepRunHooks.ApplyDeepMods`가 이 클래스가
    // 만든 ExpMul/ScoreMul/CoinMul을 곱하기 직전에 `run.Curses.Contains("hex_allornothing")`이면
    // 값을 절반으로 halve하는 국소 분기로 처리했다(WEB_PARITY_DESIGN.md §2 결정 로그 확정 항목③).
    public sealed class ArchetypeDef
    {
        public string Family, Ref, Emoji, Name, Metric;
    }

    // pouchArchetype 반환값 — 웹 `{ family, share, tier, e, n, metric }`. tier: 2(≥ArchT2)/1(≥ArchT1)/
    // 0(미달=비활성, family는 최근접 표시용으로 유지). 빈/총량0 주머니는 family=null.
    public sealed class ArchetypeState
    {
        public string Family;
        public double Share;
        public int Tier;
        public string Emoji;
        public string Name;
        public string Metric;
    }

    // archetypeMods 반환값 — 웹 `{ expMul, scoreMul, coinMul, skullPenaltyMul }`(활성 1계열만·비활성=빈맵).
    public sealed class ArchetypeModsResult
    {
        public readonly Dictionary<string, double> ExpMul = new Dictionary<string, double>();
        public readonly Dictionary<string, double> ScoreMul = new Dictionary<string, double>();
        public readonly Dictionary<string, double> CoinMul = new Dictionary<string, double>();
        public double SkullPenaltyMul = 1.0;
    }

    public static class Archetypes
    {
        // 웹 data.js DEEP.ARCH_T1/ARCH_T2 그대로.
        public const double ArchT1 = 0.25;
        public const double ArchT2 = 0.40;

        // 웹 engine.js ARCHETYPES 선언 순서 그대로(동률 tie-break 결정론 — cherry>book>gem>skull>coin>flame).
        public static readonly ArchetypeDef[] All =
        {
            new ArchetypeDef { Family = "cherry", Ref = "cherry",   Emoji = "🍒", Name = "과수원",   Metric = "exp" },
            new ArchetypeDef { Family = "book",   Ref = "tag:학습", Emoji = "📘", Name = "도서관장", Metric = "exp" },
            new ArchetypeDef { Family = "gem",    Ref = "gem",      Emoji = "💎", Name = "보석상",   Metric = "score" },
            new ArchetypeDef { Family = "skull",  Ref = "skull",    Emoji = "☠", Name = "강령학파", Metric = "exp" },
            new ArchetypeDef { Family = "coin",   Ref = "coin",     Emoji = "🪙", Name = "조폐국",   Metric = "coin" },
            new ArchetypeDef { Family = "flame",  Ref = "flame",    Emoji = "🔥", Name = "화력발전", Metric = "exp" },
        };

        public static readonly Dictionary<string, ArchetypeDef> ByFamily = BuildByFamily();
        private static Dictionary<string, ArchetypeDef> BuildByFamily()
        {
            var d = new Dictionary<string, ArchetypeDef>();
            for (int i = 0; i < All.Length; i++) d[All[i].Family] = All[i];
            return d;
        }

        // 최대 비중 아키타입 계열 1개 판정 — 웹 `pouchArchetype`.
        public static ArchetypeState PouchArchetype(IReadOnlyDictionary<string, int> pouch)
        {
            int total = Pouch.Total(pouch);
            if (total <= 0) return new ArchetypeState { Family = null, Share = 0, Tier = 0, Emoji = "", Name = "", Metric = "" };

            ArchetypeDef best = null;
            double bestShare = -1;
            for (int i = 0; i < All.Length; i++)
            {
                double share = (double)Pouch.FamilyCount(pouch, All[i].Ref) / total;
                if (share > bestShare) { bestShare = share; best = All[i]; }
            }
            double s = Math.Max(0, bestShare);
            int tier = s >= ArchT2 ? 2 : s >= ArchT1 ? 1 : 0;
            return new ArchetypeState { Family = best.Family, Share = s, Tier = tier, Emoji = best.Emoji, Name = best.Name, Metric = best.Metric };
        }

        // 아키타입 → mods 곱셈 증가분 — 웹 `archetypeMods`. 수치(스펙 배치G 그대로): exp/score 계열
        // +15/+30%, coin 계열 +10/+20%, 강령학파(skull) t2는 skullPenaltyMul 0.5 병행.
        public static ArchetypeModsResult ArchetypeMods(ArchetypeState arch)
        {
            var outR = new ArchetypeModsResult();
            if (arch == null || string.IsNullOrEmpty(arch.Family) || arch.Tier <= 0) return outR;
            string fam = arch.Family;
            bool t2 = arch.Tier >= 2;
            if (arch.Metric == "coin") outR.CoinMul[fam] = t2 ? 0.20 : 0.10;
            else if (arch.Metric == "score") outR.ScoreMul[fam] = t2 ? 0.30 : 0.15;
            else outR.ExpMul[fam] = t2 ? 0.30 : 0.15; // exp 계열(cherry/book/skull/flame)
            if (fam == "skull" && t2) outR.SkullPenaltyMul = 0.5;
            return outR;
        }
    }
}
