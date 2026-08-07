// ── 잭팟 슬롯 (웹 단독판) — 순수 엔진 로직 ────────────────────────────
// SlotV2Engine.kt 의 buildMods/evaluate/weighted/quota/보스/점수 공식을 충실 포팅.
// 상태 없음(순수 함수). 런 상태머신은 game.js. 표시는 ui.js.
import {
  C, QUOTAS, SET_EXP, SET_SCORE, SYMS, SYM_BY_ID, EMPTY_SYM, VALUE_IDS, BOSSES,
  CHARS, MACHINES, AUGMENTS, RELICS, CURSES, SETS, ITEMS, DEVICES, SCORE_TITLES,
  MAC_BY_ID, CHAR_BY_ID, REL_BY_ID, AUG_BY_ID, PERK_BY_ID, PERK_FAMILY,
  THEME_BUILDS, THEME_BUILD_CATEGORIES,
  DEEP, POUCH_UPGRADE, POUCH_RARITY, POUCH_RARITY_ORDER, POUCH_SYMBOLS, TIER_BY_RARITY,
  POUCH_CAT, POUCH_USE, JACKPOT_TAG, JACKPOT_TAG_DECK_MAX,
  SYM_AUGMENTS, SYM_RELICS, SYM_PERK_BY_ID, SYM_AUG_LEVELS, isSymAugLevelable,
} from "./data.js";

// 증강/유물 패밀리 조회 — 미등록은 고유(자기 id 가 패밀리, 랭크1 = 항상 후보).
const famOf = (p) => PERK_FAMILY[p.id] || [p.id, 1];

// ── 증강 레벨업(Lv1~3) 델타 ── 등록된 증강만 레벨업 가능(프리즘 제외). Lv2/Lv3 에서 추가 델타 적용.
//  o = { m, pse, pss, wmul, wadd } (buildMods 내부 헬퍼). run.perkLevels 로 레벨 전달.
const AUG_LEVELS = {
  study:       { 2: (o) => (o.m.expMul *= 1.0455), 3: (o) => (o.m.expMul *= 1.043) },   // 10→15→20%
  greed:       { 2: (o) => (o.m.expMul *= 1.056),  3: (o) => (o.m.expMul *= 1.061) },   // 25→32→40%
  polymath:    { 2: (o) => (o.m.expMul *= 1.05),   3: (o) => (o.m.expMul *= 1.048) },   // 20→26→32%
  cherry_up:   { 2: (o) => o.pse("cherry", 1),     3: (o) => o.pse("cherry", 1) },      // +2→+3→+4
  book_up:     { 2: (o) => o.pse("book", 1),       3: (o) => o.pse("book", 1) },
  star_up:     { 2: (o) => o.pse("star", 1),       3: (o) => o.pse("star", 1) },
  diligence:   { 2: (o) => (o.m.flatExp += 2),     3: (o) => (o.m.flatExp += 2) },      // +3→+5→+7
  set_sense:   { 2: (o) => (o.m.setExpMul *= 1.077), 3: (o) => (o.m.setExpMul *= 1.071) }, // 30→40→50%
  coin_luck:   { 2: (o) => (o.m.coinMul *= 1.077), 3: (o) => (o.m.coinMul *= 1.071) },
  skull_study: { 2: (o) => (o.m.skullExp += 2),    3: (o) => (o.m.skullExp += 2) },     // +6→+8→+10
  gem_polish:  { 2: (o) => o.pss("gem", 3),        3: (o) => o.pss("gem", 3) },         // +10→+13→+16
  lucky:       { 2: (o) => (o.m.rareWeightMul *= 1.04), 3: (o) => (o.m.rareWeightMul *= 1.04) }, // 20→~25→~30%
};
export const isAugLevelable = (id) => !!AUG_LEVELS[id];
// 심볼증강 레벨업 가능 여부(data.js 재노출) — game.js 가 E.isSymAugLevelable 로 접근.
export { isSymAugLevelable };

// ── 시드 가능 RNG (mulberry32) — 테스트 재현용. 미지정 시 난수 시드. ──
export function makeRng(seed) {
  let a = (seed >>> 0) || ((Math.random() * 0xffffffff) >>> 0);
  const next = () => {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  return {
    double: next,
    int: (min, maxExcl) => min + Math.floor(next() * (maxExcl - min)),  // [min, maxExcl)
    n: (nn) => Math.floor(next() * nn),                                  // [0, nn)
    pick: (arr) => arr[Math.floor(next() * arr.length)],
    shuffle: (arr) => { const a2 = arr.slice(); for (let i = a2.length - 1; i > 0; i--) { const j = Math.floor(next() * (i + 1)); [a2[i], a2[j]] = [a2[j], a2[i]]; } return a2; },
  };
}

// ── 밸런스 헬퍼 ───────────────────────────────────────────────────────
export function quota(stage) {
  const i = stage - 1;
  if (i < 0) return QUOTAS[0];
  if (i < QUOTAS.length) return QUOTAS[i];
  let q = QUOTAS[QUOTAS.length - 1];
  for (let k = 0; k < i - QUOTAS.length + 1; k++) q *= 1.2;
  return Math.floor(q);
}
export const isBossStage = (stage) => stage % 5 === 0;
export const bossFor = (stage) => (stage % 5 === 0 ? BOSSES[(Math.floor(stage / 5) - 1) % BOSSES.length] : null);
export const bossSpins = (stage) => bossFor(stage)?.bonusSpins ?? 0;
export const bossQuotaMul = (stage) => bossFor(stage)?.quotaMul ?? 1.0;
export const scoreModifier = (machineId, charId) => (MAC_BY_ID[machineId]?.scoreMod ?? 1) * (CHAR_BY_ID[charId]?.scoreMod ?? 1);
export function streakBonus(stage) {
  if (stage >= 15) return 600; if (stage >= 10) return 350; if (stage >= 7) return 200;
  if (stage >= 4) return 100; if (stage >= 2) return 40; return 0;
}
export function stageClearScore(stage, leftoverExp, leftSpins, curses, boss) {
  let s = stage * 50;
  s += leftoverExp * C.SCORE_PER_LEFTOVER;
  s += leftSpins * C.SCORE_PER_LEFTSPIN;
  if (boss) s += C.BOSS_CLEAR_SCORE;
  // 저주 점수 보너스 없음(패널티 전용) — curses 인자는 시그니처 호환용.
  return Math.floor(s);
}
export function scoreTitle(best) { return SCORE_TITLES.find((t) => best >= t.min) || SCORE_TITLES[SCORE_TITLES.length - 1]; }
export const titleStr = (best) => { const t = scoreTitle(best); return t.e + t.n; };

// ── Mods (집계 효과) ──────────────────────────────────────────────────
export function defaultMods() {
  return {
    expMul: 1, scoreMul: 1, coinMul: 1, flatExp: 0, flatScore: 0, bonusSpins: 0,
    skullExp: 0, skullPenaltyMul: 1, skullScoreBonus: 0, setExpMul: 1,
    perSymbolExp: {}, perSymbolScore: {}, firstSpinExpMul: 1, lastSpinExpMul: 1,
    rareWeightMul: 1, tagExpBonus: {}, centerExpMul: 1, endsMatchExpMul: 1, adjacentSameExp: 0,
    // 심화모드(deepMode) 전용 — 태그별 곱셈 버프(+10%씩). 일반모드는 항상 {}=배수1(무영향).
    //  game.js _mods() 가 r.deepMode && r.deepTagBuff 일 때만 주입. evaluate 가 셀 태그에 곱셈 적용.
    deepTagMul: {},
    // 배치 G — 심화 계열 아키타입(전공) 보너스. key = 계열 base id(cherry/book/gem/skull/coin/flame),
    //  value = 곱셈 증가분(예 0.15 = +15%). 일반모드는 항상 {}=무영향(game.js _mods 심화블록만 주입).
    //   deepFamilyExpMul  → 해당 계열(자신+상위) 셀의 EXP 곱(체리/도서관/화력 + 강령학파는 ☠EXP 분기에도 적용).
    //   deepFamilyScoreMul→ 해당 계열 셀의 점수 곱(보석상).
    //   deepFamilyCoinMul → 해당 계열 셀의 코인 곱(조폐국).
    deepFamilyExpMul: {}, deepFamilyScoreMul: {}, deepFamilyCoinMul: {},
    // Phase 5 심화 심볼증강/유물 전용(기본 0=무효). 일반모드는 항상 0(빈칸심볼 자체가 안 등장 → 이중 격리).
    //  빈칸활용(sa_use_empty)=빈칸당 점수, 빈칸설명서(sr_empty_manual)=빈칸당 EXP. evaluate 빈칸블록에서 소비.
    deepEmptyScore: 0, deepEmptyExp: 0,
    // Phase 5 심볼장치 🔏전설봉인기(dev_legend_seal) — 전설심볼(럭키7/프리즘) 랜덤효과를 최선으로 안정 발동.
    //  ★기본 false=무영향. 이 심볼들은 pouch 전용(일반 rollRaw 미등장)이라 일반모드에선 셋해도 관측효과 0(이중 격리).
    legendStable: false,
    symbolWeightMul: {}, weightAdd: {}, quotaMul: 1, clearCoinBonus: 0,
    // JS-4 신규 per-spin 조건부 필드(기본 무효=기존 동작). evaluate 가 셀 내용으로 판정.
    perSkullExp: 0, rareBurstExpMul: 1, rareBurstScoreMul: 1,
    twoSetBonusMul: 1, set3ExpMul: 1, set4ScoreMul: 1, skull3ScoreMul: 1, perfectShapeExpMul: 1,
    // 프리즘 유물 phoenix_thesis(🔥불사조논문) — buildMods 에서 cx 게이팅 후 스탬프, evaluate 가 전역배 직전 적용.
    cliffBurstExpMul: 1,
    // 상점 빌드용 — 상점 가격/아이템가/보유칸/상점칸/새로고침 보정(게임 로직에서 소비).
    shopPriceMul: 1, itemPriceMul: 1, itemCapBonus: 0, shopSlotBonus: 0, shopRerollDelta: 0,
    // §9.0 V3 J1: 심화모드 플래그 — evaluate 잭팟 태그 판정 게이팅.
    //  game.js _mods() 심화블록에서만 true 로 주입(일반모드는 항상 false = 판정 완전 제외).
    deepMode: false,
  };
}

// ── RunCtx — 조건부 증강(stage/남은스핀/누적스택) 계산용. 기본값=무효(기존 동작). ──
//  (Kotlin SlotV2Engine.RunCtx 포팅.) 파생 isFirstSpin/isLastSpin/spinsLeft 포함.
export function makeCtx(ctx = {}) {
  const spinsPerStage = ctx.spinsPerStage ?? C.SPINS_PER_STAGE;
  const spinIndex = ctx.spinIndex ?? 0;
  return {
    stage: ctx.stage ?? 0,
    spinIndex,
    spinsPerStage,
    stageExp: ctx.stageExp ?? 0,
    quota: ctx.quota ?? 0,
    growthStack: ctx.growthStack ?? 0,
    snowStack: ctx.snowStack ?? 0,
    curseCount: ctx.curseCount ?? 0,
    unluckyGauge: ctx.unluckyGauge ?? 0,
    boss: ctx.boss ?? false,
    coins: ctx.coins ?? 99,
    isFirstSpin: spinIndex === 0,
    isLastSpin: spinsPerStage >= 1 && spinIndex + 1 >= spinsPerStage,
    spinsLeft: Math.max(0, spinsPerStage - spinIndex),
  };
}

export function activeSets(perkIds, charId = "", machineId = "", deviceId = "") {
  const set = new Set(perkIds);
  return SETS.filter((s) =>
    s.req.every((r) => set.has(r)) &&
    (!s.reqChar || s.reqChar === charId) &&
    (!s.reqMachine || s.reqMachine === machineId) &&
    (!s.reqDevice || s.reqDevice === deviceId));
}

export function buildMods(machineId, charId, perkIds = [], curseIds = [], deviceId = "", ctx = {}, levels = {}) {
  const mac = MAC_BY_ID[machineId] || MACHINES[0];
  const m = defaultMods();
  const cx = makeCtx(ctx);   // 조건부 증강용(JS-2~4). 미전달 시 기본값=무효.
  Object.assign(m.symbolWeightMul, mac.wmul || {});
  Object.assign(m.weightAdd, mac.wadd || {});
  const wmul = (id, v) => { m.symbolWeightMul[id] = (m.symbolWeightMul[id] ?? 1) * v; };
  const wadd = (id, v) => { m.weightAdd[id] = (m.weightAdd[id] ?? 0) + v; };
  const pse = (id, v) => { m.perSymbolExp[id] = (m.perSymbolExp[id] ?? 0) + v; };
  const pss = (id, v) => { m.perSymbolScore[id] = (m.perSymbolScore[id] ?? 0) + v; };
  const tag = (t, v) => { m.tagExpBonus[t] = (m.tagExpBonus[t] ?? 0) + v; };
  // ── 후반 슬롯머신 특수 모드(가중치 외 효과) ──
  switch (machineId) {
    case "nightmare": m.skullExp += 6; break;                              // 악몽: 해골도 EXP 제공
    case "throne": pss("crown", 40); m.expMul *= 0.9; break;               // 빈 왕좌: 왕관 점수↑·기본 EXP-10%
    case "broke": pse("cherry", 2); pse("book", 2); pse("star", 2); m.coinMul = 0; break;  // 파산: 코인 없음·값심볼 강화
  }

  switch (charId) {
    case "novice": m.quotaMul *= 0.92; break;
    case "scholar": pse("book", 2); m.clearCoinBonus += 2; break;
    case "parttime": m.firstSpinExpMul *= 0.8; break;
    case "farmer": pse("cherry", 1); m.rareWeightMul *= 0.9; break;
    case "jeweler": pss("gem", 25); break;
    case "cultist": m.skullExp += 3; break;
    case "crowncol": pss("crown", 30); wmul("crown", 1.5); break;
    case "lucky": m.rareWeightMul *= 1.25; break;
    case "highroller": pss("gem", 25); break;
    case "monk": m.bonusSpins -= 1; m.quotaMul *= 0.9; break;
    case "alchemist": m.coinMul *= 1.25; m.clearCoinBonus += 3; break;
    case "daredevil":  // 막판형: 기본 EXP+10%·요구+20% + 막스핀 +60%(아니고 남은≤2면 +35%)
      m.expMul *= 1.1; m.quotaMul *= 1.2;
      if (cx.isLastSpin) m.expMul *= 1.6; else if (cx.spinsLeft <= 2) m.expMul *= 1.35;
      break;
    case "prodigy": m.expMul *= 1.12; break;
    // ── 후반 캐릭터(레벨 해금) ──
    case "regent": pss("crown", 60); wmul("crown", 2.0); wadd("crown", 1.5); m.expMul *= 0.9; break;         // 왕관 점수2배·등장↑·기본 EXP -10%
    case "bankrupt": if (cx.coins <= 0) m.expMul *= 1.5; else if (cx.coins >= 10) m.expMul *= 0.8; break;      // 코인0 EXP+50%·코인10+ -20%
    case "abyss_scholar": if (cx.boss) m.expMul *= 1.3; else m.expMul *= 0.9; break;                         // 보스 EXP+30%·일반 -10%
  }

  for (const id of perkIds) switch (id) {
    case "study": case "greed_s": m.expMul *= 1.10; break;
    case "preview": m.firstSpinExpMul *= 1.25; break;
    case "review": m.lastSpinExpMul *= 1.25; break;
    case "diligence": m.flatExp += 3; break;
    case "cherry_up": pse("cherry", 2); break;
    case "book_up": pse("book", 2); break;
    case "star_up": pse("star", 2); break;
    case "gem_polish": pss("gem", 10); break;
    case "coin_luck": m.coinMul *= 1.3; break;
    case "set_sense": m.setExpMul *= 1.3; break;
    // ── 상점 빌드 증강 ──
    case "discount": m.shopPriceMul *= 0.9; break;                     // 상점 가격 -10%
    case "thrifty": m.itemPriceMul *= 0.8; break;                     // 상점 아이템 가격 -20%
    case "item_bag": m.itemCapBonus += 1; break;                      // 아이템 보유칸 +1
    case "vip": m.shopSlotBonus += 1; m.shopRerollDelta -= 2; break;  // 상점 상품칸+1·새로고침-2
    case "lucky": m.rareWeightMul *= 1.2; break;
    case "study_tag": tag("학습", 4); break;
    case "cherry_farm": pse("cherry", 4); wmul("cherry", 1.3); break;
    case "library": pse("book", 4); tag("학습", 3); break;
    case "gem_invest": pss("gem", 25); break;
    case "skull_study": m.skullExp += 6; break;
    case "center": m.centerExpMul *= 2.0; break;
    case "twins": m.endsMatchExpMul *= 2.0; break;
    case "chain": m.adjacentSameExp += 20; break;
    case "crown_seek": wmul("crown", 2.0); pss("crown", 30); break;
    case "greed": m.expMul *= 1.25; break;
    case "insurance": m.bonusSpins += 1; break;
    case "overdrive": m.expMul *= 1.6; break;
    case "short_day": m.bonusSpins -= 2; m.expMul *= 2.2; break;
    case "wild_world": wadd("wild", 6.0); break;
    case "seed_garden": wadd("seed", 5.0); break;
    case "jackpot": wadd("crown", 3.0); pss("crown", 50); break;
    case "all_in": m.bonusSpins -= 1; m.expMul *= 1.45; break;
    case "cram": m.firstSpinExpMul *= 0.6; m.lastSpinExpMul *= 2.2; break;
    case "high_roller": pss("gem", 30); m.expMul *= 0.92; break;
    case "all_or_nothing": m.skullExp += 10; m.expMul *= 0.9; break;
    case "focus_fire": m.centerExpMul *= 2.5; break;
    case "symmetry": m.endsMatchExpMul *= 2.2; m.adjacentSameExp += 12; break;
    case "crammer_tag": tag("학습", 7); wmul("book", 1.4); break;
    case "gamblers_dice": wadd("dice", 5.0); m.expMul *= 1.15; break;
    case "key_master": wadd("key", 4.0); m.coinMul *= 1.25; break;
    case "glass_cannon": m.bonusSpins -= 1; m.expMul *= 1.9; m.scoreMul *= 1.1; break;
    case "rich_richer": m.coinMul *= 1.6; m.clearCoinBonus += 3; m.expMul *= 0.95; break;
    case "endgame_rush": m.lastSpinExpMul *= 3.0; m.firstSpinExpMul *= 0.5; break;
    case "deep_read": tag("학습", 3); break;
    case "morning": m.firstSpinExpMul *= 1.30; break;
    case "evening": m.lastSpinExpMul *= 1.30; break;
    case "note_take": m.flatExp += 5; break;
    case "star_up2": pse("star", 3); break;
    case "magnet_up": pse("magnet", 3); break;
    case "gem_buff": pss("gem", 12); break;
    case "combo_note": m.setExpMul *= 1.20; break;
    case "polymath": m.expMul *= 1.20; break;
    case "necromancer": m.skullExp += 8; break;
    case "bullseye": m.centerExpMul *= 1.8; break;
    case "mirror": m.endsMatchExpMul *= 1.9; break;
    case "domino": m.adjacentSameExp += 16; break;
    case "honor_student": tag("학습", 6); break;
    case "lapidary": pss("gem", 28); break;
    case "royal_decree": wmul("crown", 1.8); pss("crown", 20); break;
    case "supernova": m.expMul *= 1.70; break;
    case "joker": wadd("wild", 5.0); break;
    case "great_harvest": wadd("seed", 5.0); pse("cherry", 3); break;
    case "mega_jackpot": wadd("crown", 3.0); pss("crown", 40); break;
    case "time_warp": m.bonusSpins += 1; m.expMul *= 1.20; break;
    // 유물
    case "old_book": pse("book", 3); break;
    case "cherry_candy": pse("cherry", 2); break;
    case "rusty_coin": m.coinMul *= 1.2; break;
    case "pencil": m.firstSpinExpMul *= 1.15; break;
    case "coffee": m.lastSpinExpMul *= 1.15; break;
    case "magnifier": m.rareWeightMul *= 1.15; break;
    case "star_sticker": pss("star", 8); break;
    case "black_candle": m.skullExp += 4; break;
    case "gem_cert": pss("gem", 15); break;
    case "clover": m.expMul *= 1.08; break;
    case "set_charm": m.setExpMul *= 1.25; break;
    case "wide_lens": m.centerExpMul *= 1.5; break;
    case "eraser": pse("book", 2); break;
    case "ruler": m.firstSpinExpMul *= 1.12; break;
    case "desk_lamp": m.lastSpinExpMul *= 1.12; break;
    case "cherry_jam": pse("cherry", 3); break;
    case "bookmark": tag("학습", 3); break;
    case "coin_pouch": m.coinMul *= 1.2; break;
    case "mini_scope": m.rareWeightMul *= 1.15; break;
    case "gem_dust": pss("gem", 10); break;
    case "magnet_chip": pse("magnet", 2); break;
    case "star_chart": pse("star", 2); break;
    case "paperclip": m.setExpMul *= 1.15; break;
    case "small_candle": m.skullExp += 3; break;
    case "thick_tome": pse("book", 4); break;
    case "crystal_ball": m.rareWeightMul *= 1.3; break;
    case "skull_idol": m.skullExp += 6; break;
    case "gem_tiara": pss("gem", 20); break;
    case "focus_ring": m.centerExpMul *= 1.6; break;
    case "silver_mirror": m.endsMatchExpMul *= 1.7; break;
    case "iron_chain": m.adjacentSameExp += 14; break;
    case "diploma_relic": tag("학습", 5); break;
    case "four_clover": m.expMul *= 1.10; break;
    case "combo_trophy": m.setExpMul *= 1.25; break;
    case "crown_jewel": pss("crown", 30); break;
    case "piggy_bank": m.coinMul *= 1.4; m.clearCoinBonus += 2; break;
    case "spare_token": m.bonusSpins += 1; break;
    case "hourglass_r": m.firstSpinExpMul *= 1.2; m.lastSpinExpMul *= 1.2; break;
    case "battery": m.flatExp += 6; break;
    case "charm_relic": m.expMul *= 1.12; break;
    // ── 후반(심연) 증강 ──
    case "crown_burst": pss("crown", 100); wmul("crown", 2.5); wadd("crown", 2.0); break;
    case "curse_grad": m.scoreMul *= (1 + 0.15 * cx.curseCount); break;
    case "extreme_overload": m.expMul *= 1.9; m.quotaMul *= 1.3; break;
    case "abyss_lore": if (cx.boss) m.expMul *= 1.5; break;
    // ── 후반(심연) 유물 ──
    case "crown_monolith": pss("crown", 80); wmul("crown", 1.5); break;
    case "black_grad_photo": m.scoreMul *= (1 + 0.12 * cx.curseCount); break;
    case "last_roll": m.lastSpinExpMul *= 2.0; m.firstSpinExpMul *= 0.9; break;
    case "nameless_cup": m.scoreMul *= 1.6; m.quotaMul *= 1.25; break;
    case "cherry_press": pse("cherry", 2); break;
    case "cherry_can": pse("cherry", 3); break;
    case "auto_pen": pse("book", 2); break;
    case "library_card": pse("book", 3); tag("학습", 3); break;
    case "greed_goblet": m.expMul *= 1.10; break;
    case "ominous_skull": m.skullExp += 5; break;
    case "black_report": m.skullExp += 4; break;
    case "bloody_coupon": m.skullExp += 4; m.coinMul *= 1.2; break;
    case "crown_stand": pss("crown", 25); break;
    case "broken_crown": pss("crown", 15); break;
    case "kings_ledger": pss("crown", 20); wmul("crown", 1.5); break;
    case "flame_canister": m.expMul *= 1.08; break;
    case "hot_handle": m.expMul *= 1.09; break;
    case "fate_handle": m.rareWeightMul *= 1.25; break;
    case "gamblers_eye": m.rareWeightMul *= 1.20; break;
    case "old_wallet": m.coinMul *= 1.2; break;
    case "crumpled_coupon": m.coinMul *= 1.2; break;
    case "cursed_wallet": m.coinMul *= 1.3; m.skullExp += 2; break;
    case "practice_pad": pse("book", 2); break;
    case "calculator": pss("gem", 12); break;
    case "lucky_eraser": m.rareWeightMul *= 1.15; break;
    // ── 프리즘(PRISM) 유물 8종 (2026-06-30) — 보스클리어 유물노드 풀. #8(phoenix_thesis)만 cx 게이팅. ──
    case "prism_diploma":   m.expMul *= 1.40; m.scoreMul *= 1.20; break;                              // 🎖️ EXP+40%·점수+20%
    case "golden_ratio":    m.set3ExpMul *= 1.40; m.set4ScoreMul *= 1.40; break;                      // 📐 세트3+ EXP+40%·세트4+ 점수+40%(evaluate 586/609)
    case "starlight_crown": pss("crown", 60); m.coinMul *= 1.30; wmul("crown", 1.4); break;           // 🌟 👑점수+60·등장↑·코인+30%
    case "endless_recess":  m.bonusSpins += 1; m.firstSpinExpMul *= 1.35; m.lastSpinExpMul *= 1.35; break; // ⏱️ 스핀+1·첫/막 EXP+35%
    case "fortunes_wheel":  m.rareBurstExpMul *= 1.60; m.rareBurstScoreMul *= 1.30; m.rareWeightMul *= 1.25; break; // 🎡 희귀등장+25%·희귀2+ EXP+60%·점수+30%(evaluate 582/608)
    case "set_resonator":   m.setExpMul *= 1.50; m.twoSetBonusMul *= 1.30; break;                     // 🎼 세트보너스+50%·2세트+30%(evaluate 533/532)
    case "reapers_pact":    m.skullExp += 7; m.perSkullExp += 3; m.scoreMul *= 1.15; break;           // ⚰️ ☠당 EXP+10(페널티면제)·점수+15%(evaluate 517/562)
    case "phoenix_thesis":  if (cx.stage > 0 && cx.quota > 0 && cx.stageExp < Math.floor(cx.quota * 0.5)) m.cliffBurstExpMul *= 2.0; break; // 🔥 EXP<요구50% 시 그 스핀 EXP×2(evaluate 신규배선)
    case "red_safetynet": pse("cherry", 2); break;
    case "polish_work": pss("gem", 25); break;
    case "greed_calc": m.expMul *= 1.15; break;
    case "overheat_formula": m.expMul *= 1.14; break;
    // ── 신규 16종 (2026-06-29) — stage/스핀/스택/저주 조건부(cx) + per-spin 셀판정(evaluate) ──
    // 초반성장
    case "early_prep":  if (cx.stage >= 1 && cx.stage <= 3) m.expMul *= 1.15; break;                 // S3 이하 +15%(S6+무효)
    case "early_adapt": if (cx.stage >= 1 && cx.stage <= 5) m.expMul *= 1.12; break;                 // S1~5 +12%(S6+무효)
    case "growth_log":  m.firstSpinExpMul *= (1.0 + 0.08 * Math.max(0, Math.min(5, cx.growthStack))); break; // 첫스핀 +8%×스택(0~5)
    case "snowball":    m.expMul *= (1.0 + 0.12 * Math.max(0, Math.min(4, cx.snowStack))); break;    // 다음스테이지 +12%×스택(0~4)
    // 운빨
    case "fortune_check": if (cx.isFirstSpin) m.rareWeightMul *= 1.2; break;                         // 스테이지 첫스핀 희귀+20%
    case "luck_accum":  if (cx.unluckyGauge >= 3) m.rareWeightMul *= 1.3; break;                     // 불운3+면 다음 희귀↑(확정 X)
    case "fate_burst":  m.rareBurstExpMul *= (cx.boss ? 1.7 : 1.8); m.rareBurstScoreMul *= 1.5; break; // 희귀2+ 스핀 EXP/점수↑
    // 막판역전
    case "late_focus":  if (cx.spinsLeft >= 1 && cx.spinsLeft <= 2) m.expMul *= 1.10; break;         // 남은스핀2↓ +10%
    case "cliff_focus": if (cx.isLastSpin && cx.quota > 0 && cx.stageExp < Math.floor(cx.quota * 0.6)) m.lastSpinExpMul *= 1.8; break; // EXP<요구60%&막스핀 +80%
    // fate_bell(운명의종) = 실패직전 자동 추가스핀 → 서비스 처리(run.fateBellUsed 게이트). buildMods 무효과.
    // 세트콤보
    case "pair_match":  m.twoSetBonusMul *= 1.2; break;                                              // 2세트(bestCount==2) 보너스+20%
    case "puzzle_sense": m.set3ExpMul *= 1.25; m.set4ScoreMul *= 1.20; break;                        // 세트3+ EXP+25%·세트4+ 점수+20%
    case "perfect_shape": m.perfectShapeExpMul *= 2.2; break;                                        // 양끝같고 가운데동계열(와일드충족 evaluate서 1.7)
    // 해골저주
    case "skull_watch": m.perSkullExp += 2; m.skull3ScoreMul *= 0.9; break;                          // ☠1개당 EXP+2·☠3+ 점수-10%
    case "sacrifice":   m.expMul *= (1.0 + 0.06 * cx.curseCount); m.clearCoinBonus -= 1; break;      // 저주1개당 EXP+6%·클코인-1
    case "black_diploma": if (cx.curseCount >= 5) { m.expMul *= 1.6; m.scoreMul *= 1.3; m.bonusSpins -= 1; } break; // 저주5+ EXP+60%·점수+30%·스핀-1
  }

  // ── 증강 레벨업 델타(Lv2/Lv3) — 프리즘 제외(AUG_LEVELS 미등록). run.perkLevels 로 전달. ──
  const _ops = { m, pse, pss, wmul, wadd };
  for (const id of perkIds) {
    const lv = (levels && levels[id]) || 1; if (lv < 2) continue;
    const def = AUG_LEVELS[id]; if (!def) continue;
    if (lv >= 2 && def[2]) def[2](_ops);
    if (lv >= 3 && def[3]) def[3](_ops);
  }

  // ── 저주 = 패널티 전용(장점 없음). 이득은 별도 투자(증강 sacrifice/black_diploma, 캐릭터 cultist)로만. ──
  for (const id of curseIds) switch (id) {
    case "hard_exam": m.quotaMul *= 1.10; break;                              // 요구치 +10%
    case "cursed_skulls": wadd("skull", 4.0); m.flatExp -= 4; break;          // 해골 등장↑·EXP -4
    case "speed_test": m.bonusSpins -= 1; break;                             // 스핀 -1
    case "frugal_vow": m.coinMul *= 0.6; break;                              // 코인 -40%
    case "tunnel_vision": m.endsMatchExpMul *= 0.5; m.firstSpinExpMul *= 0.85; break; // 양끝·첫스핀 EXP↓
    case "late_bloomer": m.firstSpinExpMul *= 0.5; break;                    // 첫스핀 EXP -50%
    case "gem_obsession": pse("cherry", -2); pse("book", -2); break;         // 체리·책 EXP -2
    case "high_stakes": m.quotaMul *= 1.08; break;                           // 요구치 +8%
    case "thorny_path": wadd("skull", 3.0); m.skullExp -= 5; break;          // 해골 등장↑·해골 EXP -5
    case "hex_allornothing": m.setExpMul *= 0.5; break;                      // 세트 EXP -50%
    case "sleep_debt": m.flatExp -= 5; break;                               // 스핀마다 EXP -5
    case "diploma_pressure": m.quotaMul *= 1.12; break;                      // 요구치 +12%
    case "exam_week": m.quotaMul *= 1.12; break;                            // 요구치 +12%
    case "blackout": wadd("skull", 4.0); break;                             // 해골 등장↑
    case "pop_quiz": m.bonusSpins -= 1; break;                              // 스핀 -1
    case "student_debt": m.coinMul *= 0.5; break;                           // 코인 -50%
  }

  // 세트 효과
  for (const s of activeSets(perkIds, charId, machineId, deviceId)) switch (s.id) {
    case "set_orchard": pse("cherry", 3); wmul("cherry", 1.25); break;
    case "set_library": pse("book", 3); tag("학습", 3); break;
    case "set_necro": m.skullExp += 4; break;
    case "set_appraiser": pss("gem", 20); break;
    case "set_royal": pss("crown", 40); wadd("crown", 2.0); break;
    case "set_align": m.adjacentSameExp += 10; break;
    case "set_combo": m.setExpMul *= 1.2; break;
    case "set_diurnal": m.firstSpinExpMul *= 1.15; m.lastSpinExpMul *= 1.15; break;
    case "set_necro2": m.skullExp += 5; break;
    case "set_jewels": pss("gem", 20); break;
    case "set_combo2": m.setExpMul *= 1.20; break;
    case "set_royal2": pss("crown", 30); wadd("crown", 2.0); break;
    case "set_cherry_net": pse("cherry", 2); pss("cherry", 12); break;
    case "set_red_harvest": pse("cherry", 3); wmul("cherry", 1.25); break;
    case "set_student": m.flatExp += 4; break;
    case "set_lib_bless": pse("book", 4); tag("학습", 3); break;
    case "set_greed": m.scoreMul *= 1.12; m.coinMul *= 1.10; break;
    case "set_glory_grad": tag("학습", 4); m.lastSpinExpMul *= 1.15; break;
    case "set_skull_lab": m.skullExp += 6; break;
    case "set_black_grad": m.skullExp += 5; m.scoreMul *= 1.12; break;
    case "set_curse_cycle": m.setExpMul *= 1.30; break;
    case "set_crown_rite": pss("crown", 40); wadd("crown", 2.0); break;
    case "set_kings_order": pss("crown", 50); wadd("crown", 2.0); break;
    case "set_flame_lab": pse("flame", 5); m.scoreMul *= 1.12; break;
    case "set_last_ignite": m.lastSpinExpMul *= 1.25; m.scoreMul *= 1.10; break;
    case "set_mechanic": m.setExpMul *= 1.25; break;
    case "set_battery": m.flatExp += 6; break;
    case "set_gambler": m.rareWeightMul *= 1.3; pss("gem", 25); break;
    case "set_shop_reg": m.coinMul *= 1.20; m.clearCoinBonus += 3; break;
    case "set_scholarship": tag("학습", 4); m.clearCoinBonus += 2; break;
    case "set_bomb_calc": m.centerExpMul *= 1.5; m.scoreMul *= 1.10; break;
    case "set_perfect_calc": m.adjacentSameExp += 14; m.centerExpMul *= 1.3; break;
    case "set_safe_grad": m.flatExp += 3; m.scoreMul *= 1.08; break;
  }

  // 저주 자체는 이득 없음(패널티 전용) — 저주 개수 무료 보너스 제거. 이득은 증강/캐릭터 투자로만.
  const nc = curseIds.length;
  // 캐릭터 후처리
  if (charId === "cultist" && nc > 0) m.scoreMul *= (1 + 0.08 * nc);
  if (charId === "minimalist" && perkIds.filter((id) => REL_BY_ID[id]).length <= 3) m.expMul *= 1.25;
  return m;
}

// ── 아이템 레버(NEXTSPIN/PHASE) 오버레이 — INSTANT는 컨트롤러가 처리 ──
export function applyItemMods(base, itemIds) {
  if (!itemIds || !itemIds.length) return base;
  const m = { ...base, symbolWeightMul: { ...base.symbolWeightMul }, weightAdd: { ...base.weightAdd } };
  const wmul = (id, v) => { m.symbolWeightMul[id] = (m.symbolWeightMul[id] ?? 1) * v; };
  const wadd = (id, v) => { m.weightAdd[id] = (m.weightAdd[id] ?? 0) + v; };
  for (const id of itemIds) switch (id) {
    case "energy_drink": m.expMul *= 2.0; break;
    case "magnify": m.rareWeightMul *= 4.0; break;
    case "loaded_dice": wadd("crown", 5.0); m.scoreMul *= 2.0; break;
    case "ward_charm": wmul("skull", 0.0); break;
    case "espresso": m.flatExp += 15; break;
    case "study_streak": m.flatExp += 6; break;
    case "rare_lure": m.rareWeightMul *= 2.0; break;
    case "coin_magnet": m.coinMul *= 2.0; m.clearCoinBonus += 8; break;
    case "dbl_nothing": m.flatExp += 30; m.quotaMul *= 1.2; break;
    case "last_minute": m.lastSpinExpMul *= 2.5; break;
    case "adrenaline": m.expMul *= 3.0; break;
    case "rare_scope": m.rareWeightMul *= 3.0; break;
    case "crown_inject": wadd("crown", 8.0); break;
    case "wild_inject": wadd("wild", 6.0); break;
    case "tutor": m.flatExp += 10; break;
    case "fortune_incense": m.rareWeightMul *= 1.6; break;
    case "coin_press": m.coinMul *= 3.0; break;
    case "overtime": m.lastSpinExpMul *= 2.0; break;
    case "cherry_juice": wmul("cherry", 2.5); break;
    case "bookmark2": wmul("book", 2.5); break;
    case "sparkle_dust": wmul("gem", 2.5); break;
    case "gold_chalk": m.expMul *= 2.0; break;
    case "focus_candy": m.expMul *= 1.15; break;
    case "cram_note": m.lastSpinExpMul *= 2.0; break;
    case "rich_lure": m.rareWeightMul *= 3.0; break;
    case "prof_bribe": m.quotaMul *= 0.85; break;
    case "small_snack": case "skull_shield": case "seal_tape": wmul("skull", 0.0); break;
    case "cherry_basket": wadd("cherry", 6.0); break;
    case "gem_loupe": wmul("gem", 2.0); m.scoreMul *= 2.0; break;
    case "sugar_powder": wmul("cherry", 1.6); m.flatExp += 8; break;
    case "cherry_cracker": wmul("cherry", 2.0); m.scoreMul *= 1.2; break;
    case "book_copy": wmul("book", 2.0); m.flatExp += 8; break;
    case "allnight_note": wmul("book", 1.8); m.flatExp += 12; break;
    case "summary_note": m.flatExp += 9; break;
    case "gem_pouch": wmul("gem", 2.0); m.scoreMul *= 1.25; break;
    case "greed_lens": m.scoreMul *= 1.5; break;
    case "black_candle_i": wmul("skull", 2.0); m.expMul *= 1.3; break;
    case "curse_amp": wmul("skull", 1.6); m.scoreMul *= 1.4; break;
    case "gold_chalk_box": m.expMul *= 1.5; break;
    case "combo_mega": m.lastSpinExpMul *= 2.0; m.scoreMul *= 1.2; break;
    case "cram_note_x2": m.lastSpinExpMul *= 2.5; break;
    case "overload_potion": m.expMul *= 2.0; m.quotaMul *= 1.2; break;
    case "skull_sticker": m.skullScoreBonus += 100; break;
    case "dev_battery": m.expMul *= 1.3; break;
    case "dev_coin": m.expMul *= 1.3; break;
  }
  return m;
}

// ── 패시브 장치 오버레이 (장착 시 매 스핀) ─────────────────────────────
//  dev_safe(하한)·dev_subreel(reel+1)은 spin 호출부에서 추가 처리.
export function applyPassiveDevice(base, deviceId) {
  if (!deviceId) return base;
  const m = { ...base, symbolWeightMul: { ...base.symbolWeightMul } };
  switch (deviceId) {
    case "dev_flame": m.expMul *= 1.15; break;
    case "dev_seal": m.expMul *= 1.05; m.symbolWeightMul.skull = (m.symbolWeightMul.skull ?? 1) * 0.0; break;
    case "dev_overheat": m.expMul *= 1.18; m.weightAdd = { ...m.weightAdd, skull: (m.weightAdd.skull ?? 0) + 1.0 }; break;
    case "dev_subreel": m.expMul *= 0.7; break;
    case "dev_reaper": m.skullExp += 10; break;                      // 후반: 해골이 EXP +10
    case "dev_abyss": m.expMul *= 1.35; m.scoreMul *= 0.9; break;    // 후반: EXP+35%·점수-10%
    case "dev_reactor": m.expMul *= 1.4; m.quotaMul *= 1.15; break;  // 후반: EXP+40%·요구+15%
    // Phase 5 심볼장치(deepOnly). dev_legend_seal 은 전설심볼 랜덤 안정화 플래그만 세팅(evaluate 소비).
    //  나머지 심볼 PASSIVE(압축게이지/확장저울)는 pouch 총량이 필요해 game.js _mods 심화블록에서 주입(여기 no-op).
    case "dev_legend_seal": m.legendStable = true; break;
  }
  return m;
}

// ── 가중 추첨 ─────────────────────────────────────────────────────────
function weighted(rng, mods) {
  let total = 0; const w = new Array(SYMS.length);
  for (let i = 0; i < SYMS.length; i++) {
    const s = SYMS[i];
    let x = s.weight;
    if (s.rare) x *= mods.rareWeightMul;
    x *= mods.symbolWeightMul[s.id] ?? 1;
    x += mods.weightAdd[s.id] ?? 0;
    if (x < 0) x = 0;
    w[i] = x; total += x;
  }
  let r = rng.double() * total;
  for (let i = 0; i < SYMS.length; i++) { r -= w[i]; if (r <= 0) return SYMS[i]; }
  return SYMS[0];
}
const cell = (sym, tag = "") => ({ sym, tag });

// 배율 표기(소수 2자리·끝0/점 제거) — Kotlin fmtMul 캐논.
const fmtMul = (v) => v.toFixed(2).replace(/0+$/, "").replace(/\.$/, "");

export function rollRaw(rng, mods, reel = C.REEL, seedActive = false) {
  const cells = Array.from({ length: reel }, () => cell(weighted(rng, mods)));
  if (seedActive) {
    const grow = rng.pick(["book", "star", "crown"]);
    cells[rng.n(reel)] = cell(SYM_BY_ID[grow], "🌱→");
  }
  return cells;
}
export const cellsFromIds = (ids) => ids.map((id) => SYM_BY_ID[id]).filter(Boolean).map((s) => cell(s));
export const rollOne = (rng, mods) => cell(weighted(rng, mods));

const cellValue = (c) => c.sym.exp + c.sym.score;
const nonEmptyIdx = (cells) => cells.map((c, i) => i).filter((i) => cells[i].sym.id !== "empty");

// NEXTSPIN 셀조작 아이템(armIds 중 셀조작 토큰)을 raw 에 in-place 적용
export function applyCellOps(cells, armIds, rng) {
  const lowest = () => { const idx = nonEmptyIdx(cells); if (!idx.length) return -1; return idx.reduce((a, b) => (cellValue(cells[b]) < cellValue(cells[a]) ? b : a)); };
  const highest = () => { const idx = nonEmptyIdx(cells); if (!idx.length) return -1; return idx.reduce((a, b) => (cellValue(cells[b]) > cellValue(cells[a]) ? b : a)); };
  for (const id of armIds || []) switch (id) {
    case "eraser_old": case "eraser_fine": { const i = lowest(); if (i >= 0) cells[i] = cell(EMPTY_SYM, "🧽"); break; }
    case "eraser_god": { for (let k = 0; k < 2; k++) { const i = lowest(); if (i >= 0) cells[i] = cell(EMPTY_SYM, "🧽"); } break; }
    case "wild_temp": cells[rng.n(cells.length)] = cell(SYM_BY_ID.wild, "🌀"); break;
    case "fake_crown": { const i = highest(); if (i >= 0) cells[i] = cell(SYM_BY_ID.crown, "👑"); break; }
  }
}

// ── 평가 (원시 셀 → 결과) ─────────────────────────────────────────────
export function evaluate(rng, raw, mods, spinIndex, spinsPerStage, flamePenalty = false) {
  const notes = [];
  const cells = raw.map((c) => ({ ...c }));
  const preCells = cells.map((c) => ({ ...c }));   // 폭탄 폭발 전(착지 직후) 표시용 — UI 연출
  const reel = Math.max(1, cells.length);
  const seedNote = cells.find((c) => c.tag === "🌱→");
  if (seedNote) notes.push(`🌱 씨앗→${seedNote.sym.e}`);

  // ══════════════════════════════════════════════════════════════════════
  //  Phase 4 — 심화모드 특수심볼 즉시효과 (additive · 해당 special 셀 존재시에만 발동).
  //  ★일반모드 완전격리: 신규 30종은 weight:0(가중추첨 무등장) + 아래 블록은 전부 has_X 게이트라
  //   해당 심볼이 실제 셀에 없으면 스킵 = 일반경로(체리~왕관·폭탄·자석·세트·잭팟·전역배) 무회귀.
  //  신규 special(SEED_ANY|SEED_HIGH|CATALYST|PURIFY|WANDWILD|MIRROR|TARGET|PUZZLE5|ALARM|HOURGLASS|
  //   RECEIPT|COUPON|CART|GEAR|DEVCD|SETFRAG|SHIELD|EXEMPT|CURSE_*|LUCKY7|PRISM_SYM …)은 기존
  //   evaluate 로직이 === 로만 검사하는 기존 enum(NONE|WILD|BOMB|…)과 미충돌 → NONE 취급되어 신규효과는
  //   오직 이 블록/전역배 직전 캡·반환필드에서만 발동.
  //  누적 특수배수(럭키7/검은초/불안정폭탄/프리즘) — 전역배 직전 C.MAX_SPIN_EXP_MUL(8.0) 로 캡.
  let specialMul = 1;
  //  🧹 정화도구(PURIFY): 해골(SKULL) 1개 → 빈칸. 정화도구 수만큼 앞 해골부터.
  const purifyN = cells.filter((c) => c.sym.special === "PURIFY").length;
  if (purifyN > 0) {
    let done = 0;
    for (let i = 0; i < reel && done < purifyN; i++) {
      if (cells[i].sym.special === "SKULL") { cells[i] = cell(EMPTY_SYM, "🧹"); done++; }
    }
    if (done > 0) notes.push(`🧹 해골 ${done}개 정화`);
  }
  //  🪞 거울(MIRROR): 1번칸 ↔ 마지막칸 상호복사(취급). 거울 자체는 복사소스에서 제외(무의미). reel 가변 대응.
  if (cells.some((c) => c.sym.special === "MIRROR") && reel >= 2) {
    const a = cells[0], b = cells[reel - 1];
    const aOk = a.sym.special !== "MIRROR", bOk = b.sym.special !== "MIRROR";
    if (aOk && bOk) { cells[0] = cell(b.sym, "🪞"); cells[reel - 1] = cell(a.sym, "🪞"); notes.push("🪞 양끝 미러"); }
  }
  //  🧪 촉매(CATALYST): 상위계열 매핑(POUCH_UPGRADE) 가능한 최저등급 심볼 1개를 상위로 강화.
  //   매핑 대상이 없으면 근사(값심볼 1개 +3 EXP·catalystApproxExp 로 per-cell 루프 후 가산). 촉매 여러개여도 1회만.
  let catalystApproxExp = 0;
  if (cells.some((c) => c.sym.special === "CATALYST")) {
    const rank = (id) => POUCH_RARITY_ORDER.indexOf(POUCH_RARITY[id] || "기본");
    // 상위계열 매핑 보유 심볼 중 최저등급 1개
    let bi = -1;
    for (let i = 0; i < reel; i++) {
      const id = cells[i].sym.id;
      if (POUCH_UPGRADE[id] && (bi < 0 || rank(id) < rank(cells[bi].sym.id))) bi = i;
    }
    if (bi >= 0) {
      const from = cells[bi].sym; const up = SYM_BY_ID[POUCH_UPGRADE[from.id]];
      if (up) { cells[bi] = cell(up, "🧪"); notes.push(`🧪 ${from.e}→${up.e} 강화`); }
    } else {
      // 근사: 매핑 대상 없음 → 값심볼 존재 시 +3 EXP(catalystApproxExp), 값심볼도 없으면 무효과.
      if (cells.some((c) => VALUE_IDS.has(c.sym.id))) { catalystApproxExp = 3; notes.push("🧪 촉매(강화 +3)"); }
    }
  }

  // 💣 폭탄 — 등장한 폭탄 개수만큼 각각 양옆 제거(캐논 일치). 폭탄끼리는 안 지움, 이미 비워진 칸은
  //  중복 제거/EXP 이중계산 안 되게 가드(두 폭탄이 같은 칸을 가리켜도 1번만 +EXP).
  let bombExp = 0; const removedIdx = [];
  const bombIdxs = cells.map((c, i) => (c.sym.special === "BOMB" ? i : -1)).filter((i) => i >= 0);
  for (const bi of bombIdxs) {
    for (const j of [bi - 1, bi + 1]) {
      if (j >= 0 && j < reel && cells[j].sym.special !== "BOMB" && cells[j].sym.id !== "empty" && !removedIdx.includes(j)) {
        removedIdx.push(j); cells[j] = cell(EMPTY_SYM, "💥");
      }
    }
  }
  if (removedIdx.length > 0) { bombExp = removedIdx.length * C.BOMB_EXP_PER; notes.push(`💣${bombIdxs.length > 1 ? `×${bombIdxs.length} ` : " "}${removedIdx.length}칸 제거 +${bombExp}`); }
  // 🧲 자석 — 등장한 자석 개수만큼 각각 옆칸(왼쪽 우선→오른쪽) 실심볼을 복사(캐논 일치).
  //  복사 소스는 폭탄 처리 후·자석 적용 전 스냅샷(magSrc) 기준 → 단일 자석 시 기존 동작(폭탄 제거칸은
  //  empty 가드로 스킵) 보존하면서, 자석↔자석 연쇄복사만 차단. 자석/빈칸은 가드.
  const magIdxs = cells.map((c, i) => (c.sym.special === "MAGNET" ? i : -1)).filter((i) => i >= 0);
  if (magIdxs.length > 0) {
    const magSrc = cells.map((c) => ({ ...c }));
    for (const mi of magIdxs) {
      const src = [mi - 1, mi + 1].filter((i) => i >= 0 && i < reel).map((i) => magSrc[i]).find((c) => c.sym.special === "NONE" && c.sym.id !== "empty");
      if (src) { cells[mi] = cell(src.sym, "🧲"); notes.push(`🧲 ${src.sym.e} 복사`); }
    }
  }
  // value 집계 + 와일드
  const counts = {}; let wilds = 0;
  for (const c of cells) {
    if (c.sym.special === "WILD") wilds++;
    else if (VALUE_IDS.has(c.sym.id)) counts[c.sym.id] = (counts[c.sym.id] ?? 0) + 1;
  }
  //  🪄 마법봉(WANDWILD): 무작위 1심볼을 와일드 취급(치환 아님·세트/양끝 보조에만).
  //   ★상한: 마법봉 기여 최대 1 + 실와일드 합이 reel-1 초과 못함. ★잭팟 게이트는 마법봉 기여 제외.
  let wandWilds = 0;
  if (cells.some((c) => c.sym.special === "WANDWILD")) {
    const cap = Math.max(0, (reel - 1) - wilds);   // 실와일드 포함 총 와일드 ≤ reel-1
    wandWilds = Math.min(1, cap);
    if (wandWilds > 0) notes.push("🪄 마법봉 와일드");
  }
  const totalWilds = wilds + wandWilds;
  let bestId = null, bestN = -1;
  for (const k in counts) if (counts[k] > bestN) { bestN = counts[k]; bestId = k; }
  if (bestId != null && totalWilds > 0) counts[bestId] += totalWilds;
  else if (bestId == null && totalWilds > 0) { bestId = "cherry"; counts.cherry = totalWilds; }
  const bestCount = bestId ? counts[bestId] : 0;

  // ── 심화 계열 브릿지 — mods.deepFamilyBridge(game.js _mods 심화블록에서만 주입) 게이팅 ──
  //  상위계열 셀(cherry_ripe/tome/gem_cut/coin_bag/skull_black/ember)에 하위(base) 참조 값강화
  //  (perSymbolExp.cherry 등)를 합산 — "체리강화가 숙성체리에도"(업글 가치 보존). 방향은 하위→상위 단방향.
  //  ★일반모드 완전 무회귀: 일반 mods 엔 플래그 부재 → 항상 0. ☠해골 가산(skullExp/perSkullExp)은
  //   special:"SKULL" 분기가 skull_black 도 이미 커버 — perSymbolExp/Score 맵만 브릿지(이중가산 없음).
  const famBridge = (map, sid) => (mods.deepFamilyBridge && UPG_PARENT[sid]) ? (map[UPG_PARENT[sid]] ?? 0) : 0;
  // 배치 G — 계열 아키타입 곱: 셀 id 의 base 계열(자신 or 상위→base) 기준으로 곱셈 증가분 조회.
  //  ★일반모드 무회귀: deepFamily*Mul 은 심화 _mods 심화블록만 주입 → 일반 mods 엔 항상 빈맵(0 반환).
  const famBase = (sid) => UPG_PARENT[sid] || sid;
  const archMul = (map, sid) => (map ? (map[famBase(sid)] ?? 0) : 0);
  let exp = 0, score = 0, coins = 0;
  const tagCounts = {};
  cells.forEach((c, idx) => {
    const s = c.sym;
    let cellExp = (s.exp + (mods.perSymbolExp[s.id] ?? 0) + famBridge(mods.perSymbolExp, s.id));
    for (const t of s.tags) { tagCounts[t] = (tagCounts[t] ?? 0) + 1; cellExp += mods.tagExpBonus[t] ?? 0; }
    // 심화모드 태그 강화(곱셈 +10%씩) — deepTagMul 은 심화 전용이라 일반모드엔 항상 비어 배수 1(무회귀).
    //  셀의 태그 중 버프 대상이 있으면 그 합만큼 배수(예 #점수 +10% → cellExp *= 1.10). 여러 태그 중복 시 합산.
    //  [LOW-4] 합산 후 ±0.5 클램프 — 태그감각/전공/나침반 중첩 폭주(+0.81 실측) 상한.
    const dtm = mods.deepTagMul;
    if (dtm) { let mul = 0; for (const t of s.tags) mul += dtm[t] ?? 0; mul = Math.max(-0.5, Math.min(0.5, mul)); if (mul) cellExp *= (1 + mul); }
    // 배치 G — 아키타입 EXP 곱(체리🍒/도서관📘/화력🔥 계열). 태그버프와 별개 축(clamp 무관·순수 계열).
    const aem = archMul(mods.deepFamilyExpMul, s.id); if (aem) cellExp *= (1 + aem);
    if (idx === Math.floor(reel / 2)) cellExp *= mods.centerExpMul;
    exp += cellExp;
    // 배치 G — 아키타입 점수 곱(보석상💎 계열). perSymbolScore/브릿지 포함한 셀 점수 전체에 적용.
    let cellScore = s.score + (mods.perSymbolScore[s.id] ?? 0) + famBridge(mods.perSymbolScore, s.id);
    const asm = archMul(mods.deepFamilyScoreMul, s.id); if (asm) cellScore *= (1 + asm);
    score += cellScore;
    // 배치 G — 아키타입 코인 곱(조폐국🪙 계열).
    const acm = archMul(mods.deepFamilyCoinMul, s.id); coins += acm ? s.coin * (1 + acm) : s.coin;
    if (s.special === "DICE") { const d = rng.int(1, 13); exp += d; notes.push(`🎲 +${d}`); }
    // 강령학파☠ — 해골 EXP 분기에도 아키타입 곱 적용(skull 계열 base=skull). skullPenaltyMul 은 quota 경로 별도.
    else if (s.special === "SKULL") { const se = mods.skullExp + (mods.perSkullExp || 0); const ase = archMul(mods.deepFamilyExpMul, s.id); exp += ase ? se * (1 + ase) : se; score += mods.skullScoreBonus; }   // 해골빌드 가산 + skull_watch(perSkullExp) + 해골스티커 점수
  });
  exp += bombExp;
  if (catalystApproxExp) exp += catalystApproxExp;   // 🧪 촉매 근사(+3) — 매핑 대상 없을 때만

  // ── Phase 5 심화: 빈칸활용/빈칸설명서 (심화 심볼증강·유물) ──────────────────────────
  //  ★일반모드 격리: 일반 스핀엔 빈칸 심볼(empty)이 등장하지 않고(주머니 "empty"·폭탄 제거만 빈칸 생성),
  //   deepEmptyScore/deepEmptyExp 는 심화 _mods 심화블록에서만 주입(일반=0) → 이중 격리. has_empty 게이트.
  if (mods.deepEmptyScore || mods.deepEmptyExp) {
    const emptyN = cells.filter((c) => c.sym.id === "empty").length;
    if (emptyN > 0) {
      if (mods.deepEmptyExp) exp += emptyN * mods.deepEmptyExp;
      if (mods.deepEmptyScore) score += emptyN * mods.deepEmptyScore;
      const parts = [];
      if (mods.deepEmptyExp) parts.push(`+${emptyN * mods.deepEmptyExp}EXP`);
      if (mods.deepEmptyScore) parts.push(`+${emptyN * mods.deepEmptyScore}점`);
      notes.push(`▫ 빈칸 ${emptyN}개 활용 ${parts.join("·")}`);
    }
  }

  //  🎯 표적(TARGET): 웹엔 칸 지정 UI 없음 → 근사 = 최고 cellExp 값심볼 칸 1개 효과 +50%(1회만·중복곱 방지).
  //   per-cell 루프의 cellExp 공식을 그대로 재현해 최고 칸을 찾고, 그 50%를 exp 에 가산.
  if (cells.some((c) => c.sym.special === "TARGET")) {
    const cellExpOf = (c, idx) => {
      const s = c.sym; let ce = s.exp + (mods.perSymbolExp[s.id] ?? 0) + famBridge(mods.perSymbolExp, s.id);
      for (const t of s.tags) ce += mods.tagExpBonus[t] ?? 0;
      const dtm = mods.deepTagMul;
      if (dtm) { let mul = 0; for (const t of s.tags) mul += dtm[t] ?? 0; mul = Math.max(-0.5, Math.min(0.5, mul)); if (mul) ce *= (1 + mul); }   // [LOW-4] 본계산과 동일 클램프
      if (idx === Math.floor(reel / 2)) ce *= mods.centerExpMul;
      return ce;
    };
    let best = 0;
    cells.forEach((c, idx) => { if (VALUE_IDS.has(c.sym.id)) { const v = cellExpOf(c, idx); if (v > best) best = v; } });
    if (best > 0) { const bonus = best * 0.5; exp += bonus; notes.push(`🎯 표적 최고칸 +50% (+${Math.floor(bonus)})`); }
  }

  // 🗝 열쇠(KEY) — 금고머신(vault) 테마: 셀당 보물 코인 +KEY_COIN_PER. (구 keyBoost 死플래그 폐지 — 실효과 부여)
  const keyCount = cells.filter((c) => c.sym.special === "KEY").length;
  if (keyCount > 0) { const keyCoins = keyCount * C.KEY_COIN_PER; coins += keyCoins; notes.push(`🗝 보물 +${keyCoins}🪙`); }

  // 세트 — 2개 이상인 모든 값심볼 그룹이 각각 보너스 (와일드는 최다 그룹에 합류됨)
  const setIds = [];
  for (const id in counts) {
    const cnt = counts[id];
    if (cnt >= 2) {
      const n = Math.min(cnt, SET_EXP.length - 1);
      // pair_match: 최다 그룹이 정확히 2세트(bestCount==2)일 때 그 세트 보너스 +20%(twoSetBonusMul) — 세트 가산의 일부
      const twoMul = (id === bestId && bestCount === 2) ? mods.twoSetBonusMul : 1.0;
      const add = SET_EXP[n] * mods.setExpMul * twoMul;
      exp += add; score += SET_SCORE[n];
      setIds.push(id);
      notes.push(`${SYM_BY_ID[id].e}×${cnt} 세트 +${Math.floor(add)}`);
      if (twoMul !== 1.0) notes.push(`👯짝맞춤 +${Math.floor((twoMul - 1.0) * 100)}%`);
    }
  }
  //  🧩 퍼즐(PUZZLE5): 서로 다른 값심볼 종류 수 → 점수 보너스(1회만). reel=5·값심볼 5종(왕관 포함)이라
  //   "정확히 5종"은 극희소 → 완화 2단(4종+ +150 / 5종 +300). counts 아닌 셀의 실 값심볼 id 로 종류 집계.
  if (cells.some((c) => c.sym.special === "PUZZLE5")) {
    const kinds = new Set(cells.filter((c) => VALUE_IDS.has(c.sym.id)).map((c) => c.sym.id)).size;
    let bonus = 0;
    if (kinds >= 5) bonus = 300; else if (kinds >= 4) bonus = 150;
    if (bonus > 0) { score += bonus; notes.push(`🧩 퍼즐 ${kinds}종 +${bonus}점`); }
  }
  // 🎰 잭팟 — ★마법봉 와일드 기여는 잭팟 게이트에서 제외(인위적 잭팟 남발 차단). 세트/표기엔 bestCount 유지.
  let jackpotSym = null;
  const jackpotCount = bestCount - wandWilds;   // 마법봉 취급 와일드 제외 카운트
  if (bestId != null && jackpotCount >= reel && reel >= 5) {
    jackpotSym = bestId;
    const jb = { cherry: 120, book: 320, star: 360, gem: 160, crown: 520 }[bestId] ?? 200;
    exp += jb; score += jb * 5;
    notes.push(`🎰${SYM_BY_ID[bestId].e}×${bestCount} 잭팟! +${jb}EXP·+${jb * 5}점`);
  }
  // ── §9.0 V3 J1: 잭팟 태그 판정(심화모드 deepMode 게이팅) ───────────────────────────────────
  //  ★일반모드 완전격리: mods.deepMode 는 심화 _mods() 심화블록에서만 true 로 주입 → 일반경로 무회귀.
  //  5칸의 jackpotTag 카운트(와일드·빈칸 미기여). 최다 태그 기준 단계 판정: 3=콤보/4=리치/5=태그잭팟.
  //  동일 심볼 잭팟과 공존 가능(jackpotSym 이 설정된 경우) — 단, 두 경로 중복 지급 금지:
  //    동일 심볼 잭팟이 이미 발동했으면 태그잭팟 보상 스킵(태그 결과만 신호 반환).
  //  스핀당 최고 1단계 발동(tagStage 결정).
  let jackpotTagHit = null;   // 최다 태그 id
  let jackpotStage = null;    // "combo" | "reach" | "jackpot" | null
  let feverDelta = 0;         // §9.1 J2 피버 게이지 충전량 신호(J2 에서 소비·J3 bell/wand 확장)
  // §9.2 J3 반환 신호(game.js 가 소비)
  let bellCount = 0;          // 이번 스핀 bell 태그 심볼 수(ulim5·승격 신호용)
  let echoTriggered = false;  // 울림종 리치 시 발동 신호(점수+200, game.js consume)
  let jackpotCrownSignal = false;  // 잭팟왕관 신호(보상등급+1, 스테이지 1회·game.js manage)
  if (mods.deepMode) {
    // 와일드/빈칸 제외·태그 집계
    const tagCount = {};
    for (const c of cells) {
      if (c.sym.special === "WILD" || c.sym.id === "empty") continue;
      const jt = jackpotTagOf(c.sym.id);
      if (jt) tagCount[jt] = (tagCount[jt] || 0) + 1;
    }
    // § 9.2 J3: 슬롯조각(SLOT_SHARD)·잭팟마법봉(JACKPOT_WAND) — 최다 태그로 합류(동적)
    //  슬롯조각: 모든 태그 포함 최다 태그를 1개 더 강화(프리즘 포함).
    //  잭팟마법봉: 최다 태그(단 prism 태그 제외)로 합류.
    const hasSlotShard  = cells.some((c) => c.sym.special === "SLOT_SHARD");
    const hasJpWand     = cells.some((c) => c.sym.special === "JACKPOT_WAND");
    if (hasSlotShard || hasJpWand) {
      // 현재 최다 태그 파악(프리즘 포함)
      let curBest = null, curBestN = 0;
      for (const t in tagCount) if (tagCount[t] > curBestN) { curBestN = tagCount[t]; curBest = t; }
      if (hasSlotShard && curBest) {
        tagCount[curBest] = (tagCount[curBest] || 0) + 1;
        notes.push(`🎰 슬롯조각 — ${curBest} 태그 +1`);
      }
      if (hasJpWand) {
        // 잭팟마법봉: prism 태그 제외한 최다 태그
        let wpBest = null, wpBestN = 0;
        for (const t in tagCount) { if (t === "prism") continue; if (tagCount[t] > wpBestN) { wpBestN = tagCount[t]; wpBest = t; } }
        if (wpBest) { tagCount[wpBest] = (tagCount[wpBest] || 0) + 1; notes.push(`🪄 잭팟마법봉 — ${wpBest} 태그 +1 (prism 제외)`); }
      }
    }
    // 최다 태그 선택
    let bestJtag = null, bestJcount = 0;
    for (const t in tagCount) if (tagCount[t] > bestJcount) { bestJcount = tagCount[t]; bestJtag = t; }
    if (bestJtag && bestJcount >= 3) {
      jackpotTagHit = bestJtag;
      if (bestJcount >= 5) jackpotStage = "jackpot";
      else if (bestJcount >= 4) jackpotStage = "reach";
      else jackpotStage = "combo";
    }
    // bell 태그 집계(종 세트 피버 충전·승격 신호)
    bellCount = tagCount["bell"] || 0;

    // § 9.2 J3: 환호(CHEER) — 콤보/리치/잭팟 보너스 +25% 배율(jackpotStage 판정 후 적용)
    const hasCheer = cells.some((c) => c.sym.special === "CHEER");
    const cheerMul = (hasCheer && jackpotStage) ? 1.25 : 1.0;
    // § 9.2 J3: 대폭죽(BIG_BOOM) — 콤보 시 점수+500·잭팟 시 점수+2000(cheerMul 적용 후)
    const hasBigBoom = cells.some((c) => c.sym.special === "BIG_BOOM");
    // § 9.2 J3: 잭팟왕관(JACKPOT_CROWN) — 잭팟 시 보상등급+1 신호(스테이지 1회 → game.js manage)
    const hasJpCrown = cells.some((c) => c.sym.special === "JACKPOT_CROWN");

    // 태그 이모지+한글명 맵
    const TAG_EMOJI = { crown: "👑 왕관", seven: "7️⃣ 럭키7", coin: "🪙 코인", prism: "🌈 프리즘", curse: "💀 저주", bell: "🔔 종" };
    const tLabel = jackpotTagHit ? (TAG_EMOJI[jackpotTagHit] || jackpotTagHit) : "";
    // 단계별 보상 — 동일 심볼 잭팟(jackpotSym)과 공존 시 중복 지급 금지(태그잭팟 보상 스킵).
    if (jackpotStage === "combo") {
      const baseExpBonus = 8;
      exp += Math.floor(baseExpBonus * cheerMul);
      feverDelta = 15;
      const boomBonus = hasBigBoom ? Math.floor(500 * cheerMul) : 0;
      if (boomBonus) { score += boomBonus; notes.push(`💥 대폭죽 콤보 점수+${boomBonus}`); }
      notes.push(`🎯 ${tLabel} 콤보! (태그 ${bestJcount}개) EXP+${Math.floor(baseExpBonus * cheerMul)}${hasCheer ? " 🎉환호×1.25" : ""}`);
    } else if (jackpotStage === "reach") {
      if (!jackpotSym) { score += Math.floor(300 * cheerMul); }   // 동일심볼잭팟 공존 시 점수 중복 지급 금지
      feverDelta = 25;
      // § 9.2 J3: 울림종(BELL_ECHO) — 종 태그 리치 시 점수+200
      if (jackpotTagHit === "bell") { echoTriggered = cells.some((c) => c.sym.special === "BELL_ECHO"); if (echoTriggered) { score += 200; notes.push("🔔 울림종 — 종 리치! 점수+200"); } }
      notes.push(`🎯 ${tLabel} 리치! (태그 ${bestJcount}개)${jackpotSym ? "" : ` 점수+${Math.floor(300 * cheerMul)}`}${hasCheer ? " 🎉×1.25" : ""} — 1개만 더!`);
    } else if (jackpotStage === "jackpot") {
      if (!jackpotSym) { exp += Math.floor(30 * cheerMul); score += Math.floor(1500 * cheerMul); }   // 동일심볼잭팟 공존 시 중복 지급 금지
      feverDelta = 50;
      const boomBonus2 = hasBigBoom ? Math.floor(2000 * cheerMul) : 0;
      if (boomBonus2) { score += boomBonus2; notes.push(`💥 대폭죽 잭팟 점수+${boomBonus2}`); }
      if (hasJpCrown) { jackpotCrownSignal = true; notes.push("👑 잭팟왕관 — 보상등급+1 (스테이지 1회)"); }
      notes.push(`🎰 ${tLabel} 잭팟!! (태그 ${bestJcount}개)${jackpotSym ? "" : ` EXP+${Math.floor(30 * cheerMul)}·점수+${Math.floor(1500 * cheerMul)}`}${hasCheer ? " 🎉×1.25" : ""}`);
    }

    // § 9.2 J3: 종 세트 피버 충전(피버 게이지 충전 — feverDelta 추가, J2 경로와 동일)
    //  작은종(BELL_SMALL): 종 태그 3개+ 시 추가 feverDelta+15.
    //  황금종(BELL_GOLD): 종 태그 3개+ 시 추가 feverDelta+30 (작은종과 중복 가능).
    //  축제종(BELL_FEST): 피버 중 종 효과 +50% — game.js 가 feverSpins>0 일 때 bellFestBonus 신호로 처리(evaluate는 신호만).
    if (bellCount >= 3) {
      const hasSmallBell  = cells.some((c) => c.sym.special === "BELL_SMALL");
      const hasGoldenBell = cells.some((c) => c.sym.special === "BELL_GOLD");
      if (hasSmallBell) { feverDelta += 15; notes.push(`🔔 작은종 — 종 ${bellCount}개 피버+15`); }
      if (hasGoldenBell) { feverDelta += 30; notes.push(`🔔 황금종 — 종 ${bellCount}개 피버+30`); }
    }
  }
  // 인접 쌍
  if (mods.adjacentSameExp !== 0) {
    let pairs = 0;
    for (let i = 0; i < reel - 1; i++) { const a = cells[i].sym, b = cells[i + 1].sym; if (a.id === b.id && VALUE_IDS.has(a.id)) pairs++; }
    if (pairs > 0) { exp += pairs * mods.adjacentSameExp; notes.push(`🔗 인접 ${pairs}쌍 +${pairs * mods.adjacentSameExp}`); }
  }
  // 양끝
  if (mods.endsMatchExpMul !== 1.0 && reel >= 2) {
    const a = cells[0].sym, b = cells[reel - 1].sym;
    if (a.id === b.id && VALUE_IDS.has(a.id)) { exp *= mods.endsMatchExpMul; notes.push(`↔ 양끝 ${a.e} EXP ×${mods.endsMatchExpMul}`); }
  }
  // ☠ 해골 페널티 — 해골빌드(가산퍽 skullExp/perSkullExp 보유) 시 페널티 면제(해골=자원, +EXP는 위 SKULL서 이미 가산), 없으면 -SKULL_PENALTY/개 위험 유지
  const skulls = cells.filter((c) => c.sym.special === "SKULL").length;
  if (skulls > 0) {
    const skullBonusPer = (mods.skullExp || 0) + (mods.perSkullExp || 0);
    if (skullBonusPer > 0) {
      notes.push(`☠ ${skulls}개 +${skullBonusPer * skulls} (해골빌드)`);  // 페널티 면제 — 가산분만 표시
    } else {
      const pen = skulls * C.SKULL_PENALTY * mods.skullPenaltyMul;
      exp -= pen; if (pen > 0) notes.push(`☠ ${skulls}개 -${Math.floor(pen)}`);
    }
  }
  //  🩸 피방울(CURSE_BLOOD): 셀 exp 8(데이터) + 추가 +2/개 = 개당 +10. 저주게이지 연동은 반환필드→game.js.
  const bloodN = cells.filter((c) => c.sym.special === "CURSE_BLOOD").length;
  if (bloodN > 0) { const add = 2 * bloodN; exp += add; notes.push(`🩸 피방울 ${bloodN}개 +${8 * bloodN + add}`); }
  //  🕯 검은초(CURSE_CANDLE): 해골 수만큼 배율(개당 +25%·상한 ×2.5). 해골 0이면 이번 스핀 EXP 0(저주 리스크).
  if (cells.some((c) => c.sym.special === "CURSE_CANDLE")) {
    if (skulls > 0) { const cm = Math.min(1 + 0.25 * skulls, 2.5); specialMul *= cm; notes.push(`🕯 검은초 ☠${skulls} ×${fmtMul(cm)}`); }
    else { exp = 0; notes.push("🕯 검은초 — 해골없음 EXP 0"); }
  }
  // 🔥 불꽃
  const hasFlame = cells.some((c) => c.sym.special === "FLAME");
  if (hasFlame) { exp *= 1.5; notes.push("🔥 EXP +50%"); }
  if (flamePenalty) { exp *= 0.5; notes.push("🔥 여파 EXP -50%"); }
  // 첫/막 스핀
  if (spinIndex === 0) exp *= mods.firstSpinExpMul;
  if (spinIndex === spinsPerStage - 1) exp *= mods.lastSpinExpMul;

  // ── 신규 16종 per-spin 조건부 EXP 배수 (전역배수 이전) ──
  // 희귀(👑왕관·🌀와일드, rare=true) 개수 — fate_burst 판정
  const rareN = cells.filter((c) => c.sym.rare).length;
  // 💫 fate_burst: 희귀 2개+ 스핀 EXP↑ (보스전 약화는 buildMods 에서 1.7로 세팅)
  if (rareN >= 2 && mods.rareBurstExpMul !== 1.0) {
    exp *= mods.rareBurstExpMul; notes.push(`💫운명폭발 EXP ×${fmtMul(mods.rareBurstExpMul)}`);
  }
  // 🧩 puzzle_sense: 세트3+ EXP ×set3ExpMul
  if (bestCount >= 3 && mods.set3ExpMul !== 1.0) {
    exp *= mods.set3ExpMul; notes.push(`🧩퍼즐 세트${bestCount} EXP ×${fmtMul(mods.set3ExpMul)}`);
  }
  // 💠 perfect_shape: 양끝 같은 값심볼 & 가운데가 같은 계열(또는 와일드충족) → EXP↑(와일드충족 약화 1.7)
  if (mods.perfectShapeExpMul !== 1.0 && reel >= 3) {
    const a = cells[0].sym, b = cells[reel - 1].sym, c = cells[Math.floor(reel / 2)].sym;
    const endsWild = a.special === "WILD" || b.special === "WILD";
    const endsSame = (a.id === b.id && VALUE_IDS.has(a.id)) || (endsWild && (VALUE_IDS.has(a.id) || VALUE_IDS.has(b.id)));
    const endId = VALUE_IDS.has(a.id) ? a.id : VALUE_IDS.has(b.id) ? b.id : null;
    const centerOk = endId != null && (c.id === endId || c.special === "WILD");
    if (endsSame && centerOk) {
      const withWild = endsWild || c.special === "WILD";
      // 실심볼만으로 충족 = +120%(2.2배), 와일드 보조로 충족 = +70%(1.7배)
      const pm = withWild ? 1.7 : mods.perfectShapeExpMul;
      exp *= pm; notes.push(`💠완벽한모양 EXP ×${fmtMul(pm)}`);
    }
  }

  // 🔥 phoenix_thesis(불사조논문) — 요구 50% 미만 게이트(buildMods cx 판정 후 cliffBurstExpMul 스탬프)면 그 스핀 EXP↑
  if (mods.cliffBurstExpMul !== 1.0) {
    exp *= mods.cliffBurstExpMul; notes.push(`🔥불사조 EXP ×${fmtMul(mods.cliffBurstExpMul)}`);
  }

  // ══════════════════════════════════════════════════════════════════════
  //  Phase 4 — 배수형/전설 특수심볼(전역배 직전, specialMul 누적 → 캡). additive · has_X 게이트.
  // ══════════════════════════════════════════════════════════════════════
  let lucky7 = false;   // 반환용(업적/UX)
  //  🧨 불안정폭탄(CURSE_BOOM): 개당 50% 대폭발(×2)·50% 불발(EXP 0). 여러개면 독립 판정(캡으로 제어).
  const boomN = cells.filter((c) => c.sym.special === "CURSE_BOOM").length;
  for (let k = 0; k < boomN; k++) {
    if (rng.double() < 0.5) { specialMul *= 2.0; notes.push("🧨 대폭발 ×2"); }
    else { exp = 0; notes.push("🧨 불발 — EXP 0"); break; }   // 불발이면 이후 폭탄 무의미(EXP 0)
  }
  // ── V3P4: instant 소모형/일회용 효과 (deepMode 전용 — 일반모드 무영향) ──
  //  아래 결과는 game.js _applyDeepSpinMeta 가 소비 후 해당 심볼 -1 처리(instant 제거).
  //  fuse 심볼은 신호만 반환(조건은 game.js 훅에서 판단).
  //  🩹붕대(BANDAGE·instant): 이번 스핀 최저가치 심볼 패널티 감소(해골 패널티 -1 적용·스핀당 1회).
  const hasBandage = cells.some((c) => c.sym.special === "BANDAGE");
  if (hasBandage && skulls > 0) {
    // 해골 패널티를 1개분 감소(소비 처리는 game.js). evaluate 내 skulls 는 패널티 입력으로 쓰임 → 임시 감소.
    exp += C.SKULL_PENALTY;   // 패널티 +C.SKULL_PENALTY 는 이미 음으로 계산됐으므로 다시 +해서 상쇄
    notes.push("🩹 붕대 — 해골 패널티 1개분 감소");
  }
  //  🪢매듭(KNOT·instant): 양옆 첫 칸·마지막 칸이 같은 심볼이면 EXP+20.
  const hasKnot = cells.some((c) => c.sym.special === "KNOT");
  if (hasKnot && reel >= 2 && cells[0].sym.id !== "empty" && cells[0].sym.id === cells[reel - 1].sym.id) {
    exp += 20; notes.push(`🪢 매듭 — 양끝 동일(${cells[0].sym.e}) EXP +20`);
  }
  //  🧃에너지팩(ENERGYPACK·instant): 이번 스핀 EXP +30%(specialMul 에 가산).
  const hasEnergyPack = cells.some((c) => c.sym.special === "ENERGYPACK");
  if (hasEnergyPack) { specialMul *= 1.30; notes.push("🧃 에너지팩 — 이번 스핀 EXP +30%"); }
  //  👑가짜왕관(FAKECROWN·instant): 이번 스핀에서 왕관 취급 처리(업적 카운트 제외 플래그 fakeCrownActive).
  //   왕관 exp/score 는 evaluate 내 crown 심볼 경로로 추가 처리 — 여기선 신호만.
  const hasFakeCrown = cells.some((c) => c.sym.special === "FAKECROWN");
  if (hasFakeCrown) {
    // 가짜왕관: crown 과 동등한 EXP/점수 직접 부여(업적 추적 제외 = fakeCrownActive 플래그로 game.js 에서 관리).
    const crownSym = SYM_BY_ID["crown"]; if (crownSym) { exp += crownSym.exp; score += crownSym.score; }
    notes.push("👑 가짜왕관 — 왕관 취급 (업적 제외)");
  }
  //  🧬진화핵(EVOCORE·instant): 기본 이득 심볼(isAutoDecayTarget) 1개를 SILVER 특수 랜덤으로 교체(셀내 변환).
  //   engine 은 cells 배열 조작만. 변환 후 re-evaluate 없음(기존 cells 로 최종 집계).
  const hasEvoCore = cells.some((c) => c.sym.special === "EVOCORE");
  if (hasEvoCore) {
    const baseIdxs = cells.map((c, i) => i).filter((i) => isAutoDecayTarget(cells[i].sym.id));
    if (baseIdxs.length > 0) {
      const bi = baseIdxs[rng.n(baseIdxs.length)];
      // SILVER 특수 풀에서 랜덤 1개
      const silverPool = POUCH_SYMBOLS.filter((id) => {
        const cat = POUCH_CAT[id] || "special";
        const tier = TIER_BY_RARITY[POUCH_RARITY[id] || "기본"] || "SILVER";
        return cat === "special" && tier === "SILVER" && SYM_BY_ID[id];
      });
      if (silverPool.length > 0) {
        const newId = rng.pick(silverPool); const newSym = SYM_BY_ID[newId];
        const from = cells[bi].sym;
        cells[bi] = cell(newSym, "🧬");
        notes.push(`🧬 진화핵 — ${from.e}${from.n} → ${newSym.e}${newSym.n} 변환`);
      }
    }
  }
  //  7️⃣ 럭키7(LUCKY7): 3개+ → EXP/점수/코인 7배. ★배수 상한(specialMul 캡)으로 폭주 제어.
  //   🔏전설봉인기(mods.legendStable): 럭키7 자체는 결정적(3개+면 항상 발동)이라 안정화 대상 아님(표기만).
  const luckyN = cells.filter((c) => c.sym.id === "lucky7").length;
  if (luckyN >= 3) { lucky7 = true; specialMul *= 7; score *= 7; coins *= 7; notes.push(`7️⃣ 럭키7 ×${luckyN} — 7배!${mods.legendStable ? " 🔏안정" : ""}`); }
  //  🌈 프리즘(PRISM_SYM): 무작위 프리즘급 미니효과 1택(폭 좁게). 여러개면 각각 1택.
  //   🔏전설봉인기(mods.legendStable): 랜덤 대신 최선 효과(EXP ×1.5)로 안정 발동(변동 완화). deepOnly 장치·근사.
  const prismN = cells.filter((c) => c.sym.special === "PRISM_SYM").length;
  for (let k = 0; k < prismN; k++) {
    const pick = mods.legendStable ? 3 : rng.n(4);   // legendStable=최선(EXP ×1.5) 고정
    switch (pick) {
      case 0: exp += 40;   notes.push("🌈 프리즘 — EXP +40"); break;
      case 1: score += 120; notes.push("🌈 프리즘 — 점수 +120"); break;
      case 2: coins += 3;  notes.push("🌈 프리즘 — 코인 +3"); break;
      default: specialMul *= 1.5; notes.push(`🌈 프리즘 — EXP ×1.5${mods.legendStable ? " 🔏안정" : ""}`); break;
    }
  }
  //  ★특수배수 누적 캡(럭키7×7·검은초·폭탄·프리즘 곱 폭주 차단). 일반경로는 specialMul=1(무영향).
  if (specialMul !== 1) {
    specialMul = Math.min(specialMul, C.MAX_SPIN_EXP_MUL);
    exp *= specialMul;
  }

  // 전역
  const preMul = Math.max(0, Math.floor(exp));
  exp = exp * mods.expMul + mods.flatExp;
  // ── 신규 16종 per-spin 점수 배수 (전역 scoreMul 이전) ──
  if (rareN >= 2 && mods.rareBurstScoreMul !== 1.0) score *= mods.rareBurstScoreMul;       // 💫 fate_burst 점수↑
  if (bestCount >= 4 && mods.set4ScoreMul !== 1.0) score *= mods.set4ScoreMul;             // 🧩 puzzle_sense 세트4+ 점수↑
  if (skulls >= 3 && mods.skull3ScoreMul !== 1.0) {                                        // 👁️ skull_watch ☠3+ 점수-10%
    score *= mods.skull3ScoreMul; notes.push(`👁️해골관찰 ☠${skulls} 점수 ×${fmtMul(mods.skull3ScoreMul)}`);
  }
  score = score * mods.scoreMul + mods.flatScore;
  coins = Math.floor(coins * mods.coinMul);

  const finalExp = Math.max(0, Math.floor(exp));
  return {
    cells, preCells, bomb: bombIdxs.length > 0 ? { idxs: bombIdxs, removed: removedIdx } : null,
    exp: finalExp, score: Math.max(0, Math.floor(score)), coins,
    counts, tagCounts, bestSetId: bestId, bestSetCount: bestCount, setIds, skulls,
    seedNext: cells.some((c) => c.sym.special === "SEED"), hasFlame, keyCount,
    jackpotSym, notes, preMul, mul: mods.expMul, flat: mods.flatExp,
    // ── Phase 4 특수심볼 상태/메타 신호(심화모드 전용) — game.js(담당 2/2) 가 소비. ──
    //  일반모드는 신규 special 셀이 없어 아래 전부 falsy/null(무영향). 즉시효과는 위에서 이미 exp/score/coins 에 반영됨.
    //  다음스핀 상태(game.js run 필드로 저장·다음 스핀에 적용):
    //   growNext: "ANY"(씨앗→기본심볼) | "HIGH"(새싹→체리/책/별) | null — game.js _roll 후처리 성장
    growNext: cells.some((c) => c.sym.special === "SEED_ANY") ? "ANY"
            : cells.some((c) => c.sym.special === "SEED_HIGH") ? "HIGH" : null,
    alarmNext: cells.some((c) => c.sym.special === "ALARM"),           // ⏰ 다음스핀 EXP+10% (r.pendingNextExpMul)
    carryExp: cells.some((c) => c.sym.special === "HOURGLASS") ? Math.floor(finalExp * 0.3) : 0,  // ⏳ 이번 30% 다음스핀 이월
    gearNext: cells.some((c) => c.sym.special === "GEAR"),             // ⚙ 다음스핀 EXP+10%(근사) (r.pendingNextExpMul)
    //  메타 신호(game.js 가 run 플래그로 세팅 → 상점/보스/장치/저주게이지):
    receiptNext: cells.some((c) => c.sym.special === "RECEIPT"),       // 🧾 다음상점 -10%
    couponNext: cells.some((c) => c.sym.special === "COUPON"),         // 🎟 다음상점 상품1 할인
    cartNext: cells.some((c) => c.sym.special === "CART"),             // 🛒 다음상점 칸+1
    shieldNext: cells.some((c) => c.sym.special === "SHIELD"),         // 🛡 보스 패널티 1회 방어
    exemptNext: cells.some((c) => c.sym.special === "EXEMPT"),         // 📋 보스 특수룰 일부 무시
    batteryNext: cells.some((c) => c.sym.special === "DEVCD"),         // 🔋 장치 쿨다운-1(근사)
    kitNext: cells.some((c) => c.sym.special === "KIT"),               // 🧰 정비키트(근사): 장치 재사용/정비소 등장(game.js)
    // ── Phase 5 부활: 형광펜/복습책 (심화 증강노드 전제) ──
    augChanceNext: cells.some((c) => c.sym.special === "AUGCHANCE"),   // 🖍 다음 증강 레벨업 확률 +15%
    augLevelNext: cells.some((c) => c.sym.special === "AUGLEVEL"),     // 📚 보유 증강 1개 즉시 레벨업
    setFrag: cells.some((c) => c.sym.special === "SETFRAG"),           // 🧩 세트 관련 근사(game.js)
    //  저주게이지(불운게이지 근사)를 올리는 심볼 신호:
    curseGaugeUp: cells.filter((c) => c.sym.special === "CURSE_BLOOD" || c.sym.special === "CURSE_EYE").length,
    curseEyeNext: cells.some((c) => c.sym.special === "CURSE_EYE"),    // 🧿 다음 보상등급/후보 근사
    lucky7,                                                            // 7️⃣ 이번 스핀 럭키7 발동(업적/UX)
    // ── V3P4 instant/fuse 신호 ──────────────────────────────────────────
    //  instant(이번 스핀 소비·game.js _applyDeepSpinMeta 에서 해당 심볼 덱-1):
    hasBandage, hasKnot, hasEnergyPack, hasFakeCrown, hasEvoCore,
    //  fuse(조건 도달 시 소비·각 훅 발동 시 game.js 가 처리):
    hasSafePin: cells.some((c) => c.sym.special === "SAFEPIN"),      // 🧷 레벨업 실패 시 누적
    hasCrystal: cells.some((c) => c.sym.special === "CRYSTAL"),      // 🔮 다음 보상 후보 +1 (fuse: 상점 진입 훅)
    hasTempWild: cells.some((c) => c.sym.special === "TEMPWILD"),     // 🧲 이번 스핀 와일드 취급 (fuse: 릴 추출 시 훅)
    hasFateVortex: cells.some((c) => c.sym.special === "FATEVORTEX"), // 🌀 스핀 2회 굴려 유리한 쪽 (fuse: 스핀전 훅)
    hasBlackCard: cells.some((c) => c.sym.special === "BLACKCARD"),   // 💳 다음 상점 1개 무료+불운+1 (fuse: 상점 진입 훅)
    hasShackle: cells.some((c) => c.sym.special === "SHACKLE"),       // ⛓ 보스 관련 (상주·evaluateBoss 게이팅)
    // ── §9.0 V3 J1: 잭팟 태그 결과 신호 ──
    //  jackpotStage: "combo"|"reach"|"jackpot"|null — game.js 가 단계별 보상 배선+배너 표시에 소비.
    //  jackpotTagHit: 발동된 잭팟 태그 id(null이면 미발동). 리치 바이어스 배선에 사용.
    //  feverDelta: §9.1 J2 피버 게이지 충전 신호(J3 종 세트 충전도 이 경로로 반환).
    //  ★일반모드는 mods.deepMode=undefined(falsy) → 판정 진입 없음 → 전부 null/0(무영향).
    jackpotStage, jackpotTagHit, feverDelta,
    // ── §9.2 J3 신호 ──
    bellCount,            // 이번 스핀 bell 태그 수 (bell_ticket fuse 조건·종소리티켓 승격 판정)
    echoTriggered,        // 울림종 발동 여부 (이미 점수+200 반영됨)
    jackpotCrownSignal,   // 잭팟왕관 발동 신호(게임모드 보상등급+1, 스테이지 1회 제한 → game.js)
    hasBellFest: mods.deepMode ? cells.some((c) => c.sym.special === "BELL_FEST") : false, // 🎊 축제종 보유
    hasReachMark: mods.deepMode ? cells.some((c) => c.sym.special === "REACH_MARK") : false, // 🎯 리치표식 보유
    hasRetryReel: mods.deepMode ? cells.some((c) => c.sym.special === "RETRY_REEL") : false, // 🔁 재도전릴 보유
    hasJpTicket: mods.deepMode ? cells.some((c) => c.sym.special === "JACKPOT_TICKET") : false, // 🎟 잭팟티켓 보유
  };
}

export const spinsPerStage = (mods) => Math.max(C.MIN_SPINS, C.SPINS_PER_STAGE + mods.bonusSpins);

// ── 보스 규칙을 평가 결과 EXP 에 적용 ─────────────────────────────────
export function applyBossExp(exp, boss, spinIndex, spins, result) {
  if (!boss) return { exp, note: "" };
  switch (boss.id) {
    case "finals": {
      if (spinIndex === 0) return { exp: Math.floor(exp * 0.9), note: "📝 첫스핀 -10%" };
      if (spinIndex === spins - 1) return { exp: Math.floor(exp * 2.0), note: "📝 막스핀 ×2" };
      return { exp, note: "" };
    }
    case "strict":
      if (result.bestSetCount < 3) return { exp: Math.floor(exp * 0.5), note: "👨‍🏫 3매치 없음 ×0.5" };
      return { exp, note: "" };
    case "luck": {
      const has = result.cells.some((c) => c.sym.id === "star" || c.sym.id === "crown" || c.sym.special === "WILD");
      return has ? { exp: Math.floor(exp * 1.8), note: "🎲 ⭐👑🌀 ×1.8" } : { exp: Math.floor(exp * 0.8), note: "🎲 없음 ×0.8" };
    }
    default: return { exp, note: "" };
  }
}

// ── 특수 스핀명령 코인 비용 / 효과 설명 (SlotV2Engine.kt:2361-2384) ────
//  mode: FOCUS=1·LAST=2·PRAY=3·ALLIN=4 (그 외/N=0). boss=true 면 +1, 상한 5.
//  비용 0(=일반 스핀)은 0 그대로 유지.
export function cmdCoinCost(mode, boss) {
  let base;
  switch (mode) {
    case "FOCUS": base = C.CMD_COST_FOCUS; break;
    case "LAST":  base = C.CMD_COST_LAST;  break;
    case "PRAY":  base = C.CMD_COST_PRAY;  break;
    case "ALLIN": base = C.CMD_COST_ALLIN; break;
    default:      base = 0;
  }
  if (base === 0) return 0;
  const withBoss = base + (boss ? C.CMD_COST_BOSS_SURCHARGE : 0);
  return Math.min(withBoss, C.CMD_COST_MAX);
}
// 특수 스핀명령 효과 설명 한 줄. FOCUS/ALLIN/PRAY/LAST 외에는 빈 문자열.
export function cmdEffectDesc(mode) {
  switch (mode) {
    case "FOCUS": return "결과가 나쁘면 최소 EXP 보장 (대박 확률↓)";
    case "ALLIN": return "EXP ×2 (☠ 2개 이상이면 0)";
    case "PRAY":  return "불운 보정 + 낮은 확률로 기적 (×3)";
    case "LAST":  return "막판 스핀 EXP ×1.75";
    default:      return "";
  }
}

// ── 특수 스핀(집중/올인/기도/최후) 을 EXP 에 적용 ─────────────────────
//  주: FOCUS 는 rareWeightMul ×0.5 를 굴림 전에 적용(컨트롤러). 여기선 사후 EXP 보정.
export function applySpecialSpin(mode, exp, ctx) {
  const { quotaVal, spins, skulls, rng } = ctx;
  const per = quotaVal / spins;
  switch (mode) {
    case "FOCUS": return { exp: Math.max(exp, Math.floor(per * 0.6)), note: "🎯 집중(하한 보장)" };
    case "ALLIN": return skulls < 2 ? { exp: exp * 2, note: "🎰 올인 성공 ×2" } : { exp: 0, note: "🎰 올인 실패 (해골 2개↑)" };
    case "PRAY": {
      if (rng.double() < 0.08) return { exp: exp * 3, note: "🙏 기적! ×3" };
      if (exp < per) return { exp: exp + 25, note: "🙏 기도 +25" };
      return { exp, note: "🙏 기도(무응답)" };
    }
    case "LAST": return { exp: Math.floor(exp * 1.75), note: "⏰ 최후의 스핀 ×1.75" };
    default: return { exp, note: "" };
  }
}

// ── 결정형 티어 보상 시스템 (SlotV2Engine.kt 포팅) ────────────────────
// 티어 표기 = 기존 컨벤션 그대로 문자열 "SILVER" | "GOLD" | "PRISM" (data.js 의 perk.t 와 동일).

// 일반 증강/유물 노드 기본 티어 — 클리어 스테이지 결정형:
//  5의배수=프리즘(우선), 3의배수=골드, 그 외 실버. (≤0 은 실버.)
//  Kotlin tierForClearedStage 캐논.
export function tierForClearedStage(stage) {
  if (stage <= 0) return "SILVER";
  const m = stage % 5;
  if (m === 0) return "PRISM";   // 5·10·15… 클리어 → 프리즘
  if (m === 3) return "GOLD";    // 3·8·13… 클리어 → 골드
  return "SILVER";               // 그 외 = 실버 중심 (+ 10% 증강 레벨업 기회)
}
// 한 등급 위(실버→골드→프리즘, 프리즘은 그대로). 운빨 등급업용. Kotlin tierUp 캐논.
export function tierUp(tier) {
  return tier === "SILVER" ? "GOLD" : tier === "GOLD" ? "PRISM" : "PRISM";
}

// 진행중(req 1개+ 보유·미완성) 세트의 빠진 *증강* 조각 1개를 반환. Kotlin setSynergyAug 캐논.
//  - heldIds: 보유 perk id 집합/배열, excludeIds: 이미 후보에 든 id(+held) 집합/배열.
//  - cat: 시그니처 패리티용(미사용). Kotlin 도 노드 cat 과 무관하게 항상 AUGMENT 조각만 주입
//    (이름이 setSynergyAug 인 이유) — perk(id) 는 전체에서 찾되 cat==AUGMENT 만 채택.
//  - 가장 근접한 세트(미보유 req 최소) 우선. 없으면 null. (메인 티어와 다를 수 있음 = 세트 완성 유도)
export function setSynergyPick(heldIds, excludeIds, cat, rng) {
  const held = new Set(heldIds);
  const exclude = new Set(excludeIds);
  const augById = new Map(AUGMENTS.map((p) => [p.id, p]));   // 조각은 증강만(Kotlin cat==AUGMENT 고정)
  // (남은 미보유 req 수) 오름차순 = 근접 세트 우선.
  const ranked = SETS
    .filter((s) => s.req.some((id) => held.has(id)) && !s.req.every((id) => held.has(id)))
    .map((s) => ({ s, remain: s.req.filter((id) => !held.has(id)).length }))
    .sort((a, b) => a.remain - b.remain);
  for (const { s } of ranked) {
    const missing = s.req
      .filter((id) => !held.has(id) && !exclude.has(id))
      .map((id) => augById.get(id))
      .filter((p) => p);   // 증강 조각만(유물·미존재 제외)
    if (missing.length) return rng.pick(missing);
  }
  return null;
}

// 후보 perkId 를 지금 고르면 어떤 세트가 진행/완성되는지 라벨. Kotlin setSynergyName 캐논(태그 표기용).
export function setSynergyName(perkId, heldIds) {
  const held = new Set(heldIds);
  if (held.has(perkId)) return null;
  let best = null, bestRemain = Infinity;
  for (const s of SETS) {
    if (!s.req.includes(perkId)) continue;
    const others = s.req.filter((id) => id !== perkId);
    if (!others.some((id) => held.has(id))) continue;   // 진행 중(다른 조각 1개+ 보유)
    const remain = s.req.filter((id) => id !== perkId && !held.has(id)).length;
    if (remain < bestRemain) { bestRemain = remain; best = s; }
  }
  if (!best) return null;
  return bestRemain === 0 ? `${best.n} 완성` : `${best.n} 시너지`;
}

// 티어순수 픽 — 결정된 forceTier 풀에서만 채움(타티어 혼용 금지). Kotlin pickPerksByTier(forceTier) 캐논.
//  · bossClear=true → 프리즘 강제(해당 풀 비면 전체 프리즘 폴백). forceTier 가 우선.
//  · 보유 제외. 3개 못 채우면 적게 제시(타티어로 메우지 않음).
export function pickPerksByTier(pool, rng, held, { forceTier, bossClear = false } = {}) {
  const taken = new Set(held);
  const avail = pool.filter((p) => !taken.has(p.id));
  if (!avail.length) return [];
  // forceTier 가 bossClear 보다 우선(Kotlin: forceTier != null 분기 먼저). forceTier 없으면 bossClear→PRISM.
  let tier = forceTier || (bossClear ? "PRISM" : "SILVER");
  // 티어순수 풀 — 선택 티어만(타티어 혼용 절대 금지).
  //  유물엔 프리즘이 없음 → 프리즘 유물 노드는 풀이 빈다. 그 경우에만 *티어순수 유지*하며 한 단계 낮춰 폴백
  //  (PRISM→GOLD→SILVER). 폴백 후에도 한 티어로만 채워 순수성 보존. (증강은 전 티어 존재 → 폴백 없음.)
  let tierPool = pool.filter((p) => p.t === tier && !taken.has(p.id));
  if (!tierPool.length) {
    for (const lower of (tier === "PRISM" ? ["GOLD", "SILVER"] : tier === "GOLD" ? ["SILVER"] : [])) {
      tierPool = pool.filter((p) => p.t === lower && !taken.has(p.id));
      if (tierPool.length) { tier = lower; break; }
    }
  }
  // ── 패밀리 게이팅 ── 같은 계열은 "보유한 같은 패밀리 수 + 1" 랭크만 후보(약→강 잠금해제),
  //  그리고 한 오퍼에 같은 패밀리는 1개만. (미등록 증강은 고유 패밀리라 제약 없음. 유물엔 AUG_FAMILY 없음 → 무영향.)
  const initialHeld = [...held];
  const heldFamCount = (fam) => initialHeld.reduce((n, id) => n + (PERK_FAMILY[id] && PERK_FAMILY[id][0] === fam ? 1 : 0), 0);
  const eligible = (p) => { const [fam, rank] = famOf(p); return rank === heldFamCount(fam) + 1; };
  const out = []; const usedFams = new Set(); let guard = 0;
  while (out.length < 3 && guard++ < 120) {
    const cand = tierPool.filter((p) => !taken.has(p.id) && eligible(p) && !usedFams.has(famOf(p)[0]));
    if (!cand.length) break;
    const pick = rng.pick(cand); taken.add(pick.id); usedFams.add(famOf(pick)[0]); out.push(pick);
  }
  return rng.shuffle(out);
}

// 증강/유물 노드 오퍼 생성 — 결정형 티어 + 10% 등급업 + 5% 세트조각 주입. Kotlin offerPerks 캐논.
//  opts: { clearedStage, bossClear } — clearedStage 미지정 시 stage-1(다음스테이지 기준) 보정 없음 → 호출부에서 전달.
//  opts.compatFilter?: (perk)=>boolean — 세트조각 주입 게이트(setSynergyPick 은 raw AUGMENTS 를 뒤지므로
//   심화 관련성 필터가 여기서만 뚫림 → 미통과 조각은 주입 취소·원래 3번째 카드 유지). 미전달 = 기존과 동일 동작.
//  반환 옵션 객체는 원본 perk 를 복제 후 tierUp/setTag 메타를 부착(UI 자동 전파). meta = { tier, tierBumped, synInjected }.
export function offerPerks(pool, cat, rng, held, opts = {}) {
  const clearedStage = opts.clearedStage ?? 0;
  const bossClear = opts.bossClear ?? (clearedStage > 0 && clearedStage % 5 === 0);
  const baseTier = tierForClearedStage(clearedStage);
  // 운빨 10% 등급업(한 등급 위). 보스클리어=프리즘이라 등급업 무의미하나 Kotlin 과 동일 순서로 굴림.
  let tierBumped = false;
  let nodeTier = baseTier;
  if (rng.n(100) < 10) { const up = tierUp(baseTier); if (up !== baseTier) { tierBumped = true; nodeTier = up; } }
  if (opts.forceTier) { nodeTier = opts.forceTier; tierBumped = (nodeTier !== baseTier); }   // 프리즘 잉크 등 강제 티어
  let picks = pickPerksByTier(pool, rng, held, { forceTier: nodeTier, bossClear });
  if (!picks.length) return { options: [], meta: { tier: nodeTier, tierBumped: false, synInjected: false } };
  const mainTier = picks[0].t;
  // 🧩 세트 시너지 조각 5% 주입 — 마지막 칸 교체(메인 티어 칸 보존). 증강·유물 노드 모두.
  let synInjected = false;
  if (rng.n(100) < 5 && picks.length >= 2) {
    const exclude = new Set([...held, ...picks.map((p) => p.id)]);
    const syn0 = setSynergyPick(held, exclude, cat, rng);
    // compatFilter 미통과(심화 관련성 밖 조각) → 주입 취소. 미전달 시 syn0 그대로(rng 소비 순서 포함 기존과 동일).
    const syn = (syn0 && opts.compatFilter && !opts.compatFilter(syn0)) ? null : syn0;
    if (syn && !picks.some((p) => p.id === syn.id)) {
      picks = [...picks.slice(0, -1), syn];   // 마지막 칸 교체
      synInjected = true;
    }
  }
  // 옵션 객체 = perk 복제 + 메타 부착(원본 불변, UI 로 자동 전파).
  //  · setTag: 세트조각으로 주입된 마지막 칸 항목. 그 조각 티어가 메인티어와 같을 수도 있으나
  //    "세트로 들어온 항목"이라는 사실 자체를 표기해야 하므로 티어 일치 여부와 무관하게 태깅
  //    (스펙: "세트로 들어온 항목만 세트 태그"). tierUp 은 일반 표시이므로 setTag 와 동시 부여 안 함.
  const synIdx = synInjected ? picks.length - 1 : -1;
  const options = picks.map((p, i) => {
    const o = { ...p };
    if (i === synIdx) o.setTag = true;
    else if (tierBumped && p.t === nodeTier && p.t !== baseTier) o.tierUp = true;
    return o;
  });
  return { options, meta: { tier: mainTier, tierBumped, synInjected, baseTier, nodeTier } };
}

export const pickAugments = (rng, stage, held, n = 3, opts = {}) =>
  offerPerks(AUGMENTS, "AUGMENT", rng, held, { clearedStage: opts.clearedStage ?? (stage - 1), ...opts }).options.slice(0, n);
export const pickRelics = (rng, stage, held, n = 3, opts = {}) =>
  offerPerks(RELICS, "RELIC", rng, held, { clearedStage: opts.clearedStage ?? (stage - 1), ...opts }).options.slice(0, n);
// §3 Step 1: deepMode 인자 추가 — 완전 no-op 아이템(timeline_ticket) 심화 진열 제외.
//  일반모드(deepMode 생략/false) 호환 유지 — 일반 경로 동작 비트 동일.
export function pickItems(rng, n = 3, deepMode = false) {
  const pool = deepMode ? ITEMS.filter((it) => it.id !== "timeline_ticket") : ITEMS;
  return rng.shuffle(pool).slice(0, n);
}
export function pickDevices(rng, stage, owned, n = 1) {
  const rareChance = Math.min(0.6, 0.15 + stage * 0.03);
  const out = [];
  // ★Phase 5 격리: 심볼 장치(deepOnly)는 일반 보스 드랍/이벤트 풀에서 제외(심화 업적으로만 획득).
  //  일반 후보 = deepOnly 아님. (심화모드도 여기선 심볼장치를 드랍하지 않음 — 시작 전 장착으로만 사용.)
  const base = DEVICES.filter((d) => !d.deepOnly);
  for (let i = 0; i < n; i++) {
    const wantRare = rng.double() < rareChance;
    let pool = base.filter((d) => (wantRare ? d.rare : !d.rare) && !owned.has(d.id) && !out.some((o) => o.id === d.id));
    if (!pool.length) pool = base.filter((d) => !owned.has(d.id) && !out.some((o) => o.id === d.id));
    if (!pool.length) pool = base;
    out.push(rng.pick(pool));
  }
  return out;
}

// ── 테마 빌드 도감 완성판정 (SlotV2Engine.kt evalThemeBuilds 캐논 포팅) ──
//  순수함수. game.js 가 클리어/게임오버 시 ctx 를 채워 호출 → 충족 bld_<id> Set 반환.
//  필드 = 런누적(run*) + 보유(perks/curses) + machineId/deviceId + 이번 이벤트 플래그 + lifetime(skullTotal/closeClears).
//  perk 계열 카운트는 desc 문자열로(체리/책/막스핀배율). isPrismPerk = AUG t==PRISM. Kotlin 동형.
const perkDescCount = (perks, predicate) =>
  perks.reduce((acc, id) => { const p = PERK_BY_ID[id]; return acc + (p && p.d && predicate(p.d) ? 1 : 0); }, 0);
const isPrismPerk = (id) => (AUG_BY_ID[id] && AUG_BY_ID[id].t === "PRISM") || false;

export function evalThemeBuilds(ctx = {}) {
  const out = new Set();
  const perks = ctx.perks || [];
  const curses = ctx.curses || [];
  const nCurses = curses.length;
  const cherryPerks = perkDescCount(perks, (d) => d.includes("🍒"));
  const bookPerks = perkDescCount(perks, (d) => d.includes("📘"));
  const lastSpinPerks = perkDescCount(perks, (d) => d.includes("마지막 스핀") || d.includes("막스핀") || d.includes("막 스핀"));
  const prismPerks = perks.filter((id) => isPrismPerk(id)).length;
  const stage = ctx.stage || 0;
  const dev = (id) => ctx.deviceId === id || ctx.device2Id === id;

  // ── 성장형 ──
  if ((ctx.runFastClears || 0) >= 3) out.add("bld_fast_start");
  if (stage >= 5 && perks.length >= 3) out.add("bld_model_growth");
  if (cherryPerks >= 2 && stage >= 7) out.add("bld_cherry_sprout");
  if (bookPerks >= 2 && stage >= 7) out.add("bld_library_start");
  if (prismPerks === 0 && stage >= 10) out.add("bld_foundation");
  // ── 운명형 ──
  if ((ctx.runPrayWins || 0) >= 2) out.add("bld_fate_hand");
  if (ctx.machineId === "casino" && stage >= 10) out.add("bld_dice_grad");
  if ((ctx.runCrowns || 0) >= 10) out.add("bld_crown_caller");
  if (ctx.isBossClear && (ctx.clearSpinRareCount || 0) >= 5) out.add("bld_prob_hacker");
  if (ctx.oracleUsedThisRun && ctx.jackpotThisRun) out.add("bld_jackpot_seer");
  // ── 역전형 ──
  if ((ctx.runLastSpinClears || 0) >= 3) out.add("bld_cliff_pass");
  if ((ctx.closeClears || 0) >= 5) out.add("bld_heartbreaker");
  if (lastSpinPerks >= 3 && ctx.isBossClear) out.add("bld_cram_grad");
  if (ctx.bellUsedThisClear && ctx.isBossClear) out.add("bld_miracle_cert");
  if (stage >= 10 && ctx.isLastSpinClear) out.add("bld_last_candle");
  // ── 조합형 ──
  if (ctx.machineId === "magnet" && (ctx.runSet4 || 0) >= 1) out.add("bld_magnet_grad");
  if (ctx.clearSpinWildJackpot) out.add("bld_wild_puzzle");
  if (ctx.pinUsedThisStage) out.add("bld_pinned_fate");
  if (ctx.copyMadeSet4) out.add("bld_copy_answer");
  if ((ctx.runAdjPairs || 0) >= 5) out.add("bld_chain");
  // ── 위험형 ──
  if ((ctx.skullTotal || 0) >= 100) out.add("bld_skull_intro");
  if (nCurses >= 3 && ctx.isBossClear) out.add("bld_black_grad");
  if ((ctx.clearSpinSkullCount || 0) >= 5) out.add("bld_ossuary");
  if (nCurses >= 7 && stage >= 10) out.add("bld_curse_vessel");
  if (dev("dev_overheat") && nCurses >= 3 && stage >= 10) out.add("bld_ominous_overheat");
  return out;
}

// 빌드도감 파생 집계(도감 진행률 표시용) — 완성플래그 맵(counters) 입력. Kotlin themeBuildStats 동형.
export function themeBuildStats(counters = {}) {
  const out = {};
  let total = 0, allBasic = 0, allMaster = 0;
  for (const cat of THEME_BUILD_CATEGORIES) {
    const builds = THEME_BUILDS.filter((b) => b.cat === cat);
    const done = builds.filter((b) => (counters[b.id] || 0) > 0).length;
    out["bldCat_" + cat] = done; total += done;
    if (done >= 1) allBasic++;
    if (builds.length && done === builds.length) allMaster++;
  }
  out.bldTotal = total; out.bldAllBasic = allBasic; out.bldAllMaster = allMaster;
  return out;
}

// ══════════════════════════════════════════════════════════════════════
//  심화모드(deepMode) — 심볼 주머니(pouch) 덱빌딩 엔진 (Phase 1+2)
//  ★일반모드 무회귀: weighted(가중추첨)/evaluate(평가)/quota(요구치)/rollRaw 는 한 줄도 안 건드림.
//  아래는 전부 신규 순수함수(추가만). game.js 가 r.deepMode 일 때만 호출한다(엔진엔 deepMode 분기 없음).
//  주머니 = { [symId]: count }. 추출은 count 비중대로. mods 가중(rareWeightMul/symbolWeightMul/weightAdd)
//  은 추출 단계에서 배제(순수 주머니 확률=격리·이중적용 차단). effect(evaluate)엔 mods 그대로 전달됨.
// ══════════════════════════════════════════════════════════════════════

// 주머니 총량(양수 카운트 합).
export const pouchTotal = (pouch) =>
  Object.values(pouch || {}).reduce((s, n) => s + (n > 0 ? n : 0), 0);

// 압축 패널티 → 요구경험치(quota) 배수(≥1). 총량 높으면 1(패널티 없음).
//  DEEP.COMPRESSION 은 le 오름차순 → total<=le 첫 매치가 가장 큰 배수(예 total=82 → le80 skip, le85 매치 → 1.09).
export function compressionPenalty(total) {
  for (const { le, mul } of DEEP.COMPRESSION) if (total <= le) return mul;
  return 1;
}

// 심볼 id → 희귀도(기본|고급|희귀|전설|저주). 미등록은 "기본".
const pouchRarityOf = (id) => POUCH_RARITY[id] || "기본";

// ── 배치 D §6.0: 심볼 id → 티어(SILVER|GOLD|PRISM|CURSE). pouchRarityOf 경유(드리프트 방지).
//  §1.2 V3P1: 고급→SILVER, 희귀→GOLD(v2 대비 한 단계 하향). 전설→PRISM 유지. data.js TIER_BY_RARITY 단일소스.
//  미등록 심볼(id unknown) → "기본" → SILVER 기본값.
export const symTierOf = (id) => TIER_BY_RARITY[pouchRarityOf(id)] || "SILVER";

// ── §1.1 V3P1: 심볼 카테고리 헬퍼 — data.js POUCH_CAT 단일소스 참조. ──
//  symCatOf: "base" | "special" | "harmful". 미등록=POUCH_RARITY로 저주 판정, 그 외 "special" 기본.
//  isAutoDecayTarget: Phase 3 자동 소멸 대상(base && !harmful). Phase 3 구현 전 헬퍼만 export.
export function symCatOf(id) {
  if (POUCH_CAT[id]) return POUCH_CAT[id];
  // 미등록 심볼: 저주 희귀도면 harmful, 그 외 special 기본값.
  return pouchRarityOf(id) === "저주" ? "harmful" : "special";
}
// isAutoDecayTarget: cat=base && !harmful — skull은 POUCH_CAT에서 harmful로 재정의돼 symCatOf="harmful" → 자동 제외.
export const isAutoDecayTarget = (id) => symCatOf(id) === "base";

// ── §9.0 V3 J1: 잭팟 태그 헬퍼 ── JACKPOT_TAG 단일소스 참조. ──────────────────────────────────
//  jackpotTagOf(id): 심볼 id → 잭팟 태그 문자열("crown"|"seven"|"coin"|"prism"|"curse"|"bell" 등) | null(무태그).
//  ★와일드는 태그 판정에 미기여(호출부에서 와일드 제외 처리). 미등록=null.
export const jackpotTagOf = (id) => JACKPOT_TAG[id] || null;

// 주머니 유효성 검증 → { ok, errors[], total, kinds }. 보상 카드 활성화/경고에 사용.
//  검사: 총량 20~40(★§1.4 V3P1 DECK_MIN/MAX) · 종류 7+ · 왕관≤2 · 와일드≤4 · 특수 티어 상한 · 같은 태그≤60%.
//  opts (선택·하위호환) = { totalMax, totalMin, tagMaxRatio } — 심화 덱확장/압축·단일전공(태그상한 완화)으로
//   상·하한/태그비중 상한을 런에서 오버라이드할 때 전달. 미전달 시 DEEP 기본값(기존 호출부 무영향·격리).
export function pouchValidate(pouch, opts = {}) {
  const errors = [];
  const p = pouch || {};
  const total = pouchTotal(p);
  const tMax = opts.totalMax ?? DEEP.DECK_MAX;
  const tMin = opts.totalMin ?? DEEP.DECK_MIN;
  const tagMax = opts.tagMaxRatio ?? DEEP.TAG_MAX_RATIO;
  const kindEntries = Object.entries(p).filter(([, n]) => n > 0);
  const kinds = kindEntries.length;
  if (total < tMin) errors.push(`총량 ${total} < 최소 ${tMin}`);
  if (total > tMax) errors.push(`총량 ${total} > 최대 ${tMax}`);
  if (kinds < DEEP.MIN_KINDS) errors.push(`심볼 종류 ${kinds} < 최소 ${DEEP.MIN_KINDS}`);
  if ((p.crown || 0) > DEEP.CROWN_MAX) errors.push(`👑왕관 ${p.crown} > 상한 ${DEEP.CROWN_MAX}`);
  if ((p.wild || 0) > DEEP.WILD_MAX) errors.push(`🌀와일드 ${p.wild} > 상한 ${DEEP.WILD_MAX}`);
  // ★§1.5 V3P1: 특수 티어 상한(RARITY_MAX 재구조화). base/harmful은 ∞(상한 없음), special만 티어별 상한.
  //   RARITY_MAX 키 = "<TIER>_special" — cat=special 심볼의 티어별 총개수 합산.
  const byTierSpecial = {};
  for (const [id, n] of kindEntries) {
    if (symCatOf(id) !== "special") continue;   // base/harmful는 건너뜀
    const tk = symTierOf(id) + "_special";
    byTierSpecial[tk] = (byTierSpecial[tk] || 0) + n;
  }
  for (const key in DEEP.RARITY_MAX) {
    const cap = DEEP.RARITY_MAX[key];
    if (cap !== Infinity && (byTierSpecial[key] || 0) > cap) {
      const label = key.replace("_special", "");
      errors.push(`특수 ${label} ${byTierSpecial[key]} > 상한 ${cap}`);
    }
  }
  // ── §9.0 V3 J1: 같은 jackpotTag 특수심볼 덱 상한 JACKPOT_TAG_DECK_MAX(8) ──
  //  ★base 심볼은 제외(coin·crown 등 기본계열은 상한 없음), special 심볼만 카운트.
  //  유해(harmful) 심볼 중 저주 JACKPOT_TAG 매핑(curse 태그)도 특수가 아니라 harmful → 제외됨(의도).
  {
    const byJtag = {};
    for (const [id, n] of kindEntries) {
      if (symCatOf(id) !== "special") continue;   // base/harmful 제외
      const jt = jackpotTagOf(id);
      if (jt) byJtag[jt] = (byJtag[jt] || 0) + n;
    }
    for (const jt in byJtag) {
      if (byJtag[jt] > JACKPOT_TAG_DECK_MAX) {
        errors.push(`잭팟태그 #${jt} 특수심볼 ${byJtag[jt]} > 상한 ${JACKPOT_TAG_DECK_MAX}`);
      }
    }
  }
  // 같은 태그 최대 60%(태그별 개수 / 총량). empty/random 은 태그 없어 무관.
  if (total > 0) {
    const byTag = {};
    for (const [id, n] of kindEntries) for (const t of (SYM_BY_ID[id]?.tags || [])) byTag[t] = (byTag[t] || 0) + n;
    for (const t in byTag) if (byTag[t] / total > tagMax + 1e-9) {
      errors.push(`#${t} ${Math.round(byTag[t] / total * 100)}% > ${Math.round(tagMax * 100)}%`);
    }
  }
  return { ok: errors.length === 0, errors, total, kinds };
}

// id → 셀. "empty"=빈칸, "random"=랜덤칸(빈칸/랜덤칸 제외 실심볼 재추첨), 나머지=SYM_BY_ID.
function pouchDrawOne(id, rng, pouch) {
  if (id === "empty") return cell(EMPTY_SYM);
  if (id === "random") {
    const pool = Object.entries(pouch).filter(([k, n]) => n > 0 && k !== "random" && k !== "empty");
    if (!pool.length) return cell(EMPTY_SYM);
    const t = pool.reduce((s, [, n]) => s + n, 0);
    let r = rng.double() * t;
    for (const [k, n] of pool) { r -= n; if (r <= 0) return cell(SYM_BY_ID[k] || EMPTY_SYM, "🎲칸"); }
    return cell(SYM_BY_ID[pool[0][0]] || EMPTY_SYM, "🎲칸");
  }
  return cell(SYM_BY_ID[id] || EMPTY_SYM);
}

// 주머니 → reel 칸 추출(비중 가중랜덤). rollRaw 와 반환형 동일([{sym,tag}]) → evaluate 100% 재사용.
//  ★mods 미적용(순수 주머니 확률). 총량 0/빈 주머니는 전부 빈칸(방어).
//  bias(선택) = { mul:{symId:factor}, add:{symId:n}, rareMul:factor } — 배치 B: 심화 perk 가중치 편향.
//   유효 가중치: count × (mul[id]??1) + (count>0 ? add[id]??0 : 0). 희귀/전설 심볼에 추가 ×rareMul.
//   음수 클램프 0. count==0 심볼은 절대 주입 불가(부재 주입 금지). 전 심볼 w=0 이면 base 폴백(드로우 불능 방지).
//   bias 생략 시 기존과 비트 동일(순수 count 비중).
export function pouchDraw(rng, pouch, reel = C.REEL, bias) {
  const baseEntries = Object.entries(pouch || {}).filter(([, n]) => n > 0);
  const baseTotal = baseEntries.reduce((s, [, n]) => s + n, 0);
  // bias 없으면 기존 경로(비트 동일).
  let entries = baseEntries, total = baseTotal;
  if (bias && (bias.mul || bias.add || bias.rareMul != null)) {
    const weighted = baseEntries.map(([id, n]) => {
      const mul = (bias.mul && bias.mul[id] != null) ? bias.mul[id] : 1;
      const add = (bias.add && bias.add[id] != null) ? bias.add[id] : 0;
      let w = n * mul + add;   // count>0 보장(baseEntries 필터 통과) → add 적용
      const rar = POUCH_RARITY[id];
      if ((rar === "희귀" || rar === "전설") && bias.rareMul != null) w *= bias.rareMul;
      return [id, Math.max(0, w)];   // 음수 클램프 0
    });
    const wTotal = weighted.reduce((s, [, w]) => s + w, 0);
    if (wTotal > 0) { entries = weighted; total = wTotal; }
    // wTotal==0 → base 폴백(드로우 불능 방지, entries/total 이미 baseEntries/baseTotal)
  }
  const pick1 = () => {
    if (total <= 0) return cell(EMPTY_SYM);
    let r = rng.double() * total;
    for (const [id, w] of entries) { r -= w; if (r <= 0) return pouchDrawOne(id, rng, pouch); }
    return pouchDrawOne(entries[0][0], rng, pouch);
  };
  return Array.from({ length: reel }, pick1);
}

// 심화모드 굴림 진입점(rollRaw 대체). game.js: r.deepMode ? E.rollFromPouch(...) : E.rollRaw(...).
//  배치 B: bias 인자 추가(생략 시 기존 비트 동일). game.js _roll 심화분기에서 mods → bias 변환 후 전달.
export function rollFromPouch(rng, pouch, reel = C.REEL, bias) { return pouchDraw(rng, pouch, reel, bias); }

// 배치 B: bias 반영 유효 가중치 테이블(UI 표시용 확률% 계산·🧪 마커). 순수 계산(rng 미사용).
//  반환: [{ id, count, effWeight, pct }] 총량(effTotal)·변동 심볼 집합(biasChanged: Set<id>).
export function pouchEffWeights(pouch, bias) {
  const base = Object.entries(pouch || {}).filter(([, n]) => n > 0);
  const baseTotal = base.reduce((s, [, n]) => s + n, 0);
  let entries = base.map(([id, n]) => ({ id, count: n, effWeight: n }));
  let effTotal = baseTotal;
  const biasChanged = new Set();
  if (bias && (bias.mul || bias.add || bias.rareMul != null)) {
    entries = base.map(([id, n]) => {
      const mul = (bias.mul && bias.mul[id] != null) ? bias.mul[id] : 1;
      const add = (bias.add && bias.add[id] != null) ? bias.add[id] : 0;
      let w = n * mul + add;
      const rar = POUCH_RARITY[id];
      if ((rar === "희귀" || rar === "전설") && bias.rareMul != null) w *= bias.rareMul;
      w = Math.max(0, w);
      if (Math.abs(w - n) > 1e-9) biasChanged.add(id);
      return { id, count: n, effWeight: w };
    });
    const wTotal = entries.reduce((s, e) => s + e.effWeight, 0);
    if (wTotal > 0) { effTotal = wTotal; }
    else { entries = base.map(([id, n]) => ({ id, count: n, effWeight: n })); effTotal = baseTotal; biasChanged.clear(); }
  }
  const result = entries.map((e) => ({ ...e, pct: effTotal > 0 ? e.effWeight / effTotal : 0 }));
  return { entries: result, effTotal, biasChanged };
}

// ── 보상 연산(순수·불변) ─ 항상 새 pouch 객체 반환(미리보기·롤백 용이). ──
//  reward 형태:
//   { type:"add",     id, n }           → id +n (총량↑)
//   { type:"remove",  id, n }           → id -n (0 하한·0이면 키 삭제, 총량↓)
//   { type:"swap",    from, to, n }     → from -n, to +n (총량 유지)
//   { type:"upgrade", id, n }           → id -n, POUCH_UPGRADE[id] +n (계열 상위, 총량 유지)
export function applySymbolReward(pouch, reward) {
  const p = { ...(pouch || {}) };
  const inc = (id, d) => {
    const v = Math.max(0, (p[id] || 0) + d);
    if (v === 0) delete p[id]; else p[id] = v;
  };
  if (!reward || !reward.type) return p;
  const n = Math.max(0, reward.n | 0);
  switch (reward.type) {
    case "add": inc(reward.id, +n); break;
    case "remove": inc(reward.id, -n); break;
    // 총량 유지 불변식: 실제 제거된 만큼만 대상에 더한다(from 보유<n 이면 min(보유,n)).
    case "swap": { const moved = Math.min(p[reward.from] || 0, n); inc(reward.from, -moved); inc(reward.to, +moved); break; }
    case "upgrade": { const up = POUCH_UPGRADE[reward.id]; if (up) { const moved = Math.min(p[reward.id] || 0, n); inc(reward.id, -moved); inc(up, +moved); } break; }
    default: break;
  }
  return p;
}

// 보상 1건의 전/후 미리보기 데이터(총량·패널티·타깃 카운트 변화). UI 카드 표기용.
//  bounds(선택) = { totalMax, totalMin } — 덱확장/압축 오버라이드된 상·하한을 pouchValidate 에 전달.
export function rewardPreview(pouch, reward, bounds = {}) {
  const before = pouch || {};
  const after = applySymbolReward(before, reward);
  const totalBefore = pouchTotal(before), totalAfter = pouchTotal(after);
  // 변화한 심볼 id 목록(before/after 합집합에서 카운트 다른 것)
  const ids = new Set([...Object.keys(before), ...Object.keys(after)]);
  const changes = [];
  for (const id of ids) {
    const b = before[id] || 0, a = after[id] || 0;
    if (b !== a) changes.push({ id, before: b, after: a });
  }
  return {
    changes, totalBefore, totalAfter,
    penBefore: totalBefore > 0 ? compressionPenalty(totalBefore) : 1,
    penAfter: totalAfter > 0 ? compressionPenalty(totalAfter) : 1,
    valid: pouchValidate(after, bounds),
  };
}

// ══════════════════════════════════════════════════════════════════════
//  배치 H — 패키지 보상(P5·WEBSLOT_DEEP_AUG_SPEC Part2 배치H) — 순수·불변.
//  ★단품 add/remove/swap/upgrade 대신 방향성 ops 묶음. 선택 1번이 빌드 결정이 되도록.
//   applySymbolReward(단품) 재사용 = 각 op 이 기존 불변식(총량유지 swap/upgrade·0하한 remove) 그대로 상속.
//  ops[i] = { type:"add"|"remove"|"swap"|"upgrade", id/from/to, n } (applySymbolReward reward 와 동형).
// ══════════════════════════════════════════════════════════════════════
// 패키지(ops 배열)를 원자적으로 적용 → { ok, pouch, error }. 전체 순차 적용 후 pouchValidate 실패면 거부(부분적용 0).
//  ★원자성: 실패 시 원본 사본(변경 없음)을 반환 — 호출부가 항상 next 를 커밋해도 안전(부분 상태 없음).
//   ops 는 순서대로 누적(예 remove 후 add). 각 op 은 applySymbolReward 로 개별 불변식 준수.
export function applySymbolPackage(pouch, ops, bounds = {}) {
  const before = { ...(pouch || {}) };
  if (!Array.isArray(ops) || !ops.length) return { ok: false, pouch: before, error: "빈 패키지" };
  let cur = before;
  for (const op of ops) cur = applySymbolReward(cur, op);   // 순차 누적(순수 — 매 단계 새 객체)
  const valid = pouchValidate(cur, bounds);
  if (!valid.ok) return { ok: false, pouch: before, error: "주머니 규칙 위반: " + valid.errors.join(" · "), valid };
  return { ok: true, pouch: cur, valid };
}

// 패키지 전/후 미리보기(ops 배열 통합) — rewardPreview 와 동형 반환(카드 UI 재사용).
//  각 op 을 순차 적용해 최종 after 를 구하고, before↔after 카운트 diff·총량·압축패널티·유효성 산출.
//  ★불가능(유효성 실패) 패키지도 valid.ok=false 로 표기해 반환(오퍼 생성 시 사전 필터).
export function packagePreview(pouch, ops, bounds = {}) {
  const before = pouch || {};
  const res = applySymbolPackage(before, ops, bounds);
  const after = res.ok ? res.pouch : applyOpsUnchecked(before, ops);   // 실패해도 diff 표시용 after 계산
  const totalBefore = pouchTotal(before), totalAfter = pouchTotal(after);
  const ids = new Set([...Object.keys(before), ...Object.keys(after)]);
  const changes = [];
  for (const id of ids) {
    const b = before[id] || 0, a = after[id] || 0;
    if (b !== a) changes.push({ id, before: b, after: a });
  }
  return {
    changes, totalBefore, totalAfter,
    penBefore: totalBefore > 0 ? compressionPenalty(totalBefore) : 1,
    penAfter: totalAfter > 0 ? compressionPenalty(totalAfter) : 1,
    valid: res.valid || pouchValidate(after, bounds),
  };
}
// diff 표시 전용(유효성 무관) — ops 순차 적용 결과. applySymbolPackage 는 실패 시 원본을 돌려주므로
//  카드에 "규칙 위반이지만 이렇게 바뀔 예정"을 못 보여준다 → 여기서 검증 없이 순수 계산(제시엔 안 쓰임·표시용).
function applyOpsUnchecked(pouch, ops) {
  let cur = { ...(pouch || {}) };
  for (const op of (ops || [])) cur = applySymbolReward(cur, op);
  return cur;
}

// ── §2 V3P2: 보상 오퍼 v3 — 특수 심볼 카드 2~3장 + skip 카드. [2026-07-10 확정]
// opts:
//  · bounds       = { totalMax, totalMin } — pouchValidate 상·하한 오버라이드.
//  · symUnlocked  = Set/배열 — 해금 필터. 미전달=전체.
//  · extraCards   = [리맵] 오퍼 카드 수 +N (sa_basic_research 연동).
//  · goldBonus    = [리맵] 골드 카드 추가 확률 +10%p씩 (sa_rare_research 연동). 0~1 범위.
//  · legendWeight = PRISM 풀 내 전설 심볼 가중 (sp_legend_collect 기존값 유지).
//  · noCurseAdds  = true 면 저주 특수 혼입 0% (sp_purified_world).
//  · curseChance  = 저주 특수 혼입 확률 오버라이드(테스트용, 기본 0.05).
// ── 배치 D §6.1: 보상 오퍼 v2 dormant — 아래 v2 함수 바디는 코드 보존·비활성(정비소가 기능 커버). 삭제 금지.
// [v2 은퇴 퍽 리맵: addBasicDelta→extraCards / rareChance→goldBonus / packageChance→dormant]

// ── 배치 H — 패키지 템플릿 후보 생성(순수·결정형 rng). 현 주머니 기준 4종 템플릿을 만들고, 각각
//  applySymbolPackage 로 사전 검증(bounds/해금 포함) → ok 인 것만 { type:"package", ops, repId, title, ... } 반환.
//  ★불가능(규칙 위반) 패키지는 여기서 걸러 제시 자체 안 함(스펙 요구). 대표심볼 repId = pity 대상(add op 우선).
//  familyCount = 계열(자신+상위) 합 → 몰빵/승급 등 "계열" 기준 판정.
//   ①몰빵    최다 계열 base +6 · 차순위 계열 base -3
//   ②대청소  해골계열(skull+skull_black) -3 · 빈칸(empty) +2
//   ③승급    하위(base) -3 · 상위(POUCH_UPGRADE) +3 (총량 유지·upgrade op)
//   ④교환    계열 A base -4 · 계열 B base +4
export function packageTemplates(rng, pouch, opts = {}) {
  const p = pouch || {};
  const bounds = opts.bounds || {};
  const unlockedSet = opts.symUnlocked ? new Set(opts.symUnlocked) : null;
  const isUnlocked = (id) => !unlockedSet || unlockedSet.has(id) || id === "empty" || id === "random";
  const noCurse = !!opts.noCurseAdds;
  const addOK = (id) => isUnlocked(id) && (!noCurse || (POUCH_RARITY[id] || "기본") !== "저주");
  // 아키타입 계열(cherry/book/gem/skull/coin/flame) 중 주머니 보유수 내림차순.
  const famList = ARCHETYPES.map((a) => ({ fam: a.family, e: a.e, n: a.n, cnt: familyCount(p, a.family) }))
    .filter((x) => x.cnt > 0).sort((a, b) => b.cnt - a.cnt);
  const out = [];
  const push = (tpl, ops, repId, title, desc) => {
    // 사전 검증 — 불가능(규칙 위반)이면 제시 안 함.
    const res = applySymbolPackage(p, ops, bounds);
    if (!res.ok) return;
    // 실제 변화 있는 op 만(무의미 전량 no-op 배제 — 예 대상 0개)
    const prev = packagePreview(p, ops, bounds);
    if (!prev.changes.length) return;
    out.push({ type: "package", tpl, ops, repId: repId || null, title, desc, preview: prev });
  };
  // ① 몰빵 — 최다 계열 base +6, 차순위 계열 base -3. (add 대상 base 해금 필요.)
  if (famList.length >= 2 && addOK(famList[0].fam)) {
    const top = famList[0], sec = famList[1];
    push("allin", [{ type: "add", id: top.fam, n: 6 }, { type: "remove", id: sec.fam, n: 3 }],
      top.fam, `${top.e} ${top.n} 몰빵`, `${top.n}계열 +6 · ${sec.n}계열 -3`);
  }
  // ② 대청소 — 해골계열(skull+skull_black) -3, 빈칸 +2. (해골 보유 시만·빈칸 해금 항상.)
  if (familyCount(p, "skull") > 0 && addOK("empty")) {
    push("cleanup", [{ type: "remove", id: "skull_black", n: 3 }, { type: "remove", id: "skull", n: 3 }, { type: "add", id: "empty", n: 2 }],
      null, "☠ 대청소", "해골계열 -3 · ▫빈칸 +2");
  }
  // ③ 승급 — 보유한 업그레이드 가능 base 하나: base -3 → 상위 +3(upgrade op, 총량 유지). 상위 심볼 해금 필요.
  {
    const upBases = Object.keys(POUCH_UPGRADE).filter((b) => (p[b] || 0) >= 3 && addOK(POUCH_UPGRADE[b]));
    if (upBases.length) {
      const b = rng.pick(upBases); const up = POUCH_UPGRADE[b];
      const be = SYM_BY_ID[b], ue = SYM_BY_ID[up];
      push("promote", [{ type: "upgrade", id: b, n: 3 }], up,
        `${be ? be.e : ""}${ue ? ue.e : ""} 승급`, `${be ? be.n : b} 3개 → 상위 등급으로`);
    }
  }
  // ④ 교환 — 최다 계열 A base -4, 차순위 계열 B base +4. (B base 해금·저주가드.)
  if (famList.length >= 2 && addOK(famList[1].fam)) {
    const A = famList[0], B = famList[1];
    push("trade", [{ type: "remove", id: A.fam, n: 4 }, { type: "add", id: B.fam, n: 4 }],
      B.fam, `${A.e}→${B.e} 교환`, `${A.n}계열 -4 · ${B.n}계열 +4`);
  }
  return out;
}

export function offerSymbolRewards(rng, pouch, stage = 0, opts = {}) {
  // ── §2 V3P2: 특수 심볼 카드 2~3장 + skip 카드 ──────────────────────
  //  티어 분포(stage = r.stage-1 전달 → stage+1 이 실제 클리어 스테이지):
  //   · (stage+1)%5===0 보스클리어: PRISM 1장 보장 + 나머지 GOLD
  //   · (stage+1)%3===0 3배수클리어: GOLD 1장 보장 + 나머지 SILVER
  //   · 그 외: SILVER 기본(goldBonus 확률로 GOLD 추가)
  const p = pouch || {};
  const bounds = opts.bounds || {};
  const unlockedSet = opts.symUnlocked ? new Set(opts.symUnlocked) : null;
  const isUnlocked = (id) => !unlockedSet || unlockedSet.has(id);
  const noCurse = !!opts.noCurseAdds;
  const legendWgt = Math.max(0, opts.legendWeight | 0);
  const extraCards = Math.max(0, opts.extraCards | 0);          // sa_basic_research 리맵
  const goldBonus = Math.max(0, opts.goldBonus || 0);            // sa_rare_research 리맵 (+10%p씩)
  const curseChance = noCurse ? 0 : (opts.curseChance ?? 0.05); // 저주 특수 혼입 확률
  // §9.0 J1: 태그잭팟 달성 → 다음 POUCH 오퍼 프리즘 후보 1장 보장(보스5배수와 중복시 2장째에 영향 없음).
  const forcePrismFirst = !!opts.forcePrismFirst;

  const realStage = stage + 1;  // 실제 클리어 스테이지
  const isBoss5 = realStage % 5 === 0;
  const is3x   = !isBoss5 && realStage % 3 === 0;
  const baseCards = 2 + extraCards;  // sa_basic_research: +1

  // 특수 심볼 풀 — cat=special & 해금 & 티어별 상한 미달
  //  ★총량은 체크하지 않음 — 교체 비용(POUCH_REMOVE) 플로우가 총량 관리를 담당.
  //   상한만 검사: SILVER_special≤10 / GOLD_special≤6 / PRISM_special≤2 / CROWN≤2 / WILD≤4.
  const specByTier = { SILVER: [], GOLD: [], PRISM: [], CURSE: [] };
  // 현재 특수 티어별 합산 — 상한 비교용
  const curTierSpec = {};
  for (const [id, n] of Object.entries(p)) {
    if ((n || 0) <= 0 || symCatOf(id) !== "special") continue;
    const tk = symTierOf(id) + "_special";
    curTierSpec[tk] = (curTierSpec[tk] || 0) + n;
  }
  for (const id of POUCH_SYMBOLS) {
    if (symCatOf(id) !== "special") continue;
    if (!isUnlocked(id)) continue;
    const tier = symTierOf(id);
    if (tier === "CURSE" && noCurse) continue;
    // RARITY_MAX 상한 검사 (특수 티어별 상한) + 개별 심볼 특수 상한(왕관/와일드)
    const tk = tier + "_special";
    const tierCap = DEEP.RARITY_MAX[tk] ?? Infinity;
    const curTier = (curTierSpec[tk] || 0);
    if (curTier >= tierCap) continue;  // 이미 상한 도달
    if (id === "crown" && (p.crown || 0) >= DEEP.CROWN_MAX) continue;
    if (id === "wild"  && (p.wild  || 0) >= DEEP.WILD_MAX)  continue;
    if (specByTier[tier]) specByTier[tier].push(id);
  }
  // 전설 가중 — PRISM 풀에서 전설 rarity 심볼 반복 삽입
  if (legendWgt > 0) {
    const weighted = [];
    for (const id of specByTier.PRISM) {
      const times = pouchRarityOf(id) === "전설" ? 1 + legendWgt : 1;
      for (let i = 0; i < times; i++) weighted.push(id);
    }
    specByTier.PRISM = weighted;
  }
  // §11 레버③ 초반 밸런스 완화 — S1~2(clearedStage 0~1 · realStage<=2) 오퍼의 SILVER 풀에서
  //  EXP 기여형/기본 보조 심볼(DEEP.EARLY_EXP_BOOST_IDS) 가중 ×1.5. legendWgt 선례(반복 삽입) 재사용 —
  //  정수 근사를 위해 대상:비대상 삽입비 3:2 사용(상대가중치 정확히 1.5배). S3+ 는 무가중(원본 풀 그대로).
  if (realStage <= 2 && specByTier.SILVER.length) {
    const weighted = [];
    for (const id of specByTier.SILVER) {
      const times = DEEP.EARLY_EXP_BOOST_IDS.includes(id) ? 3 : 2;
      for (let i = 0; i < times; i++) weighted.push(id);
    }
    specByTier.SILVER = weighted;
  }

  // 티어 시퀀스 결정 (baseCards 만큼)
  const tierSeq = [];
  if (isBoss5) {
    // 보스후: PRISM 1장 보장, 나머지 GOLD
    tierSeq.push("PRISM");
    for (let i = 1; i < baseCards; i++) tierSeq.push("GOLD");
  } else if (forcePrismFirst && !isBoss5) {
    // §9.0 J1: 태그잭팟 달성 → PRISM 1장 보장(첫 번째 슬롯), 나머지는 기존 분배.
    //  PRISM 풀 소진 시 GOLD 폴백(티어 선택 로직 내 폴백이 처리). 보스5배수와 중복 불가(위 분기가 우선).
    tierSeq.push("PRISM");
    if (is3x) {
      for (let i = 1; i < baseCards; i++) tierSeq.push("SILVER");
    } else {
      for (let i = 1; i < baseCards; i++) {
        const isGold = rng.double() < (0.0 + goldBonus);
        tierSeq.push(isGold ? "GOLD" : "SILVER");
      }
    }
  } else if (is3x) {
    // 3배수: GOLD 1장 보장, 나머지 SILVER
    tierSeq.push("GOLD");
    for (let i = 1; i < baseCards; i++) tierSeq.push("SILVER");
  } else {
    // 일반: SILVER 기본, goldBonus 확률로 GOLD
    for (let i = 0; i < baseCards; i++) {
      const isGold = rng.double() < (0.0 + goldBonus);
      tierSeq.push(isGold ? "GOLD" : "SILVER");
    }
  }

  // 특수 카드 생성 — 각 슬롯에 저주 혼입 확률(curseChance) 적용
  const cards = [];
  const usedIds = new Set();
  for (let i = 0; i < tierSeq.length; i++) {
    let tier = tierSeq[i];
    // 저주 혼입: curseChance 확률 + CURSE 풀 있을 때
    if (curseChance > 0 && specByTier.CURSE.length > 0 && rng.double() < curseChance) tier = "CURSE";
    // 풀에서 중복 없이 선택 (상위→하위 폴백)
    const tierOrder = tier === "PRISM" ? ["PRISM","GOLD","SILVER"] :
                      tier === "GOLD"  ? ["GOLD","SILVER","PRISM"] :
                      tier === "CURSE" ? ["CURSE"] :
                                         ["SILVER","GOLD"];
    let chosen = null; let chosenTier = tier;
    for (const t of tierOrder) {
      const pool = (specByTier[t] || []).filter(id => !usedIds.has(id));
      if (pool.length) { chosen = rng.pick(pool); chosenTier = t; break; }
    }
    if (!chosen) continue;  // 풀 소진 시 카드 수 감소(방어)
    usedIds.add(chosen);
    const sym = SYM_BY_ID[chosen] || {};
    // cost 필드: 교체 비용 규칙(§2)
    let cost;
    if (chosenTier === "CURSE") {
      cost = { free: true };    // 저주: 무료 +1 (DECK_MAX 검사만)
    } else if (chosenTier === "PRISM") {
      cost = { removeN: 2, orCurse: true };  // 프리즘: 기본2개 or 저주+1
    } else if (chosenTier === "GOLD") {
      cost = { removeN: 2, lowRemoveN: 1 };  // 골드: 기본2개(이득<3이면 1개)
    } else {
      cost = { removeN: 1 };    // 실버: 기본1개
    }
    cards.push({ type: "special", id: chosen, tier: chosenTier,
      e: sym.e || "❔", n: sym.n || chosen, cost });
  }

  // skip 카드: [선택하지 않기: 코인 +5]
  const skipCard = { type: "skip", tier: "SILVER", coinBonus: 5, e: "⏭", n: "선택하지 않기" };

  return [...cards, skipCard];
}

// ── 배치 D §6.1 v2 offerSymbolRewards dormant 바디 (2026-07-10 은퇴, 삭제 금지)
// v2 기능은 정비소 서비스(sv_add_basic/sv_upgrade/sv_remove 등)가 커버.
// addBasicDelta(sa_basic_research), rareChance(sa_rare_research), packageChance 리맵은 dormant.
function _offerSymbolRewards_v2_DORMANT(rng, pouch, stage = 0, n = DEEP.REWARD_OPTIONS, opts = {}) {
  const p = pouch || {};
  const held = Object.entries(p).filter(([, c]) => c > 0).map(([id]) => id);
  const heldSet = new Set(held);
  const bounds = opts.bounds || {};
  const unlockedSet = opts.symUnlocked ? new Set(opts.symUnlocked) : null;
  const isUnlocked = (id) => !unlockedSet || unlockedSet.has(id) || id === "empty" || id === "random";
  const noCurse = !!opts.noCurseAdds;
  const isAddOk = (id) => isUnlocked(id) && (!noCurse || pouchRarityOf(id) !== "저주");
  const isDesigOk = (id) => {
    const rar = pouchRarityOf(id);
    return isAddOk(id) && rar !== "기본" && rar !== "저주";
  };
  const upgrChance = DEEP.DESIGNATED_UPGRADE_CHANCE ?? 0.10;
  let desTierTarget = ((stage + 1) % 5 === 0) ? "PRISM" : "GOLD";
  if (desTierTarget === "GOLD" && rng.double() < upgrChance) desTierTarget = "PRISM";
  const legendWgt = Math.max(0, opts.legendWeight | 0);
  const desPool = POUCH_SYMBOLS.filter((id) => isDesigOk(id) && symTierOf(id) === desTierTarget);
  const desValid = [];
  for (const id of desPool) {
    const c = { type: "add", id, n: 2 };
    const pv = rewardPreview(p, c, bounds);
    if (pv.valid.ok && pv.changes.length) desValid.push(id);
  }
  const desWeighted = [];
  for (const id of desValid) {
    const times = (symTierOf(id) === "PRISM" && pouchRarityOf(id) === "전설") ? 1 + legendWgt : 1;
    for (let i = 0; i < times; i++) desWeighted.push(id);
  }
  let slot1 = null;
  if (desWeighted.length) {
    const pickedId = rng.pick(desWeighted);
    slot1 = { type: "add", id: pickedId, n: 2, tier: desTierTarget,
      preview: rewardPreview(p, { type: "add", id: pickedId, n: 2 }, bounds) };
  }
  const packCount = (DEEP.RANDPACK_COUNT ?? 2) + Math.max(0, opts.addBasicDelta | 0);
  const basePrismChance = (DEEP.RANDPACK_DIST || [0.30, 0.50, 0.20])[2];
  const prismPct = Math.min(1, basePrismChance + (opts.rareChance || 0));
  const dist = DEEP.RANDPACK_DIST || [0.30, 0.50, 0.20];
  const silverBase = dist[0], goldBase = dist[1];
  const prismIncrease = prismPct - dist[2];
  const silverAdj = Math.max(0, silverBase - prismIncrease);
  const goldAdj = Math.max(0, goldBase - Math.max(0, prismIncrease - silverBase));
  const randpackDist = [silverAdj, goldAdj, prismPct];
  const tMax = bounds.totalMax ?? DEEP.DECK_MAX;
  const totalNow = pouchTotal(p);
  const slot2 = (totalNow + 1 <= tMax) ? {
    type: "randpack", tier: "GOLD", n: packCount, dist: randpackDist,
    d: `🎲 랜덤 ${packCount}개 · 골드 이상 1개 보장`,
    preview: {
      totalBefore: totalNow, totalAfter: totalNow + packCount,
      penBefore: totalNow > 0 ? compressionPenalty(totalNow) : 1,
      penAfter: (totalNow + packCount) > 0 ? compressionPenalty(totalNow + packCount) : 1,
    },
  } : null;
  const addPool = POUCH_SYMBOLS.filter((id) => isAddOk(id));
  const remN = 2; const swapN = 3; const upN = 2;
  const utilCands = [];
  for (const id of held) utilCands.push({ type: "remove", id, n: remN, tier: "SILVER" });
  for (const from of held) {
    const targets = rng.shuffle(addPool.filter((t) => t !== from)).slice(0, 2);
    for (const to of targets) utilCands.push({ type: "swap", from, to, n: swapN, tier: "SILVER" });
  }
  for (const id of Object.keys(POUCH_UPGRADE)) {
    if (heldSet.has(id)) utilCands.push({ type: "upgrade", id, n: upN, tier: "SILVER" });
  }
  const utilValid = [];
  for (const c of utilCands) {
    const pv = rewardPreview(p, c, bounds);
    if (pv.valid.ok && pv.changes.length) utilValid.push({ ...c, preview: pv });
  }
  const shuffledUtil = rng.shuffle(utilValid);
  let slot3 = shuffledUtil.length ? shuffledUtil[0] : null;
  if (slot3 !== null || shuffledUtil.length === 0) {
    const pkgChance = opts.packageChance ?? DEEP.PACKAGE_CHANCE ?? 0.30;
    if (rng.double() < pkgChance) {
      const templates = packageTemplates(rng, p, opts);
      if (templates.length) { const pkg = { ...rng.pick(templates), tier: "PRISM" }; slot3 = pkg; }
    }
  }
  return [slot1, slot2, slot3].filter(Boolean);
}

// 기본 주머니 시딩(startRun 용) — DEEP.START_POUCH 사본.
export const startPouch = () => ({ ...DEEP.START_POUCH });

// 주머니에서 태그별 총 개수 { [tag]: count }. pouchValidate 내부 계산과 동일 공식(중복 없이 재사용).
export function pouchTagCounts(pouch) {
  const byTag = {};
  for (const [id, n] of Object.entries(pouch || {})) {
    if (n <= 0) continue;
    for (const t of (SYM_BY_ID[id]?.tags || [])) byTag[t] = (byTag[t] || 0) + n;
  }
  return byTag;
}
// 주머니에서 가장 많은 태그(동률=사전순 첫). 없으면 null.
export function mostCommonTag(pouch) {
  const byTag = pouchTagCounts(pouch);
  let best = null, bn = -1;
  for (const t of Object.keys(byTag).sort()) if (byTag[t] > bn) { bn = byTag[t]; best = t; }
  return best;
}

// ══════════════════════════════════════════════════════════════════════
//  심화 관련성 필터 (WEBSLOT_DEEP_AUG_SPEC Part1) — 일반 증강/유물의 심화 노출 게이트 (순수·additive).
//  ★일반모드 무영향: game.js 가 r.deepMode 경로(SYMAUG/SYMREL 혼합·심화 SHOP 진열·EVENT 지급 등)에서만
//   호출. data.js 의 deep/dSym 태깅이 단일 소스(무태깅 = 등장률·강제등장 등 심화 no-op 퍽 → 무조건 제외).
// ══════════════════════════════════════════════════════════════════════
// 심볼 계열(주머니): base → [base, 상위]. POUCH_UPGRADE 파생(cherry:[cherry,cherry_ripe] 등 6계열).
//  ※기존 PERK_FAMILY(퍽 패밀리·오퍼 랭크 게이팅)와 전혀 다른 축 — 명명 충돌 금지(POUCH_FAMILY).
export const POUCH_FAMILY = Object.fromEntries(
  Object.entries(POUCH_UPGRADE).map(([b, u]) => [b, [b, u]])
);
// 역매핑: 상위 심볼 → base (cherry_ripe→cherry, tome→book, gem_cut→gem, coin_bag→coin,
//  skull_black→skull, ember→flame). evaluate 계열 브릿지(deepFamilyBridge)가 사용.
export const UPG_PARENT = Object.fromEntries(Object.entries(POUCH_UPGRADE).map(([b, u]) => [u, b]));

// 주머니에서 참조 계열 보유수 — ref="tag:X" 면 태그 총수(pouchTagCounts, 예 "tag:학습"=book+tome 동치),
//  아니면 계열(자신+상위) 합. 계열합산 선례 = game.js _markDeepBossAchievements.
export function familyCount(pouch, ref) {
  if (typeof ref === "string" && ref.startsWith("tag:")) return pouchTagCounts(pouch)[ref.slice(4)] || 0;
  return (POUCH_FAMILY[ref] || [ref]).reduce((s, id) => s + ((pouch || {})[id] || 0), 0);
}
// 퍽 p 가 현 주머니 빌드에 "관련" 있는가 — deep 태깅 없으면 무조건 false(심화 유입 차단).
//  dSym 퍽은 계열 보유수 ≥ DEEP.REL_MIN(심볼별 오버라이드: 왕관 2 = CROWN_MAX 정합) 일 때만 true.
export function isDeepCompat(p, pouch) {
  if (!p || !p.deep) return false;
  if (!p.dSym) return true;
  const min = (DEEP.REL_MIN_BY_SYM || {})[p.dSym] ?? DEEP.REL_MIN;
  return familyCount(pouch, p.dSym) >= min;
}
// 풀에서 심화 호환 퍽만 필터(순수). 입력 pool 은 호출부 책임(레벨해금은 game._augPool/_relicPool 경유).
export function deepCompatPool(pool, pouch) {
  return (pool || []).filter((p) => isDeepCompat(p, pouch));
}

// ══════════════════════════════════════════════════════════════════════
//  배치 G — 계열 아키타입(전공) 판정 (WEBSLOT_DEEP_AUG_SPEC Part2 배치G) — 순수함수.
//  주머니 계열 비중(share = familyCount/pouchTotal)이 임계(ARCH_T1/T2) 도달 시 자동 발동 보너스.
//  ★share 기반이라 배치 I 총량 변경(100→60 등)에 내성. 최대 share 계열 1개만 활성(단일 전공).
//  6계열: cherry🍒과수원 · book📘도서관장(학습태그) · gem💎보석상 · skull☠강령학파 · coin🪙조폐국 · flame🔥화력발전.
//   crown/wild 는 상한(2/4) 특례라 아키타입 제외. 각 ref = familyCount 조회키(book 은 tag:학습=book+tome 동치).
export const ARCHETYPES = [
  { family: "cherry", ref: "cherry",   e: "🍒", n: "과수원",   metric: "exp" },
  { family: "book",   ref: "tag:학습", e: "📘", n: "도서관장", metric: "exp" },
  { family: "gem",    ref: "gem",      e: "💎", n: "보석상",   metric: "score" },
  { family: "skull",  ref: "skull",    e: "☠", n: "강령학파", metric: "exp" },
  { family: "coin",   ref: "coin",     e: "🪙", n: "조폐국",   metric: "coin" },
  { family: "flame",  ref: "flame",    e: "🔥", n: "화력발전", metric: "exp" },
];
export const ARCH_BY_FAMILY = Object.fromEntries(ARCHETYPES.map((a) => [a.family, a]));
// 최대 비중 아키타입 계열 1개 판정. 반환 { family, share, tier, e, n, metric }.
//  tier: 2(share≥ARCH_T2) / 1(≥ARCH_T1) / 0(미달=비활성, family 는 최근접 표시용으로 유지).
//  ★동률(share)은 ARCHETYPES 정의 순서 우선(cherry>book>gem>skull>coin>flame) — 결정론.
//  빈/총량0 주머니는 family=null(HUD 미표시).
export function pouchArchetype(pouch) {
  const total = pouchTotal(pouch);
  if (total <= 0) return { family: null, share: 0, tier: 0, e: "", n: "", metric: "" };
  let best = null, bestShare = -1;
  for (const a of ARCHETYPES) {
    const share = familyCount(pouch, a.ref) / total;
    if (share > bestShare) { bestShare = share; best = a; }
  }
  const share = Math.max(0, bestShare);
  const tier = share >= DEEP.ARCH_T2 ? 2 : share >= DEEP.ARCH_T1 ? 1 : 0;
  return { family: best.family, share, tier, e: best.e, n: best.n, metric: best.metric };
}
// 퍽 dSym(참조 계열)이 아키타입 family 와 같은 계열인가 — 오퍼 시너지 정렬 판별(순수·표시용).
//  dSym 형태: "cherry"|"skull"|...(base) · "tag:학습"(도서관장==book 계열) · 상위심볼 id(cherry_ripe 등).
//  arch.family 는 base id(cherry/book/gem/skull/coin/flame). book 은 dSym "book"/"tome"/"tag:학습" 모두 매칭.
export function familyRefMatchesArch(dSym, archFamily) {
  if (!dSym || !archFamily) return false;
  if (dSym === archFamily) return true;
  if (archFamily === "book" && dSym === "tag:학습") return true;
  if (typeof dSym === "string" && dSym.startsWith("tag:")) return false;   // 그 외 태그참조는 계열 무관
  return (UPG_PARENT[dSym] || dSym) === archFamily;   // 상위심볼 id → base 로 환산 후 비교
}

// 아키타입 → mods 곱셈 증가분 산출(순수). game.js _mods 심화블록이 반환값을 deepFamily*Mul 에 주입.
//  반환 { expMul, scoreMul, coinMul } = { [family]: pctInc }(활성 1계열만·비활성=빈맵) + skullPenaltyMul.
//  수치(스펙 배치G 그대로): exp/score 계열 +15/+30%, coin 계열 +10/+20%, 강령학파 t2 는 skullPenaltyMul 0.5 병행.
export function archetypeMods(arch) {
  const out = { expMul: {}, scoreMul: {}, coinMul: {}, skullPenaltyMul: 1 };
  if (!arch || !arch.family || arch.tier <= 0) return out;
  const fam = arch.family, t2 = arch.tier >= 2;
  if (arch.metric === "coin") out.coinMul[fam] = t2 ? 0.20 : 0.10;
  else if (arch.metric === "score") out.scoreMul[fam] = t2 ? 0.30 : 0.15;
  else out.expMul[fam] = t2 ? 0.30 : 0.15;   // exp 계열(cherry/book/skull/flame)
  if (fam === "skull" && t2) out.skullPenaltyMul = 0.5;   // 강령학파 t2 — 해골 감점 완화 병행
  return out;
}

// ══════════════════════════════════════════════════════════════════════
//  Phase 5 — 심화 심볼증강/유물 효과 집계 (순수·격리).
//  ★일반모드 무영향: buildMods 를 건드리지 않고, 심볼퍽(SYM_PERK_BY_ID) id 만 골라 효과를 모은다.
//   game.js _mods() 심화블록이 r.deepMode 일 때만 호출·주입 → 일반 경로엔 이 함수가 아예 안 불림.
//  입력: perkIds(r.perks 전체·심볼퍽만 필터), pouch(태그/총량 조건 판정용).
//  반환(전부 additive·기본 무효):
//   deepTagMul   = { [tag]: pctSum }  (evaluate 셀 태그 곱셈버프에 합류)
//   emptyScore/emptyExp = 빈칸 활용
//   penaltyMul   = 압축패널티 완화 배수(≤1 이득) — 총량 조건 만족 시만. [HIGH-3] game.js _deepPenalty 가
//                  초과분(base-1)에만 적용(1+(base-1)×mul, 하한 1.0) — 요구치 통할인 금지.
//   skullPenaltyMul = ☠해골 감점 배수(확장빌드/확장가방) — mods.skullPenaltyMul 로 배선(quota 경로와 분리)
//   boundMaxDelta/boundMinDelta = 총량 상/하한 델타 · boundMax/boundMin = 절대 오버라이드(우선)
//   quotaMul/bossQuotaMul = 요구경험치 곱(프리즘 트레이드오프)
//   scoreMul     = 전역 점수 곱(정화된세계 -10% 등)
//   compressScorePct = 총량 조건 만족 시 점수 +pct (압축계약서)
//   repairMul    = { [kind|"*"]: mul }  정비 가격 배수 · repairRefundFrac = 교체 환급률
//   swapCoin/purifyCoin = 교체/정화 시 코인 보상 · purifyToBasic = 정화 결과 랜덤 기본심볼
//   rewardBonus  = 주머니 보상 후보 +N · addBasicDelta/rareChance/legendWeight = 보상 추가량 연동
//   curseChance  = 희귀사냥 저주 동반 확률(근사·>0), -1=정화된세계 저주↓ 표식
//   shopLabWeight = 정비소/상점 노드 가중 · alwaysRepair = 매 스테이지 정비 노드
//   bossCopyN/autoShredN = 보스 최다태그 심볼 +N / 스테이지 최저가치 심볼 -N
//   rareFirstScore/legendSeal/balance/statTable = 근사/표시형 신호(game.js·UI 소비)
// ══════════════════════════════════════════════════════════════════════
// 심볼증강 레벨(2/3) 델타를 eff 사본에 반영 — 형광펜/복습책 레벨업 부활. levels[id] 미지정=Lv1(원본).
//  mulSub = penaltyMul 감소분(더 이득). pct/other/score/exp/basic/rareChance/legendWeight/n = 가산.
function effWithLevel(perk, lvl) {
  const base = perk.eff;
  if (!base || !isSymAugLevelable(perk.id) || (lvl || 1) < 2) return base;
  const e = { ...base };
  const def = SYM_AUG_LEVELS[perk.id];
  for (let L = 2; L <= Math.min(3, lvl); L++) {
    const d = def[L]; if (!d) continue;
    for (const k in d) {
      if (k === "mulSub") { e.mul = Math.max(0, (e.mul ?? 1) - d.mulSub); }
      else e[k] = (e[k] || 0) + d[k];
    }
  }
  return e;
}

export function symPerkMods(perkIds = [], pouch = {}, levels = {}) {
  const out = {
    deepTagMul: {}, emptyScore: 0, emptyExp: 0, penaltyMul: 1, skullPenaltyMul: 1,
    boundMaxDelta: 0, boundMinDelta: 0, boundMax: 0, boundMin: 0,
    quotaMul: 1, bossQuotaMul: 1, scoreMul: 1, compressScorePct: 0,
    repairMul: {}, repairRefundFrac: 0, swapCoin: 0, purifyCoin: 0, purifyToBasic: false,
    rewardBonus: 0, addBasicDelta: 0, rareChance: 0, legendWeight: 0, curseChance: 0,
    shopLabWeight: 0, alwaysRepair: false, bossCopyN: 0, autoShredN: 0,
    rareFirstScore: 0, legendSeal: false, balanceScore: 0, statTable: false, tagCap: 0,
    scoreKinds: [],
  };
  const total = pouchTotal(pouch);
  const most = mostCommonTag(pouch);
  const addTag = (tag, pct) => { if (!tag) return; out.deepTagMul[tag] = (out.deepTagMul[tag] || 0) + pct; };
  for (const id of perkIds) {
    const perk = SYM_PERK_BY_ID[id]; if (!perk || !perk.eff) continue;
    const e = effWithLevel(perk, (levels && levels[id]) || 1);
    switch (e.hook) {
      case "tagBuff": {
        // MOST/MAJOR/SOLO 모두 "가장 많은 태그"에 +pct. MAJOR/SOLO 는 타 태그 -other.
        addTag(most, e.pct || 0);
        if (e.other) for (const t of Object.keys(pouchTagCounts(pouch))) if (t !== most) addTag(t, e.other);
        if (e.tagCap) out.tagCap = Math.max(out.tagCap, e.tagCap);   // 단일전공: 태그 상한 완화
        break;
      }
      case "emptyScore": out.emptyScore += (e.score || 0); break;
      case "emptyExp": out.emptyExp += (e.exp || 0); break;
      case "penaltyMul": {
        const okLe = e.whenTotalLe == null || total <= e.whenTotalLe;
        const okGe = e.whenTotalGe == null || total >= e.whenTotalGe;
        if (okLe && okGe) out.penaltyMul *= (e.mul || 1);
        break;
      }
      // [HIGH-3②] 확장계열(확장빌드) — 설명대로 ☠해골 감점 완화. quota(penaltyMul) 경로와 분리.
      case "skullPenaltyMul": {
        const okLe2 = e.whenTotalLe == null || total <= e.whenTotalLe;
        const okGe2 = e.whenTotalGe == null || total >= e.whenTotalGe;
        if (okLe2 && okGe2) out.skullPenaltyMul *= (e.mul || 1);
        break;
      }
      case "boundDelta":
        out.boundMaxDelta += (e.maxDelta || 0); out.boundMinDelta += (e.minDelta || 0);
        // [HIGH-3②] 확장가방 — 해골 완화분은 skullPenaltyMul 로(quota 통할인 오배선 제거).
        if (e.skullPenaltyMul && (e.whenTotalGe == null || total >= e.whenTotalGe)) out.skullPenaltyMul *= e.skullPenaltyMul;
        break;
      case "boundOverride":
        if (e.max) out.boundMax = Math.max(out.boundMax, e.max);
        if (e.min) out.boundMin = out.boundMin ? Math.min(out.boundMin, e.min) : e.min;
        if (e.extraPenaltyLe != null && total <= e.extraPenaltyLe && e.extraMul) out.penaltyMul *= e.extraMul;
        if (e.quotaMul) out.quotaMul *= e.quotaMul;   // (미사용 예약)
        break;
      case "compressScore":
        if (e.whenTotalLe == null || total <= e.whenTotalLe) out.compressScorePct += (e.pct || 0);
        break;
      case "repairMul": {
        for (const k of (e.kinds || [])) out.repairMul[k] = (out.repairMul[k] || 1) * (e.mul || 1);
        break;
      }
      case "repairRefund": out.repairRefundFrac = Math.max(out.repairRefundFrac, e.frac || 0); break;
      case "swapCoin": out.swapCoin += (e.coin || 0); break;
      case "purifyCoin": out.purifyCoin += (e.coin || 0); break;
      case "purifyToBasic": out.purifyToBasic = true; if (e.scoreMul) out.scoreMul *= e.scoreMul; if (e.curseChance) out.curseChance += e.curseChance; break;
      case "rewardBonus": out.rewardBonus += (e.n || 0); break;
      case "addBoost":
        out.addBasicDelta += (e.basic || 0);
        if (e.rareChance) out.rareChance = Math.max(out.rareChance, e.rareChance);
        out.legendWeight += (e.legendWeight || 0);
        if (e.curseChance) out.curseChance += e.curseChance;
        if (e.bossQuotaMul) out.bossQuotaMul *= e.bossQuotaMul;
        break;
      case "shopLab": out.shopLabWeight += (e.weight || 0); if (e.alwaysRepair) out.alwaysRepair = true; if (e.quotaMul) out.quotaMul *= e.quotaMul; break;
      case "bossCopy": out.bossCopyN += (e.n || 0); break;
      case "autoShred": out.autoShredN += (e.n || 0); break;
      case "rareFirstScore": out.rareFirstScore += (e.score || 0); break;
      case "legendSeal": out.legendSeal = true; break;
      case "score":
        if (e.kind === "balance") { out.balanceScore += (e.amount || 0); out.scoreKinds.push("balance"); }
        break;
      case "display": if (e.statTable) out.statTable = true; break;
      default: break;
    }
  }
  return out;
}

// 심볼퍽 오퍼 풀 헬퍼 — 해금 필터(symUnlocked) 적용. game.js selectNode 가 사용.
//  ★퍽은 심볼이 아님: 심볼 해금은 reqSym(연계 심볼 id) 선언 퍽만 게이팅(현재 전 퍽 미선언=전체 노출).
//   (버그수정) 이전 구현이 퍽 id(sa_*/sr_*)를 심볼 해금집합과 비교 → 실게임 풀이 항상 0
//   = SYMAUG/SYMREL 오퍼 전멸이던 문제. reqSym 없는 퍽은 항상 후보.
export function symAugPool(symUnlocked) {
  if (!symUnlocked) return SYM_AUGMENTS;
  const s = new Set(symUnlocked);
  return SYM_AUGMENTS.filter((a) => !a.reqSym || s.has(a.reqSym));
}
export function symRelPool(symUnlocked) {
  if (!symUnlocked) return SYM_RELICS;
  const s = new Set(symUnlocked);
  return SYM_RELICS.filter((a) => !a.reqSym || s.has(a.reqSym));
}

// ══════════════════════════════════════════════════════════════════════
//  Phase 3 — 심화모드 상점 '심볼 정비' 서비스 적용(순수·불변).
//  ★일반경로(evaluate/quota/rollRaw/일반상점) 무수정. game.js 가 r.deepMode 상점에서만 호출.
//  service = DEEP.REPAIR_SERVICES 항목. sel = 사용자 선택값(대상 심볼/태그 등).
//  state = 현재 정비 상태 { pouch, totalMaxDelta, totalMinDelta, compressExtra, tagBuff }.
//  반환 = { ok, error, next, changes, preview } — ok=false 면 거부(코인 미차감·상태 변경 없음).
//   next = 적용 후 상태(동일 shape). preview = { totalBefore, totalAfter, valid } (심볼 정비만·나머지 null).
// ══════════════════════════════════════════════════════════════════════

// service.kind → applySymbolReward reward 로 변환(대상 선택 sel 반영). 심볼 카운트 변경형만.
//  addBasic/addHigh/addRare → add, remove → remove, swap → swap, upgrade → upgrade, purify → skull→empty swap.
//  반환 null = 이 서비스는 심볼 카운트형이 아님(expand/compress/tagbuff).
function serviceToReward(service, sel = {}) {
  const n = service.n2 | 0;
  switch (service.kind) {
    case "addBasic": case "addHigh": case "addRare":
      return sel.id ? { type: "add", id: sel.id, n } : null;
    case "remove":  return sel.id ? { type: "remove", id: sel.id, n } : null;
    case "swap":    return (sel.from && sel.to && sel.from !== sel.to) ? { type: "swap", from: sel.from, to: sel.to, n } : null;
    case "upgrade": return sel.id ? { type: "upgrade", id: sel.id, n } : null;
    // 정화 = 해골(skull)→빈칸(empty) swap N (총량 유지·실제 해골<N 이면 있는 만큼만).
    case "purify":  return { type: "swap", from: "skull", to: "empty", n };
    default: return null;
  }
}

// 정비 상태 정규화(필드 기본값 보강) — 방어.
function normRepairState(state = {}) {
  return {
    pouch: state.pouch || {},
    totalMaxDelta: state.totalMaxDelta | 0,
    totalMinDelta: state.totalMinDelta | 0,
    compressExtra: state.compressExtra || 0,
    tagBuff: state.tagBuff || {},
    tagMaxRatio: state.tagMaxRatio ?? DEEP.TAG_MAX_RATIO,   // Phase 5 단일전공(sp_solo_major) 태그상한 완화(90%)
    curses: state.curses || [],                              // 배치 A Step 5: curseCleanse 용
  };
}

// 현재 정비 상태 기준 유효 총량 상·하한(+ 태그비중 상한). pouchValidate opts 로 그대로 전달 가능.
export function repairBounds(state = {}) {
  const s = normRepairState(state);
  return {
    totalMax: DEEP.DECK_MAX + s.totalMaxDelta,
    totalMin: Math.max(1, DEEP.DECK_MIN + s.totalMinDelta),
    tagMaxRatio: s.tagMaxRatio,
  };
}

// 정비 서비스 적용(순수). game.js 가 코인 확인 후 호출 → ok 면 상태 커밋+코인 차감.
export function applyShopService(service, state, sel = {}) {
  if (!service) return { ok: false, error: "알 수 없는 서비스", next: state };
  const s = normRepairState(state);
  const bounds = repairBounds(s);   // 현 상한/하한(확장/압축 누적 반영)

  // ── ① 심볼 카운트형(추가/제거/교체/업글/정화): applySymbolReward 재사용 + pouchValidate(현 bounds) ──
  const reward = serviceToReward(service, sel);
  if (reward) {
    const next = applySymbolReward(s.pouch, reward);
    // 실제 변화 없으면 거부(예: remove 대상 0개, swap from 0개, upgrade 대상 0개, 정화 시 해골 0개)
    const same = JSON.stringify(next) === JSON.stringify(s.pouch);
    if (same) return { ok: false, error: "변화가 없어요 (대상 심볼이 없거나 부족)", next: state };
    const valid = pouchValidate(next, bounds);
    if (!valid.ok) return { ok: false, error: "주머니 규칙 위반: " + valid.errors.join(" · "), next: state, valid };
    return {
      ok: true, next: { ...s, pouch: next },
      changes: rewardPreview(s.pouch, reward).changes,
      preview: { totalBefore: pouchTotal(s.pouch), totalAfter: pouchTotal(next), valid },
    };
  }

  // ── ② 덱 확장: 총량 상한 +N (주머니 불변). 항상 허용(상한만 늘어남). ──
  if (service.kind === "expand") {
    const next = { ...s, totalMaxDelta: s.totalMaxDelta + (service.n2 | 0) };
    return { ok: true, next, changes: [], preview: null };
  }

  // ── ③ 덱 압축: 총량 최소 -N + 요구경험치 +pct. 하한이 현 총량보다 낮아지도록만(무의미 방지 X — 항상 허용). ──
  //   ★단, 최소치가 1 미만으로는 안 내려감(repairBounds 에서 하한 클램프). 주머니 자체는 불변.
  if (service.kind === "compress") {
    const next = {
      ...s,
      totalMinDelta: s.totalMinDelta - (service.n2 | 0),
      compressExtra: s.compressExtra + (service.pct || 0),
    };
    return { ok: true, next, changes: [], preview: null };
  }

  // ── ④ 태그 강화: 특정 태그 +pct(곱셈 버프 누적). sel.tag 필수. ──
  if (service.kind === "tagbuff") {
    if (!sel.tag) return { ok: false, error: "강화할 태그를 선택하세요", next: state };
    const tagBuff = { ...s.tagBuff, [sel.tag]: (s.tagBuff[sel.tag] || 0) + (service.pct || 0) };
    return { ok: true, next: { ...s, tagBuff }, changes: [], preview: null };
  }

  // ── ⑤ 저주 정화(배치 A Step 5): 저주 1개 제거 — 가장 최근 획득(배열 마지막). 저주 없으면 거부. ──
  //   curses 는 순수 함수에서는 state 필드로 전달(game.js _repairState 에서 curses 포함). 주머니 불변.
  if (service.kind === "curseCleanse") {
    const curses = s.curses || [];
    if (!curses.length) return { ok: false, error: "정화할 저주가 없어요", next: state };
    const removed = curses[curses.length - 1];
    return { ok: true, next: { ...s, curses: curses.slice(0, -1) }, changes: [{ id: removed, delta: -1 }], preview: null };
  }

  return { ok: false, error: "알 수 없는 서비스", next: state };
}
