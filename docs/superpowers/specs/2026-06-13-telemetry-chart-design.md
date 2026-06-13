# FH6 Telemetry Overlay — Time-Series Chart Widget Design

Date: 2026-06-13
Status: Draft for review (needs user sign-off on the open questions in the last section)
Builds on:
- `docs/superpowers/specs/2026-06-13-fh6-overlay-design.md` (v1 overlay MVP)
- `docs/superpowers/specs/2026-06-13-fh6-widget-customization-design.md` (per-widget customization / `FreeLayout`)
- `docs/superpowers/specs/2026-06-13-derived-metrics-research.md` (candidate series catalog)

## Goal

Add a **scrolling multi-line time-series chart** as a new overlay widget (`ChartWidget`). One
shared plot rectangle, X = time (most recent window scrolls right-to-left), Y = several telemetry
channels drawn as distinct colored traces. Each series is individually toggleable, the time window
is configurable (e.g. 30 / 60 / 120 s), and the widget lives inside the existing
`FreeLayout` / `WidgetId` / `OverlayConfig` customization system exactly like the other widgets.

This is the classic sim-racing "trace" view: it lets you see *how* inputs and outputs evolve over a
stretch of driving (throttle/brake/speed through a corner, power/torque/rpm sweeping the powerband),
rather than the instantaneous gauges the existing widgets show.

### Scope

**MVP (this spec's primary target):**
- New `ChartHistory` ring-buffer sampler in `Fh6.Telemetry.Core` (stateless `TelemetryReadout`
  stays as-is; this is the new *stateful* companion, reset on `!IsRaceOn`).
- A fixed catalog of selectable series sourced from existing `TelemetryReadout` fields.
- **Per-series normalization to 0..1 in one shared plot** (scale strategy chosen below), drawn with
  one `StreamGeometry`/`Path` per visible series on a `Canvas`, redrawn on the existing
  `CompositionTarget.Rendering` tick.
- Configurable time window; per-series visibility; persisted in `OverlayConfig`.
- A legend showing each series' name, color, and current real (un-normalized) value.
- Settings UI: enable/disable the widget, pick the time window, toggle each series.

**Later (explicitly out of MVP, noted for forward-compat):**
- Lap-delta / distance-keyed series (needs the `HH` reference-trace machinery from the derived-metrics
  doc — heavy, deferred).
- Multiple chart instances (more than one `ChartWidget`).
- Dual-Y-axis or stacked-lane rendering modes (the data model below is designed to allow adding a
  rendering mode later without reworking the sampler).
- Cursor / hover read-out, gridlines with axis labels, pan/zoom.

---

## Research: how the established tools do it

Three tools define the design space, and they map to three rendering strategies. We cite them so the
chosen approach is a deliberate pick, not a default.

1. **MoTeC i2** — the motorsport-analysis reference. Its time/distance graph shows speed, rpm, gear,
   brake and throttle as a stack of line graphs; brake is shown both as a continuous trace and a
   visual bar. Crucially, i2 lets each channel sit in its **own vertically-stacked sub-graph with its
   own Y scale** (a "lane" per channel or small group), and overlays multiple laps for comparison.
   This is the gold standard for *analysis* but assumes a large window and a mouse-driven cursor.
   ([MoTeC i2 highlights](https://www.motec.com.au/i2/i2highlights/),
   [Coach Dave — MoTeC in iRacing](https://coachdaveacademy.com/tutorials/how-to-use-motec-data-in-iracing/))
2. **SimHub** — the real-time-overlay reference (closest to *our* use case). Its dash-studio "graph"
   controls plot a handful of channels live over a rolling window, typically with each series mapped
   to its own min/max range and drawn in one shared box with distinct colors — i.e. **per-series
   normalization into a shared plot**, optimized for an at-a-glance live HUD rather than precise
   measurement. ([SimHub](https://www.simhubdash.com/),
   [Getting started with SimHub](https://www.overtake.gg/news/getting-started-with-simhub-to-add-race-telemetry-to-your-sim-rig.229/))
3. **Generic dual-axis line charts** — the spreadsheet approach: two channels on one plot with a
   left and right Y axis. Works for exactly 2 scales; degrades immediately past that.

**Trade-offs:**

| Strategy | Pros | Cons | Fit for a live HUD overlay |
|---|---|---|---|
| **Per-series normalize → shared 0..1 plot** | Any number of series in one compact box; one coordinate transform; trivial renderer; reads great as a HUD glance | Y pixel is not an absolute value — need a legend/labels to recover real numbers; shapes comparable, magnitudes not | **Best** — compact, scales to N series, cheap to draw |
| **Stacked lanes (MoTeC)** | Each channel keeps a true Y scale; no cross-talk | Needs lots of vertical screen space; layout grows with series count; busy on a gameplay overlay | Good for a dedicated *analysis* window, poor for a small draggable HUD widget |
| **Dual Y axis** | True values on both axes | Caps at 2 series; ambiguous which axis a line uses | Too limiting given the candidate list (8+ channels) |

### Which channels are worth overlaying together

From the candidate list and the derived-metrics catalog, the genuinely useful *groupings* are:

- **Driving trace (the classic):** throttle, brake, speed — and optionally steer. Shows corner
  technique (trail-braking, throttle pickup, mid-corner speed). This is the single most valuable
  default set.
- **Powerband:** power, torque, rpm — shows where the engine makes its grunt and confirms shift
  points (cross-reference derived-metrics §9.3).
- **Handling:** steer + lateral-g (+ later, slip-angle balance from derived-metrics §4) — shows how
  much steering input buys how much cornering load, and understeer/oversteer tendencies.
- **Drivetrain sanity:** gear (as a stepped trace) alongside rpm and speed.

These groupings inform the **default-on** series and the legend grouping, but every series is
independently toggleable so the user can build any combination.

---

## Scale-strategy recommendation (the key decision)

**Recommendation: per-series normalization into a single shared plot drawn in 0..1, with a legend
that shows each series' real current value (and its mapped range). Do NOT do multi-axis or stacked
lanes for the MVP.**

Justification:

1. **It scales to N series with one coordinate transform.** The candidate list is 8+ channels with
   wildly different units (km/h, rpm, hp, lb-ft, 0..1 fractions, g, gear index). A dual axis caps at
   2; lanes eat vertical space we don't have on a draggable HUD widget. Normalization handles all of
   them uniformly — this is exactly what SimHub's live graphs do for the same reason.
2. **The HUD job is shape, not measurement.** On a live overlay you read *relationships and timing*
   ("brake released as throttle comes in", "rpm flat-lined before the upshift"), which normalized
   traces convey perfectly. Absolute numbers are recovered from the legend and the existing gauge
   widgets, so we lose nothing.
3. **It's the cheapest thing to render and reason about** (one `(value→[0..1])→pixel` map per
   series), which matters for the 60 Hz redraw budget.

### How normalization works (per series)

Each series declares a **fixed, sensible display range** `[Min, Max]` rather than auto-scaling to the
running min/max. Fixed ranges are strongly preferred for a live overlay because auto-ranging makes a
trace "breathe" (the same physical value jumps around as the window's extremes change), which is
visually confusing and makes two series incomparable frame-to-frame.

```
norm(v) = clamp01((v - Min) / (Max - Min))
yPixel  = plotTop + (1 - norm(v)) * plotHeight     // invert: 1.0 at top
```

Sensible fixed ranges (defaults; all overridable later):

| Series | Source (TelemetryReadout) | Display range | Notes |
|---|---|---|---|
| Throttle | `ThrottleFraction` | 0 .. 1 | already normalized |
| Brake | `BrakeFraction` | 0 .. 1 | already normalized |
| Clutch | `ClutchFraction` | 0 .. 1 | off by default |
| Steer | `SteerFraction` | -1 .. 1 | centered; legend shows signed |
| Speed | `SpeedKmh` | 0 .. 400 | km/h; unit follows global pref (later) |
| RPM | `Rpm` (vs `MaxRpm`) | use `RpmFraction` 0..1 | normalize against per-car MaxRpm, not a fixed ceiling — cleaner than a fixed rpm range |
| Gear | `Gear` | 0 .. 10 | stepped line (see rendering) |
| Power | `PowerHp` | 0 .. `peakSeen` or fixed 0..1500 | power has no per-frame ceiling; see note |
| Torque | `TorqueLbFt` | 0 .. fixed (e.g. 1200) | as power |
| Lateral g | `LatG` | -2 .. 2 | from derived-metrics §1.2 |
| Long g | `LongG` | -2 .. 2 | accel/brake g |

Notes on the awkward ones:
- **RPM:** normalize as `RpmFraction` (already `CurrentEngineRpm / EngineMaxRpm`, clamped 0..1 in
  `TelemetryReadout`). This auto-adapts per car without a fixed ceiling and is the natural 0..1.
- **Power / Torque:** these have no fixed maximum across cars. Two acceptable options: (a) a generous
  fixed range (0..1500 hp / 0..1200 lb-ft) — simplest and stable; or (b) a **per-series rolling
  session-max** captured into the sampler (the only place auto-range is justified, because power has
  no natural ceiling). **Recommend fixed ranges for MVP** for stability; expose session-max as a
  later option. Whichever is used, the legend always prints the real hp/lb-ft so the absolute is
  never lost.
- **Gear:** render as a step function (hold-then-jump), not a smoothed line; normalize against a
  fixed 0..10 so the steps are visible.

The legend (rendered beside/under the plot) shows, per visible series: a color swatch, the series
name, and the **current real value** formatted in its real unit (e.g. `Speed 182 km/h`,
`Power 412 hp`). This is what makes normalized traces honest.

---

## Data model

### New: `ChartHistory` (ring-buffer sampler) in `Fh6.Telemetry.Core`

This complements — does not replace — the stateless `TelemetryReadout`. `TelemetryReadout` is a pure
per-frame projection; the chart needs *history*, so we add a small stateful sampler in Core (mirrors
the "rolling-state layer" the derived-metrics doc anticipates in its Open-decision #2). Keeping it in
Core makes the sampling/normalization math unit-testable without WPF.

Design:

```csharp
namespace Fh6.Telemetry.Core;

/// <summary>One captured frame for the chart: timestamp + the raw channel values we may plot.</summary>
public readonly record struct ChartSample(
    double TimeSeconds,        // monotonic seconds since history start (from TimestampMs deltas)
    float Throttle, float Brake, float Clutch, float Steer,
    float SpeedKmh, float RpmFraction, int Gear,
    float PowerHp, float TorqueLbFt, float LatG, float LongG);

public sealed class ChartHistory
{
    private readonly ChartSample[] _buf;   // fixed-capacity ring buffer
    private int _head;                     // index of next write
    private int _count;
    private double _t0Ms;                  // TimestampMs of first sample (for delta clock)
    private bool _hasT0;

    public ChartHistory(int capacity) { _buf = new ChartSample[capacity]; }

    public int Count => _count;
    public int Capacity => _buf.Length;

    /// <summary>Append the latest readout. Uses TimestampMs deltas for the time axis
    /// (never assumes 60 Hz — packets drop), per the derived-metrics dt convention.</summary>
    public void Add(in TelemetryReadout r, uint timestampMs) { /* compute TimeSeconds, write ring slot */ }

    /// <summary>Reset on !IsRaceOn / teleport / rewind. Clears the buffer and the time origin.</summary>
    public void Reset() { _head = 0; _count = 0; _hasT0 = false; }

    /// <summary>Enumerate samples newer than (latestTime - windowSeconds), oldest-first,
    /// for the renderer. Returns count written into the caller's span (decimation-friendly).</summary>
    public int CopyWindow(double windowSeconds, Span<ChartSample> dest);
}
```

Key decisions:
- **Fixed-capacity ring buffer**, sized for the *largest* supported window. At ~60 Hz and a 120 s max
  window that's `60 * 120 = 7200` samples — trivially small (`ChartSample` is ~48 bytes →
  ~350 KB). Capacity = `maxWindowSeconds * expectedHz` with headroom (e.g. 8000). No reallocation,
  no GC churn on the hot path (`Add` overwrites in place).
- **Time axis from `TimestampMs` deltas**, not frame count — packets drop and the rate isn't
  guaranteed (derived-metrics global convention). `TimeSeconds` is `(TimestampMs - t0Ms)/1000`.
- **Reset on `!IsRaceOn`** is mandatory (so a returned-to-menu / new-session doesn't draw a garbage
  trace bridging the gap). Also reset on a large `TimestampMs` backward jump (rewind) — optional,
  cheap guard.
- **Who owns it & who feeds it:** the sampler is fed once per packet. Cleanest integration: the
  `TelemetryPump` already builds a `TelemetryReadout` per packet on the background thread and posts
  the latest to the VM. We add the `ChartHistory.Add(...)` call in the VM's `Update(in readout)` (UI
  thread, packet rate) so history stays in lock-step with the displayed values and is single-threaded
  w.r.t. the renderer. The VM checks `readout.IsRaceOn` and calls `Reset()` on the falling edge.
  - Note: `TelemetryReadout` does not currently carry `TimestampMs`; either add it to the readout
    (one field) or pass `packet.TimestampMs` alongside the readout into `Update`. **Recommend adding
    `uint TimestampMs` to `TelemetryReadout`** — small, generally useful, and avoids threading the raw
    packet through. (This is the one Core change beyond the new files.)

### Series definitions (renderer-side catalog)

The set of plottable series is a static catalog. Each entry is pure data (name, color, range, value
selector, step-vs-line). Living in the Overlay project (it references WPF colors):

```csharp
namespace Fh6.Telemetry.Overlay.Widgets;

public enum ChartSeriesId
{ Throttle, Brake, Clutch, Steer, Speed, Rpm, Gear, Power, Torque, LatG, LongG }

public sealed record ChartSeriesDef(
    ChartSeriesId Id,
    string Name,
    Color Color,
    float Min, float Max,
    bool Stepped,                              // gear renders stepped
    Func<ChartSample, float> Select,           // raw value for normalization + min/max
    Func<float, string> FormatValue);          // legend text, e.g. v => $"{v:F0} km/h"
```

Distinct default colors (high-contrast on a dark HUD): Throttle = green `#3FBF3F`, Brake = red
`#E05A5A`, Speed = cyan `#3AC5E0`, Rpm = amber `#E0C93A`, Steer = violet `#B07AE0`, Gear = grey
`#9AA0A6`, Power = orange `#E08A3A`, Torque = teal `#3AE0A0`, LatG = magenta, LongG = blue.
(Reuse the shift-light palette feel already in `TelemetryViewModel` for consistency.)

**Default-on series:** Throttle, Brake, Speed (the classic driving trace). Everything else default-off.

---

## Configurable time window & per-series visibility (config additions)

The widget needs its own config block. The existing `WidgetConfig` (Visible / X / Y / Scale / Accent
/ Surface) is reused for placement and is enough for position; the chart-specific settings (window,
series toggles) go in a new optional section on `OverlayConfig` so old config files still load.

```csharp
// OverlayConfig.cs additions
public ChartConfig Chart { get; set; } = new();   // null-safe default; old files get defaults

public sealed class ChartConfig
{
    /// <summary>Visible time window in seconds. Clamped to one of the supported steps on load.</summary>
    public double WindowSeconds { get; set; } = 60;

    /// <summary>Per-series enabled flags, keyed by ChartSeriesId.ToString().
    /// Absent key => use the catalog default-on for that series.</summary>
    public Dictionary<string, bool> Series { get; set; } = new();
}
```

- **`WidgetId` gains a `Chart` member.** `OverlayConfig.Normalize` already seeds every `WidgetId`
  into `Widgets`, so adding the enum value automatically gives the chart a `WidgetConfig` entry, a
  settings row, and a `LayoutSeeds` slot — no special-casing. (LayoutSeeds must add a `Chart` seed
  position to each of the three presets, and the chart's larger footprint means its seed X/Y should
  place it where there's room; default it `Visible: false` in the seeds so it's opt-in.)
- **Supported windows:** a small fixed set (`30, 60, 120` s) shown as a combo box; `WindowSeconds`
  is clamped to the nearest supported value in a `ChartConfig.Normalize()` call (mirrors the existing
  clamp pattern in `OverlayConfig.Normalize`). The ring buffer is always sized for the max (120 s);
  shrinking the window just changes how much of the buffer the renderer reads.
- **Series toggles:** `Series[id] = bool`. Missing => catalog default. Read into the renderer when
  the widget builds / when settings are applied.

`ConfigStore` needs no structural change (it relies on JSON defaulting); add a `ChartConfig.Normalize`
call from `OverlayConfig.Normalize` so the window value and any new series default sanely.

---

## WPF rendering approach

**Recommendation: roll our own lightweight renderer — one `Path` + `StreamGeometry` per visible
series on a `Canvas`, rebuilt on the existing render tick. Do NOT add a charting NuGet
(LiveCharts / OxyPlot / SciChart) for the MVP.**

### Why no charting dependency

- The widget needs *one* thing these libraries do not specialize in for free: cheap, normalized,
  fixed-window live scrolling integrated with our existing `CompositionTarget.Rendering` loop and our
  customization/drag system. The libraries bring axis engines, legends, hit-testing, themes, and
  (for the fast ones) licensing — all overhead we don't need for a small HUD trace.
- Our data volume is tiny (≤ ~7200 points total across all series, and far fewer after decimation),
  so the performance argument for a heavyweight lib doesn't apply. SciChart's own real-time guidance
  is about millions of points; we're three orders of magnitude below that.
- Keeping it dependency-free matches the project's existing hand-rolled widgets (e.g. `GForceWidget`
  draws its own canvas with `Canvas.SetLeft/Top`) and avoids a XAML-theming/packaging mismatch with
  the transparent click-through overlay.

If the user later wants full analysis (cursors, multi-lap overlay, axis labels), revisit OxyPlot
(free, MIT) as the lowest-friction upgrade — but that's a different, heavier widget.

### How it renders

Mirror the existing `GForceWidget` pattern (a `Canvas` named e.g. `Plot`, redraw on data change),
but driven by the per-frame tick instead of a DP change:

1. **Geometry per series.** For each *visible* series, hold one `System.Windows.Shapes.Path` with a
   `Stroke` = the series color and a `StreamGeometry` `Data`. `StreamGeometry` is the documented
   lightweight choice for "many path figures, rebuilt often" and beats `PathGeometry`/`Polyline` for
   this (MS perf guidance: *"StreamGeometry is optimized for handling many PathGeometry objects and
   performs better"*; `Polyline` is a `FrameworkElement` Shape and heavier). Freeze nothing that
   changes; `StreamGeometry` is rebuilt each redraw.
   - Alternative considered: a single `DrawingVisual` with `OnRender` + `DrawingContext` (even
     lighter — no layout/event overhead). Viable and slightly faster, but a `Path` per series is
     simpler to color, toggle, and hit-test, and is plenty fast at our point counts. **Recommend
     `Path` + `StreamGeometry` for MVP; note `DrawingVisual` as the escape hatch if profiling shows
     redraw cost.**

2. **Redraw on the render tick, throttled.** The overlay already runs `CompositionTarget.Rendering`
   (~display refresh) and calls `_viewModel.Tick(dt)`. The chart subscribes to the same signal
   (either the VM raises a `ChartDirty` notification, or the widget reads `ChartHistory` on a tick).
   Redrawing the full geometry every vsync frame is acceptable at our point count, but we **throttle
   chart redraw to ~20–30 Hz** (accumulate dt, rebuild only every ~33–50 ms) — the trace doesn't
   need 60 Hz fidelity and this halves redraw cost. The gauges keep their 60 Hz easing untouched.

3. **Coordinate transform.** Per redraw: read the window via `ChartHistory.CopyWindow(window, span)`,
   compute `latestTime`; for each sample map
   `x = plotRight - (latestTime - sample.Time)/window * plotWidth` (newest at right, scrolls left),
   `y = plotTop + (1 - norm(value)) * plotHeight`. Build each series' `StreamGeometry` with one
   `BeginFigure` + `LineTo`s (`PolyLineTo`). Gear uses stepped segments (horizontal then vertical).

4. **Decimation (point reduction).** The visible plot is only a few hundred pixels wide, so drawing
   7200 points is wasteful — most map to the same column. Decimate to roughly **one point per pixel
   column**: bucket samples by target X-pixel and emit min+max (or first/last) per bucket so spikes
   survive. This caps geometry at ~`plotWidthPx * 2` points per series regardless of window length,
   making redraw cost independent of buffer size. This is the single most important perf measure and
   keeps us far inside budget.

### Performance summary

- **Max points:** buffer ≤ 8000 samples; per redraw, after decimation, ≤ ~2 × plot-width-px points
  per series (a few hundred). With ~5 visible series that's low thousands of line segments per
  redraw — well within WPF's software/GPU path for `StreamGeometry`.
- **Redraw cost:** rebuilding N small `StreamGeometry`s at 20–30 Hz is cheap; the dominant cost is
  the per-sample loop, bounded by decimation. No per-frame allocations on the hot path beyond the
  `StreamGeometry` rebuild (use a reusable sample `Span`/array for `CopyWindow`).
- **Memory:** ~350 KB ring buffer, fixed. No GC pressure from `Add` (in-place ring writes).
- **Threading:** `Add` happens on the UI thread inside `VM.Update` (posted by the pump), redraw on
  the UI thread on the tick — single-threaded, no locks needed around `ChartHistory`.

---

## Integration as `ChartWidget` in the existing system

The chart slots into the customization machinery the same way the eight current widgets do — this is
mostly "add one more `WidgetId`":

| File | Change |
|---|---|
| `Core/ChartHistory.cs`, `Core/ChartSample.cs` *(new)* | Ring-buffer sampler + sample record (unit-tested) |
| `Core/TelemetryReadout.cs` | Add `uint TimestampMs` (so the sampler gets a real clock) |
| `Widgets/WidgetId.cs` | Add `Chart` enum member |
| `Widgets/ChartSeriesId.cs`, `Widgets/ChartSeriesCatalog.cs` *(new)* | Series enum + static `ChartSeriesDef` catalog (colors/ranges/selectors/formatters) |
| `Widgets/ChartWidget.xaml(.cs)` *(new)* | `UserControl`: a `Canvas` (`Plot`) + a legend panel; one `Path`/`StreamGeometry` per visible series; redraw method like `GForceWidget.Redraw()` but tick-driven; reads `ChartHistory` + `ChartConfig` |
| `Layouts/FreeLayout.xaml.cs` | Instantiate `ChartWidget` in the `_widgets` dictionary (`[WidgetId.Chart] = new ChartWidget()`); wire its data source (give it the `ChartHistory` + the tick signal, akin to how `GForceWidget` gets DP bindings) |
| `Layouts/LayoutSeeds.cs` | Add a `Chart` seed (X/Y/Scale, `Visible:false`) to all three presets |
| `Settings/OverlayConfig.cs` | Add `ChartConfig Chart`; call `Chart.Normalize()` from `Normalize()` |
| `Settings/SettingsWindow.xaml(.cs)` | Add a "Chart" section: time-window combo + a checkbox per `ChartSeriesId`; write back to `cfg.Chart` in `OnApply` |
| `ViewModels/TelemetryViewModel.cs` | Own the `ChartHistory`; in `Update` call `History.Add(readout, readout.TimestampMs)` and `Reset()` on the `IsRaceOn` falling edge; expose history (or a `ChartDirty` event) to the widget |

Edit-mode drag, scale, visibility, color overrides, persistence, and the F8/F9/F10 hotkeys all work
automatically because `FreeLayout`/`OverlayConfig`/`SettingsWindow` iterate `WidgetId` generically —
the chart is just another entry. The only widget-specific wiring is feeding it the `ChartHistory`
and the redraw tick (parallel to how `FreeLayout` special-cases the `GForceWidget` DP bindings).

The legend is part of the `ChartWidget` UserControl (a `StackPanel` of color-swatch + name + value
`TextBlock`s, updated on the same throttled tick from the latest `ChartSample` and each series'
`FormatValue`).

---

## Testing

**Unit-testable (xUnit, in `tests/Fh6.Telemetry.Tests`, no WPF):**
- `ChartHistory`:
  - `Add` past capacity overwrites oldest (ring wrap), `Count` caps at `Capacity`.
  - `TimeSeconds` computed from `TimestampMs` deltas, not frame count; handles a non-60 Hz / dropped
    sequence and a `TimestampMs` that doesn't start at 0.
  - `Reset()` clears buffer and time origin; subsequent `Add` restarts the clock at 0.
  - `CopyWindow(window, span)` returns only samples within the window, oldest-first, count correct
    at boundaries (empty, partial, full, over-capacity).
- Normalization math (extract a pure `static float Normalize(float v, float min, float max)` helper):
  - clamps to [0,1]; correct for inverted/centered ranges (steer -1..1 → 0.5 at v=0); `min==max`
    guard (no divide-by-zero).
- Decimation helper (pure function over a sample span → reduced span): bucket count == target
  columns; min/max per bucket preserves spikes; monotonic X.
- `ChartConfig.Normalize()`: window clamps to nearest supported step; unknown series keys ignored;
  defaults applied.

Follow the existing `CustomizationModelTests` style for the config/normalize tests.

**Manual (rendering — not unit-tested):**
- Run the overlay against a replay capture (`JsonlReplaySource` already exists); confirm traces
  scroll right-to-left, newest at the right edge, distinct colors, legend shows live real values.
- Toggle series and the window in settings; confirm immediate effect and persistence after restart.
- Drag/scale/hide the widget in F8 edit mode; confirm it behaves like the other widgets.
- Drive to `!IsRaceOn` (return to menu) and back; confirm the trace resets (no line bridging the gap).

---

## Phased implementation outline

1. **Core sampler (TDD).** Add `ChartSample`, `ChartHistory`, the `Normalize`/decimation helpers, and
   `TelemetryReadout.TimestampMs`. Write the unit tests first. No UI yet. *(Self-contained, fully
   testable.)*
2. **Series catalog + config.** Add `ChartSeriesId`, `ChartSeriesCatalog`, `ChartConfig`, the
   `WidgetId.Chart` member, `LayoutSeeds` entries, and `ChartConfig.Normalize`. Unit-test config
   normalize. *(No rendering yet.)*
3. **VM wiring.** `TelemetryViewModel` owns `ChartHistory`; `Update` feeds it and resets on
   `!IsRaceOn`. Expose history / dirty signal. *(Verifiable via a small VM test that pumps readouts
   and asserts history state.)*
4. **`ChartWidget` rendering.** Build the `UserControl` (Canvas + legend), the `Path`/`StreamGeometry`
   per series, the tick-driven throttled redraw with decimation, the coordinate transform, stepped
   gear. Register it in `FreeLayout`. Manual-verify against a replay.
5. **Settings UI.** Add the Chart section (window combo + per-series checkboxes) to `SettingsWindow`;
   wire read/apply/persist. Manual-verify toggles + persistence.
6. **Polish.** Default colors, legend formatting, gear stepping, decimation tuning, redraw-rate
   tuning; confirm 60 Hz gauges are unaffected. Optional: gridlines, a "now" marker.

Each phase is independently reviewable; phases 1–3 are pure/back-end and gated by tests, phases 4–6
are UI and gated by manual replay verification.

---

## Open questions for the user

1. **Default series set:** confirm Throttle + Brake + Speed as the default-on trace (recommended), or
   prefer a different default (e.g. add Steer, or default to the powerband Power/Torque/Rpm)?
2. **Power/Torque scaling:** fixed generous ranges (0..1500 hp / 0..1200 lb-ft, stable — recommended)
   vs a per-session rolling max (adapts per car but the trace re-scales mid-session)?
3. **Time-window choices:** is `30 / 60 / 120 s` the right menu, or do you want a continuous slider /
   a longer max (which only costs ring-buffer memory)?
4. **One chart or several?** MVP is a single `ChartWidget`. Do you foresee wanting multiple
   independent charts (e.g. one powerband + one driving trace) soon enough that the config should key
   charts by an instance id now rather than later?
5. **Units:** chart Y-ranges and legend use km/h and hp/lb-ft by default. Should the chart follow a
   future global units setting (derived-metrics Open-decision #1), or have its own unit toggles?
6. **Lap-delta as a series:** out of MVP (needs the heavy reference-trace machinery). Confirm that's
   acceptable, or is live lap-delta the actual priority (which would reorder the roadmap toward the
   `HH` work in derived-metrics §6.2)?
7. **`TelemetryReadout.TimestampMs`:** OK to add this one field to the readout (recommended), vs
   threading the raw `TelemetryPacket.TimestampMs` separately into the VM?

---

## Sources

- MoTeC i2 multi-channel / overlay analysis:
  [MoTeC i2 highlights](https://www.motec.com.au/i2/i2highlights/);
  [Coach Dave — MoTeC data in iRacing](https://coachdaveacademy.com/tutorials/how-to-use-motec-data-in-iracing/);
  [Coach Dave — MoTeC in ACC](https://coachdaveacademy.com/tutorials/how-to-use-motec-data-in-assetto-corsa-competizione/).
- SimHub live dashboards / graphs:
  [SimHub](https://www.simhubdash.com/);
  [Getting started with SimHub (OverTake)](https://www.overtake.gg/news/getting-started-with-simhub-to-add-race-telemetry-to-your-sim-rig.229/).
- WPF live-chart rendering & performance:
  [MS Learn — Optimizing Performance: 2D Graphics and Imaging](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-2d-graphics-and-imaging)
  (Drawing vs Shape, StreamGeometry vs PathGeometry, DrawingVisual);
  [SciChart — Creating a real-time WPF chart](https://www.scichart.com/creating-a-real-time-wpf-chart/) and
  [WPF FIFO scrolling charts](https://www.scichart.com/example/wpf-chart/wpf-chart-example-fifo-scrolling-charts/)
  (ring-buffer/FIFO pattern; their scale is millions of points — confirms we don't need a heavy lib).
- Candidate series, dt convention, `IsRaceOn` reset, rolling-state layer:
  `docs/superpowers/specs/2026-06-13-derived-metrics-research.md`.

(Web searches conducted 2026-06-13. SimHub's exact internal chart implementation is not publicly
documented; the "per-series normalized shared plot" characterization is inferred from its live-dash
graph behavior and is consistent with the design rationale above.)
