// 잭팟런 시작 선택 — 재디자인(2026-06-29).
// 데이터 계약은 그대로: jackpotdex/<t> 에서 해금목록을 읽고, jackpotcmd/<w> 에
// {char, machine, device, cid, ts} 를 써서 다음 런을 예약한다. 카탈로그(meta.js)는 웹 전용 보강.
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js";
import { getDatabase, ref, get, set } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-database.js";
import { CHARS, MACHINES, DEVICES, DIFF_LABEL, DIFF_COLOR, evaluate, recommend, unlockOrder } from "./meta.js";

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

const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);
const T = params.get("t"), W = params.get("w"), DEMO = params.get("demo") === "1";
const app = DEMO ? null : initializeApp(firebaseConfig);
const db = DEMO ? null : getDatabase(app);

const CHAR_ORDER = ["novice","scholar","gambler","farmer","parttime","jeweler","honor","cultist","crowncol","minimalist","lucky","highroller","monk","alchemist","daredevil","prodigy"];
const MAC_ORDER  = ["basic","cherry","library","gem","magnet","skull","crown","flame","bomb","star","clover","casino","garden","wildmac","vault","rainbow"];
const DEV_ORDER  = ["dev_flame","dev_seal","dev_safe","dev_overheat","dev_subreel","dev_coin","dev_reroll","dev_pin","dev_copy","dev_swap","dev_oracle","dev_bell"];
const CHIP_VOCAB = ["초보추천","안정형","고점형","위험","상급자용","체리","책","보석","해골","왕관","코인","콤보","세트","한방","재굴림","증강","도전","성장형","속전속결","랜덤성↑","패시브","능동","만능"];
const GRADE_RANK = { S: 0, A: 1, B: 2, C: 3 };
const TAG_CLASS = { "위험":"hot", "상급자용":"hot", "초보추천":"good", "안정형":"good", "고점형":"high", "한방":"high" };

const state = {
  nick: "", unlockedChars: new Set(), unlockedMacs: new Set(), ownedDevs: new Set(),
  unlock: null,   // 배치3b: 카톡이 push한 해금조건 힌트 { chars:{id:{t,pct,done}}, machines, devices }
  tab: "char", filter: "전체", sort: "reco",
  selChar: null, selMac: null, selDev: "",   // selDev "" = 장치 없이(기본)
  advanced: { char: false, mac: false },
};

const esc = (s) => String(s == null ? "" : s).replace(/[&<>"']/g, (c) => ({ "&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;" }[c]));
function cid() {
  let c = localStorage.getItem("jpk_cid");
  if (!c) { c = Math.random().toString(36).slice(2) + Date.now().toString(36); localStorage.setItem("jpk_cid", c); }
  return c;
}
function imgTag(key, emoji) {
  return `<img class="jc-ico" src="/jackpotdex/img/${key}.png" alt="" loading="lazy"
    onerror="this.replaceWith(Object.assign(document.createElement('div'),{className:'jc-ico emoji',textContent:'${emoji}'}))">`;
}

boot();
async function boot() {
  if (DEMO) return render(demoData());
  if (!T) return fail("토큰이 없습니다. 카톡에서 \"잭팟선택\" 으로 링크를 받아주세요.");
  try {
    const snap = await get(ref(db, "jackpotdex/" + T));
    if (!snap.exists()) return fail("아직 기록이 없어요. 카톡에서 \"잭팟\" 한 판 돌리고 다시 열어주세요.");
    render(snap.val());
  } catch (e) { fail("불러오기 실패: " + e.message); }
}
function fail(msg) { $("loading").classList.add("hidden"); const e = $("error"); e.textContent = msg; e.classList.remove("hidden"); }
function demoData() {
  return { nick: "데모플레이어",
    chars: ["novice","scholar","gambler","farmer","parttime","jeweler","cultist","lucky","prodigy"],
    machines: ["basic","cherry","library","gem","magnet","skull","star","clover"],
    ownedDevices: ["dev_safe","dev_reroll","dev_seal","dev_coin"],
    // 배치3b: 카톡 push 해금힌트 데모(잠긴 카드 일부에 진행바 표시 확인용)
    unlock: {
      chars: {
        honor:      { t: "최고점수 8,000 (6,400/8,000) · 보스 5회 (3/5)", pct: 72, done: false },
        minimalist: { t: "최고도달 S12 (S9/S12) · 무유물 클리어 1회 (0/1)", pct: 45, done: false },
        monk:       { t: "최고도달 S6 (S6/S6) · 아슬아슬 3회 (2/3)", pct: 83, done: false },
        daredevil:  { t: "최고점수 15,000 (6,400/15,000)", pct: 42, done: false },
      },
      machines: {
        crown:   { t: "👑왕관 누적 40 (28/40) · 잭팟 3회 (2/3)", pct: 68, done: false },
        flame:   { t: "최고점수 15,000 (6,400/15,000) · 최고도달 S10 (S9/S10)", pct: 65, done: false },
        bomb:    { t: "보스 5회 (3/5) · 최고도달 S10 (S9/S10)", pct: 75, done: false },
        rainbow: { t: "최고점수 25,000 (6,400/25,000) · 잭팟 10회 (2/10)", pct: 23, done: false },
      },
      devices: {
        dev_flame:    { t: "최고점수 50,000 (6,400/50,000) · 최고도달 S20 (S9/S20)", pct: 28, done: false },
        dev_overheat: { t: "막판 클리어 10회 (4/10) · 최고점수 20,000 (6,400/20,000)", pct: 36, done: false },
        dev_oracle:   { t: "최고도달 S15 (S9/S15) · 기도 클리어 3회 (1/3)", pct: 47, done: false },
      },
    },
  };
}

function render(data) {
  $("loading").classList.add("hidden");
  $("picker").classList.remove("hidden");
  state.nick = data.nick || "?";
  state.unlockedChars = new Set(data.chars || []);
  state.unlockedMacs = new Set(data.machines || []);
  state.ownedDevs = new Set(data.ownedDevices || []);
  state.unlock = (data.unlock && typeof data.unlock === "object") ? data.unlock : null;
  $("who").innerHTML = `@${esc(state.nick)} <small>— 캐릭터 + 머신 + 장치를 골라 시작을 예약하세요${DEMO ? " (데모 미리보기)" : ""}</small>`;

  // 탭
  document.querySelectorAll(".tab").forEach((t) => t.onclick = () => setTab(t.dataset.tab));
  // 추천 조합
  document.querySelectorAll(".reco").forEach((b) => b.onclick = () => applyReco(b.dataset.reco));
  // 정렬
  $("sort").onchange = () => { state.sort = $("sort").value; renderSection(); };
  // 요약 토글(모바일)
  $("sum-toggle").onclick = () => $("summary").classList.toggle("open");
  $("go").onclick = submit;

  setTab("char");
  updateSummary();
}

// ── 탭 ──────────────────────────────────────────────
function setTab(tab) {
  state.tab = tab; state.filter = "전체";
  document.querySelectorAll(".tab").forEach((t) => t.classList.toggle("active", t.dataset.tab === tab));
  $("sec-title").textContent = tab === "char" ? "🎭 캐릭터" : tab === "mac" ? "🎰 슬롯머신" : "🔧 장치";
  $("sec-note").innerHTML = tab === "dev"
    ? `장치는 런·업적으로 영구 획득해요. 가진 장치만 장착할 수 있고, 나머지는 해금 조건을 흐리게 보여줍니다. 전체 종류는 <a href="/jackpotdex/?t=${esc(T||"")}">도감</a>에서.`
    : (tab === "char" ? "캐릭터가 이번 런의 플레이 스타일을 정합니다. 잠긴 카드는 해금 조건이 함께 표시돼요."
                      : "슬롯머신은 심볼 등장 확률과 점수 보정을 정합니다. 캐릭터 테마와 맞추면 시너지가 올라가요.");
  renderChips();
  renderSection();
}
function tabIds() { return state.tab === "char" ? CHAR_ORDER : state.tab === "mac" ? MAC_ORDER : DEV_ORDER; }
function metaOf(tab, id) { return tab === "char" ? CHARS[id] : tab === "mac" ? MACHINES[id] : DEVICES[id]; }
function isUnlocked(tab, id) {
  return tab === "char" ? state.unlockedChars.has(id) : tab === "mac" ? state.unlockedMacs.has(id) : state.ownedDevs.has(id);
}

// ── 필터 칩 ──────────────────────────────────────────
function renderChips() {
  const present = new Set();
  tabIds().forEach((id) => (metaOf(state.tab, id)?.tags || []).forEach((t) => present.add(t)));
  const chips = ["전체", ...CHIP_VOCAB.filter((t) => present.has(t))];
  $("chips").innerHTML = chips.map((c) =>
    `<span class="chip${state.filter === c ? " on" : ""}" data-c="${esc(c)}">${esc(c)}</span>`).join("");
  $("chips").querySelectorAll(".chip").forEach((el) => el.onclick = () => { state.filter = el.dataset.c; renderSection(); });
}

// ── 정렬 ────────────────────────────────────────────
function sortIds(ids) {
  const tab = state.tab;
  const unlocked = (id) => isUnlocked(tab, id) ? 0 : 1;
  const metric = (id) => {
    const m = metaOf(tab, id); if (!m) return 0;
    if (state.sort === "diff") return (m.diff || 2);
    if (state.sort === "ceiling") return -(m.ceiling || 0);
    if (state.sort === "recent") return -unlockOrder(m);
    // reco
    if (tab === "char") return ((m.tags || []).includes("초보추천") ? -3 : 0) + (m.diff || 2);
    // mac/dev — 선택한 캐릭터와의 시너지 우선
    if (state.selChar) {
      const ev = tab === "mac" ? evaluate(state.selChar, id, state.selDev)
                               : evaluate(state.selChar, state.selMac || firstUnlockedMac(), id);
      if (ev) return GRADE_RANK[ev.grade] * 4 - (m.ceiling || 0) * 0.3;
    }
    return (m.diff || 2);
  };
  return [...ids].sort((a, b) => (unlocked(a) - unlocked(b)) || (metric(a) - metric(b)) || (tabIds().indexOf(a) - tabIds().indexOf(b)));
}
function firstUnlockedMac() { return MAC_ORDER.find((id) => state.unlockedMacs.has(id)) || "basic"; }

// ── 섹션 렌더 ────────────────────────────────────────
function renderSection() {
  const tab = state.tab;
  let ids = tabIds().filter((id) => state.filter === "전체" || (metaOf(tab, id)?.tags || []).includes(state.filter));
  ids = sortIds(ids);
  const cards = [];
  if (tab === "dev") cards.push(noneDevCard());
  ids.forEach((id) => cards.push(cardHtml(tab, id)));
  $("grid").innerHTML = cards.join("");
  const total = tabIds().length, unl = tabIds().filter((id) => isUnlocked(tab, id)).length;
  $("sec-cnt").textContent = `해금 ${unl}/${total}${state.filter !== "전체" ? ` · 필터 “${state.filter}”` : ""}`;
  $("grid").querySelectorAll("[data-id]").forEach((el) => {
    if (el.classList.contains("locked")) return;
    el.onclick = () => pick(tab, el.dataset.id);
  });
  renderChips();
}

function noneDevCard() {
  const sel = state.selDev === "";
  return `<div class="jcard none${sel ? " sel" : ""}" data-id="" style="--rc:#69718a">
    ${sel ? '<span class="jc-check">선택됨 ✓</span>' : ""}
    <div class="jc-top"><div class="jc-ico emoji">🚫</div>
      <div class="jc-id"><div class="jc-name">장치 없이</div><div class="jc-role">기본 · 장치 미장착</div></div></div>
    <div class="jc-eff">이번 런은 장치를 장착하지 않습니다. 순수 캐릭터·머신 조합.</div></div>`;
}

function cardHtml(tab, id) {
  const m = metaOf(tab, id); if (!m) return "";
  const unlocked = isUnlocked(tab, id);
  const selected = (tab === "char" && state.selChar === id) || (tab === "mac" && state.selMac === id) || (tab === "dev" && state.selDev === id);
  const imgKey = tab === "char" ? "char_" + id : tab === "mac" ? "mac_" + id : id;
  const stripe = tab === "dev"
    ? (m.kind === "패시브" ? "#34d3c0" : "#5b8cff")
    : (DIFF_COLOR[m.diff] || "#2a3048");
  const tagsHtml = (m.tags || []).map((t) => `<span class="tg ${TAG_CLASS[t] || ""}">${esc(t)}</span>`).join("");

  let badge, bodyExtra;
  if (tab === "dev") {
    badge = `<span class="badge b-diff" style="background:${m.kind === "패시브" ? "#34d3c0" : "#5b8cff"}">${esc(m.kind)}</span>`;
    bodyExtra =
      `<div class="jc-pc">
         ${m.cmd ? `<div class="li"><b>명령</b><span>.${esc(m.cmd)}</span></div>` : `<div class="li"><b>유형</b><span>패시브(상시 자동)</span></div>`}
         <div class="li"><b>쿨다운</b><span>${esc(m.cool)}</span></div>
       </div>
       <div class="jc-foot">추천: <b>${esc(m.when)}</b></div>`;
  } else {
    badge = `<span class="badge b-diff" style="background:${DIFF_COLOR[m.diff]}">${DIFF_LABEL[m.diff]}</span>`;
    const pros = (m.pros || []).slice(0, 2).map((p) => `<div class="li pro"><b>＋</b><span>${esc(p)}</span></div>`).join("");
    const cons = (m.cons || []).slice(0, 1).map((p) => `<div class="li con"><b>－</b><span>${esc(p)}</span></div>`).join("");
    bodyExtra = `<div class="jc-pc">${pros}${cons}</div><div class="jc-foot">추천 빌드: <b>${esc(m.build)}</b></div>`;
  }

  const corner = !unlocked ? `<span class="jc-lock">🔒 잠김</span>`
    : selected ? `<span class="jc-check">선택됨 ✓</span>` : "";
  const lockHint = !unlocked ? unlockHint(tab, id, m) : "";

  return `<div class="jcard${selected ? " sel" : ""}${unlocked ? "" : " locked"}" data-id="${esc(id)}" style="--rc:${stripe}">
    ${corner}
    <div class="jc-top">${imgTag(imgKey, m.e)}
      <div class="jc-id"><div class="jc-name">${esc(m.n)} ${badge}</div><div class="jc-role">${esc(m.role)}</div></div></div>
    <div class="jc-eff">${esc(m.eff)}</div>
    <div class="jc-tags">${tagsHtml}</div>
    ${unlocked ? bodyExtra : lockHint}
  </div>`;
}

// 잠긴 카드 해금조건 — 카톡이 push한 unlock 힌트(t + pct 진행바) 우선, 없으면 meta.js 폴백.
//  unlock 키: char→unlock.chars[id] · mac→unlock.machines[id] · dev→unlock.devices[id](dev_* 그대로).
function unlockHint(tab, id, m) {
  const u = state.unlock;
  const bucket = u && (tab === "char" ? u.chars : tab === "mac" ? u.machines : u.devices);
  const e = bucket && bucket[id];
  if (e && e.t) {
    const pct = Math.max(0, Math.min(100, e.pct || 0));
    return `<div class="jc-unlock"><b>해금:</b> ${esc(e.t)}</div>
      <div class="jc-ubar"><div class="jc-ufill" style="width:${pct}%"></div></div>
      <div class="jc-upct">${pct}%</div>`;
  }
  // 폴백 — push 데이터 없음(데모/구버전): meta.js 하드코딩 조건.
  return `<div class="jc-unlock"><b>해금:</b> ${esc((m && m.unlock) || "조건 미정")}</div>`;
}

// ── 선택 ────────────────────────────────────────────
function pick(tab, id) {
  if (tab === "char") { state.selChar = id; }
  else if (tab === "mac") { state.selMac = id; }
  else { state.selDev = id; }
  // 탭 픽 라벨
  refreshTabLabels();
  renderSection();
  updateSummary();
  // 첫 선택 시 다음 단계로 자동 진행
  if (tab === "char" && !state.advanced.char) { state.advanced.char = true; setTab("mac"); }
  else if (tab === "mac" && !state.advanced.mac) { state.advanced.mac = true; setTab("dev"); }
}
function refreshTabLabels() {
  const c = state.selChar ? CHARS[state.selChar] : null;
  const m = state.selMac ? MACHINES[state.selMac] : null;
  const d = state.selDev ? DEVICES[state.selDev] : null;
  $("tp-char").textContent = c ? `${c.e} ${c.n}` : "선택 전";
  $("tp-mac").textContent = m ? `${m.e} ${m.n}` : "선택 전";
  $("tp-dev").textContent = d ? `${d.e} ${d.n}` : "장치 없이";
  document.querySelector('.tab[data-tab="char"]').classList.toggle("done", !!c);
  document.querySelector('.tab[data-tab="mac"]').classList.toggle("done", !!m);
  document.querySelector('.tab[data-tab="dev"]').classList.add("done"); // 장치는 기본(없이)도 완료로 간주
}

// ── 추천 조합 ────────────────────────────────────────
function applyReco(kind) {
  const uc = [...state.unlockedChars], um = [...state.unlockedMacs], ud = [...state.ownedDevs];
  let combo;
  if (kind === "random") {
    const pickRand = (arr) => arr[Math.floor(Math.random() * arr.length)];
    combo = { char: pickRand(uc) || "novice", machine: pickRand(um) || "basic",
              device: ud.length && Math.random() < 0.6 ? pickRand(ud) : "" };
  } else {
    combo = recommend(kind, uc, um, ud);
  }
  if (!combo) return;
  state.selChar = combo.char; state.selMac = combo.machine; state.selDev = combo.device || "";
  state.advanced.char = state.advanced.mac = true;
  refreshTabLabels();
  renderSection();
  updateSummary();
  $("summary").classList.add("open");   // 모바일에서 상세 펼치기
  $("msg").textContent = ""; $("msg").className = "";
  flashSummary();
}
function flashSummary() {
  const el = $("summary"); el.animate?.(
    [{ boxShadow: "0 0 0 2px #ffd23f00" }, { boxShadow: "0 0 0 2px #ffd23faa" }, { boxShadow: "0 0 0 2px #ffd23f00" }],
    { duration: 700 });
}

// ── 요약 패널 ────────────────────────────────────────
function updateSummary() {
  const c = state.selChar ? CHARS[state.selChar] : null;
  const m = state.selMac ? MACHINES[state.selMac] : null;
  const d = state.selDev ? DEVICES[state.selDev] : null;
  const comboEl = $("sum-combo");
  const parts = [];
  parts.push(c ? `${c.e}${c.n}` : `<span class="ph">캐릭터?</span>`);
  parts.push(m ? `${m.e}${m.n}` : `<span class="ph">머신?</span>`);
  parts.push(d ? `${d.e}${d.n}` : `🚫장치없이`);
  comboEl.innerHTML = parts.join(' <span class="dim">+</span> ');

  const ready = !!(c && m);
  $("go").disabled = !ready;
  const grEl = $("sum-grade"), detail = $("sum-detail"), bl = $("sum-bl");
  if (!ready) {
    grEl.style.display = "none"; bl.textContent = "";
    detail.innerHTML = `<div class="sd-blurb">캐릭터와 슬롯머신을 고르면 조합 시너지·점수 고점·안정성·난이도를 분석해 드려요.</div>`;
    return;
  }
  const ev = evaluate(state.selChar, state.selMac, state.selDev);
  grEl.style.display = ""; grEl.style.color = ev.gradeColor;
  grEl.textContent = `시너지 ${ev.grade}`;
  bl.textContent = ev.buildTokens.join(" / ");

  const prosLi = ev.pros.map((p) => `<li>${esc(p)}</li>`).join("");
  const consLi = ev.cons.map((p) => `<li>${esc(p)}</li>`).join("");
  const builds = ev.buildTokens.map((b) => `<span class="bt">${esc(b)}</span>`).join("");
  detail.innerHTML = `
    <div class="sd-meters">
      <div class="meter"><div class="mk">점수 고점</div><div class="mv"><span class="stars">${ev.ceilingStars}</span></div></div>
      <div class="meter"><div class="mk">안정성</div><div class="mv"><span class="stars">${ev.stabilityStars}</span></div></div>
      <div class="meter diff"><div class="mk">난이도 · ${ev.diffLabel}</div><div class="mv"><span class="stars">${ev.difficultyStars}</span></div></div>
    </div>
    <div class="sd-blurb"><b>시너지 ${ev.grade}.</b> ${esc(ev.blurb)}</div>
    <div class="sd-cols">
      <div class="sd-box pro"><h4>장점</h4><ul>${prosLi}</ul></div>
      <div class="sd-box con"><h4>주의</h4><ul>${consLi || "<li>특별한 약점 없음</li>"}</ul></div>
    </div>
    <div class="sd-builds">${builds}</div>`;
}

// ── 예약 전송 ────────────────────────────────────────
async function submit() {
  if (!(state.selChar && state.selMac)) return;
  const c = CHARS[state.selChar], m = MACHINES[state.selMac], d = state.selDev ? DEVICES[state.selDev] : null;
  const devTxt = d ? ` + ${d.e}${d.n}` : " + 🚫장치없이";
  const combo = `${c.e}${c.n} + ${m.e}${m.n}${devTxt}`;
  if (DEMO) { msgOk(`✅ <b>${combo}</b> — 데모 모드라 실제 예약은 되지 않아요.`); return; }
  if (!W) { msgBad("이 선택 링크는 만료됐어요(쓰기 토큰 없음). 카톡에서 \"잭팟선택\" 으로 새 링크를 받아주세요."); return; }
  $("go").disabled = true; msg("예약 중…", "");
  try {
    await set(ref(db, "jackpotcmd/" + W), { char: state.selChar, machine: state.selMac, device: state.selDev || "", cid: cid(), ts: Date.now() });
    msgOk(`✅ <b>${combo}</b> 예약 완료!<br>카톡에서 "잭팟" 입력하면 이 조합으로 시작돼요. (5분 내)`);
  } catch (e) { msgBad("실패: " + e.message); $("go").disabled = false; }
}
function msg(html, cls) { const el = $("msg"); el.innerHTML = html; el.className = cls || ""; }
function msgOk(html) { msg(html, "ok"); $("summary").classList.add("open"); }
function msgBad(html) { msg(html, "bad"); $("summary").classList.add("open"); }
