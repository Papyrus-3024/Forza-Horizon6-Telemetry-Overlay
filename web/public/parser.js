// FH6 "Data Out" packet decoding (324 bytes, little-endian).
// Field offsets verified against FH6_DATA_OUT_DOC.md / the workbench design spec.

export const PACKET_SIZE = 324;

// Per-wheel arrays are ordered [FL, FR, RL, RR]; each block is 4 contiguous f32.
const OFF = {
  IsRaceOn: 0,           // s32
  TimestampMs: 4,        // u32
  EngineMaxRpm: 8,       // f32
  EngineIdleRpm: 12,     // f32
  CurrentEngineRpm: 16,  // f32
  AccelerationX: 20,     // f32
  AccelerationY: 24,     // f32
  AccelerationZ: 28,     // f32
  VelocityX: 32,         // f32
  VelocityY: 36,         // f32
  VelocityZ: 40,         // f32
  AngularVelocityX: 44,  // f32
  AngularVelocityY: 48,  // f32
  AngularVelocityZ: 52,  // f32
  Yaw: 56,               // f32
  Pitch: 60,             // f32
  Roll: 64,              // f32
  SuspNorm: 68,          // f32 x4
  TireSlipRatio: 84,     // f32 x4
  WheelRotSpeed: 100,    // f32 x4
  WheelOnRumble: 116,    // s32 x4
  WheelInPuddle: 132,    // s32 x4
  SurfaceRumble: 148,    // f32 x4
  TireSlipAngle: 164,    // f32 x4
  TireCombinedSlip: 180, // f32 x4
  SuspTravelM: 196,      // f32 x4
  CarOrdinal: 212,       // s32
  CarClass: 216,         // s32
  CarPerformanceIndex: 220, // s32
  DrivetrainType: 224,   // s32
  NumCylinders: 228,     // s32
  CarGroup: 232,         // u32
  SmashableVelDiff: 236, // f32
  SmashableMass: 240,    // f32
  PositionX: 244,        // f32 (world meters)
  PositionY: 248,        // f32
  PositionZ: 252,        // f32 (ground plane is X/Z)
  Speed: 256,            // f32 (m/s)
  Power: 260,            // f32
  Torque: 264,           // f32
  TireTemp: 268,         // f32 x4
  Boost: 284,            // f32
  Fuel: 288,             // f32
  DistanceTraveled: 292, // f32
  BestLap: 296,          // f32
  LastLap: 300,          // f32
  CurrentLap: 304,       // f32
  CurrentRaceTime: 308,  // f32
  LapNumber: 312,        // u16
  RacePosition: 314,     // u8
  Accel: 315,            // u8 throttle
  Brake: 316,            // u8
  Clutch: 317,           // u8
  HandBrake: 318,        // u8
  Gear: 319,             // u8
  Steer: 320,            // s8 (-127..127)
};

function wheelF32(dv, base) {
  return [
    dv.getFloat32(base, true),
    dv.getFloat32(base + 4, true),
    dv.getFloat32(base + 8, true),
    dv.getFloat32(base + 12, true),
  ];
}

// Decode one 324-byte packet (Uint8Array view) into a frame object (no `t`).
export function decodePacket(bytes) {
  const dv = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  return {
    raceOn: dv.getInt32(OFF.IsRaceOn, true),
    ts: dv.getUint32(OFF.TimestampMs, true),
    maxRpm: dv.getFloat32(OFF.EngineMaxRpm, true),
    idleRpm: dv.getFloat32(OFF.EngineIdleRpm, true),
    rpm: dv.getFloat32(OFF.CurrentEngineRpm, true),
    accX: dv.getFloat32(OFF.AccelerationX, true),
    accY: dv.getFloat32(OFF.AccelerationY, true),
    accZ: dv.getFloat32(OFF.AccelerationZ, true),
    velX: dv.getFloat32(OFF.VelocityX, true),
    velY: dv.getFloat32(OFF.VelocityY, true),
    velZ: dv.getFloat32(OFF.VelocityZ, true),
    angX: dv.getFloat32(OFF.AngularVelocityX, true),
    angY: dv.getFloat32(OFF.AngularVelocityY, true),
    angZ: dv.getFloat32(OFF.AngularVelocityZ, true),
    yaw: dv.getFloat32(OFF.Yaw, true),
    pitch: dv.getFloat32(OFF.Pitch, true),
    roll: dv.getFloat32(OFF.Roll, true),
    x: dv.getFloat32(OFF.PositionX, true),
    y: dv.getFloat32(OFF.PositionY, true),
    z: dv.getFloat32(OFF.PositionZ, true),
    speed: dv.getFloat32(OFF.Speed, true), // m/s
    power: dv.getFloat32(OFF.Power, true),
    torque: dv.getFloat32(OFF.Torque, true),
    boost: dv.getFloat32(OFF.Boost, true),
    fuel: dv.getFloat32(OFF.Fuel, true),
    distance: dv.getFloat32(OFF.DistanceTraveled, true),
    bestLap: dv.getFloat32(OFF.BestLap, true),
    lastLap: dv.getFloat32(OFF.LastLap, true),
    curLap: dv.getFloat32(OFF.CurrentLap, true),
    raceTime: dv.getFloat32(OFF.CurrentRaceTime, true),
    lapNumber: dv.getUint16(OFF.LapNumber, true),
    racePos: dv.getUint8(OFF.RacePosition),
    throttle: dv.getUint8(OFF.Accel),  // 0..255
    brake: dv.getUint8(OFF.Brake),     // 0..255
    clutch: dv.getUint8(OFF.Clutch),
    handbrake: dv.getUint8(OFF.HandBrake),
    gear: dv.getUint8(OFF.Gear),
    steer: dv.getInt8(OFF.Steer),      // -127..127
    suspNorm: wheelF32(dv, OFF.SuspNorm),
    slipRatio: wheelF32(dv, OFF.TireSlipRatio),
    slipAngle: wheelF32(dv, OFF.TireSlipAngle),
    combinedSlip: wheelF32(dv, OFF.TireCombinedSlip),
    tireTemp: wheelF32(dv, OFF.TireTemp),
    wheelSpeed: wheelF32(dv, OFF.WheelRotSpeed),
  };
}

// base64 -> Uint8Array (browser).
export function b64ToBytes(b64) {
  const bin = atob(b64);
  const len = bin.length;
  const out = new Uint8Array(len);
  for (let i = 0; i < len; i++) out[i] = bin.charCodeAt(i);
  return out;
}

// Parse a capture into frames sorted by capture time `t` (ms).
// Auto-detects JSONL text (first non-whitespace char `{`) vs a raw .bin of
// concatenated 324-byte packets (Uint8Array / ArrayBuffer).
export function parseCapture(input) {
  if (typeof input === 'string') {
    // decision: detect JSONL by first non-whitespace char `{` — matches the spec's
    // byte-0x7B rule without scanning the whole string.
    const head = input.match(/^\s*([\S])/);
    if (head && head[1] === '{') return parseJsonl(input);
    // decision: a non-`{` string is treated as a binary body decoded via latin1 —
    // covers servers that hand back the .bin as text; real callers pass bytes.
    return parseBin(latin1ToBytes(input));
  }
  if (input instanceof ArrayBuffer) return parseBin(new Uint8Array(input));
  if (input instanceof Uint8Array) return parseBin(input);
  // decision: ArrayBufferView other than Uint8Array → wrap its buffer slice.
  if (ArrayBuffer.isView(input)) {
    return parseBin(new Uint8Array(input.buffer, input.byteOffset, input.byteLength));
  }
  return [];
}

function parseJsonl(text) {
  const frames = [];
  const lines = text.split(/\r?\n/);
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    let rec;
    try {
      rec = JSON.parse(trimmed);
    } catch {
      continue;
    }
    if (!rec || typeof rec.b64 !== 'string') continue;
    const bytes = b64ToBytes(rec.b64);
    if (bytes.length < PACKET_SIZE) continue;
    const frame = decodePacket(bytes);
    frame.t = typeof rec.t === 'number' ? rec.t : (frames.length ? frames[frames.length - 1].t : 0);
    frames.push(frame);
  }
  frames.sort((a, b) => a.t - b.t);
  return frames;
}

const U32_WRAP = 0x100000000;

function parseBin(bytes) {
  const count = Math.floor(bytes.length / PACKET_SIZE);
  const remainder = bytes.length - count * PACKET_SIZE;
  if (remainder > 0) {
    console.warn(`parseCapture: ignoring trailing ${remainder} bytes (not a whole 324-byte packet)`);
  }
  const frames = [];
  for (let i = 0; i < count; i++) {
    const off = i * PACKET_SIZE;
    const view = new Uint8Array(bytes.buffer, bytes.byteOffset + off, PACKET_SIZE);
    frames.push(decodePacket(view));
  }

  // Derive `t` from TimestampMs deltas, accumulating across u32 wraps. If the
  // timestamps never advance (zero/non-increasing), fall back to a 60 Hz clock.
  let increasing = false;
  for (let i = 1; i < frames.length; i++) {
    if (frames[i].ts !== frames[i - 1].ts) { increasing = true; break; }
  }
  if (increasing && frames.length) {
    let acc = 0;
    frames[0].t = 0;
    for (let i = 1; i < frames.length; i++) {
      let d = frames[i].ts - frames[i - 1].ts;
      // decision: a negative delta means the u32 TimestampMs wrapped — add 2^32
      // so cumulative time stays monotonic.
      if (d < 0) d += U32_WRAP;
      acc += d;
      frames[i].t = acc;
    }
  } else {
    const step = 1000 / 60;
    for (let i = 0; i < frames.length; i++) frames[i].t = i * step;
  }

  frames.sort((a, b) => a.t - b.t);
  return frames;
}

function latin1ToBytes(str) {
  const out = new Uint8Array(str.length);
  for (let i = 0; i < str.length; i++) out[i] = str.charCodeAt(i) & 0xff;
  return out;
}

// Last frame with frame.t <= t (binary search), or null if none / empty.
export function frameAt(frames, t) {
  if (!frames || frames.length === 0) return null;
  let lo = 0;
  let hi = frames.length - 1;
  let ans = -1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (frames[mid].t <= t) {
      ans = mid;
      lo = mid + 1;
    } else {
      hi = mid - 1;
    }
  }
  return ans === -1 ? null : frames[ans];
}
