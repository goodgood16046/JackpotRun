// 잭팟런 — 선택 화면용 큐레이션 카탈로그 + 시너지 엔진.
// 소스: SlotV2Engine.kt (CHARS/MACHINES/DEVICES/SETS) 의 실제 수치를 사람이 보기 좋게 가공.
// 앱(Kotlin)은 손대지 않음 — 웹 카탈로그만 보강. 데이터 계약(chars/machines/ownedDevices,
// jackpotcmd 쓰기)은 그대로 유지한다.
//
//  diff   : 플레이 난이도 1~4  (1 입문 / 2 초중급 / 3 중급 / 4 고급)
//  ceiling: 점수 고점 잠재력 0~3
//  stab   : 안정성 0~3
//  risk   : 폭망/변동 위험 0~3
//  theme  : 시너지 매칭용 빌드 테마
//  unlock : 해금 조건 힌트(잠긴 카드에 표시). null = 기본 제공

export const DIFF_LABEL = { 1: "입문", 2: "초중급", 3: "중급", 4: "고급" };
export const DIFF_COLOR = { 1: "#34d3c0", 2: "#5b8cff", 3: "#a974ff", 4: "#ffd23f" };

// ── 캐릭터 16 ────────────────────────────────────────────────
export const CHARS = {
  novice: {
    e: "🎒", n: "초보학생", role: "입문 · 안정", theme: "만능",
    eff: "요구치 ↓ · 점수보정 ×0.90",
    tags: ["초보추천", "안정형"], diff: 1, ceiling: 0, stab: 3, risk: 0,
    pros: ["요구 EXP가 낮아 클리어가 가장 쉬움", "실수해도 잘 안 망함"],
    cons: ["점수보정 ×0.90 — 리더보드 고점은 낮음"],
    build: "입문 / 안정", unlock: null,
  },
  scholar: {
    e: "📗", n: "장학생", role: "경험치 · 책", theme: "책",
    eff: "📘책 EXP +2 · 클리어 코인 +2",
    tags: ["초보추천", "안정형", "책"], diff: 1, ceiling: 1, stab: 3, risk: 0,
    pros: ["책 EXP가 꾸준히 쌓여 안정적", "클리어마다 코인을 더 받아 상점이 든든"],
    cons: ["혼자서는 폭발력이 약함 — 책 머신과 함께"],
    build: "도서관 / 책 / 안정", unlock: null,
  },
  gambler: {
    e: "🎲", n: "도박꾼", role: "재굴림 · 고점", theme: "재굴림",
    eff: "점수보정 ×1.10 · 스테이지당 무료 재굴림 1회",
    tags: ["고점형", "재굴림", "위험"], diff: 3, ceiling: 3, stab: 1, risk: 2,
    pros: ["나쁜 스핀을 무료로 다시 굴릴 수 있음", "점수보정 ×1.10 — 고점이 높음"],
    cons: ["운에 의존해 기복이 큼", "초보에게는 판단이 어려움"],
    build: "재굴림 / 콤보 / 고점", unlock: null,
  },
  farmer: {
    e: "🍒", n: "체리농부", role: "체리 · 안정", theme: "체리",
    eff: "🍒체리 EXP +1 · 희귀심볼 ↓",
    tags: ["안정형", "체리"], diff: 1, ceiling: 1, stab: 3, risk: 0,
    pros: ["체리가 꾸준히 떠 EXP가 안정적", "희귀 변동이 줄어 기복이 작음"],
    cons: ["고점은 제한적 — 안전한 대신 폭발력 낮음"],
    build: "체리 / 안정", unlock: "2런 or 업적 체리 수확가",
  },
  parttime: {
    e: "🪙", n: "알바생", role: "코인 · 상점", theme: "코인",
    eff: "시작코인 +15 · 첫 스핀 -20%",
    tags: ["안정형", "코인"], diff: 2, ceiling: 1, stab: 2, risk: 1,
    pros: ["시작코인 +15로 초반 상점을 빠르게 굴림", "유물/리롤 자금이 넉넉"],
    cons: ["첫 스핀이 약해 출발이 느림"],
    build: "상점 / 코인 / 유물", unlock: "3런",
  },
  jeweler: {
    e: "💎", n: "보석상", role: "보석 · 점수", theme: "보석",
    eff: "💎보석 점수 +25 · 점수보정 ×1.10",
    tags: ["고점형", "보석"], diff: 2, ceiling: 3, stab: 2, risk: 1,
    pros: ["보석 한 개당 점수가 크게 붙음", "점수보정까지 높아 리더보드에 강함"],
    cons: ["보석이 안 뜨면 평범 — 보석 머신과 궁합"],
    build: "보석 / 점수", unlock: "2,500점 or 업적 첫 잭팟",
  },
  honor: {
    e: "🎓", n: "수석졸업생", role: "증강 · 스노볼", theme: "증강",
    eff: "실버 증강 1개를 들고 시작",
    tags: ["상급자용", "증강"], diff: 3, ceiling: 2, stab: 2, risk: 1,
    pros: ["증강 1개 선출발 — 빌드 완성이 빠름", "어떤 머신과도 무난"],
    cons: ["시작 증강이 랜덤이라 방향을 따라가야 함"],
    build: "증강 / 학습 / 스노볼", unlock: "S8 도달 or 업적 졸업반",
  },
  cultist: {
    e: "💀", n: "해골숭배자", role: "해골 · 저주", theme: "해골",
    eff: "☠해골 EXP +3 · 저주 1개당 점수 +8%",
    tags: ["고점형", "해골", "위험"], diff: 3, ceiling: 3, stab: 1, risk: 3,
    pros: ["위험 심볼인 해골을 EXP로 역이용", "저주를 쌓을수록 점수가 불어남"],
    cons: ["해골이 뭉치면 크게 손해", "저주 빌드라 운영 난이도 높음"],
    build: "해골 / 저주 / 고점", unlock: "S5 도달 or 업적 중간고사 통과",
  },
  crowncol: {
    e: "👑", n: "왕관수집가", role: "왕관 · 한방", theme: "왕관",
    eff: "👑왕관 점수 +30 · 등장 ↑",
    tags: ["고점형", "왕관", "한방"], diff: 3, ceiling: 3, stab: 1, risk: 2,
    pros: ["왕관이 자주 뜨고 점수가 크게 붙음", "한 번에 큰 점수를 노림"],
    cons: ["왕관 운에 기복이 큼 — 변동성 높음"],
    build: "왕관 / 한방 / 점수", unlock: "5,000점 or 업적 대관식",
  },
  minimalist: {
    e: "🍃", n: "미니멀리스트", role: "저유물 · 도전", theme: "도전",
    eff: "유물 3개 이하면 모든 EXP +25%",
    tags: ["상급자용", "도전"], diff: 4, ceiling: 2, stab: 1, risk: 2,
    pros: ["유물을 적게 들수록 강해지는 역발상", "잘 다루면 고효율"],
    cons: ["유물을 못 사는 제약 — 운영이 까다로움"],
    build: "저유물 / 도전 / 증강", unlock: "S7 도달 or 업적 완벽한 계산",
  },
  lucky: {
    e: "🍀", n: "행운아", role: "희귀 · 한방", theme: "한방",
    eff: "희귀심볼 등장 +25%",
    tags: ["고점형", "한방", "랜덤성↑"], diff: 2, ceiling: 2, stab: 1, risk: 2,
    pros: ["희귀 심볼이 자주 떠 한방이 잦음", "고변동 머신과 합이 좋음"],
    cons: ["기복이 크고 안 풀리면 평범"],
    build: "희귀 / 한방", unlock: "4런",
  },
  highroller: {
    e: "💠", n: "큰손", role: "보석 · 코인", theme: "보석",
    eff: "💎보석 점수 +25 · 시작코인 +12",
    tags: ["고점형", "보석", "코인"], diff: 2, ceiling: 3, stab: 2, risk: 1,
    pros: ["보석 점수 + 시작코인으로 점수·상점 동시에", "초반부터 빌드를 빠르게 세움"],
    cons: ["보석 의존 — 보석 머신과 함께가 정석"],
    build: "보석 / 점수 / 상점", unlock: "3,500점",
  },
  monk: {
    e: "🧘", n: "수도승", role: "속전속결", theme: "속전속결",
    eff: "스핀 -1 · 요구치 -10%",
    tags: ["상급자용", "속전속결"], diff: 3, ceiling: 1, stab: 2, risk: 1,
    pros: ["스핀이 짧아 빠르게 클리어", "요구치가 낮아 효율적"],
    cons: ["스핀이 적어 한 번의 실수가 치명적"],
    build: "속전속결 / 효율", unlock: "S6 도달",
  },
  alchemist: {
    e: "⚗️", n: "연금술사", role: "코인 · 상점", theme: "코인",
    eff: "코인 +25% · 클리어 코인 +3",
    tags: ["안정형", "코인"], diff: 2, ceiling: 1, stab: 3, risk: 0,
    pros: ["코인이 크게 불어 상점을 폭주시킴", "안정적으로 빌드를 완성"],
    cons: ["코인을 점수로 바꾸는 빌드 설계가 필요"],
    build: "상점 / 코인 / 유물", unlock: "4,000점",
  },
  daredevil: {
    e: "😈", n: "무모한도전", role: "고위험 · 고점", theme: "고점",
    eff: "모든 EXP +20% · 요구치 +25%",
    tags: ["고점형", "위험", "상급자용"], diff: 4, ceiling: 3, stab: 0, risk: 3,
    pros: ["모든 EXP +20% — 순수 화력 최강", "잘 굴리면 최고점 도전 가능"],
    cons: ["요구치 +25%로 폭망 위험 큼", "상급자 전용"],
    build: "고위험 / 고점 / 화력", unlock: "S8 도달 or 업적 만점왕",
  },
  prodigy: {
    e: "🌟", n: "천재", role: "만능 · 안정", theme: "만능",
    eff: "모든 EXP +12% · 점수보정 ×0.95",
    tags: ["안정형", "만능"], diff: 2, ceiling: 2, stab: 3, risk: 0,
    pros: ["모든 EXP +12%가 어떤 빌드든 받쳐줌", "안정적이면서 무난한 고효율"],
    cons: ["점수보정 ×0.95 — 극고점에는 약간 손해"],
    build: "만능 / 안정 / 효율", unlock: "S9 도달 or 업적 10층 등반",
  },
};

// ── 슬롯머신 16 ──────────────────────────────────────────────
export const MACHINES = {
  basic: {
    e: "🎰", n: "기본", role: "표준", theme: "만능", eff: "표준 확률 · 점수보정 ×1.00",
    tags: ["초보추천"], diff: 1, ceiling: 1, stab: 3, risk: 0,
    pros: ["치우침 없는 표준 확률 — 어떤 캐릭터와도 무난"], cons: ["특화 보너스가 없어 고점은 평범"],
    build: "입문 / 만능", unlock: null,
  },
  cherry: {
    e: "🍒", n: "체리", role: "체리 · 안정", theme: "체리", eff: "🍒체리 ↑ · 👑왕관 ↓ · 점수보정 ×0.95",
    tags: ["초보추천", "안정형", "체리"], diff: 1, ceiling: 1, stab: 3, risk: 0,
    pros: ["체리가 자주 떠 안정적", "변동이 작아 초보도 편함"], cons: ["고점은 낮은 편 (점수보정 ×0.95)"],
    build: "체리 / 안정", unlock: null,
  },
  library: {
    e: "📚", n: "도서관", role: "책 · 경험치", theme: "책", eff: "📘책 ↑ · 🪙코인/💎보석 ↓",
    tags: ["안정형", "책"], diff: 1, ceiling: 1, stab: 3, risk: 0,
    pros: ["책이 쏟아져 학습 빌드의 엔진", "EXP가 안정적으로 누적"], cons: ["코인·보석이 줄어 점수형엔 불리"],
    build: "책 / 학습 / 경험치", unlock: null,
  },
  gem: {
    e: "💎", n: "보석", role: "보석 · 점수", theme: "보석", eff: "💎보석 ↑ · 🍒/📘 ↓ · 점수보정 ×1.10",
    tags: ["고점형", "보석"], diff: 2, ceiling: 3, stab: 2, risk: 1,
    pros: ["보석이 자주 떠 점수가 크게 붙음", "점수보정 ×1.10으로 리더보드 강함"], cons: ["체리·책이 줄어 EXP는 빡빡"],
    build: "보석 / 점수", unlock: "1,500점 or 업적 첫 잭팟",
  },
  magnet: {
    e: "🧲", n: "자석", role: "콤보", theme: "콤보", eff: "🧲자석 대량 등장 (콤보·세트 용이)",
    tags: ["콤보"], diff: 2, ceiling: 2, stab: 2, risk: 1,
    pros: ["같은 심볼이 뭉쳐 콤보·세트를 만들기 쉬움", "재굴림과 합이 매우 좋음"], cons: ["해골도 함께 뭉칠 수 있음"],
    build: "콤보 / 재굴림 / 세트", unlock: "4런",
  },
  skull: {
    e: "☠", n: "해골", role: "해골 · 고위험", theme: "해골", eff: "☠해골 ↑ · 고위험 · 점수보정 ×1.10",
    tags: ["고점형", "해골", "위험"], diff: 3, ceiling: 3, stab: 1, risk: 3,
    pros: ["해골을 EXP로 바꾸는 빌드면 고점 폭발"], cons: ["대비 없이 쓰면 폭망 — 해골 캐릭/장치 필수"],
    build: "해골 / 저주 / 고점", unlock: "4,000점 or 업적 중간고사 통과",
  },
  crown: {
    e: "👑", n: "왕관", role: "왕관 · 한방", theme: "왕관", eff: "👑왕관 ↑ · 기본 ↓ · 점수보정 ×1.20",
    tags: ["고점형", "왕관", "한방", "랜덤성↑"], diff: 3, ceiling: 3, stab: 1, risk: 2,
    pros: ["왕관이 자주 떠 운빨 고점", "점수보정 ×1.20 — 최상위 고점"], cons: ["기복이 매우 큼"],
    build: "왕관 / 한방 / 점수", unlock: "6,000점 or 업적 대관식",
  },
  flame: {
    e: "🔥", n: "불꽃", role: "배율", theme: "배율", eff: "🔥불꽃·☠해골 ↑ · 점수보정 ×1.10",
    tags: ["고점형", "위험"], diff: 3, ceiling: 2, stab: 1, risk: 2,
    pros: ["불꽃 배율로 점수가 크게 붙음", "불꽃엔진 장치와 시너지"], cons: ["해골도 함께 늘어 위험"],
    build: "배율 / 화력", unlock: "6런 or 업적 만점왕",
  },
  bomb: {
    e: "💣", n: "폭탄", role: "제거 · 계산", theme: "계산", eff: "💣폭탄 ↑ (제거/계산) · 점수보정 ×1.10",
    tags: ["콤보"], diff: 3, ceiling: 2, stab: 2, risk: 1,
    pros: ["폭탄으로 판을 정리하는 계산형 플레이"], cons: ["폭탄 활용 설계가 필요 — 입문엔 어려움"],
    build: "계산 / 콤보", unlock: "8런 or 업적 졸업반",
  },
  star: {
    e: "⭐", n: "별빛", role: "콤보 · 세트", theme: "콤보", eff: "⭐별 ↑ · 세트 잘 맞음 · 점수보정 ×1.05",
    tags: ["콤보"], diff: 2, ceiling: 2, stab: 2, risk: 1,
    pros: ["별 콤보·세트를 만들기 쉬움", "꾸준한 중상위 빌드"], cons: ["폭발적 한방은 약함"],
    build: "콤보 / 세트", unlock: "3,000점",
  },
  clover: {
    e: "🍀", n: "행운", role: "행운 · 코인", theme: "한방", eff: "희귀·🪙코인·🔥불꽃 ↑ · 점수보정 ×1.05",
    tags: ["한방", "코인", "랜덤성↑"], diff: 2, ceiling: 2, stab: 2, risk: 1,
    pros: ["희귀와 코인이 함께 늘어 폭넓게 활용", "행운 빌드의 기반"], cons: ["방향이 분산되기 쉬움"],
    build: "행운 / 코인 / 희귀", unlock: "5런",
  },
  casino: {
    e: "🎲", n: "카지노", role: "운빨 · 고변동", theme: "한방", eff: "🎲주사위 등장 · 고변동 · 점수보정 ×1.10",
    tags: ["한방", "위험", "랜덤성↑"], diff: 3, ceiling: 3, stab: 0, risk: 3,
    pros: ["주사위로 초고변동 — 터지면 최고점"], cons: ["폭망도 잦은 순수 운빨 머신"],
    build: "운빨 / 한방", unlock: "4,500점",
  },
  garden: {
    e: "🌱", n: "정원", role: "성장형", theme: "성장", eff: "🌱씨앗 등장 (다음 스핀 성장) · 점수보정 ×1.05",
    tags: ["콤보"], diff: 2, ceiling: 2, stab: 2, risk: 1,
    pros: ["씨앗이 다음 스핀에 자라는 성장 플레이", "차근차근 쌓는 재미"], cons: ["즉발 화력은 낮음"],
    build: "성장 / 콤보", unlock: "7런",
  },
  wildmac: {
    e: "🌀", n: "와일드", role: "세트 조작", theme: "세트", eff: "🌀와일드 등장 (세트 합류) · 점수보정 ×1.10",
    tags: ["콤보", "세트"], diff: 3, ceiling: 3, stab: 1, risk: 2,
    pros: ["와일드가 세트를 채워 콤보 성공률↑", "복사기/교체기 장치와 강력"], cons: ["조작 장치 없으면 잠재력 절반"],
    build: "세트 / 콤보 / 조작", unlock: "5,000점",
  },
  vault: {
    e: "🗝", n: "금고", role: "코인 · 상점", theme: "코인", eff: "🗝열쇠 등장 · 🪙코인 ↑ · 점수보정 ×1.10",
    tags: ["코인"], diff: 3, ceiling: 2, stab: 2, risk: 1,
    pros: ["열쇠·코인으로 상점을 폭주", "경제 빌드의 핵심"], cons: ["코인을 점수로 환전하는 설계 필요"],
    build: "상점 / 코인", unlock: "9런",
  },
  rainbow: {
    e: "🌈", n: "무지개", role: "한방 · 고변동", theme: "한방", eff: "⭐💎👑 ↑ · 🍒📘 ↓ · 고변동 · 점수보정 ×1.20",
    tags: ["고점형", "한방", "위험", "랜덤성↑"], diff: 4, ceiling: 3, stab: 0, risk: 3,
    pros: ["희귀 심볼만 모인 초고점 머신", "터지면 압도적 점수"], cons: ["EXP 기반이 약해 폭망 위험 최고"],
    build: "한방 / 고점 / 도박", unlock: "8,000점",
  },
};

// ── 장치 12 (영구 소지 중 런당 1개 장착 · 선택) ───────────────
//  획득: 대부분 업적 보상으로 영구 해금 (런 중 장치 노드로도 획득).
export const DEVICES = {
  dev_flame: {
    e: "🔥", n: "불꽃엔진", kind: "패시브", cmd: null, cool: "상시(패시브)",
    eff: "장착 시 모든 스핀 EXP +15%",
    tags: ["패시브", "고점형"], ceiling: 2, stab: 1, risk: 0,
    when: "거의 모든 빌드 (만능 화력)", unlock: "업적 슬롯의 지배자(5만점) — 또는 런 중 장치 노드",
  },
  dev_seal: {
    e: "🔒", n: "봉인장막", kind: "패시브", cmd: null, cool: "상시(패시브)",
    eff: "☠해골 미등장 · 모든 스핀 EXP +5%",
    tags: ["패시브", "안정형"], ceiling: 1, stab: 3, risk: 0,
    when: "초보 / 안정형 (해골 차단)", unlock: "업적 왕관 수집가(👑10) — 또는 런 중 장치 노드",
  },
  dev_safe: {
    e: "🦺", n: "안전벨트", kind: "패시브", cmd: null, cool: "상시(패시브)",
    eff: "모든 스핀 최소 EXP 보장 (폭망 방지)",
    tags: ["패시브", "초보추천", "안정형"], ceiling: 0, stab: 3, risk: 0,
    when: "초보 / 안정형 (폭망 보험)", unlock: "업적 체리 수확가(🍒100) — 또는 런 중 장치 노드",
  },
  dev_overheat: {
    e: "♨️", n: "과열코어", kind: "패시브", cmd: null, cool: "상시(패시브)",
    eff: "모든 스핀 EXP +18% · ☠해골 +1 등장 (고위험)",
    tags: ["패시브", "고점형", "위험"], ceiling: 3, stab: 0, risk: 2,
    when: "고점 / 해골 빌드 (위험 감수)", unlock: "업적 벼락치기 천재(막스핀 클리어 5) — 또는 런 중 장치 노드",
  },
  dev_subreel: {
    e: "➕", n: "보조릴", kind: "패시브", cmd: null, cool: "상시(패시브)",
    eff: "항상 6칸 슬롯 · 최종 EXP -30%",
    tags: ["패시브", "콤보", "도전"], ceiling: 2, stab: 1, risk: 1,
    when: "콤보 / 세트 빌드 (칸 ↑)", unlock: "업적 첫 잭팟 — 또는 런 중 장치 노드",
  },
  dev_coin: {
    e: "🪙", n: "코인투입구", kind: "능동", cmd: "투입", cool: "스테이지당 1회",
    eff: "코인 5 소모 → 다음 스핀 EXP +30%",
    tags: ["능동", "코인"], ceiling: 2, stab: 1, risk: 0,
    when: "코인 / 상점 빌드 (자원 환전)", unlock: "업적 만점왕(1만점) — 또는 런 중 장치 노드",
  },
  dev_reroll: {
    e: "🔄", n: "재굴림기", kind: "능동", cmd: "재굴림", cool: "스테이지당 1회",
    eff: "직전 스핀 결과 다시 굴림 (점수 -10%)",
    tags: ["능동", "재굴림", "초보추천"], ceiling: 1, stab: 2, risk: 0,
    when: "초보 / 안정 / 재굴림 빌드", unlock: "업적 중간고사 통과(보스1) — 또는 런 중 장치 노드",
  },
  dev_pin: {
    e: "📌", n: "고정핀", kind: "능동", cmd: "고정 N", cool: "스테이지당 1회",
    eff: "직전 결과 N번 칸 유지 · 나머지 재굴림",
    tags: ["능동", "콤보"], ceiling: 1, stab: 2, risk: 0,
    when: "콤보 빌드 (좋은 칸 고정)", unlock: "업적 완벽한 계산 — 또는 런 중 장치 노드",
  },
  dev_copy: {
    e: "📑", n: "복사기", kind: "능동", cmd: "복사 N", cool: "스테이지당 1회",
    eff: "직전 결과 N번 칸을 옆 칸에 복사",
    tags: ["능동", "세트", "콤보"], ceiling: 2, stab: 1, risk: 0,
    when: "세트 / 콤보 조작 빌드", unlock: "업적 규칙 파괴자(프리즘 5) — 또는 런 중 장치 노드",
  },
  dev_swap: {
    e: "🔃", n: "교체기", kind: "능동", cmd: "교체 N", cool: "스테이지당 1회",
    eff: "직전 결과 N번 칸을 최다 심볼로 교체",
    tags: ["능동", "세트"], ceiling: 2, stab: 1, risk: 0,
    when: "세트 완성 빌드", unlock: "업적 졸업반(보스 5) — 또는 런 중 장치 노드",
  },
  dev_oracle: {
    e: "🔮", n: "예언안경", kind: "능동", cmd: "예언", cool: "스테이지당 1회",
    eff: "다음 스핀을 미리 보고 확정",
    tags: ["능동", "안정형"], ceiling: 1, stab: 2, risk: 0,
    when: "도전 / 안정 (변수 제거)", unlock: "업적 10층 등반(S10) — 또는 런 중 장치 노드",
  },
  dev_bell: {
    e: "🔔", n: "비상졸업벨", kind: "능동", cmd: "비상", cool: "런 1회 (파괴)",
    eff: "부족 EXP ≤25면 즉시 클리어",
    tags: ["능동", "한방"], ceiling: 1, stab: 1, risk: 0,
    when: "한방 / 도전 (아슬아슬 구제)", unlock: "업적 단골(20런) — 또는 런 중 장치 노드",
  },
};

// ── 핵심 시너지 조합(헤드라인) ───────────────────────────────
//  키 "charId|machineId" → {s: 점수 1~3, b: 시너지 문구}
const PAIRS = {
  "farmer|cherry":    { s: 3, b: "🍒체리 등장·EXP가 겹쳐 흔들림 없이 굴러가는 안정형 정석." },
  "scholar|library":  { s: 3, b: "📘책 EXP가 두 곳에서 쌓여 경험치 빌드의 핵심 엔진이 됩니다." },
  "jeweler|gem":      { s: 3, b: "💎보석 점수가 두 배로 불어나 리더보드 고점이 폭발합니다." },
  "highroller|gem":   { s: 3, b: "💎보석 점수 + 시작코인으로 점수와 상점을 동시에 챙깁니다." },
  "cultist|skull":    { s: 3, b: "☠해골이 점수·EXP로 바뀌어 고위험·고점 빌드가 완성됩니다." },
  "crowncol|crown":   { s: 3, b: "👑왕관이 쏟아지고 점수가 붙어 단숨에 큰 점수를 노립니다." },
  "gambler|magnet":   { s: 3, b: "같은 심볼이 뭉치고, 나쁜 스핀은 무료 재굴림으로 복구합니다." },
  "lucky|rainbow":    { s: 3, b: "희귀 심볼이 쏟아지는 초고변동 한방 빌드 — 터지면 최고점." },
  "gambler|star":     { s: 2, b: "⭐별 콤보를 노리다 실패하면 재굴림으로 다시 시도." },
  "parttime|vault":   { s: 2, b: "🗝열쇠·코인으로 상점을 빠르게 돌리는 경제 빌드." },
  "alchemist|vault":  { s: 2, b: "코인 증식 + 열쇠 등장으로 상점이 폭주합니다." },
  "highroller|vault": { s: 2, b: "시작코인 + 열쇠로 초반부터 상점을 장악." },
  "daredevil|rainbow":{ s: 2, b: "고위험·고변동 — 최고점을 노리는 극단 도박 빌드." },
  "daredevil|casino": { s: 2, b: "EXP +20%로 초고변동 카지노를 밀어붙이는 도박." },
  "honor|library":    { s: 2, b: "증강 선출발 + 책 학습으로 스노볼이 매우 빠릅니다." },
  "scholar|gem":      { s: 1, b: "책으로 EXP, 보석으로 점수 — 균형형이지만 자원이 분산." },
  "minimalist|basic": { s: 2, b: "유물 최소화 도전과 가장 잘 맞는 표준 머신." },
  "cultist|flame":    { s: 2, b: "불꽃 배율 + 해골 EXP로 화력형 고점." },
  "crowncol|rainbow": { s: 2, b: "왕관 보너스 + 희귀 머신으로 한방 고점 극대화." },
  "prodigy|basic":    { s: 1, b: "EXP +12%가 표준 머신을 무난하게 받쳐줍니다." },
  "monk|cherry":      { s: 1, b: "짧은 스핀을 안정적인 체리로 빠르게 클리어." },
};

// 장치 적합 — themes/chars/macs 중 하나라도 맞으면 +s, anti 면 경고.
const DEV_FIT = {
  dev_reroll:  { s: 1.5, chars: ["gambler"], macs: ["magnet", "star", "garden"], themes: ["재굴림", "콤보", "안정"], b: "실패한 스핀을 다시 굴려 콤보 성공률을 끌어올립니다." },
  dev_safe:    { s: 1.2, chars: ["novice"], macs: ["cherry", "basic", "skull", "casino", "rainbow"], themes: ["안정"], b: "최소 EXP를 보장해 폭망을 막아줍니다." },
  dev_seal:    { s: 1.2, themes: ["안정"], b: "해골을 막아 안정성을 올립니다.", anti: ["skull", "flame"], antiB: "해골/불꽃 머신의 핵심 심볼을 막아 빌드와 충돌할 수 있습니다." },
  dev_flame:   { s: 1.2, themes: ["만능", "고점", "배율", "해골", "왕관", "보석"], b: "모든 스핀 EXP +15% — 어떤 빌드에도 잘 붙는 만능 화력." },
  dev_overheat:{ s: 1.5, chars: ["cultist", "daredevil"], macs: ["skull", "flame"], themes: ["해골", "고점"], b: "EXP를 크게 올리는 대신 해골 위험을 감수하는 고점용." },
  dev_coin:    { s: 1.5, chars: ["parttime", "alchemist", "highroller"], macs: ["vault", "clover"], themes: ["코인"], b: "남는 코인을 EXP로 환전해 경제 빌드를 가속합니다." },
  dev_copy:    { s: 1.5, macs: ["wildmac", "star", "magnet"], themes: ["세트", "콤보"], b: "심볼을 복사해 세트·콤보를 인위적으로 완성합니다." },
  dev_swap:    { s: 1.5, macs: ["wildmac", "magnet"], themes: ["세트"], b: "최다 심볼로 교체해 세트를 마무리합니다." },
  dev_pin:     { s: 1.2, macs: ["magnet", "star", "garden"], themes: ["콤보"], b: "좋은 칸을 고정하고 나머지만 재굴림 — 콤보를 안정화." },
  dev_subreel: { s: 1.0, macs: ["magnet", "star", "wildmac"], themes: ["콤보", "세트"], b: "6칸으로 콤보 폭이 넓어집니다 (최종 EXP -30% 감수)." },
  dev_oracle:  { s: 1.0, chars: ["minimalist"], themes: ["도전", "안정"], b: "다음 스핀을 미리 보고 확정해 변수를 제거합니다." },
  dev_bell:    { s: 1.2, macs: ["rainbow", "crown", "casino"], themes: ["한방"], b: "아슬아슬한 스테이지를 즉시 졸업시켜 줍니다 (1회)." },
};

const GRADE_COLOR = { S: "#ff7adb", A: "#ffd23f", B: "#5b8cff", C: "#8b93a7" };
export const gradeColor = (g) => GRADE_COLOR[g] || "#8b93a7";

function stars(n) { return "★★★★★☆☆☆☆☆".slice(5 - Math.max(0, Math.min(5, n)), 10 - Math.max(0, Math.min(5, n))); }
const clamp5 = (n) => Math.max(1, Math.min(5, Math.round(n)));
function meterFrom(raw, lo, hi) { return clamp5(1 + ((raw - lo) / (hi - lo)) * 4); }

// ── 조합 시너지/메터 계산 — app.js 가 요약 패널에서 호출 ──────
export function evaluate(charId, macId, devId) {
  const c = CHARS[charId], m = MACHINES[macId];
  const d = devId ? DEVICES[devId] : null;
  if (!c || !m) return null;

  // 1) 시너지 점수(0~) — 핵심 페어 → 테마일치 → 만능/증강 폴백
  let syn = 0; const notes = []; const warns = [];
  const pair = PAIRS[charId + "|" + macId];
  if (pair) { syn += pair.s; notes.push(pair.b); }
  else if (c.theme === m.theme) { syn += 2; notes.push(`${c.theme} 테마가 겹쳐 빌드 방향이 또렷합니다.`); }
  else if (c.theme === "만능" || c.theme === "증강") { syn += 1; notes.push(`${c.n}은(는) 어떤 머신과도 무난히 어울립니다.`); }
  else if (m.theme === "만능") { syn += 0.8; notes.push("표준 머신이라 캐릭터 개성을 무난히 받쳐줍니다."); }

  // 2) 장치 적합
  if (d) {
    const fit = DEV_FIT[devId];
    if (fit) {
      const hit = (fit.chars || []).includes(charId) || (fit.macs || []).includes(macId) ||
        (fit.themes || []).some((t) => t === c.theme || t === m.theme || (c.tags || []).includes(t === "안정" ? "안정형" : t) || (m.tags || []).includes(t === "안정" ? "안정형" : t));
      if (hit) { syn += fit.s; notes.push(fit.b); }
      if (fit.anti && fit.anti.includes(macId)) warns.push(fit.antiB);
    }
  }

  // 3) 캐릭/머신 위험 충돌 경고
  if (c.risk >= 2 && m.risk >= 2 && !(d && (devId === "dev_safe" || devId === "dev_seal")))
    warns.push("캐릭터·머신 둘 다 변동이 커서 기복이 매우 큽니다. 안전벨트/봉인장막을 고려하세요.");
  if (m.theme === "해골" && charId !== "cultist" && devId !== "dev_seal" && devId !== "dev_safe")
    warns.push("해골 대비책(해골숭배자/봉인장막/안전벨트)이 없으면 폭망 위험이 큽니다.");

  // 4) 메터(1~5)
  const riskRaw = c.risk + m.risk + (d ? d.risk : 0);
  const ceilRaw = c.ceiling + m.ceiling + (d ? d.ceiling * 0.6 : 0) + syn * 0.4;     // 0 ~ ~8.6
  const stabRaw = c.stab + m.stab + (d ? d.stab * 0.6 : 0) - riskRaw * 0.6;          // ~ -5 ~ 7.8
  const ceiling = meterFrom(ceilRaw, 0.5, 8);
  const stability = meterFrom(stabRaw, -3, 7);
  const diffBase = Math.max(c.diff, m.diff) + (riskRaw >= 5 ? 1 : 0);
  const difficulty = clamp5(diffBase);

  // 5) 등급
  const grade = syn >= 4 ? "S" : syn >= 2.7 ? "A" : syn >= 1.3 ? "B" : "C";

  // 6) 빌드/장단점 취합
  const builds = [c.build, m.build, d ? d.when : ""].filter(Boolean);
  const buildTokens = [...new Set(builds.join(" / ").split(/\s*\/\s*/).map((s) => s.trim()).filter(Boolean))].slice(0, 4);
  const pros = [...new Set([...(c.pros || []), ...(m.pros || [])])].slice(0, 4);
  const cons = [...new Set([...(c.cons || []), ...(m.cons || []), ...warns])].slice(0, 4);

  return {
    grade, gradeColor: gradeColor(grade),
    ceiling, stability, difficulty,
    ceilingStars: stars(ceiling), stabilityStars: stars(stability), difficultyStars: stars(difficulty),
    diffLabel: ["", "낮음", "낮음", "보통", "높음", "매우 높음"][difficulty],
    blurb: notes.join(" ") || "무난한 조합입니다. 마음에 드는 테마로 골라보세요.",
    warns, pros, cons, buildTokens,
  };
}

// ── 추천 조합 — 보유 목록 안에서 최적을 고른다(없으면 폴백) ───
export function recommend(kind, unlockedChars, unlockedMacs, ownedDevs) {
  const has = (arr, id) => arr.includes(id);
  const firstOf = (cands, pool) => cands.find((id) => has(pool, id)) || pool[0];
  const dev = (cands) => cands.find((id) => (ownedDevs || []).includes(id)) || "";
  if (kind === "beginner") {
    return { char: firstOf(["novice", "scholar", "farmer", "prodigy"], unlockedChars),
             machine: firstOf(["basic", "cherry", "library"], unlockedMacs),
             device: dev(["dev_safe", "dev_seal", "dev_reroll"]) };
  }
  if (kind === "high") {
    return { char: firstOf(["gambler", "crowncol", "jeweler", "cultist", "daredevil"], unlockedChars),
             machine: firstOf(["crown", "gem", "rainbow", "magnet"], unlockedMacs),
             device: dev(["dev_flame", "dev_overheat", "dev_reroll"]) };
  }
  if (kind === "challenge") {
    return { char: firstOf(["daredevil", "minimalist", "cultist", "monk"], unlockedChars),
             machine: firstOf(["rainbow", "casino", "skull"], unlockedMacs),
             device: dev(["dev_overheat", "dev_bell", "dev_oracle"]) };
  }
  // random — 보유 목록에서 무작위(이미지 시드 회피용으로 호출측에서 처리)
  return null;
}

// 카드 정렬 키
export function unlockOrder(meta) {
  // 최근 해금순 정렬용 — unlock 문자열에서 숫자(점/런/스테이지) 추출, 클수록 최신
  if (!meta.unlock) return 0;
  const m = meta.unlock.replace(/,/g, "");
  const score = (m.match(/(\d+)점/) || [])[1];
  const runs = (m.match(/(\d+)런/) || [])[1];
  const st = (m.match(/S(\d+)/) || [])[1];
  return (score ? +score : 0) + (runs ? +runs * 700 : 0) + (st ? +st * 800 : 0) + 1;
}
