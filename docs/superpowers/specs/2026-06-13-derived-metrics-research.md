# FH6 Derived / Computed Metrics — Research Catalog

Date: 2026-06-13
Status: Research (no code changes). Source of field truth: `src/Fh6.Telemetry.Core/TelemetryPacket.cs`,
`TelemetryReadout.cs`, `Wheels.cs`, `Vec3.cs`.

## Purpose

Forza Horizon 6 "Data Out" emits a fixed UDP packet ~60 Hz. The game exposes many raw fields but
**not** the higher-level values a driver/coach actually reasons about (g-forces, slip-based balance,
lap delta, fuel range, gear ratios, cornering radius…). This doc catalogs metrics we can *derive*
from the fields we already decode, with formulas, required fields, state requirements, and an honest
confidence note. It is a menu for future widgets — not an implementation plan.

## Conventions & assumptions used throughout

- **Axes (car-local):** Acceleration/Velocity/AngularVelocity X = right, Y = up, Z = forward (per our
  field doc). So longitudinal = Z, lateral = X, vertical = Y.
- **g constant:** `g = 9.80665 m/s²`.
- **Frame rate:** assume ~60 Hz but **never trust a fixed dt** — always compute
  `dt = (TimestampMs_now − TimestampMs_prev) / 1000` because packets drop and the rate is not
  guaranteed. Forza's Data Out is documented as ~60 fps UDP. ([HierographX/forza-data-tools],
  [Forza support / community])
- **State legend:** **PF** = pure per-frame (no history; trivial widget). **H** = needs a small
  rolling buffer / previous-frame delta. **HH** = needs a full lap/run trace or per-track reference
  (heavy: persistence + calibration).
- **Wheels order:** `FrontLeft, FrontRight, RearLeft, RearRight` (see `Wheels.cs`). "Front axle" =
  mean(FL,FR); "rear axle" = mean(RL,RR).
- **`IsRaceOn`/gating:** every history-based metric must reset when `IsRaceOn` goes false, and
  ideally on lap rollover / large `Position` jumps (teleport, rewind).
- **Important caveat — vehicle mass is NOT exposed.** `SmashableMass` is *collision* mass for the
  object you can smash, not the player car's mass. So anything truly needing kg (force in N, exact
  power-to-weight, downforce) is **not** directly derivable. See "Powertrain" for the only partial
  workaround and its uncertainty.

---

## TOP 10 TO IMPLEMENT FIRST (ranked)

Ranked by value ÷ effort. Effort: **S** ≈ a few lines, **M** ≈ helper + small buffer/state, **L** ≈
persistence/calibration/reference trace.

| # | Metric | One-liner | Effort | Kind |
|---|--------|-----------|--------|------|
| 1 | **G-force (lat/long/vert) + traction circle** | Accel components ÷ g; magnitude = grip used | S | PF |
| 2 | **Heading & cornering radius** | Heading from velocity; R = speed ÷ yaw-rate | S | PF |
| 3 | **Wheelspin / lockup detector** | TireSlipRatio sign+spike vs throttle/brake | S | PF |
| 4 | **Chassis slip angle (drift angle)** | `atan2(lateralV, longitudinalV)` in degrees | S | PF |
| 5 | **Understeer/oversteer balance** | front avg TireSlipAngle − rear avg | S | PF |
| 6 | **Lap delta vs best** | current time − best-lap time at same distance | L | HH |
| 7 | **0–100 km/h / 0–60 / ¼-mile timers** | integrate time across speed thresholds | M | H |
| 8 | **Fuel per lap + laps remaining** | ΔFuel per lap → range estimate | M | H |
| 9 | **Tire temp axle balance + grip headroom** | F/R & L/R temp deltas; 1−CombinedSlip | S | PF |
| 10 | **Input smoothness (jerk) & line adherence** | d(input)/dt; |NormalizedDrivingLine| | M | H |

Notes: #1–#5, #9 are pure per-frame → cheap, high-impact HUD widgets, do these first. #7, #8, #10
need a rolling buffer (light state). #6 is the big one (a distance-keyed reference trace + storage)
and is the single most valuable coaching feature, so it earns rank 6 despite the effort.

---

## 1. G-forces / traction circle

### 1.1 Longitudinal g
- **Tells:** acceleration/braking force the driver is pulling.
- **Formula:** `gLong = Acceleration.Z / 9.80665`. Positive = accel, negative = braking.
- **Fields:** `Acceleration.Z`. **State:** PF. **Confidence:** High (direct read).

### 1.2 Lateral g
- **Tells:** cornering load; how hard the tires are working sideways.
- **Formula:** `gLat = Acceleration.X / 9.80665` (sign = left/right).
- **Fields:** `Acceleration.X`. **State:** PF. **Confidence:** High.

### 1.3 Vertical g
- **Tells:** bumps/compressions/airtime (≈0 g airborne, >1 g on landing/compression).
- **Formula:** `gVert = Acceleration.Y / 9.80665`. Note this is the *reported* vertical accel; on
  flat ground at rest expect ≈ +1 g if gravity is included, ≈ 0 if the game reports proper
  (gravity-removed) accel — **needs a quick empirical check** (sit still, read Y). Document whichever
  the game does and offset accordingly.
- **Fields:** `Acceleration.Y`. **State:** PF. **Confidence:** Med (gravity-offset convention TBD).

### 1.4 Combined / traction-circle magnitude
- **Tells:** total grip utilization; a g-g diagram is the classic coaching tool — staying near the
  edge of the circle = using all available grip.
- **Formula:** `gCombined = hypot(gLong, gLat) = sqrt(gLong² + gLat²)`. Plot (gLat, gLong) as a dot
  inside a circle of radius ≈ peak observed g.
- **Fields:** `Acceleration.X`, `Acceleration.Z`. **State:** PF (peak-g for the ring scale is H —
  track a rolling/run max). **Confidence:** High.

---

## 2. Acceleration / performance timers

All require detecting a **start condition** and integrating time. Use `TimestampMs` deltas, and
interpolate across the threshold frame for accuracy (don't just snap to the frame that crossed it).

### 2.1 0–60 mph / 0–100 km/h
- **Tells:** standing-start acceleration benchmark.
- **Derivation:** arm when `Speed ≈ 0` (and throttle applied); start clock at first frame Speed > ~0;
  stop when `SpeedKmh ≥ 100` (or `SpeedMs ≥ 26.8224` for 60 mph). Linear-interpolate the crossing
  frame: `tCross = tPrev + (target − vPrev)/(vNow − vPrev) * dt`.
- **Fields:** `Speed`, `TimestampMs`, `Accel` (to confirm a launch). **State:** H. **Confidence:** High.

### 2.2 ¼-mile (and 1/8-mile) time + trap speed
- **Tells:** drag benchmark.
- **Derivation:** same launch arming; accumulate distance via `ΔDistanceTraveled` (preferred, it's a
  game odometer in meters) or integrate Speed·dt. Stop at 402.336 m (¼ mile); "trap speed" =
  `Speed` at that point. Interpolate the crossing.
- **Fields:** `DistanceTraveled` (or `Speed`), `Speed`, `TimestampMs`. **State:** H. **Confidence:** High.

### 2.3 Rolling / in-gear times (e.g. 50–70 mph)
- **Tells:** passing/flexibility benchmark.
- **Derivation:** same crossing logic between two speed bounds; no zero-start needed.
- **Fields:** `Speed`, `TimestampMs`. **State:** H. **Confidence:** High.

---

## 3. Braking & traction events

### 3.1 Braking g
- **Tells:** how hard you're braking; threshold-braking feedback.
- **Formula:** `brakingG = max(0, −Acceleration.Z) / 9.80665`, gated on `Brake > 0`.
- **Fields:** `Acceleration.Z`, `Brake`. **State:** PF. **Confidence:** High.

### 3.2 Braking distance (e.g. 100→0 km/h)
- **Tells:** stopping performance.
- **Derivation:** mirror of §2.1/2.2 but decelerating; accumulate `ΔDistanceTraveled` from the frame
  Speed first drops below the upper bound while `Brake>0` until Speed ≤ lower bound.
- **Fields:** `DistanceTraveled`, `Speed`, `Brake`, `TimestampMs`. **State:** H. **Confidence:** High.

### 3.3 Lockup detection
- **Tells:** a wheel is sliding under braking (flat-spotting / lost steering) → ease off.
- **Heuristic:** `TireSlipRatio` is (wheel surface speed − road speed)/road speed. Under braking the
  wheel turns slower than the car → **negative** slip ratio. Flag lockup when
  `Brake > 0 AND TireSlipRatio[w] < −threshold` (start ~ −0.15..−0.25; tune empirically) for any
  wheel. Cross-check with `TireCombinedSlip[w] > ~1` (saturated).
- **Fields:** per-wheel `TireSlipRatio`, `Brake`, optionally `TireCombinedSlip`. **State:** PF (a
  short debounce buffer reduces flicker → H-lite). **Confidence:** Med (threshold needs tuning; sign
  convention should be confirmed against a live capture).

### 3.4 Wheelspin detection
- **Tells:** driven wheels spinning up under power → traction loss, lost drive.
- **Heuristic:** under throttle the driven wheels spin faster than road speed → **positive** slip
  ratio. Flag when `Accel > 0 AND TireSlipRatio[drivenWheel] > +threshold`. Use `DrivetrainType`
  (0=FWD,1=RWD,2=AWD — confirm mapping from a capture) to pick which wheels count.
- **Fields:** per-wheel `TireSlipRatio`, `Accel`, `DrivetrainType`. **State:** PF/H-lite.
  **Confidence:** Med (threshold + drivetrain enum mapping need confirmation).

---

## 4. Handling balance (understeer / oversteer)

### 4.1 Slip-angle balance (primary)
- **Tells:** is the car pushing (understeer) or rotating/snapping (oversteer)? The canonical
  definition: understeer when front slip angle > rear; oversteer when rear > front.
  ([Wikipedia: Slip angle], [Forza tuning community])
- **Formula:**
  `frontSA = (TireSlipAngle.FL + TireSlipAngle.FR)/2`,
  `rearSA = (TireSlipAngle.RL + TireSlipAngle.RR)/2`,
  `balance = |frontSA| − |rearSA|` → `>0` understeer, `<0` oversteer, near 0 = neutral.
  Only meaningful while cornering (`|gLat| > ~0.2` or `|AngularVelocity.Y| > small`).
- **Fields:** per-wheel `TireSlipAngle`, (gate: `Acceleration.X`). **State:** PF. **Confidence:**
  High on direction; the absolute scale/units of `TireSlipAngle` (radians vs degrees) must be
  confirmed from a capture before labeling a magnitude.

### 4.2 Yaw-rate vs expected (kinematic balance)
- **Tells:** independent confirmation of understeer/oversteer via how much the car is *actually*
  rotating vs what its speed+steering imply.
- **Derivation:** expected steady-state yaw rate ≈ `v / R`. Neutral reference uses the bicycle model
  `yaw_des = v·δ / (L + K·v²)` (L = wheelbase, K = stability factor) — but **we don't have wheelbase
  or steering-angle-in-radians** (only `Steer` −127..127, a normalized input, not road-wheel angle).
  Practical proxy: compare measured yaw rate `AngularVelocity.Y` against `gLat·g / v` (the yaw rate a
  pure circular path of the current lateral accel implies): `expectedYaw = (Acceleration.X)/Speed`.
  If `|measuredYaw| < |expectedYaw|` → understeer (not rotating enough); `>` → oversteer.
- **Fields:** `AngularVelocity.Y`, `Acceleration.X`, `Speed`. **State:** PF. **Confidence:** Med
  (proxy, no true steering angle / wheelbase; best used as a corroborating signal, not absolute).

### 4.3 Per-wheel / per-axle grip usage
- **Tells:** which corner is closest to letting go.
- **Formula:** `gripUsed[w] = clamp(TireCombinedSlip[w], 0, 1)`; `headroom = 1 − gripUsed`.
  `TireCombinedSlip` ≈ normalized combined (long+lat) slip where ~1.0 = peak grip. Axle averages
  show front-vs-rear loading.
- **Fields:** per-wheel `TireCombinedSlip`. **State:** PF. **Confidence:** Med-High (the ~1.0 = limit
  convention is the documented Forza meaning; confirm scaling on a capture).

---

## 5. Drift

### 5.1 Chassis (body) slip angle — "drift angle"
- **Tells:** how sideways the car is; the core drift metric.
- **Formula:** body slip angle `β = atan2(lateralVelocity, longitudinalVelocity) = atan2(Velocity.X,
  Velocity.Z)`, in degrees `= β·180/π`. 0° = straight; large |β| = sliding sideways.
  (Standard sideslip definition — angle between heading and actual velocity vector.)
- **Fields:** `Velocity.X`, `Velocity.Z`. **State:** PF. **Confidence:** High.

### 5.2 Drift detection / state
- **Tells:** are we drifting (vs gripping)?
- **Heuristic:** `isDrifting = Speed > ~8 m/s AND |β| > ~10–15° AND rearSlip elevated`
  (`mean(TireCombinedSlip.RL,RR) > ~0.9`). Tune thresholds.
- **Fields:** `Velocity`, `Speed`, rear `TireCombinedSlip`. **State:** PF. **Confidence:** Med.

### 5.3 Drift score (homebrew)
- **Tells:** gamified drift quality (FH6 has its own scoring, but we can't read it).
- **Derivation:** integrate `Speed · |β| · dt` while `isDrifting`, with a multiplier for sustained
  duration and a reset/penalty when β → 0 or a wall hit (`SmashableVelDiff` spike). Purely our own
  formula; will **not** match the in-game score.
- **Fields:** `Speed`, `β`, `TimestampMs`, optionally `SmashableVelDiff`. **State:** H.
  **Confidence:** Low (heuristic, not authoritative).

---

## 6. Lap timing & coaching (distance-keyed)

These are the highest-value coaching metrics and the heaviest (need persistence + a reference trace).

### 6.1 Sector times by distance split
- **Tells:** where on the lap you gain/lose time, without official sector markers (FH6 Data Out has
  no sector fields).
- **Derivation:** define N sectors by fraction of lap distance. Need **lap length**: capture it as the
  `DistanceTraveled` delta between successive `LapNumber` increments on a clean lap. Then sector
  boundaries = lapStartDistance + k/N · lapLength; record `CurrentRaceTime` (or `CurrentLap`) at each
  crossing → sector time = difference.
- **Fields:** `DistanceTraveled`, `LapNumber`, `CurrentLap`/`CurrentRaceTime`. **State:** HH (needs
  lap-length calibration + per-lap storage). **Confidence:** Med (splits are synthetic, not official,
  but consistent within a track).

### 6.2 Live lap delta vs best
- **Tells:** real-time ± seconds vs your best lap — the single most useful coaching readout.
- **Derivation:** record a **reference trace** of the best lap as `(distanceIntoLap → elapsedLapTime)`
  samples. Each frame compute `distanceIntoLap = DistanceTraveled − lapStartDistance`, look up the
  reference time at that distance (interpolate), `delta = CurrentLap − refTime(distanceIntoLap)`.
  Update the reference whenever `LastLap` improves on `BestLap`.
- **Fields:** `DistanceTraveled`, `CurrentLap`, `BestLap`, `LastLap`, `LapNumber`. **State:** HH
  (store the whole best-lap trace; reset on track change — note we can't detect track id directly, so
  key on a manual track selection or detect via position bounds). **Confidence:** Med-High for the
  math; the operational risk is detecting "same track" and lap-start distance reliably.

### 6.3 Predicted lap time
- **Tells:** projected final lap time given current delta.
- **Derivation:** `predicted = bestLap + currentDelta` (simple), or sector-weighted.
- **Fields:** as §6.2. **State:** HH. **Confidence:** Med (depends on §6.2 quality).

### 6.4 Theoretical best ("optimal") lap
- **Tells:** sum of your best-ever sector times = the lap you're capable of.
- **Derivation:** keep best time per sector across the session (from §6.1); optimal = Σ best sectors.
- **Fields:** as §6.1. **State:** HH. **Confidence:** Med (only as good as the synthetic sectors).

### 6.5 Lap-time consistency
- **Tells:** how repeatable you are (std-dev of lap times) — key for endurance/clean driving.
- **Derivation:** collect completed `LastLap` values, compute mean/σ/spread. PF to read each lap; the
  stats need a list.
- **Fields:** `LastLap`, `LapNumber`. **State:** H. **Confidence:** High.

---

## 7. Fuel

`Fuel` is 0..1 (fraction of tank). Tank litres are **not** exposed, so all fuel metrics are in
"tank-fractions" unless the user supplies a tank capacity. Many FH6 cars don't consume fuel in
non-race modes — gate on `Fuel` actually changing.

### 7.1 Fuel per lap
- **Tells:** burn rate for race strategy.
- **Derivation:** `fuelPerLap = Fuel(atLapStart_n) − Fuel(atLapStart_{n+1})`. Average over recent laps.
- **Fields:** `Fuel`, `LapNumber`. **State:** H. **Confidence:** High (in tank-fractions).

### 7.2 Fuel per minute / per km
- **Derivation:** `dFuel/dt = (Fuel_prev − Fuel_now)/dt` smoothed; per-km via `ΔDistanceTraveled`.
- **Fields:** `Fuel`, `TimestampMs`, `DistanceTraveled`. **State:** H. **Confidence:** High.

### 7.3 Laps / distance / time remaining (range)
- **Tells:** "can I make it to the end?"
- **Derivation:** `lapsRemaining = Fuel_now / fuelPerLap`; `kmRemaining = Fuel_now / fuelPerKm`;
  `minutesRemaining = Fuel_now / fuelPerMinute`.
- **Fields:** `Fuel` + the rate from §7.1/7.2. **State:** H. **Confidence:** Med (rate must stabilize;
  noisy early in a stint).

---

## 8. Tires

### 8.1 Axle temp balance
- **Tells:** front-vs-rear and left-vs-right tire loading; tuning/driving-style feedback.
- **Formula:** `frontTemp = avg(TireTemp.FL,FR)`, `rearTemp = avg(RL,RR)`,
  `axleDelta = frontTemp − rearTemp`; `leftDelta`, `rightDelta` analogous. (Units assumed °F per
  Forza convention — confirm on a capture.)
- **Fields:** per-wheel `TireTemp`. **State:** PF. **Confidence:** High (math); Med (unit/scale).

### 8.2 Optimal-window heuristic
- **Tells:** are tires in their grip window?
- **Derivation:** Forza's grip-vs-temp peak is commonly cited ≈ 175–200 °F (≈ 80–93 °C). Flag
  per-tire cold/optimal/hot vs a configurable band. **This window is a community heuristic, not a
  documented FH6 constant** — expose the band as a setting.
- **Fields:** per-wheel `TireTemp`. **State:** PF. **Confidence:** Low-Med (heuristic band).

### 8.3 Per-wheel grip headroom
- Duplicate of §4.3 (`1 − TireCombinedSlip`), surfaced as a tire widget. PF, Med-High.

---

## 9. Powertrain

### 9.1 Estimated gear ratios
- **Tells:** the car's gearing; needed for shift-point math and a gear-ratio display.
- **Derivation:** in a steady pull in gear `g`, `ratio_g ∝ CurrentEngineRpm / wheelAngularSpeed`. Use
  driven-wheel `WheelRotationSpeed` (rad/s). Collect samples per `Gear` while `Clutch≈0` and slip is
  low, fit `RPM = ratio_g · wheelSpeed`. Final-drive × gear can't be separated without tire radius,
  but the *relative* ratios and shift-RPM mapping are obtainable.
- **Fields:** `CurrentEngineRpm`, `WheelRotationSpeed` (driven), `Gear`, `Clutch`, `TireSlipRatio`
  (to reject wheelspin). **State:** HH (per-gear calibration). **Confidence:** Med.

### 9.2 Effective wheel/tire radius (bonus)
- **Derivation:** `radius ≈ Speed / WheelRotationSpeed` (m/s ÷ rad/s) when not slipping. Enables
  converting wheel speed↔road speed and improves §9.1.
- **Fields:** `Speed`, `WheelRotationSpeed`, `TireSlipRatio`. **State:** H. **Confidence:** Med.

### 9.3 Shift-point optimization
- **Tells:** the RPM at which to upshift to maximize acceleration (where wheel force in gear N drops
  below gear N+1).
- **Derivation:** with §9.1 ratios and the `Power`/`Torque` curve sampled vs RPM, compute wheel force
  `F_wheel ≈ Torque · ratio / radius` per gear and find the cross-over RPM. Even without radius, the
  cross-over RPM is found from `Torque·ratio` comparisons (radius cancels).
- **Fields:** `Torque`, `Power`, `CurrentEngineRpm`, ratios (§9.1). **State:** HH (needs a sampled
  torque curve + ratios). **Confidence:** Med (classic method; quality depends on curve coverage).

### 9.4 Wheel torque / tractive force
- **Tells:** torque actually delivered to the wheels.
- **Derivation:** `wheelTorque ≈ engineTorque · totalRatio` (needs §9.1 ratio incl. final drive,
  which we only get *relatively*). Tractive **force** in N needs radius (§9.2) and is then
  approximate. Power at wheels is just `Power` (already given).
- **Fields:** `Torque`, ratios, radius. **State:** HH. **Confidence:** Low-Med.

### 9.5 Power-to-weight — **NOT directly derivable**
- **Why:** mass is not in the packet (`SmashableMass` is collision mass of smashables, not the car).
  Options, all flagged uncertain:
  1. **User-entered mass** (per car) → exact P/W and force in N. Cleanest; recommend this.
  2. **Estimate mass via F = m·a:** during a known longitudinal force event mass = F/a. But we have
     `a` (Acceleration.Z) and **not** F in Newtons — `Power`/`Speed` gives drive force only when
     drag/rolling losses are ~known, which they aren't. So `m ≈ (Power/Speed) / a` during hard accel
     is a *rough* estimate biased high (ignores losses) — usable as a ballpark only.
  3. Look up mass from `CarOrdinal` via an external car DB (HH; needs a data file).
- **Confidence:** Low for any auto-estimate; **recommend user-supplied mass** for real P/W.

---

## 10. Motion / road geometry

### 10.1 Heading (course)
- **Formula:** `heading = atan2(Velocity.X, Velocity.Z)` (world course from velocity), or use `Yaw`
  directly for chassis heading. Difference between the two = body slip angle (§5.1).
- **Fields:** `Velocity.X/Z` or `Yaw`. **State:** PF. **Confidence:** High.

### 10.2 Cornering radius
- **Tells:** corner tightness; line analysis.
- **Formula:** `R = Speed / |AngularVelocity.Y|` (m). Equivalently `R = v² / a_lat = Speed² /
  |Acceleration.X|`. Guard small yaw-rate (straight → R→∞; clamp/display "∞"). Both forms cross-check.
- **Fields:** `Speed`, `AngularVelocity.Y` (and/or `Acceleration.X`). **State:** PF. **Confidence:** High.

### 10.3 Gradient / elevation change
- **Tells:** uphill/downhill; useful for delta context.
- **Formula:** instantaneous grade `≈ tan(Pitch)` (×100 for %); or empirical
  `grade = ΔPosition.Y / horizontalDistance` where `horizontalDistance = hypot(ΔPos.X, ΔPos.Z)`.
  `Position.Y` is altitude (m).
- **Fields:** `Pitch`, or `Position` deltas. **State:** PF (pitch) / H (position delta).
  **Confidence:** Med-High.

### 10.4 Airborne detection
- **Tells:** jumps / wheels off ground (common in Horizon).
- **Heuristic:** all four `NormalizedSuspensionTravel` near full extension (≈1.0) **and** low
  `|Acceleration.Y|` (near free-fall) → airborne. Cross-check rising `Position.Y` with negative road
  contact. Combine signals to avoid false positives over crests.
- **Fields:** `NormalizedSuspensionTravel` (all 4), `Acceleration.Y`, optionally `Position.Y`.
  **State:** PF (debounce → H-lite). **Confidence:** Med.

### 10.5 Odometer / trip distance
- **Formula:** accumulate `ΔDistanceTraveled` (already a meter odometer) or integrate `Speed·dt`.
  Reset per session/lap as desired.
- **Fields:** `DistanceTraveled` (preferred) or `Speed`+`TimestampMs`. **State:** H. **Confidence:** High.

### 10.6 Slip vs road surface flags (context)
- `WheelOnRumbleStrip`, `WheelInPuddle`, `SurfaceRumble` already classify contact; combine with §3/§4
  to explain grip loss ("lost rear on the puddle"). PF, High (direct reads).

---

## 11. Consistency / coaching inputs

### 11.1 Input smoothness (jerk / rate-of-change)
- **Tells:** jerky vs smooth driving; smoother inputs ≈ faster, more stable.
- **Formula:** for each of throttle/brake/steer, `rate = Δinput/dt`; smoothness score = inverse of
  RMS rate over a rolling window. Inputs from `Accel/255`, `Brake/255`, `Steer/127` (we already
  compute these fractions in `TelemetryReadout`).
- **Fields:** `Accel`, `Brake`, `Steer`, `TimestampMs`. **State:** H (rolling window).
  **Confidence:** High.

### 11.2 Trail-braking / overlap
- **Tells:** simultaneous brake+throttle or brake-into-corner technique.
- **Formula:** flag/measure `Brake>0 AND Accel>0` overlap; or brake pressure vs steering angle to
  detect trail-braking.
- **Fields:** `Brake`, `Accel`, `Steer`. **State:** PF/H. **Confidence:** High (definition), Med (what
  counts as "good").

### 11.3 Racing-line adherence
- **Tells:** how far off the game's ideal line you are.
- **Formula:** `|NormalizedDrivingLine|` (−127..127) is the game's encoded deviation from the racing
  line; near 0 = on line. Also `NormalizedAIBrakeDifference` shows braking earlier/later than the AI
  reference. Average |value| over a lap = an adherence score.
- **Fields:** `NormalizedDrivingLine`, `NormalizedAIBrakeDifference`. **State:** PF (instant) / H
  (lap average). **Confidence:** Med (exact encoding/units of these fields should be confirmed from a
  capture; semantics are inherited from FM/FH telemetry).

---

## Open decisions for the user

1. **Units:** mph or km/h (and miles vs km, °C vs °F, g vs m/s²)? Several metrics need a unit
   preference; recommend a single global units setting reused everywhere.
2. **History buffer:** are we OK adding a rolling-state layer (a `DerivedMetricsTracker` that holds
   prev-frame + small ring buffers) alongside the stateless `TelemetryReadout`? Everything in §2,7,8,
   11 and the lap features need it. Recommend yes, kept in Core, unit-tested, reset on `!IsRaceOn`.
3. **Lap-delta reference storage:** how much persistence do we want? In-memory per-session is easy;
   cross-session best laps need a file + a way to key by track (we can't read track id, so likely a
   manual track selector or position-bounds heuristic).
4. **Vehicle mass:** accept that true power-to-weight / forces-in-Newtons need a user-entered mass (or
   a `CarOrdinal→mass` data file). Confirm whether to add a per-car mass input.
5. **Empirical confirmations to schedule (1 short capture each):** vertical-g gravity offset (§1.3),
   `TireSlipRatio` sign & lockup/wheelspin thresholds (§3.3/3.4), `TireSlipAngle`/`TireCombinedSlip`
   units & scale (§4), `TireTemp` units (§8), `DrivetrainType` enum mapping (§3.4), and
   `NormalizedDrivingLine` encoding (§11.3). These convert several "Med" confidences to "High".

---

## Sources

- Forza "Data Out" UDP format & ~60 Hz, field semantics (sled/dash format inherited by FH6):
  community decoders, e.g. HierographX `forza-data-tools`, and Forza support docs.
- Slip angle / understeer-oversteer definitions: Wikipedia "Slip angle"; Forza tuning community
  guides (forzatune.com, simracingsetup.com).
- Cornering yaw-rate relation `R = v / yaw` and bicycle-model `yaw_des = v·δ/(L+Kv²)`: standard
  vehicle dynamics (Milliken, *Race Car Vehicle Dynamics*); Springer "Yaw rate and side-slip control
  considering vehicle longitudinal dynamics".
- Body slip angle `β = atan2(v_lat, v_long)`: standard sideslip definition (Wikipedia "Slip angle").

(Web searches conducted 2026-06-13; field-level semantics should be validated against a live FH6
capture as noted in "Open decisions" #5, since FH6's exact Data Out layout/units are not yet
authoritatively documented and are assumed from the FM/FH5 lineage our decoder targets.)
