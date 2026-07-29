# 잭팟런(Jackpot Run) 이미지 리소스 추출본 — Unity 이관용

카카오봇 잭팟런 게임에 실제로 쓰이는 이미지 290장 + 대응 게임 데이터 294건을 한곳에 모은 패키지.
추출일: 2026-07-29

---

## 1. 폴더 구성

```
JackpotRun_UnityAssets\
├─ README.md          ← 이 파일
├─ manifest.json      ← 전체 메타데이터 (Unity 임포터/ScriptableObject 생성용, 권장)
├─ manifest.csv       ← 같은 내용의 표 (Excel용, UTF-8 BOM)
├─ prompts.json       ← 원본 AI 이미지 생성 프롬프트 (재생성/고해상도용)
├─ regen_missing.ps1  ← 이미지 없는 장치 4종 생성 스크립트 (미실행 상태)
└─ Sprites\
   ├─ Characters\   char_*.png    16장  (캐릭터)
   ├─ Machines\     mac_*.png     16장  (슬롯머신)
   ├─ Devices\      dev_*.png     12장  (장치, 카탈로그는 16종 → 4종 아트 없음)
   ├─ Augments\     aug_*.png     80장  (증강)
   ├─ Relics\       rel_*.png     61장  (유물)
   ├─ Curses\       cur_*.png     16장  (저주)
   ├─ Items\        item_*.png    73장  (소모 아이템)
   └─ Achievements\ ach_*.png     16장  (업적)
                                 ─────
                                 290장
```

## 2. 이미지 사양

| 항목 | 값 |
|---|---|
| 포맷 | PNG (RGB, 알파 없음) |
| 해상도 | **256 × 256 전부 동일** |
| 배경 | 어두운 스튜디오 그라데이션 (불투명, 누끼 아님) |
| 총 용량 | 3.1 MB |
| 생성 방식 | pollinations.ai 텍스트→이미지 (프롬프트는 `prompts.json`) |

> **Unity 임포트 시 주의**
> - 256px는 카톡 도감용이라 모바일 카드 UI엔 맞지만 확대하면 뭉갭니다. 고해상도가 필요하면 3절 참고.
> - 배경이 불투명이라 아이콘으로 쓰려면 배경 제거 또는 카드 프레임 안에 넣는 연출이 필요합니다.
> - 권장 임포트 설정: `Texture Type: Sprite (2D and UI)` / `Sprite Mode: Single` / `Pixels Per Unit: 256` / `Filter: Bilinear` / `Compression: High Quality` / `Max Size: 256` (원본 이상 무의미).

## 3. 메타데이터 (`manifest.json`)

`entries[]` 각 항목 필드:

| 필드 | 설명 |
|---|---|
| `id` | 이미지 파일명과 동일한 고유 키 (예: `aug_all_in`) — 스프라이트 로딩 키로 그대로 사용 |
| `category` / `categoryLabel` | `char`/`mac`/`dev`/`aug`/`rel`/`cur`/`item`/`ach` + 한글 라벨 |
| `key` | 접두사를 뗀 원본 게임 ID (Kotlin 엔진의 ID와 일치) |
| `emoji` | 게임 내 표기 이모지 (이미지 로딩 실패 시 폴백으로 쓰이던 값) |
| `nameKo` / `descKo` | 한글 이름 / 효과 설명 (게임 밸런스 원문) |
| `tier` | 증강·유물·저주 등급 `SILVER` / `GOLD` / `PRISM` |
| `price` | 유물 상점가 (코인) |
| `coinCost` / `itemKind` | 아이템 가격 / 지속시간 종류 `NEXTSPIN`·`PHASE`·`INSTANT` |
| `deviceKind` / `command` / `rare` / `cooldown` / `unlockAch` | 장치 전용 필드 |
| `scoreMod` / `unlockReq` | 머신 점수보정 / 캐릭·머신 해금 조건 `[[스탯키, 필요값], ...]` |
| `pick` | 선택화면 큐레이션 메타 — 난이도 `diff`, 고점 `ceiling`, 안정성 `stab`, 위험 `risk`, `tags`, `pros`, `cons`, `build`, `unlock` (캐릭터·머신·장치 44건) |
| `sprite` | 이 패키지 기준 상대 경로 |
| `aiPrompt` | 이미지 생성에 쓰인 영문 프롬프트 전문 (접미 스타일 포함) |

Unity에서는 `manifest.json`을 에디터 스크립트로 읽어 카테고리별 ScriptableObject를 자동 생성하는 방식이 가장 빠릅니다. `id`가 파일명과 1:1이라 `Resources.Load<Sprite>` 또는 Addressables 키로 직결됩니다.

## 4. 알려진 결손 — 장치 4종

카탈로그에는 있으나 **이미지가 애초에 생성된 적 없는** 항목입니다 (원본 프로젝트에서도 이모지로만 표시됨).

| id | 이모지 | 이름 | 효과 |
|---|---|---|---|
| `dev_holdfile` | 🗂️ | 보류파일 | 증강 후보 1개 보관 → 다음 증강 노드에서 비교 |
| `dev_major` | 🎓 | 전공신청서 | 주력 계열 증강 등장확률 소폭↑ |
| `dev_retake` | 🔁 | 재시험관 | 증강 선택지 코인 소모로 재추첨 (스테이지당 1회) |
| `dev_syllabus` | 📋 | 강의계획서 | 증강/유물 선택 시 예상 티어 사전 안내 |

`regen_missing.ps1` 에 기존 12종과 같은 화풍의 프롬프트를 미리 넣어 두었습니다. 실행하면 pollinations.ai로 외부 요청이 나가므로 **자동 실행하지 않았습니다.** 필요할 때 직접 돌리세요.

## 5. 출처 및 원본 위치

- 이미지 원본: `C:\dev\KakaoOpenChatBot\web\jackpotdex\img\` (배포본 `C:\dev\JackpotRunWeb\public\jackpotdex\img\` 와 바이트 단위 동일 — 확인 완료)
- 게임 데이터: `app\src\main\kotlin\com\ashersoft\kakaobot\game\SlotV2Engine.kt` (증강/유물/저주/아이템/장치/머신/캐릭터)
- 업적 표기: `web\jackpotdex\app.js` 정적 카탈로그
- 큐레이션 메타: `web\jackpotpick\meta.js`

원본 프로젝트 파일은 **하나도 수정되지 않았습니다.** 이 폴더는 순수 복사본입니다.

## 6. 라이선스 참고

이미지는 pollinations.ai 로 생성된 AI 이미지입니다. 상업 배포 전에는 해당 서비스의 이용 약관과 생성물 권리 조항을 확인하시고, 필요하면 `prompts.json`의 프롬프트를 권리관계가 명확한 다른 생성기/외주로 다시 태워 교체하는 편이 안전합니다.
