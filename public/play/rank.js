// ── 잭팟 슬롯 (웹 단독판) — 랭킹 모듈 ────────────────────────────────
// 기존 잭팟런(jackpot* 노드)과 완전 격리된 별도 노드 `slotrank` 사용.
// 디바이스당 1행(slotrank/<cid>) = 최고 기록(더 높을 때만 갱신). 무인증·캐주얼 보드라
// 클라 점수라 위변조 가능성은 있음(설계상 수용). 게임 로직은 Firebase 없이도 동작.
import { initializeApp, getApps, getApp } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js";
import { getDatabase, ref, get, set, query, orderByChild, limitToLast } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-database.js";

// 독립 프로젝트 jackpotrun-web (모카봇 mokabot-8ed4d 에서 분리, 2026-08-07).
const firebaseConfig = {
  apiKey: "AIzaSyB2mxXO01_Rbaz3S_gyVYvPlnnGcYsLGjM",
  authDomain: "jackpotrun-web.firebaseapp.com",
  databaseURL: "https://jackpotrun-web-default-rtdb.asia-southeast1.firebasedatabase.app",
  projectId: "jackpotrun-web",
  storageBucket: "jackpotrun-web.firebasestorage.app",
  messagingSenderId: "52817920989",
  appId: "1:52817920989:web:02b32f4499f4dd4f4d6c34",
};
// auth.js 와 앱 인스턴스 공유(중복 initializeApp 방지).
function fbApp() { return getApps().length ? getApp() : initializeApp(firebaseConfig); }

let _db = null;
function db() { if (!_db) { try { _db = getDatabase(fbApp()); } catch (e) { _db = null; } } return _db; }

// 로그인 시 ui 가 setAuthUid(uid) 호출 → 랭킹 기록 키를 계정(u_<uid>) 기준으로. 게스트는 cid.
let _authUid = null;
export function setAuthUid(uid) { _authUid = uid || null; }
export function isAuthed() { return !!_authUid; }

export function myCid() {
  let c = localStorage.getItem("slotweb_cid");
  if (!c) { c = Math.random().toString(36).slice(2) + Date.now().toString(36); localStorage.setItem("slotweb_cid", c); }
  return c;
}
// 랭킹 기록 키: 로그인=u_<uid>(본인만 수정, DB 규칙 강제) / 게스트=cid(무인증, 기존과 동일).
export function myKey() { return _authUid ? ("u_" + _authUid) : myCid(); }
export function savedNick() { return localStorage.getItem("slotweb_nick") || ""; }
export function setNick(n) { localStorage.setItem("slotweb_nick", (n || "").slice(0, 16)); }

/** 최고기록 등록(더 높을 때만 갱신). entry={nick,score,stage}. 반환: {ok, rankUpdated} */
export async function submitScore(entry) {
  const d = db(); if (!d) return { ok: false };
  const nick = (entry.nick || "익명").trim().slice(0, 16) || "익명";
  setNick(nick);
  try {
    const node = ref(d, "slotrank/" + myKey());
    const cur = await get(node);
    const prev = cur.exists() ? cur.val() : null;
    if (prev && (prev.score || 0) >= entry.score) {
      // 점수는 유지하되 닉만 바뀌었으면 갱신
      if (prev.nick !== nick) await set(node, { ...prev, nick });
      return { ok: true, rankUpdated: false, best: prev.score };
    }
    await set(node, { nick, score: Math.floor(entry.score), stage: entry.stage || 0, ts: Date.now() });
    return { ok: true, rankUpdated: true, best: Math.floor(entry.score) };
  } catch (e) { return { ok: false, err: e.message }; }
}

/** 심화 학기(승천) 랭킹 — 별도 노드 slotrank_asc. entry={nick,score,stage,asc}. */
export async function submitAscScore(entry) {
  const d = db(); if (!d) return { ok: false };
  const nick = (entry.nick || "익명").trim().slice(0, 16) || "익명";
  try {
    const node = ref(d, "slotrank_asc/" + myKey());
    const cur = await get(node);
    const prev = cur.exists() ? cur.val() : null;
    if (prev && (prev.score || 0) >= entry.score) {
      if (prev.nick !== nick) await set(node, { ...prev, nick });
      return { ok: true, rankUpdated: false, best: prev.score };
    }
    await set(node, { nick, score: Math.floor(entry.score), stage: entry.stage || 0, asc: entry.asc || 0, ts: Date.now() });
    return { ok: true, rankUpdated: true, best: Math.floor(entry.score) };
  } catch (e) { return { ok: false, err: e.message }; }
}
export async function topAscScores(n = 50) {
  const d = db(); if (!d) return null;
  try {
    const snap = await get(query(ref(d, "slotrank_asc"), orderByChild("score"), limitToLast(n)));
    if (!snap.exists()) return [];
    const out = []; snap.forEach((ch) => { out.push({ cid: ch.key, ...ch.val() }); });
    out.sort((a, b) => (b.score || 0) - (a.score || 0));
    return out;
  } catch (e) { return null; }
}

/** 심화모드(deepMode 심볼 덱빌딩) 랭킹 — 별도 노드 slotrank_deep. entry={nick,score,stage}. */
export async function submitDeepScore(entry) {
  const d = db(); if (!d) return { ok: false };
  const nick = (entry.nick || "익명").trim().slice(0, 16) || "익명";
  try {
    const node = ref(d, "slotrank_deep/" + myKey());
    const cur = await get(node);
    const prev = cur.exists() ? cur.val() : null;
    if (prev && (prev.score || 0) >= entry.score) {
      if (prev.nick !== nick) await set(node, { ...prev, nick });
      return { ok: true, rankUpdated: false, best: prev.score };
    }
    await set(node, { nick, score: Math.floor(entry.score), stage: entry.stage || 0, ts: Date.now() });
    return { ok: true, rankUpdated: true, best: Math.floor(entry.score) };
  } catch (e) { return { ok: false, err: e.message }; }
}
export async function topDeepScores(n = 50) {
  const d = db(); if (!d) return null;
  try {
    const snap = await get(query(ref(d, "slotrank_deep"), orderByChild("score"), limitToLast(n)));
    if (!snap.exists()) return [];
    const out = []; snap.forEach((ch) => { out.push({ cid: ch.key, ...ch.val() }); });
    out.sort((a, b) => (b.score || 0) - (a.score || 0));
    return out;
  } catch (e) { return null; }
}

/** 로그인 직후: 게스트(cid) 최고기록을 계정(u_<uid>)으로 이전 + 게스트 행 삭제(중복 방지).
 *  일반(slotrank) + 승천(slotrank_asc) + 심화(slotrank_deep) 세 보드 모두 이전. */
export async function migrateGuestToUser() {
  const d = db(); if (!d || !_authUid) return { moved: false };
  const cid = myCid();
  let moved = false;
  try {
    // ① 일반 랭킹
    const cidRef = ref(d, "slotrank/" + cid);
    const snap = await get(cidRef);
    if (snap.exists()) {
      const gv = snap.val();
      await submitScore({ nick: gv.nick || "익명", score: gv.score || 0, stage: gv.stage || 0 });   // u_<uid> 로 병합(더 높을 때만)
      await set(cidRef, null);   // 게스트 행 제거(규칙상 비-u_ 키는 삭제 허용)
      moved = true;
    }
    // ② 승천(심화 학기) 랭킹
    const cidAscRef = ref(d, "slotrank_asc/" + cid);
    const snapAsc = await get(cidAscRef);
    if (snapAsc.exists()) {
      const gv = snapAsc.val();
      await submitAscScore({ nick: gv.nick || "익명", score: gv.score || 0, stage: gv.stage || 0, asc: gv.asc || 0 });
      await set(cidAscRef, null);
      moved = true;
    }
    // ③ 심화모드(심볼 덱빌딩) 랭킹
    const cidDeepRef = ref(d, "slotrank_deep/" + cid);
    const snapDeep = await get(cidDeepRef);
    if (snapDeep.exists()) {
      const gv = snapDeep.val();
      await submitDeepScore({ nick: gv.nick || "익명", score: gv.score || 0, stage: gv.stage || 0 });
      await set(cidDeepRef, null);
      moved = true;
    }
    return { moved };
  } catch (e) { return { moved: false, err: e.message }; }
}

/** 상위 n명 (내림차순). 반환 배열 또는 null(오류). 각 항목에 cid 포함. */
export async function topScores(n = 50) {
  const d = db(); if (!d) return null;
  try {
    const snap = await get(query(ref(d, "slotrank"), orderByChild("score"), limitToLast(n)));
    if (!snap.exists()) return [];
    const out = [];
    snap.forEach((ch) => { out.push({ cid: ch.key, ...ch.val() }); });
    out.sort((a, b) => (b.score || 0) - (a.score || 0));
    return out;
  } catch (e) { return null; }
}
