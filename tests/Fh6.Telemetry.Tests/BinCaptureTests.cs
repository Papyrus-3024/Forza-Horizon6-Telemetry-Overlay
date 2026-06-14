using System.IO;
using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class BinCaptureTests
{
    [Fact]
    public void Write_then_read_round_trips_frames()
    {
        var frames = new[]
        {
            new CaptureFrame(1.5, new byte[] { 1, 2, 3, 4 }),
            new CaptureFrame(2.5, new byte[] { 9, 8, 7 }),
        };

        var path = Path.GetTempFileName();
        try
        {
            using (var s = File.Create(path))
                BinCapture.Write(frames, s);

            var read = new BinReplaySource(path).Frames().ToList();

            Assert.Equal(2, read.Count);
            Assert.Equal(1.5, read[0].TimestampMs);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, read[0].Data);
            Assert.Equal(2.5, read[1].TimestampMs);
            Assert.Equal(new byte[] { 9, 8, 7 }, read[1].Data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Reader_stops_cleanly_on_truncated_tail()
    {
        var path = Path.GetTempFileName();
        try
        {
            // One valid frame, then a truncated header.
            using (var s = File.Create(path))
            {
                using var w = new BinaryWriter(s, System.Text.Encoding.UTF8, leaveOpen: true);
                w.Write(1.0); w.Write(2); w.Write(new byte[] { 5, 6 });
                w.Write(7.0); // dangling double, no length/data
            }

            var read = new BinReplaySource(path).Frames().ToList();
            Assert.Single(read);
            Assert.Equal(new byte[] { 5, 6 }, read[0].Data);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
