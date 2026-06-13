# Fh6.Telemetry.Tests

xUnit tests for `Fh6.Telemetry.Core`.

## Coverage
- `PacketParserTests` — `SpanReader` decode, golden-value parsing of real captured frames
  (driving + menu), and length validation.
- `JsonlTests` — JSONL replay reading and capture-writer round-trip.
- `CoverageTrackerTests` — coverage condition evaluation and first-frame tracking.

## Run
```bash
dotnet test
```
