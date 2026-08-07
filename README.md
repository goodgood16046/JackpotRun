# 잭팟런 (Jackpot Run)

슬롯을 돌려 스테이지를 오르는 **로그라이트 슬롯 게임**. 웹 클라이언트와 Unity 모바일 앱으로 구성된다.

## 게임 소개

매 스테이지 요구 EXP를 슬롯 스핀으로 채워 올라가고, 5층마다 보스를 만난다.
클리어할 때마다 증강·유물·상점 노드에서 빌드를 키우고, 저주와 위험을 관리하며 최고 점수에 도전한다.

- **캐릭터 16종 × 슬롯머신 16종 × 장치 16종** — 시작 조합에 따라 플레이 스타일이 갈린다 (시너지 등급 S~C)
- **증강 80 · 유물 61 · 저주 16 · 아이템 73** — 세트 효과 33종과 조합하는 빌드 구성
- **특수 스핀** — 집중 / 올인 / 기도 / 막판, 그리고 능동 장치(재굴림·고정·복사·교체·예언 등)
- **업적 482종**, 장치 면허, 전공 연구(계정 성장), 테마 빌드 도감 25종
- 스테이지 진행·상점 경제·확률 테이블 기반의 정밀한 밸런스

## 구성

```
JackpotRun/
├─ Client/Jackpot/    # Unity 모바일 앱 (2022.3 LTS, Android 타깃)
│  ├─ Assets/JackpotRun/Scripts/Engine/   # 게임 엔진 — 순수 C# (UnityEngine 비의존)
│  ├─ Assets/JackpotRun/Scripts/UI,Game/  # uGUI 화면 + 세션/저장 계층
│  └─ Tools/EngineTests/                  # dotnet 헤드리스 테스트 (17,000+ 어서션)
├─ public/            # 웹 클라이언트 (Firebase Hosting)
│  ├─ play/                               # 브라우저 단독 웹게임 (JS 엔진 내장)
│  ├─ jackpotpick/ · jackpotdex/          # 카톡 봇 연동 뷰어 (시작 조합 선택 + 도감)
│  └─ ranking/                            # 글로벌 랭킹 보드 (앱 점수 표시)
├─ Docs/              # 설계 문서 (엔진 이식 설계, 사양 추출)
├─ kotlin-reference/  # 구버전(v2) 엔진 스냅샷 — 밸런스 사양 정답지 (읽기 전용)
└─ unity-assets/      # 아트·카탈로그 원본 데이터 (294건 메타 + 스프라이트 290장)
```

## Unity 앱

- 실행: `Client/Jackpot`을 Unity 2022.3.39f1로 열고 ▶ Play — 씬 세팅 없이 메뉴가 자동 생성된다.
- 흐름: 메인 메뉴 → 시작 조합 선택(시너지 분석) → 런 플레이 → 도감/업적. 프로필은 로컬 저장.
- 엔진은 UnityEngine 비의존 순수 C#이라 에디터 없이 검증 가능:
  ```bash
  dotnet run --project Client/Jackpot/Tools/EngineTests
  ```
- Android 베이스라인(패키지명·세로 고정·IL2CPP/ARM64)은 에디터 스크립트가 자동 적용한다.

## 웹

`public/` 아래는 성격이 셋으로 나뉜다.

| | 정체 | 백엔드 필요? |
|---|---|---|
| `play/` | **게임 본체** — JS 엔진 내장, 브라우저 단독 | 불필요 (랭킹·로그인만 선택적) |
| `jackpotpick/` · `jackpotdex/` | 카톡 봇 연동 **뷰어** | 봇이 RTDB 로 push 해야 함 |
| `ranking/` | 글로벌 랭킹 보드 | RTDB `jackpotrank` 읽기 |

- 배포: `firebase deploy --only hosting,database --project jackpotrun-web`
- 데모(백엔드 없이 UI 확인): `/jackpotpick/?demo=1`

### `play/` — 브라우저 단독 웹게임

2026-08-07 에 모카봇(`C:\dev\KakaoOpenChatBot\web\slot`)에서 이관. 구 주소
`mokabot-8ed4d.web.app/slot/` 은 여기로 보내는 리다이렉트만 남으며, localStorage 는 도메인 단위라
저장키 7개를 URL fragment 로 넘겨 새 페이지가 1회만 흡수한다.

- `engine.js` 가 `SlotV2Engine.kt` 의 확률·점수·요구치 공식을 JS 로 포팅한 자체 엔진.
- Firebase 는 랭킹(`slotrank`/`slotrank_asc`/`slotrank_deep`)과 구글 로그인에만 사용. 둘 다 실패해도 게임은 동작.
- 검증: `cd public/play && node _harness.mjs` → `{ok,errorCount,errors,notes}`. 일반300+심화300+스트레스200런.
  **배포 대상 아님**(`firebase.json` 의 `**/_harness*.mjs` ignore).
- ⚠️ Unity 앱/Kotlin 판과 **콘텐츠가 갈라져 있다** — 캐릭터 19 · 머신 19 · 장치 24 · 증강 89 · 유물 73 ·
  아이템 78 이고, 테마빌드 25 · 심화모드(주머니) · 승천은 이 판 전용이다.
  불변식·검증 절차는 `C:\dev\KakaoOpenChatBot\workflow\slotdev_rules.md` 가 단일 소스.

> ⚠️ **배포 순서**: 이 프로젝트를 먼저 배포한 뒤 모카봇을 배포할 것. 뒤집으면 리다이렉트가
> 아직 없는 주소를 가리킨다.

### RTDB 노드

| 노드 | 쓰는 쪽 |
|---|---|
| `jackpotdex` · `jackpotcmd` · `jackpotcatalog` · `jackpothall` | 카톡 봇 ↔ 뷰어 |
| `jackpotrank` | **Unity 앱** 점수 제출 → `ranking/` 표시 |
| `slotrank` · `slotrank_asc` · `slotrank_deep` | `play/` 랭킹 (일반·승천·심화) |

`play/` 는 구글 로그인을 쓴다(Authentication → Google provider 활성 상태, 승인 도메인 등록 완료).
로그인 사용자 행(`u_<uid>`)은 본인만 쓸 수 있게 규칙으로 막혀 있고, 게스트 `cid` 행은 열려 있다.

## 데이터

- `unity-assets/manifest.json` — 콘텐츠 294건(이름·효과·등급·가격·해금 조건)의 단일 소스.
  `id`가 스프라이트 파일명과 1:1 대응.
- 수치·공식의 정답지는 `kotlin-reference/`(구현 스냅샷)와 `Docs/EngineSpec/`(추출 사양서).

## 표기 규칙

숫자의 소수는 항상 2자리까지, 끝의 0은 제거한다 (`1.50` → `1.5`, `2.00` → `2`).
