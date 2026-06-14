# FH6 Web Replay

A lightweight, dependency-free web app that replays an FH6 "Data Out" telemetry
capture from a **top-down view over the Forza Horizon 6 world map**, with live
telemetry readouts.

## Run

```sh
cd web
npm install   # no dependencies, but keeps the workflow conventional
npm start     # serves http://localhost:3000
```

Then open <http://localhost:3000>.

Set a different port with `PORT=8080 npm start`.

## What it does

- Loads an FH6 capture (JSONL: each line `{"t": <ms>, "len": 324, "b64": "<packet>"}`).
  Packets are decoded **client-side** with `DataView` (little-endian) into a
  per-frame array (position, speed, rpm, gear, throttle/brake, steer, …).
- Draws the selected **season map** as a top-down backdrop you can **pan (drag)**
  and **zoom (scroll wheel)**.
- Overlays the car's **path** (polyline of world X/Z) and a **moving marker**
  (a heading arrow) for the current frame.
- **Replay controls:** play/pause (also Spacebar), a scrubber that seeks by
  capture time `t`, and a playback-speed selector (0.25×–8×).
- **Telemetry panel** updates live: speed (km/h + mph), gear, RPM, throttle %,
  brake %, steer, race-on flag, world X/Z, plus throttle/brake/RPM bars.

## Loading a capture

Two ways, use either:

1. **Bundled** dropdown — lists `.jsonl` files in `web/captures/`
   (served via `/api/captures` and `/api/capture/:name`).
2. **From file** picker — reads any `.jsonl` capture directly in the browser.

To add your own bundled captures, drop `.jsonl` files into `web/captures/`
(they are git-ignored by default; `sample-drive.jsonl` is committed so the
dropdown shows a real drive out of the box).

## World → map calibration

The world positions are in meters and have no built-in mapping to map-image
pixels, so alignment is done in two stages:

- **Auto-fit (default):** the path's X/Z bounding box is scaled and centered to
  fill the view, so you always see something without any calibration.
- **Manual nudge sliders** to line the path up on the map by eye:
  - **Scale** — resize the path relative to the map.
  - **Offset X / Offset Y** — slide the path in pixels.
  - **Rotation°** — rotate the path (world and map axes rarely agree).
  - **Flip Z axis** — mirror the ground-plane Z axis if the path is reversed.
  - **Re-auto-fit** — recompute the auto-fit and reset pan/zoom.
  - **Reset nudge** — clear the manual sliders (keeps the Flip toggle).

Map pan/zoom (mouse) is separate from calibration: the map is a fixed backdrop
and the sliders move the **path** over it. Calibration is eyeballed — there is no
georeferenced transform between world meters and map pixels.

## Map assets (AVIF)

The four seasonal maps are large AVIF images (~8192², 50–60 MB each). Browsers
render AVIF natively. They are **not committed** (git-ignored under
`public/maps/*.avif`).

Place them at:

```
web/public/maps/spring.avif
web/public/maps/summer.avif
web/public/maps/autumn.avif
web/public/maps/winter.avif
```

If a season image is missing, the app still runs and shows a neutral grid
background with a note in the Map panel. `/api/maps` reports which are present.

## Packet format

Offsets used (little-endian, from `FH6_DATA_OUT_DOC.md`): IsRaceOn s32@0,
TimestampMs u32@4, EngineMaxRpm f32@8, CurrentEngineRpm f32@16, PositionX f32@244,
PositionY f32@248, PositionZ f32@252, Speed f32@256 (m/s), Accel u8@315,
Brake u8@316, Gear u8@319, Steer s8@320. Ground plane is X/Z.
