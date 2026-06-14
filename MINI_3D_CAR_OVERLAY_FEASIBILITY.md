# Feasibility: Mini 3D Car Overlay

Goal: render a small 3D car in the overlay that follows the in-game vehicle's
**position and orientation** using only the existing "Data Out" telemetry.

**Verdict: Feasible, low-to-moderate effort, no data blockers.** The telemetry already
carries a complete 6-DOF pose (world position + full orientation). The open questions are
all about rendering approach and a one-time orientation-sign calibration, not about whether
the data exists.

## 1. Data availability — the deciding question

Everything needed to pose a rigid body is present and already parsed in `TelemetryPacket`:

| Need | Telemetry field(s) | Notes |
|------|--------------------|-------|
| Position | `PositionX/Y/Z` (F32, world-space metres) | X/Z = ground plane, Y = altitude |
| Orientation | `Yaw`, `Pitch`, `Roll` (F32, radians) | Full attitude + heading |
| Motion (smoothing/extrapolation) | `VelocityX/Y/Z`, `AngularVelocityX/Y/Z` | Car-local; X=right, Y=up, Z=forward |
| Front-wheel steer angle | `Steer` (S8, -127..127) | For turning the front wheels |
| Wheel spin | `WheelRotationSpeed{FL,FR,RL,RR}` (rad/s) | For rolling the wheels |
| Suspension visuals | `NormalizedSuspensionTravel{...}` / `SuspensionTravelMeters{...}` | Optional ride-height/lean detail |

So the car body can be driven directly from `Yaw/Pitch/Roll` (+ `Position`), and the wheels
can additionally be steered and spun. This is strictly more than the minimum required.

### Current gaps in our own pipeline (small)
- `TelemetryPacket` parses `Yaw/Pitch/Roll` and `AngularVelocity`, but `TelemetryReadout`
  does **not** surface them yet (`PacketParser.cs:40-43`, `TelemetryReadout.cs`). Trivial to
  add — mirror the existing `Position` / `Acceleration` properties.
- Orientation angles are currently unused anywhere in the overlay (`SteeringHorizonWidget`
  uses steer/G-force, not real pitch/roll), so the sign/order convention is **greenfield and
  needs empirical calibration** against a replay.

## 2. Hard constraints from the data

- **No world geometry.** Telemetry gives the car's own pose only — there is no track, road,
  or scenery. A 3D car driving on a 3D *world* is impossible. What is possible:
  - **Attitude/heading indicator** — car model tilts with pitch/roll and rotates with yaw.
  - **Procedural ground** — car on a synthetic grid/plane that scrolls and rotates with
    velocity/heading to convey speed and direction.
  - **Position breadcrumb** — a fading 3D trail from `Position` history (the 2D `MapWidget`
    on `backlog/map-overlay` already does this idea in 2D; this is its 3D cousin).
- **Data only flows while driving.** Nothing is sent in menus, pauses, replays, or rewinds.
  The widget must freeze on the last good pose and visibly indicate "no signal" (every
  existing widget already handles stale data).
- **Coordinate-system mapping.** Forza uses car-local X=right/Y=up/Z=forward and world
  X/Z ground-plane with Y up; this must be mapped to WPF's right-handed Y-up 3D space (or
  Skia's screen space). The exact yaw/pitch/roll rotation order and sign almost certainly
  need flipping — resolve empirically with a replay, same as the map/horizon widgets did.

## 3. Rendering approach options

The overlay is **WPF + SkiaSharp** (2D GPU canvas); no 3D library is referenced today.

| Option | What it is | Pros | Cons |
|--------|-----------|------|------|
| **A. Skia manual projection** (recommended for a "mini" indicator) | Project a low-poly/flat-shaded car mesh ourselves with matrices, draw on the existing `SKCanvas` | No new dependency; stays in the current widget rendering stack; integrates cleanly with the layered-border shadow + theme system; full control | We write our own transform + back-face cull + painter's-order sort; flat shading only (no fancy lighting) |
| **B. WPF `Viewport3D`** (built-in) | Native retained-mode 3D: `MeshGeometry3D`, camera, lights | No NuGet; real depth buffer & lighting; can use a procedural box-car or imported mesh | Introduces an `Effect`/ClearType interaction risk (see CLAUDE.md card-shadow gotcha — a 3D viewport in the tree is fine, but mixing it with text effects needs care); separate render path from the Skia widgets |
| **C. HelixToolkit.Wpf** | NuGet wrapper over `Viewport3D` with OBJ/STL loading + camera helpers | Easiest path to a *realistic* imported car mesh; camera/orbit helpers | Adds a dependency; heavier than a "generic mini" indicator needs |

**Recommendation:** For a *generic mini* car (a small low-poly attitude/heading indicator,
not a detailed model), **Option A** is the lowest-friction, most consistent choice — it keeps
everything in the established SkiaSharp widget pattern and avoids the WPF `Effect`/text
gotchas the project has already been bitten by. Choose **C** only if the requirement is a
recognisable, detailed car mesh.

## 4. Design choices for "follows position and orientation"

- **Camera framing:** car-fixed / world-rotates (the classic attitude-indicator look —
  recommended for a compact widget) vs. world-fixed with the car translating in a small
  arena. For a mini overlay, lock the camera and apply orientation to the model.
- **Position usage:** since there's no track, raw position is most useful as a **breadcrumb
  trail** and/or to drive the scrolling ground grid; orientation alone is enough for a pure
  attitude/heading readout.
- **Smoothing:** packets arrive at the game's frame rate (variable). Interpolate pose per
  render frame and optionally extrapolate using `AngularVelocity`/`Velocity` between packets.
  Existing widgets already redraw per frame, so the loop exists.

## 5. Integration sketch (follows existing patterns)

1. Surface `Yaw/Pitch/Roll` (+ `AngularVelocity`, `WheelRotationSpeed`, `Steer` already
   present) on `TelemetryReadout`.
2. Add a `Car3DWidget` following the standard widget shape: back `Border` (surface + rim +
   `Fh6.PanelShadow`) and a transparent front `Border` hosting the render surface; register
   in `WidgetId`, `LayoutSeeds`, and settings.
3. Drive the model from the readout each frame; freeze + dim on stale data.
4. **Calibrate** yaw/pitch/roll sign and rotation order against a replay
   (`--replay <capture.jsonl>`), screenshotting per the overlay visual-verification workflow.

## 6. Risks

- **Low:** orientation convention (sign/order) — cheap to resolve iteratively against a
  capture; no data risk.
- **Low–moderate:** rendering-stack choice (Skia projection vs. Viewport3D) — affects effort
  and visual ceiling, not feasibility.
- **None:** data availability — the full pose is present and parsed.

## 7. Suggested next step

Prototype Option A: a flat-shaded low-poly car projected on the existing Skia canvas, driven
by `Yaw/Pitch/Roll`, validated against `capture-*.jsonl` via replay — starting as an
attitude/heading indicator with an optional scrolling ground grid and position breadcrumb.
