# FH6 Telemetry

A .NET CLI for working with Forza Horizon 6 "Data Out" UDP telemetry: capture, replay,
and a live refreshing dashboard. Built to grow toward overlays and an AI/ML self-driving
model later, so keep the packet parser and data pipeline reusable and well-isolated.

## Working agreement

### Prompt handling
The user provides the *idea*; Claude owns the prompt engineering. At the start of each
turn, rewrite the user's request into a clear, well-structured prompt (state goal,
constraints, and acceptance criteria), then execute that prompt. Surface the refined
prompt briefly so the user can correct intent.

### Keep the work human
Do not leave an AI/LLM footprint in the git history or the code:
- No AI attribution in commits: no "Generated with…", no `Co-Authored-By` AI trailers, no tool/model names.
- Write commit messages as a developer would: concise, imperative, explaining the change.
- No AI-tell comments (e.g. "As an AI…", boilerplate explanations of obvious code, decorative emoji).
- Match the surrounding code's style, naming, and comment density.

### Git workflow
- `main` stays green: it builds and all tests pass at every commit.
- Do work on short-lived feature branches off `main`, named by intent
  (`feat/parser-core`, `feat/cli-capture`, `docs/...`, `fix/...`). Don't commit features directly to `main`.
- Small, focused commits with imperative messages; commit logically separable units separately.
- Merge back to `main` with `--no-ff` once the unit is complete and tests pass, then delete the branch.
- Remove temporary/throwaway features (e.g. the capture coverage tracker) on their own branch once no longer needed.

## Key facts
- Packet: fixed **324 bytes**, **little-endian**. Documented fields occupy bytes 0–322;
  byte 323 is trailing padding (4-byte alignment). See `FH6_DATA_OUT_DOC.md`.
- Default UDP port: **20440** (configurable in-game under SETTINGS > HUD AND GAMEPLAY).
- Data is sent only while actively driving — not in menus, pauses, replays, or rewinds.
- Dev workflow uses recorded captures (replay), not a running game.

## Stack
- .NET 8 console app, C#. Dashboard + CLI via Spectre.Console.
- WPF overlay (`Fh6.Telemetry.Overlay`, `net8.0-windows`) reusing the Core parser/sources.
- Tests: xUnit, with golden-value assertions against trimmed capture fixtures.

## Overlay (WPF) gotchas — read before changing the overlay
- **Bound view-model properties MUST have a public setter.** WPF binds `RangeBase.Value`
  (ProgressBar) and `Run.Text` TwoWay by default, so a get-only / `private set` property
  crashes the window at startup ("A TwoWay or OneWayToSource binding cannot work on the
  read-only property…"). This has regressed 3×. Keep `{ get; set; }` (raise change
  notifications via helpers, not by removing the setter), or set `Mode=OneWay` on the binding.
- **Always RUN the overlay after VM/XAML changes — tests don't catch XAML/binding errors.**
  `dotnet run --project src/Fh6.Telemetry.Overlay` (live) or `-- --replay <file>`. To verify
  headless, launch the exe and capture the screen via PowerShell `System.Drawing.CopyFromScreen`.
- Text under an `Effect` (e.g. DropShadowEffect) loses ClearType and looks blurry — keep
  effects off text; the window sets `TextFormattingMode=Display`.
