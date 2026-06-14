# FH6 Telemetry Inventory & Derived-Metric Verification

Date: 2026-06-13
Status: Research / analysis only (no source changes).
Sources of truth: `FH6_DATA_OUT_DOC.md`, `src/Fh6.Telemetry.Core/PacketParser.cs`,
`TelemetryPacket.cs`, `TelemetryReadout.cs`, `src/Fh6.Telemetry.Overlay/Widgets/*`.
Derived verification: ~21,400 moving (>2 m/s), race-on frames decoded from the real driving
capture `src/Fh6.Telemetry.Cli/bin/Debug/net8.0/capture-1781375262939.jsonl`
(AWD V8, class 5 / PI 900, CarOrdinal 1651).

---

## 1. Field inventory — decoded vs. surfaced

Every documented field (323 bytes, byte 323 is alignment padding) **is decoded** by
`PacketParser` into `TelemetryPacket`. The gap is what reaches a widget. Widgets present:
Gear, Speed, RPM/shift, Pedals+Steer, G-force, Boost, Fuel, Power/Torque, Lap timing, Map
(world position). The Chart widget can plot Throttle/Brake/Clutch/Steer/Speed/RPM/Gear/
Power/Torque/LatG/LongG. `TelemetryReadout` additionally exposes (but no widget shows):
SpeedMph, TorqueLbFt, Position, full `Acceleration` vector.

| Field(s) | Decoded | Surfaced in a widget | Notes |
|---|---|---|---|
| IsRaceOn | yes | indirectly (gates display) | used to gate, not shown |
| TimestampMs | yes | no | used for dt only |
| EngineMaxRpm / IdleRpm | yes | Max via RPM gauge scale; Idle **no** | |
| CurrentEngineRpm | yes | yes (RPM/shift) | |
| Acceleration X/Y/Z | yes | X,Z via G-force widget; **Y no** | vertical g unused |
| Velocity X/Y/Z | yes | no | needed for drift angle / heading |
| AngularVelocity X/Y/Z | yes | no | yaw rate (Y) unused — cornering radius |
| Yaw / Pitch / Roll | yes | no | pitch = gradient; unused |
| NormalizedSuspensionTravel ×4 | yes | **no** | per-corner suspension unused |
| SuspensionTravelMeters ×4 | yes | **no** | unused |
| TireSlipRatio ×4 | yes | **no** | wheelspin/lockup unused |
| WheelRotationSpeed ×4 | yes | **no** | gear-ratio/radius unused |
| WheelOnRumbleStrip ×4 | yes | **no** | unused |
| WheelInPuddle ×4 | yes | **no** | unused |
| SurfaceRumble ×4 | yes | **no** | unused |
| TireSlipAngle ×4 | yes | **no** | understeer/oversteer balance unused |
| TireCombinedSlip ×4 | yes | **no** | per-wheel grip usage unused |
| CarOrdinal / Class / PerformanceIndex | yes | **no** | car identity unused |
| DrivetrainType / NumCylinders / CarGroup | yes | **no** | unused |
| SmashableVelDiff / SmashableMass | yes | **no** | collision (FH6-only) unused |
| Position X/Y/Z | yes | X/Z via Map widget; **Y (altitude) no** | |
| Speed | yes | yes (Speed widget) | |
| Power / Torque | yes | yes (Power/Torque widget) | |
| TireTemp ×4 | yes | **no** (only mentioned as "shown" in brief; no widget exists) | unused |
| Boost | yes | yes (Boost widget) | |
| Fuel | yes | yes (Fuel) | |
| DistanceTraveled | yes | **no** | odometer/lap-split unused |
| BestLap / LastLap / CurrentLap | yes | yes (Lap timing) | |
| CurrentRaceTime | yes | **no** | |
| LapNumber / RacePosition | yes | LapNumber yes; RacePosition partial | |
| Accel / Brake / Clutch | yes | yes (Pedals) | |
| HandBrake | yes | **no** | unused |
| Gear | yes | yes | |
| Steer | yes | yes | |
| NormalizedDrivingLine | yes | **no** | line-adherence unused |
| NormalizedAIBrakeDifference | yes | **no** | unused |

**Summary of unused-but-decoded:** all 16 per-wheel tire/suspension channels (slip ratio,
slip angle, combined slip, normalized + metric suspension travel, wheel rotation, surface
rumble, rumble-strip, puddle), TireTemp ×4, Velocity & AngularVelocity vectors, vertical
acceleration, Yaw/Pitch/Roll, altitude (Position.Y), DistanceTraveled, CurrentRaceTime,
HandBrake, NormalizedDrivingLine, NormalizedAIBrakeDifference, car identity (Ordinal/Class/
PI/Drivetrain/Cylinders/Group), and the smashable-collision pair.

---

## 2. Missing / uncertain vs. the in-game HUD

The FH "advanced telemetry" overlay (FH5; FH6 inherits it) is paged. From community/official
docs the pages are: **General** (power, torque, boost, RPM, gear, clutch, speed, throttle,
brake, e-brake), **Tire** (per-tire temperature + a friction/grip percentage per corner),
**Suspension** (per-corner travel/offset), and **Body Acceleration** (lateral & longitudinal
g, traction). See sources below.

Display gaps relative to that in-game HUD (things the game shows that we do *not* surface,
even though we decode the inputs):

- **Per-tire temperature page** — we decode TireTemp ×4 but have no widget. Highest-value gap.
- **Per-tire friction/grip %** — game shows a peak-friction % per corner; we have the raw
  ingredient (`TireCombinedSlip`, where ~1.0 ≈ limit) but surface nothing.
- **Suspension page** — per-corner travel decoded (normalized + meters), no widget.
- **Body-acceleration / traction circle** — we show lat/long g numerically but not the g-g
  "circle" the game's body-accel page implies; vertical g is dropped entirely.
- **Handbrake / clutch state, e-brake** — clutch is on pedals; handbrake decoded but unused.

Unit / sign assumptions confirmed against the capture (these resolve the "Open decisions #5"
empirical checks from the derived-metrics catalog):

- **Acceleration is gravity-removed chassis accel.** Vertical g (AccelY/g) sits at p50 ≈ 0.0
  (not +1.0) while driving on the ground → the game already removes gravity. `LatG`/`LongG`
  in `TelemetryReadout` are therefore correct as-is (no ±1 g offset needed).
- **TireSlipRatio sign convention holds.** p50 ≈ +0.15 under net throttle; rear wheels go
  strongly negative (p1 ≈ −8.7) under braking/lockup — i.e. negative = wheel slower than road
  (lockup), positive = faster (wheelspin). Matches the catalog's assumption.
- **`Boost` is gauge PSI.** Min −14.7 PSI = full manifold vacuum (1 atm below ambient), max
  ~+12 PSI. Correct as PSI-above/below-atmospheric.
- **`Power` can be negative** (engine braking; ~16% of frames, all at 0% throttle).
  `TelemetryReadout.PowerHp` clamps to ≥0, which hides legitimate engine-braking data — a
  minor display choice to be aware of, not a decode bug. `TorqueLbFt` clamps similarly.
- **`TireTemp` units are °F** (p50 ≈ 178–182, max ~325). Forza's documented grip window
  (~175–200 °F) lines up — confirms °F, not °C.
- **`Fuel` did not change** (constant 1.0) in this free-roam capture → fuel-burn derived
  metrics can't be validated here; gate them on Fuel actually decreasing.
- **`DrivetrainType` = 2 = AWD** for this car (consistent with the V8 used); enum
  0/1/2 = FWD/RWD/AWD assumption is consistent but only one value observed.

Potential mis-read to double-check (not a decode error): `TireSlipAngle` is **not** the
normalized "1.0 = grip limit" quantity the doc comment claims. Observed |slip angle| has
p50 ≈ 0.4 and exceeds 1.0 in ~32% of frames, reaching ±15 — these read like **radians**
(±15 is unphysical for a steady angle but plausible as a transient/raw value), whereas the
doc text says "= 0 grip, |angle| > 1.0 = loss of grip." The decode is byte-correct; the
*semantic label in the doc* is suspect. Treat slip-angle balance as directional, not as a
calibrated angle, until confirmed against a tire-model reference.

---

## 3. Derived-metric verification (real capture)

Ranges below: p1 / p50 / p99 over ~21.4k moving race-on frames (full min/max in parentheses
where collisions/spikes dominate). "Sane" judged against physical expectations for aggressive
open-world driving.

| Metric | Observed (p1 / p50 / p99) | Verdict |
|---|---|---|
| **Lat g** = AccelX/9.81 | −2.06 / −0.04 / +1.55 (min −19.6 crash) | **Sane** — within ±2 g; symmetric about 0. |
| **Long g** = AccelZ/9.81 | −1.97 / +0.31 / +1.20 (min −20 crash) | **Sane** — braking deeper than accel, expected. |
| **Vert g** = AccelY/9.81 | −0.59 / −0.02 / +0.38 | **Sane** — centered on 0 → gravity removed (see §2). |
| **Combined g** = hypot(lat,long) | 0.16 / 0.79 / 3.57 | **Sane** — p99 3.6 g is at-the-limit; outliers are impacts. |
| **Drift angle** = atan2(Vx,Vz)° | −50.3 / 0.02 / +58.0 | **Sane** — large slides occur in free-roam; 0 when straight. |
| **U/O balance** = \|frontSA\|−\|rearSA\| | −5.76 / −0.04 / +2.08 | **Sane (directional)** — slightly rear-biased (oversteer-leaning), near 0 median; units uncalibrated (see §2). |
| **TireSlipRatio** (per wheel) | front −0.78 / 0.15 / 12; rear −8.7 / 0.17 / 13 | **Sane** — ≈0 grip median; rear goes very negative under lockup, positive under spin. |
| **TireCombinedSlip** (per wheel) | 0.0 / ~0.5 / 13–16 | **Sane** — median ~0.5 (in grip); >1.0 in ~37% of frames (hard driving); 0 = parked/coasting. |
| **Speed (m/s)** | 2.0 / 36.5 / — (max 53.6) | **Sane** — up to ~193 km/h. |
| **Speed vs \|Velocity\|** | diff = 0.000 everywhere | **Sane** — `Speed` exactly equals \|Velocity\|; confirms our Velocity decode (offsets) is correct. |
| **TireTemp (°F)** | 60 / ~180 / — (max 325) | **Sane** — cold→working→overheated; confirms °F. |

Nothing reads as a decode bug. The headline cross-check is **Speed − |Velocity| = 0.0 to
float precision**, which independently validates that the Acceleration/Velocity field offsets
are read correctly (a wrong offset would desync these). All g-forces and slip metrics land in
physically expected envelopes once collision spikes are excluded by percentile.

Caveats / "suspect-but-not-broken":
- **TireSlipAngle scale** — see §2; usable directionally for balance, not as a calibrated angle.
- **Fuel** constant → fuel-derived metrics unverified in this capture.
- **PowerHp/TorqueLbFt clamp negatives to 0** in `TelemetryReadout` (engine braking hidden).

---

## 4. "Add to V2" shortlist (ranked)

Ranked by value ÷ effort, favoring fields we already decode and metrics verified sane above.

1. **Tire widget: per-corner temperature + grip-used** — TireTemp (°F, with cold/optimal/hot
   band) plus `1 − clamp(TireCombinedSlip,0,1)` headroom per wheel. Directly matches the
   in-game Tire page; all inputs decoded and verified. Pure per-frame.
2. **Traction circle (g-g) widget** — plot (LatG, LongG) inside a ring scaled to peak observed
   g; add vertical g readout. Upgrades the existing numeric G-force widget; matches the Body
   Acceleration page. Verified ranges are clean.
3. **Drift / chassis slip-angle widget** — `atan2(Vx,Vz)` in degrees, with a drift-state flag.
   Verified sane; uses currently-unused Velocity. High appeal for a Horizon overlay.
4. **Wheelspin / lockup indicator** — per-driven-wheel `TireSlipRatio` sign+magnitude gated on
   throttle/brake; drivetrain enum picks driven wheels. Sign convention verified.
5. **Understeer/oversteer balance bar** — front vs rear avg slip angle, shown directionally
   (push vs rotate). Verified directionally; label as relative, not absolute degrees.
6. **Suspension page** — per-corner `NormalizedSuspensionTravel` bars (0=extended,1=compressed),
   matching the in-game Suspension page. Inputs decoded.
7. **Cornering radius / yaw-rate** — `Speed / |AngularVelocity.Y|`; AngVelY verified ±2 rad/s.
   Cheap, uses an unused field.
8. **Car identity strip** — CarOrdinal/Class/PI/Drivetrain/Cylinders as a small header. Static,
   trivial, fills the "what am I driving" gap.

Lower priority (need history/persistence, not pure per-frame): fuel-per-lap & range (gate on
Fuel changing), lap delta vs best (distance-keyed reference trace), 0–100/quarter-mile timers,
input-smoothness, line-adherence (`NormalizedDrivingLine`).

---

## Sources

- FH/FH5 in-game advanced telemetry HUD pages (General, Tire/Friction, Suspension, Body
  Acceleration) and tire-temperature usage:
  - ForzaTune tuning guide (FH6/Motorsport): https://forzatune.com/guide/the-fully-updated-forza-tuning-guide/
  - Forza Wiki — Telemetry: https://forza.fandom.com/wiki/Telemetry
  - GamerTweak — Enable telemetry HUD in FH5: https://gamertweak.com/enable-telemetry-fh5/
  - Sportskeeda — Turn on telemetry in FH5: https://www.sportskeeda.com/esports/how-turn-telemetry-forza-horizon-5-custom-hud
- Field semantics / units (slip-ratio sign, combined-slip ~1.0 = limit, tire-temp window):
  `FH6_DATA_OUT_DOC.md` plus the derived-metrics research catalog
  (`docs/superpowers/specs/2026-06-13-derived-metrics-research.md`), validated against the
  driving capture above.
