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
├─ public/            # 웹 클라이언트 (Firebase Hosting) — 시작 조합 선택 + 도감/진행도
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

- 시작 조합 선택(`public/jackpotpick/`)과 도감/진행도(`public/jackpotdex/`) 화면.
- 배포: `firebase deploy --only hosting,database --project jackpotrun-web`
- 데모(백엔드 없이 UI 확인): `/jackpotpick/?demo=1`

## 데이터

- `unity-assets/manifest.json` — 콘텐츠 294건(이름·효과·등급·가격·해금 조건)의 단일 소스.
  `id`가 스프라이트 파일명과 1:1 대응.
- 수치·공식의 정답지는 `kotlin-reference/`(구현 스냅샷)와 `Docs/EngineSpec/`(추출 사양서).

## 표기 규칙

숫자의 소수는 항상 2자리까지, 끝의 0은 제거한다 (`1.50` → `1.5`, `2.00` → `2`).
