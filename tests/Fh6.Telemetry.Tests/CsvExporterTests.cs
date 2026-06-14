using System.IO;
using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class CsvExporterTests
{
    // Same real driving frame used by PacketParserTests (IsRaceOn=1, gear=1).
    private const string DrivingFrameB64 =
        "AQAAAKJ6CQD5rzNG+P9HRBFFy0WM2vi/F5yrvXvoOEEAkIQ6k/LEvVroJUEYN4g7N0ervP5RFjwUfaS/zZ5cPHBpkLpDMLM+hITNPh30Gj8bnh4/7LgrQYEEkEBhh1s+Z5NVPkTUjUI0pEdCKjwHQuSYB0IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACEVcQ8GSPRPHi6rLz0Op+8CLkrQRkFkECJllw+UYBWPihFULwIBB68oJGTO6YurTtzBgAABQAAAIQDAAACAAAACAAAABoAAAAAAAAAAAAAALpuxMU6Pi5DMEpsxS7qJUEoGlzHHaikwqJEFENxfBRDBAgVQwQIFUMiVKJAAACAP0Dan8IAAAAAAAAAAGL5JEBj+SRAAAAJAAAAAAEAOwAA";

    private static CaptureFrame Frame() =>
        new(0.0, System.Convert.FromBase64String(DrivingFrameB64));

    [Fact]
    public void Export_writes_header_and_one_row_per_frame()
    {
        var sw = new StringWriter();
        var rows = CsvExporter.Export(new[] { Frame(), Frame() }, sw);

        var lines = sw.ToString().Split('\n', System.StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, rows);
        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.StartsWith("timestamp_ms,speed_mph,speed_kmh,gear,", lines[0]);
    }

    [Fact]
    public void Export_row_has_one_field_per_header_column()
    {
        var sw = new StringWriter();
        CsvExporter.Export(new[] { Frame() }, sw);

        var lines = sw.ToString().Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        var headerCols = lines[0].Split(',').Length;
        var rowCols = lines[1].Split(',').Length;

        Assert.Equal(headerCols, rowCols);
        // gear is the 4th column and is 1 in this frame.
        Assert.Equal("1", lines[1].Split(',')[3]);
    }

    [Fact]
    public void Export_skips_unparseable_frames()
    {
        var sw = new StringWriter();
        var rows = CsvExporter.Export(new[] { new CaptureFrame(0, new byte[10]) }, sw);

        Assert.Equal(0, rows);
        Assert.Single(sw.ToString().Split('\n', System.StringSplitOptions.RemoveEmptyEntries)); // header only
    }
}
