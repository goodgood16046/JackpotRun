// 잭팟런 도감/진행도 — jackpotdex/<token> 1회 읽기 + 무료 생성 이미지(img/<key>.png)
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js";
import { getDatabase, ref, get, set } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-database.js";

// 독립 프로젝트 jackpotrun-web (모카봇 mokabot-8ed4d 에서 분리). 봇이 이 프로젝트 RTDB로 잭팟 데이터 push.
const firebaseConfig = {
  apiKey: "AIzaSyB2mxXO01_Rbaz3S_gyVYvPlnnGcYsLGjM",
  authDomain: "jackpotrun-web.firebaseapp.com",
  databaseURL: "https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app",
  projectId: "jackpotrun-web",
  storageBucket: "jackpotrun-web.firebasestorage.app",
  messagingSenderId: "52817920989",
  appId: "1:52817920989:web:02b32f4499f4dd4f4d6c34",
};

// ── 정적 카탈로그: [emoji, name, desc] ──
const ACH = {
  cherry100: ["🍒", "체리 수확가", "🍒체리 누적 100개"], cherry500: ["🍒", "체리 중독", "🍒체리 누적 500개"],
  crown10: ["👑", "왕관 수집가", "👑왕관 누적 10개"], crown30: ["👑", "대관식", "👑왕관 누적 30개"],
  jackpot1: ["🎰", "첫 잭팟", "5칸 잭팟 1회"], jackpot10: ["🎰", "잭팟 헌터", "5칸 잭팟 10회"],
  boss1: ["📝", "중간고사 통과", "보스 1회 클리어"], boss5: ["🎓", "졸업반", "보스 5회 클리어"],
  stage10: ["🧗", "10층 등반", "스테이지 10 도달"], stage15: ["🏔️", "최종보스 도달", "스테이지 15 도달"],
  lastclear5: ["⏰", "벼락치기 천재", "마지막 스핀 클리어 5회"], exact1: ["🎯", "완벽한 계산", "요구 EXP 정확 일치"],
  prism5: ["🌈", "규칙 파괴자", "프리즘 증강 5회"], score10k: ["💯", "만점왕", "최고 10,000점"],
  score50k: ["🏆", "슬롯의 지배자", "최고 50,000점"], runs20: ["🔁", "단골", "20런 플레이"],
};
const CHARS = {
  novice: ["🎒", "초보학생", "요구치↓ · 점수보정 ×0.9 (입문)"], scholar: ["📗", "장학생", "📘책 +2 · 클리어 코인 +2"],
  gambler: ["🎲", "도박꾼", "점수보정 ×1.1 · 스테이지당 1회 무료 재굴림"], farmer: ["🍒", "체리농부", "🍒체리 +1 · 희귀↓ (안정)"],
  parttime: ["🪙", "알바생", "시작코인 +15 · 첫스핀 -20%"], jeweler: ["💎", "보석상", "💎보석 점수 +20 · 점수보정 ×1.1"],
  honor: ["🎓", "수석졸업생", "실버 증강 1개로 시작"], cultist: ["💀", "해골숭배자", "☠해골 EXP +3 · 저주당 점수 +8%"],
  crowncol: ["👑", "왕관수집가", "👑왕관 점수 +30 · 등장↑"], minimalist: ["🍃", "미니멀리스트", "유물 3개↓면 EXP +40%"],
  lucky: ["🍀", "행운아", "희귀심볼 등장 +25% (한방)"], highroller: ["💠", "큰손", "💎보석 점수 +25 · 시작코인 +12"],
  monk: ["🧘", "수도승", "스핀 -1 · 요구치 -18% (속전속결)"], alchemist: ["⚗️", "연금술사", "코인 +40% · 클리어코인 +3"],
  daredevil: ["😈", "무모한도전", "모든 EXP +20% · 요구치 +10% (고위험)"], prodigy: ["🌟", "천재", "모든 EXP +12% · 점수보정 ×0.95"],
};
const MACHINES = {
  basic: ["🎰", "기본", "표준 확률 (입문)"], cherry: ["🍒", "체리", "체리↑·왕관↓ (안정)"],
  library: ["📚", "도서관", "책↑·코인/보석↓ (경험치)"], gem: ["💎", "보석", "보석↑·체리/책↓ (점수 ×1.1)"],
  magnet: ["🧲", "자석", "자석↑ (콤보)"], skull: ["☠", "해골", "해골↑·고위험 (점수 ×1.15)"],
  crown: ["👑", "왕관", "왕관↑·기본↓ (운빨고점 ×1.2)"], flame: ["🔥", "불꽃", "불꽃·해골↑ (배율 ×1.1)"],
  bomb: ["💣", "폭탄", "폭탄↑ (제거/계산 ×1.1)"],
  star: ["⭐", "별빛", "별↑·세트 잘맞음 (콤보)"], clover: ["🍀", "행운", "희귀·코인·불꽃↑ (행운)"],
  casino: ["🎲", "카지노", "🎲주사위 등장·고변동 (운빨)"], garden: ["🌱", "정원", "🌱씨앗 등장·성장형"],
  wildmac: ["🌀", "와일드", "🌀와일드 등장 (세트 조작)"], vault: ["🗝", "금고", "🗝열쇠 등장·코인↑"],
  rainbow: ["🌈", "무지개", "⭐💎👑 등장↑·고변동 (한방)"],
};
// 키는 bare(이미지 키 = "dev_"+key 로 prompts.json의 dev_* 와 일치)
const DEVICES = {
  seal: ["🔒", "봉인장막", "장착 시 모든 스핀 ☠해골 미등장·EXP +5% (패시브)"], coin: ["🪙", "코인투입구", "투입 — 코인5 → 다음 스핀 EXP +30%"],
  safe: ["🦺", "안전벨트", "장착 시 모든 스핀 최소 EXP 보장 (패시브)"], reroll: ["🔄", "재굴림기", "재굴림 — 직전 결과 다시 굴림(점수 -10%)"],
  pin: ["📌", "고정핀", "고정 N — N번 칸 유지·나머지 재굴림"], flame: ["🔥", "불꽃엔진", "장착 시 모든 스핀 EXP +15% (패시브)"],
  overheat: ["♨️", "과열코어", "장착 시 EXP +18%·☠해골 +1 (패시브·고위험)"], subreel: ["➕", "보조릴", "6칸 슬롯·최종 EXP -30% (패시브)"],
  oracle: ["🔮", "예언안경", "예언 — 다음 스핀 미리보고 확정"], copy: ["📑", "복사기", "복사 N — N번 칸 옆칸에 복사"],
  swap: ["🔃", "교체기", "교체 N — N번 칸 최다 심볼로 교체"], bell: ["🔔", "비상졸업벨", "비상 — 부족≤25 즉시 클리어(1회 파괴)"],
};

const $ = (id) => document.getElementById(id);
const app = initializeApp(firebaseConfig);
const db = getDatabase(app);
let DATA = null;
let CATALOG = null;
let HALL = null;
let TOKEN = null;

boot();
async function boot() {
  const t = new URLSearchParams(location.search).get("t");
  TOKEN = t;
  if (!t) return fail("토큰이 없습니다. 카톡에서 \"잭팟웹\" 으로 링크를 받아주세요.");
  try {
    const [snap, catSnap, hallSnap] = await Promise.all([
      get(ref(db, "jackpotdex/" + t)),
      get(ref(db, "jackpotcatalog")).catch(() => null),
      get(ref(db, "jackpothall")).catch(() => null),
    ]);
    if (!snap.exists()) return fail("아직 기록이 없어요. 카톡에서 \"잭팟\" 한 판 돌리고 다시 열어주세요.");
    DATA = snap.val();
    CATALOG = catSnap && catSnap.exists() ? catSnap.val() : null;
    HALL = hallSnap && hallSnap.exists() ? hallSnap.val() : null;
    // 캐릭/머신 desc 는 엔진 카탈로그(jackpotcatalog) 우선 — 정적 CHARS/MACHINES 의 stale 방지
    if (CATALOG && CATALOG.chars) CATALOG.chars.forEach((c) => { CHARS[c.id] = [c.e, c.n, c.d]; });
    if (CATALOG && CATALOG.machines) CATALOG.machines.forEach((c) => { MACHINES[c.id] = [c.e, c.n, c.d]; });
    render();
  } catch (e) { fail("불러오기 실패: " + e.message); }
}
function fail(msg) { $("loading").classList.add("hidden"); const e = $("error"); e.textContent = msg; e.classList.remove("hidden"); }

// 이미지(없으면 이모지 fallback)
const imgTag = (key, emoji) =>
  `<img class="ico" src="img/${key}.png" alt="" loading="lazy" onerror="this.replaceWith(Object.assign(document.createElement('div'),{className:'ico emoji',textContent:'${emoji}'}))">`;

function render() {
  $("loading").classList.add("hidden");
  $("who").textContent = "@" + (DATA.nick || "?") + (DATA.title ? " · " + DATA.title : "");
  $("stats").innerHTML = [
    stat("🏆 최고", num(DATA.bestScore) + "점"), stat("🧗 최고 스테이지", "S" + (DATA.bestStage || 0)),
    stat("🔁 런", (DATA.runs || 0) + "회"), stat("📈 통산", num(DATA.totalScore) + "점"),
  ].join("");
  const done = DATA.achDone || 0, total = DATA.achTotal || Object.keys(ACH).length;
  $("achFill").style.width = (total ? (done / total * 100) : 0) + "%";
  $("achLabel").textContent = `🏅 업적 ${done}/${total}`;
  renderAch(); renderChal(); renderDex(); renderMastery(); renderBuild(); renderRecords(); renderRank(); renderHall();
  $("foot").textContent = "업데이트: " + (DATA.updatedAt ? new Date(DATA.updatedAt).toLocaleString("ko-KR") : "-") + " · 이미지는 AI 자동생성";
  setupTabs();
}
const stat = (k, v) => `<div class="stat"><div class="k">${k}</div><div class="v">${v}</div></div>`;
const num = (n) => (n || 0).toLocaleString("ko-KR");

const ACH_CAT_ORDER = ["입문", "심볼", "명령어", "장치", "유물", "아이템", "상점", "클리어", "아슬아슬", "점수", "저주", "도전", "반복", "히든", "기타"];
function renderAch() {
  const a = DATA.ach || {};
  const list = (CATALOG && CATALOG.achievements && CATALOG.achievements.length)
    ? CATALOG.achievements
    : Object.entries(ACH).map(([id, [e, n, d]]) => ({ id, e, n, d, cat: "기타", tier: "" }));
  const byCat = {};
  list.forEach((x) => { const c = x.cat || "기타"; (byCat[c] = byCat[c] || []).push(x); });
  const cats = Object.keys(byCat).sort((p, q) => ((ACH_CAT_ORDER.indexOf(p) + 1) || 99) - ((ACH_CAT_ORDER.indexOf(q) + 1) || 99));
  let html = "";
  cats.forEach((cat) => {
    const items = byCat[cat];
    const doneN = items.filter((x) => (a[x.id] || {}).done).length;
    html += `<h3>${cat} (${doneN}/${items.length})</h3>`;
    html += items.map((x) => {
      const d = a[x.id] || { done: false, cur: 0, max: 1 };
      const masked = x.hidden && !d.done;
      const name = masked ? "❓ 히든 업적" : x.n;
      const desc = masked ? "달성 시 공개" : x.d;
      const pct = d.max ? Math.min(100, d.cur / d.max * 100) : 0;
      return `<div class="card ${d.done ? "done" : ""}" data-d="ach_${x.id}|${x.e}|${name}|${desc}${x.reward ? " — 🎁" + x.reward : ""}">
        ${imgTag("ach_" + x.id, x.e)}<div class="body">
        <div class="name">${name} ${d.done ? "✅" : ""}${x.tier ? ` <span class="dim">${x.tier}</span>` : ""}</div><div class="desc">${desc}</div>
        <div class="mini"><div class="mini-fill" style="width:${pct}%"></div></div>
        <div class="prog">${d.cur || 0}/${d.max || 1}</div></div></div>`;
    }).join("");
  });
  $("ach").innerHTML = html;
}
// revealed=true → 실제 정보 + 사용배지, false → ??? 마스킹 카드.
// usedN: 사용/획득 횟수(0이면 배지 생략 또는 ownLabel). ownLabel: chars/machines/devices 보유·해금 표기용.
function card(cat, id, e, n, desc, revealed, usedN, ownLabel) {
  const key = cat + "_" + id;
  if (!revealed) {
    return `<div class="card locked masked" data-d="masked|${e}|??? — 아직 사용/획득하지 않았어요|미발견 — 사용하면 공개">
      <div class="ico emoji">${e || "❓"}</div><div class="body"><div class="name">❓ ???</div><div class="desc">미발견 — 사용하면 공개</div></div></div>`;
  }
  const badge = (usedN > 0)
    ? ` <span class="useN">${num(usedN)}회 사용</span>`
    : (ownLabel ? ` <span class="useN">${ownLabel}</span>` : "");
  return `<div class="card" data-d="${key}|${e}|${n}|${desc}">
    ${imgTag(key, e)}<div class="body"><div class="name">${n}${badge}</div><div class="desc">${desc}</div></div></div>`;
}
function renderDex() {
  const used = (DATA.used && typeof DATA.used === "object") ? DATA.used : {};
  const uc = new Set(DATA.chars || []), um = new Set(DATA.machines || []);
  const owDev = new Set(DATA.ownedDevices || []);
  // 게이트 잠금 맵: id → {locked, school, hint} (buildNode perkUnlock push). seen 보유분은 서버에서 locked=false.
  const pu = {};
  const puSrc = DATA.perkUnlock || {};
  [].concat(puSrc.augments || [], puSrc.relics || []).forEach((x) => { if (x && x.id) pu[x.id] = x; });
  const lvl = DATA.accountLevel || 0;
  const lvlLine = lvl ? `<div class="sum-line">🎓 졸업레벨 Lv.${lvl}${DATA.accountExp != null ? ` · 누적 ${num(DATA.accountExp)} EXP` : ""} · 🔒 잠긴 증강/유물은 전공연구로 해금돼요(해금=시작보유 아님)</div>` : "";
  // 캐릭터/머신: 해금 = 공개. usedN(0이어도 보유라벨)
  const unlockSec = (title, cat, map, ownedSet, ownWord) => {
    const entries = Object.entries(map);
    const revN = entries.filter(([id]) => ownedSet.has(id)).length;
    const cards = entries.map(([id, [e, n, desc]]) => {
      const ok = ownedSet.has(id);
      return card(cat, id, e, n, desc, ok, ok ? (used[id] || 0) : 0, ok ? ownWord : "");
    }).join("");
    return `<h3>${title} (${revN}/${entries.length})</h3><div class="grid">${cards}</div>`;
  };
  let html = lvlLine +
    unlockSec("🎭 캐릭터", "char", CHARS, uc, "해금") +
    unlockSec("🎰 슬롯머신", "mac", MACHINES, um, "해금");
  // 장치: CATALOG.devices(dev_*) 우선, 없으면 정적 DEVICES fallback
  html += deviceSec(used, owDev);
  if (CATALOG) {
    html += catSec("✨ 증강", "aug", CATALOG.augments, true, used, pu);
    html += catSec("🛡️ 유물", "rel", CATALOG.relics, false, used, pu);
    html += catSec("🎁 아이템", "item", CATALOG.items, false, used);
    html += catSec("🌑 저주", "cur", CATALOG.curses, false, used);
    html += setSec(CATALOG.sets, used);
  }
  $("dex").innerHTML = html;
}
function deviceSec(used, owDev) {
  const cdev = CATALOG && Array.isArray(CATALOG.devices) ? CATALOG.devices : null;
  if (cdev && cdev.length) {
    const revN = cdev.filter((p) => owDev.has(p.id) || (used[p.id] || 0) > 0).length;
    const cards = cdev.map((p) => {
      const ok = owDev.has(p.id) || (used[p.id] || 0) > 0;
      return card("", p.id, p.e, p.n, p.d, ok, used[p.id] || 0, ok ? "보유" : "");
    }).join("");
    return `<h3>🔧 장치 (${revN}/${cdev.length})</h3><div class="grid">${cards}</div>`;
  }
  // fallback: 정적 DEVICES (이미지 키 = dev_<key>, used/ownedDevices 키도 dev_<key> 가정)
  const entries = Object.entries(DEVICES);
  const did = (k) => "dev_" + k;
  const revN = entries.filter(([k]) => owDev.has(did(k)) || (used[did(k)] || 0) > 0).length;
  const cards = entries.map(([k, [e, n, desc]]) => {
    const id = did(k), ok = owDev.has(id) || (used[id] || 0) > 0;
    return card("", id, e, n, desc, ok, used[id] || 0, ok ? "보유" : "");
  }).join("");
  return `<h3>🔧 장치 (${revN}/${entries.length})</h3><div class="grid">${cards}</div>`;
}
const TMARK = { SILVER: "🥈", GOLD: "🥇", PRISM: "🌈" };
// 게이트 잠금 카드: 미발견(seen X) + 미해금(locked) → 🔒 전공 + 해금조건(진행도). 발견형 ??? 와 구분.
function lockedGateCard(e, school, hint) {
  const sch = school ? `${school} 전공` : "전공 연구";
  const h = (hint || "조건 미달").replace(/\|/g, "/");
  return `<div class="card locked gated" data-d="gated|${e || "🔒"}|🔒 ${sch} 잠김|${h}">
    <div class="ico emoji">🔒</div><div class="body"><div class="name">🔒 ??? <span class="dim">${sch}</span></div><div class="desc">${h}</div></div></div>`;
}
function catSec(title, cat, arr, showTier, used, pu) {
  if (!arr || !arr.length) return "";
  pu = pu || {};
  const revN = arr.filter((p) => (used[p.id] || 0) > 0).length;
  const cards = arr.map((p) => {
    const u = used[p.id] || 0;
    const g = pu[p.id];
    // 미발견 & 게이트 잠금 → 전공 해금조건 노출(스펙 6장). 발견했거나 해금된 건 기존 처리.
    if (u <= 0 && g && g.locked) return lockedGateCard(p.e, g.school, g.hint);
    return card(cat, p.id, p.e, (showTier && p.t ? (TMARK[p.t] || "") : "") + p.n, p.d, u > 0, u, "");
  }).join("");
  return `<h3>${title} (${revN}/${arr.length})</h3><div class="grid">${cards}</div>`;
}
function setSec(arr, used) {
  if (!arr || !arr.length) return "";
  const revN = arr.filter((s) => (used[s.id] || 0) > 0).length;
  const cards = arr.map((s) => {
    const u = used[s.id] || 0;
    if (u <= 0) {
      return `<div class="card locked masked" data-d="masked|🎯|??? — 아직 사용/획득하지 않았어요|미발견 — 사용하면 공개">
        <div class="ico emoji">🎯</div><div class="body"><div class="name">❓ ???</div><div class="desc">미발견 — 사용하면 공개</div></div></div>`;
    }
    return `<div class="card" data-d="${s.id || "set"}|🎯|${s.n}|${s.d}"><div class="ico emoji">🎯</div><div class="body"><div class="name">${s.n} <span class="useN">${num(u)}회 사용</span></div><div class="desc">${s.d}</div></div></div>`;
  }).join("");
  return `<h3>🎯 세트 (${revN}/${arr.length})</h3><div class="grid">${cards}</div>`;
}
// ── 🎯 도전판 (challenges) — 장치면허/캐릭/머신/표준 통합, 진행바·달성·고정목표 ──
const CHAL_KIND = { DEVICE: "🔧 장치 면허", CHAR: "🎭 캐릭터 해금", MACHINE: "🎰 머신 해금", STANDARD: "🎯 도전 과제" };
const CHAL_KIND_ORDER = ["STANDARD", "DEVICE", "CHAR", "MACHINE"];
function renderChal() {
  const list = Array.isArray(DATA.challenges) ? DATA.challenges : [];
  if (!list.length) { $("chal").innerHTML = `<div class="state">도전 데이터가 아직 없어요. 카톡에서 "잭팟" 한 판 돌려보세요.</div>`; return; }
  const pinned = DATA.pinned || "";
  const doneN = list.filter((c) => c.done).length;
  let html = `<div class="sum-line">달성 ${doneN}/${list.length}${pinned ? ` · 📌 고정목표 1개` : ""}</div>`;
  // 고정목표 카드 강조(맨 위)
  const pin = pinned ? list.find((c) => c.id === pinned) : null;
  if (pin && !pin.done) html += `<h3>📌 고정목표</h3><div class="grid">${chalCard(pin, true)}</div>`;
  const byKind = {};
  list.forEach((c) => { (byKind[c.kind] = byKind[c.kind] || []).push(c); });
  const kinds = Object.keys(byKind).sort((p, q) => ((CHAL_KIND_ORDER.indexOf(p) + 1) || 99) - ((CHAL_KIND_ORDER.indexOf(q) + 1) || 99));
  kinds.forEach((k) => {
    const items = byKind[k].slice().sort((a, b) => (a.done - b.done) || ((b.pct || 0) - (a.pct || 0)));
    const dN = items.filter((c) => c.done).length;
    html += `<h3>${CHAL_KIND[k] || k} (${dN}/${items.length})</h3><div class="grid">` +
      items.map((c) => chalCard(c, c.id === pinned)).join("") + `</div>`;
  });
  $("chal").innerHTML = html;
}
function chalCard(c, pinned) {
  const pct = c.done ? 100 : Math.max(0, Math.min(100, c.pct || 0));
  const prog = (c.cur != null && c.cur !== "" && String(c.cur) !== "0") ? String(c.cur) : "";
  const reward = c.reward ? `🎁 ${c.reward}` : "";
  return `<div class="card chal ${c.done ? "done" : ""} ${pinned ? "pinned" : ""}" data-d="chal_${c.id}|${c.e || "🎯"}|${c.n}|${reward || c.n}">
    ${imgTag("chal_" + c.id, c.e || "🎯")}<div class="body">
      <div class="name">${pinned ? "📌 " : ""}${c.n} ${c.done ? "✅" : ""}</div>
      <div class="desc">${reward || "—"}</div>
      <div class="mini"><div class="mini-fill" style="width:${pct}%"></div></div>
      <div class="prog">${c.done ? "달성" : (prog ? prog + " · " : "") + pct + "%"}</div>
    </div></div>`;
}

// ── 🏅 숙련도 (mastery) — 캐릭/머신 메달 그리드 ──
const MEDAL_E = { gold: "🥇", silver: "🥈", bronze: "🥉", none: "▫️" };
const MEDAL_LBL = { gold: "금", silver: "은", bronze: "동", none: "—" };
function renderMastery() {
  const m = DATA.mastery || {};
  const chars = Array.isArray(m.chars) ? m.chars : [];
  const machines = Array.isArray(m.machines) ? m.machines : [];
  if (!chars.length && !machines.length) { $("mastery").innerHTML = `<div class="state">숙련도 데이터가 아직 없어요.</div>`; return; }
  const earned = (arr) => arr.filter((x) => x.medal && x.medal !== "none").length;
  const sec = (title, cat, map, arr) => {
    const cards = arr.map((x) => {
      const meta = map[x.id]; const e = meta ? meta[0] : "❓"; const n = meta ? meta[1] : x.id;
      const med = x.medal || "none";
      return `<div class="card medal-${med}" data-d="${cat}_${x.id}|${e}|${n}|${MEDAL_E[med]} ${MEDAL_LBL[med]} 메달 · 최고 S${x.stage || 0}">
        ${imgTag(cat + "_" + x.id, e)}<div class="body">
          <div class="name">${n} <span class="medal-badge">${MEDAL_E[med]}</span></div>
          <div class="desc">최고 S${x.stage || 0} · ${MEDAL_LBL[med]}</div>
        </div></div>`;
    }).join("");
    return `<h3>${title} (${earned(arr)}/${arr.length} 메달)</h3><div class="grid">${cards}</div>`;
  };
  $("mastery").innerHTML =
    `<div class="sum-line">🥉 S5 동 · 🥈 S10 은 · 🥇 S15 금 (캐릭/머신별 최고 스테이지)</div>` +
    sec("🎭 캐릭터", "char", CHARS, chars) + sec("🎰 슬롯머신", "mac", MACHINES, machines);
}

// ── 📦 빌드도감 (builddex) — (캐릭×머신) 조합 cleared/총 ──
function renderBuild() {
  const rows = Array.isArray(DATA.builddex) ? DATA.builddex : [];
  const total = DATA.buildDexTotal || (Object.keys(CHARS).length * Object.keys(MACHINES).length);
  if (!rows.length) { $("build").innerHTML = `<div class="state">아직 기록한 조합이 없어요. 여러 캐릭터·머신으로 플레이해 채워보세요. (0/${total})</div>`; return; }
  const cards = rows.map((r) => {
    const cm = CHARS[r.c], mm = MACHINES[r.m];
    const ce = cm ? cm[0] : "❓", cn = cm ? cm[1] : r.c;
    const me = mm ? mm[0] : "❓", mn = mm ? mm[1] : r.m;
    return `<div class="card build" data-d="build_${r.c}_${r.m}|${ce}|${cn} + ${mn}|${ce}${cn} + ${me}${mn} — 최고 S${r.stage}">
      <div class="ico emoji combo">${ce}${me}</div><div class="body">
        <div class="name">${cn} <span class="dim">+</span> ${mn}</div>
        <div class="desc">최고 S${r.stage}</div>
      </div></div>`;
  }).join("");
  const pct = total ? Math.round(rows.length / total * 100) : 0;
  $("build").innerHTML =
    `<div class="sum-line">기록한 조합 ${rows.length}/${total} (${pct}%) · 캐릭터+머신 조합마다 최고 스테이지 기록</div>` +
    `<div class="grid">${cards}</div>` +
    renderThemeBuilds();
}

// ── 🏗️ 테마 빌드도감 (themeBuilds) — 카테고리(성장형/운명형/역전형/조합형/위험형) 그룹·완성/조건 ──
const TB_CAT_ORDER = ["성장형", "운명형", "역전형", "조합형", "위험형"];
function renderThemeBuilds() {
  const list = Array.isArray(DATA.themeBuilds) ? DATA.themeBuilds : [];
  if (!list.length) return "";
  const doneN = (DATA.themeBuildDone != null) ? DATA.themeBuildDone : list.filter((b) => b.done).length;
  const total = (DATA.themeBuildTotal != null) ? DATA.themeBuildTotal : list.length;
  const byCat = {};
  list.forEach((b) => { const c = b.category || "기타"; (byCat[c] = byCat[c] || []).push(b); });
  const cats = Object.keys(byCat).sort((p, q) => ((TB_CAT_ORDER.indexOf(p) + 1) || 99) - ((TB_CAT_ORDER.indexOf(q) + 1) || 99));
  let html = `<h3>🏗️ 테마 빌드 (${doneN}/${total})</h3>`;
  cats.forEach((cat) => {
    const items = byCat[cat].slice().sort((a, b) => (b.done - a.done));
    const dN = items.filter((b) => b.done).length;
    html += `<h3 class="sub">${_hesc(cat)} (${dN}/${items.length})</h3><div class="grid">`;
    html += items.map((b) => {
      const e = b.e || "🏗️", n = _hesc(b.name || b.id), cond = _hesc(b.cond || "");
      const done = !!b.done;
      return `<div class="card build ${done ? "done" : ""}" data-d="${b.id}|${e}|${n}|${cond}">
        <div class="ico emoji">${e}</div><div class="body">
          <div class="name">${n} ${done ? "✅" : ""}</div>
          <div class="desc">${done ? "완성 — " + cond : cond}</div>
        </div></div>`;
    }).join("") + `</div>`;
  });
  return html;
}

// ── 📈 개인기록 (records) — "라벨 값" 라인 ──
function renderRecords() {
  const recs = Array.isArray(DATA.records) ? DATA.records : [];
  if (!recs.length) { $("rec").innerHTML = `<div class="state">개인기록이 아직 없어요.</div>`; return; }
  $("rec").innerHTML = `<div class="reclist">` + recs.map((line) => {
    const s = String(line);
    // "🏆 최고점수 12,345" → 아이콘+라벨 / 값 분리는 마지막 공백 기준(값이 숫자/단위)
    const mt = s.match(/^(.*?)(\s)([0-9][0-9,]*[^ ]*)$/);
    if (mt) return `<div class="recrow"><span class="rl">${_hesc(mt[1].trim())}</span><span class="rv">${_hesc(mt[3])}</span></div>`;
    return `<div class="recrow"><span class="rl">${_hesc(s)}</span><span class="rv"></span></div>`;
  }).join("") + `</div>`;
}

function renderRank() {
  const r = DATA.rank || [];
  if (!r.length) { $("rank").innerHTML = `<div class="state">랭킹 데이터 없음</div>`; return; }
  $("rank").innerHTML = r.map((x, i) =>
    `<div class="rankrow"><span class="rk">${["🥇", "🥈", "🥉"][i] || (i + 1)}</span><span class="rn">${x.nick}</span><span class="rs">${num(x.score)}점 · S${x.stage}</span></div>`).join("");
}
const _hesc = (s) => String(s == null ? "" : s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
const HALL_SEASONS = {}; // id -> season (for modal lookup)
function renderHall() {
  const seasons = HALL && HALL.seasons ? HALL.seasons : null;
  const ids = seasons ? Object.keys(seasons) : [];
  if (!ids.length) { $("hall").innerHTML = `<div class="state">아직 명예의전당 기록이 없어요.</div>`; return; }
  // v2 먼저, 나머지는 키 내림차순
  ids.sort((a, b) => (a === "v2" ? -1 : b === "v2" ? 1 : (a < b ? 1 : a > b ? -1 : 0)));
  for (const k in HALL_SEASONS) delete HALL_SEASONS[k];
  $("hall").innerHTML = ids.map((id) => {
    const s = seasons[id] || {};
    HALL_SEASONS[id] = s;
    const top = Array.isArray(s.top) ? s.top : [];
    const isV2 = id === "v2";
    const label = s.label || id;
    const date = s.ts ? new Date(s.ts).toLocaleDateString("ko-KR") : "";
    const champ = top[0] ? `🥇 ${_hesc(top[0].nick || "?")} · ${num(top[0].score)}점` : "기록 없음";
    return `<div class="season ${isV2 ? "v2" : ""}" data-season="${_hesc(id)}">
      ${isV2 ? `<span class="season-badge">★ v2 시즌</span>` : ""}
      <div class="season-body"><div class="season-ttl">${_hesc(label)}</div>
      <div class="season-sub">${champ}${date ? ` · ${date}` : ""}</div></div>
      <span class="season-go">›</span></div>`;
  }).join("");
  document.querySelectorAll("[data-season]").forEach((c) => c.onclick = () => openHall(c.dataset.season));
}
function openHall(id) {
  const s = HALL_SEASONS[id] || {};
  const top = (Array.isArray(s.top) ? s.top : []).slice().sort((a, b) => (a.rank || 0) - (b.rank || 0));
  const label = s.label || id;
  const medal = (r) => ["🥇", "🥈", "🥉"][r - 1] || ("#" + r);
  const stageTxt = (x) => (x.stage != null && x.stage !== "" ? ` · S${_hesc(x.stage)}` : "");
  const podium = top.slice(0, 3).map((x) =>
    `<div class="podium r${x.rank || 0}"><span class="pm">${medal(x.rank || 0)}</span>
     <span class="pn">${_hesc(x.nick || "?")}</span>
     <span class="ps">${num(x.score)}점${stageTxt(x)}</span></div>`).join("") || `<div class="dim">기록 없음</div>`;
  const rest = top.slice(3).map((x) =>
    `<div class="rankrow"><span class="rk">${x.rank || ""}</span><span class="rn">${_hesc(x.nick || "?")}</span>
     <span class="rs">${num(x.score)}점${stageTxt(x)}</span></div>`).join("");
  $("modalCard").innerHTML =
    `<h2>🏆 ${_hesc(label)}</h2><div class="podium-wrap">${podium}</div>` +
    (rest ? `<div class="hall-rest">${rest}</div>` : "") +
    `<button id="hclose">닫기</button>`;
  $("modal").classList.remove("hidden");
  $("hclose").onclick = () => $("modal").classList.add("hidden");
}
function setupTabs() {
  document.querySelectorAll(".tab").forEach((b) => b.onclick = () => {
    document.querySelectorAll(".tab").forEach((x) => x.classList.remove("active"));
    b.classList.add("active");
    ["ach", "chal", "dex", "mastery", "build", "rec", "rank", "hall"].forEach((id) => $(id).classList.toggle("hidden", id !== b.dataset.tab));
  });
  document.querySelectorAll("[data-d]").forEach((c) => c.onclick = () => {
    const [key, e, name, desc] = c.dataset.d.split("|");
    $("modalCard").innerHTML =
      `<img class="big" src="img/${key}.png" onerror="this.replaceWith(Object.assign(document.createElement('div'),{className:'big emoji',textContent:'${e}'}))">
       <h2>${name}</h2><p>${desc}</p><button id="mclose">닫기</button>`;
    $("modal").classList.remove("hidden");
    $("mclose").onclick = () => $("modal").classList.add("hidden");
  });
}
