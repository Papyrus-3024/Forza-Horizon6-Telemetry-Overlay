import { parseCapture } from './parser.js';

// ---- State -----------------------------------------------------------------

const state = {
  frames: [],          // decoded frames, sorted by t
  drivingFrames: [],   // subset with valid position for path/bounds
  bounds: null,        // {minX,maxX,minZ,maxZ} of drivingFrames
  fitScale: 1,         // world->px scale from auto-fit
  fitCenter: { x: 0, z: 0 },
  view: { panX: 0, panY: 0, zoom: 1 }, // canvas pan/zoom (independent of calibration)
  cal: { scale: 1, offX: 0, offY: 0, rot: 0, flipZ: false },
  season: 'spring',
  mapImg: null,
  mapPresent: {},
  playing: false,
  speed: 1,
  // playback cursor in capture-time (ms)
  curT: 0,
  startT: 0,
  endT: 0,
  lastRaf: 0,
};

// ---- DOM -------------------------------------------------------------------

const $ = (id) => document.getElementById(id);
const canvas = $('canvas');
const ctx = canvas.getContext('2d');

// ---- Map image -------------------------------------------------------------

async function loadMaps() {
  try {
    const res = await fetch('/api/maps');
    state.mapPresent = await res.json();
  } catch {
    state.mapPresent = {};
  }
  setSeason(state.season);
}

function setSeason(season) {
  state.season = season;
  const present = state.mapPresent[season];
  $('mapNote').textContent = present
    ? ''
    : `No ${season}.avif found — using neutral background. See web/README.md.`;
  if (!present) {
    state.mapImg = null;
    draw();
    return;
  }
  const img = new Image();
  img.onload = () => {
    if (state.season === season) {
      state.mapImg = img;
      draw();
    }
  };
  img.onerror = () => {
    state.mapImg = null;
    $('mapNote').textContent = `Failed to load ${season}.avif. Browser may lack AVIF support.`;
    draw();
  };
  img.src = `/maps/${season}.avif`;
}

// ---- Capture loading -------------------------------------------------------

async function listCaptures() {
  try {
    const res = await fetch('/api/captures');
    const names = await res.json();
    const sel = $('captureSelect');
    for (const name of names) {
      const opt = document.createElement('option');
      opt.value = name;
      opt.textContent = name;
      sel.appendChild(opt);
    }
  } catch {
    /* listing optional */
  }
}

function loadFrames(frames, label) {
  state.frames = frames;
  state.drivingFrames = frames.filter(
    (f) => f.raceOn !== 0 || (f.x !== 0 || f.z !== 0)
  );
  if (state.drivingFrames.length === 0) state.drivingFrames = frames.slice();

  computeBounds();
  autoFit();
  resetCalibration(false);

  state.startT = frames.length ? frames[0].t : 0;
  state.endT = frames.length ? frames[frames.length - 1].t : 0;
  state.curT = state.startT;
  state.playing = false;
  $('btnPlay').textContent = '▶';

  const dur = (state.endT - state.startT) / 1000;
  const moving = state.drivingFrames.length;
  $('captureInfo').textContent =
    `${label}: ${frames.length} frames (${moving} with position), ${dur.toFixed(1)}s`;
  $('canvasHint').style.display = frames.length ? 'none' : 'block';

  // reset view so path is centered
  state.view = { panX: 0, panY: 0, zoom: 1 };
  updateTimeline();
  draw();
}

function computeBounds() {
  const fs = state.drivingFrames;
  if (!fs.length) { state.bounds = null; return; }
  let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
  for (const f of fs) {
    if (f.x < minX) minX = f.x;
    if (f.x > maxX) maxX = f.x;
    if (f.z < minZ) minZ = f.z;
    if (f.z > maxZ) maxZ = f.z;
  }
  state.bounds = { minX, maxX, minZ, maxZ };
}

// Auto-fit world bbox into the canvas with margin.
function autoFit() {
  const b = state.bounds;
  if (!b) { state.fitScale = 1; state.fitCenter = { x: 0, z: 0 }; return; }
  const w = canvas.clientWidth || 800;
  const h = canvas.clientHeight || 600;
  const margin = 0.85;
  const spanX = Math.max(b.maxX - b.minX, 1);
  const spanZ = Math.max(b.maxZ - b.minZ, 1);
  state.fitScale = Math.min((w * margin) / spanX, (h * margin) / spanZ);
  state.fitCenter = { x: (b.minX + b.maxX) / 2, z: (b.minZ + b.maxZ) / 2 };
}

// ---- World -> screen -------------------------------------------------------
// Pipeline: world (X,Z) -> centered -> calibration (scale, rotate, flip, offset)
//           -> auto-fit scale -> view pan/zoom -> canvas pixels.

function worldToScreen(x, z) {
  const cx = (canvas.clientWidth || 800) / 2;
  const cy = (canvas.clientHeight || 600) / 2;

  // center on path center
  let wx = x - state.fitCenter.x;
  let wz = z - state.fitCenter.z;
  if (state.cal.flipZ) wz = -wz;

  // calibration rotation + scale
  const rot = (state.cal.rot * Math.PI) / 180;
  const cos = Math.cos(rot), sin = Math.sin(rot);
  const rx = wx * cos - wz * sin;
  const rz = wx * sin + wz * cos;

  const s = state.fitScale * state.cal.scale * state.view.zoom;
  let px = rx * s;
  let py = rz * s;

  // calibration pixel offset + view pan, then to canvas coords
  px += state.cal.offX + state.view.panX + cx;
  py += state.cal.offY + state.view.panY + cy;
  return [px, py];
}

// ---- Drawing ---------------------------------------------------------------

function resizeCanvas() {
  const dpr = window.devicePixelRatio || 1;
  canvas.width = Math.round(canvas.clientWidth * dpr);
  canvas.height = Math.round(canvas.clientHeight * dpr);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
}

function draw() {
  const w = canvas.clientWidth, h = canvas.clientHeight;
  ctx.clearRect(0, 0, w, h);

  // background
  ctx.fillStyle = '#0e1115';
  ctx.fillRect(0, 0, w, h);

  drawMap();
  drawPath();
  drawMarker();
}

// The map is drawn as a panned/zoomed backdrop. It is anchored independently
// of the world path so calibration sliders move the PATH over a fixed map.
function drawMap() {
  const img = state.mapImg;
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (!img) {
    // neutral grid backdrop
    ctx.strokeStyle = 'rgba(255,255,255,0.04)';
    ctx.lineWidth = 1;
    const step = 64 * state.view.zoom;
    const ox = state.view.panX % step;
    const oy = state.view.panY % step;
    for (let x = ox; x < w; x += step) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke(); }
    for (let y = oy; y < h; y += step) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke(); }
    return;
  }
  // Fit whole map into the view by default, then apply pan/zoom.
  const base = Math.min(w / img.width, h / img.height);
  const s = base * state.view.zoom;
  const dw = img.width * s, dh = img.height * s;
  const dx = (w - dw) / 2 + state.view.panX;
  const dy = (h - dh) / 2 + state.view.panY;
  ctx.drawImage(img, dx, dy, dw, dh);
}

function drawPath() {
  const fs = state.drivingFrames;
  if (fs.length < 2) return;
  ctx.lineWidth = 2;
  ctx.strokeStyle = 'rgba(79,176,255,0.85)';
  ctx.beginPath();
  let started = false;
  for (const f of fs) {
    const [px, py] = worldToScreen(f.x, f.z);
    if (!started) { ctx.moveTo(px, py); started = true; }
    else ctx.lineTo(px, py);
  }
  ctx.stroke();

  // start/end markers
  const a = fs[0], b = fs[fs.length - 1];
  dot(worldToScreen(a.x, a.z), '#4cd07d', 4);
  dot(worldToScreen(b.x, b.z), '#ff5d5d', 4);
}

function dot([px, py], color, r) {
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.arc(px, py, r, 0, Math.PI * 2);
  ctx.fill();
}

function drawMarker() {
  const f = currentFrame();
  if (!f) return;
  const [px, py] = worldToScreen(f.x, f.z);

  // heading from previous frame for a direction triangle
  const prev = prevPositionFrame(f);
  let ang = 0;
  if (prev) {
    let dx = f.x - prev.x;
    let dz = f.z - prev.z;
    if (state.cal.flipZ) dz = -dz;
    const rot = (state.cal.rot * Math.PI) / 180;
    const rdx = dx * Math.cos(rot) - dz * Math.sin(rot);
    const rdz = dx * Math.sin(rot) + dz * Math.cos(rot);
    if (rdx !== 0 || rdz !== 0) ang = Math.atan2(rdz, rdx);
  }

  ctx.save();
  ctx.translate(px, py);
  ctx.rotate(ang);
  ctx.fillStyle = '#ffffff';
  ctx.strokeStyle = '#14181d';
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.moveTo(10, 0);
  ctx.lineTo(-6, 6);
  ctx.lineTo(-6, -6);
  ctx.closePath();
  ctx.fill();
  ctx.stroke();
  ctx.restore();
}

// ---- Frame lookup ----------------------------------------------------------

function currentFrame() {
  const fs = state.frames;
  if (!fs.length) return null;
  // binary search for last frame with t <= curT
  let lo = 0, hi = fs.length - 1, ans = 0;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (fs[mid].t <= state.curT) { ans = mid; lo = mid + 1; }
    else hi = mid - 1;
  }
  return fs[ans];
}

function prevPositionFrame(f) {
  const fs = state.drivingFrames;
  const idx = fs.indexOf(f);
  if (idx > 0) return fs[idx - 1];
  // f may not be in drivingFrames; find nearest earlier driving frame
  for (let i = fs.length - 1; i >= 0; i--) {
    if (fs[i].t < f.t) return fs[i];
  }
  return null;
}

// ---- Telemetry readout -----------------------------------------------------

function updateReadout() {
  const f = currentFrame();
  if (!f) return;
  const kmh = f.speed * 3.6;
  const mph = f.speed * 2.236936;
  $('mSpeed').textContent = `${kmh.toFixed(0)} km/h · ${mph.toFixed(0)} mph`;
  $('mGear').textContent = gearLabel(f.gear);
  $('mRpm').textContent = `${Math.round(f.rpm)}`;
  const thrPct = (f.throttle / 255) * 100;
  const brkPct = (f.brake / 255) * 100;
  $('mThrottle').textContent = `${thrPct.toFixed(0)}%`;
  $('mBrake').textContent = `${brkPct.toFixed(0)}%`;
  $('mSteer').textContent = `${f.steer}`;
  $('mRaceOn').textContent = f.raceOn ? 'yes' : 'no';
  $('mPos').textContent = `${f.x.toFixed(0)}, ${f.z.toFixed(0)}`;

  $('barThrottle').style.width = `${thrPct}%`;
  $('barBrake').style.width = `${brkPct}%`;
  const rpmPct = f.maxRpm > 0 ? Math.min(100, (f.rpm / f.maxRpm) * 100) : 0;
  $('barRpm').style.width = `${rpmPct}%`;
}

function gearLabel(g) {
  if (g === 0) return 'R';
  if (g === 11) return 'N'; // some titles use high value for neutral; show raw otherwise
  return String(g);
}

// ---- Timeline / playback ---------------------------------------------------

function updateTimeline() {
  const dur = state.endT - state.startT;
  const pos = dur > 0 ? ((state.curT - state.startT) / dur) * 1000 : 0;
  $('timeline').value = String(pos);
  const cur = (state.curT - state.startT) / 1000;
  const total = dur / 1000;
  $('timeLabel').textContent = `${cur.toFixed(1)}s / ${total.toFixed(1)}s`;
}

function tick(ts) {
  if (!state.lastRaf) state.lastRaf = ts;
  const dtMs = ts - state.lastRaf;
  state.lastRaf = ts;

  if (state.playing) {
    state.curT += dtMs * state.speed;
    if (state.curT >= state.endT) {
      state.curT = state.endT;
      state.playing = false;
      $('btnPlay').textContent = '▶';
    }
    updateTimeline();
  }
  updateReadout();
  draw();
  requestAnimationFrame(tick);
}

function togglePlay() {
  if (!state.frames.length) return;
  if (state.curT >= state.endT) state.curT = state.startT; // restart from end
  state.playing = !state.playing;
  $('btnPlay').textContent = state.playing ? '❚❚' : '▶';
}

// ---- Calibration -----------------------------------------------------------

function resetCalibration(redraw = true) {
  state.cal = { scale: 1, offX: 0, offY: 0, rot: 0, flipZ: state.cal.flipZ };
  $('cScale').value = '1'; $('vScale').textContent = '1.00';
  $('cOffX').value = '0'; $('vOffX').textContent = '0';
  $('cOffY').value = '0'; $('vOffY').textContent = '0';
  $('cRot').value = '0'; $('vRot').textContent = '0';
  if (redraw) draw();
}

// ---- Events ----------------------------------------------------------------

function bindEvents() {
  $('seasonSelect').addEventListener('change', (e) => setSeason(e.target.value));

  $('captureSelect').addEventListener('change', async (e) => {
    const name = e.target.value;
    if (!name) return;
    const res = await fetch(`/api/capture/${encodeURIComponent(name)}`);
    const text = await res.text();
    loadFrames(parseCapture(text), name);
  });

  $('fileInput').addEventListener('change', (e) => {
    const file = e.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => loadFrames(parseCapture(reader.result), file.name);
    reader.readAsText(file);
  });

  $('btnPlay').addEventListener('click', togglePlay);

  $('timeline').addEventListener('input', (e) => {
    const dur = state.endT - state.startT;
    state.curT = state.startT + (Number(e.target.value) / 1000) * dur;
    updateTimeline();
    updateReadout();
    draw();
  });

  $('speedSelect').addEventListener('change', (e) => { state.speed = Number(e.target.value); });

  // calibration sliders
  $('cScale').addEventListener('input', (e) => { state.cal.scale = Number(e.target.value); $('vScale').textContent = state.cal.scale.toFixed(2); draw(); });
  $('cOffX').addEventListener('input', (e) => { state.cal.offX = Number(e.target.value); $('vOffX').textContent = e.target.value; draw(); });
  $('cOffY').addEventListener('input', (e) => { state.cal.offY = Number(e.target.value); $('vOffY').textContent = e.target.value; draw(); });
  $('cRot').addEventListener('input', (e) => { state.cal.rot = Number(e.target.value); $('vRot').textContent = e.target.value; draw(); });
  $('cFlipZ').addEventListener('change', (e) => { state.cal.flipZ = e.target.checked; draw(); });
  $('btnRefit').addEventListener('click', () => { autoFit(); state.view = { panX: 0, panY: 0, zoom: 1 }; draw(); });
  $('btnResetCal').addEventListener('click', () => resetCalibration(true));

  // pan + zoom on canvas
  let dragging = false, lastX = 0, lastY = 0;
  canvas.addEventListener('mousedown', (e) => { dragging = true; lastX = e.clientX; lastY = e.clientY; });
  window.addEventListener('mouseup', () => { dragging = false; });
  window.addEventListener('mousemove', (e) => {
    if (!dragging) return;
    state.view.panX += e.clientX - lastX;
    state.view.panY += e.clientY - lastY;
    lastX = e.clientX; lastY = e.clientY;
    draw();
  });
  canvas.addEventListener('wheel', (e) => {
    e.preventDefault();
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left, my = e.clientY - rect.top;
    const cx = canvas.clientWidth / 2, cy = canvas.clientHeight / 2;
    const factor = e.deltaY < 0 ? 1.1 : 1 / 1.1;
    // zoom toward cursor: keep point under cursor fixed
    const before = state.view.zoom;
    const after = Math.max(0.05, Math.min(50, before * factor));
    const ratio = after / before;
    // pan such that cursor stays anchored relative to center
    state.view.panX = (state.view.panX + cx - mx) * ratio - cx + mx;
    state.view.panY = (state.view.panY + cy - my) * ratio - cy + my;
    state.view.zoom = after;
    draw();
  }, { passive: false });

  window.addEventListener('resize', () => { resizeCanvas(); draw(); });

  window.addEventListener('keydown', (e) => {
    if (e.code === 'Space') { e.preventDefault(); togglePlay(); }
  });
}

// ---- Boot ------------------------------------------------------------------

function init() {
  resizeCanvas();
  bindEvents();
  loadMaps();
  listCaptures();
  requestAnimationFrame(tick);
}

init();
