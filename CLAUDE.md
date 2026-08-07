# JackpotRun — Claude 작업 컨텍스트

카카오 오픈채팅 봇(모카봇)의 **잭팟런(Jackpot Run)** 웹 클라이언트 + **Unity 앱 클라이언트**.

## 현재 목표

**잭팟런을 Unity 모바일 앱으로 재개발한다.** `unity-assets/` 가 데이터 출발점이고,
Unity 프로젝트는 **`Client/Jackpot`** (2022.3.39f1, Android 타깃)이다.
웹(`public/`)은 계속 운영 중이므로 Unity 작업이 건드리지 않는다.

### 현황 — Unity 는 아직 **UI 데모**다

`Client/Jackpot` 은 메인메뉴 · 선택화면 · 도감 **UI 만** 있고 게임 엔진은 없다.
`JackpotRunApp.Awake()` 가 `DemoData.Demo()` 하드코딩을 물고 있어 스핀·점수·보스가 전부 미구현이다.
당시 작업 PC 에 Kotlin 원본이 없어 엔진을 못 옮긴 상태로 멈춘 것이며, 그 막힘은
`kotlin-reference/` 반입(2026-07-30)으로 이미 해소됐다.

> 💡 **엔진 이식은 `public/play/engine.js` 에서 출발하는 편이 빠르다.** Kotlin 원본은 코루틴·Room·봇
> 클래스에 얽혀 있지만, `engine.js` 는 같은 공식을 **순수함수로 이미 포팅**해 뒀고 하네스로 검증돼 있다.
> Kotlin 은 값·확률표의 최종 정답지로 대조용으로 쓰고, 구조는 JS 를 따라가면 된다.

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

- 설계 문서: [Docs/UNITY_PORT_DESIGN.md](Docs/UNITY_PORT_DESIGN.md) — 데이터 변환·C# API·UI·앱 베이스라인 사양
- 카탈로그: `unity-assets/manifest.json` → `Client/Jackpot/Tools/convert_manifest.py` → `Assets/JackpotRun/Resources/JackpotRun/catalog.json` (JsonUtility-safe)
- UI는 전부 코드 생성 uGUI, TMP 미사용(한글은 번들 Pretendard 폰트)
- Android 베이스라인 자동 적용: `Assets/JackpotRun/Editor/AndroidAppBaseline.cs` (패키지 `com.phigolf.jackpotrun`)

## 원본 로직은 `kotlin-reference/` 에 있다 — 먼저 읽을 것

게임 **로직 본체는 Kotlin**이다(`SlotV2Engine.kt` · `SlotV2Service.kt` 등). 원본은
`C:\dev\KakaoOpenChatBot` 에 있고 **그 프로젝트는 git 저장소가 아니다.** 그래서 잭팟런(v2)
관련 6개 파일만 `kotlin-reference/` 에 **스냅샷으로 복사**해 두었다(2026-07-30, 525KB).

> ⚠️ **스냅샷이므로 봇 쪽을 고치면 갈라진다.** 진실의 출처는 항상 `C:\dev\KakaoOpenChatBot`.
> `kotlin-reference/` 를 수정해도 봇에 반영되지 않으며, 단독 빌드도 불가능하다(봇의 다른
> 클래스에 의존). **읽는 용도 전용.** 자세한 주의사항은 `kotlin-reference/README.md`.

두 데이터 소스의 역할이 다르다:

| 용도 | 볼 곳 |
|---|---|
| **무엇이 있는가** — 이름·효과·등급·가격·해금조건, 스프라이트 매핑 | `unity-assets/manifest.json` |
| **어떻게 계산되는가** — 확률표, EXP/점수 공식, 요구치 곡선, 상점·코인 경제 | `kotlin-reference/game/SlotV2Engine.kt` · `SlotV2Service.kt` |

**v1 슬롯은 제외했다.** 봇에는 슬롯 게임이 2개 병행한다 — v1 `슬롯` 과 v2 `잭팟`.
**잭팟런은 v2**다. 파일명에 `V2` 가 없으면 잭팟런이 아니다.

## 디렉터리

```
JackpotRun/
├─ Client/Jackpot/    # ★ Unity 앱 프로젝트 (2022.3.39f1, Android)
├─ Docs/              # 설계 문서 (UNITY_PORT_DESIGN.md 등)
├─ public/            # Firebase Hosting 배포 대상
│  ├─ play/           # ★ 단독 웹게임 (봇 불필요, JS 엔진 내장) — 2026-08-07 모카봇에서 이관
│  ├─ jackpotpick/    # 시작 조합 선택 화면 (뷰어 — 봇 필요)
│  └─ jackpotdex/     # 도감/진행도 (뷰어 — 봇 필요, img/*.png ×290)
├─ tools/             # 이미지 생성 스크립트 (배포 대상 아님)
├─ kotlin-reference/  # ★ 봇 원본 로직 스냅샷 (읽기 전용 — 확률·공식의 정답지)
│  ├─ game/           # SlotV2Engine · SlotV2Service · SlotV2AchievementsExt · SlotV2WebService
│  └─ data/           # SlotV2Entities · SlotV2Dao
└─ unity-assets/      # ★ Unity 이관용 데이터 (배포 대상 아님)
   ├─ manifest.json   # 294건 — 이름·효과·등급·가격·해금조건·큐레이션 메타
   ├─ manifest.csv    # 같은 내용 표 버전
   ├─ prompts.json    # 290장 각각의 AI 생성 프롬프트
   ├─ regen_missing.ps1
   └─ Sprites/<카테고리>/*.png
```

## unity-assets 사용법

- `manifest.json` 의 `entries[]` 294건. **`id` 가 스프라이트 파일명과 1:1** →
  `Resources.Load<Sprite>` 나 Addressables 키로 그대로 쓸 수 있다.
- 카테고리 8개: 캐릭터16 · 슬롯머신16 · 장치16 · 증강80 · 유물61 · 저주16 · 아이템73 · 업적16
- 필드: `nameKo`/`descKo`(한글 이름·효과), `tier`(SILVER/GOLD/PRISM), `price`/`coinCost`,
  `unlockReq`(해금 조건), `pick`(난이도·고점·안정성·위험·장단점 — 캐릭/머신/장치 44건)
- 이미지 **290장, 전부 256×256 PNG, 불투명 배경(누끼 아님)**. 확대하면 뭉개진다.
  고해상도가 필요하면 `prompts.json` 의 프롬프트로 재생성한다(pollinations.ai, 외부 요청 발생).
- **장치 4종은 원래부터 아트가 없다**: `dev_holdfile` · `dev_major` · `dev_retake` · `dev_syllabus`.
  → 카탈로그 294건 중 이미지 있는 것은 290건. `regen_missing.ps1` 로 생성 가능.
- `Sprites/` 의 PNG 는 `public/jackpotdex/img/` 와 같은 파일이다(git 이 동일 blob 을 한 번만 저장).
  **한쪽만 고치면 갈라진다** — 웹에도 반영해야 하면 양쪽 다 갱신할 것.

## `public/play/` — 단독 웹게임 (★ 이미 돌아가는 구현체)

**"봇 없이 실행되는 잭팟런"은 이미 존재한다.** Unity 재개발과 별개로 `public/play/` 가 그것이다.
2026-08-07 에 모카봇(`C:\dev\KakaoOpenChatBot\web\slot`)에서 이관했고, 구 경로엔 리다이렉트만 남았다.

- `engine.js` 가 `SlotV2Engine.kt` 의 확률·점수·요구치 공식을 JS 로 **이미 포팅**해 뒀다.
  → **Unity C# 이식의 실질적 출발점은 Kotlin 이 아니라 이쪽이다.** 순수함수라 코루틴·Room·봇 의존이 없다.
- Firebase 는 랭킹(`slotrank`/`slotrank_asc`/`slotrank_deep`)과 구글 로그인에만 사용. 둘 다 실패해도 게임은 동작.
- 저장: localStorage `slotweb_profile`(+ `slotweb_cid`/`_nick`/`_vol`/`_sound`/`_vibe`/`_seenlogin`).
- 검증: `cd public/play && node _harness.mjs` → `{ok,errorCount,errors,notes}`. 일반300+심화300+스트레스200런.
- 불변식(일반모드 격리·fmt2·패리티·랭킹 분리)의 단일 소스는
  `C:\dev\KakaoOpenChatBot\workflow\slotdev_rules.md` — **수정 전 반드시 읽을 것**.

> ⚠️ **카톡판의 상위집합이다** — 캐릭터 19/16 · 머신 19/16 · 장치 24/16 · 증강 89/80 · 유물 73/61 ·
> 아이템 78/73. 테마빌드 25 · 심화모드 · 승천은 웹 전용. 게임플레이 캐논은 Kotlin판이고,
> 공통 규칙을 고치면 양판 모두 반영해야 한다(한쪽만 고치면 갈라진다).

## 웹 뷰어(`jackpotpick`·`jackpotdex`) 를 건드릴 때만 해당

- 이 둘은 **자체 게임 로직이 없는 얇은 클라이언트**다. 봇(Kotlin)이 RTDB 로 push 하지 않으면 빈 화면.
- 배포: `firebase deploy --only hosting,database --project jackpotrun-web`
- `unity-assets/` 와 `tools/` 는 배포되지 않는다(`firebase.json` 의 hosting public = `public/`).
- 🔴 **미해결 보안 이슈**: `database.rules.json` 이 전면 개방 상태다 — `jackpotcatalog` ·
  `jackpothall` 은 인증 없이 read/write 가능하고, `jackpotdex`/`jackpotcmd` 는 6~40자 토큰만
  맞으면 열린다. **이 저장소가 이미 Public 이므로 해당 구조는 공개돼 있다.** 명예의전당·카탈로그
  변조가 가능한 상태이니, 봇 쓰기는 유지하면서 공개 쓰기만 차단하도록 조여야 한다.

## 표기 규칙

숫자 출력의 소수는 **항상 2자리까지, 끝의 0 은 제거**한다(`1.50` → `1.5`, `2.00` → `2`).
카톡 봇의 기존 규칙이며 Unity 이식 시에도 동일하게 유지한다.

## 원격 / 다른 PC 와의 동기화

- `origin` = **<https://github.com/goodgood16046/JackpotRun.git>** (Public, 2026-07-30 연결)
- `local` = `C:\dev\git-remotes\JackpotRunWeb.git` — DESKTOP-8IV6RC3 의 로컬 베어(백업용).
  그 PC 에서만 접근 가능하며 필수는 아니다.

새 PC 에서 시작:

```powershell
git clone https://github.com/goodgood16046/JackpotRun.git
cd JackpotRun
```

평소 작업은 일반적인 `git pull` / `git push` 로 충분하다. 양쪽 PC 에서 작업해도 되지만
**작업 시작 전 `git pull` 을 습관화**할 것.

> ⚠️ **각 PC 의 첫 푸시 1회만 사람이 직접 실행해야 한다.** Git Credential Manager 는 저장된
> 자격증명이 없으면 브라우저 OAuth 창을 띄워야 하는데, Claude 의 셸은 비대화형이라
> `Cannot prompt because user interactivity has been disabled` 로 실패한다.
> → 새 PC 에서는 **사용자가 터미널에서 `git push` 를 한 번** 실행해 브라우저 로그인을 마친다.
> 그 뒤로는 Windows 자격증명(`git:https://github.com`)에 저장되어 **Claude 도 정상적으로
> `git push` 할 수 있다**(DESKTOP-8IV6RC3 에서 확인 완료, 2026-07-30).

⚠️ **Public 저장소다.** 커밋에 토큰·비밀키를 넣지 말 것. `database.rules.json` 의 개방 규칙은
이미 공개돼 있다(위 「웹을 건드릴 때만 해당」의 미해결 보안 이슈 참고).

### 참고: 오프라인 이관(번들)

네트워크 없이 옮겨야 할 때만 사용한다.

```powershell
git bundle create <경로>\JackpotRun.bundle --all   # 내보내기
git clone JackpotRun.bundle JackpotRun            # 받는 쪽
```
