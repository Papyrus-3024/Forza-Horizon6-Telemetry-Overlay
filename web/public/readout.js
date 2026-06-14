// Live numeric telemetry panel: builds a readout grid + thin bars in `root`
// and exposes update(frame) to refresh it for the cursor frame.

const MS_TO_MPH = 2.236936;
const G = 9.80665;

// decision: render every metric through num()/fmt() — keeps NaN/null/undefined
// handling in one place so any missing field shows the em dash uniformly.
const DASH = "—";

function num(v) {
  return typeof v === "number" && Number.isFinite(v) ? v : null;
}

function fmt(v, digits) {
  return v === null ? DASH : v.toFixed(digits);
}

// Lap times arrive as float seconds; 0 means none/unset. Show "31.2s" or the dash.
function fmtLapTime(secs) {
  const s = num(secs);
  return s === null || s <= 0 ? DASH : `${s.toFixed(1)}s`;
}

// decision: gear 0 -> "R", gear 1 with rpm near idle isn't reliably "neutral" in
// FH6 (it has no distinct N code), so only map 0->R and show the raw number
// otherwise; this matches the in-game HUD where 1 is first gear.
function gearText(gear) {
  const g = num(gear);
  if (g === null) return DASH;
  if (g === 0) return "R";
  return String(g);
}

export function createReadout(root) {
  // Two-column metric grid mirroring the existing .readout/.metric markup.
  const grid = document.createElement("div");
  grid.className = "readout";

  // [label, key, modifier?] — "wide" spans the full grid width so the combined
  // km/h · mph speed string fits on one line in the 300px sidebar.
  const metrics = [
    ["Speed", "speed", "wide"],
    ["Gear", "gear"],
    ["RPM", "rpm"],
    ["Throttle", "throttle"],
    ["Brake", "brake"],
    ["Steer", "steer"],
    ["Boost", "boost"],
    ["Fuel", "fuel"],
    ["Lap", "lap"],
    ["Best lap", "bestLap"],
    ["Last lap", "lastLap"],
    ["Position", "pos"],
    ["World X/Z", "world"],
    ["Lat G", "latG"],
    ["Long G", "longG"],
  ];

  const v = {};
  for (const [label, key, mod] of metrics) {
    const cell = document.createElement("div");
    cell.className = mod ? `metric ${mod}` : "metric";
    const k = document.createElement("span");
    k.className = "k";
    k.textContent = label;
    const val = document.createElement("span");
    val.className = "v";
    val.textContent = DASH;
    cell.append(k, val);
    grid.appendChild(cell);
    v[key] = val;
  }
  root.appendChild(grid);

  // Thin progress bars for throttle, brake, and rpm (rpm vs maxRpm).
  function makeBar(cls) {
    const bar = document.createElement("div");
    bar.className = "bar";
    const fill = document.createElement("div");
    fill.className = "bar-fill " + cls;
    bar.appendChild(fill);
    root.appendChild(bar);
    return fill;
  }
  const barThrottle = makeBar("throttle");
  const barBrake = makeBar("brake");
  const barRpm = makeBar("rpm");

  function clearAll() {
    for (const key in v) v[key].textContent = DASH;
    barThrottle.style.width = "0%";
    barBrake.style.width = "0%";
    barRpm.style.width = "0%";
  }

  function update(frame) {
    if (!frame) {
      clearAll();
      return;
    }

    const speed = num(frame.speed);
    if (speed === null) {
      v.speed.textContent = DASH;
    } else {
      v.speed.textContent = `${(speed * 3.6).toFixed(1)} km/h  ·  ${(speed * MS_TO_MPH).toFixed(1)} mph`;
    }

    v.gear.textContent = gearText(frame.gear);

    const rpm = num(frame.rpm);
    v.rpm.textContent = rpm === null ? DASH : Math.round(rpm).toString();

    const throttle = num(frame.throttle); // 0..255
    const brake = num(frame.brake); // 0..255
    const throttlePct = throttle === null ? null : (throttle / 255) * 100;
    const brakePct = brake === null ? null : (brake / 255) * 100;
    v.throttle.textContent = throttlePct === null ? DASH : `${Math.round(throttlePct)}%`;
    v.brake.textContent = brakePct === null ? DASH : `${Math.round(brakePct)}%`;

    const steer = num(frame.steer); // -127..127
    v.steer.textContent = steer === null ? DASH : Math.round(steer).toString();

    v.boost.textContent = fmt(num(frame.boost), 1);

    // decision: fuel is a 0..1 fraction in the FH6 packet, so show it as a percent.
    const fuel = num(frame.fuel);
    v.fuel.textContent = fuel === null ? DASH : `${(fuel * 100).toFixed(1)}%`;

    const lapNumber = num(frame.lapNumber);
    v.lap.textContent = lapNumber === null ? DASH : String(lapNumber);

    v.bestLap.textContent = fmtLapTime(frame.bestLap);
    v.lastLap.textContent = fmtLapTime(frame.lastLap);

    const racePos = num(frame.racePos);
    v.pos.textContent = racePos === null ? DASH : `P${racePos}`;

    const x = num(frame.x);
    const z = num(frame.z);
    v.world.textContent = x === null || z === null ? DASH : `${x.toFixed(0)} / ${z.toFixed(0)}`;

    // accX is car-local lateral, accZ is forward — divide by g for load factors.
    const accX = num(frame.accX);
    const accZ = num(frame.accZ);
    v.latG.textContent = accX === null ? DASH : (accX / G).toFixed(2);
    v.longG.textContent = accZ === null ? DASH : (accZ / G).toFixed(2);

    barThrottle.style.width = throttlePct === null ? "0%" : `${throttlePct}%`;
    barBrake.style.width = brakePct === null ? "0%" : `${brakePct}%`;

    // rpm bar scales against maxRpm; guard a zero/missing ceiling.
    const maxRpm = num(frame.maxRpm);
    if (rpm === null || maxRpm === null || maxRpm <= 0) {
      barRpm.style.width = "0%";
    } else {
      const pct = Math.max(0, Math.min(100, (rpm / maxRpm) * 100));
      barRpm.style.width = `${pct}%`;
    }
  }

  return { update };
}
