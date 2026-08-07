using System.Collections.Generic;

namespace JackpotRun.Engine
{
    // 증강(AUGMENT) 80종·유물(RELIC) 61종·저주(CURSE) 16종 — SlotV2Engine.kt AUGMENTS(L383-481)/
    // RELICS(L483-548)/CURSES(L551-569) 전사 + buildMods() 실효과(L1730-2026)를 fx 딕셔너리로 데이터화.
    // Perk 타입은 S1이 정의(ENGINE_PORT_DESIGN.md 공유 타입) — 이 파일은 참조만 하고 재정의하지 않는다.
    //
    // fx 딕셔너리 키 규약은 Items.cs/Sets.cs(S2b)와 동일하게 맞췄다(buildMods가 다루는 Mods 필드명 그대로):
    //   스칼라 필드 → 필드명 그대로("expMul","flatExp","bonusSpins","skullExp","setExpMul","firstSpinExpMul",
    //     "lastSpinExpMul","rareWeightMul","centerExpMul","endsMatchExpMul","adjacentSameExp","quotaMul",
    //     "clearCoinBonus","scoreMul","coinMul","perSkullExp","skull3ScoreMul","twoSetBonusMul","set3ExpMul",
    //     "set4ScoreMul","perfectShapeExpMul" 등, Mods 필드 정의는 SlotV2Engine.kt L138-175)
    //   심볼별 오버레이 → "perSymbolExp.<symId>"(pse)/"perSymbolScore.<symId>"(pss)/
    //     "symbolWeightMul.<symId>"(wmul)/"weightAdd.<symId>"(wadd)
    //   태그 가산 → "tagExpBonus.<tag>"(tag, 예: "tagExpBonus.학습")
    //
    // ⚠️ 신규 16종(2026-06-29, early_prep~black_diploma)은 buildMods의 RunCtx 조건부 분기(§5.2, L1909-1931)라
    //   위 "필드명 그대로" 규약을 따르면 조건 없이 자동 적용되는 것으로 오독될 위험이 있다(예: early_prep은
    //   S1~3에서만 expMul*=1.15가 적용되어야 하는데 "expMul" 키를 그대로 쓰면 상시적용처럼 보임).
    //   그래서 이 16종 중 buildMods 자체가 ctx(stage/spinsLeft/growthStack/unluckyGauge/curseCount 등)로
    //   조건분기하거나 배율이 ctx에 따라 동적으로 계산되는 8종(early_prep/early_adapt/growth_log/snowball/
    //   fortune_check/luck_accum/late_focus/cliff_focus)과 sacrifice/black_diploma의 조건부 부분은
    //   "cond.<파라미터명>" 접두를 붙여 "단순 자동적용 금지 — Mods/Resolver(S3)가 id별 case로 해석할 것"을
    //   명시했다(Kotlin ctx-block과 동일하게 S3에서 특수 처리 필요). 반대로 buildMods 안에서 조건 없이
    //   그대로 대입되는 나머지(fate_burst 일부/pair_match/puzzle_sense/perfect_shape/skull_watch)는 실제
    //   Mods 필드명을 그대로 썼다 — evaluate()가 셀 내용으로 사후 판정하는 것뿐, buildMods 시점엔 무조건
    //   적용되므로 위 "필드명 그대로" 규약과 완전히 일치한다. fate_burst만 배율이 ctx.boss에 따라 갈리므로
    //   "rareBurstExpMul.boss"/"rareBurstExpMul.nonBoss"로 분리했다(심볼 오버레이의 점표기 관례를 재사용).
    //   fate_bell(운명의종)은 원본 buildMods에 아무 case가 없다(L1923 주석 "buildMods 무효과" — 서비스가
    //   run.fateBellUsed 게이트로 실패직전 추가스핀을 처리) — fx는 의도적으로 빈 딕셔너리.
    //
    // school 필드: Kotlin Perk data class(L378-381) 자체엔 school 필드가 없다 — perkGate()가 매 호출마다
    // ①BASE_PERK_IDS(L706-713, 22종) 빈게이트 ②PERK_GATE_OVERRIDES(L753-807, 45종) 개별게이트
    // ③inferSchool(desc)(L810-822) 순으로 동적 산출하는 값이다. 설계서(ENGINE_PORT_DESIGN.md 공유 타입)가
    // Perk.school을 정적 필드로 요구하므로, 이 파일이 그 3단계 로직을 그대로 재현해 로드 시점에 구워 넣는다
    // (Schools.cs는 PerkGateOverrides의 minLevel/req까지 포함한 전체 게이트 테이블만 갖고 있고 이 결합 로직
    // 자체는 갖고 있지 않다 — Schools.cs 파일 상단 주석 "결합 순서는 S3 Mods/해금 로직에서 구현" 참조).
    // BASE_PERK_IDS 22종은 원본처럼 school = ""(빈 게이트라 전공 개념 자체가 없음).
    public static class Perks
    {
        public const int AugmentCount = 89;
        public const int RelicCount = 73;
        public const int CurseCount = 16;

        // ── 증강 80종 (L383-481) ────────────────────────────────────────────
        public static readonly Perk[] Augments = WithSchool(new[]
        {
            // 🥈 실버 — 단순 스탯 (L385-396, buildMods L1777-1788)
            new Perk
            {
                id = "study", emoji = "📚", name = "기초학습", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "모든 EXP +10%",
                fx = new Dictionary<string, double> { ["expMul"] = 1.10 },
            },
            new Perk
            {
                id = "preview", emoji = "🔍", name = "예습", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "첫 스핀 EXP +25%",
                fx = new Dictionary<string, double> { ["firstSpinExpMul"] = 1.25 },
            },
            new Perk
            {
                id = "review", emoji = "📖", name = "복습", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "마지막 스핀 EXP +25%",
                fx = new Dictionary<string, double> { ["lastSpinExpMul"] = 1.25 },
            },
            new Perk
            {
                id = "diligence", emoji = "✍️", name = "꾸준함", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "스핀마다 EXP +3",
                fx = new Dictionary<string, double> { ["flatExp"] = 3 },
            },
            new Perk
            {
                id = "cherry_up", emoji = "🍒", name = "체리강화", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "🍒체리 EXP +2",
                fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 2 },
            },
            new Perk
            {
                id = "book_up", emoji = "📘", name = "책강화", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "📘책 EXP +2",
                fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 2 },
            },
            new Perk
            {
                id = "star_up", emoji = "⭐", name = "별강화", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "⭐별 EXP +2",
                fx = new Dictionary<string, double> { ["perSymbolExp.star"] = 2 },
            },
            new Perk
            {
                id = "gem_polish", emoji = "💎", name = "보석연마", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "💎보석 점수 +10",
                fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 10 },
            },
            new Perk
            {
                id = "coin_luck", emoji = "🪙", name = "동전운", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "코인 +30%",
                fx = new Dictionary<string, double> { ["coinMul"] = 1.3 },
            },
            new Perk
            {
                id = "set_sense", emoji = "🎯", name = "콤보감각", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "세트 보너스 +30%",
                fx = new Dictionary<string, double> { ["setExpMul"] = 1.3 },
            },
            new Perk
            {
                id = "lucky", emoji = "🍀", name = "행운부적", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "희귀심볼 등장 +20%",
                fx = new Dictionary<string, double> { ["rareWeightMul"] = 1.2 },
            },
            new Perk
            {
                id = "study_tag", emoji = "🎓", name = "학구열", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "학습태그(📘) 1개당 EXP +4",
                fx = new Dictionary<string, double> { ["tagExpBonus.학습"] = 4 },
            },
            // 🥇 골드 — 빌드 정의 (L398-407, buildMods L1790-1799)
            new Perk
            {
                id = "cherry_farm", emoji = "🍒", name = "체리농장", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "🍒체리 EXP +4·등장↑",
                fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 4, ["symbolWeightMul.cherry"] = 1.3 },
            },
            new Perk
            {
                id = "library", emoji = "📇", name = "도서관", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "📘책 EXP +4·학습태그 +3",
                fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 4, ["tagExpBonus.학습"] = 3 },
            },
            new Perk
            {
                id = "gem_invest", emoji = "💎", name = "보석투자", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "💎보석 점수 +25",
                fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 25 },
            },
            new Perk
            {
                id = "skull_study", emoji = "☠", name = "해골학", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "☠해골이 EXP +6",
                fx = new Dictionary<string, double> { ["skullExp"] = 6 },
            },
            new Perk
            {
                id = "center", emoji = "🎯", name = "집중", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "가운데 칸 EXP 2배",
                fx = new Dictionary<string, double> { ["centerExpMul"] = 2.0 },
            },
            new Perk
            {
                id = "twins", emoji = "↔️", name = "양끝맞춤", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "양끝 같은 심볼이면 EXP 2배",
                fx = new Dictionary<string, double> { ["endsMatchExpMul"] = 2.0 },
            },
            new Perk
            {
                id = "chain", emoji = "🔗", name = "연쇄", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "붙은 같은 심볼 쌍당 EXP +20",
                fx = new Dictionary<string, double> { ["adjacentSameExp"] = 20 },
            },
            new Perk
            {
                id = "crown_seek", emoji = "👑", name = "왕관추종", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "👑왕관 등장 2배·점수 +30",
                fx = new Dictionary<string, double> { ["symbolWeightMul.crown"] = 2.0, ["perSymbolScore.crown"] = 30 },
            },
            new Perk
            {
                id = "greed", emoji = "🤑", name = "탐욕", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "모든 EXP +25%",
                fx = new Dictionary<string, double> { ["expMul"] = 1.25 },
            },
            new Perk
            {
                id = "insurance", emoji = "❤️", name = "보험", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "스테이지 스핀 +1",
                fx = new Dictionary<string, double> { ["bonusSpins"] = 1 },
            },
            // 🌈 프리즘 — 시스템 변경 (L409-413, buildMods L1801-1805)
            new Perk
            {
                id = "overdrive", emoji = "⚡", name = "과부하", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "모든 EXP +60%",
                fx = new Dictionary<string, double> { ["expMul"] = 1.6 },
            },
            new Perk
            {
                id = "short_day", emoji = "🏃", name = "조기퇴근", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "스핀 -2 · 모든 EXP +120%",
                fx = new Dictionary<string, double> { ["bonusSpins"] = -2, ["expMul"] = 2.2 },
            },
            new Perk
            {
                id = "wild_world", emoji = "🌀", name = "와일드세계", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "🌀와일드 등장(세트 합류)",
                fx = new Dictionary<string, double> { ["weightAdd.wild"] = 6.0 },
            },
            new Perk
            {
                id = "seed_garden", emoji = "🌱", name = "씨앗정원", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "🌱씨앗 등장(다음스핀 성장)",
                fx = new Dictionary<string, double> { ["weightAdd.seed"] = 5.0 },
            },
            new Perk
            {
                id = "jackpot", emoji = "🎰", name = "잭팟기계", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "👑왕관 대량등장·점수 +50",
                fx = new Dictionary<string, double> { ["weightAdd.crown"] = 3.0, ["perSymbolScore.crown"] = 50 },
            },
            // ── 확장 (조건부/리스크/시스템, L415-426, buildMods L1807-1818) ──
            new Perk
            {
                id = "all_in", emoji = "🎯", name = "몰아치기", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "스핀 -1·모든 EXP +45%",
                fx = new Dictionary<string, double> { ["bonusSpins"] = -1, ["expMul"] = 1.45 },
            },
            new Perk
            {
                id = "cram", emoji = "⏰", name = "벼락치기", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "첫스핀 -40%·막스핀 +120%",
                fx = new Dictionary<string, double> { ["firstSpinExpMul"] = 0.6, ["lastSpinExpMul"] = 2.2 },
            },
            new Perk
            {
                id = "high_roller", emoji = "💠", name = "하이롤러", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "💎보석 점수+30·EXP -8%",
                fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 30, ["expMul"] = 0.92 },
            },
            new Perk
            {
                id = "all_or_nothing", emoji = "☠", name = "해골도박", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "☠해골 EXP+10·EXP -10%",
                fx = new Dictionary<string, double> { ["skullExp"] = 10, ["expMul"] = 0.9 },
            },
            new Perk
            {
                id = "focus_fire", emoji = "🔭", name = "정조준", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "가운데 칸 EXP 2.5배",
                fx = new Dictionary<string, double> { ["centerExpMul"] = 2.5 },
            },
            new Perk
            {
                id = "symmetry", emoji = "↔️", name = "대칭미학", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "양끝맞춤 EXP 2.2배·인접쌍+12",
                fx = new Dictionary<string, double> { ["endsMatchExpMul"] = 2.2, ["adjacentSameExp"] = 12 },
            },
            new Perk
            {
                id = "crammer_tag", emoji = "🎓", name = "주입식", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "학습태그당 EXP+7·책등장↑",
                fx = new Dictionary<string, double> { ["tagExpBonus.학습"] = 7, ["symbolWeightMul.book"] = 1.4 },
            },
            new Perk
            {
                id = "gamblers_dice", emoji = "🎲", name = "도박주사위", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "🎲주사위 등장·EXP +15%",
                fx = new Dictionary<string, double> { ["weightAdd.dice"] = 5.0, ["expMul"] = 1.15 },
            },
            new Perk
            {
                id = "key_master", emoji = "🗝", name = "열쇠장인", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "🗝열쇠 등장·코인 +25%",
                fx = new Dictionary<string, double> { ["weightAdd.key"] = 4.0, ["coinMul"] = 1.25 },
            },
            new Perk
            {
                id = "glass_cannon", emoji = "⚡", name = "유리대포", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "스핀-1·EXP +90%·점수+10%",
                fx = new Dictionary<string, double> { ["bonusSpins"] = -1, ["expMul"] = 1.9, ["scoreMul"] = 1.1 },
            },
            new Perk
            {
                id = "rich_richer", emoji = "🤑", name = "부익부", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "코인+60%·클코인+3·EXP-5%",
                fx = new Dictionary<string, double> { ["coinMul"] = 1.6, ["clearCoinBonus"] = 3, ["expMul"] = 0.95 },
            },
            new Perk
            {
                id = "endgame_rush", emoji = "🏁", name = "막판스퍼트", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "막스핀 EXP 2.4배·첫스핀 -50%",
                fx = new Dictionary<string, double> { ["lastSpinExpMul"] = 2.4, ["firstSpinExpMul"] = 0.5 },
            },
            // ── 물량 확장 (2026-06-24) 실버 (L429-436, buildMods L1820-1827) ──
            new Perk
            {
                id = "deep_read", emoji = "📕", name = "정독", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "학습태그(📘) 1개당 EXP +3",
                fx = new Dictionary<string, double> { ["tagExpBonus.학습"] = 3 },
            },
            new Perk
            {
                id = "morning", emoji = "🌅", name = "아침예습", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "첫 스핀 EXP +30%",
                fx = new Dictionary<string, double> { ["firstSpinExpMul"] = 1.30 },
            },
            new Perk
            {
                id = "evening", emoji = "🌆", name = "야간자습", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "마지막 스핀 EXP +30%",
                fx = new Dictionary<string, double> { ["lastSpinExpMul"] = 1.30 },
            },
            new Perk
            {
                id = "note_take", emoji = "📝", name = "필기", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "스핀마다 EXP +5",
                fx = new Dictionary<string, double> { ["flatExp"] = 5 },
            },
            new Perk
            {
                id = "star_up2", emoji = "🌟", name = "별관측", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "⭐별 EXP +3",
                fx = new Dictionary<string, double> { ["perSymbolExp.star"] = 3 },
            },
            new Perk
            {
                id = "magnet_up", emoji = "🧲", name = "자석강화", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "🧲자석 EXP +3",
                fx = new Dictionary<string, double> { ["perSymbolExp.magnet"] = 3 },
            },
            new Perk
            {
                id = "gem_buff", emoji = "💠", name = "보석세공", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "💎보석 점수 +12",
                fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 12 },
            },
            new Perk
            {
                id = "combo_note", emoji = "🎯", name = "콤보노트", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "세트 보너스 +20%",
                fx = new Dictionary<string, double> { ["setExpMul"] = 1.20 },
            },
            // 골드 (L438-445, buildMods L1828-1835)
            new Perk
            {
                id = "polymath", emoji = "🧠", name = "박식", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "모든 EXP +20%",
                fx = new Dictionary<string, double> { ["expMul"] = 1.20 },
            },
            new Perk
            {
                id = "necromancer", emoji = "💀", name = "강령술사", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "☠해골이 EXP +8",
                fx = new Dictionary<string, double> { ["skullExp"] = 8 },
            },
            new Perk
            {
                id = "bullseye", emoji = "🎯", name = "정조준2", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "가운데 칸 EXP 1.8배",
                fx = new Dictionary<string, double> { ["centerExpMul"] = 1.8 },
            },
            new Perk
            {
                id = "mirror", emoji = "🪞", name = "거울대칭", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "양끝 같은 심볼이면 EXP 1.9배",
                fx = new Dictionary<string, double> { ["endsMatchExpMul"] = 1.9 },
            },
            new Perk
            {
                id = "domino", emoji = "⛓️", name = "도미노", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "붙은 같은 심볼 쌍당 EXP +16",
                fx = new Dictionary<string, double> { ["adjacentSameExp"] = 16 },
            },
            new Perk
            {
                id = "honor_student", emoji = "🎓", name = "수재", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "학습태그(📘) 1개당 EXP +6",
                fx = new Dictionary<string, double> { ["tagExpBonus.학습"] = 6 },
            },
            new Perk
            {
                id = "lapidary", emoji = "💍", name = "세공장인", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "💎보석 점수 +28",
                fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 28 },
            },
            new Perk
            {
                id = "royal_decree", emoji = "📜", name = "왕명", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "👑왕관 등장↑·점수 +20",
                fx = new Dictionary<string, double> { ["symbolWeightMul.crown"] = 1.8, ["perSymbolScore.crown"] = 20 },
            },
            // 프리즘 (L447-451, buildMods L1836-1840)
            new Perk
            {
                id = "supernova", emoji = "💥", name = "초신성", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "모든 EXP +70%",
                fx = new Dictionary<string, double> { ["expMul"] = 1.70 },
            },
            new Perk
            {
                id = "joker", emoji = "🃏", name = "조커", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "🌀와일드 대량 등장",
                fx = new Dictionary<string, double> { ["weightAdd.wild"] = 5.0 },
            },
            new Perk
            {
                id = "great_harvest", emoji = "🌾", name = "대수확", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "🌱씨앗 등장·🍒체리 EXP +3",
                fx = new Dictionary<string, double> { ["weightAdd.seed"] = 5.0, ["perSymbolExp.cherry"] = 3 },
            },
            new Perk
            {
                id = "mega_jackpot", emoji = "🎰", name = "대박기계", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "👑왕관 대량등장·점수 +40",
                fx = new Dictionary<string, double> { ["weightAdd.crown"] = 3.0, ["perSymbolScore.crown"] = 40 },
            },
            new Perk
            {
                id = "time_warp", emoji = "⏳", name = "시간왜곡", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "스핀 +1·모든 EXP +20%",
                fx = new Dictionary<string, double> { ["bonusSpins"] = 1, ["expMul"] = 1.20 },
            },
            // ── 세트 컴포넌트 증강 (2026-06-24, L453-456, buildMods L1905-1908) ──
            new Perk
            {
                id = "red_safetynet", emoji = "🥅", name = "붉은 안전망", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "🍒체리 EXP +2",
                fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 2 },
            },
            new Perk
            {
                id = "polish_work", emoji = "✨", name = "광택 작업", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "💎보석 점수 +25",
                fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 25 },
            },
            new Perk
            {
                id = "greed_calc", emoji = "🤑", name = "탐욕의 계산", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "모든 EXP +15%",
                fx = new Dictionary<string, double> { ["expMul"] = 1.15 },
            },
            new Perk
            {
                id = "overheat_formula", emoji = "♨️", name = "과열 공식", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "모든 EXP +14%",
                fx = new Dictionary<string, double> { ["expMul"] = 1.14 },
            },
            // ── 신규 16종 (빌드 축 5테마, 2026-06-29, L461-480, buildMods ctx분기 L1911-1931) ──
            // 초반성장(성장학)
            new Perk
            {
                id = "early_prep", emoji = "🥚", name = "조기교육", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "S3 이하 EXP +15% (S6+ 무효)",
                // Kotlin: if (ctx.stage in 1..3) expMul *= 1.15
                fx = new Dictionary<string, double> { ["cond.stageMax"] = 3, ["cond.expMul"] = 1.15 },
            },
            new Perk
            {
                id = "growth_log", emoji = "📈", name = "성장일지", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "클리어마다 다음스테이지 첫스핀 EXP +8% (최대5·실패리셋)",
                // Kotlin: firstSpinExpMul *= (1.0 + 0.08 * ctx.growthStack.coerceIn(0,5))
                fx = new Dictionary<string, double> { ["cond.stackBase"] = 0.08, ["cond.stackMax"] = 5 },
            },
            new Perk
            {
                id = "early_adapt", emoji = "🌱", name = "빠른적응", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "S1~5 EXP +12% (S6+ 무효)",
                // Kotlin: if (ctx.stage in 1..5) expMul *= 1.12
                fx = new Dictionary<string, double> { ["cond.stageMax"] = 5, ["cond.expMul"] = 1.12 },
            },
            new Perk
            {
                id = "snowball", emoji = "❄️", name = "눈덩이", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "남은스핀2+ 클리어시 다음 EXP +12% (최대4·보스후 -1)",
                // Kotlin: expMul *= (1.0 + 0.12 * ctx.snowStack.coerceIn(0,4))
                fx = new Dictionary<string, double> { ["cond.stackBase"] = 0.12, ["cond.stackMax"] = 4 },
            },
            // 운빨(운명학)
            new Perk
            {
                id = "fortune_check", emoji = "🔍", name = "운세확인", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "스테이지 첫스핀 희귀 등장 +20%",
                // Kotlin: if (ctx.isFirstSpin) rareWeightMul *= 1.2
                fx = new Dictionary<string, double> { ["cond.firstSpinRareMul"] = 1.2 },
            },
            new Perk
            {
                id = "luck_accum", emoji = "🎰", name = "불운적립", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "희귀 미등장 스핀마다 불운+1 (3+면 다음 희귀↑)",
                // Kotlin: if (ctx.unluckyGauge >= 3) rareWeightMul *= 1.3
                fx = new Dictionary<string, double> { ["cond.gaugeThreshold"] = 3, ["cond.rareMul"] = 1.3 },
            },
            new Perk
            {
                id = "fate_burst", emoji = "💫", name = "운명폭발", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "희귀 2개+ 스핀 EXP +80%·점수 +50% (보스전 70%)",
                // Kotlin: rareBurstExpMul *= (if (ctx.boss) 1.7 else 1.8); rareBurstScoreMul *= 1.5
                // (buildMods 자체는 조건 없이 대입 — evaluate()가 희귀 2개+ 셀 조건을 사후 판정. 배율만
                // ctx.boss로 갈리므로 심볼 오버레이 점표기 관례를 재사용해 boss/nonBoss로 분리.)
                fx = new Dictionary<string, double>
                {
                    ["rareBurstExpMul.boss"] = 1.7,
                    ["rareBurstExpMul.nonBoss"] = 1.8,
                    ["rareBurstScoreMul"] = 1.5,
                },
            },
            // 막판역전(시간학)
            new Perk
            {
                id = "late_focus", emoji = "⏳", name = "후반집중", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "남은스핀 2 이하 EXP +10%",
                // Kotlin: if (ctx.spinsLeft in 1..2) expMul *= 1.10
                fx = new Dictionary<string, double> { ["cond.spinsLeftMax"] = 2, ["cond.expMul"] = 1.10 },
            },
            new Perk
            {
                id = "cliff_focus", emoji = "🧗", name = "벼랑끝집중", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "EXP가 요구 60% 미만 & 마지막스핀 → 막스핀 EXP +80%",
                // Kotlin: if (ctx.isLastSpin && ctx.quota>0 && ctx.stageExp < (ctx.quota*0.6).toLong()) lastSpinExpMul *= 1.8
                fx = new Dictionary<string, double> { ["cond.quotaPct"] = 0.6, ["cond.lastSpinExpMul"] = 1.8 },
            },
            new Perk
            {
                id = "fate_bell", emoji = "🔔", name = "운명의종", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "런 1회 부족 15 이하 실패직전 자동 추가스핀 +1",
                // Kotlin L1923: buildMods 무효과 — 서비스가 run.fateBellUsed 게이트로 처리(fx 의도적으로 빈 딕셔너리)
                fx = new Dictionary<string, double>(),
            },
            // 세트콤보(계산학)
            new Perk
            {
                id = "pair_match", emoji = "👯", name = "짝맞추기", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "같은심볼 2세트면 세트 보너스 +20%",
                // Kotlin: twoSetBonusMul *= 1.2 (조건 없음 — evaluate가 bestCount==2 사후 판정)
                fx = new Dictionary<string, double> { ["twoSetBonusMul"] = 1.2 },
            },
            new Perk
            {
                id = "puzzle_sense", emoji = "🧩", name = "퍼즐감각", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "세트3+ EXP +25%·세트4+ 점수 +20%",
                fx = new Dictionary<string, double> { ["set3ExpMul"] = 1.25, ["set4ScoreMul"] = 1.20 },
            },
            new Perk
            {
                id = "perfect_shape", emoji = "💠", name = "완벽한모양", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "양끝 같고 가운데 같은계열 EXP +120% (와일드충족 70%)",
                // Kotlin: perfectShapeExpMul *= 2.2 (와일드충족 시 evaluate가 고정 1.7로 대체 적용)
                fx = new Dictionary<string, double> { ["perfectShapeExpMul"] = 2.2 },
            },
            // 해골저주(저주학)
            new Perk
            {
                id = "skull_watch", emoji = "👁️", name = "해골관찰", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "☠1개당 EXP +2·☠3+ 스핀 점수 -10%",
                fx = new Dictionary<string, double> { ["perSkullExp"] = 2, ["skull3ScoreMul"] = 0.9 },
            },
            new Perk
            {
                id = "sacrifice", emoji = "🩸", name = "희생", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "저주1개당 EXP +6%·클리어코인 -1",
                // Kotlin: expMul *= (1.0 + 0.06 * ctx.curseCount); clearCoinBonus -= 1
                fx = new Dictionary<string, double> { ["cond.curseExpBase"] = 0.06, ["clearCoinBonus"] = -1 },
            },
            new Perk
            {
                id = "black_diploma", emoji = "🎓", name = "검은졸업장", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "저주5+ EXP +60%·점수 +30%·스핀 -1",
                // Kotlin: if (ctx.curseCount>=5) { expMul*=1.6; scoreMul*=1.3; bonusSpins-=1 }
                fx = new Dictionary<string, double>
                {
                    ["cond.curseThreshold"] = 5,
                    ["cond.expMul"] = 1.6,
                    ["cond.scoreMul"] = 1.3,
                    ["cond.bonusSpinsDelta"] = -1,
                },
            },
            // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, data.js:247-337) — 신규 9종 ──
            // 상점 빌드 4종(discount/thrifty/item_bag/vip, engine.js:204-208) — 상점/가방 시스템 자체는
            // Unity P4(화면 흐름 웹화) 대상이라 이 슬라이스에선 Mods 필드만 채워 넣는다(fx 데이터 완비).
            new Perk
            {
                id = "discount", emoji = "🏷️", name = "할인감각", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "상점 가격 -10%",
                fx = new Dictionary<string, double> { ["shopPriceMul"] = 0.9 },
            },
            new Perk
            {
                id = "thrifty", emoji = "🛍️", name = "알뜰구매", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "상점 아이템 가격 -20%",
                fx = new Dictionary<string, double> { ["itemPriceMul"] = 0.8 },
            },
            new Perk
            {
                id = "item_bag", emoji = "🎒", name = "아이템가방", tier = Tier.SILVER, cat = PCat.AUGMENT, price = 0,
                desc = "아이템 보유칸 +1",
                fx = new Dictionary<string, double> { ["itemCapBonus"] = 1 },
            },
            new Perk
            {
                id = "vip", emoji = "💳", name = "VIP 회원", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "상점 상품칸 +1·새로고침 비용 -2",
                fx = new Dictionary<string, double> { ["shopSlotBonus"] = 1, ["shopRerollDelta"] = -2 },
            },
            // refund(환불 정책) — buildMods에 case가 없다(engine.js 전수 확인, "keep" 판정은 game.js
            // useItem이 r.perks.includes("refund")로 직접 검사 — ItemUse.Use가 동일하게 처리). fx는
            // 의도적으로 빈 딕셔너리(fate_bell과 동일한 "서비스가 별도 처리" 패턴).
            new Perk
            {
                id = "refund", emoji = "🧾", name = "환불 정책", tier = Tier.GOLD, cat = PCat.AUGMENT, price = 0,
                desc = "아이템 사용 시 30% 확률로 사라지지 않음",
                fx = new Dictionary<string, double>(),
            },
            // 후반(심연) 증강 4종(unlockLevel, engine.js:301-304) — abyss_lore/curse_grad는 ctx 조건부
            // (ModsBuilder.CtxConditionalIds에 추가 필요 — cond.* 접두 규약).
            new Perk
            {
                id = "crown_burst", emoji = "👑", name = "왕관 폭발", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "👑왕관 점수 +100·대량 등장", unlockLevel = 10,
                fx = new Dictionary<string, double>
                {
                    ["perSymbolScore.crown"] = 100, ["symbolWeightMul.crown"] = 2.5, ["weightAdd.crown"] = 2.0,
                },
            },
            new Perk
            {
                id = "curse_grad", emoji = "🎓", name = "저주 대학원", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "저주 1개당 점수 +15%", unlockLevel = 12,
                // Kotlin(웹): scoreMul *= (1 + 0.15 * ctx.curseCount) — 조건 없는 대입이지만 curseCount에
                // 동적으로 의존하므로 다른 cond.curse* 계열(sacrifice/black_diploma)과 동일하게 취급.
                fx = new Dictionary<string, double> { ["cond.curseScoreBase"] = 0.15 },
            },
            new Perk
            {
                id = "extreme_overload", emoji = "⚡", name = "극한 과부하", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "모든 EXP +90% · 요구 EXP +30% (고위험)", unlockLevel = 15,
                fx = new Dictionary<string, double> { ["expMul"] = 1.9, ["quotaMul"] = 1.3 },
            },
            new Perk
            {
                id = "abyss_lore", emoji = "🌌", name = "심연 지식", tier = Tier.PRISM, cat = PCat.AUGMENT, price = 0,
                desc = "보스 스테이지 EXP +50% (보스 특화)", unlockLevel = 16,
                // Kotlin(웹): if (ctx.boss) expMul *= 1.5 — cliff_focus 등과 동일한 cond.* 조건부 표기.
                fx = new Dictionary<string, double> { ["cond.bossExpMul"] = 1.5 },
            },
        });

        // ── 유물 61종 (L484-547, buildMods 같은 루프 L1842-1904) ────────────
        public static readonly Perk[] Relics = WithSchool(new[]
        {
            new Perk { id = "old_book", emoji = "📘", name = "낡은교과서", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "📘책 EXP +3", fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 3 } },
            new Perk { id = "cherry_candy", emoji = "🍬", name = "체리사탕", tier = Tier.SILVER, cat = PCat.RELIC, price = 10, desc = "🍒체리 EXP +2", fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 2 } },
            new Perk { id = "rusty_coin", emoji = "🪙", name = "녹슨동전", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "코인 +20%", fx = new Dictionary<string, double> { ["coinMul"] = 1.2 } },
            new Perk { id = "pencil", emoji = "✏️", name = "연필깎이", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "첫 스핀 EXP +15%", fx = new Dictionary<string, double> { ["firstSpinExpMul"] = 1.15 } },
            new Perk { id = "coffee", emoji = "☕", name = "커피잔", tier = Tier.SILVER, cat = PCat.RELIC, price = 14, desc = "마지막 스핀 EXP +15%", fx = new Dictionary<string, double> { ["lastSpinExpMul"] = 1.15 } },
            new Perk { id = "magnifier", emoji = "🔎", name = "돋보기", tier = Tier.SILVER, cat = PCat.RELIC, price = 16, desc = "희귀심볼 등장 +15%", fx = new Dictionary<string, double> { ["rareWeightMul"] = 1.15 } },
            new Perk { id = "star_sticker", emoji = "⭐", name = "별스티커", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "⭐별 점수 +8", fx = new Dictionary<string, double> { ["perSymbolScore.star"] = 8 } },
            new Perk { id = "black_candle", emoji = "🕯️", name = "검은촛불", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "☠해골이 EXP +4", fx = new Dictionary<string, double> { ["skullExp"] = 4 } },
            new Perk { id = "gem_cert", emoji = "📜", name = "보석감정서", tier = Tier.GOLD, cat = PCat.RELIC, price = 20, desc = "💎보석 점수 +15", fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 15 } },
            new Perk { id = "clover", emoji = "🍀", name = "네잎클로버", tier = Tier.GOLD, cat = PCat.RELIC, price = 16, desc = "모든 EXP +8%", fx = new Dictionary<string, double> { ["expMul"] = 1.08 } },
            new Perk { id = "set_charm", emoji = "🎰", name = "세트부적", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "세트 보너스 +25%", fx = new Dictionary<string, double> { ["setExpMul"] = 1.25 } },
            new Perk { id = "wide_lens", emoji = "🔭", name = "집중경", tier = Tier.GOLD, cat = PCat.RELIC, price = 16, desc = "가운데 칸 EXP +50%", fx = new Dictionary<string, double> { ["centerExpMul"] = 1.5 } },
            // ── 물량 확장 (2026-06-24) 실버 (L497-508) ──
            new Perk { id = "eraser", emoji = "✏️", name = "지우개", tier = Tier.SILVER, cat = PCat.RELIC, price = 10, desc = "📘책 EXP +2", fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 2 } },
            new Perk { id = "ruler", emoji = "📏", name = "자", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "첫 스핀 EXP +12%", fx = new Dictionary<string, double> { ["firstSpinExpMul"] = 1.12 } },
            new Perk { id = "desk_lamp", emoji = "🪔", name = "스탠드", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "마지막 스핀 EXP +12%", fx = new Dictionary<string, double> { ["lastSpinExpMul"] = 1.12 } },
            new Perk { id = "cherry_jam", emoji = "🍓", name = "체리잼", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "🍒체리 EXP +3", fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 3 } },
            new Perk { id = "bookmark", emoji = "🔖", name = "책갈피", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "학습태그(📘) 1개당 EXP +3", fx = new Dictionary<string, double> { ["tagExpBonus.학습"] = 3 } },
            new Perk { id = "coin_pouch", emoji = "👛", name = "동전지갑", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "코인 +20%", fx = new Dictionary<string, double> { ["coinMul"] = 1.2 } },
            new Perk { id = "mini_scope", emoji = "🔬", name = "미니스코프", tier = Tier.SILVER, cat = PCat.RELIC, price = 14, desc = "희귀심볼 등장 +15%", fx = new Dictionary<string, double> { ["rareWeightMul"] = 1.15 } },
            new Perk { id = "gem_dust", emoji = "✨", name = "보석가루", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "💎보석 점수 +10", fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 10 } },
            new Perk { id = "magnet_chip", emoji = "🧲", name = "자석칩", tier = Tier.SILVER, cat = PCat.RELIC, price = 10, desc = "🧲자석 EXP +2", fx = new Dictionary<string, double> { ["perSymbolExp.magnet"] = 2 } },
            new Perk { id = "star_chart", emoji = "🌠", name = "별자리표", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "⭐별 EXP +2", fx = new Dictionary<string, double> { ["perSymbolExp.star"] = 2 } },
            new Perk { id = "paperclip", emoji = "📎", name = "클립", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "세트 보너스 +15%", fx = new Dictionary<string, double> { ["setExpMul"] = 1.15 } },
            new Perk { id = "small_candle", emoji = "🕯️", name = "작은초", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "☠해골이 EXP +3", fx = new Dictionary<string, double> { ["skullExp"] = 3 } },
            // 골드 (L510-525)
            new Perk { id = "thick_tome", emoji = "📕", name = "두꺼운책", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "📘책 EXP +4", fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 4 } },
            new Perk { id = "crystal_ball", emoji = "🔮", name = "수정구", tier = Tier.GOLD, cat = PCat.RELIC, price = 20, desc = "희귀심볼 등장 +30%", fx = new Dictionary<string, double> { ["rareWeightMul"] = 1.3 } },
            new Perk { id = "skull_idol", emoji = "💀", name = "해골우상", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "☠해골이 EXP +6", fx = new Dictionary<string, double> { ["skullExp"] = 6 } },
            new Perk { id = "gem_tiara", emoji = "💎", name = "보석티아라", tier = Tier.GOLD, cat = PCat.RELIC, price = 22, desc = "💎보석 점수 +20", fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 20 } },
            new Perk { id = "focus_ring", emoji = "💍", name = "집중반지", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "가운데 칸 EXP +60%", fx = new Dictionary<string, double> { ["centerExpMul"] = 1.6 } },
            new Perk { id = "silver_mirror", emoji = "🪞", name = "은거울", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "양끝 같은 심볼 EXP +70%", fx = new Dictionary<string, double> { ["endsMatchExpMul"] = 1.7 } },
            new Perk { id = "iron_chain", emoji = "⛓️", name = "쇠사슬", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "붙은 같은 심볼 쌍당 EXP +14", fx = new Dictionary<string, double> { ["adjacentSameExp"] = 14 } },
            new Perk { id = "diploma_relic", emoji = "🎓", name = "졸업장식", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "학습태그(📘) 1개당 EXP +5", fx = new Dictionary<string, double> { ["tagExpBonus.학습"] = 5 } },
            new Perk { id = "four_clover", emoji = "🍀", name = "네잎클로버2", tier = Tier.GOLD, cat = PCat.RELIC, price = 20, desc = "모든 EXP +10%", fx = new Dictionary<string, double> { ["expMul"] = 1.10 } },
            new Perk { id = "combo_trophy", emoji = "🏆", name = "콤보트로피", tier = Tier.GOLD, cat = PCat.RELIC, price = 20, desc = "세트 보너스 +25%", fx = new Dictionary<string, double> { ["setExpMul"] = 1.25 } },
            new Perk { id = "crown_jewel", emoji = "👑", name = "왕관보석", tier = Tier.GOLD, cat = PCat.RELIC, price = 22, desc = "👑왕관 점수 +30", fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 30 } },
            new Perk { id = "piggy_bank", emoji = "🐷", name = "돼지저금통", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "코인 +40%·클리어코인 +2", fx = new Dictionary<string, double> { ["coinMul"] = 1.4, ["clearCoinBonus"] = 2 } },
            new Perk { id = "spare_token", emoji = "🎟️", name = "여분토큰", tier = Tier.GOLD, cat = PCat.RELIC, price = 30, desc = "스테이지 스핀 +1", fx = new Dictionary<string, double> { ["bonusSpins"] = 1 } },
            new Perk { id = "hourglass_r", emoji = "⏳", name = "모래시계", tier = Tier.GOLD, cat = PCat.RELIC, price = 22, desc = "첫·마지막 스핀 EXP +20%", fx = new Dictionary<string, double> { ["firstSpinExpMul"] = 1.2, ["lastSpinExpMul"] = 1.2 } },
            new Perk { id = "battery", emoji = "🔋", name = "배터리", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "스핀마다 EXP +6", fx = new Dictionary<string, double> { ["flatExp"] = 6 } },
            new Perk { id = "charm_relic", emoji = "🧿", name = "부적", tier = Tier.GOLD, cat = PCat.RELIC, price = 20, desc = "모든 EXP +12%", fx = new Dictionary<string, double> { ["expMul"] = 1.12 } },
            // ── 세트 컴포넌트 유물 (2026-06-24, L527-547) ──
            new Perk { id = "cherry_press", emoji = "🧃", name = "체리 압축기", tier = Tier.SILVER, cat = PCat.RELIC, price = 10, desc = "🍒체리 EXP +2", fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 2 } },
            new Perk { id = "cherry_can", emoji = "🥫", name = "체리 통조림", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "🍒체리 EXP +3", fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = 3 } },
            new Perk { id = "auto_pen", emoji = "🖋️", name = "자동 필기 펜", tier = Tier.SILVER, cat = PCat.RELIC, price = 10, desc = "📘책 EXP +2", fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 2 } },
            new Perk { id = "library_card", emoji = "🪪", name = "도서관 카드", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "📘책 EXP +3·학습태그 1개당 EXP +3", fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 3, ["tagExpBonus.학습"] = 3 } },
            new Perk { id = "greed_goblet", emoji = "🏆", name = "탐욕의 잔", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "모든 EXP +10%", fx = new Dictionary<string, double> { ["expMul"] = 1.10 } },
            new Perk { id = "ominous_skull", emoji = "💀", name = "불길한 해골 목걸이", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "☠해골이 EXP +5", fx = new Dictionary<string, double> { ["skullExp"] = 5 } },
            new Perk { id = "black_report", emoji = "📋", name = "검은 성적표", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "☠해골이 EXP +4", fx = new Dictionary<string, double> { ["skullExp"] = 4 } },
            new Perk { id = "bloody_coupon", emoji = "🩸", name = "피 묻은 쿠폰북", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "☠해골이 EXP +4·코인 +20%", fx = new Dictionary<string, double> { ["skullExp"] = 4, ["coinMul"] = 1.2 } },
            new Perk { id = "crown_stand", emoji = "🏛️", name = "왕관 받침대", tier = Tier.GOLD, cat = PCat.RELIC, price = 20, desc = "👑왕관 점수 +25", fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 25 } },
            new Perk { id = "broken_crown", emoji = "👑", name = "깨진 왕관", tier = Tier.SILVER, cat = PCat.RELIC, price = 16, desc = "👑왕관 점수 +15", fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 15 } },
            new Perk { id = "kings_ledger", emoji = "📜", name = "왕의 족보", tier = Tier.GOLD, cat = PCat.RELIC, price = 22, desc = "👑왕관 점수 +20·등장↑", fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 20, ["symbolWeightMul.crown"] = 1.5 } },
            new Perk { id = "flame_canister", emoji = "🛢️", name = "불꽃 저장통", tier = Tier.GOLD, cat = PCat.RELIC, price = 16, desc = "모든 EXP +8%", fx = new Dictionary<string, double> { ["expMul"] = 1.08 } },
            new Perk { id = "hot_handle", emoji = "🔥", name = "뜨거운 슬롯핸들", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "모든 EXP +9%", fx = new Dictionary<string, double> { ["expMul"] = 1.09 } },
            new Perk { id = "fate_handle", emoji = "🎰", name = "운명의 손잡이", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "희귀심볼 등장 +25%", fx = new Dictionary<string, double> { ["rareWeightMul"] = 1.25 } },
            new Perk { id = "gamblers_eye", emoji = "👁️", name = "도박사의 눈", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "희귀심볼 등장 +20%", fx = new Dictionary<string, double> { ["rareWeightMul"] = 1.20 } },
            new Perk { id = "old_wallet", emoji = "👛", name = "낡은 지갑", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "코인 +20%", fx = new Dictionary<string, double> { ["coinMul"] = 1.2 } },
            new Perk { id = "crumpled_coupon", emoji = "🧾", name = "구겨진 쿠폰", tier = Tier.SILVER, cat = PCat.RELIC, price = 10, desc = "코인 +20%", fx = new Dictionary<string, double> { ["coinMul"] = 1.2 } },
            new Perk { id = "cursed_wallet", emoji = "💰", name = "저주받은 지갑", tier = Tier.GOLD, cat = PCat.RELIC, price = 18, desc = "코인 +30%·☠해골 EXP +2", fx = new Dictionary<string, double> { ["coinMul"] = 1.3, ["skullExp"] = 2 } },
            new Perk { id = "practice_pad", emoji = "📓", name = "연습장", tier = Tier.SILVER, cat = PCat.RELIC, price = 10, desc = "📘책 EXP +2", fx = new Dictionary<string, double> { ["perSymbolExp.book"] = 2 } },
            new Perk { id = "calculator", emoji = "🧮", name = "작은 계산기", tier = Tier.SILVER, cat = PCat.RELIC, price = 12, desc = "💎보석 점수 +12", fx = new Dictionary<string, double> { ["perSymbolScore.gem"] = 12 } },
            new Perk { id = "lucky_eraser", emoji = "🩹", name = "행운의 지우개", tier = Tier.SILVER, cat = PCat.RELIC, price = 14, desc = "희귀심볼 등장 +15%", fx = new Dictionary<string, double> { ["rareWeightMul"] = 1.15 } },
            // ── 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, data.js:480-493) — PRISM 유물 12종 신설 ──
            // 프리즘(PRISM) 유물 8종(2026-06-30, engine.js:332-339) — 보스클리어 유물노드 PRISM 풀.
            new Perk { id = "prism_diploma", emoji = "🎖️", name = "명예졸업장", tier = Tier.PRISM, cat = PCat.RELIC, price = 28, desc = "EXP +40%·점수 +20%", fx = new Dictionary<string, double> { ["expMul"] = 1.40, ["scoreMul"] = 1.20 } },
            new Perk { id = "golden_ratio", emoji = "📐", name = "황금비율", tier = Tier.PRISM, cat = PCat.RELIC, price = 30, desc = "세트3+ EXP +40%·세트4+ 점수 +40%", fx = new Dictionary<string, double> { ["set3ExpMul"] = 1.40, ["set4ScoreMul"] = 1.40 } },
            new Perk { id = "starlight_crown", emoji = "🌟", name = "별빛왕관", tier = Tier.PRISM, cat = PCat.RELIC, price = 30, desc = "👑왕관 점수 +60·등장↑·코인 +30%", fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 60, ["coinMul"] = 1.30, ["symbolWeightMul.crown"] = 1.4 } },
            new Perk { id = "endless_recess", emoji = "⏱️", name = "무한자습", tier = Tier.PRISM, cat = PCat.RELIC, price = 36, desc = "스핀 +1·첫끝 스핀 EXP +35%", fx = new Dictionary<string, double> { ["bonusSpins"] = 1, ["firstSpinExpMul"] = 1.35, ["lastSpinExpMul"] = 1.35 } },
            new Perk { id = "fortunes_wheel", emoji = "🎡", name = "운명의수레바퀴", tier = Tier.PRISM, cat = PCat.RELIC, price = 34, desc = "희귀심볼 등장 +25%·희귀 2개+ 스핀 EXP +60%·점수 +30%", fx = new Dictionary<string, double> { ["rareBurstExpMul"] = 1.60, ["rareBurstScoreMul"] = 1.30, ["rareWeightMul"] = 1.25 } },
            new Perk { id = "set_resonator", emoji = "🎼", name = "공명세트", tier = Tier.PRISM, cat = PCat.RELIC, price = 28, desc = "세트 보너스 +50%·2세트 추가 +30%", fx = new Dictionary<string, double> { ["setExpMul"] = 1.50, ["twoSetBonusMul"] = 1.30 } },
            new Perk { id = "reapers_pact", emoji = "⚰️", name = "사신의계약", tier = Tier.PRISM, cat = PCat.RELIC, price = 28, desc = "☠해골 EXP +10·점수 +15%", fx = new Dictionary<string, double> { ["skullExp"] = 7, ["perSkullExp"] = 3, ["scoreMul"] = 1.15 } },
            new Perk
            {
                id = "phoenix_thesis", emoji = "🔥", name = "불사조논문", tier = Tier.PRISM, cat = PCat.RELIC, price = 34,
                desc = "EXP가 요구치 50% 미만이면 그 스핀 EXP ×2",
                // Kotlin(웹): if (stage>0 && quota>0 && stageExp < floor(quota*0.5)) cliffBurstExpMul *= 2.0
                // — cliff_focus와 동일한 cond.* 조건부 표기(신규 Mods.cliffBurstExpMul, SpinResolver.Evaluate
                // perfectShapeExpMul 직후에서 소비).
                fx = new Dictionary<string, double> { ["cond.quotaPct"] = 0.5, ["cond.cliffBurstExpMul"] = 2.0 },
            },
            // ── 후반(심연) 유물 4종(unlockLevel, engine.js:306-309) ──
            new Perk { id = "crown_monolith", emoji = "👑", name = "왕관 모노리스", tier = Tier.PRISM, cat = PCat.RELIC, price = 28, unlockLevel = 10, desc = "👑왕관 점수 +80·등장↑", fx = new Dictionary<string, double> { ["perSymbolScore.crown"] = 80, ["symbolWeightMul.crown"] = 1.5 } },
            new Perk
            {
                id = "black_grad_photo", emoji = "🖤", name = "검은 졸업사진", tier = Tier.PRISM, cat = PCat.RELIC, price = 26, unlockLevel = 12,
                desc = "저주 1개당 점수 +12%",
                // Kotlin(웹): scoreMul *= (1 + 0.12 * curseCount) — curse_grad와 동일 cond 키 재사용(값만 다름).
                fx = new Dictionary<string, double> { ["cond.curseScoreBase"] = 0.12 },
            },
            new Perk { id = "last_roll", emoji = "📋", name = "최후의 출석부", tier = Tier.PRISM, cat = PCat.RELIC, price = 30, unlockLevel = 15, desc = "막스핀 EXP ×2 · 첫스핀 -10%", fx = new Dictionary<string, double> { ["lastSpinExpMul"] = 2.0, ["firstSpinExpMul"] = 0.9 } },
            new Perk { id = "nameless_cup", emoji = "🏆", name = "무명왕의 잔", tier = Tier.PRISM, cat = PCat.RELIC, price = 40, unlockLevel = 20, desc = "점수 ×1.6 · 요구 EXP +25% (고점)", fx = new Dictionary<string, double> { ["scoreMul"] = 1.6, ["quotaMul"] = 1.25 } },
        });

        // ── 저주 16종 — 웹 파리티 P3-4(WEB_PARITY_DESIGN.md §1-A #14, data.js:626-651 CURSES) ──
        // Kotlin 원본(양면형: 페널티+보너스 동시)을 웹(패널티 전용, "장점 없음" — 이득은 별도 투자로만
        // 캐릭터 cultist·증강 sacrifice/black_diploma를 통해서만 얻는다)으로 전량 교체했다. fx는
        // engine.js:378-395 buildMods 저주 루프 그대로(desc도 data.js 문구로 교체). 전부 Tier.GOLD
        // 고정·price=0(원본과 동일, data.js에 price 필드 자체가 없음).
        public static readonly Perk[] Curses = WithSchool(new[]
        {
            new Perk { id = "hard_exam", emoji = "📝", name = "어려운시험", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "요구치 +10%", fx = new Dictionary<string, double> { ["quotaMul"] = 1.10 } },
            new Perk { id = "cursed_skulls", emoji = "☠", name = "저주받은패", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "☠해골 등장↑ · EXP -4", fx = new Dictionary<string, double> { ["weightAdd.skull"] = 4.0, ["flatExp"] = -4 } },
            new Perk { id = "speed_test", emoji = "⏱️", name = "속성평가", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "스핀 -1", fx = new Dictionary<string, double> { ["bonusSpins"] = -1 } },
            new Perk { id = "frugal_vow", emoji = "🪙", name = "청빈서약", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "코인 -40%", fx = new Dictionary<string, double> { ["coinMul"] = 0.6 } },
            new Perk { id = "tunnel_vision", emoji = "🎯", name = "외골수", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "양끝맞춤 EXP -50% · 첫스핀 EXP -15%", fx = new Dictionary<string, double> { ["endsMatchExpMul"] = 0.5, ["firstSpinExpMul"] = 0.85 } },
            new Perk { id = "late_bloomer", emoji = "🌙", name = "늦깎이", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "첫스핀 EXP -50%", fx = new Dictionary<string, double> { ["firstSpinExpMul"] = 0.5 } },
            new Perk { id = "gem_obsession", emoji = "💎", name = "보석집착", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "🍒체리·📘책 EXP -2", fx = new Dictionary<string, double> { ["perSymbolExp.cherry"] = -2, ["perSymbolExp.book"] = -2 } },
            new Perk { id = "high_stakes", emoji = "🎲", name = "한탕주의", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "요구치 +8%", fx = new Dictionary<string, double> { ["quotaMul"] = 1.08 } },
            new Perk { id = "thorny_path", emoji = "🌵", name = "가시밭길", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "☠해골 등장↑ · ☠해골 EXP -5", fx = new Dictionary<string, double> { ["weightAdd.skull"] = 3.0, ["skullExp"] = -5 } },
            new Perk { id = "hex_allornothing", emoji = "⚡", name = "일발역전", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "세트 EXP -50%", fx = new Dictionary<string, double> { ["setExpMul"] = 0.5 } },
            new Perk { id = "sleep_debt", emoji = "😴", name = "수면부족", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "스핀마다 EXP -5", fx = new Dictionary<string, double> { ["flatExp"] = -5 } },
            new Perk { id = "diploma_pressure", emoji = "🎓", name = "학위압박", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "요구치 +12%", fx = new Dictionary<string, double> { ["quotaMul"] = 1.12 } },
            new Perk { id = "exam_week", emoji = "📅", name = "시험기간", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "요구치 +12%", fx = new Dictionary<string, double> { ["quotaMul"] = 1.12 } },
            new Perk { id = "blackout", emoji = "🌑", name = "정전", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "☠해골 등장↑", fx = new Dictionary<string, double> { ["weightAdd.skull"] = 4.0 } },
            new Perk { id = "pop_quiz", emoji = "❓", name = "쪽지시험", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "스핀 -1", fx = new Dictionary<string, double> { ["bonusSpins"] = -1 } },
            new Perk { id = "student_debt", emoji = "💸", name = "학자금", tier = Tier.GOLD, cat = PCat.CURSE, price = 0, desc = "코인 -50%", fx = new Dictionary<string, double> { ["coinMul"] = 0.5 } },
        });

        public static readonly IReadOnlyDictionary<string, Perk> All = BuildAll();

        public static Perk ById(string id)
        {
            return All.TryGetValue(id, out var p) ? p : null;
        }

        private static IReadOnlyDictionary<string, Perk> BuildAll()
        {
            var d = new Dictionary<string, Perk>(Augments.Length + Relics.Length + Curses.Length);
            foreach (var p in Augments) d[p.id] = p;
            foreach (var p in Relics) d[p.id] = p;
            foreach (var p in Curses) d[p.id] = p;
            return d;
        }

        // ── school 산출 (perkGate 결합 로직 이식, L824-836) ─────────────────
        private static Perk[] WithSchool(Perk[] perks)
        {
            foreach (var p in perks) p.school = SchoolFor(p.id, p.desc);
            return perks;
        }

        private static string SchoolFor(string id, string desc)
        {
            if (IsBasePerk(id)) return "";           // ① BASE_PERK_IDS(L706-713) → 빈 게이트, school 없음
            var over = OverrideSchool(id);
            if (over != null) return over;            // ② PERK_GATE_OVERRIDES(L753-807, 45건) → 개별 지정
            return InferSchool(desc);                  // ③ inferSchool(desc)(L810-822) → 테마 추론
        }

        // BASE_PERK_IDS — 게이트 없음(항상 등장) 22종 (L706-713). 단일 정의는 Schools.BasePerkIds.
        private static bool IsBasePerk(string id) => Schools.BasePerkIds.Contains(id);

        // PERK_GATE_OVERRIDES 45건의 school만 발췌(L753-807). minLevel/req 전체 게이트는 Schools.PerkGateOverrides(S2b) 참조.
        private static string OverrideSchool(string id)
        {
            switch (id)
            {
                case "overdrive": case "short_day": case "glass_cannon": case "supernova":
                    return "프리즘공학";
                case "endgame_rush": case "hourglass_r": case "late_focus": case "cliff_focus": case "fate_bell":
                    return "시간학";
                case "wild_world": case "joker":
                    return "와일드학";
                case "jackpot": case "mega_jackpot": case "crown_jewel": case "crown_stand": case "kings_ledger":
                    return "왕관학";
                case "seed_garden": case "great_harvest":
                    return "씨앗학";
                case "key_master": case "piggy_bank":
                    return "경제학";
                case "gamblers_dice": case "crystal_ball": case "fate_handle": case "gamblers_eye":
                case "fortune_check": case "luck_accum": case "fate_burst":
                    return "운명학";
                case "skull_idol": case "ominous_skull": case "black_report": case "flame_canister":
                case "cursed_wallet": case "skull_watch": case "sacrifice": case "black_diploma":
                    return "저주학";
                case "focus_ring": case "silver_mirror": case "pair_match": case "puzzle_sense": case "perfect_shape":
                    return "계산학";
                case "greed_goblet": case "early_prep": case "growth_log": case "early_adapt": case "snowball":
                    return "성장학";
                default:
                    return null;
            }
        }

        // inferSchool(p) — desc 텍스트 기반 전공 추론, 우선순위 순서대로 첫 매치 채택 (L810-822)
        private static string InferSchool(string d)
        {
            if (d.Contains("☠") || d.Contains("해골") || d.Contains("저주")) return "저주학";
            if (d.Contains("👑") || d.Contains("왕관") || d.Contains("🌀") || d.Contains("와일드") || d.Contains("잭팟")) return "왕관학";
            if (d.Contains("🌱") || d.Contains("씨앗")) return "씨앗학";
            if (d.Contains("🎲") || d.Contains("주사위") || d.Contains("희귀") || d.Contains("올인") || d.Contains("기도")) return "운명학";
            if (d.Contains("코인") || d.Contains("🪙") || d.Contains("상점") || d.Contains("🗝") || d.Contains("열쇠")) return "경제학";
            if (d.Contains("막") || d.Contains("첫스핀") || d.Contains("첫 스핀") || d.Contains("마지막 스핀") || d.Contains("막스핀")) return "시간학";
            if (d.Contains("세트") || d.Contains("가운데") || d.Contains("양끝") || d.Contains("인접") || d.Contains("붙은") || d.Contains("콤보")) return "계산학";
            return "성장학";
        }
    }
}
