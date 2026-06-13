namespace Fh6.Telemetry.Core;

/// <summary>A raw telemetry frame: its capture timestamp (ms) and the raw UDP bytes.</summary>
public readonly record struct CaptureFrame(double TimestampMs, byte[] Data);
