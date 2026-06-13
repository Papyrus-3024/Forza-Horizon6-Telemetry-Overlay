using System.Globalization;

namespace Fh6.Telemetry.Core;

/// <summary>
/// Buffered JSONL writer matching the {t,len,b64} capture format. Buffered writes avoid
/// dropping UDP datagrams at frame rate (the failure mode of sync-append-per-packet).
/// </summary>
public sealed class JsonlCaptureWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public JsonlCaptureWriter(string path)
    {
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16);
        _writer = new StreamWriter(stream);
    }

    public void Write(double timestampMs, ReadOnlySpan<byte> data)
    {
        _writer.Write("{\"t\":");
        _writer.Write(timestampMs.ToString(CultureInfo.InvariantCulture));
        _writer.Write(",\"len\":");
        _writer.Write(data.Length.ToString(CultureInfo.InvariantCulture));
        _writer.Write(",\"b64\":\"");
        _writer.Write(Convert.ToBase64String(data));
        _writer.Write("\"}\n");
    }

    public void Flush() => _writer.Flush();

    public void Dispose()
    {
        _writer.Flush();
        _writer.Dispose();
    }
}
