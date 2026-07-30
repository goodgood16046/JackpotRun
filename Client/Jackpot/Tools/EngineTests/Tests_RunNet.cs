using System;
using System.Collections.Generic;
using System.Linq;
using JackpotRun.Engine;

namespace JackpotRun.EngineTests
{
    // S3 회귀 그물망 보강 — Opus 1차검수 [중요-1] 반영: "S3 코드는 정확함이 확인됐지만 회귀 테스트가
    // 부족하다"는 지적에 따라, 기존 Tests_Run.cs(스핀 재현성·Mods 대표퍽5·클리어/폭망 경로·미니시뮬)가
    // 다루지 않은 8개 영역을 추가로 커버한다:
    //   1) Mods 가산 3계열(weightAdd/tagExpBonus/perSymbolScore) 누적 규칙
    //   2) buildMods ctx 조건부 11종 중 Tests_Run.cs가 다루지 않은 나머지(sacrifice 제외 10종)
    //   3) ApplyItemMods 대표5종 + dev_coin(Devices.fx 단일소스) + ApplyPassiveDevice 4종 + ARMED 통과
    //   4) 저주 스택 임계 3/5/7 중첩, minimalist relic 3/4 경계, Mods.Clone() 딕셔너리 독립성
    //   5) Evaluate() 세부 메커닉(폭탄/자석/세트3·4·잭팟/인접쌍/양끝/해골/총배율캡/rareBurst)
    //   6) ApplyBoss 4종 각각의 정수 나눗셈(내림) 결과
    //   7) QuotaOf/EffSpins/CmdCoinCost + 특수 스핀모드 4종 대표경로 + 거부 3경로 + PHASE_NOT_SPIN 가드
    //   8) StageFlow: fate_bell 회생 경계, 클리어 보상 수치, Growth/SnowStack 갱신, RollNextNodes 분기
    //
    // 기대값 산출 원칙(순환 검증 금지 — S1/S3 규칙과 동일): 이 파일의 숫자는 전부
    //   (a) kotlin-reference\game\SlotV2Engine.kt(buildMods L1730-2026·evaluate L2131-2356·applyBoss L92-106·
    //       spinsPerStage/cmdCoinCost L2364-2381)·SlotV2Service.kt(handleSpin/clearStage/handleFailure) 및
    //   (b) Docs\EngineSpec\01_engine.md·02_service.md
    //   을 근거로 손으로 재계산했다. 부동소수(double) 곱셈이 얽혀 반올림 경로가 애매한 값(예: QuotaOf의
    //   보스 배수 체인, rareBurst 배율 곱)은 Tests_Core.cs가 세운 선례(§quota 16+ 주석)와 동일하게
    //   "독립 계산기"(이 저장소 밖의 Python, IEEE754 double 연산 순서 동일)로 한 번 더 검산했다 — 각
    //   섹션 주석에 "python 검산"이라고 표기된 값이 그것이다. Formulas/Mods/SpinResolver/StageFlow의
    //   실행 결과를 베껴 넣지 않았다(테스트 대상 코드로 기대값을 만들면 순환 검증이 되어 버그를
    //   못 잡는다 — S1 규칙과 동일하게 적용).
    //
    // 셀 구성 규약: Evaluate() 관련 테스트는 SpinResolver.Evaluate(rng, cells, mods, spinIndex, spinsPerStage,
    // flamePenalty, capMul)를 직접 호출해 셀을 손으로 구성한다(RollRaw의 RNG 가중치 추첨을 우회) — 이러면
    // "이 셀 조합이 나오면 정확히 이 숫자가 나온다"를 결정론적으로 고정할 수 있다. 대부분 capMul=0으로
    // 넘겨 총배율 캡을 비활성화한 채 개별 메커닉만 분리 검증하고, 캡 자체는 전용 테스트에서 별도로 켠다.
    // dice 심볼은 어떤 셀 구성에도 넣지 않았다(rng.Next(12) 소비가 결과에 섞이면 결정론이 깨짐).
    //
    // 스핀모드/StageFlow 테스트는 RunState.LockedNext(예언/타임라인 확정 경로, §2 step16)를 이용해
    // RollRaw의 RNG 소비 없이 셀을 고정한다 — 이 필드는 RunState의 정식 public API이므로 "내부 리팩터링"이
    // 아니라 엔진이 원래 제공하는 결정론화 통로를 그대로 쓰는 것이다.

    // ── ① Mods 가산 3계열 — weightAdd(프리즘 휴면심볼 주입)·tagExpBonus·perSymbolScore ────────────────
    internal static class Tests_RunNet_ModsAdditive
    {
        public static void Run(TestCtx t)
        {
            WeightAdd(t);
            TagExpBonus(t);
            PerSymbolScore(t);
        }

        // weightAdd — Wadd 헬퍼(Mods.cs L384-385)는 "기존값(없으면 0.0) + v" 누적. 휴면심볼(seed/wild/
        // key/dice, weight=0)을 실제로 풀에 등장시키는 유일한 경로(§01_engine.md 부록, weightAdd.* 주석).
        private static void WeightAdd(TestCtx t)
        {
            // 단일 소스: cursed_skulls(저주) — weightAdd.skull=4.0·flatExp=-4·skullExp=8 (Perks.cs 저주 L645).
            var mCurse = ModsBuilder.Build("basic", "", new List<string>(), new List<string> { "cursed_skulls" }, "");
            t.EqTol(4.0, mCurse.weightAdd.TryGetValue(Sym.Skull, out var wa1) ? wa1 : 0.0, "[wadd] cursed_skulls: weightAdd[skull]=4.0");
            t.Eq(-4, mCurse.flatExp, "[wadd] cursed_skulls: flatExp=-4");
            t.Eq(8, mCurse.skullExp, "[wadd] cursed_skulls: skullExp=8");

            // 누적: seed_garden(weightAdd.seed=5.0) + great_harvest(weightAdd.seed=5.0·perSymbolExp.cherry=3)
            // → weightAdd[seed]=10.0(가산 누적), perSymbolExp[cherry]=3(great_harvest만 기여).
            var mAccum = ModsBuilder.Build("basic", "", new List<string> { "seed_garden", "great_harvest" }, new List<string>(), "");
            t.EqTol(10.0, mAccum.weightAdd.TryGetValue(Sym.Seed, out var wa2) ? wa2 : 0.0, "[wadd] seed_garden+great_harvest: weightAdd[seed]=5.0+5.0=10.0 누적");
            t.Eq(3, mAccum.perSymbolExp.TryGetValue(Sym.Cherry, out var pse) ? pse : 0, "[wadd] great_harvest만 perSymbolExp[cherry]=3 기여");
        }

        // tagExpBonus — 태그(예: "학습") 1개당 가산. 누적은 문자열 키 딕셔너리(Mods.cs L367 tag 헬퍼).
        private static void TagExpBonus(TestCtx t)
        {
            // 누적: study_tag(tagExpBonus.학습=4) + library(tagExpBonus.학습=3·perSymbolExp.book=4)
            var m = ModsBuilder.Build("basic", "", new List<string> { "study_tag", "library" }, new List<string>(), "");
            t.Eq(7, m.tagExpBonus.TryGetValue("학습", out var teb) ? teb : 0, "[tagExp] study_tag+library: tagExpBonus[학습]=4+3=7");
            t.Eq(4, m.perSymbolExp.TryGetValue(Sym.Book, out var pse) ? pse : 0, "[tagExp] library: perSymbolExp[book]=4");

            // 다른 태그 계열: thorny_path(저주) — tagExpBonus.저주=6·weightAdd.skull=3.0·skullExp=-5·clearCoinBonus=4.
            var mCurse = ModsBuilder.Build("basic", "", new List<string>(), new List<string> { "thorny_path" }, "");
            t.Eq(6, mCurse.tagExpBonus.TryGetValue("저주", out var teb2) ? teb2 : 0, "[tagExp] thorny_path: tagExpBonus[저주]=6");
            t.EqTol(3.0, mCurse.weightAdd.TryGetValue(Sym.Skull, out var wa) ? wa : 0.0, "[tagExp] thorny_path: weightAdd[skull]=3.0(부수)");
            t.Eq(-5, mCurse.skullExp, "[tagExp] thorny_path: skullExp=-5");
            t.Eq(4, mCurse.clearCoinBonus, "[tagExp] thorny_path: clearCoinBonus=4");
        }

        // perSymbolScore — pss 헬퍼(Mods.cs L390-391), perSymbolExp와 동일한 누적 규칙의 별도 딕셔너리.
        private static void PerSymbolScore(TestCtx t)
        {
            // 누적: gem_polish(pss.gem=10) + gem_invest(pss.gem=25) → perSymbolScore[gem]=35.
            var m = ModsBuilder.Build("basic", "", new List<string> { "gem_polish", "gem_invest" }, new List<string>(), "");
            t.Eq(35, m.perSymbolScore.TryGetValue(Sym.Gem, out var pss) ? pss : 0, "[pss] gem_polish+gem_invest: perSymbolScore[gem]=10+25=35");

            // crown_seek — symbolWeightMul.crown=2.0·perSymbolScore.crown=30 (동시에 wmul 검증도 겸함).
            var mCrown = ModsBuilder.Build("basic", "", new List<string> { "crown_seek" }, new List<string>(), "");
            t.Eq(30, mCrown.perSymbolScore.TryGetValue(Sym.Crown, out var pssC) ? pssC : 0, "[pss] crown_seek: perSymbolScore[crown]=30");
            t.EqTol(2.0, mCrown.symbolWeightMul.TryGetValue(Sym.Crown, out var wm) ? wm : 1.0, "[pss] crown_seek: symbolWeightMul[crown]=1.0(basic 기본)×2.0=2.0");
        }
    }

    // ── ② ctx 조건부 11종 중 Tests_Run.cs 미커버 10종(sacrifice는 기존 테스트에서 검증됨) ───────────────
    // 전부 ModsBuilder.Build(...,ctx)로 호출해 Mods.cs의 ApplyCtxConditionalPerk 분기를 직접 검증한다.
    // charId=""(무매치)·machineId="basic"(weightMul/weightAdd 빈맵)로 캐릭터/머신 간섭을 차단하고,
    // curseIds=[]로 저주 스택 임계(§3-1) 간섭도 차단한다 — RunCtx.curseCount/boss는 curseIds와 무관한
    // 별도 필드라 black_diploma/fate_burst 테스트는 ctx 필드만 조작하면 된다.
    internal static class Tests_RunNet_CtxConditional
    {
        public static void Run(TestCtx t)
        {
            GrowthLogSnowballClamp(t);
            EarlyPrepEarlyAdapt(t);
            LateFocusSpinsLeft(t);
            CliffFocusBoundary(t);
            FortuneCheckLuckAccum(t);
            FateBurstBossBranch(t);
            BlackDiplomaThreshold(t);
        }

        private static Mods BuildOne(string perkId, RunCtx ctx) =>
            ModsBuilder.Build("basic", "", new List<string> { perkId }, new List<string>(), "", ctx);

        // growth_log: firstSpinExpMul *= 1+0.08*clamp(growthStack,0,5). snowball: expMul *= 1+0.12*clamp(snowStack,0,4).
        // 스택 0·3·(각자 최대)·9(최대초과, 클램프 확인)를 각각 검사 — 9는 두 경우 모두 최대치 결과와 동일해야 함.
        private static void GrowthLogSnowballClamp(TestCtx t)
        {
            foreach (var stack in new[] { 0, 3, 5, 9 })
            {
                var ctx = new RunCtx { growthStack = stack };
                var m = BuildOne("growth_log", ctx);
                int clamped = Math.Min(stack, 5);
                double expected = 1.0 + 0.08 * clamped;
                t.EqTol(expected, m.firstSpinExpMul, $"[growth_log] growthStack={stack}(clamp→{clamped}): firstSpinExpMul=1+0.08×{clamped}");
            }

            foreach (var stack in new[] { 0, 3, 4, 9 })
            {
                var ctx = new RunCtx { snowStack = stack };
                var m = BuildOne("snowball", ctx);
                int clamped = Math.Min(stack, 4);
                double expected = 1.0 + 0.12 * clamped;
                t.EqTol(expected, m.expMul, $"[snowball] snowStack={stack}(clamp→{clamped}): expMul=1+0.12×{clamped}");
            }
        }

        // early_prep: if (stage in 1..3) expMul*=1.15 — stage 0(미충족,하한 밖)/1(경계)/3(경계)/4(미충족,상한 밖).
        // early_adapt: if (stage in 1..5) expMul*=1.12 — stage 1/5(경계)/6(미충족).
        private static void EarlyPrepEarlyAdapt(TestCtx t)
        {
            var expected = new (int stage, bool fires)[] { (0, false), (1, true), (3, true), (4, false) };
            foreach (var (stage, fires) in expected)
            {
                var m = BuildOne("early_prep", new RunCtx { stage = stage });
                t.EqTol(fires ? 1.15 : 1.0, m.expMul, $"[early_prep] stage={stage}: expMul={(fires ? "1.15(발동)" : "1.0(미발동)")}");
            }

            var expected2 = new (int stage, bool fires)[] { (1, true), (5, true), (6, false) };
            foreach (var (stage, fires) in expected2)
            {
                var m = BuildOne("early_adapt", new RunCtx { stage = stage });
                t.EqTol(fires ? 1.12 : 1.0, m.expMul, $"[early_adapt] stage={stage}: expMul={(fires ? "1.12(발동)" : "1.0(미발동)")}");
            }
        }

        // late_focus: if (spinsLeft in 1..2) expMul*=1.10 — SpinsLeft=Max(spinsPerStage-spinIndex,0).
        // spinsPerStage=5 고정, spinIndex를 바꿔 SpinsLeft=3/2/1/0을 각각 재현.
        private static void LateFocusSpinsLeft(TestCtx t)
        {
            var cases = new (int spinsLeft, int spinIndex, bool fires)[]
            {
                (3, 2, false), (2, 3, true), (1, 4, true), (0, 5, false),
            };
            foreach (var (spinsLeft, spinIndex, fires) in cases)
            {
                var ctx = new RunCtx { spinsPerStage = 5, spinIndex = spinIndex };
                t.Eq(spinsLeft, ctx.SpinsLeft, $"[late_focus] 사전조건 SpinsLeft(spinIndex={spinIndex})={spinsLeft}");
                var m = BuildOne("late_focus", ctx);
                t.EqTol(fires ? 1.10 : 1.0, m.expMul, $"[late_focus] SpinsLeft={spinsLeft}: expMul={(fires ? "1.10(발동)" : "1.0(미발동)")}");
            }
        }

        // cliff_focus: if (isLastSpin && quota>0 && stageExp < (quota*0.6).toLong()) lastSpinExpMul*=1.8.
        // quota=100 고정 → 절삭 경계 threshold=(long)(100*0.6)=60. stageExp=59(경계-1,발동)/60(경계,미발동,
        // 조건이 엄격 '<' 이므로 정확히 threshold와 같으면 미발동)/61(경계+1,미발동).
        private static void CliffFocusBoundary(TestCtx t)
        {
            var ctxCheck = new RunCtx { spinsPerStage = 5, spinIndex = 4 };
            t.True(ctxCheck.IsLastSpin, "[cliff_focus] 사전조건: spinsPerStage=5,spinIndex=4 → IsLastSpin=true");

            var cases = new (long stageExp, bool fires)[] { (59, true), (60, false), (61, false) };
            foreach (var (stageExp, fires) in cases)
            {
                var ctx = new RunCtx { spinsPerStage = 5, spinIndex = 4, quota = 100, stageExp = stageExp };
                var m = BuildOne("cliff_focus", ctx);
                t.EqTol(fires ? 1.8 : 1.0, m.lastSpinExpMul, $"[cliff_focus] quota=100,threshold=60,stageExp={stageExp}: lastSpinExpMul={(fires ? "1.8(발동)" : "1.0(미발동)")}");
            }

            // isLastSpin이 아니면 stageExp가 threshold 밑이어도 미발동(조건 AND의 첫 항).
            var ctxNotLast = new RunCtx { spinsPerStage = 5, spinIndex = 0, quota = 100, stageExp = 0 };
            var mNotLast = BuildOne("cliff_focus", ctxNotLast);
            t.EqTol(1.0, mNotLast.lastSpinExpMul, "[cliff_focus] IsLastSpin=false면 stageExp가 아무리 낮아도 미발동");
        }

        // fortune_check: if (isFirstSpin) rareWeightMul*=1.2. luck_accum: if (unluckyGauge>=3) rareWeightMul*=1.3.
        private static void FortuneCheckLuckAccum(TestCtx t)
        {
            var mFirst = BuildOne("fortune_check", new RunCtx { spinIndex = 0 });
            t.EqTol(1.2, mFirst.rareWeightMul, "[fortune_check] spinIndex=0(첫스핀): rareWeightMul=1.2");
            var mNotFirst = BuildOne("fortune_check", new RunCtx { spinIndex = 1 });
            t.EqTol(1.0, mNotFirst.rareWeightMul, "[fortune_check] spinIndex=1(첫스핀아님): rareWeightMul=1.0(미발동)");

            var gaugeCases = new (int gauge, bool fires)[] { (2, false), (3, true), (4, true) };
            foreach (var (gauge, fires) in gaugeCases)
            {
                var m = BuildOne("luck_accum", new RunCtx { unluckyGauge = gauge });
                t.EqTol(fires ? 1.3 : 1.0, m.rareWeightMul, $"[luck_accum] unluckyGauge={gauge}: rareWeightMul={(fires ? "1.3(발동)" : "1.0(미발동)")}");
            }
        }

        // fate_burst: rareBurstExpMul *= (boss?1.7:1.8) — 조건 없이 buildMods 시점에 무조건 대입(배율만
        // ctx.boss로 분기). rareBurstScoreMul *= 1.5는 boss 여부와 무관하게 항상 적용.
        private static void FateBurstBossBranch(TestCtx t)
        {
            var mBoss = BuildOne("fate_burst", new RunCtx { boss = true });
            t.EqTol(1.7, mBoss.rareBurstExpMul, "[fate_burst] boss=true: rareBurstExpMul=1.7");
            t.EqTol(1.5, mBoss.rareBurstScoreMul, "[fate_burst] boss=true: rareBurstScoreMul=1.5(공통)");

            var mNonBoss = BuildOne("fate_burst", new RunCtx { boss = false });
            t.EqTol(1.8, mNonBoss.rareBurstExpMul, "[fate_burst] boss=false: rareBurstExpMul=1.8");
            t.EqTol(1.5, mNonBoss.rareBurstScoreMul, "[fate_burst] boss=false: rareBurstScoreMul=1.5(공통)");
        }

        // black_diploma: if (curseCount>=5) { expMul*=1.6; scoreMul*=1.3; bonusSpins-=1 } — curseCount는
        // ctx 필드(실제 curseIds 리스트와 무관)라 4(미충족)/5(경계,충족)만으로 충분히 경계를 확인할 수 있다.
        private static void BlackDiplomaThreshold(TestCtx t)
        {
            var m4 = BuildOne("black_diploma", new RunCtx { curseCount = 4 });
            t.EqTol(1.0, m4.expMul, "[black_diploma] curseCount=4: expMul=1.0(미발동)");
            t.EqTol(1.0, m4.scoreMul, "[black_diploma] curseCount=4: scoreMul=1.0(미발동)");
            t.Eq(0, m4.bonusSpins, "[black_diploma] curseCount=4: bonusSpins=0(미발동)");

            var m5 = BuildOne("black_diploma", new RunCtx { curseCount = 5 });
            t.EqTol(1.6, m5.expMul, "[black_diploma] curseCount=5(경계): expMul=1.6(발동)");
            t.EqTol(1.3, m5.scoreMul, "[black_diploma] curseCount=5(경계): scoreMul=1.3(발동)");
            t.Eq(-1, m5.bonusSpins, "[black_diploma] curseCount=5(경계): bonusSpins=-1(발동)");
        }
    }

    // ── ③ ApplyItemMods 대표5종 + dev_coin(단일소스) + ApplyPassiveDevice 4종 + ARMED 통과 ─────────────
    internal static class Tests_RunNet_ItemsDevices
    {
        public static void Run(TestCtx t)
        {
            ApplyItemModsRepresentative(t);
            DevCoinSingleSource(t);
            ApplyPassiveDeviceFour(t);
            ArmedKindPassthrough(t);
        }

        // 대표 5종: energy_drink(NEXTSPIN expMul=2.0)·ward_charm(NEXTSPIN symbolWeightMul.skull=0.0)·
        // skull_sticker(NEXTSPIN skullScoreBonus=100.0)·espresso(PHASE flatExp=15.0)·prof_bribe(PHASE quotaMul=0.85).
        private static void ApplyItemModsRepresentative(TestCtx t)
        {
            var baseMods = new Mods();
            var m1 = ModsBuilder.ApplyItemMods(baseMods, new List<string> { "energy_drink", "espresso" });
            t.EqTol(2.0, m1.expMul, "[items] energy_drink: expMul=1.0×2.0=2.0");
            t.Eq(15, m1.flatExp, "[items] espresso: flatExp=0+15=15");
            t.EqTol(1.0, baseMods.expMul, "[items] ApplyItemMods는 Clone 기반 — baseMods.expMul 변형 없음");
            t.Eq(0, baseMods.flatExp, "[items] baseMods.flatExp 변형 없음");

            var m2 = ModsBuilder.ApplyItemMods(new Mods(), new List<string> { "ward_charm" });
            t.EqTol(0.0, m2.symbolWeightMul.TryGetValue(Sym.Skull, out var wm) ? wm : 1.0, "[items] ward_charm: symbolWeightMul[skull]=0.0(해골 미등장)");

            var m3 = ModsBuilder.ApplyItemMods(new Mods(), new List<string> { "skull_sticker" });
            t.Eq(100, m3.skullScoreBonus, "[items] skull_sticker: skullScoreBonus=100");

            var m4 = ModsBuilder.ApplyItemMods(new Mods(), new List<string> { "prof_bribe" });
            t.EqTol(0.85, m4.quotaMul, "[items] prof_bribe: quotaMul=0.85");

            // 빈/널 목록은 baseMods를 그대로(clone 없이) 반환 — Kotlin이 아이템 없으면 원본 mods를 그대로
            // 쓰는 것과 동일한 얕은 지름길(Mods.cs ApplyItemMods 첫 줄 가드).
            var baseMods2 = new Mods();
            var mEmpty = ModsBuilder.ApplyItemMods(baseMods2, new List<string>());
            t.True(ReferenceEquals(baseMods2, mEmpty), "[items] itemIds가 빈 리스트면 baseMods를 그대로 반환(참조 동일)");
            var mNull = ModsBuilder.ApplyItemMods(baseMods2, null);
            t.True(ReferenceEquals(baseMods2, mNull), "[items] itemIds가 null이면 baseMods를 그대로 반환(참조 동일)");
        }

        // dev_coin(🪙투입) — Opus S3 중요-2: 배수를 Mods.cs에 하드코딩하지 않고 Devices.cs fx가 단일 소스여야
        // 한다. (a) Devices.cs 값 자체가 1.3인지(콘텐츠 회귀) + (b) ApplyItemMods 결과가 baseExpMul×그 값과
        // 정확히 일치하는지(구현이 실제로 그 딕셔너리를 읽는지, 하드코딩 여부) 둘 다 검증한다.
        private static void DevCoinSingleSource(TestCtx t)
        {
            double devCoinMul = Devices.ById("dev_coin").fx["expMul"];
            t.EqTol(1.3, devCoinMul, "[dev_coin] Devices.cs fx[\"expMul\"]=1.3 (콘텐츠 회귀)");

            var baseMods = new Mods { expMul = 2.0 }; // 임의의 비-1.0 시작값 — 곱연산임을 확인하기 위함
            var m = ModsBuilder.ApplyItemMods(baseMods, new List<string> { "dev_coin" });
            t.EqTol(2.0 * devCoinMul, m.expMul, "[dev_coin] ApplyItemMods 결과 expMul = base × Devices.ById(\"dev_coin\").fx[expMul] (단일소스 구조 검증)");
            t.EqTol(2.0, baseMods.expMul, "[dev_coin] baseMods 변형 없음(Clone)");
        }

        // ApplyPassiveDevice 4종 — dev_flame/dev_seal/dev_overheat/dev_subreel(전부 kind=PASSIVE).
        private static void ApplyPassiveDeviceFour(TestCtx t)
        {
            var mFlame = ModsBuilder.ApplyPassiveDevice(new Mods(), "dev_flame");
            t.EqTol(1.15, mFlame.expMul, "[passive] dev_flame: expMul=1.15");

            var mSeal = ModsBuilder.ApplyPassiveDevice(new Mods(), "dev_seal");
            t.EqTol(1.05, mSeal.expMul, "[passive] dev_seal: expMul=1.05");
            t.EqTol(0.0, mSeal.symbolWeightMul.TryGetValue(Sym.Skull, out var wmSeal) ? wmSeal : 1.0, "[passive] dev_seal: symbolWeightMul[skull]=0.0(해골 미등장)");

            var mOverheat = ModsBuilder.ApplyPassiveDevice(new Mods(), "dev_overheat");
            t.EqTol(1.18, mOverheat.expMul, "[passive] dev_overheat: expMul=1.18");
            t.EqTol(1.0, mOverheat.weightAdd.TryGetValue(Sym.Skull, out var waOverheat) ? waOverheat : 0.0, "[passive] dev_overheat: weightAdd[skull]=1.0(고위험 해골 추가등장)");

            var mSubreel = ModsBuilder.ApplyPassiveDevice(new Mods(), "dev_subreel");
            t.EqTol(0.7, mSubreel.expMul, "[passive] dev_subreel: expMul=0.7(최종 EXP -30%)");

            // 원본 baseMods 불변(Clone) 확인 — 대표로 dev_flame 하나만.
            var baseMods = new Mods();
            ModsBuilder.ApplyPassiveDevice(baseMods, "dev_flame");
            t.EqTol(1.0, baseMods.expMul, "[passive] ApplyPassiveDevice도 Clone 기반 — baseMods 변형 없음");
        }

        // ARMED 통과 — dev_coin은 kind=ARMED(패시브 아님). ApplyPassiveDevice가 이를 통과시켜(무시하고)
        // baseMods를 그대로(참조까지 동일하게) 반환해야 한다 — PASSIVE 전용 경로에 ARMED 장치가 섞여
        // 자동발동되면 안 된다는 kind 게이팅 자체의 회귀 테스트.
        private static void ArmedKindPassthrough(TestCtx t)
        {
            var baseMods = new Mods();
            var result = ModsBuilder.ApplyPassiveDevice(baseMods, "dev_coin");
            t.True(ReferenceEquals(baseMods, result), "[armed] dev_coin(kind=ARMED)은 ApplyPassiveDevice에서 baseMods 그대로 통과(미적용)");
            t.EqTol(1.0, result.expMul, "[armed] dev_coin은 ApplyPassiveDevice로는 expMul에 영향 없음(ARMED는 arm 경유만 유효)");

            // 존재하지 않는 장치 id / MANIP 장치도 동일하게 통과.
            var result2 = ModsBuilder.ApplyPassiveDevice(baseMods, "no_such_device");
            t.True(ReferenceEquals(baseMods, result2), "[armed] 미존재 장치 id도 baseMods 그대로 통과");
            var result3 = ModsBuilder.ApplyPassiveDevice(baseMods, "dev_reroll"); // kind=MANIP
            t.True(ReferenceEquals(baseMods, result3), "[armed] dev_reroll(kind=MANIP)도 baseMods 그대로 통과");
        }
    }

    // ── ④ 저주 스택 임계 3/5/7 중첩·minimalist relic 3/4 경계·Mods.Clone() 딕셔너리 독립성 ──────────────
    internal static class Tests_RunNet_ThresholdsAndClone
    {
        public static void Run(TestCtx t)
        {
            CurseStackThreshold(t);
            MinimalistRelicBoundary(t);
            CloneDictionaryIndependence(t);
        }

        // ModsBuilder.Build L204-207: nCurses>=3 → skullExp+=2; >=5 → scoreMul*=1.12; >=7 → scoreMul*=1.12
        // "또" (5의 조건과 별개 if라 7개면 1.12×1.12 중첩). 아래 7개 저주는 전부 skullExp/scoreMul을
        // 직접 건드리는 fx가 없어(Perks.cs 저주 목록 확인) 임계값 효과만 깨끗하게 분리 관찰할 수 있다.
        private static void CurseStackThreshold(TestCtx t)
        {
            var pool = new[] { "speed_test", "frugal_vow", "tunnel_vision", "hex_allornothing", "high_stakes", "diploma_pressure", "pop_quiz" };

            var m2 = ModsBuilder.Build("basic", "gambler", new List<string>(), pool.Take(2).ToList(), "");
            t.Eq(0, m2.skullExp, "[curse3/5/7] 저주2개: skullExp=0(3개 미만, 미발동)");
            t.EqTol(1.0, m2.scoreMul, "[curse3/5/7] 저주2개: scoreMul=1.0(5개 미만, 미발동)");

            var m3 = ModsBuilder.Build("basic", "gambler", new List<string>(), pool.Take(3).ToList(), "");
            t.Eq(2, m3.skullExp, "[curse3/5/7] 저주3개(경계): skullExp=0+2=2(발동)");
            t.EqTol(1.0, m3.scoreMul, "[curse3/5/7] 저주3개: scoreMul=1.0(5개 미만, 미발동)");

            var m5 = ModsBuilder.Build("basic", "gambler", new List<string>(), pool.Take(5).ToList(), "");
            t.Eq(2, m5.skullExp, "[curse3/5/7] 저주5개(경계): skullExp=2(3개 조건 그대로 유지)");
            t.EqTol(1.12, m5.scoreMul, "[curse3/5/7] 저주5개(경계): scoreMul=1.0×1.12=1.12(발동)");

            var m7 = ModsBuilder.Build("basic", "gambler", new List<string>(), pool.Take(7).ToList(), "");
            t.Eq(2, m7.skullExp, "[curse3/5/7] 저주7개(경계): skullExp=2");
            t.EqTol(1.12 * 1.12, m7.scoreMul, "[curse3/5/7] 저주7개(경계): scoreMul=1.12×1.12=1.2544(5개조건·7개조건 중첩)");
        }

        // charId="minimalist": relicCount<=3 → expMul*=1.25. relicCount는 cat==RELIC인 perkIds만 카운트
        // (AUGMENT는 제외) — old_book/cherry_candy/rusty_coin/pencil 4종은 전부 RELIC, expMul을 건드리는
        // fx가 없어 관찰이 깨끗하다.
        private static void MinimalistRelicBoundary(TestCtx t)
        {
            var relics3 = new List<string> { "old_book", "cherry_candy", "rusty_coin" };
            var m3 = ModsBuilder.Build("basic", "minimalist", relics3, new List<string>(), "");
            t.EqTol(1.25, m3.expMul, "[minimalist] 유물3개(경계): expMul=1.0×1.25=1.25(발동)");

            var relics4 = new List<string> { "old_book", "cherry_candy", "rusty_coin", "pencil" };
            var m4 = ModsBuilder.Build("basic", "minimalist", relics4, new List<string>(), "");
            t.EqTol(1.0, m4.expMul, "[minimalist] 유물4개(경계): expMul=1.0(미발동, 4>3)");

            // AUGMENT(study)를 섞어도 relicCount는 여전히 3(증강은 유물 카운트에서 제외) → 발동 유지.
            var mixed = new List<string> { "study", "old_book", "cherry_candy", "rusty_coin" };
            var mMixed = ModsBuilder.Build("basic", "minimalist", mixed, new List<string>(), "");
            t.EqTol(1.10 * 1.25, mMixed.expMul, "[minimalist] 증강1(study)+유물3: relicCount는 유물만 세므로 여전히 발동 — expMul=1.10×1.25");
        }

        // Mods.Clone()이 Kotlin data class copy()의 "새 Map 인스턴스" 관용구를 재현하는지 — 5개 딕셔너리
        // 필드(perSymbolExp/tagExpBonus/symbolWeightMul/weightAdd/perSymbolScore) 전부를 대상으로 한다.
        private static void CloneDictionaryIndependence(TestCtx t)
        {
            var m1 = ModsBuilder.Build("basic", "", new List<string> { "cursed_skulls", "study_tag", "cherry_up", "crown_seek" }, new List<string>(), "");
            var m2 = m1.Clone();

            t.True(!ReferenceEquals(m1.perSymbolExp, m2.perSymbolExp), "[clone] perSymbolExp: 서로 다른 Dictionary 인스턴스");
            t.True(!ReferenceEquals(m1.tagExpBonus, m2.tagExpBonus), "[clone] tagExpBonus: 서로 다른 Dictionary 인스턴스");
            t.True(!ReferenceEquals(m1.symbolWeightMul, m2.symbolWeightMul), "[clone] symbolWeightMul: 서로 다른 Dictionary 인스턴스");
            t.True(!ReferenceEquals(m1.weightAdd, m2.weightAdd), "[clone] weightAdd: 서로 다른 Dictionary 인스턴스");
            t.True(!ReferenceEquals(m1.perSymbolScore, m2.perSymbolScore), "[clone] perSymbolScore: 서로 다른 Dictionary 인스턴스");

            // m2를 변형해도 m1은 그대로여야 한다.
            m2.weightAdd[Sym.Skull] = 999.0;
            m2.tagExpBonus["학습"] = 999;
            m2.perSymbolExp[Sym.Cherry] = 999;
            m2.perSymbolScore[Sym.Crown] = 999;
            m2.symbolWeightMul[Sym.Crown] = 999.0;
            m2.expMul = 999.0;

            t.EqTol(4.0, m1.weightAdd[Sym.Skull], "[clone] m2 변형 후에도 m1.weightAdd[skull]=4.0 유지(cursed_skulls)");
            t.Eq(4, m1.tagExpBonus["학습"], "[clone] m2 변형 후에도 m1.tagExpBonus[학습]=4 유지(study_tag)");
            t.Eq(2, m1.perSymbolExp[Sym.Cherry], "[clone] m2 변형 후에도 m1.perSymbolExp[cherry]=2 유지(cherry_up)");
            t.Eq(30, m1.perSymbolScore[Sym.Crown], "[clone] m2 변형 후에도 m1.perSymbolScore[crown]=30 유지(crown_seek)");
            t.EqTol(2.0, m1.symbolWeightMul[Sym.Crown], "[clone] m2 변형 후에도 m1.symbolWeightMul[crown]=2.0 유지(crown_seek)");
            t.EqTol(1.0, m1.expMul, "[clone] m2.expMul 변형은 값 타입 — m1.expMul 무관(스칼라 필드 정상 복사 확인)");

            // m2 쪽은 실제로 바뀌었는지(변형 자체가 무의미하지 않았는지) 대조 확인.
            t.EqTol(999.0, m2.weightAdd[Sym.Skull], "[clone] m2.weightAdd[skull]는 실제로 999로 바뀜(mutation sanity)");
        }
    }

    // ── ⑤ Evaluate() 세부 메커닉 — 셀 직접 구성으로 결정론 확보 ────────────────────────────────────
    internal static class Tests_RunNet_Evaluate
    {
        private static Cell C(string id, string tag = "") => new Cell(Symbols.ById(id), tag);
        private static readonly Rng UnusedRng = new Rng(1); // dice 심볼을 쓰지 않으므로 실제로 소비되지 않음

        public static void Run(TestCtx t)
        {
            Bomb(t);
            Magnet(t);
            Set3(t);
            Set4(t);
            Jackpot5(t);
            AdjacentPairs(t);
            EndsMatchAndCap(t);
            SkullPenaltyAndBonus(t);
            RareBurst(t);
        }

        // 💣 폭탄 — cells=[cherry,bomb,book,star,gem], bomb(idx1) 양옆(idx0 cherry, idx2 book) 제거.
        // bombExp=제거수(2)×BOMB_EXP_PER(8)=16. 제거된 칸은 EMPTY(exp0)로 치환되어 세트 카운트에서도 빠진다
        // (cherry/book이 사라져 남는 value 심볼은 star·gem 뿐 → 둘 다 count1, tie는 우선순위상 star).
        private static void Bomb(TestCtx t)
        {
            var cells = new List<Cell> { C("cherry"), C("bomb"), C("book"), C("star"), C("gem") };
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);

            // exp = bomb(5) + star(8) + gem(1) + bombExp(16) = 30 (제거된 두 칸은 0)
            t.Eq(30L, res.exp, "[bomb] exp = bomb기본5 + star8 + gem1 + bombExp16 = 30");
            t.Eq(15L, res.score, "[bomb] score = gem15(그 외 0)");
            t.Eq("star", res.bestSetId, "[bomb] cherry/book 제거로 남은 value심볼 star/gem 중 우선순위상 star");
            t.Eq(1, res.bestSetCount, "[bomb] bestSetCount=1(세트 미형성)");
            t.True(res.notes.Contains("💣 2칸 제거 +16"), "[bomb] note: \"💣 2칸 제거 +16\"");
        }

        // 🧲 자석 — cells=[cherry,magnet,book,gem,star], magnet(idx1) 왼쪽 우선으로 idx0(cherry) 복사.
        private static void Magnet(TestCtx t)
        {
            var cells = new List<Cell> { C("cherry"), C("magnet"), C("book"), C("gem"), C("star") };
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);

            // cherry가 idx0,idx1(복사) 2개 → 세트2, 세트보너스 SetExp[2]=8,SetScore[2]=3.
            // exp = cherry3+cherry3+book6+gem1+star8 + 8(세트) = 29
            t.Eq(29L, res.exp, "[magnet] exp = cherry×2(3+3)+book6+gem1+star8 + 세트보너스8 = 29");
            t.Eq(18L, res.score, "[magnet] score = gem15 + 세트보너스3 = 18");
            t.Eq("cherry", res.bestSetId, "[magnet] 복사로 cherry 2개 형성");
            t.Eq(2, res.bestSetCount, "[magnet] bestSetCount=2");
            t.True(res.notes.Contains("🧲 🍒 복사"), "[magnet] note: \"🧲 🍒 복사\"");
        }

        // 세트3 — mods.set3ExpMul=1.25 (bestCount>=3 조건부 배율, capBase 이후·전역배수 이전 적용).
        // capBase는 세트 고정보너스까지 포함한 시점의 값(=41)이고, set3ExpMul은 그 이후 exp에만 곱해진다.
        private static void Set3(TestCtx t)
        {
            var cells = new List<Cell> { C("cherry"), C("cherry"), C("cherry"), C("book"), C("star") };
            var mods = new Mods { set3ExpMul = 1.25 };
            var res = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);

            // 기본합 9(cherry×3)+6(book)+8(star)=23, +세트3보너스18=41, ×set3ExpMul1.25=51.25→51(0방향절삭)
            t.Eq(51L, res.exp, "[set3] (23+세트보너스18)×1.25=51.25→51 (0방향 절삭, python 검산)");
            t.Eq(9L, res.score, "[set3] score = 세트3 SetScore=9(그 외 0)");
            t.True(res.notes.Contains("🧩퍼즐 세트3 EXP ×1.25"), "[set3] note: \"🧩퍼즐 세트3 EXP ×1.25\"");
        }

        // 세트4 — mods.set4ScoreMul=1.20 (bestCount>=4 조건부 점수 배율, score 파이프라인 마지막 단계 직전).
        private static void Set4(TestCtx t)
        {
            var cells = new List<Cell> { C("cherry"), C("cherry"), C("cherry"), C("cherry"), C("book") };
            var mods = new Mods { set4ScoreMul = 1.20 };
            var res = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);

            // exp = 12(cherry×4)+6(book)+세트4보너스42 = 60 (set4는 exp에 영향 없음, score만)
            t.Eq(60L, res.exp, "[set4] exp = 12+6+세트4보너스42 = 60(set4ScoreMul은 exp 무관)");
            // score = 세트4 SetScore24 × 1.20 = 28.8 → 28(0방향 절삭)
            t.Eq(28L, res.score, "[set4] score = SetScore24×1.20=28.8→28 (python 검산)");
            t.Eq(4, res.bestSetCount, "[set4] bestSetCount=4(잭팟 아님, reel=5 미달)");
            t.True(res.jackpotSym == null, "[set4] bestCount4<reel5 → 잭팟 아님");
        }

        // 잭팟(5) — 전 칸 crown(reel=5). jb=520(crown), jackpotFixed는 exp에 가산·score는 jb×5 즉시 가산.
        private static void Jackpot5(TestCtx t)
        {
            var cells = new List<Cell> { C("crown"), C("crown"), C("crown"), C("crown"), C("crown") };
            var res = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);

            // exp = 100(crown×5, 20each) + 세트5보너스100 + jackpotFixed520 = 720
            t.Eq(720L, res.exp, "[jackpot] exp = crown합100 + 세트5보너스100 + 잭팟고정520 = 720");
            // score = 250(crown score50×5) + 세트5 SetScore70 + jb×5(2600) = 2920
            t.Eq(2920L, res.score, "[jackpot] score = 250 + 세트5보너스70 + 잭팟점수2600 = 2920");
            t.Eq("crown", res.jackpotSym, "[jackpot] jackpotSym=crown");
            t.True(res.notes.Any(n => n.Contains("잭팟") && n.Contains("+520EXP") && n.Contains("+2600점")), "[jackpot] note에 +520EXP·+2600점 포함");
        }

        // 인접쌍 — mods.adjacentSameExp=20(chain 퍽 수치). cells=[cherry,cherry,book,book,star] → 인접쌍
        // (idx0-1 cherry, idx2-3 book) 2쌍 → +40.
        private static void AdjacentPairs(TestCtx t)
        {
            var cells = new List<Cell> { C("cherry"), C("cherry"), C("book"), C("book"), C("star") };
            var mods = new Mods { adjacentSameExp = 20 };
            var res = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);

            // 기본합 6+12+8=26, +세트2(cherry) 보너스8=34, +인접쌍2×20=40 → 74
            t.Eq(74L, res.exp, "[adjacent] (26+세트보너스8)+인접쌍2×20=74");
            t.Eq(3L, res.score, "[adjacent] score=세트2 SetScore3(그 외 base score 0)");
            t.True(res.notes.Contains("🔗 인접 2쌍 +40"), "[adjacent] note: \"🔗 인접 2쌍 +40\"");
        }

        // 양끝 + 총배율 캡 — mods.endsMatchExpMul=2.0. capBase는 "양끝" 배율 적용 *이전* expNoCenter를
        // 기준으로 잡히므로(양끝은 exp만 곱하고 expNoCenter는 손대지 않음) capMul을 걸면 캡이 세게 작동하는
        // 경계 케이스를 만든다 — 이 캡 예외적 baseline이 정확히 지켜지는지가 핵심 회귀 포인트.
        private static void EndsMatchAndCap(TestCtx t)
        {
            var cells = new List<Cell> { C("cherry"), C("book"), C("star"), C("gem"), C("cherry") };
            var mods = new Mods { endsMatchExpMul = 2.0 };

            // capMul=0(캡 비활성) — 순수 양끝배율만 관찰: (21+세트2보너스8)=29, ×2.0=58.
            var resNoCap = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);
            t.Eq(58L, resNoCap.exp, "[ends] capMul=0: (21+세트보너스8)×2.0=58 (양끝 EXP 2배)");
            t.Eq(18L, resNoCap.score, "[ends] score=gem15+세트보너스3=18");

            // capMul=1.5 — capBase=29(양끝배율 적용 전 expNoCenter) 기준 ceiling=29×1.5=43.5.
            // variable(58) > ceiling(43.5) → exp=43.5→43(절삭). capBase가 양끝배율을 반영하지 않는다는
            // 사실 자체를 이 테스트가 고정한다(반영했다면 58<=58×1.5=87이 되어 캡이 발동하지 않았을 것).
            var resCap = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 1.5);
            t.Eq(43L, resCap.exp, "[ends+cap] capBase=29(양끝배율 미반영) × capMul1.5 = 43.5 → 43 (캡 발동, python 검산)");
            t.True(resCap.notes.Any(n => n.Contains("총배율 캡")), "[ends+cap] note에 총배율 캡 표시 포함");
            t.Eq(18L, resCap.score, "[ends+cap] score는 캡 영향 없음(캡은 exp 전용) — 18 그대로");
        }

        // 해골 — mods 기본값(skullExp=0,perSkullExp=0)이면 페널티 분기: pen=skulls×SKULL_PENALTY(3)×1.0.
        // mods.skullExp>0이면 보너스 분기로 전환(페널티 없음) — 두 분기 모두 검증.
        private static void SkullPenaltyAndBonus(TestCtx t)
        {
            var cells = new List<Cell> { C("skull"), C("skull"), C("cherry"), C("book"), C("star") };

            var resPenalty = SpinResolver.Evaluate(UnusedRng, cells, new Mods(), spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);
            // 기본합 0+0+3+6+8=17, 세트 미형성(cherry/book/star 각1개, bestCount1<2), 페널티 skulls2×3×1=6 → 11
            t.Eq(11L, resPenalty.exp, "[skull-] 기본17 - 페널티(2×3)=11");
            t.True(resPenalty.notes.Contains("☠ 2개 -6"), "[skull-] note: \"☠ 2개 -6\"");

            var modsBonus = new Mods { skullExp = 5 };
            var resBonus = SpinResolver.Evaluate(UnusedRng, cells, modsBonus, spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);
            // skull 셀 자체가 5씩 기여(2×5=10) → 기본합 5+5+3+6+8=27, 보너스분기(패널티 없음)
            t.Eq(27L, resBonus.exp, "[skull+] skullExp=5: skull 셀당 +5(×2)로 기본합27, 페널티 없음(해골빌드 보너스분기)");
            t.True(resBonus.notes.Contains("☠ 2개 +10 (해골빌드)"), "[skull+] note: \"☠ 2개 +10 (해골빌드)\"");
        }

        // rareBurst — mods.rareBurstExpMul=1.8·rareBurstScoreMul=1.5. cells에 rare 심볼(crown·wild) 2개 →
        // 조건(rareN>=2) 충족. wild는 최다그룹(cherry, wilds 합류 전 count1)에 합류해 cherry 세트를 2로 만든다.
        private static void RareBurst(TestCtx t)
        {
            var cells = new List<Cell> { C("crown"), C("wild"), C("cherry"), C("book"), C("star") };
            var mods = new Mods { rareBurstExpMul = 1.8, rareBurstScoreMul = 1.5 };
            var res = SpinResolver.Evaluate(UnusedRng, cells, mods, spinIndex: 1, spinsPerStage: 5, flamePenalty: false, capMul: 0);

            // 기본합 20(crown)+0(wild)+3(cherry)+6(book)+8(star)=37, wild가 cherry(최다,count1)에 합류→count2,
            // 세트2보너스8 → 45, ×rareBurstExpMul1.8=81.
            t.Eq(81L, res.exp, "[rareBurst] (37+세트2보너스8)×1.8=81 (wild가 cherry에 합류, python 검산)");
            t.Eq("cherry", res.bestSetId, "[rareBurst] wild 합류로 cherry가 최다(2)");
            t.Eq(2, res.bestSetCount, "[rareBurst] bestSetCount=2(cherry1+wild1)");
            // 기본 score = crown50+세트2 SetScore3=53, ×rareBurstScoreMul1.5=79.5→79.
            t.Eq(79L, res.score, "[rareBurst] (50+세트보너스3)×1.5=79.5→79 (python 검산)");
            t.True(res.notes.Contains("💫운명폭발 EXP ×1.8"), "[rareBurst] note: \"💫운명폭발 EXP ×1.8\"");
        }
    }

    // ── ⑥ ApplyBoss 4종 — 정수 나눗셈(내림) 결과 ─────────────────────────────────────────────────
    internal static class Tests_RunNet_ApplyBoss
    {
        public static void Run(TestCtx t)
        {
            Finals(t);
            Strict(t);
            Luck(t);
            Grad(t);
            UnknownBossDefault(t);
        }

        // finals: 막스핀(spinIndex==spins-1) → gained*2. 첫스핀(spinIndex==0, 막스핀 아닐때) → gained*9/10
        // (정수 나눗셈, 내림). 그 외 → 그대로.
        private static void Finals(TestCtx t)
        {
            var boss = Bosses.ById("finals");

            var (gLast, nLast) = SpinResolver.ApplyBoss(boss, 100, null, spinIndex: 4, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(200L, gLast, "[finals] 막스핀: 100×2=200");
            t.Eq(" · 📝기말 막스핀×2", nLast, "[finals] 막스핀 note 원문(TrimStart 이전)");

            var (gFirst, nFirst) = SpinResolver.ApplyBoss(boss, 100, null, spinIndex: 0, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(90L, gFirst, "[finals] 첫스핀: 100×9/10=90(정수나눗셈)");
            t.Eq(" · 📝기말 첫스핀-10%", nFirst, "[finals] 첫스핀 note 원문");

            // 절삭 확인: 15×9=135, /10=13.5 → 정수나눗셈 13(반올림 아님).
            var (gFirstTrunc, _) = SpinResolver.ApplyBoss(boss, 15, null, spinIndex: 0, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(13L, gFirstTrunc, "[finals] 첫스핀 절삭: 15×9/10=13.5→13(내림, 반올림 아님)");

            var (gMid, nMid) = SpinResolver.ApplyBoss(boss, 100, null, spinIndex: 2, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(100L, gMid, "[finals] 중간스핀: 변화 없음");
            t.Eq("", nMid, "[finals] 중간스핀: note 없음");
        }

        // strict: bestSetCount<3 → gained/2(정수나눗셈). >=3 → 그대로.
        private static void Strict(TestCtx t)
        {
            var boss = Bosses.ById("strict");

            var (gLow, nLow) = SpinResolver.ApplyBoss(boss, 101, new SpinResult { bestSetCount = 2 }, spinIndex: 0, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(50L, gLow, "[strict] bestSetCount=2(<3): 101/2=50.5→50(내림)");
            t.Eq(" · 👨‍🏫콤보없음 ×0.5", nLow, "[strict] note 원문");

            var (gHigh, nHigh) = SpinResolver.ApplyBoss(boss, 100, new SpinResult { bestSetCount = 3 }, spinIndex: 0, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(100L, gHigh, "[strict] bestSetCount=3(경계, >=3): 변화 없음");
            t.Eq("", nHigh, "[strict] bestSetCount=3: note 없음");
        }

        // luck: cells에 star/crown/wild 있으면 gained*18/10, 없으면 gained*8/10 (둘 다 정수나눗셈).
        private static void Luck(TestCtx t)
        {
            var boss = Bosses.ById("luck");
            var rareCells = new List<Cell> { new Cell(Symbols.ById("star")), new Cell(Symbols.ById("cherry")), new Cell(Symbols.ById("cherry")), new Cell(Symbols.ById("cherry")), new Cell(Symbols.ById("cherry")) };
            var norareCells = Enumerable.Range(0, 5).Select(_ => new Cell(Symbols.ById("cherry"))).ToList();

            var (gRare, nRare) = SpinResolver.ApplyBoss(boss, 101, new SpinResult { cells = rareCells }, spinIndex: 0, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(181L, gRare, "[luck] 희귀 있음: 101×18/10=181.8→181(내림)");
            t.Eq(" · 🎲희귀 ×1.8", nRare, "[luck] note 원문(희귀)");

            var (gNorare, nNorare) = SpinResolver.ApplyBoss(boss, 101, new SpinResult { cells = norareCells }, spinIndex: 0, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(80L, gNorare, "[luck] 희귀 없음: 101×8/10=80.8→80(내림)");
            t.Eq(" · 🎲노희귀 ×0.8", nNorare, "[luck] note 원문(노희귀)");
        }

        // grad: pace=expectedPerSpin*0.7. expectedPerSpin>0 && gained<pace 일 때만: augCount<3 →
        // gained*75/100, augCount>=3 → gained*85/100 (둘 다 정수나눗셈). expectedPerSpin<=0이면 항상 무효.
        private static void Grad(TestCtx t)
        {
            var boss = Bosses.ById("grad");

            var (gWeak, nWeak) = SpinResolver.ApplyBoss(boss, 50, null, spinIndex: 0, spins: 5, expectedPerSpin: 100, augCount: 2);
            t.Eq(37L, gWeak, "[grad] pace=70,gained50<70,augCount2(<3): 50×75/100=37.5→37(내림)");
            t.Eq(" · 🎓빈약빌드 ×0.75", nWeak, "[grad] note 원문(빈약빌드)");

            var (gMid, nMid) = SpinResolver.ApplyBoss(boss, 50, null, spinIndex: 0, spins: 5, expectedPerSpin: 100, augCount: 5);
            t.Eq(42L, gMid, "[grad] pace=70,gained50<70,augCount5(>=3): 50×85/100=42.5→42(내림)");
            t.Eq(" · 🎓꾸준함부족 ×0.85", nMid, "[grad] note 원문(꾸준함부족)");

            var (gOk, nOk) = SpinResolver.ApplyBoss(boss, 80, null, spinIndex: 0, spins: 5, expectedPerSpin: 100, augCount: 0);
            t.Eq(80L, gOk, "[grad] gained80>=pace70: 변화 없음");
            t.Eq("", nOk, "[grad] gained>=pace: note 없음");

            var (gNoExpected, _) = SpinResolver.ApplyBoss(boss, 10, null, spinIndex: 0, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(10L, gNoExpected, "[grad] expectedPerSpin<=0: 조건 자체가 무효라 gained 변화 없음");
        }

        // 알 수 없는 boss.id — default 분기, 변화 없음(구조적 방어 확인).
        private static void UnknownBossDefault(TestCtx t)
        {
            var fakeBoss = new Boss { id = "no_such_boss" };
            var (g, n) = SpinResolver.ApplyBoss(fakeBoss, 123, null, spinIndex: 0, spins: 5, expectedPerSpin: 0, augCount: 0);
            t.Eq(123L, g, "[default] 미지 boss.id: 변화 없음");
            t.Eq("", n, "[default] 미지 boss.id: note 없음");
        }
    }

    // ── ⑦ QuotaOf/EffSpins/CmdCoinCost + 스핀모드 4종 대표경로 + 거부 3경로 + PHASE_NOT_SPIN 가드 ──────
    internal static class Tests_RunNet_SpinModesAndRejects
    {
        public static void Run(TestCtx t)
        {
            QuotaOfFormula(t);
            EffSpinsFormula(t);
            CmdCoinCostTable(t);
            FocusRepresentative(t);
            AllinRepresentative(t);
            PrayRepresentative(t);
            LastRepresentative(t);
            RejectPaths(t);
        }

        // QuotaOf(stage,mods) = (long)(Formulas.Quota(stage) × mods.quotaMul × Bosses.QuotaMulFor(stage) × prop),
        // prop = (bsp>0&&baseSpins>0) ? (baseSpins+bsp)/baseSpins : 1.0.
        private static void QuotaOfFormula(TestCtx t)
        {
            // 비보스: stage1, mods 기본값 → prop=1.0, quota=110×1×1×1=110.
            var q1 = SpinResolver.QuotaOf(1, new Mods());
            t.Eq(110L, q1, "[QuotaOf] stage1(비보스),mods기본: 110");

            // 보스: stage5(finals,bonusSpins1,quotaMul1.0), mods 기본값(baseSpins5) → prop=(5+1)/5=1.2.
            // quota=150×1.0×1.0×1.2=180.0→180.
            var q5 = SpinResolver.QuotaOf(5, new Mods());
            t.Eq(180L, q5, "[QuotaOf] stage5(finals boss),mods기본: 150×1.0×1.2=180");

            // 보스+quotaMul: stage20(grad,quotaMul1.15), mods 기본값 → quota=1878×1×1.15×1.2=2591.64→2591.
            var q20 = SpinResolver.QuotaOf(20, new Mods());
            t.Eq(2591L, q20, "[QuotaOf] stage20(grad boss,quotaMul1.15),mods기본: 1878×1.15×1.2=2591.64→2591 (python 검산)");

            // mods.quotaMul이 실제로 곱해지는지: stage1, quotaMul=0.5 → 110×0.5=55.
            var qHalf = SpinResolver.QuotaOf(1, new Mods { quotaMul = 0.5 });
            t.Eq(55L, qHalf, "[QuotaOf] stage1,quotaMul=0.5: 110×0.5=55");
        }

        // EffSpins(run,mods) = Max(SpinsPerStage(mods)+run.StageBonusSpins+Bosses.Spins(run.Stage), MIN_SPINS(3)).
        private static void EffSpinsFormula(TestCtx t)
        {
            var run1 = RunTestHelpers.NewRun(1L);
            run1.Stage = 1; run1.StageBonusSpins = 2;
            t.Eq(7, SpinResolver.EffSpins(run1, new Mods()), "[EffSpins] 비보스,StageBonusSpins2,mods기본: 5+2+0=7");

            var run2 = RunTestHelpers.NewRun(2L);
            run2.Stage = 5; run2.StageBonusSpins = 0; // finals boss, bonusSpins=1
            t.Eq(6, SpinResolver.EffSpins(run2, new Mods()), "[EffSpins] finals보스(bonusSpins1),mods기본,StageBonusSpins0: 5+0+1=6");

            var run3 = RunTestHelpers.NewRun(3L);
            run3.Stage = 1; run3.StageBonusSpins = 0;
            t.Eq(3, SpinResolver.EffSpins(run3, new Mods { bonusSpins = -10 }), "[EffSpins] mods.bonusSpins=-10: SpinsPerStage 자체가 MIN_SPINS(3)로 클램프 → 3+0+0=3");
        }

        // CmdCoinCost(mode,boss) — base+boss surcharge(1), N모드는 base0이라 항상 0(surcharge 자체 미적용).
        private static void CmdCoinCostTable(TestCtx t)
        {
            t.Eq(0, SpinResolver.CmdCoinCost(SpinMode.N, false), "[cmdCost] N,비보스: 0");
            t.Eq(0, SpinResolver.CmdCoinCost(SpinMode.N, true), "[cmdCost] N,보스: 0(base0이라 surcharge도 미적용)");
            t.Eq(1, SpinResolver.CmdCoinCost(SpinMode.Focus, false), "[cmdCost] Focus,비보스: 1");
            t.Eq(2, SpinResolver.CmdCoinCost(SpinMode.Focus, true), "[cmdCost] Focus,보스: 1+1=2");
            t.Eq(2, SpinResolver.CmdCoinCost(SpinMode.Last, false), "[cmdCost] Last,비보스: 2");
            t.Eq(3, SpinResolver.CmdCoinCost(SpinMode.Last, true), "[cmdCost] Last,보스: 2+1=3");
            t.Eq(3, SpinResolver.CmdCoinCost(SpinMode.Pray, false), "[cmdCost] Pray,비보스: 3");
            t.Eq(4, SpinResolver.CmdCoinCost(SpinMode.Pray, true), "[cmdCost] Pray,보스: 3+1=4");
            t.Eq(4, SpinResolver.CmdCoinCost(SpinMode.Allin, false), "[cmdCost] Allin,비보스: 4");
            t.Eq(5, SpinResolver.CmdCoinCost(SpinMode.Allin, true), "[cmdCost] Allin,보스: 4+1=5(CMD_COST_MAX 경계, 클램프 불필요)");
        }

        // 공통 셀: cherry+book+star(center)+gem+coin, novice/basic/무퍽 → gained(mode 적용 전)=18(손계산).
        private static readonly string[] CommonCells = { "cherry", "book", "star", "gem", "coin" };

        private static RunState FreshRun(long seed, int spinIndex)
        {
            var run = RunTestHelpers.NewRun(seed);
            run.Stage = 1; run.StageExp = 0; run.SpinIndex = spinIndex; run.Coins = 100;
            run.LockedNext.AddRange(CommonCells);
            return run;
        }

        // Focus — floor=(long)(quota/spins×0.6)=12 (quota101,spins5). 낮은 셀 구성으로 floor 발동을 강제.
        private static void FocusRepresentative(TestCtx t)
        {
            var run = FreshRun(10L, spinIndex: 1);
            run.LockedNext.Clear();
            run.LockedNext.AddRange(new[] { "coin", "coin", "coin", "coin", "coin" }); // gained=0 (coin은 value심볼 아님, exp0)
            var step = StageFlow.ProcessSpin(run, SpinMode.Focus);
            t.True(!step.spin.rejected, "[Focus] 대표경로: 거부 없음");
            t.Eq(12L, step.spin.gained, "[Focus] floor=(101/5×0.6)=12.12→12, gained0<12 → 최소보장 12 (python 검산)");
            t.True(step.spin.notes.Contains("🎯집중 — 최소 보장"), "[Focus] note: 최소 보장");

            var run2 = FreshRun(11L, spinIndex: 1); // 기본 셀(18) — floor(12) 이상이라 안정 분기
            var step2 = StageFlow.ProcessSpin(run2, SpinMode.Focus);
            t.Eq(18L, step2.spin.gained, "[Focus] gained18>=floor12: 그대로 18(안정 분기)");
            t.True(step2.spin.notes.Contains("🎯집중(안정)"), "[Focus] note: 안정");
        }

        // Allin — skulls>=2면 gained=0(무조건, 계산값 무관). skulls<2면 gained*2.
        private static void AllinRepresentative(TestCtx t)
        {
            var runFail = FreshRun(20L, spinIndex: 1);
            runFail.LockedNext.Clear();
            runFail.LockedNext.AddRange(new[] { "skull", "skull", "cherry", "book", "star" });
            var stepFail = StageFlow.ProcessSpin(runFail, SpinMode.Allin);
            t.Eq(0L, stepFail.spin.gained, "[Allin] skulls=2(>=2): gained=0(무조건 실패)");
            t.True(stepFail.spin.notes.Contains("🎲올인 실패! EXP 0"), "[Allin] note: 실패");

            var runOk = FreshRun(21L, spinIndex: 1); // 기본 셀(18, skulls=0)
            var stepOk = StageFlow.ProcessSpin(runOk, SpinMode.Allin);
            t.Eq(36L, stepOk.spin.gained, "[Allin] skulls=0: 18×2=36");
            t.True(stepOk.spin.notes.Contains("🎲올인 성공! EXP ×2"), "[Allin] note: 성공");
        }

        // Pray — RNG 8% 기적 분기는 시드에 따라 갈릴 수 있어, 실제로 어느 가지를 탔는지(prayMiracle)를
        // 관찰한 뒤 그 가지의 공식이 맞는지 검증한다(어느 쪽이 나와도 항상 통과하는 결정론적 불변식).
        private static void PrayRepresentative(TestCtx t)
        {
            var run = FreshRun(30L, spinIndex: 1); // 기본 셀(18), quota101,spins5 → low=(101/5×0.5)=10.1→10
            var step = StageFlow.ProcessSpin(run, SpinMode.Pray);
            t.True(!step.spin.rejected, "[Pray] 대표경로: 거부 없음");

            // low=10(=101/5×0.5 절삭), 원본gained=18>=low이므로 "불운보정(+25)" 가지는 이 시나리오에서
            // 구조적으로 도달 불가 — 기적(8%) 여부만 시드에 따라 갈린다. 두 가지 모두 커버.
            if (step.spin.prayMiracle)
            {
                t.Eq(54L, step.spin.gained, "[Pray] 기적 분기(8%): 18×3=54");
                t.True(step.spin.notes.Any(n => n.Contains("기적")), "[Pray] note에 '기적' 포함");
            }
            else
            {
                t.Eq(18L, step.spin.gained, "[Pray] 평범 분기(gained18>=low10, 비기적): 그대로 18");
                t.True(step.spin.notes.Contains("🙏기도"), "[Pray] note: 기도(평범)");
            }
        }

        // Last — 반드시 마지막 스핀에서만 허용(스핀 -1 아니면 거부, §거부경로에서 별도 검증). gained=(long)(x×1.75).
        private static void LastRepresentative(TestCtx t)
        {
            var run = FreshRun(40L, spinIndex: 4); // spins=5 → 마지막 스핀(index4)
            var step = StageFlow.ProcessSpin(run, SpinMode.Last);
            t.True(!step.spin.rejected, "[Last] 마지막 스핀에서는 거부되지 않음");
            t.Eq(31L, step.spin.gained, "[Last] (long)(18×1.75)=31.5→31 (python 검산)");
            t.True(step.spin.notes.Contains("⏰최후 EXP ×1.75"), "[Last] note: EXP×1.75");
        }

        // 거부 3경로(LAST_NOT_FINAL_SPIN/MODE_ALREADY_USED/INSUFFICIENT_COINS) + PHASE_NOT_SPIN 가드.
        private static void RejectPaths(TestCtx t)
        {
            // 1) LAST_NOT_FINAL_SPIN — spinIndex(0) != spins-1(4)일 때 Last 모드 거부.
            var run1 = FreshRun(50L, spinIndex: 0);
            var step1 = StageFlow.ProcessSpin(run1, SpinMode.Last);
            t.True(step1.spin.rejected, "[reject] LAST_NOT_FINAL_SPIN: rejected=true");
            t.Eq("LAST_NOT_FINAL_SPIN", step1.spin.rejectReason, "[reject] rejectReason=LAST_NOT_FINAL_SPIN");
            t.Eq(0L, run1.LastGain, "[reject] 거부 시 run.LastGain 등 상태 변경 없음(부작용 0)");

            // 2) MODE_ALREADY_USED — 같은 스테이지에서 같은 모드 재사용 거부.
            var run2 = FreshRun(51L, spinIndex: 1);
            run2.UsedCmds.Add("FOCUS");
            int coinsBefore = (int)run2.Coins;
            var step2 = StageFlow.ProcessSpin(run2, SpinMode.Focus);
            t.True(step2.spin.rejected, "[reject] MODE_ALREADY_USED: rejected=true");
            t.Eq("MODE_ALREADY_USED", step2.spin.rejectReason, "[reject] rejectReason=MODE_ALREADY_USED");
            t.Eq(coinsBefore, (int)run2.Coins, "[reject] 거부 시 코인 차감 없음");

            // 3) INSUFFICIENT_COINS — 코인 부족(0 < CMD_COST_FOCUS=1).
            var run3 = FreshRun(52L, spinIndex: 1);
            run3.Coins = 0;
            var step3 = StageFlow.ProcessSpin(run3, SpinMode.Focus);
            t.True(step3.spin.rejected, "[reject] INSUFFICIENT_COINS: rejected=true");
            t.Eq("INSUFFICIENT_COINS", step3.spin.rejectReason, "[reject] rejectReason=INSUFFICIENT_COINS");
            t.Eq(0L, run3.Coins, "[reject] 거부 시 run.Coins 변화 없음(여전히 0)");
            t.Eq(1, run3.SpinIndex, "[reject] 거부 시 run.SpinIndex 변화 없음(1 유지, 부작용 없음 재확인)");

            // 4) PHASE_NOT_SPIN 가드 — StageFlow.ProcessSpin은 run.Phase!=Spin이면 SpinResolver를 호출조차
            // 하지 않고 즉시 거부한다(§02_service.md handleInput 라우팅 대응).
            var run4 = FreshRun(53L, spinIndex: 1);
            run4.Phase = RunPhase.NodeSelect;
            var step4 = StageFlow.ProcessSpin(run4, SpinMode.N);
            t.Eq(SpinStepKind.Rejected, step4.kind, "[reject] PHASE_NOT_SPIN: kind=Rejected");
            t.True(step4.spin.rejected, "[reject] PHASE_NOT_SPIN: spin.rejected=true");
            t.Eq("PHASE_NOT_SPIN", step4.spin.rejectReason, "[reject] rejectReason=PHASE_NOT_SPIN");
            t.Eq(RunPhase.NodeSelect, run4.Phase, "[reject] PHASE_NOT_SPIN: run.Phase 변경 없음(NodeSelect 유지)");
        }
    }

    // ── ⑧ StageFlow: fate_bell 회생 경계·클리어 보상 수치·Growth/SnowStack 갱신·RollNextNodes 분기 ──────
    internal static class Tests_RunNet_StageFlow
    {
        public static void Run(TestCtx t)
        {
            FateBellBoundary(t);
            ClearRewardsCloseAndLast(t);
            ClearRewardsHalfCloseAndGrade(t);
            ClearRewardsInDebt(t);
            ClearRewardsBossAndSnowDecrement(t);
            RollNextNodesStageGate(t);
        }

        // fate_bell: HandleFailure §3-C step1 — deficit<=15 && !FateBellUsed && Perks.Contains("fate_bell")
        // 이면 회생(SPIN 유지, StageBonusSpins+1). deficit=5(여유)/15(경계,회생)/16(경계+1,미회생→체인 진행).
        // 공통 셋업: novice/basic/perks=[fate_bell], stage1(quota=101,spins=5), 마지막 스핀(spinIndex4)에서
        // LockedNext=[coin×5](gained=0)로 newExp=StageExp 그대로 고정 → deficit=101-StageExp를 정밀 제어.
        private static void FateBellBoundary(TestCtx t)
        {
            RunState Setup(long seed, long stageExpForDeficit)
            {
                var run = RunTestHelpers.NewRun(seed);
                run.Stage = 1; run.SpinIndex = 4; run.StageExp = stageExpForDeficit;
                run.Perks.Add("fate_bell");
                run.LockedNext.AddRange(new[] { "coin", "coin", "coin", "coin", "coin" }); // gained=0
                return run;
            }

            // deficit=5 (101-96)
            var runA = Setup(60L, 96);
            var stepA = StageFlow.ProcessSpin(runA, SpinMode.N);
            t.Eq(SpinStepKind.Revived, stepA.kind, "[fate_bell] deficit=5: 회생(Revived)");
            t.Eq("FATE_BELL_REVIVE", stepA.failure.kind, "[fate_bell] deficit=5: failure.kind=FATE_BELL_REVIVE");
            t.Eq(5L, stepA.failure.deficitAtFailure, "[fate_bell] deficitAtFailure=5");
            t.True(runA.FateBellUsed, "[fate_bell] deficit=5: FateBellUsed=true(1회 소모)");
            t.Eq(1, runA.StageBonusSpins, "[fate_bell] deficit=5: StageBonusSpins+1");
            t.Eq(RunPhase.Spin, runA.Phase, "[fate_bell] deficit=5: Phase는 Spin 유지(스핀소진 없이 계속)");

            // deficit=15 (101-86) — 경계, 포함(<=15)이라 회생.
            var runB = Setup(61L, 86);
            var stepB = StageFlow.ProcessSpin(runB, SpinMode.N);
            t.Eq(SpinStepKind.Revived, stepB.kind, "[fate_bell] deficit=15(경계): 회생(Revived)");
            t.Eq(15L, stepB.failure.deficitAtFailure, "[fate_bell] deficitAtFailure=15");

            // deficit=16 (101-85) — 경계+1, 미회생 → 다음 체인(보험/만회 없음) → 게임오버.
            var runC = Setup(62L, 85);
            var stepC = StageFlow.ProcessSpin(runC, SpinMode.N);
            t.Eq(SpinStepKind.GameOver, stepC.kind, "[fate_bell] deficit=16(경계+1): 회생 실패 → 만회수단 없음 → GameOver");
            t.True(!runC.FateBellUsed, "[fate_bell] deficit=16: fate_bell 자체가 발동 안 해 FateBellUsed는 false 그대로");
        }

        // 공통: novice/basic/무퍽, stage1(quota=101), LockedNext=[cherry,book,star,gem,coin](gained=18,
        // spinIndex 무관하게 동일값 — mods.lastSpinExpMul 기본1.0이라 마지막스핀이어도 배율 변화 없음).
        private static RunState ClearSetup(long seed, long stageExp, int spinIndex, IReadOnlyList<string> perks = null)
        {
            var run = RunTestHelpers.NewRun(seed);
            run.Stage = 1; run.StageExp = stageExp; run.SpinIndex = spinIndex;
            if (perks != null) run.Perks.AddRange(perks);
            run.LockedNext.AddRange(new[] { "cherry", "book", "star", "gem", "coin" });
            return run;
        }

        // A: leftover=3(<=5→+300) & lastSpinClear(newIdx5>=spins5→+200) 동시 — close=500.
        // clearCoinBonus 검증도 겸함(piggy_bank: clearCoinBonus+2 → clearCoin=5+2=7).
        private static void ClearRewardsCloseAndLast(TestCtx t)
        {
            var run = ClearSetup(70L, stageExp: 86, spinIndex: 4, perks: new[] { "piggy_bank" }); // 101-18+3=86
            int stageBefore = run.Stage;
            var step = StageFlow.ProcessSpin(run, SpinMode.N);

            t.Eq(SpinStepKind.Cleared, step.kind, "[clearA] leftover3+막판 → 클리어");
            t.Eq(3L, step.clear.leftover, "[clearA] leftover=104-101=3");
            t.True(step.clear.lastSpinClear, "[clearA] lastSpinClear=true(newIdx5>=spins5)");
            t.True(step.clear.closeClear, "[clearA] closeClear=true(leftover3<=10)");
            t.Eq(500L, step.clear.closeBonus, "[clearA] close=300(leftover<=5)+200(막판)=500(stage1 streak=0)");
            t.Eq(56L, step.clear.clearScore, "[clearA] StageClearScore(1,3,0,curses0,boss false)=50+6+0=56");
            t.Eq("✅합격", step.clear.grade, "[clearA] overPct=104×100/101=102(<120) → ✅합격");
            t.Eq(0L, step.clear.gradeBonus, "[clearA] ✅합격 gradeBonus=0");
            t.Eq(556L, step.clear.gainedScore, "[clearA] gainedScore=56+500+0=556");
            t.Eq(7L, step.clear.clearCoin, "[clearA] clearCoin=CLEAR_COIN5+piggy_bank clearCoinBonus2=7");
            t.True(!step.clear.inDebt, "[clearA] inDebt=false(DebtStages=0)");
            t.Eq(1, run.GrowthStack, "[clearA] GrowthStack 0→1(클리어마다 +1, 상한5)");
            t.Eq(0, run.SnowStack, "[clearA] SnowStack 변화 없음(fastClear=false,leftSpins=0<2, boss아님)");
            t.Eq(stageBefore + 1, run.Stage, "[clearA] Stage+1");
        }

        // B: leftover=8(5<leftover<=10→+150), 막판 아님(leftSpins=3>=2 → fastClear) → SnowStack+1.
        private static void ClearRewardsHalfCloseAndGrade(TestCtx t)
        {
            var run = ClearSetup(71L, stageExp: 91, spinIndex: 1); // 101-18+8=91, newIdx=2,leftSpins=3
            var step = StageFlow.ProcessSpin(run, SpinMode.N);

            t.Eq(SpinStepKind.Cleared, step.kind, "[clearB] leftover8 → 클리어");
            t.Eq(8L, step.clear.leftover, "[clearB] leftover=109-101=8");
            t.True(!step.clear.lastSpinClear, "[clearB] lastSpinClear=false(newIdx2<spins5)");
            t.True(step.clear.fastClear, "[clearB] fastClear=true(leftSpins3>=2)");
            t.Eq(150L, step.clear.closeBonus, "[clearB] close=150(5<leftover8<=10), 막판아님이라 +200 없음, streak(stage1)=0");
            t.Eq(366L, step.clear.clearScore, "[clearB] StageClearScore(1,8,3,curses0,boss false)=50+16+300=366");
            t.Eq(1, run.SnowStack, "[clearB] fastClear → SnowStack 0→1");
            t.Eq(1, run.GrowthStack, "[clearB] GrowthStack 0→1(공통)");
        }

        // C: leftover=30(>10, 막판아님) → close=0(순수 클리어보상만). overPct=129(120<=..<150) → ✨우수(+50).
        private static void ClearRewardsInDebt(TestCtx t)
        {
            // C-1: 등급 경계 확인 (close=0 분기 겸용)
            var runGrade = ClearSetup(72L, stageExp: 113, spinIndex: 2); // 101-18+30=113 → newExp131
            var stepGrade = StageFlow.ProcessSpin(runGrade, SpinMode.N);
            t.Eq(30L, stepGrade.clear.leftover, "[clearC] leftover=131-101=30(>10)");
            t.Eq(0L, stepGrade.clear.closeBonus, "[clearC] close=0(leftover>10 & 막판아님, stage1 streak=0)");
            t.Eq("✨우수", stepGrade.clear.grade, "[clearC] overPct=131×100/101=129(120<=129<150) → ✨우수");
            t.Eq(50L, stepGrade.clear.gradeBonus, "[clearC] ✨우수 gradeBonus=50");
            t.Eq(310L, stepGrade.clear.clearScore, "[clearC] StageClearScore(1,30,leftSpins2,curses0,false)=50+60+200=310");
            t.Eq(360L, stepGrade.clear.gainedScore, "[clearC] gainedScore=310+0+50=360");

            // C-2: inDebt=true — gainedScore/clearCoin은 0으로 강제되지만 clearScore 자체는 그대로 계산됨.
            var runDebt = ClearSetup(73L, stageExp: 86, spinIndex: 4); // clearRewardsA와 동일 수치(leftover3,막판)
            runDebt.DebtStages = 1;
            var stepDebt = StageFlow.ProcessSpin(runDebt, SpinMode.N);
            t.True(stepDebt.clear.inDebt, "[clearD] DebtStages=1(>0) → inDebt=true");
            t.Eq(56L, stepDebt.clear.clearScore, "[clearD] clearScore는 inDebt와 무관하게 그대로 56(원시 계산값)");
            t.Eq(0L, stepDebt.clear.gainedScore, "[clearD] inDebt → gainedScore=0(강제)");
            t.Eq(0L, stepDebt.clear.clearCoin, "[clearD] inDebt → clearCoin=0(강제)");
            t.Eq(0, runDebt.DebtStages, "[clearD] 클리어 후 DebtStages 1→0(1회 소모)");
        }

        // E: 보스 스테이지(5, finals) 클리어 — StreakBonus/BOSS_COIN/grade/등급별 배수 + nextNodeForcedPrism
        // + SnowStack 보스감소(fastClear 없이 순수 감소만 관찰).
        private static void ClearRewardsBossAndSnowDecrement(TestCtx t)
        {
            var run = RunTestHelpers.NewRun(74L);
            run.Stage = 5; run.StageExp = 0; run.SpinIndex = 4; // finals boss, spins=6 → newIdx5<spins6(막판아님)
            run.SnowStack = 2;
            run.LockedNext.AddRange(new[] { "crown", "crown", "crown", "crown", "crown" }); // gained=720(잭팟)

            var step = StageFlow.ProcessSpin(run, SpinMode.N);

            t.Eq(SpinStepKind.Cleared, step.kind, "[clearE] 보스 클리어");
            t.True(step.clear.boss, "[clearE] boss=true(stage5)");
            t.Eq(165L, SpinResolver.QuotaOf(5, ModsBuilder.Build("basic", "novice", new List<string>(), new List<string>(), "")), "[clearE] 사전조건: stage5 quota=165(python 검산)");
            t.Eq(555L, step.clear.leftover, "[clearE] leftover=720-165=555");
            t.True(!step.clear.lastSpinClear, "[clearE] lastSpinClear=false(newIdx5<spins6)");
            t.True(!step.clear.fastClear, "[clearE] fastClear=false(leftSpins=6-5=1<2)");
            t.Eq(100L, step.clear.closeBonus, "[clearE] close=streakBonus(stage5>=4)=100(leftover>10,막판아님)");
            t.Eq(1960L, step.clear.clearScore, "[clearE] StageClearScore(5,555,1,curses0,boss true)=250+1110+100+500=1960");
            t.Eq("👹괴물", step.clear.grade, "[clearE] overPct=720×100/165=436(300<=436<500) → 👹괴물");
            t.Eq(500L, step.clear.gradeBonus, "[clearE] 👹괴물 gradeBonus=500");
            t.Eq(2560L, step.clear.gainedScore, "[clearE] gainedScore=1960+100+500=2560");
            t.Eq(12L, step.clear.clearCoin, "[clearE] clearCoin=BOSS_COIN(12)+0=12");
            t.True(step.clear.nextNodeForcedPrism, "[clearE] 보스클리어 → 다음 노드 PRISM 확정");
            t.Eq(1, run.SnowStack, "[clearE] fastClear없이 boss만 → SnowStack 2→1(순감소)");
            t.Eq(1, run.GrowthStack, "[clearE] GrowthStack 0→1(보스 무관 공통 규칙)");
        }

        // RollNextNodes: nextStage<6이면 CURSE/RISK가 풀 자체에 없어(구조적 보장) 어떤 시드로도 절대 등장
        // 불가 — 매 시행 검증. nextStage>=6이면 풀에 포함되어 셔플로 뽑힐 수 있음 — 다수 시행 중 최소
        // 1회 이상 등장함을 확률적으로 확인(2/7 풀에서 2개 추출 시 매번 배제될 확률은 극히 낮음, 60회
        // 반복 시 누락 확률 사실상 0 — 표준 통계 스모크 테스트 관례).
        private static void RollNextNodesStageGate(TestCtx t)
        {
            bool sawCurseOrRiskLowStage = false;
            for (long seed = 1; seed <= 20; seed++)
            {
                var run = RunTestHelpers.NewRun(seed);
                run.Stage = 1; run.StageExp = 1_000_000; // 확실한 클리어(어떤 롤이 나와도 quota 초과)
                var step = StageFlow.ProcessSpin(run, SpinMode.N);
                if (step.kind != SpinStepKind.Cleared) continue;
                t.Eq(3, step.clear.nodeOptions.Count, "[rollNodes] nextStage<6: 노드 옵션 항상 3개");
                t.True(step.clear.nodeOptions.Contains(NodeKind.Augment), "[rollNodes] nextStage<6: AUGMENT 필수 포함");
                if (step.clear.nodeOptions.Contains(NodeKind.Curse) || step.clear.nodeOptions.Contains(NodeKind.Risk))
                    sawCurseOrRiskLowStage = true;
            }
            t.True(!sawCurseOrRiskLowStage, "[rollNodes] nextStage=2(<6): 20회 시행 동안 CURSE/RISK 단 한 번도 등장 안 함(풀에 없음, 구조적 보장)");

            bool sawCurseOrRiskHighStage = false;
            for (long seed = 1; seed <= 60; seed++)
            {
                var run = RunTestHelpers.NewRun(1000L + seed);
                run.Stage = 5; run.StageExp = 1_000_000; // clearedStage=5 → nextStage=6(>=6 분기 진입)
                var step = StageFlow.ProcessSpin(run, SpinMode.N);
                if (step.kind != SpinStepKind.Cleared) continue;
                t.Eq(3, step.clear.nodeOptions.Count, "[rollNodes] nextStage>=6: 노드 옵션 항상 3개");
                t.True(step.clear.nodeOptions.Contains(NodeKind.Augment), "[rollNodes] nextStage>=6: AUGMENT 필수 포함");
                if (step.clear.nodeOptions.Contains(NodeKind.Curse) || step.clear.nodeOptions.Contains(NodeKind.Risk))
                    sawCurseOrRiskHighStage = true;
            }
            t.True(sawCurseOrRiskHighStage, "[rollNodes] nextStage=6(>=6): 60회 시행 중 CURSE/RISK가 최소 1회 이상 등장(풀에 포함되어 있다는 통계적 확인)");
        }
    }
}
