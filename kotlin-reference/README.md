# kotlin-reference — 잭팟런 구버전(v2) 엔진 스냅샷 (읽기 전용)

Unity 이식 시 **밸런스·공식·진행 규칙의 정답지**로 쓰기 위해 구버전(v2) 엔진 Kotlin 소스를
그대로 복사해 둔 것. 추출 시점: **2026-07-30**

## ⚠️ 이 폴더의 성질 — 먼저 읽을 것

1. **스냅샷이다.** 원본 프로젝트와 자동 동기화되지 않는다 — 여기를 고쳐도 어디에도 반영되지 않는다.
2. **단독 빌드가 불가능하다.** 원본 프로젝트의 다른 클래스(DB·메시징 계층 등)에 의존한다.
   컴파일 대상이 아니라 **읽는 용도**다.
3. **v2만 가져왔다.** 파일명에 `V2` 가 없으면 잭팟런이 아니다.
4. 현재 C# 엔진(`Client/Jackpot/Assets/JackpotRun/Scripts/Engine/`)이 이 스냅샷을 기준으로
   이식·검증되었다. 상세 사양서는 `Docs/EngineSpec/` 참조.

## 파일별로 알 수 있는 것

| 파일 | 내용 |
|---|---|
| `game/SlotV2Engine.kt` (173KB) | 머신별 **심볼 등장 확률표**, 스핀 계산, **EXP/점수 공식**, 스테이지 요구치 곡선, 콤보/세트 규칙, 증강·유물·저주 효과 수치. `MACHINES`/`CHARS`/`AUGMENTS`/`RELICS`/`CURSES`/`ITEMS`/`DEVICES` 카탈로그 정의 |
| `game/SlotV2Service.kt` (187KB) | 런 진행 흐름(스핀 수, 스테이지 → 상점 → 보스), **상점 가격·리롤**, 코인 경제, 장치 쿨다운, 입력 처리 |
| `game/SlotV2AchievementsExt.kt` (134KB) | 업적 달성 조건·보상 정확값 (확장 466종) |
| `game/SlotV2WebService.kt` (19KB) | 웹 연동 데이터 구조 — RTDB 노드 스키마, 토큰 발급 규칙 |
| `data/SlotV2Entities.kt` (7.8KB) | 저장되는 런/점수/업적 상태의 실제 스키마 |
| `data/SlotV2Dao.kt` (2.2KB) | 랭킹/조회 쿼리 패턴 |
