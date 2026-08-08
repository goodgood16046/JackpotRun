using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 승천(심화 학기) A1~A10 — 웹 파리티 P6(WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:121-141
    // ascMods(asc)/ASC_RULE 전사). asc 0 = 일반. "더 강해진 게 아니라 더 어려운 룰을 이긴다" — 누적
    // 난이도 상승 + 점수 보정. Core는 Content(Symbols/Devices 등)에 의존하지 않는다(Formulas.cs와 동일
    // 원칙 — 순수 숫자 공식만 여기 둔다. 실제 Mods/RunState에 적용하는 배선은 Engine/Run 계층 소관).
    public static class AscMods
    {
        public const int AscMax = 10;

        // 웹 game.js:124-135 ascMods(asc) 그대로 — 6축.
        //   quotaMul       요구 EXP ×(1+0.08a)
        //   bossQuotaMul   A4+ 보스 요구 EXP 추가 ×(1+0.06(a-3))
        //   shopPriceMul   A3+ 상점가 ×(1+0.12(a-2))
        //   itemCapDelta   A5+ 아이템 보유칸 -1
        //   startCoinDelta A3+ 시작 코인 -min(16,(a-2)×4)
        //   scoreMul       점수 ×(1+0.12a) — A10 ≈ ×2.2
        public readonly struct Result
        {
            public readonly int A;
            public readonly double QuotaMul;
            public readonly double BossQuotaMul;
            public readonly double ShopPriceMul;
            public readonly int ItemCapDelta;
            public readonly int StartCoinDelta;
            public readonly double ScoreMul;

            public Result(int a, double quotaMul, double bossQuotaMul, double shopPriceMul,
                int itemCapDelta, int startCoinDelta, double scoreMul)
            {
                A = a;
                QuotaMul = quotaMul;
                BossQuotaMul = bossQuotaMul;
                ShopPriceMul = shopPriceMul;
                ItemCapDelta = itemCapDelta;
                StartCoinDelta = startCoinDelta;
                ScoreMul = scoreMul;
            }
        }

        // 웹 game.js:125 `const a = Math.max(0, Math.min(ASC_MAX, Math.floor(asc || 0)));` 그대로.
        public static int Clamp(int asc)
        {
            if (asc < 0) return 0;
            if (asc > AscMax) return AscMax;
            return asc;
        }

        public static Result Get(int asc)
        {
            int a = Clamp(asc);
            double quotaMul = 1.0 + 0.08 * a;
            double bossQuotaMul = a >= 4 ? 1.0 + 0.06 * (a - 3) : 1.0;
            double shopPriceMul = a >= 3 ? 1.0 + 0.12 * (a - 2) : 1.0;
            int itemCapDelta = a >= 5 ? -1 : 0;
            int startCoinDelta = a >= 3 ? -System.Math.Min(16, (a - 2) * 4) : 0;
            double scoreMul = 1.0 + 0.12 * a;
            return new Result(a, quotaMul, bossQuotaMul, shopPriceMul, itemCapDelta, startCoinDelta, scoreMul);
        }

        // 웹 game.js:137-141 ASC_RULE — 승천 단계별 "이번에 추가되는 규칙" 안내(표시 전용, 누적 아님 —
        // 웹 ascSelector()가 "이번 단계: " + ASC_RULE[selAsc] 형태로 그 단계 하나만 보여준다).
        // Opus 2차검수(P6) — [2]="요구 EXP 추가 +"는 뒤가 잘린 것처럼 보이지만 웹 `data.js` ASC_RULE[2]
        // 원문 자체가 이 문구로 끝난다(오타/미완성으로 추정되나 웹 소스 그대로) — §0 "웹 채택이 기본"
        // 원칙에 따라 임의로 문장을 완성하지 않고 원문을 그대로 보존한다.
        public static readonly IReadOnlyDictionary<int, string> RuleText = new Dictionary<int, string>
        {
            [1] = "요구 EXP +8%",
            [2] = "요구 EXP 추가 +",
            [3] = "상점 가격 상승",
            [4] = "보스 요구 EXP 상승",
            [5] = "아이템 보유칸 -1",
            [6] = "요구·상점 더 상승",
            [7] = "시작 코인 감소",
            [8] = "전 구간 난이도 상승",
            [9] = "고점 요구",
            [10] = "극한 난이도",
        };

        public static string RuleOf(int asc) => RuleText.TryGetValue(Clamp(asc), out var r) ? r : "";
    }
}
