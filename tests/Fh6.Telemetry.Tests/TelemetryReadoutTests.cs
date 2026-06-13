using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class TelemetryReadoutTests
{
    private const string DrivingFrameB64 =
        "AQAAAKJ6CQD5rzNG+P9HRBFFy0WM2vi/F5yrvXvoOEEAkIQ6k/LEvVroJUEYN4g7N0ervP5RFjwUfaS/zZ5cPHBpkLpDMLM+hITNPh30Gj8bnh4/7LgrQYEEkEBhh1s+Z5NVPkTUjUI0pEdCKjwHQuSYB0IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACEVcQ8GSPRPHi6rLz0Op+8CLkrQRkFkECJllw+UYBWPihFULwIBB68oJGTO6YurTtzBgAABQAAAIQDAAACAAAACAAAABoAAAAAAAAAAAAAALpuxMU6Pi5DMEpsxS7qJUEoGlzHHaikwqJEFENxfBRDBAgVQwQIFUMiVKJAAACAP0Dan8IAAAAAAAAAAGL5JEBj+SRAAAAJAAAAAAEAOwAA";

    private static TelemetryReadout DrivingReadout()
    {
        var packet = PacketParser.Parse(System.Convert.FromBase64String(DrivingFrameB64));
        return new TelemetryReadout(packet);
    }

    [Fact]
    public void Maps_speed_gear_and_inputs_from_real_frame()
    {
        var r = DrivingReadout();
        Assert.Equal(37.33, r.SpeedKmh, 2);
        Assert.Equal(1, r.Gear);
        Assert.Equal(0.0, r.ThrottleFraction, 3);
        Assert.Equal(100.0, r.FuelPercent, 2);
        Assert.Equal(5.07, r.Boost, 2);
        Assert.True(r.IsRaceOn);
    }

    [Fact]
    public void RpmFraction_is_clamped_and_drives_shift_stage()
    {
        var r = DrivingReadout();
        Assert.Equal(0.57, r.RpmFraction, 2);
        Assert.Equal(0, r.ShiftLightStage);
    }

    [Theory]
    [InlineData(10000f, 7000f, 0)]
    [InlineData(10000f, 8600f, 2)]
    [InlineData(10000f, 9800f, 5)]
    public void Shift_stage_counts_thresholds(float maxRpm, float curRpm, int expected)
    {
        var packet = new TelemetryPacket { EngineMaxRpm = maxRpm, CurrentEngineRpm = curRpm };
        var r = new TelemetryReadout(packet);
        Assert.Equal(expected, r.ShiftLightStage);
    }

    [Fact]
    public void Steer_fraction_is_normalized_and_clamped()
    {
        Assert.Equal(1.0, new TelemetryReadout(new TelemetryPacket { Steer = 127 }).SteerFraction, 3);
        Assert.Equal(-1.0, new TelemetryReadout(new TelemetryPacket { Steer = -127 }).SteerFraction, 3);
        Assert.Equal(0.0, new TelemetryReadout(new TelemetryPacket { Steer = 0 }).SteerFraction, 3);
    }

    [Theory]
    [InlineData(0f, "--:--.---")]
    [InlineData(42.0f, "0:42.000")]
    [InlineData(65.25f, "1:05.250")]
    [InlineData(102.5f, "1:42.500")]
    public void LapTime_formats_seconds(float seconds, string expected)
    {
        Assert.Equal(expected, LapTime.Format(seconds));
    }
}
