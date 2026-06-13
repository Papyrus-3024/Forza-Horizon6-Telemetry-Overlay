# FH6 Telemetry Overlay — Design (v1 / MVP)

Date: 2026-06-13
Status: Approved for planning (user waived post-spec review for this round)

## Goal

An always-on-top, transparent overlay HUD that renders live Forza Horizon 6 telemetry over
the running game. v1 ships a composable widget set arranged into three selectable layout
presets, a settings page + persisted config, and works from live UDP or a recorded capture
(dev without the game). It reuses `Fh6.Telemetry.Core` for all telemetry decoding.

The high-res mini-map is intentionally a **separate later spec** (it needs a map asset and
coordinate calibration). Per-widget free-drag and per-widget customization (color/size/
visibility) are **post-MVP**, but the architecture is built so they are cheap to add.

## Architecture

New project `Fh6.Telemetry.Overlay` (`net8.0-windows`, `<UseWPF>true</UseWPF>`), added to
`FH6-Telemetry.sln`, referencing `Fh6.Telemetry.Core`. No telemetry logic is reimplemented.

```
ITelemetrySource ──> TelemetryPump (bg thread: parse) ──Dispatcher──> TelemetryViewModel
 (UdpTelemetrySource │ JsonlReplaySource, from Core)                   (INotifyPropertyChanged)
                                                                              │ data binding
                                                              ┌───────────────┴───────────────┐
                                                         Layout preset (A/B/C) composes widgets
                                              Gear · Speed · RpmShift · PedalsSteer · LapTiming · Boost
```

### Core additions (UI-agnostic, unit-tested)
- `TelemetryReadout` — a pure mapping from a raw `TelemetryPacket` to display-ready values:
  `SpeedKmh`, `SpeedMs`, `Gear`, `Rpm`, `MaxRpm`, `RpmFraction` (Current/Max, clamped 0..1),
  `ShiftLightStage` (0–5 from RPM-fraction thresholds), `ThrottleFraction`/`BrakeFraction`/
  `ClutchFraction` (byte/255), `SteerFraction` (Steer/127, clamped -1..1), `Boost`,
  `FuelPercent`, `LapNumber`, `RacePosition`, `IsRaceOn`. Computed via a constructor/factory
  from a `TelemetryPacket`.
- `LapTime.Format(float seconds)` — formats lap seconds as `m:ss.fff` (`0.0` → `"--:--.---"`).
- Shift-light thresholds (RpmFraction): `>=0.80 → 1`, `0.85 → 2`, `0.90 → 3`, `0.94 → 4`,
  `0.97 → 5` (stage = count of thresholds met). Exact values tuned but fixed in code.

### Overlay project structure
| File | Responsibility |
|---|---|
| `App.xaml(.cs)` | Entry: parse args, load config, build source, start pump, show overlay |
| `OverlayWindow.xaml(.cs)` | Transparent top-most host; applies click-through + position/opacity; hosts the active layout; owns global hotkeys |
| `ViewModels/TelemetryViewModel.cs` | `INotifyPropertyChanged` surface; wraps `TelemetryReadout`; tracks derived shift direction via `GearShifts` |
| `Telemetry/TelemetryPump.cs` | Background read of `ITelemetrySource`, parse, marshal to VM on the Dispatcher |
| `Widgets/GearWidget`, `SpeedWidget`, `RpmShiftWidget`, `PedalsSteerWidget`, `LapTimingWidget`, `BoostWidget` | Reusable `UserControl`s bound to the VM |
| `Layouts/BottomStripLayout`, `CornerPanelLayout`, `CenterDashLayout` | `UserControl`s composing the widgets on a `Canvas`/`Grid` per preset |
| `Settings/OverlayConfig.cs` | Serializable settings model |
| `Settings/ConfigStore.cs` | Load/save JSON at `%AppData%/fh6-overlay/config.json` |
| `Settings/SettingsWindow.xaml(.cs)` | Edit port/address/layout/opacity; apply + persist |
| `Interop/ClickThrough.cs` | `WS_EX_LAYERED \| WS_EX_TRANSPARENT` via `SetWindowLong` |
| `Interop/GlobalHotkey.cs` | `RegisterHotKey` so hotkeys work while the game has focus |

## Overlay window behavior

- Transparent (`AllowsTransparency=true`, `WindowStyle=None`, `Background=Transparent`),
  `Topmost=true`, `ShowInTaskbar=false`.
- **Click-through by default** (input passes to the game) via extended window styles.
- **Global hotkeys** (work while FH6 is focused):
  - **F8** — toggle *edit mode*: disables click-through and shows a drag handle/border so the
    window can be moved; toggling back re-enables click-through and saves the new position.
  - **F9** — open the **Settings** window.
  - **F10** — cycle layout preset (A→B→C).
- Opacity, position, and layout persist to config and restore on launch.

## Configuration & settings page

`OverlayConfig` (JSON) fields, all with defaults:
- `Port` (int, default 20440)
- `ListenAddress` (string, default `"0.0.0.0"`)
- `Layout` (enum string: `BottomStrip` | `CornerPanel` | `CenterDash`, default `BottomStrip`)
- `Opacity` (double 0.2–1.0, default 0.9)
- `WindowLeft`, `WindowTop` (double; null/absent → default corner)
- Reserved for post-MVP: a `Widgets` section (per-widget position/color/size/visible).

`ConfigStore` loads on start (creating defaults if missing) and saves on change/exit.

`SettingsWindow` edits Port, ListenAddress, Layout (dropdown), Opacity (slider). **Apply**
persists config, switches layout live, and restarts the telemetry source if Port/Address
changed. The page is laid out so future customization controls slot in without restructuring.

## Layout presets (all three implemented)

The same six widgets, arranged differently, selected by config/F10:
- **A · Bottom strip** — horizontal bar across the bottom edge (default).
- **B · Corner panel** — compact stacked block in a corner.
- **C · Center race-dash** — large centered gear + shift lights low; pedals and lap timing in
  flanking corners.

Each widget is placed by absolute coordinates within its layout, which is what makes
post-MVP **per-widget free-drag** a small addition (enable drag + persist each widget's
position in the reserved `Widgets` config section).

## Data source & launch flags

Reuses Core sources. Flags:
- default: live UDP using config `Port`/`ListenAddress`
- `--replay <file> [--speed N] [--loop]` — dev playback (honors timing), no game required
- `--port <n>`, `--layout A|B|C`, `--opacity <0..1>` — override config for this run

## Error handling

- UDP bind failure (port in use / firewall) shows a non-fatal message in the overlay and via
  the settings page; the app stays open so the port can be changed.
- Malformed/short packets are skipped (Core `TryParse`).
- Missing/corrupt config → fall back to defaults and rewrite a clean file.
- Pump exceptions are caught per-iteration; the loop ends cleanly on shutdown/source dispose.
- README documents the **borderless/windowed** requirement (exclusive fullscreen bypasses the
  DWM compositor so overlays don't show) and the UWP loopback note for Forza.

## Testing

- Unit tests (xUnit, existing test project → Core): `TelemetryReadout` mappings (km/h,
  RpmFraction clamping, ShiftLightStage thresholds, pedal/steer fractions, FuelPercent) using
  the real golden frame; `LapTime.Format` cases including the zero/"not set" case.
- `GearShifts` already tested.
- WPF rendering, click-through, and global hotkeys are verified manually by running
  `--replay` over a borderless window (no automated GUI test in v1).

## Reference mockup (frontend-design)

At implementation time, generate one polished **HTML reference mockup** of the full HUD via
the frontend-design plugin, used purely as a visual target the WPF widgets approximate
(it cannot emit XAML). Not shipped.

## Scope

**MVP (this spec):** overlay shell (transparent, click-through, F8 drag, F9 settings, F10
cycle), six composable widgets, three presets, live + replay sources, `OverlayConfig` +
`ConfigStore` + `SettingsWindow`, testable `TelemetryReadout`/`LapTime` in Core.

**Post-MVP (separate):** per-widget free-drag/dock; per-widget customization (color, size,
show/hide) surfaced in the settings page; the high-res **mini-map** (its own spec — map asset
+ affine transform from `Position X/Z`).
