import { parseCapture, frameAt } from './parser.js';
import { computeLaps } from './laps.js';
import { createChartPanel } from './charts.js';
import { createTrack } from './track.js';
import { createReadout } from './readout.js';
import { createTransport } from './playback.js';

const $ = (id) => document.getElementById(id);

// ---- View instances --------------------------------------------------------

const track = createTrack($('trackCanvas'));
const charts = createChartPanel($('chartStack'));
const readout = createReadout($('readout'));
const transport = createTransport({
  playBtn: $('btnPlay'),
  timeline: $('timeline'),
  timeLabel: $('timeLabel'),
  speedSelect: $('speedSelect'),
});

const app = {
  frames: [],
  laps: [],
};

// ---- Wiring ----------------------------------------------------------------

// One cursor source of truth: the transport drives every view.
transport.onCursor((t) => {
  const f = frameAt(app.frames, t);
  readout.update(f);
  track.setCursor(t);
  charts.setCursor(t);
});

// A chart click/drag is a user seek.
charts.onSeek((t) => transport.seek(t));

// Spacebar toggles play unless the user is typing in a control.
window.addEventListener('keydown', (e) => {
  if (e.code !== 'Space') return;
  const tag = (e.target.tagName || '').toLowerCase();
  if (tag === 'input' || tag === 'select' || tag === 'textarea') return;
  e.preventDefault();
  transport.toggle();
});

window.addEventListener('resize', () => {
  track.resize();
  charts.resize();
});

// ---- Capture loading -------------------------------------------------------

function loadFrames(frames, label) {
  app.frames = frames || [];
  const { laps } = computeLaps(app.frames);
  app.laps = laps;

  track.setFrames(app.frames);
  charts.setFrames(app.frames);
  charts.setLaps(app.laps);
  buildLapSelector(app.laps);

  const startT = app.frames.length ? app.frames[0].t : 0;
  const endT = app.frames.length ? app.frames[app.frames.length - 1].t : 0;
  transport.setRange(startT, endT);

  const dur = (endT - startT) / 1000;
  const info = $('captureInfo');
  if (info) {
    info.textContent = app.frames.length
      ? `${label}: ${app.frames.length} frames, ${dur.toFixed(1)}s, ${laps.length} lap(s)`
      : `${label}: no usable frames`;
  }
}

async function listCaptures() {
  const sel = $('captureSelect');
  if (!sel) return [];
  try {
    const res = await fetch('/api/captures');
    const names = await res.json();
    for (const name of names) {
      const opt = document.createElement('option');
      opt.value = name;
      opt.textContent = name;
      sel.appendChild(opt);
    }
    return names;
  } catch {
    return [];
  }
}

// Fetch a bundled capture. .bin comes back as bytes, everything else as text;
// parseCapture auto-detects either way.
async function loadBundled(name) {
  const res = await fetch(`/api/capture/${encodeURIComponent(name)}`);
  // decision: branch on the .bin extension to pick arrayBuffer vs text — reason:
  // reading a binary body as text would corrupt non-latin1 bytes; the server
  // sends .bin as application/octet-stream.
  const input = name.toLowerCase().endsWith('.bin')
    ? new Uint8Array(await res.arrayBuffer())
    : await res.text();
  loadFrames(parseCapture(input), name);
}

function bindCaptureControls() {
  const sel = $('captureSelect');
  if (sel) {
    sel.addEventListener('change', (e) => {
      const name = e.target.value;
      if (name) loadBundled(name);
    });
  }

  const fileInput = $('fileInput');
  if (fileInput) {
    fileInput.addEventListener('change', (e) => {
      const file = e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      // decision: read .bin via ArrayBuffer, text formats via text — reason:
      // mirrors the bundled path so both inputs feed parseCapture the right shape.
      if (file.name.toLowerCase().endsWith('.bin')) {
        reader.onload = () =>
          loadFrames(parseCapture(new Uint8Array(reader.result)), file.name);
        reader.readAsArrayBuffer(file);
      } else {
        reader.onload = () => loadFrames(parseCapture(reader.result), file.name);
        reader.readAsText(file);
      }
    });
  }
}

// ---- Lap selector ----------------------------------------------------------

function buildLapSelector(laps) {
  const sel = $('lapSelect');
  if (!sel) return;
  sel.innerHTML = '';
  const head = document.createElement('option');
  head.value = '';
  head.textContent = laps.length ? `${laps.length} lap(s)` : 'No laps';
  sel.appendChild(head);
  for (const lap of laps) {
    const opt = document.createElement('option');
    opt.value = String(lap.index);
    const secs = (lap.durationMs / 1000).toFixed(1);
    opt.textContent = `Lap ${lap.lapNumber} — ${secs}s`;
    sel.appendChild(opt);
  }
}

function bindLapSelector() {
  const sel = $('lapSelect');
  if (!sel) return;
  sel.addEventListener('change', (e) => {
    const idx = Number(e.target.value);
    const lap = app.laps[idx];
    if (lap) transport.seek(lap.startT);
  });
}

// ---- Optional season map ---------------------------------------------------

// Only load a backdrop if /api/maps reports one present; otherwise stay path-only
// with no nag (the spec treats maps as a bonus, not a requirement).
async function loadOptionalMap() {
  const sel = $('seasonSelect');
  let present = {};
  try {
    const res = await fetch('/api/maps');
    present = await res.json();
  } catch {
    present = {};
  }
  const apply = (season) => {
    if (!present[season]) {
      track.setMap(null);
      return;
    }
    const img = new Image();
    img.onload = () => track.setMap(img);
    img.onerror = () => track.setMap(null);
    img.src = `/maps/${season}.avif`;
  };
  if (sel) {
    sel.addEventListener('change', (e) => apply(e.target.value));
    apply(sel.value);
  }
}

// ---- Boot ------------------------------------------------------------------

async function init() {
  bindCaptureControls();
  bindLapSelector();
  loadOptionalMap();
  const names = await listCaptures();

  // ?capture=<name> auto-loads a bundled capture on boot.
  // decision: honour a capture query param — reason: enables headless screenshot
  // verification and shareable deep links to a specific drive.
  const wanted = new URLSearchParams(location.search).get('capture');
  if (wanted && names.includes(wanted)) {
    const sel = $('captureSelect');
    if (sel) sel.value = wanted;
    await loadBundled(wanted);
  }
}

init();
