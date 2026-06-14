# Sim-Racing Dashboard/Overlay UI/UX — Research for a V2 Visual Refresh

Date: 2026-06-13
Status: Research / inspiration (no code changes)

## Goal

Our WPF FH6 overlay (v1) renders correct telemetry but looks plain: flat numbers, uniform
bars, a basic traction circle, thin uniform line charts. This doc surveys how established
sim-racing dashboards and overlays make telemetry *eye-catching* — color, motion, and visual
encoding — and turns those patterns into concrete, grounded upgrade ideas for each metric we
already expose on `TelemetryReadout` (speed, rpm/shift, gear, throttle/brake/clutch, steering,
G-force, tire temp, boost, fuel, power/torque, lap). Patterns are drawn from real tools; the
intent is to copy what works rather than invent in a vacuum.

## What the established tools do

### SimHub (Dash Studio, custom dashes)
- **Threshold/gradient coloring is the core idiom.** Tire boxes and gauges change *border* and
  *fill* color by value band — e.g. tire temp colored cold (~0–55°C, blue) → optimal (green) →
  hot (95–115°C, orange/red). The whole channel reads at a glance from color before you parse the
  number. Brake-disc temps get the same per-corner color treatment.
- **Shift / redline animation.** Rev gauges *flash* as RPM approaches the limiter; the gear digit
  *blinks* at the limiter; the entire dash can flash at the shift point. Perimeter RGB LED strips
  run a progressive green→yellow→red sweep approaching redline (mirrored in software on screen as a
  segmented arc).
- **Gradient color gauges** (community-requested and shipped): a single gauge whose color
  interpolates continuously across its value range instead of stepping — used for tire pressure/temp,
  modeled on the BMW M6 GT3 cluster.
  Sources: SimHub gradient-color-gauge thread; Forza community "Tire Temp & Wear HUD for SimHub";
  Reiza "funky tyre temps"; siemens-mobile dashboard review (shift-light staging at 90/95/96%).

### RaceLab (modern iRacing/ACC overlay packs)
- **G-Force Meter**: a live dot inside a traction/friction circle. The dot moves with combined
  lateral+longitudinal G; sitting at the ring edge means grip is maxed, inside means grip in reserve.
  Shows current G numbers and **tracks/marks peak G**. Ships as a standalone overlay *and* as a module
  embedded in the Input Telemetry overlay.
- **Input Telemetry**: live pedal bars + steering, with a scrolling trace, overlaid against your best
  lap / a friend for comparison (ghost reference line).
- House design language: "clean, minimal, easy-to-scan" with heavy customization — the eye-catch comes
  from motion and color, not clutter.
  Sources: RaceLab G-Force Meter announcement; RaceLab iRacing overlays / Input Telemetry pages.

### MoTeC i2 (analysis-grade telemetry)
- Each input is shown **two ways at once**: a continuous **line trace** over time *and* a **vertical
  bar** showing instantaneous % (brake, throttle). The trace gives history/shape; the bar gives the
  current magnitude — the dual encoding is the takeaway.
- Stacked, time-aligned traces (speed/RPM/gear/brake/throttle) make differences in braking points and
  throttle application pop when laps are overlaid.
  Sources: Coach Dave "How to use MoTeC data" (ACC, iRacing); GTR2 MoTeC i2 Pro guide.

### Z1 Dashboard
- Tire temps shown **graphically and numerically together** (heat-style colored corners + numbers),
  alongside pressures, ride height, shock deflection. Two dedicated telemetry pages combine tires,
  wear, temp, G-forces, and inputs in one graphical view. Fuel shown as per-lap consumption for
  session and stint.
  Source: Z1 Dashboard manual (telemetry / garage display pages).

### General data-viz techniques worth stealing
- **Gradient fill under traces** (area sparklines) instead of bare lines — adds depth/weight; two-stop
  gradients render along or under the series.
- **Animated value transitions / needle easing** — gauges sweep to new values with easing rather than
  snapping; smooths jitter and reads as "alive."
- **Sparkline variants**: line / area / column / win-loss — i.e. the same channel can be drawn with
  fill or as columns for more visual weight than a 1px line.
- **Segmented bars** and glow/pulse on state change signal interaction and events.
  Sources: Vuetify Sparklines; AG Grid Sparklines; Flutter charts gallery; dashboard-design guides.

## Eye-catching technique toolbox (reusable across widgets)

| Technique | What it is | When to fire |
| --- | --- | --- |
| Threshold color bands | Discrete color per value zone (blue/green/orange/red) | Tire temp, brake temp, fuel low, RPM |
| Continuous gradient | Color interpolated smoothly across range | Tire temp/pressure, RPM arc |
| Gradient area fill | Filled translucent area under a trace, fading to transparent | Throttle/brake/speed traces |
| Variable stroke weight | Line thickens with magnitude or recency | Speed/steering trace emphasis |
| Glow / bloom | Soft outer glow on a bright element | Redline RPM, shift point, G dot at limit |
| Pulse / flash | Rhythmic opacity/scale animation | Limiter, lockup, fuel critical |
| Needle/value easing | Animated sweep to new value | Speedo, RPM, boost |
| Segmented arc/bar | Discrete lit segments rather than continuous fill | RPM (LED-style), boost, fuel |
| Ghost / peak marker | A faint reference line or a held max marker | G-force peak, best-lap trace, peak speed |
| Tether line | Thin line connecting a moving dot to center | G-force / traction dot |
| Event flare | Brief scale-up + color burst on a digit | Gear on shift |

## Per-metric visual treatment

| Metric (our field) | Common sim-dash treatment | Eye-catching effect to adopt | Source |
| --- | --- | --- | --- |
| Speed (`SpeedKmh`) | Big number; sometimes radial speedo | Number plus a thin gradient-area sparkline of recent speed; held faint **peak-speed** marker | MoTeC dual encoding; AG Grid/Vuetify area sparklines |
| RPM / shift (`RpmFraction`, `ShiftLightStage`) | Segmented arc green→yellow→red; LED strip; flash near limiter | Segmented RPM arc that fills + recolors by fraction; **glow + pulse** on the top segments at redline; whole-arc flash at shift | SimHub shift staging 90/95/96%; siemens-mobile review |
| Gear (`Gear`) | Large central digit; blinks at limiter | **Gear flare**: brief scale-up + color burst when the digit changes; blink/flash when at limiter | SimHub gear-blink-at-redline |
| Throttle/Brake/Clutch (`*Fraction`) | Vertical % bars; MoTeC also shows traces | Keep bars (green throttle / red brake) **and** add a short scrolling gradient-area trace; flash brake bar on lockup if detectable | MoTeC dual bar+trace; RaceLab Input Telemetry |
| Steering (`SteerFraction`) | Centered horizontal bar or wheel angle | Center-anchored bar that fills left/right from middle; optional thin steering trace under the pedal traces | RaceLab Input Telemetry; MoTeC stacked traces |
| G-force (world pos / accel) | Dot in a traction/friction circle | Dot **tethered to center by a thin line**; recolor + glow as it nears the ring; faint **peak-G marker** held; short fading trail | RaceLab G-Force Meter |
| Tire temp | Per-corner colored boxes (cold→hot) + numbers | **Continuous gradient** corner fills blue→green→orange→red with the number overlaid; pulse red when over-temp | SimHub tire HUD; Z1 graphical+numeric tires |
| Boost (`Boost`) | Bar or psi gauge, scaled to car max, hidden if N/A | Segmented or gradient boost bar with **eased fill**; subtle glow at peak boost; hide when zero/non-turbo | fastprint3d/Initial-D boost gauges; SimHub auto-hide |
| Fuel (`FuelPercent`) | Bar / per-lap consumption | Segmented fuel bar; recolor **amber→red** under low thresholds with a slow pulse when critical | Z1 fuel per-lap; threshold coloring |
| Power/torque | Less common on live HUDs; trace in analysis | Small gradient-area sparkline of recent power (and/or torque) to show the curve shape | MoTeC traces; area sparklines |
| Lap (`LapNumber`, lap times) | Lap delta bar green/red vs best; sector splits | **Lap-delta bar** that grows left(faster/green)/right(slower/red) from center; flash green on a new best lap | RaceLab delta overlay; GT7 HUD best-lap delta |

## High-impact, low-effort first pass

Ordered by visual payoff per unit of work; all build on widgets/fields we already have.

1. **Tire-temp gradient/threshold coloring** — map temp to a blue→green→orange→red brush on the
   existing tire readouts. Pure color, no new geometry. Highest "wow" for least effort, and the
   user explicitly asked for it.
2. **G-force tether + peak marker** — add a thin line from the traction-circle center to the existing
   dot, recolor/glow the dot near the edge, and hold a faint peak marker. Small additions to a widget
   we already draw.
3. **Filled/gradient traces on the line charts** — give existing throttle/brake/speed charts a
   translucent gradient area fill (fade to transparent) instead of a 1px line. A brush/geometry change,
   not new plumbing.
4. **RPM shift glow + flash** — we already compute `ShiftLightStage`; bind the top segments to a
   glow + opacity pulse at the highest stage and flash the gear digit at the limiter.
5. **Lap-delta center bar** — once a best-lap reference exists, a center-anchored green/red bar with a
   new-best flash is cheap and very legible.

Defer (more work): continuous needle easing on a radial speedo, scrolling ghost/reference comparison
traces, and segmented LED-style RPM arcs — high quality but heavier to build.

## Sources

- [SimHub gradient color gauge (forum)](https://www.simhubdash.com/community-2/simhub/gradient-color-gauge/)
- [Tire Temp & Wear HUD for SimHub (Forza forums)](https://forums.forza.net/t/tire-temp-wear-hud-for-simhub/643080)
- [SimHub funky tyre temps (Reiza forum)](https://forum.reizastudios.com/threads/simhub-funky-tyre-temps.15412/)
- [Best Sim Racing Dashboard Displays (shift-light staging)](https://siemens-mobile.com/best-sim-racing-dashboard-display-screens/)
- [RaceLab — G-Force Meter announcement](https://garage.racelab.app/news/2025/09/22/2025/g-force-meter/)
- [RaceLab — iRacing overlays / Input Telemetry](https://racelab.app/iracing/)
- [Coach Dave — How to use MoTeC data in ACC](https://coachdaveacademy.com/tutorials/how-to-use-motec-data-in-assetto-corsa-competizione/)
- [Coach Dave — How to use MoTeC data in iRacing](https://coachdaveacademy.com/tutorials/how-to-use-motec-data-in-iracing/)
- [Z1 Dashboard — telemetry analysis manual](https://paddock.z1racetech.com/manual/dashboardTelemetry.cfm)
- [Z1 Dashboard — garage displays manual](https://paddock.z1racetech.com/manual/garage.cfm)
- [Vuetify Sparklines (line/area variants)](https://vuetifyjs.com/en/components/sparklines/)
- [AG Grid Sparklines](https://blog.ag-grid.com/introducing-ag-grid-sparklines/)
- [Flutter charts/sparkline/speedometer gallery](https://fluttergems.dev/plots-visualization/)
- [SimHub boost gauge (fastprint3d)](https://fastprint3d.fr/en/displays/142-boost-20bar-or-30bar-level-gauge-controlled-by-simhub-simracing-plug-and-play.html)
- [GT7 HUD (best-lap delta)](https://www.overtake.gg/downloads/gt7-hud.56420/updates)
