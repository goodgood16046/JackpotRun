// ── 잭팟 슬롯 (웹 단독판) — 로그인(Firebase Auth) ─────────────────────
//  게스트는 로그인 없이 그대로 플레이(기존 cid/localStorage). 로그인은 선택.
//  현재 활성: 구글. 애플은 코드는 준비돼 있으나 콘솔/Apple 설정 완료 후 UI 노출.
//  지연 로드(ui 가 필요할 때 import) — 오프라인/미설정이어도 게임은 정상 동작.
import { initializeApp, getApps, getApp } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js";
import {
  getAuth, GoogleAuthProvider, OAuthProvider, signInWithPopup, signInWithRedirect,
  getRedirectResult, signOut, onAuthStateChanged, setPersistence, browserLocalPersistence,
} from "https://www.gstatic.com/firebasejs/10.12.2/firebase-auth.js";

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
// rank.js 와 앱 인스턴스 공유(중복 initializeApp 방지).
function fbApp() { return getApps().length ? getApp() : initializeApp(firebaseConfig); }
let _auth = null;
function authInst() {
  if (!_auth) { _auth = getAuth(fbApp()); try { setPersistence(_auth, browserLocalPersistence); } catch (e) {} }
  return _auth;
}

// 노출용 사용자 형태(민감정보 최소).
const shape = (u) => u ? {
  uid: u.uid, name: u.displayName || "", photo: u.photoURL || "", email: u.email || "",
  provider: (u.providerData && u.providerData[0] && u.providerData[0].providerId) || "",
} : null;

/** 로그인 상태 변화 구독(초기 1회 포함). cb(user|null). */
export function watchAuth(cb) { try { onAuthStateChanged(authInst(), (u) => cb(shape(u))); } catch (e) { cb(null); } }
export function currentUser() { try { return shape(authInst().currentUser); } catch (e) { return null; } }

export async function signInGoogle() { return oauthLogin(new GoogleAuthProvider()); }
// 애플 — Firebase 콘솔 + Apple Developer(Service ID·키·도메인 인증) 설정 완료 후 사용.
export async function signInApple() {
  const p = new OAuthProvider("apple.com"); p.addScope("name"); p.addScope("email");
  return oauthLogin(p);
}
// 팝업 우선, 팝업 차단/미지원 환경이면 리다이렉트로 폴백(페이지 이동 후 복귀 시 완료).
async function oauthLogin(provider) {
  try {
    const r = await signInWithPopup(authInst(), provider);
    return shape(r.user);
  } catch (e) {
    const code = (e && e.code) || "";
    if (code === "auth/popup-blocked" || code === "auth/operation-not-supported-in-this-environment" || code === "auth/web-storage-unsupported") {
      await signInWithRedirect(authInst(), provider);
      return null;   // 리다이렉트 진행 중(복귀 시 completeRedirect/onAuthStateChanged 로 완료)
    }
    throw e;   // 취소/미활성화 등은 호출부에서 처리
  }
}
// 리다이렉트 복귀 완료(있으면). 실패해도 조용히 무시(게스트로 계속).
export async function completeRedirect() { try { const r = await getRedirectResult(authInst()); return shape(r && r.user); } catch (e) { return null; } }
export async function signOutUser() { try { await signOut(authInst()); } catch (e) {} }
