using System;
using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 스핀 집계 효과 — Kotlin data class Mods(SlotV2Engine.kt L138-175) 전사.
    // 필드명은 Kotlin과 동일하게(camelCase) 유지 — fx 딕셔너리 키(ENGINE_PORT_DESIGN.md "계약 확정 이력"의
    // fx 명명 규칙)가 이 필드명을 문자열로 그대로 참조하므로 이름이 어긋나면 해석기가 깨진다.
    //
    // [죽은 필드 — 01_engine.md §11-14 / ENGINE_PORT_DESIGN.md 부록] flatScore(항상 0, buildMods 반환문에
    // 미포함), overkillScoreMul/carryoverPct(선언만, 전체 미사용) — 콘텐츠가 값을 바꾸지 않는다는 사실까지
    // 포함해 그대로 이식한다. 주의: overkillScoreMul/carryoverPct는 ApplyScalarFx에 case가 없어 콘텐츠가
    // fx 키로 쓰면 예외가 난다(원본에도 사용 콘텐츠 없음 — 확장하려면 해석기 case부터 추가할 것).
    // skullPenaltyMul은 위 주석이 예전에 "항상 1.0(죽은 필드)"라 적었지만 웹 파리티 P7-2(WEB_PARITY_
    // DESIGN.md §1-A #19 2/4 슬라이스)부터는 죽지 않았다 — 심화 런의 전공(Archetypes 강령학파 t2)·
    // 심볼퍽(sa_expand_build/sr_big_bag)이 DeepRunHooks.ApplyDeepMods를 통해 이 필드에 직접 곱한다
    // (ApplyScalarFx의 fx 문자열 키로는 여전히 도달 불가 — 일반모드 콘텐츠는 여전히 이 필드를 안 건드림).
    public sealed class Mods
    {
        public double expMul = 1.0;
        public double scoreMul = 1.0;
        public double coinMul = 1.0;
        public int flatExp = 0;
        public int flatScore = 0; // [죽은 필드]
        public int bonusSpins = 0;
        public int skullExp = 0;
        public double skullPenaltyMul = 1.0; // [죽은 필드] 항상 1.0
        public double setExpMul = 1.0;
        public readonly Dictionary<Sym, int> perSymbolExp = new Dictionary<Sym, int>();
        public double firstSpinExpMul = 1.0;
        public double lastSpinExpMul = 1.0;
        public double rareWeightMul = 1.0;
        public double overkillScoreMul = 1.0; // [죽은 필드]
        public int carryoverPct = 0;          // [죽은 필드]
        public readonly Dictionary<string, int> tagExpBonus = new Dictionary<string, int>();
        public double centerExpMul = 1.0;
        public double endsMatchExpMul = 1.0;
        public int adjacentSameExp = 0;
        public readonly Dictionary<Sym, double> symbolWeightMul = new Dictionary<Sym, double>();
        public readonly Dictionary<Sym, double> weightAdd = new Dictionary<Sym, double>();
        public readonly Dictionary<Sym, int> perSymbolScore = new Dictionary<Sym, int>();
        public double quotaMul = 1.0;
        public int clearCoinBonus = 0;
        public int skullScoreBonus = 0;
        public int perSkullExp = 0;
        public double skull3ScoreMul = 1.0;
        public double rareBurstExpMul = 1.0;
        public double rareBurstScoreMul = 1.0;
        public double twoSetBonusMul = 1.0;
        public double set3ExpMul = 1.0;
        public double set4ScoreMul = 1.0;
        public double perfectShapeExpMul = 1.0;

        // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14) 신규 필드 ──────────────────────────
        // phoenix_thesis(불사조논문, engine.js:111/930-933) — 요구치 50% 미만 게이트 통과 시 그 스핀
        // EXP ×2. SpinResolver.Evaluate가 perfectShapeExpMul 직후에 곱한다.
        public double cliffBurstExpMul = 1.0;
        // 상점 빌드 증강(discount/thrifty/item_bag/vip, engine.js:113) — 상점/가방 화면 자체는 P4
        // 대상이라 이 슬라이스는 필드만 채운다(소비처는 후속 슬라이스).
        public double shopPriceMul = 1.0;
        public double itemPriceMul = 1.0;
        public int itemCapBonus = 0;
        public int shopSlotBonus = 0;
        public int shopRerollDelta = 0;

        // ── 웹 파리티 P7-2(WEB_PARITY_DESIGN.md §1-A #19 2/4 슬라이스 B) 신규 필드 ──────────────
        // 배치 G 계열 아키타입(전공) — 웹 engine.js:96-99 `deepFamilyExpMul/ScoreMul/CoinMul`. 심화
        // 런에서만 채워진다(DeepRunHooks.ApplyDeepMods, AscRunHooks.ApplyRunAscMods 직후 호출) — 일반
        // 모드는 항상 빈 dict라 SpinResolver.Evaluate의 ArchMul이 0을 반환해 무회귀. key=계열 base id
        // (cherry/book/gem/skull/coin/flame), value=곱셈 증가분(예 0.15=+15%).
        public readonly Dictionary<string, double> deepFamilyExpMul = new Dictionary<string, double>();
        public readonly Dictionary<string, double> deepFamilyScoreMul = new Dictionary<string, double>();
        public readonly Dictionary<string, double> deepFamilyCoinMul = new Dictionary<string, double>();

        // Opus 2차검수(P7-2, 2026-08-09) 필수①③ — 웹 game.js:454 `deepTagMul: {...r.deepTagBuff}` +
        // engine.js:96 `deepTagMul: {}`(태그별 곱셈 증가분, 정비소 '태그 강화' + 심볼퍽 tagBuff류 합산).
        // SpinResolver.Evaluate가 셀 태그 합을 [-0.5,0.5]로 클램프해 EXP에 곱한다(웹 evaluate:682-683).
        public readonly Dictionary<string, double> deepTagMul = new Dictionary<string, double>();

        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스, 웹 game.js:1117/1122/1135
        // `r.lastMods.feverReachFix`) — 피버 중 리치 보정 확률 가산분(DEEP.FEVER_REACH_FIX=0.15).
        // 웹은 스핀 "결산 이후"(post-Evaluate) run.lastMods를 직접 mutate해 이 필드를 채운다(스핀 도중
        // 값이 아니라 "다음 스핀의 리치표식이 참조할 상태" 캐시) — DeepRunHooks.ProcessDeepSpinFollowups가
        // 이 스핀에 실제로 쓰인 로컬 mods 인스턴스(ResolveSpin이 이 함수를 호출할 때 넘기는 그 mods —
        // run.LastMods는 이 시점에 아직 갱신 전이라 직접 읽고 쓴다)에 사후 대입한다.
        public double feverReachFix = 0.0;

        // 웹 파리티 P7-3(WEB_PARITY_DESIGN.md §1-A #19 3/4 슬라이스, 웹 game.js `_mods()` 심화블록이
        // 세우는 `mods.deepMode`) — SpinResolver.Evaluate의 §9.0 J1 잭팟 태그 블록 게이트. 일반 런은
        // 항상 false(DeepRunHooks.ApplyDeepMods가 심화 런에서만 true로 세운다).
        public bool deepMode = false;

        // 웹 파리티 P8(WEB_PARITY_DESIGN.md §2 결정 로그 확정 항목② "deepFamilyBridge", 웹 game.js:461
        // `mods = { ...mods, deepFamilyBridge: true, deepMode: true }`) — 심화 런에서 항상 deepMode와
        // 나란히 true(퍽/주머니 상태 무관, 심화 런이면 무조건). SpinResolver.Evaluate의 famBridge가
        // 이 플래그로 게이팅한다(상위계열 셀에 하위 base 심볼의 perSymbolExp/Score 보너스를 합산).
        // 일반 런은 항상 false(무회귀).
        public bool deepFamilyBridge = false;

        // ── 웹 파리티 P7-3b(WEB_PARITY_DESIGN.md §1-A #19 "Sp 신규 51종 전면 이식") ──────────────
        // 심볼퍽 emptyScore/emptyExp(Content/SymPerks.cs) — evaluate 빈칸활용 블록이 소비. 웹
        // game.js:469-470 `deepEmptyScore: (mods.deepEmptyScore||0)+sp.emptyScore` 그대로.
        // DeepRunHooks.ApplyDeepMods가 채운다. 일반 모드는 항상 0(무회귀).
        public double deepEmptyScore = 0.0;
        public double deepEmptyExp = 0.0;
        // 전설봉인함(sr_legend_seal 심볼퍽 + 주머니 전설(👑/7️⃣/🌈) 보유) — 웹 game.js:476-481. 프리즘/
        // 럭키7의 랜덤 효과를 "최선 효과 고정"으로 안정화(SpinResolver.Evaluate PRISM_SYM/LUCKY7 분기).
        public bool legendStable = false;
        // ⛓족쇄(SHACKLE) 영구 저주 — 웹 game.js:509-511 `shackleActive`. 보스 스테이지 스핀-1
        // (SpinResolver.EffSpins)·클리어코인+4(DeepRunHooks.ApplyDeepMods가 clearCoinBonus에 직접 가산).
        public bool shackleActive = false;

        // Opus 2차검수(P7-2) 필수⑤[권장] — DeepRunHooks.ApplyDeepMods 이중 호출 안전장치. 이 함수는
        // "최종 mods에 딱 1회" 주입되는 게 계약이라(호출부 헤더 주석) 같은 Mods 인스턴스에 실수로
        // 두 번 불려도(예: 향후 리팩터로 호출 지점이 늘어날 때) 아키타입/심볼퍽 배수가 중복 누적되지
        // 않도록 방어한다. Clone() 이후에도 이 표식은 그대로 넘어가야 안전해 아래서 함께 복사한다.
        internal bool DeepModsApplied = false;

        // Kotlin data class copy()의 "새 Map 인스턴스" 관용구 대응 — 01_engine.md §11-8: C# Dictionary는
        // 기본 가변(mutable)이라 참조를 공유한 채 두면 한 스핀의 수정이 다른 곳에 누출된다. applyItemMods/
        // applyPassiveDevice처럼 "base 위에 오버레이"가 필요한 지점은 전부 Clone() 후 수정해야 한다.
        public Mods Clone()
        {
            var m = new Mods
            {
                expMul = expMul, scoreMul = scoreMul, coinMul = coinMul,
                flatExp = flatExp, flatScore = flatScore, bonusSpins = bonusSpins,
                skullExp = skullExp, skullPenaltyMul = skullPenaltyMul, setExpMul = setExpMul,
                firstSpinExpMul = firstSpinExpMul, lastSpinExpMul = lastSpinExpMul,
                rareWeightMul = rareWeightMul, overkillScoreMul = overkillScoreMul, carryoverPct = carryoverPct,
                centerExpMul = centerExpMul, endsMatchExpMul = endsMatchExpMul, adjacentSameExp = adjacentSameExp,
                quotaMul = quotaMul, clearCoinBonus = clearCoinBonus, skullScoreBonus = skullScoreBonus,
                perSkullExp = perSkullExp, skull3ScoreMul = skull3ScoreMul,
                rareBurstExpMul = rareBurstExpMul, rareBurstScoreMul = rareBurstScoreMul,
                twoSetBonusMul = twoSetBonusMul, set3ExpMul = set3ExpMul, set4ScoreMul = set4ScoreMul,
                perfectShapeExpMul = perfectShapeExpMul,
                cliffBurstExpMul = cliffBurstExpMul, shopPriceMul = shopPriceMul, itemPriceMul = itemPriceMul,
                itemCapBonus = itemCapBonus, shopSlotBonus = shopSlotBonus, shopRerollDelta = shopRerollDelta,
                DeepModsApplied = DeepModsApplied,
                feverReachFix = feverReachFix,
                deepMode = deepMode,
                deepFamilyBridge = deepFamilyBridge,
                deepEmptyScore = deepEmptyScore, deepEmptyExp = deepEmptyExp,
                legendStable = legendStable, shackleActive = shackleActive,
            };
            foreach (var kv in perSymbolExp) m.perSymbolExp[kv.Key] = kv.Value;
            foreach (var kv in tagExpBonus) m.tagExpBonus[kv.Key] = kv.Value;
            foreach (var kv in symbolWeightMul) m.symbolWeightMul[kv.Key] = kv.Value;
            foreach (var kv in weightAdd) m.weightAdd[kv.Key] = kv.Value;
            foreach (var kv in perSymbolScore) m.perSymbolScore[kv.Key] = kv.Value;
            foreach (var kv in deepFamilyExpMul) m.deepFamilyExpMul[kv.Key] = kv.Value;
            foreach (var kv in deepFamilyScoreMul) m.deepFamilyScoreMul[kv.Key] = kv.Value;
            foreach (var kv in deepFamilyCoinMul) m.deepFamilyCoinMul[kv.Key] = kv.Value;
            foreach (var kv in deepTagMul) m.deepTagMul[kv.Key] = kv.Value;
            return m;
        }
    }

    // 런 컨텍스트 — Kotlin data class RunCtx(SlotV2Engine.kt L1712-1727). 신규 16종 증강의 stage/남은스핀/
    // 스택/저주개수 조건부 효과 계산용(읽기 전용). 기본값 = 무효(조건 미충족, 기존 동작과 동일).
    public sealed class RunCtx
    {
        public int stage = 0;
        public int spinIndex = 0;
        public int spinsPerStage = Formulas.SPINS_PER_STAGE;
        public long stageExp = 0;
        public long quota = 0;
        public int growthStack = 0;
        public int snowStack = 0;
        public int curseCount = 0;
        public int unluckyGauge = 0;
        public bool boss = false;
        // 웹 파리티 P3-4(engine.js:136 makeCtx `coins: ctx.coins ?? 99`) — bankrupt(파산 졸업생) 캐릭터
        // 조건부 효과(코인0=EXP+50%/코인10+=−20%)용.
        // [주석 정정 — 웹 파리티 P3.5, WEB_PARITY_DESIGN.md §2-(T) 후속③] 예전 버전의 이 주석은
        // 기본값 99가 "미설정 호출부에서도 안전한 무해 기본값"이라 적었지만 부정확했다 — bankrupt
        // 캐릭터 기준 `coins>=10`이면 `expMul *= 0.8`이 실제로 걸린다(99는 결코 "중립"이 아니라
        // "패널티 조건을 충족하는 값"이다). 현재 무해한 이유는 딱 하나, ctx 없이 호출되는 "프리패스"
        // 지점(예: ModsBuilder.Build의 ctx 생략 1차 재계산, GameSession.PreviewQuotaSpins 등)이 그
        // 결과에서 bonusSpins/SpinsPerStage/QuotaOf만 뽑아 쓰고 expMul은 읽지 않기 때문이다 — 즉
        // "필드 자체가 무해"가 아니라 "현재 소비처가 우연히 그 필드를 안 읽어서 무해"인 훨씬 좁고
        // 깨지기 쉬운 보장이다. 향후 누군가 ctx-less Build() 결과의 expMul을 읽는 소비처를 추가하면
        // bankrupt 런에서 조용히 잘못된 페널티가 섞여 들어갈 수 있다(ItemUse.UseRetakeForm은 이번
        // 슬라이스에서 ctx 포함 2단계 빌드로 고쳤다 — SpinResolver.RunCtxOf 참조).
        public long coins = 99;

        public bool IsFirstSpin => spinIndex == 0;
        // Kotlin: spinsPerStage in 1..(spinIndex+1)  →  1 <= spinsPerStage <= spinIndex+1.
        public bool IsLastSpin => spinsPerStage >= 1 && spinsPerStage <= spinIndex + 1;
        public int SpinsLeft => Math.Max(spinsPerStage - spinIndex, 0);
    }

    // 머신+캐릭터+perk(+저주+세트) → 스핀 Mods 빌더. Kotlin object SlotV2Engine의 buildMods(L1730-2026)/
    // applyItemMods(L1494-1559)/applyPassiveDevice(L1121-1127)/spinsPerStage(L2364)/cmdCoinCost(L2370-2381)
    // 전사. ENGINE_PORT_DESIGN.md "계약 확정 이력"의 fx 명명 규칙(Kotlin Mods 필드명 + 점 표기 오버레이)을
    // 그대로 해석기로 구현한다 — Perks/Items/Sets/Devices(S2) 콘텐츠 파일의 fx 딕셔너리가 이미 Kotlin과
    // 대조 검증된 1:1 데이터이므로, 172개 case를 다시 손으로 베끼는 대신 데이터 기반으로 적용한다(단일
    // 소스 유지 — Perks.cs 등이 틀리면 여기도 같이 틀려야 스펙-Kotlin 불일치 테스트가 잡아낸다는 뜻이기도
    // 하다. Mods.cs 자체 로직 오류와 콘텐츠 데이터 오류를 분리해서 보는 게 검수 포인트).
    //
    // 단, 캐릭터 효과는 Perk.fx 같은 데이터가 없다(Character 클래스에 fx 필드가 없음, ContentTypes.cs 계약
    // 그대로) — ENGINE_PORT_DESIGN.md 작업 지시 "캐릭터(id별 case)"대로 이 파일에서 직접 switch로 구현한다.
    public static class ModsBuilder
    {
        // 신규 16종 중 buildMods가 RunCtx로 조건분기/동적계산하는 11종 — Perks.cs의 "cond.*" 접두 규약
        // (§Perks.cs 헤더 주석)대로, 이 id들은 일반 fx 해석기(필드명 그대로 적용)를 타지 않고 아래
        // ApplyCtxConditionalPerk에서 개별 처리한다. fate_bell은 buildMods 자체가 무효과(빈 fx)라 일반
        // 해석기를 통과해도 아무 일도 일어나지 않으므로 이 집합에 넣지 않는다(§Perks.cs L1923 주석).
        private static readonly HashSet<string> CtxConditionalIds = new HashSet<string>
        {
            "early_prep", "early_adapt", "growth_log", "snowball",
            "fortune_check", "luck_accum", "fate_burst",
            "late_focus", "cliff_focus",
            "sacrifice", "black_diploma",
            // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14) — 신규 4종(증강2·유물2)도 curseCount/boss/
            // quota%로 동적 계산되는 조건부 효과라 이 집합에 추가한다(sacrifice/abyss_lore 등과 동일 패턴).
            "curse_grad", "abyss_lore", "black_grad_photo", "phoenix_thesis",
        };

        // ── 1.5 buildMods (Kotlin L1730-2026) ──────────────────────────────────
        // levels: 웹 파리티 P3-3(WEB_PARITY_DESIGN.md §1-A #12) — 웹 buildMods(...,levels) 7번째 인자
        // (game.js:445 `E.buildMods(..., r.perkLevels)`) 대응. RunState.PerkLevels를 그대로 넘기면
        // AUG_LEVELS(Content/AugLevels.cs) 등록 증강의 Lv2/Lv3 델타가 반영된다 — null/id 미존재는
        // Lv1(무보정)과 동치(기존 호출부 호환, levels 생략 시 전부 Lv1로 취급).
        public static Mods Build(
            string machineId, string charId,
            IReadOnlyList<string> perkIds, IReadOnlyList<string> curseIds,
            string deviceId, RunCtx ctx = null, IReadOnlyDictionary<string, int> levels = null)
        {
            perkIds ??= Array.Empty<string>();
            curseIds ??= Array.Empty<string>();
            ctx ??= new RunCtx();

            var machine = Machines.ById(machineId);
            var m = new Mods();
            // 머신 weightMul/weightAdd를 시작값으로 복사(Kotlin `HashMap(m.weightMul)`/`HashMap(m.weightAdd)`,
            // L1744-1745) — 이후 wmul()/wadd() 헬퍼(및 fx 해석기)가 이 위에 곱/가산으로 누적한다.
            foreach (var kv in machine.weightMul) m.symbolWeightMul[kv.Key] = kv.Value;
            foreach (var kv in machine.weightAdd) m.weightAdd[kv.Key] = kv.Value;

            // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, engine.js:164-168) — 후반 슬롯머신 특수
            // 모드(가중치 외 효과). Machine에 fx 데이터가 없어 id별 switch로 직접 구현(캐릭터와 동일 패턴).
            switch (machineId)
            {
                case "nightmare": m.skullExp += 6; break;                              // 악몽: 해골도 EXP 제공
                case "throne": Pss(m, Sym.Crown, 40); m.expMul *= 0.9; break;           // 빈 왕좌: 왕관 점수↑·기본 EXP-10%
                case "broke": Pse(m, Sym.Cherry, 2); Pse(m, Sym.Book, 2); Pse(m, Sym.Star, 2); m.coinMul = 0; break; // 파산: 코인 없음·값심볼 강화
            }

            // ── 캐릭터 (L1755-1773) — Character에 fx 데이터가 없어 id별 switch로 직접 구현 ──
            // gambler(도박꾼)·honor(수석졸업생)는 Kotlin when-block에 케이스가 없다(01_engine.md §4 표,
            // 부록A-3) — desc의 "스테이지당 1회 무료 재굴림"(gambler)/"실버 증강 1개로 시작"(honor)은
            // 서비스 레이어(RunController/StageFlow 런 시작 훅, S4)가 별도로 처리해야 한다. 여기서는
            // scoreMod(Characters.cs)만 유효하고 buildMods 기여는 0이다 — Kotlin과 동일하게 아무 것도 안 함.
            switch (charId)
            {
                case "novice": m.quotaMul *= 0.92; break;
                case "scholar": Pse(m, Sym.Book, 2); m.clearCoinBonus += 2; break;
                case "parttime": m.firstSpinExpMul *= 0.8; break;
                case "farmer": Pse(m, Sym.Cherry, 1); m.rareWeightMul *= 0.9; break;
                case "jeweler": Pss(m, Sym.Gem, 25); break;
                case "cultist": m.skullExp += 3; break;
                case "crowncol": Pss(m, Sym.Crown, 30); Wmul(m, Sym.Crown, 1.5); break;
                case "lucky": m.rareWeightMul *= 1.25; break;
                case "highroller": Pss(m, Sym.Gem, 25); break;
                case "monk": m.bonusSpins -= 1; m.quotaMul *= 0.9; break;
                case "alchemist": m.coinMul *= 1.25; m.clearCoinBonus += 3; break;
                case "daredevil":
                    // 막판형: 기본 EXP+10%·요구+20% + 남은≤2 +35%·막스핀 +60%(막스핀이면 35% 미적용, else-if).
                    m.expMul *= 1.1; m.quotaMul *= 1.2;
                    if (ctx.IsLastSpin) m.expMul *= 1.6;
                    else if (ctx.SpinsLeft <= 2) m.expMul *= 1.35;
                    break;
                case "prodigy": m.expMul *= 1.12; break;
                // "novice"~"prodigy" 외: gambler/honor는 케이스 없음(위 주석), 그 외 미지정 id도 무효과.
                // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, engine.js:188-190) — 후반 캐릭터 3종 ──
                case "regent": // 왕의 대리인: 왕관 점수2배·등장↑·기본 EXP -10%
                    Pss(m, Sym.Crown, 60); Wmul(m, Sym.Crown, 2.0); Wadd(m, Sym.Crown, 1.5); m.expMul *= 0.9;
                    break;
                case "bankrupt": // 파산 졸업생: 코인0 EXP+50%·코인10+ -20%
                    if (ctx.coins <= 0) m.expMul *= 1.5;
                    else if (ctx.coins >= 10) m.expMul *= 0.8;
                    break;
                case "abyss_scholar": // 심연 장학생: 보스 EXP+30%·일반 -10%
                    if (ctx.boss) m.expMul *= 1.3; else m.expMul *= 0.9;
                    break;
            }

            // ── 증강+유물 (perkIds 루프, L1775-1932) ──
            for (int i = 0; i < perkIds.Count; i++)
            {
                var id = perkIds[i];
                if (CtxConditionalIds.Contains(id)) { ApplyCtxConditionalPerk(m, id, ctx); continue; }
                var perk = Perks.ById(id);
                if (perk == null) continue; // Kotlin when(id){...}의 무명 case 무시(01_engine.md §11-12)
                ApplyFx(m, perk.fx);
            }

            // ── 증강 레벨업 델타(Lv2/Lv3) — 웹 파리티 P3-3(engine.js:368-375 buildMods 증강 레벨업 델타
            // 루프). AUG_LEVELS 미등록 id(프리즘 등)는 TryGetDelta가 false를 반환해 자동 스킵된다.
            // Lv3는 Lv2+Lv3 델타를 모두 누적 적용(곱/가산 연산은 위치 무관이라 curses/sets 루프보다
            // 앞뒤 어디에 둬도 최종 결과는 같다 — perkIds 원본 효과 바로 다음에 두는 게 가독성상 자연스럽다).
            if (levels != null)
            {
                for (int i = 0; i < perkIds.Count; i++)
                {
                    var id = perkIds[i];
                    int lv = levels.TryGetValue(id, out var lvv) ? lvv : 1;
                    if (lv < 2) continue;
                    if (AugLevels.TryGetDelta(id, 2, out var d2)) ApplyFx(m, d2);
                    if (lv >= 3 && AugLevels.TryGetDelta(id, 3, out var d3)) ApplyFx(m, d3);
                }
            }

            // ── 저주 (curseIds 루프, L1935-1951) — 전부 일반 fx(ctx 조건부 없음) ──
            for (int i = 0; i < curseIds.Count; i++)
            {
                var perk = Perks.ById(curseIds[i]);
                if (perk == null) continue;
                ApplyFx(m, perk.fx);
            }

            // ── 세트 효과 (L1954-1998) — perkIds 보유 + reqChar/reqMachine/reqDevice 게이트 ──
            // [원본 버그 유지] set_align/set_perfect_calc는 requires가 완전히 동일(center,twins,chain)한데
            // 상호배제가 없어 둘 다 동시 발동한다(01_engine.md §6 표 각주·부록A-4) — 아래 루프는 Sets.All을
            // 전부 순회하며 조건 충족 세트를 전부 적용하므로 자연히 원본과 동일하게 이중 발동한다.
            var pset = new HashSet<string>(perkIds);
            var sets = Sets.All;
            for (int i = 0; i < sets.Length; i++)
            {
                var s = sets[i];
                if (!ContainsAll(pset, s.requires)) continue;
                if (!string.IsNullOrEmpty(s.reqChar) && s.reqChar != charId) continue;
                if (!string.IsNullOrEmpty(s.reqMachine) && s.reqMachine != machineId) continue;
                if (!string.IsNullOrEmpty(s.reqDevice) && s.reqDevice != deviceId) continue;
                ApplyFx(m, s.fx);
            }

            // ── 저주 스택 임계 보너스 (L2001-2004) ──
            int nCurses = curseIds.Count;
            if (nCurses >= 3) m.skullExp += 2;
            if (nCurses >= 5) m.scoreMul *= 1.12;
            if (nCurses >= 7) m.scoreMul *= 1.12; // 5개 조건과 별개 if — 7개면 중첩 적용(의도된 가속 보상)

            // ── 캐릭터 후처리 (L2006-2008, perk/저주 집합 의존이라 루프 끝난 뒤 처리) ──
            if (charId == "cultist" && curseIds.Count > 0) m.scoreMul *= 1.0 + 0.08 * curseIds.Count;
            if (charId == "minimalist")
            {
                int relicCount = 0;
                for (int i = 0; i < perkIds.Count; i++)
                {
                    var p = Perks.ById(perkIds[i]);
                    if (p != null && p.cat == PCat.RELIC) relicCount++;
                }
                if (relicCount <= 3) m.expMul *= 1.25;
            }

            return m;
        }

        // ctx 조건부 11종 개별 처리 — Perks.cs의 "cond.*" fx 값(및 fate_burst의 boss/nonBoss 분리값)을
        // 읽어 Kotlin buildMods L1911-1931과 동일한 조건식으로 적용한다. 값은 전부 Perks.cs 데이터에서
        // 읽으므로 상수를 이 파일에 중복 하드코딩하지 않는다(콘텐츠가 유일한 소스).
        private static void ApplyCtxConditionalPerk(Mods m, string id, RunCtx ctx)
        {
            var perk = Perks.ById(id);
            if (perk == null) return;
            var fx = perk.fx;
            switch (id)
            {
                case "early_prep": // if (ctx.stage in 1..3) expMul *= 1.15
                case "early_adapt": // if (ctx.stage in 1..5) expMul *= 1.12
                    if (ctx.stage >= 1 && ctx.stage <= (int)fx["cond.stageMax"]) m.expMul *= fx["cond.expMul"];
                    break;
                case "growth_log": // firstSpinExpMul *= (1 + 0.08*clamp(growthStack,0,5))
                {
                    int stack = Clamp(ctx.growthStack, 0, (int)fx["cond.stackMax"]);
                    m.firstSpinExpMul *= 1.0 + fx["cond.stackBase"] * stack;
                    break;
                }
                case "snowball": // expMul *= (1 + 0.12*clamp(snowStack,0,4))
                {
                    int stack = Clamp(ctx.snowStack, 0, (int)fx["cond.stackMax"]);
                    m.expMul *= 1.0 + fx["cond.stackBase"] * stack;
                    break;
                }
                case "fortune_check": // if (ctx.isFirstSpin) rareWeightMul *= 1.2
                    if (ctx.IsFirstSpin) m.rareWeightMul *= fx["cond.firstSpinRareMul"];
                    break;
                case "luck_accum": // if (ctx.unluckyGauge >= 3) rareWeightMul *= 1.3
                    if (ctx.unluckyGauge >= (int)fx["cond.gaugeThreshold"]) m.rareWeightMul *= fx["cond.rareMul"];
                    break;
                case "fate_burst": // rareBurstExpMul *= (boss?1.7:1.8); rareBurstScoreMul *= 1.5 (조건 없음)
                    m.rareBurstExpMul *= ctx.boss ? fx["rareBurstExpMul.boss"] : fx["rareBurstExpMul.nonBoss"];
                    m.rareBurstScoreMul *= fx["rareBurstScoreMul"];
                    break;
                case "late_focus": // if (ctx.spinsLeft in 1..2) expMul *= 1.10
                    if (ctx.SpinsLeft >= 1 && ctx.SpinsLeft <= (int)fx["cond.spinsLeftMax"]) m.expMul *= fx["cond.expMul"];
                    break;
                case "cliff_focus": // if (isLastSpin && quota>0 && stageExp < quota*0.6) lastSpinExpMul *= 1.8
                    if (ctx.IsLastSpin && ctx.quota > 0 && ctx.stageExp < (long)(ctx.quota * fx["cond.quotaPct"]))
                        m.lastSpinExpMul *= fx["cond.lastSpinExpMul"];
                    break;
                case "sacrifice": // expMul *= (1 + 0.06*curseCount); clearCoinBonus -= 1 (후자는 무조건)
                    m.expMul *= 1.0 + fx["cond.curseExpBase"] * ctx.curseCount;
                    if (fx.TryGetValue("clearCoinBonus", out var ccb)) m.clearCoinBonus += (int)ccb;
                    break;
                case "black_diploma": // if (curseCount>=5) { expMul*=1.6; scoreMul*=1.3; bonusSpins-=1 }
                    if (ctx.curseCount >= (int)fx["cond.curseThreshold"])
                    {
                        m.expMul *= fx["cond.expMul"];
                        m.scoreMul *= fx["cond.scoreMul"];
                        m.bonusSpins += (int)fx["cond.bonusSpinsDelta"];
                    }
                    break;
                // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14) — 신규 4종 ──
                case "curse_grad": // scoreMul *= (1 + 0.15*curseCount) — 무조건 대입(curseCount=0이면 무영향)
                case "black_grad_photo": // scoreMul *= (1 + 0.12*curseCount) — curse_grad와 동일 키, 값만 다름
                    m.scoreMul *= 1.0 + fx["cond.curseScoreBase"] * ctx.curseCount;
                    break;
                case "abyss_lore": // if (ctx.boss) expMul *= 1.5
                    if (ctx.boss) m.expMul *= fx["cond.bossExpMul"];
                    break;
                case "phoenix_thesis": // if (stage>0 && quota>0 && stageExp < quota*0.5) cliffBurstExpMul *= 2.0
                    if (ctx.stage > 0 && ctx.quota > 0 && ctx.stageExp < (long)(ctx.quota * fx["cond.quotaPct"]))
                        m.cliffBurstExpMul *= fx["cond.cliffBurstExpMul"];
                    break;
            }
        }

        // ── applyItemMods (Kotlin L1494-1559) — NEXTSPIN/PHASE 아이템 오버레이 ──
        // INSTANT는 여기서 다루지 않는다(원본도 "서비스가 즉시 처리", 01_engine.md §7.3). dev_coin(🪙투입)
        // 효과도 원본처럼 이 함수를 통해 적용된다(Kotlin이 applyItemMods 안에 "dev_coin" case를 둠, L1551)
        // — 호출부(SpinResolver)가 armItems 목록에 필요 시 "dev_coin"을 섞어 넣는다.
        public static Mods ApplyItemMods(Mods baseMods, IReadOnlyList<string> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) return baseMods;
            var m = baseMods.Clone();
            for (int i = 0; i < itemIds.Count; i++)
            {
                var id = itemIds[i];
                // L1551, 장치 오버레이지만 이 함수가 처리. 배수는 Devices.cs fx가 단일 소스(하드코딩 금지 — Opus S3 중요-2).
                if (id == "dev_coin") { m.expMul *= Devices.ById("dev_coin").fx["expMul"]; continue; }
                var item = Items.ById(id);
                if (item == null) continue;
                ApplyFx(m, item.fx);
            }
            return m;
        }

        // ── applyPassiveDevice (Kotlin L1121-1127) ──
        // dev_safe(하한 보장)·dev_subreel의 reel+1(6칸 확장) 자체는 여기서 처리되지 않는다(원본 주석 그대로
        // — 서비스/SpinResolver가 별도 처리). 이 함수는 Devices.cs의 fx 딕셔너리(expMul/symbolWeightMul.*/
        // weightAdd.* 오버레이)만 그대로 적용한다.
        public static Mods ApplyPassiveDevice(Mods baseMods, string deviceId)
        {
            var dev = Devices.ById(deviceId);
            if (dev == null || dev.kind != "PASSIVE") return baseMods;
            var m = baseMods.Clone();
            ApplyFx(m, dev.fx);
            return m;
        }

        // ── spinsPerStage / cmdCoinCost (Kotlin L2364, L2370-2381) ──
        public static int SpinsPerStage(Mods mods) =>
            Math.Max(Formulas.SPINS_PER_STAGE + mods.bonusSpins, Formulas.MIN_SPINS);

        public static int CmdCoinCost(SpinMode mode, bool boss)
        {
            int baseCost = mode switch
            {
                SpinMode.Focus => Formulas.CMD_COST_FOCUS,
                SpinMode.Last => Formulas.CMD_COST_LAST,
                SpinMode.Pray => Formulas.CMD_COST_PRAY,
                SpinMode.Allin => Formulas.CMD_COST_ALLIN,
                _ => 0,
            };
            if (baseCost == 0) return 0;
            int withBoss = baseCost + (boss ? Formulas.CMD_COST_BOSS_SURCHARGE : 0);
            return Math.Min(withBoss, Formulas.CMD_COST_MAX);
        }

        // ── fx 점 표기 해석기 ────────────────────────────────────────────────
        // "cond."로 시작하는 키는 여기서 처리하지 않는다(ApplyCtxConditionalPerk 전용 — 호출측이 이미
        // CtxConditionalIds로 걸러서 이 함수까지 오지 않지만, Sets/Items/Devices에는 cond.* 키가 존재하지
        // 않으므로 방어적으로만 스킵한다).
        internal static void ApplyFx(Mods m, IReadOnlyDictionary<string, double> fx)
        {
            if (fx == null) return;
            foreach (var kv in fx)
            {
                string key = kv.Key;
                double v = kv.Value;
                if (key.StartsWith("cond.", StringComparison.Ordinal)) continue;
                int dot = key.IndexOf('.');
                if (dot < 0) { ApplyScalarFx(m, key, v); continue; }

                string prefix = key.Substring(0, dot);
                string suffix = key.Substring(dot + 1);
                switch (prefix)
                {
                    case "perSymbolExp":
                        Pse(m, SymOf(suffix), (int)v);
                        break;
                    case "perSymbolScore":
                        Pss(m, SymOf(suffix), (int)v);
                        break;
                    case "symbolWeightMul":
                        Wmul(m, SymOf(suffix), v);
                        break;
                    case "weightAdd":
                        Wadd(m, SymOf(suffix), v);
                        break;
                    case "tagExpBonus":
                        m.tagExpBonus[suffix] = m.tagExpBonus.TryGetValue(suffix, out var t) ? t + (int)v : (int)v;
                        break;
                    case "rareBurstExpMul":
                        // fate_burst의 boss/nonBoss 분리 키는 ApplyCtxConditionalPerk 전용 — 여기 도달하면
                        // 데이터 계약 위반(다른 콘텐츠가 이 접두를 잘못 사용). 방어적으로 무시하지 않고 보고.
                        throw new InvalidOperationException(
                            $"Mods fx: '{key}'는 ctx 조건부 전용 키입니다(ApplyCtxConditionalPerk에서만 처리).");
                    default:
                        throw new InvalidOperationException($"Mods fx: 알 수 없는 점표기 키 '{key}'");
                }
            }
        }

        // Kotlin buildMods의 로컬 헬퍼(wmul/wadd/pse/pss/tag, L1749-1753)와 동일한 누적 규칙.
        private static void Wmul(Mods m, Sym id, double v) =>
            m.symbolWeightMul[id] = (m.symbolWeightMul.TryGetValue(id, out var ex) ? ex : 1.0) * v;

        private static void Wadd(Mods m, Sym id, double v) =>
            m.weightAdd[id] = (m.weightAdd.TryGetValue(id, out var ex) ? ex : 0.0) + v;

        private static void Pse(Mods m, Sym id, int v) =>
            m.perSymbolExp[id] = (m.perSymbolExp.TryGetValue(id, out var ex) ? ex : 0) + v;

        private static void Pss(Mods m, Sym id, int v) =>
            m.perSymbolScore[id] = (m.perSymbolScore.TryGetValue(id, out var ex) ? ex : 0) + v;

        private static Sym SymOf(string symId)
        {
            var info = Symbols.ById(symId);
            if (info == null) throw new InvalidOperationException($"Mods fx: 알 수 없는 심볼 id '{symId}'");
            return info.sym;
        }

        private static bool ContainsAll(HashSet<string> owned, string[] requires)
        {
            if (requires == null) return true;
            for (int i = 0; i < requires.Length; i++)
                if (!owned.Contains(requires[i])) return false;
            return true;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        // 스칼라(비 점표기) fx 필드 적용 — 곱연산 필드(baseline 1.0)와 가산 필드(baseline 0)를 구분한다.
        // 알 수 없는 필드명은 콘텐츠 오타를 조기에 잡기 위해 예외로 던진다(§01_engine.md §11-12의
        // "무명 case 무시"는 buildMods의 id when-block 얘기지, fx 필드명 자체의 오타 방지와는 별개다).
        private static void ApplyScalarFx(Mods m, string field, double v)
        {
            switch (field)
            {
                case "expMul": m.expMul *= v; break;
                case "scoreMul": m.scoreMul *= v; break;
                case "coinMul": m.coinMul *= v; break;
                case "flatExp": m.flatExp += (int)v; break;
                case "flatScore": m.flatScore += (int)v; break;
                case "bonusSpins": m.bonusSpins += (int)v; break;
                case "skullExp": m.skullExp += (int)v; break;
                case "skullPenaltyMul": m.skullPenaltyMul *= v; break;
                case "setExpMul": m.setExpMul *= v; break;
                case "firstSpinExpMul": m.firstSpinExpMul *= v; break;
                case "lastSpinExpMul": m.lastSpinExpMul *= v; break;
                case "rareWeightMul": m.rareWeightMul *= v; break;
                case "centerExpMul": m.centerExpMul *= v; break;
                case "endsMatchExpMul": m.endsMatchExpMul *= v; break;
                case "adjacentSameExp": m.adjacentSameExp += (int)v; break;
                case "quotaMul": m.quotaMul *= v; break;
                case "clearCoinBonus": m.clearCoinBonus += (int)v; break;
                case "skullScoreBonus": m.skullScoreBonus += (int)v; break;
                case "perSkullExp": m.perSkullExp += (int)v; break;
                case "skull3ScoreMul": m.skull3ScoreMul *= v; break;
                case "rareBurstExpMul": m.rareBurstExpMul *= v; break;
                case "rareBurstScoreMul": m.rareBurstScoreMul *= v; break;
                case "twoSetBonusMul": m.twoSetBonusMul *= v; break;
                case "set3ExpMul": m.set3ExpMul *= v; break;
                case "set4ScoreMul": m.set4ScoreMul *= v; break;
                case "perfectShapeExpMul": m.perfectShapeExpMul *= v; break;
                // 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14) 신규 필드.
                case "cliffBurstExpMul": m.cliffBurstExpMul *= v; break;
                case "shopPriceMul": m.shopPriceMul *= v; break;
                case "itemPriceMul": m.itemPriceMul *= v; break;
                case "itemCapBonus": m.itemCapBonus += (int)v; break;
                case "shopSlotBonus": m.shopSlotBonus += (int)v; break;
                case "shopRerollDelta": m.shopRerollDelta += (int)v; break;
                default: throw new InvalidOperationException($"Mods fx: 알 수 없는 필드명 '{field}'");
            }
        }
    }
}
