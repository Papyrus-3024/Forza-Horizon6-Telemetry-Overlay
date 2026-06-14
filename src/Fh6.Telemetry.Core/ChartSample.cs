namespace Fh6.Telemetry.Core;

/// <summary>One captured frame for the chart: timestamp + the raw channel values we may plot.</summary>
public readonly record struct ChartSample(
    double TimeSeconds,
    float Throttle, float Brake, float Clutch, float Steer,
    float SpeedKmh, float RpmFraction, int Gear,
    float PowerHp, float TorqueLbFt, float LatG, float LongG);
