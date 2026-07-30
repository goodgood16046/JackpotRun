> 원본: SlotV2Engine.kt @ 커밋 c73452c, 추출일 2026-07-30
> 대상 파일: `e:\UnityProject\JackpotRun\kotlin-reference\game\SlotV2Engine.kt` (전체 2,406줄, `object SlotV2Engine`)
> 이 문서는 원본 수치를 반올림·요약 없이 그대로 옮긴 C# 이식용 1차 사양이다. 각 항목에 Kotlin 라인 번호를 병기했다.
> 의존 외부 파일: `SlotV2AchievementsExt.kt` (라인 1489에서 `ACHIEVEMENTS = ACHIEVEMENTS_BASE + SlotV2AchievementsExt.LIST`로 합류 — 본 문서 범위 밖, 별도 추출 필요).

---

## 0. 밸런스 상수 전체 (L21–95)

| 상수 | 값 | 라인 | 비고 |
|---|---|---|---|
| `REEL` | 5 | 22 | 단일 라인 5칸 |
| `SPINS_PER_STAGE` | 5 | 23 | |
| `MIN_SPINS` | 3 | 24 | `spinsPerStage()` 하한 |
| `COIN_BASE` | 0 | 25 | 스핀당 기본 코인 |
| `SKULL_PENALTY` | 3 | 26 | ☠ 1개당 기본 페널티(×skullPenaltyMul) |
| `BOMB_EXP_PER` | 8 | 27 | 💣 제거 1칸당 EXP |
| `KEY_COIN_PER` | 4 | 28 | 🗝 1개당 보물 코인 |
| `MAX_SPIN_EXP_MUL` | 8.0 | 29 | 비보스+프리즘 보유 시 캡(capMulFor) |
| `UNLUCKY_MAX` | 5 | 30 | 불운 게이지 만땅 |
| `CMD_COST_FOCUS` | 1 | 34 | 집중 |
| `CMD_COST_LAST` | 2 | 35 | 최후 |
| `CMD_COST_PRAY` | 3 | 36 | 기도 |
| `CMD_COST_ALLIN` | 4 | 37 | 올인 |
| `CMD_COST_BOSS_SURCHARGE` | 1 | 38 | 보스 스테이지 가산 |
| `CMD_COST_MAX` | 5 | 39 | 비용 상한(0은 그대로 유지) |
| `CLEAR_COIN` | 5 | 79 | ※ 엔진 내부 미사용, 서비스 전용 상수 |
| `BOSS_COIN` | 12 | 80 | ※ 엔진 내부 미사용 |
| `ELITE_COIN` | 8 | 81 | ※ 엔진 내부 미사용 |
| `SCORE_PER_LEFTOVER` | 2 | 82 | stageClearScore에서 사용 |
| `SCORE_PER_LEFTSPIN` | 100 | 83 | stageClearScore에서 사용 |
| `CLOSE_CLEAR_BONUS` | 150L | 84 | ※ 엔진 내부 미사용(정의만), 서비스 전용 |
| `BOSS_CLEAR_SCORE` | 500L | 85 | stageClearScore에서 사용 |
| `RETAKE_COIN_COST` | 8 | 1065 | dev_retake 재추첨 코인 비용 |
| `SECONDARY_MUL` | 0.6 | 1112 | 보조 장치 슬롯 약화 배수 |
| `MEDAL_BRONZE_S` | 5 | 1231 | |
| `MEDAL_SILVER_S` | 10 | 1232 | |
| `MEDAL_GOLD_S` | 15 | 1233 | |

`CLEAR_COIN`/`BOSS_COIN`/`ELITE_COIN`/`CLOSE_CLEAR_BONUS`는 정의만 되어 있고 `SlotV2Engine.kt` 내부 어디에서도 참조되지 않는다(grep 확인). 서비스 레이어(별도 파일)에서 사용하는 상수로 추정 — C# 이식 시 엔진쪽 이 값들은 "노출만" 하면 되고 로직 의존은 없다.

---

## 1. 코어 공식

### 1.1 quota(stage) — 스테이지 요구 EXP (L41–51)

```
QUOTAS = longArrayOf(110, 120, 130, 140, 150, 170, 200, 235, 280, 330, 390, 460, 540, 640, 755)
// index 0..14 = stage 1..15
```

```kotlin
fun quota(stage: Int): Long {
    val i = stage - 1
    if (i < 0) return QUOTAS[0]                 // stage <= 0 → 110
    if (i < QUOTAS.size) return QUOTAS[i]        // stage 1..15 → 표 그대로
    var q = QUOTAS.last().toDouble()             // 755.0
    repeat(i - QUOTAS.size + 1) { q *= 1.2 }      // stage 16+: (stage-15)회 반복 곱
    return q.toLong()
}
```

- stage 16: `755 * 1.2` = 906.0 → 906
- stage 17: `755 * 1.2 * 1.2` = 1087.2 → 1087
- 이후 stage마다 반복 인덱스만큼 **누적 반복곱**(×1.2를 (stage-15)번 순차 곱셈). `Math.pow(1.2, n)` 한 번으로 재구현하지 말 것 — 반복곱과 거듭제곱은 부동소수 반올림이 다를 수 있음(§10 참조).

### 1.2 보스 시스템 (L53–68)

```kotlin
fun isBossStage(stage: Int): Boolean = stage % 5 == 0
```

```kotlin
data class Boss(val id: String, val emoji: String, val name: String, val desc: String,
                val bonusSpins: Int = 2, val quotaMul: Double = 1.0, val counterTags: List<String> = emptyList())
```

**BOSSES 목록** (L60–65, 5스테이지마다 `((stage/5)-1) % 4` 로 순환):

| id | emoji | name | desc(원문) | bonusSpins | quotaMul | counterTags |
|---|---|---|---|---|---|---|
| finals | 📝 | 기말고사 | 스핀+1·요구↑ · 막스핀 EXP ×2 · 첫스핀 -10% | 1 | 1.0(기본) | "막판형(복습·벼락치기)", "⏰최후" |
| strict | 👨‍🏫 | 꼰대교수 | 스핀+1·요구↑ · 같은심볼 3개↑ 없는 스핀 ×0.5 | 1 | 1.0(기본) | "세트·콤보(자석·체리)", "📌고정·📑복사" |
| luck | 🎲 | 운빨심판관 | 스핀+1·요구↑ · ⭐👑🌀 있으면 ×1.8/없으면 ×0.8 | 1 | 1.0(기본) | "⭐👑🌀 등장↑", "🔮예언·🎲올인" |
| grad | 🎓 | 졸업심사 | 스핀+1·요구↑↑ (빡센 관문) | 1 | **1.15** | "EXP 총량(과부하·탐욕)", "🚑응급처치·아이템" |

- `Boss` data class 기본 `bonusSpins=2`이지만 4종 전부 명시적으로 `1`을 넘겨 오버라이드(L59 주석: "bonusSpins 각 -1, 2026-06-29 — 보스 스핀 여유 축소로 난이도 상향"). **실질 bonusSpins는 항상 1.**
- `quotaMul`은 grad(졸업심사)만 1.15, 나머지 3종은 desc에 "요구↑"라고 적혀 있어도 코드상 배수는 기본값 1.0(= 무보정). **desc 문구와 실제 수치가 불일치하는 지점 — §11 특이사항 참조.**
- `counterTags`, desc의 "막스핀 EXP ×2", "첫스핀 -10%", "같은심볼 3개↑ 없는 스핀 ×0.5", "⭐👑🌀 배수" 등 보스별 개별 전투 규칙은 **SlotV2Engine.kt 어디에도 실행 코드가 없다**(grep 검증됨). `bossFor`/`bossSpins`/`bossQuotaMul`/`capMulFor`만 실제로 소비되는 값이고, 나머지는 순수 설명 텍스트이거나 서비스 레이어(미확인 파일)에 구현되어 있을 가능성.

```kotlin
fun bossFor(stage: Int): Boss? = if (stage % 5 == 0) BOSSES[((stage / 5) - 1) % BOSSES.size] else null
fun bossSpins(stage: Int): Int = bossFor(stage)?.bonusSpins ?: 0
fun bossQuotaMul(stage: Int): Double = bossFor(stage)?.quotaMul ?: 1.0
```

### 1.3 capMulFor — 스핀 총배율 상한 (L70–76)

```kotlin
fun capMulFor(stage: Int, hasPrism: Boolean): Double = when {
    bossFor(stage) != null -> if (hasPrism) 5.0 else 4.0
    hasPrism -> MAX_SPIN_EXP_MUL   // 8.0
    else -> 5.0
}
```
보스 스테이지: 프리즘 보유 5.0 / 미보유 4.0. 비보스: 프리즘 보유 8.0 / 미보유 5.0. `evaluate()`의 `capMul` 파라미터로 전달되어 §2.6/§9의 상세 클램프 로직에 사용됨.

### 1.4 stageClearScore (L87–95)

```kotlin
fun stageClearScore(stage: Int, leftoverExp: Long, leftSpins: Int, curses: Int, boss: Boolean): Long {
    var s = stage * 50.0
    s += leftoverExp * SCORE_PER_LEFTOVER   // ×2
    s += leftSpins * SCORE_PER_LEFTSPIN     // ×100
    if (boss) s += BOSS_CLEAR_SCORE         // +500
    s *= (1.0 + 0.05 * curses)              // 저주 1개당 +5%
    return s.toLong()
}
```
정확한 식: `score = (stage×50 + leftoverExp×2 + leftSpins×100 [+500 if boss]) × (1 + 0.05×curses)`, 최종 `Double.toLong()` 절삭(0으로 반올림, 음수 없음 전제).

### 1.5 tierForClearedStage / tierUp (L672–680)

```kotlin
fun tierForClearedStage(stage: Int): Tier = when {
    stage <= 0 -> Tier.SILVER
    stage % 5 == 0 -> Tier.PRISM
    stage % 3 == 0 -> Tier.GOLD
    else -> Tier.SILVER
}
fun tierUp(t: Tier): Tier = when (t) { Tier.SILVER -> Tier.GOLD; Tier.GOLD -> Tier.PRISM; Tier.PRISM -> Tier.PRISM }
```
5의 배수가 3의 배수보다 **우선 판정**(when 순서, 15는 PRISM으로 판정되고 GOLD 분기는 도달하지 않음).

### 기타 도파민 공식

```kotlin
fun scoreTitle(best: Long): Pair<String,String> = when {
    best >= 100_000 -> "🌈" to "잭팟의 지배자"
    best >= 50_000  -> "👑" to "슬롯 마스터"
    best >= 25_000  -> "🔥" to "하이롤러"
    best >= 12_000  -> "🏅" to "슬롯 숙련자"
    best >= 6_000   -> "💰" to "단골 도박꾼"
    best >= 3_000   -> "🎲" to "도전자"
    best >= 1_000   -> "🎰" to "슬롯 입문자"
    else            -> "🐣" to "잭팟 새내기"
}
```
(L2033–2042)

```kotlin
fun streakBonus(stage: Int): Long = when {
    stage >= 15 -> 600; stage >= 10 -> 350; stage >= 7 -> 200; stage >= 4 -> 100; stage >= 2 -> 40; else -> 0
}
```
(L2046–2053, 연승 보너스)

---

## 2. 심볼 시스템

### 2.1 Sp enum / Sym 데이터클래스 (L98–106)

```kotlin
enum class Sp { NONE, WILD, BOMB, MAGNET, SKULL, DICE, COIN, KEY, FLAME, SEED }
data class Sym(
    val id: String, val emoji: String, val name: String,
    val exp: Int = 0, val score: Int = 0, val coin: Int = 0,
    val weight: Int = 0, val special: Sp = Sp.NONE, val rare: Boolean = false,
    val tags: Set<String> = emptySet(),
)
```
`EMPTY = Sym("empty", "▫", "빈칸")` — exp/score/coin/weight 전부 0, special=NONE. `EMPTY_PUB`로 공개(applyCellOps에서 셀 제거 시 사용).

### 2.2 SYMS 전체 목록 (L113–129, 14종: 활성 10 + 휴면 4)

| id | emoji | name | exp | score | coin | weight | special | rare | tags |
|---|---|---|---|---|---|---|---|---|---|
| cherry | 🍒 | 체리 | 3 | 0 | 0 | 25 | NONE | false | {생명} |
| book | 📘 | 책 | 6 | 0 | 0 | 18 | NONE | false | {학습} |
| star | ⭐ | 별 | 8 | 0 | 0 | 13 | NONE | false | {콤보} |
| gem | 💎 | 보석 | 1 | 15 | 0 | 12 | NONE | false | {점수} |
| coin | 🪙 | 코인 | 0 | 0 | 1 | 10 | COIN | false | {코인} |
| skull | ☠ | 해골 | 0 | 0 | 0 | 10 | SKULL | false | {저주} |
| flame | 🔥 | 불꽃 | 0 | 0 | 0 | 5 | FLAME | false | {배율} |
| magnet | 🧲 | 자석 | 2 | 0 | 0 | 4 | MAGNET | false | {조작} |
| bomb | 💣 | 폭탄 | 5 | 0 | 0 | 2 | BOMB | false | {폭발} |
| crown | 👑 | 왕관 | 20 | 50 | 0 | 1 | NONE | **true** | {왕관, 희귀} |
| key | 🗝 | 열쇠 | 6 | 0 | 0 | **0**(휴면) | KEY | false | {열쇠} |
| dice | 🎲 | 주사위 | 0 | 0 | 0 | **0**(휴면) | DICE | false | {운} |
| seed | 🌱 | 씨앗 | 0 | 0 | 0 | **0**(휴면) | SEED | false | {생명, 성장} |
| wild | 🌀 | 와일드 | 0 | 0 | 0 | **0**(휴면) | WILD | **true** | {희귀, 조작} |

`rare=true`는 **crown·wild 단 2종뿐**(⭐별은 콤보 태그이지만 rare 아님 — L2084 주석 명시). 휴면 4종(key/dice/seed/wild)은 기본 가중 0으로 절대 등장하지 않고, 머신 `weightAdd`/perk `wadd()`(과부하 계열)로만 주입됨. `VALUE_IDS = {cherry, star, book, gem, crown}`(L131) — 세트/잭팟/인접/양끝 판정 대상.

### 2.3 세트 보너스 테이블 (L133–135)

```kotlin
SET_EXP   = intArrayOf(0, 0, 8, 18, 42, 100)   // index = 개수(0~5)
SET_SCORE = intArrayOf(0, 0, 3, 9, 24, 70)
```
인덱스는 `bestCount.coerceAtMost(5)`로 클램프(6칸 이상 릴에서도 배열 밖 접근 없음).

### 2.4 릴/칸 수

기본 `REEL=5`(단일 라인). `dev_subreel` 장치 장착 시 서비스가 6칸으로 확장(엔진의 `reel` 파라미터로 전달, `evaluate()`/`rollRaw()`는 `reel: Int` 인자를 받아 가변 길이 지원 — L2095, L2131).

### 2.5 잭팟 규칙 (L2239–2248)

```kotlin
if (bestId != null && bestCount >= reel && reel >= 5) {
    jackpotSym = bestId
    val jb = when (bestId) {
        "cherry" -> 120; "book" -> 320; "star" -> 360; "gem" -> 160; "crown" -> 520; else -> 200
    }
    jackpotFixed += jb        // EXP 고정가산(캡 이후 별도 가산)
    score += jb * 5           // 점수 = jb×5
}
```
**전 칸(reel칸 전부) 동일 심볼(와일드 합류 포함) 일치가 잭팟 조건.** `reel>=5` 가드가 있어 6칸 확장 시(dev_subreel)에도 "6칸 전부 일치"가 필요하며 5칸 일치로는 잭팟이 아니다. `else -> 200`은 VALUE_IDS 5종 외 케이스로 현재 코드상 도달 불가능(방어 코드).

### 2.6 콤보/세트 판정 (L2174–2237)

```kotlin
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
```
- 와일드는 카운트 후 "최다 그룹"에 전량 합류(가장 많은 value 심볼 종류에 가산). **동률(tie) 시 `maxByOrNull`은 HashMap 순회 순서상 처음 발견된 최댓값을 반환** — HashMap 순서는 Kotlin/JVM 구현에 의존적이라 재현성 없음(§10 이식 주의).
- value 심볼이 하나도 없고 와일드만 있으면 **"cherry" 그룹으로 강제 귀속**(L2183) — 명시적 특수 케이스, 이식 시 누락 주의.
- 세트 보너스(L2229–2237): `bestCount>=2`일 때 발동. `pair_match` 퍽 보유 시 `bestCount==2`일 때만 `twoSetBonusMul`(기본 1.0) 추가 곱. `add = SET_EXP[n] * mods.setExpMul * twoMul`.
- 인접쌍(L2251–2258): 붙어있는 두 칸이 같은 VALUE_IDS 심볼이면 쌍마다 `mods.adjacentSameExp` 가산.
- 양끝일치(L2260–2264): `cells[0]`과 `cells[reel-1]`이 같은 VALUE_IDS 심볼이면 `exp *= mods.endsMatchExpMul`(곱연산, reel>=2 조건).
- perfect_shape(L2300–2313): 양끝 일치 + 가운데 칸이 같은 계열(또는 와일드로 대체 충족)이면 EXP 배수. 실심볼로만 충족 시 `mods.perfectShapeExpMul`(퍽 값), 와일드 보조로 충족 시 고정 `1.7` 적용(퍽 값보다 약화).

---

## 3. 머신 16종 (L227–284)

```kotlin
data class Machine(
    val id: String, val emoji: String, val name: String, val desc: String,
    val weightMul: Map<String, Double> = emptyMap(),
    val scoreMod: Double = 1.0,
    val weightAdd: Map<String, Double> = emptyMap(),
    val unlockReq: List<Pair<String, Long>> = emptyList(),
)
```

| id | emoji/name | weightMul | scoreMod | weightAdd | unlockReq |
|---|---|---|---|---|---|
| basic | 🎰 기본 | {} | 1.0 | {} | [] (스타터/무료) |
| cherry | 🍒 체리 | cherry×1.5, crown×0.6 | 0.95 | {} | cherryTotal≥200 & bestStage≥4 |
| library | 📚 도서관 | book×1.5, coin×0.6, gem×0.6 | 1.0 | {} | bookTotal≥200 & lastSpinClears≥3 |
| gem | 💎 보석 | gem×1.7, book×0.6, cherry×0.6 | 1.1 | {} | gemTotal≥250 & bestScore≥4,000 |
| magnet | 🧲 자석 | magnet×2.5 | 1.0 | {} | set4Plus≥8 & bestStage≥6 |
| skull | ☠ 해골 | skull×1.8 | 1.10 | {} | skullTotal≥250 & curseMax≥3 |
| crown | 👑 왕관 | crown×2.0, cherry×0.7, book×0.7 | 1.2 | {} | crownTotal≥40 & jackpots≥3 |
| flame | 🔥 불꽃 | flame×1.8, skull×1.4 | 1.1 | {} | bestScore≥15,000 & bestStage≥10 |
| bomb | 💣 폭탄 | bomb×2.5 | 1.1 | {} | bossClears≥5 & bestStage≥10 |
| star | ⭐ 별빛 | star×2.0, cherry×0.8 | 1.05 | {} | starTotal≥200 & set4Plus≥10 |
| clover | 🍀 행운 | crown×1.3, coin×1.4, flame×1.3 | 1.05 | {} | prayClears≥3 & bestStage≥8 |
| casino | 🎲 카지노 | {} | 1.1 | dice+4.0 | gambles≥5 & allinWins≥5 |
| garden | 🌱 정원 | {} | 1.05 | seed+4.0 | cherryTotal≥400 & bestStage≥9 |
| wildmac | 🌀 와일드 | {} | 1.1 | wild+3.0 | prismPicks≥8 & jackpots≥5 |
| vault | 🗝 금고 | coin×1.5 | 1.10 | key+3.0 | coinTotal≥600 & shopBuys≥20 |
| rainbow | 🌈 무지개 | crown×1.6, star×1.4, gem×1.3, cherry×0.6, book×0.6 | 1.2 | {} | bestScore≥25,000 & jackpots≥10 |

`weightMul`은 심볼 최종 가중치에 **곱연산**(`weighted()`에서 `x *= symbolWeightMul[id]`), `weightAdd`는 **가산**(`x += weightAdd[id]`, 휴면 심볼 주입용). `BASE_MACHINE = MACHINES[0]`(basic). `unlockReq`는 AND 조건(§8 참조).

---

## 4. 캐릭터 16종 (L310–365, buildMods 처리는 L1755–1773 + 후처리 L2006–2008)

```kotlin
data class Character(
    val id: String, val emoji: String, val name: String, val desc: String,
    val scoreMod: Double = 1.0, val startCoins: Int = 0,
    val unlockReq: List<Pair<String, Long>> = emptyList(),
)
```

| id | emoji/name | scoreMod | startCoins | buildMods 실처리 | unlockReq |
|---|---|---|---|---|---|
| novice | 🎒 초보학생 | 0.9 | 0 | `quotaMul *= 0.92` | [] 스타터 |
| scholar | 📗 장학생 | 1.0 | 0 | `pse(book,+2)`; `clearCoinBonus += 2` | [] 스타터 |
| gambler | 🎲 도박꾼 | 1.1 | 0 | **buildMods when-block에 케이스 없음.** scoreMod=1.1만 적용. desc의 "스테이지당 1회 무료 재굴림"은 서비스 레이어 전용(엔진 미구현) | gambles≥12 & allinWins≥5 |
| farmer | 🍒 체리농부 | 0.95 | 0 | `pse(cherry,+1)`; `rareWeightMul *= 0.9` | cherryTotal≥1200 & mstage_cherry≥8 |
| parttime | 🪙 알바생 | 1.0 | **15** | `firstSpinExpMul *= 0.8` | coinTotal≥1000 & shopBuys≥20 |
| jeweler | 💎 보석상 | 1.1 | 0 | `pss(gem,+25)` | gemTotal≥1200 & bestScore≥15,000 |
| honor | 🎓 수석졸업생 | 1.0 | 0 | **buildMods에 케이스 없음**(주석 확인). desc "실버 증강 1개로 시작"은 서비스가 런 시작 시 별도 지급 | exactClears≥6 & bestStage≥12 |
| cultist | 💀 해골숭배자 | 1.15 | 0 | `skullExp += 3`(when) **+** 후처리: `curseIds`가 비어있지 않으면 `scoreMul *= (1.0 + 0.08×curseIds.size)`(L2007) | skullTotal≥1200 & curseMax≥5 |
| crowncol | 👑 왕관수집가 | 1.15 | 0 | `pss(crown,+30)`; `wmul(crown,×1.5)` | crownTotal≥250 & jackpots≥6 |
| minimalist | 🍃 미니멀리스트 | 1.1 | 0 | **when-block에 케이스 없음.** 후처리: `perkIds.count{cat==RELIC} <= 3`이면 `expMul *= 1.25`(L2008) | minimalistS10≥2 |
| lucky | 🍀 행운아 | 1.05 | 0 | `rareWeightMul *= 1.25` | prayClears≥8 |
| highroller | 💠 큰손 | 1.1 | **12** | `pss(gem,+25)` | coinTotal≥2500 & shopBuys≥40 |
| monk | 🧘 수도승 | 1.05 | 0 | `bonusSpins -= 1`; `quotaMul *= 0.9` | noItemS8≥2 |
| alchemist | ⚗️ 연금술사 | 1.0 | 0 | `coinMul *= 1.25`; `clearCoinBonus += 3` | richBossClears≥3 |
| daredevil | 😈 무모한도전 | 1.2 | 0 | `expMul *= 1.1`; `quotaMul *= 1.2`; **막스핀이면** `expMul *= 1.6` **else if** 남은스핀≤2면 `expMul *= 1.35`(상호배타, else-if) | allinWins≥18 & bestStage≥14 |
| prodigy | 🌟 천재 | 0.95 | 0 | `expMul *= 1.12` | distinctCharS10≥7 |

`pse(sym,v)` = perSymbolExp 가산, `pss(sym,v)` = perSymbolScore 가산, `wmul(sym,v)` = symbolWeightMul 곱, `wadd(sym,v)` = weightAdd 가산 (buildMods 내부 로컬 헬퍼, L1749–1753).

`grandfather` 규칙(L367–369): `charUnlocked = meetsReq(unlockReq, stat) || stat["cstage_"+id] > 0` — unlockReq 임계가 상향되어도 과거 플레이 경험(cstage_id>0)이 있으면 계속 해금 유지.

---

## 5. 퍼크 시스템 구조

### 5.1 데이터클래스/enum (L376–381)

```kotlin
enum class Tier { SILVER, GOLD, PRISM }
enum class PCat { AUGMENT, RELIC, CURSE }
data class Perk(
    val id: String, val emoji: String, val name: String,
    val tier: Tier, val cat: PCat, val desc: String, val price: Int = 0,
)
```
`price`는 유물(RELIC)에서만 실제 값 사용(코인 구매가), 증강/저주는 `price=0`(선택형이라 구매 아님).

### 5.2 발동 훅 분류 (효과가 적용되는 시점)

| 훅 | 함수 | 적용 필드 |
|---|---|---|
| **모드 조립(스핀 전, 스테이지/런 단위 1회)** | `buildMods()` (L1730–2026) | 머신+캐릭터+퍽+저주+세트효과 → `Mods` 누산. 반환된 `Mods`는 그 스테이지/설정이 바뀌기 전까지 재사용 |
| **스핀 직전 오버레이(1회용 아이템)** | `applyItemMods()` (L1494–1559) | NEXTSPIN/PHASE 아이템만. INSTANT 아이템은 서비스가 즉시 처리(엔진 미구현) |
| **패시브 장치 오버레이(스핀마다)** | `applyPassiveDevice()` (L1121–1127) | dev_flame/dev_seal/dev_overheat/dev_subreel만 처리, dev_safe·dev_major 등은 서비스 별도 처리 |
| **셀 조작(굴림 직후, 평가 직전)** | `applyCellOps()` (L2114–2125) | eraser_old/eraser_fine/eraser_god/wild_temp/fake_crown 아이템의 "셀 치환" 레버 |
| **스핀 평가(셀 내용 기반 조건부)** | `evaluate()` (L2131–2356) | `Mods`의 per-spin 조건부 필드(perSkullExp/skull3ScoreMul/rareBurstExpMul/rareBurstScoreMul/twoSetBonusMul/set3ExpMul/set4ScoreMul/perfectShapeExpMul, center/ends/adjacent 등) — 실제 셀 배열을 보고서야 발동 여부 결정 |
| **스테이지 클리어 시 점수 계산** | `stageClearScore()` (L88–95) | curses 카운트만 참조(perk 개별효과 아님) |
| **런 컨텍스트 조건부(스핀 전, RunCtx 참조)** | `buildMods()` 내부 `ctx: RunCtx` 분기(L1911–1931) | early_prep/early_adapt/growth_log/snowball/fortune_check/luck_accum/fate_burst/late_focus/cliff_focus/pair_match/puzzle_sense/perfect_shape/skull_watch/sacrifice/black_diploma — 신규 16종 전용, 스테이지/스핀 인덱스/스택 상태 필요 |
| **빌드도감 판정(클리어/게임오버 시)** | `evalThemeBuilds()` (L1392–1433) | 퍽 보유 목록 자체가 아니라 "이번 클리어/이벤트 컨텍스트"를 봄 |
| **해금 게이트(상시)** | `perkGate()`/`perkUnlocked()` (L825–894) | 스핀과 무관, 졸업레벨+전공연구 기준 |

### 5.3 증강(AUGMENT) 80종 — 정의(L385–480) + buildMods 실처리(L1777–1932)

`price=0`(모든 증강 공통). 표는 `Tier` · desc원문 · buildMods 실처리(정확한 필드/수치)로 구성.

| id | emoji | name | tier | desc(원문) | buildMods 처리 | 정의L / 처리L |
|---|---|---|---|---|---|---|
| study | 📚 | 기초학습 | SILVER | 모든 EXP +10% | `expMul *= 1.10` | 385 / 1777 |
| preview | 🔍 | 예습 | SILVER | 첫 스핀 EXP +25% | `firstSpinExpMul *= 1.25` | 386 / 1778 |
| review | 📖 | 복습 | SILVER | 마지막 스핀 EXP +25% | `lastSpinExpMul *= 1.25` | 387 / 1779 |
| diligence | ✍️ | 꾸준함 | SILVER | 스핀마다 EXP +3 | `flatExp += 3` | 388 / 1780 |
| cherry_up | 🍒 | 체리강화 | SILVER | 🍒체리 EXP +2 | `pse(cherry,2)` | 389 / 1781 |
| book_up | 📘 | 책강화 | SILVER | 📘책 EXP +2 | `pse(book,2)` | 390 / 1782 |
| star_up | ⭐ | 별강화 | SILVER | ⭐별 EXP +2 | `pse(star,2)` | 391 / 1783 |
| gem_polish | 💎 | 보석연마 | SILVER | 💎보석 점수 +10 | `pss(gem,10)` | 392 / 1784 |
| coin_luck | 🪙 | 동전운 | SILVER | 코인 +30% | `coinMul *= 1.3` | 393 / 1785 |
| set_sense | 🎯 | 콤보감각 | SILVER | 세트 보너스 +30% | `setExpMul *= 1.3` | 394 / 1786 |
| lucky | 🍀 | 행운부적 | SILVER | 희귀심볼 등장 +20% | `rareWeightMul *= 1.2` | 395 / 1787 |
| study_tag | 🎓 | 학구열 | SILVER | 학습태그 1개당 EXP +4 | `tag(학습,4)` | 396 / 1788 |
| cherry_farm | 🍒 | 체리농장 | GOLD | 🍒체리 EXP +4·등장↑ | `pse(cherry,4)`; `wmul(cherry,1.3)` | 398 / 1790 |
| library | 📇 | 도서관 | GOLD | 📘책 EXP +4·학습태그 +3 | `pse(book,4)`; `tag(학습,3)` | 399 / 1791 |
| gem_invest | 💎 | 보석투자 | GOLD | 💎보석 점수 +25 | `pss(gem,25)` | 400 / 1792 |
| skull_study | ☠ | 해골학 | GOLD | ☠해골이 EXP +6 | `skullExp += 6` | 401 / 1793 |
| center | 🎯 | 집중 | GOLD | 가운데 칸 EXP 2배 | `centerExpMul *= 2.0` | 402 / 1794 |
| twins | ↔️ | 양끝맞춤 | GOLD | 양끝 같은 심볼이면 EXP 2배 | `endsMatchExpMul *= 2.0` | 403 / 1795 |
| chain | 🔗 | 연쇄 | GOLD | 붙은 같은 심볼 쌍당 EXP +20 | `adjacentSameExp += 20` | 404 / 1796 |
| crown_seek | 👑 | 왕관추종 | GOLD | 👑왕관 등장 2배·점수 +30 | `wmul(crown,2.0)`; `pss(crown,30)` | 405 / 1797 |
| greed | 🤑 | 탐욕 | GOLD | 모든 EXP +25% | `expMul *= 1.25` | 406 / 1798 |
| insurance | ❤️ | 보험 | GOLD | 스테이지 스핀 +1 | `bonusSpins += 1` | 407 / 1799 |
| overdrive | ⚡ | 과부하 | PRISM | 모든 EXP +60% | `expMul *= 1.6` | 409 / 1801 |
| short_day | 🏃 | 조기퇴근 | PRISM | 스핀 -2 · 모든 EXP +120% | `bonusSpins -= 2`; `expMul *= 2.2` | 410 / 1802 |
| wild_world | 🌀 | 와일드세계 | PRISM | 🌀와일드 등장 | `wadd(wild,6.0)` | 411 / 1803 |
| seed_garden | 🌱 | 씨앗정원 | PRISM | 🌱씨앗 등장 | `wadd(seed,5.0)` | 412 / 1804 |
| jackpot | 🎰 | 잭팟기계 | PRISM | 👑왕관 대량등장·점수 +50 | `wadd(crown,3.0)`; `pss(crown,50)` | 413 / 1805 |
| all_in | 🎯 | 몰아치기 | GOLD | 스핀 -1·모든 EXP +45% | `bonusSpins -= 1`; `expMul *= 1.45` | 415 / 1807 |
| cram | ⏰ | 벼락치기 | GOLD | 첫스핀 -40%·막스핀 +120% | `firstSpinExpMul *= 0.6`; `lastSpinExpMul *= 2.2` | 416 / 1808 |
| high_roller | 💠 | 하이롤러 | GOLD | 💎보석 점수+30·EXP -8% | `pss(gem,30)`; `expMul *= 0.92` | 417 / 1809 |
| all_or_nothing | ☠ | 해골도박 | GOLD | ☠해골 EXP+10·EXP -10% | `skullExp += 10`; `expMul *= 0.9` | 418 / 1810 |
| focus_fire | 🔭 | 정조준 | GOLD | 가운데 칸 EXP 2.5배 | `centerExpMul *= 2.5` | 419 / 1811 |
| symmetry | ↔️ | 대칭미학 | GOLD | 양끝맞춤 EXP 2.2배·인접쌍+12 | `endsMatchExpMul *= 2.2`; `adjacentSameExp += 12` | 420 / 1812 |
| crammer_tag | 🎓 | 주입식 | GOLD | 학습태그당 EXP+7·책등장↑ | `tag(학습,7)`; `wmul(book,1.4)` | 421 / 1813 |
| gamblers_dice | 🎲 | 도박주사위 | PRISM | 🎲주사위 등장·EXP +15% | `wadd(dice,5.0)`; `expMul *= 1.15` | 422 / 1814 |
| key_master | 🗝 | 열쇠장인 | PRISM | 🗝열쇠 등장·코인 +25% | `wadd(key,4.0)`; `coinMul *= 1.25` | 423 / 1815 |
| glass_cannon | ⚡ | 유리대포 | PRISM | 스핀-1·EXP +90%·점수+10% | `bonusSpins -= 1`; `expMul *= 1.9`; `scoreMul *= 1.1` | 424 / 1816 |
| rich_richer | 🤑 | 부익부 | PRISM | 코인+60%·클코인+3·EXP-5% | `coinMul *= 1.6`; `clearCoinBonus += 3`; `expMul *= 0.95` | 425 / 1817 |
| endgame_rush | 🏁 | 막판스퍼트 | PRISM | 막스핀 EXP 2.4배·첫스핀 -50% | `lastSpinExpMul *= 2.4`; `firstSpinExpMul *= 0.5` | 426 / 1818 |
| deep_read | 📕 | 정독 | SILVER | 학습태그 1개당 EXP +3 | `tag(학습,3)` | 429 / 1820 |
| morning | 🌅 | 아침예습 | SILVER | 첫 스핀 EXP +30% | `firstSpinExpMul *= 1.30` | 430 / 1821 |
| evening | 🌆 | 야간자습 | SILVER | 마지막 스핀 EXP +30% | `lastSpinExpMul *= 1.30` | 431 / 1822 |
| note_take | 📝 | 필기 | SILVER | 스핀마다 EXP +5 | `flatExp += 5` | 432 / 1823 |
| star_up2 | 🌟 | 별관측 | SILVER | ⭐별 EXP +3 | `pse(star,3)` | 433 / 1824 |
| magnet_up | 🧲 | 자석강화 | SILVER | 🧲자석 EXP +3 | `pse(magnet,3)` | 434 / 1825 |
| gem_buff | 💠 | 보석세공 | SILVER | 💎보석 점수 +12 | `pss(gem,12)` | 435 / 1826 |
| combo_note | 🎯 | 콤보노트 | SILVER | 세트 보너스 +20% | `setExpMul *= 1.20` | 436 / 1827 |
| polymath | 🧠 | 박식 | GOLD | 모든 EXP +20% | `expMul *= 1.20` | 438 / 1828 |
| necromancer | 💀 | 강령술사 | GOLD | ☠해골이 EXP +8 | `skullExp += 8` | 439 / 1829 |
| bullseye | 🎯 | 정조준2 | GOLD | 가운데 칸 EXP 1.8배 | `centerExpMul *= 1.8` | 440 / 1830 |
| mirror | 🪞 | 거울대칭 | GOLD | 양끝 같은 심볼 EXP 1.9배 | `endsMatchExpMul *= 1.9` | 441 / 1831 |
| domino | ⛓️ | 도미노 | GOLD | 붙은 같은 심볼 쌍당 EXP +16 | `adjacentSameExp += 16` | 442 / 1832 |
| honor_student | 🎓 | 수재 | GOLD | 학습태그 1개당 EXP +6 | `tag(학습,6)` | 443 / 1833 |
| lapidary | 💍 | 세공장인 | GOLD | 💎보석 점수 +28 | `pss(gem,28)` | 444 / 1834 |
| royal_decree | 📜 | 왕명 | GOLD | 👑왕관 등장↑·점수 +20 | `wmul(crown,1.8)`; `pss(crown,20)` | 445 / 1835 |
| supernova | 💥 | 초신성 | PRISM | 모든 EXP +70% | `expMul *= 1.70` | 447 / 1836 |
| joker | 🃏 | 조커 | PRISM | 🌀와일드 대량 등장 | `wadd(wild,5.0)` | 448 / 1837 |
| great_harvest | 🌾 | 대수확 | PRISM | 🌱씨앗 등장·🍒체리 EXP +3 | `wadd(seed,5.0)`; `pse(cherry,3)` | 449 / 1838 |
| mega_jackpot | 🎰 | 대박기계 | PRISM | 👑왕관 대량등장·점수 +40 | `wadd(crown,3.0)`; `pss(crown,40)` | 450 / 1839 |
| time_warp | ⏳ | 시간왜곡 | PRISM | 스핀 +1·모든 EXP +20% | `bonusSpins += 1`; `expMul *= 1.20` | 451 / 1840 |
| red_safetynet | 🥅 | 붉은 안전망 | SILVER | 🍒체리 EXP +2 | `pse(cherry,2)` | 453 / 1905 |
| polish_work | ✨ | 광택 작업 | GOLD | 💎보석 점수 +25 | `pss(gem,25)` | 454 / 1906 |
| greed_calc | 🤑 | 탐욕의 계산 | GOLD | 모든 EXP +15% | `expMul *= 1.15` | 455 / 1907 |
| overheat_formula | ♨️ | 과열 공식 | GOLD | 모든 EXP +14% | `expMul *= 1.14` | 456 / 1908 |
| early_prep | 🥚 | 조기교육 | SILVER | S3 이하 EXP +15% (S6+ 무효) | `if (ctx.stage in 1..3) expMul *= 1.15` | 461 / 1911 |
| growth_log | 📈 | 성장일지 | SILVER | 클리어마다 다음스테이지 첫스핀 EXP +8%(최대5·실패리셋) | `firstSpinExpMul *= (1.0 + 0.08 × ctx.growthStack.coerceIn(0,5))` | 462 / 1913 |
| early_adapt | 🌱 | 빠른적응 | GOLD | S1~5 EXP +12% (S6+ 무효) | `if (ctx.stage in 1..5) expMul *= 1.12` | 463 / 1912 |
| snowball | ❄️ | 눈덩이 | PRISM | 남은스핀2+ 클리어시 다음 EXP +12%(최대4·보스후 -1) | `expMul *= (1.0 + 0.12 × ctx.snowStack.coerceIn(0,4))` | 464 / 1914 |
| fortune_check | 🔍 | 운세확인 | SILVER | 스테이지 첫스핀 희귀 등장 +20% | `if (ctx.isFirstSpin) rareWeightMul *= 1.2` | 466 / 1916 |
| luck_accum | 🎰 | 불운적립 | GOLD | 희귀 미등장 스핀마다 불운+1(3+면 다음 희귀↑) | `if (ctx.unluckyGauge>=3) rareWeightMul *= 1.3` | 467 / 1917 |
| fate_burst | 💫 | 운명폭발 | PRISM | 희귀 2개+ 스핀 EXP+80%·점수+50%(보스전 70%) | `rareBurstExpMul *= (ctx.boss ? 1.7 : 1.8)`; `rareBurstScoreMul *= 1.5` | 468 / 1918 |
| late_focus | ⏳ | 후반집중 | SILVER | 남은스핀 2 이하 EXP +10% | `if (ctx.spinsLeft in 1..2) expMul *= 1.10` | 470 / 1920 |
| cliff_focus | 🧗 | 벼랑끝집중 | GOLD | EXP<요구60%&마지막스핀 → 막스핀 EXP +80% | `if (ctx.isLastSpin && ctx.quota>0 && ctx.stageExp < (ctx.quota*0.6).toLong()) lastSpinExpMul *= 1.8` | 471 / 1921–1922 |
| fate_bell | 🔔 | 운명의종 | PRISM | 런 1회 부족15 이하 실패직전 자동 추가스핀+1 | **buildMods 무효과** — 서비스가 run.fateBellUsed 게이트로 처리(L1923 주석) | 472 / — |
| pair_match | 👯 | 짝맞추기 | SILVER | 같은심볼 2세트면 세트 보너스 +20% | `twoSetBonusMul *= 1.2` (evaluate에서 bestCount==2 조건 판정) | 474 / 1925 |
| puzzle_sense | 🧩 | 퍼즐감각 | GOLD | 세트3+ EXP+25%·세트4+ 점수+20% | `set3ExpMul *= 1.25`; `set4ScoreMul *= 1.20` | 475 / 1926 |
| perfect_shape | 💠 | 완벽한모양 | PRISM | 양끝 같고 가운데 같은계열 EXP+120%(와일드충족 70%) | `perfectShapeExpMul *= 2.2` (evaluate에서 와일드충족 시 고정 1.7로 대체) | 476 / 1927 |
| skull_watch | 👁️ | 해골관찰 | SILVER | ☠1개당 EXP+2·☠3+ 스핀 점수 -10% | `perSkullExp += 2`; `skull3ScoreMul *= 0.9` | 478 / 1929 |
| sacrifice | 🩸 | 희생 | GOLD | 저주1개당 EXP+6%·클리어코인 -1 | `expMul *= (1.0 + 0.06 × ctx.curseCount)`; `clearCoinBonus -= 1` | 479 / 1930 |
| black_diploma | 🎓 | 검은졸업장 | PRISM | 저주5+ EXP+60%·점수+30%·스핀 -1 | `if (ctx.curseCount>=5) { expMul *= 1.6; scoreMul *= 1.3; bonusSpins -= 1 }` | 480 / 1931 |

### 5.4 유물(RELIC) 61종 — 정의(L484–547) + buildMods 실처리(같은 perkIds 루프, L1842–1908)

| id | emoji | name | tier | price | desc(원문) | buildMods 처리 | 정의L / 처리L |
|---|---|---|---|---|---|---|---|
| old_book | 📘 | 낡은교과서 | SILVER | 12 | 📘책 EXP +3 | `pse(book,3)` | 484 / 1842 |
| cherry_candy | 🍬 | 체리사탕 | SILVER | 10 | 🍒체리 EXP +2 | `pse(cherry,2)` | 485 / 1843 |
| rusty_coin | 🪙 | 녹슨동전 | SILVER | 12 | 코인 +20% | `coinMul *= 1.2` | 486 / 1844 |
| pencil | ✏️ | 연필깎이 | SILVER | 12 | 첫 스핀 EXP +15% | `firstSpinExpMul *= 1.15` | 487 / 1845 |
| coffee | ☕ | 커피잔 | SILVER | 14 | 마지막 스핀 EXP +15% | `lastSpinExpMul *= 1.15` | 488 / 1846 |
| magnifier | 🔎 | 돋보기 | SILVER | 16 | 희귀심볼 등장 +15% | `rareWeightMul *= 1.15` | 489 / 1847 |
| star_sticker | ⭐ | 별스티커 | SILVER | 12 | ⭐별 점수 +8 | `pss(star,8)` | 490 / 1848 |
| black_candle | 🕯️ | 검은촛불 | GOLD | 18 | ☠해골이 EXP +4 | `skullExp += 4` | 491 / 1849 |
| gem_cert | 📜 | 보석감정서 | GOLD | 20 | 💎보석 점수 +15 | `pss(gem,15)` | 492 / 1850 |
| clover | 🍀 | 네잎클로버 | GOLD | 16 | 모든 EXP +8% | `expMul *= 1.08` | 493 / 1851 |
| set_charm | 🎰 | 세트부적 | GOLD | 18 | 세트 보너스 +25% | `setExpMul *= 1.25` | 494 / 1852 |
| wide_lens | 🔭 | 집중경 | GOLD | 16 | 가운데 칸 EXP +50% | `centerExpMul *= 1.5` | 495 / 1853 |
| eraser | ✏️ | 지우개 | SILVER | 10 | 📘책 EXP +2 | `pse(book,2)` | 497 / 1855 |
| ruler | 📏 | 자 | SILVER | 12 | 첫 스핀 EXP +12% | `firstSpinExpMul *= 1.12` | 498 / 1856 |
| desk_lamp | 🪔 | 스탠드 | SILVER | 12 | 마지막 스핀 EXP +12% | `lastSpinExpMul *= 1.12` | 499 / 1857 |
| cherry_jam | 🍓 | 체리잼 | SILVER | 12 | 🍒체리 EXP +3 | `pse(cherry,3)` | 500 / 1858 |
| bookmark | 🔖 | 책갈피 | SILVER | 12 | 학습태그 1개당 EXP +3 | `tag(학습,3)` | 501 / 1859 |
| coin_pouch | 👛 | 동전지갑 | SILVER | 12 | 코인 +20% | `coinMul *= 1.2` | 502 / 1860 |
| mini_scope | 🔬 | 미니스코프 | SILVER | 14 | 희귀심볼 등장 +15% | `rareWeightMul *= 1.15` | 503 / 1861 |
| gem_dust | ✨ | 보석가루 | SILVER | 12 | 💎보석 점수 +10 | `pss(gem,10)` | 504 / 1862 |
| magnet_chip | 🧲 | 자석칩 | SILVER | 10 | 🧲자석 EXP +2 | `pse(magnet,2)` | 505 / 1863 |
| star_chart | 🌠 | 별자리표 | SILVER | 12 | ⭐별 EXP +2 | `pse(star,2)` | 506 / 1864 |
| paperclip | 📎 | 클립 | SILVER | 12 | 세트 보너스 +15% | `setExpMul *= 1.15` | 507 / 1865 |
| small_candle | 🕯️ | 작은초 | SILVER | 12 | ☠해골이 EXP +3 | `skullExp += 3` | 508 / 1866 |
| thick_tome | 📕 | 두꺼운책 | GOLD | 18 | 📘책 EXP +4 | `pse(book,4)` | 510 / 1867 |
| crystal_ball | 🔮 | 수정구 | GOLD | 20 | 희귀심볼 등장 +30% | `rareWeightMul *= 1.3` | 511 / 1868 |
| skull_idol | 💀 | 해골우상 | GOLD | 18 | ☠해골이 EXP +6 | `skullExp += 6` | 512 / 1869 |
| gem_tiara | 💎 | 보석티아라 | GOLD | 22 | 💎보석 점수 +20 | `pss(gem,20)` | 513 / 1870 |
| focus_ring | 💍 | 집중반지 | GOLD | 18 | 가운데 칸 EXP +60% | `centerExpMul *= 1.6` | 514 / 1871 |
| silver_mirror | 🪞 | 은거울 | GOLD | 18 | 양끝 같은 심볼 EXP +70% | `endsMatchExpMul *= 1.7` | 515 / 1872 |
| iron_chain | ⛓️ | 쇠사슬 | GOLD | 18 | 붙은 같은 심볼 쌍당 EXP +14 | `adjacentSameExp += 14` | 516 / 1873 |
| diploma_relic | 🎓 | 졸업장식 | GOLD | 18 | 학습태그 1개당 EXP +5 | `tag(학습,5)` | 517 / 1874 |
| four_clover | 🍀 | 네잎클로버2 | GOLD | 20 | 모든 EXP +10% | `expMul *= 1.10` | 518 / 1875 |
| combo_trophy | 🏆 | 콤보트로피 | GOLD | 20 | 세트 보너스 +25% | `setExpMul *= 1.25` | 519 / 1876 |
| crown_jewel | 👑 | 왕관보석 | GOLD | 22 | 👑왕관 점수 +30 | `pss(crown,30)` | 520 / 1877 |
| piggy_bank | 🐷 | 돼지저금통 | GOLD | 18 | 코인 +40%·클리어코인 +2 | `coinMul *= 1.4`; `clearCoinBonus += 2` | 521 / 1878 |
| spare_token | 🎟️ | 여분토큰 | GOLD | 30 | 스테이지 스핀 +1 | `bonusSpins += 1` | 522 / 1879 |
| hourglass_r | ⏳ | 모래시계 | GOLD | 22 | 첫·마지막 스핀 EXP +20% | `firstSpinExpMul *= 1.2`; `lastSpinExpMul *= 1.2` | 523 / 1880 |
| battery | 🔋 | 배터리 | GOLD | 18 | 스핀마다 EXP +6 | `flatExp += 6` | 524 / 1881 |
| charm_relic | 🧿 | 부적 | GOLD | 20 | 모든 EXP +12% | `expMul *= 1.12` | 525 / 1882 |
| cherry_press | 🧃 | 체리 압축기 | SILVER | 10 | 🍒체리 EXP +2 | `pse(cherry,2)` | 527 / 1884 |
| cherry_can | 🥫 | 체리 통조림 | SILVER | 12 | 🍒체리 EXP +3 | `pse(cherry,3)` | 528 / 1885 |
| auto_pen | 🖋️ | 자동 필기 펜 | SILVER | 10 | 📘책 EXP +2 | `pse(book,2)` | 529 / 1886 |
| library_card | 🪪 | 도서관 카드 | GOLD | 18 | 📘책 EXP +3·학습태그 1개당 EXP +3 | `pse(book,3)`; `tag(학습,3)` | 530 / 1887 |
| greed_goblet | 🏆 | 탐욕의 잔 | GOLD | 18 | 모든 EXP +10% | `expMul *= 1.10` | 531 / 1888 |
| ominous_skull | 💀 | 불길한 해골 목걸이 | GOLD | 18 | ☠해골이 EXP +5 | `skullExp += 5` | 532 / 1889 |
| black_report | 📋 | 검은 성적표 | GOLD | 18 | ☠해골이 EXP +4 | `skullExp += 4` | 533 / 1890 |
| bloody_coupon | 🩸 | 피 묻은 쿠폰북 | GOLD | 18 | ☠해골이 EXP +4·코인 +20% | `skullExp += 4`; `coinMul *= 1.2` | 534 / 1891 |
| crown_stand | 🏛️ | 왕관 받침대 | GOLD | 20 | 👑왕관 점수 +25 | `pss(crown,25)` | 535 / 1892 |
| broken_crown | 👑 | 깨진 왕관 | SILVER | 16 | 👑왕관 점수 +15 | `pss(crown,15)` | 536 / 1893 |
| kings_ledger | 📜 | 왕의 족보 | GOLD | 22 | 👑왕관 점수 +20·등장↑ | `pss(crown,20)`; `wmul(crown,1.5)` | 537 / 1894 |
| flame_canister | 🛢️ | 불꽃 저장통 | GOLD | 16 | 모든 EXP +8% | `expMul *= 1.08` | 538 / 1895 |
| hot_handle | 🔥 | 뜨거운 슬롯핸들 | GOLD | 18 | 모든 EXP +9% | `expMul *= 1.09` | 539 / 1896 |
| fate_handle | 🎰 | 운명의 손잡이 | GOLD | 18 | 희귀심볼 등장 +25% | `rareWeightMul *= 1.25` | 540 / 1897 |
| gamblers_eye | 👁️ | 도박사의 눈 | GOLD | 18 | 희귀심볼 등장 +20% | `rareWeightMul *= 1.20` | 541 / 1898 |
| old_wallet | 👛 | 낡은 지갑 | SILVER | 12 | 코인 +20% | `coinMul *= 1.2` | 542 / 1899 |
| crumpled_coupon | 🧾 | 구겨진 쿠폰 | SILVER | 10 | 코인 +20% | `coinMul *= 1.2` | 543 / 1900 |
| cursed_wallet | 💰 | 저주받은 지갑 | GOLD | 18 | 코인 +30%·☠해골 EXP +2 | `coinMul *= 1.3`; `skullExp += 2` | 544 / 1901 |
| practice_pad | 📓 | 연습장 | SILVER | 10 | 📘책 EXP +2 | `pse(book,2)` | 545 / 1902 |
| calculator | 🧮 | 작은 계산기 | SILVER | 12 | 💎보석 점수 +12 | `pss(gem,12)` | 546 / 1903 |
| lucky_eraser | 🩹 | 행운의 지우개 | SILVER | 14 | 희귀심볼 등장 +15% | `rareWeightMul *= 1.15` | 547 / 1904 |

### 5.5 저주(CURSE) 16종 (L552–568, buildMods L1936–1951)

전부 `Tier.GOLD` 고정, `price=0`. 단점+장점 동시 부여.

| id | emoji | name | desc(원문) | buildMods 처리 | 정의L / 처리L |
|---|---|---|---|---|---|
| hard_exam | 📝 | 어려운시험 | 요구치+10% / 클리어점수+20% | `quotaMul *= 1.10`; `scoreMul *= 1.20` | 552 / 1936 |
| cursed_skulls | ☠ | 저주받은패 | 해골↑·EXP-4 / 해골 EXP+8 | `wadd(skull,4.0)`; `flatExp -= 4`; `skullExp += 8` | 553 / 1937 |
| speed_test | ⏱️ | 속성평가 | 스핀-1 / 요구치-22% | `bonusSpins -= 1`; `quotaMul *= 0.78` | 554 / 1938 |
| frugal_vow | 🪙 | 청빈서약 | 코인-40% / 요구치-12% | `coinMul *= 0.6`; `quotaMul *= 0.88` | 555 / 1939 |
| tunnel_vision | 🎯 | 외골수 | 양끝·첫스핀↓ / 가운데 2배 | `endsMatchExpMul *= 0.5`; `firstSpinExpMul *= 0.85`; `centerExpMul *= 2.0` | 556 / 1940 |
| late_bloomer | 🌙 | 늦깎이 | 첫스핀-50% / 막스핀+80% | `firstSpinExpMul *= 0.5`; `lastSpinExpMul *= 1.8` | 557 / 1941 |
| gem_obsession | 💎 | 보석집착 | 체리·책↓ / 보석 점수+35 | `pse(cherry,-2)`; `pse(book,-2)`; `pss(gem,35)`; `scoreMul *= 1.10` | 558 / 1942 |
| high_stakes | 🎲 | 한탕주의 | 요구치+8% / 희귀등장+50% | `quotaMul *= 1.08`; `rareWeightMul *= 1.5` | 559 / 1943 |
| thorny_path | 🌵 | 가시밭길 | 해골↑·EXP↓ / 클리어 코인+ | `wadd(skull,3.0)`; `skullExp -= 5`; `tag(저주,6)`; `clearCoinBonus += 4` | 560 / 1944 |
| hex_allornothing | ⚡ | 일발역전 | 세트-50% / 양끝맞춤 2배 | `setExpMul *= 0.5`; `endsMatchExpMul *= 2.0` | 561 / 1945 |
| sleep_debt | 😴 | 수면부족 | 스핀당 EXP-5 / 세트+40% | `flatExp -= 5`; `setExpMul *= 1.40` | 562 / 1946 |
| diploma_pressure | 🎓 | 학위압박 | 요구치+12% / 학습·책 강화 | `quotaMul *= 1.12`; `tag(학습,5)`; `pse(book,2)` | 563 / 1947 |
| exam_week | 📅 | 시험기간 | 요구치+12% / 클리어점수+25% | `quotaMul *= 1.12`; `scoreMul *= 1.25` | 565 / 1948 |
| blackout | 🌑 | 정전 | 해골↑·해골 EXP+6 / 희귀등장+30% | `wadd(skull,4.0)`; `skullExp += 6`; `rareWeightMul *= 1.3` | 566 / 1949 |
| pop_quiz | ❓ | 쪽지시험 | 스핀-1 / 희귀등장+40% | `bonusSpins -= 1`; `rareWeightMul *= 1.4` | 567 / 1950 |
| student_debt | 💸 | 학자금 | 코인-50% / 스핀마다 EXP+6 | `coinMul *= 0.5`; `flatExp += 6` | 568 / 1951 |

**저주 스택 임계 보너스**(buildMods L2001–2004, curseIds 개수 기준·개별 저주와 별개로 누적):
```kotlin
val nCurses = curseIds.size
if (nCurses >= 3) skullExp += 2       // 저주 3개+: 해골 EXP+2
if (nCurses >= 5) scoreMul *= 1.12    // 저주 5개+: 점수 ×1.12
if (nCurses >= 7) scoreMul *= 1.12    // 저주 7개+: 추가로 한 번 더 ×1.12 (5개 조건과 중첩 적용 — 7개면 scoreMul에 1.12×1.12 반영)
```

---

## 6. 세트 효과 (SETS, 33종, L572–611) + 시너지 로직

```kotlin
data class SetEffect(
    val id: String, val name: String, val requires: List<String>, val desc: String,
    val reqChar: String = "", val reqMachine: String = "", val reqDevice: String = "",
)
```
발동 조건(`activeSets()`, L612–618 / buildMods 내 세트 루프 L1956–1997): `perkIds`가 `requires`를 **전부(containsAll)** 보유 **AND** `reqChar/reqMachine/reqDevice`가 비어있지 않으면 해당 값과 정확히 일치해야 함.

| id | name | requires | 발동효과(buildMods) | reqChar | reqMachine | reqDevice | L(정의/처리) |
|---|---|---|---|---|---|---|---|
| set_orchard | 체리 과수원 | cherry_up, cherry_farm | `pse(cherry,3)`; `wmul(cherry,1.25)` | - | - | - | 577 / 1963 |
| set_library | 도서관 회원증 | book_up, library, study_tag | `pse(book,3)`; `tag(학습,3)` | - | - | - | 578 / 1964 |
| set_necro | 강령술 | skull_study, black_candle | `skullExp += 4` | - | - | - | 579 / 1965 |
| set_appraiser | 감정사 | gem_polish, gem_invest, gem_cert | `pss(gem,20)` | - | - | - | 580 / 1966 |
| set_royal | 왕실 알현 | crown_seek, jackpot | `pss(crown,40)`; `wadd(crown,2.0)` | - | - | - | 581 / 1967 |
| set_align | 정렬의 묘 | center, twins, chain | `adjacentSameExp += 10` | - | - | - | 582 / 1968 |
| set_combo | 콤보 마스터 | set_sense, set_charm | `setExpMul *= 1.2` | - | - | - | 583 / 1969 |
| set_diurnal | 주야겸행 | morning, evening | `firstSpinExpMul *= 1.15`; `lastSpinExpMul *= 1.15` | - | - | - | 584 / 1970 |
| set_necro2 | 사령술 비전 | necromancer, skull_idol | `skullExp += 5` | - | - | - | 585 / 1971 |
| set_jewels | 보석 왕가 | gem_buff, lapidary, gem_tiara | `pss(gem,20)` | - | - | - | 586 / 1972 |
| set_combo2 | 콤보 장인 | combo_note, combo_trophy | `setExpMul *= 1.20` | - | - | - | 587 / 1973 |
| set_royal2 | 대관식 | royal_decree, crown_jewel | `pss(crown,30)`; `wadd(crown,2.0)` | - | - | - | 588 / 1974 |
| set_cherry_net | 체리 안전망 | cherry_up, cherry_jam | `pse(cherry,2)`; `pss(cherry,12)` | farmer | - | - | 590 / 1976 |
| set_red_harvest | 붉은 수확 | cherry_farm, great_harvest | `pse(cherry,3)`; `wmul(cherry,1.25)` | - | cherry | - | 591 / 1977 |
| set_student | 모범생 | study, diligence, note_take | `flatExp += 4` | - | - | - | 592 / 1978 |
| set_lib_bless | 도서관의 축복 | book_up, library, thick_tome | `pse(book,4)`; `tag(학습,3)` | - | library | - | 593 / 1979 |
| set_greed | 탐욕 | greed, rich_richer | `scoreMul *= 1.12`; `coinMul *= 1.10` | - | - | - | 594 / 1980 |
| set_glory_grad | 빛나는 졸업식 | diploma_relic, honor_student | `tag(학습,4)`; `lastSpinExpMul *= 1.15` | honor | - | - | 595 / 1981 |
| set_skull_lab | 해골 연구 | skull_study, skull_idol | `skullExp += 6` | cultist | - | - | 596 / 1982 |
| set_black_grad | 검은 졸업 | necromancer, black_candle, skull_idol | `skullExp += 5`; `scoreMul *= 1.12` | - | skull | - | 597 / 1983 |
| set_curse_cycle | 저주 순환 | set_charm | `setExpMul *= 1.30` | - | - | dev_seal | 598 / 1984 |
| set_crown_rite | 왕관 의식 | crown_seek, crown_jewel | `pss(crown,40)`; `wadd(crown,2.0)` | crowncol | - | - | 599 / 1985 |
| set_kings_order | 왕의 명령 | royal_decree, jackpot | `pss(crown,50)`; `wadd(crown,2.0)` | - | crown | - | 600 / 1986 |
| set_flame_lab | 불꽃 실험 | all_or_nothing | `pse(flame,5)`; `scoreMul *= 1.12` | - | flame | dev_flame | 601 / 1987 |
| set_last_ignite | 마지막 점화 | review, endgame_rush | `lastSpinExpMul *= 1.25`; `scoreMul *= 1.10` | - | - | - | 602 / 1988 |
| set_mechanic | 정비공 | set_sense | `setExpMul *= 1.25` | - | - | dev_subreel | 603 / 1989 |
| set_battery | 배터리 | battery, diligence | `flatExp += 6` | - | - | - | 604 / 1990 |
| set_gambler | 도박사 | high_stakes, high_roller | `rareWeightMul *= 1.3`; `pss(gem,25)` | gambler | - | - | 605 / 1991 |
| set_shop_reg | 상점 단골 | coin_luck, piggy_bank | `coinMul *= 1.20`; `clearCoinBonus += 3` | - | - | - | 606 / 1992 |
| set_scholarship | 장학금 | study_tag, diploma_relic | `tag(학습,4)`; `clearCoinBonus += 2` | scholar | - | - | 607 / 1993 |
| set_bomb_calc | 폭탄마 | center, focus_fire | `centerExpMul *= 1.5`; `scoreMul *= 1.10` | - | bomb | - | 608 / 1994 |
| set_perfect_calc | 완벽한 계산 | center, twins, chain | `adjacentSameExp += 14`; `centerExpMul *= 1.3` | - | - | - | 609 / 1995 |
| set_safe_grad | 안전 졸업 | insurance, clover | `flatExp += 3`; `scoreMul *= 1.08` | - | - | - | 610 / 1996 |

주의: `set_align`과 `set_perfect_calc`은 **requires가 완전히 동일**(center, twins, chain)하지만 reqChar/reqMachine/reqDevice가 전부 공란이라 **둘 다 동시에 발동**(중복 미배제) — `adjacentSameExp`가 두 세트에서 각각 +10, +14로 이중 가산되고 `centerExpMul`도 set_perfect_calc에서 추가로 ×1.3 곱해짐. 의도된 중첩인지 버그인지 원본 주석에 언급 없음 — §11 특이사항 참조.

### 세트 시너지 유도 (L620–670, UI 힌트 전용 — 실발동 아님)

```kotlin
fun setSynergyName(perkId: String, held: Set<String>): String?
```
- `perkId`가 `held`에 이미 있으면 `null`.
- SETS를 순회하며 `perkId`가 `requires`에 속하고, 그 세트의 **다른** requires 중 1개 이상이 이미 `held`에 있으면 "진행 중" 후보.
- 후보 중 `remain`(perkId를 고른 뒤 남은 미보유 requires 수)이 최소인 세트를 채택.
- `remain==0`이면 `"<세트명> 완성"`, 아니면 `"<세트명> 시너지"` 문자열 반환. 후보 없으면 `null`.
- **reqChar/reqMachine/reqDevice는 무시**(perk 보유만으로 판정 불가하므로) — UI 힌트 전용, 실제 발동 여부는 `activeSets()`가 별도 확정.

```kotlin
fun setSynergyAug(held: Set<String>, exclude: Set<String>, rng: Random, cat: PCat = PCat.AUGMENT): Perk?
```
- `requires` 중 1개 이상 보유 & 미완성인 세트들을, "남은 미보유 requires 수" 오름차순(근접 세트 우선)으로 정렬.
- 각 세트마다 미보유 & exclude 제외 & `cat==AUGMENT`인 requires 후보에서 `randomOrNull(rng)` 1개 반환. 못 찾으면 다음 세트로. 전부 실패 시 `null`.

---

## 7. 아이템 73종 (L917–1002, IKind: NEXTSPIN/PHASE/INSTANT)

```kotlin
enum class IKind { NEXTSPIN, PHASE, INSTANT }
data class Item(val id: String, val emoji: String, val name: String, val kind: IKind, val coinCost: Int, val desc: String)
```

### 7.1 NEXTSPIN — 다음 1스핀만 (applyItemMods 레버, L1501–1552 발췌)

| id | emoji/name | coinCost | desc(원문) | applyItemMods 처리 |
|---|---|---|---|---|
| energy_drink | 🥤 에너지드링크 | 18 | 다음 스핀 EXP 2배 | `expMul *= 2.0` |
| magnify | 🔎 확대경 | 15 | 다음 스핀 희귀심볼 4배 | `rareWeightMul *= 4.0` |
| loaded_dice | 🎲 조작주사위 | 22 | 다음 스핀 👑왕관 주입·점수 2배 | `weightAdd[crown] += 5.0`; `scoreMul *= 2.0` |
| ward_charm | 🧿 액막이부적 | 10 | 다음 스핀 ☠해골 미등장 | `weightMul[skull] = (기존값) × 0.0` |
| adrenaline | 💉 아드레날린 | 30 | 다음 스핀 EXP 3배 | `expMul *= 3.0` |
| rare_scope | 🔭 정밀스코프 | 18 | 다음 스핀 희귀심볼 3배 | `rareWeightMul *= 3.0` |
| crown_inject | 👑 왕관주입 | 24 | 다음 스핀 👑왕관 대량 주입 | `weightAdd[crown] += 8.0` |
| wild_inject | 🌀 와일드주입 | 22 | 다음 스핀 🌀와일드 주입 | `weightAdd[wild] += 6.0` |
| cherry_juice | 🧃 체리주스 | 5 | 다음 스핀 🍒체리 확률 ↑ | `weightMul[cherry] *= 2.5` |
| bookmark2 | 🔖 책갈피 | 5 | 다음 스핀 📘책 확률 ↑ | `weightMul[book] *= 2.5` |
| sparkle_dust | ✨ 반짝이가루 | 6 | 다음 스핀 💎보석 확률 ↑ | `weightMul[gem] *= 2.5` |
| gold_chalk | 🖍️ 황금분필 | 13 | 이번 스핀 EXP ×2 | `expMul *= 2.0` |
| focus_candy | 🍬 집중사탕 | 5 | 다음 스핀 EXP +15% | `expMul *= 1.15` |
| small_snack | 🍪 작은간식 | 4 | 다음 스핀 ☠해골 미등장 | `weightMul[skull] *= 0.0`(seal_tape·skull_shield와 동일 case) |
| cherry_basket | 🧺 체리바구니 | 7 | 다음 스핀 🍒체리 대량 등장 | `weightAdd[cherry] += 6.0` |
| gem_loupe | 💎 감정확대경 | 10 | 다음 스핀 💎보석 확률↑·점수 2배 | `weightMul[gem] *= 2.0`; `scoreMul *= 2.0` |
| seal_tape | 🩹 봉인테이프 | 9 | 다음 스핀 ☠해골 미등장 | `weightMul[skull] *= 0.0` |
| skull_sticker | 💯 해골스티커 | 12 | 다음 스핀 ☠해골 1개당 점수 +100(무페널티) | `skullScoreBonus += 100`(×해골수는 evaluate가 처리) |
| eraser_old | 🧽 낡은지우개 | 8 | 다음 스핀 가장 낮은 칸 1개 제거 | `applyCellOps`: 최저가치 1칸 → EMPTY |
| eraser_fine | 🧼 고급지우개 | 12 | 다음 스핀 가장 낮은 칸 1개 제거(정밀) | `applyCellOps`: eraser_old와 동일 case |
| eraser_god | ✨ 신의지우개 | 20 | 다음 스핀 낮은 칸 최대 2개 제거 | `applyCellOps`: 최저가치 칸 제거를 2회 반복 |
| wild_temp | 🌀 임시와일드 | 16 | 다음 스핀 랜덤 1칸 → 🌀와일드 | `applyCellOps`: `cells[rng.nextInt(cells.size)] = wild` |
| fake_crown | 👑 가짜왕관 | 24 | 다음 스핀 가장 높은 칸 → 👑왕관 | `applyCellOps`: 최고가치 1칸 → crown |

`cellValue(c) = c.sym.exp + c.sym.score`(L2112)로 "가장 낮은/높은 칸"을 판정.

### 7.2 PHASE — 이번 스테이지 내내 (applyItemMods, flatExp/배수 지속)

| id | emoji/name | coinCost | desc(원문) | 처리 |
|---|---|---|---|---|
| espresso | ☕ 에스프레소 | 20 | 이번 스테이지 스핀마다 EXP +15 | `flatExp += 15` |
| study_streak | ✍️ 집중모드 | 12 | 이번 스테이지 스핀마다 EXP +6 | `flatExp += 6` |
| rare_lure | 🍀 행운미끼 | 16 | 이번 스테이지 희귀심볼 2배 | `rareWeightMul *= 2.0` |
| coin_magnet | 🧲 코인자석 | 14 | 이번 스테이지 코인 2배·클리어코인+8 | `coinMul *= 2.0`; `clearCoinBonus += 8` |
| dbl_nothing | 🎰 올인학습 | 12 | 이번 스테이지 스핀마다 EXP+30·요구치+20% | `flatExp += 30`; `quotaMul *= 1.2` |
| last_minute | ⏰ 막판스퍼트 | 18 | 이번 스테이지 마지막 스핀 EXP 2배 | `lastSpinExpMul *= 2.0` |
| tutor | 👨‍🏫 과외 | 18 | 이번 스테이지 스핀마다 EXP +10 | `flatExp += 10` |
| fortune_incense | 🍀 행운향 | 16 | 이번 스테이지 희귀심볼 1.6배 | `rareWeightMul *= 1.6` |
| coin_press | 🪙 주화압인 | 16 | 이번 스테이지 코인 3배 | `coinMul *= 3.0` |
| overtime | ⏰ 야근 | 16 | 이번 스테이지 마지막 스핀 EXP 2배 | `lastSpinExpMul *= 2.0` |
| cram_note | 📓 벼락치기노트 | 14 | 이번 스테이지 마지막 스핀 EXP ×2 | `lastSpinExpMul *= 2.0` |
| rich_lure | 🍀 큰행운미끼 | 16 | 이번 스테이지 희귀심볼 3배 | `rareWeightMul *= 3.0` |
| prof_bribe | 🧧 교수매수봉투 | 24 | 이번 스테이지 요구치 -15% | `quotaMul *= 0.85` |
| sugar_powder | 🍚 설탕가루 | 12 | 이번 스테이지 🍒체리 1.6배·EXP+8 | `weightMul[cherry] *= 1.6`; `flatExp += 8` |
| cherry_cracker | 🧨 체리폭죽 | 14 | 이번 스테이지 🍒체리 2배·점수+20% | `weightMul[cherry] *= 2.0`; `scoreMul *= 1.2` |
| book_copy | 📄 족보사본 | 12 | 이번 스테이지 📘책 2배·EXP+8 | `weightMul[book] *= 2.0`; `flatExp += 8` |
| allnight_note | 🌙 밤샘노트 | 16 | 이번 스테이지 📘책 1.8배·EXP+12 | `weightMul[book] *= 1.8`; `flatExp += 12` |
| summary_note | 🗒️ 요약노트 | 13 | 이번 스테이지 스핀마다 EXP+9 | `flatExp += 9` |
| gem_pouch | 👝 보석주머니 | 16 | 이번 스테이지 💎보석 2배·점수+25% | `weightMul[gem] *= 2.0`; `scoreMul *= 1.25` |
| greed_lens | 🔍 탐욕의렌즈 | 18 | 이번 스테이지 점수 1.5배 | `scoreMul *= 1.5` |
| black_candle_i | 🕯️ 검은양초 | 14 | 이번 스테이지 ☠해골 2배·EXP 1.3배 | `weightMul[skull] *= 2.0`; `expMul *= 1.3` |
| curse_amp | 🩸 저주증폭제 | 16 | 이번 스테이지 ☠해골 1.6배·점수 1.4배 | `weightMul[skull] *= 1.6`; `scoreMul *= 1.4` |
| gold_chalk_box | ✏️ 황금분필세트 | 20 | 이번 스테이지 EXP 1.5배 | `expMul *= 1.5` |
| skull_shield | 🛡️ 해골방패 | 14 | 이번 스테이지 ☠해골 미등장 | `weightMul[skull] *= 0.0` |
| combo_mega | 📢 콤보확성기 | 16 | 이번 스테이지 마지막 스핀 EXP 2배·점수 1.2배 | `lastSpinExpMul *= 2.0`; `scoreMul *= 1.2` |
| cram_note_x2 | 📔 벼락치기노트+ | 16 | 이번 스테이지 마지막 스핀 EXP 2배 | `lastSpinExpMul *= 2.0` |
| overload_potion | 🧪 폭주물약 | 20 | 이번 스테이지 EXP 2배·요구치+20% | `expMul *= 2.0`; `quotaMul *= 1.2` |

### 7.3 INSTANT — 즉발 (엔진 미구현 — 서비스 처리, L1006–1008 comment)

| id | emoji/name | coinCost | desc(원문) | INSTANT_CLEAR 여부 |
|---|---|---|---|---|
| first_aid | 🩹 응급처치 | 30 | 이번 스테이지 스핀 +1 | 아니오 |
| cram | 📚 벼락치기 | 12 | 즉시 게이지 +요구치 15% | 아니오 |
| answer_sheet | 📝 족보 | 40 | 즉시 게이지 +요구치 50% | **예** |
| grad_cert | 🎓 졸업장 | 100 | 즉시 게이지 +요구치 100% (돌파) | **예** |
| double_aid | 🚑 특급처치 | 55 | 이번 스테이지 스핀 +2 | 아니오 |
| cheat_sheet | 📋 커닝페이퍼 | 20 | 즉시 게이지 +요구치 30% | 아니오 |
| honor_roll | 🏅 우등생증 | 60 | 즉시 게이지 +요구치 70% | **예** |
| dev_battery | 🔋 배터리부스트 | 8 | 다음 스핀 EXP +30% | 아니오 |
| score_sticker | 💯 점수스티커 | 5 | 사용 즉시 점수 +150 | 아니오 |
| old_coin | 🪙 낡은동전 | 4 | 즉시 코인 +6 | 아니오 |
| grad_copy | 🎓 졸업장복사본 | 70 | 즉시 게이지 +요구치 80%·점수 -10% | **예** |
| score_calc | 🧮 점수계산기 | 22 | 즉시 현재 점수 +30% | 아니오 |
| mini_coupon | 🎟️ 미니쿠폰 | 5 | 즉시 코인 +9 | 아니오 |
| price_hack | 🏷️ 가격표조작기 | 12 | 즉시 코인 +18 | 아니오 |
| grad_ring | 💍 졸업반지 | 50 | 부족 EXP ≤20이면 즉시 클리어 | **예** |
| gold_grad_bell | 🔔 황금졸업벨 | 90 | 부족 EXP ≤50이면 즉시 클리어 | **예** |
| insurance_cert | 📋 보험증서 | 45 | 이번 스테이지 실패 시 1회 생존(스핀+2) | 아니오 |
| debt_note | 🧾 빚문서 | 0 | 코인 +30 / 이후 4스테이지 클리어보상 0 | 아니오 |
| retake_form | 📄 재시험신청서 | 28 | 직전 스핀 전체 다시 굴림 | 아니오 |
| black_lottery | 🎫 검은복권 | 18 | 50% 골드유물 / 50% 저주 1개 | 아니오 |
| devil_contract | 😈 악마의계약서 | 20 | 유물 1개 + 저주 1개(코인+25) | 아니오 |
| timeline_ticket | 🎟️ 세계선티켓 | 26 | 다음 스핀 2번 굴려 유리한 쪽 자동확정 | 아니오 |
| broken_prism | 🔮 깨진프리즘 | 22 | 이번 스테이지 랜덤 프리즘 증강효과 1개 | 아니오 |

`INSTANT_CLEAR_ITEMS = {answer_sheet, grad_cert, grad_copy, honor_roll, grad_ring, gold_grad_bell}`(L1009–1011) — 서비스가 스테이지당 1회("ICLEAR" 커맨드)로 캡. INSTANT 카테고리 전체는 `applyItemMods`에 case가 없다(NEXTSPIN/PHASE만 처리, L1494 주석 "INSTANT는 서비스서 즉시 처리"). 즉, 위 표의 수치·확률(예: black_lottery 50/50, devil_contract 유물+저주 지급 등)은 **SlotV2Engine.kt 밖의 서비스 코드에 구현**되어 있어야 하며 본 파일에는 존재하지 않는다.

`pickItems(rng, n=3) = ITEMS.shuffled(rng).take(n)`(L1004) — 73종 전체를 셔플 후 상위 n개.

---

## 8. 장치 16종 (DEVICES, L1022–1064)

```kotlin
enum class DevKind { PASSIVE, ARMED, MANIP, PEEK, INSTANT }
data class Device(
    val id: String, val emoji: String, val name: String, val cmd: String, val desc: String,
    val kind: DevKind = DevKind.ARMED, val needsArg: Boolean = false,
    val cooldown: Int = 0, val rare: Boolean = false,
    val unlockAch: String = "",
)
```

| id | emoji/name | cmd | kind | needsArg | rare | cooldown | desc(원문) | unlockAch | 정의L |
|---|---|---|---|---|---|---|---|---|---|
| dev_flame | 🔥 불꽃엔진 | (없음) | PASSIVE | false | **true** | 0 | 장착 시 모든 스핀 EXP +15% | lic_flame | 1030 |
| dev_seal | 🔒 봉인장막 | (없음) | PASSIVE | false | false | 0 | 장착 시 모든 스핀 ☠해골 미등장 · EXP +5% | lic_seal | 1032 |
| dev_safe | 🦺 안전벨트 | (없음) | PASSIVE | false | false | 0 | 장착 시 모든 스핀 최소 EXP 보장(폭망 방지) | lic_safe | 1034 |
| dev_overheat | ♨️ 과열코어 | (없음) | PASSIVE | false | **true** | 0 | 장착 시 모든 스핀 EXP +18%·☠해골 +1 등장(고위험) | lic_overheat | 1036 |
| dev_subreel | ➕ 보조릴 | (없음) | PASSIVE | false | **true** | 0 | 장착 시 항상 6칸 슬롯 · 최종 EXP -30% | lic_subreel | 1038 |
| dev_coin | 🪙 코인투입구 | 투입 | ARMED | false | false | 0 | 코인5 소모 → 다음 스핀 EXP +30% | lic_coin | 1041 |
| dev_reroll | 🔄 재굴림기 | 재굴림 | MANIP | false | false | 0 | 직전 스핀 결과 다시 굴림 (EXP -10% · 3🪙) | lic_reroll | 1043 |
| dev_pin | 📌 고정핀 | 고정 | MANIP | **true** | false | 0 | 직전 결과 N번 칸 유지·나머지 재굴림 (EXP -10% · 3🪙) | lic_pin | 1045 |
| dev_copy | 📑 복사기 | 복사 | MANIP | **true** | **true** | 0 | 직전 결과 N번 칸을 옆칸에 복사 (EXP -10% · 5🪙) | lic_copy | 1047 |
| dev_swap | 🔃 교체기 | 교체 | MANIP | **true** | **true** | 0 | 직전 결과 N번 칸을 최다 심볼로 교체 (EXP -10% · 5🪙) | lic_swap | 1049 |
| dev_oracle | 🔮 예언안경 | 예언 | PEEK | false | **true** | 0 | 다음 스핀을 미리 보고 확정 | lic_oracle | 1051 |
| dev_bell | 🔔 비상졸업벨 | 비상 | INSTANT | false | **true** | 0 | 부족 EXP ≤25면 즉시 클리어 (1회 파괴) | lic_bell | 1053 |
| dev_syllabus | 📋 강의계획서 | (없음) | PEEK | false | false | 0 | 장착 시 증강/유물 선택에 '예상 티어' 사전 안내 | prismPick1 | 1056 |
| dev_holdfile | 🗂️ 보류파일 | 보류 | ARMED | **true** | false | 0 | 증강 선택 후보 1개를 보관 → 다음 증강 노드에서 비교 | item10 | 1058 |
| dev_retake | 🔁 재시험관 | 재추첨 | ARMED | false | **true** | 0 | 증강 선택지를 코인 소모로 1회 다시 뽑기(스테이지당 1회) | shop50 | 1060 |
| dev_major | 🎓 전공신청서 | (없음) | PASSIVE | false | false | 0 | 장착 시 주력 계열 증강 등장확률 소폭↑ | runs50 | 1062 |

- **cooldown 필드는 16종 전부 기본값 0**(생성자 인자로 넘긴 곳 없음). "스테이지당 1회" 등 사용 제한은 `cooldown` 필드가 아니라 서비스 레이어가 별도 카운터로 강제한다.
- MANIP 계열(dev_reroll/dev_pin/dev_copy/dev_swap)의 "EXP -10% · N🪙" 비용은 **Device 데이터클래스 필드가 아니라 desc 텍스트에만 존재** — 엔진에 구조화된 상수가 없다(RETAKE_COIN_COST=8만 유일하게 상수화됨, L1065). C# 이식 시 이 수치들을 별도로 하드코딩해야 함.
- `deviceUnlockReq(dev)`(L1073–1074): `ACHIEVEMENTS.firstOrNull{it.id==dev.unlockAch}`로 실제 업적을 찾아 그 `(key,threshold)` 1쌍을 반환. dev_flame~dev_bell 코드 인근 주석은 "lic_dev_flame" 등으로 적혀 있지만 **실제 unlockAch 값은 "lic_flame"** (dev_ 접두어 없음) — 주석과 필드값이 불일치한다(§11). 주석에 적힌 임계값(예: dev_flame "최고점수 50000 & S20")은 `lic_flame` 업적의 실제 정의(`SlotV2AchievementsExt.kt`, 본 문서 범위 밖)와 대조 검증이 필요하다.

### 8.1 패시브 장치 처리 (applyPassiveDevice, L1121–1127)

```kotlin
fun applyPassiveDevice(base: Mods, deviceId: String): Mods = when (deviceId) {
    "dev_flame" -> base.copy(expMul = base.expMul * 1.15)
    "dev_seal" -> base.copy(expMul = base.expMul * 1.05,
                             symbolWeightMul = base.symbolWeightMul + ("skull" to ((base.symbolWeightMul["skull"] ?: 1.0) * 0.0)))
    "dev_overheat" -> base.copy(expMul = base.expMul * 1.18,
                                 weightAdd = base.weightAdd + ("skull" to ((base.weightAdd["skull"] ?: 0.0) + 1.0)))
    "dev_subreel" -> base.copy(expMul = base.expMul * 0.7)
    else -> base
}
```
- dev_safe(최소 EXP 하한 보장)·dev_subreel의 reel+1(6칸 확장) 자체는 **여기서 처리되지 않음** — 주석("dev_safe(하한)·dev_subreel(reel+1)은 서비스서 추가 처리")대로 서비스가 추가로 구현해야 한다. 이 함수는 dev_subreel의 "최종 EXP -30%" 페널티만 적용.
- dev_seal: `symbolWeightMul[skull]`을 기존 값에 상관없이 **0.0으로 강제**(× 0.0). dev_overheat: `weightAdd[skull]`에 +1.0 가산(기본 가중 10에 추가 가산이므로 해골 등장 확률 증가).

### 8.2 능동(코인투입) 장치 처리 — applyItemMods 경유 (L1551)

```kotlin
"dev_coin" -> expMul *= 1.3
```
`dev_coin`은 `applyItemMods()`(아이템 오버레이 함수)에서 함께 처리된다(코인5 비용 차감 자체는 서비스 처리로 추정, 여기선 효과값 EXP×1.3만). dev_reroll/dev_pin/dev_copy/dev_swap/dev_oracle/dev_bell의 **실제 셀 재추첨/고정/복사/교체/미리보기/즉시클리어 로직은 SlotV2Engine.kt에 없다** — `rollOne(rng,mods)`(L2109, 가중추첨 1칸)와 `cellsFromIds(ids)`(L2105, id목록→Cell 복원)를 헬퍼로 제공할 뿐, 이를 조합해 "N번 칸 유지·나머지 재굴림" 등을 만드는 것은 서비스 몫이다.

### 8.3 보조 장치 슬롯 (P3, L1091–1118)

- 해금 조건(`slot2Unlocked`, L1095–1098): `devicesOwned>=5 AND deviceUses>=30 AND bestStage>=12`.
- 장착 가능 종류(`secondaryAllowed`, L1100): `kind==ARMED || kind==PEEK`만(PASSIVE/MANIP/INSTANT 금지).
- 메인·보조 동시 장착 시 **같은 장치 id 또는 같은 kind 금지**(`secondaryCandidates`, L1106–1109).
- 약화 배수 `SECONDARY_MUL=0.6`(L1112): `secondaryWeaken(increment) = increment*0.6`(가산 증분 약화), `secondaryMul(fullMul) = 1.0 + (fullMul-1.0)*0.6`(곱배수를 증분부만 약화). 예: dev_coin의 expMul×1.3(증분+0.30)을 보조 슬롯에서 쓰면 `1.0+(1.3-1.0)*0.6 = 1.18`. PEEK 계열은 약화 로직 적용 대상에서 제외(주석 L1111 "PEEK은 약화 어려워 적용 제외, 그대로 허용+보조 표시").

### 8.4 장치 추첨 (pickDevices, L1129–1135)

```kotlin
fun pickDevices(rng: Random, stage: Int = 1, n: Int = 1): List<Device> {
    val rareChance = (0.15 + stage * 0.03).coerceAtMost(0.6)
    return (1..n).map {
        val pool = if (rng.nextDouble() < rareChance) DEVICES.filter { it.rare } else DEVICES.filter { !it.rare }
        pool.randomOrNull(rng) ?: DEVICES.random(rng)
    }
}
```
희귀(rare) 확률 = `min(0.15 + stage×0.03, 0.6)` — 스테이지가 깊을수록 rare 장치 확률 상승, 상한 60%.

---

## 9. 해금/계정 시스템

### 9.1 unlockReq / meetsReq / statLabel (L177–223)

```kotlin
fun meetsReq(req: List<Pair<String, Long>>, stat: Map<String, Long>): Boolean =
    req.all { (key, thr) -> (stat[key] ?: 0L) >= thr }
```
모든 unlockReq는 **AND** 조건(리스트의 전 항목이 임계 이상). 빈 리스트 = 무료(스타터).

**statKey 전체 목록과 의미** (statName/statLabel 함수 + 파일 전역에서 실사용된 키 취합):

| statKey | 의미(statName/statLabel 라벨) | 특수 처리 |
|---|---|---|
| cherryTotal | 🍒체리 누적 등장 수 | |
| bookTotal | 📘책 누적 등장 수 | |
| starTotal | ⭐별 누적 등장 수 | |
| gemTotal | 💎보석 누적 등장 수 | |
| skullTotal | ☠해골 누적 등장 수 | |
| coinTotal | 🪙코인 누적 획득 수 | |
| crownTotal | 👑왕관 누적 등장 수 | |
| seedTotal | 🌱씨앗 누적 등장 수(school_research 전용, statName 미등록) | |
| wildTotal | 🌀와일드 누적 등장 수(school_research 전용, statName 미등록) | |
| bossClears | 보스 클리어 횟수 | |
| exactClears | 정확 클리어(요구EXP와 정확히 일치) 횟수 | |
| closeClears | 아슬아슬 클리어(부족10 이하) 횟수 | |
| lastSpinClears | 막판(마지막 스핀) 클리어 횟수 | |
| prayClears | 기도(PRAY) 클리어 횟수 | |
| allinWins | 올인(ALLIN) 성공 횟수 | |
| jackpots | 잭팟 발생 횟수 | |
| set4Plus | 세트 4개 이상 완성 횟수 | |
| prismPicks | 프리즘 증강 선택 횟수 | |
| shopBuys | 상점 구매 횟수 | |
| gambles | 도박 횟수 | |
| relicsMax | 런 내 유물 최대 보유 수 | |
| curseMax | 런 내 저주 최대 보유 수 | |
| devicesOwned | 소지 장치 종류 수 | |
| totalSpins | 총 스핀 횟수 | |
| itemsUsed | 아이템 사용 횟수 | |
| rerollUses | 재굴림 사용 횟수 | |
| pinUses | 고정 사용 횟수 | |
| deviceUses | 장치 사용 횟수 | |
| noDevStage | 무장치 최고 도달 스테이지 | |
| noShopS10 | 무상점으로 S10 도달 횟수 | |
| noItemMaxS | 무아이템 최고 도달 스테이지 | |
| curse5Stage | 저주5+ 보유로 도달한 최고 스테이지 | |
| curseBossClears | 저주3+ 보유 보스클리어 횟수 | |
| maxOverPct | 한 스테이지 최대 초과 % | |
| maxRunJackpots | 한 런 최다 잭팟 횟수 | |
| noPrismBestStage | 프리즘 증강 0개로 도달한 최고S | ACH-4 제한도전 |
| noRelicBestStage | 유물 0개로 도달한 최고S | ACH-4 |
| noGoldBestStage | 골드+프리즘 증강 0개(실버/유물만)로 도달한 최고S | ACH-4 |
| basicOnlyBestStage | 초보캐릭+기본머신으로 도달한 최고S | ACH-4 |
| noCommandBestStage | 특수 스핀명령 0회로 도달한 최고S | ACH-5c |
| noRerollBestStage | 재굴림/고정/복사/교체 0회(무조작)로 도달한 최고S | ACH-5c |
| bestScore | 최고 점수 | `statLabel`: `"{천단위}점"` |
| bestStage | 최고 도달 스테이지 | `statLabel`: `"S{thr}"` |
| runs | 총 런 수 | `statLabel`: `"{thr}런"` |
| distinctCharS10 | 서로 다른 캐릭터로 S10 도달한 캐릭 수 | `statLabel`: `"서로다른 캐릭 {thr}명 S10"` |
| minimalistS10 | 유물 3개 이하로 S10 달성 횟수 | `statLabel`: `"유물3↓ S10 {thr}회"` |
| noItemS8 | 아이템 없이 S8 도달 횟수 | `statLabel`: `"아이템없이 S8 {thr}회"` |
| richBossClears | 코인50+ 보유 상태로 보스클리어 횟수 | `statLabel`: `"코인50↑ 보스클리어 {thr}회"` |
| cstage_\<charId\> | 해당 캐릭터로 도달한 최고 스테이지 | `statLabel`: `"{캐릭emoji}{캐릭name} S{thr}"` |
| mstage_\<machineId\> | 해당 머신으로 도달한 최고 스테이지 | `statLabel`: `"{머신emoji}{머신name} S{thr}"` |
| seen_\<perkId\> | 해당 perk를 과거 등장/사용한 적 있는지(grandfather 플래그) | `perkUnlocked`에서만 참조 |
| bc_\<charId\>_\<machineId\> | 빌드도감: 그 캐릭+머신 조합 최고 스테이지 | `isBcKey`/`parseBcKey`로 파싱 |
| bld_\<themeBuildId\> | 테마빌드 완성 플래그(1개당, 25종) | `themeBuildDone` |
| bldCat_\<category\> | 카테고리별 완성 빌드 수(5개 카테고리) | `themeBuildStats` 파생 |
| bldTotal / bldAllBasic / bldAllMaster | 빌드도감 전체 진행도 파생값 | `themeBuildStats` 파생 |

`statLabel(key,thr)` 기본 분기 외 나머지는 `statName(key)+" "+thr`. 정의되지 않은 임의 키는 원문 그대로 라벨링(안전한 폴백).

### 9.2 전공(school) 연구 시스템 (L687–836)

```kotlin
data class UnlockGate(val minLevel: Int = 0, val req: List<Pair<String, Long>> = emptyList(), val school: String = "")
```

**기본 풀 — 게이트 없음(BASE_PERK_IDS, L706–713, 총 22개)**: 증강 10종(study, preview, review, diligence, cherry_up, book_up, star_up, gem_polish, coin_luck, set_sense) + 유물 12종(old_book, cherry_candy, rusty_coin, pencil, coffee, eraser, ruler, desk_lamp, cherry_jam, bookmark, coin_pouch, calculator). 이 22종은 `perkGate()`가 항상 빈 `UnlockGate()`를 반환 → 항상 등장.

**SCHOOL_REQ — 전공 기본 게이트 (L716–727, 10개 전공)**

| school | minLevel | req |
|---|---|---|
| 성장학 | 5 | cherryTotal≥200 & bestStage≥5 |
| 계산학 | 7 | set4Plus≥3 & exactClears≥1 |
| 경제학 | 8 | coinTotal≥300 & shopBuys≥5 |
| 운명학 | 9 | prayClears≥1 & gambles≥3 |
| 왕관학 | 10 | crownTotal≥30 & jackpots≥1 |
| 저주학 | 11 | skullTotal≥100 & curseMax≥1 |
| 시간학 | 8 | lastSpinClears≥3 & closeClears≥5 |
| 프리즘공학 | 12 | prismPicks≥3 & bossClears≥3 & bestStage≥10 |
| 씨앗학 | 12 | mstage_garden≥8 |
| 와일드학 | 13 | set4Plus≥10 |

**SCHOOL_RESEARCH — 전공 연구 입문 (L733–744, 10개, ACH-5a)**: 해당 achId 업적이 아니라 **`(key,threshold)`만으로 직접 stat 검사**(`schoolResearchDone`). 달성 시 그 전공의 실버/골드 perk가 **레벨 게이트 무관하게 즉시 개방**(단, PRISM 티어는 이 경로에서 제외 — `perkUnlocked`의 `p.tier != Tier.PRISM` 가드).

| school | achId(참조용) | key | threshold |
|---|---|---|---|
| 성장학 | cherry300 | cherryTotal | 300 |
| 계산학 | pc_set4_3 | set4Plus | 3 |
| 경제학 | coin300 | coinTotal | 300 |
| 운명학 | gamble3 | gambles | 3 |
| 왕관학 | crown30ext | crownTotal | 30 |
| 저주학 | skull100 | skullTotal | 100 |
| 시간학 | lastspin3 | lastSpinClears | 3 |
| 프리즘공학 | prismPick3 | prismPicks | 3 |
| 씨앗학 | sp_seed10 | seedTotal | 10 |
| 와일드학 | sp_wild10 | wildTotal | 10 |

```kotlin
fun schoolResearchDone(school: String, stat: Map<String, Long>): Boolean {
    val r = SCHOOL_RESEARCH[school] ?: return false
    return (stat[r.key] ?: 0L) >= r.threshold
}
```

**PERK_GATE_OVERRIDES — 개별 고위험 게이트 (L753–807, 45개, 전체 표)**

| perk id | minLevel | req | school |
|---|---|---|---|
| overdrive | 12 | prismPicks≥5 & bossClears≥3 | 프리즘공학 |
| short_day | 15 | bestStage≥15 & exactClears≥3 | 프리즘공학 |
| glass_cannon | 15 | bestScore≥30,000 & bestStage≥10 | 프리즘공학 |
| supernova | 17 | bestScore≥50,000 & bossClears≥8 | 프리즘공학 |
| endgame_rush | 14 | lastSpinClears≥10 & closeClears≥10 | 시간학 |
| wild_world | 13 | set4Plus≥10 | 와일드학 |
| joker | 16 | jackpots≥5 & set4Plus≥20 | 와일드학 |
| jackpot | 13 | crownTotal≥100 & jackpots≥3 | 왕관학 |
| mega_jackpot | 16 | crownTotal≥300 & jackpots≥10 | 왕관학 |
| seed_garden | 12 | mstage_garden≥8 | 씨앗학 |
| great_harvest | 15 | mstage_garden≥8 | 씨앗학 |
| key_master | 12 | coinTotal≥500 & shopBuys≥10 | 경제학 |
| gamblers_dice | 11 | allinWins≥5 & gambles≥10 | 운명학 |
| crystal_ball | 9 | prayClears≥1 | 운명학 |
| fate_handle | 11 | prayClears≥3 | 운명학 |
| gamblers_eye | 11 | allinWins≥5 | 운명학 |
| piggy_bank | 8 | coinTotal≥300 & shopBuys≥5 | 경제학 |
| hourglass_r | 10 | lastSpinClears≥5 | 시간학 |
| skull_idol | 11 | skullTotal≥300 | 저주학 |
| ominous_skull | 13 | curseMax≥3 | 저주학 |
| black_report | 13 | curseBossClears≥1 | 저주학 |
| crown_jewel | 10 | crownTotal≥50 | 왕관학 |
| crown_stand | 11 | crownTotal≥100 | 왕관학 |
| kings_ledger | 13 | jackpots≥3 | 왕관학 |
| focus_ring | 8 | exactClears≥1 | 계산학 |
| silver_mirror | 9 | set4Plus≥5 | 계산학 |
| greed_goblet | 12 | bestScore≥20,000 | 성장학 |
| flame_canister | 13 | mstage_flame≥8 | 저주학 |
| cursed_wallet | 13 | coinTotal≥500 & curseMax≥3 | 저주학 |
| early_prep | 3 | cherryTotal≥100 | 성장학 |
| growth_log | 5 | cherryTotal≥120 | 성장학 |
| early_adapt | 6 | cherryTotal≥200 & bestStage≥5 | 성장학 |
| snowball | 12 | cherryTotal≥400 & bestStage≥10 | 성장학 |
| fortune_check | 7 | prayClears≥1 | 운명학 |
| luck_accum | 9 | prayClears≥1 & gambles≥3 | 운명학 |
| fate_burst | 13 | prayClears≥3 & jackpots≥3 | 운명학 |
| late_focus | 6 | lastSpinClears≥3 | 시간학 |
| cliff_focus | 8 | lastSpinClears≥3 & closeClears≥5 | 시간학 |
| fate_bell | 14 | closeClears≥10 & bossClears≥5 | 시간학 |
| pair_match | 5 | set4Plus≥3 | 계산학 |
| puzzle_sense | 7 | set4Plus≥3 & exactClears≥1 | 계산학 |
| perfect_shape | 13 | set4Plus≥10 & exactClears≥3 | 계산학 |
| skull_watch | 9 | skullTotal≥100 | 저주학 |
| sacrifice | 11 | skullTotal≥200 & curseMax≥3 | 저주학 |
| black_diploma | 14 | skullTotal≥300 & curseBossClears≥1 | 저주학 |

**inferSchool(p) — desc 텍스트 기반 전공 추론 (L810–822, 우선순위 순서대로 첫 매치 채택)**

1. desc에 "☠" 또는 "해골" 또는 "저주" 포함 → 저주학
2. "👑"/"왕관"/"🌀"/"와일드"/"잭팟" → 왕관학
3. "🌱"/"씨앗" → 씨앗학
4. "🎲"/"주사위"/"희귀"/"올인"/"기도" → 운명학
5. "코인"/"🪙"/"상점"/"🗝"/"열쇠" → 경제학
6. "막"/"첫스핀"/"첫 스핀"/"마지막 스핀"/"막스핀" → 시간학
7. "세트"/"가운데"/"양끝"/"인접"/"붙은"/"콤보" → 계산학
8. 그 외 전부 → 성장학(기본 폴백)

**perkGate(p) 최종 결합 (L825–836)**
```kotlin
fun perkGate(p: Perk): UnlockGate {
    if (p.id in BASE_PERK_IDS) return UnlockGate()
    PERK_GATE_OVERRIDES[p.id]?.let { return it }
    val school = inferSchool(p)
    val base = SCHOOL_REQ[school] ?: UnlockGate()
    return when (p.tier) {
        Tier.PRISM  -> base.copy(minLevel = max(base.minLevel + 4, 12))
        Tier.GOLD   -> base
        Tier.SILVER -> base.copy(minLevel = max(base.minLevel - 2, 2))
    }
}
```
판정 우선순위: ①BASE(빈게이트) → ②OVERRIDE(개별) → ③전공 추론+티어 보정.

**perkUnlocked(p, stat) (L884–892)**
```kotlin
fun perkUnlocked(p: Perk, stat: Map<String, Long>): Boolean {
    if ((stat["seen_" + p.id] ?: 0L) > 0L) return true          // grandfather: 과거 노출/사용 이력
    val g = perkGate(p)
    if (p.tier != Tier.PRISM && schoolResearchDone(g.school, stat)) return true  // 전공연구 우회(프리즘 제외)
    if (accountLevel(stat) < g.minLevel) return false
    return meetsReq(g.req, stat)
}
```

### 9.3 accountExp / expToLevel / expForLevel (L840–880)

```kotlin
fun accountExp(stat: Map<String, Long>): Long {
    var exp = 0L
    val bs = stat["bestStage"] ?: 0L
    if (bs >= 3) exp += 10; if (bs >= 5) exp += 30; if (bs >= 10) exp += 80; if (bs >= 15) exp += 150   // ① 마일스톤 합산(중첩, 조건 4개 모두 개별 체크)
    exp += ((stat["bossClears"] ?: 0L) * 8L).coerceAtMost(120L)     // ② 보스클리어×8, 상한120
    exp += ((stat["runs"] ?: 0L) * 3L).coerceAtMost(90L)            // ③ 런×3, 상한90
    for (a in ACHIEVEMENTS) if ((stat[a.key] ?: 0L) >= a.threshold) exp += achTierExp(a.tier)   // ④ 달성 업적 tier합
    exp += stat.count { (k, v) -> v > 0L && (k.startsWith("bc_") || k.startsWith("bld_")) } * 40L  // ⑤ 빌드도감 완성 1개당+40
    for ((k, v) in stat) if (k.startsWith("cstage_") || k.startsWith("mstage_")) exp += medalExp(medalFor(v))  // ⑥ 숙련메달
    return exp
}
private fun achTierExp(tier: String): Long = when (tier) { "프리즘"->250L; "골드"->120L; "실버"->50L; else->20L }  // 브론즈=20
private fun medalExp(m: Medal): Long = when (m) { GOLD->100L; SILVER->50L; BRONZE->20L; NONE->0L }
```
①은 **4개 조건이 각각 독립 if**이므로 bestStage≥15면 10+30+80+150=270 **전부 누적**(else-if 아님).

```kotlin
fun accountLevel(stat): Int = expToLevel(accountExp(stat))
fun expToLevel(exp: Long): Int = (1 + floor(sqrt(exp.coerceAtLeast(0).toDouble() / 22.0)).toInt()).coerceIn(1, 25)
fun expForLevel(level: Int): Long {
    if (level <= 1) return 0L
    val l = (level - 1).toDouble()
    return ceil(l * l * 22.0).toLong()
}
```
레벨 공식: `level = clamp(1 + floor(sqrt(exp/22)), 1, 25)`. 역함수(다음 레벨 필요 누적exp): `expForLevel(level) = ceil((level-1)^2 × 22)`, level≤1이면 0. 레벨 상한 25(캡).

### 9.4 관련 부가 시스템 (요약, 본 문서 §5–8과 교차)

- **업적**: `ACHIEVEMENTS_BASE`(16개, L1470–1487, cat/tier/threshold/desc 포함) + `SlotV2AchievementsExt.LIST`(외부 파일, 미추출) 합본이 `ACHIEVEMENTS`(L1489).
- **숙련 메달**: `Medal{NONE,BRONZE,SILVER,GOLD}`, `medalFor(stage)`: stage≥15→GOLD, ≥10→SILVER, ≥5→BRONZE, else NONE(L1230–1239).
- **빌드도감**: `bc_<char>_<machine>` 캐릭×머신 조합 최고스테이지(16×16=256 슬롯), `THEME_BUILDS` 25종 테마빌드(성장형5·운명형5·역전형5·조합형5·위험형5, L1276–1312) + `evalThemeBuilds(ctx)`(L1392–1433) 판정 로직.
- **표준 도전**: `STD_CHALLENGES` 10종(L1148–1159).
- **통합 도전판**: `allChallenges(stat)`가 장치업적해금+캐릭/머신해금+표준도전을 하나의 `ChallengeItem` 리스트로 병합(L1181–1209).

---

## 10. RNG 사용 방식

엔진은 **전역 Random을 두지 않고 모든 함수가 `rng: Random`을 매개변수로 명시 전달**받는 순수함수 스타일(파일 헤더 주석 "순수 로직, 상태 없음"과 일치). 시드는 서비스 레이어가 생성/보관하며 이 파일은 시드 자체를 모른다.

### 10.1 심볼 추첨 — weighted() (L2079–2091)

```kotlin
private fun weighted(rng: Random, mods: Mods): Sym {
    var total = 0.0
    val w = DoubleArray(SYMS.size)
    SYMS.forEachIndexed { i, s ->
        var x = s.weight.toDouble()
        if (s.rare) x *= mods.rareWeightMul                 // crown·wild만
        x *= mods.symbolWeightMul[s.id] ?: 1.0               // 머신/아이템 곱배수
        x += mods.weightAdd[s.id] ?: 0.0                     // 휴면심볼 가산 주입
        w[i] = x; total += x
    }
    var r = rng.nextDouble() * total       // RNG 호출 1회
    for (i in SYMS.indices) { r -= w[i]; if (r <= 0) return SYMS[i] }
    return SYMS[0]   // 폴백(부동소수 오차로 루프가 끝까지 간 경우)
}
```
- SYMS 리스트 **선언 순서**(cherry,book,star,gem,coin,skull,flame,magnet,bomb,crown,key,dice,seed,wild)가 누적가중치 스캔 순서를 결정 — 순서를 바꾸면 같은 `r` 값이라도 다른 심볼이 나올 수 있음(동일 총합이어도 절단 지점이 달라짐). C# 포트는 이 리스트 순서를 **정확히 동일하게** 유지해야 함.
- `total==0`인 극단적 상황(모든 가중치가 0으로 눌린 경우)에서는 `r = 0`이 되어 첫 반복에서 `w[0]=0`, `r-=0=0`, `0<=0` 참 → 무조건 `SYMS[0]`(cherry) 반환. 우연이 아니라 루프 구조상 필연적 폴백.

### 10.2 원시 셀 굴림 — rollRaw() (L2095–2102)

```kotlin
fun rollRaw(rng: Random, mods: Mods, reel: Int = REEL, seedActive: Boolean = false): MutableList<Cell> {
    val cells = MutableList(reel) { Cell(weighted(rng, mods)) }   // index 0→reel-1 순차 RNG 소비(reel회)
    if (seedActive) {
        val grow = listOf("book", "star", "crown").random(rng)     // (reel+1)번째 RNG 호출
        cells[rng.nextInt(reel)] = Cell(SYM_BY_ID.getValue(grow), "🌱→")   // (reel+2)번째 RNG 호출
    }
    return cells
}
```
호출 순서 고정: **[reel칸 weighted() 순차 호출] → (seedActive면) [성장 심볼 종류 선택] → [치환할 칸 인덱스 선택]**. `MutableList(size){init}`은 Kotlin에서 인덱스 0부터 오름차순으로 `init`을 정확히 1번씩 호출하는 것이 명세로 보장됨(C#의 순차 for-loop과 동등) — 병렬/지연평가로 재구현 금지.

### 10.3 다이스 심볼 — evaluate() 내부 (L2208)

```kotlin
Sp.DICE -> { val d = rng.nextInt(1, 13); exp += d; ... }   // [1,12] 균등, 셀 왼쪽→오른쪽 순회 중 DICE 심볼 등장할 때마다 1회씩 호출
```
기본 심볼 풀에서 dice는 weight=0(휴면)이라 casino 머신(`weightAdd.dice=4.0`)이나 `gamblers_dice` 퍽 보유 시에만 등장 — 등장 여부/개수에 따라 이 스핀에서 소비되는 RNG 호출 수가 달라짐(가변).

### 10.4 셀 조작 — applyCellOps() (L2114–2125)

`wild_temp`만 RNG 사용: `cells[rng.nextInt(cells.size)] = wild`(1회). eraser 계열/fake_crown은 `minByOrNull`/`maxByOrNull` 결정론적 선택(RNG 미사용, 동률 시 첫 인덱스 채택).

### 10.5 퍽/유물/아이템/장치 추첨 함수 — 호출 순서 상세

| 함수 | RNG 소비 패턴 | 라인 |
|---|---|---|
| `rollTier(rng,stage)` | `rng.nextDouble()×1` | 1569–1573 |
| `pickAugments(rng,stage,held,n=3,stat)` | `while` 루프(최대 60회 guard): 매 반복마다 `rollTier`(1회) + `randomOrNull`(0~1회) + 실패시 폴백 `randomOrNull`(0~1회) — **반복마다 1~3회, 성공 3개까지 가변 총량** | 1588–1598 |
| `pickRelics(rng,held,n=3,stat)` | `gatedPool(...).filter{...}.shuffled(rng)`: 필터링된 **전체 풀 크기**만큼 셔플 비용 소비(요청 n과 무관하게 pool.size에 비례) 후 `.take(n)` | 1601–1602 |
| `pickAugmentsCurated`/`pickRelicsCurated` | `randomOrNull` 다회 + 최종 `shuffled(rng)`(선택된 3개 재셔플) | 1619–1643 |
| `pickPerksByTier(rng,...)` | (a) `forceTier==null && !bossClear`일 때만 `rng.nextInt(total)`로 티어 결정(1회) — forceTier 지정/bossClear=true면 이 호출 자체가 스킵됨(RNG 소비 0) → **분기에 따라 호출 여부 자체가 달라짐** (b) favoredCat 지정 시 `randomOrNull`(0~1) (c) fav!=null&&fav!=cat이면 `randomOrNull`(0~1) (d) 최대 80회 guard 루프: `randomOrNull`(0~1)/회, 3개 채울 때까지 (e) 최종 `out.shuffled(rng)` | 1654–1700 |
| `setSynergyAug(held,exclude,rng,cat)` | 세트별 루프 중 `missingAug.randomOrNull(rng)` — 세트당 최대 1회, 첫 성공에서 즉시 반환(누적 아님) | 655–670 |
| `pickItems(rng,n=3)` | `ITEMS.shuffled(rng)`(73개 전체 셔플) 후 `.take(n)` | 1004 |
| `pickDevices(rng,stage,n=1)` | 매 항목마다 `rng.nextDouble()`(rare 판정, 1회) + `pool.randomOrNull(rng)`(0~1회, pool 비었을 때만 폴백 `DEVICES.random(rng)` 추가 1회) | 1129–1135 |
| `rollNodes(rng,count=3)` | `Node.values().toMutableList().shuffled(rng)`(8개 전체 셔플) 후 `.take(count)` | 2401–2404 |

**핵심 규칙**: Kotlin `Iterable<T>.randomOrNull(rng)`는 **컬렉션이 비어있으면 RNG를 전혀 소비하지 않고 즉시 null 반환**, 비어있지 않으면 정확히 `rng.nextInt(size)` 1회를 소비한다. 이 "조건부 소비"를 C# 포트가 정확히 재현하지 못하면(예: 항상 `Next()`를 먼저 호출하고 결과를 버리는 식으로 구현) 이후 모든 시드 재현이 어긋난다.

### 10.6 seedActive 성장 트리거

`rollRaw`의 `seedActive` 파라미터는 이전 스핀의 `SpinResult.seedNext`(L2351: `cells.any{it.sym.special==Sp.SEED}`) 값을 서비스가 다음 스핀 호출 시 전달하는 구조 — 즉 **씨앗 성장은 스핀 N에서 판정되고 스핀 N+1의 굴림 방식에 영향**을 준다(1스핀 지연). RNG 시퀀스 재현 시 이 상태 이월을 함께 재현해야 함.

---

## 11. C# 이식 시 주의

1. **RNG 알고리즘 자체가 다르다.** `kotlin.random.Random`(기본 구현은 XorWoShiRo116++ 계열, JVM에서는 별도)과 C#의 `System.Random`은 내부 알고리즘이 다르므로, 동일 시드를 넣어도 `nextDouble()`/`nextInt()` 시퀀스가 일치하지 않는다. 리플레이/시드 검증 기능이 필요하면 Kotlin의 PRNG를 C#으로 바이너리 호환되게 재구현하거나, 반대로 자체 PRNG(예: xoshiro256**)를 양쪽에 공통 이식해야 한다. 단순히 "난수 사용"만 옮기면 리플레이 재현성은 보장되지 않는다.
2. **`List<T>.shuffled(rng)` 알고리즘 재현.** Kotlin 표준 라이브러리의 shuffle 구현(내부적으로 Fisher-Yates, 뒤에서부터 스왑)을 그대로 재현해야 RNG 소비 횟수와 순서가 일치한다. C#에는 내장 `Shuffle`이 없으므로 직접 구현 시 스왑 방향·인덱스 범위를 Kotlin 소스와 대조해야 한다.
3. **`randomOrNull`/`random`의 "빈 컬렉션 = RNG 미소비" 규칙**(§10.5) — off-by-one으로 RNG 상태가 어긋나는 가장 흔한 지점. 특히 `pickPerksByTier`처럼 조건부로 RNG 호출 자체가 스킵되는 함수(§10.5 (a))는 분기 로직을 1:1로 옮겨야 한다.
4. **`Random.nextInt(from, until)`/`Random.nextDouble()` 경계.** Kotlin `nextInt(1,13)`은 상한 **배타적**([1,12]), `nextDouble()`은 [0,1). C# `Random.Next(1,13)`도 상한 배타적([1,12]), `NextDouble()`도 [0,1)로 동일하지만, 이식자가 "혹시 하나가 포함/배타 다르지 않을까" 혼동하기 쉬운 지점이라 명시적으로 단위 테스트 권장.
5. **quota(stage>15) 반복곱 vs 거듭제곱.** `q *= 1.2`를 `(stage-15)`번 반복하는 것과 `755 * Math.Pow(1.2, stage-15)`는 부동소수 반올림 경로가 달라 아주 드물게 최종 `toLong()` 결과가 1 차이 날 수 있다. 원본과 동일하게 **반복 곱셈 루프**로 이식할 것.
6. **`Double.toLong()`(Kotlin) vs `(long)`(C#) 캐스트 차이.** Kotlin의 `Double.toLong()`은 NaN→0, +Infinity→Long.MAX_VALUE, -Infinity→Long.MIN_VALUE로 정의되어 있으나, C#의 `(long)someDouble`은 범위를 벗어나거나 NaN이면 `unchecked` 컨텍스트에서 미정의/구현별 결과, `checked` 컨텍스트에서 `OverflowException`을 던진다. 이 파일에서는 음수/NaN이 나올 여지가 낮지만(대부분 `coerceAtLeast(0)`로 방어), 방어적으로 `Convert.ToInt64` 대신 명시적 클램프 후 캐스트를 권장.
7. **HashMap 순회 순서 의존.** `counts.maxByOrNull{it.value}`(evaluate, L2181)는 동점(tie) 시 "먼저 발견된" 항목을 반환하는데, Kotlin `HashMap`의 순회 순서는 언어 명세상 보장되지 않는다(사실상 JVM HashMap 해시 버킷 순서에 의존). C#의 `Dictionary<K,V>`도 순서 미보장이라 **두 언어의 HashMap 반복 순서가 다르면 동점 상황에서 다른 심볼이 선택될 수 있다.** 결정론이 필요하면 `LinkedHashMap`/`Dictionary` 대신 삽입순서가 고정된 구조(예: SYMS 리스트 순서를 우선순위로 쓰는 명시적 tie-break)로 바꿔야 한다 — 원본 자체가 이미 이 리스크를 안고 있으므로, 이식 시 "심볼 선언 순서(SYMS 리스트 순)"로 동점 우선순위를 명시적으로 고정하는 편이 안전하다.
8. **Mods.copy() 얕은 복사 + Map 불변성 가정.** `applyItemMods`/`applyPassiveDevice`는 Kotlin 불변 `Map`을 전제로 `base.symbolWeightMul + (k to v)`(새 맵 생성) 또는 `HashMap(base.symbolWeightMul)`(방어적 복사) 패턴을 쓴다. C#에서 `Dictionary`는 기본적으로 가변(mutable)이므로, `record`/`with` 패턴을 쓰더라도 내부 Dictionary를 **참조 공유한 채 그대로 두면 한 스핀의 수정이 다른 곳에 누출**된다. C# 포트는 Mods를 불변 컬렉션(`ImmutableDictionary`) 또는 매 변경마다 명시적 clone으로 구현해야 함.
9. **캡(capMul) 로직의 연산 순서.** `evaluate()`의 총배율 캡(L2278–2330)은 "center 배수 제외 baseline(`capBase`) 산출 → center/flame/first·last/희귀버스트/세트배수 등 적용 → 전역 `expMul`+`flatExp` 적용 → `(exp-flatExp)`를 `capBase×capMul`로 클램프 → 잭팟 고정가산은 클램프 이후 별도 가산"이라는 **엄격한 순서 의존** 로직이다. 특히 `centerExpMul`은 `capBase`에는 포함되지 않지만 클램프 대상인 `exp`에는 포함되므로, "가운데 칸 배수로 불어난 만큼도 캡에 걸릴 수 있다"는 비대칭이 의도된 동작이다. 순서를 바꾸면(예: 캡을 center 적용 전에 걸거나, flatExp를 클램프 대상에 포함하는 등) 수치가 달라진다.
10. **정수 나눗셈.** `reel/2`(가운데 칸 인덱스), `(stage/5)-1`(보스 순환 인덱스) 등은 Kotlin/C# 모두 양의 정수에서 0쪽으로 절삭하는 동일한 truncating division이라 이 파일 범위에서는 안전하지만, 스테이지/릴 값이 음수가 될 수 없다는 전제가 깨지면(예: 방어 코드 누락) 두 언어의 음수 나눗셈/모듈로 결과가 달라질 수 있으니 새 코드 작성 시 항상 입력을 양수로 보장할 것.
11. **문화권(Locale) 의존 문자열 포맷.** `"%,d".format(thr)`(statLabel, L197)와 `fmtMul`의 `"%.2f".format(v)`(L2077)는 Kotlin/JVM 기본 로케일에 의존한다. C# `ToString("N0")`/`ToString("F2")` 역시 `CultureInfo.CurrentCulture`에 의존하므로, 기기 로케일이 콤마/점 구분자를 다르게 쓰는 지역(예: 유럽식 "1.000,00")이면 표시가 달라진다. UI 텍스트를 서버/세이브 데이터 비교에 쓰지 않는 한 큰 문제는 아니지만, 고정 포맷이 필요하면 `CultureInfo.InvariantCulture`를 명시할 것.
12. **`when(id) { ... }`의 무명 case 무시(silent no-op).** `buildMods`/`applyItemMods`/세트 루프의 `when` 문은 매칭되지 않는 id에 대해 아무 것도 하지 않고 넘어간다(예외 없음) — 저장된 세이브에 삭제되었거나 오타난 퍽 id가 남아있어도 무시된다. C# `switch`에서도 `default: break;`(예외 던지지 않음)로 동일하게 관용적으로 처리해야 구버전 세이브 호환이 깨지지 않는다.
13. **`Random`을 상태 없이 매개변수로만 전달하는 설계를 유지할 것.** 원본은 정적/전역 Random이 전혀 없다(파일 헤더의 "순수 로직, 상태 없음" 원칙). C# 포트에서 `System.Random.Shared`나 static 필드로 편의상 바꾸면 테스트 결정성과 스핀 독립 원칙이 깨진다 — 반드시 함수 인자로 명시 전달.
14. **Mods 필드 중 실제로는 죽어있는(dead) 필드가 있다.** `Mods.skullPenaltyMul`(항상 1.0, 어떤 퍽/저주/세트/아이템/장치도 값을 바꾸지 않음, L1738/2013/2273 참고), `Mods.flatScore`(항상 0, buildMods 반환문에 아예 포함 안 됨), `Mods.overkillScoreMul`·`Mods.carryoverPct`(선언만 되고 전체 파일에서 읽히지도 쓰이지도 않음, "초과 EXP 이월" 기능은 헤더 주석에서 예고만 되고 미구현)는 C# 포트에서 필드는 유지하되 "현재 콘텐츠에서 항상 기본값"이라는 점을 인지하고 있어야 한다(향후 콘텐츠 확장 훅으로 추정).

---

## 부록 A. 발견한 모호/특이 사항 요약 (보고용, 본문 각주 참고)

1. **보스 flavor text와 실제 코드 불일치** — `finals`/`strict`/`luck` 3종 보스는 desc에 "요구↑"라고 적혀 있지만 실제 `quotaMul`은 기본값 1.0(변화 없음). `grad`만 실제로 1.15. 또한 4종 보스 각각의 "막스핀 EXP×2", "같은심볼 3개 없으면 ×0.5", "⭐👑🌀 배수" 같은 개별 전투 규칙은 `SlotV2Engine.kt`에 실행 코드가 전혀 없음(순수 설명 텍스트이거나 미확인 서비스 파일에 구현) — C# 이식 전 원 기획 의도(서비스 레이어 존재 여부) 확인 필요.
2. **`Boss` 기본 `bonusSpins=2`이지만 4종 전부 `1`로 오버라이드** — 데이터클래스 기본값과 실제 사용값이 다름. 이식 시 기본값이 아니라 실제 인스턴스 값을 옮겨야 함.
3. **캐릭터 `gambler`(도박꾼)와 `honor`(수석졸업생)는 `buildMods`에 처리 분기가 없음** — desc에 적힌 "스테이지당 1회 무료 재굴림"(gambler), "실버 증강 1개로 시작"(honor)은 엔진 밖(서비스)에서 구현되어야 함. 이식 시 이 두 캐릭터의 buildMods 기여분은 0(scoreMod 필드만 유효)이라는 점을 놓치기 쉬움.
4. **`set_align`과 `set_perfect_calc`의 requires가 완전히 동일**(center, twins, chain)한데 상호 배제 조건이 없어 **둘 다 동시 발동** — `adjacentSameExp`가 +10과 +14로 이중 가산되고 `centerExpMul`도 추가로 ×1.3 곱해짐. 의도된 중첩 보너스인지 콘텐츠 중복 실수인지 원본에 명시 없음.
5. **Device.unlockAch 주석과 실제 필드값 불일치** — dev_flame~dev_bell 6종의 인라인 주석은 "lic_dev_flame" 형태(dev_ 접두)로 적혀 있지만 실제 `unlockAch` 문자열은 "lic_flame"(dev_ 없음). 주석에 적힌 임계값(예: 최고점수 50000 & S20)이 실제 그 achievement id의 정의와 일치하는지는 `SlotV2AchievementsExt.kt`(본 추출 범위 밖) 확인이 필요.
6. **장치 MANIP 계열의 코인/EXP 비용이 구조화 필드가 아니라 desc 문자열에만 존재** — dev_reroll/dev_pin(3🪙·EXP-10%), dev_copy/dev_swap(5🪙·EXP-10%). `Device` 데이터클래스에 별도 cost 필드가 없어 C# 이식 시 이 수치들을 별도 상수로 새로 정의해야 함(원본에 참조할 상수가 없음).
7. **`cooldown` 필드가 16개 장치 전부 기본값 0** — "스테이지당 1회" 등 실제 사용 제한은 이 필드가 아니라 서비스 카운터가 강제하는 것으로 추정. 필드만 보고 쿨다운을 이식하면 안 됨.
8. **Mods의 일부 필드가 콘텐츠 미구현 상태로 선언만 되어 있음** — `skullPenaltyMul`(항상 1.0), `flatScore`(항상 0), `overkillScoreMul`/`carryoverPct`(완전 미사용, "초과 EXP 이월"은 파일 헤더 주석에서 예고되었으나 어떤 퍽도 구현 안 함). 이식 시 "예약 필드"로 표시하고 향후 확장 여지를 남겨둘지 결정 필요.
9. **잭팟 `else -> 200` 분기는 현재 도달 불가능한 방어 코드** — `bestId`는 항상 `VALUE_IDS={cherry,star,book,gem,crown}` 중 하나이므로 5개 case가 전부 커버함. 이식 시 굳이 옮길 필요는 없지만 향후 VALUE_IDS 확장 시를 대비한 방어 코드로 유지 권장.
10. **저주 스택 임계 보너스(5개+/7개+)가 중첩 곱** — `nCurses>=5`와 `nCurses>=7`이 별개 if문이라 저주 7개 이상이면 `scoreMul *= 1.12`가 두 번 적용(총 1.2544배). else-if가 아니므로 실수가 아니라 의도된 가속형 보상으로 보이나, 문서화가 필요해 명시함.
11. **`school`별 SCHOOL_RESEARCH 우회 경로가 PRISM 티어는 제외**(`p.tier != Tier.PRISM` 가드, perkUnlocked L889) — 전공 연구를 완료해도 프리즘 퍽은 여전히 레벨+req를 둘 다 만족해야 함(또는 보스클리어로 별도 획득, `pickPerksByTier`의 bossClear 강제 로직 참고). 텍스트만 보면 "연구하면 그 전공 실버/골드/프리즘 다 풀린다"로 오해하기 쉬움.
12. **`quota(stage)`가 stage≤0에서도 `QUOTAS[0]`(110)을 반환** — 스테이지 0 이하가 실제 게임에서 발생하는지 불명확하나, 방어적 폴백이 존재함을 기록.

## 부록 B. 섹션별 원본 라인 수(참고용)

| 구간 | 라인 범위 | 대략 라인 수 |
|---|---|---|
| 헤더/상수 | L1–95 | 95 |
| 심볼/Mods/해금공용 | L97–223 | 127 |
| 머신 | L225–306 | 82 |
| 캐릭터 | L308–373 | 66 |
| 퍽 정의(증강/유물/저주) | L375–569 | 195 |
| 세트 효과 정의 | L571–670 | 100 |
| 해금 게이트/전공/졸업레벨 | L672–914 | 243 |
| 아이템 정의 | L916–1012 | 97 |
| 장치 정의/보조슬롯 | L1014–1135 | 122 |
| 도전판/숙련도/빌드도감 | L1137–1461 | 325 |
| 업적/아이템모드/퍽추첨 | L1463–1700 | 238 |
| buildMods(머신+캐릭+퍽+저주+세트) | L1702–2026 | 325 |
| 점수보정/칭호/연승 | L2028–2053 | 26 |
| 스핀 결과/굴림/평가(evaluate) | L2055–2356 | 302 |
| 스핀 명령 비용/노드 | L2358–2406 | 49 |
| **합계** | | **2,406줄(전체)** |

(참고: 작업 지시서에는 원본이 "2,206줄"로 명시되었으나 실제 열람 결과 파일은 총 2,406줄이었다. 커밋 c73452c 시점 기준 라인 수 표기 오차로 추정 — 본 문서의 모든 라인 번호는 실제 읽은 파일 기준.)
