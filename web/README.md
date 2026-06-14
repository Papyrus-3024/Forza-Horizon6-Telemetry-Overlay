# FH6 Web Race-Review Workbench

A lightweight, dependency-free web app for **reviewing a captured FH6 "Data Out"
drive**: time-synced telemetry charts, a top-down track path, a live readout, and
a scrubber. No build step, no framework — plain Node http server + vanilla ES modules.

## Run

```sh
cd web
npm install   # no dependencies, but keeps the workflow conventional
npm start     # serves http://localhost:3000
```

Then open <http://localhost:3000>. Set a different port with `PORT=8080 npm start`.

Deep-link straight to a bundled capture with `?capture=<name>`, e.g.
<http://localhost:3000/?capture=sample-drive.jsonl>.

## What it does

- **Loads a capture** (bundled dropdown or a local file picker) and decodes every
  324-byte packet **client-side** into a per-frame array (the full documented field
  set: position, orientation, speed, rpm, per-wheel slip/temp/suspension, g-force,
  boost, fuel, lap data, inputs, …).
- **Synced charts** — a stack of time-aligned traces (speed, RPM, throttle/brake,
  steer, lat/long G) sharing one time axis, with a crosshair at the cursor. Click or
  drag any chart to seek.
- **Track path** — auto-fit top-down plot of the world X/Z path with start/end dots
  and a car marker oriented by `Yaw`. Drag to pan, scroll to zoom.
- **Live readout** — speed (km/h + mph), gear, RPM, throttle/brake %, steer, boost,
  fuel %, lap, race position, world X/Z, lat/long G, plus input bars.
- **Transport** — play/pause (Spacebar), a time scrubber, a playback-speed selector
  (0.25×–8×), and a lap selector that jumps to a lap's start.

## Capture formats

The parser **auto-detects** two formats:

1. **JSONL** — each line `{"t": <ms>, "len": 324, "b64": "<packet>"}` (the CLI's
   capture format). Timing comes from `t`.
2. **Raw `.bin`** — a stream of fixed **324-byte** little-endian packets, no wrapper.
   Capture time is derived from the in-packet `TimestampMs` (u32-wrap aware), falling
   back to 60 Hz spacing if those timestamps are flat. A trailing partial packet is
   ignored with a console warning.

Load either via the **Bundled** dropdown (`.jsonl`/`.bin` in `web/captures/`, served
by `/api/captures` + `/api/capture/:name`) or the **From file** picker. Two samples
are committed — `sample-drive.jsonl` and the equivalent `sample-drive.bin` — so both
paths work out of the box. Other captures dropped in `web/captures/` are git-ignored.

## World map

The path-only plot is the default. The **Map** > **Style** selector can switch in an
optional **FH6 map** backdrop (loaded from a remote URL — see `MAP_SOURCES` in
`app.js`), so no local assets are needed and nothing large is committed. If the image
fails to load the path falls back to a neutral grid.

The backdrop is fitted/centered and pans/zooms in lockstep with the path, but it is
**not georeferenced** to true world coordinates — the driving line won't sit on the
real roads (a hint under the selector notes this). See the calibration `TODO` in
`track.js`. The four seasonal FH6 maps (Spring/Summer/Autumn/Winter) stay in
`MAP_SOURCES` but are hidden from the selector for now: the AVIFs are ~53 MB each and
load poorly. Re-enable them by re-adding their `<option>`s in `index.html`.

## Packet format

Full FH6 layout (324 bytes, little-endian) is in `parser.js`, verified against
`../FH6_DATA_OUT_DOC.md`. FH6 inserts `CarGroup`, `SmashableVelDiff`, and
`SmashableMass` between `NumCylinders` and `PositionX`.

## Tests

```sh
cd web
npm test   # node --test: parser decode, format auto-detect, lap splitting
```
