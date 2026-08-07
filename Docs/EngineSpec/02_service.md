> 원본: SlotV2Service.kt @ 커밋 c73452c, 추출일 2026-07-30
>
> ⚠️ **파일 길이 불일치**: 작업 지시에는 "2,437줄"로 명시되었으나 실측 결과 `SlotV2Service.kt`는 총 **2,591줄**이다(빈 줄 포함, `wc -l` 확인). 본 문서는 실제 파일 전체(1~2,591줄)를 읽고 작성했다. 아래 인용은 모두 `SlotV2Service.kt:L줄번호` 형식이며, 런 상태 필드(§1)만 예외적으로 `data/SlotV2Entities.kt`(같은 리포지토리, Room 엔티티 정의 파일)를 인용한다 — `SlotV2Service.kt`가 `SlotV2RunRow`를 참조만 하고 필드를 선언하지 않기 때문에, "상태 필드 전부"를 정확히 적으려면 그 정의부가 필수였다.
>
> ⚠️ **범위 경계**: 이 서비스는 대부분의 수치 상수(스핀당 EXP 계수, 심볼 값, 쿼터 공식, 각종 코인 비용 상수 등)를 `SlotV2Engine`에 위임한다. 본 문서는 `SlotV2Service.kt`에 **직접 리터럴로 존재하는 수치**는 원문 그대로 기재하고, `SlotV2Engine`에만 정의된 값은 "Engine 정의값 — 별도 추출 필요"로 명시했다(임의로 값을 추정/기입하지 않음).

# 잭팟런 v2 — SlotV2Service 정밀 사양

---

## 0. 개요 (L1‑17)

- 단일라인 5칸 슬롯 로그라이크. 흐름: 캐릭터 선택 → 머신 선택 → (장치 장착) → 스테이지 반복(스핀 N회 안에 요구 EXP) → 노드 선택 → … → 보스(5스테이지마다) → 게임오버(실패) → 점수 기록.
- 진행 입력은 카카오톡 채팅 **댓글(답글)**로 받는다. 상위 라우터(`ChatMessageHandler`)가 `isControlToken()`으로 게임 관련 입력인지 필터링한 뒤 `handleInput()`을 호출한다(L13, L60‑67).
- 콘텐츠는 증강/유물/아이템/저주/장치 모두 구현되어 있음(주석 L15의 "다음 단계" 경고는 구버전 잔재로 보임 — 실제 코드는 전부 존재).

---

## 1. 런 상태 머신

### 1‑A. 상태(state) 값과 전이

`SlotV2RunRow.state` 필드로 관리되며(문자열), `handleInput()`의 `when(run.state)` 분기(L184‑200)에서 라우팅된다.

| state | 진입 조건 | 처리 핸들러 | 다음 state |
|---|---|---|---|
| `CHAR_SELECT` | 런 시작, 2회차 이상 & 웹픽 없음 (L294‑312) | `handleCharSelect` (L366‑386) | `MACHINE_SELECT` |
| `MACHINE_SELECT` | 캐릭터 선택 완료 | `handleMachineSelect` (L388‑393) → `proceedAfterMachine` (L400‑416) | `DEVICE_SELECT` 또는 바로 `SPIN`(소지 장치 없으면) |
| `DEVICE_SELECT` | 머신 확정 + 장착 가능 장치(`equipableDeviceList`) 1개 이상 | `handleDeviceSelect` (L445‑452) → `offerSecondaryOrLaunch` (L458‑468) | `DEVICE_SELECT2` 또는 `SPIN` |
| `DEVICE_SELECT2` | 메인 장치 확정 + 보조 슬롯 해금(`slot2Unlocked`) + 보조 후보 1개 이상 | `handleDeviceSelect2` (L470‑478) | `SPIN` (`launchRun` 호출) |
| `SPIN` | 장치 선택 종료 후, 또는 노드/상점/증강 처리 완료 후 복귀 | 입력이 `DEVICE_CMDS`면 `handleDevice`, 아니면 `handleSpin` (L189, L511‑715) | `SPIN`(계속) / `POST_SPIN` / `NODE_SELECT`(클리어) / 런 삭제(게임오버) |
| `POST_SPIN` | 마지막 스핀 실패 + (MANIP 장치 미사용 또는 도박꾼 무료재굴림 미사용) (L694‑705) | `handlePostSpin` (L1896‑1913) | `SPIN`(만회 실패 시 재시도 불가, 이 경로는 1회성) / 런 삭제(게임오버/포기) |
| `NODE_SELECT` | 스테이지 클리어 직후(`clearStage`, L890) | `handleNodeSelect` (L1125‑1246) | 노드 종류에 따라 `EVENT_AUGMENT`/`EVENT_RELIC`/`EVENT_SHOP`/즉시 `SPIN` |
| `EVENT_AUGMENT` | 노드에서 "AUGMENT" 선택, 또는 위험거래(RISK)에서 프리즘/골드 즉시 지급은 상태 진입 없이 바로 `SPIN`(L1160‑1174, 주의: RISK는 EVENT_AUGMENT를 거치지 않고 직접 지급) | `handlePerkPick`(L1371‑1388) / 보조명령 `handleHoldAug`(L1336‑1351) / `handleRetake`(L1354‑1369) | `SPIN` |
| `EVENT_RELIC` | 노드에서 "RELIC" 선택 | `handlePerkPick`, `handleRetake`(재추첨만 가능, 보류파일 불가 — L1337) | `SPIN` |
| `EVENT_SHOP` | 노드에서 "SHOP" 선택 (L1179‑1182) | `handleShop` (L1629‑1681) | `SPIN`("0" 입력 시) |

**엔티티 주석의 오기재**: `SlotV2RunRow.state` 필드의 KDoc(`data/SlotV2Entities.kt:19`)은 `"CHAR_SELECT / MACHINE_SELECT / SPIN / NODE_SELECT / EVENT_AUGMENT / EVENT_RELIC / EVENT_SHOP / EVENT_ITEMSHOP / EVENT_GAMBLE / EVENT_REST / EVENT_CURSE"`라고 적혀 있으나, 실제 코드에는 `EVENT_ITEMSHOP`/`EVENT_GAMBLE`/`EVENT_REST`/`EVENT_CURSE`라는 state가 **존재하지 않는다**. REST/GAMBLE/EVENT/CURSE/RISK 노드는 `handleNodeSelect` 내부에서 즉시 계산되어 별도 상태 전이 없이 바로 `SPIN`으로 돌아간다(L1191‑1246). `DEVICE_SELECT`/`DEVICE_SELECT2`도 주석엔 없지만 실제로 존재한다. **C# 포팅 시 이 주석이 아니라 실제 코드 분기(L184‑200)를 진실로 삼을 것.**

### 1‑B. 런 시작 특수 분기

- **첫 런(생애 누적 `runs==0`)**: 캐릭터/머신 선택을 완전히 건너뛰고 `charId="novice"`, `machineId="basic"`, `state="SPIN"`으로 바로 시작(L255‑274). 시작 코인 = `SlotV2Engine.character("novice").startCoins`.
- **웹 선택 핸드셰이크**: `SlotV2WebService.consumeWebPick()`으로 웹에서 캐릭+머신(+선택적 장치)을 미리 골라뒀다면 `CHAR_SELECT`/`MACHINE_SELECT`/`DEVICE_SELECT` 단계를 전부 건너뛰고 `proceedAfterMachine`으로 직행(L170‑181, L277‑292). Unity 이식 시 완전히 제거 대상(§10).
- **"같은조합" 재도전**(`restartSameComboReply`, L325‑364): `SlotV2ScoreRow.lastCombo`(CSV `"char,machine,device,device2"`)를 읽어 해금 상태가 여전히 유효하면 그대로 `launchRun` 직행. 장치만 면허가 만료됐으면 그 슬롯만 비우고 진행("장치는 그 슬롯만 미장착"), 캐릭/머신이 미해금이면 전체 거부.

### 1‑C. 런 세션 상태 필드 전부 (`SlotV2RunRow`, `data/SlotV2Entities.kt:14‑79`)

Room 엔티티 1행 = 플레이어 1명의 진행 중인 런(휘발성, PK = `linkId`+`ownerKey`). 아래 전 필드가 "런 상태"다.

| 필드 | 타입 | 기본값 | 의미 |
|---|---|---|---|
| `linkId` | Long | – | 채팅방 식별자 |
| `ownerKey` | String | – | `"u<userId>"` 또는 `"n<nick>"` |
| `ownerNick` | String | – | 닉네임(최신값으로 갱신됨, L158) |
| `ownerUserId` | Long | 0 | 카카오 유저ID |
| `state` | String | "CHAR_SELECT" | §1‑A 참조 |
| `charId` / `machineId` | String | "" | 선택된 캐릭터/머신 id |
| `stage` | Int | 1 | 현재 스테이지 번호(1‑base) |
| `spinIndex` | Int | 0 | 이번 스테이지에 쓴 스핀 수(0‑base 진행 카운터) |
| `stageExp` | Long | 0 | 이번 스테이지 누적 EXP(쿼터 관문) |
| `score` | Long | 0 | 런 누적 점수(리더보드 원점수, 배율 적용 전) |
| `coins` | Long | 0 | 런 한정 화폐(런 종료 시 소멸) |
| `perks` | String(CSV) | "" | 보유 증강/유물 id 목록(영구, 런 내내 유지) |
| `curses` | String(CSV) | "" | 보유 저주 id 목록 |
| `items` | String(CSV) | "" | 🎒가방 보관 아이템 id 목록(최대 3개, `ITEM_SLOTS`, L45) |
| `armItems` | String(CSV) | "" | NEXTSPIN류 아이템 — **다음 스핀 1회**에 적용 후 자동 소거(L629 `armItems=""`) |
| `phaseItems` | String(CSV) | "" | PHASE류 아이템 — **이번 스테이지 내내** 적용, 클리어 시 소거(L877) |
| `stageBonusSpins` | Int | 0 | 이번 스테이지 한정 추가 스핀 수. **스테이지 클리어 시 0으로 리셋**(L877) — 스테이지 간 이월 없음 |
| `usedCmds` | String(CSV) | "" | 이번 스테이지에 쓴 특수스핀명령/장치cmd 마커. 클리어 시 리셋되지만 `RUNSHOP`/`RUNORACLE` 마커만 예외적으로 런 끝까지 보존(L879) |
| `device` | String | "" | 메인 장치 id(모든 종류 가능) |
| `device2` | String | "" | 보조 장치 id(ARMED/PEEK 계열만, 후반 해금) |
| `pendingOptions` | String(CSV) | "" | 현재 선택지 직렬화(캐릭/머신/장치/노드/증강/상점 후보 공용) |
| `flameNext` | Boolean | false | 다음 스핀 EXP -50%(불꽃 디버프 예약) |
| `seedNext` | Boolean | false | 다음 스핀 씨앗 성장 예약 |
| `lastCells` | String(CSV) | "" | 직전 스핀 원시 심볼id — 재굴림/고정/복사/교체/재시험의 원본 |
| `lastGain` / `lastScoreGain` / `lastCoinGain` | Long/Long/Int | 0 | 직전 스핀이 더한 EXP/점수/코인(조작 시 되돌림용) |
| `lastSet4` / `lastAdjPairs` | Int | 0 | 직전 스핀이 `runSet4`/`runAdjPairs`에 더한 기여(0/1) — 재굴림/조작 net-adjust용 |
| `lastSpinNo` | Int | -1 | 직전 스핀의 `spinIndex`(0‑base), -1=없음 |
| `pendingNextExpMul` | Double | 1.0 | 다음 스핀 EXP 배수 예약(보조 코인투입 등). 적용 후 1.0로 리셋 |
| `lockedNext` | String(CSV) | "" | 예언(PEEK)/timeline_ticket으로 확정된 다음 스핀 원시 심볼id |
| `devCooldown` | Int | 0 | 장치 충전 남은 스테이지 수(주석: "점화"). **본 파일 내에서 set/read 하는 코드가 발견되지 않음** — Engine 쪽 로직으로 추정(§9 참조) |
| `runJackpots` | Int | 0 | 이번 런 잭팟 발생 횟수 |
| `runBestSpin` | Long | 0 | 이번 런 한 스핀 최고 EXP |
| `displayMode` | String | "NORMAL" | SIMPLE(간단)/NORMAL(상세)/CALC(계산) |
| `runSymCounts` | String("id:n,id:n") | "" | 이번 런 심볼 등장수 누적(실패 리포트 최다심볼용) |
| `unluckyGauge` | Int | 0 | 불운 게이지(나쁜 스핀 누적). 최대치(`UNLUCKY_MAX`, Engine 정의) 도달 시 다음 증강/유물 제시가 희귀 등급 보장 |
| `closestClear` | Int | -1 | 이번 런 가장 아슬아슬한 클리어 마진(초과 EXP 최솟값), -1=아직 없음 |
| `survive` | Boolean | false | 보험증서 아이템 — 이번 스테이지 실패 1회 생존권 |
| `debtStages` | Int | 0 | 빚문서 아이템 — 남은 "무보상 스테이지" 수(코인/점수 0) |
| `phasePerks` | String(CSV) | "" | 깨진프리즘 아이템 — 이번 스테이지 한정 임시 perk, 클리어 시 소거 |
| `heldAug` | String | "" | 보류파일(dev_holdfile) 보관 중인 증강 후보 1개 |
| `usedItemThisRun` | Boolean | false | 이번 런 아이템 1회라도 사용 여부(도전 판정용) |
| `runAdjPairs` | Int | 0 | 인접쌍 보너스 발동 횟수(누적) |
| `runPrayWins` | Int | 0 | 기도 성공 횟수(누적) |
| `runLastSpinClears` | Int | 0 | 막스핀 클리어 횟수(누적) |
| `runCloseClears` | Int | 0 | 아슬아슬(부족≤10) 클리어 횟수(누적) |
| `runFastClears` | Int | 0 | 남은스핀≥2 클리어 횟수(누적) |
| `runSet4` | Int | 0 | 세트4+ 발동 횟수(누적) |
| `growthStack` | Int | 0 | 성장일지 스택(0~5, 클리어마다 +1, 상한 5) |
| `snowStack` | Int | 0 | 눈덩이 스택(0~4, 빠른클리어 +1, 보스클리어 -1) |
| `fateBellUsed` | Int | 0 | 운명의종 사용 여부(런 1회 한정) |
| `runUsedCmd` | Int | 0 | 이번 런 특수스핀명령(집중/올인/기도/최후) 사용 여부(1=사용, 런 끝까지 유지) |
| `runRerolled` | Int | 0 | 이번 런 재굴림/조작 장치 사용 여부(1=사용, 런 끝까지 유지) |
| `startedAt` / `lastActionAt` | Long | 0 | 런 생성/마지막 행동 타임스탬프(ms) |

**TTL**: `RUN_TTL_MS = 10 * 60_000L`(10분, L18). `lastActionAt` 기준 10분 경과 런은 다음 입력 시 자동 삭제(L152‑156, `purgeExpired`).

### 1‑D. 영구 저장 (런과 별개, 캐릭터/계정 단위)

- `SlotV2ScoreRow`(`data/SlotV2Entities.kt:113‑130`): `bestScore`/`totalScore`/`runs`/`bestStage`/`bestChar`/`bestMachine`/`lastPlayedAt`/`ownedDevices`(장치 grandfather 목록)/`pinnedChallenge`(고정 도전목표)/`lastCombo`.
- `SlotV2AchRow`(`data/SlotV2Entities.kt:87‑105`): `cherryTotal`/`crownTotal`/`jackpots`/`bossClears`/`lastSpinClears`/`exactClears`/`prismPicks`/`bestStage`/`runs`/`bestScore`/`unlocked`(업적id CSV)/`counters`(확장 카운터 맵 "key:val,key:val" — 장치 사용, 심볼 누적, 보스별 클리어, 빌드도감 플래그 등 전부 여기 저장됨).

---

## 2. 스핀 처리 순서 (밸런스 핵심)

`handleSpin()`(L511‑715) 한 번의 실행 순서. **이 순서를 그대로 이식해야 밸런스가 재현된다.**

1. **모드 판별**: `SPIN_CMDS[cmdOf(t)]` → "N"(일반)/"FOCUS"/"ALLIN"/"PRAY"/"LAST". 매핑에 없으면 무시(L512).
2. **보조 상태 CSV 로드**: `usedCmds`, `armItems`(NEXTSPIN 아이템), `phaseItems`(PHASE 아이템)(L514‑516).
3. **1차 mods 산출(ctx 없음)**: `preMods0 = buildMods(machineId, charId, perks+phasePerks, curses, device)`(L521).
4. **1차 RunCtx 구성**: `preCtx = runCtxOf(run, spinIndex, spinsPerStage(preMods0), qOf(stage, preMods0))`(L522).
5. **2차 mods 산출(ctx 포함)**: `preMods = buildMods(..., ctx=preCtx)`(L523) — ctx조건부 증강(예: 마지막 스핀 판정형 증강)을 평가하기 위해.
6. **유효 스핀 수 산출**: `preEffSpins = effSpins(run, preMods)`(L524).
7. **최종 RunCtx 구성**: `runCtx = runCtxOf(run, spinIndex, preEffSpins, qOf(stage, preMods))`(L525).
8. **베이스 mods 확정**: `baseMods = buildMods(..., ctx=runCtx)`(L526).
9. **FOCUS 보정**: 모드가 FOCUS면 `rareWeightMul *= 0.5`(고점 억제, 안정화)(L527).
10. **아이템 mods 적용**: `mods = applyItemMods(baseMods, arm+phase)`(L528).
11. **패시브 장치 적용**: 메인 장치(`run.device`, 보조는 대상 아님)가 `DevKind.PASSIVE`면 `applyPassiveDevice(mods, deviceId)`(L529‑530).
12. **배율 상한(1차, expMul)**: `hasPrism = perks(영구 보유만, phasePerks 미포함)에 PRISM 티어 존재`(L532) → `capMul = capMulFor(stage, hasPrism)`(L534) → `mods.expMul > capMul`이면 clamp(L535).
    - ⚠️ `hasPrism` 판정이 `perkList(run)`(영구 perks만)만 보고 `phasePerkList(run)`(깨진프리즘 아이템의 임시 프리즘 perk)은 반영하지 않음 — broken_prism 아이템 사용 중에도 상한이 "프리즘 미보유" 기준으로 낮게 유지됨(§10 특이사항).
13. **막스핀 배율 상한(2차, lastSpinExpMul)**: `> 5.0`이면 5.0으로 clamp(L536‑537).
14. **최종 spins/quota 재계산**: `spins = effSpins(run, mods)`, `quota = qOf(stage, mods)`(L538‑539) — **11~13단계에서 아이템/패시브장치로 변형된 `mods` 기준으로 다시 계산**됨(7단계의 `runCtx`가 사용한 `preMods` 기준 값과 다를 수 있음, §10 주의).
15. **특수명령 코인 비용/사용횟수/스핀위치 검증**(L540‑550):
    - `cmdCost = SlotV2Engine.cmdCoinCost(mode, bossStage)` (mode=="N"이면 0, 일반 스핀은 항상 무료)
    - LAST는 `spinIndex == spins-1`(마지막 스핀)에서만 허용, 아니면 거부(코인 차감 없음)
    - 특수모드가 이번 스테이지에 이미 사용됐으면(`mode in used`) 거부
    - `run.coins < cmdCost`면 거부(코인 부족)
    - 이 단계에서 거부되면 **상태 변경/코인 차감 없이** 즉시 메시지 리턴.
16. **셀 결정**:
    - `reel = REEL+1`(dev_subreel 장착 시, 보조릴 패시브) / 그 외 `REEL`(L553).
    - `run.lockedNext`가 비어있지 않으면(예언/timeline_ticket으로 확정된 값) RNG를 **사용하지 않고** `cellsFromIds(lockedNext)`로 그대로 사용(L554‑555).
    - 아니면 `rollRaw(r, mods, reel, seedNext)`로 새로 굴림(L556).
17. **아이템 셀 조작**: `applyCellOps(raw, arm, r)` — NEXTSPIN 아이템의 `eraser`/`wild`/`fake_crown` 종류가 채점 직전에 셀을 in-place로 변경(L557, 주석: "평가 직전").
18. **채점**: `res = evaluate(r, raw, mods, spinIndex, spins, flameNext, capMul)`(L560) — 세트합/잭팟/불꽃보너스/해골수 등 `SpinResult` 산출. capMul을 여기서 **다시 한 번** 적용(총배율 2중 클램프, L559 주석).
19. **모드별 후처리**(L564‑573, `gained = res.exp`에서 시작):
    - FOCUS: `floor = (quota/spins*0.6).toLong()`, 미달이면 `gained=floor`.
    - ALLIN: `res.skulls>=2`면 `gained=0`(폭망), 아니면 `gained*=2`.
    - PRAY: `r.nextInt(100)<8`(8% 확률)이면 `gained*=3`(기적, `prayMiracle=true`); 아니면 `low=(quota/spins*0.5).toLong()` 미달 시 `gained+=25`(정수 가산 보정); 그 외 변화 없음.
    - LAST: `gained = (gained*1.75).toLong()`.
20. **예약 배수 적용**: `pendingNextExpMul != 1.0`이면 `gained *= pendingNextExpMul`(L575) — 19단계 **다음**.
21. **보스 특수룰**: `applyBoss(boss, gained, res, spinIndex, spins, expectedPerSpin, augCount)`(L577‑583) — 20단계 **다음**. 정확 공식은 §2‑A 참조.
22. **패시브 안전벨트**(dev_safe, 메인 슬롯만): `fl = (quota/spins*0.35).toLong()`, 미달이면 `gained=fl`(L585‑586) — 21단계 **다음**.
23. **비상졸업벨 강제클리어**(`"dev_bell" in arm`): `gained = max(gained, (quota-stageExp).coerceAtLeast(0)+1)`로 즉시 클리어 보장 + `destroyDevice=true`(L588‑592) — **모든 가산 로직 중 최후순위**.
24. **불운 게이지 갱신**: `badSpin = !destroyDevice && (gained <= expected*0.4 || res.skulls>=3)`; badSpin이면 게이지 +1(상한 `UNLUCKY_MAX`, Engine 정의)(L594‑599).
25. 상태 반영(`newExp`/`newScore`/`newCoins = coins+res.coins-cmdCost`/`newIdx`), 업적 카운터 누적(L600‑679).
26. **분기**: `newExp >= quota` → `clearStage()`(스핀 도중 남은 스핀이 있어도 즉시 클리어, L680). 아니면 `newIdx >= spins`(스핀 소진) → §3 실패처리 체인(L681‑707). 아니면 저장 후 계속(L708‑714).

### 2‑A. 보스 특수룰 정확 공식 (`applyBoss`, L92‑106)

정수 나눗셈(`* n / m`)을 그대로 유지해야 한다(내림 절삭, 반올림 아님).

```
finals: spinIndex == spins-1 → gained*2          (막스핀 ×2)
        spinIndex == 0       → gained*9/10        (첫스핀 -10%)
        그 외                 → gained (변화 없음)

strict: res.bestSetCount < 3 → gained/2           (콤보없음 ×0.5)
        그 외                 → gained

luck:   셀에 star/crown/wild 중 하나라도 있음 → gained*18/10  (희귀 ×1.8)
        없음                                     → gained*8/10   (노희귀 ×0.8)

grad:   pace = expectedPerSpin * 0.7
        expectedPerSpin>0 && gained<pace:
            augCount<3 → gained*75/100  (빈약빌드 ×0.75)
            그 외        → gained*85/100  (꾸준함부족 ×0.85)
        그 외 → gained (변화 없음)

그 외 boss.id → gained (변화 없음)
```

- `expectedPerSpin = quota/spins`(Double, L579).
- `augCount = perkList(run).size`(영구 보유 perk/relic 총수, L580 — RELIC도 카운트에 포함됨, AUGMENT만이 아님).
- 보스는 5스테이지마다 등장하는 것으로 코드 전반에서 확인됨: `clearedStage % 5 == 0`을 "방금 클리어한 스테이지가 보스"의 판정식으로 사용(L1270‑1271), "한 런 보스 3회 연속 격파 = stage/5(S15 클리어 시 3)"라는 주석(L809‑810)도 5배수를 근거로 함. **다만 `SlotV2Engine.bossFor(stage)`의 실제 구현(정확한 판정식)은 Engine 파일에 있어 이 문서 범위 밖 — 위 추론은 Service.kt 상의 정황 증거일 뿐 원문 확인 필요.**

---

## 3. 스테이지 진행

### 3‑A. 스핀 수 / 쿼터

- `effSpins(run, mods) = (spinsPerStage(mods) + run.stageBonusSpins + bossSpins(stage)).coerceAtLeast(MIN_SPINS)`(L87‑88). `spinsPerStage`/`bossSpins`/`MIN_SPINS`는 Engine 정의값(숫자 자체는 이 파일에 없음).
- `stageBonusSpins`는 **스테이지 클리어 시 0으로 리셋**(clearStage L877) — 이월되지 않는다. 아이템(first_aid +1, double_aid +2), EVENT 이벤트("행운의 바람" +1), 보험증서(+2, 1회), 운명의종(+1, 런 1회)이 전부 이 필드에 가산된다.
- `qOf(stage, mods)`(L80‑85): `SlotV2Engine.quota(stage) * mods.quotaMul * SlotV2Engine.bossQuotaMul(stage) * prop`, 여기서 `prop = (baseSpins+bossSpins)/baseSpins`(보스 스핀 증가분 비례, 보스 아니면 1.0). 최종 `.toLong()` 절삭.

### 3‑B. 클리어 판정

- 스핀 도중 어느 시점이든 `newExp >= quota`가 되는 즉시 클리어(스핀을 다 안 써도 즉시, L680). 대기 없음.
- 클리어 시 `clearStage()`(L820‑984) 실행 — 점수/코인 계산은 §3‑D 참조.

### 3‑C. 실패(폭망) 처리 체인 — `newIdx >= spins`이고 `newExp < quota`일 때(L681‑707), **순서대로** 시도:

1. **운명의종**(`fate_bell` perk, 런 1회 `fateBellUsed==0`): 부족분이 15 이하(`quota-newExp <= 15`)면 자동으로 `stageBonusSpins+1`, 스핀 소진 없이 계속(L683‑687).
2. **보험증서**(`survive==true`, 1회용): `stageBonusSpins+2`, `survive=false`로 소모(L689‑693).
3. **만회 기회(POST_SPIN)**: 메인 장치가 `MANIP` 종류이고 이번 스테이지 미사용(`dev.cmd !in usedCmds`), 또는 캐릭터가 gambler이고 무료재굴림 미사용(`"GREROL" !in usedCmds`)이면 `state="POST_SPIN"`으로 전환, 스핀을 소모하지 않고 직전 결과를 조작할 기회 제공(L694‑705).
4. 위 모두 해당 없으면 `gameOver()`(L706).

**POST_SPIN은 1회성**이다 — `handleManipulator`/`handleGamblerReroll`을 `fromPost=true`로 호출하면 결과가 곧바로 `clearStage` 또는 `gameOver`로 확정되며, 재시도 루프가 없다(L1808‑1809, L1889‑1890). 포기(`"포기"/"넘어가기"/"그만"` 또는 `"0"`)도 즉시 `gameOver`(L1898‑1902).

### 3‑D. 클리어 보상 계산 (`clearStage`, L820‑984)

```
leftSpins = spins - newIdx (음수면 0)
leftover  = newExp - quota (음수면 0)
boss      = isBossStage(stage)
inDebt    = debtStages > 0   (빚문서 아이템 활성 중 — 이번 클리어는 무보상)

clearScore = SlotV2Engine.stageClearScore(stage, leftover, leftSpins, curseCount, boss)   // Engine 정의값

close = 0
  leftover <= 5  → close += 300   "🔥턱걸이+300"
  elif leftover <= 10 → close += 150  "🔥아슬아슬+150"
  (newIdx >= spins) → close += 200 "⏰막판클리어+200"   ※ 위 조건과 배타적이지 않음(둘 다 성립 시 합산)
  streakBonus(stage) > 0 → close += streakBonus  "{stage}연속+{금액}"   // Engine 정의값

overPct = newExp*100/quota  (정수 나눗셈)
grade/gradeBonus:
  overPct>=500 → "💥슬롯파괴자" +1000
  overPct>=300 → "👹괴물" +500
  overPct>=200 → "🌟천재" +250
  overPct>=150 → "🎓장학생" +120
  overPct>=120 → "✨우수" +50
  그 외          → "✅합격" +0

gainedScore = inDebt ? 0 : (clearScore + close + gradeBonus)
clearCoin   = inDebt ? 0 : ((boss ? BOSS_COIN : CLEAR_COIN) + mods.clearCoinBonus)   // BOSS_COIN/CLEAR_COIN은 Engine 정의값
```

### 3‑E. 스테이지 클리어 시 상태 리셋 (L872‑892)

- `stage+1`, `spinIndex=0`, `stageExp=0`.
- 스테이지 스코프 소거: `armItems=""`, `phaseItems=""`, `stageBonusSpins=0`, `phasePerks=""`, `lastCells`/`lastGain`/`lastScoreGain`/`lastCoinGain=0`, `lastSpinNo=-1`, `pendingNextExpMul=1.0`, `lockedNext=""`.
- `usedCmds`는 전부 리셋되지만 `"RUNSHOP"`/`"RUNORACLE"` 마커만 예외적으로 유지(런 끝까지, 도전 판정용).
- `debtStages -1`(하한 0), `devCooldown -1`(하한 0).
- `closestClear = min(현재값 또는 최초, leftover)`.
- `growthStack +1`(상한 5). `snowStack`: 빠른클리어(`leftSpins>=2`)면 +1(상한 4), 보스 클리어면 -1(하한 0) — **두 조건이 겹치면 둘 다 적용**(같은 스핀에서 +1과 -1이 순차 적용, L840‑841).
- 다음 노드 풀: `["RELIC","SHOP","REST","GAMBLE","EVENT"]` + (`nextStage>=6`이면 `"CURSE"`,`"RISK"` 추가) → 무작위 2개 추출 + 필수 `"AUGMENT"` 1개 = 항상 3개 선택지(L868‑871).

### 3‑F. 보스 스테이지 특수 규칙

- **등장 주기**: 5스테이지마다(S5/S10/S15/…, §2‑A 근거).
- **추가 스핀**: `bossSpins(stage)`만큼 스핀 수 증가(Engine 정의값), 쿼터도 그 비율만큼 비례 증가(§3‑A `prop`).
- **EXP 배율 룰**: `applyBoss()`(§2‑A) — 보스마다 4종(`finals`/`strict`/`luck`/`grad`) 개별 룰.
- **보상**: 클리어 코인이 `BOSS_COIN`(Engine 정의값, `CLEAR_COIN`보다 높을 것으로 추정되나 이 파일엔 값 없음).
- **다음 노드 프리즘 확정**: 보스 클리어 직후의 AUGMENT/RELIC 노드는 티어가 무조건 🌈PRISM으로 고정(L1270‑1271, L1306, `bossClear = clearedStage % 5 == 0`).
- **업적 트래킹**: 보스별 클리어(`bossClear_<id>`), 각 보스 "약점 조건" 충족 클리어(`bossCounterClear_<id>` — finals=막스핀클리어, strict=세트3+ 성립, luck=star/crown/wild 등장, grad=메인+보조 장치 둘 다 미장착)(L788‑804).

---

## 4. 상점 (`EVENT_SHOP`)

### 4‑A. 진입/오퍼 생성 (`freshShopOffer`, L1404‑1419)

- 항상 최대 **6칸**: 증강 2 + 유물 2 + 아이템 2, 순서는 셔플됨.
- **프리즘 게이트**: `allowPrism = random < EVENT_PRISM_RATE`(=**0.12**, L1249, L1409) — 이번 갱신에서 증강/유물 칸에 프리즘 티어가 나올지 여부를 한 번만 굴림(증강/유물 공통 적용). `false`면 프리즘 제외 목록에서 채우고, 비프리즘이 부족하면 프리즘 포함 원본으로 폴백(데드엔드 방지, `gatePrism`, L1410‑1414).
- 증강: `pickAugments(r, stage, held, 4, stat)` → `gatePrism()`으로 2개로 추림. 유물: `pickRelics(r, held, 4, stat)` → 동일하게 2개. 아이템: `pickItems(r, 2)`(프리즘 게이트 미적용, 아이템엔 티어 개념 없음).
- 각 후보는 `"A:<id>:<price>"` / `"R:<id>:<price>"` / `"I:<id>:<price>"` 형식으로 `pendingOptions`에 직렬화.

### 4‑B. 가격

- **증강**(`augShopPrice`, L1401‑1403): SILVER=**14**, GOLD=**24**, PRISM=**36** (고정, 스테이지 무관).
- **유물**: 가격은 `Perk.price` 필드(Engine 데이터, 이 파일엔 값 없음).
- **아이템**: 가격은 `Item.coinCost` 필드(Engine 데이터, 이 파일엔 값 없음).

### 4‑C. 리롤 (`SHOP_REROLL`, L1400, 처리 L1633‑1639)

- 고정 **6코인**. 사용 횟수에 따라 증가하는 규칙 **없음**(정액제) — 트리거는 `"리롤"/"새로고침"/"새로"/"다시"/"리롤하기"/"갱신"` 텍스트 또는 목록 마지막 번호(`entries.size+1`).
- 리롤 후에도 상점 상태(`EVENT_SHOP`)는 유지, 새 6칸이 다시 생성됨.

### 4‑D. 구매 처리 (L1641‑1681)

- 번호 선택 시 `entries[c-1]`의 비용을 즉시 코인에서 차감. 코인 부족 시 거부(상점 유지, 재시도 가능).
- 아이템(`I:`) 구매 시 가방(`items`) 여유칸(`ITEM_SLOTS=3`) 확인 — 가득 차면 구매 거부(먼저 아이템을 써야 함, L1654‑1658).
- 증강/유물(`A:`/`R:`) 구매는 **즉시 `run.perks`에 영구 추가**(선택 노드와 동일 효과, 대기 없음). 아이템 구매는 가방에 보관만 하고 **즉시 발동하지 않음** — 나중에 `"아이템 N"`으로 스핀 중에만 사용.
- 구매 성공 시 `usedCmds`에 `"RUNSHOP"` 마커 기록(런 끝까지 유지, "검소한졸업" 무상점 도전 판정용).
- 구매 후에도 상점은 자동으로 닫히지 않음 — 구매한 항목만 목록에서 제거되고 계속 쇼핑 가능. `"0"` 입력으로만 나가서 `SPIN`으로 복귀.

### 4‑E. 판매

- **판매(sell) 기능은 코드 전체에서 존재하지 않는다.** 획득한 유물/증강/아이템을 코인으로 환전하는 경로는 없음(확인 완료).

---

## 5. 노드/이벤트 시스템 (`NODE_SELECT` → `handleNodeSelect`, L1125‑1246)

스테이지 클리어 시 **항상 3개**의 노드 선택지가 제시된다(§3‑E). 노드 종류별 처리:

| 노드 | 라벨(L1008‑1018) | 처리 | 이후 상태 |
|---|---|---|---|
| `AUGMENT` | ✨증강 — 무료 증강 1개 선택 | `offerPerks(run,"AUGMENT",...)`(L1257‑1316) | `EVENT_AUGMENT`(풀 소진 시 EVENT 테이블 폴백) |
| `RELIC` | 🛡️유물 — 무료 유물 1개 선택 | `offerPerks(run,"RELIC",...)` | `EVENT_RELIC`(풀 소진 시 EVENT 테이블 폴백) |
| `SHOP` | 🛒상점 — 코인으로 유물·아이템 구매 | §4 참조 | `EVENT_SHOP` |
| `REST` | 🛌휴식 — 코인 +8 | `coins += 8`(L1192) | 즉시 `SPIN` |
| `GAMBLE` | 🎲도박장 — 코인 전부 50%로 2배/소멸 | 코인 0이면 불발(무변화), 아니면 `r.nextBoolean()`으로 2배 또는 전액 소멸(L1193‑1197) | 즉시 `SPIN` |
| `EVENT` | 🎁이벤트 — 랜덤 보상 | §5‑A 10종 랜덤 테이블 | 즉시 `SPIN` |
| `CURSE`(nextStage≥6만) | 🌑저주 — 저주 1개 + 코인 +15 | 미보유 저주 무작위 1개 + `coins+15`(L1141‑1157) | 즉시 `SPIN`(풀 소진 시 EVENT 테이블 폴백) |
| `RISK`(nextStage≥6만) | 🎲위험한 거래 — 프리즘 증강 + 저주 동시 | 미보유 PRISM 증강(소진 시 GOLD 폴백) + 미보유 저주, 둘 다 성공해야 지급(L1159‑1174) | 즉시 `SPIN`(둘 중 하나라도 실패 시 EVENT 테이블 폴백) |

### 5‑A. EVENT 랜덤 테이블 (`r.nextInt(10)`, L1198‑1233) — AUGMENT/RELIC/CURSE/RISK 풀 소진 시 공통 폴백으로도 사용됨

| roll | 결과 |
|---|---|
| 0 | 코인 +15 ("동전 무더기") |
| 1 | 점수 +200 |
| 2 | 코인 +30 ("금화 발견") |
| 3 | 점수 +100 & 코인 +12 ("겹경사") |
| 4 | `stageBonusSpins +1` ("행운의 바람") |
| 5 | NEXTSPIN류 아이템 무작위 1개 무료 지급(`armItems`에 즉시 편성). 풀 없으면 코인 +15 |
| 6 | 코인 +15 (구버전엔 장치 드롭이었으나 폐지, 코인으로 대체됨 — 주석 L1207) |
| 7 | 미보유 유물 무작위 1개 무료 지급(해금분만). 없으면 코인 +25 ("유물 다 모음") |
| 8 | 미보유 증강 무작위 1개 무료 지급. **25% 확률**(`r.nextInt(4)==0`)로 유물도 동시 지급 + 코인 +10 ("🎉특별 이벤트"). 증강 없으면 코인 +25 |
| 9(else) | 보유 저주가 있으면 무작위 1개 해소("정화의 샘"). 없으면 코인 +10 |

### 5‑B. 증강/유물 티어 결정 (`offerPerks`, L1257‑1316)

- `clearedStage = run.stage - 1`(이미 다음 스테이지로 증가된 상태이므로 역산).
- `bossClear = clearedStage % 5 == 0` → 프리즘 확정.
- `baseTier = tierForClearedStage(clearedStage)`(Engine 정의: 주석상 "5마다 프리즘·3마다 골드·그외 실버, 겹치면 프리즘").
- `nodeTier` 결정 순서: 보류파일 사용 중(`heldAug`)이면 그 티어 고정 → 아니면 10% 확률로 `tierUp(baseTier)`(한 단계 상승, "행운! 등급업") → 아니면 `baseTier`.
- `pickPerksByTier(...)`로 3개 후보 산출. `forceRare = (unluckyGauge >= UNLUCKY_MAX)`면 강제로 고티어(불운 보상), `favoredCat`은 dev_major 장치 장착 시 보유 perk 기준 주력 심볼로 편향.
- 보류 후보가 있으면 그 perk를 후보 목록 맨 앞에 강제 삽입(최대 3개로 자름).
- **세트 시너지 주입**: 보류파일 미사용 시 5% 확률로 마지막 칸을 "플레이어가 짓는 중인 세트의 빠진 조각"으로 교체(다른 티어일 수 있음, L1287‑1295).

### 5‑C. 보조 명령(장치 필요)

- **보류**(`dev_holdfile`, EVENT_AUGMENT 전용): `"보류 N"`으로 후보 1개를 즉시 획득하지 않고 `heldAug`에 보관, 다음 증강 노드에서 새 후보와 비교(L1336‑1351). 보류 슬롯은 1개뿐(이미 있으면 거부).
- **재추첨**(`dev_retake`): `"재추첨"`으로 후보를 다시 뽑음. 비용 `RETAKE_COIN_COST`(Engine 정의값), 스테이지당 1회(`usedCmds`의 `"RETAKE"` 마커). **EVENT_AUGMENT/EVENT_RELIC 둘 다 사용 가능**(L1355‑1356)이나, UI 힌트 텍스트(`perkAuxHint`)는 AUGMENT 노드에서만 노출됨(L1327‑1333) — 유물 노드에서도 실제로는 작동하지만 안내가 없는 비대칭(§10 특이사항).

---

## 6. 코인 경제

### 6‑A. 획득처

| 출처 | 금액 | 근거 |
|---|---|---|
| 스핀 심볼(코인 심볼 등) | `res.coins` | Engine `evaluate()` 결과, 값 정의는 Engine에 있음 |
| 스테이지 클리어 | `(boss?BOSS_COIN:CLEAR_COIN)+mods.clearCoinBonus` (빚문서 중이면 0) | §3‑D, 상수는 Engine |
| REST 노드 | +8 | L1192 |
| GAMBLE 노드 | 보유 코인 전액 2배(50%) 또는 전액 소멸(50%) | L1193‑1197 |
| EVENT 노드/폴백 | +15/+30/+12/+15/+25/+25/+10 (분기별, §5‑A 표) | L1198‑1233 |
| CURSE 노드 | +15(저주 획득과 동시) | L1147 |
| 아이템 `old_coin` | +6 | L1451 |
| 아이템 `mini_coupon` | +9 | L1455 |
| 아이템 `price_hack` | +18 | L1456 |
| 아이템 `debt_note` | +30(단, `debtStages=4`로 향후 4회 클리어 무보상) | L1463 |
| 아이템 `black_lottery` | 50% 확률로 실패 시(유물 폴백) +15, 성공(저주 획득) 시 코인 변화 없음 | L1464‑1476 |
| 아이템 `devil_contract` | +25(유물/저주 지급 성패와 무관하게 항상) | L1477‑1487 |

### 6‑B. 사용처

| 용도 | 비용 | 근거 |
|---|---|---|
| 특수 스핀명령(집중/올인/기도/최후) | `cmdCoinCost(mode, bossStage)` — Engine 정의값. 보스 스테이지에서 가산(안내문 "보스 +1", L41). 일반 스핀은 항상 무료 | L542, L548‑549 |
| 상점 증강 | SILVER 14 / GOLD 24 / PRISM 36 | L1401‑1403 |
| 상점 유물/아이템 | `Perk.price`/`Item.coinCost`(Engine 정의) | L1416‑1417 |
| 상점 리롤 | 6(정액, 증가 없음) | L1400 |
| 재추첨(`dev_retake`) | `RETAKE_COIN_COST`(Engine 정의값) | L1359 |
| 🪙투입(`dev_coin`, 메인) | 5 → 다음 스핀 EXP +30% | L1724‑1731 |
| MANIP 장치(`dev_reroll`/`dev_pin`) | 3 | L1822‑1826 |
| MANIP 장치(`dev_copy`/`dev_swap`) | 5 | L1822‑1826 |

- **코인은 런 한정 화폐**다 — 영구 저장(`SlotV2ScoreRow`/`SlotV2AchRow`)에는 코인 값이 남지 않고, `gameOver()` 시 런 행 자체가 삭제됨(L2051)과 함께 소멸. 튜토리얼 텍스트(L2565)도 "런 안에서만 쓰는 화폐(런 종료 시 소멸)"라고 명시.

---

## 7. 명령어 목록

### 7‑A. 런 시작

| 명령 | 조건 | 효과 |
|---|---|---|
| `"잭팟"` / `"jackpot"` | 진행 중인 런 없음(또는 TTL 만료) | 새 런 시작(`startRun`) |
| `"잭팟"` / `"jackpot"` (SPIN/POST_SPIN 상태) | 진행 중인 런 있음, `SPIN` 상태 | 일반 스핀(모드 N, 무료) |
| `"스핀"` / `"spin"` | `SPIN`/`POST_SPIN` 상태에서만 유효 — **런을 새로 시작할 수는 없음**(`START_TOKENS`엔 미포함, L27) | 일반 스핀(모드 N) |
| `"같은조합"` / `"잭팟재도전"`(별도 진입점, `handleInput` 경유 아님) | 진행 중 런 없음, 직전 조합 해금 유효 | 직전 캐릭/머신/장치 조합으로 즉시 재시작 |

### 7‑B. 스핀 (state=SPIN에서만, `POST_SPIN`에서는 사용 불가)

| 명령 | 비용 | 사용 제한 | 효과 |
|---|---|---|---|
| 🎯 `"집중"`(FOCUS) | `cmdCoinCost("FOCUS",boss)` | 스테이지당 1회 | 결과 편차 축소(`rareWeightMul×0.5`) + 최소 EXP 보장(쿼터/스핀수×0.6 미달 시 그 값으로 상향) |
| 🎲 `"올인"`(ALLIN) | `cmdCoinCost("ALLIN",boss)` | 스테이지당 1회 | EXP ×2, 단 ☠해골 2개↑면 EXP=0 |
| 🙏 `"기도"`(PRAY) | `cmdCoinCost("PRAY",boss)` | 스테이지당 1회 | 8% 확률 EXP×3("기적"), 아니면 (쿼터/스핀수×0.5) 미달 시 +25 고정 가산 |
| ⏰ `"최후"`(LAST) | `cmdCoinCost("LAST",boss)` | 스테이지당 1회 **+ 반드시 마지막 스핀에서만** | EXP ×1.75 |

- 4종 모두 코인은 **판정 전에 즉시·환불불가 차감**(L603) — 올인 폭망(EXP=0)이어도 코인은 돌려주지 않음.
- 위 모드로 스핀할 때도 장치/아이템 훅은 그대로 전부 적용된다(§2 순서 동일, 모드 후처리만 추가).

### 7‑C. 장치 (`DEVICE_CMDS`, 장착된 장치의 `.cmd` 문자열, Engine 정의) — `handleDevice`/`handleManipulator`/PEEK 분기 (L1684‑1913)

| 종류(DevKind) | 예시(id) | 비용 | 제한 | 효과 |
|---|---|---|---|---|
| PASSIVE(자동, 명령 없음) | dev_subreel(릴+1칸), dev_safe(최소보장 35%) | 0 | 메인 슬롯 전용(보조엔 배정 불가, §9) | 매 스핀 자동 적용 |
| ARMED(장전형) | dev_coin(🪙투입) | 5 | 스테이지당 1회, 메인은 다음 스핀 EXP+30%, 보조는 약화판(`pendingNextExpMul`) | 다음 스핀 1회 발동 |
| ARMED(비상형) | dev_bell(🔔비상) | 0 | 부족 EXP ≤25일 때만 장전 가능, 스테이지당 1회 | 다음 스핀에 강제 클리어 + **메인 슬롯 장치 파괴**(`run.device=""`, §9) |
| PEEK(예언형) | dev_oracle(🔮예언) | 0 | 스테이지당 1회(메인/보조 공용, 보조 약화 없음) | 다음 스핀 결과를 미리 굴려 확정(`lockedNext`) |
| MANIP(직전결과 조작, 메인 전용) | dev_reroll(🔄재굴림), dev_pin(📌고정 N), dev_copy(📑복사 N), dev_swap(🔃교체 N) | 재굴림/고정 3, 복사/교체 5 | 스테이지당 1회, 스핀 소모 없음, EXP ×0.9 페널티 | 직전 스핀 결과 재계산 |
| 도박꾼 전용(장치 무관) | `"재굴림"` (charId=="gambler") | 0 | 스테이지당 1회(`GREROL` 마커), 점수 패널티 없음 | 직전 스핀 전체 재굴림 |

- `dev_pin`/`dev_copy`/`dev_swap`은 인자 필요(`"고정 3"` 형태, `argOf()`로 숫자 파싱). 없으면 거부.
- `dev_swap`은 셀을 "현재 셀 중 값심볼(cherry/book/star/gem/crown) 최다 종류"(`bestValueId`)로 교체, 동점/없음이면 `"star"` 폴백(L1851‑1852).
- `dev_copy`는 지정 셀을 오른쪽 인접 셀에 복사(오른쪽 끝이면 왼쪽으로, L1849).
- MANIP/도박꾼재굴림은 `POST_SPIN`에서도 사용 가능(만회용, L1904‑1908).

### 7‑D. 아이템 (`ITEM_CMDS = {"아이템","가방","사용","인벤"}`)

| 명령 | 상태 제한 | 효과 |
|---|---|---|
| `"아이템"`/`"가방"`(인자 없음) | 어디서든 | 가방 목록 표시(소모 없음) |
| `"아이템 N"` | **SPIN 상태에서만** | N번 아이템 사용(NEXTSPIN/PHASE/INSTANT 종류별 처리, §5‑C·아래 §7‑E) |

- 즉시클리어형 아이템(`isInstantClearItem`, Engine 정의 판정)은 스테이지당 1회(`"ICLEAR"` 마커).
- `retake_form`(📄재시험)은 특수 처리: 직전 스핀 **전체**를 완전히 다시 굴림(즉시효과가 아니라 handleItem 내 별도 분기, L1550‑1575). 직전 스핀이 없으면 사용 불가.

### 7‑E. 즉시효과(INSTANT) 아이템 수치 (`applyItemPurchase`, L1441‑1517)

| id | 효과 |
|---|---|
| `first_aid` | `stageBonusSpins+1` |
| `double_aid` | `stageBonusSpins+2` |
| `cram` | `stageExp += instantQuota*15/100` |
| `cheat_sheet` | `stageExp += instantQuota*30/100` |
| `answer_sheet` | `stageExp += instantQuota*50/100` |
| `honor_roll` | `stageExp += instantQuota*70/100` |
| `grad_cert` | `stageExp += instantQuota`(즉시 100%, 사실상 즉시클리어) |
| `dev_battery` | `armItems`에 `"dev_coin"` 추가(다음 스핀 EXP+30% 레버 재사용) |
| `score_sticker` | `score+150` |
| `old_coin` | `coins+6` |
| `grad_copy` | `stageExp += instantQuota*80/100` **및** `score = (score*9/10).coerceAtLeast(0)`(점수 10% 페널티) |
| `score_calc` | `score += score*30/100` |
| `mini_coupon` | `coins+9` |
| `price_hack` | `coins+18` |
| `grad_ring` | 부족분(`instantQuota-stageExp`)이 0~20 범위면 즉시 100% 채움(즉시클리어), 범위 밖이면 무효과 |
| `gold_grad_bell` | 부족분이 0~50 범위면 즉시 100% 채움, 범위 밖이면 무효과 |
| `insurance_cert` | `survive=true` |
| `debt_note` | `coins+30`, `debtStages=4` |
| `black_lottery` | 50%: 미보유 GOLD 유물 무작위 1개 지급(해금분만, 없으면 `coins+15`) / 50%: 미보유 저주 무작위 1개 지급(없으면 무효과) |
| `devil_contract` | 미보유 유물(해금분, 등급무관) 1개 + 미보유 저주 1개(각각 있으면 지급, 없으면 스킵) + 항상 `coins+25` |
| `broken_prism` | 안전 프리즘 목록(`overdrive`/`supernova`/`wild_world`/`joker`/`seed_garden`/`great_harvest`/`jackpot`/`mega_jackpot`/`gamblers_dice`/`key_master`/`time_warp`) 중 미보유·해금분에서 무작위 1개를 `phasePerks`(이번 스테이지 한정)로 부여. 스핀수를 바꾸는 프리즘(short_day/glass_cannon/all_in/endgame_rush)은 제외. 폴백: 해금분 없으면 미보유 안전목록 → 그마저 없으면 `"overdrive"` 하드코딩 |
| `timeline_ticket` | 다음 스핀 분포로 후보 2개를 미리 굴려 EXP가 더 높은 쪽을 `lockedNext`로 확정(PEEK와 동일 메커니즘) |
| `retake_form` | 여기선 무효과(즉발 아님) — 실제 로직은 `handleItem` 내 별도 분기(§7‑D) |

`instantQuota(run)`(L1430‑1436)은 `clearStage`와 동일한 실제 쿼터(phase 아이템의 quotaMul 변경분까지 반영)를 반환 — 위 %계산의 기준값.

### 7‑F. 진행/시스템 공용 명령

| 명령 | 범위 | 효과 |
|---|---|---|
| 숫자 `"0"`~`"9"`, `"N번"`, `"나가기"`, `"패스"`(`parseChoice`, L203‑208) | 선택지가 있는 모든 상태 | 해당 번호 선택(0=나가기/거절/미장착 등 상태별 의미 다름) |
| `"상태"` / `"잭팟상태"` | 어디서든 | 현재 진행 요약(`statusReply`) |
| `"간단"`/`"상세"`/`"보통"`/`"계산"`/`"고급"`(`DISPLAY_CMDS`) | 어디서든 | 표시 모드 전환(SIMPLE/NORMAL/CALC), 턴 소모 없음 |
| `"포기"`/`"넘어가기"`/`"그만"`(`GIVEUP`) | 주로 POST_SPIN | 즉시 게임오버 |

### 7‑G. 조회성 명령 (run 상태와 무관, `handleInput` 라우팅 밖의 별도 public 함수 — 상위 라우터가 직접 호출하는 것으로 추정, 이 파일엔 텍스트 매칭 코드 없음)

`잭팟튜토리얼 [N]`(`tutorialText`, 3페이지), `잭팟도움말`(텍스트만 참조되고 구현 함수는 이 파일에 없음 — 범위 밖), `도전`/`잭팟도전`(`challengeText`), `목표 N`(`pinChallenge`), `숙련`/`잭팟숙련`(`masteryText`), `기록`/`잭팟기록`(`recordText`), `빌드도감`/`잭팟빌드`(`buildDexText`), `장치면허`(`licenseText`), 관리자용 `statsText`/`topByBest`/`archiveSeason`.

---

## 8. 점수/랭킹/기록

### 8‑A. 최종 점수 계산 (`gameOver`, L2004‑2006)

```
finalScore = (run.score(런 누적 원점수) * SlotV2Engine.scoreModifier(machineId, charId)).toLong()
```
- `run.score`는 스핀 채점(`res.score`)과 §3‑D의 클리어 보상(`gainedScore`)이 누적된 값 — 배율(`scoreModifier`)은 **런 종료 시점에 딱 한 번만** 곱해진다(도중엔 원점수 그대로 누적).
- `scoreModifier` 값 자체는 Engine 정의(캐릭터 `scoreMod` × 머신 `scoreMod`로 추정되나 확인 안 됨, 범위 밖).

### 8‑B. 영구 기록 갱신 (`recordRun`, L2165‑2189 — `SlotV2ScoreRow` 갱신)

- `bestScore = max(기존, finalScore)`.
- `totalScore += finalScore`(통산 누적).
- `runs += 1`.
- `bestStage = max(기존, run.stage)`.
- `bestChar`/`bestMachine`: **`finalScore >= 기존 bestScore`일 때만** 이번 런의 캐릭/머신으로 갱신(신기록급 런만 "베스트 빌드"로 채택).
- `ownedDevices`(grandfather 장치 목록): 그대로 보존(점수와 무관).
- `pinnedChallenge`: 그대로 보존.
- `lastCombo = "charId,machineId,device,device2"`: **점수와 무관하게 매 런 종료마다 무조건 덮어씀**("같은조합" 재도전용).

### 8‑C. 업적 카운터 (`bumpAch`, L1930‑1963) — `SlotV2AchRow` 갱신

- `gameOver` 시 `runDone=1`, `stageReached=run.stage`, `scoreReached=finalScore`로 1회 호출(L2048‑2049).
- `inc`(가산) / `setMax`(최댓값 갱신) 맵을 병합해 `counters` CSV에 누적. 새로 임계값(threshold)을 넘긴 업적은 `unlocked`에 추가되고 즉시 팝업 배너로 안내(`achBanner`).
- 게임오버 시점 전용 추적: `prayFails`(기도 사용했는데 실패), `devicesOwned`/`curseMax`/`relicsMax`/`cstage_*`/`mstage_*`/빌드도감(`bc_*`)/`KEY_MAX_RUN_JACKPOTS` 등을 **클리어 못한 런도** setMax로 반영(도달 기준, L2010‑2027).

### 8‑D. 신기록 안내 (L2059‑2076)

- `newBest = finalScore > priorBest`, `newStage = run.stage > priorStage` — 게임오버 메시지 최상단에 "🎉🎉신기록!" 배너로 독립 안내(둘 다 해당하면 둘 다 표시).

### 8‑E. 시즌/명예의전당

- 이 파일 안에는 `archiveSeason(linkId, key, label, tsMs)`(L2472‑2473) 하나만 존재하며, 실제 로직은 `SlotV2WebService.archiveSeason`으로 **전부 위임**된다. 시즌 스냅샷/명예의전당 테이블 구조, 시즌 전환 조건 등은 **이 파일 범위 밖**(SlotV2WebService.kt 별도 추출 필요).

### 8‑F. 조회 기능

- `challengeText`/`pinChallenge`: 상시 도전판(리셋·만료 없음). 진행도는 저장되지 않고 매번 `stat` 맵(업적+점수 합성)에서 즉석 계산(`reqProgress`). 고정목표 1개만 `SlotV2ScoreRow.pinnedChallenge`에 저장.
- `masteryText`: 캐릭터별 `cstage_<id>`/머신별 `mstage_<id>`(도달 최고 스테이지)를 동/은/금 메달 기준(Engine 상수 `MEDAL_BRONZE_S`/`SILVER_S`/`GOLD_S`)으로 표시.
- `recordText`: `SlotV2Engine.recordLines(stat)`(Engine 정의 라인들) + 이 파일에서 직접 추가하는 4개 라인(무아이템 최고도달 S, 저주5↑ 최고도달 S, 저주3↑ 보스클리어 횟수, 무상점 S10 클리어 횟수) + 빌드도감 진행률.
- `buildDexText`: (a) 캐릭+머신 조합별 최고 도달 스테이지(`bcKey`, 최대 30개 표시 후 "…외 N개"), (b) 테마 빌드(`bld_*`) 완성 여부를 `THEME_BUILD_CATEGORIES`별로 그룹 표시.

---

## 9. 장치 쿨다운/파괴 규칙

### 9‑A. "쿨다운"의 두 가지 서로 다른 개념

1. **스테이지당 1회 제한**(실질적 쿨다운): 능동/조작/예언 장치는 `run.usedCmds`에 `dev.cmd` 문자열이 기록되면 그 스테이지 동안 재사용 불가. `usedCmds`는 **스테이지 클리어마다 전부 리셋**(`RUNSHOP`/`RUNORACLE` 마커만 예외)되므로, 실질적으로 "스테이지 1회 → 클리어하면 자동 충전"이 유일하게 확인되는 쿨다운 메커니즘이다(§3‑E).
2. **`devCooldown` 필드**(정수, "장치 충전 남은 스테이지"): `clearStage()`에서 매 스테이지 클리어마다 `-1`(하한 0)로 감소만 한다(L883). **이 값을 0이 아닌 값으로 설정하거나(set), 이 값이 0보다 큰지 확인해서 장치 사용을 막는 코드가 `SlotV2Service.kt` 안에 전혀 없다.** 주석상 "점화"(🔥점화, 튜토리얼 텍스트에 언급된 장치)와 연관된 것으로 추정되나, 실제 set/check 로직은 `SlotV2Engine.kt`(buildMods의 ctx 처리 등)에 있을 가능성이 높음 — **이 문서 범위 밖, Engine 쪽에서 반드시 확인 필요**.

### 9‑B. 파괴 규칙

- **유일하게 확인된 파괴 경로**: 🔔비상졸업벨(`dev_bell`)이 장전된 상태(`"dev_bell" in arm`)로 스핀했을 때, 강제클리어 발동과 동시에 `destroyDevice=true`가 되고, 결과 저장 시 `device = if (destroyDevice) "" else run.device`(L630) — **메인 슬롯(`run.device`)만 빈 문자열로 클리어**된다. "이번 런 장착 해제"라는 안내 문구(L1739)와 일치.
- **잠재적 특이사항**: `dev_bell`은 `DevKind.ARMED`이며, `DEVICE_SELECT2` 안내 텍스트(L430)에 따르면 보조 슬롯은 ARMED/PEEK 계열만 후보로 올라올 수 있다 — 즉 `dev_bell`이 **보조 슬롯**(`run.device2`)에 장착되는 것이 규칙상 가능해 보인다. 그런데 파괴 코드(L630)는 `run.device`(메인)만 검사/초기화하고 `run.device2`는 전혀 건드리지 않는다. `dev_bell`이 보조로 장착된 채 발동하면 (a) 메인 장치가 없는데도 `run.device=""`로 재대입되거나(사실상 무변화), (b) 정작 파괴돼야 할 `device2`는 그대로 남는 결함 가능성이 있다. **원문 확인 필요 항목으로 아래 §11에 보고.**
- 그 외 어떤 장치도 스핀 중 "파괴"되지 않는다. 장치는 런 시작 시 1회 장착되면 런이 끝날 때까지(또는 위 dev_bell 파괴 조건 전까지) 유지되며, 재장착/교체 UI는 없다.

### 9‑C. 비용(코인)과 쿨다운의 관계

- MANIP 장치(재굴림/고정/복사/교체)는 "스테이지당 1회" 제한과 "코인 비용"(3 또는 5)이 **동시에** 적용된다 — 쿨다운이 돌아왔어도(다음 스테이지) 코인이 없으면 사용 불가.
- ARMED `dev_coin`(🪙투입)도 마찬가지로 스테이지당 1회 + 5코인.
- PEEK(`dev_oracle`)와 도박꾼 무료재굴림은 코인 비용이 없고 스테이지당 1회 제한만 있다.

---

## 10. C# 이식 시 주의

1. **CSV 문자열 직렬화 전면 재설계**: `perks`/`curses`/`items`/`armItems`/`phaseItems`/`usedCmds`/`runSymCounts`/`pendingOptions` 등은 Room(SQLite) 단일 행 저장 제약 때문에 콤마 CSV로 인코딩된 것일 뿐, 논리적으로는 `List<string>`/`HashSet<string>`/`Dictionary<string,int>`다. Unity 이식 시 CSV 인코딩 자체를 그대로 베끼지 말고 타입이 있는 컬렉션으로 재설계할 것.
2. **`handleSpin`의 mods 3단계 재계산**(§2 3~14단계): `preMods0→preCtx→preMods→preEffSpins→runCtx→baseMods`로 만든 `runCtx`는 ctx조건부 증강 평가용이지만, 실제로 커맨드 검증(LAST 마지막스핀 여부 등)과 최종 결과 계산에 쓰이는 `spins`/`quota`는 **그 이후 아이템/패시브장치를 반영한 `mods`로 다시 계산**된다(L538‑539). 이 "3중 계산·부분적 불일치" 순서를 정확히 재현하지 않으면 스핀수 변경 아이템/증강의 경계 스핀(마지막 스핀 여부 등)에서 원작과 다른 판정이 나올 수 있다.
3. **총배율 2중 클램프**: `capMul`은 (a) `evaluate()` 호출 전에 `mods.expMul`을 한 번 클램프하고, (b) `evaluate()` 내부에도 인자로 전달돼 세트/불꽃/처음·끝 스핀 배율까지 합산한 총배율을 다시 클램프한다(L533‑535, L560 주석 "2중"). 둘 중 하나만 구현하면 고배율 빌드의 상한이 의도보다 높아진다.
4. **정수 절삭(내림) 연산을 그대로 유지**: 보스 룰(`gained*9/10`, `gained/2`, `gained*18/10` 등), MANIP 페널티(`gained*0.9`), 각종 아이템 %(`instantQuota*15/100` 등)는 전부 Kotlin의 정수/실수 절삭(`toLong()`은 0방향 절삭, 반올림 아님) 규칙을 따른다. C#의 `(long)` 캐스트도 0방향 절삭이라 연산 순서(먼저 곱하고 나누기)만 그대로 지키면 값이 일치하지만, 절대 `Math.Round`류로 치환하지 말 것.
5. **코인은 판정 전 선차감·환불불가**(§7‑B): 특수 스핀명령은 코인을 굴리기도 전에 차감하며, 올인 폭망(EXP=0)이어도 환불되지 않는다. "실패하면 돌려주자" 같은 UX 개선을 임의로 추가하면 밸런스가 달라진다.
6. **POST_SPIN은 1회성 분기이지 반복 루프가 아니다**(§3‑C): 마지막 스핀 실패 후 만회 시도는 정확히 1번만 허용되고, 그 결과로 반드시 클리어 또는 게임오버로 귀결된다. 재시도 루프를 열어두면 무한 만회가 가능해진다.
7. **재굴림/조작의 net-adjust 패턴**: `handleGamblerReroll`/`handleManipulator`는 직전 스핀의 기여분(`lastGain`/`lastScoreGain`/`lastCoinGain`/`lastSet4`/`lastAdjPairs`)을 빼고 새 결과를 더하는 방식으로 "직전 스핀 1개를 교체"한다. 이 필드들은 정확히 "직전 스핀 1개분"만 담을 수 있으므로, 연속 조작을 허용하지 않는 "스테이지당 1회" 제한과 반드시 함께 이식해야 한다(그렇지 않으면 이중 차감 버그가 재현됨).
8. **RNG는 매 호출마다 새로 생성됨**: `rng() = Random(System.nanoTime())`(L69)이 파일 전역에서 스핀/노드추첨/이벤트굴림마다 매번 새로 인스턴스화된다. 런 단위 시드나 재현 가능한 난수열은 존재하지 않는다 — 그대로 이식하려면 "매 굴림마다 새 시드"를 명시적으로 구현하고, 만약 Unity에서 리플레이/시드 고정 기능을 원한다면 이는 **새로운 설계**가 필요하다(원작엔 없는 기능).
9. **`dev_bell` 파괴가 메인 슬롯만 초기화**(§9‑B) — 보조 슬롯 장착 시 결함 가능성. Unity 설계 시 "원작 그대로 재현"이 요구사항이면 이 특이 동작까지 재현할지, 버그로 보고 수정할지 기획 결정이 필요.
10. **`devCooldown` 필드의 실제 사용처가 이 파일에 없음**(§9‑A) — Engine 쪽 로직을 반드시 확인한 뒤 이식해야 하며, 이 문서만으로는 "점화" 장치의 충전 규칙을 완성할 수 없다.
11. **`hasPrism` 판정이 임시 프리즘(phasePerks)을 무시**(§2 12단계) — `broken_prism` 아이템으로 얻은 임시 프리즘 효과는 배율 상한 완화(`capMulFor`의 hasPrism 인자)에 반영되지 않는다. 의도된 디자인인지 누락인지 원작 기획자 확인 필요.
12. **카카오톡 채팅 특유 UX — Unity에서 재설계 필요한 부분**(§11에 정리, 요약만 여기 기재): 자유텍스트 명령 별칭/오탈자 허용, 숫자·"N번"·"나가기"/"패스" 혼용 선택 파싱, 두 번째 메시지(`Reply.Msg.detail`)로 상세설명 분리 전송, 댓글(답글) 기반 상태 추적, 웹 선택 핸드셰이크(`SlotV2WebService`), 카카오 유저ID/닉네임 앙커링(`resolveUid`), 런 TTL 자동 만료(10분)와 매 액션마다의 DB round-trip 저장 패턴.

---

## 11. 발견한 모호/특이 사항 요약 (보고용, 원문 대사는 위 각 절 참조)

- 파일 실제 길이가 작업 지시(2,437줄)와 다름(실측 2,591줄) — 전체를 다 읽었는지 항상 `wc -l`로 재확인 권장.
- `SlotV2RunRow.state` KDoc 주석이 실제 코드와 불일치(`EVENT_ITEMSHOP`/`EVENT_GAMBLE`/`EVENT_REST`/`EVENT_CURSE`는 실재하지 않는 state).
- `dev_retake`(재추첨)는 유물 노드에서도 실제로 동작하지만 UI 힌트 텍스트는 증강 노드에서만 노출됨(비대칭, 버그 후보).
- `dev_bell` 파괴 로직이 메인 슬롯만 초기화 — 보조 슬롯 장착 시 결함 가능성(§9‑B, §10‑9).
- `hasPrism`(총배율 상한 완화 판정)이 영구 perks만 보고 임시 phasePerks(broken_prism 효과)를 무시함 — 의도/누락 불명(§10‑11).
- `devCooldown` 필드는 감소만 하고 set/check 코드가 이 파일에 없음 — Engine 쪽 확인 필수(§9‑A).
- `.toLong()`/정수 나눗셈 등 절삭 연산이 전 파일에 걸쳐 일관되게 쓰임(반올림 없음) — 이식 시 연산 순서까지 동일하게 유지해야 값이 일치.
- 코인 이벤트 case 6은 주석상 "구버전 장치 드롭 자리"였고 현재는 단순 코인 지급으로 대체(레거시 흔적).

---

## 12. Unity에서 재설계가 필요한 "카톡 채팅 특유" 요소 목록 (보고용)

1. **자유텍스트 명령 파싱 전체**: `norm()`(선행 "." 제거), `cmdOf()`/`argOf()`(숫자·공백 분리), 명령어 다국어/오탈자 동의어 집합(`SPIN_CMDS`, `DISPLAY_CMDS`, `REROLL_WORDS`, `GIVEUP` 등) — 버튼 UI로 대체.
2. **`parseChoice`의 숫자/"N번"/"나가기"/"패스" 혼용 파싱** — 리스트 UI의 클릭 선택으로 대체.
3. **`Reply.Msg(text, detail)` 2단 메시지 구조**(가독성을 위해 상세설명을 "별도 두 번째 카톡 메시지"로 분리 전송, L20‑23) — Unity에선 툴팁/상세 패널로 대체하는 것이 자연스러움.
4. **댓글(답글) 기반 진행**: "모든 진행은 봇 메시지에 댓글로"라는 제약 자체가 카카오톡 스레드 구조 산물 — Unity엔 해당 개념이 없음.
5. **웹 선택 핸드셰이크**(`SlotV2WebService.consumeWebPick`/`linkPick`/`sync`) — 별도 웹사이트에서 캐릭/머신/장치를 미리 고르고 채팅으로 이어받는 기능. Unity에선 게임 클라이언트 자체가 UI이므로 통째로 삭제 대상.
6. **`resolveUid` 카카오 유저ID 앙커링**(member/user_points/slot_v2_score 3단계 닉네임 매칭으로 uid 보강, L2480‑2507) — 카카오톡 댓글 경로에서 uid가 0/null로 오는 경우를 보정하는 플랫폼 특유 로직. Unity는 안정적인 플레이어/세이브슬롯 식별자를 쓰므로 전면 삭제 대상.
7. **`RUN_TTL_MS`(10분) 자동 만료·purge**(L18, L152‑156) — 다수 사용자가 공유하는 챗봇 서버 DB의 "방치된 런 정리" 필요성에서 나온 것. 싱글플레이 로컬 세이브엔 해당 개념이 불필요(또는 "일시정지/재개"로 재해석 필요).
8. **매 마이크로액션마다의 DB round-trip**(`track()`/`bumpAch()`가 스핀·장치사용·구매마다 즉시 읽기‑수정‑쓰기, L1965‑1968) — 챗봇 요청/응답 모델의 산물. Unity에선 인메모리 상태 + 체크포인트 저장으로 재설계.
9. **닉네임 변경 실시간 반영**(`run.ownerNick != nick`이면 즉시 갱신, L158) — 카카오톡 닉네임 변경 대응. Unity 계정 시스템엔 불필요.
10. **운영자 전용 텍스트 명령**(`statsText` 대시보드, `archiveSeason`) — 카카오톡 봇 관리자 명령 체계. Unity는 별도 어드민 툴/에디터 확장으로 재설계.
