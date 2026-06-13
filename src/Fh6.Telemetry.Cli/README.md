# Fh6.Telemetry.Cli

The `fh6` command-line app. Holds only the user-facing layer (Spectre.Console dashboard +
argument parsing); all telemetry logic lives in `Fh6.Telemetry.Core`.

## Commands
- `fh6 capture [-p|--port 20440] [-o|--out <file>]` — record live UDP telemetry to JSONL.
- `fh6 replay <file> [-s|--speed N] [-l|--loop]` — replay a capture to the dashboard
  (the main dev path; no running game required).
- `fh6 live [-p|--port 20440]` — live dashboard from UDP.
- `fh6 coverage <file>` — report telemetry-condition coverage of a capture (temporary).

## Run
```bash
dotnet run --project src/Fh6.Telemetry.Cli -- replay capture-XXXX.jsonl --speed 5
```

The `replay` and `live` dashboards use a refreshing display and need a real terminal
(Windows Terminal, PowerShell, or cmd.exe), not a redirected/non-interactive shell.

## Dependencies
`Fh6.Telemetry.Core`, Spectre.Console, Spectre.Console.Cli.
