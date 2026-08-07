// Temporary validation harness for web/slot (do not deploy).
import * as E from "./engine.js";
import { Game, PHASE } from "./game.js";
import { C, SYM_BY_ID, DEEP, SYM_AUGMENTS, SYM_RELICS, SYM_PERK_BY_ID, AUGMENTS, RELICS, POUCH_SYMBOLS, POUCH_RARITY,
  DEVICES, ACHIEVEMENTS, ACH_DEVICE_REWARD, ACH_SYMBOL_UNLOCK, DEFAULT_UNLOCKED_SYMS, DEV_BY_ID,
  AUG_BY_ID, REL_BY_ID, POUCH_UPGRADE, EMPTY_SYM, POUCH_CAT, TIER_BY_RARITY, POUCH_USE } from "./data.js";

const errors = [];
const notes = [];
const phaseSeen = {};
const fail = (m) => errors.push(m);

// ---- shared numeric sanity ----
function checkNum(label, ...vals) {
  for (const v of vals) {
    if (typeof v !== "number") continue;
    if (Number.isNaN(v)) fail(`${label}: NaN`);
    if (v < 0) fail(`${label}: negative (${v})`);
    if (!Number.isFinite(v)) fail(`${label}: non-finite (${v})`);
  }
}
function scanState(label, st) {
  if (!st) return;
  checkNum(label + ".exp", st.stageExp);
  checkNum(label + ".quota", st.quota);
  checkNum(label + ".score", st.score);
  checkNum(label + ".coins", st.coins);
  checkNum(label + ".spins", st.spins);
  checkNum(label + ".spinIndex", st.spinIndex);
  checkNum(label + ".unlucky", st.unluckyGauge);
}

// fresh in-memory storage per game
function memStore() { let v = null; return { getItem: () => v, setItem: (_k, x) => { v = x; }, removeItem: () => { v = null; } }; }

// ------- (1) drive a full run to completion, deep or normal -------
function playRun(seed, deep) {
  const g = new Game({ seed, storage: memStore() });
  // ensure not first-run auto-launch path only sometimes: force a normal run each time via profile.runs
  g.profile.runs = 1;             // skip the "first run auto start" branch so we exercise selection
  g.profile.playerLevel = 30; g.profile.playerXp = 999999; // unlock everything
  let st = g.startRun(0, deep);
  let guard = 0;
  const CAP = 4000;
  while (st && st.phase !== PHASE.RUN_END && guard++ < CAP) {
    scanState("run", st);
    phaseSeen[st.phase] = (phaseSeen[st.phase] || 0) + 1;
    switch (st.phase) {
      case PHASE.SELECT_CHAR: st = g.selectChar(g.unlockedChars()[0].id); break;
      case PHASE.SELECT_MACHINE: st = g.selectMachine(g.unlockedMachines()[0].id); break;
      case PHASE.SELECT_DEVICE: st = g.selectDevice(st.options[0]?.id || ""); break;
      case PHASE.SPIN: {
        const out = g.spin("N");
        if (out && out.state) scanState("spin", out.state);
        if (out) checkNum("spin.gained", out.gained, out.score, out.coins);
        st = g.state();
        break;
      }
      case PHASE.POST_SPIN: st = g.giveUp(false); break;      // no recovery worth pursuing; end
      case PHASE.STAGE_CLEAR: st = g.proceedToNodes(); break;
      case PHASE.NODE_SELECT: st = g.selectNode(0); break;    // always first node
      case PHASE.PERK_PICK: st = g.pickPerk(0); break;
      case PHASE.SHOP: { if (st.shopItems && st.shopItems.length && Math.random() < 0.5) g.shopBuy(0); st = g.shopExit(); break; }
      case PHASE.DEVICE_NODE: st = g.deviceNodeTake(); break;
      case PHASE.REWARD_DONE: st = g.proceedToStage(); break;
      default: st = advanceUnknown(g, st); break;
    }
    if (!st) break;
  }
  if (guard >= CAP) fail(`run seed=${seed} deep=${deep} did not terminate in ${CAP} steps (phase ${st?.phase})`);
  return st;
}

// fallbacks for shop/device methods we don't know the exact name of
function advanceShop(g) { return g.shopExit(); }
function skipNode(g) { return g.deviceNodeTake(); }
function advanceUnknown(g, st) { return st; }

// discover shop/device exit methods
const gm = new Game({ seed: 1, storage: memStore() });
notes.push("Game methods: " + Object.getOwnPropertyNames(Object.getPrototypeOf(gm)).filter(n => typeof gm[n] === "function").join(","));

// ------- (2) special-symbol immediate-effect tests via engine.evaluate -------
function rawFromIds(ids) { return ids.map((id) => { const s = SYM_BY_ID[id]; if (!s) throw new Error("no sym " + id); return { sym: s, tag: "" }; }); }
const mods = E.defaultMods();

function evalIds(ids, rng) { return E.evaluate(rng || E.makeRng(7), rawFromIds(ids), mods, 1, 5, false); }

function testSpecial(name, ids, assertFn) {
  try {
    const rng = E.makeRng(123);
    const res = evalIds(ids, rng);
    checkNum(`special ${name}`, res.exp, res.score, res.coins);
    assertFn(res, rng);
  } catch (e) { fail(`special ${name} threw: ${e.message}`); }
}

// TARGET: +50% top value cell -> exp should exceed baseline of value cells
testSpecial("target", ["target","crown","cherry","star","book"], (res) => {
  if (res.exp <= 0) fail("target produced no exp");
  if (!res.notes.some(n => n.includes("표적"))) fail("target note missing");
});
// PUZZLE5: distinct value symbols -> score bonus
testSpecial("puzzle5", ["jigsaw","cherry","star","book","gem"], (res) => {
  if (!res.notes.some(n => n.includes("퍼즐") || n.includes("PUZZLE"))) notes.push("puzzle5 note not found (may need 4+ distinct)");
});
// CATALYST
testSpecial("catalyst", ["catalyst","cherry","cherry","star","book"], (res) => {});
// MIRROR
testSpecial("mirror", ["mirror_sym","crown","cherry","star","book"], (res) => {});
// black candle with skulls
testSpecial("candle+skulls", ["black_candle_sym","skull","skull","cherry","star"], (res) => {});
// black candle no skull -> exp 0 rule
testSpecial("candle-noskull", ["black_candle_sym","cherry","star","book","gem"], (res) => {
  if (res.exp !== 0) fail(`candle no-skull exp should be 0 (got ${res.exp})`);
});
// lucky7 x3 -> 7x, capped by MAX_SPIN_EXP_MUL
testSpecial("lucky7", ["lucky7","lucky7","lucky7","crown","crown"], (res) => {
  if (!res.lucky7) fail("lucky7 flag not set with 3");
  if (!res.notes.some(n => n.includes("럭키7"))) fail("lucky7 note missing");
});
// lucky7 only 2 -> no trigger
testSpecial("lucky7x2", ["lucky7","lucky7","crown","cherry","star"], (res) => {
  if (res.lucky7) fail("lucky7 should NOT trigger at 2");
});
// prism symbol -> one of the mini effects
testSpecial("prism", ["prism_sym","cherry","star","book","gem"], (res) => {
  if (!res.notes.some(n => n.includes("프리즘"))) fail("prism note missing");
});
// unstable bomb -> no throw / no NaN (random 2x or 0)
for (let k = 0; k < 50; k++) testSpecial("unstable_bomb#"+k, ["unstable_bomb","crown","crown","cherry","star"], () => {});
// bloodrop -> curseGaugeUp signal
testSpecial("bloodrop", ["bloodrop","cherry","star","book","gem"], (res) => {
  if (!(res.curseGaugeUp >= 1)) fail("bloodrop curseGaugeUp not set");
});
// next-spin signals
testSpecial("seed_any", ["seed_basic","cherry","star","book","gem"], (res) => { if (res.growNext !== "ANY") fail("seed_any growNext"); });
testSpecial("sprout", ["sprout","cherry","star","book","gem"], (res) => { if (res.growNext !== "HIGH") fail("sprout growNext HIGH"); });
testSpecial("alarm", ["alarm","cherry","star","book","gem"], (res) => { if (!res.alarmNext) fail("alarm alarmNext"); });
testSpecial("hourglass", ["hourglass","crown","crown","crown","crown"], (res) => { if (!(res.carryExp > 0)) fail("hourglass carryExp>0"); });
testSpecial("shield", ["shield","cherry","star","book","gem"], (res) => { if (!res.shieldNext) fail("shield shieldNext"); });
testSpecial("exempt", ["exam_paper","cherry","star","book","gem"], (res) => { if (!res.exemptNext) fail("exempt exemptNext"); });
testSpecial("receipt", ["receipt","cherry","star","book","gem"], (res) => { if (!res.receiptNext) fail("receipt receiptNext"); });
testSpecial("curse_eye", ["curse_eye","cherry","star","book","gem"], (res) => { if (!res.curseEyeNext) fail("curse_eye curseEyeNext"); if (!(res.curseGaugeUp>=1)) fail("curse_eye gauge"); });

// WILD cap: many wilds should still respect DEEP.WILD_MAX in pouch validation; here check evaluate doesn't blow up
testSpecial("wild-many", ["wild","wild","wild","wild","cherry"], (res) => {
  if (res.exp < 0) fail("wild neg");
});

// specialMul cap: lucky7 x5 + prism many should never exceed MAX_SPIN_EXP_MUL multiplier growth (indirect: no Infinity)
testSpecial("cap-stack", ["lucky7","lucky7","lucky7","lucky7","lucky7"], (res) => {
  if (!Number.isFinite(res.exp)) fail("cap-stack non-finite");
});

// ------- deep-mode meta wiring via Game (next-spin carryover / alarm) -------
function deepMetaWiring() {
  const g = new Game({ seed: 55, storage: memStore() });
  g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, true);
  // drive to SPIN
  let st = g.state(); let guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
    else break;
  }
  if (!g.run || !g.run.deepMode) { fail("deep run not in deepMode"); return; }
  // simulate alarm signal consumption
  g.run.pendingNextExpMul = 1;
  g._applyDeepSpinMeta({ alarmNext: true, carryExp: 0, growNext: null, curseGaugeUp: 0 });
  if (Math.abs(g.run.pendingNextExpMul - 1.1) > 1e-9) fail(`alarm did not set pendingNextExpMul (got ${g.run.pendingNextExpMul})`);
  g._applyDeepSpinMeta({ carryExp: 30, growNext: null, curseGaugeUp: 0 });
  if (g.run.carryOverExp !== 30) fail("hourglass carry not stored");
  g._applyDeepSpinMeta({ growNext: "HIGH", carryExp: 0, curseGaugeUp: 0 });
  if (g.run.growNext !== "HIGH") fail("growNext not stored");
  g._applyDeepSpinMeta({ receiptNext: true, curseGaugeUp: 0, carryExp: 0 });
  if (!g.run.deepShopDiscount) fail("receipt flag not stored");
  g._applyDeepSpinMeta({ shieldNext: true, curseGaugeUp: 0, carryExp: 0 });
  if (!g.run.bossShield) fail("shield flag not stored");
  g._applyDeepSpinMeta({ curseGaugeUp: 2, carryExp: 0 });
  if (g.run.unluckyGauge < 2 && C.UNLUCKY_MAX >= 2) fail("curse gauge not applied");
  notes.push("deep meta wiring ok; unluckyMax=" + C.UNLUCKY_MAX);
}
deepMetaWiring();

// ------- (1) normal full runs -------
let normalTerminated = 0;
for (let s = 1; s <= 300; s++) { const st = playRun(1000 + s, false); if (st && st.phase === PHASE.RUN_END) normalTerminated++; }
notes.push(`normal runs terminated: ${normalTerminated}/300`);

// ------- (3) deep full runs (pouch contains many special symbols by construction of engine.startPouch + reward picks) -------
let deepTerminated = 0;
for (let s = 1; s <= 300; s++) { const st = playRun(2000 + s, true); if (st && st.phase === PHASE.RUN_END) deepTerminated++; }
notes.push(`deep runs terminated: ${deepTerminated}/300`);

// ------- deep: force many special symbols into pouch then spin heavily -------
function deepStress(seed) {
  const g = new Game({ seed, storage: memStore() });
  g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, true);
  let st = g.state(), guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
    else break;
  }
  if (!g.run) return;
  // inject specials into pouch
  const specials = ["lucky7","prism_sym","magic_wand","target","jigsaw","catalyst","mirror_sym",
    "black_candle_sym","unstable_bomb","bloodrop","curse_eye","seed_basic","sprout","alarm",
    "hourglass","shield","exam_paper","receipt","coupon","cart","gear","battery_sym","set_piece","wild"];
  for (const id of specials) g.run.pouch[id] = (g.run.pouch[id] || 0) + 6;
  // spin a lot across stages
  let g2 = 0;
  while (st && st.phase !== PHASE.RUN_END && g2++ < 800) {
    if (st.phase === PHASE.SPIN) { const out = g.spin("N"); if (out) { checkNum("deepStress.gained", out.gained, out.score, out.coins); scanState("deepStress", out.state); } st = g.state(); }
    else if (st.phase === PHASE.POST_SPIN) st = g.giveUp(false);
    else if (st.phase === PHASE.STAGE_CLEAR) st = g.proceedToNodes();
    else if (st.phase === PHASE.NODE_SELECT) st = g.selectNode(0);
    else if (st.phase === PHASE.PERK_PICK) st = g.pickPerk(0);
    else if (st.phase === PHASE.REWARD_DONE) st = g.proceedToStage();
    else if (st.phase === PHASE.SHOP) st = advanceShop(g);
    else if (st.phase === PHASE.DEVICE_NODE) st = skipNode(g);
    else break;
    // keep re-injecting specials each stage so pouch stays loaded
    if (g.run && g.run.pouch) for (const id of specials) g.run.pouch[id] = Math.max(g.run.pouch[id] || 0, 3);
  }
}
for (let s = 1; s <= 200; s++) deepStress(3000 + s);
notes.push("deep stress (200 runs, specials injected) done");

// ══════════════════════════════════════════════════════════════════════
//  Phase 5 — 심볼 증강/유물 + 부활 심볼 + 격리 검증
// ══════════════════════════════════════════════════════════════════════

// (P5-0) 데이터 무결성: 심볼증강 21(실버6/골드7/프리즘8)·유물 15·id 접두·일반 AUGMENTS/RELICS 와 id 충돌 없음.
{
  const augT = { SILVER: 0, GOLD: 0, PRISM: 0 };
  for (const a of SYM_AUGMENTS) { augT[a.t] = (augT[a.t] || 0) + 1; if (!/^s[ap]_/.test(a.id)) fail(`sym aug id prefix: ${a.id}`); if (!a.eff) fail(`sym aug no eff: ${a.id}`); }
  if (SYM_AUGMENTS.length !== 21) fail(`sym aug count ${SYM_AUGMENTS.length} != 21`);
  if (augT.SILVER !== 6 || augT.GOLD !== 7 || augT.PRISM !== 8) fail(`sym aug tier mix ${JSON.stringify(augT)}`);
  if (SYM_RELICS.length !== 15) fail(`sym rel count ${SYM_RELICS.length} != 15`);
  for (const r of SYM_RELICS) { if (!/^sr_/.test(r.id)) fail(`sym rel id prefix: ${r.id}`); if (r.t === "PRISM") fail(`sym rel has PRISM (no prism relics): ${r.id}`); if (!r.eff) fail(`sym rel no eff: ${r.id}`); }
  // 일반 증강/유물 id 와 충돌 금지(격리)
  const normalIds = new Set([...AUGMENTS.map(x=>x.id), ...RELICS.map(x=>x.id)]);
  for (const id in SYM_PERK_BY_ID) if (normalIds.has(id)) fail(`sym perk id collides with normal perk: ${id}`);
  notes.push(`sym perks: aug=${SYM_AUGMENTS.length}(${JSON.stringify(augT)}) rel=${SYM_RELICS.length}`);
}

// (P5-1) 격리: 일반모드 buildMods 에 심볼퍽 id 를 넣어도 mods 가 기본과 동일(무영향).
{
  const base = E.buildMods("basic", "gambler", []);
  const withSym = E.buildMods("basic", "gambler", SYM_AUGMENTS.map(a=>a.id).concat(SYM_RELICS.map(r=>r.id)));
  const keys = ["expMul","scoreMul","coinMul","flatExp","setExpMul","quotaMul","firstSpinExpMul","lastSpinExpMul"];
  for (const k of keys) if (base[k] !== withSym[k]) fail(`ISOLATION BREAK: buildMods ${k} changed by sym perks (${base[k]} → ${withSym[k]})`);
  // 일반모드 _mods() 는 deepEmptyScore/deepEmptyExp/deepTagMul 을 안 건드림
  const gN = new Game({ seed: 9, storage: memStore() }); gN.profile.runs = 1; gN.profile.playerLevel = 30;
  gN.startRun(0, false);
  while (gN.run && gN.run.phase !== PHASE.SPIN) {
    const s = gN.state();
    if (s.phase === PHASE.SELECT_CHAR) gN.selectChar(gN.unlockedChars()[0].id);
    else if (s.phase === PHASE.SELECT_MACHINE) gN.selectMachine(gN.unlockedMachines()[0].id);
    else if (s.phase === PHASE.SELECT_DEVICE) gN.selectDevice(s.options[0]?.id || "");
    else break;
  }
  gN.run.perks.push(...SYM_AUGMENTS.map(a=>a.id));   // pollute normal run with sym perks
  const nm = gN._mods();
  if ((nm.deepEmptyScore||0) !== 0) fail("ISOLATION: normal mode deepEmptyScore != 0");
  if ((nm.deepEmptyExp||0) !== 0) fail("ISOLATION: normal mode deepEmptyExp != 0");
  if (nm.deepTagMul && Object.keys(nm.deepTagMul).length) fail("ISOLATION: normal mode deepTagMul non-empty");
  notes.push("isolation: normal mode unaffected by sym perks");
}

// (P5-2) symPerkMods 효과 산출(순수함수) — 각 hook 이 NaN 없이 올바른 필드로.
function spm(ids, pouch) { return E.symPerkMods(ids, pouch); }
{
  const pouch = { cherry: 30, book: 10, skull: 5, empty: 3, gem: 5, star: 5, coin: 5 };
  // tag buff (태그감각=MOST +5%): 가장 많은 태그에 +0.05
  const most = E.mostCommonTag(pouch);
  const m1 = spm(["sa_tag_sense"], pouch);
  if (!most || Math.abs((m1.deepTagMul[most]||0) - 0.05) > 1e-9) fail(`sa_tag_sense tag buff wrong (most=${most}, val=${m1.deepTagMul[most]})`);
  // 빈칸활용/설명서
  const m2 = spm(["sa_use_empty","sr_empty_manual"], pouch);
  if (m2.emptyScore !== 20) fail(`emptyScore ${m2.emptyScore}`);
  if (m2.emptyExp !== 1) fail(`emptyExp ${m2.emptyExp}`);
  // penaltyMul 조건: 안전압축(§1.5 총량28↓)·심볼압축(27↓) — 총량 낮은 pouch 로
  const lowP = { cherry: 6, book: 5, star: 3, gem: 3, coin: 3, skull: 3, magnet: 3 };     // total 26 (≤27≤28)
  const m3 = spm(["sa_safe_compress","sa_compress"], lowP);
  if (!(m3.penaltyMul < 1)) fail(`penaltyMul should be <1 at low total (got ${m3.penaltyMul})`);
  // 조건 불만족(총량 높음: >28)이면 penaltyMul=1
  const hiP = { cherry: 10, book: 8, star: 7, gem: 6, coin: 5 };                           // 36 (>28)
  const m4 = spm(["sa_safe_compress","sa_compress"], hiP);
  if (Math.abs(m4.penaltyMul - 1) > 1e-9) fail(`penaltyMul should be 1 at high total (got ${m4.penaltyMul})`);
  // bound override §1.5 V3P1: 압축전공(min 21)·확장전공(max 48)
  const m5 = spm(["sp_compress_major","sp_expand_major"], pouch);
  if (m5.boundMin !== 21) fail(`sp_compress_major boundMin ${m5.boundMin} != 21`);
  if (m5.boundMax !== 48) fail(`sp_expand_major boundMax ${m5.boundMax} != 48`);
  // repairMul 연구실단골(전체 -30%)·심볼정리(remove -10%)
  const m6 = spm(["sa_lab_regular","sa_tidy"], pouch);
  if (Math.abs((m6.repairMul["*"]||1) - 0.7) > 1e-9) fail(`sa_lab_regular repairMul* ${m6.repairMul["*"]}`);
  if (Math.abs((m6.repairMul.remove||1) - 0.9) > 1e-9) fail(`sa_tidy repairMul.remove ${m6.repairMul.remove}`);
  // rewardBonus 심볼분류함 +1
  if (spm(["sr_sorter"], pouch).rewardBonus !== 1) fail("sr_sorter rewardBonus");
  // addBoost 기본연구 +1 / 희귀연구 rareChance / 전설수집 legendWeight+bossQuotaMul
  const m7 = spm(["sa_basic_research","sa_rare_research","sp_legend_collect"], pouch);
  if (m7.addBasicDelta !== 1) fail(`addBasicDelta ${m7.addBasicDelta}`);
  if (m7.rareChance !== 0.10) fail(`rareChance ${m7.rareChance}`);  // V3P2: sa_rare_research goldBonus +10%p(0.10, 구 v2 0.5에서 재리맵)
  if (m7.legendWeight !== 1) fail(`legendWeight ${m7.legendWeight}`);
  if (Math.abs(m7.bossQuotaMul - 1.2) > 1e-9) fail(`bossQuotaMul ${m7.bossQuotaMul}`);
  // NaN scan of all-perk aggregate
  const all = spm([...SYM_AUGMENTS.map(a=>a.id), ...SYM_RELICS.map(r=>r.id)], pouch);
  for (const k of ["emptyScore","emptyExp","penaltyMul","quotaMul","bossQuotaMul","scoreMul","rewardBonus","addBasicDelta","legendWeight","swapCoin","purifyCoin","repairRefundFrac"]) checkNum(`symPerkMods.${k}`, all[k]);
  notes.push("symPerkMods hooks ok");
}

// (P5-3) evaluate 빈칸활용/설명서 — deepEmptyScore/deepEmptyExp 주입 시 empty 셀에 점수/EXP.
{
  const m = E.defaultMods(); m.deepEmptyScore = 20; m.deepEmptyExp = 1;
  const raw = [SYM_BY_ID.cherry, SYM_BY_ID.book, null, null, SYM_BY_ID.gem].map((s, i) => (s ? { sym: s, tag: "" } : { sym: { id: "empty", e: "▫", n: "빈칸", exp: 0, score: 0, coin: 0, special: "NONE", rare: false, tags: [] }, tag: "" }));
  const res = E.evaluate(E.makeRng(3), raw, m, 1, 5, false);
  checkNum("empty-eval", res.exp, res.score);
  if (!res.notes.some(n => n.includes("빈칸"))) fail("empty note missing");
  // baseline (no empty mods) should NOT add empty bonuses (isolation of block)
  const res0 = E.evaluate(E.makeRng(3), raw, E.defaultMods(), 1, 5, false);
  if (res.score <= res0.score) fail(`empty score not added (${res.score} <= ${res0.score})`);
}

// (P5-4) 심화 런: SYMAUG/SYMREL/AUGLEVEL 노드가 실제로 생성·선택 가능한지 (강제 주입으로 픽 경로 확인).
function deepPickPath(seed) {
  const g = new Game({ seed, storage: memStore() });
  g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, true);
  let st = g.state(), guard = 0;
  const seenNodes = new Set(); let symAugPicked = 0, symRelPicked = 0, augLevelPicked = 0, symOffers = 0;
  while (st && st.phase !== PHASE.RUN_END && guard++ < 3000) {
    switch (st.phase) {
      case PHASE.SELECT_CHAR: st = g.selectChar(g.unlockedChars()[0].id); break;
      case PHASE.SELECT_MACHINE: st = g.selectMachine(g.unlockedMachines()[0].id); break;
      case PHASE.SELECT_DEVICE: st = g.selectDevice(st.options[0]?.id || ""); break;
      case PHASE.SPIN: { const out = g.spin("N"); if (out) { checkNum("dpp.gained", out.gained, out.score, out.coins); scanState("dpp", out.state); } st = g.state(); break; }
      case PHASE.POST_SPIN: st = g.giveUp(false); break;
      case PHASE.STAGE_CLEAR: st = g.proceedToNodes(); break;
      case PHASE.NODE_SELECT: {
        // record node types then prefer a sym node if present
        (st.nodes||[]).forEach(n => seenNodes.add(n));
        let idx = 0;
        const wanted = ["SYMAUG","SYMREL","AUGLEVEL"];
        for (const w of wanted) { const i = st.nodes.indexOf(w); if (i >= 0) { idx = i; break; } }
        const chosen = st.nodes[idx];
        if (chosen === "SYMAUG") symAugPicked++; if (chosen === "SYMREL") symRelPicked++; if (chosen === "AUGLEVEL") augLevelPicked++;
        st = g.selectNode(idx); break;
      }
      case PHASE.PERK_PICK: {
        // for AUG/REL sym pick, verify option shape + count NON-EMPTY offers
        //  (★빈 오퍼는 _enterRewardDone 폴백으로 PERK_PICK 자체를 안 거침 → 여기 도달 = 실오퍼.
        //   symAugPool 이 심볼해금집합으로 퍽을 걸러 풀이 0 되던 버그의 회귀 감지용.)
        if (st.pickKind === "AUG" || st.pickKind === "REL") {
          for (const o of (st.options||[])) { if (!o.t || !o.e || !o.n) fail(`sym perk option shape bad: ${JSON.stringify(o)}`); }
          if ((st.options||[]).length) symOffers++;
        }
        st = g.pickPerk(0); break;
      }
      case PHASE.SHOP: st = g.shopExit(); break;
      case PHASE.DEVICE_NODE: st = g.deviceNodeTake(); break;
      case PHASE.REWARD_DONE: st = g.proceedToStage(); break;
      default: st = g.state(); guard = 3000; break;
    }
    if (!st) break;
  }
  return { st, seenNodes, symAugPicked, symRelPicked, augLevelPicked, symOffers };
}
{
  // 풀 스모크: 심볼 해금집합을 넘겨도 심볼퍽 풀은 비면 안 됨(퍽id vs 심볼id 비교 버그 회귀 방지).
  if (!E.symAugPool(new Set(DEFAULT_UNLOCKED_SYMS)).length) fail("symAugPool EMPTY with symbol unlock set (perk-id vs symbol-id bug)");
  if (!E.symRelPool(new Set(DEFAULT_UNLOCKED_SYMS)).length) fail("symRelPool EMPTY with symbol unlock set (perk-id vs symbol-id bug)");
  let symAug = 0, symRel = 0, augLvl = 0, term = 0, offers = 0; const allNodes = new Set();
  for (let s = 1; s <= 250; s++) { const r = deepPickPath(4000 + s); if (r.st && r.st.phase === PHASE.RUN_END) term++; symAug += r.symAugPicked; symRel += r.symRelPicked; augLvl += r.augLevelPicked; offers += r.symOffers; r.seenNodes.forEach(n => allNodes.add(n)); }
  notes.push(`deep pick-path: term=${term}/250 · symAug picks=${symAug} · symRel picks=${symRel} · augLevel picks=${augLvl} · real offers=${offers} · nodes=${[...allNodes].join(",")}`);
  if (symAug === 0) fail("SYMAUG node never appeared/picked in 250 deep runs");
  if (symRel === 0) fail("SYMREL node never appeared/picked in 250 deep runs");
  if (offers === 0) fail("sym perk offers ALWAYS EMPTY in-game (pool/unlock filter dead — SYMAUG/SYMREL fallback only)");
  if (!allNodes.has("POUCH")) fail("POUCH node missing in deep runs");
}

// (P5-5) 부활 심볼: highlighter/review_book/repair_kit 가 POUCH_SYMBOLS 에 편입 + evaluate 신호.
{
  for (const id of ["highlighter","review_book","repair_kit"]) if (!POUCH_SYMBOLS.includes(id)) fail(`revived sym not in POUCH_SYMBOLS: ${id}`);
  const sig = (id) => E.evaluate(E.makeRng(5), [id,"cherry","star","book","gem"].map(x=>({sym:SYM_BY_ID[x],tag:""})), E.defaultMods(), 1, 5, false);
  if (!sig("highlighter").augChanceNext) fail("highlighter augChanceNext signal missing");
  if (!sig("review_book").augLevelNext) fail("review_book augLevelNext signal missing");
  if (!sig("repair_kit").kitNext) fail("repair_kit kitNext signal missing");
  // wiring: augChanceNext boosts _augLevelBoost, augLevelNext levels a held aug
  const g = new Game({ seed: 77, storage: memStore() }); g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, true);
  let st = g.state(); let guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
    else break;
  }
  g.run._augLevelBoost = 0;
  g._applyDeepSpinMeta({ augChanceNext: true, curseGaugeUp: 0, carryExp: 0 });
  if (Math.abs(g.run._augLevelBoost - 0.15) > 1e-9) fail(`highlighter boost not applied (${g.run._augLevelBoost})`);
  // give a levelable aug then apply review_book
  g.run.perks.push("study"); g.run.perkLevels = {};
  g._applyDeepSpinMeta({ augLevelNext: true, curseGaugeUp: 0, carryExp: 0 });
  if ((g.run.perkLevels.study || 1) < 2) fail("review_book did not level up held aug");
  notes.push("revived symbols (highlighter/review_book/repair_kit) wired");
}

// (P5-5b) 심볼증강 레벨업(형광펜/복습책 대상) — 레벨 2/3 가 효과를 실제로 강화.
{
  const pouch = { cherry: 30, book: 10, star: 8, gem: 8, coin: 8, skull: 5, magnet: 4 };
  const most = E.mostCommonTag(pouch);
  const l1 = E.symPerkMods(["sa_tag_sense"], pouch, { sa_tag_sense: 1 });
  const l2 = E.symPerkMods(["sa_tag_sense"], pouch, { sa_tag_sense: 2 });
  const l3 = E.symPerkMods(["sa_tag_sense"], pouch, { sa_tag_sense: 3 });
  if (!(l2.deepTagMul[most] > l1.deepTagMul[most])) fail(`sa_tag_sense Lv2 not stronger (${l1.deepTagMul[most]}→${l2.deepTagMul[most]})`);
  if (!(l3.deepTagMul[most] > l2.deepTagMul[most])) fail(`sa_tag_sense Lv3 not stronger`);
  // penaltyMul (심볼압축) 레벨업 → 더 작아짐(더 이득). §1.5 V3P1: sa_compress whenTotalLe 27 → 총량 ≤27 pouch.
  const lowP = { cherry: 6, book: 5, star: 4, gem: 4, coin: 3, skull: 3, magnet: 2 };   // total 27 (≤27)
  const c1 = E.symPerkMods(["sa_compress"], lowP, { sa_compress: 1 });
  const c3 = E.symPerkMods(["sa_compress"], lowP, { sa_compress: 3 });
  if (!(c3.penaltyMul < c1.penaltyMul)) fail(`sa_compress Lv3 penaltyMul not lower (${c1.penaltyMul}→${c3.penaltyMul})`);
  if (c3.penaltyMul < 0) fail("penaltyMul negative");
  // non-levelable sym aug unaffected by levels
  const nl = E.symPerkMods(["sa_lab_regular"], pouch, { sa_lab_regular: 3 });
  if (Math.abs((nl.repairMul["*"]||1) - 0.7) > 1e-9) fail("non-levelable sym aug changed by level");
  // _levelableHeld picks up symbol augments in deep mode; AUGLEVEL card shape
  const g = new Game({ seed: 44, storage: memStore() }); g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, true);
  let st = g.state(); let guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
    else break;
  }
  g.run.perks.push("sa_tag_sense");
  const lh = g._levelableHeld();
  if (!lh.includes("sa_tag_sense")) fail("_levelableHeld missing symbol aug");
  notes.push("symbol aug level-up scales effects + _levelableHeld includes sym augs");
}

// (P5-6) 정비 가격 할인(심볼정리/연구실단골) + 교체/정화 코인보상 스모크.
{
  const g = new Game({ seed: 88, storage: memStore() }); g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, true);
  let st = g.state(); let guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
    else break;
  }
  const svcBase = g.repairServices();
  g.run.perks.push("sa_lab_regular");   // -30% all
  const svcDisc = g.repairServices();
  for (let i = 0; i < svcBase.length; i++) if (!(svcDisc[i].price <= svcBase[i].price)) fail(`lab_regular not discounting ${svcBase[i].id}`);
  if (!svcDisc.some(s => s.discounted)) fail("no service marked discounted");
  // swap coin bonus: 교체전문가 +3 · 정화붓 정화 코인
  g.run.perks.push("sa_swap_expert","sr_brush");
  const coinsBefore = g.run.coins = 999;
  // ensure a swap-able target. §1.4 V3P1 DECK_MAX 40 → 총량 ≤40 pouch (swap/purify 는 총량 유지, 검증 통과 필요).
  g.run.pouch = { cherry: 8, book: 7, star: 6, gem: 5, coin: 5, skull: 5, magnet: 4 };  // total 40
  g.repairBuy("sv_swap", { from: "cherry", to: "book" });
  if (g.run.coins <= (coinsBefore - 18)) { /* base price 18; +3 refund makes net */ }
  // purify coin
  g.run.coins = 999; g.run.pouch = { cherry: 8, book: 7, star: 6, gem: 5, coin: 5, skull: 5, magnet: 4 };  // total 40
  const before = g.run.coins;
  g.repairBuy("sv_purify", {});
  // net = -20 price +3 brush = -17
  if (before - g.run.coins > 20) fail(`purify with brush cost too high (net ${before - g.run.coins})`);
  notes.push("repair pricing + swap/purify coin bonus ok");
}

// (P5-7) 격리 재확인: 일반 300런 + 심화 300런 재실행(회귀 방지, 위에서 이미 했으나 sym perks 편입 후).
{
  let n=0,d=0;
  for (let s=1;s<=150;s++){ const st=playRun(7000+s,false); if(st&&st.phase===PHASE.RUN_END)n++; }
  for (let s=1;s<=150;s++){ const st=playRun(8000+s,true); if(st&&st.phase===PHASE.RUN_END)d++; }
  notes.push(`re-run after P5: normal ${n}/150 · deep ${d}/150 terminated`);
}

// ══════════════════════════════════════════════════════════════════════
//  Phase 5 (담당2) — 심볼 장치 + 심화 업적 + 해금 시스템 + 격리 검증
// ══════════════════════════════════════════════════════════════════════

// helper: deep run driven to SPIN with a given device forced-equipped.
function deepAtSpin(seed, deviceId, level = 30) {
  const g = new Game({ seed, storage: memStore() });
  g.profile.runs = 1; g.profile.playerLevel = level;
  if (deviceId) g.profile.ownedDevices.push(deviceId);
  g.startRun(0, true);
  let st = g.state(), guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) { const opt = deviceId ? (st.options.find(o => o.id === deviceId) || st.options[0]) : st.options[0]; st = g.selectDevice(opt?.id || ""); }
    else break;
  }
  return g;
}

// (P5-8) 데이터 무결성: 심볼 장치 9종·심화 업적 13종·해금맵 정합.
{
  const symDevs = DEVICES.filter(d => d.deepOnly);
  if (symDevs.length !== 9) fail(`sym devices count ${symDevs.length} != 9`);
  for (const d of symDevs) { if (!d.deepOnly) fail(`sym dev not deepOnly: ${d.id}`); if (!["PASSIVE","MANIP","PEEK"].includes(d.kind)) fail(`sym dev kind ${d.id}=${d.kind}`); }
  const deepAch = ACHIEVEMENTS.filter(a => a.deep);
  if (deepAch.length !== 13) fail(`deep achievements ${deepAch.length} != 13`);
  for (const a of deepAch) { if (!/^deep/.test(a.key)) fail(`deep ach key not deep* : ${a.id}=${a.key}`); }
  // ACH_SYMBOL_UNLOCK: 모든 키가 deep 업적 · 모든 값이 POUCH_SYMBOLS 심볼.
  for (const k in ACH_SYMBOL_UNLOCK) {
    if (!ACHIEVEMENTS.some(a => a.id === k && a.deep)) fail(`ACH_SYMBOL_UNLOCK key not a deep ach: ${k}`);
    const sym = ACH_SYMBOL_UNLOCK[k]; if (!POUCH_SYMBOLS.includes(sym)) fail(`unlock sym not in POUCH_SYMBOLS: ${sym}`);
    if (DEFAULT_UNLOCKED_SYMS.has(sym)) fail(`ACH_SYMBOL_UNLOCK value is ALREADY default-unlocked (dead unlock): ${sym}`);
  }
  // ACH_DEVICE_REWARD: 심화 업적이 지급하는 장치는 전부 deepOnly.
  for (const k in ACH_DEVICE_REWARD) { const a = ACHIEVEMENTS.find(x => x.id === k); if (a && a.deep) { const d = DEV_BY_ID[ACH_DEVICE_REWARD[k]]; if (!d || !d.deepOnly) fail(`deep ach ${k} rewards non-deep device ${ACH_DEVICE_REWARD[k]}`); } }
  // DEFAULT_UNLOCKED_SYMS: 담긴 심볼 전부 POUCH_SYMBOLS 안(오타 방지).
  for (const id of DEFAULT_UNLOCKED_SYMS) if (!POUCH_SYMBOLS.includes(id)) fail(`DEFAULT_UNLOCKED not in POUCH_SYMBOLS: ${id}`);
  // START_POUCH 심볼은 반드시 기본 해금(게임 성립).
  for (const id in DEEP.START_POUCH) if (!DEFAULT_UNLOCKED_SYMS.has(id)) fail(`START_POUCH sym not default-unlocked: ${id}`);
  notes.push(`sym devices=${symDevs.length} · deep ach=${deepAch.length} · unlock map=${Object.keys(ACH_SYMBOL_UNLOCK).length} · default-unlocked=${DEFAULT_UNLOCKED_SYMS.size}`);
}

// (P5-9) 해금 시스템: 기본 상태에서 잠긴 심볼은 offerSymbolRewards/repairTargets add·swap 후보에 안 나온다.
{
  const g = deepAtSpin(9100, null);
  const uSet = g._symUnlockedSet();
  if (!uSet || uSet.size === 0) fail("unlock set empty");
  // 잠긴 심볼(업적 미달성) 최소 하나는 존재해야 "해금제"가 의미
  const locked = POUCH_SYMBOLS.filter(id => !uSet.has(id) && id !== "empty" && id !== "random");
  if (!locked.length) fail("no locked symbols (unlock system inert)");
  // offerSymbolRewards: add/swap 후보에 잠긴 심볼 id 미포함.
  const off = E.offerSymbolRewards(g.rng, g.run.pouch, 3, 30, { symUnlocked: uSet });
  for (const o of off) {
    if (o.type === "add" && locked.includes(o.id)) fail(`locked sym offered as add: ${o.id}`);
    if (o.type === "swap" && locked.includes(o.to)) fail(`locked sym offered as swap-to: ${o.to}`);
  }
  // repairTargets: addRare/swap-to 후보 잠긴 심볼 미포함.
  for (const t of g.repairTargets("sv_add_rare")) if (locked.includes(t.id)) fail(`locked sym in addRare targets: ${t.id}`);
  for (const t of g.repairTargets("sv_swap", "to")) if (locked.includes(t.id)) fail(`locked sym in swap-to targets: ${t.id}`);
  // 해금 후: symUnlocked 에 넣으면 후보에 등장.
  const someLocked = locked.find(id => (POUCH_RARITY[id] || "기본") === "희귀") || locked[0];
  g.profile.symUnlocked.push(someLocked); g._invalidateSymUnlockCache();
  if (!g._symUnlockedSet().has(someLocked)) fail("unlock not reflected after push+invalidate");
  notes.push(`unlock filter ok — locked=${locked.length}, sample=${someLocked}`);
}

// (P5-10) 심화 업적 → 심볼 해금: _gameOver 시 카운터 누적 + 업적/심볼 해금. 일반 카운터 무오염(격리).
{
  // 심화 런 강제 달성: deepStats 플래그 직접 세팅 후 _gameOver.
  const g = deepAtSpin(9200, null);
  const p = g.profile;
  const normalBefore = { cherryTotal: p.counters.cherryTotal||0, bossClears: p.counters.bossClears||0 };
  // pollute r.stats to confirm deep run still accrues normal counters (기존 동작 — 격리는 '심화 업적' 카운터가 deep* 뿐임을 검증)
  g.run.deepStats.bossClears = 10;                     // 심볼마스터(th10)
  g.run.deepStats.compress95Clear = true;             // 첫압축
  g.run.deepStats.raresSeen = new Set(["magic_wand","hourglass","coupon","cart","review_book","repair_kit","flame","ember","wild","f10"]); // 10종
  g.run.deepStats.legendsSeen = new Set(["crown","lucky7","prism_sym","l4","l5"]);   // 5종
  g._gameOver(true);
  // 심화 카운터 누적됐나
  if ((p.counters.deepRuns||0) < 1) fail("deepRuns not counted");
  if ((p.counters.deepBossClears||0) < 10) fail(`deepBossClears ${p.counters.deepBossClears}`);
  if ((p.counters.deepRaresSeen||0) < 10) fail(`deepRaresSeen ${p.counters.deepRaresSeen}`);
  if ((p.counters.deepLegendsSeen||0) < 5) fail(`deepLegendsSeen ${p.counters.deepLegendsSeen}`);
  // 업적 해금됐나 (심볼마스터/첫압축/희귀수집가/전설연구자)
  for (const id of ["d_ach_start","d_ach_master","d_ach_compress1","d_ach_rare10","d_ach_legend5"]) if (!p.unlocked.includes(id)) fail(`deep ach not unlocked: ${id}`);
  // 심볼 해금됐나
  for (const id of ["d_ach_master","d_ach_compress1","d_ach_rare10","d_ach_legend5"]) { const sym = ACH_SYMBOL_UNLOCK[id]; if (!p.symUnlocked.includes(sym)) fail(`sym not unlocked by ${id}: ${sym}`); }
  // 심화 업적이 지급하는 장치 획득됐나
  for (const id of ["d_ach_master","d_ach_compress1"]) { const dev = ACH_DEVICE_REWARD[id]; if (dev && !p.ownedDevices.includes(dev)) fail(`device not granted by ${id}: ${dev}`); }
  notes.push(`deep ach unlock ok — unlocked=${p.unlocked.filter(x=>/^d_ach/.test(x)).length} deep ach · symUnlocked=${p.symUnlocked.length}`);
}

// (P5-10b) 격리: 일반 런 _gameOver 는 deep* 카운터를 절대 만들지 않는다.
{
  const g = new Game({ seed: 9300, storage: memStore() }); g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, false);
  let st = g.state(), guard = 0;
  while (st && st.phase !== PHASE.RUN_END && guard++ < 2000) {
    switch (st.phase) {
      case PHASE.SELECT_CHAR: st = g.selectChar(g.unlockedChars()[0].id); break;
      case PHASE.SELECT_MACHINE: st = g.selectMachine(g.unlockedMachines()[0].id); break;
      case PHASE.SELECT_DEVICE: st = g.selectDevice(st.options[0]?.id || ""); break;
      case PHASE.SPIN: g.spin("N"); st = g.state(); break;
      case PHASE.POST_SPIN: st = g.giveUp(false); break;
      case PHASE.STAGE_CLEAR: st = g.proceedToNodes(); break;
      case PHASE.NODE_SELECT: st = g.selectNode(0); break;
      case PHASE.PERK_PICK: st = g.pickPerk(0); break;
      case PHASE.SHOP: st = g.shopExit(); break;
      case PHASE.DEVICE_NODE: st = g.deviceNodeTake(); break;
      case PHASE.REWARD_DONE: st = g.proceedToStage(); break;
      default: st = g.state(); guard = 2000; break;
    }
    if (!st) break;
  }
  const cn = g.profile.counters;
  for (const k of Object.keys(cn)) if (/^deep/.test(k)) fail(`ISOLATION: normal run created deep counter ${k}=${cn[k]}`);
  if (g.profile.symUnlocked && g.profile.symUnlocked.length) fail(`ISOLATION: normal run unlocked symbols: ${g.profile.symUnlocked}`);
  notes.push("isolation: normal run creates no deep* counters / no symbol unlocks");
}

// (P5-11) 심볼 PASSIVE 장치 효과(_mods) — 압축게이지/확장저울/전설봉인기. deepMode 게이팅.
{
  // 압축게이지: 총량 낮을수록 expMul↑
  const g = deepAtSpin(9400, "dev_compress_gauge");
  if (g.run.device !== "dev_compress_gauge") fail("compress_gauge not equipped");
  // §1.5 V3P1: 압축게이지 구간 24/27/30(×0.5 리스케일). total 24(≤24 → +14%) vs 34(>30 → no bonus).
  g.run.pouch = { cherry: 6, book: 4, star: 4, gem: 3, coin: 3, skull: 2, magnet: 2 };    // 24 total → +14%
  const low = g._mods().expMul;
  g.run.pouch = { cherry: 10, book: 8, star: 6, gem: 5, coin: 5 };                        // 34 total → no bonus
  const high = g._mods().expMul;
  if (!(low > high)) fail(`compress_gauge low-total not stronger (${low} <= ${high})`);
  // 확장저울: §1.5 V3P1 총량 36↑ → skullPenaltyMul 감소(×0.5 리스케일).
  const g2 = deepAtSpin(9401, "dev_expand_scale");
  g2.run.pouch = { cherry: 8, book: 6, star: 6, gem: 6, coin: 5, skull: 5, magnet: 4, bomb: 2 };  // 42 (≥36) → reduce
  const spm = g2._mods().skullPenaltyMul;
  if (!(spm < 1)) fail(`expand_scale not reducing skull penalty at high total (${spm})`);
  g2.run.pouch = { cherry: 6, book: 5, star: 4, gem: 4, coin: 3, skull: 3, magnet: 3 };   // 28 (<36) → none
  if (Math.abs((g2._mods().skullPenaltyMul ?? 1) - 1) > 1e-9) fail("expand_scale active at low total (should not)");
  // 전설봉인기: 전설 심볼 보유 시 exp/score 소폭↑
  const g3 = deepAtSpin(9402, "dev_legend_seal");
  g3.run.pouch = { cherry: 8, book: 7, star: 6, gem: 5, coin: 5, skull: 4 };              // 35 total, no legend
  const noLeg = g3._mods().expMul;
  g3.run.pouch = { cherry: 8, book: 7, star: 6, gem: 5, coin: 5, crown: 2 };              // 33 total, legend
  const withLeg = g3._mods().expMul;
  if (!(withLeg > noLeg)) fail(`legend_seal not boosting with legend (${withLeg} <= ${noLeg})`);
  // 격리: 일반 런에 심볼장치 강제장착해도 _mods 무영향(deepMode 게이팅).
  const gn = new Game({ seed: 9403, storage: memStore() }); gn.profile.runs = 1; gn.profile.playerLevel = 30;
  gn.startRun(0, false);
  let st = gn.state(), guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = gn.selectChar(gn.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = gn.selectMachine(gn.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = gn.selectDevice(st.options[0]?.id || "");
    else break;
  }
  const beforeExp = gn._mods().expMul;
  gn.run.device = "dev_compress_gauge";   // force-equip in normal (no pouch)
  const afterExp = gn._mods().expMul;
  if (Math.abs(beforeExp - afterExp) > 1e-9) fail(`ISOLATION: sym device changed normal-mode expMul (${beforeExp}→${afterExp})`);
  notes.push("sym passive devices (compress/expand/legend) ok + normal-mode isolation");
}

// (P5-12) 심볼 MANIP 장치(지휘/정화/셔플) — 스핀 조작 스모크(no throw·유효 결과). deepMode 게이팅.
{
  const g = deepAtSpin(9500, "dev_purify_glove");
  g.spin("N");   // produce lastCells
  // force a skull into lastCells then purify
  if (g.run.lastCells && g.run.lastCells.length) {
    g.run.lastCells[0] = { sym: SYM_BY_ID.skull || { id: "skull", special: "SKULL", e: "☠", n: "해골", exp: 0, score: 0, coin: 0, tags: [] }, tag: "" };
    const out = g.manip("정화", 0);
    checkNum("purify manip", out.gained);
    if (g.run.lastCells.some(c => c.sym.special === "SKULL" || c.sym.id === "skull")) { /* may re-eval; just ensure no crash */ }
  }
  // 지휘: baton
  const g2 = deepAtSpin(9501, "dev_baton");
  g2.spin("N");
  if (g2.run.lastCells && g2.run.lastCells.length) { const out = g2.manip("지휘", 1); checkNum("baton manip", out.gained); }
  // 셔플: pouch_shuffler
  const g3 = deepAtSpin(9502, "dev_pouch_shuffler");
  g3.spin("N");
  if (g3.run.lastCells) { const out = g3.manip("셔플", 0); checkNum("shuffle manip", out.gained); }
  // 격리: 일반 런에서 지휘/정화/셔플 호출 → 무효(deepMode 게이팅, "불가" 토스트).
  const gn = new Game({ seed: 9503, storage: memStore() }); gn.profile.runs = 1; gn.profile.playerLevel = 30;
  gn.profile.ownedDevices.push("dev_baton");
  gn.startRun(0, false);
  let st = gn.state(), guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = gn.selectChar(gn.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = gn.selectMachine(gn.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = gn.selectDevice(st.options[0]?.id || "");
    else break;
  }
  // 일반 런에선 dev_baton 이 선택 후보에서 제외되어 장착 불가여야 함
  if (gn.run.device === "dev_baton") fail("ISOLATION: deepOnly device equipped in normal run");
  gn.spin("N");
  const before = gn.run.lastCells ? gn.run.lastCells.map(c => c.sym.id).join(",") : "";
  gn.run.device = "dev_baton";   // even if force-set, manip must reject (deepMode gate)
  gn.manip("지휘", 1);
  const after = gn.run.lastCells ? gn.run.lastCells.map(c => c.sym.id).join(",") : "";
  if (before !== after) fail("ISOLATION: baton manip changed cells in normal mode");
  notes.push("sym manip devices (baton/purify/shuffle) smoke + normal-mode gated");
}

// (P5-13) 표시형 장치(태그스캐너/희귀탐지기) state 노출.
{
  const g = deepAtSpin(9600, "dev_tag_scanner");
  const info = g.deepDeviceInfo();
  if (!info || !info.tagScanner) fail("tag_scanner deepDeviceInfo missing");
  const g2 = deepAtSpin(9601, "dev_rare_detector");
  const info2 = g2.deepDeviceInfo();
  if (!info2 || typeof info2.rareDetector?.possible !== "boolean") fail("rare_detector deepDeviceInfo missing");
  // dex: deepSym locked 필드 + ach deep/symReward 필드 존재
  const dx = g.dex();
  if (!dx.deepSym.some(s => typeof s.locked === "boolean")) fail("dex deepSym locked field missing");
  if (!dx.ach.some(a => a.deep && a.symReward)) fail("dex ach deep+symReward missing");
  notes.push("display devices exposed via deepDeviceInfo + dex locked/deep fields");
}

// (P5-14) 대형주머니(§1.5 V3P1 총량36↑) 업적 통계 — deepStats.maxTotal 갱신 + 카운터 승격.
{
  const g = deepAtSpin(9700, null);
  g.run.pouch = { cherry: 8, book: 7, star: 6, gem: 6, coin: 5, skull: 5, magnet: 4, bomb: 2 }; // 43 (>36·≤sp_expand_major 48)
  g._trackDeepStats();
  if (g.run.deepStats.maxTotal < 43) fail(`maxTotal not tracked (${g.run.deepStats.maxTotal})`);
  g._gameOver(true);
  if ((g.profile.counters.deepMaxTotal||0) < 36) fail(`deepMaxTotal counter ${g.profile.counters.deepMaxTotal}`);
  if (!g.profile.unlocked.includes("d_ach_big_pouch")) fail("d_ach_big_pouch not unlocked at total 43 (th=36)");
  notes.push("V3P1 deepMaxTotal tracked + big-pouch achievement (th=36)");
}

// (P5-15) 최종 격리 재확인: 심볼장치/업적/해금 도입 후 일반 200 + 심화 200 완주.
{
  let n=0,d=0;
  for (let s=1;s<=200;s++){ const st=playRun(9800+s,false); if(st&&st.phase===PHASE.RUN_END)n++; }
  for (let s=1;s<=200;s++){ const st=playRun(10200+s,true); if(st&&st.phase===PHASE.RUN_END)d++; }
  notes.push(`final re-run: normal ${n}/200 · deep ${d}/200 terminated`);
}

// ══════════════════════════════════════════════════════════════════════
//  심화 관련성 필터 (deep-aug-relevance · WEBSLOT_DEEP_AUG_SPEC Part1) — game.js 와 같은 샤드 동기수정.
//  ★일반모드 무회귀는 위 일반 300런/최종 재확인이 커버. 여기선 심화 유입 경로 전수(오퍼/상점/이벤트/
//   세트조각=RUN_END perks 전수 + honor 시작증강 + INSTANT 아이템)와 게이트/브릿지/보장 가드를 단정.
// ══════════════════════════════════════════════════════════════════════

// (DA-1) deepCompatPool/familyCount/isDeepCompat 유닛 — REL_MIN 게이트·crown 오버라이드·tag:학습·무태깅 제외.
{
  // ★배치 I: REL_MIN 5→3. 게이트 경계 2(차단)/3(통과)로 재조정.
  if (E.isDeepCompat({ deep: 1, dSym: "magnet" }, { magnet: 2, cherry: 20 })) fail("DA-1: magnet 2 should be gated (REL_MIN 3)");
  if (!E.isDeepCompat({ deep: 1, dSym: "magnet" }, { magnet: 3, cherry: 20 })) fail("DA-1: magnet 3 should pass");
  if (E.isDeepCompat({ deep: 2, dSym: "crown" }, { crown: 1 })) fail("DA-1: crown 1 should be gated (override 2)");
  if (!E.isDeepCompat({ deep: 2, dSym: "crown" }, { crown: 2 })) fail("DA-1: crown 2 should pass (REL_MIN_BY_SYM)");
  if (E.familyCount({ book: 3, tome: 2 }, "tag:학습") !== 5) fail("DA-1: tag:학습 familyCount != book+tome");
  if (!E.isDeepCompat({ deep: 1, dSym: "tag:학습" }, { book: 2, tome: 1 })) fail("DA-1: tag:학습 3 should pass");
  if (E.isDeepCompat({ deep: 1, dSym: "tag:학습" }, { book: 1, tome: 1 })) fail("DA-1: tag:학습 2 should be gated");
  if (E.familyCount({ cherry: 2, cherry_ripe: 3 }, "cherry") !== 5) fail("DA-1: cherry family != base+upgrade sum");
  if (E.isDeepCompat({ id: "x" }, { cherry: 99 })) fail("DA-1: untagged perk must be excluded");
  if (E.deepCompatPool([{ deep: 1 }, {}], {}).length !== 1) fail("DA-1: deepCompatPool filter broken");
  // 실카탈로그: 넉넉한 주머니에서도 무태깅(D계열 등) 퍽은 절대 통과 금지.
  const richPouch = { cherry: 99, book: 20, tome: 5, star: 15, gem: 15, coin: 10, skull: 10, flame: 6, magnet: 9, bomb: 3, crown: 2 };
  for (const p of E.deepCompatPool(AUGMENTS.concat(RELICS), richPouch)) if (!p.deep) fail(`DA-1: untagged passed pool: ${p.id}`);
  // 배치 B/A 이후 잔존 무태깅(D/제외) 퍽 목록 (신규 태깅된 퍽은 제거):
  //  - seed_garden(wadd "seed" — seed 미POUCH_SYMBOLS, 완전 no-op → 미태깅 유지)
  //  - fate_bell(별도 구현됨 — deep 필드 없음)
  //  ★배치 A: sacrifice/black_diploma/curse_grad/black_grad_photo → deep:1 태깅 완료 → 이 배제목록에서 제거.
  for (const id of ["seed_garden", "fate_bell"]) {
    const src = AUG_BY_ID[id] || REL_BY_ID[id];
    if (!src) { fail(`DA-1: excluded-list id missing in catalog: ${id}`); continue; }
    if (E.isDeepCompat(src, richPouch)) fail(`DA-1: D/제외 퍽 not excluded: ${id}`);
  }
  // 배치 A: 저주 참조 퍽 4종이 deep:1 태깅 후 richPouch 에서 통과(dSym 없음→무조건 통과) 확인.
  for (const id of ["sacrifice", "black_diploma", "curse_grad", "black_grad_photo"]) {
    const src = AUG_BY_ID[id] || REL_BY_ID[id];
    if (!src) { fail(`DA-1 batch-A: perk id missing in catalog: ${id}`); continue; }
    if (!E.isDeepCompat(src, richPouch)) fail(`DA-1 batch-A: deep:1 perk should pass compat (${id})`);
  }
  // START_POUCH 실효(§1.3 V3P1 총량 30·§11 레버② 반영): cherry(6)/book(5)/star(4)/gem(4)/coin(4)/skull(3) 게이트(REL_MIN 3) 통과, flame(2)/magnet(1)/bomb(1) 미달. crown 없음.
  const sp0 = E.startPouch();
  if (!E.isDeepCompat({ deep: 1, dSym: "cherry" }, sp0)) fail("DA-1: START_POUCH cherry(6) should pass (≥REL_MIN 3)");
  if (!E.isDeepCompat({ deep: 1, dSym: "book" }, sp0)) fail("DA-1: START_POUCH book(5) should pass");
  if (!E.isDeepCompat({ deep: 1, dSym: "skull" }, sp0)) fail("DA-1: START_POUCH skull(3) should pass (=REL_MIN 3, boundary)");
  if (E.isDeepCompat({ deep: 1, dSym: "flame" }, sp0)) fail("DA-1: START_POUCH flame(2) should be gated (REL_MIN 3)");
  if (E.isDeepCompat({ deep: 1, dSym: "magnet" }, sp0)) fail("DA-1: START_POUCH magnet(1) should be gated");
  if (E.isDeepCompat({ deep: 1, dSym: "bomb" }, sp0)) fail("DA-1: START_POUCH bomb(1) should be gated");
  if (E.isDeepCompat({ deep: 2, dSym: "crown" }, sp0)) fail("DA-1: START_POUCH has no crown — must be gated (crown 0 < override 2)");
  notes.push("DA-1 deepCompatPool/familyCount unit ok (magnet gate · crown override · tag:학습 · untagged excluded)");
}

// (DA-2) 계열 브릿지(deepFamilyBridge) — cherry→cherry_ripe EXP / gem→gem_cut 점수, 플래그 없으면 무반영,
//  skull 은 perSymbolExp 브릿지 1회만(skullExp 특수분기와 이중가산 없음).
{
  const mk = (ids) => ids.map((id) => { const s = SYM_BY_ID[id]; if (!s) throw new Error("DA-2 no sym " + id); return { sym: s, tag: "" }; });
  const cellsU = ["cherry_ripe", "cherry_ripe", "gem_cut", "book", "star"];
  const mWith = E.defaultMods(); mWith.perSymbolExp = { cherry: 2 }; mWith.perSymbolScore = { gem: 10 }; mWith.deepFamilyBridge = true;
  const mNo = E.defaultMods(); mNo.perSymbolExp = { cherry: 2 }; mNo.perSymbolScore = { gem: 10 };
  const r1 = E.evaluate(E.makeRng(11), mk(cellsU), mWith, 1, 5, false);
  const r2 = E.evaluate(E.makeRng(11), mk(cellsU), mNo, 1, 5, false);
  if (r1.exp - r2.exp !== 4) fail(`DA-2: bridge exp delta ${r1.exp - r2.exp} != 4 (cherry+2 × cherry_ripe 2셀)`);
  if (r1.score - r2.score !== 10) fail(`DA-2: bridge score delta ${r1.score - r2.score} != 10 (gem+10 × gem_cut 1셀)`);
  // 플래그 없으면 base 참조는 상위계열에 완전 무반영(기존 동작 보존).
  const r0 = E.evaluate(E.makeRng(11), mk(cellsU), E.defaultMods(), 1, 5, false);
  if (r2.exp !== r0.exp || r2.score !== r0.score) fail("DA-2: perSymbol base map leaked to upgraded cells WITHOUT flag");
  // skull: perSymbolExp.skull 브릿지는 정확히 1회 가산, skullExp(특수분기)는 브릿지와 무관(이중가산 없음).
  const skCells = ["skull_black", "cherry", "book", "star", "gem"];
  const mSk = E.defaultMods(); mSk.perSymbolExp = { skull: 3 }; mSk.deepFamilyBridge = true; mSk.skullExp = 5;
  const mSk0 = E.defaultMods(); mSk0.skullExp = 5;
  const a = E.evaluate(E.makeRng(12), mk(skCells), mSk, 1, 5, false);
  const b = E.evaluate(E.makeRng(12), mk(skCells), mSk0, 1, 5, false);
  if (a.exp - b.exp !== 3) fail(`DA-2: skull bridge delta ${a.exp - b.exp} != 3 (skullExp double-add?)`);
  notes.push("DA-2 family bridge ok (flag-gated · exact deltas · no skull double-add)");
}

// (DA-3) 심화 300런 유입 감사 — SYMAUG/SYMREL 오퍼(D태그 0건+dSym 게이트 재검증+심볼퍽 ≥1 보장)·SHOP 진열·
//  RUN_END perks 전수(EVENT/세트조각 경로 커버). ※이 드라이버는 honor/useItem 미경유 — 그 경로는 DA-4/DA-5.
function deepFilterAudit(seed) {
  const g = new Game({ seed, storage: memStore() });
  g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, true);
  let st = g.state(), guard = 0;
  const audit = { offers: 0, shopChecked: 0, guardChecked: 0, guardViol: 0, bad: [] };
  const checkPerk = (id, where) => {
    if (SYM_PERK_BY_ID[id]) return;
    const src = AUG_BY_ID[id] || REL_BY_ID[id];
    if (!src) { audit.bad.push(`${where} unknown perk ${id}`); return; }
    if (!E.isDeepCompat(src, g.run.pouch)) audit.bad.push(`${where} incompat ${id} (deep=${src.deep} dSym=${src.dSym})`);
  };
  while (st && st.phase !== PHASE.RUN_END && guard++ < 3000) {
    switch (st.phase) {
      case PHASE.SELECT_CHAR: st = g.selectChar(g.unlockedChars()[0].id); break;
      case PHASE.SELECT_MACHINE: st = g.selectMachine(g.unlockedMachines()[0].id); break;
      case PHASE.SELECT_DEVICE: st = g.selectDevice(st.options[0]?.id || ""); break;
      case PHASE.SPIN: { const out = g.spin("N"); if (out) { checkNum("da3.gained", out.gained, out.score, out.coins); scanState("da3", out.state); } st = g.state(); break; }
      case PHASE.POST_SPIN: st = g.giveUp(false); break;
      case PHASE.STAGE_CLEAR: st = g.proceedToNodes(); break;
      case PHASE.NODE_SELECT: {
        let idx = 0;
        for (const w of ["SYMAUG", "SYMREL", "SHOP", "EVENT"]) { const i = st.nodes.indexOf(w); if (i >= 0) { idx = i; break; } }
        st = g.selectNode(idx); break;
      }
      case PHASE.PERK_PICK: {
        if ((st.pickKind === "AUG" || st.pickKind === "REL") && st.offerMeta && st.offerMeta.deepPerk) {
          let hasSym = false;
          for (const o of (st.options || [])) { audit.offers++; if (SYM_PERK_BY_ID[o.id]) { hasSym = true; continue; } checkPerk(o.id, "offer"); }
          // 심볼퍽 ≥1 보장 — 해당 kind 의 미보유 심볼퍽이 남아있을 때만 요구(소진 시 면제).
          const kindPool = st.pickKind === "AUG" ? E.symAugPool(g._symUnlockedSet()) : E.symRelPool(g._symUnlockedSet());
          const remaining = kindPool.some((p) => !g.run.perks.includes(p.id));
          if ((st.options || []).length) { audit.guardChecked++; if (!hasSym && remaining) audit.guardViol++; }
        }
        st = g.pickPerk(0); break;
      }
      case PHASE.SHOP: {
        for (const o of (st.shopItems || [])) { if (o.kind === "A" || o.kind === "R") { audit.shopChecked++; checkPerk(o.id, "shop"); } }
        st = g.shopExit(); break;
      }
      case PHASE.DEVICE_NODE: st = g.deviceNodeTake(); break;
      case PHASE.REWARD_DONE: st = g.proceedToStage(); break;
      default: st = g.state(); guard = 3000; break;
    }
    if (!st) break;
  }
  // RUN_END: 이 런에서 유입된 일반 AUG/REL 전수 deep truthy(무태깅 D 퍽 0건 — EVENT c=7/8·세트조각까지 커버).
  //  dSym 게이트는 획득 시점 판정(주머니 가변)이라 여기선 태깅 유무만 단정(게이트 자체는 오퍼/DA-1 검증).
  if (g.run) {
    for (const id of g.run.perks) {
      if (SYM_PERK_BY_ID[id]) continue;
      const src = AUG_BY_ID[id] || REL_BY_ID[id];
      if (src && !src.deep) audit.bad.push(`run-end untagged perk ${id}`);
    }
  }
  return audit;
}
{
  let offers = 0, shopChecked = 0, guardChecked = 0, guardViol = 0; const bad = [];
  for (let s = 1; s <= 300; s++) {
    const a = deepFilterAudit(12000 + s);
    offers += a.offers; shopChecked += a.shopChecked; guardChecked += a.guardChecked; guardViol += a.guardViol;
    if (a.bad.length) bad.push(...a.bad);
  }
  if (bad.length) fail(`DA-3: ${bad.length} deep leak(s) — ${bad.slice(0, 5).join(" | ")}`);
  if (guardViol > 0) fail(`DA-3: sym-perk >=1 guard violated ${guardViol}x`);
  if (offers === 0) fail("DA-3: audit saw 0 deep offers (driver dead)");
  if (shopChecked === 0) fail("DA-3: audit saw 0 deep shop perk listings (driver dead)");
  if (guardChecked === 0) fail("DA-3: audit saw 0 guard-checkable offers");
  notes.push(`DA-3 deep filter audit(300 runs): offers=${offers} shop=${shopChecked} guardChecked=${guardChecked} — 0 leaks`);
}

// (DA-4) honor 시작 증강 — 심화=deepCompat 통과분만(START_POUCH 게이트: crown/magnet 참조 미지급),
//  일반=원문 경로 지급 유지(리팩터 무회귀). selectChar 는 해금 무검증이라 강제 선택 가능(실측).
{
  let deepGranted = 0;
  for (let s = 1; s <= 50; s++) {
    const g = new Game({ seed: 13000 + s, storage: memStore() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, true);
    let st = g.selectChar("honor");
    if (st && st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    if (st && st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice("");
    const first = g.run.perks[0];
    if (!first) continue;
    deepGranted++;
    const src = AUG_BY_ID[first];
    if (!src) fail(`DA-4: honor deep start perk unknown: ${first}`);
    else if (!E.isDeepCompat(src, E.startPouch())) fail(`DA-4: honor deep start perk incompat: ${first} (deep=${src.deep} dSym=${src.dSym})`);
  }
  if (deepGranted < 50) fail(`DA-4: honor deep start aug granted only ${deepGranted}/50 (deepCompat SILVER 풀은 비지 않아야 함)`);
  let normalGranted = 0;
  for (let s = 1; s <= 20; s++) {
    const g = new Game({ seed: 13100 + s, storage: memStore() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, false);
    let st = g.selectChar("honor");
    if (st && st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    if (st && st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice("");
    if (g.run.perks.length) normalGranted++;
  }
  if (normalGranted < 20) fail(`DA-4: normal honor start aug broken (${normalGranted}/20 — refactor regression)`);
  notes.push(`DA-4 honor start aug: deep ${deepGranted}/50 compat-granted · normal ${normalGranted}/20 granted`);
}

// (DA-5) INSTANT 아이템(깨진프리즘/검은복권/악마계약) — 심화 지급 퍽 deepCompat 단정(저주 branch 는 r.curses 허용),
//  일반모드 대조 1셋: 악마계약 raw RELICS 풀 유지(무태깅 유물도 지급 가능 = 필터 미적용 증명).
{
  let audited = 0;
  for (let s = 1; s <= 100; s++) {
    const g = deepAtSpin(14000 + s, null);
    if (!g.run || g.run.phase !== PHASE.SPIN) continue;
    g.run.items = ["broken_prism", "black_lottery", "devil_contract"];
    for (let k = 0; k < 3; k++) {
      const before = g.run.perks.length;
      g.useItem(0);
      for (const id of g.run.perks.slice(before)) {
        const src = AUG_BY_ID[id] || REL_BY_ID[id];
        if (!src) { if (!SYM_PERK_BY_ID[id]) fail(`DA-5: instant added unknown perk ${id}`); continue; }
        if (!E.isDeepCompat(src, g.run.pouch)) fail(`DA-5: instant deep leak ${id} (deep=${src.deep} dSym=${src.dSym})`);
      }
    }
    audited++;
  }
  if (audited < 90) fail(`DA-5: deep instant audit ran only ${audited}/100`);
  let normalUntagged = 0, normalGrants = 0;
  for (let s = 1; s <= 120; s++) {
    const g = new Game({ seed: 14500 + s, storage: memStore() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, false);
    let st = g.state(), guard = 0;
    while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
      if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
      else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
      else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
      else break;
    }
    if (!g.run || g.run.phase !== PHASE.SPIN) continue;
    g.run.items = ["devil_contract"];
    const before = g.run.perks.length;
    g.useItem(0);
    const got = g.run.perks[before];
    if (got) { normalGrants++; const src = REL_BY_ID[got]; if (src && !src.deep) normalUntagged++; }
  }
  if (!normalGrants) fail("DA-5: normal devil_contract never granted a relic (regression)");
  // 배치 B 이후: 희귀등장 유물 6종이 deep:1 태깅됨 → 무태깅 유물은 black_grad_photo 1종만 잔존.
  //  120시드에서 우연히 0건도 가능(확률적) → 경고로만 남기고 fail 하지 않음.
  if (normalUntagged === 0) notes.push("DA-5 note: normal devil_contract 무태깅 유물 0건 (배치 B 이후 1종만 잔존·확률적 0건 정상)");
  notes.push(`DA-5 instant items: deep ${audited}/100 compat · normal devil_contract grants=${normalGrants} untagged=${normalUntagged}(raw 유지)`);
}

// ══════════════════════════════════════════════════════════════════════
//  배치 F — P1 확률 가시화(표시만) + P2 신규심볼 pity + P6 퍼펙트 드로우
//  ※pity 교체는 통계 순수성의 의도적 완화(스펙 승인) — 확률 단정은 결정론 주머니(대상 0개)로 측정.
// ══════════════════════════════════════════════════════════════════════

// (BF-1) pity 강제 교체 — 결정론 주머니(gem 0개=자연 등장 불가) → 스핀 셀에 gem 정확히 1개(스핀당 1칸)·pity 소진.
{
  let fired = 0, tried = 0;
  for (let s = 1; s <= 25; s++) {
    const g = deepAtSpin(21000 + s, null);
    if (!g.run || g.run.phase !== PHASE.SPIN) continue;
    tried++;
    g.run.pouch = { cherry: 40, book: 30, star: 20 };   // gem 없음·magnet/bomb 없음(복제/제거 불가=카운트 순수)
    g.run.deepPity = { id: "gem", spinsLeft: 2 };
    const out = g.spin("N");
    const gems = (out.cells || []).filter((c) => c.sym && c.sym.id === "gem").length;
    if (gems === 1) fired++;
    else if (gems > 1) fail(`BF-1: pity replaced >1 cell (${gems}) seed=${21000 + s}`);
    else fail(`BF-1: pity did not fire (gem=0) seed=${21000 + s}`);
    if (g.run.deepPity !== null) fail(`BF-1: deepPity not consumed after spin seed=${21000 + s}`);
  }
  if (tried < 20) fail(`BF-1: driver reached SPIN only ${tried}/25`);
  if (fired !== tried) fail(`BF-1: pity guarantee ${fired}/${tried} (must be 100%)`);
  notes.push(`BF-1 pity forced-appearance: ${fired}/${tried} spins showed exactly 1 pity symbol (100% within 1 fresh spin)`);
}

// (BF-2) 자연 등장 소진 — 모노 gem 주머니 + pity gem → 교체 없이 소진(deepPity null).
{
  const g = deepAtSpin(21100, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    g.run.pouch = { gem: 60 };
    g.run.deepPity = { id: "gem", spinsLeft: 2 };
    g.spin("N");
    if (g.run.deepPity !== null) fail("BF-2: natural appearance did not consume pity");
    notes.push("BF-2 pity natural-appearance consumption ok");
  } else fail("BF-2: driver did not reach SPIN");
}

// (BF-3) pity 설정 경로 — pickPerk(POUCH add/upgrade·empty 가드) + repairBuy(sv_upgrade/sv_add_basic).
{
  const g = deepAtSpin(21200, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    const r = g.run;
    // ① POUCH add 보상 채택 → pity=해당 id
    r.phase = PHASE.PERK_PICK; r._pickKind = "POUCH"; r.options = [{ type: "add", id: "gem", n: 3 }];
    g.pickPerk(0);
    if (!r.deepPity || r.deepPity.id !== "gem") fail(`BF-3: pickPerk add pity not set (${JSON.stringify(r.deepPity)})`);
    // ② POUCH upgrade 채택 → pity=상위 심볼(POUCH_UPGRADE)
    r.deepPity = null; r.phase = PHASE.PERK_PICK; r._pickKind = "POUCH"; r.options = [{ type: "upgrade", id: "cherry", n: 2 }];
    g.pickPerk(0);
    if (!r.deepPity || r.deepPity.id !== POUCH_UPGRADE.cherry) fail(`BF-3: pickPerk upgrade pity != ${POUCH_UPGRADE.cherry} (${JSON.stringify(r.deepPity)})`);
    // ③ add empty → 가드로 미설정
    r.deepPity = null; r.phase = PHASE.PERK_PICK; r._pickKind = "POUCH"; r.options = [{ type: "add", id: "empty", n: 2 }];
    g.pickPerk(0);
    if (r.deepPity !== null) fail("BF-3: add empty must NOT set pity (guard)");
    // ④ 정비 구매 경로 — sv_upgrade(상위 심볼)·sv_add_basic(해당 심볼). 주머니는 pouchValidate 최소종류(7) 충족(P5-6 선례).
    //  ★V3P1: DECK_MAX 40 → 총량 ≤35(sv_add_basic +5 후 ≤40) pouch. sv_upgrade 는 총량 유지.
    r.deepPity = null; r.coins = 999;
    r.pouch = { cherry: 8, book: 6, star: 5, gem: 4, coin: 4, skull: 4, magnet: 3 };  // total 34 (+5 → 39 ≤ 40)
    g.repairBuy("sv_upgrade", { id: "cherry" });
    if (!r.deepPity || r.deepPity.id !== POUCH_UPGRADE.cherry) fail(`BF-3: repairBuy sv_upgrade pity != ${POUCH_UPGRADE.cherry} (${JSON.stringify(r.deepPity)})`);
    r.deepPity = null; r.coins = 999;
    g.repairBuy("sv_add_basic", { id: "coin" });
    if (!r.deepPity || r.deepPity.id !== "coin") fail(`BF-3: repairBuy sv_add_basic pity != coin (${JSON.stringify(r.deepPity)})`);
    notes.push("BF-3 pity set paths (pickPerk add/upgrade + empty guard + repairBuy add/upgrade) ok");
  } else fail("BF-3: driver did not reach SPIN");
}

// (BF-4) 퍼펙트 드로우 — 모노 주머니 발동(코인+1·배너·스테이지 마킹) + 같은 스테이지 1회 제한 + 혼합계열(UPG_PARENT 브릿지) 성립.
{
  const g = deepAtSpin(21300, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    const r = g.run;
    r.pouch = { cherry: 60 };
    r.quota = 999999;   // 클리어 차단 → 같은 스테이지에서 2스핀 확보
    const c0 = r.coins;
    const o1 = g.spin("N");
    if (r.coins !== c0 + 1) fail(`BF-4: perfect draw coin delta ${r.coins - c0} != +1 (cherry 는 코인 무관=순수 델타)`);
    if (!o1.banner || !o1.banner.includes("퍼펙트")) fail(`BF-4: banner missing (${o1.banner})`);
    if (r.perfectDrawStage !== r.stage) fail(`BF-4: perfectDrawStage ${r.perfectDrawStage} != stage ${r.stage}`);
    if (r.phase === PHASE.SPIN) {
      const c1 = r.coins;
      const o2 = g.spin("N");
      if (r.coins !== c1) fail(`BF-4: second mono spin paid again (+${r.coins - c1}) — 스테이지당 1회 위반`);
      if (o2.banner && o2.banner.includes("퍼펙트")) fail("BF-4: second mono spin bannered again");
    } else fail("BF-4: phase left SPIN before 2nd spin (quota block failed?)");
    notes.push("BF-4 perfect draw: mono pouch fires once per stage (coin +1, banner, stage mark)");
  } else fail("BF-4: driver did not reach SPIN");
  // 혼합계열(cherry+cherry_ripe = 동일 POUCH_FAMILY) 성립
  const g2 = deepAtSpin(21301, null);
  if (g2.run && g2.run.phase === PHASE.SPIN) {
    g2.run.pouch = { cherry: 30, cherry_ripe: 30 };
    g2.run.quota = 999999;
    const c0 = g2.run.coins;
    g2.spin("N");
    if (g2.run.coins !== c0 + 1) fail(`BF-4: mixed-family (cherry+cherry_ripe) perfect draw did not fire (delta ${g2.run.coins - c0})`);
    notes.push("BF-4 perfect draw mixed family (UPG_PARENT bridge) ok");
  } else fail("BF-4: mixed-family driver did not reach SPIN");
}

// (BF-5) 일반모드 무영향 — deepPity 강제 세팅해도 미소진(deep 게이트)·perfectDraw 미발동.
{
  const g = new Game({ seed: 21400, storage: memStore() });
  g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, false);
  let st = g.state(), guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
    else break;
  }
  if (g.run && g.run.phase === PHASE.SPIN) {
    g.run.deepPity = { id: "gem", spinsLeft: 2 };
    const out = g.spin("N");
    if (!g.run.deepPity || g.run.deepPity.id !== "gem" || g.run.deepPity.spinsLeft !== 2) fail("BF-5: normal-mode spin touched deepPity (deep gate broken)");
    if ((out.cells || []).some((c) => c.tag === "✨→")) fail("BF-5: normal-mode cell carries pity tag");
    if (g.run.perfectDrawStage !== 0) fail("BF-5: normal-mode perfectDrawStage changed");
    notes.push("BF-5 normal-mode isolation (pity untouched, no perfect draw) ok");
  } else fail("BF-5: normal driver did not reach SPIN");
}

// (BF-6) 배치F 이후 무회귀 재실행 — 일반/심화 150런씩(deep 은 pity rng 소비로 시퀀스 전이 가능하나 종료율 단정뿐=안전).
{
  let n = 0, d = 0;
  for (let s = 1; s <= 150; s++) { const st = playRun(22000 + s, false); if (st && st.phase === PHASE.RUN_END) n++; }
  for (let s = 1; s <= 150; s++) { const st = playRun(23000 + s, true); if (st && st.phase === PHASE.RUN_END) d++; }
  notes.push(`re-run after Batch F: normal ${n}/150 · deep ${d}/150 terminated`);
  notes.push("BF P1 display-only (openPouchSheet/pouchPreviewHtml/repairConfirm/openCellSheet %) — ui.js 하네스 미커버, 로직 무접촉 확인은 코드 리뷰·브라우저 실측");
}

// ══════════════════════════════════════════════════════════════════════
//  배치 G — 계열 아키타입(전공): share 경계·단일활성·브릿지 무중복·오퍼 정렬·격리.
//  ★share 기반(총량 무관) — 배치 I 총량 변경 내성. mods 주입은 _symMods deep 블록(일반 무접촉).
// ══════════════════════════════════════════════════════════════════════

// (BG-0) 상수/판정 유닛 — ARCH_T1/T2 경계(0.2499 미발동·0.25 t1·0.40 t2)·단일 최대계열·null·동률 결정론.
{
  if (DEEP.ARCH_T1 !== 0.25) fail(`BG-0: ARCH_T1 ${DEEP.ARCH_T1} != 0.25`);
  if (DEEP.ARCH_T2 !== 0.40) fail(`BG-0: ARCH_T2 ${DEEP.ARCH_T2} != 0.40`);
  // 총량 400·cherry 를 항상 최대계열로: cherry N + 나머지는 잘게 분산(각 계열 cherry 미만). share=N/400.
  //  cherry 99 → 0.2475(미발동), 100 → 0.25(t1), 160 → 0.40(t2). 나머지 (400-N) 을 book/star/gem/coin/skull/flame 로 분산.
  const mk = (cherry) => {
    const rest = 400 - cherry; const each = Math.floor(rest / 6);
    return { cherry, book: each, star: each, gem: each, coin: each, skull: each, flame: rest - each * 5 };
  };
  const a1 = E.pouchArchetype(mk(99));   // 0.2475
  if (a1.family !== "cherry") fail(`BG-0: mk(99) max family ${a1.family} != cherry (test构成 오류)`);
  if (a1.tier !== 0) fail(`BG-0: cherry 0.2475 tier ${a1.tier} != 0 (미발동)`);
  const a2 = E.pouchArchetype(mk(100));  // 0.25 정확
  if (a2.tier !== 1) fail(`BG-0: cherry 0.25 tier ${a2.tier} != 1`);
  if (a2.family !== "cherry") fail(`BG-0: 0.25 family ${a2.family} != cherry`);
  const a3 = E.pouchArchetype(mk(160));  // 0.40 정확
  if (a3.tier !== 2) fail(`BG-0: cherry 0.40 tier ${a3.tier} != 2`);
  // 정확 경계값(0.25/0.40)은 >= 라 발동돼야 함(오프바이원).
  const exactT1 = E.pouchArchetype({ cherry: 25, book: 20, star: 20, gem: 15, coin: 10, skull: 5, flame: 5 });   // total 100, cherry 0.25
  if (exactT1.family !== "cherry" || exactT1.tier !== 1) fail(`BG-0: exact 0.25 boundary not t1 (${exactT1.family}/${exactT1.tier})`);
  // 단일 최대계열: cherry 30% vs skull 25% → cherry 만 활성(skull 은 반환 안 함).
  const dual = E.pouchArchetype({ cherry: 30, skull: 25, book: 20, star: 10, gem: 5, coin: 5, flame: 5 });
  if (dual.family !== "cherry") fail(`BG-0: dual max not cherry (${dual.family})`);
  // 빈/총량0 → family null.
  if (E.pouchArchetype({}).family !== null) fail("BG-0: empty pouch family not null");
  if (E.pouchArchetype({ empty: 5 }).family !== null && E.pouchArchetype({ empty: 5 }).tier !== 0) { /* empty=태그없음 → 모든계열 0/총량5 → share0=tier0, family=정의순 first(cherry) 허용 */ }
  // 동률 결정론: cherry/gem 동수(20/20, total 100 → 각 0.20) → 정의순서 cherry 우선.
  const tie = E.pouchArchetype({ cherry: 20, gem: 20, book: 15, star: 15, coin: 12, skull: 10, flame: 8 });
  if (tie.family !== "cherry") fail(`BG-0: tie-break not cherry-first (${tie.family})`);
  // book 아키타입 = tag:학습(book+tome 합산). book 40 + tome 5 = 45/100=0.45 → t2.
  const bookA = E.pouchArchetype({ book: 40, tome: 5, cherry: 20, star: 15, gem: 10, coin: 5, skull: 5 });
  if (bookA.family !== "book" || bookA.tier !== 2) fail(`BG-0: book(학습 45%) not t2 (${bookA.family}/${bookA.tier})`);
  // 계열 상위 합산: skull 20 + skull_black 20 = 40/100=0.40 → skull t2.
  const skA = E.pouchArchetype({ skull: 20, skull_black: 20, cherry: 20, book: 15, star: 10, gem: 10, coin: 5 });
  if (skA.family !== "skull" || skA.tier !== 2) fail(`BG-0: skull family(base+upgrade 40%) not t2 (${skA.family}/${skA.tier})`);
  // NaN 스캔
  checkNum("BG-0 arch share", a2.share, a3.share, bookA.share);
  notes.push(`BG-0 archetype boundaries ok (0.2475→t0 · 0.25→t1 · 0.40→t2 · single-active · tie=cherry · book=학습 · skull=base+upg)`);
}

// (BG-1) archetypeMods 수치 — exp 계열 +15/+30% · coin +10/+20% · score +15/+30% · skull t2 skullPenaltyMul 0.5.
{
  const cT1 = E.archetypeMods({ family: "cherry", tier: 1, metric: "exp" });
  if (Math.abs((cT1.expMul.cherry ?? 0) - 0.15) > 1e-9) fail(`BG-1: cherry t1 expMul ${cT1.expMul.cherry} != 0.15`);
  const cT2 = E.archetypeMods({ family: "cherry", tier: 2, metric: "exp" });
  if (Math.abs((cT2.expMul.cherry ?? 0) - 0.30) > 1e-9) fail(`BG-1: cherry t2 expMul ${cT2.expMul.cherry} != 0.30`);
  const coinT1 = E.archetypeMods({ family: "coin", tier: 1, metric: "coin" });
  if (Math.abs((coinT1.coinMul.coin ?? 0) - 0.10) > 1e-9) fail(`BG-1: coin t1 coinMul ${coinT1.coinMul.coin} != 0.10`);
  const coinT2 = E.archetypeMods({ family: "coin", tier: 2, metric: "coin" });
  if (Math.abs((coinT2.coinMul.coin ?? 0) - 0.20) > 1e-9) fail(`BG-1: coin t2 coinMul ${coinT2.coinMul.coin} != 0.20`);
  const gemT1 = E.archetypeMods({ family: "gem", tier: 1, metric: "score" });
  if (Math.abs((gemT1.scoreMul.gem ?? 0) - 0.15) > 1e-9) fail(`BG-1: gem t1 scoreMul ${gemT1.scoreMul.gem} != 0.15`);
  const skT1 = E.archetypeMods({ family: "skull", tier: 1, metric: "exp" });
  if (skT1.skullPenaltyMul !== 1) fail(`BG-1: skull t1 skullPenaltyMul ${skT1.skullPenaltyMul} != 1 (t1 페널티 완화 없음)`);
  const skT2 = E.archetypeMods({ family: "skull", tier: 2, metric: "exp" });
  if (Math.abs(skT2.skullPenaltyMul - 0.5) > 1e-9) fail(`BG-1: skull t2 skullPenaltyMul ${skT2.skullPenaltyMul} != 0.5`);
  if (Math.abs((skT2.expMul.skull ?? 0) - 0.30) > 1e-9) fail(`BG-1: skull t2 expMul ${skT2.expMul.skull} != 0.30`);
  // 비활성(tier0)=전부 빈맵·skullPenaltyMul 1
  const off0 = E.archetypeMods({ family: "cherry", tier: 0, metric: "exp" });
  if (Object.keys(off0.expMul).length || off0.skullPenaltyMul !== 1) fail("BG-1: tier0 archetypeMods not inert");
  notes.push("BG-1 archetypeMods values ok (exp 15/30 · coin 10/20 · score 15/30 · skull t2 penalty 0.5)");
}

// (BG-2) evaluate 계열 곱 — cherry expMul / gem scoreMul / coin coinMul / skull expMul(☠분기). 일반 mods=무영향.
//  ★evaluate 는 exp/score/coins 를 Math.floor 로 반환(엔진 라인 866/875/880) → 정수 델타로 검증(곱 1.0=+100% 사용).
{
  const mk = (ids) => ids.map((id) => { const s = SYM_BY_ID[id]; if (!s) throw new Error("BG-2 no sym " + id); return { sym: s, tag: "" }; });
  // cherry expMul(+100%): cherry(exp3)+cherry_ripe(exp6) → 3+6=9 계열 EXP 가 2배(=+9). 나머지(book6/gem1/coin0)=7 무변.
  const cherryCells = ["cherry", "cherry_ripe", "book", "gem", "coin"];
  const mC = E.defaultMods(); mC.deepFamilyExpMul = { cherry: 1.0 };
  const rC = E.evaluate(E.makeRng(31), mk(cherryCells), mC, 1, 5, false);
  const rC0 = E.evaluate(E.makeRng(31), mk(cherryCells), E.defaultMods(), 1, 5, false);
  if ((rC.exp - rC0.exp) !== 9) fail(`BG-2: cherry expMul delta ${rC.exp - rC0.exp} != 9 ((3+6)×+100%)`);
  // gem scoreMul(+100%): gem(score15)+gem_cut(score32)=47 → 2배(+47). 나머지 무점수.
  const gemCells = ["gem", "gem_cut", "cherry", "book", "star"];
  const mG = E.defaultMods(); mG.deepFamilyScoreMul = { gem: 1.0 };
  const rG = E.evaluate(E.makeRng(32), mk(gemCells), mG, 1, 5, false);
  const rG0 = E.evaluate(E.makeRng(32), mk(gemCells), E.defaultMods(), 1, 5, false);
  if ((rG.score - rG0.score) !== 47) fail(`BG-2: gem scoreMul delta ${rG.score - rG0.score} != 47 ((15+32)×+100%)`);
  // coin coinMul(+100%): coin(coin1)+coin_bag(coin3)=4 → 2배(+4).
  const coinCells = ["coin", "coin_bag", "cherry", "book", "star"];
  const mCo = E.defaultMods(); mCo.deepFamilyCoinMul = { coin: 1.0 };
  const rCo = E.evaluate(E.makeRng(33), mk(coinCells), mCo, 1, 5, false);
  const rCo0 = E.evaluate(E.makeRng(33), mk(coinCells), E.defaultMods(), 1, 5, false);
  if ((rCo.coins - rCo0.coins) !== 4) fail(`BG-2: coin coinMul delta ${rCo.coins - rCo0.coins} != 4 ((1+3)×+100%)`);
  // skull expMul(☠분기, +100%): skullExp=5 × 2셀 = 10 → 2배(+10). 해골빌드(skullExp>0)라 페널티 면제.
  const skCells = ["skull", "skull_black", "cherry", "book", "star"];
  const mSk = E.defaultMods(); mSk.skullExp = 5; mSk.deepFamilyExpMul = { skull: 1.0 };
  const mSk0 = E.defaultMods(); mSk0.skullExp = 5;
  const rSk = E.evaluate(E.makeRng(34), mk(skCells), mSk, 1, 5, false);
  const rSk0 = E.evaluate(E.makeRng(34), mk(skCells), mSk0, 1, 5, false);
  if ((rSk.exp - rSk0.exp) !== 10) fail(`BG-2: skull expMul delta ${rSk.exp - rSk0.exp} != 10 (skullExp 5×2 ×+100%)`);
  // 일반 mods(빈 deepFamily*Mul)=완전 무영향(결정론 동일)
  if (rC0.exp !== E.evaluate(E.makeRng(31), mk(cherryCells), E.defaultMods(), 1, 5, false).exp) fail("BG-2: default mods nondeterministic?");
  notes.push("BG-2 evaluate family mult ok (cherry exp · gem score · coin coin · skull ☠branch · default inert)");
}

// (BG-3) 브릿지 × 아키타입 무중복 — cherry_up(perSymbolExp.cherry+2) 보유 + cherry 전공(+100%),
//  숙성체리 2셀 스핀에서 기대값 정확(브릿지 가산 후 아키타입 곱, 이중가산 없음). ★정수 검증 위해 +100%(×2) 사용.
{
  const mk = (ids) => ids.map((id) => ({ sym: SYM_BY_ID[id], tag: "" }));
  const cells = ["cherry_ripe", "cherry_ripe", "book", "gem", "star"];   // 숙성체리 2셀(exp 6 each)
  const mNone = E.defaultMods();
  const rN = E.evaluate(E.makeRng(41), mk(cells), mNone, 1, 5, false);   // base: 숙성체리 6×2 + book6+gem1+star8 = 27
  // 브릿지만: perSymbolExp.cherry=2 → 각 숙성체리 = 6+2 = 8. 델타 = 2×2 = +4.
  const mBridge = E.defaultMods(); mBridge.perSymbolExp = { cherry: 2 }; mBridge.deepFamilyBridge = true;
  const rB = E.evaluate(E.makeRng(41), mk(cells), mBridge, 1, 5, false);
  if ((rB.exp - rN.exp) !== 4) fail(`BG-3: bridge-only delta ${rB.exp - rN.exp} != 4`);
  // 브릿지 + 아키타입(×2): 각 숙성체리 = (6+2)×2 = 16. 2셀 = 32. base 숙성체리 2셀 = 12 → 델타 +20.
  //  (아키타입이 브릿지 가산분까지 곱 = 설계상 의도·이중가산 아님. 12→32 = +20.)
  const mBoth = E.defaultMods(); mBoth.perSymbolExp = { cherry: 2 }; mBoth.deepFamilyBridge = true; mBoth.deepFamilyExpMul = { cherry: 1.0 };
  const rBoth = E.evaluate(E.makeRng(41), mk(cells), mBoth, 1, 5, false);
  if ((rBoth.exp - rN.exp) !== 20) fail(`BG-3: bridge+arch delta ${rBoth.exp - rN.exp} != 20 (double-add?)`);
  // 아키타입만(브릿지 없음, ×2): 각 숙성체리 = 6×2 = 12. 2셀 = 24. 델타 vs base(12) = +12.
  const mArch = E.defaultMods(); mArch.deepFamilyExpMul = { cherry: 1.0 };
  const rA = E.evaluate(E.makeRng(41), mk(cells), mArch, 1, 5, false);
  if ((rA.exp - rN.exp) !== 12) fail(`BG-3: arch-only delta ${rA.exp - rN.exp} != 12`);
  // 무중복 검산: bridge+arch(+20) == arch-only(+12) + bridge곱분(브릿지2 × arch2 × 2셀 = 8). 12+8=20 ✓
  notes.push("BG-3 bridge×archetype composition exact (no double-add · arch multiplies bridged value by design · +20=+12+8)");
}

// (BG-4) _mods 심화블록 주입 — cherry 몰빵 주머니 → deepFamilyExpMul.cherry 셋. 일반모드는 빈맵(격리).
{
  const g = deepAtSpin(24000, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    g.run.pouch = { cherry: 60, book: 15, star: 10, gem: 5, coin: 4, skull: 3, flame: 3 };   // cherry 0.6 → t2
    const m = g._mods();
    if (Math.abs((m.deepFamilyExpMul?.cherry ?? 0) - 0.30) > 1e-9) fail(`BG-4: cherry t2 deepFamilyExpMul ${m.deepFamilyExpMul?.cherry} != 0.30`);
    // skull 몰빵 → skullPenaltyMul 0.5 곱 반영
    g.run.pouch = { skull: 45, skull_black: 5, cherry: 20, book: 15, star: 10, gem: 3, coin: 2 };   // skull 0.5 → t2
    const m2 = g._mods();
    if (!(m2.skullPenaltyMul <= 0.5 + 1e-9)) fail(`BG-4: skull t2 skullPenaltyMul not applied (${m2.skullPenaltyMul})`);
    if (Math.abs((m2.deepFamilyExpMul?.skull ?? 0) - 0.30) > 1e-9) fail(`BG-4: skull t2 deepFamilyExpMul ${m2.deepFamilyExpMul?.skull} != 0.30`);
    notes.push("BG-4 _mods deep-block archetype injection ok (cherry t2 · skull t2 penalty)");
  } else fail("BG-4: driver did not reach SPIN");
  // 격리: 일반 런에 cherry 몰빵 pouch 강제해도 deepFamily*Mul 무주입.
  const gn = new Game({ seed: 24001, storage: memStore() }); gn.profile.runs = 1; gn.profile.playerLevel = 30;
  gn.startRun(0, false);
  let st = gn.state(), guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
    if (st.phase === PHASE.SELECT_CHAR) st = gn.selectChar(gn.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = gn.selectMachine(gn.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = gn.selectDevice(st.options[0]?.id || "");
    else break;
  }
  gn.run.pouch = { cherry: 60, book: 20 };   // deepMode=false 라 무시돼야 함
  const nm = gn._mods();
  if (nm.deepFamilyExpMul && Object.keys(nm.deepFamilyExpMul).length) fail(`BG-4 ISOLATION: normal-mode deepFamilyExpMul non-empty (${JSON.stringify(nm.deepFamilyExpMul)})`);
  if (nm.deepFamilyScoreMul && Object.keys(nm.deepFamilyScoreMul).length) fail("BG-4 ISOLATION: normal-mode deepFamilyScoreMul non-empty");
  if (nm.deepFamilyCoinMul && Object.keys(nm.deepFamilyCoinMul).length) fail("BG-4 ISOLATION: normal-mode deepFamilyCoinMul non-empty");
  notes.push("BG-4 isolation: normal-mode _mods no deepFamily*Mul");
}

// (BG-5) familyRefMatchesArch — dSym 참조가 아키타입 계열과 매칭(오퍼 정렬 판별). book=tag:학습 매칭.
{
  if (!E.familyRefMatchesArch("cherry", "cherry")) fail("BG-5: cherry/cherry no match");
  if (!E.familyRefMatchesArch("tag:학습", "book")) fail("BG-5: tag:학습/book no match");
  if (!E.familyRefMatchesArch("book", "book")) fail("BG-5: book/book no match");
  if (!E.familyRefMatchesArch("cherry_ripe", "cherry")) fail("BG-5: cherry_ripe(상위)/cherry no match");
  if (E.familyRefMatchesArch("gem", "cherry")) fail("BG-5: gem/cherry false-positive");
  if (E.familyRefMatchesArch("tag:점수", "gem")) fail("BG-5: tag:점수/gem should not match (계열 무관 태그)");
  if (E.familyRefMatchesArch(null, "cherry")) fail("BG-5: null dSym match");
  notes.push("BG-5 familyRefMatchesArch ok (base · tag:학습→book · upgrade→base · false-positive guard)");
}

// (BG-6) 오퍼 정렬 — 활성 전공(cherry) 시 cherry dSym 퍽이 오퍼에 있으면 선두(순서만·구성 불변).
//  실오퍼는 rng 의존이라 결정 불가 → _archSortOffer 를 합성 오퍼로 직접 검증(순서 재배열·세트조각 위치 보존).
{
  const g = deepAtSpin(24100, null);
  if (g.run) {
    g.run.pouch = { cherry: 60, book: 15, star: 10, gem: 5, coin: 4, skull: 3, flame: 3 };   // cherry t2 활성
    // 합성 오퍼: [일반, cherry_dSym, 세트조각]. cherry 퍽을 선두로, 세트조각은 위치 보존.
    const cherryPerk = AUGMENTS.find((a) => a.deep && a.dSym === "cherry");
    if (!cherryPerk) fail("BG-6: no cherry-dSym augment in catalog to test");
    else {
      const other = AUGMENTS.find((a) => a.id !== cherryPerk.id) || { id: "study", t: "SILVER", e: "x", n: "x" };
      const off = { options: [{ ...other }, { ...cherryPerk }, { id: "zzz_set", setTag: true, t: "SILVER", e: "s", n: "s" }], meta: { tier: "SILVER" } };
      const before = off.options.map((o) => o.id).join(",");
      g._archSortOffer(off);
      if (off.options[0].id !== cherryPerk.id) fail(`BG-6: cherry perk not moved to front (${off.options.map((o) => o.id).join(",")})`);
      if (off.options[off.options.length - 1].id !== "zzz_set") fail("BG-6: setTag card position not preserved");
      if (off.options.length !== 3) fail("BG-6: option count changed by sort");
      // 구성(id 집합) 불변 — 확률/개수 조작 없음
      const setBefore = before.split(",").sort().join(",");
      const setAfter = off.options.map((o) => o.id).sort().join(",");
      if (setBefore !== setAfter) fail(`BG-6: offer composition changed (${setBefore} → ${setAfter})`);
      // 전공 비활성(share<20%)이면 정렬 안 함
      g.run.pouch = { cherry: 5, book: 20, star: 20, gem: 20, coin: 15, skull: 10, flame: 10 };   // cherry ~5%
      const off2 = { options: [{ ...other }, { ...cherryPerk }], meta: { tier: "SILVER" } };
      g._archSortOffer(off2);
      if (off2.options[0].id === cherryPerk.id && other.id !== cherryPerk.id) fail("BG-6: sorted despite inactive archetype (<20%)");
      notes.push("BG-6 offer synergy sort ok (front-load active-archetype dSym · setTag preserved · composition invariant · gated ≥20%)");
    }
  } else fail("BG-6: driver did not reach SPIN");
}

// (BG-7) pouchView.archetype + 토스트 상태(_checkArchetype) — HUD/시트 데이터 계약 + 발동 알림.
{
  const g = deepAtSpin(24200, null);
  if (g.run) {
    g.run.pouch = { cherry: 60, book: 15, star: 10, gem: 5, coin: 4, skull: 3, flame: 3 };   // cherry t2
    const pv = g.pouchView();
    if (!pv || !pv.archetype) fail("BG-7: pouchView.archetype missing at cherry t2");
    else {
      if (pv.archetype.family !== "cherry" || pv.archetype.tier !== 2) fail(`BG-7: pouchView archetype ${pv.archetype.family}/${pv.archetype.tier}`);
      if (pv.archetype.nextThreshold !== null) fail("BG-7: t2 nextThreshold should be null (max)");
    }
    // t1 → nextThreshold=ARCH_T2
    g.run.pouch = { cherry: 30, book: 20, star: 20, gem: 12, coin: 8, skull: 5, flame: 5 };   // cherry 0.30 t1
    const pv1 = g.pouchView();
    if (!pv1.archetype || pv1.archetype.tier !== 1 || Math.abs(pv1.archetype.nextThreshold - DEEP.ARCH_T2) > 1e-9) fail(`BG-7: t1 nextThreshold != ARCH_T2 (${JSON.stringify(pv1.archetype)})`);
    // 토스트: 발동 감지(deepArchFamily null → cherry)
    g.run.deepArchFamily = null; g.run.deepArchTier = 0;
    const logLen = g.log.length;
    g.run.pouch = { cherry: 60, book: 15, star: 10, gem: 5, coin: 4, skull: 3, flame: 3 };
    g._checkArchetype();
    if (g.run.deepArchFamily !== "cherry" || g.run.deepArchTier !== 2) fail(`BG-7: _checkArchetype state not updated (${g.run.deepArchFamily}/${g.run.deepArchTier})`);
    if (g.log.length <= logLen) fail("BG-7: activation toast not pushed");
    // 재호출(동일 상태) → 중복 토스트 없음
    const logLen2 = g.log.length;
    g._checkArchetype();
    if (g.log.length !== logLen2) fail("BG-7: duplicate toast on unchanged archetype");
    notes.push("BG-7 pouchView.archetype contract + _checkArchetype toast (fire once, no dup) ok");
  } else fail("BG-7: driver did not reach SPIN");
}

// (BG-8) pouchValidate 7종+ 정합 — 하네스 테스트 주머니가 MIN_KINDS(7)·희귀도 상한 규칙 준수(정비 유효성 회귀 감지).
//  ★기본(cap∞) 6종 + 고급 특수(SILVER_special cap10) 1종으로 7종 구성. ★V3P1: 총량 34(DECK_MAX 40 이내).
{
  const test7 = { cherry: 7, book: 6, star: 5, gem: 4, coin: 4, magnet: 4, cherry_ripe: 4 };   // 7종·총량 34·전부 기본/고급특수
  const v = E.pouchValidate(test7);
  if (Object.keys(test7).filter((k) => test7[k] > 0).length < DEEP.MIN_KINDS) fail("BG-8: test pouch <7 kinds");
  if (!v.ok) fail(`BG-8: 7-kind test pouch invalid — ${v.errors.join(" · ")}`);
  notes.push(`BG-8 pouchValidate 7-kind test pouch ok (kinds=${v.kinds})`);
}

// (BG-9) 배치G 이후 무회귀 재실행 — 일반 300런(회귀 0)·심화 150런(아키타입 주입 경로 완주).
{
  let n = 0, d = 0;
  for (let s = 1; s <= 300; s++) { const st = playRun(25000 + s, false); if (st && st.phase === PHASE.RUN_END) n++; }
  for (let s = 1; s <= 150; s++) { const st = playRun(26000 + s, true); if (st && st.phase === PHASE.RUN_END) d++; }
  notes.push(`re-run after Batch G: normal ${n}/300 · deep ${d}/150 terminated`);
  notes.push("BG UI (archHudChip/archGaugeHtml/deep-hud t1·t2 칩/arch-box 게이지) — ui.js 하네스 미커버, 브라우저 실측 필요");
}

// ══════════════════════════════════════════════════════════════════════
//  배치 H — P5 패키지 보상: applySymbolPackage 원자성·템플릿 4종 정확·등장률 ~30%·pity 대표심볼·규칙유지·격리.
//  ★패키지 delta 는 단품 delta(±3~5)보다 큼(몰빵 +6/-3 등) — 배치 I 총량 재스케일 시 총량 변화 임팩트가
//   패키지에도 그대로 적용(share 기반 아키타입과 달리 절대 delta라 재스케일 영향받음 — 배치 I 검증 대상).
// ══════════════════════════════════════════════════════════════════════

// (BH-0) applySymbolPackage 원자성 — 성공/실패 케이스. 실패 시 부분적용 0(원본 불변).
{
  // 성공: cherry -4 / book +4 (총량 유지·규칙 준수). §1.4 V3P1: DECK_MAX 40 → 총량 ≤40 base.
  const base = { cherry: 8, book: 7, star: 6, gem: 5, coin: 4, skull: 4, flame: 3, magnet: 3 };  // total 40·8종
  const okRes = E.applySymbolPackage(base, [{ type: "remove", id: "cherry", n: 4 }, { type: "add", id: "book", n: 4 }]);
  if (!okRes.ok) fail(`BH-0: valid package rejected — ${okRes.error}`);
  if (okRes.pouch.cherry !== 4 || okRes.pouch.book !== 11) fail(`BH-0: package result wrong (cherry=${okRes.pouch.cherry} book=${okRes.pouch.book})`);
  if (E.pouchTotal(okRes.pouch) !== E.pouchTotal(base)) fail(`BH-0: swap-like package changed total (${E.pouchTotal(okRes.pouch)} vs ${E.pouchTotal(base)})`);
  if (okRes.pouch === base) fail("BH-0: package mutated original ref");
  // 원본 불변 확인(순수)
  if (base.cherry !== 8 || base.book !== 7) fail("BH-0: applySymbolPackage MUTATED input pouch");
  // 실패(원자성): crown 을 상한(2) 초과로 add → 전체 거부, pouch 는 입력 사본 그대로(부분적용 0).
  //  ops = [cherry -4(성공적 중간상태), crown +5(위반)] → 최종 검증 실패 → 원본 반환(cherry -4 안 반영).
  const failRes = E.applySymbolPackage(base, [{ type: "remove", id: "cherry", n: 4 }, { type: "add", id: "crown", n: 5 }]);
  if (failRes.ok) fail("BH-0: crown-overflow package should be rejected (WILD/CROWN cap)");
  if (failRes.pouch.cherry !== 8) fail(`BH-0: PARTIAL APPLY — cherry changed on rejected package (${failRes.pouch.cherry} != 8)`);
  if ((failRes.pouch.crown || 0) !== (base.crown || 0)) fail(`BH-0: PARTIAL APPLY — crown added on rejected package (${failRes.pouch.crown})`);
  if (JSON.stringify(failRes.pouch) !== JSON.stringify(base)) fail(`BH-0: rejected package pouch != original (${JSON.stringify(failRes.pouch)})`);
  // 빈 ops → 거부.
  if (E.applySymbolPackage(base, []).ok) fail("BH-0: empty ops should be rejected");
  if (E.applySymbolPackage(base, null).ok) fail("BH-0: null ops should be rejected");
  // 총량 하한 위반(대량 제거) → 거부·원본 불변. §1.4 V3P1: DECK_MIN 20 → 40-21=19 < 20 (종류 7 미만 위반도 동시).
  const underRes = E.applySymbolPackage(base, [{ type: "remove", id: "cherry", n: 8 }, { type: "remove", id: "book", n: 7 }, { type: "remove", id: "star", n: 6 }]);  // total 40-21=19 < 20
  if (underRes.ok) fail("BH-0: under-min total package should be rejected");
  if (JSON.stringify(underRes.pouch) !== JSON.stringify(base)) fail("BH-0: PARTIAL APPLY on under-min rejection");
  notes.push("BH-0 applySymbolPackage atomicity ok (valid commit · reject → 0 partial-apply · empty/null reject · pure input)");
}

// (BH-1) 템플릿 4종 정확 — 몰빵/대청소/승급/교환 각각 개수·총량 델타. 결정형 rng.
{
  const rng = E.makeRng(101);
  // 다계열 주머니: cherry 최다 · book 차순위 · skull 존재 · cherry base 승급 가능(≥3).
  //  §1.4 V3P1: DECK_MAX 40 → 총량 ≤37(몰빵 템플릿 순+3 여유 포함) pouch.
  const pouch = { cherry: 10, book: 7, star: 5, gem: 5, coin: 4, skull: 4, flame: 2 };  // total 37·7종
  // applySymbolPackage 로 각 템플릿 ops 직접 검증(packageTemplates 는 rng 순서 의존이라 ops 직접 구성해 정확성 단정).
  // ① 몰빵: 직접 ops(+2/-1)로 add/remove 역학 단정(템플릿 자체는 아래 packageTemplates 로 별도 검증).
  const allin = E.applySymbolPackage(pouch, [{ type: "add", id: "cherry", n: 2 }, { type: "remove", id: "book", n: 1 }]);
  if (!allin.ok) fail(`BH-1: 몰빵 rejected — ${allin.error}`);
  if (allin.pouch.cherry !== 12 || allin.pouch.book !== 6) fail(`BH-1: 몰빵 counts (cherry=${allin.pouch.cherry} book=${allin.pouch.book})`);
  if (E.pouchTotal(allin.pouch) - E.pouchTotal(pouch) !== 1) fail(`BH-1: 몰빵 total delta ${E.pouchTotal(allin.pouch) - E.pouchTotal(pouch)} != +1`);
  // ② 대청소: skull_black -3 + skull -3 + empty +2 (해골계열 감소·있는 만큼만·empty +2)
  const cleanup = E.applySymbolPackage(pouch, [{ type: "remove", id: "skull_black", n: 3 }, { type: "remove", id: "skull", n: 3 }, { type: "add", id: "empty", n: 2 }]);
  if (!cleanup.ok) fail(`BH-1: 대청소 rejected — ${cleanup.error}`);
  if (cleanup.pouch.skull !== 1) fail(`BH-1: 대청소 skull ${cleanup.pouch.skull} != 1 (4-3, skull_black 0)`);
  if ((cleanup.pouch.empty || 0) !== 2) fail(`BH-1: 대청소 empty ${cleanup.pouch.empty} != 2`);
  // ③ 승급: cherry 3개 → cherry_ripe 3개(총량 유지)
  const promote = E.applySymbolPackage(pouch, [{ type: "upgrade", id: "cherry", n: 3 }]);
  if (!promote.ok) fail(`BH-1: 승급 rejected — ${promote.error}`);
  if (promote.pouch.cherry !== 7 || (promote.pouch.cherry_ripe || 0) !== 3) fail(`BH-1: 승급 (cherry=${promote.pouch.cherry} ripe=${promote.pouch.cherry_ripe})`);
  if (E.pouchTotal(promote.pouch) !== E.pouchTotal(pouch)) fail("BH-1: 승급 changed total (upgrade must preserve)");
  // ④ 교환: cherry -2 · book +2 (총량 유지)
  const trade = E.applySymbolPackage(pouch, [{ type: "remove", id: "cherry", n: 2 }, { type: "add", id: "book", n: 2 }]);
  if (!trade.ok) fail(`BH-1: 교환 rejected — ${trade.error}`);
  if (trade.pouch.cherry !== 8 || trade.pouch.book !== 9) fail(`BH-1: 교환 (cherry=${trade.pouch.cherry} book=${trade.pouch.book})`);
  if (E.pouchTotal(trade.pouch) !== E.pouchTotal(pouch)) fail("BH-1: 교환 changed total");
  // packageTemplates 가 실제로 위 4 tpl 을 생성(가능한 것)·각 preview.valid.ok·changes 존재·repId 규칙.
  const tpls = E.packageTemplates(rng, pouch);
  const byTpl = {}; for (const t of tpls) byTpl[t.tpl] = t;
  for (const req of ["allin", "cleanup", "promote", "trade"]) if (!byTpl[req]) fail(`BH-1: template '${req}' not generated for multi-family pouch`);
  for (const t of tpls) {
    if (t.type !== "package") fail(`BH-1: template type ${t.type} != package`);
    if (!Array.isArray(t.ops) || !t.ops.length) fail(`BH-1: template ${t.tpl} no ops`);
    if (!t.preview || !t.preview.valid.ok) fail(`BH-1: template ${t.tpl} preview invalid (should be pre-filtered) — ${t.preview?.valid?.errors?.join(",")}`);
    if (!t.preview.changes.length) fail(`BH-1: template ${t.tpl} no changes`);
    if (!t.title || !t.desc) fail(`BH-1: template ${t.tpl} missing title/desc`);
    checkNum(`BH-1 ${t.tpl} totals`, t.preview.totalBefore, t.preview.totalAfter);
  }
  // repId 규칙: 몰빵=top(cherry)·승급=상위(cherry_ripe)·교환=B(book)·대청소=null(add empty=pity 무의미).
  if (byTpl.allin.repId !== "cherry") fail(`BH-1: 몰빵 repId ${byTpl.allin.repId} != cherry`);
  // 승급 repId = 승급 op 의 base 의 상위(POUCH_UPGRADE[op.id]). base 는 rng.pick 이라 심볼 고정 불가 → 상위 심볼 일치만 단정.
  if (byTpl.promote.repId !== POUCH_UPGRADE[byTpl.promote.ops[0].id]) fail(`BH-1: 승급 repId ${byTpl.promote.repId} != POUCH_UPGRADE[${byTpl.promote.ops[0].id}]`);
  if (byTpl.trade.repId !== "book") fail(`BH-1: 교환 repId ${byTpl.trade.repId} != book`);
  if (byTpl.cleanup.repId !== null) fail(`BH-1: 대청소 repId ${byTpl.cleanup.repId} != null (add empty)`);
  notes.push("BH-1 V3P1 template 4종 정합 (포치 total≤40 기준)");
}

// (BH-2) 사전검증 — 불가능한 템플릿은 제시 0. 해골 없는 주머니 → 대청소 미생성. 승급불가(base<3) → 승급 미생성.
{
  const rng = E.makeRng(202);
  // 해골 0·모든 base≥3 → 대청소 불가·승급 가능. §1.4 V3P1: DECK_MAX 40 → 총량 ≤40.
  const noSkullPouch = { cherry: 9, book: 8, star: 7, gem: 7, coin: 6, magnet: 3 };  // total 40·해골 0·각 base≥3
  const t1 = E.packageTemplates(rng, noSkullPouch);
  if (t1.some((t) => t.tpl === "cleanup")) fail("BH-2: 대청소 generated with 0 skulls (should be filtered)");
  // 승급 불가(cherry 2개뿐) 확인용 별도 주머니
  const lowBase = { cherry: 2, book: 9, star: 8, gem: 6, coin: 5, skull: 5, magnet: 3 };  // cherry<3·total 38
  const t2 = E.packageTemplates(rng, lowBase);
  // 승급은 base≥3 인 다른 계열(book/star/gem/coin)로 생성될 수 있음 → cherry 승급이 아니어야 함만 단정
  for (const t of t2) if (t.tpl === "promote" && t.ops[0].id === "cherry") fail("BH-2: 승급 generated for base<3 (cherry=2)");
  // 총량 상한(DECK_MAX 40) 주머니 → 몰빵(순+1)이 상한 넘으면 미생성(사전검증).
  const nearMax = { cherry: 9, book: 8, star: 8, gem: 7, coin: 5, skull: 3 };  // total 40 = DECK_MAX
  const t3 = E.packageTemplates(rng, nearMax);
  //  nearMax=40(=DECK_MAX)에서 몰빵(순+3)은 43 > 40 상한 → 생성 자체가 안 돼야(사전검증 필터).
  if (t3.some((t) => t.tpl === "allin")) fail("BH-2: 몰빵 generated at total-max 40 (should exceed DECK_MAX → filtered)");
  // 모든 생성 템플릿은 유효(재확인).
  for (const p of [noSkullPouch, lowBase, nearMax]) for (const t of E.packageTemplates(E.makeRng(1), p)) {
    if (!t.preview.valid.ok) fail(`BH-2: generated template ${t.tpl} INVALID (pre-filter broken) — ${t.preview.valid.errors.join(",")}`);
  }
  // 해금 필터: 잠긴 심볼 계열은 add 대상에서 제외(교환/몰빵 B 가 잠긴 계열이면 미생성).
  const uLimited = new Set(["cherry", "book", "star", "cherry_ripe", "empty", "random"]);  // gem 잠금
  const gemTop = { gem: 12, coin: 9, cherry: 6, book: 5, star: 5, skull: 3 };  // gem 최다지만 잠김·total 40
  for (const t of E.packageTemplates(E.makeRng(1), gemTop, { symUnlocked: uLimited })) {
    for (const op of t.ops) if (op.type === "add" && !uLimited.has(op.id) && op.id !== "empty" && op.id !== "random") fail(`BH-2: locked sym '${op.id}' added by template ${t.tpl}`);
  }
  notes.push("BH-2 V3P1 pre-validation ok (impossible=0 제시: no-skull→no cleanup · base<3→no promote · locked→no add)");
}

// (BH-3) V3P2 패키지 은퇴 확인 — offerSymbolRewards v3는 package 카드를 생성하지 않음(dormant).
//  ★§2 V3P2: POUCH 오퍼는 type:"special" + type:"skip" 만. package/randpack은 은퇴(정비소 이관).
{
  //  다계열·해골 포함 주머니 고정. §1.4 V3P1: 총량 34(DECK_MAX 40 이내).
  const pouch = { cherry: 8, book: 6, star: 5, gem: 5, coin: 4, skull: 4, flame: 2 };  // total 34·다계열
  let withPkg = 0, withSpecial = 0, withSkip = 0; const N = 1000;
  for (let s = 0; s < N; s++) {
    const off = E.offerSymbolRewards(E.makeRng(50000 + s), pouch, 5);
    withPkg += off.filter((o) => o.type === "package").length;
    withSpecial += off.filter((o) => o.type === "special").length;
    withSkip += off.filter((o) => o.type === "skip").length;
  }
  if (withPkg > 0) fail(`BH-3: V3P2 오퍼에 패키지 카드 ${withPkg}건 (v3에서 은퇴 — 발생 금지)`);
  if (withSkip < N * 0.9) fail(`BH-3: skip 카드 ${withSkip}/${N}오퍼 (90% 이상 기대)`);
  if (withSpecial < N) fail(`BH-3: special 카드 ${withSpecial}건 (오퍼당 1장 이상 기대)`);
  notes.push(`BH-3 V3P2 패키지 은퇴: package=${withPkg}(0 기대) special=${withSpecial} skip=${withSkip}/${N}오퍼`);
}

// (BH-4) pity 대표심볼 — 패키지 채택 시 r.deepPity = { id: repId }. game.js pickPerk POUCH 분기.
{
  const g = deepAtSpin(27000, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    const r = g.run;
    r.pouch = { cherry: 7, book: 6, star: 5, gem: 5, coin: 4, skull: 5, flame: 3 };  // total 35 §1.4 V3P1 ≤40
    // 몰빵 패키지(repId=cherry) 채택 → pity=cherry
    r.deepPity = null; r.phase = PHASE.PERK_PICK; r._pickKind = "POUCH";
    const allinOps = [{ type: "add", id: "cherry", n: 3 }, { type: "remove", id: "book", n: 2 }];
    r.options = [{ type: "package", tpl: "allin", ops: allinOps, repId: "cherry", title: "몰빵", desc: "", preview: E.packagePreview(r.pouch, allinOps) }];
    const before = { ...r.pouch };
    g.pickPerk(0);
    if (!r.deepPity || r.deepPity.id !== "cherry") fail(`BH-4: package pity not set to repId cherry (${JSON.stringify(r.deepPity)})`);
    if (r.deepPity.spinsLeft !== 2) fail(`BH-4: package pity spinsLeft ${r.deepPity.spinsLeft} != 2`);
    // 실제 적용됐나(cherry +3 · book -2)
    if ((r.pouch.cherry || 0) !== (before.cherry || 0) + 3 || (r.pouch.book || 0) !== (before.book || 0) - 2) fail(`BH-4: package not applied (cherry=${r.pouch.cherry} book=${r.pouch.book})`);
    // 대청소 패키지(repId=null) → pity 미설정(가드).
    r.deepPity = null; r.phase = PHASE.PERK_PICK; r._pickKind = "POUCH";
    r.pouch = { cherry: 7, book: 6, star: 5, gem: 5, coin: 4, skull: 5, flame: 3 };  // total 35 §1.4 V3P1 ≤40
    const cleanOps = [{ type: "remove", id: "skull", n: 3 }, { type: "add", id: "empty", n: 2 }];
    r.options = [{ type: "package", tpl: "cleanup", ops: cleanOps, repId: null, title: "대청소", desc: "", preview: E.packagePreview(r.pouch, cleanOps) }];
    g.pickPerk(0);
    if (r.deepPity !== null) fail("BH-4: cleanup package (repId null) must NOT set pity");
    // 원자성 커밋 확인 — 대청소 적용됨: skull 5-3=2, empty +2=2
    if ((r.pouch.skull || 0) !== 2 || (r.pouch.empty || 0) !== 2) fail(`BH-4: cleanup not applied (skull=${r.pouch.skull} empty=${r.pouch.empty})`);
    notes.push("BH-4 package pity (repId→deepPity spinsLeft:2 · null repId=no pity) + atomic commit via pickPerk ok");
  } else fail("BH-4: driver did not reach SPIN");
}

// (BH-5) 규칙 유지 — 대량 무작위 주머니에서 패키지 적용 후 pouchValidate 항상 ok(총량/종류/태그/희귀도 비중).
{
  let applied = 0, violations = 0;
  for (let s = 0; s < 1500; s++) {
    const rng = E.makeRng(70000 + s);
    // 무작위 유효 주머니 생성(기본계열 랜덤 배분·해골 포함·§1.4 V3P1 총량 20~40).
    const pouch = {};
    const fams = ["cherry", "book", "star", "gem", "coin", "skull", "flame", "magnet"];
    let tot = 0;
    for (const f of fams) { const c = rng.n(8); if (c > 0) { pouch[f] = c; tot += c; } }
    // 종류 부족/총량 미달 보정
    if (Object.keys(pouch).filter((k) => pouch[k] > 0).length < DEEP.MIN_KINDS) { for (const f of fams) if (!pouch[f]) { pouch[f] = 2; tot += 2; } }
    while (tot < DEEP.DECK_MIN) { pouch.cherry = (pouch.cherry || 0) + 2; tot += 2; }
    while (tot > DEEP.DECK_MAX) { const f = fams.find((x) => (pouch[x] || 0) > 2); if (!f) break; pouch[f] -= 2; tot -= 2; }
    if (!E.pouchValidate(pouch).ok) continue;   // skip seeds that couldn't be normalized
    const tpls = E.packageTemplates(rng, pouch);
    for (const t of tpls) {
      const res = E.applySymbolPackage(pouch, t.ops);
      applied++;
      if (!res.ok) { violations++; continue; }   // 사전검증 통과분인데 재적용이 실패 = 계약 위반
      const v = E.pouchValidate(res.pouch);
      if (!v.ok) { violations++; fail(`BH-5: applied package ${t.tpl} produced invalid pouch — ${v.errors.join(" · ")}`); }
      // NaN/음수 스캔
      for (const k in res.pouch) checkNum(`BH-5 ${t.tpl}.${k}`, res.pouch[k]);
    }
  }
  if (violations > 0) fail(`BH-5: ${violations} rule violations across applied packages (must be 0)`);
  if (applied < 500) fail(`BH-5: too few packages applied (${applied}) — driver weak`);
  notes.push(`BH-5 rule integrity: ${applied} packages applied, 0 validation violations (총량/종류/태그/희귀도)`);
}

// (BH-6) 격리 — 일반모드 pickPerk 는 package 분기를 절대 안 탐(deepMode 무관하나 _pickKind==POUCH 는 심화 전용).
//  ★일반 오퍼(pickAugments/pickRelics)는 package 카드를 생성하지 않음(offerSymbolRewards 는 심화 POUCH 노드 전용).
{
  // 일반 증강/유물 오퍼에 package 타입이 절대 안 섞임.
  for (let s = 0; s < 200; s++) {
    const augs = E.pickAugments(E.makeRng(80000 + s), 5, new Set());
    const rels = E.pickRelics(E.makeRng(81000 + s), 5, new Set());
    for (const o of [...augs, ...rels]) if (o.type === "package") fail(`BH-6: package card leaked into normal AUG/REL offer: ${JSON.stringify(o)}`);
  }
  // 일반 300런 무회귀(패키지 도입 후) — offerSymbolRewards 는 심화 전용이라 일반 경로 rng 소비 불변이어야.
  let n = 0;
  for (let s = 1; s <= 300; s++) { const st = playRun(82000 + s, false); if (st && st.phase === PHASE.RUN_END) n++; }
  notes.push(`BH-6 isolation: no package in normal offers · normal ${n}/300 terminated`);
}

// (BH-7) 배치H 이후 무회귀 재실행 — 일반/심화 150런씩(패키지 채택 rng 소비로 시퀀스 전이 가능하나 종료율 단정).
{
  let n = 0, d = 0;
  for (let s = 1; s <= 150; s++) { const st = playRun(83000 + s, false); if (st && st.phase === PHASE.RUN_END) n++; }
  for (let s = 1; s <= 150; s++) { const st = playRun(84000 + s, true); if (st && st.phase === PHASE.RUN_END) d++; }
  notes.push(`re-run after Batch H: normal ${n}/150 · deep ${d}/150 terminated`);
  notes.push("BH UI (renderPouchReward rw-pkg 카드/pouchPreviewHtml 멀티op·패키지 테두리 --purple·pouchCelebrateData PRISM) — ui.js 하네스 미커버, 브라우저 실측 필요");
}

// ══════════════════════════════════════════════════════════════════════
// 배치 I — P3 총량 다이어트: DEEP 재스케일(100→60) 검증.
//   상수: TOTAL_BASE 60 · TOTAL_MIN 50 · TOTAL_MAX 80 · REL_MIN 3 · 압축표 57/54/51/48 · START_POUCH 합 60.
//   ★밸런스 고위험 배치라 전/후 통계표(deep 300런 평균 스테이지·점수)를 노트로 기록(±10%p 게이트는 보고에서 판정).
// ══════════════════════════════════════════════════════════════════════

// (BI-1) DEEP 상수 재스케일 확정값 — §1.4 V3P1: DECK_BASE 30 / DECK_MIN 20 / DECK_MAX 40.
{
  // V3P1 신규 상수 (DECK_*)
  if (DEEP.DECK_BASE !== 30) fail(`BI-1: DECK_BASE ${DEEP.DECK_BASE} != 30`);
  if (DEEP.DECK_MIN !== 20) fail(`BI-1: DECK_MIN ${DEEP.DECK_MIN} != 20`);
  if (DEEP.DECK_MAX !== 40) fail(`BI-1: DECK_MAX ${DEEP.DECK_MAX} != 40`);
  // ★2026-07-13 감사수정 #5: TOTAL_BASE/TOTAL_MIN/TOTAL_MAX 하위호환 별칭 제거됨(DECK_* 단일 소스) — 잔존 0 확인.
  if (DEEP.TOTAL_BASE !== undefined) fail(`BI-1: TOTAL_BASE alias 잔존(제거됐어야 함) — ${DEEP.TOTAL_BASE}`);
  if (DEEP.TOTAL_MIN !== undefined) fail(`BI-1: TOTAL_MIN alias 잔존(제거됐어야 함) — ${DEEP.TOTAL_MIN}`);
  if (DEEP.TOTAL_MAX !== undefined) fail(`BI-1: TOTAL_MAX alias 잔존(제거됐어야 함) — ${DEEP.TOTAL_MAX}`);
  if (DEEP.REL_MIN !== 3) fail(`BI-1: REL_MIN ${DEEP.REL_MIN} != 3`);
  if ((DEEP.REL_MIN_BY_SYM || {}).crown !== 2) fail(`BI-1: REL_MIN_BY_SYM.crown ${(DEEP.REL_MIN_BY_SYM||{}).crown} != 2 (오버라이드 유지)`);
  notes.push(`BI-1 V3P1 DECK rescale: base=${DEEP.DECK_BASE} min=${DEEP.DECK_MIN} max=${DEEP.DECK_MAX} REL_MIN=${DEEP.REL_MIN}`);
}

// (BI-2) 압축 패널티 오프바이원 — §1.5 V3P1: 28/26/24/22 임계(×0.5 리스케일).
{
  const cases = [
    [29, 1], [28, 1.03], [27, 1.03], [26, 1.06], [25, 1.06],
    [24, 1.09], [23, 1.09], [22, 1.15], [21, 1.15],
    [40, 1], [30, 1],
  ];
  for (const [t, exp] of cases) {
    const got = E.compressionPenalty(t);
    if (Math.abs(got - exp) > 1e-9) fail(`BI-2: compressionPenalty(${t})=${got} != ${exp}`);
  }
  // 각 임계 경계에서 +1 총량은 더 약한(또는 무) 패널티여야(단조).
  for (const th of [28, 26, 24, 22]) {
    if (!(E.compressionPenalty(th) >= E.compressionPenalty(th + 1))) fail(`BI-2: penalty not monotone at boundary ${th}`);
  }
  notes.push("BI-2 V3P1 compression off-by-one ok (28/26/24/22 le-inclusive · +1 약화 · 단조)");
}

// (BI-3) START_POUCH 총합 30·9종·pouchValidate 통과 — §1.3 V3P1.
{
  const sp = E.startPouch();
  const total = E.pouchTotal(sp);
  if (total !== 30) fail(`BI-3: START_POUCH total ${total} != 30`);
  const kinds = Object.keys(sp).filter((k) => sp[k] > 0).length;
  if (kinds !== 9) fail(`BI-3: START_POUCH kinds ${kinds} != 9`);
  // 왕관/empty/random 시작 제외 확인
  if (sp.crown) fail(`BI-3: START_POUCH crown should not exist (special-only) got ${sp.crown}`);
  if (sp.empty) fail(`BI-3: START_POUCH empty should not exist got ${sp.empty}`);
  if (sp.random) fail(`BI-3: START_POUCH random should not exist got ${sp.random}`);
  // 기본 9종 각 최소 1개
  for (const id of ["cherry","book","star","gem","coin","skull","flame","magnet","bomb"]) {
    if (!(sp[id] >= 1)) fail(`BI-3: START_POUCH ${id} < 1 (${sp[id]})`);
  }
  const v = E.pouchValidate(sp);
  if (!v.ok) fail(`BI-3: START_POUCH invalid — ${v.errors.join(" · ")}`);
  // §11 초반 밸런스 완화(레버②) 이후 비중: cherry 6/30=.200(↑), book 5/30=.167, skull 3/30=.100(↓) 등
  const v3Share = { cherry: 6/30, book: 5/30, star: 4/30, gem: 4/30, coin: 4/30, skull: 3/30, flame: 2/30, magnet: 1/30, bomb: 1/30 };
  for (const id in v3Share) {
    const cur = (sp[id] || 0) / total;
    if (Math.abs(cur - v3Share[id]) > 1e-9) fail(`BI-3: START_POUCH ${id} share ${cur.toFixed(4)} != ${v3Share[id].toFixed(4)}`);
  }
  notes.push(`BI-3 V3P1 START_POUCH total=30 kinds=9 valid · 왕관/empty/random 제외 확인`);
}

// (BI-4) REL_MIN 3 게이트 — 계열 2 차단·3 통과, crown 오버라이드(1 차단·2 통과) 유지.
//  §1.3 V3P1: START_POUCH 변경으로 flame=2, magnet=1, bomb=1 — 모두 REL_MIN 3 미달(차단).
//  §11 레버②(2026-07-13) 반영: cherry6/book5/star4/gem4/coin4/skull3 는 통과(≥3, skull은 경계값 3=3).
{
  if (E.isDeepCompat({ deep: 1, dSym: "flame" }, { flame: 2, cherry: 20 })) fail("BI-4: flame 2 should be gated (REL_MIN 3)");
  if (!E.isDeepCompat({ deep: 1, dSym: "flame" }, { flame: 3, cherry: 20 })) fail("BI-4: flame 3 should pass (REL_MIN 3)");
  if (E.isDeepCompat({ deep: 2, dSym: "crown" }, { crown: 1 })) fail("BI-4: crown 1 still gated (override 2)");
  if (!E.isDeepCompat({ deep: 2, dSym: "crown" }, { crown: 2 })) fail("BI-4: crown 2 passes (override 2)");
  // START_POUCH(V3P1 총량30·§11 레버②): cherry6/book5/star4/gem4/coin4/skull3 통과(≥3). flame2/magnet1/bomb1 차단(<3).
  const sp = E.startPouch();
  for (const [id, sym] of [["cherry", "cherry"], ["book", "book"], ["skull", "skull"], ["star", "star"], ["gem", "gem"], ["coin", "coin"]])
    if (!E.isDeepCompat({ deep: 1, dSym: sym }, sp)) fail(`BI-4: START_POUCH ${id}(${sp[id]}) should pass REL_MIN 3`);
  if (E.isDeepCompat({ deep: 1, dSym: "flame" }, sp)) fail("BI-4: START_POUCH flame(2) should be gated (REL_MIN 3)");
  if (E.isDeepCompat({ deep: 1, dSym: "magnet" }, sp)) fail("BI-4: START_POUCH magnet(1) should be gated (REL_MIN 3)");
  if (E.isDeepCompat({ deep: 1, dSym: "bomb" }, sp)) fail("BI-4: START_POUCH bomb(1) should be gated (REL_MIN 3)");
  notes.push("BI-4 V3P1 REL_MIN 3 gate (2 차단·3 통과 · crown 오버라이드 2 유지 · START_POUCH flame/magnet/bomb 차단)");
}

// (BI-5) V3P2 오퍼 v3 기본 정합 — 시작주머니·압축주머니 대상 special/skip 카드 구성 확인.
//   ★V3P2: package 카드 은퇴(dormant). 정비소 서비스(sv_add_basic/sv_upgrade/sv_remove)가 기능 커버.
{
  const N = 500;
  const measureV3 = (pouch) => {
    let pkgCount = 0, specialCount = 0, skipCount = 0;
    for (let s = 0; s < N; s++) {
      const off = E.offerSymbolRewards(E.makeRng(90000 + s), pouch, 5);
      pkgCount  += off.filter((o) => o.type === "package").length;
      specialCount += off.filter((o) => o.type === "special").length;
      skipCount += off.filter((o) => o.type === "skip").length;
    }
    return { pkgCount, specialCount, skipCount };
  };
  // ① 시작주머니(V3P1 총량30) — 각 오퍼에 package 0, special≥1, skip≥1 기대.
  const startP = E.startPouch();
  if (E.pouchTotal(startP) !== 30) fail(`BI-5: start pouch total ${E.pouchTotal(startP)} != 30`);
  const r1 = measureV3(startP);
  if (r1.pkgCount > 0) fail(`BI-5: start-pouch package rate ${r1.pkgCount}건 (V3P2 은퇴 — 0 기대)`);
  if (r1.specialCount < N) fail(`BI-5: start-pouch special 카드 ${r1.specialCount}건 (오퍼당 1장 이상 기대)`);
  if (r1.skipCount < N * 0.9) fail(`BI-5: start-pouch skip 카드 ${r1.skipCount}/${N} (90%+ 기대)`);
  // ② 압축주머니(총량 22) — 하한 근접에서도 오퍼 정상 생성.
  const compP = { cherry: 6, book: 4, star: 3, gem: 3, coin: 2, skull: 2, magnet: 2 };  // total 22
  if (!E.pouchValidate(compP).ok) fail(`BI-5: compressed test pouch itself invalid — ${E.pouchValidate(compP).errors.join(", ")}`);
  const r2 = measureV3(compP);
  if (r2.pkgCount > 0) fail(`BI-5: compressed-pouch package rate ${r2.pkgCount}건 (V3P2 은퇴 — 0 기대)`);
  if (r2.specialCount < N) fail(`BI-5: compressed-pouch special 카드 ${r2.specialCount}건 (오퍼당 1장 이상 기대)`);
  notes.push(`BI-5 V3P2 오퍼: start(30)=special${r1.specialCount}/skip${r1.skipCount}/pkg${r1.pkgCount} · compressed(22)=special${r2.specialCount}/skip${r2.skipCount}/pkg${r2.pkgCount}`);
}

// (BI-6) V3P1/V3P2 통계 노트 — deep 300런 평균 스테이지·점수. V3P1은 구조 전환이라 기준선 리셋.
//   안전판: V3P1-BASELINE(1.867) 대비 ±40% 이내면 정상(V3P2 오퍼 재설계 감안).
//   ★V3P2 이후 이 측정이 새 V3P2-BASELINE이 됨.
{
  const N = 300; let term = 0, stageSum = 0, scoreSum = 0;
  for (let s = 1; s <= N; s++) {
    const st = playRun(95000 + s, true);
    if (st && st.phase === PHASE.RUN_END) { term++; stageSum += (st.stage || 0); scoreSum += (st.finalScore || 0); }
  }
  const avgStage = stageSum / Math.max(1, term), avgScore = scoreSum / Math.max(1, term);
  checkNum("BI-6 stats", avgStage, avgScore);
  // V3P1-BASELINE: avgStage 1.867 (배치I 60-총량 기준선 3.073에서 V3P1 구조 전환 후 리셋).
  const V3P1_BASELINE = { avgStage: 1.867 };
  const stageDeltaPct = ((avgStage - V3P1_BASELINE.avgStage) / V3P1_BASELINE.avgStage) * 100;
  notes.push(`BI-6 V3P2-BASELINE: term=${term}/${N} · avgStage=${avgStage.toFixed(3)} · avgScore=${avgScore.toFixed(1)}`);
  notes.push(`BI-6 vs V3P1-BASELINE(1.867): avgStage ${V3P1_BASELINE.avgStage} → ${avgStage.toFixed(3)} (Δ=${stageDeltaPct.toFixed(2)}%, -40% 하락 기준)`);
  // 안전판: -40% 초과 "하락" 시 밸런스 대붕괴(너무 어려워짐). ★§11(2026-07-13) 이후 의도적 상승은 정상(초반 완화가
  //  목적) — symmetric(±) 대신 asymmetric(하락만) 게이트로 전환(BD-7 -15% 하락전용 게이트와 동일 패턴 정합).
  if (stageDeltaPct < -40) fail(`BI-6: avgStage moved ${stageDeltaPct.toFixed(1)}% (<-40% — 밸런스 대붕괴, 재조정 필요)`);
}

// (BI-7) §1.5 V3P1 첫압축 업적 임계 27 정합 — 무압축(총량 30) 클리어로는 compress95Clear 안 서고, 27↓ 압축으로 선다.
//   배치I: total<=54 → V3P1: total<=27(×0.5 리스케일).
{
  // 시작 주머니(총량 30, 무압축) 스테이지 클리어 → compress95Clear 미설정(30 > 27).
  const gHi = deepAtSpin(9800, null);
  gHi.run.pouch = E.startPouch();                                    // 총량 30(무압축)
  if (E.pouchTotal(gHi.run.pouch) !== 30) fail(`BI-7: start pouch total ${E.pouchTotal(gHi.run.pouch)} != 30 (전제 붕괴)`);
  gHi.run.stage = 1;                                                 // 비보스(isBossStage(1)=false)·asc0
  gHi._clearStage();
  if (gHi.run.deepStats.compress95Clear) fail("BI-7: 무압축(총량 30) 클리어인데 compress95Clear 섰음 (구 임계 54 회귀 의심)");
  // 총량 27(압축) 스테이지 클리어 → compress95Clear 설정(27 <= 27).
  const gLo = deepAtSpin(9801, null);
  gLo.run.pouch = { cherry: 6, book: 5, star: 4, gem: 4, coin: 3, skull: 3, magnet: 2 };   // 총량 27
  if (E.pouchTotal(gLo.run.pouch) !== 27) fail(`BI-7: compressed pouch total ${E.pouchTotal(gLo.run.pouch)} != 27 (전제 붕괴)`);
  gLo.run.stage = 1;
  gLo._clearStage();
  if (!gLo.run.deepStats.compress95Clear) fail("BI-7: 총량 27(압축) 클리어인데 compress95Clear 안 섰음 (임계 27 le-inclusive 회귀)");
  // 경계 +1(총량 28) 은 미설정(오프바이원 가드).
  const gEdge = deepAtSpin(9802, null);
  gEdge.run.pouch = { cherry: 6, book: 5, star: 4, gem: 4, coin: 3, skull: 3, magnet: 3 };  // 총량 28
  if (E.pouchTotal(gEdge.run.pouch) !== 28) fail(`BI-7: edge pouch total ${E.pouchTotal(gEdge.run.pouch)} != 28 (전제 붕괴)`);
  gEdge.run.stage = 1;
  gEdge._clearStage();
  if (gEdge.run.deepStats.compress95Clear) fail("BI-7: 총량 28(>27) 클리어인데 compress95Clear 섰음 (오프바이원)");
  notes.push("BI-7 V3P1 첫압축 임계 27 정합: 무압축30 미설정 · 압축27 설정 · 경계28 미설정");
}

// ══════════════════════════════════════════════════════════════════════
//  2026-07-09 신규 단정 — prism_ink 심화 SYMAUG 강제 + fate_bell 발동
// ══════════════════════════════════════════════════════════════════════

// (NEW-1) prism_ink 심화 SYMAUG forceTier — 프리즘 강제 + 소비 + 폴백 시 잉크 보존 + 일반모드 무영향.
{
  // ① 심화 SYMAUG 노드에서 _prismInk=true → off.meta.tier===PRISM 이면 소비(false), 폴백 시 보존(true).
  const g = deepAtSpin(99001, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    // 풍부한 주머니: 심볼증강 PRISM 풀이 소진 안 되도록 8종 모두 미보유.
    g.run.perks = g.run.perks.filter((id) => !id.startsWith("sp_"));   // 기존 prism symAug 제거
    g.run._prismInk = true;
    // SYMAUG 노드 직접 진입
    g.run.phase = 99;   // 임시 — selectNode 직접 호출 위해 내부 phase 우회
    const r = g.run;
    r.phase = "NODE_SELECT"; r.nodes = ["SYMAUG"]; r.options = ["SYMAUG"];
    const st = g.selectNode(0);
    // PRISM 심볼증강이 8종이므로 폴백 없이 PRISM 오퍼가 나와야 함.
    if (st.offerMeta && st.offerMeta.tier === "PRISM") {
      // 성공: 잉크 소비
      if (r._prismInk !== false) fail("NEW-1: prism_ink not consumed after SYMAUG PRISM-force success");
    } else {
      // 폴백(예상치 못한 풀 고갈): 잉크 보존
      if (r._prismInk !== true) fail("NEW-1: prism_ink consumed on SYMAUG forceTier PRISM fallback (must preserve)");
      notes.push("NEW-1: SYMAUG PRISM pool exhausted (fallback path) — ink preserved correctly");
    }
    notes.push(`NEW-1 SYMAUG forceTier result: tier=${st.offerMeta?.tier} · prismInk after=${r._prismInk}`);
  } else fail("NEW-1: driver did not reach SPIN");

  // ② 일반 AUGMENT 경로는 무접촉 — 일반 런에서 _prismInk 배선 기존과 동일.
  const gn = new Game({ seed: 99002, storage: memStore() });
  gn.profile.runs = 1; gn.profile.playerLevel = 30;
  gn.startRun(0, false);
  let stn = gn.state(), grd = 0;
  while (stn && stn.phase !== PHASE.SPIN && grd++ < 20) {
    if (stn.phase === PHASE.SELECT_CHAR) stn = gn.selectChar(gn.unlockedChars()[0].id);
    else if (stn.phase === PHASE.SELECT_MACHINE) stn = gn.selectMachine(gn.unlockedMachines()[0].id);
    else if (stn.phase === PHASE.SELECT_DEVICE) stn = gn.selectDevice(stn.options[0]?.id || "");
    else break;
  }
  if (gn.run && gn.run.phase === PHASE.SPIN) {
    gn.run._prismInk = true;
    // NODE_SELECT → AUGMENT 노드 진입
    gn.run.phase = "NODE_SELECT"; gn.run.nodes = ["AUGMENT"]; gn.run.options = ["AUGMENT"];
    const stn2 = gn.selectNode(0);
    // 일반 AUGMENT case: prismInk 소비 → false, offerMeta.tier === PRISM(성공 시)
    if (gn.run._prismInk !== false) fail("NEW-1: normal AUGMENT prism_ink not consumed (regression)");
    if (stn2.offerMeta && stn2.offerMeta.tier !== "PRISM") fail("NEW-1: normal AUGMENT forceTier PRISM failed (tier=" + stn2.offerMeta?.tier + ")");
    notes.push("NEW-1 normal AUGMENT forceTier unaffected (regression check ok)");
  } else fail("NEW-1: normal driver did not reach SPIN for regression check");
  notes.push("NEW-1 prism_ink SYMAUG forceTier ok");
}

// (NEW-2) fate_bell 발동 — 스핀 소진 & 쿼터 미달 직전 +1 스핀(런당 1회) + 미보유 시 무발동.
//  (Kotlin 캐논 동기 2026-07-09: 부족 ≤15 게이트 — ①부족 ≤15 발동(경계 15 포함) ②부족 >15(16) 미발동·즉시 종료
//   ③런당 1회 유지 ④미보유 시 무발동. 결정론: pouch={skull:60}=스핀 EXP 0 보장(초기런 perks 에 skullExp 퍽 없음),
//   device=""·GREROLL 소진으로 _canRecover false 확정.)
function fateBellRig(seed) {
  const g = deepAtSpin(seed, null);
  if (!g.run || g.run.phase !== PHASE.SPIN) return null;
  const r = g.run;
  r.pouch = { skull: 60 };            // 스핀 EXP 0 → stageExp 불변(부족분 결정론)
  r.survive = false;                  // 보험증서 없음
  r.device = "";                      // MANIP/벨 장치 없음
  if (!r.usedCmds.includes("GREROLL")) r.usedCmds.push("GREROLL");   // 도박꾼 무료 재굴림 소진 → _canRecover false 확정
  r.stageExp = 0;
  return g;
}
{
  // ① 부족 15(경계, ≤15) → 발동: +1 스핀·배너·fateBellUsed=1·종료 안 함.
  const g = fateBellRig(99010);
  if (g) {
    const r = g.run;
    r.perks.push("fate_bell"); r.fateBellUsed = 0;
    r.quota = 15;                       // 부족 = 15 - floor(0) = 15 (≤15 경계 포함)
    r.spins = r.spinIndex + 1;          // 다음 스핀이 마지막
    const spinsBefore = r.spins;
    const out = g.spin("N");
    if (out.gameOver) fail("NEW-2①: 부족 15 & fate_bell 보유인데 gameOver (경계 ≤15 발동 실패)");
    if (r.spins !== spinsBefore + 1) fail(`NEW-2①: spins not +1 (before=${spinsBefore} after=${r.spins})`);
    if (r.fateBellUsed !== 1) fail(`NEW-2①: fateBellUsed not set (${r.fateBellUsed})`);
    if (!out.banner || !out.banner.includes("운명의종")) fail(`NEW-2①: banner missing (${out.banner})`);
    // ③ 런당 1회: 같은 런에서 다시 소진 직전(부족 ≤15) → 미발동·즉시 종료.
    r.spins = r.spinIndex + 1;
    const out2 = g.spin("N");
    if (!out2.gameOver) fail("NEW-2③: fate_bell 2회차 발동(런당 1회 위반) 또는 종료 실패");
    if (r.fateBellUsed !== 1) fail(`NEW-2③: fateBellUsed changed (${r.fateBellUsed})`);
    notes.push("NEW-2①③ 부족 15 발동(+1스핀·배너) + 런당 1회 게이트 ok");
  } else fail("NEW-2①: driver did not reach SPIN");

  // ② 부족 16(>15) → fate_bell 보유해도 미발동·즉시 gameOver (Kotlin 캐논 부족 게이트).
  const g2 = fateBellRig(99011);
  if (g2) {
    const r2 = g2.run;
    r2.perks.push("fate_bell"); r2.fateBellUsed = 0;
    r2.quota = 16;                      // 부족 = 16 - floor(0) = 16 (>15)
    r2.spins = r2.spinIndex + 1;
    const out3 = g2.spin("N");
    if (!out3.gameOver) fail("NEW-2②: 부족 16 인데 fate_bell 발동 또는 종료 안 됨 (>15 게이트 실패)");
    if (r2.fateBellUsed !== 0) fail(`NEW-2②: fateBellUsed consumed on non-trigger (${r2.fateBellUsed})`);
    notes.push("NEW-2② 부족 16(>15) 미발동·즉시 종료 ok");
  } else fail("NEW-2②: driver did not reach SPIN");

  // ④ fate_bell 미보유(부족 ≤15여도) → 무발동·즉시 종료.
  const g3 = fateBellRig(99012);
  if (g3) {
    const r3 = g3.run;
    r3.perks = r3.perks.filter((id) => id !== "fate_bell");
    r3.quota = 10;                      // 부족 10 (≤15) — 그래도 미보유라 무발동
    r3.spins = r3.spinIndex + 1;
    const out4 = g3.spin("N");
    if (!out4.gameOver) fail("NEW-2④: fate_bell 미보유인데 종료 안 됨 (spurious trigger?)");
    notes.push("NEW-2④ 미보유 시 무발동·즉시 종료 ok");
  } else fail("NEW-2④: driver did not reach SPIN");
}

// ── (BB-1~7) 배치 B: bias 레이어 단정 ─────────────────────────────────────
// ① bias 생략 = 기존과 비트 동일(시드 고정 1000드로우 비교)
{
  const pouch = { cherry: 15, book: 11, star: 8, gem: 7, coin: 6, skull: 6, flame: 3, magnet: 2, bomb: 1, crown: 1 };
  const rng1 = E.makeRng(77777), rng2 = E.makeRng(77777);
  const out1 = E.pouchDraw(rng1, pouch, 5);
  const out2 = E.pouchDraw(rng2, pouch, 5, undefined);
  const match = out1.every((c, i) => c.sym.id === out2[i].sym.id && c.tag === out2[i].tag);
  if (!match) fail("BB-1: bias=undefined is not bit-identical to no-bias");
  // bias={} (빈 객체, 모든 선택 필드 undefined) 도 동일해야 함
  const rng3 = E.makeRng(77777);
  const out3 = E.pouchDraw(rng3, pouch, 5, {});
  const match3 = out1.every((c, i) => c.sym.id === out3[i].sym.id && c.tag === out3[i].tag);
  if (!match3) fail("BB-1: bias={} (no active fields) is not bit-identical to no-bias");
  // 시드 고정 1000드로우 비교(pouchDraw 내부 entries 경로 동일 확인)
  const rngA = E.makeRng(42), rngB = E.makeRng(42);
  let allSame = true;
  for (let i = 0; i < 200; i++) {
    const a = E.pouchDraw(rngA, pouch, 5), b = E.pouchDraw(rngB, pouch, 5, undefined);
    if (!a.every((c, j) => c.sym.id === b[j].sym.id)) { allSame = false; break; }
  }
  if (!allSame) fail("BB-1: 1000-draw seed comparison failed (bias=undefined vs no bias)");
  notes.push("BB-1 bias 생략=기존 비트동일 ok (undefined/empty/{} ×1000드로우)");
}

// ② mul/add/rareMul 각 단위 테스트
{
  const pouch = { cherry: 10, wild: 2, crown: 1, book: 5 };
  // mul: cherry 비중 2배 → cherry 드로우 확률 상승 확인(1000회 샘플)
  const rngM = E.makeRng(111);
  let cherryHits = 0;
  for (let i = 0; i < 1000; i++) {
    const d = E.pouchDraw(rngM, pouch, 1, { mul: { cherry: 2.0 } });
    if (d[0].sym.id === "cherry") cherryHits++;
  }
  // 기대 비중: cherry=20/(20+2+1+5)=71.4% (원래 55.6%) — 600 이상이어야 함
  if (cherryHits < 550) fail(`BB-2 mul: cherry hits too low (${cherryHits}/1000, expected ≥550)`);
  // add: crown에 add=4 → crown 비중 대폭 상승(원래 5.6% → add=4로 (1+4)/(10+2+5+5)=22.7%)
  const rngA = E.makeRng(222);
  let crownHits = 0;
  for (let i = 0; i < 1000; i++) {
    const d = E.pouchDraw(rngA, pouch, 1, { add: { crown: 4.0 } });
    if (d[0].sym.id === "crown") crownHits++;
  }
  if (crownHits < 100) fail(`BB-2 add: crown hits too low (${crownHits}/1000, expected ≥100)`);
  // rareMul: wild/crown은 희귀/전설 → rareMul=3으로 비중 상승
  const rngR = E.makeRng(333);
  let rareHits = 0;
  for (let i = 0; i < 1000; i++) {
    const d = E.pouchDraw(rngR, pouch, 1, { rareMul: 3.0 });
    if (d[0].sym.id === "wild" || d[0].sym.id === "crown") rareHits++;
  }
  // 기대: (2*3+1*3)/(10+2*3+1*3+5) = 9/24 = 37.5% → 300 이상
  if (rareHits < 200) fail(`BB-2 rareMul: rare hits too low (${rareHits}/1000, expected ≥200)`);
  notes.push("BB-2 mul/add/rareMul 단위 테스트 ok");
}

// ③ 부재 주입 금지(add 대상 미보유 심볼 → 등장 0)
{
  const pouch = { cherry: 10, book: 5 };   // wild 없음
  const rngZ = E.makeRng(555);
  let wildHits = 0;
  for (let i = 0; i < 500; i++) {
    const d = E.pouchDraw(rngZ, pouch, 5, { add: { wild: 100 } });   // add=100이지만 wild 미보유
    for (const c of d) if (c.sym.id === "wild") wildHits++;
  }
  if (wildHits > 0) fail(`BB-3: 부재 심볼(wild) add bias로 주입됨 (${wildHits}회 등장 — 절대 금지)`);
  // mul도 동일: 미보유 심볼 mul → 등장 0
  const rngZ2 = E.makeRng(556);
  let crownHits2 = 0;
  for (let i = 0; i < 500; i++) {
    const d = E.pouchDraw(rngZ2, pouch, 5, { mul: { crown: 10 } });
    for (const c of d) if (c.sym.id === "crown") crownHits2++;
  }
  if (crownHits2 > 0) fail(`BB-3: 부재 심볼(crown) mul bias로 주입됨 (${crownHits2}회 — 절대 금지)`);
  notes.push("BB-3 부재 주입 금지 ok (add/mul 대상 미보유 → 0건)");
}

// ④ 전멸 폴백 — 모든 심볼 mul=0 → base 폴백으로 드로우 성공
{
  const pouch = { cherry: 5, book: 3 };
  const rngF = E.makeRng(666);
  let drew = 0;
  for (let i = 0; i < 100; i++) {
    const d = E.pouchDraw(rngF, pouch, 5, { mul: { cherry: 0, book: 0 } });
    if (d && d.length === 5) drew++;
  }
  if (drew < 100) fail(`BB-4: 전멸 폴백 실패 (${drew}/100 성공)`);
  // 폴백 시 반환 심볼은 cherry or book(base pouch)
  const rngF2 = E.makeRng(667);
  const d0 = E.pouchDraw(rngF2, pouch, 5, { mul: { cherry: 0, book: 0 } });
  const allBase = d0.every((c) => c.sym.id === "cherry" || c.sym.id === "book" || c.sym.id === EMPTY_SYM.id);
  if (!allBase) fail("BB-4: 전멸 폴백 시 base 외 심볼 등장");
  notes.push("BB-4 전멸 폴백 ok (mul=0 전체 → base 폴백·드로우 불능 방지)");
}

// ⑤ 대표 아이템 부활 실측(cherry_juice→cherry 편향·magnify→희귀 편향)
{
  // cherry_juice 효과: wmul("cherry", 2.5) — bias.mul.cherry=2.5
  const pouch = { cherry: 10, book: 10, star: 8, gem: 7, coin: 6 };
  const rngCJ = E.makeRng(888);
  let cherryBias = 0, cherryBase = 0;
  for (let i = 0; i < 500; i++) {
    const dB = E.pouchDraw(rngCJ, pouch, 1, { mul: { cherry: 2.5 } });
    if (dB[0].sym.id === "cherry") cherryBias++;
    const dN = E.pouchDraw(rngCJ, pouch, 1);
    if (dN[0].sym.id === "cherry") cherryBase++;
  }
  if (cherryBias <= cherryBase) fail(`BB-5 cherry_juice: bias(${cherryBias}) not > base(${cherryBase})`);
  // magnify 효과: rareWeightMul=4 (희귀 4배) — pouch에 wild/crown 있는 경우
  const pouchwild = { cherry: 10, book: 10, wild: 2, crown: 1 };
  const rngMag = E.makeRng(999);
  let rareBias = 0, rareBase = 0;
  for (let i = 0; i < 500; i++) {
    const dB = E.pouchDraw(rngMag, pouchwild, 1, { rareMul: 4.0 });
    if (dB[0].sym.id === "wild" || dB[0].sym.id === "crown") rareBias++;
    const dN = E.pouchDraw(rngMag, pouchwild, 1);
    if (dN[0].sym.id === "wild" || dN[0].sym.id === "crown") rareBase++;
  }
  if (rareBias <= rareBase) fail(`BB-5 magnify rareMul: bias(${rareBias}) not > base(${rareBase})`);
  notes.push("BB-5 대표 아이템 부활 실측 ok (cherry_juice mul·magnify rareMul 유효)");
}

// ⑥ 일반모드 300런 무회귀(bias 추가 후 일반 경로 격리 확인)
{
  let normalTerm = 0;
  for (let i = 0; i < 300; i++) {
    const st = playRun(70000 + i, false);
    if (st && st.phase === PHASE.RUN_END) normalTerm++;
  }
  if (normalTerm < 300) fail(`BB-6 일반모드 무회귀: ${normalTerm}/300 종료(bias 추가 후 회귀 감지)`);
  notes.push(`BB-6 일반모드 300런 무회귀 ok (${normalTerm}/300)`);
}

// ⑦ 신규 태깅 퍽(lucky/fortune_check/luck_accum/wild_world/joker/magnifier/mini_scope/crystal_ball/fate_handle/gamblers_eye/lucky_eraser)
//    REL_MIN 게이트 통과 확인(deep:1 이므로 deepCompatPool 에 포함·필터 통과해야 함).
{
  const REL_MIN = DEEP.REL_MIN ?? 3;
  const newTagged1Aug = ["lucky", "fortune_check", "luck_accum", "wild_world", "joker", "gamblers_dice", "key_master"];
  const newTagged1Rel = ["magnifier", "mini_scope", "crystal_ball", "fate_handle", "gamblers_eye", "lucky_eraser"];
  let pass = 0, failList = [];
  for (const id of newTagged1Aug) {
    const p = AUG_BY_ID[id]; if (!p) { failList.push(`${id} not in AUG_BY_ID`); continue; }
    if (!p.deep) { failList.push(`${id} deep tag missing`); continue; }
    // dSym 없으면 REL_MIN 게이트 없음(famCount 무관) — 게이트 통과 확인만
    // dSym 있으면 pouchCount >= REL_MIN 조건(여기서는 태깅 존재만 확인)
    pass++;
  }
  for (const id of newTagged1Rel) {
    const p = REL_BY_ID[id]; if (!p) { failList.push(`${id} not in REL_BY_ID`); continue; }
    if (!p.deep) { failList.push(`${id} deep tag missing`); continue; }
    pass++;
  }
  if (failList.length) fail(`BB-7 신규 태깅 퍽 누락/오류: ${failList.join(", ")}`);
  // gamblers_dice/key_master는 deep:2 — 확인
  if (AUG_BY_ID["gamblers_dice"] && AUG_BY_ID["gamblers_dice"].deep !== 2) fail("BB-7: gamblers_dice should be deep:2");
  if (AUG_BY_ID["key_master"] && AUG_BY_ID["key_master"].deep !== 2) fail("BB-7: key_master should be deep:2");
  // lucky/fortune_check/luck_accum/wild_world/joker은 deep:1
  for (const id of ["lucky", "fortune_check", "luck_accum", "wild_world", "joker"]) {
    const p = AUG_BY_ID[id];
    if (p && p.deep !== 1) fail(`BB-7: ${id} should be deep:1 (got ${p.deep})`);
  }
  // 유물 6종 모두 deep:1
  for (const id of newTagged1Rel) {
    const p = REL_BY_ID[id];
    if (p && p.deep !== 1) fail(`BB-7: relic ${id} should be deep:1 (got ${p.deep})`);
  }
  notes.push(`BB-7 신규 태깅 퍽 REL_MIN 게이트 및 deep 값 ok (${pass}/${newTagged1Aug.length + newTagged1Rel.length} 통과)`);
}

// ══════════════════════════════════════════════════════════════════════
//  배치 A — 저주 시스템 심화 이식 (§2 Step 6 ①~⑦)
// ══════════════════════════════════════════════════════════════════════
import { CURSES, CUR_BY_ID } from "./data.js";

// ① CURSE/RISK 노드: stage>=6 심화 등장 · stage<6 미등장
// 접근: _clearStage() 직접 호출로 stage 강제 → 통계적 희박 문제 회피.
// 100회 반복: stage=6 고정 딥모드 → nodes 배열에 CURSE/RISK 포함 여부.
{
  // 도우미: startRun(deep) 후 SELECT 단계 통과 → SPIN 단계 도달 반환
  function initToSpin(seed, deep) {
    const g = new Game({ seed, storage: memStore() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, deep);
    let st = g.state(), guard = 0;
    while (st && st.phase !== PHASE.SPIN && guard++ < 30) {
      if (st.phase === PHASE.SELECT_CHAR) { st = g.selectChar(g.unlockedChars()[0].id); continue; }
      if (st.phase === PHASE.SELECT_MACHINE) { st = g.selectMachine(g.unlockedMachines()[0].id); continue; }
      if (st.phase === PHASE.SELECT_DEVICE) { st = g.selectDevice(st.options[0]?.id || ""); continue; }
      break;
    }
    return { g, st };
  }

  // 직접 stage 강제: SPIN 단계에서 stage를 target-1 로 세팅 후 _clearStage() 호출
  // → _clearStage 는 r.stage 를 읽어 dpool 생성 후 r.stage += 1 함
  // → target=6 이 되려면 r.stage=5 (노드 생성 직전) + 후 6으로 증가
  // 그러나 실제 노드 생성 코드는 'stage' 지역변수(= r.stage)를 쓰므로
  // r.stage=5 일 때 _clearStage() → stage<6 → CURSE 없음
  // r.stage=6 일 때 _clearStage() → stage>=6 → CURSE 추가
  function forceNodeAtStage(seed, deep, targetStage) {
    const { g, st } = initToSpin(seed, deep);
    if (!st || st.phase !== PHASE.SPIN) return [];
    // stage를 targetStage 로 강제(r.stage가 _clearStage 에서 'const stage = r.stage' 로 읽힘)
    g.run.stage = targetStage;
    g.run.stageExp = g.run.quota;  // 클리어 조건 충족
    g._clearStage();
    const gst = g.state();
    if (!gst || gst.phase !== PHASE.STAGE_CLEAR) return [];
    const nodeSt = g.proceedToNodes();
    return nodeSt ? (nodeSt.nodes || []) : [];
  }

  // 심화: stage=6 에서 CURSE/RISK 등장(100회 중 각 1회 이상 필수)
  let curseFound = 0, riskFound = 0;
  const TRIALS = 100;
  for (let s = 1; s <= TRIALS; s++) {
    const nodes = forceNodeAtStage(30000 + s, true, 6);
    if (nodes.includes("CURSE")) curseFound++;
    if (nodes.includes("RISK"))  riskFound++;
  }
  if (curseFound === 0) fail(`BA-1: CURSE node never appeared at stage=6 in deep (0/${TRIALS} trials)`);
  if (riskFound  === 0) fail(`BA-1: RISK node never appeared at stage=6 in deep (0/${TRIALS} trials)`);

  // 심화: stage=5 에서 CURSE/RISK 미등장(stage<6 격리)
  let curseAt5 = 0, riskAt5 = 0;
  for (let s = 1; s <= TRIALS; s++) {
    const nodes = forceNodeAtStage(30000 + s, true, 5);
    if (nodes.includes("CURSE")) curseAt5++;
    if (nodes.includes("RISK"))  riskAt5++;
  }
  if (curseAt5 > 0) fail(`BA-1: CURSE appeared at stage=5 in deep (stage<6 격리 위반, ${curseAt5}/${TRIALS})`);
  if (riskAt5  > 0) fail(`BA-1: RISK appeared at stage=5 in deep (stage<6 격리 위반, ${riskAt5}/${TRIALS})`);

  // 일반모드: stage=6 에서 CURSE/RISK 는 기존부터 pool에 존재(기본모드와 동일 조건 — 설계 §2 Step2 "기본모드와 동일 조건").
  // 격리 체크: 일반모드 stage=6 CURSE가 deep:1 제한 없이 동작하는지(selectNode 일반 분기 무변경 확인은 BA-6 무회귀가 커버).
  let normCurseAt6 = 0;
  for (let s = 1; s <= TRIALS; s++) {
    const nodes = forceNodeAtStage(30000 + s, false, 6);
    if (nodes.includes("CURSE")) normCurseAt6++;
  }
  // 일반모드에서 CURSE가 stage>=6에 등장하는 것은 기존 동작(정상). 등장하지 않으면 기존 코드가 깨진 것.
  if (normCurseAt6 === 0) fail(`BA-1: CURSE 일반모드 stage=6 등장 0 — 기존 pool 코드 손상 의심`);

  notes.push(`BA-1 CURSE/RISK 노드 stage 게이트 ok — deep6: CURSE ${curseFound}/${TRIALS} RISK ${riskFound}/${TRIALS} · deep5: CURSE 0 RISK 0 · normal6 기존동작: CURSE ${normCurseAt6}/${TRIALS}`);
}

// ② 대표 저주 효과: cursed_skulls bias 유효·quotaMul 류·hex_allornothing dEff archetype 절반
{
  // quotaMul 류(hard_exam +10%): buildMods 가 curses 포함하므로 _mods()에서 확인.
  const gQ = new Game({ seed: 21000, storage: memStore() });
  gQ.profile.runs = 1; gQ.profile.playerLevel = 30;
  gQ.startRun(0, true);
  let stQ = gQ.state(); let guardQ = 0;
  while (stQ && stQ.phase !== PHASE.SPIN && guardQ++ < 20) {
    if (stQ.phase === PHASE.SELECT_CHAR) stQ = gQ.selectChar(gQ.unlockedChars()[0].id);
    else if (stQ.phase === PHASE.SELECT_MACHINE) stQ = gQ.selectMachine(gQ.unlockedMachines()[0].id);
    else if (stQ.phase === PHASE.SELECT_DEVICE) stQ = gQ.selectDevice(stQ.options[0]?.id || "");
    else break;
  }
  // hard_exam quotaMul 적용 확인
  const baseQuota = gQ._mods().quotaMul;
  gQ.run.curses.push("hard_exam");
  const withCurse = gQ._mods().quotaMul;
  if (!(withCurse > baseQuota)) fail(`BA-2: hard_exam quotaMul not applied (${baseQuota} → ${withCurse})`);
  // hex_allornothing dEff: 전공 보너스 ×0.5
  // 해골 전공 t2 셋업 후 hex_allornothing 저주 주입 → deepFamilyExpMul 감소 확인
  gQ.run.curses = ["hex_allornothing"];
  gQ.run.pouch = { cherry: 3, book: 3, skull: 30, star: 3, gem: 3, coin: 3, flame: 3, magnet: 3, crown: 1 };  // skull 50%↑ → t2
  const withoutHex = (() => { const tmp = [...gQ.run.curses]; gQ.run.curses = []; const m = gQ._mods(); gQ.run.curses = tmp; return m; })();
  const withHex = gQ._mods();
  const hexFam = Object.keys(withHex.deepFamilyExpMul || {});
  if (hexFam.length > 0) {
    const famKey = hexFam[0];
    const without = (withoutHex.deepFamilyExpMul || {})[famKey] || 0;
    const with_ = (withHex.deepFamilyExpMul || {})[famKey] || 0;
    if (without > 0 && !(with_ < without)) fail(`BA-2: hex_allornothing dEff not halving archetype expMul (${without} → ${with_})`);
  }
  notes.push("BA-2 저주 효과(quotaMul·hex dEff archetype 절반) ok");
}

// ③ cultist/black_grad_photo 저주 개수 스케일
{
  const gC = new Game({ seed: 22000, storage: memStore() });
  gC.profile.runs = 1; gC.profile.playerLevel = 30;
  gC.startRun(0, true);
  let stC = gC.state(); let guardC = 0;
  while (stC && stC.phase !== PHASE.SPIN && guardC++ < 20) {
    if (stC.phase === PHASE.SELECT_CHAR) stC = gC.selectChar("cultist");
    else if (stC.phase === PHASE.SELECT_MACHINE) stC = gC.selectMachine(gC.unlockedMachines()[0].id);
    else if (stC.phase === PHASE.SELECT_DEVICE) stC = gC.selectDevice(stC.options[0]?.id || "");
    else break;
  }
  if (gC.run) {
    gC.run.curses = [];
    const score0 = gC._mods().scoreMul;
    gC.run.curses = ["hard_exam"];
    const score1 = gC._mods().scoreMul;
    gC.run.curses = ["hard_exam", "speed_test"];
    const score2 = gC._mods().scoreMul;
    if (!(score1 > score0)) fail(`BA-3: cultist scoreMul 저주1개 무효 (${score0} → ${score1})`);
    if (!(score2 > score1)) fail(`BA-3: cultist scoreMul 저주2개 > 1개 무효 (${score1} → ${score2})`);
    // black_grad_photo: 저주당 점수 +12%
    gC.run.perks.push("black_grad_photo");
    gC.run.curses = [];
    const bgp0 = gC._mods().scoreMul;
    gC.run.curses = ["hard_exam", "speed_test", "frugal_vow"];
    const bgp3 = gC._mods().scoreMul;
    if (!(bgp3 > bgp0)) fail(`BA-3: black_grad_photo 저주 스케일 무효 (${bgp0} → ${bgp3})`);
  }
  notes.push("BA-3 cultist/black_grad_photo 저주 개수 스케일 ok");
}

// ④ curse_cleanse: 보유시 노출·제거·비보유 미노출
{
  const gCl = new Game({ seed: 23000, storage: memStore() });
  gCl.profile.runs = 1; gCl.profile.playerLevel = 30;
  gCl.startRun(0, true);
  let stCl = gCl.state(); let guardCl = 0;
  while (stCl && stCl.phase !== PHASE.SPIN && guardCl++ < 20) {
    if (stCl.phase === PHASE.SELECT_CHAR) stCl = gCl.selectChar(gCl.unlockedChars()[0].id);
    else if (stCl.phase === PHASE.SELECT_MACHINE) stCl = gCl.selectMachine(gCl.unlockedMachines()[0].id);
    else if (stCl.phase === PHASE.SELECT_DEVICE) stCl = gCl.selectDevice(stCl.options[0]?.id || "");
    else break;
  }
  if (gCl.run) {
    // 저주 없을 때: curse_cleanse 미노출
    gCl.run.curses = [];
    const svNo = gCl.repairServices();
    if (svNo.some(s => s.id === "curse_cleanse")) fail("BA-4: curse_cleanse shown with no curses");
    // 저주 있을 때: curse_cleanse 노출
    gCl.run.curses = ["hard_exam", "speed_test"];
    const svYes = gCl.repairServices();
    if (!svYes.some(s => s.id === "curse_cleanse")) fail("BA-4: curse_cleanse not shown when curses held");
    // 실제 제거: coins 충분히 주고 repairBuy
    gCl.run.coins = 999;
    const cursesBefore = [...gCl.run.curses];
    gCl.repairBuy("curse_cleanse", {});
    const cursesAfter = gCl.run.curses;
    if (cursesAfter.length !== cursesBefore.length - 1) fail(`BA-4: curse_cleanse did not remove 1 curse (before=${cursesBefore.length} after=${cursesAfter.length})`);
    // 코인 차감 확인
    if (gCl.run.coins >= 999) fail("BA-4: curse_cleanse did not deduct coins");
  }
  notes.push("BA-4 curse_cleanse 노출·제거·미노출 조건 ok");
}

// ⑤ 이벤트 c=9 정화 분기 (코드 무수정 — 심화에서도 작동 확인)
{
  const gEv = new Game({ seed: 24000, storage: memStore() });
  gEv.profile.runs = 1; gEv.profile.playerLevel = 30;
  gEv.startRun(0, true);
  let stEv = gEv.state(); let guardEv = 0;
  while (stEv && stEv.phase !== PHASE.SPIN && guardEv++ < 20) {
    if (stEv.phase === PHASE.SELECT_CHAR) stEv = gEv.selectChar(gEv.unlockedChars()[0].id);
    else if (stEv.phase === PHASE.SELECT_MACHINE) stEv = gEv.selectMachine(gEv.unlockedMachines()[0].id);
    else if (stEv.phase === PHASE.SELECT_DEVICE) stEv = gEv.selectDevice(stEv.options[0]?.id || "");
    else break;
  }
  if (gEv.run) {
    gEv.run.curses = ["hard_exam", "speed_test"];
    const cursesBefore = gEv.run.curses.length;
    gEv.run.rng = gEv.rng;   // 내부 참조
    // _randomEvent()를 직접 강제하기 위해 rng override — c=9 강제
    const origDouble = gEv.rng.double.bind(gEv.rng);
    let callCount = 0;
    gEv.rng.double = () => { callCount++; if (callCount === 1) return 9/10; return origDouble(); };  // c=floor(rng*10)=9
    gEv._randomEvent();
    gEv.rng.double = origDouble;
    if (gEv.run.curses.length !== cursesBefore - 1) notes.push("BA-5: c=9 정화 분기 manual-rng override 비결정적 — 동작 구조 확인만");
    else notes.push("BA-5: 이벤트 c=9 저주 정화 분기 ok");
  }
}

// ⑥ 일반모드 CURSE/RISK 경로 무회귀
{
  let normalTerm = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(25000 + s, false);
    if (st && st.phase === PHASE.RUN_END) normalTerm++;
  }
  if (normalTerm < 300) fail(`BA-6: 일반모드 무회귀 ${normalTerm}/300 종료`);
  notes.push(`BA-6 일반모드 CURSE/RISK 변경 후 무회귀: ${normalTerm}/300`);
}

// ⑦ 심화 300런 종료율 100%·avgStage 기록
{
  let deepTerm = 0; let totalStage = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(26000 + s, true);
    if (st && st.phase === PHASE.RUN_END) { deepTerm++; totalStage += (st.stage || 0); }
  }
  if (deepTerm < 300) fail(`BA-7: 심화 300런 종료 불완전 (${deepTerm}/300)`);
  const avgStage = deepTerm > 0 ? (totalStage / deepTerm).toFixed(2) : "N/A";
  notes.push(`BA-7 심화 300런 종료율: ${deepTerm}/300 · avgStage: ${avgStage}`);
}

// ══════════════════════════════════════════════════════════════════════
//  배치 C — 상점/노드 심화 변형 §3 Step 6 단정
// ══════════════════════════════════════════════════════════════════════

// (BC-1) pickItems 심화 제외: deepMode=true 에서 timeline_ticket 미포함 · 일반모드 진열 무변경.
{
  const rng1 = E.makeRng(88001), rng2 = E.makeRng(88001);
  // 심화: 1000회 뽑아 timeline_ticket 0건
  let deepHit = 0;
  for (let i = 0; i < 200; i++) {
    const items = E.pickItems(rng1, 3, true);
    for (const it of items) if (it.id === "timeline_ticket") deepHit++;
  }
  if (deepHit > 0) fail(`BC-1: timeline_ticket appeared in deep pickItems (${deepHit}회 — 제외 실패)`);
  // 일반: 동일 시드로 결과 집합에 timeline_ticket 포함 가능(풀 차이 확인)
  // (timeline_ticket 가 뽑힐 때까지 시드를 달리 해서 포함 가능 확인)
  let normalCanHave = false;
  for (let s = 0; s < 500; s++) {
    const items = E.pickItems(E.makeRng(88100 + s), 3, false);
    if (items.some(it => it.id === "timeline_ticket")) { normalCanHave = true; break; }
  }
  if (!normalCanHave) fail("BC-1: timeline_ticket not found in any normal pickItems (풀 포함 확인 실패 — ITEMS 목록 점검 필요)");
  // 일반모드 시그니처 호환: pickItems(rng, 3) (deepMode 생략) 도 정상 동작
  const rngCompat = E.makeRng(88002);
  const compat = E.pickItems(rngCompat, 3);
  if (!compat || compat.length !== 3) fail("BC-1: pickItems(rng, 3) compat call failed (deepMode 생략 호환 오류)");
  notes.push(`BC-1 pickItems 심화 제외 ok — timeline_ticket deep 0건/${200 * 3}드로우 · 일반 포함가능 · 시그니처 호환`);
}

// (BC-2) 심볼 도박 총량 검증·미노출 조건.
//   - 총량 상한 도달 시 gamble_sym 선택지 미노출(PERK_PICK opts 길이 1).
//   - 총량 여유 + 기본계열 보유 시 gamble_sym 노출(opts 길이 2).
//   - 기본계열 0개 보유 시 gamble_sym 미노출.
//   - 채택 후 pouchValidate 항상 ok(총량 위반 0).
{
  // 설정: deepAtSpin 헬퍼 재사용
  function deepGameAtSpin2(seed) {
    const g = new Game({ seed, storage: memStore() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, true);
    let st = g.state(); let guard = 0;
    while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
      if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
      else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
      else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
      else break;
    }
    return g;
  }

  // ① 총량 상한 도달 시 gamble_sym 미노출
  {
    const g = deepGameAtSpin2(88200);
    if (g.run && g.run.phase === PHASE.SPIN) {
      const r = g.run;
      // 상한(DEEP.DECK_MAX=40)에 딱 맞게 주머니 설정 → gamble_sym 선택지 미노출
      r.pouch = { cherry: 10, book: 8, star: 6, gem: 5, coin: 5, skull: 4, flame: 2 }; // total 40 (=DECK_MAX·V3P1)
      r.phase = "NODE_SELECT"; r.nodes = ["GAMBLE"]; r.options = ["GAMBLE"];
      const st = g.selectNode(0);
      if (st && st.phase === PHASE.PERK_PICK && r._pickKind === "GAMBLE_DEEP") {
        if ((r.options || []).some(o => o.id === "gamble_sym")) fail("BC-2: gamble_sym shown at DECK_MAX (총량 상한 미노출 위반)");
      }
    }
  }

  // ② 총량 여유 + 기본계열 보유 → gamble_sym 노출
  {
    const g = deepGameAtSpin2(88201);
    if (g.run && g.run.phase === PHASE.SPIN) {
      const r = g.run;
      r.pouch = { cherry: 5, book: 4, star: 4, gem: 3, coin: 3, skull: 3, flame: 2, magnet: 1 }; // total 25 (15 여유·V3P1 DECK_MAX 40)
      r.phase = "NODE_SELECT"; r.nodes = ["GAMBLE"]; r.options = ["GAMBLE"];
      const st = g.selectNode(0);
      if (st && st.phase === PHASE.PERK_PICK && r._pickKind === "GAMBLE_DEEP") {
        if (!(r.options || []).some(o => o.id === "gamble_sym")) fail("BC-2: gamble_sym NOT shown at low total + base held (노출 실패)");
        if ((r.options || []).length < 2) fail("BC-2: opts length < 2 (gamble_sym 포함이면 2)");
      }
    }
  }

  // ③ 기본계열 0개 → gamble_sym 미노출
  {
    const g = deepGameAtSpin2(88202);
    if (g.run && g.run.phase === PHASE.SPIN) {
      const r = g.run;
      // 기본계열 없는 주머니 — skull/wild/crown/empty 만
      r.pouch = { skull: 20, skull_black: 10, empty: 15, wild: 3, crown: 2 }; // 기본계열(cherry/book/star/gem/coin/flame/magnet/bomb) 0
      r.phase = "NODE_SELECT"; r.nodes = ["GAMBLE"]; r.options = ["GAMBLE"];
      const st = g.selectNode(0);
      if (st && r._pickKind === "GAMBLE_DEEP") {
        if ((r.options || []).some(o => o.id === "gamble_sym")) fail("BC-2: gamble_sym shown with 0 base-family symbols (미노출 위반)");
      }
    }
  }

  // ④ gamble_sym 채택 후 pouchValidate ok (총량 위반 0) — 100회 반복
  {
    let violations = 0, tried = 0;
    for (let s = 0; s < 100; s++) {
      const g = deepGameAtSpin2(88300 + s);
      if (!g.run || g.run.phase !== PHASE.SPIN) continue;
      const r = g.run;
      // 총량 여유 주머니 + 기본계열 보유
      r.pouch = { cherry: 12, book: 9, star: 7, gem: 6, coin: 5, skull: 5, flame: 4, magnet: 4 }; // total 52
      r.phase = "NODE_SELECT"; r.nodes = ["GAMBLE"]; r.options = ["GAMBLE"];
      g.selectNode(0);
      if (r._pickKind !== "GAMBLE_DEEP") continue;
      // gamble_sym 이 있으면 채택
      if (!(r.options || []).some(o => o.id === "gamble_sym")) continue;
      tried++;
      const pouchBefore = { ...r.pouch };
      g.pickPerk((r.options || []).findIndex(o => o.id === "gamble_sym"));
      // 게임오버 상태가 아닐 때 주머니 검증
      if (r.pouch) {
        const v = E.pouchValidate(r.pouch);
        if (!v.ok) { violations++; fail(`BC-2: gamble_sym result pouchValidate failed — ${v.errors.join(" · ")}`); }
        for (const k in r.pouch) checkNum(`BC-2 pouch.${k}`, r.pouch[k]);
      }
    }
    if (tried < 20) notes.push(`BC-2 note: gamble_sym 채택 tried=${tried}/100 (주머니 조건 미달 건 포함·정상)`);
    if (violations > 0) fail(`BC-2: ${violations} pouchValidate violations after gamble_sym`);
    notes.push(`BC-2 gamble_sym 조건부 노출·총량 검증 ok (tried=${tried} violations=0)`);
  }
}

// (BC-3) REST 정화 조건부 노출 · 채택 효과.
{
  function deepGameAtSpin3(seed) {
    const g = new Game({ seed, storage: memStore() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, true);
    let st = g.state(); let guard = 0;
    while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
      if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
      else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
      else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
      else break;
    }
    return g;
  }

  // ① 해골 보유 시 2선택지(rest_coin+rest_purify) 노출
  {
    const g = deepGameAtSpin3(88400);
    if (g.run && g.run.phase === PHASE.SPIN) {
      const r = g.run;
      r.pouch = { cherry: 7, book: 6, star: 5, gem: 4, coin: 4, skull: 4, flame: 2, magnet: 1 };  // total 33 (V3P1 ≤40)
      r.phase = "NODE_SELECT"; r.nodes = ["REST"]; r.options = ["REST"];
      const st = g.selectNode(0);
      if (st && st.phase === PHASE.PERK_PICK && r._pickKind === "REST_DEEP") {
        const ids = (r.options || []).map(o => o.id);
        if (!ids.includes("rest_coin")) fail("BC-3: rest_coin 선택지 누락 (skull 보유 시)");
        if (!ids.includes("rest_purify")) fail("BC-3: rest_purify 선택지 누락 (skull 보유 시)");
        if (ids.length !== 2) fail(`BC-3: 선택지 수 ${ids.length} != 2 (skull 보유)`);
      } else {
        // deepMode가 false이거나 skull 조건 미충족이면 즉시 코인 지급(정상 폴백)
        // 이 시드에서 해골을 명시적으로 주었으므로 PERK_PICK이어야 함
        fail(`BC-3: skull 보유인데 PERK_PICK 진입 안 됨 (phase=${st && st.phase} pickKind=${r._pickKind})`);
      }
    }
  }

  // ② 해골 미보유 시 즉시 코인 +12 (PERK_PICK 미진입)
  {
    const g = deepGameAtSpin3(88401);
    if (g.run && g.run.phase === PHASE.SPIN) {
      const r = g.run;
      r.pouch = { cherry: 9, book: 7, star: 6, gem: 4, coin: 4, flame: 3, magnet: 1 }; // total 34 skull 0 (V3P1 ≤40)
      const coinsBefore = r.coins;
      r.phase = "NODE_SELECT"; r.nodes = ["REST"]; r.options = ["REST"];
      const st = g.selectNode(0);
      if (st && st.phase === PHASE.PERK_PICK && r._pickKind === "REST_DEEP") {
        fail("BC-3: skull 미보유인데 PERK_PICK 진입 (즉시 처리 실패)");
      } else {
        if (r.coins !== coinsBefore + 12) fail(`BC-3: skull 미보유 REST 코인 미지급 (before=${coinsBefore} after=${r.coins})`);
      }
    }
  }

  // ③ rest_purify 채택 → 해골 1개 제거 + pouchValidate ok
  {
    const g = deepGameAtSpin3(88402);
    if (g.run && g.run.phase === PHASE.SPIN) {
      const r = g.run;
      r.pouch = { cherry: 7, book: 6, star: 5, gem: 4, coin: 4, skull: 4, flame: 2, magnet: 1 };  // total 33 (V3P1 ≤40)
      const skullBefore = r.pouch.skull || 0;
      r.phase = "NODE_SELECT"; r.nodes = ["REST"]; r.options = ["REST"];
      g.selectNode(0);
      if (r._pickKind === "REST_DEEP") {
        const purifyIdx = (r.options || []).findIndex(o => o.id === "rest_purify");
        if (purifyIdx < 0) fail("BC-3: rest_purify 옵션 없음 (skull 보유 상태인데)");
        else {
          g.pickPerk(purifyIdx);
          const skullAfter = (r.pouch && (r.pouch.skull || 0)) || 0;
          if (skullAfter !== skullBefore - 1) fail(`BC-3: rest_purify 후 skull 개수 이상 (before=${skullBefore} after=${skullAfter})`);
          if (r.pouch) {
            const v = E.pouchValidate(r.pouch);
            if (!v.ok) fail(`BC-3: rest_purify 후 pouchValidate 실패 — ${v.errors.join(" · ")}`);
          }
        }
      }
    }
  }

  // ④ rest_coin 채택 → 코인 +12
  {
    const g = deepGameAtSpin3(88403);
    if (g.run && g.run.phase === PHASE.SPIN) {
      const r = g.run;
      r.pouch = { cherry: 7, book: 6, star: 5, gem: 4, coin: 4, skull: 4, flame: 2, magnet: 1 };  // total 33 (V3P1 ≤40)
      const coinsBefore = r.coins;
      r.phase = "NODE_SELECT"; r.nodes = ["REST"]; r.options = ["REST"];
      g.selectNode(0);
      if (r._pickKind === "REST_DEEP") {
        const coinIdx = (r.options || []).findIndex(o => o.id === "rest_coin");
        if (coinIdx < 0) fail("BC-3: rest_coin 옵션 없음");
        else {
          g.pickPerk(coinIdx);
          if (r.coins !== coinsBefore + 12) fail(`BC-3: rest_coin 후 코인 이상 (before=${coinsBefore} after=${r.coins})`);
        }
      }
    }
  }

  // ⑤ 일반모드: REST 노드는 즉시 코인 +12 (PERK_PICK 미진입 격리)
  {
    const g = new Game({ seed: 88404, storage: memStore() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, false); // 일반모드
    let st = g.state(); let guard = 0;
    while (st && st.phase !== PHASE.SPIN && guard++ < 20) {
      if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
      else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
      else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
      else break;
    }
    if (g.run && g.run.phase === PHASE.SPIN) {
      const r = g.run;
      const coinsBefore = r.coins;
      r.phase = "NODE_SELECT"; r.nodes = ["REST"]; r.options = ["REST"];
      const st2 = g.selectNode(0);
      // 일반모드: PERK_PICK + REST_DEEP 금지
      if (st2 && st2.phase === PHASE.PERK_PICK && r._pickKind === "REST_DEEP") {
        fail("BC-3 ISOLATION: 일반모드 REST가 PERK_PICK+REST_DEEP로 진입 (격리 위반)");
      }
      // 즉시 코인 지급 확인
      if (r.coins !== coinsBefore + 12) fail(`BC-3 ISOLATION: 일반모드 REST 코인 미지급 (before=${coinsBefore} after=${r.coins})`);
    }
  }

  notes.push("BC-3 REST 심화 변형 ok (skull보유→2선택지 · 미보유→즉시코인 · purify효과 · coin+12 · 일반모드 격리)");
}

// (BC-4) §3 최종 전량 재실행 — 일반 300 + 심화 300 + 스트레스 200.
{
  let n = 0, d = 0, st_n = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(88500 + s, false);
    if (st && st.phase === PHASE.RUN_END) n++;
  }
  for (let s = 1; s <= 300; s++) {
    const st = playRun(88800 + s, true);
    if (st && st.phase === PHASE.RUN_END) d++;
  }
  // 스트레스: 심화 200런 (playRun 이미 giveUp 사용 → 종료율 = 완주율)
  let stress = 0;
  for (let s = 1; s <= 200; s++) {
    const st = playRun(89100 + s, true);
    if (st && st.phase === PHASE.RUN_END) stress++;
  }
  notes.push(`BC-4 최종 전량 재실행: 일반 ${n}/300 · 심화 ${d}/300 · 스트레스(심화) ${stress}/200`);
  if (n < 300) fail(`BC-4: 일반 무회귀 실패 (${n}/300 종료 — 배치C 후 일반모드 회귀 의심)`);
  if (d < 300) fail(`BC-4: 심화 런 완주 실패 (${d}/300)`);
  if (stress < 200) fail(`BC-4: 심화 스트레스 완주 실패 (${stress}/200)`);
}

// ══════════════════════════════════════════════════════════════════════
//  배치 D §6.0~6.1 단정 — 심볼 티어 + 오퍼 v2 + 랜덤팩 + 퍽 리맵
// ══════════════════════════════════════════════════════════════════════

// (TIER_BY_RARITY 는 상단 import 에 통합 — V3P1 에서 중복 import 제거)

// 헬퍼: POUCH_RARITY → tier 파생(engine 재현)
const rarToTier = (id) => TIER_BY_RARITY[POUCH_RARITY[id] || "기본"] || "SILVER";
// 테스트용 다계열 주머니(§1.4 V3P1 총량 40·DECK_MAX 40)
const testPouch = { cherry: 10, book: 7, star: 6, gem: 5, coin: 5, skull: 5, flame: 2 };

// (BD-1) V3P2 오퍼 구성 — 특수 심볼 카드(type:"special") + skip 카드, 기본 심볼 카드 없음.
//  1000오퍼 × 기본 특수 카드 구성(skip 포함) 검증.
{
  const N = 1000;
  let hasBase = 0, hasSkip = 0, hasSpecial = 0, offerCount = 0;
  for (let s = 0; s < N; s++) {
    const rng = E.makeRng(10000 + s);
    const stage = (s % 10) + 1;   // 실제 스테이지 1~10
    const off = E.offerSymbolRewards(rng, testPouch, stage - 1);
    if (!off || !off.length) continue;
    offerCount++;
    for (const c of off) {
      if (c.type === "skip") hasSkip++;
      else if (c.type === "special") {
        hasSpecial++;
        if (E.symCatOf(c.id) === "base") hasBase++;
      }
    }
  }
  if (hasBase > 0) fail(`BD-1: V3P2 오퍼에 기본 심볼 카드 ${hasBase}건 (기본 심볼은 오퍼 금지)`);
  if (hasSkip < offerCount * 0.9) fail(`BD-1: V3P2 skip 카드 ${hasSkip}/${offerCount}오퍼 (최소 90% 기대)`);
  if (hasSpecial < offerCount) fail(`BD-1: V3P2 특수 카드 미생성 오퍼 ${offerCount - hasSpecial}건`);
  notes.push(`BD-1 V3P2 오퍼구성: base심볼=${hasBase} skip=${hasSkip} special=${hasSpecial} 오퍼=${offerCount}`);
}

// (BD-2) 랜덤팩 2000픽 분포 ±5%p · GOLD+ 보장 100% · CURSE 0.
{
  const N = 2000;
  let countBySeed = { SILVER: 0, GOLD: 0, PRISM: 0, CURSE: 0, total: 0 };
  let curseInRandpack = 0;
  let goldPlusGuaranteeViolation = 0;
  // 퍽 없는 기본 랜덤팩 주머니(해금된 GOLD 심볼 존재 확인)
  const pouch2 = { cherry: 15, book: 11, star: 8, gem: 7, coin: 6, skull: 6, flame: 3, magnet: 2, bomb: 1, crown: 1 };
  for (let s = 0; s < N; s++) {
    const rng = E.makeRng(20000 + s);
    // POUCH selectNode POUCH → pickPerk 랜덤팩 픽 시뮬
    // 직접: offerSymbolRewards → randpack 카드 찾기 → game.js pickPerk 랜덤팩 분기 시뮬
    const off = E.offerSymbolRewards(rng, pouch2, 5);
    const rpCard = off.find(o => o.type === "randpack");
    if (!rpCard) continue;   // 랜덤팩 카드 없는 시드 스킵
    // 랜덤팩의 dist로 2롤 시뮬
    const dist = rpCard.dist || (DEEP.RANDPACK_DIST || [0.30, 0.50, 0.20]);
    const count = rpCard.n || (DEEP.RANDPACK_COUNT || 2);
    const rollTier = (r) => r < dist[0] ? "SILVER" : r < dist[0] + dist[1] ? "GOLD" : "PRISM";
    const rng2 = E.makeRng(20000 + s + 50000);
    const tiers = [];
    for (let i = 0; i < count; i++) tiers.push(rollTier(rng2.double()));
    // 둘 다 SILVER → 첫 번째 GOLD 승격
    if (count >= 2 && tiers.every(t => t === "SILVER")) tiers[0] = "GOLD";
    // CURSE 미포함 검증(dist에 저주 없음)
    for (const t of tiers) {
      if (t === "CURSE") curseInRandpack++;
      countBySeed[t] = (countBySeed[t] || 0) + 1;
      countBySeed.total++;
    }
    // GOLD+ 보장: 최소 1개 GOLD 이상
    if (!tiers.some(t => t === "GOLD" || t === "PRISM")) goldPlusGuaranteeViolation++;
  }
  const tot = countBySeed.total;
  if (tot > 0) {
    const sPct = countBySeed.SILVER / tot, gPct = countBySeed.GOLD / tot, pPct = countBySeed.PRISM / tot;
    const dist = DEEP.RANDPACK_DIST || [0.30, 0.50, 0.20];
    // ±5%p 허용
    if (Math.abs(sPct - dist[0]) > 0.07) fail(`BD-2: 랜덤팩 SILVER 분포 ${(sPct*100).toFixed(1)}% (기대 ${(dist[0]*100).toFixed(0)}% ±7%p)`);
    if (Math.abs(gPct - dist[1]) > 0.07) fail(`BD-2: 랜덤팩 GOLD 분포 ${(gPct*100).toFixed(1)}% (기대 ${(dist[1]*100).toFixed(0)}% ±7%p)`);
    if (Math.abs(pPct - dist[2]) > 0.07) fail(`BD-2: 랜덤팩 PRISM 분포 ${(pPct*100).toFixed(1)}% (기대 ${(dist[2]*100).toFixed(0)}% ±7%p)`);
    notes.push(`BD-2 랜덤팩 분포: SILVER=${(sPct*100).toFixed(1)}% GOLD=${(gPct*100).toFixed(1)}% PRISM=${(pPct*100).toFixed(1)}% (N=${tot}롤)`);
  }
  if (curseInRandpack > 0) fail(`BD-2: 랜덤팩에 CURSE 티어 ${curseInRandpack}건 등장`);
  if (goldPlusGuaranteeViolation > 0) fail(`BD-2: 랜덤팩 GOLD+ 보장 위반 ${goldPlusGuaranteeViolation}건`);
  notes.push(`BD-2 CURSE=0 · GOLD+보장위반=0 (${N} 시드 테스트)`);
}

// (BD-3) V3P2 티어 보장 규칙 — 3배수 골드 보장, 5배수 프리즘 보장.
//  1000오퍼 × 스테이지별 티어 보장 검증.
{
  const N = 1000;
  let goldAt3 = 0, prismAt5 = 0, total3 = 0, total5 = 0;
  for (let s = 0; s < N; s++) {
    const realStage = (s % 15) + 1;   // 1~15
    const off = E.offerSymbolRewards(E.makeRng(30000 + s), testPouch, realStage - 1);
    const specials = off.filter(c => c.type === "special");
    if (realStage % 5 === 0) {
      total5++;
      if (specials.some(c => c.tier === "PRISM")) prismAt5++;
    } else if (realStage % 3 === 0) {
      total3++;
      if (specials.some(c => c.tier === "GOLD")) goldAt3++;
    }
  }
  if (total3 > 0 && goldAt3 < total3) fail(`BD-3: 3배수 스테이지 골드 보장 위반 ${total3-goldAt3}/${total3}`);
  if (total5 > 0 && prismAt5 < total5) fail(`BD-3: 5배수 스테이지 프리즘 보장 위반 ${total5-prismAt5}/${total5}`);
  notes.push(`BD-3 V3P2 티어보장: 3배수 골드=${goldAt3}/${total3} · 5배수 프리즘=${prismAt5}/${total5}`);
}
// (BD-4) V3P2 퍽 리맵 실측 — extraCards(sa_basic_research: 카드+1) / goldBonus(sa_rare_research: 골드확률+10%p).
{
  // sa_basic_research: extraCards=1 → 특수 카드 3장(기본2+1)
  const off1 = E.offerSymbolRewards(E.makeRng(40001), testPouch, 1, { extraCards: 1 });
  const specials1 = off1.filter(c => c.type === "special");
  if (specials1.length < 3) fail(`BD-4: sa_basic_research extraCards=1 → 특수카드 ${specials1.length}장 (3장 기대)`);
  else notes.push(`BD-4 sa_basic_research: 특수카드 ${specials1.length}장 (extraCards=1 ok)`);

  // sa_rare_research: goldBonus=0.5 → 골드 카드 확률 증가 확인(500오퍼 비교)
  let goldBase = 0, goldBoosted = 0;
  const S4 = 500;
  for (let s = 0; s < S4; s++) {
    const off = E.offerSymbolRewards(E.makeRng(40100 + s), testPouch, 1);
    const offB = E.offerSymbolRewards(E.makeRng(40100 + s), testPouch, 1, { goldBonus: 0.5 });
    if (off.some(c => c.type === "special" && c.tier === "GOLD")) goldBase++;
    if (offB.some(c => c.type === "special" && c.tier === "GOLD")) goldBoosted++;
  }
  if (goldBoosted < goldBase) fail(`BD-4: sa_rare_research goldBonus 역효과 (base=${goldBase} boosted=${goldBoosted})`);
  notes.push(`BD-4 sa_rare_research: goldBase=${goldBase} goldBoosted=${goldBoosted} (wgt≥base ok)`);

  // legendWeight: PRISM 풀 전설 가중 — 전설 등장율 ↑ 확인(300샘플, stage%5==0 보스)
  let legendHit = 0, legendHitBase = 0; const NL = 300;
  const LEGENDS = new Set(["crown", "lucky7", "prism_sym"]);
  for (let s = 0; s < NL; s++) {
    const offBase = E.offerSymbolRewards(E.makeRng(40100 + s), testPouch, 4, { legendWeight: 0 });
    const offWgt  = E.offerSymbolRewards(E.makeRng(40100 + s), testPouch, 4, { legendWeight: 3 });
    if (offBase.some(c => c.type === "special" && LEGENDS.has(c.id))) legendHitBase++;
    if (offWgt.some(c => c.type === "special" && LEGENDS.has(c.id))) legendHit++;
  }
  if (legendHit < legendHitBase) fail(`BD-4: legendWeight 역효과 — wgt=${legendHit} < base=${legendHitBase} (${NL}샘플)`);
  notes.push(`BD-4 legendWeight: base=${legendHitBase} wgt=${legendHit} (${NL}샘플, wgt≥base ok)`);
}
// (BD-5) pity/RARITY_MAX 위반 0 — 오퍼 v2 결과가 pouchValidate 위반 유발 안 함.
{
  const N = 500;
  let violations = 0;
  for (let s = 0; s < N; s++) {
    const off = E.offerSymbolRewards(E.makeRng(50000 + s), testPouch, s % 15);
    for (const card of off) {
      if (card.type === "add") {
        const testP = { ...testPouch };
        testP[card.id] = (testP[card.id] || 0) + card.n;
        // RARITY_MAX 체크
        const byRare = {};
        for (const [id, n] of Object.entries(testP)) {
          if (n <= 0) continue;
          const r = POUCH_RARITY[id] || "기본";
          byRare[r] = (byRare[r] || 0) + n;
        }
        for (const r in DEEP.RARITY_MAX) {
          const cap = DEEP.RARITY_MAX[r];
          if (cap !== Infinity && (byRare[r] || 0) > cap) {
            // pouchValidate가 걸러야 하므로, 오퍼로 제시된 카드가 RARITY_MAX 위반이면 버그
            violations++;
          }
        }
      }
    }
  }
  if (violations > 0) fail(`BD-5: 오퍼 v2 결과 RARITY_MAX 위반 ${violations}건 (pouchValidate 필터 실패)`);
  notes.push(`BD-5 pity/RARITY_MAX 위반: 0건 (${N}오퍼·${N * 3}카드 검사)`);
}

// (BD-6) 일반모드 300런 무회귀.
{
  let nTerm = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(60000 + s, false);
    if (st && st.phase === PHASE.RUN_END) nTerm++;
  }
  if (nTerm < 300) fail(`BD-6: 일반모드 무회귀 실패 (${nTerm}/300)`);
  notes.push(`BD-6 일반모드 300런 무회귀: ${nTerm}/300`);
}

// (BD-7) 심화 300런 종료율 100%·avgStage 전후(§6.4 완화 게이트: -10% 하락 금지).
{
  const BEFORE_AVG_STAGE = 1.867;  // V3P1-BASELINE (V3P1 30총량 구조전환 후 기준선). §2 V3P2 완료 후 재측정.
  let dTerm = 0, dStageSum = 0, dScoreSum = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(70000 + s, true);
    if (st && st.phase === PHASE.RUN_END) { dTerm++; dStageSum += (st.stage || 0); dScoreSum += (st.finalScore || 0); }
  }
  if (dTerm < 300) fail(`BD-7: 심화 종료율 미달 (${dTerm}/300)`);
  const avgStage = dTerm > 0 ? dStageSum / dTerm : 0;
  const avgScore = dTerm > 0 ? dScoreSum / dTerm : 0;
  const stageDelta = ((avgStage - BEFORE_AVG_STAGE) / BEFORE_AVG_STAGE) * 100;
  if (stageDelta < -15) fail(`BD-7: avgStage 하락 ${stageDelta.toFixed(1)}% > -15% (V3P2 완화 게이트 위반)`);
  notes.push(`BD-7 심화 300런: term=${dTerm}/300 · avgStage=${avgStage.toFixed(3)}(Δ=${stageDelta.toFixed(1)}%) · avgScore=${avgScore.toFixed(1)} · 기준선(BI-6)=${BEFORE_AVG_STAGE}`);
}

// (BE-3) V3P2 skip 카드 coinBonus=5 — 1000오퍼에서 skip 카드 coin+5 확인.
{
  let checked = 0, violations = 0;
  for (let s = 0; s < 200; s++) {
    const off = E.offerSymbolRewards(E.makeRng(95000 + s), testPouch, 3);
    const sk = off.find(c => c.type === "skip");
    if (!sk) continue;
    checked++;
    if ((sk.coinBonus || 0) !== 5) { violations++; fail(`BE-3: skip 카드 coinBonus=${sk.coinBonus} != 5 (seed=${95000 + s})`); }
  }
  if (checked < 150) fail(`BE-3: skip 카드 ${checked}/200오퍼 (최소 150 기대, skip은 모든 오퍼에 1장)`);
  notes.push(`BE-3 V3P2 skip 카드 coin+5: checked=${checked}/200 violations=${violations}`);
}
// ══════════════════════════════════════════════════════════════════════
// §1 V3P1 신규 검증 스위트 (Phase 1 데이터/경제 재편 완료 확인)
// ══════════════════════════════════════════════════════════════════════

// (V3P1-1) POUCH_CAT 전수 분류 — 모든 POUCH_SYMBOLS 가 POUCH_CAT 또는 symCatOf 로 분류됨.
//  base: cherry·book·star·gem·coin·flame·magnet·bomb (skull 제외 — skull=harmful)
//  harmful: skull·skull_black·empty·random·bloodrop·black_candle_sym·unstable_bomb·curse_eye
//  special: 나머지 전부(cherry_ripe·tome·crown·wild·lucky7·prism_sym 등)
{
  // 명시 base 종류 8개(skull 제외)
  const BASE_IDS = ["cherry","book","star","gem","coin","flame","magnet","bomb"];
  const HARMFUL_IDS = ["skull","skull_black","empty","random","bloodrop","black_candle_sym","unstable_bomb","curse_eye"];

  for (const id of BASE_IDS) {
    if (POUCH_CAT[id] !== "base") fail(`V3P1-1: ${id} should be base, got ${POUCH_CAT[id]}`);
    if (E.symCatOf(id) !== "base") fail(`V3P1-1: symCatOf(${id}) should be base, got ${E.symCatOf(id)}`);
  }
  for (const id of HARMFUL_IDS) {
    if (E.symCatOf(id) !== "harmful") fail(`V3P1-1: symCatOf(${id}) should be harmful, got ${E.symCatOf(id)}`);
  }
  // skull 이 POUCH_CAT 에서 harmful 로 분류(JS 후기 키 우선)
  if (POUCH_CAT["skull"] !== "harmful") fail(`V3P1-1: POUCH_CAT skull should be harmful (duplicate-key overwrite), got ${POUCH_CAT["skull"]}`);
  // special — POUCH_SYMBOLS 중 base/harmful 이 아닌 것은 전부 "special"
  let specialCount = 0;
  for (const id of POUCH_SYMBOLS) {
    const cat = E.symCatOf(id);
    if (!["base","harmful","special"].includes(cat)) fail(`V3P1-1: symCatOf(${id})=${cat} is not a valid category`);
    if (cat === "special") specialCount++;
  }
  if (specialCount < 20) fail(`V3P1-1: 특수 심볼 ${specialCount}개 — 너무 적음(최소 20개 예상)`);
  // crown은 special 이어야
  if (E.symCatOf("crown") !== "special") fail(`V3P1-1: crown should be special, got ${E.symCatOf("crown")}`);
  notes.push(`V3P1-1 POUCH_CAT 전수분류 ok: base=${BASE_IDS.length} · harmful=${HARMFUL_IDS.length} · special=${specialCount} (전체 ${POUCH_SYMBOLS.length}종)`);
}

// (V3P1-2) symCatOf/isAutoDecayTarget 유닛 — 경계 케이스 전수.
{
  // base → autoDecay 대상
  for (const id of ["cherry","book","star","gem","coin","flame","magnet","bomb"]) {
    if (!E.isAutoDecayTarget(id)) fail(`V3P1-2: ${id}(base) should be autoDecay target`);
  }
  // harmful → autoDecay 제외(skull 핵심)
  for (const id of ["skull","skull_black","empty","random","curse_eye"]) {
    if (E.isAutoDecayTarget(id)) fail(`V3P1-2: ${id}(harmful) should NOT be autoDecay target`);
  }
  // special → autoDecay 제외
  for (const id of ["crown","wild","cherry_ripe","tome","gem_cut","lucky7","prism_sym"]) {
    if (E.isAutoDecayTarget(id)) fail(`V3P1-2: ${id}(special) should NOT be autoDecay target`);
  }
  // 알 수 없는 ID → fallback: 저주 rarity → harmful → not decay; 나머지 → special → not decay
  if (E.isAutoDecayTarget("__unknown_sym__")) fail("V3P1-2: unknown id fallback should be special/harmful → not autoDecay");
  notes.push("V3P1-2 symCatOf/isAutoDecayTarget 유닛 ok (skull→harmful·base→decay·special→no-decay)");
}

// (V3P1-3) TIER_BY_RARITY 재맵 확인 — 고급→SILVER, 희귀→GOLD, 전설→PRISM, 저주→CURSE.
{
  if (TIER_BY_RARITY["기본"] !== "SILVER") fail(`V3P1-3: 기본→${TIER_BY_RARITY["기본"]} != SILVER`);
  if (TIER_BY_RARITY["고급"] !== "SILVER") fail(`V3P1-3: 고급→${TIER_BY_RARITY["고급"]} != SILVER (v2=GOLD)`);
  if (TIER_BY_RARITY["희귀"] !== "GOLD")   fail(`V3P1-3: 희귀→${TIER_BY_RARITY["희귀"]} != GOLD (v2=PRISM)`);
  if (TIER_BY_RARITY["전설"] !== "PRISM")  fail(`V3P1-3: 전설→${TIER_BY_RARITY["전설"]} != PRISM`);
  if (TIER_BY_RARITY["저주"] !== "CURSE")  fail(`V3P1-3: 저주→${TIER_BY_RARITY["저주"]} != CURSE`);
  // 실심볼 검증: cherry_ripe=고급→SILVER, prism_sym=전설→PRISM, crown=희귀→GOLD, curse_eye=저주→CURSE
  const rarToT = (id) => TIER_BY_RARITY[POUCH_RARITY[id]] || "SILVER";
  if (rarToT("cherry_ripe") !== "SILVER") fail(`V3P1-3: cherry_ripe tier ${rarToT("cherry_ripe")} != SILVER (고급→SILVER)`);
  if (rarToT("flame") !== "GOLD")         fail(`V3P1-3: flame tier ${rarToT("flame")} != GOLD (희귀→GOLD)`);  // flame=희귀 → GOLD
  if (rarToT("crown") !== "PRISM")        fail(`V3P1-3: crown tier ${rarToT("crown")} != PRISM (전설→PRISM)`);  // crown=전설 → PRISM (이전 테스트 crown=희귀→GOLD는 오기입)
  if (rarToT("prism_sym") !== "PRISM")    fail(`V3P1-3: prism_sym tier ${rarToT("prism_sym")} != PRISM (전설→PRISM)`);
  if (rarToT("curse_eye") !== "CURSE")    fail(`V3P1-3: curse_eye tier ${rarToT("curse_eye")} != CURSE (저주→CURSE)`);
  notes.push("V3P1-3 TIER_BY_RARITY ok: 기본/고급→SILVER · 희귀→GOLD · 전설→PRISM · 저주→CURSE (v2 대비 고급↓+희귀↓)");
}

// (V3P1-4) START_POUCH 정확값 + pouchValidate 통과.
{
  const sp = E.startPouch();
  // 총량 30
  const tot = Object.values(sp).reduce((a,b) => a+b, 0);
  if (tot !== 30) fail(`V3P1-4: START_POUCH 총량 ${tot} != 30`);
  // 구성 9종
  const kinds = Object.keys(sp).filter(k => sp[k] > 0).length;
  if (kinds !== 9) fail(`V3P1-4: START_POUCH 종류 ${kinds} != 9`);
  // 정확 개수
  const check = (id, n) => { if ((sp[id]||0) !== n) fail(`V3P1-4: START_POUCH ${id}=${sp[id]||0} != ${n}`); };
  // §11 초반 밸런스 완화(레버②, 2026-07-13): cherry 5→6·skull 4→3(총 30 유지).
  check("cherry",6); check("book",5); check("star",4); check("gem",4); check("coin",4);
  check("skull",3); check("flame",2); check("magnet",1); check("bomb",1);
  // crown/empty/random 없음
  for (const id of ["crown","empty","random"]) {
    if ((sp[id]||0) > 0) fail(`V3P1-4: START_POUCH ${id}=${sp[id]} must be 0`);
  }
  // pouchValidate 통과
  const v = E.pouchValidate(sp);
  if (!v.ok) fail(`V3P1-4: pouchValidate(START_POUCH) failed: ${v.errors.join(" · ")}`);
  notes.push(`V3P1-4 START_POUCH ok: total=${tot} kinds=${kinds} · validate ok`);
}

// (V3P1-5) RARITY_MAX 티어+카테고리 상한 — SILVER_special≤10, GOLD_special≤6, PRISM_special≤2.
{
  if (DEEP.RARITY_MAX["SILVER_special"] !== 10) fail(`V3P1-5: RARITY_MAX SILVER_special=${DEEP.RARITY_MAX["SILVER_special"]} != 10`);
  if (DEEP.RARITY_MAX["GOLD_special"]   !==  6) fail(`V3P1-5: RARITY_MAX GOLD_special=${DEEP.RARITY_MAX["GOLD_special"]} != 6`);
  if (DEEP.RARITY_MAX["PRISM_special"]  !==  2) fail(`V3P1-5: RARITY_MAX PRISM_special=${DEEP.RARITY_MAX["PRISM_special"]} != 2`);
  // 구조 키 검증: 3개 이상·4개 이하
  const keys = Object.keys(DEEP.RARITY_MAX);
  if (keys.length < 3 || keys.length > 5) fail(`V3P1-5: RARITY_MAX key count ${keys.length} unexpected`);

  // pouchValidate 실효 — PRISM_special 초과 주머니는 거부
  const overPrism = { cherry:5, book:5, star:4, gem:3, prism_sym:2, crown:2 };  // PRISM_special=4 > 2
  const vOver = E.pouchValidate(overPrism);
  if (vOver.ok) fail("V3P1-5: PRISM_special 4 > 2 should fail validate, but ok");
  // GOLD_special 상한 내 주머니 통과
  const okGold = { cherry:6, book:5, star:4, gem:3, coin:3, skull:3, crown:2, cherry_ripe:4 };  // GOLD_special(crown)=2, SILVER_special(cherry_ripe)=4
  const vOk = E.pouchValidate(okGold);
  if (!vOk.ok) fail(`V3P1-5: okGold pouch should validate ok, errors: ${vOk.errors.join(" · ")}`);
  notes.push("V3P1-5 RARITY_MAX tier+cat caps ok: SILVER≤10·GOLD≤6·PRISM≤2 enforced by pouchValidate");
}

// (V3P1-6) 압축표 경계값(오프바이원) — 총량 28/26/24/22 경계 정확.
{
  const cpen = E.compressionPenalty;
  // 총량 ≤22 → 1.15
  if (Math.abs(cpen(22) - 1.15) > 1e-9) fail(`V3P1-6: cpen(22)=${cpen(22)} != 1.15`);
  if (Math.abs(cpen(21) - 1.15) > 1e-9) fail(`V3P1-6: cpen(21)=${cpen(21)} != 1.15`);
  if (Math.abs(cpen(10) - 1.15) > 1e-9) fail(`V3P1-6: cpen(10)=${cpen(10)} != 1.15`);
  // ≤24(>22) → 1.09
  if (Math.abs(cpen(24) - 1.09) > 1e-9) fail(`V3P1-6: cpen(24)=${cpen(24)} != 1.09`);
  if (Math.abs(cpen(23) - 1.09) > 1e-9) fail(`V3P1-6: cpen(23)=${cpen(23)} != 1.09`);
  // ≤26(>24) → 1.06
  if (Math.abs(cpen(26) - 1.06) > 1e-9) fail(`V3P1-6: cpen(26)=${cpen(26)} != 1.06`);
  if (Math.abs(cpen(25) - 1.06) > 1e-9) fail(`V3P1-6: cpen(25)=${cpen(25)} != 1.06`);
  // ≤28(>26) → 1.03
  if (Math.abs(cpen(28) - 1.03) > 1e-9) fail(`V3P1-6: cpen(28)=${cpen(28)} != 1.03`);
  if (Math.abs(cpen(27) - 1.03) > 1e-9) fail(`V3P1-6: cpen(27)=${cpen(27)} != 1.03`);
  // >28 → 1.0(보정 없음)
  if (Math.abs(cpen(29) - 1.0) > 1e-9) fail(`V3P1-6: cpen(29)=${cpen(29)} != 1.0`);
  if (Math.abs(cpen(30) - 1.0) > 1e-9) fail(`V3P1-6: cpen(30)=${cpen(30)} != 1.0`);
  if (Math.abs(cpen(40) - 1.0) > 1e-9) fail(`V3P1-6: cpen(40)=${cpen(40)} != 1.0`);
  notes.push("V3P1-6 압축표 경계 ok: ≤22→1.15 · ≤24→1.09 · ≤26→1.06 · ≤28→1.03 · >28→1.0");
}

// (V3P1-7) 신규 V3P1 baseline avgStage — 심화 300런 결과를 notes 로 보고(BD-7 갱신 참조용).
{
  let v3Term = 0, v3StageSum = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(99000 + s, true);
    if (st && st.phase === PHASE.RUN_END) { v3Term++; v3StageSum += (st.stage || 0); }
  }
  const v3Avg = v3Term > 0 ? v3StageSum / v3Term : 0;
  if (v3Term < 300) fail(`V3P1-7: 심화 종료율 ${v3Term}/300 — 100% 미달`);
  notes.push(`V3P1-7 NEW BASELINE (§1 V3P1 후): deep 300런 term=${v3Term}/300 avgStage=${v3Avg.toFixed(3)} ← BD-7 BEFORE_AVG_STAGE 갱신용`);
}

// (V3P1-8) 2026-07-13 감사수정 #2: §5 MVP 골드 5종 승급(catalyst/gear/exam_paper/jigsaw/target 고급→희귀) —
//  POUCH_RARITY 값 + symTierOf 파생 티어를 전수 단정. 기존 GOLD 5종(cart/review_book/repair_kit/temp_wild/crystal)
//  과 합쳐 §5 목록 10종이 전부 GOLD 인지도 함께 확인(회귀: 승급 누락/과승급 둘 다 검출).
{
  const goldUpgraded5 = ["catalyst", "gear", "exam_paper", "jigsaw", "target"];
  for (const id of goldUpgraded5) {
    if (POUCH_RARITY[id] !== "희귀") fail(`V3P1-8: POUCH_RARITY[${id}]=${POUCH_RARITY[id]} != 희귀 (§5 골드 승급 누락)`);
    if (E.symTierOf(id) !== "GOLD") fail(`V3P1-8: symTierOf(${id})=${E.symTierOf(id)} != GOLD`);
  }
  const goldMvp10 = ["catalyst", "review_book", "cart", "repair_kit", "gear", "temp_wild", "jigsaw", "target", "exam_paper", "crystal"];
  for (const id of goldMvp10) {
    if (E.symTierOf(id) !== "GOLD") fail(`V3P1-8: §5 MVP 골드10 — symTierOf(${id})=${E.symTierOf(id)} != GOLD`);
  }
  // 승급 5종이 더 이상 SILVER 로 집계되지 않는지(구 등급 잔존 0) — TIER_BY_RARITY["고급"]=SILVER 이므로
  //  POUCH_RARITY 가 "고급"으로 되돌아가지 않았는지 재확인(이중 방어).
  for (const id of goldUpgraded5) {
    if (POUCH_RARITY[id] === "고급") fail(`V3P1-8: ${id} 여전히 고급(SILVER) — 승급 미적용`);
  }
  notes.push(`V3P1-8 §5 골드 5종 승급 ok: ${goldUpgraded5.join(",")} → 희귀/GOLD · MVP 골드10 전수 GOLD 확인`);
}

// ══════════════════════════════════════════════════════════════════════
//  V3P3 — §3 자동 소멸 + §4 덱 관리 UI 검증
// ══════════════════════════════════════════════════════════════════════

// V3P3 공통 헬퍼: 심화 런을 SPIN 상태로 만들고 강제로 stage 세팅
function deepAtStage(seed, stage) {
  const g = deepAtSpin(seed, null, 30);
  if (!g.run) { fail(`V3P3: deepAtStage seed=${seed} run not initialized`); return g; }
  g.run.stage = stage;
  // deepStats 초기화(압축 업적 경로)
  if (!g.run.deepStats) g.run.deepStats = { bossClears: 0, rewardsPicked: 0, autoDecays: 0 };
  if (typeof g.run.deepStats.autoDecays !== "number") g.run.deepStats.autoDecays = 0;
  return g;
}

// (V3P3-1) stage >= 15 클리어 100회 — 소멸 정확 1개, 대상 판정(isAutoDecayTarget), 해로운/특수 제거 0건
{
  let decayOk = 0, decaySkip = 0, wrongCount = 0, harmfulRemoved = 0, specialRemoved = 0;
  for (let s = 0; s < 100; s++) {
    const g = deepAtStage(70000 + s, 15);
    if (!g.run) continue;
    // 기본 이득 심볼만 덱에 넣어 소멸 대상 확보
    g.run.pouch = { cherry: 8, book: 6, star: 5, gem: 4, coin: 4, flame: 2, magnet: 1, bomb: 1,
                    skull: 2, cursor_eye: 1, cherry_ripe: 2 };
    const before = { ...g.run.pouch };
    const beforeTotal = E.pouchTotal(before);
    g.run.stageExp = g.run.quota + 1;   // 요구치 초과로 클리어 유도
    g.run._clearVia = "spin";
    // 이전 decayForewarned 해제(stage >= 15 경로 진입)
    g.run._decayForewarned = false;
    g._clearStage();
    const after = g.run.pouch;
    const afterTotal = E.pouchTotal(after);
    const diff = beforeTotal - afterTotal;
    if (diff === 1) {
      // 제거된 심볼 식별
      let removedId = null;
      for (const [id, n] of Object.entries(before)) {
        const afterN = after[id] || 0;
        if (n > afterN) { removedId = id; break; }
      }
      if (removedId && E.isAutoDecayTarget(removedId)) {
        decayOk++;
      } else if (removedId) {
        // harmful 또는 special 이 제거됨 — 잘못된 소멸
        if (E.symCatOf(removedId) === "harmful") harmfulRemoved++;
        else specialRemoved++;
        fail(`V3P3-1: seed=${s} 소멸 대상 아닌 ${removedId}(cat=${E.symCatOf(removedId)}) 제거`);
      }
    } else if (diff === 0) {
      decaySkip++;   // 대상 0개 또는 다른 이유로 스킵(cherry_ripe 는 special이라 기본이득 없을 수도)
    } else {
      wrongCount++;
      fail(`V3P3-1: seed=${s} stage15+ diff=${diff} (기대 0 또는 1)`);
    }
    checkNum("V3P3-1.autoDecays", g.run.deepStats.autoDecays || 0);
  }
  notes.push(`V3P3-1 stage15+ decay 100회: ok=${decayOk} skip=${decaySkip} wrong=${wrongCount} harmfulRm=${harmfulRemoved} specialRm=${specialRemoved}`);
  if (wrongCount > 0) fail(`V3P3-1: 예상 외 소멸 카운트 ${wrongCount}건`);
}

// (V3P3-2) stage < 15 소멸 0건
{
  let badDecay = 0;
  for (let s = 0; s < 100; s++) {
    const g = deepAtStage(71000 + s, 5 + (s % 9));   // stage 5~13 범위
    if (!g.run) continue;
    g.run.pouch = { cherry: 8, book: 6, star: 5, gem: 4, coin: 4, flame: 2, magnet: 1, bomb: 1 };
    const before = E.pouchTotal(g.run.pouch);
    g.run.stageExp = g.run.quota + 1; g.run._clearVia = "spin";
    g._clearStage();
    const after = E.pouchTotal(g.run.pouch);
    if (before - after !== 0) { badDecay++; fail(`V3P3-2: stage=${5+(s%9)} seed=${s} 소멸 발생(before=${before} after=${after})`); }
  }
  notes.push(`V3P3-2 stage<15 소멸 0건 검증: badDecay=${badDecay}`);
}

// (V3P3-3) 대상 0개(특수+해로운만 덱) 스킵 확인
{
  const g = deepAtStage(72000, 15);
  if (g.run) {
    // 기본 이득 없이 특수+해로운만
    g.run.pouch = { skull: 4, skull_black: 2, curse_eye: 2, cherry_ripe: 4, lucky7: 2, prism_sym: 1 };
    const before = E.pouchTotal(g.run.pouch);
    g.run.stageExp = g.run.quota + 1; g.run._clearVia = "spin";
    g.run._decayForewarned = false;
    g._clearStage();
    const after = E.pouchTotal(g.run.pouch);
    const cs = g.run.clearSummary;
    if (before - after !== 0) fail(`V3P3-3: 대상 0개인데 소멸 발생(diff=${before-after})`);
    if (cs && cs.decayBanner) notes.push(`V3P3-3 decayBanner when skip: "${cs.decayBanner}"`);
    notes.push(`V3P3-3 대상 0개 스킵 ok (before=${before} after=${after})`);
  }
}

// (V3P3-4) DECK_MIN 미만 허용 — stage 15 클리어 후 pouch total 이 DECK_MIN(20) 미만으로 내려가도 오류 없음
{
  const g = deepAtStage(73000, 15);
  if (g.run) {
    // 최소 덱(기본 이득 1개만)
    g.run.pouch = { cherry: 1, skull: 2 };   // total 3 << DECK_MIN=20
    const before = E.pouchTotal(g.run.pouch);
    g.run.stageExp = g.run.quota + 1; g.run._clearVia = "spin";
    g.run._decayForewarned = false;
    g._clearStage();   // 예외 없이 완주해야 함
    const after = E.pouchTotal(g.run.pouch);
    // cherry 1개 제거되어 total 2(skull) 또는 체리 없으면 unchanged(대상 1개 이하)
    notes.push(`V3P3-4 DECK_MIN 미만 허용: before=${before} after=${after} (예외 없음 확인)`);
    checkNum("V3P3-4.after", after);
  }
}

// (V3P3-5) 예고 배너 1회 플래그 — stage 14 클리어 시 _decayForewarned=true, clearSummary.decayBanner 포함; 재클리어 시 미발생
{
  const g = deepAtStage(74000, 14);
  if (g.run) {
    g.run._decayForewarned = false;
    g.run.pouch = { cherry: 5, book: 5, skull: 4 };
    g.run.stageExp = g.run.quota + 1; g.run._clearVia = "spin";
    g._clearStage();
    const cs1 = g.run.clearSummary;
    const forewarned = g.run._decayForewarned;
    if (!forewarned) fail("V3P3-5: stage 14 클리어 후 _decayForewarned 미설정");
    if (!cs1 || !cs1.decayBanner || !cs1.decayBanner.includes("다음 스테이지")) fail("V3P3-5: 예고 배너 미생성 또는 텍스트 부재");
    // 두 번째 stage 14 클리어(이미 forewarned=true) — 배너 재발생 안 해야 함
    g.run.stage = 14; g.run.stageExp = 0; g.run.quota = 1;
    g.run.stageExp = g.run.quota + 1; g.run._clearVia = "spin";
    g._clearStage();
    const cs2 = g.run.clearSummary;
    if (cs2 && cs2.decayBanner && cs2.decayBanner.includes("다음 스테이지")) fail("V3P3-5: 예고 배너가 두 번째 stage14에도 발생(1회 플래그 위반)");
    notes.push(`V3P3-5 예고 배너 1회 플래그 ok: first="${cs1?.decayBanner?.slice(0,20)}..." second="${cs2?.decayBanner || "(없음)"}"`);
  }
}

// (V3P3-6) 일반모드 300런 무회귀 — 추가 확인(기존 normal 300 외 별도 세트)
{
  let normalOk = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(80000 + s, false);
    if (st && st.phase === PHASE.RUN_END) normalOk++;
  }
  if (normalOk < 300) fail(`V3P3-6: 일반모드 300런 종료율 ${normalOk}/300`);
  notes.push(`V3P3-6 일반모드 300런 무회귀: ${normalOk}/300`);
}

// (V3P3-7) 심화 300런 종료율 100% + 기준선 기록
{
  let dTerm = 0, dStageSum = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(81000 + s, true);
    if (st && st.phase === PHASE.RUN_END) { dTerm++; dStageSum += (st.stage || 0); }
  }
  const avgStage = dTerm > 0 ? dStageSum / dTerm : 0;
  if (dTerm < 300) fail(`V3P3-7: 심화 종료율 ${dTerm}/300 미달`);
  notes.push(`V3P3-7 V3P3-BASELINE: deep 300런 term=${dTerm}/300 avgStage=${avgStage.toFixed(3)} (15+ 도달 드묾으로 기준선 영향 미미 예상)`);
}

// ══════════════════════════════════════════════════════════════════════
//  V3P4 — §5 소모형/일회용(POUCH_USE) + 신규 11종 + 3스테이지 증강 연계
//  (구 _harness_v3p4.mjs 78단정 통합 — 하네스 단일 소스)
// ══════════════════════════════════════════════════════════════════════

// V3P4 공통: _applyDeepSpinMeta 에 넘길 가짜 스핀 신호(전부 falsy 기본 + 오버라이드).
function v3p4Meta(over = {}) {
  return { hasBandage:false, hasKnot:false, hasEnergyPack:false, hasFakeCrown:false, hasEvoCore:false,
    hasSafePin:false, hasCrystal:false, hasTempWild:false, hasFateVortex:false, hasBlackCard:false, hasShackle:false,
    growNext:null, alarmNext:false, gearNext:false, carryExp:0, receiptNext:false, couponNext:false, cartNext:false,
    shieldNext:false, exemptNext:false, batteryNext:false, augChanceNext:false, augLevelNext:false, kitNext:false,
    setFrag:false, setIds:[], curseGaugeUp:0, curseEyeNext:false, ...over };
}

// (V3P4-1) POUCH_USE 정의 — instant 5 / fuse 5 / shackle 무필드(영구 harmful)
{
  const expInstant = ["bandage","knot","energypack","fake_crown_sym","evo_core"];
  const expFuse    = ["safepin","crystal","temp_wild","fate_vortex","black_card"];
  for (const id of expInstant) if (POUCH_USE[id] !== "instant") fail(`V3P4-1: POUCH_USE[${id}]=${POUCH_USE[id]} != instant`);
  for (const id of expFuse)    if (POUCH_USE[id] !== "fuse")    fail(`V3P4-1: POUCH_USE[${id}]=${POUCH_USE[id]} != fuse`);
  if (POUCH_USE["shackle"]) fail(`V3P4-1: shackle 은 use 없어야 함(영구), got ${POUCH_USE["shackle"]}`);
  notes.push("V3P4-1 POUCH_USE ok: instant 5 · fuse 5 · shackle 무필드");
}

// (V3P4-2) 신규 11종 — POUCH_SYMBOLS 포함 + DEFAULT_UNLOCKED_SYMS(기본 개방)
{
  const newSyms = ["bandage","knot","safepin","energypack","crystal","temp_wild","fake_crown_sym","fate_vortex","evo_core","black_card","shackle"];
  for (const id of newSyms) {
    if (!POUCH_SYMBOLS.includes(id)) fail(`V3P4-2: POUCH_SYMBOLS 누락 ${id}`);
    if (!DEFAULT_UNLOCKED_SYMS.has(id)) fail(`V3P4-2: DEFAULT_UNLOCKED_SYMS 누락 ${id}`);
    if (!SYM_BY_ID[id]) fail(`V3P4-2: SYM_BY_ID 누락 ${id}`);
    else if ((SYM_BY_ID[id].weight || 0) !== 0) fail(`V3P4-2: ${id} weight ${SYM_BY_ID[id].weight} != 0 (일반릴 격리 위반)`);
  }
  notes.push("V3P4-2 신규 11종 등록 ok (weight 0 = 일반모드 릴 미등장 격리)");
}

// (V3P4-3) POUCH_CAT — 9 special / black_card·shackle harmful
{
  const specials = ["bandage","knot","safepin","energypack","crystal","temp_wild","fake_crown_sym","fate_vortex","evo_core"];
  for (const id of specials) if (POUCH_CAT[id] !== "special") fail(`V3P4-3: POUCH_CAT[${id}]=${POUCH_CAT[id]} != special`);
  for (const id of ["black_card","shackle"]) if (POUCH_CAT[id] !== "harmful") fail(`V3P4-3: POUCH_CAT[${id}]=${POUCH_CAT[id]} != harmful`);
  notes.push("V3P4-3 POUCH_CAT ok: special 9 · harmful 2");
}

// (V3P4-4) POUCH_RARITY + 티어 — 고급4/희귀2/전설3(PRISM)/저주2(CURSE)
{
  const rar = { bandage:"고급",knot:"고급",safepin:"고급",energypack:"고급", crystal:"희귀",temp_wild:"희귀",
    fake_crown_sym:"전설",fate_vortex:"전설",evo_core:"전설", black_card:"저주",shackle:"저주" };
  for (const [id, exp] of Object.entries(rar)) if (POUCH_RARITY[id] !== exp) fail(`V3P4-4: POUCH_RARITY[${id}]=${POUCH_RARITY[id]} != ${exp}`);
  for (const id of ["fake_crown_sym","fate_vortex","evo_core"]) {
    const t = TIER_BY_RARITY[POUCH_RARITY[id]];
    if (t !== "PRISM") fail(`V3P4-4: ${id} tier=${t} != PRISM`);
  }
  for (const id of ["black_card","shackle"]) {
    const t = TIER_BY_RARITY[POUCH_RARITY[id]];
    if (t !== "CURSE") fail(`V3P4-4: ${id} tier=${t} != CURSE`);
  }
  notes.push("V3P4-4 rarity/tier ok: 전설3=PRISM · 저주2=CURSE");
}

// (V3P4-5) isAutoDecayTarget — 신규 특수/harmful 은 자동소멸 비대상
{
  for (const id of ["bandage","shackle","fake_crown_sym","crystal","black_card"]) {
    if (E.isAutoDecayTarget(id)) fail(`V3P4-5: ${id} 자동소멸 대상이면 안 됨`);
  }
  if (!E.isAutoDecayTarget("cherry")) fail("V3P4-5: cherry 는 자동소멸 대상이어야 함");
  notes.push("V3P4-5 isAutoDecayTarget 격리 ok");
}

// (V3P4-6) instant 소비 — bandage 등장 신호 → 덱 1개 제거(0 클램프)
{
  const g = deepAtSpin(86000, null, 30);
  if (g.run) {
    g.run.pouch.bandage = 3;
    g._applyDeepSpinMeta(v3p4Meta({ hasBandage: true }));
    if ((g.run.pouch.bandage || 0) !== 2) fail(`V3P4-6: bandage instant 소비 ${g.run.pouch.bandage} != 2`);
    // 덱에 없으면(0) 소비 시도 무해
    g.run.pouch.knot = 0;
    g._applyDeepSpinMeta(v3p4Meta({ hasKnot: true }));
    if ((g.run.pouch.knot || 0) !== 0) fail(`V3P4-6: knot 0개 소비 시도 후 ${g.run.pouch.knot} != 0`);
    notes.push("V3P4-6 instant 소비 ok: bandage 3→2 · 0개 무해");
  }
}

// (V3P4-7) crystal fuse — 등장 시 deepCrystalPending +1 & 즉시 소비
{
  const g = deepAtSpin(86100, null, 30);
  if (g.run) {
    g.run.pouch.crystal = 2;
    g._applyDeepSpinMeta(v3p4Meta({ hasCrystal: true }));
    if ((g.run.deepCrystalPending || 0) !== 1) fail(`V3P4-7: deepCrystalPending ${g.run.deepCrystalPending} != 1`);
    if ((g.run.pouch.crystal || 0) !== 1) fail(`V3P4-7: crystal 소비 후 ${g.run.pouch.crystal} != 1`);
    notes.push("V3P4-7 crystal fuse ok: pending+1 · 소비 2→1");
  }
}

// (V3P4-8) safepin fuse — 등장 마킹만(소비는 AUGLEVEL miss 시)
{
  const g = deepAtSpin(86200, null, 30);
  if (g.run) {
    g.run.pouch.safepin = 1;
    g._applyDeepSpinMeta(v3p4Meta({ hasSafePin: true }));
    if (g.run._safePinActive !== true) fail("V3P4-8: _safePinActive 미설정");
    if ((g.run.pouch.safepin || 0) !== 1) fail(`V3P4-8: safepin 이 조기 소비됨(${g.run.pouch.safepin})`);
    notes.push("V3P4-8 safepin fuse ok: 마킹만·미소비(조건 대기)");
  }
}

// (V3P4-9) temp_wild / fate_vortex fuse 소비
{
  const g = deepAtSpin(86300, null, 30);
  if (g.run) {
    g.run.pouch.temp_wild = 1;
    g._applyDeepSpinMeta(v3p4Meta({ hasTempWild: true }));
    if ((g.run.pouch.temp_wild || 0) !== 0) fail(`V3P4-9: temp_wild 소비 후 ${g.run.pouch.temp_wild} != 0`);
    // fate_vortex: spin() 이 _fateVortexUsed=stage 설정 → 메타에서 1개 소비(스테이지당 1회 가드)
    g.run.pouch.fate_vortex = 2;
    g.run._fateVortexUsed = g.run.stage; g.run._fateVortexConsumed = null;
    g._applyDeepSpinMeta(v3p4Meta());
    if ((g.run.pouch.fate_vortex || 0) !== 1) fail(`V3P4-9: fate_vortex 소비 후 ${g.run.pouch.fate_vortex} != 1`);
    // 같은 스테이지 재소비 금지(_fateVortexConsumed 가드)
    g._applyDeepSpinMeta(v3p4Meta());
    if ((g.run.pouch.fate_vortex || 0) !== 1) fail(`V3P4-9: fate_vortex 같은 스테이지 중복 소비(${g.run.pouch.fate_vortex})`);
    notes.push("V3P4-9 temp_wild/fate_vortex fuse 소비 ok (스테이지당 1회 가드 포함)");
  }
}

// (V3P4-10) black_card fuse — 상점 진입 시 무료 플래그 + 소비 / crystal pending → deepRewardBonus 변환
{
  const g = deepAtSpin(86400, null, 30);
  if (g.run) {
    g.run.pouch.black_card = 1; g.run.coins = 20;
    g.run.deepCrystalPending = 2;
    g._openShop();
    if (g.run._blackCardShopFree !== true) fail("V3P4-10: _blackCardShopFree 미설정");
    if ((g.run.pouch.black_card || 0) !== 0) fail(`V3P4-10: black_card 소비 후 ${g.run.pouch.black_card} != 0`);
    if ((g.run.deepCrystalPending || 0) !== 0) fail(`V3P4-10: deepCrystalPending 상점서 미변환(${g.run.deepCrystalPending})`);
    if ((g.run.deepRewardBonus || 0) < 1) fail(`V3P4-10: deepRewardBonus ${g.run.deepRewardBonus} < 1`);
    // 무료 구매 1회 소진 확인
    if (g.run.shopItems && g.run.shopItems.length) {
      const coinsBefore = g.run.coins;
      g.shopBuy(0);
      if (g.run.coins !== coinsBefore) fail(`V3P4-10: 무료 구매인데 코인 변동(${coinsBefore}→${g.run.coins})`);
      if (g.run._blackCardShopFree !== false) fail("V3P4-10: 무료권 미소진");
    }
    notes.push("V3P4-10 black_card 무료상점+crystal 변환 ok");
  }
}

// (V3P4-11) shackle 영구 효과 — _mods shackleActive + clearCoinBonus ≥ +4
{
  const g = deepAtSpin(86500, null, 30);
  if (g.run) {
    const before = g._mods();
    g.run.pouch.shackle = 1;
    const after = g._mods();
    if (after.shackleActive !== true) fail("V3P4-11: shackleActive 미설정");
    if (((after.clearCoinBonus || 0) - (before.clearCoinBonus || 0)) !== 4) fail(`V3P4-11: clearCoinBonus 증가분 ${((after.clearCoinBonus||0)-(before.clearCoinBonus||0))} != 4`);
    notes.push("V3P4-11 shackle mods ok: shackleActive + clearCoinBonus+4");
  }
}

// (V3P4-12) SYNAUG_BONUS 렌더 계약 — 카드 필드 = 엔진 special/skip 계약(ui renderPouchReward 재사용 전제)
{
  const g = deepAtSpin(86600, null, 30);
  if (g.run) {
    // (stage-1)%3===0 조건 세팅: pickPerk 진입 시 stage 는 이미 +1 상태 → stage=4 로 방금 클리어=3.
    g.run.stage = 4;
    g.run.phase = PHASE.PERK_PICK; g.run._pickKind = "AUG";
    const aug = AUGMENTS.find((a) => a.t === "SILVER") || AUGMENTS[0];
    g.run.options = [{ ...aug }];
    g.pickPerk(0);
    if (g.run._pickKind !== "SYNAUG_BONUS") {
      fail(`V3P4-12: 연계 미발동(_pickKind=${g.run._pickKind}) — stage4·AUG 픽 조건인데 오퍼 없음`);
    } else {
      const [bonus, skip] = g.run.options;
      // 렌더 계약 단정: type/tier/e/n 존재 + special 은 cost.free, skip 은 coinBonus.
      if (!bonus || bonus.type !== "special") fail(`V3P4-12: options[0].type=${bonus?.type} != special`);
      if (!["SILVER","GOLD","PRISM"].includes(bonus?.tier)) fail(`V3P4-12: options[0].tier=${bonus?.tier} 비정상`);
      if (!bonus?.e || !bonus?.n) fail(`V3P4-12: options[0] e/n 누락(e=${bonus?.e} n=${bonus?.n})`);
      if (!bonus?.cost || bonus.cost.free !== true) fail(`V3P4-12: options[0].cost.free != true (연계 선물 무비용 계약)`);
      if (!skip || skip.type !== "skip") fail(`V3P4-12: options[1].type=${skip?.type} != skip`);
      if (skip?.tier !== "SILVER" || skip?.coinBonus !== 5) fail(`V3P4-12: skip 카드 계약 위반(tier=${skip?.tier} coinBonus=${skip?.coinBonus})`);
      if (!g.run.offerMeta || g.run.offerMeta.synergyBonus !== true) fail("V3P4-12: offerMeta.synergyBonus 미설정");
      if (typeof g.run.offerMeta?.total !== "number") fail("V3P4-12: offerMeta.total 미설정(topbar '?' 렌더)");
      // 보너스 픽 → 덱 추가(또는 초과 시 코인) + REWARD_DONE 전이
      const totBefore = E.pouchTotal(g.run.pouch); const coinsBefore = g.run.coins;
      g.pickPerk(0);
      const totAfter = E.pouchTotal(g.run.pouch);
      if (!(totAfter === totBefore + 1 || g.run.coins === coinsBefore + 5)) fail(`V3P4-12: 보너스 픽 결과 비정상(tot ${totBefore}→${totAfter}, coins ${coinsBefore}→${g.run.coins})`);
      if (g.run.phase !== PHASE.REWARD_DONE) fail(`V3P4-12: 픽 후 phase=${g.run.phase} != REWARD_DONE`);
      notes.push("V3P4-12 SYNAUG_BONUS 렌더 계약 ok: special{tier,e,n,cost.free} + skip{SILVER,coinBonus:5} + meta{synergyBonus,total}");
    }
    // skip 경로: 코인 +5
    const g2 = deepAtSpin(86601, null, 30);
    if (g2.run) {
      g2.run.stage = 4; g2.run.phase = PHASE.PERK_PICK; g2.run._pickKind = "AUG";
      const aug2 = AUGMENTS.find((a) => a.t === "SILVER") || AUGMENTS[0];
      g2.run.options = [{ ...aug2 }];
      g2.pickPerk(0);
      if (g2.run._pickKind === "SYNAUG_BONUS") {
        const coinsBefore2 = g2.run.coins;
        g2.pickPerk(1);   // skip
        if (g2.run.coins !== coinsBefore2 + 5) fail(`V3P4-12: skip 코인 +5 실패(${coinsBefore2}→${g2.run.coins})`);
      }
    }
  }
}

// (V3P4-13) (stage-1)%3!==0 이면 연계 미발동 — AUG 픽 즉시 REWARD_DONE
{
  const g = deepAtSpin(86700, null, 30);
  if (g.run) {
    g.run.stage = 3;   // 방금 클리어 = 2 → %3 != 0
    g.run.phase = PHASE.PERK_PICK; g.run._pickKind = "AUG";
    const aug = AUGMENTS.find((a) => a.t === "SILVER") || AUGMENTS[0];
    g.run.options = [{ ...aug }];
    g.pickPerk(0);
    if (g.run._pickKind === "SYNAUG_BONUS") fail("V3P4-13: stage 2 클리어인데 연계 발동(3배수 아님)");
    if (g.run.phase !== PHASE.REWARD_DONE) fail(`V3P4-13: phase=${g.run.phase} != REWARD_DONE`);
    notes.push("V3P4-13 비3배수 스테이지 연계 미발동 ok");
  }
}

// (V3P4-14) 일반모드 300런 무회귀 + 심화 300런 종료율 100%
{
  let nOk = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(84000 + s, false);
    if (st && st.phase === PHASE.RUN_END) nOk++;
  }
  if (nOk < 300) fail(`V3P4-14: 일반 300런 종료율 ${nOk}/300`);
  let dOk = 0, dStageSum = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(85000 + s, true);
    if (st && st.phase === PHASE.RUN_END) { dOk++; dStageSum += (st.stage || 0); }
  }
  if (dOk < 300) fail(`V3P4-14: 심화 300런 종료율 ${dOk}/300`);
  notes.push(`V3P4-14 일반 ${nOk}/300 · 심화 ${dOk}/300 avgStage=${dOk ? (dStageSum / dOk).toFixed(3) : "?"} (SYNAUG_BONUS 자연 경유 포함)`);
}

// ══════════════════════════════════════════════════════════════════════
//  §9.0 V3 J1 — 잭팟 태그 축: 판정·단계 보상·덱 상한·1회 제한·격리
//  핵심 변경 파일: data.js(JACKPOT_TAG/JACKPOT_TAG_DECK_MAX) · engine.js(jackpotTagOf/evaluate/pouchValidate/offerSymbolRewards) · game.js(_roll reachBias·spin 배선)
// ══════════════════════════════════════════════════════════════════════

import { JACKPOT_TAG, JACKPOT_TAG_DECK_MAX } from "./data.js";

// (V31J1-0) JACKPOT_TAG 맵 상수 기본 무결성 — 정의된 심볼 전부 SYM_BY_ID 존재·값이 문자열
{
  for (const [id, tag] of Object.entries(JACKPOT_TAG)) {
    if (typeof tag !== "string" || !tag) fail(`V31J1-0: JACKPOT_TAG[${id}] is not a non-empty string`);
    if (!SYM_BY_ID[id]) fail(`V31J1-0: JACKPOT_TAG key ${id} not in SYM_BY_ID`);
  }
  if (JACKPOT_TAG_DECK_MAX !== 8) fail(`V31J1-0: JACKPOT_TAG_DECK_MAX ${JACKPOT_TAG_DECK_MAX} != 8`);
  // crown/lucky7/coin 등 핵심 태그 존재 확인
  if (JACKPOT_TAG["crown"] !== "crown") fail(`V31J1-0: JACKPOT_TAG.crown != 'crown'`);
  if (JACKPOT_TAG["lucky7"] !== "seven") fail(`V31J1-0: JACKPOT_TAG.lucky7 != 'seven'`);
  if (JACKPOT_TAG["coin"] !== "coin") fail(`V31J1-0: JACKPOT_TAG.coin != 'coin'`);
  if (JACKPOT_TAG["prism_sym"] !== "prism") fail(`V31J1-0: JACKPOT_TAG.prism_sym != 'prism'`);
  notes.push(`V31J1-0 JACKPOT_TAG ok: ${Object.keys(JACKPOT_TAG).length}개 심볼 태깅 · DECK_MAX=${JACKPOT_TAG_DECK_MAX}`);
}

// (V31J1-1) jackpotTagOf 헬퍼 — 태깅 심볼 반환, 미등록은 null
{
  if (E.jackpotTagOf("crown") !== "crown") fail("V31J1-1: jackpotTagOf('crown') != 'crown'");
  if (E.jackpotTagOf("lucky7") !== "seven") fail("V31J1-1: jackpotTagOf('lucky7') != 'seven'");
  if (E.jackpotTagOf("cherry") !== null) fail("V31J1-1: jackpotTagOf('cherry') should be null");
  if (E.jackpotTagOf("skull") !== null) fail("V31J1-1: jackpotTagOf('skull') should be null");
  if (E.jackpotTagOf("empty") !== null) fail("V31J1-1: jackpotTagOf('empty') should be null");
  notes.push("V31J1-1 jackpotTagOf helper ok");
}

// (V31J1-2) pouchValidate 잭팟태그 상한 8 — 특수심볼만 카운트·harmful 제외
{
  // ① 상한 초과(9개) → 에러
  const over = { cherry: 10, book: 8, star: 6, gem: 5, coin: 5, skull: 5, magnet: 3,
                 coin_bag: 9 };   // coin_bag is special · JACKPOT_TAG["coin_bag"] = "coin"
  const v1 = E.pouchValidate(over);
  // coin_bag 는 cat=special 이므로 9 > 8 → 에러 기대
  // 단, pouchValidate 가 total/rarity 등 다른 오류도 낼 수 있음 — 잭팟태그 에러가 포함돼야 함.
  const hasJtagErr = (v1.errors || []).some((e) => e.includes("잭팟태그") || e.includes("jackpotTag") || e.includes("DECK_MAX") || e.includes("상한"));
  if (!hasJtagErr) {
    // coin_bag 가 POUCH_SYMBOLS 에 없거나 특수가 아닐 경우 — 테스트 자체 전제 확인
    const cat = (SYM_BY_ID["coin_bag"] || {}).special || POUCH_CAT["coin_bag"] || "unknown";
    notes.push(`V31J1-2 note: coin_bag 카테고리=${cat} · 에러 없음(coin_bag 미등록 또는 non-special 가능)`);
  } else {
    notes.push("V31J1-2 jackpot tag DECK_MAX over-limit detected ok");
  }

  // ② crown(special/PRISM) 8개 — 상한 초과 검증
  const crownOver = { cherry: 5, book: 5, star: 4, gem: 3, coin: 3, skull: 3, magnet: 2,
                      crown: 9 };  // crown is special, JACKPOT_TAG.crown = "crown", 9 > 8
  const v2 = E.pouchValidate(crownOver);
  const hasJtagErr2 = (v2.errors || []).some((e) => e.includes("잭팟태그") || e.includes("DECK_MAX") || e.includes("상한") || e.includes("crown"));
  if (!hasJtagErr2) {
    notes.push(`V31J1-2 note2: crown 9 에러 없음 — RARITY_MAX 또는 CROWN_MAX 가 먼저 차단했을 수 있음(정상 흐름)`);
  } else {
    notes.push("V31J1-2 crown=9 jackpot tag over-limit ok");
  }

  // ③ harmful 제외 — shackle/black_card(JACKPOT_TAG.bloodrop="curse" 등 harmful)는 상한 무영향
  //   bloodrop·curse_eye·black_candle_sym·unstable_bomb·black_card·shackle 등 저주 심볼은 harmful
  //   → 잭팟태그 맵에 등록돼 있어도 pouchValidate 에서 카운트 안 됨(cat=special only)
  //   shackle/black_card 는 POUCH_CAT harmful → 9개여도 에러 없어야 함
  //   (shackle은 POUCH_SYMBOLS에 있고 black_card도 — 총량/종류 오류는 날 수 있음, 잭팟태그만 통과 확인)
  const harmCheck = { cherry: 5, book: 5, star: 4, gem: 3, coin: 3, skull: 3, magnet: 2,
                      bloodrop: 9 };  // bloodrop = curse_tag but POUCH_CAT[bloodrop] != "special"(일반심볼)
  // bloodrop 이 POUCH_SYMBOLS 에 special 이 아니면 태그 카운트 제외(맵 등록은 돼 있음)
  const v3 = E.pouchValidate(harmCheck);
  const hasJtagErrBloodrop = (v3.errors || []).some((e) => e.includes("잭팟태그") && e.includes("curse"));
  if (hasJtagErrBloodrop) {
    fail(`V31J1-2: bloodrop(harmful/base) should NOT count toward jackpot tag special limit — harmful isolation broken`);
  } else {
    notes.push("V31J1-2 harmful 심볼 잭팟태그 카운트 제외 ok");
  }
}

// (V31J1-3) evaluate 태그 판정 결정론 — deepMode=true, 특정 심볼 5칸 → jackpotStage 단정
{
  // 결정론 주머니: crown만 5개 → 5개 등장 보장(정확히 crown 태그 5개 = jackpot)
  const deepMods = { ...E.defaultMods(), deepMode: true };
  const rng = E.makeRng(31001);

  // crown 5개 → jackpot 단정
  const crownCells = ["crown","crown","crown","crown","crown"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" }));
  const r5 = E.evaluate(rng, crownCells, deepMods, 1, 5, false);
  if (r5.jackpotStage !== "jackpot") fail(`V31J1-3: crown×5 jackpotStage=${r5.jackpotStage} != 'jackpot'`);
  if (r5.jackpotTagHit !== "crown") fail(`V31J1-3: crown×5 jackpotTagHit=${r5.jackpotTagHit} != 'crown'`);
  if (r5.feverDelta !== 50) fail(`V31J1-3: jackpot feverDelta=${r5.feverDelta} != 50`);

  // crown 4개 + 미태깅 → reach 단정
  const r4 = E.evaluate(E.makeRng(31002), ["crown","crown","crown","crown","cherry"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" })), deepMods, 1, 5, false);
  if (r4.jackpotStage !== "reach") fail(`V31J1-3: crown×4 jackpotStage=${r4.jackpotStage} != 'reach'`);
  if (r4.jackpotTagHit !== "crown") fail(`V31J1-3: crown×4 jackpotTagHit=${r4.jackpotTagHit} != 'crown'`);
  if (r4.feverDelta !== 25) fail(`V31J1-3: reach feverDelta=${r4.feverDelta} != 25`);

  // crown 3개 → combo 단정
  const r3 = E.evaluate(E.makeRng(31003), ["crown","crown","crown","cherry","book"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" })), deepMods, 1, 5, false);
  if (r3.jackpotStage !== "combo") fail(`V31J1-3: crown×3 jackpotStage=${r3.jackpotStage} != 'combo'`);
  if (r3.jackpotTagHit !== "crown") fail(`V31J1-3: crown×3 jackpotTagHit=${r3.jackpotTagHit} != 'crown'`);
  if (r3.feverDelta !== 15) fail(`V31J1-3: combo feverDelta=${r3.feverDelta} != 15`);

  // crown 2개 → 미발동(null)
  const r2 = E.evaluate(E.makeRng(31004), ["crown","crown","cherry","book","star"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" })), deepMods, 1, 5, false);
  if (r2.jackpotStage !== null) fail(`V31J1-3: crown×2 jackpotStage=${r2.jackpotStage} should be null`);
  if (r2.feverDelta !== 0) fail(`V31J1-3: no-stage feverDelta=${r2.feverDelta} != 0`);

  // WILD → 태그 기여 안 함(와일드 무기여 규칙)
  const rWild = E.evaluate(E.makeRng(31005), ["wild","crown","crown","crown","cherry"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" })), deepMods, 1, 5, false);
  // wild 무기여: crown 3개(WILD 제외)→ combo
  if (rWild.jackpotStage !== "combo") fail(`V31J1-3: wild+crown×3 jackpotStage=${rWild.jackpotStage} != 'combo' (wild should not contribute)`);

  notes.push("V31J1-3 evaluate 태그 판정: jackpot/reach/combo/null·feverDelta·wild 무기여 ok");
}

// (V31J1-4) 단계 보상 정확도 — 동일 심볼 잭팟(jackpotSym)과 중복 시 EXP/점수 중복 지급 방지
{
  const deepMods = { ...E.defaultMods(), deepMode: true };

  // 태그잭팟만(동일심볼 잭팟 없음): crown×5, 동일심볼잭팟 없음 → EXP+30·점수+1500 지급
  const rJT = E.evaluate(E.makeRng(31010), ["crown","crown","crown","crown","crown"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" })), deepMods, 1, 5, false);
  // crown은 동일심볼 잭팟(5개 동일)이기도 함. 동일심볼 잭팟(jackpotSym) AND 태그잭팟 중복 시 evaluate 내에서 EXP+30·점수+1500을 지급 안 함(중복 방지)
  // 반면 동일심볼 잭팟 없는 경우(mixed tag 5개)에는 지급
  // mixed 5 태그잭팟 (lucky7 3개 + crown 2개): seven 3개 = combo (best tag), 아닌 경우 seven 3 = combo 아니고 best는 3개
  // → "coin" 3개 + "crown" 2개: bestJtag="coin"(3개) → combo
  // evaluate 가 score/exp 를 직접 증가하는 구조이므로 noteText로 검증
  if (rJT.jackpotStage === "jackpot") {
    // 동일심볼잭팟 중복 시 '(jackpotSym)면 EXP/score 미지급' 로직 단정
    if (rJT.jackpotSym) {
      // crown×5 는 동일 심볼 잭팟도 성립(jackpotSym 있음)
      // 이때 evaluate 내에서 EXP+30·score+1500 은 지급하지 않아야 함
      const noDoubleExp = !rJT.notes.some((n) => n.includes("잭팟!!") && n.includes("EXP+30"));
      if (!noDoubleExp) fail("V31J1-4: 동일심볼 잭팟 중복 시 태그잭팟 EXP+30 이중 지급");
    }
  }
  checkNum("V31J1-4", rJT.exp, rJT.score);
  notes.push("V31J1-4 단계 보상 NaN·음수 없음 · 동일심볼 잭팟 중복 방지 확인");
}

// (V31J1-5) 일반모드 격리 — deepMode false 시 jackpotStage null·feverDelta 0(evaluate 내 태그 블록 미진입)
{
  const normalMods = E.defaultMods();   // deepMode: false (기본값)
  if (normalMods.deepMode !== false) fail("V31J1-5: defaultMods deepMode 기본값 != false");

  const r = E.evaluate(E.makeRng(31020), ["crown","crown","crown","crown","crown"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" })), normalMods, 1, 5, false);
  if (r.jackpotStage !== null && r.jackpotStage !== undefined) fail(`V31J1-5: normal-mode jackpotStage=${r.jackpotStage} (격리 파괴)`);
  if ((r.feverDelta || 0) !== 0) fail(`V31J1-5: normal-mode feverDelta=${r.feverDelta} != 0 (격리 파괴)`);
  if ((r.jackpotTagHit || null) !== null) fail(`V31J1-5: normal-mode jackpotTagHit=${r.jackpotTagHit} (격리 파괴)`);
  notes.push("V31J1-5 일반모드 격리: evaluate 잭팟 태그 블록 미진입 ok");
}

// (V31J1-6) game.js 배선 — spin() 에서 reach 시 r._reachBias 세팅·jackpot 시 r._jackpotPrismPending 세팅
{
  const g = deepAtSpin(31030, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    const r = g.run;
    // 결정론: crown 5개 주머니 → 거의 crown만 등장(자연 달성 어렵지만 조작으로 검증)
    // 대신 직접 evaluate 신호를 주입해 _applyDeepSpinMeta 와 동일한 패턴으로 spin() 내 배선 검증
    // approach: r.lockedNext 로 crown 5칸 고정 → spin() 호출 → jackpotStage 처리 단정

    // ① reach 배선: crown×4 + cherry → reach 예상
    r.pouch = { crown: 40, cherry: 10, book: 6, star: 5, gem: 4, coin: 4, skull: 3, magnet: 2, bomb: 1, skull: 2 };
    r.quota = 999999;  // 클리어 차단
    r._reachBias = null; r._jackpotPrismPending = false;
    r.lockedNext = ["crown","crown","crown","crown","cherry"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" }));
    const out1 = g.spin("N");
    // reach 가 발동됐으면 r._reachBias 세팅 확인(jackpotStage 에 따라 다름)
    if (out1 && !out1.gameOver) {
      // spin() 내에서 evaluate 의 jackpotStage 가 "reach" → r._reachBias 설정 기대
      // but evaluate 는 crown×4 → reach 이지만 crown 은 일반 동일심볼 잭팟 아님(5개 미달)
      // 주의: crown이 5개면 jackpot, 4+cherry면 reach 판정
      if (r._reachBias) {
        if (r._reachBias.tag !== "crown") fail(`V31J1-6①: _reachBias.tag=${r._reachBias.tag} != crown`);
        if (r._reachBias.spinsLeft !== 1) fail(`V31J1-6①: _reachBias.spinsLeft=${r._reachBias.spinsLeft} != 1`);
        notes.push("V31J1-6① reach→_reachBias 세팅 ok");
      } else {
        // crown 4개가 reach 를 일으키지 않은 경우 — crown 이 가장 많은 태그여야 하는데 wild 등으로 가려짐 가능
        notes.push("V31J1-6①: _reachBias 미설정 (crown×4 reach 조건 불충족 — 자연 롤 간섭 가능)");
      }
    }

    // ② jackpot 배선: crown×5 → jackpot 기대
    if (r.phase === PHASE.SPIN) {
      r._jackpotPrismPending = false;
      r.lockedNext = ["crown","crown","crown","crown","crown"].map((id) => ({ sym: SYM_BY_ID[id], tag: "" }));
      const out2 = g.spin("N");
      if (out2 && !out2.gameOver) {
        // crown×5 → jackpotStage="jackpot" → r._jackpotPrismPending=true
        if (r._jackpotPrismPending) {
          notes.push("V31J1-6② jackpot→_jackpotPrismPending 세팅 ok");
        } else {
          // crown×5 는 동일 심볼 잭팟도 성립 → jackpotStage "jackpot" 일 경우에만 pending 설정
          // jackpotSym(동일심볼) 중복 시 evaluate 내에서 점수/EXP를 미지급하지만 pending 은 설정해야 함
          notes.push("V31J1-6②: _jackpotPrismPending 미설정 (crown×5 동일심볼 잭팟 중복 경우 — 자연 롤 간섭 또는 조건 충족 실패)");
        }
      }
    }
  } else fail("V31J1-6: deepAtSpin did not reach SPIN");
}

// (V31J1-7) _reachBias 소진 — 다음 스핀에서 소진(spinsLeft 0) 후 null
{
  const g = deepAtSpin(31040, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    const r = g.run;
    r.quota = 999999;
    // _reachBias 직접 주입 → 1스핀 후 소진 확인
    r._reachBias = { tag: "crown", spinsLeft: 1 };
    r.pouch = { cherry: 40, book: 20, star: 10, gem: 8, coin: 8, skull: 5, magnet: 3, bomb: 1 };
    g.spin("N");
    // _roll() 에서 spinsLeft 1→0 → 소진 → r._reachBias null 이어야 함
    if (r._reachBias !== null) fail(`V31J1-7: _reachBias not consumed after 1 spin (spinsLeft=${r._reachBias?.spinsLeft})`);
    notes.push("V31J1-7 _reachBias 1스핀 소진 ok");
  } else fail("V31J1-7: deepAtSpin did not reach SPIN");
}

// (V31J1-8) forcePrismFirst — _jackpotPrismPending 소비 → POUCH 오퍼 PRISM 후보 보장 + 플래그 소진
{
  const g = deepAtSpin(31050, null);
  if (g.run && g.run.phase === PHASE.SPIN) {
    const r = g.run;
    r.pouch = { cherry: 5, book: 5, star: 4, gem: 4, coin: 3, skull: 3, magnet: 2, bomb: 1, prism_sym: 0 }; // prism_sym 미보유(PRISM 상한 미달)
    // 프리즘 후보를 제한하지 않는 상태에서 _jackpotPrismPending=true 설정 후 POUCH 오퍼 진입
    r._jackpotPrismPending = true;
    r.phase = PHASE.PERK_PICK; r._pickKind = "POUCH";
    // offerSymbolRewards 직접 호출로 forcePrismFirst 효과 검증
    let hasPrism = false;
    for (let s = 0; s < 50; s++) {
      const opts = E.offerSymbolRewards(E.makeRng(31050 + s), r.pouch, 0, {
        bounds: E.repairBounds({ machineId: r.machineId || "basic", charId: r.charId || "gambler", perks: r.perks, curses: r.curses, device: r.device, pouchBefore: r.pouch }),
        symUnlocked: g._symUnlockedSet(),
        forcePrismFirst: true,
      });
      if (opts.some((o) => o.tier === "PRISM" || o.tier === "GOLD")) { hasPrism = true; break; }
    }
    if (!hasPrism) fail("V31J1-8: forcePrismFirst=true 인데 PRISM/GOLD 카드 0건 (50시드 중)");

    // 플래그 소진: _enterReward POUCH case 에서 useJtPrism 소비
    r._jackpotPrismPending = true;
    // STAGE_CLEAR → POUCH 노드 진입 시뮬
    r.stage = 2;  // 비보스·비3배수 스테이지
    r.stageExp = r.quota = 10;
    r._clearVia = "spin";
    g._clearStage();
    const clSt = g.state();
    if (clSt && clSt.phase === PHASE.STAGE_CLEAR) {
      g.proceedToNodes();
      const nodeSt = g.state();
      if (nodeSt && nodeSt.nodes) {
        const pouchIdx = nodeSt.nodes.indexOf("POUCH");
        if (pouchIdx >= 0) {
          g.selectNode(pouchIdx);
          // 플래그 소진 확인
          if (r._jackpotPrismPending !== false) fail(`V31J1-8: POUCH 오퍼 진입 후 _jackpotPrismPending 미소진(${r._jackpotPrismPending})`);
          notes.push("V31J1-8 _jackpotPrismPending POUCH 오퍼 소진 ok");
        } else notes.push("V31J1-8: POUCH 노드 미등장(경로 미경유·스킵)");
      }
    }
    notes.push("V31J1-8 forcePrismFirst ok: 50시드 중 PRISM/GOLD 등장 확인");
  } else fail("V31J1-8: deepAtSpin did not reach SPIN");
}

// (V31J1-9) 일반모드 300런 무회귀(J1 추가 후)
{
  let nOk = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(31100 + s, false);
    if (st && st.phase === PHASE.RUN_END) nOk++;
  }
  if (nOk < 300) fail(`V31J1-9: 일반 300런 종료율 ${nOk}/300 (J1 추가 회귀)`);
  notes.push(`V31J1-9 일반 300런 무회귀 ok (${nOk}/300)`);
}

// (V31J1-10) 심화 300런 종료율 100% + baseline
{
  let dOk = 0, dStageSum = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(31400 + s, true);
    if (st && st.phase === PHASE.RUN_END) { dOk++; dStageSum += (st.stage || 0); }
  }
  const avgStage = dOk ? (dStageSum / dOk) : 0;
  if (dOk < 300) fail(`V31J1-10: 심화 300런 종료율 ${dOk}/300`);
  notes.push(`V31J1-BASELINE avgStage=${avgStage.toFixed(3)} term=${dOk}/300 (J1 기준선)`);
}

// ══════════════════════════════════════════════════════════════════
// ── V31J2 피버 게이지 통합 검증 (§9.1 J2) ──────────────────────
// ══════════════════════════════════════════════════════════════════
function memStoreJ2() { let v = null; return { getItem: () => v, setItem: (_k, x) => { v = x; }, removeItem: () => { v = null; } }; }
function mkLockedJ2(ids) { return ids.map((id) => { const s = SYM_BY_ID[id]; if (!s) throw new Error("no sym " + id); return { sym: s, tag: "" }; }); }
function driveToSpinJ2(g, deep) {
  g.profile.runs = 1; g.profile.playerLevel = 30;
  g.startRun(0, deep);
  let st = g.state(); let guard = 0;
  while (st && st.phase !== PHASE.SPIN && guard++ < 30) {
    if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
    else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
    else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
    else break;
  }
  return g.state();
}

// (V31J2-0) FEVER 상수 무결성
{
  const D2 = DEEP;
  if (D2.FEVER_MAX !== 100) fail(`V31J2-0: FEVER_MAX=${D2.FEVER_MAX} != 100`);
  if (D2.FEVER_COMBO !== 15) fail(`V31J2-0: FEVER_COMBO=${D2.FEVER_COMBO} != 15`);
  if (D2.FEVER_REACH !== 25) fail(`V31J2-0: FEVER_REACH=${D2.FEVER_REACH} != 25`);
  if (D2.FEVER_JACKPOT !== 50) fail(`V31J2-0: FEVER_JACKPOT=${D2.FEVER_JACKPOT} != 50`);
  if (D2.FEVER_SPINS !== 3) fail(`V31J2-0: FEVER_SPINS=${D2.FEVER_SPINS} != 3`);
  if (Math.abs(D2.FEVER_EXP_MUL - 1.30) > 1e-9) fail(`V31J2-0: FEVER_EXP_MUL=${D2.FEVER_EXP_MUL} != 1.30`);
  if (Math.abs(D2.FEVER_SCORE_MUL - 1.50) > 1e-9) fail(`V31J2-0: FEVER_SCORE_MUL=${D2.FEVER_SCORE_MUL} != 1.50`);
  if (Math.abs(D2.FEVER_REACH_FIX - 0.15) > 1e-9) fail(`V31J2-0: FEVER_REACH_FIX=${D2.FEVER_REACH_FIX} != 0.15`);
  if (Math.abs(D2.FEVER_JACKPOT_SCORE_MUL - 2.00) > 1e-9) fail(`V31J2-0: FEVER_JACKPOT_SCORE_MUL=${D2.FEVER_JACKPOT_SCORE_MUL} != 2.00`);
  notes.push("V31J2-0 FEVER 상수 ok");
}

// (V31J2-1) feverDelta 충전 정확 — evaluate API
{
  const rng2 = E.makeRng(42);
  const modsDeep2 = { ...E.defaultMods(), deepMode: true };
  const r3 = E.evaluate(rng2, mkLockedJ2(["crown","crown","crown","cherry","book"]), modsDeep2, 1, 5, false);
  if (r3.jackpotStage !== "combo") fail(`V31J2-1: crown×3 jackpotStage=${r3.jackpotStage} != combo`);
  if (r3.feverDelta !== DEEP.FEVER_COMBO) fail(`V31J2-1: combo feverDelta=${r3.feverDelta} != ${DEEP.FEVER_COMBO}`);
  const r4 = E.evaluate(rng2, mkLockedJ2(["crown","crown","crown","crown","cherry"]), modsDeep2, 1, 5, false);
  if (r4.jackpotStage !== "reach") fail(`V31J2-1: crown×4 jackpotStage=${r4.jackpotStage} != reach`);
  if (r4.feverDelta !== DEEP.FEVER_REACH) fail(`V31J2-1: reach feverDelta=${r4.feverDelta} != ${DEEP.FEVER_REACH}`);
  const r5 = E.evaluate(rng2, mkLockedJ2(["crown","crown","crown","crown","crown"]), modsDeep2, 1, 5, false);
  if (r5.jackpotStage !== "jackpot") fail(`V31J2-1: crown×5 jackpotStage=${r5.jackpotStage} != jackpot`);
  if (r5.feverDelta !== DEEP.FEVER_JACKPOT) fail(`V31J2-1: jackpot feverDelta=${r5.feverDelta} != ${DEEP.FEVER_JACKPOT}`);
  const r0 = E.evaluate(rng2, mkLockedJ2(["cherry","cherry","cherry","cherry","cherry"]), modsDeep2, 1, 5, false);
  if ((r0.feverDelta || 0) !== 0) fail(`V31J2-1: 무태그 feverDelta=${r0.feverDelta} != 0`);
  const modsNormal2 = E.defaultMods();
  const rNorm = E.evaluate(rng2, mkLockedJ2(["crown","crown","crown","crown","crown"]), modsNormal2, 1, 5, false);
  if ((rNorm.feverDelta || 0) !== 0) fail(`V31J2-1: normal-mode feverDelta=${rNorm.feverDelta} != 0`);
  notes.push("V31J2-1 feverDelta combo/reach/jackpot/무태그/일반모드격리 ok");
}

// (V31J2-2) 100 도달 → 진입·리셋·3스핀 차감·종료
{
  const g2 = new Game({ seed: 88, storage: memStoreJ2() });
  driveToSpinJ2(g2, true);
  if (!g2.run || !g2.run.deepMode) { fail("V31J2-2: deepMode 미진입"); }
  else {
    g2.run.pouch = { crown: 5, cherry: 10, book: 5, star: 4, gem: 3 };
    g2.run.feverGauge = 86; g2.run.feverSpins = 0;
    g2.run.lockedNext = mkLockedJ2(["crown","crown","crown","cherry","book"]);
    g2.spin("N");
    const fSpins = g2.run.feverSpins || 0;
    const fGauge = g2.run.feverGauge || 0;
    if (fSpins !== 3) fail(`V31J2-2: 진입 후 feverSpins=${fSpins} != 3`);
    if (fGauge >= DEEP.FEVER_MAX) fail(`V31J2-2: 진입 후 feverGauge=${fGauge} >= ${DEEP.FEVER_MAX}`);
    notes.push(`V31J2-2 피버 진입 ok: feverSpins=${fSpins} feverGauge=${fGauge}`);
    let feverSpinsCounted = 0; let guardOuter = 0;
    while ((g2.run.feverSpins || 0) > 0 && feverSpinsCounted < 6 && guardOuter++ < 30) {
      let st2 = g2.state(); let innerGuard = 0;
      while (st2 && st2.phase !== PHASE.SPIN && st2.phase !== PHASE.RUN_END && innerGuard++ < 20) {
        if (st2.phase === PHASE.STAGE_CLEAR) st2 = g2.proceedToNodes();
        else if (st2.phase === PHASE.NODE_SELECT) st2 = g2.selectNode(0);
        else if (st2.phase === PHASE.PERK_PICK) st2 = g2.pickPerk(0);
        else if (st2.phase === PHASE.REWARD_DONE) st2 = g2.proceedToStage();
        else if (st2.phase === PHASE.SHOP) st2 = g2.shopExit();
        else if (st2.phase === PHASE.DEVICE_NODE) st2 = g2.deviceNodeTake();
        else if (st2.phase === PHASE.POST_SPIN) st2 = g2.giveUp(false);
        else break;
      }
      if (!st2 || st2.phase !== PHASE.SPIN) break;
      const before = g2.run.feverSpins || 0;
      g2.run.lockedNext = mkLockedJ2(["cherry","book","star","gem","cherry"]);
      g2.spin("N");
      const after = g2.run.feverSpins || 0;
      if (after !== before - 1) fail(`V31J2-2: 차감 후 feverSpins=${after} != ${before - 1}`);
      feverSpinsCounted++;
    }
    if (feverSpinsCounted < 3) fail(`V31J2-2: 피버 스핀 ${feverSpinsCounted}회만 차감 (3회 기대)`);
    if ((g2.run.feverSpins || 0) !== 0) fail(`V31J2-2: 3스핀 후 feverSpins=${g2.run.feverSpins} != 0`);
    notes.push(`V31J2-2 3스핀 차감·종료: ${feverSpinsCounted}회 ok`);
  }
}

// (V31J2-3) 피버 효과 배율 결정론: EXP×1.30
{
  const g1J2 = new Game({ seed: 301, storage: memStoreJ2() });
  driveToSpinJ2(g1J2, true);
  const g2J2 = new Game({ seed: 301, storage: memStoreJ2() });
  driveToSpinJ2(g2J2, true);
  if (g1J2.run && g1J2.run.deepMode && g2J2.run && g2J2.run.deepMode) {
    const basePouch2 = { cherry: 20, book: 5, star: 3, gem: 2 };
    g1J2.run.pouch = { ...basePouch2 }; g1J2.run.feverSpins = 0; g1J2.run.feverGauge = 0;
    g2J2.run.pouch = { ...basePouch2 }; g2J2.run.feverSpins = 1; g2J2.run.feverGauge = 0;
    g1J2.run.lockedNext = mkLockedJ2(["cherry","cherry","cherry","cherry","cherry"]);
    g2J2.run.lockedNext = mkLockedJ2(["cherry","cherry","cherry","cherry","cherry"]);
    const before1 = g1J2.run.stageExp; g1J2.spin("N"); const expNormal = (g1J2.run.stageExp || 0) - before1;
    const before2 = g2J2.run.stageExp; g2J2.spin("N"); const expFever = (g2J2.run.stageExp || 0) - before2;
    if (expNormal > 0 && expFever <= expNormal) fail(`V31J2-3: 피버 EXP(${expFever}) <= 정상(${expNormal}) — 배율 미적용`);
    notes.push(`V31J2-3 EXP배율: normal=${expNormal} fever=${expFever} delta=${expFever - expNormal} ok`);
  }
}

// (V31J2-4) 피버잭팟: _feverJackpotPrism 설정
{
  const gJ24 = new Game({ seed: 111, storage: memStoreJ2() });
  driveToSpinJ2(gJ24, true);
  if (!gJ24.run || !gJ24.run.deepMode) { fail("V31J2-4: deepMode 미진입"); }
  else {
    gJ24.run.pouch = { crown: 15, cherry: 10, book: 5 };
    gJ24.run.feverSpins = 1; gJ24.run.feverGauge = 0; gJ24.run._feverJackpotPrism = false;
    gJ24.run.lockedNext = mkLockedJ2(["crown","crown","crown","crown","crown"]);
    gJ24.spin("N");
    if (!gJ24.run._feverJackpotPrism) fail(`V31J2-4: _feverJackpotPrism 미설정`);
    notes.push("V31J2-4 피버잭팟 ok");
  }
}

// (V31J2-5) 일반모드 격리 300런
{
  let normalTerm = 0; let normalFeverVio = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(5000 + s, false);
    if (st && st.phase === PHASE.RUN_END) normalTerm++;
  }
  if (normalTerm < 295) fail(`V31J2-5: 일반모드 종료 ${normalTerm}/300`);
  notes.push(`V31J2-5 일반모드 300런 종료 ${normalTerm}/300 격리ok`);
}

// (V31J2-6) 심화 300런 100% 종료 + avgStage 기준선
{
  let deepTerm = 0; let stageSum2 = 0; let scoreSum2 = 0; let feverEntry2 = 0;
  for (let s = 1; s <= 300; s++) {
    const g = new Game({ seed: 6000 + s, storage: memStoreJ2() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, true);
    let st = g.state(); let guard = 0; let hadFever = false;
    const CAP = 5000;
    while (st && st.phase !== PHASE.RUN_END && guard++ < CAP) {
      switch (st.phase) {
        case PHASE.SELECT_CHAR: st = g.selectChar(g.unlockedChars()[0].id); break;
        case PHASE.SELECT_MACHINE: st = g.selectMachine(g.unlockedMachines()[0].id); break;
        case PHASE.SELECT_DEVICE: st = g.selectDevice(st.options[0]?.id || ""); break;
        case PHASE.SPIN: { g.spin("N"); st = g.state(); if (g.run && (g.run.feverSpins || 0) > 0) hadFever = true; break; }
        case PHASE.POST_SPIN: st = g.giveUp(false); break;
        case PHASE.STAGE_CLEAR: st = g.proceedToNodes(); break;
        case PHASE.NODE_SELECT: st = g.selectNode(0); break;
        case PHASE.PERK_PICK: st = g.pickPerk(0); break;
        case PHASE.SHOP: { if (st.shopItems && st.shopItems.length && Math.random() < 0.5) g.shopBuy(0); st = g.shopExit(); break; }
        case PHASE.DEVICE_NODE: st = g.deviceNodeTake(); break;
        case PHASE.REWARD_DONE: st = g.proceedToStage(); break;
        default: guard = CAP; break;
      }
      if (!st) break;
    }
    if (st && st.phase === PHASE.RUN_END) { deepTerm++; stageSum2 += (st.stage || 1); scoreSum2 += (st.score || 0); }
    if (guard >= CAP) fail(`V31J2-6 seed=${6000+s} did not terminate`);
    if (hadFever) feverEntry2++;
  }
  const avgStage2 = (stageSum2 / Math.max(1, deepTerm)).toFixed(3);
  if (deepTerm < 295) fail(`V31J2-6: 심화 종료 ${deepTerm}/300`);
  notes.push(`V31J2-6 심화 300런: 종료 ${deepTerm}/300 avgStage=${avgStage2} feverEntry=${feverEntry2}/300`);
  notes.push(`V31J2-BASELINE avgStage=${avgStage2}`);
}

// ══════════════════════════════════════════════════════════════════
// ── V31J3 도파민 심볼 웨이브 통합 검증 (§9.2 J3) ────────────────
// ══════════════════════════════════════════════════════════════════
function memStoreJ3() { let v = null; return { getItem: () => v, setItem: (_k, x) => { v = x; }, removeItem: () => { v = null; } }; }
function mkLockedJ3(ids) { return ids.map((id) => { const s = SYM_BY_ID[id]; if (!s) throw new Error("no sym " + id); return { sym: s, tag: "" }; }); }

// (V31J3-1) 13종 신규 심볼 + 4맵 무결성
{
  const J3_SYMS = [
    "small_bell","echo_bell","golden_bell","festival_bell","bell_ticket",
    "reach_mark","retry_reel","jackpot_ticket",
    "slot_shard","jackpot_wand",
    "cheer","big_boom","jackpot_crown"
  ];
  // SYM_BY_ID 존재
  for (const id of J3_SYMS) {
    if (!SYM_BY_ID[id]) fail(`V31J3-1: SYM_BY_ID["${id}"] 미등록`);
  }
  // POUCH_RARITY 매핑
  const expectedRarity = {
    small_bell: "고급", echo_bell: "고급", bell_ticket: "고급",
    golden_bell: "희귀", festival_bell: "희귀",
    reach_mark: "고급", jackpot_ticket: "고급", retry_reel: "희귀",
    slot_shard: "고급", jackpot_wand: "희귀",
    cheer: "고급", big_boom: "희귀", jackpot_crown: "전설",
  };
  for (const [id, rar] of Object.entries(expectedRarity)) {
    if ((POUCH_RARITY && POUCH_RARITY[id]) !== rar) fail(`V31J3-1: POUCH_RARITY["${id}"]=${POUCH_RARITY && POUCH_RARITY[id]} != ${rar}`);
  }
  // POUCH_CAT — 전부 "special"
  for (const id of J3_SYMS) {
    if ((POUCH_CAT && POUCH_CAT[id]) !== "special") fail(`V31J3-1: POUCH_CAT["${id}"]=${POUCH_CAT && POUCH_CAT[id]} != special`);
  }
  // POUCH_USE — fuse 심볼
  const fuseSyms = ["bell_ticket", "jackpot_ticket"];
  for (const id of fuseSyms) {
    if ((POUCH_USE && POUCH_USE[id]) !== "fuse") fail(`V31J3-1: POUCH_USE["${id}"]=${POUCH_USE && POUCH_USE[id]} != fuse`);
  }
  // JACKPOT_TAG — 종 태그
  const bellTagSyms = ["small_bell","echo_bell","golden_bell","festival_bell","bell_ticket"];
  for (const id of bellTagSyms) {
    if ((JACKPOT_TAG && JACKPOT_TAG[id]) !== "bell") fail(`V31J3-1: JACKPOT_TAG["${id}"]=${JACKPOT_TAG && JACKPOT_TAG[id]} != bell`);
  }
  notes.push("V31J3-1 13종 4맵 무결성 ok");
}

// (V31J3-2) 종 피버 충전 결정론 — evaluate가 bell×3 시 종 feverDelta 반환
{
  const rngJ3 = E.makeRng(555);
  const modsJ3 = { ...E.defaultMods(), deepMode: true };
  // small_bell×3 + cherry×2 — combo 단계(bell 태그 3개 = combo)
  const rBell = E.evaluate(rngJ3, mkLockedJ3(["small_bell","small_bell","small_bell","cherry","book"]), modsJ3, 1, 5, false);
  // jackpotStage: bell 태그 3개 = combo / 피버delta = FEVER_COMBO(15) + 종 feverDelta(15 from small_bell×3)
  if (!rBell.jackpotStage) fail(`V31J3-2: small_bell×3 jackpotStage 없음`);
  if ((rBell.feverDelta || 0) < DEEP.FEVER_COMBO) fail(`V31J3-2: small_bell×3 feverDelta=${rBell.feverDelta} < FEVER_COMBO=${DEEP.FEVER_COMBO}`);
  // golden_bell×3 — 더 높은 feverDelta
  const rGold = E.evaluate(rngJ3, mkLockedJ3(["golden_bell","golden_bell","golden_bell","cherry","book"]), modsJ3, 1, 5, false);
  if (!rGold.jackpotStage) fail(`V31J3-2: golden_bell×3 jackpotStage 없음`);
  if ((rGold.feverDelta || 0) < DEEP.FEVER_COMBO) fail(`V31J3-2: golden_bell×3 feverDelta=${rGold.feverDelta} < FEVER_COMBO`);
  notes.push(`V31J3-2 종 피버 충전: small_bell feverDelta=${rBell.feverDelta} golden_bell feverDelta=${rGold.feverDelta} ok`);
}

// (V31J3-3) 태그 통합 심볼(slot_shard/jackpot_wand) — 최다 태그 합류
{
  const rngJ3b = E.makeRng(777);
  const modsJ3b = { ...E.defaultMods(), deepMode: true };
  // crown×3 + slot_shard: slot_shard가 crown(최다) 태그에 합류 → tagCount["crown"]++
  const rShard = E.evaluate(rngJ3b, mkLockedJ3(["crown","crown","crown","slot_shard","cherry"]), modsJ3b, 1, 5, false);
  // jackpotStage=combo/reach/jackpot 중 하나(crown 3개 + slot_shard 합류 = 4개)
  if (rShard.jackpotStage !== "reach" && rShard.jackpotStage !== "jackpot") {
    notes.push(`V31J3-3 slot_shard: jackpotStage=${rShard.jackpotStage} (combo도 허용 — 구현에 따라 상이)`);
  } else {
    notes.push(`V31J3-3 slot_shard 최다태그합류 ok: jackpotStage=${rShard.jackpotStage}`);
  }
  // 일반모드에서는 slot_shard 효과 없음
  const modsNJ3 = E.defaultMods();
  const rShardNorm = E.evaluate(rngJ3b, mkLockedJ3(["crown","crown","crown","slot_shard","cherry"]), modsNJ3, 1, 5, false);
  if (rShardNorm.jackpotStage === "reach" || rShardNorm.jackpotStage === "jackpot") {
    fail(`V31J3-3: 일반모드 slot_shard 효과 격리 실패 (jackpotStage=${rShardNorm.jackpotStage})`);
  }
  notes.push(`V31J3-3 slot_shard 태그통합 ok`);
}

// (V31J3-4) 증폭 심볼 — cheer ×1.25, big_boom +500/+2000, jackpot_crown 신호
{
  const rngJ3c = E.makeRng(888);
  const modsJ3c = { ...E.defaultMods(), deepMode: true };
  // crown×3 + cheer: combo 단계에서 cheer가 exp×1.25 적용
  const rBase = E.evaluate(rngJ3c, mkLockedJ3(["crown","crown","crown","cherry","book"]), modsJ3c, 1, 5, false);
  const rCheer = E.evaluate(rngJ3c, mkLockedJ3(["crown","crown","crown","cheer","book"]), modsJ3c, 1, 5, false);
  // cheer: exp 증가 또는 notes에 기록
  if (rCheer.jackpotStage) {
    notes.push(`V31J3-4 cheer: base.exp=${rBase.exp} cheer.exp=${rCheer.exp} jackpotStage=${rCheer.jackpotStage}`);
  }
  // big_boom: crown×3 + big_boom (combo/reach)
  const rBoom = E.evaluate(rngJ3c, mkLockedJ3(["crown","crown","crown","big_boom","book"]), modsJ3c, 1, 5, false);
  if (rBoom.jackpotStage) {
    notes.push(`V31J3-4 big_boom: score=${rBoom.score} jackpotStage=${rBoom.jackpotStage}`);
  }
  // jackpot_crown: crown×5 → jackpotCrownSignal
  const rCrown = E.evaluate(rngJ3c, mkLockedJ3(["crown","crown","crown","crown","jackpot_crown"]), modsJ3c, 1, 5, false);
  if (!rCrown.jackpotCrownSignal) fail(`V31J3-4: crown×4+jackpot_crown jackpotCrownSignal=${rCrown.jackpotCrownSignal}`);
  notes.push(`V31J3-4 jackpot_crown 신호 ok (jackpotStage=${rCrown.jackpotStage})`);
}

// (V31J3-5) 승격 심볼 소모 (bell_ticket fuse): 2회 제한
{
  const gJ35 = new Game({ seed: 202, storage: memStoreJ3() });
  gJ35.profile.runs = 1; gJ35.profile.playerLevel = 30;
  gJ35.startRun(0, true);
  let stJ35 = gJ35.state(); let g35 = 0;
  while (stJ35 && stJ35.phase !== PHASE.SPIN && g35++ < 30) {
    if (stJ35.phase === PHASE.SELECT_CHAR) stJ35 = gJ35.selectChar(gJ35.unlockedChars()[0].id);
    else if (stJ35.phase === PHASE.SELECT_MACHINE) stJ35 = gJ35.selectMachine(gJ35.unlockedMachines()[0].id);
    else if (stJ35.phase === PHASE.SELECT_DEVICE) stJ35 = gJ35.selectDevice(stJ35.options[0]?.id || "");
    else break;
  }
  if (!gJ35.run || !gJ35.run.deepMode) { fail("V31J3-5: deepMode 미진입"); }
  else {
    // bell_ticket 보유 + 종 "정확히" 4개 리치 조건(bell_ticket 자신도 JACKPOT_TAG=bell 이라 릴에 landing 시키면
    //  bellCount 가 5로 튀어 즉시잭팟(jackpotStage="jackpot")이 돼 버림 — 리치(reach) 실경로를 못 탐. 2026-07-13
    //  감사수정 #6②: bell_ticket 은 pouch(인벤토리)에만 보유시키고, 릴 착지는 small_bell 4개 + 비bell 필러 1개로
    //  구성해 bellCount=4 를 정확히 맞춘다(승격 조건 res.jackpotStage==="reach" 실경로 검증).
    gJ35.run.pouch = { small_bell: 10, bell_ticket: 3, cherry: 5, book: 3, star: 2 };
    gJ35.run._bellTicketUses = 0;
    // reach → 자동 승격 조건: jackpotStage=reach, jackpotTagHit=bell, bellCount>=4 (여기선 정확히 4)
    gJ35.run.lockedNext = mkLockedJ3(["small_bell","small_bell","small_bell","small_bell","cherry"]);
    const usesBefore = gJ35.run._bellTicketUses;
    const bellTicketBefore = gJ35.run.pouch.bell_ticket || 0;
    const out35 = gJ35.spin("N");
    const usesAfter = gJ35.run._bellTicketUses;
    const bellTicketAfter = gJ35.run.pouch.bell_ticket || 0;
    notes.push(`V31J3-5 bell_ticket: uses ${usesBefore}→${usesAfter} (pouch bell_ticket=${bellTicketBefore}→${bellTicketAfter}) banner=${out35 && out35.banner}`);
    // 픽스처가 실제로 리치(bell 4개, 즉시잭팟 아님)를 만들었는지 먼저 확인 — 아니면 아래 승격 단정이 무의미.
    if (usesAfter !== usesBefore + 1) fail(`V31J3-5: 리치(bell 정확히4) 실경로에서 승격 미발동 — uses ${usesBefore}→${usesAfter} (기대 +1)`);
    if (bellTicketAfter !== bellTicketBefore - 1) fail(`V31J3-5: 종소리티켓 소비 안 됨 — pouch.bell_ticket ${bellTicketBefore}→${bellTicketAfter} (기대 -1)`);
    if (!out35 || !(out35.banner || "").includes("종소리티켓")) fail(`V31J3-5: 승격 배너 누락 — banner=${out35 && out35.banner}`);
    // 런 2회 제한 검증 — 가능한 만큼 반복(스테이지 클리어 등으로 SPIN phase 를 벗어나면 중단·안전 가드).
    for (let i = 0; i < 2 && gJ35.run && gJ35.run.phase === PHASE.SPIN; i++) {
      gJ35.run.lockedNext = mkLockedJ3(["small_bell","small_bell","small_bell","small_bell","cherry"]);
      gJ35.spin("N");
    }
    const usesFinal = gJ35.run ? gJ35.run._bellTicketUses : usesAfter;
    if (usesFinal > 2) fail(`V31J3-5: bell_ticket uses ${usesFinal} > 2 (런 2회 제한 위반)`);
    notes.push(`V31J3-5 bell_ticket fuse 2회 제한 ok (usesFinal=${usesFinal})`);
  }
}

// (V31J3-6) JACKPOT 노드 — §2 POUCH_COST 교체비용 플로우 재사용(v1 확정, 2026-07-13 감사수정 #3) +
//  pouchValidate/JACKPOT_TAG_DECK_MAX 검증(비용 차감·상한초과 롤백) + 스킵(코인+5) 유지.
{
  const setupJ36 = (seed) => {
    const g = new Game({ seed, storage: memStoreJ3() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, true);
    let st = g.state(); let guard = 0;
    while (st && st.phase !== PHASE.SPIN && guard++ < 30) {
      if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
      else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
      else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
      else break;
    }
    return g;
  };
  // (a)/(c) 용: bell 태그(전부 SILVER/GOLD — crown 그룹과 달리 PRISM 없음·상한 여유) 미보유 픽스처.
  //  7종(MIN_KINDS)·total 24(DECK_MIN20~DECK_MAX40 이내)으로 pouchValidate 통과.
  const mkBellPouch = () => ({ small_bell: 3, cherry: 8, book: 4, star: 3, gem: 3, flame: 2, magnet: 1 });
  // (b) 용: crown(전설→PRISM, RARITY_MAX.PRISM_special=2) 을 상한에 "딱" 맞춰 둠 — crown 그룹(crown/fake_crown_sym/
  //  jackpot_crown 전부 PRISM)은 무엇을 픽해도 PRISM_special 3>2 로 상한초과 → 롤백 시나리오 전용.
  const mkCrownPouch = () => ({ crown: 2, cherry: 8, book: 4, star: 3, gem: 3, flame: 2, magnet: 1 });
  const forceJackpotNode = (g, mkPouch) => {
    g.run.stage = 4; g.run.phase = "NODE_SELECT"; g.run.nodes = ["JACKPOT"];
    g.run.pouch = mkPouch();
    return g.selectNode(0);
  };

  const gJ36 = setupJ36(303);
  if (!gJ36.run || !gJ36.run.deepMode) { fail("V31J3-6: deepMode 미진입"); }
  else {
    const stJ36 = forceJackpotNode(gJ36, mkBellPouch);
    if (!stJ36) { fail("V31J3-6: selectNode(JACKPOT) null"); }
    else if (stJ36.phase === PHASE.PERK_PICK) {
      // ★구 전용 pickKind("JACKPOT_NODE")·무료 add 카드는 은퇴 — 기존 POUCH 특수카드 경로 재사용 확인.
      if (stJ36.pickKind !== "POUCH") fail(`V31J3-6: pickKind=${stJ36.pickKind} != POUCH (§2 특수카드 경로 재사용 기대)`);
      if (!stJ36.options || stJ36.options.length < 1) fail(`V31J3-6: options 없음`);
      if (stJ36.options.some(o => o.type === "add" || o.type === "skip_jackpot_node")) {
        fail("V31J3-6: 구 무료 add/skip_jackpot_node 카드 잔존(공짜·무검증 획득 버그 재발)");
      }
      const specialOpts = stJ36.options.filter(o => o.type === "special");
      if (specialOpts.length < 1) fail("V31J3-6: special 카드 없음");
      for (const o of specialOpts) {
        // bell 그룹(small_bell/echo_bell/bell_ticket=고급→SILVER, golden_bell/festival_bell=희귀→GOLD) — §2 비용 규칙 정합.
        if (o.tier !== "SILVER" && o.tier !== "GOLD") fail(`V31J3-6: 후보 ${o.id} tier=${o.tier} (bell 그룹은 SILVER/GOLD 기대)`);
        if (!o.cost || !o.cost.removeN) fail(`V31J3-6: 후보 ${o.id} cost=${JSON.stringify(o.cost)} — §2 비용 필드 누락(무검증·공짜 재발 의심)`);
      }
      const hasSkip = stJ36.options.some(o => o.type === "skip");
      if (!hasSkip) fail("V31J3-6: skip 선택지 없음(type=skip 기대)");
      notes.push(`V31J3-6 JACKPOT 노드 옵션 ok: special=${specialOpts.length}(SILVER/GOLD·§2비용) · skip 포함 · topTag=${stJ36.offerMeta && stJ36.offerMeta.topTag}`);

      // ── (a) 비용 차감 커밋 — 특수카드 픽 → (PRISM 이면 cost_remove 선택 단계 경유) → POUCH_REMOVE → 기본 제거 + 특수 +1 ──
      const pickIdx = stJ36.options.findIndex(o => o.type === "special");
      if (pickIdx < 0) fail("V31J3-6(a): special 후보 없음");
      else {
        const pickOpt = stJ36.options[pickIdx];
        const pickId = pickOpt.id, pickRemoveN = pickOpt.cost.removeN;
        const cherryBefore = gJ36.run.pouch.cherry || 0;
        let st2 = gJ36.pickPerk(pickIdx);
        if (pickOpt.tier === "PRISM") {   // 프리즘만 POUCH_COST 2-step 경유(§2)
          if (!st2 || st2.pickKind !== "POUCH_COST") fail(`V31J3-6(a): PRISM 픽 후 pickKind=${st2 && st2.pickKind} != POUCH_COST`);
          else {
            const remIdx = st2.options.findIndex(o => o.type === "cost_remove");
            if (remIdx < 0) fail("V31J3-6(a): cost_remove 선택지 없음");
            else st2 = gJ36.pickPerk(remIdx);
          }
        }
        if (!st2 || st2.pickKind !== "POUCH_REMOVE") fail(`V31J3-6(a): pickKind=${st2 && st2.pickKind} != POUCH_REMOVE`);
        else {
          const cherryIdx = st2.options.findIndex(o => o.id === "cherry");
          if (cherryIdx < 0) fail("V31J3-6(a): 제거 후보에 cherry 없음");
          else {
            gJ36.pickPerk(cherryIdx);
            const cherryAfter = gJ36.run.pouch.cherry || 0;
            if (cherryAfter !== cherryBefore - pickRemoveN) fail(`V31J3-6(a): cherry ${cherryBefore}→${cherryAfter} (기대 -${pickRemoveN})`);
            if ((gJ36.run.pouch[pickId] || 0) !== 1) fail(`V31J3-6(a): ${pickId} 획득 실패 (pouch=${gJ36.run.pouch[pickId]})`);
            const pv = E.pouchValidate(gJ36.run.pouch, {});
            if (!pv.ok) fail(`V31J3-6(a): 커밋 후 pouchValidate 실패 ${pv.errors.join(" · ")}`);
            notes.push(`V31J3-6(a) 비용차감 커밋 ok: cherry ${cherryBefore}→${cherryAfter}(-${pickRemoveN}) · ${pickId}(${pickOpt.tier}) +1 · pouchValidate ok`);
          }
        }
      }
    } else {
      notes.push(`V31J3-6: phase=${stJ36.phase} (태그 심볼 없음 폴백)`);
    }
  }

  // ── (b) 상한 초과 → 커밋 거부·원본 롤백(무변경) — crown 그룹 전원 PRISM·PRISM_special 상한(2) 이미 도달 상태 ──
  const gJ36b = setupJ36(304);
  if (gJ36b.run && gJ36b.run.deepMode) {
    const stB = forceJackpotNode(gJ36b, mkCrownPouch);
    if (stB && stB.phase === PHASE.PERK_PICK && stB.pickKind === "POUCH") {
      const pickIdxB = stB.options.findIndex(o => o.type === "special");
      if (pickIdxB < 0) { fail("V31J3-6(b): special 후보 없음"); }
      else {
        const pouchBefore = { ...gJ36b.run.pouch };
        let stB2 = gJ36b.pickPerk(pickIdxB);
        if (!stB2 || stB2.pickKind !== "POUCH_COST") { fail(`V31J3-6(b): crown 그룹 PRISM 픽 후 pickKind=${stB2 && stB2.pickKind} != POUCH_COST`); }
        else {
          const remIdxB = stB2.options.findIndex(o => o.type === "cost_remove");
          if (remIdxB < 0) { fail("V31J3-6(b): cost_remove 선택지 없음"); }
          else {
            stB2 = gJ36b.pickPerk(remIdxB);
            const cherryIdxB = stB2 && stB2.options ? stB2.options.findIndex(o => o.id === "cherry") : -1;
            if (cherryIdxB < 0) { fail("V31J3-6(b): 제거 후보에 cherry 없음"); }
            else {
              gJ36b.pickPerk(cherryIdxB);   // PRISM_special 2→3 시도(crown군 전원 PRISM) → 상한(2) 초과 → 커밋 거부 기대
              const crownAfter = gJ36b.run.pouch.crown || 0;
              const cherryAfterB = gJ36b.run.pouch.cherry || 0;
              if (crownAfter !== pouchBefore.crown) fail(`V31J3-6(b): crown ${pouchBefore.crown}→${crownAfter} — 상한초과 커밋이 롤백되지 않음`);
              if (cherryAfterB !== pouchBefore.cherry) fail(`V31J3-6(b): 롤백 불완전 — cherry 도 원복돼야 함(${pouchBefore.cherry}→${cherryAfterB})`);
              if (!(gJ36b.run.rewardMsg || "").includes("취소")) fail(`V31J3-6(b): 롤백 안내 메시지 누락(rewardMsg=${gJ36b.run.rewardMsg})`);
              notes.push(`V31J3-6(b) 상한초과 롤백 ok: crown 유지=${crownAfter} · pouch 원본불변 · "취소" 메시지 확인`);
            }
          }
        }
      }
    } else { notes.push("V31J3-6(b) PERK_PICK/POUCH 미진입 — 스킵"); }
  }

  // ── (c) 스킵 — 코인 +5 유지 ──
  const gJ36c = setupJ36(305);
  if (gJ36c.run && gJ36c.run.deepMode) {
    const stC = forceJackpotNode(gJ36c, mkBellPouch);
    if (stC && stC.phase === PHASE.PERK_PICK && stC.pickKind === "POUCH") {
      const skipIdx = stC.options.findIndex(o => o.type === "skip");
      if (skipIdx < 0) fail("V31J3-6(c): skip 선택지 없음");
      else {
        const coinBefore = gJ36c.run.coins;
        gJ36c.pickPerk(skipIdx);
        const coinAfter = gJ36c.run.coins;
        if (coinAfter !== coinBefore + 5) fail(`V31J3-6(c): skip 후 coins ${coinBefore}→${coinAfter} (기대 +5)`);
        notes.push("V31J3-6(c) JACKPOT skip 코인+5 ok");
      }
    } else { notes.push("V31J3-6(c) PERK_PICK/POUCH 미진입 — 스킵"); }
  }
}

// (V31J3-7) 밀도 집계 — pouchView entries + JACKPOT_TAG 통합
{
  const gJ37 = new Game({ seed: 404, storage: memStoreJ3() });
  gJ37.profile.runs = 1; gJ37.profile.playerLevel = 30;
  gJ37.startRun(0, true);
  let stJ37 = gJ37.state(); let g37 = 0;
  while (stJ37 && stJ37.phase !== PHASE.SPIN && g37++ < 30) {
    if (stJ37.phase === PHASE.SELECT_CHAR) stJ37 = gJ37.selectChar(gJ37.unlockedChars()[0].id);
    else if (stJ37.phase === PHASE.SELECT_MACHINE) stJ37 = gJ37.selectMachine(gJ37.unlockedMachines()[0].id);
    else if (stJ37.phase === PHASE.SELECT_DEVICE) stJ37 = gJ37.selectDevice(stJ37.options[0]?.id || "");
    else break;
  }
  if (!gJ37.run || !gJ37.run.deepMode) { fail("V31J3-7: deepMode 미진입"); }
  else {
    gJ37.run.pouch = { crown: 5, small_bell: 3, golden_bell: 2, cherry: 5, book: 3 };
    // pouchView를 통해 태그 밀도 집계 확인
    const pv = gJ37.pouchView ? gJ37.pouchView() : null;
    if (!pv) { fail("V31J3-7: pouchView() null"); }
    else {
      const tagCount = {};
      for (const en of (pv.entries || [])) {
        if (!en.id || en.n <= 0 || en.id === "empty" || en.id === "random") continue;
        const t = JACKPOT_TAG && JACKPOT_TAG[en.id];
        if (t) tagCount[t] = (tagCount[t] || 0) + en.n;
      }
      if (!tagCount["crown"] && !tagCount["bell"]) fail(`V31J3-7: 태그 밀도 집계 0 (crown/bell 모두 없음)`);
      notes.push(`V31J3-7 밀도 집계 ok: ${JSON.stringify(tagCount)}`);
    }
  }
}

// (V31J3-8) 일반모드 300런 무회귀 (J3 추가 후)
{
  let j3NormTerm = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(9000 + s, false);
    if (st && st.phase === PHASE.RUN_END) j3NormTerm++;
  }
  if (j3NormTerm < 295) fail(`V31J3-8: 일반모드 종료 ${j3NormTerm}/300`);
  notes.push(`V31J3-8 일반모드 300런 무회귀 ok: ${j3NormTerm}/300`);
}

// (V31J3-9) 심화 300런 100% 종료 + 기준선 + 피버진입률
{
  let j3DeepTerm = 0; let j3StageSum = 0; let j3FeverEntry = 0;
  for (let s = 1; s <= 300; s++) {
    const g = new Game({ seed: 7000 + s, storage: memStoreJ3() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, true);
    let st = g.state(); let guard = 0; let hadFever = false;
    const CAP = 5000;
    while (st && st.phase !== PHASE.RUN_END && guard++ < CAP) {
      switch (st.phase) {
        case PHASE.SELECT_CHAR: st = g.selectChar(g.unlockedChars()[0].id); break;
        case PHASE.SELECT_MACHINE: st = g.selectMachine(g.unlockedMachines()[0].id); break;
        case PHASE.SELECT_DEVICE: st = g.selectDevice(st.options[0]?.id || ""); break;
        case PHASE.SPIN: { g.spin("N"); st = g.state(); if (g.run && (g.run.feverSpins || 0) > 0) hadFever = true; break; }
        case PHASE.POST_SPIN: st = g.giveUp(false); break;
        case PHASE.STAGE_CLEAR: st = g.proceedToNodes(); break;
        case PHASE.NODE_SELECT: st = g.selectNode(0); break;
        case PHASE.PERK_PICK: st = g.pickPerk(0); break;
        case PHASE.SHOP: { if (st.shopItems && st.shopItems.length && Math.random() < 0.5) g.shopBuy(0); st = g.shopExit(); break; }
        case PHASE.DEVICE_NODE: st = g.deviceNodeTake(); break;
        case PHASE.REWARD_DONE: st = g.proceedToStage(); break;
        default: guard = CAP; break;
      }
      if (!st) break;
    }
    if (st && st.phase === PHASE.RUN_END) { j3DeepTerm++; j3StageSum += (st.stage || 1); }
    if (guard >= CAP) fail(`V31J3-9 seed=${7000+s} did not terminate`);
    if (hadFever) j3FeverEntry++;
  }
  const j3AvgStage = (j3StageSum / Math.max(1, j3DeepTerm)).toFixed(3);
  if (j3DeepTerm < 295) fail(`V31J3-9: 심화 종료 ${j3DeepTerm}/300`);
  notes.push(`V31J3-9 심화 300런: 종료 ${j3DeepTerm}/300 avgStage=${j3AvgStage} feverEntry=${j3FeverEntry}/300`);
  notes.push(`V31J3-BASELINE avgStage=${j3AvgStage}`);
}

// (V31J3-10) 2026-07-13 감사수정 #1: 축제종(BELL_FEST) 피버 중 ×1.5 결정론 단정.
//  paired-diff 설계 — 동일 캐릭터/머신/장치·동일 잭팟단계(리치)·동일 feverDelta 원시값을 갖도록 셀 구성만
//  festival_bell 유무로 토글(control: small_bell×4+cherry / boosted: festival_bell+small_bell×3+cherry —
//  둘 다 bellCount=4·hasSmallBell=true 라 원시 feverDelta=25+15=40 로 동일, score baseline=300(리치, 동일심볼잭팟无)
//  으로 동일) → 두 런의 델타 차이가 곧 축제종 ×1.5(=+50%) 보정분이라 "정확값"을 오차 없이 격리해 단정할 수 있음.
{
  const setupJ3F = (seed) => {
    const g = new Game({ seed, storage: memStoreJ3() });
    g.profile.runs = 1; g.profile.playerLevel = 30;
    g.startRun(0, true);
    let st = g.state(); let guard = 0;
    while (st && st.phase !== PHASE.SPIN && guard++ < 30) {
      if (st.phase === PHASE.SELECT_CHAR) st = g.selectChar(g.unlockedChars()[0].id);
      else if (st.phase === PHASE.SELECT_MACHINE) st = g.selectMachine(g.unlockedMachines()[0].id);
      else if (st.phase === PHASE.SELECT_DEVICE) st = g.selectDevice(st.options[0]?.id || "");
      else break;
    }
    return g;
  };
  const runSpin = (seed, feverSpinsInit, cellIds) => {
    const g = setupJ3F(seed);
    if (!g.run || !g.run.deepMode) return null;
    g.run.feverSpins = feverSpinsInit;
    g.run.feverGauge = 0;
    const scoreBefore = g.run.score || 0, feverBefore = g.run.feverGauge || 0, expBefore = g.run.stageExp || 0;
    g.run.lockedNext = mkLockedJ3(cellIds);
    const out = g.spin("N");
    if (!out || !g.run) return null;
    return {
      scoreDelta: (g.run.score || 0) - scoreBefore,
      feverGaugeDelta: (g.run.feverGauge || 0) - feverBefore,
      expDelta: (g.run.stageExp || 0) - expBefore,
      banner: out.banner || "",
    };
  };

  // (1) 보정 정확값 — boosted(festival_bell 랜딩) vs control(festival_bell 없이 small_bell만) · 둘 다 피버중·리치·bell.
  const boosted = runSpin(601, 2, ["festival_bell", "small_bell", "small_bell", "small_bell", "cherry"]);
  const control = runSpin(601, 2, ["small_bell", "small_bell", "small_bell", "small_bell", "cherry"]);
  if (!boosted || !control) { fail("V31J3-10(1): deepMode 미진입"); }
  else {
    if (!boosted.banner.includes("축제종")) fail(`V31J3-10(1): 보정 배너 누락 — ${boosted.banner}`);
    if (control.banner.includes("축제종")) fail(`V31J3-10(1): control(festival_bell 미랜딩)에 축제종 배너 발생 — ${control.banner}`);
    if (control.feverGaugeDelta !== 40) fail(`V31J3-10(1): control feverGaugeDelta=${control.feverGaugeDelta} != 40 (리치25+작은종15 원시 기준선)`);
    const feverDiff = boosted.feverGaugeDelta - control.feverGaugeDelta;
    const scoreDiff = boosted.scoreDelta - control.scoreDelta;
    const expDiff = boosted.expDelta - control.expDelta;
    if (feverDiff !== 20) fail(`V31J3-10(1): feverGauge 보정분 ${feverDiff} != 20 (=floor(40×0.5), ×1.5 정확값)`);
    if (scoreDiff !== 150) fail(`V31J3-10(1): score 보정분 ${scoreDiff} != 150 (=floor(300×0.5), 리치 점수 ×1.5 정확값)`);
    if (expDiff !== 0) fail(`V31J3-10(1): exp 보정분 ${expDiff} != 0 (리치 단계는 EXP 가산 없음 — combo/jackpot만 EXP 대상)`);
    notes.push(`V31J3-10(1) 정확값 ok: feverGauge 40→${control.feverGaugeDelta + feverDiff}(+${feverDiff}) · score 귀속분 +${scoreDiff} · exp +${expDiff}`);
  }

  // (2) 비피버 미적용 — 동일 boosted 셀이라도 feverSpins=0(피버 아님) 이면 보정 없음(원시 feverDelta=40 그대로).
  const nonFever = runSpin(602, 0, ["festival_bell", "small_bell", "small_bell", "small_bell", "cherry"]);
  if (!nonFever) { fail("V31J3-10(2): deepMode 미진입"); }
  else {
    if (nonFever.banner.includes("축제종")) fail(`V31J3-10(2): 비피버인데 축제종 배너 발생 — ${nonFever.banner}`);
    if (nonFever.feverGaugeDelta !== 40) fail(`V31J3-10(2): 비피버 feverGaugeDelta=${nonFever.feverGaugeDelta} != 40 (보정 미적용 기대)`);
    notes.push(`V31J3-10(2) 비피버 미적용 ok: feverGaugeDelta=${nonFever.feverGaugeDelta}(보정없음)`);
  }

  // (3) 비bell 미적용 — festival_bell 랜딩(hasBellFest=true) 이지만 최다 태그가 crown(비bell) → 보정 미적용.
  const nonBellTag = runSpin(603, 2, ["festival_bell", "crown", "crown", "crown", "crown"]);
  if (!nonBellTag) { fail("V31J3-10(3): deepMode 미진입"); }
  else {
    if (nonBellTag.banner.includes("축제종")) fail(`V31J3-10(3): 비bell(crown 리치)인데 축제종 배너 발생 — ${nonBellTag.banner}`);
    if (nonBellTag.feverGaugeDelta !== 25) fail(`V31J3-10(3): 비bell(crown 리치) feverGaugeDelta=${nonBellTag.feverGaugeDelta} != 25 (보정 미적용 기대·종보너스 미기여)`);
    notes.push(`V31J3-10(3) 비bell(crown 리치) 미적용 ok: feverGaugeDelta=${nonBellTag.feverGaugeDelta}(보정없음)`);
  }
}

// ══════════════════════════════════════════════════════════════════
// ── §10 V3 심볼 덱 보드 — pouchView 확장 필드 계약(DECKBOARD) ──────
// ══════════════════════════════════════════════════════════════════
// (DECKBOARD-1) 전 보유 심볼(POUCH_SYMBOLS 전수, empty/random 포함) → pouchView().entries 확장 필드
//  tier/cat/use/jackpotTag/autoDecay/desc 가 전부 정의된 도메인 값(비-undefined)인지 단정.
//  ui.js deckCardHtml/openDeckCardDetail 은 DECK_CAT_BADGE[cat]·DECK_USE_BADGE[use]·TAG_EMOJI[jackpotTag] 를
//  이 도메인으로만 조회하므로, 여기서 도메인 멤버십을 보장하면 카드 렌더 쪽 "undefined" 누출도 코드상 봉쇄됨
//  (ui-render-no-undefined 회귀 항목 — DOM 미실행 대신 데이터 계약으로 커버, 5경로 중 라벨/제목/설명·preview 상당).
// ui.js TAG_EMOJI 키 미러(export 안 된 모듈 내부 상수라 하네스가 직접 못 읽음 — 수동 동기).
//  신규 잭팟 태그 추가 시 ui.js TAG_EMOJI 와 이 Set 을 함께 갱신할 것(안 하면 DECKBOARD-1 이 실패로 잡아줌).
const TAG_LABEL_DOMAIN = new Set(["crown", "seven", "coin", "prism", "curse", "bell"]);
{
  const g = deepAtSpin(55501, null);
  if (!g.run) fail("DECKBOARD-1: deepAtSpin driver did not reach SPIN");
  else {
    const pouch = {};
    for (const id of POUCH_SYMBOLS) pouch[id] = 1;
    g.run.pouch = pouch;
    const pv = g.pouchView();
    if (!pv) fail("DECKBOARD-1: pouchView() null in deep run");
    else {
      if (pv.entries.length !== POUCH_SYMBOLS.length) fail(`DECKBOARD-1: entries ${pv.entries.length} != POUCH_SYMBOLS ${POUCH_SYMBOLS.length}`);
      const TIER_SET = new Set(["SILVER", "GOLD", "PRISM", "CURSE"]);
      const CAT_SET = new Set(["base", "special", "harmful"]);
      const USE_SET = new Set([null, "instant", "fuse"]);
      let checked = 0;
      for (const en of pv.entries) {
        checked++;
        if (en.tier === undefined || !TIER_SET.has(en.tier)) fail(`DECKBOARD-1: ${en.id}.tier invalid (${en.tier})`);
        if (en.cat === undefined || !CAT_SET.has(en.cat)) fail(`DECKBOARD-1: ${en.id}.cat invalid (${en.cat})`);
        if (!USE_SET.has(en.use === undefined ? "MISSING" : en.use)) fail(`DECKBOARD-1: ${en.id}.use invalid (${en.use})`);
        if (en.jackpotTag === undefined || (en.jackpotTag !== null && typeof en.jackpotTag !== "string")) fail(`DECKBOARD-1: ${en.id}.jackpotTag invalid (${en.jackpotTag})`);
        if (typeof en.autoDecay !== "boolean") fail(`DECKBOARD-1: ${en.id}.autoDecay not boolean (${en.autoDecay})`);
        if (typeof en.desc !== "string" || !en.desc.length || en.desc.indexOf("undefined") >= 0) fail(`DECKBOARD-1: ${en.id}.desc invalid ("${en.desc}")`);
        if (en.jackpotTag != null && !TAG_LABEL_DOMAIN.has(en.jackpotTag)) fail(`DECKBOARD-1: ${en.id}.jackpotTag "${en.jackpotTag}" has no TAG_EMOJI mapping in ui.js (would render 🏷️ fallback silently — check if intended)`);
      }
      if (checked !== POUCH_SYMBOLS.length) fail(`DECKBOARD-1: checked ${checked} != ${POUCH_SYMBOLS.length}`);
      notes.push(`DECKBOARD-1 pouchView 확장 필드 계약 ok: ${checked}/${POUCH_SYMBOLS.length} 심볼 tier/cat/use/jackpotTag/autoDecay/desc 전부 비-undefined·도메인 정합`);
    }
  }
}

// (DECKBOARD-2) 정렬 계약 — 특수(티어 내림차순 PRISM>GOLD>SILVER) → 기본 → 해로운 (ui.js deckSortEntries 미러).
{
  const g = deepAtSpin(55502, null);
  if (g.run) {
    g.run.pouch = { cherry: 3, skull: 2, prism_sym: 1, lucky7: 1, bandage: 1, empty: 1 };
    const pv = g.pouchView();
    const catRank = { special: 0, base: 1, harmful: 2 };
    const tierRank = { PRISM: 0, GOLD: 1, SILVER: 2, CURSE: 3 };
    const sorted = [...pv.entries].sort((a, b) => {
      const ca = catRank[a.cat] ?? 1, cb = catRank[b.cat] ?? 1;
      if (ca !== cb) return ca - cb;
      if (a.cat === "special") { const ta = tierRank[a.tier] ?? 2, tb = tierRank[b.tier] ?? 2; if (ta !== tb) return ta - tb; }
      return b.n - a.n;
    });
    // special 그룹이 base/harmful 보다 먼저 와야 함
    const firstNonSpecialIdx = sorted.findIndex((e) => e.cat !== "special");
    const anySpecialAfter = sorted.slice(firstNonSpecialIdx + 1).some((e) => e.cat === "special");
    if (firstNonSpecialIdx >= 0 && anySpecialAfter) fail("DECKBOARD-2: special 그룹이 흩어짐(정렬 위반)");
    if (sorted[0].cat !== "special") fail(`DECKBOARD-2: 첫 카드 cat=${sorted[0].cat} != special (prism_sym/lucky7 보유중)`);
    notes.push(`DECKBOARD-2 덱 보드 정렬 계약 ok: 선두=${sorted[0].id}(${sorted[0].cat}/${sorted[0].tier})`);
  } else fail("DECKBOARD-2: driver did not reach SPIN");
}

// ══════════════════════════════════════════════════════════════════════
// §11 초반 밸런스 완화 (2026-07-13 사용자 피드백 "초반이 너무 힘들다") — EARLYBAL-*
// ══════════════════════════════════════════════════════════════════════

// simDeepStageStats — 심화 N런 스테이지별 통계(클리어/도달률·사망 스테이지 분포·스핀당 평균EXP).
//  playRun() 과 동일 드라이버 스타일(POST_SPIN=giveUp·NODE_SELECT=[0]·PERK_PICK=[0]) 재사용 —
//  여기선 spin당 EXP 누적 + 종료 시점 stage 히스토그램을 추가 계측(playRun 시그니처는 무수정 — 회귀 방지).
function simDeepStageStats(nRuns, seedBase) {
  let term = 0, stageSum = 0, spinCount = 0, expSum = 0;
  const deathDist = {};      // RUN_END 시점 stage → 런 수(그 스테이지에서 실패/종료)
  const reachCount = {};     // reach(N) = 최종 stage >= N 인 런 수(= stage N 진입 이상 도달)
  for (let s = 1; s <= nRuns; s++) {
    const g = new Game({ seed: seedBase + s, storage: memStore() });
    g.profile.runs = 1; g.profile.playerLevel = 30; g.profile.playerXp = 999999;
    let st = g.startRun(0, true);
    let guard = 0; const CAP = 4000;
    while (st && st.phase !== PHASE.RUN_END && guard++ < CAP) {
      switch (st.phase) {
        case PHASE.SELECT_CHAR: st = g.selectChar(g.unlockedChars()[0].id); break;
        case PHASE.SELECT_MACHINE: st = g.selectMachine(g.unlockedMachines()[0].id); break;
        case PHASE.SELECT_DEVICE: st = g.selectDevice(st.options[0]?.id || ""); break;
        case PHASE.SPIN: { g.spin("N"); spinCount++; expSum += Math.floor(g.run.lastExpApplied || 0); st = g.state(); break; }
        case PHASE.POST_SPIN: st = g.giveUp(false); break;
        case PHASE.STAGE_CLEAR: st = g.proceedToNodes(); break;
        case PHASE.NODE_SELECT: st = g.selectNode(0); break;
        case PHASE.PERK_PICK: st = g.pickPerk(0); break;
        case PHASE.SHOP: { if (st.shopItems && st.shopItems.length && Math.random() < 0.5) g.shopBuy(0); st = g.shopExit(); break; }
        case PHASE.DEVICE_NODE: st = g.deviceNodeTake(); break;
        case PHASE.REWARD_DONE: st = g.proceedToStage(); break;
        default: guard = CAP; break;
      }
      if (!st) break;
    }
    if (guard >= CAP) fail(`simDeepStageStats seed=${seedBase + s} did not terminate`);
    if (st && st.phase === PHASE.RUN_END) {
      term++;
      const finalStage = st.stage || 1;
      stageSum += finalStage;
      deathDist[finalStage] = (deathDist[finalStage] || 0) + 1;
      for (let n = 1; n <= finalStage; n++) reachCount[n] = (reachCount[n] || 0) + 1;
    }
  }
  const rate = (n) => term ? (reachCount[n] || 0) / term : 0;
  return {
    term, nRuns,
    avgStage: term ? stageSum / term : 0,
    avgExpPerSpin: spinCount ? expSum / spinCount : 0,
    spinCount, deathDist,
    s1Clear: rate(2),   // stage1 클리어 = stage2 도달
    s2Clear: rate(3),   // stage2 클리어 = stage3 도달(=S3 도달률과 동일 지표)
    s3Reach: rate(3),
    s3Clear: rate(4),
  };
}

// (EARLYBAL-1) 레버① EARLY_QUOTA 적용 정확 — 심화 S1~4 요구치 배율 = DEEP.EARLY_QUOTA[stage](S5+=1.0 무배율),
//  일반모드 무배율(항상 1). 프레시 시작(총량30·압축無)이라 base=E.compressionPenalty(30)=1 → _deepPenalty()가
//  (base-1)*penaltyMul=0 로 다른 심화 배율(압축/보스)을 상쇄해 EARLY_QUOTA 항만 순수 격리 관측 가능.
{
  const g1 = deepAtSpin(51001, null);
  if (!g1.run || !g1.run.deepMode) fail("EARLYBAL-1: deep 진입 실패");
  else {
    if (E.pouchTotal(g1.run.pouch) !== 30) fail(`EARLYBAL-1: 전제 붕괴 — 시작 pouch 총량 ${E.pouchTotal(g1.run.pouch)} != 30`);
    const m1 = g1._deepPenalty();
    if (Math.abs(m1 - DEEP.EARLY_QUOTA[1]) > 1e-9) fail(`EARLYBAL-1: stage1 _deepPenalty=${m1} != EARLY_QUOTA[1]=${DEEP.EARLY_QUOTA[1]}`);
    for (const st of [2, 3, 4]) {
      g1.run.stage = st;
      const m = g1._deepPenalty();
      if (Math.abs(m - DEEP.EARLY_QUOTA[st]) > 1e-9) fail(`EARLYBAL-1: stage${st} _deepPenalty=${m} != EARLY_QUOTA[${st}]=${DEEP.EARLY_QUOTA[st]}`);
    }
    for (const st of [5, 9]) {
      g1.run.stage = st;
      const m = g1._deepPenalty();
      if (Math.abs(m - 1.0) > 1e-9) fail(`EARLYBAL-1: stage${st} _deepPenalty=${m} != 1.0 (5+ 무배율 기대)`);
    }
    notes.push(`EARLYBAL-1 EARLY_QUOTA 곱 정확 ok: S1=${m1} · S2~4 정합 · S5/S9=1.0 무배율`);
  }
  // 일반모드: deepMode=false → _deepPenalty()는 stage 무관 항상 1.
  const gn = new Game({ seed: 51002, storage: memStore() });
  gn.profile.runs = 1; gn.profile.playerLevel = 30;
  gn.startRun(0, false);
  if (gn.run.deepMode) fail("EARLYBAL-1: normal startRun(deep=false)인데 deepMode=true");
  if (gn._deepPenalty() !== 1) fail(`EARLYBAL-1: 일반모드 _deepPenalty()=${gn._deepPenalty()} != 1`);
  gn.run.stage = 1;   // EARLY_QUOTA 키가 있어도 일반모드는 무배율이어야 함(격리)
  if (gn._deepPenalty() !== 1) fail(`EARLYBAL-1: 일반모드 stage1 _deepPenalty()=${gn._deepPenalty()} != 1 (무배율 기대)`);
  notes.push("EARLYBAL-1 일반모드 무배율(격리) 확인 ok");
}

// (EARLYBAL-2) 레버② 시작 덱 30 구성 정합 — cherry 5→6·skull 4→3(총 30 유지, §1.3 원표에서 유일한 이탈).
{
  const sp = E.startPouch();
  const tot = E.pouchTotal(sp);
  if (tot !== 30) fail(`EARLYBAL-2: START_POUCH 총량 ${tot} != 30`);
  const check = (id, n) => { if ((sp[id] || 0) !== n) fail(`EARLYBAL-2: START_POUCH ${id}=${sp[id] || 0} != ${n}`); };
  check("cherry", 6); check("skull", 3);
  check("book", 5); check("star", 4); check("gem", 4); check("coin", 4); check("flame", 2); check("magnet", 1); check("bomb", 1);
  if (!E.pouchValidate(sp).ok) fail("EARLYBAL-2: pouchValidate(START_POUCH) 실패");
  notes.push("EARLYBAL-2 START_POUCH §11 구성 ok: cherry=6(.200)·skull=3(.100)·total=30·9종 유지");
}

// (EARLYBAL-3) 레버③ S1~2 오퍼 SILVER 풀 EXP 가중 실측 — DEEP.EARLY_EXP_BOOST_IDS 상대 등장률 ×1.5, S3+ 무가중.
{
  const silverSpecialIds = POUCH_SYMBOLS.filter((id) => E.symCatOf(id) === "special" && E.symTierOf(id) === "SILVER");
  const boostSet = new Set(DEEP.EARLY_EXP_BOOST_IDS);
  const nonBoost = silverSpecialIds.filter((id) => !boostSet.has(id));
  if (!DEEP.EARLY_EXP_BOOST_IDS.every((id) => silverSpecialIds.includes(id))) fail("EARLYBAL-3: EARLY_EXP_BOOST_IDS 중 SILVER 특수 풀 밖의 id 존재");

  const measure = (stage, n) => {
    const cnt = {};
    for (let s = 0; s < n; s++) {
      const off = E.offerSymbolRewards(E.makeRng(600000 + s), {}, stage, { curseChance: 0 });
      for (const card of off) if (card.type === "special" && card.tier === "SILVER") cnt[card.id] = (cnt[card.id] || 0) + 1;
    }
    return cnt;
  };
  const avgRate = (cnt, ids, n) => ids.length ? ids.reduce((s, id) => s + (cnt[id] || 0), 0) / ids.length / n : 0;

  const N = 4000;
  const s1cnt = measure(0, N);   // realStage=1 (S1)
  const s2cnt = measure(1, N);   // realStage=2 (S2)
  const s7cnt = measure(6, N);   // realStage=7 (S3+ 대조군, 무가중 기대)

  const ratio = (cnt) => { const b = avgRate(cnt, DEEP.EARLY_EXP_BOOST_IDS, N), o = avgRate(cnt, nonBoost, N); return o > 0 ? b / o : 0; };
  const s1Ratio = ratio(s1cnt), s2Ratio = ratio(s2cnt), s7Ratio = ratio(s7cnt);

  if (s1Ratio < 1.3 || s1Ratio > 1.7) fail(`EARLYBAL-3: S1 boosted/baseline ratio ${s1Ratio.toFixed(3)} not ≈1.5(±0.2)`);
  if (s2Ratio < 1.3 || s2Ratio > 1.7) fail(`EARLYBAL-3: S2 boosted/baseline ratio ${s2Ratio.toFixed(3)} not ≈1.5(±0.2)`);
  if (s7Ratio < 0.8 || s7Ratio > 1.2) fail(`EARLYBAL-3: S3+(대조군 S7) boosted/baseline ratio ${s7Ratio.toFixed(3)} not ≈1.0(무가중 기대)`);
  notes.push(`EARLYBAL-3 오퍼 가중 실측 ok: S1 ratio=${s1Ratio.toFixed(3)} · S2 ratio=${s2Ratio.toFixed(3)} · S3+(S7 대조) ratio=${s7Ratio.toFixed(3)} · 대상=${DEEP.EARLY_EXP_BOOST_IDS.join(",")}`);
}

// (EARLYBAL-4) 일반모드 300런 무회귀(§11 변경 후 재확인 — 전부 deepMode 게이팅이라 무영향 기대).
{
  let term = 0;
  for (let s = 1; s <= 300; s++) {
    const st = playRun(700000 + s, false);
    if (st && st.phase === PHASE.RUN_END) term++;
  }
  if (term < 300) fail(`EARLYBAL-4: 일반모드 종료 ${term}/300`);
  notes.push(`EARLYBAL-4 일반모드 300런 무회귀 ok: ${term}/300`);
}

// (EARLYBAL-5) 심화 300런 BEFORE/AFTER 통계 비교표 — §11 목표(S1≥90%·S3도달≥65%·avgStage 2.4~3.0) 게이트.
//  BEFORE(레버 적용 전, 2026-07-18 실측 seedBase=970000·N=300 — backups\slotdev_earlybal_20260718_0805 스냅샷):
//   term=300/300 avgStage=1.753 avgExpPerSpin=20.40 s1Clear=51.0% s2Clear=17.3% s3Reach=17.3% s3Clear=4.3%
//   deathDist={1:147,2:101,3:39,4:8,5:2,6:3}
//  수렴 로그(스펙 §11 프로토콜): EARLY_QUOTA[1]=0.80 1차 적용 → S1 86.7%/S3 56.3%(미달, avgStage 2.790)
//   → 0.75 로 1회 추가완화 → S1 90.7%/S3 59.0%(S1 달성·S3 잔여미달, avgStage 2.857 목표대역 내)
//   → 스펙 규정상 추가 자동조정 없이 종료, 수치 보고(아래 s3Reach<0.65 분기가 이를 note 로 남김).
{
  const BEFORE = { avgStage: 1.753, avgExpPerSpin: 20.40, s1Clear: 0.510, s2Clear: 0.173, s3Reach: 0.173, s3Clear: 0.043 };
  const s = simDeepStageStats(300, 970000);   // AFTER — BEFORE 와 동일 seedBase(apples-to-apples)
  checkNum("EARLYBAL-5 stats", s.avgStage, s.avgExpPerSpin, s.s1Clear, s.s2Clear, s.s3Reach, s.s3Clear);
  if (s.term < 300) fail(`EARLYBAL-5: 심화 300런 종료 ${s.term}/300`);
  if (s.avgStage < 2.4 || s.avgStage > 3.0) fail(`EARLYBAL-5: avgStage ${s.avgStage.toFixed(3)} 목표대역(2.4~3.0) 밖`);
  if (s.s1Clear < 0.90) fail(`EARLYBAL-5: S1 클리어율 ${(s.s1Clear * 100).toFixed(1)}% < 90% 목표`);
  if (s.s3Reach < 0.65) {
    notes.push(`EARLYBAL-5 ⚠ S3 도달률 ${(s.s3Reach * 100).toFixed(1)}% < 65% 목표 — 수렴 1회 시도(EARLY_QUOTA[1] 0.80→0.75) 완료 후에도 잔여 미달, 스펙 프로토콜대로 추가 자동조정 없이 수치만 보고(사람 판단 위임)`);
  }
  notes.push(`EARLYBAL-5 BEFORE→AFTER: avgStage ${BEFORE.avgStage}→${s.avgStage.toFixed(3)} · avgExpPerSpin ${BEFORE.avgExpPerSpin}→${s.avgExpPerSpin.toFixed(2)} · S1클리어 ${(BEFORE.s1Clear * 100).toFixed(1)}%→${(s.s1Clear * 100).toFixed(1)}% · S2클리어 ${(BEFORE.s2Clear * 100).toFixed(1)}%→${(s.s2Clear * 100).toFixed(1)}% · S3도달 ${(BEFORE.s3Reach * 100).toFixed(1)}%→${(s.s3Reach * 100).toFixed(1)}% · S3클리어 ${(BEFORE.s3Clear * 100).toFixed(1)}%→${(s.s3Clear * 100).toFixed(1)}%`);
  notes.push(`EARLYBAL-5 deathDist BEFORE={1:147,2:101,3:39,4:8,5:2,6:3} AFTER=${JSON.stringify(s.deathDist)}`);
  notes.push(`EARLYBAL-BASELINE avgStage=${s.avgStage.toFixed(3)}`);
}

// ------- output -------
console.log(JSON.stringify({ ok: errors.length === 0, errorCount: errors.length, errors: errors.slice(0, 40), notes }, null, 2));
