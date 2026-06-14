// Transport controller: owns play state, playback speed, and a single rAF loop
// that advances a cursor across the capture's time range. Emits onCursor(t) on
// every tick and seek so the views (track / charts / readout) stay in sync.

export function createTransport({ playBtn, timeline, timeLabel, speedSelect }) {
  const state = {
    startT: 0,
    endT: 0,
    curT: 0,
    speed: 1,
    playing: false,
    lastRaf: 0,
    cursorCb: null,
  };

  // ---- Emit / formatting -----------------------------------------------------

  function emit() {
    if (state.cursorCb) state.cursorCb(state.curT);
  }

  function fmtTime(ms) {
    return (ms / 1000).toFixed(1);
  }

  // Reflect curT onto the timeline range (0..1000) + the time label.
  function syncUi() {
    const dur = state.endT - state.startT;
    const pos = dur > 0 ? ((state.curT - state.startT) / dur) * 1000 : 0;
    if (timeline) timeline.value = String(pos);
    if (timeLabel) {
      timeLabel.textContent = `${fmtTime(state.curT - state.startT)}s / ${fmtTime(dur)}s`;
    }
  }

  function setPlayBtn() {
    if (playBtn) playBtn.textContent = state.playing ? '❚❚' : '▶';
  }

  // ---- Public API ------------------------------------------------------------

  function setRange(startT, endT) {
    state.startT = startT || 0;
    state.endT = endT > startT ? endT : startT;
    state.curT = state.startT;
    state.playing = false;
    setPlayBtn();
    syncUi();
    emit();
  }

  // Programmatic move: update curT + UI but no extra echo beyond the cursor emit.
  function setCursor(t) {
    state.curT = clamp(t);
    syncUi();
    emit();
  }

  // User-initiated move: same effect as setCursor; the distinct name lets callers
  // (charts seek, lap selector) signal intent without risking a feedback loop —
  // decision: seek() and setCursor() share a body but stay separate so a future
  // change (e.g. pause-on-seek) can diverge without touching call sites.
  function seek(t) {
    state.curT = clamp(t);
    syncUi();
    emit();
  }

  function clamp(t) {
    if (t < state.startT) return state.startT;
    if (t > state.endT) return state.endT;
    return t;
  }

  function play() {
    if (state.endT <= state.startT) return;
    // decision: restart from the beginning when starting playback at the very end
    // — reason: avoids an immediate stall when the user hits play after a full run.
    if (state.curT >= state.endT) state.curT = state.startT;
    state.playing = true;
    state.lastRaf = 0;
    setPlayBtn();
  }

  function pause() {
    state.playing = false;
    setPlayBtn();
  }

  function toggle() {
    if (state.playing) pause();
    else play();
  }

  function onCursor(cb) {
    state.cursorCb = cb;
  }

  // ---- rAF loop --------------------------------------------------------------

  function tick(ts) {
    if (state.playing) {
      if (!state.lastRaf) state.lastRaf = ts;
      const dtMs = ts - state.lastRaf;
      state.lastRaf = ts;
      state.curT += dtMs * state.speed;
      if (state.curT >= state.endT) {
        state.curT = state.endT;
        state.playing = false;
        setPlayBtn();
      }
      syncUi();
      emit();
    }
    requestAnimationFrame(tick);
  }

  // ---- Control bindings ------------------------------------------------------

  if (playBtn) playBtn.addEventListener('click', toggle);

  if (speedSelect) {
    state.speed = Number(speedSelect.value) || 1;
    speedSelect.addEventListener('change', () => {
      state.speed = Number(speedSelect.value) || 1;
    });
  }

  if (timeline) {
    timeline.addEventListener('input', () => {
      const dur = state.endT - state.startT;
      // A drag on the timeline is a user seek; pause so the scrub doesn't fight
      // the rAF advance.
      state.playing = false;
      setPlayBtn();
      seek(state.startT + (Number(timeline.value) / 1000) * dur);
    });
  }

  requestAnimationFrame(tick);
  setPlayBtn();

  return { setRange, setCursor, seek, play, pause, toggle, onCursor };
}
