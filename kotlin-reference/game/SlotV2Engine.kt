package com.ashersoft.kakaobot.game

import kotlin.random.Random

/**
 * 잭팟런 v2 — 단일 라인(5칸) 로그라이크 슬롯 엔진 (순수 로직, 상태 없음).
 *
 * v1(SlotEngine, 3×3 8라인)과 병행 개발. 완성·검증 후 "슬롯" 커맨드를 v2로 전환 예정.
 * 라이브 게임(v1)을 깨지 않도록 새 파일/새 object 로 격리.
 *
 * ── 3화폐 ──
 *  경험치(EXP)  = 스테이지 쿼터 진행. 초과분은 점수로 환산(+이월 증강).
 *  점수(SCORE)  = 리더보드 고득점. 런 내내 누적, 게임오버 시 기록.
 *  코인(COIN)   = 상점 화폐. 런 한정 휘발.
 *
 * 스핀 독립 원칙 유지(런 누적 곱배수 없음). 성장은 증강/유물/아이템(perk)으로만.
 * 밸런스 수치는 전부 이 파일 상단 const/표 로.
 */
object SlotV2Engine {

    // ── 밸런스 상수 ─────────────────────────────────────────
    const val REEL = 5                 // 단일 라인 5칸
    const val SPINS_PER_STAGE = 5
    const val MIN_SPINS = 3
    const val COIN_BASE = 0            // 스핀당 기본 코인(코인은 🪙심볼·클리어보상 위주)
    const val SKULL_PENALTY = 3        // ☠ 기본 페널티(skulls×3×skullPenaltyMul; perk/머신 가산으로 돌파)
    const val BOMB_EXP_PER = 8         // 💣 제거 1칸당 EXP
    const val KEY_COIN_PER = 4         // 🗝 열쇠 셀당 보물 코인(vault 머신 "코인↑" 테마 — keyBoost 死플래그 대체)
    const val MAX_SPIN_EXP_MUL = 8.0   // 한 스핀 EXP 배율 상한(고점빌드 폭주 통제 — 일반 곱연산 안전장치)
    const val UNLUCKY_MAX = 5          // 불운 게이지 만땅(나쁜 스핀 누적 → 다음 보상 희귀↑ 보장)

    // ── 특수 스핀명령 코인 비용 ────────────────────────────
    // 일반 스테이지: 집중1·최후2·기도3·올인4. 보스 스테이지: 각 +1(최대 5). 일반 스핀(N)=0.
    const val CMD_COST_FOCUS = 1       // 집중 FOCUS
    const val CMD_COST_LAST  = 2       // 최후 LAST
    const val CMD_COST_PRAY  = 3       // 기도 PRAY
    const val CMD_COST_ALLIN = 4       // 올인 ALLIN
    const val CMD_COST_BOSS_SURCHARGE = 1  // 보스 스테이지 가산
    const val CMD_COST_MAX = 5         // 비용 상한(0은 0 유지)

    /** 스테이지별 목표 EXP. 무증강 클리어율 목표(1~5: 90/82/70/56/42%)에 맞춰 몬테카를로 역산.
     *  6~15는 빌드 성장 대비 가파른 램프(콘텐츠 확정 후 빌드 클리어율로 재튜닝). 15 이후 ×1.2. */
    private val QUOTAS = longArrayOf(110, 120, 130, 140, 150, 170, 200, 235, 280, 330, 390, 460, 540, 640, 755)
    fun quota(stage: Int): Long {
        val i = stage - 1
        if (i < 0) return QUOTAS[0]
        if (i < QUOTAS.size) return QUOTAS[i]
        var q = QUOTAS.last().toDouble()
        repeat(i - QUOTAS.size + 1) { q *= 1.2 }
        return q.toLong()
    }

    /** 5스테이지마다 보스. */
    fun isBossStage(stage: Int): Boolean = stage % 5 == 0

    // ── 보스 특수룰 (5스테이지마다, 순환) ────────────────────
    data class Boss(val id: String, val emoji: String, val name: String, val desc: String,
                    val bonusSpins: Int = 2, val quotaMul: Double = 1.0, val counterTags: List<String> = emptyList())
    //  bonusSpins 각 -1 (해금 시스템 동봉, 2026-06-29) — 보스 스핀 여유 축소로 난이도 상향.
    val BOSSES = listOf(
        Boss("finals", "📝", "기말고사",   "스핀+1·요구↑ · 막스핀 EXP ×2 · 첫스핀 -10%", 1, counterTags = listOf("막판형(복습·벼락치기)", "⏰최후")),
        Boss("strict", "👨‍🏫", "꼰대교수",   "스핀+1·요구↑ · 같은심볼 3개↑ 없는 스핀 ×0.5", 1, counterTags = listOf("세트·콤보(자석·체리)", "📌고정·📑복사")),
        Boss("luck",   "🎲", "운빨심판관", "스핀+1·요구↑ · ⭐👑🌀 있으면 ×1.8/없으면 ×0.8", 1, counterTags = listOf("⭐👑🌀 등장↑", "🔮예언·🎲올인")),
        Boss("grad",   "🎓", "졸업심사",   "스핀+1·요구↑↑ (빡센 관문)", 1, 1.15, counterTags = listOf("EXP 총량(과부하·탐욕)", "🚑응급처치·아이템")),
    )
    fun bossFor(stage: Int): Boss? = if (stage % 5 == 0) BOSSES[((stage / 5) - 1) % BOSSES.size] else null
    fun bossSpins(stage: Int): Int = bossFor(stage)?.bonusSpins ?: 0
    fun bossQuotaMul(stage: Int): Double = bossFor(stage)?.quotaMul ?: 1.0

    /** (C2) 한 스핀 총배율 상한 — 보스 우선(프리즘 보유여도 보스에선 5/없으면 4),
     *  비보스 프리즘 보유 ×8(MAX_SPIN_EXP_MUL), 그 외 ×5. evaluate(capMul) 와 expMul 클램프 양쪽에서 공용. */
    fun capMulFor(stage: Int, hasPrism: Boolean): Double = when {
        bossFor(stage) != null -> if (hasPrism) 5.0 else 4.0
        hasPrism -> MAX_SPIN_EXP_MUL
        else -> 5.0
    }

    // ── 보상/점수 상수 (서비스에서 사용) ──
    const val CLEAR_COIN = 5
    const val BOSS_COIN = 12
    const val ELITE_COIN = 8
    const val SCORE_PER_LEFTOVER = 2     // 초과 EXP 1당 점수
    const val SCORE_PER_LEFTSPIN = 100   // 남은 스핀 1개당 점수
    const val CLOSE_CLEAR_BONUS = 150L   // 부족 10이하로 클리어
    const val BOSS_CLEAR_SCORE = 500L

    /** 스테이지 클리어 점수 = 스테이지×50 + 초과×2 + 남은스핀×100 (+보스500) ×(1+저주5%). */
    fun stageClearScore(stage: Int, leftoverExp: Long, leftSpins: Int, curses: Int, boss: Boolean): Long {
        var s = stage * 50.0
        s += leftoverExp * SCORE_PER_LEFTOVER
        s += leftSpins * SCORE_PER_LEFTSPIN
        if (boss) s += BOSS_CLEAR_SCORE
        s *= (1.0 + 0.05 * curses)
        return s.toLong()
    }

    // ── 심볼 ────────────────────────────────────────────────
    enum class Sp { NONE, WILD, BOMB, MAGNET, SKULL, DICE, COIN, KEY, FLAME, SEED }

    data class Sym(
        val id: String, val emoji: String, val name: String,
        val exp: Int = 0, val score: Int = 0, val coin: Int = 0,
        val weight: Int = 0, val special: Sp = Sp.NONE, val rare: Boolean = false,
        /** 태그 — 태그 기반 유물/증강의 판정 키(예: "학습" 2개↑면 보너스). */
        val tags: Set<String> = emptySet(),
    )

    private val EMPTY = Sym("empty", "▫", "빈칸")
    val EMPTY_PUB = EMPTY   // applyCellOps 공개용(빈칸=exp0/score0/NONE, evaluate서 무해 처리)

    // 기본 풀(10종) — 밸런스 스펙 확률(가중=%). 특수 4종(🗝🎲🌱🌀)은 weight 0 = 휴면,
    // 추후 증강/유물/이벤트로 풀에 주입(예: '씨앗 재배', '와일드 교육').
    val SYMS = listOf(
        Sym("cherry", "🍒", "체리",   exp = 3,  weight = 25, tags = setOf("생명")),
        Sym("book",   "📘", "책",     exp = 6,  weight = 18, tags = setOf("학습")),
        Sym("star",   "⭐", "별",     exp = 8,  weight = 13, tags = setOf("콤보")),
        Sym("gem",    "💎", "보석",   exp = 1,  score = 15, weight = 12, tags = setOf("점수")),
        Sym("coin",   "🪙", "코인",   coin = 1, weight = 10, special = Sp.COIN, tags = setOf("코인")),
        Sym("skull",  "☠", "해골",   weight = 10, special = Sp.SKULL, tags = setOf("저주")),
        Sym("flame",  "🔥", "불꽃",   weight = 5, special = Sp.FLAME, tags = setOf("배율")),
        Sym("magnet", "🧲", "자석",   exp = 2, weight = 4, special = Sp.MAGNET, tags = setOf("조작")),
        Sym("bomb",   "💣", "폭탄",   exp = 5, weight = 2, special = Sp.BOMB, tags = setOf("폭발")),
        Sym("crown",  "👑", "왕관",   exp = 20, score = 50, weight = 1, rare = true, tags = setOf("왕관", "희귀")),
        // ── 휴면(특수) ──
        Sym("key",    "🗝", "열쇠",   exp = 6,  weight = 0,  special = Sp.KEY, tags = setOf("열쇠")),
        Sym("dice",   "🎲", "주사위", weight = 0, special = Sp.DICE, tags = setOf("운")),
        Sym("seed",   "🌱", "씨앗",   weight = 0, special = Sp.SEED, tags = setOf("생명", "성장")),
        Sym("wild",   "🌀", "와일드", weight = 0, special = Sp.WILD, rare = true, tags = setOf("희귀", "조작")),
    )
    val SYM_BY_ID = SYMS.associateBy { it.id }
    private val VALUE_IDS = setOf("cherry", "star", "book", "gem", "crown") // 세트 집계 대상

    /** 같은 심볼 N개(와일드 포함) 세트 보너스. index = 개수(0~5). 기본 EXP 스케일에 맞춰 보수적. */
    private val SET_EXP = intArrayOf(0, 0, 8, 18, 42, 100)
    private val SET_SCORE = intArrayOf(0, 0, 3, 9, 24, 70)

    // ── 집계 효과(증강/유물/아이템) — 콘텐츠 추가 시 필드 확장 ──
    data class Mods(
        val expMul: Double = 1.0,
        val scoreMul: Double = 1.0,
        val coinMul: Double = 1.0,
        val flatExp: Int = 0,
        val flatScore: Int = 0,
        val bonusSpins: Int = 0,
        val skullExp: Int = 0,            // ☠ 1개당 EXP 가산(해골빌드; 페널티 상쇄/역전)
        val skullPenaltyMul: Double = 1.0,
        val setExpMul: Double = 1.0,
        val perSymbolExp: Map<String, Int> = emptyMap(), // 심볼별 추가 EXP(체리강화 등)
        val firstSpinExpMul: Double = 1.0,
        val lastSpinExpMul: Double = 1.0,
        val rareWeightMul: Double = 1.0,  // 희귀심볼(와일드/왕관) 등장 가중
        val overkillScoreMul: Double = 1.0,
        val carryoverPct: Int = 0,        // 초과 EXP 다음 스테이지 이월%
        // ── 한 줄 판정 훅 (기본 무효과 → 콘텐츠가 켬) ──
        val tagExpBonus: Map<String, Int> = emptyMap(),  // 태그 1개당 EXP(학습+/저주+ 등)
        val centerExpMul: Double = 1.0,   // 3번 칸(가운데) 심볼 EXP 배수
        val endsMatchExpMul: Double = 1.0,// 양끝(1·5번)이 같은 값심볼이면 EXP 배수
        val adjacentSameExp: Int = 0,     // 인접한 같은 값심볼 쌍 1개당 EXP
        // ── 머신/캐릭터/perk ──
        val symbolWeightMul: Map<String, Double> = emptyMap(), // 심볼 등장 가중 배수(머신)
        val weightAdd: Map<String, Double> = emptyMap(),       // 심볼 가중 가산(휴면심볼 주입 — 프리즘)
        val perSymbolScore: Map<String, Int> = emptyMap(),     // 심볼별 추가 점수
        val quotaMul: Double = 1.0,        // 요구 EXP 배수(캐릭터 — 서비스 적용)
        val clearCoinBonus: Int = 0,       // 스테이지 클리어 시 추가 코인(서비스 적용)
        val skullScoreBonus: Int = 0,    // (e) ☠ 1개당 점수 가산(해골스티커 NEXTSPIN)
        // ── 신규 16종(2026-06-29) per-spin 조건부 훅 — buildMods 가 켜고 evaluate 가 셀 내용으로 판정 ──
        val perSkullExp: Int = 0,          // skull_watch: ☠ 1개당 EXP 가산(페널티와 별개의 보너스)
        val skull3ScoreMul: Double = 1.0,  // skull_watch: ☠3개+ 스핀 점수 배수(0.9 = -10%)
        val rareBurstExpMul: Double = 1.0, // fate_burst: 희귀 2개+ 스핀 EXP 배수(보스전 약화)
        val rareBurstScoreMul: Double = 1.0,// fate_burst: 희귀 2개+ 스핀 점수 배수
        val twoSetBonusMul: Double = 1.0,  // pair_match: 같은심볼 2세트(bestCount==2) 세트 보너스 추가 배수
        val set3ExpMul: Double = 1.0,      // puzzle_sense: 세트3+ EXP 배수
        val set4ScoreMul: Double = 1.0,    // puzzle_sense: 세트4+ 점수 배수
        val perfectShapeExpMul: Double = 1.0, // perfect_shape: 양끝 같고 가운데 같은계열 EXP 배수(와일드충족 약화)
    )

    // ── 해금 복합조건(공용) ───────────────────────────────────
    //  unlockReq/license = (statKey, 임계) 리스트의 AND. 빈 리스트 = 무료(스타터).
    //  statKey 는 누적 카운터(cherryTotal/bossClears/…) 또는 특수(bestScore/bestStage/runs,
    //  cstage_<charId>/mstage_<machineId> 머신·캐릭별 최고스테이지, distinctCharS10 등).
    //  서비스가 카운터맵 + bestScore/bestStage/runs + 파생키(distinctCharS10)를 합쳐 stat 으로 전달.
    fun meetsReq(req: List<Pair<String, Long>>, stat: Map<String, Long>): Boolean =
        req.all { (key, thr) -> (stat[key] ?: 0L) >= thr }

    /** 진행조건을 사람이 읽는 텍스트로 (예 "🍒체리 300 & 체리머신 S5"). 미충족 항목은 (현재/목표) 표기. */
    fun reqHint(req: List<Pair<String, Long>>, stat: Map<String, Long> = emptyMap()): String {
        if (req.isEmpty()) return "무료"
        return req.joinToString(" & ") { (key, thr) ->
            val cur = stat[key] ?: 0L
            val label = statLabel(key, thr)
            if (stat.isEmpty()) label else "$label(${if (cur >= thr) "✓" else "$cur/$thr"})"
        }
    }

    /** statKey + 임계 → 사람이 읽는 라벨. */
    fun statLabel(key: String, thr: Long): String = when {
        key == "bestScore" -> "${"%,d".format(thr)}점"
        key == "bestStage" -> "S$thr"
        key == "runs" -> "${thr}런"
        key == "distinctCharS10" -> "서로다른 캐릭 ${thr}명 S10"
        key == "minimalistS10" -> "유물3↓ S10 ${thr}회"
        key == "noItemS8" -> "아이템없이 S8 ${thr}회"
        key == "richBossClears" -> "코인50↑ 보스클리어 ${thr}회"
        key.startsWith("cstage_") -> "${character(key.removePrefix("cstage_")).emoji}${character(key.removePrefix("cstage_")).name} S$thr"
        key.startsWith("mstage_") -> "${machine(key.removePrefix("mstage_")).emoji}${machine(key.removePrefix("mstage_")).name} S$thr"
        else -> "${statName(key)} $thr"
    }
    private fun statName(key: String): String = when (key) {
        "cherryTotal" -> "🍒체리"; "bookTotal" -> "📘책"; "starTotal" -> "⭐별"
        "gemTotal" -> "💎보석"; "skullTotal" -> "☠해골"; "coinTotal" -> "🪙코인"
        "crownTotal" -> "👑왕관"; "bossClears" -> "보스클리어"; "exactClears" -> "정확클리어"
        "closeClears" -> "아슬아슬클리어"; "lastSpinClears" -> "막판클리어"; "prayClears" -> "기도클리어"
        "allinWins" -> "올인성공"; "jackpots" -> "잭팟"; "set4Plus" -> "4세트+"
        "prismPicks" -> "프리즘선택"; "shopBuys" -> "상점구매"; "gambles" -> "도박"
        "relicsMax" -> "유물보유"; "curseMax" -> "저주보유"; "devicesOwned" -> "장치소지"
        "totalSpins" -> "총스핀"; "itemsUsed" -> "아이템사용"; "rerollUses" -> "재굴림"
        "pinUses" -> "고정"; "deviceUses" -> "장치사용"
        // ── 표준 도전(배치3a) 전용 카운터 ──
        "noDevStage" -> "무장치 도달S"; "noShopS10" -> "무상점 S10"; "noItemMaxS" -> "무아이템 도달S"
        "curse5Stage" -> "저주5 도달S"; "curseBossClears" -> "저주3↑ 보스클리어"
        "maxOverPct" -> "최대초과%"; "maxRunJackpots" -> "한런 최다잭팟"
        else -> key
    }

    // ── 슬롯머신 (런 시작 시 선택 — 기본 확률/룰) ─────────────
    //  unlockReq = 테마 기반 복합 AND 조건(2개). 빈 리스트 = 무료(스타터=기본).
    data class Machine(
        val id: String, val emoji: String, val name: String, val desc: String,
        val weightMul: Map<String, Double> = emptyMap(),
        val scoreMod: Double = 1.0,
        val weightAdd: Map<String, Double> = emptyMap(),   // 휴면심볼 주입(와일드/씨앗/주사위/열쇠)
        val unlockReq: List<Pair<String, Long>> = emptyList(),   // 테마 복합조건(AND). 빈=무료(스타터)
    )
    val MACHINES = listOf(
        Machine("basic",   "🎰", "기본",   "표준 확률 (입문)"),   // 스타터(무료)
        // 체리 테마 — 체리 누적 + 도달(farmer가 cherry머신을 요구하므로 순환 회피: bestStage 사용)
        Machine("cherry",  "🍒", "체리",   "체리↑·왕관↓ (안정)", mapOf("cherry" to 1.5, "crown" to 0.6), 0.95,
            unlockReq = listOf("cherryTotal" to 200L, "bestStage" to 4L)),
        // 도서관 테마 — 책 누적 + 막판클리어(복습형)
        Machine("library", "📚", "도서관", "책↑·코인/보석↓ (경험치)", mapOf("book" to 1.5, "coin" to 0.6, "gem" to 0.6), 1.0,
            unlockReq = listOf("bookTotal" to 200L, "lastSpinClears" to 3L)),
        // 보석 테마 — 보석 누적 + 점수
        Machine("gem",     "💎", "보석",   "보석↑·체리/책↓ (점수)", mapOf("gem" to 1.7, "book" to 0.6, "cherry" to 0.6), 1.1,
            unlockReq = listOf("gemTotal" to 250L, "bestScore" to 4_000L)),
        // 자석 테마 — 콤보(세트) + 도달
        Machine("magnet",  "🧲", "자석",   "자석↑ (콤보)", mapOf("magnet" to 2.5), 1.0,
            unlockReq = listOf("set4Plus" to 8L, "bestStage" to 6L)),
        // 해골 테마 — 해골 누적 + 저주보유
        Machine("skull",   "☠", "해골",   "해골↑·고위험 (점수↑)", mapOf("skull" to 1.8), 1.10,
            unlockReq = listOf("skullTotal" to 250L, "curseMax" to 3L)),
        // 왕관 테마 — 왕관 누적 + 잭팟
        Machine("crown",   "👑", "왕관",   "왕관↑·기본↓ (운빨 고점)", mapOf("crown" to 2.0, "cherry" to 0.7, "book" to 0.7), 1.2,
            unlockReq = listOf("crownTotal" to 40L, "jackpots" to 3L)),
        // 불꽃 테마 — 배율(점수) + 도달
        Machine("flame",   "🔥", "불꽃",   "불꽃·해골↑ (배율형)", mapOf("flame" to 1.8, "skull" to 1.4), 1.1,
            unlockReq = listOf("bestScore" to 15_000L, "bestStage" to 10L)),
        // 폭탄 테마 — 보스클리어(계산) + 도달
        Machine("bomb",    "💣", "폭탄",   "폭탄↑ (제거/계산)", mapOf("bomb" to 2.5), 1.1,
            unlockReq = listOf("bossClears" to 5L, "bestStage" to 10L)),
        // ── 확장 머신 ──
        // 별빛 — 별 누적 + 4세트+
        Machine("star",    "⭐", "별빛",   "별↑·세트 잘맞음 (콤보)", mapOf("star" to 2.0, "cherry" to 0.8), 1.05,
            unlockReq = listOf("starTotal" to 200L, "set4Plus" to 10L)),
        // 행운 — 희귀(기도) + 도달
        Machine("clover",  "🍀", "행운",   "희귀·코인·불꽃↑ (행운)", mapOf("crown" to 1.3, "coin" to 1.4, "flame" to 1.3), 1.05,
            unlockReq = listOf("prayClears" to 3L, "bestStage" to 8L)),
        // 카지노 — 도박 + 올인
        Machine("casino",  "🎲", "카지노", "🎲주사위 등장·고변동 (운빨)", emptyMap(), 1.1, weightAdd = mapOf("dice" to 4.0),
            unlockReq = listOf("gambles" to 5L, "allinWins" to 5L)),
        // 정원 — 체리 + 도달(성장형)
        Machine("garden",  "🌱", "정원",   "🌱씨앗 등장·성장형", emptyMap(), 1.05, weightAdd = mapOf("seed" to 4.0),
            unlockReq = listOf("cherryTotal" to 400L, "bestStage" to 9L)),
        // 와일드 — 프리즘선택 + 잭팟(세트 조작)
        Machine("wildmac", "🌀", "와일드", "🌀와일드 등장 (세트 조작)", emptyMap(), 1.1, weightAdd = mapOf("wild" to 3.0),
            unlockReq = listOf("prismPicks" to 8L, "jackpots" to 5L)),
        // 금고 — 코인 + 상점구매(부유형)
        Machine("vault",   "🗝", "금고",   "🗝열쇠 등장·코인↑", mapOf("coin" to 1.5), 1.10, weightAdd = mapOf("key" to 3.0),
            unlockReq = listOf("coinTotal" to 600L, "shopBuys" to 20L)),
        // 무지개 — 고점(점수) + 잭팟(한방)
        Machine("rainbow", "🌈", "무지개", "⭐💎👑 등장↑·🍒📘↓·고변동 (한방)", mapOf("crown" to 1.6, "star" to 1.4, "gem" to 1.3, "cherry" to 0.6, "book" to 0.6), 1.2,
            unlockReq = listOf("bestScore" to 25_000L, "jackpots" to 10L)),
    )
    val BASE_MACHINE = MACHINES[0]
    fun machine(id: String): Machine = MACHINES.firstOrNull { it.id == id } ?: BASE_MACHINE

    fun machineUnlocked(m: Machine, stat: Map<String, Long>): Boolean = meetsReq(m.unlockReq, stat)
    fun unlockedMachines(stat: Map<String, Long>): List<Machine> = MACHINES.filter { machineUnlocked(it, stat) }
    fun lockedMachines(stat: Map<String, Long>): List<Machine> = MACHINES.filter { !machineUnlocked(it, stat) }
    /** 난이도 표기 — unlockReq 임계 합으로 근사(스타터=입문). */
    fun machineDiff(m: Machine): String = reqDiff(m.unlockReq)
    fun machineHint(m: Machine, stat: Map<String, Long> = emptyMap()): String = reqHint(m.unlockReq, stat)
    fun achName(id: String): String = ACHIEVEMENTS.firstOrNull { it.id == id }?.name ?: id

    /** 복합조건 난이도 등급 — 가장 무거운 임계 기준 근사. */
    private fun reqDiff(req: List<Pair<String, Long>>): String {
        if (req.isEmpty()) return "입문"
        val hard = req.any { (k, v) ->
            (k == "bestScore" && v >= 15_000) || (k == "bestStage" && v >= 12) ||
            (k == "bossClears" && v >= 8) || (k == "jackpots" && v >= 8)
        }
        val mid = req.any { (k, v) ->
            (k == "bestScore" && v >= 4_000) || (k == "bestStage" && v >= 8) ||
            (k == "bossClears" && v >= 3)
        }
        return when { hard -> "고급"; mid -> "중급"; else -> "초중급" }
    }

    // ── 캐릭터 (시작 능력/패널티/플레이 스타일) ──────────────
    //  unlockReq = 테마 기반 복합 AND 조건. 빈 리스트 = 무료(스타터=초보/장학생).
    data class Character(
        val id: String, val emoji: String, val name: String, val desc: String,
        val scoreMod: Double = 1.0,   // 리더보드 점수 보정(난이도 보상)
        val startCoins: Int = 0,
        val unlockReq: List<Pair<String, Long>> = emptyList(),   // 복합조건(AND). 빈=무료(스타터)
    )
    val CHARS = listOf(
        Character("novice",   "🎒", "초보학생", "요구치↓·점수보정 ×0.9 (입문)", 0.9),   // 스타터(무료)
        Character("scholar",  "📗", "장학생",   "📘책+2·클리어코인+2", 1.0),              // 스타터(무료)
        // ⚠️해금 임계 대폭↑ + 빌드특화(2026-06-30): S20/10만 한 판에 다 풀리던 문제 → 여러 포커스 런 필요. 이미 플레이한 캐릭은 charUnlocked 의 cstage grandfather 로 보존.
        // 도박꾼 — 도박 + 올인성공(누적)
        Character("gambler",  "🎲", "도박꾼",   "점수보정 ×1.1 · 스테이지당 1회 무료 재굴림", 1.1,
            unlockReq = listOf("gambles" to 12L, "allinWins" to 5L)),
        // 체리농부 — 체리 대량 누적 + 체리머신 S8(mstage_cherry = 체리머신으로 도달한 최고스테이지)
        Character("farmer",   "🍒", "체리농부", "🍒체리+1·희귀↓ (안정)", 0.95,
            unlockReq = listOf("cherryTotal" to 1200L, "mstage_cherry" to 8L)),
        // 알바생 — 코인 누적 + 상점구매 다수
        Character("parttime", "🪙", "알바생",   "시작코인+15·첫스핀 -20%", 1.0, startCoins = 15,
            unlockReq = listOf("coinTotal" to 1000L, "shopBuys" to 20L)),
        // 보석상 — 보석 대량 누적 + 고득점
        Character("jeweler",  "💎", "보석상",   "💎보석 점수+25·점수보정 ×1.1", 1.1,
            unlockReq = listOf("gemTotal" to 1200L, "bestScore" to 15_000L)),
        // 수석졸업생 — 정확클리어 다수 + 도달
        Character("honor",     "🎓", "수석졸업생",   "실버 증강 1개로 시작", 1.0,
            unlockReq = listOf("exactClears" to 6L, "bestStage" to 12L)),
        // 해골숭배자 — 해골 대량 누적 + 저주 5보유(저주빌드)
        Character("cultist",   "💀", "해골숭배자",   "☠해골 EXP+3·저주당 점수+8%", 1.15,
            unlockReq = listOf("skullTotal" to 1200L, "curseMax" to 5L)),
        // 왕관수집가 — 왕관 누적 + 잭팟 다수
        Character("crowncol",  "👑", "왕관수집가",   "👑왕관 점수+30·등장↑", 1.15,
            unlockReq = listOf("crownTotal" to 250L, "jackpots" to 6L)),
        // 미니멀리스트 — 유물3↓로 S10 달성 2회(런조건 카운터)
        Character("minimalist","🍃", "미니멀리스트", "유물 3개 이하면 EXP +25%", 1.1,
            unlockReq = listOf("minimalistS10" to 2L)),
        // ── 확장 캐릭터 ──
        // 행운아 — 기도(희귀) 클리어 다수
        Character("lucky",     "🍀", "행운아",       "희귀심볼 등장+25% (한방)", 1.05,
            unlockReq = listOf("prayClears" to 8L)),
        // 큰손 — 코인 대량 + 상점구매 다수(부유)
        Character("highroller","💠", "큰손",         "💎보석 점수+25·시작코인+12", 1.1, startCoins = 12,
            unlockReq = listOf("coinTotal" to 2500L, "shopBuys" to 40L)),
        // 수도승 — 아이템 없이 S8 도달 2회(런조건: noItemS8 = 그 런에 아이템 미사용 & S8 클리어 시 +1)
        Character("monk",      "🧘", "수도승",       "스핀-1·요구치-10% (속전속결)", 1.05,
            unlockReq = listOf("noItemS8" to 2L)),
        // 연금술사 — 코인50↑ 보유로 보스클리어 3회(런조건 카운터)
        Character("alchemist", "⚗️", "연금술사",     "코인+25%·클리어코인+3", 1.0,
            unlockReq = listOf("richBossClears" to 3L)),
        // 무모한도전 — 올인 다수 + 깊은 도달
        Character("daredevil", "😈", "무모한도전",   "모든 EXP+10%·요구치+20% · 남은≤2 EXP+35%·막스핀 +60% (막판형)", 1.2,
            unlockReq = listOf("allinWins" to 18L, "bestStage" to 14L)),
        // 천재 — 서로다른 캐릭 7명 S10
        Character("prodigy",   "🌟", "천재",         "모든 EXP+12%·점수보정 ×0.95", 0.95,
            unlockReq = listOf("distinctCharS10" to 7L)),
    )
    val BASE_CHAR = CHARS[0]
    fun character(id: String): Character = CHARS.firstOrNull { it.id == id } ?: BASE_CHAR

    // grandfather: 이미 플레이(클리어경험 cstage>0)한 캐릭은 임계 상향에도 유지(고인물 보존). 신규는 unlockReq 충족 필요.
    fun charUnlocked(c: Character, stat: Map<String, Long>): Boolean =
        meetsReq(c.unlockReq, stat) || (stat["cstage_" + c.id] ?: 0L) > 0L
    fun unlockedChars(stat: Map<String, Long>): List<Character> = CHARS.filter { charUnlocked(it, stat) }
    fun lockedChars(stat: Map<String, Long>): List<Character> = CHARS.filter { !charUnlocked(it, stat) }
    fun charDiff(c: Character): String = reqDiff(c.unlockReq)
    fun charHint(c: Character, stat: Map<String, Long> = emptyMap()): String = reqHint(c.unlockReq, stat)

    // ── 증강/유물 (perk) ────────────────────────────────────
    enum class Tier { SILVER, GOLD, PRISM }
    enum class PCat { AUGMENT, RELIC, CURSE }
    data class Perk(
        val id: String, val emoji: String, val name: String,
        val tier: Tier, val cat: PCat, val desc: String, val price: Int = 0,
    )

    val AUGMENTS = listOf(
        // 🥈 실버 — 단순 스탯
        Perk("study",      "📚", "기초학습", Tier.SILVER, PCat.AUGMENT, "모든 EXP +10%"),
        Perk("preview",    "🔍", "예습",     Tier.SILVER, PCat.AUGMENT, "첫 스핀 EXP +25%"),
        Perk("review",     "📖", "복습",     Tier.SILVER, PCat.AUGMENT, "마지막 스핀 EXP +25%"),
        Perk("diligence",  "✍️", "꾸준함",   Tier.SILVER, PCat.AUGMENT, "스핀마다 EXP +3"),
        Perk("cherry_up",  "🍒", "체리강화", Tier.SILVER, PCat.AUGMENT, "🍒체리 EXP +2"),
        Perk("book_up",    "📘", "책강화",   Tier.SILVER, PCat.AUGMENT, "📘책 EXP +2"),
        Perk("star_up",    "⭐", "별강화",   Tier.SILVER, PCat.AUGMENT, "⭐별 EXP +2"),
        Perk("gem_polish", "💎", "보석연마", Tier.SILVER, PCat.AUGMENT, "💎보석 점수 +10"),
        Perk("coin_luck",  "🪙", "동전운",   Tier.SILVER, PCat.AUGMENT, "코인 +30%"),
        Perk("set_sense",  "🎯", "콤보감각", Tier.SILVER, PCat.AUGMENT, "세트 보너스 +30%"),
        Perk("lucky",      "🍀", "행운부적", Tier.SILVER, PCat.AUGMENT, "희귀심볼 등장 +20%"),
        Perk("study_tag",  "🎓", "학구열",   Tier.SILVER, PCat.AUGMENT, "학습태그(📘) 1개당 EXP +4"),
        // 🥇 골드 — 빌드 정의
        Perk("cherry_farm","🍒", "체리농장", Tier.GOLD, PCat.AUGMENT, "🍒체리 EXP +4·등장↑"),
        Perk("library",    "📇", "도서관",   Tier.GOLD, PCat.AUGMENT, "📘책 EXP +4·학습태그 +3"),
        Perk("gem_invest", "💎", "보석투자", Tier.GOLD, PCat.AUGMENT, "💎보석 점수 +25"),
        Perk("skull_study","☠", "해골학",   Tier.GOLD, PCat.AUGMENT, "☠해골이 EXP +6"),
        Perk("center",     "🎯", "집중",     Tier.GOLD, PCat.AUGMENT, "가운데 칸 EXP 2배"),
        Perk("twins",      "↔️", "양끝맞춤", Tier.GOLD, PCat.AUGMENT, "양끝 같은 심볼이면 EXP 2배"),
        Perk("chain",      "🔗", "연쇄",     Tier.GOLD, PCat.AUGMENT, "붙은 같은 심볼 쌍당 EXP +20"),
        Perk("crown_seek", "👑", "왕관추종", Tier.GOLD, PCat.AUGMENT, "👑왕관 등장 2배·점수 +30"),
        Perk("greed",      "🤑", "탐욕",     Tier.GOLD, PCat.AUGMENT, "모든 EXP +25%"),
        Perk("insurance",  "❤️", "보험",     Tier.GOLD, PCat.AUGMENT, "스테이지 스핀 +1"),
        // 🌈 프리즘 — 시스템 변경
        Perk("overdrive",  "⚡", "과부하",   Tier.PRISM, PCat.AUGMENT, "모든 EXP +60%"),
        Perk("short_day",  "🏃", "조기퇴근", Tier.PRISM, PCat.AUGMENT, "스핀 -2 · 모든 EXP +120%"),
        Perk("wild_world", "🌀", "와일드세계", Tier.PRISM, PCat.AUGMENT, "🌀와일드 등장(세트 합류)"),
        Perk("seed_garden","🌱", "씨앗정원", Tier.PRISM, PCat.AUGMENT, "🌱씨앗 등장(다음스핀 성장)"),
        Perk("jackpot",    "🎰", "잭팟기계", Tier.PRISM, PCat.AUGMENT, "👑왕관 대량등장·점수 +50"),
        // ── 확장 (조건부/리스크/시스템) ──
        Perk("all_in",        "🎯", "몰아치기",   Tier.GOLD,  PCat.AUGMENT, "스핀 -1·모든 EXP +45%"),
        Perk("cram",          "⏰", "벼락치기",   Tier.GOLD,  PCat.AUGMENT, "첫스핀 -40%·막스핀 +120%"),
        Perk("high_roller",   "💠", "하이롤러",   Tier.GOLD,  PCat.AUGMENT, "💎보석 점수+30·EXP -8%"),
        Perk("all_or_nothing","☠", "해골도박",   Tier.GOLD,  PCat.AUGMENT, "☠해골 EXP+10·EXP -10%"),
        Perk("focus_fire",    "🔭", "정조준",     Tier.GOLD,  PCat.AUGMENT, "가운데 칸 EXP 2.5배"),
        Perk("symmetry",      "↔️", "대칭미학",   Tier.GOLD,  PCat.AUGMENT, "양끝맞춤 EXP 2.2배·인접쌍+12"),
        Perk("crammer_tag",   "🎓", "주입식",     Tier.GOLD,  PCat.AUGMENT, "학습태그당 EXP+7·책등장↑"),
        Perk("gamblers_dice", "🎲", "도박주사위", Tier.PRISM, PCat.AUGMENT, "🎲주사위 등장·EXP +15%"),
        Perk("key_master",    "🗝", "열쇠장인",   Tier.PRISM, PCat.AUGMENT, "🗝열쇠 등장·코인 +25%"),
        Perk("glass_cannon",  "⚡", "유리대포",   Tier.PRISM, PCat.AUGMENT, "스핀-1·EXP +90%·점수+10%"),
        Perk("rich_richer",   "🤑", "부익부",     Tier.PRISM, PCat.AUGMENT, "코인+60%·클코인+3·EXP-5%"),
        Perk("endgame_rush",  "🏁", "막판스퍼트", Tier.PRISM, PCat.AUGMENT, "막스핀 EXP 2.4배·첫스핀 -50%"),
        // ── 물량 확장 (2026-06-24) ──
        // 실버
        Perk("deep_read",   "📕", "정독",     Tier.SILVER, PCat.AUGMENT, "학습태그(📘) 1개당 EXP +3"),
        Perk("morning",     "🌅", "아침예습", Tier.SILVER, PCat.AUGMENT, "첫 스핀 EXP +30%"),
        Perk("evening",     "🌆", "야간자습", Tier.SILVER, PCat.AUGMENT, "마지막 스핀 EXP +30%"),
        Perk("note_take",   "📝", "필기",     Tier.SILVER, PCat.AUGMENT, "스핀마다 EXP +5"),
        Perk("star_up2",    "🌟", "별관측",   Tier.SILVER, PCat.AUGMENT, "⭐별 EXP +3"),
        Perk("magnet_up",   "🧲", "자석강화", Tier.SILVER, PCat.AUGMENT, "🧲자석 EXP +3"),
        Perk("gem_buff",    "💠", "보석세공", Tier.SILVER, PCat.AUGMENT, "💎보석 점수 +12"),
        Perk("combo_note",  "🎯", "콤보노트", Tier.SILVER, PCat.AUGMENT, "세트 보너스 +20%"),
        // 골드
        Perk("polymath",    "🧠", "박식",     Tier.GOLD, PCat.AUGMENT, "모든 EXP +20%"),
        Perk("necromancer", "💀", "강령술사", Tier.GOLD, PCat.AUGMENT, "☠해골이 EXP +8"),
        Perk("bullseye",    "🎯", "정조준2",  Tier.GOLD, PCat.AUGMENT, "가운데 칸 EXP 1.8배"),
        Perk("mirror",      "🪞", "거울대칭", Tier.GOLD, PCat.AUGMENT, "양끝 같은 심볼이면 EXP 1.9배"),
        Perk("domino",      "⛓️", "도미노",   Tier.GOLD, PCat.AUGMENT, "붙은 같은 심볼 쌍당 EXP +16"),
        Perk("honor_student","🎓", "수재",    Tier.GOLD, PCat.AUGMENT, "학습태그(📘) 1개당 EXP +6"),
        Perk("lapidary",    "💍", "세공장인", Tier.GOLD, PCat.AUGMENT, "💎보석 점수 +28"),
        Perk("royal_decree","📜", "왕명",     Tier.GOLD, PCat.AUGMENT, "👑왕관 등장↑·점수 +20"),
        // 프리즘
        Perk("supernova",   "💥", "초신성",   Tier.PRISM, PCat.AUGMENT, "모든 EXP +70%"),
        Perk("joker",       "🃏", "조커",     Tier.PRISM, PCat.AUGMENT, "🌀와일드 대량 등장"),
        Perk("great_harvest","🌾", "대수확",  Tier.PRISM, PCat.AUGMENT, "🌱씨앗 등장·🍒체리 EXP +3"),
        Perk("mega_jackpot","🎰", "대박기계", Tier.PRISM, PCat.AUGMENT, "👑왕관 대량등장·점수 +40"),
        Perk("time_warp",   "⏳", "시간왜곡", Tier.PRISM, PCat.AUGMENT, "스핀 +1·모든 EXP +20%"),
        // ── 세트 컴포넌트 증강 (2026-06-24) ──
        Perk("red_safetynet","🥅","붉은 안전망",Tier.SILVER,PCat.AUGMENT,"🍒체리 EXP +2"),
        Perk("polish_work","✨","광택 작업",Tier.GOLD,PCat.AUGMENT,"💎보석 점수 +25"),
        Perk("greed_calc","🤑","탐욕의 계산",Tier.GOLD,PCat.AUGMENT,"모든 EXP +15%"),
        Perk("overheat_formula","♨️","과열 공식",Tier.GOLD,PCat.AUGMENT,"모든 EXP +14%"),
        // ── 신규 16종 (빌드 축 5테마, 2026-06-29) — 해금 게이트 따름 ──
        //   초반성장(성장학)·운빨(운명학)·역전(시간학)·세트(계산학)·해골저주(저주학).
        //   run.growthStack/snowStack/fateBellUsed/unluckyGauge + stage/남은스핀 컨텍스트 참조(buildMods/evaluate).
        // 초반성장 (성장학)
        Perk("early_prep",  "🥚", "조기교육",   Tier.SILVER, PCat.AUGMENT, "S3 이하 EXP +15% (S6+ 무효)"),
        Perk("growth_log",  "📈", "성장일지",   Tier.SILVER, PCat.AUGMENT, "클리어마다 다음스테이지 첫스핀 EXP +8% (최대5·실패리셋)"),
        Perk("early_adapt", "🌱", "빠른적응",   Tier.GOLD,   PCat.AUGMENT, "S1~5 EXP +12% (S6+ 무효)"),
        Perk("snowball",    "❄️", "눈덩이",     Tier.PRISM,  PCat.AUGMENT, "남은스핀2+ 클리어시 다음 EXP +12% (최대4·보스후 -1)"),
        // 운빨 (운명학)
        Perk("fortune_check","🔍", "운세확인",  Tier.SILVER, PCat.AUGMENT, "스테이지 첫스핀 희귀 등장 +20%"),
        Perk("luck_accum",  "🎰", "불운적립",   Tier.GOLD,   PCat.AUGMENT, "희귀 미등장 스핀마다 불운+1 (3+면 다음 희귀↑)"),
        Perk("fate_burst",  "💫", "운명폭발",   Tier.PRISM,  PCat.AUGMENT, "희귀 2개+ 스핀 EXP +80%·점수 +50% (보스전 70%)"),
        // 막판역전 (시간학)
        Perk("late_focus",  "⏳", "후반집중",   Tier.SILVER, PCat.AUGMENT, "남은스핀 2 이하 EXP +10%"),
        Perk("cliff_focus", "🧗", "벼랑끝집중", Tier.GOLD,   PCat.AUGMENT, "EXP가 요구 60% 미만 & 마지막스핀 → 막스핀 EXP +80%"),
        Perk("fate_bell",   "🔔", "운명의종",   Tier.PRISM,  PCat.AUGMENT, "런 1회 부족 15 이하 실패직전 자동 추가스핀 +1"),
        // 세트콤보 (계산학)
        Perk("pair_match",  "👯", "짝맞추기",   Tier.SILVER, PCat.AUGMENT, "같은심볼 2세트면 세트 보너스 +20%"),
        Perk("puzzle_sense","🧩", "퍼즐감각",   Tier.GOLD,   PCat.AUGMENT, "세트3+ EXP +25%·세트4+ 점수 +20%"),
        Perk("perfect_shape","💠", "완벽한모양", Tier.PRISM,  PCat.AUGMENT, "양끝 같고 가운데 같은계열 EXP +120% (와일드충족 70%)"),
        // 해골저주 (저주학)
        Perk("skull_watch", "👁️", "해골관찰",   Tier.SILVER, PCat.AUGMENT, "☠1개당 EXP +2·☠3+ 스핀 점수 -10%"),
        Perk("sacrifice",   "🩸", "희생",       Tier.GOLD,   PCat.AUGMENT, "저주1개당 EXP +6%·클리어코인 -1"),
        Perk("black_diploma","🎓", "검은졸업장", Tier.PRISM,  PCat.AUGMENT, "저주5+ EXP +60%·점수 +30%·스핀 -1"),
    )

    val RELICS = listOf(
        Perk("old_book",    "📘", "낡은교과서", Tier.SILVER, PCat.RELIC, "📘책 EXP +3", 12),
        Perk("cherry_candy","🍬", "체리사탕",   Tier.SILVER, PCat.RELIC, "🍒체리 EXP +2", 10),
        Perk("rusty_coin",  "🪙", "녹슨동전",   Tier.SILVER, PCat.RELIC, "코인 +20%", 12),
        Perk("pencil",      "✏️", "연필깎이",   Tier.SILVER, PCat.RELIC, "첫 스핀 EXP +15%", 12),
        Perk("coffee",      "☕", "커피잔",     Tier.SILVER, PCat.RELIC, "마지막 스핀 EXP +15%", 14),
        Perk("magnifier",   "🔎", "돋보기",     Tier.SILVER, PCat.RELIC, "희귀심볼 등장 +15%", 16),
        Perk("star_sticker","⭐", "별스티커",   Tier.SILVER, PCat.RELIC, "⭐별 점수 +8", 12),
        Perk("black_candle","🕯️", "검은촛불",   Tier.GOLD,   PCat.RELIC, "☠해골이 EXP +4", 18),
        Perk("gem_cert",    "📜", "보석감정서", Tier.GOLD,   PCat.RELIC, "💎보석 점수 +15", 20),
        Perk("clover",      "🍀", "네잎클로버", Tier.GOLD,   PCat.RELIC, "모든 EXP +8%", 16),
        Perk("set_charm",   "🎰", "세트부적",   Tier.GOLD,   PCat.RELIC, "세트 보너스 +25%", 18),
        Perk("wide_lens",   "🔭", "집중경",     Tier.GOLD,   PCat.RELIC, "가운데 칸 EXP +50%", 16),
        // ── 물량 확장 (2026-06-24) ── 실버
        Perk("eraser",      "✏️", "지우개",     Tier.SILVER, PCat.RELIC, "📘책 EXP +2", 10),
        Perk("ruler",       "📏", "자",         Tier.SILVER, PCat.RELIC, "첫 스핀 EXP +12%", 12),
        Perk("desk_lamp",   "🪔", "스탠드",     Tier.SILVER, PCat.RELIC, "마지막 스핀 EXP +12%", 12),
        Perk("cherry_jam",  "🍓", "체리잼",     Tier.SILVER, PCat.RELIC, "🍒체리 EXP +3", 12),
        Perk("bookmark",    "🔖", "책갈피",     Tier.SILVER, PCat.RELIC, "학습태그(📘) 1개당 EXP +3", 12),
        Perk("coin_pouch",  "👛", "동전지갑",   Tier.SILVER, PCat.RELIC, "코인 +20%", 12),
        Perk("mini_scope",  "🔬", "미니스코프", Tier.SILVER, PCat.RELIC, "희귀심볼 등장 +15%", 14),
        Perk("gem_dust",    "✨", "보석가루",   Tier.SILVER, PCat.RELIC, "💎보석 점수 +10", 12),
        Perk("magnet_chip", "🧲", "자석칩",     Tier.SILVER, PCat.RELIC, "🧲자석 EXP +2", 10),
        Perk("star_chart",  "🌠", "별자리표",   Tier.SILVER, PCat.RELIC, "⭐별 EXP +2", 12),
        Perk("paperclip",   "📎", "클립",       Tier.SILVER, PCat.RELIC, "세트 보너스 +15%", 12),
        Perk("small_candle","🕯️", "작은초",     Tier.SILVER, PCat.RELIC, "☠해골이 EXP +3", 12),
        // 골드
        Perk("thick_tome",  "📕", "두꺼운책",   Tier.GOLD, PCat.RELIC, "📘책 EXP +4", 18),
        Perk("crystal_ball","🔮", "수정구",     Tier.GOLD, PCat.RELIC, "희귀심볼 등장 +30%", 20),
        Perk("skull_idol",  "💀", "해골우상",   Tier.GOLD, PCat.RELIC, "☠해골이 EXP +6", 18),
        Perk("gem_tiara",   "💎", "보석티아라", Tier.GOLD, PCat.RELIC, "💎보석 점수 +20", 22),
        Perk("focus_ring",  "💍", "집중반지",   Tier.GOLD, PCat.RELIC, "가운데 칸 EXP +60%", 18),
        Perk("silver_mirror","🪞", "은거울",    Tier.GOLD, PCat.RELIC, "양끝 같은 심볼 EXP +70%", 18),
        Perk("iron_chain",  "⛓️", "쇠사슬",     Tier.GOLD, PCat.RELIC, "붙은 같은 심볼 쌍당 EXP +14", 18),
        Perk("diploma_relic","🎓", "졸업장식",  Tier.GOLD, PCat.RELIC, "학습태그(📘) 1개당 EXP +5", 18),
        Perk("four_clover", "🍀", "네잎클로버2", Tier.GOLD, PCat.RELIC, "모든 EXP +10%", 20),
        Perk("combo_trophy","🏆", "콤보트로피", Tier.GOLD, PCat.RELIC, "세트 보너스 +25%", 20),
        Perk("crown_jewel", "👑", "왕관보석",   Tier.GOLD, PCat.RELIC, "👑왕관 점수 +30", 22),
        Perk("piggy_bank",  "🐷", "돼지저금통", Tier.GOLD, PCat.RELIC, "코인 +40%·클리어코인 +2", 18),
        Perk("spare_token", "🎟️", "여분토큰",   Tier.GOLD, PCat.RELIC, "스테이지 스핀 +1", 30),
        Perk("hourglass_r", "⏳", "모래시계",   Tier.GOLD, PCat.RELIC, "첫·마지막 스핀 EXP +20%", 22),
        Perk("battery",     "🔋", "배터리",     Tier.GOLD, PCat.RELIC, "스핀마다 EXP +6", 18),
        Perk("charm_relic", "🧿", "부적",       Tier.GOLD, PCat.RELIC, "모든 EXP +12%", 20),
        // ── 세트 컴포넌트 유물 (2026-06-24) ──
        Perk("cherry_press","🧃","체리 압축기",Tier.SILVER,PCat.RELIC,"🍒체리 EXP +2",10),
        Perk("cherry_can","🥫","체리 통조림",Tier.SILVER,PCat.RELIC,"🍒체리 EXP +3",12),
        Perk("auto_pen","🖋️","자동 필기 펜",Tier.SILVER,PCat.RELIC,"📘책 EXP +2",10),
        Perk("library_card","🪪","도서관 카드",Tier.GOLD,PCat.RELIC,"📘책 EXP +3·학습태그 1개당 EXP +3",18),
        Perk("greed_goblet","🏆","탐욕의 잔",Tier.GOLD,PCat.RELIC,"모든 EXP +10%",18),
        Perk("ominous_skull","💀","불길한 해골 목걸이",Tier.GOLD,PCat.RELIC,"☠해골이 EXP +5",18),
        Perk("black_report","📋","검은 성적표",Tier.GOLD,PCat.RELIC,"☠해골이 EXP +4",18),
        Perk("bloody_coupon","🩸","피 묻은 쿠폰북",Tier.GOLD,PCat.RELIC,"☠해골이 EXP +4·코인 +20%",18),
        Perk("crown_stand","🏛️","왕관 받침대",Tier.GOLD,PCat.RELIC,"👑왕관 점수 +25",20),
        Perk("broken_crown","👑","깨진 왕관",Tier.SILVER,PCat.RELIC,"👑왕관 점수 +15",16),
        Perk("kings_ledger","📜","왕의 족보",Tier.GOLD,PCat.RELIC,"👑왕관 점수 +20·등장↑",22),
        Perk("flame_canister","🛢️","불꽃 저장통",Tier.GOLD,PCat.RELIC,"모든 EXP +8%",16),
        Perk("hot_handle","🔥","뜨거운 슬롯핸들",Tier.GOLD,PCat.RELIC,"모든 EXP +9%",18),
        Perk("fate_handle","🎰","운명의 손잡이",Tier.GOLD,PCat.RELIC,"희귀심볼 등장 +25%",18),
        Perk("gamblers_eye","👁️","도박사의 눈",Tier.GOLD,PCat.RELIC,"희귀심볼 등장 +20%",18),
        Perk("old_wallet","👛","낡은 지갑",Tier.SILVER,PCat.RELIC,"코인 +20%",12),
        Perk("crumpled_coupon","🧾","구겨진 쿠폰",Tier.SILVER,PCat.RELIC,"코인 +20%",10),
        Perk("cursed_wallet","💰","저주받은 지갑",Tier.GOLD,PCat.RELIC,"코인 +30%·☠해골 EXP +2",18),
        Perk("practice_pad","📓","연습장",Tier.SILVER,PCat.RELIC,"📘책 EXP +2",10),
        Perk("calculator","🧮","작은 계산기",Tier.SILVER,PCat.RELIC,"💎보석 점수 +12",12),
        Perk("lucky_eraser","🩹","행운의 지우개",Tier.SILVER,PCat.RELIC,"희귀심볼 등장 +15%",14),
    )

    // ── 저주 (단점+장점 동시, CURSE 노드서 획득) ──────────────
    val CURSES = listOf(
        Perk("hard_exam",       "📝", "어려운시험", Tier.GOLD, PCat.CURSE, "요구치+10% / 클리어점수+20%"),
        Perk("cursed_skulls",   "☠", "저주받은패", Tier.GOLD, PCat.CURSE, "해골↑·EXP-4 / 해골 EXP+8"),
        Perk("speed_test",      "⏱️", "속성평가",   Tier.GOLD, PCat.CURSE, "스핀-1 / 요구치-22%"),
        Perk("frugal_vow",      "🪙", "청빈서약",   Tier.GOLD, PCat.CURSE, "코인-40% / 요구치-12%"),
        Perk("tunnel_vision",   "🎯", "외골수",     Tier.GOLD, PCat.CURSE, "양끝·첫스핀↓ / 가운데 2배"),
        Perk("late_bloomer",    "🌙", "늦깎이",     Tier.GOLD, PCat.CURSE, "첫스핀-50% / 막스핀+80%"),
        Perk("gem_obsession",   "💎", "보석집착",   Tier.GOLD, PCat.CURSE, "체리·책↓ / 보석 점수+35"),
        Perk("high_stakes",     "🎲", "한탕주의",   Tier.GOLD, PCat.CURSE, "요구치+8% / 희귀등장+50%"),
        Perk("thorny_path",     "🌵", "가시밭길",   Tier.GOLD, PCat.CURSE, "해골↑·EXP↓ / 클리어 코인+"),
        Perk("hex_allornothing","⚡", "일발역전", Tier.GOLD, PCat.CURSE, "세트-50% / 양끝맞춤 2배"),
        Perk("sleep_debt",      "😴", "수면부족",   Tier.GOLD, PCat.CURSE, "스핀당 EXP-5 / 세트+40%"),
        Perk("diploma_pressure","🎓", "학위압박",   Tier.GOLD, PCat.CURSE, "요구치+12% / 학습·책 강화"),
        // ── 물량 확장 (2026-06-24) ──
        Perk("exam_week",       "📅", "시험기간",   Tier.GOLD, PCat.CURSE, "요구치+12% / 클리어점수+25%"),
        Perk("blackout",        "🌑", "정전",       Tier.GOLD, PCat.CURSE, "해골↑·해골 EXP+6 / 희귀등장+30%"),
        Perk("pop_quiz",        "❓", "쪽지시험",   Tier.GOLD, PCat.CURSE, "스핀-1 / 희귀등장+40%"),
        Perk("student_debt",    "💸", "학자금",     Tier.GOLD, PCat.CURSE, "코인-50% / 스핀마다 EXP+6"),
    )

    // ── 세트 효과 (특정 perk 조합 보유 시 발동) ───────────────
    data class SetEffect(
        val id: String, val name: String, val requires: List<String>, val desc: String,
        val reqChar: String = "", val reqMachine: String = "", val reqDevice: String = "",
    )
    val SETS = listOf(
        SetEffect("set_orchard",   "체리 과수원",   listOf("cherry_up", "cherry_farm"), "🍒체리 EXP+3·등장↑"),
        SetEffect("set_library",   "도서관 회원증", listOf("book_up", "library", "study_tag"), "📘책 EXP+3·학습+3"),
        SetEffect("set_necro",     "강령술",        listOf("skull_study", "black_candle"), "☠해골 EXP+4"),
        SetEffect("set_appraiser", "감정사",        listOf("gem_polish", "gem_invest", "gem_cert"), "💎보석 점수+20"),
        SetEffect("set_royal",     "왕실 알현",     listOf("crown_seek", "jackpot"), "👑왕관 점수+40·등장↑"),
        SetEffect("set_align",     "정렬의 묘",     listOf("center", "twins", "chain"), "인접쌍 EXP+10"),
        SetEffect("set_combo",     "콤보 마스터",   listOf("set_sense", "set_charm"), "세트 보너스+20%"),
        SetEffect("set_diurnal",   "주야겸행",      listOf("morning", "evening"), "첫·막 스핀 EXP+15%"),
        SetEffect("set_necro2",    "사령술 비전",   listOf("necromancer", "skull_idol"), "☠해골 EXP+5"),
        SetEffect("set_jewels",    "보석 왕가",     listOf("gem_buff", "lapidary", "gem_tiara"), "💎보석 점수+20"),
        SetEffect("set_combo2",    "콤보 장인",     listOf("combo_note", "combo_trophy"), "세트 보너스+20%"),
        SetEffect("set_royal2",    "대관식",        listOf("royal_decree", "crown_jewel"), "👑왕관 점수+30·등장↑"),
        // ── 세트 확장 (조건부, 2026-06-24) ──
        SetEffect("set_cherry_net","체리 안전망",listOf("cherry_up","cherry_jam"),"🍒체리 EXP+2·점수+12",reqChar="farmer"),
        SetEffect("set_red_harvest","붉은 수확",listOf("cherry_farm","great_harvest"),"🍒체리 EXP+3·등장↑",reqMachine="cherry"),
        SetEffect("set_student","모범생",listOf("study","diligence","note_take"),"스핀마다 EXP+4"),
        SetEffect("set_lib_bless","도서관의 축복",listOf("book_up","library","thick_tome"),"📘책 EXP+4·학습+3",reqMachine="library"),
        SetEffect("set_greed","탐욕",listOf("greed","rich_richer"),"모든 점수+12%·코인+10%"),
        SetEffect("set_glory_grad","빛나는 졸업식",listOf("diploma_relic","honor_student"),"학습태그당 EXP+4·막스핀+15%",reqChar="honor"),
        SetEffect("set_skull_lab","해골 연구",listOf("skull_study","skull_idol"),"☠해골 EXP+6",reqChar="cultist"),
        SetEffect("set_black_grad","검은 졸업",listOf("necromancer","black_candle","skull_idol"),"☠해골 EXP+5·점수+12%",reqMachine="skull"),
        SetEffect("set_curse_cycle","저주 순환",listOf("set_charm"),"세트 보너스+30%",reqDevice="dev_seal"),
        SetEffect("set_crown_rite","왕관 의식",listOf("crown_seek","crown_jewel"),"👑왕관 점수+40·등장↑",reqChar="crowncol"),
        SetEffect("set_kings_order","왕의 명령",listOf("royal_decree","jackpot"),"👑왕관 점수+50·등장↑",reqMachine="crown"),
        SetEffect("set_flame_lab","불꽃 실험",listOf("all_or_nothing"),"🔥불꽃 EXP+5·점수+12%",reqMachine="flame",reqDevice="dev_flame"),
        SetEffect("set_last_ignite","마지막 점화",listOf("review","endgame_rush"),"막 스핀 EXP+25%·점수+10%"),
        SetEffect("set_mechanic","정비공",listOf("set_sense"),"세트 보너스+25%",reqDevice="dev_subreel"),
        SetEffect("set_battery","배터리",listOf("battery","diligence"),"스핀마다 EXP+6"),
        SetEffect("set_gambler","도박사",listOf("high_stakes","high_roller"),"희귀등장↑·💎보석 점수+25",reqChar="gambler"),
        SetEffect("set_shop_reg","상점 단골",listOf("coin_luck","piggy_bank"),"코인+20%·클리어코인+3"),
        SetEffect("set_scholarship","장학금",listOf("study_tag","diploma_relic"),"학습태그당 EXP+4·클리어코인+2",reqChar="scholar"),
        SetEffect("set_bomb_calc","폭탄마",listOf("center","focus_fire"),"가운데 칸 EXP+50%·점수+10%",reqMachine="bomb"),
        SetEffect("set_perfect_calc","완벽한 계산",listOf("center","twins","chain"),"인접쌍 EXP+14·가운데 +30%"),
        SetEffect("set_safe_grad","안전 졸업",listOf("insurance","clover"),"스핀마다 EXP+3·모든 점수+8%"),
    )
    fun activeSets(perkIds: Set<String>, charId: String = "", machineId: String = "", deviceId: String = ""): List<SetEffect> =
        SETS.filter {
            perkIds.containsAll(it.requires) &&
            (it.reqChar.isEmpty()    || it.reqChar    == charId) &&
            (it.reqMachine.isEmpty() || it.reqMachine == machineId) &&
            (it.reqDevice.isEmpty()  || it.reqDevice  == deviceId)
        }

    // ── 세트 시너지 유도 (증강 3택 태그 + 조각 주입) ───────────────
    //  requires 가 전부 perk id 인 일반 세트 위주로 판정(reqChar/reqMachine/reqDevice
    //  조건은 perk 보유만으로 판정 불가 → 무시하고 requires 진행도만 본다. 채팅엔
    //  "시너지" 힌트만 띄우고 실발동은 activeSets 가 별도 조건까지 확정하므로 안전).
    //
    //  setSynergyName: 후보 perkId 를 지금 고르면 어떤 세트가 진행/완성되는지 라벨.
    //  - perkId 가 SetEffect.requires 에 속하고, 그 세트의 *다른* requires 중 1개+ 가
    //    held 에 이미 있으면(=세트 진행) 후보로 본다.
    //  - 후보를 고른 뒤(held + perkId) 남은 미보유 requires==0 이면 "<세트명> 완성",
    //    아니면 "<세트명> 시너지".
    //  - 가장 근접(고른 뒤 남은 미보유 최소) 세트 우선. 없으면 null.
    fun setSynergyName(perkId: String, held: Set<String>): String? {
        if (perkId in held) return null
        var best: SetEffect? = null
        var bestRemain = Int.MAX_VALUE
        for (s in SETS) {
            if (perkId !in s.requires) continue
            // 이 세트의 perkId 를 뺀 나머지 requires 중 이미 보유한 게 1개+ 여야 "진행 중"
            val others = s.requires.filter { it != perkId }
            if (others.none { it in held }) continue
            // 후보를 고른 뒤 남은 미보유 requires 수
            val remain = s.requires.count { it != perkId && it !in held }
            if (remain < bestRemain) {
                bestRemain = remain
                best = s
            }
        }
        val s = best ?: return null
        return if (bestRemain == 0) "${s.name} 완성" else "${s.name} 시너지"
    }

    // setSynergyAug: 플레이어가 짓는 중인(requires 1개+ 보유·미완성) 세트들의
    //  미보유 requires 중 cat==AUGMENT 이고 exclude 에 없는 perk 들에서, 가장 근접한
    //  세트(미보유 requires 최소) 우선으로 1개 randomOrNull. 없으면 null.
    //  - 메인 티어와 다를 수 있음(세트 완성이 목적).
    fun setSynergyAug(held: Set<String>, exclude: Set<String>, rng: Random, cat: PCat = PCat.AUGMENT): Perk? {
        // (남은 미보유 requires 수) 오름차순 = 근접 세트 우선. 후보 perk → 그 세트의 근접도.
        val candidateBySet = SETS
            .filter { s -> s.requires.any { it in held } && !held.containsAll(s.requires) }
            .map { s -> s to s.requires.count { it !in held } }
            .sortedBy { it.second }
        for ((s, _) in candidateBySet) {
            val missingAug = s.requires
                .filter { it !in held && it !in exclude }
                .mapNotNull { perk(it) }
                .filter { it.cat == PCat.AUGMENT }
            val pick = missingAug.randomOrNull(rng)
            if (pick != null) return pick
        }
        return null
    }

    /** 일반 증강/유물 노드 기본 티어 — 클리어 스테이지 결정형: 5의배수=프리즘(우선), 3의배수=골드, 그외 실버. */
    fun tierForClearedStage(stage: Int): Tier = when {
        stage <= 0 -> Tier.SILVER
        stage % 5 == 0 -> Tier.PRISM
        stage % 3 == 0 -> Tier.GOLD
        else -> Tier.SILVER
    }
    /** 한 등급 위(실버→골드→프리즘, 프리즘은 그대로). 운빨 등급업용. */
    fun tierUp(t: Tier): Tier = when (t) { Tier.SILVER -> Tier.GOLD; Tier.GOLD -> Tier.PRISM; Tier.PRISM -> Tier.PRISM }

    val ALL_PERKS: Map<String, Perk> = (AUGMENTS + RELICS + CURSES).associateBy { it.id }
    fun perk(id: String): Perk? = ALL_PERKS[id]
    fun curse(id: String): Perk? = CURSES.firstOrNull { it.id == id }

    // ══════════════════════════════════════════════════════════
    //  콘텐츠 해금 (졸업레벨 + 전공게이트) — 2026-06-29
    //  증강/유물을 처음부터 전부 등장시키지 않고, "후보 풀"을 점진 개방.
    //  해금 = 풀 개방(영구 장착 아님). 졸업레벨/EXP = 기존 누적스탯에서 파생계산(저장 X) →
    //  고인물 소급(레벨 자동 높음 = 기존 콘텐츠 유지). DB 마이그레이션 불필요.
    //
    //  perkGate(p) 로 게이트 산출(130개 per-perk 필드 편집 없음):
    //   ① BASE_PERK_IDS    → 빈 게이트(항상 등장)
    //   ② PERK_GATE_OVERRIDES → 고위험 개별 게이트
    //   ③ 그 외            → 테마(desc 이모지/계열)·티어(프리즘=고게이트)로 school+req 추론
    // ══════════════════════════════════════════════════════════

    /** 해금 게이트 — minLevel(졸업레벨) AND req(전공연구 누적조건). school = 전공 표시·추론용. */
    data class UnlockGate(
        val minLevel: Int = 0,
        val req: List<Pair<String, Long>> = emptyList(),
        val school: String = "",
    )

    /** ── (2) 기본 풀 — 게이트 없음(항상 등장). 초보는 단순 성장형만. ── */
    val BASE_PERK_IDS: Set<String> = setOf(
        // 증강(실버 단순 스탯)
        "study", "preview", "review", "diligence",
        "cherry_up", "book_up", "star_up", "gem_polish", "coin_luck", "set_sense",
        // 유물(기본 — 낡은교과서·체리사탕·녹슨동전·연필깎이·커피잔·지우개류·자·스탠드·체리잼·책갈피·동전지갑·작은계산기)
        "old_book", "cherry_candy", "rusty_coin", "pencil", "coffee",
        "eraser", "ruler", "desk_lamp", "cherry_jam", "bookmark", "coin_pouch", "calculator",
    )

    /** ── (3) 전공(school) 기본 req — 테마로 배정된 perk 가 상속. (스펙 3장) ── */
    private val SCHOOL_REQ: Map<String, UnlockGate> = mapOf(
        "성장학"     to UnlockGate(5,  listOf("cherryTotal" to 200L, "bestStage" to 5L), "성장학"),     // 체리/책/초반성장(OR은 cherry 임계로 근사)
        "계산학"     to UnlockGate(7,  listOf("set4Plus" to 3L, "exactClears" to 1L), "계산학"),         // 세트/가운데/양끝/인접
        "경제학"     to UnlockGate(8,  listOf("coinTotal" to 300L, "shopBuys" to 5L), "경제학"),         // 코인/상점
        "운명학"     to UnlockGate(9,  listOf("prayClears" to 1L, "gambles" to 3L), "운명학"),           // 기도/희귀/주사위
        "왕관학"     to UnlockGate(10, listOf("crownTotal" to 30L, "jackpots" to 1L), "왕관학"),         // 왕관/잭팟/와일드
        "저주학"     to UnlockGate(11, listOf("skullTotal" to 100L, "curseMax" to 1L), "저주학"),        // 해골/저주/고위험
        "시간학"     to UnlockGate(8,  listOf("lastSpinClears" to 3L, "closeClears" to 5L), "시간학"),   // 막스핀/아슬아슬
        "프리즘공학" to UnlockGate(12, listOf("prismPicks" to 3L, "bossClears" to 3L, "bestStage" to 10L), "프리즘공학"), // 시스템변경급
        "씨앗학"     to UnlockGate(12, listOf("mstage_garden" to 8L), "씨앗학"),                          // 씨앗/성장스택(정원 S8)
        "와일드학"   to UnlockGate(13, listOf("set4Plus" to 10L), "와일드학"),                            // 와일드/세트/잭팟
    )

    // ── (3·b) 전공 연구 입문 (ACH-5a) — 연구 업적 달성 시 해당 school 의 실버/골드 증강·유물 풀 개방. ──
    //  추가 해금 경로(perkUnlocked 의 게이트/seen 과 OR). ⚠️프리즘 티어는 제외 — 프리즘은 보스클리어 전용(#6/#7).
    //  값 = (achId 참조용, key=기존 추적 카운터, threshold). schoolResearchDone 은 stat[key]>=threshold 만 본다(업적 행 존재 불요).
    data class ResearchEntry(val achId: String, val key: String, val threshold: Long)
    val SCHOOL_RESEARCH: Map<String, ResearchEntry> = mapOf(
        "성장학"     to ResearchEntry("cherry300",  "cherryTotal",    300L),  // 🍒체리 누적 300 — 체리 과수원
        "계산학"     to ResearchEntry("pc_set4_3",  "set4Plus",         3L),  // 같은심볼 4+ 3회 (hid_set4_1↑·pc_set4_5↓ 사이)
        "경제학"     to ResearchEntry("coin300",    "coinTotal",      300L),  // 🪙코인 누적 300 — 환전상
        "운명학"     to ResearchEntry("gamble3",    "gambles",          3L),  // 도박장 3회 (gamble1↑·ec_gamble5↓ 사이)
        "왕관학"     to ResearchEntry("crown30ext", "crownTotal",      30L),  // 👑왕관 누적 30 — 왕관 보관소
        "저주학"     to ResearchEntry("skull100",   "skullTotal",     100L),  // 💀해골 누적 100 — 해골 수집가
        "시간학"     to ResearchEntry("lastspin3",  "lastSpinClears",   3L),  // 막판 클리어 3회 (lastspin1↑·lastspin5↓ 사이)
        "프리즘공학" to ResearchEntry("prismPick3", "prismPicks",       3L),  // 프리즘 선택 3회 (prismPick1↑·prism5↓ 사이) — ⚠️연구로 PRISM perk 는 안 열림(아래 가드)
        "씨앗학"     to ResearchEntry("sp_seed10",  "seedTotal",       10L),  // 🌱씨앗 누적 10 (sp_seed30 의 입문 컷)
        "와일드학"   to ResearchEntry("sp_wild10",  "wildTotal",       10L),  // 🌀와일드 누적 10 (sp_wild30 의 입문 컷)
    )

    /** 해당 school 의 연구 입문 달성 여부 = SCHOOL_RESEARCH[school] 의 stat[key] >= threshold. (없는 school = false) */
    fun schoolResearchDone(school: String, stat: Map<String, Long>): Boolean {
        val r = SCHOOL_RESEARCH[school] ?: return false
        return (stat[r.key] ?: 0L) >= r.threshold
    }

    /** ── (4) 고위험 개별 게이트 (스펙 4장 — req 더 빡세게). 실제 perk id 매핑. ── */
    val PERK_GATE_OVERRIDES: Map<String, UnlockGate> = mapOf(
        // 증강
        "overdrive"     to UnlockGate(12, listOf("prismPicks" to 5L, "bossClears" to 3L), "프리즘공학"),                 // 과부하
        "short_day"     to UnlockGate(15, listOf("bestStage" to 15L, "exactClears" to 3L), "프리즘공학"),                // 조기퇴근
        "glass_cannon"  to UnlockGate(15, listOf("bestScore" to 30_000L, "bestStage" to 10L), "프리즘공학"),            // 유리대포
        "supernova"     to UnlockGate(17, listOf("bestScore" to 50_000L, "bossClears" to 8L), "프리즘공학"),            // 초신성
        "endgame_rush"  to UnlockGate(14, listOf("lastSpinClears" to 10L, "closeClears" to 10L), "시간학"),             // 막판스퍼트
        "wild_world"    to UnlockGate(13, listOf("set4Plus" to 10L), "와일드학"),                                       // 와일드세계
        "joker"         to UnlockGate(16, listOf("jackpots" to 5L, "set4Plus" to 20L), "와일드학"),                     // 조커
        "jackpot"       to UnlockGate(13, listOf("crownTotal" to 100L, "jackpots" to 3L), "왕관학"),                    // 잭팟기계
        "mega_jackpot"  to UnlockGate(16, listOf("crownTotal" to 300L, "jackpots" to 10L), "왕관학"),                   // 대박기계
        "seed_garden"   to UnlockGate(12, listOf("mstage_garden" to 8L), "씨앗학"),                                     // 씨앗정원(정원 S8)
        "great_harvest" to UnlockGate(15, listOf("mstage_garden" to 8L), "씨앗학"),                                     // 대수확(씨앗빌드)
        "key_master"    to UnlockGate(12, listOf("coinTotal" to 500L, "shopBuys" to 10L), "경제학"),                    // 열쇠장인
        "gamblers_dice" to UnlockGate(11, listOf("allinWins" to 5L, "gambles" to 10L), "운명학"),                       // 도박주사위
        // 유물
        "crystal_ball"  to UnlockGate(9,  listOf("prayClears" to 1L), "운명학"),                                        // 수정구
        "fate_handle"   to UnlockGate(11, listOf("prayClears" to 3L), "운명학"),                                        // 운명의 손잡이
        "gamblers_eye"  to UnlockGate(11, listOf("allinWins" to 5L), "운명학"),                                         // 도박사의 눈
        "piggy_bank"    to UnlockGate(8,  listOf("coinTotal" to 300L, "shopBuys" to 5L), "경제학"),                     // 돼지저금통
        "hourglass_r"   to UnlockGate(10, listOf("lastSpinClears" to 5L), "시간학"),                                    // 모래시계
        "skull_idol"    to UnlockGate(11, listOf("skullTotal" to 300L), "저주학"),                                      // 해골우상
        "ominous_skull" to UnlockGate(13, listOf("curseMax" to 3L), "저주학"),                                          // 불길한 해골 목걸이
        "black_report"  to UnlockGate(13, listOf("curseBossClears" to 1L), "저주학"),                                   // 검은 성적표(저주3 보스)
        "crown_jewel"   to UnlockGate(10, listOf("crownTotal" to 50L), "왕관학"),                                       // 왕관보석
        "crown_stand"   to UnlockGate(11, listOf("crownTotal" to 100L), "왕관학"),                                      // 왕관 받침대
        "kings_ledger"  to UnlockGate(13, listOf("jackpots" to 3L), "왕관학"),                                          // 왕의 족보
        "focus_ring"    to UnlockGate(8,  listOf("exactClears" to 1L), "계산학"),                                       // 집중반지
        "silver_mirror" to UnlockGate(9,  listOf("set4Plus" to 5L), "계산학"),                                          // 은거울
        "greed_goblet"  to UnlockGate(12, listOf("bestScore" to 20_000L), "성장학"),                                    // 탐욕의 잔
        "flame_canister" to UnlockGate(13, listOf("mstage_flame" to 8L), "저주학"),                                     // 불꽃 저장통(불꽃 S8)
        "cursed_wallet" to UnlockGate(13, listOf("coinTotal" to 500L, "curseMax" to 3L), "저주학"),                     // 저주받은 지갑
        // ── 신규 16종 게이트 (테마→전공, 🌈프리즘=고·🥇골드=중·🥈실버=낮음). 처음부터 안 뜨고 해금분만. ──
        // 초반성장 = 성장학
        "early_prep"    to UnlockGate(3,  listOf("cherryTotal" to 100L), "성장학"),                                     // 🥈 조기교육
        "growth_log"    to UnlockGate(5,  listOf("cherryTotal" to 120L), "성장학"),                                    // 🥈 성장일지(초반성장·early_prep 동급)
        "early_adapt"   to UnlockGate(6,  listOf("cherryTotal" to 200L, "bestStage" to 5L), "성장학"),                  // 🥇 빠른적응
        "snowball"      to UnlockGate(12, listOf("cherryTotal" to 400L, "bestStage" to 10L), "성장학"),                 // 🌈 눈덩이
        // 운빨 = 운명학
        "fortune_check" to UnlockGate(7,  listOf("prayClears" to 1L), "운명학"),                                        // 🥈 운세확인
        "luck_accum"    to UnlockGate(9,  listOf("prayClears" to 1L, "gambles" to 3L), "운명학"),                       // 🥇 불운적립
        "fate_burst"    to UnlockGate(13, listOf("prayClears" to 3L, "jackpots" to 3L), "운명학"),                      // 🌈 운명폭발
        // 막판역전 = 시간학
        "late_focus"    to UnlockGate(6,  listOf("lastSpinClears" to 3L), "시간학"),                                    // 🥈 후반집중
        "cliff_focus"   to UnlockGate(8,  listOf("lastSpinClears" to 3L, "closeClears" to 5L), "시간학"),               // 🥇 벼랑끝집중
        "fate_bell"     to UnlockGate(14, listOf("closeClears" to 10L, "bossClears" to 5L), "시간학"),                  // 🌈 운명의종
        // 세트콤보 = 계산학
        "pair_match"    to UnlockGate(5,  listOf("set4Plus" to 3L), "계산학"),                                          // 🥈 짝맞추기
        "puzzle_sense"  to UnlockGate(7,  listOf("set4Plus" to 3L, "exactClears" to 1L), "계산학"),                     // 🥇 퍼즐감각
        "perfect_shape" to UnlockGate(13, listOf("set4Plus" to 10L, "exactClears" to 3L), "계산학"),                    // 🌈 완벽한모양
        // 해골저주 = 저주학
        "skull_watch"   to UnlockGate(9,  listOf("skullTotal" to 100L), "저주학"),                                      // 🥈 해골관찰
        "sacrifice"     to UnlockGate(11, listOf("skullTotal" to 200L, "curseMax" to 3L), "저주학"),                    // 🥇 희생
        "black_diploma" to UnlockGate(14, listOf("skullTotal" to 300L, "curseBossClears" to 1L), "저주학"),             // 🌈 검은졸업장
    )

    /** desc 이모지/계열 → 전공(school) 추론. (스펙 3장 테마 배정) */
    private fun inferSchool(p: Perk): String {
        val d = p.desc
        return when {
            d.contains("☠") || d.contains("해골") || d.contains("저주") -> "저주학"
            d.contains("👑") || d.contains("왕관") || d.contains("🌀") || d.contains("와일드") || d.contains("잭팟") -> "왕관학"
            d.contains("🌱") || d.contains("씨앗") -> "씨앗학"
            d.contains("🎲") || d.contains("주사위") || d.contains("희귀") || d.contains("올인") || d.contains("기도") -> "운명학"
            d.contains("코인") || d.contains("🪙") || d.contains("상점") || d.contains("🗝") || d.contains("열쇠") -> "경제학"
            d.contains("막") || d.contains("첫스핀") || d.contains("첫 스핀") || d.contains("마지막 스핀") || d.contains("막스핀") -> "시간학"
            d.contains("세트") || d.contains("가운데") || d.contains("양끝") || d.contains("인접") || d.contains("붙은") || d.contains("콤보") -> "계산학"
            else -> "성장학"   // 체리/책/별/보석/EXP 단순성장 — 성장학 기본
        }
    }

    /** ── (4·끝) perkGate — perk 1개의 해금 게이트. BASE → 빈 / OVERRIDE → 개별 / 그 외 → 테마+티어 추론. ── */
    fun perkGate(p: Perk): UnlockGate {
        if (p.id in BASE_PERK_IDS) return UnlockGate()
        PERK_GATE_OVERRIDES[p.id]?.let { return it }
        // 테마 추론 — school 기본 req 상속 + 프리즘 티어면 minLevel 상향(고게이트).
        val school = inferSchool(p)
        val base = SCHOOL_REQ[school] ?: UnlockGate()
        return when (p.tier) {
            Tier.PRISM -> base.copy(minLevel = (base.minLevel + 4).coerceAtLeast(12))  // 프리즘 = 시스템급 → 고게이트
            Tier.GOLD  -> base                                                          // 골드 = school 기본
            Tier.SILVER -> base.copy(minLevel = (base.minLevel - 2).coerceAtLeast(2))  // 중급 실버 = 조금 낮게
        }
    }
    fun perkGate(perkId: String): UnlockGate = perk(perkId)?.let { perkGate(it) } ?: UnlockGate()

    // ── (5) 졸업레벨 (파생 — 저장 X) ───────────────────────────
    //  accountExp = 다음 마일스톤 합(현재 스탯에서 1회성 최초달성형으로 계산).
    /** 졸업 EXP — bestStage/보스/런/업적tier/빌드도감/숙련메달 마일스톤 합. (스펙 1장) */
    fun accountExp(stat: Map<String, Long>): Long {
        var exp = 0L
        // ① 최고도달 스테이지 마일스톤
        val bs = stat["bestStage"] ?: 0L
        if (bs >= 3) exp += 10; if (bs >= 5) exp += 30; if (bs >= 10) exp += 80; if (bs >= 15) exp += 150
        // ② 보스 클리어 (1회당 +8, 상한 +120)
        exp += ((stat["bossClears"] ?: 0L) * 8L).coerceAtMost(120L)
        // ③ 런 (1런당 +3, 상한 +90)
        exp += ((stat["runs"] ?: 0L) * 3L).coerceAtMost(90L)
        // ④ 업적 tier별 합 — 달성(stat[key]>=threshold) 업적의 tier 점수. (myAch unlocked 셋 × tier ≡ stat 파생)
        for (a in ACHIEVEMENTS) {
            if ((stat[a.key] ?: 0L) >= a.threshold) exp += achTierExp(a.tier)
        }
        // ⑤ 빌드도감(bld_*/bc_*) 완성 1개당 +40
        exp += stat.count { (k, v) -> v > 0L && (k.startsWith("bc_") || k.startsWith("bld_")) } * 40L
        // ⑥ 숙련 메달 — 캐릭/머신 동/은/금 (cstage_*/mstage_* 최고스테이지 기준)
        for ((k, v) in stat) {
            if (k.startsWith("cstage_") || k.startsWith("mstage_")) exp += medalExp(medalFor(v))
        }
        return exp
    }
    private fun achTierExp(tier: String): Long = when (tier) {
        "프리즘" -> 250L; "골드" -> 120L; "실버" -> 50L; else -> 20L   // 브론즈 기본
    }
    private fun medalExp(m: Medal): Long = when (m) {
        Medal.GOLD -> 100L; Medal.SILVER -> 50L; Medal.BRONZE -> 20L; Medal.NONE -> 0L
    }

    /** 졸업레벨 — accountExp 누적곡선(Lv1~25). 고인물(bestStage15+골드업적 다수)=Lv15+ 되게 튜닝.
     *  level = 1 + floor(sqrt(exp / 22)). exp 0→Lv1, ~88→Lv3, ~352→Lv5, ~2000→Lv10, ~4500→Lv15, ~12500→Lv25(캡). */
    fun accountLevel(stat: Map<String, Long>): Int = expToLevel(accountExp(stat))
    fun expToLevel(exp: Long): Int =
        (1 + Math.floor(Math.sqrt(exp.coerceAtLeast(0L).toDouble() / 22.0)).toInt()).coerceIn(1, 25)
    /** 다음 레벨까지 필요한 누적 exp (진행도 표시용). Lv25 = 캡(0). */
    fun expForLevel(level: Int): Long {
        if (level <= 1) return 0L
        val l = (level - 1).toDouble()
        return Math.ceil(l * l * 22.0).toLong()
    }

    // ── (6) 해금 판정 ─────────────────────────────────────────
    /** perk 해금 여부 = 졸업레벨 ≥ minLevel AND req 전부충족. (전공연구 = req 조건 자체) */
    fun perkUnlocked(p: Perk, stat: Map<String, Long>): Boolean {
        // seen grandfather: 과거 등장/사용 경험(seen_<id> 카운터>0)이 있으면 게이트 무관 영구 해금 (고인물 콘텐츠 보존)
        if ((stat["seen_" + p.id] ?: 0L) > 0L) return true
        val g = perkGate(p)
        // ACH-5a: 전공 연구 입문 달성 시 그 school 의 실버/골드 증강·유물 추가 개방. ⚠️프리즘은 보스클리어 전용이라 제외.
        if (p.tier != Tier.PRISM && schoolResearchDone(g.school, stat)) return true
        if (accountLevel(stat) < g.minLevel) return false
        return meetsReq(g.req, stat)
    }
    fun perkUnlocked(perkId: String, stat: Map<String, Long>): Boolean =
        perk(perkId)?.let { perkUnlocked(it, stat) } ?: false

    /** 미해금 perk 의 진행도 힌트 — "🌈프리즘공학 Lv12/Lv9 · 프리즘선택(5/3) & 보스클리어(2/3)". */
    fun perkUnlockHint(p: Perk, stat: Map<String, Long> = emptyMap()): String {
        val g = perkGate(p)
        if (g.minLevel == 0 && g.req.isEmpty()) return "기본 (항상 등장)"
        val lv = accountLevel(stat)
        val sb = StringBuilder()
        if (g.school.isNotBlank()) sb.append("🎓").append(g.school).append(" ")
        if (g.minLevel > 0) sb.append("Lv${lv}/Lv${g.minLevel}").append(if (lv >= g.minLevel) "✓" else "")
        if (g.req.isNotEmpty()) { if (sb.isNotEmpty()) sb.append(" · "); sb.append(reqHint(g.req, stat)) }
        return sb.toString().ifBlank { "무료" }
    }
    fun perkUnlockHint(perkId: String, stat: Map<String, Long> = emptyMap()): String =
        perk(perkId)?.let { perkUnlockHint(it, stat) } ?: ""

    /** unlocked 만 후보로 거른 perk 풀(pickPerksByTier/유물노드/상점 공용). */
    fun unlockedPerks(pool: List<Perk>, stat: Map<String, Long>): List<Perk> =
        pool.filter { perkUnlocked(it, stat) }
    fun lockedPerks(pool: List<Perk>, stat: Map<String, Long>): List<Perk> =
        pool.filter { !perkUnlocked(it, stat) }

    // ── 아이템 (1회용, 코인 구매 — v2는 EXP가 스테이지 게이지라 비용=코인) ──
    enum class IKind { NEXTSPIN, PHASE, INSTANT }
    data class Item(val id: String, val emoji: String, val name: String, val kind: IKind, val coinCost: Int, val desc: String)
    val ITEMS = listOf(
        // NEXTSPIN — 다음 1스핀만
        Item("energy_drink",  "🥤", "에너지드링크", IKind.NEXTSPIN, 18, "다음 스핀 EXP 2배"),
        Item("magnify",       "🔎", "확대경",       IKind.NEXTSPIN, 15, "다음 스핀 희귀심볼 4배"),
        Item("loaded_dice",   "🎲", "조작주사위",   IKind.NEXTSPIN, 22, "다음 스핀 👑왕관 주입·점수 2배"),
        Item("ward_charm",    "🧿", "액막이부적",   IKind.NEXTSPIN, 10, "다음 스핀 ☠해골 미등장"),
        // PHASE — 이번 스테이지 내내
        Item("espresso",      "☕", "에스프레소",   IKind.PHASE, 20, "이번 스테이지 스핀마다 EXP +15"),
        Item("study_streak",  "✍️", "집중모드",     IKind.PHASE, 12, "이번 스테이지 스핀마다 EXP +6"),
        Item("rare_lure",     "🍀", "행운미끼",     IKind.PHASE, 16, "이번 스테이지 희귀심볼 2배"),
        Item("coin_magnet",   "🧲", "코인자석",     IKind.PHASE, 14, "이번 스테이지 코인 2배·클리어코인+8"),
        Item("dbl_nothing",   "🎰", "올인학습",     IKind.PHASE, 12, "이번 스테이지 스핀마다 EXP+30·요구치+20%"),
        Item("last_minute",   "⏰", "막판스퍼트",   IKind.PHASE, 18, "이번 스테이지 마지막 스핀 EXP 2배"),
        // INSTANT — 즉발
        Item("first_aid",     "🩹", "응급처치",     IKind.INSTANT, 30, "이번 스테이지 스핀 +1"),
        Item("cram",          "📚", "벼락치기",     IKind.INSTANT, 12, "즉시 게이지 +요구치 15%"),
        Item("answer_sheet",  "📝", "족보",         IKind.INSTANT, 40, "즉시 게이지 +요구치 50%"),
        Item("grad_cert",     "🎓", "졸업장",       IKind.INSTANT, 100, "즉시 게이지 +요구치 100% (돌파)"),
        // ── 물량 확장 (2026-06-24) ── NEXTSPIN
        Item("adrenaline",    "💉", "아드레날린",   IKind.NEXTSPIN, 30, "다음 스핀 EXP 3배"),
        Item("rare_scope",    "🔭", "정밀스코프",   IKind.NEXTSPIN, 18, "다음 스핀 희귀심볼 3배"),
        Item("crown_inject",  "👑", "왕관주입",     IKind.NEXTSPIN, 24, "다음 스핀 👑왕관 대량 주입"),
        Item("wild_inject",   "🌀", "와일드주입",   IKind.NEXTSPIN, 22, "다음 스핀 🌀와일드 주입"),
        // PHASE
        Item("tutor",         "👨‍🏫", "과외",        IKind.PHASE, 18, "이번 스테이지 스핀마다 EXP +10"),
        Item("fortune_incense","🍀", "행운향",       IKind.PHASE, 16, "이번 스테이지 희귀심볼 1.6배"),
        Item("coin_press",    "🪙", "주화압인",     IKind.PHASE, 16, "이번 스테이지 코인 3배"),
        Item("overtime",      "⏰", "야근",         IKind.PHASE, 16, "이번 스테이지 마지막 스핀 EXP 2배"),
        // INSTANT
        Item("double_aid",    "🚑", "특급처치",     IKind.INSTANT, 55, "이번 스테이지 스핀 +2"),
        Item("cheat_sheet",   "📋", "커닝페이퍼",   IKind.INSTANT, 20, "즉시 게이지 +요구치 30%"),
        Item("honor_roll",    "🏅", "우등생증",     IKind.INSTANT, 60, "즉시 게이지 +요구치 70%"),
        // ── 추가 (빌드별·조건부, 2026-06-24) ──
        Item("cherry_juice",  "🧃", "체리주스",     IKind.NEXTSPIN, 5,  "다음 스핀 🍒체리 확률 ↑"),
        Item("bookmark2",     "🔖", "책갈피",       IKind.NEXTSPIN, 5,  "다음 스핀 📘책 확률 ↑"),
        Item("sparkle_dust",  "✨", "반짝이가루",   IKind.NEXTSPIN, 6,  "다음 스핀 💎보석 확률 ↑"),
        Item("gold_chalk",    "🖍️", "황금분필",     IKind.NEXTSPIN, 13, "이번 스핀 EXP ×2"),
        Item("focus_candy",   "🍬", "집중사탕",     IKind.NEXTSPIN, 5,  "다음 스핀 EXP +15%"),
        Item("cram_note",     "📓", "벼락치기노트", IKind.PHASE, 14, "이번 스테이지 마지막 스핀 EXP ×2"),
        Item("rich_lure",     "🍀", "큰행운미끼",   IKind.PHASE, 16, "이번 스테이지 희귀심볼 3배"),
        Item("prof_bribe",    "🧧", "교수매수봉투", IKind.PHASE, 24, "이번 스테이지 요구치 -15%"),
        Item("dev_battery",   "🔋", "배터리부스트", IKind.INSTANT, 8,  "다음 스핀 EXP +30%"),
        Item("score_sticker", "💯", "점수스티커",   IKind.INSTANT, 5,  "사용 즉시 점수 +150"),
        Item("old_coin",      "🪙", "낡은동전",     IKind.INSTANT, 4,  "즉시 코인 +6"),
        // ── 단순레버 확장 (2026-06-24) ──
        Item("small_snack","🍪","작은간식",IKind.NEXTSPIN,4,"다음 스핀 ☠해골 미등장"),
        Item("cherry_basket","🧺","체리바구니",IKind.NEXTSPIN,7,"다음 스핀 🍒체리 대량 등장"),
        Item("gem_loupe","💎","감정확대경",IKind.NEXTSPIN,10,"다음 스핀 💎보석 확률↑·점수 2배"),
        Item("sugar_powder","🍚","설탕가루",IKind.PHASE,12,"이번 스테이지 🍒체리 1.6배·EXP+8"),
        Item("cherry_cracker","🧨","체리폭죽",IKind.PHASE,14,"이번 스테이지 🍒체리 2배·점수+20%"),
        Item("book_copy","📄","족보사본",IKind.PHASE,12,"이번 스테이지 📘책 2배·EXP+8"),
        Item("allnight_note","🌙","밤샘노트",IKind.PHASE,16,"이번 스테이지 📘책 1.8배·EXP+12"),
        Item("summary_note","🗒️","요약노트",IKind.PHASE,13,"이번 스테이지 스핀마다 EXP+9"),
        Item("gem_pouch","👝","보석주머니",IKind.PHASE,16,"이번 스테이지 💎보석 2배·점수+25%"),
        Item("greed_lens","🔍","탐욕의렌즈",IKind.PHASE,18,"이번 스테이지 점수 1.5배"),
        Item("black_candle_i","🕯️","검은양초",IKind.PHASE,14,"이번 스테이지 ☠해골 2배·EXP 1.3배"),
        Item("curse_amp","🩸","저주증폭제",IKind.PHASE,16,"이번 스테이지 ☠해골 1.6배·점수 1.4배"),
        Item("gold_chalk_box","✏️","황금분필세트",IKind.PHASE,20,"이번 스테이지 EXP 1.5배"),
        Item("skull_shield","🛡️","해골방패",IKind.PHASE,14,"이번 스테이지 ☠해골 미등장"),
        Item("combo_mega","📢","콤보확성기",IKind.PHASE,16,"이번 스테이지 마지막 스핀 EXP 2배·점수 1.2배"),
        Item("cram_note_x2","📔","벼락치기노트+",IKind.PHASE,16,"이번 스테이지 마지막 스핀 EXP 2배"),
        Item("overload_potion","🧪","폭주물약",IKind.PHASE,20,"이번 스테이지 EXP 2배·요구치+20%"),
        Item("grad_copy","🎓","졸업장복사본",IKind.INSTANT,70,"즉시 게이지 +요구치 80%·점수 -10%"),
        Item("score_calc","🧮","점수계산기",IKind.INSTANT,22,"즉시 현재 점수 +30%"),
        Item("mini_coupon","🎟️","미니쿠폰",IKind.INSTANT,5,"즉시 코인 +9"),
        Item("price_hack","🏷️","가격표조작기",IKind.INSTANT,12,"즉시 코인 +18"),
        // ── 복잡아이템 (2026-06-24) ──
        Item("seal_tape","🩹","봉인테이프",IKind.NEXTSPIN,9,"다음 스핀 ☠해골 미등장"),
        Item("skull_sticker","💯","해골스티커",IKind.NEXTSPIN,12,"다음 스핀 ☠해골 1개당 점수 +100(무페널티)"),
        Item("eraser_old","🧽","낡은지우개",IKind.NEXTSPIN,8,"다음 스핀 가장 낮은 칸 1개 제거"),
        Item("eraser_fine","🧼","고급지우개",IKind.NEXTSPIN,12,"다음 스핀 가장 낮은 칸 1개 제거(정밀)"),
        Item("eraser_god","✨","신의지우개",IKind.NEXTSPIN,20,"다음 스핀 낮은 칸 최대 2개 제거"),
        Item("wild_temp","🌀","임시와일드",IKind.NEXTSPIN,16,"다음 스핀 랜덤 1칸 → 🌀와일드"),
        Item("fake_crown","👑","가짜왕관",IKind.NEXTSPIN,24,"다음 스핀 가장 높은 칸 → 👑왕관"),
        Item("grad_ring","💍","졸업반지",IKind.INSTANT,50,"부족 EXP ≤20이면 즉시 클리어"),
        Item("gold_grad_bell","🔔","황금졸업벨",IKind.INSTANT,90,"부족 EXP ≤50이면 즉시 클리어"),
        Item("insurance_cert","📋","보험증서",IKind.INSTANT,45,"이번 스테이지 실패 시 1회 생존(스핀+2)"),
        Item("debt_note","🧾","빚문서",IKind.INSTANT,0,"코인 +30 / 이후 4스테이지 클리어보상 0"),
        Item("retake_form","📄","재시험신청서",IKind.INSTANT,28,"직전 스핀 전체 다시 굴림"),
        Item("black_lottery","🎫","검은복권",IKind.INSTANT,18,"50% 골드유물 / 50% 저주 1개"),
        Item("devil_contract","😈","악마의계약서",IKind.INSTANT,20,"유물 1개 + 저주 1개(코인+25)"),
        Item("timeline_ticket","🎟️","세계선티켓",IKind.INSTANT,26,"다음 스핀 2번 굴려 유리한 쪽 자동확정"),
        Item("broken_prism","🔮","깨진프리즘",IKind.INSTANT,22,"이번 스테이지 랜덤 프리즘 증강효과 1개"),
    )
    fun item(id: String): Item? = ITEMS.firstOrNull { it.id == id }
    fun pickItems(rng: Random, n: Int = 3): List<Item> = ITEMS.shuffled(rng).take(n)

    /** (C1) 즉시클리어/대량스킵형 아이템 — 게이지를 요구치의 큰 비율(≥50%) 즉발 채우거나 조건부 즉시클리어.
     *  서비스가 스테이지당 1회(usedCmds "ICLEAR")로 캡. answer_sheet/grad_cert/grad_copy/honor_roll/
     *  grad_ring/gold_grad_bell. (cram/cheat_sheet 15~30%·소량은 제외 — 캡 대상 아님). */
    val INSTANT_CLEAR_ITEMS: Set<String> = setOf(
        "answer_sheet", "grad_cert", "grad_copy", "honor_roll", "grad_ring", "gold_grad_bell",
    )
    fun isInstantClearItem(id: String): Boolean = id in INSTANT_CLEAR_ITEMS

    // ── 장치 (장착형 액티브 — 명령어로 발동, 스테이지당 1회) ──
    //  ARMED  : 다음 스핀 발동(장전 후 "잭팟")     MANIP : 직전 스핀 결과 조작(스핀 소모 X)
    //  PEEK   : 다음 스핀 미리보고 확정            INSTANT: 즉발(비상)
    //  PASSIVE: 장착 시 매 스핀 자동 적용(명령어 없음)   ARMED: 코인 등 자원 소모 능동
    //  MANIP  : 직전 결과 조작(능동)   PEEK: 다음 스핀 미리보기(능동)   INSTANT: 즉발(능동)
    enum class DevKind { PASSIVE, ARMED, MANIP, PEEK, INSTANT }
    //  unlockAch = "업적 해금" — 테마 맞는 업적 1개에 매핑. 장치는 해당 업적 달성으로만 영구해금
    //  (구 license 복합조건 면허모델 폐지 — 업적 단일경로, 2026-06-30). grandfather(ownedDevices 보유)는 서비스가 별도 union.
    data class Device(
        val id: String, val emoji: String, val name: String, val cmd: String, val desc: String,
        val kind: DevKind = DevKind.ARMED, val needsArg: Boolean = false,
        val cooldown: Int = 0, val rare: Boolean = false,
        val unlockAch: String = "",   // 해금 업적 id(ACHIEVEMENTS). 빈=무조건(드롭 전용·임시)
    )
    val DEVICES = listOf(
        // ── 패시브 (장착하면 매 스핀 자동, 명령어 없음) ──
        Device("dev_flame",   "🔥", "불꽃엔진",   "", "장착 시 모든 스핀 EXP +15%", DevKind.PASSIVE, rare = true,
            unlockAch = "lic_flame"),         // 🔥불꽃엔진 면허(lic_dev_flame): 최고점수 50000 & S20
        Device("dev_seal",    "🔒", "봉인장막",   "", "장착 시 모든 스핀 ☠해골 미등장 · EXP +5%", DevKind.PASSIVE,
            unlockAch = "lic_seal"),          // 🔒봉인장막 면허(lic_dev_seal): 해골 누적 200 & S8
        Device("dev_safe",    "🦺", "안전벨트",   "", "장착 시 모든 스핀 최소 EXP 보장(폭망 방지)", DevKind.PASSIVE,
            unlockAch = "lic_safe"),          // 🦺안전벨트 면허(lic_dev_safe): 아슬 클리어 5 & S6
        Device("dev_overheat","♨️", "과열코어",   "", "장착 시 모든 스핀 EXP +18%·☠해골 +1 등장(고위험)", DevKind.PASSIVE, rare = true,
            unlockAch = "lic_overheat"),      // ♨️과열코어 면허(lic_dev_overheat): 막판 클리어 10 & 최고점수 20000
        Device("dev_subreel", "➕", "보조릴",     "", "장착 시 항상 6칸 슬롯 · 최종 EXP -30%", DevKind.PASSIVE, rare = true,
            unlockAch = "lic_subreel"),       // ➕보조릴 면허(lic_dev_subreel): 잭팟 5 & 4세트+ 10
        // ── 능동 (명령어) ──
        Device("dev_coin",  "🪙", "코인투입구", "투입",   "코인5 소모 → 다음 스핀 EXP +30%", DevKind.ARMED,
            unlockAch = "lic_coin"),          // 🪙코인투입구 면허(lic_dev_coin): 코인 누적 500 & 상점구매 15
        Device("dev_reroll","🔄", "재굴림기",   "재굴림", "직전 스핀 결과 다시 굴림 (EXP -10% · 3🪙)", DevKind.MANIP,
            unlockAch = "lic_reroll"),        // 🔄재굴림기 면허(lic_dev_reroll): 보스 클리어 3 & 막판 클리어 3
        Device("dev_pin",   "📌", "고정핀",     "고정",   "직전 결과 N번 칸 유지·나머지 재굴림 (EXP -10% · 3🪙, 예: 고정 3)", DevKind.MANIP, needsArg = true,
            unlockAch = "lic_pin"),           // 📌고정핀 면허(lic_dev_pin): 정확 클리어 3 & S8
        Device("dev_copy",  "📑", "복사기",     "복사",   "직전 결과 N번 칸을 옆칸에 복사 (EXP -10% · 5🪙, 예: 복사 3)", DevKind.MANIP, needsArg = true, rare = true,
            unlockAch = "lic_copy"),          // 📑복사기 면허(lic_dev_copy): 프리즘 선택 10 & 4세트+ 10
        Device("dev_swap",  "🔃", "교체기",     "교체",   "직전 결과 N번 칸을 최다 심볼로 교체 (EXP -10% · 5🪙, 예: 교체 2)", DevKind.MANIP, needsArg = true, rare = true,
            unlockAch = "lic_swap"),          // 🔃교체기 면허(lic_dev_swap): 보스 클리어 10 & S15
        Device("dev_oracle","🔮", "예언안경",   "예언",   "다음 스핀을 미리 보고 확정", DevKind.PEEK, rare = true,
            unlockAch = "lic_oracle"),        // 🔮예언안경 면허(lic_dev_oracle): 기도 클리어 3 & S15
        Device("dev_bell",  "🔔", "비상졸업벨", "비상",   "부족 EXP ≤25면 즉시 클리어 (1회 파괴)", DevKind.INSTANT, rare = true,
            unlockAch = "lic_bell"),          // 🔔비상졸업벨 면허(lic_dev_bell): 아슬 클리어 30 & 보스 클리어 8
        // ── P7 증강 보조 계열 (장착 시 증강 선택 보조. 직접 EXP/점수↑·프리즘 확정획득 없음) ──
        Device("dev_syllabus", "📋", "강의계획서", "", "장착 시 증강/유물 선택에 '예상 티어' 사전 안내(파워 변화 없음·정보형)", DevKind.PEEK,
            unlockAch = "prismPick1"),        // 🔮 첫 프리즘 유물(프리즘 1) — 티어 사전안내 테마
        Device("dev_holdfile", "🗂️", "보류파일", "보류", "증강 선택 후보 1개를 보관 → 다음 증강 노드에서 새 후보와 함께 비교(보류 N)", DevKind.ARMED, needsArg = true,
            unlockAch = "item10"),            // 🎒 아이템 애용가(아이템 10) — 보관/관리 테마
        Device("dev_retake",   "🔁", "재시험관", "재추첨", "증강 선택지를 코인 소모로 1회 다시 뽑기(스테이지당 1회)", DevKind.ARMED, rare = true,
            unlockAch = "shop50"),            // 🛍️ 큰손(상점 50) — 코인 재추첨 테마
        Device("dev_major",    "🎓", "전공신청서", "", "장착 시 주력 계열 증강 등장확률 소폭↑(직접 파워↑ 아님·메인슬롯 전용)", DevKind.PASSIVE,
            unlockAch = "runs50"),            // 🔁 베테랑(50런) — 다회차 전공선택 테마
    )
    const val RETAKE_COIN_COST = 8        // dev_retake 재추첨 코인 비용
    fun device(id: String): Device? = DEVICES.firstOrNull { it.id == id }
    fun deviceByCmd(cmd: String): Device? = DEVICES.firstOrNull { it.cmd.isNotEmpty() && it.cmd == cmd }
    val DEVICE_CMD_SET: Set<String> = DEVICES.filter { it.cmd.isNotEmpty() }.map { it.cmd }.toSet()
    fun isPassiveDevice(id: String): Boolean = device(id)?.kind == DevKind.PASSIVE

    // ── 장치 업적 해금(영구해금) — 매핑 업적 1개 달성 시 영구 소지(구 면허모델 폐지) ──
    /** 장치 해금 업적의 (statKey, threshold). 매핑 업적 없으면 빈 리스트(무조건·드롭 전용·임시). */
    fun deviceUnlockReq(dev: Device): List<Pair<String, Long>> =
        ACHIEVEMENTS.firstOrNull { it.id == dev.unlockAch }?.let { listOf(it.key to it.threshold) } ?: emptyList()
    /** 장치 해금 여부 = 매핑 업적 완료(stat[ach.key] >= ach.threshold). grandfather(ownedDevices)는 서비스가 별도 union. */
    fun deviceUnlocked(dev: Device, stat: Map<String, Long>): Boolean {
        val req = deviceUnlockReq(dev)
        return req.isNotEmpty() && meetsReq(req, stat)
    }
    /** 해금 업적을 달성한 장치 목록(영구해금). 업적 미설정(빈) 장치는 제외(드롭 전용·임시). */
    fun unlockedDevices(stat: Map<String, Long>): List<Device> = DEVICES.filter { it.unlockAch.isNotBlank() && deviceUnlocked(it, stat) }
    fun lockedDevices(stat: Map<String, Long>): List<Device> = DEVICES.filter { it.unlockAch.isNotBlank() && !deviceUnlocked(it, stat) }
    /** 해금 힌트 — 업적명 + 진행도(예 "📊고득점자 30000점(12000/30000)"). */
    fun deviceUnlockHint(dev: Device, stat: Map<String, Long> = emptyMap()): String {
        val ach = ACHIEVEMENTS.firstOrNull { it.id == dev.unlockAch } ?: return "무조건"
        return "${ach.emoji}${ach.name} ${reqHint(listOf(ach.key to ach.threshold), stat)}"
    }
    /** 장치 해금에 쓰이는 업적 진행률(0.0~1.0) — 웹/정렬용. */
    fun deviceUnlockProgress(dev: Device, stat: Map<String, Long>): Double = reqProgress(deviceUnlockReq(dev), stat)

    // ── 보조 장치 슬롯 (P3, 2026-06-29) ───────────────────────
    //  메인 슬롯(run.device) = 모든 장치 가능. 보조 슬롯(run.device2) = 극후반 해금 + 계열제한 + 약화.
    //  3번째 슬롯 절대 금지(메인+보조 = 최대 2). 듀얼 캐릭/멘토 슬롯은 이번 배치 제외.
    /** 보조 장치 슬롯 해금 조건 — devicesOwned≥5 & deviceUses≥30 & bestStage≥12 (stat 기반, 저장 불필요). */
    fun slot2Unlocked(stat: Map<String, Long>): Boolean =
        (stat["devicesOwned"] ?: 0L) >= 5L &&
        (stat["deviceUses"] ?: 0L) >= 30L &&
        (stat["bestStage"] ?: 0L) >= 12L
    /** 보조 슬롯에 장착 가능한 장치인가 — ARMED/PEEK 계열만 허용(PASSIVE/MANIP/INSTANT 금지). */
    fun secondaryAllowed(dev: Device): Boolean = dev.kind == DevKind.ARMED || dev.kind == DevKind.PEEK
    fun secondaryAllowed(deviceId: String): Boolean = device(deviceId)?.let { secondaryAllowed(it) } ?: false
    /** 보조 슬롯에 장착 가능한 장치 목록(업적해금 충족 + ARMED/PEEK). */
    fun secondaryEquipable(stat: Map<String, Long>): List<Device> =
        unlockedDevices(stat).filter { secondaryAllowed(it) }
    /** 같은 장치/같은 계열(kind) 양슬롯 동시 금지 — main 장착 시 보조 후보 필터. */
    fun secondaryCandidates(mainDeviceId: String, stat: Map<String, Long>): List<Device> {
        val main = device(mainDeviceId)
        return secondaryEquipable(stat).filter { it.id != mainDeviceId && (main == null || it.kind != main.kind) }
    }

    /** 보조 슬롯 약화 배수 — ARMED 효과 수치에 곱(권장 60%; PEEK은 약화 어려워 적용 제외, 그대로 허용+보조 표시). */
    const val SECONDARY_MUL = 0.6
    /** 보조 약화 적용 헬퍼: 가산 효과(증분)를 SECONDARY_MUL 만큼 약화.
     *  예) dev_coin 보조 → expMul *1.3(증분 +0.30) 대신 +0.18(=1+0.30*0.6) 로 적용.
     *  서비스 applyItemMods/handleDevice 에서 보조 ARMED 효과 적용 시 이 헬퍼로 증분 약화. */
    fun secondaryWeaken(increment: Double): Double = increment * SECONDARY_MUL
    /** 곱배수(예 1.30)를 보조용으로 약화한 곱배수 반환(증분 부분만 약화). 1.30 → 1.18 (=1+0.30*0.6). */
    fun secondaryMul(fullMul: Double): Double = 1.0 + (fullMul - 1.0) * SECONDARY_MUL

    /** 패시브 장치 효과를 Mods에 오버레이 (장착 시 매 스핀 자동). dev_safe(하한)·dev_subreel(reel+1)은 서비스서 추가 처리. */
    fun applyPassiveDevice(base: Mods, deviceId: String): Mods = when (deviceId) {
        "dev_flame" -> base.copy(expMul = base.expMul * 1.15)
        "dev_seal" -> base.copy(expMul = base.expMul * 1.05, symbolWeightMul = base.symbolWeightMul + ("skull" to ((base.symbolWeightMul["skull"] ?: 1.0) * 0.0)))
        "dev_overheat" -> base.copy(expMul = base.expMul * 1.18, weightAdd = base.weightAdd + ("skull" to ((base.weightAdd["skull"] ?: 0.0) + 1.0)))
        "dev_subreel" -> base.copy(expMul = base.expMul * 0.7)   // 6칸 슬롯(서비스 reel+1)의 대가 — 최종 EXP -30%
        else -> base
    }
    /** 장치 추첨 — 후반일수록 희귀 장치 확률↑. */
    fun pickDevices(rng: Random, stage: Int = 1, n: Int = 1): List<Device> {
        val rareChance = (0.15 + stage * 0.03).coerceAtMost(0.6)
        return (1..n).map {
            val pool = if (rng.nextDouble() < rareChance) DEVICES.filter { d -> d.rare } else DEVICES.filter { d -> !d.rare }
            pool.randomOrNull(rng) ?: DEVICES.random(rng)
        }
    }

    // ══════════════════════════════════════════════════════════
    //  배치3a — 상시 도전판 / 숙련도 / 빌드도감 / 개인기록 (P4/P6)
    //  전부 리셋·만료 없는 상시 구조. 매일숙제/시즌/시간제한 일절 없음.
    // ══════════════════════════════════════════════════════════

    // ── (1) 표준 도전 (지시서 9-1) — 실존 카운터로만 판정. 보상=칭호/도감/힌트(핵심기능 히든 금지) ──
    //  reward 는 "표시용" 안내 텍스트일 뿐(면허=장치 / unlockReq=캐릭·머신 해금이 실보상).
    data class StdChallenge(
        val id: String, val emoji: String, val name: String, val desc: String,
        val req: List<Pair<String, Long>>, val reward: String = "칭호·도감",
    )
    val STD_CHALLENGES = listOf(
        StdChallenge("ch_disarm",   "🚫", "무장해제", "장치 없이 S10 도달",            listOf("noDevStage" to 10L), "칭호 '맨손졸업'"),
        StdChallenge("ch_cram",     "⏰", "벼락치기", "막판 스핀 클리어 5회",          listOf("lastSpinClears" to 5L), "칭호 '벼락치기왕'"),
        StdChallenge("ch_precise",  "🎯", "정밀계산", "요구 EXP 정확 클리어 3회",       listOf("exactClears" to 3L), "칭호 '계산기'"),
        StdChallenge("ch_skullboss","💀", "해골연구", "저주 3개↑ 보유로 보스 클리어",   listOf("curseBossClears" to 1L), "도감"),
        StdChallenge("ch_frugal",   "🪙", "검소한졸업", "상점 한 번도 안 쓰고 S10",     listOf("noShopS10" to 1L), "칭호 '자린고비'"),
        StdChallenge("ch_crown",    "👑", "왕관시험", "잭팟 5회",                       listOf("jackpots" to 5L), "도감"),
        StdChallenge("ch_noitem",   "🧘", "무아이템", "아이템 없이 S8 도달",            listOf("noItemMaxS" to 8L), "칭호 '수도자'"),
        StdChallenge("ch_curse10",  "☠", "저주졸업식", "저주 5개↑ 보유로 S10 도달",     listOf("curse5Stage" to 10L), "칭호 '저주받은졸업'"),
        StdChallenge("ch_overgrad", "💥", "초과졸업", "요구 300% 초과 클리어 (한 스테이지)", listOf("maxOverPct" to 300L), "도감"),
        StdChallenge("ch_jackpot3", "🎰", "한방연발", "한 런에 잭팟 3회",              listOf("maxRunJackpots" to 3L), "도감"),
    )
    fun stdChallenge(id: String): StdChallenge? = STD_CHALLENGES.firstOrNull { it.id == id }

    // ── 통합 도전 항목 (장치면허 + 캐릭/머신 해금 + 표준도전) ──
    enum class ChKind { DEVICE, CHAR, MACHINE, STANDARD }
    data class ChallengeItem(
        val id: String, val emoji: String, val name: String, val kind: ChKind,
        val req: List<Pair<String, Long>>, val done: Boolean,
        val progressText: String,   // (현재/목표) 진행도
        val rewardHint: String,     // 보상 안내(장치/캐릭/머신 해금 또는 칭호·도감)
    )

    /** req + stat → "현재/목표" 진행도 텍스트 (reqHint 재사용). 미충족 항목만 (cur/thr) 표기. */
    fun reqProgressText(req: List<Pair<String, Long>>, stat: Map<String, Long>): String = reqHint(req, stat)

    /**
     * 통합 도전판 — 전부 (id,emoji,name,종류,조건,진행도,달성여부,보상힌트)로 반환.
     *  (a) 장치 업적해금(Device.unlockAch) — 보상=장치 영구해금
     *  (b) 캐릭/머신 복합해금(unlockReq) — 보상=캐릭/머신 해금
     *  (c) 표준 도전(STD_CHALLENGES) — 보상=칭호/도감/힌트
     * 미달성을 앞에(진행률 높은 순), 달성을 뒤에 정렬해 서비스가 그대로 출력.
     */
    fun allChallenges(stat: Map<String, Long>): List<ChallengeItem> {
        val out = mutableListOf<ChallengeItem>()
        // (a) 장치 업적해금 (업적 미설정 장치는 제외 — 드롭 전용·임시)
        for (d in DEVICES.filter { it.unlockAch.isNotBlank() }) {
            val done = deviceUnlocked(d, stat)
            val req = deviceUnlockReq(d)
            out += ChallengeItem(d.id, d.emoji, "${d.name} 해금", ChKind.DEVICE,
                req, done, deviceUnlockHint(d, stat), "${d.emoji}${d.name} 장치 영구해금(업적 ${achName(d.unlockAch)})")
        }
        // (b) 캐릭 해금 (스타터=무료 제외)
        for (c in CHARS.filter { it.unlockReq.isNotEmpty() }) {
            val done = charUnlocked(c, stat)
            out += ChallengeItem("char_${c.id}", c.emoji, "${c.name} 해금", ChKind.CHAR,
                c.unlockReq, done, reqProgressText(c.unlockReq, stat), "${c.emoji}${c.name} 캐릭터 해금")
        }
        // (b) 머신 해금 (스타터=무료 제외)
        for (m in MACHINES.filter { it.unlockReq.isNotEmpty() }) {
            val done = machineUnlocked(m, stat)
            out += ChallengeItem("mac_${m.id}", m.emoji, "${m.name} 머신", ChKind.MACHINE,
                m.unlockReq, done, reqProgressText(m.unlockReq, stat), "${m.emoji}${m.name} 머신 해금")
        }
        // (c) 표준 도전
        for (s in STD_CHALLENGES) {
            val done = meetsReq(s.req, stat)
            out += ChallengeItem(s.id, s.emoji, s.name, ChKind.STANDARD,
                s.req, done, reqProgressText(s.req, stat), s.reward)
        }
        return out
    }

    /** 미달성 중 가장 근접한(진행률 최고) 도전 N개 — 런종료 리포트 '다음 도전 추천'/목표고정 후보. */
    fun nearestChallenges(stat: Map<String, Long>, n: Int = 2): List<ChallengeItem> =
        allChallenges(stat).filter { !it.done }
            .sortedByDescending { reqProgress(it.req, stat) }
            .take(n)

    /** req 진행률 0.0~1.0 (각 조건 cur/thr 의 평균, 캡 1.0). 정렬·근접도용. */
    fun reqProgress(req: List<Pair<String, Long>>, stat: Map<String, Long>): Double {
        if (req.isEmpty()) return 1.0
        return req.map { (k, thr) ->
            if (thr <= 0) 1.0 else ((stat[k] ?: 0L).toDouble() / thr).coerceIn(0.0, 1.0)
        }.average()
    }

    /** 도전 id(통합) 로 ChallengeItem 조회 — 목표고정 검증/표시용. */
    fun challengeById(id: String, stat: Map<String, Long>): ChallengeItem? =
        allChallenges(stat).firstOrNull { it.id == id }

    // ── (2) 숙련도 메달 — cstage_<char> / mstage_<machine> 의 최고스테이지로 동/은/금 ──
    enum class Medal(val emoji: String, val label: String) { NONE("·", "없음"), BRONZE("🥉", "동"), SILVER("🥈", "은"), GOLD("🥇", "금") }
    const val MEDAL_BRONZE_S = 5      // S5 동
    const val MEDAL_SILVER_S = 10     // S10 은
    const val MEDAL_GOLD_S = 15       // S15 금
    fun medalFor(stage: Long): Medal = when {
        stage >= MEDAL_GOLD_S -> Medal.GOLD
        stage >= MEDAL_SILVER_S -> Medal.SILVER
        stage >= MEDAL_BRONZE_S -> Medal.BRONZE
        else -> Medal.NONE
    }
    /** 캐릭별 숙련 메달 (cstage_<charId> 최고스테이지 기준). */
    fun charMastery(charId: String, stat: Map<String, Long>): Medal = medalFor(stat["cstage_$charId"] ?: 0L)
    fun charBestStage(charId: String, stat: Map<String, Long>): Long = stat["cstage_$charId"] ?: 0L
    /** 머신별 숙련 메달 (mstage_<machineId> 최고스테이지 기준). */
    fun machineMastery(machineId: String, stat: Map<String, Long>): Medal = medalFor(stat["mstage_$machineId"] ?: 0L)
    fun machineBestStage(machineId: String, stat: Map<String, Long>): Long = stat["mstage_$machineId"] ?: 0L

    // ── (3) 빌드도감 키 규약 + (4) 개인기록 카운터 키 ──
    /** 클리어/게임오버 시 setMax 로 기록할 캐릭+머신 조합 최고스테이지 키. (서비스가 setMax(bcKey, stage)) */
    fun bcKey(charId: String, machineId: String): String = "bc_${charId}_${machineId}"
    fun isBcKey(key: String): Boolean = key.startsWith("bc_")
    /** bc_<char>_<machine> → (charId, machineId) 복원. 캐릭/머신 id 에 '_' 없음을 전제로 안전 분리. */
    fun parseBcKey(key: String): Pair<String, String>? {
        if (!isBcKey(key)) return null
        val body = key.removePrefix("bc_")
        // charId 가 CHARS 중 prefix 매칭되는 가장 긴 것을 찾아 분리(둘 다 '_' 미포함이지만 방어적).
        val cid = CHARS.map { it.id }.filter { body.startsWith("${it}_") }.maxByOrNull { it.length } ?: return null
        val mid = body.removePrefix("${cid}_")
        return cid to mid
    }

    /** 빌드도감 목록 — 클리어/플레이한 (캐릭,머신,최고스테이지). bc_* 카운터 키에서 복원. 스테이지 내림차순. */
    data class BuildDexRow(val charId: String, val machineId: String, val stage: Long)
    fun buildDex(stat: Map<String, Long>): List<BuildDexRow> =
        stat.filterKeys { isBcKey(it) }.mapNotNull { (k, v) ->
            parseBcKey(k)?.let { (c, m) -> BuildDexRow(c, m, v) }
        }.sortedByDescending { it.stage }
    /** 가능한 전체 조합 수(캐릭×머신) — 도감 진행도 분모. */
    fun buildDexTotal(): Int = CHARS.size * MACHINES.size

    // ══════════════════════════════════════════════════════════
    //  빌드 도감 (테마 빌드 25종) — 2026-06-29
    //  캐릭×머신 조합도감(bc_*)과 별개. "플레이스타일 완성" 칭호형 도감.
    //  완성판정 = clearStage/gameOver 시 evalThemeBuilds() 평가 → 서비스가 counters bld_<id>=1 (setMax).
    //  보상 = 도감 표기·졸업EXP(+40, accountExp ⑤가 bld_ 집계). 핵심기능 히든 없음.
    // ══════════════════════════════════════════════════════════
    data class ThemeBuild(val id: String, val emoji: String, val name: String, val category: String, val cond: String)
    val THEME_BUILDS = listOf(
        // ── 성장형 ──
        ThemeBuild("bld_fast_start",    "🚀", "빠른입학",     "성장형", "남은스핀 2+ 클리어 3회(한 런)"),
        ThemeBuild("bld_model_growth",  "📈", "모범성장",     "성장형", "S5 도달 & 증강/유물 3개+ 보유"),
        ThemeBuild("bld_cherry_sprout", "🍒", "체리새싹런",   "성장형", "체리계열 증강 2개+ & S7 도달"),
        ThemeBuild("bld_library_start", "📚", "도서관스타트", "성장형", "책계열 증강 2개+ & S7 도달"),
        ThemeBuild("bld_foundation",    "🧱", "기초공사",     "성장형", "프리즘 증강 0개로 S10 도달"),
        // ── 운명형 ──
        ThemeBuild("bld_fate_hand",     "🤲", "운명의손",     "운명형", "기도 성공 2회(한 런)"),
        ThemeBuild("bld_dice_grad",     "🎲", "주사위의졸업식","운명형", "🎲카지노 머신으로 S10 도달"),
        ThemeBuild("bld_crown_caller",  "👑", "왕관을부르는자","운명형", "한 런 👑왕관 10개+ 등장"),
        ThemeBuild("bld_prob_hacker",   "🎯", "확률조작자",   "운명형", "희귀심볼 5개+ 스핀으로 보스 클리어"),
        ThemeBuild("bld_jackpot_seer",  "🔮", "잭팟예언자",   "운명형", "🔮예언안경 사용 후 잭팟 발생"),
        // ── 역전형 ──
        ThemeBuild("bld_cliff_pass",    "🧗", "벼랑끝합격",   "역전형", "막스핀 클리어 3회(한 런)"),
        ThemeBuild("bld_heartbreaker",  "💔", "심장파괴자",   "역전형", "통산 아슬아슬 클리어 5회"),
        ThemeBuild("bld_cram_grad",     "⏰", "벼락치기졸업", "역전형", "막스핀배율 증강 3개+ 후 보스 클리어"),
        ThemeBuild("bld_miracle_cert",  "🔔", "기적의졸업장", "역전형", "🔔비상졸업벨로 보스 클리어"),
        ThemeBuild("bld_last_candle",   "🕯️", "마지막촛불",   "역전형", "S10+ 에서 막스핀 클리어"),
        // ── 조합형 ──
        ThemeBuild("bld_magnet_grad",   "🧲", "끌어당기는졸업","조합형", "🧲자석 머신으로 세트4+ 완성"),
        ThemeBuild("bld_wild_puzzle",   "🌀", "와일드퍼즐",   "조합형", "🌀와일드 포함 잭팟"),
        ThemeBuild("bld_pinned_fate",   "📌", "고정된운명",   "조합형", "📌고정핀 사용 후 스테이지 클리어"),
        ThemeBuild("bld_copy_answer",   "📑", "복사답안",     "조합형", "📑복사기로 세트4+ 완성"),
        ThemeBuild("bld_chain",         "🔗", "연쇄반응",     "조합형", "한 런 인접쌍 보너스 5회+"),
        // ── 위험형 ──
        ThemeBuild("bld_skull_intro",   "☠", "해골입문",     "위험형", "통산 ☠해골 100개+ 등장"),
        ThemeBuild("bld_black_grad",    "🖤", "검은졸업식",   "위험형", "저주 3개+ 보유로 보스 클리어"),
        ThemeBuild("bld_ossuary",       "💀", "납골당졸업",   "위험형", "☠해골 5개+ 나온 스핀으로 클리어"),
        ThemeBuild("bld_curse_vessel",  "🏺", "저주의그릇",   "위험형", "저주 7개+ 보유 & S10 도달"),
        ThemeBuild("bld_ominous_overheat","♨️", "불길한과열",  "위험형", "♨️과열코어 + 저주 3개+ 로 S10 도달"),
    )
    fun themeBuild(id: String): ThemeBuild? = THEME_BUILDS.firstOrNull { it.id == id }
    fun themeBuildTotal(): Int = THEME_BUILDS.size
    /** 카테고리 순서(도감 그룹 표시용). */
    val THEME_BUILD_CATEGORIES = listOf("성장형", "운명형", "역전형", "조합형", "위험형")

    /** 빌드도감 보유여부(완성=counters bld_<id>>0). */
    fun themeBuildDone(id: String, stat: Map<String, Long>): Boolean = (stat[id] ?: 0L) > 0L
    fun themeBuildsDoneCount(stat: Map<String, Long>): Int = THEME_BUILDS.count { themeBuildDone(it.id, stat) }

    /**
     * 빌드도감 파생 통계 — ACH-6(빌드도감 업적)용 순수 파생(distinctCharS10/lic_ 와 동일, 신규 추적/DB 0).
     *  bld_<id> 완성 플래그(stat[id]>0)를 THEME_BUILDS/THEME_BUILD_CATEGORIES 로 집계.
     *   - bldCat_<category> : 그 category 의 완성 빌드 수
     *   - bldTotal          : 전체 완성 수
     *   - bldAllBasic       : 완성 빌드를 1개+ 가진 카테고리 수(=전 카테고리 1개+ 면 5)
     *   - bldAllMaster      : 그 category 의 빌드를 전부 완성한 카테고리 수(=전 마스터면 5)
     *  서비스 composeStat 가 이 맵을 stat 에 머지(키 충돌 없음 — bldCat_/bldTotal/bldAllBasic/bldAllMaster 전용).
     */
    fun themeBuildStats(stat: Map<String, Long>): Map<String, Long> {
        val out = LinkedHashMap<String, Long>()
        var total = 0L
        var allBasic = 0L
        var allMaster = 0L
        for (cat in THEME_BUILD_CATEGORIES) {
            val builds = THEME_BUILDS.filter { it.category == cat }
            val done = builds.count { themeBuildDone(it.id, stat) }
            out["bldCat_$cat"] = done.toLong()
            total += done
            if (done >= 1) allBasic++
            if (builds.isNotEmpty() && done == builds.size) allMaster++
        }
        out["bldTotal"] = total
        out["bldAllBasic"] = allBasic
        out["bldAllMaster"] = allMaster
        return out
    }

    /**
     * 빌드 도감 완성판정 컨텍스트 — clearStage/gameOver 시점의 런 상태.
     *  서비스가 매 클리어/게임오버 직전에 채워 evalThemeBuilds() 호출 → 충족 bld id 셋 반환 → counters setMax(id,1).
     *  필드 = 런누적(run*) + 보유(perks/curses) + 머신id + 이번 이벤트 플래그(보스클리어·잭팟·셀조작 등).
     *  ★ 머신/장치 실 id: 자석=magnet·카지노=casino·불꽃=flame / 복사기=dev_copy·고정핀=dev_pin·
     *    예언안경=dev_oracle·과열코어=dev_overheat·비상졸업벨=dev_bell.
     */
    data class BuildCtx(
        val stage: Int = 0,              // 방금 클리어/도달한 스테이지(=이번 평가 시점 도달치)
        val machineId: String = "",
        val deviceId: String = "",       // 메인 장치
        val device2Id: String = "",      // 보조 장치
        val perks: List<String> = emptyList(),
        val curses: List<String> = emptyList(),
        // 런 누적 카운터(SlotV2RunRow run* 필드)
        val runFastClears: Int = 0,      // 남은스핀≥2 클리어 수
        val runLastSpinClears: Int = 0,  // 막스핀 클리어 수
        val runPrayWins: Int = 0,        // 기도 성공 수
        val runAdjPairs: Int = 0,        // 인접쌍 보너스 발동 수
        val runSet4: Int = 0,            // 세트4+ 완성 수
        val runCrowns: Int = 0,          // 한 런 👑왕관 등장 수(runSymCounts crown)
        // 이번 클리어/게임오버 이벤트 플래그(서비스가 직전 스핀/클리어 사실로 세팅)
        val isBossClear: Boolean = false,        // 이번 클리어가 보스 스테이지 클리어
        val isLastSpinClear: Boolean = false,    // 이번 클리어가 막스핀에서 성사
        val clearSpinRareCount: Int = 0,         // 이번 클리어 성사 스핀의 희귀심볼 수
        val clearSpinSkullCount: Int = 0,        // 이번 클리어 성사 스핀의 ☠해골 수
        val clearSpinWildJackpot: Boolean = false, // 이번 잭팟에 🌀와일드 포함
        val jackpotThisRun: Boolean = false,     // 이번 런 잭팟 발생(예언자용)
        val oracleUsedThisRun: Boolean = false,  // 이번 런 예언안경 사용
        val pinUsedThisStage: Boolean = false,   // 이번 스테이지 고정핀 사용
        val copyMadeSet4: Boolean = false,       // 복사기로 세트4+ 완성
        val bellUsedThisClear: Boolean = false,  // 이번 클리어가 비상졸업벨로 성사
        // lifetime stat(누적)
        val skullTotal: Long = 0,
        val closeClears: Long = 0,
    )

    /** desc 이모지로 perk 계열 카운트(체리/책/막스핀배율 등). favoredSymbol 과 동일 규약. */
    private fun perkDescCount(perks: List<String>, predicate: (String) -> Boolean): Int =
        perks.count { id -> ALL_PERKS[id]?.desc?.let(predicate) == true }
    private fun isPrismPerk(id: String): Boolean = ALL_PERKS[id]?.tier == Tier.PRISM

    /**
     * 빌드 도감 완성판정 — 충족된 bld_<id> 셋 반환(순수함수). 서비스가 clearStage/gameOver 시 호출 후
     *  반환 id 마다 counters setMax(id, 1). 미충족은 미포함(이미 완성분은 setMax 라 영구 유지).
     */
    fun evalThemeBuilds(ctx: BuildCtx): Set<String> {
        val out = HashSet<String>()
        val perks = ctx.perks
        val nCurses = ctx.curses.size
        val cherryPerks = perkDescCount(perks) { it.contains("🍒") }
        val bookPerks = perkDescCount(perks) { it.contains("📘") }
        val lastSpinPerks = perkDescCount(perks) { it.contains("마지막 스핀") || it.contains("막스핀") || it.contains("막 스핀") }
        val prismPerks = perks.count { isPrismPerk(it) }
        fun dev(id: String) = ctx.deviceId == id || ctx.device2Id == id

        // ── 성장형 ──
        if (ctx.runFastClears >= 3) out += "bld_fast_start"
        if (ctx.stage >= 5 && perks.size >= 3) out += "bld_model_growth"
        if (cherryPerks >= 2 && ctx.stage >= 7) out += "bld_cherry_sprout"
        if (bookPerks >= 2 && ctx.stage >= 7) out += "bld_library_start"
        if (prismPerks == 0 && ctx.stage >= 10) out += "bld_foundation"
        // ── 운명형 ──
        if (ctx.runPrayWins >= 2) out += "bld_fate_hand"
        if (ctx.machineId == "casino" && ctx.stage >= 10) out += "bld_dice_grad"
        if (ctx.runCrowns >= 10) out += "bld_crown_caller"
        if (ctx.isBossClear && ctx.clearSpinRareCount >= 5) out += "bld_prob_hacker"
        if (ctx.oracleUsedThisRun && ctx.jackpotThisRun) out += "bld_jackpot_seer"
        // ── 역전형 ──
        if (ctx.runLastSpinClears >= 3) out += "bld_cliff_pass"
        if (ctx.closeClears >= 5) out += "bld_heartbreaker"
        if (lastSpinPerks >= 3 && ctx.isBossClear) out += "bld_cram_grad"
        if (ctx.bellUsedThisClear && ctx.isBossClear) out += "bld_miracle_cert"
        if (ctx.stage >= 10 && ctx.isLastSpinClear) out += "bld_last_candle"
        // ── 조합형 ──
        if (ctx.machineId == "magnet" && ctx.runSet4 >= 1) out += "bld_magnet_grad"
        if (ctx.clearSpinWildJackpot) out += "bld_wild_puzzle"
        if (ctx.pinUsedThisStage) out += "bld_pinned_fate"
        if (ctx.copyMadeSet4) out += "bld_copy_answer"
        if (ctx.runAdjPairs >= 5) out += "bld_chain"
        // ── 위험형 ──
        if (ctx.skullTotal >= 100) out += "bld_skull_intro"
        if (nCurses >= 3 && ctx.isBossClear) out += "bld_black_grad"
        if (ctx.clearSpinSkullCount >= 5) out += "bld_ossuary"
        if (nCurses >= 7 && ctx.stage >= 10) out += "bld_curse_vessel"
        if (dev("dev_overheat") && nCurses >= 3 && ctx.stage >= 10) out += "bld_ominous_overheat"
        return out
    }

    // 개인기록 — clearStage/gameOver 에서 setMax 로 갱신할 카운터 키(전부 누적 최댓값).
    //  maxRunJackpots = 한 런 최다 잭팟 / maxOverPct = 한 스테이지 최대 초과% / noDevStage = 무장치 최고도달 S
    const val KEY_MAX_RUN_JACKPOTS = "maxRunJackpots"
    const val KEY_MAX_OVER_PCT = "maxOverPct"
    const val KEY_NO_DEV_STAGE = "noDevStage"
    const val KEY_NO_ITEM_MAX_S = "noItemMaxS"
    const val KEY_CURSE5_STAGE = "curse5Stage"
    const val KEY_CURSE_BOSS_CLEARS = "curseBossClears"
    const val KEY_NO_SHOP_S10 = "noShopS10"
    // ── ACH-4: 제한도전 최고도달 S (clearStage 클리어 시점, 조건충족 setMax) ──
    const val KEY_NO_PRISM_STAGE = "noPrismBestStage"     // 프리즘 증강 0개로 도달
    const val KEY_NO_RELIC_STAGE = "noRelicBestStage"     // 유물 0개로 도달
    const val KEY_NO_GOLD_STAGE = "noGoldBestStage"       // 골드+프리즘 증강 0개(실버/유물만)로 도달
    const val KEY_BASIC_ONLY_STAGE = "basicOnlyBestStage" // 초보캐릭+기본머신으로 도달
    // ── ACH-5c: 무명령/무조작 제한도달 S (clearStage 클리어 시점, run 플래그 0 일 때 setMax) ──
    const val KEY_NO_CMD_STAGE = "noCommandBestStage"     // 특수 스핀명령(집중/올인/기도/최후) 0회로 도달
    const val KEY_NO_REROLL_STAGE = "noRerollBestStage"   // 재굴림/고정/복사/교체 0회(무조작)로 도달

    /** 개인기록 표시 헬퍼 — bestScore/bestStage/runs + 신규 setMax 기록을 한 줄 라벨 리스트로. */
    fun recordLines(stat: Map<String, Long>): List<String> = listOf(
        "🏆 최고점수 ${"%,d".format(stat["bestScore"] ?: 0L)}",
        "🧗 최고도달 S${stat["bestStage"] ?: 0L}",
        "🔁 통산 ${stat["runs"] ?: 0L}런",
        "🎰 한 런 최다잭팟 ${stat[KEY_MAX_RUN_JACKPOTS] ?: 0L}회",
        "💥 한 스테이지 최대초과 ${stat[KEY_MAX_OVER_PCT] ?: 0L}%",
        "🚫 무장치 최고도달 S${stat[KEY_NO_DEV_STAGE] ?: 0L}",
    )

    // ── 업적 (누적 카운터 ≥ 임계) ────────────────────────────
    //  cat: 입문/심볼/명령어/장치/유물/아이템/상점/클리어/아슬아슬/점수/저주/도전/히든/반복
    //  tier: 브론즈/실버/골드/프리즘   reward: 표시용(칭호·도감·해금 안내)
    data class Achievement(
        val id: String, val emoji: String, val name: String, val key: String, val threshold: Long, val desc: String,
        val cat: String = "기타", val tier: String = "브론즈", val reward: String = "", val hidden: Boolean = false,
    )
    val ACHIEVEMENTS_BASE = listOf(
        Achievement("cherry100", "🍒", "체리 수확가",   "cherryTotal", 100, "🍒체리 누적 100개 등장"),
        Achievement("cherry500", "🍒", "체리 중독",     "cherryTotal", 500, "🍒체리 누적 500개 등장"),
        Achievement("crown10",   "👑", "왕관 수집가",   "crownTotal", 10, "👑왕관 누적 10개 등장"),
        Achievement("crown30",   "👑", "대관식",       "crownTotal", 30, "👑왕관 누적 30개 등장"),
        Achievement("jackpot1",  "🎰", "첫 잭팟",      "jackpots", 1, "5칸 잭팟 1회"),
        Achievement("jackpot10", "🎰", "잭팟 헌터",     "jackpots", 10, "5칸 잭팟 10회"),
        Achievement("boss1",     "📝", "중간고사 통과",  "bossClears", 1, "보스 1회 클리어"),
        Achievement("boss5",     "🎓", "졸업반",       "bossClears", 5, "보스 5회 클리어"),
        Achievement("stage10",   "🧗", "10층 등반",     "bestStage", 10, "스테이지 10 도달"),
        Achievement("stage15",   "🏔️", "최종보스 도달",  "bestStage", 15, "스테이지 15 도달"),
        Achievement("lastclear5", "⏰", "벼락치기 천재",  "lastSpinClears", 5, "마지막 스핀 클리어 5회"),
        Achievement("exact1",    "🎯", "완벽한 계산",    "exactClears", 1, "요구 EXP 정확히 일치 클리어"),
        Achievement("prism5",    "🌈", "규칙 파괴자",    "prismPicks", 5, "프리즘 증강 5회 선택"),
        Achievement("score10k",  "💯", "만점왕",        "bestScore", 10_000, "최고 점수 10,000"),
        Achievement("score50k",  "🏆", "슬롯의 지배자",   "bestScore", 50_000, "최고 점수 50,000"),
        Achievement("runs20",    "🔁", "단골",         "runs", 20, "20런 플레이"),
    )
    /** 기본 16 + 확장(SlotV2AchievementsExt) 전체. */
    val ACHIEVEMENTS: List<Achievement> = ACHIEVEMENTS_BASE + SlotV2AchievementsExt.LIST

    // (장치 영구해금 = Device.unlockAch 업적 단일 경로(deviceUnlocked). 구 면허/직접 영구지급 폐지.)

    /** NEXTSPIN/PHASE 아이템 효과를 기존 Mods 위에 오버레이 (INSTANT는 서비스서 즉시 처리). */
    fun applyItemMods(base: Mods, itemIds: List<String>): Mods {
        if (itemIds.isEmpty()) return base
        var expMul = base.expMul; var scoreMul = base.scoreMul; var coinMul = base.coinMul
        var flatExp = base.flatExp; var rareWeightMul = base.rareWeightMul
        var lastSpinExpMul = base.lastSpinExpMul; var quotaMul = base.quotaMul; var clearCoinBonus = base.clearCoinBonus
        var skullScoreBonus = base.skullScoreBonus
        val weightMul = HashMap(base.symbolWeightMul); val weightAdd = HashMap(base.weightAdd)
        for (id in itemIds) when (id) {
            "energy_drink" -> expMul *= 2.0
            "magnify" -> rareWeightMul *= 4.0
            "loaded_dice" -> { weightAdd["crown"] = (weightAdd["crown"] ?: 0.0) + 5.0; scoreMul *= 2.0 }
            "ward_charm" -> weightMul["skull"] = (weightMul["skull"] ?: 1.0) * 0.0
            "espresso" -> flatExp += 15
            "study_streak" -> flatExp += 6
            "rare_lure" -> rareWeightMul *= 2.0
            "coin_magnet" -> { coinMul *= 2.0; clearCoinBonus += 8 }
            "dbl_nothing" -> { flatExp += 30; quotaMul *= 1.2 }
            "last_minute" -> lastSpinExpMul *= 2.0
            // 확장 아이템
            "adrenaline" -> expMul *= 3.0
            "rare_scope" -> rareWeightMul *= 3.0
            "crown_inject" -> weightAdd["crown"] = (weightAdd["crown"] ?: 0.0) + 8.0
            "wild_inject" -> weightAdd["wild"] = (weightAdd["wild"] ?: 0.0) + 6.0
            "tutor" -> flatExp += 10
            "fortune_incense" -> rareWeightMul *= 1.6
            "coin_press" -> coinMul *= 3.0
            "overtime" -> lastSpinExpMul *= 2.0
            // 추가 아이템 (빌드별/조건부)
            "cherry_juice" -> weightMul["cherry"] = (weightMul["cherry"] ?: 1.0) * 2.5
            "bookmark2" -> weightMul["book"] = (weightMul["book"] ?: 1.0) * 2.5
            "sparkle_dust" -> weightMul["gem"] = (weightMul["gem"] ?: 1.0) * 2.5
            "gold_chalk" -> expMul *= 2.0
            "focus_candy" -> expMul *= 1.15
            "cram_note" -> lastSpinExpMul *= 2.0
            "rich_lure" -> rareWeightMul *= 3.0
            "prof_bribe" -> quotaMul *= 0.85
            // ── 단순레버 확장 (2026-06-24) ──
            "small_snack", "skull_shield", "seal_tape" -> weightMul["skull"] = (weightMul["skull"] ?: 1.0) * 0.0
            "cherry_basket" -> weightAdd["cherry"] = (weightAdd["cherry"] ?: 0.0) + 6.0
            "gem_loupe" -> { weightMul["gem"] = (weightMul["gem"] ?: 1.0) * 2.0; scoreMul *= 2.0 }
            "sugar_powder" -> { weightMul["cherry"] = (weightMul["cherry"] ?: 1.0) * 1.6; flatExp += 8 }
            "cherry_cracker" -> { weightMul["cherry"] = (weightMul["cherry"] ?: 1.0) * 2.0; scoreMul *= 1.2 }
            "book_copy" -> { weightMul["book"] = (weightMul["book"] ?: 1.0) * 2.0; flatExp += 8 }
            "allnight_note" -> { weightMul["book"] = (weightMul["book"] ?: 1.0) * 1.8; flatExp += 12 }
            "summary_note" -> flatExp += 9
            "gem_pouch" -> { weightMul["gem"] = (weightMul["gem"] ?: 1.0) * 2.0; scoreMul *= 1.25 }
            "greed_lens" -> scoreMul *= 1.5
            "black_candle_i" -> { weightMul["skull"] = (weightMul["skull"] ?: 1.0) * 2.0; expMul *= 1.3 }
            "curse_amp" -> { weightMul["skull"] = (weightMul["skull"] ?: 1.0) * 1.6; scoreMul *= 1.4 }
            "gold_chalk_box" -> expMul *= 1.5
            "combo_mega" -> { lastSpinExpMul *= 2.0; scoreMul *= 1.2 }
            "cram_note_x2" -> lastSpinExpMul *= 2.0
            "overload_potion" -> { expMul *= 2.0; quotaMul *= 1.2 }
            // ── 복잡아이템 NEXTSPIN 레버 (셀조작 아닌 것) ──
            "skull_sticker" -> skullScoreBonus += 100   // (e) ×해골수는 evaluate가 처리
            // 주: seal_tape는 위 small_snack 줄에 합침. eraser_*/wild_temp/fake_crown은 applyCellOps(셀조작)에서.
            // 능동 장치(코인투입) — 다음스핀 발동. 패시브(점화/봉인/안전/과열/보조릴)는 applyPassiveDevice, MANIP/PEEK/비상은 서비스서.
            "dev_coin" -> expMul *= 1.3
        }
        return base.copy(
            expMul = expMul, scoreMul = scoreMul, coinMul = coinMul, flatExp = flatExp,
            rareWeightMul = rareWeightMul, lastSpinExpMul = lastSpinExpMul, quotaMul = quotaMul,
            clearCoinBonus = clearCoinBonus, symbolWeightMul = weightMul, weightAdd = weightAdd,
            skullScoreBonus = skullScoreBonus,
        )
    }

    /** 스테이지 진행도에 따라 티어 가중 — 후반일수록 골드/프리즘↑. */
    private fun tierWeights(stage: Int): Triple<Double, Double, Double> = when {
        stage <= 3 -> Triple(78.0, 22.0, 0.0)   // 초보: 프리즘 미노출 (스테이지4+ 등장)
        stage <= 6 -> Triple(50.0, 42.0, 8.0)
        stage <= 9 -> Triple(35.0, 50.0, 15.0)
        else       -> Triple(22.0, 53.0, 25.0)
    }

    private fun rollTier(rng: Random, stage: Int): Tier {
        val (s, g, p) = tierWeights(stage)
        val r = rng.nextDouble() * (s + g + p)
        return if (r < s) Tier.SILVER else if (r < s + g) Tier.GOLD else Tier.PRISM
    }

    /** (P7·dev_syllabus PEEK) 해당 스테이지의 티어 확률을 사람이 읽는 텍스트로 — pickPerksByTier 의 stage 가중과
     *  동일 비율(silverW/goldW)로 산출(정보형, 파워 변화 없음). 프리즘은 일반 노드 제외(보스클리어 전용)이므로
     *  비보스 표기엔 미포함. 보스클리어 노드면 🌈프리즘 확정 안내. 예 "🥈45% 🥇55%". */
    fun tierOddsHint(stage: Int, bossClear: Boolean = false): String {
        if (bossClear) return "🌈프리즘 확정 (보스클리어 보상)"
        val silverW = (12 - stage).coerceAtLeast(2).toDouble()
        val goldW = (4 + stage * 2).toDouble()
        val total = (silverW + goldW).coerceAtLeast(1.0)
        fun pct(w: Double) = (w / total * 100).toInt()
        return "🥈${pct(silverW)}% 🥇${pct(goldW)}%"
    }

    /** 증강 3개 추첨 (보유 제외, 티어 가중). */
    fun pickAugments(rng: Random, stage: Int, held: Set<String>, n: Int = 3, stat: Map<String, Long> = emptyMap()): List<Perk> {
        val src = gatedPool(AUGMENTS, stat)
        val out = mutableListOf<Perk>(); val used = held.toMutableSet(); var guard = 0
        while (out.size < n && guard++ < 60) {
            val tier = rollTier(rng, stage)
            val pick = src.filter { it.tier == tier && it.id !in used }.randomOrNull(rng)
                ?: src.filter { it.id !in used }.randomOrNull(rng) ?: break
            out += pick; used += pick.id
        }
        return out
    }

    /** 유물 3개 추첨 (보유 제외). stat 지정 시 미해금 유물 제외(전부잠김이면 BASE 폴백). */
    fun pickRelics(rng: Random, held: Set<String>, n: Int = 3, stat: Map<String, Long> = emptyMap()): List<Perk> =
        gatedPool(RELICS, stat).filter { it.id !in held }.shuffled(rng).take(n)

    /** 해금 게이트 필터 — unlockedPerks 결과, 전부 잠겼으면 BASE_PERK_IDS 분만 폴백(신규 데드엔드 방지). */
    fun gatedPool(pool: List<Perk>, stat: Map<String, Long>): List<Perk> {
        if (stat.isEmpty()) return pool
        val unlocked = unlockedPerks(pool, stat)
        return unlocked.ifEmpty { pool.filter { it.id in BASE_PERK_IDS } }.ifEmpty { pool }
    }

    /** 보유 perk가 가장 많이 강화한 심볼 이모지 (빌드 방향 추정). */
    fun favoredSymbol(held: Set<String>): String? {
        val emojis = listOf("🍒", "📘", "⭐", "💎", "👑", "☠")
        val best = emojis.maxByOrNull { e -> held.count { id -> ALL_PERKS[id]?.desc?.contains(e) == true } } ?: return null
        return best.takeIf { e -> held.any { id -> ALL_PERKS[id]?.desc?.contains(e) == true } }
    }

    /** 선택지 품질: 안정(실버) + 빌드관련(보유 시너지 골드) + 고점(프리즘/골드). 쓰레기 3택 방지. */
    fun pickAugmentsCurated(rng: Random, stage: Int, held: Set<String>): List<Perk> {
        val used = held.toMutableSet(); val out = mutableListOf<Perk>()
        fun take(p: Perk?) { if (p != null) { out += p; used += p.id } }
        take(AUGMENTS.filter { it.tier == Tier.SILVER && it.id !in used }.randomOrNull(rng))     // 안정
        val fav = favoredSymbol(held)
        val gold = AUGMENTS.filter { it.tier == Tier.GOLD && it.id !in used }
        take((if (fav != null) gold.filter { it.desc.contains(fav) } else emptyList()).randomOrNull(rng) ?: gold.randomOrNull(rng))  // 빌드
        take((if (stage >= 4) AUGMENTS.filter { it.tier == Tier.PRISM && it.id !in used }.randomOrNull(rng) else null)
            ?: AUGMENTS.filter { (it.tier == Tier.GOLD || it.tier == Tier.PRISM) && it.id !in used }.randomOrNull(rng))  // 고점
        var guard = 0
        while (out.size < 3 && guard++ < 40) take(AUGMENTS.filter { it.id !in used }.randomOrNull(rng) ?: break)
        return out.shuffled(rng)
    }

    fun pickRelicsCurated(rng: Random, held: Set<String>): List<Perk> {
        val used = held.toMutableSet(); val out = mutableListOf<Perk>()
        fun take(p: Perk?) { if (p != null) { out += p; used += p.id } }
        take(RELICS.filter { it.tier == Tier.SILVER && it.id !in used }.randomOrNull(rng))        // 안정
        val fav = favoredSymbol(held)
        val gold = RELICS.filter { it.tier == Tier.GOLD && it.id !in used }
        take((if (fav != null) gold.filter { it.desc.contains(fav) } else emptyList()).randomOrNull(rng) ?: gold.randomOrNull(rng))  // 빌드
        var guard = 0
        while (out.size < 3 && guard++ < 40) take(RELICS.filter { it.id !in used }.randomOrNull(rng) ?: break)  // 범용
        return out.shuffled(rng)
    }

    /**
     * 티어 통일 3택 — 한 번의 선택지는 전부 같은 티어(실버끼리/골드끼리/프리즘끼리).
     * 티어를 stage·불운보정(forceRare) 가중으로 고른 뒤, 그 티어에서 빌드시너지(favored) 우선 채움.
     * 해당 티어 풀이 3개 미만이면 타 티어로 폴백해 항상 3개 보장. AUGMENTS/RELICS 공용.
     *
     * @param favoredCat (dev_major 전공신청서) 주력 계열 편향 — 심볼 이모지(🍒/📘/⭐/💎/👑/☠).
     *   지정 시 해당 계열 perk 선출 슬롯을 1칸 더 보장(직접 파워↑ 아님·프리즘 확정 아님, 등장확률 소폭↑).
     *   null 이면 보유 perk 자동추정(favoredSymbol)만 사용 — 기존 동작 동일.
     */
    fun pickPerksByTier(rng: Random, rawPool: List<Perk>, stage: Int, held: Set<String>, forceRare: Boolean,
                        favoredCat: String? = null, stat: Map<String, Long> = emptyMap(),
                        bossClear: Boolean = false, forceTier: Tier? = null): List<Perk> {
        // 해금 게이트 — 미해금 perk 는 후보에서 제외(전부잠김이면 BASE 폴백). stat 빈 맵이면 필터 없음(기존 동작).
        val pool = gatedPool(rawPool, stat)
        val avail = pool.filter { it.id !in held }
        if (avail.isEmpty()) return emptyList()
        fun cnt(t: Tier) = avail.count { it.tier == t }
        // (#6·#7) 프리즘 분배 — 프리즘은 보스클리어(5스테이지) 노드에서만 기본 등장.
        //  · bossClear=true  → 티어=PRISM 강제(해금 프리즘 우선, 없으면 전체 프리즘풀 폴백 → 보스도달=프리즘기회 항상 보장)
        //  · bossClear=false → weights 에서 PRISM 제외(실버/골드만). 이벤트/상점 프리즘은 별도 경로.
        val tier: Tier
        if (forceTier != null) {
            tier = forceTier               // 🗂️보류파일 — 보류한 perk 티어로 통일(티어순수 유지)
        } else if (bossClear) {
            tier = Tier.PRISM
        } else {
            val silverW = if (forceRare) 0 else (12 - stage).coerceAtLeast(2)   // 초반 실버↑, 후반에도 소량 유지
            val goldW = 4 + stage * 2                                           // 골드가 주력(진행할수록↑)
            val weights = listOf(
                Tier.SILVER to (if (cnt(Tier.SILVER) > 0) silverW else 0),
                Tier.GOLD to (if (cnt(Tier.GOLD) > 0) goldW else 0),
                // 프리즘은 일반 노드에서 제외(보스클리어 전용) — weight 0.
            )
            val total = weights.sumOf { it.second }
            tier = if (total <= 0) (avail.firstOrNull { it.tier != Tier.PRISM }?.tier ?: avail.random(rng).tier) else {
                var x = rng.nextInt(total); var picked = weights.first { it.second > 0 }.first
                for ((t, w) in weights) { if (w > 0) { if (x < w) { picked = t; break }; x -= w } }
                picked
            }
        }
        val used = held.toMutableSet(); val out = mutableListOf<Perk>()
        fun take(p: Perk?) { if (p != null) { out += p; used += p.id } }
        val fav = favoredSymbol(held)
        // 보스클리어 프리즘 풀 — 해금분 우선, 비었으면 전체 프리즘풀 폴백(보스도달=프리즘획득 기회 항상 1+개 보장).
        val tierPool: List<Perk> = if (tier == Tier.PRISM) {   // 프리즘(보스클리어 or 프리즘 보류)은 해금분 우선·전체풀 폴백
            pool.filter { it.tier == Tier.PRISM }.ifEmpty { rawPool.filter { it.tier == Tier.PRISM } }
        } else pool.filter { it.tier == tier }
        // (P7·dev_major) 전공 계열 편향 — favoredCat 지정 시 해당 계열 perk 슬롯 1칸 추가 보장(과하지 않게 1칸).
        val cat = favoredCat?.takeIf { it.isNotBlank() }
        if (cat != null) take(tierPool.filter { it.id !in used && it.desc.contains(cat) }.randomOrNull(rng))
        if (fav != null && fav != cat) take(tierPool.filter { it.id !in used && it.desc.contains(fav) }.randomOrNull(rng))  // 같은 티어 내 빌드시너지 우선
        var guard = 0
        // (#8) 티어순수 — 선택 티어 풀에서만 채움. 3개 못 채우면 그냥 적게 제시(2개·1개). 타티어 혼용 금지.
        while (out.size < 3 && guard++ < 80) take(tierPool.filter { it.id !in used }.randomOrNull(rng) ?: break)
        return out.shuffled(rng)
    }

    /**
     * 런 컨텍스트 — 신규 16종 증강의 stage/남은스핀/스택 조건부 효과 계산용(읽기 전용).
     * 서비스가 매 스핀 buildMods 호출 시 현재 run 상태로 채워 전달. 기본값 = 무효(조건 미충족).
     *  - stage: 현재 스테이지(early_prep/early_adapt 게이트)
     *  - spinIndex 0-base, spinsPerStage: 남은스핀=spinsPerStage-spinIndex (late_focus/cliff_focus/fortune_check firstSpin)
     *  - stageExp/quota: cliff_focus(요구60% 미만) 판정
     *  - growthStack(0~5)/snowStack(0~4): 성장일지·눈덩이 누적 EXP%
     *  - curseCount: sacrifice/black_diploma 저주개수
     *  - boss: fate_burst 보스전 약화 분기
     */
    data class RunCtx(
        val stage: Int = 0,
        val spinIndex: Int = 0,
        val spinsPerStage: Int = SPINS_PER_STAGE,
        val stageExp: Long = 0,
        val quota: Long = 0,
        val growthStack: Int = 0,
        val snowStack: Int = 0,
        val curseCount: Int = 0,
        val unluckyGauge: Int = 0,
        val boss: Boolean = false,
    ) {
        val isFirstSpin: Boolean get() = spinIndex == 0
        val isLastSpin: Boolean get() = spinsPerStage in 1..(spinIndex + 1)
        val spinsLeft: Int get() = (spinsPerStage - spinIndex).coerceAtLeast(0)
    }

    /** 머신+캐릭터+perk(+저주) → 스핀 Mods (누산). ctx = 신규 증강 stage/스핀/스택 조건부(기본 무효=기존 동작). */
    fun buildMods(machineId: String, charId: String, perkIds: List<String> = emptyList(),
                  curseIds: List<String> = emptyList(), deviceId: String = "",
                  ctx: RunCtx = RunCtx()): Mods {
        val m = machine(machineId)
        var expMul = 1.0; var scoreMul = 1.0; var coinMul = 1.0
        var flatExp = 0; var bonusSpins = 0
        var setExpMul = 1.0; var firstSpinExpMul = 1.0; var lastSpinExpMul = 1.0
        var rareWeightMul = 1.0; var centerExpMul = 1.0; var endsMatchExpMul = 1.0
        var adjacentSameExp = 0; var skullExp = 0; var skullPenaltyMul = 1.0
        var quotaMul = 1.0; var clearCoinBonus = 0
        // 신규 16종 per-spin 조건부 필드(evaluate 가 셀 내용으로 판정)
        var perSkullExp = 0; var skull3ScoreMul = 1.0
        var rareBurstExpMul = 1.0; var rareBurstScoreMul = 1.0
        var twoSetBonusMul = 1.0; var set3ExpMul = 1.0; var set4ScoreMul = 1.0; var perfectShapeExpMul = 1.0
        val weightMul = HashMap<String, Double>(m.weightMul)
        val weightAdd = HashMap<String, Double>(m.weightAdd)   // 머신 휴면심볼 주입
        val perSymExp = HashMap<String, Int>()
        val perSymScore = HashMap<String, Int>()
        val tagExp = HashMap<String, Int>()
        fun wmul(id: String, v: Double) { weightMul[id] = (weightMul[id] ?: 1.0) * v }
        fun wadd(id: String, v: Double) { weightAdd[id] = (weightAdd[id] ?: 0.0) + v }
        fun pse(id: String, v: Int) { perSymExp[id] = (perSymExp[id] ?: 0) + v }
        fun pss(id: String, v: Int) { perSymScore[id] = (perSymScore[id] ?: 0) + v }
        fun tag(t: String, v: Int) { tagExp[t] = (tagExp[t] ?: 0) + v }

        when (charId) {
            "novice"   -> quotaMul *= 0.92
            "scholar"  -> { pse("book", 2); clearCoinBonus += 2 }
            "parttime" -> firstSpinExpMul *= 0.8
            "farmer"   -> { pse("cherry", 1); rareWeightMul *= 0.9 }
            "jeweler"  -> pss("gem", 25)
            "cultist"  -> skullExp += 3
            "crowncol" -> { pss("crown", 30); wmul("crown", 1.5) }
            "lucky"     -> rareWeightMul *= 1.25
            "highroller"-> pss("gem", 25)
            "monk"      -> { bonusSpins -= 1; quotaMul *= 0.9 }
            "alchemist" -> { coinMul *= 1.25; clearCoinBonus += 3 }
            "daredevil" -> {   // 막판형: 기본 EXP+10%·요구+20% + 남은≤2 +35%·막스핀 +60%(막스핀이면 35% 미적용)
                expMul *= 1.1; quotaMul *= 1.2
                if (ctx.isLastSpin) expMul *= 1.6 else if (ctx.spinsLeft <= 2) expMul *= 1.35
            }
            "prodigy"   -> expMul *= 1.12
            // honor(시작 증강)·minimalist(후처리)는 별도
        }

        for (id in perkIds) when (id) {
            // 실버 증강 / 유물
            "study" -> expMul *= 1.10   // (C3) 'greed_s' 死별칭 제거(Perk 정의 없음)
            "preview" -> firstSpinExpMul *= 1.25
            "review" -> lastSpinExpMul *= 1.25
            "diligence" -> flatExp += 3
            "cherry_up" -> pse("cherry", 2)
            "book_up" -> pse("book", 2)
            "star_up" -> pse("star", 2)
            "gem_polish" -> pss("gem", 10)
            "coin_luck" -> coinMul *= 1.3
            "set_sense" -> setExpMul *= 1.3
            "lucky" -> rareWeightMul *= 1.2
            "study_tag" -> tag("학습", 4)
            // 골드 증강
            "cherry_farm" -> { pse("cherry", 4); wmul("cherry", 1.3) }
            "library" -> { pse("book", 4); tag("학습", 3) }
            "gem_invest" -> pss("gem", 25)
            "skull_study" -> skullExp += 6
            "center" -> centerExpMul *= 2.0
            "twins" -> endsMatchExpMul *= 2.0
            "chain" -> adjacentSameExp += 20
            "crown_seek" -> { wmul("crown", 2.0); pss("crown", 30) }
            "greed" -> expMul *= 1.25
            "insurance" -> bonusSpins += 1
            // 프리즘 증강
            "overdrive" -> expMul *= 1.6
            "short_day" -> { bonusSpins -= 2; expMul *= 2.2 }
            "wild_world" -> wadd("wild", 6.0)
            "seed_garden" -> wadd("seed", 5.0)
            "jackpot" -> { wadd("crown", 3.0); pss("crown", 50) }
            // 확장 증강 (조건부/리스크/시스템)
            "all_in" -> { bonusSpins -= 1; expMul *= 1.45 }
            "cram" -> { firstSpinExpMul *= 0.6; lastSpinExpMul *= 2.2 }
            "high_roller" -> { pss("gem", 30); expMul *= 0.92 }
            "all_or_nothing" -> { skullExp += 10; expMul *= 0.9 }
            "focus_fire" -> centerExpMul *= 2.5
            "symmetry" -> { endsMatchExpMul *= 2.2; adjacentSameExp += 12 }
            "crammer_tag" -> { tag("학습", 7); wmul("book", 1.4) }
            "gamblers_dice" -> { wadd("dice", 5.0); expMul *= 1.15 }
            "key_master" -> { wadd("key", 4.0); coinMul *= 1.25 }
            "glass_cannon" -> { bonusSpins -= 1; expMul *= 1.9; scoreMul *= 1.1 }
            "rich_richer" -> { coinMul *= 1.6; clearCoinBonus += 3; expMul *= 0.95 }
            "endgame_rush" -> { lastSpinExpMul *= 2.4; firstSpinExpMul *= 0.5 }
            // 확장 증강
            "deep_read" -> tag("학습", 3)
            "morning" -> firstSpinExpMul *= 1.30
            "evening" -> lastSpinExpMul *= 1.30
            "note_take" -> flatExp += 5
            "star_up2" -> pse("star", 3)
            "magnet_up" -> pse("magnet", 3)
            "gem_buff" -> pss("gem", 12)
            "combo_note" -> setExpMul *= 1.20
            "polymath" -> expMul *= 1.20
            "necromancer" -> skullExp += 8
            "bullseye" -> centerExpMul *= 1.8
            "mirror" -> endsMatchExpMul *= 1.9
            "domino" -> adjacentSameExp += 16
            "honor_student" -> tag("학습", 6)
            "lapidary" -> pss("gem", 28)
            "royal_decree" -> { wmul("crown", 1.8); pss("crown", 20) }
            "supernova" -> expMul *= 1.70
            "joker" -> wadd("wild", 5.0)
            "great_harvest" -> { wadd("seed", 5.0); pse("cherry", 3) }
            "mega_jackpot" -> { wadd("crown", 3.0); pss("crown", 40) }
            "time_warp" -> { bonusSpins += 1; expMul *= 1.20 }
            // 유물
            "old_book" -> pse("book", 3)
            "cherry_candy" -> pse("cherry", 2)
            "rusty_coin" -> coinMul *= 1.2
            "pencil" -> firstSpinExpMul *= 1.15
            "coffee" -> lastSpinExpMul *= 1.15
            "magnifier" -> rareWeightMul *= 1.15
            "star_sticker" -> pss("star", 8)
            "black_candle" -> skullExp += 4
            "gem_cert" -> pss("gem", 15)
            "clover" -> expMul *= 1.08
            "set_charm" -> setExpMul *= 1.25
            "wide_lens" -> centerExpMul *= 1.5
            // 확장 유물
            "eraser" -> pse("book", 2)
            "ruler" -> firstSpinExpMul *= 1.12
            "desk_lamp" -> lastSpinExpMul *= 1.12
            "cherry_jam" -> pse("cherry", 3)
            "bookmark" -> tag("학습", 3)
            "coin_pouch" -> coinMul *= 1.2
            "mini_scope" -> rareWeightMul *= 1.15
            "gem_dust" -> pss("gem", 10)
            "magnet_chip" -> pse("magnet", 2)
            "star_chart" -> pse("star", 2)
            "paperclip" -> setExpMul *= 1.15
            "small_candle" -> skullExp += 3
            "thick_tome" -> pse("book", 4)
            "crystal_ball" -> rareWeightMul *= 1.3
            "skull_idol" -> skullExp += 6
            "gem_tiara" -> pss("gem", 20)
            "focus_ring" -> centerExpMul *= 1.6
            "silver_mirror" -> endsMatchExpMul *= 1.7
            "iron_chain" -> adjacentSameExp += 14
            "diploma_relic" -> tag("학습", 5)
            "four_clover" -> expMul *= 1.10
            "combo_trophy" -> setExpMul *= 1.25
            "crown_jewel" -> pss("crown", 30)
            "piggy_bank" -> { coinMul *= 1.4; clearCoinBonus += 2 }
            "spare_token" -> bonusSpins += 1
            "hourglass_r" -> { firstSpinExpMul *= 1.2; lastSpinExpMul *= 1.2 }
            "battery" -> flatExp += 6
            "charm_relic" -> expMul *= 1.12
            // ── 세트 컴포넌트 (2026-06-24) ──
            "cherry_press" -> pse("cherry", 2)
            "cherry_can" -> pse("cherry", 3)
            "auto_pen" -> pse("book", 2)
            "library_card" -> { pse("book", 3); tag("학습", 3) }
            "greed_goblet" -> expMul *= 1.10
            "ominous_skull" -> skullExp += 5
            "black_report" -> skullExp += 4
            "bloody_coupon" -> { skullExp += 4; coinMul *= 1.2 }
            "crown_stand" -> pss("crown", 25)
            "broken_crown" -> pss("crown", 15)
            "kings_ledger" -> { pss("crown", 20); wmul("crown", 1.5) }
            "flame_canister" -> expMul *= 1.08
            "hot_handle" -> expMul *= 1.09
            "fate_handle" -> rareWeightMul *= 1.25
            "gamblers_eye" -> rareWeightMul *= 1.20
            "old_wallet" -> coinMul *= 1.2
            "crumpled_coupon" -> coinMul *= 1.2
            "cursed_wallet" -> { coinMul *= 1.3; skullExp += 2 }
            "practice_pad" -> pse("book", 2)
            "calculator" -> pss("gem", 12)
            "lucky_eraser" -> rareWeightMul *= 1.15
            "red_safetynet" -> pse("cherry", 2)
            "polish_work" -> pss("gem", 25)
            "greed_calc" -> expMul *= 1.15
            "overheat_formula" -> expMul *= 1.14
            // ── 신규 16종 (2026-06-29) — stage/스핀/스택/저주 조건부(ctx) + per-spin 셀판정(evaluate) ──
            // 초반성장
            "early_prep"  -> if (ctx.stage in 1..3) expMul *= 1.15                                   // S3 이하 +15%(S6+무효)
            "early_adapt" -> if (ctx.stage in 1..5) expMul *= 1.12                                   // S1~5 +12%(S6+무효)
            "growth_log"  -> firstSpinExpMul *= (1.0 + 0.08 * ctx.growthStack.coerceIn(0, 5))        // 첫스핀 +8%×스택(0~5)
            "snowball"    -> expMul *= (1.0 + 0.12 * ctx.snowStack.coerceIn(0, 4))                    // 다음스테이지 +12%×스택(0~4)
            // 운빨
            "fortune_check" -> if (ctx.isFirstSpin) rareWeightMul *= 1.2                              // 스테이지 첫스핀 희귀+20%
            "luck_accum"  -> if (ctx.unluckyGauge >= 3) rareWeightMul *= 1.3                          // 불운3+면 다음 희귀↑(확정 X)
            "fate_burst"  -> { rareBurstExpMul *= (if (ctx.boss) 1.7 else 1.8); rareBurstScoreMul *= 1.5 } // 희귀2+ 스핀 EXP/점수↑
            // 막판역전
            "late_focus"  -> if (ctx.spinsLeft in 1..2) expMul *= 1.10                                // 남은스핀2↓ +10%
            "cliff_focus" -> if (ctx.isLastSpin && ctx.quota > 0 && ctx.stageExp < (ctx.quota * 0.6).toLong())
                                 lastSpinExpMul *= 1.8                                                // EXP<요구60%&막스핀 +80%
            // fate_bell(운명의종) = 실패직전 자동 추가스핀 → 서비스 처리(run.fateBellUsed 게이트). buildMods 무효과.
            // 세트콤보
            "pair_match"   -> twoSetBonusMul *= 1.2                                                   // 2세트(bestCount==2) 보너스+20%
            "puzzle_sense" -> { set3ExpMul *= 1.25; set4ScoreMul *= 1.20 }                            // 세트3+ EXP+25%·세트4+ 점수+20%
            "perfect_shape"-> perfectShapeExpMul *= 2.2                                               // 양끝같고 가운데동계열(와일드충족 evaluate서 1.7)
            // 해골저주
            "skull_watch"  -> { perSkullExp += 2; skull3ScoreMul *= 0.9 }                             // ☠1개당 EXP+2·☠3+ 점수-10%
            "sacrifice"    -> { expMul *= (1.0 + 0.06 * ctx.curseCount); clearCoinBonus -= 1 }        // 저주1개당 EXP+6%·클코인-1
            "black_diploma"-> if (ctx.curseCount >= 5) { expMul *= 1.6; scoreMul *= 1.3; bonusSpins -= 1 } // 저주5+ EXP+60%·점수+30%·스핀-1
        }

        // 저주 — 단점+장점 동시 (★ expMul 곱폭주 회피: 보상은 quotaMul 인하/가산형으로)
        for (id in curseIds) when (id) {
            "hard_exam" -> { quotaMul *= 1.10; scoreMul *= 1.20 }
            "cursed_skulls" -> { wadd("skull", 4.0); flatExp -= 4; skullExp += 8 }
            "speed_test" -> { bonusSpins -= 1; quotaMul *= 0.78 }
            "frugal_vow" -> { coinMul *= 0.6; quotaMul *= 0.88 }
            "tunnel_vision" -> { endsMatchExpMul *= 0.5; firstSpinExpMul *= 0.85; centerExpMul *= 2.0 }
            "late_bloomer" -> { firstSpinExpMul *= 0.5; lastSpinExpMul *= 1.8 }
            "gem_obsession" -> { pse("cherry", -2); pse("book", -2); pss("gem", 35); scoreMul *= 1.10 }
            "high_stakes" -> { quotaMul *= 1.08; rareWeightMul *= 1.5 }
            "thorny_path" -> { wadd("skull", 3.0); skullExp -= 5; tag("저주", 6); clearCoinBonus += 4 }
            "hex_allornothing" -> { setExpMul *= 0.5; endsMatchExpMul *= 2.0 }
            "sleep_debt" -> { flatExp -= 5; setExpMul *= 1.40 }
            "diploma_pressure" -> { quotaMul *= 1.12; tag("학습", 5); pse("book", 2) }
            "exam_week" -> { quotaMul *= 1.12; scoreMul *= 1.25 }
            "blackout" -> { wadd("skull", 4.0); skullExp += 6; rareWeightMul *= 1.3 }
            "pop_quiz" -> { bonusSpins -= 1; rareWeightMul *= 1.4 }
            "student_debt" -> { coinMul *= 0.5; flatExp += 6 }
        }

        // 세트 효과 — perk 조합 보유 시 발동 (조건가드: reqChar/reqMachine/reqDevice)
        val pset = perkIds.toSet()
        for (s in SETS) {
            if (!pset.containsAll(s.requires)) continue
            if (s.reqChar.isNotEmpty()    && s.reqChar    != charId)    continue
            if (s.reqMachine.isNotEmpty() && s.reqMachine != machineId) continue
            if (s.reqDevice.isNotEmpty()  && s.reqDevice  != deviceId)  continue
            when (s.id) {
                // 기존 12
                "set_orchard" -> { pse("cherry", 3); wmul("cherry", 1.25) }
                "set_library" -> { pse("book", 3); tag("학습", 3) }
                "set_necro" -> skullExp += 4
                "set_appraiser" -> pss("gem", 20)
                "set_royal" -> { pss("crown", 40); wadd("crown", 2.0) }
                "set_align" -> adjacentSameExp += 10
                "set_combo" -> setExpMul *= 1.2
                "set_diurnal" -> { firstSpinExpMul *= 1.15; lastSpinExpMul *= 1.15 }
                "set_necro2" -> skullExp += 5
                "set_jewels" -> pss("gem", 20)
                "set_combo2" -> setExpMul *= 1.20
                "set_royal2" -> { pss("crown", 30); wadd("crown", 2.0) }
                // 신규 21 (조건부)
                "set_cherry_net" -> { pse("cherry", 2); pss("cherry", 12) }
                "set_red_harvest" -> { pse("cherry", 3); wmul("cherry", 1.25) }
                "set_student" -> flatExp += 4
                "set_lib_bless" -> { pse("book", 4); tag("학습", 3) }
                "set_greed" -> { scoreMul *= 1.12; coinMul *= 1.10 }
                "set_glory_grad" -> { tag("학습", 4); lastSpinExpMul *= 1.15 }
                "set_skull_lab" -> skullExp += 6
                "set_black_grad" -> { skullExp += 5; scoreMul *= 1.12 }
                "set_curse_cycle" -> setExpMul *= 1.30
                "set_crown_rite" -> { pss("crown", 40); wadd("crown", 2.0) }
                "set_kings_order" -> { pss("crown", 50); wadd("crown", 2.0) }
                "set_flame_lab" -> { pse("flame", 5); scoreMul *= 1.12 }
                "set_last_ignite" -> { lastSpinExpMul *= 1.25; scoreMul *= 1.10 }
                "set_mechanic" -> setExpMul *= 1.25
                "set_battery" -> flatExp += 6
                "set_gambler" -> { rareWeightMul *= 1.3; pss("gem", 25) }
                "set_shop_reg" -> { coinMul *= 1.20; clearCoinBonus += 3 }
                "set_scholarship" -> { tag("학습", 4); clearCoinBonus += 2 }
                "set_bomb_calc" -> { centerExpMul *= 1.5; scoreMul *= 1.10 }
                "set_perfect_calc" -> { adjacentSameExp += 14; centerExpMul *= 1.3 }
                "set_safe_grad" -> { flatExp += 3; scoreMul *= 1.08 }
            }
        }

        // 저주 빌드 임계 보너스 (저주 스택을 보상 — 고위험 빌드 살리기)
        val nCurses = curseIds.size
        if (nCurses >= 3) skullExp += 2       // 저주 3개↑: 해골 EXP+2
        if (nCurses >= 5) scoreMul *= 1.12    // 저주 5개↑: 점수 +12%
        if (nCurses >= 7) scoreMul *= 1.12    // 저주 7개↑: 점수 +12% (누적)

        // 캐릭터 후처리 (perk/저주 집합 의존)
        if (charId == "cultist" && curseIds.isNotEmpty()) scoreMul *= (1.0 + 0.08 * curseIds.size)
        if (charId == "minimalist" && perkIds.count { ALL_PERKS[it]?.cat == PCat.RELIC } <= 3) expMul *= 1.25

        return Mods(
            expMul = expMul, scoreMul = scoreMul, coinMul = coinMul,
            flatExp = flatExp, bonusSpins = bonusSpins,
            skullExp = skullExp, skullPenaltyMul = skullPenaltyMul,
            setExpMul = setExpMul, perSymbolExp = perSymExp,
            firstSpinExpMul = firstSpinExpMul, lastSpinExpMul = lastSpinExpMul,
            rareWeightMul = rareWeightMul,
            tagExpBonus = tagExp, centerExpMul = centerExpMul,
            endsMatchExpMul = endsMatchExpMul, adjacentSameExp = adjacentSameExp,
            symbolWeightMul = weightMul, weightAdd = weightAdd,
            perSymbolScore = perSymScore, quotaMul = quotaMul, clearCoinBonus = clearCoinBonus,
            perSkullExp = perSkullExp, skull3ScoreMul = skull3ScoreMul,
            rareBurstExpMul = rareBurstExpMul, rareBurstScoreMul = rareBurstScoreMul,
            twoSetBonusMul = twoSetBonusMul, set3ExpMul = set3ExpMul, set4ScoreMul = set4ScoreMul,
            perfectShapeExpMul = perfectShapeExpMul,
        )
    }

    /** 최종 점수 보정 = 머신 × 캐릭터 (난이도 보상). */
    fun scoreModifier(machineId: String, charId: String): Double = machine(machineId).scoreMod * character(charId).scoreMod

    // ── 도파민: 칭호 / 연승 ──────────────────────────────────
    /** 칭호 — 최고 점수 구간별 (이모지, 이름). 게임오버·내잭팟·랭킹 표기. */
    fun scoreTitle(best: Long): Pair<String, String> = when {
        best >= 100_000 -> "🌈" to "잭팟의 지배자"
        best >= 50_000 -> "👑" to "슬롯 마스터"
        best >= 25_000 -> "🔥" to "하이롤러"
        best >= 12_000 -> "🏅" to "슬롯 숙련자"
        best >= 6_000 -> "💰" to "단골 도박꾼"
        best >= 3_000 -> "🎲" to "도전자"
        best >= 1_000 -> "🎰" to "슬롯 입문자"
        else -> "🐣" to "잭팟 새내기"
    }
    fun titleStr(best: Long): String = scoreTitle(best).let { "${it.first}${it.second}" }

    /** 연승(연속 스테이지 클리어) 보너스 점수 — 깊을수록 가속. */
    fun streakBonus(stage: Int): Long = when {
        stage >= 15 -> 600
        stage >= 10 -> 350
        stage >= 7 -> 200
        stage >= 4 -> 100
        stage >= 2 -> 40
        else -> 0
    }

    // ── 스핀 결과 ──
    data class Cell(val sym: Sym, val tag: String = "")  // tag: 표시 보조(와일드치환/복사/성장 등)
    data class SpinResult(
        val cells: List<Cell>,
        val exp: Long,
        val score: Long,
        val coins: Int,
        val counts: Map<String, Int>,
        val tagCounts: Map<String, Int>,
        val bestSetId: String?,
        val bestSetCount: Int,
        val skulls: Int,
        val flameNext: Boolean,   // 다음 스핀 EXP -50%
        val seedNext: Boolean,    // 다음 스핀 씨앗 성장
        val jackpotSym: String?,  // 5칸 동일(잭팟) 심볼 id, 없으면 null
        val notes: List<String>,
        val preMul: Long = 0,     // 전역배수 적용 전 EXP(심볼·세트·위치 합) — 계산모드 표시
        val mul: Double = 1.0,    // 적용된 전역 expMul
        val flat: Int = 0,        // 가산 flatExp
    )

    /** 배율 표기 — 소수 2자리·끝0제거 (예 1.70→"1.7", 1.25→"1.25"). desc/노트 일관 표기용. */
    private fun fmtMul(v: Double): String = "%.2f".format(v).trimEnd('0').trimEnd('.')

    private fun weighted(rng: Random, mods: Mods): Sym {
        var total = 0.0
        val w = DoubleArray(SYMS.size)
        SYMS.forEachIndexed { i, s ->
            var x = s.weight.toDouble()
            if (s.rare) x *= mods.rareWeightMul   // 희귀심볼 = 👑왕관·🌀와일드(rare)만. ⭐별은 희귀 아님(콤보 심볼)
            x *= mods.symbolWeightMul[s.id] ?: 1.0
            x += mods.weightAdd[s.id] ?: 0.0   // 휴면심볼 주입(프리즘)
            w[i] = x; total += x
        }
        var r = rng.nextDouble() * total
        for (i in SYMS.indices) { r -= w[i]; if (r <= 0) return SYMS[i] }
        return SYMS[0]
    }

    /** 원시 셀 굴림 — 가중 추첨 + 🌱씨앗 성장. 평가 전 단계(장치 고정/재굴림/예언 재사용). */
    fun rollRaw(rng: Random, mods: Mods, reel: Int = REEL, seedActive: Boolean = false): MutableList<Cell> {
        val cells = MutableList(reel) { Cell(weighted(rng, mods)) }
        if (seedActive) {
            val grow = listOf("book", "star", "crown").random(rng)
            cells[rng.nextInt(reel)] = Cell(SYM_BY_ID.getValue(grow), "🌱→")
        }
        return cells
    }

    /** id 목록 → 원시 셀 (장치 조작 시 직전 셀 복원). */
    fun cellsFromIds(ids: List<String>): MutableList<Cell> =
        ids.mapNotNull { SYM_BY_ID[it]?.let { s -> Cell(s) } }.toMutableList()

    /** 가중 추첨 1칸 (고정/복사/교체 장치용). */
    fun rollOne(rng: Random, mods: Mods): Cell = Cell(weighted(rng, mods))

    /** NEXTSPIN '셀조작' 아이템(armIds 중 해당 토큰)을 raw에 in-place 적용(평가 직전). 값=exp+score 추정. */
    private fun cellValue(c: Cell): Int = c.sym.exp + c.sym.score
    private fun nonEmptyIdx(c: List<Cell>): List<Int> = c.indices.filter { c[it].sym.id != "empty" }
    fun applyCellOps(cells: MutableList<Cell>, armIds: List<String>, rng: Random) {
        for (id in armIds) when (id) {
            "eraser_old", "eraser_fine" -> nonEmptyIdx(cells).minByOrNull { cellValue(cells[it]) }
                ?.let { cells[it] = Cell(EMPTY_PUB, "🧽") }                                      // (a)
            "eraser_god" -> repeat(2) { nonEmptyIdx(cells).minByOrNull { cellValue(cells[it]) }
                ?.let { i -> cells[i] = Cell(EMPTY_PUB, "🧽") } }                                // (a) 최대2
            "wild_temp" -> cells[rng.nextInt(cells.size)] = Cell(SYM_BY_ID.getValue("wild"), "🌀") // (b) 랜덤1칸
            "fake_crown" -> nonEmptyIdx(cells).maxByOrNull { cellValue(cells[it]) }
                ?.let { cells[it] = Cell(SYM_BY_ID.getValue("crown"), "👑") }                    // (c) 최고가치1
            // seal_tape/skull_sticker = applyItemMods 레버 → 여기 무시
        }
    }

    /**
     * 원시 셀 → 평가(폭탄·자석·세트·잭팟·위치·페널티·배수). 장치 조작 시 재평가에 재사용.
     * @param spinIndex 0-base (0=첫스핀, spinsPerStage-1=마지막), @param flamePenalty 직전 🔥 여파
     */
    fun evaluate(
        rng: Random, raw: List<Cell>, mods: Mods, spinIndex: Int, spinsPerStage: Int,
        flamePenalty: Boolean = false,
        capMul: Double = 0.0,   // (C2) 한 스핀 총배율 상한(0=비활성). center/ends/flame/first·last/global 모든 곱 포함, 잭팟 고정가산 예외.
    ): SpinResult {
        val notes = mutableListOf<String>()
        val cells = raw.map { it.copy() }.toMutableList()
        val reel = cells.size.coerceAtLeast(1)

        // 🌱 씨앗 성장 표기
        cells.firstOrNull { it.tag == "🌱→" }?.let { notes += "🌱 씨앗→${it.sym.emoji}" }

        // 💣 폭탄: 등장한 폭탄 개수만큼 각각 양옆 제거 → EXP 환산.
        //  폭탄끼리는 안 지움, 이미 비워진 칸은 중복 제거/EXP 이중계산 방지(두 폭탄이 같은 칸을 가리켜도 1번만 +EXP).
        var bombExp = 0
        val bombIdxs = cells.indices.filter { cells[it].sym.special == Sp.BOMB }
        if (bombIdxs.isNotEmpty()) {
            val removedSet = LinkedHashSet<Int>()
            for (bi in bombIdxs) {
                for (j in intArrayOf(bi - 1, bi + 1)) {
                    if (j in 0 until reel && cells[j].sym.special != Sp.BOMB && cells[j].sym.id != "empty" && j !in removedSet) {
                        removedSet += j; cells[j] = Cell(EMPTY, "💥")
                    }
                }
            }
            val removed = removedSet.size
            bombExp = removed * BOMB_EXP_PER
            if (removed > 0) notes += "💣${if (bombIdxs.size > 1) "×${bombIdxs.size} " else " "}${removed}칸 제거 +$bombExp"
        }

        // 🧲 자석: 등장한 자석 개수만큼 각각 옆칸(왼쪽 우선→오른쪽) 실심볼 복사.
        //  복사 소스는 폭탄 처리 후·자석 적용 전 스냅샷(magSrc) 기준 → 단일 자석 시 기존 동작(폭탄 제거칸은
        //  empty 가드로 스킵) 보존하면서, 자석↔자석 연쇄복사만 차단. 자석/빈칸은 가드.
        val magIdxs = cells.indices.filter { cells[it].sym.special == Sp.MAGNET }
        if (magIdxs.isNotEmpty()) {
            val magSrc = cells.map { it.copy() }
            for (mi in magIdxs) {
                val src = listOf(mi - 1, mi + 1).filter { it in 0 until reel }
                    .map { magSrc[it] }.firstOrNull { it.sym.special == Sp.NONE && it.sym.id != "empty" }
                if (src != null) { cells[mi] = Cell(src.sym, "🧲"); notes += "🧲 ${src.sym.emoji} 복사" }
            }
        }

        // value 심볼 집계 (🌀 와일드는 최다 그룹에 합류)
        val counts = HashMap<String, Int>()
        var wilds = 0
        for (c in cells) {
            if (c.sym.special == Sp.WILD) wilds++
            else if (c.sym.id in VALUE_IDS) counts[c.sym.id] = (counts[c.sym.id] ?: 0) + 1
        }
        var bestId = counts.maxByOrNull { it.value }?.key
        if (bestId != null && wilds > 0) counts[bestId] = counts.getValue(bestId) + wilds
        else if (bestId == null && wilds > 0) { bestId = "cherry"; counts["cherry"] = wilds }
        val bestCount = bestId?.let { counts[it] } ?: 0

        // 기본 EXP/점수/코인 + 즉발 심볼효과 + 태그 집계
        // (C2) capBase = 위치/불꽃/전역배수 적용 전 '가산 baseline'(심볼·세트·인접·주사위·해골 가산 − 페널티,
        //  centerExpMul 미적용). 최종 총배율 캡 비교용. 잭팟 고정가산(jackpotFixed)은 별도 추적·캡 예외.
        var exp = 0.0; var score = 0.0; var coins = 0
        var expNoCenter = 0.0   // center 배수 미적용 누적(capBase 산출용)
        var jackpotFixed = 0.0  // 🎰 잭팟 고정가산 EXP — 총배율 캡에서 제외
        var symCoinGain = 0     // 🪙 코인심볼로 얻은 코인(스핀노트 표시용)
        var keyCount = 0        // 🗝 열쇠 개수(보물코인 가산용)
        val tagCounts = HashMap<String, Int>()
        cells.forEachIndexed { idx, c ->
            val s = c.sym
            var cellExp = (s.exp + (mods.perSymbolExp[s.id] ?: 0)).toDouble()
            for (tag in s.tags) {
                tagCounts[tag] = (tagCounts[tag] ?: 0) + 1
                cellExp += mods.tagExpBonus[tag] ?: 0
            }
            expNoCenter += cellExp
            if (idx == reel / 2) cellExp *= mods.centerExpMul          // 가운데 칸 강화
            exp += cellExp
            score += s.score + (mods.perSymbolScore[s.id] ?: 0)
            coins += s.coin
            when (s.special) {
                Sp.DICE -> { val d = rng.nextInt(1, 13); exp += d; expNoCenter += d; notes += "🎲 +$d" }
                Sp.SKULL -> { val se = mods.skullExp + mods.perSkullExp; exp += se; expNoCenter += se; score += mods.skullScoreBonus }   // 해골빌드 가산(skull_study 등) + skull_watch(perSkullExp) + 해골스티커 점수
                Sp.COIN -> symCoinGain += s.coin     // 🪙 코인심볼 — 노트로 표시(coins 가산은 위 coins += s.coin 에서 처리됨)
                Sp.KEY -> keyCount++                 // 🗝 열쇠 — 보물코인은 아래서 일괄 가산
                else -> {}
            }
        }
        exp += bombExp; expNoCenter += bombExp

        // 🗝 열쇠(KEY) — 금고머신(vault) 테마: 셀당 보물 코인 +KEY_COIN_PER. (구 keyBoost 死플래그 폐지 — 실효과 부여)
        if (keyCount > 0) {
            val keyCoins = keyCount * KEY_COIN_PER
            coins += keyCoins
            notes += "🗝 보물 +${keyCoins}🪙"
        }
        // 🪙 코인심볼 노트(coinMul 미적용 원천량 — 클리어보상과 별개로 즉시 표시)
        if (symCoinGain > 0) notes += "🪙 +${symCoinGain}🪙"
        // 🌱 씨앗(SEED) — 다음 스핀 성장 예고 노트
        if (cells.any { it.sym.special == Sp.SEED }) notes += "🌱 다음 성장↑"

        // 세트 보너스 (setExpMul 은 세트 '가산'의 일부 → capBase 에 포함, 총배율 캡 대상 아님)
        if (bestId != null && bestCount >= 2) {
            val n = bestCount.coerceAtMost(SET_EXP.size - 1)
            // pair_match: 같은심볼 정확히 2세트일 때 세트 보너스 +20%(twoSetBonusMul) — 세트 가산의 일부
            val twoMul = if (bestCount == 2) mods.twoSetBonusMul else 1.0
            val add = SET_EXP[n] * mods.setExpMul * twoMul
            exp += add; expNoCenter += add; score += SET_SCORE[n]
            notes += "${SYM_BY_ID.getValue(bestId).emoji}×$bestCount 세트 +${add.toInt()}"
            if (twoMul != 1.0) notes += "👯짝맞춤 +${((twoMul - 1.0) * 100).toInt()}%"
        }

        // 🎰 잭팟 — 전 칸 동일(와일드 포함) 심볼별 대박. 고정가산은 jackpotFixed 로 분리(총배율 캡 예외).
        var jackpotSym: String? = null
        if (bestId != null && bestCount >= reel && reel >= 5) {
            jackpotSym = bestId
            val jb = when (bestId) {
                "cherry" -> 120; "book" -> 320; "star" -> 360; "gem" -> 160; "crown" -> 520; else -> 200
            }
            jackpotFixed += jb; score += jb * 5   // 잭팟 고정가산은 곱연산 체인 밖(고정) — 캡 후 최종 가산
            notes += "🎰${SYM_BY_ID.getValue(bestId).emoji}×$bestCount 잭팟! +${jb}EXP·+${jb * 5}점"
        }

        // 인접 판정 — 붙어있는 같은 값심볼 쌍
        if (mods.adjacentSameExp != 0) {
            var pairs = 0
            for (i in 0 until reel - 1) {
                val a = cells[i].sym; val b = cells[i + 1].sym
                if (a.id == b.id && a.id in VALUE_IDS) pairs++
            }
            if (pairs > 0) { exp += pairs * mods.adjacentSameExp; expNoCenter += pairs * mods.adjacentSameExp; notes += "🔗 인접 ${pairs}쌍 +${pairs * mods.adjacentSameExp}" }
        }

        // 위치 판정 — 양끝이 같은 값심볼
        if (mods.endsMatchExpMul != 1.0 && reel >= 2) {
            val a = cells[0].sym; val b = cells[reel - 1].sym
            if (a.id == b.id && a.id in VALUE_IDS) { exp *= mods.endsMatchExpMul; notes += "↔ 양끝 ${a.emoji} EXP ×${mods.endsMatchExpMul}" }
        }

        // ☠ 해골 페널티 — 해골빌드(가산퍽 skullExp/perSkullExp 보유) 시 페널티 면제(해골=자원, +EXP는 위 Sp.SKULL서 이미 가산), 없으면 -SKULL_PENALTY/개 위험 유지
        val skulls = cells.count { it.sym.special == Sp.SKULL }
        if (skulls > 0) {
            val skullBonusPer = mods.skullExp + mods.perSkullExp
            if (skullBonusPer > 0) {
                notes += "☠ ${skulls}개 +${skullBonusPer * skulls} (해골빌드)"   // 페널티 면제 — 가산분만 표시
            } else {
                val pen = skulls * SKULL_PENALTY * mods.skullPenaltyMul
                exp -= pen; expNoCenter -= pen; if (pen > 0) notes += "☠ ${skulls}개 -${pen.toInt()}"
            }
        }

        // (C2) capBase 확정 — 위치/불꽃/전역배수 적용 직전의 가산 baseline(center 미적용, 잭팟 고정가산은 애초에 미포함).
        val capBase = expNoCenter.coerceAtLeast(0.0)

        // 🔥 불꽃: 이번 스핀 EXP +50%
        if (cells.any { it.sym.special == Sp.FLAME }) { exp *= 1.5; notes += "🔥 EXP +50%" }
        if (flamePenalty) { exp *= 0.5; notes += "🔥 여파 EXP -50%" }

        // 첫/막 스핀 배수
        if (spinIndex == 0) exp *= mods.firstSpinExpMul
        if (spinIndex == spinsPerStage - 1) exp *= mods.lastSpinExpMul

        // ── 신규 16종 per-spin 조건부 배수 (capBase 이후·전역배수 이전 → 총배율 캡 대상) ──
        // 희귀(👑왕관·🌀와일드, rare=true) 개수 — fate_burst 판정
        val rareN = cells.count { it.sym.rare }
        // 💫 fate_burst: 희귀 2개+ 스핀 EXP/점수↑ (보스전 약화는 buildMods 에서 1.7로 세팅)
        if (rareN >= 2 && mods.rareBurstExpMul != 1.0) {
            exp *= mods.rareBurstExpMul; notes += "💫운명폭발 EXP ×${fmtMul(mods.rareBurstExpMul)}"
        }
        // 🧩 puzzle_sense: 세트3+ EXP ×set3ExpMul
        if (bestCount >= 3 && mods.set3ExpMul != 1.0) {
            exp *= mods.set3ExpMul; notes += "🧩퍼즐 세트${bestCount} EXP ×${fmtMul(mods.set3ExpMul)}"
        }
        // 💠 perfect_shape: 양끝 같은 값심볼 & 가운데가 같은 계열(또는 와일드충족) → EXP↑(와일드충족 약화 1.7)
        if (mods.perfectShapeExpMul != 1.0 && reel >= 3) {
            val a = cells[0].sym; val b = cells[reel - 1].sym; val c = cells[reel / 2].sym
            val endsWild = a.special == Sp.WILD || b.special == Sp.WILD
            val endsSame = (a.id == b.id && a.id in VALUE_IDS) || (endsWild && (a.id in VALUE_IDS || b.id in VALUE_IDS))
            val endId = when { a.id in VALUE_IDS -> a.id; b.id in VALUE_IDS -> b.id; else -> null }
            val centerOk = endId != null && (c.id == endId || c.special == Sp.WILD)
            if (endsSame && centerOk) {
                val withWild = endsWild || c.special == Sp.WILD
                // 실심볼만으로 충족 = +120%(2.2배), 와일드 보조로 충족 = +70%(1.7배)
                val pm = if (withWild) 1.7 else mods.perfectShapeExpMul
                exp *= pm; notes += "💠완벽한모양 EXP ×${fmtMul(pm)}"
            }
        }

        // 전역 배수 + 고정 (잭팟 고정가산은 아직 미포함 — 곱연산 체인 밖)
        val preMulExp = exp.toLong().coerceAtLeast(0)   // 계산모드: 합산(심볼·세트·위치) 단계(전역배수 직전)
        exp = exp * mods.expMul + mods.flatExp

        // (C2) 총배율 캡 — center/ends/flame/first·last/global·setExpMul 등 모든 곱이 합쳐진 최종배율(=exp-flatExp)을
        //  capBase 대비 capMul 로 클램프. 잭팟 고정가산은 캡 적용 후 따로 더함(예외). capMul<=0 이면 캡 비활성.
        if (capMul > 0.0 && capBase > 0.0) {
            val variable = exp - mods.flatExp            // 곱 적용분(flat 가산 제외)
            val ceiling = capBase * capMul
            if (variable > ceiling) {
                exp = ceiling + mods.flatExp
                notes += "🧯총배율 캡 ×${"%.1f".format(capMul).trimEnd('0').trimEnd('.')}"
            }
        }
        // 🎰 잭팟 고정가산 — 캡 예외(곱 밖). 마지막에 그대로 가산.
        exp += jackpotFixed
        // ── 신규 16종 per-spin 점수 배수 ──
        if (rareN >= 2 && mods.rareBurstScoreMul != 1.0) score *= mods.rareBurstScoreMul     // 💫 fate_burst 점수↑
        if (bestCount >= 4 && mods.set4ScoreMul != 1.0) score *= mods.set4ScoreMul           // 🧩 puzzle_sense 세트4+ 점수↑
        if (skulls >= 3 && mods.skull3ScoreMul != 1.0) {                                     // 👁️ skull_watch ☠3+ 점수-10%
            score *= mods.skull3ScoreMul; notes += "👁️해골관찰 ☠${skulls} 점수 ×${fmtMul(mods.skull3ScoreMul)}"
        }
        score = score * mods.scoreMul + mods.flatScore
        coins = (coins * mods.coinMul).toInt() + COIN_BASE

        return SpinResult(
            cells = cells,
            exp = exp.toLong().coerceAtLeast(0),
            score = score.toLong().coerceAtLeast(0),
            coins = coins,
            counts = counts,
            tagCounts = tagCounts,
            bestSetId = bestId,
            bestSetCount = bestCount,
            skulls = skulls,
            flameNext = false,
            seedNext = cells.any { it.sym.special == Sp.SEED },
            jackpotSym = jackpotSym,
            notes = notes,
            preMul = preMulExp, mul = mods.expMul, flat = mods.flatExp,
        )
    }

    /** 단일 라인 스핀 = 굴림 + 평가. */
    fun spin(
        rng: Random, mods: Mods, spinIndex: Int, spinsPerStage: Int,
        flamePenalty: Boolean = false, seedActive: Boolean = false, reel: Int = REEL,
    ): SpinResult = evaluate(rng, rollRaw(rng, mods, reel, seedActive), mods, spinIndex, spinsPerStage, flamePenalty)

    fun spinsPerStage(mods: Mods): Int = (SPINS_PER_STAGE + mods.bonusSpins).coerceAtLeast(MIN_SPINS)

    /**
     * 특수 스핀명령 코인 비용. mode: FOCUS=1·LAST=2·PRAY=3·ALLIN=4 (그 외/N=0).
     * boss=true 면 +1, 상한 5. 비용 0(=일반 스핀)은 0 그대로 유지.
     */
    fun cmdCoinCost(mode: String, boss: Boolean): Int {
        val base = when (mode) {
            "FOCUS" -> CMD_COST_FOCUS
            "LAST"  -> CMD_COST_LAST
            "PRAY"  -> CMD_COST_PRAY
            "ALLIN" -> CMD_COST_ALLIN
            else    -> 0
        }
        if (base == 0) return 0
        val withBoss = base + (if (boss) CMD_COST_BOSS_SURCHARGE else 0)
        return withBoss.coerceAtMost(CMD_COST_MAX)
    }

    /**
     * 특수 스핀명령의 효과 설명 한 줄. 결과 메시지 상단 발동 배너용.
     * FOCUS/ALLIN/PRAY/LAST 외에는 빈 문자열.
     */
    fun cmdEffectDesc(mode: String): String = when (mode) {
        "FOCUS" -> "결과가 나쁘면 최소 EXP 보장 (대박 확률↓)"
        "ALLIN" -> "EXP ×2 (☠ 2개 이상이면 0)"
        "PRAY"  -> "불운 보정 + 낮은 확률로 기적 (×3)"
        "LAST"  -> "막판 스핀 EXP ×1.75"
        else    -> ""
    }

    fun render(cells: List<Cell>): String = cells.joinToString(" ") { "[${it.sym.emoji}]" }

    // ── 스테이지 선택 맵(노드) — 서비스 상태머신에서 사용 ──
    enum class Node { AUGMENT, RELIC, SHOP, EVENT, REST, CURSE, ELITE, GAMBLE }

    /** 스테이지 클리어 후 제시할 노드 후보 N개. (보스/엘리트 빈도는 추후 조정) */
    fun rollNodes(rng: Random, count: Int = 3): List<Node> {
        val pool = Node.values().toMutableList()
        return pool.shuffled(rng).take(count.coerceAtMost(pool.size))
    }
}
