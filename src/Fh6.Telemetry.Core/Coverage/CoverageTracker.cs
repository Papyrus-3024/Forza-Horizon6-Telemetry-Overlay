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
        new("Reverse gear", p => p.Gear == 0 && p.IsRaceOn == 1),
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
