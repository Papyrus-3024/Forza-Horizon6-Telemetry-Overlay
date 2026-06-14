// Vertical stack of dependency-free canvas mini time-charts sharing one X (time)
// axis. Each chart plots a derived signal vs frame.t; a crosshair tracks the
// cursor and click/drag on any chart seeks. Lap boundaries draw as faint guides.

// Palette mirrors style.css so the charts match the rest of the dark theme.
const COL = {
  bg: '#0e1115',
  panel: '#1c2128',
  line: '#2d3742',
  text: '#d7dee6',
  muted: '#8a97a5',
  accent: '#4fb0ff',
  green: '#4cd07d',
  red: '#ff5d5d',
  amber: '#ffc94d',
};

// Chart geometry (CSS px).
const ROW_H = 78;        // height of one mini-chart row
const PAD_L = 48;        // left gutter for the y-axis label/value
const PAD_R = 12;
const PAD_T = 16;        // top room for the chart title
const PAD_B = 6;

// Each series describes one row: how to pull a value from a frame, its colour,
// and a fixed or auto y-range. `range` null means auto-fit from the data.
// decision: fixed ranges for steer/throttle/brake/G — reason: keeps the
// baseline stable across captures so shapes are comparable; speed/rpm auto-fit
// since their scale varies wildly between cars/drives.
const SERIES = [
  {
    key: 'speed', title: 'Speed (km/h)', color: COL.accent, range: null,
    get: (f) => f.speed * 3.6, fmt: (v) => v.toFixed(0),
  },
  {
    key: 'rpm', title: 'RPM', color: COL.amber, range: null,
    get: (f) => f.rpm, fmt: (v) => Math.round(v).toString(),
  },
  {
    // Two overlaid traces share one row; drawn specially below.
    key: 'pedals', title: 'Throttle / Brake (%)', range: [0, 100],
    traces: [
      { color: COL.green, get: (f) => (f.throttle / 255) * 100 },
      { color: COL.red, get: (f) => (f.brake / 255) * 100 },
    ],
    get: (f) => (f.throttle / 255) * 100,
    fmt: (v) => v.toFixed(0),
  },
  {
    key: 'steer', title: 'Steer', color: COL.text, range: [-127, 127],
    get: (f) => f.steer, fmt: (v) => v.toFixed(0),
  },
  {
    // Lateral and longitudinal G overlaid.
    key: 'accG', title: 'Lat / Long G', range: [-2, 2],
    traces: [
      { color: COL.accent, get: (f) => f.accX / 9.80665 },
      { color: COL.amber, get: (f) => f.accZ / 9.80665 },
    ],
    get: (f) => f.accX / 9.80665,
    fmt: (v) => v.toFixed(2),
  },
];

export function createChartPanel(container) {
  const canvas = document.createElement('canvas');
  canvas.style.display = 'block';
  canvas.style.width = '100%';
  canvas.style.height = '100%';
  canvas.style.cursor = 'crosshair';
  container.appendChild(canvas);
  const ctx = canvas.getContext('2d');

  const state = {
    frames: [],
    laps: [],
    startT: 0,
    endT: 1,
    curT: 0,
    seekCb: null,
    // Per-series computed ranges + cached pixel polylines (rebuilt on setFrames
    // / resize). Caching the polylines keeps redraws on every cursor tick cheap.
    rows: [],
    w: 0,
    h: 0,
  };

  // ---- Public API ----------------------------------------------------------

  function setFrames(frames) {
    state.frames = frames || [];
    if (state.frames.length) {
      state.startT = state.frames[0].t;
      state.endT = state.frames[state.frames.length - 1].t;
      if (state.endT <= state.startT) state.endT = state.startT + 1;
      state.curT = state.startT;
    } else {
      state.startT = 0;
      state.endT = 1;
      state.curT = 0;
    }
    rebuild();
  }

  function setLaps(laps) {
    state.laps = laps || [];
    draw();
  }

  function setCursor(t) {
    state.curT = t;
    draw();
  }

  function onSeek(cb) {
    state.seekCb = cb;
  }

  // ---- Layout / scaling ----------------------------------------------------

  function resize() {
    const dpr = window.devicePixelRatio || 1;
    const w = container.clientWidth || 600;
    const h = Math.max(container.clientHeight || 0, SERIES.length * ROW_H);
    canvas.width = Math.round(w * dpr);
    canvas.height = Math.round(h * dpr);
    canvas.style.height = `${h}px`;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    state.w = w;
    state.h = h;
    rebuild();
  }

  // Map capture time -> x pixel within the plot area.
  function tToX(t) {
    const span = state.endT - state.startT;
    const frac = span > 0 ? (t - state.startT) / span : 0;
    return PAD_L + frac * (state.w - PAD_L - PAD_R);
  }

  // Map x pixel -> capture time (for seeking).
  function xToT(x) {
    const plotW = state.w - PAD_L - PAD_R;
    const frac = plotW > 0 ? (x - PAD_L) / plotW : 0;
    const clamped = Math.max(0, Math.min(1, frac));
    return state.startT + clamped * (state.endT - state.startT);
  }

  // Recompute each row's y-range and pre-build pixel polylines for its traces.
  function rebuild() {
    const fs = state.frames;
    const plotW = state.w - PAD_L - PAD_R;
    state.rows = SERIES.map((s, i) => {
      const top = i * ROW_H + PAD_T;
      const bottom = (i + 1) * ROW_H - PAD_B;
      const traces = s.traces || [{ color: s.color, get: s.get }];

      // Resolve y-range: fixed if declared, else auto-fit across all traces.
      let lo, hi;
      if (s.range) {
        [lo, hi] = s.range;
      } else {
        lo = Infinity; hi = -Infinity;
        for (const f of fs) {
          for (const tr of traces) {
            const v = tr.get(f);
            if (Number.isFinite(v)) {
              if (v < lo) lo = v;
              if (v > hi) hi = v;
            }
          }
        }
        if (!Number.isFinite(lo) || !Number.isFinite(hi)) { lo = 0; hi = 1; }
        if (hi - lo < 1e-6) hi = lo + 1;
        // decision: pad auto-ranges by 5% — reason: stops peaks touching the
        // row edge so the trace stays readable.
        const pad = (hi - lo) * 0.05;
        lo -= pad; hi += pad;
      }

      const yOf = (v) => {
        const frac = (v - lo) / (hi - lo);
        return bottom - frac * (bottom - top);
      };

      // Build a pixel polyline per trace once; redraws just stroke them.
      const polylines = traces.map((tr) => {
        const pts = new Float32Array(fs.length * 2);
        for (let j = 0; j < fs.length; j++) {
          pts[j * 2] = tToX(fs[j].t);
          pts[j * 2 + 1] = yOf(tr.get(fs[j]));
        }
        return { color: tr.color, pts };
      });

      return { series: s, top, bottom, lo, hi, yOf, polylines, plotW };
    });
    draw();
  }

  // ---- Drawing -------------------------------------------------------------

  function draw() {
    const { w, h } = state;
    if (!w || !h) return;

    ctx.clearRect(0, 0, w, h);
    ctx.fillStyle = COL.bg;
    ctx.fillRect(0, 0, w, h);

    if (!state.frames.length) {
      ctx.fillStyle = COL.muted;
      ctx.font = '13px system-ui, -apple-system, "Segoe UI", sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText('Load a capture to see charts.', w / 2, h / 2);
      return;
    }

    const cursorFrame = frameAtCursor();
    const cursorX = tToX(state.curT);

    for (const row of state.rows) {
      drawRow(row, cursorFrame, cursorX);
    }

    drawCrosshair(cursorX);
  }

  function drawRow(row, cursorFrame, cursorX) {
    const { series, top, bottom } = row;
    const right = state.w - PAD_R;

    // Row baseline + frame.
    ctx.strokeStyle = COL.line;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(PAD_L, bottom + 0.5);
    ctx.lineTo(right, bottom + 0.5);
    ctx.stroke();

    // Zero line for signed signals (steer, G) sitting inside the range.
    if (row.lo < 0 && row.hi > 0) {
      const zy = Math.round(row.yOf(0)) + 0.5;
      ctx.strokeStyle = 'rgba(255,255,255,0.07)';
      ctx.beginPath();
      ctx.moveTo(PAD_L, zy);
      ctx.lineTo(right, zy);
      ctx.stroke();
    }

    // Lap boundary guides.
    drawLapGuides(top, bottom);

    // Series traces.
    ctx.lineWidth = 1.5;
    ctx.lineJoin = 'round';
    for (const pl of row.polylines) {
      const pts = pl.pts;
      if (pts.length < 4) continue;
      ctx.strokeStyle = pl.color;
      ctx.beginPath();
      ctx.moveTo(pts[0], pts[1]);
      for (let i = 2; i < pts.length; i += 2) ctx.lineTo(pts[i], pts[i + 1]);
      ctx.stroke();
    }

    // Title (top-left).
    ctx.fillStyle = COL.muted;
    ctx.font = '10px system-ui, -apple-system, "Segoe UI", sans-serif';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';
    ctx.fillText(series.title.toUpperCase(), PAD_L, top - 4);

    // Value(s) at the cursor, top-right.
    if (cursorFrame) {
      const traces = series.traces || [{ color: series.color, get: series.get }];
      ctx.textAlign = 'right';
      ctx.font = '11px system-ui, -apple-system, "Segoe UI", sans-serif';
      const parts = traces.map((tr) => series.fmt(tr.get(cursorFrame)));
      // decision: colour each value to match its trace — reason: in overlaid
      // rows (pedals, G) it disambiguates which number is which without a key.
      let x = state.w - PAD_R;
      for (let i = traces.length - 1; i >= 0; i--) {
        ctx.fillStyle = traces[i].color;
        ctx.fillText(parts[i], x, top - 4);
        x -= ctx.measureText(parts[i]).width + 8;
        if (i > 0) {
          ctx.fillStyle = COL.muted;
          ctx.fillText('/', x, top - 4);
          x -= ctx.measureText('/').width + 8;
        }
      }
    }
  }

  function drawLapGuides(top, bottom) {
    if (!state.laps.length) return;
    ctx.strokeStyle = 'rgba(255,255,255,0.10)';
    ctx.lineWidth = 1;
    for (const lap of state.laps) {
      // decision: guide at each lap start only — reason: consecutive laps share
      // a boundary, so drawing starts avoids doubled lines.
      const x = Math.round(tToX(lap.startT)) + 0.5;
      ctx.beginPath();
      ctx.moveTo(x, top);
      ctx.lineTo(x, bottom);
      ctx.stroke();
    }
  }

  function drawCrosshair(cursorX) {
    const x = Math.round(cursorX) + 0.5;
    ctx.strokeStyle = 'rgba(79,176,255,0.55)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(x, PAD_T - 4);
    ctx.lineTo(x, state.h);
    ctx.stroke();
  }

  // ---- Cursor frame lookup -------------------------------------------------
  // Binary search for the last frame with t <= curT (matches frameAt in spec).

  function frameAtCursor() {
    const fs = state.frames;
    if (!fs.length) return null;
    if (state.curT <= fs[0].t) return fs[0];
    let lo = 0, hi = fs.length - 1, ans = 0;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      if (fs[mid].t <= state.curT) { ans = mid; lo = mid + 1; }
      else hi = mid - 1;
    }
    return fs[ans];
  }

  // ---- Seek interaction ----------------------------------------------------

  function emitSeekFromEvent(e) {
    if (!state.seekCb || !state.frames.length) return;
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    state.seekCb(xToT(x));
  }

  let dragging = false;
  canvas.addEventListener('mousedown', (e) => {
    dragging = true;
    emitSeekFromEvent(e);
  });
  window.addEventListener('mousemove', (e) => {
    if (dragging) emitSeekFromEvent(e);
  });
  window.addEventListener('mouseup', () => { dragging = false; });

  resize();

  return { setFrames, setLaps, setCursor, onSeek, resize };
}
