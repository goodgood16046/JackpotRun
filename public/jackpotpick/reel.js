// 잭팟런 랜덤 조합 릴 스핀 오버레이 — Unity ReelView(S13/S14) 노치 스크롤 이식.
// 구조: 릴마다 뷰포트(overflow:hidden) + 세로 5칸 스트립(위2/중앙 index2/아래2).
// 한 노치 = 스트립이 슬롯 높이만큼 아래로 흘러간 뒤 맨 아래 슬롯을 맨 위로 재활용.
// 감속 D=3노치, 타깃은 (D-3)=0번째 감속 노치에서 주입 → 마지막 노치 종료 시 중앙 도착.
//
// 대응관계(Unity ReelView → 여기):
//   NotchDuration()      → notchDuration()
//   AdvanceNotch()       → advanceNotch()  (DOM 재활용: 맨 아래 슬롯 → 맨 위로 이동 후 재렌더)
//   SpinOneReel()        → spinOne()       (니어미스/기대감 연출은 웹 스코프 제외)
//   RestNeighborAlpha/Scale, MaxSpeedSymbolAlpha/Scale → .rslot / .reel.spinning .rslot (CSS)
//   착지 오버슈트+스쿼시 → overshoot() + land()의 squash()

const SLOT = 92;                 // 슬롯 높이 px (Unity 130의 웹 축소판)
const ACCEL_DURATION = 0.25;     // 0→최고속 가속
const MAX_SPEED_NOTCH = 0.06;    // 최고속 노치 길이(초)
const ACCEL_START_NOTCH = 0.16;  // 가속 시작 노치 길이
const BASE_SPIN_HOLD = 0.35;     // 스태거 시작 전 최소 유지
const STAGGER_DELAY = 0.10;      // 왼쪽부터 릴별 정지 지연
const DECEL_DURS = [0.10, 0.16, 0.24]; // 감속 3노치(outCubic)
const OVERSHOOT_PX = 8;          // 착지 오버슈트
const OVERSHOOT_DUR = 0.16;      // outBack 복귀
const LAND_SQUASH_DUR = 0.06;    // 착지 스쿼시
const LAND_SQUASH_Y = 0.92;      // scaleY 0.92→1
const WIN_HOLD = 0.9;            // 전 릴 착지 후 자동 닫힘까지
const WIN_HOLD_SKIP = 0.4;       // 사용자 스킵으로 끝난 경우 단축 대기(모션 최소화 설정은 제외 — 정적 결과를 충분히 보여준다)

const BASE_Y = -1.75 * SLOT;     // 스트립 기준 오프셋(뷰포트 1.5×SLOT 중앙에 index2가 오도록)

// ── 이징 ────────────────────────────────────────────
const linear = (t) => t;
const outQuad = (t) => t * (2 - t);
const outCubic = (t) => 1 - (1 - t) ** 3;
const outBack = (t) => { const c1 = 1.70158, c3 = c1 + 1; return 1 + c3 * (t - 1) ** 3 + c1 * (t - 1) ** 2; };
const clamp01 = (x) => (x < 0 ? 0 : x > 1 ? 1 : x);

// 재진입 가드(모듈 레벨). 스핀 중 skip 플래그도 여기서 공유 —
// isOpen 이 동시성을 1개 세션으로 제한하므로 안전하다.
let isOpen = false;
let skip = false;

function notchDuration(t) {
  const p = outQuad(clamp01(t / ACCEL_DURATION));
  return ACCEL_START_NOTCH + (MAX_SPEED_NOTCH - ACCEL_START_NOTCH) * p;
}

// rAF 기반 트윈. skip 이 true 이면 즉시 fn(1) 후 resolve.
function tween(dur, ease, fn) {
  return new Promise((resolve) => {
    if (skip || dur <= 0) { fn(1); resolve(); return; }
    const t0 = performance.now();
    function frame(now) {
      if (skip) { fn(1); resolve(); return; }
      const t = Math.min(1, (now - t0) / (dur * 1000));
      fn(ease(t));
      if (t >= 1) { resolve(); return; }
      requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
  });
}

function randomItem(col) {
  const pool = col.pool;
  return pool[Math.floor(Math.random() * pool.length)];
}

// 슬롯 1칸의 내용을 item 으로 렌더(이미지 실패 시 이모지 폴백 — app.js imgTag 패턴과 동일).
function renderSlotContent(slotEl, item) {
  slotEl.textContent = "";
  if (item.imgKey) {
    const img = document.createElement("img");
    img.className = "rslot-ico";
    img.alt = "";
    img.src = `/jackpotdex/img/${item.imgKey}.png`;
    img.onerror = () => {
      const emoji = document.createElement("div");
      emoji.className = "rslot-ico emoji";
      emoji.textContent = item.e;
      img.replaceWith(emoji);
    };
    slotEl.appendChild(img);
  } else {
    const emoji = document.createElement("div");
    emoji.className = "rslot-ico emoji";
    emoji.textContent = item.e;
    slotEl.appendChild(emoji);
  }
}

function setSpinning(col, active) {
  col.reelEl.classList.toggle("spinning", active);
  if (active) {
    Array.from(col.stripEl.children).forEach((s) => s.classList.remove("center"));
  }
}

// 노치 1회 전진: 스트립을 SLOT 만큼 아래로 흘린 뒤, 맨 아래 슬롯을 맨 위로 재활용하고
// 그 슬롯의 내용을 newItem 으로 다시 렌더한다(노치당 렌더 1회). 마지막에 이음매 없이 복귀.
function advanceNotch(col, dur, newItem, ease) {
  return tween(dur, ease, (p) => {
    col.stripEl.style.transform = `translateY(${BASE_Y + SLOT * p}px)`;
  }).then(() => {
    const strip = col.stripEl;
    const recycled = strip.lastElementChild;
    strip.insertBefore(recycled, strip.firstElementChild);
    renderSlotContent(recycled, newItem);
    strip.style.transform = `translateY(${BASE_Y}px)`;
  });
}

// 착지 정적 상태(center 클래스·이름 표시·.land) — land()/settle() 공용.
function applyLandStatic(col) {
  Array.from(col.stripEl.children).forEach((s) => s.classList.remove("center"));
  col.stripEl.children[2].classList.add("center");
  col.nameEl.textContent = `${col.target.e} ${col.target.n}`;
  col.nameEl.classList.add("show");
  col.reelEl.classList.add("land");
}

function squash(col) {
  col.reelEl.style.transformOrigin = "50% 100%";
  return tween(LAND_SQUASH_DUR, outQuad, (p) => {
    const sy = LAND_SQUASH_Y + (1 - LAND_SQUASH_Y) * p;
    col.reelEl.style.transform = `scaleY(${sy})`;
  }).then(() => { col.reelEl.style.transform = ""; });
}

function land(col) {
  applyLandStatic(col);
  squash(col); // fire-and-forget — spinOne 은 land() 완료를 기다리지 않는다
}

function overshoot(col) {
  const from = BASE_Y - OVERSHOOT_PX;
  col.stripEl.style.transform = `translateY(${from}px)`;
  return tween(OVERSHOOT_DUR, outBack, (p) => {
    col.stripEl.style.transform = `translateY(${from + OVERSHOOT_PX * p}px)`;
  });
}

// skip/reduced-motion 공용 최종 상태: 스트립 스냅 + 5칸을 [무작위,무작위,target,무작위,무작위]로 직접 렌더.
function settle(col) {
  col.stripEl.style.transform = `translateY(${BASE_Y}px)`;
  const items = [randomItem(col), randomItem(col), col.target, randomItem(col), randomItem(col)];
  Array.from(col.stripEl.children).forEach((s, i) => renderSlotContent(s, items[i]));
  applyLandStatic(col);
}

async function spinOne(col, idx) {
  setSpinning(col, true);
  let t = 0;
  const stopDelay = ACCEL_DURATION + BASE_SPIN_HOLD + idx * STAGGER_DELAY;
  while (!skip) {
    const nd = notchDuration(t);
    if (t + nd >= stopDelay) break;
    await advanceNotch(col, nd, randomItem(col), linear);
    t += nd;
  }
  if (!skip) {
    await advanceNotch(col, MAX_SPEED_NOTCH, randomItem(col), linear); // 마지막 유지 노치
  }
  setSpinning(col, false);
  for (let k = 0; k < 3 && !skip; k++) {
    // 감속 — k===0 에서 타깃 주입(D-3 공식)
    await advanceNotch(col, DECEL_DURS[k], k === 0 ? col.target : randomItem(col), outCubic);
  }
  if (skip) { settle(col); return; }
  await overshoot(col);
  land(col);
}

// ── DOM 빌드 ────────────────────────────────────────
function buildCol(spec) {
  const root = document.createElement("div");
  root.className = "reelcol";

  const label = document.createElement("div");
  label.className = "reelcol-label";
  label.textContent = spec.label;
  root.appendChild(label);

  const reelEl = document.createElement("div");
  reelEl.className = "reel";
  const stripEl = document.createElement("div");
  stripEl.className = "strip";
  for (let i = 0; i < 5; i++) {
    const s = document.createElement("div");
    s.className = "rslot";
    stripEl.appendChild(s);
  }
  reelEl.appendChild(stripEl);
  root.appendChild(reelEl);

  const nameEl = document.createElement("div");
  nameEl.className = "reelcol-name";
  nameEl.textContent = "···";
  root.appendChild(nameEl);

  return { label: spec.label, pool: spec.pool, target: spec.target, root, reelEl, stripEl, nameEl };
}

function preload(cols) {
  cols.forEach((c) => {
    c.pool.forEach((item) => {
      if (item.imgKey) {
        const img = new Image();
        img.src = `/jackpotdex/img/${item.imgKey}.png`;
      }
    });
  });
}

/**
 * 랜덤 조합 릴 스핀 오버레이를 재생한다.
 * reels: 길이 3 배열. 각 원소 { label, pool, target }, item = { id, e, n, imgKey }.
 * 반환: 오버레이가 닫힐 때 resolve 되는 Promise(reject 없음).
 */
export function playReelSpin(reels) {
  if (isOpen) return Promise.resolve();
  isOpen = true;
  skip = false;

  return new Promise((resolve) => {
    const specs = reels.map((r) => ({
      label: r.label,
      pool: (r.pool && r.pool.length) ? r.pool : [r.target],
      target: r.target,
    }));

    const overlay = document.createElement("div");
    overlay.className = "reelov";
    overlay.style.setProperty("--rslot", SLOT + "px");

    const box = document.createElement("div");
    box.className = "reelov-box";

    const title = document.createElement("div");
    title.className = "reelov-title";
    title.textContent = "🎲 랜덤 조합 추첨!";
    box.appendChild(title);

    const row = document.createElement("div");
    row.className = "reelov-row";
    const cols = specs.map((spec) => {
      const col = buildCol(spec);
      row.appendChild(col.root);
      return col;
    });
    box.appendChild(row);

    const hint = document.createElement("div");
    hint.className = "reelov-hint";
    hint.textContent = "탭하면 결과 바로 보기";
    box.appendChild(hint);

    overlay.appendChild(box);
    document.body.appendChild(overlay);
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    preload(cols);

    // 초기 슬롯 5칸 무작위 채움(ShowIdle 대응)
    cols.forEach((col) => {
      col.stripEl.style.transform = `translateY(${BASE_Y}px)`;
      Array.from(col.stripEl.children).forEach((s) => renderSlotContent(s, randomItem(col)));
    });

    // 모션 최소화 설정이면 연출 전체를 건너뛰되(skip), 자동 닫힘은 사용자 스킵과 달리
    // 단축하지 않는다 — 정적 결과를 볼 시간이 오히려 더 필요하다.
    let reduced = false;
    if (window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      skip = true;
      reduced = true;
    }

    let winPhase = false;
    let done = false;
    let timer = 0;

    function onInput() {
      if (winPhase) { finish(); return; }
      skip = true;
    }
    function onKeyDown(e) { if (e.key === "Escape") onInput(); }
    overlay.addEventListener("click", onInput);
    document.addEventListener("keydown", onKeyDown);

    function finish() {
      if (done) return;
      done = true;
      clearTimeout(timer);
      overlay.removeEventListener("click", onInput);
      document.removeEventListener("keydown", onKeyDown);
      overlay.remove();
      document.body.style.overflow = prevOverflow;
      isOpen = false;
      resolve();
    }

    (async () => {
      try {
        await Promise.all(cols.map((col, i) => spinOne(col, i)));
      } catch (err) {
        // 연출 중 예외가 나도 오버레이가 화면을 영구히 덮으면 안 된다(inset:0 + overflow 잠금이
        // 남고 applyReco 의 await 도 영원히 미완이 된다) — 최종 상태로 강제 스냅 후 정상 종료.
        skip = true;
        cols.forEach((col) => { try { settle(col); } catch { } });
      }
      winPhase = true;
      title.textContent = "✨ 당첨!";
      overlay.classList.add("win");
      cols.forEach((col) => col.reelEl.classList.add("win"));
      hint.textContent = "탭하여 닫기";
      timer = setTimeout(finish, ((skip && !reduced) ? WIN_HOLD_SKIP : WIN_HOLD) * 1000);
    })();
  });
}
