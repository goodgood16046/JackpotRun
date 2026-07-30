package com.ashersoft.kakaobot.game

import com.ashersoft.kakaobot.App
import com.ashersoft.kakaobot.data.SlotV2RunRow
import com.ashersoft.kakaobot.data.SlotV2ScoreRow
import kotlin.random.Random

/**
 * 잭팟런 v2 — 단일라인 5칸 로그라이크 오케스트레이터 (라이브 v1 슬롯과 병행).
 *
 * 흐름: 캐릭터 선택 → 머신 선택 → (스테이지: 5스핀 안에 요구 EXP) → 노드 선택 → 반복.
 * 실패 = 게임오버 → 점수(×난이도보정) 리더보드 기록. 커맨드 "잭팟".
 * 진행 입력은 ChatMessageHandler 가 [isControlToken] 으로 걸러 [handleInput] 호출.
 *
 * ⚠️ 콘텐츠(증강/유물/아이템/저주)는 다음 단계 — 현재 노드는 휴식/도박장/이벤트(코인·점수)만.
 */
object SlotV2Service {
    private const val RUN_TTL_MS = 10 * 60_000L

    sealed interface Reply {
        /** detail: 있으면 별도 두 번째 메시지로 발송(선택지 상세 안내 — 가독성). */
        data class Msg(val text: String, val detail: String? = null) : Reply
        data object Ignore : Reply
    }

    private val PUSH = setOf("잭팟", "jackpot", "스핀", "spin")
    private val START_TOKENS = setOf("잭팟", "jackpot")
    private val NUM_WORDS = setOf("나가기", "패스", "1번", "2번", "3번", "4번", "5번", "6번")
    /** 스핀 명령어 → 모드. 특수(FOCUS/ALLIN/PRAY/LAST)는 스테이지당 1회. */
    private val SPIN_CMDS = mapOf(
        "잭팟" to "N", "jackpot" to "N", "스핀" to "N", "spin" to "N",
        "집중" to "FOCUS", "올인" to "ALLIN", "기도" to "PRAY", "최후" to "LAST",
    )
    private fun cmdLabel(mode: String): String = when (mode) {
        "FOCUS" -> "🎯집중"; "ALLIN" -> "🎲올인"; "PRAY" -> "🙏기도"; "LAST" -> "⏰최후"; else -> "잭팟"
    }
    private fun spinCmdHelp(boss: Boolean = false): String =
        "🎯집중=폭망방지(최소보장·고점↓) · 🎲올인=대박EXP×2(해골2개↑면 0) · 🙏기도=불운보정+희박한기적×3 · ⏰최후=마지막스핀 ×1.75 (특수는 스테이지당 1회)\n${spinCmdCostHint(boss)}"
    /** 특수 스핀명령 코인 비용 안내 한 줄 — 현재 스테이지 boss 여부로 가산 반영. 일반 스핀은 무료. */
    private fun spinCmdCostHint(boss: Boolean = false): String =
        "🪙비용: 🎯집중 ${SlotV2Engine.cmdCoinCost("FOCUS", boss)}🪙·⏰최후 ${SlotV2Engine.cmdCoinCost("LAST", boss)}🪙·🙏기도 ${SlotV2Engine.cmdCoinCost("PRAY", boss)}🪙·🎲올인 ${SlotV2Engine.cmdCoinCost("ALLIN", boss)}🪙 (보스 +1) · 일반 스핀 무료"
    private val DEVICE_CMDS: Set<String> = SlotV2Engine.DEVICE_CMD_SET
    private val GIVEUP = setOf("포기", "넘어가기", "그만")
    private val DISPLAY_CMDS = mapOf("간단" to "SIMPLE", "상세" to "NORMAL", "보통" to "NORMAL", "계산" to "CALC", "고급" to "CALC")
    private const val ITEM_SLOTS = 3                       // 🎒 아이템 보유칸
    private val ITEM_CMDS = setOf("아이템", "가방", "사용", "인벤")
    /** 세트/인접 집계 대상 값심볼 (엔진 VALUE_IDS 와 동일 — 인접쌍·세트4 판정용). */
    private val VALUE_SYM_IDS = setOf("cherry", "book", "star", "gem", "crown")

    fun ownerKeyFor(userId: Long?, nick: String): String =
        if (userId != null && userId > 0L) "u$userId" else "n$nick"

    /** 선행 "." 제거 + 공백 정리 (".점화"·"점화" 모두 허용). */
    private fun norm(text: String): String = text.trim().removePrefix(".").trim()
    /** 명령 단어만 추출 ("고정 3"·"고정3" → "고정"). */
    private fun cmdOf(s: String): String = s.takeWhile { !it.isDigit() && it != ' ' }
    /** 인자 숫자 추출 ("고정 3"·"고정3" → 3, 없으면 null). */
    private fun argOf(s: String): Int? = Regex("(\\d+)").find(s)?.value?.toIntOrNull()

    fun isControlToken(text: String): Boolean {
        val s = norm(text)
        val cmd = cmdOf(s)
        if (s in SPIN_CMDS || cmd in DEVICE_CMDS || cmd in ITEM_CMDS || s in NUM_WORDS || s in GIVEUP || s in DISPLAY_CMDS ||
            s == "리롤" || s == "새로고침" || s == "상태" || s == "잭팟상태") return true
        val n = s.toIntOrNull()
        return n != null && n in 0..9
    }

    private fun rng(): Random = Random(System.nanoTime())
    /** 신규 증강(ctx 조건부)용 RunCtx 구성 — spinIndex/스핀수는 호출부 값, 나머지는 run 상태. */
    private fun runCtxOf(run: SlotV2RunRow, spinIndex: Int, spinsPerStage: Int, quota: Long): SlotV2Engine.RunCtx =
        SlotV2Engine.RunCtx(
            stage = run.stage, spinIndex = spinIndex, spinsPerStage = spinsPerStage,
            stageExp = run.stageExp, quota = quota,
            growthStack = run.growthStack, snowStack = run.snowStack,
            curseCount = curseCount(run), unluckyGauge = run.unluckyGauge,
            boss = SlotV2Engine.bossFor(run.stage) != null,
        )
    /** 요구 EXP = 스테이지 기본 × quotaMul × 보스 quotaMul × (보스 스핀증가 비례). */
    private fun qOf(stage: Int, mods: SlotV2Engine.Mods): Long {
        val baseSpins = SlotV2Engine.spinsPerStage(mods)
        val bsp = SlotV2Engine.bossSpins(stage)
        val prop = if (bsp > 0 && baseSpins > 0) (baseSpins + bsp).toDouble() / baseSpins else 1.0
        return (SlotV2Engine.quota(stage) * mods.quotaMul * SlotV2Engine.bossQuotaMul(stage) * prop).toLong()
    }
    /** 이번 스테이지 실제 스핀 수 = 기본 + 스테이지보너스(아이템) + 보스 추가. */
    private fun effSpins(run: SlotV2RunRow, mods: SlotV2Engine.Mods): Int =
        (SlotV2Engine.spinsPerStage(mods) + run.stageBonusSpins + SlotV2Engine.bossSpins(run.stage)).coerceAtLeast(SlotV2Engine.MIN_SPINS)
    /** 보스 스핀 효과 — (조정EXP, 표기).
     *  @param expectedPerSpin 이번 스테이지 균등 페이스(quota/spins) — 졸업심사(grad) 꾸준함 판정용.
     *  @param augCount 보유 증강 수 — 졸업심사 빈약빌드 페널티 판정용. */
    private fun applyBoss(boss: SlotV2Engine.Boss, gained: Long, res: SlotV2Engine.SpinResult, spinIndex: Int, spins: Int,
                          expectedPerSpin: Double = 0.0, augCount: Int = 0): Pair<Long, String> = when (boss.id) {
        "finals" -> if (spinIndex == spins - 1) (gained * 2) to " · 📝기말 막스핀×2" else if (spinIndex == 0) (gained * 9 / 10) to " · 📝기말 첫스핀-10%" else gained to ""
        "strict" -> if (res.bestSetCount < 3) (gained / 2) to " · 👨‍🏫콤보없음 ×0.5" else gained to ""
        "luck" -> if (res.cells.any { it.sym.id in setOf("star", "crown", "wild") }) (gained * 18 / 10) to " · 🎲희귀 ×1.8" else (gained * 8 / 10) to " · 🎲노희귀 ×0.8"
        // (C5) 졸업심사 — "EXP 총량(과부하·탐욕)" 테마. 꾸준함 요구: 균등페이스(70%) 미달 스핀은 ×0.85.
        //  단, 빌드가 빈약(증강 3개 미만)하면 페널티 강화(×0.75) — 증강 쌓기/꾸준한 스핀으로 대응 가능(단일정답 회피).
        "grad" -> {
            val pace = expectedPerSpin * 0.7
            if (expectedPerSpin > 0.0 && gained < pace) {
                if (augCount < 3) (gained * 75 / 100) to " · 🎓빈약빌드 ×0.75" else (gained * 85 / 100) to " · 🎓꾸준함부족 ×0.85"
            } else gained to ""
        }
        else -> gained to ""
    }
    private fun fmt(n: Long): String = "%,d".format(n)
    private fun fmt(n: Int): String = "%,d".format(n)
    /** 소수점 최대 2자리(끝 0 제거). 점수보정 등 배율 표기에 사용. */
    private fun fmt2(d: Double): String = "%.2f".format(d).trimEnd('0').trimEnd('.')
    private const val DIV = "━━━━━━━━━━"

    private fun perkList(run: SlotV2RunRow): List<String> = run.perks.split(",").filter { it.isNotBlank() }
    private fun curseList(run: SlotV2RunRow): List<String> = run.curses.split(",").filter { it.isNotBlank() }
    private fun curseCount(run: SlotV2RunRow): Int = curseList(run).size
    /** (k) 깨진프리즘 — 이번 스테이지 한정 임시 perk(클리어 시 소거). buildMods 합산용. */
    private fun phasePerkList(run: SlotV2RunRow): List<String> = run.phasePerks.split(",").filter { it.isNotBlank() }

    // ── (P7) 장착 장치 보조판정 — 메인(device) ∪ 보조(device2) ──
    private fun hasDevice(run: SlotV2RunRow, id: String): Boolean = run.device == id || run.device2 == id
    /** (P7·dev_major) 전공 편향 계열 — 장착 시 보유 perk 기준 주력 심볼(없으면 null). pickPerksByTier favoredCat 인자. */
    private fun majorFavoredCat(run: SlotV2RunRow): String? =
        if (hasDevice(run, "dev_major")) SlotV2Engine.favoredSymbol(perkList(run).toSet()) else null

    private suspend fun findActiveRun(linkId: Long, userId: Long?, nick: String): SlotV2RunRow? {
        val dao = App.db.slotV2Run()
        dao.find(linkId, ownerKeyFor(userId, nick))?.let { return it }
        if (userId != null && userId > 0L) {
            dao.findByUserId(linkId, userId)?.let { return it }
            dao.find(linkId, "n$nick")?.let { return it }
        }
        return null
    }

    // ── 진입 ────────────────────────────────────────────────
    suspend fun command(linkId: Long, nick: String, userId0: Long?, args: String): String? {
        val userId = resolveUid(linkId, nick, userId0)   // (#12) canonical uid — 댓글경로 0/null 보강
        val input = args.trim().ifEmpty { "잭팟" }
        return when (val r = handleInput(linkId, nick, userId, input)) {
            is Reply.Msg -> r.text
            Reply.Ignore -> {
                val run = findActiveRun(linkId, userId, nick)
                if (run == null) null else promptFor(run)  // 현재 선택지/상태 다시 보여주기
            }
        }
    }

    suspend fun handleInput(linkId: Long, nick: String, userId0: Long?, text: String): Reply {
        val userId = resolveUid(linkId, nick, userId0)   // (#12) canonical uid — run.ownerUserId 가 canonical 되게
        val t = norm(text)
        val now = System.currentTimeMillis()
        runCatching { App.db.slotV2Run().purgeExpired(now - RUN_TTL_MS) }
        val run0 = findActiveRun(linkId, userId, nick)
        if (run0 == null || now - run0.lastActionAt >= RUN_TTL_MS) {
            if (run0 != null) App.db.slotV2Run().delete(run0.linkId, run0.ownerKey)
            return if (t in START_TOKENS) startRun(linkId, nick, userId) else Reply.Ignore
        }
        val run = if (run0.ownerNick != nick) run0.copy(ownerNick = nick) else run0
        // .상태 — 현재 진행 상황 (어느 단계든)
        if (t == "상태" || t == "잭팟상태") return Reply.Msg(statusReply(run))
        // 표시 모드 전환 (간단/상세)
        DISPLAY_CMDS[t]?.let { mode ->
            App.db.slotV2Run().upsert(run.copy(displayMode = mode, lastActionAt = now))
            val label = when (mode) { "SIMPLE" -> "🔹간단 (결과만)"; "CALC" -> "🧮계산 (합산×배율 표시)"; else -> "🔸상세 (효과 표시)" }
            return Reply.Msg("@${run.ownerNick} 표시 모드 → $label  ·  전환: \"간단\"/\"상세\"/\"계산\"")
        }
        val cmd = cmdOf(t)
        // 🎒 아이템 — 목록 보기는 어디서든, 사용(아이템 N)은 handleItem서 SPIN만 허용
        if (cmd in ITEM_CMDS) return handleItem(run, t)
        // 🌐 웹에서 캐릭/머신 골랐으면(jackpotcmd) 선택 단계 건너뛰고 바로 시작 (DEVICE_SELECT2 보조단계는 웹 미지원)
        if (run.state == "CHAR_SELECT" || run.state == "MACHINE_SELECT" || run.state == "DEVICE_SELECT") {
            val wp = runCatching { SlotV2WebService.consumeWebPick(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 }) }.getOrNull()
            if (wp != null) {
                val (cId, mId, dId) = wp
                val stat = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
                if (cId.isNotEmpty() && mId.isNotEmpty() &&
                    SlotV2Engine.charUnlocked(SlotV2Engine.character(cId), stat) &&
                    SlotV2Engine.machineUnlocked(SlotV2Engine.machine(mId), stat)
                ) return proceedAfterMachine(run, cId, mId, "🌐웹선택 ", dId)
            }
        }
        // 선택 단계에서 스핀명령 재입력 = "헷갈려, 다시 보여줘" → 현재 선택지 재표시
        if (t in SPIN_CMDS && run.state != "SPIN" && run.state != "POST_SPIN") return Reply.Msg(promptFor(run))
        return when (run.state) {
            "CHAR_SELECT" -> handleCharSelect(run, t)
            "MACHINE_SELECT" -> handleMachineSelect(run, t)
            "DEVICE_SELECT" -> handleDeviceSelect(run, t)
            "DEVICE_SELECT2" -> handleDeviceSelect2(run, t)
            "SPIN" -> if (cmd in DEVICE_CMDS) handleDevice(run, t) else handleSpin(run, t)
            "POST_SPIN" -> handlePostSpin(run, t)
            "NODE_SELECT" -> handleNodeSelect(run, t)
            // (P7) 증강 선택 중 보조장치 명령 — 🗂️보류(보관)·🔁재추첨(재뽑기). 그 외 번호는 선택.
            "EVENT_AUGMENT", "EVENT_RELIC" -> when (cmd) {
                "보류" -> handleHoldAug(run, t)
                "재추첨" -> handleRetake(run)
                else -> handlePerkPick(run, t)
            }
            "EVENT_SHOP" -> handleShop(run, t)
            else -> Reply.Ignore
        }
    }

    private fun parseChoice(t: String): Int? = when (t) {
        "0", "나가기", "패스" -> 0
        "1", "1번" -> 1; "2", "2번" -> 2; "3", "3번" -> 3
        "4", "4번" -> 4; "5", "5번" -> 5; "6", "6번" -> 6
        else -> t.toIntOrNull()
    }

    /** 현재 상태의 선택지/안내를 다시 렌더 (헷갈릴 때 재표시용). */
    private fun promptFor(run: SlotV2RunRow): String = when (run.state) {
        "CHAR_SELECT" -> buildString {
            append("🎰 캐릭터 선택 @${run.ownerNick}\n")
            val offered = run.pendingOptions.split(",").filter { it.isNotBlank() }.map { SlotV2Engine.character(it) }
            offered.forEachIndexed { i, c -> append("${i + 1}️⃣ ${c.emoji}${c.name} — ${c.desc}\n") }
            append("👉 댓글로 \"1\"~\"${offered.size}\" 선택")
        }
        "MACHINE_SELECT" -> buildString {
            append("🎰 슬롯머신 선택 @${run.ownerNick}\n")
            val offered = run.pendingOptions.split(",").filter { it.isNotBlank() }.map { SlotV2Engine.machine(it) }
            offered.forEachIndexed { i, m -> append("${i + 1}️⃣ ${m.emoji}${m.name} — ${m.desc} (점수×${fmt2(m.scoreMod)})\n") }
            append("👉 댓글로 \"1\"~\"${offered.size}\" 선택")
        }
        "DEVICE_SELECT" -> deviceSelectText(run.ownerNick, run.pendingOptions.split(",").filter { it.isNotBlank() }.mapNotNull { SlotV2Engine.device(it) })
        "DEVICE_SELECT2" -> deviceSelect2Text(run.ownerNick, SlotV2Engine.device(run.device),
            run.pendingOptions.split(",").filter { it.isNotBlank() }.mapNotNull { SlotV2Engine.device(it) })
        "NODE_SELECT" -> "@${run.ownerNick}\n" + nodeText(run)
        "EVENT_AUGMENT" -> "@${run.ownerNick} " + perkPickText("AUGMENT",
            run.pendingOptions.split(",").filter { it.isNotBlank() }.mapNotNull { SlotV2Engine.perk(it) }, perkList(run).toSet()) + perkAuxHint(run, "AUGMENT")
        "EVENT_RELIC" -> "@${run.ownerNick} " + perkPickText("RELIC",
            run.pendingOptions.split(",").filter { it.isNotBlank() }.mapNotNull { SlotV2Engine.perk(it) }, perkList(run).toSet())
        "EVENT_SHOP" -> "@${run.ownerNick} " + shopText(run)
        "SPIN" -> "@${run.ownerNick} ${stageGoalLine(run)}\n👉 댓글로 \"잭팟\" 스핀\n💡 ${spinCmdHelp(SlotV2Engine.bossFor(run.stage) != null)}" + deviceFooter(run)
        "POST_SPIN" -> {
            val dev = SlotV2Engine.device(run.device)
            "@${run.ownerNick} 💀 마지막 스핀 실패! ${dev?.let { "🔧 ${it.emoji}${it.name}(\"${it.cmd}${if (it.needsArg) " N" else ""}\")로 만회 가능 — ${it.desc}" } ?: ""}\n👉 장치 쓰거나 \"포기\""
        }
        else -> "🎰 진행 중 — 댓글로 \"잭팟\""
    }

    // ── 시작 → 캐릭터 선택 ──────────────────────────────────
    suspend fun startRun(linkId: Long, nick: String, userId: Long?): Reply {
        val now = System.currentTimeMillis()
        val existing = findActiveRun(linkId, userId, nick)
        if (existing != null && now - existing.lastActionAt < RUN_TTL_MS) {
            return Reply.Msg("🎰 이미 진행 중! \"잭팟\" 으로 계속하거나 번호 선택해줘.")
        }
        if (existing != null) App.db.slotV2Run().delete(existing.linkId, existing.ownerKey)
        val sc = myScore(linkId, nick, userId)
        val ach = myAch(linkId, nick, userId)
        val stat = composeStat(ach, sc)
        val runs = sc?.runs ?: 0

        // 첫 런 — 캐릭터·머신 선택 없이 🎒초보학생 + 🎰기본 고정, 바로 스핀 (둘 다 차근차근 해금).
        if (runs == 0) {
            val run = SlotV2RunRow(
                linkId = linkId, ownerKey = ownerKeyFor(userId, nick), ownerNick = nick,
                ownerUserId = userId ?: 0L, state = "SPIN", charId = "novice", machineId = "basic",
                coins = SlotV2Engine.character("novice").startCoins.toLong(), stage = 1,
                startedAt = now, lastActionAt = now,
            )
            App.db.slotV2Run().upsert(run)
            val mods = SlotV2Engine.buildMods("basic", "novice")
            val quota = qOf(1, mods)
            val spins = effSpins(run, mods)
            return Reply.Msg(buildString {
                append("🎰 잭팟런 v3 첫 도전 @$nick!\n")
                append("🎒초보학생 + 🎰기본 슬롯으로 시작 (캐릭터·머신은 플레이하며 차근차근 해금!)\n")
                append("5칸 슬롯을 5번 돌려 요구 ⭐EXP를 넘기면 다음 스테이지! (못 넘으면 끝)\n")
                append("💬 모든 진행은 봇 메시지에 **댓글(답글)** 로!\n")
                append("🎯 스테이지1: ${spins}스핀 안에 ${fmt(quota)}EXP\n")
                append("${spinCmdCostHint(SlotV2Engine.bossFor(1) != null)}\n")
                append("👉 댓글로 \"잭팟\" 스핀!  ·  📖 처음이면 \"잭팟튜토리얼\" (요약은 \"잭팟도움말\")")
            }, "📖 잭팟런이 처음이라면 \"잭팟튜토리얼\" 로 단계별 설명을 봐! (요약만 빠르게는 \"잭팟도움말\")\n· 5칸을 5번 돌려 ⭐EXP 목표를 넘기면 클리어\n· 클리어하면 증강/유물로 점점 강해져\n· 못 넘기면 게임오버 → 점수 기록\n· 스핀은 \"잭팟\"이 기본 — 안 풀리면 \"집중\"(최소 보장)·\"올인\"(EXP×2 도박)도 있어!")
        }

        // 🌐 웹 선택 핸드셰이크 — 웹에서 캐릭+머신 골랐으면(jackpotcmd, 랜덤 쓰기토큰+cid) 바로 시작
        val webPick = runCatching { SlotV2WebService.consumeWebPick(linkId, nick, userId) }.getOrNull()
        if (webPick != null) {
            val (cId, mId, dId) = webPick
            val ch = SlotV2Engine.character(cId); val m = SlotV2Engine.machine(mId)
            if (cId.isNotEmpty() && mId.isNotEmpty() &&
                SlotV2Engine.charUnlocked(ch, stat) && SlotV2Engine.machineUnlocked(m, stat)
            ) {
                val base = SlotV2RunRow(
                    linkId = linkId, ownerKey = ownerKeyFor(userId, nick), ownerNick = nick,
                    ownerUserId = userId ?: 0L, startedAt = now, lastActionAt = now,
                )
                App.db.slotV2Run().upsert(base)
                return proceedAfterMachine(base, cId, mId, "🌐웹선택 ", dId)
            }
        }

        val offered = SlotV2Engine.unlockedChars(stat)
        val locked = SlotV2Engine.lockedChars(stat)
        val run = SlotV2RunRow(
            linkId = linkId, ownerKey = ownerKeyFor(userId, nick), ownerNick = nick,
            ownerUserId = userId ?: 0L, state = "CHAR_SELECT",
            pendingOptions = offered.joinToString(",") { it.id },
            startedAt = now, lastActionAt = now,
        )
        App.db.slotV2Run().upsert(run)
        val webLink = runCatching { SlotV2WebService.linkPick(linkId, nick, userId) }.getOrNull()
        return Reply.Msg(buildString {
            append("🎰 잭팟런 v3 — 시작 @$nick\n")
            if (webLink != null) append("🎮 웹에서 캐릭터+머신 골라 시작 ▶\n$webLink\n")
            append("또는 💬 댓글로 번호 선택 👇\n")
            equipableDeviceList(sc, stat).size.let { if (it > 0) append("🔧 장착 가능 장치 ${it}개 — 캐릭·머신 고른 뒤 🔧장착 단계가 떠요!\n") }
            offered.forEachIndexed { i, c -> append("${i + 1}️⃣ ${c.emoji}${c.name} — ${c.desc}\n") }
            locked.forEach { c -> append("🔒 ${c.emoji}${c.name} — 해금: ${SlotV2Engine.charHint(c, stat)}\n") }
            append("👉 \"1\"~\"${offered.size}\" 선택 (진행은 봇 메시지에 댓글로!)")
        }, charDetailText(offered))
    }

    /**
     * 같은 조합 재도전 (지시서11-B) — 직전 런 조합(lastCombo CSV "char,machine,device,device2")으로 즉시 새 런 시작.
     * 캐릭/머신 해금·장치 면허(또는 grandfather 보유)가 여전히 유효해야 그 조합으로 launchRun. 진행중 런 있으면 거부.
     * lastCombo 없거나 무효(미해금 등)면 일반 시작 안내.
     */
    /** "같은조합"/"잭팟재도전" 명령 진입점 — Reply.text 만 반환(command() 패턴과 동일). */
    suspend fun restartSameCombo(linkId: Long, nick: String, userId: Long?): String =
        (restartSameComboReply(linkId, nick, userId) as? Reply.Msg)?.text
            ?: "🎰 @$nick 직전 런 조합이 없어요. \"잭팟\"·\"잭팟선택\"으로 시작해줘!"

    private suspend fun restartSameComboReply(linkId: Long, nick: String, userId0: Long?): Reply {
        val userId = resolveUid(linkId, nick, userId0)   // (#12) canonical uid — 댓글경로 0/null 보강
        val now = System.currentTimeMillis()
        val existing = findActiveRun(linkId, userId, nick)
        if (existing != null && now - existing.lastActionAt < RUN_TTL_MS)
            return Reply.Msg("🎰 이미 진행 중! \"잭팟\" 으로 계속하거나 끝낸 뒤 다시 시도해줘.")
        if (existing != null) App.db.slotV2Run().delete(existing.linkId, existing.ownerKey)

        val sc = myScore(linkId, nick, userId)
        val ach = myAch(linkId, nick, userId)
        val stat = composeStat(ach, sc)
        val combo = (sc?.lastCombo ?: "").split(",")
        val charId = combo.getOrNull(0)?.takeIf { it.isNotBlank() }
        val machineId = combo.getOrNull(1)?.takeIf { it.isNotBlank() }
        val devId = combo.getOrNull(2)?.takeIf { it.isNotBlank() }
        val dev2Id = combo.getOrNull(3)?.takeIf { it.isNotBlank() }
        if (charId == null || machineId == null)
            return Reply.Msg("🎰 @$nick 직전 런 조합이 없어요. \"잭팟\"(빠른시작) 또는 \"잭팟선택\"(웹/번호 선택)으로 시작해줘!")

        val ch = SlotV2Engine.character(charId); val m = SlotV2Engine.machine(machineId)
        if (!SlotV2Engine.charUnlocked(ch, stat) || !SlotV2Engine.machineUnlocked(m, stat))
            return Reply.Msg("🎰 @$nick 직전 조합(${ch.emoji}${ch.name}+${m.emoji}${m.name})이 지금은 해금 상태가 아니라 재도전할 수 없어요. \"잭팟\"·\"잭팟선택\"으로 시작해줘!")

        // 장치는 면허/grandfather 로 여전히 장착 가능한 것만 인정(무효면 그 슬롯만 비움 — 런 자체는 진행)
        val equipIds = equipableDeviceList(sc, stat).map { it.id }.toSet()
        val dev = devId?.takeIf { it in equipIds } ?: ""
        val dev2 = dev2Id?.takeIf { it != dev && it in equipIds } ?: ""
        val dropped = buildString {
            if (devId != null && dev.isEmpty()) SlotV2Engine.device(devId)?.let { append(" (🔧${it.emoji}${it.name} 면허 만료로 미장착)") }
            if (dev2Id != null && dev2.isEmpty()) SlotV2Engine.device(dev2Id)?.let { append(" (🔧${it.emoji}${it.name}(보조) 미장착)") }
        }

        val base = SlotV2RunRow(
            linkId = linkId, ownerKey = ownerKeyFor(userId, nick), ownerNick = nick,
            ownerUserId = userId ?: 0L, charId = charId, machineId = machineId,
            device = dev, device2 = dev2, startedAt = now, lastActionAt = now,
        )
        App.db.slotV2Run().upsert(base)
        return launchRun(base, charId, machineId, "🔁같은 조합 재도전$dropped ")
    }

    private suspend fun handleCharSelect(run: SlotV2RunRow, t: String): Reply {
        val c = parseChoice(t) ?: return Reply.Ignore
        val offered = run.pendingOptions.split(",").filter { it.isNotBlank() }.map { SlotV2Engine.character(it) }
        if (c < 1 || c > offered.size) return Reply.Ignore
        val ch = offered[c - 1]
        val stat = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
        val mOffered = SlotV2Engine.unlockedMachines(stat)
        val mLocked = SlotV2Engine.lockedMachines(stat)
        val run2 = run.copy(
            charId = ch.id, state = "MACHINE_SELECT",
            pendingOptions = mOffered.joinToString(",") { it.id },
            lastActionAt = System.currentTimeMillis(),
        )
        App.db.slotV2Run().upsert(run2)
        return Reply.Msg(buildString {
            append("@${run.ownerNick} ${ch.emoji}${ch.name} 선택! 🎰 슬롯머신을 골라줘\n")
            mOffered.forEachIndexed { i, m -> append("${i + 1}️⃣ ${m.emoji}${m.name} — ${m.desc} (점수×${fmt2(m.scoreMod)})\n") }
            mLocked.forEach { m -> append("🔒 ${m.emoji}${m.name} — 해금: ${SlotV2Engine.machineHint(m, stat)}\n") }
            append("👉 \"1\"~\"${mOffered.size}\" 선택")
        }, machineDetailText(mOffered))
    }

    private suspend fun handleMachineSelect(run: SlotV2RunRow, t: String): Reply {
        val c = parseChoice(t) ?: return Reply.Ignore
        val offered = run.pendingOptions.split(",").filter { it.isNotBlank() }.map { SlotV2Engine.machine(it) }
        if (c < 1 || c > offered.size) return Reply.Ignore
        return proceedAfterMachine(run, run.charId, offered[c - 1].id, "")
    }

    /**
     * 머신 확정 후 — 영구 소지 장치 있으면 장착 단계(DEVICE_SELECT), 없으면 바로 시작. 인챗·웹선택 공용.
     * webDevice != null 이면 웹에서 장치까지 골랐다는 뜻 → DEVICE_SELECT 건너뛰고 바로 장착+시작
     * ("" = 웹에서 '장치 없이' 선택). null 이면 인챗 흐름(소지 장치 있으면 DEVICE_SELECT).
     */
    private suspend fun proceedAfterMachine(run: SlotV2RunRow, charId: String, machineId: String, prefix: String, webDevice: String? = null): Reply {
        val sc = myScore(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
        val stat = composeStat(myAch(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 }), sc)
        val owned = equipableDeviceList(sc, stat)
        // 🌐 웹에서 장치까지 선택함 → 검증 후 바로 장착하고 시작 (소지한 것만 인정)
        if (webDevice != null) {
            val dev = if (webDevice.isNotBlank()) owned.firstOrNull { it.id == webDevice } else null
            val dpre = dev?.let { "🔧${it.emoji}${it.name} 장착 " } ?: ""
            return launchRun(run.copy(charId = charId, machineId = machineId, device = dev?.id ?: ""), charId, machineId, prefix + dpre)
        }
        if (owned.isEmpty()) return launchRun(run, charId, machineId, prefix)
        val run2 = run.copy(charId = charId, machineId = machineId, device = "", state = "DEVICE_SELECT",
            pendingOptions = owned.joinToString(",") { it.id }, lastActionAt = System.currentTimeMillis())
        App.db.slotV2Run().upsert(run2)
        val pre = if (prefix.isNotEmpty()) "${prefix}캐릭·머신 선택됨!\n" else ""
        return Reply.Msg(pre + deviceSelectText(run.ownerNick, owned))
    }

    private fun deviceSelectText(nick: String, owned: List<SlotV2Engine.Device>): String = buildString {
        append("@$nick 🔧 메인 장치 장착 (면허 취득 — 이번 런에 쓸 1개 선택)\n")
        owned.forEachIndexed { i, d ->
            val how = if (d.kind == SlotV2Engine.DevKind.PASSIVE || d.cmd.isEmpty()) "패시브·자동" else "능동·\"${d.cmd}\""
            append("${i + 1}️⃣ ${d.emoji}${d.name} [$how] — ${d.desc}\n")
        }
        append("0️⃣ 장착 안 함\n👉 \"0\"~\"${owned.size}\" 선택")
    }

    private fun deviceSelect2Text(nick: String, main: SlotV2Engine.Device?, cands: List<SlotV2Engine.Device>): String = buildString {
        val mainTxt = main?.let { "${it.emoji}${it.name}" } ?: "없음"
        append("@$nick 🔧🔧 보조 장치 슬롯 해금! (메인 🔧$mainTxt)\n")
        append("보조는 능동(⚡장전·🔮예언) 계열만·메인과 다른 장치/계열·효과 ${fmt2((1 - SlotV2Engine.SECONDARY_MUL) * 100)}% 약화\n")
        cands.forEachIndexed { i, d ->
            val weak = if (d.kind == SlotV2Engine.DevKind.ARMED) " (보조 약화)" else ""
            val tag = if (d.cmd.isNotEmpty()) "능동·\"${d.cmd}\"" else "자동·정보형"   // dev_syllabus(PEEK·무cmd) 등 대응
            append("${i + 1}️⃣ ${d.emoji}${d.name} [$tag]$weak — ${d.desc}\n")
        }
        append("0️⃣ 보조 없이 시작\n👉 \"0\"~\"${cands.size}\" 선택")
    }

    /** SPIN 안내 푸터 — 메인+보조 장치 둘 다 표기(능동 명령어 안내). */
    private fun deviceFooter(run: SlotV2RunRow): String = buildString {
        SlotV2Engine.device(run.device)?.let { append("\n🔧 ${it.emoji}${it.name}: ${if (it.cmd.isNotEmpty()) "\"${it.cmd}\"" else "(자동)"} — ${it.desc}") }
        SlotV2Engine.device(run.device2)?.let { append("\n🔧 ${it.emoji}${it.name}(보조): ${if (it.cmd.isNotEmpty()) "\"${it.cmd}\"" else "(자동)"} — ${it.desc} (효과 약화)") }
    }

    private suspend fun handleDeviceSelect(run: SlotV2RunRow, t: String): Reply {
        val owned = run.pendingOptions.split(",").filter { it.isNotBlank() }.mapNotNull { SlotV2Engine.device(it) }
        val c = parseChoice(t) ?: return Reply.Ignore
        if (c == 0) return offerSecondaryOrLaunch(run.copy(device = ""), "")
        if (c < 1 || c > owned.size) return Reply.Ignore
        val dev = owned[c - 1]
        return offerSecondaryOrLaunch(run.copy(device = dev.id), "🔧${dev.emoji}${dev.name} 장착 ")
    }

    /**
     * 메인 장치 확정 후 — 보조 슬롯이 해금됐고(slot2Unlocked) 보조 후보(ARMED/PEEK·메인과 다른 장치·다른 계열)가 1개↑면
     * 보조 슬롯 선택 단계(DEVICE_SELECT2)로, 아니면 바로 시작. run.device 는 이미 메인으로 세팅된 상태로 호출.
     */
    private suspend fun offerSecondaryOrLaunch(run: SlotV2RunRow, prefix: String): Reply {
        // 메인 미장착(device="")이면 보조 슬롯 제안 없음(보조는 메인 위에 얹는 슬롯).
        val stat = if (run.device.isNotBlank()) playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 }) else emptyMap()
        val cands = if (run.device.isNotBlank() && SlotV2Engine.slot2Unlocked(stat)) SlotV2Engine.secondaryCandidates(run.device, stat) else emptyList()
        if (cands.isEmpty()) return launchRun(run.copy(device2 = ""), run.charId, run.machineId, prefix)
        val run2 = run.copy(device2 = "", state = "DEVICE_SELECT2",
            pendingOptions = cands.joinToString(",") { it.id }, lastActionAt = System.currentTimeMillis())
        App.db.slotV2Run().upsert(run2)
        val pre = if (prefix.isNotEmpty()) "@${run.ownerNick} $prefix·\n" else ""
        return Reply.Msg(pre + deviceSelect2Text(run.ownerNick, SlotV2Engine.device(run.device), cands))
    }

    private suspend fun handleDeviceSelect2(run: SlotV2RunRow, t: String): Reply {
        val cands = run.pendingOptions.split(",").filter { it.isNotBlank() }.mapNotNull { SlotV2Engine.device(it) }
        val c = parseChoice(t) ?: return Reply.Ignore
        if (c == 0) return launchRun(run.copy(device2 = ""), run.charId, run.machineId, "")
        if (c < 1 || c > cands.size) return Reply.Ignore
        val dev2 = cands[c - 1]
        val mainPre = SlotV2Engine.device(run.device)?.let { "🔧${it.emoji}${it.name}" } ?: "🔧없음"
        return launchRun(run.copy(device2 = dev2.id), run.charId, run.machineId, "$mainPre + 보조🔧${dev2.emoji}${dev2.name} 장착 ")
    }

    /** 캐릭터·머신 확정 → 스테이지1 SPIN 시작 (수석졸업생 시작증강 포함). 머신선택·웹선택 공용. */
    private suspend fun launchRun(run: SlotV2RunRow, charId: String, machineId: String, prefix: String): Reply {
        val ch = SlotV2Engine.character(charId); val m = SlotV2Engine.machine(machineId)
        var startPerks = ""; var honorMsg = ""
        if (charId == "honor") {
            // 시작 증강도 해금분만(미해금 실버 차단) — 전부 미해금이면 gatedPool BASE 폴백.
            val hStat = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
            val aug = SlotV2Engine.gatedPool(SlotV2Engine.AUGMENTS, hStat).filter { it.tier == SlotV2Engine.Tier.SILVER }.randomOrNull(rng())
            if (aug != null) { startPerks = aug.id; honorMsg = " (시작 증강 ${aug.emoji}${aug.name})" }
        }
        val run2 = run.copy(
            charId = charId, machineId = machineId, coins = ch.startCoins.toLong(),
            state = "SPIN", perks = startPerks, stage = 1, spinIndex = 0, stageExp = 0, pendingOptions = "",
            lastActionAt = System.currentTimeMillis(),
        )
        App.db.slotV2Run().upsert(run2)
        track(run2, "seen_$charId" to 1L, "seen_$machineId" to 1L)   // 도감: 캐릭/머신 사용기록
        if (startPerks.isNotEmpty()) track(run2, "seen_$startPerks" to 1L)
        // 장치 숙련(ACH-5c) — 장착한 메인/보조 장치 각각 dvuse_<id> +1 (=그 장치로 시작한 런 수).
        if (run2.device.isNotBlank()) track(run2, "seen_${run2.device}" to 1L, "dvuse_${run2.device}" to 1L)
        if (run2.device2.isNotBlank()) track(run2, "seen_${run2.device2}" to 1L, "dvuse_${run2.device2}" to 1L)   // 도감: 보조 장치 사용기록 + 숙련
        val mods = SlotV2Engine.buildMods(machineId, charId, perkList(run2))
        return Reply.Msg(
            "@${run.ownerNick} $prefix${ch.emoji}${ch.name}+${m.emoji}${m.name} 시작!$honorMsg\n🎯 스테이지1: ${SlotV2Engine.spinsPerStage(mods)}스핀 안에 ${fmt(qOf(1, mods))}EXP${deviceFooter(run2)}\n👉 댓글로 \"잭팟\" 스핀! (특수 스핀: 집중/올인/기도/최후)",
            "📖 스핀 명령어 (그냥 \"잭팟\"이 기본, 아래는 선택지·스테이지당 1회)\n🎯집중 — 폭망 방지(최소 EXP 보장), 대신 고점↓. 안 풀릴 때.\n🎲올인 — EXP ×2! 단 ☠해골 2개↑면 0. 한 방 노릴 때.\n🙏기도 — 불운하면 +보정, 낮은 확률로 기적 ×3.\n⏰최후 — 마지막 스핀에서만, EXP ×1.75.",
        )
    }

    // ── 스핀 (.잭팟 기본 / .집중·.올인·.기도·.최후 특수, 스테이지당 1회) ──
    private fun appendNote(note: String, add: String): String = if (note.isEmpty()) add else "$note · $add"

    private suspend fun handleSpin(run: SlotV2RunRow, t: String): Reply {
        val mode = SPIN_CMDS[cmdOf(t)] ?: return Reply.Ignore
        val now = System.currentTimeMillis()
        val used = run.usedCmds.split(",").filter { it.isNotBlank() }
        val arm = run.armItems.split(",").filter { it.isNotBlank() }
        val phase = run.phaseItems.split(",").filter { it.isNotBlank() }
        // 신규 증강(ctx 조건부)용 — 먼저 ctx 없는 mods 로 spins/quota 산출 후 RunCtx 구성, 그 ctx 로 본 mods 재계산.
        // ★ RunCtx.spinsPerStage 는 evaluate 에 넘기는 effSpins(기본+stageBonus+보스) 와 동일해야 함
        //   (late_focus/cliff_focus 의 isLastSpin/spinsLeft 가 보스/보너스스핀과 어긋나면 1~2 오발동). 그래서 effSpins 를 미리 산출해 전달.
        //   bonusSpins 에 ctx 조건부(black_diploma, curseCount≥5 시 -1)가 끼므로 ctx 적용 mods 로 effSpins 산출(curseCount는 스핀수 비의존).
        val preMods0 = SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device)
        val preCtx = runCtxOf(run, run.spinIndex, SlotV2Engine.spinsPerStage(preMods0), qOf(run.stage, preMods0))
        val preMods = SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device, preCtx)
        val preEffSpins = effSpins(run, preMods)
        val runCtx = runCtxOf(run, run.spinIndex, preEffSpins, qOf(run.stage, preMods))
        var baseMods = SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device, runCtx)
        if (mode == "FOCUS") baseMods = baseMods.copy(rareWeightMul = baseMods.rareWeightMul * 0.5)  // 안정: 고점↓
        var mods = SlotV2Engine.applyItemMods(baseMods, arm + phase)
        val devEq = SlotV2Engine.device(run.device)
        if (devEq?.kind == SlotV2Engine.DevKind.PASSIVE) mods = SlotV2Engine.applyPassiveDevice(mods, devEq.id)  // 패시브 장치 자동
        // 효과중복 상한 — 일반 ×5 / 보스 ×4 / 프리즘 증강 보유 시 ×8 돌파 (고점빌드 통제)
        val hasPrism = perkList(run).any { SlotV2Engine.perk(it)?.tier == SlotV2Engine.Tier.PRISM }
        // (C2) 총배율 캡 — capMulFor 단일 결정(보스 우선). expMul 1차 클램프 + evaluate 총배율 캡(center/세트/불꽃 포함) 2중.
        val capMul = SlotV2Engine.capMulFor(run.stage, hasPrism)
        if (mods.expMul > capMul) mods = mods.copy(expMul = capMul)
        // (C2) 막스핀 폭주 캡 — lastSpinExpMul 최종값을 5.0 으로 클램프 (evaluate 전)
        if (mods.lastSpinExpMul > 5.0) mods = mods.copy(lastSpinExpMul = 5.0)
        val spins = effSpins(run, mods)
        val quota = qOf(run.stage, mods)
        // 특수명령 제한 — 막스핀/1회제한 + 코인 비용(즉시차감·무환불). 일반 스핀(N)은 무료(cmdCost=0).
        val bossStage = SlotV2Engine.bossFor(run.stage) != null
        val cmdCost = SlotV2Engine.cmdCoinCost(mode, bossStage)   // mode==N → 0
        if (mode != "N") {
            if (mode == "LAST" && run.spinIndex != spins - 1)
                return Reply.Msg("@${run.ownerNick} ⏰최후는 마지막 스핀(#$spins)에만 쓸 수 있어!")
            if (mode in used)
                return Reply.Msg("@${run.ownerNick} ${cmdLabel(mode)} 은(는) 이번 스테이지에 이미 썼어 (스테이지당 1회)")
            if (run.coins < cmdCost)
                return Reply.Msg("@${run.ownerNick} 🪙 코인 부족 — ${cmdLabel(mode)} ${cmdCost}코인 필요 (보유 ${fmt(run.coins)}🪙) · 일반 스핀은 무료")
        }
        val r = rng()
        // 🔮예언으로 확정된 셀 / ➕보조릴(패시브 6칸) / 일반 굴림
        val reel = if (devEq?.id == "dev_subreel") SlotV2Engine.REEL + 1 else SlotV2Engine.REEL
        val raw = if (run.lockedNext.isNotBlank())
            SlotV2Engine.cellsFromIds(run.lockedNext.split(",").filter { it.isNotBlank() })
        else SlotV2Engine.rollRaw(r, mods, reel, run.seedNext)
        SlotV2Engine.applyCellOps(raw, arm, r)        // NEXTSPIN 셀조작(eraser/wild/fake_crown) 평가 직전 in-place (rawIds=조작후 반영)
        val rawIds = raw.joinToString(",") { it.sym.id }
        // (C2) evaluate 에 총배율 캡 전달 — center/ends/flame/first·last/세트/global 곱 합산을 capMul 로 클램프(잭팟 고정가산 예외).
        val res = SlotV2Engine.evaluate(r, raw, mods, run.spinIndex, spins, run.flameNext, capMul = capMul)
        var gained = res.exp
        var modeNote = ""
        var prayMiracle = false   // 🙏 기도 성공(기적 ×3) — runPrayWins 카운트(bld_fate_hand)
        when (mode) {
            "FOCUS" -> { val floor = (quota.toDouble() / spins * 0.6).toLong()
                if (gained < floor) { gained = floor; modeNote = "🎯집중 — 최소 ${fmt(floor)} 보장" } else modeNote = "🎯집중(안정)" }
            "ALLIN" -> if (res.skulls >= 2) { gained = 0; modeNote = "🎲올인 실패! ☠${res.skulls}개 → EXP 0" }
                else { gained *= 2; modeNote = "🎲올인 성공! EXP ×2" }
            "PRAY" -> { val low = (quota.toDouble() / spins * 0.5).toLong()
                if (r.nextInt(100) < 8) { gained *= 3; modeNote = "🙏✨ 기적! EXP ×3"; prayMiracle = true }
                else if (gained < low) { gained += 25; modeNote = "🙏기도 — 불운보정 +25" } else modeNote = "🙏기도" }
            "LAST" -> { gained = (gained * 1.75).toLong(); modeNote = "⏰최후 EXP ×1.75" }
        }
        // 🪙 예약 EXP배수 — 직전에 장전된 다음스핀 배수(보조 코인투입 약화분 등)
        if (run.pendingNextExpMul != 1.0) { gained = (gained * run.pendingNextExpMul).toLong(); modeNote = appendNote(modeNote, "🪙다음스핀 ×${fmt2(run.pendingNextExpMul)}") }
        // 👑 보스 특수룰
        val boss = SlotV2Engine.bossFor(run.stage)
        if (boss != null) {
            val expPerSpin = if (spins > 0) quota.toDouble() / spins else 0.0
            val (g2, bn) = applyBoss(boss, gained, res, run.spinIndex, spins, expPerSpin, perkList(run).size)
            gained = g2
            if (bn.isNotEmpty()) modeNote = (modeNote + bn).removePrefix(" · ")
        }
        // 🦺안전벨트 (패시브) — 폭망 방지 하한 보장
        if (devEq?.id == "dev_safe") { val fl = (quota.toDouble() / spins * 0.35).toLong()
            if (gained < fl) { gained = fl; modeNote = appendNote(modeNote, "🦺안전벨트 최소 ${fmt(fl)} 보장") } }
        // 🔔비상졸업벨 — 즉시클리어(보장) + 장치 파괴
        var destroyDevice = false
        if ("dev_bell" in arm) {
            gained = maxOf(gained, (quota - run.stageExp).coerceAtLeast(0) + 1)
            destroyDevice = true; modeNote = appendNote(modeNote, "🔔비상졸업벨 발동! 즉시 클리어")
        }
        val newPendingMul = 1.0
        // 🍀 불운 게이지 — 나쁜 스핀(기대치 40%↓ 또는 ☠3개↑) 누적, 만땅이면 다음 보상 희귀↑
        val expected = if (spins > 0) quota.toDouble() / spins else 0.0
        val badSpin = !destroyDevice && (gained <= expected * 0.4 || res.skulls >= 3)
        val newGauge = if (badSpin) (run.unluckyGauge + 1).coerceAtMost(SlotV2Engine.UNLUCKY_MAX) else run.unluckyGauge
        if (badSpin && newGauge > run.unluckyGauge)
            modeNote = appendNote(modeNote, if (newGauge >= SlotV2Engine.UNLUCKY_MAX) "🍀불운 가득! 다음 보상 희귀↑ 보장" else "🍀불운 $newGauge/${SlotV2Engine.UNLUCKY_MAX}")
        val newUsed = if (mode != "N") (used + mode).joinToString(",") else run.usedCmds
        val newExp = run.stageExp + gained
        val newScore = run.score + res.score
        val newCoins = run.coins + res.coins - cmdCost   // 특수명령 코인 비용 즉시차감(무환불), 일반 스핀은 cmdCost=0
        val newIdx = run.spinIndex + 1
        // 🔥 한 방 신기록 — 이번 런 최고 EXP 스핀 갱신 시 연출
        if (gained > run.runBestSpin && gained >= quota / 2 && run.runBestSpin > 0)
            modeNote = appendNote(modeNote, "🔥 이번 런 최고의 한 방!")
        val m = SlotV2Engine.machine(run.machineId)
        val itemTag = if (arm.isNotEmpty() || phase.isNotEmpty()) " ✨아이템" else ""
        // (#11-a) 스핀 카운터 강조줄 — 방금 돌린 스핀(newIdx)을 눈에 띄게 맨 위에
        val counterLine = spinCounterLine(run, spins, spinNo = newIdx, upcoming = false)
        val header = "$counterLine\n${m.emoji}${m.name} @${run.ownerNick} S${run.stage}$itemTag"
        // ★ 특수 스핀명령(집중/올인/기도/최후) 발동 배너 — 결과 메시지 최상단 1줄. 효과 설명. 일반 스핀(N)은 없음.
        val cmdBanner = if (mode != "N") {
            "${cmdLabel(mode)} 발동! — ${SlotV2Engine.cmdEffectDesc(mode)}${if (cmdCost > 0) " · -${cmdCost}🪙" else ""}\n"
        } else ""
        val block = cmdBanner + spinBlock(header, res, gained, newExp, quota, modeNote, run.displayMode)

        // ── v68 신규 빌드 축: 이번 스핀 발동 카운터 ──
        // 인접쌍 보너스 발동(chain) — adjacentSameExp 효과가 있고 실제 인접 같은값심볼 쌍이 1개+ 생긴 스핀.
        val adjPairsFired = if (mods.adjacentSameExp != 0 && adjPairCount(res.cells) > 0) 1 else 0
        // 세트4+ 발동(magnet_grad) — 이번 스핀에 같은심볼 4개+ 세트 성립.
        val set4Fired = if (res.bestSetCount >= 4) 1 else 0
        // 기도 성공(fate_hand) — 🙏기적(×3) 발동했거나 이 기도 스핀으로 스테이지 클리어.
        val prayWin = if (mode == "PRAY" && (prayMiracle || newExp >= quota)) 1 else 0

        val spun = run.copy(
            stageExp = newExp, score = newScore, coins = newCoins, spinIndex = newIdx,
            flameNext = res.flameNext, seedNext = res.seedNext, armItems = "", usedCmds = newUsed,
            device = if (destroyDevice) "" else run.device, lastActionAt = now,
            lastCells = rawIds, lastGain = gained, lastScoreGain = res.score, lastCoinGain = res.coins,
            lastSet4 = set4Fired, lastAdjPairs = adjPairsFired,   // 재굴림/조작 교체 시 net-adjust용 직전 기여 저장
            lastSpinNo = run.spinIndex, pendingNextExpMul = newPendingMul, lockedNext = "",
            runJackpots = run.runJackpots + (if (res.jackpotSym != null) 1 else 0),
            runBestSpin = maxOf(run.runBestSpin, gained),
            runSymCounts = bumpSymCounts(run.runSymCounts, res.cells),
            unluckyGauge = newGauge,
            runAdjPairs = run.runAdjPairs + adjPairsFired,
            runSet4 = run.runSet4 + set4Fired,
            runPrayWins = run.runPrayWins + prayWin,
            // 무명령 제한도전(ACH-5c) — 특수 스핀명령(집중/올인/기도/최후) 사용 시 런 플래그 set. 한번 set되면 런 끝까지 유지(mode==N 이어도 1 유지).
            runUsedCmd = if (run.runUsedCmd == 1 || mode != "N") 1 else 0,
        )
        // 업적 누적 (이번 스핀) — 심볼/세트/명령어 카운터
        val cherryN = res.cells.count { it.sym.id == "cherry" }.toLong()
        val crownN = res.cells.count { it.sym.id == "crown" }.toLong()
        val incMap = linkedMapOf<String, Long>("totalSpins" to 1L)
        for (s in listOf("book", "star", "gem", "skull", "coin")) { val n = res.cells.count { it.sym.id == s }.toLong(); if (n > 0) incMap["${s}Total"] = n }
        if (res.bestSetCount >= 4) incMap["set4Plus"] = 1L
        when (mode) { "FOCUS" -> incMap["focusUses"] = 1L; "LAST" -> incMap["lastUses"] = 1L; "ALLIN" -> if (res.skulls < 2) incMap["allinWins"] = 1L }
        // ── 특수명령 코인 지출 누적(차감 시점) — cc_ 업적 키(cmdCoin_focus/pray/allin/last + cmdCoinTotal) ──
        if (cmdCost > 0) {
            when (mode) {
                "FOCUS" -> incMap["cmdCoin_focus"] = cmdCost.toLong()
                "PRAY"  -> incMap["cmdCoin_pray"]  = cmdCost.toLong()
                "ALLIN" -> incMap["cmdCoin_allin"] = cmdCost.toLong()
                "LAST"  -> incMap["cmdCoin_last"]  = cmdCost.toLong()
            }
            incMap["cmdCoinTotal"] = cmdCost.toLong()
        }
        // ── ACH-3: 특수심볼 누적(inc) — 평가 후 셀의 special 개수만큼 가산 ──
        for ((sp, key) in listOf(
            SlotV2Engine.Sp.WILD to "wildTotal", SlotV2Engine.Sp.SEED to "seedTotal",
            SlotV2Engine.Sp.DICE to "diceTotal", SlotV2Engine.Sp.KEY to "keyTotal",
            SlotV2Engine.Sp.FLAME to "flameTotal", SlotV2Engine.Sp.MAGNET to "magnetTotal",
            SlotV2Engine.Sp.BOMB to "bombTotal",
        )) { val n = res.cells.count { it.sym.special == sp }.toLong(); if (n > 0) incMap[key] = n }
        // ── ACH-3: 잭팟 종류(inc) — 왕관잭팟 / 와일드 포함 잭팟 ──
        if (res.jackpotSym == "crown") incMap["crownJackpots"] = 1L
        if (res.jackpotSym != null && res.cells.any { it.sym.special == SlotV2Engine.Sp.WILD }) incMap["wildJackpots"] = 1L
        // ── ACH-3: 🎲올인 폭망(inc) — ☠2개+ 로 EXP 0 ──
        if (mode == "ALLIN" && res.skulls >= 2) incMap["allinBusts"] = 1L
        // ── ACH-3: 한 스핀 내 같은 심볼 최대 개수(setMax) ──
        val spinMax = linkedMapOf<String, Long>()
        for ((id, key) in listOf(
            "skull" to "maxSkullSpin", "coin" to "maxCoinSpin", "cherry" to "maxCherrySpin",
            "book" to "maxBookSpin", "gem" to "maxGemSpin",
        )) { val n = res.cells.count { it.sym.id == id }.toLong(); if (n > 0) spinMax[key] = n }
        val sb = achBanner(bumpAch(spun, cherry = cherryN, crown = crownN, jackpot = if (res.jackpotSym != null) 1 else 0, inc = incMap, setMax = spinMax))
        if (newExp >= quota) return appendBanner(clearStage(spun, res, newExp, newScore, newCoins, newIdx, spins, quota, block, bellUsedThisClear = destroyDevice), sb)
        if (newIdx >= spins) {
            // 🔔 운명의종(fate_bell) — 런 1회, 부족 ≤15 실패직전 자동 추가스핀 +1. 비상졸업벨(dev_bell 즉시클리어)과 중첩 안 됨(그건 이미 클리어 처리).
            if (newExp < quota && (quota - newExp) <= 15 && spun.fateBellUsed == 0 && "fate_bell" in perkList(run)) {
                val revived = spun.copy(fateBellUsed = 1, stageBonusSpins = spun.stageBonusSpins + 1)
                App.db.slotV2Run().upsert(revived)
                return appendBanner(Reply.Msg("$block\n$DIV\n🔔 운명의종 발동! 부족 ${fmt(quota - newExp)}EXP → 추가 스핀 +1 (런 1회)"), sb)
            }
            // 📋 보험증서(f) — 이번 스테이지 실패 1회 생존(스핀+2). 1회용.
            if (newExp < quota && spun.survive) {
                val revived = spun.copy(survive = false, stageBonusSpins = spun.stageBonusSpins + 2)
                App.db.slotV2Run().upsert(revived)
                return appendBanner(Reply.Msg("$block\n$DIV\n📋 보험증서 발동! 스핀 +2 생존 (1회용)"), sb)
            }
            // 🔧 직전결과 조작(MANIP 장치) 또는 🎲도박꾼 무료 재굴림이 남았으면 게임오버 보류 → POST_SPIN(만회 기회)
            val dev = SlotV2Engine.device(run.device)
            val manipAvail = dev != null && dev.kind == SlotV2Engine.DevKind.MANIP && dev.cmd !in newUsed.split(",")
            val gamblerReroll = run.charId == "gambler" && "GREROL" !in newUsed.split(",")
            if (manipAvail || gamblerReroll) {
                App.db.slotV2Run().upsert(spun.copy(state = "POST_SPIN"))
                val short = quota - newExp
                val opts = mutableListOf<String>()
                if (gamblerReroll) opts += "🎲\"재굴림\"(도박꾼 무료)"
                if (manipAvail) opts += "🔧\"${dev!!.cmd}${if (dev.needsArg) " N(칸번호)" else ""}\"(${dev.emoji}${dev.name})"
                return appendBanner(Reply.Msg("$block\n$DIV\n💀 마지막 스핀! 부족 ${fmt(short)}EXP\n만회: ${opts.joinToString(" 또는 ")}  ·  안 되면 \"포기\""), sb)
            }
            return appendBanner(gameOver(spun, newScore, block, "요구 ${fmt(quota)}EXP 미달"), sb)
        }
        App.db.slotV2Run().upsert(spun)
        val devHint = listOfNotNull(run.device, run.device2)
            .mapNotNull { SlotV2Engine.device(it) }
            .filter { it.cmd.isNotEmpty() && it.cmd !in newUsed }
            .joinToString("") { " · 🔧${it.cmd}" }
        val itemHint = heldItems(run).size.let { if (it > 0) " · 🎒${it}개(\"아이템\"으로 사용)" else "" }
        return Reply.Msg("$block\n👉 \"잭팟\" (또는 집중/올인/기도" + (if (newIdx == spins - 1) "/최후" else "") + "$devHint)$itemHint$sb")
    }

    private fun spinBlock(header: String, res: SlotV2Engine.SpinResult, gainedExp: Long, stageExp: Long, quota: Long, modeNote: String = "", mode: String = "NORMAL"): String = buildString {
        append("$header\n")
        if (mode == "SIMPLE") {  // 🔹간단 — 결과만
            if (res.jackpotSym != null) append("🎰JACKPOT ")
            append(SlotV2Engine.render(res.cells)).append("\n")
            append("+${fmt(gainedExp)}EXP → ${fmt(stageExp)}/${fmt(quota)}")
            return@buildString
        }
        if (mode == "CALC") {  // 🧮계산 — 합산×배율 표시
            if (res.jackpotSym != null) append("🎰JACKPOT ")
            append(SlotV2Engine.render(res.cells)).append("\n")
            if (res.notes.isNotEmpty()) append(res.notes.joinToString(" · ")).append("\n")
            append("🧮 합산 ${fmt(res.preMul)} × 배율 ${fmt2(res.mul)}${if (res.flat != 0) " + 고정 ${res.flat}" else ""} = ${fmt(res.exp)}\n")
            if (modeNote.isNotEmpty()) append("→ $modeNote\n")
            append("최종 +${fmt(gainedExp)}EXP")
            if (res.score > 0) append(" · +${fmt(res.score)}점")
            append("  →  ${fmt(stageExp)}/${fmt(quota)}EXP")
            return@buildString
        }
        if (res.jackpotSym != null) append("🎰🎰🎰 J A C K P O T 🎰🎰🎰\n")
        append(SlotV2Engine.render(res.cells)).append("\n")
        if (modeNote.isNotEmpty()) append(modeNote).append("\n")
        if (res.notes.isNotEmpty()) append(res.notes.joinToString(" · ")).append("\n")
        append("+${fmt(gainedExp)}EXP")
        if (res.score > 0) append(" · +${fmt(res.score)}점")
        if (res.coins > 0) append(" · +${res.coins}🪙")
        append("  →  ${fmt(stageExp)}/${fmt(quota)}EXP")
    }

    /** 최다 등장 심볼 (이모지, 횟수). */
    private fun topSymOf(csv: String): Pair<String, Int>? =
        csv.split(",").filter { it.isNotBlank() }
            .mapNotNull { val p = it.split(":"); if (p.size == 2) (SlotV2Engine.SYM_BY_ID[p[0]]?.emoji ?: p[0]) to (p[1].toIntOrNull() ?: 0) else null }
            .maxByOrNull { it.second }

    /** 이번 런 심볼 등장수 누적 ("id:n,id:n") — 실패 리포트 최다심볼. */
    private fun bumpSymCounts(csv: String, cells: List<SlotV2Engine.Cell>): String {
        val m = LinkedHashMap<String, Int>()
        csv.split(",").filter { it.isNotBlank() }.forEach { val p = it.split(":"); if (p.size == 2) m[p[0]] = p[1].toIntOrNull() ?: 0 }
        cells.forEach { c -> if (c.sym.id != "empty") m[c.sym.id] = (m[c.sym.id] ?: 0) + 1 }
        return m.entries.joinToString(",") { "${it.key}:${it.value}" }
    }

    /** 이번 런 증강/유물 중 지정 티어 perk 개수 (run.perks → SlotV2Engine.perk(id)?.tier). */
    private fun perkTierCount(run: SlotV2RunRow, vararg tiers: SlotV2Engine.Tier): Int =
        perkList(run).count { SlotV2Engine.perk(it)?.tier in tiers }

    /** 이번 런 유물(RELIC cat) 개수. */
    private fun relicCount(run: SlotV2RunRow): Int =
        perkList(run).count { SlotV2Engine.perk(it)?.cat == SlotV2Engine.PCat.RELIC }

    /**
     * ACH-4 클리어 추적 — 제한도전(setMax) + 보스별/클리어히든(inc). 전부 run 상태로 파생 가능한 것만.
     *  clearInc/clearSetMax 맵에 직접 누적 → 호출부의 단일 bumpAch 로 커밋(추가 DB 라운드트립 없음).
     *  boss/overPct/res/lastSpinClear/inDebt/coinsAtClear 는 clearStage 가 이미 계산한 값.
     */
    private fun addAch4ClearTracking(
        run: SlotV2RunRow, clearInc: LinkedHashMap<String, Long>, clearSetMax: LinkedHashMap<String, Long>,
        boss: Boolean, overPct: Long, res: SlotV2Engine.SpinResult,
        lastSpinClear: Boolean, inDebt: Boolean, coinsAtClear: Long,
    ) {
        val stage = run.stage.toLong()
        val prismN = perkTierCount(run, SlotV2Engine.Tier.PRISM)
        val goldPlusN = perkTierCount(run, SlotV2Engine.Tier.GOLD, SlotV2Engine.Tier.PRISM)
        val relicN = relicCount(run)
        // ── 제한도전 최고도달 S (조건충족 시 setMax 클리어 스테이지) ──
        if (prismN == 0) clearSetMax[SlotV2Engine.KEY_NO_PRISM_STAGE] = stage
        if (relicN == 0) clearSetMax[SlotV2Engine.KEY_NO_RELIC_STAGE] = stage
        if (goldPlusN == 0) clearSetMax[SlotV2Engine.KEY_NO_GOLD_STAGE] = stage
        if (run.charId == "novice" && run.machineId == "basic") clearSetMax[SlotV2Engine.KEY_BASIC_ONLY_STAGE] = stage
        // ── 보스별 클리어 (boss=bossFor(stage)!=null 클리어 시) ──
        if (boss) {
            val bossObj = SlotV2Engine.bossFor(run.stage)
            val bossId = bossObj?.id
            if (bossId != null) {
                clearInc["bossClear_$bossId"] = (clearInc["bossClear_$bossId"] ?: 0L) + 1L
                // 각 보스 카운터(약점)조건 충족 클리어:
                //  finals=막스핀 클리어, strict=세트3+(이번 클리어 스핀), luck=⭐👑🌀(star/crown/wild) 포함,
                //  grad=무장치(메인·보조 둘 다 미장착) — bossFor desc/counterTags 매핑.
                val counterMet = when (bossId) {
                    "finals" -> lastSpinClear
                    "strict" -> res.bestSetCount >= 3
                    "luck"   -> res.cells.any { it.sym.id == "star" || it.sym.id == "crown" || it.sym.id == "wild" }
                    "grad"   -> run.device.isEmpty() && run.device2.isEmpty()
                    else     -> false
                }
                if (counterMet) clearInc["bossCounterClear_$bossId"] = (clearInc["bossCounterClear_$bossId"] ?: 0L) + 1L
            }
            // 보스 클리어 공통 제약(inc)
            if (!run.usedItemThisRun) clearInc["bossNoItemClears"] = (clearInc["bossNoItemClears"] ?: 0L) + 1L
            if (run.device.isEmpty() && run.device2.isEmpty()) clearInc["bossNoDeviceClears"] = (clearInc["bossNoDeviceClears"] ?: 0L) + 1L
            if (overPct >= 500) clearInc["bossOverkillClears"] = (clearInc["bossOverkillClears"] ?: 0L) + 1L
            // 한 런 보스 3회 연속 격파 — 클리어한 보스수 = stage/5 (S15 보스클리어 시 3) ≥3 이면 setMax(1)
            if (run.stage >= 15) clearSetMax["bossStreak3"] = maxOf(clearSetMax["bossStreak3"] ?: 0L, 1L)
        }
        // ── 클리어 히든 ──
        // 빈 지갑 클리어: 클리어 직전(가산 전) 보유 코인 0 으로 스테이지 클리어
        if (coinsAtClear <= 0L) clearInc["zeroCoinClears"] = (clearInc["zeroCoinClears"] ?: 0L) + 1L
        // 빚더미 보스 클리어: 🧾빚문서 활성(debtStages>0) 상태로 보스 클리어
        if (boss && inDebt) clearInc["debtBossClears"] = (clearInc["debtBossClears"] ?: 0L) + 1L
    }

    // ── 스테이지 클리어 → 노드 선택 ─────────────────────────
    private suspend fun clearStage(
        run: SlotV2RunRow, res: SlotV2Engine.SpinResult,
        newExp: Long, newScore: Long, newCoins: Long, newIdx: Int, spins: Int, quota: Long, block: String,
        bellUsedThisClear: Boolean = false,   // 🔔비상졸업벨로 성사된 클리어(bld_miracle_cert)
    ): Reply {
        val phase = run.phaseItems.split(",").filter { it.isNotBlank() }
        val mods = SlotV2Engine.applyItemMods(
            SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device), phase)
        val leftSpins = (spins - newIdx).coerceAtLeast(0)
        val leftover = (newExp - quota).coerceAtLeast(0)
        val boss = SlotV2Engine.isBossStage(run.stage)
        val clearScore = SlotV2Engine.stageClearScore(run.stage, leftover, leftSpins, curseCount(run), boss)
        // ── v68 신규 빌드 축: 이번 클리어 분류 + 증강 스택 갱신 ──
        val lastSpinClear = newIdx >= spins                  // 막스핀에 클리어
        val closeClear = leftover <= 10                      // 부족 ≤10(턱걸이) 클리어
        val fastClear = leftSpins >= 2                       // 남은스핀 ≥2(여유) 클리어
        // 📈 성장일지: 클리어마다 +1(최대5). 🔔다음스테이지 첫스핀 EXP+8%×스택.
        val newGrowthStack = (run.growthStack + 1).coerceAtMost(5)
        // ❄️ 눈덩이: 빠른클리어(남은스핀≥2)면 +1(최대4), 보스클리어 후 -1. 다음스테이지 EXP+12%×스택.
        var newSnowStack = run.snowStack
        if (fastClear) newSnowStack = (newSnowStack + 1).coerceAtMost(4)
        if (boss) newSnowStack = (newSnowStack - 1).coerceAtLeast(0)
        val inDebt = run.debtStages > 0   // 🧾 빚문서(i) — 무보상 스테이지
        val clearCoin = if (inDebt) 0 else (if (boss) SlotV2Engine.BOSS_COIN else SlotV2Engine.CLEAR_COIN) + mods.clearCoinBonus
        // 아슬아슬 보너스 티어
        var close = 0L
        val closeTags = mutableListOf<String>()
        if (leftover <= 5) { close += 300; closeTags += "🔥턱걸이+300" }
        else if (leftover <= 10) { close += 150; closeTags += "🔥아슬아슬+150" }
        if (newIdx >= spins) { close += 200; closeTags += "⏰막판클리어+200" }
        // 🔥 연승(연속 스테이지 클리어) 보너스 — 깊을수록 가속
        val streakB = SlotV2Engine.streakBonus(run.stage)
        if (streakB > 0) { close += streakB; closeTags += "🔥${run.stage}연속+${streakB}" }
        // 초과 경험치 평가등급
        val overPct = if (quota > 0) newExp * 100 / quota else 100
        val (grade, gradeBonus) = when {
            overPct >= 500 -> "💥슬롯파괴자" to 1000L
            overPct >= 300 -> "👹괴물" to 500L
            overPct >= 200 -> "🌟천재" to 250L
            overPct >= 150 -> "🎓장학생" to 120L
            overPct >= 120 -> "✨우수" to 50L
            else -> "✅합격" to 0L
        }
        val gainedScore = if (inDebt) 0L else clearScore + close + gradeBonus
        val nextStage = run.stage + 1
        val now = System.currentTimeMillis()
        // 성장 노드(증강) 1개 보장 + 무작위 2개
        val rN = rng()
        val pool = mutableListOf("RELIC", "SHOP", "REST", "GAMBLE", "EVENT")  // 장치는 이벤트 랜덤/보스/업적으로만(전용 노드 제거)
        if (nextStage >= 6) { pool.add("CURSE"); pool.add("RISK") }  // 저주·위험거래는 첫 보스(S5) 이후 — 초보 피로 방지
        val extras = pool.shuffled(rN).take(2)
        val nodes = (listOf("AUGMENT") + extras).shuffled(rN)
        val run2 = run.copy(
            stage = nextStage, spinIndex = 0, stageExp = 0,
            score = newScore + gainedScore, coins = newCoins + clearCoin,
            flameNext = false, seedNext = false,
            // 스테이지 휘발 소거 — 단, RUNSHOP(이번 런 상점사용 마커)은 런 끝까지 유지(검소한졸업 판정)
            armItems = "", phaseItems = "", stageBonusSpins = 0,
            // 런-스코프 마커(RUNSHOP 검소한졸업·RUNORACLE 예언사용)는 스테이지 클리어 후에도 런 끝까지 유지.
            usedCmds = run.usedCmds.split(",").filter { it == "RUNSHOP" || it == "RUNORACLE" }.distinct().joinToString(","),
            debtStages = (run.debtStages - 1).coerceAtLeast(0),                 // (i) 빚 스테이지 차감
            phasePerks = "",                                                    // (k) 깨진프리즘 임시perk 휘발
            lastCells = "", lastGain = 0, lastScoreGain = 0, lastCoinGain = 0, lastSpinNo = -1,
            pendingNextExpMul = 1.0, lockedNext = "", devCooldown = (run.devCooldown - 1).coerceAtLeast(0),
            closestClear = if (run.closestClear < 0) leftover.toInt() else minOf(run.closestClear, leftover.toInt()),
            // ── v68 신규 빌드 축: 클리어 카운터 + 증강 스택 ──
            runLastSpinClears = run.runLastSpinClears + (if (lastSpinClear) 1 else 0),
            runCloseClears = run.runCloseClears + (if (closeClear) 1 else 0),
            runFastClears = run.runFastClears + (if (fastClear) 1 else 0),
            growthStack = newGrowthStack, snowStack = newSnowStack,
            state = "NODE_SELECT", pendingOptions = nodes.joinToString(","),
            lastActionAt = now,
        )
        App.db.slotV2Run().upsert(run2)
        val relicN = perkList(run).count { SlotV2Engine.perk(it)?.cat == SlotV2Engine.PCat.RELIC }.toLong()
        val clearInc = linkedMapOf<String, Long>()
        if (leftover <= 10) clearInc["closeClears"] = 1L
        if ("PRAY" in run.usedCmds.split(",")) clearInc["prayClears"] = 1L
        // ★ 코인소모 명령으로 성사한 클리어 추적 — 이번 스테이지 usedCmds(spun 기준, 방금 사용분 포함)에 마커가 있으면 가산.
        if ("LAST" in run.usedCmds.split(",")) clearInc["lastClears"] = 1L          // ⏰최후로 클리어
        if (boss && "ALLIN" in run.usedCmds.split(",")) clearInc["bossAllinClear"] = 1L  // 👑보스에서 🎲올인 사용+클리어
        // ── 면허/캐릭/머신 해금용 신규 카운터 ──
        // 미니멀리스트: 유물 3개 이하로 S10 도달 클리어
        if (relicN <= 3 && run.stage >= 10) clearInc["minimalistS10"] = 1L
        // 연금술사: 코인50↑ 보유로 보스 클리어 (clearCoin 가산 전 보유 코인 기준)
        if (boss && run.coins >= 50) clearInc["richBossClears"] = 1L
        // 수도승: 이번 런 아이템 미사용 & S8 도달 클리어
        if (run.stage >= 8 && !run.usedItemThisRun) clearInc["noItemS8"] = 1L
        // ── 배치3a 표준 도전(inc) ──
        val nCurse = curseCount(run)
        // 🪙 검소한졸업: 이번 런 상점 한 번도 안 쓰고(=RUNSHOP 마커 없음) S10 도달
        if (run.stage >= 10 && "RUNSHOP" !in run.usedCmds.split(",")) clearInc[SlotV2Engine.KEY_NO_SHOP_S10] = 1L
        // 💀 해골연구: 저주 3개↑ 보유로 보스 클리어
        if (boss && nCurse >= 3) clearInc[SlotV2Engine.KEY_CURSE_BOSS_CLEARS] = 1L
        // ── 배치3a 개인기록/표준 도전(setMax) ──
        val noDevice = run.device.isEmpty() && run.device2.isEmpty()
        val clearSetMax = linkedMapOf(
            "curseMax" to nCurse.toLong(), "relicsMax" to relicN,
            "cstage_${run.charId}" to run.stage.toLong(), "mstage_${run.machineId}" to run.stage.toLong(),
            // 빌드도감 — 클리어한 캐릭+머신 조합 최고스테이지
            SlotV2Engine.bcKey(run.charId, run.machineId) to run.stage.toLong(),
            // 개인기록 — 한 런 최다잭팟 / 한 스테이지 최대초과%
            SlotV2Engine.KEY_MAX_RUN_JACKPOTS to run.runJackpots.toLong(),
            SlotV2Engine.KEY_MAX_OVER_PCT to overPct,
        )
        // 🚫 무장치 최고도달 S (메인·보조 둘 다 미장착)
        if (noDevice) clearSetMax[SlotV2Engine.KEY_NO_DEV_STAGE] = run.stage.toLong()
        // 🧘 무아이템 최고도달 S (이번 런 아이템 미사용)
        if (!run.usedItemThisRun) clearSetMax[SlotV2Engine.KEY_NO_ITEM_MAX_S] = run.stage.toLong()
        // ☠ 저주졸업식: 저주 5개↑ 보유로 도달한 최고 S
        if (nCurse >= 5) clearSetMax[SlotV2Engine.KEY_CURSE5_STAGE] = run.stage.toLong()
        // 🔧 장치 장인(ACH-5c) — 이번 런 장착 메인/보조 장치별 도달한 최고 클리어 S (dvstage_<id> setMax)
        if (run.device.isNotBlank()) clearSetMax["dvstage_${run.device}"] = run.stage.toLong()
        if (run.device2.isNotBlank()) clearSetMax["dvstage_${run.device2}"] = run.stage.toLong()
        // ⌨️🚫 무명령 제한도전(ACH-5c) — 이번 런 특수 스핀명령 0회(runUsedCmd==0)로 도달한 최고 S
        if (run.runUsedCmd == 0) clearSetMax[SlotV2Engine.KEY_NO_CMD_STAGE] = run.stage.toLong()
        // 🔧🚫 무조작 제한도전(ACH-5c) — 이번 런 재굴림/고정/복사/교체 0회(runRerolled==0)로 도달한 최고 S
        if (run.runRerolled == 0) clearSetMax[SlotV2Engine.KEY_NO_REROLL_STAGE] = run.stage.toLong()
        // ── v68 빌드 도감 완성판정 (클리어 시점) — evalThemeBuilds → bld_* setMax + 신규완성 알림 ──
        val priorBlds = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
        val buildCtx = SlotV2Engine.BuildCtx(
            stage = run.stage, machineId = run.machineId, deviceId = run.device, device2Id = run.device2,
            perks = perkList(run), curses = curseList(run),
            runFastClears = run2.runFastClears, runLastSpinClears = run2.runLastSpinClears,
            runPrayWins = run2.runPrayWins, runAdjPairs = run2.runAdjPairs, runSet4 = run2.runSet4,
            runCrowns = runCrownCount(run.runSymCounts),
            isBossClear = boss, isLastSpinClear = lastSpinClear,
            clearSpinRareCount = res.cells.count { it.sym.rare },
            clearSpinSkullCount = res.skulls,
            clearSpinWildJackpot = res.jackpotSym != null && res.cells.any { it.sym.special == SlotV2Engine.Sp.WILD },
            jackpotThisRun = run.runJackpots > 0 || res.jackpotSym != null,
            oracleUsedThisRun = "RUNORACLE" in run.usedCmds.split(","),   // 장착이 아니라 실제 🔮예언 호출 여부(RUNORACLE 마커)
            pinUsedThisStage = "고정" in run.usedCmds.split(","),
            copyMadeSet4 = hasDevice(run, "dev_copy") && run2.runSet4 >= 1,
            bellUsedThisClear = bellUsedThisClear,
            // skullTotal 은 스핀시점 bumpAch 로 이미 반영됨(현재값). closeClears 는 이 clearStage 의 clearInc(line ↓)에서
            // bumpAch 이후 증가하므로 priorBlds 엔 이번 클리어분이 빠져 있음 → 직접 가산해야 5번째 close-clear 런에서 bld_heartbreaker 즉시 충족.
            skullTotal = priorBlds["skullTotal"] ?: 0L,
            closeClears = (priorBlds["closeClears"] ?: 0L) + (if (closeClear) 1L else 0L),
        )
        val satisfiedBlds = SlotV2Engine.evalThemeBuilds(buildCtx)
        satisfiedBlds.forEach { clearSetMax[it] = 1L }
        val newlyBlds = satisfiedBlds.filter { (priorBlds[it] ?: 0L) <= 0L }
        // ══ ACH-4: 제한도전(setMax) + 보스별/클리어히든(inc) — 클리어 시점, 런상태 파생만 ══
        addAch4ClearTracking(run, clearInc, clearSetMax, boss = boss, overPct = overPct,
            res = res, lastSpinClear = lastSpinClear, inDebt = inDebt, coinsAtClear = run.coins)
        // 캐릭/머신별 최고스테이지(setMax) — distinctCharS10·체리농부(mstage_cherry) 등 조건의 소스
        val achNew = bumpAch(run, boss = if (boss) 1 else 0, lastClear = if (newIdx >= spins) 1 else 0,
            exact = if (newExp == quota) 1 else 0, stageReached = run.stage.toLong(),
            inc = clearInc, setMax = clearSetMax)
        val banner = buildString {
            append("$block\n")
            append("$DIV\n")
            append("✅ @${run.ownerNick} 스테이지${run.stage} 클리어! [$grade ${overPct}%]")
            if (boss) append(" 👑BOSS")
            append("\n+${fmt(gainedScore)}점")
            if (leftSpins > 0) append(" (남은스핀 ${leftSpins}×100)")
            if (closeTags.isNotEmpty()) append(" " + closeTags.joinToString(" "))
            append(" · +${clearCoin}🪙\n")
            append("🏆누적 ${fmt(run2.score)}점 · 🪙${fmt(run2.coins)}\n")
            append("$DIV\n")
            append(nodeText(run2))
        }
        return Reply.Msg(banner + achBanner(achNew) + buildCompleteBanner(newlyBlds))
    }

    /** 🏅 신규 빌드 완성 알림(클리어/게임오버 시) — diff 로 새로 완성된 bld_* 만 1줄씩. */
    private fun buildCompleteBanner(newlyBldIds: List<String>): String {
        if (newlyBldIds.isEmpty()) return ""
        return newlyBldIds.mapNotNull { SlotV2Engine.themeBuild(it) }
            .joinToString("") { "\n🏅 빌드 완성: ${it.emoji}${it.name}" }
    }

    /** 이번 런 👑왕관 등장 수 (runSymCounts "id:n" CSV 에서 crown). */
    private fun runCrownCount(csv: String): Int =
        csv.split(",").filter { it.isNotBlank() }
            .firstOrNull { it.startsWith("crown:") }?.removePrefix("crown:")?.toIntOrNull() ?: 0

    /** 셀에서 붙어있는 같은 값심볼 쌍 개수 (chain 빌드축 판정). */
    private fun adjPairCount(cells: List<SlotV2Engine.Cell>): Int {
        var p = 0
        for (i in 0 until cells.size - 1) {
            val a = cells[i].sym; val b = cells[i + 1].sym
            if (a.id == b.id && a.id in VALUE_SYM_IDS) p++
        }
        return p
    }

    private fun nodeLabel(id: String): String = when (id) {
        "AUGMENT" -> "✨ 증강 — 무료 증강 1개 선택 (성장)"
        "RELIC" -> "🛡️ 유물 — 무료 유물 1개 선택 (성장)"
        "SHOP" -> "🛒 상점 — 코인으로 유물·아이템 구매"
        "REST" -> "🛌 휴식 — 코인 +8 (안전)"
        "GAMBLE" -> "🎲 도박장 — 코인 전부 50% 2배 / 50% 잃음"
        "EVENT" -> "🎁 이벤트 — 랜덤 보상(코인·점수·아이템·🛡️유물·✨증강, 가끔 🎉특별)"
        "CURSE" -> "🌑 저주 — 저주 1개 받고 코인 +15 (단점+장점)"
        "RISK" -> "🎲 위험한 거래 — 🌈프리즘 증강 1개 + 🌑저주 1개 (고위험·고보상)"
        else -> id
    }

    private fun tierMark(t: SlotV2Engine.Tier): String = when (t) {
        SlotV2Engine.Tier.SILVER -> "🥈"; SlotV2Engine.Tier.GOLD -> "🥇"; SlotV2Engine.Tier.PRISM -> "🌈"
    }
    /** 초보 추천 태그 — 티어별. */
    private fun tierTag(t: SlotV2Engine.Tier): String = when (t) {
        SlotV2Engine.Tier.SILVER -> "[초보추천]"; SlotV2Engine.Tier.GOLD -> "[빌드]"; SlotV2Engine.Tier.PRISM -> "[고급]"
    }
    private fun tierAdvice(t: SlotV2Engine.Tier): String = when (t) {
        SlotV2Engine.Tier.SILVER -> "무난·안정형. 빌드 안 타도 항상 이득 — 초보 추천."
        SlotV2Engine.Tier.GOLD -> "이번 런의 빌드 방향을 정하는 특수 효과. 조건/시너지를 보고 선택."
        SlotV2Engine.Tier.PRISM -> "게임 규칙을 바꾸는 강력한 고급 증강. 리스크도 큼."
    }
    // ── 선택지 상세 안내 (두 번째 메시지) ──
    private fun perkDetailText(picks: List<SlotV2Engine.Perk>, held: Set<String> = emptySet()): String = buildString {
        append("📖 선택 안내\n")
        picks.forEachIndexed { i, p ->
            append("${i + 1}. ${tierMark(p.tier)} ${p.emoji}${p.name} ${tierTag(p.tier)}\n")
            append("   · 효과: ${p.desc}\n")
            append("   · ${tierAdvice(p.tier)}\n")
            SlotV2Engine.setSynergyName(p.id, held)?.let { syn ->
                val tail = if (syn.endsWith("완성")) "고르면 세트 효과 발동!" else "고르면 세트 효과에 근접."
                append("   · 🧩$syn — $tail\n")
            }
        }
    }
    private fun charDetailText(offered: List<SlotV2Engine.Character>): String = buildString {
        append("📖 캐릭터 안내 (플레이 스타일이 달라져요)\n")
        offered.forEachIndexed { i, c ->
            append("${i + 1}. ${c.emoji}${c.name} [난이도 ${SlotV2Engine.charDiff(c)}]\n   · ${c.desc}\n   · 점수보정 ×${fmt2(c.scoreMod)}${if (c.startCoins > 0) " · 시작코인 ${c.startCoins}" else ""} · 추천: ${charRecommend(c)}\n")
        }
    }
    private fun machineDetailText(offered: List<SlotV2Engine.Machine>): String = buildString {
        append("📖 슬롯머신 안내 (확률 환경이 달라져요)\n")
        offered.forEachIndexed { i, m ->
            append("${i + 1}. ${m.emoji}${m.name} [난이도 ${SlotV2Engine.machineDiff(m)}]\n   · ${m.desc}\n   · 점수보정 ×${fmt2(m.scoreMod)}\n")
        }
    }
    /** 캐릭터별 추천 빌드 방향(첫 사용 가이드). */
    private fun charRecommend(c: SlotV2Engine.Character): String = when (c.id) {
        "scholar", "honor" -> "📘책·학습 빌드"; "farmer" -> "🍒체리 세트"; "jeweler", "highroller" -> "💎보석 점수"
        "cultist" -> "☠해골·저주 빌드"; "crowncol" -> "👑왕관 한방"; "minimalist" -> "유물 적게(증강 위주)"
        "gambler", "lucky", "daredevil" -> "고위험·희귀 한방"; "monk" -> "속전속결(적은 스핀)"
        "alchemist", "parttime" -> "코인·상점 활용"; "prodigy" -> "안정 성장"; else -> "무난(입문)"
    }

    /** .상태 — 현재 진행 상황 요약 (어느 단계든). */
    private suspend fun statusReply(run: SlotV2RunRow): String {
        val ch = SlotV2Engine.character(run.charId); val m = SlotV2Engine.machine(run.machineId)
        val head = if (run.machineId.isBlank()) "🎰 (선택 진행 중)" else "🎰 ${ch.emoji}${ch.name} + ${m.emoji}${m.name}"
        val uid = run.ownerUserId.takeIf { it > 0 }
        val sc = myScore(run.linkId, run.ownerNick, uid)
        val statForStatus = composeStat(myAch(run.linkId, run.ownerNick, uid), sc)
        val pinned = pinnedLine(sc, statForStatus)
        return buildString {
            append("@${run.ownerNick} $head\n")
            append("🎓 졸업레벨 Lv.${SlotV2Engine.accountLevel(statForStatus)}\n")
            append("🏆 점수 ${fmt(run.score)} · 🪙 ${fmt(run.coins)}\n")
            if (run.state == "SPIN" || run.machineId.isNotBlank()) append("${stageGoalLine(run)}\n")
            append(heldLine(run))
            val curseDetails = curseList(run).mapNotNull { SlotV2Engine.perk(it) }
            if (curseDetails.isNotEmpty()) {
                append("\n🌑 저주 효과:")
                curseDetails.forEach { append("\n· ${it.emoji}${it.name} — ${it.desc}") }
            }
            if (pinned.isNotEmpty()) append("\n$pinned")
        }
    }

    private fun heldLine(run: SlotV2RunRow): String {
        val perks = perkList(run).mapNotNull { SlotV2Engine.perk(it)?.emoji }
        val curses = curseList(run).mapNotNull { SlotV2Engine.perk(it)?.let { c -> "${c.emoji}${c.name}" } }
        val sets = SlotV2Engine.activeSets(perkList(run).toSet())
        val dev = SlotV2Engine.device(run.device)
        val dev2 = SlotV2Engine.device(run.device2)
        return buildString {
            append(if (perks.isEmpty()) "보유 없음" else "보유 ${perks.joinToString("")}")
            if (curses.isNotEmpty()) append("\n🌑저주: ${curses.joinToString(" ")} (효과는 \"상태\")")
            if (dev != null) append(" 🔧${dev.emoji}${if (dev.cmd.isNotEmpty()) "(${dev.cmd})" else "(자동)"}")
            if (dev2 != null) append(" 🔧${dev2.emoji}(${if (dev2.cmd.isNotEmpty()) dev2.cmd else "자동"}·보조)")
            if (sets.isNotEmpty()) append(" · 🎯세트:${sets.joinToString(",") { it.name }}")
        }
    }

    private fun perkPickText(node: String, picks: List<SlotV2Engine.Perk>, held: Set<String> = emptySet()): String = buildString {
        append(if (node == "AUGMENT") "✨ 증강 선택!\n" else "🛡️ 유물 선택!\n")
        picks.forEachIndexed { i, p ->
            val syn = SlotV2Engine.setSynergyName(p.id, held)?.let { " 🧩$it" } ?: ""
            append("${i + 1}️⃣ ${tierMark(p.tier)}${p.emoji}${p.name} ${tierTag(p.tier)} — ${p.desc}$syn\n")
        }
        append("👉 \"1\"~\"${picks.size}\" 선택")
    }

    private fun nodeText(run: SlotV2RunRow): String = buildString {
        val mods = SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run), curseList(run))
        val quota = qOf(run.stage, mods)
        append("🗺️ 다음 경로 선택 (→ 스테이지${run.stage} 목표 ${fmt(quota)}EXP)\n")
        SlotV2Engine.bossFor(run.stage)?.let { b ->
            append("⚠️ 다음 스테이지는 👑보스 ${b.emoji}${b.name}! (${b.desc})\n")
            if (b.counterTags.isNotEmpty()) append("🎯 추천 대비: ${b.counterTags.joinToString(" · ")} — 보상을 그에 맞게!\n")
        }
        val nodes = run.pendingOptions.split(",").filter { it.isNotBlank() }
        nodes.forEachIndexed { i, n -> append("${i + 1}️⃣ ${nodeLabel(n)}\n") }
        append("👉 \"1\"~\"${nodes.size}\" 선택")
    }

    private suspend fun handleNodeSelect(run: SlotV2RunRow, t: String): Reply {
        val c = parseChoice(t) ?: return Reply.Ignore
        val nodes = run.pendingOptions.split(",").filter { it.isNotBlank() }
        if (c < 1 || c > nodes.size) return Reply.Ignore
        val node = nodes[c - 1]
        val now = System.currentTimeMillis()
        val r = rng()
        val stat = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })   // 해금 게이트 — 유물/증강 발견·RISK·상점 풀 필터

        // 성장 노드 → 퍽 선택 서브상태로
        if (node == "AUGMENT" || node == "RELIC") {
            offerPerks(run, node, now, r)?.let { return it }
            // 풀 소진 → 코인 보상으로 폴백
        }

        // 저주 노드 → 저주 1개 획득 + 코인
        if (node == "CURSE") {
            val held = curseList(run).toSet()
            val curse = SlotV2Engine.CURSES.filter { it.id !in held }.randomOrNull(r)
            if (curse != null) {
                val run2 = run.copy(
                    curses = (curseList(run) + curse.id).joinToString(","),
                    coins = run.coins + 15, state = "SPIN", pendingOptions = "", lastActionAt = now,
                )
                App.db.slotV2Run().upsert(run2)
                val mods = SlotV2Engine.buildMods(run2.machineId, run2.charId, perkList(run2), curseList(run2))
                val quota = qOf(run2.stage, mods)
                val spins = effSpins(run2, mods)
                return Reply.Msg("@${run.ownerNick} 🌑 저주 «${curse.emoji}${curse.name}» 받음! (${curse.desc}) · 코인+15\n" +
                    "${heldLine(run2)}\n🎯 스테이지${run2.stage}: ${spins}스핀 안에 ${fmt(quota)}EXP\n👉 \"잭팟\"")
            }
            // 저주 풀 소진 → 코인 폴백
        }

        // 위험한 거래 → 🌈프리즘 증강 + 🌑저주 동시 (고위험·고보상 빌드)
        if (node == "RISK") {
            val held = perkList(run).toSet(); val heldC = curseList(run).toSet()
            val augPool = SlotV2Engine.unlockedPerks(SlotV2Engine.AUGMENTS, stat)   // 해금분만(미해금 프리즘 증강 노출 차단)
            val aug = augPool.filter { it.tier == SlotV2Engine.Tier.PRISM && it.id !in held }.randomOrNull(r)
                ?: augPool.filter { it.tier == SlotV2Engine.Tier.GOLD && it.id !in held }.randomOrNull(r)   // 프리즘 소진 시 골드 폴백
            val curse = SlotV2Engine.CURSES.filter { it.id !in heldC }.randomOrNull(r)
            if (aug != null && curse != null) {
                val mark = if (aug.tier == SlotV2Engine.Tier.PRISM) "🌈" else "🥇"
                val run2 = run.copy(perks = (perkList(run) + aug.id).joinToString(","), curses = (curseList(run) + curse.id).joinToString(","), state = "SPIN", pendingOptions = "", lastActionAt = now)
                App.db.slotV2Run().upsert(run2)
                val mods = SlotV2Engine.buildMods(run2.machineId, run2.charId, perkList(run2), curseList(run2))
                val quota = qOf(run2.stage, mods); val spins = effSpins(run2, mods)
                return Reply.Msg("@${run.ownerNick} 🎲 위험한 거래!\n✨${mark}${aug.emoji}${aug.name} — ${aug.desc}\n🌑${curse.emoji}${curse.name} — ${curse.desc}\n${heldLine(run2)}\n🎯 스테이지${run2.stage}: ${spins}스핀 안에 ${fmt(quota)}EXP\n👉 \"잭팟\"")
            }
        }

        // (전용 DEVICE 노드 폐지 — 장치는 면허(영구) + EVENT 랜덤 임시장착 단일경로. 노드 풀에 "DEVICE" 없음.)

        // 상점 노드 → 코인 구매 서브상태로 ("0" 나가기 전까지 유지)
        if (node == "SHOP") {
            val run2 = run.copy(state = "EVENT_SHOP", pendingOptions = freshShopOffer(run, r, stat), lastActionAt = now)
            App.db.slotV2Run().upsert(run2)
            return Reply.Msg("@${run.ownerNick} " + shopText(run2))
        }

        var coins = run.coins
        var score = run.score
        var bonusSpins = run.stageBonusSpins
        var removedCurse: String? = null
        val grantedPerks = mutableListOf<String>()   // 이벤트로 무료 획득한 유물/증강
        val armList = run.armItems.split(",").filter { it.isNotBlank() }.toMutableList()
        val msg: String = when (node) {
            "REST" -> { coins += 8; "🛌 휴식 — 코인 +8" }
            "GAMBLE" -> {
                if (coins <= 0) "🎲 코인이 없어 도박 불발 ㅋㅋ"
                else if (r.nextBoolean()) { val g = coins; coins *= 2; "🎲 도박 성공! 🪙${fmt(g)} → ${fmt(coins)}" }
                else { val l = coins; coins = 0; "🎲 도박 실패… 🪙${fmt(l)} 날림 ㅠ" }
            }
            else -> when (r.nextInt(10)) {  // EVENT (또는 퍽풀 소진 폴백) — 코인/점수/아이템/유물/증강/특별/정화
                0 -> { coins += 15; "🎁 동전 무더기 — 코인 +15" }
                1 -> { score += 200; "🎁 보너스 점수 +200" }
                2 -> { coins += 30; "🎁 금화 발견! 코인 +30" }
                3 -> { score += 100; coins += 12; "🎁 겹경사 — 점수 +100 · 코인 +12" }
                4 -> { bonusSpins += 1; "🎁 행운의 바람! 다음 스테이지 스핀 +1" }
                5 -> { val gift = SlotV2Engine.ITEMS.filter { i -> i.kind == SlotV2Engine.IKind.NEXTSPIN }.randomOrNull(r)
                       if (gift != null) { armList.add(gift.id); "🎁 수상한 상인 — ${gift.emoji}${gift.name} 선물! (다음 스핀 발동)" }
                       else { coins += 15; "🎁 코인 +15" } }
                6 -> { coins += 15; "🎁 동전 무더기 — 코인 +15" }  // (장치 드롭/임시장착 폐지 — 장치는 업적해금+시작장착 단일경로)
                7 -> {  // 🛡️ 유물 발견 — 미보유 랜덤 유물 1개 무료(해금분만 — 미해금 유물 차단). (#6·#7) 프리즘은 가끔만.
                    val held = (perkList(run) + grantedPerks).toSet()
                    val relic = casualPerkPool(SlotV2Engine.unlockedPerks(SlotV2Engine.RELICS, stat), r).filter { it.id !in held }.randomOrNull(r)
                    if (relic != null) { grantedPerks.add(relic.id); "🛡️ 유물 발견! ${relic.emoji}${relic.name} 무료 획득 — ${relic.desc}" }
                    else { coins += 25; "🎁 코인 +25 (유물 다 모음)" }
                }
                8 -> {  // ✨ 증강 발견 / 🎉 특별 이벤트(증강+유물 동시, 25%) — 해금분만. (#6·#7) 프리즘은 가끔만.
                    val held = (perkList(run) + grantedPerks).toSet()
                    val aug = casualPerkPool(SlotV2Engine.unlockedPerks(SlotV2Engine.AUGMENTS, stat), r).filter { it.id !in held }.randomOrNull(r)
                    when {
                        aug != null && r.nextInt(4) == 0 -> {   // 25% 대박
                            grantedPerks.add(aug.id); coins += 10
                            val relic2 = casualPerkPool(SlotV2Engine.unlockedPerks(SlotV2Engine.RELICS, stat), r).filter { it.id !in (perkList(run) + grantedPerks).toSet() }.randomOrNull(r)
                            if (relic2 != null) { grantedPerks.add(relic2.id); "🎉 특별 이벤트! ${aug.emoji}${aug.name} + ${relic2.emoji}${relic2.name} 동시 획득 · 코인 +10!" }
                            else "🎉 특별 이벤트! ${aug.emoji}${aug.name} 획득 · 코인 +10!"
                        }
                        aug != null -> { grantedPerks.add(aug.id); "✨ 증강 발견! ${aug.emoji}${aug.name} 무료 획득 — ${aug.desc}" }
                        else -> { coins += 25; "🎁 코인 +25 (증강 다 모음)" }
                    }
                }
                else -> {  // 🌑 저주 해소(보유 시) 또는 꽝 (case 9)
                    val cl = curseList(run)
                    if (cl.isNotEmpty()) { removedCurse = cl.random(r); "🌑 정화의 샘 — ${SlotV2Engine.perk(removedCurse!!)?.let { "${it.emoji}${it.name}" } ?: "저주"} 해소!" }
                    else { coins += 10; "🎁 동전 +10" }
                }
            }
        }
        val newCurses = if (removedCurse != null) curseList(run).filter { it != removedCurse }.joinToString(",") else run.curses
        val newPerks = perkList(run) + grantedPerks
        val newDevice = run.device   // 장치는 시작 장착만(이벤트 임시장착 폐지)
        val mods = SlotV2Engine.buildMods(run.machineId, run.charId, newPerks, newCurses.split(",").filter { it.isNotBlank() })
        val quota = qOf(run.stage, mods)
        val run2 = run.copy(coins = coins, score = score, perks = newPerks.joinToString(","), curses = newCurses, device = newDevice, stageBonusSpins = bonusSpins, armItems = armList.joinToString(","), state = "SPIN", pendingOptions = "", lastActionAt = now)
        val spins = effSpins(run2, mods)
        App.db.slotV2Run().upsert(run2)
        if (node == "GAMBLE") track(run2, "gambles" to 1L)
        if (grantedPerks.isNotEmpty()) track(run2, *grantedPerks.map { "seen_$it" to 1L }.toTypedArray())   // 도감 발견 기록
        return Reply.Msg("@${run.ownerNick} $msg\n🎯 스테이지${run.stage}: ${spins}스핀 안에 ${fmt(quota)}EXP (🪙${fmt(coins)})\n👉 \"잭팟\"")
    }

    // ── (#6·#7) 프리즘은 보스클리어 노드 전용 — 이벤트(증강/유물 발견)·상점은 가끔(EVENT_PRISM_RATE)만 프리즘 ──
    private const val EVENT_PRISM_RATE = 0.12   // 이벤트/상점 비보스 획득에서 프리즘 등장 확률(나머지는 실버/골드)
    /** 비보스 경로(이벤트 발견/상점)용 perk 풀 — EVENT_PRISM_RATE 확률로만 프리즘 허용, 그 외엔 프리즘 제외.
     *  프리즘 제외 후 풀이 비면(전부 프리즘이거나 보유로 소진) 원본 그대로 폴백(데드엔드 방지). */
    private fun casualPerkPool(src: List<SlotV2Engine.Perk>, @Suppress("UNUSED_PARAMETER") r: Random): List<SlotV2Engine.Perk> = src   // 🎲 이벤트 발견 = 전 티어 랜덤(실버/골드/프리즘 다 등장)

    // ── (P7) 증강/유물 3택 제시 — heldAug(보류파일)·dev_major(전공편향)·dev_syllabus(티어힌트) 반영 ──
    /** 증강/유물 선택지 생성 → EVENT_AUGMENT/EVENT_RELIC 진입. 풀 소진 시 null(노드가 코인 폴백).
     *  reoffer=true(재추첨)면 heldAug 미주입(이미 보류로 보관 중인 후보는 그대로 유지) + ICLEAR 무관. */
    private suspend fun offerPerks(run: SlotV2RunRow, node: String, now: Long, r: Random, reoffer: Boolean = false): Reply? {
        val held = perkList(run).toSet()
        val stat = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })   // 해금 게이트 판정용
        val lucky = run.unluckyGauge >= SlotV2Engine.UNLUCKY_MAX   // 불운 만땅 → 희귀(골드/프리즘) 티어 보장
        val pool = if (node == "AUGMENT") SlotV2Engine.AUGMENTS else SlotV2Engine.RELICS
        val favCat = if (node == "AUGMENT") majorFavoredCat(run) else null   // dev_major 는 증강에만 편향
        // 🗂️보류파일 — 보관 후보(heldAug)는 새 후보와 함께 비교(증강 노드만). 재추첨 시엔 그대로 둠.
        val heldAug = run.heldAug.takeIf { it.isNotBlank() && node == "AUGMENT" && !reoffer && it !in held }
        // (#6·#7) 보스클리어 직후 노드면 프리즘 확정 — clearStage 에서 run.stage 가 nextStage 로 증가되므로
        //  "방금 클리어한 스테이지" = run.stage - 1. 그게 보스스테이지(5의 배수)면 이 증강/유물 노드는 프리즘.
        //  재추첨(reoffer)도 같은 노드라 동일 판정 유지(첫 제시와 일관). offerPerks 는 NODE_SELECT→AUGMENT/RELIC 직후만 호출.
        // 티어 = 클리어 스테이지 결정형(5마다 🌈프리즘·3마다 🥇골드·그외 🥈실버, 겹치면 프리즘) — 증강·유물 동일.
        //  + 운빨 10% 등급업(한 등급 위). 🗂️보류파일이면 보류 티어 우선(결정형/등급업 무시).
        val clearedStage = run.stage - 1
        val bossClear = clearedStage % 5 == 0    // 5스테이지=프리즘 배너 안내용
        val heldPerk = heldAug?.let { SlotV2Engine.perk(it) }
        val baseTier = SlotV2Engine.tierForClearedStage(clearedStage)
        var tierBumped = false
        val nodeTier = heldPerk?.tier ?: run {
            if (r.nextInt(100) < 10) { val up = SlotV2Engine.tierUp(baseTier); if (up != baseTier) { tierBumped = true; up } else baseTier } else baseTier
        }
        var picks = SlotV2Engine.pickPerksByTier(r, pool, run.stage, held, forceRare = lucky, favoredCat = favCat, stat = stat, bossClear = bossClear, forceTier = nodeTier)
        if (heldPerk != null) picks = (listOf(heldPerk) + picks.filter { it.id != heldPerk.id }).take(3)
        if (picks.isEmpty()) return null
        // 🧩 세트 시너지 조각 주입 — 증강 노드 & 보류파일 미사용(heldPerk==null) 시에만.
        //  플레이어가 짓는 중인 세트의 빠진 AUGMENT 조각 1개를 마지막 칸에 주입(메인 티어와 다를 수 있음 = 세트 완성 유도).
        //  이미 후보(또는 보유)에 있으면 주입 안 함. 유물(RELIC)·보류파일 케이스는 기존 그대로.
        // ★ 배너 티어(tName)는 메인 후보 기준 — 시너지 off-tier 조각 주입 전에 캡처(주입은 마지막 칸 교체라 first()는 안 바뀌지만 방어적으로 고정).
        val mainTier = picks.first().tier
        var synInjected = false
        // 세트 시너지 off-tier 조각 — 증강·유물 노드 모두, 운빨 ≤5%로만(평소엔 티어순수). 보류파일 미사용 시.
        if (heldPerk == null && r.nextInt(100) < 5) {
            val synCat = if (node == "AUGMENT") SlotV2Engine.PCat.AUGMENT else SlotV2Engine.PCat.RELIC
            val syn = SlotV2Engine.setSynergyAug(held, picks.map { it.id }.toSet() + held, r, synCat)
            if (syn != null && syn.id !in picks.map { it.id } && picks.size >= 2) {
                picks = (picks.dropLast(1) + syn).take(3)   // 항상 마지막 칸 교체(메인 티어 칸 보존)
                synInjected = true
            }
        }
        val st = if (node == "AUGMENT") "EVENT_AUGMENT" else "EVENT_RELIC"
        // heldAug 는 이번 제시에 소비(picks 에 합류) → run.heldAug 비움. reoffer/relic 은 유지.
        val run2 = run.copy(state = st, pendingOptions = picks.joinToString(",") { it.id },
            heldAug = if (heldAug != null) "" else run.heldAug,
            unluckyGauge = if (lucky && !reoffer) 0 else run.unluckyGauge, lastActionAt = now)
        App.db.slotV2Run().upsert(run2)
        val tName = when (mainTier) { SlotV2Engine.Tier.SILVER -> "🥈실버"; SlotV2Engine.Tier.GOLD -> "🥇골드"; else -> "🌈프리즘" }
        val stageTierB = when {
            heldPerk != null -> ""
            tierBumped -> "🍀 행운! 등급업 — $tName 등장!\n"
            nodeTier == SlotV2Engine.Tier.PRISM -> "🌈 5스테이지 보상 — 프리즘 확정!\n"
            nodeTier == SlotV2Engine.Tier.GOLD -> "🥇 3스테이지 보상 — 골드!\n"
            else -> ""
        }
        val heldB = if (heldAug != null) "🗂️ 보류 후보 포함!\n" else ""
        val synB = if (synInjected) "🧩 세트 시너지 후보 포함! (등급은 다를 수 있어 — 세트 완성용)\n" else ""
        val syllabusB = if (hasDevice(run, "dev_syllabus")) "📋 강의계획서: ${syllabusHint(run, bossClear)}\n" else ""
        val auxB = perkAuxHint(run, node)
        val banner = "$stageTierB$heldB$synB$syllabusB$tName ${if (node == "AUGMENT") "증강" else "유물"} 3택!\n"
        return Reply.Msg("@${run.ownerNick} $banner" + perkPickText(node, picks, held) + auxB, perkDetailText(picks, held))
    }

    /** 📋 강의계획서(dev_syllabus·PEEK) — 현재 노드(보스클리어=프리즘확정 반영)/다음 스테이지 티어 확률 안내(정보형, 파워 변화 없음).
     *  다음 스테이지(next)가 보스 직후가 되려면 run.stage 가 보스(5의 배수)여야 함 → 그 다음 노드는 프리즘. */
    private fun syllabusHint(run: SlotV2RunRow, bossClear: Boolean = false): String {
        val next = run.stage + 1
        val nextBossClear = SlotV2Engine.isBossStage(run.stage)   // 현재 스테이지가 보스면 next 노드가 보스클리어 프리즘
        return "S${run.stage} 현재는 ${SlotV2Engine.tierOddsHint(run.stage, bossClear)} · 다음(S$next)은 ${SlotV2Engine.tierOddsHint(next, nextBossClear)}"
    }

    /** EVENT_AUGMENT 하단 보조명령 안내(🗂️보류/🔁재추첨) — 장착 시에만 노출. */
    private fun perkAuxHint(run: SlotV2RunRow, node: String): String {
        if (node != "AUGMENT") return ""
        val parts = mutableListOf<String>()
        if (hasDevice(run, "dev_holdfile") && run.heldAug.isBlank()) parts += "🗂️\"보류 N\"(후보1개 보관→다음 증강노드서 비교)"
        if (hasDevice(run, "dev_retake")) parts += "🔁\"재추첨\"(${SlotV2Engine.RETAKE_COIN_COST}🪙·스테이지당 1회)"
        return if (parts.isEmpty()) "" else "\n🔧 보조: " + parts.joinToString(" · ")
    }

    /** 🗂️ 보류파일(dev_holdfile·ARMED) — 증강 후보 N 1개를 heldAug 에 보관(이번 즉시획득 안 함) → SPIN 으로. 다음 증강노드서 비교. */
    private suspend fun handleHoldAug(run: SlotV2RunRow, t: String): Reply {
        if (run.state != "EVENT_AUGMENT") return Reply.Msg("@${run.ownerNick} 🗂️보류는 증강 선택에서만 써 (유물 노드 X).")
        if (!hasDevice(run, "dev_holdfile")) return Reply.Msg("@${run.ownerNick} 🗂️보류파일 장치가 없어.")
        if (run.heldAug.isNotBlank()) return Reply.Msg("@${run.ownerNick} 🗂️ 이미 보류 중인 후보가 있어 (보류 슬롯 1개).")
        val ids = run.pendingOptions.split(",").filter { it.isNotBlank() }
        val n = argOf(t)
        if (n == null || n < 1 || n > ids.size)
            return Reply.Msg("@${run.ownerNick} 🗂️ 보관할 후보 번호가 필요해 — \"보류 1\"~\"보류 ${ids.size}\"")
        val perk = SlotV2Engine.perk(ids[n - 1]) ?: return Reply.Ignore
        val now = System.currentTimeMillis()
        // 보관 후 SPIN 으로 진행(이번 증강은 받지 않음). 다음 증강 노드에서 보관분+새 후보 비교.
        val run2 = run.copy(heldAug = perk.id, state = "SPIN", pendingOptions = "", lastActionAt = now)
        App.db.slotV2Run().upsert(run2)
        track(run, "deviceUses" to 1L, "seen_dev_holdfile" to 1L)
        return Reply.Msg("@${run.ownerNick} 🗂️ ${tierMark(perk.tier)}${perk.emoji}${perk.name} 보관! (${perk.desc})\n다음 증강 노드에서 새 후보와 함께 비교돼.\n${stageGoalLine(run2)}\n👉 \"잭팟\"")
    }

    /** 🔁 재시험관(dev_retake·ARMED) — 증강 선택지를 코인 소모로 1회 다시 뽑기(스테이지당 1회·usedCmds "RETAKE"). */
    private suspend fun handleRetake(run: SlotV2RunRow): Reply {
        if (run.state != "EVENT_AUGMENT" && run.state != "EVENT_RELIC")
            return Reply.Msg("@${run.ownerNick} 🔁재추첨은 증강/유물 선택에서만 써.")
        if (!hasDevice(run, "dev_retake")) return Reply.Msg("@${run.ownerNick} 🔁재시험관 장치가 없어.")
        if ("RETAKE" in run.usedCmds.split(",")) return Reply.Msg("@${run.ownerNick} 🔁 재추첨은 이번 스테이지에 이미 썼어 (스테이지당 1회).")
        val cost = SlotV2Engine.RETAKE_COIN_COST
        if (run.coins < cost) return Reply.Msg("@${run.ownerNick} 🔁 재추첨엔 ${cost}🪙 필요 (보유 ${fmt(run.coins)}🪙)")
        val node = if (run.state == "EVENT_AUGMENT") "AUGMENT" else "RELIC"
        val now = System.currentTimeMillis()
        val newUsed = (run.usedCmds.split(",").filter { it.isNotBlank() } + "RETAKE").distinct().joinToString(",")
        val spent = run.copy(coins = run.coins - cost, usedCmds = newUsed)
        track(run, "deviceUses" to 1L, "seen_dev_retake" to 1L)
        // 재추첨(reoffer=true): heldAug 미주입(보관분 보존) + 불운게이지 미소비. 풀 소진 시 기존 선택지 유지.
        return offerPerks(spent, node, now, rng(), reoffer = true)
            ?: Reply.Msg("@${run.ownerNick} 🔁 새로 뽑을 후보가 없어 — 그대로 골라줘.")
    }

    private suspend fun handlePerkPick(run: SlotV2RunRow, t: String): Reply {
        val c = parseChoice(t) ?: return Reply.Ignore
        val ids = run.pendingOptions.split(",").filter { it.isNotBlank() }
        if (c < 1 || c > ids.size) return Reply.Ignore
        val perk = SlotV2Engine.perk(ids[c - 1]) ?: return Reply.Ignore
        val now = System.currentTimeMillis()
        val newPerks = (perkList(run) + perk.id).joinToString(",")
        val run2 = run.copy(perks = newPerks, state = "SPIN", pendingOptions = "", lastActionAt = now)
        App.db.slotV2Run().upsert(run2)
        val mods = SlotV2Engine.buildMods(run2.machineId, run2.charId, perkList(run2), curseList(run2))
        val quota = qOf(run2.stage, mods)
        val spins = effSpins(run2, mods)
        val seen = linkedMapOf("seen_${perk.id}" to 1L)   // 도감 사용기록 (증강/유물 + 발동 세트)
        SlotV2Engine.activeSets(perkList(run2).toSet()).forEach { seen["seen_${it.id}"] = 1L }
        val sb = achBanner(bumpAch(run2, prism = if (perk.tier == SlotV2Engine.Tier.PRISM) 1 else 0, inc = seen))
        return Reply.Msg("@${run2.ownerNick} ${tierMark(perk.tier)}${perk.emoji}${perk.name} 획득! (${perk.desc})\n" +
            "${heldLine(run2)}\n🎯 스테이지${run2.stage}: ${spins}스핀 안에 ${fmt(quota)}EXP\n👉 \"잭팟\"$sb")
    }

    // ── 상점 (코인 구매, 멀티) ──────────────────────────────
    private fun shopEntryLabel(entry: String): String {
        val p = entry.split(":")
        return when (p[0]) {
            "A" -> SlotV2Engine.perk(p[1])?.let { "✨${tierMark(it.tier)}${it.emoji}${it.name} — ${it.desc} (${p[2]}🪙)" } ?: entry
            "R" -> SlotV2Engine.perk(p[1])?.let { "🛡️${it.emoji}${it.name} — ${it.desc} (${p[2]}🪙)" } ?: entry
            else -> SlotV2Engine.item(p[1])?.let { "${it.emoji}${it.name} — ${it.desc} (${p[2]}🪙)" } ?: entry
        }
    }

    private const val SHOP_REROLL = 6
    private fun augShopPrice(t: SlotV2Engine.Tier): Int = when (t) {
        SlotV2Engine.Tier.SILVER -> 14; SlotV2Engine.Tier.GOLD -> 24; SlotV2Engine.Tier.PRISM -> 36
    }
    private fun freshShopOffer(run: SlotV2RunRow, r: Random, stat: Map<String, Long> = emptyMap()): String {
        val held = perkList(run).toSet()
        // 미해금 증강/유물은 상점에 안 올라옴(전부잠김이면 BASE 폴백 — pickAugments/pickRelics 내부 gatedPool).
        // (#6·#7) 프리즘은 보스클리어 노드 전용 — 상점은 EVENT_PRISM_RATE 확률로만 프리즘 포함(나머지는 실버/골드).
        //   over-draw(2칸의 +2) 후 프리즘 게이트 적용해 2개로 추림. 비프리즘 부족 시 프리즘 폴백(데드엔드 방지).
        val allowPrism = r.nextDouble() < EVENT_PRISM_RATE
        fun gatePrism(list: List<SlotV2Engine.Perk>): List<SlotV2Engine.Perk> {
            if (allowPrism) return list.take(2)
            val noPrism = list.filter { it.tier != SlotV2Engine.Tier.PRISM }
            return (noPrism.ifEmpty { list }).take(2)
        }
        val augs = gatePrism(SlotV2Engine.pickAugments(r, run.stage, held, 4, stat)).map { "A:${it.id}:${augShopPrice(it.tier)}" }
        val relics = gatePrism(SlotV2Engine.pickRelics(r, held, 4, stat)).map { "R:${it.id}:${it.price}" }
        val items = SlotV2Engine.pickItems(r, 2).map { "I:${it.id}:${it.coinCost}" }
        return (augs + relics + items).shuffled(r).joinToString(",")
    }

    private fun shopText(run: SlotV2RunRow): String = buildString {
        append("🛒 상점 · 보유 🪙${fmt(run.coins)}\n")
        val entries = run.pendingOptions.split(",").filter { it.isNotBlank() }
        if (entries.isEmpty()) append("(다 팔렸어요)\n")
        entries.forEachIndexed { i, e -> append("${i + 1}️⃣ ${shopEntryLabel(e)}\n") }
        append("${entries.size + 1}️⃣ 🔄 목록 새로고침 (${SHOP_REROLL}🪙)\n")
        append("👉 번호 입력 = 구매/새로고침 · \"0\" = 나가기")
    }

    private fun instantQuota(run: SlotV2RunRow): Long {
        // clearStage 와 동일한 실제 클리어 요구치 — phaseItems(quotaMul 변경 등)까지 applyItemMods 로 반영.
        val phase = run.phaseItems.split(",").filter { it.isNotBlank() }
        val mods = SlotV2Engine.applyItemMods(
            SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device), phase)
        return qOf(run.stage, mods)
    }

    private fun applyItemPurchase(run: SlotV2RunRow, itm: SlotV2Engine.Item, stat: Map<String, Long> = emptyMap()): SlotV2RunRow = when (itm.kind) {
        SlotV2Engine.IKind.NEXTSPIN -> run.copy(armItems = (run.armItems.split(",").filter { it.isNotBlank() } + itm.id).joinToString(","))
        SlotV2Engine.IKind.PHASE -> run.copy(phaseItems = (run.phaseItems.split(",").filter { it.isNotBlank() } + itm.id).joinToString(","))
        SlotV2Engine.IKind.INSTANT -> when (itm.id) {
            "first_aid" -> run.copy(stageBonusSpins = run.stageBonusSpins + 1)
            "double_aid" -> run.copy(stageBonusSpins = run.stageBonusSpins + 2)
            "cram" -> run.copy(stageExp = run.stageExp + instantQuota(run) * 15 / 100)
            "cheat_sheet" -> run.copy(stageExp = run.stageExp + instantQuota(run) * 30 / 100)
            "answer_sheet" -> run.copy(stageExp = run.stageExp + instantQuota(run) * 50 / 100)
            "honor_roll" -> run.copy(stageExp = run.stageExp + instantQuota(run) * 70 / 100)
            "grad_cert" -> run.copy(stageExp = run.stageExp + instantQuota(run))
            "dev_battery" -> run.copy(armItems = (run.armItems.split(",").filter { it.isNotBlank() } + "dev_coin").joinToString(","))  // 🔋 다음 스핀 EXP +30%(dev_coin 레버 재사용)
            "score_sticker" -> run.copy(score = run.score + 150)    // 점수 +150
            "old_coin" -> run.copy(coins = run.coins + 6)           // 코인 +6
            // ── 단순 INSTANT (2026-06-24) ──
            "grad_copy" -> run.copy(stageExp = run.stageExp + instantQuota(run) * 80 / 100, score = (run.score * 9 / 10).coerceAtLeast(0))
            "score_calc" -> run.copy(score = run.score + run.score * 30 / 100)
            "mini_coupon" -> run.copy(coins = run.coins + 9)
            "price_hack" -> run.copy(coins = run.coins + 18)
            // ── 복잡 INSTANT (2026-06-24) ──
            "grad_ring" -> { val short = instantQuota(run) - run.stageExp
                if (short in 0..20) run.copy(stageExp = instantQuota(run)) else run }
            "gold_grad_bell" -> { val short = instantQuota(run) - run.stageExp
                if (short in 0..50) run.copy(stageExp = instantQuota(run)) else run }
            "insurance_cert" -> run.copy(survive = true)
            "debt_note" -> run.copy(coins = run.coins + 30, debtStages = 4)
            "black_lottery" -> {
                val g = rng()
                if (g.nextBoolean()) {
                    val held = perkList(run)
                    // 해금분(골드 유물)만 — 미해금 유물 지급 차단(stat 빈 맵이면 필터 없음).
                    val rel = SlotV2Engine.gatedPool(SlotV2Engine.RELICS, stat).filter { it.tier == SlotV2Engine.Tier.GOLD && it.id !in held }.randomOrNull(g)
                    if (rel != null) run.copy(perks = (held + rel.id).joinToString(",")) else run.copy(coins = run.coins + 15)
                } else {
                    val cur = curseList(run)
                    val c = SlotV2Engine.CURSES.filter { it.id !in cur }.randomOrNull(g)
                    if (c != null) run.copy(curses = (cur + c.id).joinToString(",")) else run
                }
            }
            "devil_contract" -> {
                val g = rng(); val held = perkList(run); val cur = curseList(run)
                // 해금분 유물만 — 미해금 유물 지급 차단.
                val rel = SlotV2Engine.gatedPool(SlotV2Engine.RELICS, stat).filter { it.id !in held }.randomOrNull(g)
                val c = SlotV2Engine.CURSES.filter { it.id !in cur }.randomOrNull(g)
                run.copy(
                    perks = if (rel != null) (held + rel.id).joinToString(",") else run.perks,
                    curses = if (c != null) (cur + c.id).joinToString(",") else run.curses,
                    coins = run.coins + 25,
                )
            }
            "broken_prism" -> {
                val g = rng()
                // 스테이지-안전 프리즘만(스핀-2류 short_day/glass_cannon/all_in/endgame_rush 제외)
                val safe = listOf("overdrive", "supernova", "wild_world", "joker", "seed_garden",
                    "great_harvest", "jackpot", "mega_jackpot", "gamblers_dice", "key_master", "time_warp")
                // 해금분만(미해금 프리즘 효과 차단) → 전부 미해금이면 안전 폴백 overdrive.
                val avail = safe.filter { it !in perkList(run) && SlotV2Engine.perkUnlocked(it, stat) }
                val pick = avail.randomOrNull(g) ?: safe.filter { it !in perkList(run) }.randomOrNull(g) ?: "overdrive"
                run.copy(phasePerks = pick)
            }
            "timeline_ticket" -> {
                val g = rng()
                // handleSpin 과 동일한 분포: buildMods(...,run.device) + 패시브 장치 + phaseItems applyItemMods.
                var pmods = SlotV2Engine.applyItemMods(
                    SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device),
                    run.phaseItems.split(",").filter { it.isNotBlank() })
                val devEq = SlotV2Engine.device(run.device)
                if (devEq?.kind == SlotV2Engine.DevKind.PASSIVE) pmods = SlotV2Engine.applyPassiveDevice(pmods, devEq.id)
                val reel = if (devEq?.id == "dev_subreel") SlotV2Engine.REEL + 1 else SlotV2Engine.REEL
                val spins = effSpins(run, pmods)
                val a = SlotV2Engine.rollRaw(g, pmods, reel, run.seedNext)
                val b = SlotV2Engine.rollRaw(g, pmods, reel, run.seedNext)
                val ea = SlotV2Engine.evaluate(g, a, pmods, run.spinIndex, spins).exp
                val eb = SlotV2Engine.evaluate(g, b, pmods, run.spinIndex, spins).exp
                val win = if (eb > ea) b else a
                run.copy(lockedNext = win.joinToString(",") { it.sym.id })
            }
            "retake_form" -> run   // 즉발 X → handleItem 특수분기서 처리(아래 4-e)
            else -> run
        }
    }

    // ── 🎒 아이템 가방 (상점서 구매→보관, 스핀 중 "아이템 N"으로 사용) ──
    private fun heldItems(run: SlotV2RunRow): List<SlotV2Engine.Item> =
        run.items.split(",").filter { it.isNotBlank() }.mapNotNull { SlotV2Engine.item(it) }
    private fun itemKindLabel(k: SlotV2Engine.IKind): String = when (k) {
        SlotV2Engine.IKind.NEXTSPIN -> "다음스핀"; SlotV2Engine.IKind.PHASE -> "이번스테이지"; SlotV2Engine.IKind.INSTANT -> "즉시"
    }
    private fun itemBagText(run: SlotV2RunRow): String {
        val held = heldItems(run)
        if (held.isEmpty()) return "@${run.ownerNick} 🎒 가방이 비었어 — 🛒상점에서 아이템을 사면 여기 보관됐다가 \"아이템 N\"으로 써."
        return buildString {
            append("@${run.ownerNick} 🎒 아이템 (${held.size}/$ITEM_SLOTS)\n")
            held.forEachIndexed { i, it -> append("${i + 1}️⃣ ${it.emoji}${it.name} [${itemKindLabel(it.kind)}] — ${it.desc}\n") }
            append("👉 \"아이템 N\" 으로 사용")
        }
    }

    private suspend fun handleItem(run: SlotV2RunRow, t: String): Reply {
        val held = heldItems(run)
        val n = argOf(t)
        if (n == null) return Reply.Msg(itemBagText(run))   // "아이템"/"가방" = 목록만 (어디서든)
        if (held.isEmpty()) return Reply.Msg(itemBagText(run))
        if (run.state != "SPIN") return Reply.Msg("@${run.ownerNick} 🎒 아이템은 스핀 돌릴 때(잭팟 단계)에만 사용돼. 먼저 진행해줘.\n" + itemBagText(run))
        if (n < 1 || n > held.size) return Reply.Msg("@${run.ownerNick} 🎒 1~${held.size} 중에 골라줘 (\"아이템\"으로 목록)")
        val itm = held[n - 1]
        val now = System.currentTimeMillis()
        // (C1) 즉시클리어/대량스킵형 아이템은 스테이지당 1회 (usedCmds "ICLEAR" 마커로 캡, 클리어 시 초기화)
        if (SlotV2Engine.isInstantClearItem(itm.id) && "ICLEAR" in run.usedCmds.split(","))
            return Reply.Msg("@${run.ownerNick} 🎓 즉시클리어/대량돌파 아이템은 이번 스테이지에 이미 썼어 (스테이지당 1회). 스핀으로 마무리하거나 다음 스테이지에서!")
        val remaining = run.items.split(",").filter { it.isNotBlank() }.toMutableList()
        remaining.removeAt(n - 1)
        // 📄 재시험(h) — 직전 스핀 전체 다시 굴림(즉발 X, 특수 처리). 기존 remaining 재사용(재-remove 금지).
        if (itm.id == "retake_form") {
            if (run.lastCells.isBlank() || run.lastSpinNo < 0)
                return Reply.Msg("@${run.ownerNick} 📄 재시험은 직전 스핀이 있어야 써(아직 안 굴림).")
            // handleSpin 과 동일한 분포: buildMods(...,run.device) + 패시브 장치 + phaseItems applyItemMods.
            var pmods = SlotV2Engine.applyItemMods(
                SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device),
                run.phaseItems.split(",").filter { it.isNotBlank() })
            val devEq = SlotV2Engine.device(run.device)
            if (devEq?.kind == SlotV2Engine.DevKind.PASSIVE) pmods = SlotV2Engine.applyPassiveDevice(pmods, devEq.id)
            val spins = effSpins(run, pmods)
            val rr = rng()
            val newRaw = MutableList(run.lastCells.split(",").filter { it.isNotBlank() }.size) { SlotV2Engine.rollOne(rr, pmods) }
            val res2 = SlotV2Engine.evaluate(rr, newRaw, pmods, run.lastSpinNo, spins, run.flameNext)
            val run2 = run.copy(
                items = remaining.joinToString(","), lastActionAt = now, usedItemThisRun = true,
                stageExp = (run.stageExp - run.lastGain).coerceAtLeast(0) + res2.exp,
                score = (run.score - run.lastScoreGain + res2.score).coerceAtLeast(0),
                coins = (run.coins - run.lastCoinGain + res2.coins).coerceAtLeast(0),
                lastCells = newRaw.joinToString(",") { it.sym.id }, lastGain = res2.exp,
                lastScoreGain = res2.score, lastCoinGain = res2.coins,
            )
            App.db.slotV2Run().upsert(run2)
            track(run, "itemsUsed" to 1L, "seen_retake_form" to 1L)
            return Reply.Msg("@${run.ownerNick} 📄 재시험! ${SlotV2Engine.render(res2.cells)}\n→ +${fmt(res2.exp)}EXP (게이지 ${fmt(run2.stageExp)}/${fmt(instantQuota(run2))})")
        }
        // (C1) 즉시클리어형이면 "ICLEAR" 마커 기록(스테이지당 1회 캡 — clearStage 시 usedCmds 초기화로 리셋)
        val usedNow = if (SlotV2Engine.isInstantClearItem(itm.id))
            (run.usedCmds.split(",").filter { it.isNotBlank() } + "ICLEAR").distinct().joinToString(",")
        else run.usedCmds
        var run2 = run.copy(items = remaining.joinToString(","), usedCmds = usedNow, lastActionAt = now, usedItemThisRun = true)
        // 해금 게이트 — black_lottery/devil_contract/broken_prism 의 유물·프리즘 지급을 해금분으로 제한.
        val itemStat = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
        run2 = applyItemPurchase(run2, itm, itemStat)   // 종류별 적용(arm/phase/instant)
        // 💍🔔 졸업반지/황금졸업벨(g) — 즉시클리어 도달 시 clearStage 연출
        if ((itm.id == "grad_ring" || itm.id == "gold_grad_bell") && run2.stageExp >= instantQuota(run2)) {
            App.db.slotV2Run().upsert(run2)
            val pmods = SlotV2Engine.applyItemMods(
                SlotV2Engine.buildMods(run2.machineId, run2.charId, perkList(run2) + phasePerkList(run2), curseList(run2)),
                run2.phaseItems.split(",").filter { it.isNotBlank() })
            val spins = effSpins(run2, pmods); val quota = qOf(run2.stage, pmods)
            val dummy = SlotV2Engine.evaluate(rng(), SlotV2Engine.rollRaw(rng(), pmods), pmods, run2.spinIndex, spins)
            return clearStage(run2, dummy, run2.stageExp, run2.score, run2.coins, run2.spinIndex, spins, quota,
                "@${run.ownerNick} ${itm.emoji}${itm.name} 발동! 즉시 클리어!")
        }
        App.db.slotV2Run().upsert(run2)
        track(run, "itemsUsed" to 1L, "seen_${itm.id}" to 1L)   // 도감 사용기록
        val eff = when (itm.kind) {
            SlotV2Engine.IKind.NEXTSPIN -> "다음 스핀에 발동!"
            SlotV2Engine.IKind.PHASE -> "이번 스테이지 내내 적용!"
            SlotV2Engine.IKind.INSTANT -> "즉시 발동!"
        }
        return Reply.Msg("@${run.ownerNick} 🎒 ${itm.emoji}${itm.name} 사용 — $eff\n${stageGoalLine(run2)}\n👉 \"잭팟\"")
    }

    private fun stageGoalLine(run: SlotV2RunRow): String {
        val phase = run.phaseItems.split(",").filter { it.isNotBlank() }
        val mods = SlotV2Engine.applyItemMods(
            SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device), phase)
        val quota = qOf(run.stage, mods)
        val spins = effSpins(run, mods)
        val boss = SlotV2Engine.bossFor(run.stage)?.let { "\n👑BOSS ${it.emoji}${it.name}: ${it.desc}" } ?: ""
        val bag = heldItems(run).size.let { if (it > 0) " · 🎒$it" else "" }
        // (#11-a) 스핀 카운터 — 다음에 돌릴 스핀 번호/총스핀·남은 횟수를 눈에 띄게 강조
        return "${spinCounterLine(run, spins, upcoming = true)}\n🎯 스테이지${run.stage}: ${fmt(quota)}EXP (게이지 ${fmt(run.stageExp)} · 🪙${fmt(run.coins)}$bag)$boss"
    }

    /** (#11-a) 스핀 카운터 강조줄 — "🎰 스핀 N/M · 남은 K번".
     *  @param spinNo 표시할 스핀 번호(1-base). null 이면 run.spinIndex+1(다음 돌릴 스핀) 사용.
     *  @param upcoming true=앞으로 돌릴 스핀 안내(곧 N번째), false=방금 돌린 결과 안내(N번째 완료). */
    private fun spinCounterLine(run: SlotV2RunRow, spins: Int, spinNo: Int? = null, upcoming: Boolean = false): String {
        val n = (spinNo ?: (run.spinIndex + 1)).coerceIn(1, spins)
        val left = (spins - n + (if (upcoming) 1 else 0)).coerceAtLeast(0)
        val bossTag = SlotV2Engine.bossFor(run.stage)?.let { " 👑${it.emoji}${it.name}" } ?: ""
        val lastTag = if (n >= spins) " ⏰마지막!" else ""
        return "🎰 스핀 $n/$spins$bossTag · 남은 ${left}번$lastTag"
    }

    private val REROLL_WORDS = setOf("리롤", "새로고침", "새로", "다시", "리롤하기", "갱신")
    private suspend fun handleShop(run: SlotV2RunRow, t: String): Reply {
        val now = System.currentTimeMillis()
        val entries = run.pendingOptions.split(",").filter { it.isNotBlank() }
        val c = parseChoice(t)
        // 🔄 리롤 — "리롤" 글자 또는 마지막 번호(entries.size+1)
        if (t in REROLL_WORDS || c == entries.size + 1) {
            if (run.coins < SHOP_REROLL) return Reply.Msg("@${run.ownerNick} 🪙 리롤은 ${SHOP_REROLL}🪙 필요! (현재 ${fmt(run.coins)}🪙)\n번호로 구매하거나 \"0\" 나가기\n" + shopText(run))
            val rerollStat = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })   // 해금 게이트 — 미해금 perk 상점 제외
            val run2 = run.copy(coins = run.coins - SHOP_REROLL, pendingOptions = freshShopOffer(run, rng(), rerollStat), lastActionAt = now)
            App.db.slotV2Run().upsert(run2)
            return Reply.Msg("@${run.ownerNick} 🔄 목록 새로고침! (-${SHOP_REROLL}🪙)\n" + shopText(run2))
        }
        if (c == null) return Reply.Ignore
        // 나가기 — "0"
        if (c == 0) {
            val run2 = run.copy(state = "SPIN", pendingOptions = "", lastActionAt = now)
            App.db.slotV2Run().upsert(run2)
            return Reply.Msg("@${run.ownerNick} 🛒 상점 나감.\n${stageGoalLine(run2)}\n👉 \"잭팟\"")
        }
        if (c < 1 || c > entries.size) return Reply.Msg("@${run.ownerNick} 🛒 ${c}번은 없어! [1~${entries.size}]=구매 · [${entries.size + 1}]=새로고침(${SHOP_REROLL}🪙) · [0]=나가기\n" + shopText(run))
        val p = entries[c - 1].split(":")
        val cost = p.getOrNull(2)?.toIntOrNull() ?: return Reply.Ignore
        if (run.coins < cost) {
            return Reply.Msg("@${run.ownerNick} 🪙 코인 부족! (필요 $cost · 보유 ${fmt(run.coins)})\n다른 번호 / \"리롤\" / \"0\" 나가기")
        }
        // 🎒 아이템은 사면 가방에 보관(즉시 적용 X) → 스핀 중 "아이템 N"으로 원할 때 사용
        if (p[0] != "R" && p[0] != "A") {
            val held = run.items.split(",").filter { it.isNotBlank() }
            if (held.size >= ITEM_SLOTS) return Reply.Msg("@${run.ownerNick} 🎒 가방이 가득(${ITEM_SLOTS}칸)! 스핀 중 \"아이템\"으로 먼저 써줘.\n" + shopText(run))
        }
        // RUNSHOP — 이번 런에 상점을 1회라도 사용했음(검소한졸업 도전 판정용, 런 끝까지 유지)
        val usedNow = (run.usedCmds.split(",").filter { it.isNotBlank() } + "RUNSHOP").distinct().joinToString(",")
        var run2 = run.copy(coins = run.coins - cost, usedCmds = usedNow, lastActionAt = now)
        val bought: String
        if (p[0] == "R" || p[0] == "A") {
            val perk = SlotV2Engine.perk(p[1])
            run2 = run2.copy(perks = (perkList(run) + p[1]).joinToString(","))
            bought = perk?.let { "${tierMark(it.tier)}${it.emoji}${it.name}" } ?: p[1]
        } else {
            val itm = SlotV2Engine.item(p[1]) ?: return Reply.Ignore
            run2 = run2.copy(items = (run.items.split(",").filter { it.isNotBlank() } + itm.id).joinToString(","))
            bought = "${itm.emoji}${itm.name} 🎒보관"
        }
        // 구매 후에도 상점 유지 (남은 목록 — 비어도 "0"으로만 나감)
        val remaining = entries.filterIndexed { i, _ -> i != c - 1 }
        run2 = run2.copy(pendingOptions = remaining.joinToString(","))
        App.db.slotV2Run().upsert(run2)
        val shopInc = mutableListOf("shopBuys" to 1L)
        if (p[0] == "R" || p[0] == "A") shopInc += "seen_${p[1]}" to 1L   // 도감: 상점서 산 증강/유물도 기록
        track(run2, *shopInc.toTypedArray())
        val useHint = if (p[0] != "R" && p[0] != "A") "  (🎒가방 → 스핀 중 \"아이템\"으로 사용!)" else ""
        return Reply.Msg("@${run.ownerNick} $bought 구매!$useHint 🪙${fmt(run2.coins)} 남음\n\n" + shopText(run2))
    }

    // ── 장치 (장착형 액티브) ────────────────────────────────
    private suspend fun handleDevice(run: SlotV2RunRow, t: String): Reply {
        // 🎲 도박꾼 무료 재굴림 (장치 무관·스테이지당 1회·점수 패널티 없음) — dev_reroll 장착보다 우선
        if (run.charId == "gambler" && cmdOf(t) == "재굴림") {
            if ("GREROL" !in run.usedCmds.split(",")) return handleGamblerReroll(run, fromPost = false)
            if (run.device != "dev_reroll")
                return Reply.Msg("@${run.ownerNick} 🎲 무료 재굴림은 이번 스테이지에 다 썼어 (다음 스테이지에 다시!)")
            // 무료분 소진 + dev_reroll 도 장착 → 아래 일반 장치(유료 재굴림) 로직으로
        }
        val dev = SlotV2Engine.deviceByCmd(cmdOf(t)) ?: return Reply.Ignore
        val now = System.currentTimeMillis()
        // 메인(run.device) 또는 보조(run.device2) 중 어느 슬롯의 명령인지 판별 (보조면 효과 약화)
        val isSecondary = (run.device != dev.id && run.device2 == dev.id)
        if (run.device != dev.id && run.device2 != dev.id) {
            val cur = SlotV2Engine.device(run.device)
            val cur2 = SlotV2Engine.device(run.device2)
            val have = listOfNotNull(cur?.let { "${it.emoji}${it.name} ${it.cmd}" }, cur2?.let { "${it.emoji}${it.name}(보조) ${it.cmd}" })
            return Reply.Msg("@${run.ownerNick} ${dev.emoji}${dev.name} 장치 없음 (보유: ${if (have.isEmpty()) "없음" else have.joinToString(" · ")})")
        }
        val secTag = if (isSecondary) "(보조)" else ""
        val used = run.usedCmds.split(",").filter { it.isNotBlank() }
        if (dev.cmd in used) return Reply.Msg("@${run.ownerNick} ${dev.emoji}${dev.name}$secTag 은 이번 스테이지에 이미 썼어 (스테이지당 1회)")
        // 🔧 직전결과 조작(MANIP) — 메인 전용(보조엔 MANIP 불가). 스핀 소모 없이 직전 결과 변경
        if (dev.kind == SlotV2Engine.DevKind.MANIP) return handleManipulator(run, dev, argOf(t), fromPost = false)
        // 🔮 예언(PEEK) — 다음 스핀 미리 굴려 확정 (보조도 그대로 허용·약화 없음, 보조 표시만)
        if (dev.kind == SlotV2Engine.DevKind.PEEK) {
            val phase = run.phaseItems.split(",").filter { it.isNotBlank() }
            val mods = SlotV2Engine.applyItemMods(SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run), curseList(run)), phase)
            val raw = SlotV2Engine.rollRaw(rng(), mods, SlotV2Engine.REEL, run.seedNext)
            // 🔮예언안경 실제 사용 마커(RUNORACLE) — bld_jackpot_seer '예언 사용후 잭팟' 판정용(장착≠사용). 런 끝까지 유지(RUNSHOP 패턴).
            val usedWithMark = if (dev.id == "dev_oracle") used + dev.cmd + "RUNORACLE" else used + dev.cmd
            val run2 = run.copy(lockedNext = raw.joinToString(",") { it.sym.id }, usedCmds = usedWithMark.distinct().joinToString(","), lastActionAt = now)
            App.db.slotV2Run().upsert(run2)
            track(run, "deviceUses" to 1L, "seen_${dev.id}" to 1L)
            return Reply.Msg("${deviceBanner(dev, isSecondary)}@${run.ownerNick} 다음 스핀은 이렇게 나와:\n${SlotV2Engine.render(raw)}\n👉 \"잭팟\" 으로 확정 (집중/올인 등도 가능)")
        }
        // ⚡ 능동(ARMED) — 다음 스핀에 발동 (코인투입 / 비상). 보조 슬롯이면 ARMED 수치 약화.
        val arm = run.armItems.split(",").filter { it.isNotBlank() }.toMutableList()
        var coins = run.coins
        var pendMul = run.pendingNextExpMul
        val msg: String = when (dev.id) {
            "dev_coin" -> {
                if (coins < 5) return Reply.Msg("@${run.ownerNick} 🪙 코인 부족 (5 필요 · 보유 ${fmt(coins)})")
                coins -= 5
                if (isSecondary) {
                    // 보조 코인투입 — armItems(전체효과 1.3) 대신 pendingNextExpMul 로 약화 적용(1.18)
                    pendMul *= SlotV2Engine.secondaryMul(1.3)
                    "🪙투입(보조)! 코인5 → 다음 스핀 EXP +${fmt2((SlotV2Engine.secondaryMul(1.3) - 1.0) * 100)}% (약화)"
                } else { arm.add("dev_coin"); "🪙투입! 코인5 → 다음 스핀 EXP +30%" }
            }
            "dev_bell" -> {
                val phase = run.phaseItems.split(",").filter { it.isNotBlank() }
                val mods = SlotV2Engine.applyItemMods(SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run), curseList(run)), phase)
                val quota = qOf(run.stage, mods)
                val short = quota - run.stageExp
                if (short > 25) return Reply.Msg("@${run.ownerNick} 🔔비상은 부족 EXP ≤25 일 때만! (현재 부족 ${fmt(short)})")
                arm.add("dev_bell"); "🔔비상졸업벨 장전! 다음 \"잭팟\"에 즉시 클리어 (이번 런 장착 해제)"
            }
            // (P7) 보류/재추첨은 증강 선택(EVENT_AUGMENT) 단계에서만 동작 — 스핀 중엔 안내만
            "dev_holdfile" -> return Reply.Msg("@${run.ownerNick} 🗂️보류는 ✨증강 선택 중에 \"보류 N\"으로 써.")
            "dev_retake" -> return Reply.Msg("@${run.ownerNick} 🔁재추첨은 ✨증강/유물 선택 중에 \"재추첨\"으로 써.")
            else -> return Reply.Ignore
        }
        val run2 = run.copy(armItems = arm.joinToString(","), coins = coins, pendingNextExpMul = pendMul,
            usedCmds = (used + dev.cmd).joinToString(","), lastActionAt = now)
        App.db.slotV2Run().upsert(run2)
        track(run, "deviceUses" to 1L, "seen_${dev.id}" to 1L)
        return Reply.Msg("${deviceBanner(dev, isSecondary)}@${run.ownerNick} $msg\n👉 \"잭팟\" 으로 스핀!")
    }

    private fun bestValueId(cells: List<SlotV2Engine.Cell>): String? {
        val counts = HashMap<String, Int>()
        for (c in cells) if (c.sym.id in setOf("cherry", "book", "star", "gem", "crown")) counts[c.sym.id] = (counts[c.sym.id] ?: 0) + 1
        return counts.maxByOrNull { it.value }?.key
    }

    /** 🎲 도박꾼 전용 무료 재굴림 — 직전 스핀 전체 재굴림. 점수 패널티 없음, 스테이지당 1회(usedCmds "GREROL"). handleSpin과 동일한 mods(장치·패시브·phasePerks) 사용. */
    private suspend fun handleGamblerReroll(run: SlotV2RunRow, fromPost: Boolean): Reply {
        val now = System.currentTimeMillis()
        if (run.lastCells.isBlank() || run.lastSpinNo < 0)
            return Reply.Msg("@${run.ownerNick} 🎲 재굴림은 직전 스핀이 있어야 써 — 먼저 \"잭팟\" 으로 돌려!")
        track(run, "rerollUses" to 1L)
        val phase = run.phaseItems.split(",").filter { it.isNotBlank() }
        val preMods0 = SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device)
        val rrCtx = runCtxOf(run, run.lastSpinNo, SlotV2Engine.spinsPerStage(preMods0), qOf(run.stage, preMods0))
        var mods = SlotV2Engine.applyItemMods(
            SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run) + phasePerkList(run), curseList(run), run.device, rrCtx), phase)
        val devEq = SlotV2Engine.device(run.device)
        if (devEq?.kind == SlotV2Engine.DevKind.PASSIVE) mods = SlotV2Engine.applyPassiveDevice(mods, devEq.id)
        val spins = effSpins(run, mods)
        val quota = qOf(run.stage, mods)
        val r = rng()
        val raw = SlotV2Engine.cellsFromIds(run.lastCells.split(",").filter { it.isNotBlank() })
        if (raw.isEmpty()) return Reply.Msg("@${run.ownerNick} 직전 결과 복원 실패 — 그냥 \"잭팟\" 으로 진행!")
        for (i in raw.indices) raw[i] = SlotV2Engine.rollOne(r, mods)
        val hasPrism = perkList(run).any { SlotV2Engine.perk(it)?.tier == SlotV2Engine.Tier.PRISM }
        val capMul = SlotV2Engine.capMulFor(run.stage, hasPrism)
        val res = SlotV2Engine.evaluate(r, raw, mods, run.lastSpinNo, spins, false, capMul = capMul)
        var gained = res.exp
        val boss = SlotV2Engine.bossFor(run.stage)
        if (boss != null) gained = applyBoss(boss, gained, res, run.lastSpinNo, spins,
            if (spins > 0) quota.toDouble() / spins else 0.0, perkList(run).size).first
        val newExp = (run.stageExp - run.lastGain + gained).coerceAtLeast(0)
        val newScore = (run.score - run.lastScoreGain + res.score).coerceAtLeast(0)
        val newCoins = (run.coins - run.lastCoinGain + res.coins).coerceAtLeast(0)
        val used = run.usedCmds.split(",").filter { it.isNotBlank() }
        val m = SlotV2Engine.machine(run.machineId)
        val header = "${m.emoji}${m.name} @${run.ownerNick} S${run.stage} #${run.lastSpinNo + 1}/$spins 🎲재굴림"
        // 🎲 도박꾼 무료 재굴림 발동 배너 — 결과 최상단 1줄(장치 아님·캐릭터 능력, 효과=직전 스핀 전체 재굴림·점수패널티 없음·스테이지당 1회).
        val gReroBanner = "🎲 도박꾼 무료 재굴림 발동! — 직전 스핀 전체 재굴림 (점수 패널티 없음·스테이지당 1회)\n"
        val block = gReroBanner + spinBlock(header, res, gained, newExp, quota, "🎲도박꾼 무료 재굴림 (무페널티)", run.displayMode)
        // 빌드축 카운터 net-adjust — 교체 전 원스핀 기여(run.lastSet4/lastAdjPairs)를 빼고 재굴림 결과 기여를 더함(중복카운트 방지).
        val rrSet4 = if (res.bestSetCount >= 4) 1 else 0
        val rrAdj = if (mods.adjacentSameExp != 0 && adjPairCount(res.cells) > 0) 1 else 0
        val spun = run.copy(
            stageExp = newExp, score = newScore, coins = newCoins, state = "SPIN",
            lastCells = raw.joinToString(",") { it.sym.id }, lastGain = gained, lastScoreGain = res.score, lastCoinGain = res.coins,
            lastSet4 = rrSet4, lastAdjPairs = rrAdj,
            usedCmds = (used + "GREROL").joinToString(","), lastActionAt = now,
            runJackpots = run.runJackpots + (if (res.jackpotSym != null) 1 else 0),
            runBestSpin = maxOf(run.runBestSpin, gained),
            runSet4 = (run.runSet4 - run.lastSet4 + rrSet4).coerceAtLeast(0),
            runAdjPairs = (run.runAdjPairs - run.lastAdjPairs + rrAdj).coerceAtLeast(0),
            runRerolled = 1,   // 무조작 제한도전(ACH-5c) — 🎲도박꾼 재굴림 사용 시 런 플래그 set(런 끝까지 유지)
        )
        if (newExp >= quota) return clearStage(spun, res, newExp, newScore, newCoins, run.lastSpinNo + 1, spins, quota, block)
        if (fromPost || run.lastSpinNo + 1 >= spins) return gameOver(spun, newScore, block, "요구 ${fmt(quota)}EXP 미달")
        App.db.slotV2Run().upsert(spun)
        return Reply.Msg("$block\n👉 \"잭팟\" 계속")
    }

    /** 🔧 직전 스핀 결과 조작(재굴림/고정/복사/교체) — 스핀 소모 X. fromPost: 게임오버 보류(POST_SPIN)에서 호출. */
    private suspend fun handleManipulator(run: SlotV2RunRow, dev: SlotV2Engine.Device, argN: Int?, fromPost: Boolean): Reply {
        val now = System.currentTimeMillis()
        if (run.lastCells.isBlank() || run.lastSpinNo < 0)
            return Reply.Msg("@${run.ownerNick} 직전 스핀이 없어 — 먼저 \"잭팟\" 으로 돌려!")
        if (dev.needsArg && (argN == null || argN < 1))
            return Reply.Msg("@${run.ownerNick} ${dev.emoji}${dev.name} 은 칸 번호가 필요해 (예: \"${dev.cmd} 3\")")
        // (B) 조작 장치 코인 비용 — reroll/pin 3코인, copy/swap 5코인 (부족 시 거부)
        val devCost = when (dev.id) {
            "dev_reroll", "dev_pin" -> 3
            "dev_copy", "dev_swap" -> 5
            else -> 0
        }
        if (run.coins < devCost)
            return Reply.Msg("@${run.ownerNick} ${dev.emoji}${dev.name} 사용엔 ${devCost}🪙 필요 (보유 ${run.coins}🪙)")
        val devInc = mutableListOf("deviceUses" to 1L, "seen_${dev.id}" to 1L)
        if (dev.id == "dev_reroll") devInc += "rerollUses" to 1L
        if (dev.id == "dev_pin") devInc += "pinUses" to 1L
        track(run, *devInc.toTypedArray())
        val phase = run.phaseItems.split(",").filter { it.isNotBlank() }
        val preModsM = SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run), curseList(run))
        val mCtx = runCtxOf(run, run.lastSpinNo, SlotV2Engine.spinsPerStage(preModsM), qOf(run.stage, preModsM))
        val mods = SlotV2Engine.applyItemMods(SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run), curseList(run), ctx = mCtx), phase)
        val spins = effSpins(run, mods)
        val quota = qOf(run.stage, mods)
        val r = rng()
        val raw = SlotV2Engine.cellsFromIds(run.lastCells.split(",").filter { it.isNotBlank() })
        if (raw.isEmpty()) return Reply.Msg("@${run.ownerNick} 직전 결과 복원 실패 — 그냥 \"잭팟\" 으로 진행!")
        val n = raw.size
        val costNote = if (devCost > 0) " · -${devCost}🪙" else ""
        var opNote = ""
        when (dev.id) {
            "dev_reroll" -> { for (i in 0 until n) raw[i] = SlotV2Engine.rollOne(r, mods); opNote = "🔄재굴림 (EXP -10%$costNote)" }
            "dev_pin" -> { val keep = (argN!! - 1).coerceIn(0, n - 1)
                for (i in 0 until n) if (i != keep) raw[i] = SlotV2Engine.rollOne(r, mods); opNote = "📌${keep + 1}번 칸 유지·재굴림 (EXP -10%$costNote)" }
            "dev_copy" -> { val src = (argN!! - 1).coerceIn(0, n - 1); val dst = if (src + 1 < n) src + 1 else src - 1
                if (dst in 0 until n) raw[dst] = raw[src].copy(); opNote = "📑${src + 1}번 칸을 옆칸에 복사 (EXP -10%$costNote)" }
            "dev_swap" -> { val idx = (argN!! - 1).coerceIn(0, n - 1); val target = bestValueId(raw) ?: "star"
                raw[idx] = SlotV2Engine.Cell(SlotV2Engine.SYM_BY_ID.getValue(target)); opNote = "🔃${idx + 1}번 칸을 ${SlotV2Engine.SYM_BY_ID.getValue(target).emoji}로 교체 (EXP -10%$costNote)" }
            else -> return Reply.Ignore
        }
        val hasPrism = perkList(run).any { SlotV2Engine.perk(it)?.tier == SlotV2Engine.Tier.PRISM }
        val capMul = SlotV2Engine.capMulFor(run.stage, hasPrism)
        val res = SlotV2Engine.evaluate(r, raw, mods, run.lastSpinNo, spins, false, capMul = capMul)
        var gained = res.exp
        val boss = SlotV2Engine.bossFor(run.stage)
        if (boss != null) gained = applyBoss(boss, gained, res, run.lastSpinNo, spins,
            if (spins > 0) quota.toDouble() / spins else 0.0, perkList(run).size).first
        // (B) 조작 결과 EXP ×0.9 (4종 일원화 — 점수 -10% scoreScale 폐지)
        gained = (gained * 0.9).toLong()
        val newScoreGain = res.score
        // 직전 기여분 되돌리고 새 결과로 교체
        val newExp = (run.stageExp - run.lastGain + gained).coerceAtLeast(0)
        val newScore = (run.score - run.lastScoreGain + newScoreGain).coerceAtLeast(0)
        // (B) 코인: 직전 기여분 환원 + 이번 결과 코인 - 조작 비용
        val newCoins = (run.coins - run.lastCoinGain + res.coins - devCost).coerceAtLeast(0)
        val used = run.usedCmds.split(",").filter { it.isNotBlank() }
        val m = SlotV2Engine.machine(run.machineId)
        val header = "${m.emoji}${m.name} @${run.ownerNick} S${run.stage} #${run.lastSpinNo + 1}/$spins 🔧${dev.name}"
        // 🔧 조작 장치(재굴림/고정/복사/교체) 발동 배너 — 결과 최상단 1줄(효과=Device.desc). opNote(작업 상세)와 별개로 효과 강조.
        val block = deviceBanner(dev) + spinBlock(header, res, gained, newExp, quota, opNote, run.displayMode)
        // 조작 결과의 세트4+(magnet_grad·copy_answer)·인접쌍(chain) 도 빌드축 카운트 — 교체 전 원스핀 기여를 빼고 더함(net-adjust·중복방지)
        val manipSet4 = if (res.bestSetCount >= 4) 1 else 0
        val manipAdj = if (mods.adjacentSameExp != 0 && adjPairCount(res.cells) > 0) 1 else 0
        val spun = run.copy(
            stageExp = newExp, score = newScore, coins = newCoins, state = "SPIN",
            lastCells = raw.joinToString(",") { it.sym.id }, lastGain = gained, lastScoreGain = newScoreGain, lastCoinGain = res.coins,
            lastSet4 = manipSet4, lastAdjPairs = manipAdj,
            usedCmds = (used + dev.cmd).joinToString(","), lastActionAt = now,
            runJackpots = run.runJackpots + (if (res.jackpotSym != null) 1 else 0),
            runBestSpin = maxOf(run.runBestSpin, gained),
            runSet4 = (run.runSet4 - run.lastSet4 + manipSet4).coerceAtLeast(0),
            runAdjPairs = (run.runAdjPairs - run.lastAdjPairs + manipAdj).coerceAtLeast(0),
            runRerolled = 1,   // 무조작 제한도전(ACH-5c) — 🔧조작장치(재굴림/고정/복사/교체) 사용 시 런 플래그 set(런 끝까지 유지)
        )
        if (newExp >= quota) return clearStage(spun, res, newExp, newScore, newCoins, run.lastSpinNo + 1, spins, quota, block)
        if (fromPost || run.lastSpinNo + 1 >= spins) return gameOver(spun, newScore, block, "요구 ${fmt(quota)}EXP 미달")
        App.db.slotV2Run().upsert(spun)
        return Reply.Msg("$block\n👉 \"잭팟\" 계속")
    }

    /** 마지막 스핀 실패 후 MANIP 장치로 만회하는 단계. */
    private suspend fun handlePostSpin(run: SlotV2RunRow, t: String): Reply {
        val dev = SlotV2Engine.device(run.device)
        if (t in GIVEUP || t == "0") {
            val mods = SlotV2Engine.applyItemMods(SlotV2Engine.buildMods(run.machineId, run.charId, perkList(run), curseList(run)), run.phaseItems.split(",").filter { it.isNotBlank() })
            val quota = qOf(run.stage, mods)
            val m = SlotV2Engine.machine(run.machineId)
            return gameOver(run, run.score, "${m.emoji}${m.name} @${run.ownerNick} S${run.stage} (마지막 스핀)", "요구 ${fmt(quota)}EXP 미달")
        }
        // 🎲 도박꾼 무료 재굴림으로 만회
        if (run.charId == "gambler" && cmdOf(t) == "재굴림" && "GREROL" !in run.usedCmds.split(","))
            return handleGamblerReroll(run, fromPost = true)
        if (dev != null && dev.kind == SlotV2Engine.DevKind.MANIP && cmdOf(t) == dev.cmd)
            return handleManipulator(run, dev, argOf(t), fromPost = true)
        val opts = mutableListOf<String>()
        if (run.charId == "gambler" && "GREROL" !in run.usedCmds.split(",")) opts += "🎲\"재굴림\"(무료)"
        dev?.takeIf { it.kind == SlotV2Engine.DevKind.MANIP }?.let { opts += "🔧\"${it.cmd}${if (it.needsArg) " N(칸번호)" else ""}\"" }
        return Reply.Msg("@${run.ownerNick} 💀 마지막 스핀 — ${if (opts.isEmpty()) "만회 수단 없음" else "만회: ${opts.joinToString(" 또는 ")}"} 또는 \"포기\"")
    }

    // (handleDeviceNode/EVENT_DEVICE 전용 장치노드 폐지 — 장치는 면허+EVENT 임시장착 단일경로, 데드코드 제거.)

    // ── 업적 누적/평가 ──────────────────────────────────────
    private fun parseCounters(csv: String): LinkedHashMap<String, Long> {
        val m = LinkedHashMap<String, Long>()
        csv.split(",").filter { it.isNotBlank() }.forEach { val p = it.split(":"); if (p.size == 2) m[p[0]] = p[1].toLongOrNull() ?: 0 }
        return m
    }
    private fun achValue(row: com.ashersoft.kakaobot.data.SlotV2AchRow, key: String): Long = when (key) {
        "cherryTotal" -> row.cherryTotal; "crownTotal" -> row.crownTotal; "jackpots" -> row.jackpots
        "bossClears" -> row.bossClears; "lastSpinClears" -> row.lastSpinClears; "exactClears" -> row.exactClears
        "prismPicks" -> row.prismPicks; "bestStage" -> row.bestStage; "runs" -> row.runs; "bestScore" -> row.bestScore
        else -> parseCounters(row.counters)[key] ?: 0   // 확장 카운터 맵
    }

    /** 카운터 누적 + 신규 달성 업적 반환 (즉시 팝업용). inc=가산, setMax=최댓값 갱신(확장 카운터). */
    private suspend fun bumpAch(
        run: SlotV2RunRow, cherry: Long = 0, crown: Long = 0, jackpot: Long = 0, boss: Long = 0,
        lastClear: Long = 0, exact: Long = 0, prism: Long = 0, runDone: Long = 0, stageReached: Long = 0, scoreReached: Long = 0,
        inc: Map<String, Long> = emptyMap(), setMax: Map<String, Long> = emptyMap(),
    ): List<SlotV2Engine.Achievement> {
        val dao = App.db.slotV2Ach()
        val uid = run.ownerUserId.takeIf { it > 0 }
        val cur = (uid?.let { dao.findByUserId(run.linkId, it) }) ?: dao.find(run.linkId, run.ownerKey)
        val before = (cur?.unlocked ?: "").split(",").filter { it.isNotBlank() }.toSet()
        val cm = parseCounters(cur?.counters ?: "")
        inc.forEach { (k, v) -> cm[k] = (cm[k] ?: 0) + v }
        setMax.forEach { (k, v) -> cm[k] = maxOf(cm[k] ?: 0, v) }
        val row = com.ashersoft.kakaobot.data.SlotV2AchRow(
            linkId = run.linkId, ownerKey = run.ownerKey, ownerNick = run.ownerNick, userId = uid,
            cherryTotal = (cur?.cherryTotal ?: 0) + cherry,
            crownTotal = (cur?.crownTotal ?: 0) + crown,
            jackpots = (cur?.jackpots ?: 0) + jackpot,
            bossClears = (cur?.bossClears ?: 0) + boss,
            lastSpinClears = (cur?.lastSpinClears ?: 0) + lastClear,
            exactClears = (cur?.exactClears ?: 0) + exact,
            prismPicks = (cur?.prismPicks ?: 0) + prism,
            runs = (cur?.runs ?: 0) + runDone,
            bestStage = maxOf(cur?.bestStage ?: 0, stageReached),
            bestScore = maxOf(cur?.bestScore ?: 0, scoreReached),
            unlocked = cur?.unlocked ?: "",
            counters = cm.entries.joinToString(",") { "${it.key}:${it.value}" },
            lastAt = System.currentTimeMillis(),
        )
        val newly = SlotV2Engine.ACHIEVEMENTS.filter { it.id !in before && achValue(row, it.key) >= it.threshold }
        dao.upsert(row.copy(unlocked = (before + newly.map { it.id }).joinToString(",")))
        // (장치 영구지급 폐지 — 장치는 Device.unlockAch 업적 달성으로만 영구해금(deviceUnlocked).)
        return newly
    }

    /** 카운터만 누적(즉시 팝업 생략) — 장치/상점/도박 등. */
    private suspend fun track(run: SlotV2RunRow, vararg counters: Pair<String, Long>) {
        if (counters.isNotEmpty()) bumpAch(run, inc = counters.toMap())
    }

    private fun achBanner(newly: List<SlotV2Engine.Achievement>): String {
        if (newly.isEmpty()) return ""
        return "\n🏅 업적 달성! " + newly.joinToString(", ") { "${it.emoji}${it.name}" }
    }

    private fun appendBanner(reply: Reply, banner: String): Reply =
        if (banner.isEmpty() || reply !is Reply.Msg) reply else Reply.Msg(reply.text + banner)

    /** 🔧 능동/명령형 장치 발동 배너 1줄 — 결과 메시지 최상단. 효과는 Device.desc (DB 변경 0·표시만). 패시브는 제외. */
    private fun deviceBanner(dev: SlotV2Engine.Device, secondary: Boolean = false): String {
        val secTag = if (secondary) "(보조)" else ""
        return "🔧 ${dev.emoji}${dev.name}$secTag 발동! — ${dev.desc}\n"
    }

    /** 🌈 런종료 해금 알림 — before→after 로 새로 해금된 증강/유물을 전공(school)별로 안내.
     *  해금 = 앞으로 등장 가능(시작 시 보유 아님). 신규 해금 없으면 빈 문자열. */
    private fun unlockNotifyBanner(beforeStat: Map<String, Long>, afterStat: Map<String, Long>): String {
        val pool = SlotV2Engine.AUGMENTS + SlotV2Engine.RELICS
        val before = SlotV2Engine.unlockedPerks(pool, beforeStat).map { it.id }.toSet()
        val after = SlotV2Engine.unlockedPerks(pool, afterStat)
        val newly = after.filter { it.id !in before }
        if (newly.isEmpty()) return ""
        // school 별 묶음 (school 빈값=기본은 게이트 없는 BASE 라 신규 해금에 거의 안 나오지만 안전하게 "기본" 라벨)
        val bySchool = newly.groupBy { SlotV2Engine.perkGate(it).school.ifBlank { "기본" } }
        return buildString {
            bySchool.forEach { (school, perks) ->
                append("\n🌈$school 연구 완료! 앞으로 등장 가능: ")
                append(perks.joinToString(", ") { "${it.emoji}${it.name}" })
                append(" (해금=시작보유 아님)")
            }
        }
    }

    // ── 게임오버 → 기록 ─────────────────────────────────────
    private suspend fun gameOver(run: SlotV2RunRow, rawScore: Long, block: String, reason: String): Reply {
        val mod = SlotV2Engine.scoreModifier(run.machineId, run.charId)
        val finalScore = (rawScore * mod).toLong()
        val prev = myScore(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
        val priorBest = prev?.bestScore ?: 0L; val priorStage = prev?.bestStage ?: 0; val priorRuns = prev?.runs ?: 0
        recordRun(run, finalScore)
        // devicesOwned = 면허취득(이번 런 점수반영 전 stat) ∪ 기존 보유 장치 수. cstage/mstage = 이 캐릭/머신으로 도달한 최고스테이지(미클리어 포함).
        val prevAch = myAch(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
        val devCount = equipableDeviceList(prev, composeStat(prevAch, prev)).size.toLong()
        // 클리어 못한 런도 보유 최대(저주/유물)를 반영 — clearStage 와 동일 키(curseMax·relicsMax) setMax.
        val relicNGo = perkList(run).count { SlotV2Engine.perk(it)?.cat == SlotV2Engine.PCat.RELIC }.toLong()
        val nCurseGo = curseCount(run)
        val noDeviceGo = run.device.isEmpty() && run.device2.isEmpty()
        // 배치3a — 도달 스테이지 기반 setMax(클리어 못해도 도달은 인정) + 빌드도감/개인기록
        val goSetMax = linkedMapOf(
            "devicesOwned" to devCount,
            "curseMax" to nCurseGo.toLong(), "relicsMax" to relicNGo,
            "cstage_${run.charId}" to run.stage.toLong(), "mstage_${run.machineId}" to run.stage.toLong(),
            SlotV2Engine.bcKey(run.charId, run.machineId) to run.stage.toLong(),   // 빌드도감(도달 기준)
            SlotV2Engine.KEY_MAX_RUN_JACKPOTS to run.runJackpots.toLong(),         // 한 런 최다잭팟
        )
        if (noDeviceGo) goSetMax[SlotV2Engine.KEY_NO_DEV_STAGE] = run.stage.toLong()
        if (!run.usedItemThisRun) goSetMax[SlotV2Engine.KEY_NO_ITEM_MAX_S] = run.stage.toLong()
        if (nCurseGo >= 5) goSetMax[SlotV2Engine.KEY_CURSE5_STAGE] = run.stage.toLong()
        // ── v68 빌드 도감 완성판정 (게임오버 시점) — 도달/런누적/통산형 빌드. 클리어 이벤트 플래그는 false(이번은 클리어 아님). ──
        val priorStatGo = composeStat(prevAch, prev)
        val buildCtxGo = SlotV2Engine.BuildCtx(
            stage = run.stage, machineId = run.machineId, deviceId = run.device, device2Id = run.device2,
            perks = perkList(run), curses = curseList(run),
            runFastClears = run.runFastClears, runLastSpinClears = run.runLastSpinClears,
            runPrayWins = run.runPrayWins, runAdjPairs = run.runAdjPairs, runSet4 = run.runSet4,
            runCrowns = runCrownCount(run.runSymCounts),
            jackpotThisRun = run.runJackpots > 0,
            oracleUsedThisRun = "RUNORACLE" in run.usedCmds.split(","),   // 장착이 아니라 실제 🔮예언 호출 여부(RUNORACLE 마커)
            copyMadeSet4 = hasDevice(run, "dev_copy") && run.runSet4 >= 1,
            skullTotal = priorStatGo["skullTotal"] ?: 0L,
            closeClears = priorStatGo["closeClears"] ?: 0L,
        )
        val satisfiedBldsGo = SlotV2Engine.evalThemeBuilds(buildCtxGo)
        satisfiedBldsGo.forEach { goSetMax[it] = 1L }
        val newlyBldsGo = satisfiedBldsGo.filter { (priorStatGo[it] ?: 0L) <= 0L }
        // ── ACH-3: 🙏기도 실패(inc) — 이 스테이지에 기도를 썼는데 클리어 못하고 게임오버 ──
        val goInc = linkedMapOf<String, Long>()
        if ("PRAY" in run.usedCmds.split(",")) goInc["prayFails"] = 1L
        val achNew = bumpAch(run, runDone = 1, stageReached = run.stage.toLong(), scoreReached = finalScore,
            inc = goInc, setMax = goSetMax)
        runCatching { SlotV2WebService.sync(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 }) }
        App.db.slotV2Run().delete(run.linkId, run.ownerKey)
        // 🌈 런종료 해금 알림 — 이번 런으로 새로 연구완료(해금)된 증강/유물을 school별로 안내(해금=시작보유 아님).
        val beforeStat = priorStatGo
        val afterStat = playerStat(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })   // recordRun/bumpAch 반영 최신
        val unlockBanner = unlockNotifyBanner(beforeStat, afterStat)
        val acctLine = "🎓 졸업레벨 Lv.${SlotV2Engine.accountLevel(afterStat)}\n"
        val m = SlotV2Engine.machine(run.machineId)
        val ch = SlotV2Engine.character(run.charId)
        val newBest = finalScore > priorBest; val newStage = run.stage > priorStage
        val title = SlotV2Engine.titleStr(maxOf(finalScore, priorBest))
        val perks = perkList(run).mapNotNull { SlotV2Engine.perk(it)?.emoji }
        val curses = curseList(run).mapNotNull { SlotV2Engine.perk(it)?.emoji }
        val sets = SlotV2Engine.activeSets(perkList(run).toSet())
        val dev = SlotV2Engine.device(run.device)
        val dev2 = SlotV2Engine.device(run.device2)
        // 🎯 다음 도전 추천 — recordRun/bumpAch 반영된 최신 stat 기준(고정목표 우선)
        val recoLine = nextChallengeReco(run.linkId, run.ownerNick, run.ownerUserId.takeIf { it > 0 })
        return Reply.Msg(buildString {
            append("$block\n$DIV\n")
            if (newBest || newStage) {
                append("🎉🎉 신기록! ")
                if (newBest) append("최고점수 갱신")
                if (newBest && newStage) append(" · ")
                if (newStage) append("최고 S${run.stage} 돌파")
                append(" 🎉🎉\n")
            }
            append("💀 게임오버 — @${run.ownerNick}  $title\n")
            append(acctLine)
            append("🏁 최종 ${fmt(finalScore)}점")
            if (newBest && priorBest > 0) append(" (이전 ${fmt(priorBest)} ▲${fmt(finalScore - priorBest)})")
            append("\n📊 S${run.stage} 도달 ($reason) · ${ch.emoji}${ch.name}+${m.emoji}${m.name} ×${fmt2(mod)}\n")
            append("🎰 이번 런 잭팟 ${run.runJackpots}회 · 💥최고 한 방 ${fmt(run.runBestSpin)}EXP\n")
            topSymOf(run.runSymCounts)?.let { (emoji, n) -> append("🎲 최다 심볼 $emoji ×$n\n") }
            if (run.closestClear in 0..99) append("😱 가장 아슬아슬: ${run.closestClear} 차이로 클리어\n")
            perkList(run).mapNotNull { SlotV2Engine.perk(it)?.takeIf { p -> p.cat == SlotV2Engine.PCat.RELIC } }.let { rs ->
                if (rs.isNotEmpty()) append("🛡️ 유물 ${rs.joinToString("") { it.emoji }}\n")
            }
            if (perks.isNotEmpty() || curses.isNotEmpty() || dev != null || dev2 != null) {
                append("🧩 빌드 ${if (perks.isEmpty()) "-" else perks.joinToString("")}")
                if (curses.isNotEmpty()) append(" 🌑${curses.joinToString("")}")
                if (dev != null) append(" ${dev.emoji}")
                if (dev2 != null) append(" ${dev2.emoji}(보조)")
                if (sets.isNotEmpty()) append(" · 세트 ${sets.joinToString(",") { it.name }}")
                append("\n")
            }
            // 📦 이번 런 빌드 리포트 — 주력축 추정 + 이번 런 완성한 빌드 도감(통산 카운트)
            append(runBuildReport(run, completedThisRun = satisfiedBldsGo, afterStat = afterStat))
            if (unlockBanner.isNotEmpty()) append(unlockBanner)
            if (recoLine.isNotEmpty()) append(recoLine)
            append("👉 댓글로 \"잭팟\" 재도전!")
            append("\n🔁 같은 조합 재도전: \"같은조합\"")
            if (priorRuns <= 2) append("\n💡 초보 팁: 보상은 [초보추천]🥈 위주로! 증강·유물로 빌드를 쌓을수록 더 깊이 가요. \"잭팟도움말\"")
            append(achBanner(achNew))
            append(buildCompleteBanner(newlyBldsGo))
        })
    }

    /**
     * 📦 런종료 리포트 — 이번 런 "주력 축" 추정 + 이번 런 완성한 빌드 도감 한 줄.
     *  주력축 = 보유 증강/저주의 desc 키워드로 5테마(초반성장/운빨/역전/세트/해골저주) 중 최다 매칭.
     *  completedThisRun = 이번 런에서 충족 판정된 bld_* (게임오버/마지막클리어 시점). afterStat = 통산 진행 표기용.
     */
    private fun runBuildReport(run: SlotV2RunRow, completedThisRun: Set<String>, afterStat: Map<String, Long>): String {
        val axis = primaryAxisGuess(run)
        val done = SlotV2Engine.themeBuildsDoneCount(afterStat)
        val total = SlotV2Engine.themeBuildTotal()
        val sb = StringBuilder()
        sb.append("📦 주력 축: $axis")
        val doneThis = completedThisRun.mapNotNull { SlotV2Engine.themeBuild(it) }
        if (doneThis.isNotEmpty()) sb.append(" · 이번 런 완성 ${doneThis.joinToString("") { it.emoji }}")
        sb.append(" · 빌드도감 $done/$total\n")
        return sb.toString()
    }

    /** 보유 증강/저주 desc 키워드로 이번 런 주력 축(5테마) 추정 — 리포트용(강제성 없음). */
    private fun primaryAxisGuess(run: SlotV2RunRow): String {
        val perks = perkList(run)
        val descs = perks.mapNotNull { SlotV2Engine.perk(it)?.desc }
        fun cnt(vararg kw: String) = descs.count { d -> kw.any { d.contains(it) } }
        val growth = cnt("S3 이하", "S1~5", "성장일지", "눈덩이", "첫 스핀", "첫스핀")
        val fortune = cnt("희귀", "불운", "운명폭발", "운세")
        val comeback = cnt("마지막 스핀", "막스핀", "막 스핀", "후반집중", "벼랑끝", "운명의종")
        val combo = cnt("세트", "양끝", "가운데", "인접", "콤보", "짝맞", "퍼즐", "완벽한모양")
        val curse = cnt("☠", "해골", "저주") + curseCount(run)
        val pairs = listOf("🌱초반성장" to growth, "🎰운빨" to fortune, "⏰역전" to comeback, "🧩세트콤보" to combo, "☠해골저주" to curse)
        val best = pairs.maxByOrNull { it.second }
        return if (best == null || best.second == 0) "균형/미정" else best.first
    }

    /**
     * 🎯 런종료 리포트용 "다음 도전 추천" 1~2줄 (강제성 없음).
     *  고정목표(pinnedChallenge) 미달성이면 맨 앞에, 이어서 가장 근접한 미달성 도전.
     *  전부 달성/근접 없음이면 빈 문자열.
     */
    private suspend fun nextChallengeReco(linkId: Long, nick: String, userId: Long?): String {
        val sc = myScore(linkId, nick, userId)
        val stat = composeStat(myAch(linkId, nick, userId), sc)
        val picked = LinkedHashSet<String>()
        val lines = mutableListOf<String>()
        // 고정목표 우선(미달성일 때만)
        val pinId = sc?.pinnedChallenge?.takeIf { it.isNotBlank() }
        if (pinId != null) SlotV2Engine.challengeById(pinId, stat)?.takeIf { !it.done }?.let {
            lines += "📌${it.emoji}${it.name} (${it.progressText})"; picked += it.id
        }
        // 가장 근접한 미달성 도전으로 2줄까지 채움
        for (c in SlotV2Engine.nearestChallenges(stat, 4)) {
            if (lines.size >= 2) break
            if (c.id in picked) continue
            lines += "${c.emoji}${c.name} (${c.progressText})"; picked += c.id
        }
        if (lines.isEmpty()) return ""
        return "🎯 다음 도전 추천: ${lines.joinToString(" · ")}\n"
    }

    private suspend fun recordRun(run: SlotV2RunRow, finalScore: Long) {
        val dao = App.db.slotV2Score()
        val now = System.currentTimeMillis()
        val uid = run.ownerUserId.takeIf { it > 0 }
        val existing = (uid?.let { dao.findByUserId(run.linkId, it) }) ?: dao.find(run.linkId, run.ownerNick)
        val best = maxOf(existing?.bestScore ?: 0L, finalScore)
        val bestStage = maxOf(existing?.bestStage ?: 0, run.stage)
        val row = SlotV2ScoreRow(
            linkId = run.linkId,
            nickname = run.ownerNick,
            bestScore = best,
            totalScore = (existing?.totalScore ?: 0L) + finalScore,
            runs = (existing?.runs ?: 0) + 1,
            bestStage = bestStage,
            bestChar = if (finalScore >= (existing?.bestScore ?: 0L)) run.charId else (existing?.bestChar ?: run.charId),
            bestMachine = if (finalScore >= (existing?.bestScore ?: 0L)) run.machineId else (existing?.bestMachine ?: run.machineId),
            lastPlayedAt = now,
            userId = uid,
            ownedDevices = existing?.ownedDevices ?: "",   // 영구 장치 소지 보존
            pinnedChallenge = existing?.pinnedChallenge ?: "",   // 고정목표 보존(리셋/만료 없음)
            // 직전 런 조합 저장(지시서11-B 같은조합 재도전) — CSV "char,machine,device,device2"
            lastCombo = listOf(run.charId, run.machineId, run.device, run.device2).joinToString(","),
        )
        dao.upsert(row)
    }

    // ── 리더보드 ────────────────────────────────────────────
    suspend fun myAch(linkId: Long, nick: String, userId: Long?): com.ashersoft.kakaobot.data.SlotV2AchRow? {
        val dao = App.db.slotV2Ach()
        if (userId != null && userId > 0L) dao.findByUserId(linkId, userId)?.let { return it }
        return dao.find(linkId, ownerKeyFor(userId, nick))
    }
    fun achCounter(row: com.ashersoft.kakaobot.data.SlotV2AchRow?, key: String): Long =
        if (row == null) 0 else achValue(row, key)

    /** 달성 업적 id 집합 (캐릭/머신 업적 해금 판정용). */
    suspend fun myAchSet(linkId: Long, nick: String, userId: Long?): Set<String> =
        (myAch(linkId, nick, userId)?.unlocked ?: "").split(",").filter { it.isNotBlank() }.toSet()

    // ── 해금 판정용 stat 맵 ──────────────────────────────────
    //  엔진 charUnlocked/machineUnlocked/deviceUnlocked 의 stat 인자.
    //  = 업적 카운터(cherryTotal/bossClears/cstage_*/mstage_*/minimalistS10…) + bestScore/bestStage/runs + 파생키(distinctCharS10).
    /** 업적행 + 점수행 → stat 맵 (이미 읽은 행으로 합성, DB 재조회 없음). */
    private fun composeStat(ach: com.ashersoft.kakaobot.data.SlotV2AchRow?, sc: SlotV2ScoreRow?): Map<String, Long> {
        val stat = LinkedHashMap<String, Long>()
        // 확장 카운터 맵 전부(cstage_*/mstage_*/bookTotal/closeClears/minimalistS10/richBossClears/noItemS8 …)
        parseCounters(ach?.counters ?: "").forEach { (k, v) -> stat[k] = v }
        // 전용 컬럼 카운터
        stat["cherryTotal"] = ach?.cherryTotal ?: 0
        stat["crownTotal"] = ach?.crownTotal ?: 0
        stat["jackpots"] = ach?.jackpots ?: 0
        stat["bossClears"] = ach?.bossClears ?: 0
        stat["lastSpinClears"] = ach?.lastSpinClears ?: 0
        stat["exactClears"] = ach?.exactClears ?: 0
        stat["prismPicks"] = ach?.prismPicks ?: 0
        // 특수(점수/스테이지/런) — score 행(최신) 우선, ach 행 폴백
        stat["bestScore"] = maxOf(sc?.bestScore ?: 0L, ach?.bestScore ?: 0L)
        stat["bestStage"] = maxOf((sc?.bestStage ?: 0).toLong(), ach?.bestStage ?: 0L)
        stat["runs"] = maxOf((sc?.runs ?: 0).toLong(), ach?.runs ?: 0L)
        // 파생키: 서로다른 캐릭 N명 S10 = cstage_* 중 ≥10 인 캐릭 수
        stat["distinctCharS10"] = stat.count { (k, v) -> k.startsWith("cstage_") && v >= 10 }.toLong()
        // 파생키: 장치 면허 lic_<deviceId> — 면허 조건표(기존 추적 stat 들의 AND) 충족 시 1, 아니면 0.
        //   순수 파생(distinctCharS10 처럼 신규 추적/DB 0). 면허 업적(key=lic_<deviceId>, threshold=1)이 이 키로 해금 판정.
        //   조건의 stat 키가 map 에 없으면 getOrElse 로 0 취급. 12 메인 장치 전용(보조 4개는 기존 업적 매핑 유지).
        fun g(k: String): Long = stat[k] ?: 0L
        fun lic(deviceId: String, cond: Boolean) { stat["lic_$deviceId"] = if (cond) 1L else 0L }
        lic("dev_safe",     g("closeClears") >= 5  && g("bestStage") >= 6)
        lic("dev_seal",     g("skullTotal") >= 200 && g("bestStage") >= 8)
        lic("dev_reroll",   g("bossClears") >= 3   && g("lastSpinClears") >= 3)
        lic("dev_pin",      g("exactClears") >= 3  && g("bestStage") >= 8)
        lic("dev_coin",     g("coinTotal") >= 500  && g("shopBuys") >= 15)
        lic("dev_subreel",  g("jackpots") >= 5     && g("set4Plus") >= 10)
        lic("dev_overheat", g("lastSpinClears") >= 10 && g("bestScore") >= 20000)
        lic("dev_oracle",   g("prayClears") >= 3   && g("bestStage") >= 15)
        lic("dev_copy",     g("prismPicks") >= 10  && g("set4Plus") >= 10)
        lic("dev_swap",     g("bossClears") >= 10  && g("bestStage") >= 15)
        lic("dev_bell",     g("closeClears") >= 30 && g("bossClears") >= 8)
        lic("dev_flame",    g("bestScore") >= 50000 && g("bestStage") >= 20)
        // 파생키: 빌드도감(ACH-6) — bld_<id> 완성 플래그를 카테고리별/총합으로 집계(순수 파생, 신규 추적/DB 0).
        //   bldCat_<category>·bldTotal·bldAllBasic(완성≥1 카테고리 수)·bldAllMaster(전부완성 카테고리 수).
        SlotV2Engine.themeBuildStats(stat).forEach { (k, v) -> stat[k] = v }
        // 파생키: 졸업레벨 — 해금 게이트 판정 공용. (accountExp/accountLevel 은 이 키 무시·자기참조 방지)
        //   ⚠️ lic_* 보다 뒤에 둠 — accountExp 가 면허 업적(key=lic_*)을 tier 합산하므로 lic_* 가 먼저 채워져야 정확.
        stat["accountLevel"] = SlotV2Engine.accountLevel(stat).toLong()
        return stat
    }

    /** 플레이어의 해금 stat 맵 (캐릭/머신/장치 해금 판정 공용). */
    suspend fun playerStat(linkId: Long, nick: String, userId: Long?): Map<String, Long> =
        composeStat(myAch(linkId, nick, userId), myScore(linkId, nick, userId))

    /** 장착 가능 장치 id 목록 (면허취득 ∪ 기존보유) — 웹/명령 공용. */
    suspend fun equipableDeviceIds(linkId: Long, nick: String, userId: Long?): List<String> {
        val sc = myScore(linkId, nick, userId)
        return equipableDeviceList(sc, composeStat(myAch(linkId, nick, userId), sc)).map { it.id }
    }

    /** "장치면허" 명령 — 전 장치의 해금업적+진행도 (취득/미취득). 장치는 업적 달성으로만 영구해금. */
    suspend fun licenseText(linkId: Long, nick: String, userId: Long?): String {
        val sc = myScore(linkId, nick, userId)
        val stat = composeStat(myAch(linkId, nick, userId), sc)
        val grandfathered = (sc?.ownedDevices ?: "").split(",").filter { it.isNotBlank() }.toSet()
        val unlocked = SlotV2Engine.unlockedDevices(stat)
        val locked = SlotV2Engine.lockedDevices(stat)
        return buildString {
            append("🔧 장치 해금 — @$nick (장치는 해금 업적 달성으로만 영구해금)\n")
            append("🪙 장치는 해금 업적으로만 영구해금 — 노드/이벤트 드롭·임시장착 없음(시작 시 장착).\n")
            append("$DIV\n")
            append("✅ 취득 (${unlocked.size}/${SlotV2Engine.DEVICES.count { it.unlockAch.isNotBlank() }})\n")
            if (unlocked.isEmpty()) append("· 아직 없음 — 아래 업적을 달성해봐!\n")
            unlocked.forEach { d ->
                val how = if (d.kind == SlotV2Engine.DevKind.PASSIVE || d.cmd.isEmpty()) "패시브·자동" else "능동·\"${d.cmd}\""
                append("· ${d.emoji}${d.name} [$how] — ${d.desc}\n")
            }
            // grandfather — 업적 미달성인데 과거 보유한 장치(인정)
            val gfOnly = grandfathered.mapNotNull { SlotV2Engine.device(it) }.filter { it !in unlocked }
            if (gfOnly.isNotEmpty()) {
                append("🎁 기존 보유(인정): ")
                append(gfOnly.joinToString(", ") { "${it.emoji}${it.name}" }).append("\n")
            }
            append("$DIV\n")
            append("🔒 미취득 — 해금 업적\n")
            locked.forEach { d ->
                val how = if (d.kind == SlotV2Engine.DevKind.PASSIVE || d.cmd.isEmpty()) "패시브" else "능동·${d.cmd}"
                append("· ${d.emoji}${d.name} [$how]\n   업적: ${SlotV2Engine.deviceUnlockHint(d, stat)}\n")
            }
        }.trimEnd()
    }

    // ══════════════════════════════════════════════════════════
    //  배치3a — 상시 도전판 / 목표고정 / 숙련 / 기록 / 빌드도감 (P4/P6)
    //  전부 리셋·만료 없는 상시 구조. 매일숙제/시즌/시간제한 없음.
    // ══════════════════════════════════════════════════════════
    private fun chKindMark(kind: SlotV2Engine.ChKind): String = when (kind) {
        SlotV2Engine.ChKind.DEVICE -> "🔧"; SlotV2Engine.ChKind.CHAR -> "🎭"
        SlotV2Engine.ChKind.MACHINE -> "🎰"; SlotV2Engine.ChKind.STANDARD -> "🏆"
    }

    /** "도전"/"잭팟도전" — 통합 상시 도전판(장치면허+캐릭/머신해금+표준도전), 진행도+고정여부 표시. */
    suspend fun challengeText(linkId: Long, nick: String, userId: Long?): String {
        val sc = myScore(linkId, nick, userId)
        val stat = composeStat(myAch(linkId, nick, userId), sc)
        val pinId = sc?.pinnedChallenge?.takeIf { it.isNotBlank() }
        val all = SlotV2Engine.allChallenges(stat)
        // 미달성(진행률 높은 순) → 달성(뒤). 번호는 "목표 N" 고정용으로 미달성에만 매김.
        val undone = all.filter { !it.done }.sortedByDescending { SlotV2Engine.reqProgress(it.req, stat) }
        val done = all.filter { it.done }
        return buildString {
            append("🏆 잭팟런 상시 도전판 — @$nick (${done.size}/${all.size} 달성)\n")
            append("리셋·만료 없는 상시 목표! \"목표 N\" 으로 하나 고정하면 상태/런종료에 진행도가 떠요.\n")
            pinId?.let { p -> SlotV2Engine.challengeById(p, stat)?.let { c ->
                append("📌 고정목표: ${c.emoji}${c.name} (${if (c.done) "✅달성!" else c.progressText})\n") } }
            append("$DIV\n")
            append("🔥 도전 중 (가까운 순) — 번호로 \"목표 N\" 고정\n")
            if (undone.isEmpty()) append("· 🎉 모든 도전 달성! 대단해요\n")
            undone.forEachIndexed { i, c ->
                val pin = if (c.id == pinId) "📌" else ""
                append("${i + 1}. ${chKindMark(c.kind)}${c.emoji}${c.name}$pin — ${c.progressText}  → ${c.rewardHint}\n")
            }
            if (done.isNotEmpty()) {
                append("$DIV\n")
                append("✅ 달성 (${done.size}): ")
                append(done.joinToString(" ") { "${chKindMark(it.kind)}${it.emoji}${it.name}" })
                append("\n")
            }
        }.trimEnd()
    }

    /** "목표 <번호>" — 도전판 미달성 N번을 고정(pinnedChallenge 저장). 0/없음 = 해제. 리셋/만료 없음. */
    suspend fun pinChallenge(linkId: Long, nick: String, userId: Long?, arg: Int?): String {
        val sc = myScore(linkId, nick, userId)
        val stat = composeStat(myAch(linkId, nick, userId), sc)
        val undone = SlotV2Engine.allChallenges(stat).filter { !it.done }
            .sortedByDescending { SlotV2Engine.reqProgress(it.req, stat) }
        // 인자 없음/0 = 고정 해제
        if (arg == null || arg == 0) {
            persistPin(linkId, nick, userId, "")
            return "📌 @$nick 고정목표 해제. \"도전\" 으로 번호를 골라 \"목표 N\" 으로 다시 고정할 수 있어."
        }
        if (arg < 1 || arg > undone.size)
            return "📌 @$nick \"목표 N\" 은 1~${undone.size} 중에서! (\"도전\" 으로 번호 확인)"
        val c = undone[arg - 1]
        persistPin(linkId, nick, userId, c.id)
        return "📌 @$nick 목표 고정: ${c.emoji}${c.name}\n진행도 ${c.progressText} · 보상 ${c.rewardHint}\n상태/런종료에 진행도가 표시돼요. (\"목표\" 만 입력하면 해제)"
    }

    private suspend fun persistPin(linkId: Long, nick: String, userId: Long?, challengeId: String) {
        val dao = App.db.slotV2Score()
        val uid = userId?.takeIf { it > 0 }
        val existing = (uid?.let { dao.findByUserId(linkId, it) }) ?: dao.find(linkId, nick)
        val row = (existing ?: SlotV2ScoreRow(linkId = linkId, nickname = nick, userId = uid))
            .copy(pinnedChallenge = challengeId, lastPlayedAt = System.currentTimeMillis())
        dao.upsert(row)
    }

    /** 고정목표 한 줄(상태/내잭팟용) — 미고정/없음이면 빈 문자열. */
    private fun pinnedLine(sc: SlotV2ScoreRow?, stat: Map<String, Long>): String {
        val pinId = sc?.pinnedChallenge?.takeIf { it.isNotBlank() } ?: return ""
        val c = SlotV2Engine.challengeById(pinId, stat) ?: return ""
        return "📌 목표 ${c.emoji}${c.name} (${if (c.done) "✅달성!" else c.progressText})"
    }

    /** 고정목표 한 줄(외부 명령용, 내잭팟 등) — 미고정/없음이면 빈 문자열. */
    suspend fun pinnedStatusLine(linkId: Long, nick: String, userId: Long?): String {
        val sc = myScore(linkId, nick, userId)
        return pinnedLine(sc, composeStat(myAch(linkId, nick, userId), sc))
    }

    /** "숙련"/"잭팟숙련" — 캐릭별 cstage_/머신별 mstage_ 로 메달(S5동·S10은·S15금) 표시. */
    suspend fun masteryText(linkId: Long, nick: String, userId: Long?): String {
        val stat = playerStat(linkId, nick, userId)
        fun medalCell(emoji: String, name: String, stage: Long, medal: SlotV2Engine.Medal): String =
            "${medal.emoji}${emoji}${name} S$stage"
        val chars = SlotV2Engine.CHARS.map { c ->
            Triple(c, SlotV2Engine.charBestStage(c.id, stat), SlotV2Engine.charMastery(c.id, stat))
        }.sortedByDescending { it.second }
        val macs = SlotV2Engine.MACHINES.map { m ->
            Triple(m, SlotV2Engine.machineBestStage(m.id, stat), SlotV2Engine.machineMastery(m.id, stat))
        }.sortedByDescending { it.second }
        val cGold = chars.count { it.third == SlotV2Engine.Medal.GOLD }
        val mGold = macs.count { it.third == SlotV2Engine.Medal.GOLD }
        return buildString {
            append("🏅 잭팟런 숙련도 — @$nick\n")
            append("메달: 🥉S${SlotV2Engine.MEDAL_BRONZE_S} · 🥈S${SlotV2Engine.MEDAL_SILVER_S} · 🥇S${SlotV2Engine.MEDAL_GOLD_S} (그 캐릭/머신으로 도달한 최고스테이지)\n")
            append("$DIV\n")
            append("🎭 캐릭터 (🥇금 $cGold/${SlotV2Engine.CHARS.size})\n")
            chars.filter { it.second > 0 }.forEach { (c, s, md) -> append("· ${medalCell(c.emoji, c.name, s, md)}\n") }
            if (chars.none { it.second > 0 }) append("· 아직 기록 없음 — \"잭팟\" 으로 플레이!\n")
            append("$DIV\n")
            append("🎰 머신 (🥇금 $mGold/${SlotV2Engine.MACHINES.size})\n")
            macs.filter { it.second > 0 }.forEach { (m, s, md) -> append("· ${medalCell(m.emoji, m.name, s, md)}\n") }
            if (macs.none { it.second > 0 }) append("· 아직 기록 없음\n")
        }.trimEnd()
    }

    /** "기록"/"잭팟기록" — 개인 최고기록 + 신규 setMax 기록(엔진 recordLines) + 칭호. */
    suspend fun recordText(linkId: Long, nick: String, userId: Long?): String {
        val sc = myScore(linkId, nick, userId)
        val stat = composeStat(myAch(linkId, nick, userId), sc)
        val title = SlotV2Engine.titleStr(stat["bestScore"] ?: 0L)
        return buildString {
            append("📈 잭팟런 개인기록 — @$nick  $title\n")
            append("$DIV\n")
            SlotV2Engine.recordLines(stat).forEach { append("$it\n") }
            // 표준 도전 관련 추가 기록
            append("🧘 무아이템 최고도달 S${stat[SlotV2Engine.KEY_NO_ITEM_MAX_S] ?: 0L}\n")
            append("☠ 저주5↑ 최고도달 S${stat[SlotV2Engine.KEY_CURSE5_STAGE] ?: 0L}\n")
            append("💀 저주3↑ 보스클리어 ${stat[SlotV2Engine.KEY_CURSE_BOSS_CLEARS] ?: 0L}회\n")
            append("🪙 무상점 S10 클리어 ${stat[SlotV2Engine.KEY_NO_SHOP_S10] ?: 0L}회\n")
            append("$DIV\n")
            append("📦 빌드도감 ${SlotV2Engine.buildDex(stat).size}/${SlotV2Engine.buildDexTotal()} 조합 · 🏆 \"도전\" 으로 상시 목표 확인")
        }.trimEnd()
    }

    /** "빌드도감"/"잭팟빌드" — 클리어/도달한 캐릭+머신 조합(bc_*) + 테마 빌드 도감(bld_*, 카테고리 그룹). */
    suspend fun buildDexText(linkId: Long, nick: String, userId: Long?): String {
        val stat = playerStat(linkId, nick, userId)
        val rows = SlotV2Engine.buildDex(stat)
        val total = SlotV2Engine.buildDexTotal()
        val themeDone = SlotV2Engine.themeBuildsDoneCount(stat)
        val themeTotal = SlotV2Engine.themeBuildTotal()
        return buildString {
            append("📦 잭팟런 빌드도감 — @$nick\n")
            append("🎭 캐릭+머신 조합 ${rows.size}/$total · 🏅 테마 빌드 $themeDone/$themeTotal\n")
            append("$DIV\n")
            append("【캐릭+머신 조합】 (플레이한 조합의 최고도달 스테이지)\n")
            if (rows.isEmpty()) append("· 아직 없음 — 캐릭터·머신을 골라 \"잭팟\" 도전!\n")
            rows.take(30).forEach { r ->
                val c = SlotV2Engine.character(r.charId); val m = SlotV2Engine.machine(r.machineId)
                append("· ${c.emoji}${c.name} + ${m.emoji}${m.name} — S${r.stage}\n")
            }
            if (rows.size > 30) append("· … 외 ${rows.size - 30}개\n")
            // ── 테마 빌드 도감(bld_*) — 카테고리 그룹별 완성/미완 + 완성조건 ──
            append("$DIV\n")
            append("【테마 빌드】 (플레이스타일 완성 도감)\n")
            for (cat in SlotV2Engine.THEME_BUILD_CATEGORIES) {
                val inCat = SlotV2Engine.THEME_BUILDS.filter { it.category == cat }
                val doneN = inCat.count { SlotV2Engine.themeBuildDone(it.id, stat) }
                append("▸ $cat ($doneN/${inCat.size})\n")
                inCat.forEach { b ->
                    val done = SlotV2Engine.themeBuildDone(b.id, stat)
                    append(if (done) "  ✅ ${b.emoji}${b.name}\n" else "  ⬜ ${b.emoji}${b.name} — ${b.cond}\n")
                }
            }
        }.trimEnd()
    }

    suspend fun topByBest(linkId: Long, limit: Int): List<SlotV2ScoreRow> = App.db.slotV2Score().topByBest(linkId, limit)

    /** 📊 밸런스 대시보드(운영) — 캐릭/머신 인기·평균 도달·업적 진행 집계. */
    suspend fun statsText(linkId: Long): String {
        val rows = App.db.slotV2Score().allForLink(linkId).filter { it.runs > 0 }
        if (rows.isEmpty()) return "📊 잭팟런 v3 통계 — 아직 데이터 없음 (v3 새 시즌)"
        val totalRuns = rows.sumOf { it.runs }
        val avgStage = rows.map { it.bestStage }.average()
        val maxStage = rows.maxOf { it.bestStage }
        val charPop = rows.filter { it.bestChar.isNotBlank() }.groupingBy { SlotV2Engine.character(it.bestChar).let { c -> "${c.emoji}${c.name}" } }.eachCount().entries.sortedByDescending { it.value }.take(5)
        val macPop = rows.filter { it.bestMachine.isNotBlank() }.groupingBy { SlotV2Engine.machine(it.bestMachine).let { m -> "${m.emoji}${m.name}" } }.eachCount().entries.sortedByDescending { it.value }.take(5)
        return buildString {
            append("📊 잭팟런 v3 통계 (운영)\n")
            append("👥 플레이어 ${rows.size} · 🔁 통산 런 $totalRuns · 평균 최고 S${"%.1f".format(avgStage)} (최고 S$maxStage)\n")
            append("🎭 인기 캐릭터: ${if (charPop.isEmpty()) "-" else charPop.joinToString(", ") { "${it.key}(${it.value})" }}\n")
            append("🎰 인기 머신: ${if (macPop.isEmpty()) "-" else macPop.joinToString(", ") { "${it.key}(${it.value})" }}\n")
            append("📚 콘텐츠: 증강${SlotV2Engine.AUGMENTS.size}·유물${SlotV2Engine.RELICS.size}·아이템${SlotV2Engine.ITEMS.size}·저주${SlotV2Engine.CURSES.size}·장치${SlotV2Engine.DEVICES.size}·캐릭${SlotV2Engine.CHARS.size}·머신${SlotV2Engine.MACHINES.size}·업적${SlotV2Engine.ACHIEVEMENTS.size}")
        }
    }

    suspend fun archiveSeason(linkId: Long, key: String, label: String, tsMs: Long): Int =
        SlotV2WebService.archiveSeason(linkId, key, label, tsMs)
    suspend fun myScore(linkId: Long, nick: String, userId: Long?): SlotV2ScoreRow? {
        val dao = App.db.slotV2Score()
        if (userId != null && userId > 0L) dao.findByUserId(linkId, userId)?.let { return it }
        return dao.find(linkId, nick)
    }

    /**
     * (#12) canonical userId 해석 — 방 댓글경로에서 resolvedUserId/ctx.userId 가 0/null 로 오면
     * 게임이 쓴 노드(uid앵커)와 웹 토큰/조회가 어긋남. userId>0 이면 그대로, 아니면
     * member(currentNickname) → user_points(nickname) → slot_v2_score(nickname) 순으로 닉→uid 보강.
     * 게임진행(command/handleInput/restartSameCombo) 과 웹(linkDex/linkPick/sync) 진입에서 호출해
     * 같은 사람이 게임이든 웹이든 동일 canonical uid 를 쓰게 한다. (myScore 의 nick 폴백은 레거시 호환으로 유지.)
     */
    suspend fun resolveUid(linkId: Long, nick: String, userId: Long?): Long? {
        if (userId != null && userId > 0L) return userId
        if (nick.isBlank() || nick.startsWith("user_")) return null
        return runCatching {
            // 1) member 현재 닉 매칭
            App.db.query(
                "SELECT userId FROM member WHERE linkId=? AND currentNickname=? AND userId>0 LIMIT 1",
                arrayOf(linkId, nick),
            ).use { c -> if (c.moveToFirst() && !c.isNull(0)) c.getLong(0) else null }
            // 2) user_points 닉 매칭
                ?: App.db.query(
                    "SELECT userId FROM user_points WHERE linkId=? AND nickname=? AND userId>0 LIMIT 1",
                    arrayOf(linkId, nick),
                ).use { c -> if (c.moveToFirst() && !c.isNull(0)) c.getLong(0) else null }
            // 3) slot_v2_score 닉 매칭 (이 게임이 직접 남긴 uid 앵커 레코드)
                ?: App.db.query(
                    "SELECT userId FROM slot_v2_score WHERE linkId=? AND nickname=? AND userId>0 LIMIT 1",
                    arrayOf(linkId, nick),
                ).use { c -> if (c.moveToFirst() && !c.isNull(0)) c.getLong(0) else null }
        }.getOrNull()
    }

    // ── (#11-b) 튜토리얼 — 현재 게임을 정확 반영한 단계별 설명(페이지형) ──
    const val TUTORIAL_PAGES = 3
    /** 잭팟튜토리얼 [N] — 페이지형 상세 설명. 도움말("잭팟도움말")은 요약, 튜토리얼은 상세. */
    fun tutorialText(page: Int): String {
        val p = page.coerceIn(1, TUTORIAL_PAGES)
        val nav = "\n$DIV\n📖 잭팟튜토리얼 $p/$TUTORIAL_PAGES" +
            (if (p < TUTORIAL_PAGES) " · 다음 ▶ \"잭팟튜토리얼 ${p + 1}\"" else " · 처음 ▶ \"잭팟튜토리얼 1\"") +
            "\n(요약만 보려면 \"잭팟도움말\")"
        val body = when (p) {
            1 -> buildString {
                append("🎰 잭팟런 튜토리얼 (1/$TUTORIAL_PAGES) — 목표 · 심볼 · 세트\n")
                append("⚠️ 모든 진행은 봇 메시지에 **댓글(답글)** 로 입력해요!\n")
                append("$DIV\n")
                append("①【목표】 5칸짜리 슬롯을 굴려(\"잭팟\") 각 스테이지의 요구 ⭐EXP를 넘기면 다음 스테이지로!\n")
                append("· 한 스테이지에 쓸 수 있는 스핀은 정해져 있어요(기본 ${SlotV2Engine.SPINS_PER_STAGE}번).\n")
                append("· 매 스핀 위에 \"🎰 스핀 N/M · 남은 K번\" 이 떠요 — 지금 몇 번째인지 꼭 확인!\n")
                append("· 스핀을 다 썼는데 요구 EXP를 못 넘기면 게임오버 → 도달 깊이만큼 점수가 기록돼요.\n")
                append("$DIV\n")
                append("②【심볼】 슬롯에 뜨는 5칸\n")
                append("· 값심볼(EXP): 🍒3 · 📘6 · ⭐8 · 💎(EXP1·점수15) · 👑(EXP20·점수50, 아주 희귀)\n")
                append("· 특수심볼:\n")
                append("  🪙코인=상점 화폐 · 🗝열쇠=EXP6(보물)\n")
                append("  🔥불꽃=이번 스핀 EXP ×1.5 · 🧲자석=옆칸 복사 · 💣폭탄=양옆 제거+EXP\n")
                append("  🌀와일드=아무 심볼로 취급(세트 합류) · 🌱씨앗=다음 스핀 성장\n")
                append("  ☠해골=위험(올인 때 2개↑면 EXP 0) · 🎲주사위=운\n")
                append("  ※ 🗝🌀🌱🎲 등은 평소 안 나오고 특정 증강/유물/머신을 갖춰야 풀에 등장해요.\n")
                append("$DIV\n")
                append("③【세트 · 🎰잭팟】\n")
                append("· 같은 값심볼이 2~5개 모이면 세트 보너스! (2개+8 · 3개+18 · 4개+42 · 5개+100)\n")
                append("· 특정 조건이 맞으면 🎰JACKPOT 연출과 함께 큰 EXP·점수를 받아요.")
            }
            2 -> buildString {
                append("🎰 잭팟런 튜토리얼 (2/$TUTORIAL_PAGES) — 성장(증강·유물·저주) · 장치\n")
                append("$DIV\n")
                append("④【성장: 증강 🥈🥇🌈 / 유물 / 저주 🌑】\n")
                append("· ✨증강 = 무료로 1개 선택(같은 등급끼리 비교). 🥈은(안정·초보추천)·🥇금(빌드방향)·🌈프리즘(고급·판흔듦).\n")
                append("· 🛡️유물 = 상점/이벤트에서 코인으로 얻는 성장 효과.\n")
                append("· 🌑저주 = 단점과 함께 장점도 같이 주는 고위험 선택. 효과 설명은 \"상태\" 로 확인!\n")
                append("· ※ 스핀 자체엔 누적 배율이 없어요 — 오직 증강/유물로 강해져요.\n")
                append("$DIV\n")
                append("⑤【장치 🔧】\n")
                append("· 업적을 달성하면 장치가 영구 해금돼요 → 런 시작 시 🔧장착 단계에서 1개 장착.\n")
                append("· 패시브(자동)·능동(명령어로 발동, 스테이지당 1회)·직전결과 조작형 등 종류가 다양해요.\n")
                append("  예: 🔥점화 🔒봉인 🪙투입 / 🔄재굴림 📌고정 N 📑복사 N 🔃교체 N / 🔮예언 🔔비상\n")
                append("· 마지막 스핀에서 부족해도 조작형 장치가 있으면 만회 기회가 떠요!\n")
                append("· 해금 조건은 \"장치면허\" 로 확인.\n")
                append("$DIV\n")
                append("⑥【노드 선택 · 보스 👑 · 프리즘 🌈】\n")
                append("· 스테이지를 깨면 다음 경로를 골라요: ✨증강/🛡️유물 · 🛌휴식 · 🎲도박장 · 🎁이벤트 · (후반)🌑저주/위험거래.\n")
                append("· 5스테이지마다 👑보스! 특수 룰이 붙어요(예: 막스핀×2, 콤보없으면×0.5 등).\n")
                append("· 보스를 깨고 ✨증강을 고르면 🌈프리즘 증강이 확정으로 등장해요(강력!).")
            }
            else -> buildString {
                append("🎰 잭팟런 튜토리얼 (3/$TUTORIAL_PAGES) — 코인·상점 · 명령어\n")
                append("$DIV\n")
                append("⑦【코인 🪙 · 상점 🛒】\n")
                append("· 🪙코인은 런 안에서만 쓰는 화폐예요(런이 끝나면 사라짐).\n")
                append("· 스핀에서 🪙심볼·클리어 보상으로 모으고, 🛒상점/🪙투입 장치 등에 사용해요.\n")
                append("· 상점에선 유물·아이템 구매, \"리롤\" 로 목록 새로고침.\n")
                append("· 🎒아이템은 가방(최대 ${ITEM_SLOTS}칸)에 보관 → \"아이템\" 목록, \"아이템 N\" 사용.\n")
                append("$DIV\n")
                append("⑧【주요 명령어】 (전부 봇 메시지에 댓글로!)\n")
                append("· 스핀: \"잭팟\"(기본) · \"집중\"(폭망방지) · \"올인\"(EXP×2 도박) · \"기도\"(불운보정·기적) · \"최후\"(막스핀×1.75)\n")
                append("  ※ 특수 스핀(집중/올인/기도/최후)은 스테이지당 1회.\n")
                append("· 진행: 숫자(\"1\"~)로 선택 · \"상태\"(현재 진행) · \"간단/상세/계산\"(표시 모드)\n")
                append("· 조회: \"잭팟랭킹\" · \"내잭팟\" · \"잭팟도감\"(웹) · \"잭팟선택\"(웹·캐릭/머신 선택)\n")
                append("· 재도전: \"같은조합\"(직전 조합 그대로 새 런)\n")
                append("$DIV\n")
                append("🎉 준비 끝! 댓글로 \"잭팟\" 입력해서 첫 스핀을 돌려봐요!")
            }
        }
        return body + nav
    }

    // ── 장착 가능 장치 = 면허 취득(영구) ∪ 기존 보유(grandfather) ──
    //  P1: 장치는 면허(복합조건)로만 영구해금. 과거 노드/보스/업적으로 받은 ownedDevices 는 회수하지 않고 인정(grandfather).
    /** 이번 런 시작 시 장착 선택지 = unlockedDevices(stat) ∪ ownedDevices(grandfather). (DEVICE_SELECT) */
    private fun equipableDeviceList(sc: SlotV2ScoreRow?, stat: Map<String, Long>): List<SlotV2Engine.Device> {
        val grandfathered = (sc?.ownedDevices ?: "").split(",").filter { it.isNotBlank() }.mapNotNull { SlotV2Engine.device(it) }
        val unlocked = SlotV2Engine.unlockedDevices(stat)
        return (unlocked + grandfathered).distinctBy { it.id }
    }
}
