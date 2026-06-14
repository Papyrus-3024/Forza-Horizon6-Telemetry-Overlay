# UI/UX Library Research for Overlay V2 (eye-catching, animated)

Date: 2026-06-13
Status: Research only — no code changed.

## Purpose

Decide which third-party WPF libraries (if any) to adopt for an eye-catching, animated
telemetry overlay (V2) so we stop reinventing charts, gauges, effects, and animation by
hand — **without** breaking the overlay's defining constraint.

## The hard constraint that filters everything

The overlay (`OverlayWindow.xaml`) is a **transparent, click-through, always-on-top,
full-screen layered window**:

```
WindowStyle="None"  AllowsTransparency="True"  Background="Transparent"  Topmost="True"
```

Plus a `WS_EX_TRANSPARENT` click-through extended style toggled at runtime
(`ClickThrough.SetClickThrough`). Rendering today is hand-rolled: WPF `Canvas` + `Shape`s +
`StreamGeometry` (see `ChartWidget.xaml.cs`), animated off `CompositionTarget.Rendering` in
`OverlayWindow.xaml.cs` and `ChartWidget`.

### Why this filters most libraries: the WPF "airspace" problem

`AllowsTransparency=true` makes this a **layered window**. Windows only supports
per-window transparency for *top-level* windows; it does **not** apply to *child* HWNDs.
So any control that renders through a **child HWND or a shared DirectX surface** misbehaves:

- **Child HWND** (OpenGL via `GLWpfControl`/`WindowsFormsHost`, or `HwndHost`): draws
  **opaque, on top of** WPF content, ignores WPF z-order/clipping, **cannot be transparent
  over the desktop**, and breaks click-through.
- **`D3DImage`** (DirectX shared surface): avoids airspace in *normal* windows but is
  documented to go **fully invisible** under `AllowsTransparency=true`.

The only overlay-safe rendering paths are:

1. **Pure WPF visuals** (Shapes / `DrawingContext` / `StreamGeometry`) — what we do now.
2. **A bitmap blitted into the WPF visual tree** — e.g. SkiaSharp's **`SKElement`**, which
   renders to a `Pbgra32` (premultiplied-alpha) `WriteableBitmap` and composites via
   `DrawingContext.DrawImage`. No child HWND, full alpha → **safe on a layered window**.
   (Confirmed in `SKElement.cs`: `new WriteableBitmap(..., PixelFormats.Pbgra32, null)` +
   `OnRender`/`DrawImage`.)

**Rule of thumb for this project:** take *control styles / behaviors / animations / a
software-rasterized drawing surface*; **reject** anything built around a custom `Window`
subclass with its own chrome/DWM backdrop, or anything using a child HWND / `D3DImage`
for rendering.

---

## Charting libraries

Live scrolling traces (RPM/speed/etc.) updating ~28–60 Hz, with fills, variable stroke
weights, transparent background.

| Library | License | Maintained? | Overlay-safe backend? | Live scrolling fit | Recommendation |
|---|---|---|---|---|---|
| **ScottPlot 5 (WPF)** | MIT | Yes — `ScottPlot.WPF 5.1.58`, 2026-03 | **Yes** — `WpfPlot` uses `SKElement` (CPU bitmap). Avoid `WpfPlotGL` (OpenGL child surface). | **Purpose-built** — `DataStreamer`/`DataLogger` plottables for fixed-window scrolling signals (ECG-style) | **Best fit if adopting a chart lib** |
| **LiveCharts2** | MIT (+ optional paid tier) | Yes — `2.0.4`, 2026-05; dev builds 2026-06 | **Yes** — SkiaSharp via `SKElement` (CPU bitmap) | Good; disable default animations at high Hz; must clear *all* background paints (chart + axis fills) for true transparency | Strong alternative; also has gauges |
| **OxyPlot (WPF)** | MIT | **Slow** — `2.2.0`, 2024-09; infrequent releases | Yes (pure-WPF renderer, or optional Skia renderer) | **Weak** — default Shapes/Canvas renderer materializes many visuals, poor at high point counts/update rates; good perf only via the Skia renderer | Mediocre; skip |
| **SciChart WPF** | **Commercial only** — no free WPF tier; ~$1.5k+/dev/yr; 30-day trial | Yes | **No by default** — `D3DImage` goes invisible under `AllowsTransparency`. Needs `Direct3D10RenderSurface.UseAlternativeFillSource=true` workaround | Excellent | Overkill + cost + airspace caveat; skip |
| **InteractiveDataDisplay.WPF** | MIT | **Abandoned** — `1.0.0` 2017, repo archived, **.NET Framework only** | Yes (native WPF) | n/a | **Disqualified** (no .NET 8) |

Notes:
- Both ScottPlot's `WpfPlot` and LiveCharts2 ride the **same overlay-safe `SKElement`
  CPU-bitmap path** — that's why they're the two viable options.
- **Do not use `WpfPlotGL` / `SKGLElement`** anywhere — that's the airspace-prone OpenGL path.
- For click-through over a chart, set backgrounds to **`{x:Null}`** rather than
  `Transparent` where you want mouse pass-through (a `Transparent` brush still participates
  in WPF hit-testing), in addition to the window-level `WS_EX_TRANSPARENT`.

---

## Gauges / dials

| Option | License | Free? | Overlay-safe? | Recommendation |
|---|---|---|---|---|
| **Hand-roll (SkiaSharp `SKElement` or WPF `Path`/arc)** | — | — | Yes | **Recommended** — full visual control, native transparency, no licensing/bloat |
| **LiveCharts2 gauges** | MIT | Yes | Yes (SkiaSharp/`SKElement`) | Fine for a quick prototype/reference; replace with hand-rolled for final look |
| **Syncfusion `SfCircularGauge` (WPF)** | Commercial; Community License free **only if eligible** (<$1M rev, ≤5 devs, ≤10 employees) | Conditional | Native WPF; renders, but heavy + theming friction | Skip (eligibility strings, bloat) |
| **SciChart / DevExpress / Telerik gauges** | Commercial | No | varies | Skip |

**Verdict:** for bespoke racing dials (redline zones, glow, tapered needles, custom type),
**hand-roll the gauge** — packaged gauge controls assume opaque chart backgrounds and fight
custom styling. Do it with **SkiaSharp** for the gradients/glow, or keep WPF `Path`/arc for
simple dials.

---

## Custom 2D drawing / effects / performance — SkiaSharp

| Aspect | Finding |
|---|---|
| License | **MIT** (.NET binding to Google Skia, under mono/.NET Foundation) |
| Maintained? | Yes — stable `3.119.x` (2025), NativeAssets updated into early 2026 |
| `SKElement` (software/CPU) | **Overlay-safe.** Pure WPF `FrameworkElement`, no child HWND; renders to `Pbgra32` `WriteableBitmap`; `canvas.Clear(SKColors.Transparent)` composites correctly over the desktop |
| `SKGLElement` (OpenGL/GPU) | **Unsafe — do not use.** Hosts a child HWND → airspace; renders opaque on top, no transparency over desktop. A community POC explicitly reports "AllowsTransparency enabled shows no controls" |
| Perf at ~60 fps | Viable. Small/medium `SKElement`s hold 60 fps; **full-screen blits can drop to ~30 fps**. Blit cost scales with pixel area |

**Performance tips (important):**
- **Size each `SKElement` to its widget, not the whole screen** — use several small surfaces
  (one per gauge/minimap) instead of one full-screen element. Biggest single win.
- Only repaint when telemetry advances; throttle to the data rate (we already gate
  `ChartWidget` at ~28 Hz and skip redraw when the latest timestamp is unchanged).
- Drive redraws off `CompositionTarget.Rendering`; **reuse** Skia paints/shaders/filters
  across frames instead of recreating them.

**What SkiaSharp makes easy that hand-rolled WPF shapes do not:**
- Gradients (`SKShader.CreateSweepGradient` is ideal for gauge arcs).
- **Glow/bloom** via `SKImageFilter.CreateBlur` / `SKMaskFilter.CreateBlur` — cheap; painful
  and heavyweight with WPF `BlurEffect`/`DropShadowEffect`.
- Antialiased arcs/fills, tapered caps, tick marks, thousands of primitives in one pass
  (particles) far cheaper than thousands of WPF `Shape` elements.
- SkSL runtime shaders for effects impossible in plain WPF.

---

## Styling / animation kits

| Library | License | Maintained? | Overlay-safe? | Recommendation |
|---|---|---|---|---|
| **Microsoft.Xaml.Behaviors.Wpf** | MIT | Yes — `1.1.142`, 2026-03 | **Yes** — pure attached behaviors/triggers; no Window class, no chrome, no DWM | **Adopt** — declarative trigger/glue for threshold flashes |
| **Built-in WPF Storyboard + EasingFunctions** | (in-box) | n/a | **Yes** — operates on properties/brushes, never the window | **Adopt** — the animation engine itself |
| **WPF-UI (lepoco)** | MIT | Yes (active) | **No** — `FluentWindow` + DWM Mica/Acrylic assume a normal composed window; open issue #925: transparent `FluentWindow` makes content invisible | **Skip** (cherry-pick a single style's XAML at most) |
| **MahApps.Metro** | MIT | **Maintenance mode** — `2.4.11`, sparse releases | **Wrong shape** — entirely `MetroWindow`/chrome-centric | **Skip** |
| **MaterialDesignInXAML** | MIT | Yes — `5.3.x`, 2026 | Workable but heavy global theme dictionaries; elevation/shadow/ripple assume opaque surfaces, look wrong on transparency | **Skip** (overkill) |

**Key principle confirmed:** toolkits built around a custom `Window` subclass
(`FluentWindow`, `MetroWindow`) or DWM backdrops fight `AllowsTransparency` overlays. Use
**behavior/style-only** packages and WPF's native animation stack.

For shift-light flashes / color-threshold animation:
`DataTrigger` (XAML Behaviors) + `ControlStoryboardAction` to fire/stop a `Storyboard` that
animates a `SolidColorBrush.Color` / `Opacity`, with built-in easing (`CubicEase`,
`ElasticEase`, `BackEase`, `BounceEase`, `SineEase`, etc.). No third-party animation lib needed.

---

## RECOMMENDED minimal stack

Adopt a **small, coherent, all-MIT** stack and keep one rendering technology for custom visuals:

1. **SkiaSharp + `SkiaSharp.Views.WPF` (`SKElement`, software/CPU)** for new eye-catching
   custom widgets (gauges, glow, gradient fills, particles, shift lights). It's the only
   way to get gradients/glow/shaders cheaply while staying transparent-safe — and it gives
   us one drawing stack instead of two. **Use `SKElement` only; never `SKGLElement`.**
   Size each surface to its widget.
2. **Microsoft.Xaml.Behaviors.Wpf** for declarative threshold triggers, plus **built-in WPF
   Storyboards + EasingFunctions** for animation. No third-party animation/UI-chrome kit.
3. **Charts:** prefer **keeping the existing hand-rolled `StreamGeometry` `ChartWidget`** —
   it already decimates (min/max), reuses buffers, throttles to ~28 Hz, and is overlay-safe
   and tuned. Only if we want richer scrolling-trace features (multi-pane, auto-axis,
   markers) is it worth adopting **ScottPlot 5 (`WpfPlot`)** — MIT, actively maintained, and
   purpose-built for streaming via `DataStreamer`/`DataLogger`. Either way, if we move charts
   onto Skia, **redraw the chart inside the same `SKElement` widget** to keep one stack.
4. **Gauges:** **hand-roll on SkiaSharp.** No gauge control adopted (LiveCharts2 gauges only
   as a throwaway prototype/reference if useful).

### Justification
- One drawing stack (SkiaSharp `SKElement`) for all custom visuals avoids fragmentation and
  unlocks glow/gradients/particles that are painful in raw WPF, while remaining
  transparent-safe.
- The existing chart is already good and overlay-tuned; replacing it is optional, not a
  reinvention we need to "fix."
- XAML Behaviors + native Storyboards cover animation/threshold polish with zero
  transparency risk and no heavy theme dictionaries.

### Explicit transparency / airspace warnings
- **NEVER** use `SKGLElement`, `WpfPlotGL`, `GLWpfControl`, `WindowsFormsHost`, `HwndHost`,
  or `D3DImage` in this window — all break layered-window transparency and/or click-through.
- **No `FluentWindow` / `MetroWindow` / DWM Mica/Acrylic** — incompatible with
  `AllowsTransparency=true`.
- **Full-screen `SKElement` is a perf trap** (~30 fps). Use per-widget surfaces.
- **`Transparent` vs `{x:Null}`:** a `Transparent` brush still hit-tests. Where mouse
  pass-through matters, use `{x:Null}` / `IsHitTestVisible=false` (we already set
  `IsHitTestVisible=false` on chart paths) in addition to `WS_EX_TRANSPARENT`.
- **SciChart** would need `UseAlternativeFillSource=true` to even render here, and costs
  ~$1.5k+/dev/yr — not justified.

---

## Sources

Charting:
- ScottPlot — https://www.nuget.org/packages/ScottPlot.WPF ,
  https://scottplot.net/cookbook/5/LiveData/ ,
  https://scottplot.net/cookbook/5/LiveData/DataStreamerQuickstart/ ,
  https://deepwiki.com/ScottPlot/ScottPlot/6.3-desktop-platform-controls ,
  https://github.com/ScottPlot/ScottPlot/blob/main/LICENSE
- LiveCharts2 — https://github.com/Live-Charts/LiveCharts2 ,
  https://www.nuget.org/packages/LiveChartsCore.SkiaSharpView.WPF/ ,
  https://github.com/Live-Charts/LiveCharts2/discussions/1693 , https://livecharts.dev/
- OxyPlot — https://github.com/oxyplot/oxyplot/releases ,
  https://deepwiki.com/oxyplot/oxyplot/8.1-wpf-integration
- SciChart — https://www.scichart.com/shop/ , https://www.scichart.com/licensing-scichart-wpf/ ,
  https://www.scichart.com/scichart-wpf-directx-compatibility/
- InteractiveDataDisplay — https://www.nuget.org/packages/InteractiveDataDisplay.WPF/ ,
  https://github.com/microsoft/InteractiveDataDisplay.WPF

SkiaSharp / airspace / gauges:
- https://github.com/mono/SkiaSharp/blob/main/source/SkiaSharp.Views/SkiaSharp.Views.WPF/SKElement.cs
- https://learn.microsoft.com/en-us/dotnet/api/skiasharp.views.wpf.skelement
- https://github.com/freezy/wpf-skia-opengl/blob/master/README.md
- https://github.com/mono/SkiaSharp/issues/745
- https://dwayneneed.github.io/wpf/2013/02/26/mitigating-airspace-issues-in-wpf-applications.html
- https://learn.microsoft.com/en-us/previous-versions/dotnet/netframework-3.0/aa970688(v=vs.85)
- https://github.com/mono/SkiaSharp/releases , https://mono.github.io/SkiaSharp/
- https://github.com/dotnet/wpf/issues/8045 (full-screen WriteableBitmap perf)
- https://www.syncfusion.com/products/communitylicense , https://www.syncfusion.com/wpf-controls/radial-gauge

Styling / animation:
- https://github.com/microsoft/XamlBehaviorsWpf ,
  https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Wpf ,
  https://devblogs.microsoft.com/dotnet/open-sourcing-xaml-behaviors-for-wpf/
- https://deepwiki.com/lepoco/wpfui/5.3-window-backdrop , https://github.com/lepoco/wpfui/issues/925
- https://github.com/MahApps/MahApps.Metro/releases
- https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit ,
  https://www.nuget.org/packages/MaterialDesignThemes
