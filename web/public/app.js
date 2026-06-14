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

// ---- Map backdrop ----------------------------------------------------------

// Remote FH6 map images, keyed by the map-style selector. The seasonal maps are
// the full-island top-down renders; "road" is an alternative Google-style road
// map (incomplete — see the calibration TODO in track.js).
const MAP_SOURCES = {
  spring: 'https://cdn.leox.dev/fh6/map/spring.avif',
  summer: 'https://cdn.leox.dev/fh6/map/summer.avif',
  autumn: 'https://cdn.leox.dev/fh6/map/autumn.avif',
  winter: 'https://cdn.leox.dev/fh6/map/winter.avif',
  road: 'https://i.imgur.com/wG2Kgx7.jpeg',
};

// Swap the backdrop to the chosen style. Remote loads are async, so a stale
// request finishing after a newer pick must not clobber the current map.
function loadMap() {
  const sel = $('mapSelect');
  let token = 0;
  const apply = (style) => {
    const url = MAP_SOURCES[style];
    if (!url) {
      track.setMap(null);
      return;
    }
    const mine = ++token;
    const img = new Image();
    img.crossOrigin = 'anonymous'; // keep the canvas un-tainted for any future pixel readback
    img.onload = () => { if (mine === token) track.setMap(img); };
    img.onerror = () => { if (mine === token) track.setMap(null); };
    img.src = url;
  };
  if (sel) {
    // ?style=<name> deep-links a starting map (mirrors ?capture=), which also
    // lets headless screenshots pick the lighter road map for quick verification.
    const wanted = new URLSearchParams(location.search).get('style');
    if (wanted && MAP_SOURCES[wanted]) sel.value = wanted;
    sel.addEventListener('change', (e) => apply(e.target.value));
    apply(sel.value);
  }
}

// ---- Boot ------------------------------------------------------------------

async function init() {
  bindCaptureControls();
  bindLapSelector();
  loadMap();
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
