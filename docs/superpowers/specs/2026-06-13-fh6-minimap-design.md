# FH6 Telemetry Overlay — High-Res Mini-Map / World-Map Widget Design

Date: 2026-06-13
Status: Draft for review (research + design; not yet approved for planning)

## Goal

Add a **real high-resolution world-map widget** to the overlay that plots the car's live
position (and heading) on an actual rendered map of the FH6 Japan world — *not* the game's
built-in transparent minimap, and *not* a recorded-polyline "track shape". It must run inside
the existing `Fh6.Telemetry.Overlay` (WPF, .NET 8) and reuse `Fh6.Telemetry.Core`'s already
decoded `Position` (X/Y/Z, meters), `Velocity`, `Speed`, and `Yaw`.

This is the deferred mini-map that the overlay design spec
(`docs/superpowers/specs/2026-06-13-fh6-overlay-design.md`) explicitly punted to its own spec,
"its own spec — map asset + affine transform from `Position X/Z`."

### Scope

**MVP**
- A `MiniMapWidget` `UserControl` showing a static high-res map image with a live car marker
  plotted via a calibrated world→pixel affine transform.
- Marker rotates to show heading (from velocity `atan2`, falling back to `Yaw` at low speed).
- A pure, unit-tested `WorldToMap` transform class in **Core**, seeded with the published FH6
  affine constants (below), re-fittable from landmark pairs.
- Config additions so the widget can be enabled/placed and so the map asset path + transform
  can be overridden without recompiling.
- Integration into the existing layout/customization model (it is just another widget).

**Later (post-MVP)**
- Pan/zoom and a "follow-car" auto-center/auto-zoom mode (SimHub-style recenter button).
- True tiled rendering (XYZ pyramid) for crisp zoom instead of one downscaled image.
- A driven-path breadcrumb trail / heatmap (cf. austinbaccus race-line plots).
- Seasonal map layers and POI markers.
- Road-graph features (snap-to-road, routing) — explicitly out of scope; noted only because
  the primary reference ships one.

---

## Map asset sourcing — the KEY open question / blocker

The transform math is *solved* (see next section). The real blocker is **what map image we
are allowed to render**. There is no official Microsoft/Playground map-tile API. Options:

### Option A — Reuse a community interactive-map tile source (e.g. GamerGuides)
The primary reference, **vasyadiagnost/Forza-Horizon-6-Live-Map**, is built directly against
the **GamerGuides Japan interactive map** (its `map_meta.json` carries `"map_id": 481`,
seasonal `layer_id`s `760/757/758/759/756`, a `20000×20000` px map at `tile_size: 256`,
zoom `12–18`, and the in-app subtitle credits the "GamerGuides Japan map").
[map_meta.json](https://raw.githubusercontent.com/vasyadiagnost/Forza-Horizon-6-Live-Map/main/data/map_meta.json),
[GamerGuides Japan map](https://www.gamerguides.com/forza-horizon-6/maps/japan).
Its tiles are served through the app's own `/tile/{layerId}/{zoom}/{tx}/{ty}.png` proxy
endpoint that caches into `tile_cache/481/760/18/...`.
[fh6_live_map_server.py](https://raw.githubusercontent.com/vasyadiagnost/Forza-Horizon-6-Live-Map/main/fh6_live_map_server.py).

- **Pros:** highest-quality, complete, already calibrated (the published affine constants are
  fit *to this exact image*, so we get pixel-accurate plotting for free); seasonal variants.
- **Cons / uncertainty:** this is a **third-party commercial site's content**. The reference
  repo never publishes the upstream tile-server URL — it proxies and caches tiles locally,
  which is effectively scraping. **Redistributing or hot-linking GamerGuides tiles is almost
  certainly against their terms** and is not something we should ship. Treat the upstream tile
  URL as *unconfirmed and not for redistribution*. SimHub's well-known FH4/FH5 GPS maps used
  the same pattern (base maps "taken from the freely available interactive maps on
  swissgameguides"), which shows it's common but does not make it licensed.
  [SimHub FH GPS Maps](https://www.simhubdash.com/community-2/dashboard-templates/forza-horizon-gps-maps-for-simhub/).

### Option B — Ship a single static, self-produced high-res map image
Produce one downscaled full-world image we have the right to ship (e.g. our own in-game
photo-mode/screenshot mosaic, or an explicitly free/redistributable community asset), store it
in the repo/installer, and fit the affine transform to *that* image from landmark pairs.

- **Pros:** legally clean if we own/are-licensed-for the image; zero network at runtime; simple
  WPF (`Image` + transforms). Matches the existing offline, self-contained overlay ethos.
- **Cons:** producing a seamless high-res world mosaic of a brand-new 2026 map is real work;
  lower fidelity than the tiled interactive map; we must re-fit the transform ourselves.

### Option C — Extract the map from game files
Pull the map texture/atlas from FH6's installed assets.

- **Cons:** format reverse-engineering effort, EULA/DMCA exposure, breaks on patches.
  **Not recommended.**

### Recommendation
- **MVP:** Option B, but **do not ship any map image we don't have rights to**. Architect the
  widget so the map image is an *external, user-supplied/optional asset* (a configurable file
  path, blank by default with a "no map configured" placeholder). Ship the **transform,
  marker, calibration tooling, and the published affine constants as the default** — so a user
  who legally obtains a 20000×20000 GamerGuides-style image (or any image) gets pixel-accurate
  plotting immediately, and anyone with a different image re-fits in seconds. This keeps us
  legally clean while delivering the full feature mechanically.
- **Later:** if a properly licensed/redistributable tile source becomes available, add Option A
  tiled rendering behind the same `WorldToMap` transform.
- **Flag for the user:** the map-image licensing decision is a product/legal call, not a
  technical one — see Open Questions.

---

## World → pixel transform

FH6 (like all Forza Data Out) reports world position as `Position.X`, `Position.Y`,
`Position.Z` in **meters**. **X and Z are the ground plane; Y is altitude/height.** The map is
a 2D top-down image, so we use only **X (→ map column) and Z (→ map row)**; `Y` is ignored for
positioning (available later for an altitude readout). This matches austinbaccus's
visualizations, which plot the ground track from the X/Z pair and treat the third axis as
height. [forza-map-visualization](https://github.com/austinbaccus/forza-map-visualization)
(`animated_3d_racing_lines.py`, `interactive_3d_racing_lines.py`).

### The affine model
A 2D affine map handles scale, rotation, shear, and translation in one step:

```
map_x = a * X + b * Z + c
map_y = d * X + e * Z + f
```

### Published FH6 constants (primary reference — fit to the 20000×20000 GamerGuides image)
From vasyadiagnost's `map_meta.json` `formula` block and confirmed in the server code:

```
a = 0.652837    b = 0.000763    c = 10387.027
d = -0.003754   e = -0.657135   f = 9846.097
```
i.e.
```
map_x = 0.652837 * ForzaX + 0.000763 * ForzaZ + 10387.027
map_y = -0.003754 * ForzaX - 0.657135 * ForzaZ + 9846.097
```
[map_meta.json](https://raw.githubusercontent.com/vasyadiagnost/Forza-Horizon-6-Live-Map/main/data/map_meta.json).
Note `a ≈ -e ≈ 0.653` and `b,d ≈ 0` (near-pure scale + Y-flip, tiny rotation): roughly
**1 map pixel ≈ 1.53 m**, and the negative `e` reflects that image rows increase downward
while Forza `Z` increases the other way. **These constants are valid only for that specific
image.** If we ship a different image (Option B), they must be re-fit (below) — but they're the
correct *default* and a sanity check.

### Re-fitting from landmark pairs (when using a different image)
The reference also ships its calibration set, the input to a least-squares affine fit:
[calibration_points.csv](https://raw.githubusercontent.com/vasyadiagnost/Forza-Horizon-6-Live-Map/main/data/calibration_points.csv)

| name | forza_x | forza_z | map_x | map_y |
|---|---|---|---|---|
| Mei's House | -2020.8 | -3108.4 | 9065 | 11894 |
| Highway View Speed Zone | 4419.121 | 362.277 | 13225 | 9545 |
| Nissan Safari Treasure Car | 5459.831 | 2282.841 | 13989 | 8359 |
| Sotoyama and Takashiro Day Trip | 23.06 | 5596.714 | 10406 | 6167 |
| Arashiyama Takao Touge Battle | -343.828 | -1150.676 | 10162 | 10609 |
| Hakone Nanamagari Touge Battle | -2360.912 | -7565.983 | 8835 | 14819 |
| Daikoku Circuit Road Race | 45.556 | -6012.863 | 10429 | 13816 |

**Fitting procedure** (do this offline; bake the result into config, not at runtime):
1. Stand the car at ≥3 (ideally 6–8, well-spread) known world points; record `(X, Z)` from
   telemetry and the corresponding `(px, py)` you read off the chosen map image.
2. Solve the over-determined linear system for `a..f` by least squares. Two independent fits,
   one per output coordinate:
   - `[X Z 1] · [a b c]ᵀ = map_x`
   - `[X Z 1] · [d e f]ᵀ = map_y`
   The normal-equations solution is `θ = (AᵀA)⁻¹ Aᵀ y` for each, where `A`'s rows are
   `[X_i, Z_i, 1]`. This is a tiny 3×3 solve — implementable in Core with no dependency.
3. Report residuals (max/mean pixel error) so a bad pair is obvious.

A small **calibration helper** (a dev-only mode/CLI subcommand, or a few-row editor in
Settings) that captures live `(X,Z)`, lets the user click the map pixel, then prints/saves
`a..f` makes re-fitting painless. MVP can ship the helper as a documented manual workflow.

### Heading derivation
Match the reference exactly (it works well):
- **Above ~1.5 m/s**, derive heading from the **velocity vector transformed into map space**
  (so heading aligns with the *image*, absorbing the transform's Y-flip/rotation):
  ```
  vx_map = a * Velocity.X + b * Velocity.Z
  vy_map = d * Velocity.X + e * Velocity.Z
  heading_deg = atan2(vx_map, -vy_map)   // degrees; 0 = map-up
  ```
- **At/under ~1.5 m/s** (stationary), fall back to `Yaw`: `forward = (sin Yaw, cos Yaw)`, run
  the same map-space transform, then `atan2`.
Reference: `forza_to_map`, `map_vector_to_screen_angle_deg`, and the 1.5 m/s threshold in
[fh6_live_map_server.py](https://raw.githubusercontent.com/vasyadiagnost/Forza-Horizon-6-Live-Map/main/fh6_live_map_server.py).
Transforming the velocity (not raw `Yaw`) is what keeps the arrow visually correct on the
flipped/scaled image — important detail.

---

## Architecture

### Core (UI-agnostic, unit-tested): `WorldToMap`
A pure value/class in `Fh6.Telemetry.Core`, mirroring the `TelemetryReadout` style (small,
allocation-light, fully testable):

```csharp
public sealed class WorldToMap
{
    // Published FH6 defaults (GamerGuides 20000x20000 image).
    public static readonly WorldToMap Fh6Default = new(
        0.652837, 0.000763, 10387.027,
        -0.003754, -0.657135, 9846.097);

    public WorldToMap(double a, double b, double c, double d, double e, double f) { ... }

    // X = east/west, Z = north/south ground plane; Y (altitude) is ignored.
    public (double X, double Y) ToPixel(float worldX, float worldZ) => (
        a * worldX + b * worldZ + c,
        d * worldX + e * worldZ + f);

    // Map-space heading in degrees from a world-space direction vector (velocity or yaw fwd).
    public double HeadingDegrees(float dirX, float dirZ) {
        var vx = a * dirX + b * dirZ;
        var vy = d * dirX + e * dirZ;
        return Math.Atan2(vx, -vy) * 180.0 / Math.PI;
    }

    // Least-squares fit from >=3 landmark pairs -> new WorldToMap (+ residual stats).
    public static WorldToMap Fit(IReadOnlyList<(float X, float Z, double Px, double Py)> pts);
}
```

A tiny `MapMarkerState ToMarker(in TelemetryPacket, float minSpeedMs = 1.5f)` convenience (pixel
+ heading, with the velocity/yaw fallback) keeps the WPF layer dumb.

### Overlay: `MiniMapWidget` (WPF `UserControl`)
Follows the existing widget pattern (a `UserControl` bound to `TelemetryViewModel`; ctor just
`InitializeComponent()`, cf. `SpeedWidget`). Composition:

- A `Canvas` as the map viewport (clipped to the widget bounds).
- An `Image` child = the configured map asset (`BitmapImage`, `RenderOptions` for quality),
  positioned via a `TranslateTransform`/`ScaleTransform` group (the camera).
- A marker child (a `Path`/`Polygon` arrow or a styled dot) positioned with
  `Canvas.SetLeft/Top` to the transformed pixel and rotated with a `RotateTransform` =
  heading. For MVP, **map fixed, marker moves**; for follow-mode (later) invert the camera so
  the marker stays centered and the map translates/scales under it.

The widget exposes a couple of dependency-property/VM hooks so it reacts to telemetry updates
on the existing Dispatcher path (no new threading). It must **gracefully no-op** when no map
asset is configured (placeholder text), since MVP ships without a bundled image.

### VM additions
Extend `TelemetryViewModel` with `MapX`, `MapY`, `HeadingDeg` (computed from `WorldToMap` +
the latest packet) so XAML can bind directly, consistent with the existing readout-wrapping
approach. The transform instance comes from config (defaults to `WorldToMap.Fh6Default`).

### Rendering approach (and why not tiles for MVP)
A single `Image` + transforms is trivial and fast for a static downscaled map, and pannable/
zoomable purely via the transform group. **True XYZ tile loading is deferred** — it adds a tile
cache, async loaders, and (for Option A) the unresolved licensing question. The `WorldToMap`
math is identical either way, so moving to tiles later is additive.

---

## Configuration & settings integration

Extend `OverlayConfig` (currently `Port/ListenAddress/Layout/Opacity/WindowLeft/WindowTop`)
with an optional `MiniMap` section, all defaulted so existing configs keep working:

```jsonc
"MiniMap": {
  "Enabled": false,                 // off by default (no asset shipped)
  "MapImagePath": null,             // user-supplied high-res image; null => placeholder
  "MapWidthPx": 20000,
  "MapHeightPx": 20000,
  "Transform": {                    // defaults to published FH6 constants
    "A": 0.652837, "B": 0.000763, "C": 10387.027,
    "D": -0.003754, "E": -0.657135, "F": 9846.097
  },
  "Zoom": 1.0,                      // later: pan/zoom + follow
  "FollowCar": true,
  "MarkerSizePx": 18
}
```

- `ConfigStore` already loads/saves this JSON; the new section just rides along (and the
  "missing/corrupt → defaults" behavior covers older files).
- The map is placed by absolute coords within a layout exactly like the other six widgets, so
  it slots into the post-MVP per-widget free-drag/customization model the overlay spec reserved
  (`Widgets` section). For MVP, give it a sensible default slot in one or more presets (e.g. a
  corner of CornerPanel / CenterDash) and a settings toggle.
- `SettingsWindow` gains: an **Enable mini-map** checkbox, a **Browse for map image** picker,
  and (nice-to-have) a small calibration panel that runs `WorldToMap.Fit` from captured pairs
  and writes the `Transform` block.

---

## Testing strategy

- **Unit tests (xUnit, existing Core test project):**
  - `WorldToMap.ToPixel` against the 7 published calibration pairs using `Fh6Default`; assert
    each predicted pixel is within a small tolerance (a few px) of the table's `map_x/map_y`.
    This both tests our implementation *and* validates we transcribed the constants correctly.
  - `WorldToMap.Fit`: feed the 7 pairs, assert the recovered `a..f` are close to the published
    constants and that residuals are small; add a synthetic exact-fit case (points generated
    from a known affine) that must recover it to ~machine precision.
  - `HeadingDegrees`: known vectors (due-north/east in world space) map to expected on-screen
    angles; verify the velocity-vs-yaw fallback switch at the 1.5 m/s threshold via
    `ToMarker`.
- **Manual (no automated GUI test, per overlay convention):** run `--replay <capture>` with a
  configured map image; confirm the marker tracks roads and the arrow points the way the car
  drives, including the stationary-yaw fallback. Verify graceful placeholder when no asset is
  set.

---

## Implementation outline (phases)

1. **Core transform (TDD):** add `WorldToMap` (+ `Fit`, `HeadingDegrees`, `ToMarker`) with the
   published constants; write the calibration-pair and fit tests first. No UI.
2. **Config plumbing:** add the `MiniMap` section to `OverlayConfig`; load/save through
   `ConfigStore`; wire defaults.
3. **VM hooks:** expose `MapX/MapY/HeadingDeg` on `TelemetryViewModel` from `WorldToMap`.
4. **Widget (MVP):** `MiniMapWidget` with `Image` + marker + transforms; static map, moving
   marker; placeholder when no asset; drop it into a layout preset slot.
5. **Settings:** enable toggle + image picker; document the manual calibration/re-fit workflow.
6. **Later:** pan/zoom + follow-car mode; breadcrumb trail; tiled rendering / seasonal layers /
   POIs if a licensed tile source is secured.

---

## Open questions / uncertainties (flagged)

1. **Map image rights (blocking, product/legal):** Do we have rights to *ship* any world-map
   image? The reference's source (GamerGuides) is third-party commercial content and almost
   certainly not redistributable. Recommended MVP avoids shipping one (user supplies the
   image); confirm that's acceptable, or identify a properly licensed/own-produced image.
2. **Upstream tile URL is unconfirmed.** The reference proxies/caches tiles and never publishes
   the GamerGuides tile endpoint; I did not (and should not) scrape it. If Option A is ever
   pursued, the exact tile URL and its terms must be obtained legitimately.
3. **Constant validity across versions/regions.** The published `a..f` are fit to one specific
   20000×20000 image of the FH6 Japan map at a point in time. If the game map or the source
   image changes, re-fit. They are correct as a default + sanity check, not a guarantee.
4. **Single-image fidelity vs tiles.** A downscaled static image may look soft when zoomed.
   Acceptable for MVP? If not, prioritize tiled rendering (and resolve #1/#2 first).
5. **`Position` units/centering.** We rely on Core decoding `Position` as meters with the same
   X/Z ground-plane convention the constants assume; verify once with a live/replay capture
   against a known landmark before trusting plotting.

## Sources

- Primary reference (affine constants, tile scheme, heading): vasyadiagnost/Forza-Horizon-6-Live-Map —
  [repo](https://github.com/vasyadiagnost/Forza-Horizon-6-Live-Map),
  [map_meta.json](https://raw.githubusercontent.com/vasyadiagnost/Forza-Horizon-6-Live-Map/main/data/map_meta.json),
  [calibration_points.csv](https://raw.githubusercontent.com/vasyadiagnost/Forza-Horizon-6-Live-Map/main/data/calibration_points.csv),
  [fh6_live_map_server.py](https://raw.githubusercontent.com/vasyadiagnost/Forza-Horizon-6-Live-Map/main/fh6_live_map_server.py).
- X/Z ground-plane plotting: [austinbaccus/forza-map-visualization](https://github.com/austinbaccus/forza-map-visualization).
- Recorded-vs-image map tradeoffs & community-tile precedent: [SimHub Forza Horizon GPS Maps](https://www.simhubdash.com/community-2/dashboard-templates/forza-horizon-gps-maps-for-simhub/).
- Map image source the constants were fit to: [GamerGuides FH6 Japan interactive map](https://www.gamerguides.com/forza-horizon-6/maps/japan).
- MapGenie-style community maps precedent: [Map Genie FH5 app](https://play.google.com/store/apps/details?id=io.mapgenie.fh5).
