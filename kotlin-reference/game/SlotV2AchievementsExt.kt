package com.ashersoft.kakaobot.game

/** 잭팟런 확장 업적 — 기본 16 외 추가분. SlotV2Engine.ACHIEVEMENTS 에 합쳐진다. */
object SlotV2AchievementsExt {
    val LIST: List<SlotV2Engine.Achievement> = listOf(
        // ── 입문 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "intro_firstSpin", emoji = "🎰", name = "첫 스핀", key = "totalSpins", threshold = 1, desc = "처음으로 슬롯을 돌렸다", cat = "입문", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "intro_firstRun", emoji = "🏫", name = "입학식", key = "runs", threshold = 1, desc = "첫 런을 시작했다", cat = "입문", tier = "브론즈", reward = "칭호: 신입생"),
        SlotV2Engine.Achievement(id = "intro_firstBoss", emoji = "👹", name = "첫 보스 격파", key = "bossClears", threshold = 1, desc = "보스를 처음 클리어했다", cat = "입문", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "intro_firstStage5", emoji = "🪜", name = "5층 도달", key = "bestStage", threshold = 5, desc = "5스테이지까지 도달했다", cat = "입문", tier = "브론즈", reward = "칭호: 초보 모험가"),
        SlotV2Engine.Achievement(id = "intro_firstShop", emoji = "🛒", name = "첫 장보기", key = "shopBuys", threshold = 1, desc = "상점에서 처음 구매했다", cat = "입문", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "intro_firstDevice", emoji = "⚙️", name = "장치 입문", key = "deviceUses", threshold = 1, desc = "장치를 처음 사용했다", cat = "입문", tier = "브론즈", reward = "도감 등록"),

        // ── 심볼: 체리 🍒 ───────────────────────────────────────
        SlotV2Engine.Achievement(id = "cherry1000", emoji = "🍒", name = "체리 농장주", key = "cherryTotal", threshold = 1000, desc = "🍒체리 누적 1000개 등장", cat = "심볼", tier = "골드", reward = "칭호: 체리 농장주"),
        SlotV2Engine.Achievement(id = "cherry30", emoji = "🍒", name = "체리 새싹", key = "cherryTotal", threshold = 30, desc = "🍒체리 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cherry300", emoji = "🍒", name = "체리 과수원", key = "cherryTotal", threshold = 300, desc = "🍒체리 누적 300개 등장", cat = "심볼", tier = "실버", reward = "🎭캐릭터 해금 힌트: 체리농부"),
        SlotV2Engine.Achievement(id = "cherry3000", emoji = "🍒", name = "체리 제국", key = "cherryTotal", threshold = 3000, desc = "🍒체리 누적 3000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 체리 황제"),

        // ── 심볼: 책 📖 ─────────────────────────────────────────
        SlotV2Engine.Achievement(id = "book30", emoji = "📖", name = "책장 정리", key = "bookTotal", threshold = 30, desc = "📖책 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "book100", emoji = "📖", name = "다독가", key = "bookTotal", threshold = 100, desc = "📖책 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 다독가"),
        SlotV2Engine.Achievement(id = "book300", emoji = "📖", name = "서재의 주인", key = "bookTotal", threshold = 300, desc = "📖책 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 서재의 주인"),
        SlotV2Engine.Achievement(id = "book500", emoji = "📖", name = "장서가", key = "bookTotal", threshold = 500, desc = "📖책 누적 500개 등장", cat = "심볼", tier = "골드", reward = "칭호: 장서가"),
        SlotV2Engine.Achievement(id = "book1000", emoji = "📖", name = "도서관장", key = "bookTotal", threshold = 1000, desc = "📖책 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 도서관장"),

        // ── 심볼: 별 ⭐ ─────────────────────────────────────────
        SlotV2Engine.Achievement(id = "star30", emoji = "⭐", name = "별 줍기", key = "starTotal", threshold = 30, desc = "⭐별 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "star100", emoji = "⭐", name = "별 수집가", key = "starTotal", threshold = 100, desc = "⭐별 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 별 수집가"),
        SlotV2Engine.Achievement(id = "star300", emoji = "⭐", name = "별자리 화가", key = "starTotal", threshold = 300, desc = "⭐별 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 별자리 화가"),
        SlotV2Engine.Achievement(id = "star500", emoji = "⭐", name = "밤하늘의 주인", key = "starTotal", threshold = 500, desc = "⭐별 누적 500개 등장", cat = "심볼", tier = "골드", reward = "칭호: 밤하늘의 주인"),
        SlotV2Engine.Achievement(id = "star1000", emoji = "⭐", name = "은하 수집가", key = "starTotal", threshold = 1000, desc = "⭐별 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 은하 수집가"),

        // ── 심볼: 보석 💎 ───────────────────────────────────────
        SlotV2Engine.Achievement(id = "gem30", emoji = "💎", name = "원석 줍기", key = "gemTotal", threshold = 30, desc = "💎보석 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "gem100", emoji = "💎", name = "보석 세공사", key = "gemTotal", threshold = 100, desc = "💎보석 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 보석 세공사"),
        SlotV2Engine.Achievement(id = "gem300", emoji = "💎", name = "보석상", key = "gemTotal", threshold = 300, desc = "💎보석 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 보석상"),
        SlotV2Engine.Achievement(id = "gem500", emoji = "💎", name = "보석 감정사", key = "gemTotal", threshold = 500, desc = "💎보석 누적 500개 등장", cat = "심볼", tier = "골드", reward = "칭호: 보석 감정사"),
        SlotV2Engine.Achievement(id = "gem1000", emoji = "💎", name = "다이아 광맥", key = "gemTotal", threshold = 1000, desc = "💎보석 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 다이아 광맥"),

        // ── 심볼: 왕관 👑 ───────────────────────────────────────
        SlotV2Engine.Achievement(id = "crown30ext", emoji = "👑", name = "왕관 보관소", key = "crownTotal", threshold = 30, desc = "👑왕관 누적 30개 등장", cat = "심볼", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "crown100", emoji = "👑", name = "대관식", key = "crownTotal", threshold = 100, desc = "👑왕관 누적 100개 등장", cat = "심볼", tier = "골드", reward = "칭호: 즉위한 자"),
        SlotV2Engine.Achievement(id = "crown300", emoji = "👑", name = "왕가의 보고", key = "crownTotal", threshold = 300, desc = "👑왕관 누적 300개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 왕중왕"),

        // ── 심볼: 해골 💀 ───────────────────────────────────────
        SlotV2Engine.Achievement(id = "skull30", emoji = "💀", name = "해골 친구", key = "skullTotal", threshold = 30, desc = "💀해골 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "skull100", emoji = "💀", name = "해골 수집가", key = "skullTotal", threshold = 100, desc = "💀해골 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 해골 수집가"),
        SlotV2Engine.Achievement(id = "skull300", emoji = "💀", name = "납골당지기", key = "skullTotal", threshold = 300, desc = "💀해골 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 납골당지기"),
        SlotV2Engine.Achievement(id = "skull1000", emoji = "💀", name = "죽음의 군주", key = "skullTotal", threshold = 1000, desc = "💀해골 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 죽음의 군주"),

        // ── 심볼: 코인 🪙 ───────────────────────────────────────
        SlotV2Engine.Achievement(id = "coin30", emoji = "🪙", name = "동전 줍기", key = "coinTotal", threshold = 30, desc = "🪙코인 누적 30개 등장", cat = "심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "coin100", emoji = "🪙", name = "저금통", key = "coinTotal", threshold = 100, desc = "🪙코인 누적 100개 등장", cat = "심볼", tier = "실버", reward = "칭호: 저금왕"),
        SlotV2Engine.Achievement(id = "coin300", emoji = "🪙", name = "환전상", key = "coinTotal", threshold = 300, desc = "🪙코인 누적 300개 등장", cat = "심볼", tier = "골드", reward = "칭호: 환전상"),
        SlotV2Engine.Achievement(id = "coin500", emoji = "🪙", name = "금고지기", key = "coinTotal", threshold = 500, desc = "🪙코인 누적 500개 등장", cat = "심볼", tier = "골드", reward = "칭호: 금고지기"),
        SlotV2Engine.Achievement(id = "coin1000", emoji = "🪙", name = "조폐국장", key = "coinTotal", threshold = 1000, desc = "🪙코인 누적 1000개 등장", cat = "심볼", tier = "프리즘", reward = "칭호: 조폐국장"),

        // ── 명령어 ──────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "cmd_focus1", emoji = "🎯", name = "집중 입문", key = "focusUses", threshold = 1, desc = "집중 명령을 처음 사용", cat = "명령어", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmd_focus10", emoji = "🎯", name = "집중의 달인", key = "focusUses", threshold = 10, desc = "집중 명령 10회 사용", cat = "명령어", tier = "실버", reward = "칭호: 집중의 달인"),
        SlotV2Engine.Achievement(id = "cmd_focus50", emoji = "🎯", name = "무아지경", key = "focusUses", threshold = 50, desc = "집중 명령 50회 사용", cat = "명령어", tier = "골드", reward = "칭호: 무아지경"),
        SlotV2Engine.Achievement(id = "cmd_allin1", emoji = "💥", name = "첫 올인", key = "allinWins", threshold = 1, desc = "올인 스핀을 처음 승리", cat = "명령어", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmd_allin5", emoji = "💥", name = "올인 전문가", key = "allinWins", threshold = 5, desc = "올인 스핀 5회 승리", cat = "명령어", tier = "실버", reward = "칭호: 올인 전문가"),
        SlotV2Engine.Achievement(id = "cmd_allin20", emoji = "💥", name = "도박의 신", key = "allinWins", threshold = 20, desc = "올인 스핀 20회 승리", cat = "명령어", tier = "골드", reward = "칭호: 도박의 신"),
        SlotV2Engine.Achievement(id = "cmd_pray1", emoji = "🙏", name = "첫 기도", key = "prayClears", threshold = 1, desc = "기도 후 스테이지를 클리어", cat = "명령어", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmd_pray5", emoji = "🙏", name = "기적의 증인", key = "prayClears", threshold = 5, desc = "기도 후 클리어 5회", cat = "명령어", tier = "골드", reward = "칭호: 기적의 증인"),
        SlotV2Engine.Achievement(id = "cmd_last1", emoji = "⏳", name = "최후의 한 수", key = "lastUses", threshold = 1, desc = "최후 명령을 처음 사용", cat = "명령어", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmd_last10", emoji = "⏳", name = "벼랑 끝의 명수", key = "lastUses", threshold = 10, desc = "최후 명령 10회 사용", cat = "명령어", tier = "골드", reward = "칭호: 벼랑 끝의 명수"),
        SlotV2Engine.Achievement(id = "cmd_reroll1", emoji = "🔄", name = "재굴림 입문", key = "rerollUses", threshold = 1, desc = "재굴림을 처음 사용", cat = "명령어", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmd_pin1", emoji = "📌", name = "고정 입문", key = "pinUses", threshold = 1, desc = "고정을 처음 사용", cat = "명령어", tier = "브론즈", reward = "도감 등록"),

        // ── 장치 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "dev_use10", emoji = "⚙️", name = "장치 애호가", key = "deviceUses", threshold = 10, desc = "장치를 10회 사용", cat = "장치", tier = "실버", reward = "칭호: 장치 애호가"),
        SlotV2Engine.Achievement(id = "dev_use50", emoji = "⚙️", name = "기계공", key = "deviceUses", threshold = 50, desc = "장치를 50회 사용", cat = "장치", tier = "골드", reward = "칭호: 기계공"),
        SlotV2Engine.Achievement(id = "dev_own1", emoji = "🔧", name = "첫 장치 보유", key = "devicesOwned", threshold = 1, desc = "장치를 1종 영구 보유", cat = "장치", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "dev_own5", emoji = "🔧", name = "장치 수집가", key = "devicesOwned", threshold = 5, desc = "장치를 5종 영구 보유", cat = "장치", tier = "골드", reward = "칭호: 장치 수집가"),
        SlotV2Engine.Achievement(id = "dev_own12", emoji = "🔧", name = "장치 마스터", key = "devicesOwned", threshold = 12, desc = "장치를 12종 모두 보유", cat = "장치", tier = "프리즘", reward = "칭호: 장치 마스터"),
        SlotV2Engine.Achievement(id = "dev_reroll10", emoji = "🔄", name = "재굴림 중독", key = "rerollUses", threshold = 10, desc = "재굴림 10회 사용", cat = "장치", tier = "실버", reward = "칭호: 재굴림 중독"),
        SlotV2Engine.Achievement(id = "dev_pin10", emoji = "📌", name = "고정의 달인", key = "pinUses", threshold = 10, desc = "고정 10회 사용", cat = "장치", tier = "실버", reward = "칭호: 고정의 달인"),

        // ── 유물 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "relic3", emoji = "🏺", name = "유물 수집 시작", key = "relicsMax", threshold = 3, desc = "한 런에 유물 3개 동시 보유", cat = "유물", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "relic5", emoji = "🏺", name = "유물 애호가", key = "relicsMax", threshold = 5, desc = "한 런에 유물 5개 동시 보유", cat = "유물", tier = "실버", reward = "칭호: 유물 애호가"),
        SlotV2Engine.Achievement(id = "relic10", emoji = "🏺", name = "유물 수집광", key = "relicsMax", threshold = 10, desc = "한 런에 유물 10개 동시 보유", cat = "유물", tier = "골드", reward = "칭호: 유물 수집광"),
        SlotV2Engine.Achievement(id = "prismPick1", emoji = "🔮", name = "첫 프리즘 유물", key = "prismPicks", threshold = 1, desc = "프리즘 증강을 처음 선택", cat = "유물", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "prismPick20", emoji = "🔮", name = "프리즘 마니아", key = "prismPicks", threshold = 20, desc = "프리즘 증강 20회 선택", cat = "유물", tier = "프리즘", reward = "칭호: 프리즘 마니아"),

        // ── 아이템 ──────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "item1", emoji = "🎒", name = "첫 아이템", key = "itemsUsed", threshold = 1, desc = "아이템을 처음 사용", cat = "아이템", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "item10", emoji = "🎒", name = "아이템 애용가", key = "itemsUsed", threshold = 10, desc = "아이템 10회 사용", cat = "아이템", tier = "실버", reward = "칭호: 아이템 애용가"),
        SlotV2Engine.Achievement(id = "item50", emoji = "🎒", name = "만물상 단골", key = "itemsUsed", threshold = 50, desc = "아이템 50회 사용", cat = "아이템", tier = "골드", reward = "칭호: 만물상 단골"),
        SlotV2Engine.Achievement(id = "item100", emoji = "🎒", name = "소비의 화신", key = "itemsUsed", threshold = 100, desc = "아이템 100회 사용", cat = "아이템", tier = "프리즘", reward = "칭호: 소비의 화신"),

        // ── 상점 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "shop10", emoji = "🛍️", name = "단골 손님", key = "shopBuys", threshold = 10, desc = "상점에서 10회 구매", cat = "상점", tier = "실버", reward = "칭호: 단골 손님"),
        SlotV2Engine.Achievement(id = "shop50", emoji = "🛍️", name = "큰손", key = "shopBuys", threshold = 50, desc = "상점에서 50회 구매", cat = "상점", tier = "골드", reward = "칭호: 큰손"),
        SlotV2Engine.Achievement(id = "gamble1", emoji = "🎲", name = "첫 도박", key = "gambles", threshold = 1, desc = "도박장 노드를 처음 이용", cat = "상점", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "gamble10", emoji = "🎲", name = "도박장 VIP", key = "gambles", threshold = 10, desc = "도박장 노드 10회 이용", cat = "상점", tier = "골드", reward = "칭호: 도박장 VIP"),

        // ── 클리어 ──────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "boss5ext", emoji = "👹", name = "보스 사냥꾼", key = "bossClears", threshold = 5, desc = "보스 5회 클리어", cat = "클리어", tier = "실버", reward = "칭호: 보스 사냥꾼"),
        SlotV2Engine.Achievement(id = "boss20", emoji = "👹", name = "보스 학살자", key = "bossClears", threshold = 20, desc = "보스 20회 클리어", cat = "클리어", tier = "골드", reward = "칭호: 보스 학살자"),
        SlotV2Engine.Achievement(id = "stage10ext", emoji = "🪜", name = "10층 등반가", key = "bestStage", threshold = 10, desc = "10스테이지 도달", cat = "클리어", tier = "실버", reward = "칭호: 10층 등반가"),
        SlotV2Engine.Achievement(id = "stage15ext", emoji = "🪜", name = "고지 점령", key = "bestStage", threshold = 15, desc = "15스테이지 도달", cat = "클리어", tier = "골드", reward = "칭호: 고지 점령자"),
        SlotV2Engine.Achievement(id = "runs10", emoji = "🔁", name = "꾸준한 도전자", key = "runs", threshold = 10, desc = "10런 플레이", cat = "클리어", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "runs50", emoji = "🔁", name = "베테랑", key = "runs", threshold = 50, desc = "50런 플레이", cat = "클리어", tier = "골드", reward = "칭호: 베테랑"),

        // ── 아슬아슬 ────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "close10", emoji = "😅", name = "아슬아슬 통과", key = "closeClears", threshold = 10, desc = "잔여 EXP 10이하로 10회 클리어", cat = "아슬아슬", tier = "실버", reward = "칭호: 아슬아슬"),
        SlotV2Engine.Achievement(id = "close30", emoji = "😅", name = "줄타기 곡예사", key = "closeClears", threshold = 30, desc = "잔여 EXP 10이하로 30회 클리어", cat = "아슬아슬", tier = "골드", reward = "칭호: 줄타기 곡예사"),
        SlotV2Engine.Achievement(id = "lastspin1", emoji = "🎯", name = "막판 뒤집기", key = "lastSpinClears", threshold = 1, desc = "마지막 스핀에 클리어", cat = "아슬아슬", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "lastspin5", emoji = "🎯", name = "끝내기의 명수", key = "lastSpinClears", threshold = 5, desc = "마지막 스핀 클리어 5회", cat = "아슬아슬", tier = "골드", reward = "칭호: 끝내기의 명수"),
        SlotV2Engine.Achievement(id = "exact1ext", emoji = "🎯", name = "딱 떨어지게", key = "exactClears", threshold = 1, desc = "요구치와 정확히 일치 클리어", cat = "아슬아슬", tier = "골드", reward = "도감 등록"),

        // ── 점수 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "score5k", emoji = "📊", name = "점수 입문", key = "bestScore", threshold = 5000, desc = "한 런 5000점 달성", cat = "점수", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "score10kext", emoji = "📊", name = "만점 클럽", key = "bestScore", threshold = 10000, desc = "한 런 10000점 달성", cat = "점수", tier = "실버", reward = "칭호: 만점 클럽"),
        SlotV2Engine.Achievement(id = "score30k", emoji = "📊", name = "고득점자", key = "bestScore", threshold = 30000, desc = "한 런 30000점 달성", cat = "점수", tier = "골드", reward = "칭호: 고득점자"),
        SlotV2Engine.Achievement(id = "score50kext", emoji = "📊", name = "점수 사냥꾼", key = "bestScore", threshold = 50000, desc = "한 런 50000점 달성", cat = "점수", tier = "골드", reward = "칭호: 점수 사냥꾼"),

        // ── 저주 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "curse1", emoji = "🩸", name = "첫 저주", key = "curseMax", threshold = 1, desc = "한 런에 저주 1개 보유", cat = "저주", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "curse3", emoji = "🩸", name = "저주받은 자", key = "curseMax", threshold = 3, desc = "한 런에 저주 3개 동시 보유", cat = "저주", tier = "실버", reward = "칭호: 저주받은 자"),
        SlotV2Engine.Achievement(id = "curse5", emoji = "🩸", name = "저주 수집가", key = "curseMax", threshold = 5, desc = "한 런에 저주 5개 동시 보유", cat = "저주", tier = "골드", reward = "칭호: 저주 수집가"),
        SlotV2Engine.Achievement(id = "curse7", emoji = "🩸", name = "저주의 그릇", key = "curseMax", threshold = 7, desc = "한 런에 저주 7개 동시 보유", cat = "저주", tier = "프리즘", reward = "칭호: 저주의 그릇"),

        // ── 도전 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "chal_stage20", emoji = "🏔️", name = "정상 정복", key = "bestStage", threshold = 20, desc = "20스테이지 도달", cat = "도전", tier = "프리즘", reward = "칭호: 정상 정복자"),
        SlotV2Engine.Achievement(id = "chal_boss50", emoji = "💀", name = "백전노장", key = "bossClears", threshold = 50, desc = "보스 50회 클리어", cat = "도전", tier = "프리즘", reward = "칭호: 백전노장"),
        SlotV2Engine.Achievement(id = "chal_curse7", emoji = "☠️", name = "저주의 화신", key = "curseMax", threshold = 7, desc = "한 런에 저주 7개 동시 보유", cat = "도전", tier = "프리즘", reward = "칭호: 저주의 화신"),
        SlotV2Engine.Achievement(id = "chal_score100k", emoji = "🏆", name = "10만점 돌파", key = "bestScore", threshold = 100000, desc = "한 런 100000점 달성", cat = "도전", tier = "프리즘", reward = "칭호: 10만점의 전설"),

        // ── 반복 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "spin100", emoji = "🔂", name = "백 번의 스핀", key = "totalSpins", threshold = 100, desc = "누적 100회 스핀", cat = "반복", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "spin500", emoji = "🔂", name = "오백 번의 스핀", key = "totalSpins", threshold = 500, desc = "누적 500회 스핀", cat = "반복", tier = "실버", reward = "칭호: 손가락 운동"),
        SlotV2Engine.Achievement(id = "spin1000", emoji = "🔂", name = "천 번의 스핀", key = "totalSpins", threshold = 1000, desc = "누적 1000회 스핀", cat = "반복", tier = "골드", reward = "칭호: 스핀 중독"),
        SlotV2Engine.Achievement(id = "spin5000", emoji = "🔂", name = "오천 번의 스핀", key = "totalSpins", threshold = 5000, desc = "누적 5000회 스핀", cat = "반복", tier = "프리즘", reward = "칭호: 스핀의 화신"),
        SlotV2Engine.Achievement(id = "runs100", emoji = "🔁", name = "백 번의 도전", key = "runs", threshold = 100, desc = "100런 플레이", cat = "반복", tier = "프리즘", reward = "칭호: 슬롯의 산증인"),

        // ── 히든 ────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "hid_exact5", emoji = "🎯", name = "딱 맞췄다", key = "exactClears", threshold = 5, desc = "요구치와 정확히 일치 클리어 5회", cat = "히든", tier = "프리즘", reward = "칭호: 정밀 저격수", hidden = true),
        SlotV2Engine.Achievement(id = "hid_close1", emoji = "💔", name = "심장 파괴자", key = "closeClears", threshold = 1, desc = "잔여 EXP 10이하로 클리어", cat = "히든", tier = "실버", reward = "칭호: 심장 파괴자", hidden = true),
        SlotV2Engine.Achievement(id = "hid_lastspin20", emoji = "🃏", name = "운명의 마지막", key = "lastSpinClears", threshold = 20, desc = "마지막 스핀 클리어 20회", cat = "히든", tier = "프리즘", reward = "칭호: 운명의 카드", hidden = true),
        SlotV2Engine.Achievement(id = "hid_jackpot1", emoji = "🎉", name = "첫 잭팟", key = "jackpots", threshold = 1, desc = "5개 일치 잭팟 달성", cat = "히든", tier = "골드", reward = "도감 등록", hidden = true),
        SlotV2Engine.Achievement(id = "hid_jackpot5", emoji = "🎉", name = "잭팟 단골", key = "jackpots", threshold = 5, desc = "잭팟 5회 달성", cat = "히든", tier = "프리즘", reward = "칭호: 잭팟 메이커", hidden = true),
        SlotV2Engine.Achievement(id = "hid_jackpot20", emoji = "🎰", name = "잭팟런의 주인", key = "jackpots", threshold = 20, desc = "잭팟 20회 달성", cat = "히든", tier = "프리즘", reward = "칭호: 잭팟런의 주인", hidden = true),
        SlotV2Engine.Achievement(id = "hid_curse7", emoji = "🎓", name = "검은 졸업식", key = "curseMax", threshold = 7, desc = "저주 7개를 안고 살아남았다", cat = "히든", tier = "프리즘", reward = "칭호: 검은 졸업생", hidden = true),
        SlotV2Engine.Achievement(id = "hid_score100k", emoji = "👾", name = "졸업식의 괴물", key = "bestScore", threshold = 100000, desc = "한 런 100000점의 괴물", cat = "히든", tier = "프리즘", reward = "칭호: 졸업식의 괴물", hidden = true),
        SlotV2Engine.Achievement(id = "hid_set4_1", emoji = "🍀", name = "행운의 네 잎", key = "set4Plus", threshold = 1, desc = "같은 심볼 4개 이상 등장", cat = "히든", tier = "실버", reward = "도감 등록", hidden = true),
        SlotV2Engine.Achievement(id = "hid_set4_50", emoji = "🍀", name = "사천왕", key = "set4Plus", threshold = 50, desc = "같은 심볼 4개 이상 50회", cat = "히든", tier = "프리즘", reward = "칭호: 사천왕", hidden = true),
        SlotV2Engine.Achievement(id = "hid_allin20", emoji = "🔥", name = "불꽃의 도박사", key = "allinWins", threshold = 20, desc = "올인 20회 승리의 광기", cat = "히든", tier = "프리즘", reward = "칭호: 불꽃의 도박사", hidden = true),
        SlotV2Engine.Achievement(id = "hid_skull1000", emoji = "⚰️", name = "사신의 친구", key = "skullTotal", threshold = 1000, desc = "💀해골과 1000번 마주쳤다", cat = "히든", tier = "프리즘", reward = "칭호: 사신의 친구", hidden = true),
        SlotV2Engine.Achievement(id = "hid_prismPick5", emoji = "🌈", name = "무지개를 쫓는 자", key = "prismPicks", threshold = 5, desc = "프리즘 증강 5회 선택", cat = "히든", tier = "골드", reward = "칭호: 무지개를 쫓는 자", hidden = true),
        SlotV2Engine.Achievement(id = "hid_pray5", emoji = "✨", name = "신앙의 결실", key = "prayClears", threshold = 5, desc = "기도가 모두 응답받았다", cat = "히든", tier = "프리즘", reward = "칭호: 신앙의 결실", hidden = true),

        // ── 캐릭터 숙련 (cstage_<charId> 최고스테이지 S5/10/15/20 = 브론즈/실버/골드/프리즘) ──
        // 🎒 초보학생 (novice)
        SlotV2Engine.Achievement(id = "cmast_novice_b", emoji = "🎒", name = "첫 등교", key = "cstage_novice", threshold = 5, desc = "초보학생(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_novice_s", emoji = "🎒", name = "성실한 신입", key = "cstage_novice", threshold = 10, desc = "초보학생(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 성실한 신입"),
        SlotV2Engine.Achievement(id = "cmast_novice_g", emoji = "🎒", name = "모범 신입생", key = "cstage_novice", threshold = 15, desc = "초보학생(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 신입생의 가방"),
        SlotV2Engine.Achievement(id = "cmast_novice_p", emoji = "🎒", name = "초심의 전설", key = "cstage_novice", threshold = 20, desc = "초보학생(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 초심을 잃지 않은 자"),
        // 📗 장학생 (scholar)
        SlotV2Engine.Achievement(id = "cmast_scholar_b", emoji = "📗", name = "장학 입문", key = "cstage_scholar", threshold = 5, desc = "장학생(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_scholar_s", emoji = "📗", name = "우등 장학생", key = "cstage_scholar", threshold = 10, desc = "장학생(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 우등 장학생"),
        SlotV2Engine.Achievement(id = "cmast_scholar_g", emoji = "📗", name = "전액 장학생", key = "cstage_scholar", threshold = 15, desc = "장학생(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 장학증서"),
        SlotV2Engine.Achievement(id = "cmast_scholar_p", emoji = "📗", name = "장학의 전설", key = "cstage_scholar", threshold = 20, desc = "장학생(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 학문의 정점"),
        // 🎲 도박꾼 (gambler)
        SlotV2Engine.Achievement(id = "cmast_gambler_b", emoji = "🎲", name = "도박 입문", key = "cstage_gambler", threshold = 5, desc = "도박꾼(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_gambler_s", emoji = "🎲", name = "도박 숙련", key = "cstage_gambler", threshold = 10, desc = "도박꾼(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 노련한 도박꾼"),
        SlotV2Engine.Achievement(id = "cmast_gambler_g", emoji = "🎲", name = "도박 졸업", key = "cstage_gambler", threshold = 15, desc = "도박꾼(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 황금 주사위"),
        SlotV2Engine.Achievement(id = "cmast_gambler_p", emoji = "🎲", name = "도박의 전설", key = "cstage_gambler", threshold = 20, desc = "도박꾼(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 운명을 건 자"),
        // 🍒 체리농부 (farmer)
        SlotV2Engine.Achievement(id = "cmast_farmer_b", emoji = "🍒", name = "텃밭 가꾸기", key = "cstage_farmer", threshold = 5, desc = "체리농부(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_farmer_s", emoji = "🍒", name = "능숙한 농부", key = "cstage_farmer", threshold = 10, desc = "체리농부(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 능숙한 농부"),
        SlotV2Engine.Achievement(id = "cmast_farmer_g", emoji = "🍒", name = "대농장주", key = "cstage_farmer", threshold = 15, desc = "체리농부(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 풍년의 화환"),
        SlotV2Engine.Achievement(id = "cmast_farmer_p", emoji = "🍒", name = "체리의 전설", key = "cstage_farmer", threshold = 20, desc = "체리농부(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 풍요의 수호자"),
        // 🪙 알바생 (parttime)
        SlotV2Engine.Achievement(id = "cmast_parttime_b", emoji = "🪙", name = "첫 출근", key = "cstage_parttime", threshold = 5, desc = "알바생(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_parttime_s", emoji = "🪙", name = "성실한 알바", key = "cstage_parttime", threshold = 10, desc = "알바생(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 성실한 알바"),
        SlotV2Engine.Achievement(id = "cmast_parttime_g", emoji = "🪙", name = "에이스 직원", key = "cstage_parttime", threshold = 15, desc = "알바생(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 우수사원 명패"),
        SlotV2Engine.Achievement(id = "cmast_parttime_p", emoji = "🪙", name = "알바의 전설", key = "cstage_parttime", threshold = 20, desc = "알바생(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 시급의 제왕"),
        // 💎 보석상 (jeweler)
        SlotV2Engine.Achievement(id = "cmast_jeweler_b", emoji = "💎", name = "세공 입문", key = "cstage_jeweler", threshold = 5, desc = "보석상(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_jeweler_s", emoji = "💎", name = "숙련 세공사", key = "cstage_jeweler", threshold = 10, desc = "보석상(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 숙련 세공사"),
        SlotV2Engine.Achievement(id = "cmast_jeweler_g", emoji = "💎", name = "보석 명장", key = "cstage_jeweler", threshold = 15, desc = "보석상(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 보석 진열장"),
        SlotV2Engine.Achievement(id = "cmast_jeweler_p", emoji = "💎", name = "보석의 전설", key = "cstage_jeweler", threshold = 20, desc = "보석상(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 원석의 지배자"),
        // 🎓 수석졸업생 (honor)
        SlotV2Engine.Achievement(id = "cmast_honor_b", emoji = "🎓", name = "우등 입문", key = "cstage_honor", threshold = 5, desc = "수석졸업생(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_honor_s", emoji = "🎓", name = "학년 수석", key = "cstage_honor", threshold = 10, desc = "수석졸업생(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 학년 수석"),
        SlotV2Engine.Achievement(id = "cmast_honor_g", emoji = "🎓", name = "전체 수석", key = "cstage_honor", threshold = 15, desc = "수석졸업생(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 수석 졸업장"),
        SlotV2Engine.Achievement(id = "cmast_honor_p", emoji = "🎓", name = "수석의 전설", key = "cstage_honor", threshold = 20, desc = "수석졸업생(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 졸업식의 주인공"),
        // 💀 해골숭배자 (cultist)
        SlotV2Engine.Achievement(id = "cmast_cultist_b", emoji = "💀", name = "입교 의식", key = "cstage_cultist", threshold = 5, desc = "해골숭배자(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_cultist_s", emoji = "💀", name = "충실한 신도", key = "cstage_cultist", threshold = 10, desc = "해골숭배자(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 충실한 신도"),
        SlotV2Engine.Achievement(id = "cmast_cultist_g", emoji = "💀", name = "교단의 사제", key = "cstage_cultist", threshold = 15, desc = "해골숭배자(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 해골 제단"),
        SlotV2Engine.Achievement(id = "cmast_cultist_p", emoji = "💀", name = "숭배의 전설", key = "cstage_cultist", threshold = 20, desc = "해골숭배자(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 죽음을 섬기는 자"),
        // 👑 왕관수집가 (crowncol)
        SlotV2Engine.Achievement(id = "cmast_crowncol_b", emoji = "👑", name = "수집 입문", key = "cstage_crowncol", threshold = 5, desc = "왕관수집가(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_crowncol_s", emoji = "👑", name = "왕관 애호가", key = "cstage_crowncol", threshold = 10, desc = "왕관수집가(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 왕관 애호가"),
        SlotV2Engine.Achievement(id = "cmast_crowncol_g", emoji = "👑", name = "왕관 명인", key = "cstage_crowncol", threshold = 15, desc = "왕관수집가(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 왕관 진열대"),
        SlotV2Engine.Achievement(id = "cmast_crowncol_p", emoji = "👑", name = "수집의 전설", key = "cstage_crowncol", threshold = 20, desc = "왕관수집가(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 왕관의 지배자"),
        // 🍃 미니멀리스트 (minimalist)
        SlotV2Engine.Achievement(id = "cmast_minimalist_b", emoji = "🍃", name = "비움 입문", key = "cstage_minimalist", threshold = 5, desc = "미니멀리스트(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_minimalist_s", emoji = "🍃", name = "절제의 달인", key = "cstage_minimalist", threshold = 10, desc = "미니멀리스트(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 절제의 달인"),
        SlotV2Engine.Achievement(id = "cmast_minimalist_g", emoji = "🍃", name = "비움의 미학", key = "cstage_minimalist", threshold = 15, desc = "미니멀리스트(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 단순함의 잎새"),
        SlotV2Engine.Achievement(id = "cmast_minimalist_p", emoji = "🍃", name = "비움의 전설", key = "cstage_minimalist", threshold = 20, desc = "미니멀리스트(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 무소유의 현자"),
        // 🍀 행운아 (lucky)
        SlotV2Engine.Achievement(id = "cmast_lucky_b", emoji = "🍀", name = "행운 입문", key = "cstage_lucky", threshold = 5, desc = "행운아(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_lucky_s", emoji = "🍀", name = "운수 좋은 날", key = "cstage_lucky", threshold = 10, desc = "행운아(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 운수 좋은 자"),
        SlotV2Engine.Achievement(id = "cmast_lucky_g", emoji = "🍀", name = "행운의 화신", key = "cstage_lucky", threshold = 15, desc = "행운아(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 네 잎 클로버"),
        SlotV2Engine.Achievement(id = "cmast_lucky_p", emoji = "🍀", name = "행운의 전설", key = "cstage_lucky", threshold = 20, desc = "행운아(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 행운의 여신이 택한 자"),
        // 💠 큰손 (highroller)
        SlotV2Engine.Achievement(id = "cmast_highroller_b", emoji = "💠", name = "거래 입문", key = "cstage_highroller", threshold = 5, desc = "큰손(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_highroller_s", emoji = "💠", name = "큰 거래상", key = "cstage_highroller", threshold = 10, desc = "큰손(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 큰 거래상"),
        SlotV2Engine.Achievement(id = "cmast_highroller_g", emoji = "💠", name = "VIP 큰손", key = "cstage_highroller", threshold = 15, desc = "큰손(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: VIP 카드"),
        SlotV2Engine.Achievement(id = "cmast_highroller_p", emoji = "💠", name = "큰손의 전설", key = "cstage_highroller", threshold = 20, desc = "큰손(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 판을 흔드는 큰손"),
        // 🧘 수도승 (monk)
        SlotV2Engine.Achievement(id = "cmast_monk_b", emoji = "🧘", name = "수행 입문", key = "cstage_monk", threshold = 5, desc = "수도승(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_monk_s", emoji = "🧘", name = "정진하는 자", key = "cstage_monk", threshold = 10, desc = "수도승(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 정진하는 자"),
        SlotV2Engine.Achievement(id = "cmast_monk_g", emoji = "🧘", name = "해탈의 경지", key = "cstage_monk", threshold = 15, desc = "수도승(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 깨달음의 후광"),
        SlotV2Engine.Achievement(id = "cmast_monk_p", emoji = "🧘", name = "수행의 전설", key = "cstage_monk", threshold = 20, desc = "수도승(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 무념의 대선사"),
        // ⚗️ 연금술사 (alchemist)
        SlotV2Engine.Achievement(id = "cmast_alchemist_b", emoji = "⚗️", name = "조합 입문", key = "cstage_alchemist", threshold = 5, desc = "연금술사(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_alchemist_s", emoji = "⚗️", name = "능숙한 연금술", key = "cstage_alchemist", threshold = 10, desc = "연금술사(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 능숙한 연금술사"),
        SlotV2Engine.Achievement(id = "cmast_alchemist_g", emoji = "⚗️", name = "현자의 돌", key = "cstage_alchemist", threshold = 15, desc = "연금술사(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 현자의 돌"),
        SlotV2Engine.Achievement(id = "cmast_alchemist_p", emoji = "⚗️", name = "연금의 전설", key = "cstage_alchemist", threshold = 20, desc = "연금술사(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 만물의 변환자"),
        // 😈 무모한도전 (daredevil)
        SlotV2Engine.Achievement(id = "cmast_daredevil_b", emoji = "😈", name = "도전 입문", key = "cstage_daredevil", threshold = 5, desc = "무모한도전(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_daredevil_s", emoji = "😈", name = "겁 없는 자", key = "cstage_daredevil", threshold = 10, desc = "무모한도전(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 겁 없는 자"),
        SlotV2Engine.Achievement(id = "cmast_daredevil_g", emoji = "😈", name = "광기의 질주", key = "cstage_daredevil", threshold = 15, desc = "무모한도전(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 불타는 뿔"),
        SlotV2Engine.Achievement(id = "cmast_daredevil_p", emoji = "😈", name = "무모함의 전설", key = "cstage_daredevil", threshold = 20, desc = "무모한도전(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 한계를 비웃는 자"),
        // 🌟 천재 (prodigy)
        SlotV2Engine.Achievement(id = "cmast_prodigy_b", emoji = "🌟", name = "재능 발현", key = "cstage_prodigy", threshold = 5, desc = "천재(으)로 S5 도달", cat = "캐릭터숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "cmast_prodigy_s", emoji = "🌟", name = "빛나는 영재", key = "cstage_prodigy", threshold = 10, desc = "천재(으)로 S10 도달", cat = "캐릭터숙련", tier = "실버", reward = "칭호: 빛나는 영재"),
        SlotV2Engine.Achievement(id = "cmast_prodigy_g", emoji = "🌟", name = "비범한 천재", key = "cstage_prodigy", threshold = 15, desc = "천재(으)로 S15 도달", cat = "캐릭터숙련", tier = "골드", reward = "프레임: 천재의 별빛"),
        SlotV2Engine.Achievement(id = "cmast_prodigy_p", emoji = "🌟", name = "천재의 전설", key = "cstage_prodigy", threshold = 20, desc = "천재(으)로 S20 도달", cat = "캐릭터숙련", tier = "프리즘", reward = "고급 칭호: 시대를 앞선 천재"),

        // ── 머신 숙련 (mstage_<machineId> 최고스테이지 S5/10/15/20 = 브론즈/실버/골드/프리즘) ──
        // 🎰 기본 (basic)
        SlotV2Engine.Achievement(id = "mmast_basic_b", emoji = "🎰", name = "기본기 다지기", key = "mstage_basic", threshold = 5, desc = "기본 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_basic_s", emoji = "🎰", name = "정석 플레이어", key = "mstage_basic", threshold = 10, desc = "기본 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 정석 플레이어"),
        SlotV2Engine.Achievement(id = "mmast_basic_g", emoji = "🎰", name = "표준의 달인", key = "mstage_basic", threshold = 15, desc = "기본 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 클래식 슬롯"),
        SlotV2Engine.Achievement(id = "mmast_basic_p", emoji = "🎰", name = "기본의 전설", key = "mstage_basic", threshold = 20, desc = "기본 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 정석의 화신"),
        // 🍒 체리 (cherry)
        SlotV2Engine.Achievement(id = "mmast_cherry_b", emoji = "🍒", name = "체리 머신 입문", key = "mstage_cherry", threshold = 5, desc = "체리 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_cherry_s", emoji = "🍒", name = "체리 머신 숙련", key = "mstage_cherry", threshold = 10, desc = "체리 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 체리 머신 숙련가"),
        SlotV2Engine.Achievement(id = "mmast_cherry_g", emoji = "🍒", name = "체리 머신 명인", key = "mstage_cherry", threshold = 15, desc = "체리 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 체리 릴"),
        SlotV2Engine.Achievement(id = "mmast_cherry_p", emoji = "🍒", name = "체리 머신의 전설", key = "mstage_cherry", threshold = 20, desc = "체리 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 체리 릴의 지배자"),
        // 📚 도서관 (library)
        SlotV2Engine.Achievement(id = "mmast_library_b", emoji = "📚", name = "도서관 입문", key = "mstage_library", threshold = 5, desc = "도서관 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_library_s", emoji = "📚", name = "도서관 숙련", key = "mstage_library", threshold = 10, desc = "도서관 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 도서관 단골"),
        SlotV2Engine.Achievement(id = "mmast_library_g", emoji = "📚", name = "도서관 명인", key = "mstage_library", threshold = 15, desc = "도서관 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 지혜의 서가"),
        SlotV2Engine.Achievement(id = "mmast_library_p", emoji = "📚", name = "도서관의 전설", key = "mstage_library", threshold = 20, desc = "도서관 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 지식의 수호자"),
        // 💎 보석 (gem)
        SlotV2Engine.Achievement(id = "mmast_gem_b", emoji = "💎", name = "보석 머신 입문", key = "mstage_gem", threshold = 5, desc = "보석 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_gem_s", emoji = "💎", name = "보석 머신 숙련", key = "mstage_gem", threshold = 10, desc = "보석 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 보석 머신 숙련가"),
        SlotV2Engine.Achievement(id = "mmast_gem_g", emoji = "💎", name = "보석 머신 명인", key = "mstage_gem", threshold = 15, desc = "보석 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 보석 릴"),
        SlotV2Engine.Achievement(id = "mmast_gem_p", emoji = "💎", name = "보석 머신의 전설", key = "mstage_gem", threshold = 20, desc = "보석 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 광채의 지배자"),
        // 🧲 자석 (magnet)
        SlotV2Engine.Achievement(id = "mmast_magnet_b", emoji = "🧲", name = "자석 머신 입문", key = "mstage_magnet", threshold = 5, desc = "자석 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_magnet_s", emoji = "🧲", name = "자석 머신 숙련", key = "mstage_magnet", threshold = 10, desc = "자석 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 콤보 장인"),
        SlotV2Engine.Achievement(id = "mmast_magnet_g", emoji = "🧲", name = "자석 머신 명인", key = "mstage_magnet", threshold = 15, desc = "자석 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 자기장 릴"),
        SlotV2Engine.Achievement(id = "mmast_magnet_p", emoji = "🧲", name = "자석 머신의 전설", key = "mstage_magnet", threshold = 20, desc = "자석 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 인력의 지배자"),
        // ☠ 해골 (skull)
        SlotV2Engine.Achievement(id = "mmast_skull_b", emoji = "☠", name = "해골 머신 입문", key = "mstage_skull", threshold = 5, desc = "해골 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_skull_s", emoji = "☠", name = "해골 머신 숙련", key = "mstage_skull", threshold = 10, desc = "해골 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 위험의 동반자"),
        SlotV2Engine.Achievement(id = "mmast_skull_g", emoji = "☠", name = "해골 머신 명인", key = "mstage_skull", threshold = 15, desc = "해골 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 해골 릴"),
        SlotV2Engine.Achievement(id = "mmast_skull_p", emoji = "☠", name = "해골 머신의 전설", key = "mstage_skull", threshold = 20, desc = "해골 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 사신의 도박판"),
        // 👑 왕관 (crown)
        SlotV2Engine.Achievement(id = "mmast_crown_b", emoji = "👑", name = "왕관 머신 입문", key = "mstage_crown", threshold = 5, desc = "왕관 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_crown_s", emoji = "👑", name = "왕관 머신 숙련", key = "mstage_crown", threshold = 10, desc = "왕관 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 운빨의 귀족"),
        SlotV2Engine.Achievement(id = "mmast_crown_g", emoji = "👑", name = "왕관 머신 명인", key = "mstage_crown", threshold = 15, desc = "왕관 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 왕관 릴"),
        SlotV2Engine.Achievement(id = "mmast_crown_p", emoji = "👑", name = "왕관 머신의 전설", key = "mstage_crown", threshold = 20, desc = "왕관 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 운명을 거머쥔 왕"),
        // 🔥 불꽃 (flame)
        SlotV2Engine.Achievement(id = "mmast_flame_b", emoji = "🔥", name = "불꽃 머신 입문", key = "mstage_flame", threshold = 5, desc = "불꽃 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_flame_s", emoji = "🔥", name = "불꽃 머신 숙련", key = "mstage_flame", threshold = 10, desc = "불꽃 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 배율의 연소자"),
        SlotV2Engine.Achievement(id = "mmast_flame_g", emoji = "🔥", name = "불꽃 머신 명인", key = "mstage_flame", threshold = 15, desc = "불꽃 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 화염 릴"),
        SlotV2Engine.Achievement(id = "mmast_flame_p", emoji = "🔥", name = "불꽃 머신의 전설", key = "mstage_flame", threshold = 20, desc = "불꽃 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 불꽃의 지배자"),
        // 💣 폭탄 (bomb)
        SlotV2Engine.Achievement(id = "mmast_bomb_b", emoji = "💣", name = "폭탄 머신 입문", key = "mstage_bomb", threshold = 5, desc = "폭탄 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_bomb_s", emoji = "💣", name = "폭탄 머신 숙련", key = "mstage_bomb", threshold = 10, desc = "폭탄 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 폭파 전문가"),
        SlotV2Engine.Achievement(id = "mmast_bomb_g", emoji = "💣", name = "폭탄 머신 명인", key = "mstage_bomb", threshold = 15, desc = "폭탄 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 폭탄 릴"),
        SlotV2Engine.Achievement(id = "mmast_bomb_p", emoji = "💣", name = "폭탄 머신의 전설", key = "mstage_bomb", threshold = 20, desc = "폭탄 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 파괴의 지배자"),
        // ⭐ 별빛 (star)
        SlotV2Engine.Achievement(id = "mmast_star_b", emoji = "⭐", name = "별빛 머신 입문", key = "mstage_star", threshold = 5, desc = "별빛 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_star_s", emoji = "⭐", name = "별빛 머신 숙련", key = "mstage_star", threshold = 10, desc = "별빛 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 별빛 항해사"),
        SlotV2Engine.Achievement(id = "mmast_star_g", emoji = "⭐", name = "별빛 머신 명인", key = "mstage_star", threshold = 15, desc = "별빛 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 별빛 릴"),
        SlotV2Engine.Achievement(id = "mmast_star_p", emoji = "⭐", name = "별빛 머신의 전설", key = "mstage_star", threshold = 20, desc = "별빛 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 별자리의 지배자"),
        // 🍀 행운 (clover)
        SlotV2Engine.Achievement(id = "mmast_clover_b", emoji = "🍀", name = "행운 머신 입문", key = "mstage_clover", threshold = 5, desc = "행운 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_clover_s", emoji = "🍀", name = "행운 머신 숙련", key = "mstage_clover", threshold = 10, desc = "행운 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 행운 머신 숙련가"),
        SlotV2Engine.Achievement(id = "mmast_clover_g", emoji = "🍀", name = "행운 머신 명인", key = "mstage_clover", threshold = 15, desc = "행운 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 클로버 릴"),
        SlotV2Engine.Achievement(id = "mmast_clover_p", emoji = "🍀", name = "행운 머신의 전설", key = "mstage_clover", threshold = 20, desc = "행운 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 행운의 지배자"),
        // 🎲 카지노 (casino)
        SlotV2Engine.Achievement(id = "mmast_casino_b", emoji = "🎲", name = "카지노 입문", key = "mstage_casino", threshold = 5, desc = "카지노 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_casino_s", emoji = "🎲", name = "카지노 숙련", key = "mstage_casino", threshold = 10, desc = "카지노 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 고변동의 베터"),
        SlotV2Engine.Achievement(id = "mmast_casino_g", emoji = "🎲", name = "카지노 명인", key = "mstage_casino", threshold = 15, desc = "카지노 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 주사위 릴"),
        SlotV2Engine.Achievement(id = "mmast_casino_p", emoji = "🎲", name = "카지노의 전설", key = "mstage_casino", threshold = 20, desc = "카지노 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 운빨의 제왕"),
        // 🌱 정원 (garden)
        SlotV2Engine.Achievement(id = "mmast_garden_b", emoji = "🌱", name = "정원 머신 입문", key = "mstage_garden", threshold = 5, desc = "정원 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_garden_s", emoji = "🌱", name = "정원 머신 숙련", key = "mstage_garden", threshold = 10, desc = "정원 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 성장의 정원사"),
        SlotV2Engine.Achievement(id = "mmast_garden_g", emoji = "🌱", name = "정원 머신 명인", key = "mstage_garden", threshold = 15, desc = "정원 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 새싹 릴"),
        SlotV2Engine.Achievement(id = "mmast_garden_p", emoji = "🌱", name = "정원 머신의 전설", key = "mstage_garden", threshold = 20, desc = "정원 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 생명의 정원사"),
        // 🌀 와일드 (wildmac)
        SlotV2Engine.Achievement(id = "mmast_wildmac_b", emoji = "🌀", name = "와일드 입문", key = "mstage_wildmac", threshold = 5, desc = "와일드 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_wildmac_s", emoji = "🌀", name = "와일드 숙련", key = "mstage_wildmac", threshold = 10, desc = "와일드 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 세트 조작가"),
        SlotV2Engine.Achievement(id = "mmast_wildmac_g", emoji = "🌀", name = "와일드 명인", key = "mstage_wildmac", threshold = 15, desc = "와일드 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 와일드 릴"),
        SlotV2Engine.Achievement(id = "mmast_wildmac_p", emoji = "🌀", name = "와일드의 전설", key = "mstage_wildmac", threshold = 20, desc = "와일드 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 혼돈의 지배자"),
        // 🗝 금고 (vault)
        SlotV2Engine.Achievement(id = "mmast_vault_b", emoji = "🗝", name = "금고 머신 입문", key = "mstage_vault", threshold = 5, desc = "금고 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_vault_s", emoji = "🗝", name = "금고 머신 숙련", key = "mstage_vault", threshold = 10, desc = "금고 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 금고털이"),
        SlotV2Engine.Achievement(id = "mmast_vault_g", emoji = "🗝", name = "금고 머신 명인", key = "mstage_vault", threshold = 15, desc = "금고 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 황금 열쇠"),
        SlotV2Engine.Achievement(id = "mmast_vault_p", emoji = "🗝", name = "금고 머신의 전설", key = "mstage_vault", threshold = 20, desc = "금고 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 보고의 지배자"),
        // 🌈 무지개 (rainbow)
        SlotV2Engine.Achievement(id = "mmast_rainbow_b", emoji = "🌈", name = "무지개 입문", key = "mstage_rainbow", threshold = 5, desc = "무지개 머신으로 S5 도달", cat = "머신숙련", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "mmast_rainbow_s", emoji = "🌈", name = "무지개 숙련", key = "mstage_rainbow", threshold = 10, desc = "무지개 머신으로 S10 도달", cat = "머신숙련", tier = "실버", reward = "칭호: 한방의 추격자"),
        SlotV2Engine.Achievement(id = "mmast_rainbow_g", emoji = "🌈", name = "무지개 명인", key = "mstage_rainbow", threshold = 15, desc = "무지개 머신으로 S15 도달", cat = "머신숙련", tier = "골드", reward = "프레임: 무지개 릴"),
        SlotV2Engine.Achievement(id = "mmast_rainbow_p", emoji = "🌈", name = "무지개의 전설", key = "mstage_rainbow", threshold = 20, desc = "무지개 머신으로 S20 도달", cat = "머신숙련", tier = "프리즘", reward = "고급 칭호: 일곱 빛깔의 지배자"),

        // ══════════════════════════════════════════════════════════
        //  2차 확장 — 기존 추적 카운터 전용(신규 추적코드 0). 중복 임계 없음.
        // ══════════════════════════════════════════════════════════

        // ── 역전 (lastSpinClears 10/30, closeClears 5/50) ─────────
        SlotV2Engine.Achievement(id = "rv_last10", emoji = "🎯", name = "역전의 명수", key = "lastSpinClears", threshold = 10, desc = "마지막 스핀 클리어 10회", cat = "역전", tier = "골드", reward = "칭호: 역전의 명수"),
        SlotV2Engine.Achievement(id = "rv_last30", emoji = "🎯", name = "막판 승부사", key = "lastSpinClears", threshold = 30, desc = "마지막 스핀 클리어 30회", cat = "역전", tier = "프리즘", reward = "고급 칭호: 운명의 한 스핀"),
        SlotV2Engine.Achievement(id = "rv_close5", emoji = "😰", name = "간발의 차", key = "closeClears", threshold = 5, desc = "잔여 EXP 10이하로 5회 클리어", cat = "역전", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "rv_close50", emoji = "😰", name = "벼랑 끝 곡예", key = "closeClears", threshold = 50, desc = "잔여 EXP 10이하로 50회 클리어", cat = "역전", tier = "프리즘", reward = "고급 칭호: 외줄타기의 달인"),

        // ── 정밀 (exactClears 10/20, set4Plus 5/20) ───────────────
        SlotV2Engine.Achievement(id = "pc_exact10", emoji = "📐", name = "정밀 사격수", key = "exactClears", threshold = 10, desc = "요구치 정확 일치 클리어 10회", cat = "정밀", tier = "골드", reward = "칭호: 정밀 사격수"),
        SlotV2Engine.Achievement(id = "pc_exact20", emoji = "📐", name = "0의 미학", key = "exactClears", threshold = 20, desc = "요구치 정확 일치 클리어 20회", cat = "정밀", tier = "프리즘", reward = "고급 칭호: 빈틈없는 계산가"),
        SlotV2Engine.Achievement(id = "pc_set4_5", emoji = "🍀", name = "네 잎의 행운", key = "set4Plus", threshold = 5, desc = "같은 심볼 4개 이상 5회", cat = "정밀", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "pc_set4_20", emoji = "🍀", name = "정렬의 달인", key = "set4Plus", threshold = 20, desc = "같은 심볼 4개 이상 20회", cat = "정밀", tier = "골드", reward = "칭호: 정렬의 달인"),

        // ── 고점 (bestScore 20k/70k, bestStage 25, maxOverPct 120/150/200/300/500) ──
        SlotV2Engine.Achievement(id = "ov_score20k", emoji = "📈", name = "이만점 클럽", key = "bestScore", threshold = 20000, desc = "한 런 20000점 달성", cat = "고점", tier = "실버", reward = "칭호: 이만점 클럽"),
        SlotV2Engine.Achievement(id = "ov_score70k", emoji = "📈", name = "칠만의 벽", key = "bestScore", threshold = 70000, desc = "한 런 70000점 달성", cat = "고점", tier = "골드", reward = "칭호: 칠만의 벽을 넘은 자"),
        SlotV2Engine.Achievement(id = "ov_stage25", emoji = "🗻", name = "끝없는 등반", key = "bestStage", threshold = 25, desc = "25스테이지 도달", cat = "고점", tier = "프리즘", reward = "고급 칭호: 천공의 등반가"),
        SlotV2Engine.Achievement(id = "ov_over120", emoji = "💥", name = "여유로운 클리어", key = "maxOverPct", threshold = 120, desc = "한 스테이지 요구치 120% 초과 달성", cat = "고점", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "ov_over150", emoji = "💥", name = "넉넉한 한 방", key = "maxOverPct", threshold = 150, desc = "한 스테이지 요구치 150% 초과 달성", cat = "고점", tier = "실버", reward = "칭호: 넉넉한 한 방"),
        SlotV2Engine.Achievement(id = "ov_over200", emoji = "💥", name = "두 배의 폭발", key = "maxOverPct", threshold = 200, desc = "한 스테이지 요구치 200% 초과 달성", cat = "고점", tier = "골드", reward = "칭호: 두 배의 폭발"),
        SlotV2Engine.Achievement(id = "ov_over300", emoji = "💥", name = "압도적 초과", key = "maxOverPct", threshold = 300, desc = "한 스테이지 요구치 300% 초과 달성", cat = "고점", tier = "골드", reward = "칭호: 압도적 초과"),
        SlotV2Engine.Achievement(id = "ov_over500", emoji = "💥", name = "초과의 화신", key = "maxOverPct", threshold = 500, desc = "한 스테이지 요구치 500% 초과 달성", cat = "고점", tier = "프리즘", reward = "고급 칭호: 한계를 부수는 자"),

        // ── 보스 (bossClears 10/30) ───────────────────────────────
        SlotV2Engine.Achievement(id = "bs_boss10", emoji = "👹", name = "보스 토벌대", key = "bossClears", threshold = 10, desc = "보스 10회 클리어", cat = "보스", tier = "실버", reward = "칭호: 보스 토벌대장"),
        SlotV2Engine.Achievement(id = "bs_boss30", emoji = "👹", name = "보스 처형자", key = "bossClears", threshold = 30, desc = "보스 30회 클리어", cat = "보스", tier = "프리즘", reward = "고급 칭호: 보스의 천적"),

        // ── 경제 ──────────────────────────────────────────────────
        SlotV2Engine.Achievement(id = "ec_shop5", emoji = "🛍️", name = "첫 단골", key = "shopBuys", threshold = 5, desc = "상점에서 5회 구매", cat = "경제", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "ec_shop25", emoji = "🛍️", name = "VIP 고객", key = "shopBuys", threshold = 25, desc = "상점에서 25회 구매", cat = "경제", tier = "골드", reward = "칭호: 상점 VIP"),
        SlotV2Engine.Achievement(id = "ec_reroll5", emoji = "🔄", name = "다시 한 번", key = "rerollUses", threshold = 5, desc = "재굴림 5회 사용", cat = "경제", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "ec_reroll30", emoji = "🔄", name = "운명 거부자", key = "rerollUses", threshold = 30, desc = "재굴림 30회 사용", cat = "경제", tier = "골드", reward = "칭호: 운명 거부자"),
        SlotV2Engine.Achievement(id = "ec_gamble5", emoji = "🎲", name = "도박장 단골", key = "gambles", threshold = 5, desc = "도박장 노드 5회 이용", cat = "경제", tier = "실버", reward = "칭호: 도박장 단골"),
        SlotV2Engine.Achievement(id = "ec_gamble30", emoji = "🎲", name = "하우스의 친구", key = "gambles", threshold = 30, desc = "도박장 노드 30회 이용", cat = "경제", tier = "프리즘", reward = "고급 칭호: 하우스의 친구"),
        SlotV2Engine.Achievement(id = "ec_allin10", emoji = "💥", name = "올인 베테랑", key = "allinWins", threshold = 10, desc = "올인 스핀 10회 승리", cat = "경제", tier = "골드", reward = "칭호: 올인 베테랑"),
        SlotV2Engine.Achievement(id = "ec_allin50", emoji = "💥", name = "올인의 전설", key = "allinWins", threshold = 50, desc = "올인 스핀 50회 승리", cat = "경제", tier = "프리즘", reward = "고급 칭호: 올인의 전설"),
        SlotV2Engine.Achievement(id = "ec_pray15", emoji = "🙏", name = "독실한 신자", key = "prayClears", threshold = 15, desc = "기도 후 클리어 15회", cat = "경제", tier = "골드", reward = "칭호: 독실한 신자"),
        SlotV2Engine.Achievement(id = "ec_pray30", emoji = "🙏", name = "기적의 사도", key = "prayClears", threshold = 30, desc = "기도 후 클리어 30회", cat = "경제", tier = "프리즘", reward = "고급 칭호: 기적의 사도"),
        SlotV2Engine.Achievement(id = "ec_jackpot25", emoji = "🎰", name = "잭팟 수집가", key = "jackpots", threshold = 25, desc = "5칸 잭팟 25회 달성", cat = "경제", tier = "골드", reward = "칭호: 잭팟 수집가"),
        SlotV2Engine.Achievement(id = "ec_jackpot50", emoji = "🎰", name = "잭팟의 화신", key = "jackpots", threshold = 50, desc = "5칸 잭팟 50회 달성", cat = "경제", tier = "프리즘", reward = "고급 칭호: 잭팟의 화신"),
        SlotV2Engine.Achievement(id = "ec_prism10", emoji = "🔮", name = "프리즘 애호가", key = "prismPicks", threshold = 10, desc = "프리즘 증강 10회 선택", cat = "경제", tier = "골드", reward = "칭호: 프리즘 애호가"),
        SlotV2Engine.Achievement(id = "ec_prism50", emoji = "🔮", name = "프리즘 마스터", key = "prismPicks", threshold = 50, desc = "프리즘 증강 50회 선택", cat = "경제", tier = "프리즘", reward = "고급 칭호: 프리즘 마스터"),
        SlotV2Engine.Achievement(id = "ec_dev30", emoji = "⚙️", name = "장치 숙련공", key = "deviceUses", threshold = 30, desc = "장치를 30회 사용", cat = "경제", tier = "골드", reward = "칭호: 장치 숙련공"),
        SlotV2Engine.Achievement(id = "ec_dev100", emoji = "⚙️", name = "장치 대가", key = "deviceUses", threshold = 100, desc = "장치를 100회 사용", cat = "경제", tier = "프리즘", reward = "고급 칭호: 장치의 대가"),
        SlotV2Engine.Achievement(id = "ec_item25", emoji = "🎒", name = "아이템 애호가", key = "itemsUsed", threshold = 25, desc = "아이템 25회 사용", cat = "경제", tier = "실버", reward = "칭호: 알뜰 소비자"),
        SlotV2Engine.Achievement(id = "ec_item200", emoji = "🎒", name = "소비의 정점", key = "itemsUsed", threshold = 200, desc = "아이템 200회 사용", cat = "경제", tier = "프리즘", reward = "고급 칭호: 소비의 정점"),
        SlotV2Engine.Achievement(id = "ec_coin3000", emoji = "🪙", name = "조폐국 총재", key = "coinTotal", threshold = 3000, desc = "🪙코인 누적 3000개 등장", cat = "경제", tier = "프리즘", reward = "고급 칭호: 조폐국 총재"),
        SlotV2Engine.Achievement(id = "ec_dev_own8", emoji = "🔧", name = "장치 보유 8종", key = "devicesOwned", threshold = 8, desc = "장치를 8종 영구 보유", cat = "경제", tier = "골드", reward = "칭호: 장치 보유 8종"),

        // ── 빌드 (relicsMax 7/15, curse5Stage·noDevStage·noItemMaxS S10/S15) ──
        SlotV2Engine.Achievement(id = "bd_relic7", emoji = "🏺", name = "유물 수호자", key = "relicsMax", threshold = 7, desc = "한 런에 유물 7개 동시 보유", cat = "빌드", tier = "골드", reward = "칭호: 유물 수호자"),
        SlotV2Engine.Achievement(id = "bd_relic15", emoji = "🏺", name = "유물 박물관장", key = "relicsMax", threshold = 15, desc = "한 런에 유물 15개 동시 보유", cat = "빌드", tier = "프리즘", reward = "고급 칭호: 유물 박물관장"),
        SlotV2Engine.Achievement(id = "bd_curse5_10", emoji = "☠️", name = "저주를 안고", key = "curse5Stage", threshold = 10, desc = "저주 5개 이상 보유로 S10 도달", cat = "빌드", tier = "골드", reward = "칭호: 저주를 안은 등반가"),
        SlotV2Engine.Achievement(id = "bd_curse5_15", emoji = "☠️", name = "저주와 동행", key = "curse5Stage", threshold = 15, desc = "저주 5개 이상 보유로 S15 도달", cat = "빌드", tier = "프리즘", reward = "고급 칭호: 저주를 다스리는 자"),
        SlotV2Engine.Achievement(id = "bd_nodev10", emoji = "🚫", name = "맨손의 등반가", key = "noDevStage", threshold = 10, desc = "장치 없이 S10 도달", cat = "빌드", tier = "골드", reward = "칭호: 맨손의 등반가"),
        SlotV2Engine.Achievement(id = "bd_nodev15", emoji = "🚫", name = "무장치의 달인", key = "noDevStage", threshold = 15, desc = "장치 없이 S15 도달", cat = "빌드", tier = "프리즘", reward = "고급 칭호: 무장치의 달인"),
        SlotV2Engine.Achievement(id = "bd_noitem10", emoji = "🧘", name = "비움의 등반", key = "noItemMaxS", threshold = 10, desc = "아이템 없이 S10 도달", cat = "빌드", tier = "골드", reward = "칭호: 비움의 등반가"),
        SlotV2Engine.Achievement(id = "bd_noitem15", emoji = "🧘", name = "무소유의 경지", key = "noItemMaxS", threshold = 15, desc = "아이템 없이 S15 도달", cat = "빌드", tier = "프리즘", reward = "고급 칭호: 무소유의 경지"),

        // ── 반복 (totalSpins 2000/10000, runs·명령어 미존재 임계) ──
        SlotV2Engine.Achievement(id = "rp_spin2000", emoji = "🔂", name = "이천 번의 스핀", key = "totalSpins", threshold = 2000, desc = "누적 2000회 스핀", cat = "반복", tier = "골드", reward = "칭호: 손목의 장인"),
        SlotV2Engine.Achievement(id = "rp_spin10000", emoji = "🔂", name = "만 번의 스핀", key = "totalSpins", threshold = 10000, desc = "누적 10000회 스핀", cat = "반복", tier = "프리즘", reward = "고급 칭호: 스핀의 화신"),
        SlotV2Engine.Achievement(id = "rp_focus100", emoji = "🎯", name = "집중의 화신", key = "focusUses", threshold = 100, desc = "집중 명령 100회 사용", cat = "반복", tier = "프리즘", reward = "고급 칭호: 집중의 화신"),
        SlotV2Engine.Achievement(id = "rp_last50", emoji = "⏳", name = "최후의 화신", key = "lastUses", threshold = 50, desc = "최후 명령 50회 사용", cat = "반복", tier = "프리즘", reward = "고급 칭호: 최후의 화신"),
        SlotV2Engine.Achievement(id = "rp_reroll50", emoji = "🔄", name = "재굴림의 화신", key = "rerollUses", threshold = 50, desc = "재굴림 50회 사용", cat = "반복", tier = "프리즘", reward = "고급 칭호: 재굴림의 화신"),
        SlotV2Engine.Achievement(id = "rp_pin30", emoji = "📌", name = "고정의 화신", key = "pinUses", threshold = 30, desc = "고정 30회 사용", cat = "반복", tier = "골드", reward = "칭호: 고정의 화신"),

        // ── 한 런 최다잭팟 (maxRunJackpots 2/3/5 — 업적 미존재 카운터) ──
        SlotV2Engine.Achievement(id = "rj_run2", emoji = "🎰", name = "더블 잭팟", key = "maxRunJackpots", threshold = 2, desc = "한 런에 잭팟 2회", cat = "역전", tier = "실버", reward = "칭호: 더블 잭팟"),
        SlotV2Engine.Achievement(id = "rj_run3", emoji = "🎰", name = "트리플 잭팟", key = "maxRunJackpots", threshold = 3, desc = "한 런에 잭팟 3회", cat = "역전", tier = "골드", reward = "칭호: 트리플 잭팟"),
        SlotV2Engine.Achievement(id = "rj_run5", emoji = "🎰", name = "잭팟 폭풍", key = "maxRunJackpots", threshold = 5, desc = "한 런에 잭팟 5회", cat = "역전", tier = "프리즘", reward = "고급 칭호: 잭팟 폭풍의 주인"),

        // ══════════════════════════════════════════════════════════
        //  ACH-3 확장 — 특수심볼 누적 + 스핀단위 히든 (신규 CSV 카운터 추적, 2026-06-30)
        //  추적: SlotV2Service.handleSpin(L633 부근 incMap/spinMax) + gameOver(prayFails).
        // ══════════════════════════════════════════════════════════

        // ── 특수심볼 누적 🌀 와일드 (wildTotal) ──
        SlotV2Engine.Achievement(id = "sp_wild30", emoji = "🌀", name = "와일드 입문", key = "wildTotal", threshold = 30, desc = "🌀와일드 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "sp_wild150", emoji = "🌀", name = "혼돈의 조율사", key = "wildTotal", threshold = 150, desc = "🌀와일드 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 혼돈의 조율사"),
        SlotV2Engine.Achievement(id = "sp_wild500", emoji = "🌀", name = "와일드 마술사", key = "wildTotal", threshold = 500, desc = "🌀와일드 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 와일드 마술사"),
        SlotV2Engine.Achievement(id = "sp_wild1500", emoji = "🌀", name = "혼돈의 지배자", key = "wildTotal", threshold = 1500, desc = "🌀와일드 누적 1500개 등장", cat = "특수심볼", tier = "프리즘", reward = "고급 칭호: 혼돈의 지배자"),
        // ── 특수심볼 누적 🌱 씨앗 (seedTotal) ──
        SlotV2Engine.Achievement(id = "sp_seed30", emoji = "🌱", name = "씨 뿌리기", key = "seedTotal", threshold = 30, desc = "🌱씨앗 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "sp_seed150", emoji = "🌱", name = "성실한 정원사", key = "seedTotal", threshold = 150, desc = "🌱씨앗 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 성실한 정원사"),
        SlotV2Engine.Achievement(id = "sp_seed500", emoji = "🌱", name = "생명의 재배자", key = "seedTotal", threshold = 500, desc = "🌱씨앗 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 생명의 재배자"),
        // ── 특수심볼 누적 🎲 주사위 (diceTotal) ──
        SlotV2Engine.Achievement(id = "sp_dice30", emoji = "🎲", name = "주사위 굴리기", key = "diceTotal", threshold = 30, desc = "🎲주사위 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "sp_dice150", emoji = "🎲", name = "운명의 도박사", key = "diceTotal", threshold = 150, desc = "🎲주사위 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 운명의 도박사"),
        SlotV2Engine.Achievement(id = "sp_dice500", emoji = "🎲", name = "확률의 지배자", key = "diceTotal", threshold = 500, desc = "🎲주사위 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 확률의 지배자"),
        // ── 특수심볼 누적 🗝 열쇠 (keyTotal) ──
        SlotV2Engine.Achievement(id = "sp_key30", emoji = "🗝", name = "열쇠 줍기", key = "keyTotal", threshold = 30, desc = "🗝열쇠 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "sp_key150", emoji = "🗝", name = "금고 단골", key = "keyTotal", threshold = 150, desc = "🗝열쇠 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 금고 단골"),
        SlotV2Engine.Achievement(id = "sp_key500", emoji = "🗝", name = "보고의 열쇠지기", key = "keyTotal", threshold = 500, desc = "🗝열쇠 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 보고의 열쇠지기"),
        // ── 특수심볼 누적 🔥 불꽃 (flameTotal) ──
        SlotV2Engine.Achievement(id = "sp_flame30", emoji = "🔥", name = "불씨 점화", key = "flameTotal", threshold = 30, desc = "🔥불꽃 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "sp_flame150", emoji = "🔥", name = "연소의 달인", key = "flameTotal", threshold = 150, desc = "🔥불꽃 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 연소의 달인"),
        SlotV2Engine.Achievement(id = "sp_flame500", emoji = "🔥", name = "화염의 지배자", key = "flameTotal", threshold = 500, desc = "🔥불꽃 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 화염의 지배자"),
        // ── 특수심볼 누적 🧲 자석 (magnetTotal) ──
        SlotV2Engine.Achievement(id = "sp_magnet30", emoji = "🧲", name = "자석 입문", key = "magnetTotal", threshold = 30, desc = "🧲자석 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "sp_magnet150", emoji = "🧲", name = "끌어당기는 손", key = "magnetTotal", threshold = 150, desc = "🧲자석 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 끌어당기는 손"),
        SlotV2Engine.Achievement(id = "sp_magnet500", emoji = "🧲", name = "인력의 지배자", key = "magnetTotal", threshold = 500, desc = "🧲자석 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 인력의 지배자"),
        // ── 특수심볼 누적 💣 폭탄 (bombTotal) ──
        SlotV2Engine.Achievement(id = "sp_bomb30", emoji = "💣", name = "폭탄 해체반", key = "bombTotal", threshold = 30, desc = "💣폭탄 누적 30개 등장", cat = "특수심볼", tier = "브론즈", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "sp_bomb150", emoji = "💣", name = "폭파의 전문가", key = "bombTotal", threshold = 150, desc = "💣폭탄 누적 150개 등장", cat = "특수심볼", tier = "실버", reward = "칭호: 폭파의 전문가"),
        SlotV2Engine.Achievement(id = "sp_bomb500", emoji = "💣", name = "파괴의 지배자", key = "bombTotal", threshold = 500, desc = "💣폭탄 누적 500개 등장", cat = "특수심볼", tier = "골드", reward = "칭호: 파괴의 지배자"),

        // ── 잭팟 종류 (crownJackpots / wildJackpots) ──
        SlotV2Engine.Achievement(id = "jk_crown1", emoji = "👑", name = "왕관 잭팟", key = "crownJackpots", threshold = 1, desc = "👑왕관 5칸 잭팟 달성", cat = "잭팟", tier = "골드", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "jk_crown5", emoji = "👑", name = "황금의 정렬", key = "crownJackpots", threshold = 5, desc = "👑왕관 잭팟 5회 달성", cat = "잭팟", tier = "프리즘", reward = "고급 칭호: 왕관 잭팟의 제왕"),
        SlotV2Engine.Achievement(id = "jk_wild1", emoji = "🌀", name = "와일드 잭팟", key = "wildJackpots", threshold = 1, desc = "🌀와일드를 끼워 잭팟 달성", cat = "잭팟", tier = "골드", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "jk_wild10", emoji = "🌀", name = "조작된 운명", key = "wildJackpots", threshold = 10, desc = "🌀와일드 포함 잭팟 10회", cat = "잭팟", tier = "프리즘", reward = "고급 칭호: 운명을 조작하는 자"),

        // ── 히든: 한 스핀 같은 심볼 풀(5칸 또는 보조릴 6칸) ──
        SlotV2Engine.Achievement(id = "hid_skull5spin", emoji = "💀", name = "죽음의 한 줄", key = "maxSkullSpin", threshold = 5, desc = "한 스핀에 ☠해골 5개", cat = "히든", tier = "프리즘", reward = "칭호: 죽음의 한 줄", hidden = true),
        SlotV2Engine.Achievement(id = "hid_coin5spin", emoji = "🪙", name = "동전 벼락", key = "maxCoinSpin", threshold = 5, desc = "한 스핀에 🪙코인 5개", cat = "히든", tier = "프리즘", reward = "칭호: 동전 벼락", hidden = true),
        SlotV2Engine.Achievement(id = "hid_cherry5spin", emoji = "🍒", name = "체리 만발", key = "maxCherrySpin", threshold = 5, desc = "한 스핀에 🍒체리 5개", cat = "히든", tier = "골드", reward = "칭호: 체리 만발", hidden = true),
        SlotV2Engine.Achievement(id = "hid_book5spin", emoji = "📘", name = "전권 정렬", key = "maxBookSpin", threshold = 5, desc = "한 스핀에 📘책 5개", cat = "히든", tier = "골드", reward = "칭호: 전권 정렬", hidden = true),
        SlotV2Engine.Achievement(id = "hid_gem5spin", emoji = "💎", name = "보석 일렬", key = "maxGemSpin", threshold = 5, desc = "한 스핀에 💎보석 5개", cat = "히든", tier = "골드", reward = "칭호: 보석 일렬", hidden = true),

        // ── 히든: 🎲올인 폭망 / 🙏기도 실패 (위험 도전의 흔적) ──
        SlotV2Engine.Achievement(id = "hid_allinbust1", emoji = "💀", name = "올인의 대가", key = "allinBusts", threshold = 1, desc = "올인이 ☠2개로 EXP 0이 되었다", cat = "히든", tier = "브론즈", reward = "도감 등록", hidden = true),
        SlotV2Engine.Achievement(id = "hid_allinbust10", emoji = "💀", name = "파산의 길", key = "allinBusts", threshold = 10, desc = "올인 폭망 10회", cat = "히든", tier = "골드", reward = "칭호: 파산의 길", hidden = true),
        SlotV2Engine.Achievement(id = "hid_prayfail1", emoji = "🙏", name = "응답 없는 기도", key = "prayFails", threshold = 1, desc = "기도하고도 스테이지 실패", cat = "히든", tier = "브론즈", reward = "도감 등록", hidden = true),
        SlotV2Engine.Achievement(id = "hid_prayfail10", emoji = "🙏", name = "시험받는 신앙", key = "prayFails", threshold = 10, desc = "기도 실패 10회", cat = "히든", tier = "골드", reward = "칭호: 시험받는 신앙", hidden = true),

        // ══════════════════════════════════════════════════════════
        //  ACH-4 확장 — 제한도전 + 보스별 공략 + 클리어단위 히든 (신규 CSV 카운터, 2026-06-30)
        //  추적: SlotV2Service.addAch4ClearTracking(clearStage). 전부 런상태 파생(DB 스키마 무변경).
        // ══════════════════════════════════════════════════════════

        // ── 제한도전: 프리즘 증강 0개 도달 (noPrismBestStage) ──
        SlotV2Engine.Achievement(id = "lc_noprism5", emoji = "🚷", name = "무지개 금욕", key = "noPrismBestStage", threshold = 5, desc = "🌈프리즘 증강 없이 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "lc_noprism10", emoji = "🚷", name = "절제의 등반", key = "noPrismBestStage", threshold = 10, desc = "🌈프리즘 증강 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 절제의 등반가"),
        SlotV2Engine.Achievement(id = "lc_noprism15", emoji = "🚷", name = "무채색의 정점", key = "noPrismBestStage", threshold = 15, desc = "🌈프리즘 증강 없이 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 무채색의 정점"),
        // ── 제한도전: 유물 0개 도달 (noRelicBestStage) ──
        SlotV2Engine.Achievement(id = "lc_norelic5", emoji = "🛡️", name = "맨몸의 도전", key = "noRelicBestStage", threshold = 5, desc = "🛡️유물 없이 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "lc_norelic10", emoji = "🛡️", name = "유물 없는 길", key = "noRelicBestStage", threshold = 10, desc = "🛡️유물 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 무유물 등반가"),
        SlotV2Engine.Achievement(id = "lc_norelic15", emoji = "🛡️", name = "순수 증강주의", key = "noRelicBestStage", threshold = 15, desc = "🛡️유물 없이 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 순수 증강주의"),
        // ── 제한도전: 골드+프리즘 증강 0개(실버/유물만) 도달 (noGoldBestStage) ──
        SlotV2Engine.Achievement(id = "lc_nogold5", emoji = "🥈", name = "실버 빌드", key = "noGoldBestStage", threshold = 5, desc = "골드↑ 증강 없이 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "lc_nogold10", emoji = "🥈", name = "은의 길", key = "noGoldBestStage", threshold = 10, desc = "골드↑ 증강 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 은의 길"),
        SlotV2Engine.Achievement(id = "lc_nogold12", emoji = "🥈", name = "겸손한 명인", key = "noGoldBestStage", threshold = 12, desc = "골드↑ 증강 없이 S12 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 겸손한 명인"),
        // ── 제한도전: 초보캐릭+기본머신 도달 (basicOnlyBestStage) ──
        SlotV2Engine.Achievement(id = "lc_basic5", emoji = "🐣", name = "맨주먹 신입생", key = "basicOnlyBestStage", threshold = 5, desc = "초보+기본 머신으로 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "lc_basic10", emoji = "🐣", name = "기본기의 증명", key = "basicOnlyBestStage", threshold = 10, desc = "초보+기본 머신으로 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 기본기의 달인"),
        SlotV2Engine.Achievement(id = "lc_basic15", emoji = "🐣", name = "무에서 정점으로", key = "basicOnlyBestStage", threshold = 15, desc = "초보+기본 머신으로 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 무에서 정점으로"),

        // ── 보스별 격파: 📝기말고사 (bossClear_finals) ──
        SlotV2Engine.Achievement(id = "bc_finals1", emoji = "📝", name = "기말고사 합격", key = "bossClear_finals", threshold = 1, desc = "📝기말고사를 처음 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bc_finals10", emoji = "📝", name = "수석 졸업", key = "bossClear_finals", threshold = 10, desc = "📝기말고사 10회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 기말 수석"),
        SlotV2Engine.Achievement(id = "bc_finals_ctr", emoji = "⏰", name = "최후의 답안", key = "bossCounterClear_finals", threshold = 5, desc = "📝기말고사를 막스핀 클리어 5회", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 막판의 천재"),
        // ── 보스별 격파: 👨‍🏫꼰대교수 (bossClear_strict) ──
        SlotV2Engine.Achievement(id = "bc_strict1", emoji = "👨‍🏫", name = "꼰대 통과", key = "bossClear_strict", threshold = 1, desc = "👨‍🏫꼰대교수를 처음 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bc_strict10", emoji = "👨‍🏫", name = "교수의 인정", key = "bossClear_strict", threshold = 10, desc = "👨‍🏫꼰대교수 10회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 교수의 애제자"),
        SlotV2Engine.Achievement(id = "bc_strict_ctr", emoji = "🧩", name = "완벽한 세트", key = "bossCounterClear_strict", threshold = 5, desc = "👨‍🏫꼰대교수를 세트3+ 스핀으로 클리어 5회", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 세트의 장인"),
        // ── 보스별 격파: 🎲운빨심판관 (bossClear_luck) ──
        SlotV2Engine.Achievement(id = "bc_luck1", emoji = "🎲", name = "심판관 통과", key = "bossClear_luck", threshold = 1, desc = "🎲운빨심판관을 처음 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bc_luck10", emoji = "🎲", name = "운명의 총아", key = "bossClear_luck", threshold = 10, desc = "🎲운빨심판관 10회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 운명의 총아"),
        SlotV2Engine.Achievement(id = "bc_luck_ctr", emoji = "🍀", name = "행운의 정렬", key = "bossCounterClear_luck", threshold = 5, desc = "🎲운빨심판관을 ⭐👑🌀 포함 스핀으로 클리어 5회", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 행운의 화신"),
        // ── 보스별 격파: 🎓졸업심사 (bossClear_grad) ──
        SlotV2Engine.Achievement(id = "bc_grad1", emoji = "🎓", name = "졸업 승인", key = "bossClear_grad", threshold = 1, desc = "🎓졸업심사를 처음 클리어", cat = "보스공략", tier = "골드", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bc_grad10", emoji = "🎓", name = "명예 졸업", key = "bossClear_grad", threshold = 10, desc = "🎓졸업심사 10회 클리어", cat = "보스공략", tier = "프리즘", reward = "칭호: 명예 졸업생"),
        SlotV2Engine.Achievement(id = "bc_grad_ctr", emoji = "✋", name = "맨손 졸업", key = "bossCounterClear_grad", threshold = 3, desc = "🎓졸업심사를 무장치로 클리어 3회", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 맨손의 졸업생"),

        // ── 보스 공통 제약 (bossNoItemClears / bossNoDeviceClears / bossOverkillClears) ──
        SlotV2Engine.Achievement(id = "bx_noitem1", emoji = "🧘", name = "무소유의 보스전", key = "bossNoItemClears", threshold = 1, desc = "아이템 없이 보스 클리어", cat = "보스공략", tier = "골드", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bx_noitem10", emoji = "🧘", name = "비움의 정복자", key = "bossNoItemClears", threshold = 10, desc = "아이템 없이 보스 10회 클리어", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 비움의 정복자"),
        SlotV2Engine.Achievement(id = "bx_nodev1", emoji = "🚫", name = "맨몸 보스전", key = "bossNoDeviceClears", threshold = 1, desc = "장치 없이 보스 클리어", cat = "보스공략", tier = "골드", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bx_nodev10", emoji = "🚫", name = "장치 없는 사냥꾼", key = "bossNoDeviceClears", threshold = 10, desc = "장치 없이 보스 10회 클리어", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 장치 없는 사냥꾼"),
        SlotV2Engine.Achievement(id = "bx_overkill1", emoji = "💥", name = "보스 오버킬", key = "bossOverkillClears", threshold = 1, desc = "초과 500%+ 로 보스 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 오버킬"),
        SlotV2Engine.Achievement(id = "bx_overkill10", emoji = "💥", name = "압도적 우위", key = "bossOverkillClears", threshold = 10, desc = "초과 500%+ 로 보스 10회 클리어", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 압도적 지배자"),
        // ── 한 런 보스 3회 격파 (bossStreak3) ──
        SlotV2Engine.Achievement(id = "bx_streak3", emoji = "🔥", name = "보스 삼연참", key = "bossStreak3", threshold = 1, desc = "한 런에 보스 3회 격파(S15+)", cat = "보스공략", tier = "프리즘", reward = "고급 칭호: 보스 삼연참"),

        // ── 클리어 히든: 빈 지갑 / 빚더미 보스 ──
        SlotV2Engine.Achievement(id = "hid_zerocoin1", emoji = "🪙", name = "무일푼 클리어", key = "zeroCoinClears", threshold = 1, desc = "코인 0으로 스테이지 클리어", cat = "히든", tier = "실버", reward = "도감 등록", hidden = true),
        SlotV2Engine.Achievement(id = "hid_zerocoin20", emoji = "🪙", name = "청빈의 도", key = "zeroCoinClears", threshold = 20, desc = "코인 0으로 20회 클리어", cat = "히든", tier = "프리즘", reward = "칭호: 청빈의 도", hidden = true),
        SlotV2Engine.Achievement(id = "hid_debtboss1", emoji = "🧾", name = "빚더미 보스전", key = "debtBossClears", threshold = 1, desc = "빚문서 상태로 보스 클리어", cat = "히든", tier = "골드", reward = "도감 등록", hidden = true),
        SlotV2Engine.Achievement(id = "hid_debtboss5", emoji = "🧾", name = "채무의 승부사", key = "debtBossClears", threshold = 5, desc = "빚문서 상태로 보스 5회 클리어", cat = "히든", tier = "프리즘", reward = "칭호: 채무의 승부사", hidden = true),

        // ── ACH-4 보강: 기존 추적 카운터의 누락 임계 채움 (제한도전 입문 + 보스 카운터 1회 입문) ──
        // 제한도전: 무장치(noDevStage)/무아이템(noItemMaxS)/무상점(noShopS10)/미니멀(minimalistS10) 입문 임계
        SlotV2Engine.Achievement(id = "lc_nodev5", emoji = "🚫", name = "맨손 등반 입문", key = "noDevStage", threshold = 5, desc = "🚫장치 없이 S5 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "lc_noitem8", emoji = "🧘", name = "비움의 등반", key = "noItemMaxS", threshold = 8, desc = "🧘아이템 없이 S8 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "lc_noshop10", emoji = "🪙", name = "자급자족 졸업", key = "noShopS10", threshold = 10, desc = "🛒상점 없이 S10 도달", cat = "제한도전", tier = "골드", reward = "칭호: 자급자족"),
        SlotV2Engine.Achievement(id = "lc_minimalist10", emoji = "🍃", name = "미니멀리스트", key = "minimalistS10", threshold = 1, desc = "유물 3개 이하로 S10 클리어", cat = "제한도전", tier = "실버", reward = "도감 등록"),
        // 보스 카운터(약점 공략) 첫 성공 입문 — 보스별 카운터조건 1회 충족
        SlotV2Engine.Achievement(id = "bc_finals_ctr1", emoji = "⏰", name = "막판의 한 수", key = "bossCounterClear_finals", threshold = 1, desc = "📝기말고사를 막스핀 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bc_strict_ctr1", emoji = "🧩", name = "세트의 첫 인정", key = "bossCounterClear_strict", threshold = 1, desc = "👨‍🏫꼰대교수를 세트3+ 스핀으로 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bc_luck_ctr1", emoji = "🍀", name = "첫 행운의 정렬", key = "bossCounterClear_luck", threshold = 1, desc = "🎲운빨심판관을 ⭐👑🌀 포함 스핀으로 클리어", cat = "보스공략", tier = "실버", reward = "도감 등록"),
        SlotV2Engine.Achievement(id = "bc_grad_ctr1", emoji = "✋", name = "첫 맨손 졸업", key = "bossCounterClear_grad", threshold = 1, desc = "🎓졸업심사를 무장치로 클리어", cat = "보스공략", tier = "골드", reward = "도감 등록"),
        // 보스 공통 제약 중간 임계 (3회)
        SlotV2Engine.Achievement(id = "bx_noitem3", emoji = "🧘", name = "무소유의 연승", key = "bossNoItemClears", threshold = 3, desc = "아이템 없이 보스 3회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 무소유 사냥꾼"),
        SlotV2Engine.Achievement(id = "bx_nodev3", emoji = "🚫", name = "맨몸의 연승", key = "bossNoDeviceClears", threshold = 3, desc = "장치 없이 보스 3회 클리어", cat = "보스공략", tier = "골드", reward = "칭호: 맨몸 사냥꾼"),

        // ══════════════════════════════════════════════════════════
        //  ACH-5a 연구(전공) — 10전공 × 입문/심화/박사 (신규 추적코드 0, 전부 기존 추적 카운터).
        //  ★입문(rs_*_intro)의 (key,threshold)는 SlotV2Engine.SCHOOL_RESEARCH 와 정확히 일치 →
        //    달성 시 해당 school 의 실버/골드 증강·유물 풀 개방(perkUnlocked OR 경로). 프리즘 perk 는 가드로 제외.
        //  심화/박사 = 더 높은 임계의 코스메틱(칭호/프레임)·풀개방 아님. (key,threshold) 기존 374종과 중복 없음.
        // ══════════════════════════════════════════════════════════

        // ── 🌱 성장학 (cherryTotal) — 입문 300 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_growth_intro", emoji = "🌱", name = "성장학 입문", key = "cherryTotal", threshold = 300, desc = "🍒체리 누적 300 — 성장학 연구 완료, 성장학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓성장학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_growth_adv", emoji = "🌱", name = "성장학 심화", key = "cherryTotal", threshold = 600, desc = "🍒체리 누적 600 — 성장학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 성장학 연구원"),
        SlotV2Engine.Achievement(id = "rs_growth_phd", emoji = "🌱", name = "성장학 박사", key = "cherryTotal", threshold = 2000, desc = "🍒체리 누적 2000 — 성장학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 성장학 박사"),
        // ── 🧮 계산학 (set4Plus) — 입문 3 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_calc_intro", emoji = "🧮", name = "계산학 입문", key = "set4Plus", threshold = 3, desc = "같은 심볼 4+ 3회 — 계산학 연구 완료, 계산학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓계산학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_calc_adv", emoji = "🧮", name = "계산학 심화", key = "set4Plus", threshold = 10, desc = "같은 심볼 4+ 10회 — 계산학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 계산학 연구원"),
        SlotV2Engine.Achievement(id = "rs_calc_phd", emoji = "🧮", name = "계산학 박사", key = "set4Plus", threshold = 30, desc = "같은 심볼 4+ 30회 — 계산학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 계산학 박사"),
        // ── 💰 경제학 (coinTotal) — 입문 300 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_econ_intro", emoji = "💰", name = "경제학 입문", key = "coinTotal", threshold = 300, desc = "🪙코인 누적 300 — 경제학 연구 완료, 경제학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓경제학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_econ_adv", emoji = "💰", name = "경제학 심화", key = "coinTotal", threshold = 700, desc = "🪙코인 누적 700 — 경제학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 경제학 연구원"),
        SlotV2Engine.Achievement(id = "rs_econ_phd", emoji = "💰", name = "경제학 박사", key = "coinTotal", threshold = 1500, desc = "🪙코인 누적 1500 — 경제학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 경제학 박사"),
        // ── 🎲 운명학 (gambles) — 입문 3 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_fate_intro", emoji = "🎴", name = "운명학 입문", key = "gambles", threshold = 3, desc = "도박장 3회 — 운명학 연구 완료, 운명학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓운명학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_fate_adv", emoji = "🎴", name = "운명학 심화", key = "gambles", threshold = 15, desc = "도박장 15회 — 운명학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 운명학 연구원"),
        SlotV2Engine.Achievement(id = "rs_fate_phd", emoji = "🎴", name = "운명학 박사", key = "gambles", threshold = 50, desc = "도박장 50회 — 운명학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 운명학 박사"),
        // ── 👑 왕관학 (crownTotal) — 입문 30 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_crown_intro", emoji = "👑", name = "왕관학 입문", key = "crownTotal", threshold = 30, desc = "👑왕관 누적 30 — 왕관학 연구 완료, 왕관학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓왕관학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_crown_adv", emoji = "👑", name = "왕관학 심화", key = "crownTotal", threshold = 60, desc = "👑왕관 누적 60 — 왕관학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 왕관학 연구원"),
        SlotV2Engine.Achievement(id = "rs_crown_phd", emoji = "👑", name = "왕관학 박사", key = "crownTotal", threshold = 200, desc = "👑왕관 누적 200 — 왕관학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 왕관학 박사"),
        // ── 💀 저주학 (skullTotal) — 입문 100 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_curse_intro", emoji = "💀", name = "저주학 입문", key = "skullTotal", threshold = 100, desc = "💀해골 누적 100 — 저주학 연구 완료, 저주학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓저주학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_curse_adv", emoji = "💀", name = "저주학 심화", key = "skullTotal", threshold = 500, desc = "💀해골 누적 500 — 저주학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 저주학 연구원"),
        SlotV2Engine.Achievement(id = "rs_curse_phd", emoji = "💀", name = "저주학 박사", key = "skullTotal", threshold = 700, desc = "💀해골 누적 700 — 저주학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 저주학 박사"),
        // ── ⏳ 시간학 (lastSpinClears) — 입문 3 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_time_intro", emoji = "⏳", name = "시간학 입문", key = "lastSpinClears", threshold = 3, desc = "막판 스핀 클리어 3회 — 시간학 연구 완료, 시간학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓시간학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_time_adv", emoji = "⏳", name = "시간학 심화", key = "lastSpinClears", threshold = 7, desc = "막판 스핀 클리어 7회 — 시간학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 시간학 연구원"),
        SlotV2Engine.Achievement(id = "rs_time_phd", emoji = "⏳", name = "시간학 박사", key = "lastSpinClears", threshold = 15, desc = "막판 스핀 클리어 15회 — 시간학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 시간학 박사"),
        // ── 🔮 프리즘공학 (prismPicks) — 입문 3 = SCHOOL_RESEARCH 트리거. ⚠️PRISM perk 는 연구로 안 열림(엔진 가드) ──
        SlotV2Engine.Achievement(id = "rs_prism_intro", emoji = "🔮", name = "프리즘공학 입문", key = "prismPicks", threshold = 3, desc = "프리즘 선택 3회 — 프리즘공학 연구 완료, 프리즘공학 실버·골드 증강·유물 풀 개방(프리즘 티어 제외)", cat = "연구", tier = "실버", reward = "🔓프리즘공학 증강·유물 풀 개방(프리즘 티어 제외)"),
        SlotV2Engine.Achievement(id = "rs_prism_adv", emoji = "🔮", name = "프리즘공학 심화", key = "prismPicks", threshold = 15, desc = "프리즘 선택 15회 — 프리즘공학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 프리즘공학 연구원"),
        SlotV2Engine.Achievement(id = "rs_prism_phd", emoji = "🔮", name = "프리즘공학 박사", key = "prismPicks", threshold = 30, desc = "프리즘 선택 30회 — 프리즘공학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 프리즘공학 박사"),
        // ── 🌰 씨앗학 (seedTotal) — 입문 10 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_seed_intro", emoji = "🌰", name = "씨앗학 입문", key = "seedTotal", threshold = 10, desc = "🌱씨앗 누적 10 — 씨앗학 연구 완료, 씨앗학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓씨앗학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_seed_adv", emoji = "🌰", name = "씨앗학 심화", key = "seedTotal", threshold = 75, desc = "🌱씨앗 누적 75 — 씨앗학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 씨앗학 연구원"),
        SlotV2Engine.Achievement(id = "rs_seed_phd", emoji = "🌰", name = "씨앗학 박사", key = "seedTotal", threshold = 300, desc = "🌱씨앗 누적 300 — 씨앗학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 씨앗학 박사"),
        // ── 🌀 와일드학 (wildTotal) — 입문 10 = SCHOOL_RESEARCH 트리거 ──
        SlotV2Engine.Achievement(id = "rs_wild_intro", emoji = "🌀", name = "와일드학 입문", key = "wildTotal", threshold = 10, desc = "🌀와일드 누적 10 — 와일드학 연구 완료, 와일드학 증강·유물 풀 개방", cat = "연구", tier = "실버", reward = "🔓와일드학 증강·유물 풀 개방"),
        SlotV2Engine.Achievement(id = "rs_wild_adv", emoji = "🌀", name = "와일드학 심화", key = "wildTotal", threshold = 75, desc = "🌀와일드 누적 75 — 와일드학 심화 연구", cat = "연구", tier = "골드", reward = "칭호: 와일드학 연구원"),
        SlotV2Engine.Achievement(id = "rs_wild_phd", emoji = "🌀", name = "와일드학 박사", key = "wildTotal", threshold = 300, desc = "🌀와일드 누적 300 — 와일드학 박사 학위", cat = "연구", tier = "프리즘", reward = "고급 칭호: 와일드학 박사"),

        // ══════════════════════════════════════════════════════════
        //  ACH-5b 장치 면허 — 12 메인 장치 전용 면허 업적 (#9 정합, 2026-06-30).
        //  key = lic_<deviceId> = composeStat 파생키(면허 조건표의 기존 추적 stat AND → 1/0, 신규 추적/DB 0).
        //  threshold = 1, tier = 골드(인플레 최소). 달성 = 해당 장치 영구해금(Device.unlockAch 매핑).
        //  보조 4 장치(syllabus/holdfile/retake/major)는 면허 미적용 — 기존 업적 매핑 유지.
        // ══════════════════════════════════════════════════════════
        SlotV2Engine.Achievement(id = "lic_safe", emoji = "🦺", name = "안전벨트 면허", key = "lic_dev_safe", threshold = 1, desc = "아슬아슬 클리어 5회 & S6 도달 — 🦺안전벨트 영구해금", cat = "면허", tier = "골드", reward = "🦺안전벨트 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_seal", emoji = "🔒", name = "봉인장막 면허", key = "lic_dev_seal", threshold = 1, desc = "💀해골 누적 200 & S8 도달 — 🔒봉인장막 영구해금", cat = "면허", tier = "골드", reward = "🔒봉인장막 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_reroll", emoji = "🔄", name = "재굴림기 면허", key = "lic_dev_reroll", threshold = 1, desc = "보스 3회 클리어 & 막판 클리어 3회 — 🔄재굴림기 영구해금", cat = "면허", tier = "골드", reward = "🔄재굴림기 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_pin", emoji = "📌", name = "고정핀 면허", key = "lic_dev_pin", threshold = 1, desc = "정확 클리어 3회 & S8 도달 — 📌고정핀 영구해금", cat = "면허", tier = "골드", reward = "📌고정핀 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_coin", emoji = "🪙", name = "코인투입구 면허", key = "lic_dev_coin", threshold = 1, desc = "🪙코인 누적 500 & 상점구매 15회 — 🪙코인투입구 영구해금", cat = "면허", tier = "골드", reward = "🪙코인투입구 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_subreel", emoji = "➕", name = "보조릴 면허", key = "lic_dev_subreel", threshold = 1, desc = "잭팟 5회 & 4세트+ 10회 — ➕보조릴 영구해금", cat = "면허", tier = "골드", reward = "➕보조릴 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_overheat", emoji = "♨️", name = "과열코어 면허", key = "lic_dev_overheat", threshold = 1, desc = "막판 클리어 10회 & 최고점수 20,000 — ♨️과열코어 영구해금", cat = "면허", tier = "골드", reward = "♨️과열코어 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_oracle", emoji = "🔮", name = "예언안경 면허", key = "lic_dev_oracle", threshold = 1, desc = "기도 클리어 3회 & S15 도달 — 🔮예언안경 영구해금", cat = "면허", tier = "골드", reward = "🔮예언안경 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_copy", emoji = "📑", name = "복사기 면허", key = "lic_dev_copy", threshold = 1, desc = "프리즘 선택 10회 & 4세트+ 10회 — 📑복사기 영구해금", cat = "면허", tier = "골드", reward = "📑복사기 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_swap", emoji = "🔃", name = "교체기 면허", key = "lic_dev_swap", threshold = 1, desc = "보스 10회 클리어 & S15 도달 — 🔃교체기 영구해금", cat = "면허", tier = "골드", reward = "🔃교체기 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_bell", emoji = "🔔", name = "비상졸업벨 면허", key = "lic_dev_bell", threshold = 1, desc = "아슬아슬 클리어 30회 & 보스 8회 클리어 — 🔔비상졸업벨 영구해금", cat = "면허", tier = "골드", reward = "🔔비상졸업벨 장치 영구해금"),
        SlotV2Engine.Achievement(id = "lic_flame", emoji = "🔥", name = "불꽃엔진 면허", key = "lic_dev_flame", threshold = 1, desc = "최고점수 50,000 & S20 도달 — 🔥불꽃엔진 영구해금", cat = "면허", tier = "골드", reward = "🔥불꽃엔진 장치 영구해금"),

        // ══════════════════════════════════════════════════════════
        //  ACH-5c 장치 숙련/장인 + 무명령/무조작 제한도전 (2026-06-30, 추적코드 선행 완료분 기반).
        //  ★숙련 = dvuse_<deviceId> (장착 런수 inc, launchRun) threshold 10.
        //  ★장인 = dvstage_<deviceId> (장착 도달 최고 클리어 S, clearStage setMax) threshold 15.
        //   12 메인 장치 각 숙련/장인 = 24. id 접두 dm_<id>_use / dm_<id>_master (유니크).
        //  ★무명령(noCommandBestStage)/무조작(noRerollBestStage) = run 플래그 0 일 때 setMax(S).
        //   id 접두 rc_nocmd* / rc_noreroll*. 전부 추적확인 키·기존 432종과 (key,threshold)·id 중복 0·코스메틱.
        // ══════════════════════════════════════════════════════════

        // ── 장치 숙련/장인: 🔥불꽃엔진 ──
        SlotV2Engine.Achievement(id = "dm_dev_flame_use", emoji = "🔥", name = "불꽃엔진 숙련", key = "dvuse_dev_flame", threshold = 10, desc = "🔥불꽃엔진 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 불꽃엔진 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_flame_master", emoji = "🔥", name = "불꽃엔진 장인", key = "dvstage_dev_flame", threshold = 15, desc = "🔥불꽃엔진 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 불꽃엔진 장인"),
        // ── 장치 숙련/장인: 🔒봉인장막 ──
        SlotV2Engine.Achievement(id = "dm_dev_seal_use", emoji = "🔒", name = "봉인장막 숙련", key = "dvuse_dev_seal", threshold = 10, desc = "🔒봉인장막 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 봉인장막 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_seal_master", emoji = "🔒", name = "봉인장막 장인", key = "dvstage_dev_seal", threshold = 15, desc = "🔒봉인장막 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 봉인장막 장인"),
        // ── 장치 숙련/장인: 🦺안전벨트 ──
        SlotV2Engine.Achievement(id = "dm_dev_safe_use", emoji = "🦺", name = "안전벨트 숙련", key = "dvuse_dev_safe", threshold = 10, desc = "🦺안전벨트 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 안전벨트 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_safe_master", emoji = "🦺", name = "안전벨트 장인", key = "dvstage_dev_safe", threshold = 15, desc = "🦺안전벨트 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 안전벨트 장인"),
        // ── 장치 숙련/장인: ♨️과열코어 ──
        SlotV2Engine.Achievement(id = "dm_dev_overheat_use", emoji = "♨️", name = "과열코어 숙련", key = "dvuse_dev_overheat", threshold = 10, desc = "♨️과열코어 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 과열코어 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_overheat_master", emoji = "♨️", name = "과열코어 장인", key = "dvstage_dev_overheat", threshold = 15, desc = "♨️과열코어 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 과열코어 장인"),
        // ── 장치 숙련/장인: ➕보조릴 ──
        SlotV2Engine.Achievement(id = "dm_dev_subreel_use", emoji = "➕", name = "보조릴 숙련", key = "dvuse_dev_subreel", threshold = 10, desc = "➕보조릴 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 보조릴 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_subreel_master", emoji = "➕", name = "보조릴 장인", key = "dvstage_dev_subreel", threshold = 15, desc = "➕보조릴 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 보조릴 장인"),
        // ── 장치 숙련/장인: 🪙코인투입구 ──
        SlotV2Engine.Achievement(id = "dm_dev_coin_use", emoji = "🪙", name = "코인투입구 숙련", key = "dvuse_dev_coin", threshold = 10, desc = "🪙코인투입구 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 코인투입구 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_coin_master", emoji = "🪙", name = "코인투입구 장인", key = "dvstage_dev_coin", threshold = 15, desc = "🪙코인투입구 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 코인투입구 장인"),
        // ── 장치 숙련/장인: 🔄재굴림기 ──
        SlotV2Engine.Achievement(id = "dm_dev_reroll_use", emoji = "🔄", name = "재굴림기 숙련", key = "dvuse_dev_reroll", threshold = 10, desc = "🔄재굴림기 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 재굴림기 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_reroll_master", emoji = "🔄", name = "재굴림기 장인", key = "dvstage_dev_reroll", threshold = 15, desc = "🔄재굴림기 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 재굴림기 장인"),
        // ── 장치 숙련/장인: 📌고정핀 ──
        SlotV2Engine.Achievement(id = "dm_dev_pin_use", emoji = "📌", name = "고정핀 숙련", key = "dvuse_dev_pin", threshold = 10, desc = "📌고정핀 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 고정핀 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_pin_master", emoji = "📌", name = "고정핀 장인", key = "dvstage_dev_pin", threshold = 15, desc = "📌고정핀 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 고정핀 장인"),
        // ── 장치 숙련/장인: 📑복사기 ──
        SlotV2Engine.Achievement(id = "dm_dev_copy_use", emoji = "📑", name = "복사기 숙련", key = "dvuse_dev_copy", threshold = 10, desc = "📑복사기 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 복사기 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_copy_master", emoji = "📑", name = "복사기 장인", key = "dvstage_dev_copy", threshold = 15, desc = "📑복사기 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 복사기 장인"),
        // ── 장치 숙련/장인: 🔃교체기 ──
        SlotV2Engine.Achievement(id = "dm_dev_swap_use", emoji = "🔃", name = "교체기 숙련", key = "dvuse_dev_swap", threshold = 10, desc = "🔃교체기 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 교체기 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_swap_master", emoji = "🔃", name = "교체기 장인", key = "dvstage_dev_swap", threshold = 15, desc = "🔃교체기 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 교체기 장인"),
        // ── 장치 숙련/장인: 🔮예언안경 ──
        SlotV2Engine.Achievement(id = "dm_dev_oracle_use", emoji = "🔮", name = "예언안경 숙련", key = "dvuse_dev_oracle", threshold = 10, desc = "🔮예언안경 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 예언안경 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_oracle_master", emoji = "🔮", name = "예언안경 장인", key = "dvstage_dev_oracle", threshold = 15, desc = "🔮예언안경 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 예언안경 장인"),
        // ── 장치 숙련/장인: 🔔비상졸업벨 ──
        SlotV2Engine.Achievement(id = "dm_dev_bell_use", emoji = "🔔", name = "비상졸업벨 숙련", key = "dvuse_dev_bell", threshold = 10, desc = "🔔비상졸업벨 장착으로 10런 시작", cat = "장치면허", tier = "골드", reward = "칭호: 비상졸업벨 숙련자"),
        SlotV2Engine.Achievement(id = "dm_dev_bell_master", emoji = "🔔", name = "비상졸업벨 장인", key = "dvstage_dev_bell", threshold = 15, desc = "🔔비상졸업벨 장착으로 S15 클리어", cat = "장치면허", tier = "프리즘", reward = "고급 칭호: 비상졸업벨 장인"),

        // ── 제한도전: 무명령(특수 스핀명령 0회) 도달 (noCommandBestStage) ──
        SlotV2Engine.Achievement(id = "rc_nocmd10", emoji = "🤐", name = "무언의 등반", key = "noCommandBestStage", threshold = 10, desc = "🤐집중/올인/기도/최후 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 무언의 등반가"),
        SlotV2Engine.Achievement(id = "rc_nocmd15", emoji = "🤐", name = "침묵의 정점", key = "noCommandBestStage", threshold = 15, desc = "🤐집중/올인/기도/최후 없이 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 침묵의 정점"),
        // ── 제한도전: 무조작(재굴림/고정/복사/교체 0회) 도달 (noRerollBestStage) ──
        SlotV2Engine.Achievement(id = "rc_noreroll10", emoji = "🙌", name = "운명에 맡긴 등반", key = "noRerollBestStage", threshold = 10, desc = "🙌재굴림/고정/복사/교체 없이 S10 클리어", cat = "제한도전", tier = "골드", reward = "칭호: 운명에 맡긴 자"),
        SlotV2Engine.Achievement(id = "rc_noreroll15", emoji = "🙌", name = "무조작의 정점", key = "noRerollBestStage", threshold = 15, desc = "🙌재굴림/고정/복사/교체 없이 S15 클리어", cat = "제한도전", tier = "프리즘", reward = "고급 칭호: 무조작의 정점"),

        // ══════════════════════════════════════════════════════════
        //  ACH-6 명령비 지출(경제) — 특수 스핀명령 코인 비용 부과(잭팟런 v3) 연동.
        //  추적: SlotV2Service handleSpin incMap(cmdCoin_focus/pray/allin/total, 차감 시점) +
        //        clearStage clearInc(lastClears = ⏰최후로 클리어, bossAllinClear = 👑보스에서 🎲올인+클리어).
        //  신규 추적/DB 0 — 전부 추적확인 키. 기존 460종과 (key,threshold)·id 중복 0. 보상=코스메틱(칭호/힌트).
        // ══════════════════════════════════════════════════════════
        SlotV2Engine.Achievement(id = "cc_focus10", emoji = "🎯", name = "집중 투자", key = "cmdCoin_focus", threshold = 10, desc = "🎯집중 명령에 코인 누적 10 지출", cat = "경제", tier = "실버", reward = "칭호: 집중 투자자"),
        SlotV2Engine.Achievement(id = "cc_pray30", emoji = "🙏", name = "유료 기도", key = "cmdCoin_pray", threshold = 30, desc = "🙏기도 명령에 코인 누적 30 지출 — 운명/연구의 가호를 산 자", cat = "경제", tier = "골드", reward = "칭호: 유료 기도자"),
        SlotV2Engine.Achievement(id = "cc_allin50", emoji = "🎲", name = "진짜 올인", key = "cmdCoin_allin", threshold = 50, desc = "🎲올인 명령에 코인 누적 50 지출", cat = "경제", tier = "골드", reward = "칭호: 진짜 도박사"),
        SlotV2Engine.Achievement(id = "cc_lastclear5", emoji = "⏰", name = "마지막 결제", key = "lastClears", threshold = 5, desc = "⏰최후 명령으로 스테이지 5회 클리어", cat = "경제", tier = "골드", reward = "칭호: 막판 결제자"),
        SlotV2Engine.Achievement(id = "cc_total100", emoji = "🪙", name = "명령비 지출왕", key = "cmdCoinTotal", threshold = 100, desc = "🪙특수 명령에 코인 누적 100 지출", cat = "경제", tier = "프리즘", reward = "고급 칭호: 명령비 지출왕"),
        SlotV2Engine.Achievement(id = "cc_bossallin1", emoji = "💸", name = "비싼 졸업", key = "bossAllinClear", threshold = 1, desc = "👑보스에서 🎲올인을 쓰고 클리어", cat = "히든", tier = "프리즘", reward = "칭호: 비싼 졸업생", hidden = true),

        // ══════════════════════════════════════════════════════════
        //  ACH-6 빌드 도감 — 25 테마빌드(THEME_BUILDS) 완성을 보상(2026-06-30).
        //  추적: bld_<id> 완성 플래그(evalThemeBuilds→setMax 1) → SlotV2Engine.themeBuildStats() 파생키,
        //        composeStat 가 stat 에 머지. 신규 추적/DB 0 — 전부 기존 bld_* 의 순수 파생.
        //  파생키: bldCat_<category>(카테고리별 완성수, 5개 카테고리 각 5빌드) · bldTotal(전체완성수) ·
        //          bldAllBasic(완성≥1 카테고리 수, =5 전공) · bldAllMaster(전부완성 카테고리 수, =5 마스터).
        //  id 접두 bdx_(기존 bd_* 와 분리). 기존 466종과 (key,threshold)·id 중복 0. 보상=코스메틱(칭호/도감장식/프레임).
        // ══════════════════════════════════════════════════════════
        // ── 카테고리 입문(각 1개 완성) — bldCat_<cat> >= 1 ──
        SlotV2Engine.Achievement(id = "bdx_intro_growth", emoji = "📈", name = "성장형 빌드 입문", key = "bldCat_성장형", threshold = 1, desc = "성장형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 성장형 입문자"),
        SlotV2Engine.Achievement(id = "bdx_intro_fate", emoji = "🔮", name = "운명형 빌드 입문", key = "bldCat_운명형", threshold = 1, desc = "운명형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 운명형 입문자"),
        SlotV2Engine.Achievement(id = "bdx_intro_reversal", emoji = "🧗", name = "역전형 빌드 입문", key = "bldCat_역전형", threshold = 1, desc = "역전형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 역전형 입문자"),
        SlotV2Engine.Achievement(id = "bdx_intro_combo", emoji = "🔗", name = "조합형 빌드 입문", key = "bldCat_조합형", threshold = 1, desc = "조합형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 조합형 입문자"),
        SlotV2Engine.Achievement(id = "bdx_intro_risk", emoji = "☠", name = "위험형 빌드 입문", key = "bldCat_위험형", threshold = 1, desc = "위험형 테마빌드를 1개 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 위험형 입문자"),
        // ── 카테고리 마스터(전체 5빌드 완성) — bldCat_<cat> >= 5 ──
        SlotV2Engine.Achievement(id = "bdx_master_growth", emoji = "📈", name = "성장형 빌드 마스터", key = "bldCat_성장형", threshold = 5, desc = "성장형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 성장형 마스터"),
        SlotV2Engine.Achievement(id = "bdx_master_fate", emoji = "🔮", name = "운명형 빌드 마스터", key = "bldCat_운명형", threshold = 5, desc = "운명형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 운명형 마스터"),
        SlotV2Engine.Achievement(id = "bdx_master_reversal", emoji = "🧗", name = "역전형 빌드 마스터", key = "bldCat_역전형", threshold = 5, desc = "역전형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 역전형 마스터"),
        SlotV2Engine.Achievement(id = "bdx_master_combo", emoji = "🔗", name = "조합형 빌드 마스터", key = "bldCat_조합형", threshold = 5, desc = "조합형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 조합형 마스터"),
        SlotV2Engine.Achievement(id = "bdx_master_risk", emoji = "☠", name = "위험형 빌드 마스터", key = "bldCat_위험형", threshold = 5, desc = "위험형 테마빌드 5개 전부 완성", cat = "빌드도감", tier = "골드", reward = "프레임: 위험형 마스터"),
        // ── 총완성 마일스톤 — bldTotal ──
        SlotV2Engine.Achievement(id = "bdx_total5", emoji = "📖", name = "빌드 수집 시작", key = "bldTotal", threshold = 5, desc = "테마빌드 누적 5종 완성", cat = "빌드도감", tier = "실버", reward = "칭호: 빌드 수집가"),
        SlotV2Engine.Achievement(id = "bdx_total10", emoji = "📚", name = "빌드 도감 절반", key = "bldTotal", threshold = 10, desc = "테마빌드 누적 10종 완성", cat = "빌드도감", tier = "골드", reward = "도감 장식: 빌드 책갈피"),
        SlotV2Engine.Achievement(id = "bdx_total15", emoji = "🗂️", name = "빌드 연구가", key = "bldTotal", threshold = 15, desc = "테마빌드 누적 15종 완성", cat = "빌드도감", tier = "골드", reward = "도감 장식: 빌드 인장"),
        SlotV2Engine.Achievement(id = "bdx_total25", emoji = "🏆", name = "빌드 도감 완성", key = "bldTotal", threshold = 25, desc = "테마빌드 25종 전부 완성", cat = "빌드도감", tier = "프리즘", reward = "프리즘 칭호: 빌드 도감 완성자"),
        // ── 전 카테고리 — bldAllBasic / bldAllMaster ──
        SlotV2Engine.Achievement(id = "bdx_all_basic", emoji = "🎓", name = "전공 선택 완료", key = "bldAllBasic", threshold = 5, desc = "5개 빌드 카테고리에서 각각 1개+ 완성", cat = "빌드도감", tier = "골드", reward = "칭호: 전공 선택 완료"),
        SlotV2Engine.Achievement(id = "bdx_all_master", emoji = "👨‍🏫", name = "잭팟런 교수", key = "bldAllMaster", threshold = 5, desc = "5개 빌드 카테고리를 전부 마스터", cat = "빌드도감", tier = "프리즘", reward = "프리즘 칭호: 잭팟런 교수"),
    )
}
