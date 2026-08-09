# 작업 로그

모든 작업 완료 시 이 파일에 기록한다. 서식: `## 날짜 - 작업내용` (최신 항목이 위로 오도록 추가)

---

## 2026-08-09 - 웹 파리티 P7-3b(심화모드 4/4, P7 완료) — Sp 신규 39종 특수심볼 효과 전면 이식

상세는 `Docs/WEB_PARITY_DESIGN.md` §2-(DD) 참조. 요약:

- **범위**: P7-3(§2-(CC))가 잭팟태그 계열 13종만 처리하고 남긴 나머지 Sp 39종의 실제 효과를 전부
  이식 — evaluate 즉시효과(정화/거울/촉매/마법봉/빈칸활용/표적/퍼즐/피방울/검은초/불안정폭탄/instant
  5종(붕대·매듭·에너지팩·가짜왕관·진화핵)/럭키7/프리즘) + 스핀 후속 소비(알람/톱니/모래시계/영수증/
  쿠폰/장바구니/방패/시험지/배터리/정비키트/형광펜/복습책/세트조각/저주게이지/저주눈/검은카드/수정구/
  안전핀/임시와일드/운명의소용돌이) + 굴림 시(임시와일드 무조건 주입)·상주(족쇄).
- **`SpinResolver.Evaluate`**: 웹 engine.js 순서 그대로 신규 블록 삽입(`specialMul` 로컬 변수 신설,
  PURIFY→MIRROR→CATALYST가 BOMB 이전, WANDWILD가 와일드 집계 직후로 잭팟 게이트에서 제외, 빈칸활용→
  TARGET이 SET 이전, PUZZLE5가 SET 이후, CURSE_BLOOD/CURSE_CANDLE이 해골페널티 이후, Phase4 배수형
  블록(CURSE_BOOM→instant5종→LUCKY7→PRISM_SYM→specialMul캡)이 phoenix 이후·전역배수 이전). `Mods.cs`
  4필드(`deepEmptyScore/deepEmptyExp/legendStable/shackleActive`)·`SpinResult` 27필드 신설.
- **`DeepRunHooks.ProcessDeepSpinFollowups` 확장**: 웹 `_applyDeepSpinMeta` 전체 이식(모래시계
  이월소비→growNext저장→알람/톱니 배수누적→상점3플래그→보스2플래그→배터리/정비키트(신규
  `ReleaseDeviceUse`, HashSet이라 "최후1건" 대신 "MANIP마커 1건" 근사)→형광펜/복습책→세트조각(근사,
  Unity SET이 다중집합 미지원인 기존 기술부채 발견)→저주게이지/저주눈→검은카드/수정구/안전핀/
  임시와일드(자연등장 시만 조건부 소모)/운명의소용돌이).
- **굴림/스핀 파이프라인**: `ResolveSpin`에 임시와일드 무조건 cellOp 주입(기존 "wild_temp" 아이템
  재사용)·운명의소용돌이 2회굴림 비교(웹의 `!r.lockedNext` 죽은 가드를 "항상 참"으로 정확히 재현하기
  위해 조건 자체를 생략)·방패/시험지 보스 처리 재구성. `EffSpins`에 족쇄(`shackleActive`) 보스스핀-1
  내장(Unity는 "spins" 캐시 필드가 없어 순수함수 재계산으로 동치 구현).
- **상점 훅**: `Shop.PriceMul`에 영수증(-10%), `ShopSlotBonus`에 장바구니 가산 — P3.5가 "P7 미구현"
  으로 미뤄뒀던 두 항목 실배선. 쿠폰(`ShopEntry.couponTag` 신규)·검은카드 1회 무료 구매(`Shop.Buy`)·
  `NodeEvents.ChooseNode` Shop 진입 훅(수정구 예약치 이관·검은카드 소비) 추가.
- **`StageFlow.RollDeepNodes`**: 안전핀노트 — AUGLEVEL pity 실패 분기에서 +1%p 추가 누적+소비(웹
  game.js:1478-1488).
- **이탈/신규 발견(보고 대상)**: ①`mods.deepFamilyBridge`(웹 famBridge, 상위계열 셀 값강화 브릿지)
  는 Sp 특수값이 아닌 별도 mods 플래그라 미착수(전수 grep으로 부재 확인, 향후 슬라이스 필요).
  ②Unity SET 블록이 "최다 세트 1개"만 지급(웹은 count≥2인 모든 값심볼 각각 지급) — 세트조각(SETFRAG)
  근사 구현 중 발견한 기존 기술부채, 이번 슬라이스 범위 밖이라 미수정.
- **테스트**: 신규 `Tests_P7_3b_SpEffects.cs` — evaluate 즉시효과 18종·DeepRunHooks 소비 17종
  (조건부 미소모 케이스 포함)·SpinResolver 파이프라인 5종·Shop 5종·StageFlow 안전핀 pity(400시드
  탐색)·심화 자동플레이 스모크(신규효과 관측 카운트 리포트). `dotnet run --project Client/Jackpot/
  Tools/EngineTests` 29421(직전 슬라이스 종료 시점 대비 스모크 카운트 자연변동, 0실패 무회귀 확인)
  → 34080(+4659), 0 실패. `dotnet build` 0에러·0경고.
- **Opus 2차검수 반영(HIGH 1건·MED 5건·LOW 일괄)**: 상세는 §2-(DD) 참조.
  ①**[HIGH]** SETFRAG 게이트를 `bestSetId!=null`→`bestSetCount>=2`로 정정(값심볼 1개만 나와도
  bestId가 채워지는 버그 발견).
  ②**[MED]** `ConsumeInstantSymbols` 시그니처를 raw 셀→`SpinResult`로 바꿔 evaluate가 계산한
  hasX 플래그(폭탄에 날아간 instant는 정확히 false)를 직접 소비, 호출 시점도 fate_vortex 채택 이후로
  이동.
  ③**[MED]** fate_vortex가 두 번째 굴림을 채택하면 raw/rawIds/run.LastCells도 함께 교체(릴 표시
  정합).
  ④**[MED]** `EffSpins`가 `mods.shackleActive` 대신 `run.Pouch["shackle"]`을 직접 참조하도록
  정정(preEffSpins·프리뷰·DeviceActions·ItemUse 등 ApplyDeepMods를 거치지 않은 mods 경로 4곳 일괄
  해결).
  ⑤**[MED — Fable 결정]** 🩸/🧿/💳의 UnluckyGauge 가산을 제거 — Unity 게이지는 만땅 시 forceRare
  실보상이 걸려 있어 저주 심볼이 이득이 되는 부호 역전이 있었다(웹은 게이지가 장식이라 무해).
  ⑥**[MED — Fable 결정]** 검은초/불안정폭탄의 specialMul 캡·exp=0 리셋을 jackpotFixed에도 적용
  (웹 순서, 심화 전용 경로라 일반 골든 무접촉) — 전역 expMul 배제(P2 결정)는 유지.
  ⑦**[LOW 일괄]** fate_vortex 소비를 스테이지 스코프→런 스코프로 통일(웹 quirk), 모래시계 이월을
  `run.StageExp` 직접 가산으로 변경(RunBestSpin/LastGain 오염 방지), 주머니 감소 5개 지점에
  `CheckArchetypeChange` 호출 추가, Lucky7 테스트에 EXP·점수 ×7 어서션 보강, 심화 스모크에 신규
  심볼을 시작 주머니에 시딩해 관측 카운트 확보.
  재검증: 34080 → **33734**(스모크 런이 더 일찍 끝나 스텝당 반복 어서션이 줄어든 게 주 요인, 개별
  결정론 테스트는 순증), 0 실패.
- **P7 완료**: WEB_PARITY_DESIGN.md §1-A #19 "심화모드(심볼 덱/주머니)" 4/4 슬라이스 전부 완료. 남은
  항목(UI 보드·심화 업적13종/장치9종·랭킹 3노드 분리)은 전부 P7-4로 이관.

## 2026-08-09 - 웹 파리티 P7-3(심화모드 3/4) — 잭팟태그6종·피버게이지·자동소멸·POUCH오퍼v3(2-step)·심화노드풀·퍼펙트드로우·3스테이지연계보너스

상세는 `Docs/WEB_PARITY_DESIGN.md` §2-(CC) 참조. 요약:

- **잭팟 태그 시스템**: `SpinResolver.Evaluate`에 §9.0 J1 블록 신설(`Mods.deepMode`로 게이팅) —
  최다 잭팟태그(crown/seven/coin/prism/curse/bell) 3단계(콤보3=EXP+8/리치4=점수+300/태그잭팟5=
  EXP+30·점수+1500), 동일심볼 잭팟 공존 시 중복 지급 금지. 증폭 심볼 4종(환호×1.25·대폭죽+500/+2000·
  슬롯조각/잭팟마법봉 최다태그+1·잭팟왕관 신호)·종세트 추가충전(작은종+15·황금종+30·울림종+200점수)
  전부 이식. `SpinResult`에 신규 필드 10개.
- **리치 bias + 재도전릴**: `SpinResolver.RollCells`가 `DeepRunHooks.ApplyReachBias`(리치 태그
  ×1.5, 1스핀)·재도전릴(리치 다음 스핀 1칸 재굴림)까지 처리 — P7-1/P7-2가 명시적으로 미뤄둔 항목
  해소(MANIP 등 1칸 굴림 호출부는 범위 제외, 기존 RNG 위상 divergence 선례와 일관).
- **피버 게이지**: 신규 `DeepRunHooks.ProcessDeepSpinFollowups`(웹 game.js:960-1138 스핀 후속 처리
  전체 통합 — 희귀표본상자·퍼펙트드로우·잭팟태그 배너/bias예약·승격심볼 소모·피버 충전/발동/효과/종료).
  **자체 검수로 발견·정정**: 피버 배율 계산(feverExpExtra/feverScoreExtra/fjScoreBonus)이 "스핀
  결산 시점 고정 원본값"이 아니라 앞선 보너스로 이미 불어난 라이브 값을 읽어 복리 과다지급되는 버그를
  직접 작성한 단위테스트로 스스로 잡아 함수 진입 시점 스냅숏 방식으로 정정(1000점 시나리오
  기대 2250→오류 실측 3000→정정 2500).
- **자동 소멸**: `StageFlow.ClearStage`에 §3 V3P3 블록 추가 — stage14 클리어 예고 1회, stage15+
  클리어마다 기본 이득 심볼(cat=base && !harmful) 1개 무작위 제거. `ClearOutcome.decayBanner`/
  `DeepStats.AutoDecays` 신규.
- **POUCH 오퍼 v3 + 2-step 커밋**: 신규 `Run/PouchOffer.cs`(`OfferSymbolRewards` — 보스5배수=
  PRISM보장·3배수=GOLD보장·태그잭팟/피버잭팟/잭팟왕관=forcePrismFirst, 저주혼입5%·전설가중·초반
  가중). `RunPhase` 6종 신설(EventPouch/Cost/Remove/EventRestDeep/EventGambleDeep/EventSynAugBonus
  — 작업 지시는 POUCH 3종만 명시했으나 REST/GAMBLE 심화 2택·3스테이지 연계 보너스도 동일 패턴이라
  확장, 이탈 사항). `RunController.DispatchPickOffer`가 `PickOffer(index)` 하나를 이 6개+기존 3개
  phase로 라우팅(웹 `pickPerk`의 `_pickKind` 분기와 동일 설계). 실버1개·골드2개(완화1개)·프리즘
  2개(또는 저주+1 택1)·저주 무료 비용 규칙 + `Pouch.Validate` 원자적 검증/롤백.
- **JACKPOT/SYMAUG/SYMREL 노드**: `PouchOffer.EnterJackpotNode`(최다 태그 특수심볼 3택+스킵,
  `sym.special != NONE` 기준이라 coin은 포함·crown은 제외되는 정확도 함정 확인 후 웹 그대로 이식).
  `PouchOffer.EnterSymAugOrRel`(심볼퍽+deepCompatPool 혼합 오퍼 — 신규 `Content/DeepPerkMeta.cs`,
  AUGMENTS 89·RELICS 73의 deep/dSym/dDesc 메타를 별도 테이블로 전사) — 실제 그랜트는 기존
  `NodeEvents.PickOffer` 재사용(Phase 공유).
- **심화 노드 풀**: `StageFlow.RollDeepNodes` 신규 — POUCH 고정+second(SYMAUG40%/SYMREL20~35%/
  dpool)+third(dpool). dpool=SHOP/REST/GAMBLE/EVENT 상시+stage≥6 CURSE/RISK+stage≥3 JACKPOT.
  `NodeKind` 4종(Pouch/Jackpot/SymAug/SymRel) 신규.
- **REST/GAMBLE 심화 2택**: `PouchOffer.EnterRest`/`EnterGamble` — 심화 gamble_coin은 실패해도
  코인을 잃지 않는다는 웹의 정확도 함정 확인 후 그대로 이식(일반 GAMBLE과 실패 처리가 다름).
- **퍼펙트 드로우**: 5칸 전부 동일 계열+전실심볼이면 스테이지 1회 코인+1.
- **3스테이지 연계 보너스**: `NodeEvents.PickOffer`에 추가 — (stage-1)%3==0 AUG 픽 직후 태그 일치
  특수심볼 무료 2택 오퍼.
- **이탈/생략**: profile.symUnlocked(P7-4) 대신 `Pouch.DefaultUnlocked`로 근사·compressScorePct/
  balanceScore 미소비(P7-2 이월분, 이번 슬라이스 범위 밖)·RANDPACK 계열(웹 자체 dormant)·
  hex_allornothing dEff·Sp 신규 51종 중 잭팟태그 외 나머지 실제효과(safepin 포함)·WANDWILD·심화
  업적/랭킹/UI(P7-4) — 상세 근거는 §2-(CC) 참조.
- **검증(1차)**: 신규 `Tests_P7_3_JackpotFeverOffer.cs`(잭팟태그/증폭심볼/리치bias/피버5종/승격심볼5종/
  퍼펙트드로우/희귀표본/자동소멸3종/오퍼티어시퀀스4종/2-step커밋6종/노드풀4종/3스테이지연계/자동플레이
  스모크) + 기존 P7-1/P7-2 자동플레이 스모크 2건 확장(신규 이벤트·phase 인식). EngineTests
  23874 → **27770**(+3896), 0 실패. 오프라인 스모크 컴파일(Unity 미실행 확인, `dotnet exec
  csc.dll -noconfig`) — Assembly-CSharp(런타임 90개)·Assembly-CSharp-Editor(6개) 둘 다 0에러·0경고.
- **Opus 2차검수 반영**(상세는 §2-(CC) 참조): ①`EnterSymAugOrRel` 세트시너지 5% 주입에 compatFilter
  (심볼퍽 or deepCompatPool 통과) 누락 정정 ②REST 노드 `node` 필드 회귀 복구 + POUCH/JACKPOT/SYMAUG/
  SYMREL/GAMBLE심화 전체에 `node` 배선(UI2 RunView 완료화면 문구·StatTracker "gambles" 카운터가 이
  필드에 의존) ③노드 진입 4종(EnterSymAugOrRel·EnterJackpotNode·PickRestDeep·PickGambleDeep) 전용
  테스트 신설 + 스모크 ChooseNode를 시드기반 랜덤 인덱스로 전환 ④`RollDeepNodes`의 stage 게이트를
  nextStage→clearedStage로 정정(웹 `_clearStage()`의 `stage`와 정합, JACKPOT/CURSE/RISK 등장이
  1스테이지 앞당겨져 있던 버그) — 일반 런 `RollNextNodes`의 기존 nextStage 관례는 별개 이탈로 남겨둠
  ⑤EnterSymAugOrRel 빈오퍼 조기반환 위치 정정(RNG 소비 순서)·Mods.cs 주석 오기 함수명 정정·
  EarlyExpBoostIds에 P7-3b 연동 주석 추가. **재검증**: EngineTests 27770 → **29422**(+1652),
  0 실패. 오프라인 스모크 컴파일 재확인 둘 다 0에러·0경고.

## 2026-08-09 - 웹 파리티 P7-2(심화모드 2/4) — 심볼퍽21+15·전공 아키타입·정비소11 + 선행 blocker 해소

상세는 `Docs/WEB_PARITY_DESIGN.md` §2-(BB) 참조. 요약:

- **§0 선행 blocker 해소**(§2-(AA)가 남긴 항목): `SpinResolver.RollCells`/`RollCellOne`(신규
  단일 굴림 진입점 — DeepMode면 mods를 PouchBias로 변환해 주머니 추출, 아니면 기존 가중추첨)로
  PEEK/MANIP(재굴림/고정)/도박꾼재굴림/재시험/timeline_ticket을 전부 통합 — 이전엔 심화 런에서도
  일반 가중추첨(72종 전체)을 그대로 썼다. `CellsFromIds`에 rng/pouch 선택 인자 추가 —
  "empty"/"random"/미지 id를 더 이상 드롭하지 않고 안전하게 복원(입력=출력 칸수 보장, PEEK→다음
  스핀 5칸 유지 확인).
- **심볼증강21+심볼유물15+레벨8종**: 신규 `Content/SymPerks.cs` — 데이터 전사 +
  `SymPerks.ComputeMods`가 웹 `symPerkMods` 21개 훅 카테고리를 그대로 집계(일반 buildMods와 분리된
  순수 함수). 보유 저장소는 신규 필드 대신 **기존 RunState.Perks/PerkLevels 재사용**(웹 실제 구조
  대조 후 결정 — 이탈 사항, 근거는 SymPerks.cs/RunState.cs 헤더). 배선: `DeepRunHooks.DeepPenalty`가
  심볼퍽 penaltyMul(초과분에만)+bossQuotaMul(전설봉인함 25%감쇄)까지 완성(웹 `_deepPenalty()` 전체
  공식). pouch bias는 기존 mods.symbolWeightMul/weightAdd/rareWeightMul→PouchBias 변환으로 배선
  (addBoost는 POUCH 오퍼 전용이라 값만 계산, 소비처는 P7-3).
- **전공 아키타입 6계열**: 신규 `Content/Archetypes.cs`(cherry/book/gem/skull/coin/flame, 임계
  0.25/0.40, 단일전공, exp/score+15·30%·coin+10·20%·강령학파 skullPenaltyMul0.5). `Mods.
  deepFamilyExpMul/ScoreMul/CoinMul` 신규 + `DeepRunHooks.ApplyDeepMods`가 최종 mods에 주입 +
  `SpinResolver.Evaluate`에 계열 곱 반영(EXP/점수/코인/해골가산 4곳, 웹 engine.js 정확 대조).
- **정비소 11 서비스**: 신규 `Content/RepairServices.cs`(카탈로그) + `Run/RepairShop.cs`
  (`Execute` — 검증 실패/변화없음/코인부족 시 미차감 거부, 심볼증강 할인 반영 가격, 교체/정화 부수
  코인효과, 전공 발동/승급 이벤트). `RunController.RepairBuy` 액션 신규 배선.
- **테스트**: 신규 `Tests_P7_2_SymPerks.cs`(심볼퍽 36종+레벨8종 골든·symPerkMods 손계산 5조합·
  아키타입 임계/tie-break/보너스+Evaluate 반영 6케이스·DeepPenalty 전체식·정비소 11종 실행(성공/
  거부/부수효과/가격할인/전공이벤트)·blocker 6종(왕복+PEEK/MANIP/재굴림/재시험 pouch전용 등장)·
  RepairBuy를 섞은 심화 자동플레이 스모크). 어서션 22758 → 23858(+1100), 0 실패(1차 제출 시점).
- **스모크 컴파일**: Unity 에디터 미실행 확인 후 `dotnet exec csc.dll` 오프라인 검증(런타임 88개 —
  신규 4파일 포함·Editor 6개, Editor rsp의 Assembly-CSharp.ref.dll 참조를 새 스크래치 ref로 정정)
  둘 다 0에러·0경고.
- **Opus 2차검수 반영("됐다고 문서화됐지만 실제 미배선" 3건 실배선 + 권장 2건)**: ①심볼퍽
  skullPenaltyMul(sa_expand_build/sr_big_bag)이 계산만 되고 mods에 실제로 곱해지지 않던 것을
  `DeepRunHooks.ApplyDeepMods`에서 실배선. ②`RepairShop.Execute`에 `RunPhase.EventShop` 페이즈
  게이트 추가(Shop.Buy와 동일 관례 — 이전엔 아무 phase에서나 정비 구매가 통과됐음). ③신규
  `Mods.deepTagMul` 필드 + `SpinResolver.Evaluate` 소비 지점 배선으로 sv_tagbuff(22코인)를
  실효화(정비소 '태그 강화' + 심볼퍽 tagBuff류 병합, ±50% 클램프). ④[권장] sv_add_high/
  sv_add_rare 실행 테스트 추가. ⑤[권장] `Mods.DeepModsApplied` 가드로 ApplyDeepMods 이중호출
  안전화 + `CheckArchetypeChange`의 빈 주머니 조기반환 제거(스테일 전공 상태 리셋 실버그 수정).
  재검증: 23858 → **23881**(+23, 누적 +1123), 0 실패. 스모크 컴파일 재확인 0에러·0경고. LOW
  잔여 4건(dev_pin RNG 소비 위상·EffWithLevel 화이트리스트·legendStable 미이식·rarity 게이팅
  UI 몫)은 §2-(BB)에 후속 항목으로만 기재.
- **범위 밖(P7-3/4로 이월, 계획대로)**: symPerkMods 잔여 필드(emptyScore/Exp·quotaMul·rewardBonus
  등 — skullPenaltyMul/deepTagMul은 Opus 반영으로 배선 완료, repairMul류는 정비소가 이미 소비)·
  잭팟태그 실제발동·피버·POUCH 오퍼 2-step·심볼해금13종·심화 업적13/장치9·UI 보드(심볼퍽/정비소/
  전공 표시)·랭킹 3노드 분리·hex_allornothing dEff 재설계·Sp 신규51종 실제 특수효과(P7-1부터
  이어지는 이월)·위 LOW 잔여 4건.

## 2026-08-09 - 웹 파리티 P7-1(심화모드 1/4) — 주머니 코어 + 심볼 카탈로그

상세는 `Docs/WEB_PARITY_DESIGN.md` §2-(AA) 참조. 요약:

- **심볼 카탈로그 72종**: `Symbols.cs` 14종 → 72종(웹 `data.js` SYMS 전사, 신규 58종은 전부
  weight=0/dormant=true — 원본 14종의 가중치 스캔·일반모드 확률에 무영향). `Sym`/`Sp` enum도
  각각 58/51개 확장. `Symbols.LegacyCount=14` 신규 + "weight>0 심볼 집합 불변" 불변식 테스트 추가.
- **주머니 코어**: 신규 `Content/Pouch.cs`(POUCH_CAT/RARITY/USE·TIER_BY_RARITY·POUCH_SYMBOLS(71)·
  DEFAULT_UNLOCKED_SYMS(58)·JACKPOT_TAG·START_POUCH(총30)·DECK_MIN/MAX·MIN_KINDS·TAG_MAX_RATIO·
  CROWN/WILD_MAX·RARITY_MAX·COMPRESSION 표·EARLY_QUOTA 전부 웹 손전사) + `Pouch.Validate`(덱 검증
  7규칙 — 총량20~40·종류≥7·왕관≤2·와일드≤4·특수 티어 상한·같은 태그≤60%·잭팟태그≤8, 에러 메시지
  포함). 신규 `Run/PouchOps.cs`(`PouchDraw` — 웹 `pouchDraw`/`pouchDrawOne` 전사, empty/random 특수
  처리, bias 구조는 P7-2/3용으로 미리 마련). 신규 `Run/DeepRunHooks.cs`(`DeepPenalty` — 압축패널티×
  EARLY_QUOTA 램프, `ApplyDeepPity` — 획득심볼 2스핀 보장, `ConsumeInstantSymbols` — instant 소모
  단순화 버전).
- **배선**: `SpinResolver.ResolveSpin`이 `run.DeepMode`면 가중추첨 대신 주머니 추출을 탄다.
  `SpinResolver.QuotaOf`에 `deepPenaltyMul` 5번째 인자 추가 + 실사용 15곳 전부 갱신. `RunController`/
  `GameSession` 생성자에 `deep` 매개변수 추가 — deep이면 asc를 항상 0으로 강제(승천/심화 상호배제,
  P6에서 미해결로 남겨뒀던 항목 완료). `PlayerProfile.BestDeepScore/BestDeepStage`로 점수 격리(웹
  `if(deep){...}else if(asc>0){...}else{...}` 순서 그대로). UI는 모드 진입 배선만(`AppRoot.
  SelectedDeep`→`StartRun`→`GameSession`) — 실제 토글은 `MenuView`가 P7-4까지 계속 잠가 둔다.
- **범위 밖(P7-2/3/4로 이월, 계획대로)**: 심볼퍽 21+15종·정비소 11종·전공 아키타입·잭팟태그 실제
  발동·피버 게이지·POUCH 오퍼 2-step·심볼 해금 13종·심화 업적/장치·UI 보드·랭킹 3노드 분리.
  **🚧 P7-2/3 선행 blocker(Opus 2차검수 확정)**: 장치(예언/재굴림/MANIP) 경로가 아직 `DeepMode`를
  인식하지 못하고(일반 가중추첨을 그대로 씀) + `RunState.LockedNext`가 "empty"/"random" 주머니
  센티널을 왕복시키지 못하는 구조적 제약 — P7-2/3 착수 전 먼저 해결해야 함(§2-(AA) 참조).
- **테스트**: 신규 `Tests_P7_1_Pouch.cs`(58종 골든·카탈로그 교차대조·START_POUCH·압축패널티/
  EARLY_QUOTA 배선·검증 7규칙 경계(정확히 상한 통과 6종 포함)·PouchDraw 분포+결정론+미지id방어·
  empty/random·deepPity·instant소모(id당 최대1회, 웹 golden 재산출)·asc 상호배제·점수격리·DTO
  왕복(Asc/Deep 최고기록)·deep 런 XP 획득·deep 자동플레이 스모크 5시드). 어서션 20164 →
  22758(+2594), 0 실패.
- **스모크 컴파일**: Unity 에디터 미실행 — `dotnet exec csc.dll` 오프라인 검증(Assembly-CSharp
  런타임 84개·Assembly-CSharp-Editor 6개) 둘 다 0에러·0경고.
- **Opus 2차검수 반영(HIGH2·MED2·LOW일괄, 상세 §2-(AA) 하단)**: ①[HIGH] `ReelView.RandomSymbol()`
  (릴 필러 5곳)이 72종 전체에서 균등 추첨해 신규 58종(스프라이트 없음)이 뽑힐 때마다 빈칸이 보이던
  회귀를 `Symbols.LegacyCount`(14)로 한정해 해소. ②[HIGH] `UiSceneBuilder`의 타이틀/릴 스프라이트
  굽기 루프도 동일하게 LegacyCount로 한정(타이틀 화면은 null 미필터라 실제 회귀, 게임 릴은 소비처가
  이미 null을 걸러 기능 버그는 아니었지만 오염 방지 차원에서 함께 정리). ③[MED] instant 소모를
  웹(game.js:814-828) 그대로 "id당 최대 1회"로 정정(중복 등장해도 1개만 차감 — knot 골든값 5→3에서
  5→4로 재산출). ④[MED] 어서션 기준선을 P6 마지막 공식 기록(20164)으로 정정. ⑤[LOW 일괄] DTO 왕복
  테스트에 승천/심화 최고기록 5필드 추가, PouchOps의 미지 id 무시 계약을 주석+테스트로 명문화, 덱
  검증 7규칙 중 상한형 5규칙의 "정확히 상한" 통과 케이스 6종 추가, 장치/LockedNext 이슈를 P7-2/3
  선행 blocker로 명시. 부수 발견: Editor 스모크가 스테일 참조 dll을 그대로 썼던 절차 결함도
  함께 정정(§2-(AA) 하단 "재검증" 각주).

## 2026-08-09 - 웹 파리티 P6 — 승천(심화 학기) A1~A10

상세는 `Docs/WEB_PARITY_DESIGN.md` §2-(Z) 참조. 요약:

- **ascMods 6축**: 신규 `Engine/Core/AscMods.cs` — 요구EXP×(1+0.08a)·보스요구 A4+·상점가 A3+·
  아이템칸 A5+ -1·시작코인 A3+ 감소·점수×(1+0.12a) 전부 웹과 동일한 실제 계산 지점에 배선
  (`SpinResolver.QuotaOf` 신규 오버로드 15곳 호출부·`Shop.ShopPriceMul/ItemPriceMul`·`ItemUse.
  EffectiveSlots`·`RunController` 생성자 시작코인·`StageFlow.ForceGameOver` 최종점수).
- **단계 규칙 5종**: 신규 `Engine/Run/AscRunHooks.cs` — A2 해골 weightAdd 가산 + A8 금지심볼
  symbolWeightMul=0(실제 롤 mods 6곳에 적용) · A7 프리즘 증강 픽 저주 자동부착(RELIC/SILVER 이하는
  제외, `NodeEvents.PickOffer`) · A9 코인투입/예언 쿨다운 stage+2(`DeviceActions`) · A10 최종보스
  2페이즈(`StageFlow.ClearStage` — 1페이즈는 점수/코인/노드/카운터 미반영 후 요구치×1.3로 재시작,
  신규 `RunEvent.BOSS_PHASE2` 타입으로 "진짜 클리어 아님"을 구분해 보스 카운트 중복을 막음).
- **점수 격리 + 졸업**: `PlayerProfile.AscMax/BestAscScore/BestAscLevel` 신규 — asc>0 런은 bestScore
  대신 별도 최고점으로 기록(단 웹 실소스 재확인 결과 totalScore/bestStage/runs는 asc 무관 항상
  누적이라 그대로 채택 — 작업 지시 문면과 웹 실제 동작이 갈리는 지점, 상세는 §2-(Z) 결정 로그).
  일반 런(asc=0)의 스테이지15 클리어도 졸업으로 인정돼 `ascMax=0` 확정 → 승천 선택기 해금.
- **UI**: 홈 화면 승천 선택기(◀ 심화N ▶ + 규칙 설명, 졸업 전엔 숨김) · HUD 승천 배지 · 런 로그에
  금지심볼/A7저주동반/A10 2페이즈 안내(웹은 지속 배지가 아니라 토스트 1회성임을 재확인 후 로그
  방식으로 이식) · 런종료 보드 승천 표기. `Editor/UiSceneBuilder.cs`에 신규 UI 구성 전부 반영
  (씬 리빌드는 Fable이 배치 처리).
- **버그 발견 즉시 수정**: 테스트 작성 중 `StageFlow.ForceGameOver`가 최종점수에 ascMods.scoreMul을
  전혀 곱하지 않던 누락을 발견해 그 자리에서 배선(asc=0은 완전 무변화라 회귀 없음, 신규 테스트로
  직접 검증).
- **테스트**: 신규 `Tests_P6_Ascension.cs`(6축 손계산·단계규칙 5종 고정시드·점수격리·졸업→ascMax→
  선택상한·숙련도 배선·asc=10 자동플레이 하네스 등 14개 항목). 어서션 20016 → 20152(+136), 0 실패.
- **스모크 컴파일**: Unity 에디터 미실행이라 `dotnet exec csc.dll` 오프라인 검증 — Assembly-CSharp
  (81개, 신규 AscMods.cs/AscRunHooks.cs 포함)·Assembly-CSharp-Editor(6개) 둘 다 0에러·0경고.
- **Opus 2차검수 반영(엔진 정확도 통과·totalScore 웹 채택 승인, 5건)**: ① `RunView.RejectReasons`에
  `DEVICE_COOLDOWN` 한글 문구 추가(웹 토스트 대응, A9 쿨다운 거부 시 무안내였던 결함 해소). ②
  `ProfileDto.FromDto`에 ascMax 로드 마이그레이션 가드 추가 — `(ascMax==0 && graduations==0)`이면
  -1로 강제 정정(JsonUtility가 구세이브 빈 필드를 0으로 채워 "이미 asc0 졸업"으로 오판정할 위험 차단,
  정당한 ascMax=0은 반드시 graduations>=1을 동반한다는 논리로 무결하게 구분). ③ §2-(Z)에 RewardDone
  프리뷰의 웹 자체 회귀(웹은 미리보기에 ascMods 4항을 곱하지 않아 실제 quota보다 항상 작게 표시 —
  Unity는 재현하지 않고 정확값 표시, §0 예외 조항) 명시. ④ `HandQuota` 순환검증을 보완하는 quota
  리터럴 고정값 테스트 신설(stage15·asc10 조합, bossPhase2 true/false 각각 3010/2315). ⑤[권장]
  HUD 승천 배지를 SetActive 토글로 전환(일반 런 HUD 폭 낭비 제거) + 상점가 테스트를 콘텐츠 원본
  기준가로 재작성(시스템 산출값 간접 대조 → 직접 대조) + ASC_RULE[2] 문구가 웹 원문 자체의
  미완성 오타임을 주석에 명시 + "16개 호출부"→"15곳" 표기 정정. 어서션 20152 → 20164(+12), 0 실패
  (오프라인 스모크 컴파일 재검증 포함 0에러·0경고 불변).
- **생략/보고 대상**: 랭킹 3노드 분리는 P7-4로 이관(bestAscScore 기록만 이번에 시작) · 심화모드(deep)
  와의 상호배제는 P7에서(현재 주석만) · REWARD_DONE 능력치 패널에 ascMods 6축 개별 행 미노출(실제
  계산에는 전부 반영, 표시 패널만 범위 밖).

## 2026-08-09 - 웹 파리티 P5(마지막 세부 페이즈) — 사운드(절차 합성 SFX 16종 + BGM)

상세는 `Docs/WEB_PARITY_DESIGN.md` §2-(Y) 참조. 요약:

- **절차 합성 엔진**: 신규 `Scripts/Game/SoundKit.cs`(DontDestroyOnLoad 자가부팅 싱글턴, `AppRoot`와
  독립) — 웹 `public/play/sound.js`의 `tone()`(오실레이터+지수 게인 엔벨로프+주파수 슬라이드)/
  `noise()`(선형감쇠 화이트노이즈+bandpass, Web Audio spec 공식 그대로 구현)를 기동 시 `AudioClip.
  Create`로 오프라인 합성해 캐시(sfx 16종 + BGM 음표 팔레트 9클립), 재생은 `AudioSource` 풀(8개)
  `PlayOneShot`만. "SFX 17종" 표기는 sound.js switch case 16개(select/buy/error 3종은 웹 자체에서도
  호출되지 않는 죽은 코드, 합성만 하고 배선 안 함) + BGM 1종의 합.
- **트리거 배선**: tap(전역 PressFx, 스핀 버튼 5개만 제외)·spin·reel(릴별 착지)·win/jackpot·bomb·
  clear/perfect/fanfare(130ms지연)/win(150ms지연)/boss(70ms지연, 클리어+스테이지 최초진입 2곳)·
  perk(오퍼 픽)·coin(상점구매·소리토글·볼륨release)·gameover·bgmStart(Play씬 Spin/PostSpin)/bgmStop
  (Intro씬 진입+게임오버) — 전부 `ui.js` 실제 호출 지점 grep 대조로 배선.
- **설정 완성**: `SettingsSheet`의 "준비 중" 소리/볼륨 자리를 실 토글+`Slider`(이 프로젝트 최초
  uGUI Slider)로 교체, `MenuView`에 홈 소리 토글 신설(웹 `renderHome` sndtog 자리). PlayerPrefs
  `jackpotrun_sound`(기본켜짐)/`jackpotrun_vol`(기본0.7).
- **결함 발견 + 수정**: `Editor/UiSceneBuilder.cs`의 `UICamera`에 `AudioListener`가 없어(전수 확인)
  씬에 리스너가 아예 없었다 — 사운드가 전혀 안 들렸을 근본 결함. `SoundKit` 자신의 DontDestroyOnLoad
  오브젝트에 리스너를 보장해 씬 리빌드와 무관하게 해결.
- **스모크 컴파일**: `dotnet exec csc.dll` 오프라인 검증(Unity `D:\Unity\2022.3.39f1` Managed +
  Library/ScriptAssemblies + NetStandard 2.1 ref/netfx shim17, 참조 경로를 이번에 처음으로 재현
  가능한 형태로 §2-(Y)에 기록) — Assembly-CSharp(79개, 신규 SoundKit.cs 포함)·Assembly-CSharp-Editor
  (6개) 둘 다 0에러·0경고. `dotnet run --project Client/Jackpot/Tools/EngineTests` 20016 passed 불변
  (Engine/ 무접촉).
- **웹 파리티 로드맵 P5 완료** — 남은 페이즈는 P6(승천)·P7(심화모드)뿐. 씬 리빌드는 Fable이 처리.
- **Opus 2차검수 반영(HIGH1+4, 상세 §2-(Y) 하단)**: ①[HIGH] noise() 게인 엔벨로프가 tone()의 0.008s
  어택 구간을 잘못 공유해(spin/jackpot/perfect/fanfare/bomb 5종 타격감 오류) `ToneEnvelope`/
  `NoiseEnvelope`로 분리(웹은 noise가 어택 없이 즉시 최대치→단일 지수감쇠). ② 슬라이더 핸들
  `sizeDelta`를 `(26,26)`→`(26,0)`(Slider.UpdateVisuals의 y축 앵커 스트레치 덮어쓰기로 62px까지
  튀어나오던 결함). ③ 볼륨 드래그 중 매 프레임 `PlayerPrefs.Save()` 제거 — `SetVolume`은 캐시만,
  신설 `SaveVolume()`을 release/시트Hide 시 1회만. ④ 소리 토글 양방향 동기 — `SettingsSheet.Show`에
  `onHide` 콜백 추가, `MenuView`가 시트 닫힐 때 홈 라벨 재동기화(웹 syncSndIcons 대응). ⑤ tap 보강
  2곳(릴 셀 탭·시트 딤 배경 탭 — 둘 다 PressFx 미부착 raw Button이라 무음이었음) + 슬라이더 트랙
  두께 10f→18f(S13 §A 9-slice 위반 해소). 재검증: 스모크 컴파일 0에러·0경고, EngineTests 20016
  passed 불변.

## 2026-08-09 - 웹 파리티 P4(3/3, 마지막) — 튜토리얼 + 설정 시트 + STAGE_CLEAR 보드 정합 + MANIP final 파리티

P4 마지막 슬라이스. 상세는 `Docs/WEB_PARITY_DESIGN.md` §2-(X) 참조. 요약:

- **A. 튜토리얼 3단**: 신규 `UI2/Run/TutorialOverlay.cs`(RunView 소유) — 웹 TOUR 6스텝(스포트라이트,
  astral 제거) → 결과 해설(첫 스핀 후 0.26s 지연 배너) → 라이브 안내(phase 전환마다, REWARD_DONE에서
  종료+`PlayerProfile.MarkTutorialDone()`). 대상 하이라이트는 진짜 컷아웃 대신 골드 테두리 프레임
  (`RectTransformUtility.CalculateRelativeRectTransformBounds`로 다른 컴포넌트 소유 RectTransform
  위치를 매 스텝 재계산) — 작업 지시가 명시적으로 허용한 대안. 트리거: 미완료+stage1/spinIndex0이면
  420ms 후 자동 시작(웹 동일 지연) + HUD "?" 버튼으로 수동 재시작. `PlayerProfile.TutDone`+
  `ProfileDto.tutDone` 신규.
- **B. 설정 시트**: 신규 `UI2/SettingsSheet.cs` — 진동 토글(PlayerPrefs, 즉시 동작)·소리/볼륨(P5
  예약, 비활성 "준비 중")·닫기. 홈(MenuView 우상단 ⚙)+런(HUD "?"+"⚙") 양쪽 진입점, 화면별 전용
  인스턴스. 데이터 초기화 행은 홈 인스턴스에만 짓는다(웹 설정 시트엔 애초에 없는 요소, 홈 전용
  `.reset-link`와 별개 — `BuildSettingsSheet` `includeReset` 매개변수). 진동은 `AndroidJavaObject`
  경유 `VibrationEffect.createOneShot(15ms)`(API 26+, 구버전 폴백)로 웹의 짧은 확인 진동에 근사—
  `PressFx`(골드 버튼 탭 피드백)에 훅.
- **C. STAGE_CLEAR 보드 웹 정합**: `StageFlow.ClearOutcome`에 `stageExpAtClear`/`quotaAtClear`/
  `usedSpins`/`totalSpins`/`lastSpinGain` 5필드 신설(런 리셋 전 스냅샷). `NodePanel`에 2바(달성
  EXP%·사용 스핀)+마지막 스핀 5칸/획득 내역+누적 총점수+"점수 상세" 토글(stage×50·초과×2·
  남은스핀×100·보스·연승 분해) 섹션을 스크롤 카드 영역 상단에 추가(뜬 배너 구조는 무변경).
- **D. MANIP final 파리티**: `DeviceActions.HandleManip`/`GamblerReroll`이 `run.LastCells`(raw)
  대신 `run.LastCellsFinal`(웹 `manip()`의 `r.lastCells` = 항상 최종 칸)에서 조작 대상을 복원하도록
  정정 — 폭탄/자석 스핀 직후 dev_pin/dev_copy가 화면에 보이는 칸 기준으로 동작한다(§2-(W) "신규
  발견" 해소). 재수강(`ItemUse.UseRetakeForm`)은 웹이 애초에 `r.lastCells` 값을 안 읽어 대상 아님.
  부수: `run.LastNotes` 신규(웹 `r.lastResult.notes` 대응, STAGE_CLEAR 보드 "획득 내역"이 소비).
- **테스트**: 폭탄 탐색 후 dev_pin/dev_copy가 화면 기준으로 동작하는지 신규 검증(6000시드) + 기존
  MANIP 픽스처 3곳 보정(LastCellsFinal도 함께 채움) + ClearOutcome 신규 필드 손계산 + TutDone 왕복 +
  점수 상세 분해 합==gainedScore 회귀 가드. 어서션 20004 → 20016(+12), 0 실패. 스모크 컴파일
  (csc.dll 오프라인, Unity 2022.3.39f1) 0에러.
- **Opus 2차검수 반영(필수6·MED4·LOW6)**: ①[CRITICAL] `TutorialOverlay` 배너 콜백이 자기 자신을
  넘겨 확인 버튼에서 무한 재귀(스택 오버플로)로 이어지던 결함 수정 — 일반 라이브 배너는 웹처럼
  닫기만(null), REWARD_DONE 배너만 확인 클릭 시 `EndTutorial` 실행(이전엔 배너를 띄우자마자
  사라졌음). ②[HIGH] stage≥2 폴백 종료를 `phase==Spin||PostSpin`에서만 평가하도록 한정 — 이전엔
  스테이지 클리어 직후 NodeSelect 진입 즉시 라이브 배너 4종을 못 보여주고 튜토리얼이 조기 종료됐다.
  ③[HIGH] 툴팁 세로 배치에 툴팁 자신의 반높이 반영 + 화면 클램프 추가(action 스텝 스핀 버튼과
  겹침 방지), 툴팁 배경 raycastTarget=false. ④[MED] 진동을 `Handheld.Vibrate()`(안드로이드 ~500ms
  롱버즈)에서 `VibrationEffect.createOneShot(15ms)`로 교체(웹 근사 길이). ⑤[MED] NodePanel EXP%
  라벨 언클램프(바만 100 클램프) + `lastSpinGain`을 `run.LastGain` 스냅샷 기준으로(벨/아이템
  클리어 "+0" 오표시 해소) + `BuildClearDetail` 널가드. ⑥[MED] 런 화면 설정 시트에서 데이터
  초기화 행 자체를 제거(웹 설정 시트에 없는 요소 — "안내 토스트로 대체" 절충안 폐기). ⑦[LOW
  일괄] 자동 시작을 1회성 코루틴에서 매 액션 후 재평가(`MaybeAutoStart`)로 · 결과해설 배너가
  0.26s 대기 후 `_active`/`_live` 재확인 · `RunState.LastNotes`를 readonly+Clear/AddRange로
  통일(LastCellsFinal과 대칭) · `ManipPickPopup` 칸 수 소스를 LastCellsFinal로 · 점수 상세 분해
  합 회귀 어서션 · RunView 주석 정정.
- **웹 대비 생략/보고 대상**: 튜토리얼 하이라이트는 테두리 강조(컷아웃 아님) · 라이브 안내 5단계를
  Unity 화면 구조에 맞춰 4단계로 병합(STAGE_CLEAR+NODE_SELECT 합본) · 설정 시트 볼륨은 정적 비활성
  행(실 Slider는 P5) · 마지막 스핀 5칸 표시는 보조릴 6칸 스핀의 6번째 칸 미표시(로직 무관, 표시만)
  · 씬 리빌드·프리팹·.meta·시각 검수는 Fable 담당.

P4 페이즈 완료(1/3 홈·2/3 REWARD_DONE/셀정보/클리어등급·3/3 이번 슬라이스 + Opus 2차검수 반영).
다음은 P5(사운드).

## 2026-08-09 - 웹 파리티 P4(2/3) — REWARD_DONE 화면 + 셀 정보 탭 + 클리어 등급 연출

- **A. REWARD_DONE 페이즈(웹 `_enterRewardDone`, game.js:1573-1585)**: `RunPhase.RewardDone` 신설.
  노드/상점 처리를 마친 뒤 곧장 `Spin`으로 돌아가지 않고 이 화면에서 대기한다 — `RewardFlow.Enter`
  (신규 헬퍼)로 `NodeEvents.cs`(Rest/Gamble/Curse/Risk/EVENT테이블/AugLevel무보상/PickOffer/
  HoldAugment) + `Shop.cs`(Leave)의 종전 `Phase=Spin` 대입을 전부 교체했다. 예외 1곳: `TakeDevice`
  (DEVICE 노드 확정)는 웹 `deviceNodeTake`가 `_enterRewardDone`을 건너뛰고 곧장 `_beginStage()`로
  가는 것과 동일하게 그대로 Spin 직행 유지. `RunController.ProceedToStage`(신규 액션, "스테이지 N
  시작" 탭) → `RunPhase.Spin`. Unity는 스핀수/요구치를 스테이지마다 캐시하지 않고 매번 즉석 계산하는
  구조라(`SpinResolver.EffSpins/QuotaOf`) 웹 `_beginStage()`가 하던 재계산이 이미 `StageFlow.
  ClearStage` 시점에 끝나 있음 — `ProceedToStage`는 순수 phase 게이트.
  - `RunState.RewardMessage`(웹 `r.rewardMsg`) + `RunState.ShopBoughtLabels`(웹 `r.shopBought`,
    상점 진입 시 리셋·구매마다 누적) 신규 필드. 메시지는 엔진에서 직접 조립(웹 문구 그대로 또는
    Unity 실지급 내역 기반 — EVENT 테이블 case4/6은 §1-A #4/(F) 결정으로 이미 웹과 수치가 갈라져
    있어 웹 리터럴을 베끼지 않고 `EventRewardMessage`가 RunEvent 필드로 재구성).
  - `RewardDoneInfo.cs`(신규) — `RewardDoneView.NextPreview(run)`(웹 `nextPreview`, quota/spins/
    bossId) · `CurrentStats(run)`(웹 `currentStats()`, mods 15행 + 심볼EXP/심볼점수/태그 델타 —
    심볼 라벨은 emoji 대신 한글 이름으로 astral 렌더 문제 회피).
  - UI: `RewardDonePanel.cs`(신규) — 보상 메시지 → 보유 효과(증강/유물/저주/장치, BagPopup 행
    관례 재사용, 웹과 달리 처음부터 전부 펼쳐 보임·탭 토글 생략) → 현재 능력치(GainPanel Inner/
    Label·Value 관례 재사용) → 다음 스테이지 프리뷰 → [스테이지 N 시작]. `RunView`/
    `UiSceneBuilder.BuildRewardDonePanel` 배선.
- **B. 셀 정보 탭(웹 `cellInfo`, game.js:2706-2787 / `openCellSheet`, ui.js:959-1010)**:
  `CellInfoView.cs`(신규) — 칸 EXP/점수 분해(기본→심볼 보너스→태그 보너스→해골→가운데 배수) +
  전체배수 + 이 칸에 영향 주는 증강/유물/캐릭터/저주 델타 라벨(baseline `buildMods("basic",
  "gambler",[])` 대비 diff). `RunState.LastMods` 신규(웹 `r.lastMods`) — `SpinResolver.ResolveSpin`
  + `DeviceActions`의 MANIP/도박꾼재굴림 2곳 + `ItemUse.UseRetakeForm`에서 캐시해 "그 칸이 실제로
  나온 스핀"의 mods로 정확히 분해(현재 mods 재계산이 아님). 심화모드 pouchInfo는 Unity에 심화모드
  자체가 없어 미이식.
  - UI: `ReelView`에 셀 탭(Button 컴포넌트, `SetCellTapHandler`) 추가 — 결과 없는 칸은
    `CellInfoView.Build`가 null을 반환해 조용히 무시. `CellInfoSheet.cs`(신규, BagPopup류 스크림
    닫힘 팝업) — RewardDonePanel과 동일한 두 행 템플릿 관례(Inner/Label·Value, IconSlot+InfoCol)를
    재사용. `RunView`/`UiSceneBuilder.BuildCellInfoSheet` 배선.
  - **Opus 2차검수(2026-08-09) 필수 반영**: ①`RunState.LastCellsFinal`(신규, `List<Cell>`, 웹
    `r.lastCells = res.cells` 대응) 도입 — 예전엔 `LastCells`(원시 재굴림 입력, Evaluate 이전
    스냅샷)를 읽어 폭탄 제거·자석 복사 후의 실제 릴 표시와 셀 정보가 어긋날 수 있었다.
    `SpinResolver.ResolveSpin`·`DeviceActions`(MANIP·도박꾼재굴림)·`ItemUse.UseRetakeForm` 4곳에서
    `Evaluate` 직후 갱신, `CellInfoView`는 이제 이 필드만 읽는다(LastCells는 재굴림 입력 용도로
    유지). ②빈칸(SpinResolver.EmptySym) 처리 시 `Sym` enum 자리표시값(EmptySym.sym=Sym.Cherry,
    실사용 안 함 전제)으로 `perSymbolExp`/`perSymbolScore`를 조회하면 "빈칸인데 체리 보너스가
    새어 들어오는" 오판정이 날 수 있어 empty면 무조건 0 고정하는 가드 추가 — 동시에 `Cell.tag`가
    이제 보존되므로 자석("🧲")·씨앗성장("🌱→") 특수 안내도 마저 복원(예전 슬라이스의 "재현 불가"
    범위축소가 해소됨). ③CellInfoSheet.cs 2곳(specials/sets 텍스트)에 방어적 StripAstral 추가 +
    CellInfoView.cs의 "🪙" 리터럴을 "(코인)"으로(엔진 산출 문자열 규약 자기위반 해소). ④
    `RewardDoneView.NextPreview`·`GameSession.PreviewQuotaSpins`에 `ApplyPassiveDevice` 누락 수정
    — dev_reactor(quotaMul×1.15) 장착 시 프리뷰 요구 EXP가 실제보다 15% 낮게 보이던 결함 해소,
    `CurrentStats`까지 3곳 모두 Build→ApplyPassiveDevice→ApplyItemMods로 통일. ⑤`CellInfoSheet.cs`
    Awake()의 `gameObject.SetActive(false)` 자기호출 결함 발견·제거 — 빌더(`BuildSheetChrome`)가
    이미 비활성으로 구워 두는데 Awake에서 다시 끄면, 씬 로드로 지연된 Awake가 `Show()`의
    `SetActive(true)` 직후 동기 실행되며 그 활성화를 스스로 되돌려 최초 오픈 1회만
    `StartCoroutine(EnterRoutine())`이 조용히 실패했다(2회째부터는 Awake가 다시 안 불려 정상으로
    보임) — 같은 패턴을 가진 기존 `BagPopup`/`ManipPickPopup`/`ConfirmSheetPopup`/`DexView.
    DexDetailPopup`(4건, 전부 사전 존재 결함)도 함께 수정.
  - **LOW 일괄 반영**: retake_form(ItemUse.cs)이 `LastMods`를 갱신하지 않는 이유(웹 `_freeReroll()`
    도 `r.lastMods`를 안 건드림 — MANIP·도박꾼재굴림이 타는 통합 `manip()`만 갱신, game.js:1286)를
    주석으로 명시. `CellInfoView`의 skullExp가 `perSkullExp`를 반영하지 않는 단순 근사임을(웹
    cellInfo 원본부터 그런 quirk — GainPanel의 "해골 페널티 근사치"와 동일 성격) 주석 명시.
    `LabelDiff`의 태그 델타 표기를 "#{t}태그"→"{t}태그"로 정정(웹 cellInfo label()과 openCellSheet
    행 라벨은 "#" 유무가 다른 별개 표기 — 혼용 수정). `NodePanel.ConfettiBurstsByTier`의 죽은
    배열 원소 제거(6개 중 index5 미사용) + perfect 강도를 tier1 근처로 하향(웹 raw 색종이 개수는
    perfect=30이 tier1=24보다 살짝 많을 뿐 tier2=40보다 한참 적다 — 예전 값은 tier3급으로 과했음).
    `RunView`의 셀 탭 핸들러에 `_busy`/`_session` null 가드 추가. `RewardDoneInfo.cs`(파일명)와
    `RewardDoneView`(주 타입명) 불일치는 리네임 대신 헤더 주석으로 관계를 명시.
- **C. 클리어 등급 연출(웹 `stageClearFx`, ui.js:1700-1739)**: `ClearOutcome.grade/gradeTier`는
  P2에서 이미 존재 — 이번엔 연출만 추가. `NodePanel`의 클리어 배너에 tier별 색(1-2초록·3파랑·4보라·
  5+PERFECT골드) + 등장 펄스(OutQuad→OutBack 팝) + 색종이 escalation(`FxId.Clear` 반복재생 1~5회,
  웹 24/40/58/78/104 색종이 개수를 "재생 횟수"로 근사) 추가. `ReelView.PlayClearShake(tier)`(신규) —
  웹 `shake(tier>=5?"xl":tier>=3?"bg":"sm")` + "tier≥4면 230ms 후 2차 흔들림" 그대로, `NodePanel`이
  콜백(`Action<int> onShake`, `RunView`가 `reelView.PlayClearShake`로 연결)으로 배너 등장과 같은
  타이밍에 트리거.
- **테스트**: `Tests_P4_RewardDoneCellInfo.cs`(신규) — ProceedToStage phase 게이트, 노드별
  RewardMessage 문구(Rest/Shop구매유무/Gamble/AugLevel무보상/Device예외), NextPreview 손계산
  2케이스(비보스 stage1, 보스 stage5), CurrentStats 손계산(퍽 없음 1케이스 + study·cherry_up
  조합), CellInfoView 손계산(기본 분해 3칸 + cherry_up 영향 퍽 델타 라벨 + 무관 칸 제외 확인).
  **Opus 2차검수 필수⑥ 테스트 보강 4건 추가**: 실제 `RunController.Do(new Spin(...))` 결과에서
  "클린" 스핀(세트·잭팟·해골·특수효과 없음)의 칸별 cellExp 합이 `result.exp`/`outcome.gained`와
  정확히 일치하는지(4000시드 중 최소 3건 확보) · 폭탄/자석 포함 스핀을 시드 탐색으로 찾아
  `LastCellsFinal`이 릴 표시(빈칸·복사칸)를 정확히 반영하는지(6000시드) · MANIP 전후 사이에 퍽을
  추가해 `LastMods`(1.0→1.10)·`LastCellsFinal`이 그 순간 값으로 실제 재계산되는지 · EVENT 10종
  표의 `RewardMessage` 정확한 문구(coinsDelta가 scoreDelta보다 먼저 조립되는 실제 필드 순서까지
  검증). 기존 `Tests_S4.cs`/`Tests_P3_AugLevel.cs`/자동플레이 하네스(Tests_S4/S5/P3_Mastery/
  PlayerLevel)의 `RunPhase.Spin` 기대값 전수 갱신 + `RunPhase.RewardDone` 케이스 추가(P3-3
  EventAugLevel 선례 그대로). 어서션 19867 → 19937(1차) → **20004(Opus 2차검수 반영 후, +67)**,
  0 실패.
  스모크 컴파일(csc, Unity 2022.3.39f1 Managed DLL 대조, 2차검수 반영 후 재확인): Assembly-CSharp·
  Assembly-CSharp-Editor 둘 다 0에러(경고는 전부 기존 관례와 동일한 미할당 SerializeField CS0649).
- **이탈·생략(보고 대상)**: ① 웹의 "보유 효과 칩 탭→상세 토글" 2단 인터랙션을 RewardDonePanel에서
  상시 펼침으로 단순화(정보량 동일, 탭 수만 감소). ② 색종이 개수(24/40/58/78/104)는 웹 CSS 파티클
  카운트라 1:1 이식 대상이 아니라고 판단해 "프리팹 반복재생 횟수"로 근사(작업 지시 "근사" 명시
  범위). ③ **신규 발견(2차검수 범위 밖, 다음 슬라이스 보고 대상)**: `DeviceActions.HandleManip`이
  조작 대상 칸을 `run.LastCells`(raw, Evaluate 이전)에서 복원한다 — 웹 통합 `manip()`은
  `r.lastCells.map(...)`(이미 최종본)에서 복원한다(game.js:1238). 폭탄/자석 등으로 원본과 최종이
  갈리는 스핀 직후 MANIP을 쓰면 "화면에 보이는 빈 칸"이 아니라 "그 뒤에 있던 원본 심볼"을
  조작하게 되는 파리티 차이가 있다 — 이번 슬라이스는 표시 전용(CellInfoView) 범위만 다뤄 이
  게임플레이 로직 자체는 손대지 않았다. 씬 리빌드·.meta는 Fable 배치 처리 예정(빌더 코드만 이번
  슬라이스 범위).

## 2026-08-08 - 웹 파리티 P4(1/3) — 홈 화면 + 레벨 보상 화면 + 런종료 XP 블록

- **엔진(최소 데이터 노출)**: `Formulas.PlayerLevelProgressFromXp`(신규, 웹 `levelInfo()` 그대로 —
  level/inLevel/need/ratio/max) + `PlayerProfile.LevelProgress()`(위임). `PlayerLevelFromXp`는 같은
  루프를 공유하도록 리팩터(행동 동일). `PlayerProfile.LevelUnlocks()`는 P3-4에서 이미 준비돼 있어
  재사용만 함.
- **A. 홈 화면(MenuView)**: 웹 `renderHome` 순서로 재구성 — **레벨 카드**(신규, 클릭형 → 레벨 보상
  화면) → **게임 모드 선택기**(신규, 일반 선택됨 + 심화·심볼덱 "준비 중" 배지, 탭 시 토스트, P7
  미구현) → (승천 선택기는 P6 미구현이라 렌더 생략, 주석 예약) → 기존 hud/버튼/설명 유지 →
  **데이터 초기화**(신규, `ConfirmSheetPopup` 재사용 — `ProfileStore.Delete()` + `AppRoot.
  ResetProfile()`). 소리 토글은 P5 예약(주석만).
- **B. 레벨 보상 화면(신규)**: `LevelRewardsView.cs` + `UiSceneBuilder.BuildLevelRewardsScreen` —
  레벨 카드(비클릭형) + `PlayerProfile.LevelUnlocks()` 로드맵을 레벨순 행 목록(`RankView` 템플릿
  clone 패턴 재사용)으로 표시, 해금/잠김을 "해금"/"잠김" 한글+색상으로 구분(자물쇠 이모지 astral
  대체 — S8 항목⑤ 기존 관례). `ScreenRouter.ScreenId.LevelRewards` 신규.
- **C. 런종료 XP 블록(GameOverPanel)**: 웹 `renderEnd` endxp 블록 이식 — "플레이어 레벨 Lv.N" +
  "+N XP"(카운트업) + 레벨업 시 "Lv.A → Lv.B" 강조(OutBack 팝인) + XP 진행바(레벨 유지 시 트윈) +
  "다음 레벨까지 N XP"/"최고 레벨 달성". `FailureOutcome.PlayerXpGain`/`PlayerLevelBefore/After`
  (P3-1에서 이미 준비됨)를 소비만 함.
- **Opus 2차검수 반영(같은 날)**: ①`ResetProfile()`에 `PlayerPrefs.DeleteKey(LoginView.NickPrefKey)`
  추가(웹 slotweb_nick 제거 파리티, 랭킹 PlayerPrefs는 유지) ②XP 연출을 `EnterRoutine`(딤+스케일인)
  완료 **후**에 재생하도록 정정(카드가 접혀 있는 동안 끝나버리던 결함) + MAX 레벨 바 트윈 버그
  (0→100% 오재생) 수정 ③레벨 카드 badge/body 레이아웃 — `HorizontalLayoutGroup.
  childForceExpandHeight`가 `flexibleHeight=0`도 강제 승격시키던 결함 수정(badge 정사각 유지·body
  세로 중앙 정렬) ④GameOverPanel XpBlock 고정 높이(164)→`ContentSizeFitter` 자동높이(achContent와
  동일 관례)로 교체, 하단 공백 제거 ⑤게임 모드 "일반" 카드에도 Button+PressFx(웹은 양쪽 다 버튼)
  ⑥경미 정리 6건(죽은 필드 제거·MAX 바 Green 색 제거·주석 오기/정확도 정정·자물쇠 이모지 주석 제거·
  로드맵 빈 상태 "해금 항목 없음" 폴백). MSBuild 스모크 0에러 + EngineTests 19867 passed 재확인.
- **검증**: MSBuild 스모크(Assembly-CSharp + Assembly-CSharp-Editor) 0에러, `dotnet run --project
  Client/Jackpot/Tools/EngineTests` 19867 passed·0 failed(+19 신규 어서션). 씬 리빌드·육안 검수는
  Fable이 배치로 진행 예정.
- **생략/후속**: 승천 선택기(§1-A #15 A.3, P6 대기) · 소리 토글(P5) · P4 잔여 항목(REWARD_DONE
  능력치 패널·셀 정보 탭·클리어 등급 6단계 연출·튜토리얼·설정 시트, §1-A #15/#16)은 다음 슬라이스.
  자세한 내용은 `Docs/WEB_PARITY_DESIGN.md` §2-(V) 참조.

---

## 2026-08-08 - 웹 파리티 P3.5 — 퍽 오퍼 알고리즘 웹 완전 동기화 (P3-4 후속 3건)

- **①PERK_FAMILY 랭크 게이팅 이식**: 신규 `Content/PerkFamily.cs` — 웹 `data.js` AUG_FAMILY(51)+
  REL_FAMILY(45)=96종을 `(패밀리키,랭크)` 튜플로 손전사(Unity `Perks.cs` 178개 id와 bash로 사전 대조
  검증). `Shop.PickPerksByTier`가 `eligible(p)=rank==heldFamCount(fam)+1` + 오퍼당 같은 패밀리 1개
  (`usedFams`)로 후보를 거른다(웹 engine.js:1229-1239).
- **②오퍼 알고리즘 전면 웹 대조·정렬**: `Shop.PickPerksByTier`를 웹 `pickPerksByTier`(engine.js:
  1213-1241) 리터럴 포팅으로 재작성 — 스테이지 가중 확률 롤(TierWeights/RollTier, forceTire가 항상
  확정돼 넘어오는 현재 호출구조에서 이미 죽어있던 코드) 제거, "티어 풀 소진 시 avail 전체 폴백"(2026-
  08-03 승인 예외, 근본원인이던 BASE22종 게이트는 §2-(P)가 이미 해소)→웹 기준 PRISM→GOLD→SILVER
  단계형 폴백으로 환원(ENGINE_PORT_DESIGN.md S16 §A에 역참조 각주 추가). **forceRare(불운게이지
  만땅) — 죽은 분기만 건드려 실질적으로 무효과였던 버그를 발견**, `NodeEvents.OfferPerks`에서
  nodeTier 재구현. **[Opus 2차검수 반영] Fable 결정으로 범위 확정**: kotlin 의도("silverW=0"=
  "GOLD 이상 보장") 그대로 **SILVER 노드일 때만 GOLD로 승급, GOLD/PRISM 노드는 무승급** — 게이지는
  오퍼 발생 시 항상 소모(heldPerk 분기 포함). 1차 구현이 이 승급 판정을 `heldPerk!=null`/`else` 분기
  **밖**에 둬 보류파일(dev_holdfile) 사용 중 보류 티어가 강제 등급업되는 회귀가 있었던 것도 Opus
  검수에서 발견해 `heldPerk==null` 분기 안으로 이동(보류 티어 결정형 우선 원칙 복원).
  **dev_major favoredCat — Kotlin 유래 빌드시너지 편향의 웹 기준 제거(밸런스 변경, Fable 승인)**:
  발견한 구현 결함(미장착 상태에서도 매 오퍼마다 몰래 추가 RNG 소비)은 명백한 버그라 고쳤지만,
  "보유 퍽 중 가장 흔한 심볼로 오퍼를 편향시킨다"는 발상 자체는 웹에 없는 Kotlin 원본 산물이라
  dev_major 장착 시로 좁힌 것은 버그 수정이 아니라 밸런스 결정으로 문서 프레이밍을 정정(Opus
  2차검수 필수⑤). dev_holdfile과 같은 "장착시에만 소비" 패턴으로 재배선. `Shop.SetSynergyAug`도
  웹처럼 node 종류와 무관하게 항상 AUGMENT만 주입하도록 정정(RELIC 노드도 AUGMENT 조각 주입 가능) +
  **5% 시너지 롤의 `picks.Count>=2` 검사를 `SetSynergyAug` 호출 앞으로 이동**(웹 engine.js:1262 —
  1~2장 오퍼에서 RNG 미소비, Opus 2차검수 필수③). unlockLevel 게이트를 PickPerksByTier 내부→
  호출자(NodeEvents)로 이동(웹 `_augPool()` 패턴).
  **범위 밖 발견**: 웹의 진짜 상점(game.js:2334-2337)도 `offerPerks`를 쓰는데 Unity `Shop.FreshOffer`
  는 여전히 별도 Kotlin 유래 가중 롤을 쓴다 — 이번 슬라이스 범위 밖, 별도 슬라이스 필요(보고만).
- **③retake_form ctx 반영**: `ItemUse.UseRetakeForm`이 `SpinResolver`와 동일한 2단계 패턴으로 ctx
  포함 mods를 빌드하도록 수정(`SpinResolver.RunCtxOf` internal화, 웹 `_freeReroll()`↔`_mods()`↔
  `_ctx()` 대응). ctx-조건부 퍽 14종이 재굴림 시점에도 실제 run 상태를 반영. `Mods.cs`의 `RunCtx.
  coins=99` "무해 기본값" 주석을 "우연히 안 읽혀서 무해했을 뿐" 정정. 이 2단계는 현재 콘텐츠 기준
  `ResolveSpin`의 3단계와 결과 동치이나 구조적으로는 다르다(향후 ctx-조건부 퍽이 quotaMul/bonusSpins를
  다른 ctx 필드에 의존해 계산하게 되면 갈릴 수 있음) — 주석에 명시.
- **테스트**: 신규 `Tests_P3_5_OfferParity.cs`(4클래스 — PERK_FAMILY 골든·랭크게이팅 경계 4종·오퍼
  고정시드 회귀(7케이스 실제 퍽 id 배열 하드코딩 — SILVER 평상·GOLD+family게이팅·GOLD→SILVER 폴백·
  forceRare 승급/GOLD무승급/PRISM무승급·보류파일 우선순위 + forceTier/bossClear/dev_major 전슬롯·
  RNG미소비 대조)·retake ctx 전파). `Tests_S4_TierPoolFallback` 어서션도 "전원 SILVER"로 강화.
  어서션 19244→19848(+604), 0 실패.
- 상세: `Docs/WEB_PARITY_DESIGN.md` §2-(U). `Docs/ENGINE_PORT_DESIGN.md` S16 §A에 환원 역참조 각주.

## 2026-08-08 - 웹 파리티 P3-4 Opus 2차검수 반영 (필수4 + 웹 이탈 정리6 + 신규 골든1)

- **필수 4건**: ①`NodeEvents.OfferPerks` 프리즘잉크 강제티어 — 10% 등급업 롤을 무조건 먼저 소비한
  뒤 forceTier로 덮어쓰도록 정정(웹 engine.js:1254-1256 RNG 순서 파리티, 이전엔 else-if로 롤 자체를
  건너뛰어 시드 스트림이 갈라졌음). ②`AppRoot.Awake`/`EndRun`이 `ProfileStore.Load()` 직후
  `GrantLevelDevices()`를 호출(+지급 있었을 때만 저장)하도록 추가 — 기존엔 `GameSession` 생성자에서만
  불러 Pick/Dex가 보는 `AppRoot.Profile`이 런 시작 전까지 1런 지연됐음(웹 game.js:179 대응). ③상점
  5필드(shopPriceMul/itemPriceMul/itemCapBonus/shopSlotBonus/shopRerollDelta) 실배선 —
  `Shop.FreshOffer`(가격에 pm/itemPm 곱, 아이템칸 2+slot) · `Shop.RerollCostFor(run)`(신설,
  `max(2,6+shopRerollDelta)`, 구 `RerollCost`→`BaseRerollCost`) · `ItemUse.EffectiveSlots(run)`(신설,
  `3+itemCapBonus`, 구 `ItemSlots`→`BaseItemSlots`) — `Shop.Buy`/`BagPopup`/`RunView`/`ShopPanel`
  전부 이걸로 전환. 승천/심화 항은 P6/P7 미구현이라 주석만 남기고 생략. ④`DexView` 상세 팝업의 스테일
  `e.unlockReq`(catalog.json 구 Kotlin StatReq, manifest.json 미갱신) 렌더 차단 → `pick.unlock`(웹
  OR 문구) 폴백.
- **웹 이탈 정리 6건**: ⑤`RunState.PrismInkBought` 신설 — 프리즘잉크 런당 1회 구매 제한
  (`Shop.Buy`가 코인/가방 체크보다 먼저 거부). ⑥`RunController`honor 시작증강·`ItemUse`
  black_lottery/devil_contract 3곳에서 `Shop.GatedPool` 래핑 제거 → raw `Perks.Augments`/
  `Perks.Relics`로 복원(웹 원본부터 이 3경로엔 해금 게이트가 없음, game.js:393-397·1374-1376 — held
  제외 필터는 그대로 유지). ⑦`refund` 30% 미소모 판정을 `retake_form`에도 적용(웹은 단일 useItem()
  파이프라인이라 예외 없음) — 단 `NO_LAST_SPIN` 사전검증은 keep 롤보다 먼저 둬서 "거부=완전 무변형"
  Unity 불변식은 유지. ⑧`PlayerProfile.LevelUnlocks()` `List.Sort`(불안정)→LINQ `OrderBy`(안정) +
  `LevelDeviceReward` 순회를 키 오름차순으로 고정(동순위 항목 표시 순서 비결정성 제거). ⑨
  `Tests_Content2/Perks/Core.cs`의 catalog↔engine 교차대조를 "engine-only 무제한 허용"에서 "이번
  슬라이스 신규 id 명시 allowlist"로 좁힘(향후 catalog 미갱신 누락을 잡아내게).
- **신규 골든 1건**: `Tests_P3_4_ContentGolden.cs` 신설 — 신규 증강9·유물12·저주16(fx 교체분)을
  Perks.cs를 안 보고 data.js/engine.js를 다시 읽어 옮겨 적은 (id,tier,unlockLevel,price,fx 전체)
  독립 골든 + 상점 5필드 보유 전/후 가격·칸·리롤 변화 손계산 테스트(discount/thrifty/item_bag/vip).
- **검증**: 어서션 18992→19244(+252), 0 실패.

## 2026-08-08 - 웹 파리티 P3-4: 콘텐츠 증보 + 해금 OR + 레벨 보상 (P3 마지막 슬라이스, P3 전체 완료)

- **차집합 산출(python set-diff, 손계산 아님)**: 캐릭 19-16=+3(`regent`/`bankrupt`/`abyss_scholar`),
  머신 19-16=+3(`nightmare`/`throne`/`broke`), 증강 89-80=+9(discount/thrifty/item_bag/vip/refund +
  crown_burst/curse_grad/extreme_overload/abyss_lore), 유물 73-61=+12(PRISM 신설: 보스클리어풀 8+
  레벨해금 4), 아이템 78-73=+5(증강 레벨업 상점 5종: study_note/aug_catalyst/gold_marker/prism_ink/
  overcharge), 장치는 레벨자동지급 3종만(dev_reaper/dev_abyss/dev_reactor — 나머지 9종은 P7 심화전용
  제외). 저주는 id 불변(16=16), fx/desc만 웹 패널티 전용값으로 16종 전량 교체.
- **콘텐츠 데이터**: `Characters.cs`/`Machines.cs`/`Devices.cs`/`Perks.cs`/`Items.cs`에 위 신규분 전사
  (fx는 전부 engine.js 라인 근거 주석 포함). `ContentTypes.cs`에 `Perk.unlockLevel` 필드 신설.
- **신규 효과 훅**: `Mods.cs`에 `cliffBurstExpMul`(phoenix_thesis)·`shopPriceMul`/`itemPriceMul`/
  `itemCapBonus`/`shopSlotBonus`/`shopRerollDelta`(상점 빌드 증강 4종, 소비처는 P4) 6개 필드 신설.
  `ModsBuilder`에 머신 특수효과 switch(nightmare/throne/broke)·캐릭터 3종(regent/bankrupt/
  abyss_scholar, `RunCtx.coins` 신규 필드 필요)·ctx-조건부 4종(curse_grad/black_grad_photo/
  abyss_lore/phoenix_thesis) 추가. `SpinResolver.Evaluate`가 cliffBurstExpMul을 perfectShapeExpMul
  직후·전역배수 이전에 소비.
- **해금 OR 모델**: `Character`/`Machine`의 `unlockReq`(StatReq AND)를 전량 폐기, 웹 game.js:259-277
  OR 5축/4축(unlockRuns/Score/Stage/Level/Ach)으로 교체 — 기존 16+16종도 웹 data.js 값으로 재산출
  (예: gambler/cherry/library는 웹에 unlock 필드가 없어 항상 해금으로 완화). grandfather(cstage_)는
  웹 전수 확인 결과 부재해 폐기. 판정은 `PlayerProfile.IsCharUnlocked`/`IsMachineUnlocked`.
- **퍽 레벨 게이트**: `Shop.PerkGate`/`PerkUnlocked`/`GatedPool`을 "unlockLevel 있는 8종만
  PlayerLevel 게이트, 나머지 154종 항상 개방"으로 전면 재작성 — 기존 전공연구(Schools)·AccountLevel·
  seen_ 그랜드파더 폐기(Schools.cs 파일 자체는 존치, 게이트 연결만 절단). PERK_FAMILY 랭크 순차
  게이팅(오퍼 표시 순서 규칙, 해금 축 아님)은 이번 슬라이스 범위 밖이라 미변경 — 오퍼 알고리즘 자체는
  기존 기술부채 그대로(보고 대상, 별도 슬라이스 필요).
- **레벨 장치 자동지급**: `PlayerProfile.LevelDeviceReward`(14/18/22)+`GrantLevelDevices()`(멱등) —
  웹 `_grantLevelDevices()` 그대로, `GameSession` 생성자(로드 직후)·GAME_OVER 분기(런종료) 양쪽 호출.
  `PlayerProfile.LevelUnlocks()`(P4용 데이터 함수만, UI 없음)도 추가.
- **UI 최소 정합**: `PickMeta.FallbackInfo`(신규 콘텐츠를 Engine 데이터로 합성한 최소 PickInfo) +
  `PickView`가 catalog 미스 시 이 폴백 사용, `CharOrder`/`MacOrder`/`DevOrder`에 신규 9종 추가(안 하면
  선택 불가). `JackpotCatalog.EnsureLoaded()`가 실 JSON 파싱 후 Engine 콘텐츠 기반 합성 엔트리(스프라이트
  없음)를 메모리 조회 테이블에만 이어붙여 DexView에도 신규 콘텐츠가 뜨게 함(실 JSON 미수정).
- **테스트**: 어서션 18787→18992(+205), 0 실패. 신규: Perks fx/meta 스냅샷(신규 21종+저주 16종 재계산),
  캐릭터 exact(OR 5축, 19종), 머신 가중치표(신규 3종), 아이템/장치 exact+fx(신규 8종). 재작성(의미가
  바뀐 기존 테스트): ModsAdditive/ModsAggregation(저주 보너스 삭제 반영) · ShopOffer/TierPoolFallback/
  RetakeExhaustion(Schools 폴백 전제 소멸 → held-기반 소진으로 교체) · SeenGateTracking(그랜드파더 폐기
  반영) · CharUnlockDerivedKeyGate(prodigy 구 파생키 게이트 → 신 OR 모델 검증으로 전환).
- **판단 보류/스코프 경계(보고 대상)**: ① `Shop.PickPerksByTier` 오퍼 알고리즘 자체(가중치분포·
  favoredCat)는 웹 최신 `pickPerksByTier`와 이미 갈라진 기존 기술부채 — 이번 슬라이스는 게이트 축만
  다뤄 미변경. ② 상점 빌드 증강 4종(discount 등)의 Mods 필드는 계산 완료·소비처(상점 화면)는 P4.
  ③ `refund`의 "30% 미소모"는 `ItemUse.Use` 주경로에만 적용 — `retake_form`(직전 스핀 재굴림) 특수
  분기는 기존에도 별도 아키텍처라 미적용. ④ prism_ink 아이템 "런당 1회 구매" 상점측 제한은 미구현
  (Shop 화면 자체가 P4). ⑤ catalog.json 저주 16종 `descKo`·신규 35종 실제 PNG 아트는 아직 구/미갱신
  (manifest.json → convert_manifest.py 파이프라인 별도 작업, 이번 슬라이스는 이모지 폴백만).
- 검증: `dotnet run --project Client/Jackpot/Tools/EngineTests` 18992 passed, 0 failed. Unity Editor
  미실행 환경이라 Game/UI2/Data 레이어(GameSession.cs·PickView.cs·PickMeta.cs·JackpotCatalog.cs)는
  MCP 컴파일 확인 대신 수동 코드 리뷰로 검증(리뷰 중 `DeviceDef.cmd` 미존재 필드 참조 오류 2건 발견·수정).

## 2026-08-08 - 웹 파리티 P3-3: 숙련도 + 증강 레벨업

- **숙련도(mastery)**: `PlayerProfile.Mastery`(kind="char"/"mac"/"dev" → id → runs/bestStage/
  bossClears/bestScore/ascMax) + `BumpMastery`/`MasteryOf` 신설. 마일스톤 판정(`Formulas.
  MasteryLevel`/`MasteryTotal`)은 웹 `MASTERY` 표(game.js:143-165) 그대로 — 5개를 `else-if`
  순차 게이트가 아니라 매번 독립 판정 후 카운트한다(웹 masteryLevel L166-170과 동일 규칙).
  갱신은 런 종료(GAME_OVER) 시점(신규 `MasteryTracker.ApplyRunEnd`, PlayerLevelTracker와 동일
  패턴 — GameSession이 그 다음 순서로 호출). ascMax는 승천(P6) 미구현이라 필드만 두고 영구
  -1(코드 변경 불필요). ProfileDto는 (kind,id) 조합당 1행 병렬 배열 7개로 왕복. `PickView`/
  `DexView` 카드에 ★☆ 표기(웹 ui.js:1886 literal 그대로) — 기존 텍스트 필드 재사용, 최소 침습.
- **증강 레벨업(AUG_LEVELS)**: 신규 `Content/AugLevels.cs` 12종(study/greed/polymath/cherry_up/
  book_up/star_up/diligence/set_sense/coin_luck/skull_study/gem_polish/lucky) Lv2/Lv3 델타를
  웹 engine.js:19-33 그대로 이식 — 기존 `Perks.cs` fx와 동일한 점표기 포맷이라 `ModsBuilder.
  ApplyFx` 해석기를 재사용(새 해석기 없음). `ModsBuilder.Build`에 `levels` 매개변수 추가,
  실제 게임플레이 mods를 계산하는 17개 호출부(SpinResolver·StageFlow·ItemUse·DeviceActions·
  RunController·GameSession) 전수에 `run.PerkLevels`를 연결 — 한 곳이라도 빠지면 그 경로만
  레벨업 미반영이라 전수 갱신했다.
- **레벨 표시(작업 지시 B.4)**: `run.Perks`를 그리는 화면이 기존 UI2에 하나도 없었다(grep 결과
  전무 — 작업 지시가 후보로 짚은 `BagPopup`뿐). 새 화면을 만드는 대신 `BagPopup`(아이템 전용
  팝업)을 최소 침습으로 확장 — 기존 행 템플릿을 그대로 재사용해 레벨업 가능한 보유 증강(`AugLevels.
  IsLevelable`)을 아이템 목록 아래에 "{이름} Lv.N"으로 추가 표시(사용 버튼은 숨김, 증강은 소모형이
  아니므로).
- **AUGLEVEL 노드**: `RunState.PerkLevels`/`AugLevelChance`(pity, 기본10%)/`AugLevelBoost`(촉매
  후크, 대응 아이템 없어 항상 0) 신설. `StageFlow.ClearStage`가 노드 확정 직후 웹과 동일한 확률식
  (10%+pity, 미발동 +2%p 누적 상한20%, 발동 시 리셋)으로 AUGMENT를 AUGLEVEL로 교체(3택 개수는
  그대로 유지 — DEVICE처럼 "추가"가 아니라 "대체"). 신규 `RunPhase.EventAugLevel` + `NodeEvents.
  ChooseNode`/`PickOffer` 분기(PickOffer는 새 퍽 add 대신 `PerkLevels[id]+1`, 신규 이벤트
  `PERK_LEVELED`). UI는 기존 `PerkOfferPanel`(헤더 "증강 강화", 카드에 "Lv.N → Lv.N+1" 배지) ·
  `NodePanel`("⬆ 증강 강화" 카드) 재사용. 부수 발견: 기존 범용 자동플레이 하네스(Tests_S4.cs
  AutoPlay/AutoPlayRich, Tests_S5.cs 2곳)가 신규 phase를 몰라 예외를 던지던 회귀를
  100시드 시뮬레이션이 실제로 잡아내 PickOffer(0) 라우팅으로 함께 수정.
- **테스트**: `Tests_P3_AugLevel.cs`(AUG_LEVELS 12종 골든 델타·Lv3 클램프·미등록id 무영향·
  Lv1vsLv3 스핀 델타·AUGLEVEL pity 불변식(2시드×40회)·게이트 미보유 시 영구 미등장·ChooseNode→
  PickOffer 흐름·Lv3 이후 방어적 무보상) + `Tests_P3_Mastery.cs`(char/mac/dev 마일스톤 경계값
  전수·레벨=독립 카운트 확인·BumpMastery 누적규칙·MasteryTracker 3축(dev 미장착 스킵)·null가드·
  ProfileDto 왕복·자동플레이 2시드 교차검증) 신규.
- **Opus 1차검수 필수 반영(같은 날) — AUGLEVEL 오퍼 3장 캡**: 웹은 레벨업 가능 증강을 전량
  오퍼하지만(CSS 그리드 래핑) Unity `PerkOfferPanel`은 320px 고정 카드 3장 전용이라 4장 이상이면
  화면 밖으로 잘리는 문제를 발견 — `NodeEvents.ChooseNode`의 AUGLEVEL 분기에서 3장 이하는 전량,
  4장 이상은 `run.Rng.Shuffle`로 섞은 뒤 앞 3장만 선발하도록 캡(RNG는 4장 이상일 때만 소비해
  기존 시드 스트림 영향 최소화). 테스트 3종 추가(3장 이하 전량 유지·2/3개 경계·5장 보유 시 3장
  선발+고정시드 재현성+선발결과 전부 레벨업가능 대상 확인+다른시드 분산 확인).
- 검증: EngineTests **18,315→18,803 통과**(+488 어서션, 0 실패). Unity UI2(PickView/DexView/
  PerkOfferPanel/NodePanel/RunView/BagPopup) 컴파일은 이번 세션에 연결된 Unity 에디터 인스턴스가
  없어 MCP `read_console`로 실측하지 못했다 — 코드 리뷰로 대체(다음 에디터 세션에서 1회 확인 권장).
  P3 로드맵 "진행 중(3/4)".

## 2026-08-08 - 웹 파리티 P3-2: 업적 34종 교체

- **`Achievements.cs` 482→34종 전량 교체**(웹 `data.js:774-817` 기본16+후반5+심화13, id/name/desc/
  key/threshold/deep 그대로 — astral 이모지(대부분)는 렌더링 불가라 BMP 5종만 남기고 빈 문자열로
  대체, S8 항목⑤ 선례). 웹에 없는 cat/tier/hidden/reward는 구 "기본 16"과 동일 균일값으로 채움.
- **장치 보상**: Devices.cs 12종의 `unlockAch`를 구 `lic_*` 면허 id에서 웹 ACH_DEVICE_REWARD
  매핑(jackpot1→dev_subreel 등)으로 직접 교체, `AchievementEngine.Evaluate`는 lic_ 접두 특례
  없이 범용화(unlockAch==달성id 매칭). 심화 9건은 대응 장치가 없어 데이터/주석만 보존(P7).
  `dev_syllabus`/`dev_holdfile`/`dev_retake`/`dev_major` 4종은 범위 밖이라 구 id 유지(§1-B).
- **파생키 축소**: `ComposeStat`에서 `lic_dev_*`·`bldCat_*`/`bldTotal`/`bldAllBasic`/`bldAllMaster`·
  `accountLevel`(소비처 없음) 제거, `distinctCharS10`만 유지(Characters.cs "prodigy" 참조 중).
  StatTracker 원시 카운터 수집은 전부 유지. `Formulas.AccountExp`/`AccountLevel`은 그대로 살아있음
  (퍽 게이트 폐기는 다음 슬라이스).
- **새 카운터 2종**: `graduations`(웹 game.js:1401 stage===15 클리어=졸업 그대로, grad1용) ·
  `playerLevel`(웹 game.js:2578 XP 부여 직전 1런 지연 스냅샷, lv20/lv40용 — StatTracker.
  ApplyGameOverTracking이 PlayerLevelTracker보다 먼저 실행되는 호출 순서로 지연이 자연히 재현됨).
- **XP 재시딩 마이그레이션(§2-(L))**: `PlayerXpReseed34` 플래그 — 재산출값이 더 작을 때만 1회
  덮어씀. 구현 중 `ProfileStore.Load()`의 "파일없음→new PlayerProfile() 직행" 경로가 신규
  플레이어의 첫 런 XP를 다음 로드에서 잘못 깎을 수 있는 엣지케이스를 발견해 `FromDto(빈 DTO)`
  경유로 수정(Runs=0 상태에서 플래그를 미리 안전하게 true 확정).
- **테스트**: `Tests_Ach.cs` 전면 재작성(개수/중복/카탈로그 교차/deep플래그/장치매핑 무결성),
  `Tests_S5.cs`(업적 트리거·장치보상·계정레벨 연동·ComposeStat 제거 회귀), `Tests_PlayerLevel.cs`
  (재시딩 마이그레이션 4종 + playerLevel 1런지연 신규), `Tests_Fx.cs`(장치 unlockAch 골든값) 갱신.
- **Opus 검수 반영**(34종 전사·매핑·카운터 연결 전수 대조 오류 0): ProfileStore catch 폴백 통일
  (JSON 손상 시 XP 오삭감 방지), 34행 골든 테이블 어서션(전사 회귀망), 장치 4종
  (`dev_syllabus`/`holdfile`/`retake`/`major`) **드랍 전용화**(unlockAch="" — 웹에 없는 Unity 전용,
  검수가 잡은 devicesOwned 집계 누락 버그 동반 수정), 업적 텍스트 astral 새니타이저
  (`Core/TextSanitize`), SetStat 헬퍼·스테일 주석 정리. 재시딩은 블랭킷 유지 결정(미출시 —
  §2-(L-1)), 업적 분모 34 유지(deep/asc는 P6/P7에서 도달 가능해짐).
- 검증: EngineTests **18,315 통과**(골든 테이블 +194). Unity 배치 컴파일 0에러.
  P3 로드맵 "진행 중(2/4)".

### Opus 2차 검수 반영 (같은 날 후속)

- **[필수]** `ProfileStore.Load()` catch 폴백도 `FromDto(빈 DTO)` 경유로 통일(나머지 3경로와 일관 —
  JSON 손상 시에도 §2-(L) 오삭감 위험 차단).
- **[필수]** `Tests_Ach.cs`에 웹 `data.js` 손전사 34행 골든 테이블 추가(id/name/desc/key/th/deep
  전항목 양방향 대조 — 전사 슬라이스 핵심 회귀망, `Achievements.All` id 집합과 완전 일치 검증).
- **[Fable 결정]** 블랭킷 재시딩 현행 유지(앱 미출시, §2-(L) 문언 그대로) — 단 `ProfileDto.cs` 주석을
  "482종 세이브만 선별 정정"이 아니라 "이력 기반 재산출로 통일(적립식이 시딩식보다 항상 커서
  실질적으로 항상 발동)"으로 실동작대로 정정. `WEB_PARITY_DESIGN.md` §2-(L-1)에 결정 기록.
- **[Fable 결정]** `dev_syllabus`/`dev_holdfile`/`dev_retake`/`dev_major` 4종을 **드랍 전용 장치로
  확정**(`unlockAch=""`) — 업적 해금 없음, 런 중 장치 드랍(P1)으로만 영구 획득. `ComputeDevicesOwned`의
  순서 버그(드랍 보유분 누락)도 함께 수정. `DexView` 잠금 힌트를 "런 중 장치 드랍으로 획득"으로
  고정 안내(`PickView`는 이 4종의 catalog pick이 원래 null이라 카드 자체가 안 뜸 — 확인만).
- **[저]** `Core/TextSanitize.StripAstral` 신설 — GameOverPanel/DexView(카드+상세팝업)의 업적/카탈로그
  name·desc를 Text에 넣기 직전 astral 문자만 제거(ReelView.TagTranslate 선례를 문장 중간 이모지까지
  일반화). 데이터(Achievements.cs/catalog.json)는 웹 원문 그대로 유지.
- **[저]** `PlayerProfile.SetStat(key,value)` 신설 — `StatTracker`가 원재료 Dictionary에 인덱서로
  직접 쓰던 지점(playerLevel 스냅샷)을 Inc/SetMax와 나란한 공개 계약으로 승격.
- **[저]** `PlayerProfile.cs`/`ProfileDto.cs`/`GameSession.cs`/`StatTracker.cs`/`MenuView.cs`의
  스테일 주석(삭제된 lic_* 파생키 메커니즘을 현재형으로 서술하던 곳들, 하드코딩된 "n/482") 정정.
- 검증: EngineTests **18,315 통과**(0 실패) — 골든 테이블·드랍전용 장치 회귀 2종 추가로 +194.

## 2026-08-07 - 웹 파리티 P3-1: 플레이어 XP/레벨 코어

- 웹 공식 이식(`game.js:107-120, 2617-2625`): `PlayerXpReq=120+(lvl-1)×60`(순차 차감 누적,
  캡 100), 런XP `40+min(20,stage)×12+floor(score/250)+런보스×20+신규업적×25`(자발 포기에도 부여),
  이력 시딩 `runs×30+totalScore/300+bossClears×15+bestStage×8`(웹과 동일 2중 가드·1회성),
  로드 시 레벨 재산출. 신규 `Engine/Profile/PlayerLevelTracker.cs` + `RunState.RunBossClears`
  (런XP는 런 단위 보스 수 — 통산과 구분), FailureOutcome에 xpGain/levelBefore/After(표시는 P4).
- 파이프라인: Fable 설계 → Sonnet 구현 → Opus 검수(공식·가드·순서 문자 단위 대조, 어긋남 0.
  실측으로 **업적 482종發 XP 인플레이션** 경고 → 설계 §2-(L) 결정: 다음 슬라이스 = 업적 34종
  교체 + XP 재시딩 마이그레이션) → Fable 최종. AccountExp(졸업레벨)·레벨 보상·게이트는 무접촉
  (후속 슬라이스).
- 검증: EngineTests 18,178 통과(+76 — 공식 손계산·DTO 왕복·시딩 4시나리오·자동플레이 런보스
  교차검증). P3 로드맵 "진행 중(1/4)".

## 2026-08-07 - 웹 파리티 P2 완료: 점수·배율캡·보스 웹화

- **클리어 점수 웹 공식 채택**(`game.js:1412-1419`): `stage×50 + leftover×2 + leftSpins×100 +
  boss500 + streak` — 등급 보너스(50~1000)·closeBonus(150/300)·막판 보너스(200)·저주 배수 전부 제거.
  등급은 **연출 전용 6단계**로 전환(웹 `ui.js` clearGrade 임계 20/50/100/200%·exact 선처리·보스 +1
  승급·한글 라벨 1:1 + `gradeTier` 정수 필드). 클리어 코인은 웹이 **가산식**(`5 + 보스12 + 보너스`)
  임을 확인해 보스 클리어 12→17로 수정, **빚문서는 점수만 0이고 코인은 지급**(웹 대조로 잡은 2건).
- **총배율 캡 제거**: 웹 일반 모드에 캡 없음을 grep으로 확정(`capMul` 0건, `MAX_SPIN_EXP_MUL`은
  심화 specialMul 전용) → `Formulas.CapMulFor`·Evaluate capBase/expNoCenter·`lastSpinExpMul` 상한
  5.0 삭제. Evaluate/ApplyBoss 시그니처 정리(호출부 8곳 전수 갱신).
- **보스 정리**: grad(졸업심사) pace 룰 제거 — 웹은 EXP 룰 없음, quotaMul 1.15만. finals 분기 순서
  웹 정렬. finals/strict/luck 수치·정수처리 일치 확인(음수 방어로 C# 정수나눗셈 == JS floor 증명).
- 파이프라인: Fable 설계 → Sonnet 구현 → Opus 검수(웹 인용 라인 전수 실물 확인, 인용 오류 0 —
  필수 2건: 빚문서 코인 분리·등급 회귀 보강) → Sonnet 반영 → Fable 최종.
- 검증: EngineTests 전체 통과(HEAD 기준선 교차 실행으로 의도 외 골든 파손 0, 기대값 전부
  웹 공식 수기 재산출 — 순환 검증 0). `WEB_PARITY_DESIGN.md` §2-B (I)(J)(K) 근거 라인 추가, P2 완료.

## 2026-08-07 - 웹 파리티 전환 착수 + P1 완료 (Unity ← `public/play/`)

- 지시 "유니티 게임을 웹페이지에 있는 게임처럼 다 뜯어고쳐줘" — 양측 전수 인벤토리 조사(웹 20개
  시스템/Unity 현황·격차) 후 **[Docs/WEB_PARITY_DESIGN.md](Docs/WEB_PARITY_DESIGN.md) 마스터 플랜**
  작성: 정답지를 kotlin-reference → **웹 단독판(`public/play/`)으로 전환**(CLAUDE.md 반영),
  규칙 충돌 결정 로그(A~H), 페이즈 로드맵 P1~P7(P7 심화모드가 최대 규모).
- **P1 룰 파리티 완료** (Fable 설계 → Sonnet 구현 → Opus 검수 → Sonnet 반영 → Fable 최종):
  ① 특수스핀 **첫 사용 무료**(런당 종류별 1회, 발동 성공 시만 소진 — `RunState.CmdFreeUsed`,
  모드 버튼 "무료" 라벨) ② **첫 판 즉시 시작**(runs==0 → novice+basic 직행 + 안내 토스트)
  ③ 실패 체인 웹 순서(보험+2스핀 → POST_SPIN → fate_bell≤15 → 게임오버) ④ REST 12·CURSE 30·
  EVENT-6 장치 획득(rare 가중 `min(0.6,0.15+stage×0.03)` 이식)·보스 클리어 **DEVICE 노드**
  (장착 or 코인15, 어느 쪽이든 영구 보유 — RunEvent→StatTracker→Profile 경로) ⑤ 자발적 **포기**
  (Spin/PostSpin 즉시 결산, voluntary 플래그·확인 시트).
- Opus 검수 반영: **[치명] dev_bell POST_SPIN 데드엔드**(버튼이 거부 토스트로 빠지고 fate_bell
  회생까지 차단하던 신규 회귀) → 웹 `emergencyBell()` 동일 즉시 클리어+장치 파괴로 해소.
  영구 보유 왕복 테스트 신설, GameSession 장치 시드를 UnlockedDevices(2원 판정)로, 순환 검증
  제거, 포기 버튼 페이즈 게이팅. 결정 로그 F/G/H 추가(웹 pickDevices의 owned 인자 버그
  미재현 — 미보유 필터 채택 등).
- 검증: EngineTests **18,094 통과(+262, 기준선 17,832)** — HEAD 테스트 × 신엔진 교차 실행으로
  "의도된 의미 변경 67건 외 골든(시드 유래 fx/점수/EXP) 파손 0" 확인(순환 검증 아님 증명).
  Unity 배치(`BuildAllUnattended`)로 임포트·컴파일·씬 리빌드.

## 2026-08-07 - 잭팟런 소개·설명 PDF 제작 (`Docs/잭팟런_소개서.pdf`)

- 요청 "잭팟런 소개 및 설명 pdf가 필요해" — A4 6페이지 소개서 신규 제작.
  구성: 표지 → 게임 개요(한 판 흐름·요구 EXP 곡선·실패 구제 체인) → 심볼&스핀(심볼 14종 표·
  세트 보너스·특수 스핀 4종·보스 4종) → 빌드&성장(3스텝·콘텐츠 규모·메타 성장) →
  게임 모드&랭킹(일반·심화·승천 A1~A10·랭킹 3보드) → 플랫폼&개발 현황(웹/Unity·파리티 P1~P7).
- 수치는 `public/play/data.js`(웹 단독판 정답지)·`Docs/EngineSpec/01_engine.md`·
  `Docs/WEB_PARITY_DESIGN.md` 실측 기준. 콘텐츠 카운트는 웹판(캐릭19·머신19·장치24·증강89·
  유물73·아이템78·저주16·테마빌드25·업적34·심볼72).
- 제작 방식: 다크 카지노 테마 HTML(번들 Pretendard 폰트 + `public/jackpotdex/img` 스프라이트
  13장 file:// 참조) → Edge 헤드리스 `--print-to-pdf`. 스크린샷 검수로 6페이지 넘침 없음 확인.
  원본 HTML은 세션 스크래치패드(임시) — 재생성 필요 시 이 항목 참조.

## 2026-08-07 - 웹게임 스핀을 노치 스크롤 릴로 교체 (`public/play/`)

- 요청 "잭팟픽에 구현했던 슬롯 형태로 실제 슬롯 돌리는 것처럼" — `/play/` 게임의 스핀 연출을
  "제자리 이모지 셔플(70ms 인터벌)"에서 **Unity ReelView(S13/S14)식 노치 스크롤 릴**로 교체.
  파이프라인: Fable 설계 → Sonnet 구현 → Opus 검수 → Sonnet 반영 → Fable 최종 검수(실측).
- `ui.js`/`style.css`만 수정(엔진·게임 로직 불변): `.reel`(aspect 2/3) > `.rstrip`(5슬롯, 기본
  `translateY(-35%)` = JS BASE -1.75S와 일치, Opus 수기 검증 오차 0) > `.rslot`(이웃 dim .38/.85).
  `g.spin` 결과 선확정 → 릴별 가속(0.22s, 노치 0.14→0.055)+유지(0.28+i×0.09 스태거)→감속
  3노치(D-3 타깃 주입)→슬롯비례 오버슈트 착지. blastBomb/glowMatches/잭팟 후속 흐름 원문 보존.
- Opus 검수 반영 9건: Promise.all 예외 가드(연출 예외에도 결과 강제 반영 — 런 유실 방지),
  매치/폭발 `.sym` 선택자를 중앙 슬롯으로 스코프 축소, `.rfade` 상하단 페이드 마스크,
  `max-height:560px` 릴 72px 캡, 착지 후 인라인 transform 정리(리사이즈 어긋남 방지),
  reduced-motion settle 해제, 이웃 장식 stripMemo 캐시(재렌더 깜빡임 제거), 오버슈트 비례화,
  이웃 drop-shadow 제거(컴포지터 필터 30→6).
- 검증: 하네스 `ok:true` ×2회 · 헤드리스 실플레이 390×844/740×360/라이브 3회 — 스트립 구조·
  스핀 중 실이동·중앙 착지·EXP 반영·연속 스핀·폭탄 케이스(💣→▫)·콘솔 에러 0. 배포 완료.

## 2026-08-07 - 단독 웹게임(`public/play/`) 이관 — 모카봇 `web/slot` → 이 저장소

"저장소를 클론하면 잭팟런이 돌아가야 한다"는 요구에 대응. **새로 만들지 않고 이미 존재하던
브라우저 단독판을 옮겼다** — 사본을 늘리지 않기 위해 복사가 아니라 **이동**.

- `public/play/` 신규 — 모카봇 `C:\dev\KakaoOpenChatBot\web\slot` 의 10파일(1.02MB).
  data.js(카탈로그) · engine.js(순수엔진, `SlotV2Engine.kt` 공식 포팅) · game.js(런 상태머신) ·
  ui.js(렌더) · rank.js · auth.js · sound.js · style.css · index.html · _harness.mjs(검증, 배포 제외).
  **이미지 0장**(전부 이모지), 하위폴더 없음, 절대경로 의존 0건 → `/play/` 로 무수정 이식 가능했음.
- Firebase 컷오버: `auth.js`·`rank.js` 의 config 를 `mokabot-8ed4d` → **`jackpotrun-web`**.
  게임 로직은 Firebase 와 무관하게 동작하므로(지연 로드·실패 무시) 랭킹/로그인만 영향.
- `database.rules.json`: 랭킹 3노드 추가 — `slotrank` · `slotrank_asc` · `slotrank_deep`.
  `.indexOn: "score"` 부여(`orderByChild("score")` 쿼리가 인덱스 없이 전량 다운로드하는 문제 예방),
  키 길이 6~64 검증(게스트 cid / 로그인 `u_<uid>` 양쪽 수용).
- `firebase.json`: `/play/**` no-cache 헤더 + `**/_harness*.mjs` hosting 제외.
- **구 주소 리다이렉트**: 모카봇 `web/slot/index.html` 을 리다이렉트 전용 페이지로 교체(게임 9파일 삭제).
  localStorage 는 도메인 단위라 그냥 보내면 진행도가 끊기므로, **저장키 7개를 URL fragment(base64url)
  에 실어 넘기고 새 페이지가 1회만 흡수**하도록 했다. fragment 는 서버로 전송되지 않는다.
  수신측은 `public/play/index.html` 의 인라인 스크립트(module 보다 먼저 실행) — 기존 프로필이
  있으면 덮어쓰지 않고, 실패해도 조용히 새 프로필로 시작한다.
- 슬롯 개발 툴체인 경로 갱신(이관하면 깨지는 것들): `workflow/slotdev_rules.md`,
  `.claude/workflows/slot-game-dev.js`(`WEB` 상수), `.claude/agents/slot-dev.md`.
- 검증: `node --check` 7파일 통과 + **공식 하네스 `ok:true errorCount:0`**
  (일반 300/300 · 심화 300/300 · 스트레스 200런 · NaN/음수/무한대 스캔).
- 백업: `C:\dev\KakaoOpenChatBot\backups\web_slot_20260807_backup\` (원본 10파일).
- ⚠️ **배포 순서 고정**: `jackpotrun-web` 먼저 → 모카봇 나중. 뒤집으면 리다이렉트가 없는 주소를 가리킨다.
- **[E드라이브 PC] 1단계 배포 완료**: pull(fast-forward, 충돌 0) 후 `firebase deploy --only hosting,database`.
  라이브 검증 — `/play/` 200 · `_harness.mjs` 배포 제외 확인(404) · 헤드리스 실플레이로
  인트로→게스트→홈→런 시작→첫 스핀까지 EXP/코인/불운게이지 정산 정상, 콘솔 에러 0.
  링크: <https://jackpotrun-web.web.app/play/>. 모카봇 쪽 리다이렉트 배포(2단계)는 모카봇 PC에서.
- 🔴 **수동 필요**: `jackpotrun-web` 콘솔에서 Google provider 사용설정 + 승인 도메인 등록.
  미설정 시 로그인만 실패(게스트 플레이는 정상). 기존 `slotrank` 랭킹 기록은 이설하지 않아 초기화된다.
## 2026-08-07 - 웹 "시작 버튼 무반응" 조사 → 결과 모달 추가 + 전체 배포

- 사용자 보고 "시작 버튼 눌러도 반응이 없다". 헤드리스 브라우저(CDP) 실클릭 재현으로 조사 —
  라이브 데모·로컬 신코드·모바일 뷰포트(390×844) 전부에서 버튼은 정상 동작했고(#msg 표시·뷰포트 내·가림 없음),
  jackpotpick은 7/24 초기 커밋 이후 무변경이라 회귀도 아님. **실체는 UX**: 데모 모드 시작 버튼의 피드백이
  하단 바의 12.5px 안내 한 줄뿐이라 "무반응"으로 느껴졌던 것.
- `app.js`/`pick.css`: `msgOk`/`msgBad` 결과(데모 안내·예약 완료·만료·실패)를 화면 중앙 모달 카드
  (`.msgov`, ✅/⚠️ + 확인 버튼, 탭 닫힘)로도 표시. 파이프라인: Fable 설계 → Sonnet 구현 → Fable 최종 검수
  (검수에서 reduced-motion `animation:none`이 캐스케이드 후순위에 덮이는 설계 실수 1건 수정).
- 조사 중 발견한 미배포 2건 포함 **전체 배포 실행**(`firebase deploy --only hosting,database`, 자격증명은
  8/4 로그인 잔존분): ① 8/7 릴 스핀 연출 ② 8/3 랭킹 페이지+DB 규칙 — **랭킹 규칙이 미배포 상태라
  jackpotrank 읽기/쓰기가 계속 거부되고 있었다**(앱 점수 제출도 그간 실패했을 것). 배포 후 라이브에서
  reel.js/ranking 200, jackpotrank 읽기 200, 데모 흐름·모달·릴 오버레이 실클릭 검증 완료.
- 참고: 데모 모드는 설계상 실제 예약이 되지 않는다 — 실제 시작 예약은 카톡 "잭팟선택" 토큰 링크에서만.

## 2026-08-07 - 웹 잭팟픽: 🎲 랜덤 버튼을 유니티식 릴 스핀 연출로

- 요청 "유니티에 구현한 것처럼 위아래 줄 보여주기식으로 룰렛 돌려서 당첨되는 식으로" — 웹의 유일한 룰렛 성격 기능인 jackpotpick 랜덤 조합 버튼(기존: 즉시 적용)에 Unity `ReelView`(S13/S14) 연출을 이식. 파이프라인: Fable 설계 → Sonnet 구현 → Opus 검수 → Fable 최종 검수.
- `public/jackpotpick/reel.js` 신규: 세로 5칸 스트립(위2/중앙/아래2) 노치 스크롤 릴 3개(캐릭터·머신·장치) 오버레이. Unity 명시값 그대로 — 가속 0.25s(노치 0.16→0.06s), 유지 0.35s, 왼쪽부터 0.10s 스태거 감속(0.10/0.16/0.24s), 타깃은 D-3 공식으로 첫 감속 노치에 주입, +8px 오버슈트 착지, 이웃 칸 상시 노출(알파 .42/스케일 .88), 최고속 블러/확대(.75/1.04). 결과를 먼저 확정하고 연출이 그 결과에 착지하는 결정론적 구조(엔진과 동일 원칙). 탭/Esc 스킵, prefers-reduced-motion 즉시 표시, 이미지 프리로드+이모지 폴백.
- `app.js`: `applyReco` async화 — random 분기에서만 릴 연출 await 후 기존 적용 꼬리 실행(초보/고점/도전 추천은 기존과 동일하게 즉시). 릴 연출 중 재진입 가드.
- `pick.css`: 오버레이 섹션 추가(기존 CSS 변수 사용, 320px 뷰포트 대응).
- Opus 검수 반영 4건: 연출 예외 시 오버레이 영구 잠금 방어(settle 강제 스냅), reduced-motion 표시시간 분리, `loading="lazy"` 제거, 긴 이름 줄바꿈으로 인한 박스 튐 방지. 스태거 양자화·오버슈트 방향 지적은 Unity 원본과 동일 동작이라 기각.
- 검증: 노치 재활용·타깃 주입 산수 수기 전개(Opus)로 "마지막 감속 노치 종료 시 중앙=타깃" 확인, `node --check`(ES 모듈) 통과. 미배포 — `firebase deploy --only hosting --project jackpotrun-web` 은 사용자 확인 후.

## 2026-08-04 - 파티클 근본 수정 3건 + S16 스핀 결과 패널(GainPanel)

- **사용자 보고 "터지는 이펙트는 어디 갔나 · 그냥 텍스처 덩어리만 보인다"** — 파티클 미구현이 아니라 `FxPrefabGen`이 굽는 에셋 쪽 버그 3건이었다. 커밋 `527bfe8`.
  1. **텍스처가 머티리얼에 안 붙음**: `GenerateAll`이 `StartAssetEditing` 배치 안에서 PNG를 쓰고 곧바로 `LoadAssetAtPath`로 읽어 항상 null → `fx_add/fx_alpha`의 `_MainTex`가 빈 채로 저장. 텍스처 생성을 배치 밖으로 빼고 `Refresh` 후 재로드.
  2. **파생 머티리얼 참조 끊김**: `CloneWithTexture`의 메모리 상 `Material`은 `SaveAsPrefabAsset`에 저장되지 않아 렌더러 머티리얼이 None → 유니티 기본 파티클 머티리얼(텍스처 없는 흰 쿼드)로 대체됐다. `Art/FX/mats/*.mat` 에셋으로 저장(재실행 시 제자리 갱신 → GUID·프리팹 참조 보존).
  3. **단위 불일치 = "덩어리"의 정체**: `scalingMode=Local`은 부모 스케일을 무시해 설계의 캔버스px 수치를 월드 단위로 해석 — Screen Space-Camera 캔버스 `lossyScale`이 약 0.005라 크기 8이 **화면 두 배 크기의 흰 사각형**이 됐다 → `Hierarchy`. 같은 이유로 `gravityModifier`(월드 가속)는 입자를 0.25초에 화면 밖 수천 px로 날려 긴 줄무늬만 남겼다 → `forceOverLifetime`(Local space) 기반 `SetGravityPx`로 교체. 트레일 `minVertexDistance` 4→0.02.
  - 검증: 프리팹 39개 렌더러 전부 텍스처 있는 머티리얼 + Hierarchy, 플레이 모드 `Simulate(0.25s)` 후 카메라 렌더 캡처로 실제 입자 형태(컨페티·코인·파편·스파클) 육안 확인.
- **S16 스핀 결과 패널** (설계 ENGINE_PORT_DESIGN.md S16, 사용자 피드백 "로그 나열 말고 얼마나·왜 얻었는지"). 파이프라인: Fable 설계 → Sonnet 구현 → Fable 최종 검수. 커밋 `8486911`.
  - `UI2/Run/GainPanel.cs` 신규: 획득 대문짝(+N EXP, 0.35s 카운트업 + OutBack 팝인) · 점수/코인 칩(0이면 숨김) · 기여 내역 최대 6줄(0.05s 스태거) · 세트 설명 박스. **표시 전용 분해** — 심볼 기본/세트/해골/배율/가산으로 나누고 남는 차이는 `기타` 줄로 드러낸다(오차 은폐 금지), 최종 수치는 항상 엔진 값 그대로.
  - `RunView`: `resultLineText`/`ScorePopupRoutine` 제거 → `gainPanel.Show/Clear`. `NotesFeed`: 3줄로 축소 + astral 이모지 한글 치환(레거시 Text가 서로게이트 쌍을 못 그려 `🔥 EXP +50%`가 앞이 빈 채로 보이던 문제).
  - **Fable 최종 검수에서 잡은 것 2건**: ① GainPanel에 flex를 준 첫 구현이 잔여 공간을 통째로 먹어 세트 박스와 로그 사이에 화면 중앙 검은 구멍이 생김 → 잔여를 릴 섹션(flex 1)과 조작부 위 스페이서(flex 1)로 반씩 분배. ② `fx_exp_gain`이 `ZeroVelocityXZ`를 잘못 호출해 방금 넣은 x 속도를 도로 지우고 y만 다른 모드로 남겨 "Particle Velocity curves must all be in the same mode" 경고가 상시 발생 → `ZeroVelocityYZ` 추가, 콘솔 경고 0.
  - 검증: 컴파일 0오류 · 씬 재빌드 · 플레이 모드 실제 스핀에서 `+12 EXP = 체리×2 +6 · 보석×1 +1 · 세트 2연속 +8 · 해골×1 -3` 합 일치, 로그 3줄 한글 정상 표기.

## 2026-08-03 - S16 증강 오퍼 티어 폴백 + 폭탄 폭발 연출

- **버그(사용자 보고 "증강 골랐는데 그냥 지나감") 원인 확정**: BASE 퍼크 22종 전부 SILVER + 클리어 스테이지 %3==0 → GOLD 강제 → 신규 프로필의 게이트 풀에 GOLD 0개 → `PickPerksByTier` 오퍼가 통째로 비어 증강/유물 노드가 **랜덤 EVENT로 조용히 대체**(Kotlin 원본 동일 로직 — 봇은 계정이 장기 성장해 미노출, 단독 앱은 상시 재현).
- **수정 [원본 이탈 — Fable 승인]**: `Shop.PickPerksByTier` 티어 풀 소진 시 잔여 후보 전체(any tier) 폴백 1줄(PickAugments 기존 패턴과 동일). 정상 경로 RNG·결과 완전 불변(Opus 전수 확인). 전 풀 소진 → EVENT 경로는 보존. 회귀 테스트 3건 추가(`Tests_S4_TierPoolFallback` — GOLD 강제 시 PERK_OFFER 보장 ×증강/유물, 전 풀 소진 시 기존 EVENT 폴백 보존).
- **폭탄 연출(사용자 요청)**: `SpinResult.rawCells`(Evaluate 입력 스냅샷, [표시 전용 — 밸런스 무관]) 추가 → ReelView가 **원시 심볼로 착지 → 0.25s 대기 → 폭탄 인접 칸 FxId.Skull 주황 버스트 + 1→1.18→0 InBack 펀치 + 폭탄 칸 0.94→1 펀치 + 셰이크(±6px) → 빈칸 전환**(칸별 0.05s 스태거). 자석 복사 칸은 0.12s 팝 스왑. 변형 없는 스핀은 추가 지연 0. `UiTween.Ease.InBack` 신규(OutBack 대칭).
- **Opus 검수**: 치명 0 · 중요 1(변형 목록 주석/설계 부정확 — Evaluate 내부 변형은 💥·🧲 2종뿐, 👑/🧽/🌀/🌱→는 이전 단계에서 raw에 반영. 문서·주석 정정) · 경미 5(폭탄 칸 펀치 시작 스케일 명시 대입 반영, 테스트 `?.tier` 방어 반영, PRISM 전량 보유 케이스 폴백 문서화, 셰이크 타이밍은 유지 판단).
- **검증**: EngineTests **17,832 통과(+13, 골든값 무수정)** · csc 0오류 · 에디터 플레이 주입 재생으로 폭탄 연출 확인.
- 특이사항: 검증 중 에디터가 플레이 진입 도메인 리로드에서 35분 데드락(`Begin MonoManager ReloadAssembly`에서 정지) — 강제 종료 후 재시작으로 복구. 씬·스프라이트 meta 등 디스크 저장분 유실 없음.

## 2026-08-03 - S15 글로벌 랭킹 (Firebase RTDB, 앱+웹)

- **Firebase 확인**: 콘솔 표시명이 "JackpotRun"으로 바뀌었으나 **프로젝트 ID는 `jackpotrun-web` 불변**(표시명 변경은 ID/URL/키에 무영향 — RTDB·호스팅 실측, `jackpotrun` ID는 전 리전 404). 코드는 기존 URL 유지, 프로젝트 이전 시 `RankingService.DbUrl`·웹 config 두 곳만 교체.
- **파이프라인**: Fable 설계(ENGINE_PORT_DESIGN.md S15) → Sonnet 2병렬(Unity/웹) → Opus 검수 → Fable 반영. Opus 검수: 치명 1(행 자식 경로 계약 — HGroup이 중간 노드라 `row.Find("RankNo")` 전부 null → `"Content/…"` 계약으로 통일, Dex 선례) · 중요 2(씬 리빌드 필요, astral 이모지 미렌더 → 1~3위 금은동 **색상 숫자**로 대체) · 경미 8(MiniJson `catch(Exception)` 확대, `CompleteLogin`에도 제출 훅(닉 변경 즉시 반영), 주석 정리 등 반영).
- **Unity 앱**: `Game/MiniJson.cs`(순수 C# JSON 파서) · `Game/RankingService.cs`(RTDB REST — `jackpotrank/$pid` PUT/GET, PlayerPrefs pid GUID·제출 캐시로 중복 PUT 방지, 실패 시 다음 Intro 진입에서 자동 재시도) · `UI2/RankView.cs`(상위 100, 내 행 CardTop+골드 닉 강조) · ScreenRouter `Rank` + AppRoot `ShowRank`/RegisterIntro 제출 훅 + MenuView 랭킹 버튼 실연결(토스트 제거) + UiSceneBuilder `BuildRankScreen` + **Intro 씬 리빌드 완료**.
- **웹**: `public/ranking/`(index/app/style — 동일 정렬 score↓·ts↑, `_hesc` XSS 이스케이프, 상위 100, 날짜 표기) · `firebase.json` `/ranking/**` no-cache · `database.rules.json` `jackpotrank`(read 공개, `$pid` 검증 쓰기, indexOn score, `$other` 차단).
- **검증**: csc 오프라인 컴파일 0오류 · 에디터 컴파일/씬 리빌드 0오류 · 플레이 스모크 실클릭 경로(타이틀→메뉴→랭킹→백버튼) — 오류 상태 문구(규칙 미배포라 정상) 및 **성공 경로**(가짜 5건 주입: 행 렌더·점수 표기 `52,340 · S15`·내 행 강조) 스크린샷 확인, 콘솔 오류·경고 0. 참고: 비포커스 화면 겹침은 기지의 runInBackground=false 현상(코드 버그 아님).
- ⏳ **남은 것(사용자 실행 필요)**: `firebase deploy --only hosting,database --project jackpotrun-web` — 이 PC엔 firebase CLI 없음. **규칙 배포 전에는 앱·웹 랭킹이 "불러오지 못했어요" 문구를 띄우는 게 정상**(RTDB 기본 거부).

## 2026-07-31 - MCP 인터랙티브 실플레이 검증 통과 + 저장소 문서 정리

- **MCP 연결**: stdio 브리지가 자동 시작되지 않던 원인(AutoStartOnLoad 기본 off) 해결 — `Editor/McpBridgeAutoStart.cs` 추가(에디터 열면 자동 연결). 
- **실플레이 검증 (전부 실제 버튼 클릭 경로)**: 메뉴 → 조합 선택(카드 클릭·자동 탭 진행) → 시작 예약 → **스테이지 1~7 풀런**(보스 5층 클리어, 유물/증강 픽, 노드 선택) → 게임오버(점수 3,906) → 메뉴 복귀. 콘솔 오류·경고 **0건**.
- 검증된 것: 최종 점수 공식(3,906×novice 0.9=3,515 정확), 신규 프로필의 BASE 퍼크 게이트 폴백, **업적 23종 실시간 달성**, 프로필 로컬 저장(`persistentDataPath/jackpotrun_profile.json` 스탯 키 실기록), 메뉴 프로필 요약 갱신("최고점수 3,515 · 런 1회 · 업적 23/482"), 게임오버 화면 렌더링(한글 폰트·업적 목록·HUD) 스크린샷 확인.
- 참고: 에디터 비포커스 시 지연 Destroy 미처리 현상은 runInBackground=false 기본값 때문(코드 버그 아님 — 검증 중 runtime 플래그로 확인).
- **저장소 문서 정리(사용자 지시)**: README.md·CLAUDE.md·kotlin-reference/README.md에서 특정 플랫폼(챗봇) 유래 서술 제거 — **웹 & 모바일 앱 게임**으로서 게임 기능 중심으로 재작성. kotlin-reference는 "구버전(v2) 엔진 스냅샷 — 밸런스 정답지"로 재정의.

## 2026-07-31 - 엔진 이식 완결 (S5b 프로필·트래킹 + S6 게임 화면) — 테스트 17,819개

- **S4 회귀 보강**: +6,200 어서션 — INSTANT 아이템 15종 상태변화 실측, 확률표(티어/10%등급업/12%프리즘 — 비순환 층화 설계), 리치 자동플레이(전 액션 실행 확인). 신규 버그 0.
- **S5b (Sonnet → Opus 검수 → 반영)**: `Engine/Profile/` PlayerProfile(스탯 156키 단일 Dictionary) · StatTracker(Kotlin track/bumpAch 19개 호출 지점 전수 이식 — Opus 대조 18/19 정확, 결손 1건 즉시 수정) · AchievementEngine(composeStat 파생키·THEME_BUILDS 25종·lic→장치 해금) · ProfileDto + `Game/ProfileStore.cs`(JsonUtility·원자적 저장). seen_* 그랜드파더 게이트 이식(RISK 노드 미기록 기벽 보존).
- **Opus S5b 검수 반영**: H1 dev_pin 스크래치(업적 3종 봉인 해제) · M1 파생키 게이트(prodigy) · M2 즉시클리어 itemsUsed 원본 동작 · M3 세이브 원자성(File.Replace) · M4 devicesOwned 지연 · L1~L4. bld_* 25종 직접 검증 등 +88 어서션.
- **S6 게임 화면**: `UI/RunScreen.cs`·`RunPanels.cs`(HUD·릴·노트 피드·특수모드 4종·가방·장치 버튼·MANIP 칸선택 팝업, Phase 패널 5종 — 노드3택/퍼크오퍼(보류·재추첨·시너지 배지)/상점/만회/게임오버) + `Game/GameSession.cs`(프로필→런→트래킹→업적판정→저장 수명주기). PickScreen "시작 예약" → 실제 런 시작 연결, 메인 메뉴에 프로필 요약.
- 최종: **dotnet 17,819 테스트 통과 + 전 스크립트 csc 컴파일 0오류 + Unity 에디터 실컴파일 성공(DLL에 GameSession/RunController/StatTracker 포함 확인) + 플레이모드 스모크 예외 0**. 남은 항목: 에디터에서 런 화면 인터랙티브 검증(MCP 세션 필요 — 스핀→상점→게임오버 클릭 플로우), PickScreen 해금 표시의 프로필 연동(현재 데모 데이터), 표시모드 명령(S6 후속), Firebase 연동, 앱 아이콘/스플래시/실기기 빌드.

## 2026-07-31 - 엔진 C# 이식 3단계 (S4 상점·노드·아이템·장치·RunController) — 테스트 11,504개

- **S4 (Sonnet → Opus 검수 → Fable 반영)**: `Engine/Run/` Shop(perkGate→gatedPool→pickPerksByTier→offerPerks 확률 파이프라인, 5% 세트시너지 주입 포함 — RNG 소비 순서 call-for-call 일치) · NodeEvents(노드 8종+EVENT 10종 랜덤표) · ItemUse(INSTANT 23케이스+즉시클리어 캡+retake_form) · DeviceActions(MANIP 9단계 net-adjust·보조슬롯 0.6약화·gambler 무료재굴림) · RunController(typed action façade, §7 명령 전수 매핑).
- **S3 회귀망 보강**: Tests_RunNet.cs +256 어서션(조건부 증강 경계·Evaluate 세부·보스 4종 절삭·거부 경로 전수).
- **Opus S4 검수**: 치명 0 · 중요 2 · 경미 6. 확률 실측 전부 이론값 일치(티어표·10%등급업·5%주입 4.55~4.98%·EVENT 12%프리즘), MANIP 계약·노드/이벤트 전수 일치, RunState/StageFlow 수정분 git diff로 로직 변화 0 확인.
- 반영(중요2+경미5): broken_prism 누적→덮어쓰기(Kotlin CSV 대입 의미) · Retake 풀소진 시 코인/마커 롤백 · 보조슬롯 ARMED/PEEK 검증 · HandleContinue mods 원본 생략 보존 · PERK_OFFER에 보류포함 플래그 · UI 계약 주석 2건(STAGE_CLEARED result null·stat 참조 계약). 표시모드 전환 명령은 S6 UI 소관으로 이관.
- 전 스위트 **11,504 통과**. 100시드 풀런(상점·노드 포함) 예외 0, 평균 도달 S4.77.
- 진행 중: S4 테스트 결손 보강(INSTANT 효과·Retake/Hold·시뮬 액션 커버), S5b(프로필·스탯 트래킹·저장 어댑터).

## 2026-07-30 - 엔진 C# 이식 2단계 (S3 런·스핀 + S5a 업적482 + 회귀망) — 테스트 2,025개

- **S3 (Sonnet → Opus 검수 → Fable 반영)**: `Engine/Run/` — RunState(SlotV2RunRow 이식, 카톡 전용 필드 제외 목록 주석) · Mods(fx 범용 해석기 + 캐릭터/조건부11종 id별 case) · SpinResolver(스핀 26단계, capMul 이중 클램프, 정수 절삭 위치 보존) · StageFlow(클리어 보상/실패 4단계 체인/3노드 롤).
- **Opus 검수 결과**: 치명 0. fx 해석기 **223케이스 기계 대조 완전 일치**(연산 종류·기본값·적용 순서), 스핀 26단계·ClearStage·실패체인 원본 일치, ctx 조건부 11종 하한·경계 실측 통과, 값심볼 동률 결정론화는 JVM HashMap 순서 분석 결과 **사실상 원본 동작 보존** 판정. 100시드 시뮬레이션 평균 도달 S3.85(이론 기대 S3.6 대역 내).
- 반영(중요2·경미6 중 5): dev_coin 배수 단일소스화(Devices.fx) · 불운게이지/최고 한 방 노트 2종 추가 · ProcessSpin Phase 가드 · Rejected 계약 주석 · 죽은 필드 주석 정정. fx 미지 키 fail-fast 정책은 유지(콘텐츠 오타 조기 검출 — Tests_Fx가 전수 커버).
- **S5a**: `Content/Achievements.cs` — 업적 482종(기본16+확장466) 파서 자동 전사, 카테고리 30종·티어 분포 스펙 100% 일치, 스탯 키 156종 사전 이탈 0, 면허 lic_12/dm_24 매핑 테스트로 검증.
- **fx 회귀망**: `Tests_Fx.cs` — 퍼크 157 fx·메타 스냅샷(FNV-1a), 아이템 73·장치 16·세트 33 명시 대조, 캐릭터 16·연구 10은 Kotlin 직접 대조 상수. 어서션 +934.
- 총 **2,025 테스트 통과** (dotnet, 에디터 불필요). 남은 슬라이스: S4(상점·장치액션·RunController)·S5b(프로필/저장)·S6(게임 UI). S3 로직 회귀망 보강(Opus 중요-1)은 별도 진행.

## 2026-07-30 - 엔진 C# 이식 1단계 (S1 코어 + S2 콘텐츠) — Opus 전수 대조 통과

파이프라인: Fable 설계(`Docs/ENGINE_PORT_DESIGN.md`) → 사양 추출 3부(`Docs/EngineSpec/`) → Sonnet 3병렬(S1 코어공식·테스트 하네스 / S2a 퍼크 157 / S2b 아이템·장치·세트·연구) → Opus 전수 기계 대조 → Fable 반영.

- **순수 C# 엔진** `Client/Jackpot/Assets/JackpotRun/Scripts/Engine/` (UnityEngine 비의존) + `Tools/EngineTests`(dotnet net8.0) 골든 테스트 **798개 통과**.
- 이식 완료: 밸런스 상수 27 · quota/stageClearScore/티어/계정EXP 공식 · 심볼 14 · 머신 16(가중치표) · 캐릭터 16 · 보스 4 · 증강 80 · 유물 61 · 저주 16 · 아이템 73 · 장치 16 · 세트 33(캐릭/머신/장치 게이트 14종 포함) · school 10 + 게이트 오버라이드 45 + BASE_PERK 22.
- **Opus 검수: 리터럴 400건+ 전수 대조 수치 불일치 0건**, 골든값 독립 재산출 확인(순환검증 아님), Rng 의미론(빈 컬렉션 미소비·셔플 방향·복원추출) 일치 판정. 원본 버그 4종은 [원본 버그 유지] 주석으로 보존.
- 반영: BASE_PERK_IDS를 `Schools.BasePerkIds`로 공개(단일 정의), 조건부 증강 하한 함정·Rng.Next(0) 의미차를 S3에 전달, S4 백로그(INSTANT_CLEAR_ITEMS·needsArg 등) 설계서 기록. fx 회귀 스냅샷 테스트는 후속 추가 예정.
- 진행 중: S3(런 상태머신·Mods·스핀 파이프라인·스테이지 진행) Sonnet 구현.

## 2026-07-30 - Docs/EngineSpec/02_service.md 작성 (SlotV2Service.kt 사양 추출)

- `kotlin-reference/game/SlotV2Service.kt` 전체(실측 2,591줄 — 기존 WORKLOG 기재 "2,437줄"과 불일치 확인, `wc -l`로 재검증)를 정독하고 C# 이식용 정밀 사양 문서를 신규 작성: `Docs/EngineSpec/02_service.md`. 수치는 원문 그대로(반올림·요약 없음), Kotlin 라인번호 병기.
- 포함 내용: 런 상태 머신(state 전이표 + `SlotV2RunRow` 전 필드 표, `data/SlotV2Entities.kt` 참조), 스핀 처리 26단계 정확 순서(장치/아이템/증강/보스룰 발동 순서·정수절삭 연산 포함 — 밸런스 핵심), 스테이지 진행(실패 체인 4단계·보스 특수룰), 상점(오퍼 6칸 생성규칙·가격·리롤 정액 6코인·판매 기능 없음 확인), 노드/이벤트 시스템(8종 노드 + EVENT 10종 랜덤표 + 티어결정), 코인 경제(획득처/사용처 전액 표), 명령어 목록(스핀 4종+장치 5종+아이템+시스템), 점수/랭킹/기록, 장치 쿨다운/파괴 규칙, C# 이식 주의 12항.
- 특이사항 발견(문서 §11·§12에 정리): `dev_bell` 파괴가 메인 슬롯만 초기화(보조 슬롯 장착 시 결함 가능성), `devCooldown` 필드가 Service.kt 내 set/check 코드 없음(Engine 쪽 확인 필요), `hasPrism` 배율상한 판정이 임시 `phasePerks`(broken_prism 효과)를 무시, `SlotV2RunRow.state` KDoc 주석과 실제 state 불일치(`EVENT_ITEMSHOP`/`EVENT_GAMBLE`/`EVENT_REST`/`EVENT_CURSE` 미실재), `dev_retake` 유물노드 동작-안내 비대칭.
- 이 문서는 §1 런상태만 예외적으로 `data/SlotV2Entities.kt`를 함께 인용(RunRow 필드 선언부가 Service.kt에 없어 불가피).

## 2026-07-30 - kotlin-reference 스냅샷 추가 (잭팟런 v2 원본 로직)

직전 항목의 "Kotlin 게임 로직은 이 저장소에도 없음"을 해소. 사용자 지시로 봇 원본 소스를 스냅샷 반입.

- `kotlin-reference/` 신규 — 봇(`C:\dev\KakaoOpenChatBot`)의 잭팟런 **v2 파일 6개, 525KB**.
  원본과 **SHA256 전량 일치** 확인(잘림·변형 없음).
  - `game/SlotV2Engine.kt`(2,206줄) — 머신별 심볼 확률표, EXP/점수 공식, 스테이지 요구치 곡선, 콤보·세트 규칙, 카탈로그 정의(MACHINES/CHARS/AUGMENTS/RELICS/CURSES/ITEMS/DEVICES)
  - `game/SlotV2Service.kt`(2,437줄) — 런 흐름(스핀→스테이지→상점→보스), 상점가·리롤, 코인 경제, 장치 쿨다운
  - `game/SlotV2AchievementsExt.kt`(631줄) — 업적 조건·보상 정확값
  - `game/SlotV2WebService.kt`(304줄) — RTDB 노드 스키마, 토큰 발급(24-hex UUID·60분 TTL·1인1개)
  - `data/SlotV2Entities.kt`(122줄) · `data/SlotV2Dao.kt`(54줄) — 저장 스키마·랭킹 쿼리
- **v1 슬롯 제외** — 봇에는 슬롯이 2개 병행한다(v1 `슬롯` / v2 `잭팟`). 잭팟런은 v2. 파일명에 `V2` 없으면 잭팟런이 아니다.
- ⚠️ **스냅샷이라 갈라진다.** 봇 본체가 비-git 이어서 서브모듈 불가. 진실의 출처는 항상 `C:\dev\KakaoOpenChatBot`. 단독 빌드 불가(참조 전용). 주의사항은 `kotlin-reference/README.md`.
- 보안 스캔: API키·비밀키·비밀번호·Bearer 토큰 **없음**, 하드코딩된 방 linkId **없음**. URL 2개(`jackpotrun-web.web.app`, RTDB base)는 이미 웹 클라이언트에 공개된 값이라 추가 노출 없음.
- `CLAUDE.md`: "이 저장소에 없는 것" 섹션을 `kotlin-reference/` 안내로 교체. **manifest = 무엇이 있는가 / kotlin-reference = 어떻게 계산되는가** 역할 구분 명시. 디렉터리 트리에 `Client/`·`Docs/`·`kotlin-reference/` 반영. 이미 Public 이 된 사실에 맞춰 RTDB 규칙 경고 문구 갱신.

## 2026-07-30 - GitHub 원격 연결 및 Unity 작업 커밋

- 사용자 지시로 `https://github.com/goodgood16046/JackpotRun.git`(Public) 연결 — 웹 저장소(구 JackpotRunWeb 번들)의 정식 원격. 번들 대비 신규 커밋 2개(원격 문서화) 수신.
- 프로젝트 루트(`e:\UnityProject\JackpotRun`)를 git 저장소로 초기화하고 origin/main 체크아웃 — 웹 파일(public/, unity-assets/ 등)이 루트로 합류, Unity 작업(Client/, Docs/ 등)과 한 저장소가 됨.
- `CLAUDE.md` 병합: 저장소 버전(웹 컨텍스트 + GitHub 워크플로) + 로컬 규칙(모델 역할 파이프라인·작업 로그) + Unity 클라이언트 안내 통합. 기존 로컬본은 `CLAUDE.local.md`(gitignore)로 보존.
- `.gitignore` 확장: Unity Library/Temp 등 + 머신 종속 항목(Tools/unity-mcp, .mcp.json, .claude/) + 구 번들 파일.
- 커밋 `e2b4799`: Unity 클라이언트 671파일 (스프라이트 290 + 메타, 스크립트 13, 설정, 문서). Kotlin 게임 로직은 이 저장소에도 **없음** 재확인(전 커밋 .kt 0개).
- ⚠️ **푸시 보류**: 이 PC에 GitHub 자격증명 없음 — 저장소 규칙대로 **사용자가 터미널에서 `git push` 1회 직접 실행**(브라우저 로그인) 필요. 이후부터는 Claude가 push 가능.

## 2026-07-30 - 앱(Android) 타깃 베이스라인 적용

앱 출시 방침 확정에 따라 파이프라인(Fable 설계 §5.5 → Sonnet 구현 → Opus 검수 → Fable 수정)으로 진행.

- `Assets/JackpotRun/Editor/AndroidAppBaseline.cs`: PlayerSettings 1회 자동 적용 — 회사 Phigolf · 제품 JackpotRun · 패키지 `com.phigolf.jackpotrun` · 세로 고정 · minSdk 24/targetSdk Auto · IL2CPP + ARM64|ARMv7 · 빌드타깃 Android 자동 전환. Opus 지적 반영: 스위치 실패 시 마커 미기록(재시도 가능), 마커 절대경로, IsBuildTargetSupported 사전 확인, bundleVersion은 Unity 기본값일 때만 초기화(클린 클론 버전 되돌림 방지).
- **한글 폰트 번들링**: Pretendard-Regular.otf(OFL, 1.54MB) + 라이선스를 `Resources/JackpotRun/Fonts/`에 추가, `UiFactory.Kor()`가 번들 폰트 우선 로드 — 기기(Android)에 맑은 고딕이 없어 한글이 깨지는 문제 해결. Pretendard에 이모지 글리프가 없어 `fontNames` 폴백 체인(Segoe UI Emoji 등) 설정 — 장기적으로는 이모지의 스프라이트 대체 권장(로드맵).
- 에디터 실적용 확인: baseline applied 로그, **빌드타깃 Windows→Android 전환 완료(64s)**, ProjectSettings 반영·마커 생성·폰트 임포트 확인, CS 오류·예외 0.
- 남은 앱 작업(로드맵): 게임 로직(슬롯 엔진) C# 이식(**Kotlin 원본 필요 — 다른 PC**), Firebase 연동(Unity SDK 또는 REST), 앱 아이콘/스플래시, Safe Area 대응, 이모지 스프라이트화, 실기기 IL2CPP 빌드 테스트, 키스토어/서명, (iOS는 macOS 필요).

## 2026-07-30 - 새벽 자동 이어서 작업 (06:03 예약 세션)

- 컴파일 상태: Editor.log CS 오류 0건, `Assembly-CSharp.dll` 최종빌드(02:27) 이후 변화 없음 — 수정 필요 없었음.
- 스프라이트: 290장 전부 `.meta` 생성·Sprite 타입(textureType 8) 임포트 확인.
- MCP 패키지: `com.coplaydev.unity-mcp` 로컬 참조로 정상 resolve, `MCPForUnity.Editor/Runtime.dll` 컴파일 확인. MCP 도구는 Claude 세션 재시작 후 사용 가능(이번 세션은 미로드) → 플레이모드 검증은 키 입력(Ctrl+P)+Editor.log 방식으로 대체.
- 플레이모드 스모크 테스트: 진입("Reloading assemblies for play mode" 확인) → 실행 → 종료까지 예외·Assertion·LogError **0건**. UI 부트스트랩(카탈로그 로드 실패 시 LogError 발생 설계)이 무오류로 통과.
- 발견 버그 없음. 남은 항목: MCP 연결 후 화면 육안(스크린샷) 검증, PickScreen 섹션 카운트 미포팅(경미-3) 등 어제 목록 유지, Firebase 연동·게임 로직 이식은 별도 설계.

## 2026-07-30 - JackpotRunWeb.bundle → Unity 이식 (1차: 데이터·아트·화면 포팅)

파이프라인: Fable 설계(`Docs/UNITY_PORT_DESIGN.md`) → Sonnet 구현 ×2(데이터/UI 병렬) → Opus 1차 검수(전수 시뮬레이션 대조) → Fable 최종 검수·수정. 검수 결과 치명 0 · 중요 3(전부 반영) · 경미 16(핵심 8건 반영, 나머지는 아래 "남은 항목").

- **에셋 추출**: 번들 클론 → 스프라이트 290장(8카테고리)을 `Client/Jackpot/Assets/JackpotRun/Resources/JackpotRun/Sprites/`로, `manifest.json`(294건 단일 소스)·`manifest.csv`·`prompts.json`을 `Assets/JackpotRun/Editor/SourceData/`로 복사. 이미지 없는 장치 4종(dev_holdfile/major/retake/syllabus)은 이모지 폴백.
- **데이터 변환**: `Client/Jackpot/Tools/convert_manifest.py` — manifest를 JsonUtility-safe `Resources/JackpotRun/catalog.json`으로 변환(null 금지·전 키 존재·unlockReq 튜플→객체). 검증 통과: 294건, 스프라이트 290/290 대조, 실패 시 exit 1.
- **C# 데이터 계층** (`Assets/JackpotRun/Scripts/`): `Data/CatalogModels.cs`(JsonUtility 모델) · `Data/JackpotCatalog.cs`(카탈로그 로더·스프라이트 로딩) · `Data/PickMeta.cs`(meta.js 시너지 엔진 완전 포팅 — PAIRS 21·DEV_FIT 12·evaluate/recommend/unlockOrder, Opus가 3,328조합 전수 대조로 원본 일치 확인) · `Data/DemoData.cs`(데모 해금 상태) · `Core/NumberFormat.cs`(소수 2자리·끝 0 제거 표기 규칙).
- **C# UI 계층** (코드 생성 uGUI, TMP 미사용·맑은 고딕 동적 폰트): `UI/UiFactory.cs` · `UI/JackpotRunApp.cs`(어느 씬에서든 자동 부트스트랩) · `UI/MainMenuScreen.cs` · `UI/PickScreen.cs`(잭팟픽 포팅 — 탭/필터/정렬/추천/시너지 요약, 정렬 2,448상태 전수 일치) · `UI/DexScreen.cs`(도감 카탈로그 브라우저) · `UI/DetailPopup.cs`.
- **에디터 스크립트**: `Editor/JackpotSpriteImporter.cs` — JackpotRun 스프라이트 임포트 설정 강제.
- **최종 검수 반영(중요 3 + 경미 8)**: 요약 패널 높이 430→560(장점/주의 칸 0px 압축 방지) · 태그 칩 글자색 대비 수정 · 잠금 오버레이 α0.86→0.45(잠긴 카드 식별 가능) · Evaluate hasPick 가드 · 머신 점수보정 ×n 표기 추가(도감·팝업, NumberFormat.Fmt 사용) · 팝업 tier 한글화(🥈실버 등) · 선택됨✓ 앵커 수정 · Canvas sortingOrder=100 · 변환도구(결정적 generatedAt·실패 exit code·null 정규식).
- **검증**: csc 스모크 컴파일 통과(런타임+에디터) + Unity 에디터 실컴파일 CS 오류 0건, 스프라이트 290장 Sprite 타입 임포트 확인.
- **MCP 세팅**: PyPI `mcpforunityserver`(v10.1) 설치 → 루트 `.mcp.json`(UnityMCP stdio) 생성, `com.coplaydev.unity-mcp`를 로컬 클론(`Tools/unity-mcp`) 참조로 `Packages/manifest.json`에 추가, 에디터에서 패키지 임포트 확인.
- **남은 항목(06:00 자동 세션 예약됨)**: 플레이모드 실검증(MCP), PickScreen 섹션 카운트/제목 미포팅(경미-3), 팝업 정리 스코프(경미-10), FirstOf 폴백 비결정성(경미-15), RTDB(Firebase) 연동, 실제 게임 로직(Kotlin 엔진) 이식은 별도 설계 필요.

## 2026-07-30 - 페이블 사용규칙 및 작업 로그 체계 수립

- `FABLE_RULES.md` 생성: 모델 역할 분담 4단계 파이프라인 정의 (Fable 설계 → Sonnet 구현 → Opus 검수 → Fable 최종 검수)
- `CLAUDE.md` 생성: 세션마다 규칙이 자동 로드되도록 FABLE_RULES.md 참조 추가
- `WORKLOG.md` 생성: 작업 로그 규칙 시작
