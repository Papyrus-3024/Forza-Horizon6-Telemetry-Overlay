using System.IO;

namespace Fh6.Telemetry.Overlay.Diagnostics;

/// <summary>
/// Minimal file logger for diagnosing the overlay (especially whether live telemetry is
/// arriving). Writes to %AppData%/fh6-overlay/overlay-log.txt. All calls are best-effort
/// and never throw.
/// </summary>
public static class OverlayLog
{
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "fh6-overlay", "overlay-log.txt");

    /// <summary>Truncates the log and writes a session header. Call once at startup.</summary>
    public static void Reset()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, $"{Stamp()} === overlay session start ===\n");
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(FilePath, $"{Stamp()} {message}\n");
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    private static string Stamp() => DateTime.Now.ToString("HH:mm:ss.fff");
}
