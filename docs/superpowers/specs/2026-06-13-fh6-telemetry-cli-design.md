# FH6 Telemetry CLI — Design (v1)

Date: 2026-06-13
Status: Approved for planning

## Goal

A .NET command-line tool for Forza Horizon 6 "Data Out" UDP telemetry. v1 delivers a
reusable packet parser plus three subcommands: capture, replay, and a live refreshing
dashboard. The parser and data pipeline are the foundation for later work (overlays, an
AI/ML self-driving model), so isolation and testability are first-class concerns.

Dev/testing runs off recorded captures (replay); a running game is not required. The user
can re-capture from the game when richer fixtures are needed.

## Background / verified facts

Source of truth for the wire format: `FH6_DATA_OUT_DOC.md`.

- Packet is a fixed **324 bytes**, **little-endian**, one-way UDP, sent at frame rate.
- Verified empirically against the supplied captures: decoding at offset 0 yields sane
  values (`IsRaceOn=1`, `EngineMaxRpm=11500`, `IdleRpm=800`, `CurrentRpm≈6505`,
  `Speed≈10.37 m/s`, `Gear=1`, `NumCylinders=8`, `PerfIndex=900`, `Fuel=1.0`).
- The documented fields sum to **323 bytes (offsets 0–322)**; **byte 323 is trailing
  padding** (value `0`; 324 = 81×4, i.e. 4-byte struct alignment). The parser reads the
  323 documented bytes and ignores byte 323.
- FH6-specific fields vs Forza Motorsport: `CarGroup`, `SmashableVelDiff`, `SmashableMass`
  inserted after `NumCylinders` and before `PositionX`. No `TireWear`/`TrackOrdinal`.

## Architecture

One solution (`FH6-Telemetry.sln`), three projects:

| Project | Type | Responsibility |
|---|---|---|
| `Fh6.Telemetry.Core` | class library | Wire format + parsing. No third-party deps. |
| `Fh6.Telemetry.Cli` | console exe | Subcommands, UDP I/O, capture file I/O, dashboard. Depends on Core + Spectre.Console. |
| `Fh6.Telemetry.Tests` | xUnit | Parser golden-value tests, round-trip + malformed-packet tests. |

The existing `FH6-Telemetry` console project is repurposed as `Fh6.Telemetry.Cli`. Target
framework stays `net8.0` (SDK 9.0.308 is installed; bumping to `net9.0` is optional).

### Core library

- `TelemetryPacket` — a `readonly record struct` holding all fields, grouped logically
  (engine, motion/orientation, per-wheel arrays, car identity, race/lap, driver inputs).
  Per-wheel data exposed as length-4 groupings (FL, FR, RL, RR) for clarity.
- `PacketLayout` — offset constants for every field, derived from the doc, with a compile-
  time/total-size check (`= 323` documented, packet `= 324`).
- `PacketParser.Parse(ReadOnlySpan<byte> packet) -> TelemetryPacket` — pure, allocation-
  free decode using `BinaryPrimitives`/`MemoryMarshal`, little-endian. Validates length.

### CLI pipeline (the key isolation boundary)

```
byte source ── ITelemetrySource ──> PacketParser ──> TelemetryPacket ──> IDashboard
 (UdpSource │ JsonlReplaySource)                                          (renderer)
```

- `ITelemetrySource` — yields raw 324-byte frames (with relative timestamps for replay).
  Implementations: `UdpTelemetrySource` (live) and `JsonlReplaySource` (capture file).
- `IDashboard` — consumes parsed packets and renders. v1 implementation:
  `SpectreDashboard` (refreshing panel/table layout via Spectre.Console `Live`).
- Both `replay` and `live` feed the same parser and the same dashboard. This is the seam
  that overlays and the AI model will later tap into.

### Subcommands (Spectre.Console.Cli)

- `fh6 capture [--port 20440] [--out <file>]`
  UDP listener that records raw frames to JSONL (`{t, len, b64}`, same shape as the
  legacy `capture.js`). Uses a buffered write stream (not sync-append-per-packet) so it
  does not drop datagrams at frame rate. Replaces and retires `capture.js`.
- `fh6 replay <file.jsonl> [--speed N] [--loop]`
  Reads a capture, decodes each frame, and drives the dashboard honoring inter-frame
  timing from the `t` field (scaled by `--speed`). Primary dev path.
- `fh6 live [--port 20440]`
  UDP listener that decodes and drives the dashboard in real time.

- `fh6 coverage <file.jsonl>` — **temporary** (see "Capture coverage tracker" below)
  Reads a capture and reports which telemetry conditions were exercised vs still missing,
  so manual test captures can be confirmed complete. Removed once captures are validated.

### Dashboard content (v1)

Refreshing layout showing: race state, speed (m/s + km/h), gear, engine RPM (with
idle/max context), throttle/brake/clutch/handbrake inputs, steering, per-wheel tire slip
and temperature, boost, fuel, and lap/position info. Exact layout finalized during
implementation; the renderer is swappable behind `IDashboard`.

## Capture coverage tracker (temporary)

A throwaway aid for manual testing: it confirms that a new in-game capture actually
exercised every family of packet fields, so the team knows the captures are good before
relying on them. **Built last** (after Core, capture, replay, dashboard) and **removed**
once captures are confirmed — its removal is a tracked step.

`fh6 coverage <file.jsonl>` parses every frame, evaluates each condition below, and prints
a checklist of met / missing items (with a simple count or first-seen frame for met
items). Thresholds are approximate and tuned during implementation.

Conditions to track (chosen so each maps to a distinct field family):

- Race state: `IsRaceOn` observed both 1 and 0 (driving vs menu transition).
- Inputs: full throttle (`Accel` ≈ 255), hard brake (`Brake` ≈ 255), clutch used
  (`Clutch` > 0), handbrake used (`HandBrake` > 0), full steer left and right (`Steer` ≈ ±127).
- Drivetrain: each forward gear reached (1..max observed), reverse, neutral; RPM near
  redline (`CurrentEngineRpm` close to `EngineMaxRpm`).
- Grip/tires: high slip ratio (`|TireSlipRatio|` > 1), high slip angle
  (`|TireSlipAngle|` > 1), high combined slip (> 1), tire temperature variation.
- Surface: wheel on rumble strip (any `WheelOnRumbleStrip` = 1), wheel in puddle (any
  `WheelInPuddle` = 1, needs wet conditions), high `SurfaceRumble` (off-road/rough).
- Suspension/airborne: near-full compression (landing) and near-full stretch on all four
  wheels (airborne / jump).
- Powertrain: positive `Boost` (turbo/SC car), non-trivial `Power`/`Torque` range.
- Collisions: smashable hit (`SmashableVelDiff` > 0 / `SmashableMass` > 0).
- Race/lap: lap completed (`LapNumber` increments; `BestLap`/`LastLap` populated),
  `RacePosition` recorded, high `Speed` reached, `DistanceTraveled` accumulates.

Some conditions require a chosen environment (puddle needs rain; collision needs hitting an
object; airborne needs a jump). The tracker only reports; the driver decides what to cover.

## Error handling

- Reject frames whose length != 324; count and report skipped frames rather than crash.
- Graceful `Ctrl-C` shutdown that flushes the capture writer.
- Clear errors on UDP bind failure (port in use / firewall) and missing/invalid capture files.
- Replay handles EOF and `--loop`.

## Testing

- xUnit with golden-value assertions parsed from a **trimmed capture fixture** committed
  under the test project (a handful of representative frames, including a driving frame and
  a menu/zero frame). Raw root-level `capture-*.jsonl` files are git-ignored.
- Tests: known-field decode, length validation/skip behavior, JSONL round-trip
  (capture write -> replay read), and per-wheel grouping correctness.
- Test-driven: write the failing parser test against a known frame before implementing.

## Out of scope for v1 (future)

- `CarOrdinal` -> car make/model name lookup (needs an external ordinal database; dashboard
  shows the numeric ordinal for now).
- CSV/analysis export and lap breakdowns.
- Overlays (transparent window over the game).
- AI/ML self-driving model (will consume `ITelemetrySource` + `TelemetryPacket`).

## Project setup (done alongside this spec)

- `git init`, `.gitignore` (excludes `.vs/`, `bin/`, `obj/`, root `capture-*.jsonl`).
- `CLAUDE.md` with the working agreement: prompt-handling rule and the no-AI-footprint rule.

## Open blockers / decisions

None blocking implementation. Resolved during design:

1. ~~323-vs-324 byte layout~~ — resolved: byte 323 is alignment padding (verified).
2. ~~Endianness~~ — resolved: little-endian (verified).
3. ~~.NET SDK present~~ — resolved: 9.0.308 installed.

Minor / non-blocking:

- Fixture quality: the large capture covers driving; a future capture with deliberate
  braking, gear changes, and off-road would make richer test/dashboard fixtures. The user
  can record this on demand; not required to start.
