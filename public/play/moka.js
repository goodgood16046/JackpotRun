// 모카월드 계정(카카오) — 잭팟런 웹 로그인 · 통합 랭킹 (2026-11-19, 모카월드 4단계).
// - 로그인: 모카월드 로그인 문(/account/index.html — 로그인·동의가 끝났으면 큰 화면 없이 바로 돌아오고, 그 밖은 /world/connect 로 넘어감)으로 보냈다가 돌아오면 #mokaCode 를 열쇠(Bearer)로 바꾼다(PKCE).
//   처음이면 거기서 카카오 로그인 · 가입을 한다(카톡방 연동 필요 없음).
// - 랭킹: 모든 방이 함께하는 통합 보드(모카월드 서버). 순위는 누구나 보고, 기록은 로그인한 계정만(이름 · 방 이름은 서버가 붙인다).
// - 열쇠는 이 브라우저(localStorage)에만 둔다. 게임 진행도는 지금처럼 기기에 저장된다.
export const ACCOUNT_ORIGIN = "https://mokaworld.app";
export const WORLD_HOME_URL = ACCOUNT_ORIGIN + "/world";   // 모카월드 홈 — 각 서비스에서 돌아가는 곳 (2026-11-20)
const API = ACCOUNT_ORIGIN + "/api/v1/web-account/";
const KEY = "jackpot-moka-v1", PENDING = "jackpot-moka-pending-v1";

// PENDING 도 localStorage — 모바일에서 로그인 화면 이동 중 탭이 다시 만들어져도 살아남게(30분 TTL).
function read(key) { try { return JSON.parse(localStorage.getItem(key)); } catch (e) { return null; } }
function write(key, value) { try { value ? localStorage.setItem(key, JSON.stringify(value)) : localStorage.removeItem(key); } catch (e) {} }

let _identity = null, _init = null, _lastError = null, _cameBack = false;
/** 돌아왔는데 로그인을 마치지 못한 이유(없으면 null). */
export function lastError() { return _lastError; }
/** 방금 모카월드 로그인에서 돌아와 열쇠를 받았는지. */
export function cameBack() { return _cameBack; }

// AbortSignal.timeout 이 없는 브라우저(iOS 15 등)용
function timeoutSignal(ms) {
  const c = new AbortController(); const t = setTimeout(() => c.abort(), ms);
  return { signal: c.signal, done: () => clearTimeout(t) };
}

async function api(path, { body, token } = {}) {
  const headers = {};
  if (token) headers.Authorization = "Bearer " + token;
  if (body !== undefined) { headers["Content-Type"] = "application/json"; headers["X-MS-Client"] = "1"; }
  const to = timeoutSignal(15000);
  let r, j = null;
  try {
    r = await fetch(API + path, {
      method: body === undefined ? "GET" : "POST", credentials: "omit", cache: "no-store", headers,
      signal: to.signal, ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    try { j = await r.json(); } catch (e) {}
  } catch (e) {
    // 브라우저 원문("Failed to fetch" · "Load failed" · 시간 초과) 대신 한글 안내
    const err = new Error("모카월드에 연결하지 못했어요. 잠시 후 다시 시도해 주세요."); err.status = 0;
    throw err;
  } finally { to.done(); }
  if (!r.ok || !j || !j.ok) {
    if (r.status === 401 && token) { write(KEY, null); _identity = null; }
    const e = new Error((j && j.error && j.error.message) || "모카월드에 연결하지 못했어요.");
    e.code = j && j.error && j.error.code; e.status = r.status;
    throw e;
  }
  return j.data;
}

const hex = () => Array.from(crypto.getRandomValues(new Uint8Array(32)), (x) => x.toString(16).padStart(2, "0")).join("");

/** 모카월드 로그인 문으로(돌아올 곳 = 지금 화면). */
export async function login() {
  const verifier = hex(), state = hex();
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier));
  const challenge = btoa(String.fromCharCode(...new Uint8Array(digest))).replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");
  write(PENDING, { verifier, state, at: Date.now() });
  const callback = location.origin + location.pathname + location.search;
  location.assign(ACCOUNT_ORIGIN + "/account/index.html?" + new URLSearchParams({ callback, challenge, state }));
}

/** 돌아온 #mokaCode 를 열쇠로 바꾸고, 내 표시(닉 · 방 이름)를 읽는다. 로그인 안 했으면 null. */
export function initAccount() {
  return (_init ??= (async () => {
    const hash = new URLSearchParams(location.hash.slice(1));
    if (hash.has("mokaCode")) {
      history.replaceState(null, "", location.pathname + location.search);
      const p = read(PENDING); write(PENDING, null);
      if (p && p.state === hash.get("mokaState") && Date.now() - p.at < 30 * 60_000) {
        try {
          write(KEY, await api("exchange", { body: { code: hash.get("mokaCode"), state: p.state, verifier: p.verifier } }));
          _cameBack = true;
        } catch (e) { _lastError = e.message || "로그인을 마치지 못했어요."; }
      } else _lastError = "로그인 요청이 만료됐어요. 다시 로그인해 주세요.";
    }
    const token = read(KEY)?.token;
    if (!token) return null;
    try { const me = await api("jackpot-me", { token }); _identity = me ? { ...me, stars: normStars(me.stars) } : null; } catch (e) {
      _identity = null;
      if (_cameBack) _lastError = "모카월드에 연결하지 못했어요. 잠시 후 다시 시도해 주세요.";
    }
    return _identity;
  })());
}

/** 지금 로그인한 사람 { cid, nick, roomName } | null. */
export function identity() { return _identity; }

/** 서버가 준 별사탕 값을 화면에 그릴 모양으로(수는 수, 글자는 글자 — top() 과 같은 까닭). 없으면 null. */
function normStars(s) {
  if (!s || typeof s !== "object") return null;
  const n = (v) => Math.max(0, Math.floor(Number(v)) || 0);
  const skipped = s.skipped === "TOO_SOON" || s.skipped === "CLOSED" ? s.skipped : undefined;
  return { run: n(s.run), record: n(s.record), runLeft: n(s.runLeft), recordLeft: n(s.recordLeft), balance: n(s.balance), ...(skipped ? { skipped } : {}) };
}

/** 지금 별사탕 상태 { balance, runLeft, recordLeft, … } | null(로그인 전 · 옛 서버). */
export function stars() { return _identity?.stars ?? null; }

export async function logout() {
  // 이 기기에서 먼저 지우고(화면이 바로 바뀌게), 서버의 열쇠는 그다음에 지운다
  const token = read(KEY)?.token;
  write(KEY, null); _identity = null;
  try { if (token) await api("logout", { body: {}, token }); } catch (e) {}
}

const clampInt = (v) => Math.min(10000, Math.max(0, Math.floor(Number(v)) || 0));

/** 통합 랭킹 기록(더 높을 때만 갱신). mode = normal | asc | deep. */
export async function submit(mode, entry) {
  const token = read(KEY)?.token;
  if (!token || !_identity) return { ok: false, needLogin: true, err: "모카월드에 로그인하면 랭킹에 올라가요" };
  const score = Math.floor(Number(entry.score));
  if (!Number.isSafeInteger(score) || score < 1) return { ok: false, err: "등록할 점수가 없어요" };
  try {
    const r = await api("jackpot-scores", { body: { mode, score, stage: clampInt(entry.stage), asc: clampInt(entry.asc) }, token });
    return { ok: true, ...r };
  } catch (e) {
    return { ok: false, needLogin: e.status === 401, err: e.message };
  }
}

/** 판 하나를 끝냈다고 알린다(stage = 넘긴 스테이지 수) → 별사탕(모카월드 공통 재화). 같은 runId 는 서버가 한 번만 준다. */
export async function reportRun(mode, stage, runId) {
  const token = read(KEY)?.token;
  if (!token || !_identity) return { ok: false, needLogin: true };
  try {
    // 별사탕을 모르는 옛 서버(jackpot-me 에 stars 가 없음)에는 보내지 않는다 — 화면은 아무것도 그리지 않는다
    if (!_identity.stars) return { ok: true, stars: null };
    const r = await api("jackpot-run", { body: { mode, stage: clampInt(stage), runId }, token });
    const stars = normStars(r?.stars);
    // 기다리는 동안 로그아웃했으면(다른 사람으로 다시 로그인했어도) 표시를 되살리지 않는다
    if (stars && _identity && read(KEY)?.token === token) _identity = { ..._identity, stars };
    return { ok: true, stars };
  } catch (e) {
    return { ok: false, needLogin: e.status === 401, err: e.message };
  }
}

/** 통합 랭킹 상위 n(로그인 필요 없음). 실패 = null. */
export async function top(mode, n = 50) {
  try {
    const data = await api(`jackpot-ranking?mode=${mode}&limit=${Math.min(100, Math.max(1, n))}`);
    if (!Array.isArray(data.rows)) return null;
    // 화면에 그리는 값만 모양을 맞춰 넘긴다(글자는 글자, 수는 수)
    return data.rows.map((v) => ({
      cid: String(v?.cid ?? ""), nick: String(v?.nick ?? ""), roomName: String(v?.roomName ?? ""),
      score: Number(v?.score) || 0, stage: Number(v?.stage) || 0, asc: Number(v?.asc) || 0, ts: Number(v?.ts) || 0,
    }));
  } catch (e) { return null; }
}
