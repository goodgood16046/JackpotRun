# JackpotRun Web

카카오 오픈채팅 봇(모카봇)의 **잭팟런(Jackpot Run) 웹**을 별도 Firebase 프로젝트로 분리한 독립 프로젝트.
`mokabot-8ed4d` 에서 떼어내 자체 프로젝트 **`jackpotrun-web`** 로 운영한다.

- 선택 화면: <https://jackpotrun-web.web.app/jackpotpick/?t=…&w=…>
- 도감/진행도: <https://jackpotrun-web.web.app/jackpotdex/?t=…>

## 구성

```
JackpotRunWeb/
├─ .firebaserc            # default 프로젝트 = jackpotrun-web
├─ firebase.json          # hosting(public/) + no-cache 헤더 + database 규칙
├─ database.rules.json    # jackpotdex / jackpotcmd / jackpotcatalog / jackpothall 규칙
├─ public/                # 배포 대상(정적)
│  ├─ jackpotpick/        # 시작 조합 선택 (index.html, app.js, meta.js, pick.css)
│  └─ jackpotdex/         # 도감/진행도 (index.html, app.js, style.css, img/*.png ×290)
├─ tools/                 # 배포 대상 아님(빌드 전용)
│  ├─ gen_images.ps1      # 도감 이미지 재생성 (pollinations.ai)
│  └─ prompts.json        # 이미지 프롬프트 매니페스트
└─ unity-assets/          # 배포 대상 아님 — Unity 이관용 추출본(2026-07-29)
   ├─ manifest.json/.csv  # 294건 메타데이터(이름·효과·등급·해금조건·큐레이션)
   ├─ prompts.json        # 이미지 생성 프롬프트 사본
   ├─ regen_missing.ps1   # 아트 없는 장치 4종 생성(미실행)
   └─ Sprites/<카테고리>/  # 290장을 8개 카테고리로 분류
```

## 아키텍처 (중요)

이 웹은 **자체 게임 로직이 없는 얇은 클라이언트**다. 실제 잭팟런 게임은 카카오 봇(Kotlin
`SlotV2Service`/`SlotV2Engine`)에서 돌아가고, 봇이 **이 프로젝트의 RTDB로 데이터를 push** 한다.

- 봇 → 웹: `jackpotdex/<t>`(진행/해금/랭킹), `jackpotcatalog`(공유 카탈로그), `jackpothall/seasons/<key>`(명예의전당)
- 웹 → 봇: `jackpotcmd/<w>` 에 `{char, machine, device, cid, ts}`(다음 런 선택 예약) → 봇이 런 시작 시 소비

즉 **봇이 이 프로젝트로 쓰지 않으면 웹은 빈 화면**이다. 봇 측 writer 는
`app/src/main/kotlin/com/ashersoft/kakaobot/game/SlotV2WebService.kt` — 이 파일이
잭팟 전용 RTDB base(`jackpotrun-web-default-rtdb`)와 `WEB_BASE`(`https://jackpotrun-web.web.app`)를 가리켜야 한다.

### Firebase
- 프로젝트: `jackpotrun-web` (프로젝트 번호 52817920989)
- RTDB: `https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app` (asia-southeast1)
- Auth/Firestore/Storage 미사용. 규칙은 잭팟 4노드만 open(read/write), 토큰 길이 6~40 검증.

## 배포

```powershell
# 프로젝트 루트(JackpotRunWeb)에서
firebase deploy --only hosting,database --project jackpotrun-web
# 정적만: firebase deploy --only hosting --project jackpotrun-web
# 규칙만: firebase deploy --only database --project jackpotrun-web
```

`public/` 만 배포된다(`tools/` 는 hosting 대상 아님). `firebase.json` 의 no-cache 헤더로
`/jackpotpick`·`/jackpotdex` 는 CDN 캐시 없이 즉시 반영된다.

## 도감 이미지 재생성

```powershell
# 새 아이템/이미지 추가 시 prompts.json 갱신 후
powershell -File tools/gen_images.ps1
```
이미 존재하고 2KB 초과인 파일은 skip. 결과는 `public/jackpotdex/img/<id>.png`.

## Unity 이관용 추출본 (`unity-assets/`)

봇/웹에서 쓰는 이미지 290장을 카테고리별로 분류하고, `SlotV2Engine.kt` · `jackpotdex/app.js` ·
`jackpotpick/meta.js` 에서 뽑은 게임 데이터 294건을 결합한 패키지. **배포 대상이 아니다**
(`firebase.json` 의 hosting public 은 `public/` 뿐).

- 이미지는 전부 256×256 PNG, 불투명 배경(누끼 아님). 확대 시 뭉개지므로 고해상도가 필요하면
  `prompts.json` 의 프롬프트로 재생성한다.
- `manifest.json` 의 `id` 가 스프라이트 파일명과 1:1 → Unity 에서 로딩 키로 그대로 사용 가능.
- 장치 4종(`dev_holdfile`·`dev_major`·`dev_retake`·`dev_syllabus`)은 원래부터 아트가 없다.
  `unity-assets/regen_missing.ps1` 로 생성할 수 있다(외부 요청 발생).
- `Sprites/` 의 PNG 는 `public/jackpotdex/img/` 와 내용이 같다. git 은 동일 blob 을 한 번만
  저장하므로 저장소 용량은 거의 늘지 않는다.

## 원격 저장소

- `origin` = <https://github.com/goodgood16046/JackpotRun.git> (Public)
- `local` = `C:\dev\git-remotes\JackpotRunWeb.git` (로컬 베어, 백업용 — 선택)

```powershell
git clone https://github.com/goodgood16046/JackpotRun.git
```

⚠️ Public 저장소이므로 토큰·비밀키를 커밋하지 말 것.

## 데모(백엔드 없이 UI 확인)
- 선택: `/jackpotpick/?demo=1` — Firebase 초기화 없이 하드코딩 데이터로 렌더.
