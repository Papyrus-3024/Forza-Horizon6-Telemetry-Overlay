namespace Fh6.Telemetry.Core;

/// <summary>Pure, display-ready projection of a raw <see cref="TelemetryPacket"/>.</summary>
public readonly struct TelemetryReadout
{
    private static readonly float[] ShiftThresholds = { 0.80f, 0.85f, 0.90f, 0.94f, 0.97f };

    public TelemetryReadout(in TelemetryPacket p)
    {
        IsRaceOn    = p.IsRaceOn == 1;
        TimestampMs = p.TimestampMs;
        SpeedMs = p.Speed;
        SpeedKmh = p.Speed * 3.6f;
        Gear = p.Gear;
        Rpm = p.CurrentEngineRpm;
        MaxRpm = p.EngineMaxRpm;
        RpmFraction = p.EngineMaxRpm > 0f
            ? Math.Clamp(p.CurrentEngineRpm / p.EngineMaxRpm, 0f, 1f)
            : 0f;

        var stage = 0;
        foreach (var t in ShiftThresholds)
            if (RpmFraction >= t) stage++;
        ShiftLightStage = stage;

        ThrottleFraction = p.Accel / 255f;
        BrakeFraction = p.Brake / 255f;
        ClutchFraction = p.Clutch / 255f;
        SteerFraction = Math.Clamp(p.Steer / 127f, -1f, 1f);
        Boost = p.Boost;
        FuelPercent = p.Fuel * 100f;
        LapNumber = p.LapNumber;
        RacePosition = p.RacePosition;
        BestLap = p.BestLap;
        LastLap = p.LastLap;
        CurrentLap = p.CurrentLap;
        Acceleration = p.Acceleration;
        Power = p.Power;
        Torque = p.Torque;
        Position = p.Position;
    }

    public bool IsRaceOn    { get; }
    public uint TimestampMs { get; }
    public float SpeedMs { get; }
    public float SpeedKmh { get; }
    public int Gear { get; }
    public float Rpm { get; }
    public float MaxRpm { get; }
    public float RpmFraction { get; }
    public int ShiftLightStage { get; }
    public float ThrottleFraction { get; }
    public float BrakeFraction { get; }
    public float ClutchFraction { get; }
    public float SteerFraction { get; }
    public float Boost { get; }
    public float FuelPercent { get; }
    public int LapNumber { get; }
    public int RacePosition { get; }
    public float BestLap { get; }
    public float LastLap { get; }
    public float CurrentLap { get; }
    public Vec3 Acceleration { get; }
    public float Power { get; }
    public float Torque { get; }
    public Vec3 Position { get; }

    // World ground-plane coordinates (X/Z are the ground plane, Y is altitude)
    public float PositionX => Position.X;
    public float PositionZ => Position.Z;

    // Unit conversions
    public float SpeedMph  => SpeedMs * 2.2369363f;
    public float LatG      => Acceleration.X / 9.80665f;
    public float LongG     => Acceleration.Z / 9.80665f;
    public float PowerHp   => Power / 745.6999f;
    public float TorqueLbFt => Torque * 0.7375621f;
}
