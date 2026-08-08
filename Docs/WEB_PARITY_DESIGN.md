# WEB_PARITY_DESIGN — Unity 앱을 웹 단독판(`public/play/`)과 동일하게

> 2026-08-07 사용자 지시: **"유니티 게임을 웹페이지에 있는 게임처럼 다 뜯어고쳐줘."**
> 이 문서가 그 전환의 마스터 플랜이다. 이후 모든 슬라이스는 이 문서의 페이즈 번호를 참조한다.

## 0. 정답지 전환 (중대)

- 기존: 게임 규칙의 원본 = `kotlin-reference/`(구버전 카톡 봇 엔진).
- **변경: 규칙·콘텐츠·흐름의 원본 = `public/play/`(웹 단독판 — 모카봇 `web/slot` 이관본).**
  웹 단독판은 kotlin 이후로도 계속 발전해 온 최신 본선이다(심화모드·승천·레벨 등).
- kotlin-reference 는 "웹에 없는 세부가 모호할 때"의 참고로 강등한다.
- 충돌 시 결정 원칙: **웹 채택이 기본.** 웹 쪽이 명백한 회귀 버그(§2 결정 로그에 명시)일 때만 예외.
- 웹 쪽 참조 파일: `data.js`(카탈로그) · `engine.js`(순수 계산) · `game.js`(런 상태머신) · `ui.js`(표시) · `_harness.mjs`(회귀망).

## 1. 전수 차이 요약 (2026-08-07 양측 인벤토리 조사 결과)

### 1-A. 웹에 있고 Unity에 없는 것 (이식 대상)

| # | 시스템 | 규모 | 페이즈 |
|---|---|---|---|
| 1 | 특수스핀 **첫 사용 무료**(런당 종류별 1회, `cmdFreeUsed`) | 소 | **P1** |
| 2 | **첫 판 즉시 시작**(runs==0 → novice+basic, 선택 스킵) | 소 | **P1** |
| 3 | 실패 체인 웹 순서: ①보험증서 ②POST_SPIN ③fate_bell(부족≤15) ④게임오버 | 소 | **P1** |
| 4 | 노드 보상 수치 웹화: REST +12(Unity 8) · CURSE 저주+코인30(Unity 15) · EVENT 6번 분기=장치 획득(Unity 레거시 코인15) · DEVICE 전용 노드(보스 드랍: 장착 or 코인+15) | 소 | **P1** |
| 5 | 자발적 **포기**(스핀 중 즉시 결산, voluntary 플래그) | 소 | **P1** |
| 6 | 클리어 점수 웹 공식: `stage×50 + leftover×2 + leftSpins×100 + boss500 + streak` — **등급보너스/아슬아슬보너스/저주배수 없음**(등급은 연출 전용 6단계) | 중(골든 대량 갱신) | P2 |
| 7 | **capMulFor(총배율 캡) 제거** — 웹은 미적용(심화 specialMul 캡 8.0만) | 중(밸런스+골든) | P2 |
| 8 | 보스 grad(졸업심사): EXP 룰 없음, quotaMul 1.15만(Unity의 pace 룰 제거). finals=첫×0.9/막×2.0 명시 확인 | 소 | P2 |
| 9 | **플레이어 레벨/XP**: `xpReq(lvl)=120+(lvl-1)×60`, MAX100, 런XP=`40+min(20,stage)×12+score/250+boss×20+신규업적×25`, 레벨 해금 로드맵(캐릭3·머신3·장치3 자동지급·증강4·유물4), 이력 XP 시딩 | 중 | P3 |
| 10 | **업적 웹 체계**: 34종(기본16+후반5+심화13) + 장치 보상 21매핑 + 심볼 해금 13매핑. Unity 482종·전공연구·AccountExp 게이트는 **폐기**(→§2-D) | 대 | P3 |
| 11 | **숙련도(mastery)**: char/mac/dev × 5마일스톤, 선택·도감 ★표기 | 중 | P3 |
| 12 | **증강 레벨업**: `AUG_LEVELS` 12종 Lv1~3 + AUGLEVEL 노드(10%+pity≤20%) + 관련 아이템 | 중 | P3 |
| 13 | 해금 조건 웹화: unlockRuns/Score/Stage/Level/Ach **OR** 조건(Unity AND StatReq 폐기) | 중 | P3 |
| 14 | 콘텐츠 증보: 캐릭 16→19, 머신 16→19, 장치 16→24(심화9 포함), 증강 80→89, 유물 61→73(**PRISM 12 신설**), 아이템 73→78, 저주 16(패널티 전용화) | 중 | P3 |
| 15 | 화면 흐름 웹화: 인트로(탭하여 시작) → 로그인게이트(첫1회) → **홈**(레벨카드·게임모드·승천 선택·업적/장치 요약) → 선택 3스텝 → 런 → REWARD_DONE(능력치 패널·다음 스테이지 프리뷰) → 런종료(XP 블록·랭킹 위젯 자동등록) | 대 | P4 |
| 16 | **셀 정보 탭**(칸별 EXP/점수 분해 + 영향 퍽 델타) · 클리어 등급 6단계 연출 · 튜토리얼 3단(스포트라이트 6스텝→결과해설→라이브) · 설정 시트(소리/진동/초기화) | 대 | P4 |
| 17 | **사운드**: 절차 합성(SFX 17종 + BGM 루프) — Unity는 AudioSource + 절차 생성 클립으로 재현 | 중 | P5 |
| 18 | **승천(심화 학기) A1~A10**: 요구EXP ×(1+0.08a)·보스 A4+·상점가 A3+·아이템칸 A5+ -1·시작코인 A3+ 감소·점수 ×(1+0.12a) + A2 해골↑/A7 프리즘저주/A8 금지심볼/A9 장치쿨다운/A10 2페이즈 보스. 별도 랭킹/최고점 | 중 | P6 |
| 19 | **심화모드(심볼 덱/주머니)**: 사실상 제2의 게임 — 주머니 추출, 심볼 72종, 덱 검증 7규칙, 압축 패널티, POUCH 오퍼 2-step, 심볼증강21/심볼유물15/정비소11, 전공 아키타입 6계열, 잭팟 태그 6종, 피버 게이지, 자동소멸, 심화 업적/랭킹 | 최대 | P7 |
| 20 | 랭킹 3노드: `slotrank`/`slotrank_asc`/`slotrank_deep`(Unity 단일 `jackpotrank`) + 게스트(cid)/로그인(u_uid) 키 + 마이그레이션 | 중 | P6~P7 연동 |

### 1-B. Unity에 있고 웹에 없는 것 (처분 결정)

| 항목 | 결정 |
|---|---|
| PickView 분석 화면(추천/시너지 S~C/필터/정렬, PickMeta 498줄) | **유지** — 웹 renderSelect보다 상위 호환. 웹 파리티에 위배되지 않음 |
| 업적 482종 + 전공(Schools) 연구 + AccountExp/면허(lic_*) | **폐기 → 웹 34종 체계로 교체**(P3). 게이트는 웹 방식(레벨/업적/패밀리)으로 |
| 보류파일(dev_holdfile) 오퍼 보관 | 웹에 없음 — **유지**(웹의 장치 24종에 없으면 P3에서 정리 판단) |
| 결정론 단일시드 RNG · 원자적 세이브 · 골든 테스트 922 어서션 | **유지** — 품질 자산. 골든 기대값은 페이즈마다 웹 공식 기준으로 재산출 |
| FxKit 파티클 · 씬 분리(Intro/Play) | **유지** |

## 2. 규칙 충돌 결정 로그

- **(A) 불운 게이지**: 웹 구현(해골+1/무해골-1, 보상 연동 없음)은 자체 UI 문구("가득차면 다음 보상 희귀↑")조차 미이행한 회귀로 판정. **Unity 현행(나쁜스핀 적립, 만땅 시 forceRare+리셋 — kotlin 규칙) 유지.** 단 표기는 웹 문구를 따른다.
- **(B) 클리어 점수/캡**: 웹 채택(P2). Unity의 등급보너스·closeBonus·capMulFor 는 제거하되 등급 "연출"(6단계 배지)은 웹처럼 유지.
- **(C) 업적/게이트**: 웹 채택(P3). 482종 자산은 삭제가 아니라 `Engine/Content/Achievements.cs`를 웹 34종으로 교체하고 구파일은 git 이력으로만 남긴다.
  - **(C-1) 2026-08-08 완료 세부(P3-2 "업적 34종 교체" 슬라이스)**: `Achievements.cs`를 웹 `data.js:774-817`
    ACHIEVEMENTS 34종(기본16+후반5+심화13, id/emoji/name/desc/key/threshold/deep 데이터 그대로 — 단 astral
    이모지는 레거시 uGUI Text가 렌더링하지 못해 5종(⏰⭐⚠️☠️⚖️, 전부 BMP)만 남기고 나머지는 빈 문자열로
    대체, S8 항목⑤ 선례)로 전량 교체. 웹에 없는 cat/tier/hidden/reward 4필드는 구 "기본 16"과 동일한
    균일 기본값(cat="기타"/tier="브론즈"/hidden=false/reward="")으로 채웠다(tier="브론즈"는 Formulas.
    AccountExp ④ 컴포넌트가 계속 동작하게 하는 안전한 폴백이기도 함).
  - **장치 보상 매핑(웹 ACH_DEVICE_REWARD, data.js:818-828) 적용/보류**: 기본 12건은 Devices.cs 12종의
    `unlockAch`를 구 `lic_*` 면허 업적 id에서 새 34종 id로 직접 갱신해 적용(jackpot1→dev_subreel,
    boss1→dev_reroll, crown10→dev_seal, cherry100→dev_safe, exact1→dev_pin, lastclear5→dev_overheat,
    score10k→dev_coin, stage10→dev_oracle, prism5→dev_copy, boss5→dev_swap, runs20→dev_bell,
    score50k→dev_flame). `AchievementEngine.Evaluate`는 "unlockAch==방금 달성한 업적 id"를 범용으로
    찾아 지급하도록 일반화했다(구 `lic_` 접두 특례 제거). 심화 9건(d_ach_compress1→dev_compress_gauge
    등)은 대응 장치 자체가 Devices.cs에 없어(전부 P7 심화 전용 신규 장치) **데이터/주석만 보존, 미적용**
    (Achievements.cs 헤더 각주). ACH_SYMBOL_UNLOCK 13건(data.js:834-848)은 심화 전용(P7, 주머니/심볼
    시스템 자체가 Unity에 없음)이라 데이터도 옮기지 않았다.
  - **(C-2) 2026-08-08 Opus 2차 검수·Fable 결정 — 장치 4종 드랍 전용화 확정**: `dev_syllabus`/
    `dev_holdfile`/`dev_retake`/`dev_major`(§1-B "웹에 없음 — 유지, P3에서 정리 판단" 유예 항목)를 P3-2
    슬라이스에서 확정했다 — **드랍 전용 장치로 전환**. `unlockAch`를 빈 문자열로 바꿔 업적 해금 경로를
    없앴다(구 확장업적 id prismPick1/item10/shop50/runs50는 새 34종에 존재하지 않아 어차피 영구
    미달성이었다 — 죽은 참조를 정리). 대신 P1에서 이미 구현된 런 중 장치 드랍(DEVICE 노드/EVENT-6)
    만으로 영구 획득한다. `PlayerProfile.IsDeviceUnlocked`는 unlockAch가 비면 `OwnedDevices` 소속
    여부만 보므로 안전(별도 코드 변경 불필요, 회귀 테스트 `Tests_S5_DropOnlyDevicesNeverAchievementUnlocked`
    로 확인). 부수 발견: `StatTracker.ComputeDevicesOwned`가 `unlockAch` 공백 체크를 `OwnedDevices` 소속
    체크보다 먼저 해서, 드랍으로 이미 보유한 장치까지 `devicesOwned` 스탯에서 누락시키던 순서 버그를
    함께 고쳤다(`Tests_S5_DropOnlyDeviceCountsTowardDevicesOwned`). `DexView`의 장치 잠금 카드 힌트도
    이 4종에서 "런 중 장치 드랍으로 획득"으로 고정 안내하도록 조정했다 — `PickView`는 이 4종의
    `catalog.json` pick이 원래 `null`이라 애초에 카드 자체가 렌더링되지 않는다(확인만, 코드 변경 없음).
  - **파생키 정리**: `AchievementEngine.ComposeStat`에서 `lic_dev_*`(12) · `bldCat_*`/`bldTotal`/
    `bldAllBasic`/`bldAllMaster`(구 "빌드도감" 업적 전용) · `accountLevel`(소비처가 테스트뿐이라 제거)을
    없앴다. `distinctCharS10`만 유지(Characters.cs "prodigy" unlockReq가 여전히 참조). StatTracker의
    원시 카운터 수집(bld_&lt;id&gt; 25종 포함)은 전부 그대로 유지 — 다음 슬라이스/도감/통계가 계속 쓴다.
    `Formulas.AccountExp`/`AccountLevel` 함수 자체는 그대로 살아있다(Shop.PerkGate가 원재료 Stats로 직접
    호출 중, 퍽 게이트/전공 폐기는 다음 슬라이스).
  - **새 카운터**: `graduations`(웹 game.js:1401 "stage===15 클리어 = 졸업" 그대로 이식 — StatTracker.
    ApplyClearTracking이 stage==15 클리어 시 +1, grad1 업적용) · `playerLevel`(웹 game.js:2578 "XP 부여
    직전 1런 지연 스냅샷" — StatTracker.ApplyGameOverTracking이 PlayerLevelTracker 실행 *전에*
    `Stats["playerLevel"] = profile.PlayerLevel`를 직접 대입, lv20/lv40 업적용). `ascMax`는 승천(P6)이
    아직 없어 원시 카운터 자체를 추가하지 않았다(GetStat 기본값 0이 자연히 asc3/asc5를 영구 미달성으로
    둔다 — 별도 코드 불필요).
  - **XP 재시딩 마이그레이션(§2-(L))**: `PlayerProfile.PlayerXpReseed34`(신규 플래그) + `ProfileDto`
    왕복 필드로 구현. `ProfileDto.FromDto`가 로드마다 `PlayerSeedXpFromHistory`로 재산출한 값이 현재
    `PlayerXp`보다 작을 때만 1회 덮어쓴다. 부수 발견: `ProfileStore.Load()`가 "파일 없음"(최초 실행)일 때
    `new PlayerProfile()`을 직접 반환하던 기존 코드는 이 마이그레이션과 만나면 신규 플레이어의 첫 런
    직후 XP를 잘못 깎을 수 있었다(Runs=0/PlayerXp=0 상태로 플래그가 먼저 안전하게 true 확정될 기회가
    없었음) — `ProfileStore.Load()`도 "파일 없음"을 `ProfileDto.FromDto(new PlayerProfileDto())` 경유로
    바꿔 재발을 막았다(EngineTests `Tests_PlayerLevel_XpReseed34Migration`으로 검증). 같은 이유로
    `ProfileStore.Load()`의 catch 폴백(JSON 파싱 실패 등)도 동일 경유로 통일했다(2026-08-08 Opus 2차
    검수 필수①).
  - **(L-1) 2026-08-08 Opus 2차 검수·Fable 결정 — 블랭킷 재시딩 현행 유지**: 재시딩 조건("재산출값이
    작을 때만 덮어쓴다")은 실제로는 "482종 시절 세이브만 골라 정정"이 아니라 **이력 기반 재산출로
    통일**한 것이다 — 런XP 적립 공식(`PlayerRunXp`, 스핀마다 정밀 가산)이 이력 시딩 근사식
    (`PlayerSeedXpFromHistory`, runs×30+... 거친 평균)보다 항상 크거나 같아, 정상적으로 플레이해 쌓은
    세이브에서도 이 마이그레이션이 사실상 항상 발동해 playerXp를 시딩값으로 낮춘다. 앱이 아직
    미출시라 실사용 세이브가 0건인 지금 단계에서는 이 블랭킷 정정의 부작용이 실질적으로 없으므로,
    표적 정정(마이그레이션 시점 기록·달성 업적 이력 대조 등)을 새로 설계하는 대신 단순 통일을 그대로
    채택한다 — §2-(L) 문언("재산출값이 작을 때만 덮어써라")을 문자 그대로 구현한 현재 코드를 유지한다
    (`ProfileDto.cs` 마이그레이션 블록 주석에 이 결정과 실동작을 그대로 기록).
- **(D) RNG**: Unity 단일시드 유지(웹은 Math.random 비재현 — 품질상 Unity 방식이 우위, 결과 분포 동일).
- **(E) 첫 사용 무료 소진 시점**: 웹과 동일 — **발동 성공 시에만** 소진, `_beginStage` 리셋 금지(런 단위).
- **(F) 장치 추첨(EVENT-6·보스드랍) owned 필터**: 웹은 이미 보유한 장치도 다시 뽑혀 허탕이 나는
  구조다 — `pickDevices` 호출부(game.js:1438 보스드랍, 2292 EVENT-6) 둘 다 `owned` 인자에 실제
  `ownedDevices`가 아니라 `curses`(저주 보유 집합)를 잘못 넘기는 버그성 코드라, 두 등급 폴백까지
  가더라도 사실상 "미보유" 필터가 걸리지 않는다. **Unity는 이 버그를 재현하지 않고 미보유 필터 +
  전부 보유 시 코인+15 폴백을 채택한다**(의도적 개선 — 웹 회귀 버그 예외 조항 적용, §0 결정 원칙).
  rare/non-rare 가중(`rareChance=min(0.6,0.15+stage*0.03)`) 자체는 웹과 동일하게 이식했다
  (`NodeEvents.PickDevice`) — 다른 건 "미보유"를 실제로 지키는지 여부뿐이다.
- **(G) 실패 체인과 fate_bell의 사실상 무력화**: §3-C 새 순서(①보험 ②POST_SPIN ③fate_bell)에서는
  MANIP 장치를 장착한 채로 실패하면 fate_bell을 보유하고 있어도 체크 자체에 도달하지 못한다(POST_SPIN
  분기가 먼저 걸려서). 이는 웹과 동일한 구조적 결과라 **파리티 유지** — 밸런스 재고(예: 우선순위
  재배치)는 P2 이후 별도 논의 대상으로 미룬다.
- **(L) XP 인플레이션 가드(P3-1 Opus 검수)**: 업적 482종이 살아 있는 동안 런XP의 `신규업적×25`
  항이 웹(34종) 대비 크게 부풀며, 부풀린 XP는 세이브에 남는다. 결정 — **P3 다음 슬라이스를
  "업적 34종 교체"로 승격**하고, 그 슬라이스에 ① `playerXp`를 시딩 공식으로 **재산출하는 1회
  마이그레이션**(교체 이전 세이브의 인플레이션 정정)과 ② `Stats["playerLevel"]` 기록(웹
  game.js:2578 — XP 부여 직전 1런 지연 스냅샷, 업적 lv20/lv40용)을 반드시 포함한다.
- **(H) 장치 획득 시 저장 시점**: 웹은 `profile.ownedDevices.push` 즉시 `_saveProfile()`을 호출해
  그 자리에서 영속화한다. Unity는 기존 관례(`GameSession.Do`가 `GAME_OVER` 시점에만
  `ProfileStore.Save`를 일괄 호출)를 그대로 따른다 — **런 도중 강제 종료(앱 킬 등) 시 그 런에서
  얻은 장치가 유실될 수 있음을 인지한 채 유지**(다른 런 중 획득물 전체와 동일한 저장 타이밍 정책,
  이 항목만 예외 처리하지 않는다).
- **(I) P2 — 웹 총배율 캡 부재 확정 근거** (2026-08-07): `public/play/engine.js` 전수 grep 결과,
  `capMul`/`MAX_SPIN_EXP_MUL` 문자열이 등장하는 곳은 딱 한 블록뿐이다 — `evaluate()` 내부의
  "누적 특수배수(specialMul)" 캡(engine.js:582-583 주석 · 893 검은초 · 942 대폭발 · 962 에너지팩 ·
  995 럭키7×7 · 1005 프리즘, 그리고 1009-1011 `if (specialMul !== 1) { specialMul =
  Math.min(specialMul, C.MAX_SPIN_EXP_MUL); exp *= specialMul; }`). 이는 럭키7/검은초/불안정폭탄/
  프리즘 4종의 "배수형 특수효과"끼리만 곱해 누적되는 별도 변수(specialMul)에 거는 캡으로, Unity
  구버전이 갖고 있던 "위치/불꽃/첫막스핀/전역배수(mods.expMul) 전체를 클램프하는 총배율 캡"과는
  완전히 다른 메커니즘이다 — 게다가 그 specialMul 캡 자체도 아직 Unity에 이식되지 않았다(심화모드
  전용 4개 특수심볼 중 일부만 존재, 전면 이식은 P7 심화모드 슬라이스 범위). 일반 스핀 경로(전역
  expMul·위치·첫막스핀 배수)에는 웹 어디에도 상한이 없다 — 이 근거로 `Formulas.CapMulFor`·
  `SpinResolver.Evaluate`의 capBase/capMul 클램프·`mods.lastSpinExpMul` 5.0 상한을 전부 제거했다.
- **(J) P2 — 클리어 점수 실사용부 확정**: `engine.js:73-80`의 `stageClearScore()` 함수는 저주 배수 없이
  `stage×50+leftover×2+leftSpins×100+(boss?500:0)`만 계산하지만, grep 결과 이 함수를 호출하는
  곳은 **웹 전체에 단 한 곳도 없다**(죽은 export). 실제 클리어 점수는 `game.js:1403-1418`의
  `_clearStage()`가 동일한 4항을 직접 재계산(`sBase+sLeft+sSpins+sBoss`)한 뒤 `E.streakBonus(stage)`
  (engine.js:69-71)를 별도로 더해 `gain`을 만든다 — Unity의 `Formulas.StageClearScore`는 이
  죽은 함수와 시그니처·계산을 그대로 맞추고(curses 인자 존재하되 미사용), `StageFlow.ClearStage`가
  실사용부(game.js)처럼 `Formulas.StreakBonus`를 별도로 더하는 2단 구조를 그대로 재현했다. 등급
  6단계는 `ui.js:1684-1698 clearGrade()`(exact=PERFECT, 그 외 leftover/quota 초과율 1~5단계 + 보스
  +1단계 5상한)를 한글 라벨(레이블 텍스트, astral 이모지 없이)로 그대로 옮겼다 — 점수 비가산.
  부수 발견: 클리어 코인도 웹(`game.js:1419` `C.CLEAR_COIN + (boss ? C.BOSS_COIN : 0) + clearCoinBonus`,
  가산식)과 옛 Unity·`kotlin-reference/game/SlotV2Service.kt:843`(boss일 때 CLEAR_COIN 대신 BOSS_COIN으로
  교체하는 삼항식)가 갈라져 있었다 — 보스 클리어 코인이 12(옛) vs 17(웹) 차이. §0 웹 채택 원칙에 따라
  가산식으로 수정(`StageFlow.ClearStage`).
- **(K) P2 — 보스 grad pace 룰 부재 확정**: `engine.js:1088-1104 applyBossExp()`의 switch문에는
  `finals`/`strict`/`luck` 3개 case만 있고 `grad`는 default로 떨어져 무보정이다(quotaMul 1.15만
  `data.js:141`에 명시). Unity 구버전이 갖고 있던 "빈약빌드×0.75/꾸준함부족×0.85" pace 룰은 웹
  어디에도 없어 `SpinResolver.ApplyBoss`에서 완전히 제거했다(expectedPerSpin/augCount 매개변수도
  함께 삭제 — 웹 함수 시그니처와 동일하게 `(boss, gained, res, spinIndex, spins)`만 받음).
  finals(첫스핀×0.9·막스핀×2.0, engine.js:1091-1095)·strict(3매치 미만×0.5, 1096-1098)·
  luck(⭐👑🌀×1.8/없으면×0.8, 1099-1102)는 정수나눗셈(내림) 결과까지 Unity와 일치 확인(무변경).
- **(M) 2026-08-08 완료 세부(P3-3 "숙련도 + 증강 레벨업" 슬라이스)**:
  - **숙련도(mastery)**: `PlayerProfile.Mastery`(kind="char"/"mac"/"dev" → id → `MasteryStats{Runs,
    BestStage,BossClears,BestScore,AscMax}`, 웹 `profile.mastery` 그대로) 신규. `PlayerProfile.
    BumpMastery`(웹 `_bumpMastery` L217-227 — runs+1/bestStage·bestScore setMax/bossClears 누적, ascMax는
    승천(P6) 미구현이라 갱신 로직 자체를 두지 않아 영구 -1)와 `MasteryOf`(레벨=충족 마일스톤 수 조회)를
    추가하고, 마일스톤 판정(`Formulas.MasteryLevel`/`MasteryTotal`)은 웹 `MASTERY` 표(game.js:143-165)를
    그대로 옮겼다 — 5개 마일스톤은 `else-if` 순차 게이트가 아니라 매번 독립 판정 후 카운트(웹
    `masteryLevel` L166-170 그대로, 예: bossClears가 낮아도 bestScore만 높으면 그 마일스톤만 별도 인정).
    갱신 시점은 웹과 동일하게 런 종료(GAME_OVER) 시점 — 신규 `MasteryTracker.ApplyRunEnd`(PlayerLevelTracker
    와 동일 패턴)를 `GameSession.FinishAction`이 PlayerLevelTracker.ApplyRunEnd 다음 순서로 호출한다(웹
    game.js:2627 playerXp 계산 직후 위치 그대로). ProfileDto는 JsonUtility Dictionary 미지원 제약 때문에
    (kind,id) 조합당 1행으로 펼친 병렬 배열 7개(`masteryKind/Id/Runs/BestStage/BossClears/BestScore/
    AscMax`)로 왕복한다. UI는 `PickView`(카드 role 텍스트에 ` · ★★☆☆☆` 접미)·`DexView`(Sub 텍스트에
    별 또는 "점수보정×N · 별" 병기)에 기존 필드를 최소 침습으로 재사용 — 웹 ui.js:1886(★채움/☆빈칸
    literal, `.pcard-mast`의 CSS 스타일링 방식이 아니라 이쪽 표기를 그대로 따랐다) 그대로.
  - **증강 레벨업(AUG_LEVELS)**: 신규 `Content/AugLevels.cs`가 12종(study/greed/polymath/cherry_up/
    book_up/star_up/diligence/set_sense/coin_luck/skull_study/gem_polish/lucky) Lv2/Lv3 델타를 웹
    engine.js:19-33 그대로(수치 손계산 골든 테이블 — Tests_P3_AugLevel.cs) 담는다. 델타는 기존
    `Perks.cs` fx와 동일한 점표기 딕셔너리 포맷이라 `ModsBuilder.ApplyFx` 해석기를 그대로 재사용했다
    (새 해석기 없음). `ModsBuilder.Build`에 `levels`(`IReadOnlyDictionary<string,int>`, 기본 null=전부
    Lv1) 매개변수를 추가하고 perkIds 루프 직후 Lv2(있으면)+Lv3(있으면, 둘 다 누적 적용 — 웹 buildMods
    L370-375 `if(lv>=2)...if(lv>=3)...`와 동일, "최종값 교체"가 아니라 "추가 델타 누적")를 적용한다.
    실제 게임플레이 mods를 계산하는 **모든** 호출부(SpinResolver 3단계·StageFlow.ClearStage·ItemUse
    4곳·DeviceActions 7곳·RunController.HandleContinue·GameSession.PreviewQuotaSpins, 총 17곳)에
    `run.PerkLevels`(RunState 신규 필드, `Dictionary<string,int>`, 웹 `r.perkLevels` 대응)를 함께
    전달하도록 갱신했다 — 한 곳이라도 빠뜨리면 그 경로에서만 레벨업이 무반영되는 값이 섞여 나갈 수
    있어 전수 갱신했다(웹은 UI 라벨용 "단일 퍽 격리 설명" 호출(engine.js:2735/2752/2754)에선 levels를
    안 넘기지만, Unity `ModsBuilder.Build` 호출부 17곳은 전부 실제 게임플레이 경로라 그런 격리형
    호출이 애초에 없다 — 예외 없이 전수 적용).
  - **레벨 표시(작업 지시 B.4)**: `run.Perks`(보유 증강/유물)를 그리는 화면이 기존 UI2에 하나도
    없었다(전수 grep 결과 전무 — 작업 지시가 후보로 짚은 `BagPopup`뿐). 새 화면을 신설하는 대신
    `BagPopup`(원래 아이템 전용 팝업)을 최소 침습으로 확장했다 — 기존 행 템플릿(Icon/Name/Desc/
    UseButton)을 그대로 재사용해 레벨업 가능한 보유 증강(`AugLevels.IsLevelable`)을 아이템 목록
    아래에 "{이름} Lv.N"으로 추가 표시하고 UseButton은 숨긴다(증강은 소모형 아이템과 달리 "사용"
    동작이 없는 상시 효과).
  - **AUGLEVEL 노드**: `RunState`에 `PerkLevels`/`AugLevelChance`(pity, 기본0.10)/`AugLevelBoost`(촉매
    후크, 항상 0 — 웹 🖍형광펜/🧪증강촉매 동형 아이템이 Unity 콘텐츠에 없어 부스트를 걸 수단 자체가
    없다) 3필드를 추가했다. `StageFlow.ClearStage`가 노드 목록 확정 직후(DEVICE 노드 포함 이후) 웹
    game.js:1501-1507과 동일한 확률식(`min(0.6, chance+boost)`, 미발동 시 +2%p 누적 상한20%, 발동 시
    10%로 리셋)으로 `NodeKind.Augment`를 `NodeKind.AugLevel`로 교체한다(3택 개수는 그대로 — DEVICE처럼
    "추가" 옵션이 아니라 "대체"). 게이트는 `AugLevels.LevelableHeld(run)`(보유 & Lv<3, 함수 자체는
    캡 없이 전량 반환 — 웹 game.js:1622 `r.options = this._levelableHeld().map(...)`). 오퍼 최종
    카드 수는 **Unity 전용으로 3장 캡**(Opus 1차검수 필수, 2026-08-08 — `PerkOfferPanel`이 320px
    고정 카드 3장 전용 레이아웃이라 웹처럼 전량 오퍼하면 4장 이상일 때 화면 밖으로 잘린다): 3장
    이하면 전량, 4장 이상이면 `NodeEvents.ChooseNode`가 `run.Rng.Shuffle`로 섞은 뒤 앞 3장만
    선발(RNG는 4장 이상일 때만 소비 — 기존 시드 스트림 영향 최소화). 신규 `RunPhase.
    EventAugLevel`을 두고 `NodeEvents.ChooseNode`/`PickOffer`가 이 phase를 EventAugment/EventRelic과
    나란히 처리하되, PickOffer는 "새 퍽 add" 대신 `PerkLevels[id]=min(3,cur+1)`로 분기한다(신규 RunEvent
    타입 `PERK_LEVELED`, `perkLevelBefore`/`perkLevelAfter` 필드). 후보가 0개인 방어적 경우(이론상 도달
    불가 — 롤 시점과 선택 시점 사이 후보가 바뀔 수단이 없음)는 EVENT 테이블 폴백이 아니라 웹처럼
    무보상 즉시 종료로 처리했다(AUGMENT/RELIC 풀 소진 폴백과는 다른 케이스). UI는 기존 `PerkOfferPanel`
    을 재사용 — 헤더 "증강 강화", 카드 Badges 텍스트에 "Lv.N → Lv.N+1"(dev_retake/dev_holdfile 액션은
    AugLevel에 성립하지 않는 개념이라 숨김), `NodePanel`에 "⬆ 증강 강화" 노드 카드 추가. 기존 범용
    자동플레이 하네스(`Tests_S4.cs` AutoPlay/AutoPlayRich, `Tests_S5.cs` 2곳)가 신규 phase를 몰라
    예외를 던지던 걸 발견해 `PickOffer(0)`으로 라우팅하도록 함께 고쳤다(회귀 발견 — Tests_S4_
    RunControllerAutoplay 100시드 시뮬레이션이 실제로 이 phase를 뽑아서 잡아냈다).
  - **테스트**: `Tests_P3_AugLevel.cs`(AUG_LEVELS 12종 골든 델타·Lv3 클램프·미등록id 무영향·Lv1vs Lv3
    스핀 델타 손계산·AUGLEVEL pity 불변식 2시드×40회·게이트 미보유 시 영구 미등장·ChooseNode→PickOffer
    흐름·Lv3 이후 방어적 무보상) + `Tests_P3_Mastery.cs`(char/mac/dev 마일스톤 경계값 전수·레벨=독립
    카운트 확인·BumpMastery 누적규칙·MasteryTracker 3축(dev는 미장착 스킵)·null가드·ProfileDto 왕복·
    자동플레이 2시드 교차검증) — 어서션 18315→18787(+472), 0 실패.
- **(N) 2026-08-08 완료 세부(P3-4 "콘텐츠 증보 + 해금 OR + 레벨 보상" 슬라이스, P3 마지막) — 정확한 차집합 산출**:
  data.js 대 Unity Content/*.cs를 id 기준 python set-diff로 전수 대조했다(손으로 세지 않음).
  - **캐릭터(19-16=+3)**: `regent`(Lv8)·`bankrupt`(Lv12)·`abyss_scholar`(Lv16), Unity에만 있는 항목 0건.
  - **머신(19-16=+3)**: `nightmare`(Lv10)·`throne`(Lv12)·`broke`(Lv14), Unity에만 있는 항목 0건.
  - **장치**: 웹 24종 중 이번 슬라이스 대상은 레벨자동지급 3종(`dev_reaper`/`dev_abyss`/`dev_reactor`,
    engine.js:508-510 applyPassiveDevice 그대로 fx 전사)뿐 — 나머지 9종(`dev_compress_gauge` 등, `deepOnly`)은
    P7 심화모드 전용이라 데이터·로직 모두 이번 슬라이스에서 제외(§1-A #14 원문 그대로). Unity 전용 드랍 4종
    (`dev_syllabus`/`dev_holdfile`/`dev_retake`/`dev_major`)은 §2-(C-2) 결정대로 유지.
  - **증강(89-80=+9)**: `discount`·`thrifty`·`item_bag`·`vip`·`refund`(상점/가방 시스템 관련 5종, engine.js:
    204-208) + `crown_burst`(Lv10)·`curse_grad`(Lv12)·`extreme_overload`(Lv15)·`abyss_lore`(Lv16)(후반 4종,
    engine.js:300-304). Unity에만 있는 항목 0건.
  - **유물(73-61=+12)**: PRISM 신설 12종 — 보스클리어 풀 8종(`prism_diploma`·`golden_ratio`·`starlight_crown`·
    `endless_recess`·`fortunes_wheel`·`set_resonator`·`reapers_pact`·`phoenix_thesis`, engine.js:332-339) +
    레벨해금 4종(`crown_monolith`Lv10·`black_grad_photo`Lv12·`last_roll`Lv15·`nameless_cup`Lv20, engine.js:
    306-309). Unity에만 있는 항목 0건.
  - **아이템(78-73=+5)**: 증강 레벨업 상점 상품군 — `study_note`·`aug_catalyst`·`gold_marker`·`prism_ink`·
    `overcharge`(data.js:765-770, 웹 game.js:1368-1372 useItem 분기 그대로 ItemUse.cs에 이식). Unity에만
    있는 항목 0건.
  - **저주(16=16, id 불변)**: 항목⑤ "패널티 전용화" — Unity 구 fx(양면형: 페널티+보너스)를 웹
    engine.js:378-395 buildMods 저주 루프의 순수 패널티값으로 16종 전량 교체(desc도 data.js:634-650
    그대로). 예: `hard_exam` 구 `quotaMul×1.10 & scoreMul×1.20` → 신 `quotaMul×1.10`만(요구치만 패널티,
    클리어점수 보너스 삭제). `frugal_vow` 구 `coinMul×0.6 & quotaMul×0.88`(코인 페널티+요구치 할인 보너스)
    → 신 `coinMul×0.6`만. 16종 전부 같은 패턴(감산/페널티 필드는 유지, 보너스 필드는 삭제) — 상세는
    `Perks.cs` Curses 배열 주석 및 `Tests_Fx.cs` 골든 테이블 참조.
  - **콘텐츠 fx 신규 훅**: `phoenix_thesis`(유물)의 `cliffBurstExpMul`(요구치 50% 미만 게이트 시 그 스핀
    EXP×2, engine.js:930-933)을 `Mods.cs`에 신규 필드로 추가하고 `SpinResolver.Evaluate`가
    perfectShapeExpMul 직후·전역배수 이전에 소비하도록 배선(웹과 동일 위치). `curse_grad`/`black_grad_photo`
    (scoreMul×(1+ratio×curseCount))·`abyss_lore`(보스면 expMul×1.5)는 기존 "cond.*" ctx-조건부 규약을 그대로
    재사용(신규 해석기 불필요). 상점 빌드 증강 4종(discount/thrifty/item_bag/vip)은 `Mods`에
    `shopPriceMul`/`itemPriceMul`/`itemCapBonus`/`shopSlotBonus`/`shopRerollDelta` 5개 신규 필드를
    추가했다 — **소비처(실제 상점 화면)는 P4 대상이라 이번 슬라이스는 필드·fx만 완성**(계산 로직은
    끝, 화면 연결은 후속).
- **(O) 2026-08-08 완료 세부(P3-4, 해금 OR 모델 전환)**: `Character`/`Machine`의 `unlockReq`(StatReq AND
  리스트)를 전량 폐기하고 웹 game.js:259-277 `charUnlocked`/`machineUnlocked` OR 5축/4축(unlockRuns·
  unlockScore·unlockStage(캐릭만)·unlockLevel·unlockAch)으로 교체했다 — 기존 16종도 웹 data.js 값으로
  재산출(예: `gambler`/`cherry`/`library` 머신은 웹에 unlock 필드 자체가 없어 항상 해금으로 바뀜, 구
  StatReq 게이트보다 대체로 완화됨). **grandfather(cstage_>0) 규칙은 웹 전수 grep 결과 부재를 확인**해
  폐기했다(§2 결정 로그 조건부 지시 "웹에 있으면 유지"가 부재 시 자동 미적용을 뜻함 — `Characters.Unlocked`
  구 정적 메서드 자체를 제거, 판정 로직은 `PlayerProfile.IsCharUnlocked`/`IsMachineUnlocked`로 이관).
  `PlayerLevel`은 지연 스냅샷(`Stats["playerLevel"]`, 업적 lv20/lv40 전용)이 아니라 실 필드를 직접 읽어
  레벨업 즉시 반영한다.
- **(P) 2026-08-08 완료 세부(P3-4, 퍽 레벨 게이트)**: `Shop.PerkGate`/`SchoolResearchDone`/`PerkUnlocked`/
  `GatedPool`을 전면 재작성 — 기존 전공연구(Schools.SchoolReq/SchoolResearch)·AccountLevel·`seen_`
  그랜드파더 게이트를 폐기하고 "unlockLevel 있는 8종(증강4·유물4)만 PlayerLevel로 게이트, 나머지 154종은
  항상 개방"으로 단순화했다. 웹 `pickPerksByTier`(engine.js:1213-1241)의 PERK_FAMILY 랭크 순차 게이팅은
  "해금" 축이 아니라 "한 오퍼 안에서 같은 계열이 겹치지 않게 하는 표시 순서 규칙"이라 이번 슬라이스
  범위(해금 축) 밖으로 판단해 손대지 않았다 — Unity `Shop.PickPerksByTier`의 오퍼 알고리즘 자체(가중치
  분포·favoredCat)는 이전 슬라이스부터 이미 웹 최신 `pickPerksByTier`와 갈라져 있던 기존 기술부채이고,
  이번 작업 지시가 명시한 범위(게이트 축)에 포함되지 않아 **손대지 않았다**(판단 보류 — 별도 슬라이스
  필요, 보고 대상). `Schools.cs` 파일 자체는 삭제하지 않고 게이트 연결만 끊었다(작업 지시 "삭제 범위가
  크면 파일 정리는 보류" 그대로). `GameSession` 생성자가 `Profile.SetStat("playerLevel", Profile.PlayerLevel)`
  로 런 시작 시점에 최신 레벨을 스냅샷해 Shop/NodeEvents 게이트 판정에 반영한다(업적 lv20/lv40용 1런 지연
  스냅샷 의미는 런 종료 시점에 StatTracker가 다시 되돌려 써서 그대로 보존됨).
- **(Q) 2026-08-08 완료 세부(P3-4, 레벨 장치 자동지급)**: `PlayerProfile.LevelDeviceReward`(14→dev_reaper·
  18→dev_abyss·22→dev_reactor)+`GrantLevelDevices()`(멱등)를 웹 game.js:172/246-254 `_grantLevelDevices()`
  그대로 이식 — `GameSession` 생성자(로드 직후)와 `FinishAction`의 GAME_OVER 분기(런 종료·레벨업 직후)
  양쪽에서 호출한다. `PlayerProfile.LevelUnlocks()`(웹 game.js:201-211 levelUnlocks 데이터 함수만, P4 UI
  화면은 없음)도 함께 추가했다.
- **(R) 2026-08-08 완료 세부(P3-4, UI 최소 정합 — 작업 지시 C)**: `PickView`/`PickMeta`/`JackpotCatalog`가
  신규 콘텐츠를 렌더링할 때 공백 카드가 뜨지 않도록 안전 폴백을 추가했다 — `unity-assets/manifest.json`에
  아직 신규 35종(캐릭3·머신3·장치3·증강9·유물12·아이템5)의 아트/엔트리가 없어(PNG 생성은 별도 이미지
  파이프라인 요청 필요, 이 슬라이스 범위 밖) `catalog.json` 조회가 전부 미스가 나기 때문이다.
  `PickMeta.FallbackInfo(tab,id)`가 Engine 콘텐츠(Characters/Machines/Devices)에서 직접 emoji/name/desc/
  unlock 텍스트를 합성하고, `PickView.MetaOf`/`BuildCard`가 catalog 미스 시 이 폴백으로 대체한다(아이콘은
  스프라이트 없음 → 기존 "⊘" BMP 폴백 관례 그대로, 시너지 분석은 P4). `PickMeta.CharOrder`/`MacOrder`/
  `DevOrder`에도 신규 9개 id를 추가해(그렇지 않으면 카드 자체가 순회 대상에서 빠져 "선택 불가"가 됨)
  실제로 선택 가능하게 했다. `JackpotCatalog.EnsureLoaded()`는 실 JSON 파싱 후 Engine 콘텐츠 기반 합성
  엔트리(스프라이트 없음)를 메모리상의 조회 테이블에만 이어붙여 `DexView`(도감)에도 신규 콘텐츠가 뜨게
  했다(실 JSON 파일은 미수정 — manifest.json이 나중에 갱신되면 정식 엔트리가 자동으로 우선). 기존 우선
  폴백 관례(DexView가 아트 없는 드랍전용 장치 4종에 raw astral 이모지를 그대로 쓰는 것)를 그대로 따랐다
  — PickView의 "⊘" 고정 치환과는 다른 화면별 관례이며 이번 슬라이스가 새로 만든 게 아니라 기존 관례를
  확인해 재사용한 것이다.
  - **후속 필요(스코프 밖, 보고 대상)**: `catalog.json`의 저주 16종 `descKo`는 여전히 구 텍스트(양면형)를
    담고 있다 — 엔진(`Perks.cs`)은 갱신됐지만 표시 데이터(`unity-assets/manifest.json` → convert_manifest.py
    → catalog.json 파이프라인)는 건드리지 않았다. 실제 PNG 아트 생성도 마찬가지로 후속 작업.
- **(S) 2026-08-08 완료 세부(P3-4, 테스트)**: 어서션 18787→18992(+205), 0 실패. 신규/변경 골든:
  Perks fx·meta 스냅샷 해시(신규 21종 추가 + 저주 16종 재계산), 캐릭터 exact 테이블(OR 5축으로 재설계,
  19종), 머신 가중치표(신규 3종), 아이템/장치 exact+fx(신규 8종). **의미가 바뀐 기존 테스트 재작성**:
  `Tests_RunNet_ModsAdditive`(cursed_skulls/thorny_path 보너스 삭제분 재확인) · `Tests_Run_ModsAggregation`
  (hard_exam/frugal_vow 보너스 삭제 반영 재계산) · `Tests_S4_ShopOffer`/`Tests_S4_TierPoolFallback`/
  `Tests_S4_RetakeExhaustion`(Schools "BasePerkIds 폴백" 전제가 사라져 held-기반 소진 재현으로 교체) ·
  `Tests_S5_SeenGateTracking`(seen_ 그랜드파더 폐기 반영 — "여전히 잠김" 확인으로 전환) ·
  `Tests_S5_CharUnlockDerivedKeyGate`(prodigy의 구 `distinctCharS10` 파생키 게이트 폐기 → 신 OR 모델
  unlockStage/unlockAch 검증으로 전환).
- **(T) 2026-08-08 Opus 2차검수 반영(필수4·웹 이탈 정리6·신규 골든 1) + 후속 문서화 3건**:
  - **필수**: ①`NodeEvents.OfferPerks`의 프리즘잉크 강제 티어가 10% 등급업 롤을 건너뛰던 걸 고쳐,
    웹 engine.js:1254-1256과 동일하게 "굴림은 무조건 먼저 소비 → forceTier로 덮어쓰기" 순서로 정정
    (RNG 스트림 파리티). ②`AppRoot.Awake`/`EndRun`가 `ProfileStore.Load()` 직후 `GrantLevelDevices()`를
    호출하도록 추가(웹 game.js:179 대응) — 지급이 있었을 때만 저장. 기존엔 `GameSession` 생성자에서만
    호출돼 Pick/Dex가 보는 `AppRoot.Profile`이 "런 시작 전까지" 1런 지연됐다. ③상점 5필드
    (shopPriceMul/itemPriceMul/itemCapBonus/shopSlotBonus/shopRerollDelta)를 실제로 배선 —
    `Shop.FreshOffer`(증강·유물·아이템 가격에 pm/itemPm 곱, 아이템 상품칸 2+slot) ·
    `Shop.RerollCostFor(run)`(신설, `max(2, 6+shopRerollDelta)`, 구 `RerollCost` 상수는
    `BaseRerollCost`로 이름 변경해 기본값 의미만 유지) · `ItemUse.EffectiveSlots(run)`(신설,
    `3+itemCapBonus`, 구 `ItemSlots` 상수는 `BaseItemSlots`로 변경) — `Shop.Buy`의 가방 한도 체크,
    `BagPopup`/`RunView`/`ShopPanel`의 표시 라벨까지 전부 이 메서드들로 전환했다. 승천(ascMods)·심화
    (영수증/장바구니) 항은 P6/P7 미구현이라 각 지점에 "여기에 곱연산으로 추가" 주석만 남기고 생략.
    ④`DexView` 상세 팝업의 `e.unlockReq`(catalog.json의 구 Kotlin StatReq AND 문구, manifest.json
    미갱신이라 스테일) 렌더를 차단하고 `pick.unlock`(PickInfo.unlock, 웹 OR 문구)으로 대체.
  - **웹 이탈 정리**: ⑤`RunState.PrismInkBought` 신설 — `Shop.Buy`가 프리즘잉크 재구매를 코인/가방
    체크보다 먼저 거부(웹 game.js:2350). ⑥`RunController`의 honor 시작 증강, `ItemUse`의
    black_lottery/devil_contract 3곳에서 `Shop.GatedPool(...)` 래핑을 제거하고 raw
    `Perks.Augments`/`Perks.Relics`를 직접 쓰도록 되돌렸다(웹 원본부터 이 3경로는 해금 게이트가 없다
    — game.js:393-397·1374-1376, "이미 보유 중이면 제외"하는 held 필터는 게이트가 아니라 그대로 유지).
    ⑦`refund`(환불 정책)의 30% 미소모 판정을 `retake_form`에도 적용(웹은 단일 `useItem()` 파이프라인이라
    예외가 없다, game.js:1337) — 단 `retake_form`의 `NO_LAST_SPIN` 사전 검증은 keep 롤보다 앞에 둬서
    "거부 시 RNG 포함 아무 것도 변형하지 않는다"는 기존 Unity 불변식을 지켰다(웹엔 이 가드 자체가 없음
    — Unity 전용 방어). ⑧`PlayerProfile.LevelUnlocks()`의 `List.Sort`(불안정 정렬)를 LINQ
    `OrderBy`(안정 정렬)로 바꾸고 `LevelDeviceReward`(Dictionary) 순회를 키 오름차순으로 고정 — 동순위
    항목(예: Lv12의 bankrupt/throne/curse_grad/black_grad_photo 4건)의 표시 순서가 실행마다 달라지던
    잠재 비결정성을 제거. ⑨`Tests_Content2.cs`/`Tests_Perks.cs`/`Tests_Core.cs`의 catalog↔engine
    교차대조를 "engine-only 무제한 허용"에서 "이번 슬라이스 신규 id 명시 allowlist"로 좁혔다 — 향후
    catalog.json 갱신을 누락한 콘텐츠 추가를 잡아낼 수 있게.
  - **신규 골든**: ⑩`Tests_P3_4_ContentGolden.cs` 신설 — 신규 증강9·유물12·저주16(fx 교체분)을
    `Perks.cs`를 보지 않고 `data.js`/`engine.js`를 다시 읽어 독립적으로 옮겨 적은 (id,tier,unlockLevel,
    price,fx 전체) 표. Tests_Fx.cs의 FNV 스냅샷과 별개 축 — 스냅샷은 "이후 변경 감지"만 하고 "지금 값이
    웹과 맞는지"는 검증하지 못한다(Perks.cs가 처음부터 틀렸으면 스냅샷도 함께 틀린 채 통과). fx는
    "포함 여부"가 아니라 "키 집합이 정확히 일치"까지 검증(golden에 없는 여분 fx 키가 남아 있어도 실패).
  - **어서션**: 18992 → 19244(+252). `Tests_P3_4_ContentGolden.cs`(신규 21퍽·저주16 손전사 골든 +
    상점 5필드 손계산) 신설이 대부분을 차지.
  - **후속 문서화만(코드 미반영, 별도 슬라이스 대상)** — ①②③ 전부 **P3.5(2026-08-08)에서 완료. 아래
    §2-(U) 참조**:
    1. ~~PERK_FAMILY 랭크 게이팅~~ → §2-(U) 항목①.
    2. ~~retake_form의 RunCtx 누락~~ → §2-(U) 항목③.
    3. ~~Mods.cs:107 "기본값 99 무해" 주석 정정~~ → §2-(U) 항목③.
- **(U) 2026-08-08 완료(P3.5 "퍽 오퍼 알고리즘 웹 완전 동기화" — §2-(T) 후속①②③ 슬라이스, Opus
  2차검수 필수4·권장3 반영 포함)**:
  - **① PERK_FAMILY 랭크 게이팅 이식**: 신규 `Content/PerkFamily.cs` — 웹 `data.js:345-375`
    (AUG_FAMILY 51종) + `data.js:603-621`(REL_FAMILY 45종) = 96종을 `(패밀리키, 랭크)` 튜플로 손전사
    (bash로 Unity `Perks.cs`의 178개 id 전체와 대조해 96종이 전부 실존 id인지 사전 검증). `Shop.
    PickPerksByTier`가 이제 `eligible(p) = rank == heldFamCount(fam)+1`(웹 engine.js:1233)로 후보를
    거르고 `usedFams`로 오퍼 1개당 같은 패밀리 1개만 허용한다(engine.js:1236-1238). `heldFamCount`는
    웹처럼 매 후보 평가마다 다시 reduce하지 않고 오퍼 시작 시점 `Dictionary`로 1회 집계(결과 동치,
    성능만 개선 — 이 오퍼에서 새로 뽑은 항목은 웹과 동일하게 카운트에 반영 안 됨, `usedFams`가 같은
    패밀리 중복 픽 자체를 막아주므로 문제 없음). 미등록 퍽(154-96=58종 미만, 실제로는 대부분)은
    `PerkFamily.FamOf`가 `(자기id, 랭크1)`로 폴백해 항상 후보(웹 engine.js:15 `famOf` 그대로).
    (`.meta` 파일은 별도 배치 처리 대상 — 이 슬라이스에서 건드리지 않음.)
  - **② 오퍼 알고리즘 전면 웹 대조 — 차이점 전수 목록(웹 기준 정렬)**:
    1. **티어 결정 — 스테이지 가중 롤(TierWeights/RollTier) 완전 제거**: 예전 `Shop.PickPerksByTier`는
       Kotlin 유래의 스테이지별 SILVER/GOLD/PRISM 확률 가중 롤(forceTier가 없을 때만 타는 else 분기)을
       갖고 있었는데, `NodeEvents.OfferPerks`가 **항상** forceTier를 확정해서 넘기는 현재 호출 구조에서
       이 분기는 애초에 도달 불가능한 죽은 코드였다. 웹 `pickPerksByTier`(engine.js:1213-1241)에는
       이런 가중 롤 개념 자체가 없다(항상 `tierForClearedStage`+10%등급업+`forceTier`로 결정형) — 죽은
       분기를 전부 제거하고 시그니처를 `(rng, pool, held, forceTier, bossClear, favoredCat)`로 웹과
       동형화했다. **주의**: 상점 구매 오퍼(`Shop.FreshOffer`가 쓰는 `PickAugments`/`PickRelics`)는
       여전히 이 가중 롤(`TierWeights`/`RollTier`)을 그대로 쓴다 — 별개 함수라 영향 없음(아래 "발견한
       추가 이탈" 참조, 이번 슬라이스 범위 밖).
    2. **forceRare(불운 게이지 만땅) — 죽은 코드였음을 발견 + [Fable 결정] 범위 확정(Opus 2차검수
       필수②)**: 예전 `forceRare`는 위 죽은 가중-롤 분기의 `silverW`를 0으로 만드는 것 말곤 아무 일도
       하지 않아, 실제 게임플레이에서 게이지가 가득 차도 오퍼 티어에 **전혀 영향이 없는 버그**였다.
       1차 재구현안(무조건 `TierUp` 한 단계)을 Opus 2차검수에서 재검토 — **Fable 결정: kotlin 원본
       의도("silverW=0" = "GOLD 이상 보장")를 그대로 보존해 SILVER 노드일 때만 GOLD로 승급하고, GOLD/
       PRISM 노드는 무승급**(이미 "희귀↑ 보장" 조건을 자연히 만족한 것으로 간주 — PRISM까지 계속
       밀어올리는 건 원본 의도를 넘어서는 과승급이었다). **게이지는 만땅 상태에서 오퍼가 발생하면
       (heldPerk 분기를 포함해) 항상 소모**한다 — 이미 GOLD+ 노드라 승급이 안 걸려도 "희귀↑ 보장"
       조건 자체는 자연히 이행된 것으로 보고 리셋한다. `NodeEvents.OfferPerks`에서 nodeTier 확정
       직후(10%롤+forceTier 처리 이후) `lucky && nodeTier==SILVER`면 `nodeTier=GOLD`로 결정적 후처리
       (RNG 미소비)한다 — 게이지가 0인 절대다수 오퍼에서는 웹과 RNG 소비 순서가 완전히 동일하게
       유지된다. **관련 테스트**: `Tests_P3_5_OfferFixedSeedRegression`의 gold4(SILVER→GOLD 승급
       대조쌍)·gold5(GOLD 노드 무승급)·gold6(PRISM 노드 무승급) — 세 케이스 모두 `expectGaugeReset:
       true`로 게이지 소모까지 확인.
    2-1. **🗂️보류파일 오퍼 티어 혼용 회귀 제거(Opus 2차검수 필수①)**: 위 forceRare 재구현 1차안이
       `heldPerk!=null`/`else` 분기 **밖**에 있어, 보류파일(`dev_holdfile`) 사용 중에 불운 게이지가
       만땅이면 보류 티어(결정형)가 강제로 등급업되는 회귀가 있었다("보류 티어 결정형 우선" 원칙 위반).
       `lucky` 판정을 `heldPerk==null`(보류 미사용) 분기 **안**으로 옮겨, 보류 사용 중엔 forceRare가
       전혀 개입하지 않도록 정정했다 — `Tests_P3_5_OfferFixedSeedRegression` gold7(HeldAug="preview" +
       게이지5 → 오퍼 티어가 SILVER로 유지되고 0번 칸이 정확히 "preview" 자신)로 직접 검증.
    3. **티어 풀 소진 폴백 — "avail 전체(타티어 혼용)"→웹 기준 단계형(PRISM→GOLD→SILVER) 폴백으로 환원**:
       예전엔 강제 티어 풀에 미보유 후보가 없으면 **모든 티어**를 섞은 `avail`로 폴백했다(2026-08-03
       Fable 승인, ENGINE_PORT_DESIGN.md S16 §A — 당시 근거는 "BASE 22종 게이트로 대부분 풀이 텅 비어
       오퍼가 통째로 EVENT로 새던 문제"). 웹은 "그 자리에서 멈추는" 단계형 폴백만 쓰고(PRISM 없으면
       GOLD, GOLD도 없으면 SILVER, 그래도 없으면 그대로 빈 티어 — "3개 못 채우면 적게 제시, 타티어로
       메우지 않음") avail 전체 폴백 개념이 없다. S16 §A의 근본 원인(BASE 22종 게이트)은 §2-(P)
       슬라이스가 게이트 자체를 unlockLevel 8종 전용으로 단순화(154/162종 상시개방)하며 이미 해소돼
       있어, 이번 슬라이스에서 웹 기준 단계형 폴백으로 되돌렸다(ENGINE_PORT_DESIGN.md S16 §A에 이
       환원을 가리키는 역참조 각주 추가). `Tests_S4_TierPoolFallback`의 어서션을 Opus 2차검수 권장⑥
       반영해 "GOLD 없음"에서 "전원 SILVER"(`All(tier==SILVER)`)로 강화했다 — GOLD 소진 시 단계형
       폴백은 `[SILVER]`뿐이라 PRISM은 애초에 후보에 들지 않는다(구 주석의 "SILVER/PRISM 잔여" 서술은
       부정확했던 것도 함께 정정).
    4. **dev_major favoredCat — Kotlin 유래 빌드시너지 편향의 웹 기준 제거(밸런스 변경, Fable 승인
       — "버그 수정"이 아님, Opus 2차검수 필수⑤ 반영해 문서 프레이밍 정정)**: 예전 코드에서 실제로
       발견한 구현 결함은 `favoredCat`(dev_major 전용) 매개변수와 별개로 `var fav =
       FavoredSymbol(held);`를 **매 호출마다 무조건** 계산해, dev_major를 장착하지 않은 절대다수의
       오퍼에서도 "보유 퍽 중 가장 흔한 심볼" 편향 픽이 매번 섞여 들어가던 것(웹에 전혀 없는 RNG
       소비 — RNG 순서가 웹과 항상 어긋나 있었다)이다. 이 자체는 명백한 코드 결함이라 고쳤다. 하지만
       더 근본적으로는 **"보유 퍽 중 가장 흔한 심볼로 오퍼를 편향시킨다"는 발상 자체가 Kotlin 원본
       (kotlin-reference)의 산물이며, 웹 `pickPerksByTier`엔 이런 개념이 애초에 존재하지 않는다** —
       즉 "장착 여부와 무관하게 항상 적용되던 걸 dev_major 장착 시로 좁힌 것"은 버그 수정이 아니라
       **웹 기준으로 이 빌드시너지 편향 자체를 (dev_major라는 Unity 전용 장치 하나로 한정해) 축소하는
       밸런스 결정**이다(§0 "충돌 시 웹 채택이 기본" 원칙 적용, Fable 승인). dev_major(장치, 웹에
       대응 없음)의 desc("주력 계열 증강 등장확률 소폭↑")가 유일한 실효과라 완전히 제거하면 장치가
       no-op이 되므로, dev_holdfile/dev_retake와 동일한 원칙(미장착 시엔 웹과 100% 동일, 장착 시에만
       추가 소비)으로 재배선했다 — `favoredCat`이 null(=dev_major 미장착이거나 RELIC 노드, 또는 held
       가 비어 `FavoredSymbol([])`이 null을 반환하는 경우)이면 이 블록 자체가 RNG를 전혀 소비하지
       않는다. **관련 테스트(Opus 2차검수 권장⑦ 반영)**: (a) 전 슬롯 기준(예전엔 슬롯0만 봐서
       `PickPerksByTier` 끝의 `rng.Shuffle`이 위치를 다시 섞는다는 점을 놓쳐 신호가 희석됐다)으로
       favored 심볼 포함율 대조, (b) held=[]로 favoredCat이 null로 귀결되는 상황에서 dev_major
       장착·미장착 두 실행이 **완전히 동일한 오퍼**를 내는지(=RNG 미소비의 직접 증거) 40시드 확인.
    5. **SetSynergyAug의 cat 필터 — 웹은 항상 AUGMENT, 예전 Unity는 node 종류로 분기**: 웹
       `setSynergyPick`(engine.js:1170-1192)은 `cat` 매개변수를 시그니처에는 받지만 본문에서 절대
       읽지 않고 `augById = Map(AUGMENTS...)`로 고정한다("이름이 setSynergyAug인 이유" — 코드 주석
       원문) — 즉 **RELIC 노드 오퍼라도 5% 시너지 주입 조각은 항상 AUGMENT일 수 있다**(RELIC이 아님).
       예전 Unity는 `node == Augment ? PCat.AUGMENT : PCat.RELIC`으로 실제로 필터링 카테고리를
       갈랐었다 — `Shop.SetSynergyAug`를 웹과 동일하게 `cat` 인자 무시·항상 `PCat.AUGMENT`로 고쳤다.
       `Tests_S4_SetSynergyInjection`의 RELIC 시나리오를 "set_combo(set_charm 주입)"→"set_cherry_net
       (cherry_up 주입, AUGMENT)"으로 교체해 이 동작을 직접 검증한다(예전 페어는 새 규칙에서
       injected=0%가 되어 그대로 두면 실패).
    5-1. **5% 세트조각 주입 — `picks.Count>=2` 검사 위치 정정(Opus 2차검수 필수③)**: 웹
       engine.js:1262 `if (rng.n(100) < 5 && picks.length >= 2) { ... setSynergyPick(...) ... }`는
       `picks.length>=2`가 `&&` 우변이라 **`setSynergyPick`(RNG 소비) 호출 자체가 조건절 안에 있다**
       — 1~2장짜리 오퍼에서는 100-roll이 성공해도 `setSynergyPick`을 아예 호출하지 않아 그만큼 RNG를
       추가 소비하지 않는다. 1차 구현은 `SetSynergyAug`를 먼저 호출한 뒤에야 `picks.Count>=2`를
       검사해, 1장짜리 오퍼에서도 웹에 없는 RNG 소비가 발생했다 — `picks.Count>=2`를 `SetSynergyAug`
       호출 **앞**의 `&&` 조건절로 옮겨 웹과 동일한 단락 평가 순서로 정정했다.
    6. **unlockLevel 게이트 위치 — PickPerksByTier 내부 → 호출자(NodeEvents.OfferPerks)로 이동**: 웹
       `pickPerksByTier` 자체엔 게이트 개념이 없다 — 게이트는 `_augPool()`/`_relicPool()`(game.js:
       234-235)이 호출 *전에* 미리 걸러서 넘긴다. `Shop.GatedPool` 호출을 `PickPerksByTier` 내부에서
       `NodeEvents.OfferPerks`로 옮겨 웹과 동일한 "호출자가 먼저 거른다" 구조로 맞췄다(행동 동일, 구조만
       정렬 — §2-(P)가 만든 게이트 규칙 자체는 그대로).
    7. **bossClear 계산식의 `clearedStage>0` 누락(사문사 — 실질 영향 없음)**: `bossClear =
       Formulas.IsBossStage(clearedStage)`가 `clearedStage==0`(0%5==0)을 보스클리어로 오판할 수 있었다
       — 웹 `opts.bossClear ?? (clearedStage > 0 && clearedStage % 5 === 0)`(engine.js:1250)엔 `>0`
       조건이 있다. `NodeEvents.OfferPerks`에서 노드 오퍼가 호출되는 시점엔 `clearedStage`가 0이 될 수
       없어(스테이지 클리어 후에만 노드 선택이 뜬다) 실질 영향은 없지만, `bossClear`가
       `RunEvent.offerBossPrism` 표시 필드로 그대로 노출되므로 문자 그대로 맞췄다.
    - **RNG 소비 순서 최종 정리(Opus 2차검수 반영 후)** — dev_holdfile·dev_major 둘 다 미장착인 일반
      경로는 이제 웹과 완전히 동일한 순서다: `[10%등급업 롤] → [forceTier 덮어쓰기, RNG 없음] →
      [불운게이지 SILVER→GOLD 승급, RNG 없음, Unity 전용] → PickPerksByTier[티어폴백 RNG없음 →
      family-gated 채움 루프(픽마다 1회) → 최종 shuffle] → [5%시너지 롤 → picks.Count>=2일 때만
      SetSynergyAug(세트별 최대 1회 PickOrDefault)]`. dev_holdfile 장착 시엔 10%롤/불운승급/시너지
      단계 전부를 스킵(보류 티어 결정형 우선, 웹에 대응 없는 Unity 전용 분기 — 게이지는 여전히
      소모됨), dev_major 장착 시엔 family-gated 채움 루프 진입 전 1회 추가 `PickOrDefault`가
      끼어든다(둘 다 장착 시에만 발생, 웹 파리티 예외로 문서화된 지점).
    - **발견한 추가 웹 이탈(이번 슬라이스 범위 밖, 보고 대상)**: 웹 game.js:2334-2337(실제 상점 오퍼
      생성)은 `E.offerPerks(...)`를 직접 호출한다 — 즉 **웹의 진짜 상점도 `pickPerksByTier`/
      `offerPerks`(family게이팅·10%등급업 포함)를 쓰지, 별도의 스테이지 가중 확률표를 쓰지 않는다.**
      Unity `Shop.FreshOffer`는 여전히 Kotlin 유래의 `PickAugments`/`PickRelics`(`TierWeights`/
      `RollTier` 스테이지 가중 롤 + `GatePrism` 2택 컷)를 쓴다 — 이는 §2-(P)가 이미 "이전 슬라이스부터
      갈라져 있던 기존 기술부채"로 기록한 항목과 같은 축이며, 이번 작업 지시가 명시한 범위
      (`Shop.PickPerksByTier`/`NodeEvents.OfferPerks` — 노드 리워드 오퍼)에 상점 오퍼 생성부는
      포함되지 않아 손대지 않았다. 상점 오퍼까지 웹과 동기화하려면 `Shop.FreshOffer`를 `offerPerks`
      기반으로 재작성하는 별도 슬라이스가 필요하다(가격 정책·`GatePrism`·`allowPrism`(EVENT_PRISM_RATE)
      로직과의 결합 방식을 새로 설계해야 함 — 단순 치환이 아님).
  - **③ retake_form의 RunCtx 반영**: `ItemUse.UseRetakeForm`이 `SpinResolver.ResolveSpin`과 동일한
    2단계 패턴(ctx 없는 1차 `ModsBuilder.Build`로 `EffSpins`/`QuotaOf` 산출 → 그 값으로
    `SpinResolver.RunCtxOf`(신규 `internal`, 웹 `_ctx()` 대응) 구성 → ctx 포함 2차 `ModsBuilder.Build`)
    으로 웹 `_freeReroll()`(game.js:1214-1224 → `_mods()`→`_ctx()`, game.js:443-445)과 동등하게 ctx를
    채운다. 영향받는 ctx-조건부 퍽 14종(early_prep/growth_log/snowball/fortune_check/luck_accum/
    fate_burst/late_focus/cliff_focus/sacrifice/black_diploma/bankrupt/abyss_scholar/curse_grad/
    black_grad_photo/phoenix_thesis)이 이제 재굴림 시점에도 실제 run 상태(stage/stageExp/quota/
    growthStack/snowStack/curseCount/unluckyGauge/boss/coins)를 정확히 반영한다. `Mods.cs`의
    `RunCtx.coins` 기본값(99) 주석도 "미설정 호출부에서도 안전한 무해 기본값"이라던 부정확한 서술을
    "우연히 소비처가 expMul을 안 읽어서 무해했을 뿐, 필드 자체는 결코 중립값이 아니다"로 정정했다.
    **[Opus 2차검수 권장⑧ 반영]** 이 2단계 패턴은 오늘 시점 콘텐츠 기준으로는 `SpinResolver.
    ResolveSpin`의 3단계 재계산과 **결과값이 동치**다(ctx-조건부 14종 중 `bonusSpins`/`quotaMul`에
    영향을 주는 항목이 `black_diploma`의 `bonusSpins`뿐인데, 그 조건은 `ctx.curseCount`(=
    `run.Curses.Count`, mods 계산과 무관하게 즉시 알 수 있는 값)만 보므로 이번 2단계로 정확히
    포착된다) — 그러나 **구조적으로 3단계와 동일하지는 않다**. 향후 ctx-조건부 퍽이 `quotaMul`/
    `bonusSpins`를 "다른 ctx 필드"(예: stage나 spinIndex)에 의존해 계산하도록 확장되면, 1차 mods로
    산출한 `preSpins`/`preQuota`가 실제 최종값과 어긋나는 시나리오가 이론상 가능하다 — 그 경우엔
    `ResolveSpin`처럼 진짜 3단계(혹은 고정점 반복)로 확장해야 한다.
  - **테스트**: 신규 `Tests_P3_5_OfferParity.cs` — ⓐ`Tests_P3_5_PerkFamilyGolden`(96종 손전사 골든,
    키 집합 완전 일치 검증 + 미등록 id 폴백 확인) ⓑ`Tests_P3_5_FamilyRankGating`(랭크1만 등장/랭크1
    보유 후 랭크2 개방/exp_g 4랭크 순차 체인/오퍼당 같은 패밀리 1개 — `Shop.PickPerksByTier` 직접 호출
    400시드×4시나리오) ⓒ`Tests_P3_5_OfferFixedSeedRegression`(Opus 2차검수 필수④ 반영 — `HardcodedGoldenCases`
    7케이스: SILVER 평상 오퍼·GOLD+family게이팅 실사용·GOLD→SILVER 단계형 폴백·forceRare SILVER→GOLD
    승급 대조쌍·forceRare GOLD 무승급·forceRare PRISM 무승급·보류파일 우선순위, 전부 실제 퍽 id 배열
    하드코딩 + `offerTierBumped`/게이지 리셋까지 단정 — 티어·family게이팅·폴백 로직 중 하나라도
    바뀌면 최소 1케이스가 실패한다. 기존 결정론/forceTier프리즘잉크/bossClear 구조 검증과 dev_major
    전슬롯 편향·RNG 미소비 대조도 유지) ⓓ`Tests_P3_5_RetakeCtxPropagation`(bankrupt 캐릭터로 coins=0
    vs coins=20 재굴림 — `SpinResult.mul`이 정확히 1.5 vs 0.8로 갈리고 `preMul`은 동일 시드라 완전히
    같음을 확인, ctx 미반영이면 둘 다 0.8로 나와 실패했을 시나리오). `Tests_S4_TierPoolFallback`도
    권장⑥ 반영해 강화(위 항목②-3). 어서션 19244 → 19848(+604), 0 실패.
- **(V) 2026-08-08 완료(P4 1/3 — 홈 화면 + 레벨 보상 화면 + 런종료 XP 블록, WEB_PARITY_DESIGN.md
  §1-A #15)**:
  - **엔진 데이터 노출(최소)**: `Formulas.PlayerLevelProgressFromXp(totalXp)`(신규, 웹 game.js:110-115
    `levelInfo()` 그대로 — level/inLevel/need/ratio/xp/max) + `PlayerProfile.LevelProgress()`(위임)를
    추가했다. 기존 `PlayerLevelFromXp`는 새 `PlayerLevelLoop` 사설 헬퍼로 리팩터해 두 함수가 동일한
    while 루프를 공유한다(행동 변경 없음 — 리팩터 전후 결과 동일성은 `Tests_PlayerLevel_ProgressFromXp`
    가 `PlayerLevelFromXp`와 교차 대조). `PlayerProfile.LevelUnlocks()`는 P3-4(§2-(N)/(Q))에서 이미
    엔진에 준비돼 있어 이번 슬라이스에서는 추가하지 않았다(그대로 재사용).
  - **A. 홈 화면(MenuView)**: 웹 `renderHome`(ui.js:603-631) 순서대로 재구성 — scr-title →
    **레벨 카드**(신규, 클릭형 — 탭하면 레벨 보상 화면) → **게임 모드 선택기**(신규, 일반/심화 2카드
    — 심화는 "준비 중" 배지 + 탭 시 토스트, P7 미구현) → (승천 선택기 자리는 주석만 남기고 렌더
    생략 — 아래 참조) → 기존 hud 카드(칭호+3스탯)+요약줄+게임시작/랭킹/도감 버튼+설명문 유지 →
    **데이터 초기화**(신규, `ConfirmSheetPopup` 재사용). `MenuView.Refresh()`를 `private`→`public`
    으로 승격해 `AppRoot.ResetProfile()`이 화면 전환 없이 즉시 재호출할 수 있게 했다.
  - **승천 선택기 생략 근거**: 웹 `ascSelector()`(ui.js:572-590)는 `g.ascUnlocked()`
    (`profile.ascMax>=0`, 승천 1회 이상 졸업)가 false면 렌더 자체를 안 한다 — 승천(P6, §1-A #18)이
    미구현이라 `ascMax` 필드조차 프로필에 없어(§2-(M) MasteryStats.AscMax와는 별개 개념) 이 조건을
    지금 판정할 수 없다. **주석으로 자리만 예약**(BuildMenuScreen 내부)하고 렌더는 생략 — 웹도
    동일 조건에서 미노출이므로 파리티 위배 아님.
  - **B. 레벨 보상 화면(신규, LevelRewardsView.cs + Editor/UiSceneBuilder.cs BuildLevelRewardsScreen)**:
    웹 `renderLevelRewards`(ui.js:635-646) 이식 — 레벨 카드(비클릭형) + `PlayerProfile.LevelUnlocks()`
    로드맵을 레벨순 행 목록(`RankView.cs`의 "템플릿 clone" 패턴 그대로 재사용)으로 나열, 각 행
    "Lv.N — 이름 (종류)" + 해금/잠김 색상·라벨. `ScreenRouter.ScreenId.LevelRewards` 신규 추가 +
    `IntroSceneRoot.levelRewardsView` 필드 + `AppRoot.ShowLevelRewards()`. 뒤로가기는 기존
    `NavButton.Target.Menu` 재사용(신규 Target 불필요).
  - **잠금 표기**: 웹 로드맵 행은 `unlocked`일 때 "✓", 아닐 때 "🔒"(astral)를 쓰는데, 레거시 uGUI
    Text가 astral을 렌더링하지 못하는 프로젝트 제약(S8 항목⑤)이 있어 "해금"/"잠김" 한글 라벨 +
    색상(Good/TextSecondary)으로 대체했다(`DexView`의 "[미해금]"·`PickView`의 "잠김" 라벨과 동일
    기존 관례 재사용 — 이번 슬라이스가 새로 만든 패턴이 아니다). 로드맵 항목의 이모지 접두(🎭🎰🔧📜 등,
    `PlayerProfile.LevelUnlocks()`가 웹처럼 이름 앞에 직접 박아 만든 문자열)도 대부분 astral이라
    `TextSanitize.StripAstral`로 표시 직전에만 걸러낸다(GameOverPanel 업적 행과 동일 관례).
  - **C. 런종료 XP 블록(GameOverPanel)**: 웹 `renderEnd` endxp 블록(ui.js:2117-2124) 이식 — 신규
    업적 리스트 아래·메뉴 버튼 위(웹 배치 순서 그대로, Unity엔 랭킹 위젯이 없어 그 자리는 비움)에
    "플레이어 레벨 Lv.N" + "+N XP" + (레벨업 시만) "레벨 업! Lv.A → Lv.B" + XP 진행바 + "다음
    레벨까지 N XP"/"최고 레벨 달성"을 추가했다. `FailureOutcome.PlayerXpGain`/`PlayerLevelBefore`/
    `PlayerLevelAfter`는 P3-1(§2-(L) 이전 슬라이스)에서 이미 엔진에 준비돼 있어 소비만 했다. 웹은
    이 블록을 정적으로 한 번에 그리지만, 기존 Unity 연출 관례(`HudView.AnimateExpRoutine`의
    "이전값→현재값 트윈", `GainPanel.ShowRoutine`의 대문짝 카운트업/팝인)를 따라 "+N XP" 카운트업
    (0.4s)과 XP 바 채움(레벨 유지 시 트윈, 레벨업 시 즉시 반영+배너 OutBack 팝인)을 추가했다(작업
    지시 "카운트업/펄스는 기존 UiTween 관례" 반영 — 웹에 없는 연출이지만 프로젝트 기존 관례의
    자연스러운 확장이며 로직/수치에는 영향 없음).
  - **데이터 초기화 배선**: `ProfileStore.Delete()`(신규, 저장 파일 삭제, 예외 비전파) +
    `AppRoot.ResetProfile()`(신규 — Session 비움 → 파일 삭제 → `ProfileDto.FromDto(new
    PlayerProfileDto())`로 새 프로필 생성 → 즉시 `ProfileStore.Save`로 재영속화 →
    `PlayerPrefs.DeleteKey(LoginView.NickPrefKey)`로 닉네임도 제거(웹 `resetData()`가 로컬스토리지
    "slotweb_nick"까지 지우는 것과 동일 파리티 — 초기화 후 재진입 시 로그인 화면부터 다시 시작, 랭킹
    관련 PlayerPrefs는 웹처럼 그대로 유지) → `MenuView.Refresh()` → 토스트) 조합으로 웹
    `resetData()`(game.js:2636, "리셋 즉시 영속화")와 동일하게 동작한다. 웹과 달리 화면 전환은 하지
    않는다(이미 메뉴 화면에서만 진입 가능하므로 카드만 새로고침) — 이 점만 웹과 다르다(의도적
    단순화, 결과는 동일).
  - **소리 토글 생략**: 웹 `.reset-link.sndtog`(소리 켜짐/꺼짐 토글)는 작업 지시대로 버튼 자체를
    짓지 않고 주석으로만 P5 예약을 남겼다(빈 자리도 만들지 않음).
  - **2026-08-08 Opus 2차검수 반영(필수2·폴리시4·정리6)**:
    ① XP 블록 연출 타이밍 — 이전엔 `Show()`가 카드 스케일인(EnterRoutine)과 XP 애니메이션을 동시에
    시작해, 카드가 아직 scale 0으로 접혀 있는 0.75s(딤 0.4s+스케일인 0.35s) 동안 카운트업/바 채움이
    끝나버리는 결함이 있었다. `EnterThenXpRoutine`(신규)으로 묶어 EnterRoutine 완료 **후**에만 XP
    연출이 시작하도록 정정(firstShow 경로만 — 이미 카드가 보이는 재갱신 경로는 그대로 즉시 재생).
    ② MAX 레벨(`lp.Need==0`)일 때 XP 바가 매번 "0%→100%" 트윈으로 잘못 채워지던 버그 수정 — MAX면
    `fromPct`를 `targetPct`(1f)로 고정해 트윈을 생략(더 채울 여지가 없으므로).
    ③ 레벨 카드 badge가 108×108이 아니라 행의 가용 높이(114)까지 늘어나고 body가 위쪽에 붙어 보이던
    레이아웃 결함 — Unity uGUI `HorizontalLayoutGroup.childForceExpandHeight=true`는 자식의 명시적
    `flexibleHeight=0`도 내부적으로 `Mathf.Max(flexible,1)`로 강제 승격한다는 사실을 간과한 것이
    원인이었다. `BuildLevelCard`의 badge/body 행에서 `childControlHeight`는 유지한 채
    `childForceExpandHeight`만 꺼서 badge는 108 정사각 그대로, body는 `childAlignment=MiddleLeft`로
    세로 중앙 정렬되게 정정.
    ④ `GameOverPanel` XpBlock — 고정 높이(164)를 `achContent`와 동일한 자동높이 조합
    (`VerticalLayoutGroup`+`ContentSizeFitter`, xpBlock 자신에 직접)으로 교체해, 레벨업 미표시(대부분
    의 런)일 때 하단 44px 공백이 남던 문제 해소.
    ⑤ 게임 모드 "일반" 카드에도 Button+PressFx 추가(웹은 두 카드 모두 `<button>` — 탭 눌림 피드백
    파리티. 클릭 리스너는 여전히 "심화" 카드에만).
    ⑥ 경미 정리 — `LevelCardResult.root`(어디서도 읽지 않는 죽은 필드) 제거·XP 진행바 MAX 색상
    특수처리(Green) 전부 제거(웹은 무색 — 항상 Accent, MenuView·LevelRewardsView·GameOverPanel 3곳)·
    `Formulas.PlayerLevelProgress.Xp` 필드 주석 정정(Unity는 항상 0 이상 클램프하는데 웹 `xp` 필드
    자체는 미클램프라던 원래 주석이 부정확했음 — 실사용상 무해한 차이임을 명시)·`Tests_PlayerLevel.cs`
    주석 오기("레벨 2 진입 직후" → 실제 계산값 "레벨 3") 정정·`UiSceneBuilder.cs`/`LevelRewardsView.cs`
    주석에 남아 있던 자물쇠 이모지(🔓/🔒) 리터럴을 텍스트 설명으로 교체·`LevelRewardsView`에 웹
    `roadHtml || '해금 항목 없음'`(ui.js:640) 폴백 추가(로드맵이 비면 "해금 항목 없음" 표시 —
    실제로는 `PlayerProfile.LevelUnlocks()`가 항상 항목을 갖고 있어 도달 어려운 방어 경로).
  - **테스트**: `Tests_PlayerLevel_ProgressFromXp`(신규, `Tests_PlayerLevel.cs`) — xp=0/일반/MAX/
    음수 4구간 손계산 + `PlayerLevelFromXp`와의 level 일관성 + `PlayerProfile.LevelProgress()` 위임
    확인. 어서션 19848 → 19867(+19), 0 실패(Opus 반영 재검증 포함 동일). 씬 리빌드가 필요한 UI 회귀
    (레이아웃 겹침 등)는 Fable이 배치 리빌드 후 육안 검수(설계 지시 "씬 리빌드는 Fable이 배치로
    실행") — 이 슬라이스는 빌더 코드 + MSBuild 스모크 컴파일(Assembly-CSharp/​Assembly-CSharp-Editor
    0에러)까지만 확인했다.
  - **웹 대비 생략/보고 대상**: 승천 선택기(위 근거) · 소리 토글(P5) 외에 이번 슬라이스가 다루지
    않은 P4 잔여 항목(§1-A #15/#16의 REWARD_DONE 능력치 패널·셀 정보 탭·클리어 등급 6단계 연출·
    튜토리얼 3단·설정 시트)은 후속 슬라이스(2/3, 3/3) 대상.
- **(W) 2026-08-09 완료(P4 2/3 — REWARD_DONE 화면 + 셀 정보 탭 + 클리어 등급 연출, WEB_PARITY_DESIGN.md
  §1-A #15/#16)**:
  - **A. REWARD_DONE**: `RunPhase.RewardDone` 신설 — 웹 `_enterRewardDone`(game.js:1573-1585)처럼
    노드/상점 처리 직후 곧장 `Spin`으로 안 가고 이 화면에서 "스테이지 N 시작" 탭을 기다린다.
    `RewardFlow.Enter`(신규 헬퍼)가 `NodeEvents.cs`(Rest/Gamble/Curse/Risk/EVENT테이블 및 AUGMENT/
    RELIC/CURSE/RISK 풀소진 공유폴백/AugLevel무보상/PickOffer의 PERK_GRANTED·PERK_LEVELED 두 분기/
    HoldAugment)와 `Shop.cs`(Leave)의 옛 `run.Phase=RunPhase.Spin` 대입을 전부 대체한다. 유일한 예외
    `NodeEvents.TakeDevice`(DEVICE 노드 확정)는 웹 `deviceNodeTake`(game.js:2523-2529)가
    `_enterRewardDone`을 거치지 않고 곧장 `_beginStage()`로 가는 것과 동일 파리티로 그대로 Spin
    직행 유지(확인 완료 — 웹 소스 전수 grep 결과 `PHASE.SPIN` 직접 대입은 런 시작 1곳과
    `_beginStage()` 내부뿐, deviceNodeTake만 REWARD_DONE 미경유). `HoldAugment`(dev_holdfile,
    Unity 전용 — 웹에 대응 개념 없음)도 다른 노드 해소 분기와의 일관성을 위해 REWARD_DONE을
    거치도록 확장했다(웹 이탈 아님 — Fable 최종검수 대상으로 표기).
    - `RunController.ProceedToStage`(신규 액션, 웹 `proceedToStage()`) — `RewardDone→Spin` 게이트.
      Unity는 스핀수/요구치를 스테이지마다 캐시하지 않고 매번 `SpinResolver.EffSpins/QuotaOf`로
      즉석 계산하는 구조라(`StageFlow.ClearStage` 헤더 주석) 웹 `_beginStage()`가 하던 재계산은
      이미 `ClearStage` 시점에 끝나 있다 — 이 액션은 순수 phase 게이트이고 추가 상태 리셋이 없다.
    - `RunState.RewardMessage`(웹 `r.rewardMsg`) + `RunState.ShopBoughtLabels`(웹 `r.shopBought`
      — 상점 진입 시 `ChooseNode`가 리셋, `Shop.Buy`가 구매마다 이름 누적, `Shop.Leave`가 조합해
      메시지 완성) 신규 필드. 메시지는 엔진에서 직접 조립하되 웹 리터럴을 맹목적으로 베끼지 않았다
      — EVENT 테이블 case4(웹: 코인+20 / Unity: 스테이지 스핀+1)·case6(웹: 미장착 시 자동장착 /
      Unity: rare가중 추첨+미보유필터, §1-A #4·§2-(F) 결정으로 이미 갈라진 수치)는
      `EventRewardMessage`가 `RunEvent` 필드(coinsDelta/scoreDelta/bonusSpinsDelta/
      itemGrantedId/...)에서 재구성해 실제 지급과 항상 일치시킨다. 이모지 없이 한글+숫자만 사용
      (엔진 산출 문자열 규약 — 표시 레이어가 안전하게 그대로 쓸 수 있게).
    - `RewardDoneInfo.cs`(신규, 표시 전용) — `RewardDoneView.NextPreview(run)`(웹 `nextPreview`,
      quota/spins/bossId — run.Stage가 이미 ClearStage에서 다음 스테이지로 갱신돼 있어 정확),
      `CurrentStats(run)`(웹 `currentStats()` — mods 15행 + 심볼별 EXP/점수/태그 델타. ctx는
      "다음 스테이지 시작 직전" 의미로 stage=run.Stage·spinIndex=0·growthStack/snowStack/
      curseCount/boss/coins=현재 run 값으로 채워 daredevil/cliff_focus류 ctx-조건부 퍽까지
      정확히 반영 — 웹 `_ctx()`가 실제로는 갱신 전 이전 스테이지의 stale `r.spins/r.quota`를
      참조하는 사소한 차이는 재현하지 않음). 심볼 라벨은 emoji 대신 한글 이름 사용(astral 렌더
      제약 회피, GainPanel 선례).
    - UI: `RewardDonePanel.cs`(신규, `UI2/Run/Panels/`) — 보상 메시지 → 보유 효과(증강/유물/저주/
      장치, BagPopup 행 관례 재사용 — 웹의 "칩 탭→상세 토글" 2단 인터랙션 대신 처음부터 전부 펼쳐
      보임, 정보량 동일·탭수만 감소로 단순화) → 현재 능력치(GainPanel의 Inner/Label·Value 행 관례
      재사용) → 다음 스테이지 프리뷰(보스면 보스 emoji/이름/설명, 아니면 "다음 STAGE N") →
      [스테이지 N 시작]. `RunView`(`rewardDonePanel` 필드 + `RefreshPhasePanel` 분기) /
      `Editor/UiSceneBuilder.cs`(`BuildRewardDonePanel` + 두 행 템플릿 `BuildRewardBuildRowTemplate`
      /`BuildRewardStatRowTemplate`, `RunOverlayResult`/`WireRunView` 배선) 신규.
  - **B. 셀 정보 탭**: `CellInfoView.cs`(신규, 표시 전용) — 웹 `cellInfo`(game.js:2706-2787) 그대로
    칸 EXP/점수 분해(기본→심볼 보너스→태그 보너스→해골→가운데 배수) + 전체배수 안내 + "이 칸에
    영향 주는 증강/유물/캐릭터/저주" 델타 라벨(baseline `ModsBuilder.Build("basic","gambler",[])`
    대비 diff — 웹 `label()` 클로저와 동일 로직, 심볼 표기만 emoji→한글 이름). 정확도를 위해
    `RunState.LastMods`(신규, 웹 `r.lastMods`) 캐시가 필요했다 — "지금 이 순간"이 아니라 "그 칸이
    실제로 나온 스핀"의 mods로 분해해야 하므로 `SpinResolver.ResolveSpin`(주 경로) +
    `DeviceActions.cs`의 MANIP 재계산·도박꾼 무료재굴림 + `ItemUse.UseRetakeForm`(단, LastMods는
    의도적으로 미갱신 — 아래 §2-(W) 참조) 총 4곳에서 캐시한다. 심화모드 `pouchInfo`(주머니
    보유율)는 Unity에 심화모드 자체가 없어 미이식. `RunState.LastCellsFinal`(신규, `List<Cell>`,
    웹 `r.lastCells = res.cells` 대응 — Evaluate 이후 최종 칸, 폭탄 제거·자석 복사·성장 전부 반영)
    을 읽어 "자석으로 복사된 칸"/"씨앗이 성장한 칸"/"폭탄으로 제거된 빈 칸" 특수 안내 3줄 모두
    재현한다(아래 §2-(W) Opus 2차검수 — 예전 슬라이스에서 `RunState.LastCells`(raw)만 있어
    2줄을 재현 못 하던 범위축소가 해소됨).
    - UI: `ReelView`에 셀 탭 추가 — `Editor/UiSceneBuilder.cs BuildReelCellTemplate`가 셀 루트에
      `Button`(transition=None, 기존 배경 Image를 targetGraphic으로 재사용) 부착, `ReelView.
      EnsureCellCount`가 매 스핀 셀 재생성 시 인덱스별 `onClick`을 다시 걸고
      `SetCellTapHandler(Action<int>)`(신규, `RunView.WireOnce`가 1회 등록)로 콜백을 받는다.
      결과 없는 칸(대기 상태 등)은 `CellInfoView.Build`가 null을 반환해 조용히 무시(웹
      `openCellSheet`의 `if (!info) return;`과 동일). `CellInfoSheet.cs`(신규, `UI2/Run/Panels/`) —
      BagPopup류 스크림 클릭 닫힘 바텀시트. RewardDonePanel과 같은 두 행 템플릿을 재사용(EXP/점수
      분해=Inner/Label·Value, 영향 항목 목록=IconSlot+InfoCol). `RunView`(`cellInfoSheet` 필드) /
      `Editor/UiSceneBuilder.cs`(`BuildCellInfoSheet`) 배선.
  - **C. 클리어 등급 연출**: `ClearOutcome.grade`/`gradeTier`는 P2에서 이미 산출돼 있어(§2-(J))
    이번 슬라이스는 연출만 추가했다. 웹 `stageClearFx`(ui.js:1700-1739) 대응 — `NodePanel`의 클리어
    배너 등급 텍스트에 tier별 색(1~2=초록·3=파랑·4=보라·5+PERFECT=골드, 웹 `.cchip.grade.g1~g5/
    perfect` CSS 그라디언트를 단색으로 근사) + 등장 펄스(배너 안착 시 OutQuad→OutBack 스케일 팝,
    고티어일수록 큰 폭) + 색종이 escalation(`FxId.Clear` 프리팹을 tier에 비례해 1~5회 스태거
    반복재생 — 웹 24/40/58/78/104개 파티클 카운트는 CSS 전용이라 1:1 이식 대상이 아니라고 판단해
    "재생 횟수"로 근사, 작업 지시 "근사" 명시 범위 그대로). `ReelView.PlayClearShake(int gradeTier)`
    (신규) — 웹 `shake(tier>=5?"xl":tier>=3?"bg":"sm")` + "tier≥4면 230ms 후 2차 흔들림" 그대로
    이식(진폭/지속시간 자체는 웹 CSS 수치가 아니라 ReelView 기존 셰이크 상수 눈금 3~9px에 맞춤).
    `NodePanel.Show`에 `Action<int> onShake` 콜백 매개변수 추가(NodePanel은 ReelView를 직접 참조하지
    않는 기존 컴포넌트 분리를 유지 — `RunView`가 `tier => reelView?.PlayClearShake(tier)`로 연결,
    배너 등장과 같은 타이밍에 트리거).
  - **테스트**: `Tests_P4_RewardDoneCellInfo.cs`(신규) — `ProceedToStage` phase 게이트(정상+
    잘못된phase 거부) · 노드별 RewardMessage 정확한 문구(Rest/Shop 구매유무 2케이스/Gamble 승패
    양쪽/AugLevel무보상/Device 예외 확인) · `NextPreview` 손계산 2케이스(비보스 stage1: 110×0.92=
    101, 보스 stage5 "finals": 150×0.92×1.2=165·스핀6) · `CurrentStats` 손계산(퍽 없음=novice
    quotaMul 행 1개만 / study+cherry_up 조합=expMul×1.1 행 + symExp "체리+2") ·
    `CellInfoView` 손계산(퍽 없음 3칸 기본 분해 + cherry_up 보유 시 해당 칸 델타와 무관 칸 제외
    확인). 기존 `Tests_S4.cs`(노드 8종·상점 나가기·보류·티어폴백 전수)·`Tests_P3_AugLevel.cs`
    (AUGLEVEL 흐름·무후보 폴백)의 `RunPhase.Spin` 기대값을 `RunPhase.RewardDone`으로 갱신(TakeDevice
    확정 분기는 Spin 유지 — 예외 그대로 반영)했고, 자동플레이 하네스 4곳(`Tests_S4.cs` AutoPlay/
    AutoPlayRich·`Tests_S5.cs` 2곳·`Tests_P3_Mastery.cs`·`Tests_PlayerLevel.cs`)에
    `case RunPhase.RewardDone: rc.Do(new ProceedToStage())` 분기를 추가했다(P3-3 EventAugLevel
    선례 그대로 "전수 대응"). 어서션 19867 → 19937(1차) → 20004(Opus 2차검수 반영 후), 0 실패.
    - **스모크 컴파일**: Unity 에디터가 이 슬라이스 작업 중 미실행 상태라(MCP 브리지 미연결)
      `dotnet exec csc.dll`로 Unity 2022.3.39f1 Managed DLL(UnityEngine/UnityEditor 전체 모듈 +
      Library/ScriptAssemblies 기존 패키지 DLL + NetStandard 2.1 레퍼런스 파사드)을 직접 참조해
      오프라인 검증했다(`/define:UNITY_EDITOR` 포함) — `Assembly-CSharp`(런타임 76개 스크립트) ·
      `Assembly-CSharp-Editor`(6개 스크립트) 둘 다 0에러(경고는 전부 기존에도 있던 미할당
      SerializeField CS0649뿐, 내 신규 필드들도 같은 패턴). 2차검수 반영 후 재확인도 동일하게
      0에러. 씬 리빌드·프리팹·.meta 파일 생성은 다루지 않았다 — Fable이 에디터에서 배치 실행 예정
      (`UiSceneBuilder` 빌더 코드만 이 슬라이스 범위, 기존 슬라이스들과 동일 분업).
  - **§2-(W) 2026-08-09 Opus 2차검수 반영(필수6·LOW일괄)**: ①`RunState.LastCellsFinal`(신규)
    도입 — `LastCells`(재굴림 입력용 원시 스냅샷, Evaluate 이전)만 읽던 `CellInfoView`가 폭탄
    제거·자석 복사 후 릴에 실제로 보이는 결과와 어긋날 수 있던 결함 수정(웹 `r.lastCells =
    res.cells` 파리티). `SpinResolver.ResolveSpin`·`DeviceActions`(MANIP·도박꾼재굴림)·
    `ItemUse.UseRetakeForm` 4곳에서 갱신(`ItemUse`쪽은 LastMods는 의도적으로 미갱신 — 웹
    `_freeReroll()`도 `r.lastMods`를 안 건드림, game.js:1214-1224 확인). ②빈칸(EmptySym)의
    `Sym` enum 자리표시값(Sym.Cherry, 실사용 안 함 전제)으로 `perSymbolExp`/`perSymbolScore`를
    가드 없이 조회하면 새어 들어오는 오판정 수정 + 이제 `Cell.tag` 보존으로 자석/성장 특수 안내도
    복원. ③CellInfoSheet.cs 방어적 StripAstral 2곳 + "🪙" 리터럴 제거(엔진 문자열 규약 자기위반
    해소). ④`RewardDoneView.NextPreview`/`GameSession.PreviewQuotaSpins`에 `ApplyPassiveDevice`
    누락 수정(dev_reactor 프리뷰 15% 어긋남 해소, `CurrentStats`까지 3곳 정책 통일). ⑤
    `CellInfoSheet`/`BagPopup`/`ManipPickPopup`/`ConfirmSheetPopup`/`DexView.DexDetailPopup`
    Awake()의 `gameObject.SetActive(false)` 자기호출 결함(최초 오픈 1회만 코루틴 실패, 빌더가
    이미 비활성으로 굽는 것과 충돌) 발견·제거 — 뒤 4개는 이번 P4 슬라이스 이전부터 있던 결함.
    ⑥LOW 일괄: retake_form의 LastMods 미갱신 근거 주석(위 참조) · CellInfoView skullExp가
    `perSkullExp` 미반영 근사임을 주석 명시(웹 cellInfo 자체의 quirk) · 태그 델타 표기
    "#{t}태그"→"{t}태그" 정정(웹 label()과 openCellSheet 행 라벨의 "#" 유무 혼용 수정) ·
    `NodePanel.ConfettiBurstsByTier` 죽은 배열 원소 제거 + perfect 강도를 웹 raw 개수 비율(30이
    tier1=24 근처)에 맞게 하향 · `RunView` 셀 탭에 `_busy`/`_session` null 가드 · `RewardDoneInfo.cs`
    파일명-타입명 불일치는 리네임 대신 주석으로 명시.
    - **신규 발견(2차검수 범위 밖, 다음 슬라이스 보고 대상)**: `DeviceActions.HandleManip`이
      조작 대상 칸을 `run.LastCells`(raw)에서 복원하는데, 웹 통합 `manip()`은 `r.lastCells`(이미
      최종본)에서 복원한다(game.js:1238) — 폭탄/자석으로 원본·최종이 갈리는 스핀 직후 MANIP을
      쓰면 "화면에 보이는 빈칸"이 아니라 "그 뒤 원본 심볼"을 조작하는 파리티 차이가 있다. 이번
      슬라이스는 표시 전용(CellInfoView) 범위만 다뤄 게임플레이 로직은 손대지 않았다.
    - **테스트 보강(필수⑥) 4건**: 실제 `Do(new Spin(...))` 결과에서 클린 스핀(세트·잭팟·해골·
      특수효과 없음)의 칸별 cellExp 합이 `result.exp`/`gained`와 정확히 일치(4000시드 중 최소
      3건) · 폭탄/자석 포함 스핀 탐색 후 `LastCellsFinal`이 릴 표시를 정확히 반영(6000시드) ·
      MANIP 전후 사이 퍽 추가로 `LastMods`(1.0→1.10)·`LastCellsFinal`이 그 순간 값으로 재계산됨
      확인 · EVENT 10종 표 `RewardMessage` 정확한 문구(coinsDelta가 scoreDelta보다 먼저 조립되는
      실제 필드 순서까지 검증). 어서션 19937→20004(+67).
  - **웹 대비 생략/보고 대상**: ① RewardDonePanel의 "칩 탭→상세" 2단 인터랙션을 상시 펼침으로
    단순화. ② 색종이 개수의 "재생 횟수" 근사(작업 지시 허용 범위). ③ 튜토리얼 3단·설정 시트
    (§1-A #16 나머지)는 P4 3/3 대상, 이번 슬라이스에서 다루지 않음. ④ 위 "신규 발견" 항목
    (HandleManip의 LastCells 기준 복원, 다음 슬라이스 판단 필요).

## 3. 페이즈 로드맵

| 페이즈 | 내용 | 상태 |
|---|---|---|
| **P1** | 룰 파리티 1차: 첫판 즉시시작 · 특수스핀 첫사용무료 · 실패체인 웹 순서 · 노드 보상 수치/DEVICE 노드 · 포기 | ✅ 2026-08-07 완료 |
| **P2** | 점수·캡 공식 웹화 + 보스 grad/finals 정리 + 골든 테스트 재산출 | ✅ 2026-08-07 완료 |
| P3 | 메타 웹화: XP/레벨/레벨보상 · 업적 34종 교체 · 숙련도 · 증강 레벨업 · 해금 OR · 콘텐츠 증보(+3캐릭/+3머신/+장치/+증강9/+유물12/+아이템5) | ✅ 2026-08-08 완료 |
| P4 | 화면 흐름 웹화: 홈 · REWARD_DONE 능력치 · 셀 정보 탭 · 클리어 등급 연출 · 튜토리얼 · 설정 | 🔶 진행 중(2/3: REWARD_DONE·셀 정보 탭·클리어 등급 연출, 2026-08-09) |
| P5 | 사운드(절차 합성 SFX 17 + BGM) | 대기 |
| P6 | 승천 A1~A10 + 승천 랭킹 분리 | 대기 |
| P7 | 심화모드 전체(주머니·심볼72·심볼퍽·정비소·전공·잭팟태그·피버) + 심화 랭킹 | 대기 |

각 페이즈는 FABLE_RULES 4단계 파이프라인으로 진행하고, EngineTests 골든망을 웹 수치로 갱신하며 통과를 유지한다.
