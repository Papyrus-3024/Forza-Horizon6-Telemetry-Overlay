using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Arc tachometer: a 270° sweep (8-o'clock to 4-o'clock) with a dim track, a green
/// power band, a red redline zone, and an eased gradient value fill with a warm glow.
/// The current gear sits in the centre; a shift-light strip and "cur / max" rpm
/// readout frame it. The headline gauge of the V2 bottom-centre cluster.
/// </summary>
public partial class ArcTachWidget : UserControl
{
    // ── Dependency property ───────────────────────────────────────────────────

    public static readonly DependencyProperty RpmFractionProperty =
        DependencyProperty.Register(
            nameof(RpmFraction),
            typeof(double),
            typeof(ArcTachWidget),
            new PropertyMetadata(0.0, OnRpmChanged));

    /// <summary>Eased rpm fraction 0.0–1.0.</summary>
    public double RpmFraction
    {
        get => (double)GetValue(RpmFractionProperty);
        set => SetValue(RpmFractionProperty, value);
    }

    // ── Arc geometry ──────────────────────────────────────────────────────────

    // 270° sweep starting at 8-o'clock (135°, clockwise) and ending at 4-o'clock.
    private const double SweepDeg = 270.0;
    private const double StartDeg = 135.0;

    // Warm glow on the value fill (matches the mockup's drop-shadow on the tach fill).
    private readonly DropShadowEffect _glow = new()
    {
        ShadowDepth = 0,
        BlurRadius  = 9,
        Opacity     = 0.7,
        Color       = Color.FromRgb(0xFF, 0x90, 0x40),
    };

    // Arc centre/radius, recomputed only when the size changes.
    private double _cx, _cy, _r;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ArcTachWidget()
    {
        InitializeComponent();
        FillArc.Effect = _glow;
        Loaded += (_, _) => Layout();
    }

    private static void OnRpmChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ArcTachWidget)d).UpdateFill();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Layout();

    // ── Static layout (size-dependent only) ────────────────────────────────────

    private void Layout()
    {
        double w = ArcField.Width;
        double h = ArcField.Height;
        if (w <= 0 || h <= 0) return;

        _cx = w / 2.0;
        _cy = h / 2.0 + 2.0;
        _r  = Math.Min(w, h) / 2.0 - 8.0;

        // Track / band / redline never change with rpm — build them once per size.
        TrackArc.Data   = ArcPath(_cx, _cy, _r, StartDeg, SweepDeg);
        BandArc.Data    = ArcPath(_cx, _cy, _r, StartDeg + 0.62 * SweepDeg, 0.24 * SweepDeg);
        RedlineArc.Data = ArcPath(_cx, _cy, _r, StartDeg + 0.90 * SweepDeg, 0.10 * SweepDeg);

        // Centre the gear block on the arc centre.
        CenterBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var bs = CenterBlock.DesiredSize;
        Canvas.SetLeft(CenterBlock, _cx - bs.Width / 2.0);
        Canvas.SetTop(CenterBlock,  _cy - bs.Height / 2.0);

        UpdateFill();
    }

    // ── Per-frame fill update (cheap) ───────────────────────────────────────────

    private void UpdateFill()
    {
        if (_r <= 0) return;

        double frac = Math.Clamp(double.IsNaN(RpmFraction) ? 0.0 : RpmFraction, 0.0, 1.0);
        if (frac > 0.005)
        {
            FillArc.Data = ArcPath(_cx, _cy, _r, StartDeg, frac * SweepDeg);
            FillArc.Visibility = Visibility.Visible;
        }
        else
        {
            FillArc.Visibility = Visibility.Collapsed;
        }
    }

    // ── Arc geometry helpers ────────────────────────────────────────────────────

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
}
