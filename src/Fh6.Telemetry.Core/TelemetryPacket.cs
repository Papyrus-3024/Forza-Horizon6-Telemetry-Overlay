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
