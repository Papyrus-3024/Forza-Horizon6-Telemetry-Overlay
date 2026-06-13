using System.Windows.Media;

namespace Fh6.Telemetry.Overlay.Helpers;

/// <summary>
/// Converts between "#AARRGGBB" hex strings and frozen <see cref="SolidColorBrush"/> instances.
/// Invalid or null inputs degrade to null (use widget theme default) rather than throwing.
/// </summary>
public static class ColorOverrides
{
    /// <summary>
    /// Parses a hex color string (e.g. "#FF5AD15A") into a frozen <see cref="SolidColorBrush"/>.
    /// Returns null for null, empty, or invalid input.
    /// </summary>
    public static Brush? TryParse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;
        try
        {
            var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a <see cref="SolidColorBrush"/> back to a "#AARRGGBB" hex string.
    /// Returns null for null or non-SolidColorBrush inputs.
    /// </summary>
    public static string? ToHex(Brush? brush)
    {
        if (brush is not SolidColorBrush solid)
            return null;
        var c = solid.Color;
        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
