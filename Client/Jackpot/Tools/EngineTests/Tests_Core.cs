using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // S1 골든 테스트 — ENGINE_PORT_DESIGN.md "검증(Tools/EngineTests)" 절 카테고리 1/2(부분)/3.
    // 모든 기대값은 01_engine.md의 Kotlin 식을 이 코드와 "독립적으로" 재계산해 얻었다(테스트 대상인
    // Formulas.* 를 먼저 실행해서 그 결과를 베껴 넣는 방식은 순환 검증이라 금지 — 작업 지시서 규칙).
    // Program.cs가 이 파일의 `Tests_*` 정적 클래스를 전부 리플렉션으로 찾아 Run(TestCtx)을 호출한다.

    internal static class TestHelpers
    {
        public static void CollectionEq<T>(TestCtx t, IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
        {
            if (expected == null || actual == null)
            {
                t.True(expected == actual, label, "one side is null");
                return;
            }
            if (expected.Count != actual.Count)
            {
                t.Fail(label, $"length expected={expected.Count} actual={actual.Count}");
                return;
            }
            bool allEq = true;
            for (int i = 0; i < expected.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i])) { allEq = false; break; }
            }
            t.True(allEq, label, allEq ? null : $"expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
        }
    }

    // ── Rng — 01_engine.md §10/§11, 특히 "빈 컬렉션 = RNG 미소비" 규칙(§10.5) ──────
    internal static class Tests_Core_Rng
    {
        public static void Run(TestCtx t)
        {
            // PickOrDefault(빈 리스트)는 RNG를 전혀 소비하지 않아야 한다(Kotlin randomOrNull 규칙).
            // 증명 방법: 같은 시드로 시작한 두 Rng 중 하나는 PickOrDefault(empty)를 5번 부른 뒤 Next()를,
            // 다른 하나는 아무것도 안 하고 바로 Next()를 부른다 — 미소비라면 두 결과가 똑같아야 한다.
            var empty = new List<int>();
            var rngA = new Rng(42);
            for (int i = 0; i < 5; i++)
                t.Eq(0, rngA.PickOrDefault(empty), $"PickOrDefault(empty) returns default(int) [{i}]");
            int afterEmptyDraws = rngA.Next(1_000_000);

            var rngB = new Rng(42); // 동일 시드, empty PickOrDefault 호출 없음
            int noEmptyDraws = rngB.Next(1_000_000);

            t.Eq(noEmptyDraws, afterEmptyDraws,
                "PickOrDefault(empty) x5 이후 Next()가 신규 Rng의 첫 Next()와 일치 (RNG 미소비 증명)");

            // Pick(빈 리스트)는 예외(Kotlin random(rng)의 빈 컬렉션 동작).
            bool threw = false;
            try { new Rng(1).Pick(empty); }
            catch (InvalidOperationException) { threw = true; }
            t.True(threw, "Pick(empty) throws InvalidOperationException");

            // PickOrDefault(비어있지 않은 리스트)는 정확히 1회 소비 — 원소 1개 리스트는 항상 index 0을
            // 뽑으므로, "1회 소비 후"의 다음 값이 Next(1)을 한 번 부른 것과 동일해야 한다.
            var one = new List<string> { "x" };
            var rngC = new Rng(7);
            t.Eq("x", rngC.PickOrDefault(one), "PickOrDefault(1-element list) returns the element");
            int afterOneDraw = rngC.Next(1_000_000);

            var rngD = new Rng(7);
            rngD.Next(1); // 원소 1개 리스트에서의 소비량(1회)과 동일하게 흉내
            int afterManualDraw = rngD.Next(1_000_000);

            t.Eq(afterManualDraw, afterOneDraw, "PickOrDefault(1-element)는 정확히 1회 RNG를 소비한다");

            // Shuffle — 같은 시드면 같은 순열(결정적 재현성), 원소 멀티셋은 항상 보존.
            var listE = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            var listF = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            new Rng(123).Shuffle(listE);
            new Rng(123).Shuffle(listF);
            TestHelpers.CollectionEq(t, listE, listF, "Shuffle: same seed -> same permutation");

            var sortedE = new List<int>(listE);
            sortedE.Sort();
            TestHelpers.CollectionEq(t, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, sortedE, "Shuffle preserves element multiset");
        }
    }

    // ── quota(stage) — 01_engine.md §1.1 ────────────────────────────────────────
    internal static class Tests_Core_Quota
    {
        // 기대값 산출: stage 1~15는 QUOTAS 표(01_engine.md §1.1) 그대로.
        // stage 16+는 Kotlin quota()와 동일한 "반복 곱셈" 알고리즘(755.0에서 시작해 (stage-15)번 *1.2를
        // 순차 곱한 뒤 0-방향 절삭)을 이 코드와는 독립된 계산기(파이썬 인터프리터, IEEE754 double 연산
        // 순서 동일)로 재구현해 산출했다. Math.Pow 같은 거듭제곱 한방 계산은 절대 쓰지 않았다 —
        // 반복곱과 거듭제곱은 부동소수 반올림 경로가 달라 결과가 어긋날 수 있음(§11-5).
        // 전개 예시(16~20, 이후 21~40도 동일한 규칙으로 직전 값에 계속 *1.2):
        //   stage16: 755.0    * 1.2 = 906.0     -> 906
        //   stage17: 906.0    * 1.2 = 1087.2    -> 1087
        //   stage18: 1087.2   * 1.2 = 1304.64   -> 1304
        //   stage19: 1304.64  * 1.2 = 1565.568  -> 1565
        //   stage20: 1565.568 * 1.2 = 1878.6816 -> 1878
        private static readonly long[] Expected =
        {
            /*1*/ 110, /*2*/ 120, /*3*/ 130, /*4*/ 140, /*5*/ 150, /*6*/ 170, /*7*/ 200, /*8*/ 235, /*9*/ 280, /*10*/ 330,
            /*11*/ 390, /*12*/ 460, /*13*/ 540, /*14*/ 640, /*15*/ 755,
            /*16*/ 906, /*17*/ 1087, /*18*/ 1304, /*19*/ 1565, /*20*/ 1878,
            /*21*/ 2254, /*22*/ 2705, /*23*/ 3246, /*24*/ 3895, /*25*/ 4674,
            /*26*/ 5609, /*27*/ 6731, /*28*/ 8077, /*29*/ 9693, /*30*/ 11632,
            /*31*/ 13958, /*32*/ 16750, /*33*/ 20100, /*34*/ 24120, /*35*/ 28944,
            /*36*/ 34733, /*37*/ 41680, /*38*/ 50016, /*39*/ 60020, /*40*/ 72024,
        };

        public static void Run(TestCtx t)
        {
            for (int stage = 1; stage <= 40; stage++)
                t.Eq(Expected[stage - 1], Formulas.Quota(stage), $"quota(stage={stage})");

            // 경계: stage<=0 은 QUOTAS[0]=110 폴백 (01_engine.md 부록A-12, i<0 분기).
            t.Eq(110L, Formulas.Quota(0), "quota(stage=0) fallback");
            t.Eq(110L, Formulas.Quota(-5), "quota(stage=-5) fallback");
        }
    }

    // ── stageClearScore — 웹 파리티 P2(WEB_PARITY_DESIGN §2-B, 웹 engine.js:73-80) ─────────────────
    // s = stage*50 + leftoverExp*2 + leftSpins*100 (+500 if boss); toLong()(0-방향 절삭). 저주 배수
    // (옛 s*=(1+0.05*curses))는 웹에 대응 항목이 없어 제거됐다 — curses 인자는 시그니처 호환용으로만
    // 남는다(웹도 동일). 아래 A/B/E/H는 일부러 curses에 큰 값(99/50/7/42)을 넣어 "결과에 전혀 영향이
    // 없음"을 직접 증명한다(옛 코드였다면 값이 달라졌을 것 — 새 규칙이 옛 보너스를 더하지 않음을 검증).
    internal static class Tests_Core_StageClearScore
    {
        public static void Run(TestCtx t)
        {
            // A: stage=1,leftover=0,leftSpins=0,curses=99(고의로 큰 값),boss=false
            //   s=1*50=50; +0+0=50; boss없음 -> 50 (curses=99라도 무관)
            t.Eq(50L, Formulas.StageClearScore(1, 0, 0, 99, false), "case A (curses=99 무시)");

            // B: stage=5,leftover=20,leftSpins=2,curses=50(고의로 큰 값),boss=true
            //   s=5*50=250; +20*2=40->290; +2*100=200->490; +500(boss)->990 (curses=50이라도 무관)
            t.Eq(990L, Formulas.StageClearScore(5, 20, 2, 50, true), "case B (curses=50 무시)");

            // C: stage=10,leftover=0,leftSpins=5,curses=2,boss=false
            //   s=10*50=500; +0->500; +5*100=500->1000; boss없음 -> 1000(옛 코드라면 curses2×0.05=1100이었을 값)
            t.Eq(1000L, Formulas.StageClearScore(10, 0, 5, 2, false), "case C (옛 1100 아님)");

            // D: stage=15,leftover=100,leftSpins=3,curses=5,boss=true
            //   s=15*50=750; +100*2=200->950; +3*100=300->1250; +500->1750 (옛 코드라면 ×1.25=2187이었을 값)
            t.Eq(1750L, Formulas.StageClearScore(15, 100, 3, 5, true), "case D (옛 2187 아님)");

            // E: stage=20,leftover0,leftSpins0,curses=7(고의로 큰 값),boss=false -> s=1000 (curses 무관)
            t.Eq(1000L, Formulas.StageClearScore(20, 0, 0, 7, false), "case E (curses=7 무시)");

            // F: stage=8,leftover=55,leftSpins=1,curses=7,boss=false
            //   s=8*50=400; +55*2=110->510; +1*100=100->610; boss없음 -> 610(옛 코드라면 ×1.35=823이었을 값)
            t.Eq(610L, Formulas.StageClearScore(8, 55, 1, 7, false), "case F (옛 823 아님)");

            // G: stage=25,leftover=12,leftSpins=8,curses=1,boss=true
            //   s=25*50=1250; +12*2=24->1274; +8*100=800->2074; +500->2574 (옛 코드라면 ×1.05=2702이었을 값)
            t.Eq(2574L, Formulas.StageClearScore(25, 12, 8, 1, true), "case G (옛 2702 아님)");

            // H: stage=0, leftover/leftSpins=0, curses=42(고의로 큰 값) -> s=0 (curses 무관)
            t.Eq(0L, Formulas.StageClearScore(0, 0, 0, 42, false), "case H (stage 0 edge, curses=42 무시)");
        }
    }

    // ── capMulFor — 웹 파리티 P2로 삭제(WEB_PARITY_DESIGN §2-B) ─────────────────────────────────
    // 웹 engine.js에는 스핀 총배율 상한이 없다(Formulas.cs 주석 근거) — Formulas.CapMulFor 자체를
    // 삭제했으므로 이 클래스가 하던 단위 테스트도 함께 삭제한다. "캡이 실제로 더 이상 작동하지 않음"을
    // 증명하는 부정 어서션은 Evaluate() 레벨에서 더 의미가 크므로 Tests_RunNet.cs
    // Tests_RunNet_EvaluateMechanics.EndsMatchNoCap로 옮겨 확장했다(옛 이 클래스의 8개 어서션은
    // 그쪽의 새 어서션들로 상쇄).

    // ── tierForClearedStage / tierUp — 01_engine.md §1.5 ────────────────────────
    internal static class Tests_Core_TierCurve
    {
        public static void Run(TestCtx t)
        {
            // stage<=0 -> SILVER; %5==0 -> PRISM(최우선, when순서); %3==0(5의 배수 아닐때) -> GOLD; else SILVER
            t.Eq(Tier.SILVER, Formulas.TierForClearedStage(0), "stage=0");
            t.Eq(Tier.SILVER, Formulas.TierForClearedStage(-3), "stage=-3");
            t.Eq(Tier.SILVER, Formulas.TierForClearedStage(1), "stage=1");
            t.Eq(Tier.SILVER, Formulas.TierForClearedStage(2), "stage=2");
            t.Eq(Tier.GOLD, Formulas.TierForClearedStage(3), "stage=3");
            t.Eq(Tier.SILVER, Formulas.TierForClearedStage(4), "stage=4");
            t.Eq(Tier.PRISM, Formulas.TierForClearedStage(5), "stage=5");
            t.Eq(Tier.GOLD, Formulas.TierForClearedStage(6), "stage=6");
            t.Eq(Tier.GOLD, Formulas.TierForClearedStage(9), "stage=9");
            t.Eq(Tier.PRISM, Formulas.TierForClearedStage(10), "stage=10");
            t.Eq(Tier.GOLD, Formulas.TierForClearedStage(12), "stage=12 (%3==0,%5!=0)");
            // stage=15: %5==0 AND %3==0 -> PRISM 우선(when 순서, GOLD 분기 도달 안 함, 01_engine.md §1.5 명시)
            t.Eq(Tier.PRISM, Formulas.TierForClearedStage(15), "stage=15 (mult of 5 and 3 -> PRISM priority)");

            t.Eq(Tier.GOLD, Formulas.TierUp(Tier.SILVER), "tierUp(SILVER)");
            t.Eq(Tier.PRISM, Formulas.TierUp(Tier.GOLD), "tierUp(GOLD)");
            t.Eq(Tier.PRISM, Formulas.TierUp(Tier.PRISM), "tierUp(PRISM) stays PRISM");
        }
    }

    // ── accountExp / expToLevel / expForLevel — 01_engine.md §9.3 ──────────────
    internal static class Tests_Core_AccountExp
    {
        // 각 케이스의 기대 accountExp는 §9.3 식(①~⑥)을 독립 계산기(파이썬)로 재구현해 손으로 단계별
        // 산출한 값이다. 실제 482종 ACHIEVEMENTS는 S5 산출물이라 S1 시점에는 없다(S1은 S2/S5 콘텐츠
        // 파일을 참조하지 않는다) — Formulas.AccountExp의 achievements 매개변수는 선택적이며, 비워두면
        // ④ 컴포넌트가 0으로 계산된다(나머지 ①②③⑤⑥은 Kotlin과 완전히 동일하게 동작). case6은 ④
        // 컴포넌트 로직 자체를 검증하기 위한 합성(테스트 전용) 소규모 업적 목록을 사용한다.
        public static void Run(TestCtx t)
        {
            // case1: 빈 stat -> 모든 컴포넌트 0
            t.Eq(0L, Formulas.AccountExp(new Dictionary<string, long>()), "accountExp(empty)");
            t.Eq(1, Formulas.ExpToLevel(0), "expToLevel(0)");

            // case2: bestStage=5 -> ①마일스톤 bs>=3(+10)+bs>=5(+30)=40, 나머지 0.
            //   expToLevel(40)=1+floor(sqrt(40/22))=1+floor(sqrt(1.81818...))=1+floor(1.34840...)=1+1=2
            var s2 = new Dictionary<string, long> { ["bestStage"] = 5 };
            t.Eq(40L, Formulas.AccountExp(s2), "accountExp(bestStage=5)");
            t.Eq(2, Formulas.AccountLevel(s2), "accountLevel(bestStage=5)");

            // case3: bestStage=15(①10+30+80+150=270) + bossClears=20(②20*8=160→cap120) + runs=50(③50*3=150→cap90)
            //   exp = 270+120+90 = 480
            //   expToLevel(480)=1+floor(sqrt(480/22))=1+floor(sqrt(21.81818...))=1+floor(4.67128...)=1+4=5
            var s3 = new Dictionary<string, long> { ["bestStage"] = 15, ["bossClears"] = 20, ["runs"] = 50 };
            t.Eq(480L, Formulas.AccountExp(s3), "accountExp(bs15,boss20,runs50)");
            t.Eq(5, Formulas.AccountLevel(s3), "accountLevel(bs15,boss20,runs50)");

            // case4: ⑥숙련메달만 검증 — cstage_novice=15(GOLD100)+mstage_basic=10(SILVER50)
            //   +cstage_scholar=5(BRONZE20)+mstage_cherry=3(NONE0) = 170 (bestStage 없음→마일스톤0)
            var s4 = new Dictionary<string, long>
            {
                ["cstage_novice"] = 15, ["mstage_basic"] = 10, ["cstage_scholar"] = 5, ["mstage_cherry"] = 3,
            };
            t.Eq(170L, Formulas.AccountExp(s4), "accountExp(medals)");
            // expToLevel(170)=1+floor(sqrt(170/22))=1+floor(sqrt(7.72727...))=1+floor(2.77974...)=1+2=3
            t.Eq(3, Formulas.AccountLevel(s4), "accountLevel(medals)");

            // case5: ⑤빌드도감 — bc_/bld_ 접두이면서 값>0인 키만 40씩 가산. 접두 아닌 키는 값 커도 무시.
            var s5 = new Dictionary<string, long>
            {
                ["bc_novice_basic"] = 5, ["bc_farmer_cherry"] = 8, ["bld_x"] = 1, ["someOtherKey_not_matching"] = 100,
            };
            t.Eq(120L, Formulas.AccountExp(s5), "accountExp(bc/bld x3 = 120)");
            t.Eq(3, Formulas.AccountLevel(s5), "accountLevel(bc/bld)");

            // case6: ④업적 컴포넌트 — 합성(테스트 전용) 3종.
            //   achievements=[(cherryTotal,100,실버=50),(bossClears,5,골드=120),(jackpots,10,프리즘=250)]
            //   stat: cherryTotal=150(>=100 충족 +50), bossClears=3(<5 미충족 +0, 단 ②컴포넌트로는 3*8=24 별도 가산),
            //         jackpots=10(>=10 충족 +250)
            //   exp = ②(24) + ④(50+250=300) = 324
            //   expToLevel(324)=1+floor(sqrt(324/22))=1+floor(sqrt(14.72727...))=1+floor(3.83757...)=1+3=4
            var achievements = new List<(string key, long threshold, string tier)>
            {
                ("cherryTotal", 100L, "실버"),
                ("bossClears", 5L, "골드"),
                ("jackpots", 10L, "프리즘"),
            };
            var s6 = new Dictionary<string, long> { ["cherryTotal"] = 150, ["bossClears"] = 3, ["jackpots"] = 10 };
            t.Eq(324L, Formulas.AccountExp(s6, achievements), "accountExp(synthetic achievements)");
            t.Eq(4, Formulas.AccountLevel(s6, achievements), "accountLevel(synthetic achievements)");

            // expForLevel 역함수 — expForLevel(L)=ceil((L-1)^2*22), L<=1이면 0.
            t.Eq(0L, Formulas.ExpForLevel(1), "expForLevel(1)");
            t.Eq(22L, Formulas.ExpForLevel(2), "expForLevel(2) = ceil(1^2*22)");
            t.Eq(88L, Formulas.ExpForLevel(3), "expForLevel(3) = ceil(2^2*22)");
            t.Eq(352L, Formulas.ExpForLevel(5), "expForLevel(5) = ceil(4^2*22)");
            t.Eq(12672L, Formulas.ExpForLevel(25), "expForLevel(25) = ceil(24^2*22)");
            t.Eq(13750L, Formulas.ExpForLevel(26), "expForLevel(26) (함수 자체는 25 클램프 없음)");

            // expToLevel 경계값 — 문턱 바로 아래/위.
            t.Eq(1, Formulas.ExpToLevel(21), "expToLevel(21) just below level2 threshold(22)");
            t.Eq(2, Formulas.ExpToLevel(22), "expToLevel(22) exactly at level2 threshold");
            t.Eq(2, Formulas.ExpToLevel(87), "expToLevel(87) just below level3 threshold(88)");
            t.Eq(3, Formulas.ExpToLevel(88), "expToLevel(88) exactly at level3 threshold");
            t.Eq(24, Formulas.ExpToLevel(12671), "expToLevel(12671) just below level25 threshold");
            t.Eq(25, Formulas.ExpToLevel(12672), "expToLevel(12672) exactly at level25 threshold");
            t.Eq(25, Formulas.ExpToLevel(999999), "expToLevel clamps at 25");
            t.Eq(1, Formulas.ExpToLevel(-100), "expToLevel clamps negative exp to 0 -> level 1");
        }
    }

    // ── Symbols 14종 — 01_engine.md §2.2 ────────────────────────────────────────
    internal static class Tests_Core_Symbols
    {
        public static void Run(TestCtx t)
        {
            t.Eq(14, Symbols.Count, "Symbols.Count");
            t.Eq(14, Symbols.All.Length, "Symbols.All.Length");

            AssertSym(t, Sym.Cherry, "cherry", "🍒", "체리", 3, 0, 0, 25, Sp.NONE, false, new[] { "생명" });
            AssertSym(t, Sym.Book, "book", "📘", "책", 6, 0, 0, 18, Sp.NONE, false, new[] { "학습" });
            AssertSym(t, Sym.Star, "star", "⭐", "별", 8, 0, 0, 13, Sp.NONE, false, new[] { "콤보" });
            AssertSym(t, Sym.Gem, "gem", "💎", "보석", 1, 15, 0, 12, Sp.NONE, false, new[] { "점수" });
            AssertSym(t, Sym.Coin, "coin", "🪙", "코인", 0, 0, 1, 10, Sp.COIN, false, new[] { "코인" });
            AssertSym(t, Sym.Skull, "skull", "☠", "해골", 0, 0, 0, 10, Sp.SKULL, false, new[] { "저주" });
            AssertSym(t, Sym.Flame, "flame", "🔥", "불꽃", 0, 0, 0, 5, Sp.FLAME, false, new[] { "배율" });
            AssertSym(t, Sym.Magnet, "magnet", "🧲", "자석", 2, 0, 0, 4, Sp.MAGNET, false, new[] { "조작" });
            AssertSym(t, Sym.Bomb, "bomb", "💣", "폭탄", 5, 0, 0, 2, Sp.BOMB, false, new[] { "폭발" });
            AssertSym(t, Sym.Crown, "crown", "👑", "왕관", 20, 50, 0, 1, Sp.NONE, true, new[] { "왕관", "희귀" });
            AssertSym(t, Sym.Key, "key", "🗝", "열쇠", 6, 0, 0, 0, Sp.KEY, false, new[] { "열쇠" }, dormant: true);
            AssertSym(t, Sym.Dice, "dice", "🎲", "주사위", 0, 0, 0, 0, Sp.DICE, false, new[] { "운" }, dormant: true);
            AssertSym(t, Sym.Seed, "seed", "🌱", "씨앗", 0, 0, 0, 0, Sp.SEED, false, new[] { "생명", "성장" }, dormant: true);
            AssertSym(t, Sym.Wild, "wild", "🌀", "와일드", 0, 0, 0, 0, Sp.WILD, true, new[] { "희귀", "조작" }, dormant: true);

            // 선언 순서 = Kotlin SYMS 순서(§10.1 누적가중치 스캔에 영향) — 순서 자체도 검증.
            var expectedOrder = new[]
            {
                Sym.Cherry, Sym.Book, Sym.Star, Sym.Gem, Sym.Coin, Sym.Skull, Sym.Flame,
                Sym.Magnet, Sym.Bomb, Sym.Crown, Sym.Key, Sym.Dice, Sym.Seed, Sym.Wild,
            };
            for (int i = 0; i < expectedOrder.Length; i++)
                t.Eq(expectedOrder[i], Symbols.All[i].sym, $"Symbols.All[{i}].sym order");

            // VALUE_IDS (SlotV2Engine.kt L131) 순서까지 그대로.
            var expectedValueIds = new[] { Sym.Cherry, Sym.Star, Sym.Book, Sym.Gem, Sym.Crown };
            TestHelpers.CollectionEq(t, expectedValueIds, Symbols.ValueIds, "ValueIds");

            // SET_EXP / SET_SCORE (SlotV2Engine.kt L134-135)
            TestHelpers.CollectionEq(t, new[] { 0, 0, 8, 18, 42, 100 }, Symbols.SetExp, "SetExp");
            TestHelpers.CollectionEq(t, new[] { 0, 0, 3, 9, 24, 70 }, Symbols.SetScore, "SetScore");
        }

        private static void AssertSym(
            TestCtx t, Sym sym, string id, string emoji, string name,
            long exp, long score, long coin, int weight, Sp special, bool rare, string[] tags,
            bool dormant = false)
        {
            var s = Symbols.BySym(sym);
            t.Eq(id, s.id, $"{id}.id");
            t.Eq(emoji, s.emoji, $"{id}.emoji");
            t.Eq(name, s.name, $"{id}.name");
            t.Eq(exp, s.exp, $"{id}.exp");
            t.Eq(score, s.score, $"{id}.score");
            t.Eq(coin, s.coin, $"{id}.coin");
            t.Eq(weight, s.weight, $"{id}.weight");
            t.Eq(special, s.special, $"{id}.special");
            t.Eq(rare, s.rare, $"{id}.rare");
            t.Eq(dormant, s.dormant, $"{id}.dormant");
            TestHelpers.CollectionEq(t, tags, s.tags, $"{id}.tags");

            var byId = Symbols.ById(id);
            t.True(byId != null && byId.id == id, $"Symbols.ById(\"{id}\") resolves");
        }
    }

    // ── Bosses 4종 — 01_engine.md §1.2 ──────────────────────────────────────────
    internal static class Tests_Core_Bosses
    {
        public static void Run(TestCtx t)
        {
            t.Eq(4, Bosses.Count, "Bosses.Count");
            t.Eq(4, Bosses.All.Length, "Bosses.All.Length");

            AssertBoss(t, 0, "finals", "📝", "기말고사", "스핀+1·요구↑ · 막스핀 EXP ×2 · 첫스핀 -10%",
                1, 1.0, new[] { "막판형(복습·벼락치기)", "⏰최후" });
            AssertBoss(t, 1, "strict", "👨‍🏫", "꼰대교수", "스핀+1·요구↑ · 같은심볼 3개↑ 없는 스핀 ×0.5",
                1, 1.0, new[] { "세트·콤보(자석·체리)", "📌고정·📑복사" });
            AssertBoss(t, 2, "luck", "🎲", "운빨심판관", "스핀+1·요구↑ · ⭐👑🌀 있으면 ×1.8/없으면 ×0.8",
                1, 1.0, new[] { "⭐👑🌀 등장↑", "🔮예언·🎲올인" });
            AssertBoss(t, 3, "grad", "🎓", "졸업심사", "스핀+1·요구↑↑ (빡센 관문)",
                1, 1.15, new[] { "EXP 총량(과부하·탐욕)", "🚑응급처치·아이템" });

            // 순환: ((stage/5)-1) % 4 — stage 5,10,15,20 -> finals,strict,luck,grad ; 25 -> finals(순환 재시작).
            // stage=0은 stage%5==0 조건은 만족하지만 (0/5-1)=-1이 되어 원본에서도 정의역 밖(음수 인덱스) —
            // 실제 게임 스테이지는 1부터 시작하므로 이 경계는 검증하지 않는다(Kotlin도 동일하게 미정의).
            t.Eq("finals", Bosses.For(5)?.id, "Bosses.For(5)");
            t.Eq("strict", Bosses.For(10)?.id, "Bosses.For(10)");
            t.Eq("luck", Bosses.For(15)?.id, "Bosses.For(15)");
            t.Eq("grad", Bosses.For(20)?.id, "Bosses.For(20)");
            t.Eq("finals", Bosses.For(25)?.id, "Bosses.For(25) wraps around");
            t.True(Bosses.For(1) == null, "Bosses.For(1) non-boss stage -> null");
            t.True(Bosses.For(4) == null, "Bosses.For(4) non-boss stage -> null");
            t.True(Bosses.For(7) == null, "Bosses.For(7) non-boss stage -> null");

            t.Eq(1, Bosses.Spins(5), "Bosses.Spins(5)");
            t.Eq(0, Bosses.Spins(4), "Bosses.Spins(4) non-boss -> 0");
            t.EqTol(1.15, Bosses.QuotaMulFor(20), "Bosses.QuotaMulFor(20) grad");
            t.EqTol(1.0, Bosses.QuotaMulFor(4), "Bosses.QuotaMulFor(4) non-boss -> 1.0");
        }

        private static void AssertBoss(
            TestCtx t, int idx, string id, string emoji, string name, string desc,
            int bonusSpins, double quotaMul, string[] counterTags)
        {
            var b = Bosses.All[idx];
            t.Eq(id, b.id, $"Bosses.All[{idx}].id");
            t.Eq(emoji, b.emoji, $"{id}.emoji");
            t.Eq(name, b.name, $"{id}.name");
            t.Eq(desc, b.desc, $"{id}.desc");
            t.Eq(bonusSpins, b.bonusSpins, $"{id}.bonusSpins");
            t.EqTol(quotaMul, b.quotaMul, $"{id}.quotaMul");
            TestHelpers.CollectionEq(t, counterTags, b.counterTags, $"{id}.counterTags");

            var byId = Bosses.ById(id);
            t.True(byId != null && byId.id == id, $"Bosses.ById(\"{id}\")");
        }
    }

    // ── 머신 16종 + 최종 가중치표 — 01_engine.md §3 ─────────────────────────────
    internal static class Tests_Core_Machines
    {
        public static void Run(TestCtx t)
        {
            t.Eq(19, Machines.Count, "Machines.Count");
            t.Eq(19, Machines.All.Length, "Machines.All.Length");

            var ids = new HashSet<string>();
            int dup = 0;
            foreach (var m in Machines.All) if (!ids.Add(m.id)) dup++;
            t.Eq(0, dup, "Machines id duplicates");

            t.Eq("basic", Machines.Base.id, "Machines.Base == MACHINES[0]");

            // 최종 가중치표(base × weightMul + weightAdd) — 01_engine.md §3 표를 독립적으로 재전사해 대조.
            // base weight(Symbols): cherry25 book18 star13 gem12 coin10 skull10 flame5 magnet4 bomb2 crown1
            //                       key0 dice0 seed0 wild0
            AssertWeights(t, "basic", new Dictionary<Sym, double>());
            AssertWeights(t, "cherry", new Dictionary<Sym, double> { [Sym.Cherry] = 37.5, [Sym.Crown] = 0.6 });
            AssertWeights(t, "library", new Dictionary<Sym, double> { [Sym.Book] = 27.0, [Sym.Coin] = 6.0, [Sym.Gem] = 7.2 });
            AssertWeights(t, "gem", new Dictionary<Sym, double> { [Sym.Gem] = 20.4, [Sym.Book] = 10.8, [Sym.Cherry] = 15.0 });
            AssertWeights(t, "magnet", new Dictionary<Sym, double> { [Sym.Magnet] = 10.0 });
            AssertWeights(t, "skull", new Dictionary<Sym, double> { [Sym.Skull] = 18.0 });
            AssertWeights(t, "crown", new Dictionary<Sym, double> { [Sym.Crown] = 2.0, [Sym.Cherry] = 17.5, [Sym.Book] = 12.6 });
            AssertWeights(t, "flame", new Dictionary<Sym, double> { [Sym.Flame] = 9.0, [Sym.Skull] = 14.0 });
            AssertWeights(t, "bomb", new Dictionary<Sym, double> { [Sym.Bomb] = 5.0 });
            AssertWeights(t, "star", new Dictionary<Sym, double> { [Sym.Star] = 26.0, [Sym.Cherry] = 20.0 });
            AssertWeights(t, "clover", new Dictionary<Sym, double> { [Sym.Crown] = 1.3, [Sym.Coin] = 14.0, [Sym.Flame] = 6.5 });
            AssertWeights(t, "casino", new Dictionary<Sym, double> { [Sym.Dice] = 4.0 });
            AssertWeights(t, "garden", new Dictionary<Sym, double> { [Sym.Seed] = 4.0 });
            AssertWeights(t, "wildmac", new Dictionary<Sym, double> { [Sym.Wild] = 3.0 });
            AssertWeights(t, "vault", new Dictionary<Sym, double> { [Sym.Coin] = 15.0, [Sym.Key] = 3.0 });
            AssertWeights(t, "rainbow", new Dictionary<Sym, double>
            {
                [Sym.Crown] = 1.6, [Sym.Star] = 18.2, [Sym.Gem] = 15.6, [Sym.Cherry] = 15.0, [Sym.Book] = 10.8,
            });
            // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, data.js:188-190) — 신규 머신 3종 ──
            // nightmare: skull 10×2.2+2.0=24.0.  throne: crown 1×2.5+2.0=4.5.  broke: wmul/wadd 전부 빈값
            // (buildMods의 코인0·값심볼+2는 Mods.cs 별도 switch, 가중치표엔 영향 없음).
            AssertWeights(t, "nightmare", new Dictionary<Sym, double> { [Sym.Skull] = 24.0 });
            AssertWeights(t, "throne", new Dictionary<Sym, double> { [Sym.Crown] = 4.5 });
            AssertWeights(t, "broke", new Dictionary<Sym, double>());
        }

        // machineId에 대해 "명시적으로 달라지는 심볼"만 nonDefault로 받고, 나머지 심볼은 전부
        // base weight 그대로(mul=1,add=0)인지까지 14종 전수 검사한다.
        private static void AssertWeights(TestCtx t, string machineId, Dictionary<Sym, double> nonDefault)
        {
            var m = Machines.ById(machineId);
            t.True(m != null && m.id == machineId, $"Machines.ById(\"{machineId}\")");
            foreach (Sym sym in Enum.GetValues(typeof(Sym)))
            {
                double baseW = Symbols.BySym(sym).weight;
                double expected = nonDefault.TryGetValue(sym, out var v) ? v : baseW;
                double actual = Machines.FinalWeight(m, sym);
                t.EqTol(expected, actual, $"{machineId}.finalWeight[{sym}]");
            }
        }
    }

    // ── 캐릭터 19종(기존16+신규3) — 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, data.js:145-166) ──
    internal static class Tests_Core_Characters
    {
        public static void Run(TestCtx t)
        {
            t.Eq(19, Characters.Count, "Characters.Count");
            t.Eq(19, Characters.All.Length, "Characters.All.Length");

            var ids = new HashSet<string>();
            int dup = 0;
            foreach (var c in Characters.All) if (!ids.Add(c.id)) dup++;
            t.Eq(0, dup, "Characters id duplicates");

            t.Eq("novice", Characters.Base.id, "Characters.Base == CHARS[0]");

            // 대표 수치 스팟체크(전체는 §4 표 참조) — startCoins/scoreMod가 정확히 옮겨졌는지.
            AssertChar(t, "novice", 0.9, 0);
            AssertChar(t, "scholar", 1.0, 0);
            AssertChar(t, "parttime", 1.0, 15);
            AssertChar(t, "highroller", 1.1, 12);
            AssertChar(t, "cultist", 1.15, 0);
            AssertChar(t, "daredevil", 1.2, 0);
            AssertChar(t, "prodigy", 0.95, 0);
            // 신규 3종(data.js:163-165) — 전부 scoreMod 1.15·startCoins 0, unlockLevel로만 구분.
            AssertChar(t, "regent", 1.15, 0);
            AssertChar(t, "bankrupt", 1.15, 0);
            AssertChar(t, "abyss_scholar", 1.15, 0);
            t.Eq(8, Characters.ById("regent").unlockLevel, "regent.unlockLevel");
            t.Eq(12, Characters.ById("bankrupt").unlockLevel, "bankrupt.unlockLevel");
            t.Eq(16, Characters.ById("abyss_scholar").unlockLevel, "abyss_scholar.unlockLevel");

            // ── 웹 파리티 P3-4(§1-A #13, game.js:259-268 charUnlocked) — OR 5축, grandfather 폐기 ──
            // (기존 StatReq AND + cstage_ grandfather 테스트를 대체 — Characters.cs/Machines.cs 헤더
            // 각주 "웹은 grandfather 규칙이 없다" 결정 그대로).
            var gambler = Characters.ById("gambler");
            t.True(new PlayerProfile().IsCharUnlocked(gambler), "gambler always unlocked (data.js:148 — unlock 필드 없음)");

            var farmer = Characters.ById("farmer");
            var pFarmer = new PlayerProfile();
            t.True(!pFarmer.IsCharUnlocked(farmer), "farmer locked with empty profile");
            pFarmer.SetStat("runs", 2);
            t.True(pFarmer.IsCharUnlocked(farmer), "farmer unlocked via unlockRuns==2 (runs>=2)");
            var pFarmerAch = new PlayerProfile();
            pFarmerAch.AchievedIds.Add("cherry100");
            t.True(pFarmerAch.IsCharUnlocked(farmer), "farmer unlocked via unlockAch cherry100 (OR — either axis suffices)");

            var honor = Characters.ById("honor"); // unlockStage=8
            var pHonor = new PlayerProfile();
            t.True(!pHonor.IsCharUnlocked(honor), "honor locked below stage 8");
            pHonor.SetMax("bestStage", 8);
            t.True(pHonor.IsCharUnlocked(honor), "honor unlocked via unlockStage==8");

            var regent = Characters.ById("regent"); // unlockLevel=8
            var pRegent = new PlayerProfile();
            t.True(!pRegent.IsCharUnlocked(regent), "regent locked below level 8");
            pRegent.PlayerLevel = 8;
            t.True(pRegent.IsCharUnlocked(regent), "regent unlocked at PlayerLevel 8");
        }

        private static void AssertChar(TestCtx t, string id, double scoreMod, int startCoins)
        {
            var c = Characters.ById(id);
            t.True(c != null && c.id == id, $"Characters.ById(\"{id}\")");
            t.EqTol(scoreMod, c.scoreMod, $"{id}.scoreMod");
            t.Eq(startCoins, c.startCoins, $"{id}.startCoins");
        }
    }

    // ── catalog.json ↔ char_/mac_ id 교차 대조 ───────────────────────────────────
    internal static class Tests_Core_CatalogCrossCheck
    {
        public static void Run(TestCtx t)
        {
            string catalogPath = Path.Combine(
                t.RepoRoot, "Client", "Jackpot", "Assets", "JackpotRun", "Resources", "JackpotRun", "catalog.json");

            if (!File.Exists(catalogPath))
            {
                t.Fail("catalog.json exists", "not found at " + catalogPath);
                return;
            }

            string json = File.ReadAllText(catalogPath, Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);
            var entries = doc.RootElement.GetProperty("entries");

            var catalogCharIds = new HashSet<string>();
            var catalogMacIds = new HashSet<string>();
            foreach (var e in entries.EnumerateArray())
            {
                string category = e.GetProperty("category").GetString();
                string id = e.GetProperty("id").GetString();
                if (category == "char" && id != null && id.StartsWith("char_", StringComparison.Ordinal))
                    catalogCharIds.Add(id.Substring("char_".Length));
                else if (category == "mac" && id != null && id.StartsWith("mac_", StringComparison.Ordinal))
                    catalogMacIds.Add(id.Substring("mac_".Length));
            }

            var engineCharIds = new HashSet<string>();
            foreach (var c in Characters.All) engineCharIds.Add(c.id);
            var engineMacIds = new HashSet<string>();
            foreach (var m in Machines.All) engineMacIds.Add(m.id);

            t.Eq(16, catalogCharIds.Count, "catalog char_ id count");
            t.Eq(16, catalogMacIds.Count, "catalog mac_ id count");

            // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14) — 신규 캐릭3·머신3은 manifest.json에 아직
            // 아트가 없어(이모지 폴백) catalog.json에 대응 항목이 없다. catalog-only=죽은 참조는 여전히
            // 0을 기대. Opus 2차검수 웹 이탈 정리⑨ — engine-only를 "무제한 허용"하면 앞으로 catalog.json
            // 갱신을 깜빡한 새 콘텐츠도 영영 못 잡는다 — 이번 슬라이스가 만든 신규분만 명시 allowlist로 좁힌다.
            var expectedNewCharIds = new HashSet<string> { "regent", "bankrupt", "abyss_scholar" };
            var expectedNewMacIds = new HashSet<string> { "nightmare", "throne", "broke" };
            ReportAndAssertCatalogSubset(t, "char", engineCharIds, catalogCharIds, expectedNewCharIds);
            ReportAndAssertCatalogSubset(t, "mac", engineMacIds, catalogMacIds, expectedNewMacIds);
        }

        private static void ReportAndAssertCatalogSubset(TestCtx t, string label, HashSet<string> engineIds, HashSet<string> catalogIds, HashSet<string> allowedEngineOnly)
        {
            var engineOnly = new List<string>();
            foreach (var id in engineIds) if (!catalogIds.Contains(id)) engineOnly.Add(id);
            var catalogOnly = new List<string>();
            foreach (var id in catalogIds) if (!engineIds.Contains(id)) catalogOnly.Add(id);

            engineOnly.Sort(StringComparer.Ordinal);
            catalogOnly.Sort(StringComparer.Ordinal);

            t.Report($"{label} engine-only ids (신규·아트 없음 — 정상)", engineOnly.Count == 0 ? "(none)" : string.Join(", ", engineOnly));
            t.Report($"{label} catalog-only ids", catalogOnly.Count == 0 ? "(none)" : string.Join(", ", catalogOnly));

            t.True(
                catalogOnly.Count == 0,
                $"{label}_ catalog ⊆ engine id set",
                $"catalog-only(죽은 참조)=[{string.Join(",", catalogOnly)}]");

            var unexpected = engineOnly.Where(id => !allowedEngineOnly.Contains(id)).ToList();
            t.True(
                unexpected.Count == 0,
                $"{label}_ engine-only id ⊆ P3-4 신규 allowlist",
                $"예상 밖 engine-only=[{string.Join(",", unexpected)}]");
        }
    }
}
