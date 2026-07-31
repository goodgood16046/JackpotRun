# JackpotRun — Claude 작업 컨텍스트

**잭팟런(Jackpot Run)** — 로그라이트 슬롯 게임. **웹 클라이언트 + Unity 모바일 앱**.

## 현재 목표

**잭팟런을 Unity 모바일 앱으로 개발·출시한다.** Unity 프로젝트는 **`Client/Jackpot`**
(2022.3.39f1, Android 타깃). 웹(`public/`)은 기존 사용자용으로 계속 운영 중이므로 Unity 작업이 건드리지 않는다.

## 모델 역할 분담 (필수)

모든 구현 작업은 [FABLE_RULES.md](FABLE_RULES.md)의 4단계 파이프라인을 따른다:

1. **계획 및 설계** — Fable (메인 세션)
2. **구현** — Sonnet 서브에이전트 (Agent 도구, `model: sonnet`)
3. **1차 검수** — Opus 서브에이전트 (Agent 도구, `model: opus`)
4. **최종 검수** — Fable (메인 세션)

구현이 없는 단순 질문·조회는 파이프라인 없이 Fable이 직접 처리한다.

## 작업 로그 (필수)

모든 작업 완료 시 [WORKLOG.md](WORKLOG.md)에 `## 날짜 - 작업내용` 서식으로 기록한다. 최신 항목이 위로.

## Unity 클라이언트 (`Client/Jackpot`)

- 설계 문서: [Docs/UNITY_PORT_DESIGN.md](Docs/UNITY_PORT_DESIGN.md)(데이터·UI·앱 베이스라인) ·
  [Docs/ENGINE_PORT_DESIGN.md](Docs/ENGINE_PORT_DESIGN.md)(엔진 아키텍처·타입 계약·슬라이스 이력)
- **게임 엔진은 순수 C#** (`Assets/JackpotRun/Scripts/Engine/` — UnityEngine 비의존):
  `dotnet run --project Client/Jackpot/Tools/EngineTests` 로 에디터 없이 검증 (어서션 17,000+).
  콘텐츠 수치를 바꾸면 이 테스트가 잡는다(fx 스냅샷 회귀망).
- 카탈로그: `unity-assets/manifest.json` → `Client/Jackpot/Tools/convert_manifest.py` →
  `Assets/JackpotRun/Resources/JackpotRun/catalog.json` (JsonUtility-safe)
- UI는 전부 코드 생성 uGUI, TMP 미사용(한글은 번들 Pretendard 폰트). 씬 세팅 불필요 — ▶ Play로 즉시 실행.
- Android 베이스라인 자동 적용: `Assets/JackpotRun/Editor/AndroidAppBaseline.cs` (패키지 `com.phigolf.jackpotrun`)
- MCP: 에디터를 열면 stdio 브리지가 자동 시작된다(`Editor/McpBridgeAutoStart.cs`). 루트 `.mcp.json` 사용.

## 밸런스 정답지 — `kotlin-reference/` 와 `Docs/EngineSpec/`

게임 규칙의 원본은 구버전(v2) 엔진 Kotlin 스냅샷이다. **읽기 전용** — 수정 금지, 단독 빌드 불가.
자세한 주의사항은 `kotlin-reference/README.md`.

| 용도 | 볼 곳 |
|---|---|
| **무엇이 있는가** — 이름·효과·등급·가격·해금조건, 스프라이트 매핑 | `unity-assets/manifest.json` |
| **어떻게 계산되는가** — 확률표, EXP/점수 공식, 요구치 곡선, 상점·코인 경제 | `kotlin-reference/game/SlotV2Engine.kt` · `SlotV2Service.kt` (정밀 추출본: `Docs/EngineSpec/`) |

C# 엔진은 이 스냅샷과 전수 대조로 검증되었다(원본 유지 버그 목록은 ENGINE_PORT_DESIGN.md 부록).

## 디렉터리

```
JackpotRun/
├─ Client/Jackpot/    # ★ Unity 앱 프로젝트 (2022.3.39f1, Android)
├─ Docs/              # 설계 문서 + EngineSpec(사양 추출 3부)
├─ public/            # 웹 클라이언트 (Firebase Hosting 배포 대상)
│  ├─ jackpotpick/    # 시작 조합 선택 화면
│  └─ jackpotdex/     # 도감/진행도 (img/*.png ×290)
├─ tools/             # 이미지 생성 스크립트 (배포 대상 아님)
├─ kotlin-reference/  # ★ 구버전 엔진 스냅샷 (읽기 전용 — 확률·공식의 정답지)
└─ unity-assets/      # ★ 카탈로그·아트 원본 데이터 (배포 대상 아님)
   ├─ manifest.json   # 294건 — 이름·효과·등급·가격·해금조건·큐레이션 메타
   ├─ prompts.json    # 290장 각각의 AI 생성 프롬프트
   └─ Sprites/<카테고리>/*.png
```

## unity-assets 사용법

- `manifest.json` 의 `entries[]` 294건. **`id` 가 스프라이트 파일명과 1:1** → `Resources.Load<Sprite>` 키로 그대로 사용.
- 카테고리 8개: 캐릭터16 · 슬롯머신16 · 장치16 · 증강80 · 유물61 · 저주16 · 아이템73 · 업적16
- 이미지 **290장, 전부 256×256 PNG, 불투명 배경(누끼 아님)**. 확대하면 뭉개진다.
  고해상도가 필요하면 `prompts.json` 프롬프트로 재생성(외부 요청 발생).
- **장치 4종은 원래부터 아트가 없다**: `dev_holdfile` · `dev_major` · `dev_retake` · `dev_syllabus` → 이모지 폴백.
- `Sprites/` 의 PNG 는 `public/jackpotdex/img/` 와 같은 파일이다. **한쪽만 고치면 갈라진다** — 양쪽 다 갱신할 것.

## 웹(`public/`) 을 건드릴 때만 해당

- 웹은 **자체 게임 로직이 없는 얇은 클라이언트**다 — 백엔드가 RTDB로 push한 데이터를 표시한다.
- 배포: `firebase deploy --only hosting,database --project jackpotrun-web`
- `unity-assets/` 와 `tools/` 는 배포되지 않는다(hosting public = `public/`).
- 🔴 **미해결 보안 이슈**: `database.rules.json` 이 사실상 개방 상태다(일부 노드 무인증 read/write,
  나머지는 토큰 길이 검증만). **이 저장소가 Public 이므로 해당 구조는 공개돼 있다.**
  백엔드 쓰기는 유지하면서 공개 쓰기만 차단하도록 조여야 한다.

## 표기 규칙

숫자 출력의 소수는 **항상 2자리까지, 끝의 0 은 제거**한다(`1.50` → `1.5`, `2.00` → `2`).

## 원격 / 다른 PC 와의 동기화

- `origin` = **<https://github.com/goodgood16046/JackpotRun.git>** (Public, 2026-07-30 연결)

새 PC 에서 시작:

```powershell
git clone https://github.com/goodgood16046/JackpotRun.git
cd JackpotRun
```

평소 작업은 일반적인 `git pull` / `git push` 로 충분하다. **작업 시작 전 `git pull` 습관화.**

> ⚠️ **각 PC 의 첫 푸시 1회만 사람이 직접 실행해야 한다.** Git Credential Manager 는 저장된
> 자격증명이 없으면 브라우저 OAuth 창을 띄워야 하는데, Claude 의 셸은 비대화형이라 실패한다.
> → 새 PC 에서는 **사용자가 터미널에서 `git push` 를 한 번** 실행해 브라우저 로그인을 마친다.
> 그 뒤로는 Windows 자격증명에 저장되어 Claude 도 정상적으로 push 할 수 있다.

⚠️ **Public 저장소다.** 커밋에 토큰·비밀키를 넣지 말 것.
