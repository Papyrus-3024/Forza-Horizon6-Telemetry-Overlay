namespace Fh6.Telemetry.Core;

public interface ITelemetrySource
{
    /// <summary>Yields frames in order. Finite for replay, unbounded for live UDP.</summary>
    IEnumerable<CaptureFrame> Frames();
}
