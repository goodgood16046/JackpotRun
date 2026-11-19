// ── 잭팟 슬롯 (웹 단독판) — 로그인(모카월드 · 카카오, 2026-11-19) ─────────────
//  게스트는 로그인 없이 그대로 플레이(기존 cid/localStorage). 로그인은 선택 — 하면 통합 랭킹에 올라간다.
//  예전 구글 · 애플(Firebase Auth) 로그인은 모카월드 로그인으로 바뀌었다. 예전 랭킹 기록은 rank.js 의 「예전 기록」으로 그대로 볼 수 있다.
//  ui.js 가 쓰던 이름(watchAuth · signInGoogle 등)을 그대로 둔다 — 구글 · 애플 이름도 모카월드 로그인으로 이어진다.
import { cameBack, identity, initAccount, lastError, login, logout } from "./moka.js";

// 노출용 사용자 형태(민감정보 최소). provider "moka" = 모카월드(카카오). roomName = 랭킹에 붙는 방 이름(웹 계정은 「모카월드」).
const shape = (i) => i ? { uid: i.cid, name: i.nick || "", photo: "", email: "", provider: "moka", roomName: i.roomName || "" } : null;

/** 로그인 상태(초기 1회). 모카월드에서 돌아왔으면 그 열쇠로 바꾼 뒤 알린다. cb(user|null). */
export function watchAuth(cb) { initAccount().then((i) => cb(shape(i)), () => cb(null)); }
export function currentUser() { return shape(identity()); }

/** 모카월드 로그인 문으로 이동(돌아오면 watchAuth 가 로그인 상태로 알린다). */
export async function signIn() { await login(); }
export const signInGoogle = signIn;
export const signInApple = signIn;

/** 예전 Firebase 리다이렉트 복귀 자리 — 모카월드는 initAccount 가 처리한다. */
export async function completeRedirect() { return null; }

export async function signOutUser() { await logout(); }

/** 돌아왔는데 로그인을 마치지 못한 이유(없으면 null). watchAuth 콜백 이후에 읽는다. */
export function lastLoginError() { return lastError(); }
/** 방금 모카월드 로그인에서 돌아왔는지. */
export function justReturned() { return cameBack(); }
