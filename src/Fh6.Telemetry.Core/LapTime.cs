namespace Fh6.Telemetry.Core;

public static class LapTime
{
    /// <summary>Formats lap seconds as m:ss.fff. Non-positive (not set) renders as a blank time.</summary>
    public static string Format(float seconds)
    {
        if (seconds <= 0f)
            return "--:--.---";

        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:00}.{ts.Milliseconds:000}";
    }
}
