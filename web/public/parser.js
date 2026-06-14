// FH6 "Data Out" packet decoding (324 bytes, little-endian).
// Field offsets verified against FH6_DATA_OUT_DOC.md field order.

export const PACKET_SIZE = 324;

const OFF = {
  IsRaceOn: 0,        // s32
  TimestampMs: 4,     // u32
  EngineMaxRpm: 8,    // f32
  CurrentEngineRpm: 16, // f32
  PositionX: 244,     // f32 (world meters)
  PositionY: 248,     // f32
  PositionZ: 252,     // f32 (ground plane is X/Z)
  Speed: 256,         // f32 (m/s)
  Accel: 315,         // u8 throttle
  Brake: 316,         // u8
  Gear: 319,          // u8
  Steer: 320,         // s8 (-127..127)
};

// Decode one 324-byte packet (Uint8Array view) into a frame object.
export function decodePacket(bytes) {
  const dv = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  return {
    raceOn: dv.getInt32(OFF.IsRaceOn, true),
    ts: dv.getUint32(OFF.TimestampMs, true),
    maxRpm: dv.getFloat32(OFF.EngineMaxRpm, true),
    rpm: dv.getFloat32(OFF.CurrentEngineRpm, true),
    x: dv.getFloat32(OFF.PositionX, true),
    y: dv.getFloat32(OFF.PositionY, true),
    z: dv.getFloat32(OFF.PositionZ, true),
    speed: dv.getFloat32(OFF.Speed, true), // m/s
    throttle: dv.getUint8(OFF.Accel),      // 0..255
    brake: dv.getUint8(OFF.Brake),         // 0..255
    gear: dv.getUint8(OFF.Gear),
    steer: dv.getInt8(OFF.Steer),          // -127..127
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

// Parse JSONL capture text into an array of frames with capture time `t`.
// Skips blank/malformed lines and packets that are not 324 bytes.
export function parseCapture(text) {
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
  // Ensure monotonically ordered by capture time.
  frames.sort((a, b) => a.t - b.t);
  return frames;
}
