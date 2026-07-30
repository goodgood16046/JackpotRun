package com.ashersoft.kakaobot.game

import com.ashersoft.kakaobot.App
import com.ashersoft.kakaobot.chat.FirebaseRtdb
import org.json.JSONArray
import org.json.JSONObject

/**
 * 잭팟런 v2 웹 도감 동기화 — Firebase RTDB(jackpotdex/<token>) 에 플레이어 진행도/업적/랭킹 push.
 * 웹: jackpotrun-web.web.app/jackpotdex/?t=<token> 에서 get() 1회 읽기 (fishdex 패턴). 분리 프로젝트.
 * 웹→카톡 선택: jackpotcmd/<token> 에 웹이 선택 기록 → 봇이 런 시작 시 읽음(추후).
 */
object SlotV2WebService {
    private const val WEB_BASE = "https://jackpotrun-web.web.app"   // 독립 프로젝트로 분리(구 mokabot-8ed4d.web.app)
    private const val DEX = "$WEB_BASE/jackpotdex"   // 도감(읽기 전용)
    private const val PICK = "$WEB_BASE/jackpotpick"  // 선택 전용
    // 잭팟 전용 RTDB — 분리 프로젝트 jackpotrun-web. FirebaseRtdb 는 대시보드/마피아/낚시 공유라 BASE 를 못 바꿈 → 호출별 base override.
    private const val JP_RTDB = "https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app"

    /** (linkId,userId) 안정 토큰 — fishdex 와 동일 공식. **읽기(도감) 전용** — 공개 자랑용. */
    fun token(linkId: Long, nickname: String, userId: Long?): String {
        val uid = if (userId != null && userId != 0L) userId else nickname.hashCode().toLong()
        return "%08x%08x".format(linkId.hashCode(), uid.hashCode())
    }

    // ── 쓰기 토큰(웹 캐릭/머신 선택 핸드셰이크 jackpotcmd) ──────────────────────
    // 도감(읽기) 토큰은 결정론이라 같은 방 멤버가 재계산 가능 → 쓰기에 그대로 쓰면
    // 타인이 남의 런에 선택을 주입할 수 있다. 그래서 쓰기 전용 랜덤 토큰을 분리한다:
    // UUID 24-hex(~96bit) + 60분 TTL + 1인1개 + cid 첫사용 bind-window. (fishcmd 패턴)
    private const val WRITE_TTL_MS = 60 * 60 * 1000L   // 60분
    private const val BIND_WINDOW_MS = 5 * 60 * 1000L  // 발급 후 첫 cid 바인딩 허용 창(묵힌 링크 선점 차단)

    private data class WriteCtx(
        val linkId: Long, val nick: String, val userId: Long?,
        val createdAt: Long, @Volatile var issuedAt: Long,
        @Volatile var boundCid: String? = null,
    )
    private val writeTokens = java.util.concurrent.ConcurrentHashMap<String, WriteCtx>()
    private val userActiveWrite = java.util.concurrent.ConcurrentHashMap<String, String>()
    private fun userKey(linkId: Long, nick: String, userId: Long?): String =
        "$linkId:${if (userId != null && userId != 0L) userId else nick.hashCode()}"

    /** 본인 전용 랜덤 쓰기토큰 발급(1인1개, 이전 폐기). pushAndLink 에서 호출. */
    private fun issueWriteToken(linkId: Long, nick: String, userId: Long?): String {
        val now = System.currentTimeMillis()
        val key = userKey(linkId, nick, userId)
        userActiveWrite.remove(key)?.let { writeTokens.remove(it) }
        writeTokens.entries.removeIf { now - it.value.issuedAt > WRITE_TTL_MS }
        val t = java.util.UUID.randomUUID().toString().replace("-", "").take(24)
        writeTokens[t] = WriteCtx(linkId, nick, userId, now, now)
        userActiveWrite[key] = t
        return t
    }

    /**
     * 런 시작 시 웹 선택 소비 — 본인 활성 쓰기토큰 노드(jackpotcmd/<token>)를 읽고
     * cid bind-window 검증 후 (char,machine) 반환. 검증/소비 실패 시 노드 정리 후 null.
     * 결정론 토큰을 안 쓰므로 같은 방 멤버가 남의 선택을 주입할 수 없다.
     */
    suspend fun consumeWebPick(linkId: Long, nick: String, userId: Long?): Triple<String, String, String>? {
        val key = userKey(linkId, nick, userId)
        val t = userActiveWrite[key] ?: return null
        val ctx = writeTokens[t] ?: return null
        val now = System.currentTimeMillis()
        if (now - ctx.issuedAt > WRITE_TTL_MS) { writeTokens.remove(t); userActiveWrite.remove(key); return null }
        val body = runCatching { FirebaseRtdb.get("jackpotcmd/$t", JP_RTDB) }.getOrNull() ?: return null
        val cmd = runCatching { JSONObject(body) }.getOrNull() ?: return null
        val cid = cmd.optString("cid", "")
        if (ctx.boundCid != null && ctx.boundCid != cid) {             // 다른 기기 → 거부
            runCatching { FirebaseRtdb.delete("jackpotcmd/$t", JP_RTDB) }; return null
        }
        if (ctx.boundCid == null) {                                    // 첫 사용 cid 바인딩(발급 직후 창 안에서만)
            if (now - ctx.createdAt > BIND_WINDOW_MS) { runCatching { FirebaseRtdb.delete("jackpotcmd/$t", JP_RTDB) }; return null }
            ctx.boundCid = cid
        }
        ctx.issuedAt = now
        val cId = cmd.optString("char", ""); val mId = cmd.optString("machine", "")
        val dId = cmd.optString("device", "")   // 웹에서 고른 장치(영구 소지 중 1개, "" = 미장착)
        runCatching { FirebaseRtdb.delete("jackpotcmd/$t", JP_RTDB) }
        return if (cId.isNotBlank() && mId.isNotBlank()) Triple(cId, mId, dId) else null
    }

    // ── 도전 진행도 cur/max 산출 (다조건 AND → 진행률 가장 낮은 병목 조건의 현재/목표) ──
    //  웹 진행바용 단일 (cur,max). 전체 진행률은 별도 pct(reqProgress 평균)로 push.
    private fun bottleneck(req: List<Pair<String, Long>>, stat: Map<String, Long>): Pair<Long, Long>? =
        req.filter { it.second > 0 }.minByOrNull { (k, thr) ->
            ((stat[k] ?: 0L).toDouble() / thr).coerceIn(0.0, 1.0)
        }?.let { (k, thr) -> minOf(stat[k] ?: 0L, thr) to thr }
    private fun curOf(req: List<Pair<String, Long>>, stat: Map<String, Long>): Long = bottleneck(req, stat)?.first ?: 0L
    private fun maxOf2(req: List<Pair<String, Long>>, stat: Map<String, Long>): Long = bottleneck(req, stat)?.second ?: 0L

    /** 플레이어 상태 노드 — 진행도·업적·해금·랭킹 + (배치3b) 도전/숙련/빌드도감/기록/목표고정/해금힌트. */
    suspend fun buildNode(linkId: Long, nickname: String, userId: Long?): JSONObject {
        val sc = SlotV2Service.myScore(linkId, nickname, userId)
        val ach = SlotV2Service.myAch(linkId, nickname, userId)
        val best = sc?.bestScore ?: 0L; val runs = sc?.runs ?: 0; val bStage = sc?.bestStage ?: 0
        val node = JSONObject()
        node.put("nick", nickname)
        node.put("title", SlotV2Engine.titleStr(best))
        node.put("bestScore", best)
        node.put("bestStage", bStage)
        node.put("runs", runs)
        node.put("totalScore", sc?.totalScore ?: 0L)
        node.put("updatedAt", System.currentTimeMillis())

        // 업적 (id → 달성 여부 + 진행/목표)
        val unlocked = (ach?.unlocked ?: "").split(",").filter { it.isNotBlank() }.toSet()
        val achObj = JSONObject()
        for (a in SlotV2Engine.ACHIEVEMENTS) {
            val cur = SlotV2Service.achCounter(ach, a.key)
            achObj.put(a.id, JSONObject()
                .put("done", a.id in unlocked)
                .put("cur", minOf(cur, a.threshold))
                .put("max", a.threshold))
        }
        node.put("ach", achObj)
        node.put("achDone", unlocked.size)
        node.put("achTotal", SlotV2Engine.ACHIEVEMENTS.size)

        // 📖 도감 사용기록 — seen_<id> 카운터 → {id: 사용횟수}. 없으면 웹에서 ??? 처리.
        val usedObj = JSONObject()
        (ach?.counters ?: "").split(",").filter { it.isNotBlank() }.forEach {
            val pp = it.split(":"); if (pp.size == 2 && pp[0].startsWith("seen_")) usedObj.put(pp[0].removePrefix("seen_"), pp[1].toLongOrNull() ?: 0)
        }
        node.put("used", usedObj)

        // 해금된 캐릭/머신 id (복합조건 stat 기반)
        val stat = SlotV2Service.playerStat(linkId, nickname, userId)
        node.put("chars", JSONArray(SlotV2Engine.unlockedChars(stat).map { it.id }))
        node.put("machines", JSONArray(SlotV2Engine.unlockedMachines(stat).map { it.id }))
        // 장착 가능 장치 = 면허취득(영구) ∪ 기존 보유(grandfather). 웹 시작선택 UI 장치 슬롯.
        node.put("ownedDevices", JSONArray(SlotV2Service.equipableDeviceIds(linkId, nickname, userId)))

        // ── 배치3b 신규필드 (상시도전·숙련·빌드도감·기록·목표고정·해금힌트) ──────────
        // 🏆 상시 도전판 — 장치면허 + 캐릭/머신 복합해금 + 표준도전 통합 (allChallenges)
        val chArr = JSONArray()
        for (ci in SlotV2Engine.allChallenges(stat)) {
            chArr.put(JSONObject()
                .put("id", ci.id)
                .put("e", ci.emoji)
                .put("n", ci.name)
                .put("kind", ci.kind.name)                 // DEVICE / CHAR / MACHINE / STANDARD
                .put("done", ci.done)
                .put("cur", curOf(ci.req, stat))           // 진행도 — 병목(가장 덜 찬) 조건의 현재
                .put("max", maxOf2(ci.req, stat))          // 병목 조건의 목표
                .put("pct", (SlotV2Engine.reqProgress(ci.req, stat) * 100).toInt())
                .put("reward", ci.rewardHint))
        }
        node.put("challenges", chArr)

        // 🏅 숙련도 — 캐릭/머신별 메달(none/bronze/silver/gold) + 최고스테이지
        val masChars = JSONArray()
        for (c in SlotV2Engine.CHARS) {
            masChars.put(JSONObject()
                .put("id", c.id)
                .put("medal", SlotV2Engine.charMastery(c.id, stat).name.lowercase())
                .put("stage", SlotV2Engine.charBestStage(c.id, stat)))
        }
        val masMachines = JSONArray()
        for (m in SlotV2Engine.MACHINES) {
            masMachines.put(JSONObject()
                .put("id", m.id)
                .put("medal", SlotV2Engine.machineMastery(m.id, stat).name.lowercase())
                .put("stage", SlotV2Engine.machineBestStage(m.id, stat)))
        }
        node.put("mastery", JSONObject().put("chars", masChars).put("machines", masMachines))

        // 📦 빌드도감 — 플레이한 (캐릭,머신,최고스테이지) 조합 + 총 조합 수
        val bdArr = JSONArray()
        for (r in SlotV2Engine.buildDex(stat)) {
            bdArr.put(JSONObject().put("c", r.charId).put("m", r.machineId).put("stage", r.stage))
        }
        node.put("builddex", bdArr)
        node.put("buildDexTotal", SlotV2Engine.buildDexTotal())

        // 🏗️ 테마 빌드도감 — 카테고리별 25종(성장형/운명형/역전형/조합형/위험형). 완성 = stat[bld_<id>] > 0.
        val tbArr = JSONArray()
        for (b in SlotV2Engine.THEME_BUILDS) {
            tbArr.put(JSONObject()
                .put("id", b.id)
                .put("name", b.name)
                .put("e", b.emoji)
                .put("category", b.category)
                .put("done", SlotV2Engine.themeBuildDone(b.id, stat))
                .put("cond", b.cond))
        }
        node.put("themeBuilds", tbArr)
        node.put("themeBuildDone", SlotV2Engine.themeBuildsDoneCount(stat))
        node.put("themeBuildTotal", SlotV2Engine.themeBuildTotal())

        // 📈 개인기록 — "라벨: 값" 라인 리스트 (recordLines)
        node.put("records", JSONArray(SlotV2Engine.recordLines(stat)))

        // 📌 목표고정 — score.pinnedChallenge (없으면 "")
        node.put("pinned", sc?.pinnedChallenge ?: "")

        // 🔓 해금 힌트 — 잠금/미취득 캐릭·머신·장치의 해금조건 힌트(t)+진행률(pct)+달성여부(done)
        val unChars = JSONObject()
        for (c in SlotV2Engine.CHARS.filter { it.unlockReq.isNotEmpty() }) {
            unChars.put(c.id, JSONObject()
                .put("t", SlotV2Engine.charHint(c, stat))
                .put("pct", (SlotV2Engine.reqProgress(c.unlockReq, stat) * 100).toInt())
                .put("done", SlotV2Engine.charUnlocked(c, stat)))
        }
        val unMachines = JSONObject()
        for (m in SlotV2Engine.MACHINES.filter { it.unlockReq.isNotEmpty() }) {
            unMachines.put(m.id, JSONObject()
                .put("t", SlotV2Engine.machineHint(m, stat))
                .put("pct", (SlotV2Engine.reqProgress(m.unlockReq, stat) * 100).toInt())
                .put("done", SlotV2Engine.machineUnlocked(m, stat)))
        }
        val unDevices = JSONObject()
        for (d in SlotV2Engine.DEVICES.filter { it.unlockAch.isNotBlank() }) {
            unDevices.put(d.id, JSONObject()
                .put("t", SlotV2Engine.deviceUnlockHint(d, stat))
                .put("pct", (SlotV2Engine.deviceUnlockProgress(d, stat) * 100).toInt())
                .put("done", SlotV2Engine.deviceUnlocked(d, stat)))
        }
        node.put("unlock", JSONObject().put("chars", unChars).put("machines", unMachines).put("devices", unDevices))

        // 🌈 졸업레벨(accountLevel/accountExp) — 해금 게이트 진행 지표
        node.put("accountLevel", SlotV2Engine.accountLevel(stat))
        node.put("accountExp", SlotV2Engine.accountExp(stat))

        // 🔒 증강/유물 해금 상태 — 카탈로그/도감 마스킹용(locked/school/hint). 데이터 제공까지(클라 마스킹은 별도).
        fun perkUnlockArr(list: List<SlotV2Engine.Perk>) = JSONArray().apply {
            list.forEach { p ->
                put(JSONObject()
                    .put("id", p.id)
                    .put("locked", !SlotV2Engine.perkUnlocked(p, stat))
                    .put("school", SlotV2Engine.perkGate(p).school)
                    .put("hint", SlotV2Engine.perkUnlockHint(p, stat)))
            }
        }
        node.put("perkUnlock", JSONObject()
            .put("augments", perkUnlockArr(SlotV2Engine.AUGMENTS))
            .put("relics", perkUnlockArr(SlotV2Engine.RELICS)))

        // 랭킹 TOP10 — (#10) 서로 다른 userId 가 같은 표시닉이면 낮은 순위쪽에 숫자 suffix(최고=원닉, 다음=닉2…)
        val rankArr = JSONArray()
        val topRows = SlotV2Service.topByBest(linkId, 10)
        val seenCount = HashMap<String, Int>()       // 표시닉 → 등장한 서로다른 userId 수
        val byUid = HashMap<Long, String>()          // userId>0 → 이미 부여한 표시닉(재사용)
        // 채팅 displayNick 과 동일 정규화(user_→uid_, 20자 base64→(알수없음)) — 채팅/웹 닉충돌 기준 일치
        fun dispNick(nick: String): String =
            if (nick.startsWith("user_")) "uid_" + nick.removePrefix("user_")
            else if (nick.length >= 20 && nick.matches(Regex("^[A-Za-z0-9+/=]+$"))) "(알수없음)"
            else nick
        topRows.forEach { r ->
            val uid = r.userId?.takeIf { it > 0L }
            val base = dispNick(r.nickname)
            val disp = (uid?.let { byUid[it] }) ?: run {
                val n = (seenCount[base] ?: 0) + 1
                seenCount[base] = n
                val d = if (n == 1) base else "$base$n"
                if (uid != null) byUid[uid] = d
                d
            }
            rankArr.put(JSONObject().put("nick", disp).put("score", r.bestScore).put("stage", r.bestStage))
        }
        node.put("rank", rankArr)
        return node
    }

    /** 전체 카탈로그(증강/유물/아이템/저주/세트/장치) — 엔진이 소스, 공유 노드에 1회 push. */
    fun buildCatalog(): JSONObject {
        fun perkArr(list: List<SlotV2Engine.Perk>) = JSONArray().apply {
            list.forEach { put(JSONObject().put("id", it.id).put("e", it.emoji).put("n", it.name).put("d", it.desc).put("t", it.tier.name)) }
        }
        val o = JSONObject()
        o.put("augments", perkArr(SlotV2Engine.AUGMENTS))
        o.put("relics", perkArr(SlotV2Engine.RELICS))
        o.put("curses", perkArr(SlotV2Engine.CURSES))
        o.put("items", JSONArray().apply {
            SlotV2Engine.ITEMS.forEach { put(JSONObject().put("id", it.id).put("e", it.emoji).put("n", it.name).put("d", it.desc).put("k", it.kind.name)) }
        })
        o.put("sets", JSONArray().apply {
            SlotV2Engine.SETS.forEach { put(JSONObject().put("id", it.id).put("n", it.name).put("d", it.desc)) }
        })
        o.put("devices", JSONArray().apply {
            SlotV2Engine.DEVICES.forEach { put(JSONObject().put("id", it.id).put("e", it.emoji).put("n", it.name).put("d", it.desc).put("cmd", it.cmd).put("kind", it.kind.name)) }
        })
        o.put("chars", JSONArray().apply {            // 캐릭/머신도 엔진 desc 소스로 push(웹 정적 drift 방지)
            SlotV2Engine.CHARS.forEach { put(JSONObject().put("id", it.id).put("e", it.emoji).put("n", it.name).put("d", it.desc)) }
        })
        o.put("machines", JSONArray().apply {
            SlotV2Engine.MACHINES.forEach { put(JSONObject().put("id", it.id).put("e", it.emoji).put("n", it.name).put("d", it.desc)) }
        })
        o.put("achievements", JSONArray().apply {
            SlotV2Engine.ACHIEVEMENTS.forEach { put(JSONObject().put("id", it.id).put("e", it.emoji).put("n", it.name).put("d", it.desc).put("cat", it.cat).put("tier", it.tier).put("reward", it.reward).put("hidden", it.hidden)) }
        })
        return o
    }
    suspend fun pushCatalog() { runCatching { FirebaseRtdb.put("jackpotcatalog", buildCatalog().toString(), JP_RTDB) } }

    /** 시즌 아카이브 — 현재 top-10을 jackpothall/seasons/<key> 에 저장 (월간/특별 시즌, 웹 명예의전당 표시). */
    suspend fun archiveSeason(linkId: Long, key: String, label: String, tsMs: Long): Int {
        val top = SlotV2Service.topByBest(linkId, 10)
        val arr = JSONArray()
        top.forEachIndexed { i, r -> arr.put(JSONObject().put("rank", i + 1).put("nick", r.nickname).put("score", r.bestScore).put("stage", r.bestStage)) }
        val node = JSONObject().put("label", label).put("ts", tsMs).put("top", arr)
        runCatching { FirebaseRtdb.put("jackpothall/seasons/$key", node.toString(), JP_RTDB) }
        return top.size
    }

    /** 📖 도감(읽기 전용) 링크 — push 후 t만. 선택 UI 없음. */
    suspend fun linkDex(linkId: Long, nickname: String, userId0: Long?): String {
        val userId = SlotV2Service.resolveUid(linkId, nickname, userId0)   // (#12) canonical uid — 웹 토큰이 게임 노드와 일치
        val t = token(linkId, nickname, userId)
        runCatching { FirebaseRtdb.put("jackpotdex/$t", buildNode(linkId, nickname, userId).toString(), JP_RTDB) }
        pushCatalog()
        return "$DEX/?t=$t"
    }

    /** 🎮 선택 전용 링크 — push + 랜덤 쓰기토큰 w. 캐릭/머신 선택만 노출(별도 페이지). */
    suspend fun linkPick(linkId: Long, nickname: String, userId0: Long?): String {
        val userId = SlotV2Service.resolveUid(linkId, nickname, userId0)   // (#12) canonical uid
        val t = token(linkId, nickname, userId)
        runCatching { FirebaseRtdb.put("jackpotdex/$t", buildNode(linkId, nickname, userId).toString(), JP_RTDB) }
        val w = issueWriteToken(linkId, nickname, userId)
        return "$PICK/?t=$t&w=$w"
    }

    /** 하위호환 — 시작 화면 등은 선택 링크. */
    suspend fun pushAndLink(linkId: Long, nickname: String, userId: Long?): String = linkPick(linkId, nickname, userId)

    /** 자동 동기화(게임오버 등) — 링크 불필요, 조용히 push. 카탈로그도 함께 갱신. */
    suspend fun sync(linkId: Long, nickname: String, userId0: Long?) {
        val userId = SlotV2Service.resolveUid(linkId, nickname, userId0)   // (#12) canonical uid
        runCatching {
            val t = token(linkId, nickname, userId)
            FirebaseRtdb.put("jackpotdex/$t", buildNode(linkId, nickname, userId).toString(), JP_RTDB)
        }
        pushCatalog()
    }
}
