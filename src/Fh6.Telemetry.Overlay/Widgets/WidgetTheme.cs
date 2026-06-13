using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Frozen brushes and factory helpers that implement the "Dark Glass" theme
/// across all widgets. Edit here to change the global look in one place.
/// </summary>
public static class WidgetTheme
{
    // ── Surfaces ────────────────────────────────────────────────────────────
    /// <summary>Panel background: translucent dark — approximates frosted glass.</summary>
    public static readonly Brush Surface = Frozen(0xC8, 0x12, 0x18, 0x21);

    /// <summary>Subtle 1-px light border around each panel.</summary>
    public static readonly Brush BorderBrush = Frozen(0x26, 0xFF, 0xFF, 0xFF);
    public const double BorderThickness = 1.0;
    public const double CornerRadius = 8.0;

    // ── Text ────────────────────────────────────────────────────────────────
    /// <summary>Primary value text (large, bold).</summary>
    public static readonly Brush TextPrimary = Frozen(0xFF, 0xF2, 0xF4, 0xF7);

    /// <summary>Secondary / sub-value text.</summary>
    public static readonly Brush TextSecondary = Frozen(0x99, 0xFF, 0xFF, 0xFF);

    /// <summary>Dim label text (uppercase category labels).</summary>
    public static readonly Brush TextLabel = Frozen(0x80, 0xFF, 0xFF, 0xFF);

    // ── Accents ─────────────────────────────────────────────────────────────
    public static readonly Brush AccentGreen = Frozen(0xFF, 0x5A, 0xD1, 0x5A);
    public static readonly Brush AccentAmber = Frozen(0xFF, 0xE0, 0xC9, 0x3A);
    public static readonly Brush AccentRed   = Frozen(0xFF, 0xE0, 0x5A, 0x5A);
    public static readonly Brush AccentBlue  = Frozen(0xFF, 0x6A, 0xA8, 0xE0);

    // ── Progress bar backgrounds ─────────────────────────────────────────────
    /// <summary>Dark green-tinted trough for RPM / pedal bars.</summary>
    public static readonly Brush BarBackground = Frozen(0xFF, 0x1A, 0x24, 0x16);

    // ── Drop shadow ──────────────────────────────────────────────────────────
    /// <summary>
    /// Returns a new (unfrozen) soft drop-shadow to attach to each widget root.
    /// Kept as a factory because DropShadowEffect is not shareable across elements.
    /// </summary>
    public static DropShadowEffect PanelShadow() => new()
    {
        Color       = Color.FromRgb(0x00, 0x00, 0x00),
        ShadowDepth = 0,
        BlurRadius  = 10,
        Opacity     = 0.50,
        Direction   = 270,
    };

    // ── Frozen brush helper ──────────────────────────────────────────────────
    private static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
