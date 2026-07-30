# kotlin-reference — 잭팟런 원본 로직 스냅샷 (읽기 전용)

Unity 이관 시 **밸런스·공식·진행 규칙의 정답지**로 쓰기 위해 카카오 봇의 잭팟런(v2) Kotlin
소스를 그대로 복사해 둔 것. 추출 시점: **2026-07-30**

원본: `C:\dev\KakaoOpenChatBot\app\src\main\kotlin\com\ashersoft\kakaobot\`

## ⚠️ 이 폴더의 성질 — 먼저 읽을 것

1. **스냅샷이다.** 봇 본체는 git 저장소가 아니라 서브모듈·심링크로 묶을 수 없다.
   → **봇 쪽을 수정하면 이 폴더는 자동으로 갱신되지 않고 갈라진다.**
   진실의 출처(source of truth)는 언제나 `C:\dev\KakaoOpenChatBot` 이다.
2. **단독 빌드가 불가능하다.** `FirebaseRtdb`, Room DAO/DB, `ChatMessageHandler`,
   포인트/멤버 서비스 등 봇의 다른 클래스에 의존한다. 컴파일 대상이 아니라 **읽는 용도**다.
3. **여기를 고치지 말 것.** 수정해도 봇에 반영되지 않는다. 봇 동작을 바꿔야 하면 원본을 고친다.
4. **v1 슬롯은 제외했다.** 봇에는 슬롯 게임이 2개 병행한다 — v1 `슬롯`(`SlotEngine.kt`,
   `SlotRunService.kt`, `SlotDao.kt`, `SlotEntities.kt`)과 v2 `잭팟`(댓글 전용).
   **잭팟런은 v2** 이므로 v2 만 가져왔다. 파일명에 `V2` 가 없으면 잭팟런이 아니다.

## 파일별로 알 수 있는 것

### 1순위 (필수)

| 파일 | 내용 |
|---|---|
| `game/SlotV2Engine.kt` (173KB) | 머신별 **심볼 등장 확률표**, 스핀 계산, **EXP/점수 공식**, 스테이지 요구치 곡선, 콤보/세트 규칙, 증강·유물·저주 효과의 실제 수치 처리. `MACHINES`/`CHARS`/`AUGMENTS`/`RELICS`/`CURSES`/`ITEMS`/`DEVICES` 카탈로그 정의가 여기 있다 |
| `game/SlotV2Service.kt` (187KB) | 런 진행 흐름(스핀 수, 스테이지 → 상점 → 보스), **상점 가격·리롤**, 코인 경제, 장치 쿨다운, 명령어 처리 |

### 2순위

| 파일 | 내용 |
|---|---|
| `game/SlotV2AchievementsExt.kt` (134KB) | 업적 달성 조건·보상 정확값 |
| `game/SlotV2WebService.kt` (19KB) | **RTDB 데이터 구조** — 나중에 Firebase 연동할 때 노드 스키마(`jackpotdex/<t>`, `jackpotcmd/<w>`, `jackpotcatalog`, `jackpothall/seasons/<key>`), 토큰 발급(24-hex UUID, 60분 TTL, 1인1개) |

### 3순위 (밸런스 상수 분리 대비)

| 파일 | 내용 |
|---|---|
| `data/SlotV2Entities.kt` (7.8KB) | Room 엔티티 — 저장되는 런/점수/업적 상태의 실제 스키마 |
| `data/SlotV2Dao.kt` (2.8KB) | 쿼리 — 랭킹·조회 기준 |

## `unity-assets/` 와의 관계

- `unity-assets/manifest.json` 은 **이 소스에서 뽑아낸 카탈로그 데이터**(이름·효과·등급·가격·해금조건)다.
  UI 표시용으로는 manifest 가 편하다.
- 반면 **확률·공식·진행 규칙은 manifest 에 없다.** 그건 이 폴더의 `SlotV2Engine.kt` /
  `SlotV2Service.kt` 를 직접 읽어야 한다.
- 즉 **manifest = 무엇이 있는가 / kotlin-reference = 어떻게 계산되는가.**

## 참고: 봇 → 웹 데이터 흐름

`SlotV2WebService.kt` 가 이 프로젝트의 RTDB(`jackpotrun-web-default-rtdb`)로 push 하고,
`public/jackpotdex` · `public/jackpotpick` 이 그것을 읽는다. 웹은 자체 로직이 없는 얇은 클라이언트다.
