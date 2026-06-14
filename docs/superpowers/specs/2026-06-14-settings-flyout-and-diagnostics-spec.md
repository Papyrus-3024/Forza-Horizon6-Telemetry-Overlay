# Spec: Settings flyout + connection diagnostics (V2 round 2)

Date: 2026-06-14
Status: Approved; autonomous execution.

Covers the remainder of the approved round: **task 2** (F9 settings refactor + gear-icon
flyout) and the **task 5** QoL items, plus backlog updates. Tasks 3 (capture storage) and
4 (F8 drag-resize) are backlogged (`BACKLOG.md`). Already shipped this round: suspension
widget, CSV export.

## Execution mode

Sequential, single-worker, in the main loop. Rationale: every item touches the shared
overlay-registration files (`WidgetId`, `FreeLayout`, `LayoutSeeds`, the view model) and
each needs serial run-and-screenshot verification on the one dev desktop — parallel agents
would conflict and can't share the single overlay/screenshot pipeline. Each unit ships on
its own `feat/...` branch, verified, merged `--no-ff`, `main` kept green.

## Auto-decisions (forks resolved without asking)

1. **Gear-ratio widget → deferred to backlog.** A meaningful readout is HH per
   `derived-metrics-research.md §9.1` (needs `WheelRotationSpeed`, per-gear sample fitting
   while clutch≈0, tire-radius separation). A raw instantaneous `rpm/wheelSpeed` is noisy
   and unit-ambiguous. Shipping a misleading number is worse than not shipping; backlog it
   with the derivation notes.
2. **Settings UX = quick-flyout + restyled full panel, both in-overlay.** The gear icon
   opens an in-overlay flyout (hover-peek / click-pin). To avoid a risky big-bang port of
   all 219 lines of `SettingsWindow` logic, the flyout hosts the **reorganized full
   settings content** as an in-overlay panel; the separate modal `SettingsWindow` is
   retired. F9 toggles the pinned flyout (keeps the existing hotkey meaningful).
3. **Hover on a click-through window** is detected by polling the cursor position
   (`GetCursorPos`) on a ~60 ms `DispatcherTimer`. When the cursor enters the gear hotspot
   (screen rect), disable click-through so the icon/panel become interactive and peek the
   panel; rely on normal WPF mouse events thereafter. When not pinned and the cursor leaves
   the panel bounds, hide and re-enable click-through. Pinned state survives until the user
   unpins (gear click / close button / F9).
4. **Diagnostics:** the pump exposes a rolling packets/sec + total; the VM surfaces a
   `Diagnostics` string ("12.3 pkt/s : 20440") shown in the flyout. A `fh6 doctor` CLI
   command binds the port, samples ~4 s, reports pkt/s, and prints the UWP loopback fix.

## Acceptance

- Gear icon visible in a corner; hover peeks settings; click pins; pinned panel is fully
  interactive; closing restores click-through so gameplay clicks pass through.
- All current settings (port/address, layout, opacity, HUD scale, theme, chart, per-widget
  visibility/scale, saved layouts) reachable and Apply works as before.
- New widgets (suspension, arc-tach) appear in the widget list automatically.
- Overlay shows live pkt/s; `fh6 doctor` reports connection status + loopback guidance.
- 162+ tests green; overlay verified by run + screenshot.
