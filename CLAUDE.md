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

## Key facts
- Packet: fixed **324 bytes**, **little-endian**. Documented fields occupy bytes 0–322;
  byte 323 is trailing padding (4-byte alignment). See `FH6_DATA_OUT_DOC.md`.
- Default UDP port: **20440** (configurable in-game under SETTINGS > HUD AND GAMEPLAY).
- Data is sent only while actively driving — not in menus, pauses, replays, or rewinds.
- Dev workflow uses recorded captures (replay), not a running game.

## Stack
- .NET 8 console app, C#. Dashboard + CLI via Spectre.Console.
- Tests: xUnit, with golden-value assertions against trimmed capture fixtures.
