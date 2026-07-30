# 03_meta.md — 잭팟런 메타 시스템 사양 (업적/면허/영속 상태/RTDB)

**원본: kotlin-reference @ 커밋 c73452c, 추출일 2026-07-30**

> 본 문서는 아래 4개 Kotlin 원본 파일을 라인 단위로 대조하여 작성한 C# 이식용 1차 사양이다.
> 수치(threshold, TTL, 인덱스 등)는 원본 그대로이며 반올림·요약·해석을 하지 않았다.
> - `kotlin-reference\game\SlotV2AchievementsExt.kt` (작업 지시 631줄 표기, 실측 683줄)
> - `kotlin-reference\data\SlotV2Entities.kt` (작업 지시 122줄 표기, 실측 131줄)
> - `kotlin-reference\data\SlotV2Dao.kt` (작업 지시 54줄 표기, 실측 68줄)
> - `kotlin-reference\game\SlotV2WebService.kt` (작업 지시 304줄 표기, 실측 336줄)
>
> 위 4개 파일에는 `SlotV2Engine.kt`(업적 정의 클래스 `SlotV2Engine.Achievement`, `DEVICES`, `CHARS`, `MACHINES`,
> `SCHOOL_RESEARCH`, `THEME_BUILDS`, `composeStat`, `achCounter`, `reqProgress`, `allChallenges` 등 실제 판정 로직)와
> `SlotV2Service.kt`(스탯 증가 시점, `handleSpin`/`clearStage`/`launchRun`/`addAch4ClearTracking`/`gameOver` 등)가
> **포함되어 있지 않다.** 이 문서는 그 두 파일을 소스로 하지 않으므로, "판정식(AND 조합)"과 "증가 호출 시점"은
> `SlotV2AchievementsExt.kt` 내 주석에 적힌 원문을 그대로 인용하는 방식으로만 기술했다. 정확한 판정/증가 구현이
> 필요하면 `SlotV2Engine.kt`/`SlotV2Service.kt` 추출이 추가로 필요하다.

---

## 1. 업적 전체 목록

`SlotV2AchievementsExt.LIST` (game\SlotV2AchievementsExt.kt:5-681)는 `SlotV2Engine.Achievement` 데이터클래스의
리스트다. 필드: `id, emoji, name, key, threshold, desc, cat, tier, reward, hidden(기본 false)`.
파일 3줄 주석 원문: "잭팟런 v2 확장 업적 — 기본 16 외 추가분. SlotV2Engine.ACHIEVEMENTS 에 합쳐진다." →
**아래 466개는 "확장분"이며, `SlotV2Engine.kt`(미포함)에 정의된 기본 16개 업적은 별도로 존재한다.**
즉 잭팟런 v2 전체 업적 수는 최소 466 + 16 = 482개 이상이다(기본 16개의 실제 필드는 미확인).

### 1.1 요약 통계 (확장분 466개 기준, 파싱 스크립트로 전수 검증 — id 중복 0건 확인)

카테고리(cat)별 개수:

| 카테고리(cat) | 개수 |
|---|---|
| 캐릭터숙련 | 64 |
| 머신숙련 | 64 |
| 심볼 | 31 |
| 연구 | 30 |
| 히든 | 28 |
| 경제 | 25 |
| 보스공략 | 25 |
| 장치면허 | 24 |
| 특수심볼 | 22 |
| 제한도전 | 20 |
| 빌드도감 | 16 |
| 명령어 | 12 |
| 면허 | 12 |
| 반복 | 11 |
| 고점 | 8 |
| 빌드 | 8 |
| 장치 | 7 |
| 역전 | 7 |
| 입문 | 6 |
| 클리어 | 6 |
| 유물 | 5 |
| 아슬아슬 | 5 |
| 아이템 | 4 |
| 상점 | 4 |
| 점수 | 4 |
| 저주 | 4 |
| 도전 | 4 |
| 정밀 | 4 |
| 잭팟 | 4 |
| 보스 | 2 |
| **합계** | **466** |

티어(tier)별 개수:

| tier | 개수 |
|---|---|
| 브론즈 | 70 |
| 실버 | 103 |
| 골드 | 163 |
| 프리즘 | 130 |

히든(hidden=true) 개수: **28개** / 전체 466개. 카탈로그 노드(4.4절)에는 히든 여부와 무관하게 전체 필드가
그대로 push되므로("클라 마스킹은 별도" 주석, game\SlotV2WebService.kt:226 인근), 은닉 렌더링은 클라이언트 책임이다.

고유 스탯 키(key) 개수: **156개** (5장 "스탯 키 사전" 참조).

### 1.2 전체 목록 (원문 그대로, 466행)

열 의미: `#`=목록 순번(파일 내 등장 순서) · `id`=업적 식별자 · `emoji` · `이름`=name · `key`=판정에 쓰는 stat 키 ·
`threshold`=달성 임계값(원본 정수 그대로) · `cat`=카테고리 · `tier`=브론즈/실버/골드/프리즘 · `hidden`=히든 여부 ·
`reward`=보상 원문 · `desc`=달성 조건 설명 원문.

| # | id | emoji | 이름 | key | threshold | cat | tier | hidden | reward | desc |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | intro_firstSpin | 🎰 | 첫 스핀 | totalSpins | 1 | 입문 | 브론즈 | false | 도감 등록 | 처음으로 슬롯을 돌렸다 |
| 2 | intro_firstRun | 🏫 | 입학식 | runs | 1 | 입문 | 브론즈 | false | 칭호: 신입생 | 첫 런을 시작했다 |
| 3 | intro_firstBoss | 👹 | 첫 보스 격파 | bossClears | 1 | 입문 | 브론즈 | false | 도감 등록 | 보스를 처음 클리어했다 |
| 4 | intro_firstStage5 | 🪜 | 5층 도달 | bestStage | 5 | 입문 | 브론즈 | false | 칭호: 초보 모험가 | 5스테이지까지 도달했다 |
| 5 | intro_firstShop | 🛒 | 첫 장보기 | shopBuys | 1 | 입문 | 브론즈 | false | 도감 등록 | 상점에서 처음 구매했다 |
| 6 | intro_firstDevice | ⚙️ | 장치 입문 | deviceUses | 1 | 입문 | 브론즈 | false | 도감 등록 | 장치를 처음 사용했다 |
| 7 | cherry1000 | 🍒 | 체리 농장주 | cherryTotal | 1000 | 심볼 | 골드 | false | 칭호: 체리 농장주 | 🍒체리 누적 1000개 등장 |
| 8 | cherry30 | 🍒 | 체리 새싹 | cherryTotal | 30 | 심볼 | 브론즈 | false | 도감 등록 | 🍒체리 누적 30개 등장 |
| 9 | cherry300 | 🍒 | 체리 과수원 | cherryTotal | 300 | 심볼 | 실버 | false | 🎭캐릭터 해금 힌트: 체리농부 | 🍒체리 누적 300개 등장 |
| 10 | cherry3000 | 🍒 | 체리 제국 | cherryTotal | 3000 | 심볼 | 프리즘 | false | 칭호: 체리 황제 | 🍒체리 누적 3000개 등장 |
| 11 | book30 | 📖 | 책장 정리 | bookTotal | 30 | 심볼 | 브론즈 | false | 도감 등록 | 📖책 누적 30개 등장 |
| 12 | book100 | 📖 | 다독가 | bookTotal | 100 | 심볼 | 실버 | false | 칭호: 다독가 | 📖책 누적 100개 등장 |
| 13 | book300 | 📖 | 서재의 주인 | bookTotal | 300 | 심볼 | 골드 | false | 칭호: 서재의 주인 | 📖책 누적 300개 등장 |
| 14 | book500 | 📖 | 장서가 | bookTotal | 500 | 심볼 | 골드 | false | 칭호: 장서가 | 📖책 누적 500개 등장 |
| 15 | book1000 | 📖 | 도서관장 | bookTotal | 1000 | 심볼 | 프리즘 | false | 칭호: 도서관장 | 📖책 누적 1000개 등장 |
| 16 | star30 | ⭐ | 별 줍기 | starTotal | 30 | 심볼 | 브론즈 | false | 도감 등록 | ⭐별 누적 30개 등장 |
| 17 | star100 | ⭐ | 별 수집가 | starTotal | 100 | 심볼 | 실버 | false | 칭호: 별 수집가 | ⭐별 누적 100개 등장 |
| 18 | star300 | ⭐ | 별자리 화가 | starTotal | 300 | 심볼 | 골드 | false | 칭호: 별자리 화가 | ⭐별 누적 300개 등장 |
| 19 | star500 | ⭐ | 밤하늘의 주인 | starTotal | 500 | 심볼 | 골드 | false | 칭호: 밤하늘의 주인 | ⭐별 누적 500개 등장 |
| 20 | star1000 | ⭐ | 은하 수집가 | starTotal | 1000 | 심볼 | 프리즘 | false | 칭호: 은하 수집가 | ⭐별 누적 1000개 등장 |
| 21 | gem30 | 💎 | 원석 줍기 | gemTotal | 30 | 심볼 | 브론즈 | false | 도감 등록 | 💎보석 누적 30개 등장 |
| 22 | gem100 | 💎 | 보석 세공사 | gemTotal | 100 | 심볼 | 실버 | false | 칭호: 보석 세공사 | 💎보석 누적 100개 등장 |
| 23 | gem300 | 💎 | 보석상 | gemTotal | 300 | 심볼 | 골드 | false | 칭호: 보석상 | 💎보석 누적 300개 등장 |
| 24 | gem500 | 💎 | 보석 감정사 | gemTotal | 500 | 심볼 | 골드 | false | 칭호: 보석 감정사 | 💎보석 누적 500개 등장 |
| 25 | gem1000 | 💎 | 다이아 광맥 | gemTotal | 1000 | 심볼 | 프리즘 | false | 칭호: 다이아 광맥 | 💎보석 누적 1000개 등장 |
| 26 | crown30ext | 👑 | 왕관 보관소 | crownTotal | 30 | 심볼 | 실버 | false | 도감 등록 | 👑왕관 누적 30개 등장 |
| 27 | crown100 | 👑 | 대관식 | crownTotal | 100 | 심볼 | 골드 | false | 칭호: 즉위한 자 | 👑왕관 누적 100개 등장 |
| 28 | crown300 | 👑 | 왕가의 보고 | crownTotal | 300 | 심볼 | 프리즘 | false | 칭호: 왕중왕 | 👑왕관 누적 300개 등장 |
| 29 | skull30 | 💀 | 해골 친구 | skullTotal | 30 | 심볼 | 브론즈 | false | 도감 등록 | 💀해골 누적 30개 등장 |
| 30 | skull100 | 💀 | 해골 수집가 | skullTotal | 100 | 심볼 | 실버 | false | 칭호: 해골 수집가 | 💀해골 누적 100개 등장 |
| 31 | skull300 | 💀 | 납골당지기 | skullTotal | 300 | 심볼 | 골드 | false | 칭호: 납골당지기 | 💀해골 누적 300개 등장 |
| 32 | skull1000 | 💀 | 죽음의 군주 | skullTotal | 1000 | 심볼 | 프리즘 | false | 칭호: 죽음의 군주 | 💀해골 누적 1000개 등장 |
| 33 | coin30 | 🪙 | 동전 줍기 | coinTotal | 30 | 심볼 | 브론즈 | false | 도감 등록 | 🪙코인 누적 30개 등장 |
| 34 | coin100 | 🪙 | 저금통 | coinTotal | 100 | 심볼 | 실버 | false | 칭호: 저금왕 | 🪙코인 누적 100개 등장 |
| 35 | coin300 | 🪙 | 환전상 | coinTotal | 300 | 심볼 | 골드 | false | 칭호: 환전상 | 🪙코인 누적 300개 등장 |
| 36 | coin500 | 🪙 | 금고지기 | coinTotal | 500 | 심볼 | 골드 | false | 칭호: 금고지기 | 🪙코인 누적 500개 등장 |
| 37 | coin1000 | 🪙 | 조폐국장 | coinTotal | 1000 | 심볼 | 프리즘 | false | 칭호: 조폐국장 | 🪙코인 누적 1000개 등장 |
| 38 | cmd_focus1 | 🎯 | 집중 입문 | focusUses | 1 | 명령어 | 브론즈 | false | 도감 등록 | 집중 명령을 처음 사용 |
| 39 | cmd_focus10 | 🎯 | 집중의 달인 | focusUses | 10 | 명령어 | 실버 | false | 칭호: 집중의 달인 | 집중 명령 10회 사용 |
| 40 | cmd_focus50 | 🎯 | 무아지경 | focusUses | 50 | 명령어 | 골드 | false | 칭호: 무아지경 | 집중 명령 50회 사용 |
| 41 | cmd_allin1 | 💥 | 첫 올인 | allinWins | 1 | 명령어 | 브론즈 | false | 도감 등록 | 올인 스핀을 처음 승리 |
| 42 | cmd_allin5 | 💥 | 올인 전문가 | allinWins | 5 | 명령어 | 실버 | false | 칭호: 올인 전문가 | 올인 스핀 5회 승리 |
| 43 | cmd_allin20 | 💥 | 도박의 신 | allinWins | 20 | 명령어 | 골드 | false | 칭호: 도박의 신 | 올인 스핀 20회 승리 |
| 44 | cmd_pray1 | 🙏 | 첫 기도 | prayClears | 1 | 명령어 | 실버 | false | 도감 등록 | 기도 후 스테이지를 클리어 |
| 45 | cmd_pray5 | 🙏 | 기적의 증인 | prayClears | 5 | 명령어 | 골드 | false | 칭호: 기적의 증인 | 기도 후 클리어 5회 |
| 46 | cmd_last1 | ⏳ | 최후의 한 수 | lastUses | 1 | 명령어 | 실버 | false | 도감 등록 | 최후 명령을 처음 사용 |
| 47 | cmd_last10 | ⏳ | 벼랑 끝의 명수 | lastUses | 10 | 명령어 | 골드 | false | 칭호: 벼랑 끝의 명수 | 최후 명령 10회 사용 |
| 48 | cmd_reroll1 | 🔄 | 재굴림 입문 | rerollUses | 1 | 명령어 | 브론즈 | false | 도감 등록 | 재굴림을 처음 사용 |
| 49 | cmd_pin1 | 📌 | 고정 입문 | pinUses | 1 | 명령어 | 브론즈 | false | 도감 등록 | 고정을 처음 사용 |
| 50 | dev_use10 | ⚙️ | 장치 애호가 | deviceUses | 10 | 장치 | 실버 | false | 칭호: 장치 애호가 | 장치를 10회 사용 |
| 51 | dev_use50 | ⚙️ | 기계공 | deviceUses | 50 | 장치 | 골드 | false | 칭호: 기계공 | 장치를 50회 사용 |
| 52 | dev_own1 | 🔧 | 첫 장치 보유 | devicesOwned | 1 | 장치 | 브론즈 | false | 도감 등록 | 장치를 1종 영구 보유 |
| 53 | dev_own5 | 🔧 | 장치 수집가 | devicesOwned | 5 | 장치 | 골드 | false | 칭호: 장치 수집가 | 장치를 5종 영구 보유 |
| 54 | dev_own12 | 🔧 | 장치 마스터 | devicesOwned | 12 | 장치 | 프리즘 | false | 칭호: 장치 마스터 | 장치를 12종 모두 보유 |
| 55 | dev_reroll10 | 🔄 | 재굴림 중독 | rerollUses | 10 | 장치 | 실버 | false | 칭호: 재굴림 중독 | 재굴림 10회 사용 |
| 56 | dev_pin10 | 📌 | 고정의 달인 | pinUses | 10 | 장치 | 실버 | false | 칭호: 고정의 달인 | 고정 10회 사용 |
| 57 | relic3 | 🏺 | 유물 수집 시작 | relicsMax | 3 | 유물 | 브론즈 | false | 도감 등록 | 한 런에 유물 3개 동시 보유 |
| 58 | relic5 | 🏺 | 유물 애호가 | relicsMax | 5 | 유물 | 실버 | false | 칭호: 유물 애호가 | 한 런에 유물 5개 동시 보유 |
| 59 | relic10 | 🏺 | 유물 수집광 | relicsMax | 10 | 유물 | 골드 | false | 칭호: 유물 수집광 | 한 런에 유물 10개 동시 보유 |
| 60 | prismPick1 | 🔮 | 첫 프리즘 유물 | prismPicks | 1 | 유물 | 실버 | false | 도감 등록 | 프리즘 증강을 처음 선택 |
| 61 | prismPick20 | 🔮 | 프리즘 마니아 | prismPicks | 20 | 유물 | 프리즘 | false | 칭호: 프리즘 마니아 | 프리즘 증강 20회 선택 |
| 62 | item1 | 🎒 | 첫 아이템 | itemsUsed | 1 | 아이템 | 브론즈 | false | 도감 등록 | 아이템을 처음 사용 |
| 63 | item10 | 🎒 | 아이템 애용가 | itemsUsed | 10 | 아이템 | 실버 | false | 칭호: 아이템 애용가 | 아이템 10회 사용 |
| 64 | item50 | 🎒 | 만물상 단골 | itemsUsed | 50 | 아이템 | 골드 | false | 칭호: 만물상 단골 | 아이템 50회 사용 |
| 65 | item100 | 🎒 | 소비의 화신 | itemsUsed | 100 | 아이템 | 프리즘 | false | 칭호: 소비의 화신 | 아이템 100회 사용 |
| 66 | shop10 | 🛍️ | 단골 손님 | shopBuys | 10 | 상점 | 실버 | false | 칭호: 단골 손님 | 상점에서 10회 구매 |
| 67 | shop50 | 🛍️ | 큰손 | shopBuys | 50 | 상점 | 골드 | false | 칭호: 큰손 | 상점에서 50회 구매 |
| 68 | gamble1 | 🎲 | 첫 도박 | gambles | 1 | 상점 | 브론즈 | false | 도감 등록 | 도박장 노드를 처음 이용 |
| 69 | gamble10 | 🎲 | 도박장 VIP | gambles | 10 | 상점 | 골드 | false | 칭호: 도박장 VIP | 도박장 노드 10회 이용 |
| 70 | boss5ext | 👹 | 보스 사냥꾼 | bossClears | 5 | 클리어 | 실버 | false | 칭호: 보스 사냥꾼 | 보스 5회 클리어 |
| 71 | boss20 | 👹 | 보스 학살자 | bossClears | 20 | 클리어 | 골드 | false | 칭호: 보스 학살자 | 보스 20회 클리어 |
| 72 | stage10ext | 🪜 | 10층 등반가 | bestStage | 10 | 클리어 | 실버 | false | 칭호: 10층 등반가 | 10스테이지 도달 |
| 73 | stage15ext | 🪜 | 고지 점령 | bestStage | 15 | 클리어 | 골드 | false | 칭호: 고지 점령자 | 15스테이지 도달 |
| 74 | runs10 | 🔁 | 꾸준한 도전자 | runs | 10 | 클리어 | 브론즈 | false | 도감 등록 | 10런 플레이 |
| 75 | runs50 | 🔁 | 베테랑 | runs | 50 | 클리어 | 골드 | false | 칭호: 베테랑 | 50런 플레이 |
| 76 | close10 | 😅 | 아슬아슬 통과 | closeClears | 10 | 아슬아슬 | 실버 | false | 칭호: 아슬아슬 | 잔여 EXP 10이하로 10회 클리어 |
| 77 | close30 | 😅 | 줄타기 곡예사 | closeClears | 30 | 아슬아슬 | 골드 | false | 칭호: 줄타기 곡예사 | 잔여 EXP 10이하로 30회 클리어 |
| 78 | lastspin1 | 🎯 | 막판 뒤집기 | lastSpinClears | 1 | 아슬아슬 | 실버 | false | 도감 등록 | 마지막 스핀에 클리어 |
| 79 | lastspin5 | 🎯 | 끝내기의 명수 | lastSpinClears | 5 | 아슬아슬 | 골드 | false | 칭호: 끝내기의 명수 | 마지막 스핀 클리어 5회 |
| 80 | exact1ext | 🎯 | 딱 떨어지게 | exactClears | 1 | 아슬아슬 | 골드 | false | 도감 등록 | 요구치와 정확히 일치 클리어 |
| 81 | score5k | 📊 | 점수 입문 | bestScore | 5000 | 점수 | 브론즈 | false | 도감 등록 | 한 런 5000점 달성 |
| 82 | score10kext | 📊 | 만점 클럽 | bestScore | 10000 | 점수 | 실버 | false | 칭호: 만점 클럽 | 한 런 10000점 달성 |
| 83 | score30k | 📊 | 고득점자 | bestScore | 30000 | 점수 | 골드 | false | 칭호: 고득점자 | 한 런 30000점 달성 |
| 84 | score50kext | 📊 | 점수 사냥꾼 | bestScore | 50000 | 점수 | 골드 | false | 칭호: 점수 사냥꾼 | 한 런 50000점 달성 |
| 85 | curse1 | 🩸 | 첫 저주 | curseMax | 1 | 저주 | 브론즈 | false | 도감 등록 | 한 런에 저주 1개 보유 |
| 86 | curse3 | 🩸 | 저주받은 자 | curseMax | 3 | 저주 | 실버 | false | 칭호: 저주받은 자 | 한 런에 저주 3개 동시 보유 |
| 87 | curse5 | 🩸 | 저주 수집가 | curseMax | 5 | 저주 | 골드 | false | 칭호: 저주 수집가 | 한 런에 저주 5개 동시 보유 |
| 88 | curse7 | 🩸 | 저주의 그릇 | curseMax | 7 | 저주 | 프리즘 | false | 칭호: 저주의 그릇 | 한 런에 저주 7개 동시 보유 |
| 89 | chal_stage20 | 🏔️ | 정상 정복 | bestStage | 20 | 도전 | 프리즘 | false | 칭호: 정상 정복자 | 20스테이지 도달 |
| 90 | chal_boss50 | 💀 | 백전노장 | bossClears | 50 | 도전 | 프리즘 | false | 칭호: 백전노장 | 보스 50회 클리어 |
| 91 | chal_curse7 | ☠️ | 저주의 화신 | curseMax | 7 | 도전 | 프리즘 | false | 칭호: 저주의 화신 | 한 런에 저주 7개 동시 보유 |
| 92 | chal_score100k | 🏆 | 10만점 돌파 | bestScore | 100000 | 도전 | 프리즘 | false | 칭호: 10만점의 전설 | 한 런 100000점 달성 |
| 93 | spin100 | 🔂 | 백 번의 스핀 | totalSpins | 100 | 반복 | 브론즈 | false | 도감 등록 | 누적 100회 스핀 |
| 94 | spin500 | 🔂 | 오백 번의 스핀 | totalSpins | 500 | 반복 | 실버 | false | 칭호: 손가락 운동 | 누적 500회 스핀 |
| 95 | spin1000 | 🔂 | 천 번의 스핀 | totalSpins | 1000 | 반복 | 골드 | false | 칭호: 스핀 중독 | 누적 1000회 스핀 |
| 96 | spin5000 | 🔂 | 오천 번의 스핀 | totalSpins | 5000 | 반복 | 프리즘 | false | 칭호: 스핀의 화신 | 누적 5000회 스핀 |
| 97 | runs100 | 🔁 | 백 번의 도전 | runs | 100 | 반복 | 프리즘 | false | 칭호: 슬롯의 산증인 | 100런 플레이 |
| 98 | hid_exact5 | 🎯 | 딱 맞췄다 | exactClears | 5 | 히든 | 프리즘 | true | 칭호: 정밀 저격수 | 요구치와 정확히 일치 클리어 5회 |
| 99 | hid_close1 | 💔 | 심장 파괴자 | closeClears | 1 | 히든 | 실버 | true | 칭호: 심장 파괴자 | 잔여 EXP 10이하로 클리어 |
| 100 | hid_lastspin20 | 🃏 | 운명의 마지막 | lastSpinClears | 20 | 히든 | 프리즘 | true | 칭호: 운명의 카드 | 마지막 스핀 클리어 20회 |
| 101 | hid_jackpot1 | 🎉 | 첫 잭팟 | jackpots | 1 | 히든 | 골드 | true | 도감 등록 | 5개 일치 잭팟 달성 |
| 102 | hid_jackpot5 | 🎉 | 잭팟 단골 | jackpots | 5 | 히든 | 프리즘 | true | 칭호: 잭팟 메이커 | 잭팟 5회 달성 |
| 103 | hid_jackpot20 | 🎰 | 잭팟런의 주인 | jackpots | 20 | 히든 | 프리즘 | true | 칭호: 잭팟런의 주인 | 잭팟 20회 달성 |
| 104 | hid_curse7 | 🎓 | 검은 졸업식 | curseMax | 7 | 히든 | 프리즘 | true | 칭호: 검은 졸업생 | 저주 7개를 안고 살아남았다 |
| 105 | hid_score100k | 👾 | 졸업식의 괴물 | bestScore | 100000 | 히든 | 프리즘 | true | 칭호: 졸업식의 괴물 | 한 런 100000점의 괴물 |
| 106 | hid_set4_1 | 🍀 | 행운의 네 잎 | set4Plus | 1 | 히든 | 실버 | true | 도감 등록 | 같은 심볼 4개 이상 등장 |
| 107 | hid_set4_50 | 🍀 | 사천왕 | set4Plus | 50 | 히든 | 프리즘 | true | 칭호: 사천왕 | 같은 심볼 4개 이상 50회 |
| 108 | hid_allin20 | 🔥 | 불꽃의 도박사 | allinWins | 20 | 히든 | 프리즘 | true | 칭호: 불꽃의 도박사 | 올인 20회 승리의 광기 |
| 109 | hid_skull1000 | ⚰️ | 사신의 친구 | skullTotal | 1000 | 히든 | 프리즘 | true | 칭호: 사신의 친구 | 💀해골과 1000번 마주쳤다 |
| 110 | hid_prismPick5 | 🌈 | 무지개를 쫓는 자 | prismPicks | 5 | 히든 | 골드 | true | 칭호: 무지개를 쫓는 자 | 프리즘 증강 5회 선택 |
| 111 | hid_pray5 | ✨ | 신앙의 결실 | prayClears | 5 | 히든 | 프리즘 | true | 칭호: 신앙의 결실 | 기도가 모두 응답받았다 |
| 112 | cmast_novice_b | 🎒 | 첫 등교 | cstage_novice | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 초보학생(으)로 S5 도달 |
| 113 | cmast_novice_s | 🎒 | 성실한 신입 | cstage_novice | 10 | 캐릭터숙련 | 실버 | false | 칭호: 성실한 신입 | 초보학생(으)로 S10 도달 |
| 114 | cmast_novice_g | 🎒 | 모범 신입생 | cstage_novice | 15 | 캐릭터숙련 | 골드 | false | 프레임: 신입생의 가방 | 초보학생(으)로 S15 도달 |
| 115 | cmast_novice_p | 🎒 | 초심의 전설 | cstage_novice | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 초심을 잃지 않은 자 | 초보학생(으)로 S20 도달 |
| 116 | cmast_scholar_b | 📗 | 장학 입문 | cstage_scholar | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 장학생(으)로 S5 도달 |
| 117 | cmast_scholar_s | 📗 | 우등 장학생 | cstage_scholar | 10 | 캐릭터숙련 | 실버 | false | 칭호: 우등 장학생 | 장학생(으)로 S10 도달 |
| 118 | cmast_scholar_g | 📗 | 전액 장학생 | cstage_scholar | 15 | 캐릭터숙련 | 골드 | false | 프레임: 장학증서 | 장학생(으)로 S15 도달 |
| 119 | cmast_scholar_p | 📗 | 장학의 전설 | cstage_scholar | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 학문의 정점 | 장학생(으)로 S20 도달 |
| 120 | cmast_gambler_b | 🎲 | 도박 입문 | cstage_gambler | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 도박꾼(으)로 S5 도달 |
| 121 | cmast_gambler_s | 🎲 | 도박 숙련 | cstage_gambler | 10 | 캐릭터숙련 | 실버 | false | 칭호: 노련한 도박꾼 | 도박꾼(으)로 S10 도달 |
| 122 | cmast_gambler_g | 🎲 | 도박 졸업 | cstage_gambler | 15 | 캐릭터숙련 | 골드 | false | 프레임: 황금 주사위 | 도박꾼(으)로 S15 도달 |
| 123 | cmast_gambler_p | 🎲 | 도박의 전설 | cstage_gambler | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 운명을 건 자 | 도박꾼(으)로 S20 도달 |
| 124 | cmast_farmer_b | 🍒 | 텃밭 가꾸기 | cstage_farmer | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 체리농부(으)로 S5 도달 |
| 125 | cmast_farmer_s | 🍒 | 능숙한 농부 | cstage_farmer | 10 | 캐릭터숙련 | 실버 | false | 칭호: 능숙한 농부 | 체리농부(으)로 S10 도달 |
| 126 | cmast_farmer_g | 🍒 | 대농장주 | cstage_farmer | 15 | 캐릭터숙련 | 골드 | false | 프레임: 풍년의 화환 | 체리농부(으)로 S15 도달 |
| 127 | cmast_farmer_p | 🍒 | 체리의 전설 | cstage_farmer | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 풍요의 수호자 | 체리농부(으)로 S20 도달 |
| 128 | cmast_parttime_b | 🪙 | 첫 출근 | cstage_parttime | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 알바생(으)로 S5 도달 |
| 129 | cmast_parttime_s | 🪙 | 성실한 알바 | cstage_parttime | 10 | 캐릭터숙련 | 실버 | false | 칭호: 성실한 알바 | 알바생(으)로 S10 도달 |
| 130 | cmast_parttime_g | 🪙 | 에이스 직원 | cstage_parttime | 15 | 캐릭터숙련 | 골드 | false | 프레임: 우수사원 명패 | 알바생(으)로 S15 도달 |
| 131 | cmast_parttime_p | 🪙 | 알바의 전설 | cstage_parttime | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 시급의 제왕 | 알바생(으)로 S20 도달 |
| 132 | cmast_jeweler_b | 💎 | 세공 입문 | cstage_jeweler | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 보석상(으)로 S5 도달 |
| 133 | cmast_jeweler_s | 💎 | 숙련 세공사 | cstage_jeweler | 10 | 캐릭터숙련 | 실버 | false | 칭호: 숙련 세공사 | 보석상(으)로 S10 도달 |
| 134 | cmast_jeweler_g | 💎 | 보석 명장 | cstage_jeweler | 15 | 캐릭터숙련 | 골드 | false | 프레임: 보석 진열장 | 보석상(으)로 S15 도달 |
| 135 | cmast_jeweler_p | 💎 | 보석의 전설 | cstage_jeweler | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 원석의 지배자 | 보석상(으)로 S20 도달 |
| 136 | cmast_honor_b | 🎓 | 우등 입문 | cstage_honor | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 수석졸업생(으)로 S5 도달 |
| 137 | cmast_honor_s | 🎓 | 학년 수석 | cstage_honor | 10 | 캐릭터숙련 | 실버 | false | 칭호: 학년 수석 | 수석졸업생(으)로 S10 도달 |
| 138 | cmast_honor_g | 🎓 | 전체 수석 | cstage_honor | 15 | 캐릭터숙련 | 골드 | false | 프레임: 수석 졸업장 | 수석졸업생(으)로 S15 도달 |
| 139 | cmast_honor_p | 🎓 | 수석의 전설 | cstage_honor | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 졸업식의 주인공 | 수석졸업생(으)로 S20 도달 |
| 140 | cmast_cultist_b | 💀 | 입교 의식 | cstage_cultist | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 해골숭배자(으)로 S5 도달 |
| 141 | cmast_cultist_s | 💀 | 충실한 신도 | cstage_cultist | 10 | 캐릭터숙련 | 실버 | false | 칭호: 충실한 신도 | 해골숭배자(으)로 S10 도달 |
| 142 | cmast_cultist_g | 💀 | 교단의 사제 | cstage_cultist | 15 | 캐릭터숙련 | 골드 | false | 프레임: 해골 제단 | 해골숭배자(으)로 S15 도달 |
| 143 | cmast_cultist_p | 💀 | 숭배의 전설 | cstage_cultist | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 죽음을 섬기는 자 | 해골숭배자(으)로 S20 도달 |
| 144 | cmast_crowncol_b | 👑 | 수집 입문 | cstage_crowncol | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 왕관수집가(으)로 S5 도달 |
| 145 | cmast_crowncol_s | 👑 | 왕관 애호가 | cstage_crowncol | 10 | 캐릭터숙련 | 실버 | false | 칭호: 왕관 애호가 | 왕관수집가(으)로 S10 도달 |
| 146 | cmast_crowncol_g | 👑 | 왕관 명인 | cstage_crowncol | 15 | 캐릭터숙련 | 골드 | false | 프레임: 왕관 진열대 | 왕관수집가(으)로 S15 도달 |
| 147 | cmast_crowncol_p | 👑 | 수집의 전설 | cstage_crowncol | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 왕관의 지배자 | 왕관수집가(으)로 S20 도달 |
| 148 | cmast_minimalist_b | 🍃 | 비움 입문 | cstage_minimalist | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 미니멀리스트(으)로 S5 도달 |
| 149 | cmast_minimalist_s | 🍃 | 절제의 달인 | cstage_minimalist | 10 | 캐릭터숙련 | 실버 | false | 칭호: 절제의 달인 | 미니멀리스트(으)로 S10 도달 |
| 150 | cmast_minimalist_g | 🍃 | 비움의 미학 | cstage_minimalist | 15 | 캐릭터숙련 | 골드 | false | 프레임: 단순함의 잎새 | 미니멀리스트(으)로 S15 도달 |
| 151 | cmast_minimalist_p | 🍃 | 비움의 전설 | cstage_minimalist | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 무소유의 현자 | 미니멀리스트(으)로 S20 도달 |
| 152 | cmast_lucky_b | 🍀 | 행운 입문 | cstage_lucky | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 행운아(으)로 S5 도달 |
| 153 | cmast_lucky_s | 🍀 | 운수 좋은 날 | cstage_lucky | 10 | 캐릭터숙련 | 실버 | false | 칭호: 운수 좋은 자 | 행운아(으)로 S10 도달 |
| 154 | cmast_lucky_g | 🍀 | 행운의 화신 | cstage_lucky | 15 | 캐릭터숙련 | 골드 | false | 프레임: 네 잎 클로버 | 행운아(으)로 S15 도달 |
| 155 | cmast_lucky_p | 🍀 | 행운의 전설 | cstage_lucky | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 행운의 여신이 택한 자 | 행운아(으)로 S20 도달 |
| 156 | cmast_highroller_b | 💠 | 거래 입문 | cstage_highroller | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 큰손(으)로 S5 도달 |
| 157 | cmast_highroller_s | 💠 | 큰 거래상 | cstage_highroller | 10 | 캐릭터숙련 | 실버 | false | 칭호: 큰 거래상 | 큰손(으)로 S10 도달 |
| 158 | cmast_highroller_g | 💠 | VIP 큰손 | cstage_highroller | 15 | 캐릭터숙련 | 골드 | false | 프레임: VIP 카드 | 큰손(으)로 S15 도달 |
| 159 | cmast_highroller_p | 💠 | 큰손의 전설 | cstage_highroller | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 판을 흔드는 큰손 | 큰손(으)로 S20 도달 |
| 160 | cmast_monk_b | 🧘 | 수행 입문 | cstage_monk | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 수도승(으)로 S5 도달 |
| 161 | cmast_monk_s | 🧘 | 정진하는 자 | cstage_monk | 10 | 캐릭터숙련 | 실버 | false | 칭호: 정진하는 자 | 수도승(으)로 S10 도달 |
| 162 | cmast_monk_g | 🧘 | 해탈의 경지 | cstage_monk | 15 | 캐릭터숙련 | 골드 | false | 프레임: 깨달음의 후광 | 수도승(으)로 S15 도달 |
| 163 | cmast_monk_p | 🧘 | 수행의 전설 | cstage_monk | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 무념의 대선사 | 수도승(으)로 S20 도달 |
| 164 | cmast_alchemist_b | ⚗️ | 조합 입문 | cstage_alchemist | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 연금술사(으)로 S5 도달 |
| 165 | cmast_alchemist_s | ⚗️ | 능숙한 연금술 | cstage_alchemist | 10 | 캐릭터숙련 | 실버 | false | 칭호: 능숙한 연금술사 | 연금술사(으)로 S10 도달 |
| 166 | cmast_alchemist_g | ⚗️ | 현자의 돌 | cstage_alchemist | 15 | 캐릭터숙련 | 골드 | false | 프레임: 현자의 돌 | 연금술사(으)로 S15 도달 |
| 167 | cmast_alchemist_p | ⚗️ | 연금의 전설 | cstage_alchemist | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 만물의 변환자 | 연금술사(으)로 S20 도달 |
| 168 | cmast_daredevil_b | 😈 | 도전 입문 | cstage_daredevil | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 무모한도전(으)로 S5 도달 |
| 169 | cmast_daredevil_s | 😈 | 겁 없는 자 | cstage_daredevil | 10 | 캐릭터숙련 | 실버 | false | 칭호: 겁 없는 자 | 무모한도전(으)로 S10 도달 |
| 170 | cmast_daredevil_g | 😈 | 광기의 질주 | cstage_daredevil | 15 | 캐릭터숙련 | 골드 | false | 프레임: 불타는 뿔 | 무모한도전(으)로 S15 도달 |
| 171 | cmast_daredevil_p | 😈 | 무모함의 전설 | cstage_daredevil | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 한계를 비웃는 자 | 무모한도전(으)로 S20 도달 |
| 172 | cmast_prodigy_b | 🌟 | 재능 발현 | cstage_prodigy | 5 | 캐릭터숙련 | 브론즈 | false | 도감 등록 | 천재(으)로 S5 도달 |
| 173 | cmast_prodigy_s | 🌟 | 빛나는 영재 | cstage_prodigy | 10 | 캐릭터숙련 | 실버 | false | 칭호: 빛나는 영재 | 천재(으)로 S10 도달 |
| 174 | cmast_prodigy_g | 🌟 | 비범한 천재 | cstage_prodigy | 15 | 캐릭터숙련 | 골드 | false | 프레임: 천재의 별빛 | 천재(으)로 S15 도달 |
| 175 | cmast_prodigy_p | 🌟 | 천재의 전설 | cstage_prodigy | 20 | 캐릭터숙련 | 프리즘 | false | 고급 칭호: 시대를 앞선 천재 | 천재(으)로 S20 도달 |
| 176 | mmast_basic_b | 🎰 | 기본기 다지기 | mstage_basic | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 기본 머신으로 S5 도달 |
| 177 | mmast_basic_s | 🎰 | 정석 플레이어 | mstage_basic | 10 | 머신숙련 | 실버 | false | 칭호: 정석 플레이어 | 기본 머신으로 S10 도달 |
| 178 | mmast_basic_g | 🎰 | 표준의 달인 | mstage_basic | 15 | 머신숙련 | 골드 | false | 프레임: 클래식 슬롯 | 기본 머신으로 S15 도달 |
| 179 | mmast_basic_p | 🎰 | 기본의 전설 | mstage_basic | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 정석의 화신 | 기본 머신으로 S20 도달 |
| 180 | mmast_cherry_b | 🍒 | 체리 머신 입문 | mstage_cherry | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 체리 머신으로 S5 도달 |
| 181 | mmast_cherry_s | 🍒 | 체리 머신 숙련 | mstage_cherry | 10 | 머신숙련 | 실버 | false | 칭호: 체리 머신 숙련가 | 체리 머신으로 S10 도달 |
| 182 | mmast_cherry_g | 🍒 | 체리 머신 명인 | mstage_cherry | 15 | 머신숙련 | 골드 | false | 프레임: 체리 릴 | 체리 머신으로 S15 도달 |
| 183 | mmast_cherry_p | 🍒 | 체리 머신의 전설 | mstage_cherry | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 체리 릴의 지배자 | 체리 머신으로 S20 도달 |
| 184 | mmast_library_b | 📚 | 도서관 입문 | mstage_library | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 도서관 머신으로 S5 도달 |
| 185 | mmast_library_s | 📚 | 도서관 숙련 | mstage_library | 10 | 머신숙련 | 실버 | false | 칭호: 도서관 단골 | 도서관 머신으로 S10 도달 |
| 186 | mmast_library_g | 📚 | 도서관 명인 | mstage_library | 15 | 머신숙련 | 골드 | false | 프레임: 지혜의 서가 | 도서관 머신으로 S15 도달 |
| 187 | mmast_library_p | 📚 | 도서관의 전설 | mstage_library | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 지식의 수호자 | 도서관 머신으로 S20 도달 |
| 188 | mmast_gem_b | 💎 | 보석 머신 입문 | mstage_gem | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 보석 머신으로 S5 도달 |
| 189 | mmast_gem_s | 💎 | 보석 머신 숙련 | mstage_gem | 10 | 머신숙련 | 실버 | false | 칭호: 보석 머신 숙련가 | 보석 머신으로 S10 도달 |
| 190 | mmast_gem_g | 💎 | 보석 머신 명인 | mstage_gem | 15 | 머신숙련 | 골드 | false | 프레임: 보석 릴 | 보석 머신으로 S15 도달 |
| 191 | mmast_gem_p | 💎 | 보석 머신의 전설 | mstage_gem | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 광채의 지배자 | 보석 머신으로 S20 도달 |
| 192 | mmast_magnet_b | 🧲 | 자석 머신 입문 | mstage_magnet | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 자석 머신으로 S5 도달 |
| 193 | mmast_magnet_s | 🧲 | 자석 머신 숙련 | mstage_magnet | 10 | 머신숙련 | 실버 | false | 칭호: 콤보 장인 | 자석 머신으로 S10 도달 |
| 194 | mmast_magnet_g | 🧲 | 자석 머신 명인 | mstage_magnet | 15 | 머신숙련 | 골드 | false | 프레임: 자기장 릴 | 자석 머신으로 S15 도달 |
| 195 | mmast_magnet_p | 🧲 | 자석 머신의 전설 | mstage_magnet | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 인력의 지배자 | 자석 머신으로 S20 도달 |
| 196 | mmast_skull_b | ☠ | 해골 머신 입문 | mstage_skull | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 해골 머신으로 S5 도달 |
| 197 | mmast_skull_s | ☠ | 해골 머신 숙련 | mstage_skull | 10 | 머신숙련 | 실버 | false | 칭호: 위험의 동반자 | 해골 머신으로 S10 도달 |
| 198 | mmast_skull_g | ☠ | 해골 머신 명인 | mstage_skull | 15 | 머신숙련 | 골드 | false | 프레임: 해골 릴 | 해골 머신으로 S15 도달 |
| 199 | mmast_skull_p | ☠ | 해골 머신의 전설 | mstage_skull | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 사신의 도박판 | 해골 머신으로 S20 도달 |
| 200 | mmast_crown_b | 👑 | 왕관 머신 입문 | mstage_crown | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 왕관 머신으로 S5 도달 |
| 201 | mmast_crown_s | 👑 | 왕관 머신 숙련 | mstage_crown | 10 | 머신숙련 | 실버 | false | 칭호: 운빨의 귀족 | 왕관 머신으로 S10 도달 |
| 202 | mmast_crown_g | 👑 | 왕관 머신 명인 | mstage_crown | 15 | 머신숙련 | 골드 | false | 프레임: 왕관 릴 | 왕관 머신으로 S15 도달 |
| 203 | mmast_crown_p | 👑 | 왕관 머신의 전설 | mstage_crown | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 운명을 거머쥔 왕 | 왕관 머신으로 S20 도달 |
| 204 | mmast_flame_b | 🔥 | 불꽃 머신 입문 | mstage_flame | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 불꽃 머신으로 S5 도달 |
| 205 | mmast_flame_s | 🔥 | 불꽃 머신 숙련 | mstage_flame | 10 | 머신숙련 | 실버 | false | 칭호: 배율의 연소자 | 불꽃 머신으로 S10 도달 |
| 206 | mmast_flame_g | 🔥 | 불꽃 머신 명인 | mstage_flame | 15 | 머신숙련 | 골드 | false | 프레임: 화염 릴 | 불꽃 머신으로 S15 도달 |
| 207 | mmast_flame_p | 🔥 | 불꽃 머신의 전설 | mstage_flame | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 불꽃의 지배자 | 불꽃 머신으로 S20 도달 |
| 208 | mmast_bomb_b | 💣 | 폭탄 머신 입문 | mstage_bomb | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 폭탄 머신으로 S5 도달 |
| 209 | mmast_bomb_s | 💣 | 폭탄 머신 숙련 | mstage_bomb | 10 | 머신숙련 | 실버 | false | 칭호: 폭파 전문가 | 폭탄 머신으로 S10 도달 |
| 210 | mmast_bomb_g | 💣 | 폭탄 머신 명인 | mstage_bomb | 15 | 머신숙련 | 골드 | false | 프레임: 폭탄 릴 | 폭탄 머신으로 S15 도달 |
| 211 | mmast_bomb_p | 💣 | 폭탄 머신의 전설 | mstage_bomb | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 파괴의 지배자 | 폭탄 머신으로 S20 도달 |
| 212 | mmast_star_b | ⭐ | 별빛 머신 입문 | mstage_star | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 별빛 머신으로 S5 도달 |
| 213 | mmast_star_s | ⭐ | 별빛 머신 숙련 | mstage_star | 10 | 머신숙련 | 실버 | false | 칭호: 별빛 항해사 | 별빛 머신으로 S10 도달 |
| 214 | mmast_star_g | ⭐ | 별빛 머신 명인 | mstage_star | 15 | 머신숙련 | 골드 | false | 프레임: 별빛 릴 | 별빛 머신으로 S15 도달 |
| 215 | mmast_star_p | ⭐ | 별빛 머신의 전설 | mstage_star | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 별자리의 지배자 | 별빛 머신으로 S20 도달 |
| 216 | mmast_clover_b | 🍀 | 행운 머신 입문 | mstage_clover | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 행운 머신으로 S5 도달 |
| 217 | mmast_clover_s | 🍀 | 행운 머신 숙련 | mstage_clover | 10 | 머신숙련 | 실버 | false | 칭호: 행운 머신 숙련가 | 행운 머신으로 S10 도달 |
| 218 | mmast_clover_g | 🍀 | 행운 머신 명인 | mstage_clover | 15 | 머신숙련 | 골드 | false | 프레임: 클로버 릴 | 행운 머신으로 S15 도달 |
| 219 | mmast_clover_p | 🍀 | 행운 머신의 전설 | mstage_clover | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 행운의 지배자 | 행운 머신으로 S20 도달 |
| 220 | mmast_casino_b | 🎲 | 카지노 입문 | mstage_casino | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 카지노 머신으로 S5 도달 |
| 221 | mmast_casino_s | 🎲 | 카지노 숙련 | mstage_casino | 10 | 머신숙련 | 실버 | false | 칭호: 고변동의 베터 | 카지노 머신으로 S10 도달 |
| 222 | mmast_casino_g | 🎲 | 카지노 명인 | mstage_casino | 15 | 머신숙련 | 골드 | false | 프레임: 주사위 릴 | 카지노 머신으로 S15 도달 |
| 223 | mmast_casino_p | 🎲 | 카지노의 전설 | mstage_casino | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 운빨의 제왕 | 카지노 머신으로 S20 도달 |
| 224 | mmast_garden_b | 🌱 | 정원 머신 입문 | mstage_garden | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 정원 머신으로 S5 도달 |
| 225 | mmast_garden_s | 🌱 | 정원 머신 숙련 | mstage_garden | 10 | 머신숙련 | 실버 | false | 칭호: 성장의 정원사 | 정원 머신으로 S10 도달 |
| 226 | mmast_garden_g | 🌱 | 정원 머신 명인 | mstage_garden | 15 | 머신숙련 | 골드 | false | 프레임: 새싹 릴 | 정원 머신으로 S15 도달 |
| 227 | mmast_garden_p | 🌱 | 정원 머신의 전설 | mstage_garden | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 생명의 정원사 | 정원 머신으로 S20 도달 |
| 228 | mmast_wildmac_b | 🌀 | 와일드 입문 | mstage_wildmac | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 와일드 머신으로 S5 도달 |
| 229 | mmast_wildmac_s | 🌀 | 와일드 숙련 | mstage_wildmac | 10 | 머신숙련 | 실버 | false | 칭호: 세트 조작가 | 와일드 머신으로 S10 도달 |
| 230 | mmast_wildmac_g | 🌀 | 와일드 명인 | mstage_wildmac | 15 | 머신숙련 | 골드 | false | 프레임: 와일드 릴 | 와일드 머신으로 S15 도달 |
| 231 | mmast_wildmac_p | 🌀 | 와일드의 전설 | mstage_wildmac | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 혼돈의 지배자 | 와일드 머신으로 S20 도달 |
| 232 | mmast_vault_b | 🗝 | 금고 머신 입문 | mstage_vault | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 금고 머신으로 S5 도달 |
| 233 | mmast_vault_s | 🗝 | 금고 머신 숙련 | mstage_vault | 10 | 머신숙련 | 실버 | false | 칭호: 금고털이 | 금고 머신으로 S10 도달 |
| 234 | mmast_vault_g | 🗝 | 금고 머신 명인 | mstage_vault | 15 | 머신숙련 | 골드 | false | 프레임: 황금 열쇠 | 금고 머신으로 S15 도달 |
| 235 | mmast_vault_p | 🗝 | 금고 머신의 전설 | mstage_vault | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 보고의 지배자 | 금고 머신으로 S20 도달 |
| 236 | mmast_rainbow_b | 🌈 | 무지개 입문 | mstage_rainbow | 5 | 머신숙련 | 브론즈 | false | 도감 등록 | 무지개 머신으로 S5 도달 |
| 237 | mmast_rainbow_s | 🌈 | 무지개 숙련 | mstage_rainbow | 10 | 머신숙련 | 실버 | false | 칭호: 한방의 추격자 | 무지개 머신으로 S10 도달 |
| 238 | mmast_rainbow_g | 🌈 | 무지개 명인 | mstage_rainbow | 15 | 머신숙련 | 골드 | false | 프레임: 무지개 릴 | 무지개 머신으로 S15 도달 |
| 239 | mmast_rainbow_p | 🌈 | 무지개의 전설 | mstage_rainbow | 20 | 머신숙련 | 프리즘 | false | 고급 칭호: 일곱 빛깔의 지배자 | 무지개 머신으로 S20 도달 |
| 240 | rv_last10 | 🎯 | 역전의 명수 | lastSpinClears | 10 | 역전 | 골드 | false | 칭호: 역전의 명수 | 마지막 스핀 클리어 10회 |
| 241 | rv_last30 | 🎯 | 막판 승부사 | lastSpinClears | 30 | 역전 | 프리즘 | false | 고급 칭호: 운명의 한 스핀 | 마지막 스핀 클리어 30회 |
| 242 | rv_close5 | 😰 | 간발의 차 | closeClears | 5 | 역전 | 브론즈 | false | 도감 등록 | 잔여 EXP 10이하로 5회 클리어 |
| 243 | rv_close50 | 😰 | 벼랑 끝 곡예 | closeClears | 50 | 역전 | 프리즘 | false | 고급 칭호: 외줄타기의 달인 | 잔여 EXP 10이하로 50회 클리어 |
| 244 | pc_exact10 | 📐 | 정밀 사격수 | exactClears | 10 | 정밀 | 골드 | false | 칭호: 정밀 사격수 | 요구치 정확 일치 클리어 10회 |
| 245 | pc_exact20 | 📐 | 0의 미학 | exactClears | 20 | 정밀 | 프리즘 | false | 고급 칭호: 빈틈없는 계산가 | 요구치 정확 일치 클리어 20회 |
| 246 | pc_set4_5 | 🍀 | 네 잎의 행운 | set4Plus | 5 | 정밀 | 브론즈 | false | 도감 등록 | 같은 심볼 4개 이상 5회 |
| 247 | pc_set4_20 | 🍀 | 정렬의 달인 | set4Plus | 20 | 정밀 | 골드 | false | 칭호: 정렬의 달인 | 같은 심볼 4개 이상 20회 |
| 248 | ov_score20k | 📈 | 이만점 클럽 | bestScore | 20000 | 고점 | 실버 | false | 칭호: 이만점 클럽 | 한 런 20000점 달성 |
| 249 | ov_score70k | 📈 | 칠만의 벽 | bestScore | 70000 | 고점 | 골드 | false | 칭호: 칠만의 벽을 넘은 자 | 한 런 70000점 달성 |
| 250 | ov_stage25 | 🗻 | 끝없는 등반 | bestStage | 25 | 고점 | 프리즘 | false | 고급 칭호: 천공의 등반가 | 25스테이지 도달 |
| 251 | ov_over120 | 💥 | 여유로운 클리어 | maxOverPct | 120 | 고점 | 브론즈 | false | 도감 등록 | 한 스테이지 요구치 120% 초과 달성 |
| 252 | ov_over150 | 💥 | 넉넉한 한 방 | maxOverPct | 150 | 고점 | 실버 | false | 칭호: 넉넉한 한 방 | 한 스테이지 요구치 150% 초과 달성 |
| 253 | ov_over200 | 💥 | 두 배의 폭발 | maxOverPct | 200 | 고점 | 골드 | false | 칭호: 두 배의 폭발 | 한 스테이지 요구치 200% 초과 달성 |
| 254 | ov_over300 | 💥 | 압도적 초과 | maxOverPct | 300 | 고점 | 골드 | false | 칭호: 압도적 초과 | 한 스테이지 요구치 300% 초과 달성 |
| 255 | ov_over500 | 💥 | 초과의 화신 | maxOverPct | 500 | 고점 | 프리즘 | false | 고급 칭호: 한계를 부수는 자 | 한 스테이지 요구치 500% 초과 달성 |
| 256 | bs_boss10 | 👹 | 보스 토벌대 | bossClears | 10 | 보스 | 실버 | false | 칭호: 보스 토벌대장 | 보스 10회 클리어 |
| 257 | bs_boss30 | 👹 | 보스 처형자 | bossClears | 30 | 보스 | 프리즘 | false | 고급 칭호: 보스의 천적 | 보스 30회 클리어 |
| 258 | ec_shop5 | 🛍️ | 첫 단골 | shopBuys | 5 | 경제 | 브론즈 | false | 도감 등록 | 상점에서 5회 구매 |
| 259 | ec_shop25 | 🛍️ | VIP 고객 | shopBuys | 25 | 경제 | 골드 | false | 칭호: 상점 VIP | 상점에서 25회 구매 |
| 260 | ec_reroll5 | 🔄 | 다시 한 번 | rerollUses | 5 | 경제 | 브론즈 | false | 도감 등록 | 재굴림 5회 사용 |
| 261 | ec_reroll30 | 🔄 | 운명 거부자 | rerollUses | 30 | 경제 | 골드 | false | 칭호: 운명 거부자 | 재굴림 30회 사용 |
| 262 | ec_gamble5 | 🎲 | 도박장 단골 | gambles | 5 | 경제 | 실버 | false | 칭호: 도박장 단골 | 도박장 노드 5회 이용 |
| 263 | ec_gamble30 | 🎲 | 하우스의 친구 | gambles | 30 | 경제 | 프리즘 | false | 고급 칭호: 하우스의 친구 | 도박장 노드 30회 이용 |
| 264 | ec_allin10 | 💥 | 올인 베테랑 | allinWins | 10 | 경제 | 골드 | false | 칭호: 올인 베테랑 | 올인 스핀 10회 승리 |
| 265 | ec_allin50 | 💥 | 올인의 전설 | allinWins | 50 | 경제 | 프리즘 | false | 고급 칭호: 올인의 전설 | 올인 스핀 50회 승리 |
| 266 | ec_pray15 | 🙏 | 독실한 신자 | prayClears | 15 | 경제 | 골드 | false | 칭호: 독실한 신자 | 기도 후 클리어 15회 |
| 267 | ec_pray30 | 🙏 | 기적의 사도 | prayClears | 30 | 경제 | 프리즘 | false | 고급 칭호: 기적의 사도 | 기도 후 클리어 30회 |
| 268 | ec_jackpot25 | 🎰 | 잭팟 수집가 | jackpots | 25 | 경제 | 골드 | false | 칭호: 잭팟 수집가 | 5칸 잭팟 25회 달성 |
| 269 | ec_jackpot50 | 🎰 | 잭팟의 화신 | jackpots | 50 | 경제 | 프리즘 | false | 고급 칭호: 잭팟의 화신 | 5칸 잭팟 50회 달성 |
| 270 | ec_prism10 | 🔮 | 프리즘 애호가 | prismPicks | 10 | 경제 | 골드 | false | 칭호: 프리즘 애호가 | 프리즘 증강 10회 선택 |
| 271 | ec_prism50 | 🔮 | 프리즘 마스터 | prismPicks | 50 | 경제 | 프리즘 | false | 고급 칭호: 프리즘 마스터 | 프리즘 증강 50회 선택 |
| 272 | ec_dev30 | ⚙️ | 장치 숙련공 | deviceUses | 30 | 경제 | 골드 | false | 칭호: 장치 숙련공 | 장치를 30회 사용 |
| 273 | ec_dev100 | ⚙️ | 장치 대가 | deviceUses | 100 | 경제 | 프리즘 | false | 고급 칭호: 장치의 대가 | 장치를 100회 사용 |
| 274 | ec_item25 | 🎒 | 아이템 애호가 | itemsUsed | 25 | 경제 | 실버 | false | 칭호: 알뜰 소비자 | 아이템 25회 사용 |
| 275 | ec_item200 | 🎒 | 소비의 정점 | itemsUsed | 200 | 경제 | 프리즘 | false | 고급 칭호: 소비의 정점 | 아이템 200회 사용 |
| 276 | ec_coin3000 | 🪙 | 조폐국 총재 | coinTotal | 3000 | 경제 | 프리즘 | false | 고급 칭호: 조폐국 총재 | 🪙코인 누적 3000개 등장 |
| 277 | ec_dev_own8 | 🔧 | 장치 보유 8종 | devicesOwned | 8 | 경제 | 골드 | false | 칭호: 장치 보유 8종 | 장치를 8종 영구 보유 |
| 278 | bd_relic7 | 🏺 | 유물 수호자 | relicsMax | 7 | 빌드 | 골드 | false | 칭호: 유물 수호자 | 한 런에 유물 7개 동시 보유 |
| 279 | bd_relic15 | 🏺 | 유물 박물관장 | relicsMax | 15 | 빌드 | 프리즘 | false | 고급 칭호: 유물 박물관장 | 한 런에 유물 15개 동시 보유 |
| 280 | bd_curse5_10 | ☠️ | 저주를 안고 | curse5Stage | 10 | 빌드 | 골드 | false | 칭호: 저주를 안은 등반가 | 저주 5개 이상 보유로 S10 도달 |
| 281 | bd_curse5_15 | ☠️ | 저주와 동행 | curse5Stage | 15 | 빌드 | 프리즘 | false | 고급 칭호: 저주를 다스리는 자 | 저주 5개 이상 보유로 S15 도달 |
| 282 | bd_nodev10 | 🚫 | 맨손의 등반가 | noDevStage | 10 | 빌드 | 골드 | false | 칭호: 맨손의 등반가 | 장치 없이 S10 도달 |
| 283 | bd_nodev15 | 🚫 | 무장치의 달인 | noDevStage | 15 | 빌드 | 프리즘 | false | 고급 칭호: 무장치의 달인 | 장치 없이 S15 도달 |
| 284 | bd_noitem10 | 🧘 | 비움의 등반 | noItemMaxS | 10 | 빌드 | 골드 | false | 칭호: 비움의 등반가 | 아이템 없이 S10 도달 |
| 285 | bd_noitem15 | 🧘 | 무소유의 경지 | noItemMaxS | 15 | 빌드 | 프리즘 | false | 고급 칭호: 무소유의 경지 | 아이템 없이 S15 도달 |
| 286 | rp_spin2000 | 🔂 | 이천 번의 스핀 | totalSpins | 2000 | 반복 | 골드 | false | 칭호: 손목의 장인 | 누적 2000회 스핀 |
| 287 | rp_spin10000 | 🔂 | 만 번의 스핀 | totalSpins | 10000 | 반복 | 프리즘 | false | 고급 칭호: 스핀의 화신 | 누적 10000회 스핀 |
| 288 | rp_focus100 | 🎯 | 집중의 화신 | focusUses | 100 | 반복 | 프리즘 | false | 고급 칭호: 집중의 화신 | 집중 명령 100회 사용 |
| 289 | rp_last50 | ⏳ | 최후의 화신 | lastUses | 50 | 반복 | 프리즘 | false | 고급 칭호: 최후의 화신 | 최후 명령 50회 사용 |
| 290 | rp_reroll50 | 🔄 | 재굴림의 화신 | rerollUses | 50 | 반복 | 프리즘 | false | 고급 칭호: 재굴림의 화신 | 재굴림 50회 사용 |
| 291 | rp_pin30 | 📌 | 고정의 화신 | pinUses | 30 | 반복 | 골드 | false | 칭호: 고정의 화신 | 고정 30회 사용 |
| 292 | rj_run2 | 🎰 | 더블 잭팟 | maxRunJackpots | 2 | 역전 | 실버 | false | 칭호: 더블 잭팟 | 한 런에 잭팟 2회 |
| 293 | rj_run3 | 🎰 | 트리플 잭팟 | maxRunJackpots | 3 | 역전 | 골드 | false | 칭호: 트리플 잭팟 | 한 런에 잭팟 3회 |
| 294 | rj_run5 | 🎰 | 잭팟 폭풍 | maxRunJackpots | 5 | 역전 | 프리즘 | false | 고급 칭호: 잭팟 폭풍의 주인 | 한 런에 잭팟 5회 |
| 295 | sp_wild30 | 🌀 | 와일드 입문 | wildTotal | 30 | 특수심볼 | 브론즈 | false | 도감 등록 | 🌀와일드 누적 30개 등장 |
| 296 | sp_wild150 | 🌀 | 혼돈의 조율사 | wildTotal | 150 | 특수심볼 | 실버 | false | 칭호: 혼돈의 조율사 | 🌀와일드 누적 150개 등장 |
| 297 | sp_wild500 | 🌀 | 와일드 마술사 | wildTotal | 500 | 특수심볼 | 골드 | false | 칭호: 와일드 마술사 | 🌀와일드 누적 500개 등장 |
| 298 | sp_wild1500 | 🌀 | 혼돈의 지배자 | wildTotal | 1500 | 특수심볼 | 프리즘 | false | 고급 칭호: 혼돈의 지배자 | 🌀와일드 누적 1500개 등장 |
| 299 | sp_seed30 | 🌱 | 씨 뿌리기 | seedTotal | 30 | 특수심볼 | 브론즈 | false | 도감 등록 | 🌱씨앗 누적 30개 등장 |
| 300 | sp_seed150 | 🌱 | 성실한 정원사 | seedTotal | 150 | 특수심볼 | 실버 | false | 칭호: 성실한 정원사 | 🌱씨앗 누적 150개 등장 |
| 301 | sp_seed500 | 🌱 | 생명의 재배자 | seedTotal | 500 | 특수심볼 | 골드 | false | 칭호: 생명의 재배자 | 🌱씨앗 누적 500개 등장 |
| 302 | sp_dice30 | 🎲 | 주사위 굴리기 | diceTotal | 30 | 특수심볼 | 브론즈 | false | 도감 등록 | 🎲주사위 누적 30개 등장 |
| 303 | sp_dice150 | 🎲 | 운명의 도박사 | diceTotal | 150 | 특수심볼 | 실버 | false | 칭호: 운명의 도박사 | 🎲주사위 누적 150개 등장 |
| 304 | sp_dice500 | 🎲 | 확률의 지배자 | diceTotal | 500 | 특수심볼 | 골드 | false | 칭호: 확률의 지배자 | 🎲주사위 누적 500개 등장 |
| 305 | sp_key30 | 🗝 | 열쇠 줍기 | keyTotal | 30 | 특수심볼 | 브론즈 | false | 도감 등록 | 🗝열쇠 누적 30개 등장 |
| 306 | sp_key150 | 🗝 | 금고 단골 | keyTotal | 150 | 특수심볼 | 실버 | false | 칭호: 금고 단골 | 🗝열쇠 누적 150개 등장 |
| 307 | sp_key500 | 🗝 | 보고의 열쇠지기 | keyTotal | 500 | 특수심볼 | 골드 | false | 칭호: 보고의 열쇠지기 | 🗝열쇠 누적 500개 등장 |
| 308 | sp_flame30 | 🔥 | 불씨 점화 | flameTotal | 30 | 특수심볼 | 브론즈 | false | 도감 등록 | 🔥불꽃 누적 30개 등장 |
| 309 | sp_flame150 | 🔥 | 연소의 달인 | flameTotal | 150 | 특수심볼 | 실버 | false | 칭호: 연소의 달인 | 🔥불꽃 누적 150개 등장 |
| 310 | sp_flame500 | 🔥 | 화염의 지배자 | flameTotal | 500 | 특수심볼 | 골드 | false | 칭호: 화염의 지배자 | 🔥불꽃 누적 500개 등장 |
| 311 | sp_magnet30 | 🧲 | 자석 입문 | magnetTotal | 30 | 특수심볼 | 브론즈 | false | 도감 등록 | 🧲자석 누적 30개 등장 |
| 312 | sp_magnet150 | 🧲 | 끌어당기는 손 | magnetTotal | 150 | 특수심볼 | 실버 | false | 칭호: 끌어당기는 손 | 🧲자석 누적 150개 등장 |
| 313 | sp_magnet500 | 🧲 | 인력의 지배자 | magnetTotal | 500 | 특수심볼 | 골드 | false | 칭호: 인력의 지배자 | 🧲자석 누적 500개 등장 |
| 314 | sp_bomb30 | 💣 | 폭탄 해체반 | bombTotal | 30 | 특수심볼 | 브론즈 | false | 도감 등록 | 💣폭탄 누적 30개 등장 |
| 315 | sp_bomb150 | 💣 | 폭파의 전문가 | bombTotal | 150 | 특수심볼 | 실버 | false | 칭호: 폭파의 전문가 | 💣폭탄 누적 150개 등장 |
| 316 | sp_bomb500 | 💣 | 파괴의 지배자 | bombTotal | 500 | 특수심볼 | 골드 | false | 칭호: 파괴의 지배자 | 💣폭탄 누적 500개 등장 |
| 317 | jk_crown1 | 👑 | 왕관 잭팟 | crownJackpots | 1 | 잭팟 | 골드 | false | 도감 등록 | 👑왕관 5칸 잭팟 달성 |
| 318 | jk_crown5 | 👑 | 황금의 정렬 | crownJackpots | 5 | 잭팟 | 프리즘 | false | 고급 칭호: 왕관 잭팟의 제왕 | 👑왕관 잭팟 5회 달성 |
| 319 | jk_wild1 | 🌀 | 와일드 잭팟 | wildJackpots | 1 | 잭팟 | 골드 | false | 도감 등록 | 🌀와일드를 끼워 잭팟 달성 |
| 320 | jk_wild10 | 🌀 | 조작된 운명 | wildJackpots | 10 | 잭팟 | 프리즘 | false | 고급 칭호: 운명을 조작하는 자 | 🌀와일드 포함 잭팟 10회 |
| 321 | hid_skull5spin | 💀 | 죽음의 한 줄 | maxSkullSpin | 5 | 히든 | 프리즘 | true | 칭호: 죽음의 한 줄 | 한 스핀에 ☠해골 5개 |
| 322 | hid_coin5spin | 🪙 | 동전 벼락 | maxCoinSpin | 5 | 히든 | 프리즘 | true | 칭호: 동전 벼락 | 한 스핀에 🪙코인 5개 |
| 323 | hid_cherry5spin | 🍒 | 체리 만발 | maxCherrySpin | 5 | 히든 | 골드 | true | 칭호: 체리 만발 | 한 스핀에 🍒체리 5개 |
| 324 | hid_book5spin | 📘 | 전권 정렬 | maxBookSpin | 5 | 히든 | 골드 | true | 칭호: 전권 정렬 | 한 스핀에 📘책 5개 |
| 325 | hid_gem5spin | 💎 | 보석 일렬 | maxGemSpin | 5 | 히든 | 골드 | true | 칭호: 보석 일렬 | 한 스핀에 💎보석 5개 |
| 326 | hid_allinbust1 | 💀 | 올인의 대가 | allinBusts | 1 | 히든 | 브론즈 | true | 도감 등록 | 올인이 ☠2개로 EXP 0이 되었다 |
| 327 | hid_allinbust10 | 💀 | 파산의 길 | allinBusts | 10 | 히든 | 골드 | true | 칭호: 파산의 길 | 올인 폭망 10회 |
| 328 | hid_prayfail1 | 🙏 | 응답 없는 기도 | prayFails | 1 | 히든 | 브론즈 | true | 도감 등록 | 기도하고도 스테이지 실패 |
| 329 | hid_prayfail10 | 🙏 | 시험받는 신앙 | prayFails | 10 | 히든 | 골드 | true | 칭호: 시험받는 신앙 | 기도 실패 10회 |
| 330 | lc_noprism5 | 🚷 | 무지개 금욕 | noPrismBestStage | 5 | 제한도전 | 실버 | false | 도감 등록 | 🌈프리즘 증강 없이 S5 클리어 |
| 331 | lc_noprism10 | 🚷 | 절제의 등반 | noPrismBestStage | 10 | 제한도전 | 골드 | false | 칭호: 절제의 등반가 | 🌈프리즘 증강 없이 S10 클리어 |
| 332 | lc_noprism15 | 🚷 | 무채색의 정점 | noPrismBestStage | 15 | 제한도전 | 프리즘 | false | 고급 칭호: 무채색의 정점 | 🌈프리즘 증강 없이 S15 클리어 |
| 333 | lc_norelic5 | 🛡️ | 맨몸의 도전 | noRelicBestStage | 5 | 제한도전 | 실버 | false | 도감 등록 | 🛡️유물 없이 S5 클리어 |
| 334 | lc_norelic10 | 🛡️ | 유물 없는 길 | noRelicBestStage | 10 | 제한도전 | 골드 | false | 칭호: 무유물 등반가 | 🛡️유물 없이 S10 클리어 |
| 335 | lc_norelic15 | 🛡️ | 순수 증강주의 | noRelicBestStage | 15 | 제한도전 | 프리즘 | false | 고급 칭호: 순수 증강주의 | 🛡️유물 없이 S15 클리어 |
| 336 | lc_nogold5 | 🥈 | 실버 빌드 | noGoldBestStage | 5 | 제한도전 | 실버 | false | 도감 등록 | 골드↑ 증강 없이 S5 클리어 |
| 337 | lc_nogold10 | 🥈 | 은의 길 | noGoldBestStage | 10 | 제한도전 | 골드 | false | 칭호: 은의 길 | 골드↑ 증강 없이 S10 클리어 |
| 338 | lc_nogold12 | 🥈 | 겸손한 명인 | noGoldBestStage | 12 | 제한도전 | 프리즘 | false | 고급 칭호: 겸손한 명인 | 골드↑ 증강 없이 S12 클리어 |
| 339 | lc_basic5 | 🐣 | 맨주먹 신입생 | basicOnlyBestStage | 5 | 제한도전 | 실버 | false | 도감 등록 | 초보+기본 머신으로 S5 클리어 |
| 340 | lc_basic10 | 🐣 | 기본기의 증명 | basicOnlyBestStage | 10 | 제한도전 | 골드 | false | 칭호: 기본기의 달인 | 초보+기본 머신으로 S10 클리어 |
| 341 | lc_basic15 | 🐣 | 무에서 정점으로 | basicOnlyBestStage | 15 | 제한도전 | 프리즘 | false | 고급 칭호: 무에서 정점으로 | 초보+기본 머신으로 S15 클리어 |
| 342 | bc_finals1 | 📝 | 기말고사 합격 | bossClear_finals | 1 | 보스공략 | 실버 | false | 도감 등록 | 📝기말고사를 처음 클리어 |
| 343 | bc_finals10 | 📝 | 수석 졸업 | bossClear_finals | 10 | 보스공략 | 골드 | false | 칭호: 기말 수석 | 📝기말고사 10회 클리어 |
| 344 | bc_finals_ctr | ⏰ | 최후의 답안 | bossCounterClear_finals | 5 | 보스공략 | 프리즘 | false | 고급 칭호: 막판의 천재 | 📝기말고사를 막스핀 클리어 5회 |
| 345 | bc_strict1 | 👨‍🏫 | 꼰대 통과 | bossClear_strict | 1 | 보스공략 | 실버 | false | 도감 등록 | 👨‍🏫꼰대교수를 처음 클리어 |
| 346 | bc_strict10 | 👨‍🏫 | 교수의 인정 | bossClear_strict | 10 | 보스공략 | 골드 | false | 칭호: 교수의 애제자 | 👨‍🏫꼰대교수 10회 클리어 |
| 347 | bc_strict_ctr | 🧩 | 완벽한 세트 | bossCounterClear_strict | 5 | 보스공략 | 프리즘 | false | 고급 칭호: 세트의 장인 | 👨‍🏫꼰대교수를 세트3+ 스핀으로 클리어 5회 |
| 348 | bc_luck1 | 🎲 | 심판관 통과 | bossClear_luck | 1 | 보스공략 | 실버 | false | 도감 등록 | 🎲운빨심판관을 처음 클리어 |
| 349 | bc_luck10 | 🎲 | 운명의 총아 | bossClear_luck | 10 | 보스공략 | 골드 | false | 칭호: 운명의 총아 | 🎲운빨심판관 10회 클리어 |
| 350 | bc_luck_ctr | 🍀 | 행운의 정렬 | bossCounterClear_luck | 5 | 보스공략 | 프리즘 | false | 고급 칭호: 행운의 화신 | 🎲운빨심판관을 ⭐👑🌀 포함 스핀으로 클리어 5회 |
| 351 | bc_grad1 | 🎓 | 졸업 승인 | bossClear_grad | 1 | 보스공략 | 골드 | false | 도감 등록 | 🎓졸업심사를 처음 클리어 |
| 352 | bc_grad10 | 🎓 | 명예 졸업 | bossClear_grad | 10 | 보스공략 | 프리즘 | false | 칭호: 명예 졸업생 | 🎓졸업심사 10회 클리어 |
| 353 | bc_grad_ctr | ✋ | 맨손 졸업 | bossCounterClear_grad | 3 | 보스공략 | 프리즘 | false | 고급 칭호: 맨손의 졸업생 | 🎓졸업심사를 무장치로 클리어 3회 |
| 354 | bx_noitem1 | 🧘 | 무소유의 보스전 | bossNoItemClears | 1 | 보스공략 | 골드 | false | 도감 등록 | 아이템 없이 보스 클리어 |
| 355 | bx_noitem10 | 🧘 | 비움의 정복자 | bossNoItemClears | 10 | 보스공략 | 프리즘 | false | 고급 칭호: 비움의 정복자 | 아이템 없이 보스 10회 클리어 |
| 356 | bx_nodev1 | 🚫 | 맨몸 보스전 | bossNoDeviceClears | 1 | 보스공략 | 골드 | false | 도감 등록 | 장치 없이 보스 클리어 |
| 357 | bx_nodev10 | 🚫 | 장치 없는 사냥꾼 | bossNoDeviceClears | 10 | 보스공략 | 프리즘 | false | 고급 칭호: 장치 없는 사냥꾼 | 장치 없이 보스 10회 클리어 |
| 358 | bx_overkill1 | 💥 | 보스 오버킬 | bossOverkillClears | 1 | 보스공략 | 골드 | false | 칭호: 오버킬 | 초과 500%+ 로 보스 클리어 |
| 359 | bx_overkill10 | 💥 | 압도적 우위 | bossOverkillClears | 10 | 보스공략 | 프리즘 | false | 고급 칭호: 압도적 지배자 | 초과 500%+ 로 보스 10회 클리어 |
| 360 | bx_streak3 | 🔥 | 보스 삼연참 | bossStreak3 | 1 | 보스공략 | 프리즘 | false | 고급 칭호: 보스 삼연참 | 한 런에 보스 3회 격파(S15+) |
| 361 | hid_zerocoin1 | 🪙 | 무일푼 클리어 | zeroCoinClears | 1 | 히든 | 실버 | true | 도감 등록 | 코인 0으로 스테이지 클리어 |
| 362 | hid_zerocoin20 | 🪙 | 청빈의 도 | zeroCoinClears | 20 | 히든 | 프리즘 | true | 칭호: 청빈의 도 | 코인 0으로 20회 클리어 |
| 363 | hid_debtboss1 | 🧾 | 빚더미 보스전 | debtBossClears | 1 | 히든 | 골드 | true | 도감 등록 | 빚문서 상태로 보스 클리어 |
| 364 | hid_debtboss5 | 🧾 | 채무의 승부사 | debtBossClears | 5 | 히든 | 프리즘 | true | 칭호: 채무의 승부사 | 빚문서 상태로 보스 5회 클리어 |
| 365 | lc_nodev5 | 🚫 | 맨손 등반 입문 | noDevStage | 5 | 제한도전 | 실버 | false | 도감 등록 | 🚫장치 없이 S5 클리어 |
| 366 | lc_noitem8 | 🧘 | 비움의 등반 | noItemMaxS | 8 | 제한도전 | 실버 | false | 도감 등록 | 🧘아이템 없이 S8 클리어 |
| 367 | lc_noshop10 | 🪙 | 자급자족 졸업 | noShopS10 | 10 | 제한도전 | 골드 | false | 칭호: 자급자족 | 🛒상점 없이 S10 도달 |
| 368 | lc_minimalist10 | 🍃 | 미니멀리스트 | minimalistS10 | 1 | 제한도전 | 실버 | false | 도감 등록 | 유물 3개 이하로 S10 클리어 |
| 369 | bc_finals_ctr1 | ⏰ | 막판의 한 수 | bossCounterClear_finals | 1 | 보스공략 | 실버 | false | 도감 등록 | 📝기말고사를 막스핀 클리어 |
| 370 | bc_strict_ctr1 | 🧩 | 세트의 첫 인정 | bossCounterClear_strict | 1 | 보스공략 | 실버 | false | 도감 등록 | 👨‍🏫꼰대교수를 세트3+ 스핀으로 클리어 |
| 371 | bc_luck_ctr1 | 🍀 | 첫 행운의 정렬 | bossCounterClear_luck | 1 | 보스공략 | 실버 | false | 도감 등록 | 🎲운빨심판관을 ⭐👑🌀 포함 스핀으로 클리어 |
| 372 | bc_grad_ctr1 | ✋ | 첫 맨손 졸업 | bossCounterClear_grad | 1 | 보스공략 | 골드 | false | 도감 등록 | 🎓졸업심사를 무장치로 클리어 |
| 373 | bx_noitem3 | 🧘 | 무소유의 연승 | bossNoItemClears | 3 | 보스공략 | 골드 | false | 칭호: 무소유 사냥꾼 | 아이템 없이 보스 3회 클리어 |
| 374 | bx_nodev3 | 🚫 | 맨몸의 연승 | bossNoDeviceClears | 3 | 보스공략 | 골드 | false | 칭호: 맨몸 사냥꾼 | 장치 없이 보스 3회 클리어 |
| 375 | rs_growth_intro | 🌱 | 성장학 입문 | cherryTotal | 300 | 연구 | 실버 | false | 🔓성장학 증강·유물 풀 개방 | 🍒체리 누적 300 — 성장학 연구 완료, 성장학 증강·유물 풀 개방 |
| 376 | rs_growth_adv | 🌱 | 성장학 심화 | cherryTotal | 600 | 연구 | 골드 | false | 칭호: 성장학 연구원 | 🍒체리 누적 600 — 성장학 심화 연구 |
| 377 | rs_growth_phd | 🌱 | 성장학 박사 | cherryTotal | 2000 | 연구 | 프리즘 | false | 고급 칭호: 성장학 박사 | 🍒체리 누적 2000 — 성장학 박사 학위 |
| 378 | rs_calc_intro | 🧮 | 계산학 입문 | set4Plus | 3 | 연구 | 실버 | false | 🔓계산학 증강·유물 풀 개방 | 같은 심볼 4+ 3회 — 계산학 연구 완료, 계산학 증강·유물 풀 개방 |
| 379 | rs_calc_adv | 🧮 | 계산학 심화 | set4Plus | 10 | 연구 | 골드 | false | 칭호: 계산학 연구원 | 같은 심볼 4+ 10회 — 계산학 심화 연구 |
| 380 | rs_calc_phd | 🧮 | 계산학 박사 | set4Plus | 30 | 연구 | 프리즘 | false | 고급 칭호: 계산학 박사 | 같은 심볼 4+ 30회 — 계산학 박사 학위 |
| 381 | rs_econ_intro | 💰 | 경제학 입문 | coinTotal | 300 | 연구 | 실버 | false | 🔓경제학 증강·유물 풀 개방 | 🪙코인 누적 300 — 경제학 연구 완료, 경제학 증강·유물 풀 개방 |
| 382 | rs_econ_adv | 💰 | 경제학 심화 | coinTotal | 700 | 연구 | 골드 | false | 칭호: 경제학 연구원 | 🪙코인 누적 700 — 경제학 심화 연구 |
| 383 | rs_econ_phd | 💰 | 경제학 박사 | coinTotal | 1500 | 연구 | 프리즘 | false | 고급 칭호: 경제학 박사 | 🪙코인 누적 1500 — 경제학 박사 학위 |
| 384 | rs_fate_intro | 🎴 | 운명학 입문 | gambles | 3 | 연구 | 실버 | false | 🔓운명학 증강·유물 풀 개방 | 도박장 3회 — 운명학 연구 완료, 운명학 증강·유물 풀 개방 |
| 385 | rs_fate_adv | 🎴 | 운명학 심화 | gambles | 15 | 연구 | 골드 | false | 칭호: 운명학 연구원 | 도박장 15회 — 운명학 심화 연구 |
| 386 | rs_fate_phd | 🎴 | 운명학 박사 | gambles | 50 | 연구 | 프리즘 | false | 고급 칭호: 운명학 박사 | 도박장 50회 — 운명학 박사 학위 |
| 387 | rs_crown_intro | 👑 | 왕관학 입문 | crownTotal | 30 | 연구 | 실버 | false | 🔓왕관학 증강·유물 풀 개방 | 👑왕관 누적 30 — 왕관학 연구 완료, 왕관학 증강·유물 풀 개방 |
| 388 | rs_crown_adv | 👑 | 왕관학 심화 | crownTotal | 60 | 연구 | 골드 | false | 칭호: 왕관학 연구원 | 👑왕관 누적 60 — 왕관학 심화 연구 |
| 389 | rs_crown_phd | 👑 | 왕관학 박사 | crownTotal | 200 | 연구 | 프리즘 | false | 고급 칭호: 왕관학 박사 | 👑왕관 누적 200 — 왕관학 박사 학위 |
| 390 | rs_curse_intro | 💀 | 저주학 입문 | skullTotal | 100 | 연구 | 실버 | false | 🔓저주학 증강·유물 풀 개방 | 💀해골 누적 100 — 저주학 연구 완료, 저주학 증강·유물 풀 개방 |
| 391 | rs_curse_adv | 💀 | 저주학 심화 | skullTotal | 500 | 연구 | 골드 | false | 칭호: 저주학 연구원 | 💀해골 누적 500 — 저주학 심화 연구 |
| 392 | rs_curse_phd | 💀 | 저주학 박사 | skullTotal | 700 | 연구 | 프리즘 | false | 고급 칭호: 저주학 박사 | 💀해골 누적 700 — 저주학 박사 학위 |
| 393 | rs_time_intro | ⏳ | 시간학 입문 | lastSpinClears | 3 | 연구 | 실버 | false | 🔓시간학 증강·유물 풀 개방 | 막판 스핀 클리어 3회 — 시간학 연구 완료, 시간학 증강·유물 풀 개방 |
| 394 | rs_time_adv | ⏳ | 시간학 심화 | lastSpinClears | 7 | 연구 | 골드 | false | 칭호: 시간학 연구원 | 막판 스핀 클리어 7회 — 시간학 심화 연구 |
| 395 | rs_time_phd | ⏳ | 시간학 박사 | lastSpinClears | 15 | 연구 | 프리즘 | false | 고급 칭호: 시간학 박사 | 막판 스핀 클리어 15회 — 시간학 박사 학위 |
| 396 | rs_prism_intro | 🔮 | 프리즘공학 입문 | prismPicks | 3 | 연구 | 실버 | false | 🔓프리즘공학 증강·유물 풀 개방(프리즘 티어 제외) | 프리즘 선택 3회 — 프리즘공학 연구 완료, 프리즘공학 실버·골드 증강·유물 풀 개방(프리즘 티어 제외) |
| 397 | rs_prism_adv | 🔮 | 프리즘공학 심화 | prismPicks | 15 | 연구 | 골드 | false | 칭호: 프리즘공학 연구원 | 프리즘 선택 15회 — 프리즘공학 심화 연구 |
| 398 | rs_prism_phd | 🔮 | 프리즘공학 박사 | prismPicks | 30 | 연구 | 프리즘 | false | 고급 칭호: 프리즘공학 박사 | 프리즘 선택 30회 — 프리즘공학 박사 학위 |
| 399 | rs_seed_intro | 🌰 | 씨앗학 입문 | seedTotal | 10 | 연구 | 실버 | false | 🔓씨앗학 증강·유물 풀 개방 | 🌱씨앗 누적 10 — 씨앗학 연구 완료, 씨앗학 증강·유물 풀 개방 |
| 400 | rs_seed_adv | 🌰 | 씨앗학 심화 | seedTotal | 75 | 연구 | 골드 | false | 칭호: 씨앗학 연구원 | 🌱씨앗 누적 75 — 씨앗학 심화 연구 |
| 401 | rs_seed_phd | 🌰 | 씨앗학 박사 | seedTotal | 300 | 연구 | 프리즘 | false | 고급 칭호: 씨앗학 박사 | 🌱씨앗 누적 300 — 씨앗학 박사 학위 |
| 402 | rs_wild_intro | 🌀 | 와일드학 입문 | wildTotal | 10 | 연구 | 실버 | false | 🔓와일드학 증강·유물 풀 개방 | 🌀와일드 누적 10 — 와일드학 연구 완료, 와일드학 증강·유물 풀 개방 |
| 403 | rs_wild_adv | 🌀 | 와일드학 심화 | wildTotal | 75 | 연구 | 골드 | false | 칭호: 와일드학 연구원 | 🌀와일드 누적 75 — 와일드학 심화 연구 |
| 404 | rs_wild_phd | 🌀 | 와일드학 박사 | wildTotal | 300 | 연구 | 프리즘 | false | 고급 칭호: 와일드학 박사 | 🌀와일드 누적 300 — 와일드학 박사 학위 |
| 405 | lic_safe | 🦺 | 안전벨트 면허 | lic_dev_safe | 1 | 면허 | 골드 | false | 🦺안전벨트 장치 영구해금 | 아슬아슬 클리어 5회 & S6 도달 — 🦺안전벨트 영구해금 |
| 406 | lic_seal | 🔒 | 봉인장막 면허 | lic_dev_seal | 1 | 면허 | 골드 | false | 🔒봉인장막 장치 영구해금 | 💀해골 누적 200 & S8 도달 — 🔒봉인장막 영구해금 |
| 407 | lic_reroll | 🔄 | 재굴림기 면허 | lic_dev_reroll | 1 | 면허 | 골드 | false | 🔄재굴림기 장치 영구해금 | 보스 3회 클리어 & 막판 클리어 3회 — 🔄재굴림기 영구해금 |
| 408 | lic_pin | 📌 | 고정핀 면허 | lic_dev_pin | 1 | 면허 | 골드 | false | 📌고정핀 장치 영구해금 | 정확 클리어 3회 & S8 도달 — 📌고정핀 영구해금 |
| 409 | lic_coin | 🪙 | 코인투입구 면허 | lic_dev_coin | 1 | 면허 | 골드 | false | 🪙코인투입구 장치 영구해금 | 🪙코인 누적 500 & 상점구매 15회 — 🪙코인투입구 영구해금 |
| 410 | lic_subreel | ➕ | 보조릴 면허 | lic_dev_subreel | 1 | 면허 | 골드 | false | ➕보조릴 장치 영구해금 | 잭팟 5회 & 4세트+ 10회 — ➕보조릴 영구해금 |
| 411 | lic_overheat | ♨️ | 과열코어 면허 | lic_dev_overheat | 1 | 면허 | 골드 | false | ♨️과열코어 장치 영구해금 | 막판 클리어 10회 & 최고점수 20,000 — ♨️과열코어 영구해금 |
| 412 | lic_oracle | 🔮 | 예언안경 면허 | lic_dev_oracle | 1 | 면허 | 골드 | false | 🔮예언안경 장치 영구해금 | 기도 클리어 3회 & S15 도달 — 🔮예언안경 영구해금 |
| 413 | lic_copy | 📑 | 복사기 면허 | lic_dev_copy | 1 | 면허 | 골드 | false | 📑복사기 장치 영구해금 | 프리즘 선택 10회 & 4세트+ 10회 — 📑복사기 영구해금 |
| 414 | lic_swap | 🔃 | 교체기 면허 | lic_dev_swap | 1 | 면허 | 골드 | false | 🔃교체기 장치 영구해금 | 보스 10회 클리어 & S15 도달 — 🔃교체기 영구해금 |
| 415 | lic_bell | 🔔 | 비상졸업벨 면허 | lic_dev_bell | 1 | 면허 | 골드 | false | 🔔비상졸업벨 장치 영구해금 | 아슬아슬 클리어 30회 & 보스 8회 클리어 — 🔔비상졸업벨 영구해금 |
| 416 | lic_flame | 🔥 | 불꽃엔진 면허 | lic_dev_flame | 1 | 면허 | 골드 | false | 🔥불꽃엔진 장치 영구해금 | 최고점수 50,000 & S20 도달 — 🔥불꽃엔진 영구해금 |
| 417 | dm_dev_flame_use | 🔥 | 불꽃엔진 숙련 | dvuse_dev_flame | 10 | 장치면허 | 골드 | false | 칭호: 불꽃엔진 숙련자 | 🔥불꽃엔진 장착으로 10런 시작 |
| 418 | dm_dev_flame_master | 🔥 | 불꽃엔진 장인 | dvstage_dev_flame | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 불꽃엔진 장인 | 🔥불꽃엔진 장착으로 S15 클리어 |
| 419 | dm_dev_seal_use | 🔒 | 봉인장막 숙련 | dvuse_dev_seal | 10 | 장치면허 | 골드 | false | 칭호: 봉인장막 숙련자 | 🔒봉인장막 장착으로 10런 시작 |
| 420 | dm_dev_seal_master | 🔒 | 봉인장막 장인 | dvstage_dev_seal | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 봉인장막 장인 | 🔒봉인장막 장착으로 S15 클리어 |
| 421 | dm_dev_safe_use | 🦺 | 안전벨트 숙련 | dvuse_dev_safe | 10 | 장치면허 | 골드 | false | 칭호: 안전벨트 숙련자 | 🦺안전벨트 장착으로 10런 시작 |
| 422 | dm_dev_safe_master | 🦺 | 안전벨트 장인 | dvstage_dev_safe | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 안전벨트 장인 | 🦺안전벨트 장착으로 S15 클리어 |
| 423 | dm_dev_overheat_use | ♨️ | 과열코어 숙련 | dvuse_dev_overheat | 10 | 장치면허 | 골드 | false | 칭호: 과열코어 숙련자 | ♨️과열코어 장착으로 10런 시작 |
| 424 | dm_dev_overheat_master | ♨️ | 과열코어 장인 | dvstage_dev_overheat | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 과열코어 장인 | ♨️과열코어 장착으로 S15 클리어 |
| 425 | dm_dev_subreel_use | ➕ | 보조릴 숙련 | dvuse_dev_subreel | 10 | 장치면허 | 골드 | false | 칭호: 보조릴 숙련자 | ➕보조릴 장착으로 10런 시작 |
| 426 | dm_dev_subreel_master | ➕ | 보조릴 장인 | dvstage_dev_subreel | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 보조릴 장인 | ➕보조릴 장착으로 S15 클리어 |
| 427 | dm_dev_coin_use | 🪙 | 코인투입구 숙련 | dvuse_dev_coin | 10 | 장치면허 | 골드 | false | 칭호: 코인투입구 숙련자 | 🪙코인투입구 장착으로 10런 시작 |
| 428 | dm_dev_coin_master | 🪙 | 코인투입구 장인 | dvstage_dev_coin | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 코인투입구 장인 | 🪙코인투입구 장착으로 S15 클리어 |
| 429 | dm_dev_reroll_use | 🔄 | 재굴림기 숙련 | dvuse_dev_reroll | 10 | 장치면허 | 골드 | false | 칭호: 재굴림기 숙련자 | 🔄재굴림기 장착으로 10런 시작 |
| 430 | dm_dev_reroll_master | 🔄 | 재굴림기 장인 | dvstage_dev_reroll | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 재굴림기 장인 | 🔄재굴림기 장착으로 S15 클리어 |
| 431 | dm_dev_pin_use | 📌 | 고정핀 숙련 | dvuse_dev_pin | 10 | 장치면허 | 골드 | false | 칭호: 고정핀 숙련자 | 📌고정핀 장착으로 10런 시작 |
| 432 | dm_dev_pin_master | 📌 | 고정핀 장인 | dvstage_dev_pin | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 고정핀 장인 | 📌고정핀 장착으로 S15 클리어 |
| 433 | dm_dev_copy_use | 📑 | 복사기 숙련 | dvuse_dev_copy | 10 | 장치면허 | 골드 | false | 칭호: 복사기 숙련자 | 📑복사기 장착으로 10런 시작 |
| 434 | dm_dev_copy_master | 📑 | 복사기 장인 | dvstage_dev_copy | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 복사기 장인 | 📑복사기 장착으로 S15 클리어 |
| 435 | dm_dev_swap_use | 🔃 | 교체기 숙련 | dvuse_dev_swap | 10 | 장치면허 | 골드 | false | 칭호: 교체기 숙련자 | 🔃교체기 장착으로 10런 시작 |
| 436 | dm_dev_swap_master | 🔃 | 교체기 장인 | dvstage_dev_swap | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 교체기 장인 | 🔃교체기 장착으로 S15 클리어 |
| 437 | dm_dev_oracle_use | 🔮 | 예언안경 숙련 | dvuse_dev_oracle | 10 | 장치면허 | 골드 | false | 칭호: 예언안경 숙련자 | 🔮예언안경 장착으로 10런 시작 |
| 438 | dm_dev_oracle_master | 🔮 | 예언안경 장인 | dvstage_dev_oracle | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 예언안경 장인 | 🔮예언안경 장착으로 S15 클리어 |
| 439 | dm_dev_bell_use | 🔔 | 비상졸업벨 숙련 | dvuse_dev_bell | 10 | 장치면허 | 골드 | false | 칭호: 비상졸업벨 숙련자 | 🔔비상졸업벨 장착으로 10런 시작 |
| 440 | dm_dev_bell_master | 🔔 | 비상졸업벨 장인 | dvstage_dev_bell | 15 | 장치면허 | 프리즘 | false | 고급 칭호: 비상졸업벨 장인 | 🔔비상졸업벨 장착으로 S15 클리어 |
| 441 | rc_nocmd10 | 🤐 | 무언의 등반 | noCommandBestStage | 10 | 제한도전 | 골드 | false | 칭호: 무언의 등반가 | 🤐집중/올인/기도/최후 없이 S10 클리어 |
| 442 | rc_nocmd15 | 🤐 | 침묵의 정점 | noCommandBestStage | 15 | 제한도전 | 프리즘 | false | 고급 칭호: 침묵의 정점 | 🤐집중/올인/기도/최후 없이 S15 클리어 |
| 443 | rc_noreroll10 | 🙌 | 운명에 맡긴 등반 | noRerollBestStage | 10 | 제한도전 | 골드 | false | 칭호: 운명에 맡긴 자 | 🙌재굴림/고정/복사/교체 없이 S10 클리어 |
| 444 | rc_noreroll15 | 🙌 | 무조작의 정점 | noRerollBestStage | 15 | 제한도전 | 프리즘 | false | 고급 칭호: 무조작의 정점 | 🙌재굴림/고정/복사/교체 없이 S15 클리어 |
| 445 | cc_focus10 | 🎯 | 집중 투자 | cmdCoin_focus | 10 | 경제 | 실버 | false | 칭호: 집중 투자자 | 🎯집중 명령에 코인 누적 10 지출 |
| 446 | cc_pray30 | 🙏 | 유료 기도 | cmdCoin_pray | 30 | 경제 | 골드 | false | 칭호: 유료 기도자 | 🙏기도 명령에 코인 누적 30 지출 — 운명/연구의 가호를 산 자 |
| 447 | cc_allin50 | 🎲 | 진짜 올인 | cmdCoin_allin | 50 | 경제 | 골드 | false | 칭호: 진짜 도박사 | 🎲올인 명령에 코인 누적 50 지출 |
| 448 | cc_lastclear5 | ⏰ | 마지막 결제 | lastClears | 5 | 경제 | 골드 | false | 칭호: 막판 결제자 | ⏰최후 명령으로 스테이지 5회 클리어 |
| 449 | cc_total100 | 🪙 | 명령비 지출왕 | cmdCoinTotal | 100 | 경제 | 프리즘 | false | 고급 칭호: 명령비 지출왕 | 🪙특수 명령에 코인 누적 100 지출 |
| 450 | cc_bossallin1 | 💸 | 비싼 졸업 | bossAllinClear | 1 | 히든 | 프리즘 | true | 칭호: 비싼 졸업생 | 👑보스에서 🎲올인을 쓰고 클리어 |
| 451 | bdx_intro_growth | 📈 | 성장형 빌드 입문 | bldCat_성장형 | 1 | 빌드도감 | 실버 | false | 칭호: 성장형 입문자 | 성장형 테마빌드를 1개 완성 |
| 452 | bdx_intro_fate | 🔮 | 운명형 빌드 입문 | bldCat_운명형 | 1 | 빌드도감 | 실버 | false | 칭호: 운명형 입문자 | 운명형 테마빌드를 1개 완성 |
| 453 | bdx_intro_reversal | 🧗 | 역전형 빌드 입문 | bldCat_역전형 | 1 | 빌드도감 | 실버 | false | 칭호: 역전형 입문자 | 역전형 테마빌드를 1개 완성 |
| 454 | bdx_intro_combo | 🔗 | 조합형 빌드 입문 | bldCat_조합형 | 1 | 빌드도감 | 실버 | false | 칭호: 조합형 입문자 | 조합형 테마빌드를 1개 완성 |
| 455 | bdx_intro_risk | ☠ | 위험형 빌드 입문 | bldCat_위험형 | 1 | 빌드도감 | 실버 | false | 칭호: 위험형 입문자 | 위험형 테마빌드를 1개 완성 |
| 456 | bdx_master_growth | 📈 | 성장형 빌드 마스터 | bldCat_성장형 | 5 | 빌드도감 | 골드 | false | 칭호: 성장형 마스터 | 성장형 테마빌드 5개 전부 완성 |
| 457 | bdx_master_fate | 🔮 | 운명형 빌드 마스터 | bldCat_운명형 | 5 | 빌드도감 | 골드 | false | 칭호: 운명형 마스터 | 운명형 테마빌드 5개 전부 완성 |
| 458 | bdx_master_reversal | 🧗 | 역전형 빌드 마스터 | bldCat_역전형 | 5 | 빌드도감 | 골드 | false | 칭호: 역전형 마스터 | 역전형 테마빌드 5개 전부 완성 |
| 459 | bdx_master_combo | 🔗 | 조합형 빌드 마스터 | bldCat_조합형 | 5 | 빌드도감 | 골드 | false | 칭호: 조합형 마스터 | 조합형 테마빌드 5개 전부 완성 |
| 460 | bdx_master_risk | ☠ | 위험형 빌드 마스터 | bldCat_위험형 | 5 | 빌드도감 | 골드 | false | 프레임: 위험형 마스터 | 위험형 테마빌드 5개 전부 완성 |
| 461 | bdx_total5 | 📖 | 빌드 수집 시작 | bldTotal | 5 | 빌드도감 | 실버 | false | 칭호: 빌드 수집가 | 테마빌드 누적 5종 완성 |
| 462 | bdx_total10 | 📚 | 빌드 도감 절반 | bldTotal | 10 | 빌드도감 | 골드 | false | 도감 장식: 빌드 책갈피 | 테마빌드 누적 10종 완성 |
| 463 | bdx_total15 | 🗂️ | 빌드 연구가 | bldTotal | 15 | 빌드도감 | 골드 | false | 도감 장식: 빌드 인장 | 테마빌드 누적 15종 완성 |
| 464 | bdx_total25 | 🏆 | 빌드 도감 완성 | bldTotal | 25 | 빌드도감 | 프리즘 | false | 프리즘 칭호: 빌드 도감 완성자 | 테마빌드 25종 전부 완성 |
| 465 | bdx_all_basic | 🎓 | 전공 선택 완료 | bldAllBasic | 5 | 빌드도감 | 골드 | false | 칭호: 전공 선택 완료 | 5개 빌드 카테고리에서 각각 1개+ 완성 |
| 466 | bdx_all_master | 👨‍🏫 | 잭팟런 교수 | bldAllMaster | 5 | 빌드도감 | 프리즘 | false | 프리즘 칭호: 잭팟런 교수 | 5개 빌드 카테고리를 전부 마스터 |


---

## 2. 도전과제/면허 시스템

### 2.1 장치 면허(lic_*) — 12종, `cat = "면허"`

game\SlotV2AchievementsExt.kt:568-585 주석 원문:
> "ACH-5b 장치 면허 — 12 메인 장치 전용 면허 업적 (#9 정합, 2026-06-30).
> key = lic_<deviceId> = composeStat 파생키(면허 조건표의 기존 추적 stat AND → 1/0, 신규 추적/DB 0).
> threshold = 1, tier = 골드(인플레 최소). 달성 = 해당 장치 영구해금(Device.unlockAch 매핑).
> 보조 4 장치(syllabus/holdfile/retake/major)는 면허 미적용 — 기존 업적 매핑 유지."

`lic_dev_<id>` 스탯 키는 **파생 키**(0 또는 1)이며, 그 합성 로직(`composeStat`, 두 조건의 AND)은 `SlotV2Engine.kt`에
있고 본 파일에는 없다. 아래 표의 "조건 원문"은 `desc` 필드에 사람이 읽을 수 있게 적힌 두 조건(`&`로 연결)이며,
실제 AND 판정에 쓰이는 정확한 하위 stat 키·연산자 자체는 확인 불가(미포함 파일).

| id (achievement) | stat key | line | 조건 원문(desc) | 지급 장치(reward) |
|---|---|---|---|---|
| lic_safe | lic_dev_safe | 574 | 아슬아슬 클리어 5회 & S6 도달 — 🦺안전벨트 영구해금 | 🦺안전벨트 장치 영구해금 |
| lic_seal | lic_dev_seal | 575 | 💀해골 누적 200 & S8 도달 — 🔒봉인장막 영구해금 | 🔒봉인장막 장치 영구해금 |
| lic_reroll | lic_dev_reroll | 576 | 보스 3회 클리어 & 막판 클리어 3회 — 🔄재굴림기 영구해금 | 🔄재굴림기 장치 영구해금 |
| lic_pin | lic_dev_pin | 577 | 정확 클리어 3회 & S8 도달 — 📌고정핀 영구해금 | 📌고정핀 장치 영구해금 |
| lic_coin | lic_dev_coin | 578 | 🪙코인 누적 500 & 상점구매 15회 — 🪙코인투입구 영구해금 | 🪙코인투입구 장치 영구해금 |
| lic_subreel | lic_dev_subreel | 579 | 잭팟 5회 & 4세트+ 10회 — ➕보조릴 영구해금 | ➕보조릴 장치 영구해금 |
| lic_overheat | lic_dev_overheat | 580 | 막판 클리어 10회 & 최고점수 20,000 — ♨️과열코어 영구해금 | ♨️과열코어 장치 영구해금 |
| lic_oracle | lic_dev_oracle | 581 | 기도 클리어 3회 & S15 도달 — 🔮예언안경 영구해금 | 🔮예언안경 장치 영구해금 |
| lic_copy | lic_dev_copy | 582 | 프리즘 선택 10회 & 4세트+ 10회 — 📑복사기 영구해금 | 📑복사기 장치 영구해금 |
| lic_swap | lic_dev_swap | 583 | 보스 10회 클리어 & S15 도달 — 🔃교체기 영구해금 | 🔃교체기 장치 영구해금 |
| lic_bell | lic_dev_bell | 584 | 아슬아슬 클리어 30회 & 보스 8회 클리어 — 🔔비상졸업벨 영구해금 | 🔔비상졸업벨 장치 영구해금 |
| lic_flame | lic_dev_flame | 585 | 최고점수 50,000 & S20 도달 — 🔥불꽃엔진 영구해금 | 🔥불꽃엔진 장치 영구해금 |

`dev_own12`(장치를 12종 모두 보유, game\SlotV2AchievementsExt.kt:78)가 "12종"을 명시하므로 위 12개 메인 장치가
"영구 보유 가능 장치"의 전체 모집합이다. 보조 4장치(syllabus/holdfile/retake/major) 중 최소 1개(`dev_holdfile`)의
정확한 id는 `SlotV2RunRow.heldAug` 주석(data\SlotV2Entities.kt:61, "P7·dev_holdfile 보류파일")으로 확인되며, 나머지
3개(`syllabus`/`retake`/`major`)의 정확한 id 문자열과 `devicesOwned` 카운트 포함 여부는 이 4개 파일만으로는 확인 불가.

### 2.2 장치 숙련/장인(dm_*) — 24종 (12장치 × 숙련/장인)

game\SlotV2AchievementsExt.kt:587-593 주석 원문:
> "ACH-5c 장치 숙련/장인 + 무명령/무조작 제한도전 (2026-06-30, 추적코드 선행 완료분 기반).
> 숙련 = dvuse_<deviceId> (장착 런수 inc, launchRun) threshold 10.
> 장인 = dvstage_<deviceId> (장착 도달 최고 클리어 S, clearStage setMax) threshold 15.
> 12 메인 장치 각 숙련/장인 = 24. id 접두 dm_<id>_use / dm_<id>_master (유니크)."

`dvuse_*`는 "해당 장치를 장착하고 런을 시작한 횟수"의 누적(threshold 10, `launchRun` 시점 증가로 주석에 명시),
`dvstage_*`는 "해당 장치를 장착한 채 도달한 최고 클리어 스테이지"의 최댓값(threshold 15, `clearStage`에서
`setMax`로 명시) — 하나는 누적 카운터, 다른 하나는 최댓값 게이지로 갱신 연산 자체가 다르다. C# 포팅 시
`Dictionary<string,long>` 단일 맵에 넣더라도 키별로 "증가(+=)"와 "최댓값 갱신(Max)" 두 갱신 규칙을 구분해야 한다.

| id | stat key | threshold | line | desc |
|---|---|---|---|---|
| dm_dev_flame_use | dvuse_dev_flame | 10 | 597 | 🔥불꽃엔진 장착으로 10런 시작 |
| dm_dev_flame_master | dvstage_dev_flame | 15 | 598 | 🔥불꽃엔진 장착으로 S15 클리어 |
| dm_dev_seal_use | dvuse_dev_seal | 10 | 600 | 🔒봉인장막 장착으로 10런 시작 |
| dm_dev_seal_master | dvstage_dev_seal | 15 | 601 | 🔒봉인장막 장착으로 S15 클리어 |
| dm_dev_safe_use | dvuse_dev_safe | 10 | 603 | 🦺안전벨트 장착으로 10런 시작 |
| dm_dev_safe_master | dvstage_dev_safe | 15 | 604 | 🦺안전벨트 장착으로 S15 클리어 |
| dm_dev_overheat_use | dvuse_dev_overheat | 10 | 606 | ♨️과열코어 장착으로 10런 시작 |
| dm_dev_overheat_master | dvstage_dev_overheat | 15 | 607 | ♨️과열코어 장착으로 S15 클리어 |
| dm_dev_subreel_use | dvuse_dev_subreel | 10 | 609 | ➕보조릴 장착으로 10런 시작 |
| dm_dev_subreel_master | dvstage_dev_subreel | 15 | 610 | ➕보조릴 장착으로 S15 클리어 |
| dm_dev_coin_use | dvuse_dev_coin | 10 | 612 | 🪙코인투입구 장착으로 10런 시작 |
| dm_dev_coin_master | dvstage_dev_coin | 15 | 613 | 🪙코인투입구 장착으로 S15 클리어 |
| dm_dev_reroll_use | dvuse_dev_reroll | 10 | 615 | 🔄재굴림기 장착으로 10런 시작 |
| dm_dev_reroll_master | dvstage_dev_reroll | 15 | 616 | 🔄재굴림기 장착으로 S15 클리어 |
| dm_dev_pin_use | dvuse_dev_pin | 10 | 618 | 📌고정핀 장착으로 10런 시작 |
| dm_dev_pin_master | dvstage_dev_pin | 15 | 619 | 📌고정핀 장착으로 S15 클리어 |
| dm_dev_copy_use | dvuse_dev_copy | 10 | 621 | 📑복사기 장착으로 10런 시작 |
| dm_dev_copy_master | dvstage_dev_copy | 15 | 622 | 📑복사기 장착으로 S15 클리어 |
| dm_dev_swap_use | dvuse_dev_swap | 10 | 624 | 🔃교체기 장착으로 10런 시작 |
| dm_dev_swap_master | dvstage_dev_swap | 15 | 625 | 🔃교체기 장착으로 S15 클리어 |
| dm_dev_oracle_use | dvuse_dev_oracle | 10 | 627 | 🔮예언안경 장착으로 10런 시작 |
| dm_dev_oracle_master | dvstage_dev_oracle | 15 | 628 | 🔮예언안경 장착으로 S15 클리어 |
| dm_dev_bell_use | dvuse_dev_bell | 10 | 630 | 🔔비상졸업벨 장착으로 10런 시작 |
| dm_dev_bell_master | dvstage_dev_bell | 15 | 631 | 🔔비상졸업벨 장착으로 S15 클리어 |

### 2.3 장치 관련 기타 업적 (면허 카테고리 아님, 참고용)

`deviceUses`(장치 총 사용횟수) · `devicesOwned`(영구 보유 장치 종수) · `rerollUses` · `pinUses`를 조건으로 쓰는
비-면허 업적 (game\SlotV2AchievementsExt.kt:74-80, 354-355, 366-367, 371):

| id | key | threshold | line | desc | reward |
|---|---|---|---|---|---|
| dev_use10 | deviceUses | 10 | 74 | 장치를 10회 사용 | 칭호: 장치 애호가 |
| dev_use50 | deviceUses | 50 | 75 | 장치를 50회 사용 | 칭호: 기계공 |
| dev_own1 | devicesOwned | 1 | 76 | 장치를 1종 영구 보유 | 도감 등록 |
| dev_own5 | devicesOwned | 5 | 77 | 장치를 5종 영구 보유 | 칭호: 장치 수집가 |
| dev_own12 | devicesOwned | 12 | 78 | 장치를 12종 모두 보유 | 칭호: 장치 마스터 |
| dev_reroll10 | rerollUses | 10 | 79 | 재굴림 10회 사용 | 칭호: 재굴림 중독 |
| dev_pin10 | pinUses | 10 | 80 | 고정 10회 사용 | 칭호: 고정의 달인 |
| ec_reroll5 | rerollUses | 5 | 354 | 재굴림 5회 사용 | 도감 등록 |
| ec_reroll30 | rerollUses | 30 | 355 | 재굴림 30회 사용 | 칭호: 운명 거부자 |
| ec_dev30 | deviceUses | 30 | 366 | 장치를 30회 사용 | 칭호: 장치 숙련공 |
| ec_dev100 | deviceUses | 100 | 367 | 장치를 100회 사용 | 고급 칭호: 장치의 대가 |
| ec_dev_own8 | devicesOwned | 8 | 371 | 장치를 8종 영구 보유 | 칭호: 장치 보유 8종 |

### 2.4 상시 도전판(allChallenges) — 웹 연동 참고

game\SlotV2WebService.kt:135-149(`buildNode` 내부)는 `SlotV2Engine.allChallenges(stat)`을 순회하며 `jackpotdex/<t>`
노드의 `challenges` 배열을 만든다. 각 항목은 `kind`(DEVICE/CHAR/MACHINE/STANDARD 4종), `done`, `cur`/`max`(다조건
AND 중 **진행률이 가장 낮은 병목 조건**의 현재/목표 — `bottleneck()` 함수, game\SlotV2WebService.kt:85-90),
`pct`(`reqProgress` 평균 × 100, 정의는 미포함), `reward`(힌트 문자열)를 갖는다. `allChallenges()`/`Challenge` 구조
자체는 `SlotV2Engine.kt`(미포함)에 있어 필드 스펙만 확인 가능하고, 그 안에 무엇이 몇 개 들어있는지(예: 12 면허 +
캐릭/머신 복합해금 + 표준도전이 각각 몇 개인지)는 이 4개 파일만으로는 확인 불가.

---

## 3. 영속 상태 스키마 (Room → Unity 로컬 JSON 설계 기준)

세 엔티티 모두 `com.ashersoft.kakaobot.data` 패키지, data\SlotV2Entities.kt. Kotlin data class 필드는 선언 순서
그대로 아래 표에 옮겼다(원본 그대로, 재배열 없음). "기본값" 열은 Kotlin 프로퍼티의 `=` 기본값이며, 없으면 필수
(위치 인자) 필드다.

### 3.1 `slot_v2_run` — `SlotV2RunRow` (data\SlotV2Entities.kt:14-79)

`@Entity(tableName = "slot_v2_run", primaryKeys = ["linkId", "ownerKey"])` (data\SlotV2Entities.kt:13)

클래스 주석(data\SlotV2Entities.kt:6-12) 원문:
> "잭팟런 v2 (단일라인 5칸 로그라이크) — 진행 중인 런 상태 (한 사람당 1런, 휘발).
> 3화폐: stageExp(이번 스테이지 진행) / score(리더보드 누적) / coins(상점, 휘발).
> 캐릭터+머신 선택 → 스핀 5회 안에 요구 EXP 달성 → 노드 선택 → 반복. 실패=게임오버.
> v1(slot_run)과 별도 테이블로 병행. ownerKey = "u<userId>"/"n<nick>"."

| # | 필드 | 타입 | 기본값 | 라인 | 의미(원문 주석) |
|---|---|---|---|---|---|
| 1 | linkId | Long | (PK, 필수) | 15 | — |
| 2 | ownerKey | String | (PK, 필수) | 16 | "u<userId>" 또는 "n<nick>" 형식(클래스 주석) |
| 3 | ownerNick | String | (필수) | 17 | — |
| 4 | ownerUserId | Long | 0 | 18 | — |
| 5 | state | String | "CHAR_SELECT" | 20 | CHAR_SELECT / MACHINE_SELECT / SPIN / NODE_SELECT / EVENT_AUGMENT / EVENT_RELIC / EVENT_SHOP / EVENT_ITEMSHOP / EVENT_GAMBLE / EVENT_REST / EVENT_CURSE (19줄 주석 열거) |
| 6 | charId | String | "" | 21 | — |
| 7 | machineId | String | "" | 22 | — |
| 8 | stage | Int | 1 | 23 | — |
| 9 | spinIndex | Int | 0 | 24 | 이번 스테이지에 쓴 스핀 수 |
| 10 | stageExp | Long | 0 | 25 | 이번 스테이지 누적 EXP (쿼터 관문) |
| 11 | score | Long | 0 | 26 | 런 누적 점수 (리더보드) |
| 12 | coins | Long | 0 | 27 | — |
| 13 | perks | String | "" | 28 | 증강/유물 id CSV |
| 14 | curses | String | "" | 29 | 저주 id CSV |
| 15 | items | String | "" | 30 | (예비) 보유 소모성 아이템 id CSV |
| 16 | armItems | String | "" | 31 | NEXTSPIN 아이템 — 다음 스핀에 적용 후 소거 |
| 17 | phaseItems | String | "" | 32 | PHASE 아이템 — 이번 스테이지 내내, 클리어 시 소거 |
| 18 | stageBonusSpins | Int | 0 | 33 | 이번 스테이지 한정 추가 스핀(응급처치), 클리어 시 0 |
| 19 | usedCmds | String | "" | 34 | 이번 스테이지에 쓴 특수 스핀명령/장치 CSV, 클리어 시 0 |
| 20 | device | String | "" | 35 | 장착 장치 id (메인 장치 슬롯 — 모든 장치 가능) |
| 21 | device2 | String | "" | 36 | 보조 장치 id (극후반 해금 슬롯 — ARMED/PEEK만·약화·계열제한). ""=미장착/미해금 |
| 22 | pendingOptions | String | "" | 37 | 현재 선택지 직렬화 |
| 23 | flameNext | Boolean | false | 38 | 다음 스핀 EXP -50% |
| 24 | seedNext | Boolean | false | 39 | 다음 스핀 씨앗 성장 |
| 25 | lastCells | String | "" | 41 | 직전 스핀 원시 심볼 id CSV (재굴림/고정/복사/교체) |
| 26 | lastGain | Long | 0 | 42 | 직전 스핀이 더한 EXP (조작 시 되돌림) |
| 27 | lastScoreGain | Long | 0 | 43 | 직전 스핀이 더한 점수 |
| 28 | lastCoinGain | Int | 0 | 44 | 직전 스핀이 더한 코인 |
| 29 | lastSet4 | Int | 0 | 45 | 직전 스핀이 runSet4 에 더한 기여(0/1) — 재굴림/조작 교체 시 net-adjust(되돌림) |
| 30 | lastAdjPairs | Int | 0 | 46 | 직전 스핀이 runAdjPairs 에 더한 기여(0/1) — 재굴림/조작 교체 시 net-adjust(되돌림) |
| 31 | lastSpinNo | Int | -1 | 47 | 직전 스핀의 spinIndex(0-base), -1=없음 |
| 32 | pendingNextExpMul | Double | 1.0 | 48 | 다음 스핀 EXP 배수(과열 여파 등), 적용 후 1.0 |
| 33 | lockedNext | String | "" | 49 | 예언으로 확정된 다음 스핀 원시 심볼 id CSV |
| 34 | devCooldown | Int | 0 | 50 | 장치 충전(쿨다운) 남은 스테이지 (점화) |
| 35 | runJackpots | Int | 0 | 52 | 이번 런 잭팟 횟수 |
| 36 | runBestSpin | Long | 0 | 53 | 이번 런 한 스핀 최고 EXP |
| 37 | displayMode | String | "NORMAL" | 54 | 표시 모드: SIMPLE(간단)/NORMAL(상세) |
| 38 | runSymCounts | String | "" | 55 | 이번 런 심볼 등장수 "id:n,id:n" (실패 리포트 최다심볼) |
| 39 | unluckyGauge | Int | 0 | 56 | 불운 게이지(나쁜 스핀 누적) — 가득 차면 다음 보상 희귀↑ 보장 |
| 40 | closestClear | Int | -1 | 57 | 이번 런 가장 아슬아슬한 클리어 마진(초과EXP 최소), -1=없음 |
| 41 | survive | Boolean | false | 58 | (f) 보험증서 — 이번 스테이지 실패 1회 생존 |
| 42 | debtStages | Int | 0 | 59 | (i) 빚문서 — 남은 무보상 스테이지 수 |
| 43 | phasePerks | String | "" | 60 | (k) 깨진프리즘 — 이번 스테이지 한정 임시 perk CSV(클리어 소거) |
| 44 | heldAug | String | "" | 61 | (P7·dev_holdfile 보류파일) 보관 중인 증강 후보 id 1개(""=없음). 다음 증강 노드서 새 후보와 함께 비교. 런종료/클리어 시 소거 가능 |
| 45 | usedItemThisRun | Boolean | false | 62 | 이번 런에 아이템을 1개라도 썼는지 (수도승 '아이템 없이 S8' 런조건 트래킹) |
| 46 | runAdjPairs | Int | 0 | 64 | 인접쌍 보너스 발동 횟수 (연쇄반응 bld_chain) |
| 47 | runPrayWins | Int | 0 | 65 | 기도 성공 횟수 (운명의손 bld_fate_hand) |
| 48 | runLastSpinClears | Int | 0 | 66 | 막스핀 클리어 횟수 (벼랑끝합격 bld_cliff_pass) |
| 49 | runCloseClears | Int | 0 | 67 | 아슬아슬(부족≤10) 클리어 횟수 |
| 50 | runFastClears | Int | 0 | 68 | 남은스핀≥2 클리어 횟수 (빠른입학 bld_fast_start) |
| 51 | runSet4 | Int | 0 | 69 | 세트4+ 발동 횟수 (끌어당기는졸업 bld_magnet_grad) |
| 52 | growthStack | Int | 0 | 71 | 성장일지 스택 (0~5, 클리어 누적·실패 리셋) |
| 53 | snowStack | Int | 0 | 72 | 눈덩이 스택 (0~4, 남은스핀≥2 클리어 누적·보스후 -1) |
| 54 | fateBellUsed | Int | 0 | 73 | 운명의종 사용 여부 (0/1, 런 1회) |
| 55 | runUsedCmd | Int | 0 | 75 | 이번 런 스핀명령(focus/allin/pray/last 등) 사용=1 |
| 56 | runRerolled | Int | 0 | 76 | 이번 런 재굴림/고정/복사/교체 장치 사용=1 |
| 57 | startedAt | Long | 0 | 77 | — |
| 58 | lastActionAt | Long | 0 | 78 | — |

### 3.2 `slot_v2_ach` — `SlotV2AchRow` (data\SlotV2Entities.kt:87-105)

`@Entity(tableName = "slot_v2_ach", primaryKeys = ["linkId", "ownerKey"], indices = [Index(value = ["linkId", "userId"])])`
(data\SlotV2Entities.kt:82-86). 클래스 주석(81줄): "잭팟런 v2 업적/누적 카운터 (플레이어별 영구 누적)."

| # | 필드 | 타입 | 기본값 | 라인 | 의미 |
|---|---|---|---|---|---|
| 1 | linkId | Long | (PK, 필수) | 88 | — |
| 2 | ownerKey | String | (PK, 필수) | 89 | — |
| 3 | ownerNick | String | "" | 90 | — |
| 4 | userId | Long? | null | 91 | nullable — 인덱스(linkId,userId) 대상 |
| 5 | cherryTotal | Long | 0 | 92 | 전용 컬럼(누적 카운터) |
| 6 | crownTotal | Long | 0 | 93 | 전용 컬럼 |
| 7 | jackpots | Long | 0 | 94 | 전용 컬럼 |
| 8 | bossClears | Long | 0 | 95 | 전용 컬럼 |
| 9 | lastSpinClears | Long | 0 | 96 | 전용 컬럼 |
| 10 | exactClears | Long | 0 | 97 | 전용 컬럼 |
| 11 | prismPicks | Long | 0 | 98 | 전용 컬럼 |
| 12 | bestStage | Long | 0 | 99 | 전용 컬럼 |
| 13 | runs | Long | 0 | 100 | 전용 컬럼 |
| 14 | bestScore | Long | 0 | 101 | 전용 컬럼 |
| 15 | unlocked | String | "" | 102 | 달성한 업적 id CSV |
| 16 | counters | String | "" | 103 | 확장 카운터 맵 "key:val,key:val" (bookTotal/deviceUses/closeClears… — 5행~14행 전용 컬럼 10개를 제외한 나머지 146개 key가 여기 저장되는 것으로 추정) |
| 17 | lastAt | Long | 0 | 104 | — |

**중요**: `cherryTotal, crownTotal, jackpots, bossClears, lastSpinClears, exactClears, prismPicks, bestStage, runs,
bestScore` 10개(위 5~14행)만 Room의 전용 Long 컬럼이고, 나머지 146개 stat 키(`bookTotal`, `deviceUses`,
`cstage_*`, `mstage_*`, `lic_dev_*`, `dvuse_*`, `dvstage_*` 등)는 `counters` 문자열 컬럼 안에 `"key:val,key:val,..."`
CSV로 직렬화되어 있다. C# 포팅 시 이 구분(전용 필드 vs. CSV 맵)을 유지할지, 전부 `Dictionary<string,long>` 하나로
통합할지는 설계 결정 사항 — 원본은 전용 컬럼 10개 + CSV 맵 혼합 구조다.

### 3.3 `slot_v2_score` — `SlotV2ScoreRow` (data\SlotV2Entities.kt:113-130)

`@Entity(tableName = "slot_v2_score", primaryKeys = ["linkId", "nickname"], indices = [Index(value = ["linkId", "userId"])])`
(data\SlotV2Entities.kt:108-112). 클래스 주석(107줄): "잭팟런 v2 최고기록 + 통산 (리더보드 — user_points 무관)."

| # | 필드 | 타입 | 기본값 | 라인 | 의미 |
|---|---|---|---|---|---|
| 1 | linkId | Long | (PK, 필수) | 114 | — |
| 2 | nickname | String | (PK, 필수) | 115 | — |
| 3 | bestScore | Long | 0 | 116 | — |
| 4 | totalScore | Long | 0 | 117 | — |
| 5 | runs | Int | 0 | 118 | — |
| 6 | bestStage | Int | 0 | 119 | — |
| 7 | bestChar | String | "" | 120 | — |
| 8 | bestMachine | String | "" | 121 | — |
| 9 | lastPlayedAt | Long | 0 | 122 | — |
| 10 | userId | Long? | null | 123 | nullable — 인덱스(linkId,userId) 대상 |
| 11 | ownedDevices | String | "" | 124-125 | 영구 소지 장치 id CSV — 런/업적으로 획득, 런 끝나도 유지. 시작 시 장착 선택 |
| 12 | pinnedChallenge | String | "" | 126-127 | 고정한 도전 id (배치3a, P4) — "목표 <번호>" 로 1개 고정. 리셋/만료 없음. 빈=""=미고정 |
| 13 | lastCombo | String | "" | 128-129 | 직전 런 조합 CSV "char,machine,device,device2" (지시서11-B 같은조합 재도전). recordRun 시 저장. 빈=""=없음 |

**설계 노트**: 이 테이블의 PK는 `(linkId, nickname)`이지 `(linkId, userId)`가 아니다 — 즉 동일 유저가 닉네임을 바꾸면
새 행이 생길 수 있는 구조(3.4절 `topByBest` 쿼리가 이를 `userId` 기준으로 재병합하는 이유).

### 3.4 DAO 쿼리 패턴 (data\SlotV2Dao.kt)

#### `SlotV2RunDao` (data\SlotV2Dao.kt:8-24)
| 메서드 | 쿼리/동작 | 라인 |
|---|---|---|
| `find(linkId, ownerKey)` | `SELECT * FROM slot_v2_run WHERE linkId = :linkId AND ownerKey = :ownerKey` | 10 |
| `findByUserId(linkId, userId)` | `SELECT * FROM slot_v2_run WHERE linkId = :linkId AND ownerUserId = :userId AND ownerUserId > 0 LIMIT 1` | 13 |
| `upsert(row)` | `@Insert(onConflict = OnConflictStrategy.REPLACE)` | 16-17 |
| `delete(linkId, ownerKey)` | `DELETE FROM slot_v2_run WHERE linkId = :linkId AND ownerKey = :ownerKey` | 19-20 |
| `purgeExpired(before)` | `DELETE FROM slot_v2_run WHERE lastActionAt < :before` | 22-23 |

#### `SlotV2AchDao` (data\SlotV2Dao.kt:26-36)
| 메서드 | 쿼리/동작 | 라인 |
|---|---|---|
| `find(linkId, ownerKey)` | `SELECT * FROM slot_v2_ach WHERE linkId = :linkId AND ownerKey = :ownerKey` | 28 |
| `findByUserId(linkId, userId)` | `SELECT * FROM slot_v2_ach WHERE linkId = :linkId AND userId = :userId LIMIT 1` | 31 |
| `upsert(row)` | `@Insert(onConflict = OnConflictStrategy.REPLACE)` | 34-35 |

#### `SlotV2ScoreDao` (data\SlotV2Dao.kt:38-68)
| 메서드 | 쿼리/동작 | 라인 |
|---|---|---|
| `find(linkId, nickname)` | `SELECT * FROM slot_v2_score WHERE linkId = :linkId AND nickname = :nickname` | 40 |
| `findByUserId(linkId, userId)` | `SELECT * FROM slot_v2_score WHERE linkId = :linkId AND userId = :userId LIMIT 1` | 43 |
| `upsert(row)` | `@Insert(onConflict = OnConflictStrategy.REPLACE)` | 46-47 |
| `allForLink(linkId)` | `SELECT * FROM slot_v2_score WHERE linkId = :linkId` | 49-50 |
| `topByBest(linkId, limit)` | 아래 참조(랭킹 쿼리) | 52-67 |

**`topByBest` 랭킹 쿼리 원문 그대로** (data\SlotV2Dao.kt:52-67):
```sql
SELECT linkId, MAX(nickname) AS nickname, MAX(bestScore) AS bestScore,
       SUM(totalScore) AS totalScore, SUM(runs) AS runs, MAX(bestStage) AS bestStage,
       MAX(bestChar) AS bestChar, MAX(bestMachine) AS bestMachine,
       MAX(lastPlayedAt) AS lastPlayedAt, userId, MAX(ownedDevices) AS ownedDevices,
       MAX(pinnedChallenge) AS pinnedChallenge, MAX(lastCombo) AS lastCombo
  FROM slot_v2_score
 WHERE linkId = :linkId AND userId IS NOT NULL
 GROUP BY linkId, userId
 UNION ALL
SELECT linkId, nickname, bestScore, totalScore, runs, bestStage,
       bestChar, bestMachine, lastPlayedAt, userId, ownedDevices, pinnedChallenge, lastCombo
  FROM slot_v2_score WHERE linkId = :linkId AND userId IS NULL
 ORDER BY bestScore DESC LIMIT :limit
```
동작 요약(원문 구조 해설, 창작 아님):
1. `userId IS NOT NULL`인 행들을 `(linkId, userId)`로 `GROUP BY` — 동일 유저가 닉네임을 바꿔 여러 행이 생긴 경우
   `bestScore/bestStage/…`는 `MAX`, `totalScore/runs`는 `SUM`으로 **병합**한다.
2. `userId IS NULL`인 행(닉네임 전용, 미인증 유저)은 그대로 통과.
3. 두 결과를 `UNION ALL`한 뒤 전체를 `bestScore DESC`로 정렬해 `LIMIT :limit`.

이 쿼리가 웹 랭킹(4.2절 `jackpotdex`의 `rank` 배열)과 시즌 아카이브(4.5절 `jackpothall/seasons/<key>`)의 공통
데이터 소스다(`SlotV2Service.topByBest` 경유, game\SlotV2WebService.kt:241, 298).

---

## 4. RTDB 스키마 (Firebase Realtime Database, game\SlotV2WebService.kt)

### 4.1 프로젝트/베이스 URL

- 웹 앱: `https://jackpotrun-web.web.app` (WEB_BASE, 라인 14) — "독립 프로젝트로 분리(구 mokabot-8ed4d.web.app)"
- 도감(읽기 전용) 웹 경로: `$WEB_BASE/jackpotdex` (DEX, 라인 15)
- 선택 전용 웹 경로: `$WEB_BASE/jackpotpick` (PICK, 라인 16)
- RTDB 베이스: `https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app` (JP_RTDB, 라인 18) —
  주석(17줄): "잭팟 전용 RTDB — 분리 프로젝트 jackpotrun-web. FirebaseRtdb 는 대시보드/마피아/낚시 공유라 BASE 를
  못 바꿈 → 호출별 base override." 모든 RTDB 호출은 `FirebaseRtdb.get/put/delete(path, JP_RTDB)` 형태로 base를
  명시적으로 override한다(공용 `FirebaseRtdb` 객체 자체는 다른 프로젝트와 공유).

### 4.2 `jackpotdex/<t>` — 플레이어 상태 노드 (읽기 전용, 도감/공개 자랑용)

생성: `buildNode(linkId, nickname, userId)` (game\SlotV2WebService.kt:93-263), push 지점: `linkDex`/`linkPick`/`sync`
(라인 307-335). 필드(JSONObject, `put` 호출 순서 그대로):

| 필드 | 타입 | 라인 | 설명 |
|---|---|---|---|
| `nick` | string | 98 | nickname 그대로 |
| `title` | string | 99 | `SlotV2Engine.titleStr(best)` — 칭호 문자열(로직 미포함 파일) |
| `bestScore` | long | 100 | `sc?.bestScore ?: 0L` |
| `bestStage` | int | 101 | `sc?.bestStage ?: 0` |
| `runs` | int | 102 | `sc?.runs ?: 0` |
| `totalScore` | long | 103 | `sc?.totalScore ?: 0L` |
| `updatedAt` | long(ms) | 104 | `System.currentTimeMillis()` |
| `ach` | object | 106-116 | achId를 키로 하는 객체 맵. 각 값은 done(bool)/cur(min(counter,threshold))/max(threshold) 3필드. `SlotV2Engine.ACHIEVEMENTS` 전체(확장 466 + 기본 16 추정) 순회. `unlocked` set은 `ach.unlocked` CSV 파싱(3.2절) |
| `achDone` | int | 117 | `unlocked.size` |
| `achTotal` | int | 118 | `SlotV2Engine.ACHIEVEMENTS.size` |
| `used` | object | 120-125 | id를 키로, 열람/사용 횟수를 값으로 하는 맵 — `ach.counters`에서 `seen_` 접두 키만 추출, 접두 제거 후 매핑(도감 열람 기록) |
| `chars` | array\<string\> | 127-129 | `SlotV2Engine.unlockedChars(stat)`의 id 목록 |
| `machines` | array\<string\> | 130 | `SlotV2Engine.unlockedMachines(stat)`의 id 목록 |
| `ownedDevices` | array\<string\> | 131-132 | `SlotV2Service.equipableDeviceIds(...)` = "면허취득(영구) ∪ 기존 보유(grandfather)" |
| `challenges` | array\<object\> | 135-149 | 항목당 id/e/n/kind(DEVICE·CHAR·MACHINE·STANDARD)/done/cur/max/pct/reward 9필드 — 2.4절 참조 |
| `mastery` | object | 151-166 | chars/machines 두 배열. 각 원소는 id/medal(none·bronze·silver·gold)/stage 3필드 |
| `builddex` | array\<object\> | 168-173 | 항목당 c(charId)/m(machineId)/stage 3필드 — 플레이해본 (캐릭,머신) 조합과 그 조합 최고 스테이지 |
| `buildDexTotal` | int | 174 | `SlotV2Engine.buildDexTotal()` |
| `themeBuilds` | array\<object\> | 176-187 | 항목당 id/name/e/category/done/cond 6필드 — 카테고리별 25종(성장형/운명형/역전형/조합형/위험형), done = stat[bld_<id>] > 0 |
| `themeBuildDone` | int | 188 | — |
| `themeBuildTotal` | int | 189 | — |
| `records` | array\<string\> | 192 | `SlotV2Engine.recordLines(stat)` — "라벨: 값" 형식 라인들 |
| `pinned` | string | 195 | `sc?.pinnedChallenge ?: ""` (3.3절 `pinnedChallenge` 그대로) |
| `unlock` | object | 197-219 | chars/machines/devices 세 객체. 각각 id를 키로, t(힌트)/pct(reqProgress×100)/done 3필드를 값으로 — 잠금 상태 항목만 포함 |
| `accountLevel` | ? | 222 | `SlotV2Engine.accountLevel(stat)` |
| `accountExp` | ? | 223 | `SlotV2Engine.accountExp(stat)` |
| `perkUnlock` | object | 226-237 | augments/relics 두 배열. 각 원소는 id/locked/school/hint 4필드 |
| `rank` | array\<object\> | 239-261 | TOP10, 항목당 nick/score/stage 3필드 — 아래 "닉네임 표시 규칙" 참조 |

**닉네임 표시 규칙(rank 배열, game\SlotV2WebService.kt:239-260)** — 원문 그대로:
- `dispNick(nick)`: `nick`이 `"user_"`로 시작하면 `"uid_" + (user_ 제거한 나머지)`로 치환. 그 외 `nick.length >= 20`이고
  정규식 `^[A-Za-z0-9+/=]+$` 전체 매치(base64 형태로 추정)면 `"(알수없음)"`으로 치환. 그 외는 원본 그대로.
- 서로 다른 `userId`(0보다 큰 값)를 가진 두 랭커가 `dispNick` 결과가 같은 표시닉이 되면, **먼저 등장한(=랭킹이 높은,
  `bestScore DESC` 정렬 기준) 쪽이 원래 닉을 갖고, 이후 등장하는 쪽부터 순서대로 숫자 접미사**(2, 3, …)가 붙는다.
  동일 `userId`가 이미 표시닉을 배정받았으면 그 표시닉을 재사용한다(`byUid` 캐시).

### 4.3 `jackpotcmd/<w>` — 웹→봇 선택 핸드셰이크 노드 (쓰기 전용, 휘발)

이 노드는 **웹 클라이언트가 쓰고 봇이 읽어서 즉시 삭제**하는 단방향 큐이다. 봇 쪽 코드에서 직접 `put`하는 부분은
본 파일에 없음(웹 쪽에서 기록). 봇이 읽는 코드: `consumeWebPick` (game\SlotV2WebService.kt:60-81).

읽어들이는 JSON 필드(`cmd.optString(...)`, 라인 68-78):
| 필드 | 타입 | 라인 | 설명 |
|---|---|---|---|
| `cid` | string | 68 | 클라이언트 식별자 — bind-window 검증에 사용(4.6절) |
| `char` | string | 77 | 선택한 캐릭터 id |
| `machine` | string | 77 | 선택한 머신 id |
| `device` | string | 78 | 선택한 장치 id, ""=미장착. 주석: "웹에서 고른 장치(영구 소지 중 1개, ""=미장착)" |

소비 흐름(원문 그대로, 라인 60-81):
1. `userKey(linkId, nick, userId)`로 해당 유저의 현재 활성 쓰기토큰 `t`를 `userActiveWrite` 맵에서 조회. 없으면 null 반환.
2. `writeTokens[t]`(`WriteCtx`) 조회. 없으면 null.
3. `now - ctx.issuedAt > WRITE_TTL_MS`(60분)면 토큰 만료 → 두 맵에서 제거하고 null 반환.
4. `FirebaseRtdb.get("jackpotcmd/$t", JP_RTDB)`로 노드 조회, 실패 시 null.
5. JSON 파싱 실패 시 null.
6. `cid` 추출. `ctx.boundCid != null && ctx.boundCid != cid`(다른 기기가 같은 토큰 재사용 시도) → 노드 삭제 후 null.
7. `ctx.boundCid == null`(최초 사용)이면: `now - ctx.createdAt > BIND_WINDOW_MS`(5분, 발급 시각 기준)면 노드 삭제
   후 null, 아니면 `ctx.boundCid = cid`로 바인딩.
8. `ctx.issuedAt = now`로 갱신(리프레시).
9. `char`/`machine`/`device` 추출 후 **노드를 무조건 삭제**(`FirebaseRtdb.delete`).
10. `char`/`machine`이 모두 비어있지 않으면 (char, machine, device) 3-튜플 반환, 아니면 null.

### 4.4 `jackpotcatalog` — 전체 카탈로그 (공유 정적 노드, 1개만 존재)

생성: `buildCatalog()` (game\SlotV2WebService.kt:266-293), push: `pushCatalog()` (라인 294,
`FirebaseRtdb.put("jackpotcatalog", ...)`). "엔진이 소스, 공유 노드에 1회 push"(주석) — 플레이어별이 아닌 전역
정적 데이터.

| 필드 | 배열 원소 필드 구성 | 라인 |
|---|---|---|
| `augments` | id / e / n / d / t(tier.name) | 271 |
| `relics` | id / e / n / d / t | 272 |
| `curses` | id / e / n / d / t | 273 |
| `items` | id / e / n / d / k(kind.name) | 274-276 |
| `sets` | id / n / d | 277-279 |
| `devices` | id / e / n / d / cmd / kind(kind.name) | 280-282 |
| `chars` | id / e / n / d | 283-285 |
| `machines` | id / e / n / d | 286-288 |
| `achievements` | id / e / n / d / cat / tier / reward / hidden | 289-291 |

**주의**: `achievements` 배열은 `hidden=true`인 항목도 그대로 포함해서 push한다(필터링 없음). 같은 파일 다른
곳(라인 226 인근, `perkUnlockArr`)에는 "데이터 제공까지(클라 마스킹은 별도)"라는 주석이 있어, **히든 업적의
마스킹(이름/설명 은닉)은 서버가 아니라 클라이언트(웹) 책임**임을 명시하고 있다 — 즉 RTDB에는 히든 업적의 이름·
설명·보상이 평문으로 노출된다. Unity 포팅 시 로컬 저장이든 서버 연동이든 이 마스킹 정책을 그대로 따를지 재검토 필요.

### 4.5 `jackpothall/seasons/<key>` — 시즌 아카이브 (명예의 전당)

생성/저장: `archiveSeason(linkId, key, label, tsMs)` (game\SlotV2WebService.kt:297-304).

| 필드 | 타입 | 라인 | 설명 |
|---|---|---|---|
| `label` | string | 301 | 시즌 라벨(호출자 인자, 예: 월간명) |
| `ts` | long(ms) | 301 | 아카이브 시각(호출자 인자) |
| `top` | array\<object\> | 299-301 | 항목당 rank(1-base)/nick/score/stage 4필드 — `SlotV2Service.topByBest(linkId, 10)` 결과를 인덱스+1로 rank 부여 |

반환값: 아카이브된 인원 수(`top.size`, 라인 303). 함수 시그니처상 `key`는 호출자가 결정(예: "2026-07" 같은 월
키로 추정 — 호출부는 본 파일에 없어 확인 불가).

### 4.6 토큰 발급 규칙

**두 종류의 토큰이 있고 서로 알고리즘이 다르다** — 혼동 주의.

#### (A) 읽기 토큰 t — `token()` (game\SlotV2WebService.kt:21-24), `jackpotdex/<t>` 경로에 사용

원문(Kotlin):
```
fun token(linkId: Long, nickname: String, userId: Long?): String {
    val uid = if (userId != null && userId != 0L) userId else nickname.hashCode().toLong()
    return "%08x%08x".format(linkId.hashCode(), uid.hashCode())
}
```
- **결정론적**(같은 입력 → 항상 같은 토큰). `linkId.hashCode()`와 `uid.hashCode()`를 각각 8자리 16진수로 포맷 →
  총 16 hex 문자(구분자 없이 연결). TTL 없음(재계산 가능하면 항상 유효, 영구).
  주석(라인 20): "(linkId,userId) 안정 토큰 — fishdex 와 동일 공식. **읽기(도감) 전용** — 공개 자랑용."
- 보안 성격: 같은 방 멤버는 `linkId`/닉네임을 알면 동일 토큰을 재계산할 수 있음(주석 라인 27-29) → 그래서
  "쓰기"에는 이 토큰을 쓰지 않고 별도 랜덤 토큰(B)을 쓴다.

#### (B) 쓰기 토큰 w — `issueWriteToken()` (game\SlotV2WebService.kt:44-53), `jackpotcmd/<w>` 경로에 사용

```
private const val WRITE_TTL_MS = 60 * 60 * 1000L   // 60분
private const val BIND_WINDOW_MS = 5 * 60 * 1000L  // 발급 후 첫 cid 바인딩 허용 창(묵힌 링크 선점 차단)
```
- 생성: `java.util.UUID.randomUUID().toString().replace("-", "").take(24)` → **24-hex 문자열**(하이픈 제거한 UUID
  32-hex 중 앞 24자만 사용, 원본 주석상 "~96bit"로 표기 — 24 hex × 4bit = 96bit이나 UUID의 버전/변이 비트가 섞여
  있어 완전한 96bit 균일 랜덤 엔트로피는 아니라는 점은 원본 주석 표기를 그대로 따른 것).
- **TTL 60분**(`WRITE_TTL_MS`). 갱신 방식: `consumeWebPick` 성공 시마다 `ctx.issuedAt = now`로 슬라이딩 갱신
  (라인 76) — 즉 60분은 "발급 후 고정 만료"가 아니라 "마지막 사용 후 60분 미사용 시 만료"에 가까운 슬라이딩 TTL이다.
- **1인 1개**(주석 라인 29, 43): `issueWriteToken` 호출 시 기존 활성 토큰을 먼저 폐기(`userActiveWrite.remove(key)`로
  이전 토큰 id를 얻어 `writeTokens`에서도 제거)한 뒤 새 토큰을 발급 — 동일 유저가 새 링크를 받으면 이전 쓰기토큰은
  즉시 무효화된다.
- 만료 청소: 매 발급 시 `writeTokens` 전체를 훑어 `now - issuedAt > WRITE_TTL_MS`인 항목을 제거(지연 청소, 별도
  스케줄러 없음).
- **cid bind-window**(라인 31, 72-75): 토큰 발급 후 `BIND_WINDOW_MS`(5분) 이내에 처음 도착한 `cid`가 해당 토큰에
  바인딩된다. 이 창을 넘겨서 첫 접근이 오면 거부(노드 삭제 후 null) — "묵힌 링크 선점 차단"(발급만 해두고 방치된
  토큰을 나중에 다른 사람이 가로채는 것 방지). 바인딩된 이후에는 동일 `cid`만 계속 허용된다.
- **저장소는 인메모리**(`ConcurrentHashMap<String, WriteCtx>` — `writeTokens`, `userActiveWrite`, 라인 38-39).
  Room DB나 RTDB에 영속화되지 않음 → **봇 프로세스 재시작 시 모든 쓰기토큰이 소실**된다. Unity(클라이언트) 포팅
  시 이 서버 상태 자체는 이식 대상이 아니지만, 별도 백엔드를 구축할 경우 이 휘발성 특성을 재현할지 검토 필요.

`userKey(linkId, nick, userId)` (라인 40-41): `linkId`와 `(userId가 0이 아니면 userId, 아니면 nick.hashCode())`를
콜론으로 이어붙인 문자열 — 읽기 토큰의 `uid` 계산과 동일한 폴백 규칙(userId 없으면 닉네임 해시).

---

## 5. 스탯 키 사전

`SlotV2AchRow`(3.2절)의 전용 컬럼 10개 + `counters` CSV에 들어가는 나머지 146개 = 업적 판정에 쓰이는
**고유 key 156개**(1.1절, `SlotV2AchievementsExt.LIST`의 `key` 필드 전수 조사 기준. `SlotV2Engine.kt`의
기본 16개 업적이 쓰는 key는 미포함이라 실제로는 이보다 많을 수 있음).

### 5.1 `SlotV2AchRow` 전용 Long 컬럼 (10개, data\SlotV2Entities.kt:92-101)

`cherryTotal` · `crownTotal` · `jackpots` · `bossClears` · `lastSpinClears` · `exactClears` · `prismPicks` ·
`bestStage` · `runs` · `bestScore`

### 5.2 갱신 시점 — 원문 주석 인용 (SlotV2Service.kt/SlotV2Engine.kt 미포함, 실제 구현 코드는 확인 불가)

`SlotV2AchievementsExt.kt`에 흩어진 추적 시점 주석을 라인 그대로 인용한다. **아래는 전부 "주석에 적힌 설명"이며
실제 구현 코드가 아니다** — 정확한 증가 조건은 `SlotV2Service.kt` 추출이 필요하다.

| 대상 key 그룹 | 주석 원문 | 라인 |
|---|---|---|
| `wildTotal, seedTotal, diceTotal, keyTotal, flameTotal, magnetTotal, bombTotal, crownJackpots, wildJackpots, maxSkullSpin, maxCoinSpin, maxCherrySpin, maxBookSpin, maxGemSpin, allinBusts, prayFails` (ACH-3 확장) | "추적: SlotV2Service.handleSpin(L633 부근 incMap/spinMax) + gameOver(prayFails)." | 398 |
| ACH-4 확장 전반(제한도전 noXxxBestStage, 보스별 bossClear_*/bossCounterClear_*, bossNoItemClears/bossNoDeviceClears/bossOverkillClears, bossStreak3, zeroCoinClears, debtBossClears) | "추적: SlotV2Service.addAch4ClearTracking(clearStage). 전부 런상태 파생(DB 스키마 무변경)." | 452 |
| `cstage_<charId>`(14종), `mstage_<machineId>`(16종) — 캐릭터/머신 숙련 | 최고스테이지 갱신 시점을 명시하는 별도 주석 없음(섹션 헤더만 존재) | 157, 239 |
| `rs_*_intro/adv/phd` (연구 10전공) | "신규 추적코드 0, 전부 기존 추적 카운터." — 기존 심볼/카운터 키(cherryTotal, set4Plus, coinTotal, gambles, crownTotal, skullTotal, lastSpinClears, prismPicks, seedTotal, wildTotal) 재사용. 입문 임계는 `SCHOOL_RESEARCH`(SlotV2Engine.kt, 미포함)와 정확히 일치해야 학교별 실버/골드 풀 개방 트리거가 됨 | 521-523 |
| `lic_dev_*`(12종, 장치 면허) | "composeStat 파생키(면허 조건표의 기존 추적 stat AND → 1/0, 신규 추적/DB 0)" | 570 |
| `dvuse_dev_*`(12종, 장치 숙련) | "장착 런수 inc, launchRun) threshold 10" | 590 |
| `dvstage_dev_*`(12종, 장치 장인) | "장착 도달 최고 클리어 S, clearStage setMax) threshold 15" | 591 |
| `noCommandBestStage`, `noRerollBestStage` | "run 플래그 0 일 때 setMax(S)" | 592 |
| `cmdCoin_focus/pray/allin`, `cmdCoinTotal`, `lastClears`, `bossAllinClear` (ACH-6 명령비) | "추적: SlotV2Service handleSpin incMap(cmdCoin_focus/pray/allin/total, 차감 시점) + clearStage clearInc(lastClears = ⏰최후로 클리어, bossAllinClear = 👑보스에서 🎲올인+클리어)." | 642-643 |
| `bldCat_<category>`(5종), `bldTotal`, `bldAllBasic`, `bldAllMaster` (ACH-6 빌드도감) | "추적: bld_<id> 완성 플래그(evalThemeBuilds→setMax 1) → SlotV2Engine.themeBuildStats() 파생키, composeStat 가 stat 에 머지." | 655-656 |

### 5.3 고유 key 전체 목록 (원문 그대로, 156개, 알파벳순)

- allinBusts · allinWins · basicOnlyBestStage · bestScore
- bestStage · bldAllBasic · bldAllMaster · bldCat_성장형
- bldCat_역전형 · bldCat_운명형 · bldCat_위험형 · bldCat_조합형
- bldTotal · bombTotal · bookTotal · bossAllinClear
- bossClear_finals · bossClear_grad · bossClear_luck · bossClear_strict
- bossClears · bossCounterClear_finals · bossCounterClear_grad · bossCounterClear_luck
- bossCounterClear_strict · bossNoDeviceClears · bossNoItemClears · bossOverkillClears
- bossStreak3 · cherryTotal · closeClears · cmdCoinTotal
- cmdCoin_allin · cmdCoin_focus · cmdCoin_pray · coinTotal
- crownJackpots · crownTotal · cstage_alchemist · cstage_crowncol
- cstage_cultist · cstage_daredevil · cstage_farmer · cstage_gambler
- cstage_highroller · cstage_honor · cstage_jeweler · cstage_lucky
- cstage_minimalist · cstage_monk · cstage_novice · cstage_parttime
- cstage_prodigy · cstage_scholar · curse5Stage · curseMax
- debtBossClears · deviceUses · devicesOwned · diceTotal
- dvstage_dev_bell · dvstage_dev_coin · dvstage_dev_copy · dvstage_dev_flame
- dvstage_dev_oracle · dvstage_dev_overheat · dvstage_dev_pin · dvstage_dev_reroll
- dvstage_dev_safe · dvstage_dev_seal · dvstage_dev_subreel · dvstage_dev_swap
- dvuse_dev_bell · dvuse_dev_coin · dvuse_dev_copy · dvuse_dev_flame
- dvuse_dev_oracle · dvuse_dev_overheat · dvuse_dev_pin · dvuse_dev_reroll
- dvuse_dev_safe · dvuse_dev_seal · dvuse_dev_subreel · dvuse_dev_swap
- exactClears · flameTotal · focusUses · gambles
- gemTotal · itemsUsed · jackpots · keyTotal
- lastClears · lastSpinClears · lastUses · lic_dev_bell
- lic_dev_coin · lic_dev_copy · lic_dev_flame · lic_dev_oracle
- lic_dev_overheat · lic_dev_pin · lic_dev_reroll · lic_dev_safe
- lic_dev_seal · lic_dev_subreel · lic_dev_swap · magnetTotal
- maxBookSpin · maxCherrySpin · maxCoinSpin · maxGemSpin
- maxOverPct · maxRunJackpots · maxSkullSpin · minimalistS10
- mstage_basic · mstage_bomb · mstage_casino · mstage_cherry
- mstage_clover · mstage_crown · mstage_flame · mstage_garden
- mstage_gem · mstage_library · mstage_magnet · mstage_rainbow
- mstage_skull · mstage_star · mstage_vault · mstage_wildmac
- noCommandBestStage · noDevStage · noGoldBestStage · noItemMaxS
- noPrismBestStage · noRelicBestStage · noRerollBestStage · noShopS10
- pinUses · prayClears · prayFails · prismPicks
- relicsMax · rerollUses · runs · seedTotal
- set4Plus · shopBuys · skullTotal · starTotal
- totalSpins · wildJackpots · wildTotal · zeroCoinClears

### 5.4 매개변수화된 key 패턴 (C# enum/상수 설계 참고, 5.3절에서 전수 계산 — 하드코딩 아님)

원본은 Kotlin 문자열 리터럴을 그대로 나열했지만, 아래 패턴들은 `<id>` 부분만 바뀌는 동일 구조 반복이다.
C# 포팅 시 `Dictionary<string, long>` 키를 문자열 템플릿(예: `$"cstage_{charId}"`)으로 생성하는 편이 466개
achievement 정의를 하드코딩하는 것보다 안전하다.

| 패턴 | 개수 | <id> 목록 (unique key 전수, 원문 그대로) |
|---|---|---|
| cstage_<charId> | 16 | alchemist, crowncol, cultist, daredevil, farmer, gambler, highroller, honor, jeweler, lucky, minimalist, monk, novice, parttime, prodigy, scholar |
| mstage_<machineId> | 16 | basic, bomb, casino, cherry, clover, crown, flame, garden, gem, library, magnet, rainbow, skull, star, vault, wildmac |
| lic_dev_<deviceId> | 12 | bell, coin, copy, flame, oracle, overheat, pin, reroll, safe, seal, subreel, swap |
| dvuse_dev_<deviceId> | 12 | bell, coin, copy, flame, oracle, overheat, pin, reroll, safe, seal, subreel, swap |
| dvstage_dev_<deviceId> | 12 | bell, coin, copy, flame, oracle, overheat, pin, reroll, safe, seal, subreel, swap |
| bossClear_<bossId> | 4 | finals, grad, luck, strict |
| bossCounterClear_<bossId> | 4 | finals, grad, luck, strict |
| cmdCoin_<cmd> | 3 | allin, focus, pray |
| bldCat_<category> | 5 | 성장형, 역전형, 운명형, 위험형, 조합형 |
| max<Symbol>Spin | 5 | maxBookSpin, maxCherrySpin, maxCoinSpin, maxGemSpin, maxSkullSpin |
| <symbol>Total | 14 | bombTotal, bookTotal, cherryTotal, coinTotal, crownTotal, diceTotal, flameTotal, gemTotal, keyTotal, magnetTotal, seedTotal, skullTotal, starTotal, wildTotal |

---

## 6. C# 이식 시 주의

1. **소스 파일 범위 한계** — 이 문서가 커버하는 4개 파일에는 업적 판정 로직(`composeStat`, `achCounter`,
   `reqProgress`, `SCHOOL_RESEARCH`, `THEME_BUILDS`, `allChallenges`, `unlockedChars/Machines`, `charMastery`,
   `titleStr`, `accountLevel/Exp` 등)과 스탯 증가 호출부(`SlotV2Service.handleSpin/clearStage/launchRun/
   addAch4ClearTracking/gameOver`)가 전혀 포함되어 있지 않다. 1장의 466개 표는 "업적의 정적 정의(카탈로그)"
   이지 "언제 무엇이 몇 만큼 증가하는가"의 완전한 사양이 아니다 — 그 부분은 `SlotV2Engine.kt`/`SlotV2Service.kt`를
   추가로 추출해야 한다.
2. **기본 16개 업적 누락** — `SlotV2AchievementsExt.LIST`는 "기본 16 외 추가분"(파일 3줄 주석)이다. 즉 잭팟런 v2
   전체 업적 목록은 이 466개 + `SlotV2Engine.kt`에 정의된 기본 16개(미확인) = 최소 482개. 업적 총
   개수를 UI에 하드코딩하지 말고 두 소스를 합산하는 방식으로 이식할 것.
3. **PK 설계 차이** — `slot_v2_run`/`slot_v2_ach`는 PK가 `(linkId, ownerKey)`이고, `slot_v2_score`는 PK가
   `(linkId, nickname)`이며 `userId`는 별도 인덱스 컬럼(3.3절)이다. 즉 스코어 테이블만 "닉네임 변경 시 새 행"이
   생길 수 있는 구조 — `topByBest` 쿼리(3.4절)가 이를 `userId` 그룹핑으로 재병합하는 이유가 여기 있다. Unity
   로컬(단일 플레이어, 멀티계정 없음) 저장에서는 이 이중 구조가 불필요할 수 있으나, 서버 동기화를 유지한다면
   동일한 병합 규칙을 재구현해야 랭킹 집계가 어긋나지 않는다.
4. **전용 컬럼 vs. CSV 맵의 혼합 저장** — `SlotV2AchRow`는 10개 stat만 전용 Long 컬럼이고 나머지는 `counters`
   문자열에 `"key:val,key:val"` CSV로 저장된다(3.2절). Unity 로컬 JSON으로 옮길 때 이 구분을 유지할 이유가 없다면
   전부 `Dictionary<string, long>` 하나로 통합하는 편이 낫다 — 단, 두 그룹이 갱신되는 코드 경로가 원본에서 다를
   가능성이 있으므로(전용 컬럼은 Room 필드 직접 대입, CSV는 파싱/직렬화 왕복) 마이그레이션 시 두 경로 모두 동일한
   값이 되는지 검증 필요.
5. **CSV 직렬화 필드 다수** — `SlotV2RunRow`에만 CSV/직렬화 문자열 필드가 12개 이상이다(perks, curses, items,
   armItems, phaseItems, usedCmds, lastCells, runSymCounts, phasePerks, pendingOptions, lockedNext 등). C#/Unity에서는
   `List<string>` 또는 `JsonUtility` 직렬화 가능한 배열로 정규화하고, CSV 구분자(쉼표)가 id 문자열 내부에 나타나지
   않는다는 원본의 암묵적 전제를 그대로 유지할지 확인할 것(id에 쉼표가 없다는 불변식이 깨지면 파싱이 깨짐).
6. **`lastXxx` 필드들의 "되돌림(net-adjust)" 의미** — `SlotV2RunRow.lastGain/lastScoreGain/lastCoinGain/lastSet4/
   lastAdjPairs/lastSpinNo`(라인 42-47)는 재굴림/고정/복사/교체 장치가 "직전 스핀 결과를 취소하고 다시 계산"할 때
   되돌리기 위한 상태다. 단순 로그가 아니라 **장치 액션의 실행 가능 여부/정정 계산에 필수**인 상태이므로, 이
   필드들을 생략하거나 근사치로 대체하면 장치 로직이 깨진다.
7. **RTDB 쓰기토큰은 인메모리·휘발성 서버 상태** — 4.6절 (B)의 `writeTokens`/`userActiveWrite`는 Room이나 RTDB에
   저장되지 않는 카카오봇 프로세스 내부 상태다. Unity 클라이언트 단독 포팅이라면 이 메커니즘 자체가 필요 없을 수
   있지만("웹 연동" 기능을 유지할지는 별도 결정), 만약 유지한다면 봇 프로세스가 여러 인스턴스로 스케일아웃될 때
   이 인메모리 맵이 인스턴스마다 분리되어 토큰이 유실될 수 있다는 원본의 잠재적 약점도 함께 검토할 것.
8. **히든 업적 마스킹은 서버 책임이 아니다** — 4.4절에서 확인했듯 `jackpotcatalog`에는 hidden=true인 업적도
   이름/설명/보상이 그대로 노출된다. Unity 로컬 단독 빌드는 이 문제가 없지만(클라이언트가 곧 전부이므로), 만약
   원본처럼 별도 웹/서버로 카탈로그를 노출할 계획이라면 마스킹 로직을 클라이언트에 새로 구현해야 한다(서버 원본에
   마스킹 코드가 없음).
9. **`achCounter`/`reqProgress`/`bottleneck` 등 진행률 계산 함수는 이 문서에 명세가 없다** — 2.4절의 `challenges`
   배열과 4.2절의 `unlock` 객체에 쓰이는 cur/max/pct는 `bottleneck()`(game\SlotV2WebService.kt:85-90, 다조건 AND
   중 진행률이 가장 낮은 조건 하나를 고른다는 로직만 확인됨)과 `reqProgress()`(호출만 있고 정의는 미포함)에 의존한다.
   진행바 UI를 그대로 재현하려면 `SlotV2Engine.kt`의 `reqProgress` 구현을 추가로 확인해야 한다.
10. **원본 파일의 줄 수 표기 불일치** — 작업 지시에 명시된 줄 수(631/122/54/304)와 실제 파일 줄 수(683/131/68/336)가
    다르다(문서 상단 안내 참조). 커밋 c73452c 기준 실측치를 신뢰하고 진행했다.
