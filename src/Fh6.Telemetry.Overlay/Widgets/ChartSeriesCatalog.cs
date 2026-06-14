using System.Windows.Media;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.Settings;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>Descriptor for one plottable telemetry channel.</summary>
public sealed record ChartSeriesDef(
    ChartSeriesId Id,
    string Name,
    Color Color,
    float Min,
    float Max,
    bool Stepped,
    Func<ChartSample, float> Select,
    Func<float, string> FormatValue);

/// <summary>
/// Static catalog of all <see cref="ChartSeriesId"/> definitions: colors, ranges, selectors,
/// formatters, and default-on flags. Pure data — no WPF rendering logic here.
/// </summary>
public static class ChartSeriesCatalog
{
    // Color helpers — inline so the catalog is self-contained.
    private static Color Hex(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    /// <summary>One entry per <see cref="ChartSeriesId"/>, in enum declaration order.</summary>
    public static readonly IReadOnlyList<ChartSeriesDef> All = new[]
    {
        new ChartSeriesDef(
            ChartSeriesId.Throttle,
            "Throttle",
            Hex(0x3F, 0xBF, 0x3F),   // green
            0f, 1f,
            Stepped: false,
            s => s.Throttle,
            v => $"{v * 100:F0}%"),

        new ChartSeriesDef(
            ChartSeriesId.Brake,
            "Brake",
            Hex(0xE0, 0x5A, 0x5A),   // red
            0f, 1f,
            Stepped: false,
            s => s.Brake,
            v => $"{v * 100:F0}%"),

        new ChartSeriesDef(
            ChartSeriesId.Clutch,
            "Clutch",
            Hex(0x80, 0xC0, 0x80),   // muted green
            0f, 1f,
            Stepped: false,
            s => s.Clutch,
            v => $"{v * 100:F0}%"),

        new ChartSeriesDef(
            ChartSeriesId.Steer,
            "Steer",
            Hex(0xB0, 0x7A, 0xE0),   // violet
            -1f, 1f,
            Stepped: false,
            s => s.Steer,
            v => $"{v:+0.00;-0.00}"),

        new ChartSeriesDef(
            ChartSeriesId.Speed,
            "Speed",
            Hex(0x3A, 0xC5, 0xE0),   // cyan
            0f, 400f,
            Stepped: false,
            s => s.SpeedKmh,
            v => $"{v:F0} km/h"),

        new ChartSeriesDef(
            ChartSeriesId.Rpm,
            "RPM",
            Hex(0xE0, 0xC9, 0x3A),   // amber
            0f, 1f,
            Stepped: false,
            s => s.RpmFraction,
            v => $"{v * 100:F0}%"),

        new ChartSeriesDef(
            ChartSeriesId.Gear,
            "Gear",
            Hex(0x9A, 0xA0, 0xA6),   // grey
            0f, 10f,
            Stepped: true,
            s => s.Gear,
            v => $"{(int)v}"),

        new ChartSeriesDef(
            ChartSeriesId.Power,
            "Power",
            Hex(0xE0, 0x8A, 0x3A),   // orange
            0f, 1500f,
            Stepped: false,
            s => s.PowerHp,
            v => $"{v:F0} hp"),

        new ChartSeriesDef(
            ChartSeriesId.Torque,
            "Torque",
            Hex(0x3A, 0xE0, 0xA0),   // teal
            0f, 1200f,
            Stepped: false,
            s => s.TorqueLbFt,
            v => $"{v:F0} lb·ft"),

        new ChartSeriesDef(
            ChartSeriesId.LatG,
            "Lat G",
            Hex(0xE0, 0x3A, 0xC5),   // magenta
            -2f, 2f,
            Stepped: false,
            s => s.LatG,
            v => $"{v:+0.00;-0.00}g"),

        new ChartSeriesDef(
            ChartSeriesId.LongG,
            "Long G",
            Hex(0x3A, 0x7A, 0xE0),   // blue
            -2f, 2f,
            Stepped: false,
            s => s.LongG,
            v => $"{v:+0.00;-0.00}g"),
    };

    /// <summary>Whether a series is on by default when no config entry is present.</summary>
    public static bool DefaultOn(ChartSeriesId id) => id is
        ChartSeriesId.Throttle or
        ChartSeriesId.Brake or
        ChartSeriesId.Speed;

    /// <summary>
    /// Returns true if the series is enabled in <paramref name="cfg"/>.
    /// A missing key falls back to <see cref="DefaultOn"/>.
    /// </summary>
    public static bool IsEnabled(ChartConfig cfg, ChartSeriesId id)
        => cfg.Series.TryGetValue(id.ToString(), out var v) ? v : DefaultOn(id);

    /// <summary>Looks up a series definition by id. Returns null if not found.</summary>
    public static ChartSeriesDef? ById(ChartSeriesId id)
    {
        foreach (var def in All)
            if (def.Id == id) return def;
        return null;
    }
}
