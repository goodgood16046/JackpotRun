# 작업 로그

모든 작업 완료 시 이 파일에 기록한다. 서식: `## 날짜 - 작업내용` (최신 항목이 위로 오도록 추가)

---

## 2026-08-07 - 웹 파리티 P2 완료: 점수·배율캡·보스 웹화

- **클리어 점수 웹 공식 채택**(`game.js:1412-1419`): `stage×50 + leftover×2 + leftSpins×100 +
  boss500 + streak` — 등급 보너스(50~1000)·closeBonus(150/300)·막판 보너스(200)·저주 배수 전부 제거.
  등급은 **연출 전용 6단계**로 전환(웹 `ui.js` clearGrade 임계 20/50/100/200%·exact 선처리·보스 +1
  승급·한글 라벨 1:1 + `gradeTier` 정수 필드). 클리어 코인은 웹이 **가산식**(`5 + 보스12 + 보너스`)
  임을 확인해 보스 클리어 12→17로 수정, **빚문서는 점수만 0이고 코인은 지급**(웹 대조로 잡은 2건).
- **총배율 캡 제거**: 웹 일반 모드에 캡 없음을 grep으로 확정(`capMul` 0건, `MAX_SPIN_EXP_MUL`은
  심화 specialMul 전용) → `Formulas.CapMulFor`·Evaluate capBase/expNoCenter·`lastSpinExpMul` 상한
  5.0 삭제. Evaluate/ApplyBoss 시그니처 정리(호출부 8곳 전수 갱신).
- **보스 정리**: grad(졸업심사) pace 룰 제거 — 웹은 EXP 룰 없음, quotaMul 1.15만. finals 분기 순서
  웹 정렬. finals/strict/luck 수치·정수처리 일치 확인(음수 방어로 C# 정수나눗셈 == JS floor 증명).
- 파이프라인: Fable 설계 → Sonnet 구현 → Opus 검수(웹 인용 라인 전수 실물 확인, 인용 오류 0 —
  필수 2건: 빚문서 코인 분리·등급 회귀 보강) → Sonnet 반영 → Fable 최종.
- 검증: EngineTests 전체 통과(HEAD 기준선 교차 실행으로 의도 외 골든 파손 0, 기대값 전부
  웹 공식 수기 재산출 — 순환 검증 0). `WEB_PARITY_DESIGN.md` §2-B (I)(J)(K) 근거 라인 추가, P2 완료.

## 2026-08-07 - 웹 파리티 전환 착수 + P1 완료 (Unity ← `public/play/`)

- 지시 "유니티 게임을 웹페이지에 있는 게임처럼 다 뜯어고쳐줘" — 양측 전수 인벤토리 조사(웹 20개
  시스템/Unity 현황·격차) 후 **[Docs/WEB_PARITY_DESIGN.md](Docs/WEB_PARITY_DESIGN.md) 마스터 플랜**
  작성: 정답지를 kotlin-reference → **웹 단독판(`public/play/`)으로 전환**(CLAUDE.md 반영),
  규칙 충돌 결정 로그(A~H), 페이즈 로드맵 P1~P7(P7 심화모드가 최대 규모).
- **P1 룰 파리티 완료** (Fable 설계 → Sonnet 구현 → Opus 검수 → Sonnet 반영 → Fable 최종):
  ① 특수스핀 **첫 사용 무료**(런당 종류별 1회, 발동 성공 시만 소진 — `RunState.CmdFreeUsed`,
  모드 버튼 "무료" 라벨) ② **첫 판 즉시 시작**(runs==0 → novice+basic 직행 + 안내 토스트)
  ③ 실패 체인 웹 순서(보험+2스핀 → POST_SPIN → fate_bell≤15 → 게임오버) ④ REST 12·CURSE 30·
  EVENT-6 장치 획득(rare 가중 `min(0.6,0.15+stage×0.03)` 이식)·보스 클리어 **DEVICE 노드**
  (장착 or 코인15, 어느 쪽이든 영구 보유 — RunEvent→StatTracker→Profile 경로) ⑤ 자발적 **포기**
  (Spin/PostSpin 즉시 결산, voluntary 플래그·확인 시트).
- Opus 검수 반영: **[치명] dev_bell POST_SPIN 데드엔드**(버튼이 거부 토스트로 빠지고 fate_bell
  회생까지 차단하던 신규 회귀) → 웹 `emergencyBell()` 동일 즉시 클리어+장치 파괴로 해소.
  영구 보유 왕복 테스트 신설, GameSession 장치 시드를 UnlockedDevices(2원 판정)로, 순환 검증
  제거, 포기 버튼 페이즈 게이팅. 결정 로그 F/G/H 추가(웹 pickDevices의 owned 인자 버그
  미재현 — 미보유 필터 채택 등).
- 검증: EngineTests **18,094 통과(+262, 기준선 17,832)** — HEAD 테스트 × 신엔진 교차 실행으로
  "의도된 의미 변경 67건 외 골든(시드 유래 fx/점수/EXP) 파손 0" 확인(순환 검증 아님 증명).
  Unity 배치(`BuildAllUnattended`)로 임포트·컴파일·씬 리빌드.

## 2026-08-07 - 잭팟런 소개·설명 PDF 제작 (`Docs/잭팟런_소개서.pdf`)

- 요청 "잭팟런 소개 및 설명 pdf가 필요해" — A4 6페이지 소개서 신규 제작.
  구성: 표지 → 게임 개요(한 판 흐름·요구 EXP 곡선·실패 구제 체인) → 심볼&스핀(심볼 14종 표·
  세트 보너스·특수 스핀 4종·보스 4종) → 빌드&성장(3스텝·콘텐츠 규모·메타 성장) →
  게임 모드&랭킹(일반·심화·승천 A1~A10·랭킹 3보드) → 플랫폼&개발 현황(웹/Unity·파리티 P1~P7).
- 수치는 `public/play/data.js`(웹 단독판 정답지)·`Docs/EngineSpec/01_engine.md`·
  `Docs/WEB_PARITY_DESIGN.md` 실측 기준. 콘텐츠 카운트는 웹판(캐릭19·머신19·장치24·증강89·
  유물73·아이템78·저주16·테마빌드25·업적34·심볼72).
- 제작 방식: 다크 카지노 테마 HTML(번들 Pretendard 폰트 + `public/jackpotdex/img` 스프라이트
  13장 file:// 참조) → Edge 헤드리스 `--print-to-pdf`. 스크린샷 검수로 6페이지 넘침 없음 확인.
  원본 HTML은 세션 스크래치패드(임시) — 재생성 필요 시 이 항목 참조.

## 2026-08-07 - 웹게임 스핀을 노치 스크롤 릴로 교체 (`public/play/`)

- 요청 "잭팟픽에 구현했던 슬롯 형태로 실제 슬롯 돌리는 것처럼" — `/play/` 게임의 스핀 연출을
  "제자리 이모지 셔플(70ms 인터벌)"에서 **Unity ReelView(S13/S14)식 노치 스크롤 릴**로 교체.
  파이프라인: Fable 설계 → Sonnet 구현 → Opus 검수 → Sonnet 반영 → Fable 최종 검수(실측).
- `ui.js`/`style.css`만 수정(엔진·게임 로직 불변): `.reel`(aspect 2/3) > `.rstrip`(5슬롯, 기본
  `translateY(-35%)` = JS BASE -1.75S와 일치, Opus 수기 검증 오차 0) > `.rslot`(이웃 dim .38/.85).
  `g.spin` 결과 선확정 → 릴별 가속(0.22s, 노치 0.14→0.055)+유지(0.28+i×0.09 스태거)→감속
  3노치(D-3 타깃 주입)→슬롯비례 오버슈트 착지. blastBomb/glowMatches/잭팟 후속 흐름 원문 보존.
- Opus 검수 반영 9건: Promise.all 예외 가드(연출 예외에도 결과 강제 반영 — 런 유실 방지),
  매치/폭발 `.sym` 선택자를 중앙 슬롯으로 스코프 축소, `.rfade` 상하단 페이드 마스크,
  `max-height:560px` 릴 72px 캡, 착지 후 인라인 transform 정리(리사이즈 어긋남 방지),
  reduced-motion settle 해제, 이웃 장식 stripMemo 캐시(재렌더 깜빡임 제거), 오버슈트 비례화,
  이웃 drop-shadow 제거(컴포지터 필터 30→6).
- 검증: 하네스 `ok:true` ×2회 · 헤드리스 실플레이 390×844/740×360/라이브 3회 — 스트립 구조·
  스핀 중 실이동·중앙 착지·EXP 반영·연속 스핀·폭탄 케이스(💣→▫)·콘솔 에러 0. 배포 완료.

## 2026-08-07 - 단독 웹게임(`public/play/`) 이관 — 모카봇 `web/slot` → 이 저장소

"저장소를 클론하면 잭팟런이 돌아가야 한다"는 요구에 대응. **새로 만들지 않고 이미 존재하던
브라우저 단독판을 옮겼다** — 사본을 늘리지 않기 위해 복사가 아니라 **이동**.

- `public/play/` 신규 — 모카봇 `C:\dev\KakaoOpenChatBot\web\slot` 의 10파일(1.02MB).
  data.js(카탈로그) · engine.js(순수엔진, `SlotV2Engine.kt` 공식 포팅) · game.js(런 상태머신) ·
  ui.js(렌더) · rank.js · auth.js · sound.js · style.css · index.html · _harness.mjs(검증, 배포 제외).
  **이미지 0장**(전부 이모지), 하위폴더 없음, 절대경로 의존 0건 → `/play/` 로 무수정 이식 가능했음.
- Firebase 컷오버: `auth.js`·`rank.js` 의 config 를 `mokabot-8ed4d` → **`jackpotrun-web`**.
  게임 로직은 Firebase 와 무관하게 동작하므로(지연 로드·실패 무시) 랭킹/로그인만 영향.
- `database.rules.json`: 랭킹 3노드 추가 — `slotrank` · `slotrank_asc` · `slotrank_deep`.
  `.indexOn: "score"` 부여(`orderByChild("score")` 쿼리가 인덱스 없이 전량 다운로드하는 문제 예방),
  키 길이 6~64 검증(게스트 cid / 로그인 `u_<uid>` 양쪽 수용).
- `firebase.json`: `/play/**` no-cache 헤더 + `**/_harness*.mjs` hosting 제외.
- **구 주소 리다이렉트**: 모카봇 `web/slot/index.html` 을 리다이렉트 전용 페이지로 교체(게임 9파일 삭제).
  localStorage 는 도메인 단위라 그냥 보내면 진행도가 끊기므로, **저장키 7개를 URL fragment(base64url)
  에 실어 넘기고 새 페이지가 1회만 흡수**하도록 했다. fragment 는 서버로 전송되지 않는다.
  수신측은 `public/play/index.html` 의 인라인 스크립트(module 보다 먼저 실행) — 기존 프로필이
  있으면 덮어쓰지 않고, 실패해도 조용히 새 프로필로 시작한다.
- 슬롯 개발 툴체인 경로 갱신(이관하면 깨지는 것들): `workflow/slotdev_rules.md`,
  `.claude/workflows/slot-game-dev.js`(`WEB` 상수), `.claude/agents/slot-dev.md`.
- 검증: `node --check` 7파일 통과 + **공식 하네스 `ok:true errorCount:0`**
  (일반 300/300 · 심화 300/300 · 스트레스 200런 · NaN/음수/무한대 스캔).
- 백업: `C:\dev\KakaoOpenChatBot\backups\web_slot_20260807_backup\` (원본 10파일).
- ⚠️ **배포 순서 고정**: `jackpotrun-web` 먼저 → 모카봇 나중. 뒤집으면 리다이렉트가 없는 주소를 가리킨다.
- **[E드라이브 PC] 1단계 배포 완료**: pull(fast-forward, 충돌 0) 후 `firebase deploy --only hosting,database`.
  라이브 검증 — `/play/` 200 · `_harness.mjs` 배포 제외 확인(404) · 헤드리스 실플레이로
  인트로→게스트→홈→런 시작→첫 스핀까지 EXP/코인/불운게이지 정산 정상, 콘솔 에러 0.
  링크: <https://jackpotrun-web.web.app/play/>. 모카봇 쪽 리다이렉트 배포(2단계)는 모카봇 PC에서.
- 🔴 **수동 필요**: `jackpotrun-web` 콘솔에서 Google provider 사용설정 + 승인 도메인 등록.
  미설정 시 로그인만 실패(게스트 플레이는 정상). 기존 `slotrank` 랭킹 기록은 이설하지 않아 초기화된다.
## 2026-08-07 - 웹 "시작 버튼 무반응" 조사 → 결과 모달 추가 + 전체 배포

- 사용자 보고 "시작 버튼 눌러도 반응이 없다". 헤드리스 브라우저(CDP) 실클릭 재현으로 조사 —
  라이브 데모·로컬 신코드·모바일 뷰포트(390×844) 전부에서 버튼은 정상 동작했고(#msg 표시·뷰포트 내·가림 없음),
  jackpotpick은 7/24 초기 커밋 이후 무변경이라 회귀도 아님. **실체는 UX**: 데모 모드 시작 버튼의 피드백이
  하단 바의 12.5px 안내 한 줄뿐이라 "무반응"으로 느껴졌던 것.
- `app.js`/`pick.css`: `msgOk`/`msgBad` 결과(데모 안내·예약 완료·만료·실패)를 화면 중앙 모달 카드
  (`.msgov`, ✅/⚠️ + 확인 버튼, 탭 닫힘)로도 표시. 파이프라인: Fable 설계 → Sonnet 구현 → Fable 최종 검수
  (검수에서 reduced-motion `animation:none`이 캐스케이드 후순위에 덮이는 설계 실수 1건 수정).
- 조사 중 발견한 미배포 2건 포함 **전체 배포 실행**(`firebase deploy --only hosting,database`, 자격증명은
  8/4 로그인 잔존분): ① 8/7 릴 스핀 연출 ② 8/3 랭킹 페이지+DB 규칙 — **랭킹 규칙이 미배포 상태라
  jackpotrank 읽기/쓰기가 계속 거부되고 있었다**(앱 점수 제출도 그간 실패했을 것). 배포 후 라이브에서
  reel.js/ranking 200, jackpotrank 읽기 200, 데모 흐름·모달·릴 오버레이 실클릭 검증 완료.
- 참고: 데모 모드는 설계상 실제 예약이 되지 않는다 — 실제 시작 예약은 카톡 "잭팟선택" 토큰 링크에서만.

## 2026-08-07 - 웹 잭팟픽: 🎲 랜덤 버튼을 유니티식 릴 스핀 연출로

- 요청 "유니티에 구현한 것처럼 위아래 줄 보여주기식으로 룰렛 돌려서 당첨되는 식으로" — 웹의 유일한 룰렛 성격 기능인 jackpotpick 랜덤 조합 버튼(기존: 즉시 적용)에 Unity `ReelView`(S13/S14) 연출을 이식. 파이프라인: Fable 설계 → Sonnet 구현 → Opus 검수 → Fable 최종 검수.
- `public/jackpotpick/reel.js` 신규: 세로 5칸 스트립(위2/중앙/아래2) 노치 스크롤 릴 3개(캐릭터·머신·장치) 오버레이. Unity 명시값 그대로 — 가속 0.25s(노치 0.16→0.06s), 유지 0.35s, 왼쪽부터 0.10s 스태거 감속(0.10/0.16/0.24s), 타깃은 D-3 공식으로 첫 감속 노치에 주입, +8px 오버슈트 착지, 이웃 칸 상시 노출(알파 .42/스케일 .88), 최고속 블러/확대(.75/1.04). 결과를 먼저 확정하고 연출이 그 결과에 착지하는 결정론적 구조(엔진과 동일 원칙). 탭/Esc 스킵, prefers-reduced-motion 즉시 표시, 이미지 프리로드+이모지 폴백.
- `app.js`: `applyReco` async화 — random 분기에서만 릴 연출 await 후 기존 적용 꼬리 실행(초보/고점/도전 추천은 기존과 동일하게 즉시). 릴 연출 중 재진입 가드.
- `pick.css`: 오버레이 섹션 추가(기존 CSS 변수 사용, 320px 뷰포트 대응).
- Opus 검수 반영 4건: 연출 예외 시 오버레이 영구 잠금 방어(settle 강제 스냅), reduced-motion 표시시간 분리, `loading="lazy"` 제거, 긴 이름 줄바꿈으로 인한 박스 튐 방지. 스태거 양자화·오버슈트 방향 지적은 Unity 원본과 동일 동작이라 기각.
- 검증: 노치 재활용·타깃 주입 산수 수기 전개(Opus)로 "마지막 감속 노치 종료 시 중앙=타깃" 확인, `node --check`(ES 모듈) 통과. 미배포 — `firebase deploy --only hosting --project jackpotrun-web` 은 사용자 확인 후.

## 2026-08-04 - 파티클 근본 수정 3건 + S16 스핀 결과 패널(GainPanel)

- **사용자 보고 "터지는 이펙트는 어디 갔나 · 그냥 텍스처 덩어리만 보인다"** — 파티클 미구현이 아니라 `FxPrefabGen`이 굽는 에셋 쪽 버그 3건이었다. 커밋 `527bfe8`.
  1. **텍스처가 머티리얼에 안 붙음**: `GenerateAll`이 `StartAssetEditing` 배치 안에서 PNG를 쓰고 곧바로 `LoadAssetAtPath`로 읽어 항상 null → `fx_add/fx_alpha`의 `_MainTex`가 빈 채로 저장. 텍스처 생성을 배치 밖으로 빼고 `Refresh` 후 재로드.
  2. **파생 머티리얼 참조 끊김**: `CloneWithTexture`의 메모리 상 `Material`은 `SaveAsPrefabAsset`에 저장되지 않아 렌더러 머티리얼이 None → 유니티 기본 파티클 머티리얼(텍스처 없는 흰 쿼드)로 대체됐다. `Art/FX/mats/*.mat` 에셋으로 저장(재실행 시 제자리 갱신 → GUID·프리팹 참조 보존).
  3. **단위 불일치 = "덩어리"의 정체**: `scalingMode=Local`은 부모 스케일을 무시해 설계의 캔버스px 수치를 월드 단위로 해석 — Screen Space-Camera 캔버스 `lossyScale`이 약 0.005라 크기 8이 **화면 두 배 크기의 흰 사각형**이 됐다 → `Hierarchy`. 같은 이유로 `gravityModifier`(월드 가속)는 입자를 0.25초에 화면 밖 수천 px로 날려 긴 줄무늬만 남겼다 → `forceOverLifetime`(Local space) 기반 `SetGravityPx`로 교체. 트레일 `minVertexDistance` 4→0.02.
  - 검증: 프리팹 39개 렌더러 전부 텍스처 있는 머티리얼 + Hierarchy, 플레이 모드 `Simulate(0.25s)` 후 카메라 렌더 캡처로 실제 입자 형태(컨페티·코인·파편·스파클) 육안 확인.
- **S16 스핀 결과 패널** (설계 ENGINE_PORT_DESIGN.md S16, 사용자 피드백 "로그 나열 말고 얼마나·왜 얻었는지"). 파이프라인: Fable 설계 → Sonnet 구현 → Fable 최종 검수. 커밋 `8486911`.
  - `UI2/Run/GainPanel.cs` 신규: 획득 대문짝(+N EXP, 0.35s 카운트업 + OutBack 팝인) · 점수/코인 칩(0이면 숨김) · 기여 내역 최대 6줄(0.05s 스태거) · 세트 설명 박스. **표시 전용 분해** — 심볼 기본/세트/해골/배율/가산으로 나누고 남는 차이는 `기타` 줄로 드러낸다(오차 은폐 금지), 최종 수치는 항상 엔진 값 그대로.
  - `RunView`: `resultLineText`/`ScorePopupRoutine` 제거 → `gainPanel.Show/Clear`. `NotesFeed`: 3줄로 축소 + astral 이모지 한글 치환(레거시 Text가 서로게이트 쌍을 못 그려 `🔥 EXP +50%`가 앞이 빈 채로 보이던 문제).
  - **Fable 최종 검수에서 잡은 것 2건**: ① GainPanel에 flex를 준 첫 구현이 잔여 공간을 통째로 먹어 세트 박스와 로그 사이에 화면 중앙 검은 구멍이 생김 → 잔여를 릴 섹션(flex 1)과 조작부 위 스페이서(flex 1)로 반씩 분배. ② `fx_exp_gain`이 `ZeroVelocityXZ`를 잘못 호출해 방금 넣은 x 속도를 도로 지우고 y만 다른 모드로 남겨 "Particle Velocity curves must all be in the same mode" 경고가 상시 발생 → `ZeroVelocityYZ` 추가, 콘솔 경고 0.
  - 검증: 컴파일 0오류 · 씬 재빌드 · 플레이 모드 실제 스핀에서 `+12 EXP = 체리×2 +6 · 보석×1 +1 · 세트 2연속 +8 · 해골×1 -3` 합 일치, 로그 3줄 한글 정상 표기.

## 2026-08-03 - S16 증강 오퍼 티어 폴백 + 폭탄 폭발 연출

- **버그(사용자 보고 "증강 골랐는데 그냥 지나감") 원인 확정**: BASE 퍼크 22종 전부 SILVER + 클리어 스테이지 %3==0 → GOLD 강제 → 신규 프로필의 게이트 풀에 GOLD 0개 → `PickPerksByTier` 오퍼가 통째로 비어 증강/유물 노드가 **랜덤 EVENT로 조용히 대체**(Kotlin 원본 동일 로직 — 봇은 계정이 장기 성장해 미노출, 단독 앱은 상시 재현).
- **수정 [원본 이탈 — Fable 승인]**: `Shop.PickPerksByTier` 티어 풀 소진 시 잔여 후보 전체(any tier) 폴백 1줄(PickAugments 기존 패턴과 동일). 정상 경로 RNG·결과 완전 불변(Opus 전수 확인). 전 풀 소진 → EVENT 경로는 보존. 회귀 테스트 3건 추가(`Tests_S4_TierPoolFallback` — GOLD 강제 시 PERK_OFFER 보장 ×증강/유물, 전 풀 소진 시 기존 EVENT 폴백 보존).
- **폭탄 연출(사용자 요청)**: `SpinResult.rawCells`(Evaluate 입력 스냅샷, [표시 전용 — 밸런스 무관]) 추가 → ReelView가 **원시 심볼로 착지 → 0.25s 대기 → 폭탄 인접 칸 FxId.Skull 주황 버스트 + 1→1.18→0 InBack 펀치 + 폭탄 칸 0.94→1 펀치 + 셰이크(±6px) → 빈칸 전환**(칸별 0.05s 스태거). 자석 복사 칸은 0.12s 팝 스왑. 변형 없는 스핀은 추가 지연 0. `UiTween.Ease.InBack` 신규(OutBack 대칭).
- **Opus 검수**: 치명 0 · 중요 1(변형 목록 주석/설계 부정확 — Evaluate 내부 변형은 💥·🧲 2종뿐, 👑/🧽/🌀/🌱→는 이전 단계에서 raw에 반영. 문서·주석 정정) · 경미 5(폭탄 칸 펀치 시작 스케일 명시 대입 반영, 테스트 `?.tier` 방어 반영, PRISM 전량 보유 케이스 폴백 문서화, 셰이크 타이밍은 유지 판단).
- **검증**: EngineTests **17,832 통과(+13, 골든값 무수정)** · csc 0오류 · 에디터 플레이 주입 재생으로 폭탄 연출 확인.
- 특이사항: 검증 중 에디터가 플레이 진입 도메인 리로드에서 35분 데드락(`Begin MonoManager ReloadAssembly`에서 정지) — 강제 종료 후 재시작으로 복구. 씬·스프라이트 meta 등 디스크 저장분 유실 없음.

## 2026-08-03 - S15 글로벌 랭킹 (Firebase RTDB, 앱+웹)

- **Firebase 확인**: 콘솔 표시명이 "JackpotRun"으로 바뀌었으나 **프로젝트 ID는 `jackpotrun-web` 불변**(표시명 변경은 ID/URL/키에 무영향 — RTDB·호스팅 실측, `jackpotrun` ID는 전 리전 404). 코드는 기존 URL 유지, 프로젝트 이전 시 `RankingService.DbUrl`·웹 config 두 곳만 교체.
- **파이프라인**: Fable 설계(ENGINE_PORT_DESIGN.md S15) → Sonnet 2병렬(Unity/웹) → Opus 검수 → Fable 반영. Opus 검수: 치명 1(행 자식 경로 계약 — HGroup이 중간 노드라 `row.Find("RankNo")` 전부 null → `"Content/…"` 계약으로 통일, Dex 선례) · 중요 2(씬 리빌드 필요, astral 이모지 미렌더 → 1~3위 금은동 **색상 숫자**로 대체) · 경미 8(MiniJson `catch(Exception)` 확대, `CompleteLogin`에도 제출 훅(닉 변경 즉시 반영), 주석 정리 등 반영).
- **Unity 앱**: `Game/MiniJson.cs`(순수 C# JSON 파서) · `Game/RankingService.cs`(RTDB REST — `jackpotrank/$pid` PUT/GET, PlayerPrefs pid GUID·제출 캐시로 중복 PUT 방지, 실패 시 다음 Intro 진입에서 자동 재시도) · `UI2/RankView.cs`(상위 100, 내 행 CardTop+골드 닉 강조) · ScreenRouter `Rank` + AppRoot `ShowRank`/RegisterIntro 제출 훅 + MenuView 랭킹 버튼 실연결(토스트 제거) + UiSceneBuilder `BuildRankScreen` + **Intro 씬 리빌드 완료**.
- **웹**: `public/ranking/`(index/app/style — 동일 정렬 score↓·ts↑, `_hesc` XSS 이스케이프, 상위 100, 날짜 표기) · `firebase.json` `/ranking/**` no-cache · `database.rules.json` `jackpotrank`(read 공개, `$pid` 검증 쓰기, indexOn score, `$other` 차단).
- **검증**: csc 오프라인 컴파일 0오류 · 에디터 컴파일/씬 리빌드 0오류 · 플레이 스모크 실클릭 경로(타이틀→메뉴→랭킹→백버튼) — 오류 상태 문구(규칙 미배포라 정상) 및 **성공 경로**(가짜 5건 주입: 행 렌더·점수 표기 `52,340 · S15`·내 행 강조) 스크린샷 확인, 콘솔 오류·경고 0. 참고: 비포커스 화면 겹침은 기지의 runInBackground=false 현상(코드 버그 아님).
- ⏳ **남은 것(사용자 실행 필요)**: `firebase deploy --only hosting,database --project jackpotrun-web` — 이 PC엔 firebase CLI 없음. **규칙 배포 전에는 앱·웹 랭킹이 "불러오지 못했어요" 문구를 띄우는 게 정상**(RTDB 기본 거부).

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
