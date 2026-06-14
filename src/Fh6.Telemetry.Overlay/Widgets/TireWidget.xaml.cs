using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Four-corner tire tile widget. Each tile shows temperature as a continuous background
/// color mapped through a cold→optimal→warm→hot scale, the temp value in °F, and a
/// thin grip-headroom bar derived from combined slip.
///
/// Temp bands (°F):
///   &lt; 150  — cold   (blue #4F95E0)
///   150–230 — building → optimal (blue→green gradient)
///   230–270 — warm   (amber #E09C3A)
///   &gt; 270  — hot    (red  #EF4332)
/// </summary>
public partial class TireWidget : UserControl
{
    // ── Dependency properties ────────────────────────────────────────────────

    public static readonly DependencyProperty TireTempFLProperty = Reg(nameof(TireTempFL));
    public static readonly DependencyProperty TireTempFRProperty = Reg(nameof(TireTempFR));
    public static readonly DependencyProperty TireTempRLProperty = Reg(nameof(TireTempRL));
    public static readonly DependencyProperty TireTempRRProperty = Reg(nameof(TireTempRR));
    public static readonly DependencyProperty TireSlipFLProperty = Reg(nameof(TireSlipFL));
    public static readonly DependencyProperty TireSlipFRProperty = Reg(nameof(TireSlipFR));
    public static readonly DependencyProperty TireSlipRLProperty = Reg(nameof(TireSlipRL));
    public static readonly DependencyProperty TireSlipRRProperty = Reg(nameof(TireSlipRR));

    public double TireTempFL { get => (double)GetValue(TireTempFLProperty); set => SetValue(TireTempFLProperty, value); }
    public double TireTempFR { get => (double)GetValue(TireTempFRProperty); set => SetValue(TireTempFRProperty, value); }
    public double TireTempRL { get => (double)GetValue(TireTempRLProperty); set => SetValue(TireTempRLProperty, value); }
    public double TireTempRR { get => (double)GetValue(TireTempRRProperty); set => SetValue(TireTempRRProperty, value); }
    public double TireSlipFL { get => (double)GetValue(TireSlipFLProperty); set => SetValue(TireSlipFLProperty, value); }
    public double TireSlipFR { get => (double)GetValue(TireSlipFRProperty); set => SetValue(TireSlipFRProperty, value); }
    public double TireSlipRL { get => (double)GetValue(TireSlipRLProperty); set => SetValue(TireSlipRLProperty, value); }
    public double TireSlipRR { get => (double)GetValue(TireSlipRRProperty); set => SetValue(TireSlipRRProperty, value); }

    // ── Temp scale stops (°F) ────────────────────────────────────────────────
    // cold < 150 → optimal band 150-230 → warm 230-270 → hot > 270
    private const double TempCold    = 150.0;
    private const double TempOptimal = 230.0;
    private const double TempWarm    = 270.0;

    // Semantic colors — hardcoded (not theme-derived): same palette as the mockup
    private static readonly Color ColCold    = Color.FromRgb(0x4F, 0x95, 0xE0); // blue
    private static readonly Color ColOptimal = Color.FromRgb(0x2B, 0xBD, 0x74); // green
    private static readonly Color ColWarm    = Color.FromRgb(0xE0, 0x9C, 0x3A); // amber
    private static readonly Color ColHot     = Color.FromRgb(0xEF, 0x43, 0x32); // red

    // Text color: dark for mid-range tiles, white for hot tiles (legibility)
    private static readonly Brush TextDark  = Freeze(Color.FromRgb(0x08, 0x18, 0x10));
    private static readonly Brush TextLight = Freeze(Color.FromRgb(0xF2, 0xF4, 0xF7));

    // Grip bar fill brush (teal accent matching theme)
    private static readonly Brush GripFill = Freeze(Color.FromRgb(0x4F, 0xD1, 0xA5));

    // ── Constructor ──────────────────────────────────────────────────────────

    public TireWidget()
    {
        InitializeComponent();
    }

    // ── Property change ───────────────────────────────────────────────────────

    private static DependencyProperty Reg(string name) =>
        DependencyProperty.Register(name, typeof(double), typeof(TireWidget),
            new PropertyMetadata(0.0, OnChanged));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TireWidget)d).Redraw();

    // ── Redraw ────────────────────────────────────────────────────────────────

    private void Redraw()
    {
        ApplyCorner(TileFL, TempFL, GripFL, TireTempFL, TireSlipFL);
        ApplyCorner(TileFR, TempFR, GripFR, TireTempFR, TireSlipFR);
        ApplyCorner(TileRL, TempRL, GripRL, TireTempRL, TireSlipRL);
        ApplyCorner(TileRR, TempRR, GripRR, TireTempRR, TireSlipRR);
    }

    private static void ApplyCorner(Border tile, TextBlock label, Rectangle gripBar,
                                     double tempF, double slip)
    {
        var bg = TempColor(tempF);
        tile.Background = new SolidColorBrush(bg);

        // Text: show °F; dark text on bright greens/ambers, white on hot/cold
        bool useDark = tempF is > 170 and < 260;
        label.Text       = double.IsNaN(tempF) ? "--" : $"{tempF:F0}°";
        label.Foreground = useDark ? TextDark : TextLight;

        // Grip headroom: clamp(1 - slip, 0..1) → bar width fraction of 52px
        double grip = Math.Clamp(1.0 - (double.IsNaN(slip) ? 0.0 : slip), 0.0, 1.0);
        gripBar.Width  = grip * 52.0;
        gripBar.Fill   = GripFill;
    }

    // ── Temp → color interpolation ────────────────────────────────────────────

    internal static Color TempColor(double tempF)
    {
        if (double.IsNaN(tempF) || tempF <= TempCold)
            return ColCold;
        if (tempF >= TempWarm)
            return ColHot;

        if (tempF <= TempOptimal)
        {
            double t = (tempF - TempCold) / (TempOptimal - TempCold);
            return Lerp(ColCold, ColOptimal, t);
        }
        else
        {
            double t = (tempF - TempOptimal) / (TempWarm - TempOptimal);
            return Lerp(ColOptimal, ColWarm, t);
        }
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        double s = 1.0 - t;
        return Color.FromRgb(
            (byte)(a.R * s + b.R * t),
            (byte)(a.G * s + b.G * t),
            (byte)(a.B * s + b.B * t));
    }

    private static SolidColorBrush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
