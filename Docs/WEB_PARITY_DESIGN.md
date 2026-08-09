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
- **(X) 2026-08-09 완료(P4 3/3, 마지막 — 튜토리얼 + 설정 시트 + STAGE_CLEAR 보드 정합 + MANIP final
  파리티, WEB_PARITY_DESIGN.md §1-A #16 잔여 + §2-(W) "신규 발견" 해소)**:
  - **A. 튜토리얼 3단**: 신규 `UI2/Run/TutorialOverlay.cs`(RunView 소유) — 웹 TOUR(ui.js:1741-1748)
    6스텝을 astral 제거·`<b>` 굵게 유지로 그대로 옮겼다(특수스핀 비용 문구만 Unity 실제 라벨/원가
    — 집중1·막판2·기도3·올인4 — 로 재매핑, 웹 원문 🎯1·⏰2·🙏3·🎰4는 그대로 베끼면 오정보). 대상
    하이라이트는 웹의 진짜 컷아웃(투명 구멍) 대신 **골드 테두리 프레임**으로 강조한다(작업 지시가
    명시한 "코드생성 uGUI로 실현 가능한 방식 선택" 대안 — uGUI 표준 머티리얼은 스텐실 없이 진짜
    구멍을 뚫을 수 없음). 다른 컴포넌트(HudView/ReelView/스핀버튼/특수스핀행/아이콘행) 소유
    RectTransform 위치는 `RectTransformUtility.CalculateRelativeRectTransformBounds`로 매 스텝
    다시 계산해 겹쳐 그린다(대상 참조는 `RunView.WireOnce`가 `TutorialOverlay.SetTargets`로 1회
    전달 — modeButtons[0]/bagButton의 **부모 HGroup**을 각각 웹 `#ab-extra`/`#abicons` 근사로 씀).
    action 스텝(마지막)만 딤의 `blocksRaycasts=false`라 실제 스핀 버튼을 누를 수 있다(웹 tutSpot의
    `block:!s.action` 그대로). 2단(결과 해설)·3단(라이브 안내)은 배너 1종(딤+중앙 카드)을 공유—
    결과 해설은 `RunView.PlayRoutine`이 스핀 애니메이션 완료 콜백에서
    `TutorialOverlay.NotifySpinResult(run, quota, spins)`를 호출(0.26s 지연은 웹
    `setTimeout(tutExplainSpin,260)` 그대로 컴포넌트 내부 코루틴으로 재현), 라이브 안내는
    `PlayRoutine` 공통 꼬리에서 `NotifyPhase(phase, stage)`를 매 액션 배치 후 호출한다. Unity
    NodePanel이 웹의 STAGE_CLEAR+NODE_SELECT 2단계 화면을 한 화면(NodeSelect 진입 1회)으로 합쳐
    보여주므로 그 두 라이브 안내를 합본 1개로 통합했다(그 외 PERK_PICK/SHOP/REWARD_DONE 3개는
    웹 phase와 1:1). REWARD_DONE 안내가 표시되는 순간 튜토리얼을 종료하고
    `PlayerProfile.MarkTutorialDone()`+`ProfileStore.Save`(웹 `markTutorialDone()` 그대로).
    폴백: `NotifyPhase`가 라이브 상태에서 stage≥2면 무조건 종료(웹 render() 꼬리 그대로).
    트리거: `RunView.InitForRun`이 `!profile.TutDone`이면 420ms 지연 코루틴을 예약해 그 시점에도
    여전히 stage1/spinIndex0/Spin phase면 시작(웹 setTimeout 420ms 그대로) — HUD "?" 버튼(BMP
    ASCII, 항상 안전)으로 SPIN/POST_SPIN phase에 한해 수동 재시작 가능(웹 startTutorial 가드 동일).
    `PlayerProfile.TutDone`(신규)+`ProfileDto.tutDone` 왕복 추가.
  - **B. 설정 시트**: 신규 `UI2/SettingsSheet.cs` — 진동 토글(PlayerPrefs `jackpotrun_vibe`, 웹
    `slotweb_vibe` 대응, 기본 켜짐)은 즉시 동작·소리/볼륨은 P5 예약 비활성 행("준비 중", 상호작용
    불가)·닫기. 진입점은 홈(MenuView 우상단 ⚙ 고정 아이콘, `BuildCornerIconButton` 신설)과 런
    (RunView HUD 우측 "?"+"⚙" 2버튼) 양쪽 — 웹 `gearbtn`은 전역 1개지만 Unity는 씬이 Intro/Play로
    갈려 화면별 전용 인스턴스를 각자 짓는다(`resetConfirmPopup`/`giveUpConfirmPopup` 등 기존
    다중 인스턴스 관례 그대로). 데이터 초기화 행은 **홈 인스턴스에만** 짓는다(`BuildSettingsSheet`
    신규 `includeReset` 매개변수 — 웹 설정 시트(ui.js:881-908 openSettings)엔 애초에 데이터
    초기화 항목이 없다, 홈 화면 전용 `.reset-link`는 별개 UI다 — Opus 2차검수 필수⑥). 런 화면
    인스턴스는 `resetButton`/`resetConfirmPopup` 필드가 처음부터 null이라 `SettingsSheet.
    OnResetClicked` 자체가 도달 불가(방어적 null 체크로 컴포넌트는 계속 공용). `Handheld.
    Vibrate()` 훅은 작업 지시대로 "탭 피드백 위치"(`PressFx.OnPointerDown`, 골드 버튼만 — 기존
    `fx_btn_press` 파티클과 동일 조건)에 얹었다 — `SettingsSheet.VibeEnabled`가 꺼져 있으면 무동작.
  - **C. STAGE_CLEAR 보드 웹 정합**: `StageFlow.ClearOutcome`에 `stageExpAtClear`/`quotaAtClear`/
    `usedSpins`/`totalSpins`/`lastSpinGain` 5필드 신설(런 리셋 전 스냅샷 — `ClearStage`가 이미
    지역변수로 갖고 있던 `newExp`/`quota`/`newIdx`/`spins`/`outcome.gained`를 그대로 실어 나른다,
    §3-E 상태 반영 블록이 `run.StageExp`/`run.LastGain` 등을 리셋해 버리므로 `run` 재조회로는
    복원 불가). `NodePanel`에 새 섹션(스크롤 카드 영역 맨 위, 뜬 배너 자체는 무변경 — 배너를
    그대로 키우면 하단 시트 카드와 겹칠 위험이 있어 §7 재해석 원칙에 따라 이미 스크롤 가능한
    영역에 배치)을 추가: **2바**(달성 EXP%·사용 스핀, `BuildMiniBarRow` 신설)·**마지막 스핀 5칸**
    (`run.LastCellsFinal`, astral 회피로 한글 이름 표기)+**획득 내역**(`run.LastNotes` 신규 필드,
    §D 참조)·**누적 총점수**(`run.Score`, 이미 클리어 반영됨)·**"점수 상세" 토글**(stage×50·
    초과×2·남은스핀×100·보스·연승 분해, `BuildRewardStatRowTemplate` 재사용 — 기본 접힘, 매
    Show()마다 재접힘). 기존 배너(CLEAR 배지+등급칩+점수 카운트업)의 subText만 확장해 남은
    스핀·다음 스테이지를 추가했다.
  - **D. MANIP final 파리티**: `DeviceActions.HandleManip`이 `run.LastCells`(raw, Evaluate 이전
    원시 입력)에서 복원하던 것을 `run.LastCellsFinal`(웹 `manip()`의 `r.lastCells` = 항상 최종
    칸, game.js:1238·940·1222·1286 스핀 3경로 전부 `r.lastCells = res.cells`)로 정정 — 이미
    `List<Cell>`이라 `SpinResolver.CellsFromIds` 변환도 불필요해졌다. **도박꾼 무료재굴림
    (`GamblerReroll`)도 동일하게 정정**(웹은 "재굴림" cmd를 gambler/장치 구분 없이 같은
    `manip()`으로 처리하므로 파리티상 동일 소스여야 함 — 전체 재굴림이라 셀 값 자체는 무관해도
    소스를 일치시켜 뒀다). **재수강(`ItemUse.UseRetakeForm`)은 대상 아님** — 웹 `_freeReroll()`은
    `r.lastCells`를 아예 읽지 않고(존재 확인만) 매번 전체 재굴림만 하므로 원본 그대로 두었다
    (§2-(W) `LastMods` 미갱신 결정과 같은 축, 이번엔 손댈 지점 자체가 없음). 가드도 `run.
    LastCellsFinal.Count==0`이면 `LAST_CELLS_UNAVAILABLE`로 조기 거부하도록 추가(두 함수 모두).
    부수: `run.LastNotes`(신규, 웹 `r.lastResult.notes` 대응) — `LastCellsFinal`과 동일 4곳
    (SpinResolver.ResolveSpin·DeviceActions MANIP·도박꾼재굴림·ItemUse.UseRetakeForm 전부, `LastMods`와
    달리 재수강도 포함 — 웹 `_freeReroll()`도 `r.lastResult = res`는 함) 갱신 — §C의 "획득 내역"
    표시가 이 필드를 읽는다.
  - **테스트**: 폭탄 스핀 탐색 후 dev_pin(고정 대상이 화면 그대로 빈칸 유지)·dev_copy(복사 결과가
    빈칸으로 정확히 복사됨) 2건 신규 검증(`Tests_P4_3_ManipUsesFinalCells`, 6000시드) + 기존
    MANIP 픽스처(Tests_S4.cs·Tests_S5.cs 3곳)가 `LastCells`만 채우던 걸 `LastCellsFinal`도 함께
    채우도록 보정(그러지 않으면 새 가드가 즉시 거부) + `ClearOutcome` 신규 5필드 손계산 검증
    (`clearA` 기존 골든에 추가) + `PlayerProfile.TutDone`/`ProfileDto.tutDone` 왕복 + 점수 상세
    분해 합==gainedScore 회귀 가드(`clearE`, 2차검수 LOW⑤). 어서션 20004 → 20016(+12), 0 실패.
  - **스모크 컴파일**: Unity 에디터 미실행 상태라 P4-2와 동일하게 `dotnet exec csc.dll` 오프라인
    검증(Unity 2022.3.39f1 Managed DLL + Library/ScriptAssemblies + NetStandard 2.1 ref/compat
    shim, Editor 어셈블리는 netfx shim 17종 추가 참조) — `Assembly-CSharp`(런타임, 신규 2파일
    SettingsSheet.cs/TutorialOverlay.cs 포함 78개)·`Assembly-CSharp-Editor`(6개) 둘 다 0에러
    (경고는 기존과 동일한 미할당 SerializeField CS0649뿐), 2차검수 반영 후 재확인도 동일 0에러.
  - **2026-08-09 Opus 2차검수 반영(필수6·MED4·LOW6)**:
    ①[CRITICAL] `TutorialOverlay.ShowBanner` 호출부 2곳이 콜백으로 `OnBannerOkClicked` 자기
    자신을 넘겨, 확인 버튼을 누르면 `_bannerOkAction.Invoke()`가 `OnBannerOkClicked`를 다시
    불러 무한 재귀(스택 오버플로)로 이어지던 결함 수정 — 웹 tutBannerOk는 배너를 닫기만 한다
    (tutClear()) — 결과해설→라이브전환 배너와 node/perk/shop 라이브 배너는 `null`(닫기만),
    REWARD_DONE("stats") 배너만 확인을 눌러야 `EndTutorial`(마킹+저장)이 실행되도록 콜백 자체를
    `EndTutorial`로 교체(이전엔 배너를 띄운 직후 곧바로 `EndTutorial()`을 호출해 사용자가 읽기도
    전에 사라지는 결함도 함께였다).
    ②[HIGH] `NotifyPhase`의 stage≥2 폴백 종료를 `phase==Spin||PostSpin`일 때만 평가하도록 한정
    (웹 ui.js:738은 renderPlay 컨텍스트 안에서만 평가됨) — 이 가드 없이는 스테이지 클리어 직후
    NodeSelect 진입 시점(`run.Stage`가 이미 다음 값으로 전진해 있음)에 라이브 배너 4종을 하나도
    보여주지 못하고 곧장 `EndTutorial`+`markTutorialDone`이 조기 확정돼 버렸다.
    ③[HIGH] 툴팁 세로 배치 — 하이라이트 반높이만 반영하던 gap에 툴팁 자신의 반높이도 더하고
    (`tooltipRect.rect.height*0.5f`), 화면 상하 경계를 벗어나지 않게 클램프를 추가했다(action
    스텝에서 스핀 버튼과 겹칠 위험 해소). 툴팁 배경 `Image.raycastTarget=false`도 방어적으로
    적용(Skip/Next 버튼은 각자 별도 Image라 영향 없음 — 기하가 어긋나도 배경이 클릭을 삼키지
    않게).
    ④[MED] 진동 API를 `Handheld.Vibrate()`(안드로이드 기기별 기본 패턴, 대략 ~500ms 롱버즈)에서
    `AndroidJavaObject` 경유 `VibrationEffect.createOneShot(15ms, DEFAULT_AMPLITUDE)`(API 26+,
    구버전은 `Vibrator.vibrate(ms)` 폴백)로 교체 — 웹의 짧은 확인 진동(7~18ms대)에 근사한 길이.
    `UNITY_ANDROID && !UNITY_EDITOR`로 감싸 에디터/비안드로이드는 완전 무동작. `PressFx` 훅
    지점은 그대로 유지.
    ⑤[MED] `NodePanel`: EXP% 라벨을 100 클램프 없이 그대로 표시(웹 "(320%)"처럼 초과 표시 가능,
    바 너비만 100 클램프 — `Math.min(100,pct)`는 웹도 바에만 적용). `ClearOutcome.lastSpinGain`
    소스를 `outcome.gained`(벨/즉시클리어 아이템 경로에서 항상 0으로 합성됨)에서 `run.LastGain`
    스냅샷(웹 `r.lastExpApplied` 대응, 벨/아이템이 건드리지 않아 마지막 실제 스핀 값 유지)으로
    교체 — 벨/아이템 클리어의 "이 스핀에서 +0 EXP 획득" 오표시 해소. `BuildClearDetail`에
    `clear==null||run==null` 널가드 추가.
    ⑥[MED] 런 화면 설정 시트에서 "데이터 초기화" 행 자체를 제거(위 B 참조 — 웹 설정 시트엔 없는
    요소, 홈 전용 진입점만 유지). 이전 버전의 "안내 토스트로 대체" 절충안은 폐기.
    ⑦[LOW 일괄] 튜토리얼 자동 시작을 `InitForRun` 1회성 코루틴에서 `PlayRoutine` 꼬리마다
    재평가하는 `TutorialOverlay.MaybeAutoStart`로 교체(웹이 매 render()마다 조건을 다시 보는 것과
    동일 취지) · `ExplainSpinDelayed`가 0.26s 대기 후 `_active`/`_live`를 재확인(웹 tutExplainSpin
    가드) · `RunState.LastNotes`를 `readonly`로 바꾸고 4개 갱신 지점 전부 `Clear()+AddRange()`로
    통일(이전엔 `res.notes` 참조를 그대로 대입해 `LastCellsFinal`과의 대칭이 깨져 있었다) ·
    `ManipPickPopup`의 칸 수 소스를 `run.LastCells.Count`→`run.LastCellsFinal.Count`로(D 항목과
    소스 일치) · 점수 상세 분해 합==gainedScore 회귀 어서션 1건(`clearE`, 보스+연승 모두 0이
    아닌 케이스로 검증) · `RunView` `_iconsTarget`/`extraRow` 근사 주석에 toolRow가 "포기" 버튼도
    포함한다는 점을 명시.
  - **웹 대비 생략/보고 대상**: ① 튜토리얼 스포트라이트는 "테두리 강조"로 실현(작업 지시가 명시적
    으로 허용한 대안, 진짜 컷아웃 아님). ② 3단 라이브 안내는 웹 5단계(STAGE_CLEAR/NODE_SELECT/
    PERK_PICK/SHOP/REWARD_DONE)를 Unity 화면 구조에 맞춰 4단계로 병합(STAGE_CLEAR+NODE_SELECT
    합본). ③ 설정 시트의 볼륨 슬라이더는 실제 `Slider` 컴포넌트가 아니라 정적 비활성 행(작업 지시
    "준비 중" 자리만 — 실 슬라이더 UI는 P5 사운드 슬라이스에서 완성). ④ `NodePanel` "마지막 스핀
    5칸"은 고정 5슬롯이라 보조릴(dev_subreel, 6칸) 스핀의 6번째 칸은 표시에서 빠진다(드문 조합,
    로직 영향 없음 — 표시만). ⑤ 씬 리빌드·프리팹·.meta 파일 생성은 다루지 않았다 — Fable이
    에디터에서 배치 실행 예정(기존 슬라이스들과 동일 분업). 시각 검수(설정 시트 여백, 툴팁 위/아래
    판정, 하이라이트 프레임 위치)도 씬 리빌드 후 Fable 육안 확인 필요.
- **(Y) 2026-08-09 완료(P5 "사운드" — WEB_PARITY_DESIGN.md §1-A #17, 정답지 `public/play/sound.js`
  전체 84줄 + `ui.js`의 `snd.*` 호출 지점 전수 grep)**:
  - **절차 합성 엔진**: 신규 `Scripts/Game/SoundKit.cs`(DontDestroyOnLoad 싱글턴, `AppRoot`와 동일한
    `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 자가부팅 패턴 — `AppRoot`에 종속시키지 않고
    독립시켰다, 사운드는 프로필 로드와 무관한 별도 관심사). 웹은 재생마다 Web Audio 그래프를 실시간
    구성하지만, Unity는 기동 시 `AudioClip.Create`로 sfx 16종(아래 "정확한 개수" 참조) + BGM 음표
    팔레트(PENTA 5 + bass 4, 총 9클립)를 전부 오프라인 합성해 캐시하고 재생 시점엔 `AudioSource`
    풀(8개, 라운드로빈) `PlayOneShot`만 한다 — 스케줄 오차가 없어 오히려 웹보다 타이밍이 정확하다.
    합성은 sound.js의 `tone()`(오실레이터 sine/triangle/square/sawtooth + `slideTo` 지수 주파수
    슬라이드 + `0.0001→vol→0.0001` 지수 게인 엔벨로프)과 `noise()`(선형 감쇠 화이트노이즈 + 선택적
    bandpass)를 오프라인 렌더러로 그대로 전사했다(파라미터는 클립 캐시 빌드 코드의 주석에 원본 수식
    그대로 인용). bandpass는 근사가 아니라 Web Audio spec의 "constant 0dB peak gain BPF"(Q=1 기본값)
    RBJ 쿡북 공식을 정확히 구현(Direct Form I biquad).
  - **"SFX 17종"의 정확한 개수 확인**: `sound.js`의 `switch(name)`은 tap/select/spin/reel/win/
    jackpot/coin/buy/clear/perfect/fanfare/perk/bomb/boss/error/gameover **16개**뿐이다(전수 확인).
    작업 지시·설계 문서의 "17종"은 여기에 BGM 루프 1종을 더한 표기로 보인다(16 SFX + BGM = 17개
    사운드 유닛) — 임의로 항목을 늘리지 않고 16개 그대로 구현했다.
  - **select/buy/error 배선 없음(웹 자체 죽은 코드)**: 3종 모두 합성은 구현·캐시했지만(추후 웹이
    호출을 추가하면 바로 쓸 수 있게), `ui.js`/`game.js`/`engine.js` 전체에서 `snd.sfx("select"|
    "buy"|"error")`를 호출하는 곳이 실제로는 단 한 곳도 없다(전수 grep 확인) — §0 "웹 채택이 기본"
    원칙대로 웹에 없는 트리거를 새로 만들지 않았다.
  - **트리거 배선 대응표**(웹 호출 지점 → Unity 지점, 전부 실제 코드 grep으로 대조):
    - `tap`(웹 전역 `[data-act]` 클릭 위임, spin 제외) → `PressFx.OnPointerDown`(모든 `UiKit.Button`
      공용 훅, `isGoldButton` 여부와 무관하게 전역 재생) + `SuppressTapSfx()`(spinButton·특수스핀
      4버튼에 `RunView.WireOnce`가 호출 — 웹 `data-act="spin"` 제외와 동형).
    - `spin`(웹 `doSpin()` 첫 줄) → `RunView` 5개 스핀 버튼 onClick 람다.
    - `reel`(웹 `landReel()` 꼬리, 릴 1개당 1회) → `ReelView.SpinOneReel`(착지 임팩트 직후).
    - `win`/`jackpot`(웹 `out.jackpot`/`out.bestCount>=3`) → `ReelView.PostRevealFx`(jackpot 배타
      우선, hasSet이면 win — matchCount 3/4 둘 다 커버).
    - `bomb`(웹 `blastBomb()` 꼬리) → `ReelView.TransformRevealRoutine`(`anyBombBurst` 셰이크와 동시).
    - `clear`/`perfect`/`fanfare`(tier≥4, 130ms 지연)/`win`(tier≥2·<4, 150ms 지연)/`boss`(클리어 시,
      70ms 지연) → `NodePanel.EnterRoutine`(웹 `stageClearFx` setTimeout 3종을 `DelayedSfx` 지연
      코루틴으로 재현).
    - `boss`(스테이지 최초 진입) → `RunView.PlayRoutine` 꼬리(`_lastBossCheckStage` 신규 필드, 웹
      `curStage` 변수와 동형 — phase가 Spin/PostSpin일 때만 평가해 웹 `renderPlay()` 전용 체크와
      동일 범위 유지).
    - `perk`(웹 `celebratePerk`, 오퍼 픽 시점) → `PerkOfferPanel.OnCardPicked`(홀드는 이 경로를
      타지 않아 웹처럼 무음 유지).
    - `coin`(웹 `shopBuyConfirm`/`setSound(true)`/볼륨 release) → `RunView`의 `shopPanel.Show` onBuy
      람다 + `SettingsSheet.OnSoundToggle`/`OnVolumeReleased` + `MenuView.OnSoundToggleClicked`.
    - `gameover`(+ `bgmStop`) → `GameOverPanel.Show`(firstShow 1회).
    - `bgmStart`(웹 `renderPlay()` 매번, `soundOn`이면) → `RunView.PlayRoutine` 꼬리(phase==Spin/
      PostSpin일 때만, `SoundKit.BgmStart`는 이미 재생 중이면 무시라 매번 불러도 안전).
    - `bgmStop`(웹 renderIntro/renderLoginGate/renderHome) → `IntroSceneRoot.Awake`(Title/Login/
      Menu/Pick/Dex/Rank/LevelRewards 전부가 이 씬 하나라 진입 1회로 대체 — BgmStart는 Play 씬
      RunView에서만 일어나므로 씬 분리 자체가 웹의 3개 개별 호출과 동치).
  - **AudioListener 결함 발견 + 수정**: `Editor/UiSceneBuilder.cs`의 `UICamera` 생성부(`typeof(Camera)`
    만, `AudioListener` 미포함)를 전수 확인한 결과 씬에 리스너가 전혀 없었다 — 이 상태로는
    `AudioSource.Play`가 전부 무음이 된다(콘솔 경고만 뜨고 아무 것도 안 들림, 육안 검증 불가 환경이라
    특히 위험한 결함). 씬 리빌드에 기대지 않도록 `SoundKit`(DontDestroyOnLoad) 자신의 GameObject에
    `AudioListener`를 보장한다(Intro/Play 두 씬 전환에도 유지되는 유일한 리스너) — `UiSceneBuilder`
    쪽은 변경하지 않았다(리스너 2개가 되는 상황을 피함).
  - **설정 시트 완성**: `Editor/UiSceneBuilder.cs BuildSettingsSheet`의 "준비 중" 비활성 행 2개(소리/
    볼륨, `BuildSettingsDisabledRow` — 이번에 제거)를 실제 토글(`BuildSettingsToggleRow` 재사용) +
    신규 볼륨 슬라이더(`BuildSettingsVolumeRow`+`BuildSlider`, 이 프로젝트 최초의 uGUI `Slider`
    — 기존 진행바는 전부 anchorMax 정적 표시 바라 release 이벤트가 없어 `SliderReleaseRelay`
    (`IPointerUpHandler`) 소형 헬퍼로 웹 `#volrange` "change"(release 시 예시음) vs "input"(드래그 중
    무음) 구분을 재현했다)로 교체. `MenuView`도 웹 `renderHome`의 `🔊 소리` 링크 버튼 자리를
    새로 지었다(`⚠ 데이터 초기화`와 한 행에 나란히, 웹과 동일 순서) — astral(🔊/🔇)은 렌더링되지
    않아(S8 항목⑤) 아이콘 없이 "소리 켜짐/꺼짐" 텍스트만 사용.
  - **PlayerPrefs**: `jackpotrun_sound`(기본 1=켜짐, 웹 `slotweb_sound !== "0"`과 동형) /
    `jackpotrun_vol`(기본 0.7, 웹 `let vol = 0.7`과 동일값) — `SoundKit.SetEnabled`/`SetVolume`이
    직접 영속화(호출부가 매번 저장할 필요 없음, `SettingsSheet.SafeVibrate`류 "시스템 유틸리티는
    직접 호출" 기존 관례와 동일선상).
  - **합성 근사(웹과 다른 점, 전부 `SoundKit.cs` 헤더 주석에도 동일 기록)**: ① 오실레이터 파형은
    Web Audio의 band-limited(안티에일리어싱) 버퍼가 아니라 단순 수식파(naive) — 청감상 무시 가능한
    수준. ② 웹 `o.stop(t+dur+0.03)`의 +0.03s "게인이 이미 바닥인 무음에 가까운 꼬리"는 렌더링하지
    않는다(들리는 차이 없음). ③ `noise()`의 난수원은 웹이 `Math.random()`(비결정)인 반면 Unity는
    레이어 파라미터 기반 고정 시드(`System.Random`) — 기동 시 1회만 합성해 캐시하는 구조상 오히려
    결정론이 유리하고, 화이트노이즈 텍스처의 통계적 성질은 동일해 청감 차이 없음.
  - **모바일 resume 없음**: 웹 `snd.resume()`(Web Audio 자동재생 정책 우회, 사용자 제스처 안에서
    AudioContext 재개)에 대응하는 Unity 개념이 없다(`AudioSource`는 이런 브라우저 전용 제약이 없음)
    — 작업 지시대로 의도적으로 구현하지 않고 `SoundKit.cs`에 사유만 주석으로 남겼다.
  - **스모크 컴파일**: Unity 에디터 미실행 상태라 이전 슬라이스와 동일하게 `dotnet exec csc.dll`
    오프라인 검증 — 이번엔 실제 참조 경로까지 확정해 재현 가능한 커맨드로 남긴다(이전 슬라이스들은
    결과만 기록하고 커맨드는 남기지 않아 매번 새로 재구성해야 했다): Unity 설치는 `D:\Unity\
    2022.3.39f1`(Managed dll은 `Editor\Data\Managed\*.dll` 19개 + `Editor\Data\Managed\UnityEngine\
    *.dll` 87개 — 이 하위 폴더 하나에 UnityEngine.*Module과 UnityEditor.*Module이 함께 들어있다),
    NetStandard 2.1 ref는 `Editor\Data\NetStandard\ref\2.1.0\netstandard.dll`, Editor 전용 netfx
    shim 17종은 `Editor\Data\NetStandard\compat\2.1.0\shims\netfx\*.dll`, csc.dll은 로컬 .NET SDK의
    `sdk\8.0.302\Roslyn\bincore\csc.dll`(`dotnet exec csc.dll @응답파일.rsp`로 호출, 인자가 많아
    response file 필수). `Assembly-CSharp`(런타임, `Scripts/**/*.cs` 79개 — 신규 `SoundKit.cs` 포함)
    은 `Library/ScriptAssemblies/*.dll`(Assembly-CSharp/-Editor 자기 자신 2개는 참조에서 제외) +
    위 Unity Managed + netstandard만으로, `Assembly-CSharp-Editor`(`Editor/*.cs` 6개)는 위에 더해
    방금 만든 `AsmCSharp.dll` 자체 참조 + `UNITY_EDITOR` 정의 + netfx shim 17개를 추가해 컴파일 —
    **양쪽 다 0에러·0경고**(CS0169/0649/0414만 `-nowarn`, 기존 슬라이스들의 "미할당 SerializeField
    CS0649뿐" 관례와 동일 성격이라 사전에 억제). 엔진(Engine/) 무접촉 확인 — `dotnet run --project
    Client/Jackpot/Tools/EngineTests` 20016 passed, 0 failed(불변, 이번 슬라이스가 건드리지 않은
    회귀망 그대로 통과).
  - **웹 대비 생략/보고 대상**: ① 오실레이터 band-limiting·오디오 이펙트(리버브 등, 웹도 안 씀이라
    해당 없음)는 처음부터 웹에도 없어 이식 대상이 아니다. ② 씬 리빌드·프리팹·.meta 파일 생성은
    다루지 않았다 — Fable이 에디터에서 배치 실행 예정(신규 `SoundKit` GameObject는 런타임
    자가생성이라 씬 배치 자체가 불필요, `SettingsSheet`/`MenuView`의 신규 필드만 씬 리빌드로
    와이어링 필요). ③ 실제 청감 검증(합성음이 실제로 웹과 "비슷하게 들리는지")은 에디터 Play
    모드에서만 가능 — 이번 슬라이스는 파라미터 전사의 정확성과 트리거 대응표의 완전성까지만
    검증했다(작업 지시 "오디오는 육안 검증 불가 환경" 그대로).
  - **2026-08-09 Opus 2차검수 반영(HIGH 1건 + 4건)**:
    ①[HIGH] `SoundKit.cs` — noise() 게인 엔벨로프가 tone()과 같은 `Envelope` 함수를 공유하고
    있었다(합성 수학 수기 검증에서 적발). 웹 `noise()`는 `g.gain.setValueAtTime(vol,t)`(어택 없이
    즉시 최대치) → `exponentialRampToValueAtTime(0.0001,t+dur)`(전체 dur 단일 지수감쇠)뿐인데,
    tone()의 0.008s 어택 구간(`Envelope`)을 그대로 재사용해 spin/jackpot/perfect/fanfare/bomb 5종의
    노이즈 레이어가 실제보다 느리게 붙는 타격감으로 합성돼 있었다 — `ToneEnvelope`(기존)/
    `NoiseEnvelope`(신설, 어택 없이 `peak * Pow(EnvFloor/peak, t/dur)`)로 분리해 `RenderTone`/
    `RenderNoise`가 각자 전용 함수만 쓰도록 정정.
    ② `Editor/UiSceneBuilder.cs BuildSlider` — 핸들 `sizeDelta`가 `(26,26)`이었는데, `Slider.
    UpdateVisuals`가 LeftToRight 방향에서 핸들의 y축 anchorMin/anchorMax를 항상 `(0,1)`(Handle Slide
    Area 전체 스트레치)로 덮어써 y가 "부모 대비 오프셋" 의미로 바뀐다 — 26을 그대로 두면 최종 높이가
    62px(=36px 행+26)로 튀어나온다. `(26,0)`으로 정정(Unity 기본 Slider 프리팹의 `(20,0)` 관례와
    동일 원리 — x는 앵커가 점으로 붕괴돼 절대폭 그대로 유지).
    ③ `SoundKit.SetVolume`이 볼륨 슬라이더 드래그 중(매 프레임 `onValueChanged`) `PlayerPrefs.Save()`
    까지 매번 호출해 디스크 flush가 과도했다 — `SetVolume`은 값 반영 + `SetFloat`(메모리 캐시)만
    하도록 축소하고, 신설 `SoundKit.SaveVolume()`(`PlayerPrefs.Save()`)을 release 시점(`SettingsSheet.
    OnVolumeReleased`)과 시트 `Hide()`(드래그 중 release 이벤트를 놓치고 닫는 경우 대비, 방어적
    1회 추가 호출) 두 곳에서만 호출하도록 분리.
    ④ 소리 토글 양방향 동기화 — 웹 `syncSndIcons()`는 `.sndtog` 전체를 한 번에 동기화하지만 Unity는
    `MenuView`(홈 링크 버튼)와 `SettingsSheet`(시트 내부 토글)가 서로 독립된 라벨 상태였다 — 시트
    안에서 토글해도 홈 쪽 라벨은 갱신되지 않는 결함. `SettingsSheet.Show`에 `onHide` 콜백을 추가해
    `Hide()` 호출 시점(스크림/닫기/데이터초기화 확정 전부 포함)에 실행하고, `MenuView.
    OnSettingsClicked`가 `RefreshSoundToggle`을 넘겨 시트를 닫을 때마다 홈 라벨을 재동기화한다
    (RunView는 별도 표시 라벨이 없어 전달하지 않음).
    ⑤ tap 사운드 보강 2곳 + 슬라이더 트랙 두께 — (a) 릴 셀 탭(`RunView.OnCellTapped`)은 `ReelView`
    전용 raw `Button`(PressFx 미부착, `BuildReelCellTemplate` 참조)이라 전역 tap 훅을 타지 못했다 —
    웹은 셀도 `[data-act]` 전역 위임을 그대로 타 무조건 tap이 나므로, 가드보다 먼저 `SoundKit.
    Sfx("tap")`을 추가. (b) 시트 딤 배경 탭(웹 `.sheet-bg` data-act="closeSheet")도 `BuildSheetChrome`
    의 `scrimBtn`이 `UiKit.Button` 헬퍼를 거치지 않는 수동 생성이라 PressFx가 없었다 — `dismissOnScrimClick`
    분기에 `AddComponent<PressFx>()`를 추가해 SettingsSheet/BagPopup/CellInfoSheet 등 이 헬퍼를 쓰는
    모든 딤-탭-닫기 시트에 일괄 적용. (c) 슬라이더 트랙(Background/Fill) 두께를 10f→18f로 올려
    S13 §A 9-slice 위반 구간(border합 18px가 대상 10px를 넘어 `PillSprite`가 폴백으로 늘어난 타원을
    그리던 문제)을 해소하고 터치 영역 체감도 개선.
    **권고(코드 변경 불필요, 알려진 근사로 §2-(Y) 상단에 이미 기재)**: BGM 드리프트(코루틴 프레임
    지연 누적, 웹 setTimeout도 동일한 종류의 드리프트가 있어 파리티상 문제 아님) · 재생 중 볼륨
    추종(`PlayOneShot`은 시작 시점 볼륨을 고정 캡처 — 웹 GainNode의 실시간 곱셈과 달리 재생 중인
    사운드에는 슬라이더를 움직여도 소급 적용되지 않음, 다음 재생부터 반영) · `PressFx.OnPointerDown`
    (down) vs 웹 `click`(up) 타이밍 차이(§2-(Y) 상단 트리거 배선 섹션 tap 항목 참조).
    **재검증**: `dotnet exec csc.dll` 오프라인 컴파일 재실행 — Assembly-CSharp(79개)·
    Assembly-CSharp-Editor(6개) 둘 다 0에러·0경고(불변). `dotnet run --project Client/Jackpot/Tools/
    EngineTests` 20016 passed, 0 failed(불변, Engine/ 무접촉 재확인).

- **(Z) 2026-08-09 완료(P6 "승천(심화 학기) A1~A10" — WEB_PARITY_DESIGN.md §1-A #18, 웹 game.js:121-141
  ascMods/ASC_RULE·285-291 startRun·425/449-452 _mods()/_beginStage() A2/A8·1304-1317 insertCoin/oracle
  A9·1395-1401 _clearStage A10·2147-2151 pickPerk A7·2549-2578 gameOver 점수격리, ui.js:572-590/704/2132
  ascSelector/HUD배지/런종료표기 전수 대조)**:
  - **엔진 — 6축**: 신규 `Engine/Core/AscMods.cs`(Core, Content 무의존) — `AscMods.Get(asc)`가 웹
    `ascMods(a)`의 quotaMul/bossQuotaMul/shopPriceMul/itemCapDelta/startCoinDelta/scoreMul 6필드를
    그대로 계산(`Clamp`로 [0,10] 방어), `RuleText`(웹 ASC_RULE 10단계 표시문구)도 포함. 6축 배선 지점은
    전부 웹과 동일 위치 — `SpinResolver.QuotaOf(stage,mods,asc=0,bossPhase2=false)`(신규 오버로드,
    기존 2-인자 호출 100% 호환 — Opus 2차검수 정정: 실제로는 15곳 전수 갱신(SpinResolver.ResolveSpin
    3곳·DeviceActions 5곳·ItemUse 3곳·RunController.HandleContinue·RewardDoneInfo.NextPreview·
    GameSession.PreviewQuotaSpins, grep 재확인 — 최초 보고했던 "16개"는 오산) · `Shop.
    ShopPriceMul/ItemPriceMul`(신규 `RunState run` 매개변수 추가,
    `AscMods.Get(run.Asc).ShopPriceMul` 곱연산) · `ItemUse.EffectiveSlots`(`+AscMods.Get(run.Asc).
    ItemCapDelta`) · `RunController` 생성자(`run.Coins = Math.Max(0, ch.startCoins + AscMods.Get(run.Asc).
    StartCoinDelta)`) · **`StageFlow.ForceGameOver`(최종점수)** — 웹 `_gameOver()`의 `const am =
    ascMods(r.asc); const finalScore = Math.floor(r.score * mod * am.scoreMul);`(game.js:2549-2551)를
    그대로 옮겨 `finalScore = (long)(run.Score * ScoreModifierFor(...) * AscMods.Get(run.Asc).ScoreMul)`
    로 배선했다 — **작성 중 처음엔 이 축을 빠뜨렸다가**(`ForceGameOver`가 기존엔 `run.Score *
    ScoreModifierFor(...)`만 계산해 asc 배수를 전혀 곱하지 않고 있었다) `Tests_P6_Ascension.cs` 작성
    단계에서 "최종 점수" 축 자체를 명시적으로 손계산 검증하려다 발견해 즉시 수정했다(신규
    `FinalScoreScoreMulWiring` 테스트로 asc=0/5/10 3케이스 직접 확인). `RunController.GiveUp()`(자발적
    포기)도 동일 `ForceGameOver`를 재사용하므로 자동으로 함께 반영된다. asc=0이면 `ScoreMul`이 정확히
    1.0이라 기존(P6 이전) 결과와 완전히 동일 — 회귀 없음(전체 20152 어서션 재검증 완료).
  - **엔진 — 단계 규칙 5종**: 신규 `Engine/Run/AscRunHooks.cs`(internal) — `RollBannedSym(run)`(A8,
    stage 진입 3곳: RunController 생성자·StageFlow.ClearStage 다음스테이지 진입·A10 2페이즈 재시작에서
    호출) · `ApplyRunAscMods(mods, run)`(A2 weightAdd.skull 가산 + A8 symbolWeightMul[banned]=0 대입 —
    실제 롤에 쓰이는 최종 mods에만 적용하는 6개 지점: SpinResolver.ResolveSpin·DeviceActions.
    HandlePeek/HandleManip/GamblerReroll·ItemUse.UseRetakeForm/timeline_ticket). A7(프리즘 저주)은
    `NodeEvents.PickOffer`에 인라인(node==Augment && tier==PRISM && asc>=7일 때만, RELIC 노드/SILVER
    이하 티어는 미부착 — 웹 `_pickKind==="AUG"` 조건 그대로, `RunEvent.curseGrantedId` 기존 필드
    재사용). A9(장치 쿨다운)은 `RunState.DevCdUntil`(신규) + `DeviceActions.HandleDevCoin`/
    `HandlePeek`(dev_oracle 한정, dev_syllabus는 웹에 대응 없어 제외)에 가드 삽입. A10(2페이즈
    보스)은 `StageFlow.ClearStage` 최상단 단락 — `clearedStage==15 && asc>=10 && !run.BossPhase2`면
    점수/코인/노드/카운터를 전혀 건드리지 않고 스테이지 시작 휘발성 필드만 리셋 후 `ClearOutcome{
    bossPhase2Restart=true}`로 즉시 반환(웹 `_clearStage()`가 이 시점에 `_beginStage()`로 리턴하는 것과
    동일 파리티) — 신규 `SpinStepKind.BossPhase2`/`RunEvent.type="BOSS_PHASE2"`/`StageFlow.
    BuildClearEvent(outcome,clear,deviceId)` 헬퍼(DeviceActions 3곳·ItemUse 1곳의 "스핀 없이 직접
    ClearStage 호출" 경로가 공유)로 "진짜 클리어 아님"을 호출측에 알린다. `StatTracker.ApplyOne`에
    `"BOSS_PHASE2"` 케이스 추가 — 스핀 자체의 심볼 통계(`ApplySpinIncrements`)는 반영하되
    `ApplyClearTracking`(bestStage/bossClears/graduations 등)은 건너뛰어 **보스 카운트 중복을
    방지**한다(작업 지시 명시 요구사항, `Tests_P6_Ascension.BossPhase2A10`이 `RunBossClears==1`로 직접
    검증). 2페이즈 완료 시점(`clearedStage==15` 도달, 웹 game.js:1401 그대로)에 `run.GraduatedThisRun
    =true; run.BossPhase2=false;`를 세운다 — 일반 런(asc=0)의 stage15 클리어도 이 조건을 그대로
    통과하므로(2페이즈 게이트는 `asc>=10`에서만 걸림) 동일 메커니즘으로 처리된다.
  - **엔진 — 점수 격리 + 졸업/ascMax**: `PlayerProfile`에 `AscMax`(-1 기본)·`BestAscScore`·
    `BestAscLevel`(웹 defaultProfile과 동일 기본값) + `AscUnlocked()`/`MaxPlayableAsc()`/`GetAscInfo(a)`
    (웹 ascUnlocked/maxPlayableAsc/ascInfo 그대로) 신규. `StatTracker.ApplyGameOverTracking`이
    `run.Asc<=0`이면 `bestScore`만 갱신하고 asc>0이면 `BestAscScore/BestAscLevel`로 분기(웹 game.js:
    2555-2559) — `runs`/`totalScore`/`bestStage`는 웹처럼 asc 무관하게 항상 갱신(아래 §2-(Z) 결정 로그
    참조, 작업 지시 문면과 실제 웹 소스가 갈리는 지점). `run.GraduatedThisRun`이 서면 `p.AscMax =
    Math.Max(p.AscMax, run.Asc)`(웹 2564-2565 그대로, "일반 런(asc=0) S15 클리어 → ascMax=0 확정"
    작업 지시 요구사항 충족 — `Tests_P6_Ascension.GraduationAscMaxSelectionCap`으로 직접 검증).
    `Stats["ascMax"]`를 매 게임오버마다 `p.AscMax`로 스냅샷(웹 `cnt.ascMax`, asc3/asc5 업적 카운터 —
    이미 P3-2에 데이터만 준비돼 있던 두 업적이 이제 실제로 달성 가능해짐). `PlayerProfile.BumpMastery`
    에 `graduatedThisRun`/`asc` 매개변수 추가(웹 game.js:226 `s.ascMax = Math.max(s.ascMax??-1, r.asc)`)
    — `MasteryTracker.ApplyRunEnd`가 `run.GraduatedThisRun`/`run.Asc`를 그대로 전달해 char[4](심화
    학기5 졸업) 마일스톤이 이제 실제로 충족 가능(예전엔 `ascMax` 매개변수가 항상 -1로 고정돼 있었음,
    `Formulas.MasteryLevel` 자체는 P3-3부터 이미 이 매개변수를 받고 있었다). `ProfileDto`
    ascMax/bestAscScore/bestAscLevel 왕복 3필드 추가.
  - **엔진 — 런 시작 파이프라인**: `RunController` 생성자에 `int asc=0`(방어적 `AscMods.Clamp`) 추가.
    `GameSession` 생성자에 `int asc=0` 추가 — `profile.MaxPlayableAsc()`로 재클램프 후 `RunController`에
    전달(웹 game.js:288-289 `maxAsc`/`useAsc` 그대로, "해금 상한 클램프는 Engine/Profile을 아는 계층이
    담당" 설계 원칙 6 유지 — RunController 자신은 `[0,10]` 방어적 클램프만).
  - **UI**: `AppRoot.SelectedAsc`(신규 int 프로퍼티, 웹 `let selAsc=0` 모듈 전역과 동일 — 앱 실행 내내
    메모리 유지, 세이브 미영속) + `StartRun(...,int asc=0)`/`PendingLaunchInfo.asc` 핸드오프.
    `MenuView`에 승천 선택기(웹 ascSelector 순서 그대로 — 배지 "일반"/"심화 N" + ◀/▶ + "점수 보정
    ×N"/"이번 단계: ..." + 힌트문구, `profile.AscUnlocked()==false`면 섹션 SetActive(false) — 웹은 렌더
    자체를 생략하지만 이 프로젝트는 씬이 전부 코드생성 uGUI라 "짓되 숨김"으로 동치 구현). `PickView.
    OnStartClicked`가 `appRoot.SelectedAsc`를 `StartRun`에 전달. `Editor/UiSceneBuilder.cs`
    `BuildAscSelector`(신규, `BuildModeCard`와 동일 카드 룩 재사용) + `MenuBuildResult`/`WireMenuView`
    필드 7종 추가. **HUD 승천 배지**: `HudView.ascBadgeText`(신규) — `RefreshStageCurses`에서
    `run.Asc>0`일 때만 "심화 N ×M"(웹 ui.js:704 asc-hud, astral 🎓 금지) 표시, `UiSceneBuilder.
    BuildRunHud` topRow에 stageText/cursesText 사이 삽입. **금지 심볼(A8) 표시**: 웹을 재확인한 결과
    HUD에 지속 표시되는 배지가 아니라 **스테이지 진입 시 1회성 토스트뿐**이다(웹 game.js:425
    `this.toast(...)`, ui.js `renderPlay()`에 별도 HUD 요소 없음 — 전수 grep 확인) → 작업 지시의
    "금지 심볼 표시"를 이 웹 실제 동작에 맞춰 **`RunView`의 게임 로그(notesFeed) 안내**로 구현했다
    (지속 배지를 새로 발명하지 않음 — §0 "웹 정확 전사" 원칙). `RunView.AppendBannedSymNoteIfAny()`가
    `RUN_STARTED`/`STAGE_STARTED`/`BOSS_PHASE2` 이벤트 처리 시 `run.BannedSym`을 직접 읽어 안내
    (엔진에 토스트 문자열 필드가 없음 — `HudView.RefreshBossState`와 동일한 "UI가 RunState를 직접
    관찰" 기존 패턴 재사용). A7 저주 동반/A10 2페이즈 안내도 같은 로그에 추가(`PERK_GRANTED` 케이스
    확장 + 신규 `BOSS_PHASE2` 케이스). **런종료 보드**: `GameOverPanel.ascResultRoot/ascResultText`
    (신규, 웹 ui.js:2132 `.asc-result` — "심화 학기 N · 점수 보정 ×M", `run.Asc>0`일 때만 활성화) +
    `UiSceneBuilder` stageReachedText 다음 자리에 삽입.
  - **랭킹 3노드 분리**: 작업 지시대로 이번 슬라이스는 다루지 않는다(P7-4 일괄 예정, 주석 예약도 이미
    기존 코드(`RankingService`/`RankView` 등)에 손대지 않는 방식으로 범위 밖 유지) — 단
    `PlayerProfile.BestAscScore/BestAscLevel` 기록 자체는 이번 슬라이스부터 시작됐다(작업 지시 "단
    bestAscScore 기록은 지금부터" 그대로 충족, 실제 랭킹 서버 제출(`RankingService.
    submitAscScore` 상당)은 P7-4 대상).
  - **결정 로그 — 점수 격리 축(§0 "웹 채택이 기본" 적용, 작업 지시 문면과 실제 웹 소스 대조 후 실제
    소스 채택)**: 작업 지시는 "asc>0 런은 `bestScore`/`totalScore` 일반 기록에 미반영"이라 적었으나,
    `public/play/game.js:2554-2560`을 직접 재확인한 결과 `p.runs += 1; p.totalScore += finalScore;`
    (무조건 누적)와 `p.bestStage = Math.max(p.bestStage, r.stage);`(무조건 갱신)는 **asc 여부와
    무관하게 항상 실행**되고, asc 게이트가 실제로 걸리는 것은 `p.bestScore` 대입 한 줄뿐이다
    (`if(deep){...} else if(asc>0){bestAscScore...} else p.bestScore=Math.max(...)`). 이 슬라이스는
    실제 웹 소스를 정답으로 채택해 `StatTracker.ApplyGameOverTracking`에서 `bestScore`(+ Unity 전용
    부가 필드 `BestChar`/`BestMachine`, 같은 취지로 asc==0에서만 갱신하도록 함께 게이트)만 asc로
    분기하고 `runs`/`totalScore`/`bestStage`는 그대로 무조건 갱신한다 — 작업 지시 문면의 "totalScore도
    격리"는 웹 원문과 다르므로 채택하지 않았다(Fable 최종검수 시 재확인 요망 — 의도적으로 문면이
    아니라 웹 실제 동작을 따른 판단).
  - **테스트**: 신규 `Tests_P6_Ascension.cs` — ①ascMods 6축 손계산(a=0/1/3/5/10 경계 + 상하한 클램프)
    ②QuotaOf 배선(비보스/보스/2페이즈 3케이스 손계산 + 실제 `RunController.Do(Spin)` 라이브 교차검증)
    ③A2 skull weightAdd(a=1/3/10 경계) ④A8 금지심볼 3000회 롤 실측 0건 + RollBannedSym 대조군
    ⑤A7 프리즘저주(정타 케이스 + asc<7/RELIC노드/SILVER티어 3대조군) ⑥A9 장치쿨다운(dev_coin/
    dev_oracle 2종 + asc<9 대조군, 거부→해제→재사용 3단계) ⑦A10 2페이즈(1페이즈 상태보존 확인·quota
    ×1.3 손계산·2페이즈 완료 후 졸업확정·보스카운트 중복없음·BuildClearEvent 타입 분기) ⑧상점가·
    아이템칸 배선(동일시드 오퍼 비교로 가격만 다름을 검증) ⑨시작코인 배선(parttime 캐릭터, 하한
    클램프 포함) ⑩**최종점수 scoreMul 배선**(asc=0/5/10, 위 발견 항목 직접 검증) ⑪점수격리(asc>0/
    asc=0 양쪽 대조) ⑫졸업→ascMax→선택상한(3단계 순차 시나리오 + 낮은 asc 재졸업이 ascMax를 낮추지
    않음) ⑬숙련도 AscMax 배선(졸업/미졸업 대조) ⑭자동플레이 하네스 asc=10 5시드×20000틱(BOSS_PHASE2
    이벤트 화이트리스트 포함, 예외 없이 게임오버 도달). 기존 `Tests_S4_RunControllerAutoplay`의
    `KnownEventTypes`에도 `"BOSS_PHASE2"`를 방어적으로 추가(asc=0 기본 정책으론 도달 불가하지만 향후
    확장 대비). 어서션 20016 → 20152(+136), 0 실패.
  - **스모크 컴파일**: Unity 에디터 미실행(프로세스 확인 결과 미기동) — 기존 슬라이스와 동일하게
    `dotnet exec csc.dll` 오프라인 검증. `Assembly-CSharp`(런타임, 신규 2파일 AscMods.cs/AscRunHooks.cs
    포함 81개) · `Assembly-CSharp-Editor`(6개) 둘 다 0에러·0경고(CS0169/0649/0414만 억제, 기존 관례
    동일, `ForceGameOver` 수정 후 재검증 포함). `dotnet run --project Client/Jackpot/Tools/EngineTests`
    20152 passed, 0 failed.
  - **2026-08-09 Opus 2차검수 반영(엔진 정확도 통과·totalScore 웹 소스 채택 판단 승인, 아래 5건)**:
    ①`RunView.RejectReasons`에 `"DEVICE_COOLDOWN"` 한글 문구 추가 — 웹 game.js:1306/1315 토스트
    "♨️ 심화 규칙 — 장치 쿨다운(다음 스테이지에 사용)"의 astral 제거판("심화 규칙 — 장치 쿨다운(다음
    스테이지에 사용)"). A9 쿨다운 거부 시 아무 안내 없이 무시되던 결함 해소.
    ②**ascMax 로드 마이그레이션 가드** — `ProfileDto.FromDto`가 `p.AscMax = dto.ascMax` 대입 직후
    `if (p.AscMax == 0 && p.GetStat("graduations") == 0) p.AscMax = -1;`을 추가했다. Unity
    `JsonUtility`가 이 필드 도입 이전 세이브(필드 자체가 JSON에 없음)를 역직렬화할 때 C# 필드
    초기값(-1)을 항상 보장한다고 볼 수 없다는 지적(관측 근거: 0으로 채워지는 경로가 있음) — 0으로
    채워지면 "이번 앱 첫 실행부터 이미 asc0을 졸업한 것"으로 오판정돼 승천 선택기가 부당하게
    해금될 위험이 있었다. 판별식 근거: 정당한 `ascMax=0`은 반드시 `graduations>=1`을 동반한다(asc0
    졸업이 선행돼야 `StatTracker.ApplyClearTracking`이 `graduations`를 올리므로 두 값이 함께 0인
    경우는 "필드 부재"뿐 — 논리적으로 무결한 구분). `Tests_P6_Ascension.AscMaxLoadGuard` 4케이스로
    직접 검증: (ascMax=0,graduations=0)→-1 · (0,1)→0(정당한 졸업, 미정정) · (3,2)→3(0이 아니면 가드
    미개입) · statKeys 자체가 없는 완전 구세이브도 -1.
    ③**RewardDone 프리뷰의 웹 자체 회귀(§0 예외 조항 적용, 문서화만)** — 웹 `_enterRewardDone`
    (game.js:1577-1580)의 `r.nextPreview.quota` 계산은 `E.quota(stage) * mods.quotaMul *
    E.bossQuotaMul(stage) * this._deepPenalty()`뿐이고 `ascMods(r.asc)`(am.quotaMul/am.bossQuotaMul/
    bossPhase2 ×1.3)를 전혀 곱하지 않는다 — 반면 실제 `_beginStage()`가 계산하는 진짜 `r.quota`
    (game.js:423)는 이 4항을 전부 곱한다. 즉 **웹 자신도 REWARD_DONE 화면에서 다음 스테이지 요구
    EXP를 승천 배수만큼 과소 표시하는 회귀가 있다**(미리보기가 실제보다 항상 작게 보임, asc가 높을수록
    괴리가 커짐 — a=10이면 실제 대비 최대 절반 이하로 표시될 수 있음). `RewardDoneView.NextPreview`가
    이미 `SpinResolver.QuotaOf(run.Stage, mods, run.Asc, run.BossPhase2)`(6축 전부 포함)를 쓰고 있어
    Unity는 이 회귀를 재현하지 않고 정확한 값을 보여준다 — §0 "웹 쪽이 명백한 회귀 버그일 때만 예외"
    조항 적용(의도적 이탈, 버그 아님).
    ④**quota 리터럴 기대값 신설** — `Tests_P6_Ascension.QuotaOfLiteralGolden`이 `HandQuota`(코드와
    동일 수식을 다시 써서 대조하는 순환검증) 없이 stage15·기본`Mods()`·asc10 축 조합의 결과를
    정수 리터럴로 고정한다(bossPhase2=false→2315, true→3010 — 계산 근거는 테스트 파일 주석에 단계별
    수치로 남김). `QuotaOfWiring`의 기존 `HandQuota` 비교들은 "실제 배선 지점이 같은 인자를 정확히
    전달하는지"를 확인하는 용도로는 여전히 유효해 남겨 두고, 이 리터럴 테스트가 "수식 자체의 정확성"
    을 보완한다.
    ⑤**[권장 3건]** HUD 승천 배지: 빈 문자열 대입(칸은 계속 차지) 대신 `ascBadgeText.gameObject.
    SetActive(run.Asc>0)`로 전환 — 일반 런에서 HUD 상단 행이 승천 배지 칸(130px)만큼 낭비되던 것을
    해소(`HorizontalLayoutGroup`이 비활성 자식을 레이아웃에서 자동 제외). 상점가 테스트
    (`ShopPriceAndItemCapWiring`): `offerA[i].price`(시스템 산출값)를 "기준가" 대용으로 쓰던 것을
    걷어내고, `Perks.ById(id).tier`/`.price`·`Items.ById(id).coinCost`에서 직접 기준가를 구해 asc=0/
    asc=3 두 오퍼를 각각 독립적으로 검증하도록 재작성(간접 대조→직접 대조). ASC_RULE[2] 문구
    ("요구 EXP 추가 +", 뒤가 잘려 있음)는 웹 `data.js` ASC_RULE[2] 원문 자체의 오타/미완성 문구를
    그대로 옮긴 것이다(Unity가 만든 결함이 아님, §0 "웹 채택이 기본" — 임의로 문장을 완성하지 않고
    원문 그대로 보존) — `Engine/Core/AscMods.cs` 주석에도 이 사실을 명시. "16개 호출부" 표기는 실제
    grep 결과 15곳으로 정정(위 6축 문단에서 직접 수정).
    재검증: `dotnet run --project Client/Jackpot/Tools/EngineTests` 20152 → 20164(+12), 0 실패.
  - **웹 대비 생략/보고 대상**: ① 랭킹 3노드 분리(P7-4 예정, 위 참조). ② `RewardDoneView.CurrentStats`
    (다음 스테이지 능력치 미리보기 패널)가 개별 mods 필드(quotaMul 등)는 보여주지만 ascMods 6축을
    별도 행으로 노출하지 않는다(작업 지시 범위 밖 — 6축은 QuotaOf/가격/시작코인/최종점수 등 실제
    계산에는 전부 반영되지만 이 특정 표시 패널 갱신은 하지 않았음). ③ 심화모드(deep) 상호배제는
    `MenuView`/`UiSceneBuilder` 주석으로만 자리를 예약(P7에서 deep 토글 상태가 생기면 `ascSelector`도
    웹처럼 `if(selDeep) hide`를 추가해야 함). ④ 씬 리빌드·프리팹·.meta 파일 생성은 다루지 않았다 —
    Fable이 에디터에서 배치 실행 예정(신규 SerializeField 7+3+2종의 실제 GameObject 배선·시각 검수
    포함, 기존 슬라이스들과 동일 분업).
- **(AA) 2026-08-09 완료(P7-1 "심화모드(심볼 덱/주머니) 코어" — WEB_PARITY_DESIGN.md §1-A #19 1/4
  슬라이스, 웹 `data.js` SYMS/POUCH_*/DEEP·`engine.js` pouchTotal/compressionPenalty/symCatOf/
  symTierOf/pouchValidate/pouchDraw/rollFromPouch·`game.js` startRun/deepPity/_deepPenalty/gameOver
  전수 대조)**:
  - **심볼 카탈로그 72종**: `Symbols.cs` 14종 → 72종(웹 SYMS L26-131 그대로, 신규 58종은 원본 배열
    뒤에 그대로 이어붙임 — 순서를 지켜야 하는 건 원본 14종의 §10.1 누적가중치 스캔뿐이라 뒤에
    추가하는 한 영향 없음). `Sym` enum도 58개 추가(72종), `Sp` enum도 신규 51개 추가(61종 — 신규
    58종 중 4종은 기존 COIN/SKULL/FLAME/KEY를 재사용). 신규 58종은 전부 `weight=0/dormant=true`
    (심화모드 주머니로만 등장, 일반모드 확률 무영향) — `Symbols.Count`를 72로, 신규
    `Symbols.LegacyCount=14`를 원본 경계로 추가했다. **불변식 테스트**(작업 지시 "weight>0 심볼
    집합이 바뀌면 안 됨"): `Tests_Core_Symbols`에 ①신규 58종 전부 weight==0·dormant==true ②weight>0
    심볼 집합이 정확히 원본 10종(cherry/book/star/gem/coin/skull/flame/magnet/bomb/crown)과 일치
    (정렬 비교) 2개 검증을 추가 — `SpinResolver.Weighted()`가 이제 72개를 순회하지만 0-가중치
    항목은 누적합 스캔에서 폭을 전혀 차지하지 않으므로 원본 14종의 절단 지점이 하나도 옮겨가지
    않는다(수학적으로 자명하나 테스트로 고정).
  - **주머니 데이터 — 신규 `Content/Pouch.cs`**: 웹 `POUCH_CAT`(71)·`POUCH_RARITY`(71)·`POUCH_USE`
    (12)·`TIER_BY_RARITY`(5)·`POUCH_SYMBOLS`(71, 순서 그대로 `Symbols71`)·`DEFAULT_UNLOCKED_SYMS`
    (58)·`JACKPOT_TAG`(18)·`JACKPOT_TAG_DECK_MAX`(8)·`DEEP.START_POUCH`(9종/총30)·`DEEP.DECK_MIN/MAX`
    (20/40)·`DEEP.MIN_KINDS`(7)·`DEEP.TAG_MAX_RATIO`(0.60)·`DEEP.CROWN_MAX/WILD_MAX`(2/4)·
    `DEEP.RARITY_MAX`(SILVER10/GOLD6/PRISM2)·`DEEP.COMPRESSION`(22/24/26/28→1.15/1.09/1.06/1.03)·
    `DEEP.EARLY_QUOTA`(stage1~4 램프) 전부 손전사(id 오탈자 방지를 위해 python 정규식으로 data.js를
    직접 파싱해 C# 리터럴을 생성한 뒤 육안 대조 — Tests_P7_1_Pouch.cs의
    `SymbolCatalogCrossReference`가 개수/키집합을 golden으로 재확인). **결정 — 주머니 스캔 순서**:
    C# `Dictionary<string,int>`는 순서 보장이 없어(JS 객체의 삽입 순서 보장과 다름) `PouchOps.
    PouchDraw`의 누적가중치 스캔이 `Pouch.Symbols71`(고정 배열)의 선언 순서를 단일 진실 공급원으로
    쓴다(웹은 `Object.entries(pouch)` 삽입순 — 이 둘은 START_POUCH 9종에 한해 우연히 같은 상대순서라
    시작 상태의 스캔 순서는 사실상 동일하지만, 이후 보상으로 새 종류가 추가되면 갈라질 수 있다. §D
    RNG 결정 원칙 그대로 "자체 재현성만 보장, 비트스트림 일치 불필요"이므로 파리티 위반 아님 —
    Pouch.cs/PouchOps.cs 헤더 각주에 근거 명시).
  - **덱 검증 7규칙 — `Pouch.Validate`**: 총량 20~40·종류≥7·왕관≤2·와일드≤4·특수 티어 상한(SILVER
    ≤10/GOLD≤6/PRISM≤2)·같은 태그≤60%·잭팟태그 특수심볼≤8을 웹 `pouchValidate` 그대로(에러 메시지
    한글 문구 포함, opts로 상하한 오버라이드 가능한 구조까지 — 실제 오버라이드 호출은 P7-2/3 정비소
    슬라이스가 시작). 표시 반올림만 JS `Math.round`(항상 +∞ 방향)와 C# `Math.Round` 기본(은행가
    반올림)이 정확히 .5%p일 때 어긋날 수 있어 `JsRound(x)=Floor(x+0.5)` 헬퍼로 통일(ok/errors 존재
    여부 자체는 이 반올림과 무관 — 순수 표시 문구 정확도 문제, 발견 즉시 수정).
  - **주머니 추출 — 신규 `Run/PouchOps.cs`**: `PouchDraw(rng, pouch, reel, bias=null)`가 웹
    `pouchDraw`/`pouchDrawOne`을 그대로 전사(비중 가중랜덤, "empty"=빈칸, "random"=실심볼만 재추첨
    후 없으면 빈칸 폴백, 총량0/빈 주머니 방어). `PouchBias`(Mul/Add/RareMul) 타입과 분기 골격을
    미리 마련해 뒀다 — 이번 슬라이스는 `SpinResolver.ResolveSpin`이 항상 `bias=null`로 호출해(심화
    퍽 자체가 P7-2/3 범위) 순수 count 비중과 100% 동일하게 동작한다.
  - **심화 런 스핀 진입점 배선**: `SpinResolver.ResolveSpin`의 롤 분기를 `run.DeepMode ?
    DeepRunHooks.ApplyDeepPity(run, PouchOps.PouchDraw(...)) : RollRaw(...)`로 교체(LockedNext 예언
    경로는 기존과 동일 — 웹도 `_pityRoll`이 `_roll(...)` fresh 체인에만 걸림, 예언/오퍼 진입점이
    이 슬라이스에 없어 LockedNext 경로 pity는 P7-2/3로 미룸). Evaluate는 완전히 그대로 재사용(작업
    지시 6번 "세트/잭팟 등 기존 평가는 심볼 확장으로 자연 동작" — `s.exp/s.score/s.coin/s.tags`만
    읽는 범용 로직이라 신규 심볼도 무추가 코드로 기본 이득이 반영된다. `Sp` 신규 51종은
    `Evaluate`의 switch에 case가 없어 NONE과 동일하게 흡수만 되고 특수효과는 발동하지 않는다 —
    웹의 "미지 special은 NONE 취급" 그대로, 진짜 효과는 P7-2/3). VALUE_IDS(cherry/star/book/gem/
    crown)는 무변경 확인.
  - **deepPity — 신규 `Run/DeepRunHooks.cs`**: `ApplyDeepPity(run, raw)`가 웹 `_pityRoll` 그대로
    (자연 등장 시 소진, 미등장 시 무작위 1칸 강제 치환 후 소진, spinsLeft 안전망). 이 슬라이스는
    지급 진입점(오퍼/보상, P7-2/3)이 없어 "상태·치환 로직만 준비"(작업 지시 5번) — 테스트는
    `RunState.DeepPity`를 직접 세팅해 검증한다. `RunState.DeepPity`(신규 `DeepPityState` 참조형,
    null 가능)·`RunState.DeepCompressExtra`(신규, 정비소 누적 요구율 — 이번 슬라이스는 항상 0)도
    함께 추가.
  - **압축 패널티 — `DeepRunHooks.DeepPenalty(run)`**: 웹 `_deepPenalty()`를 `base = compressionPenalty
    (pouchTotal) × (1+deepCompressExtra)` → `× EARLY_QUOTA[stage]` 순서로 그대로 이식하되, 웹 공식의
    나머지 두 항(심볼퍽 penaltyMul "초과분에만 적용" 완화 클램프·전설봉인함 보스요구 25% 감쇄)은
    심볼퍽 자체(웹 `_symMods()`)가 P7-2/3 범위라 존재하지 않는다 — 함수 안에 그 두 항이 들어갈
    자리(`TODO(P7-2/3)` 주석)를 이미 "base → 심볼퍽 배수 → EARLY_QUOTA" 순서로 비워 뒀다. **quota
    배선**: 웹 파리티 P6 때와 동일한 패턴으로 `SpinResolver.QuotaOf`에 5번째 인자
    `deepPenaltyMul=1.0`(기본값, 기존 호출부 100% 호환)을 추가하고 실사용 15곳(SpinResolver
    ResolveSpin 3곳·DeviceActions 6곳·ItemUse 3곳·RunController.HandleContinue·RewardDoneInfo.
    NextPreview·GameSession.PreviewQuotaSpins) 전부에 `DeepRunHooks.DeepPenalty(run)`을 갱신했다
    (asc/bossPhase2 배선 시 다졌던 "트레일링 기본값 + 전 호출부 갱신" 패턴 재사용, grep으로 15곳
    전수 확인).
  - **instant 소모 — 단순화 버전(작업 지시 6번)**: 웹은 붕대/매듭/에너지팩/가짜왕관/진화핵 5종
    각각의 실제 효과(evaluate 내부 분기, P7-2/3)가 발동하는 순간 자기 자신을 -1 한다. 이 슬라이스는
    그 실제 효과가 없으므로 `DeepRunHooks.ConsumeInstantSymbols(run, raw)`가 "`Pouch.Use[id]==
    "instant"`인 심볼이 이번 스핀에 등장한 횟수만큼(중복 등장 포함, 0 하한) 무조건 -1"만 일반화해
    적용한다(fuse 12종 중 "fuse" 값은 이 슬라이스에서 전혀 소비하지 않음 — 조건부 발동 자체가
    없음). **웹 대조 범위 명시(§0 승인 필요 사항 아님, 의도된 단순화)**: 실제 효과가 붙는 P7-2/3에서
    이 함수를 대체/확장해야 이중 소모가 안 생긴다 — 헤더 주석에 통합 지침을 남겼다.
  - **RunState/RunController/GameSession/UI 배선**: `RunState`에 `DeepMode`(bool)·`Pouch`
    (Dictionary<string,int>)·`DeepPity`·`DeepCompressExtra`·`DeepStats`(신규 클래스, 아래 참조) 5필드
    추가. `RunController` 생성자에 `bool deep=false` 트레일링 매개변수 추가 — `run.Asc = deep ? 0 :
    AscMods.Clamp(asc)`로 웹 `startRun(asc,deep)`의 "wantDeep이면 asc 강제 0"을 그대로 재현(§0 결정
    원칙: 승천/심화 상호배제, P6 §2-(Z) 미해결 항목 ③ 완료). deep이면 `Pouch.NewStartPouch()`로
    시작 덱을 채우고 `DeepStats{MaxTotal=...}`를 만든다. `GameSession` 생성자에도 동일하게
    `bool deep=false` 추가(`useAsc = deep ? 0 : ...` — 이중 안전망, RunController가 방어적으로 한 번
    더 강제하므로 실제로는 어느 한쪽만 있어도 안전하다). **UI(모드 진입 배선만, 작업 지시 범위)**:
    `AppRoot.SelectedDeep`(신규, `SelectedAsc`와 나란히) → `StartRun(...,deep)` →
    `PendingLaunchInfo.deep` → `RegisterPlay`의 `new GameSession(...,deep)`까지 값이 흐르도록
    배선했지만, `PickView.OnStartClicked`가 넘기는 값은 `appRoot.SelectedDeep`이고 이 값을 true로
    세우는 UI 컨트롤이 아직 없다(`MenuView` 게임모드 선택기는 P7-4까지 "준비 중" 토스트로 계속
    잠김 — 배선 자체는 끝나 있어 P7-4가 토글만 추가하면 즉시 동작).
  - **DeepStats 골격만(작업 지시 8번)**: 신규 `DeepStats` 클래스(웹 `r.deepStats` 전체 필드 —
    RewardsPicked/Repairs/MaxTotal/BossClears/Compress95Clear 등 8개 불리언 플래그/RaresSeen·
    LegendsSeen 두 집합) — `RunController`가 심화 런 시작 시 `MaxTotal`만 시작 덱 총량으로 채우고
    나머지는 전부 0/false/빈 컬렉션. 실제 증가/갱신(P7-2/3 보상·정비소·보스클리어 훅)과 StatTracker/
    AchievementEngine 소비(P7-4 심화 업적 13종)는 전부 다음 슬라이스.
  - **점수 격리**: `PlayerProfile.BestDeepScore/BestDeepStage`(신규, 웹 defaultProfile
    bestDeepScore:0/bestDeepStage:0 그대로) + `ProfileDto` 왕복 필드(마이그레이션 가드 불필요 —
    AscMax의 -1/0 구분과 달리 기본값 0이 곧 "미달성"이라 dto 미도입 세이브도 자연히 0/0으로
    떨어짐). `StatTracker.ApplyGameOverTracking`을 웹 game.js:2557 `if(deep){...bestDeepScore...}
    else if(asc>0){...bestAscScore...} else p.bestScore=...`와 동일한 순서(deep 우선 — deep이면
    asc가 항상 0으로 강제돼 있어 순서가 바뀌면 asc<=0 분기가 먼저 걸려 deep 런이 bestScore를
    오염시킨다)로 재작성. `BestChar/BestMachine`(Unity 전용 부가 필드) 갱신 게이트에도 `!run.
    DeepMode` 조건을 추가(P6 때는 asc만 게이트했던 것을 이번에 deep까지 확장). `runs`/`totalScore`/
    `bestStage`는 웹처럼 deep 무관 항상 갱신.
  - **asc 상호배제 검증**: `RunController(...,asc:7,deep:true)` → `State.Asc==0` 확인(요청값 무시) +
    `deep:false,asc:7` 대조군은 그대로 7 반영 — `Tests_P7_1_Pouch.DeepModeAscMutualExclusion`.
  - **테스트 — 신규 `Tests_P7_1_Pouch.cs`**: ①신규 58종 손전사 골든(Symbols.cs를 보지 않고 data.js를
    다시 읽어 옮긴 독립 대조축, id/emoji/name/exp/score/coin/special/rare/tags 전수) ②심볼 카탈로그
    ↔ Pouch.Symbols71/Cat/Rarity 교차 대조(개수·키집합·71=72-3(key/dice/seed)+2(empty/random))
    ③START_POUCH golden(9종/총30/새 Dictionary 반환 확인)+자체 validate 통과 ④pouchTotal/
    compressionPenalty 표 8개 경계값 ⑤EARLY_QUOTA 5개 값 + DeepPenalty 배선 4케이스(일반모드
    무영향·stage1 압축전·압축후 손계산·deepCompressExtra 반영) ⑥PouchValidate 7규칙 각각 최소
    1개(가능하면 단독) 위반 케이스 + 경계 정확값(20/40/60%) 통과 확인 + opts 오버라이드 구조 확인
    ⑦PouchDraw 20000회 비중 실측(75/25 분포 ±2%)·같은 seed 결정론·빈 주머니 방어 ⑧empty/random
    특수 처리 3케이스 ⑨deepPity 4케이스(자연소진·강제치환·DeepMode=false 무개입·안전망만료)
    ⑩instant 소모 4케이스(소진시 키제거·fuse 미소비·부분차감·DeepMode=false 무시) ⑪asc 상호배제
    3케이스 ⑫점수격리(deep/asc/일반 3분기 대조군 + BestChar/Machine 오염 없음 확인) ⑬deep 런
    자동플레이 스모크 5시드×20000틱(웹 `_harness.mjs` 동형 불변식 — Score/Coins/StageExp NaN·음수
    없음, Pouch 카운트 음수 없음, 알려진 RunEvent.type 화이트리스트) ⑭심화 런 XP 획득(작업 지시 8번
    첫 절 — 코드 변경 없이 자연 동작함을 `PlayerLevelTracker.ApplyRunEnd` 직접 호출로 재확인).
  - **스모크 컴파일**: Unity 에디터 미실행 확인 후 기존 슬라이스와 동일한 `dotnet exec csc.dll`
    오프라인 검증(`D:\Unity\2022.3.39f1`의 Roslyn csc.dll + NetCoreRuntime\dotnet.exe, Library/Bee의
    직전 성공 컴파일 rsp를 재사용해 신규 3파일만 파일목록에 추가) — `Assembly-CSharp`(런타임, 84개
    — 신규 `Content/Pouch.cs`·`Run/PouchOps.cs`·`Run/DeepRunHooks.cs` 포함, `Scripts/Game/
    GameSession.cs`·`Scripts/UI2/AppRoot.cs`·`PickView.cs` 등 이번에 수정한 UI2/Game 파일도 이
    rsp 하나로 전부 커버)·`Assembly-CSharp-Editor`(6개, 무변경) 둘 다 0에러·0경고.
  - **웹 대비 생략/이월 — 전부 작업 지시 §범위 그대로(보고 대상 아님, 계획된 다음 슬라이스)**: ①
    심볼퍽(SYM_AUGMENTS/SYM_RELICS 21+15종) ②정비소(REPAIR_SERVICES 11종, 상점 심화 탭) ③전공
    아키타입 6계열(ARCH_T1/T2 발동) ④잭팟 태그 6종의 실제 발동(잭팟 판정 시 태그 매칭 — 이번
    슬라이스는 덱검증 상한 계산에만 `JackpotTag`/`JackpotTagDeckMax` 사용, evaluate 개입 없음)
    ⑤피버 게이지(FEVER_*) ⑥POUCH 오퍼 2-step(RANDPACK_*/DESIGNATED_UPGRADE_CHANCE/PACKAGE_CHANCE)
    ⑦심볼 해금(ACH_SYMBOL_UNLOCK 13종, profile.symUnlocked) ⑧심화 업적 13종/장치 9종(P7-4) ⑨UI
    보드(주머니 시각화·정비소 화면·심화 HUD, P7-4) ⑩랭킹 3노드 분리(P6~P7 연동, P7-4) ⑪MenuView
    게임모드 선택기 잠금 해제(작업 지시 명시 유지 — 주석은 이미 있고 이번엔 실배선까지만).
  - **🚧 P7-2/3 착수 전 선행 blocker(Opus 2차검수 확정, 아래 반영 내역 참조) — 장치 경로 DeepMode
    미적용 + LockedNext 센티널 왕복 재설계**: 장치(예언 `dev_oracle`/`dev_syllabus`·재굴림
    `dev_reroll`/`GamblerReroll`·MANIP `dev_pin`/`dev_copy`/`dev_swap`) 경로는 이번 슬라이스에서
    `run.DeepMode`를 전혀 보지 않는다 — `DeviceActions.HandlePeek`/`HandleManip`/`GamblerReroll`은
    여전히 `SpinResolver.RollRaw`(일반 가중추첨)로 셀을 만든다. 메인 스핀 경로
    (`SpinResolver.ResolveSpin`)만 주머니 추출로 전환하라는 작업 지시 2번 문면("심화 런의 스핀
    굴림이... 분기") 그대로의 범위 해석이지만, 실제 플레이에서는 심화 런 중 이 장치들을 쓰면
    주머니에 없는 심볼이 섞여 나올 수 있다는 뜻이다. 근본적으로는 `RunState.LockedNext`(문자열 id
    리스트)가 `CellsFromIds`(→`Symbols.ById`)로만 복원돼 "empty"/"random" 같은 주머니 전용
    센티널을 왕복시키지 못하는 구조적 제약도 있어(pouch draw 결과에 "empty"가 섞이면
    `CellsFromIds`가 그 칸을 통째로 누락시켜 셀 개수가 줄어드는 별도 버그까지 유발할 수 있음),
    단순히 `run.DeepMode` 분기만 추가해서는 안전하게 고칠 수 없다 — **장치/정비소가 본격적으로
    들어오는 P7-2/3는 이 두 가지(장치 경로 DeepMode 인식 + LockedNext의 empty/random 센티널
    왕복 재설계)를 먼저 해결하고 시작해야 한다**(선행 blocker로 명시, 스코프 누락이 아니라 다음
    슬라이스의 0순위 작업 항목).
  - **Opus 2차검수 반영(2026-08-09, HIGH2·MED2·LOW일괄 — 아래 5건)**:
    ①**[HIGH] `ReelView.RandomSymbol()` 필러 범위 회귀**: `Symbols.All`이 72종으로 늘면서 이 함수
    (릴 스핀 중 지나가는 필러·니어미스·첫 프레임 채움, 호출부 5곳)가 균등 난수로 72종 전체에서
    고르다 보니 신규 58종(스프라이트 없음)이 뽑힐 때마다 빈 사각형이 보이는 회귀가 생겼다(58/72≈
    80%) — `UnityEngine.Random.Range(0, syms.Length)`를 `Range(0, Symbols.LegacyCount)`로 한정해
    실제 등장 가능한 14종 안에서만 고르도록 수정.
    ②**[HIGH] `UiSceneBuilder` 스프라이트 굽기 오염**: 타이틀 화면 장식 릴(`TitleBuildResult.
    symbolSprites`, :795-798) — 소비처 `TitleView.SymbolCycleLoop()`가 null 스프라이트를 걸러내지
    않고 그대로 대입해(TitleView.cs:149) ①과 동일한 "타이틀 릴 80% 빈칸" 버그를 유발했다.
    `Symbols.LegacyCount` 범위로 한정 + null 방어(List로 모아 압축)로 수정. 게임 릴(`ReelView.
    symbolSprites`, :1973-1977, id-keyed 튜플) 쪽은 `ReelView.Awake()`가 이미 null을 걸러내
    (ReelView.cs:225) 기능 버그는 아니었지만, 씬 리빌드마다 58개를 굽고 버리는 낭비("다음 씬
    리빌드 오염")라 동일하게 LegacyCount로 한정.
    ③**[MED] instant 소모 웹 정합**: 웹 game.js:814-828을 재확인한 결과 evaluate()가 뽑은
    "이번 스핀에 등장했는가"(불리언 플래그, 개수 아님)만 보고 `remove n:1`을 **id당 최대 1회만**
    적용한다 — 같은 instant 심볼이 5칸 중 여러 번 나와도 덱에서는 정확히 1개만 빠진다.
    `DeepRunHooks.ConsumeInstantSymbols`가 등장 횟수만큼 매번 -1 하던 것을(예: knot 2회 등장 시
    5→3) `HashSet`으로 이번 스핀에 처리한 id를 추적해 id당 1회로 정정(5→4). 골든 테스트
    (`Tests_P7_1_Pouch.InstantSymbolConsumption`)도 5→4로 재산출하고, 서로 다른 instant id 2종이
    각각 독립적으로 최대 1개씩만 빠지는 케이스를 추가했다.
    ④**[MED] 어서션 수치 정정**: §2-(Z)(P6 최종)가 남긴 마지막 공식 기록은 20164였다 — 이 슬라이스는
    그 값을 기준선으로 삼는다(아래 최종 수치 참조).
    ⑤**[LOW 일괄]**: `Tests_S5_ProfileDtoRoundTrip`에 `AscMax`/`BestAscScore`/`BestAscLevel`/
    `BestDeepScore`/`BestDeepStage` 왕복 검증(값 케이스 + 빈 프로필 기본값 케이스) 추가 — 그동안
    이 5필드는 각자 도입 슬라이스(P6/P7-1)의 전용 테스트에서만 개별 검증되고 공용 DTO 왕복 테스트엔
    없었다. `PouchOps.PouchDraw`가 `Pouch.Symbols71` 밖 id를 구조적으로 무시함을 헤더 주석으로
    명문화하고 `PouchDrawIgnoresUnknownIds` 테스트로 직접 실증(미지 id에 압도적 카운트를 줘도 절대
    안 뽑힘 + `Pouch.Total()`은 그 카운트까지 합산해 "실제 뽑히는 총량"과 갈라질 수 있음을 함께
    확인). `PouchValidateRules`에 "정확히 상한" 통과 케이스 6종 추가(왕관==2·와일드==4·SILVER
    특수==10·GOLD 특수==6·PRISM 특수==2·잭팟태그 특수==8, 전부 strict `>` 비교라 경계값 자체는
    통과해야 함을 각각 확인).
    **재검증**: `dotnet run --project Client/Jackpot/Tools/EngineTests` 20164 → 22758(+2594),
    0 실패. 오프라인 스모크 컴파일도 재검증(`Assembly-CSharp` 84개·`Assembly-CSharp-Editor` 6개
    둘 다 0에러·0경고) — 이 과정에서 스모크 절차 자체의 맹점 하나를 발견해 함께 고쳤다: Editor rsp가
    참조하는 `Assembly-CSharp.ref.dll`은 항상 Library/Bee 캐시의 **원본 경로**(내 스크래치 `-refout`
    산출물이 아니라, 최후로 실제 Unity 에디터가 컴파일해 둔 스테일 dll)를 그대로 가리킨다 — 이번
    슬라이스 최초 스모크(이 review 이전)때는 Editor 쪽 코드가 P7-1 신규 API를 전혀 참조하지 않아
    우연히 문제가 안 됐지만, 이번 반영①②로 `UiSceneBuilder.cs`가 신규 `Symbols.LegacyCount`를
    참조하게 되자 그 스테일 dll엔 그 상수가 없어 `CS0117`로 즉시 걸렸다. Editor rsp의
    `-r:".../Assembly-CSharp.ref.dll"`을 이번에 새로 빌드한 런타임 스크래치 ref dll로 가리키도록
    정정한 뒤에야 진짜 0에러가 확인됐다 — **향후 슬라이스가 Editor 코드에서 그 회차의 신규 Engine
    API를 참조하게 될 때마다 이 rsp 참조 정정을 함께 챙겨야 한다**(안 그러면 런타임 어셈블리는
    최신인데 Editor 스모크만 조용히 스테일 레퍼런스로 "우연히 통과"할 위험이 있음, 이번처럼 매번
    걸리리라는 보장이 없다).

- **(BB) 2026-08-09 완료(P7-2 "심화모드(심볼 덱/주머니) 2/4" — WEB_PARITY_DESIGN.md §1-A #19 2/4
  슬라이스: 심볼증강21+심볼유물15+레벨·전공 아키타입6·정비소11 + §2-(AA) 선행 blocker 해소. 웹
  `data.js` SYM_AUGMENTS/SYM_RELICS/SYM_AUG_LEVELS/POUCH_UPGRADE/DEEP.REPAIR_SERVICES·`engine.js`
  symPerkMods/ARCHETYPES/pouchArchetype/archetypeMods/applyShopService/repairBounds·`game.js` _mods
  심화블록(457-531)/_deepPenalty/_repairPriceMul·_repairPrice/repairBuy/_purifyEmptyToBasic/
  _checkArchetype/_roll/manip/oracle/_freeReroll 전수 대조)**:
  - **§0 선행 blocker 해소**:
    - **굴림 진입점 통합**: `SpinResolver.RollCells(run,mods,reel,seedActive)`/`RollCellOne(run,mods)`
      신규 — DeepMode면 mods.symbolWeightMul/weightAdd/rareWeightMul을 `DeepRunHooks.BuildPouchBias`로
      변환해 `PouchOps.PouchDraw`를, 아니면 기존 RollRaw/RollOne을 그대로 호출한다(웹 `_roll()`의
      `_reachBias`(리치 태그 bias)·RETRY_REEL 재도전은 잭팟 태그 실제 판정 자체가 P7-3 범위라 제외).
      `DeviceActions.HandlePeek`(+ApplyDeepPity, 웹 oracle()이 pity를 태우는 유일한 예외 경로)·
      `HandleManip`(dev_reroll/dev_pin)·`GamblerReroll`·`ItemUse.UseRetakeForm`·
      `ItemUse.ApplyItemPurchase`(timeline_ticket, 웹은 애초에 deepMode 아이템 풀에서 제외하지만
      Unity는 아직 그 필터가 없어 방어적으로 통합)까지 전부 이 헬퍼로 수렴 — 이전에는 전부 RollRaw/
      RollOne(일반 가중추첨, 72종 전체)을 직접 호출해 심화 런에서도 주머니 밖 심볼이 섞여 나올 수
      있었다.
    - **LockedNext 센티널 왕복**: `SpinResolver.CellsFromIds(ids, rng=null, pouch=null)`에 트레일링
      선택 인자 추가 — "empty"는 `EmptySym`으로, "random"은 rng/pouch가 있으면 `PouchOps.DrawOne`로
      완전히 재해석(생략 시 안전하게 빈칸 대체)하고, **그 외 미지 id도 더 이상 드롭하지 않고
      EmptySym으로 대체**한다(입력 id 개수 = 출력 칸 개수를 구조적으로 보장). `PouchOps.DrawOne`을
      `private`→`internal`로 승격해 중복 정의 없이 재사용. `ResolveSpin`의 LockedNext 복원 호출에
      `run.Rng`/`run.Pouch`를 넘기도록 갱신. 테스트: `BlockerCellsFromIdsRoundTrip`(단위) +
      `BlockerPeekThenSpinKeeps5Cells`(PEEK로 empty 포함 LockedNext를 만들고 다음 스핀까지 5칸 유지
      확인, 작업 지시 "심화 런 PEEK→다음 스핀 5칸 유지" 그대로).
  - **A. 심볼증강21+심볼유물15+레벨 — 신규 `Content/SymPerks.cs`**: `SYM_AUGMENTS`(실버6/골드7/
    프리즘8)·`SYM_RELICS`(실버7/골드8, 프리즘 없음)·`SYM_AUG_LEVELS`(8종 Lv2/3 델타) 데이터 전사 +
    `SymPerks.ComputeMods(perkIds,pouch,levels)`가 웹 `symPerkMods` 21개 훅 카테고리(repairMul/
    repairRefund/swapCoin/tagBuff/penaltyMul/skullPenaltyMul/boundDelta/boundOverride/rewardBonus/
    addBoost/shopLab/purifyCoin/purifyToBasic/emptyScore/emptyExp/autoShred/bossCopy/compressScore/
    rareFirstScore/legendSeal/display)를 전부 그대로 집계한다(일반 ModsBuilder.Build와 완전히 분리된
    순수 함수, 웹 구조 그대로). **결정 — 보유 저장소는 신규 필드가 아니라 기존 RunState.Perks/
    PerkLevels 재사용**: 작업 지시 문면은 "RunState.SymPerks(보유)·SymAugLevels(레벨8종)" 신규
    필드를 언급했지만, 웹 정답지를 대조한 결과 웹은 실제로 `E.symPerkMods(r.perks, r.pouch,
    r.perkLevels)`처럼 일반 증강·유물·저주와 **완전히 같은 배열/딕셔너리**를 공유한다(`_heldSymPerks()`
    도 `r.perks.filter(id=>SYM_PERK_BY_ID[id])`). id 네임스페이스가 sa_/sp_/sr_ 접두로 전역
    유일해 안전하게 공유 가능하고, `ModsBuilder.Build`의 퍽 루프도 `Perks.ById(symId)`가 null이라
    자동으로 건너뛴다(무회귀, `SymPerkModsIgnoresNonSymIds` 테스트로 확인). CLAUDE.md §0 "충돌 시
    웹 채택이 기본" 원칙에 따라 웹 구조를 그대로 따르기로 결정 — Content/SymPerks.cs·RunState.cs
    헤더에 근거 명시(이탈 사항, 보고 대상).
    - **배선(작업 지시 2번째 불릿)**: ①`DeepRunHooks.DeepPenalty`가 이제 심볼퍽 `PenaltyMul`을
      "초과분에만" 적용(`Max(1, 1+(base-1)×sp.PenaltyMul)`)하고 보스 스테이지에서 `BossQuotaMul`
      (전설봉인함 `LegendSeal` 보유 시 증가분 25% 감쇄)까지 곱한다 — 웹 `_deepPenalty()` 전체 공식을
      드디어 완성(P7-1이 남긴 TODO 두 항 모두 해소). ②pouch bias는 위 §0 blocker의 `RollCells`/
      `BuildPouchBias`가 그 자리(symPerkMods의 `addBoost`가 아니라 **기존 mods.symbolWeightMul/
      weightAdd/rareWeightMul**임을 웹 `_roll()` 코드 대조로 확인 — `addBoost`(basic/rareChance/
      legendWeight)는 POUCH 오퍼 카드 생성 전용이라 P7-3 범위, 값은 계산되지만 아직 소비처 없음).
      ③`skullPenaltyMul`은 `DeepRunHooks.ApplyDeepMods`가 곱한다(아래 B 참조, `Mods.skullPenaltyMul`은
      기존에 "죽은 필드"였는데 이번에 부활 — Mods.cs 헤더 주석 갱신). ④정비소 소비(`repairMul`/
      `repairRefund`/`swapCoin`/`purifyCoin`/`purifyToBasic`/`boundMaxDelta`~`tagCap`)는 아래 C.
      **미배선(계산은 정확하나 소비처 없음, 다음 슬라이스)**: `deepTagMul`(tagBuff)→evaluate 태그곱,
      `emptyScore`/`emptyExp`→evaluate 빈칸 블록, `scoreMul`(purifyToBasic 부수), `quotaMul`
      (shopLab, sp_deckslot 전용)→mods.quotaMul, `rewardBonus`/`addBasicDelta`/`legendWeight`/
      `curseChance`/`shopLabWeight`/`alwaysRepair`/`bossCopyN`/`autoShredN`/`rareFirstScore`/
      `compressScorePct`/`balanceScore`/`statTable` — 전부 POUCH 오퍼(P7-3)·보스클리어 훅(P7-3)·
      상점 노드 가중(P7-3)·UI(P7-4) 소비처가 아직 없다. 코드에 TODO 주석으로 소비 지점을 명시.
  - **B. 전공 아키타입 6계열 — 신규 `Content/Archetypes.cs`**: `ARCHETYPES`(cherry🍒/book📘(tag:학습)/
    gem💎/skull☠/coin🪙/flame🔥) + 임계(`ArchT1`0.25/`ArchT2`0.40) + `PouchArchetype`(최대 share
    단일 전공, 동률은 선언순서) + `ArchetypeMods`(exp/score계열 +15/30%·coin계열 +10/20%·강령학파
    t2 skullPenaltyMul 0.5) 전부 웹 그대로. **평가 반영 지점**(작업 지시 명시)까지 완성 —
    `Mods.deepFamilyExpMul/ScoreMul/CoinMul`(신규 Dictionary<string,double> 3종) 추가하고
    `DeepRunHooks.ApplyDeepMods(mods,run)`(신규, AscRunHooks.ApplyRunAscMods 직후 5곳: ResolveSpin·
    HandleManip·GamblerReroll·UseRetakeForm·timeline_ticket에서 호출)이 활성 아키타입의 곱을 그
    최종 mods에 주입한다. `SpinResolver.Evaluate`에 `ArchMul(map,sid)`(웹 `famBase`/`archMul` 그대로
    — 상위계열 id는 `Pouch.UpgradeParent`로 base 환산 후 조회) 헬퍼를 추가해 셀 EXP/점수/코인 3곳 +
    ☠해골 EXP 가산분(강령학파)에 곱을 반영 — 웹 engine.js:685/690/693/696과 1:1 대응. 부수 변경:
    `coins` 지역 누산을 `int`→`double`(coinsAcc)로 바꾸고 최종 1회만 `int`로 캐스트(아키타입 코인곱이
    셀당 소수 배율을 낼 수 있어 웹처럼 절삭을 마지막에 1번만 하도록, 이전엔 매 칸 절삭 오차 누적
    위험). **범위 밖**: hex_allornothing 저주의 심화 전용 dEff(`archetypeMul:0.5`, 전공 배율 절반
    재설계)는 "dEff"(deepMode 전용 효과 오버라이드) 메커니즘 자체가 Unity에 없어 미이식 —
    Archetypes.cs 헤더 각주에 근거·향후 연결 지점 명시.
  - **C. 정비소 11 서비스 — 신규 `Content/RepairServices.cs` + `Run/RepairShop.cs`**: 서비스 카탈로그
    11종(addBasic/addHigh/addRare/remove/swap/upgrade/purify/expand/compress/tagbuff/curseCleanse)
    데이터 그대로. `RepairShop.Execute(run,serviceId,args)`(신규 `RunAction.RepairBuy`로
    `RunController.Do`에 배선)가 웹 `applyShopService`+`repairBuy` 커밋을 하나로 합쳐 실행: 심볼
    카운트형은 `Pouch.ApplyReward`(신규, 웹 `applySymbolReward` 그대로 — add/remove/swap/upgrade,
    `Pouch.Upgrade`/`UpgradeParent`(신규, 웹 POUCH_UPGRADE 6종) 활용) + `Pouch.Validate`(현재 유효
    상한·하한·태그비중, `RepairShop.Bounds`가 정비소 누적 델타+심볼퍽 boundMaxDelta/MinDelta/
    boundMax/boundMin/tagCap 합산)로 검증 — **위반/변화없음이면 코인 미차감 거부**. 심볼증강 할인은
    `RepairShop.PriceMul`(repairMul["*"]×repairMul[kind])이 실가격에 반영. 성공 시 부수효과(교체
    코인/환급·정화 코인·정화전문가 랜덤기본승격·deepPity 예약)까지 웹 그대로. `RunState`에
    `DeepTotalMaxDelta`/`DeepTotalMinDelta`/`DeepTagBuff`(신규, expand/compress/tagbuff 누적) +
    `DeepArchFamily`/`DeepArchTier`(신규, 전공 발동/승급 감지 스냅샷) 추가. `DeepRunHooks.
    CheckArchetypeChange`(신규)가 매 정비 구매 후 전공 변화를 감지해 `RunEvent{type="ARCHETYPE_
    CHANGED"}`를 반환(발동/승급만 알림, 소멸은 무음 — 웹과 동일). `RunEvent`에 `repairServiceId`/
    `repairPrice`/`archFamily`/`archTier`/`archUpgraded` 필드 추가(curseCleanse는 기존
    `curseRemovedId` 재사용). **RunPhase/상점 탭 통합은 계획대로 P7-4** — 이 슬라이스는 엔진 API와
    테스트만.
  - **테스트 — 신규 `Tests_P7_2_SymPerks.cs`**: ①심볼퍽 36종(21+15) 골든(데이터 재전사, 독립 대조축)
    +레벨 8종 골든 ②symPerkMods 집계 손계산 5개 조합(repairMul 3중첩·tagBuff MOST/MAJOR/SOLO
    3중첩·penaltyMul 임계 경계(27/28)·addBoost+legendSeal·레벨 적용) + 일반 id 혼입 무시 확인
    ③아키타입 임계 경계(0.24/0.25/0.40)·동률 tie-break·book(tag:학습) 계열·보너스 수치 6종 +
    Evaluate 아키타입 곱 반영 6케이스(exp base+상위계열·score·coin·skull분기) + DeepPenalty 전체식
    (bossQuotaMul+legendSeal) ④정비소 11종 각 실행(성공/거부/코인 — insufficient/invalid-pouch/
    no-change/missing-arg) + 심볼퍽 부수효과(교체·정화)·가격할인·전공 이벤트 발동 ⑤§0 blocker:
    CellsFromIds 왕복·PEEK→스핀 5칸 유지·PEEK/MANIP/도박꾼재굴림/재시험 pouch 전용 등장(각 20회
    반복) ⑥심화 자동플레이 스모크(3시드×20000틱, RepairBuy를 확률적으로 섞어 REPAIR_DONE/
    ARCHETYPE_CHANGED 이벤트까지 실사) — 총 1100개 어서션 추가.
  - **재검증(1차 제출 시점)**: `dotnet run --project Client/Jackpot/Tools/EngineTests` 22758 →
    **23858**(+1100), 0 실패. 오프라인 스모크 컴파일도 재검증(Unity 에디터 미실행 확인 후
    `dotnet exec csc.dll`, Library/Bee의 직전 성공 rsp에 신규 4파일만 추가) — `Assembly-CSharp`
    (런타임, 88개 — 신규 `Content/Archetypes.cs`·`Content/RepairServices.cs`·`Content/SymPerks.cs`·
    `Run/RepairShop.cs` 포함)·`Assembly-CSharp-Editor`(6개, Editor rsp의 `-r:".../Assembly-CSharp.
    ref.dll"`을 이번에 새로 빌드한 런타임 스크래치 ref dll로 정정, §2-(AA) 후기 절차 그대로 재확인)
    둘 다 0에러·0경고.
  - **Opus 2차검수 반영(2026-08-09, "됐다고 문서화됐지만 실제 미배선" 3건 실배선 + 권장 2건)**:
    ①**[필수] 심볼퍽 skullPenaltyMul 실배선**: 1차 제출은 `SymPerks.ComputeMods(...).SkullPenaltyMul`
    (sa_expand_build 0.7·sr_big_bag 0.8)을 계산만 하고 `mods.skullPenaltyMul`에 실제로 곱하지
    않았다(아키타입 쪽 skullPenaltyMul만 배선돼 있었음) — `DeepRunHooks.ApplyDeepMods`가 이제
    `mods.skullPenaltyMul *= sp.SkullPenaltyMul`을 아키타입 곱보다 먼저 적용(웹 game.js:474
    `(mods.skullPenaltyMul??1)*(sp.skullPenaltyMul||1)` 그대로). 검증:
    `SkullPenaltyMulFromSymPerkAppliesInEvaluate`(sa_expand_build 보유 시 해골 페널티가
    0.7배로 감쇄되는 것을 Evaluate() 실행 결과로 직접 손계산 대조, perk 없는 대조군과 병행).
    ②**[필수] RepairBuy 페이즈 게이트**: `RepairShop.Execute`에 `run.Phase != RunPhase.EventShop`
    시 `"PHASE_NOT_SHOP"` 거부를 추가(Shop.Buy/Reroll/Leave 3곳과 동일 관례, Shop.cs 대조 확인) —
    1차 제출은 이 게이트가 아예 없어 Spin/PostSpin 등 아무 phase에서나 정비 구매가 통과됐다.
    `RepairPhaseGate` 테스트 신규 + 나머지 성공 케이스 전부 신규 `MakeDeepRunInShop` 헬퍼(Phase.
    EventShop 세팅)로 전환. 자동플레이 스모크의 RepairBuy 호출 지점도 Spin phase(잘못된 경로)에서
    EventShop phase 진입 시(BuyOffer 대신 확률적으로 시도)로 이동.
    ③**[필수] DeepTagBuff 소비처 배선**: 신규 `Mods.deepTagMul` 필드 추가 + `DeepRunHooks.
    ApplyDeepMods`가 웹 game.js:454(정비소 '태그 강화' `run.DeepTagBuff` 직접 복사, pouch/심볼퍽과
    무관하게 최우선 적용) → game.js:464-465(심볼퍽 tagBuff류 `sp.DeepTagMul`을 그 위에 태그별 가산
    병합) 순서 그대로 채운다. 소비 지점은 `SpinResolver.Evaluate`에 신규 삽입(웹 engine.js:679-683
    그대로 — 셀이 가진 모든 태그의 배수 합을 ±50%로 클램프해 `cellExp`에 곱함, 아키타입 곱보다
    먼저 적용). 1차 제출은 `run.DeepTagBuff`를 쓰기만 하고 아무도 읽지 않아 sv_tagbuff(22코인)가
    순수 장식이었다. 검증: `TagbuffPurchaseAffectsEvaluate`(체리 5개 잭팟+세트보너스 조합으로 절삭
    경계를 넘는 명확한 EXP 델타 손계산, 235→236).
    ④**[권장] sv_add_high/sv_add_rare 실행 테스트 추가**: `RepairAddHighSuccessAndPity`(cherry_ripe,
    고급)·`RepairAddRareSuccessAndPity`(wild, 희귀) 신규 — 기존엔 11종 중 addBasic만 직접 실행
    검증됐고 addHigh/addRare는 골든 데이터로만 존재했다.
    ⑤**[권장] ApplyDeepMods 가산→대입 전환 + 빈 주머니 조기 반환 정리**: 신규 `Mods.DeepModsApplied`
    가드로 `ApplyDeepMods`를 이중 호출 안전하게 만들고(Clone()도 이 플래그를 복사), 그 안의
    `deepFamilyExpMul/ScoreMul/CoinMul` 딕셔너리 채움은 (이제 1회 보장이라) 가산이 아니라 대입으로
    단순화했다. `ApplyDeepMods`·`CheckArchetypeChange` 둘 다 있던 `run.Pouch.Count==0` 조기 반환도
    제거 — 웹 `if (r.pouch)`는 빈 객체 `{}`도 truthy라 항상 통과하고, `SymPerks.ComputeMods`/
    `Archetypes.PouchArchetype` 둘 다 빈/0-총량 pouch를 이미 안전한 중립값(Family=null 등)으로
    처리하므로 별도 가드가 애초에 불필요했다 — 오히려 `CheckArchetypeChange`의 조기 반환은 주머니가
    완전히 비워지는 극단 케이스에서 직전 전공 상태가 리셋 안 되고 스테일하게 남는 실버그였다(웹은
    자연히 `pouchArchetype({})→family=null`로 리셋됨). 검증:
    `CheckArchetypeChangeResetsOnEmptyPouch`.
    **LOW 잔여(이번 슬라이스 미착수, 후속 슬라이스 항목으로만 기재)**:
    - **dev_pin(고정) MANIP의 RNG 소비 위상**: 웹 manip() "고정"은 5칸 전체를 한 번에 새로 굴린 뒤
      keep 위치만 원래 칸으로 되돌리는 방식(`this._roll(mods,false)` 1회, 5칸 전부 RNG 소비)인데,
      Unity `HandleManip`은 keep 제외 자리마다 `RollCellOne`을 개별 호출해(4칸만 RNG 소비) 소비
      횟수·위치 대응이 웹과 다르다. P7-1 이전부터 있던 Kotlin 이식 특성이라(§ENGINE_PORT_DESIGN.md
      "RNG 소비 순서는 자체 재현성만 보장, 비트스트림 일치 불필요") 이번 슬라이스가 새로 만든 문제는
      아니지만, RollCellOne으로 전환하며 심화 런에서도 이 패턴이 그대로 노출됐다 — 자체 재현성엔
      문제없어 방치, 필요 시 후속 슬라이스에서 "웹처럼 5칸 한 번에 굴려 splice" 방식으로 정렬 가능.
    - **`SymPerks.EffWithLevel`의 화이트리스트 의존**: `IsLevelable(id)`가 `AugLevels` 딕셔너리(8종
      고정)에 있는 id만 레벨 델타를 적용한다(웹 `isSymAugLevelable`과 1:1 동치 — by-design 제약).
      향후 9번째 이상 레벨업 가능 심볼퍽이 추가되면 `AugLevels`에 함께 등록해야 자동 반영된다는 점을
      명시.
    - **`legendStable` 미이식**: 웹 `mods.legendStable`(sr_legend_seal/dev_legend_seal이 세우는
      플래그 — 프리즘/럭키7 랜덤효과를 "최선 효과 고정"으로 안정화)은 그 소비처인 Sp.PRISM_SYM/
      Sp.LUCKY7 특수효과 자체가 Unity에 아직 없어(§Sp 신규 51종 이월 항목) 만들 자리가 없다 —
      `SymPerkModsResult.LegendSeal`(bool)까지는 정확히 계산되지만 `mods.legendStable` 필드도,
      그걸 읽는 evaluate 분기도 존재하지 않는다. Sp 신규 51종 실제 효과 이식 슬라이스에서 함께.
    - **정비소 addBasic/addHigh/addRare의 rarity 게이팅은 UI 몫**: `RepairShop.ServiceToReward`는
      `args.Id`가 서비스 선언 희귀도(`sv.rarity`)와 실제로 일치하는지 검증하지 않는다 — 웹
      `applyShopService`도 동일하게 무검증이며, 대상 후보를 희귀도로 좁히는 건 웹 `repairTargets()`
      (UI 드롭다운)의 몫이다. Unity도 동일 분담을 그대로 따른다(P7-4 UI가 후보 목록을 필터링).
  - **재검증(Opus 반영 후 최종)**: `dotnet run --project Client/Jackpot/Tools/EngineTests`
    23858 → **23881**(+23, 누적 22758 대비 +1123), 0 실패. 오프라인 스모크 컴파일 재확인
    (Mods.cs/DeepRunHooks.cs/SpinResolver.cs/RepairShop.cs 수정분 포함) — 런타임 88개·Editor 6개
    둘 다 0에러·0경고.
  - **웹 대비 생략/이월 — 전부 작업 지시 §범위 그대로(보고 대상 아님, 계획된 다음 슬라이스)**: ①
    symPerkMods 중 evaluate/quota/오퍼에 여전히 안 꽂힌 필드들(skullPenaltyMul·deepTagMul은 Opus
    반영으로 배선 완료 — repairMul/repairRefund/swapCoin/purifyCoin/purifyToBasic/boundMaxDelta~
    tagCap은 정비소(C)가 이미 소비. 나머지 — rewardBonus/addBasicDelta/rareChance/legendWeight/
    curseChance/shopLabWeight/alwaysRepair/bossCopyN/autoShredN/rareFirstScore/compressScorePct/
    balanceScore/statTable — 는 POUCH 오퍼·보스클리어 훅·상점 노드 가중·UI가 아직 없어 미소비)
    ②잭팟 태그 6종의 실제 발동(evaluate 판정 — 이번 슬라이스도 덱검증 상한 계산에만 사용) ③피버
    게이지 ④POUCH 오퍼 2-step(RANDPACK_*/DESIGNATED_UPGRADE_CHANCE/PACKAGE_CHANCE) ⑤심볼 해금
    13종(profile.symUnlocked) ⑥심화 업적 13종/장치 9종(P7-4) ⑦UI 보드(심볼퍽 표시·정비소 화면·전공
    HUD·토스트, P7-4) ⑧랭킹 3노드 분리(P7-4) ⑨hex_allornothing dEff(위 B 각주) ⑩Sp 신규 51종의
    실제 특수효과(CATALYST/MIRROR/TARGET/PUZZLE5/ALARM 등, evaluate에 아직 case 없음 — P7-1부터
    이어지는 이월 항목, 이번 슬라이스도 미착수) ⑪위 Opus 2차검수 LOW 잔여 4건(dev_pin RNG 위상·
    EffWithLevel 화이트리스트·legendStable 미이식·rarity 게이팅 UI 몫).

- **(CC) 2026-08-09 완료(P7-3 "심화모드(심볼 덱/주머니) 3/4" — WEB_PARITY_DESIGN.md §1-A #19 3/4
  슬라이스: 잭팟태그6종+피버게이지+자동소멸+POUCH오퍼v3(2-step)+심화노드풀+퍼펙트드로우+3스테이지연계
  보너스. 웹 `engine.js` 잭팟태그(§768-870)·offerSymbolRewards(§1725-1863)·isDeepCompat/deepCompatPool
  (§1993-2002)·symAugPool/symRelPool(§2189-2198)·`data.js` FEVER_*/BELL_FEST_MUL(§1136-1155)·
  REL_MIN/REL_MIN_BY_SYM(§1071-1077)·AUGMENTS/RELICS의 deep/dSym/dDesc(§229-494)·`game.js` 스핀 후속
  처리(§960-1138)·심화 노드 풀(§1439-1494)·자동소멸(§1516-1542)·POUCH 2-step 커밋(§1890-2037)·
  3스테이지 연계(§2152-2184) 전수 대조)**:
  - **① 잭팟 태그 시스템 — `SpinResolver.Evaluate`에 신규 블록**: 5칸의 `Pouch.JackpotTagOf` 카운트
    (와일드·빈칸 미기여) → 최다 태그 3단계(콤보3=EXP+8·리치4=점수+300·태그잭팟5=EXP+30·점수+1500)
    판정 그대로. 동일심볼 잭팟(`jackpotSym`)과 공존 시 태그잭팟 EXP/점수는 스킵(신호만 반환) — 웹
    `if (!jackpotSym) {...}` 그대로. 증폭 심볼 4종(환호 CHEER ×1.25·대폭죽 BIG_BOOM 콤보+500/잭팟+2000·
    슬롯조각 SLOT_SHARD/잭팟마법봉 JACKPOT_WAND 최다태그+1(마법봉은 prism 제외)·잭팟왕관 JACKPOT_CROWN
    신호)·종세트 추가 충전(작은종+15·황금종+30·울림종 리치 시 점수+200) 전부 이식. `Mods.deepMode`
    신규 필드(웹 `mods.deepMode`) — `DeepRunHooks.ApplyDeepMods`가 심화 런에서만 true로 세워 이 블록을
    게이팅(일반 런 완전 격리, `SpinResult`에 jackpotTagHit/jackpotStage/feverDelta/bellCount/
    echoTriggered/jackpotCrownSignal/hasBellFest/hasReachMark/hasRetryReel/hasJpTicket 10개 신규
    필드 반환).
  - **② 리치 bias — `SpinResolver.RollCells`**: 웹 `_roll()`의 `_reachBias`(리치 태그 심볼 ×1.5, 1스핀)를
    `DeepRunHooks.ApplyReachBias`로 이식(P7-1/P7-2가 명시적으로 미룬 항목 해소) + 재도전릴 후처리(리치
    다음 스핀 1칸 재굴림)까지 `RollCells` 안에서 완결. **범위 제한**: `RollCellOne`(MANIP 등 1칸씩
    굴리는 호출부)에는 확장하지 않았다 — dev_pin RNG 소비 위상이 이미 웹과 다르다는 선례(§2-(BB) LOW
    잔여)가 있어, 리치bias/재도전릴을 거기까지 얹으면 스핀당 다중 소진 위험만 커진다(범위 제한, 보고
    대상).
  - **③ 피버 게이지 — `DeepRunHooks.ProcessDeepSpinFollowups`**: 웹 game.js:960-1138 스핀 후속 처리
    전체(희귀표본상자 rareFirstScore·퍼펙트드로우·잭팟태그 배너/bias예약·승격심볼 소모·피버 충전/발동/
    효과/종료)를 이 신규 함수로 통합 이식, `SpinResolver.ResolveSpin`이 mode/보스/dev_safe/dev_bell
    보정이 전부 끝난 시점(배드스핀 판정 직전)에 1회 호출한다. `Pouch.cs`에 FEVER_MAX(100)/COMBO(15)/
    REACH(25)/JACKPOT(50)/SPINS(3)/EXP_MUL(1.30)/SCORE_MUL(1.50)/REACH_FIX(0.15)/
    JACKPOT_SCORE_MUL(2.00)/BELL_FEST_MUL(1.5) 상수 그대로 전사. `RunState`에 FeverGauge/FeverSpins +
    §9.0/§9.2 단회성 신호 10개(ReachBiasTag/SpinsLeft·JackpotPrismPending·FeverJackpotPrism·
    JackpotCrownPending·RetryReelPending/Used·ReachMarkUsed·JackpotCrownUsed·BellTicketUses/
    JpTicketUses) 신규. `Mods.feverReachFix` 신규 필드(리치표식이 참조) — **자체 검수로 발견한 정합성
    함정**: 웹은 `res.score`/지역변수 `exp`를 스핀 결산(`r.stageExp += exp`) 이후 전혀 재대입하지
    않고(피버/승격 보너스는 전부 `r.stageExp +=`/`r.score +=`로 런 누적치에만 가산), 피버 배율 3곳
    (feverExpExtra/feverScoreExtra/fjScoreBonus)은 그 "고정 원본값"을 기준으로 계산한다. 이 C# 포트는
    `gained`/`res.score`를 누적 델타 그 자체로 설계했는데, 함수 진입 시점 스냅숏(`expSnapshot`/
    `scoreSnapshot`) 없이 라이브 값을 읽으면 피버잭팟이 앞선 보너스(희귀표본·승격심볼 등) 위에 복리로
    얹혀 웹보다 과다 지급되는 버그가 됐다 — 1차 구현에서 직접 작성한 단위테스트(`FeverJackpotDoublesScore`,
    1000점 시나리오 기대값 2250 vs 실측 3000)로 스스로 잡아 정정했다(함수 진입 시 스냅숏 캡처 후 피버
    배율 계산 3곳만 스냅숏 참조로 전환, 최종 정답 2500). 유사 정합성 버그의 재발 방지를 위해 함수
    헤더에 상세 근거를 남겼다.
  - **④ 자동 소멸 — `StageFlow.ClearStage`**: `clearedStage==14` 클리어 시 예고 1회(`RunState.
    DecayForewarned`), `clearedStage>=15`부터 클리어마다 `Pouch.IsAutoDecayTarget`(cat=base &&
    !harmful) 심볼 1개 무작위 제거(DECK_MIN 미만도 허용, 대상 0개면 스킵) — 웹 그대로. `ClearOutcome.
    decayBanner` 신규(빈 문자열=소멸 없음, UI는 P7-4). `DeepStats.AutoDecays` 카운터 신규.
  - **⑤ POUCH 오퍼 v3 — 신규 `Run/PouchOffer.cs`**: `OfferSymbolRewards`(웹 `offerSymbolRewards`
    그대로 — 티어 시퀀스: 보스5배수=PRISM보장+GOLD·3배수=GOLD보장+SILVER·그 외=SILVER+goldBonus확률
    GOLD·태그잭팟/피버잭팟/잭팟왕관 신호=forcePrismFirst로 PRISM 강제. 특수 카드 저주 혼입 5%(노출
    옵션으로 0 가능)·전설 가중(legendWeight)·S1~2 EARLY_EXP_BOOST_IDS 3:2 가중. `bounds` opt는 웹
    원문에서도 함수 본문 어디에서도 참조되지 않는 죽은 매개변수라 이식하지 않음, 근거를 grep으로
    확인). **2-step 커밋 — `RunPhase` 6종 신설**(EventPouch/EventPouchCost/EventPouchRemove/
    EventRestDeep/EventGambleDeep/EventSynAugBonus — 작업 지시는 POUCH 2-step 3종만 명시했으나, REST/
    GAMBLE 심화 2택·3스테이지 연계 보너스도 동일하게 "사용자 응답을 기다리는 오퍼" 패턴이라 P1
    DeviceNode 선례를 그대로 넓혀 적용했다 — 이탈 사항, 이번 보고에 명시). `PouchOfferCard`/
    `PouchPendingSpecial` 신규 타입. `RunController.DispatchPickOffer`가 `PickOffer(index)` 액션
    하나를 이 6개 phase + 기존 EventAugment/Relic/AugLevel로 라우팅(웹 `pickPerk(idx)`가 `_pickKind`로
    분기하는 것과 동일 설계 — Unity는 RunPhase가 그 역할). 실버=기본1개 제거·골드=기본2개(이득<3이면
    1개)·프리즘=기본2개 또는 저주+1 선택(EventPouchCost)·저주=무료(DECK_MAX만 검사) 비용 규칙 전부
    `Pouch.Validate`로 원자적 검증 후 커밋, 실패 시 롤백(주머니 불변) + "규칙 위반" 메시지로 RewardDone
    귀결(웹도 성공/실패 모두 `_enterRewardDone`으로 귀결 — 별도 재시도 루프 없음). 획득 성공 시
    `RunState.DeepPity` 예약(기존 P7-1/P7-2 정비소와 동일 관례).
  - **⑥ JACKPOT 노드 — `PouchOffer.EnterJackpotNode`**: 현재 덱 최다 잭팟태그 기준 특수심볼 3택(미보유
    우선, 부족하면 전체) + 스킵. **정확도 함정 주의 이식**: 후보 필터는 `Pouch.CatOf(id)=="special"`이
    아니라 웹 그대로 `sym.special != Sp.NONE`(진리값) 기준이다 — 이 차이로 "coin"(Sp.COIN, cat="base")은
    coin 태그 후보에 **포함**되지만 "crown"(special=Sp.NONE, cat="special")은 crown 태그 후보에서
    **제외**된다(둘 다 실제로 웹과 대조해 확인, 테스트로 고정). 후보 0개(태그 없음/풀 소진)면 코인+5
    폴백.
  - **⑦ SYMAUG/SYMREL 노드 — `PouchOffer.EnterSymAugOrRel`**: 심볼증강21/심볼유물15 + 관련 일반
    증강·유물(웹 `deepCompatPool` — dSym 참조 계열 보유수가 `Pouch.RelMin`(3, 왕관은 `RelMinBySym`=2)
    이상일 때만 관련 있다고 판정) 혼합 오퍼. 웹은 이 둘도 일반 AUGMENT/RELIC과 동일한 `_pickKind`
    ("AUG"/"REL")로 라우팅하므로, Unity도 **오퍼 생성만 별도 함수**로 두고 실제 그랜트(픽 확정)는
    기존 `NodeEvents.PickOffer`를 100% 재사용한다(RunPhase를 EventAugment/EventRelic으로 공유 —
    SymPerkDef를 `Perk`로 합성 래핑해 `Shop.PickPerksByTier`/`Shop.SetSynergyAug`/`PerkFamily.FamOf`
    (미등록 id는 "고유 패밀리 랭크1" 폴백이라 안전)를 그대로 통과시킨다). 프리즘잉크 소비 조건이
    SYMAUG는 "성공(PRISM 확정)했을 때만"(폴백 시 보존)로 일반 AUGMENT의 "무조건 리셋"과 실제로
    다르다는 것을 웹 원문 대조로 확인해 그 차이까지 그대로 이식(성급한 통일 금지 — 정확도 함정 ②).
  - **⑧ 3스테이지 증강 연계 보너스 — `NodeEvents.PickOffer`에 추가**: 심화 런 + AUG 픽(일반 AUGMENT·
    SYMAUG 공용) + `(stage-1)%3==0 && stage-1>0`일 때, 획득 증강의 `DeepPerkMeta.DSymOf` 1차 태그와
    일치하는 미보유·해금 특수심볼 후보가 있으면 무료 2택(획득/건너뛰기, `RunPhase.EventSynAugBonus`)
    으로 이어간다(그랜트 자체는 이미 완료 — 오퍼만 지연). 신규 `Content/DeepPerkMeta.cs`(AUGMENTS
    89종·RELICS 73종의 `deep`/`dSym`/`dDesc` 메타를 웹 data.js 헤더 규약대로 python 정규식 파싱해
    id 기준 별도 테이블로 이식 — `Perk` 클래스 자체엔 필드 추가하지 않음, `Content/Pouch.cs`
    POUCH_CAT/JACKPOT_TAG 선례와 동일한 "별도 맵" 관례).
  - **⑨ 심화 노드 풀 — `StageFlow.RollDeepNodes`**: POUCH 고정 + second(SYMAUG 40%/SYMREL 20%(보스
    35%)/dpool 셔플 1장) + third(dpool 비중복 1장) — dpool = SHOP/REST/GAMBLE/EVENT 상시 + stage≥6
    CURSE/RISK + stage≥3 JACKPOT + 심볼퍽 `shopLabWeight`(연구실중독/연구실열쇠)만큼 SHOP 추가(최대
    4장). SYMAUG 슬롯 + 레벨업 가능 보유증강 있으면 확률로 AUGLEVEL 교체(일반 런과 동일 pity 공식,
    `AUGMENT` 대신 `SYMAUG` 슬롯 대상). `sp_deckslot`(alwaysRepair) SHOP 노드 보장. `dev_call_bell`
    (연구실호출벨) 체크는 그 장치 자체가 Devices.cs에 없어(§2-(C-2) "심화 9종 대응 장치 없음"과 동일
    사유) 현재 항상 false인 죽은 분기지만 웹 정확 전사 원칙에 따라 자리만 남겼다(장치 이식 시 자동
    활성화). `NodeKind` 4종(Pouch/Jackpot/SymAug/SymRel) 신규 — 일반 런의 `RollNextNodes`는 절대
    이 값들을 생성하지 않는다(자동플레이 스모크로 확인).
  - **⑩ REST/GAMBLE 심화 2택 — `PouchOffer.EnterRest`/`EnterGamble`**: 심화 런 + 해골 보유 시 REST가
    2택(코인+12 vs 해골 정화)으로, 심화 런이면 GAMBLE이 2택(코인 도박 vs 심볼 도박)으로 갈라진다.
    **정확도 함정 ③**: 심화 `gamble_coin`은 일반 `NodeEvents.ResolveGamble`과 확률(50%)만 같을 뿐
    실패 처리가 다르다 — 일반은 실패 시 코인을 0으로 날리지만, 심화 `gamble_coin`은 "코인 유지"(웹
    원문 그대로, 손실 없음)다. 일반 런 분기는 기존 `NodeEvents.ResolveGamble`을 `internal`로 승격해
    그대로 재사용(중복 정의 방지, node/coinsDelta 필드까지 보존).
  - **⑪ 퍼펙트 드로우 — `DeepRunHooks.ProcessDeepSpinFollowups`에 포함**: 5칸 전부 동일 계열(base
    환산, `Pouch.UpgradeParent`) + 전부 실심볼(빈칸 불성립)이면 스테이지 1회 코인+1(`RunState.
    PerfectDrawStage`).
  - **웹 대비 생략/이월 — 명시적으로 범위 밖(보고 대상 아님, 계획된 다음 슬라이스)**: ①
    profile.symUnlocked(ACH_SYMBOL_UNLOCK 13종, P7-4) — 이 슬라이스는 `Pouch.DefaultUnlocked`(58종
    기본 해금)로 근사(POUCH 오퍼·3스테이지 연계 보너스 후보 필터 둘 다 동일 근거) — 프로필 시스템이
    붙으면 이 근사만 실제 값으로 교체하면 된다. ②symPerkMods 중 여전히 미소비인 compressScorePct/
    balanceScore(스핀당 점수 보너스, sr_compress_contract/sr_scale) — P7-2가 미소비로 남긴 항목을
    이번 슬라이스도 착수하지 않았다(작업 지시 §범위 밖, 웹 game.js:944-959). ③RANDPACK_*/
    DESIGNATED_UPGRADE_CHANCE/PACKAGE_CHANCE(POUCH 오퍼 v2, 웹 자체가 "dormant, 삭제 금지"로 은퇴시킨
    죽은 코드라 이식 대상 아님). ④hex_allornothing 저주의 dEff(전공 배율 절반 재설계, dEff 메커니즘
    자체가 Unity에 없음 — §2-(BB) B 각주와 동일). ⑤Sp 신규 51종 중 잭팟태그 계열 10종(BELL_*/CHEER/
    BIG_BOOM/SLOT_SHARD/JACKPOT_WAND/JACKPOT_CROWN/REACH_MARK/RETRY_REEL/JACKPOT_TICKET)을 제외한
    나머지(CATALYST/MIRROR/TARGET/PUZZLE5/ALARM/SAFEPIN 등)의 실제 효과 — P7-1부터 이어지는 이월
    항목, 안전핀노트(safepin)의 AugLevelChance 부스트 포함 미착수. ⑥WANDWILD(마법봉) 동일심볼 잭팟의
    와일드 제외 로직("wandWilds") — Unity에 마법봉 특수효과 자체가 없어 대상 없음(정합 위험 없음 —
    기존 상태 그대로). ⑦심화 업적 13종/장치 9종·UI 보드(오퍼 카드·피버 HUD·자동소멸 배너 등)·랭킹
    3노드 분리 — 전부 P7-4.
  - **테스트 — 신규 `Tests_P7_3_JackpotFeverOffer.cs`**: 잭팟태그 3단계(콤보/리치/태그잭팟) 손계산
    + 동일심볼 잭팟 공존 중복금지 + 일반모드 완전격리(총 5개) · 증폭 심볼 4종 각각(환호/대폭죽/
    슬롯조각/잭팟마법봉) · 리치bias 적용·소진 · 피버 5종(충전/발동/효과/종료/피버잭팟, 스냅숏 버그
    정정 반영) · 승격 심볼 5종(종소리티켓·잭팟티켓 런2회 제한·리치표식 확률(feverReachFix 극단값으로
    RNG 무관 결정론화)·재도전릴/잭팟왕관 스테이지1회) · 퍼펙트 드로우(1회성·전실심볼 요구) · 희귀표본
    상자 · 자동소멸 3종(예고/제거/대상없음스킵) · POUCH 오퍼 티어시퀀스 4종(보스5배수/3배수/
    forcePrismFirst/비용구조) · 2-step 커밋 6종(RunController 경유 — 실버/저주/스킵/프리즘 제거경로/
    프리즘 저주경로/롤백) · 심화 노드풀 4종(POUCH 고정·JACKPOT stage≥3·CURSE·RISK stage≥6·일반풀
    미사용) · 3스테이지 연계 보너스 · 심화 자동플레이 스모크(10시드×최대20000틱, 전 신기능 이벤트
    화이트리스트 + NaN/음수/런제한 불변식). 기존 P7-1/P7-2 자동플레이 스모크 2건도 신규 이벤트 타입
    (POUCH_OFFER 등)·신규 phase(EventPouch 등)를 인식하도록 화이트리스트/스위치를 확장했다(그 파일들
    자체 로직은 무변경).
  - **검증(1차 제출 시점)**: `dotnet run --project Client/Jackpot/Tools/EngineTests` 23874 →
    **27770**(+3896), 0 실패. 오프라인 스모크 컴파일(Unity 에디터 미실행 확인 후 `dotnet exec
    csc.dll -noconfig`, Library/Bee의 P7-2 시점 rsp에 신규 2파일만 추가) — `Assembly-CSharp`
    (런타임, 90개 — 신규 `Content/DeepPerkMeta.cs`·`Run/PouchOffer.cs` 포함)·`Assembly-CSharp-Editor`
    (6개, Editor rsp의 `-r:".../Assembly-CSharp.ref.dll"`을 신규 빌드한 런타임 스크래치 ref dll로
    정정, §2-(AA) 후기 절차 그대로 재확인) 둘 다 0에러·0경고.
  - **Opus 2차검수 반영(2026-08-09, MED 2건 + 정합 1건 + 소소 3건)**:
    ①**[MED] compatFilter 누락**: `PouchOffer.EnterSymAugOrRel`의 5% 세트시너지 주입이 웹
    engine.js:1265-1267·game.js:1773/1786 `opts.compatFilter`(심볼퍽이거나 `isDeepCompat` 통과) 없이
    무조건 주입되고 있었다 — `SymPerks.Get(syn.id)!=null || DeepPerkMeta.IsDeepCompat(syn, run.Pouch)`
    검사를 추가해 미통과 시 주입을 취소(원래 마지막 칸 유지)하도록 정정.
    ②**[MED] REST 노드 이벤트 회귀**: `PouchOffer.cs`로 배선이 옮겨가며 `NodeEvents.ChooseNode`의
    기존 `node = node` 관례가 REST뿐 아니라 GAMBLE 심화 분기·POUCH/JACKPOT/SYMAUG/SYMREL(신규 4종
    NodeKind) 진입 이벤트에도 전부 누락돼 있었다 — `UI2/Run/RunView.cs:775`의 P4 완료화면 문구
    템플릿(`switch (e.node) { case NodeKind.Rest: ... }`)과 `Profile/StatTracker.cs:264`의
    "gambles" 통계 카운터(`if (e.node==NodeKind.Gamble) p.Inc("gambles")`)가 이 필드에 의존한다는
    걸 확인하고 `EnterRest`/`PickRestDeep`/`EnterGamble`(심화 분기)/`PickGambleDeep`/
    `EnterPouchOffer`/`EnterJackpotNode`/`EnterSymAugOrRel`(×2) 전부에 정확한 `node` 값을 채웠다
    (`Resolved`/`CommitAndFinish` 헬퍼에 선택적 `node` 매개변수 추가). `Tests_S4.cs`의 `RestNode`에
    `ev[0].node==NodeKind.Rest` 어서션을 추가해 재발 방지 고정.
    ③**[MED] 노드 진입 4종 전용 테스트 신설**: `EnterSymAugOrRelOfferAndPickRoundTrip`(오퍼→픽→직후
    스핀 1회까지 실행해 fx=null 합성 Perk가 `ModsBuilder.Build`를 안전하게 통과하는지 실증 — AUGMENT/
    RELIC 양쪽), `EnterJackpotNodeCandidatePoolCoinIncludedCrownExcluded`(coin은 후보 포함·crown은
    후보 원천배제, 각 100시드), `PickRestDeepChoosesCoinOrPurify`/`PickGambleDeepChoosesCoinOrSym`
    (각 2분기 커밋+node 검증) 4종 신설. 심화 자동플레이 스모크의 `ChooseNode`도 항상 index 0 대신
    시드 기반 별도 RNG로 매 스텝 유효 인덱스를 균등 무작위 선택하도록 변경(노드가 3개일 때 종전엔
    2/3 경로가 스모크에서 전혀 실사되지 않았다).
    ④**[웹 정합] 심화 노드 풀 stage 게이트 기준 오류**: `StageFlow.RollDeepNodes`가 JACKPOT(stage≥3)·
    CURSE/RISK(stage≥6) 게이트에 `nextStage`(clearedStage+1)를 썼던 것을 `clearedStage`(방금 클리어한
    스테이지, `r.stage=stage+1` 재대입 *이전* 값 — `NodeEvents.PickDevice`가 동일 시점의 `run.Stage`를
    쓰는 것과 동일 근거)로 정정 — 등장 시점이 웹보다 1스테이지 앞당겨져 있었다. 경계값(clearedStage=2
    vs 3, 5 vs 6)을 직접 probing하는 테스트로 교체해 재발 방지. **잔여 이탈(범위 밖, 손대지 않음)**:
    일반 런 `RollNextNodes`는 이미 `nextStage` 관례를 쓰고 있다(이 슬라이스 이전부터 존재하던 별개
    관례) — 이번 정정 대상이 아니다, 필요 시 별도 슬라이스에서 재검토.
    ⑤**[소소]**: `EnterSymAugOrRel`의 picks 빈 배열 조기반환을 웹 engine.js:1258
    `if (!picks.length) return {...}` 위치(세트시너지 5% 롤·lucky 리셋·EnsureSymPerkCard *이전*)로
    이동(`NodeEvents.OfferPerks`의 기존 `if (picks.Count==0) return picks;` 관례와 동일 위치 — 이전엔
    `&&` 좌변 우선평가로 picks가 비어도 `rng.Next(100)`이 소비돼 웹에 없는 RNG 소비가 있었다). 프리즘잉크
    소비 판정도 빈 배열일 때 "실제 결과 티어"가 없으므로 요청 티어(nodeTier) 기준으로 갈라지도록 정정.
    `Mods.cs:83` 주석의 오기 함수명(`ApplyDeepFeverAndTagFollowups`, 실제로 만든 적 없는 이름)을
    실제 함수명 `DeepRunHooks.ProcessDeepSpinFollowups`로 정정 + `run.LastMods`가 아니라 호출 시점의
    로컬 `mods` 인스턴스를 직접 읽고 쓴다는 정확한 설명으로 교체. `Pouch.EarlyExpBoostIds`는 그대로
    유지하되(웹도 이 6종 가중은 항상 켜져 있고 실제 특수효과 이식 여부와 독립 축) 그중 4종(alarm/knot/
    energypack/bandage)의 실제 특수효과는 아직 P7-3b(Sp 신규 51종 중 잭팟태그 외 나머지 전면 이식)
    대상이라는 점을 주석으로 명시.
  - **재검증(Opus 반영 후 최종)**: `dotnet run --project Client/Jackpot/Tools/EngineTests` 27770 →
    **29422**(+1652), 0 실패. 오프라인 스모크 컴파일 재확인(`Assembly-CSharp` 90개·
    `Assembly-CSharp-Editor` 6개 — 파일 목록 변경 없음, 기존 rsp 그대로 재컴파일) 둘 다 0에러·0경고.
  - **P7-3b 예고(다음 슬라이스, 이번엔 착수하지 않음)**: 미이식 `Sp` 51종 중 잭팟태그 계열 10종을
    제외한 나머지(CATALYST/MIRROR/TARGET/PUZZLE5/ALARM/SAFEPIN/KNOT/ENERGYPACK/BANDAGE/HOURGLASS/
    DEVCD/GEAR/KIT 등)의 실제 특수효과를 전면 이식 확정 — `Pouch.EarlyExpBoostIds`가 참조하는 4종
    (alarm/knot/energypack/bandage)도 이때 함께 살아난다.

## 3. 페이즈 로드맵

| 페이즈 | 내용 | 상태 |
|---|---|---|
| **P1** | 룰 파리티 1차: 첫판 즉시시작 · 특수스핀 첫사용무료 · 실패체인 웹 순서 · 노드 보상 수치/DEVICE 노드 · 포기 | ✅ 2026-08-07 완료 |
| **P2** | 점수·캡 공식 웹화 + 보스 grad/finals 정리 + 골든 테스트 재산출 | ✅ 2026-08-07 완료 |
| P3 | 메타 웹화: XP/레벨/레벨보상 · 업적 34종 교체 · 숙련도 · 증강 레벨업 · 해금 OR · 콘텐츠 증보(+3캐릭/+3머신/+장치/+증강9/+유물12/+아이템5) | ✅ 2026-08-08 완료 |
| P4 | 화면 흐름 웹화: 홈 · REWARD_DONE 능력치 · 셀 정보 탭 · 클리어 등급 연출 · 튜토리얼 · 설정 | ✅ 2026-08-09 완료(3/3) |
| P5 | 사운드(절차 합성 SFX 16종 + BGM 루프) | ✅ 2026-08-09 완료 |
| P6 | 승천 A1~A10 + 승천 랭킹 분리 | ✅ 2026-08-09 완료(랭킹 분리는 P7-4로 이관, bestAscScore 기록은 완료) |
| P7 | 심화모드 전체(주머니·심볼72·심볼퍽·정비소·전공·잭팟태그·피버) + 심화 랭킹 | 🔶 진행 중(3/4 — 코어+심볼퍽/전공/정비소+잭팟태그/피버/자동소멸/POUCH오퍼/노드풀 완료, §2-(AA)/§2-(BB)/§2-(CC)) |

각 페이즈는 FABLE_RULES 4단계 파이프라인으로 진행하고, EngineTests 골든망을 웹 수치로 갱신하며 통과를 유지한다.
