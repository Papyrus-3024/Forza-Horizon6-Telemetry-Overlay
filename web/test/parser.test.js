import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  PACKET_SIZE,
  decodePacket,
  b64ToBytes,
  parseCapture,
  frameAt,
} from '../public/parser.js';
import { computeLaps } from '../public/laps.js';

// Build a 324-byte packet with known values at the documented offsets.
function makePacket({ ts = 0, rpm = 0, x = 0, z = 0, speed = 0, yaw = 0, throttle = 0, brake = 0, gear = 0, steer = 0, lapNumber = 0, slipFL = 0 } = {}) {
  const buf = new ArrayBuffer(PACKET_SIZE);
  const dv = new DataView(buf);
  dv.setInt32(0, 1, true);            // IsRaceOn
  dv.setUint32(4, ts, true);          // TimestampMs
  dv.setFloat32(8, 8000, true);       // EngineMaxRpm
  dv.setFloat32(12, 900, true);       // EngineIdleRpm
  dv.setFloat32(16, rpm, true);       // CurrentEngineRpm
  dv.setFloat32(56, yaw, true);       // Yaw
  dv.setFloat32(84, slipFL, true);    // TireSlipRatio FL
  dv.setFloat32(244, x, true);        // PositionX
  dv.setFloat32(252, z, true);        // PositionZ
  dv.setFloat32(256, speed, true);    // Speed
  dv.setUint16(312, lapNumber, true); // LapNumber
  dv.setUint8(315, throttle);         // Accel
  dv.setUint8(316, brake);            // Brake
  dv.setUint8(319, gear);             // Gear
  dv.setInt8(320, steer);             // Steer
  return new Uint8Array(buf);
}

function bytesToB64(bytes) {
  return Buffer.from(bytes).toString('base64');
}

test('decodePacket reads golden field values', () => {
  const bytes = makePacket({
    ts: 1234, rpm: 6500, x: 12.5, z: -40.25, speed: 55.5,
    yaw: 1.5, throttle: 200, brake: 30, gear: 4, steer: -63,
    lapNumber: 2, slipFL: 0.75,
  });
  const f = decodePacket(bytes);
  assert.equal(f.raceOn, 1);
  assert.equal(f.ts, 1234);
  assert.equal(f.maxRpm, 8000);
  assert.equal(f.idleRpm, 900);
  assert.equal(f.rpm, 6500);
  assert.equal(f.x, 12.5);
  assert.equal(f.z, -40.25);
  assert.equal(f.speed, 55.5);
  assert.equal(f.yaw, 1.5);
  assert.equal(f.throttle, 200);
  assert.equal(f.brake, 30);
  assert.equal(f.gear, 4);
  assert.equal(f.steer, -63);
  assert.equal(f.lapNumber, 2);
  assert.deepEqual(f.slipRatio, [0.75, 0, 0, 0]);
  assert.equal(f.suspNorm.length, 4);
});

test('parseCapture auto-detects JSONL', () => {
  const p0 = makePacket({ ts: 0, speed: 10, lapNumber: 0 });
  const p1 = makePacket({ ts: 16, speed: 20, lapNumber: 0 });
  const jsonl =
    JSON.stringify({ t: 0, len: PACKET_SIZE, b64: bytesToB64(p0) }) + '\n' +
    JSON.stringify({ t: 16, len: PACKET_SIZE, b64: bytesToB64(p1) }) + '\n';
  const frames = parseCapture(jsonl);
  assert.equal(frames.length, 2);
  assert.equal(frames[0].t, 0);
  assert.equal(frames[1].t, 16);
  assert.equal(frames[0].speed, 10);
  assert.equal(frames[1].speed, 20);
});

test('parseCapture skips malformed JSONL lines', () => {
  const p0 = makePacket({ ts: 0, speed: 10 });
  // First line must start with `{` so auto-detect picks JSONL; the bad lines
  // inside are skipped by the per-line parser.
  const jsonl =
    JSON.stringify({ t: 0, b64: bytesToB64(p0) }) + '\n' +
    '{ not valid json\n' +
    'garbage line\n' +
    JSON.stringify({ t: 5, b64: 'AAAA' }) + '\n'; // short packet, skipped
  const frames = parseCapture(jsonl);
  assert.equal(frames.length, 1);
  assert.equal(frames[0].speed, 10);
});

test('parseCapture auto-detects raw .bin and derives t from TimestampMs', () => {
  const p0 = makePacket({ ts: 1000, speed: 10 });
  const p1 = makePacket({ ts: 1016, speed: 20 });
  const raw = new Uint8Array(PACKET_SIZE * 2);
  raw.set(p0, 0);
  raw.set(p1, PACKET_SIZE);
  const frames = parseCapture(raw);
  assert.equal(frames.length, 2);
  assert.equal(frames[0].t, 0);
  assert.equal(frames[1].t, 16); // 1016 - 1000
  assert.equal(frames[0].speed, 10);
  assert.equal(frames[1].speed, 20);
});

test('parseCapture .bin falls back to 60Hz when TimestampMs is flat', () => {
  const p0 = makePacket({ ts: 0, speed: 10 });
  const p1 = makePacket({ ts: 0, speed: 20 });
  const raw = new Uint8Array(PACKET_SIZE * 2);
  raw.set(p0, 0);
  raw.set(p1, PACKET_SIZE);
  const frames = parseCapture(raw);
  assert.equal(frames.length, 2);
  assert.equal(frames[0].t, 0);
  assert.ok(Math.abs(frames[1].t - 1000 / 60) < 1e-9);
});

test('parseCapture .bin handles u32 TimestampMs wrap', () => {
  const p0 = makePacket({ ts: 0xfffffff0, speed: 10 });
  const p1 = makePacket({ ts: 0x0000000f, speed: 20 }); // wrapped: +31ms
  const raw = new Uint8Array(PACKET_SIZE * 2);
  raw.set(p0, 0);
  raw.set(p1, PACKET_SIZE);
  const frames = parseCapture(raw);
  assert.equal(frames[1].t, 31);
});

test('parseCapture .bin ignores trailing partial packet', () => {
  const p0 = makePacket({ ts: 0, speed: 10 });
  const raw = new Uint8Array(PACKET_SIZE + 100);
  raw.set(p0, 0);
  const frames = parseCapture(raw);
  assert.equal(frames.length, 1);
});

test('parseCapture accepts ArrayBuffer', () => {
  const p0 = makePacket({ ts: 0, speed: 42 });
  const frames = parseCapture(p0.buffer);
  assert.equal(frames.length, 1);
  assert.equal(frames[0].speed, 42);
});

test('frameAt returns last frame at or before t', () => {
  const frames = [{ t: 0 }, { t: 10 }, { t: 20 }, { t: 30 }];
  assert.equal(frameAt(frames, -1), null);
  assert.equal(frameAt(frames, 0).t, 0);
  assert.equal(frameAt(frames, 15).t, 10);
  assert.equal(frameAt(frames, 20).t, 20);
  assert.equal(frameAt(frames, 999).t, 30);
  assert.equal(frameAt([], 5), null);
});

test('computeLaps splits on lapNumber increments', () => {
  // lapNumbers: 0,0,1,1,1,2  -> three laps
  const frames = [
    { t: 0, lapNumber: 0 },
    { t: 100, lapNumber: 0 },
    { t: 200, lapNumber: 1 },
    { t: 300, lapNumber: 1 },
    { t: 400, lapNumber: 1 },
    { t: 700, lapNumber: 2 },
  ];
  const { laps, bestLapIndex } = computeLaps(frames);
  assert.equal(laps.length, 3);

  assert.equal(laps[0].lapNumber, 0);
  assert.equal(laps[0].startIdx, 0);
  assert.equal(laps[0].endIdx, 1);
  assert.equal(laps[0].durationMs, 100);

  assert.equal(laps[1].lapNumber, 1);
  assert.equal(laps[1].startIdx, 2);
  assert.equal(laps[1].endIdx, 4);
  assert.equal(laps[1].durationMs, 200);

  assert.equal(laps[2].lapNumber, 2);
  assert.equal(laps[2].startIdx, 5);
  assert.equal(laps[2].endIdx, 5);

  // Only laps 0 and 1 are completed; lap 0 (100ms) is shortest.
  assert.equal(bestLapIndex, 0);
});

test('computeLaps picks the shortest completed lap and excludes the final lap', () => {
  // lapNumbers: 0,0 (300ms) | 1,1 (120ms, shortest) | 2,2 (200ms) | 3 (final, incomplete)
  const frames = [
    { t: 0, lapNumber: 0 },
    { t: 300, lapNumber: 0 },
    { t: 420, lapNumber: 1 },
    { t: 540, lapNumber: 1 },
    { t: 620, lapNumber: 2 },
    { t: 820, lapNumber: 2 },
    { t: 900, lapNumber: 3 }, // trailing/incomplete lap, must not be eligible
  ];
  const { laps, bestLapIndex } = computeLaps(frames);
  assert.equal(laps.length, 4);
  assert.equal(laps[1].durationMs, 120); // the shortest completed lap
  assert.equal(bestLapIndex, 1);
  // The final lap is excluded even though its 0ms duration would otherwise win.
  assert.equal(laps[3].durationMs, 0);
  assert.notEqual(bestLapIndex, 3);
});

test('computeLaps on empty input', () => {
  const { laps, bestLapIndex } = computeLaps([]);
  assert.deepEqual(laps, []);
  assert.equal(bestLapIndex, -1);
});

test('computeLaps single lap has no completed best', () => {
  const frames = [{ t: 0, lapNumber: 0 }, { t: 50, lapNumber: 0 }];
  const { laps, bestLapIndex } = computeLaps(frames);
  assert.equal(laps.length, 1);
  assert.equal(bestLapIndex, -1);
});
