// ── 잭팟 슬롯 (웹 단독판) — 데이터 카탈로그 ────────────────────────────
// 소스: SlotV2Engine.kt 의 밸런스 수치를 그대로 옮김. 이 게임은 브라우저에서 100% 로컬로
// 동작하며, 기존 카톡 잭팟런(봇/Firebase 노드)에는 일절 접근하지 않는다(완전 격리).
// 효과 로직은 engine.js 의 buildMods/applyItemMods/evaluate 가 담당. 여기는 정의/표시값만.

export const C = {
  REEL: 5, SPINS_PER_STAGE: 5, MIN_SPINS: 3,
  BOMB_EXP_PER: 8, MAX_SPIN_EXP_MUL: 8.0, UNLUCKY_MAX: 5, SKULL_PENALTY: 3,
  KEY_COIN_PER: 4,
  CLEAR_COIN: 5, BOSS_COIN: 12, ELITE_COIN: 8,
  SCORE_PER_LEFTOVER: 2, SCORE_PER_LEFTSPIN: 100, CLOSE_CLEAR_BONUS: 150, BOSS_CLEAR_SCORE: 500,
  // 특수 스핀명령 코인 비용 (engine.cmdCoinCost). 0=일반 스핀(무료) 유지·보스 +1·상한 5.
  CMD_COST_FOCUS: 1, CMD_COST_LAST: 2, CMD_COST_PRAY: 3, CMD_COST_ALLIN: 4,
  CMD_COST_BOSS_SURCHARGE: 1, CMD_COST_MAX: 5,
};

// 스테이지별 요구 EXP. 15 이후 ×1.2.
export const QUOTAS = [110, 120, 130, 140, 150, 170, 200, 235, 280, 330, 390, 460, 540, 640, 755];

// 같은 값심볼 N개 세트 보너스 (index = 개수 0~5)
export const SET_EXP = [0, 0, 8, 18, 42, 100];
export const SET_SCORE = [0, 0, 3, 9, 24, 70];

// ── 심볼 (10 활성 + 4 휴면) ──────────────────────────────────────────
//  special: NONE|WILD|BOMB|MAGNET|SKULL|DICE|COIN|KEY|FLAME|SEED
export const SYMS = [
  { id: "cherry", e: "🍒", n: "체리", exp: 3, score: 0, coin: 0, weight: 25, special: "NONE", rare: false, tags: ["생명"] },
  { id: "book", e: "📘", n: "책", exp: 6, score: 0, coin: 0, weight: 18, special: "NONE", rare: false, tags: ["학습"] },
  { id: "star", e: "⭐", n: "별", exp: 8, score: 0, coin: 0, weight: 13, special: "NONE", rare: false, tags: ["콤보"] },
  { id: "gem", e: "💎", n: "보석", exp: 1, score: 15, coin: 0, weight: 12, special: "NONE", rare: false, tags: ["점수"] },
  { id: "coin", e: "🪙", n: "코인", exp: 0, score: 0, coin: 1, weight: 10, special: "COIN", rare: false, tags: ["코인"] },
  { id: "skull", e: "☠", n: "해골", exp: 0, score: 0, coin: 0, weight: 10, special: "SKULL", rare: false, tags: ["저주"] },
  { id: "flame", e: "🔥", n: "불꽃", exp: 0, score: 0, coin: 0, weight: 5, special: "FLAME", rare: false, tags: ["배율"] },
  { id: "magnet", e: "🧲", n: "자석", exp: 2, score: 0, coin: 0, weight: 4, special: "MAGNET", rare: false, tags: ["조작"] },
  { id: "bomb", e: "💣", n: "폭탄", exp: 5, score: 0, coin: 0, weight: 2, special: "BOMB", rare: false, tags: ["폭발"] },
  { id: "crown", e: "👑", n: "왕관", exp: 20, score: 50, coin: 0, weight: 1, special: "NONE", rare: true, tags: ["왕관", "희귀"] },
  // 휴면(weight 0; 머신/증강/아이템이 주입)
  { id: "key", e: "🗝", n: "열쇠", exp: 6, score: 0, coin: 0, weight: 0, special: "KEY", rare: false, tags: ["열쇠"] },
  { id: "dice", e: "🎲", n: "주사위", exp: 0, score: 0, coin: 0, weight: 0, special: "DICE", rare: false, tags: ["운"] },
  { id: "seed", e: "🌱", n: "씨앗", exp: 0, score: 0, coin: 0, weight: 0, special: "SEED", rare: false, tags: ["생명", "성장"] },
  { id: "wild", e: "🌀", n: "와일드", exp: 0, score: 0, coin: 0, weight: 0, special: "WILD", rare: true, tags: ["희귀", "조작"] },
  // ── 심화모드(주머니) 전용 상위/변형 심볼 (weight:0 = 일반모드 확률 무영향; 주머니로만 등장) ──
  //  evaluate 는 심볼의 exp/score/coin/special/tags 만 소비하므로 아래 필드만으로 효과 자동 배선(신규 로직 0줄).
  //  계열 확장(단순 경험치/점수/위험) 수준 — 복잡 특수효과는 후속 Phase. special 은 기존 계열 재사용.
  { id: "cherry_ripe", e: "🍑", n: "숙성체리", exp: 6,  score: 0,  coin: 0, weight: 0, special: "NONE",  rare: false, tags: ["생명"] },
  { id: "tome",        e: "📖", n: "족보",     exp: 12, score: 0,  coin: 0, weight: 0, special: "NONE",  rare: false, tags: ["학습"] },
  { id: "gem_cut",     e: "💠", n: "연마보석", exp: 2,  score: 32, coin: 0, weight: 0, special: "NONE",  rare: false, tags: ["점수"] },
  { id: "coin_bag",    e: "💰", n: "돈주머니", exp: 0,  score: 0,  coin: 3, weight: 0, special: "COIN",  rare: false, tags: ["코인"] },
  { id: "skull_black", e: "💀", n: "검은해골", exp: 0,  score: 0,  coin: 0, weight: 0, special: "SKULL", rare: false, tags: ["저주"] },
  { id: "ember",       e: "🎇", n: "불씨",     exp: 0,  score: 0,  coin: 0, weight: 0, special: "FLAME", rare: false, tags: ["배율"] },
  // ── 심화모드(주머니) 특수심볼 30종 (Phase 4, 2026-06-29) — weight:0(일반모드 무등장·주머니로만) ──
  //  신규 special 계열. evaluate 는 미지 special 을 NONE 취급(exp/score/coin/tags 만 흡수)하므로,
  //  각 계열별 신규효과는 engine.js evaluate 의 additive "has_X 게이트" 블록(해당 심볼이 셀에 있을 때만 발동)에서만 구현.
  //  다음스핀/메타 상태는 evaluate 반환 신규필드 → game.js 가 소비(담당 2/2). 일반모드 evaluate 경로 무수정.
  //  성장
  { id: "seed_basic",   e: "🌱", n: "씨앗",       exp: 0, score: 0, coin: 0, weight: 0, special: "SEED_ANY",     rare: false, tags: ["생명", "성장"] },
  { id: "sprout",       e: "🌿", n: "새싹",       exp: 0, score: 0, coin: 0, weight: 0, special: "SEED_HIGH",    rare: false, tags: ["생명", "성장"] },
  //  변환
  { id: "catalyst",     e: "🧪", n: "촉매",       exp: 0, score: 0, coin: 0, weight: 0, special: "CATALYST",     rare: false, tags: ["변환"] },
  { id: "purifier",     e: "🧹", n: "정화도구",   exp: 0, score: 0, coin: 0, weight: 0, special: "PURIFY",       rare: false, tags: ["변환", "정화"] },
  { id: "magic_wand",   e: "🪄", n: "마법봉",     exp: 0, score: 0, coin: 0, weight: 0, special: "WANDWILD",     rare: true,  tags: ["변환", "조작", "희귀"] },
  { id: "mirror_sym",   e: "🪞", n: "거울",       exp: 0, score: 0, coin: 0, weight: 0, special: "MIRROR",       rare: false, tags: ["변환"] },
  //  위치
  { id: "target",       e: "🎯", n: "표적",       exp: 0, score: 0, coin: 0, weight: 0, special: "TARGET",       rare: false, tags: ["위치"] },
  { id: "jigsaw",       e: "🧩", n: "퍼즐",       exp: 0, score: 0, coin: 0, weight: 0, special: "PUZZLE5",      rare: false, tags: ["위치"] },
  //  시간
  { id: "alarm",        e: "⏰", n: "알람",       exp: 0, score: 0, coin: 0, weight: 0, special: "ALARM",        rare: false, tags: ["시간"] },
  { id: "hourglass",    e: "⏳", n: "모래시계",   exp: 0, score: 0, coin: 0, weight: 0, special: "HOURGLASS",    rare: true,  tags: ["시간", "희귀"] },
  //  상점
  { id: "receipt",      e: "🧾", n: "영수증",     exp: 0, score: 0, coin: 0, weight: 0, special: "RECEIPT",      rare: false, tags: ["상점"] },
  { id: "coupon",       e: "🎟", n: "쿠폰",       exp: 0, score: 0, coin: 0, weight: 0, special: "COUPON",       rare: true,  tags: ["상점", "희귀"] },
  { id: "cart",         e: "🛒", n: "장바구니",   exp: 0, score: 0, coin: 0, weight: 0, special: "CART",         rare: true,  tags: ["상점", "희귀"] },
  //  증강 (심화모드에 증강노드 부재 → 풀 제외·후속 Phase5. 데이터는 정의만 유지)
  { id: "highlighter",  e: "🖍", n: "형광펜",     exp: 0, score: 0, coin: 0, weight: 0, special: "AUGCHANCE",    rare: false, tags: ["증강"] },
  { id: "review_book",  e: "📚", n: "복습책",     exp: 0, score: 0, coin: 0, weight: 0, special: "AUGLEVEL",     rare: true,  tags: ["증강", "희귀"] },
  //  장치
  { id: "battery_sym",  e: "🔋", n: "배터리",     exp: 0, score: 0, coin: 0, weight: 0, special: "DEVCD",        rare: false, tags: ["장치"] },
  { id: "gear",         e: "⚙", n: "톱니바퀴",   exp: 0, score: 0, coin: 0, weight: 0, special: "GEAR",         rare: false, tags: ["장치"] },
  { id: "repair_kit",   e: "🧰", n: "정비키트",   exp: 0, score: 0, coin: 0, weight: 0, special: "KIT",          rare: true,  tags: ["장치", "희귀"] },
  //  세트
  { id: "set_piece",    e: "🧩", n: "세트조각",   exp: 0, score: 0, coin: 0, weight: 0, special: "SETFRAG",      rare: false, tags: ["세트"] },
  { id: "key_gold",     e: "🗝", n: "열쇠",       exp: 6, score: 0, coin: 0, weight: 0, special: "KEY",          rare: false, tags: ["세트", "열쇠"] },
  //  보스
  { id: "shield",       e: "🛡", n: "방패",       exp: 0, score: 0, coin: 0, weight: 0, special: "SHIELD",       rare: false, tags: ["보스"] },
  { id: "exam_paper",   e: "📋", n: "시험지",     exp: 0, score: 0, coin: 0, weight: 0, special: "EXEMPT",       rare: false, tags: ["보스"] },
  //  저주
  { id: "bloodrop",     e: "🩸", n: "피방울",     exp: 8, score: 0, coin: 0, weight: 0, special: "CURSE_BLOOD",  rare: false, tags: ["저주"] },
  { id: "black_candle_sym", e: "🕯", n: "검은초", exp: 0, score: 0, coin: 0, weight: 0, special: "CURSE_CANDLE", rare: false, tags: ["저주"] },
  { id: "unstable_bomb", e: "🧨", n: "불안정폭탄", exp: 0, score: 0, coin: 0, weight: 0, special: "CURSE_BOOM",   rare: false, tags: ["저주"] },
  { id: "curse_eye",    e: "🧿", n: "저주눈",     exp: 0, score: 0, coin: 0, weight: 0, special: "CURSE_EYE",    rare: false, tags: ["저주"] },
  //  전설
  { id: "lucky7",       e: "7️⃣", n: "럭키7",      exp: 0, score: 0, coin: 0, weight: 0, special: "LUCKY7",       rare: true,  tags: ["전설", "희귀"] },
  { id: "prism_sym",    e: "🌈", n: "프리즘",     exp: 0, score: 0, coin: 0, weight: 0, special: "PRISM_SYM",    rare: true,  tags: ["전설", "희귀"] },
  // ── Phase 4 V3P4: 소모형/일회용 신규 심볼 11종 (2026-07-10) ──
  //  use="instant": 릴 등장 즉시 효과 발동+덱에서 1개 제거(일회용).
  //  use="fuse": 덱 상주, 조건 충족 시 발동+제거(소모형). 조건 훅은 기존 훅 재사용.
  //  저주 2종: use 없음(저주=덱 상주 부담, 조건부 제거 없음 — ⛓족쇄는 fuse 예외).
  //  실버 소모형/일회용
  { id: "bandage",      e: "🩹", n: "붕대",       exp: 0, score: 0, coin: 0, weight: 0, special: "BANDAGE",      rare: false, tags: ["보호"] },
  { id: "knot",         e: "🪢", n: "매듭",       exp: 0, score: 0, coin: 0, weight: 0, special: "KNOT",         rare: false, tags: ["위치"] },
  { id: "safepin",      e: "🧷", n: "안전핀노트", exp: 0, score: 0, coin: 0, weight: 0, special: "SAFEPIN",      rare: false, tags: ["증강"] },
  { id: "energypack",   e: "🧃", n: "에너지팩",   exp: 0, score: 0, coin: 0, weight: 0, special: "ENERGYPACK",   rare: false, tags: ["배율"] },
  //  골드 소모형/일회용
  { id: "crystal",      e: "🔮", n: "수정구",     exp: 0, score: 0, coin: 0, weight: 0, special: "CRYSTAL",      rare: false, tags: ["보상"] },
  { id: "temp_wild",    e: "🧲", n: "임시와일드", exp: 0, score: 0, coin: 0, weight: 0, special: "TEMPWILD",     rare: false, tags: ["조작"] },
  //  프리즘 소모형/일회용
  { id: "fake_crown_sym", e: "👑", n: "가짜왕관", exp: 0, score: 0, coin: 0, weight: 0, special: "FAKECROWN",    rare: true,  tags: ["희귀", "왕관"] },
  { id: "fate_vortex",  e: "🌀", n: "운명의소용돌이", exp: 0, score: 0, coin: 0, weight: 0, special: "FATEVORTEX", rare: true, tags: ["희귀", "운"] },
  { id: "evo_core",     e: "🧬", n: "진화핵",     exp: 0, score: 0, coin: 0, weight: 0, special: "EVOCORE",      rare: true,  tags: ["희귀", "변환"] },
  //  저주 특수(use 없음 — 저주는 상주 부담)
  { id: "black_card",   e: "💳", n: "검은카드",   exp: 0, score: 0, coin: 0, weight: 0, special: "BLACKCARD",    rare: false, tags: ["저주", "상점"] },
  { id: "shackle",      e: "⛓", n: "족쇄",       exp: 0, score: 0, coin: 0, weight: 0, special: "SHACKLE",      rare: false, tags: ["저주"] },
  // ── §9.2 J3: 도파민 심볼 웨이브 13종 (2026-07-10) — weight:0(일반모드 무등장·주머니 전용) ──
  //  종 세트 5종 (bell 태그) — 피버 충전 + 리치 보상 + 승격
  { id: "small_bell",   e: "🔔", n: "작은종",     exp: 0, score: 0, coin: 0, weight: 0, special: "BELL_SMALL",   rare: false, tags: ["종"] },
  { id: "echo_bell",    e: "🔔", n: "울림종",     exp: 0, score: 0, coin: 0, weight: 0, special: "BELL_ECHO",    rare: false, tags: ["종"] },
  { id: "golden_bell",  e: "🔔", n: "황금종",     exp: 0, score: 0, coin: 0, weight: 0, special: "BELL_GOLD",    rare: false, tags: ["종"] },
  { id: "festival_bell",e: "🎊", n: "축제종",     exp: 0, score: 0, coin: 0, weight: 0, special: "BELL_FEST",    rare: false, tags: ["종"] },
  { id: "bell_ticket",  e: "🎟", n: "종소리티켓", exp: 0, score: 0, coin: 0, weight: 0, special: "BELL_TICKET",  rare: false, tags: ["종"] },
  //  리치 보정 3종
  { id: "reach_mark",   e: "🎯", n: "리치표식",   exp: 0, score: 0, coin: 0, weight: 0, special: "REACH_MARK",   rare: false, tags: ["리치"] },
  { id: "retry_reel",   e: "🔁", n: "재도전릴",   exp: 0, score: 0, coin: 0, weight: 0, special: "RETRY_REEL",   rare: false, tags: ["리치"] },
  { id: "jackpot_ticket", e: "🎟", n: "잭팟티켓", exp: 0, score: 0, coin: 0, weight: 0, special: "JACKPOT_TICKET", rare: false, tags: ["리치"] },
  //  태그 통합 2종
  { id: "slot_shard",   e: "🎰", n: "슬롯조각",   exp: 0, score: 0, coin: 0, weight: 0, special: "SLOT_SHARD",   rare: false, tags: ["잭팟"] },
  { id: "jackpot_wand", e: "🪄", n: "잭팟마법봉", exp: 0, score: 0, coin: 0, weight: 0, special: "JACKPOT_WAND", rare: false, tags: ["잭팟"] },
  //  증폭 3종
  { id: "cheer",        e: "📣", n: "환호",       exp: 0, score: 0, coin: 0, weight: 0, special: "CHEER",        rare: false, tags: ["잭팟"] },
  { id: "big_boom",     e: "💥", n: "대폭죽",     exp: 0, score: 0, coin: 0, weight: 0, special: "BIG_BOOM",     rare: false, tags: ["잭팟"] },
  { id: "jackpot_crown",e: "👑", n: "잭팟왕관",   exp: 0, score: 0, coin: 0, weight: 0, special: "JACKPOT_CROWN", rare: true,  tags: ["잭팟", "희귀"] },
];
export const SYM_BY_ID = Object.fromEntries(SYMS.map((s) => [s.id, s]));
export const EMPTY_SYM = { id: "empty", e: "▫", n: "빈칸", exp: 0, score: 0, coin: 0, weight: 0, special: "NONE", rare: false, tags: [] };
export const VALUE_IDS = new Set(["cherry", "star", "book", "gem", "crown"]);

// ── 보스 (5스테이지마다 순환) ─────────────────────────────────────────
export const BOSSES = [
  { id: "finals", e: "📝", n: "기말고사", desc: "스핀+1 · 막스핀 EXP ×2 · 첫스핀 -10%", bonusSpins: 1, quotaMul: 1.0, counter: ["막판형(복습·벼락치기)", "⏰최후"] },
  { id: "strict", e: "👨‍🏫", n: "꼰대교수", desc: "스핀+1 · 같은심볼 3개↑ 없으면 ×0.5", bonusSpins: 1, quotaMul: 1.0, counter: ["세트·콤보(자석·체리)", "📌고정·📑복사"] },
  { id: "luck", e: "🎲", n: "운빨심판관", desc: "스핀+1 · ⭐👑🌀 있으면 ×1.8 / 없으면 ×0.8", bonusSpins: 1, quotaMul: 1.0, counter: ["⭐👑🌀 등장↑", "🔮예언·🎲올인"] },
  { id: "grad", e: "🎓", n: "졸업심사", desc: "스핀+1 · 요구치 ×1.15 (빡센 관문)", bonusSpins: 1, quotaMul: 1.15, counter: ["EXP 총량(과부하·탐욕)", "🚑응급처치·아이템"] },
];

// ── 캐릭터 16 ─────────────────────────────────────────────────────────
export const CHARS = [
  { id: "novice", e: "🎒", n: "초보학생", d: "요구치↓·점수보정 ×0.9 (입문)", scoreMod: 0.9, startCoins: 0 },
  { id: "scholar", e: "📗", n: "장학생", d: "📘책+2·클리어코인+2", scoreMod: 1.0, startCoins: 0 },
  { id: "gambler", e: "🎲", n: "도박꾼", d: "점수보정 ×1.1·스테이지당 무료 재굴림 1회", scoreMod: 1.1, startCoins: 0 },
  { id: "farmer", e: "🍒", n: "체리농부", d: "🍒체리+1·희귀↓ (안정)", scoreMod: 0.95, startCoins: 0, unlockRuns: 2, unlockAch: "cherry100" },
  { id: "parttime", e: "🪙", n: "알바생", d: "시작코인+15·첫스핀-20%", scoreMod: 1.0, startCoins: 15, unlockRuns: 3 },
  { id: "jeweler", e: "💎", n: "보석상", d: "💎보석 점수+25·점수보정 ×1.1", scoreMod: 1.1, startCoins: 0, unlockScore: 2500, unlockAch: "jackpot1" },
  { id: "honor", e: "🎓", n: "수석졸업생", d: "실버 증강 1개로 시작", scoreMod: 1.0, startCoins: 0, unlockStage: 8, unlockAch: "boss5" },
  { id: "cultist", e: "💀", n: "해골숭배자", d: "☠해골 EXP+3·저주당 점수+8%", scoreMod: 1.15, startCoins: 0, unlockStage: 5, unlockAch: "boss1" },
  { id: "crowncol", e: "👑", n: "왕관수집가", d: "👑왕관 점수+30·등장↑", scoreMod: 1.15, startCoins: 0, unlockScore: 5000, unlockAch: "crown30" },
  { id: "minimalist", e: "🍃", n: "미니멀리스트", d: "유물 3개↓면 EXP+25%", scoreMod: 1.1, startCoins: 0, unlockStage: 7, unlockAch: "exact1" },
  { id: "lucky", e: "🍀", n: "행운아", d: "희귀심볼 등장+25% (한방)", scoreMod: 1.05, startCoins: 0, unlockRuns: 4 },
  { id: "highroller", e: "💠", n: "큰손", d: "💎보석 점수+25·시작코인+12", scoreMod: 1.1, startCoins: 12, unlockScore: 3500 },
  { id: "monk", e: "🧘", n: "수도승", d: "스핀-1·요구치-10% (속전속결)", scoreMod: 1.05, startCoins: 0, unlockStage: 6 },
  { id: "alchemist", e: "⚗️", n: "연금술사", d: "코인+25%·클리어코인+3", scoreMod: 1.0, startCoins: 0, unlockScore: 4000 },
  { id: "daredevil", e: "😈", n: "무모한도전", d: "모든 EXP+10%·요구치+20% · 남은≤2 EXP+35%·막스핀 +60% (막판형)", scoreMod: 1.2, startCoins: 0, unlockStage: 8, unlockAch: "score10k" },
  { id: "prodigy", e: "🌟", n: "천재", d: "모든 EXP+12%·점수보정 ×0.95", scoreMod: 0.95, startCoins: 0, unlockStage: 9, unlockAch: "stage10" },
  // ── 후반 캐릭터(플레이어 레벨 해금) — 강한 대신 룰이 이상하거나 리스크 ──
  { id: "regent", e: "🤴", n: "왕의 대리인", d: "👑왕관 점수+60·등장2배·기본 EXP-10% (왕관 올인)", scoreMod: 1.15, startCoins: 0, unlockLevel: 8 },
  { id: "bankrupt", e: "💸", n: "파산 졸업생", d: "코인0이면 EXP+50%·코인10↑이면 -20% (무상점 위험)", scoreMod: 1.15, startCoins: 0, unlockLevel: 12 },
  { id: "abyss_scholar", e: "🌌", n: "심연 장학생", d: "보스 스테이지 EXP+30%·일반 스테이지 -10% (보스 특화)", scoreMod: 1.15, startCoins: 0, unlockLevel: 16 },
];

// ── 슬롯머신 16 ───────────────────────────────────────────────────────
//  wmul: 심볼 가중 배수, wadd: 휴면심볼 주입
export const MACHINES = [
  { id: "basic", e: "🎰", n: "기본", d: "표준 확률 (입문)", scoreMod: 1.0, wmul: {}, wadd: {} },
  { id: "cherry", e: "🍒", n: "체리", d: "체리↑·왕관↓ (안정)", scoreMod: 0.95, wmul: { cherry: 1.5, crown: 0.6 }, wadd: {} },
  { id: "library", e: "📚", n: "도서관", d: "책↑·코인/보석↓ (경험치)", scoreMod: 1.0, wmul: { book: 1.5, coin: 0.6, gem: 0.6 }, wadd: {} },
  { id: "gem", e: "💎", n: "보석", d: "보석↑·체리/책↓ (점수)", scoreMod: 1.1, wmul: { gem: 1.7, book: 0.6, cherry: 0.6 }, wadd: {}, unlockScore: 1500, unlockAch: "jackpot1" },
  { id: "magnet", e: "🧲", n: "자석", d: "자석↑ (콤보)", scoreMod: 1.0, wmul: { magnet: 2.5 }, wadd: {}, unlockRuns: 4 },
  { id: "skull", e: "☠", n: "해골", d: "해골↑·고위험 (점수↑)", scoreMod: 1.1, wmul: { skull: 1.8 }, wadd: {}, unlockScore: 4000, unlockAch: "boss1" },
  { id: "crown", e: "👑", n: "왕관", d: "왕관↑·기본↓ (운빨 고점)", scoreMod: 1.2, wmul: { crown: 2.0, cherry: 0.7, book: 0.7 }, wadd: {}, unlockScore: 6000, unlockAch: "crown30" },
  { id: "flame", e: "🔥", n: "불꽃", d: "불꽃·해골↑ (배율형)", scoreMod: 1.1, wmul: { flame: 1.8, skull: 1.4 }, wadd: {}, unlockRuns: 6, unlockAch: "score10k" },
  { id: "bomb", e: "💣", n: "폭탄", d: "폭탄↑ (제거/계산)", scoreMod: 1.1, wmul: { bomb: 2.5 }, wadd: {}, unlockRuns: 8, unlockAch: "boss5" },
  { id: "star", e: "⭐", n: "별빛", d: "별↑·세트 잘맞음 (콤보)", scoreMod: 1.05, wmul: { star: 2.0, cherry: 0.8 }, wadd: {}, unlockScore: 3000 },
  { id: "clover", e: "🍀", n: "행운", d: "희귀·코인·불꽃↑ (행운)", scoreMod: 1.05, wmul: { crown: 1.3, coin: 1.4, flame: 1.3 }, wadd: {}, unlockRuns: 5 },
  { id: "casino", e: "🎲", n: "카지노", d: "🎲주사위 등장·고변동 (운빨)", scoreMod: 1.1, wmul: {}, wadd: { dice: 4.0 }, unlockScore: 4500,
    dDesc: "[심화] 🎲주사위 등장↑ 효과 미적용 (주머니에 주사위 없음) — 점수보정 ×1.1 유효" },
  { id: "garden", e: "🌱", n: "정원", d: "🌱씨앗 등장·성장형", scoreMod: 1.05, wmul: {}, wadd: { seed: 4.0 }, unlockRuns: 7,
    dDesc: "[심화] 🌱씨앗 등장↑ 효과 미적용 (주머니 씨앗 경로와 별개) — 점수보정 ×1.05 유효" },
  { id: "wildmac", e: "🌀", n: "와일드", d: "🌀와일드 등장 (세트 조작)", scoreMod: 1.1, wmul: {}, wadd: { wild: 3.0 }, unlockScore: 5000 },
  // ── 후반 슬롯머신(플레이어 레벨 해금) — 기본보다 특이·고위험 고점 ──
  { id: "nightmare", e: "😱", n: "악몽 슬롯", d: "☠해골 대량 등장·해골도 EXP+6 (고위험 고점)", scoreMod: 1.2, wmul: { skull: 2.2 }, wadd: { skull: 2.0 }, unlockLevel: 10 },
  { id: "throne", e: "👑", n: "빈 왕좌 슬롯", d: "👑왕관 대량·왕관 점수+40·기본 EXP-10% (왕관 특화)", scoreMod: 1.2, wmul: { crown: 2.5 }, wadd: { crown: 2.0 }, unlockLevel: 12 },
  { id: "broke", e: "💸", n: "파산 슬롯", d: "코인 안 나옴·🍒📘⭐ 값심볼 EXP+2 (무코인 고점)", scoreMod: 1.2, wmul: {}, wadd: {}, unlockLevel: 14 },
  { id: "vault", e: "🗝", n: "금고", d: "🗝열쇠 등장·코인↑", scoreMod: 1.1, wmul: { coin: 1.5 }, wadd: { key: 3.0 }, unlockRuns: 9,
    dDesc: "[심화] 🗝열쇠 등장↑ 효과 미적용 (주머니에 열쇠 없음) — 코인↑(wmul coin ×1.5) 유효" },
  { id: "rainbow", e: "🌈", n: "무지개", d: "⭐💎👑↑·🍒📘↓·고변동 (한방)", scoreMod: 1.2, wmul: { crown: 1.6, star: 1.4, gem: 1.3, cherry: 0.6, book: 0.6 }, wadd: {}, unlockScore: 8000 },
];

// ── 장치 12 ───────────────────────────────────────────────────────────
//  kind: PASSIVE|ARMED|MANIP|PEEK|INSTANT, cmd: 발동 액션명(웹 버튼), needsArg: 칸번호 필요
export const DEVICES = [
  { id: "dev_flame", e: "🔥", n: "불꽃엔진", cmd: null, d: "장착 시 모든 스핀 EXP +15%", kind: "PASSIVE", needsArg: false, rare: true },
  { id: "dev_seal", e: "🔒", n: "봉인장막", cmd: null, d: "☠해골 미등장·EXP +5%", kind: "PASSIVE", needsArg: false, rare: false },
  { id: "dev_safe", e: "🦺", n: "안전벨트", cmd: null, d: "최소 EXP 보장(폭망 방지)", kind: "PASSIVE", needsArg: false, rare: false },
  { id: "dev_overheat", e: "♨️", n: "과열코어", cmd: null, d: "EXP +18%·☠해골 +1 (고위험)", kind: "PASSIVE", needsArg: false, rare: true },
  { id: "dev_subreel", e: "➕", n: "보조릴", cmd: null, d: "항상 6칸 슬롯·최종 EXP -30%", kind: "PASSIVE", needsArg: false, rare: true },
  { id: "dev_coin", e: "🪙", n: "코인투입구", cmd: "투입", d: "코인5 → 다음 스핀 EXP +30%", kind: "ARMED", needsArg: false, rare: false },
  { id: "dev_reroll", e: "🔄", n: "재굴림기", cmd: "재굴림", d: "직전 스핀 다시 굴림 (점수 -10%)", kind: "MANIP", needsArg: false, rare: false },
  { id: "dev_pin", e: "📌", n: "고정핀", cmd: "고정", d: "N번 칸 유지·나머지 재굴림", kind: "MANIP", needsArg: true, rare: false },
  { id: "dev_copy", e: "📑", n: "복사기", cmd: "복사", d: "N번 칸을 옆칸에 복사", kind: "MANIP", needsArg: true, rare: true },
  { id: "dev_swap", e: "🔃", n: "교체기", cmd: "교체", d: "N번 칸을 최다 심볼로 교체", kind: "MANIP", needsArg: true, rare: true },
  { id: "dev_oracle", e: "🔮", n: "예언안경", cmd: "예언", d: "다음 스핀 미리보고 확정", kind: "PEEK", needsArg: false, rare: true },
  { id: "dev_bell", e: "🔔", n: "비상졸업벨", cmd: "비상", d: "부족 EXP ≤25면 즉시 클리어 (1회)", kind: "INSTANT", needsArg: false, rare: true },
  // ── 후반 장치(플레이어 레벨 도달 시 지급) — 강력하지만 리스크 동반 ──
  { id: "dev_reaper", e: "⚰️", n: "사신의 낫", cmd: null, d: "☠해골이 EXP +10 (해골 빌드 코어) · Lv.14 지급", kind: "PASSIVE", needsArg: false, rare: true },
  { id: "dev_abyss", e: "🌌", n: "심연 코어", cmd: null, d: "모든 스핀 EXP +35% · 점수 -10% · Lv.18 지급", kind: "PASSIVE", needsArg: false, rare: true },
  { id: "dev_reactor", e: "⚡", n: "과부하 리액터", cmd: null, d: "모든 스핀 EXP +40% · 요구 EXP +15% · Lv.22 지급", kind: "PASSIVE", needsArg: false, rare: true },
  // ── Phase 5: 심화모드(주머니 덱빌딩) 전용 심볼 장치 ──
  //  ★전부 심화모드(r.deepMode)에서만 효과(일반 장착 시 no-op·격리). 심화 업적으로 해금(ACH_DEVICE_REWARD).
  //   PASSIVE=_mods 에서 주머니 총량 판정으로 배수 주입 · MANIP=스핀 조작(cmd) · PEEK=표시형(엔진 무영향, ui 근사).
  { id: "dev_compress_gauge", e: "🗜️", n: "압축게이지", cmd: null, d: "[심화] 주머니 총량이 낮을수록 EXP↑ (30↓ +3% / 27↓ +8% / 24↓ +14%)", kind: "PASSIVE", needsArg: false, rare: true, deepOnly: true },
  { id: "dev_expand_scale", e: "⚖️", n: "확장저울", cmd: null, d: "[심화] 총량 36↑면 ☠해골 패널티 감소(-20%)", kind: "PASSIVE", needsArg: false, rare: true, deepOnly: true },
  { id: "dev_call_bell", e: "🔔", n: "연구실호출벨", cmd: null, d: "[심화] 다음 보스 클리어 후 정비소(SHOP) 등장 보장", kind: "PASSIVE", needsArg: false, rare: false, deepOnly: true },
  { id: "dev_legend_seal", e: "🔏", n: "전설봉인기", cmd: null, d: "[심화] 전설 심볼(럭키7·프리즘) 효과 변동 완화·안정 발동", kind: "PASSIVE", needsArg: false, rare: true, deepOnly: true },
  { id: "dev_baton", e: "🎯", n: "심볼지휘봉", cmd: "지휘", d: "[심화] 이번 스핀 칸 1개를 같은 등급 다른 심볼로 변경", kind: "MANIP", needsArg: true, rare: true, deepOnly: true },
  { id: "dev_purify_glove", e: "🧤", n: "정화장갑", cmd: "정화", d: "[심화] 이번 스핀 ☠해골 1개 → ▫빈칸", kind: "MANIP", needsArg: false, rare: false, deepOnly: true },
  { id: "dev_pouch_shuffler", e: "🔀", n: "주머니셔플러", cmd: "셔플", d: "[심화] 이번 스핀 다시 추출(확률표 재분배)", kind: "MANIP", needsArg: false, rare: true, deepOnly: true },
  { id: "dev_tag_scanner", e: "🔎", n: "태그스캐너", cmd: null, d: "[심화·표시형] 주머니 최강/최약 태그 표시", kind: "PEEK", needsArg: false, rare: false, deepOnly: true },
  { id: "dev_rare_detector", e: "📡", n: "희귀탐지기", cmd: null, d: "[심화·표시형] 다음 보상에 희귀 심볼 유무 표시", kind: "PEEK", needsArg: false, rare: false, deepOnly: true },
];

// ── 증강 (AUGMENT) ─ tier: SILVER|GOLD|PRISM ──────────────────────────
//  ★심화(deepMode) 관련성 태깅(2026-07-03, WEBSLOT_DEEP_AUG_SPEC Part1 — RELICS 동일 스키마):
//   deep: 1=심화 전부 유효 / 2=부분 유효(dDesc 필수) / 없음=심화 제외(등장률·강제등장·저주참조 — 신규 퍽은 opt-in)
//   dSym: 심볼직결 퍽의 참조 계열("cherry"|"book"|…|"crown"|"tag:학습") — 주머니 계열 보유수가
//         DEEP.REL_MIN(왕관은 REL_MIN_BY_SYM.crown=2) 미만이면 심화 오퍼 미노출(engine.isDeepCompat).
//   dDesc: 심화 전용 설명(등장률 문구 삭제·유효 파트만) — deep:2 전수 + fate_burst/perfect_shape.
//   ※ACHIEVEMENTS 의 deep:true(업적 심화표시)와 별개 스키마 — 혼용 금지. 소비자=engine.deepCompatPool/ui(dDesc).
export const AUGMENTS = [
  { id: "study", e: "📚", n: "기초학습", t: "SILVER", d: "모든 EXP +10%", deep: 1 },
  { id: "preview", e: "🔍", n: "예습", t: "SILVER", d: "첫 스핀 EXP +25%", deep: 1 },
  { id: "review", e: "📖", n: "복습", t: "SILVER", d: "마지막 스핀 EXP +25%", deep: 1 },
  { id: "diligence", e: "✍️", n: "꾸준함", t: "SILVER", d: "스핀마다 EXP +3", deep: 1 },
  { id: "cherry_up", e: "🍒", n: "체리강화", t: "SILVER", d: "🍒체리 EXP +2", deep: 1, dSym: "cherry" },
  { id: "book_up", e: "📘", n: "책강화", t: "SILVER", d: "📘책 EXP +2", deep: 1, dSym: "book" },
  { id: "star_up", e: "⭐", n: "별강화", t: "SILVER", d: "⭐별 EXP +2", deep: 1, dSym: "star" },
  { id: "gem_polish", e: "💎", n: "보석연마", t: "SILVER", d: "💎보석 점수 +10", deep: 1, dSym: "gem" },
  { id: "coin_luck", e: "🪙", n: "동전운", t: "SILVER", d: "코인 +30%", deep: 1 },
  { id: "set_sense", e: "🎯", n: "콤보감각", t: "SILVER", d: "세트 보너스 +30%", deep: 1 },
  { id: "discount", e: "🏷️", n: "할인감각", t: "SILVER", d: "상점 가격 -10%", deep: 1 },
  { id: "thrifty", e: "🛍️", n: "알뜰구매", t: "SILVER", d: "상점 아이템 가격 -20%", deep: 1 },
  { id: "item_bag", e: "🎒", n: "아이템가방", t: "SILVER", d: "아이템 보유칸 +1", deep: 1 },
  { id: "vip", e: "💳", n: "VIP 회원", t: "GOLD", d: "상점 상품칸 +1·새로고침 비용 -2", deep: 1 },
  //  refund=deep:1 확정(§0 실측): game.js useItem 의 keep 판정이 deepMode 무관 경로 — 심화 SHOP/EVENT 아이템에 실효.
  { id: "refund", e: "🧾", n: "환불 정책", t: "GOLD", d: "아이템 사용 시 30% 확률로 사라지지 않음", deep: 1 },
  { id: "lucky", e: "🍀", n: "행운부적", t: "SILVER", d: "희귀심볼 등장 +20%", deep: 1, dDesc: "희귀 심볼(🌀와일드·👑왕관) 주머니 드로우 확률 +20%" },
  { id: "study_tag", e: "🎓", n: "학구열", t: "SILVER", d: "학습태그(📘) 1개당 EXP +4", deep: 1, dSym: "tag:학습" },
  { id: "cherry_farm", e: "🍒", n: "체리농장", t: "GOLD", d: "🍒체리 EXP +4·등장↑", deep: 2, dSym: "cherry", dDesc: "🍒체리 EXP +4" },
  { id: "library", e: "📇", n: "도서관", t: "GOLD", d: "📘책 EXP +4·학습태그 +3", deep: 1, dSym: "book" },
  { id: "gem_invest", e: "💎", n: "보석투자", t: "GOLD", d: "💎보석 점수 +25", deep: 1, dSym: "gem" },
  { id: "skull_study", e: "☠", n: "해골학", t: "GOLD", d: "☠해골이 EXP +6", deep: 1, dSym: "skull" },
  { id: "center", e: "🎯", n: "집중", t: "GOLD", d: "가운데 칸 EXP 2배", deep: 1 },
  { id: "twins", e: "↔️", n: "양끝맞춤", t: "GOLD", d: "양끝 같은 심볼이면 EXP 2배", deep: 1 },
  { id: "chain", e: "🔗", n: "연쇄", t: "GOLD", d: "붙은 같은 심볼 쌍당 EXP +20", deep: 1 },
  { id: "crown_seek", e: "👑", n: "왕관추종", t: "GOLD", d: "👑왕관 등장 2배·점수 +30", deep: 2, dSym: "crown", dDesc: "👑왕관 점수 +30" },
  { id: "greed", e: "🤑", n: "탐욕", t: "GOLD", d: "모든 EXP +25%", deep: 1 },
  { id: "insurance", e: "❤️", n: "보험", t: "GOLD", d: "스테이지 스핀 +1", deep: 1 },
  { id: "overdrive", e: "⚡", n: "과부하", t: "PRISM", d: "모든 EXP +60%", deep: 1 },
  { id: "short_day", e: "🏃", n: "조기퇴근", t: "PRISM", d: "스핀 -2·모든 EXP +120%", deep: 1 },
  { id: "wild_world", e: "🌀", n: "와일드세계", t: "PRISM", d: "🌀와일드 등장(세트 합류)", deep: 1, dDesc: "🌀와일드 주머니 보유 시 드로우 확률 대폭↑(세트 합류)" },
  { id: "seed_garden", e: "🌱", n: "씨앗정원", t: "PRISM", d: "🌱씨앗 등장(다음스핀 성장)" },
  { id: "jackpot", e: "🎰", n: "잭팟기계", t: "PRISM", d: "👑왕관 대량등장·점수 +50", deep: 2, dSym: "crown", dDesc: "👑왕관 점수 +50" },
  { id: "all_in", e: "🎯", n: "몰아치기", t: "GOLD", d: "스핀 -1·모든 EXP +45%", deep: 1 },
  { id: "cram", e: "⏰", n: "벼락치기", t: "GOLD", d: "첫스핀 -40%·막스핀 +120%", deep: 1 },
  { id: "high_roller", e: "💠", n: "하이롤러", t: "GOLD", d: "💎보석 점수+30·EXP -8%", deep: 1 },
  { id: "all_or_nothing", e: "☠", n: "해골도박", t: "GOLD", d: "☠해골 EXP+10·EXP -10%", deep: 1 },
  { id: "focus_fire", e: "🔭", n: "정조준", t: "GOLD", d: "가운데 칸 EXP 2.5배", deep: 1 },
  { id: "symmetry", e: "↔️", n: "대칭미학", t: "GOLD", d: "양끝맞춤 EXP 2.2배·인접쌍+12", deep: 1 },
  { id: "crammer_tag", e: "🎓", n: "주입식", t: "GOLD", d: "학습태그당 EXP+7·책등장↑", deep: 2, dSym: "tag:학습", dDesc: "학습태그 1개당 EXP +7" },
  { id: "gamblers_dice", e: "🎲", n: "도박주사위", t: "PRISM", d: "🎲주사위 등장·EXP +15%", deep: 2, dDesc: "EXP +15% — 🎲주사위 등장↑은 심화 주머니 미적용" },
  { id: "key_master", e: "🗝", n: "열쇠장인", t: "PRISM", d: "🗝열쇠 등장·코인 +25%", deep: 2, dDesc: "코인 +25% — 🗝열쇠 등장↑은 심화 주머니 미적용" },
  { id: "glass_cannon", e: "⚡", n: "유리대포", t: "PRISM", d: "스핀-1·EXP +90%·점수+10%", deep: 1 },
  { id: "rich_richer", e: "🤑", n: "부익부", t: "PRISM", d: "코인+60%·클코인+3·EXP-5%", deep: 1 },
  { id: "endgame_rush", e: "🏁", n: "막판스퍼트", t: "PRISM", d: "막스핀 EXP 3배·첫스핀 -50%", deep: 1 },
  { id: "deep_read", e: "📕", n: "정독", t: "SILVER", d: "학습태그(📘) 1개당 EXP +3", deep: 1, dSym: "tag:학습" },
  { id: "morning", e: "🌅", n: "아침예습", t: "SILVER", d: "첫 스핀 EXP +30%", deep: 1 },
  { id: "evening", e: "🌆", n: "야간자습", t: "SILVER", d: "마지막 스핀 EXP +30%", deep: 1 },
  { id: "note_take", e: "📝", n: "필기", t: "SILVER", d: "스핀마다 EXP +5", deep: 1 },
  { id: "star_up2", e: "🌟", n: "별관측", t: "SILVER", d: "⭐별 EXP +3", deep: 1, dSym: "star" },
  { id: "magnet_up", e: "🧲", n: "자석강화", t: "SILVER", d: "🧲자석 EXP +3", deep: 1, dSym: "magnet" },
  { id: "gem_buff", e: "💠", n: "보석세공", t: "SILVER", d: "💎보석 점수 +12", deep: 1, dSym: "gem" },
  { id: "combo_note", e: "🎯", n: "콤보노트", t: "SILVER", d: "세트 보너스 +20%", deep: 1 },
  { id: "polymath", e: "🧠", n: "박식", t: "GOLD", d: "모든 EXP +20%", deep: 1 },
  { id: "necromancer", e: "💀", n: "강령술사", t: "GOLD", d: "☠해골이 EXP +8", deep: 1, dSym: "skull" },
  { id: "bullseye", e: "🎯", n: "정조준2", t: "GOLD", d: "가운데 칸 EXP 1.8배", deep: 1 },
  { id: "mirror", e: "🪞", n: "거울대칭", t: "GOLD", d: "양끝 같은 심볼이면 EXP 1.9배", deep: 1 },
  { id: "domino", e: "⛓️", n: "도미노", t: "GOLD", d: "붙은 같은 심볼 쌍당 EXP +16", deep: 1 },
  { id: "honor_student", e: "🎓", n: "수재", t: "GOLD", d: "학습태그(📘) 1개당 EXP +6", deep: 1, dSym: "tag:학습" },
  { id: "lapidary", e: "💍", n: "세공장인", t: "GOLD", d: "💎보석 점수 +28", deep: 1, dSym: "gem" },
  { id: "royal_decree", e: "📜", n: "왕명", t: "GOLD", d: "👑왕관 등장↑·점수 +20", deep: 2, dSym: "crown", dDesc: "👑왕관 점수 +20" },
  { id: "supernova", e: "💥", n: "초신성", t: "PRISM", d: "모든 EXP +70%", deep: 1 },
  { id: "joker", e: "🃏", n: "조커", t: "PRISM", d: "🌀와일드 대량 등장", deep: 1, dDesc: "🌀와일드 주머니 보유 시 드로우 확률 대폭↑" },
  { id: "great_harvest", e: "🌾", n: "대수확", t: "PRISM", d: "🌱씨앗 등장·🍒체리 EXP +3", deep: 2, dSym: "cherry", dDesc: "🍒체리 EXP +3" },
  { id: "mega_jackpot", e: "🎰", n: "대박기계", t: "PRISM", d: "👑왕관 대량등장·점수 +40", deep: 2, dSym: "crown", dDesc: "👑왕관 점수 +40" },
  { id: "time_warp", e: "⏳", n: "시간왜곡", t: "PRISM", d: "스핀 +1·모든 EXP +20%", deep: 1 },
  { id: "red_safetynet", e: "🥅", n: "붉은 안전망", t: "SILVER", d: "🍒체리 EXP +2", deep: 1, dSym: "cherry" },
  { id: "polish_work", e: "✨", n: "광택 작업", t: "GOLD", d: "💎보석 점수 +25", deep: 1, dSym: "gem" },
  { id: "greed_calc", e: "🤑", n: "탐욕의 계산", t: "GOLD", d: "모든 EXP +15%", deep: 1 },
  { id: "overheat_formula", e: "♨️", n: "과열 공식", t: "GOLD", d: "모든 EXP +14%", deep: 1 },
  // ── 신규 16종 (빌드 축 5테마, 2026-06-29) — SlotV2Engine.kt 캐논 포팅 ──
  //   초반성장(성장학)·운빨(운명학)·역전(시간학)·세트(계산학)·해골저주(저주학).
  //   조건부 효과는 engine.js buildMods(stage/스핀/스택/저주 ctx) + evaluate(셀 판정).
  // 초반성장 (성장학)
  { id: "early_prep", e: "🥚", n: "조기교육", t: "SILVER", d: "S3 이하 EXP +15% (S6+ 무효)", deep: 1 },
  { id: "growth_log", e: "📈", n: "성장일지", t: "SILVER", d: "클리어마다 다음스테이지 첫스핀 EXP +8% (최대5·실패리셋)", deep: 1 },
  { id: "early_adapt", e: "🌱", n: "빠른적응", t: "GOLD", d: "S1~5 EXP +12% (S6+ 무효)", deep: 1 },
  { id: "snowball", e: "❄️", n: "눈덩이", t: "PRISM", d: "남은스핀2+ 클리어시 다음 EXP +12% (최대4·보스후 -1)", deep: 1 },
  // 운빨 (운명학)
  { id: "fortune_check", e: "🔍", n: "운세확인", t: "SILVER", d: "스테이지 첫스핀 희귀 등장 +20%", deep: 1, dDesc: "첫 스핀 희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +20%" },
  { id: "luck_accum", e: "🎰", n: "불운적립", t: "GOLD", d: "희귀 미등장 스핀마다 불운+1 (3+면 다음 희귀↑)", deep: 1, dDesc: "불운 3+ 스핀 시 다음 스핀 희귀 심볼 드로우 확률 +30%" },
  { id: "fate_burst", e: "💫", n: "운명폭발", t: "PRISM", d: "희귀 2개+ 스핀 EXP +80%·점수 +50% (보스전 70%)", deep: 1, dDesc: "희귀(👑/🌀) 2개+ 스핀 EXP +80%·점수 +50% (보스전 70%) — 주머니에 👑/🌀 필요" },
  // 막판역전 (시간학)
  { id: "late_focus", e: "⏳", n: "후반집중", t: "SILVER", d: "남은스핀 2 이하 EXP +10%", deep: 1 },
  { id: "cliff_focus", e: "🧗", n: "벼랑끝집중", t: "GOLD", d: "EXP가 요구 60% 미만 & 마지막스핀 → 막스핀 EXP +80%", deep: 1 },
  // fate_bell: 스핀 소진 & 쿼터 부족 ≤15 실패 직전 런당 1회 자동 추가스핀 +1(일반/심화 공통).
  //  판정식은 Kotlin 캐논(SlotV2Service fate_bell 분기)과 동일. 구현 완료(2026-07-09).
  { id: "fate_bell", e: "🔔", n: "운명의종", t: "PRISM", d: "런 1회 부족 15 이하 실패직전 자동 추가스핀 +1" },
  // 세트콤보 (계산학)
  { id: "pair_match", e: "👯", n: "짝맞추기", t: "SILVER", d: "같은심볼 2세트면 세트 보너스 +20%", deep: 1 },
  { id: "puzzle_sense", e: "🧩", n: "퍼즐감각", t: "GOLD", d: "세트3+ EXP +25%·세트4+ 점수 +20%", deep: 1 },
  { id: "perfect_shape", e: "💠", n: "완벽한모양", t: "PRISM", d: "양끝 같고 가운데 같은계열 EXP +120% (와일드충족 70%)", deep: 1, dDesc: "양끝 같고 가운데 같은계열 EXP +120% (와일드충족 70%) — 🌀와일드 보유 시 유리" },
  // 해골저주 (저주학)
  { id: "skull_watch", e: "👁️", n: "해골관찰", t: "SILVER", d: "☠1개당 EXP +2·☠3+ 스핀 점수 -10%", deep: 1, dSym: "skull" },
  { id: "sacrifice", e: "🩸", n: "희생", t: "GOLD", d: "저주1개당 EXP +6%·클리어코인 -1", deep: 1, dDesc: "저주당 EXP +6%(심화 저주 경로 유효)" },
  { id: "black_diploma", e: "🎓", n: "검은졸업장", t: "PRISM", d: "저주5+ EXP +60%·점수 +30%·스핀 -1", deep: 1, dDesc: "저주5+ EXP +60%·점수 +30%·스핀 -1(심화 저주 경로 유효)" },
  // ── 후반(심연) 증강 — 플레이어 레벨 도달 후 PRISM 노드/상점에 등장 ──
  { id: "crown_burst", e: "👑", n: "왕관 폭발", t: "PRISM", d: "👑왕관 점수 +100·대량 등장", unlockLevel: 10, deep: 2, dSym: "crown", dDesc: "👑왕관 점수 +100" },
  { id: "curse_grad", e: "🎓", n: "저주 대학원", t: "PRISM", d: "저주 1개당 점수 +15%", unlockLevel: 12, deep: 1, dDesc: "저주당 점수 +15%(심화 저주 경로 유효)" },
  { id: "extreme_overload", e: "⚡", n: "극한 과부하", t: "PRISM", d: "모든 EXP +90% · 요구 EXP +30% (고위험)", unlockLevel: 15, deep: 1 },
  { id: "abyss_lore", e: "🌌", n: "심연 지식", t: "PRISM", d: "보스 스테이지 EXP +50% (보스 특화)", unlockLevel: 16, deep: 1 },
];

// ── 증강 패밀리(비슷한 효과 계열) ─────────────────────────────────────
//  같은 계열의 증강이 한 오퍼에 겹쳐 나오지 않도록 묶고, "약→강" 순서로 잠금 해제.
//  값 = [패밀리키, 랭크]. 각 패밀리는 단일 티어(같은 등급 내 escalation). 미등록 증강 = 고유(항상 등장).
//  게이팅 규칙(engine.pickPerksByTier): 랭크 R 증강은 "보유한 같은-패밀리 증강 수 == R-1" 일 때만 등장.
//   → 랭크1은 항상 후보, 랭크1을 골라야 랭크2가 등장, … (예: 모든EXP +14→+15→+20→+25). 오퍼 내 같은 패밀리 1개만.
export const AUG_FAMILY = {
  // 첫/마지막 스핀 EXP% (SILVER)
  preview: ["first_spin", 1], morning: ["first_spin", 2],
  review: ["last_spin", 1], evening: ["last_spin", 2],
  // 스핀마다 EXP (SILVER)
  diligence: ["per_spin", 1], note_take: ["per_spin", 2],
  // 심볼 강화 (SILVER)
  cherry_up: ["cherry_s", 1], red_safetynet: ["cherry_s", 1],
  star_up: ["star_s", 1], star_up2: ["star_s", 2],
  gem_polish: ["gem_s", 1], gem_buff: ["gem_s", 2],
  combo_note: ["setbonus_s", 1], set_sense: ["setbonus_s", 2],
  deep_read: ["studytag_s", 1], study_tag: ["studytag_s", 2],
  // 모든 EXP% (GOLD)
  overheat_formula: ["exp_g", 1], greed_calc: ["exp_g", 2], polymath: ["exp_g", 3], greed: ["exp_g", 4],
  // 심볼/콤보 강화 (GOLD)
  gem_invest: ["gem_g", 1], polish_work: ["gem_g", 1], lapidary: ["gem_g", 2],
  skull_study: ["skull_g", 1], necromancer: ["skull_g", 2],
  bullseye: ["center_g", 1], center: ["center_g", 2], focus_fire: ["center_g", 3],
  mirror: ["ends_g", 1], twins: ["ends_g", 2], symmetry: ["ends_g", 3],
  domino: ["chain_g", 1], chain: ["chain_g", 2],
  honor_student: ["studytag_g", 1], crammer_tag: ["studytag_g", 2],
  royal_decree: ["crown_g", 1], crown_seek: ["crown_g", 2],
  // 모든 EXP% / 특수 (PRISM)
  overdrive: ["exp_p", 1], supernova: ["exp_p", 2], extreme_overload: ["exp_p", 3],   // 신규 극한과부하 = 모든EXP% 계열
  wild_world: ["wild_p", 1], joker: ["wild_p", 2],
  seed_garden: ["seed_p", 1], great_harvest: ["seed_p", 2],
  mega_jackpot: ["jackpot_p", 1], jackpot: ["jackpot_p", 2], crown_burst: ["jackpot_p", 3],   // 신규 왕관폭발 = 왕관점수 프리즘 계열
  // 후반 저주 프리즘 계열 / 상점 할인 계열
  black_diploma: ["curse_p", 1], curse_grad: ["curse_p", 2],
  discount: ["disc_s", 1], thrifty: ["disc_s", 2],
};

// ── 테마 빌드 도감 (25종, 5카테고리) ──────────────────────────────────
//  SlotV2Engine.kt THEME_BUILDS 캐논 포팅. "플레이스타일 완성" 칭호형 도감.
//  완성판정 = engine.evalThemeBuilds(ctx) (게임 클리어/오버 시 game.js 가 호출) → profile.counters bld_<id>=1.
//  보상 = 도감 표기(로컬). 핵심기능 히든 없음 — DB/랭킹/봇 무관, localStorage 전용.
//   cat: 성장형|운명형|역전형|조합형|위험형
export const THEME_BUILDS = [
  // ── 성장형 ──
  { id: "bld_fast_start",    e: "🚀", n: "빠른입학",       cat: "성장형", cond: "남은스핀 2+ 클리어 3회 (한 런)" },
  { id: "bld_model_growth",  e: "📈", n: "모범성장",       cat: "성장형", cond: "S5 도달 & 증강/유물 3개+ 보유" },
  { id: "bld_cherry_sprout", e: "🍒", n: "체리새싹런",     cat: "성장형", cond: "체리계열 증강 2개+ & S7 도달" },
  { id: "bld_library_start", e: "📚", n: "도서관스타트",   cat: "성장형", cond: "책계열 증강 2개+ & S7 도달" },
  { id: "bld_foundation",    e: "🧱", n: "기초공사",       cat: "성장형", cond: "프리즘 증강 0개로 S10 도달" },
  // ── 운명형 ──
  { id: "bld_fate_hand",     e: "🤲", n: "운명의손",       cat: "운명형", cond: "기도 성공 2회 (한 런)" },
  { id: "bld_dice_grad",     e: "🎲", n: "주사위의졸업식", cat: "운명형", cond: "🎲카지노 머신으로 S10 도달" },
  { id: "bld_crown_caller",  e: "👑", n: "왕관을부르는자", cat: "운명형", cond: "한 런 👑왕관 10개+ 등장" },
  { id: "bld_prob_hacker",   e: "🎯", n: "확률조작자",     cat: "운명형", cond: "희귀심볼 5개+ 스핀으로 보스 클리어" },
  { id: "bld_jackpot_seer",  e: "🔮", n: "잭팟예언자",     cat: "운명형", cond: "🔮예언안경 사용 후 잭팟 발생" },
  // ── 역전형 ──
  { id: "bld_cliff_pass",    e: "🧗", n: "벼랑끝합격",     cat: "역전형", cond: "막스핀 클리어 3회 (한 런)" },
  { id: "bld_heartbreaker",  e: "💔", n: "심장파괴자",     cat: "역전형", cond: "통산 아슬아슬 클리어 5회" },
  { id: "bld_cram_grad",     e: "⏰", n: "벼락치기졸업",   cat: "역전형", cond: "막스핀배율 증강 3개+ 후 보스 클리어" },
  { id: "bld_miracle_cert",  e: "🔔", n: "기적의졸업장",   cat: "역전형", cond: "🔔비상졸업벨로 보스 클리어" },
  { id: "bld_last_candle",   e: "🕯️", n: "마지막촛불",     cat: "역전형", cond: "S10+ 에서 막스핀 클리어" },
  // ── 조합형 ──
  { id: "bld_magnet_grad",   e: "🧲", n: "끌어당기는졸업", cat: "조합형", cond: "🧲자석 머신으로 세트4+ 완성" },
  { id: "bld_wild_puzzle",   e: "🌀", n: "와일드퍼즐",     cat: "조합형", cond: "🌀와일드 포함 잭팟" },
  { id: "bld_pinned_fate",   e: "📌", n: "고정된운명",     cat: "조합형", cond: "📌고정핀 사용 후 스테이지 클리어" },
  { id: "bld_copy_answer",   e: "📑", n: "복사답안",       cat: "조합형", cond: "📑복사기로 세트4+ 완성" },
  { id: "bld_chain",         e: "🔗", n: "연쇄반응",       cat: "조합형", cond: "한 런 인접쌍 보너스 5회+" },
  // ── 위험형 ──
  { id: "bld_skull_intro",   e: "☠", n: "해골입문",       cat: "위험형", cond: "통산 ☠해골 100개+ 등장" },
  { id: "bld_black_grad",    e: "🖤", n: "검은졸업식",     cat: "위험형", cond: "저주 3개+ 보유로 보스 클리어" },
  { id: "bld_ossuary",       e: "💀", n: "납골당졸업",     cat: "위험형", cond: "☠해골 5개+ 나온 스핀으로 클리어" },
  { id: "bld_curse_vessel",  e: "🏺", n: "저주의그릇",     cat: "위험형", cond: "저주 7개+ 보유 & S10 도달" },
  { id: "bld_ominous_overheat", e: "♨️", n: "불길한과열",   cat: "위험형", cond: "♨️과열코어 + 저주 3개+ 로 S10 도달" },
];
export const THEME_BUILD_CATEGORIES = ["성장형", "운명형", "역전형", "조합형", "위험형"];
export const TBUILD_BY_ID = Object.fromEntries(THEME_BUILDS.map((b) => [b.id, b]));

// ── 유물 (RELIC) ─ price = 상점 코인가 ────────────────────────────────
export const RELICS = [
  { id: "old_book", e: "📘", n: "낡은교과서", t: "SILVER", d: "📘책 EXP +3", price: 12, deep: 1, dSym: "book" },
  { id: "cherry_candy", e: "🍬", n: "체리사탕", t: "SILVER", d: "🍒체리 EXP +2", price: 10, deep: 1, dSym: "cherry" },
  { id: "rusty_coin", e: "🪙", n: "녹슨동전", t: "SILVER", d: "코인 +20%", price: 12, deep: 1 },
  { id: "pencil", e: "✏️", n: "연필깎이", t: "SILVER", d: "첫 스핀 EXP +15%", price: 12, deep: 1 },
  { id: "coffee", e: "☕", n: "커피잔", t: "SILVER", d: "마지막 스핀 EXP +15%", price: 14, deep: 1 },
  { id: "magnifier", e: "🔎", n: "돋보기", t: "SILVER", d: "희귀심볼 등장 +15%", price: 16, deep: 1, dDesc: "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +15%" },
  { id: "star_sticker", e: "⭐", n: "별스티커", t: "SILVER", d: "⭐별 점수 +8", price: 12, deep: 1, dSym: "star" },
  { id: "black_candle", e: "🕯️", n: "검은촛불", t: "GOLD", d: "☠해골이 EXP +4", price: 18, deep: 1, dSym: "skull" },
  { id: "gem_cert", e: "📜", n: "보석감정서", t: "GOLD", d: "💎보석 점수 +15", price: 20, deep: 1, dSym: "gem" },
  { id: "clover", e: "🍀", n: "네잎클로버", t: "GOLD", d: "모든 EXP +8%", price: 16, deep: 1 },
  { id: "set_charm", e: "🎰", n: "세트부적", t: "GOLD", d: "세트 보너스 +25%", price: 18, deep: 1 },
  { id: "wide_lens", e: "🔭", n: "집중경", t: "GOLD", d: "가운데 칸 EXP +50%", price: 16, deep: 1 },
  { id: "eraser", e: "✏️", n: "지우개", t: "SILVER", d: "📘책 EXP +2", price: 10, deep: 1, dSym: "book" },
  { id: "ruler", e: "📏", n: "자", t: "SILVER", d: "첫 스핀 EXP +12%", price: 12, deep: 1 },
  { id: "desk_lamp", e: "🪔", n: "스탠드", t: "SILVER", d: "마지막 스핀 EXP +12%", price: 12, deep: 1 },
  { id: "cherry_jam", e: "🍓", n: "체리잼", t: "SILVER", d: "🍒체리 EXP +3", price: 12, deep: 1, dSym: "cherry" },
  { id: "bookmark", e: "🔖", n: "책갈피", t: "SILVER", d: "학습태그(📘) 1개당 EXP +3", price: 12, deep: 1, dSym: "tag:학습" },
  { id: "coin_pouch", e: "👛", n: "동전지갑", t: "SILVER", d: "코인 +20%", price: 12, deep: 1 },
  { id: "mini_scope", e: "🔬", n: "미니스코프", t: "SILVER", d: "희귀심볼 등장 +15%", price: 14, deep: 1, dDesc: "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +15%" },
  { id: "gem_dust", e: "✨", n: "보석가루", t: "SILVER", d: "💎보석 점수 +10", price: 12, deep: 1, dSym: "gem" },
  { id: "magnet_chip", e: "🧲", n: "자석칩", t: "SILVER", d: "🧲자석 EXP +2", price: 10, deep: 1, dSym: "magnet" },
  { id: "star_chart", e: "🌠", n: "별자리표", t: "SILVER", d: "⭐별 EXP +2", price: 12, deep: 1, dSym: "star" },
  { id: "paperclip", e: "📎", n: "클립", t: "SILVER", d: "세트 보너스 +15%", price: 12, deep: 1 },
  { id: "small_candle", e: "🕯️", n: "작은초", t: "SILVER", d: "☠해골이 EXP +3", price: 12, deep: 1, dSym: "skull" },
  { id: "thick_tome", e: "📕", n: "두꺼운책", t: "GOLD", d: "📘책 EXP +4", price: 18, deep: 1, dSym: "book" },
  { id: "crystal_ball", e: "🔮", n: "수정구", t: "GOLD", d: "희귀심볼 등장 +30%", price: 20, deep: 1, dDesc: "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +30%" },
  { id: "skull_idol", e: "💀", n: "해골우상", t: "GOLD", d: "☠해골이 EXP +6", price: 18, deep: 1, dSym: "skull" },
  { id: "gem_tiara", e: "💎", n: "보석티아라", t: "GOLD", d: "💎보석 점수 +20", price: 22, deep: 1, dSym: "gem" },
  { id: "focus_ring", e: "💍", n: "집중반지", t: "GOLD", d: "가운데 칸 EXP +60%", price: 18, deep: 1 },
  { id: "silver_mirror", e: "🪞", n: "은거울", t: "GOLD", d: "양끝 같은 심볼 EXP +70%", price: 18, deep: 1 },
  { id: "iron_chain", e: "⛓️", n: "쇠사슬", t: "GOLD", d: "붙은 같은 심볼 쌍당 EXP +14", price: 18, deep: 1 },
  { id: "diploma_relic", e: "🎓", n: "졸업장식", t: "GOLD", d: "학습태그(📘) 1개당 EXP +5", price: 18, deep: 1, dSym: "tag:학습" },
  { id: "four_clover", e: "🍀", n: "네잎클로버2", t: "GOLD", d: "모든 EXP +10%", price: 20, deep: 1 },
  { id: "combo_trophy", e: "🏆", n: "콤보트로피", t: "GOLD", d: "세트 보너스 +25%", price: 20, deep: 1 },
  { id: "crown_jewel", e: "👑", n: "왕관보석", t: "GOLD", d: "👑왕관 점수 +30", price: 22, deep: 1, dSym: "crown" },
  { id: "piggy_bank", e: "🐷", n: "돼지저금통", t: "GOLD", d: "코인 +40%·클리어코인 +2", price: 18, deep: 1 },
  { id: "spare_token", e: "🎟️", n: "여분토큰", t: "GOLD", d: "스테이지 스핀 +1", price: 30, deep: 1 },
  { id: "hourglass_r", e: "⏳", n: "모래시계", t: "GOLD", d: "첫·마지막 스핀 EXP +20%", price: 22, deep: 1 },
  { id: "battery", e: "🔋", n: "배터리", t: "GOLD", d: "스핀마다 EXP +6", price: 18, deep: 1 },
  { id: "charm_relic", e: "🧿", n: "부적", t: "GOLD", d: "모든 EXP +12%", price: 20, deep: 1 },
  { id: "cherry_press", e: "🧃", n: "체리 압축기", t: "SILVER", d: "🍒체리 EXP +2", price: 10, deep: 1, dSym: "cherry" },
  { id: "cherry_can", e: "🥫", n: "체리 통조림", t: "SILVER", d: "🍒체리 EXP +3", price: 12, deep: 1, dSym: "cherry" },
  { id: "auto_pen", e: "🖋️", n: "자동 필기 펜", t: "SILVER", d: "📘책 EXP +2", price: 10, deep: 1, dSym: "book" },
  { id: "library_card", e: "🪪", n: "도서관 카드", t: "GOLD", d: "📘책 EXP +3·학습태그당 EXP +3", price: 18, deep: 1, dSym: "book" },
  { id: "greed_goblet", e: "🏆", n: "탐욕의 잔", t: "GOLD", d: "모든 EXP +10%", price: 18, deep: 1 },
  { id: "ominous_skull", e: "💀", n: "불길한 해골 목걸이", t: "GOLD", d: "☠해골이 EXP +5", price: 18, deep: 1, dSym: "skull" },
  { id: "black_report", e: "📋", n: "검은 성적표", t: "GOLD", d: "☠해골이 EXP +4", price: 18, deep: 1, dSym: "skull" },
  { id: "bloody_coupon", e: "🩸", n: "피 묻은 쿠폰북", t: "GOLD", d: "☠해골이 EXP +4·코인 +20%", price: 18, deep: 1 },
  { id: "crown_stand", e: "🏛️", n: "왕관 받침대", t: "GOLD", d: "👑왕관 점수 +25", price: 20, deep: 1, dSym: "crown" },
  { id: "broken_crown", e: "👑", n: "깨진 왕관", t: "SILVER", d: "👑왕관 점수 +15", price: 16, deep: 1, dSym: "crown" },
  { id: "kings_ledger", e: "📜", n: "왕의 족보", t: "GOLD", d: "👑왕관 점수 +20·등장↑", price: 22, deep: 2, dSym: "crown", dDesc: "👑왕관 점수 +20" },
  { id: "flame_canister", e: "🛢️", n: "불꽃 저장통", t: "GOLD", d: "모든 EXP +8%", price: 16, deep: 1 },
  { id: "hot_handle", e: "🔥", n: "뜨거운 슬롯핸들", t: "GOLD", d: "모든 EXP +9%", price: 18, deep: 1 },
  { id: "fate_handle", e: "🎰", n: "운명의 손잡이", t: "GOLD", d: "희귀심볼 등장 +25%", price: 18, deep: 1, dDesc: "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +25%" },
  { id: "gamblers_eye", e: "👁️", n: "도박사의 눈", t: "GOLD", d: "희귀심볼 등장 +20%", price: 18, deep: 1, dDesc: "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +20%" },
  { id: "old_wallet", e: "👛", n: "낡은 지갑", t: "SILVER", d: "코인 +20%", price: 12, deep: 1 },
  { id: "crumpled_coupon", e: "🧾", n: "구겨진 쿠폰", t: "SILVER", d: "코인 +20%", price: 10, deep: 1 },
  { id: "cursed_wallet", e: "💰", n: "저주받은 지갑", t: "GOLD", d: "코인 +30%·☠해골 EXP +2", price: 18, deep: 1 },
  { id: "practice_pad", e: "📓", n: "연습장", t: "SILVER", d: "📘책 EXP +2", price: 10, deep: 1, dSym: "book" },
  { id: "calculator", e: "🧮", n: "작은 계산기", t: "SILVER", d: "💎보석 점수 +12", price: 12, deep: 1, dSym: "gem" },
  { id: "lucky_eraser", e: "🩹", n: "행운의 지우개", t: "SILVER", d: "희귀심볼 등장 +15%", price: 14, deep: 1, dDesc: "희귀 심볼(🌀와일드·👑왕관) 드로우 확률 +15%" },
  // ── 프리즘(PRISM) 유물 8종 (2026-06-30) — 보스클리어 유물노드 PRISM 풀 (하향폴백 해소) ──
  { id: "prism_diploma",  e: "🎖️", n: "명예졸업장",     t: "PRISM", d: "EXP +40%·점수 +20%", price: 28, deep: 1 },
  { id: "golden_ratio",   e: "📐",  n: "황금비율",       t: "PRISM", d: "세트3+ EXP +40%·세트4+ 점수 +40%", price: 30, deep: 1 },
  { id: "starlight_crown", e: "🌟", n: "별빛왕관",       t: "PRISM", d: "👑왕관 점수 +60·등장↑·코인 +30%", price: 30, deep: 2, dSym: "crown", dDesc: "👑왕관 점수 +60·코인 +30%" },
  { id: "endless_recess", e: "⏱️", n: "무한자습",       t: "PRISM", d: "스핀 +1·첫끝 스핀 EXP +35%", price: 36, deep: 1 },
  { id: "fortunes_wheel", e: "🎡",  n: "운명의수레바퀴", t: "PRISM", d: "희귀심볼 등장 +25%·희귀 2개+ 스핀 EXP +60%·점수 +30%", price: 34, deep: 2, dDesc: "희귀(👑/🌀) 2개+ 스핀 EXP +60%·점수 +30% — 주머니에 👑/🌀 필요" },
  { id: "set_resonator",  e: "🎼",  n: "공명세트",       t: "PRISM", d: "세트 보너스 +50%·2세트 추가 +30%", price: 28, deep: 1 },
  { id: "reapers_pact",   e: "⚰️", n: "사신의계약",     t: "PRISM", d: "☠해골 EXP +10·점수 +15%", price: 28, deep: 1, dSym: "skull" },
  { id: "phoenix_thesis", e: "🔥",  n: "불사조논문",     t: "PRISM", d: "EXP가 요구치 50% 미만이면 그 스핀 EXP ×2", price: 34, deep: 1 },
  // ── 후반(심연) 유물 — 플레이어 레벨 도달 후 보상/상점 풀에 등장(PRISM 노드) ──
  { id: "crown_monolith",   e: "👑", n: "왕관 모노리스",   t: "PRISM", d: "👑왕관 점수 +80·등장↑",              price: 28, unlockLevel: 10, deep: 2, dSym: "crown", dDesc: "👑왕관 점수 +80" },
  { id: "black_grad_photo", e: "🖤", n: "검은 졸업사진",   t: "PRISM", d: "저주 1개당 점수 +12%",                price: 26, unlockLevel: 12, deep: 1, dDesc: "저주당 점수 +12%(심화 저주 경로 유효)" },
  { id: "last_roll",        e: "📋", n: "최후의 출석부",   t: "PRISM", d: "막스핀 EXP ×2 · 첫스핀 -10%",         price: 30, unlockLevel: 15, deep: 1 },
  { id: "nameless_cup",     e: "🏆", n: "무명왕의 잔",     t: "PRISM", d: "점수 ×1.6 · 요구 EXP +25% (고점)",   price: 40, unlockLevel: 20, deep: 1 },
];

// ══════════════════════════════════════════════════════════════════════
//  심화모드(deepMode) 전용 — 심볼 증강 / 심볼 유물 (Phase 5)
//  ★일반모드 완전 격리: 아래 배열은 일반 AUGMENTS/RELICS 와 별개 네임스페이스(sa_/sp_/sr_ 접두).
//   효과 로직은 engine.buildMods 를 건드리지 않고, game.js _mods() 심화블록의 symPerkMods() 에서만
//   r.deepMode && 보유 심볼퍽 일 때 mods/run 필드로 주입한다(일반 경로 컴파일·실행 무관).
//  · 심볼증강 = 실버6 / 골드7 / 프리즘8 = 21종 (t: SILVER|GOLD|PRISM — 오퍼 티어 시스템 재사용).
//  · 심볼유물 = 15종. 유물엔 프리즘 없음(SILVER|GOLD) — offerPerks 프리즘 폴백(→GOLD) 관례 정합.
//  d = 표시문(소수 2자리). eff = symPerkMods 가 읽는 효과 스펙(연동 방식은 game.js 참조).
//   eff.hook 카테고리:
//    "repairMul"  = 정비 서비스 kind 별 가격 배수 (repairBuy 에서 곱). {kinds:[...], mul}
//    "repairRefund" = 교체(swap) 정비/보상 시 코인 환급 비율. {frac} — game.js repairBuy/보상서 처리
//    "swapCoin"   = 심볼 교체 시 코인 보상 +N. {coin}
//    "tagBuff"    = 태그 곱셈버프 가산(deepTagMul 과 합류). {tag:"MOST"|"SELECT"|"SOLO", pct}
//    "penaltyMul" = 압축 패널티 완화 배수. {mul} — 낮을수록 이득. [HIGH-3] 요구치 통할인이 아니라
//                   _deepPenalty 서 초과분에만 적용: mul' = 1+(base-1)×mul, 하한 1.0 클램프.
//    "skullPenaltyMul" = ☠해골 감점 배수(evaluate skull 페널티에 곱·심화 전용). {mul} — 확장계열 전용
//    "boundDelta" = 총량 상/하한 델타. {maxDelta, minDelta}
//    "rewardBonus"= 주머니 보상 후보 +N. {n}
//    "addBoost"   = 보상/정비 추가량 관련. {basic, rareChance, legendWeight}
//    "shopLab"    = 정비소/상점 등장↑(노드풀 가중). {weight}
//    "purifyCoin" = 해골 정화 시 코인 +N. {coin}
//    "purifyToBasic" = 해골 정화 결과를 랜덤 기본심볼로. {}
//    "emptyScore" = 빈칸 등장 시 점수 +N/개. {score} (evaluate 심화 빈칸블록)
//    "emptyExp"   = 빈칸 EXP +N/개. {exp}
//    "boundOverride" = 총량 상/하한 절대 오버라이드. {max, min}
//    "expBounds"  = 총량 기반 조건부 점수/패널티 (확장/단일 등). 세부는 game.js.
//    "score"      = 조건부 점수 보너스(근사). {kind, amount}
//   ※ "근사" 표기는 pouchDraw(mods 미개입 설계)와 충돌하는 항목(희귀등장↑ 등)을 정직히 명시.
// ══════════════════════════════════════════════════════════════════════
export const SYM_AUGMENTS = [
  // ── 실버 6 ──
  { id: "sa_tidy",           e: "🧹", n: "심볼정리",   t: "SILVER", d: "심볼 제거 비용 -10%",              eff: { hook: "repairMul", kinds: ["remove"], mul: 0.9 } },
  { id: "sa_basic_research", e: "🌱", n: "기본연구",   t: "SILVER", d: "오퍼 카드 +1장",                    dDesc: "[v3] POUCH 오퍼 특수 카드 1장 추가(extraCards +1)", eff: { hook: "addBoost", basic: 1 } },
  { id: "sa_tag_sense",      e: "🏷️", n: "태그감각",   t: "SILVER", d: "가장 많은 태그 효과 +5%",          eff: { hook: "tagBuff", tag: "MOST", pct: 0.05 } },
  { id: "sa_safe_compress",  e: "🛟", n: "안전압축",   t: "SILVER", d: "총량 28↓ 압축 패널티 감소",         eff: { hook: "penaltyMul", mul: 0.6, whenTotalLe: 28 } },
  { id: "sa_use_empty",      e: "▫", n: "빈칸활용",   t: "SILVER", d: "빈칸 등장 시 점수 +20/개",          eff: { hook: "emptyScore", score: 20 } },
  { id: "sa_purify_class",   e: "🕊️", n: "정화수업",   t: "SILVER", d: "해골 제거·정화 비용 -20%",          eff: { hook: "repairMul", kinds: ["purify"], mul: 0.8 } },
  // ── 골드 7 ──
  { id: "sa_compress",       e: "🗜️", n: "심볼압축",   t: "GOLD",   d: "총량 27↓ 압축 패널티 절반",          eff: { hook: "penaltyMul", mul: 0.5, whenTotalLe: 27 } },
  { id: "sa_tag_major",      e: "🎓", n: "태그전공",   t: "GOLD",   d: "가장 많은 태그 +20%·타 태그 -5%",     eff: { hook: "tagBuff", tag: "MAJOR", pct: 0.20, other: -0.05 } },
  { id: "sa_lab_regular",    e: "🔬", n: "연구실단골", t: "GOLD",   d: "정비 탭 가격 -30%",                  eff: { hook: "repairMul", kinds: ["*"], mul: 0.7 } },
  { id: "sa_rare_research",  e: "🔮", n: "희귀연구",   t: "GOLD",   d: "오퍼 골드 확률 +10%p",               dDesc: "[v3] POUCH 오퍼 GOLD 카드 확률 +10%p/레벨(goldBonus 누적)", eff: { hook: "addBoost", rareChance: 0.10 } },
  { id: "sa_swap_expert",    e: "🔁", n: "교체전문가", t: "GOLD",   d: "심볼 교체 시 코인 +3",               eff: { hook: "swapCoin", coin: 3 } },
  //  [HIGH-3②] 확장빌드 — 설명대로 ☠해골 패널티 완화(skullPenaltyMul). 이전 quota(penaltyMul) 오배선 수정.
  { id: "sa_expand_build",   e: "📦", n: "확장빌드",   t: "GOLD",   d: "총량 33↑ 해골 패널티 감소",           eff: { hook: "skullPenaltyMul", mul: 0.7, whenTotalGe: 33 } },
  { id: "sa_purify_expert",  e: "✨", n: "정화전문가", t: "GOLD",   d: "해골 정화 결과가 랜덤 기본심볼로",     eff: { hook: "purifyToBasic" } },
  // ── 프리즘 8 ──
  { id: "sp_deckslot",       e: "🎰", n: "덱빌딩슬롯", t: "PRISM",  d: "매 스테이지 후 정비 노드·요구 EXP +15%", eff: { hook: "shopLab", weight: 3, quotaMul: 1.15, alwaysRepair: true } },
  //  ★§1.5 V3P1: 총량 커플드 ×0.5 리스케일 — 압축전공 min 42→21·extraPenaltyLe 54→27 / 확장전공 max 96→48.
  { id: "sp_compress_major", e: "🗜️", n: "압축전공",   t: "PRISM",  d: "총량 최소 21까지·27↓ 요구 급증",      eff: { hook: "boundOverride", min: 21, extraPenaltyLe: 27, extraMul: 1.10 } },
  { id: "sp_expand_major",   e: "📦", n: "확장전공",   t: "PRISM",  d: "총량 최대 48까지 (확률 희석)",        eff: { hook: "boundOverride", max: 48 } },
  { id: "sp_solo_major",     e: "🎯", n: "단일전공",   t: "PRISM",  d: "태그 제한 60→90%·최다 태그 +30%·타 -30%", eff: { hook: "tagBuff", tag: "SOLO", pct: 0.30, other: -0.30, tagCap: 0.90 } },
  { id: "sp_rare_hunt",      e: "🏹", n: "희귀사냥",   t: "PRISM",  d: "랜덤팩 PRISM 확률 +10%p·희귀 add 시 저주 위험↑", dDesc: "[오퍼 v2] 랜덤팩 PRISM +10%p(rareChance 연동)·희귀 add 채택 시 불운↑", eff: { hook: "addBoost", rareChance: 0.6, curseChance: 0.3, approx: true } },
  { id: "sp_lab_addict",     e: "⚗️", n: "연구실중독", t: "PRISM",  d: "보상이 정비소(상점) 중심",            eff: { hook: "shopLab", weight: 5 } },
  { id: "sp_legend_collect", e: "👑", n: "전설수집",   t: "PRISM",  d: "지정슬롯 전설 비중 ↑·보스 요구 EXP +20%", dDesc: "[오퍼 v2] 지정 슬롯 PRISM 내 전설 가중·보스 요구 EXP +20%", eff: { hook: "addBoost", legendWeight: 1, bossQuotaMul: 1.20 } },
  { id: "sp_purified_world", e: "🕊️", n: "정화된세계", t: "PRISM",  d: "해골 정화 가능·저주↓·점수 보너스↓",    eff: { hook: "purifyToBasic", scoreMul: 0.9, curseChance: -1 } },
];

// ── 심볼 유물 15종 (SILVER|GOLD) — 프리즘 없음(offerPerks 프리즘 폴백→GOLD) ──
export const SYM_RELICS = [
  { id: "sr_sorter",           e: "🗂️", n: "심볼분류함",   t: "SILVER", d: "주머니 보상 선택지 +1",           eff: { hook: "rewardBonus", n: 1 } },
  { id: "sr_shredder",         e: "🪓", n: "낡은파쇄기",   t: "GOLD",   d: "스테이지마다 최저가치 심볼 -1",     eff: { hook: "autoShred", n: 1 } },
  { id: "sr_notebook",         e: "📓", n: "연구노트",     t: "SILVER", d: "심볼 업그레이드 비용 -20%",        eff: { hook: "repairMul", kinds: ["upgrade"], mul: 0.8 } },
  { id: "sr_compass",          e: "🧭", n: "태그나침반",   t: "GOLD",   d: "가장 많은 태그 효과 +8%",          eff: { hook: "tagBuff", tag: "MOST", pct: 0.08 } },
  { id: "sr_scale",            e: "⚖️", n: "균형저울",     t: "GOLD",   d: "태그 60% 근접 시 점수 +12% (근사)", eff: { hook: "score", kind: "balance", amount: 0.12, approx: true } },
  { id: "sr_brush",            e: "🖌️", n: "정화붓",       t: "SILVER", d: "해골 정화 시 코인 +3",             eff: { hook: "purifyCoin", coin: 3 } },
  { id: "sr_empty_manual",     e: "📄", n: "빈칸설명서",   t: "SILVER", d: "빈칸 EXP +1/개",                  eff: { hook: "emptyExp", exp: 1 } },
  { id: "sr_rare_case",        e: "🧰", n: "희귀표본상자", t: "GOLD",   d: "희귀 첫 추가 시 점수 +300",         eff: { hook: "rareFirstScore", score: 300 } },
  //  [MED-3] 전설봉인함 배선: ①주머니 전설(👑7️⃣🌈) 보유 시 전설 효과 안정 발동(legendStable, 프리즘=최선효과 고정)
  //   ②보스 요구 증가분(전설수집 +20% 등 bossQuotaMul>1) 25% 감쇄(_deepPenalty). d 도 실효과에 일치.
  { id: "sr_legend_seal",      e: "🔏", n: "전설봉인함",   t: "GOLD",   d: "전설 보유 시 효과 안정 발동·보스 요구 증가분 -25%", eff: { hook: "legendSeal" } },
  { id: "sr_copier",           e: "📑", n: "심볼복사판",   t: "GOLD",   d: "보스 클리어 시 최다 태그 심볼 +2",   eff: { hook: "bossCopy", n: 2 } },
  { id: "sr_lab_key",          e: "🗝", n: "연구실열쇠",   t: "SILVER", d: "정비소(상점) 등장↑",               eff: { hook: "shopLab", weight: 2 } },
  { id: "sr_compress_contract", e: "📜", n: "압축계약서",   t: "GOLD",   d: "총량 27↓ 점수 +20%",              eff: { hook: "compressScore", pct: 0.20, whenTotalLe: 27 } },
  //  [HIGH-3②] 확장가방 — 해골 완화분을 skullPenaltyMul 로 배선(quota 통할인 오배선 제거). ★§1.5 V3P1: 임계 72→36·상한델타 12→6(×0.5 리스케일).
  { id: "sr_big_bag",          e: "🎒", n: "확장가방",     t: "GOLD",   d: "총량 36↑ 해골 패널티↓·상한 +6",   eff: { hook: "boundDelta", maxDelta: 6, skullPenaltyMul: 0.8, whenTotalGe: 36 } },
  { id: "sr_swap_receipt",     e: "🧾", n: "교체영수증",   t: "SILVER", d: "심볼 교체 시 25% 코인 환급",        eff: { hook: "repairRefund", frac: 0.25 } },
  //  [MED-4] 낡은통계표 배선: 보유 시 주머니 시트에 🎲랜덤칸 재분배 반영 '실효 등장확률' 추가 표시(pouchView.effRatio).
  { id: "sr_stat_table",       e: "📊", n: "낡은통계표",   t: "SILVER", d: "주머니 실효 확률(랜덤칸 반영) 표시 (표시형)", eff: { hook: "display", statTable: true } },
];

export const SYM_AUG_BY_ID = Object.fromEntries(SYM_AUGMENTS.map((x) => [x.id, x]));
export const SYM_REL_BY_ID = Object.fromEntries(SYM_RELICS.map((x) => [x.id, x]));
// 심볼증강+유물 통합 조회(게임 표시/도감/발견 추적용). id 는 sa_/sp_/sr_ 접두로 전역 유일.
export const SYM_PERK_BY_ID = { ...SYM_AUG_BY_ID, ...SYM_REL_BY_ID };

// ── 심볼 증강 레벨업(Lv1~3) — 형광펜/복습책(심화 증강레벨업) 부활의 대상 ──────────────
//  ★심화모드엔 일반 증강이 없어(심볼증강만 보유) 기존 AUG_LEVELS(일반 증강 대상)로는 형광펜/복습책이 데드였다.
//   → 심볼증강 중 수치효과가 깔끔히 스케일되는 것만 레벨업 대상으로 지정. Lv2/Lv3 에서 eff 수치 가산.
//   symPerkMods(engine) 가 levels 를 받아 아래 델타를 적용(누적). 미등록 심볼증강은 레벨업 불가(고정).
//  값 = { 2: {필드:델타...}, 3: {...} } — eff 의 pct/other/score/exp/mul(감소)/rareChance/n 등을 additive/합성.
export const SYM_AUG_LEVELS = {
  sa_tag_sense:     { 2: { pct: 0.03 }, 3: { pct: 0.03 } },          // 태그 +5→+8→+11%
  sa_use_empty:     { 2: { score: 10 }, 3: { score: 10 } },          // 빈칸 +20→+30→+40점
  sa_basic_research:{ 2: { basic: 1 }, 3: { basic: 1 } },            // 기본추가 +1→+2→+3
  sa_tag_major:     { 2: { pct: 0.05 }, 3: { pct: 0.05 } },          // 최다태그 +20→+25→+30%
  sa_rare_research: { 2: { rareChance: 0.20 }, 3: { rareChance: 0.30 } }, // 골드확률 +10→+20→+30%p(max 누적)
  sa_compress:      { 2: { mulSub: 0.05 }, 3: { mulSub: 0.05 } },    // 패널티배수 0.5→0.45→0.40 (더 감소=이득)
  sa_expand_build:  { 2: { mulSub: 0.05 }, 3: { mulSub: 0.05 } },    // 패널티배수 0.7→0.65→0.60
  sp_solo_major:    { 2: { pct: 0.05 }, 3: { pct: 0.05 } },          // 단일 최다 +30→+35→+40%
};
export const isSymAugLevelable = (id) => !!SYM_AUG_LEVELS[id];

// ── 유물 패밀리(비슷한 효과 계열) ─────────────────────────────────────
//  증강 AUG_FAMILY 와 동일 규칙. 유물 패밀리키는 rel_ 접두(증강 패밀리와 별개 네임스페이스).
//  같은 계열 유물은 한 오퍼(유물 노드)에 1개만, "약→강" 순서로 잠금 해제. 미등록 유물 = 고유.
//  (상점 유물은 offerPerks 를 안 쓰므로 영향 없음 — 유물 보상 노드에만 적용.)
export const REL_FAMILY = {
  // SILVER
  eraser: ["rel_book_s", 1], auto_pen: ["rel_book_s", 1], practice_pad: ["rel_book_s", 1], old_book: ["rel_book_s", 2],
  cherry_candy: ["rel_cherry_s", 1], cherry_press: ["rel_cherry_s", 1], cherry_jam: ["rel_cherry_s", 2], cherry_can: ["rel_cherry_s", 2],
  rusty_coin: ["rel_coin_s", 1], coin_pouch: ["rel_coin_s", 1], old_wallet: ["rel_coin_s", 1], crumpled_coupon: ["rel_coin_s", 1],
  ruler: ["rel_first_s", 1], pencil: ["rel_first_s", 2],
  desk_lamp: ["rel_last_s", 1], coffee: ["rel_last_s", 2],
  magnifier: ["rel_rare_s", 1], mini_scope: ["rel_rare_s", 1], lucky_eraser: ["rel_rare_s", 1],
  gem_dust: ["rel_gem_s", 1], calculator: ["rel_gem_s", 2],
  // GOLD
  library_card: ["rel_book_g", 1], thick_tome: ["rel_book_g", 2],
  clover: ["rel_exp_g", 1], flame_canister: ["rel_exp_g", 1], hot_handle: ["rel_exp_g", 2], four_clover: ["rel_exp_g", 3], greed_goblet: ["rel_exp_g", 3], charm_relic: ["rel_exp_g", 4],
  set_charm: ["rel_setbonus_g", 1], combo_trophy: ["rel_setbonus_g", 1],
  wide_lens: ["rel_center_g", 1], focus_ring: ["rel_center_g", 2],
  black_candle: ["rel_skull_g", 1], black_report: ["rel_skull_g", 1], ominous_skull: ["rel_skull_g", 2], skull_idol: ["rel_skull_g", 3],
  gem_cert: ["rel_gem_g", 1], gem_tiara: ["rel_gem_g", 2],
  gamblers_eye: ["rel_rare_g", 1], fate_handle: ["rel_rare_g", 2], crystal_ball: ["rel_rare_g", 3],
  kings_ledger: ["rel_crown_g", 1], crown_stand: ["rel_crown_g", 2], crown_jewel: ["rel_crown_g", 3],
};

// 증강+유물 통합 패밀리 맵 — 엔진 오퍼 게이팅에서 사용(id 는 증강·유물 전역 유일).
export const PERK_FAMILY = { ...AUG_FAMILY, ...REL_FAMILY };

// ── 저주 (CURSE) ─ 패널티 전용(장점 없음) ─────────────────────────────
//  저주 자체는 순수 불이익. 이득을 보려면 별도 투자 필요(증강 희생/검은졸업장, 캐릭터 광신도).
//  deep:1 = 심화 CURSE/RISK 노드에서 배출 가능. dEff = 심화 전용 효과 오버라이드(게임플레이 재설계).
//  ★배치 A Step 1: 16종 전수 태깅.
//    - 14종 deep:1 직접 유효(비고: cursed_skulls/thorny_path/blackout 의 weightAdd.skull 은 bias 레이어로 심화에서 자동 유효).
//    - tunnel_vision deep:1: evaluate 가 심화 5칸에서도 endsMatchExpMul·firstSpinExpMul 을 그대로 소비 → 유효.
//    - hex_allornothing 재설계: 심화에서 세트 희소 → dEff={archetypeMul:0.5} 전공 배율 절반(game.js _mods 심화블록 소비).
export const CURSES = [
  { id: "hard_exam",         e: "📝", n: "어려운시험", t: "GOLD", d: "요구치 +10%",                  deep: 1 },
  { id: "cursed_skulls",     e: "☠",  n: "저주받은패", t: "GOLD", d: "☠해골 등장↑ · EXP -4",          deep: 1 },
  { id: "speed_test",        e: "⏱️", n: "속성평가",   t: "GOLD", d: "스핀 -1",                       deep: 1 },
  { id: "frugal_vow",        e: "🪙", n: "청빈서약",   t: "GOLD", d: "코인 -40%",                     deep: 1 },
  { id: "tunnel_vision",     e: "🎯", n: "외골수",     t: "GOLD", d: "양끝맞춤 EXP -50% · 첫스핀 EXP -15%", deep: 1 },
  { id: "late_bloomer",      e: "🌙", n: "늦깎이",     t: "GOLD", d: "첫스핀 EXP -50%",               deep: 1 },
  { id: "gem_obsession",     e: "💎", n: "보석집착",   t: "GOLD", d: "🍒체리·📘책 EXP -2",             deep: 1 },
  { id: "high_stakes",       e: "🎲", n: "한탕주의",   t: "GOLD", d: "요구치 +8%",                    deep: 1 },
  { id: "thorny_path",       e: "🌵", n: "가시밭길",   t: "GOLD", d: "☠해골 등장↑ · ☠해골 EXP -5",    deep: 1 },
  { id: "hex_allornothing",  e: "⚡", n: "일발역전",   t: "GOLD", d: "세트 EXP -50%", deep: 1,
    dEff: { archetypeMul: 0.5 }, dDesc: "[심화] 전공 보너스 절반" },
  { id: "sleep_debt",        e: "😴", n: "수면부족",   t: "GOLD", d: "스핀마다 EXP -5",               deep: 1 },
  { id: "diploma_pressure",  e: "🎓", n: "학위압박",   t: "GOLD", d: "요구치 +12%",                   deep: 1 },
  { id: "exam_week",         e: "📅", n: "시험기간",   t: "GOLD", d: "요구치 +12%",                   deep: 1 },
  { id: "blackout",          e: "🌑", n: "정전",       t: "GOLD", d: "☠해골 등장↑",                   deep: 1 },
  { id: "pop_quiz",          e: "❓", n: "쪽지시험",   t: "GOLD", d: "스핀 -1",                       deep: 1 },
  { id: "student_debt",      e: "💸", n: "학자금",     t: "GOLD", d: "코인 -50%",                     deep: 1 },
];

// ── 세트 (특정 perk 조합 보유 시 발동) ────────────────────────────────
export const SETS = [
  { id: "set_orchard", n: "체리 과수원", req: ["cherry_up", "cherry_farm"], d: "🍒체리 EXP+3·등장↑" },
  { id: "set_library", n: "도서관 회원증", req: ["book_up", "library", "study_tag"], d: "📘책 EXP+3·학습+3" },
  { id: "set_necro", n: "강령술", req: ["skull_study", "black_candle"], d: "☠해골 EXP+4" },
  { id: "set_appraiser", n: "감정사", req: ["gem_polish", "gem_invest", "gem_cert"], d: "💎보석 점수+20" },
  { id: "set_royal", n: "왕실 알현", req: ["crown_seek", "jackpot"], d: "👑왕관 점수+40·등장↑" },
  { id: "set_align", n: "정렬의 묘", req: ["center", "twins", "chain"], d: "인접쌍 EXP+10" },
  { id: "set_combo", n: "콤보 마스터", req: ["set_sense", "set_charm"], d: "세트 보너스+20%" },
  { id: "set_diurnal", n: "주야겸행", req: ["morning", "evening"], d: "첫·막 스핀 EXP+15%" },
  { id: "set_necro2", n: "사령술 비전", req: ["necromancer", "skull_idol"], d: "☠해골 EXP+5" },
  { id: "set_jewels", n: "보석 왕가", req: ["gem_buff", "lapidary", "gem_tiara"], d: "💎보석 점수+20" },
  { id: "set_combo2", n: "콤보 장인", req: ["combo_note", "combo_trophy"], d: "세트 보너스+20%" },
  { id: "set_royal2", n: "대관식", req: ["royal_decree", "crown_jewel"], d: "👑왕관 점수+30·등장↑" },
  { id: "set_cherry_net", n: "체리 안전망", req: ["cherry_up", "cherry_jam"], d: "🍒체리 EXP+2·점수+12", reqChar: "farmer" },
  { id: "set_red_harvest", n: "붉은 수확", req: ["cherry_farm", "great_harvest"], d: "🍒체리 EXP+3·등장↑", reqMachine: "cherry" },
  { id: "set_student", n: "모범생", req: ["study", "diligence", "note_take"], d: "스핀마다 EXP+4" },
  { id: "set_lib_bless", n: "도서관의 축복", req: ["book_up", "library", "thick_tome"], d: "📘책 EXP+4·학습+3", reqMachine: "library" },
  { id: "set_greed", n: "탐욕", req: ["greed", "rich_richer"], d: "모든 점수+12%·코인+10%" },
  { id: "set_glory_grad", n: "빛나는 졸업식", req: ["diploma_relic", "honor_student"], d: "학습태그당 EXP+4·막스핀+15%", reqChar: "honor" },
  { id: "set_skull_lab", n: "해골 연구", req: ["skull_study", "skull_idol"], d: "☠해골 EXP+6", reqChar: "cultist" },
  { id: "set_black_grad", n: "검은 졸업", req: ["necromancer", "black_candle", "skull_idol"], d: "☠해골 EXP+5·점수+12%", reqMachine: "skull" },
  { id: "set_curse_cycle", n: "저주 순환", req: ["set_charm"], d: "세트 보너스+30%", reqDevice: "dev_seal" },
  { id: "set_crown_rite", n: "왕관 의식", req: ["crown_seek", "crown_jewel"], d: "👑왕관 점수+40·등장↑", reqChar: "crowncol" },
  { id: "set_kings_order", n: "왕의 명령", req: ["royal_decree", "jackpot"], d: "👑왕관 점수+50·등장↑", reqMachine: "crown" },
  { id: "set_flame_lab", n: "불꽃 실험", req: ["all_or_nothing"], d: "🔥불꽃 EXP+5·점수+12%", reqMachine: "flame", reqDevice: "dev_flame" },
  { id: "set_last_ignite", n: "마지막 점화", req: ["review", "endgame_rush"], d: "막 스핀 EXP+25%·점수+10%" },
  { id: "set_mechanic", n: "정비공", req: ["set_sense"], d: "세트 보너스+25%", reqDevice: "dev_subreel" },
  { id: "set_battery", n: "배터리", req: ["battery", "diligence"], d: "스핀마다 EXP+6" },
  { id: "set_gambler", n: "도박사", req: ["high_stakes", "high_roller"], d: "희귀등장↑·💎보석 점수+25", reqChar: "gambler" },
  { id: "set_shop_reg", n: "상점 단골", req: ["coin_luck", "piggy_bank"], d: "코인+20%·클리어코인+3" },
  { id: "set_scholarship", n: "장학금", req: ["study_tag", "diploma_relic"], d: "학습태그당 EXP+4·클리어코인+2", reqChar: "scholar" },
  { id: "set_bomb_calc", n: "폭탄마", req: ["center", "focus_fire"], d: "가운데 칸 EXP+50%·점수+10%", reqMachine: "bomb" },
  { id: "set_perfect_calc", n: "완벽한 계산", req: ["center", "twins", "chain"], d: "인접쌍 EXP+14·가운데 +30%" },
  { id: "set_safe_grad", n: "안전 졸업", req: ["insurance", "clover"], d: "스핀마다 EXP+3·모든 점수+8%" },
];

// ── 아이템 ─ kind: NEXTSPIN|PHASE|INSTANT ─────────────────────────────
export const ITEMS = [
  { id: "energy_drink", e: "🥤", n: "에너지드링크", k: "NEXTSPIN", cost: 18, d: "다음 스핀 EXP 2배" },
  { id: "magnify", e: "🔎", n: "확대경", k: "NEXTSPIN", cost: 15, d: "다음 스핀 희귀심볼 4배" },
  { id: "loaded_dice", e: "🎲", n: "조작주사위", k: "NEXTSPIN", cost: 22, d: "다음 스핀 👑왕관 주입·점수 2배" },
  { id: "ward_charm", e: "🧿", n: "액막이부적", k: "NEXTSPIN", cost: 10, d: "다음 스핀 ☠해골 미등장" },
  { id: "espresso", e: "☕", n: "에스프레소", k: "PHASE", cost: 20, d: "이번 스테이지 스핀마다 EXP +15" },
  { id: "study_streak", e: "✍️", n: "집중모드", k: "PHASE", cost: 12, d: "이번 스테이지 스핀마다 EXP +6" },
  { id: "rare_lure", e: "🍀", n: "행운미끼", k: "PHASE", cost: 16, d: "이번 스테이지 희귀심볼 2배" },
  { id: "coin_magnet", e: "🧲", n: "코인자석", k: "PHASE", cost: 14, d: "이번 스테이지 코인 2배·클리어코인+8" },
  { id: "dbl_nothing", e: "🎰", n: "올인학습", k: "PHASE", cost: 12, d: "이번 스테이지 스핀마다 EXP+30·요구치+20%" },
  { id: "last_minute", e: "⏰", n: "막판스퍼트", k: "PHASE", cost: 18, d: "이번 스테이지 마지막 스핀 EXP 2.5배" },
  { id: "first_aid", e: "🩹", n: "응급처치", k: "INSTANT", cost: 30, d: "이번 스테이지 스핀 +1" },
  { id: "cram", e: "📚", n: "벼락치기", k: "INSTANT", cost: 14, d: "즉시 게이지 +요구치 15%" },
  { id: "answer_sheet", e: "📝", n: "족보", k: "INSTANT", cost: 40, d: "즉시 게이지 +요구치 50%" },
  { id: "grad_cert", e: "🎓", n: "졸업장", k: "INSTANT", cost: 90, d: "즉시 게이지 +요구치 100% (돌파)" },
  { id: "adrenaline", e: "💉", n: "아드레날린", k: "NEXTSPIN", cost: 30, d: "다음 스핀 EXP 3배" },
  { id: "rare_scope", e: "🔭", n: "정밀스코프", k: "NEXTSPIN", cost: 18, d: "다음 스핀 희귀심볼 3배" },
  { id: "crown_inject", e: "👑", n: "왕관주입", k: "NEXTSPIN", cost: 24, d: "다음 스핀 👑왕관 대량 주입" },
  { id: "wild_inject", e: "🌀", n: "와일드주입", k: "NEXTSPIN", cost: 22, d: "다음 스핀 🌀와일드 주입" },
  { id: "tutor", e: "👨‍🏫", n: "과외", k: "PHASE", cost: 18, d: "이번 스테이지 스핀마다 EXP +10" },
  { id: "fortune_incense", e: "🍀", n: "행운향", k: "PHASE", cost: 16, d: "이번 스테이지 희귀심볼 1.6배" },
  { id: "coin_press", e: "🪙", n: "주화압인", k: "PHASE", cost: 16, d: "이번 스테이지 코인 3배" },
  { id: "overtime", e: "⏰", n: "야근", k: "PHASE", cost: 16, d: "이번 스테이지 마지막 스핀 EXP 2배" },
  { id: "double_aid", e: "🚑", n: "특급처치", k: "INSTANT", cost: 55, d: "이번 스테이지 스핀 +2" },
  { id: "cheat_sheet", e: "📋", n: "커닝페이퍼", k: "INSTANT", cost: 24, d: "즉시 게이지 +요구치 30%" },
  { id: "honor_roll", e: "🏅", n: "우등생증", k: "INSTANT", cost: 60, d: "즉시 게이지 +요구치 70%" },
  { id: "cherry_juice", e: "🧃", n: "체리주스", k: "NEXTSPIN", cost: 5, d: "다음 스핀 🍒체리 확률↑" },
  { id: "bookmark2", e: "🔖", n: "책갈피", k: "NEXTSPIN", cost: 5, d: "다음 스핀 📘책 확률↑" },
  { id: "sparkle_dust", e: "✨", n: "반짝이가루", k: "NEXTSPIN", cost: 6, d: "다음 스핀 💎보석 확률↑" },
  { id: "gold_chalk", e: "🖍️", n: "황금분필", k: "NEXTSPIN", cost: 13, d: "이번 스핀 EXP ×2" },
  { id: "focus_candy", e: "🍬", n: "집중사탕", k: "NEXTSPIN", cost: 5, d: "다음 스핀 EXP +15%" },
  { id: "cram_note", e: "📓", n: "벼락치기노트", k: "PHASE", cost: 14, d: "이번 스테이지 마지막 스핀 EXP ×2" },
  { id: "rich_lure", e: "🍀", n: "큰행운미끼", k: "PHASE", cost: 16, d: "이번 스테이지 희귀심볼 3배" },
  { id: "prof_bribe", e: "🧧", n: "교수매수봉투", k: "PHASE", cost: 24, d: "이번 스테이지 요구치 -15%" },
  { id: "dev_battery", e: "🔋", n: "배터리부스트", k: "INSTANT", cost: 8, d: "다음 스핀 EXP +30%" },
  { id: "score_sticker", e: "💯", n: "점수스티커", k: "INSTANT", cost: 5, d: "사용 즉시 점수 +150" },
  { id: "old_coin", e: "🪙", n: "낡은동전", k: "INSTANT", cost: 4, d: "즉시 코인 +6" },
  { id: "small_snack", e: "🍪", n: "작은간식", k: "NEXTSPIN", cost: 4, d: "다음 스핀 ☠해골 미등장" },
  { id: "cherry_basket", e: "🧺", n: "체리바구니", k: "NEXTSPIN", cost: 7, d: "다음 스핀 🍒체리 대량 등장" },
  { id: "gem_loupe", e: "💎", n: "감정확대경", k: "NEXTSPIN", cost: 10, d: "다음 스핀 💎보석 확률↑·점수 2배" },
  { id: "sugar_powder", e: "🍚", n: "설탕가루", k: "PHASE", cost: 12, d: "이번 스테이지 🍒체리 1.6배·EXP+8" },
  { id: "cherry_cracker", e: "🧨", n: "체리폭죽", k: "PHASE", cost: 14, d: "이번 스테이지 🍒체리 2배·점수+20%" },
  { id: "book_copy", e: "📄", n: "족보사본", k: "PHASE", cost: 12, d: "이번 스테이지 📘책 2배·EXP+8" },
  { id: "allnight_note", e: "🌙", n: "밤샘노트", k: "PHASE", cost: 16, d: "이번 스테이지 📘책 1.8배·EXP+12" },
  { id: "summary_note", e: "🗒️", n: "요약노트", k: "PHASE", cost: 13, d: "이번 스테이지 스핀마다 EXP+9" },
  { id: "gem_pouch", e: "👝", n: "보석주머니", k: "PHASE", cost: 16, d: "이번 스테이지 💎보석 2배·점수+25%" },
  { id: "greed_lens", e: "🔍", n: "탐욕의렌즈", k: "PHASE", cost: 18, d: "이번 스테이지 점수 1.5배" },
  { id: "black_candle_i", e: "🕯️", n: "검은양초", k: "PHASE", cost: 14, d: "이번 스테이지 ☠해골 2배·EXP 1.3배" },
  { id: "curse_amp", e: "🩸", n: "저주증폭제", k: "PHASE", cost: 16, d: "이번 스테이지 ☠해골 1.6배·점수 1.4배" },
  { id: "gold_chalk_box", e: "✏️", n: "황금분필세트", k: "PHASE", cost: 20, d: "이번 스테이지 EXP 1.5배" },
  { id: "skull_shield", e: "🛡️", n: "해골방패", k: "PHASE", cost: 14, d: "이번 스테이지 ☠해골 미등장" },
  { id: "combo_mega", e: "📢", n: "콤보확성기", k: "PHASE", cost: 16, d: "이번 스테이지 마지막 스핀 EXP 2배·점수 1.2배" },
  { id: "cram_note_x2", e: "📔", n: "벼락치기노트+", k: "PHASE", cost: 16, d: "이번 스테이지 마지막 스핀 EXP 2.5배" },
  { id: "overload_potion", e: "🧪", n: "폭주물약", k: "PHASE", cost: 20, d: "이번 스테이지 EXP 2배·요구치+20%" },
  { id: "grad_copy", e: "🎓", n: "졸업장복사본", k: "INSTANT", cost: 70, d: "즉시 게이지 +요구치 80%·점수 -100" },
  { id: "score_calc", e: "🧮", n: "점수계산기", k: "INSTANT", cost: 22, d: "즉시 현재 점수 +30%" },
  { id: "mini_coupon", e: "🎟️", n: "미니쿠폰", k: "INSTANT", cost: 5, d: "즉시 코인 +9" },
  { id: "price_hack", e: "🏷️", n: "가격표조작기", k: "INSTANT", cost: 12, d: "즉시 코인 +18" },
  { id: "seal_tape", e: "🩹", n: "봉인테이프", k: "NEXTSPIN", cost: 9, d: "다음 스핀 ☠해골 미등장" },
  { id: "skull_sticker", e: "💯", n: "해골스티커", k: "NEXTSPIN", cost: 12, d: "다음 스핀 ☠해골 1개당 점수 +100" },
  { id: "eraser_old", e: "🧽", n: "낡은지우개", k: "NEXTSPIN", cost: 8, d: "다음 스핀 가장 낮은 칸 1개 제거" },
  { id: "eraser_fine", e: "🧼", n: "고급지우개", k: "NEXTSPIN", cost: 12, d: "다음 스핀 가장 낮은 칸 1개 제거(정밀)" },
  { id: "eraser_god", e: "✨", n: "신의지우개", k: "NEXTSPIN", cost: 20, d: "다음 스핀 낮은 칸 최대 2개 제거" },
  { id: "wild_temp", e: "🌀", n: "임시와일드", k: "NEXTSPIN", cost: 16, d: "다음 스핀 랜덤 1칸 → 🌀와일드" },
  { id: "fake_crown", e: "👑", n: "가짜왕관", k: "NEXTSPIN", cost: 24, d: "다음 스핀 가장 높은 칸 → 👑왕관" },
  { id: "grad_ring", e: "💍", n: "졸업반지", k: "INSTANT", cost: 50, d: "부족 EXP ≤20이면 즉시 클리어" },
  { id: "gold_grad_bell", e: "🔔", n: "황금졸업벨", k: "INSTANT", cost: 80, d: "부족 EXP ≤50이면 즉시 클리어" },
  { id: "insurance_cert", e: "📋", n: "보험증서", k: "INSTANT", cost: 45, d: "이번 스테이지 실패 시 1회 생존(스핀+2)" },
  { id: "debt_note", e: "🧾", n: "빚문서", k: "INSTANT", cost: 0, d: "코인 +40 / 이후 3스테이지 클리어보상 0" },
  { id: "retake_form", e: "📄", n: "재시험신청서", k: "INSTANT", cost: 28, d: "직전 스핀 전체 다시 굴림" },
  { id: "black_lottery", e: "🎫", n: "검은복권", k: "INSTANT", cost: 18, d: "50% 골드유물 / 50% 저주 1개" },
  { id: "devil_contract", e: "😈", n: "악마의계약서", k: "INSTANT", cost: 20, d: "유물 1개 + 저주 1개(코인+25)" },
  { id: "timeline_ticket", e: "🎟️", n: "세계선티켓", k: "INSTANT", cost: 26, d: "다음 스핀 2번 굴려 유리한 쪽 자동확정" },
  { id: "broken_prism", e: "🔮", n: "깨진프리즘", k: "INSTANT", cost: 22, d: "이번 스테이지 랜덤 프리즘 증강효과 1개" },
  // ── 증강 레벨업 상점 상품 ──
  { id: "study_note", e: "📓", n: "강화노트", k: "INSTANT", cost: 18, d: "보유 증강 중 가장 낮은 레벨 1개를 즉시 레벨업" },
  { id: "aug_catalyst", e: "🧪", n: "증강촉매", k: "INSTANT", cost: 12, d: "다음 증강 레벨업 발생 확률 +15%" },
  { id: "gold_marker", e: "📙", n: "골드형광펜", k: "INSTANT", cost: 30, d: "보유 골드 증강 1개를 즉시 레벨업" },
  { id: "prism_ink", e: "💧", n: "프리즘잉크", k: "INSTANT", cost: 35, d: "다음 증강 선택을 프리즘 등급으로 (런당 1회 구매)" },
  { id: "overcharge", e: "⚡", n: "과충전배터리", k: "INSTANT", cost: 26, d: "다음 스핀 EXP +50% · 점수 -200 (고위험)" },
];

// ── 업적 (기본 16) + 장치 보상 매핑 ──────────────────────────────────
export const ACHIEVEMENTS = [
  { id: "cherry100", e: "🍒", n: "체리 수확가", key: "cherryTotal", th: 100, d: "🍒체리 누적 100개" },
  { id: "cherry500", e: "🍒", n: "체리 중독", key: "cherryTotal", th: 500, d: "🍒체리 누적 500개" },
  { id: "crown10", e: "👑", n: "왕관 수집가", key: "crownTotal", th: 10, d: "👑왕관 누적 10개" },
  { id: "crown30", e: "👑", n: "대관식", key: "crownTotal", th: 30, d: "👑왕관 누적 30개" },
  { id: "jackpot1", e: "🎰", n: "첫 잭팟", key: "jackpots", th: 1, d: "5칸 잭팟 1회" },
  { id: "jackpot10", e: "🎰", n: "잭팟 헌터", key: "jackpots", th: 10, d: "5칸 잭팟 10회" },
  { id: "boss1", e: "📝", n: "중간고사 통과", key: "bossClears", th: 1, d: "보스 1회 클리어" },
  { id: "boss5", e: "🎓", n: "졸업반", key: "bossClears", th: 5, d: "보스 5회 클리어" },
  { id: "stage10", e: "🧗", n: "10층 등반", key: "bestStage", th: 10, d: "스테이지 10 도달" },
  { id: "stage15", e: "🏔️", n: "최종보스 도달", key: "bestStage", th: 15, d: "스테이지 15 도달" },
  { id: "lastclear5", e: "⏰", n: "벼락치기 천재", key: "lastSpinClears", th: 5, d: "마지막 스핀 클리어 5회" },
  { id: "exact1", e: "🎯", n: "완벽한 계산", key: "exactClears", th: 1, d: "요구 EXP 정확히 클리어" },
  { id: "prism5", e: "🌈", n: "규칙 파괴자", key: "prismPicks", th: 5, d: "프리즘 증강 5회 선택" },
  { id: "score10k", e: "💯", n: "만점왕", key: "bestScore", th: 10000, d: "최고 점수 10,000" },
  { id: "score50k", e: "🏆", n: "슬롯의 지배자", key: "bestScore", th: 50000, d: "최고 점수 50,000" },
  { id: "runs20", e: "🔁", n: "단골", key: "runs", th: 20, d: "20런 플레이" },
  // ── 후반 업적(레벨/졸업/심화 학기) ──
  { id: "grad1", e: "🎓", n: "졸업생", key: "graduations", th: 1, d: "스테이지 15 클리어(졸업) 1회" },
  { id: "lv20", e: "⭐", n: "베테랑", key: "playerLevel", th: 20, d: "플레이어 레벨 20 달성" },
  { id: "asc3", e: "📈", n: "심화 3 수료", key: "ascMax", th: 3, d: "심화 학기 3 졸업" },
  { id: "asc5", e: "🔥", n: "심화 5 석사", key: "ascMax", th: 5, d: "심화 학기 5 졸업" },
  { id: "lv40", e: "🌌", n: "고인물", key: "playerLevel", th: 40, d: "플레이어 레벨 40 달성" },
  // ── Phase 5: 심화모드(주머니 덱빌딩) 전용 업적 (deep:true) ──
  //  ★key 는 전부 deep* 신규 카운터(심화 런에서만 누적·_gameOver r.deepMode 게이팅) → 일반 업적/카운터 무영향(격리).
  //   달성 시 심볼 종류 해금(ACH_SYMBOL_UNLOCK) + 일부는 심볼 장치도 지급(ACH_DEVICE_REWARD 하단 병합).
  { id: "d_ach_start",         e: "🔬", n: "심볼연구 시작", key: "deepRuns",           th: 1,  d: "[심화] 심화모드 첫 플레이", deep: true },
  //  ★§1.5 V3P1: key 이름 deepCompress95 는 카운터 연속성·grandfather 위해 유지. 판정 임계 54→27(×0.5 리스케일, game.js 동기).
  { id: "d_ach_compress1",     e: "🗜️", n: "첫 압축",       key: "deepCompress95",     th: 1,  d: "[심화] 총량 27↓로 스테이지 클리어", deep: true },
  { id: "d_ach_risk_compress", e: "⚠️", n: "위험한 압축",   key: "deepCompress85Boss", th: 1,  d: "[심화] 총량 85↓로 보스 클리어", deep: true },
  { id: "d_ach_big_pouch",     e: "🎒", n: "대형 주머니",   key: "deepMaxTotal",       th: 36,d: "[심화] 총량 36↑ 달성", deep: true },
  //  [HIGH-1] 체리/보석 전공 판정은 태그가 아니라 심볼 계열 개수(체리=🍒+🍑, 보석=💎+💠) — 설명도 일치.
  { id: "d_ach_cherry_major",  e: "🍒", n: "체리 전공",     key: "deepCherry50Boss",   th: 1,  d: "[심화] 체리 계열(🍒+🍑) 비중 50%↑로 보스 클리어", deep: true },
  { id: "d_ach_curse_major",   e: "☠️", n: "저주 전공",     key: "deepSkull40Boss",    th: 1,  d: "[심화] 해골 40%↑로 보스 클리어", deep: true },
  { id: "d_ach_gem_major",     e: "💎", n: "보석 전공",     key: "deepGem50Score30k",  th: 1,  d: "[심화] 보석 계열(💎+💠) 비중 50%↑·점수 3만↑ 보스 클리어", deep: true },
  { id: "d_ach_crown",         e: "👑", n: "왕관 연구",     key: "deepCrown2Boss",     th: 1,  d: "[심화] 주머니 왕관 2개로 보스 클리어", deep: true },
  { id: "d_ach_balance",       e: "⚖️", n: "완벽한 균형",   key: "deepBalanceBoss",    th: 1,  d: "[심화] 모든 태그 20%↓ 균형으로 보스 클리어", deep: true },
  { id: "d_ach_purifier",      e: "🕊️", n: "정화자",        key: "deepSkull0Boss",     th: 1,  d: "[심화] 주머니 해골 0으로 보스 클리어", deep: true },
  //  [HIGH-2] 도달 가능 수치로 정정 — 희귀 9종 중 잠금 해제 전 실도달 최대 7종(⏳모래시계=이 업적 보상=자기참조,
  //   📚복습책=전설연구자 보상) → th 6. 전설은 카탈로그 3종(👑기본·7️⃣왕관연구·🌈보석전공[HIGH-1 수정으로 도달 가능]) → th 3.
  { id: "d_ach_rare10",        e: "🔮", n: "희귀 수집가",   key: "deepRaresSeen",      th: 6,  d: "[심화] 희귀 등급 심볼 6종 발견", deep: true },
  { id: "d_ach_legend5",       e: "🏆", n: "전설 연구자",   key: "deepLegendsSeen",    th: 3,  d: "[심화] 전설 등급 심볼 3종 발견", deep: true },
  { id: "d_ach_master",        e: "🎓", n: "심볼 마스터",   key: "deepBossClears",     th: 10, d: "[심화] 심화모드 보스 통산 10회 클리어", deep: true },
];
export const ACH_DEVICE_REWARD = {
  jackpot1: "dev_subreel", boss1: "dev_reroll", crown10: "dev_seal", cherry100: "dev_safe",
  exact1: "dev_pin", lastclear5: "dev_overheat", score10k: "dev_coin", stage10: "dev_oracle",
  prism5: "dev_copy", boss5: "dev_swap", runs20: "dev_bell", score50k: "dev_flame",
  // ── Phase 5: 심화 업적 → 심볼 장치 지급(전부 deepOnly 장치·일반 장착 시 no-op) ──
  d_ach_compress1: "dev_compress_gauge", d_ach_big_pouch: "dev_expand_scale",
  d_ach_risk_compress: "dev_baton", d_ach_purifier: "dev_purify_glove",
  d_ach_balance: "dev_tag_scanner", d_ach_rare10: "dev_rare_detector",
  d_ach_legend5: "dev_legend_seal", d_ach_master: "dev_pouch_shuffler",
  d_ach_crown: "dev_call_bell",
};

// ── Phase 5: 심화 업적 달성 → 심볼 종류 해금(POUCH_SYMBOLS 중 잠금 심볼 개방) ──
//  ★업적 id → 해금되는 심볼 id. _gameOver 업적 해금 루프에서 profile.symUnlocked 에 push.
//   해금 대상 = 처음엔 잠겨있는(DEFAULT_UNLOCKED_SYMS 미포함) 특수/희귀/전설 심볼.
//   ★부활 3종 중 highlighter/review_book 은 여기로 해금(정비키트 repair_kit 는 기본 개방=게임성 보장).
export const ACH_SYMBOL_UNLOCK = {
  d_ach_start:         "catalyst",          // 촉매(변환)
  d_ach_compress1:     "purifier",          // 정화(해골→빈칸)
  d_ach_risk_compress: "mirror_sym",        // 거울
  d_ach_big_pouch:     "target",            // 표적
  d_ach_cherry_major:  "jigsaw",            // 퍼즐
  d_ach_curse_major:   "black_candle_sym",  // 검은양초(저주 코어)
  d_ach_gem_major:     "prism_sym",         // 프리즘(전설)
  d_ach_crown:         "lucky7",            // 럭키7(전설)
  d_ach_balance:       "magic_wand",        // 마법봉(와일드)
  d_ach_purifier:      "highlighter",       // 형광펜(부활·증강 레벨업 확률↑)
  d_ach_rare10:        "hourglass",         // 모래시계(이월)
  d_ach_legend5:       "review_book",       // 복습책(부활·즉시 레벨업)
  d_ach_master:        "shield",            // 방패(보스 방어)
};

export const SCORE_TITLES = [
  { min: 100000, e: "🌈", n: "잭팟의 지배자" }, { min: 50000, e: "👑", n: "슬롯 마스터" },
  { min: 25000, e: "🔥", n: "하이롤러" }, { min: 12000, e: "🏅", n: "슬롯 숙련자" },
  { min: 6000, e: "💰", n: "단골 도박꾼" }, { min: 3000, e: "🎲", n: "도전자" },
  { min: 1000, e: "🎰", n: "슬롯 입문자" }, { min: 0, e: "🐣", n: "잭팟 새내기" },
];

// id → 카탈로그 항목(증강/유물/저주/세트/아이템/장치/캐릭/머신) 통합 조회
export const AUG_BY_ID = Object.fromEntries(AUGMENTS.map((x) => [x.id, x]));
export const REL_BY_ID = Object.fromEntries(RELICS.map((x) => [x.id, x]));
export const CUR_BY_ID = Object.fromEntries(CURSES.map((x) => [x.id, x]));
export const SET_BY_ID = Object.fromEntries(SETS.map((x) => [x.id, x]));
export const ITEM_BY_ID = Object.fromEntries(ITEMS.map((x) => [x.id, x]));
export const DEV_BY_ID = Object.fromEntries(DEVICES.map((x) => [x.id, x]));
export const CHAR_BY_ID = Object.fromEntries(CHARS.map((x) => [x.id, x]));
export const MAC_BY_ID = Object.fromEntries(MACHINES.map((x) => [x.id, x]));
export const PERK_BY_ID = { ...AUG_BY_ID, ...REL_BY_ID, ...CUR_BY_ID };  // perk 통합(증강+유물+저주)

// ══════════════════════════════════════════════════════════════════════
//  심화모드(deepMode) — 심볼 주머니(pouch) 덱빌딩 (Phase 1+2)
//  ★일반모드와 완전 격리. 위 SYMS/C/증강/유물/저주 등 기존 데이터는 무수정(추가만).
//  주머니 = { [symId]: count }. 5칸 추출은 count 비중대로(engine.pouchDraw). effect 는
//  기존 evaluate 재사용(추출만 주머니). 아래는 정의/상수/메타뿐(로직은 engine.js).
// ══════════════════════════════════════════════════════════════════════

// 상위계열 매핑(업그레이드 보상용): 하위 심볼 id → 상위 심볼 id. (계열 상위로 승격, 총량 유지)
export const POUCH_UPGRADE = {
  cherry: "cherry_ripe", book: "tome", gem: "gem_cut",
  coin: "coin_bag", skull: "skull_black", flame: "ember",
};

// MVP 기본 20종 — 주머니에 담길 수 있는 심볼 id 목록(표시/오퍼 후보 풀).
//  실심볼 18 = 기존 SYMS 활성/휴면 중 12(cherry/book/star/gem/coin/skull/flame/magnet/bomb/crown/wild)
//   + 상위 6(cherry_ripe/tome/gem_cut/coin_bag/skull_black/ember) → 총 17 실심볼 + 특수 2("empty" 빈칸, "random" 랜덤칸) = 19.
//   (열쇠/주사위/씨앗은 머신·머신전용 성장 심볼이라 Phase1 주머니 MVP 제외 — 후속 Phase 확장 여지.)
export const POUCH_SYMBOLS = [
  "cherry", "book", "star", "gem", "coin", "skull", "flame", "magnet", "bomb", "crown", "wild",
  "cherry_ripe", "tome", "gem_cut", "coin_bag", "skull_black", "ember",
  "empty", "random",
  // ── Phase 4 특수심볼 편입(보상/정비 풀 노출 = 기본 개방). ──
  //  ★Phase 5: highlighter/review_book/repair_kit 부활 — 심화모드에 증강 오퍼/레벨업 노드(SYMAUG/AUGLEVEL)가
  //   생겨 형광펜(레벨업 확률↑)·복습책(즉시 레벨업)이 실효과. 정비키트(KIT)는 능동장치 재사용/정비소 등장 근사로 부활.
  //  성장·변환·위치·시간
  "seed_basic", "sprout", "catalyst", "purifier", "magic_wand", "mirror_sym", "target", "jigsaw",
  "alarm", "hourglass",
  //  상점·장치(근사)·세트·보스
  "receipt", "coupon", "cart", "battery_sym", "gear", "set_piece", "key_gold", "shield", "exam_paper",
  //  증강(Phase5 부활)
  "highlighter", "review_book", "repair_kit",
  //  저주·전설
  "bloodrop", "black_candle_sym", "unstable_bomb", "curse_eye", "lucky7", "prism_sym",
  // ── Phase 4 V3P4: 신규 소모형/일회용 심볼 11종 (기본 개방 — 즉시 체감) ──
  //  실버: 붕대(instant)/매듭(instant)/안전핀노트(fuse)/에너지팩(instant)
  "bandage", "knot", "safepin", "energypack",
  //  골드: 수정구(fuse)/임시와일드(fuse)
  "crystal", "temp_wild",
  //  프리즘: 가짜왕관(instant)/운명의소용돌이(fuse)/진화핵(instant)
  "fake_crown_sym", "fate_vortex", "evo_core",
  //  저주 특수: 검은카드(fuse)/족쇄(상주)
  "black_card", "shackle",
  // ── §9.2 J3: 도파민 심볼 웨이브 13종 (기본 개방) ──
  //  종 세트 5: small_bell/echo_bell/golden_bell/festival_bell/bell_ticket
  "small_bell", "echo_bell", "golden_bell", "festival_bell", "bell_ticket",
  //  리치 보정 3: reach_mark/retry_reel/jackpot_ticket
  "reach_mark", "retry_reel", "jackpot_ticket",
  //  태그 통합 2: slot_shard/jackpot_wand
  "slot_shard", "jackpot_wand",
  //  증폭 3: cheer/big_boom/jackpot_crown
  "cheer", "big_boom", "jackpot_crown",
];

// ── Phase 5: 심볼 해금 시스템 — 처음부터 개방(기본 해금)되는 심볼 집합. ──
//  ★§1.3 V3P1: START_POUCH 9종(왕관 제외)으로 축소. crown은 START_POUCH에서 빠졌지만
//   보상/정비 풀에서 등장해야 하므로 기본 해금 유지(ACH_SYMBOL_UNLOCK 미포함 → 해금 없으면 영구 미노출).
//   나머지(ACH_SYMBOL_UNLOCK 값 13종)만 업적 해금.
//   ★repair_kit(정비키트)=기본 개방(게임성). curse_eye/bloodrop/unstable_bomb=기본(저주 빌드 접근성).
//   game.js 가 profile.symUnlocked 와 이 집합을 합쳐 "해금된 심볼만" 보상/정비 풀에 노출.
export const DEFAULT_UNLOCKED_SYMS = new Set([
  // §1.3 V3P1 START_POUCH 9종(왕관 제외) + 상위계열 6 + 왕관(보상풀 등장 필요)
  "cherry", "book", "star", "gem", "coin", "skull", "flame", "magnet", "bomb", "crown",
  "cherry_ripe", "tome", "gem_cut", "coin_bag", "skull_black", "ember",
  // 특수(구조상 필수)
  "empty", "random", "wild",
  // Phase4 기본 개방 유지(하위호환·게임 접근성) — 성장/상점/장치/세트/저주 계열
  "seed_basic", "sprout", "alarm",
  "receipt", "coupon", "cart", "battery_sym", "gear", "set_piece", "key_gold", "exam_paper",
  "bloodrop", "curse_eye", "unstable_bomb",
  // 부활 3종 중 정비키트는 기본 개방(게임성) — 형광펜/복습책은 업적 해금
  "repair_kit",
  // V3P4 신규 11종: 기본 개방(신규 콘텐츠 즉시 체감, 업적 해금은 2차 웨이브)
  "bandage", "knot", "safepin", "energypack",
  "crystal", "temp_wild",
  "fake_crown_sym", "fate_vortex", "evo_core",
  "black_card", "shackle",
  // §9.2 J3 신규 13종: 기본 개방(잭팟 태그 축 즉시 체감)
  "small_bell", "echo_bell", "golden_bell", "festival_bell", "bell_ticket",
  "reach_mark", "retry_reel", "jackpot_ticket",
  "slot_shard", "jackpot_wand",
  "cheer", "big_boom", "jackpot_crown",
]);

// 희귀도(기본|고급|희귀|전설|저주) — SYMS 에 안 넣고 별도 맵(기존 도감/데이터 무오염).
//  "random"(랜덤칸)=고급, "empty"(빈칸)=기본. 미등록 심볼은 engine 에서 "기본" 기본값.
export const POUCH_RARITY = {
  cherry: "기본", book: "기본", star: "기본", gem: "기본", coin: "기본", magnet: "기본",
  empty: "기본", random: "고급",
  cherry_ripe: "고급", tome: "고급", gem_cut: "고급", coin_bag: "고급", bomb: "고급",
  flame: "희귀", ember: "희귀", wild: "희귀",
  crown: "전설",
  skull: "저주", skull_black: "저주",
  // ── Phase 4 특수심볼 등급(SYMS 무오염·별도맵 유지). ★§1.5 V3P1: 상한은 RARITY_MAX(특수 티어 상한)로 이관. ──
  //  기본
  seed_basic: "기본", alarm: "기본", battery_sym: "기본",
  //  고급
  purifier: "고급", mirror_sym: "고급",
  sprout: "고급", receipt: "고급", set_piece: "고급", key_gold: "고급",
  shield: "고급", highlighter: "고급",
  //  희귀 (magic_wand/hourglass/coupon/cart/review_book/repair_kit + §5 MVP 골드5종 승급: catalyst/gear/exam_paper/jigsaw/target = 11종 · 상한8 초과분은 획득풀 진입만 제한, 실제 담김은 RARITY_MAX 검증)
  magic_wand: "희귀", hourglass: "희귀", coupon: "희귀", cart: "희귀",
  review_book: "희귀", repair_kit: "희귀",
  catalyst: "희귀", gear: "희귀", exam_paper: "희귀", jigsaw: "희귀", target: "희귀",
  //  전설 (crown 포함 총 개수 상한 2 — 종류 추가는 무해, 담기는 개수만 제한)
  lucky7: "전설", prism_sym: "전설",
  //  저주 (상한 ∞)
  bloodrop: "저주", black_candle_sym: "저주", unstable_bomb: "저주", curse_eye: "저주",
  // ── V3P4 신규 11종 ──
  //  실버 특수
  bandage: "고급", knot: "고급", safepin: "고급", energypack: "고급",
  //  골드 특수
  crystal: "희귀", temp_wild: "희귀",
  //  프리즘 특수
  fake_crown_sym: "전설", fate_vortex: "전설", evo_core: "전설",
  //  저주 특수
  black_card: "저주", shackle: "저주",
  // ── §9.2 J3 신규 13종 ──
  //  종 세트 5: 작은종/울림종=실버(고급), 황금종/축제종=골드(희귀), 종소리티켓=실버(fuse·고급)
  small_bell: "고급", echo_bell: "고급", bell_ticket: "고급",
  golden_bell: "희귀", festival_bell: "희귀",
  //  리치 보정 3: 리치표식=실버(고급), 재도전릴=골드(희귀), 잭팟티켓=실버(fuse·고급)
  reach_mark: "고급", jackpot_ticket: "고급",
  retry_reel: "희귀",
  //  태그 통합 2: 슬롯조각=실버(고급), 잭팟마법봉=골드(희귀)
  slot_shard: "고급",
  jackpot_wand: "희귀",
  //  증폭 3: 환호=실버(고급), 대폭죽=골드(희귀), 잭팟왕관=프리즘(전설)
  cheer: "고급",
  big_boom: "희귀",
  jackpot_crown: "전설",
};
export const POUCH_RARITY_ORDER = ["기본", "고급", "희귀", "전설", "저주"];

// ── §1.1 V3P1: 심볼 카테고리 맵 — base / special / harmful 전수 분류.
//  base(9): 시작 덱 뼈대 기본 심볼(cherry/book/star/gem/coin/flame/magnet/bomb/skull).
//  harmful: 해골/빈칸/저주 계열 — 자동 소멸 제외(Phase 3), 플레이어가 직접 정화.
//  special: 나머지 전부(상위계열·유틸·전설 등 — 보상/상점/이벤트로만 획득).
//  ★skull = 개념상 base(시작 덱 9종)이면서 harmful — 판정은 harmful 우선(자동소멸 제외)이므로
//   맵 값은 "harmful" 단일 기재(중복 키 금지). isAutoDecayTarget = cat==="base"(base && !harmful 등가).
export const POUCH_CAT = {
  // base — 시작 덱 뼈대 8종(+skull 은 harmful 로 분류·아래 참조)
  cherry: "base", book: "base", star: "base", gem: "base", coin: "base",
  flame: "base", magnet: "base", bomb: "base",
  // harmful — 해골/빈칸/저주 계열(skull: base 출신이지만 harmful 우선)
  skull: "harmful", skull_black: "harmful",
  empty: "harmful", random: "harmful",
  bloodrop: "harmful", black_candle_sym: "harmful",
  unstable_bomb: "harmful", curse_eye: "harmful",
  // special — 나머지 전부(상위계열/유틸/전설/특수)
  cherry_ripe: "special", tome: "special", gem_cut: "special", coin_bag: "special", ember: "special",
  crown: "special", wild: "special", lucky7: "special", prism_sym: "special",
  seed_basic: "special", sprout: "special", alarm: "special",
  catalyst: "special", purifier: "special", mirror_sym: "special", target: "special", jigsaw: "special",
  receipt: "special", coupon: "special", cart: "special", battery_sym: "special",
  gear: "special", set_piece: "special", key_gold: "special", shield: "special", exam_paper: "special",
  magic_wand: "special", hourglass: "special", highlighter: "special", review_book: "special", repair_kit: "special",
  // V3P4 신규 11종 — special(보상/상점/이벤트로만 획득) / harmful(저주 2종)
  bandage: "special", knot: "special", safepin: "special", energypack: "special",
  crystal: "special", temp_wild: "special",
  fake_crown_sym: "special", fate_vortex: "special", evo_core: "special",
  black_card: "harmful", shackle: "harmful",
  // §9.2 J3 신규 13종 — 전부 special(잭팟 태그 덱빌딩 축 심볼)
  small_bell: "special", echo_bell: "special", golden_bell: "special",
  festival_bell: "special", bell_ticket: "special",
  reach_mark: "special", retry_reel: "special", jackpot_ticket: "special",
  slot_shard: "special", jackpot_wand: "special",
  cheer: "special", big_boom: "special", jackpot_crown: "special",
};

// ── §1.2 V3P1: 심볼 티어 축 개정 — 특수 심볼이 SILVER/GOLD/PRISM 3티어를 온전히 사용하도록 한 단계 하향.
//  v3: 기본→SILVER(유지), 고급→SILVER(v2 GOLD에서 하향), 희귀→GOLD(v2 PRISM에서 하향), 전설→PRISM(유지), 저주→CURSE.
//  영향: 고급 심볼이 오퍼 지정슬롯(GOLD 이상 필수)에서 빠지고 랜덤팩 SILVER 풀에 합류.
//  분포(v3): SILVER(기본+고급) / GOLD(희귀) / PRISM(전설) / CURSE(저주).
export const TIER_BY_RARITY = {
  "기본":  "SILVER",
  "고급":  "SILVER",
  "희귀":  "GOLD",
  "전설":  "PRISM",
  "저주":  "CURSE",
};

// 심화모드 상수 — 기본 주머니 구성·총량 제한·비중 상한·압축 패널티.
export const DEEP = {
  // ★§1.3 V3P1: 시작 덱 재설계(총 30). 왕관·empty·random 시작 제외(왕관=전설 PRISM 특수로만 획득).
  //  각 기본 심볼 최소 1개 조건 충족. 기본 9종만으로 구성 = 모든 특수 추가는 곧 기본 교체(§2 교체 비용과 정합).
  //  ★§11 초반 밸런스 완화(2026-07-13, 레버②): skull 4→3 · cherry 5→6(총 30 유지, 사용자 원표에서 유일한 이탈).
  //   해골 비중 13.3%→10%(v2 수준 복원)·체리(주 EXP원) 16.7%→20%. 각 기본 심볼 ≥1 조건 그대로 유지.
  //  비중(§11 후): 체리.200 책.167 별.133 보석.133 코인.133 해골.100 불꽃.067 자석.033 폭탄.033
  START_POUCH: {
    cherry: 6, book: 5, star: 4, gem: 4, coin: 4,
    skull: 3, flame: 2, magnet: 1, bomb: 1,
  },
  // ★§1.4 V3P1: 덱 용량 상수 재정의. 시작=30/30 풀 상태.
  //  (v2 TOTAL_BASE 60/MIN 50/MAX 80 대체)
  //  ★TOTAL_BASE/TOTAL_MIN/TOTAL_MAX 하위호환 별칭은 제거됨(2026-07-13 감사수정 #5) — ui.js pouchView()
  //   필드(pv.totalMin/pv.totalMax, game.js pouchView() 가 repairBounds() 로 항상 채움)를 직사용하도록 정리,
  //   폴백 도달 불가 실증. 전 파일 DECK_* 단일 소스.
  DECK_BASE: 30, DECK_MIN: 20, DECK_MAX: 40,
  MIN_KINDS: 7,            // 최소 심볼 종류
  TAG_MAX_RATIO: 0.60,     // 같은 태그 최대 비중(개수/총량)
  CROWN_MAX: 2,            // 왕관 상한(복사·와일드 취급 불가)
  WILD_MAX: 4,             // 와일드 상한
  // ── 심화 관련성 필터(일반 증강/유물 deep/dSym 태깅) 게이트 — engine.isDeepCompat 가 소비 ──
  //  dSym 퍽은 주머니에 해당 계열(자신+상위, tag:X 는 태그 총수)이 REL_MIN 개 이상일 때만 심화 오퍼/상점/이벤트 노출.
  //  ★배치 I: 5→3. §1.5 V3P1: 스펙 리스케일 목록에 REL_MIN 미포함 → 3 유지(시작 체리5/책5·별4 등
  //   주요 계열은 임계 도달 가능, 불꽃2/자석1/폭탄1 계열 퍽은 심볼 보충 후 노출 — 의도된 게이트).
  REL_MIN: 3,
  //  심볼별 오버라이드: 왕관은 CROWN_MAX=2(전설 상한 2)라 5는 영구 미노출 데드게이트 → 2(왕관 2개 보유 =
  //  의도적 왕관 빌드, d_ach_crown "왕관 2개 보스클리어" 정합). ※dSym:"wild" 퍽은 현재 0종 — 신설 시 여기에 추가.
  REL_MIN_BY_SYM: { crown: 2 },
  // ── 배치 G: 계열 아키타입(전공) 발동 임계 — share(계열 비중=familyCount/총량) 기준. ──
  //  ★share 기반이라 배치 I 총량 다이어트(100→60)에도 튜닝 불필요(비중은 총량 무관). engine.pouchArchetype 가 소비.
  ARCH_T1: 0.25,   // tier1(1차 전공) — 25%↑
  ARCH_T2: 0.40,   // tier2(2차 전공, 강화 보너스) — 40%↑
  // ★§1.5 V3P1: RARITY_MAX → 특수 티어 상한으로 개정(기존 희귀도명 키 폐기).
  //  base 심볼 ∞ / harmful 심볼 ∞ / SILVER 특수 ≤10 / GOLD 특수 ≤6 / PRISM 특수 ≤2.
  //  CROWN_MAX 2 · WILD_MAX 4 별도 유지(4>2 이지만 WILD도 GOLD 특수 상한 6 내 → 무모순).
  //  pouchValidate 는 이 키를 cat+tier 기반으로 소비(engine.js §1.5 재구조화 참조).
  RARITY_MAX: { "SILVER_special": 10, "GOLD_special": 6, "PRISM_special": 2 },
  // ★§1.5 V3P1: 압축 패널티 리스케일(배치 I 48/51/54/57 → 22/24/26/28, 스펙 §1.5 표 그대로).
  //  28↓ +3% / 26↓ +6% / 24↓ +9% / 22↓ +15%. 배수 유지. 기본총량 30(DECK_BASE) 정합.
  COMPRESSION: [
    { le: 22, mul: 1.15 }, { le: 24, mul: 1.09 }, { le: 26, mul: 1.06 }, { le: 28, mul: 1.03 },
  ],
  // 보상 카드 개수(스테이지 클리어 후).
  REWARD_OPTIONS: 3,
  // ── 배치 H: 패키지 보상 등장 확률 — 매 POUCH 오퍼에서 이 확률로 3장 중 1장을 방향성 패키지 카드로 교체.
  //  ★가능한(사전검증 통과) 템플릿이 있을 때만 실제 교체(engine.offerSymbolRewards). 튜닝 노출.
  PACKAGE_CHANCE: 0.30,
  // ── 배치 D §6.1: 오퍼 v2 — 랜덤팩 슬롯2 분포 상수. (독립 2롤, 둘 다 SILVER → 1개 GOLD 승격.)
  //  RANDPACK_DIST: 각 롤의 티어 분포 [SILVER, GOLD, PRISM]. 합계=1.0.
  RANDPACK_DIST: [0.30, 0.50, 0.20],
  //  RANDPACK_COUNT: 랜덤팩에서 지급하는 심볼 수.
  RANDPACK_COUNT: 2,
  //  RANDPACK_PRISM_BONUS: 퍽 rareChance→랜덤팩 PRISM 추가 확률(+10%p씩 가산).
  RANDPACK_PRISM_BONUS: 0.10,
  //  DESIGNATED_UPGRADE_CHANCE: 지정 슬롯1 기본 10% 등급업(GOLD→PRISM).
  DESIGNATED_UPGRADE_CHANCE: 0.10,
  // ── Phase 3: 심화모드 상점 '심볼 정비' 서비스 카탈로그 ──
  //  ★심화모드 상점에서만 노출(game.js r.deepMode 게이팅). 일반모드 상점 무영향.
  //  각 서비스: { id, e, n, price, kind, desc }
  //   kind = 정비 종류(엔진 applyShopService 가 소비):
  //     "addBasic"|"addHigh"|"addRare" = 희귀도 등급별 심볼 추가(대상 심볼 선택)
  //     "remove"  = 선택 심볼 -N
  //     "swap"    = A N개 → B N개(대상 2개 선택)
  //     "upgrade" = 기본심볼 N개 → 상위계열 N개(대상 선택)
  //     "purify"  = 해골(skull) → 빈칸(empty) swap N (정화)
  //     "expand"  = 총량 상한 +N (run.deepTotalMaxDelta)
  //     "compress"= 총량 최소 -N + 요구경험치 +pct (run.deepTotalMinDelta/deepCompressExtra)
  //     "tagbuff" = 특정 태그 심볼 효과 +pct (run.deepTagBuff, evaluate 심화 전용 배수)
  //  n = 심볼 증감량 · pct = 배수증가분(요구/태그).  ※대상 선택 필요 서비스는 targetPick=true.
  REPAIR_SERVICES: [
    { id: "sv_add_basic", e: "🟢", n: "기본 심볼 추가", price: 8,  kind: "addBasic", n2: 5, rarity: "기본", targetPick: true,  desc: "선택한 기본 심볼 +5개" },
    { id: "sv_add_high",  e: "🔵", n: "고급 심볼 추가", price: 15, kind: "addHigh",  n2: 3, rarity: "고급", targetPick: true,  desc: "선택한 고급 심볼 +3개" },
    { id: "sv_add_rare",  e: "🟣", n: "희귀 심볼 추가", price: 30, kind: "addRare",  n2: 1, rarity: "희귀", targetPick: true,  desc: "선택한 희귀 심볼 +1개" },
    { id: "sv_remove",    e: "🗑️", n: "심볼 제거",     price: 12, kind: "remove",   n2: 3,               targetPick: true,  desc: "선택한 심볼 -3개" },
    { id: "sv_swap",      e: "🔁", n: "심볼 교체",     price: 18, kind: "swap",     n2: 3,               targetPick: true,  desc: "A 심볼 3개 → B 심볼 3개 (총량 유지)" },
    { id: "sv_upgrade",   e: "⬆️", n: "심볼 업그레이드", price: 25, kind: "upgrade",  n2: 5,               targetPick: true,  desc: "기본 심볼 5개 → 상위 등급 5개 (총량 유지)" },
    { id: "sv_purify",    e: "🕊️", n: "해골 정화",     price: 20, kind: "purify",   n2: 3,               targetPick: false, desc: "☠해골 -3, ▫빈칸 +3 (총량 유지)" },
    { id: "sv_expand",    e: "📦", n: "덱 확장",       price: 15, kind: "expand",       n2: 5,            targetPick: false, desc: "총량 상한 +5" },
    { id: "sv_compress",  e: "🗜️", n: "덱 압축",       price: 25, kind: "compress",     n2: 5, pct: 0.05, targetPick: false, desc: "총량 최소치 -5, 요구 EXP +5%" },
    { id: "sv_tagbuff",   e: "🏷️", n: "태그 강화",     price: 22, kind: "tagbuff",      pct: 0.10,        targetPick: true,  desc: "선택한 태그 심볼 효과 +10%" },
    // ★배치 A Step 5: 심화 전용 저주 정화 서비스(저주 보유 시만 노출·게임 로직에서 cursesHeld 조건 체크).
    { id: "curse_cleanse", e: "🕊️", n: "저주 정화",    price: 35, kind: "curseCleanse", n2: 1,            targetPick: false, desc: "현재 저주 1개 제거 (가장 최근 획득)" },
  ],
  // 희귀도 등급별 추가 서비스가 대상으로 허용하는 심볼 풀(추가 서비스의 targetPick 후보).
  //  POUCH_SYMBOLS 중 해당 희귀도만 노출(remove/swap/upgrade 는 별도 규칙 — game.js 에서 산출).

  // ── §9.1 J2: 피버 게이지 상수 ──────────────────────────────────────
  //  FEVER_MAX: 게이지 만충 임계(100 도달 시 피버 진입).
  //  충전량: 콤보+FEVER_COMBO(15)/리치+FEVER_REACH(25)/잭팟+FEVER_JACKPOT(50).
  //  (J3 폭죽류 심볼 충전+15 은 feverDelta 확장 포인트로 배선 — 이 상수와 동일 경로 사용.)
  //  FEVER_SPINS: 피버 지속 스핀 수(3). 100 도달 시 feverSpins=3, 게이지 리셋.
  //  피버 효과: EXP×FEVER_EXP_MUL(1.30)·점수×FEVER_SCORE_MUL(1.50).
  //  피버 중 리치 보정 확률 +FEVER_REACH_FIX(0.15) — mods.feverReachFix 노출(J3 소비 확장 포인트).
  //  피버잭팟: 피버 중 태그잭팟 = 점수×FEVER_JACKPOT_SCORE_MUL(2.00) 추가 + 프리즘 보장 1회 추가.
  FEVER_MAX: 100,
  FEVER_COMBO: 15,
  FEVER_REACH: 25,
  FEVER_JACKPOT: 50,
  FEVER_SPINS: 3,
  FEVER_EXP_MUL: 1.30,
  FEVER_SCORE_MUL: 1.50,
  FEVER_REACH_FIX: 0.15,
  FEVER_JACKPOT_SCORE_MUL: 2.00,
  // §9.2 J3: 축제종(BELL_FEST) — 피버 중(feverSpins>0) 이번 스핀 잭팟 태그 발동이 bell 이면
  //  해당 단계의 종 유래 EXP/점수 가산분 + feverDelta 에 적용하는 배율.
  BELL_FEST_MUL: 1.5,

  // ── §11 초반 밸런스 완화 (2026-07-13 사용자 피드백 "초반이 너무 힘들다") — 전부 심화 전용·후반 불변 ──
  //  진단: 시작 덱 30 전환으로 해골 비중↑·체리(주 EXP원) 비중↓ + 보상이 직접 파워업→유틸 특수로 바뀌며
  //  초반 EXP 수급 급락(개장 하네스 실측: S1 클리어 51.0%·S3 도달 17.3%·avgStage 1.753, 목표 미달).
  //  레버①: stage별 요구경험치 완화 램프 — game.js _deepPenalty() 에서 곱셈(전부 곱산이라 다른 심화 quota
  //   배율(압축 페널티·저주 quotaMul 등)과 순서 무관). 5+ 는 무배율(1.0, 키 없으면 ?? 1.0 폴백).
  //  ★2026-07-13 수렴 1회차: S1 86.7%/S3 56.3%(목표 90%/65% 미달, avgStage 2.790 정상) → EARLY_QUOTA[1] 0.80→0.75 1회 추가 완화(스펙 §11 수렴 프로토콜).
  EARLY_QUOTA: { 1: 0.75, 2: 0.85, 3: 0.90, 4: 0.95 },
  //  레버③: S1~2(clearedStage 0~1, engine.offerSymbolRewards 의 realStage<=2) 오퍼 SILVER 풀에서
  //   EXP 기여형/기본 보조 심볼 가중 ×1.5(legendWeight 선례 — 풀 반복 삽입. 정수 근사를 위해 boosted:일반
  //   삽입비 3:2 사용 = 상대가중치 정확히 1.5배, S3+ 는 무가중).
  //   실사 근거(engine.js evaluate/SP_DESC 효과·SYMS tags 참조, 전부 SILVER 특수=cat:special & 고급/기본 rarity):
  //    tome(📖족보, tags:["학습"], exp+12/매치·book 상위계열) · cherry_ripe(🍑숙성체리, exp+6/매치·cherry 상위계열) ·
  //    alarm(⏰알람, ALARM: 다음스핀 EXP+10%) · knot(🪢매듭, KNOT: 양끝 동일심볼 EXP+20) ·
  //    energypack(🧃에너지팩, ENERGYPACK: 이번스핀 EXP+30%) · bandage(🩹붕대, BANDAGE: 해골 패널티 1개분 상쇄).
  //   ★스펙 원문 예시(바구니류/summary_note)는 실제 카탈로그 불일치로 대체: cart(🛒장바구니)=GOLD 특수(희귀 rarity,
  //   SILVER 풀 미해당) · summary_note 는 POUCH 심볼이 아니라 ITEM 카탈로그(PHASE 아이템) — 둘 다 제외.
  EARLY_EXP_BOOST_IDS: ["tome", "cherry_ripe", "alarm", "knot", "energypack", "bandage"],
};

// ── Phase 4 V3P4: 소모형/일회용 속성 맵 ──────────────────────────────
//  "instant": 릴 등장 즉시 효과 발동 + 덱에서 1개 제거(일회용). 토스트 표시.
//  "fuse": 덱 상주, 조건 충족 시 발동 + 제거(소모형). 기존 훅 재사용(상점/소멸/실패직전/와일드취급).
//  미등록 심볼은 영구 상주(use=undefined).
//  POUCH_CAT 선례 따라 별도 맵(SYMS 무오염).
export const POUCH_USE = {
  // 실버 instant
  bandage: "instant",     // 🩹붕대: 이번 스핀 최저 심볼 패널티 감소
  knot: "instant",        // 🪢매듭: 양옆 동일 심볼이면 EXP+20
  energypack: "instant",  // 🧃에너지팩: 이번 스핀 EXP+30%
  // 실버 fuse
  safepin: "fuse",        // 🧷안전핀노트: 증강 레벨업 미발생 시 다음 확률 +1%p 누적
  // 골드 fuse
  crystal: "fuse",        // 🔮수정구: 다음 보상 후보 +1
  temp_wild: "fuse",      // 🧲임시와일드: 등장 스핀에서 와일드 취급 후 제거
  // 프리즘 instant
  fake_crown_sym: "instant",  // 👑가짜왕관: 그 스핀 왕관 취급(업적 카운트 제외)
  evo_core: "instant",        // 🧬진화핵: 기본 이득 심볼 1개→랜덤 특수 변환
  // 프리즘 fuse
  fate_vortex: "fuse",        // 🌀운명의소용돌이: 스핀 2회 굴려 유리한 쪽(스테이지 1회)
  // 저주 fuse
  black_card: "fuse",         // 💳검은카드: 다음 상점 1개 무료+불운게이지+1
  // 족쇄(⛓shackle)는 use 없음 — 영구 상주 저주(보스 보상 2배/보스 스핀 -1)
  // §9.2 J3: 소모형 심볼(fuse — 조건 충족 시 발동+제거)
  bell_ticket: "fuse",      // 🎟종소리티켓: 종 4개 리치 → 5개 승격(잭팟), 런 2회 제한
  jackpot_ticket: "fuse",   // 🎟잭팟티켓: 리치 → 잭팟 승격 후 제거, 런 2회 공유 카운터
  // reach_mark/retry_reel: use 없음(특수 신호 + 스테이지 1회 제한, 상주하다 조건 적중 시 신호)
  // slot_shard/jackpot_wand/cheer/big_boom: use 없음(상주 — 매 스핀 evaluate 개입)
  // jackpot_crown: use 없음(상주 — 잭팟 시 1회 발동, 스테이지 1회 제한)
};

// ── §9.0 V3 J1: 잭팟 태그 맵 — 아이콘이 달라도 같은 태그면 잭팟 판정(심화모드 전용) ───────────────
//  POUCH_CAT 선례 — SYMS 무오염·별도 맵. jackpotTagOf(id) 헬퍼(engine.js 에서 export).
//  미등록 심볼 → null(태그 없음·동일 심볼 잭팟 경로만). 와일드는 미기여(판정 코드에서 제외).
//  태그 6종: crown / seven / coin / prism / curse / (bell = J3에서 채워짐)
//  ★bell 키는 구조 확보용으로만(빈 배열 — J3 전 심볼 미할당). J3에서 bell 심볼 추가 시 채워질 맵 슬롯.
export const JACKPOT_TAG = {
  // crown 태그: 왕관 계열(실왕관 + 가짜왕관 + 잭팟왕관)
  crown:          "crown",
  fake_crown_sym: "crown",
  jackpot_crown:  "crown",   // §9.2 J3: 잭팟왕관 — crown 태그 기여(4왕관+잭팟왕관 → 잭팟 발동 가능)
  // seven 태그: 7계열
  lucky7:         "seven",
  // coin 태그: 코인 계열
  coin:           "coin",
  coin_bag:       "coin",
  // prism 태그: 프리즘 계열
  prism_sym:      "prism",
  // prism_ticket(프리즘티켓) — 현재 미정의 심볼이라 주석(J3에서 신규 심볼 추가 시 등록)
  // curse 태그: 저주 심볼 6종
  bloodrop:       "curse",
  curse_eye:      "curse",
  black_candle_sym: "curse",
  unstable_bomb:  "curse",
  black_card:     "curse",
  shackle:        "curse",
  // bell 태그: §9.2 J3 종 세트 5종 (종소리티켓은 태그 잭팟에 기여, fuse 조건은 별도)
  small_bell:    "bell",
  echo_bell:     "bell",
  golden_bell:   "bell",
  festival_bell: "bell",
  bell_ticket:   "bell",
};

// ── §9.0 V3 J1: 잭팟 태그 제한 상수 ─────────────────────────────────────
//  같은 jackpotTag 를 가진 특수심볼(cat=special)의 덱 상한.
//  ★base 심볼(crown/coin 등 기본계열)은 이 상한에서 제외(특수만 카운트·pouchValidate 참조).
export const JACKPOT_TAG_DECK_MAX = 8;
