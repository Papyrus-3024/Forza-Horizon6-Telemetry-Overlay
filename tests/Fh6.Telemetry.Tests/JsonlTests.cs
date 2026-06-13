using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class JsonlTests
{
    [Fact]
    public void JsonlReplaySource_reads_frames_in_order()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                "{\"t\":1.5,\"len\":3,\"b64\":\"AAEC\"}\n" +
                "\n" + // blank line should be skipped
                "{\"t\":2.5,\"len\":2,\"b64\":\"//8=\"}\n");

            var frames = new JsonlReplaySource(path).Frames().ToList();

            Assert.Equal(2, frames.Count);
            Assert.Equal(1.5, frames[0].TimestampMs);
            Assert.Equal(new byte[] { 0, 1, 2 }, frames[0].Data);
            Assert.Equal(2.5, frames[1].TimestampMs);
            Assert.Equal(new byte[] { 0xFF, 0xFF }, frames[1].Data);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Capture_then_replay_round_trips_bytes_and_timestamp()
    {
        var path = Path.GetTempFileName();
        try
        {
            byte[] payload = { 1, 2, 3, 250, 0, 127 };
            using (var writer = new JsonlCaptureWriter(path))
            {
                writer.Write(12.25, payload);
            }

            var frames = new JsonlReplaySource(path).Frames().ToList();

            Assert.Single(frames);
            Assert.Equal(12.25, frames[0].TimestampMs);
            Assert.Equal(payload, frames[0].Data);
        }
        finally { File.Delete(path); }
    }
}
