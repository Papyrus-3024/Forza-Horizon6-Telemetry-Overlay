# FH6 Telemetry

A Windows telemetry suite for **Forza Horizon 6** "Data Out" UDP telemetry: a composable,
transparent **overlay HUD**, plus a **CLI** for capture, replay, CSV export, and connection
diagnostics. Built around a reusable, well-tested FH6 packet parser.

> Status: v1.0. The overlay and CLI are release-ready. The `web/` map-replay app is a
> work-in-progress and is **not** part of the release.

## Quick start

### Run the release build (no .NET install needed)
1. Download the release and unzip.
2. In FH6: **Settings → HUD and Gameplay → Data Out = ON**, **Data Out IP = `127.0.0.1`**
   (if the game runs on this same PC), **Data Out Port = `20440`**.
3. Run `overlay/Fh6.Telemetry.Overlay.exe`. Start driving — the HUD updates only while you're
   actively driving (not in menus, pauses, replays, or rewinds).

The overlay listens on `0.0.0.0:20440` by default (all interfaces). Game side points at this PC.

### Run from source
Requires the .NET 8 SDK.
```
dotnet run --project src/Fh6.Telemetry.Overlay              # live overlay
dotnet run --project src/Fh6.Telemetry.Overlay -- --replay <capture.jsonl>
dotnet run --project src/Fh6.Telemetry.Cli -- live          # terminal dashboard
```

## Overlay

A transparent, click-through HUD of composable widgets you can freely arrange.

### Hotkeys (global — work while the game has focus)
| Key | Action |
|-----|--------|
| **F7** | Quit the overlay |
| **F8** | Edit mode — drag widgets to reposition (click-through off); saves on exit |
| **F9** | Toggle the settings panel (pinned) |
| **F10** | Cycle layout preset (BottomStrip → CornerPanel → CenterDash) |

The corner **⚙ gear** icon opens the settings panel too — hover to peek, click to pin. Drag
the panel by its header to move it; its position is remembered.

### Settings tabs
- **General** — port, listen address, layout, opacity, HUD scale, theme, accent.
- **Widgets** — show/hide and per-widget scale.
- **Chart** — time window and which series to plot.
- **Layouts** — save / load / rename / delete named layouts.
- **Health** — live connection status (Receiving / no data) and what to check (see below).
- **Capture** — session recording + export (see below).

### Sessions (recording raw telemetry)
Live sessions are recorded to disk automatically so raw telemetry is never lost
(`%AppData%/fh6-overlay/captures/`).
- **Always save sessions** — keep every session automatically (off = only ones you save).
- **Session name** — auto-named, editable.
- **Save current session** — writes the chosen formats (**JSONL / CSV / BIN**) and starts a
  fresh session.

Widgets include: gear, speed, RPM / arc-tachometer, pedals, boost, fuel, power/torque, lap
timing, G-force friction circle, tire temps & grip, suspension, steering, and a multi-series
input/speed chart. Themes: Dark Glass, Sport Red, Cool Blue, Mono (+ custom accent).

## CLI (`fh6`)
```
fh6 live                       # terminal dashboard from live UDP
fh6 capture [-o out.jsonl]     # record live UDP to JSONL
fh6 replay <file> [-s 2]       # replay a capture to the dashboard
fh6 export <file> [-o out.csv] # flatten a capture to pandas-friendly CSV
fh6 doctor [-p 20440] [-s 5]   # diagnose the connection (packets/sec, validity)
```

## Troubleshooting: no data
The **Health** tab (overlay) and **`fh6 doctor`** (CLI) auto-detect whether packets are
arriving. If none are, check (these are game-side and can't be tested by the app):
- FH6 → Settings → HUD and Gameplay → **Data Out = ON**
- Game **Data Out IP = `127.0.0.1`** (same PC) · overlay **listen = `0.0.0.0`**
- The **port matches** on both sides (default `20440`)
- Data is only sent **while actively driving** (not menus / pause / replay)
- No other app is **bound to the port** (close other overlays / SimHub)

## Build & publish
```
dotnet build                   # build everything
dotnet test                    # run the test suite
./publish.ps1                  # self-contained single-file exes → ./dist
```

## Project layout
- `src/Fh6.Telemetry.Core` — packet parser, readouts, capture I/O (JSONL/BIN), CSV export.
- `src/Fh6.Telemetry.Cli` — Spectre.Console CLI.
- `src/Fh6.Telemetry.Overlay` — WPF overlay HUD.
- `tests/` — xUnit tests (golden-value assertions against trimmed capture fixtures).
- `web/` — map-replay web app (WIP; not in the release).
- `FH6_DATA_OUT_DOC.md` — packet format. `BACKLOG.md` — deferred work.

## Packet
Fixed **324 bytes**, little-endian. Documented fields occupy bytes 0–322; byte 323 is
padding. Default UDP port **20440** (configurable in-game).
