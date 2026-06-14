# Real-World Automotive Instrument/HUD Design — Research for a V2 Visual Refresh

Date: 2026-06-13
Status: Research / inspiration (no code changes)

## Goal

Companion to the sim-racing research (`2026-06-13-uiux-sim-research.md`). That doc surveyed how
sim *overlays* render telemetry. This one goes to the source those overlays imitate: **real
performance-car instrument clusters, OEM track displays, and motorsport steering-wheel dashes**.
The user wants the V2 overlay to feel like a real performance car's instrument cluster — tire-temp
color zones, a g-meter with the dot tethered to center, animated tachometers, ambient color shifts —
not just prettier numbers. Every idea below maps to a metric we already expose on `TelemetryReadout`
(speed, rpm/`ShiftLightStage`, gear, throttle/brake/clutch, steering, lat/long G, boost, power/torque,
fuel, lap timing, world position). Sources are real cars and serious driving tools; the intent is to
copy the established visual language rather than invent one.

> Data note: tire temperatures/pressures and fluid temps are **not currently surfaced** on
> `TelemetryReadout` (the FH "sled"/dash packet does carry per-wheel data). The tire-silhouette and
> temp-arc treatments below are aspirational — they require plumbing those fields through first, but
> are documented here because they are the single most eye-catching real-car idiom.

## What real cars and motorsport do

### G-force display — the friction circle and the "tethered dot"
The "dot connected to center by a thin line" the user described is the **friction circle / traction
circle / g-g diagram** from vehicle dynamics, rendered live. The dot is the resultant acceleration
vector: distance from center = combined g magnitude, angle = direction. The tether is that vector
drawn as a line; a fading trail is the dot's recent path.

- **Porsche Sport Chrono Plus** (911/Cayman/Taycan TFT): the canonical OEM circular g-display, current
  lateral+longitudinal acceleration shown graphically with a separate **resettable peak-g** sub-screen.
- **Porsche Track Precision App** and **Chevrolet Corvette Performance Data Recorder**: render the
  combined lateral/braking/acceleration vector as one meter (a friction-circle readout) burned into
  recorded video, not three separate bars.
- **Nissan GT-R multifunction display** (designed by Polyphony Digital / *Gran Turismo*): the "looks
  like a PlayStation game" cluster — overall G, lateral G, accel G on dark backgrounds with neon/cyan
  plotting. This GT-aesthetic dark+neon polish is the look to evoke.
- **Phone/sim g-meters** give the clearest rendering detail: a dot plotted on X/Y in real time with a
  **dynamic fading motion trail**, plus **peak-hold markers held in place with a glowing highlight**
  (G-Force Meter+). The motorsport friction circle draws a **red boundary ring = 100% of available
  grip**, faint concentric **0.5 g / 1.0 g rings** as a scale, and **axis crosshairs** (vertical =
  braking up / accel down, horizontal = lateral). The dot ramps green→yellow→red as it nears the limit.
  Sources: VRS / vrperfdev traction-circle explainers; Porsche Newsroom; Corvette PDR write-ups;
  GT-R MFD articles; G-Force Meter+ / Racelab listings.

### Tachometer, shift lights, gear, speed
- **Central tachometer** is the Porsche signature: the rev counter sits dead-center because "exact
  speed is secondary" on track; today's digital cluster keeps it central with speed nested inside. The
  dial carries a **green optimal/power band** and a **red over-rev zone** as colored arc segments.
- **Mode-driven re-theme** (Corvette C8): Tour = round dial, cool blue tones; Sport = "angrier red
  tones," gear moves to center; **Track = the tach becomes a horizontal bar/strip across the top**,
  closest to the line of sight, with a huge central gear number and speed just below. BMW M Sport mode
  floods the cluster red; AMG "Supersport" adds a shift-up prompt. The blue→red color-temperature shift
  by drive mode is a strong, feasible idea for a configurable overlay.
- **Shift lights** — the F1 idiom: a horizontal LED strip (commonly ~15 LEDs) filling progressively,
  **green → red → blue/violet**, with the top stage **flashing** at the optimal shift point so it is
  caught in peripheral vision (Leo Bodnar SLI-F1; Fanatec replicas; MoTeC C125/C127's 10 programmable
  LEDs). Production strips run **green → yellow → orange → red** with the final reds strobing. We
  already compute `ShiftLightStage` — this maps directly.
- **Gear** — large central glyph (consider a 7-segment-style face for the motorsport feel) that
  **recolors/pulses at the shift cue**. **Speed** — clean digital number, smaller than gear/tach during
  hard driving (de-emphasized in the hierarchy).
- **Animation**: real needles are **damped** (ease toward target, ~80–150 ms, not snap); a one-shot
  **startup sweep** (needle to max and back + a left-to-right LED wipe) is an authentic intro flourish.
  Discrete LEDs snap on at thresholds; the final flash is a hard blink loop. Mixing eased analog motion
  with snappy LEDs matches how real clusters read.
  Sources: Porsche Newsroom tachometer history; Corvette C8 cluster guides; Wikipedia "Shift light";
  Leo Bodnar SLI-F1; Fanatec RevLEDs; MoTeC C125; CommunityToolkit RadialGauge.

### Tire temp/pressure, boost, power/torque, fluid temps
- **Tire silhouette**: a top-down car outline with four tire blocks, each split into **inner / middle /
  outer** bands, colored on a universal **blue (cold) → green (optimal) → orange (hot) → red
  (overheating)** scale. Optimal road-tire window ≈ **88–99°C (190–210°F)**. The IN/MID/OUT split
  encodes diagnostics (inner−outer spread → camber; hot middle → over-inflation) and is the distinctive
  detail most consumer overlays skip. OEM analogs: AMG Track Pace (per-corner temp + pressure), McLaren
  Cyber Tyre optimal-window alert, Porsche PCM TPMS (green/yellow/red).
- **Boost/turbo**: analog vacuum-to-boost sweep or a horizontal fill bar, both with a **persistent
  peak-hold marker** and **red past a threshold** (GT-R MFD "bar goes red past your redline";
  aftermarket AEM/P3 peak-hold). Cheap, readable, authentic.
- **Power/torque**: live HP/TQ fill bars, or the standout — a **dyno-style live curve** (HP & torque vs
  RPM as a `Polyline`) with a moving "current operating point" dot. The GT-R's **30-second rolling
  history graph** is a proven, cheap pattern.
- **Fluid temps**: 3-zone arc (blue cold → neutral → amber caution → red warning), reusing the same
  thermal scale as tires for cross-glance legibility.
  Sources: ACC tire widget convention; motorsportsraceparts / Elephant Racing IN-MID-OUT diagnostics;
  AMG Track Pace; Pirelli/McLaren; Porsche PCM TPMS; GT-R MFD (Jalopnik); AEM/P3 boost gauges.

### Modern EV / hypercar visual language and motorsport color codes
- **EV digital dashes** (Lucid, Taycan, Rivian, Tesla): near-black backgrounds, **one brand accent
  color**, flat layered translucent cards, heavy negative space, bespoke condensed sans typefaces.
- **Drive-mode ambient theming** is widespread and eye-catching: **Eco = green, Comfort = blue,
  Sport = red** (Genesis/Kia/Rivian recolor cluster + ambient light together; Rivian renders the car
  per mode and uses warm-day/cool-night palettes).
- **Motorsport timing colors** (the most transferable idea): **purple = session-fastest, green =
  personal best, yellow = slower than own best, white = standard.** For a live **predictive delta vs a
  reference lap**: a center-zero horizontal bar, **green when gaining / red when losing**, magnitude as
  bar width.
- **Warnings**: amber = caution, red = warning, with red states **pulsing/flashing** for peripheral
  attention.
- **Typography**: condensed sans (DIN-like; Saira is racing-oriented and free) with **tabular numerals**
  so live digits don't jitter; reserve boxy faces (Eurostile/Bank Gothic) for static labels.
  Sources: formtrends Lucid/Mercedes UI; Porsche Taycan HMI; Rivian VOS (Behance/The Drive); Genesis/Kia
  drive-mode theming; RacingNews365 / F1 Dictionary sector & delta colors; SimHub delta bar; MS
  RadialGauge; font references (FontAlternatives, PRINT Magazine, Saira).

## Per-metric translation

| Our metric | Real-car / motorsport reference | Concrete visual treatment to adopt | Source |
|---|---|---|---|
| **Speed** (`SpeedKmh`/`SpeedMph`) | Porsche central-tach (speed nested, secondary); Corvette Track mode (speed below gear) | Clean digital number with **tabular numerals**, smaller than gear/tach, placed below center; small unit label | [Porsche tach history](https://newsroom.porsche.com/en/2026/history/porsche-icon-christophorus-417-tachometer-41697.html); [C8 cluster](http://lovingmycars.blogspot.com/2019/09/your-guide-to-c8-corvettes-digital.html) |
| **RPM / `RpmFraction`** | Porsche central rev counter; AMG/BMW M track tach; Corvette mode-dependent tach | **Central arc tach** with a green **optimal/power band** + red **redline zone** as colored arc segments; **eased fill** (~100 ms, CubicEase), not snap | [Porsche tach](https://newsroom.porsche.com/en/2026/history/porsche-icon-christophorus-417-tachometer-41697.html); [Drive — analog tach](https://www.thedrive.com/news/porsche-had-pretty-intense-discussion-over-finally-axing-analog-tachometer) |
| **`ShiftLightStage`** | F1 rev-light strip; Leo Bodnar SLI-F1; MoTeC C125; production GT3/Corvette/AMG | Top **horizontal LED strip**, fill green→amber→red by RPM%, final stage **flashes blue/white (or red)** at optimal shift; per-gear thresholds optional | [Leo Bodnar SLI-F1](https://www.leobodnar.com/shop/index.php?main_page=product_info&products_id=184); [Shift light (Wikipedia)](https://en.wikipedia.org/wiki/Shift_light); [Fanatec RevLEDs](https://www.fanatec.com/us/en/explorer/products/steering-wheel/customizing-revleds-for-fanatec-steering-wheels/) |
| **Gear** | Corvette Track (gear big, dead-center); SLI-F1 7-segment gear; Ferrari/Porsche nested | **Large central glyph** (7-segment-style font); **recolor/pulse** when in the shift-flash zone | [C8 cluster](http://lovingmycars.blogspot.com/2019/09/your-guide-to-c8-corvettes-digital.html); [SLI-F1](https://www.leobodnar.com/shop/index.php?main_page=product_info&products_id=184) |
| **G-force** (`LatG`/`LongG`) | Friction circle (VRS); Porsche Sport Chrono peak-g; Corvette PDR; GT-R/GT aesthetic; G-Force Meter+ | **Traction circle**: red 1.0 g boundary ring + faint 0.5 g ring + crosshairs; **thin tether** center→dot; **short fading trail**; dot **green→red near the limit**; **glowing peak-hold** marker reset per session | [Traction circle](https://vrperfdev.wordpress.com/2016/01/01/traction-circle-g-g-diagram-explained/); [Porsche Sport Chrono](https://www.dubizzle.com/blog/cars/porsche-sport-chrono-package/); [G-Force Meter+](https://apps.apple.com/us/app/g-force-meter-accelerometer/id6760181285) |
| **Throttle / brake / clutch** | MoTeC/AMG combined input visualization; Corvette PDR throttle/brake | Keep eased bars; consider **green/red threshold tint** at full-throttle/threshold-braking; pair with the g-trail to read trail-braking | [Corvette PDR](https://www.edmunds.com/car-reviews/features/2015-chevrolet-corvette-performance-data-recorder-pdr.html); [AMG Track Pace](https://www.mercedes-amg.com/en/amg-track-pace) |
| **Steering** (`SteerFraction`) | OEM track displays; PDR steering-angle gauge | Center-anchored arc/indicator that **eases**; subtle to keep hierarchy below tach/gear | [Corvette PDR](https://www.edmunds.com/car-reviews/features/2015-chevrolet-corvette-performance-data-recorder-pdr.html) |
| **Boost** (`Boost`) | GT-R MFD boost bar (red past redline); AEM/P3 peak-hold gauges | Horizontal fill bar (or arc sweep) with a **persistent peak-hold marker** and **red past a threshold** | [GT-R MFD](https://www.jalopnik.com/the-r34-gt-rs-multifunction-display-was-secretly-its-co-1845687935/); [AEM X-Series](https://subimods.com/products/aem-x-series-35-psi-boost-gauge-52mm) |
| **Power / torque** (`PowerHp`/`TorqueLbFt`) | Dyno apps (HP/TQ curves); GT-R 30-s rolling graph; PDR live data | Live HP/TQ **fill bars**; standout = **dyno-style live curve** vs RPM with a moving current-point dot; optional **30-s rolling mini-graph** | [Dyno graph](https://www.cjponyparts.com/resources/how-to-read-a-dyno-graph); [GT-R MFD](https://www.jalopnik.com/the-r34-gt-rs-multifunction-display-was-secretly-its-co-1845687935/) |
| **Fuel** (`FuelPercent`) | OEM low-fuel amber warning; cluster fuel arc | Arc/bar that turns **amber** at a low threshold; reuse the shared thermal/warning palette | [Coolant/fuel warnings](https://www.edenmotorgroup.com/latest-news/what-does-the-engine-coolant-warning-light-mean/) |
| **Lap timing** (`BestLap`/`LastLap`/`CurrentLap`/`RacePosition`) | F1 sector/delta colors; SimHub predictive delta | **Purple/green/yellow/white** for best/PB/slower/standard; **center-zero delta bar** green(faster)/red(slower) vs reference; flash green on a new best; tabular numerals | [F1 sector colors](https://racingnews365.com/what-sectors-are-f1-and-what-do-the-different-colours-mean); [Delta](https://www.formula1-dictionary.net/delta.html) |
| **World position** (`PositionX/Z`, minimap) | EV nav/map cluster modes (Taycan Map/Full Map) | Dark map with one accent for the car dot; keep within the dark+accent system | [Taycan HMI](https://www.hmi.gallery/hmi/porsche-taycan-hmi-design) |
| **Tire temp/pressure** *(needs plumbing)* | Tire silhouette (ACC/AMG/GT-R); IN-MID-OUT motorsport temps; Porsche PCM TPMS | Top-down **car silhouette**, 4 tire blocks each split **IN/MID/OUT**, colored **blue→green→orange→red** (~88–99°C optimal); pressure as small numeric per corner | [ACC tire widget](https://simracingsetup.com/assetto-corsa/acc-tyre-pressure-guide/); [IN-MID-OUT](https://www.motorsportsraceparts.com/how-to-read-a-tires-temperature-profile/); [AMG Track Pace](https://www.mercedes-amg.com/en/amg-track-pace) |
| **Global theme / drive mode** | EV ambient theming (Genesis/Kia/Rivian); Corvette mode re-theme; EV dark+accent | **Near-black bg + one swappable accent brush** via `DynamicResource`, keyed to a mode (Eco green / Comfort blue / Sport red); amber/red **pulsing** warnings | [Genesis ambient](https://genesisowners.com/genesis-forum/threads/2026-ambient-lighting-link-to-drive-mode-customization.56963/); [Lucid/MB UI](https://www.formtrends.com/user-interface-lucid-air-vs-mercedes-benz-s-class/) |

## Most compelling, distinctive, WPF-feasible treatments

Ordered by "real-car wow" per unit of effort. All map to fields we already expose unless noted.

1. **G-force friction circle: tether + fading trail + glowing peak-hold dot.** The most information-
   dense, most "alive" element and exactly what the user described. Static rings/crosshairs drawn once
   (`Ellipse` + `Line`); dot via `TranslateTransform`; trail as a capped (~30–60 pt) `Polyline` with a
   gradient fade; peak-hold as a held `Ellipse` with a soft `DropShadowEffect`/`RadialGradientBrush`
   (use sparingly — the one effect that can cost frames). Lerp the dot per frame so it glides like the
   OEM displays.
2. **Top shift-light LED strip with a flashing optimal-shift stage.** Drives off `ShiftLightStage`. A
   horizontal `ItemsControl` of LED ellipses, green→amber→red, with a `Storyboard` blink on the final
   stage. The single most faithful, recognizable motorsport cue; trivial in WPF.
3. **Central arc tachometer with a green power band + red redline, eased fill.** The Porsche idiom.
   `Path`/`ArcSegment` for the arc, a segmented/gradient brush for the bands, an eased `DoubleAnimation`
   (~100 ms CubicEase) for the fill — light smoothing is the key to the premium feel at 60 Hz.
4. **Drive-mode ambient theming via a single swappable accent brush.** `DynamicResource` accent keyed
   to Eco/Comfort/Sport (green/blue/red) instantly reads "modern EV" for near-zero cost.
5. **Lap-delta center bar + F1 timing colors.** Center-zero `Border` with animated `Width`, green
   left / red right, plus purple/green/yellow/white lap-time labels with tabular numerals. High impact,
   low effort once a reference lap exists.
6. **Boost peak-hold marker + red-past-threshold.** A sticky tick on a fill bar; cheap and authentic.
7. **Startup sweep intro** (tach sweep to max and back + LED wipe) — an authentic branded flourish via
   one-shot `Storyboard` on overlay load.

Higher effort / aspirational (worth it but heavier): **tire silhouette with IN/MID/OUT thermal bands**
(needs per-wheel fields plumbed through `TelemetryReadout` first — the most eye-catching real-car idiom,
but gated on data); **dyno-style live power/torque curve**; the **Corvette-style mode morph** where the
tach changes geometry (dial ↔ top bar) by drive mode.

### Typography and palette baseline
- Condensed technical sans (DIN-like, or **Saira** — racing-oriented, free on Google Fonts) for labels;
  **tabular/monospaced numerals** for every live value (speed, RPM, G, lap/delta) so digits don't jitter.
- Near-black background, one accent brush, amber = caution / red = warning (pulsing). One shared
  **blue→green→orange→red** thermal scale reused across tires and fluid temps for cross-glance legibility.

## Sources

G-force / friction circle
- [Traction circle / g-g diagram explained (vrperfdev)](https://vrperfdev.wordpress.com/2016/01/01/traction-circle-g-g-diagram-explained/)
- [The Traction Circle (Virtual Racing School)](https://virtualracingschool.com/academy/iracing-career-guide/second-season/the-traction-circle/)
- [Porsche Track Precision App (Porsche Newsroom)](https://newsroom.porsche.com/en/innovation/engineering/porsche-track-precision-app-gt-sports-cars-11024.html)
- [Porsche Sport Chrono package (dubizzle)](https://www.dubizzle.com/blog/cars/porsche-sport-chrono-package/)
- [Corvette PDR (Edmunds)](https://www.edmunds.com/car-reviews/features/2015-chevrolet-corvette-performance-data-recorder-pdr.html)
- [GT-R multifunction display (Gizmodo)](https://gizmodo.com/nissan-gt-r-multifunction-display-looks-like-a-playstat-313871)
- [G-Force Meter+ (App Store)](https://apps.apple.com/us/app/g-force-meter-accelerometer/id6760181285)

Tachometer / shift lights / gear / speed
- [Porsche tachometer history (Newsroom)](https://newsroom.porsche.com/en/2026/history/porsche-icon-christophorus-417-tachometer-41697.html)
- [Porsche analog tachometer discussion (The Drive)](https://www.thedrive.com/news/porsche-had-pretty-intense-discussion-over-finally-axing-analog-tachometer)
- [C8 Corvette digital cluster guide](http://lovingmycars.blogspot.com/2019/09/your-guide-to-c8-corvettes-digital.html)
- [Leo Bodnar SLI-F1 shift-light strip](https://www.leobodnar.com/shop/index.php?main_page=product_info&products_id=184)
- [Shift light (Wikipedia)](https://en.wikipedia.org/wiki/Shift_light)
- [Fanatec RevLEDs customization](https://www.fanatec.com/us/en/explorer/products/steering-wheel/customizing-revleds-for-fanatec-steering-wheels/)
- [MoTeC C125 dash logger](https://www.motec.com.au/c125/c125overview/)
- [Startup gauge sweep self-test (forum)](https://www.toyotanation.com/threads/gauge-sweep-on-start.1802201/)

Tire / boost / power / temps
- [ACC tyre pressure/temp widget colors](https://simracingsetup.com/assetto-corsa/acc-tyre-pressure-guide/)
- [Reading a tire temperature profile (IN/MID/OUT)](https://www.motorsportsraceparts.com/how-to-read-a-tires-temperature-profile/)
- [Reading tire temperature (Elephant Racing)](https://www.elephantracing.com/tech-topics/reading-tire-temperature/)
- [Mercedes-AMG Track Pace](https://www.mercedes-amg.com/en/amg-track-pace)
- [R34 GT-R multifunction display (Jalopnik)](https://www.jalopnik.com/the-r34-gt-rs-multifunction-display-was-secretly-its-co-1845687935/)
- [Porsche PCM TPMS warnings (pcarwise)](https://www.pcarwise.com/local-help/porsche-common-problems/porsche-dashboard-messages-symbols/tire-pressure-porsche-dashboard-warnings/)
- [AEM X-Series boost gauge (peak hold)](https://subimods.com/products/aem-x-series-35-psi-boost-gauge-52mm)
- [How to read a dyno graph (CJ Pony Parts)](https://www.cjponyparts.com/resources/how-to-read-a-dyno-graph)
- [Engine coolant/fuel warning thresholds (Eden Motor Group)](https://www.edenmotorgroup.com/latest-news/what-does-the-engine-coolant-warning-light-mean/)

EV / hypercar UI, motorsport color codes, typography
- [Lucid Air vs Mercedes UI (formtrends)](https://www.formtrends.com/user-interface-lucid-air-vs-mercedes-benz-s-class/)
- [Porsche Taycan HMI (hmi.gallery)](https://www.hmi.gallery/hmi/porsche-taycan-hmi-design)
- [Rivian Vehicle OS (Behance)](https://www.behance.net/gallery/166386511/Rivian-Vehicle-OS)
- [Genesis ambient lighting / drive-mode theming](https://genesisowners.com/genesis-forum/threads/2026-ambient-lighting-link-to-drive-mode-customization.56963/)
- [F1 sector colors explained (RacingNews365)](https://racingnews365.com/what-sectors-are-f1-and-what-do-the-different-colours-mean)
- [Delta time (F1 Dictionary)](https://www.formula1-dictionary.net/delta.html)
- [CommunityToolkit RadialGauge (animated transitions)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/windows/radialgauge)
- [Automotive font alternatives (DIN/Saira/Eurostile)](https://fontalternatives.com/best-fonts/automotive/)
