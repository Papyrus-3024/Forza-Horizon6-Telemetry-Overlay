# FH6 Telemetry Overlay — QoL / Feature-Gap Research (V2 planning)

Date: 2026-06-13
Status: Research (no code changes). Web-sourced; cross-checked against what our overlay already does.

## Purpose

We already have a composable, free-drag widget overlay with named layouts, a live multi-series
chart, per-widget customization, replay, and a season-aware map being backlogged. This doc asks:
**what do Forza Horizon telemetry users actually ask for that existing tools (free + paid) do
poorly or not at all — and which of those are genuinely additive beyond what we already cover?**

It is a prioritized menu for V2, not an implementation plan.

## Honest read on signal strength (read this first)

Forza Horizon is arcade-leaning. Serious "telemetry analysis" demand is **modest and niche**
versus true sims (iRacing/AC/ACC). Most public discussion is about **HUD overlays and tuning
aids**, not deep analysis. Reddit threads were hard to surface directly (search engines kept
returning GitHub/app-store listings instead of Reddit posts; some forum pages 403'd), so much of
the evidence below leans on **GitHub issue trackers, official/SimHub/RealDash forums, and product
pricing pages** rather than upvoted Reddit wishlists. There is **no smoking-gun "top 10 most-wanted
Forza features" artifact** anywhere. Where signal is thin, it is labelled thin — do not treat thin
items as user-demand-driven.

Two hard constraints from the packet itself (bound everything below):
- FH "Data Out" carries vehicle dynamics, tire data, race status, and player inputs — **no
  track/season identity and no map tiles**. Season/track awareness must be *inferred*. ([Forza
  Support — FH6 Data Out](https://support.forza.net/hc/en-us/articles/51744149102611-Forza-Horizon-6-Data-Out-Documentation))
- **Nothing is sent in menus, pauses, replays, or rewinds.** Any "replay a session" feature must
  log continuously while driving (which our `capture` already does).

---

## Prioritized QoL table

Effort: **S** ≈ a few hours, **M** ≈ a focused feature, **L** ≈ multi-day / persistence /
calibration. "Have it?" reflects current code + approved backlog (map = backlogged, not shipped).

| # | QoL feature | Who wants it / evidence (source) | Have it? | Effort | Recommendation |
|---|---|---|---|---|---|
| 1 | **One-click / auto-diagnosed UWP loopback + port setup** | The single most-cited Forza-PC pain across every ecosystem. SimHub author confirms store games are sandboxed and can't reach localhost; users hit unrecoverable states ([SimHub forum](https://www.simhubdash.com/community-2/simhub-support/forza-udp-not-working/), [SimHub wiki](https://github.com/SHWotever/SimHub/wiki/SimHub-Basics----Games-config-and-troubleshooting)). Half of richstokes/austinbaccus GitHub issues are "can't connect / no data" ([richstokes issues](https://github.com/richstokes/Forza-data-tools/issues), [austinbaccus issues](https://github.com/austinbaccus/forza-telemetry/issues)). Svxy lists "port selection" as a TODO ([Svxy](https://github.com/Svxy/FH5-Telemetry)). | **No** (we document loopback in README; no in-app helper) | M | **V2** — highest-confidence win |
| 2 | **Correct, current FH6 packet decoding + "is data arriving?" diagnostics** | SimHub currently mis-decodes FH6: 128–140 packets/s arrive but the dash updates every ~10 s, suspected FH6-vs-FH5 packet-format drift ([SimHub #2267](https://github.com/SHWotever/SimHub/issues/2267)). FH6 SimHub support is immature (manual screen selection, no presets) ([Sportskeeda](https://tech.sportskeeda.com/gaming-news/how-use-simhub-forza-horizon-6)). | **Yes** (Core is a purpose-built FH6 324-byte parser) — but we lack an in-overlay "receiving N pkt/s on port X" indicator | S | **V2** — cheap diagnostics on top of an existing strength |
| 3 | **Lap-delta / sector "where am I losing time" vs best lap** | Convergent across paid tools: fh6.tech sector splits + gain/loss vs best, FH6 Tech lap-delta, Race Dash / Racing View delta. When multiple paid products all build the same thing, demand is real ([fh6.tech](https://fh6.tech/)). Absent in every free/OSS tool surveyed. Note Horizon's open-world makes "laps" fuzzy outside circuit events. | **Partial** — chart spec defers lap/distance-keyed delta as the heavy `HH` case; derived-metrics doc ranks "lap delta vs best" as item 6 (L) | L | **V2 (scoped)** — strongest *analysis* differentiator; ship circuit-event delta first |
| 4 | **Telemetry-driven tuning aids (gear ratios, suspension)** | Most-monetized analysis category: ForzaLabs paywalls gear-ratio/launch calc + tuning calculator ([ForzaLabs](https://forza.labsgg.com/join-premium)); fh6.tech/FH6 Tech auto-tune springs/gearing/aero/diff per surface+goal; ForzaTune guides. Users on Forza forums want suspension telemetry "directly in the center of your FOV" for tuning ([Forza forums](https://forums.forza.net/t/simhub-pedal-telemetry-overlay/768096)). austinbaccus #64 "understanding tire temperatures" = users want help *interpreting* values. | **Partial** — derived-metrics doc has gear-ratio/powerband material; no tuning-advice widget | M–L | **V2 (start small)** — a gear-ratio readout (S) is a quick win; full auto-tune is L/skip |
| 5 | **Season-aware track/route map + minimap** | Multi-year unmet demand: "people have been asking for [zoomable map] in FH4 for 3+ years" ([Steam](https://steamcommunity.com/app/1551360/discussions/0/4328520278449912637/)). The community SimHub GPS map exists *because* FH lacks one, and is fragile (breaks on updates, pixelates on 2nd monitor, mobile zoom unreliable) ([SimHub GPS maps](https://www.simhubdash.com/community-2/dashboard-templates/forza-horizon-gps-maps-for-simhub/)). jasperan builds track auto-mapping from position ([jasperan](https://github.com/jasperan/forza-horizon-5-telemetry-listener)). **Season awareness specifically is absent everywhere.** | **Yes (backlogged)** — our MapWidget + MapCalibration + season images directly target this | L | **V2** — finish the branch; season-awareness is a genuine unique angle |
| 6 | **Interpreted "smart hints" / coaching from telemetry** | ONYX Drive HUD "Smart Hints" (high rear-axle slip, excess throttle in corner); FH6 Radial "driving score" (grip/stability/line/inputs); jasperan heuristic + optional LLM tips. Appetite for *interpreted* telemetry, not raw numbers ([ONYX](https://vgtimes.com/games/forza-horizon-6/files/94764-onyx-drive-hud-telemetry-overlay.html)). | **Partial** — derived-metrics doc has the inputs (slip, balance, traction circle); no hint engine | M | **Later** — high ceiling but moderate signal; build on derived metrics once those ship |
| 7 | **CSV / data export + replayable capture** | OSS tools center on this (richstokes, austinbaccus, Svxy). But complaints are about *completeness* when present (austinbaccus #69 missing values, #67 no tire wear) more than absence. Enthusiast niche, not mass demand. | **Partial** — `fh6 capture` records JSONL + `replay`; no CSV/analysis export | S | **V2 (small)** — add `export csv` to the CLI; cheap, closes a known gap |
| 8 | **Car identity (ordinal → car name) on telemetry** | austinbaccus #55 "add ordinal/car name" — users want to know *which car* the data is from ([issue](https://github.com/austinbaccus/forza-telemetry/issues/55)). Needs an ordinal→name lookup table. | **No** | S–M | **Later** — nice context; needs/maintains a car-ordinal table |
| 9 | **Native-looking HUD that doesn't look "third-party"** | Forza-forum complaint that overlays "look a little out of place… noticeably third-party," plus SimHub font-shadow artifacts ([Forza forums](https://forums.forza.net/t/simhub-pedal-telemetry-overlay/768096)). | **Partial** — we have per-widget theming/customization; "native FH look" is a design choice | S–M | **Later** — polish item; address via a default FH-style theme |
| 10 | **Controller / wheel + pedal input visualization** | A *satisfied* use case, not a pain point — SimHub pedal overlay called "very handy," users build their own ([Forza forums](https://forums.forza.net/t/simhub-pedal-telemetry-overlay/768096)). ForzaDash already shows steering+pedals. | **Yes** — `PedalsSteerWidget` (throttle/brake/clutch + steering) | — | **Skip** — already covered, demand already met |
| 11 | **Free widget arrangement vs fixed presets** | SimHub's whole custom-layout ecosystem implies it; one user confused by Dashboards/Layouts/Overlays mental model ([SimHub forum](https://www.simhubdash.com/community-2/simhub-support/dashboards-dashboard-layouts-overlays-and-overlay-layouts/)). **No explicit "let me freely arrange" complaint found.** | **Yes** — free per-widget drag + named layouts is our core model | — | **Skip** — already a differentiator; nothing to add |
| 12 | **Multi-monitor / HDR support** | Only ever surfaced as a *bug* (SimHub GPS map pixelates on 2nd monitor), never an articulated wishlist. No Forza-specific HDR complaint found anywhere. | **No** | M–L | **Skip / Later** — evidence too thin to prioritize |
| 13 | **Drift scoring** | Appears as a tuning *goal* in auto-tuners (fh6.tech drift mode, FH6 Radial drift angle) but **no standalone "drift scoring" wishlist demand found.** | **Partial** — derived-metrics has drift/slip angle | S | **Later** — cheap if built on existing slip-angle metric, but demand unproven |
| 14 | **Resolution/DPI scaling robustness** | Recurring *bug* across OSS tools: austinbaccus hardcoded 1080p and misaligns; ForzaDash tested only at 1280×800 ([austinbaccus](https://github.com/austinbaccus/forza-telemetry), [ForzaDash](https://github.com/himanshupapola/ForzaDash)). | **Likely OK** — WPF DPI-aware + free-drag avoids hardcoded coords | S | **V2 (verify)** — confirm we scale cleanly at non-1080p; cheap insurance |

---

## High-confidence quick wins (do early in V2)

These have the strongest evidence-to-effort ratio:

1. **In-app connection diagnostics + UWP loopback helper** (#1, #2). The #1 cross-ecosystem pain is
   "no data arriving." A visible "receiving N packets/s on port X" indicator plus a one-click
   `CheckNetIsolation LoopbackExempt` helper (or a clear, copy-pasteable diagnostic) would directly
   beat the most common wall. Our FH6 parser correctness is already an advantage over SimHub's
   currently-broken FH6 decode — surface it.
2. **CSV export from the CLI** (#7). We already capture JSONL + replay; an `export csv` command is a
   few hours and closes a gap that the entire OSS cluster is built around.
3. **Gear-ratio readout widget** (subset of #4). The single most-monetized tuning aid; the
   derived-metrics doc already has the math. A read-only widget is S effort.
4. **Verify non-1080p / DPI scaling** (#14). Cheap insurance against the exact bug that plagues the
   OSS competitors; our free-drag model probably already avoids it.

## Nice-to-have (V2 if capacity, else later)

- **Lap-delta vs best, circuit-events first** (#3) — strongest analysis differentiator, but L and
  fuzzy in open-world; scope to circuit/route events initially.
- **Finish the season-aware map branch** (#5) — genuine multi-year unmet demand; our season angle
  is unique. Already backlogged.
- **Smart hints / coaching layer** (#6) — high ceiling, moderate signal; build atop derived metrics
  once those land.
- **Car name from ordinal** (#8), **native FH-style default theme** (#9), **drift score** (#13).

## Explicitly skip (don't build on a demand premise)

- **Input/pedal/steering display** (#10) — already shipped *and* community demand already met.
- **Free widget arrangement** (#11) — already our core strength; no unmet ask.
- **HDR / multi-monitor** (#12) — evidence is a single bug report, not a wishlist.

---

## How much of this is genuinely NEW for us?

Of 14 distinct items: **~5 are net-new** (loopback/diagnostics helper, CSV export, gear-ratio/
tuning aids, car-name lookup, smart-hints engine), **~4 are partial/extend existing work**
(lap-delta extends the chart+derived-metrics specs, season map is backlogged-not-shipped, native
theme extends customization, drift extends slip metrics), and **~5 we already cover or should skip**
(input display, free arrangement, multi-monitor/HDR, plus our existing parser/replay strengths).

The honest headline: **our composable-widget + free-layout + chart + replay foundation already
covers most of what the community complains is missing in preset-locked tools.** The real V2 wedge
is the *boring-but-painful* setup/diagnostics layer (#1/#2) and the *analysis/tuning* layer
(lap-delta, gear ratios, smart hints) that competitors lock behind paywalls — not more gauges.

## Confidence statement

- **High confidence:** setup/loopback friction and FH6-decode immaturity are real and well-evidenced
  (multiple independent sources, active GitHub issues). Lap-delta and tuning-aid demand is strongly
  evidenced by convergent paid products. Map/minimap demand is multi-year and proven by a buggy
  community workaround.
- **Low confidence / thin signal:** drift scoring, HDR/multi-monitor, native-look, "free
  arrangement" — treat as opinion-led, not demand-led.
- **Could not verify:** substantive Reddit threads (search limitations); a ranked community
  feature-request list (none exists publicly). Findings are not padded to fill that gap.

## Sources

- Forza Support — FH6 Data Out documentation: https://support.forza.net/hc/en-us/articles/51744149102611-Forza-Horizon-6-Data-Out-Documentation
- SimHub forum — "Forza UDP not working": https://www.simhubdash.com/community-2/simhub-support/forza-udp-not-working/
- SimHub wiki — games config & troubleshooting: https://github.com/SHWotever/SimHub/wiki/SimHub-Basics----Games-config-and-troubleshooting
- SimHub issue #2267 — FH6 slow updates / packet format: https://github.com/SHWotever/SimHub/issues/2267
- SimHub forum — Dashboards/Layouts/Overlays confusion: https://www.simhubdash.com/community-2/simhub-support/dashboards-dashboard-layouts-overlays-and-overlay-layouts/
- SimHub community — Forza Horizon GPS Maps: https://www.simhubdash.com/community-2/dashboard-templates/forza-horizon-gps-maps-for-simhub/
- Sportskeeda — How to use SimHub with FH6: https://tech.sportskeeda.com/gaming-news/how-use-simhub-forza-horizon-6
- Forza forums — SimHub pedal telemetry overlay: https://forums.forza.net/t/simhub-pedal-telemetry-overlay/768096
- richstokes/Forza-data-tools (repo + issues): https://github.com/richstokes/Forza-data-tools / https://github.com/richstokes/Forza-data-tools/issues
- austinbaccus/forza-telemetry (repo + issues incl. #55, #64, #67, #69): https://github.com/austinbaccus/forza-telemetry / https://github.com/austinbaccus/forza-telemetry/issues
- himanshupapola/ForzaDash: https://github.com/himanshupapola/ForzaDash
- jasperan/forza-horizon-5-telemetry-listener: https://github.com/jasperan/forza-horizon-5-telemetry-listener
- Svxy/FH5-Telemetry: https://github.com/Svxy/FH5-Telemetry
- RealDash forum — Forza Horizon section: https://forum.realdash.net/c/racing-simulators/forza-horizon-4-5/53
- RealDash supported connections: https://realdash.net/support.php
- Z1 Analyzer channels/import: https://www.z1simwheel.com/analyzer/channels.cfm
- OverTake — Z1 software thread: https://www.overtake.gg/threads/z1-software-z1-dashboard-z1-analyzer-z1-designer.201044/
- ForzaLabs premium: https://forza.labsgg.com/join-premium
- fh6.tech: https://fh6.tech/
- FH6 Radial HUD (Gumroad): https://quentindanblon.gumroad.com/l/fh6-forza-horizon-6-overlay
- ONYX Drive HUD: https://vgtimes.com/games/forza-horizon-6/files/94764-onyx-drive-hud-telemetry-overlay.html
- Steam — FH map zoom request thread: https://steamcommunity.com/app/1551360/discussions/0/4328520278449912637/
- ForzaTune tuning guide: https://forzatune.com/guide/the-fully-updated-forza-tuning-guide/
