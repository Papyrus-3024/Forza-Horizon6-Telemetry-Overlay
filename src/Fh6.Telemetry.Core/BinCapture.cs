namespace Fh6.Telemetry.Core;

/// <summary>
/// Compact binary capture format: per frame, a little-endian
/// [double timestampMs][int32 length][length bytes]. Smaller and faster to read than JSONL;
/// round-trips through the same <see cref="PacketParser"/> as every other source.
/// </summary>
public static class BinCapture
{
    public static void Write(IEnumerable<CaptureFrame> frames, Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (var f in frames)
        {
            w.Write(f.TimestampMs);     // little-endian double
            w.Write(f.Data.Length);     // little-endian int32
            w.Write(f.Data);
        }
    }
}

/// <summary>Replays a <see cref="BinCapture"/> file as a telemetry source.</summary>
public sealed class BinReplaySource : ITelemetrySource
{
    private readonly string _path;

    public BinReplaySource(string path) => _path = path;

    public IEnumerable<CaptureFrame> Frames()
    {
        using var r = new BinaryReader(File.OpenRead(_path));
        var s = r.BaseStream;
        while (s.Position + 12 <= s.Length)
        {
            double t = r.ReadDouble();
            int len = r.ReadInt32();
            if (len < 0 || s.Position + len > s.Length) yield break; // truncated/corrupt tail
            var data = r.ReadBytes(len);
            yield return new CaptureFrame(t, data);
        }
    }
}
