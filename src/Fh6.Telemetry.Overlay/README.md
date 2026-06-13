# Fh6.Telemetry.Overlay

Transparent, always-on-top WPF HUD that renders live FH6 telemetry over the game. Reuses
`Fh6.Telemetry.Core` for decoding.

## Run
```bash
# Live (game running, Data Out -> 127.0.0.1:<port>)
dotnet run --project src/Fh6.Telemetry.Overlay

# Dev without the game: replay a capture
dotnet run --project src/Fh6.Telemetry.Overlay -- --replay capture-XXXX.jsonl --speed 2
```

Flags: `--replay <file>`, `--speed N`, `--port <n>`, `--layout BottomStrip|CornerPanel|CenterDash`, `--opacity <0.2..1>`.

## Hotkeys (global)
- **F8** — toggle edit mode (disables click-through so you can drag the overlay; re-lock to save position)
- **F9** — open settings (port, listen address, layout, opacity)
- **F10** — cycle layout preset

## Requirements / caveats
- Run FH6 in **Borderless** or **Windowed** mode. Exclusive fullscreen bypasses the Windows
  compositor (DWM), so the overlay will not appear over it.
- Forza is a Microsoft Store/UWP app; if no data arrives, allow UDP loopback for the game.
- Config persists at `%AppData%/fh6-overlay/config.json`.

## Layout presets
Bottom strip (default), corner panel, center race-dash — the same widgets arranged
differently. Per-widget drag and color/size customization are planned post-MVP.

## Widgets (v1)
Gear, speed, RPM + shift lights, throttle/brake/clutch pedals + steering, lap timing
(current/best + last shift), boost + fuel. All decoded fields are available in
`Fh6.Telemetry.Core` for adding more widgets later.
