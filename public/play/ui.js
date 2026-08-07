// ── 잭팟 슬롯 (웹 단독판) — UI 레이어 ─────────────────────────────────
// game.js 컨트롤러를 버튼/카드/애니메이션으로 구동. 채팅 입력 없음.
import { Game, PHASE } from "./game.js";
import * as D from "./data.js";
import { symTierOf, symCatOf, isAutoDecayTarget } from "./engine.js";
import * as snd from "./sound.js";

const g = new Game();
let soundOn = (localStorage.getItem("slotweb_sound") !== "0");
snd.setEnabled(soundOn);
let vibeOn = (localStorage.getItem("slotweb_vibe") !== "0");   // 진동(햅틱) on/off
const sndIcon = () => (soundOn ? "🔊" : "🔇");
// 진동 — 설정 off 면 무시. navigator.vibrate 미지원 기기는 자동 무시(예외 무시).
function vibrate(pattern) { if (!vibeOn) return; try { navigator.vibrate && navigator.vibrate(pattern); } catch (_) {} }
const app = document.getElementById("app");
const toastRoot = document.getElementById("toast");
const sheetRoot = document.getElementById("sheet-root");
const gearBtn = document.getElementById("gearbtn");
// 고정 설정 기어: 인트로/플레이 화면 외 모든 화면 우상단에 표시(플레이는 HUD 내 기어 사용).
function gearShow(on) { if (gearBtn) gearBtn.style.display = on ? "flex" : "none"; }

let st = null;            // 현재 상태 스냅샷
let busy = false;         // 애니메이션 중
let logShown = 0;         // 토스트 표시 커서
let lastGain = null;      // 최근 스핀 결과 표시
let uiManip = null;       // 고정/복사/교체 칸 선택 대기
let dexTab = "char";      // 도감 탭
let selAsc = 0;           // 홈에서 선택한 심화 학기(승천) 단계
let selDeep = false;      // 홈에서 선택한 심화모드(심볼 주머니 덱빌딩) — 일반/심화 토글
let rankMode = "normal";  // 랭킹 화면 모드: normal | asc | deep
let shopTab = "buy";      // 심화모드 상점 탭: buy(증강/유물/아이템) | repair(심볼 정비). 일반모드 무시.
let introTimer = null;    // 인트로 릴 회전
const tut = { active: false, ti: 0, live: false, shown: {} };   // 튜토리얼

const esc = (s) => String(s == null ? "" : s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
// 배치F P1: 칸당 확률 %(소수 2자리·끝0 자동제거, fmt2 계약) — 분모 0 가드.
const pct2 = (n, d) => d > 0 ? Math.round(n / d * 10000) / 100 : 0;
// 배치G: 전공(계열 아키타입) HUD 칩 — 활성(tier>0)일 때만 콤팩트 배지. pouchView.archetype 소비.
function archHudChip(pv) {
  const a = pv && pv.archetype; if (!a || a.tier <= 0) return "";
  return ` <span class="dh-arch t${a.tier}" title="${esc(a.n)} ${a.tier >= 2 ? "심화(2차)" : "1차"} 전공">${a.e}${a.tier >= 2 ? "★" : ""}</span>`;
}
// §9.1 J2: 피버 게이지 HUD — 덱 카운트 칩 옆에 인라인 배치. 기존 deep-hud/expbar 문법 재사용.
//  피버 중(feverSpins>0): 붉은 톤 + "🔥 Nx스핀" 표기. 미진입: 게이지 채움 바 표시.
//  ★일반모드 호출 없음(renderBattle 내 st.deepMode 조건 분기로만 호출).
function feverHudHtml(st) {
  const gauge = st.feverGauge || 0;
  const spins = st.feverSpins || 0;
  const max = (D.DEEP && D.DEEP.FEVER_MAX) ? D.DEEP.FEVER_MAX : 100;
  const pct = Math.min(100, Math.round(gauge / max * 100));
  if (spins > 0) {
    // 피버 진행 중 — 붉은 톤 칩 + 잔여 스핀 수
    return `<span class="fever-hud fever-active">🔥 ${spins}스핀</span>`;
  }
  // 피버 대기 중 — 게이지 바
  return `<span class="fever-hud"><span class="fever-bar-wrap"><span class="fever-bar-fill" id="fever-fill" style="width:${pct}%"></span></span><span class="fever-label">🔥${pct}%</span></span>`;
}
// 배치G: 전공 게이지 — 활성/최근접 전공 + 다음 임계까지 진행("🍒과수원 23%→25%"). 시트 상단용. pct2 헬퍼 재사용.
function archGaugeHtml(pv) {
  const a = pv && pv.archetype; if (!a) return "";
  const share = Math.round(a.share * 10000) / 100;   // 현재 비중 %(fmt2)
  const lvl = a.tier >= 2 ? "심화 전공(2차)" : a.tier >= 1 ? "전공(1차)" : "전공 준비";
  const metricTxt = a.metric === "coin" ? "코인" : a.metric === "score" ? "점수" : "EXP";
  const bonus = a.metric === "coin" ? (a.tier >= 2 ? "+20%" : "+10%") : (a.tier >= 2 ? "+30%" : "+15%");
  const nextTxt = a.nextThreshold != null
    ? `<span class="arch-next">${share}% → ${Math.round(a.nextThreshold * 100)}%</span>`
    : `<span class="arch-next max">최대 달성</span>`;
  const pctToNext = a.nextThreshold != null ? Math.min(100, Math.round(a.share / a.nextThreshold * 100)) : 100;
  return `<div class="arch-box t${a.tier}">
    <div class="arch-head"><span class="arch-e">${a.e}</span><span class="arch-nm">${esc(a.n)}</span><span class="arch-lvl">${lvl}</span>${a.tier > 0 ? `<span class="arch-bonus">${esc(metricTxt)} ${bonus}</span>` : ""}</div>
    <div class="arch-bar"><div class="arch-fill t${a.tier}" style="width:${pctToNext}%"></div></div>
    <div class="arch-foot">계열 비중 <b>${share}%</b> ${nextTxt}</div>
  </div>`;
}
const SPIN_EMOJIS = D.SYMS.filter((s) => s.weight > 0).map((s) => s.e);
const titleOf = (best) => { const t = D.SCORE_TITLES.find((x) => best >= x.min); return t ? t.e + t.n : "🐣잭팟 새내기"; };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const $ = (id) => document.getElementById(id);
const CLEAR_BEAT = 2000;   // 스테이지 클리어 결과 화면이 나오기 전 여운(ms) — 승리 보드를 잠시 음미

// ── 토스트 ──
function drainToasts() {
  while (g.log.length > logShown) { showToast(g.log[logShown]); logShown++; }
}
function showToast(msg) {
  const t = document.createElement("div"); t.className = "t"; t.textContent = msg; toastRoot.appendChild(t);
  setTimeout(() => { t.style.transition = "opacity .4s, transform .4s"; t.style.opacity = "0"; t.style.transform = "translateY(-6px)"; setTimeout(() => t.remove(), 420); }, 2300);
  // 최대 3개 제한 — 초과 시 가장 오래된 토스트 즉시 제거(페이드 생략)
  const ts = toastRoot.children; while (ts.length > 3) ts[0].remove();
}

// ── 클릭 위임 (document 전역 — #app 과 바텀시트 모두 포함) ──
document.addEventListener("click", (e) => {
  const t = e.target.closest("[data-act]"); if (!t) return;
  snd.resume();
  if (t.dataset.act !== "spin" && t.dataset.act !== "noop") snd.sfx("tap");
  tapFx(e.clientX, e.clientY, t);
  handle(t.dataset.act, t.dataset.arg, t);
});
// 모바일/인앱브라우저 오디오 잠금 해제 — 첫 포인터다운에서 컨텍스트 재개
document.addEventListener("pointerdown", () => snd.resume(), { passive: true });

// ── 탭 이펙트 (리플 + 파티클 + 햅틱) — 모든 클릭에 손맛 부여 ──
function tapFx(x, y, el) {
  if (x == null) return;
  vibrate(7);
  const cl = el.classList;
  const big = cl.contains("bigbtn") || cl.contains("spinbtn") || cl.contains("regbtn");
  const card = cl.contains("bigcard") || cl.contains("pcard") || cl.contains("reel");
  let col = "rgba(255,210,63,.55)";
  if (cl.contains("dev")) col = "rgba(46,230,200,.6)";
  else if (cl.contains("special")) col = "rgba(176,123,255,.62)";
  else if (cl.contains("danger")) col = "rgba(255,93,108,.6)";
  else if (cl.contains("ghost") || cl.contains("chip") || cl.contains("tab") || cl.contains("iconbtn") || cl.contains("sheet-close") || cl.contains("sheet-bg") || cl.contains("backbtn")) col = "rgba(180,200,255,.42)";
  const ripple = document.createElement("div"); ripple.className = "tap-ripple" + (big ? " big" : "");
  ripple.style.left = x + "px"; ripple.style.top = y + "px"; ripple.style.setProperty("--col", col);
  document.body.appendChild(ripple); setTimeout(() => ripple.remove(), 560);
  if (big || card || cl.contains("abtn") || cl.contains("reco") || cl.contains("iuse")) {
    const n = big ? 9 : 6; const solid = col.replace(/[\d.]+\)\s*$/, "1)");
    for (let i = 0; i < n; i++) {
      const p = document.createElement("div"); p.className = "tap-pt";
      const a = (Math.PI * 2 * i) / n + Math.random() * 0.7; const d = (big ? 28 : 17) + Math.random() * 16;
      p.style.left = x + "px"; p.style.top = y + "px"; p.style.background = solid; p.style.color = solid;
      p.style.setProperty("--dx", (Math.cos(a) * d).toFixed(0) + "px"); p.style.setProperty("--dy", (Math.sin(a) * d).toFixed(0) + "px");
      document.body.appendChild(p); setTimeout(() => p.remove(), 460);
    }
  }
}

let curStage = 0;
function handle(act, arg, el) {
  switch (act) {
    case "noop": break;
    case "start": st = g.startRun(selDeep ? 0 : selAsc, selDeep); logShown = g.log.length; lastGain = null; render(); break;
    case "deepToggle": selDeep = !selDeep; if (selDeep) selAsc = 0; renderHome(); break;   // 심화모드(주머니) 토글 — 켜면 승천 0 고정(요구치 이중가중 방지)
    case "pouch": openPouchSheet(); break;   // 심화모드 주머니 현황 시트
    case "ascUp": selAsc = Math.min(g.maxPlayableAsc(), selAsc + 1); renderHome(); break;
    case "ascDown": selAsc = Math.max(0, selAsc - 1); renderHome(); break;
    case "selChar": st = g.selectChar(arg); afterSelect(); break;
    case "selMac": st = g.selectMachine(arg); afterSelect(); break;
    case "selDev": st = g.selectDevice(arg); afterSelect(); break;
    case "spin": doSpin(arg || "N"); break;
    case "item": openItemSheet(); break;
    case "deviceSheet": openDeviceSheet(); break;
    case "status": openStatusSheet(); break;
    case "cellinfo": if (!busy) openCellSheet(+arg); break;
    case "deckCard": openDeckCardDetail(arg); break;   // §10 V3: 덱 보드 카드 탭 → 상세(뒤로=덱 보드 복귀)
    case "buildInfo": showBuildInfo(arg, el); break;
    case "useItem": st = g.useItem(+arg); afterPlayAction(); closeSheet(); break;
    case "dev": doDevice(arg); break;
    case "manipPick": uiManip = arg; updateExtras(); break;
    case "manip": uiManip = null; { const out = g.manip(arg, +($("manipArg")?.value || 0)); afterManip(out); } break;
    case "manipN": { uiManip = null; const out = g.manip(arg.split(":")[0], +arg.split(":")[1]); afterManip(out); } break;
    case "giveUpAsk": giveUpAsk(); break;
    case "giveUpConfirm": closeSheet(); st = g.giveUp(true); drainToasts(); render(); break;
    case "proceed": st = g.proceedToNodes(); render(); break;
    case "cleardetail": { const bd = $("clear-bd"); if (bd && el) { const open = bd.style.display === "none"; bd.style.display = open ? "block" : "none"; el.textContent = (open ? "▲" : "▼") + " 점수 상세"; } break; }
    case "proceedStage": st = g.proceedToStage(); lastGain = null; render(); break;
    case "node": { if (el) { el.classList.add("chosen"); spawnSparkle(el, "GOLD"); } st = g.selectNode(+arg); drainToasts(); setTimeout(render, 260); break; }
    case "perk": {
      const p = st.options[+arg];
      // 심화모드 주머니 보상은 {type,id/from/to,n,preview} 라 증강/유물({e,n,t,d})과 형태가 다르다.
      // celebratePerk 가 기대하는 형태로 라벨을 채워 넣어 빈 팝업을 방지(시각 폴리시만).
      // V3P4: SYNAUG_BONUS 도 special/skip 카드 계약 → 동일 변환 경유(undefined 렌더 방지).
      const cp = (st.pickKind === "POUCH" || st.pickKind === "SYNAUG_BONUS") ? pouchCelebrateData(p) : p;
      if (el) { el.classList.add("chosen"); spawnSparkle(el, cp && cp.t); }
      st = g.pickPerk(+arg); drainToasts(); setTimeout(() => celebratePerk(cp), 230); setTimeout(render, 820); break;
    }
    case "shopBuyAsk": shopBuyAsk(+arg); break;
    case "shopBuyConfirm": closeSheet(); st = g.shopBuy(+arg); snd.sfx("coin"); drainToasts(); render(); break;
    case "shopReroll": st = g.shopReroll(); drainToasts(); render(); break;
    case "shopExit": shopTab = "buy"; st = g.shopExit(); drainToasts(); render(); break;
    // ── Phase 3: 심화모드 상점 심볼 정비 탭 ──
    case "shopTab": shopTab = arg; render(); break;
    case "repairAsk": repairAsk(arg); break;
    case "repairSelSym": repairSel.sel = { id: arg }; repairConfirm(); break;
    case "repairSelFrom": repairSel.sel = { from: arg }; repairPickSwapTo(); break;
    case "repairSelTo": repairSel.sel = { ...repairSel.sel, to: arg }; repairConfirm(); break;
    case "repairSelTag": repairSel.sel = { tag: arg }; repairConfirm(); break;
    case "repairConfirmBuy": repairDoBuy(); break;
    case "repairCancel": repairCancel(); break;
    case "devnode": st = g.deviceNodeTake(arg === "equip"); drainToasts(); render(); break;
    case "restart": st = g.startRun(selDeep ? 0 : selAsc, selDeep); logShown = g.log.length; lastGain = null; render(); break;
    case "home": st = null; render(); break;
    case "introStart": exitIntro(); break;
    case "rank": openRank(); break;
    case "levelRewards": renderLevelRewards(); break;
    case "goalsback": render(); break;
    case "rankMode": rankMode = arg; openRank(); break;
    case "login": doLogin("google"); break;
    case "loginApple": doLogin("apple"); break;
    case "gateGuest": localStorage.setItem("slotweb_seenlogin", "1"); st = null; render(); break;
    case "logout": doLogout(); break;
    case "rankback": render(); break;
    case "submitRank": submitRank(); break;
    case "endRankSubmit": endRankSubmit(); break;
    case "dex": renderDex(); break;
    case "dexTab": dexTab = arg; renderDex(); break;
    case "dexback": render(); break;
    case "dexInfo": openDexInfo(arg.split("|")[0], arg.split("|")[1]); break;
    case "tut": startTutorial(); break;
    case "tutNext": tut.ti++; renderTour(); break;
    case "tutSkip": endTutorial(); break;
    case "tutToFree": tutToFree(); break;
    case "tutGoLive": tutGoLive(); break;
    case "tutBannerOk": tutClear(); break;
    case "resetAsk": openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 취소</button><h3>⚠️ 데이터 초기화</h3>
      <div class="sub">최고 점수 · 캐릭터/머신/장치 해금 · 도감 발견 기록이 <b style="color:var(--red)">모두 사라지고</b> 처음(초보학생+기본)부터 시작합니다.<br><span class="dim">랭킹에 등록한 기록은 그대로 유지돼요.</span></div>
      <div class="confirm-row"><button class="bigbtn ghost" data-act="closeSheet">취소</button><button class="bigbtn danger-btn" data-act="resetConfirm">🧹 초기화</button></div>`); break;
    case "resetConfirm": g.resetData(); closeSheet(); st = null; logShown = g.log.length; lastGain = null; render(); showToast("🧹 데이터를 초기화했어요 — 처음부터 시작!"); break;
    case "soundToggle": setSound(!soundOn); break;
    case "settings": openSettings(); break;
    case "setMute": setSound(!soundOn);
      { if (el) { el.textContent = soundOn ? "켜짐" : "꺼짐"; el.classList.toggle("on", soundOn); }
        const vr = document.querySelector(".set-row.vol"); if (vr) vr.classList.toggle("disabled", !soundOn); } break;
    case "setVibe": vibeOn = !vibeOn; localStorage.setItem("slotweb_vibe", vibeOn ? "1" : "0");
      if (vibeOn) vibrate(18);   // 켤 때 짧게 진동으로 확인
      if (el) { el.textContent = vibeOn ? "켜짐" : "꺼짐"; el.classList.toggle("on", vibeOn); } break;
    case "closeSheet": closeSheet(); break;
  }
}
function afterSelect() { drainToasts(); if (st.phase === PHASE.SPIN) lastGain = null; render(); }

// ── 디바이스 액션 ──
function doDevice(arg) {
  let out;
  if (arg === "투입") out = g.insertCoin();
  else if (arg === "예언") out = g.oracle();
  else if (arg === "비상") out = g.emergencyBell();
  else return;
  drainToasts();
  if (leftPlay(g.run.phase)) { st = g.state(); renderBeat(); }
  else { st = g.state(); updateHud(); updateExtras(); }
}
function afterManip(out) {
  st = out.state || g.state(); drainToasts();
  if (out.cells) { const rb = $("reels"); rb && rb.classList.remove("multi-2", "multi-3", "multi-4", "multi-5"); setReels(out.cells); glowMatches([...(rb?.querySelectorAll(".reel") || [])], out.cells, out.setIds, out.bestSetId, out.bestCount, rb); }
  if (out.gained != null) { lastGain = { gained: out.gained, notes: out.notes || [], jackpot: null, banner: out.banner, bestCount: out.bestCount, setGroups: (out.setIds || []).length }; showGain(); }
  if (leftPlay(st.phase)) { renderBeat(650); }
  else { updateHud(); updateExtras(); }
}
function afterPlayAction() {
  st = g.state(); drainToasts();
  if (leftPlay(st.phase)) renderBeat();
  else { updateHud(); updateExtras(); }
}
function leftPlay(phase) { return phase === PHASE.STAGE_CLEAR || phase === PHASE.NODE_SELECT || phase === PHASE.REWARD_DONE || phase === PHASE.RUN_END; }
// STAGE_CLEAR 로 진입하면 ~2초 여운을 두고 결과 화면을 띄운다(승리 보드 음미). 그 외 화면은 즉시/기존 지연.
function renderBeat(fallbackDelay = 0) {
  if (st && st.phase === PHASE.STAGE_CLEAR) setTimeout(render, CLEAR_BEAT);
  else if (fallbackDelay) setTimeout(render, fallbackDelay);
  else render();
}

// ── 스핀(애니메이션 + 이펙트) ──
// Unity ReelView(S13/S14) 노치 스크롤 이식 — jackpotpick/reel.js 와 동일 메커니즘의 게임판.
// 반복 플레이 리듬을 위해 Unity 명시값 대비 소폭 단축(가속 .25→.22 등).
const R_ACCEL = 0.22;                    // 0→최고속 가속
const R_NOTCH_MAX = 0.055;               // 최고속 노치(초)
const R_NOTCH_START = 0.14;              // 가속 시작 노치
const R_HOLD = 0.28;                     // 스태거 시작 전 유지
const R_STAGGER = 0.09;                  // 릴별 정지 지연
const R_DECEL = [0.09, 0.14, 0.20];      // 감속 3노치(outCubic) — 타깃은 idx0 주입(D-3 공식)
const R_OVERSHOOT_RATIO = 0.085, R_OVERSHOOT_DUR = 0.14; // 착지 튕김(outBack) — 오버슈트 픽셀은 슬롯 크기(S) 비례

// ── 이징/트윈(rAF 기반) — jackpotpick/reel.js 와 동일 공식 ──
const linear = (t) => t;
const outQuad = (t) => t * (2 - t);
const outCubic = (t) => 1 - (1 - t) ** 3;
const outBack = (t) => { const c1 = 1.70158, c3 = c1 + 1; return 1 + c3 * (t - 1) ** 3 + c1 * (t - 1) ** 2; };
const clamp01 = (x) => (x < 0 ? 0 : x > 1 ? 1 : x);
const lerp = (a, b, t) => a + (b - a) * t;
function rTween(dur, ease, fn) {
  return new Promise((resolve) => {
    if (dur <= 0) { fn(1); resolve(); return; }
    const t0 = performance.now();
    function frame(now) {
      const t = Math.min(1, (now - t0) / (dur * 1000));
      fn(ease(t));
      if (t >= 1) { resolve(); return; }
      requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
  });
}
function randSpinCell() { return { sym: { e: SPIN_EMOJIS[Math.floor(Math.random() * SPIN_EMOJIS.length)] }, tag: "" }; }

// 릴 1개 시퀀스: 결과(landCell)는 이미 확정된 상태로 진입 — 연출은 그 결과에 착지할 뿐(결정론).
async function spinReelOne(reelEl, idx, landCell) {
  const strip = reelEl.querySelector(".rstrip");
  const S = reelEl.clientHeight / 1.5;   // 뷰포트 높이 = 1.5×슬롯(§D)
  const BASE = -1.75 * S;
  const overshoot = S * R_OVERSHOOT_RATIO;
  reelEl.classList.add("spinning");
  reelEl.classList.remove("settle", "match", "m-2", "m-3", "m-4", "m-5", "blast", "empty");
  [...strip.children].forEach((s) => s.classList.remove("center"));
  reelEl.querySelectorAll(".mshine, .mspark").forEach((n) => n.remove());

  const advance = (dur, ease, cell) => rTween(dur, ease, (p) => {
    strip.style.transform = `translateY(${BASE + S * p}px)`;
  }).then(() => {
    const recycled = strip.lastElementChild;
    strip.insertBefore(recycled, strip.firstElementChild);
    recycled.innerHTML = reelInner(cell);
    strip.style.transform = `translateY(${BASE}px)`;
  });

  let t = 0;
  const stopAt = R_ACCEL + R_HOLD + idx * R_STAGGER;
  while (true) {
    const nd = lerp(R_NOTCH_START, R_NOTCH_MAX, outQuad(clamp01(t / R_ACCEL)));
    if (t + nd >= stopAt) break;
    await advance(nd, linear, randSpinCell());
    t += nd;
  }
  await advance(R_NOTCH_MAX, linear, randSpinCell());   // 유지 마지막 노치

  reelEl.classList.remove("spinning");
  for (let k = 0; k < 3; k++) {
    // 감속 3노치(outCubic) — idx0 에서 타깃 주입(D-3 공식): 마지막 노치 종료 시 정확히 중앙 도착
    await advance(R_DECEL[k], outCubic, k === 0 ? landCell : randSpinCell());
  }

  strip.style.transform = `translateY(${BASE - overshoot}px)`;
  await rTween(R_OVERSHOOT_DUR, outBack, (p) => {
    strip.style.transform = `translateY(${BASE - overshoot + overshoot * p}px)`;
  });

  strip.children[2].classList.add("center");
  reelEl.classList.add("settle");
  strip.style.transform = "";   // outBack 종료 지점(BASE)은 CSS 기본값(-35%)과 동일 지점 — 인라인 px 를 걷어내 리사이즈/브레이크포인트 전환 시 어긋남 방지
  // 착지한 이웃 장식(0,1,3,4)을 memo 에 반영 — 다음 재렌더(setReels/renderPlay)가 지금 보이는 모습 그대로 복원(깜빡임 방지).
  stripMemo.set(idx, [0, 1, 3, 4].map((k) => strip.children[k].querySelector(".sym")?.textContent || ""));
  snd.sfx("reel");
}

async function doSpin(mode) {
  if (busy || st.phase !== PHASE.SPIN) return;
  busy = true; uiManip = null; snd.sfx("spin");
  const spinBtn = $("spinbtn"); if (spinBtn) spinBtn.disabled = true; updateExtras();
  const reelsBox = $("reels");
  let reelEls = [...reelsBox.querySelectorAll(".reel")];
  reelsBox.classList.remove("win", "jackpot");
  reelsBox.classList.remove("multi-2", "multi-3", "multi-4", "multi-5");
  const out = g.spin(mode);   // 결과를 먼저 확정 — 연출은 이 결과에 착지할 뿐
  if (!out || !out.cells) {   // 조기 반환(예: 이미 쓴 특수스핀) — 애니 안전 복구
    st = out || g.state(); busy = false; drainToasts(); renderPlay(); return;
  }
  const finalCells = out.cells;
  const landCells = out.preCells || finalCells;   // 폭탄 폭발 전(착지) 모습
  const reduced = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (reduced) {
    setReels(landCells);
    reelEls = [...reelsBox.querySelectorAll(".reel")];   // setReels 가 DOM 을 새로 그렸으므로 재조회
    await sleep(150);
  } else {
    try {
      await Promise.all(reelEls.map((el, i) => spinReelOne(el, i, landCells[i] || finalCells[i])));
    } catch (err) {
      // 연출 예외에도 결과는 반드시 반영한다(g.spin 은 이미 상태를 바꿨다) — 최종 상태로 강제 스냅
      setReels(landCells);
      reelEls = [...reelsBox.querySelectorAll(".reel")];
    }
  }
  await sleep(110);
  if (out.bomb) await blastBomb(reelEls, out.bomb, finalCells);   // 다 등장 후 폭발 → 비우기
  glowMatches(reelEls, finalCells, out.setIds, out.bestSetId, out.bestCount, reelsBox);
  if (out.jackpot) { reelsBox.classList.add("jackpot"); jackpotFx(out.jackpot); shake("bg"); floatExp(out.gained, true); snd.sfx("jackpot"); }
  else { if (out.bestCount >= 3) { reelsBox.classList.add("win"); shake("sm"); snd.sfx("win"); } if (out.gained >= st.quota / Math.max(1, st.spins) * 1.5) floatExp(out.gained, false); }
  lastGain = { gained: out.gained, notes: out.notes, jackpot: out.jackpot, banner: out.banner, bestCount: out.bestCount, setGroups: (out.setIds || []).length };
  st = out.state; showGain(); updateHud(); drainToasts();
  // 잭팟 흔들림(jpShake)만 1회성으로 제거.
  // ★반짝임 일회성화(CSS): 매치 펄스/빛스윕(.mshine)/반짝이(.mspark)/프리즘/컨테이너 호흡은
  // 전부 finite iteration(+both) 라 매치 직후 ~2초 터졌다가 잦아든다. 결과 화면 동안에는
  // 차분한 정적 하이라이트(.reel.match 골드 테두리 + 정적 글로우)만 남아 매치 셀을 표시.
  // 클래스 자체(.match/m-*/multi-*)는 다음 스핀 시작 시 doSpin 상단에서 일괄 초기화한다.
  setTimeout(() => { reelsBox.classList.remove("jackpot"); }, 1500);
  busy = false;
  if (tut.active && !tut.live) {
    if (out.cleared || out.gameOver) tut.live = true;   // 클리어/실패 → 라이브로(이후 화면이 안내)
    else if (TOUR[tut.ti] && TOUR[tut.ti].action) { setTimeout(tutExplainSpin, 260); }
  }
  if (out.cleared) { busy = true; await sleep(CLEAR_BEAT); busy = false; render(); }   // 클리어 → ~2초 여운 후 결과
  else if (out.gameOver) { await sleep(out.jackpot ? 1300 : 850); render(); }
  else updateExtras();
}

// ── 이펙트 헬퍼 ──
function shake(kind) { app.classList.remove("shk-sm", "shk-bg", "shk-xl"); void app.offsetWidth; app.classList.add(kind === "xl" ? "shk-xl" : kind === "bg" ? "shk-bg" : "shk-sm"); setTimeout(() => app.classList.remove("shk-sm", "shk-bg", "shk-xl"), 640); }
function glowMatches(reelEls, cells, setIds, bestSetId, bestCount, reelsBox) {
  if (!setIds || !setIds.length) return;
  const set = new Set(setIds);
  // 각 값심볼 id 별 등장수 집계(와일드 제외) + 와일드 개수
  const countById = {}; let wilds = 0;
  cells.forEach((c) => {
    if (!c.sym) return;
    if (c.sym.special === "WILD") wilds++;
    else if (set.has(c.sym.id)) countById[c.sym.id] = (countById[c.sym.id] || 0) + 1;
  });
  // 엔진 규칙: 와일드는 최다 그룹(bestSetId)에 합산됨 → 그 그룹의 표시 크기에만 반영
  const grpSize = (id) => (countById[id] || 0) + (id === bestSetId ? wilds : 0);
  const lvl = (n) => Math.max(2, Math.min(5, n));   // 2~5 단계로 클램프
  let topLvl = 0;
  cells.forEach((c, i) => {
    if (!c.sym || !reelEls[i]) return;
    const isWild = c.sym.special === "WILD";
    if (!isWild && !set.has(c.sym.id)) return;
    // 와일드는 최다 그룹에 묻어가므로 bestCount 크기로 발광
    const n = lvl(isWild ? (bestCount || grpSize(bestSetId) || 2) : grpSize(c.sym.id));
    reelEls[i].classList.add("match", "m-" + n);
    addMatchSparkle(reelEls[i], n);
    if (n > topLvl) topLvl = n;
  });
  // 컨테이너에 전체 최강 단계 → 단계별 배경/흔들림 강화(고단계 화려)
  if (reelsBox && topLvl >= 2) reelsBox.classList.add("multi-" + topLvl);
}
// 매치 셀에 빛 스윕(.mshine) + 반짝이 입자(.mspark) 주입. 단계가 클수록 입자 多.
// "같은 게 많을수록 더 많이 반짝" — 입자 수를 단계별로 뚜렷이 벌리고(2→3,3→6,4→9,5→13),
// 고단계는 트윙클 주기를 짧게(더 자주 반짝)+딜레이 폭 좁게(밀집 반짝) 한다.
// 셀 안(overflow:hidden)에서만 동작 → 레이아웃 무영향. 다음 스핀 innerHTML 리셋으로 정리.
const SPARK_CNT = { 2: 3, 3: 6, 4: 9, 5: 13 };   // 단계별 입자 수(상한 13)
function addMatchSparkle(el, n) {
  if (!el || el.querySelector(".mshine")) return;   // 중복 방지(결과 화면 동안 1회만)
  const set = n >= 5 ? ["✨", "🌟", "💫", "⭐"] : n >= 4 ? ["✨", "🌟", "⭐"] : n >= 3 ? ["✨", "⭐"] : ["✨", "✦"];
  const cnt = SPARK_CNT[n] || 3;                    // n=2→3 … n=5→13개
  const tw = (1.5 - 0.16 * n).toFixed(2);           // 트윙클 주기: n↑ → 짧아짐(더 자주)
  const dlySpan = Math.max(0.5, 1.4 - 0.18 * n);    // 딜레이 폭: n↑ → 좁아짐(더 밀집해서 반짝)
  // ★성능: 입자 전체를 문자열로 만들어 mshine 을 한 번에 삽입(개별 appendChild 리플로우 제거).
  //  일회성(2회 트윙클 후 투명 settle)·주기·딜레이는 인라인 스타일로 유지.
  // 큰 화면(--sc>1)에선 셀이 커진 만큼 입자 크기도 같은 배율로 키워 비율 유지(위치는 % 라 자동 추종).
  const sc = parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--sc")) || 1;
  let html = "";
  for (let i = 0; i < cnt; i++) {
    const left = (8 + Math.random() * 78).toFixed(0), top = (34 + Math.random() * 28).toFixed(0);   // 중앙 밴드(대략 34~62%)로 한정 — 이웃 칸까지 번지지 않게
    const fs = ((9 + n + Math.random() * 4) * sc).toFixed(0), dly = (Math.random() * dlySpan).toFixed(2);
    html += `<div class="mspark" style="left:${left}%;top:${top}%;font-size:${fs}px;animation-duration:${tw}s;animation-delay:${dly}s;animation-iteration-count:2;animation-fill-mode:both">${set[i % set.length]}</div>`;
  }
  const shine = document.createElement("div"); shine.className = "mshine"; shine.innerHTML = html;
  el.appendChild(shine);
}
async function blastBomb(reelEls, bomb, finalCells) {
  const bombIdxs = bomb.idxs || (bomb.idx != null ? [bomb.idx] : []);   // 다중 폭탄(idxs) 호환 + 구 단일(idx) 폴백
  [...bombIdxs, ...bomb.removed].forEach((j) => reelEls[j] && reelEls[j].classList.add("blast"));
  bombIdxs.forEach((j) => spawnExplosion(reelEls[j])); bomb.removed.forEach((j) => spawnExplosion(reelEls[j]));
  shake("sm"); snd.sfx("bomb");
  await sleep(440);
  bomb.removed.forEach((j) => { const el = reelEls[j]; if (el) { el.classList.remove("blast"); el.classList.add("empty"); setCenterCell(el, finalCells[j]); } });
  bombIdxs.forEach((j) => reelEls[j] && reelEls[j].classList.remove("blast"));
  await sleep(150);
}
function spawnExplosion(el) {
  if (!el) return;
  const b = document.createElement("div"); b.className = "boom"; b.innerHTML = `<div class="core">💥</div>`;
  for (let i = 0; i < 9; i++) { const p = document.createElement("div"); p.className = "pt"; const a = (Math.PI * 2 * i) / 9; const d = 24 + Math.random() * 16;
    p.style.setProperty("--dx", (Math.cos(a) * d).toFixed(1) + "px"); p.style.setProperty("--dy", (Math.sin(a) * d).toFixed(1) + "px"); b.appendChild(p); }
  el.appendChild(b); setTimeout(() => b.remove(), 580);
}
function jackpotFx(symId) {
  const e = D.SYM_BY_ID[symId]?.e || "🎰";
  const o = document.createElement("div"); o.className = "jackpot-fx";
  o.innerHTML = `<div class="rays"></div><div class="jptxt">${e}<span>JACKPOT!</span></div>`;
  document.body.appendChild(o); setTimeout(() => o.remove(), 1750);
}
function floatExp(amount, jp) {
  const f = document.createElement("div"); f.className = "float-exp" + (jp ? " jp" : ""); f.textContent = `+${amount} EXP`;
  document.body.appendChild(f); setTimeout(() => f.remove(), 1050);
}
function celebratePerk(p) {
  if (!p) return;
  snd.sfx("perk");
  const o = document.createElement("div"); o.className = "celebrate c-" + (p.t || "GOLD");
  o.innerHTML = `<div class="cel-burst"></div><div class="cel-ico">${p.e}</div><div class="cel-name">${esc(p.n)}</div><div class="cel-tier">${esc(p.t || "")}</div><div class="cel-desc">${esc(p.d || "")}</div>`;
  document.body.appendChild(o);
  setTimeout(() => { o.style.transition = "opacity .4s"; o.style.opacity = "0"; setTimeout(() => o.remove(), 420); }, 1000);
}
// 카드에서 터지는 별 파티클 (등급별 색/이모지)
function spawnSparkle(el, tier) {
  if (!el) return;
  const set = tier === "PRISM" ? ["✨", "🌟", "💫", "🌈", "⭐"] : tier === "GOLD" ? ["✨", "⭐", "💛", "🌟"] : ["✨", "⭐", "🤍"];
  const n = tier === "PRISM" ? 16 : tier === "GOLD" ? 13 : 10;
  for (let i = 0; i < n; i++) {
    const s = document.createElement("div"); s.className = "spark"; s.textContent = set[i % set.length];
    s.style.left = "50%"; s.style.top = "42%";
    const a = (Math.PI * 2 * i) / n + Math.random() * 0.6; const d = 46 + Math.random() * 60;
    s.style.setProperty("--dx", (Math.cos(a) * d).toFixed(1) + "px");
    s.style.setProperty("--dy", (Math.sin(a) * d).toFixed(1) + "px");
    s.style.fontSize = (12 + Math.random() * 8).toFixed(0) + "px";
    el.appendChild(s); setTimeout(() => s.remove(), 660);
  }
}

// ── 렌더 디스패치 ──
function render() {
  uiManip = null;
  // 플레이 화면(HUD 내 ⚙️ 있음)만 고정 기어 숨김 — 그 외 모든 화면은 우상단에 표시
  gearShow(!(st && (st.phase === PHASE.SPIN || st.phase === PHASE.POST_SPIN)));
  if (!st) { renderHome(); return; }
  switch (st.phase) {
    case PHASE.SELECT_CHAR: renderSelect("char"); break;
    case PHASE.SELECT_MACHINE: renderSelect("mac"); break;
    case PHASE.SELECT_DEVICE: renderSelect("dev"); break;
    case PHASE.SPIN: case PHASE.POST_SPIN: renderPlay(); break;
    case PHASE.STAGE_CLEAR: renderStageClear(); break;
    case PHASE.NODE_SELECT: renderNode(); break;
    case PHASE.PERK_PICK: renderPerk(); break;
    case PHASE.SHOP: renderShop(); break;
    case PHASE.DEVICE_NODE: renderDeviceNode(); break;
    case PHASE.REWARD_DONE: renderRewardDone(); break;
    case PHASE.RUN_END: renderEnd(); break;
  }
  tutLive();
}

// ── 인트로 (타이틀 스플래시) ──
function renderIntro() {
  snd.bgmStop();
  gearShow(false);   // 인트로에서는 숨김
  const p = g.profile;
  app.innerHTML = `
    <div class="intro">
      <div class="intro-reels"><span>🍒</span><span>7️⃣</span><span>💎</span></div>
      <h1 class="intro-title">🎰 <span class="g">잭팟 슬롯</span></h1>
      <div class="intro-sub">텍스트 로그라이크 슬롯머신 · 웹 단독판</div>
      ${p.runs ? `<div class="intro-best">${esc(titleOf(p.bestScore))} · 최고 ${p.bestScore.toLocaleString()}점 · ${p.runs}런</div>` : `<div class="intro-best">버튼·애니로 즐기는 슬롯 로그라이크!</div>`}
      <button class="intro-start" data-act="introStart">▶ 탭하여 시작</button>
      <div class="intro-hint">🔊 소리 ON · 첫 탭에서 활성화돼요</div>
    </div>`;
  const SYM = ["🍒", "🔔", "💎", "7️⃣", "⭐", "🍀", "💰", "📘", "👑", "🎰"];
  const reels = [...app.querySelectorAll(".intro-reels span")];
  if (introTimer) clearInterval(introTimer);
  introTimer = setInterval(() => { reels.forEach((s) => { s.textContent = SYM[Math.floor(Math.random() * SYM.length)]; }); }, 110);
}
function exitIntro() {
  if (introTimer) { clearInterval(introTimer); introTimer = null; }
  snd.resume(); st = null;
  // 첫 실행(로그인 안 됨 + 이전에 선택 안 함) → 로그인 게이트. 그 외(로그인됨/이미 선택) → 홈.
  if (!authUser && localStorage.getItem("slotweb_seenlogin") !== "1") renderLoginGate();
  else render();
}
// ── 로그인 게이트 (인트로 직후 첫 1회) — 구글 로그인 / 게스트로 이용하기 ──
function renderLoginGate() {
  snd.bgmStop(); gearShow(false);
  const p = g.profile;
  app.innerHTML = `
    <div class="intro login-gate" id="login-gate">
      <div class="intro-reels"><span>🍒</span><span>7️⃣</span><span>💎</span></div>
      <h1 class="intro-title">🎰 <span class="g">잭팟 슬롯</span></h1>
      <div class="intro-sub">${p.runs ? `${esc(titleOf(p.bestScore))} · 최고 ${p.bestScore.toLocaleString()}점` : "시작 방법을 선택하세요"}</div>
      <div class="gate-btns">
        <button class="gate-btn google" data-act="login"><span class="gg">G</span> Google로 로그인</button>
        <button class="gate-btn guest" data-act="gateGuest">👤 게스트로 이용하기</button>
      </div>
      <div class="gate-note">로그인하면 랭킹 기록이 계정에 저장돼 <b>기기를 바꿔도 유지</b>돼요.<br>게스트도 언제든 <b>설정 ⚙️</b>에서 로그인할 수 있어요.</div>
    </div>`;
}

// ── 홈 ──
// 심화모드(심볼 주머니 덱빌딩) 선택기 — 해금 없음(항상 노출). ★"심화 학기(승천)"와는 별개 시스템.
function deepSelector() {
  return `<div class="deep-sel${selDeep ? " on" : ""}">
    <div class="deep-head">🎒 게임 모드</div>
    <div class="deep-modes">
      <button class="deep-mode${selDeep ? "" : " sel"}" data-act="deepToggle" ${selDeep ? "" : 'aria-pressed="true"'}>
        <div class="dm-ico">🎰</div><div class="dm-nm">일반</div><div class="dm-ds">고정 확률 (기본)</div></button>
      <button class="deep-mode${selDeep ? " sel" : ""}" data-act="deepToggle" ${selDeep ? 'aria-pressed="true"' : ""}>
        <div class="dm-ico">🎒</div><div class="dm-nm">심화 · 심볼 덱</div><div class="dm-ds">주머니 확률·덱빌딩</div></button>
    </div>
    ${selDeep ? `<div class="deep-hint">스테이지마다 <b>심볼 보상</b>으로 주머니(확률 덱)를 다듬어요. 주머니를 <b>압축</b>하면 원하는 심볼 확률↑ 대신 요구 EXP가 올라가요.<br><span class="dim">심화모드는 확률 소스가 달라 <b>일반 랭킹과 별도</b>예요.</span></div>` : ""}
  </div>`;
}
// 심화 학기(승천) 선택기(홈) — 졸업 1회 이상이면 노출. ★심화모드(주머니) 켜져 있으면 숨김(요구치 이중가중 방지).
function ascSelector() {
  if (selDeep) return "";
  if (!g.ascUnlocked()) return "";
  const maxA = g.maxPlayableAsc();
  selAsc = Math.max(0, Math.min(maxA, selAsc));
  const info = g.ascInfo(selAsc);
  return `<div class="asc-sel">
    <div class="asc-head">🎓 심화 학기 <span class="asc-badge${selAsc > 0 ? " on" : ""}">${selAsc === 0 ? "일반" : "심화 " + selAsc}</span></div>
    <div class="asc-ctl">
      <button class="asc-btn" data-act="ascDown"${selAsc <= 0 ? " disabled" : ""}>◀</button>
      <div class="asc-mid">
        <div class="asc-lv">${selAsc === 0 ? "일반 난이도" : `점수 보정 ×${info.scoreMul.toFixed(2)}`}</div>
        <div class="asc-rule">${selAsc === 0 ? "표준 규칙 · 점수 랭킹 반영" : (info.rule ? "이번 단계: " + esc(info.rule) : "누적 난이도 상승")}</div>
      </div>
      <button class="asc-btn" data-act="ascUp"${selAsc >= maxA ? " disabled" : ""}>▶</button>
    </div>
    <div class="asc-hint">${selAsc >= maxA && selAsc < info.max ? `다음 단계는 <b>심화 ${selAsc} 졸업</b>(스테이지 15 클리어) 후 열려요` : "승천 점수는 별도 집계 — 일반 랭킹은 안전"}</div>
  </div>`;
}
// 플레이어 레벨 카드 — 레벨 배지 + XP 진행바. clickable=true 면 클릭 시 레벨 보상 메뉴.
function lvlCard(lp, clickable = false) {
  const pct = Math.round((lp.ratio || 0) * 100);
  const attr = clickable ? ` clickable" data-act="levelRewards` : "";
  return `<div class="lvl-card${attr}">
    <div class="lvl-badge"><span class="lvl-n">Lv.${lp.level}</span></div>
    <div class="lvl-body">
      <div class="lvl-top"><span class="lvl-lbl">플레이어 레벨${clickable ? " <span class=\"lvl-more\">· 보상 보기 ›</span>" : ""}</span><span class="lvl-xp">${lp.max ? "MAX" : `${lp.inLevel.toLocaleString()} / ${lp.need.toLocaleString()} XP`}</span></div>
      <div class="lvl-bar"><div class="lvl-fill" style="width:${pct}%"></div></div>
    </div>
  </div>`;
}
function renderHome() {
  snd.bgmStop();
  const p = g.profile;
  const lp = g.playerProgress();
  app.innerHTML = `
    <div class="scr-title" style="margin-top:34px">
      <h1>🎰 <span class="g">잭팟 슬롯</span></h1>
      <div class="sub">텍스트 로그라이크 슬롯머신 · 웹 단독판</div>
    </div>
    ${lvlCard(lp, true)}
    ${deepSelector()}
    ${ascSelector()}
    <div class="hud" style="text-align:center">
      <div style="font-size:15px;font-weight:800;color:var(--gold)">${esc(titleOf(p.bestScore))}</div>
      <div class="hud-stats" style="margin-top:10px">
        <div class="s"><div class="k">최고 점수</div><div class="v">${p.bestScore.toLocaleString()}</div></div>
        <div class="s"><div class="k">최고 스테이지</div><div class="v">${p.bestStage}</div></div>
        <div class="s"><div class="k">플레이</div><div class="v">${p.runs}</div></div>
      </div>
      <div class="dim" style="font-size:calc(11.5px * var(--sc));margin-top:9px">업적 ${p.unlocked.length}/${D.ACHIEVEMENTS.length} · 장치 ${p.ownedDevices.length}/${D.DEVICES.length} 해금</div>
    </div>
    <div class="choose-foot" style="display:flex;gap:8px;flex-wrap:wrap">
      <button class="bigbtn" data-act="start" style="flex:1 1 100%">▶ 게임 시작</button>
      <button class="bigbtn ghost" data-act="rank" style="flex:1">🏆 랭킹</button>
      <button class="bigbtn ghost" data-act="dex" style="flex:1">📚 도감</button>
    </div>
    <p class="dim" style="font-size:calc(11px * var(--sc));text-align:center;margin-top:12px">캐릭터 · 슬롯머신 · 장치를 골라 스테이지를 클리어하세요.<br>증강/유물/장치로 빌드를 키워 고득점에 도전!</p>
    <div style="text-align:center;margin-top:16px"><button class="reset-link sndtog withlabel" data-act="soundToggle">${sndIcon()} 소리</button> <button class="reset-link" data-act="resetAsk">⚠️ 데이터 초기화</button></div>`;
}

// ── 선택 화면 ──
// 후반 도전 목표판 — 레벨/심화학기/졸업/숙련/후반업적 + 레벨 해금 트리
function renderLevelRewards() {
  gearShow(true);
  const lp = g.playerProgress();
  const road = g.levelUnlocks();
  const now = road.filter((o) => o.unlocked).length;
  const roadHtml = road.map((o) => `<div class="road-row${o.unlocked ? " on" : ""}"><span class="road-lv">Lv.${o.lv}</span><span class="road-lb">${esc(o.label)}</span><span class="road-ck">${o.unlocked ? "✓" : "🔒"}</span></div>`).join("");
  app.innerHTML = `<button class="backbtn" data-act="goalsback">◀ 뒤로</button>
    <div class="scr-title"><div class="step-tag">🎖️ 레벨 보상</div><h1>레벨 보상</h1><div class="sub">레벨을 올리면 후반 캐릭터·슬롯·장치·증강·유물이 해금돼요</div></div>
    ${lvlCard(lp)}
    <div class="goal-road-ttl">🔓 레벨 해금 보상 <span class="dim">${now}/${road.length}</span></div>
    <div class="goal-road">${roadHtml || '<div class="dim" style="padding:8px">해금 항목 없음</div>'}</div>`;
}
// 숙련도 별(★채움/빈) — 캐릭/슬롯/장치 카드용
function masteryStars(kind, id) {
  if (!id) return "";
  const m = g.masteryOf(kind, id);
  return `<span class="mast-stars">${"★".repeat(m.lv)}<span class="ms-empty">${"★".repeat(Math.max(0, m.total - m.lv))}</span></span>`;
}
function renderSelect(kind) {
  const stepN = kind === "char" ? "1" : kind === "mac" ? "2" : "3";
  const title = kind === "char" ? "🎭 캐릭터 선택" : kind === "mac" ? "🎰 슬롯머신 선택" : "🔧 장치 선택";
  const sub = kind === "char" ? "이번 런의 플레이 스타일을 정합니다." : kind === "mac" ? "심볼 등장 확률과 점수 보정이 달라집니다." : "보유 장치 중 하나를 장착하거나 없이 시작하세요.";
  const act = kind === "char" ? "selChar" : kind === "mac" ? "selMac" : "selDev";
  let cards = st.options.map((o) => {
    return `<button class="pcard" data-act="${act}" data-arg="${o.id}">
      <div class="ico">${o.e}</div><div class="nm">${esc(o.n)}</div><div class="ds">${esc(o.d || "")}</div><div class="pcard-mast">${masteryStars(kind, o.id)}</div></button>`;
  }).join("");
  if (kind === "dev") cards = `<button class="pcard" data-act="selDev" data-arg=""><div class="ico">🚫</div><div class="nm">장치 없이</div><div class="ds">장치를 장착하지 않고 시작</div></button>` + cards;
  app.innerHTML = `
    <div class="scr-title"><div class="step-tag">STEP ${stepN}/3</div><h1>${title}</h1><div class="sub">${sub}</div></div>
    <div class="grid">${cards}</div>`;
}

// ── 플레이 화면 ──
function buildBadge() {
  const dev = st.device ? D.DEV_BY_ID[st.device] : null;
  return `<span>✨${st.perks.filter((id) => D.AUG_BY_ID[id] || D.REL_BY_ID[id]).length}</span>${st.curses.length ? `<span>🌑${st.curses.length}</span>` : ""}${dev ? `<span>${dev.e}</span>` : ""}`;
}
function reelInner(c) { return `<span class="sym">${c.sym.e}</span>${c.tag ? `<span class="tag">${esc(c.tag)}</span>` : ""}`; }
// Unity ReelView(S13/S14) 노치 스크롤 이식 — jackpotpick/reel.js 와 동일 메커니즘의 게임판(§A).
// 이웃 장식 캐시 — 인덱스(릴 순번)별 4칸을 최초 1회만 추첨해 재사용(재렌더할 때마다 다시 뽑으면 깜빡임).
// spinReelOne 착지 후 실제 DOM 모습으로 갱신되어 다음 재렌더가 착지 상태 그대로를 복원한다.
const stripMemo = new Map();
// 셀 1개의 스트립 마크업 — center 에 실제 셀, 이웃 4칸은 무작위 이모지(정지 상태 장식, memo 캐시)
function reelStrip(c, i) {
  let nb = stripMemo.get(i);
  if (!nb) { nb = [0, 1, 2, 3].map(() => randSpinCell().sym.e); stripMemo.set(i, nb); }
  const deco = (e) => reelInner({ sym: { e }, tag: "" });
  return `<div class="rstrip">
    <div class="rslot">${deco(nb[0])}</div><div class="rslot">${deco(nb[1])}</div>
    <div class="rslot center">${reelInner(c)}</div>
    <div class="rslot">${deco(nb[2])}</div><div class="rslot">${deco(nb[3])}</div>
  </div><div class="rfade"></div>`;
}
// 중앙 슬롯 내용만 교체(스트립 구조 보존) — blastBomb/변형 공개용
function setCenterCell(reelEl, c) {
  const center = reelEl.querySelector(".rslot.center");
  if (center) center.innerHTML = reelInner(c);
}
function renderPlay() {
  if (st.stage !== curStage) { lastGain = null; curStage = st.stage; if (st.boss) snd.sfx("boss"); }
  if (soundOn) snd.bgmStart();
  const cells = st.lastCells || Array.from({ length: g.reel() }, () => ({ sym: { e: "❔" }, tag: "" }));
  const boss = st.boss;
  app.innerHTML = `
    <div class="hud">
      <div class="hud-top">
        <span class="stage-badge">STAGE ${st.stage}</span>
        ${boss ? `<span class="boss-badge">${boss.e} ${esc(boss.n)}</span>` : ""}
        ${st.asc > 0 ? `<span class="asc-hud">🎓 심화 ${st.asc} <b>×${(st.ascScoreMul || 1).toFixed(2)}</b></span>` : ""}
        ${st.deepMode ? `<span class="deep-hud" data-act="pouch">🎒${st.pouchView ? `<b>${st.pouchView.total}</b>/${st.pouchView.totalMax ?? D.DEEP.DECK_MAX}` : "덱"}${st.pouchView && st.pouchView.penalty > 1 ? ` <span class="dh-pen">+${Math.round((st.pouchView.penalty - 1) * 100)}%</span>` : ""}${archHudChip(st.pouchView)}</span>` : ""}
        ${st.deepMode ? feverHudHtml(st) : ""}
        <button class="hud-help hud-gear" data-act="settings" style="margin-left:auto">⚙️</button>
        <button class="hud-help" data-act="tut" style="margin-left:6px">❓</button>
        <span class="hud-build" data-act="status" style="margin-left:6px">${buildBadge()} <span style="color:var(--dim)">›</span></span>
      </div>
      <div class="expbar-wrap"><div class="expbar" id="expbar"></div><div class="expbar-txt" id="expbar-txt"></div></div>
      <div class="hud-stats">
        <div class="s"><div class="k">스핀</div><div class="v" id="spins-v"></div></div>
        <div class="s"><div class="k">점수</div><div class="v" id="score-v"></div></div>
        <div class="s coin-s"><div class="k">코인</div><div class="v coin" id="coins-v"></div></div>
      </div>
      <div class="unlucky" id="unlucky"></div>
    </div>
    ${boss ? `<div class="boss-rule"><b>보스 룰</b> · ${esc(boss.desc)}</div>` : ""}
    <div class="reels" id="reels">${cells.map((c, i) => `<div class="reel" data-act="cellinfo" data-arg="${i}">${reelStrip(c, i)}</div>`).join("")}</div>
    <div class="gain" id="gain"></div>
    <div class="logbox" id="logbox"></div>
    <div class="actionbar">
      <div class="ab-extra" id="ab-extra"></div>
      <div class="ab-main">
        <button class="spinbtn" id="spinbtn" data-act="spin" data-arg="N">🎰 슬롯 돌리기</button>
        <span id="abicons" style="display:flex;gap:8px">
          <div class="iconwrap"><button class="iconbtn" data-act="item">🎒<span class="lbl">아이템</span></button>${st.items.length ? `<span class="cnt">${st.items.length}</span>` : ""}</div>
          <button class="iconbtn" data-act="deviceSheet">🔧<span class="lbl">장치</span></button>
          ${st.deepMode ? `<button class="iconbtn" data-act="pouch">👝<span class="lbl">주머니</span></button>` : ""}
          <button class="iconbtn" data-act="status">📊<span class="lbl">상태</span></button>
        </span>
      </div>
    </div>`;
  updateHud(); showGain(); updateExtras(); renderLog();
  if (!g.profile.tutDone && !tut.active && st.stage === 1 && st.spinIndex === 0)
    setTimeout(() => { if (st && st.phase === PHASE.SPIN && !tut.active) startTutorial(); }, 420);
  if (tut.active && tut.live && st.stage >= 2) endTutorial();   // 2스테이지 진입 = 튜토리얼 완료(폴백)
}
function renderLog() {
  const box = $("logbox"); if (!box) return;
  box.innerHTML = g.log.slice(-8).reverse().map((l) => `<div class="l">${esc(l)}</div>`).join("");
}
function setReels(cells) {
  const box = $("reels"); if (!box) return;
  box.innerHTML = cells.map((c, i) => `<div class="reel settle" data-act="cellinfo" data-arg="${i}">${reelStrip(c, i)}</div>`).join("");
}
function updateHud() {
  if (!$("expbar")) return;
  const pct = Math.min(100, st.quota ? (st.stageExp / st.quota) * 100 : 0);
  const bar = $("expbar"); bar.style.width = pct + "%"; bar.classList.toggle("over", st.stageExp >= st.quota);
  $("expbar-txt").textContent = `EXP ${Math.floor(st.stageExp)} / ${st.quota}`;
  $("spins-v").textContent = `${Math.min(st.spinIndex, st.spins)} / ${st.spins}`;
  $("score-v").textContent = st.score.toLocaleString();
  $("coins-v").textContent = st.coins;
  const ug = $("unlucky"); if (ug) ug.innerHTML = `불운 게이지 <span class="dots">${"●".repeat(st.unluckyGauge)}${"○".repeat(D.C.UNLUCKY_MAX - st.unluckyGauge)}</span> <span class="dim">(가득차면 다음 보상 희귀↑)</span>`;
  // §9.1 J2: 피버 게이지 바 동적 갱신 — fever-fill 이 DOM 에 존재할 때만(피버 대기 중인 경우).
  //  피버 진행 중은 fever-active 칩으로 대체 렌더이므로 fever-fill 엘리먼트가 없음(그냥 스킵).
  if (st.deepMode) {
    const ff = $("fever-fill");
    if (ff) {
      const fmax = (D.DEEP && D.DEEP.FEVER_MAX) ? D.DEEP.FEVER_MAX : 100;
      const fpct = Math.min(100, Math.round((st.feverGauge || 0) / fmax * 100));
      ff.style.width = fpct + "%";
      // 레이블 갱신(fever-label 은 fever-fill 의 형제)
      const fl = ff.parentElement && ff.parentElement.nextElementSibling;
      if (fl && fl.classList.contains("fever-label")) fl.textContent = `🔥${fpct}%`;
    }
  }
  renderLog();
}
function showGain() {
  const el = $("gain"); if (!el) return;
  if (!lastGain) { el.innerHTML = `<span class="dim" style="font-size:12.5px">🎰 슬롯을 돌려 EXP를 모으세요</span>`; return; }
  el.innerHTML =
    (lastGain.banner ? `<div class="spin-banner pop">${esc(lastGain.banner)}</div>` : "") +
    `<div class="big pop">+${lastGain.gained} EXP</div>` +
    (lastGain.notes && lastGain.notes.length ? `<div class="notes">${lastGain.notes.map((n) => `<span class="note">${esc(n)}</span>`).join("")}</div>` : "") +
    setBonusExplain(lastGain.bestCount, lastGain.setGroups);
  const big = el.querySelector(".big"); if (big) { big.classList.remove("pop"); void big.offsetWidth; big.classList.add("pop"); }
}
// 같은 심볼 2개 이상 모였을 때 "왜 점수가 높은지" 쉬운 설명(표시 전용 — 점수 계산 미접촉).
// SET_EXP=[0,0,8,18,42,100] → 많이 모을수록 보너스가 급격히 커진다는 원리를 안내.
function setBonusExplain(bestCount, setGroups) {
  const n = bestCount || 0;
  if (n < 2 && !(setGroups > 0)) return "";
  const cur = (n >= 2 && D.SET_EXP[Math.min(n, D.SET_EXP.length - 1)]) || D.SET_EXP[2];
  const tip = n >= 5 ? `같은 그림 ${n}개 풀세트! 🎉 세트 보너스가 최대로 터졌어요`
    : `같은 그림 ${n}개 모음! 세트 보너스 +${cur} EXP`;
  return `<div class="set-explain">🧩 <b>${esc(tip)}</b>
    <span class="se-scale">많이 모을수록 점수 ↑ &nbsp;<span class="se-steps">2개+8 · 3개+18 · 4개+42 · 5개+100</span></span></div>`;
}
function updateExtras() {
  const box = $("ab-extra"); if (!box) return;
  const post = st.phase === PHASE.POST_SPIN;
  const spinBtn = $("spinbtn");
  let html = "";
  if (post) {
    if (spinBtn) { spinBtn.disabled = true; spinBtn.textContent = "스핀 소진 — 마지막 기회"; }
    const canG = st.charId === "gambler" && !st.usedCmds.includes("GREROLL");
    const canDevR = st.device === "dev_reroll" && !st.usedCmds.includes("재굴림");
    if (canG || canDevR) html += `<button class="abtn dev" data-act="manipN" data-arg="재굴림:0">🔄 재굴림</button>`;
    if (st.device === "dev_bell" && st.quota - st.stageExp <= 25) html += `<button class="abtn dev" data-act="dev" data-arg="비상">🔔 비상졸업</button>`;
    html += giveUpBtn();
    box.innerHTML = html; return;
  }
  if (spinBtn) { spinBtn.disabled = busy; spinBtn.textContent = "🎰 슬롯 돌리기"; }
  // 특수 스핀 (종류별 스테이지당 1회 — 하나 써도 나머지는 남음). 코인 비용·부족 시 비활성 표시.
  const cmdBtn = (mode, label) => {
    const free = !!(st.cmdFreeUsed && st.cmdFreeUsed[mode] === false);   // 종류별 런 첫 사용 = 무료
    const cost = g.cmdCost(mode);
    const tag = free ? `<span class="cmd-cost free">무료</span>` : (cost > 0 ? `<span class="cmd-cost">${cost}🪙</span>` : "");
    const poor = !free && cost > 0 && st.coins < cost;   // 무료면 코인 0이어도 활성
    return `<button class="abtn special${poor ? " short" : ""}" data-act="spin" data-arg="${mode}"${poor ? " disabled" : ""}>${label}${tag}</button>`;
  };
  if (!st.usedCmds.includes("FOCUS")) html += cmdBtn("FOCUS", "🎯 집중");
  if (!st.usedCmds.includes("ALLIN")) html += cmdBtn("ALLIN", "🎰 올인");
  if (!st.usedCmds.includes("PRAY")) html += cmdBtn("PRAY", "🙏 기도");
  if (st.spinIndex === st.spins - 1 && !st.usedCmds.includes("LAST")) html += cmdBtn("LAST", "⏰ 최후");
  // 장치 액션
  const dev = st.device ? D.DEV_BY_ID[st.device] : null;
  if (dev) {
    if (st.device === "dev_coin" && st.coins >= 5 && !st.usedCmds.includes("투입")) html += `<button class="abtn dev" data-act="dev" data-arg="투입">🪙 투입(-5)</button>`;
    if (st.device === "dev_oracle" && !st.usedCmds.includes("예언")) html += `<button class="abtn dev" data-act="dev" data-arg="예언">🔮 예언</button>`;
    if (st.device === "dev_bell" && st.quota - st.stageExp <= 25) html += `<button class="abtn dev" data-act="dev" data-arg="비상">🔔 비상</button>`;
    if (st.lastCells) {
      if ((st.device === "dev_reroll" && !st.usedCmds.includes("재굴림")) || (st.charId === "gambler" && !st.usedCmds.includes("GREROLL")))
        html += `<button class="abtn dev" data-act="manipN" data-arg="재굴림:0">🔄 재굴림</button>`;
      if (st.device === "dev_pin" && !st.usedCmds.includes("고정")) html += manipBtn("고정", "📌");
      if (st.device === "dev_copy" && !st.usedCmds.includes("복사")) html += manipBtn("복사", "📑");
      if (st.device === "dev_swap" && !st.usedCmds.includes("교체")) html += manipBtn("교체", "🔃");
      // ── Phase 5 심볼 능동장치(심화 전용) — 지휘=칸 선택, 정화/셔플=즉시. usedCmds 스테이지당 1회. ──
      if (st.deepMode) {
        if (st.device === "dev_baton" && !st.usedCmds.includes("지휘")) html += manipBtn("지휘", "🎯");
        if (st.device === "dev_purify_glove" && !st.usedCmds.includes("정화")) html += `<button class="abtn dev" data-act="manipN" data-arg="정화:0">🧤 정화</button>`;
        if (st.device === "dev_pouch_shuffler" && !st.usedCmds.includes("셔플")) html += `<button class="abtn dev" data-act="manipN" data-arg="셔플:0">🔀 셔플</button>`;
      }
    }
  }
  // gambler 무료 재굴림 (장치 없을 때도)
  if (st.lastCells && st.charId === "gambler" && !st.usedCmds.includes("GREROLL") && st.device !== "dev_reroll")
    html += `<button class="abtn dev" data-act="manipN" data-arg="재굴림:0">🎲 무료재굴림</button>`;
  // 고정/복사/교체 칸 선택 중
  if (uiManip) {
    const n = g.reel();
    html += `<div style="flex:1 1 100%;font-size:11.5px;color:var(--dim);margin-top:2px">${uiManip} 할 칸 선택:</div>`;
    for (let i = 1; i <= n; i++) html += `<button class="abtn" data-act="manipN" data-arg="${uiManip}:${i}">${i}번</button>`;
  } else {
    // 게임 포기(즉시 결산) — 스핀 중 언제든. 오작동 방지: 작게·구석(맨 뒤 줄바꿈)·별색.
    html += giveUpBtn();
  }
  box.innerHTML = html;
}
function manipBtn(cmd, e) { return `<button class="abtn dev" data-act="manipPick" data-arg="${cmd}">${e} ${cmd}</button>`; }
// 즉시 결산 버튼 — 주 버튼(스핀)과 구분되게 작고 흐리게, 별도 줄(flex-basis 100%)로 구석 배치.
function giveUpBtn() { return `<button class="abtn giveup" data-act="giveUpAsk">🏳️ 게임 포기 (즉시 결산)</button>`; }

// ── 바텀시트 ──
function openSheet(html) { sheetRoot.innerHTML = `<div class="sheet-bg" data-act="closeSheet"><div class="sheet" data-act="noop"><div class="grab"></div>${html}</div></div>`; }
function closeSheet() { sheetRoot.innerHTML = ""; repairSel = { serviceId: null, sel: {} }; }

// 게임 포기(즉시 결산) 확인 — 오작동 방지 1회 확인(앱 톤의 커스텀 시트). 스핀 애니 중엔 무시.
function giveUpAsk() {
  if (busy) return;
  if (!st || ![PHASE.SPIN, PHASE.POST_SPIN].includes(st.phase)) return;
  openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 취소</button><h3>🏳️ 게임 포기</h3>
    <div class="buy-confirm">지금까지 모은 <b>점수 ${st.score.toLocaleString()}점</b> · <b>스테이지 ${st.stage}</b> 로 런을 <b>즉시 종료·결산</b>할까요?<br>
      <span class="dim" style="font-size:11.5px">획득한 점수·업적·경험치는 그대로 반영되고 최고 기록/랭킹에도 등록돼요.</span></div>
    <div class="confirm-row"><button class="bigbtn ghost" data-act="closeSheet">계속 플레이</button><button class="bigbtn danger-btn" data-act="giveUpConfirm">🏁 결산하기</button></div>`);
}

// ── 사운드 on/off + 아이콘 동기화 ──
function setSound(on) {
  soundOn = !!on; snd.setEnabled(soundOn); localStorage.setItem("slotweb_sound", soundOn ? "1" : "0");
  if (soundOn) { snd.resume(); snd.sfx("coin"); if (st && st.phase) snd.bgmStart(); } else snd.bgmStop();
  syncSndIcons();
}
function syncSndIcons() { document.querySelectorAll(".sndtog").forEach((b) => { b.textContent = b.classList.contains("withlabel") ? sndIcon() + " 소리" : sndIcon(); }); }

// ── 설정 창(현재는 소리 조절만) ──
function openSettings() {
  const volPct = Math.round(snd.getVolume() * 100);
  openSheet(`
    <button class="sheet-close" data-act="closeSheet">◀ 닫기</button>
    <h3>⚙️ 설정</h3>
    <div class="set-sec">
      <div class="set-row acc" id="acc-row">${accountRowHtml()}</div>
      <div class="set-row">
        <span class="set-lbl">🔊 소리</span>
        <button class="set-tog${soundOn ? " on" : ""}" data-act="setMute">${soundOn ? "켜짐" : "꺼짐"}</button>
      </div>
      <div class="set-row vol${soundOn ? "" : " disabled"}">
        <span class="set-lbl">🎚️ 볼륨</span>
        <input id="volrange" class="set-range" type="range" min="0" max="100" value="${volPct}" data-act="noop" />
        <span class="set-vol" id="volval">${volPct}%</span>
      </div>
      <div class="set-row">
        <span class="set-lbl">📳 진동</span>
        <button class="set-tog${vibeOn ? " on" : ""}" data-act="setVibe">${vibeOn ? "켜짐" : "꺼짐"}</button>
      </div>
      <div class="set-hint">🎚️ 볼륨을 움직이면 예시음이 들려요. 위쪽 [소리]로 전체 음소거, [진동]으로 터치 진동을 끌 수 있어요.</div>
    </div>`);
  const r = $("volrange");
  if (r) {
    r.addEventListener("input", () => { const v = (+r.value) / 100; snd.setVolume(v); localStorage.setItem("slotweb_vol", String(v)); const vv = $("volval"); if (vv) vv.textContent = r.value + "%"; });
    r.addEventListener("change", () => { snd.resume(); if (soundOn) snd.sfx("coin"); });
  }
}
function openItemSheet() {
  const items = st.items.map((id, i) => { const it = D.ITEM_BY_ID[id]; const kindTxt = it.k === "NEXTSPIN" ? "다음 스핀" : it.k === "PHASE" ? "이번 스테이지" : "즉시"; return `
    <div class="icard"><div class="ie">${it.e}</div><div class="ib"><div class="iname">${esc(it.n)} <span class="dim" style="font-size:10px">· ${kindTxt}</span></div><div class="idesc">${esc(it.d)}</div></div>
      <button class="iuse" data-act="useItem" data-arg="${i}">사용</button></div>`; }).join("");
  openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 닫기</button><h3>🎒 아이템</h3><div class="sub">보유 ${st.items.length}/3 · 장전한 효과는 스핀에 적용돼요.</div>
    <div class="ilist">${items || `<div class="icard empty">보유한 아이템이 없어요. 상점/이벤트에서 얻을 수 있어요.</div>`}</div>
    ${st.armItems.length ? `<div class="sub" style="margin-top:10px">🔋 장전됨: ${st.armItems.map((id) => D.ITEM_BY_ID[id]?.e || "·").join(" ")}</div>` : ""}`);
}
function openDeviceSheet() {
  const dev = st.device ? D.DEV_BY_ID[st.device] : null;
  let body;
  if (!dev) body = `<div class="icard empty">장착한 장치가 없어요.</div>`;
  else {
    const usable = devUsableNote();
    body = `<div class="icard"><div class="ie">${dev.e}</div><div class="ib"><div class="iname">${esc(dev.n)} <span class="dim" style="font-size:10px">· ${dev.kind === "PASSIVE" ? "패시브(상시)" : "능동"}</span></div><div class="idesc">${esc(dev.d)}</div><div class="idesc" style="color:var(--teal)">${usable}</div></div></div>`;
  }
  const owned = g.profile.ownedDevices.filter((id) => id !== st.device).map((id) => { const d = D.DEV_BY_ID[id]; return `<div class="icard" style="opacity:.7"><div class="ie">${d.e}</div><div class="ib"><div class="iname">${esc(d.n)}</div><div class="idesc">${esc(d.d)} · <span class="dim">보유(런 시작 시 장착 선택)</span></div></div></div>`; }).join("");
  openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 닫기</button><h3>🔧 장치</h3><div class="sub">능동 장치는 하단 액션바 버튼으로 발동해요. 패시브는 매 스핀 자동.</div>
    <div class="ilist">${body}${owned}</div>`);
}
function devUsableNote() {
  if (st.device === "dev_coin") return st.usedCmds.includes("투입") ? "이번 스테이지 사용 완료" : (st.coins >= 5 ? "🪙 투입 가능 (액션바)" : "코인 5 필요");
  if (["dev_reroll", "dev_pin", "dev_copy", "dev_swap"].includes(st.device)) return st.usedCmds.includes(D.DEV_BY_ID[st.device].cmd) ? "이번 스테이지 사용 완료" : "직전 스핀 후 액션바에서 발동";
  if (st.device === "dev_oracle") return st.usedCmds.includes("예언") ? "사용 완료" : "🔮 예언 가능 (액션바)";
  if (st.device === "dev_bell") return "부족 EXP ≤25일 때 액션바에 [비상] 표시";
  // ── Phase 5 심볼 능동장치(심화 전용) ──
  if (["dev_baton", "dev_purify_glove", "dev_pouch_shuffler"].includes(st.device)) {
    if (!st.deepMode) return "심화모드 전용 — 일반 런에서는 효과 없음";
    return st.usedCmds.includes(D.DEV_BY_ID[st.device].cmd) ? "이번 스테이지 사용 완료" : "직전 스핀 후 액션바에서 발동";
  }
  // 심화 표시형(PEEK)·심화 패시브 — 심화모드에서만 동작.
  if (["dev_tag_scanner", "dev_rare_detector"].includes(st.device)) return st.deepMode ? "표시형 — 주머니 시트/보상에 정보 표시" : "심화모드 전용";
  if (["dev_compress_gauge", "dev_expand_scale", "dev_call_bell", "dev_legend_seal"].includes(st.device)) return st.deepMode ? "심화 패시브 — 주머니 조건에 따라 자동 적용" : "심화모드 전용 — 일반 런에서는 효과 없음";
  return "장착 시 매 스핀 자동 적용";
}
function openStatusSheet() {
  const ch = D.CHAR_BY_ID[st.charId], mc = D.MAC_BY_ID[st.machineId];
  const perkRow = (id) => { const p = D.PERK_BY_ID[id]; return p ? `<div class="icard"><div class="ie">${p.e}</div><div class="ib"><div class="iname">${esc(p.n)} <span class="tier t-${p.t}" style="position:static;font-size:calc(9px * var(--sc))">${p.t}</span></div><div class="idesc">${esc(st.deepMode && p.dDesc ? p.dDesc : p.d)}</div></div></div>` : ""; };
  const augs = st.perks.filter((id) => D.AUG_BY_ID[id]); const rels = st.perks.filter((id) => D.REL_BY_ID[id]);
  const sets = []; // 활성 세트 표시
  openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 닫기</button><h3>📊 상태</h3>
    <div class="sub">${ch?.e}${esc(ch?.n || "")} + ${mc?.e}${esc(mc?.n || "")}${st.device ? " + " + D.DEV_BY_ID[st.device].e + esc(D.DEV_BY_ID[st.device].n) : ""}</div>
    <div class="hud-stats" style="margin-bottom:12px"><div class="s"><div class="k">점수</div><div class="v">${st.score.toLocaleString()}</div></div><div class="s"><div class="k">코인</div><div class="v coin">${st.coins}</div></div><div class="s"><div class="k">스테이지</div><div class="v">${st.stage}</div></div></div>
    ${augs.length ? `<div class="sub">✨ 증강 ${augs.length}</div><div class="ilist" style="margin-bottom:10px">${augs.map(perkRow).join("")}</div>` : ""}
    ${rels.length ? `<div class="sub">🛡️ 유물 ${rels.length}</div><div class="ilist" style="margin-bottom:10px">${rels.map(perkRow).join("")}</div>` : ""}
    ${st.curses.length ? `<div class="sub">🌑 저주 ${st.curses.length}</div><div class="ilist">${st.curses.map(perkRow).join("")}</div>` : ""}
    ${!augs.length && !rels.length && !st.curses.length ? `<div class="icard empty">아직 획득한 증강·유물이 없어요.</div>` : ""}`);
}

// ── 슬롯 칸 상세 분해 시트 ──
function openCellSheet(idx) {
  const info = g.cellInfo(idx); if (!info) return;
  const s = info.sym;
  const row = (label, val, cls) => `<div class="cb-row${cls ? " " + cls : ""}"><span>${esc(label)}</span><b>${esc(val)}</b></div>`;
  let calc = "";
  if (info.empty) calc = row("이 칸", "EXP 0 (제거됨)", "total");
  else {
    calc += row("기본 EXP", info.baseExp);
    if (info.perSymExp) calc += row(`심볼 보너스 ${s.e}`, (info.perSymExp > 0 ? "+" : "") + info.perSymExp);
    info.tagBonuses.forEach((t) => { calc += row(`태그 보너스 #${t.t}`, "+" + t.v); });
    if (info.skullExp) calc += row("해골 EXP", (info.skullExp > 0 ? "+" : "") + info.skullExp);
    if (info.centerMul !== 1) calc += row("가운데 칸 배수", "×" + (Math.round(info.centerMul * 100) / 100));
    calc += row("= 이 칸 EXP", info.cellExp, "total");
  }
  let score = "";
  if (info.cellScore) {
    score += row("기본 점수", info.baseScore);
    if (info.perSymScore) score += row("심볼 점수 보너스", "+" + info.perSymScore);
    if (info.skullScore) score += row("해골 점수", "+" + info.skullScore);
    score += row("= 이 칸 점수", info.cellScore, "total");
  }
  const tags = s.tags.length ? `<div class="cb-tags">${s.tags.map((t) => `<span class="cbtag">#${esc(t)}</span>`).join("")}</div>` : "";
  const specials = info.specials.length ? `<div class="cb-sp">${info.specials.map((x) => `<div>▸ ${esc(x)}</div>`).join("")}</div>` : "";
  const affect = info.affecting.length
    ? info.affecting.map((a) => `<div class="icard"><div class="ie">${a.e}</div><div class="ib"><div class="iname">${esc(a.n)} <span class="dim" style="font-size:10px">${a.tier ? `<span class="tier t-${a.tier}" style="font-size:9px">${a.tier}</span> ` : ""}${esc(a.kind)}</span></div><div class="idesc">${esc(a.effect)}</div></div></div>`).join("")
    : `<div class="icard empty">이 칸에 직접 영향 주는 항목이 아직 없어요.</div>`;
  const sets = info.sets.length ? `<div class="sub" style="margin-top:12px">🎰 활성 세트 효과</div><div class="ilist">${info.sets.map((x) => `<div class="icard"><div class="ie">🎰</div><div class="ib"><div class="iname">${esc(x.n)}</div><div class="idesc">${esc(x.d)}</div></div></div>`).join("")}</div>` : "";
  const pos = info.isCenter ? " · 가운데 칸" : info.idx === 0 ? " · 첫 칸" : "";
  // §6.3 심화모드 심볼 티어 뱃지 — pouchInfo 존재(주머니 심볼)일 때 노출.
  const cellTierBadge = (info.pouchInfo && info.pouchInfo.total > 0 && s.id && s.id !== "empty" && s.id !== "random")
    ? ` <span class="tier t-${symTierOf(s.id)}" style="font-size:calc(9px * var(--sc))">${symTierOf(s.id)}</span>` : "";
  // §4 V3P3: 심화 주머니 심볼일 때 소멸대상/제외 한 줄 표기.
  let cellDecayNote = "";
  if (info.pouchInfo && info.pouchInfo.total > 0 && s.id && s.id !== "empty" && s.id !== "random") {
    if (isAutoDecayTarget(s.id)) {
      cellDecayNote = `<div class="cb-note decay-target">🍂 자동 소멸 대상 — 15라운드부터 매 클리어 시 무작위 1개 제거됩니다</div>`;
    } else if (symCatOf(s.id) === "harmful") {
      cellDecayNote = `<div class="cb-note decay-exempt">🛡️ 자동 소멸 제외 — 해로운 심볼은 직접 정화해야 합니다</div>`;
    }
  }
  openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 닫기</button>
    <h3>${s.e} ${esc(s.n)}${cellTierBadge} <span class="dim" style="font-size:12px">${info.idx + 1}번 칸${pos}</span></h3>
    ${tags}${specials}
    <div class="cb-calc">${calc}${score}</div>
    <div class="cb-note">⚙️ 전체 배수: 모든 칸 합계에 <b>EXP ×${Math.round(info.expMul * 100) / 100}</b>${info.flatExp ? ` · 스핀마다 <b>+${info.flatExp}</b>` : ""} 가 곱·가산되어 최종 획득 EXP가 됩니다.</div>
    ${info.pouchInfo && info.pouchInfo.total > 0 ? `<div class="cb-note">🎒 주머니: 이 심볼 <b>${info.pouchInfo.count}개</b> / 총량 ${info.pouchInfo.total} (<b>${info.pouchInfo.biasChanged ? pct2(info.pouchInfo.effPct * 100, 100) : pct2(info.pouchInfo.count, info.pouchInfo.total)}%</b>)${info.pouchInfo.biasChanged ? ` <span class="bias-marker" title="증강·유물 효과로 드로우 확률 변동">🧪</span> <span class="dim">(기본 ${pct2(info.pouchInfo.count, info.pouchInfo.total)}%)</span>` : ""}</div>` : ""}
    ${cellDecayNote}
    <div class="cb-note">🧩 세트 보너스: <b>같은 그림을 2개 이상</b> 모으면 추가 EXP·점수! 많이 모을수록 보너스가 급격히 커져요 — <b>2개 +8 · 3개 +18 · 4개 +42 · 5개 +100 EXP</b>. (같은 칸이 여러 그룹 이뤄도 각각 적용)</div>
    <div class="sub" style="margin-top:13px">✨ 이 칸에 영향 주는 증강·유물·캐릭터</div>
    <div class="ilist">${affect}</div>
    ${sets}`);
}

// ── 보상 노드 ──
const NODE_INFO = {
  AUGMENT: ["✨", "증강", "강력한 증강 1개를 골라 빌드를 키웁니다."],
  RELIC: ["🛡️", "유물", "유물 1개를 골라 효과를 더합니다."],
  SHOP: ["🛒", "상점", "코인으로 증강·유물·아이템을 구매합니다."],
  REST: ["🛌", "휴식", "코인 +12를 받고 다음 스테이지로."],
  GAMBLE: ["🎲", "도박장", "50% 코인 2배 / 50% 그대로."],
  EVENT: ["🎁", "이벤트", "무작위 보상(코인·점수·아이템·장치 등)."],
  CURSE: ["🌑", "저주", "저주 1개(단점+장점) + 코인 30."],
  RISK: ["🎲", "위험한 거래", "프리즘 증강 + 저주 1개. 고위험 고보상."],
  DEVICE: ["🔧", "장치", "보스 보상 장치를 영구 획득합니다."],
  AUGLEVEL: ["⬆️", "증강 강화", "보유 증강 1개를 레벨업 (10% 확률 기회!)."],
  POUCH: ["🎒", "심볼 주머니", "주머니를 편집해 확률 덱을 다듬습니다 (추가·제거·교체·업글)."],
  // [MED-1] Phase 5 심화 노드 — 미등록 시 "❔ SYMAUG" 원시 id 로 렌더되던 문제 수정.
  SYMAUG: ["✨", "심볼 증강", "주머니 빌드를 강화하는 심볼 증강 1개를 고릅니다."],
  SYMREL: ["🛡️", "심볼 유물", "주머니 관련 효과의 심볼 유물 1개를 고릅니다."],
  // §9.2 J3: 잭팟 노드 — 최다 태그 기반 심볼 패키지 3택 + 스킵
  JACKPOT: ["🎰", "잭팟 노드", "현재 최다 태그 심볼 3종 중 하나를 선택해 주머니에 추가합니다."],
};
function renderNode() {
  app.innerHTML = `
    <div class="scr-title"><div class="step-tag">스테이지 ${st.stage - 1} 클리어!</div><h1>보상 선택</h1><div class="sub">하나를 골라 다음 스테이지로 진행합니다.</div></div>
    <div class="coins-line">🪙 보유 코인 ${st.coins}</div>
    <div class="cards-col">${st.nodes.map((nd, i) => { const [e, n, d] = NODE_INFO[nd] || ["❔", nd, ""]; return `
      <button class="bigcard node-card nd-${esc(nd)}" data-act="node" data-arg="${i}"><div class="be">${e}</div><div class="bn">${esc(n)}</div><div class="bd">${esc(d)}</div></button>`; }).join("")}</div>`;
}
function renderPerk() {
  // V3P2: POUCH 및 2-step 서브상태 전부 renderPouchReward로 위임
  // V3P4: SYNAUG_BONUS(3스테이지 증강 연계 미니오퍼)도 동일 카드 계약(special/skip) — 같은 렌더러 재사용.
  if (st.pickKind === "POUCH" || st.pickKind === "POUCH_REMOVE" || st.pickKind === "POUCH_COST" || st.pickKind === "SYNAUG_BONUS") { renderPouchReward(); return; }
  // §9.2 J3 v1(2026-07-10): JACKPOT 노드는 이제 pickKind="POUCH"(§2 특수카드 경로 재사용)로 생성되므로
  //  위 분기에서 이미 renderPouchReward() 로 처리됨(offerMeta.kind==="JACKPOT_NODE" 로 헤더만 커스텀 — 그
  //  함수 내부 참고). 구 전용 렌더러(태그 이름·rarity 기반 카드)는 은퇴 — special 5경로 재사용으로 대체.
  if (st.pickKind === "LVL") {
    app.innerHTML = `
      <div class="scr-title"><div class="step-tag">⬆️ 증강 강화</div><h1>강화할 증강 선택</h1><div class="sub">보유 증강 하나를 레벨업해요 (최대 Lv.3). 🎁 10% 확률 강화 기회!</div></div>
      <div class="cards-col">${st.options.map((p, i) => `
        <button class="bigcard t-${p.t}" data-act="perk" data-arg="${i}" style="border-color:#ffd23f88">
          <div style="display:flex;align-items:center;gap:8px"><span class="be">${p.e}</span><span class="lvl-up-badge">Lv.${p.curLevel} → Lv.${p.nextLevel}</span></div>
          <div class="bn">${esc(p.n)}</div><div class="bd">${esc(p.d)}</div></button>`).join("")}</div>`;
    return;
  }
  // Phase 5→관련성 필터: 심화 SYMAUG/SYMREL 오퍼는 이제 심볼퍽 + 주머니 호환 일반 증강/유물 "혼합".
  //  🎒심볼 뱃지/sym-perk 클래스는 per-card SYM_PERK_BY_ID 멤버십 판별(sa_/sp_/sr_ 접두 판별 금지) — 일반 퍽 오표기 방지.
  //  deep:2(부분적용) 퍽은 심화 런에서 dDesc(유효 파트만) 우선 표시 + 🎒부분적용 뱃지(기존 badge sym 문법 재사용).
  const deepPerk = !!(st.offerMeta && st.offerMeta.deepPerk);
  // §3 배치 C: 심화 도박/휴식 선택지 헤더 — 카드 렌더링은 아래 기존 경로 공유(옵션 {id,e,n,d,t} 호환).
  let stepTag, titleTxt, subTxt;
  if (st.pickKind === "GAMBLE_DEEP") {
    stepTag = "🎲 도박"; titleTxt = "선택지 1개 선택"; subTxt = "운에 맡겨보세요 — 심볼 도박은 주머니가 바뀝니다";
  } else if (st.pickKind === "REST_DEEP") {
    stepTag = "🛌 휴식"; titleTxt = "선택지 1개 선택"; subTxt = "코인을 받거나 주머니를 정화해요";
  } else {
    const kind = st.pickKind === "REL" ? "유물" : "증강";
    const dk = deepPerk ? "심화 " + kind : kind;
    const tagE = st.pickKind === "REL" ? "🛡️" : "✨";
    stepTag = (deepPerk ? `🎒 심화 ${kind}` : `${tagE} ${kind}`) + " 선택";
    titleTxt = `${dk} 1개 선택`;
    subTxt = deepPerk ? `지금 주머니(심볼 덱) 빌드에 맞는 ${kind}만 나와요.` : `빌드 방향에 맞는 ${kind}을 고르세요.`;
  }
  app.innerHTML = `
    <div class="scr-title"><div class="step-tag">${stepTag}</div><h1>${titleTxt}</h1><div class="sub">${subTxt}</div></div>
    <div class="cards-col">${st.options.map((p, i) => { const isSym = !!D.SYM_PERK_BY_ID[p.id]; return `
      <button class="bigcard perk-card t-${p.t}${isSym ? " sym-perk" : ""}" data-act="perk" data-arg="${i}">
        <div style="display:flex;align-items:center;gap:8px"><span class="be">${p.e}</span><span class="tier t-${p.t}" style="position:static">${p.t}</span>${isSym ? `<span class="badge sym">🎒심볼</span>` : ""}${st.deepMode && p.deep === 2 ? `<span class="badge sym">🎒부분적용</span>` : ""}${p.setTag ? `<span class="badge set">🧩세트</span>` : p.tierUp ? `<span class="badge up">🍀등급업</span>` : ""}</div>
        <div class="bn">${esc(p.n)}</div><div class="bd">${esc(st.deepMode && p.dDesc ? p.dDesc : p.d)}</div></button>`; }).join("")}</div>`;
}

// ── 심화모드: 심볼 주머니(덱) 보상 선택 카드 ──
//  옵션 = [{type, id/from/to, n, preview}] (engine.offerSymbolRewards). 각 카드에 변경 전/후 미리보기·총량·압축경고.
const POUCH_RW = {   // 보상 타입별 표기(라벨·클래스·기호)
  // V3P2 신규 타입
  special:     { lb: "특수",    cls: "rw-special", sign: "✨" },  // V3P2: 특수 심볼 카드
  skip:        { lb: "건너뛰기", cls: "rw-skip",    sign: "⏭" },  // V3P2: 스킵 카드 (코인+5)
  cost_remove: { lb: "제거",    cls: "rw-cost-rm",  sign: "🗑️" },  // V3P2: 프리즘 비용 선택(기본 제거)
  cost_curse:  { lb: "저주추가", cls: "rw-cost-curse", sign: "☠" },  // V3P2: 프리즘 비용 선택(저주+1)
  // v2 dormant 타입 (정비소 커버·보존)
  add:     { lb: "추가",  cls: "rw-add",  sign: "+" },
  remove:  { lb: "제거",  cls: "rw-remove", sign: "−" },
  swap:    { lb: "교체",  cls: "rw-swap", sign: "⇄" },
  upgrade: { lb: "업그레이드", cls: "rw-up", sign: "▲" },
  package: { lb: "패키지", cls: "rw-pkg", sign: "📦" },   // 배치 H dormant
  randpack: { lb: "랜덤팩", cls: "rw-pack", sign: "🎲" },  // 배치 E §6.1 dormant
};
function symE(id) { const s = D.SYM_BY_ID[id]; return s ? s.e : (id === "empty" ? "▫" : id === "random" ? "🎲" : "❔"); }
function symN(id) { const s = D.SYM_BY_ID[id]; return s ? s.n : (id === "empty" ? "빈칸" : id === "random" ? "랜덤칸" : id); }
// 비용 설명 헬퍼(V3P2 특수 카드용)
function costDesc(p) {
  const cost = p.cost || {};
  if (!p.tier || p.tier === "CURSE") return "[저주] 무료 추가 (+1)";
  if (cost.free) return "무료 추가";
  if (p.tier === "PRISM") return "덱 추가 비용: 기본 심볼 2개 제거 또는 저주 +1";
  if (p.tier === "GOLD") return `덱 추가 비용: 기본 심볼 ${cost.removeN || 2}개 제거`;
  return "덱 추가 비용: 기본 심볼 1개 제거";
}
function pouchRewardTitle(p) {
  // V3P2 타입
  if (p.type === "special") return `${p.e || symE(p.id)} ${p.n || symN(p.id)}`;
  if (p.type === "skip") return "⏭ 선택하지 않기";
  if (p.type === "cost_remove") return `기본 심볼 ${p.removeN || 1}개 제거`;
  if (p.type === "cost_curse") return "☠ 저주 심볼 +1 추가";
  // v2 dormant 타입
  const m = POUCH_RW[p.type] || { lb: p.type };
  if (p.type === "package") return p.title || "패키지";   // 배치 H dormant
  if (p.type === "randpack") return `🎲 ×${p.n ?? 2}`;    // 배치 E §6.1 dormant
  if (p.type === "swap") return `${symE(p.from)} → ${symE(p.to)}`;
  if (p.type === "upgrade") return `${symE(p.id)} ▲`;
  return `${symE(p.id)} ${m.sign || ""}${p.n || ""}`;
}
function pouchRewardDesc(p) {
  // V3P2 타입
  if (p.type === "special") return costDesc(p);
  if (p.type === "skip") return "코인 +5 획득";
  if (p.type === "cost_remove") return `기본 이득 심볼 ${p.removeN || 1}개 제거 선택`;
  if (p.type === "cost_curse") return "해골 +1 추가 — 특수 심볼 비용 면제";
  // v2 dormant 타입
  if (p.type === "package") return p.desc || "";
  if (p.type === "add") return `${symN(p.id)} +${p.n}개 추가 (총량 늘어남)`;
  if (p.type === "remove") return `${symN(p.id)} -${p.n}개 제거 (총량 줄어듦)`;
  if (p.type === "swap") return `${symN(p.from)} ${p.n}개 → ${symN(p.to)} ${p.n}개 (총량 유지)`;
  if (p.type === "upgrade") return `${symN(p.id)} ${p.n}개 → 상위 등급으로 (총량 유지)`;
  return "";
}
// 주머니 보상({type,...}) → celebratePerk 가 기대하는 {e,n,t,d} 형태로 변환.
// 심볼 이모지 + 보상 제목 + GOLD 색. (오직 심화모드 주머니 픽 축하 팝업용)
function pouchCelebrateData(p) {
  if (!p) return null;
  // V3P2 신규 타입
  if (p.type === "special") {
    const tone = p.tier === "PRISM" ? "PRISM" : p.tier === "GOLD" ? "GOLD" : p.tier === "CURSE" ? "CURSE" : "SILVER";
    return { e: p.e || symE(p.id), n: `${p.n || symN(p.id)} 획득 (${p.tier || "SILVER"})`, t: tone, d: costDesc(p) };
  }
  if (p.type === "skip")        return { e: "⏭", n: "건너뛰기 — 코인 +5", t: "SILVER", d: "코인 +5 획득" };
  if (p.type === "cost_remove") return { e: "🗑️", n: `기본 심볼 ${p.removeN || 1}개 제거`, t: "SILVER", d: "기본 이득 심볼 제거" };
  if (p.type === "cost_curse")  return { e: "☠", n: "저주 심볼 +1 추가", t: "CURSE", d: "해골 +1 추가" };
  const m = POUCH_RW[p.type] || { lb: p.type };
  // v2 dormant 타입 (기존 로직 보존)
  if (p.type === "package") {
    const pv0 = p.preview || {};
    const totalTxt0 = (pv0.totalBefore != null && pv0.totalAfter != null && pv0.totalBefore !== pv0.totalAfter) ? ` · 총량 ${pv0.totalBefore}→${pv0.totalAfter}` : "";
    return { e: p.repId ? symE(p.repId) : "📦", n: `${m.lb} · ${p.title || ""}`, t: "PRISM", d: (p.desc || "") + totalTxt0 };
  }
  if (p.type === "randpack") {
    return { e: "🎲", n: `랜덤팩 · ${p.n ?? 2}개`, t: p.tier || "GOLD", d: randpackDesc(p) };
  }
  const iconId = p.type === "swap" ? p.to : p.id;
  const pv = p.preview || {};
  const tb = pv.totalBefore, ta = pv.totalAfter;
  const totalTxt = (tb != null && ta != null && tb !== ta) ? ` · 총량 ${tb}→${ta}` : "";
  return { e: symE(iconId), n: `${m.lb} · ${pouchRewardTitle(p)}`, t: p.tier || "GOLD", d: pouchRewardDesc(p) + totalTxt };
}
// 변경 전/후 미리보기(심볼별) + 총량 + 압축 패널티 경고 마크업
function pouchPreviewHtml(p) {
  const pv = p.preview || {};
  // 배치F P1: tb/ta 를 changes 맵보다 먼저 선언(맵 템플릿의 % 계산이 참조 — TDZ 금지). 계산·up/down 판정 불변.
  const tb = pv.totalBefore ?? 0, ta = pv.totalAfter ?? 0;
  const changes = (pv.changes || []).map((c) => { const dir = c.after > c.before ? " up" : c.after < c.before ? " down" : ""; return `<div class="rw-chg"><span class="rw-ce">${symE(c.id)}</span><span class="rw-cn">${esc(symN(c.id))}</span><span class="rw-cv${dir}">${c.before} <span class="dim">(${pct2(c.before, tb)}%)</span> → <b>${c.after}</b> <span class="dim">(${pct2(c.after, ta)}%)</span></span></div>`; }).join("");
  // 방어 가드: preview 에 총량 필드가 없으면 총량 행 미표시("0 → 0" 오표기 방지). 실값 있으면 그대로.
  const totalRow = (pv.totalBefore != null && pv.totalAfter != null) ? `<div class="rw-total">총량 <b>${tb} → ${ta}</b></div>` : "";
  const pb = pv.penBefore ?? 1, pa = pv.penAfter ?? 1;
  let penWarn = "";
  if (pa > 1 || pb !== pa) {
    const pctA = Math.round((pa - 1) * 100);
    const up = pa > pb;
    penWarn = `<div class="rw-pen${pa > 1 ? " on" : ""}">${pa > 1 ? "⚠️ 압축 패널티 요구 EXP +" + pctA + "%" : "압축 패널티 없음"}${up && pa > 1 ? " (증가)" : ""}</div>`;
  }
  return `<div class="rw-prev">${changes}${totalRow}${penWarn}</div>`;
}
// randpack 카드 전용 설명 — dist 실값을 % 표기(소수 없이 Math.round), n 실값 반영.
function randpackDesc(p) {
  const n = p.n ?? 2;
  const d = p.dist;
  if (!d || d.length < 3) return `🎲 랜덤 ${n}개 · 골드 이상 1개 보장`;
  const sp = Math.round(d[0] * 100), gp = Math.round(d[1] * 100), pp = Math.round(d[2] * 100);
  return `🎲 랜덤 ${n}개 · 실버 ${sp}% / 골드 ${gp}% / 프리즘 ${pp}% · 골드 이상 1개 보장`;
}
function renderPouchReward() {
  // V3P2: POUCH_REMOVE 서브상태 — 제거할 기본 심볼 선택 시트
  if (st.pickKind === "POUCH_REMOVE") {
    const ps = g.run && g.run._pendingSpecial || {};
    const sym = ps.e ? `${ps.e}${ps.n || ps.id || ""}` : (ps.id || "특수 심볼");
    const removeN = ps.removeN || 1;
    const cards = (st.options || []).map((p, i) => {
      const se = D.SYM_BY_ID[p.id]; const nm = se ? se.n : p.id; const ic = se ? se.e : "❔";
      return `<button class="bigcard pouch-rw rw-remove t-SILVER" data-act="perk" data-arg="${i}">
        <div class="rw-head"><span class="rw-lb">제거</span><span class="be rw-title">${ic} ${esc(nm)}</span></div>
        <div class="bd rw-desc">${esc(nm)} ×${p.n || 0}개 보유 — ${removeN}개 제거됩니다</div>
      </button>`;
    }).join("");
    app.innerHTML = `
      <div class="scr-title"><div class="step-tag">🗑️ 제거할 심볼 선택</div><h1>${esc(sym)} 획득</h1>
      <div class="sub">덱 추가 비용: 기본 이득 심볼 ${removeN}개를 제거하세요.<br>기본 이득 심볼(체리·책·별·보석·코인·불꽃·자석·폭탄)만 선택 가능합니다.</div></div>
      <div class="cards-col">${cards || `<div class="icard empty">제거할 기본 심볼이 없습니다.</div>`}</div>`;
    return;
  }
  // V3P2: POUCH_COST 서브상태 — 프리즘 비용 방식 선택
  if (st.pickKind === "POUCH_COST") {
    const ps = g.run && g.run._pendingSpecial || {};
    const sym = ps.e ? `${ps.e}${ps.n || ps.id || ""}` : (ps.id || "프리즘 심볼");
    const cards = (st.options || []).map((p, i) => {
      const m = POUCH_RW[p.type] || { lb: p.type, cls: "" };
      return `<button class="bigcard pouch-rw ${m.cls} t-SILVER" data-act="perk" data-arg="${i}">
        <div class="rw-head"><span class="rw-lb">${esc(m.lb)}</span><span class="be rw-title">${esc(pouchRewardTitle(p))}</span></div>
        <div class="bd rw-desc">${esc(pouchRewardDesc(p))}</div>
      </button>`;
    }).join("");
    app.innerHTML = `
      <div class="scr-title"><div class="step-tag">🌈 비용 방식 선택</div><h1>${esc(sym)} 획득</h1>
      <div class="sub">프리즘 심볼 획득 비용을 선택하세요.</div></div>
      <div class="cards-col">${cards || `<div class="icard empty">선택지가 없습니다.</div>`}</div>`;
    return;
  }
  // 기본 POUCH 오퍼 선택 화면 (V3P2: special + skip 카드)
  const meta = st.offerMeta || {};
  const cards = (st.options || []).map((p, i) => {
    const m = POUCH_RW[p.type] || { lb: p.type, cls: "" };
    const tier = p.tier || "SILVER";
    // V3P2 special 카드: 비용 설명 표기 + 티어 테두리
    // randpack 카드(dormant): dist/n 실값 반영 설명
    const isRandpack = p.type === "randpack";
    const descHtml = isRandpack
      ? `<div class="bd rw-desc">${randpackDesc(p)}</div>`
      : `<div class="bd rw-desc">${esc(pouchRewardDesc(p))}</div>`;
    return `<button class="bigcard pouch-rw ${m.cls} t-${tier}" data-act="perk" data-arg="${i}">
      <div class="rw-head"><span class="rw-lb">${esc(m.lb)}</span><span class="be rw-title">${pouchRewardTitle(p)}</span></div>
      ${descHtml}
      ${pouchPreviewHtml(p)}
    </button>`;
  }).join("");
  // V3P4: 3스테이지 증강 연계 미니오퍼 — 제목/설명만 교체(카드 계약·렌더 경로는 동일).
  // §9.2 J3 v1: JACKPOT 노드도 제목/설명만 교체(카드 렌더는 동일한 special/skip 5경로 재사용).
  const head = meta.synergyBonus
    ? `<div class="scr-title"><div class="step-tag">🔗 연계 보너스</div><h1>증강 연계 심볼</h1><div class="sub">3스테이지 증강 연계! 태그 일치 특수 심볼 1개를 <b>무료로</b> 덱에 추가하거나, 건너뛰고 코인 +5.</div></div>`
    : meta.kind === "JACKPOT_NODE"
    ? `<div class="scr-title"><div class="step-tag">🎰 잭팟 노드</div><h1>태그 심볼 선택</h1><div class="sub">${esc(meta.topTag ? `최다 태그: ${meta.topTag}` : "태그 심볼")} — 특수 심볼 1개를 덱에 추가하세요. 기본 심볼 제거 비용이 발생합니다. 건너뛰면 코인 +5.</div></div>`
    : `<div class="scr-title"><div class="step-tag">🎒 심볼 주머니</div><h1>주머니 보상 선택</h1><div class="sub">특수 심볼 카드 1개를 덱에 추가하세요. 기본 심볼 제거 비용이 발생합니다. 건너뛰면 코인 +5.</div></div>`;
  app.innerHTML = `
    ${head}
    <div class="pouch-topbar">
      <span class="pt-total">현재 총량 <b>${meta.total ?? "?"}</b></span>
      <button class="regbtn ghost2" data-act="pouch">🎒 주머니 현황</button>
    </div>
    <div class="cards-col">${cards || `<div class="icard empty">고를 수 있는 보상이 없어요.</div>`}</div>`;
}

// ── §10 V3: 덱 보드(심볼 카드 그리드) — 잭팟 태그 이모지(밀도 섹션과 별도 소형 칩·기존 TAG_LABEL 이모지값 재사용). ──
const TAG_EMOJI = { crown: "👑", seven: "7️⃣", coin: "🪙", prism: "🌈", curse: "💀", bell: "🔔" };
// 카테고리 뱃지(슬롯1, 항상 1개) — pouchView.cat 소비. 기존 cat-badge/cat-* CSS 문법 재사용(신규 색 없음).
const DECK_CAT_BADGE = {
  base:    { cls: "cat-base",    lb: "기본·소멸대상",  title: "자동 소멸 대상 — 15라운드부터 매 클리어 무작위 1개 제거" },
  harmful: { cls: "cat-harmful", lb: "해로운·소멸제외", title: "자동 소멸 제외 — 직접 정화해야 해요" },
  special: { cls: "cat-special", lb: "특수",           title: "보상·상점·이벤트로만 획득하는 특수 심볼" },
};
// 소모 속성 뱃지(슬롯2, use 있을 때만) — pouchView.use 소비.
const DECK_USE_BADGE = {
  instant: { cls: "cat-instant", lb: "일회용", title: "등장 즉시 발동 후 제거" },
  fuse:    { cls: "cat-fuse",    lb: "소모형", title: "조건 충족 시 발동 후 제거" },
};
// 정렬: 특수(티어 내림차순 PRISM→GOLD→SILVER) → 기본 → 해로운(§10 정렬 규칙).
const DECK_CAT_RANK = { special: 0, base: 1, harmful: 2 };
const DECK_TIER_RANK = { PRISM: 0, GOLD: 1, SILVER: 2, CURSE: 3 };
function deckSortEntries(entries) {
  return [...entries].sort((a, b) => {
    const ca = DECK_CAT_RANK[a.cat] ?? 1, cb = DECK_CAT_RANK[b.cat] ?? 1;
    if (ca !== cb) return ca - cb;
    if (a.cat === "special") { const ta = DECK_TIER_RANK[a.tier] ?? 2, tb = DECK_TIER_RANK[b.tier] ?? 2; if (ta !== tb) return ta - tb; }
    return b.n - a.n;
  });
}
// 심볼 1종 → 덱 보드 카드. .pcard(기존 선택카드 grid 문법) 재사용 — 이모지 크게(.ico)·이름(.nm)·개수·티어 테두리(t-*)·
//  뱃지 최대 2([기본·소멸대상]/[해로운·소멸제외]/[특수] 1개 + [일회용]/[소모형] 1개)·잭팟 태그 칩·효과 1줄(.ds, pouchView.desc).
function deckCardHtml(en, pv) {
  const tier = en.tier || "SILVER";
  const cb = DECK_CAT_BADGE[en.cat] || DECK_CAT_BADGE.special;
  const ub = en.use ? DECK_USE_BADGE[en.use] : null;
  const badges = `<span class="badge cat-badge ${cb.cls}" title="${esc(cb.title)}">${cb.lb}</span>${ub ? ` <span class="badge cat-badge ${ub.cls}" title="${esc(ub.title)}">${ub.lb}</span>` : ""}`;
  const tagChip = en.jackpotTag ? ` <span class="deck-tag-chip" title="잭팟 태그: ${esc(en.jackpotTag)}">${TAG_EMOJI[en.jackpotTag] || "🏷️"}</span>` : "";
  // 개수 옆 확률 — 🧪 bias 유효%(우선) → [MED-4] 통계표 실효% → 기본 비중%(기존 pouch-row 우선순위 그대로 유지).
  let pctTxt;
  if (en.biasChanged && en.effRatioBias != null) pctTxt = `${pct2(en.effRatioBias * 100, 100)}% 🧪`;
  else if (pv.statTable && en.effRatio != null && en.id !== "empty") pctTxt = `${Math.round(en.effRatio * 10000) / 100}%`;
  else pctTxt = `${pct2(en.n, pv.total)}%`;
  return `<div class="pcard deck-card t-${tier}" data-act="deckCard" data-arg="${en.id}">
    <div class="ico">${en.e}</div>
    <div class="nm">${esc(en.nm)} <span class="dc-cnt">×${en.n}</span></div>
    <div class="dc-pct dim">${pctTxt}</div>
    <div class="dc-badges">${badges}${tagChip}</div>
    <div class="ds">${esc(en.desc)}</div>
  </div>`;
}
// 카드 탭 → 심볼 1개 상세(cb-row 계약 재사용 — openDexInfo 문법과 동일). '◀ 뒤로'는 덱 보드로 복귀(닫기 아님 —
//  openSheet 는 시트 내용을 통째로 교체하는 구조라 '뒤로' data-act=pouch 로 openPouchSheet() 재호출하면 그대로 복귀).
function openDeckCardDetail(id) {
  const pv = (st && st.pouchView) || g.pouchView();
  if (!pv) { closeSheet(); return; }
  const en = pv.entries.find((x) => x.id === id);
  if (!en) { openPouchSheet(); return; }
  const row = (label, val) => (val != null && val !== "") ? `<div class="cb-row"><span>${esc(label)}</span><b>${esc(String(val))}</b></div>` : "";
  const tierChip = en.tier ? ` <span class="tier t-${en.tier}" style="font-size:calc(10px * var(--sc))">${en.tier}</span>` : "";
  const catLabel = (DECK_CAT_BADGE[en.cat] || DECK_CAT_BADGE.special).lb;
  const useLabel = en.use ? (DECK_USE_BADGE[en.use] || {}).title : "";
  const pct = pv.total > 0 ? pct2(en.n, pv.total) : 0;
  const rows = row("보유 수량", `×${en.n} (${pct}%)`)
    + row("분류", catLabel)
    + row("등급", en.rarity)
    + (useLabel ? row("속성", useLabel) : "")
    + (en.jackpotTag ? row("잭팟 태그", `${TAG_EMOJI[en.jackpotTag] || ""} ${en.jackpotTag}`) : "")
    + row("효과", en.desc);
  openSheet(`<button class="sheet-close" data-act="pouch">◀ 뒤로</button>
    <h3 style="font-size:calc(20px * var(--sc))">${en.e} ${esc(en.nm)}${tierChip}</h3>
    <div class="cb-calc" style="margin-top:8px">${rows}</div>`);
}

// ── 심화모드: 주머니 현황 시트(§10 V3 덱 보드 — 상단 요약 통합 + 심볼 카드 그리드) ──
function openPouchSheet() {
  const pv = (st && st.pouchView) || g.pouchView();
  if (!pv) { showToast("심화모드에서만 볼 수 있어요"); return; }
  const penPct = Math.round((pv.penalty - 1) * 100);
  const penLine = pv.penalty > 1
    ? `<span class="pouch-pen on">⚠️ 압축 패널티 요구 EXP +${penPct}%</span>`
    : `<span class="pouch-pen">압축 패널티 없음</span>`;
  const warn = pv.valid && !pv.valid.ok
    ? `<div class="pouch-warn">⚠️ ${pv.valid.errors.map(esc).join(" · ")}</div>` : "";
  // Phase 5 심볼 표시형 장치(PEEK) — 태그스캐너/희귀탐지기(deepDeviceInfo). 장착 시에만 노출.
  const di = (st && st.deepDeviceInfo) || null;
  let peek = "";
  if (di && di.tagScanner) peek += `<div class="pouch-peek">🔎 태그스캐너 · 최강 <b>#${esc(di.tagScanner.strongest.tag)}</b> ${di.tagScanner.strongest.pct}% · 최약 <b>#${esc(di.tagScanner.weakest.tag)}</b> ${di.tagScanner.weakest.pct}%</div>`;
  if (di && di.rareDetector) peek += `<div class="pouch-peek">📡 희귀탐지기 · 다음 보상 희귀 심볼 ${di.rareDetector.possible ? "<b>가능</b>" : "없음"} <span class="dim">(오퍼는 무작위·가능성 표시)</span></div>`;
  // [MED-4] 낡은통계표(sr_stat_table) 보유 시 실효 확률 안내 배너.
  if (pv.statTable) peek += `<div class="pouch-peek">📊 낡은통계표 · 🎲랜덤칸 재분배를 반영한 <b>실효 등장확률</b>을 각 심볼에 표시 중</div>`;
  // 보유 심볼 증강/유물(심화 빌드 확인) — 정비 탭과 동일 칩.
  const symPerks = (pv.symPerks || []);
  const symLine = symPerks.length
    ? `<div class="sym-held">${symPerks.map((p) => `<span class="sym-chip t-${p.t}" title="${esc(p.d)}">${p.e}${esc(p.n)}</span>`).join("")}</div>` : "";
  // 총량 임계 경고칩(표시 전용) — 압축 패널티 세기 기준: >=40% 위험(빨강), >0% 주의(주황).
  const totalMax = pv.totalMax;
  const totalWarnCls = penPct >= 40 ? "warn-hi" : penPct > 0 ? "warn-lo" : "";

  // §4 V3P3 + §10: cat 집계(기본/특수/해로운) + use 집계(일회용/소모형) — pouchView.cat/use 필드 소비(추가 헬퍼 호출 불필요).
  let cntBase = 0, cntSpecial = 0, cntHarmful = 0, cntInstant = 0, cntFuse = 0;
  for (const en of (pv.entries || [])) {
    if (!en.n) continue;
    if (en.cat === "base") cntBase += en.n;
    else if (en.cat === "harmful") cntHarmful += en.n;
    else cntSpecial += en.n;
    if (en.use === "instant") cntInstant += en.n;
    else if (en.use === "fuse") cntFuse += en.n;
  }
  const useSuffix = (cntInstant || cntFuse)
    ? ` <span class="dim">(${[cntInstant ? `일회용 ${cntInstant}` : "", cntFuse ? `소모형 ${cntFuse}` : ""].filter(Boolean).join(" · ")})</span>` : "";

  // §4 V3P3: 자동 소멸 안내 행 — stage 기준(st.stage = 다음 스테이지, 즉 클리어 후 +1된 값).
  const curStage = (st && st.stage) || 0;
  let decayInfoLine = "";
  if (curStage > 15) {
    // 15+ 클리어 이미 발생(다음 스테이지 16+): 경고 톤
    decayInfoLine = `<div class="ps-decay warn">🍂 심화 압력 진행 중 — 매 클리어 기본 이득 1개 제거</div>`;
  } else if (curStage >= 15) {
    // 현재 15 스테이지 진행 중(15 클리어 직전): 예고 톤
    decayInfoLine = `<div class="ps-decay warn">🍂 심화 압력 진행 중 — 매 클리어 기본 이득 1개 제거</div>`;
  } else {
    // 15 미만: 예고 톤
    decayInfoLine = `<div class="ps-decay preview">15라운드부터 기본 이득 심볼이 매 클리어 1개씩 사라집니다</div>`;
  }

  // §9.2 J3: 잭팟 태그 밀도 섹션 — 태그별 심볼 개수 + 최다 태그 강조
  let tagDensityHtml = "";
  if (pv && pv.entries && D.JACKPOT_TAG) {
    const tagCount = {};
    for (const en of pv.entries) {
      if (!en.id || en.n <= 0 || en.id === "empty" || en.id === "random") continue;
      const t = D.JACKPOT_TAG[en.id];
      if (t) tagCount[t] = (tagCount[t] || 0) + en.n;
    }
    const tagEntries = Object.entries(tagCount).sort(([, a], [, b]) => b - a);
    if (tagEntries.length > 0) {
      const maxN = tagEntries[0][1];
      const jdChips = tagEntries.map(([t, n]) => {
        const isTop = (n === maxN);
        const total = pv.total || 1;
        const pct = Math.round((n / total) * 100);
        const topMark = isTop ? " 🔥" : "";
        return `<span class="jd-chip${isTop ? " jd-top" : ""}" title="${t}태그 ${n}개 (${pct}%)">${t} ${n}${topMark}</span>`;
      }).join("");
      const comment = tagEntries.length === 1
        ? "태그 1종 집중!" : maxN >= 5
        ? `🎰 ${tagEntries[0][0]} 잭팟 근접!` : `최다 태그: ${tagEntries[0][0]}`;
      tagDensityHtml = `<div class="jd-section"><span class="jd-label">🎰 잭팟 태그 밀도</span> ${jdChips} <span class="jd-comment dim">${comment}</span></div>`;
    }
  }

  // §10: 심볼 카드 그리드 — 정렬(특수 티어 내림차순 → 기본 → 해로운).
  const cards = deckSortEntries(pv.entries || []).map((en) => deckCardHtml(en, pv)).join("");
  openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 닫기</button>
    <h3>🎒 심볼 주머니</h3>
    <div class="sub">5칸은 주머니 비중대로 뽑혀요. 총량을 줄여 원하는 심볼 확률을 높일 수 있지만, 압축하면 요구 EXP가 올라가요.</div>
    <div class="pouch-summary">
      <span class="ps-item ${totalWarnCls}"><b class="ps-total-n">${pv.total}</b><span class="dim">/${totalMax}</span> · 기본 <b>${cntBase}</b> · 특수 <b>${cntSpecial}</b> · 해로운 <b>${cntHarmful}</b>${useSuffix}</span>
      <span class="ps-item">종류 <b>${pv.kinds}</b></span>
      ${penLine}
      ${decayInfoLine}
    </div>
    ${archGaugeHtml(pv)}
    ${peek}
    ${symLine}
    ${tagDensityHtml}
    ${warn}
    <div class="deck-grid">${cards || `<div class="icard empty">주머니가 비어 있어요.</div>`}</div>`);
}
function renderShop() {
  const deep = !!st.deepMode;
  if (!deep && shopTab !== "buy") shopTab = "buy";   // 일반모드는 정비 탭 없음(항상 buy)
  // 심화모드 탭 바(상점|정비). 일반모드는 미노출(격리).
  const tabs = deep ? `<div class="dex-tabs shop-tabs">
      <button class="dex-tab${shopTab === "buy" ? " on" : ""}" data-act="shopTab" data-arg="buy">🛒 상점</button>
      <button class="dex-tab${shopTab === "repair" ? " on" : ""}" data-act="shopTab" data-arg="repair">🔧 심볼 정비</button>
    </div>` : "";
  const body = (deep && shopTab === "repair") ? repairTabHtml() : shopBuyHtml();
  app.innerHTML = `
    <div class="scr-title"><div class="step-tag">🛒 상점</div><h1>상점</h1><div class="sub">${deep && shopTab === "repair" ? "코인으로 주머니(심볼 덱)를 정비하세요." : "상품을 눌러 구매하세요. 나가기 전까지 머무릅니다."}</div></div>
    <div class="coins-line">🪙 보유 코인 ${st.coins}</div>
    ${tabs}
    ${body}
    <div class="shop-foot">${deep && shopTab === "repair" ? "" : `<button class="bigbtn ghost" data-act="shopReroll">🔄 새로고침 (6🪙)</button>`}<button class="bigbtn" data-act="shopExit">나가기 ▶</button></div>`;
}
// 기존 상점(증강/유물/아이템) 목록 마크업 — 일반/심화 공용(buy 탭).
function shopBuyHtml() {
  const empty = !st.shopItems.length;
  return `<div class="cards-col">${empty ? `<div class="icard empty">남은 상품이 없어요 — 새로고침하거나 나가세요.</div>` : st.shopItems.map((o, i) => {
      const kindTxt = o.kind === "A" ? "증강" : o.kind === "R" ? "유물" : "아이템"; const afford = st.coins >= o.price;
      return `<div class="bigcard shop-item${afford ? "" : " short"}" data-act="shopBuyAsk" data-arg="${i}" style="display:flex;align-items:center;gap:11px">
        <span style="font-size:calc(28px * var(--sc))">${o.e}</span>
        <div style="flex:1;min-width:0"><div class="bn" style="font-size:calc(15px * var(--sc))">${esc(o.n)} <span class="tier t-${o.t || "GOLD"}" style="position:static;font-size:9px">${kindTxt}</span>${st.deepMode && o.deep === 2 ? `<span class="badge sym">🎒부분적용</span>` : ""}${o.couponTag ? `<span class="badge up">🎟쿠폰 -15%</span>` : o.setTag ? `<span class="badge set">🧩세트</span>` : o.tierUp ? `<span class="badge up">🍀등급업</span>` : ""}</div><div class="bd">${esc(st.deepMode && o.dDesc ? o.dDesc : o.d)}</div></div>
        <div class="shop-price${afford ? "" : " short"}">${o.price}🪙</div>
      </div>`; }).join("")}</div>`;
}

// ── Phase 3: 심화모드 상점 '심볼 정비' 탭 ──────────────────────────────
//  서비스 카드 목록 + 현재 주머니 요약(총량/상한/압축/태그버프). 대상 선택 필요 서비스는 시트로.
function repairTabHtml() {
  const pv = st.pouchView || {};
  const svcs = st.repairServices || [];
  const buffTags = Object.entries(pv.tagBuff || {}).filter(([, v]) => v);
  const buffLine = buffTags.length ? `<span class="ps-item">태그강화 ${buffTags.map(([t, v]) => `#${t}+${Math.round(v * 100)}%`).join(" ")}</span>` : "";
  const penPct = Math.round((pv.penalty - 1) * 100);
  // Phase 5: 보유 심볼 증강/유물 표시(정비 탭에서 빌드 확인). 압축패널티는 심볼증강 배수까지 반영된 값(pouchView.penalty).
  const symPerks = (pv.symPerks || []);
  const symLine = symPerks.length
    ? `<div class="sym-held">${symPerks.map((p) => `<span class="sym-chip t-${p.t}" title="${esc(p.d)}">${p.e}${esc(p.n)}</span>`).join("")}</div>`
    : "";
  const summary = `<div class="pouch-summary repair-summary">
      <span class="ps-item">총량 <b>${pv.total ?? "?"}</b> <span class="dim">/ ${pv.totalMin}~${pv.totalMax}</span></span>
      <span class="ps-item">종류 <b>${pv.kinds ?? "?"}</b></span>
      ${pv.penalty > 1 ? `<span class="pouch-pen on">⚠️ 요구 EXP +${penPct}%</span>` : `<span class="pouch-pen">압축 없음</span>`}
      ${buffLine}
      <button class="regbtn ghost2" data-act="pouch" style="margin-left:auto">🎒 주머니 현황</button>
    </div>${symLine}`;
  const cards = svcs.map((sv) => {
    const afford = st.coins >= sv.price;
    // 심볼증강 할인(discounted) 시 원가 취소선 + 실가격 표시.
    const priceHtml = sv.discounted
      ? `<span class="price-was">${sv.basePrice}</span> ${sv.price}🪙`
      : `${sv.price}🪙`;
    return `<div class="bigcard shop-item repair-item${afford ? "" : " short"}${sv.discounted ? " discounted" : ""}" data-act="repairAsk" data-arg="${sv.id}" style="display:flex;align-items:center;gap:11px">
      <span style="font-size:calc(26px * var(--sc))">${sv.e}</span>
      <div style="flex:1;min-width:0"><div class="bn" style="font-size:14.5px">${esc(sv.n)}${sv.targetPick ? ` <span class="tier t-SILVER" style="position:static;font-size:9px">선택</span>` : ""}</div><div class="bd">${esc(sv.desc)}</div></div>
      <div class="shop-price${afford ? "" : " short"}">${priceHtml}</div>
    </div>`;
  }).join("");
  return `${summary}<div class="cards-col">${cards}</div>`;
}
function shopBuyAsk(i) {
  const o = st.shopItems[i]; if (!o) return;
  if (st.coins < o.price) { showToast(`🪙 코인 ${o.price - st.coins} 부족 — 살 수 없어요`); return; }
  const kindTxt = o.kind === "A" ? "증강" : o.kind === "R" ? "유물" : "아이템";
  openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 취소</button><h3>🛒 구매할까요?</h3>
    <div class="ilist"><div class="icard"><div class="ie">${o.e}</div><div class="ib"><div class="iname">${esc(o.n)} <span class="tier t-${o.t || "GOLD"}" style="font-size:9px">${kindTxt}</span></div><div class="idesc">${esc(o.d)}</div></div></div></div>
    <div class="buy-confirm">가격 <b>${o.price}🪙</b> · 보유 ${st.coins}🪙 → 구매 후 <b>${st.coins - o.price}🪙</b></div>
    <div class="confirm-row"><button class="bigbtn ghost" data-act="closeSheet">취소</button><button class="bigbtn" data-act="shopBuyConfirm" data-arg="${i}">🛒 구매</button></div>`);
}

// ── Phase 3: 심볼 정비 구매 흐름(대상 선택 시트 → 확인 시트) ─────────────
//  대상 선택 상태는 모듈 변수(repairSel)로 유지(복잡 sel 을 data-arg 인코딩하지 않음). 취소/닫기 시 리셋.
let repairSel = { serviceId: null, sel: {} };
const rarClsOf = (r) => "rar-" + ({ "기본": "basic", "고급": "high", "희귀": "rare", "전설": "legend", "저주": "curse" }[r] || "basic");
function svcById(id) { return (st.repairServices || []).find((x) => x.id === id); }

// 심볼 선택 행(선택 시트용) 마크업. picked=현재 선택 강조.
function repairSymRow(en, act, picked) {
  return `<button class="pouch-row rsel-row${picked ? " picked" : ""}" data-act="${act}" data-arg="${en.id}">
    <span class="pr-e">${en.e}</span>
    <div class="pr-body"><div class="pr-top"><span class="pr-nm">${esc(en.nm)}</span><span class="pr-rar ${rarClsOf(en.rarity)}">${esc(en.rarity)}</span><span class="pr-cnt">보유 ×${en.n}</span></div></div>
  </button>`;
}

// 정비 서비스 진입 — 대상 선택 필요 여부로 분기.
function repairAsk(serviceId) {
  const sv = svcById(serviceId); if (!sv) return;
  if (st.coins < sv.price) { showToast(`🪙 코인 ${sv.price - st.coins} 부족 — 살 수 없어요`); return; }
  repairSel = { serviceId, sel: {} };
  if (!sv.targetPick) { repairConfirm(); return; }         // 대상 없음(정화/확장/압축) → 바로 확인
  if (sv.kind === "tagbuff") { repairPickTag(); return; }  // 태그 선택
  if (sv.kind === "swap") { repairPickSwapFrom(); return; } // 교체: A → B 2단계
  repairPickSymbol();                                       // 추가/제거/업글: 심볼 1개
}

// 심볼 1개 선택(추가/제거/업그레이드)
function repairPickSymbol() {
  const sv = svcById(repairSel.serviceId); if (!sv) return;
  const targets = g.repairTargets(sv.id, "id");
  const rows = targets.length ? targets.map((en) => repairSymRow(en, "repairSelSym", false)).join("")
    : `<div class="icard empty">${sv.kind === "upgrade" ? "업그레이드할 기본 심볼이 없어요." : sv.kind === "remove" ? "제거할 심볼이 없어요." : "대상 심볼이 없어요."}</div>`;
  openSheet(`<button class="sheet-close" data-act="repairCancel">◀ 취소</button>
    <h3>${sv.e} ${esc(sv.n)}</h3>
    <div class="sub">${esc(sv.desc)} — 대상 심볼을 고르세요.</div>
    <div class="pouch-list rsel-list">${rows}</div>`);
}

// 교체 1단계: 원본 A 선택(보유 심볼)
function repairPickSwapFrom() {
  const sv = svcById(repairSel.serviceId); if (!sv) return;
  const targets = g.repairTargets(sv.id, "from");
  const rows = targets.length ? targets.map((en) => repairSymRow(en, "repairSelFrom", false)).join("")
    : `<div class="icard empty">교체할 심볼이 없어요.</div>`;
  openSheet(`<button class="sheet-close" data-act="repairCancel">◀ 취소</button>
    <h3>${sv.e} ${esc(sv.n)} — ① 바꿀 심볼</h3>
    <div class="sub">주머니에서 <b>줄일 심볼(A)</b>을 고르세요. A ${sv.n2}개가 B ${sv.n2}개로 바뀝니다.</div>
    <div class="pouch-list rsel-list">${rows}</div>`);
}
// 교체 2단계: 대상 B 선택(전체 풀, A 제외)
function repairPickSwapTo() {
  const sv = svcById(repairSel.serviceId); if (!sv) return;
  const from = repairSel.sel.from;
  const targets = g.repairTargets(sv.id, "to").filter((en) => en.id !== from);
  const rows = targets.map((en) => repairSymRow(en, "repairSelTo", false)).join("");
  openSheet(`<button class="sheet-close" data-act="repairCancel">◀ 취소</button>
    <h3>${sv.e} ${esc(sv.n)} — ② 새 심볼</h3>
    <div class="sub"><b>${symE(from)} ${esc(symN(from))}</b> ${sv.n2}개 → 어떤 심볼로 바꿀까요?</div>
    <div class="pouch-list rsel-list">${rows}</div>`);
}
// 태그 선택(태그강화)
function repairPickTag() {
  const sv = svcById(repairSel.serviceId); if (!sv) return;
  const tags = g.repairTargets(sv.id);
  const rows = tags.length ? tags.map((t) => `<button class="pouch-row rsel-row" data-act="repairSelTag" data-arg="${t.tag}">
      <span class="pr-e">🏷️</span>
      <div class="pr-body"><div class="pr-top"><span class="pr-nm">#${esc(t.tag)}</span><span class="pr-cnt">주머니 ${t.cnt}개${t.buff ? ` · 현재 +${Math.round(t.buff * 100)}%` : ""}</span></div></div>
    </button>`).join("")
    : `<div class="icard empty">강화할 태그가 없어요.</div>`;
  openSheet(`<button class="sheet-close" data-act="repairCancel">◀ 취소</button>
    <h3>${sv.e} ${esc(sv.n)}</h3>
    <div class="sub">강화할 태그를 고르세요 — 해당 태그 심볼 EXP +${Math.round((sv.pct || 0) * 100)}% (심화모드 적용).</div>
    <div class="pouch-list rsel-list">${rows}</div>`);
}

// 정비 구매 확인 시트(미리보기 포함) — repairSel 기준.
function repairConfirm() {
  const sv = svcById(repairSel.serviceId); if (!sv) { closeSheet(); return; }
  const pv = g.repairPreview(sv.id, repairSel.sel);
  if (!pv) { closeSheet(); return; }
  // 미리보기 본문(심볼형은 전/후 변화, run델타형은 설명 텍스트)
  let previewHtml = "";
  if (pv.preview) {
    // 배치F P1: % 병기 — 분모는 pv.preview.* 인라인 참조(아래 tb/ta 선언보다 맵이 먼저 실행 = TDZ 회피). up/down 판정 불변.
    const changes = (pv.changes || []).map((c) => { const dir = c.after > c.before ? " up" : c.after < c.before ? " down" : ""; return `<div class="rw-chg"><span class="rw-ce">${symE(c.id)}</span><span class="rw-cn">${esc(symN(c.id))}</span><span class="rw-cv${dir}">${c.before} <span class="dim">(${pct2(c.before, pv.preview.totalBefore ?? 0)}%)</span> → <b>${c.after}</b> <span class="dim">(${pct2(c.after, pv.preview.totalAfter ?? 0)}%)</span></span></div>`; }).join("");
    const tb = pv.preview.totalBefore, ta = pv.preview.totalAfter;
    previewHtml = `<div class="rw-prev">${changes}${tb !== ta ? `<div class="rw-total">총량 <b>${tb} → ${ta}</b></div>` : ""}</div>`;
  } else if (sv.kind === "expand") previewHtml = `<div class="rw-prev"><div class="rw-total">총량 상한 <b>+${sv.n2}</b></div></div>`;
  else if (sv.kind === "compress") previewHtml = `<div class="rw-prev"><div class="rw-total">총량 최소치 <b>-${sv.n2}</b> · 요구 EXP <b>+${Math.round((sv.pct || 0) * 100)}%</b></div></div>`;
  else if (sv.kind === "tagbuff") previewHtml = `<div class="rw-prev"><div class="rw-total">#${esc(repairSel.sel.tag || "")} 심볼 EXP <b>+${Math.round((sv.pct || 0) * 100)}%</b></div></div>`;
  // 대상 요약
  let target = "";
  if (repairSel.sel.from) target = `${symE(repairSel.sel.from)}${esc(symN(repairSel.sel.from))} → ${symE(repairSel.sel.to)}${esc(symN(repairSel.sel.to))}`;
  else if (repairSel.sel.id) target = `${symE(repairSel.sel.id)}${esc(symN(repairSel.sel.id))}`;
  else if (repairSel.sel.tag) target = `#${esc(repairSel.sel.tag)}`;
  const warn = (!pv.ok) ? `<div class="pouch-warn">⚠️ ${esc(pv.error)}</div>` : "";
  const canBuy = pv.ok && pv.afford;
  openSheet(`<button class="sheet-close" data-act="repairCancel">◀ 취소</button><h3>🔧 정비할까요?</h3>
    <div class="ilist"><div class="icard"><div class="ie">${sv.e}</div><div class="ib"><div class="iname">${esc(sv.n)}${target ? ` <span class="dim">${target}</span>` : ""}</div><div class="idesc">${esc(sv.desc)}</div></div></div></div>
    ${previewHtml}
    ${warn}
    <div class="buy-confirm">가격 <b>${sv.price}🪙</b> · 보유 ${st.coins}🪙 → 정비 후 <b>${st.coins - sv.price}🪙</b></div>
    <div class="confirm-row"><button class="bigbtn ghost" data-act="repairCancel">취소</button><button class="bigbtn${canBuy ? "" : " ghost"}" data-act="${canBuy ? "repairConfirmBuy" : "noop"}"${canBuy ? "" : " disabled"}>🔧 정비</button></div>`);
}
function repairDoBuy() {
  const sv = svcById(repairSel.serviceId);
  closeSheet();
  if (!sv) return;
  st = g.repairBuy(sv.id, repairSel.sel);
  repairSel = { serviceId: null, sel: {} };
  snd.sfx("coin"); drainToasts(); render();
}
function repairCancel() { repairSel = { serviceId: null, sel: {} }; closeSheet(); }

function renderDeviceNode() {
  const d = st.drop || st.options[0];
  app.innerHTML = `
    <div class="scr-title"><div class="step-tag">🔧 보스 보상</div><h1>장치 획득</h1><div class="sub">영구 보유 장치입니다. 지금 장착하거나, 코인을 받으세요.</div></div>
    <div class="cards-col"><div class="bigcard"><div class="be">${d.e}</div><div class="bn">${esc(d.n)}</div><div class="bd">${esc(d.d)} · <span class="dim">${d.kind === "PASSIVE" ? "패시브" : "능동(" + (d.cmd || "") + ")"}</span></div></div></div>
    <div class="shop-foot"><button class="bigbtn" data-act="devnode" data-arg="equip">장착하기</button><button class="bigbtn ghost" data-act="devnode" data-arg="coin">코인 +15 받기</button></div>`;
}

// ── 스테이지 클리어 화면 (큰 연출) ──
function renderStageClear() {
  const cs = st.clearSummary; if (!cs) { st = g.proceedToNodes(); return render(); }
  const f2 = (n) => Math.round(n * 100) / 100;
  const row = (label, val, cls) => `<div class="cb-row${cls ? " " + cls : ""}"><span>${esc(label)}</span><b>${esc(val)}</b></div>`;
  let bd = row(`스테이지 보너스 (${cs.stage}×50)`, "+" + cs.sBase);
  if (cs.sLeft) bd += row(`초과 EXP (${cs.leftover}×2)`, "+" + cs.sLeft);
  if (cs.sSpins) bd += row(`남은 스핀 (${cs.leftSpins}×100)`, "+" + cs.sSpins);
  if (cs.sBoss) bd += row(`보스 보너스 (${cs.bossName})`, "+" + cs.sBoss);
  if (cs.curses && cs.curseMul !== 1) bd += row(`저주 배수 (저주 ${cs.curses})`, "×" + f2(cs.curseMul));
  if (cs.streak) bd += row("연승 보너스", "+" + cs.streak);
  bd += row("= 클리어 점수", cs.debt ? "0 (빚문서)" : "+" + cs.gain, "total");

  // ── 성공 지표: 2개 바(달성 EXP / 사용 슬롯) + 마지막 5칸 심볼 + 획득내역 ──
  const pct = cs.quota > 0 ? Math.round(cs.stageExp / cs.quota * 100) : 100;
  const expFill = Math.min(100, pct);
  const spinFill = cs.totalSpins > 0 ? Math.min(100, Math.round(cs.usedSpins / cs.totalSpins * 100)) : 100;
  const fast = cs.bySpin && cs.leftSpins >= 2;
  const grade = clearGrade(cs);
  // 성공 요약 칩: 등급(초과 EXP 기반) + 초과/초고속/아슬아슬/보스 — 3번째 카드 대신 감정선 유지
  const chips = [`<span class="cchip grade ${grade.cls}">${grade.icon} ${grade.label}</span>`];
  if (!cs.exact && cs.leftover > 0) chips.push(`<span class="cchip over">✨ 초과 EXP +${cs.leftover.toLocaleString()}</span>`);
  if (fast) chips.push(`<span class="cchip fast">⚡ ${cs.leftSpins}스핀 남기고 초고속</span>`);
  else if (cs.bySpin && cs.leftSpins === 0) chips.push(`<span class="cchip close">😮‍💨 아슬아슬 클리어</span>`);
  if (cs.boss) chips.push(`<span class="cchip boss">👑 보스 격파</span>`);
  const chipRow = chips.length ? `<div class="clear-chips">${chips.join("")}</div>` : "";
  // 마지막 스핀 5칸 + 어떻게 EXP를 얻어 성공했는지
  const cells = cs.lastCells || [];
  const clearReels = cells.length ? `
      <div class="rd-last-ttl">🎰 마지막 스핀 5칸${cs.bySpin ? " — 이렇게 성공!" : ""}</div>
      <div class="rd-reels">${cells.map((c) => `<div class="rd-reel">${c.e}${c.tag ? `<span class="rt">${esc(c.tag)}</span>` : ""}</div>`).join("")}</div>
      <div class="rd-gained">🎁 이 스핀에서 <b>+${(cs.lastSpinExp || 0).toLocaleString()}</b> EXP 획득</div>
      <div class="rd-notes-ttl">${cs.bySpin ? "이렇게 EXP를 얻어 요구치를 채웠어요" : "직전 스핀 결과"}</div>
      <div class="rd-notes">${(cs.lastNotes && cs.lastNotes.length) ? cs.lastNotes.map((n) => `<span class="rd-note">${esc(n)}</span>`).join("") : `<span class="rd-note dim">기본 심볼 EXP만 · 특수 효과 없음</span>`}</div>` : "";
  const instNote = !cs.bySpin ? `<div class="rd-inst">${cs.via === "bell" ? "🔔 비상졸업벨로" : "🎁 아이템으로"} 요구 EXP 즉시 달성 · ${cs.usedSpins}스핀 사용</div>` : "";
  // §3 V3P3: 자동 소멸 배너 — decayBanner 있으면 클리어 배너 하단에 병합(없으면 빈 문자열).
  const decayNote = cs.decayBanner ? `<div class="rd-decay">${esc(cs.decayBanner)}</div>` : "";

  app.innerHTML = `
    <div class="clear-scr">
      <div class="clear-badge${cs.boss ? " boss" : ""}">${cs.boss ? "👑 BOSS CLEAR" : "STAGE " + cs.stage + " CLEAR"}</div>
      <div class="clear-title">🎉 ${cs.boss ? "보스 격파!" : "스테이지 " + cs.stage + " 클리어!"}</div>
      ${chipRow}
      <div class="res-detail ok">
        <div class="rd-ttl ok">✅ 이렇게 클리어했어요</div>
        <div class="rd-bars">
          <div class="rdb"><div class="rdb-head"><span class="rdb-k">🎯 총 경험치 / 요구</span><span class="rdb-v">${cs.stageExp.toLocaleString()} / ${cs.quota.toLocaleString()} (${pct}%)</span></div><div class="rdb-bar"><div class="rdb-fill exp" style="width:${expFill}%"></div></div></div>
          <div class="rdb"><div class="rdb-head"><span class="rdb-k">🎰 사용 슬롯 / 최대</span><span class="rdb-v">${cs.usedSpins} / ${cs.totalSpins}${fast ? " ⚡" : ""}</span></div><div class="rdb-bar"><div class="rdb-fill spin" style="width:${spinFill}%"></div></div></div>
        </div>
        ${instNote}
        ${clearReels}
      </div>
      <div class="clear-gain">획득 점수 <span class="gn" id="clear-gain">+0</span></div>
      <div class="clear-total">누적 총점수 <b>${(cs.total || 0).toLocaleString()}</b></div>
      <div class="clear-sub">🪙 코인 ${cs.coinsNow != null ? cs.coinsNow : "?"} (+${cs.coin}) · 남은 스핀 ${cs.leftSpins} · 다음 STAGE ${cs.nextStage}</div>
      ${decayNote}
      <button class="clear-detail-tog" data-act="cleardetail">▼ 점수 상세</button>
      <div class="cb-calc clear-bd" id="clear-bd" style="display:none">${bd}</div>
    </div>
    <div class="choose-foot"><button class="bigbtn pulse" data-act="proceed">다음 → 보상 선택 ▶</button></div>`;
  stageClearFx(grade, cs.boss);
  countUp($("clear-gain"), cs.debt ? 0 : cs.gain);
}
function countUp(el, target, prefix = "+") {
  if (!el) return; const dur = 800; const start = performance.now();
  const tick = (now) => { const t = Math.min(1, (now - start) / dur); const e = 1 - Math.pow(1 - t, 3);
    el.textContent = prefix + Math.floor(target * e).toLocaleString(); if (t < 1) requestAnimationFrame(tick); };
  requestAnimationFrame(tick);
}
// 클리어 등급 — 초과 EXP(요구 대비 %)에 따라 연출/사운드 강도 결정. 딱맞춤=PERFECT, 보스는 +1단계.
function clearGrade(cs) {
  if (cs.exact) return { tier: 6, key: "perfect", cls: "perfect", icon: "🎯", label: "PERFECT!", sub: "딱 맞춤 — 완벽 클리어!" };
  const overPct = cs.quota > 0 ? (cs.leftover / cs.quota) * 100 : 0;
  let t = overPct < 20 ? 1 : overPct < 50 ? 2 : overPct < 100 ? 3 : overPct < 200 ? 4 : 5;
  if (cs.boss) t = Math.min(5, t + 1);
  const M = {
    1: { icon: "✅", label: "CLEAR", sub: "클리어 성공!" },
    2: { icon: "✨", label: "GREAT!", sub: "훌륭한 클리어!" },
    3: { icon: "💠", label: "AMAZING!", sub: "엄청난 초과 달성!" },
    4: { icon: "🌟", label: "SPECTACULAR!", sub: "압도적인 오버킬!" },
    5: { icon: "🏆", label: "LEGENDARY!", sub: "전설적인 대폭발!!" },
  };
  return { tier: t, key: "g" + t, cls: "g" + t, icon: M[t].icon, label: M[t].label, sub: M[t].sub };
}

// 스테이지 클리어 연출 — 등급별 escalation(색종이·불꽃·코인비·플래시·수렴링·랭크배너·사운드·흔들림).
function stageClearFx(grade, boss) {
  const tier = grade.tier;               // 1..5, 6=perfect
  const perfect = grade.key === "perfect";
  // 사운드(등급 상승)
  snd.sfx(perfect ? "perfect" : "clear");
  if (tier >= 4) setTimeout(() => snd.sfx("fanfare"), 130);
  else if (tier >= 2) setTimeout(() => snd.sfx("win"), 150);
  if (boss) setTimeout(() => snd.sfx("boss"), 70);
  // 흔들림(등급 비례 + 고티어 이중타)
  shake(tier >= 5 ? "xl" : tier >= 3 ? "bg" : "sm");
  if (tier >= 4) setTimeout(() => shake("bg"), 230);
  // 오버레이 레이어
  const o = document.createElement("div");
  o.className = "clear-fx " + grade.cls + (boss ? " boss" : "");
  const cols = ["#ffd23f", "#ff6ec7", "#5b9bff", "#2ee6c8", "#b07bff", "#4ade80", "#ff9a3f"];
  let html = `<div class="rays"></div>`;
  if (tier >= 2 || perfect) html += `<div class="cf-flash"></div>`;
  const confN = perfect ? 30 : ({ 1: 24, 2: 40, 3: 58, 4: 78, 5: 104 })[tier] || 30;
  for (let i = 0; i < confN; i++) {
    const c = cols[i % cols.length];
    const shp = i % 4 === 0 ? " tri" : (i % 4 === 2 ? " round" : "");
    html += `<div class="confetti${shp}" style="left:${(Math.random() * 100).toFixed(0)}%;--bg:${c};background:${c};animation-delay:${(Math.random() * 0.4).toFixed(2)}s;animation-duration:${(1.2 + Math.random() * 0.9).toFixed(2)}s;--rot:${(Math.random() * 360).toFixed(0)}deg"></div>`;
  }
  if (tier >= 3) { const fw = tier >= 5 ? 6 : tier === 4 ? 4 : 2; for (let i = 0; i < fw; i++) html += fireworkHtml(cols[i % cols.length], i); }
  if (tier >= 4) {
    const rain = ["🪙", "⭐", "💎", "🌟"]; const rn = tier >= 5 ? 22 : 14;
    for (let i = 0; i < rn; i++) html += `<div class="cf-coin" style="left:${(Math.random() * 100).toFixed(0)}%;animation-delay:${(Math.random() * 0.6).toFixed(2)}s;animation-duration:${(1.3 + Math.random() * 0.7).toFixed(2)}s;font-size:${(16 + Math.random() * 15).toFixed(0)}px">${rain[i % rain.length]}</div>`;
  }
  if (perfect) html += `<div class="cf-ring"></div><div class="cf-ring d2"></div><div class="cf-burst">🎯</div>`;
  html += `<div class="cf-rank"><div class="cfr-big">${grade.label}</div><div class="cfr-sub">${esc(grade.sub)}</div></div>`;
  o.innerHTML = html; document.body.appendChild(o);
  setTimeout(() => o.remove(), tier >= 4 ? 2700 : tier >= 2 ? 2200 : 1900);
}
function fireworkHtml(color, i) {
  const x = 14 + Math.random() * 72, y = 14 + Math.random() * 38, delay = 0.15 + i * 0.24 + Math.random() * 0.2;
  let spokes = ""; const N = 12;
  for (let k = 0; k < N; k++) spokes += `<i style="--a:${(k * 360 / N).toFixed(0)}deg"></i>`;
  return `<div class="cf-fw" style="left:${x.toFixed(0)}%;top:${y.toFixed(0)}%;color:${color};animation-delay:${delay.toFixed(2)}s">${spokes}</div>`;
}

// ── 튜토리얼 (첫 플레이: UI 가이드 → 직접 스핀·결과 해설 → 클리어/보상/증강/상점 안내 → 자유플레이) ──
const TOUR = [
  { sel: ".hud", t: "📊 상태 정보", b: "스테이지·점수·코인과 EXP 바예요. <b>스핀(현재/최대) 안에 EXP 바를 요구치까지</b> 채우면 클리어! 동시에 <b>점수를 최대한 많이</b> 모으는 게 목표예요." },
  { sel: "#reels", t: "🎰 슬롯 5칸", b: "한 줄 슬롯이에요. <b>칸을 탭하면</b> 그 심볼의 효과·영향을 볼 수 있어요. 같은 값심볼 2개↑면 세트 보너스!" },
  { sel: "#spinbtn", t: "🕹️ 슬롯 돌리기", b: "이 버튼으로 돌려 EXP를 모아요. <b>정해진 스핀(현재/최대) 안에</b> 요구치를 채우면 클리어! <b>빨리 클리어할수록(남은 스핀·초과 EXP) 점수가 더 올라가요.</b>" },
  { sel: "#ab-extra", t: "✨ 특수 스핀", b: "집중(안정)·올인(2배도박)·기도(역전)·최후(막스핀↑) — <b>스테이지당 종류별 1회</b>씩 쓰는 모험 스핀! <b>한 판에서 종류별 첫 사용은 무료(코인 0이어도 OK)</b>, 두 번째부터 <b>코인 비용(🎯1·⏰2·🙏3·🎰4, 보스 +1)</b>이 들고 버튼에 가격이 표시돼요." },
  { sel: "#abicons", t: "🎒 아이템·장치·상태", b: "아이템 사용 · 장치 발동 · <b>내 빌드(능력치)</b> 확인을 여기서 해요." },
  { sel: "#spinbtn", t: "🎮 직접 돌려보세요!", b: "이제 <b>[🎰 슬롯 돌리기]</b>를 눌러 첫 스핀! 어떤 효과가 났는지 바로 알려드릴게요.", action: true },
];
function startTutorial() {
  if (!st || (st.phase !== PHASE.SPIN && st.phase !== PHASE.POST_SPIN)) return;
  tut.active = true; tut.live = false; tut.shown = {}; tut.ti = 0; renderTour();
}
function renderTour() {
  while (tut.ti < TOUR.length) { const el = document.querySelector(TOUR[tut.ti].sel); if (el && el.getBoundingClientRect().width > 0) break; tut.ti++; }
  if (tut.ti >= TOUR.length) return tutGoLive();
  const s = TOUR[tut.ti];
  const buttons = s.action ? `<button class="tut-skip" data-act="tutSkip">건너뛰기</button>`
    : `<button class="tut-skip" data-act="tutSkip">건너뛰기</button><button class="tut-next" data-act="tutNext">다음 ▶</button>`;
  tutSpot(s.sel, { title: s.t, body: s.b, step: `튜토리얼 ${tut.ti + 1}/${TOUR.length}${s.action ? " · 직접 해보기" : ""}`, block: !s.action, action: s.action, buttons });
}
let tutHl = null;
function tutSpot(sel, o) {
  const el = document.querySelector(sel); if (!el) return tutClear();
  if (tutHl && tutHl !== el) tutHl.classList.remove("tut-hl");
  tutHl = el; el.classList.add("tut-hl");
  const r = el.getBoundingClientRect(); const pad = 8, vh = window.innerHeight, below = r.top < vh * 0.42;
  const cx = r.left + r.width / 2, fingerAbove = r.top > 56;
  let root = $("tut-root"); if (!root) { root = document.createElement("div"); root.id = "tut-root"; document.body.appendChild(root); }
  // 단계 인디케이터(점) — TOUR 정규 진행 스텝에서만 표시(tutBanner/tutExplainSpin 경유 호출엔 미표시)
  const showDots = tut.active && !tut.live && tut.ti < TOUR.length && /^튜토리얼 \d+\//.test(o.step || "");
  const dots = showDots ? `<span class="tut-dots">${TOUR.map((_, i) => `<span class="tut-dot${i <= tut.ti ? " on" : ""}"></span>`).join("")}</span>` : "";
  root.innerHTML = `
    ${o.block ? '<div class="tut-block" data-act="noop"></div>' : ""}
    <div class="tut-spot${o.action ? " action" : ""}" style="left:${Math.max(2, r.left - pad)}px;top:${Math.max(2, r.top - pad)}px;width:${r.width + pad * 2}px;height:${r.height + pad * 2}px"></div>
    <div class="tut-finger" style="left:${cx}px;${fingerAbove ? `top:${r.top - 34}px` : `top:${r.bottom + 6}px`}">${fingerAbove ? "👇" : "👆"}</div>
    <div class="tut-tip" style="${below ? `top:${r.bottom + 14}px` : `bottom:${vh - r.top + 14}px`}">
      <div class="tut-step"><span>${o.step || "튜토리얼"}</span>${dots}</div><div class="tut-ttl">${o.title}</div><div class="tut-body">${o.body}</div>
      <div class="tut-row">${o.buttons}</div></div>`;
}
function tutBanner(html, btn) {
  let root = $("tut-root"); if (!root) { root = document.createElement("div"); root.id = "tut-root"; document.body.appendChild(root); }
  root.innerHTML = `<div class="tut-banner"><div class="tut-body">${html}</div><button class="tut-next" data-act="${btn || "tutBannerOk"}">확인 ▶</button></div>`;
}
function tutClear() { if (tutHl) { tutHl.classList.remove("tut-hl"); tutHl = null; } const r = $("tut-root"); if (r) r.remove(); }
function endTutorial() { tut.active = false; tut.live = false; tutClear(); g.markTutorialDone(); }
function tutExplainSpin() {
  if (!tut.active || tut.live) return;
  const cells = (st.lastCells || []).map((c) => c.sym.e).join("");
  const notes = ((lastGain && lastGain.notes) || []).slice(0, 4).join(" · ");
  const gained = lastGain ? lastGain.gained : 0;
  const used = Math.min(st.spinIndex, st.spins), left = Math.max(0, st.spins - st.spinIndex);
  const cleared = st.stageExp >= st.quota;
  tutSpot("#reels", { title: "💡 방금 스핀 결과", step: "튜토리얼 · 결과 해설", block: true,
    body: `<b>${cells}</b> 가 나왔어요.<br>${notes ? `효과: <b>${esc(notes)}</b><br>` : ""}이번 스핀 <b>+${gained} EXP</b> → 현재 <b>${Math.floor(st.stageExp)}/${st.quota} EXP</b><br>스핀 <b>${used}/${st.spins}</b> 사용 · <b>남은 ${left}회</b>${cleared ? "<br>🎉 <b>EXP를 다 채웠어요 — 스핀이 남아도 바로 클리어!</b>" : ""}`,
    buttons: `<button class="tut-next" data-act="tutToFree" style="flex:1">알겠어요 ▶</button>` });
}
function tutToFree() {
  tut.live = true;   // 이제부터 게임 진행에 맞춰 자동 안내(클리어→증강→상점)
  tutBanner("🎯 <b>정해진 스핀(현재/최대) 안에 EXP 바를 채워 클리어</b>! 스핀이 남아도 다 채우면 바로 클리어돼요.<br>🏆 <b>남은 스핀·초과 EXP·💎보석·👑왕관이 점수가 되니, 빨리·많이 모아 최대 점수에 도전!</b><br>💡 일반 말고 <b>특수 스핀(🎯집중·🎰올인·🙏기도·⏰최후)</b> 으로도 돌릴 수 있어요 — 아래 보라색 버튼! <b>코인 비용(🎯1·⏰2·🙏3·🎰4, 보스 +1)</b>이 들어요. 직접 채워보세요.", "tutBannerOk");
}
function tutGoLive() { tut.live = true; tutClear(); }
function tutLive() {
  if (!tut.active || !tut.live || !st) return;
  const map = {
    [PHASE.STAGE_CLEAR]: ["clear", "🎉 <b>스테이지 클리어!</b> 초과 EXP·남은 스핀·연승이 점수로 환산되고 코인도 받았어요. 누적 총점수도 확인! 아래 [다음]으로 보상 선택."],
    [PHASE.NODE_SELECT]: ["node", "🎁 <b>보상 선택</b> — ✨증강(영구 강화) · 🛡️유물(추가효과) · 🛒<b>상점</b>(코인으로 구매) · 🌑저주(위험+보상). 처음엔 ✨<b>증강</b> 추천!"],
    [PHASE.PERK_PICK]: ["perk", "✨ <b>증강 카드</b> — 등급이 <b>🥈실버 < 🥇골드 < 🌈프리즘</b> 순으로 강력! 빌드에 맞게 하나 골라요."],
    [PHASE.SHOP]: ["shop", "🛒 <b>상점</b> — 코인으로 증강·유물·아이템 구매. 항목을 누르면 구매 확인! 다 보면 [나가기]."],
    [PHASE.REWARD_DONE]: ["stats", "📊 고른 보상이 <b>능력치</b>에 반영됐어요. 다음 스테이지 정보 확인 후 [시작]!<br><b>이대로 계속 진행해 더 깊은 스테이지에 도전하세요!</b> 🎰"],
  };
  const L = map[st.phase]; if (!L || tut.shown[L[0]]) return;
  tut.shown[L[0]] = true; tutBanner(L[1], "tutBannerOk");
  if (L[0] === "stats") { tut.active = false; tut.live = false; g.markTutorialDone(); }
}

// ── 도감 (사용·발견한 것만 상세, 미발견은 ??? + 조건) ──
// [LOW-5] symAug/symRel = 심화 심볼 증강/유물 도감(36종) 탭 추가.
const DEX_CATS = [["char", "🎭 캐릭터"], ["mac", "🎰 머신"], ["dev", "🔧 장치"], ["aug", "✨ 증강"], ["rel", "🛡️ 유물"], ["cur", "🌑 저주"], ["set", "🎲 세트"], ["item", "🎒 아이템"], ["sym", "🔣 심볼"], ["deepSym", "🎒 심화심볼"], ["symAug", "✨ 심볼증강"], ["symRel", "🛡️ 심볼유물"], ["bld", "🏗️ 빌드"], ["ach", "🏆 업적"]];
function renderDex() {
  const d = g.dex(); const cat = dexTab; const list = d[cat] || []; const isAch = cat === "ach"; const isBld = cat === "bld";
  const doneOrSeen = (l, k) => (k === "ach" || k === "bld") ? l.filter((x) => x.done).length : l.filter((x) => x.seen).length;
  const cnt = doneOrSeen(list, cat);
  const tabs = DEX_CATS.map(([k, label]) => { const l = d[k] || []; const c = doneOrSeen(l, k);
    return `<button class="dex-tab${k === cat ? " on" : ""}" data-act="dexTab" data-arg="${k}">${label}<span class="dx-cnt">${c}/${l.length}</span></button>`; }).join("");
  let cards;
  if (isBld) {
    // 카테고리별 그룹 — 완성/미완성 모두 효과(조건) 노출(히든 없음·로컬). 클릭 시 상세.
    cards = D.THEME_BUILD_CATEGORIES.map((category) => {
      const items = list.filter((x) => x.cat === category);
      const cdone = items.filter((x) => x.done).length;
      const inner = items.map((x) => `<div class="dex-card bld ${x.done ? "seen" : "todo"}" data-act="dexInfo" data-arg="bld|${x.id}"><div class="dx-ico">${x.e}</div><div class="dx-b"><div class="dx-n">${esc(x.n)} ${x.done ? '<span class="dx-done">✓ 완성</span>' : '<span class="dx-todo">미완성</span>'}</div><div class="dx-d">${esc(x.detail)}</div></div></div>`).join("");
      return `<div class="dex-group-ttl">${esc(category)} <span class="dim">${cdone}/${items.length}</span></div>${inner}`;
    }).join("");
  } else {
    cards = list.map((x) => {
      if (isAch) { const rw = [x.reward ? `🔧${esc(x.reward)}` : "", x.symReward ? `🔓${esc(x.symReward)}` : ""].filter(Boolean).join(" · "); const dtag = x.deep ? `<span class="dx-deep">심화</span> ` : ""; return `<div class="dex-card ach ${x.done ? "seen" : "todo"}" data-act="dexInfo" data-arg="ach|${x.id}"><div class="dx-ico">${x.e}</div><div class="dx-b"><div class="dx-n">${dtag}${esc(x.n)} ${x.done ? '<span class="dx-done">✓ 달성</span>' : '<span class="dx-todo">미달성</span>'}</div><div class="dx-d">${esc(x.detail)}${rw ? ` · 보상 ${rw}` : ""}</div></div></div>`; }
      // 심화심볼: 발견했어도 아직 미해금(locked)이면 자물쇠 표기(주머니 풀 미등장). 장치는 deep 뱃지.
      if (x.seen) {
        // §6.3 deepSym 탭: x.tier 없으면 symTierOf 파생(CURSE 포함 전 티어). 기타 탭은 기존 x.tier 그대로.
        const resolvedTier = x.tier || (cat === "deepSym" ? symTierOf(x.id) : "");
        const tier = resolvedTier ? ` <span class="tier t-${resolvedTier}" style="font-size:9px">${resolvedTier}</span>` : "";
        const ms = (cat === "char" || cat === "mac" || cat === "dev") ? " " + masteryStars(cat, x.id) : ""; const lockTag = (cat === "deepSym" && x.locked) ? ` <span class="dx-lock">🔒 미해금</span>` : ""; const deepTag = (cat === "dev" && x.deep) ? ` <span class="dx-deep">심화</span>` : ""; return `<div class="dex-card seen" data-act="dexInfo" data-arg="${cat}|${x.id}"><div class="dx-ico">${x.e}</div><div class="dx-b"><div class="dx-n">${esc(x.n)}${tier}${deepTag}${lockTag}${ms} <span class="dx-more">›</span></div><div class="dx-d">${esc(x.detail)}</div></div></div>`; }
      return `<div class="dex-card masked"><div class="dx-ico">❔</div><div class="dx-b"><div class="dx-n">???</div><div class="dx-d cond">🔒 ${esc(x.cond || "아직 발견하지 못함")}</div></div></div>`;
    }).join("");
  }
  const subLabel = (isAch || isBld) ? `완성 ${cnt}/${list.length}` : `발견 ${cnt}/${list.length}`;
  app.innerHTML = `
    <button class="backbtn" data-act="dexback">◀ 뒤로</button>
    <div class="scr-title"><div class="step-tag">📚 도감</div><h1>슬롯 도감</h1><div class="sub">${subLabel} · ${isBld ? "플레이스타일을 완성하면 도감에 새겨져요" : "사용·발견한 항목만 공개됩니다"}</div></div>
    <div class="dex-tabs">${tabs}</div>
    <div class="dex-grid">${cards}</div>`;
}

// 도감 카드 상세 — 발견한 항목의 전체 능력치/효과
function dexUnlockText(o, withStage) {
  const f = (n) => n.toLocaleString(); const ps = [];
  if (o.unlockRuns) ps.push(o.unlockRuns + "런");
  if (o.unlockScore) ps.push(f(o.unlockScore) + "점");
  if (withStage && o.unlockStage) ps.push("S" + o.unlockStage + " 도달");
  if (o.unlockLevel) ps.push("Lv." + o.unlockLevel);
  if (o.unlockAch) { const a = D.ACHIEVEMENTS.find((x) => x.id === o.unlockAch); ps.push("업적 " + (a ? a.n : o.unlockAch)); }
  return ps.join(" 또는 ") || "기본 제공";
}
function openDexInfo(cat, id) {
  const f2 = (n) => Math.round(n * 100) / 100;
  const row = (k, v) => (v != null && v !== "") ? `<div class="cb-row"><span>${esc(k)}</span><b>${esc(String(v))}</b></div>` : "";
  const SP = { BOMB: "양옆 칸 제거 → 칸당 +" + D.C.BOMB_EXP_PER + " EXP", MAGNET: "한쪽 옆 심볼 복사", SKULL: "기본 무페널티 · 해골빌드면 EXP/점수 가산", FLAME: "이번 스핀 전체 +50% (다음 스핀 -50%)", DICE: "1~12 무작위 EXP", WILD: "최다 심볼 그룹에 합류 (세트·잭팟)", SEED: "다음 스핀에 책/별/왕관으로 성장", COIN: "코인 획득 (코인 배수 적용)", KEY: "보물 코인 +" + D.C.KEY_COIN_PER + "🪙 (열쇠 1개당)", NONE: "" };
  let e = "", n = "", tier = "", rows = "";
  if (cat === "sym") { const s = D.SYM_BY_ID[id]; if (!s) return; e = s.e; n = s.n; rows = row("기본 EXP", s.exp) + (s.score ? row("점수", "+" + s.score) : "") + (s.coin ? row("코인", "+" + s.coin) : "") + row("등장 가중치", s.weight > 0 ? s.weight : "휴면(머신·증강으로 등장)") + (SP[s.special] ? row("특수 효과", SP[s.special]) : "") + (s.rare ? row("희귀", "✦ 희귀 심볼") : "") + (s.tags.length ? row("태그", s.tags.map((t) => "#" + t).join(" ")) : ""); }
  else if (cat === "deepSym") { const s = D.SYM_BY_ID[id]; if (!s) return; e = s.e; n = s.n; tier = symTierOf(id); const dsc = (g.dex().deepSym || []).find((x) => x.id === id); rows = row("등급", D.POUCH_RARITY[id] || "기본") + row("티어", tier) + row("효과", (dsc && dsc.detail) ? dsc.detail.split(" · #")[0] : "특수 효과") + (s.exp ? row("기본 EXP", s.exp) : "") + (s.tags.length ? row("태그", s.tags.map((t) => "#" + t).join(" ")) : "") + row("등장", "심화모드 주머니 전용"); }
  else if (cat === "dev") { const d = D.DEV_BY_ID[id]; if (!d) return; e = d.e; n = d.n; rows = row("유형", d.kind === "PASSIVE" ? "패시브 (상시 자동)" : "능동") + (d.cmd ? row("발동", d.cmd) : "") + row("효과", d.d) + row("쿨다운", d.kind === "PASSIVE" ? "상시" : "스테이지당 1회") + (d.rare ? row("희귀", "✦ 희귀 장치") : ""); }
  else if (cat === "rel") { const r = D.REL_BY_ID[id]; if (!r) return; e = r.e; n = r.n; tier = r.t; rows = row("효과", r.d) + row("상점가", r.price + "🪙"); }
  else if (cat === "item") { const it = D.ITEM_BY_ID[id]; if (!it) return; e = it.e; n = it.n; rows = row("종류", { NEXTSPIN: "다음 스핀 1회", PHASE: "이번 스테이지 내내", INSTANT: "즉시 발동" }[it.k]) + row("효과", it.d) + row("코인가", it.cost + "🪙"); }
  else if (cat === "aug") { const a = D.AUG_BY_ID[id]; if (!a) return; e = a.e; n = a.n; tier = a.t; rows = row("등급", a.t) + row("효과", a.d); }
  // [LOW-5] 심화 심볼 증강/유물 상세 — SYM_PERK_BY_ID 통합 조회(sa_/sp_/sr_ 전역 유일).
  else if (cat === "symAug" || cat === "symRel") { const a = D.SYM_PERK_BY_ID[id]; if (!a) return; e = a.e; n = a.n; tier = a.t; rows = row("등급", a.t) + row("효과", a.d) + row("등장", cat === "symAug" ? "심화모드 심볼 증강 노드" : "심화모드 심볼 유물 노드") + (D.isSymAugLevelable(id) ? row("레벨업", "가능 (Lv.1~3 · 증강 강화 노드)") : ""); }
  else if (cat === "cur") { const c = D.CUR_BY_ID[id]; if (!c) return; e = c.e; n = c.n; rows = row("효과 (단점 / 장점)", c.d); }
  else if (cat === "set") { const s = D.SET_BY_ID[id]; if (!s) return; e = "🎰"; n = s.n; let req = s.req.map((pid) => { const p = D.PERK_BY_ID[pid]; return p ? p.e + p.n : pid; }).join(" + "); if (s.reqChar) req += " + " + ((D.CHAR_BY_ID[s.reqChar] || {}).e || "") + ((D.CHAR_BY_ID[s.reqChar] || {}).n || s.reqChar); if (s.reqMachine) req += " + " + ((D.MAC_BY_ID[s.reqMachine] || {}).e || "") + "머신"; if (s.reqDevice) req += " + " + ((D.DEV_BY_ID[s.reqDevice] || {}).e || ""); rows = row("필요 조합", req) + row("발동 효과", s.d); }
  else if (cat === "char") { const c = D.CHAR_BY_ID[id]; if (!c) return; e = c.e; n = c.n; rows = row("효과", c.d) + row("점수 보정", "×" + f2(c.scoreMod)) + (c.startCoins ? row("시작 코인", "+" + c.startCoins) : "") + row("해금 조건", dexUnlockText(c, true)); }
  else if (cat === "mac") { const m = D.MAC_BY_ID[id]; if (!m) return; e = m.e; n = m.n; const w = Object.entries(m.wmul || {}).map(([k, v]) => `${(D.SYM_BY_ID[k] || {}).e || k}×${f2(v)}`).concat(Object.entries(m.wadd || {}).map(([k]) => `${(D.SYM_BY_ID[k] || {}).e || k} 등장`)).join("  "); rows = row("효과", m.d) + row("점수 보정", "×" + f2(m.scoreMod)) + (w ? row("심볼 가중", w) : "") + row("해금 조건", dexUnlockText(m, false)); }
  else if (cat === "ach") { const a = D.ACHIEVEMENTS.find((x) => x.id === id); if (!a) return; e = a.e; n = a.n; const dv = D.DEV_BY_ID[D.ACH_DEVICE_REWARD[a.id]]; rows = row("달성 조건", a.d) + row("상태", g.profile.unlocked.includes(a.id) ? "✓ 달성" : "미달성") + (dv ? row("보상 장치", dv.e + dv.n) : ""); }
  else if (cat === "bld") { const b = D.TBUILD_BY_ID[id]; if (!b) return; e = b.e; n = b.n; rows = row("분류", b.cat) + row("완성 조건", b.cond) + row("상태", (g.profile.counters[b.id] || 0) > 0 ? "✓ 완성" : "미완성"); }
  // 숙련도(캐릭/슬롯/장치)
  if (cat === "char" || cat === "mac" || cat === "dev") {
    const m = g.masteryOf(cat, id);
    rows += row("숙련도", `Lv.${m.lv}/${m.total}  ${"★".repeat(m.lv)}${"☆".repeat(Math.max(0, m.total - m.lv))}`);
    rows += row("다음 숙련", m.next ? `Lv.${m.next.lv} — ${m.next.d}` : "★ 마스터 달성!");
  }
  if (!e) return;
  const tierChip = tier ? ` <span class="tier t-${tier}" style="font-size:calc(10px * var(--sc))">${tier}</span>` : "";
  openSheet(`<button class="sheet-close" data-act="closeSheet">◀ 닫기</button>
    <h3 style="font-size:calc(20px * var(--sc))">${e} ${esc(n)}${tierChip}</h3>
    <div class="cb-calc" style="margin-top:8px">${rows || '<div class="cb-row"><span>정보</span><b>—</b></div>'}</div>`);
}

// ── 보상 획득 → 다음 스테이지 인트로 (클릭해야 진행) ──
function renderRewardDone() {
  const np = st.nextPreview || {};
  const perks = st.perks.map((id) => D.PERK_BY_ID[id] && D.PERK_BY_ID[id].e).filter(Boolean);
  const curses = st.curses.map((id) => D.CUR_BY_ID[id] && D.CUR_BY_ID[id].e).filter(Boolean);
  const dev = st.device ? (D.DEV_BY_ID[st.device] && D.DEV_BY_ID[st.device].e) : "";
  // 보유 효과 = 클릭 가능한 칩(누르면 아래에 이름·등급·효과 정보 표시). 순서: 증강/유물 → 저주 → 장치
  const buildIds = [...st.perks, ...st.curses, ...(st.device ? [st.device] : [])];
  const chipEls = buildIds.map((id) => { const p = D.PERK_BY_ID[id] || D.DEV_BY_ID[id]; return p ? `<span class="bchip" data-act="buildInfo" data-arg="${id}">${p.e}</span>` : ""; }).join("");
  // 🌑 저주 효과 명시 — 보유 저주의 이름+효과(단점/장점)를 카드로 펼쳐 "무슨 저주인지 모름" 해소
  const curseBlock = st.curses.length ? `
      <div class="rd-curses">
        <div class="rd-curses-ttl">🌑 보유 저주 효과 (${st.curses.length})</div>
        <div class="ilist">${st.curses.map((id) => { const c = D.CUR_BY_ID[id]; return c ? `<div class="icard"><div class="ie">${c.e}</div><div class="ib"><div class="iname">${esc(c.n)}</div><div class="idesc">${esc(c.d)}</div></div></div>` : ""; }).join("")}</div>
      </div>` : "";
  const sv = st.statsView || {};
  const statRows = (sv.rows || []).map((r) => `<div class="cb-row"><span>${esc(r.k)}</span><b class="${r.up ? "up" : "down"}">${esc(r.v)}</b></div>`).join("");
  const symLines = [];
  if (sv.symExp && sv.symExp.length) symLines.push(`심볼 EXP &nbsp;${sv.symExp.join("  ")}`);
  if (sv.symScore && sv.symScore.length) symLines.push(`심볼 점수 &nbsp;${sv.symScore.join("  ")}`);
  if (sv.tags && sv.tags.length) symLines.push(`태그 &nbsp;${sv.tags.join("  ")}`);
  const statsBlock = (statRows || symLines.length) ? `
      <div class="rd-stats">
        <div class="rd-stats-ttl">📊 현재 능력치</div>
        <div class="cb-calc">${statRows || `<div class="cb-row"><span>기본</span><b>보정 없음</b></div>`}</div>
        ${symLines.length ? `<div class="rd-symline">${symLines.map((l) => `<div>${l}</div>`).join("")}</div>` : ""}
      </div>` : "";
  app.innerHTML = `
    <div class="reward-done">
      <div class="rd-msg">${esc(st.rewardMsg || "보상 획득!")}</div>
      ${chipEls ? `<div class="rd-build"><div class="rd-build-ttl">보유 효과 (${perks.length + curses.length}${dev ? "+장치" : ""}) <span class="rd-build-hint">· 항목을 눌러 정보 보기</span></div><div class="rd-chips">${chipEls}</div><div class="rd-chip-info" id="build-info"></div></div>` : ""}
      ${curseBlock}
      ${statsBlock}
      <div class="rd-next${np.boss ? " boss" : ""}">
        <div class="rd-next-ttl">${np.boss ? `${np.boss.e} 보스 — ${esc(np.boss.n)}` : `다음 STAGE ${np.stage || ""}`}</div>
        ${np.boss ? `<div class="rd-boss">${esc(np.boss.desc)}</div>` : ""}
        <div class="rd-next-sub">요구 EXP <b>${np.quota || "?"}</b> · 스핀 <b>${np.spins || "?"}</b></div>
      </div>
    </div>
    <div class="choose-foot"><button class="bigbtn tapstart" data-act="proceedStage"><span class="ts-arrow">▶</span> 스테이지 ${np.stage || ""} 시작 <span class="ts-hint">— 탭해서 시작</span></button></div>`;
}
// 보상 화면 '보유 효과' 칩 클릭 → 바로 아래에 이름·등급·효과 카드 토글(같은 칩 재클릭 시 닫힘).
function showBuildInfo(id, el) {
  const box = $("build-info"); if (!box) return;
  const p = D.PERK_BY_ID[id] || D.DEV_BY_ID[id]; if (!p) return;
  const wasSel = el && el.classList.contains("sel");
  document.querySelectorAll(".bchip.sel").forEach((b) => b.classList.remove("sel"));
  if (wasSel) { box.classList.remove("show"); box.innerHTML = ""; return; }   // 같은 칩 재클릭 → 닫기
  if (el) el.classList.add("sel");
  const kind = D.AUG_BY_ID[id] ? "증강" : D.REL_BY_ID[id] ? "유물" : D.CUR_BY_ID[id] ? "저주" : D.DEV_BY_ID[id] ? "장치" : "";
  const tier = p.t ? ` <span class="tier t-${p.t}" style="position:static;font-size:calc(9px * var(--sc))">${p.t}</span>` : "";
  const lvl = (st && st.perkLevels && st.perkLevels[id]) || 1;
  const lvlBadge = lvl > 1 ? ` <span class="lv-badge">Lv.${lvl}</span>` : "";
  box.innerHTML = `<div class="icard"><div class="ie">${p.e}</div><div class="ib"><div class="iname">${esc(p.n)} <span class="dim" style="font-size:10px">· ${kind}</span>${tier}${lvlBadge}</div><div class="idesc">${esc(st && st.deepMode && p.dDesc ? p.dDesc : p.d)}</div></div></div>`;
  box.classList.add("show");
}

// ── 랭킹 (지연 로드 — 오프라인에서도 게임은 동작) ──
let _rank = null;
async function rankMod() { if (!_rank) { try { _rank = await import("./rank.js"); } catch (e) { _rank = null; } } return _rank; }

// ── 로그인(구글) — 게스트는 로그인 없이 그대로. 지연 로드. 애플은 콘솔 설정 후 UI 노출. ──
let authUser = null;   // {uid,name,photo,email,provider} | null
// ★팝업 신뢰성: auth 모듈을 미리 로드해 둔다. 로그인 버튼 클릭 핸들러 안에서 import 를 await 하면
//   사용자 제스처 컨텍스트가 끊겨 브라우저가 signInWithPopup 을 팝업 차단할 수 있음(→ 조용한 실패/리다이렉트).
//   미리 프리로드해 두면 클릭 시 곧바로 팝업이 떠서 차단을 피함.
let _authModP = null;
function authMod() { if (!_authModP) _authModP = import("./auth.js").catch(() => null); return _authModP; }
let _authResolvedOnce = false;   // 첫 onAuthStateChanged 발화 여부(세션 복원 vs 사용자 로그인 구분)
async function initAuth() {
  const a = await authMod(); if (!a) return;
  try { a.completeRedirect(); } catch (e) {}   // 리다이렉트 로그인 복귀 완료(있으면)
  a.watchAuth(async (u) => {
    // 페이지 로드 시 세션 자동복원(첫 발화)은 "방금 로그인"이 아님 → 환영 토스트/게이트 이동 안 함.
    const silentRestore = !_authResolvedOnce;
    const justLoggedIn = !silentRestore && !authUser && !!u;
    _authResolvedOnce = true;
    authUser = u;
    const rank = await rankMod();
    if (rank) rank.setAuthUid(u ? u.uid : null);
    if (u && rank) {
      try { await rank.migrateGuestToUser(); } catch (e) {}              // 게스트 기록 → 계정으로 이전
      if (!rank.savedNick() && u.name) rank.setNick(u.name);             // 닉 없으면 계정 이름 프리필
      if (g.profile.bestScore > 0) { try { await rank.submitScore({ nick: rank.savedNick() || u.name || "익명", score: g.profile.bestScore, stage: g.profile.bestStage }); } catch (e) {} }
    }
    if (justLoggedIn) showToast(`👤 ${u.name || "로그인"} 님 환영해요!`);
    if (u) localStorage.setItem("slotweb_seenlogin", "1");
    // 로그인(사용자 액션 또는 리다이렉트 복귀)으로 게이트가 떠 있으면 홈으로. 무음복원 땐 게이트 로직이 알아서 처리.
    if (u && !silentRestore && $("login-gate")) { st = null; render(); }
    else refreshAuthUI();
  });
}
function refreshAuthUI() { if ($("end-rank")) setupEndRank(); if ($("acc-row")) renderAccountRow(); }
function provChip(pid) { if (!pid) return ""; if (pid.indexOf("google") >= 0) return ` <span class="auth-prov g">Google</span>`; if (pid.indexOf("apple") >= 0) return ` <span class="auth-prov a">Apple</span>`; return ""; }
function accountRowHtml() {
  if (authUser) {
    // 로그인됨: 이름/이메일 + 제공자 + 로그아웃
    const who = esc(authUser.name || authUser.email || "로그인됨");
    const sub = authUser.email && authUser.email !== authUser.name ? `<span class="acc-mail">${esc(authUser.email)}</span>` : "";
    return `<span class="set-lbl">👤 계정</span>
      <span class="acc-info"><span class="acc-who">${who}${provChip(authUser.provider)}</span>${sub}</span>
      <button class="set-tog logout-tog" data-act="logout">로그아웃</button>`;
  }
  // 게스트(비로그인): "구글로 로그인/연동" 버튼을 바로 노출(게스트 기록은 로그인 시 계정으로 이전됨).
  return `<span class="set-lbl">👤 계정</span>
    <span class="acc-info"><span class="acc-guest">게스트로 플레이 중</span><span class="acc-hint">로그인하면 기록이 계정에 저장돼요</span></span>
    <button class="set-tog acc-link" data-act="login"><span class="gg">G</span> 구글로 로그인</button>`;
}
function renderAccountRow() { const r = $("acc-row"); if (r) r.innerHTML = accountRowHtml(); }
async function doLogin(provider) {
  // 모듈은 initAuth 에서 이미 프리로드됨(캐시된 프라미스) → 사실상 즉시 resolve, 팝업 제스처 유지.
  const a = await authMod(); if (!a) { showAuthError("로그인 모듈을 불러오지 못했어요. 네트워크(오프라인)를 확인해 주세요."); return; }
  gateBusy(true);   // 게이트면 "로그인 중" 대기 표시(성공 시 watchAuth 가 홈으로, 리다이렉트면 페이지 이동)
  clearAuthError();
  try {
    if (provider === "apple") await a.signInApple(); else await a.signInGoogle();
  } catch (e) {
    gateBusy(false);
    const code = (e && e.code) || "";
    // 사용자가 직접 취소/창닫음 — 조용히 안내만
    if (code === "auth/popup-closed-by-user" || code === "auth/cancelled-popup-request" || code === "auth/user-cancelled") { showToast("로그인이 취소됐어요"); return; }
    // 팝업 자체가 안 뜬 경우(차단/환경) — auth.js 가 리다이렉트로 폴백하지만, 그마저 막히면 여기로 옴
    if (code === "auth/popup-blocked") { showAuthError("브라우저가 로그인 팝업을 차단했어요. 팝업 허용 후 다시 시도하거나, 카톡/인앱 브라우저면 크롬·사파리 등 기본 브라우저로 열어 주세요."); return; }
    // ▼ Firebase 콘솔 설정이 필요한 경우 — 코드로는 못 고침. 사용자에게 명확히 안내.
    if (code === "auth/operation-not-allowed") { showAuthError("구글 로그인이 아직 켜져 있지 않아요.\nFirebase 콘솔 → Authentication → 로그인 방법 → Google 을 사용 설정해야 해요.", true); return; }
    if (code === "auth/unauthorized-domain") { showAuthError("이 주소가 로그인 허용 도메인에 없어요.\nFirebase 콘솔 → Authentication → 설정 → 승인된 도메인에 이 사이트 주소를 추가해야 해요.", true); return; }
    if (code === "auth/network-request-failed") { showAuthError("네트워크 오류로 로그인하지 못했어요. 연결을 확인하고 다시 시도해 주세요."); return; }
    showAuthError("로그인에 실패했어요: " + (e.message || code || "알 수 없는 오류"));
  }
}
// 로그인 오류를 앱 톤 UI 로 표시(게이트에는 인라인 배너, 그 외 화면에서는 토스트).
// config=true 이면 관리자가 콘솔에서 조치해야 하는 설정 오류(오래 남기고 눈에 띄게).
function showAuthError(msg, config) {
  const gate = $("login-gate");
  if (gate) {
    let box = $("gate-error");
    if (!box) { box = document.createElement("div"); box.id = "gate-error"; box.className = "gate-error"; const btns = gate.querySelector(".gate-btns"); if (btns) btns.after(box); else gate.appendChild(box); }
    box.classList.toggle("config", !!config);
    box.innerHTML = `<span class="ge-ic">${config ? "🛠️" : "⚠️"}</span><span class="ge-tx">${esc(msg).replace(/\n/g, "<br>")}</span>`;
    return;
  }
  showToast((config ? "🛠️ " : "⚠️ ") + msg.replace(/\n/g, " "));
}
function clearAuthError() { const b = $("gate-error"); if (b) b.remove(); }
// 로그인 게이트가 떠 있으면 로딩 상태 토글(버튼 비활성 + 안내). 게이트 아니면(설정 등) 무시.
function gateBusy(on) {
  const gate = $("login-gate"); if (!gate) return;
  gate.querySelectorAll(".gate-btn").forEach((b) => { b.disabled = on; });
  let ov = $("gate-loading");
  if (on) { if (!ov) { ov = document.createElement("div"); ov.id = "gate-loading"; ov.className = "gate-loading"; ov.textContent = "🔄 로그인 창에서 계정을 선택하세요…"; const btns = gate.querySelector(".gate-btns"); if (btns) btns.after(ov); } }
  else if (ov) ov.remove();
}
async function doLogout() { const a = await authMod(); if (a) await a.signOutUser(); showToast("로그아웃되었어요"); }
async function openRank() {
  app.innerHTML = `<div class="scr-title"><div class="step-tag">🏆 랭킹</div><h1>명예의 전당</h1><div class="sub">불러오는 중…</div></div><div class="state">⏳</div>`;
  const rank = await rankMod();
  if (!rank) { app.innerHTML = `<button class="backbtn" data-act="rankback">◀ 뒤로</button><div class="scr-title"><h1>🏆 랭킹</h1><div class="sub err">랭킹 모듈을 불러오지 못했어요.</div></div>`; return; }
  const asc = rankMode === "asc";
  const deep = rankMode === "deep";
  const rows = await (deep ? rank.topDeepScores(50) : asc ? rank.topAscScores(50) : rank.topScores(50));
  const p = g.profile; const myKey = rank.myKey(); const nick = rank.savedNick() || (authUser && authUser.name) || "";
  let list;
  if (rows === null) list = `<div class="icard empty">랭킹을 불러오지 못했어요(네트워크). 잠시 후 다시 시도.</div>`;
  else if (!rows.length) list = `<div class="icard empty">${deep ? "아직 심화모드 기록이 없어요 — 첫 도전자가 되어보세요!" : asc ? "아직 심화 학기 기록이 없어요 — 첫 도전자가 되어보세요!" : "아직 등록된 기록이 없어요 — 첫 주인공이 되어보세요!"}</div>`;
  else list = rows.map((r, i) => {
    const me = r.cid === myKey;
    const pos = i === 0 ? "🥇" : i === 1 ? "🥈" : i === 2 ? "🥉" : `${i + 1}`;
    const tail = deep ? `S${r.stage || 0}` : asc ? `심화${r.asc || 0}` : `S${r.stage || 0}`;
    return `<div class="rankrow${me ? " me" : ""}"><div class="rk-pos">${pos}</div><div class="rk-nick">${esc(r.nick || "익명")}${me ? ` <span class="me-tag">나</span>` : ""}</div><div class="rk-score">${(r.score || 0).toLocaleString()}<span class="rk-stage">${tail}</span></div></div>`;
  }).join("");
  const myRow = rows && rows.find && rows.find((r) => r.cid === myKey);
  const authLine = authUser
    ? `<div class="auth-line"><span class="auth-who">👤 <b>${esc(authUser.name || "로그인됨")}</b>${provChip(authUser.provider)}</span><button class="auth-out" data-act="logout">로그아웃</button></div>`
    : `<div class="auth-line"><span class="auth-guest">게스트</span><button class="auth-in" data-act="login">🔓 구글 로그인</button></div>`;
  const toggle = `<div class="rank-toggle"><button class="rt-btn${!asc && !deep ? " on" : ""}" data-act="rankMode" data-arg="normal">일반</button><button class="rt-btn${asc ? " on" : ""}" data-act="rankMode" data-arg="asc">🎓 심화 학기</button><button class="rt-btn${deep ? " on" : ""}" data-act="rankMode" data-arg="deep">🎒 심화모드</button></div>`;
  const reg = deep
    ? `<div class="rank-reg">${authLine}<div class="sub">내 심화모드 최고 <b>${(p.bestDeepScore || 0).toLocaleString()}</b>점 · S${p.bestDeepStage || 0}${myRow ? ` · <span style="color:var(--gold)">등록됨</span>` : ""}</div><div class="dim" style="font-size:11px;margin-top:4px">심화모드 점수는 심볼 덱 런 종료 시 자동 등록돼요.</div></div>`
    : asc
    ? `<div class="rank-reg">${authLine}<div class="sub">내 심화 최고 <b>${(p.bestAscScore || 0).toLocaleString()}</b>점 (심화 ${p.bestAscLevel || 0})${myRow ? ` · <span style="color:var(--gold)">등록됨</span>` : ""}</div><div class="dim" style="font-size:11px;margin-top:4px">심화 학기 점수는 승천 런 종료 시 자동 등록돼요.</div></div>`
    : `<div class="rank-reg">${authLine}<div class="sub">내 최고 <b>${p.bestScore.toLocaleString()}</b>점 · S${p.bestStage}${myRow ? ` · <span style="color:var(--gold)">등록됨 ${(myRow.score || 0).toLocaleString()}</span>` : ""}</div><div class="reg-row"><input id="nickin" class="nick-input" maxlength="16" placeholder="닉네임 입력" value="${esc(nick)}" /><button class="regbtn" data-act="submitRank"${p.bestScore > 0 ? "" : " disabled"}>🏆 등록</button></div></div>`;
  app.innerHTML = `<button class="backbtn" data-act="rankback">◀ 뒤로</button>
    <div class="scr-title"><div class="step-tag">🏆 랭킹</div><h1>명예의 전당</h1><div class="sub">${deep ? "심화모드 랭킹 TOP 50" : asc ? "심화 학기 랭킹 TOP 50" : "최고 점수 TOP 50"}</div></div>
    ${toggle}${reg}<div class="rank-list">${list}</div>`;
}
async function submitRank() {
  const p = g.profile; if (p.bestScore <= 0) { showToast("아직 기록이 없어요 — 한 판 먼저!"); return; }
  const rank = await rankMod(); if (!rank) { showToast("랭킹 연결 실패"); return; }
  const nick = ($("nickin")?.value || "").trim() || "익명";
  showToast("등록 중…");
  const res = await rank.submitScore({ nick, score: p.bestScore, stage: p.bestStage });
  showToast(res.ok ? (res.rankUpdated ? "🏆 랭킹 등록 완료!" : "이미 더 높은 기록이 등록돼 있어요") : "등록 실패(네트워크)");
  openRank();
}

// ── 런 종료 ──
function renderEnd() {
  snd.bgmStop(); snd.sfx("gameover");
  const ch = D.CHAR_BY_ID[st.charId], mc = D.MAC_BY_ID[st.machineId];
  const build = [...st.perks.map((id) => D.PERK_BY_ID[id]?.e).filter(Boolean), ...st.curses.map((id) => D.CUR_BY_ID[id]?.e)].slice(0, 18);
  const ach = (st.newAch || []).map((a) => `<span class="a">${a.e} ${esc(a.n)}</span>`).join("");
  const fi = st.failInfo;
  const fpct = fi && fi.quota ? Math.min(100, Math.round(fi.exp / fi.quota * 100)) : 0;
  const spinPct = fi && fi.spins ? Math.min(100, Math.round(fi.usedSpins / fi.spins * 100)) : 0;
  const lastCells = (fi && fi.lastCells) || [];
  const lastReels = lastCells.length ? `
      <div class="rd-last-ttl">🎰 마지막 스핀 5칸</div>
      <div class="rd-reels">${lastCells.map((c) => `<div class="rd-reel">${c.e}${c.tag ? `<span class="rt">${esc(c.tag)}</span>` : ""}</div>`).join("")}</div>
      <div class="rd-gained">🎁 이 스핀에서 <b>+${(fi.lastGained || 0).toLocaleString()}</b> EXP 획득</div>
      <div class="rd-notes-ttl">이렇게 EXP를 얻었어요</div>
      <div class="rd-notes">${(fi.lastNotes && fi.lastNotes.length) ? fi.lastNotes.map((n) => `<span class="rd-note">${esc(n)}</span>`).join("") : `<span class="rd-note dim">기본 심볼 EXP만 · 특수 효과 없음</span>`}</div>` : "";
  const failBanner = fi ? `
    <div class="res-detail">
      <div class="rd-ttl${fi.shortBy > 0 ? " fail" : ""}">${fi.shortBy > 0 ? `💥 STAGE ${fi.stage} 실패` : fi.voluntary ? `🏁 STAGE ${fi.stage}에서 종료` : `STAGE ${fi.stage} 도달`}</div>
      <div class="rd-bars">
        <div class="rdb"><div class="rdb-head"><span class="rdb-k">🎯 총 경험치 / 요구</span><span class="rdb-v">${fi.exp} / ${fi.quota} (${fpct}%)</span></div><div class="rdb-bar"><div class="rdb-fill exp" style="width:${fpct}%"></div></div></div>
        <div class="rdb"><div class="rdb-head"><span class="rdb-k">🎰 사용 슬롯 / 최대</span><span class="rdb-v">${fi.usedSpins} / ${fi.spins}</span></div><div class="rdb-bar"><div class="rdb-fill spin" style="width:${spinPct}%"></div></div></div>
      </div>
      ${fi.shortBy > 0 ? `<div class="fail-short">요구 EXP까지 <b>${fi.shortBy}</b> 부족 — 정해진 슬롯 안에 못 채웠어요</div>` : ""}
      ${lastReels}
      <div class="fail-tip">💡 증강·유물로 EXP를 키우거나, 막판에 <b>특수 스핀(🎯집중·🙏기도)</b>·<b>아이템</b>으로 부족분을 채워보세요.</div>
    </div>` : "";
  const lp = g.playerProgress();
  const leveled = (st.levelAfter || 0) > (st.levelBefore || 0);
  const xpBlock = `<div class="endxp${leveled ? " up" : ""}">
      <div class="endxp-top"><span>플레이어 레벨 <b>Lv.${lp.level}</b></span><span class="endxp-gain">+${(st.xpGain || 0).toLocaleString()} XP</span></div>
      ${leveled ? `<div class="endxp-up">🎉 레벨 업! Lv.${st.levelBefore} → <b>Lv.${st.levelAfter}</b></div>` : ""}
      <div class="lvl-bar"><div class="lvl-fill" style="width:${Math.round((lp.ratio || 0) * 100)}%"></div></div>
      <div class="endxp-next">${lp.max ? "최고 레벨 달성" : `다음 레벨까지 ${(lp.need - lp.inLevel).toLocaleString()} XP`}</div>
    </div>`;
  app.innerHTML = `
    ${failBanner}
    <div class="result">
      <div class="title">${esc(titleOf(st.finalScore))}</div>
      <div class="dim" style="font-size:13px;margin-top:6px">최종 점수</div>
      <div class="fs">${(st.finalScore ?? 0).toLocaleString()}</div>
      <div class="meta">스테이지 ${st.stage} 도달 · ${ch?.e}${esc(ch?.n || "")} + ${mc?.e}${esc(mc?.n || "")}${st.device ? " + " + D.DEV_BY_ID[st.device].e : ""}</div>
      ${st.asc > 0 ? `<div class="asc-result">🎓 심화 학기 <b>${st.asc}</b> · 점수 보정 <b>×${(st.ascScoreMul || 1).toFixed(2)}</b></div>` : ""}
      ${st.deepMode ? `<div class="deep-result">🎒 심볼 덱(심화모드) 런 · <span class="dim">일반 랭킹과 별도 집계</span></div>` : ""}
      ${build.length ? `<div class="build">${build.map((e) => `<span class="b">${e}</span>`).join("")}</div>` : ""}
      ${ach ? `<div class="dim" style="font-size:12px">🏆 새 업적 해금</div><div class="ach">${ach}</div>` : ""}
      ${!st.deepMode && st.finalScore >= g.profile.bestScore ? `<div style="color:var(--gold);font-weight:800;margin:8px 0">🎉 최고 기록 갱신!</div>` : ""}
    </div>
    ${xpBlock}
    <div class="end-rank" id="end-rank"><div class="er-msg">🏆 랭킹 불러오는 중…</div></div>
    <div class="choose-foot" style="display:flex;flex-direction:column;gap:8px">
      <button class="bigbtn" data-act="restart">🔄 다시 도전</button>
      <div style="display:flex;gap:8px"><button class="bigbtn ghost" data-act="dex" style="flex:1">📚 도감</button><button class="bigbtn ghost" data-act="home" style="flex:1">🏠 홈으로</button></div>
    </div>`;
  setupEndRank();
}
// 게임 종료 시 랭킹 등록 위젯 — 닉 있으면 자동 등록·표시, 없으면 닉 입력해 바로 등록
async function setupEndRank() {
  const box = $("end-rank"); if (!box) return;
  const rank = await rankMod();
  if (!rank) { box.innerHTML = `<div class="er-msg dim">랭킹 연결 안 됨(오프라인) — 점수는 기기에 저장돼요</div>`; return; }
  if (g.profile.bestScore <= 0 && (g.profile.bestAscScore || 0) <= 0 && (g.profile.bestDeepScore || 0) <= 0) { box.innerHTML = ""; return; }
  const authLine = authUser
    ? `<div class="auth-line"><span class="auth-who">👤 <b>${esc(authUser.name || "로그인됨")}</b>${provChip(authUser.provider)}</span><button class="auth-out" data-act="logout">로그아웃</button></div>`
    : `<div class="auth-line"><span class="auth-guest">게스트로 플레이 중</span><button class="auth-in" data-act="login">🔓 구글 로그인</button></div>`;
  const nick = rank.savedNick() || (authUser && authUser.name) || "";
  let body;
  if (nick) {
    // 일반 기록이 있을 때만 일반 보드 제출 — 심화모드만 플레이한 유저(bestScore=0)가 slotrank 에 0점 행을 만들지 않게
    const res = g.profile.bestScore > 0 ? await rank.submitScore({ nick, score: g.profile.bestScore, stage: g.profile.bestStage }) : null;
    // 방금 승천 런이었으면 심화 학기 보드에도 자동 등록(별도 랭킹)
    let ascNote = "";
    if (st && st.asc > 0 && (g.profile.bestAscScore || 0) > 0) {
      const ares = await rank.submitAscScore({ nick, score: g.profile.bestAscScore, stage: g.profile.bestStage, asc: g.profile.bestAscLevel || st.asc });
      ascNote = `<div class="er-asc">🎓 심화 학기 랭킹 · 심화 최고 <b>${(g.profile.bestAscScore || 0).toLocaleString()}</b> ${ares.rankUpdated ? `<span class="er-new">등록!</span>` : "<span class='dim'>등록됨</span>"} <button class="regbtn ghost2" data-act="rankMode" data-arg="asc">심화 랭킹</button></div>`;
    }
    // 방금 심화모드(심볼 덱) 런이었으면 심화모드 보드에도 자동 등록(별도 랭킹 slotrank_deep)
    let deepNote = "";
    if (st && st.deepMode && (g.profile.bestDeepScore || 0) > 0) {
      const dres = await rank.submitDeepScore({ nick, score: g.profile.bestDeepScore, stage: g.profile.bestDeepStage || 0 });
      deepNote = `<div class="er-asc">🎒 심화모드 랭킹 · 심화모드 최고 <b>${(g.profile.bestDeepScore || 0).toLocaleString()}</b> ${dres.rankUpdated ? `<span class="er-new">등록!</span>` : "<span class='dim'>등록됨</span>"} <button class="regbtn ghost2" data-act="rankMode" data-arg="deep">심화모드 랭킹</button></div>`;
    }
    if (!$("end-rank")) return;   // 화면이 바뀌었으면 중단
    body = `${res ? `<div class="er-done">${res.ok ? `<b>${esc(nick)}</b> · 최고 <b>${g.profile.bestScore.toLocaleString()}</b>점 ${res.rankUpdated ? `<span class="er-new">랭킹 등록!</span>` : "<span class='dim'>등록됨</span>"}` : "<span class='dim'>등록 실패(네트워크) — 잠시 후 자동 재시도</span>"}</div>` : ""}
      ${ascNote}${deepNote}
      <div class="er-row"><input id="endnick" class="nick-input" maxlength="16" value="${esc(nick)}" /><button class="regbtn" data-act="endRankSubmit">닉 변경</button> <button class="regbtn ghost2" data-act="rank">전체 보기</button></div>`;
  } else {
    body = `<div class="er-sub">닉네임을 정하거나 로그인하면 이 점수가 랭킹에 올라가요.</div>
      <div class="er-row"><input id="endnick" class="nick-input" maxlength="16" placeholder="닉네임 입력" value="" /><button class="regbtn" data-act="endRankSubmit">🏆 등록</button></div>`;
  }
  box.innerHTML = `<div class="er-ttl">🏆 명예의 전당</div>${authLine}${body}`;
}
async function endRankSubmit() {
  // 세 모드 점수가 전부 0 이하일 때만 거부(setupEndRank 표시 조건과 동일 — 승천/심화 전용 플레이어 차단 방지)
  const p = g.profile;
  if (p.bestScore <= 0 && (p.bestAscScore || 0) <= 0 && (p.bestDeepScore || 0) <= 0) { showToast("아직 기록이 없어요"); return; }
  const rank = await rankMod(); if (!rank) { showToast("랭킹 연결 실패"); return; }
  const nick = ($("endnick")?.value || "").trim() || "익명";
  showToast("등록 중…");
  // 각 모드는 점수 > 0 일 때만 개별 제출(0점 행 생성 금지)
  if (p.bestScore > 0) {
    const res = await rank.submitScore({ nick, score: p.bestScore, stage: p.bestStage });
    showToast(res.ok ? (res.rankUpdated ? "🏆 랭킹 등록 완료!" : "닉 갱신 완료 (점수는 최고 기록 유지)") : "등록 실패(네트워크)");
  }
  if ((p.bestAscScore || 0) > 0) {
    await rank.submitAscScore({ nick, score: p.bestAscScore, stage: p.bestStage, asc: p.bestAscLevel || 0 });
  }
  if ((p.bestDeepScore || 0) > 0) {
    await rank.submitDeepScore({ nick, score: p.bestDeepScore, stage: p.bestDeepStage || 0 });
  }
  if (p.bestScore <= 0) showToast("닉 갱신 완료");
  setupEndRank();
}

// 시작 — 인트로 스플래시부터
renderIntro();
initAuth();   // 로그인 세션 복원/감지(비차단, 실패해도 게스트로 정상 동작)
