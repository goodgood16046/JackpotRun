// 잭팟런 글로벌 랭킹(일반 보드, 앱+웹 통합) — Opus 2차검수(P7-4b) [중대⑤①] — 웹 파리티 P7-4
// 랭킹 3노드 전환(slotrank/slotrank_asc/slotrank_deep, WEB_PARITY_DESIGN.md §1-A #20)으로 구 단일
// 노드 jackpotrank는 폐기됐다(Unity RankingService.cs·웹 rank.js 둘 다 더는 쓰지 않음) — 이 페이지가
// 계속 jackpotrank를 읽으면 그 시점 이후로는 새 기록이 전혀 반영되지 않는 스테일 페이지가 된다.
// slotrank(일반 보드)로 갱신 — 행 스키마({nick,score,stage,ts})가 동일해 render()/row() 로직은 무변경.
// slotrank_asc(승천)/slotrank_deep(심화) 등 별도 탭은 이 페이지 범위 밖(단일 보드 유지, 3탭은 앱 내
// RankView.cs가 담당) — slotrank 1회 읽기 → score 내림차순·ts 오름차순 정렬 → 상위 100 렌더.
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js";
import { getDatabase, ref, get } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-database.js";

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

const $ = (id) => document.getElementById(id);
const app = initializeApp(firebaseConfig);
const db = getDatabase(app);

boot();
async function boot() {
  try {
    const snap = await get(ref(db, "slotrank"));
    const val = snap.exists() ? snap.val() : null;
    render(val);
  } catch (e) {
    fail("랭킹을 불러오지 못했어요");
  }
}
function fail(msg) {
  $("loading").classList.add("hidden");
  const e = $("error");
  e.textContent = msg;
  e.classList.remove("hidden");
}

function render(val) {
  $("loading").classList.add("hidden");
  const raw = (val && typeof val === "object") ? val : {};
  const entries = Object.entries(raw).map(([pid, v]) => ({
    pid,
    nick: v && v.nick != null ? String(v.nick) : "?",
    score: (v && Number(v.score)) || 0,
    stage: (v && Number(v.stage)) || 0,
    ts: (v && Number(v.ts)) || 0,
  }));
  if (!entries.length) return fail("아직 등록된 기록이 없어요");
  entries.sort((a, b) => (b.score - a.score) || (a.ts - b.ts));
  const top = entries.slice(0, 100);
  $("list").innerHTML = top.map((x, i) => row(x, i)).join("");
  $("foot").textContent = `상위 ${top.length}명 표시 · 개인 최고 기록 기준`;
}
function row(x, i) {
  const rk = ["🥇", "🥈", "🥉"][i] || String(i + 1);
  return `<div class="rankrow"><span class="rk">${rk}</span><span class="rn">${_hesc(x.nick)}</span><span class="rs">${num(x.score)}점 · S${x.stage}</span><span class="rd">${dateStr(x.ts)}</span></div>`;
}
const num = (n) => (n || 0).toLocaleString("ko-KR");
function dateStr(ts) {
  if (!ts) return "";
  const d = new Date(ts);
  const p = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}.${p(d.getMonth() + 1)}.${p(d.getDate())}`;
}
const _hesc = (s) => String(s == null ? "" : s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
