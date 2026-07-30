# JackpotRunWeb — Claude 작업 컨텍스트

카카오 오픈채팅 봇(모카봇)의 **잭팟런(Jackpot Run)** 웹 클라이언트 + **Unity 이관용 리소스 추출본**.

## 현재 목표

**잭팟런을 Unity로 재개발한다.** `unity-assets/` 가 그 출발점이다.
웹(`public/`)은 기존 카톡 봇용으로 계속 운영 중이므로 Unity 작업이 건드리지 않는다.

## ⚠️ 이 저장소에 없는 것 — 먼저 읽을 것

게임 **로직 본체는 Kotlin**이다: `SlotV2Engine.kt`(173KB) · `SlotV2Service.kt`(187KB) ·
`SlotV2AchievementsExt.kt`(134KB) 등. 이들은 `C:\dev\KakaoOpenChatBot` 에 있고
**이 저장소에 포함돼 있지 않으며, 그 프로젝트는 git 저장소조차 아니다.**
→ 다른 PC에는 존재하지 않는다. 없는 파일을 찾지 말 것.

대신 밸런스·카탈로그 데이터는 전부 뽑아 두었다:

> **`unity-assets/manifest.json` 이 Unity 작업의 단일 소스다.**

## 디렉터리

```
JackpotRunWeb/
├─ public/            # Firebase Hosting 배포 대상 (웹, 기존 카톡용)
│  ├─ jackpotpick/    # 시작 조합 선택 화면
│  └─ jackpotdex/     # 도감/진행도 (img/*.png ×290)
├─ tools/             # 이미지 생성 스크립트 (배포 대상 아님)
└─ unity-assets/      # ★ Unity 이관용 (배포 대상 아님)
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

## 웹(`public/`) 을 건드릴 때만 해당

- 이 웹은 **자체 게임 로직이 없는 얇은 클라이언트**다. 봇(Kotlin)이 RTDB 로 push 하지 않으면 빈 화면.
- 배포: `firebase deploy --only hosting,database --project jackpotrun-web`
- `unity-assets/` 와 `tools/` 는 배포되지 않는다(`firebase.json` 의 hosting public = `public/`).
- ⚠️ **`database.rules.json` 이 전면 개방 상태다**(`jackpotcatalog`·`jackpothall` 은 인증 없이 read/write).
  미해결 이슈 — 이 저장소를 공개 저장소로 올리기 전에 반드시 조일 것.

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

> ⚠️ **푸시는 사람이 직접 실행해야 한다.** 이 환경의 Git Credential Manager 는 비대화형
> 세션에서 인증을 거부하므로(`Cannot prompt because user interactivity has been disabled`),
> Claude 가 `git push` 를 대신 실행할 수 없다. 커밋까지는 Claude 가 하고, 푸시는
> **사용자가 직접 열어둔 터미널**에서 `git push` 를 실행한다. 최초 1회 브라우저 로그인 후에는
> Windows 자격증명에 저장된다.

⚠️ **Public 저장소다.** 커밋에 토큰·비밀키를 넣지 말 것. `database.rules.json` 의 개방 규칙은
이미 공개돼 있다(아래 미해결 이슈).

### 참고: 오프라인 이관(번들)

네트워크 없이 옮겨야 할 때만 사용한다.

```powershell
git bundle create <경로>\JackpotRun.bundle --all   # 내보내기
git clone JackpotRun.bundle JackpotRun            # 받는 쪽
```
