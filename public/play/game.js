// ── 잭팟 슬롯 (웹 단독판) — 런 상태머신 컨트롤러 ──────────────────────
// 순수 로직(DOM 없음). UI(ui.js)가 메서드를 호출하고 반환된 상태/이벤트를 그린다.
// 저장은 localStorage('slotweb_profile') — 기존 카톡 잭팟런과 완전 격리.
import * as E from "./engine.js";
import {
  C, CHARS, MACHINES, DEVICES, ITEM_BY_ID, DEV_BY_ID, ACHIEVEMENTS, ACH_DEVICE_REWARD, ACH_SYMBOL_UNLOCK,
  AUG_BY_ID, REL_BY_ID, CUR_BY_ID, AUGMENTS, CURSES, RELICS, CHAR_BY_ID, MAC_BY_ID, PERK_BY_ID, SYM_BY_ID, EMPTY_SYM,
  SYMS, SETS, ITEMS, VALUE_IDS, THEME_BUILDS, THEME_BUILD_CATEGORIES, TBUILD_BY_ID,
  POUCH_RARITY, POUCH_UPGRADE, POUCH_SYMBOLS, DEEP, DEFAULT_UNLOCKED_SYMS, TIER_BY_RARITY,
  SYM_AUGMENTS, SYM_RELICS, SYM_AUG_BY_ID, SYM_PERK_BY_ID, POUCH_USE, POUCH_CAT, JACKPOT_TAG,
} from "./data.js";

const fmt2 = (n) => (Math.round(n * 100) / 100).toString();
// 심화모드(주머니) 전용 상위 심볼 id 집합(일반 도감 노출 제외용).
const POUCH_UPGRADE_VALUES = new Set(Object.values(POUCH_UPGRADE));
// 일반모드 evaluate 가 인식하는 기존 special enum(이 밖의 special = Phase4 심화 전용 특수심볼).
const CORE_SPECIALS = new Set(["NONE", "WILD", "BOMB", "MAGNET", "SKULL", "DICE", "COIN", "KEY", "FLAME", "SEED"]);
// 심화모드(주머니) 전용 심볼(상위계열 6종 + Phase4 특수 30종) — 일반 도감 노출 제외(격리·표시측).
//  ★key_gold 는 special="KEY"(코어)라 CORE_SPECIALS 로는 안 걸림 → 명시 제외(일반 열쇠 key 와 중복 표기 방지).
const DEEP_ONLY_SYMS = new Set([
  ...POUCH_UPGRADE_VALUES,
  ...SYMS.filter((s) => s.weight === 0 && !CORE_SPECIALS.has(s.special)).map((s) => s.id),
  "key_gold", "bloodrop",   // 코어 special 재사용/일반 exp 보유이나 심화 전용 신규 id
]);

// [LOW-2] 장치 발동 기록 식별용 — usedCmds 엔 특수스핀(FOCUS/ALLIN/PRAY/LAST)·도박꾼(GREROLL) 기록도 섞여 있어
//  배터리/정비키트의 '장치 재사용 해제'는 장치 cmd 항목만 골라 제거해야 함(특수스핀 1회 제한 풀림 방지).
const DEVICE_CMDS = new Set(DEVICES.filter((d) => d.cmd).map((d) => d.cmd));

const PHASE = {
  SELECT_CHAR: "SELECT_CHAR", SELECT_MACHINE: "SELECT_MACHINE", SELECT_DEVICE: "SELECT_DEVICE",
  SPIN: "SPIN", POST_SPIN: "POST_SPIN", STAGE_CLEAR: "STAGE_CLEAR", NODE_SELECT: "NODE_SELECT", PERK_PICK: "PERK_PICK",
  SHOP: "SHOP", DEVICE_NODE: "DEVICE_NODE", REWARD_DONE: "REWARD_DONE", RUN_END: "RUN_END",
};
export { PHASE };

const SPECIAL_LABEL = { FOCUS: "🎯집중", ALLIN: "🎰올인", PRAY: "🙏기도", LAST: "⏰최후" };
// 조작 장치(manip) 배너용 라벨/설명
const MANIP_LABEL = { "재굴림": "🔄재굴림", "고정": "📌고정", "복사": "📑복사", "교체": "🔃교체",
  "지휘": "🎯지휘", "정화": "🧤정화", "셔플": "🔀셔플" };
const MANIP_DESC = {
  "재굴림": "직전 스핀을 다시 굴립니다 (도박꾼=무료·재굴림기=점수 -10%)",
  "고정": "선택한 칸만 남기고 나머지를 다시 굴립니다",
  "복사": "선택한 칸을 옆 칸에 복사합니다",
  "교체": "선택한 칸을 최다 심볼로 바꿉니다 (점수 -10%)",
  // Phase 5 심볼 장치(심화 전용)
  "지휘": "선택한 칸을 같은 등급의 다른 심볼로 바꿉니다 (심볼지휘봉)",
  "정화": "☠해골 1칸을 ▫빈칸으로 정화합니다 (정화장갑)",
  "셔플": "이번 스핀을 주머니 확률표로 다시 추출합니다 (주머니셔플러)",
};

// 특수심볼 한 줄 설명(도감 카드 detail용) — raw enum 노출 대신 읽기 쉬운 효과로. dead 심볼 없음.
const SP_DESC = {
  COIN: "코인 획득", SKULL: "기본 무페널티·해골빌드면 EXP가산", FLAME: "스핀 전체 EXP+50%(다음-50%)",
  MAGNET: "옆 심볼 복사", BOMB: `양옆 제거+칸당 EXP`, KEY: `보물코인 +${C.KEY_COIN_PER}🪙/개`,
  DICE: "1~12 무작위 EXP", SEED: "다음 스핀 성장", WILD: "최다 그룹 합류(세트·잭팟)",
  // ── Phase 4 심화모드 특수심볼(주머니 전용) 설명 — 도감 카드 detail 용. 근사 항목은 명시. ──
  SEED_ANY: "다음 스핀 무작위 기본심볼로 성장", SEED_HIGH: "다음 스핀 체리/책/별로 성장",
  CATALYST: "최저등급 심볼 1개 한 단계 강화", PURIFY: "해골 1개를 빈칸으로 정화",
  WANDWILD: "무작위 1심볼 와일드 취급(세트·양끝 보조)", MIRROR: "양끝(1·마지막) 칸 서로 복사",
  TARGET: "최고 EXP 값심볼 칸 효과 +50%", PUZZLE5: "서로 다른 값심볼 4종+150·5종+300점",
  ALARM: "다음 스핀 EXP +10%", HOURGLASS: "이번 스핀 EXP 30%를 다음 스핀으로 이월",
  RECEIPT: "다음 상점 전체 가격 -10%", COUPON: "다음 상점 상품 1개 할인", CART: "다음 상점 상품칸 +1",
  GEAR: "다음 스핀 EXP +10%(근사)", DEVCD: "능동장치 재사용 1회 허용(근사)",
  SETFRAG: "세트 형성 시 코인 소량 보상(근사)",
  // ── Phase 5 부활 심볼(심화 증강노드 전제) ──
  AUGCHANCE: "다음 증강 레벨업 발생 확률 +15%", AUGLEVEL: "보유 증강 1개 즉시 레벨업",
  KIT: "능동장치 재사용 1회 허용(근사) 또는 다음 상점 정비소 등장",
  SHIELD: "다음 보스 패널티 1회 방어", EXEMPT: "다음 보스 감점룰 1회 무시",
  CURSE_BLOOD: "EXP +10 · 불운게이지 상승", CURSE_CANDLE: "해골 수만큼 배율·해골없으면 EXP 0",
  CURSE_BOOM: "50% 대폭발(×2) · 50% EXP 0", CURSE_EYE: "다음 주머니 보상 후보 +1 · 불운게이지↑",
  LUCKY7: "3개+이면 EXP/점수/코인 7배", PRISM_SYM: "무작위 프리즘급 미니효과",
  // ── V3P4 신규 심볼 설명(소모형/일회용) ──
  BANDAGE: "일회용 — 해골 패널티 1회 상쇄 후 제거",
  KNOT: "일회용 — 양끝 동일 시 EXP +20 후 제거",
  SAFEPIN: "소모형 — AUGLEVEL 미발생 시 확률 +1%p 누적 후 제거",
  ENERGYPACK: "일회용 — 이번 스핀 특수심볼 EXP 배율 ×1.3 후 제거",
  CRYSTAL: "소모형 — 등장 시 다음 주머니 보상 후보 +1 예약 후 제거",
  TEMPWILD: "소모형 — 이번 스핀 와일드 칸 1개 주입 후 제거",
  FAKECROWN: "일회용 — 이번 스핀 왕관 취급(업적 카운트 제외) 후 제거",
  FATEVORTEX: "소모형 — 스테이지당 1회: 스핀 2회 굴려 더 좋은 결과 선택 후 제거",
  EVOCORE: "일회용 — 기본 이득 심볼 1개를 SILVER 특수로 변환 후 제거",
  BLACKCARD: "소모형 — 불운게이지+1, 다음 상점 1개 무료 후 제거",
  SHACKLE: "영구 저주 — 보스 스핀 -1, 보스 클리어 코인 +4",
};

// §10 V3: 심볼 1줄 효과 설명(덱 보드 카드용) — dex() 의 sym/deepSym detail 생성 규칙과 동일(SP_DESC 우선,
//  기본 심볼은 exp/score/coin 요약 + 태그). dex() 는 별도 리팩터 없이 그대로 두고(무회귀), 카드 렌더 전용으로 신설.
function symDeckDesc(s) {
  if (!s) return "";
  if (s.special && s.special !== "NONE" && SP_DESC[s.special]) {
    return SP_DESC[s.special] + (s.tags && s.tags.length ? ` · #${s.tags.join(" #")}` : "");
  }
  const parts = [];
  if (s.exp) parts.push(`EXP ${s.exp}`);
  if (s.score) parts.push(`점수 ${s.score}`);
  if (s.coin) parts.push(`코인 ${s.coin}`);
  if (s.tags && s.tags.length) parts.push(`#${s.tags.join(" #")}`);
  return parts.length ? parts.join(" · ") : "특수 효과";
}

const STORE_KEY = "slotweb_profile";
function memStore() { let v = null; return { getItem: () => v, setItem: (_k, x) => { v = x; } }; }
function defaultProfile() {
  return { bestScore: 0, totalScore: 0, runs: 0, bestStage: 0, ownedDevices: [], unlocked: [], counters: {}, seen: {}, playerXp: 0, playerLevel: 1, ascMax: -1, graduations: 0, bestAscScore: 0, bestAscLevel: 0, mastery: { char: {}, mac: {}, dev: {} }, symUnlocked: [], bestDeepScore: 0, bestDeepStage: 0, deepRaresSeenIds: [], deepLegendsSeenIds: [] };
}
// ── 플레이어 레벨(메타 진행) ── 레벨은 "콘텐츠 해금 게이트"일 뿐, 영구 스탯 보정 없음(랭킹 밸런스 보호).
const PLV_MAX = 100;
const xpReq = (lvl) => 120 + (lvl - 1) * 60;   // 레벨 lvl→lvl+1 에 필요한 XP(점증)
function levelInfo(totalXp) {
  let lvl = 1, rem = Math.max(0, Math.floor(totalXp || 0));
  while (lvl < PLV_MAX && rem >= xpReq(lvl)) { rem -= xpReq(lvl); lvl++; }
  const need = lvl >= PLV_MAX ? 0 : xpReq(lvl);
  return { level: lvl, inLevel: rem, need, ratio: need ? rem / need : 1, xp: Math.floor(totalXp || 0), max: lvl >= PLV_MAX };
}
// 기존 플레이어(런 이력 보유)에게 최초 1회만 부여할 초기 XP — 그동안의 플레이를 레벨에 반영.
function seedXpFromHistory(p) {
  const c = p.counters || {};
  return (p.runs || 0) * 30 + Math.floor((p.totalScore || 0) / 300) + (c.bossClears || 0) * 15 + (p.bestStage || 0) * 8;
}
// ── 심화 학기(승천) ── asc 0=일반. 누적 난이도 + 점수 보정. "더 강해진 게 아니라 더 어려운 룰을 이긴다".
//  ★승천 점수는 일반 랭킹/bestScore 에 반영 안 함(별도) — 랭킹 밸런스 보호. 규칙 수치는 라이브 튜닝영역.
const ASC_MAX = 10;
function ascMods(asc) {
  const a = Math.max(0, Math.min(ASC_MAX, Math.floor(asc || 0)));
  return {
    a,
    quotaMul: 1 + 0.08 * a,                          // 요구치 +8%/단계
    bossQuotaMul: a >= 4 ? 1 + 0.06 * (a - 3) : 1,   // A4↑ 보스 요구치 추가 상승
    shopPriceMul: a >= 3 ? 1 + 0.12 * (a - 2) : 1,   // A3↑ 상점가 상승
    itemCapDelta: a >= 5 ? -1 : 0,                   // A5↑ 아이템 보유칸 -1
    startCoinDelta: a >= 3 ? -Math.min(16, (a - 2) * 4) : 0,
    scoreMul: 1 + 0.12 * a,                          // 점수 보정 (A10 ≈ ×2.2)
  };
}
// 승천 단계별 "이번에 추가되는 규칙" 안내(누적). UI 표기용.
const ASC_RULE = {
  1: "요구 EXP +8%", 2: "요구 EXP 추가 +", 3: "상점 가격 상승", 4: "보스 요구 EXP 상승",
  5: "아이템 보유칸 -1", 6: "요구·상점 더 상승", 7: "시작 코인 감소", 8: "전 구간 난이도 상승",
  9: "고점 요구", 10: "극한 난이도",
};
// ── 숙련도(캐릭터/슬롯/장치를 많이·잘 쓰면 오름) ── 보상=칭호·변형 해금(Phase 4). 레벨=충족 마일스톤 수(0~5).
const MASTERY = {
  char: [
    { lv: 1, test: (s) => s.bestStage >= 3, d: "스테이지 3 도달" },
    { lv: 2, test: (s) => s.bossClears >= 3, d: "보스 3회 처치" },
    { lv: 3, test: (s) => s.bestStage >= 15, d: "스테이지 15 클리어(졸업)" },
    { lv: 4, test: (s) => s.bestScore >= 50000, d: "점수 50,000 달성" },
    { lv: 5, test: (s) => s.ascMax >= 5, d: "심화 학기 5 졸업" },
  ],
  mac: [
    { lv: 1, test: (s) => s.runs >= 3, d: "3판 플레이" },
    { lv: 2, test: (s) => s.bestStage >= 8, d: "스테이지 8 도달" },
    { lv: 3, test: (s) => s.bossClears >= 5, d: "보스 5회 처치" },
    { lv: 4, test: (s) => s.bestScore >= 40000, d: "점수 40,000 달성" },
    { lv: 5, test: (s) => s.bestStage >= 15, d: "스테이지 15 클리어" },
  ],
  dev: [
    { lv: 1, test: (s) => s.runs >= 5, d: "5판 장착" },
    { lv: 2, test: (s) => s.runs >= 15, d: "15판 장착" },
    { lv: 3, test: (s) => s.runs >= 30, d: "30판 장착" },
    { lv: 4, test: (s) => s.runs >= 60, d: "60판 장착" },
    { lv: 5, test: (s) => s.runs >= 100, d: "100판 장착" },
  ],
};
function masteryLevel(kind, stats) {
  const defs = MASTERY[kind] || []; const s = stats || {}; let lv = 0, next = null;
  for (const d of defs) { if (d.test(s)) lv++; else if (!next) next = d; }
  return { lv, next, total: defs.length };
}
// 플레이어 레벨 도달 시 지급되는 후반 장치(콘텐츠 해금 — 스탯 영구증가 아님, 장착 선택).
const LEVEL_DEVICE_REWARD = { 14: "dev_reaper", 18: "dev_abyss", 22: "dev_reactor" };

export class Game {
  constructor(opts = {}) {
    this.storage = opts.storage || (typeof localStorage !== "undefined" ? localStorage : memStore());
    this.rng = E.makeRng(opts.seed || ((Date.now ? Date.now() : 1) & 0xffffffff));
    this.profile = this._loadProfile();
    this._grantLevelDevices();   // 기존 고레벨 플레이어에게 후반 장치 즉시 지급(멱등)
    this.run = null;
    this.log = [];   // 최근 이벤트 토스트(UI 표시)
  }

  _loadProfile() {
    try {
      const raw = this.storage.getItem(STORE_KEY);
      if (raw) {
        const p = { ...defaultProfile(), ...JSON.parse(raw) };
        if (p._xpInit !== true) {   // 레벨 시스템 최초 도입 마이그레이션: 기존 이력으로 초기 XP 부여
          if ((p.runs || 0) > 0 && !(p.playerXp > 0)) p.playerXp = seedXpFromHistory(p);
          p._xpInit = true;
        }
        p.playerLevel = levelInfo(p.playerXp).level;
        return p;
      }
    } catch (e) {}
    return defaultProfile();
  }
  // UI 용 레벨 진행 정보(level/inLevel/need/ratio/xp/max).
  playerProgress() { return levelInfo(this.profile.playerXp); }
  // 레벨 해금 트리(후반 목표판) — 레벨로 열리는 캐릭/슬롯/장치/증강/유물을 레벨순 정렬 + 해금여부.
  levelUnlocks() {
    const lvl = this.profile.playerLevel || 1; const out = [];
    for (const c of CHARS) if (c.unlockLevel) out.push({ lv: c.unlockLevel, label: `🎭 ${c.n} (캐릭터)` });
    for (const mc of MACHINES) if (mc.unlockLevel) out.push({ lv: mc.unlockLevel, label: `🎰 ${mc.n} (슬롯)` });
    for (const k of Object.keys(LEVEL_DEVICE_REWARD)) { const d = DEV_BY_ID[LEVEL_DEVICE_REWARD[k]]; if (d) out.push({ lv: +k, label: `🔧 ${d.n} (장치)` }); }
    for (const a of AUGMENTS) if (a.unlockLevel) out.push({ lv: a.unlockLevel, label: `✨ ${a.n} (증강)` });
    for (const r of RELICS) if (r.unlockLevel) out.push({ lv: r.unlockLevel, label: `📜 ${r.n} (유물)` });
    out.sort((x, y) => x.lv - y.lv);
    return out.map((o) => ({ ...o, unlocked: lvl >= o.lv }));
  }
  // 심화 학기(승천) — 해금여부/최대선택가능/단계정보.
  ascUnlocked() { return (this.profile.ascMax ?? -1) >= 0; }
  maxPlayableAsc() { return Math.max(0, (this.profile.ascMax ?? -1) + 1); }
  ascInfo(a) { const m = ascMods(a); return { a: m.a, scoreMul: m.scoreMul, rule: ASC_RULE[m.a] || "", cleared: (this.profile.ascMax ?? -1) >= m.a, max: ASC_MAX }; }
  // ── 숙련도: 런 종료 시 사용한 캐릭/슬롯/장치 성과 누적 + 조회 ──
  _bumpMastery(kind, id) {
    if (!id) return; const r = this.run, p = this.profile;
    if (!p.mastery) p.mastery = { char: {}, mac: {}, dev: {} };
    const bag = p.mastery[kind] || (p.mastery[kind] = {});
    const s = bag[id] || (bag[id] = { runs: 0, bestStage: 0, bossClears: 0, bestScore: 0, ascMax: -1 });
    s.runs += 1;
    s.bestStage = Math.max(s.bestStage, r.stage);
    s.bossClears += r.stats.bossClears;
    s.bestScore = Math.max(s.bestScore, r.finalScore || 0);
    if (r.graduatedThisRun) s.ascMax = Math.max(s.ascMax ?? -1, r.asc);
  }
  masteryOf(kind, id) {
    const bag = (this.profile.mastery && this.profile.mastery[kind]) || {};
    const stats = bag[id] || { runs: 0, bestStage: 0, bossClears: 0, bestScore: 0, ascMax: -1 };
    return { ...masteryLevel(kind, stats), stats };
  }
  // 보상/상점 풀 — 후반(unlockLevel) 증강·유물은 플레이어 레벨 도달 후에만 등장.
  _augPool() { const lvl = this.profile.playerLevel || 1; return AUGMENTS.filter((a) => !a.unlockLevel || lvl >= a.unlockLevel); }
  _relicPool() { const lvl = this.profile.playerLevel || 1; return RELICS.filter((r) => !r.unlockLevel || lvl >= r.unlockLevel); }
  // 레벨업 가능한 보유 증강(레벨업 등록 증강 & Lv3 미만).
  //  일반 증강(AUG_LEVELS) + 심화 심볼증강(SYM_AUG_LEVELS·Phase5 형광펜/복습책 대상). 둘 다 Lv3 미만만.
  _levelableHeld() {
    const r = this.run; if (!r) return [];
    return r.perks.filter((id) =>
      ((AUG_BY_ID[id] && E.isAugLevelable(id)) || (SYM_AUG_BY_ID[id] && E.isSymAugLevelable(id)))
      && ((r.perkLevels[id] || 1) < 3));
  }
  // 레벨업 카드/토스트용 증강 조회(일반 AUG_BY_ID 또는 심볼 SYM_AUG_BY_ID).
  _augInfo(id) { return AUG_BY_ID[id] || SYM_AUG_BY_ID[id]; }
  // 현재 플레이어 레벨에 해당하는 후반 장치를 미보유 시 지급. 반환=이번에 지급된 id 목록.
  _grantLevelDevices() {
    const p = this.profile; const lvl = p.playerLevel || 1; const got = [];
    for (const k of Object.keys(LEVEL_DEVICE_REWARD)) {
      const dev = LEVEL_DEVICE_REWARD[k];
      if (lvl >= +k && !p.ownedDevices.includes(dev)) { p.ownedDevices.push(dev); got.push(dev); }
    }
    return got;
  }
  _saveProfile() { try { this.storage.setItem(STORE_KEY, JSON.stringify(this.profile)); } catch (e) {} }
  toast(msg) { this.log.push(msg); if (this.log.length > 40) this.log.shift(); }

  // ── 해금 판정 (프로필 기반) ──
  charUnlocked(c) {
    const p = this.profile;
    if (!c.unlockRuns && !c.unlockScore && !c.unlockStage && !c.unlockLevel && !c.unlockAch) return true;
    if (c.unlockRuns && p.runs >= c.unlockRuns) return true;
    if (c.unlockScore && p.bestScore >= c.unlockScore) return true;
    if (c.unlockStage && p.bestStage >= c.unlockStage) return true;
    if (c.unlockLevel && (p.playerLevel || 1) >= c.unlockLevel) return true;
    if (c.unlockAch && p.unlocked.includes(c.unlockAch)) return true;
    return false;
  }
  machineUnlocked(m) {
    const p = this.profile;
    if (!m.unlockRuns && !m.unlockScore && !m.unlockLevel && !m.unlockAch) return true;
    if (m.unlockRuns && p.runs >= m.unlockRuns) return true;
    if (m.unlockScore && p.bestScore >= m.unlockScore) return true;
    if (m.unlockLevel && (p.playerLevel || 1) >= m.unlockLevel) return true;
    if (m.unlockAch && p.unlocked.includes(m.unlockAch)) return true;
    return false;
  }
  unlockedChars() { return CHARS.filter((c) => this.charUnlocked(c)); }
  unlockedMachines() { return MACHINES.filter((m) => this.machineUnlocked(m)); }
  ownedDevices() { return this.profile.ownedDevices.map((id) => DEV_BY_ID[id]).filter(Boolean); }

  // ── 런 시작 ──
  //  deep=true → 심화모드(심볼 주머니 덱빌딩). 일반모드와 완전 격리(pouch로만 추출·압축 패널티·심볼 보상).
  //  ★심화모드에서는 승천(asc) 미적용(요구치 이중 가중 방지) — deep 이면 asc 강제 0.
  startRun(asc = 0, deep = false) {
    this.log = [];
    const wantDeep = !!deep;
    const maxAsc = Math.max(0, (this.profile.ascMax ?? -1) + 1);
    const useAsc = wantDeep ? 0 : Math.max(0, Math.min(maxAsc, Math.floor(asc || 0)));
    this.run = {
      asc: useAsc, graduatedThisRun: false,
      deepMode: wantDeep,                                   // 심화모드 여부(일반=false)
      pouch: wantDeep ? E.startPouch() : null,              // 심볼 주머니 { [symId]: count } (심화만)
      // 심화 추적(랭킹 오염 방지·요약 + Phase5 심화 업적 카운터 소스). 전부 심화 런에서만 존재(일반=null).
      deepStats: wantDeep ? {
        rewardsPicked: 0, repairs: 0,
        maxTotal: E.pouchTotal(E.startPouch()),   // 이번 런 최대 총량(대형주머니 업적)
        bossClears: 0,                            // 심화 보스 클리어 수(심볼마스터)
        compress95Clear: false, compress85BossClear: false,   // 압축 클리어(첫압축/위험한압축)
        cherry50BossClear: false, skull40BossClear: false,     // 태그 전공(체리/저주)
        gem50Score30kBoss: false, crown2BossClear: false,      // 보석전공/왕관연구
        balanceBossClear: false, skull0BossClear: false,       // 완벽한균형/정화자
        raresSeen: new Set(), legendsSeen: new Set(),          // 희귀/전설 등급 심볼 발견(수집가/연구자)
      } : null,
      // ── Phase 3: 심화모드 상점 '심볼 정비' 누적 상태(심화만 의미·전부 r.deepMode 게이팅) ──
      deepTotalMaxDelta: 0,     // 덱확장 누적(총량 상한 +5씩)
      deepTotalMinDelta: 0,     // 덱압축 누적(총량 최소 -5씩)
      deepCompressExtra: 0,     // 덱압축 요구경험치 추가율(+0.05씩) — _deepPenalty 곱셈
      deepTagBuff: {},          // 태그강화 { [태그]: 배수증가분 +0.10씩 } — evaluate 심화 배수
      // ── Phase 4: 심화모드 특수심볼 메타 상태(전부 r.deepMode 게이팅·신규 필드=기존 무변경) ──
      growNext: null,           // 🌱씨앗/🌿새싹: "ANY"|"HIGH" — 다음 _roll 후처리 성장(심화만)
      carryOverExp: 0,          // ⏳모래시계: 이번스핀 30% → 다음스핀 stageExp 이월
      deepShopDiscount: false,  // 🧾영수증: 다음 상점 전체가 -10%(1회)
      deepShopCoupon: false,    // 🎟쿠폰: 다음 상점 상품 1개 -15%(1회)
      deepShopSlotBonus: 0,     // 🛒장바구니: 다음 상점 상품칸 +N(1회)
      bossShield: false,        // 🛡방패: 이번 스핀 보스 패널티 방어(1회)
      bossExempt: false,        // 📋시험지: 이번 스핀 보스 감점룰 무시(1회)
      deepRewardBonus: 0,       // 🧿저주눈: 다음 주머니 보상 후보 +N(근사)
      perkLevels: {}, _augLevelChance: 0.10, _augLevelBoost: 0,   // 증강 레벨업(Lv1~3) 추적 + 발생 확률(pity+촉매)
      _prismInk: false, _prismInkBought: false,   // 프리즘 잉크(다음 증강노드 프리즘 강제) 상태
      _bossPhase2: false, _devCdUntil: 0,          // A10 2페이즈 보스 / A9 장치 쿨다운
      // §9.0 J1: 잭팟 태그 시스템 상태 필드(심화 전용)
      _reachBias: null,              // §9.0 리치 달성 시 다음 스핀 해당 태그 bias ×1.5 ({tag, spinsLeft:1})
      _jackpotPrismPending: false,   // §9.0 태그잭팟 달성 시 다음 POUCH 오퍼 프리즘 후보 1장 보장
      // §9.1 J2: 피버 게이지 상태 필드(심화 전용 — 일반모드 deepMode=false 시 미생성·격리)
      feverGauge: 0,                 // 0~DEEP.FEVER_MAX: 콤보+15/리치+25/잭팟+50 충전. 100 도달 시 피버 진입 후 리셋.
      feverSpins: 0,                 // 피버 잔여 스핀 수(3에서 차감, 0이면 피버 종료).
      _feverJackpotPrism: false,     // 피버잭팟 → 다음 POUCH 오퍼 프리즘 보장 추가(1회).
      // §9.2 J3: 도파민 심볼 웨이브 상태 필드(심화 전용)
      _bellTicketUses: 0,      // 종소리티켓(fuse) 런당 최대 2회 사용 카운터
      _jpTicketUses: 0,        // 잭팟티켓(fuse) 런당 최대 2회 사용 카운터 (bell_ticket과 공유 아님)
      _reachMarkUsed: false,   // 리치표식 스테이지 1회 제한
      _retryReelUsed: false,   // 재도전릴 스테이지 1회 제한(예약 신호)
      _retryReelPending: false, // 재도전릴 다음 스핀 1칸 재굴림 신호
      _jackpotCrownUsed: false,    // 잭팟왕관 스테이지 1회 제한
      _jackpotCrownPending: false, // 잭팟왕관 보상등급+1 — 다음 POUCH 오퍼 소비
      deepPity: null,           // 배치F P2: {id, spinsLeft} 신규심볼 등장 보장 — add/upgrade 직후 설정(덮어쓰기=최신 우선), 첫 fresh 굴림에서 소진
      perfectDrawStage: 0,      // 배치F P6: 퍼펙트 드로우 지급 스테이지 마킹(스테이지당 1회, stage 단조증가=자기 리셋)
      deepArchFamily: null, deepArchTier: 0,   // 배치G: 마지막으로 관측한 활성 전공 계열/티어(발동·승급 토스트 게이팅)

      phase: PHASE.SELECT_CHAR, stage: 1, spinIndex: 0, spins: 0, stageExp: 0, quota: 0,
      score: 0, coins: 0, charId: null, machineId: null, device: "",
      perks: [], curses: [], armItems: [], phaseItems: [], items: [],   // items = 보유 아이템 백팩(최대3)
      lastCells: null, lastExpApplied: 0, lastResult: null,
      flameNext: false, seedNext: false, lockedNext: null,
      survive: false, debtStages: 0, usedCmds: [], pendingNextExpMul: 1,
      cmdFreeUsed: { FOCUS: false, ALLIN: false, PRAY: false, LAST: false },   // 런 단위: 종류별 첫 1회 무료권(소진 시 true) — _beginStage 리셋 금지
      growthStack: 0, snowStack: 0, fateBellUsed: 0,   // JS-4 조건부 증강 누적스택/운명의종 게이트(값 변경은 JS-4)
      unluckyGauge: 0, boss: null, nodes: [], options: [], shopItems: [],
      stats: { cherry: 0, crown: 0, jackpots: 0, bossClears: 0, lastClears: 0, exactClears: 0, prismPicks: 0, bestSpin: 0 },
      // ── 테마빌드 도감(JS-5) 추적: 런 누적 카운터 + 이번스테이지/런 이벤트 플래그 ──
      tb: { fastClears: 0, prayWins: 0, adjPairs: 0, set4: 0, oracleUsed: false, jackpotRun: false,
            pinUsedStage: false, copySet4: false, wildJackpotRun: false,
            lastSpinClear: false, lastSpinRare: 0, lastSpinSkull: 0, bellUsed: false },
      lastMods: null, lastSpinIndex: 0, seenSyms: new Set(), seenItems: new Set(), shopBought: [],
    };
    if (!this.profile.seen) this.profile.seen = {};
    // 첫 판은 선택 없이 바로 시작 (초보학생 + 기본 슬롯) — 입문 친화
    if (this.profile.runs === 0) {
      this.run.charId = "novice"; this.run.machineId = "basic"; this.run.device = "";
      this.toast("🎮 첫 판은 바로 시작! (초보학생 🎒 + 기본 슬롯 🎰) — 다음 판부터 직접 선택해요");
      this._launch();
      return this.state();
    }
    this.run.options = this.unlockedChars();
    return this.state();
  }

  selectChar(id) {
    const r = this.run; if (r.phase !== PHASE.SELECT_CHAR) return this.state();
    r.charId = id; r.phase = PHASE.SELECT_MACHINE; r.options = this.unlockedMachines();
    return this.state();
  }
  selectMachine(id) {
    const r = this.run; if (r.phase !== PHASE.SELECT_MACHINE) return this.state();
    r.machineId = id;
    // Phase 5: 심화 전용 심볼 장치(deepOnly)는 일반 런에선 장착 후보에서 제외(격리·죽은 no-op 방지). 심화 런은 전부 노출.
    const owned = this.ownedDevices().filter((d) => r.deepMode || !d.deepOnly);
    if (owned.length) { r.phase = PHASE.SELECT_DEVICE; r.options = owned; }
    else { this._launch(); }
    return this.state();
  }
  selectDevice(id) {
    const r = this.run; if (r.phase !== PHASE.SELECT_DEVICE) return this.state();
    r.device = id || ""; this._launch();
    return this.state();
  }

  _launch() {
    const r = this.run;
    const ch = CHAR_BY_ID[r.charId];
    r.coins = Math.max(0, (ch?.startCoins || 0) + ascMods(r.asc).startCoinDelta);
    if (r.charId === "honor") {
      // 심화(deepMode): 시작 증강도 관련성 필터 경유 — raw pickAugments 는 D계열(등장률 심화 no-op) SILVER 퍽을
      //  줄 수 있어 봉쇄(WEBSLOT_DEEP_AUG_SPEC §3f). 기준풀은 일반과 동일 raw AUGMENTS(unlockLevel 미적용 기존 quirk 보존).
      //  pickAugments(rng,1,held,1) ≡ offerPerks(AUGMENTS,…,{clearedStage:0}).options[0] — deep 분기는 풀만 다름.
      //  compatFilter=세트조각 주입 방어(계약상 봉쇄). 빈 오퍼면 기존 `if (a)` 가드로 미지급(방어용·실질 도달 불가).
      const a = r.deepMode
        ? E.offerPerks(E.deepCompatPool(AUGMENTS, r.pouch), "AUGMENT", this.rng, new Set(),
            { clearedStage: 0, compatFilter: (p) => E.isDeepCompat(p, r.pouch) }).options[0]
        : E.pickAugments(this.rng, 1, new Set(), 1)[0];
      if (a) { r.perks.push(a.id); this.toast(`🎓 시작 증강: ${a.e}${a.n}`); }
    }
    this._beginStage();
  }

  _beginStage() {
    const r = this.run;
    r.spinIndex = 0; r.stageExp = 0; r.usedCmds = []; r.armItems = []; r.phaseItems = [];
    r.flameNext = false; r.seedNext = false; r.lockedNext = null; r.lastCells = null; r.lastResult = null; r.lastExpApplied = 0;
    // 테마빌드: 스테이지 단위 플래그/직전스핀 이벤트 리셋(고정핀=이번스테이지, 막스핀 성사정보=이번스핀)
    if (r.tb) { r.tb.pinUsedStage = false; r.tb.lastSpinClear = false; r.tb.lastSpinRare = 0; r.tb.lastSpinSkull = 0; r.tb.bellUsed = false; r.tb.copySet4 = false; }
    r.boss = E.bossFor(r.stage);
    // Phase 5 심볼유물: 낡은파쇄기(sr_shredder) — 스테이지 시작 시 최저가치 심볼 -N(총량 소량 감소).
    if (r.deepMode) { const sp = this._symMods(); if (sp && sp.autoShredN > 0) this._autoShred(sp.autoShredN); }
    // 배치G: 스테이지 진입 시 전공(계열 아키타입) 발동/승급 감지 → 토스트(주머니 변경은 직전 노드/상점서 발생).
    if (r.deepMode) this._checkArchetype();
    const mods = this._mods();
    const am = ascMods(r.asc);
    r.spins = E.spinsPerStage(mods) + E.bossSpins(r.stage);
    // V3P4: ⛓족쇄(shackleActive) — 보스 스테이지에서 스핀 -1(최소 1 보장).
    if (r.deepMode && mods.shackleActive && r.boss && r.spins > 1) r.spins -= 1;
    r.quota = Math.max(1, Math.floor(E.quota(r.stage) * mods.quotaMul * E.bossQuotaMul(r.stage) * am.quotaMul * (r.boss ? am.bossQuotaMul : 1) * (r._bossPhase2 ? 1.3 : 1) * this._deepPenalty()));
    // 심화 8+ : 이번 스테이지 랜덤 금지 심볼 1개(등장 안 함)
    if ((r.asc || 0) >= 8) { r._bannedSym = this.rng.pick(["cherry", "book", "star", "gem"]); const bs = SYM_BY_ID[r._bannedSym]; this.toast(`🚫 심화 규칙 — 이번 스테이지 금지 심볼: ${bs ? bs.e : r._bannedSym}`); } else r._bannedSym = null;
    r.phase = PHASE.SPIN; r.options = [];
    if (r.boss) this.toast(`${r.boss.e} 보스 [${r.boss.n}] — ${r.boss.desc}`);
  }

  // 조건부 증강(JS-2~4)용 RunCtx — 현재 run 상태로 구성. 미설정 필드는 makeCtx 기본=무효.
  _ctx() {
    const r = this.run;
    return {
      stage: r.stage, spinIndex: r.spinIndex, spinsPerStage: r.spins,
      stageExp: r.stageExp, quota: r.quota,
      growthStack: r.growthStack || 0, snowStack: r.snowStack || 0,
      curseCount: r.curses.length, unluckyGauge: r.unluckyGauge,
      boss: !!r.boss, coins: r.coins,
    };
  }

  // 현재 mods (장비+증강+저주+패시브장치+phase아이템). per-spin arm/FOCUS는 doSpin에서.
  _mods(extraItems = []) {
    const r = this.run;
    let mods = E.buildMods(r.machineId, r.charId, r.perks, r.curses, r.device, this._ctx(), r.perkLevels);
    mods = E.applyPassiveDevice(mods, r.device);
    const items = [...r.phaseItems, ...extraItems];
    if (items.length) mods = E.applyItemMods(mods, items);
    // 심화 학기 추가 규칙: A2+ 해골 등장↑, A8+ 이번 스테이지 금지 심볼(등장 0)
    const a = r.asc || 0;
    if (a >= 2) mods = { ...mods, weightAdd: { ...mods.weightAdd, skull: (mods.weightAdd.skull || 0) + 0.5 * (a - 1) } };
    if (a >= 8 && r._bannedSym) mods = { ...mods, symbolWeightMul: { ...mods.symbolWeightMul, [r._bannedSym]: 0 } };
    // 심화모드 태그강화(상점 정비) → evaluate 곱셈 배수. 심화 + 버프 존재 시에만 주입(일반모드 무영향).
    if (r.deepMode && r.deepTagBuff && Object.keys(r.deepTagBuff).length) mods = { ...mods, deepTagMul: { ...r.deepTagBuff } };
    // ── Phase 5 심화 심볼증강/유물 효과 주입 (심화모드 전용·일반 buildMods 무접촉) ──
    //  ★일반모드 격리: r.deepMode 게이팅 + symPerkMods 는 심볼퍽(sa_/sp_/sr_)만 읽음. 일반 perk 는 무영향.
    if (r.deepMode) {
      // 계열 브릿지 활성화(engine.evaluate famBridge 게이팅) — 하위(base) 참조 값강화(perSymbolExp.cherry 등)를
      //  상위계열(cherry_ripe 등) 셀에도 합산. 심볼퍽 보유와 무관하게 심화면 항상 주입(일반 mods 엔 플래그 부재=무회귀).
      // §9.0 J1: deepMode 플래그 주입 — engine.evaluate 잭팟 태그 판정 게이팅(일반 mods 에는 부재=무회귀).
      mods = { ...mods, deepFamilyBridge: true, deepMode: true };
      const sp = this._symMods();
      if (sp) {
        const tagMul = { ...(mods.deepTagMul || {}) };
        for (const t in sp.deepTagMul) tagMul[t] = (tagMul[t] || 0) + sp.deepTagMul[t];
        mods = {
          ...mods,
          deepTagMul: tagMul,
          deepEmptyScore: (mods.deepEmptyScore || 0) + sp.emptyScore,
          deepEmptyExp: (mods.deepEmptyExp || 0) + sp.emptyExp,
          scoreMul: mods.scoreMul * sp.scoreMul,
          quotaMul: mods.quotaMul * sp.quotaMul,
          // [HIGH-3②] 확장계열(확장빌드/확장가방) — 설명대로 ☠해골 감점 완화 배선(quota 경로에서 제거됨).
          skullPenaltyMul: (mods.skullPenaltyMul ?? 1) * (sp.skullPenaltyMul || 1),
        };
        // [MED-3] 전설봉인함(sr_legend_seal) — 주머니 전설(👑crown/7️⃣lucky7/🌈prism_sym) 보유 시
        //  전설 랜덤효과 안정 발동(legendStable, evaluate 프리즘=최선효과 고정). 장치 전설봉인기와 동일 플래그(중복 무해).
        if (sp.legendSeal && r.pouch) {
          const legends = (r.pouch.crown || 0) + (r.pouch.lucky7 || 0) + (r.pouch.prism_sym || 0);
          if (legends > 0 && !mods.legendStable) mods = { ...mods, legendStable: true };
        }
      }
      // ── 배치 G: 계열 아키타입(전공) 보너스 주입 (심화 전용·주머니 비중 기반). ──
      //  ★deepFamily*Mul 은 별개 축(태그버프/브릿지와 무중복 — evaluate 가 계열 base 기준으로 곱). 일반 mods 엔 부재=무영향.
      //   Part1 계열 브릿지(deepFamilyBridge)는 perSymbolExp/Score 를 상위계열에 복사할 뿐 — 곱셈 아키타입과 가산·곱 위치가 달라 중복가산 없음.
      if (r.pouch) {
        const am = E.archetypeMods(E.pouchArchetype(r.pouch));
        if (Object.keys(am.expMul).length || Object.keys(am.scoreMul).length || Object.keys(am.coinMul).length || am.skullPenaltyMul !== 1) {
          // 배치 A Step 1: hex_allornothing dEff — 심화에서 전공 배율 ×0.5(저주 보유 시 적용).
          //   archetypeMods 의 각 계열 증가분(expMul/scoreMul/coinMul map)에 0.5를 곱.
          //   예: t2 expMul.cherry=0.30 → 0.15(절반만 적용).
          let expMulAdj = am.expMul, scoreMulAdj = am.scoreMul, coinMulAdj = am.coinMul;
          if ((r.curses || []).includes("hex_allornothing")) {
            const half = (m) => Object.fromEntries(Object.entries(m).map(([k, v]) => [k, v * 0.5]));
            expMulAdj = half(am.expMul); scoreMulAdj = half(am.scoreMul); coinMulAdj = half(am.coinMul);
          }
          mods = {
            ...mods,
            deepFamilyExpMul: { ...(mods.deepFamilyExpMul || {}), ...expMulAdj },
            deepFamilyScoreMul: { ...(mods.deepFamilyScoreMul || {}), ...scoreMulAdj },
            deepFamilyCoinMul: { ...(mods.deepFamilyCoinMul || {}), ...coinMulAdj },
            skullPenaltyMul: (mods.skullPenaltyMul ?? 1) * am.skullPenaltyMul,   // 강령학파 t2 해골 감점 완화
          };
        }
      }
      // V3P4: ⛓족쇄(SHACKLE·harmful·permanent) — 덱에 있으면 보스 스핀 -1(보스 스테이지에서 스핀 제한), 대신 보스 클리어 보상 코인 +보너스.
      //  bonusSpins 는 E.spinsPerStage 에서 참조하지 않음(spins 는 _beginStage 에서 E.bossSpins 포함해 계산).
      //  근사: clearCoinBonus 로 보스 보상 배가 반영, bonusSpins 로 -1(bossSpins 는 엔진상수라 직접 주입은 _beginStage에서).
      if (r.pouch && (r.pouch.shackle || 0) > 0) {
        mods = { ...mods, shackleActive: true, clearCoinBonus: (mods.clearCoinBonus || 0) + 4 };
      }
      // ── Phase 5: 심화 전용 심볼 PASSIVE 장치(주머니 총량 기반) — deepMode 게이팅(일반 장착 시 no-op·격리). ──
      //  buildMods 는 pouch 를 모르므로 여기서 총량 판정 후 주입. 압축게이지=총량 낮을수록 EXP↑ / 확장저울=총량↑면 해골패널티↓.
      if (r.pouch) {
        const total = E.pouchTotal(r.pouch);
        if (r.device === "dev_compress_gauge") {
          // ★§1.5 V3P1: 총량 60→30 ×0.5 재스케일. 구간 48/54/60 → 24/27/30(기본총량 30 정합).
          const mul = total <= 24 ? 1.14 : total <= 27 ? 1.08 : total <= 30 ? 1.03 : 1;   // 압축 보상(구간별)
          if (mul > 1) mods = { ...mods, expMul: mods.expMul * mul };
        }
        if (r.device === "dev_expand_scale" && total >= 36) {   // ★§1.5 V3P1: 72→36(×0.5, 확장 트레이드오프 임계)
          mods = { ...mods, skullPenaltyMul: (mods.skullPenaltyMul ?? 1) * 0.8 };            // 확장 트레이드오프 완화
        }
        // 🔏전설봉인기 — 주머니에 전설 심볼(crown/lucky7/prism_sym) 보유 시 안정 보너스(변동 큰 전설의 하한 보강·근사).
        //  ★엔진 PRISM/LUCKY7 랜덤 로직은 무접촉(격리·안정성). 전설 보유 시 소폭 EXP 보정으로 "안정 발동" 근사.
        if (r.device === "dev_legend_seal") {
          const legends = (r.pouch.crown || 0) + (r.pouch.lucky7 || 0) + (r.pouch.prism_sym || 0);
          if (legends > 0) mods = { ...mods, expMul: mods.expMul * 1.06, scoreMul: mods.scoreMul * 1.06 };
        }
      }
    }
    return mods;
  }

  // Phase 5: 현재 런의 심볼증강/유물 효과 집계(심화 전용·캐시성 X — 매번 계산). 비심화면 null.
  //  주머니(총량/태그) 조건 판정에 현재 pouch 사용. 일반모드는 호출 자체가 없음(_mods 게이팅).
  _symMods() {
    const r = this.run; if (!r || !r.deepMode) return null;
    return E.symPerkMods(r.perks, r.pouch || {}, r.perkLevels || {});
  }
  // 심화 심볼퍽 보유분(도감/UI 표시용) — r.perks 중 심볼증강/유물만.
  _heldSymPerks() { const r = this.run; if (!r) return []; return r.perks.filter((id) => SYM_PERK_BY_ID[id]); }

  // 배치G: 현재 주머니 전공 상태 조회(UI/HUD·토스트 공용). 비심화면 null.
  _archetype() { const r = this.run; if (!r || !r.deepMode || !r.pouch) return null; return E.pouchArchetype(r.pouch); }
  // 배치G: 주머니 변경 후 전공 발동/승급 감지 → 토스트(변화 있을 때만). 발동/승급/전환 시 알림, 소멸은 무음(과알림 방지).
  //  ★r.deepMode 게이팅(일반모드 무접촉). deepArchFamily/deepArchTier 로 직전 상태 기억.
  _checkArchetype() {
    const r = this.run; if (!r || !r.deepMode || !r.pouch) return;
    const a = E.pouchArchetype(r.pouch);
    const fam = a.tier > 0 ? a.family : null;   // 비활성(tier0)은 "없음"으로 취급
    const prevFam = r.deepArchFamily, prevTier = r.deepArchTier || 0;
    if (fam && (fam !== prevFam || a.tier > prevTier)) {
      const lvl = a.tier >= 2 ? "심화(2차)" : "1차";
      this.toast(`${a.e} 전공 ${fam !== prevFam ? "발동" : "승급"}: ${a.n} ${lvl} — 계열 ${Math.round(a.share * 100)}%`);
    }
    r.deepArchFamily = fam; r.deepArchTier = fam ? a.tier : 0;
  }
  // 심볼 종류 해금 집합 (Phase 5) — 기본 해금(DEFAULT_UNLOCKED_SYMS) + 프로필 업적해금(profile.symUnlocked) 합집합.
  //  ★offerSymbolRewards/symAugPool/repairTargets 가 이 Set 으로 "해금된 심볼만" add/swap 대상에 노출.
  //   engine 은 empty/random 을 별도 예외 처리(항상 개방)하므로 여기 미포함이어도 무관(그래도 명시 포함=안전).
  //   심화모드 전용 개념이나 반환은 항상 유효(일반모드는 이 메서드를 호출하는 경로가 없음=격리).
  _symUnlockedSet() {
    if (this._symUnlockedCache) return this._symUnlockedCache;
    const s = new Set(DEFAULT_UNLOCKED_SYMS);
    for (const id of (this.profile.symUnlocked || [])) s.add(id);
    this._symUnlockedCache = s;
    return s;
  }
  // 해금 집합 캐시 무효화(업적으로 신규 심볼 해금 시 호출).
  _invalidateSymUnlockCache() { this._symUnlockedCache = null; }

  // ── Phase 5: 심화 업적 통계 추적 (심화 런에서만·deepStats 게이팅) ──────────────
  //  ★일반 런은 r.deepStats=null → 전부 no-op(격리). r.stats(일반 카운터)는 절대 안 건드림.
  //  주머니 총량 최대치 갱신 + 희귀/전설 등급 심볼 발견 집합 갱신(수집가/연구자 업적).
  _trackDeepStats() {
    const r = this.run; if (!r || !r.deepMode || !r.deepStats || !r.pouch) return;
    const ds = r.deepStats;
    ds.maxTotal = Math.max(ds.maxTotal, E.pouchTotal(r.pouch));
    for (const [id, n] of Object.entries(r.pouch)) {
      if (n <= 0) continue;
      const rar = POUCH_RARITY[id];
      if (rar === "희귀") ds.raresSeen.add(id);
      else if (rar === "전설") ds.legendsSeen.add(id);
    }
  }
  // 보스 클리어 시점의 주머니 조건 판정 → 심화 업적 플래그 세팅(deepStats 게이팅).
  //  finalScore 는 미확정이라 gem 전공(점수3만)은 현재 런 누적점수(r.score) 로 근사 판정.
  _markDeepBossAchievements() {
    const r = this.run; if (!r || !r.deepMode || !r.deepStats || !r.pouch) return;
    const ds = r.deepStats; const total = E.pouchTotal(r.pouch);
    if (total > 0 && total <= 85) ds.compress85BossClear = true;
    const tags = E.pouchTagCounts(r.pouch);
    // [HIGH-1] 체리/보석 전공 — "체리"/"보석" 태그는 카탈로그에 없음(실태그=생명/점수) → 심볼 계열 개수로 판정.
    //  체리 계열=🍒cherry+🍑cherry_ripe, 보석 계열=💎gem+💠gem_cut. 50%는 TAG_MAX_RATIO 0.60 내 달성 가능.
    const cherries = (r.pouch.cherry || 0) + (r.pouch.cherry_ripe || 0);
    if (total > 0 && cherries / total >= 0.50) ds.cherry50BossClear = true;
    const gems = (r.pouch.gem || 0) + (r.pouch.gem_cut || 0);
    if (total > 0 && gems / total >= 0.50 && r.score >= 30000) ds.gem50Score30kBoss = true;
    // 해골 비중: skull/skull_black 개수 합(태그 대신 심볼 직접·정합).
    const skulls = (r.pouch.skull || 0) + (r.pouch.skull_black || 0);
    if (total > 0 && skulls / total >= 0.40) ds.skull40BossClear = true;
    if (skulls === 0) ds.skull0BossClear = true;
    if ((r.pouch.crown || 0) >= 2) ds.crown2BossClear = true;
    // 완벽한 균형: 모든 태그 비중이 20% 이하(태그가 하나라도 존재할 때만).
    const tvals = Object.values(tags);
    if (total > 0 && tvals.length && tvals.every((c) => c / total <= 0.20)) ds.balanceBossClear = true;
  }
  // 낡은파쇄기(sr_shredder) — 주머니에서 가치(exp+score) 최저 실심볼 N개 감소. empty/random·최소종류 방어.
  //  총량 하한(TOTAL_MIN)·최소 종류(MIN_KINDS) 아래로는 안 깎음(주머니 규칙 붕괴 방지).
  _autoShred(n) {
    const r = this.run; if (!r.pouch) return;
    const bounds = E.repairBounds(this._repairState());
    let shredded = 0;
    for (let k = 0; k < n; k++) {
      const held = Object.entries(r.pouch).filter(([, c]) => c > 0);
      if (E.pouchTotal(r.pouch) <= bounds.totalMin) break;   // 하한 도달 시 중단
      // 최저가치 실심볼(빈칸/랜덤칸 제외) — 값 동률이면 개수 많은 쪽 우선(안전).
      const val = (id) => { const s = SYM_BY_ID[id]; return s ? (s.exp + s.score) : 0; };
      const cands = held.filter(([id]) => id !== "empty" && id !== "random");
      if (!cands.length) break;
      // 이 심볼을 -1 해도 최소종류 유지되는지 확인(마지막 1개면 종류 감소 → 종류 하한 방어)
      cands.sort((a, b) => (val(a[0]) - val(b[0])) || (b[1] - a[1]));
      let target = null;
      for (const [id, c] of cands) {
        const kindsAfter = Object.entries(r.pouch).filter(([kid, kc]) => kc > 0 && (kid !== id || c > 1)).length;
        if (kindsAfter >= DEEP.MIN_KINDS) { target = id; break; }
      }
      if (!target) break;
      r.pouch[target] -= 1; if (r.pouch[target] <= 0) delete r.pouch[target];
      shredded++;
    }
    if (shredded > 0) this.toast(`🪓 낡은파쇄기 — 최저가치 심볼 ${shredded}개 제거`);
  }
  // 심볼복사판(sr_copier) — 보스 클리어 시 '가장 많은 태그' 계열 심볼 +N(총량 상한 방어).
  _bossCopy(n) {
    const r = this.run; if (!r.pouch) return;
    const tag = E.mostCommonTag(r.pouch); if (!tag) return;
    // 그 태그를 가진 보유 심볼 중 개수 최다(대표) 1종 선택.
    const held = Object.entries(r.pouch).filter(([, c]) => c > 0);
    const tagged = held.filter(([id]) => (SYM_BY_ID[id]?.tags || []).includes(tag)).sort((a, b) => b[1] - a[1]);
    if (!tagged.length) return;
    const id = tagged[0][0];
    const bounds = E.repairBounds(this._repairState());
    let added = 0;
    for (let k = 0; k < n; k++) { if (E.pouchTotal(r.pouch) >= bounds.totalMax) break; r.pouch[id] = (r.pouch[id] || 0) + 1; added++; }
    if (added > 0) { const s = SYM_BY_ID[id]; this.toast(`📑 심볼복사판 — ${s ? s.e + s.n : id} +${added} (최다 #${tag})`); }
  }

  reel() { return this.run.device === "dev_subreel" ? 6 : 5; }

  // ── 굴림 분기(심화모드=주머니, 일반모드=기존 rollRaw) ──
  //  ★일반모드는 rollRaw 원본 경로 그대로(무회귀). 심화모드는 pouch로만 추출.
  //   배치 B: 심화모드에서 mods 의 symbolWeightMul/weightAdd/rareWeightMul 을 bias 로 변환·전달.
  //   bias는 영구 perk 기반(buildMods 경유) — NEXTSPIN 아이템(applyItemMods 경유) 은 NEXTSPIN 배지가 커버.
  //   evaluate 이후 로직/mods 전달은 두 모드 동일 → effect(증강/유물)는 심화모드에서도 살아있음.
  _roll(mods, seedActive) {
    const r = this.run;
    if (r.deepMode) {
      // bias 조립: symbolWeightMul/weightAdd/rareWeightMul → 각 선택적 필드(빈 객체는 undefined 처리).
      let symWtMul = mods.symbolWeightMul && Object.keys(mods.symbolWeightMul).length > 0
        ? { ...mods.symbolWeightMul } : {};
      // §9.0 J1: 리치 달성 다음 스핀 — 해당 태그 심볼 출현 bias ×1.5(deepPity 선례 구조 재사용, 1스핀 소진).
      if (r._reachBias && r._reachBias.spinsLeft > 0) {
        const jt = r._reachBias.tag;
        // JACKPOT_TAG 맵에서 해당 태그를 가진 심볼 목록을 구해 각 ×1.5 배율
        for (const [id, tag] of Object.entries(JACKPOT_TAG || {})) {
          if (tag === jt) symWtMul[id] = (symWtMul[id] || 1) * 1.5;
        }
        r._reachBias.spinsLeft -= 1;
        if (r._reachBias.spinsLeft <= 0) r._reachBias = null;
      }
      const hasMul = Object.keys(symWtMul).length > 0;
      const hasAdd = mods.weightAdd && Object.keys(mods.weightAdd).length > 0;
      const hasRare = mods.rareWeightMul != null && mods.rareWeightMul !== 1;
      const bias = (hasMul || hasAdd || hasRare) ? {
        mul: hasMul ? symWtMul : undefined,
        add: hasAdd ? mods.weightAdd : undefined,
        rareMul: hasRare ? mods.rareWeightMul : undefined,
      } : undefined;
      const cells = E.rollFromPouch(this.rng, r.pouch, this.reel(), bias);
      // §9.2 J3: 재도전릴(RETRY_REEL) — 리치 다음 스핀에 무작위 1칸 재굴림
      if (r._retryReelPending) {
        r._retryReelPending = false;
        const rerollIdx = this.rng.n(cells.length);
        const replacement = E.rollFromPouch(this.rng, r.pouch, this.reel(), bias);
        cells[rerollIdx] = replacement[rerollIdx];
      }
      return cells;
    }
    return E.rollRaw(this.rng, mods, this.reel(), seedActive);
  }
  // Phase 4 씨앗/새싹 성장 — 심화모드 전용(일반경로 rollRaw 무수정). r.growNext 있으면 raw 무작위 1칸을
  //  성장 심볼로 치환하고 growNext 소진. "ANY"=기본심볼 무작위, "HIGH"=체리/책/별. rollFromPouch 에
  //  seedActive 인자가 없어(설계 함정) game.js 후처리로 배선. 실제 굴림 커밋 시점(spin/oracle)에만 소비.
  _growNextRoll(raw) {
    const r = this.run;
    if (!r.deepMode || !r.growNext || !raw || !raw.length) return raw;
    const pool = r.growNext === "HIGH" ? ["cherry", "book", "star"]
               : ["cherry", "book", "star", "gem", "coin"];
    const gid = this.rng.pick(pool); const gsym = SYM_BY_ID[gid];
    if (gsym) raw[this.rng.n(raw.length)] = { sym: gsym, tag: "🌱→" };
    r.growNext = null;
    return raw;
  }
  // 배치F P2: 신규심볼 등장 보장(pity) — growNext 선례 그대로(rollFromPouch 무수정·game.js 후처리 배선). 심화 전용.
  //  add/upgrade 직후 설정된 r.deepPity 를 첫 fresh 굴림에서 소진: 5칸에 이미 있으면 자연 등장 소진,
  //  없으면 무작위 1칸 교체 후 소진(스핀당 최대 1칸=단일 치환). spinsLeft 는 방어용 안전망(정상 플로우 미도달).
  //  ★확률 순수성의 의도적 완화(스펙 승인) — lockedNext(예언) 재사용 경로는 growNext 처럼 미통과(중복 방지).
  _pityRoll(raw) {
    const r = this.run;
    if (!r.deepMode || !r.deepPity || !raw || !raw.length) return raw;
    const pid = r.deepPity.id; const psym = SYM_BY_ID[pid];
    if (!psym) { r.deepPity = null; return raw; }                      // 설정 가드 통과분이라 실질 미도달(방어)
    r.deepPity.spinsLeft -= 1;
    if (raw.some((c) => c.sym && c.sym.id === pid)) { r.deepPity = null; return raw; }   // 자연 등장 → 소진
    if (r.deepPity.spinsLeft < 0) { r.deepPity = null; return raw; }   // 만료 안전망
    raw[this.rng.n(raw.length)] = { sym: psym, tag: "✨→" };           // 무작위 1칸 교체 후 소진
    r.deepPity = null;
    return raw;
  }
  // 심화모드 압축 패널티(요구치 배수) — 일반모드는 1(무영향).
  //  총량 기반 압축 패널티 × 상점 '덱 압축' 누적 요구율(+5%씩) × 심볼증강 패널티 완화.
  //  [HIGH-3①] 압축계열(안전압축/심볼압축/압축전공)의 penaltyMul 은 초과분(base-1)에만 적용:
  //   mul = 1 + (base-1)×penaltyMul, 하한 1.0 클램프 — base 통곱이면 요구치 반토막(통할인) 오버밸런스.
  //   확장계열(확장빌드/확장가방)은 skullPenaltyMul 로 분리 배선(_mods 심화블록) — 이 경로에서 제거됨.
  //  전설수집(sp_legend_collect) 보스 요구 +20% 는 보스 스테이지에서만 곱.
  //  [MED-3] 전설봉인함(legendSeal) — 보스 요구 '증가분'(bossQuotaMul>1)을 25% 감쇄.
  //  [§11 레버①] EARLY_QUOTA — stage 1~4 요구경험치 완화 램프(5+=1.0 무배율). 전부 곱셈 체인이라
  //   위 압축/저주/보스 배율들과 순서 무관(교환법칙). 최상단 `if (!r.deepMode) return 1` 가드로 일반모드 무접촉.
  _deepPenalty() {
    const r = this.run; if (!r.deepMode) return 1;
    const base = E.compressionPenalty(E.pouchTotal(r.pouch)) * (1 + (r.deepCompressExtra || 0));
    const sp = this._symMods();
    let mul = sp ? Math.max(1, 1 + (base - 1) * sp.penaltyMul) : base;
    if (sp && r.boss && sp.bossQuotaMul !== 1) {
      let bq = sp.bossQuotaMul;
      if (sp.legendSeal && bq > 1) bq = 1 + (bq - 1) * 0.75;
      mul *= bq;
    }
    mul *= (DEEP.EARLY_QUOTA[r.stage] ?? 1.0);
    return mul;
  }
  // 심화모드 현재 정비 상태(엔진 applyShopService 용) — pouch + 확장/압축/태그 델타 묶음.
  //  Phase 5: 심볼증강(확장가방 상한+20·확장전공 상한160·압축전공 하한70)의 bound 델타/오버라이드를 합산.
  //   오버라이드(boundMax/boundMin)는 절대값 → DEEP 기본 대비 델타로 환산해 합류(엔진 repairBounds 재사용).
  _repairState() {
    const r = this.run;
    const sp = this._symMods();
    let maxDelta = r.deepTotalMaxDelta || 0;
    let minDelta = r.deepTotalMinDelta || 0;
    if (sp) {
      maxDelta += sp.boundMaxDelta;
      minDelta += sp.boundMinDelta;
      if (sp.boundMax) maxDelta = Math.max(maxDelta, sp.boundMax - DEEP.DECK_MAX);   // 확장전공 절대상한→델타
      if (sp.boundMin) minDelta = Math.min(minDelta, sp.boundMin - DEEP.DECK_MIN);   // 압축전공 절대하한→델타(더 낮게)
    }
    const tagMaxRatio = (sp && sp.tagCap) ? Math.max(DEEP.TAG_MAX_RATIO, sp.tagCap) : DEEP.TAG_MAX_RATIO;   // 단일전공 완화
    return { pouch: r.pouch, totalMaxDelta: maxDelta, totalMinDelta: minDelta,
             compressExtra: r.deepCompressExtra || 0, tagBuff: r.deepTagBuff || {}, tagMaxRatio,
             curses: r.curses || [] };   // 배치 A Step 5: curseCleanse 서비스용
  }

  // ── Phase 4: 특수심볼 메타 신호 소비 (심화모드 전용·evaluate 반환 res 기반·additive) ──────────
  //  ★일반모드는 신규 special 셀이 없어 res 의 신호가 전부 falsy → 이 메서드는 spin()에서 r.deepMode 게이팅.
  //  즉시효과(표적/촉매/럭키7/검은초 등)는 이미 evaluate 에서 exp/score/coins 에 반영됨. 여기선
  //  "다음 스핀 상태 / 상점·보스 플래그 / 저주게이지" 만 배선한다(연동 대상이 web/slot 에 실제 존재하는 것만).
  _applyDeepSpinMeta(res) {
    const r = this.run;
    // 🌱씨앗/🌿새싹 — 다음 스핀 성장 예약(_growNextRoll 이 다음 굴림 때 소비). HIGH(새싹) 우선.
    if (res.growNext) r.growNext = res.growNext;
    // ⏰알람 / ⚙톱니(근사) — 다음 스핀 EXP +10%(pendingNextExpMul 누적곱, spin()이 자동 소비).
    if (res.alarmNext) { r.pendingNextExpMul *= 1.1; this.toast("⏰ 알람 — 다음 스핀 EXP +10%"); }
    if (res.gearNext) { r.pendingNextExpMul *= 1.1; this.toast("⚙ 톱니 — 다음 스핀 EXP +10% (근사)"); }
    // ⏳모래시계 — 이번 스핀의 30%를 다음 스핀으로 이월(carryExp 는 evaluate 산출, 다음 spin 게이지에 가산).
    if (res.carryExp > 0) { r.carryOverExp += res.carryExp; this.toast(`⏳ 모래시계 — EXP ${res.carryExp} 다음 스핀 이월`); }
    // 🧾영수증 / 🎟쿠폰 / 🛒장바구니 — 다음 상점 1회 적용 플래그(_freshShop 에서 소진).
    if (res.receiptNext) { r.deepShopDiscount = true; this.toast("🧾 영수증 — 다음 상점 전체 -10%"); }
    if (res.couponNext) { r.deepShopCoupon = true; this.toast("🎟 쿠폰 — 다음 상점 상품 1개 할인"); }
    if (res.cartNext) { r.deepShopSlotBonus = Math.min(2, (r.deepShopSlotBonus || 0) + 1); this.toast("🛒 장바구니 — 다음 상점 상품칸 +1"); }
    // 🛡방패 / 📋시험지 — 다음 보스 스핀 1회용(spin() applyBossExp 게이팅에서 소진).
    if (res.shieldNext) { r.bossShield = true; this.toast("🛡 방패 — 다음 보스 패널티 1회 방어"); }
    if (res.exemptNext) { r.bossExempt = true; this.toast("📋 시험지 — 다음 보스 감점룰 1회 무시"); }
    // 🔋배터리(근사) — web/slot 일반장치는 '쿨다운 턴' 개념이 없음(usedCmds 스테이지 리셋뿐). A9 승천 쿨다운
    //  (_devCdUntil)만 존재하나 심화모드는 asc=0 강제라 미작동 → 근사: 이번 스테이지 능동장치 사용기록 1건 해제
    //  (장치 재사용 1회 허용). 해제할 게 없으면 무효(정직히 근사·과대약속 금지).
    //  [LOW-2] pop() 대신 장치 cmd 항목만 골라 제거 — 특수스핀 기록(FOCUS 등)이 풀리던 버그 수정.
    if (res.batteryNext) { const popped = this._releaseDeviceUse(); if (popped) this.toast(`🔋 배터리 — 장치 [${popped}] 재사용 가능 (근사)`); }
    // ── Phase 5 부활: 🖍형광펜 / 📚복습책 (심화 증강노드가 생겨 실효과) ──
    //  🖍형광펜(AUGCHANCE) = aug_catalyst 동형: 다음 AUGLEVEL 노드 발생 확률 +15%(_augLevelBoost).
    if (res.augChanceNext) { r._augLevelBoost = (r._augLevelBoost || 0) + 0.15; this.toast("🖍 형광펜 — 다음 증강 레벨업 확률 +15%"); }
    //  📚복습책(AUGLEVEL) = study_note 동형: 보유 증강 중 최저레벨 1개 즉시 레벨업(없으면 무효).
    if (res.augLevelNext) {
      const ids = this._levelableHeld();
      if (ids.length) { ids.sort((a, b) => (r.perkLevels[a] || 1) - (r.perkLevels[b] || 1)); const t = ids[0]; r.perkLevels[t] = Math.min(3, (r.perkLevels[t] || 1) + 1); const a = this._augInfo(t); this.toast(`📚 복습책 — ${a.e}${a.n} Lv.${r.perkLevels[t]} 강화!`); }
      else this.toast("📚 복습책 — 강화할 증강이 없어요");
    }
    // 🧰 정비키트(KIT·근사) — web/slot 엔 '장치 게이지'가 실존하지 않음(정찰 확인). 근사 재정의:
    //  ① 이번 스테이지 능동장치 사용기록 1건 해제(재사용 1회 허용, battery 동형) 또는
    //  ② 해제할 게 없으면 다음 상점 정비소 등장(deepShopSlotBonus)로 정비 접근성 보강. 과대약속 금지·정직히 근사.
    if (res.kitNext) {
      // [LOW-2] 장치 cmd 기록만 해제(특수스핀 보존) — 해제 대상 없으면 상점칸 +1 폴백.
      const popped = this._releaseDeviceUse();
      if (popped) this.toast(`🧰 정비키트 — 장치 [${popped}] 재사용 가능 (근사)`);
      else { r.deepShopSlotBonus = Math.min(2, (r.deepShopSlotBonus || 0) + 1); this.toast("🧰 정비키트 — 다음 상점 상품칸 +1 (근사)"); }
    }
    // 🧩세트조각(근사) — 심화모드엔 증강 오퍼(세트조각 주입 훅)가 없음. 근사: 이번 스핀 세트 보너스가 있었다면
    //  소량 코인 환급으로 "세트 관련 보상" 느낌만(설계상 근사). 세트 미형성이면 무효.
    if (res.setFrag && (res.setIds && res.setIds.length)) { r.coins += 2; this.toast("🧩 세트조각 — 세트 보상 +2🪙 (근사)"); }
    // 🩸피방울 / 🧿저주눈 — 저주게이지(web/slot 은 별도 '저주게이지' 없음 → 불운게이지 근사) 가산.
    if (res.curseGaugeUp > 0) r.unluckyGauge = Math.min(C.UNLUCKY_MAX, r.unluckyGauge + res.curseGaugeUp);
    // 🧿저주눈 — "다음 보상등급 상승"은 심화 POUCH 보상에 등급개념이 약함 → 근사: 다음 주머니 보상 후보 +1.
    if (res.curseEyeNext) { r.deepRewardBonus = Math.min(2, (r.deepRewardBonus || 0) + 1); this.toast("🧿 저주눈 — 다음 주머니 보상 후보 +1 · 불운+1"); }
    // ── V3P4: instant 심볼 소비(등장했으면 덱에서 1개 제거·0 클램프) ──
    //  효과는 engine.js evaluate 에서 이미 반영됨. 여기선 덱 제거만(applySymbolReward remove 재사용).
    if (r.pouch) {
      const instants = {
        hasBandage: "bandage", hasKnot: "knot", hasEnergyPack: "energypack",
        hasFakeCrown: "fake_crown_sym", hasEvoCore: "evo_core",
      };
      for (const [key, symId] of Object.entries(instants)) {
        if (res[key] && (r.pouch[symId] || 0) > 0) {
          r.pouch = E.applySymbolReward(r.pouch, { type: "remove", id: symId, n: 1 });
          this._checkArchetype();
          // 제거 토스트는 evaluate의 notes에 이미 포함(중복 생략)
        }
      }
    }
    // ── V3P4: fuse 발동 훅 (상점 진입·상점 무료·스핀전 확인은 각 훅에서) ──
    //  💳검은카드(BLACKCARD·fuse): 이번 스핀 등장 시 불운게이지 +1 표시(무료 상점은 _openShop 훅에서 소비).
    if (res.hasBlackCard && r.pouch && (r.pouch.black_card || 0) > 0) {
      r.unluckyGauge = Math.min(C.UNLUCKY_MAX, (r.unluckyGauge || 0) + 1);
      this.toast("💳 검은카드 — 불운게이지 +1 (다음 상점 1개 무료 예약)");
    }
    // 🔮수정구(CRYSTAL·fuse): 스핀 중 등장 시 다음 POUCH 보상 후보 +1 예약(deepRewardBonus).
    if (res.hasCrystal && r.pouch && (r.pouch.crystal || 0) > 0) {
      r.deepCrystalPending = (r.deepCrystalPending || 0) + 1;
      r.pouch = E.applySymbolReward(r.pouch, { type: "remove", id: "crystal", n: 1 });
      this._checkArchetype();
      this.toast("🔮 수정구 — 다음 주머니 보상 후보 +1 (소모)");
    }
    // 🧷안전핀노트(SAFEPIN·fuse): AUGLEVEL 미발생 시 누적은 _clearStage AUGLEVEL 분기에서 처리.
    //  여기선 스핀 등장 마킹만(safepin이 덱에 있고 스핀에 등장했음을 r._safePinActive로 표시).
    if (res.hasSafePin && r.pouch && (r.pouch.safepin || 0) > 0) {
      r._safePinActive = true;  // 이번 스테이지 safepin 등장했음 — AUGLEVEL 실패 시 +1%p 누적 대상
    }
    // 🧲임시와일드(TEMPWILD·fuse): 이번 스핀 wild_temp 주입이 실행됐으면 소비(덱 1개 제거).
    //  spin() 프리훅에서 이미 armItems에 wild_temp 주입됨 — hasTempWild는 evaluate 결과 기반.
    if (res.hasTempWild && r.pouch && (r.pouch.temp_wild || 0) > 0) {
      r.pouch = E.applySymbolReward(r.pouch, { type: "remove", id: "temp_wild", n: 1 });
      this._checkArchetype();
      this.toast("🧲 임시와일드 — 와일드 사용 완료 (소모)");
    }
    // 🌀운명의소용돌이(FATEVORTEX·fuse): 2번 굴림은 spin() 에서 처리됨. 여기선 소비만.
    //  spin() 에서 _fateVortexUsed 이 이번 stage 로 설정됐으면 1개 소비(스테이지당 1회 = 1 제거).
    if (r._fateVortexUsed === r.stage && r.pouch && (r.pouch.fate_vortex || 0) > 0 && !r._fateVortexConsumed) {
      r.pouch = E.applySymbolReward(r.pouch, { type: "remove", id: "fate_vortex", n: 1 });
      this._checkArchetype();
      r._fateVortexConsumed = r.stage;  // 이번 스테이지 소비 완료(중복 소비 방지)
    }
    // ⛓족쇄(SHACKLE): permanent harmful — 소비/fuse 없음(런 내내 효과 유지). _mods() 에서 보스 효과 반영.
  }

  // [LOW-2] usedCmds 에서 마지막 '장치 발동 기록' 1건만 제거(특수스핀/도박꾼 기록 보존). 제거된 cmd 반환·없으면 null.
  _releaseDeviceUse() {
    const r = this.run; if (!r || !r.usedCmds) return null;
    for (let i = r.usedCmds.length - 1; i >= 0; i--) {
      if (DEVICE_CMDS.has(r.usedCmds[i])) return r.usedCmds.splice(i, 1)[0];
    }
    return null;
  }

  // 특수명령 코인 비용(현재 보스 상태 기준) — UI 버튼/도움말 라벨용. mode="N"=0.
  cmdCost(mode) { return mode && mode !== "N" ? E.cmdCoinCost(mode, !!this.run?.boss) : 0; }

  // ── 스핀 (mode: N | FOCUS | ALLIN | PRAY | LAST) ──
  spin(mode = "N") {
    const r = this.run; if (r.phase !== PHASE.SPIN) return this.state();
    const special = mode !== "N";
    const boss = !!r.boss;
    const baseCost = special ? E.cmdCoinCost(mode, boss) : 0;
    // 런 단위 종류별 첫 1회 무료(코인 0이어도 발동, 실제 발동 성공 시에만 소진)
    const isFree = special && !(r.cmdFreeUsed && r.cmdFreeUsed[mode]);
    const cmdCost = isFree ? 0 : baseCost;
    if (special) {
      if (mode === "LAST" && r.spinIndex !== r.spins - 1) { this.toast("⏰ 최후는 마지막 스핀에서만 쓸 수 있어요"); return this.state(); }
      if (r.usedCmds.includes(mode)) { this.toast(`${SPECIAL_LABEL[mode] || "특수스핀"} 은(는) 이번 스테이지에 이미 썼어요 (종류별 스테이지당 1회)`); return this.state(); }
      if (cmdCost > 0 && r.coins < cmdCost) { this.toast(`${SPECIAL_LABEL[mode] || "특수스핀"} 발동에 코인 ${cmdCost}🪙 필요 (보유 ${r.coins}🪙)`); return this.state(); }
    }
    // V3P4: 🧲임시와일드(temp_wild·fuse) — 덱에 있으면 이번 스핀에 wild_temp cellOp 자동 주입.
    //  소비(덱 제거)는 _applyDeepSpinMeta(hasTempWild 신호) 에서. armItems 는 이번 스핀만(spin() 후 리셋).
    if (r.deepMode && r.pouch && (r.pouch.temp_wild || 0) > 0) { r.armItems = [...r.armItems, "wild_temp"]; }
    const cellOps = r.armItems.filter((id) => ["eraser_old", "eraser_fine", "eraser_god", "wild_temp", "fake_crown"].includes(id));
    const lever = r.armItems.filter((id) => !cellOps.includes(id));
    let mods = this._mods(lever);
    if (mode === "FOCUS") mods = { ...mods, rareWeightMul: mods.rareWeightMul * 0.5 };
    if (r.pendingNextExpMul !== 1) { mods = { ...mods, expMul: mods.expMul * r.pendingNextExpMul }; r.pendingNextExpMul = 1; }

    // 굴림 (예언 확정 시 lockedNext 재사용). 심화모드 씨앗/새싹 성장은 예언에서 이미 반영됐으므로
    //  fresh 굴림 경로에서만 _growNextRoll 로 소비(lockedNext 재사용은 중복 성장 방지).
    let raw;
    if (r.lockedNext) { raw = r.lockedNext.map((c) => ({ ...c })); }
    else { raw = this._roll(mods, r.seedNext); raw = this._growNextRoll(raw); raw = this._pityRoll(raw); }   // 배치F P2: pity 는 growNext 뒤 체이닝(fresh 굴림만)
    r.lockedNext = null;
    if (cellOps.length) E.applyCellOps(raw, cellOps, this.rng);
    let res = E.evaluate(this.rng, raw, mods, r.spinIndex, r.spins, r.flameNext);
    // V3P4: 🌀운명의소용돌이(fate_vortex·fuse) — 스테이지당 1회. 덱에 있고 미사용 스테이지면
    //  2번째 굴림을 수행해 EXP 더 높은 결과를 채택. 소비(덱 제거)는 _applyDeepSpinMeta 에서.
    if (r.deepMode && r.pouch && (r.pouch.fate_vortex || 0) > 0 && !r.lockedNext
        && r._fateVortexUsed !== r.stage) {
      const raw2 = this._roll(mods, false);
      const res2 = E.evaluate(this.rng, raw2, mods, r.spinIndex, r.spins, r.flameNext);
      if (res2.exp > res.exp) { res = res2; res.notes = [...res.notes, "🌀 운명의소용돌이 — 더 좋은 결과 선택"]; }
      else res.notes = [...res.notes, "🌀 운명의소용돌이 — 원래 결과 유지"];
      r._fateVortexUsed = r.stage;  // 이번 스테이지 사용 완료
    }

    let exp = res.exp;
    if (r.boss) {
      // Phase 4 🛡방패/📋시험지 — 심화모드 전용(일반경로 무영향). 방패=보스 패널티(exp 감소) 방어(보너스는 유지),
      //  시험지=감점 보스룰(strict/luck)만 이번 스핀 무시. 소비는 실제 발동(감점 상황)에만.
      if (r.deepMode && r.bossExempt && (r.boss.id === "strict" || r.boss.id === "luck")) {
        r.bossExempt = false; res.notes.push("📋 시험지 — 보스 감점룰 무시");
      } else {
        const b = E.applyBossExp(exp, r.boss, r.spinIndex, r.spins, res);
        if (r.deepMode && r.bossShield && b.exp < exp) { r.bossShield = false; res.notes.push(`🛡 방패 — 보스 패널티 방어`); }
        else { exp = b.exp; if (b.note) res.notes.push(b.note); }
      }
    }
    if (special) {
      const sp = E.applySpecialSpin(mode, exp, { quotaVal: r.quota, spins: r.spins, skulls: res.skulls, rng: this.rng });
      exp = Math.floor(sp.exp); if (sp.note) res.notes.push(sp.note);
      r.usedCmds.push(mode);
      if (isFree && r.cmdFreeUsed) r.cmdFreeUsed[mode] = true;   // 무료로 실제 발동 성공 시에만 무료권 소진(코인부족 차단은 위에서 이미 return → 미소진)
    }
    if (r.device === "dev_safe") { const floor = Math.floor(r.quota / r.spins * 0.35); if (exp < floor) { exp = floor; res.notes.push("🦺 안전벨트 하한"); } }

    // 반영
    r.armItems = []; r.flameNext = res.hasFlame; r.seedNext = res.seedNext;
    r.lastCells = res.cells.map((c) => ({ ...c })); r.lastResult = res; r.lastExpApplied = exp;
    r.lastMods = mods; r.lastSpinIndex = r.spinIndex;
    r.spinIndex += 1;
    r.stageExp += exp; r.score += res.score; r.coins += res.coins - cmdCost;   // 특수명령 코인 즉시차감(무환불)
    // ── Phase 5 심볼유물 per-spin 점수 보너스(심화 전용·additive) — 압축계약서/균형저울/희귀표본상자 ──
    //  ★일반모드 격리: r.deepMode 게이팅 + symPerkMods 는 심볼퍽만 읽어 일반 런은 sp 전부 0(무영향).
    //   이 블록은 res.score(이미 r.score 에 가산됨)를 기준으로 추가 점수만 더한다(evaluate/일반경로 무접촉).
    if (r.deepMode && res.score > 0) {
      const sp = this._symMods();
      if (sp) {
        // 📜 압축계약서(compressScorePct): 총량 54↓(★배치 I 재스케일·symPerkMods 에서 총량조건 판정) → 이번 스핀 점수 +pct.
        if (sp.compressScorePct > 0) { const b = Math.floor(res.score * sp.compressScorePct); if (b > 0) { r.score += b; res.notes.push(`📜 압축계약서 점수 +${b}`); } }
        // ⚖️ 균형저울(balanceScore): 어떤 태그도 60% 근접(≥50%)하지 않으면(=균형) 이번 스핀 점수 +pct. 주머니 태그분포 기준.
        if (sp.balanceScore > 0) {
          const total = E.pouchTotal(r.pouch); let peak = 0;
          if (total > 0) { const byTag = {}; for (const [id, n] of Object.entries(r.pouch)) { if (n <= 0) continue; for (const t of (SYM_BY_ID[id]?.tags || [])) byTag[t] = (byTag[t] || 0) + n; } for (const t in byTag) peak = Math.max(peak, byTag[t] / total); }
          if (peak < 0.50) { const b = Math.floor(res.score * sp.balanceScore); if (b > 0) { r.score += b; res.notes.push(`⚖️ 균형저울 점수 +${b}`); } }
        }
      }
    }
    // 🧰 희귀표본상자(rareFirstScore) — 희귀등급 심볼을 이번 런에서 처음 발견한 스핀에 1회 점수 +N(res.score 무관).
    //  ★seenSyms 갱신(아래) 전에 판정 — 이번 셀의 희귀등급 심볼이 기존 발견목록에 없으면 신규.
    if (r.deepMode) {
      const sp = this._symMods();
      if (sp && sp.rareFirstScore > 0) {
        const newRare = res.cells.some((c) => c.sym.id !== "empty" && (POUCH_RARITY[c.sym.id] || "기본") === "희귀" && !r.seenSyms.has(c.sym.id));
        if (newRare) { r.score += sp.rareFirstScore; res.notes.push(`🧰 희귀표본 첫 발견 점수 +${sp.rareFirstScore}`); }
      }
    }
    // ⏳ 모래시계 — 지난 스핀에서 이월된 EXP를 이번 스핀 게이지에 1회 가산(클리어 판정 전). 소진 후 0.
    if (r.deepMode && r.carryOverExp) { const co = r.carryOverExp; r.carryOverExp = 0; r.stageExp += co; res.notes.push(`⏳ 이월 EXP +${co}`); }
    if (exp > r.stats.bestSpin) r.stats.bestSpin = exp;
    r.stats.cherry += res.counts.cherry || 0; r.stats.crown += (res.cells.filter((c) => c.sym.id === "crown").length);
    res.cells.forEach((c) => { if (c.sym.id !== "empty") r.seenSyms.add(c.sym.id); });   // 도감 심볼 발견
    // Phase 5: 심화 업적 — 스핀에서 등장한 희귀/전설 등급 심볼 발견 집계(수집가/연구자). deepStats 게이팅(격리).
    if (r.deepMode && r.deepStats) { for (const c of res.cells) { const rar = POUCH_RARITY[c.sym.id]; if (rar === "희귀") r.deepStats.raresSeen.add(c.sym.id); else if (rar === "전설") r.deepStats.legendsSeen.add(c.sym.id); } }
    if (res.jackpotSym) r.stats.jackpots += 1;
    if (res.skulls > 0) r.unluckyGauge = Math.min(C.UNLUCKY_MAX, r.unluckyGauge + 1);
    else r.unluckyGauge = Math.max(0, r.unluckyGauge - 1);
    // Phase 4 특수심볼 메타 신호 소비(심화모드 전용·additive). 다음스핀/상점/저주게이지 등 배선.
    //  ★불운게이지 skull 조정(위) 이후에 저주게이지(피방울/저주눈)를 additive 로 가산.
    if (r.deepMode) this._applyDeepSpinMeta(res);
    // 테마빌드 추적(이 스핀의 결과로 누적/이벤트 갱신). mode=기도 성공 여부는 note 로 판별.
    this._trackSpin(res, mode, r.spinIndex - 1 === r.spins - 1);

    // 배치F P6: 퍼펙트 드로우 — 5칸 전부 동일 계열(POUCH_FAMILY, 빈칸 불성립·random 은 실심볼 착지라 자연 배제).
    //  스테이지당 1회(perfectDrawStage 마킹, stage 단조증가=자기 리셋). ★클리어 판정(_clearStage=stage+1) 전에 현재 stage 로 판정.
    let pdBanner = "";
    if (r.deepMode && r.perfectDrawStage !== r.stage && res.cells.length
        && res.cells.every((c) => c.sym && c.sym.id !== "empty")) {
      const famBase = (id) => E.UPG_PARENT[id] || id;
      const f0 = famBase(res.cells[0].sym.id);
      if (res.cells.every((c) => famBase(c.sym.id) === f0)) {
        r.coins += 1; r.perfectDrawStage = r.stage;
        const fs = SYM_BY_ID[f0];
        pdBanner = `🎯 퍼펙트 드로우! 5칸 모두 ${fs ? fs.e + fs.n : f0} 계열 — 코인 +1`;
        res.notes.push(pdBanner);
      }
    }

    // §9.0 J1: 잭팟 태그 단계 보상 소비(심화 전용). evaluate 가 반환한 신호를 여기서 배선.
    //  보상(EXP/점수)은 evaluate 에서 이미 반영됨 → 여기서는 런 플래그·bias 세팅 + 배너만.
    //  스테이지 1회 제한: r._reachBias/r._jackpotPrismPending 은 다음 스핀·오퍼에서 소진(단회성).
    let jtBanner = "";
    if (r.deepMode && res.jackpotStage) {
      const TAG_LABEL = { crown: "👑 왕관", seven: "7️⃣ 럭키7", coin: "🪙 코인", prism: "🌈 프리즘", curse: "💀 저주", bell: "🔔 종" };
      const tLabel = TAG_LABEL[res.jackpotTagHit] || res.jackpotTagHit || "태그";
      if (res.jackpotStage === "combo") {
        jtBanner = `🎯 ${tLabel} 콤보! — EXP+8`;
      } else if (res.jackpotStage === "reach") {
        // 다음 스핀 해당 태그 bias ×1.5 (1스핀). 이미 r._reachBias 가 있으면 덮어쓰기(최신 우선).
        r._reachBias = { tag: res.jackpotTagHit, spinsLeft: 1 };
        jtBanner = `🎯 ${tLabel} 리치! — 다음 스핀 해당 태그 ×1.5`;
      } else if (res.jackpotStage === "jackpot") {
        // 다음 POUCH 오퍼 프리즘 후보 1장 보장
        r._jackpotPrismPending = true;
        jtBanner = `🎰 ${tLabel} 잭팟!! — 다음 주머니 오퍼 프리즘 보장`;
      }
    }
    // §9.2 J3: 승격/보정 심볼 소모 처리 (심화 전용 — r.deepMode 게이팅)
    if (r.deepMode) {
      // 종소리티켓(BELL_TICKET·fuse): 종 4개 리치 → 잭팟 승격 후 제거(런 2회 제한 _bellTicketUses)
      //  bellCount=4 리치 상태 = jackpotStage==="reach" && jackpotTagHit==="bell"
      if (res.jackpotStage === "reach" && res.jackpotTagHit === "bell" && (res.bellCount || 0) >= 4) {
        const bellTicketN = r.pouch["bell_ticket"] || 0;
        const bellUses = r._bellTicketUses || 0;
        if (bellTicketN > 0 && bellUses < 2) {
          // 승격: 잭팟 보상 지급
          r.stageExp += 30; r.score += 1500; r._bellTicketUses = bellUses + 1;
          r.pouch["bell_ticket"] = bellTicketN - 1; if (r.pouch["bell_ticket"] <= 0) delete r.pouch["bell_ticket"];
          r._jackpotPrismPending = true;
          jtBanner = (jtBanner ? jtBanner + " · " : "") + `🎟 종소리티켓 — 종 4개 잭팟 승격! (런 ${r._bellTicketUses}/2)`;
        }
      }
      // 잭팟티켓(JACKPOT_TICKET·fuse): 리치 → 잭팟 승격 후 제거(런 2회 공유 카운터 _jpTicketUses)
      if (res.jackpotStage === "reach" && res.hasJpTicket) {
        const jpTicketN = r.pouch["jackpot_ticket"] || 0;
        const jpUses = r._jpTicketUses || 0;
        if (jpTicketN > 0 && jpUses < 2) {
          r.stageExp += 30; r.score += 1500; r._jpTicketUses = jpUses + 1;
          r.pouch["jackpot_ticket"] = jpTicketN - 1; if (r.pouch["jackpot_ticket"] <= 0) delete r.pouch["jackpot_ticket"];
          r._jackpotPrismPending = true;
          jtBanner = (jtBanner ? jtBanner + " · " : "") + `🎟 잭팟티켓 — 리치 → 잭팟 승격! (런 ${r._jpTicketUses}/2)`;
        }
      }
      // 리치표식(REACH_MARK): 태그 4개 리치 시 30%+feverReachFix 확률로 부족 1칸 보정(잭팟 승격), 스테이지 1회
      if (res.jackpotStage === "reach" && res.hasReachMark && !r._reachMarkUsed) {
        const baseProb = 0.30 + ((r.lastMods && r.lastMods.feverReachFix) ? r.lastMods.feverReachFix : 0);
        if (this.rng.double() < baseProb) {
          r.stageExp += 30; r.score += 1500; r._reachMarkUsed = true;
          r._jackpotPrismPending = true;
          jtBanner = (jtBanner ? jtBanner + " · " : "") + `🎯 리치표식 — 부족 1칸 보정 잭팟 승격! (확률 ${Math.round(baseProb * 100)}%)`;
        }
      }
      // 재도전릴(RETRY_REEL): 리치 시 1칸 재굴림(spinsLeft 소비 없이, 단 스테이지 1회)
      // → 효과는 lockedNext 조작이지만 evaluate 이후라 이번 스핀엔 미적용.
      //  다음 스핀에 1칸 re-roll 신호 저장(_retryReelPending). game._roll 에서 소비.
      if (res.jackpotStage === "reach" && res.hasRetryReel && !r._retryReelUsed) {
        r._retryReelPending = true; r._retryReelUsed = true;
        jtBanner = (jtBanner ? jtBanner + " · " : "") + "🔁 재도전릴 — 다음 스핀 1칸 재굴림 예약";
      }
      // 잭팟왕관(JACKPOT_CROWN): 잭팟 시 보상등급+1, 스테이지 1회
      if (res.jackpotCrownSignal && !r._jackpotCrownUsed) {
        r._jackpotCrownUsed = true;
        // 보상등급+1 = 다음 POUCH 오퍼에 추가 PRISM 후보(기존 forcePrism 플래그 활용 + 추가 신호)
        r._jackpotCrownPending = true;
      }
    }

    // §9.1 J2: 피버 게이지 충전 + 피버 진입/효과/종료 (심화 전용 — r.deepMode 게이팅).
    //  evaluate 가 반환한 feverDelta(콤보+15/리치+25/잭팟+50, J3 종 세트 추가 충전 포함)를 여기서 소비.
    //  피버 진입 시: feverSpins=FEVER_SPINS(3), 게이지 리셋. 피버 종료는 스핀마다 feverSpins 차감.
    //  피버 효과(EXP/점수)는 mods 경유가 아닌 즉석 보정(mods는 스핀 전 고정이므로 스핀 후 결산 재보정).
    //  ★일반모드는 이 블록 전체 미진입(r.deepMode 가드) → 격리.
    let feverBanner = "";
    if (r.deepMode) {
      // §9.2 J3: 축제종(BELL_FEST) 보정 후 게이지 충전에 쓰일 유효 feverDelta((A)에서 보정, (B)에서 소비).
      let feverDeltaEff = res.feverDelta || 0;
      // (A) 피버 효과 적용 — 피버 진입 판정 전에 "이전 스핀 잔여 피버" 효과 적용
      if (r.feverSpins > 0) {
        // §9.2 J3: 축제종(BELL_FEST) — 피버 중(feverSpins>0) + 이번 스핀 잭팟 태그 발동이 bell 이면
        //  해당 단계의 종 유래 보상(EXP/점수 가산분)과 feverDelta 에 ×BELL_FEST_MUL(1.5) 적용.
        //  res.jackpotTagHit==="bell" 은 jackpotStage(combo/reach/jackpot)가 실제 bell 태그로 발동했음을 뜻함.
        if (res.hasBellFest && res.jackpotTagHit === "bell" && res.jackpotStage) {
          // 단계별 종 유래 가산분 재구성(엔진 evaluate 의 코어 상수와 동일치 — cheerMul 등 별도 배수 미포함).
          let bellExpBonus = 0, bellScoreBonus = 0;
          if (res.jackpotStage === "combo") bellExpBonus = 8;
          else if (res.jackpotStage === "reach") bellScoreBonus = (res.jackpotSym ? 0 : 300) + (res.echoTriggered ? 200 : 0);
          else if (res.jackpotStage === "jackpot") { bellExpBonus = 30; bellScoreBonus = res.jackpotSym ? 0 : 1500; }
          const festMulExtra = DEEP.BELL_FEST_MUL - 1;   // 0.5
          const festExpExtra = Math.floor(bellExpBonus * festMulExtra);
          const festScoreExtra = Math.floor(bellScoreBonus * festMulExtra);
          const festFeverExtra = Math.floor(feverDeltaEff * festMulExtra);
          if (festExpExtra > 0) r.stageExp += festExpExtra;
          if (festScoreExtra > 0) r.score += festScoreExtra;
          if (festFeverExtra > 0) feverDeltaEff += festFeverExtra;
          if (festExpExtra > 0 || festScoreExtra > 0 || festFeverExtra > 0) {
            // jtBanner(잭팟 태그 계열 배너)에 병합 — feverBanner 는 "피버 N스핀 남음" 등 상태 안내용이라
            //  덮어써지면(else if(!feverBanner)) 상태 안내가 사라지므로 별도 배너 변수 사용.
            jtBanner = (jtBanner ? jtBanner + " · " : "") + `🎊 축제종 ×${fmt2(DEEP.BELL_FEST_MUL)}`;
          }
        }
        // EXP 재보정(evaluate 결산 이후이므로 additive 배율 적용: 획득 EXP × FEVER_EXP_MUL)
        //  exp 는 이미 r.stageExp 에 더해진 뒤이므로 delta 만 추가(원래 exp × (mul-1))
        const feverExpExtra = Math.floor(exp * (DEEP.FEVER_EXP_MUL - 1));
        if (feverExpExtra > 0) r.stageExp += feverExpExtra;
        // 점수 재보정(이번 스핀 점수 res.score × FEVER_SCORE_MUL, 이미 r.score 에 더해진 상태)
        const feverScoreExtra = Math.floor(res.score * (DEEP.FEVER_SCORE_MUL - 1));
        if (feverScoreExtra > 0) r.score += feverScoreExtra;
        // 피버잭팟: 피버 중 태그잭팟 = 점수 ×FEVER_JACKPOT_SCORE_MUL(×2) 추가 + 다음 POUCH 프리즘 보장 1회 추가
        if (res.jackpotStage === "jackpot" && res.jackpotTagHit) {
          const fjScoreBonus = Math.floor(res.score * (DEEP.FEVER_JACKPOT_SCORE_MUL - 1));
          if (fjScoreBonus > 0) r.score += fjScoreBonus;
          if (!r._feverJackpotPrism) r._feverJackpotPrism = true;
          jtBanner = (jtBanner ? jtBanner + " · " : "") + "🔥 피버잭팟!! — 점수×2 추가 · 오퍼 프리즘+1";
        }
        // feverReachFix: mods 필드 노출(J3 리치보정 심볼이 참조)
        if (r.lastMods) r.lastMods.feverReachFix = DEEP.FEVER_REACH_FIX;
        r.feverSpins -= 1;
        if (r.feverSpins <= 0) {
          r.feverSpins = 0;
          feverBanner = (feverBanner ? feverBanner + " · " : "") + "🔥 피버 종료!";
          if (r.lastMods) r.lastMods.feverReachFix = 0;
        } else if (!feverBanner) {
          feverBanner = `🔥 피버 ${r.feverSpins}스핀 남음`;
        }
      }
      // (B) 피버 게이지 충전(이번 스핀 feverDelta — 피버 효과 적용 후 충전, 축제종 보정 반영된 feverDeltaEff 사용)
      if (feverDeltaEff > 0) {
        r.feverGauge = (r.feverGauge || 0) + feverDeltaEff;
        // (C) 피버 진입 판정
        if (r.feverGauge >= DEEP.FEVER_MAX) {
          r.feverGauge = 0;
          r.feverSpins = DEEP.FEVER_SPINS;
          feverBanner = `🔥 피버 타임! ${DEEP.FEVER_SPINS}스핀`;
          if (r.lastMods) r.lastMods.feverReachFix = DEEP.FEVER_REACH_FIX;
        }
      }
    }

    const out = { kind: "spin", mode, cells: res.cells, preCells: res.preCells, bomb: res.bomb, bestSetId: res.bestSetId, bestCount: res.bestSetCount, setIds: res.setIds, gained: exp, score: res.score, coins: res.coins, notes: res.notes, jackpot: res.jackpotSym };
    if (special) out.banner = `${SPECIAL_LABEL[mode] || "특수스핀"} 발동! — ${E.cmdEffectDesc(mode)}${isFree ? " · 🆓첫 사용 무료" : (cmdCost > 0 ? ` · -${cmdCost}🪙` : "")}`;
    if (pdBanner) out.banner = (out.banner ? out.banner + " · " : "") + pdBanner;   // 배치F P6: special 배너와 병합(덮어쓰기 금지)
    if (jtBanner) out.banner = (out.banner ? out.banner + " · " : "") + jtBanner;  // §9.0 J1: 잭팟 태그 배너 병합
    if (feverBanner) out.banner = (out.banner ? out.banner + " · " : "") + feverBanner; // §9.1 J2: 피버 배너 병합
    if (r.stageExp >= r.quota) { if (r.spinIndex - 1 === r.spins - 1) { r.stats.lastClears += 1; r.tb.lastSpinClear = true; } if (r.stageExp === r.quota) r.stats.exactClears += 1; r._clearVia = "spin"; this._clearStage(); out.cleared = true; }
    else if (r.spinIndex >= r.spins) {
      if (r.survive) { r.survive = false; r.spins += 2; this.toast("📋 보험증서 발동 — 생존(스핀+2)"); }
      else if (this._canRecover()) { r.phase = PHASE.POST_SPIN; out.postSpin = true; }
      else if (r.perks.includes("fate_bell") && !r.fateBellUsed && r.stageExp < r.quota && (r.quota - Math.floor(r.stageExp)) <= 15) {
        // 운명의종: 스핀 소진 & 쿼터 부족 ≤15 실패 직전 1회 자동 추가스핀(런당 1회 게이트).
        //  판정식 = Kotlin 캐논(SlotV2Service fate_bell 분기: newExp<quota && quota-newExp<=15) 동일 —
        //  부족분은 _gameOver shortBy 와 같은 Math.floor(stageExp) 기준(캐논은 정수 EXP라 floor 항등).
        r.spins += 1; r.fateBellUsed = 1;
        out.banner = (out.banner ? out.banner + " · " : "") + "🔔 운명의종 발동! — 추가 스핀 +1";
        this.toast("🔔 운명의종 발동 — 추가 스핀 +1");
      }
      else { this._gameOver(); out.gameOver = true; }
    }
    out.state = this.state();
    return out;
  }

  // ── 테마빌드: 한 스핀 결과로 런 누적/이벤트 갱신 (evalThemeBuilds ctx 재료) ──
  _trackSpin(res, mode, isLastSpin) {
    const r = this.run, tb = r.tb; if (!tb) return;
    const cells = res.cells || [];
    const rareN = cells.filter((c) => c.sym && c.sym.rare).length;
    const skullN = res.skulls || 0;
    // 인접 같은 값심볼 쌍 수
    let pairs = 0;
    for (let i = 0; i < cells.length - 1; i++) { const a = cells[i].sym, b = cells[i + 1].sym; if (a && b && a.id === b.id && VALUE_IDS.has(a.id)) pairs++; }
    if (pairs > 0) tb.adjPairs += pairs;                                  // bld_chain (런 5+)
    if ((res.bestSetCount || 0) >= 4) tb.set4 += 1;                       // bld_magnet_grad / copy_answer 재료
    // 잭팟 + 와일드 포함 여부
    if (res.jackpotSym) { tb.jackpotRun = true; if (cells.some((c) => c.sym && c.sym.special === "WILD")) tb.wildJackpotRun = true; }
    // 기도 성공(기적 ×3 또는 +25 보정) — note 로 판별
    if (mode === "PRAY" && (res.notes || []).some((n) => n.includes("기적") || n.includes("+25"))) tb.prayWins += 1;
    // 이번(=직전) 스핀의 클리어 성사 후보 정보(클리어 판정 직전에 호출되므로 최신값 보존)
    tb.lastSpinRare = rareN; tb.lastSpinSkull = skullN;
    // lifetime ☠해골 누적(bld_skull_intro 통산 100+)
    if (skullN > 0) { const cn = this.profile.counters; cn.skullTotal = (cn.skullTotal || 0) + skullN; }
  }

  // ── 테마빌드 완성판정 — 현재 런 상태로 ctx 구성 → engine.evalThemeBuilds → profile.counters bld_<id>=1(영구) ──
  _evalThemeBuilds(reachedStage, isBossClear, isLastSpinClear) {
    const r = this.run; if (!r || !r.tb) return;
    const cn = this.profile.counters;
    const ctx = {
      stage: reachedStage, machineId: r.machineId, deviceId: r.device, device2Id: "",
      perks: r.perks, curses: r.curses,
      runFastClears: r.tb.fastClears, runLastSpinClears: r.stats.lastClears, runPrayWins: r.tb.prayWins,
      runAdjPairs: r.tb.adjPairs, runSet4: r.tb.set4, runCrowns: r.stats.crown,
      isBossClear: !!isBossClear, isLastSpinClear: !!isLastSpinClear,
      clearSpinRareCount: r.tb.lastSpinRare, clearSpinSkullCount: r.tb.lastSpinSkull,
      clearSpinWildJackpot: r.tb.wildJackpotRun, jackpotThisRun: r.tb.jackpotRun, oracleUsedThisRun: r.tb.oracleUsed,
      pinUsedThisStage: r.tb.pinUsedStage, copyMadeSet4: r.tb.copySet4, bellUsedThisClear: r.tb.bellUsed,
      skullTotal: cn.skullTotal || 0, closeClears: cn.closeClears || 0,
    };
    const done = E.evalThemeBuilds(ctx);
    let newly = false;
    for (const id of done) { if (!cn[id]) { cn[id] = 1; newly = true; const b = TBUILD_BY_ID[id]; if (b) this.toast(`🏗️ 빌드도감 완성: ${b.e}${b.n}`); } }
    if (newly) this._saveProfile();
  }

  _canRecover() {
    const r = this.run;
    const dev = DEV_BY_ID[r.device];
    const manipReady = dev && dev.kind === "MANIP" && !r.usedCmds.includes(dev.cmd);
    const gambler = r.charId === "gambler" && !r.usedCmds.includes("GREROLL");
    const bellReady = r.device === "dev_bell" && (r.quota - r.stageExp) <= 25;
    return manipReady || gambler || bellReady;
  }
  // 직전 스핀 무료 재굴림(재시험 신청서). 점수 패널티 없음, usedCmds 무관.
  _freeReroll() {
    const r = this.run; if (!r.lastCells) return;
    const mods = this._mods();
    const cells = this._roll(mods, false);
    const res = E.evaluate(this.rng, cells, mods, Math.max(0, r.spinIndex - 1), r.spins, r.flameNext);
    let exp = res.exp; if (r.boss) exp = E.applyBossExp(exp, r.boss, r.spinIndex - 1, r.spins, res).exp;
    r.stageExp = r.stageExp - r.lastExpApplied + exp;
    r.score = r.score - Math.floor(r.lastResult?.score || 0) + res.score;
    r.lastExpApplied = exp; r.lastCells = res.cells.map((c) => ({ ...c })); r.lastResult = res; r.flameNext = res.hasFlame;
    if (r.stageExp >= r.quota && r.phase === PHASE.SPIN) { r._clearVia = "reroll"; this._clearStage(); }
  }

  // 포기 (스핀 진행 중 언제든 즉시 결산 = 지금까지 점수/스테이지/업적/XP 그대로 확정·랭킹 등록).
  // voluntary=true 면 '실패'가 아닌 '자발적 종료'로 표기(산식 무접촉, 표시만).
  giveUp(voluntary = false) {
    if ([PHASE.SPIN, PHASE.POST_SPIN].includes(this.run.phase)) { this._gameOver(voluntary); }
    return this.state();
  }

  // ── 재굴림/조작 장치 (직전 스핀 결과 조작, 스핀 미소모) ──
  manip(cmd, arg = 0) {
    const r = this.run;
    if (![PHASE.SPIN, PHASE.POST_SPIN].includes(r.phase) || !r.lastCells) { this.toast("직전 스핀 결과가 없어요"); return this.state(); }
    let scorePenalty = 1;
    let cells = r.lastCells.map((c) => ({ ...c }));
    const mods = this._mods();
    if (cmd === "재굴림") {
      // 도박꾼 무료 재굴림 우선, 없으면 재굴림기(점수 -10%). 둘 다 없으면 불가.
      if (r.charId === "gambler" && !r.usedCmds.includes("GREROLL")) { r.usedCmds.push("GREROLL"); }
      else if (r.device === "dev_reroll" && !r.usedCmds.includes("재굴림")) { scorePenalty = 0.9; r.usedCmds.push("재굴림"); }
      else { this.toast("재굴림 불가"); return this.state(); }
      cells = this._roll(mods, false);
    } else if (cmd === "고정") {
      if (r.device !== "dev_pin" || r.usedCmds.includes("고정")) { this.toast("고정 불가"); return this.state(); }
      const keep = arg - 1; const fresh = this._roll(mods, false);
      cells = cells.map((c, i) => (i === keep ? c : fresh[i])); r.usedCmds.push("고정"); if (r.tb) r.tb.pinUsedStage = true;
    } else if (cmd === "복사") {
      if (r.device !== "dev_copy" || r.usedCmds.includes("복사")) { this.toast("복사 불가"); return this.state(); }
      const src = arg - 1; const dst = src + 1 < cells.length ? src + 1 : src - 1;
      if (cells[src] && cells[dst]) cells[dst] = { ...cells[src] }; r.usedCmds.push("복사");
    } else if (cmd === "교체") {
      if (r.device !== "dev_swap" || r.usedCmds.includes("교체")) { this.toast("교체 불가"); return this.state(); }
      const counts = {}; cells.forEach((c) => { counts[c.sym.id] = (counts[c.sym.id] || 0) + 1; });
      let bid = cells[0].sym.id, bn = 0; for (const k in counts) if (counts[k] > bn) { bn = counts[k]; bid = k; }
      const idx = arg - 1; if (cells[idx]) cells[idx] = { sym: this._sym(bid), tag: "🔃" }; scorePenalty = 0.9; r.usedCmds.push("교체");
    // ── Phase 5 심볼 장치(MANIP·전부 r.deepMode 게이팅=일반 장착 시 무효·격리) ──
    } else if (cmd === "지휘") {   // 🎯심볼지휘봉 — 선택 칸을 같은 희귀도의 다른(해금된) 심볼로 변경
      if (!r.deepMode || r.device !== "dev_baton" || r.usedCmds.includes("지휘")) { this.toast("지휘 불가"); return this.state(); }
      const idx = arg - 1; const cur = cells[idx]; if (!cur) { this.toast("지휘 불가(칸)"); return this.state(); }
      const rar = POUCH_RARITY[cur.sym.id] || "기본";
      const unlocked = this._symUnlockedSet();
      const pool = POUCH_SYMBOLS.filter((id) => id !== cur.sym.id && SYM_BY_ID[id]
        && (POUCH_RARITY[id] || "기본") === rar && (!unlocked || unlocked.has(id)));
      if (!pool.length) { this.toast("🎯 지휘봉 — 바꿀 같은 등급 심볼이 없어요"); return this.state(); }
      cells[idx] = { sym: this._sym(this.rng.pick(pool)), tag: "🎯" }; r.usedCmds.push("지휘");
    } else if (cmd === "정화") {   // 🧤정화장갑 — 해골 1칸 → 빈칸
      if (!r.deepMode || r.device !== "dev_purify_glove" || r.usedCmds.includes("정화")) { this.toast("정화 불가"); return this.state(); }
      const si = cells.findIndex((c) => c.sym.special === "SKULL" || c.sym.id === "skull" || c.sym.id === "skull_black");
      if (si < 0) { this.toast("🧤 정화장갑 — 정화할 해골이 없어요"); return this.state(); }
      cells[si] = { sym: EMPTY_SYM, tag: "🧤" }; r.usedCmds.push("정화");
    } else if (cmd === "셔플") {   // 🔀주머니셔플러 — 이번 스핀 전체 재추출(주머니 확률표 기준)
      if (!r.deepMode || r.device !== "dev_pouch_shuffler" || r.usedCmds.includes("셔플")) { this.toast("셔플 불가"); return this.state(); }
      cells = this._roll(mods, false); r.usedCmds.push("셔플");
    } else { return this.state(); }

    const res = E.evaluate(this.rng, cells, mods, r.spinIndex - 1, r.spins, r.flameNext);
    let exp = res.exp;
    if (r.boss) exp = E.applyBossExp(exp, r.boss, r.spinIndex - 1, r.spins, res).exp;
    if (r.device === "dev_safe") exp = Math.max(exp, Math.floor(r.quota / r.spins * 0.35));
    // 직전 스핀 기여 치환
    r.stageExp = r.stageExp - r.lastExpApplied + exp;
    r.score = r.score - Math.floor((r.lastResult?.score || 0)) + Math.floor(res.score * scorePenalty);
    r.lastExpApplied = exp; r.lastCells = res.cells.map((c) => ({ ...c })); r.lastResult = res; r.flameNext = res.hasFlame; r.lastMods = mods;
    // 테마빌드: 복사로 세트4+ 완성 / 조작 결과의 잭팟·세트4 누적
    if (r.tb) {
      if (cmd === "복사" && (res.bestSetCount || 0) >= 4) r.tb.copySet4 = true;
      if ((res.bestSetCount || 0) >= 4) r.tb.set4 += 1;
      if (res.jackpotSym) { r.tb.jackpotRun = true; if (res.cells.some((c) => c.sym && c.sym.special === "WILD")) r.tb.wildJackpotRun = true; }
      r.tb.lastSpinRare = res.cells.filter((c) => c.sym && c.sym.rare).length; r.tb.lastSpinSkull = res.skulls || 0;
    }
    const out = { kind: "manip", cmd, cells: res.cells, preCells: res.preCells, bomb: res.bomb, bestSetId: res.bestSetId, bestCount: res.bestSetCount, setIds: res.setIds, gained: exp, notes: res.notes };
    out.banner = `🔧${MANIP_LABEL[cmd] || cmd} 발동! — ${MANIP_DESC[cmd] || ""}`;
    if (r.stageExp >= r.quota) { r._clearVia = "manip"; this._clearStage(); out.cleared = true; }
    else if (r.phase === PHASE.POST_SPIN && !this._canRecover()) { this._gameOver(); out.gameOver = true; }
    out.state = this.state();
    return out;
  }
  _sym(id) { return (E.cellsFromIds([id])[0] || { sym: null }).sym || this.run.lastCells[0].sym; }

  // 코인투입 (다음 스핀 +30%)
  insertCoin() {
    const r = this.run;
    if ((r.asc || 0) >= 9 && r.stage < (r._devCdUntil || 0)) { this.toast("♨️ 심화 규칙 — 장치 쿨다운(다음 스테이지에 사용)"); return this.state(); }
    if (r.device !== "dev_coin" || r.coins < 5 || r.usedCmds.includes("투입")) { this.toast("코인투입 불가"); return this.state(); }
    r.coins -= 5; r.pendingNextExpMul *= 1.3; r.usedCmds.push("투입"); this.toast("🪙 코인투입 — 다음 스핀 EXP +30%");
    if ((r.asc || 0) >= 9) r._devCdUntil = r.stage + 2;   // A9 쿨다운 +1
    return this.state();
  }
  // 예언 (다음 스핀 미리보고 확정)
  oracle() {
    const r = this.run;
    if ((r.asc || 0) >= 9 && r.stage < (r._devCdUntil || 0)) { this.toast("♨️ 심화 규칙 — 장치 쿨다운(다음 스테이지에 사용)"); return this.state(); }
    if (r.device !== "dev_oracle" || r.usedCmds.includes("예언") || r.phase !== PHASE.SPIN) { this.toast("예언 불가"); return this.state(); }
    if ((r.asc || 0) >= 9) r._devCdUntil = r.stage + 2;   // A9 쿨다운 +1
    const mods = this._mods(r.armItems.filter((id) => !["eraser_old", "eraser_fine", "eraser_god", "wild_temp", "fake_crown"].includes(id)));
    // 심화모드 씨앗/새싹 성장은 예언이 확정하는 이 굴림에서 소비(preview 와 실제 스핀이 일치·중복 성장 방지).
    r.lockedNext = this._pityRoll(this._growNextRoll(this._roll(mods, r.seedNext))); r.usedCmds.push("예언"); if (r.tb) r.tb.oracleUsed = true;   // 배치F P2: 예언이 확정하는 굴림에서 pity 소비(preview=실스핀 일치)
    const preview = E.evaluate(this.rng, r.lockedNext.map((c) => ({ ...c })), mods, r.spinIndex, r.spins, r.flameNext);
    this.toast(`🔮 예언: ${r.lockedNext.map((c) => c.sym.e).join("")} (확정)`);
    return { kind: "oracle", preview: r.lockedNext.map((c) => c.sym.e), gained: preview.exp, state: this.state() };
  }
  // 비상졸업벨 (부족≤25 즉시 클리어)
  emergencyBell() {
    const r = this.run;
    if (r.device !== "dev_bell" || (r.quota - r.stageExp) > 25) { this.toast("비상벨 불가(부족 EXP 25 초과)"); return this.state(); }
    r.stageExp = r.quota; if (r.tb) r.tb.bellUsed = true; r.device = ""; this.toast("🔔 비상졸업벨 — 즉시 클리어 (장치 파괴)");
    r._clearVia = "bell"; this._clearStage(); return this.state();
  }

  // ── 아이템 ──
  useItem(idx) {
    const r = this.run; const id = r.items[idx]; if (!id) return this.state();
    const it = ITEM_BY_ID[id]; if (!it) return this.state();
    const keep = r.perks.includes("refund") && this.rng.double() < 0.3;   // 환불 정책: 30% 확률 미소모
    if (it.k === "NEXTSPIN") { r.armItems.push(id); this.toast(`${it.e} ${it.n} 장전(다음 스핀)`); }
    else if (it.k === "PHASE") { r.phaseItems.push(id); this.toast(`${it.e} ${it.n} 발동(이번 스테이지)`); }
    else this._instant(id);
    if (keep) this.toast("🧾 환불 정책 — 아이템이 사라지지 않았어요!");
    else r.items.splice(idx, 1);
    if (r.stageExp >= r.quota && r.phase === PHASE.SPIN) { r._clearVia = "item"; this._clearStage(); }
    return this.state();
  }
  _instant(id) {
    const r = this.run; const q = r.quota;
    switch (id) {
      case "first_aid": r.spins += 1; break;
      case "double_aid": r.spins += 2; break;
      case "cram": r.stageExp += Math.floor(q * 0.15); break;
      case "cheat_sheet": r.stageExp += Math.floor(q * 0.30); break;
      case "answer_sheet": r.stageExp += Math.floor(q * 0.50); break;
      case "honor_roll": r.stageExp += Math.floor(q * 0.70); break;
      case "grad_copy": r.stageExp += Math.floor(q * 0.80); r.score = Math.max(0, r.score - 100); break;
      case "grad_cert": r.stageExp += q; break;
      case "grad_ring": if (q - r.stageExp <= 20) r.stageExp = q; break;
      case "gold_grad_bell": if (q - r.stageExp <= 50) r.stageExp = q; break;
      case "score_sticker": r.score += 150; break;
      case "score_calc": r.score = Math.floor(r.score * 1.3); break;
      case "old_coin": r.coins += 6; break;
      case "mini_coupon": r.coins += 9; break;
      case "price_hack": r.coins += 18; break;
      case "dev_battery": r.pendingNextExpMul *= 1.3; break;
      case "insurance_cert": r.survive = true; break;
      case "debt_note": r.coins += 40; r.debtStages = 3; break;
      case "retake_form": this._freeReroll(); break;
      case "study_note": { const ids = this._levelableHeld(); if (ids.length) { ids.sort((a, b) => (r.perkLevels[a] || 1) - (r.perkLevels[b] || 1)); const t = ids[0]; r.perkLevels[t] = Math.min(3, (r.perkLevels[t] || 1) + 1); const a = this._augInfo(t); this.toast(`📓 강화노트 — ${a.e}${a.n} Lv.${r.perkLevels[t]} 강화!`); } else this.toast("📓 강화노트 — 강화할 증강이 없어요"); break; }
      case "aug_catalyst": r._augLevelBoost = (r._augLevelBoost || 0) + 0.15; this.toast("🧪 증강 촉매 — 다음 증강 레벨업 확률 +15%"); break;
      case "gold_marker": { const ids = this._levelableHeld().filter((x) => this._augInfo(x)?.t === "GOLD"); if (ids.length) { ids.sort((x, y) => (r.perkLevels[x] || 1) - (r.perkLevels[y] || 1)); const t = ids[0]; r.perkLevels[t] = Math.min(3, (r.perkLevels[t] || 1) + 1); const a = this._augInfo(t); this.toast(`📙 골드형광펜 — ${a.e}${a.n} Lv.${r.perkLevels[t]} 강화!`); } else this.toast("📙 골드형광펜 — 강화할 골드 증강이 없어요"); break; }
      case "prism_ink": r._prismInk = true; this.toast("💧 프리즘 잉크 — 다음 증강 선택이 프리즘 등급으로!"); break;
      case "overcharge": r.pendingNextExpMul *= 1.5; r.score = Math.max(0, r.score - 200); this.toast("⚡ 과충전 — 다음 스핀 EXP +50% (점수 -200)"); break;
      // §3(g) 깨진프리즘/검은복권/악마계약 — 심화(deepMode)는 퍽 지급 풀을 deepCompatPool 경유(D계열 누출 봉쇄).
      //  빈 풀 폴백=코인 +25(악마계약 기존 +25 와 동일 스케일·실질 도달 불가 방어용). 일반모드=원문 풀 그대로(무회귀).
      //  저주 branch(r.curses)는 기존 심화 동작 그대로 무수정(퍽 카탈로그 필터와 별개 시스템).
      case "broken_prism": {
        const pr = AUGMENTS.filter((a) => a.t === "PRISM" && !r.perks.includes(a.id));
        let base = pr.length ? pr : AUGMENTS;   // 기존 폴백 순서 보존(프리즘 소진 시 전체)
        if (r.deepMode) base = E.deepCompatPool(base, r.pouch);
        if (!base.length) { r.coins += 25; this.toast("🔮 깨진프리즘 — 맞는 증강이 없어 코인 +25"); break; }
        const p = this.rng.pick(base); r.perks.push(p.id); r.stats.prismPicks += 1; this.toast(`🔮 깨진프리즘 — ${p.e}${p.n}`); break;
      }
      case "black_lottery": if (this.rng.double() < 0.5) { let pool = RELICS.filter((x) => x.t === "GOLD"); if (r.deepMode) pool = E.deepCompatPool(pool, r.pouch); if (!pool.length) { r.coins += 25; this.toast("🎫 검은복권 — 코인 +25"); } else { const rel = this.rng.pick(pool); r.perks.push(rel.id); this.toast(`🎫 ${rel.e}${rel.n}`); } } else { const cu = this.rng.pick(CURSES); r.curses.push(cu.id); this.toast(`🎫 저주 ${cu.e}${cu.n} — ${cu.d}`); } break;
      case "devil_contract": { const pool = r.deepMode ? E.deepCompatPool(RELICS, r.pouch) : RELICS; if (!pool.length) { r.coins += 25; this.toast("😈 계약 무산 — 코인 +25"); break; } const rel = this.rng.pick(pool); const cu = this.rng.pick(CURSES); r.perks.push(rel.id); r.curses.push(cu.id); r.coins += 25; this.toast(`😈 ${rel.e}${rel.n} + 저주 ${cu.e}${cu.n}(${cu.d}) · 코인 +25`); break; }
      default: this.toast("아이템 사용"); break;
    }
  }
  _applyPrismLike(id) { /* broken_prism: 간이 — 해당 프리즘 증강을 임시 perk로 (이번 스테이지) */ this.run.phaseItems.push(id); }

  // ── 스테이지 클리어 → 요약 화면(STAGE_CLEAR) → (사용자 클릭) → 보상 노드 ──
  _clearStage() {
    const r = this.run;
    const stage = r.stage;
    // 심화 10+ : 최종 보스(스테이지15) 2페이즈 — 1페이즈 클리어 시 요구치↑로 같은 스테이지 재시작, 2페이즈 클리어해야 졸업
    if (stage === 15 && (r.asc || 0) >= 10 && !r._bossPhase2) {
      r._bossPhase2 = true; this.toast("👹 최종 보스 2페이즈! 요구 EXP 상승 — 한 번 더!"); this._beginStage(); return;
    }
    const leftover = Math.max(0, Math.floor(r.stageExp) - r.quota);
    const leftSpins = Math.max(0, r.spins - r.spinIndex);
    const boss = E.isBossStage(stage);
    if (stage === 15) { r.graduatedThisRun = true; r._bossPhase2 = false; }   // 스테이지 15 클리어 = 졸업(2페이즈면 여기 도달 시 완료)
    // ── JS-4 조건부 증강 스택 갱신(클리어 시) ── 성장일지=클리어마다+1(≤5). 눈덩이=여유클리어(남은≥2)+1(≤4)·보스클리어-1(≥0).
    r.growthStack = Math.min(5, (r.growthStack || 0) + 1);
    if (leftSpins >= 2) r.snowStack = Math.min(4, (r.snowStack || 0) + 1);
    if (boss) r.snowStack = Math.max(0, (r.snowStack || 0) - 1);
    // ── 테마빌드(JS-5): 런 클리어 누적 + lifetime '아슬아슬'(남은스핀 0 클리어) ──
    if (r.tb) {
      if (leftSpins >= 2) r.tb.fastClears += 1;                 // bld_fast_start (런 3+)
      if (leftSpins === 0) { const cn = this.profile.counters; cn.closeClears = (cn.closeClears || 0) + 1; }   // bld_heartbreaker (통산 5+)
    }
    const curses = r.curses.length;
    const sBase = stage * 50, sLeft = leftover * C.SCORE_PER_LEFTOVER, sSpins = leftSpins * C.SCORE_PER_LEFTSPIN, sBoss = boss ? C.BOSS_CLEAR_SCORE : 0;
    const curseMul = 1;   // 저주는 클리어 점수 보너스 없음(패널티 전용)
    const afterCurse = sBase + sLeft + sSpins + sBoss;
    const streak = E.streakBonus(stage);
    let gain = afterCurse + streak; let debt = false;
    if (r.debtStages > 0) { gain = 0; r.debtStages -= 1; debt = true; this.toast("🧾 빚문서 — 이번 클리어 보상 0"); }
    r.score += gain;
    const clearCoin = C.CLEAR_COIN + (boss ? C.BOSS_COIN : 0) + (this._mods().clearCoinBonus || 0);
    r.coins += clearCoin;
    if (boss) r.stats.bossClears += 1;
    // ── Phase 5: 심화 업적 통계(스테이지/보스 클리어 시점) — deepStats 게이팅(일반 무영향·격리). ──
    if (r.deepMode && r.deepStats) {
      const ds = r.deepStats; const total = E.pouchTotal(r.pouch || {});
      if (total > 0 && total <= 27) ds.compress95Clear = true;   // ★§1.5 V3P1: 임계 54→27(×0.5). 플래그/카운터 이름(compress95Clear/deepCompress95)은 연속성·grandfather 위해 유지.
      if (boss) {
        ds.bossClears += 1;                     // 심볼마스터(통산 10)
        this._markDeepBossAchievements();       // 압축/전공/균형/정화/왕관 보스클리어 판정
      }
      this._trackDeepStats();                   // 최대총량·희귀/전설 발견(클리어 시점 주머니 반영)
    }
    // Phase 5 심볼유물: 심볼복사판(sr_copier) — 보스 클리어 시 최다 태그 심볼 +N(주머니).
    if (r.deepMode && boss) { const sp = this._symMods(); if (sp && sp.bossCopyN > 0) this._bossCopy(sp.bossCopyN); }
    this.toast(`✅ 스테이지 ${stage} 클리어! +${gain}점 · +${clearCoin}코인`);

    // 보스 클리어 → 장치 드랍 + 보상 노드 3개(증강1 확정 + 랜덤2) 미리 생성
    const drops = [];
    if (boss) { const d = E.pickDevices(this.rng, stage, new Set(r.curses), 1)[0]; if (d) drops.push(d); }
    if (r.deepMode) {
      // ── 심화모드: 주머니(POUCH) 편집 + Phase5 심볼 증강/유물 노드 병행 + 코인 경제 노드. ──
      //  ★POUCH 는 항상. 두 번째 슬롯을 확률로 심볼증강(SYMAUG)/심볼유물(SYMREL)/코인경제(SHOP·REST·GAMBLE·EVENT)에서 결정.
      //   - 보스클리어: 심볼유물 비중↑(일반모드 유물=보스 관례 정합).
      //   - 심볼증강 슬롯이 뽑히고 레벨업 가능 보유증강 있으면 10%+pity 로 AUGLEVEL(증강 레벨업)로 교체
      //     → 형광펜/복습책 부활의 핵심(레벨업 노드가 심화에 존재).
      //  연구실단골/중독·연구실열쇠(shopLab)·덱빌딩슬롯(alwaysRepair)=정비소(SHOP) 등장↑.
      const sp = this._symMods();
      const dpool = ["SHOP", "REST", "GAMBLE", "EVENT"];
      // 배치 A Step 2: stage>=6 시 CURSE/RISK 노드 추가(기본판 조건과 동일).
      if (stage >= 6) { dpool.push("CURSE"); dpool.push("RISK"); }
      // §9.2 J3: JACKPOT 노드 — 심화 dpool, stage 3+, 낮은 가중(1회 삽입으로 ~14% 풀 비중)
      if (stage >= 3) dpool.push("JACKPOT");
      if (sp && sp.shopLabWeight > 0) for (let k = 0; k < Math.min(4, sp.shopLabWeight); k++) dpool.push("SHOP");
      const dsh = this.rng.shuffle(dpool);
      // 두 번째 슬롯 결정(확률). [MED-2] 덱빌딩슬롯(alwaysRepair)은 second 를 SHOP 으로 '고정'하면
      //  SYMAUG/SYMREL/AUGLEVEL 노드가 영구 미생성(프리즘 픽이 자기 빌드 잠금) → second 는 기존 확률대로
      //  굴리고 SHOP 은 아래에서 세 번째 노드로 push(중복 방지). 요구 +15%(quotaMul)는 그대로 유지.
      let second;
      {
        const roll = this.rng.double();
        const relThresh = boss ? 0.35 : 0.20;   // 보스=심볼유물 35%, 일반=20%
        if (roll < 0.40) second = "SYMAUG";
        else if (roll < 0.40 + relThresh) second = "SYMREL";
        else second = dsh[0];
      }
      // §6.2 노드 3택화: ["POUCH", second, third]. third = dpool 에서 second 비중복 1장.
      //  second 가 SYMAUG/SYMREL(dpool 비출신) → third = dsh[0].
      //  second 가 dpool 출신 → third = dsh[1](second=dsh[0] 이므로 비중복 보장).
      //  dsh 길이 부족 시(dpool 축소 등) third 없이 2장 폴백(안전망).
      const thirdCandidate = (second === "SYMAUG" || second === "SYMREL") ? dsh[0] : dsh[1];
      const nodes = ["POUCH", second];
      if (thirdCandidate && thirdCandidate !== second) nodes.push(thirdCandidate);
      // 심볼증강 슬롯 + 레벨업 가능 보유증강 → 확률로 AUGLEVEL 교체(형광펜/복습책이 참조하는 pity/boost).
      if (second === "SYMAUG" && this._levelableHeld().length) {
        const chance = Math.min(0.6, (r._augLevelChance ?? 0.10) + (r._augLevelBoost || 0));
        if (this.rng.double() < chance) { nodes[1] = "AUGLEVEL"; r._augLevelChance = 0.10; }
        else {
          r._augLevelChance = Math.min(0.20, (r._augLevelChance ?? 0.10) + 0.02);
          // V3P4: 🧷안전핀노트(safepin·fuse) — AUGLEVEL 미발생 시 _augLevelChance +1%p 추가 누적 후 소비.
          if (r.pouch && (r.pouch.safepin || 0) > 0 && r._safePinActive) {
            r._augLevelChance = Math.min(0.20, (r._augLevelChance ?? 0.10) + 0.01);
            r.pouch = E.applySymbolReward(r.pouch, { type: "remove", id: "safepin", n: 1 });
            this._checkArchetype();
            this.toast("🧷 안전핀노트 — 증강 레벨업 확률 +1%p 누적 (소모)");
          }
        }
        r._augLevelBoost = 0;
      }
      r._safePinActive = false;   // 스테이지 종료 시 리셋
      // [MED-2] 덱빌딩슬롯 — 매 스테이지 정비(SHOP) 노드 보장은 '추가 노드'로(이미 있으면 중복 방지).
      if (sp && sp.alwaysRepair && !nodes.includes("SHOP")) nodes.push("SHOP");
      // Phase 5 심볼 장치: 🔔연구실호출벨 — 보스 클리어 후 정비소(SHOP) 등장 보장(중복 방지). deepMode 전용.
      if (boss && r.device === "dev_call_bell" && !nodes.includes("SHOP")) { nodes.push("SHOP"); this.toast("🔔 연구실호출벨 — 정비소 등장!"); }
      if (drops.length) nodes.push("DEVICE");
      r.nodes = nodes; r._drop = drops[0] || null; r.options = nodes;
    } else {
    const pool = ["RELIC", "SHOP", "REST", "GAMBLE", "EVENT"];
    if (stage >= 6) pool.push("CURSE", "RISK");
    const nodes = ["AUGMENT"]; const sh = this.rng.shuffle(pool); nodes.push(sh[0], sh[1]);
    if (drops.length) nodes.push("DEVICE");
    r.nodes = nodes; r._drop = drops[0] || null; r.options = nodes;
    // ── 증강 레벨업 기회(10%+pity, 최대20%) ── 레벨업 가능 보유증강(<Lv3) 있을 때만, AUGMENT 노드를 AUGLEVEL 로 교체
    if (this._levelableHeld().length) {
      const chance = Math.min(0.6, (r._augLevelChance ?? 0.10) + (r._augLevelBoost || 0));   // pity(최대20%) + 촉매 부스트
      if (this.rng.double() < chance) { const i = r.nodes.indexOf("AUGMENT"); if (i >= 0) r.nodes[i] = "AUGLEVEL"; r._augLevelChance = 0.10; }
      else r._augLevelChance = Math.min(0.20, (r._augLevelChance ?? 0.10) + 0.02);
      r._augLevelBoost = 0;   // 촉매는 1회성(다음 기회에 소진)
    }
    }
    // ── 클리어 연출용 통계(엔진 점수/EXP 산식 무수정 — 기록만) ──
    const via = r._clearVia || "spin";            // spin|reroll|manip|bell|item (호출부에서 세팅)
    const bySpin = via === "spin" || via === "reroll" || via === "manip";  // 슬롯/조작 결과로 클리어
    const usedSpins = r.spinIndex;                 // 사용 스핀 수(메인스핀 경로는 클리어 스핀 번호와 동일)
    const lastSpinExp = Math.floor(r.lastExpApplied || 0);   // 클리어를 만든 직전 스핀 EXP(스핀 클리어 시)
    const overPct = r.quota > 0 ? (leftover / r.quota) * 100 : 0;  // 요구치 대비 초과 비율(%)

    // ── §3 V3P3: 심화 자동 소멸 — stage(클리어 직전 스테이지) >= 15 시 기본 이득 심볼 1개 무작위 제거 ──
    //  대상: isAutoDecayTarget(id) — cat=base && !harmful (체리/책/별/보석/코인/불꽃/자석/폭탄).
    //  해골/빈칸/저주/특수는 제외. DECK_MIN 미만 허용(압박 의도). 대상 0개면 스킵.
    //  예고: stage 14 클리어(=15 진입 직전) 시 런당 1회 플래그(_decayForewarned).
    let decayBanner = "";
    if (r.deepMode && r.pouch) {
      if (stage === 14 && !r._decayForewarned) {
        // 14 클리어 = 다음 스테이지가 15 → 예고 1회
        r._decayForewarned = true;
        decayBanner = "⚠️ 다음 스테이지부터 기본 이득 심볼이 매 클리어 1개씩 사라집니다";
      } else if (stage >= 15) {
        // 15+ 클리어 시 기본 이득 심볼 1개 제거
        const decayTargets = Object.entries(r.pouch)
          .filter(([id, n]) => n > 0 && E.isAutoDecayTarget(id))
          .flatMap(([id, n]) => Array(n).fill(id));   // 개수만큼 풀에 삽입(균등 무작위)
        if (decayTargets.length > 0) {
          const picked = decayTargets[Math.floor(this.rng.double() * decayTargets.length)];
          const symInfo = SYM_BY_ID[picked] || { e: "?", n: picked };
          // applySymbolReward 재사용(remove 경로). DECK_MIN 미만도 허용(소멸 전용 경로).
          r.pouch = E.applySymbolReward(r.pouch, { type: "remove", id: picked, n: 1 });
          this._checkArchetype();
          decayBanner = `🍂 심화 압력 — 기본 심볼이 낡아 사라졌습니다: ${symInfo.e}${symInfo.n} 1개 제거 (해로운 심볼은 남습니다)`;
          if (r.deepStats) r.deepStats.autoDecays = (r.deepStats.autoDecays || 0) + 1;
        }
        // 대상 0개 = 특수 덱 완성 상태 → 스킵(decayBanner 빈 문자열 유지)
      }
    }

    // §9.2 J3: 스테이지 1회 제한 플래그 초기화(리치표식/재도전릴/잭팟왕관 — 스테이지마다 재사용 허용)
    if (r.deepMode) {
      r._reachMarkUsed    = false;
      r._retryReelUsed    = false;
      r._jackpotCrownUsed = false;
    }

    r.clearSummary = {
      stage, quota: r.quota, stageExp: Math.floor(r.stageExp), leftover, overPct, leftSpins, boss,
      bossName: boss ? (E.bossFor(stage)?.n || "") : "", sBase, sLeft, sSpins, sBoss,
      curses, curseMul, afterCurse, streak, gain, coin: clearCoin, debt, nextStage: stage + 1,
      total: r.score, coinsNow: r.coins,
      via, bySpin, usedSpins, totalSpins: r.spins, clearSpin: usedSpins, lastSpinExp,
      lastCells: (r.lastCells || []).map((c) => ({ e: c.sym.e, tag: c.tag || "" })),
      lastNotes: (r.lastResult && r.lastResult.notes) ? r.lastResult.notes.slice() : [],
      exact: leftover === 0,
      decayBanner,   // §3 V3P3: 자동 소멸 연출(빈 문자열=소멸 없음, 문자열=배너 표기)
    };
    r._clearVia = null;
    r.stage = stage + 1;
    r.phase = PHASE.STAGE_CLEAR;
    // 테마빌드 완성판정(방금 클리어한 stage 도달, 보스/막스핀 여부). 이후 막스핀 플래그 리셋.
    this._evalThemeBuilds(stage, boss, !!(r.tb && r.tb.lastSpinClear));
    if (r.tb) r.tb.lastSpinClear = false;
    this._syncDex();
  }
  proceedToNodes() { if (this.run.phase === PHASE.STAGE_CLEAR) { this.run.phase = PHASE.NODE_SELECT; this.run.options = this.run.nodes; } return this.state(); }

  // 증강/보상 적용 후 → 다음 스테이지 인트로(클릭해야 시작). 다음 스테이지 미리보기 포함.
  _enterRewardDone(msg) {
    const r = this.run; r.rewardMsg = msg;
    const mods = this._mods();
    const stage = r.stage;   // _clearStage 에서 이미 다음 스테이지로 +1 됨
    r.nextPreview = {
      stage, quota: Math.max(1, Math.floor(E.quota(stage) * mods.quotaMul * E.bossQuotaMul(stage) * this._deepPenalty())),
      spins: E.spinsPerStage(mods) + E.bossSpins(stage), boss: E.bossFor(stage),
    };
    r.statsView = this.currentStats();
    this._syncDex();
    r.phase = PHASE.REWARD_DONE;
    this.toast(msg);
  }
  proceedToStage() { if (this.run.phase === PHASE.REWARD_DONE) this._beginStage(); return this.state(); }

  // 현재 빌드의 능력치 상세값 (증강/보상 인트로에서 표시)
  currentStats() {
    const m = this._mods();
    const rows = [];
    const mul = (k, v, lo) => { if (v !== 1) rows.push({ k, v: "×" + (Math.round(v * 100) / 100), up: lo ? v < 1 : v > 1 }); };
    const add = (k, v, suf) => { if (v) rows.push({ k, v: (v > 0 ? "+" : "") + v + (suf || ""), up: v > 0 }); };
    mul("모든 EXP", m.expMul);
    add("스핀당 EXP", m.flatExp);
    mul("점수", m.scoreMul);
    mul("코인", m.coinMul);
    mul("첫 스핀 EXP", m.firstSpinExpMul);
    mul("막 스핀 EXP", m.lastSpinExpMul);
    mul("세트 보너스", m.setExpMul);
    mul("가운데 칸 EXP", m.centerExpMul);
    mul("양끝 일치 EXP", m.endsMatchExpMul);
    add("인접쌍 EXP", m.adjacentSameExp, "/쌍");
    mul("희귀 등장", m.rareWeightMul);
    add("☠해골 EXP", m.skullExp);
    add("스테이지 스핀", m.bonusSpins);
    mul("요구 EXP", m.quotaMul, true);   // 낮을수록 이득
    add("클리어 코인", m.clearCoinBonus);
    const e = (id) => (SYM_BY_ID[id] ? SYM_BY_ID[id].e : id);
    const symExp = Object.entries(m.perSymbolExp).filter(([, v]) => v).map(([id, v]) => `${e(id)}${v > 0 ? "+" : ""}${v}`);
    const symScore = Object.entries(m.perSymbolScore).filter(([, v]) => v).map(([id, v]) => `${e(id)}${v > 0 ? "+" : ""}${v}`);
    const tags = Object.entries(m.tagExpBonus).filter(([, v]) => v).map(([t, v]) => `#${t}${v > 0 ? "+" : ""}${v}`);
    return { rows, symExp, symScore, tags };
  }

  // 보상 노드 선택
  selectNode(idx) {
    const r = this.run; const node = r.nodes[idx]; if (!node) return this.state();
    switch (node) {
      case "AUGMENT": { r.phase = PHASE.PERK_PICK; r._pickKind = "AUG"; const ft = r._prismInk ? "PRISM" : undefined; const off = E.offerPerks(this._augPool(), "AUGMENT", this.rng, new Set(r.perks), { clearedStage: r.stage - 1, forceTier: ft }); r.options = off.options; r.offerMeta = off.meta; r._prismInk = false; break; }
      case "RELIC": { r.phase = PHASE.PERK_PICK; r._pickKind = "REL"; const off = E.offerPerks(this._relicPool(), "RELIC", this.rng, new Set(r.perks), { clearedStage: r.stage - 1 }); r.options = off.options; r.offerMeta = off.meta; break; }
      case "AUGLEVEL": { r.phase = PHASE.PERK_PICK; r._pickKind = "LVL"; r.offerMeta = null; r.options = this._levelableHeld().map((id) => { const a = this._augInfo(id); const lv = r.perkLevels[id] || 1; return { ...a, curLevel: lv, nextLevel: lv + 1 }; }); if (!r.options.length) { this._enterRewardDone("강화할 증강이 없어요"); } break; }
      case "SHOP": this._openShop(); break;
      case "REST": {
        // §3 Step 3: 심화 변형 — 선택지: "코인 +12" vs "정화: 해골 1개 제거"(해골 보유 시만).
        //   일반모드: 기존 코인 +12 단일 처리(무회귀).
        if (r.deepMode && r.pouch && (r.pouch.skull || 0) + (r.pouch.skull_black || 0) > 0) {
          r.phase = PHASE.PERK_PICK; r._pickKind = "REST_DEEP"; r.offerMeta = null;   // 직전 SYMAUG deepPerk 메타 잔존 → 헤더 오염 방지
          r.options = [
            { id: "rest_coin",  e: "🪙", n: "코인 보충",   d: "코인 +12", t: "SILVER" },
            { id: "rest_purify", e: "🕊️", n: "해골 정화", d: "주머니 ☠해골 1개 제거", t: "GOLD" },
          ];
        } else { r.coins += 12; this._enterRewardDone("🛌 휴식 — 코인 +12 획득"); }
        break;
      }
      case "GAMBLE": {
        // §3 Step 2: 심화 변형 — 코인 도박(기존) + "심볼 도박" 선택지 추가.
        //   심볼 도박: 50% 보유 기본계열 랜덤 +1 / 50% 해골 +1 (pouchValidate 실패 시 미노출).
        //   일반모드: 기존 코인 도박만(무회귀).
        if (r.deepMode && r.pouch) {
          const baseFamilies = ["cherry", "book", "star", "gem", "coin", "flame", "magnet", "bomb"];
          const heldBase = baseFamilies.filter((id) => (r.pouch[id] || 0) > 0);
          let symGambleOk = heldBase.length > 0;
          if (symGambleOk) {
            // pouchValidate: 해골 +1은 총량 상한 넘지 않으면 유효(간단 검증).
            const total = E.pouchTotal(r.pouch);
            const bounds = E.repairBounds(this._repairState());
            if (total >= (bounds.max || DEEP.DECK_MAX)) symGambleOk = false;
          }
          r.phase = PHASE.PERK_PICK; r._pickKind = "GAMBLE_DEEP"; r.offerMeta = null;   // 직전 SYMAUG deepPerk 메타 잔존 → 헤더 오염 방지
          const opts = [
            { id: "gamble_coin",   e: "🎲", n: "코인 도박",   d: "50% 코인 2배 / 50% 코인 유지", t: "SILVER" },
          ];
          if (symGambleOk) {
            opts.push({ id: "gamble_sym", e: "🎰", n: "심볼 도박",
              d: "50% 보유 기본 심볼 +1 / 50% ☠해골 +1", t: "GOLD" });
          }
          r.options = opts;
        } else {
          let msg; if (this.rng.double() < 0.5) { r.coins *= 2; msg = "🎲 도박 성공 — 코인 2배!"; } else { msg = "🎲 도박 실패 — 코인 유지"; }
          this._enterRewardDone(msg);
        }
        break;
      }
      case "EVENT": this._randomEvent(); break;
      case "CURSE": {
        // 심화모드: deep:1 태깅된 저주만 픽(배치 A Step 2). 일반모드: 전체 풀(기존 동작 무수정).
        const cursePool = r.deepMode
          ? CURSES.filter((c) => c.deep >= 1 && !r.curses.includes(c.id))
          : CURSES.filter((c) => !r.curses.includes(c.id));
        const cFallback = r.deepMode ? CURSES.filter((c) => c.deep >= 1) : CURSES;
        const cPick = this.rng.pick(cursePool.length ? cursePool : (cFallback.length ? cFallback : CURSES));
        r.curses.push(cPick.id); r.coins += 30;
        this._enterRewardDone(`🌑 저주 ${cPick.e}${cPick.n} 획득 — ${r.deepMode ? (cPick.dDesc || cPick.d) : cPick.d} · 코인 +30`);
        break;
      }
      case "RISK": {
        // 배치 A Step 3: 심화 RISK = PRISM 심볼퍽(SYM_AUGMENTS/SYM_RELICS) + 심화 유효 저주.
        //   일반 RISK = PRISM 일반증강 + 전체 저주(기존 동작 무수정).
        let riskPerk, riskMsg;
        if (r.deepMode) {
          const symPrismPool = [...SYM_AUGMENTS, ...SYM_RELICS].filter((p) => p.t === "PRISM" && !r.perks.includes(p.id));
          const symPrismFallback = [...SYM_AUGMENTS, ...SYM_RELICS].filter((p) => !r.perks.includes(p.id));
          riskPerk = this.rng.pick(symPrismPool.length ? symPrismPool : (symPrismFallback.length ? symPrismFallback : SYM_AUGMENTS));
        } else {
          const pr = AUGMENTS.filter((a) => a.t === "PRISM" && !r.perks.includes(a.id));
          riskPerk = this.rng.pick(pr.length ? pr : AUGMENTS);
        }
        r.perks.push(riskPerk.id); r.stats.prismPicks += 1;
        const riskCursePool = r.deepMode ? CURSES.filter((c) => c.deep >= 1) : CURSES;
        const riskCurse = this.rng.pick(riskCursePool.length ? riskCursePool : CURSES);
        r.curses.push(riskCurse.id);
        this._enterRewardDone(`🎲 위험거래 — ${riskPerk.e}${riskPerk.n}(${riskPerk.d}) + 저주 ${riskCurse.e}${riskCurse.n}(${r.deepMode ? (riskCurse.dDesc || riskCurse.d) : riskCurse.d})`);
        break;
      }
      case "DEVICE": r.phase = PHASE.DEVICE_NODE; r.options = [r._drop]; break;
      case "JACKPOT": {   // §9.2 J3: 잭팟 노드 — 현재 덱 최다 태그 기반 심볼 3택 + 스킵(+5코인)
        // 심화 전용(dpool 에서만 뽑히므로 deepMode=true 보장). 최다 태그 계산 후 해당 태그 심볼 3종 제시.
        //  ★v1 확정(2026-07-10): §2 교체 비용 플로우(POUCH_COST) 재사용 — 공짜·무검증 획득 금지.
        //   r._pickKind="POUCH" 로 라우팅해 기존 POUCH 특수카드 픽/렌더/celebrate 경로 그대로 재사용
        //   (pouchValidate·JACKPOT_TAG_DECK_MAX 검증도 그 경로에 이미 내장돼 자동 적용됨). 별도 JACKPOT_NODE
        //   pickKind/렌더러는 은퇴(ui.js renderPerk 참고).
        r.phase = PHASE.PERK_PICK; r._pickKind = "POUCH";
        // 현재 덱에서 JACKPOT_TAG 기반 태그별 개수 집계 → 최다 태그 결정
        const jtTagCount = {};
        for (const [sid, cnt] of Object.entries(r.pouch || {})) {
          if (cnt <= 0) continue;
          const t = (JACKPOT_TAG && JACKPOT_TAG[sid]) || null;
          if (t) jtTagCount[t] = (jtTagCount[t] || 0) + cnt;
        }
        let topTag = null, topTagN = 0;
        for (const [t, n] of Object.entries(jtTagCount)) { if (n > topTagN) { topTagN = n; topTag = t; } }
        // 최다 태그 심볼 풀에서 3종 랜덤 선택(미보유 우선, 없으면 전체에서)
        const jtAllSyms = SYMS.filter((s) => s.id && s.special && (JACKPOT_TAG && JACKPOT_TAG[s.id]) === topTag);
        const jtUnowned  = jtAllSyms.filter((s) => !(r.pouch && r.pouch[s.id] >= 1));
        const jtCandPool = jtUnowned.length >= 3 ? jtUnowned : jtAllSyms;
        const jtPicked = this.rng.shuffle(jtCandPool.map((s) => s)).slice(0, Math.min(3, jtCandPool.length));
        // §2 특수 카드 계약과 동일(type/id/tier/e/n/cost) — engine.offerSymbolRewards 의 cost 규칙 그대로 재사용.
        //  실버=기본1개 제거 / 골드=기본2개(이득<3 이면 1개) / 프리즘=기본2개 or 저주+1 선택 / 저주=무료(+1, DECK_MAX만).
        const jtCards = jtPicked.map((s) => {
          const tier = E.symTierOf(s.id);
          const cost = tier === "CURSE" ? { free: true }
            : tier === "PRISM" ? { removeN: 2, orCurse: true }
            : tier === "GOLD"  ? { removeN: 2, lowRemoveN: 1 }
            : { removeN: 1 };   // SILVER
          return { type: "special", id: s.id, tier, e: s.e, n: s.n, cost };
        });
        // 스킵 카드: 기존 POUCH 오퍼의 범용 skip 타입 그대로(코인 +5) — 렌더/픽 처리 전부 공용 경로.
        jtCards.push({ type: "skip", tier: "SILVER", coinBonus: 5, e: "⏭", n: "선택하지 않기" });
        r.options = jtCards;
        r.offerMeta = { kind: "JACKPOT_NODE", topTag, topTagN, total: E.pouchTotal(r.pouch) };
        if (!jtPicked.length) { r.coins += 5; this._enterRewardDone("🎰 잭팟 노드 — 해당 태그 심볼 없음, 코인 +5"); }
        break;
      }
      case "POUCH": {   // 심화모드: 주머니(심볼 덱) 편집 보상 — 추가/제거/교체/업그레이드 카드
        r.phase = PHASE.PERK_PICK; r._pickKind = "POUCH";
        // 🧿저주눈(근사): 다음 주머니 보상 후보 +N. 1회성(소진).
        const bonus = r.deepRewardBonus || 0; r.deepRewardBonus = 0;
        // V3P2: 보상 오퍼 v3 — 특수 카드(extraCards/goldBonus 리맵). rewardBonus/심볼분류함(sr_sorter) 연동 유지.
        const sp = this._symMods();
        const symBonus = sp ? sp.rewardBonus : 0;   // sr_sorter: 선택지 +1(extraCards로 흡수)
        // §9.0 J1: 태그잭팟 달성 플래그 소비 → 이번 오퍼 PRISM 후보 1장 보장(forcePrismFirst).
        // §9.1 J2: 피버잭팟 플래그도 동일 경로로 프리즘 보장 추가(최대 1회 중복이면 forcePrismFirst=true로 충분).
        // §9.2 J3: 잭팟왕관(JACKPOT_CROWN) — 잭팟 시 보상등급+1(다음 POUCH 오퍼 PRISM 보장 추가).
        const useJtPrism = !!r._jackpotPrismPending;
        if (useJtPrism) r._jackpotPrismPending = false;
        const useFeverPrism = !!r._feverJackpotPrism;
        if (useFeverPrism) r._feverJackpotPrism = false;
        const useJpCrownPrism = !!r._jackpotCrownPending;
        if (useJpCrownPrism) { r._jackpotCrownPending = false; this.toast("👑 잭팟왕관 — 다음 주머니 오퍼 보상등급 +1!"); }
        r.options = E.offerSymbolRewards(this.rng, r.pouch, r.stage - 1, {
          bounds: E.repairBounds(this._repairState()),
          symUnlocked: this._symUnlockedSet(),
          extraCards: (bonus + symBonus) + (sp ? sp.addBasicDelta : 0),  // sa_basic_research 리맵
          goldBonus: sp ? sp.rareChance : 0,          // sa_rare_research 리맵: 골드확률 +10%p
          legendWeight: sp ? sp.legendWeight : 0,
          noCurseAdds: !!(sp && sp.curseChance < 0),   // [LOW-3] 정화된세계 — 저주 특수 혼입 0%
          forcePrismFirst: useJtPrism || useFeverPrism || useJpCrownPrism, // §9.0 J1 태그잭팟 / §9.1 J2 피버잭팟 / §9.2 J3 왕관
        });
        r.offerMeta = { deep: true, pouchBefore: { ...r.pouch }, total: E.pouchTotal(r.pouch), bonusOptions: bonus + symBonus };
        if (!r.options.length) this._enterRewardDone("🎒 이번엔 마땅한 주머니 보상이 없어요 — 다음 스테이지로!");   // 후보 0 방어
        break;
      }
      case "SYMAUG": {   // 심화모드: 심볼 증강 + 관련 일반 증강 혼합 오퍼(deepCompatPool — D계열·주머니 무관 퍽 차단).
        //  effect 는 심볼퍽=_mods symPerkMods / 일반 퍽=buildMods 로 각자 자동 배선. 일반 AUGMENT case 무접촉(격리).
        //  compatFilter=세트조각 주입 누출 차단(setSynergyPick 은 raw AUGMENTS 소스). 심볼퍽은 항상 통과.
        r.phase = PHASE.PERK_PICK; r._pickKind = "AUG";
        const symPool = E.symAugPool(this._symUnlockedSet());
        // 프리즘 잉크: AUGMENT case 와 동일 패턴으로 forceTier "PRISM" 전달.
        //  PRISM 심볼증강 풀이 소진돼 off.meta.tier 가 PRISM 이 아니면(폴백 발생) 잉크 미소비(잉크 보존).
        const ftSym = r._prismInk ? "PRISM" : undefined;
        const off = E.offerPerks(symPool.concat(E.deepCompatPool(this._augPool(), r.pouch)), "AUGMENT", this.rng, new Set(r.perks),
          { clearedStage: r.stage - 1, forceTier: ftSym, compatFilter: (p) => !!SYM_PERK_BY_ID[p.id] || E.isDeepCompat(p, r.pouch) });
        // 잉크 소비 여부: PRISM 강제 성공(off.meta.tier === "PRISM") 시에만 소비. 폴백 시 잉크 보존(안전 폴백).
        if (ftSym) r._prismInk = (off.meta && off.meta.tier !== "PRISM");
        this._ensureSymPerkCard(off, symPool);   // 오퍼가 전부 일반 퍽이면 심볼퍽 최소 1장 보장
        this._archSortOffer(off);                // 배치G: 전공 계열 dSym 퍽 표시 우선(순서만·확률 불변)
        r.options = off.options; r.offerMeta = { ...off.meta, deepPerk: true };
        if (!r.options.length) this._enterRewardDone("✨ 이번엔 새 심볼 증강이 없어요 — 다음 스테이지로!");
        break;
      }
      case "SYMREL": {   // 심화모드: 심볼 유물 + 관련 일반 유물 혼합 오퍼(SYMAUG 동형·프리즘 폴백→GOLD 은 offerPerks 처리).
        r.phase = PHASE.PERK_PICK; r._pickKind = "REL";
        const symPool = E.symRelPool(this._symUnlockedSet());
        const off = E.offerPerks(symPool.concat(E.deepCompatPool(this._relicPool(), r.pouch)), "RELIC", this.rng, new Set(r.perks),
          { clearedStage: r.stage - 1, compatFilter: (p) => !!SYM_PERK_BY_ID[p.id] || E.isDeepCompat(p, r.pouch) });
        this._ensureSymPerkCard(off, symPool);   // 심볼퍽 최소 1장 보장(SYMAUG 동형)
        this._archSortOffer(off);                // 배치G: 전공 계열 dSym 퍽 표시 우선(순서만·확률 불변)
        r.options = off.options; r.offerMeta = { ...off.meta, deepPerk: true };
        if (!r.options.length) this._enterRewardDone("🛡️ 이번엔 새 심볼 유물이 없어요 — 다음 스테이지로!");
        break;
      }
    }
    return this.state();
  }

  // 심화 SYMAUG/SYMREL 혼합 오퍼 가드 — 오퍼가 전부 일반 퍽으로 채워지면 심볼퍽 최소 1장 보장.
  //  ★판별은 SYM_PERK_BY_ID 멤버십(접두 sa_/sr_ 금지 — 프리즘 심볼증강은 sp_ 라 접두 판별은 누락).
  //  후보 = 심볼퍽 풀(symPool) 중 미보유·오퍼 미포함. 동티어(off.meta.tier) 우선, 없으면 아무 티어(보장이 순수성보다 우선).
  //  교체 대상 = setTag 없는 첫 카드(세트조각 카드 보존). 후보 전무(심볼퍽 소진)면 그대로 통과(면제).
  //  r.deepMode 경로(SYMAUG/SYMREL case)에서만 호출 — 일반 오퍼 무접촉.
  _ensureSymPerkCard(off, symPool) {
    const r = this.run;
    if (!off.options.length || off.options.some((o) => SYM_PERK_BY_ID[o.id])) return;
    const inOffer = new Set(off.options.map((o) => o.id));
    const avail = symPool.filter((p) => !r.perks.includes(p.id) && !inOffer.has(p.id));
    if (!avail.length) return;
    const sameTier = avail.filter((p) => p.t === off.meta.tier);
    const chosen = this.rng.pick(sameTier.length ? sameTier : avail);
    let i = off.options.findIndex((o) => !o.setTag);
    if (i < 0) i = 0;
    off.options[i] = { ...chosen };
  }

  // 배치G 오퍼 시너지 — 이미 생성된 오퍼(off.options) 표시 순서만 재정렬: 활성(또는 share≥20% 근접) 전공 계열의
  //  dSym 퍽 1장을 맨 앞으로. ★확률 조작 아님(어떤 퍽이 나올지·개수·티어는 offerPerks 가 이미 결정) — UI 우선노출뿐.
  //   setTag(세트조각) 카드는 위치 보존(맨 뒤 계약 유지). r.deepMode SYMAUG/SYMREL case 에서만 호출.
  _archSortOffer(off) {
    const r = this.run;
    const arch = E.pouchArchetype(r.pouch);
    if (!arch.family || arch.share < 0.20 || !off.options || off.options.length < 2) return;   // 근접(≥20%) 이상만
    const isArchDSym = (o) => { const src = AUG_BY_ID[o.id] || REL_BY_ID[o.id]; return !!(src && src.dSym && E.familyRefMatchesArch(src.dSym, arch.family)); };
    const i = off.options.findIndex((o) => !o.setTag && isArchDSym(o));
    if (i > 0) { const [pick] = off.options.splice(i, 1); off.options.unshift(pick); }   // 세트조각 아닌 전공 퍽을 선두로
  }

  pickPerk(idx) {
    const r = this.run; const p = r.options[idx]; if (!p) return this.state();
    // V3P4: 3스테이지 증강 연계 보너스 심볼 선택 처리.
    if (r._pickKind === "SYNAUG_BONUS") {
      r._synergyBonusPending = false;
      if (p.type === "skip") { r.coins += 5; this._enterRewardDone(`⏭ 연계 보너스 건너뜀 — 코인 +5`); return this.state(); }
      if (p.type === "special" && p.id) {
        // 심볼 추가(비용 없음 — 연계 선물). 덱 최대치 초과 시 코인으로 환원.
        const bounds = E.repairBounds(this._repairState());
        const next = E.applySymbolReward(r.pouch, { type: "add", id: p.id, n: 1 });
        if (E.pouchValidate(next, bounds).ok) {
          r.pouch = next; this._checkArchetype();
          this._enterRewardDone(`🔗 연계 심볼 획득: ${p.e}${p.n}!`);
        } else {
          r.coins += 5; this._enterRewardDone(`🔗 덱 초과 — 코인 +5`);
        }
      } else { this._enterRewardDone("연계 처리 완료"); }
      return this.state();
    }
    // §3 Step 2: 심화 도박 선택지 처리.
    if (r._pickKind === "GAMBLE_DEEP") {
      if (p.id === "gamble_coin") {
        if (this.rng.double() < 0.5) { r.coins *= 2; this._enterRewardDone("🎲 도박 성공 — 코인 2배!"); }
        else { this._enterRewardDone("🎲 도박 실패 — 코인 유지"); }
      } else if (p.id === "gamble_sym" && r.pouch) {
        // 심볼 도박: 50% 보유 기본계열 랜덤 +1 / 50% 해골 +1 (applySymbolReward 재사용).
        const baseFamilies = ["cherry", "book", "star", "gem", "coin", "flame", "magnet", "bomb"];
        const heldBase = baseFamilies.filter((id) => (r.pouch[id] || 0) > 0);
        const bounds = E.repairBounds(this._repairState());
        if (this.rng.double() < 0.5 && heldBase.length > 0) {
          const chosen = this.rng.pick(heldBase); const sym = SYM_BY_ID[chosen];
          const rw = { type: "add", id: chosen, n: 1 };
          const next = E.applySymbolReward(r.pouch, rw);
          if (E.pouchValidate(next, bounds).ok) { r.pouch = next; this._checkArchetype(); this._enterRewardDone(`🎰 심볼 도박 성공 — ${sym ? sym.e + sym.n : chosen} +1`); }
          else { this._enterRewardDone("🎰 심볼 도박 — 총량 초과로 실패"); }
        } else {
          const rw = { type: "add", id: "skull", n: 1 };
          const next = E.applySymbolReward(r.pouch, rw);
          if (E.pouchValidate(next, bounds).ok) { r.pouch = next; this._checkArchetype(); this._enterRewardDone("🎰 심볼 도박 실패 — ☠해골 +1"); }
          else { this._enterRewardDone("🎰 심볼 도박 — 총량 초과로 해골 추가 불가"); }
        }
      }
      return this.state();
    }
    // §3 Step 3: 심화 휴식 선택지 처리.
    if (r._pickKind === "REST_DEEP") {
      if (p.id === "rest_coin") { r.coins += 12; this._enterRewardDone("🛌 휴식 — 코인 +12 획득"); }
      else if (p.id === "rest_purify" && r.pouch) {
        // 해골 계열(skull 우선, 없으면 skull_black) 1개 제거.
        const bounds = E.repairBounds(this._repairState());
        const target = (r.pouch.skull || 0) > 0 ? "skull" : "skull_black";
        if ((r.pouch[target] || 0) > 0) {
          const rw = { type: "remove", id: target, n: 1 };
          const next = E.applySymbolReward(r.pouch, rw);
          if (E.pouchValidate(next, bounds).ok) {
            r.pouch = next; this._checkArchetype();
            const sym = SYM_BY_ID[target];
            this._enterRewardDone(`🕊️ 정화 — ${sym ? sym.e + sym.n : target} 1개 제거`);
          } else { r.coins += 12; this._enterRewardDone("🛌 정화 실패(총량 하한) — 코인 +12 대신 지급"); }
        } else { r.coins += 12; this._enterRewardDone("🛌 해골 없음 — 코인 +12 대신 지급"); }
      }
      return this.state();
    }
    // ── V3P2: POUCH_COST — 프리즘 교체 비용 방식 선택 (기본2개 or 저주+1) ──
    if (r._pickKind === "POUCH_COST") {
      const ps = r._pendingSpecial;
      if (!ps) return this.state();
      const sym = SYM_BY_ID[ps.id] || {};
      if (p.type === "cost_remove") {
        // 기본 이득 심볼 제거 선택지로 전환
        const baseSymbols = Object.entries(r.pouch)
          .filter(([id, n]) => n > 0 && E.isAutoDecayTarget(id))
          .map(([id, n]) => ({ id, n }));
        if (baseSymbols.length === 0) {
          // 기본 이득 없으면 무료 추가(PRISM 예외: 기본 없으면 비용 생략)
          const draft = { ...r.pouch, [ps.id]: (r.pouch[ps.id] || 0) + 1 };
          r.pouch = draft; this._checkArchetype();
          r.deepPity = { id: ps.id, spinsLeft: 2 };
          r._pendingSpecial = null; r._pickKind = null;
          if (r.deepStats) r.deepStats.rewardsPicked += 1;
          this._enterRewardDone(`🌈 ${sym.e || ""}${sym.n || ps.id} 획득! (기본 심볼 없어 무료 추가)`);
          return this.state();
        }
        ps.mode = "remove"; ps.removeN = 2;
        r._pickKind = "POUCH_REMOVE"; r.options = baseSymbols;
        return this.state();
      }
      if (p.type === "cost_curse") {
        // 저주 심볼 +1 추가 후 특수 획득
        const bounds = E.repairBounds(this._repairState());
        const draftCurse = { ...r.pouch, skull: (r.pouch.skull || 0) + 1 };
        const draftFull = { ...draftCurse, [ps.id]: (draftCurse[ps.id] || 0) + 1 };
        const pv = E.pouchValidate(draftFull, bounds);
        if (pv.ok) {
          r.pouch = draftFull; this._checkArchetype();
          r.deepPity = { id: ps.id, spinsLeft: 2 };
          r._pendingSpecial = null; r._pickKind = null;
          if (r.deepStats) r.deepStats.rewardsPicked += 1;
          this._enterRewardDone(`🌈 ${sym.e || ""}${sym.n || ps.id} 획득! (☠해골 +1 저주 경로)`);
        } else {
          // 총량 초과: 저주 경로 불가, 취소
          r._pendingSpecial = null; r._pickKind = null;
          this._enterRewardDone("⚠️ 저주 경로 총량 초과 — 교체 취소됨");
        }
        return this.state();
      }
      return this.state();
    }

    // ── V3P2: POUCH_REMOVE — 제거할 기본 심볼 선택 & 원자적 커밋 ──
    if (r._pickKind === "POUCH_REMOVE") {
      const ps = r._pendingSpecial;
      if (!ps) return this.state();
      const sym = SYM_BY_ID[ps.id] || {};
      const remSym = SYM_BY_ID[p.id] || {};
      const bounds = E.repairBounds(this._repairState());
      // 원자적 draft — 제거 후 특수 추가
      const draft = { ...r.pouch };
      const remAmt = Math.min(ps.removeN || 1, draft[p.id] || 0);
      if (remAmt > 0) {
        draft[p.id] = (draft[p.id] || 0) - remAmt;
        if (draft[p.id] <= 0) delete draft[p.id];
      }
      draft[ps.id] = (draft[ps.id] || 0) + 1;
      const pv = E.pouchValidate(draft, bounds);
      if (pv.ok) {
        r.pouch = draft; this._checkArchetype();
        r.deepPity = { id: ps.id, spinsLeft: 2 };
        r._pendingSpecial = null; r._pickKind = null;
        if (r.deepStats) r.deepStats.rewardsPicked += 1;
        this._enterRewardDone(`🎒 ${sym.e || ""}${sym.n || ps.id} +1 / ${remSym.e || ""}${remSym.n || p.id} -${remAmt}`);
      } else {
        // 롤백(pouchValidate 실패)
        r._pendingSpecial = null; r._pickKind = null;
        this._enterRewardDone("⚠️ 주머니 규칙 위반 — 교체 취소됨");
      }
      return this.state();
    }

    // §9.2 J3 v1(2026-07-10): JACKPOT 노드는 이제 r._pickKind="POUCH" 로 생성되므로(위 selectNode case
    //  "JACKPOT" 참고) 별도 분기 없이 바로 아래 POUCH 처리로 흘러들어간다(§2 교체비용 플로우 재사용).
    //  구 "JACKPOT_NODE" 전용 분기(무료 add 카드)는 공짜·무검증 획득 버그였음 — 은퇴.

    if (r._pickKind === "POUCH") {   // 심화모드: 주머니(심볼 덱) 보상 적용
      // ── V3P2: skip 카드 — 코인 +5 즉시 ──
      if (p.type === "skip") {
        r.coins = (r.coins || 0) + 5;
        if (r.deepStats) r.deepStats.rewardsPicked += 1;
        this._enterRewardDone("⏭ 건너뛰기 — 코인 +5");
        return this.state();
      }
      // ── V3P2: 특수 심볼 카드 픽 — 2-step 교체 플로우 ──
      if (p.type === "special" && r.pouch) {
        const tier = p.tier || "SILVER";
        const sym = SYM_BY_ID[p.id] || {};
        if (tier === "CURSE") {
          // 저주 특수: 무료 추가, DECK_MAX만 체크
          const bounds = E.repairBounds(this._repairState());
          const draft = { ...r.pouch, [p.id]: (r.pouch[p.id] || 0) + 1 };
          if (E.pouchTotal(draft) <= (bounds.totalMax ?? DEEP.DECK_MAX)) {
            r.pouch = draft; this._checkArchetype();
            r.deepPity = { id: p.id, spinsLeft: 2 };
            if (r.deepStats) r.deepStats.rewardsPicked += 1;
            this._enterRewardDone(`☠ ${sym.e || ""}${sym.n || p.id} 저주 카드 추가 (무료)`);
          } else {
            this._enterRewardDone("☠ 저주 카드 추가 실패 (덱 만원)");
          }
          return this.state();
        }
        // 비저주 특수: 기본 이득 심볼 목록 생성
        const baseSymbols = Object.entries(r.pouch)
          .filter(([id, n]) => n > 0 && E.isAutoDecayTarget(id))
          .map(([id, n]) => ({ id, n }));
        // 기본 이득 0개: 바로 추가(교체 대상 없음)
        if (baseSymbols.length === 0) {
          const bounds = E.repairBounds(this._repairState());
          const draft = { ...r.pouch, [p.id]: (r.pouch[p.id] || 0) + 1 };
          const pv = E.pouchValidate(draft, bounds);
          if (pv.ok) {
            r.pouch = draft; this._checkArchetype();
            r.deepPity = { id: p.id, spinsLeft: 2 };
            if (r.deepStats) r.deepStats.rewardsPicked += 1;
            this._enterRewardDone(`🎒 ${sym.e || ""}${sym.n || p.id} 추가 (기본 이득 없어 무료)`);
          } else {
            this._enterRewardDone(`⚠️ ${sym.n || p.id} 추가 실패 (상한 초과)`);
          }
          return this.state();
        }
        const cost = p.cost || {};
        // 골드: 기본 이득 총량 < 3이면 1개로 완화
        const baseTotal = baseSymbols.reduce((s, x) => s + x.n, 0);
        const removeN = tier === "SILVER" ? 1
          : tier === "GOLD" ? (baseTotal < 3 ? 1 : (cost.removeN || 2))
          : (cost.removeN || 2);  // PRISM

        if (tier === "PRISM") {
          // 프리즘: 비용 방식 선택 먼저
          r._pendingSpecial = { id: p.id, e: sym.e, n: sym.n, tier, mode: "choose_cost", removeN };
          r._pickKind = "POUCH_COST";
          r.options = [
            { type: "cost_remove", removeN: 2, e: "🗑️", n: `기본 심볼 2개 제거` },
            { type: "cost_curse",  e: "☠", n: `저주 심볼 +1 추가` }
          ];
          return this.state();
        }
        // 실버/골드: 바로 제거 선택
        r._pendingSpecial = { id: p.id, e: sym.e, n: sym.n, tier, mode: "remove", removeN };
        r._pickKind = "POUCH_REMOVE";
        r.options = baseSymbols;
        return this.state();
      }
      // ── V3P2 dormant: v2 randpack/package/add/remove/upgrade/swap 경로 ──
      // (정비소 서비스가 기능 커버 — 코드 보존·비활성. 삭제 금지)
      // ── 배치 D §6.1: randpack 처리 — 픽 시점에 분포 롤(결과 공개).
      if (p.type === "randpack") {
        const dist = p.dist || (DEEP.RANDPACK_DIST || [0.30, 0.50, 0.20]);
        const count = p.n || (DEEP.RANDPACK_COUNT || 2);
        const bounds = E.repairBounds(this._repairState());
        const unlockedSet = this._symUnlockedSet();
        // 해금되고 저주 아닌 심볼 풀(tier별 분리)
        const symByTier = { SILVER: [], GOLD: [], PRISM: [] };
        for (const id of POUCH_SYMBOLS) {
          if (!unlockedSet || unlockedSet.has(id)) {
            const rar = POUCH_RARITY[id] || "기본";
            if (rar === "저주") continue;   // CURSE 제외
            const tier = TIER_BY_RARITY[rar] || "SILVER";
            if (symByTier[tier]) symByTier[tier].push(id);
          }
        }
        // 독립 count 롤 → 티어 결정. 둘 다 SILVER면 1개 GOLD 승격(count=2 기준).
        const rollTier = (rn) => {
          const s = dist[0], sg = s + dist[1];
          if (rn < s) return "SILVER";
          if (rn < sg) return "GOLD";
          return "PRISM";
        };
        const tiers = [];
        for (let i = 0; i < count; i++) tiers.push(rollTier(this.rng.double()));
        // 둘 다 SILVER이면 첫 번째 GOLD 승격(§6.1 보장: "골드 이상 1개")
        if (count >= 2 && tiers.every((t) => t === "SILVER")) tiers[0] = "GOLD";
        // 각 롤마다 폴백(해당 티어 부재 시 하위 티어로). PRISM→GOLD→SILVER 순.
        const fallback = (tier) => {
          const order = ["PRISM", "GOLD", "SILVER"];
          for (const t of order) {
            const pool = symByTier[t];
            if (tier === t || (order.indexOf(t) >= order.indexOf(tier)) && symByTier[t].length) {
              if (symByTier[t].length) return { tier: t, pool: symByTier[t] };
            }
          }
          return { tier: "SILVER", pool: symByTier.SILVER };
        };
        // 재롤 최대 10회(상한 초과 시 하한 폴백)
        const picked = [];
        let pityId = null;
        for (let i = 0; i < count; i++) {
          const { tier: rTier, pool: rPool } = fallback(tiers[i]);
          let chosen = null;
          for (let attempt = 0; attempt < 10; attempt++) {
            const candidate = this.rng.pick(rPool.length ? rPool : symByTier.SILVER);
            if (!candidate) break;
            // pouchValidate: 1개 추가 시 상한 초과하면 재롤
            const testPouch = { ...r.pouch };
            testPouch[candidate] = (testPouch[candidate] || 0) + 1;
            const pv = E.pouchValidate(testPouch, bounds);
            if (pv.ok) { chosen = candidate; break; }
          }
          if (!chosen) {
            // 폴백: 하한 심볼 중 상한 미초과인 것 선택
            const any = symByTier.SILVER.find((id) => {
              const t2 = { ...r.pouch }; t2[id] = (t2[id] || 0) + 1;
              return E.pouchValidate(t2, bounds).ok;
            });
            chosen = any || null;
          }
          if (chosen) {
            r.pouch = E.applySymbolReward(r.pouch, { type: "add", id: chosen, n: 1 });
            picked.push(chosen);
            if (!pityId && chosen !== "empty" && chosen !== "random" && SYM_BY_ID[chosen]) pityId = chosen;
          }
        }
        // pity 연동 — 첫 유효 심볼
        if (pityId) r.deepPity = { id: pityId, spinsLeft: 2 };
        // deepPity 연동 통보 + 결과 공개 토스트(심볼명 나열)
        const resultNames = picked.map((id) => `${SYM_BY_ID[id] ? SYM_BY_ID[id].e + SYM_BY_ID[id].n : id}`).join(" · ");
        if (r.deepStats) r.deepStats.rewardsPicked += 1;
        this._enterRewardDone(`🎲 랜덤팩 — ${resultNames || "빈 결과"}`);
        return this.state();
      }
      // 배치 H — 패키지 카드(type:"package") 는 ops 를 원자적 적용(applySymbolPackage). 사전검증 통과분만 오퍼됐으나
      //  방어적으로 ok 일 때만 커밋(실패 시 주머니 불변). pity 대표심볼 = repId(add op 우선). 그 외는 단품 applySymbolReward.
      if (p.type === "package") {
        const res = E.applySymbolPackage(r.pouch, p.ops, E.repairBounds(this._repairState()));
        if (res.ok) r.pouch = res.pouch;
        const pid = p.repId;
        if (pid && pid !== "empty" && pid !== "random" && SYM_BY_ID[pid]) r.deepPity = { id: pid, spinsLeft: 2 };
      } else {
        r.pouch = E.applySymbolReward(r.pouch, p);
        // 배치F P2: add/upgrade 채택 → 신규심볼 pity 설정(덮어쓰기=최신 우선). upgrade 의 신규=상위 심볼(POUCH_UPGRADE).
        //  가드: empty/random(강제 등장 불가)·SYM_BY_ID 부재 심볼은 미설정.
        if (p.type === "add" || p.type === "upgrade") {
          const pid = p.type === "upgrade" ? POUCH_UPGRADE[p.id] : p.id;
          if (pid && pid !== "empty" && pid !== "random" && SYM_BY_ID[pid]) r.deepPity = { id: pid, spinsLeft: 2 };
        }
      }
      if (r.deepStats) r.deepStats.rewardsPicked += 1;
      // [LOW-3] 희귀사냥(sp_rare_hunt) 저주 동반(근사) — 희귀 등급 심볼 add 채택 시 curseChance 확률로 불운게이지 +1.
      const spq = this._symMods();
      if (spq && spq.curseChance > 0 && p.type === "add" && (POUCH_RARITY[p.id] || "기본") === "희귀"
          && this.rng.double() < Math.min(1, spq.curseChance)) {
        r.unluckyGauge = Math.min(C.UNLUCKY_MAX, r.unluckyGauge + 1);
        this.toast("🏹 희귀사냥 — 희귀 심볼에 저주 기운이 붙었어요 (불운게이지 +1)");
      }
      this._enterRewardDone(`🎒 주머니 갱신 — ${this._pouchRewardLabel(p)}`);
      return this.state();
    }
    if (r._pickKind === "LVL") {   // 증강 레벨업(보유 증강 강화)
      r.perkLevels[p.id] = Math.min(3, (r.perkLevels[p.id] || 1) + 1);
      this._enterRewardDone(`⬆️ ${p.e} ${p.n} Lv.${r.perkLevels[p.id]} 강화 완료!`);
      return this.state();
    }
    r.perks.push(p.id);
    if (r._pickKind === "AUG" && AUG_BY_ID[p.id]?.t === "PRISM") {
      r.stats.prismPicks += 1;
      if ((r.asc || 0) >= 7) { const c = this.rng.pick(CURSES); r.curses.push(c.id); this.toast(`⚠️ 심화 규칙 — 프리즘엔 저주가 따라와요: ${c.e}${c.n}(${c.d})`); }
    }
    // V3P4: 3스테이지 증강 연계(deepMode·SYMAUG 경로) — (stage-1)%3===0 시 태그 일치 특수 심볼 보너스 후보 1개.
    //  pickPerk 진입 시 r.stage 는 이미 +1 됨(다음 스테이지 번호). 방금 클리어한 스테이지 = r.stage-1.
    if (r.deepMode && r._pickKind === "AUG" && (r.stage - 1) % 3 === 0 && r.stage - 1 > 0) {
      const augInfo = AUG_BY_ID[p.id] || SYM_AUG_BY_ID[p.id];
      const augTags = (augInfo && augInfo.dSym) ? (SYM_BY_ID[augInfo.dSym]?.tags || []) : [];
      const augFamilyTag = augTags[0];   // 1차 태그(배율/점수/조작 등)
      const bonusCandidates = POUCH_SYMBOLS.filter((sid) => {
        const cat = POUCH_CAT[sid] || "special";
        if (cat !== "special") return false;                               // base/harmful 제외
        if (r.pouch && (r.pouch[sid] || 0) > 0) return false;            // 이미 보유 제외(중복 방지)
        const unlocked = this._symUnlockedSet();
        if (!unlocked.has(sid)) return false;                              // 미해금 제외
        if (!augFamilyTag) return true;                                    // 태그 미정 → 전체 특수
        const symTags = SYM_BY_ID[sid]?.tags || [];
        return symTags.includes(augFamilyTag);
      });
      if (bonusCandidates.length > 0) {
        // 태그 일치 후보 중 랜덤 1개 → 미니 오퍼(skip 포함 2택).
        // ★카드 필드 계약 = engine.offerSymbolRewards v3 special/skip 카드와 동일(ui renderPouchReward 재사용).
        //  연계 선물이라 비용 없음 → cost:{free:true} (엔진의 무비용 표현·costDesc "무료 추가" 렌더 정합).
        const bonusSym = this.rng.pick(bonusCandidates);
        const bsym = SYM_BY_ID[bonusSym];
        const tier = TIER_BY_RARITY[POUCH_RARITY[bonusSym] || "고급"] || "SILVER";
        const bonusOption = { type: "special", id: bonusSym, tier, e: bsym?.e || "❔", n: bsym?.n || bonusSym, cost: { free: true } };
        const skipOption = { type: "skip", tier: "SILVER", coinBonus: 5, e: "⏭", n: "선택하지 않기" };
        r._synergyBonusPending = true;
        r.phase = PHASE.PERK_PICK; r._pickKind = "SYNAUG_BONUS";
        r.options = [bonusOption, skipOption];
        r.offerMeta = { synergyBonus: true, augId: p.id, tag: augFamilyTag, total: E.pouchTotal(r.pouch) };
        this.toast(`🔗 3스테이지 증강 연계! 태그 [${augFamilyTag || "전체"}] 특수 심볼 보너스`);
        return this.state();
      }
    }
    this._enterRewardDone(`${p.e} ${p.n} 획득!`);
    return this.state();
  }

  // 심화모드 주머니 보상 1건의 짧은 요약 라벨(토스트/인트로 메시지용).
  _pouchRewardLabel(rw) {
    const e = (id) => (SYM_BY_ID[id] ? SYM_BY_ID[id].e : (id === "empty" ? "▫" : id === "random" ? "🎲" : id));
    const nm = (id) => (SYM_BY_ID[id] ? SYM_BY_ID[id].n : (id === "empty" ? "빈칸" : id === "random" ? "랜덤칸" : id));
    if (!rw) return "";
    switch (rw.type) {
      // V3P2 신규 타입
      case "special": return `${rw.e || e(rw.id)}${rw.n || nm(rw.id)} 획득 (${rw.tier || "SILVER"})`;
      case "skip": return "⏭ 건너뛰기 — 코인 +5";
      case "cost_remove": return `기본 심볼 ${rw.removeN || 1}개 제거 선택`;
      case "cost_curse": return "☠ 저주 심볼 +1 추가";
      // v2 dormant 타입 (정비소 커버)
      case "package": return `📦 ${rw.title || "패키지"} — ${rw.desc || ""}`;   // 배치 H dormant
      case "add": return `${e(rw.id)}${nm(rw.id)} +${rw.n} 추가`;
      case "remove": return `${e(rw.id)}${nm(rw.id)} -${rw.n} 제거`;
      case "swap": return `${e(rw.from)}${nm(rw.from)} → ${e(rw.to)}${nm(rw.to)} ${rw.n}개 교체`;
      case "upgrade": return `${e(rw.id)}${nm(rw.id)} ${rw.n}개 → 상위 등급으로!`;
      default: return "";
    }
  }

  // 현재 주머니 현황(UI 시트용) — 심볼별 개수/비중/희귀도/태그 + 총량/종류/압축패널티/유효성.
  pouchView() {
    const r = this.run; if (!r || !r.deepMode || !r.pouch) return null;
    const total = E.pouchTotal(r.pouch);
    // 배치 B: 영구 perk bias 반영 유효% — buildMods(NEXTSPIN 아이템 제외, 영구 perk만)로 bias 조립.
    //  _mods()는 r.armItems(NEXTSPIN 포함) 기준이므로, lever = armItems 에서 NEXTSPIN 제외 영구분만 전달.
    //  (실제 buildMods는 perkIds/relicIds 기반이라 lever 인자는 PHASE/NEXTSPIN 아이템에만 영향 — 여기선 생략)
    const permMods = this._mods([]);   // 아이템 미전달 = 영구 perk bias(증강·유물·캐릭 등)
    const hasMul = permMods.symbolWeightMul && Object.keys(permMods.symbolWeightMul).length > 0;
    const hasAdd = permMods.weightAdd && Object.keys(permMods.weightAdd).length > 0;
    const hasRare = permMods.rareWeightMul != null && permMods.rareWeightMul !== 1;
    const bias = (hasMul || hasAdd || hasRare) ? {
      mul: hasMul ? permMods.symbolWeightMul : undefined,
      add: hasAdd ? permMods.weightAdd : undefined,
      rareMul: hasRare ? permMods.rareWeightMul : undefined,
    } : undefined;
    const biasData = bias ? E.pouchEffWeights(r.pouch, bias) : null;
    const biasChanged = biasData ? biasData.biasChanged : new Set();
    const effTotal = biasData ? biasData.effTotal : total;
    const effWeightById = biasData ? Object.fromEntries(biasData.entries.map((e) => [e.id, e.effWeight])) : {};
    const entries = Object.entries(r.pouch).filter(([, n]) => n > 0)
      .sort((a, b) => b[1] - a[1])
      .map(([id, n]) => {
        const s = SYM_BY_ID[id];
        const eff = effTotal > 0 && biasData ? (effWeightById[id] ?? n) / effTotal : (total > 0 ? n / total : 0);
        return {
          id, n, ratio: total > 0 ? n / total : 0,
          effRatioBias: eff,           // 영구 perk bias 반영 유효비중(UI 확률% 표시)
          biasChanged: biasChanged.has(id),  // 🧪 마커 여부
          e: s ? s.e : (id === "empty" ? "▫" : id === "random" ? "🎲" : "❔"),
          nm: s ? s.n : (id === "empty" ? "빈칸" : id === "random" ? "랜덤칸" : id),
          rarity: POUCH_RARITY[id] || "기본",
          tags: s ? (s.tags || []) : [],
          // §10 V3 덱 보드 카드용 확장 필드(추가만 — 위 기존 필드는 무변경, 하네스 소비 무회귀).
          tier: E.symTierOf(id),           // SILVER|GOLD|PRISM|CURSE
          cat: E.symCatOf(id),             // base|special|harmful
          use: POUCH_USE[id] || null,      // instant|fuse|null
          jackpotTag: E.jackpotTagOf(id),  // 태그 문자열|null
          autoDecay: E.isAutoDecayTarget(id),  // 15+ 자동 소멸 대상 여부
          desc: s ? symDeckDesc(s) : (id === "empty" ? "EXP 0 (빈칸)" : id === "random" ? "주머니 비중대로 재분배" : ""),  // 효과 1줄
        };
      });
    const bounds = E.repairBounds(this._repairState());   // 상점 확장/압축 반영 상·하한
    const valid = E.pouchValidate(r.pouch, bounds);
    const sp = this._symMods();
    // 압축패널티 표시 = _deepPenalty 와 동일 공식([HIGH-3①] 완화는 초과분에만·하한 1.0) — HUD/시트 일관.
    //  (보스요구+20%[전설수집]은 스테이지 요구치에만 적용·주머니 HUD엔 미포함 — 총량 관련 압축만 표시.)
    const baseP = E.compressionPenalty(total) * (1 + (r.deepCompressExtra || 0));
    const penalty = sp ? Math.max(1, 1 + (baseP - 1) * sp.penaltyMul) : baseP;
    // [MED-4] 낡은통계표(sr_stat_table) — 🎲랜덤칸 재분배(실심볼 비중대로 재추첨)를 반영한 실효 등장확률.
    //  실효비중 = (n + 랜덤칸수×n/실심볼총량) / 총량. 랜덤칸 자신은 0(그 모습으론 안 착지), 빈칸은 그대로.
    const statTable = !!(sp && sp.statTable);
    if (statTable && total > 0) {
      const nRandom = r.pouch.random || 0;
      const realTotal = total - nRandom - (r.pouch.empty || 0);
      for (const en of entries) {
        en.effRatio = en.id === "random" ? 0
          : en.id === "empty" ? en.ratio
          : (en.n + (realTotal > 0 ? nRandom * en.n / realTotal : 0)) / total;
      }
    }
    // 배치G: 전공(계열 아키타입) 현황 — 활성/최근접 계열 + 다음 임계까지 게이지. HUD/시트 표시(pct2 헬퍼).
    const arch = E.pouchArchetype(r.pouch);
    const nextT = arch.tier >= 2 ? null : (arch.tier >= 1 ? DEEP.ARCH_T2 : DEEP.ARCH_T1);   // 다음 목표 임계(t2 도달 시 없음)
    return {
      entries, total, kinds: entries.length,
      penalty, valid, statTable,
      totalMax: bounds.totalMax, totalMin: bounds.totalMin,
      tagBuff: { ...(r.deepTagBuff || {}) },   // 태그강화 현황(UI 표시)
      symPerks: this._heldSymPerks().map((id) => { const pk = SYM_PERK_BY_ID[id]; return { id, e: pk.e, n: pk.n, t: pk.t, d: pk.d }; }),   // 보유 심볼증강/유물(UI)
      archetype: arch.family ? { family: arch.family, e: arch.e, n: arch.n, tier: arch.tier, share: arch.share, nextThreshold: nextT, metric: arch.metric } : null,
    };
  }

  _randomEvent() {
    const r = this.run; const c = this.rng.n(10); let msg;
    if (c === 0) { r.coins += 15; msg = "🎁 이벤트 — 코인 +15"; }
    else if (c === 1) { r.score += 200; msg = "🎁 이벤트 — 점수 +200"; }
    else if (c === 2) { r.coins += 30; msg = "🎁 이벤트 — 코인 +30"; }
    else if (c === 3) { r.score += 100; r.coins += 12; msg = "🎁 이벤트 — 점수 +100·코인 +12"; }
    else if (c === 4) { r.coins += 20; msg = "🎁 이벤트 — 코인 +20"; }
    else if (c === 5) { const it = this.rng.pick(ITEMS_NEXTSPIN); this._giveItem(it.id); msg = `🎁 이벤트 — 아이템 ${it.e}${it.n}`; }
    else if (c === 6) { const d = E.pickDevices(this.rng, r.stage, new Set(r.curses), 1)[0]; if (d && !this.profile.ownedDevices.includes(d.id)) { this.profile.ownedDevices.push(d.id); this._saveProfile(); } if (d && !r.device) r.device = d.id; msg = `🎁 이벤트 — 장치 ${d ? d.e + d.n : "?"} 획득`; }
    // c=7/8: 심화(deepMode)는 관련성 필터(deepCompatPool) 통과분에서 pick — 빈 풀이면 c=2형 코인 지급 폴백(§3e).
    //  기준풀은 일반과 동일 raw 카탈로그(unlockLevel·보유중복 미적용 기존 quirk 보존·스코프 확대 금지).
    //  일반모드는 pool=원본 배열 그대로 rng.pick(소비 순서 포함 원문 동작·무회귀).
    else if (c === 7) { const pool = r.deepMode ? E.deepCompatPool(RELICS, r.pouch) : RELICS; if (!pool.length) { r.coins += 30; msg = "🎁 이벤트 — 코인 +30"; } else { const rel = this.rng.pick(pool); r.perks.push(rel.id); msg = `🎁 이벤트 — 유물 ${rel.e}${rel.n}`; } }
    else if (c === 8) { const pool = r.deepMode ? E.deepCompatPool(AUGMENTS, r.pouch) : AUGMENTS; if (!pool.length) { r.coins += 30; msg = "🎁 이벤트 — 코인 +30"; } else { const a = this.rng.pick(pool); r.perks.push(a.id); msg = `🎁 이벤트 — 증강 ${a.e}${a.n}`; } }
    else { if (r.curses.length) { r.curses.pop(); msg = "🎁 이벤트 — 저주 1개 정화"; } else { r.coins += 10; msg = "🎁 이벤트 — 코인 +10"; } }
    this._enterRewardDone(msg);
  }
  _giveItem(id) { const r = this.run; r.seenItems.add(id); const cap = 3 + ascMods(r.asc).itemCapDelta + (this._mods().itemCapBonus || 0); if (r.items.length < cap) r.items.push(id); else r.coins += 5; }

  // ── 상점 ──
  _openShop() {
    const r = this.run; r.phase = PHASE.SHOP; r.shopItems = this._freshShop(); r.shopBought = [];
    // Phase 4: 영수증/쿠폰/장바구니는 상점을 여는 이번 1회에만 반영 → 진열 생성 직후 소진(reroll 재적용 방지).
    if (r.deepMode) { r.deepShopDiscount = false; r.deepShopCoupon = false; r.deepShopSlotBonus = 0; }
    // ── V3P4: 수정구(crystal·fuse) — 상점 진입 시 deepCrystalPending 소비 → deepRewardBonus 추가.
    if (r.deepMode && r.deepCrystalPending > 0) {
      r.deepRewardBonus = Math.min(2, (r.deepRewardBonus || 0) + r.deepCrystalPending);
      r.deepCrystalPending = 0;
      this.toast("🔮 수정구 효과 — 다음 주머니 보상 후보 +1");
    }
    // V3P4: 검은카드(black_card·fuse) — 상점 진입 시 덱에 있으면 1개 무료 제공 후 소비.
    if (r.deepMode && r.pouch && (r.pouch.black_card || 0) > 0) {
      r._blackCardShopFree = true;  // _freshShop/_shopBuy 에서 첫 상품 1개 무료 처리
      r.pouch = E.applySymbolReward(r.pouch, { type: "remove", id: "black_card", n: 1 });
      this._checkArchetype();
      this.toast("💳 검은카드 — 이번 상점 1개 무료");
    }
  }
  _freshShop() {
    const r = this.run;
    const sm = this._mods();
    // Phase 4(심화 전용): 🧾영수증 = 이번 상점 전체 -10%(pm ×0.9). 🛒장바구니 = 상품칸 +N. 일반모드는 플래그 항상 false/0.
    const receiptMul = (r.deepMode && r.deepShopDiscount) ? 0.9 : 1;
    let pm = Math.max(0.4, ascMods(r.asc).shopPriceMul * (sm.shopPriceMul || 1) * receiptMul);   // 승천 상승 × 증강 할인 × 영수증(하한 -60%)
    const itemPm = Math.max(0.4, pm * (sm.itemPriceMul || 1));
    const cartBonus = (r.deepMode ? (r.deepShopSlotBonus || 0) : 0);
    const slot = Math.max(0, Math.min(3, (sm.shopSlotBonus || 0) + cartBonus));         // VIP 등 상점 상품칸+ · 🛒장바구니
    // 심화(deepMode): 증강/유물 진열을 관련성 필터(deepCompatPool) 통과분으로만 — 죽은 D계열 퍽 진열 제거(§3d).
    //  compatFilter=세트조각 주입 누출 차단. 빈 풀=진열 축소 수용(items 잔존). 일반모드=원문 풀·무필터(무회귀).
    const compat = r.deepMode ? ((p) => E.isDeepCompat(p, r.pouch)) : undefined;
    const augSrc = r.deepMode ? E.deepCompatPool(this._augPool(), r.pouch) : this._augPool();
    const relSrc = r.deepMode ? E.deepCompatPool(this._relicPool(), r.pouch) : this._relicPool();
    const augs = E.offerPerks(augSrc, "AUGMENT", this.rng, new Set(r.perks), { clearedStage: r.stage - 1, compatFilter: compat }).options.slice(0, 2).map((p) => ({ kind: "A", ...p, price: Math.max(1, Math.round((p.t === "PRISM" ? 36 : p.t === "GOLD" ? 24 : 14) * pm)) }));
    const rels = E.offerPerks(relSrc, "RELIC", this.rng, new Set(r.perks), { clearedStage: r.stage - 1, compatFilter: compat }).options.slice(0, 2).map((p) => ({ kind: "R", ...p, price: Math.max(1, Math.round((p.price || 14) * pm)) }));
    // §3 Step 1: 심화(deepMode)는 완전 no-op 아이템 제외 풀에서 뽑음. 일반모드는 전체 풀 그대로(무회귀).
    const items = E.pickItems(this.rng, 2 + slot, r.deepMode).map((it) => ({ kind: "I", ...it, price: Math.max(1, Math.round(it.cost * itemPm)) }));
    const shop = [...augs, ...rels, ...items];
    // 🎟쿠폰(심화 전용) — 상품 1개(무작위) 추가 -15% 할인. 표기(couponTag)로 UI/도감에 노출.
    if (r.deepMode && r.deepShopCoupon && shop.length) {
      const ci = this.rng.n(shop.length); const o = shop[ci];
      o.price = Math.max(1, Math.round(o.price * 0.85)); o.couponTag = true;
    }
    return shop;
  }
  shopBuy(idx) {
    const r = this.run; const o = r.shopItems[idx]; if (!o) return this.state();
    if (o.id === "prism_ink" && r._prismInkBought) { this.toast("💧 프리즘 잉크는 런당 1회만 구매할 수 있어요"); return this.state(); }
    // V3P4: 검은카드(black_card·fuse) — 첫 번째 구매 무료(r._blackCardShopFree 플래그).
    const isFree = r._blackCardShopFree && r.deepMode;
    if (!isFree && r.coins < o.price) { this.toast(`코인 ${o.price - r.coins} 부족`); return this.state(); }
    if (!isFree) r.coins -= o.price;
    else { r._blackCardShopFree = false; this.toast("💳 검은카드 효과 — 무료 구매!"); }
    if (o.id === "prism_ink") r._prismInkBought = true;
    if (o.kind === "I") this._giveItem(o.id); else r.perks.push(o.id);
    (r.shopBought || (r.shopBought = [])).push(o.e + o.n);
    this.toast(`🛒 ${o.e}${o.n} 구매 완료`);
    r.shopItems.splice(idx, 1);   // 구매한 항목은 목록에서 제거
    return this.state();
  }
  shopReroll() { const r = this.run; const cost = Math.max(2, 6 + (this._mods().shopRerollDelta || 0)); if (r.coins < cost) { this.toast(`코인 ${cost} 부족`); return this.state(); } r.coins -= cost; r.shopItems = this._freshShop(); return this.state(); }

  // ── Phase 3: 심화모드 상점 '심볼 정비' 탭 ──────────────────────────────
  //  ★심화모드에서만 노출(UI 게이팅). 일반모드 상점 무영향. 코인 경제 shopBuy 와 동일 관례.

  // Phase 5: 심볼증강/유물에 의한 정비 서비스 가격 배수(kind 별). 심볼정리/정화수업/연구실단골/연구노트 등.
  //  repairMul["*"] = 전체 할인(연구실단골), repairMul[kind] = 특정 종류(remove/purify/upgrade …). 배수 곱.
  _repairPriceMul(kind) {
    const sp = this._symMods(); if (!sp) return 1;
    let mul = 1;
    if (sp.repairMul["*"]) mul *= sp.repairMul["*"];
    if (sp.repairMul[kind]) mul *= sp.repairMul[kind];
    return mul;
  }
  // 심볼증강 할인 반영된 정비 서비스 실가격(하한 1).
  _repairPrice(sv) { return Math.max(1, Math.round(sv.price * this._repairPriceMul(sv.kind))); }

  // 정비 서비스 목록(가격/설명/구매가능 여부·이유). UI 정비 탭 카드용.
  //  일부 서비스는 '대상 선택' 필요(targetPick) → UI 가 심볼/태그 선택 시트를 띄운 뒤 repairBuy 호출.
  //  price = 심볼증강 할인 반영 실가격, basePrice = 원가(할인 표시용).
  repairServices() {
    const r = this.run; if (!r || !r.deepMode) return [];
    const hasCurses = (r.curses || []).length > 0;
    return DEEP.REPAIR_SERVICES.filter((sv) => {
      // 배치 A Step 5: curse_cleanse 는 저주 보유 시만 노출.
      if (sv.kind === "curseCleanse") return hasCurses;
      return true;
    }).map((sv) => {
      const price = this._repairPrice(sv);
      return {
        id: sv.id, e: sv.e, n: sv.n, price, basePrice: sv.price, kind: sv.kind, desc: sv.desc,
        n2: sv.n2 || 0, pct: sv.pct || 0, targetPick: !!sv.targetPick,
        discounted: price < sv.price, afford: r.coins >= price,
      };
    });
  }

  // 특정 정비 서비스의 '대상 후보' 목록(UI 선택 시트용). kind 에 따라 다른 풀.
  //  addBasic/addHigh/addRare = 해당 희귀도의 POUCH_SYMBOLS 전체(주머니 미보유 포함=신규 추가 가능).
  //  remove/swap(from) = 현재 보유 심볼. swap(to) = POUCH_SYMBOLS 전체. upgrade = 보유한 업그레이드 가능 기본심볼.
  //  tagbuff = 현재 주머니에 존재하는 태그.
  repairTargets(serviceId, which = "id") {
    const r = this.run; if (!r || !r.deepMode) return [];
    const sv = DEEP.REPAIR_SERVICES.find((x) => x.id === serviceId); if (!sv) return [];
    const held = Object.entries(r.pouch).filter(([, n]) => n > 0).map(([id]) => id);
    const heldSet = new Set(held);
    const symInfo = (id) => ({
      id, n: r.pouch[id] || 0,
      e: SYM_BY_ID[id] ? SYM_BY_ID[id].e : (id === "empty" ? "▫" : id === "random" ? "🎲" : "❔"),
      nm: SYM_BY_ID[id] ? SYM_BY_ID[id].n : (id === "empty" ? "빈칸" : id === "random" ? "랜덤칸" : id),
      rarity: POUCH_RARITY[id] || "기본",
    });
    // Phase 5 해금 필터: add/swap-to 대상은 "해금된 심볼만"(remove/swap-from 은 이미 보유분이라 필터 불필요).
    const unlocked = this._symUnlockedSet();
    const isOpen = (id) => !unlocked || unlocked.has(id) || id === "empty" || id === "random";
    if (sv.kind === "addBasic" || sv.kind === "addHigh" || sv.kind === "addRare") {
      return POUCH_SYMBOLS.filter((id) => (POUCH_RARITY[id] || "기본") === sv.rarity && isOpen(id)).map(symInfo);
    }
    if (sv.kind === "remove") return held.map(symInfo);
    if (sv.kind === "upgrade") return held.filter((id) => POUCH_UPGRADE[id]).map(symInfo);
    if (sv.kind === "swap") {
      if (which === "to") return POUCH_SYMBOLS.filter(isOpen).map(symInfo);   // 교체 대상(B) = 해금된 풀
      return held.map(symInfo);                                              // 교체 원본(A) = 보유
    }
    if (sv.kind === "tagbuff") {
      const byTag = {};
      for (const [id, n] of Object.entries(r.pouch)) { if (n <= 0) continue; for (const t of (SYM_BY_ID[id]?.tags || [])) byTag[t] = (byTag[t] || 0) + n; }
      return Object.entries(byTag).sort((a, b) => b[1] - a[1]).map(([tag, cnt]) => ({ tag, cnt, buff: (r.deepTagBuff && r.deepTagBuff[tag]) || 0 }));
    }
    return [];
  }

  // 정비 서비스 구매 미리보기(UI 확인 시트용) — 코인/변화/유효성. 실제 적용 X.
  //  price = 심볼증강 할인 반영 실가격.
  repairPreview(serviceId, sel = {}) {
    const r = this.run; if (!r || !r.deepMode) return null;
    const sv = DEEP.REPAIR_SERVICES.find((x) => x.id === serviceId); if (!sv) return null;
    const res = E.applyShopService(sv, this._repairState(), sel);
    const price = this._repairPrice(sv);
    return {
      price, basePrice: sv.price, coins: r.coins, afford: r.coins >= price,
      ok: res.ok, error: res.error || "", changes: res.changes || [],
      preview: res.preview || null, kind: sv.kind, n2: sv.n2 || 0, pct: sv.pct || 0,
    };
  }

  // 정비 서비스 구매 적용 — 코인 확인 → 엔진 적용(규칙 위반 시 거부·미차감) → 상태 커밋.
  //  Phase 5 심볼증강/유물: 할인가격(_repairPrice) · 교체 코인보상/환급(교체전문가/교체영수증) · 정화 코인보상(정화붓).
  repairBuy(serviceId, sel = {}) {
    const r = this.run; if (!r || !r.deepMode) return this.state();
    const sv = DEEP.REPAIR_SERVICES.find((x) => x.id === serviceId);
    if (!sv) { this.toast("알 수 없는 서비스"); return this.state(); }
    const price = this._repairPrice(sv);
    if (r.coins < price) { this.toast(`🪙 코인 ${price - r.coins} 부족 — 살 수 없어요`); return this.state(); }
    const res = E.applyShopService(sv, this._repairState(), sel);
    if (!res.ok) { this.toast(`❌ ${res.error || "정비 실패"}`); return this.state(); }   // 규칙 위반=미차감 거부
    // 커밋: 코인 차감 + 정비 상태 반영(pouch/확장/압축/태그/저주).
    r.coins -= price;
    const nx = res.next;
    r.pouch = nx.pouch;
    r.deepTotalMaxDelta = nx.totalMaxDelta;
    r.deepTotalMinDelta = nx.totalMinDelta;
    r.deepCompressExtra = nx.compressExtra;
    r.deepTagBuff = nx.tagBuff;
    // 배치 A Step 5: curseCleanse — 저주 배열 갱신.
    if (sv.kind === "curseCleanse") { r.curses = nx.curses || []; this.toast(`🕊️ 저주 정화 — 저주 1개 제거됨`); }
    if (r.deepStats) r.deepStats.repairs = (r.deepStats.repairs || 0) + 1;
    // 배치F P2: 정비 add/upgrade 구매 성공 → 신규심볼 pity 설정(pickPerk POUCH 와 동일 가드·swap 은 제외).
    if (sv.kind === "addBasic" || sv.kind === "addHigh" || sv.kind === "addRare" || sv.kind === "upgrade") {
      const pid = sv.kind === "upgrade" ? POUCH_UPGRADE[sel.id] : sel.id;
      if (pid && pid !== "empty" && pid !== "random" && SYM_BY_ID[pid]) r.deepPity = { id: pid, spinsLeft: 2 };
    }
    // 심볼증강/유물 코인 보상 — 교체(교체전문가 +3·교체영수증 환급%)·정화(정화붓 +N).
    const sp = this._symMods();
    if (sp) {
      if (sv.kind === "swap") {
        if (sp.swapCoin) { r.coins += sp.swapCoin; this.toast(`🔁 교체전문가 — 코인 +${sp.swapCoin}`); }
        if (sp.repairRefundFrac) { const rb = Math.round(price * sp.repairRefundFrac); if (rb) { r.coins += rb; this.toast(`🧾 교체영수증 — ${Math.round(sp.repairRefundFrac * 100)}% 환급 +${rb}🪙`); } }
      }
      if (sv.kind === "purify") {
        if (sp.purifyCoin) { r.coins += sp.purifyCoin; this.toast(`🖌️ 정화붓 — 코인 +${sp.purifyCoin}`); }
        // 정화전문가/정화된세계(purifyToBasic): 정화로 생긴 빈칸(empty)을 랜덤 기본심볼로 승격(총량 유지).
        if (sp.purifyToBasic) {
          const purified = (res.changes || []).find((c) => c.id === "skull");
          const moved = purified ? Math.max(0, purified.before - purified.after) : 0;
          if (moved > 0) this._purifyEmptyToBasic(moved);
        }
      }
    }
    (r.shopBought || (r.shopBought = [])).push(`${sv.e}${sv.n}`);
    this.toast(`🔧 ${sv.e}${sv.n} 완료`);
    this._checkArchetype();   // 배치G: 정비로 주머니 비중 변경 → 전공 발동/승급 즉시 알림
    return this.state();
  }

  // 정화전문가/정화된세계 — 정화로 생긴 빈칸(empty) N개를 랜덤 기본심볼로 승격(총량 유지·pouch 내에서만).
  //  기본심볼 풀 = POUCH_RARITY "기본" 중 empty/random 제외(cherry/book/star/gem/coin 등).
  _purifyEmptyToBasic(n) {
    const r = this.run; if (!r.pouch) return;
    const basics = POUCH_SYMBOLS.filter((id) => (POUCH_RARITY[id] || "기본") === "기본" && id !== "empty" && id !== "random");
    if (!basics.length) return;
    let converted = 0;
    for (let k = 0; k < n && (r.pouch.empty || 0) > 0; k++) {
      const gid = this.rng.pick(basics);
      r.pouch.empty -= 1; if (r.pouch.empty <= 0) delete r.pouch.empty;
      r.pouch[gid] = (r.pouch[gid] || 0) + 1;
      converted++;
    }
    if (converted > 0) this.toast(`✨ 정화전문가 — 정화 ${converted}칸을 기본심볼로 승격`);
  }

  shopExit() {
    const bought = this.run.shopBought || [];
    const msg = bought.length ? `🛒 상점에서 구매: ${bought.join(" · ")}` : "🛒 상점을 둘러봤어요 (구매 없음)";
    this.run.shopBought = [];
    this._enterRewardDone(msg);   // 갑자기 다음 스테이지로 안 가고 인트로 화면으로
    return this.state();
  }

  // 장치 노드 (장착 or 코인)
  deviceNodeTake(equip) {
    const r = this.run; const d = r._drop; if (!d) { this._beginStage(); return this.state(); }
    if (!this.profile.ownedDevices.includes(d.id)) { this.profile.ownedDevices.push(d.id); this._saveProfile(); }
    if (equip) { r.device = d.id; this.toast(`🔧 ${d.e}${d.n} 장착·영구 획득`); }
    else { r.coins += 15; this.toast(`🔧 ${d.e}${d.n} 영구 획득(미장착)·코인 +15`); }
    r._drop = null; this._beginStage(); return this.state();
  }

  // ── 게임 오버 ──
  _gameOver(voluntary = false) {
    const r = this.run; r.phase = PHASE.RUN_END;
    // 테마빌드: 런 종료 시점에도 판정(도달스테이지 기준 빌드 — foundation/curse_vessel/dice_grad 등). 실패=보스/막스핀클리어 아님.
    this._evalThemeBuilds(r.stage, false, false);
    this._syncDex();
    // 실패 사유(요구 EXP 대비 부족분) 기록 + 즉시 안내. 자발적 포기(voluntary)면 '실패' 프레이밍 생략.
    const short = Math.max(0, r.quota - Math.floor(r.stageExp));
    r.voluntary = !!voluntary;
    r.failInfo = {
      stage: r.stage, quota: r.quota, exp: Math.floor(r.stageExp), spins: r.spins,
      usedSpins: Math.min(r.spinIndex, r.spins), shortBy: voluntary ? 0 : short, voluntary: !!voluntary,
      lastCells: (r.lastCells || []).map((c) => ({ e: c.sym.e, tag: c.tag || "" })),
      lastNotes: (r.lastResult && r.lastResult.notes) ? r.lastResult.notes.slice() : [],
      lastGained: Math.floor(r.lastExpApplied || 0),
    };
    if (voluntary) this.toast(`🏁 스테이지 ${r.stage}에서 런을 종료했어요 — 지금까지 점수로 결산!`);
    else if (short > 0) this.toast(`💥 스테이지 ${r.stage} 실패 — 요구 EXP까지 ${short} 부족!`);
    const mod = E.scoreModifier(r.machineId, r.charId);
    const am = ascMods(r.asc);
    const finalScore = Math.floor(r.score * mod * am.scoreMul);
    r.finalScore = finalScore;
    const p = this.profile;
    p.runs += 1; p.totalScore += finalScore;
    // ★승천(asc>0) 점수는 일반 bestScore/랭킹에 반영 안 함(별도 추적) — 랭킹 밸런스 보호.
    // ★심화모드(deepMode) 점수도 일반 bestScore/랭킹 오염 금지(주머니 덱은 확률 소스가 달라 별개 게임) — Phase1은 미집계.
    if (r.deepMode) { if (finalScore > (p.bestDeepScore || 0)) { p.bestDeepScore = finalScore; p.bestDeepStage = r.stage; } }
    else if (r.asc > 0) { if (finalScore > (p.bestAscScore || 0)) { p.bestAscScore = finalScore; p.bestAscLevel = r.asc; } }
    else p.bestScore = Math.max(p.bestScore, finalScore);
    p.bestStage = Math.max(p.bestStage, r.stage);
    // 졸업(스테이지15 클리어) → 심화 학기 해금/승급
    if (r.graduatedThisRun) {
      p.graduations = (p.graduations || 0) + 1;
      const prevMax = p.ascMax ?? -1;
      p.ascMax = Math.max(prevMax, r.asc);
      if (p.ascMax > prevMax) this.toast(r.asc > 0 ? `🎓 심화 ${r.asc} 졸업! 심화 ${Math.min(ASC_MAX, r.asc + 1)} 해금` : "🎓 졸업! 심화 학기 해금 — 홈에서 난이도를 올려보세요");
    }
    // 업적 카운터
    const cnt = p.counters;
    cnt.cherryTotal = (cnt.cherryTotal || 0) + r.stats.cherry;
    cnt.crownTotal = (cnt.crownTotal || 0) + r.stats.crown;
    cnt.jackpots = (cnt.jackpots || 0) + r.stats.jackpots;
    cnt.bossClears = (cnt.bossClears || 0) + r.stats.bossClears;
    cnt.lastSpinClears = (cnt.lastSpinClears || 0) + r.stats.lastClears;
    cnt.exactClears = (cnt.exactClears || 0) + r.stats.exactClears;
    cnt.prismPicks = (cnt.prismPicks || 0) + r.stats.prismPicks;
    cnt.runs = p.runs; cnt.bestStage = p.bestStage; cnt.bestScore = p.bestScore;
    cnt.graduations = p.graduations || 0; cnt.ascMax = (p.ascMax ?? -1); cnt.playerLevel = p.playerLevel || 1;   // 후반 업적용(playerLevel 은 1런 지연)
    // ── Phase 5: 심화 전용 카운터 (심화 런에서만 누적 → 심화 업적 게이팅·일반 카운터와 완전 분리) ──
    //  ★r.deepMode 게이팅으로 일반 런은 이 블록 전부 스킵(격리). 카운터명은 전부 deep* 접두(일반 업적 key 와 미충돌).
    if (r.deepMode && r.deepStats) {
      const ds = r.deepStats;
      cnt.deepRuns = (cnt.deepRuns || 0) + 1;                                  // 심화 첫 플레이
      cnt.deepBossClears = (cnt.deepBossClears || 0) + ds.bossClears;          // 심볼마스터(통산)
      cnt.deepMaxTotal = Math.max(cnt.deepMaxTotal || 0, ds.maxTotal);        // 대형주머니(런 최대 총량의 통산 최고)
      // 1회성 달성 플래그 → 통산 카운터로 승격(한번이라도 달성하면 1↑). 임계값 1 업적.
      const flag = (cur, ok) => (cur || 0) + (ok ? 1 : 0);
      cnt.deepCompress95     = flag(cnt.deepCompress95,     ds.compress95Clear);
      cnt.deepCompress85Boss = flag(cnt.deepCompress85Boss, ds.compress85BossClear);
      cnt.deepCherry50Boss   = flag(cnt.deepCherry50Boss,   ds.cherry50BossClear);
      cnt.deepSkull40Boss    = flag(cnt.deepSkull40Boss,    ds.skull40BossClear);
      cnt.deepGem50Score30k  = flag(cnt.deepGem50Score30k,  ds.gem50Score30kBoss);
      cnt.deepCrown2Boss     = flag(cnt.deepCrown2Boss,     ds.crown2BossClear);
      cnt.deepBalanceBoss    = flag(cnt.deepBalanceBoss,    ds.balanceBossClear);
      cnt.deepSkull0Boss     = flag(cnt.deepSkull0Boss,     ds.skull0BossClear);
      // 희귀/전설 발견 종류 = 통산 누적 집합(배열)의 크기. 런별 집합을 프로필 배열에 합집합.
      const mergeSeen = (key, set) => { const arr = p[key] || (p[key] = []); for (const id of set) if (!arr.includes(id)) arr.push(id); return arr.length; };
      cnt.deepRaresSeen   = mergeSeen("deepRaresSeenIds",   ds.raresSeen);
      cnt.deepLegendsSeen = mergeSeen("deepLegendsSeenIds", ds.legendsSeen);
    }
    // 업적 해금 + 장치 보상 + (Phase5) 심볼 종류 해금
    const newly = [];
    const newSyms = [];
    for (const a of ACHIEVEMENTS) {
      if (!p.unlocked.includes(a.id) && (cnt[a.key] || 0) >= a.th) {
        p.unlocked.push(a.id); newly.push(a);
        const dev = ACH_DEVICE_REWARD[a.id];
        if (dev && !p.ownedDevices.includes(dev)) { p.ownedDevices.push(dev); }
        // Phase 5: 심화 업적 → 심볼 종류 해금(profile.symUnlocked). 캐시 무효화로 다음 오퍼부터 반영.
        const sym = ACH_SYMBOL_UNLOCK[a.id];
        if (sym) { if (!p.symUnlocked) p.symUnlocked = []; if (!p.symUnlocked.includes(sym)) { p.symUnlocked.push(sym); newSyms.push(sym); this._invalidateSymUnlockCache(); } }
      }
    }
    r.newAch = newly;
    r.newSyms = newSyms;
    if (newSyms.length) { const nm = (id) => (SYM_BY_ID[id] ? SYM_BY_ID[id].e + SYM_BY_ID[id].n : id); this.toast(`🔓 심볼 해금: ${newSyms.map(nm).join(", ")}`); }
    // ── 플레이어 레벨 XP (레벨업 = 콘텐츠 해금, 영구 스탯 보정 없음) ──
    const lvlBefore = levelInfo(p.playerXp).level;
    const runXp = 40 + Math.min(20, r.stage) * 12 + Math.floor(finalScore / 250) + r.stats.bossClears * 20 + newly.length * 25;
    p.playerXp = (p.playerXp || 0) + runXp;
    const info = levelInfo(p.playerXp); p.playerLevel = info.level;
    r.xpGain = runXp; r.levelBefore = lvlBefore; r.levelAfter = info.level;
    if (info.level > lvlBefore) this.toast(`🎉 플레이어 레벨 ${info.level} 달성!`);
    const gotDev = this._grantLevelDevices();   // 레벨 보상 장치
    if (gotDev.length) this.toast(`🔧 레벨 보상 장치 획득: ${gotDev.map((id) => { const d = DEV_BY_ID[id]; return d ? d.e + d.n : id; }).join(", ")}`);
    // 숙련도 집계(사용한 캐릭/슬롯/장치)
    this._bumpMastery("char", r.charId); this._bumpMastery("mac", r.machineId); if (r.device) this._bumpMastery("dev", r.device);
    this._saveProfile();
  }

  newRunReset() { this.run = null; return this.startRun(); }

  markTutorialDone() { this.profile.tutDone = true; this._saveProfile(); }

  // 전체 데이터 초기화 (신규 플레이어 상태로) — 최고점수·해금·장치·도감 전부 리셋. 랭킹 등록기록은 별도(유지).
  resetData() {
    this.profile = defaultProfile(); this.run = null;
    this._invalidateSymUnlockCache();   // [LOW-1] 리셋 후 stale 해금 캐시로 잠금 심볼이 노출되던 문제 방지
    try { this.storage.setItem(STORE_KEY, JSON.stringify(this.profile)); } catch (e) {}
    try { if (typeof localStorage !== "undefined") localStorage.removeItem("slotweb_nick"); } catch (e) {}
  }

  // ── 도감 발견 추적 ──
  _see(cat, id) { if (!id) return; const a = this.profile.seen[cat] || (this.profile.seen[cat] = []); if (!a.includes(id)) { a.push(id); this._dexDirty = true; } }
  _syncDex() {
    const r = this.run; if (!r) return;
    this._see("char", r.charId); this._see("mac", r.machineId); if (r.device) this._see("dev", r.device);
    for (const id of r.perks) { if (AUG_BY_ID[id]) this._see("aug", id); else if (REL_BY_ID[id]) this._see("rel", id); else if (SYM_PERK_BY_ID[id]) this._see("symperk", id); }   // [LOW-5] 심볼증강/유물 발견 추적
    for (const id of r.curses) this._see("cur", id);
    if (r.seenSyms) for (const id of r.seenSyms) this._see("sym", id);
    if (r.seenItems) for (const id of r.seenItems) this._see("item", id);
    for (const s of E.activeSets(r.perks, r.charId, r.machineId, r.device)) this._see("set", s.id);
    if (this._dexDirty) { this._saveProfile(); this._dexDirty = false; }
  }

  // 전체 카탈로그 + 발견여부 + (미발견 시)조건. UI 도감이 렌더.
  dex() {
    const p = this.profile; const sn = (cat, id) => (p.seen[cat] || []).includes(id);
    const f = (n) => n.toLocaleString();
    const achName = (id) => { const a = ACHIEVEMENTS.find((x) => x.id === id); return a ? a.n : id; };
    const charCond = (c) => sn("char", c.id) ? "" : (this.charUnlocked(c) ? "해금됨 — 이 캐릭터로 플레이하면 공개"
      : "해금: " + ([c.unlockRuns && `${c.unlockRuns}런`, c.unlockScore && `${f(c.unlockScore)}점`, c.unlockStage && `S${c.unlockStage} 도달`, c.unlockAch && `업적 [${achName(c.unlockAch)}]`].filter(Boolean).join(" 또는 ") || "기본 제공"));
    const macCond = (m) => sn("mac", m.id) ? "" : (this.machineUnlocked(m) ? "해금됨 — 이 머신으로 플레이하면 공개"
      : "해금: " + ([m.unlockRuns && `${m.unlockRuns}런`, m.unlockScore && `${f(m.unlockScore)}점`, m.unlockAch && `업적 [${achName(m.unlockAch)}]`].filter(Boolean).join(" 또는 ") || "기본 제공"));
    const devCond = (d) => { if (p.ownedDevices.includes(d.id)) return "보유 중 — 장착하면 공개"; const k = Object.keys(ACH_DEVICE_REWARD).find((x) => ACH_DEVICE_REWARD[x] === d.id); if (d.deepOnly) return k ? `심화 업적 [${achName(k)}] 달성 시 영구 획득 (심화 전용 장치)` : "심화 업적으로 획득 (심화 전용 장치)"; return k ? `업적 [${achName(k)}] 달성 시 영구 획득 · 또는 런 중 장치 노드` : "런 중 장치 노드에서 획득"; };
    const setCond = (s) => { const parts = s.req.map((id) => PERK_BY_ID[id] ? PERK_BY_ID[id].e + PERK_BY_ID[id].n : id); let ex = ""; if (s.reqChar) ex += ` +${CHAR_BY_ID[s.reqChar] ? CHAR_BY_ID[s.reqChar].e + CHAR_BY_ID[s.reqChar].n : s.reqChar}`; if (s.reqMachine) ex += ` +${MAC_BY_ID[s.reqMachine] ? MAC_BY_ID[s.reqMachine].e : s.reqMachine}머신`; if (s.reqDevice) ex += ` +${DEV_BY_ID[s.reqDevice] ? DEV_BY_ID[s.reqDevice].e : s.reqDevice}`; return "필요 조합: " + parts.join(" + ") + ex; };
    const dn = (id) => { const d = DEV_BY_ID[ACH_DEVICE_REWARD[id]]; return d ? d.e + d.n : ""; };
    // Phase 5: 업적이 해금하는 심볼 라벨(도감 업적 카드 보상 표기) + 심볼→해금업적 역맵(심볼 도감 조건).
    const symRewardOf = (achId) => { const id = ACH_SYMBOL_UNLOCK[achId]; if (!id) return ""; const s = SYM_BY_ID[id]; return s ? s.e + s.n : id; };
    const achForSym = (symId) => { const k = Object.keys(ACH_SYMBOL_UNLOCK).find((x) => ACH_SYMBOL_UNLOCK[x] === symId); return k ? achName(k) : null; };
    const symUnlockedSet = this._symUnlockedSet();
    const symOpen = (id) => !symUnlockedSet || symUnlockedSet.has(id) || id === "empty" || id === "random";
    return {
      // 심화모드(주머니) 전용 상위/변형/특수(Phase4) 심볼은 일반 도감에서 제외 — 일반모드 완전 격리(표시측).
      sym: SYMS.filter((s) => !DEEP_ONLY_SYMS.has(s.id)).map((s) => ({ id: s.id, e: s.e, n: s.n, seen: sn("sym", s.id), detail: `EXP ${s.exp}${s.score ? ` · 점수 ${s.score}` : ""}${s.coin ? ` · 코인 ${s.coin}` : ""}${s.special !== "NONE" && SP_DESC[s.special] ? ` · ${SP_DESC[s.special]}` : ""}${s.tags.length ? ` · #${s.tags.join(" #")}` : ""}`, cond: s.weight > 0 ? "스핀에서 등장하면 공개" : "머신·증강으로 등장하면 공개" })),
      // 심화모드 전용 특수심볼 도감(Phase4) — 별도 섹션(deepSym). 주머니에서 등장/획득하면 공개. 일반 sym 탭과 분리(격리).
      // deepSym: 심화 전용 심볼 도감 + Phase5 해금 상태(locked=업적 잠금·기본해금은 항상 개방). locked 면 해금 업적 안내.
      deepSym: SYMS.filter((s) => DEEP_ONLY_SYMS.has(s.id) && POUCH_SYMBOLS.includes(s.id)).map((s) => {
        const locked = !symOpen(s.id); const ach = achForSym(s.id);
        return { id: s.id, e: s.e, n: s.n, seen: sn("sym", s.id), locked,
          detail: `${SP_DESC[s.special] || (s.exp ? `EXP ${s.exp}` : "특수 효과")}${s.tags.length ? ` · #${s.tags.join(" #")}` : ""}`,
          rarity: POUCH_RARITY[s.id] || "기본",
          cond: locked ? (ach ? `🔒 잠김 — 업적 [${ach}] 달성 시 해금` : "🔒 잠김") : "심화모드 주머니에서 등장하면 공개" };
      }),
      // [LOW-5] 심화 심볼 증강/유물 도감(36종) — deepSym 패턴(획득 시 공개, profile.seen.symperk).
      symAug: SYM_AUGMENTS.map((a) => ({ id: a.id, e: a.e, n: a.n, seen: sn("symperk", a.id), detail: a.d, tier: a.t, cond: "심화모드 심볼 증강 노드에서 얻으면 공개" })),
      symRel: SYM_RELICS.map((a) => ({ id: a.id, e: a.e, n: a.n, seen: sn("symperk", a.id), detail: a.d, tier: a.t, cond: "심화모드 심볼 유물 노드에서 얻으면 공개" })),
      char: CHARS.map((c) => ({ id: c.id, e: c.e, n: c.n, seen: sn("char", c.id), detail: c.d, cond: charCond(c) })),
      mac: MACHINES.map((m) => ({ id: m.id, e: m.e, n: m.n, seen: sn("mac", m.id), detail: m.d, cond: macCond(m) })),
      dev: DEVICES.map((d) => ({ id: d.id, e: d.e, n: d.n, seen: sn("dev", d.id), deep: !!d.deepOnly, detail: `${d.d} · ${d.kind === "PASSIVE" ? "패시브" : d.kind === "PEEK" ? "표시형" : "능동(" + (d.cmd || "") + ")"}`, cond: devCond(d) })),
      aug: AUGMENTS.map((a) => ({ id: a.id, e: a.e, n: a.n, seen: sn("aug", a.id), detail: a.d, tier: a.t, cond: "증강 보상·상점에서 얻으면 공개" })),
      rel: RELICS.map((a) => ({ id: a.id, e: a.e, n: a.n, seen: sn("rel", a.id), detail: a.d, tier: a.t, cond: "상점·유물 노드에서 얻으면 공개" })),
      cur: CURSES.map((a) => ({ id: a.id, e: a.e, n: a.n, seen: sn("cur", a.id), detail: a.d, cond: "저주 노드·위험거래에서 얻으면 공개" })),
      set: SETS.map((s) => ({ id: s.id, e: "🎰", n: s.n, seen: sn("set", s.id), detail: s.d, cond: setCond(s) })),
      item: ITEMS.map((it) => ({ id: it.id, e: it.e, n: it.n, seen: sn("item", it.id), detail: `${it.d} · ${it.k}`, cond: "상점·이벤트에서 얻거나 쓰면 공개" })),
      // ach: 일반+심화(deep). 심화 업적은 deep:true + 해금 심볼 표기(symReward). 장치/심볼 보상 모두 노출.
      ach: ACHIEVEMENTS.map((a) => ({ id: a.id, e: a.e, n: a.n, done: p.unlocked.includes(a.id), detail: a.d, reward: dn(a.id), deep: !!a.deep, symReward: symRewardOf(a.id) })),
      // 테마빌드 도감 — done=완성(counters bld_<id>>0). 미완성은 조건만 노출(업적 탭과 동형).
      bld: THEME_BUILDS.map((b) => ({ id: b.id, e: b.e, n: b.n, cat: b.cat, done: (p.counters[b.id] || 0) > 0, detail: b.cond })),
    };
  }
  // 테마빌드 진행 집계(도감 탭 표시) — { bldTotal, bldCat_*, bldAllBasic, bldAllMaster }
  themeBuildStats() { return E.themeBuildStats(this.profile.counters); }

  // ── 슬롯 칸 상세 분해 (UI 칸 클릭 시) ──
  cellInfo(idx) {
    const r = this.run; if (!r || !r.lastCells || !r.lastCells[idx]) return null;
    const c = r.lastCells[idx]; const s = c.sym;
    const mods = r.lastMods || this._mods();
    const reel = r.lastCells.length;
    const isCenter = idx === Math.floor(reel / 2);
    const isFirst = (r.lastSpinIndex || 0) === 0;
    const isLast = (r.lastSpinIndex || 0) === r.spins - 1;
    const empty = s.id === "empty";

    const baseExp = s.exp;
    const perSymExp = mods.perSymbolExp[s.id] || 0;
    const tagBonuses = (s.tags || []).map((t) => ({ t, v: mods.tagExpBonus[t] || 0 })).filter((x) => x.v);
    const skullExp = s.special === "SKULL" ? (mods.skullExp || 0) : 0;
    const sub = baseExp + perSymExp + tagBonuses.reduce((a, b) => a + b.v, 0) + skullExp;
    const centerMul = isCenter ? mods.centerExpMul : 1;
    const cellExp = Math.max(0, Math.round(sub * centerMul));
    const baseScore = s.score;
    const perSymScore = mods.perSymbolScore[s.id] || 0;
    const skullScore = s.special === "SKULL" ? (mods.skullScoreBonus || 0) : 0;
    const cellScore = baseScore + perSymScore + skullScore;

    const SP = { COIN: `코인 +${s.coin || 1} 획득 (코인 배수 적용)`, BOMB: `양옆 칸을 제거하고 칸당 +${C.BOMB_EXP_PER} EXP`, MAGNET: "한쪽 옆 심볼을 복사", SKULL: "기본 무페널티 · 해골빌드면 EXP/점수 가산", FLAME: "이번 스핀 전체 EXP +50% (다음 스핀 -50%)", DICE: "1~12 무작위 EXP 추가", WILD: "최다 심볼 그룹에 합류(세트·잭팟 기여)", SEED: "다음 스핀에 책/별/왕관으로 성장", KEY: `보물 코인 +${C.KEY_COIN_PER}🪙 (열쇠 1개당)` };
    const specials = [];
    if (SP[s.special]) specials.push(SP[s.special]);
    if (c.tag === "🧲") specials.push("자석으로 복사된 칸");
    if (c.tag === "🌱→") specials.push("씨앗이 성장한 칸");
    if (empty || c.tag === "💥") specials.push("폭탄으로 제거된 빈 칸 (EXP 0)");

    const baseM = E.buildMods("basic", "gambler", []);
    const label = (m) => {
      const out = []; const d = (a, b) => (a || 0) - (b || 0);
      const de = d(m.perSymbolExp[s.id], baseM.perSymbolExp[s.id]); if (de) out.push(`${s.e}EXP ${de > 0 ? "+" : ""}${de}`);
      const ds = d(m.perSymbolScore[s.id], baseM.perSymbolScore[s.id]); if (ds) out.push(`${s.e}점수 ${ds > 0 ? "+" : ""}${ds}`);
      (s.tags || []).forEach((t) => { const dv = d(m.tagExpBonus[t], baseM.tagExpBonus[t]); if (dv) out.push(`${t}태그 EXP ${dv > 0 ? "+" : ""}${dv}`); });
      if (isCenter && m.centerExpMul !== baseM.centerExpMul) out.push(`가운데 칸 ×${fmt2(m.centerExpMul)}`);
      if (s.special === "SKULL" && m.skullExp !== baseM.skullExp) out.push(`해골EXP ${m.skullExp - baseM.skullExp > 0 ? "+" : ""}${m.skullExp - baseM.skullExp}`);
      if (m.expMul !== baseM.expMul) out.push(`전체EXP ×${fmt2(m.expMul)}`);
      if (m.flatExp !== baseM.flatExp) out.push(`스핀마다 ${m.flatExp - baseM.flatExp > 0 ? "+" : ""}${m.flatExp - baseM.flatExp}`);
      if (isFirst && m.firstSpinExpMul !== baseM.firstSpinExpMul) out.push(`첫스핀 ×${fmt2(m.firstSpinExpMul)}`);
      if (isLast && m.lastSpinExpMul !== baseM.lastSpinExpMul) out.push(`막스핀 ×${fmt2(m.lastSpinExpMul)}`);
      if (s.rare && m.rareWeightMul !== baseM.rareWeightMul) out.push(`희귀등장 ×${fmt2(m.rareWeightMul)}`);
      if (s.special === "COIN" && m.coinMul !== baseM.coinMul) out.push(`코인 ×${fmt2(m.coinMul)}`);
      return out;
    };
    const affecting = [];
    const charM = E.buildMods("basic", r.charId, []);
    { const p = label(charM); if (p.length) { const ch = CHAR_BY_ID[r.charId]; affecting.push({ e: ch.e, n: ch.n, kind: "캐릭터", effect: p.join(" · ") }); } }
    for (const id of r.perks) { const info = PERK_BY_ID[id]; if (!info) continue; const p = label(E.buildMods("basic", "gambler", [id])); if (p.length) affecting.push({ e: info.e, n: info.n, kind: AUG_BY_ID[id] ? "증강" : "유물", tier: info.t, effect: p.join(" · ") }); }
    for (const id of r.curses) { const info = CUR_BY_ID[id]; if (!info) continue; const p = label(E.buildMods("basic", "gambler", [], [id])); if (p.length) affecting.push({ e: info.e, n: info.n, kind: "저주", effect: p.join(" · ") }); }
    const sets = E.activeSets(r.perks, r.charId, r.machineId, r.device).map((x) => ({ n: x.n, d: x.d }));

    // 배치 B: 심화 런에서 영구 perk bias 반영 유효% 계산 — cellInfo 칸상세 표시용.
    let pouchInfo = null;
    if (r.deepMode && r.pouch) {
      const pouch = r.pouch; const symId = s.id;
      const baseCount = pouch[symId] || 0; const baseTotal = E.pouchTotal(pouch);
      const permMods = this._mods([]);
      const hasMul = permMods.symbolWeightMul && Object.keys(permMods.symbolWeightMul).length > 0;
      const hasAdd = permMods.weightAdd && Object.keys(permMods.weightAdd).length > 0;
      const hasRare = permMods.rareWeightMul != null && permMods.rareWeightMul !== 1;
      const bias = (hasMul || hasAdd || hasRare) ? {
        mul: hasMul ? permMods.symbolWeightMul : undefined,
        add: hasAdd ? permMods.weightAdd : undefined,
        rareMul: hasRare ? permMods.rareWeightMul : undefined,
      } : undefined;
      let effPct = baseTotal > 0 ? baseCount / baseTotal : 0;
      let biasChanged = false;
      if (bias && baseCount > 0) {
        const bd = E.pouchEffWeights(pouch, bias);
        const en = bd.entries.find((x) => x.id === symId);
        if (en) { effPct = bd.effTotal > 0 ? en.effWeight / bd.effTotal : 0; biasChanged = bd.biasChanged.has(symId); }
      }
      pouchInfo = { count: baseCount, total: baseTotal, effPct, biasChanged };
    }
    return { sym: { e: s.e, n: s.n, id: s.id, special: s.special, tags: s.tags || [] }, idx, isCenter, isFirst, isLast, empty,
      baseExp, perSymExp, tagBonuses, skullExp, centerMul, cellExp, baseScore, perSymScore, skullScore, cellScore,
      specials, affecting, sets, expMul: mods.expMul, flatExp: mods.flatExp,
      // 배치F P1: 심화 런이면 이 심볼의 주머니 보유/총량(표시 전용·로직 무접촉). 일반 런=null(UI 조건부 미표시).
      // 배치 B: pouchInfo에 effPct(bias 반영 유효%)+biasChanged(🧪 마커) 추가.
      pouchInfo };
  }

  // ── UI 가 읽는 상태 스냅샷 ──
  state() {
    const r = this.run; if (!r) return null;
    return {
      phase: r.phase, stage: r.stage, spinIndex: r.spinIndex, spins: r.spins,
      stageExp: r.stageExp, quota: r.quota, score: r.score, coins: r.coins,
      charId: r.charId, machineId: r.machineId, device: r.device,
      perks: r.perks.slice(), curses: r.curses.slice(), items: r.items.slice(),
      armItems: r.armItems.slice(), phaseItems: r.phaseItems.slice(),
      boss: r.boss, nodes: r.nodes.slice(), options: r.options, shopItems: r.shopItems.slice(),
      unluckyGauge: r.unluckyGauge, usedCmds: r.usedCmds.slice(), cmdFreeUsed: { ...r.cmdFreeUsed }, lastCells: r.lastCells,
      clearSummary: r.clearSummary, rewardMsg: r.rewardMsg, nextPreview: r.nextPreview, statsView: r.statsView, failInfo: r.failInfo,
      finalScore: r.finalScore, newAch: r.newAch, newSyms: r.newSyms, drop: r._drop, pickKind: r._pickKind,
      xpGain: r.xpGain, levelBefore: r.levelBefore, levelAfter: r.levelAfter,
      asc: r.asc || 0, ascScoreMul: ascMods(r.asc).scoreMul, perkLevels: { ...r.perkLevels },
      offerMeta: r.offerMeta || null,
      lever: r.armItems.slice(),
      deepMode: !!r.deepMode,                          // 심화모드 여부(UI 분기용)
      pouchView: r.deepMode ? this.pouchView() : null, // 심화모드 주머니 현황(시트·HUD)
      repairServices: r.deepMode ? this.repairServices() : null,   // Phase 3: 심화 상점 정비 서비스(탭)
      deepDeviceInfo: r.deepMode ? this.deepDeviceInfo() : null,    // Phase 5: 심화 심볼 장치(표시형 PEEK) 정보
      // §9.1 J2: 피버 게이지 — 심화 전용(일반모드 = null·격리).
      feverGauge: r.deepMode ? (r.feverGauge || 0) : null,
      feverSpins: r.deepMode ? (r.feverSpins || 0) : null,
    };
  }

  // ── Phase 5: 심화 심볼 장치 표시형(PEEK) 정보 — 태그스캐너/희귀탐지기(엔진 무영향·UI 근사). ──
  //  ★엔진 효과 없는 표시 전용 장치. state 로 노출해 ui 가 렌더. 미장착이면 null 필드.
  deepDeviceInfo() {
    const r = this.run; if (!r || !r.deepMode || !r.pouch) return null;
    const info = {};
    if (r.device === "dev_tag_scanner") {
      const tags = E.pouchTagCounts(r.pouch); const total = E.pouchTotal(r.pouch);
      const sorted = Object.entries(tags).sort((a, b) => b[1] - a[1]);
      if (sorted.length && total > 0) {
        const top = sorted[0], bot = sorted[sorted.length - 1];
        info.tagScanner = {
          strongest: { tag: top[0], pct: fmt2((top[1] / total) * 100) },
          weakest: { tag: bot[0], pct: fmt2((bot[1] / total) * 100) },
        };
      }
    }
    if (r.device === "dev_rare_detector") {
      // 다음 보상에 희귀 심볼 유무(해금된 희귀 심볼이 존재하면 표시) — 결정 불가(오퍼는 rng)라 "가능성" 표시(정직·근사).
      const unlocked = this._symUnlockedSet();
      const anyRare = POUCH_SYMBOLS.some((id) => (POUCH_RARITY[id] || "기본") === "희귀" && (!unlocked || unlocked.has(id)));
      info.rareDetector = { possible: anyRare };
    }
    return Object.keys(info).length ? info : null;
  }
}

// 이벤트용 NEXTSPIN 아이템 풀 (ITEMS 는 상단에서 import)
const ITEMS_NEXTSPIN = ITEMS.filter((it) => it.k === "NEXTSPIN");
