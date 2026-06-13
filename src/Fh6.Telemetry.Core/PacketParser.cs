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
