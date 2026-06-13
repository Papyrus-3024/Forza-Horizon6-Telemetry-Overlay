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
