# FH6 Telemetry CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A .NET CLI that captures, replays, and live-displays Forza Horizon 6 "Data Out" UDP telemetry, plus a temporary coverage tracker to validate manual-test captures.

**Architecture:** One solution, three projects. `Fh6.Telemetry.Core` (class lib) holds all testable logic: the packet parser, capture-file I/O, telemetry sources, and the coverage tracker. `Fh6.Telemetry.Cli` (console exe) holds only the Spectre.Console UI and command wiring. `Fh6.Telemetry.Tests` (xUnit) references Core. Both `replay` and `live` feed the same parser and dashboard via `ITelemetrySource`.

**Tech Stack:** C# / .NET 8, Spectre.Console + Spectre.Console.Cli (UI + arg parsing), xUnit. Packets are fixed 324-byte little-endian (323 documented + 1 alignment pad).

**Conventions (from CLAUDE.md):** Work on the feature branch named at each phase; small commits per task; merge to `main` with `--no-ff` at the end of each phase, then delete the branch. No AI footprint in commits or code.

---

## File Structure

```
P:/Projects/fh6-telemetry/
  FH6-Telemetry.sln
  src/
    Fh6.Telemetry.Core/
      Fh6.Telemetry.Core.csproj
      Vec3.cs                  # X/Y/Z float triple
      Wheels.cs                # FL/FR/RL/RR float + int variants with Any/All helpers
      TelemetryPacket.cs       # decoded packet (record struct)
      SpanReader.cs            # sequential little-endian reader (internal)
      PacketParser.cs          # bytes -> TelemetryPacket
      CaptureFrame.cs          # (timestampMs, raw bytes)
      ITelemetrySource.cs      # yields CaptureFrame
      JsonlReplaySource.cs     # reads {t,len,b64} JSONL
      JsonlCaptureWriter.cs    # buffered JSONL writer
      UdpTelemetrySource.cs    # live UDP listener
      Coverage/
        CoverageReport.cs      # report + item types
        CoverageTracker.cs     # condition evaluation
  src/Fh6.Telemetry.Cli/
      Fh6.Telemetry.Cli.csproj
      Program.cs               # CommandApp wiring
      IDashboard.cs
      SpectreDashboard.cs      # refreshing dashboard renderer
      Commands/
        CaptureCommand.cs
        ReplayCommand.cs
        LiveCommand.cs
        CoverageCommand.cs
  tests/Fh6.Telemetry.Tests/
      Fh6.Telemetry.Tests.csproj
      PacketParserTests.cs
      JsonlTests.cs
      CoverageTrackerTests.cs
```

---

## Phase 1 — Solution layout

Branch: `chore/solution-layout`

### Task 1: Create the clean solution structure

**Files:** removes the old nested `FH6-Telemetry/` (Hello-World only); creates root solution + three projects.

- [ ] **Step 1: Create the branch**

```bash
cd /p/Projects/fh6-telemetry
git checkout -b chore/solution-layout
```

- [ ] **Step 2: Remove the old nested solution (Hello-World starter, nothing to keep)**

```bash
rm -rf FH6-Telemetry
```

- [ ] **Step 3: Scaffold the solution and projects**

```bash
dotnet new sln -n FH6-Telemetry
dotnet new classlib -n Fh6.Telemetry.Core -o src/Fh6.Telemetry.Core -f net8.0
dotnet new console  -n Fh6.Telemetry.Cli  -o src/Fh6.Telemetry.Cli  -f net8.0
dotnet new xunit    -n Fh6.Telemetry.Tests -o tests/Fh6.Telemetry.Tests -f net8.0
dotnet sln add src/Fh6.Telemetry.Core src/Fh6.Telemetry.Cli tests/Fh6.Telemetry.Tests
dotnet add src/Fh6.Telemetry.Cli reference src/Fh6.Telemetry.Core
dotnet add tests/Fh6.Telemetry.Tests reference src/Fh6.Telemetry.Core
dotnet add src/Fh6.Telemetry.Cli package Spectre.Console
dotnet add src/Fh6.Telemetry.Cli package Spectre.Console.Cli
rm -f src/Fh6.Telemetry.Core/Class1.cs tests/Fh6.Telemetry.Tests/UnitTest1.cs
```

- [ ] **Step 4: Build to verify the solution is wired**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors (a "no tests" state is fine; the test project compiles).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Restructure into Core/Cli/Tests solution"
```

- [ ] **Step 6: Merge to main and delete branch**

```bash
git checkout main
git merge --no-ff chore/solution-layout -m "Add Core/Cli/Tests solution layout"
git branch -d chore/solution-layout
```

> Note: the old `FH6-Telemetry/FH6-Telemetry.sln` is gone. Open `FH6-Telemetry.sln` at the repo root in the IDE.

---

## Phase 2 — Core parser (TDD)

Branch: `feat/parser-core`  (create with `git checkout -b feat/parser-core`)

### Task 2: Value types (Vec3, Wheels, WheelsInt)

**Files:**
- Create: `src/Fh6.Telemetry.Core/Vec3.cs`
- Create: `src/Fh6.Telemetry.Core/Wheels.cs`

- [ ] **Step 1: Write Vec3**

```csharp
namespace Fh6.Telemetry.Core;

public readonly record struct Vec3(float X, float Y, float Z);
```

- [ ] **Step 2: Write Wheels + WheelsInt with Any/All helpers**

```csharp
namespace Fh6.Telemetry.Core;

public readonly record struct Wheels(float FrontLeft, float FrontRight, float RearLeft, float RearRight)
{
    public bool Any(Func<float, bool> predicate) =>
        predicate(FrontLeft) || predicate(FrontRight) || predicate(RearLeft) || predicate(RearRight);

    public bool All(Func<float, bool> predicate) =>
        predicate(FrontLeft) && predicate(FrontRight) && predicate(RearLeft) && predicate(RearRight);
}

public readonly record struct WheelsInt(int FrontLeft, int FrontRight, int RearLeft, int RearRight)
{
    public bool Any(Func<int, bool> predicate) =>
        predicate(FrontLeft) || predicate(FrontRight) || predicate(RearLeft) || predicate(RearRight);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Fh6.Telemetry.Core`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Add Vec3 and Wheels value types"
```

### Task 3: SpanReader (TDD)

**Files:**
- Create: `src/Fh6.Telemetry.Core/SpanReader.cs`
- Test: `tests/Fh6.Telemetry.Tests/PacketParserTests.cs` (start the file with the SpanReader test)
- Modify: `src/Fh6.Telemetry.Core/Fh6.Telemetry.Core.csproj` (expose internals to tests)

- [ ] **Step 1: Expose internals to the test project**

Add inside the `<Project>` element of `src/Fh6.Telemetry.Core/Fh6.Telemetry.Core.csproj`:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="Fh6.Telemetry.Tests" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing test**

Create `tests/Fh6.Telemetry.Tests/PacketParserTests.cs`:

```csharp
using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class PacketParserTests
{
    [Fact]
    public void SpanReader_reads_little_endian_sequentially()
    {
        // 0x01 as S32, then 2.0f as F32, then 0xAB as U8
        byte[] bytes = { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0xAB };
        var r = new SpanReader(bytes);

        Assert.Equal(1, r.S32());
        Assert.Equal(2.0f, r.F32());
        Assert.Equal(0xAB, r.U8());
        Assert.Equal(9, r.Position);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test --filter SpanReader_reads_little_endian_sequentially`
Expected: FAIL — `SpanReader` does not exist (compile error).

- [ ] **Step 4: Implement SpanReader**

Create `src/Fh6.Telemetry.Core/SpanReader.cs`:

```csharp
using System.Buffers.Binary;

namespace Fh6.Telemetry.Core;

internal ref struct SpanReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _pos;

    public SpanReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _pos = 0;
    }

    public int Position => _pos;

    public float F32()
    {
        var v = BinaryPrimitives.ReadSingleLittleEndian(_data.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public int S32()
    {
        var v = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public uint U32()
    {
        var v = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public ushort U16()
    {
        var v = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_pos, 2));
        _pos += 2;
        return v;
    }

    public byte U8() => _data[_pos++];

    public sbyte S8() => (sbyte)_data[_pos++];
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test --filter SpanReader_reads_little_endian_sequentially`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Add SpanReader for little-endian sequential decode"
```

### Task 4: TelemetryPacket

**Files:**
- Create: `src/Fh6.Telemetry.Core/TelemetryPacket.cs`

- [ ] **Step 1: Write the packet model (field order matches the wire layout)**

```csharp
namespace Fh6.Telemetry.Core;

public readonly record struct TelemetryPacket
{
    public int IsRaceOn { get; init; }
    public uint TimestampMs { get; init; }

    public float EngineMaxRpm { get; init; }
    public float EngineIdleRpm { get; init; }
    public float CurrentEngineRpm { get; init; }

    public Vec3 Acceleration { get; init; }
    public Vec3 Velocity { get; init; }
    public Vec3 AngularVelocity { get; init; }

    public float Yaw { get; init; }
    public float Pitch { get; init; }
    public float Roll { get; init; }

    public Wheels NormalizedSuspensionTravel { get; init; }
    public Wheels TireSlipRatio { get; init; }
    public Wheels WheelRotationSpeed { get; init; }
    public WheelsInt WheelOnRumbleStrip { get; init; }
    public WheelsInt WheelInPuddle { get; init; }
    public Wheels SurfaceRumble { get; init; }
    public Wheels TireSlipAngle { get; init; }
    public Wheels TireCombinedSlip { get; init; }
    public Wheels SuspensionTravelMeters { get; init; }

    public int CarOrdinal { get; init; }
    public int CarClass { get; init; }
    public int CarPerformanceIndex { get; init; }
    public int DrivetrainType { get; init; }
    public int NumCylinders { get; init; }
    public uint CarGroup { get; init; }

    public float SmashableVelDiff { get; init; }
    public float SmashableMass { get; init; }

    public Vec3 Position { get; init; }
    public float Speed { get; init; }
    public float Power { get; init; }
    public float Torque { get; init; }

    public Wheels TireTemp { get; init; }

    public float Boost { get; init; }
    public float Fuel { get; init; }
    public float DistanceTraveled { get; init; }

    public float BestLap { get; init; }
    public float LastLap { get; init; }
    public float CurrentLap { get; init; }
    public float CurrentRaceTime { get; init; }

    public ushort LapNumber { get; init; }
    public byte RacePosition { get; init; }
    public byte Accel { get; init; }
    public byte Brake { get; init; }
    public byte Clutch { get; init; }
    public byte HandBrake { get; init; }
    public byte Gear { get; init; }
    public sbyte Steer { get; init; }
    public sbyte NormalizedDrivingLine { get; init; }
    public sbyte NormalizedAIBrakeDifference { get; init; }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Fh6.Telemetry.Core`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "Add TelemetryPacket model"
```

### Task 5: PacketParser + golden tests (TDD)

**Files:**
- Create: `src/Fh6.Telemetry.Core/PacketParser.cs`
- Modify: `tests/Fh6.Telemetry.Tests/PacketParserTests.cs` (add golden + validation tests)

- [ ] **Step 1: Add the failing golden tests**

Append these members to the `PacketParserTests` class. The base64 strings are real frames captured from the game.

```csharp
    // A real driving frame (IsRaceOn=1) captured from FH6.
    private const string DrivingFrameB64 =
        "AQAAAKJ6CQD5rzNG+P9HRBFFy0WM2vi/F5yrvXvoOEEAkIQ6k/LEvVroJUEYN4g7N0ervP5RFjwUfaS/zZ5cPHBpkLpDMLM+hITNPh30Gj8bnh4/7LgrQYEEkEBhh1s+Z5NVPkTUjUI0pEdCKjwHQuSYB0IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACEVcQ8GSPRPHi6rLz0Op+8CLkrQRkFkECJllw+UYBWPihFULwIBB68oJGTO6YurTtzBgAABQAAAIQDAAACAAAACAAAABoAAAAAAAAAAAAAALpuxMU6Pi5DMEpsxS7qJUEoGlzHHaikwqJEFENxfBRDBAgVQwQIFUMiVKJAAACAP0Dan8IAAAAAAAAAAGL5JEBj+SRAAAAJAAAAAAEAOwAA";

    // A real menu/stopped frame (IsRaceOn=0): only TimestampMs is non-zero.
    private const string MenuFrameB64 =
        "AAAAAIVDCQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static byte[] B64(string s) => Convert.FromBase64String(s);

    [Fact]
    public void Parse_decodes_driving_frame_fields()
    {
        var p = PacketParser.Parse(B64(DrivingFrameB64));

        Assert.Equal(1, p.IsRaceOn);
        Assert.Equal(621218u, p.TimestampMs);
        Assert.Equal(11499.99, p.EngineMaxRpm, 2);
        Assert.Equal(800.00, p.EngineIdleRpm, 2);
        Assert.Equal(6504.63, p.CurrentEngineRpm, 2);
        Assert.Equal(10.37, p.Speed, 2);
        Assert.Equal(5.07, p.Boost, 2);
        Assert.Equal(1.0, p.Fuel, 3);
        Assert.Equal(10.73, p.TireSlipRatio.FrontLeft, 2);
        Assert.Equal(1651, p.CarOrdinal);
        Assert.Equal(5, p.CarClass);
        Assert.Equal(900, p.CarPerformanceIndex);
        Assert.Equal(2, p.DrivetrainType);
        Assert.Equal(8, p.NumCylinders);
        // Tail fields prove the reader stays aligned to the end of the packet.
        Assert.Equal((byte)9, p.RacePosition);
        Assert.Equal((byte)1, p.Gear);
        Assert.Equal((sbyte)0, p.Steer);
        Assert.Equal((sbyte)59, p.NormalizedDrivingLine);
    }

    [Fact]
    public void Parse_decodes_menu_frame()
    {
        var p = PacketParser.Parse(B64(MenuFrameB64));

        Assert.Equal(0, p.IsRaceOn);
        Assert.Equal(607109u, p.TimestampMs);
        Assert.Equal(0.0, p.Speed, 3);
        Assert.Equal((byte)0, p.Gear);
        Assert.Equal(0.0, p.EngineMaxRpm, 3);
    }

    [Fact]
    public void TryParse_rejects_wrong_length()
    {
        Assert.False(PacketParser.TryParse(new byte[100], out _));
        Assert.False(PacketParser.TryParse(new byte[323], out _));
        Assert.True(PacketParser.TryParse(B64(MenuFrameB64), out _));
    }

    [Fact]
    public void Parse_throws_on_too_small()
    {
        Assert.Throws<ArgumentException>(() => PacketParser.Parse(new byte[10]));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter PacketParserTests`
Expected: FAIL — `PacketParser` does not exist (compile error).

- [ ] **Step 3: Implement the parser**

Create `src/Fh6.Telemetry.Core/PacketParser.cs`:

```csharp
namespace Fh6.Telemetry.Core;

public static class PacketParser
{
    /// <summary>Total UDP packet size: 323 documented bytes + 1 alignment pad.</summary>
    public const int PacketSize = 324;

    /// <summary>Bytes consumed by documented fields; byte 323 is padding.</summary>
    public const int DocumentedSize = 323;

    public static bool TryParse(ReadOnlySpan<byte> packet, out TelemetryPacket result)
    {
        if (packet.Length != PacketSize)
        {
            result = default;
            return false;
        }

        result = Parse(packet);
        return true;
    }

    public static TelemetryPacket Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < DocumentedSize)
            throw new ArgumentException(
                $"Packet too small: {packet.Length} bytes, need at least {DocumentedSize}.", nameof(packet));

        // Object-initializer members run in textual order, matching the wire layout.
        var r = new SpanReader(packet);
        return new TelemetryPacket
        {
            IsRaceOn = r.S32(),
            TimestampMs = r.U32(),
            EngineMaxRpm = r.F32(),
            EngineIdleRpm = r.F32(),
            CurrentEngineRpm = r.F32(),
            Acceleration = new Vec3(r.F32(), r.F32(), r.F32()),
            Velocity = new Vec3(r.F32(), r.F32(), r.F32()),
            AngularVelocity = new Vec3(r.F32(), r.F32(), r.F32()),
            Yaw = r.F32(),
            Pitch = r.F32(),
            Roll = r.F32(),
            NormalizedSuspensionTravel = new Wheels(r.F32(), r.F32(), r.F32(), r.F32()),
            TireSlipRatio = new Wheels(r.F32(), r.F32(), r.F32(), r.F32()),
            WheelRotationSpeed = new Wheels(r.F32(), r.F32(), r.F32(), r.F32()),
            WheelOnRumbleStrip = new WheelsInt(r.S32(), r.S32(), r.S32(), r.S32()),
            WheelInPuddle = new WheelsInt(r.S32(), r.S32(), r.S32(), r.S32()),
            SurfaceRumble = new Wheels(r.F32(), r.F32(), r.F32(), r.F32()),
            TireSlipAngle = new Wheels(r.F32(), r.F32(), r.F32(), r.F32()),
            TireCombinedSlip = new Wheels(r.F32(), r.F32(), r.F32(), r.F32()),
            SuspensionTravelMeters = new Wheels(r.F32(), r.F32(), r.F32(), r.F32()),
            CarOrdinal = r.S32(),
            CarClass = r.S32(),
            CarPerformanceIndex = r.S32(),
            DrivetrainType = r.S32(),
            NumCylinders = r.S32(),
            CarGroup = r.U32(),
            SmashableVelDiff = r.F32(),
            SmashableMass = r.F32(),
            Position = new Vec3(r.F32(), r.F32(), r.F32()),
            Speed = r.F32(),
            Power = r.F32(),
            Torque = r.F32(),
            TireTemp = new Wheels(r.F32(), r.F32(), r.F32(), r.F32()),
            Boost = r.F32(),
            Fuel = r.F32(),
            DistanceTraveled = r.F32(),
            BestLap = r.F32(),
            LastLap = r.F32(),
            CurrentLap = r.F32(),
            CurrentRaceTime = r.F32(),
            LapNumber = r.U16(),
            RacePosition = r.U8(),
            Accel = r.U8(),
            Brake = r.U8(),
            Clutch = r.U8(),
            HandBrake = r.U8(),
            Gear = r.U8(),
            Steer = r.S8(),
            NormalizedDrivingLine = r.S8(),
            NormalizedAIBrakeDifference = r.S8(),
        };
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter PacketParserTests`
Expected: PASS (5 tests including the SpanReader test).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add packet parser with golden-frame tests"
```

- [ ] **Step 6: Merge phase to main**

```bash
git checkout main
git merge --no-ff feat/parser-core -m "Add core telemetry parser"
git branch -d feat/parser-core
```

---

## Phase 3 — Capture I/O and sources

Branch: `feat/capture-io`  (`git checkout -b feat/capture-io`)

### Task 6: CaptureFrame, ITelemetrySource, JsonlReplaySource (TDD)

**Files:**
- Create: `src/Fh6.Telemetry.Core/CaptureFrame.cs`
- Create: `src/Fh6.Telemetry.Core/ITelemetrySource.cs`
- Create: `src/Fh6.Telemetry.Core/JsonlReplaySource.cs`
- Test: `tests/Fh6.Telemetry.Tests/JsonlTests.cs`

- [ ] **Step 1: Write CaptureFrame and ITelemetrySource**

`src/Fh6.Telemetry.Core/CaptureFrame.cs`:

```csharp
namespace Fh6.Telemetry.Core;

/// <summary>A raw telemetry frame: its capture timestamp (ms) and the raw UDP bytes.</summary>
public readonly record struct CaptureFrame(double TimestampMs, byte[] Data);
```

`src/Fh6.Telemetry.Core/ITelemetrySource.cs`:

```csharp
namespace Fh6.Telemetry.Core;

public interface ITelemetrySource
{
    /// <summary>Yields frames in order. Finite for replay, unbounded for live UDP.</summary>
    IEnumerable<CaptureFrame> Frames();
}
```

- [ ] **Step 2: Write the failing test**

`tests/Fh6.Telemetry.Tests/JsonlTests.cs`:

```csharp
using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class JsonlTests
{
    [Fact]
    public void JsonlReplaySource_reads_frames_in_order()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                "{\"t\":1.5,\"len\":3,\"b64\":\"AAEC\"}\n" +
                "\n" + // blank line should be skipped
                "{\"t\":2.5,\"len\":2,\"b64\":\"//8=\"}\n");

            var frames = new JsonlReplaySource(path).Frames().ToList();

            Assert.Equal(2, frames.Count);
            Assert.Equal(1.5, frames[0].TimestampMs);
            Assert.Equal(new byte[] { 0, 1, 2 }, frames[0].Data);
            Assert.Equal(2.5, frames[1].TimestampMs);
            Assert.Equal(new byte[] { 0xFF, 0xFF }, frames[1].Data);
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test --filter JsonlReplaySource_reads_frames_in_order`
Expected: FAIL — `JsonlReplaySource` does not exist.

- [ ] **Step 4: Implement JsonlReplaySource**

`src/Fh6.Telemetry.Core/JsonlReplaySource.cs`:

```csharp
using System.Text.Json;

namespace Fh6.Telemetry.Core;

public sealed class JsonlReplaySource : ITelemetrySource
{
    private readonly string _path;

    public JsonlReplaySource(string path) => _path = path;

    public IEnumerable<CaptureFrame> Frames()
    {
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var t = root.GetProperty("t").GetDouble();
            var b64 = root.GetProperty("b64").GetString()
                      ?? throw new FormatException("Capture line missing 'b64'.");
            yield return new CaptureFrame(t, Convert.FromBase64String(b64));
        }
    }
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --filter JsonlReplaySource_reads_frames_in_order`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Add capture frame model and JSONL replay source"
```

### Task 7: JsonlCaptureWriter (TDD round-trip)

**Files:**
- Create: `src/Fh6.Telemetry.Core/JsonlCaptureWriter.cs`
- Modify: `tests/Fh6.Telemetry.Tests/JsonlTests.cs`

- [ ] **Step 1: Add the failing round-trip test**

Append to the `JsonlTests` class:

```csharp
    [Fact]
    public void Capture_then_replay_round_trips_bytes_and_timestamp()
    {
        var path = Path.GetTempFileName();
        try
        {
            byte[] payload = { 1, 2, 3, 250, 0, 127 };
            using (var writer = new JsonlCaptureWriter(path))
            {
                writer.Write(12.25, payload);
            }

            var frames = new JsonlReplaySource(path).Frames().ToList();

            Assert.Single(frames);
            Assert.Equal(12.25, frames[0].TimestampMs);
            Assert.Equal(payload, frames[0].Data);
        }
        finally { File.Delete(path); }
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter Capture_then_replay_round_trips_bytes_and_timestamp`
Expected: FAIL — `JsonlCaptureWriter` does not exist.

- [ ] **Step 3: Implement the buffered writer**

`src/Fh6.Telemetry.Core/JsonlCaptureWriter.cs`:

```csharp
using System.Globalization;

namespace Fh6.Telemetry.Core;

/// <summary>
/// Buffered JSONL writer matching the {t,len,b64} capture format. Buffered writes avoid
/// dropping UDP datagrams at frame rate (the failure mode of sync-append-per-packet).
/// </summary>
public sealed class JsonlCaptureWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public JsonlCaptureWriter(string path)
    {
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
        _writer = new StreamWriter(stream);
    }

    public void Write(double timestampMs, ReadOnlySpan<byte> data)
    {
        _writer.Write("{\"t\":");
        _writer.Write(timestampMs.ToString(CultureInfo.InvariantCulture));
        _writer.Write(",\"len\":");
        _writer.Write(data.Length.ToString(CultureInfo.InvariantCulture));
        _writer.Write(",\"b64\":\"");
        _writer.Write(Convert.ToBase64String(data));
        _writer.Write("\"}\n");
    }

    public void Flush() => _writer.Flush();

    public void Dispose()
    {
        _writer.Flush();
        _writer.Dispose();
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter JsonlTests`
Expected: PASS (both JSONL tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add buffered JSONL capture writer"
```

### Task 8: UdpTelemetrySource

**Files:**
- Create: `src/Fh6.Telemetry.Core/UdpTelemetrySource.cs`

> Not unit-tested (requires a live socket); exercised manually via the `capture`/`live` commands later.

- [ ] **Step 1: Implement the UDP source**

`src/Fh6.Telemetry.Core/UdpTelemetrySource.cs`:

```csharp
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Fh6.Telemetry.Core;

public sealed class UdpTelemetrySource : ITelemetrySource, IDisposable
{
    private readonly UdpClient _client;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public UdpTelemetrySource(int port) => _client = new UdpClient(port);

    public IEnumerable<CaptureFrame> Frames()
    {
        var endpoint = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            byte[] data;
            try
            {
                data = _client.Receive(ref endpoint);
            }
            catch (SocketException)
            {
                yield break; // socket closed (e.g. Ctrl-C disposed the client)
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }

            yield return new CaptureFrame(_clock.Elapsed.TotalMilliseconds, data);
        }
    }

    public void Dispose() => _client.Dispose();
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Fh6.Telemetry.Core`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "Add live UDP telemetry source"
```

- [ ] **Step 4: Merge phase to main**

```bash
git checkout main
git merge --no-ff feat/capture-io -m "Add capture I/O and telemetry sources"
git branch -d feat/capture-io
```

---

## Phase 4 — CLI app and dashboard

Branch: `feat/cli-dashboard`  (`git checkout -b feat/cli-dashboard`)

> UI tasks are verified by running the app against the local capture
> `capture-1781369277049.jsonl` (present locally, git-ignored), not by unit tests.

### Task 9: CLI app skeleton

**Files:**
- Modify: `src/Fh6.Telemetry.Cli/Program.cs` (replace the template)

- [ ] **Step 1: Replace Program.cs with the CommandApp wiring**

```csharp
using Fh6.Telemetry.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("fh6");
    config.AddCommand<CaptureCommand>("capture")
        .WithDescription("Record live UDP telemetry to a JSONL capture file.");
    config.AddCommand<ReplayCommand>("replay")
        .WithDescription("Replay a capture file to the dashboard.");
    config.AddCommand<LiveCommand>("live")
        .WithDescription("Show the live telemetry dashboard from UDP.");
    config.AddCommand<CoverageCommand>("coverage")
        .WithDescription("Report telemetry-condition coverage of a capture (temporary).");
});
return app.Run(args);
```

> This will not compile until the four command classes exist (Tasks 11–13 and 15). Build at the end of Task 13.

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "Wire CLI command app skeleton"
```

### Task 10: IDashboard and SpectreDashboard

**Files:**
- Create: `src/Fh6.Telemetry.Cli/IDashboard.cs`
- Create: `src/Fh6.Telemetry.Cli/SpectreDashboard.cs`

- [ ] **Step 1: Write the dashboard interface**

`src/Fh6.Telemetry.Cli/IDashboard.cs`:

```csharp
using Fh6.Telemetry.Core;

namespace Fh6.Telemetry.Cli;

public interface IDashboard
{
    /// <summary>
    /// Runs a live refreshing display. The driver is invoked with a callback that should be
    /// called once per parsed packet to update the screen; the driver controls iteration/timing.
    /// </summary>
    void Run(Action<Action<TelemetryPacket>> driver);
}
```

- [ ] **Step 2: Write the Spectre renderer**

`src/Fh6.Telemetry.Cli/SpectreDashboard.cs`:

```csharp
using Fh6.Telemetry.Core;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Fh6.Telemetry.Cli;

public sealed class SpectreDashboard : IDashboard
{
    public void Run(Action<Action<TelemetryPacket>> driver)
    {
        AnsiConsole.Live(Render(default))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
                driver(packet =>
                {
                    ctx.UpdateTarget(Render(packet));
                    ctx.Refresh();
                });
            });
    }

    private static IRenderable Render(TelemetryPacket p)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("Race", p.IsRaceOn == 1 ? "[green]ON[/]" : "[grey]off[/]");
        grid.AddRow("Speed", $"{p.Speed * 3.6f:F1} km/h  ({p.Speed:F1} m/s)");
        grid.AddRow("Gear", p.Gear.ToString());
        grid.AddRow("RPM", $"{p.CurrentEngineRpm:F0} / {p.EngineMaxRpm:F0}");
        grid.AddRow("Throttle / Brake", $"{p.Accel} / {p.Brake}");
        grid.AddRow("Clutch / Handbrake", $"{p.Clutch} / {p.HandBrake}");
        grid.AddRow("Steer", p.Steer.ToString());
        grid.AddRow("Boost", $"{p.Boost:F1} psi");
        grid.AddRow("Fuel", $"{p.Fuel * 100f:F0}%");
        grid.AddRow("Combined slip (FL FR RL RR)",
            $"{p.TireCombinedSlip.FrontLeft:F2} {p.TireCombinedSlip.FrontRight:F2} " +
            $"{p.TireCombinedSlip.RearLeft:F2} {p.TireCombinedSlip.RearRight:F2}");
        grid.AddRow("Tire temp (FL FR RL RR)",
            $"{p.TireTemp.FrontLeft:F0} {p.TireTemp.FrontRight:F0} " +
            $"{p.TireTemp.RearLeft:F0} {p.TireTemp.RearRight:F0}");
        grid.AddRow("Lap", $"{p.LapNumber}  cur {p.CurrentLap:F2}s  last {p.LastLap:F2}s  best {p.BestLap:F2}s");
        grid.AddRow("Position", p.RacePosition.ToString());
        grid.AddRow("Car", $"ordinal {p.CarOrdinal}  class {p.CarClass}  PI {p.CarPerformanceIndex}");

        return new Panel(grid).Header("FH6 Telemetry").Expand();
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "Add Spectre dashboard renderer"
```

### Task 11: ReplayCommand

**Files:**
- Create: `src/Fh6.Telemetry.Cli/Commands/ReplayCommand.cs`

- [ ] **Step 1: Implement the replay command**

`src/Fh6.Telemetry.Cli/Commands/ReplayCommand.cs`:

```csharp
using System.ComponentModel;
using Fh6.Telemetry.Core;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class ReplayCommand : Command<ReplayCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to a JSONL capture file.")]
        public string File { get; init; } = "";

        [CommandOption("-s|--speed")]
        [Description("Playback speed multiplier (1.0 = realtime).")]
        [DefaultValue(1.0)]
        public double Speed { get; init; }

        [CommandOption("-l|--loop")]
        [Description("Loop the capture continuously.")]
        public bool Loop { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var source = new JsonlReplaySource(settings.File);
        var dashboard = new SpectreDashboard();

        dashboard.Run(render =>
        {
            do
            {
                double? prevT = null;
                foreach (var frame in source.Frames())
                {
                    if (prevT is double previous && settings.Speed > 0)
                    {
                        var waitMs = (frame.TimestampMs - previous) / settings.Speed;
                        if (waitMs > 0)
                            Thread.Sleep(TimeSpan.FromMilliseconds(waitMs));
                    }
                    prevT = frame.TimestampMs;

                    if (PacketParser.TryParse(frame.Data, out var packet))
                        render(packet);
                }
            } while (settings.Loop);
        });

        return 0;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "Add replay command"
```

### Task 12: CaptureCommand

**Files:**
- Create: `src/Fh6.Telemetry.Cli/Commands/CaptureCommand.cs`

- [ ] **Step 1: Implement the capture command**

`src/Fh6.Telemetry.Cli/Commands/CaptureCommand.cs`:

```csharp
using System.ComponentModel;
using Fh6.Telemetry.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class CaptureCommand : Command<CaptureCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--port")]
        [Description("UDP port to listen on.")]
        [DefaultValue(20440)]
        public int Port { get; init; }

        [CommandOption("-o|--out")]
        [Description("Output file. Defaults to capture-<unixms>.jsonl.")]
        public string? Out { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var outPath = settings.Out ?? $"capture-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jsonl";

        using var source = new UdpTelemetrySource(settings.Port);
        using var writer = new JsonlCaptureWriter(outPath);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            source.Dispose(); // unblocks Receive(), ends the loop
        };

        AnsiConsole.MarkupLine($"[green]Listening on UDP :{settings.Port}[/] -> {outPath}  (Ctrl-C to stop)");

        long count = 0;
        foreach (var frame in source.Frames())
        {
            writer.Write(frame.TimestampMs, frame.Data);
            count++;
            if (count % 60 == 0)
                AnsiConsole.Markup($"\r{count} packets ");
        }

        writer.Flush();
        AnsiConsole.MarkupLine($"\n[green]Saved {count} packets to {outPath}[/]");
        return 0;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "Add capture command"
```

### Task 13: LiveCommand

**Files:**
- Create: `src/Fh6.Telemetry.Cli/Commands/LiveCommand.cs`

- [ ] **Step 1: Implement the live command**

`src/Fh6.Telemetry.Cli/Commands/LiveCommand.cs`:

```csharp
using System.ComponentModel;
using Fh6.Telemetry.Core;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class LiveCommand : Command<LiveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--port")]
        [Description("UDP port to listen on.")]
        [DefaultValue(20440)]
        public int Port { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        using var source = new UdpTelemetrySource(settings.Port);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            source.Dispose();
        };

        var dashboard = new SpectreDashboard();
        dashboard.Run(render =>
        {
            foreach (var frame in source.Frames())
                if (PacketParser.TryParse(frame.Data, out var packet))
                    render(packet);
        });

        return 0;
    }
}
```

- [ ] **Step 2: Temporarily stub CoverageCommand so the app compiles**

> `Program.cs` references `CoverageCommand`, built in Phase 5. Add a minimal stub now; it is fully implemented in Task 15.

`src/Fh6.Telemetry.Cli/Commands/CoverageCommand.cs`:

```csharp
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class CoverageCommand : Command<CoverageCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        public string File { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings) => 0;
}
```

- [ ] **Step 3: Build the whole solution**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Manually verify replay against the local capture**

Run: `dotnet run --project src/Fh6.Telemetry.Cli -- replay capture-1781369277049.jsonl --speed 5`
Expected: a refreshing "FH6 Telemetry" panel showing changing speed/RPM/gear/tire values; exits at end of file. Ctrl-C stops early.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add live command and coverage stub; dashboard verified via replay"
```

- [ ] **Step 6: Merge phase to main**

```bash
git checkout main
git merge --no-ff feat/cli-dashboard -m "Add CLI commands and refreshing dashboard"
git branch -d feat/cli-dashboard
```

---

## Phase 5 — Coverage tracker (temporary)

Branch: `feat/coverage-tracker`  (`git checkout -b feat/coverage-tracker`)

### Task 14: CoverageReport and CoverageTracker (TDD)

**Files:**
- Create: `src/Fh6.Telemetry.Core/Coverage/CoverageReport.cs`
- Create: `src/Fh6.Telemetry.Core/Coverage/CoverageTracker.cs`
- Test: `tests/Fh6.Telemetry.Tests/CoverageTrackerTests.cs`

- [ ] **Step 1: Write the report types**

`src/Fh6.Telemetry.Core/Coverage/CoverageReport.cs`:

```csharp
namespace Fh6.Telemetry.Core.Coverage;

public readonly record struct CoverageItem(string Name, bool Met, long? FirstFrame);

public sealed class CoverageReport
{
    public CoverageReport(IReadOnlyList<CoverageItem> items) => Items = items;

    public IReadOnlyList<CoverageItem> Items { get; }

    public bool Complete => Items.All(i => i.Met);
}
```

- [ ] **Step 2: Write the failing test**

`tests/Fh6.Telemetry.Tests/CoverageTrackerTests.cs`:

```csharp
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Core.Coverage;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class CoverageTrackerTests
{
    private static bool Met(CoverageReport r, string name) =>
        r.Items.Single(i => i.Name == name).Met;

    [Fact]
    public void Tracks_conditions_across_packets_and_records_first_frame()
    {
        var tracker = new CoverageTracker();

        // Frame 0: a menu/idle packet (all zero) -> only "Menu/stopped" should be met.
        tracker.Observe(new TelemetryPacket());

        // Frame 1: an active driving packet exercising several field families.
        tracker.Observe(new TelemetryPacket
        {
            IsRaceOn = 1,
            Accel = 255,
            Brake = 255,
            Clutch = 10,
            HandBrake = 10,
            Steer = 127,
            Gear = 6,
            Boost = 5f,
            Speed = 60f,
            LapNumber = 1,
            TireCombinedSlip = new Wheels(2f, 0f, 0f, 0f),
        });

        var r = tracker.Report();

        Assert.True(Met(r, "Menu/stopped"));
        Assert.True(Met(r, "Driving"));
        Assert.True(Met(r, "Full throttle"));
        Assert.True(Met(r, "Hard braking"));
        Assert.True(Met(r, "Clutch used"));
        Assert.True(Met(r, "Handbrake used"));
        Assert.True(Met(r, "Full steer right"));
        Assert.True(Met(r, "High gear (>=5)"));
        Assert.True(Met(r, "Boost present"));
        Assert.True(Met(r, "High speed (>50 m/s)"));
        Assert.True(Met(r, "Lap completed"));
        Assert.True(Met(r, "High combined slip"));

        // Not exercised by either packet.
        Assert.False(Met(r, "Puddle (wet)"));
        Assert.False(Met(r, "Collision (smashable)"));
        Assert.False(r.Complete);

        // "Driving" first appeared on frame index 1.
        Assert.Equal(1L, r.Items.Single(i => i.Name == "Driving").FirstFrame);
    }
}
```

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test --filter CoverageTrackerTests`
Expected: FAIL — `CoverageTracker` does not exist.

- [ ] **Step 4: Implement the tracker**

`src/Fh6.Telemetry.Core/Coverage/CoverageTracker.cs`:

```csharp
namespace Fh6.Telemetry.Core.Coverage;

/// <summary>
/// Temporary aid: checks that a capture exercised every family of packet fields, so manual
/// test captures can be confirmed complete. Remove once captures are validated.
/// </summary>
public sealed class CoverageTracker
{
    private sealed record Condition(string Name, Func<TelemetryPacket, bool> IsMet);

    private static readonly Condition[] Conditions =
    {
        new("Driving", p => p.IsRaceOn == 1),
        new("Menu/stopped", p => p.IsRaceOn == 0),
        new("Full throttle", p => p.Accel >= 250),
        new("Hard braking", p => p.Brake >= 250),
        new("Clutch used", p => p.Clutch > 0),
        new("Handbrake used", p => p.HandBrake > 0),
        new("Full steer left", p => p.Steer <= -120),
        new("Full steer right", p => p.Steer >= 120),
        new("Reverse gear", p => p.Gear == 0),
        new("High gear (>=5)", p => p.Gear >= 5),
        new("Near redline", p => p.EngineMaxRpm > 0 && p.CurrentEngineRpm >= 0.95f * p.EngineMaxRpm),
        new("High slip ratio", p => p.TireSlipRatio.Any(v => Math.Abs(v) > 1f)),
        new("High slip angle", p => p.TireSlipAngle.Any(v => Math.Abs(v) > 1f)),
        new("High combined slip", p => p.TireCombinedSlip.Any(v => v > 1f)),
        new("Rumble strip", p => p.WheelOnRumbleStrip.Any(v => v == 1)),
        new("Puddle (wet)", p => p.WheelInPuddle.Any(v => v == 1)),
        new("Off-road (surface rumble)", p => p.SurfaceRumble.Any(v => v > 0.1f)),
        new("Suspension compression", p => p.NormalizedSuspensionTravel.Any(v => v >= 0.95f)),
        new("Airborne (all wheels stretched)", p => p.NormalizedSuspensionTravel.All(v => v <= 0.05f) && p.IsRaceOn == 1),
        new("Boost present", p => p.Boost > 1f),
        new("Collision (smashable)", p => p.SmashableVelDiff > 0f || p.SmashableMass > 0f),
        new("Lap completed", p => p.LapNumber >= 1),
        new("High speed (>50 m/s)", p => p.Speed > 50f),
    };

    private readonly Dictionary<string, long> _firstSeen = new();
    private long _frame = -1;

    public void Observe(in TelemetryPacket packet)
    {
        _frame++;
        foreach (var condition in Conditions)
        {
            if (_firstSeen.ContainsKey(condition.Name))
                continue;
            if (condition.IsMet(packet))
                _firstSeen[condition.Name] = _frame;
        }
    }

    public CoverageReport Report()
    {
        var items = Conditions
            .Select(c => new CoverageItem(
                c.Name,
                _firstSeen.ContainsKey(c.Name),
                _firstSeen.TryGetValue(c.Name, out var f) ? f : (long?)null))
            .ToList();
        return new CoverageReport(items);
    }
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --filter CoverageTrackerTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Add capture coverage tracker"
```

### Task 15: CoverageCommand (replace the stub)

**Files:**
- Modify: `src/Fh6.Telemetry.Cli/Commands/CoverageCommand.cs`

- [ ] **Step 1: Replace the stub with the full implementation**

```csharp
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Core.Coverage;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fh6.Telemetry.Cli.Commands;

public sealed class CoverageCommand : Command<CoverageCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        public string File { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var source = new JsonlReplaySource(settings.File);
        var tracker = new CoverageTracker();

        long frames = 0;
        foreach (var frame in source.Frames())
        {
            if (PacketParser.TryParse(frame.Data, out var packet))
            {
                tracker.Observe(packet);
                frames++;
            }
        }

        var report = tracker.Report();
        var table = new Table()
            .AddColumn("Condition")
            .AddColumn("Status")
            .AddColumn("First frame");

        foreach (var item in report.Items)
        {
            table.AddRow(
                Markup.Escape(item.Name),
                item.Met ? "[green]met[/]" : "[red]missing[/]",
                item.FirstFrame?.ToString() ?? "-");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(report.Complete
            ? $"[green]Coverage complete[/] over {frames} frames."
            : $"[yellow]Coverage incomplete[/] over {frames} frames.");

        return report.Complete ? 0 : 1;
    }
}
```

- [ ] **Step 2: Build and manually verify against the local capture**

Run: `dotnet build`
Expected: `Build succeeded.`

Run: `dotnet run --project src/Fh6.Telemetry.Cli -- coverage capture-1781369277049.jsonl`
Expected: a table of conditions with met/missing status and first-frame indices, plus a summary line. (Some conditions like puddle/collision will likely show "missing" — that is the signal for what to drive next.)

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "Implement coverage command output"
```

- [ ] **Step 4: Merge phase to main**

```bash
git checkout main
git merge --no-ff feat/coverage-tracker -m "Add coverage tracker command"
git branch -d feat/coverage-tracker
```

---

## Phase 6 — Retire the legacy capture script

Branch: `chore/retire-capture-js`  (`git checkout -b chore/retire-capture-js`)

### Task 16: Remove capture.js

**Files:**
- Delete: `capture.js`
- Modify: `CLAUDE.md` (drop the "(legacy capture script)" mention if present is optional)

- [ ] **Step 1: Remove the superseded script**

```bash
git rm capture.js
```

- [ ] **Step 2: Verify nothing references it**

Run: `dotnet build`
Expected: `Build succeeded.` (no references to capture.js exist in the solution.)

- [ ] **Step 3: Commit and merge**

```bash
git add -A
git commit -m "Remove legacy node capture script (replaced by fh6 capture)"
git checkout main
git merge --no-ff chore/retire-capture-js -m "Retire legacy capture script"
git branch -d chore/retire-capture-js
```

---

## Final verification

- [ ] Run the full test suite: `dotnet test`
      Expected: all tests pass (parser, JSONL, coverage).
- [ ] `dotnet build` succeeds with 0 warnings/errors.
- [ ] `dotnet run --project src/Fh6.Telemetry.Cli -- replay capture-1781369277049.jsonl --speed 5` shows the dashboard.
- [ ] `dotnet run --project src/Fh6.Telemetry.Cli -- coverage capture-1781369277049.jsonl` prints the coverage table.
- [ ] `git log --graph --oneline` shows clean per-phase merges with no AI footprint.

## Removal note (coverage tracker)

Once a fresh in-game capture reports **Coverage complete**, remove the temporary feature on
its own branch: delete `src/Fh6.Telemetry.Core/Coverage/`, `CoverageCommand.cs`,
`CoverageTrackerTests.cs`, and the `coverage` registration in `Program.cs`; then update the spec.
