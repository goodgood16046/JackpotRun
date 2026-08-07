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

## 3. 페이즈 로드맵

| 페이즈 | 내용 | 상태 |
|---|---|---|
| **P1** | 룰 파리티 1차: 첫판 즉시시작 · 특수스핀 첫사용무료 · 실패체인 웹 순서 · 노드 보상 수치/DEVICE 노드 · 포기 | ✅ 2026-08-07 완료 |
| **P2** | 점수·캡 공식 웹화 + 보스 grad/finals 정리 + 골든 테스트 재산출 | ✅ 2026-08-07 완료 |
| P3 | 메타 웹화: XP/레벨/레벨보상 · 업적 34종 교체 · 숙련도 · 증강 레벨업 · 해금 OR · 콘텐츠 증보(+3캐릭/+3머신/+장치/+증강9/+유물12/+아이템5) | 진행 중(3/4: 업적 34종 완료 · 숙련도+증강 레벨업 완료) |
| P4 | 화면 흐름 웹화: 홈 · REWARD_DONE 능력치 · 셀 정보 탭 · 클리어 등급 연출 · 튜토리얼 · 설정 | 대기 |
| P5 | 사운드(절차 합성 SFX 17 + BGM) | 대기 |
| P6 | 승천 A1~A10 + 승천 랭킹 분리 | 대기 |
| P7 | 심화모드 전체(주머니·심볼72·심볼퍽·정비소·전공·잭팟태그·피버) + 심화 랭킹 | 대기 |

각 페이즈는 FABLE_RULES 4단계 파이프라인으로 진행하고, EngineTests 골든망을 웹 수치로 갱신하며 통과를 유지한다.
