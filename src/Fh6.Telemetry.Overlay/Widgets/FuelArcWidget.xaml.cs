using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Arc gauge displaying fuel level 0–100%.
/// Arc sweeps 220° (same geometry as BoostWidget).
/// Colour: green (healthy) → amber (≤30%) → red (≤15%).
/// </summary>
public partial class FuelArcWidget : UserControl
{
    // Arc geometry — matches BoostWidget conventions.
    private const double SweepDeg = 220.0;
    private const double StartDeg = 160.0;   // 8-o'clock

    // Colour thresholds
    private const double LowThreshold      = 0.30;   // amber below 30 %
    private const double CriticalThreshold = 0.15;   // red below 15 %

    // ── Brushes ───────────────────────────────────────────────────────────────
    private static readonly SolidColorBrush BrushHealthy  = FreezeS(Color.FromRgb(0x46, 0xE0, 0x8A));
    private static readonly SolidColorBrush BrushAmber    = FreezeS(Color.FromRgb(0xFF, 0xC2, 0x4B));
    private static readonly SolidColorBrush BrushCritical = FreezeS(Color.FromRgb(0xFF, 0x40, 0x40));

    // ── Dependency property ───────────────────────────────────────────────────

    public static readonly DependencyProperty FuelFractionProperty =
        DependencyProperty.Register(
            nameof(FuelFraction),
            typeof(double),
            typeof(FuelArcWidget),
            new PropertyMetadata(0.0, OnFuelChanged));

    /// <summary>Fuel level 0.0 (empty) to 1.0 (full).</summary>
    public double FuelFraction
    {
        get => (double)GetValue(FuelFractionProperty);
        set => SetValue(FuelFractionProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    // Color-matched glow halo on the fill arc.
    private readonly DropShadowEffect _glow = new() { ShadowDepth = 0, BlurRadius = 9, Opacity = 0.8 };

    public FuelArcWidget()
    {
        InitializeComponent();
        FillArc.Effect = _glow;
        Loaded += (_, _) => Redraw();
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private static void OnFuelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FuelArcWidget)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    // ── Redraw ────────────────────────────────────────────────────────────────

    private void Redraw()
    {
        double w = ArcField.Width;
        double h = ArcField.Height;
        if (w <= 0 || h <= 0) return;

        double cx = w / 2.0;
        double cy = h / 2.0 + 4.0;
        double r  = Math.Min(w, h) / 2.0 - 6.0;

        // Track arc (dim full ring).
        TrackArc.Data = ArcPath(cx, cy, r, StartDeg, SweepDeg);

        double fraction = Math.Clamp(double.IsNaN(FuelFraction) ? 0.0 : FuelFraction, 0.0, 1.0);
        double fillDeg  = fraction * SweepDeg;

        if (fillDeg > 0.5)
        {
            FillArc.Data   = ArcPath(cx, cy, r, StartDeg, fillDeg);
            var fillColor  = fraction <= CriticalThreshold ? BrushCritical
                           : fraction <= LowThreshold      ? BrushAmber
                                                           : BrushHealthy;
            FillArc.Stroke = fillColor;
            _glow.Color    = fillColor.Color;
            FillArc.Visibility = Visibility.Visible;
        }
        else
        {
            FillArc.Visibility = Visibility.Collapsed;
        }

        // Centre label.
        FuelValue.Text = $"{fraction * 100:F0}";

        double blockW = 50;
        double blockH = 36;
        Canvas.SetLeft(CenterBlock, cx - blockW / 2.0);
        Canvas.SetTop(CenterBlock,  cy - blockH / 2.0 - 4.0);
        CenterBlock.Width = blockW;
    }

    // ── Arc geometry ──────────────────────────────────────────────────────────

    private static Geometry ArcPath(double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        if (sweepDeg <= 0) return Geometry.Empty;

        var (sx, sy) = PointOnArc(cx, cy, r, startDeg);
        var (ex, ey) = PointOnArc(cx, cy, r, startDeg + sweepDeg);
        bool isLarge = sweepDeg > 180.0;

        var seg = new ArcSegment(new Point(ex, ey), new Size(r, r), 0, isLarge, SweepDirection.Clockwise, true);
        var fig = new PathFigure(new Point(sx, sy), new[] { seg }, false);
        return new PathGeometry(new[] { fig });
    }

    private static (double x, double y) PointOnArc(double cx, double cy, double r, double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180.0;
        return (cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    private static SolidColorBrush FreezeS(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
