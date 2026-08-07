// ── 잭팟 슬롯 — 절차적 사운드 (Web Audio, 외부 파일 없음·오프라인 OK) ──
// 효과음(SFX)과 가벼운 배경음(BGM)을 오실레이터/노이즈로 합성. 음원 자산 불필요.
let ctx = null, master = null, enabled = true, bgmOn = false, bgmTimer = null, bgmStep = 0, vol = 0.7;
try { const sv = localStorage.getItem("slotweb_vol"); if (sv != null) { const p = parseFloat(sv); if (p >= 0 && p <= 1) vol = p; } } catch (e) {}

function init() {
  if (ctx) return ctx;
  try {
    const AC = window.AudioContext || window.webkitAudioContext; if (!AC) return null;
    ctx = new AC(); master = ctx.createGain(); master.gain.value = vol; master.connect(ctx.destination);
  } catch (e) { ctx = null; }
  return ctx;
}
// 사용자 제스처 안에서 호출 — 컨텍스트를 (필요시) 생성하고 즉시 재개해 모바일/인앱브라우저 자동재생 정책 우회.
export function resume() { try { const c = init(); if (c && c.state === "suspended") c.resume(); } catch (e) {} }
export function setEnabled(on) { enabled = !!on; if (!on) bgmStop(); }
export function isEnabled() { return enabled; }
export function setVolume(v) { vol = Math.max(0, Math.min(1, +v || 0)); if (master) master.gain.value = vol; try { localStorage.setItem("slotweb_vol", String(vol)); } catch (e) {} }
export function getVolume() { return vol; }

function tone(freq, dur, type, vol, when, slideTo) {
  const c = init(); if (!c || !enabled) return; resume();
  const t = c.currentTime + (when || 0);
  const o = c.createOscillator(), g = c.createGain();
  o.type = type || "sine"; o.frequency.setValueAtTime(freq, t);
  if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t + dur);
  g.gain.setValueAtTime(0.0001, t);
  g.gain.exponentialRampToValueAtTime(vol || 0.2, t + 0.008);
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  o.connect(g); g.connect(master); o.start(t); o.stop(t + dur + 0.03);
}
function noise(dur, vol, when, filterFreq) {
  const c = init(); if (!c || !enabled) return; resume();
  const t = c.currentTime + (when || 0);
  const n = Math.max(1, Math.floor(c.sampleRate * dur));
  const buf = c.createBuffer(1, n, c.sampleRate), d = buf.getChannelData(0);
  for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / n);
  const src = c.createBufferSource(); src.buffer = buf;
  const g = c.createGain(); g.gain.setValueAtTime(vol || 0.2, t); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  let node = src;
  if (filterFreq) { const f = c.createBiquadFilter(); f.type = "bandpass"; f.frequency.value = filterFreq; src.connect(f); node = f; }
  node.connect(g); g.connect(master); src.start(t); src.stop(t + dur + 0.03);
}

const PENTA = [523.25, 587.33, 659.25, 783.99, 880];   // C D E G A
export function sfx(name) {
  if (!enabled) return;
  switch (name) {
    case "tap": tone(660, 0.05, "triangle", 0.1); break;
    case "select": tone(587, 0.07, "triangle", 0.13); tone(880, 0.1, "triangle", 0.12, 0.05); break;
    case "spin": noise(0.42, 0.06, 0, 900); tone(200, 0.42, "sawtooth", 0.05, 0, 340); break;
    case "reel": tone(840, 0.035, "square", 0.07); break;
    case "win": [0, 0.08, 0.16].forEach((d, i) => tone(PENTA[i + 1], 0.18, "triangle", 0.15, d)); break;
    case "jackpot": [523, 659, 784, 1046].forEach((f, i) => tone(f, 0.3, "triangle", 0.2, i * 0.1)); noise(0.5, 0.08, 0, 2200); break;
    case "coin": tone(988, 0.06, "square", 0.11); tone(1318, 0.1, "square", 0.11, 0.05); break;
    case "buy": tone(660, 0.08, "square", 0.12); tone(988, 0.12, "square", 0.12, 0.06); break;
    case "clear": [523, 659, 784, 1046, 1318].forEach((f, i) => tone(f, 0.26, "triangle", 0.17, i * 0.09)); break;
    case "perfect": tone(1046, 0.1, "triangle", 0.2); tone(1568, 0.2, "triangle", 0.17, 0.08); tone(2093, 0.34, "sine", 0.13, 0.17); noise(0.3, 0.05, 0.16, 4200); break;
    case "fanfare": [523, 659, 784, 1046, 1318, 1568].forEach((f, i) => tone(f, 0.32, "triangle", 0.18, i * 0.075)); tone(130.8, 0.6, "sawtooth", 0.08, 0); tone(2093, 0.5, "sine", 0.12, 0.5); noise(0.55, 0.08, 0.46, 3200); break;
    case "perk": [784, 988, 1318].forEach((f, i) => tone(f, 0.2, "sine", 0.14, i * 0.06)); break;
    case "bomb": noise(0.35, 0.2, 0, 220); tone(80, 0.35, "sawtooth", 0.11, 0, 40); break;
    case "boss": tone(110, 0.5, "sawtooth", 0.13, 0, 90); tone(165, 0.5, "sawtooth", 0.09, 0.1); break;
    case "error": tone(170, 0.16, "sawtooth", 0.12, 0, 120); break;
    case "gameover": [392, 330, 262, 196].forEach((f, i) => tone(f, 0.3, "triangle", 0.14, i * 0.16)); break;
  }
}

// 부드러운 펜타토닉 아르페지오 루프 (저음량)
export function bgmStart() {
  if (!enabled || bgmOn) return; const c = init(); if (!c) return; resume();
  bgmOn = true; bgmStep = 0;
  const bass = [130.81, 130.81, 174.61, 196.00];   // C C F G
  const tick = () => {
    if (!bgmOn || !enabled) { bgmOn = false; return; }
    const note = PENTA[(bgmStep * 3) % PENTA.length] / 2;
    tone(note, 0.3, "triangle", 0.045);
    if (bgmStep % 4 === 0) tone(bass[((bgmStep / 4) | 0) % bass.length], 0.62, "sine", 0.05);
    bgmStep++;
    bgmTimer = setTimeout(tick, 230);
  };
  tick();
}
export function bgmStop() { bgmOn = false; if (bgmTimer) { clearTimeout(bgmTimer); bgmTimer = null; } }
export function bgmActive() { return bgmOn; }
