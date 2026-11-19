// 잭팟런 글로벌 랭킹 페이지 — 두 구역(2026-11-19).
//  ① 🌍 통합 랭킹: 모카월드 서버의 일반 보드(모든 방이 함께, 로그인 없이 읽기). 실패하면 이 구역에만 오류 표시.
//  ② 📜 예전 기록: Firebase slotrank(2026-11-19 이전 구글·게스트·앱 기록) 읽기 전용 — 예전 로직 그대로.
//  승천(slotrank_asc)/심화모드(slotrank_deep) 보드는 이 페이지 범위 밖(앱·게임 안 랭킹 화면이 담당).
// Firebase 는 예전 기록 구역에서만 늦게 불러온다(통합 랭킹은 Firebase 와 무관).

// 독립 프로젝트 jackpotrun-web (콘솔 표시명 "JackpotRun" = 프로젝트 ID jackpotrun-web).
// jackpotdex/app.js 와 동일한 설정(그대로 복사) — 앱·웹·봇이 모두 이 프로젝트 RTDB를 공유한다.
const firebaseConfig = {
  apiKey: "AIzaSyB2mxXO01_Rbaz3S_gyVYvPlnnGcYsLGjM",
  authDomain: "jackpotrun-web.firebaseapp.com",
  databaseURL: "https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app",
  projectId: "jackpotrun-web",
  storageBucket: "jackpotrun-web.firebasestorage.app",
  messagingSenderId: "52817920989",
  appId: "1:52817920989:web:02b32f4499f4dd4f4d6c34",
};

const MOKA_RANK_URL = "https://mokaworld.app/api/v1/web-account/jackpot-ranking?mode=normal&limit=100";

const $ = (id) => document.getElementById(id);

boot();
function boot() {
  bootLegacy();   // 따로 돌린다(자기 "불러오는 중…" 문구를 가짐)
  bootMoka().finally(() => $("loading").classList.add("hidden"));
}

// ① 모카월드 통합 랭킹(무인증 GET, 커스텀 헤더 없음)
async function bootMoka() {
  try {
    const c = new AbortController(); const t = setTimeout(() => c.abort(), 15000);
    let r, j;
    try { r = await fetch(MOKA_RANK_URL, { cache: "no-store", signal: c.signal }); j = await r.json(); } finally { clearTimeout(t); }
    if (!r.ok || !j || !j.ok) throw new Error("bad response");
    const rows = ((j.data && j.data.rows) || []).map((v) => ({
      nick: v.nick != null ? String(v.nick) : "?",
      room: v.roomName ? String(v.roomName) : "",
      score: Number(v.score) || 0,
      stage: Number(v.stage) || 0,
      ts: Number(v.ts) || 0,
    }));
    if (!rows.length) { $("list").innerHTML = `<div class="state">아직 등록된 기록이 없어요</div>`; return; }
    $("list").innerHTML = rows.map((x, i) => row(x, i)).join("");
    $("foot").textContent = `통합 랭킹 상위 ${rows.length}명 표시 · 개인 최고 기록 기준`;
  } catch (e) {
    $("list").innerHTML = `<div class="state">통합 랭킹을 불러오지 못했어요</div>`;
  }
}

// ② 예전 기록(Firebase slotrank, 읽기 전용)
async function bootLegacy() {
  $("legacy").innerHTML = `<div class="state">불러오는 중…</div>`;
  try {
    const [A, D] = await Promise.all([
      import("https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js"),
      import("https://www.gstatic.com/firebasejs/10.12.2/firebase-database.js"),
    ]);
    const db = D.getDatabase(A.initializeApp(firebaseConfig));
    const snap = await D.get(D.ref(db, "slotrank"));
    const val = snap.exists() ? snap.val() : null;
    const raw = (val && typeof val === "object") ? val : {};
    const entries = Object.entries(raw).map(([pid, v]) => ({
      pid,
      nick: v && v.nick != null ? String(v.nick) : "?",
      room: "",
      score: (v && Number(v.score)) || 0,
      stage: (v && Number(v.stage)) || 0,
      ts: (v && Number(v.ts)) || 0,
    }));
    if (!entries.length) { $("legacy").innerHTML = `<div class="state">예전 기록이 없어요</div>`; return; }
    entries.sort((a, b) => (b.score - a.score) || (a.ts - b.ts));
    $("legacy").innerHTML = entries.slice(0, 100).map((x, i) => row(x, i)).join("");
  } catch (e) {
    $("legacy").innerHTML = `<div class="state">예전 기록을 불러오지 못했어요</div>`;
  }
}

function row(x, i) {
  const rk = ["🥇", "🥈", "🥉"][i] || String(i + 1);
  const room = x.room ? `<span class="rr">${_hesc(x.room)}</span>` : "";
  return `<div class="rankrow"><span class="rk">${rk}</span><span class="rn">${_hesc(x.nick)}${room}</span><span class="rs">${num(x.score)}점 · S${x.stage}</span><span class="rd">${dateStr(x.ts)}</span></div>`;
}
const num = (n) => (n || 0).toLocaleString("ko-KR");
function dateStr(ts) {
  if (!ts) return "";
  const d = new Date(ts);
  const p = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}.${p(d.getMonth() + 1)}.${p(d.getDate())}`;
}
const _hesc = (s) => String(s == null ? "" : s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
