# Fh6.Telemetry.Core

Reusable, UI-free library for Forza Horizon 6 "Data Out" telemetry. This is the foundation
the CLI (and future overlays / AI work) build on, so it has no third-party dependencies.

## What's here
- `PacketParser` / `TelemetryPacket` — decode a 324-byte little-endian UDP packet (323
  documented bytes + 1 alignment pad) into a strongly-typed struct. `SpanReader` does the
  sequential little-endian reads.
- `Vec3`, `Wheels`, `WheelsInt` — value types for XYZ triples and per-wheel (FL/FR/RL/RR) data.
- `ITelemetrySource` + `CaptureFrame` — a stream of raw frames. Implementations:
  `UdpTelemetrySource` (live) and `JsonlReplaySource` (capture file).
- `JsonlCaptureWriter` — buffered `{t,len,b64}` JSONL writer (avoids dropping datagrams).

## Usage
```csharp
foreach (var frame in new JsonlReplaySource("capture.jsonl").Frames())
    if (PacketParser.TryParse(frame.Data, out var packet))
        Console.WriteLine(packet.Speed);
```

See `FH6_DATA_OUT_DOC.md` at the repo root for the wire format.
