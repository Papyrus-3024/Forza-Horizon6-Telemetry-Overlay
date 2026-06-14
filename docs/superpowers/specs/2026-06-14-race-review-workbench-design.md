# FH6 Web Race-Review Workbench — Design Spec

Date: 2026-06-14
Status: Approved
Scope: `web/` (Node static server + vanilla ES-module front-end)

## Goal

Turn the existing `web/` app from a map-centric replay (which degrades to a bare grid
because it depends on uncommitted ~50 MB AVIF maps) into a **race-review workbench**: load a
captured telemetry drive and review it via time-synced charts, a track path, lap splits, and
a scrubber. Dependency-free vanilla JS; reuse the working Node server and packet decoder.

## Approved decisions (from brainstorming)

1. **Focus:** telemetry-analysis workbench (charts + track + laps + scrubber).
2. **Input:** auto-detect both JSONL(base64) and raw `.bin` (concatenated 324-byte packets).
3. **Map:** self-scaled path plot by default (zero assets); world-map backdrop optional.
4. **Approach:** keep server + parser; rebuild the front-end. Stay dependency-free.
5. **Car heading:** from the `Yaw` field (not movement-derived).

## Packet layout (FH6, 324 bytes, little-endian)

Full documented field set. Verified byte offsets (FH6 inserts `CarGroup`,
`SmashableVelDiff`, `SmashableMass` after `NumCylinders`, before `PositionX`):

```
0   S32 IsRaceOn          56  F32 Yaw                 244 F32 PositionX
4   U32 TimestampMS       60  F32 Pitch               248 F32 PositionY
8   F32 EngineMaxRpm      64  F32 Roll                252 F32 PositionZ
12  F32 EngineIdleRpm     68  F32 SuspNormFL          256 F32 Speed (m/s)
16  F32 CurrentEngineRpm  72/76/80  FR/RL/RR          260 F32 Power
20  F32 AccelerationX     84  F32 TireSlipRatioFL     264 F32 Torque
24  F32 AccelerationY     88/92/96  FR/RL/RR          268 F32 TireTempFL
28  F32 AccelerationZ     100 F32 WheelRotSpeedFL     272/276/280 FR/RL/RR
32  F32 VelocityX         104/108/112 FR/RL/RR        284 F32 Boost
36  F32 VelocityY         116 S32 WheelOnRumbleFL     288 F32 Fuel
40  F32 VelocityZ         120/124/128 FR/RL/RR        292 F32 DistanceTraveled
44  F32 AngularVelocityX  132 S32 WheelInPuddleFL     296 F32 BestLap
48  F32 AngularVelocityY  136/140/144 FR/RL/RR        300 F32 LastLap
52  F32 AngularVelocityZ  148 F32 SurfaceRumbleFL     304 F32 CurrentLap
                          152/156/160 FR/RL/RR        308 F32 CurrentRaceTime
                          164 F32 TireSlipAngleFL     312 U16 LapNumber
                          168/172/176 FR/RL/RR        314 U8  RacePosition
                          180 F32 TireCombinedSlipFL  315 U8  Accel (throttle)
                          184/188/192 FR/RL/RR        316 U8  Brake
                          196 F32 SuspTravelMFL       317 U8  Clutch
                          200/204/208 FR/RL/RR        318 U8  HandBrake
                          212 S32 CarOrdinal          319 U8  Gear
                          216 S32 CarClass            320 S8  Steer
                          220 S32 CarPerformanceIndex 321 S8  NormalizedDrivingLine
                          224 S32 DrivetrainType      322 S8  NormalizedAIBrakeDifference
                          228 S32 NumCylinders        323 padding
                          232 U32 CarGroup
                          236 F32 SmashableVelDiff
                          240 F32 SmashableMass
```

Per-wheel arrays are ordered `[FL, FR, RL, RR]`.

## Frame schema (parser output)

Each frame is a plain object. Camel-cased fields, units noted:

```
t                 // ms, normalized capture time (monotonic)
raceOn ts
maxRpm idleRpm rpm
accX accY accZ            // m/s^2, car-local (X=right, Y=up, Z=forward)
velX velY velZ            // m/s, car-local
angX angY angZ            // rad/s
yaw pitch roll            // rad
x y z                     // world meters (ground plane = X/Z)
speed                     // m/s
power torque boost fuel distance
bestLap lastLap curLap raceTime
lapNumber racePos
throttle brake clutch handbrake   // 0..255
gear steer                        // gear u8; steer -127..127
suspNorm slipRatio slipAngle combinedSlip tireTemp wheelSpeed  // each [FL,FR,RL,RR]
```

Derived getters used by views (computed in app or accessors): `speedKmh = speed*3.6`,
`speedMph = speed*2.236936`, `latG = accX/9.80665`, `longG = accZ/9.80665`,
`throttlePct = throttle/255*100`, `brakePct = brake/255*100`.

## Module interfaces (each isolated, one job)

**`parser.js`** (extend existing)
- `PACKET_SIZE = 324`
- `decodePacket(bytes: Uint8Array) -> frame` (no `t`)
- `b64ToBytes(b64) -> Uint8Array`
- `parseCapture(input: string | Uint8Array | ArrayBuffer) -> frame[]`
  - Auto-detect: first non-whitespace byte `{` (0x7B) → JSONL (each line
    `{t,len,b64}`); else treat as raw `.bin` = N×324 bytes.
  - Timing: JSONL uses `rec.t`. Raw `.bin` derives `t` from `TimestampMs` deltas
    (handles u32 wrap); if `TimestampMs` is non-increasing/zero, fall back to
    `index * (1000/60)`.
  - Skips malformed lines / short packets. Returns frames sorted by `t`.
- `frameAt(frames, t) -> frame | null` (binary search: last frame with `frame.t <= t`)

**`laps.js`**
- `computeLaps(frames) -> { laps: Lap[], bestLapIndex: number }`
  - `Lap = { index, lapNumber, startT, endT, durationMs, startIdx, endIdx }`
  - Split on `lapNumber` increments. `durationMs` from `t` boundary delta.
    `bestLapIndex` = shortest completed lap (-1 if none).

**`charts.js`**
- `createChartPanel(container: HTMLElement) -> ChartPanel`
  - `setFrames(frames)`, `setCursor(t)`, `onSeek(cb: (t)=>void)`, `resize()`, `setLaps(laps)`
  - Renders a vertical stack of mini time-charts sharing one X (time) axis:
    speed (km/h), RPM, throttle+brake (overlaid 0..100%), steer (-127..127),
    lat/long G. Vertical crosshair at cursor; click/drag a chart seeks. Lap
    boundaries drawn as faint vertical guides.

**`track.js`**
- `createTrack(canvas: HTMLCanvasElement) -> Track`
  - `setFrames(frames)`, `setCursor(t)`, `setMap(img|null)`, `resize()`
  - Auto-fit path polyline from world X/Z; start (green) / end (red) dots; car
    marker oriented by `yaw` at the cursor frame. Mouse drag = pan, wheel = zoom.
    Optional map image drawn under the path when provided (else neutral).

**`readout.js`**
- `createReadout(root: HTMLElement) -> { update(frame) }`
  - Builds the live numeric panel (speed km/h+mph, gear, rpm, throttle/brake %,
    steer, boost, fuel, lap, race pos, world X/Z, lat/long G) and progress bars.

**`playback.js`** (transport controller)
- `createTransport({ playBtn, timeline, timeLabel, speedSelect }) -> Transport`
  - `setRange(startT, endT)`, `setCursor(t)` (programmatic), `seek(t)` (user),
    `play()`, `pause()`, `toggle()`, `onCursor(cb: (t)=>void)`
  - Owns play state + speed + an internal rAF loop; emits `onCursor(t)` on every
    tick and seek. Spacebar handled by app and routed to `toggle()`.

**`app.js`** (thin integrator)
- Loads captures (bundled dropdown + file input, accepts `.jsonl`/`.bin`), parses,
  computes laps, builds the views, and wires: `transport.onCursor(t)` →
  `frameAt` → `readout.update` + `track.setCursor` + `charts.setCursor`;
  `charts.onSeek` → `transport.seek`; lap selector → `transport.seek(lap.startT)`.

**`server.js`** — add `.bin` to the captures listing filter and capture-name
validation (currently `.jsonl` only). MIME for `.bin` =
`application/octet-stream`. Serve `.bin` as a binary body.

**`index.html` / `style.css`** — layout: left column = track canvas + readout;
right/main = chart stack; bottom = transport bar with lap selector; top of sidebar
= capture + (optional) map controls. Dark theme consistent with current look.

## Data flow

```
load capture (text|bytes) -> parseCapture -> frames[]
  -> computeLaps -> laps
  -> track.setFrames / charts.setFrames+setLaps / readout
  -> transport.setRange(start,end)
render: transport.onCursor(t) -> f = frameAt(frames,t)
        -> readout.update(f); track.setCursor(t); charts.setCursor(t)
seek:   charts.onSeek(t) | lapSelect -> transport.seek(t)
```

## Error handling

- Malformed JSONL lines and non-324-byte packets skipped silently.
- Empty/zero-frame capture → on-canvas hint, controls inert.
- Missing numeric fields render as `—`.
- Missing map image → path-only, no nagging note.
- Raw `.bin` whose length isn't a multiple of 324 → parse the whole packets, ignore
  the trailing remainder (log a console warning).

## Testing & verification

- **Unit (`web/test/parser.test.js`, `node --test`):** round-trip a synthetic
  324-byte packet through `decodePacket` (golden field values); `parseCapture`
  auto-detect for both JSONL and raw `.bin`; `computeLaps` on a synthetic
  lap-number sequence. Add `"test": "node --test"` to `package.json`.
- **Visual:** start the server, generate a `.bin` from `sample-drive.jsonl`, and
  drive Chrome headless to screenshot the page loading both the `.jsonl` and the
  `.bin` — confirm charts + path render and the console is clean.

## Out of scope (YAGNI)

Sector timing (no telemetry sectors), multi-capture comparison, georeferenced
world→pixel map alignment, persistence/accounts, server-side parsing.
