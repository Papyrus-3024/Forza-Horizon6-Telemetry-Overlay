# Backlog

Deferred features with enough detail to pick up later. Keep `main` green; build each on
its own `feat/...` branch and merge `--no-ff` when done.

## Capture storage: binary capture + analysis export

**Goal:** replace/augment today's JSONL capture with a compact on-disk format, and an
analysis-friendly export.

**Context:** `fh6 capture` currently writes JSONL (one JSON object per packet). That is
bulky and slow to parse. We want (a) a compact capture for storage + replay and (b) an
export for offline analysis (the user works in pandas).

**Open decision (deferred):** which target —
- **Raw `.bin`**: timestamp + the exact 324-byte packet, concatenated. Smallest; our
  existing `PacketParser` replays it directly with no new dependency. Convert to
  CSV/Parquet later.
- **Parquet**: columnar, loads straight into pandas. Needs a dependency (`Parquet.NET`)
  and an explicit column schema mapped from `TelemetryReadout`.
- **Both** (recommended starting point): capture to `.bin`, add `fh6 export parquet`.

**Acceptance:** `capture` can write the chosen format; `replay` reads it back; if Parquet,
a one-command export produces a file that loads in pandas with sensible column names/types.
Note: `pandas` itself is Python and out of our .NET stack — we produce the file, not read it.

**Effort:** M.

## F8 edit mode: drag-corner resize per widget

**Goal:** in edit mode (F8), let each widget be resized by dragging a corner handle, not
just moved.

**Approach:** show corner grips on each widget while in edit mode; dragging a corner maps
to the widget's existing per-widget `Scale` (uniform — widgets are fixed-aspect designs),
clamped to the same 0.5–2.5 range Settings uses. Persist `Scale` in `WidgetConfig` on
exit (alongside the existing X/Y flush in `FreeLayout.FlushPositions`).

**Notes / gotchas:**
- The drag/hit-test in `FreeLayout` identifies widgets by reference as direct `Surface`
  children; resize grips must not break that (overlay the grips, route their drags
  separately from the move-drag).
- If independent width/height is ever wanted instead of uniform scale, most widgets would
  need layout review first; uniform `Scale` is the low-risk default.

**Acceptance:** in F8, dragging a widget corner resizes it smoothly; the new size persists
across restart; move-drag still works; click-through still restored on F8 exit.

**Effort:** M.
