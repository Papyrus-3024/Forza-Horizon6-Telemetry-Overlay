using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Radial boost gauge with eased arc fill and session peak-hold tick.
/// Range: -15 psi (vacuum) to +25 psi (positive boost).
/// The arc sweeps 220° (−110° to +110° from the 6-o'clock position,
/// starting at ~8-o'clock and ending at ~4-o'clock).
/// Negative boost (vacuum) fills to the left of the 0 psi point.
/// </summary>
public partial class BoostWidget : UserControl
{
    // ── Dependency property ───────────────────────────────────────────────────

    public static readonly DependencyProperty DisplayedBoostRawProperty =
        DependencyProperty.Register(
            nameof(DisplayedBoostRaw),
            typeof(double),
            typeof(BoostWidget),
            new PropertyMetadata(0.0, OnBoostChanged));

    public double DisplayedBoostRaw
    {
        get => (double)GetValue(DisplayedBoostRawProperty);
        set => SetValue(DisplayedBoostRawProperty, value);
    }

    // ── Arc geometry constants ────────────────────────────────────────────────

    // Total sweep of the arc in degrees (220° — leaves a gap at the bottom).
    private const double SweepDeg   = 220.0;
    // Where the arc starts (degrees from positive X-axis, clockwise).
    // 0 = east; 90 = south. Start at 8-o'clock = 160°.
    private const double StartDeg   = 160.0;

    // Boost range
    private const double BoostMin   = -15.0; // psi
    private const double BoostMax   =  25.0; // psi
    private const double BoostRange =  BoostMax - BoostMin; // 40 psi total

    // ── Brushes ───────────────────────────────────────────────────────────────

    // Positive boost: blue → cyan gradient
    private static readonly LinearGradientBrush PositiveBrush = MakeGradient(
        Color.FromRgb(0x6F, 0xB6, 0xFF),
        Color.FromRgb(0x2F, 0x7F, 0xD6));

    // Negative boost (vacuum): amber
    private static readonly SolidColorBrush VacuumBrush = FreezeS(Color.FromRgb(0xE0, 0xC9, 0x3A));

    // ── Peak-hold state ───────────────────────────────────────────────────────

    private double _peakBoost      = double.MinValue;
    private double _peakLastUpdate = double.MinValue;
    private const double PeakHoldMs   = 3000.0;
    private const double PeakDecayMs  = 1500.0;

    // Color-matched glow halo on the fill arc (updated as boost sign changes).
    private readonly DropShadowEffect _glow = new() { ShadowDepth = 0, BlurRadius = 9, Opacity = 0.8 };

    // ── Constructor ───────────────────────────────────────────────────────────

    // Arc centre/radius, recomputed only when the size changes.
    private double _cx, _cy, _r;

    public BoostWidget()
    {
        InitializeComponent();
        FillArc.Effect = _glow;
        Loaded += (_, _) => Layout();
    }

    // ── Property / size change ────────────────────────────────────────────────

    private static void OnBoostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((BoostWidget)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Layout();

    // ── Static layout (size-dependent only) ────────────────────────────────────

    private void Layout()
    {
        double w = ArcField.Width;
        double h = ArcField.Height;
        if (w <= 0 || h <= 0) return;

        _cx = w / 2.0;
        _cy = h / 2.0 + 4.0; // shift center down slightly so arc is visible
        _r  = Math.Min(w, h) / 2.0 - 6.0;

        // Track arc (full range, dim) never changes with boost — build once per size.
        TrackArc.Data = ArcPath(_cx, _cy, _r, StartDeg, SweepDeg);

        // Position center block in the canvas (static).
        double blockW = 50;
        double blockH = 36;
        Canvas.SetLeft(CenterBlock, _cx - blockW / 2.0);
        Canvas.SetTop(CenterBlock,  _cy - blockH / 2.0 - 4.0);
        CenterBlock.Width = blockW;

        Redraw();
    }

    // ── Redraw (per-frame value update) ─────────────────────────────────────────

    private void Redraw()
    {
        if (_r <= 0) return;

        double cx = _cx, cy = _cy, r = _r;

        // ── Fill arc ──────────────────────────────────────────────────────────
        double boostRaw = double.IsNaN(DisplayedBoostRaw) ? 0.0 : DisplayedBoostRaw;
        double boostClamped = Math.Clamp(boostRaw, BoostMin, BoostMax);

        double fillFraction = (boostClamped - BoostMin) / BoostRange;
        double fillDeg = fillFraction * SweepDeg;

        if (fillDeg > 0.5)
        {
            FillArc.Data   = ArcPath(cx, cy, r, StartDeg, fillDeg);
            FillArc.Stroke = boostClamped >= 0 ? PositiveBrush : VacuumBrush;
            _glow.Color    = boostClamped >= 0
                ? Color.FromRgb(0x6F, 0xB6, 0xFF)
                : Color.FromRgb(0xE0, 0xC9, 0x3A);
            FillArc.Visibility = Visibility.Visible;
        }
        else
        {
            FillArc.Visibility = Visibility.Collapsed;
        }

        // ── Center label ──────────────────────────────────────────────────────
        BoostValue.Text = $"{boostRaw:F1}";

        // ── Peak tick ─────────────────────────────────────────────────────────
        UpdatePeak(boostRaw, cx, cy, r);
    }

    // ── Peak hold ────────────────────────────────────────────────────────────

    private void UpdatePeak(double boost, double cx, double cy, double r)
    {
        double nowMs = Environment.TickCount64;

        if (boost > _peakBoost)
        {
            _peakBoost       = boost;
            _peakLastUpdate  = nowMs;
        }
        else if (_peakBoost > BoostMin + 1.0)
        {
            double elapsed = nowMs - _peakLastUpdate;
            if (elapsed > PeakHoldMs + PeakDecayMs)
            {
                // Reset
                _peakBoost = double.MinValue;
                PeakTick.Visibility = Visibility.Collapsed;
                return;
            }
        }

        if (_peakBoost <= BoostMin + 0.5)
        {
            PeakTick.Visibility = Visibility.Collapsed;
            return;
        }

        double peakFraction = Math.Clamp((_peakBoost - BoostMin) / BoostRange, 0.0, 1.0);
        double peakAngleDeg = StartDeg + peakFraction * SweepDeg;
        var (px, py) = PointOnArc(cx, cy, r, peakAngleDeg);
        var (ix, iy) = PointOnArc(cx, cy, r - 6.0, peakAngleDeg);

        PeakTick.X1 = px; PeakTick.Y1 = py;
        PeakTick.X2 = ix; PeakTick.Y2 = iy;
        PeakTick.Visibility = Visibility.Visible;
    }

    // ── Arc geometry helpers ───────────────────────────────────────────────────

    /// <summary>Builds a PathGeometry for an open arc stroke.</summary>
    private static Geometry ArcPath(double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        if (sweepDeg <= 0) return Geometry.Empty;

        var (sx, sy) = PointOnArc(cx, cy, r, startDeg);
        var (ex, ey) = PointOnArc(cx, cy, r, startDeg + sweepDeg);

        bool isLarge = sweepDeg > 180.0;

        var seg = new ArcSegment(
            new Point(ex, ey),
            new Size(r, r),
            0,
            isLarge,
            SweepDirection.Clockwise,
            true);

        var fig = new PathFigure(new Point(sx, sy), new[] { seg }, false);
        return new PathGeometry(new[] { fig });
    }

    private static (double x, double y) PointOnArc(double cx, double cy, double r, double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180.0;
        return (cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    // ── Brush helpers ─────────────────────────────────────────────────────────

    private static LinearGradientBrush MakeGradient(Color from, Color to)
    {
        var b = new LinearGradientBrush(from, to, 0.0);
        b.Freeze();
        return b;
    }

    private static SolidColorBrush FreezeS(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
