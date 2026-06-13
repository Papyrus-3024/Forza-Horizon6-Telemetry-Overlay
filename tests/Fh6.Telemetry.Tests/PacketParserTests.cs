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
}
