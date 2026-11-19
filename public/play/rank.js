// ── 잭팟 슬롯 (웹 단독판) — 랭킹 모듈 ────────────────────────────────
// 2026-11-19: 모카월드 통합 랭킹(서버)으로 전환. 기록은 모카월드(카카오)로 로그인한 계정만 올릴 수 있고,
// 이름 · 방 이름은 서버가 붙인다(moka.js). 게스트는 그대로 플레이할 수 있지만 랭킹 등록은 로그인 뒤에.
// 예전 Firebase 보드(slotrank 일반 보드)는 읽기 전용 「예전 기록」으로만 남긴다(승천·심화모드 보드는 웹이 읽지 않음)
// (2026-11-19 이전 구글·게스트 기록과 앱 기록). 웹은 Firebase 에 더 이상 쓰지 않는다.
// 일반 / 승천(asc) / 심화모드(deep) 점수는 서로 다른 보드로 분리 유지.
import { identity, submit, top } from "./moka.js";

// 독립 프로젝트 jackpotrun-web (모카봇 mokabot-8ed4d 에서 분리, 2026-08-07). 예전 기록 읽기 전용.
const firebaseConfig = {
  apiKey: "AIzaSyB2mxXO01_Rbaz3S_gyVYvPlnnGcYsLGjM",
  authDomain: "jackpotrun-web.firebaseapp.com",
  databaseURL: "https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app",
  projectId: "jackpotrun-web",
  storageBucket: "jackpotrun-web.firebasestorage.app",
  messagingSenderId: "52817920989",
  appId: "1:52817920989:web:02b32f4499f4dd4f4d6c34",
};
// Firebase 는 예전 기록을 열 때만 늦게 불러온다(gstatic 이 막혀도 통합 랭킹 · 등록은 동작).
let _fbP = null;
const fb = () => (_fbP ??= Promise.all([
  import("https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js"),
  import("https://www.gstatic.com/firebasejs/10.12.2/firebase-database.js"),
]));

// 로그인 시 ui 가 setAuthUid(uid) 호출. uid = 모카월드 cid.
let _authUid = null;
export function setAuthUid(uid) { _authUid = uid || null; }
export function isAuthed() { return !!_authUid; }

export function myCid() {
  let c = localStorage.getItem("slotweb_cid");
  if (!c) { c = Math.random().toString(36).slice(2) + Date.now().toString(36); localStorage.setItem("slotweb_cid", c); }
  return c;
}
// 모카월드 cid = 랭킹 행의 cid 라서 "나" 표시가 맞는다. 게스트는 기존 기기 id.
export function myKey() { return _authUid ? _authUid : myCid(); }
// 로그인했으면 모카월드 이름, 게스트는 "" (랭킹 등록 불가 → ui 가 로그인 안내를 띄운다).
export function savedNick() { return _authUid ? (identity()?.nick || "") : ""; }
export function setNick(n) { localStorage.setItem("slotweb_nick", (n || "").slice(0, 16)); }

/** 최고기록 등록(더 높을 때만 갱신). entry={score,stage,asc}. 반환: {ok, rankUpdated, best} | {ok:false, needLogin, err} */
export function submitScore(entry) { return submit("normal", entry); }
/** 승천(심화 학기) 랭킹. */
export function submitAscScore(entry) { return submit("asc", entry); }
/** 심화모드(심볼 덱빌딩) 랭킹. */
export function submitDeepScore(entry) { return submit("deep", entry); }

/** 통합 랭킹 상위 n (내림차순). 반환 배열 또는 null(오류). */
export function topScores(n = 50) { return top("normal", n); }
export function topAscScores(n = 50) { return top("asc", n); }
export function topDeepScores(n = 50) { return top("deep", n); }

/** 예전 게스트 기록 이전은 더 하지 않는다 — 로그인 뒤 ui 가 로컬 최고기록을 새로 등록한다. */
export async function migrateGuestToUser() { return { moved: false }; }

// ── 예전 기록(Firebase, 읽기 전용) ──
async function legacyTop(node, n) {
  try {
    const [A, D] = await fb();
    const db = D.getDatabase(A.getApps().length ? A.getApp() : A.initializeApp(firebaseConfig));
    const snap = await D.get(D.query(D.ref(db, node), D.orderByChild("score"), D.limitToLast(n)));
    if (!snap.exists()) return [];
    const out = [];
    snap.forEach((ch) => {
      const v = ch.val();
      out.push({ cid: ch.key, nick: String(v?.nick ?? ""), score: Number(v?.score) || 0, stage: Number(v?.stage) || 0, asc: Number(v?.asc) || 0, ts: Number(v?.ts) || 0 });
    });
    out.sort((a, b) => b.score - a.score);
    return out;
  } catch (e) { _fbP = null; return null; }
}
export function legacyTopScores(n = 50) { return legacyTop("slotrank", n); }
