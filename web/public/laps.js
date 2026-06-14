// Lap segmentation from a frame stream. Laps are split on `lapNumber` increments;
// each lap spans the frames between two crossings.

// computeLaps(frames) -> { laps, bestLapIndex }
//   Lap = { index, lapNumber, startT, endT, durationMs, startIdx, endIdx }
//   bestLapIndex = index of the shortest completed lap, or -1 if none.
export function computeLaps(frames) {
  if (!frames || frames.length === 0) return { laps: [], bestLapIndex: -1 };

  const laps = [];
  let startIdx = 0;
  let curLapNumber = frames[0].lapNumber;

  for (let i = 1; i < frames.length; i++) {
    // decision: split when lapNumber strictly increases — a decrease/reset
    // (e.g. new session) also closes the current lap so timing never goes negative.
    if (frames[i].lapNumber !== curLapNumber) {
      pushLap(laps, frames, startIdx, i - 1, curLapNumber);
      startIdx = i;
      curLapNumber = frames[i].lapNumber;
    }
  }
  // The final, possibly-incomplete lap runs to the last frame.
  pushLap(laps, frames, startIdx, frames.length - 1, curLapNumber);

  // decision: the last lap is "completed" only if a later increment closed it.
  // Since the trailing lap is never followed by an increment, treat every lap
  // except the last as completed for best-lap selection.
  let bestLapIndex = -1;
  let bestDur = Infinity;
  for (let i = 0; i < laps.length - 1; i++) {
    if (laps[i].durationMs > 0 && laps[i].durationMs < bestDur) {
      bestDur = laps[i].durationMs;
      bestLapIndex = i;
    }
  }
  return { laps, bestLapIndex };
}

function pushLap(laps, frames, startIdx, endIdx, lapNumber) {
  const startT = frames[startIdx].t;
  const endT = frames[endIdx].t;
  laps.push({
    index: laps.length,
    lapNumber,
    startT,
    endT,
    durationMs: endT - startT,
    startIdx,
    endIdx,
  });
}
