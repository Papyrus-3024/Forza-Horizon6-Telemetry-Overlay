using System.Text.Json;

namespace Fh6.Telemetry.Core;

public sealed class JsonlReplaySource : ITelemetrySource
{
    private readonly string _path;

    public JsonlReplaySource(string path) => _path = path;

    public IEnumerable<CaptureFrame> Frames()
    {
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var t = root.GetProperty("t").GetDouble();
            var b64 = root.GetProperty("b64").GetString()
                      ?? throw new FormatException("Capture line missing 'b64'.");
            yield return new CaptureFrame(t, Convert.FromBase64String(b64));
        }
    }
}
