# 작업 로그

모든 작업 완료 시 이 파일에 기록한다. 서식: `## 날짜 - 작업내용` (최신 항목이 위로 오도록 추가)

---

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
