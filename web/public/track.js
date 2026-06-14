// Track path view: auto-fits the world X/Z driving line into the canvas, draws
// start/end markers, and a car marker oriented by the frame yaw. Self-contained:
// owns its own pan/zoom and world->screen math (no shared app state).

export function createTrack(canvas) {
  const ctx = canvas.getContext('2d');

  const state = {
    frames: [],          // all frames, sorted by t
    pathFrames: [],       // subset with a usable position, drawn as the line
    bounds: null,         // {minX,maxX,minZ,maxZ} of pathFrames
    fitScale: 1,          // world->px from auto-fit
    fitCenter: { x: 0, z: 0 },
    view: { panX: 0, panY: 0, zoom: 1 },
    mapImg: null,
    cursorT: 0,
  };

  // ---- World -> screen -----------------------------------------------------
  // world X -> screen X (right), world Z -> screen Y. We negate Z so that
  // increasing Z points up on screen, matching the usual map convention where
  // "forward/north" is up.
  // decision: flip world Z to screen-up — Forza ground plane is X/Z; negating Z
  // makes the track read like a map (north up) instead of mirrored vertically.

  function worldToScreen(x, z) {
    const cx = (canvas.clientWidth || 800) / 2;
    const cy = (canvas.clientHeight || 600) / 2;
    const s = state.fitScale * state.view.zoom;
    const px = (x - state.fitCenter.x) * s + state.view.panX + cx;
    const py = -(z - state.fitCenter.z) * s + state.view.panY + cy;
    return [px, py];
  }

  function hasPosition(f) {
    // skip menu/idle frames sitting at the world origin
    return f.x !== 0 || f.z !== 0;
  }

  function computeBounds() {
    const fs = state.pathFrames;
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

  // Auto-fit the world bbox into the canvas with a margin.
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

  function frameAtCursor() {
    const fs = state.frames;
    if (!fs.length) return null;
    let lo = 0, hi = fs.length - 1, ans = 0;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      if (fs[mid].t <= state.cursorT) { ans = mid; lo = mid + 1; }
      else hi = mid - 1;
    }
    return fs[ans];
  }

  // ---- Drawing -------------------------------------------------------------

  function draw() {
    const w = canvas.clientWidth, h = canvas.clientHeight;
    ctx.clearRect(0, 0, w, h);

    ctx.fillStyle = '#0e1115';
    ctx.fillRect(0, 0, w, h);

    drawBackdrop();

    if (!state.frames.length) {
      drawHint('Load a capture to see the track');
      return;
    }
    if (state.pathFrames.length < 2) {
      drawHint('No position data in this capture');
      return;
    }

    drawPath();
    drawEndpoints();
    drawCar();
  }

  function drawHint(text) {
    const w = canvas.clientWidth, h = canvas.clientHeight;
    ctx.fillStyle = '#8a97a5';
    ctx.font = '13px system-ui, "Segoe UI", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, w / 2, h / 2);
    ctx.textAlign = 'start';
    ctx.textBaseline = 'alphabetic';
  }

  // Map image (when supplied) sits under the path; otherwise a neutral grid.
  // The grid scrolls with pan/zoom so the path reads as moving over a surface.
  function drawBackdrop() {
    const w = canvas.clientWidth, h = canvas.clientHeight;
    const img = state.mapImg;
    if (img) {
      const base = Math.min(w / img.width, h / img.height);
      const s = base * state.view.zoom;
      const dw = img.width * s, dh = img.height * s;
      const dx = (w - dw) / 2 + state.view.panX;
      const dy = (h - dh) / 2 + state.view.panY;
      ctx.drawImage(img, dx, dy, dw, dh);
      return;
    }
    ctx.strokeStyle = 'rgba(255,255,255,0.04)';
    ctx.lineWidth = 1;
    const step = 64 * state.view.zoom;
    if (step < 4) return;
    const ox = ((state.view.panX % step) + step) % step;
    const oy = ((state.view.panY % step) + step) % step;
    for (let x = ox; x < w; x += step) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke(); }
    for (let y = oy; y < h; y += step) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke(); }
  }

  function drawPath() {
    const fs = state.pathFrames;
    ctx.lineWidth = 2;
    ctx.lineJoin = 'round';
    ctx.strokeStyle = 'rgba(79,176,255,0.85)';
    ctx.beginPath();
    let started = false;
    for (const f of fs) {
      const [px, py] = worldToScreen(f.x, f.z);
      if (!started) { ctx.moveTo(px, py); started = true; }
      else ctx.lineTo(px, py);
    }
    ctx.stroke();
  }

  function drawEndpoints() {
    const fs = state.pathFrames;
    const a = fs[0], b = fs[fs.length - 1];
    dot(worldToScreen(a.x, a.z), '#4cd07d', 5);
    dot(worldToScreen(b.x, b.z), '#ff5d5d', 5);
  }

  function dot([px, py], color, r) {
    ctx.fillStyle = color;
    ctx.strokeStyle = '#14181d';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.arc(px, py, r, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  }

  function drawCar() {
    const f = frameAtCursor();
    if (!f) return;
    const [px, py] = worldToScreen(f.x, f.z);

    // Map yaw (radians) to a screen heading. yaw=0 means the car faces +Z; with
    // Z flipped to screen-up that is straight up (-Y on screen). Increasing yaw
    // turns the car toward +X (screen right). canvas rotate() is clockwise from
    // +X, so a screen angle measured from up going right is (yaw - 90deg).
    // decision: screenAngle = yaw - PI/2 — pairs yaw=0 (facing +Z) with the
    // upward, north-up orientation used for the path; turning right increases yaw.
    const screenAngle = f.yaw - Math.PI / 2;

    ctx.save();
    ctx.translate(px, py);
    ctx.rotate(screenAngle);
    ctx.fillStyle = '#ffffff';
    ctx.strokeStyle = '#14181d';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(0, -11);   // nose points along the heading
    ctx.lineTo(7, 7);
    ctx.lineTo(-7, 7);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.restore();
  }

  // ---- Pan / zoom ----------------------------------------------------------

  function bindPanZoom() {
    let dragging = false, lastX = 0, lastY = 0;
    canvas.addEventListener('mousedown', (e) => {
      dragging = true; lastX = e.clientX; lastY = e.clientY;
    });
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
      const before = state.view.zoom;
      const after = Math.max(0.05, Math.min(50, before * factor));
      const ratio = after / before;
      // keep the point under the cursor anchored while zooming
      state.view.panX = (state.view.panX + cx - mx) * ratio - cx + mx;
      state.view.panY = (state.view.panY + cy - my) * ratio - cy + my;
      state.view.zoom = after;
      draw();
    }, { passive: false });
  }

  // ---- Canvas sizing -------------------------------------------------------

  function resize() {
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.round((canvas.clientWidth || 800) * dpr);
    canvas.height = Math.round((canvas.clientHeight || 600) * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    autoFit();
    draw();
  }

  // ---- Public API ----------------------------------------------------------

  function setFrames(frames) {
    state.frames = frames || [];
    state.pathFrames = state.frames.filter(hasPosition);
    if (state.pathFrames.length === 0) state.pathFrames = state.frames.slice();
    state.cursorT = state.frames.length ? state.frames[0].t : 0;
    state.view = { panX: 0, panY: 0, zoom: 1 };
    computeBounds();
    autoFit();
    draw();
  }

  function setCursor(t) {
    state.cursorT = t;
    draw();
  }

  function setMap(img) {
    state.mapImg = img || null;
    draw();
  }

  resize();
  bindPanZoom();

  return { setFrames, setCursor, setMap, resize };
}
