using System.Globalization;
using System.Windows.Media;

namespace Fh6.Telemetry.Overlay.Theming;

/// <summary>
/// A resolved set of named brushes that define the visual identity of the overlay.
/// All brushes are frozen solid-color brushes; create via <see cref="ThemePalette.Resolve"/>.
/// </summary>
public sealed class ThemePalette
{
    public SolidColorBrush Accent        { get; private init; } = null!;
    public SolidColorBrush AccentDim     { get; private init; } = null!;
    public SolidColorBrush Surface       { get; private init; } = null!;
    public SolidColorBrush Border        { get; private init; } = null!;
    public SolidColorBrush TextPrimary   { get; private init; } = null!;
    public SolidColorBrush TextSecondary { get; private init; } = null!;
    public SolidColorBrush Good          { get; private init; } = null!;
    public SolidColorBrush Warn          { get; private init; } = null!;
    public SolidColorBrush Danger        { get; private init; } = null!;

    // ── Built-in presets ────────────────────────────────────────────────────

    private static readonly Dictionary<string, ThemePalette> PresetCatalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DarkGlass"] = new ThemePalette
            {
                // Current "Dark Glass" look
                Accent        = Frozen(0xFF, 0x5A, 0xD1, 0x5A),   // teal-green
                AccentDim     = Frozen(0x99, 0x5A, 0xD1, 0x5A),
                Surface       = Frozen(0xC8, 0x12, 0x18, 0x21),   // translucent dark
                Border        = Frozen(0x26, 0xFF, 0xFF, 0xFF),   // subtle light rim
                TextPrimary   = Frozen(0xFF, 0xF2, 0xF4, 0xF7),
                TextSecondary = Frozen(0x99, 0xFF, 0xFF, 0xFF),
                Good          = Frozen(0xFF, 0x5A, 0xD1, 0x5A),   // green
                Warn          = Frozen(0xFF, 0xE0, 0xC9, 0x3A),   // amber
                Danger        = Frozen(0xFF, 0xE0, 0x5A, 0x5A),   // red
            },
            ["SportRed"] = new ThemePalette
            {
                Accent        = Frozen(0xFF, 0xE0, 0x3A, 0x3A),
                AccentDim     = Frozen(0x99, 0xE0, 0x3A, 0x3A),
                Surface       = Frozen(0xC8, 0x18, 0x10, 0x10),
                Border        = Frozen(0x33, 0xFF, 0x80, 0x80),
                TextPrimary   = Frozen(0xFF, 0xF5, 0xF0, 0xF0),
                TextSecondary = Frozen(0x99, 0xFF, 0xCC, 0xCC),
                Good          = Frozen(0xFF, 0x5A, 0xD1, 0x5A),
                Warn          = Frozen(0xFF, 0xE0, 0xC9, 0x3A),
                Danger        = Frozen(0xFF, 0xFF, 0x22, 0x22),
            },
            ["CoolBlue"] = new ThemePalette
            {
                Accent        = Frozen(0xFF, 0x4A, 0x9F, 0xE8),
                AccentDim     = Frozen(0x99, 0x4A, 0x9F, 0xE8),
                Surface       = Frozen(0xC8, 0x0A, 0x14, 0x24),
                Border        = Frozen(0x33, 0x80, 0xC0, 0xFF),
                TextPrimary   = Frozen(0xFF, 0xEE, 0xF4, 0xFF),
                TextSecondary = Frozen(0x99, 0xCC, 0xE0, 0xFF),
                Good          = Frozen(0xFF, 0x5A, 0xD1, 0x5A),
                Warn          = Frozen(0xFF, 0xE0, 0xC9, 0x3A),
                Danger        = Frozen(0xFF, 0xE0, 0x5A, 0x5A),
            },
            ["Mono"] = new ThemePalette
            {
                Accent        = Frozen(0xFF, 0xD0, 0xD0, 0xD0),
                AccentDim     = Frozen(0x99, 0xD0, 0xD0, 0xD0),
                Surface       = Frozen(0xC8, 0x10, 0x10, 0x10),
                Border        = Frozen(0x40, 0xFF, 0xFF, 0xFF),
                TextPrimary   = Frozen(0xFF, 0xFF, 0xFF, 0xFF),
                TextSecondary = Frozen(0x88, 0xFF, 0xFF, 0xFF),
                Good          = Frozen(0xFF, 0xBB, 0xBB, 0xBB),
                Warn          = Frozen(0xFF, 0x88, 0x88, 0x88),
                Danger        = Frozen(0xFF, 0x55, 0x55, 0x55),
            },
        };

    /// <summary>Names of all built-in presets, in display order.</summary>
    public static IReadOnlyList<string> PresetNames { get; } =
        ["DarkGlass", "SportRed", "CoolBlue", "Mono"];

    /// <summary>
    /// Returns a palette for the named preset. If <paramref name="customAccentHex"/> is a
    /// valid #RRGGBB or #AARRGGBB hex string, it overrides the preset's Accent and AccentDim
    /// (AccentDim gets the same hue at 60% opacity). Unknown preset names fall back to DarkGlass.
    /// Invalid customAccentHex values are silently ignored.
    /// </summary>
    public static ThemePalette Resolve(string preset, string? customAccentHex = null)
    {
        if (!PresetCatalog.TryGetValue(preset ?? string.Empty, out var palette))
            palette = PresetCatalog["DarkGlass"];

        if (string.IsNullOrWhiteSpace(customAccentHex))
            return palette;

        if (!TryParseHex(customAccentHex, out var accentColor))
            return palette;

        // Clone the palette, replacing only the accent brushes.
        var dim = Color.FromArgb(0x99, accentColor.R, accentColor.G, accentColor.B);
        return new ThemePalette
        {
            Accent        = Frozen(accentColor.A, accentColor.R, accentColor.G, accentColor.B),
            AccentDim     = Frozen(dim.A, dim.R, dim.G, dim.B),
            Surface       = palette.Surface,
            Border        = palette.Border,
            TextPrimary   = palette.TextPrimary,
            TextSecondary = palette.TextSecondary,
            Good          = palette.Good,
            Warn          = palette.Warn,
            Danger        = palette.Danger,
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>Parses #RRGGBB (alpha=FF) or #AARRGGBB. Returns false on any failure.</summary>
    private static bool TryParseHex(string hex, out Color color)
    {
        color = default;
        var s = hex.Trim().TrimStart('#');

        if (s.Length == 6)
            s = "FF" + s;

        if (s.Length != 8)
            return false;

        if (!uint.TryParse(s, NumberStyles.HexNumber, null, out var val))
            return false;

        color = Color.FromArgb(
            (byte)((val >> 24) & 0xFF),
            (byte)((val >> 16) & 0xFF),
            (byte)((val >>  8) & 0xFF),
            (byte)( val        & 0xFF));
        return true;
    }
}
