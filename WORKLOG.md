# 작업 로그

모든 작업 완료 시 이 파일에 기록한다. 서식: `## 날짜 - 작업내용` (최신 항목이 위로 오도록 추가)

---

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
