# FH6 Telemetry Overlay — Per-Widget Customization Design

Date: 2026-06-13
Status: Draft for review (needs user sign-off on the open questions in the last section)
Builds on: `docs/superpowers/specs/2026-06-13-fh6-overlay-design.md` (the v1 overlay MVP)

## Goal

Today the overlay moves as **one window** and renders one of three fixed presets
(`BottomStripLayout`, `CornerPanelLayout`, `CenterDashLayout`), each a `StackPanel` of the six
widgets. The v1 design explicitly reserved a `Widgets` config section and built widgets as
self-contained `UserControl`s so this feature is "cheap to add".

This spec makes each widget:

1. **Independently positionable** — free drag per widget in F8 edit mode (today only the whole
   window drags via `OverlayWindow.DragMove()`).
2. **Customizable** — per-widget visibility (show/hide), size (scale), and color overrides
   (accent + background).
3. **Persisted** in `OverlayConfig` under the reserved `Widgets` section, restored on launch.

The three presets are kept, but reframed as **seed arrangements**: selecting a preset (F10 or
settings) writes starting X/Y/scale into the per-widget config, after which the user freely
adjusts. No new telemetry logic; `TelemetryViewModel` / Core are untouched.

## Affected files

| File | Change |
|---|---|
| `Settings/OverlayConfig.cs` | Add `Dictionary<string, WidgetConfig> Widgets`; new `WidgetConfig` record |
| `Settings/ConfigStore.cs` | No structural change (relies on JSON defaulting); add a `Normalize`/migration helper |
| `Widgets/WidgetId.cs` *(new)* | Canonical string ids for the six widgets |
| `Widgets/CustomizableWidget.cs` *(new)* | Base `UserControl` exposing `Accent`/`Surface`/`Scale` dependency properties |
| `Widgets/*.xaml(.cs)` | Re-base each widget on `CustomizableWidget`; bind `Background`/accent/`LayoutTransform` to the new DPs |
| `Layouts/FreeLayout.xaml(.cs)` *(new)* | `Canvas`-based host that places each widget by `Canvas.Left/Top` + `ScaleTransform` |
| `Layouts/LayoutSeeds.cs` *(new)* | The three presets expressed as `IReadOnlyDictionary<WidgetId, WidgetSeed>` (X/Y/scale) |
| `Layouts/*Layout.xaml` | Retired from runtime composition; kept only as the source of the seed coordinates, or deleted once `LayoutSeeds` is authored |
| `OverlayWindow.xaml(.cs)` | Host `FreeLayout`; per-widget drag in edit mode; right-click context menu; whole-window move fallback |
| `Settings/SettingsWindow.xaml(.cs)` | Add a "Widgets" section (per-widget visibility/scale/color) |
| `Settings/WidgetCustomizeControl.xaml(.cs)` *(new, optional)* | Reusable row of controls for one widget, used in settings and/or context menu |

---

## 1. Data model

### `WidgetConfig` record

Per-widget settings live in a new serializable type. All members are nullable / defaulted so a
missing widget or missing field falls back to the seed/theme default — this is what keeps old
config files loading unchanged.

```csharp
namespace Fh6.Telemetry.Overlay.Settings;

public sealed class WidgetConfig
{
    public bool Visible { get; set; } = true;

    // Absolute position inside the overlay Canvas, in DIPs. Null => use seed position.
    public double? X { get; set; }
    public double? Y { get; set; }

    // Uniform scale applied via ScaleTransform. 1.0 = design size. Clamped 0.5..2.5 on apply.
    public double Scale { get; set; } = 1.0;

    // Color overrides as "#AARRGGBB" strings. Null => widget's theme default.
    public string? Accent { get; set; }   // value text / progress foreground accent
    public string? Surface { get; set; }  // widget background (Border.Background)
}
```

Colors are stored as **hex strings**, not `Color`/`Brush`, so JSON stays human-editable and the
existing `System.Text.Json` setup needs no custom converter. Parsing/formatting goes through a
single `ColorOverrides` helper (`ColorConverter.ConvertFromString` on load; `ToString()` on
save) so a bad hex value degrades to "use default" rather than throwing.

### Extending `OverlayConfig`

```csharp
public sealed class OverlayConfig
{
    public int Port { get; set; } = 20440;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public OverlayLayout Layout { get; set; } = OverlayLayout.BottomStrip;
    public double Opacity { get; set; } = 0.9;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    // NEW: keyed by WidgetId.Key (stable string). Absent keys => not yet customized.
    public Dictionary<string, WidgetConfig> Widgets { get; set; } = new();
}
```

`Layout` is **retained** and now means "which seed arrangement is active / was last applied".
It still drives F10 cycling and the settings dropdown.

### Widget ids

A stable string key per widget (used as the `Widgets` dictionary key, in the context menu, and
to look up seeds). Strings (not the type names) so renaming a class never silently orphans saved
config.

```csharp
public enum WidgetId { Gear, Speed, RpmShift, PedalsSteer, Boost, LapTiming }
// WidgetId.ToString() is the JSON key: "Gear", "Speed", ...
```

### JSON shape

```jsonc
{
  "Port": 20440,
  "ListenAddress": "0.0.0.0",
  "Layout": "BottomStrip",
  "Opacity": 0.9,
  "WindowLeft": 40,
  "WindowTop": 40,
  "Widgets": {
    "Gear":        { "Visible": true,  "X": 6,   "Y": 24, "Scale": 1.0 },
    "Speed":       { "Visible": true,  "X": 64,  "Y": 24, "Scale": 1.0 },
    "RpmShift":    { "Visible": true,  "X": 130, "Y": 24, "Scale": 1.0, "Accent": "#FF5AD15A" },
    "PedalsSteer": { "Visible": true,  "X": 290, "Y": 24, "Scale": 1.0 },
    "Boost":       { "Visible": false, "X": 360, "Y": 24, "Scale": 1.0 },
    "LapTiming":   { "Visible": true,  "X": 420, "Y": 24, "Scale": 1.2, "Surface": "#C0101418" }
  }
}
```

### Migration / defaults

No version field is needed because every new member defaults safely:

- **Old config (no `Widgets`)** → `Widgets` deserializes to an empty dictionary. On first load,
  `OverlayConfig.Normalize()` seeds it from the active `Layout` (see §2) and saves. Result:
  identical visual layout to today, now editable.
- **Partial `Widgets`** (some keys missing) → missing keys are filled from the active seed at
  load; present keys win.
- **Missing field within a `WidgetConfig`** → `Scale` defaults to 1.0, `Visible` to true, colors
  to null (theme default), `X/Y` null → seed position.
- **Corrupt file** → existing `ConfigStore.Load` already returns `new OverlayConfig()`, then
  `Normalize()` seeds defaults. The next save rewrites a clean file (matches v1 behavior).

`ConfigStore` itself is unchanged structurally; add a `OverlayConfig.Normalize(OverlayLayout)`
method called once after `Load` in `App` startup (and after a preset is applied).

---

## 2. Free-positioning architecture

### From `StackPanel` presets to one `Canvas`

Replace runtime use of the three `*Layout` `UserControl`s with a single `FreeLayout` whose root
is a `Canvas`. `FreeLayout` instantiates each of the six widgets once, keeps a
`Dictionary<WidgetId, FrameworkElement>` of them, and positions each via `Canvas.SetLeft/SetTop`
plus a `ScaleTransform` from its `WidgetConfig`.

```
OverlayWindow
└─ Border Root  (edit-mode border; #01000000 hit-test background)
   └─ Canvas (FreeLayout)
      ├─ GearWidget        Canvas.Left/Top from cfg, LayoutTransform=Scale
      ├─ SpeedWidget       ...
      ├─ RpmShiftWidget    ...
      ├─ PedalsSteerWidget ...
      ├─ BoostWidget       (Visibility=Collapsed if !Visible)
      └─ LapTimingWidget   ...
```

`OverlayWindow` currently uses `SizeToContent="WidthAndHeight"`. A free `Canvas` has no implicit
size, so the window must size to the screen working area (or a configured canvas size) and stay
transparent/click-through. **Change:** drop `SizeToContent`, set the window to the primary
screen working-area bounds (`SystemParameters.WorkArea`) with `Left/Top = 0`. The existing
`#01000000` near-transparent `Root` background already provides hit-testing only where widgets
are; with click-through on, input still passes to the game everywhere.

> The status `TextBlock` (bound to `Status`) moves into the `Canvas` too (top-left, fixed), or
> stays in the window chrome above the canvas. Recommend keeping it pinned top-left, not
> user-movable, to avoid one more thing to lay out.

### Presets become seed arrangements

`LayoutSeeds` holds the three arrangements as data. Each maps `WidgetId → WidgetSeed { X, Y,
Scale, Visible }`. The X/Y values are authored by reading the current `*Layout.xaml` visual
arrangement (translate the StackPanel order/margins into absolute coordinates) — a one-time
manual conversion. Example:

```csharp
public readonly record struct WidgetSeed(double X, double Y, double Scale = 1.0, bool Visible = true);

public static class LayoutSeeds
{
    public static IReadOnlyDictionary<WidgetId, WidgetSeed> For(OverlayLayout layout) => layout switch
    {
        OverlayLayout.CornerPanel => CornerPanel,
        OverlayLayout.CenterDash  => CenterDash,
        _                          => BottomStrip,
    };
    // BottomStrip/CornerPanel/CenterDash: static dictionaries of seeds.
}
```

**Applying a preset** (F10 cycle or settings dropdown) calls
`config.ApplySeed(LayoutSeeds.For(layout))`, which **overwrites** X/Y/Scale/Visible for all six
widgets from the seed (it does *not* clear color overrides). This is the documented behavior:
choosing a preset re-arranges everything; manual tweaks after that are preserved until the next
preset apply. (See open question on whether preset-apply should also reset colors.)

The `OverlayLayout` enum and `Layout` config field stay; `BottomStrip` remains the default and
the first-run seed.

### Edit-mode per-widget drag

Today (`OverlayWindow.xaml.cs`): F8 toggles `_editMode`, flips click-through, draws a yellow
border, and `MouseLeftButtonDown` → `DragMove()` moves the whole window.

New edit-mode behavior, layered on top:

- **Hit-testing / which widget**: in `FreeLayout`, handle `PreviewMouseLeftButtonDown` on the
  `Canvas`; walk up from `e.OriginalSource` via `VisualTreeHelper` to find the owning widget
  `UserControl` (each widget is a direct `Canvas` child, so match against the children set).
- **Drag handle**: in edit mode each widget shows a thin handle/outline. Simplest: an
  edit-mode-only `Border` overlay (a sibling `Adorner` is the "correct" WPF approach; an overlay
  border is simpler and adequate). Dragging anywhere on the widget moves it (mirrors the current
  whole-window feel) — a dedicated grip strip is optional polish.
- **Drag math**: on mouse-down capture the pointer and the offset between cursor and the
  widget's `Canvas.Left/Top`. On `MouseMove` set `Canvas.SetLeft/Top = cursorCanvasPos - offset`,
  clamped to `[0, canvasW - widgetW]` × `[0, canvasH - widgetH]`. On mouse-up release capture and
  write back to `WidgetConfig.X/Y`. **Snapping** (optional MVP+): snap to an 8px grid and to
  other widgets' edges when within ~6px.
- **Whole-window move**: with per-widget drag claiming left-button drags on widgets, the
  whole-window `DragMove()` is reached only when the drag starts on empty canvas (not on a
  widget). Keep it as the fallback — drag empty space to move the whole overlay; drag a widget to
  move just it. (Alternative: drop whole-window move entirely now that the window fills the
  screen — see open question.)
- **Persistence**: leaving edit mode (F8) writes all changed `WidgetConfig.X/Y` plus the
  existing `WindowLeft/Top`, then `ConfigStore.Save` — same place the code saves today.
- **Click-through**: unchanged. `ClickThrough.SetClickThrough(_hwnd, !_editMode)` still gates all
  input; per-widget drag only runs while `_editMode` is true.

### Visibility

`WidgetConfig.Visible == false` sets the child's `Visibility = Collapsed` in `FreeLayout`. In
edit mode, hidden widgets render as a dim ghosted placeholder so they can be re-enabled by
right-click without reopening settings.

---

## 3. Customization UI

Two complementary surfaces; **MVP ships the settings section**, the context menu is a fast-path
that can follow.

### A. Settings window section (primary)

Extend `SettingsWindow.xaml` — keep the existing `StackPanel`/`Margin="14"` style, the
Cancel/Apply buttons, and the `OnApply` write-back pattern. Below the existing Opacity slider,
add a **Widgets** group: an `ItemsControl`/`ListView` of six rows, one per `WidgetId`, each a
`WidgetCustomizeControl` (new `UserControl`) with:

- a `CheckBox` (Visible),
- a `Slider` (Scale 0.5–2.5, snapped to 0.05 — mirrors the Opacity slider),
- two small color pickers for Accent and Surface — MVP uses a `ComboBox`/swatch list of preset
  colors (no third-party color dialog dependency) plus a "Default" entry that clears the override.

`OnApply` writes each row back into `_config.Widgets[id]` and sets `DialogResult = true`. The
overlay already re-applies config after settings close (`ApplyLayout` today) — replace that with
`FreeLayout.ApplyConfig(_config)` so scale/visibility/color/position refresh live.

### B. Per-widget context menu in edit mode (fast-path, MVP+)

In edit mode, right-click a widget opens a `ContextMenu`:

- Show / Hide (toggles `Visible`)
- Scale ▸ 75% / 100% / 125% / 150%
- Accent ▸ swatches, Surface ▸ swatches, Reset colors
- "More…" → opens `SettingsWindow` scrolled to that widget

This reuses the same write-back into `WidgetConfig` and the same `FreeLayout.ApplyConfig` refresh,
so the two UIs never diverge. Position is adjusted by dragging, not the menu.

---

## 4. Styling hooks

Goal: a future "polish pass" restyles widgets without fighting per-widget overrides. The
override is an **opt-in** layered on top of the default theme.

### Per-widget dependency properties

Introduce `CustomizableWidget : UserControl` with three dependency properties:

```csharp
public Brush Accent  { get; set; }  // default from theme; value text / progress foreground
public Brush Surface { get; set; }  // default from theme; Border background
public double Scale   { get; set; } = 1.0;
```

Each widget XAML re-bases its root to bind:
- `Border.Background="{Binding Surface, RelativeSource=Self}"` (replaces the hardcoded
  `#A6080A08`),
- accent elements (`Foreground` of the value `TextBlock`s, `ProgressBar.Foreground`, the value
  text, the green/amber/red shift-light colors) bind to `Accent` where a single accent makes
  sense,
- `LayoutTransform="{ScaleTransform ...}"` driven by `Scale` (LayoutTransform so neighbors and
  hit-testing reflow correctly, unlike RenderTransform).

`FreeLayout.ApplyConfig` sets these DPs per widget from `WidgetConfig` (parsing hex → `Brush`,
freezing the brush). When an override is null, the DP keeps its **theme default**.

### Shared theme object (default source)

The current hardcoded palette (`#A6080A08` surface, `#5AD15A` green, `#E0C93A` amber,
`#E05A5A` red, and the `Off` brush in `TelemetryViewModel`) is consolidated into a
`WidgetTheme` (a `ResourceDictionary` of named brushes, or a static class of frozen brushes).
Widget DPs default to these theme brushes. This is the seam the polish pass edits: change
`WidgetTheme`, and every non-overridden widget updates; per-widget overrides still win.

> The shift lights (`Light1..5`) are computed in `TelemetryViewModel` from fixed `Green/Amber/
> Red`. For MVP, leave those as-is (a per-widget "Accent" on `RpmShift` only retints the RPM
> bar/text, not the staged light colors). Full themable shift-light colors are a later item —
> noted so the polish pass doesn't assume per-widget control there yet.

This separation means: **theme = global defaults (polish pass), `WidgetConfig` = per-widget
opt-in overrides (user).** They compose; they don't fight.

---

## 5. Scope — MVP vs later

**MVP (this spec, recommended cut):**
- `WidgetConfig` + `OverlayConfig.Widgets` + migration/`Normalize`.
- `FreeLayout` (Canvas) replacing runtime preset composition; presets as seeds; F10 still cycles
  (now re-seeds), settings dropdown unchanged.
- Per-widget **drag** in F8 edit mode (with clamping; whole-window move as empty-space fallback).
- Per-widget **visibility** and **scale**, surfaced in the **settings window**.
- Styling hooks: `CustomizableWidget` DPs + `WidgetTheme` defaults wired so scale works and
  colors *can* be set; bind `Surface`/`Accent`.

**Later (follow-up):**
- Per-widget **color overrides UI** — store/restore is in the MVP model, but ship the swatch
  pickers and the **edit-mode right-click context menu** as a fast second pass. (Color *data*
  model lands in MVP so no migration later.)
- Snapping/alignment guides; multi-monitor canvas selection.
- Fully themable shift-light stage colors.
- Drag *handles*/adorners instead of drag-anywhere; per-widget rotation; mini-map (separate spec).

Rationale for the cut: position + scale + visibility deliver the bulk of the user value and are
the riskiest plumbing (Canvas, drag, persistence). Color is low-risk additive UI on a model that
already supports it.

---

## 6. Testing

**Unit-testable (xUnit, no WPF dispatcher needed — pure logic in non-UI helpers):**
- `OverlayConfig.Normalize` / migration: old config (no `Widgets`) → fully seeded; partial
  `Widgets` → missing keys filled, present keys preserved; corrupt fields → defaults.
- `LayoutSeeds.For` returns all six `WidgetId`s for each preset; `ApplySeed` overwrites
  position/scale/visibility but **keeps** color overrides.
- Color parse helper: valid `#AARRGGBB` round-trips; invalid string → null (use default).
- **Position/drag math** extracted into a pure function, e.g.
  `DragMath.Clamp(point, widgetSize, canvasSize)` and grid/edge snapping — unit-tested without
  WPF. Keep this logic out of the code-behind so it is testable.
- JSON round-trip of `OverlayConfig` with a populated `Widgets` dictionary
  (`System.Text.Json` + existing `JsonStringEnumConverter`).

**Manual (consistent with v1 — no automated GUI tests):**
- F8 edit mode: drag individual widgets, drag empty space to move window, verify clamping and
  that positions persist across restart (run `--replay` over a borderless window).
- Settings + context menu: toggle visibility, change scale, change colors; confirm live refresh
  and persistence.
- F10 re-seed: cycling presets re-arranges widgets; confirm whether colors should persist
  (per open question).
- Click-through still works outside edit mode (input reaches the game).

---

## 7. Phased implementation outline

1. **Model + migration.** Add `WidgetConfig`, `WidgetId`, `OverlayConfig.Widgets`,
   `Normalize`/`ApplySeed`, color helper. Unit tests. (No UI change yet.)
2. **Seeds.** Author `LayoutSeeds` for the three presets by converting the existing
   `*Layout.xaml` arrangements to absolute X/Y/scale. Tests for completeness.
3. **FreeLayout (Canvas).** New `FreeLayout` that instantiates the six widgets and applies
   position/scale/visibility from config. Switch `OverlayWindow` to host it (full-screen
   transparent window; drop `SizeToContent`). Verify rendering matches current presets.
4. **Styling hooks.** `CustomizableWidget` base + `WidgetTheme`; re-base each widget's XAML to
   bind `Surface`/`Accent`/`Scale`. Verify visual parity with defaults.
5. **Per-widget drag.** Edit-mode drag with `DragMath` clamping; empty-space whole-window
   fallback; persist on F8 exit.
6. **Settings UI.** `WidgetCustomizeControl` rows (visibility + scale + color swatches) in
   `SettingsWindow`; wire `OnApply` → `FreeLayout.ApplyConfig`.
7. **Context menu (follow-up).** Edit-mode right-click fast-path reusing the same write-back.
8. **Polish/later.** Snapping, themable lights, adorner handles.

---

## 8. Open questions / decisions for the user

1. **Whole-window move:** keep "drag empty canvas = move whole overlay" as a fallback, or remove
   it now that every widget is independently movable and the window is full-screen? (Recommend:
   keep as fallback — cheap, familiar.)
2. **Preset re-apply scope:** when F10/settings applies a preset, should it overwrite only
   position/scale/visibility (recommended) or also **reset color overrides**? Affects `ApplySeed`.
3. **Color UI fidelity:** MVP swatch picker (no dependency) vs a full RGBA color dialog (likely a
   NuGet/extended-toolkit dependency). Recommend swatches for MVP.
4. **Window sizing:** size the overlay window to the full primary working area (simplest, assumed
   above), or keep a configurable canvas size / support multi-monitor selection now? (Recommend:
   primary working area for MVP; multi-monitor later.)
5. **Color granularity:** per-widget `Accent` + `Surface` only (recommended), or finer (separate
   throttle/brake/clutch bar colors, individual shift-light stage colors)? Finer control expands
   `WidgetConfig` and the UI.
6. **Status line:** keep the `Status` text pinned top-left and non-movable (recommended), or make
   it a customizable/movable widget too?
```