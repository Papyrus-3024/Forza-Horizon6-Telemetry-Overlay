using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class GForceWidget : UserControl
{
    // ── Dependency properties ────────────────────────────────────────────────

    public static readonly DependencyProperty LatGProperty =
        DependencyProperty.Register(
            nameof(LatG),
            typeof(double),
            typeof(GForceWidget),
            new PropertyMetadata(0.0, OnGChanged));

    public static readonly DependencyProperty LongGProperty =
        DependencyProperty.Register(
            nameof(LongG),
            typeof(double),
            typeof(GForceWidget),
            new PropertyMetadata(0.0, OnGChanged));

    public double LatG
    {
        get => (double)GetValue(LatGProperty);
        set => SetValue(LatGProperty, value);
    }

    public double LongG
    {
        get => (double)GetValue(LongGProperty);
        set => SetValue(LongGProperty, value);
    }

    // ── Trail ring buffer ────────────────────────────────────────────────────

    private const int TrailLength = 20;

    // Pre-allocated ring buffer of canvas positions.
    private readonly double[] _trailX = new double[TrailLength];
    private readonly double[] _trailY = new double[TrailLength];
    private int _trailHead;      // next write index
    private int _trailCount;     // how many valid entries

    // Reusable PointCollection for the trail polyline — rebuilt in-place each frame.
    private readonly PointCollection _trailPoints = new(TrailLength);

    // ── Peak-hold state ──────────────────────────────────────────────────────

    private double _peakMag;
    private double _peakCanvasX;
    private double _peakCanvasY;
    private double _peakLastBeatTime = double.MinValue; // Environment.TickCount64 ms

    private const double PeakHoldMs = 3000.0;

    // ── Brushes (resolved once, from theme or fallback) ──────────────────────

    // Frozen semantic brushes (never change, no allocation per frame).
    private static readonly SolidColorBrush FallbackGood   = Freeze(Color.FromRgb(0x5A, 0xD1, 0x5A));
    private static readonly SolidColorBrush FallbackWarn   = Freeze(Color.FromRgb(0xE0, 0xC9, 0x3A));
    private static readonly SolidColorBrush FallbackDanger = Freeze(Color.FromRgb(0xE0, 0x5A, 0x5A));

    // ── Visual elements (created in constructor) ──────────────────────────────

    private Polyline? _trail;
    private Line?     _tether;
    private Ellipse?  _peakMarker;

    // ── Constructor ──────────────────────────────────────────────────────────

    // Glow halo on the live dot, recolored to match the semantic grip color.
    private readonly DropShadowEffect _dotGlow = new() { ShadowDepth = 0, BlurRadius = 11, Opacity = 0.85 };

    public GForceWidget()
    {
        InitializeComponent();
        BuildOverlayShapes();
        Dot.Effect = _dotGlow;
    }

    // ── Shape setup ──────────────────────────────────────────────────────────

    private void BuildOverlayShapes()
    {
        // Trail polyline — behind everything except the static ring/crosshair.
        // Insert before the Dot so it renders below it.
        _trail = new Polyline
        {
            Points          = _trailPoints,
            StrokeThickness = 2,
            StrokeLineJoin  = PenLineJoin.Round,
            IsHitTestVisible = false,
            // Stroke is reassigned each frame to avoid creating brushes per-frame;
            // we update Stroke once in ResolveBrushes and reuse it.
            Stroke          = new SolidColorBrush(Color.FromArgb(0x80, 0x5A, 0xD1, 0x5A)),
        };

        // Tether — thin line from center to dot, behind the dot.
        _tether = new Line
        {
            StrokeThickness  = 1,
            Stroke           = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
            IsHitTestVisible = false,
        };

        // Peak marker — small hollow circle.
        _peakMarker = new Ellipse
        {
            Width            = 8,
            Height           = 8,
            Fill             = Brushes.Transparent,
            Stroke           = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0x80)),
            StrokeThickness  = 1.5,
            IsHitTestVisible = false,
            Visibility       = Visibility.Collapsed,
        };

        // Insert: trail first (lowest), then tether, then the existing Dot, then peak marker.
        // Field already has: Ring, HLine, VLine, Dot (in XAML order → z-order).
        // We insert trail + tether BEFORE Dot, peak marker AFTER Dot.
        int dotIndex = Field.Children.IndexOf(Dot);
        if (dotIndex >= 0)
        {
            Field.Children.Insert(dotIndex, _trail);
            Field.Children.Insert(dotIndex + 1, _tether); // now trail, tether, Dot
            Field.Children.Add(_peakMarker);              // above Dot
        }
        else
        {
            // Fallback: just append all.
            Field.Children.Add(_trail);
            Field.Children.Add(_tether);
            Field.Children.Add(_peakMarker);
        }
    }

    // ── Property change / size change ─────────────────────────────────────────

    private static void OnGChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GForceWidget)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    // ── Redraw ────────────────────────────────────────────────────────────────

    private void Redraw()
    {
        double w = Field.ActualWidth;
        double h = Field.ActualHeight;
        if (w <= 0 || h <= 0) return;
        if (double.IsNaN(w) || double.IsNaN(h)) return;

        double cx = w / 2.0;
        double cy = h / 2.0;

        // ── Ring ─────────────────────────────────────────────────────────────
        double ringRadius = cx / 1.5;
        double ringDiam   = ringRadius * 2.0;
        Ring.Width  = ringDiam;
        Ring.Height = ringDiam;
        Canvas.SetLeft(Ring, cx - ringRadius);
        Canvas.SetTop(Ring,  cy - ringRadius);

        // ── Crosshair ─────────────────────────────────────────────────────────
        HLine.X1 = 0;  HLine.Y1 = cy; HLine.X2 = w; HLine.Y2 = cy;
        VLine.X1 = cx; VLine.Y1 = 0;  VLine.X2 = cx; VLine.Y2 = h;

        // ── Dot position ──────────────────────────────────────────────────────
        double rawLat  = double.IsNaN(LatG)  ? 0.0 : LatG;
        double rawLong = double.IsNaN(LongG) ? 0.0 : LongG;

        double dotOffX = Math.Clamp(rawLat  / 1.5, -1.0, 1.0) * cx;
        double dotOffY = Math.Clamp(rawLong / 1.5, -1.0, 1.0) * cy;

        // Clamp to circle.
        double clampLen = Math.Sqrt(dotOffX * dotOffX + dotOffY * dotOffY);
        if (clampLen > cx)
        {
            double scale = cx / clampLen;
            dotOffX *= scale;
            dotOffY *= scale;
        }

        double dotHalf = Dot.Width / 2.0;
        double dotCanvasX = cx + dotOffX;
        double dotCanvasY = cy - dotOffY; // screen Y is inverted
        Canvas.SetLeft(Dot, dotCanvasX - dotHalf);
        Canvas.SetTop(Dot,  dotCanvasY - dotHalf);

        // ── Magnitude for dot color ────────────────────────────────────────────
        // Use actual g values (not clamped canvas coords).
        double mag = Math.Sqrt(rawLat * rawLat + rawLong * rawLong);

        // Semantic grip color (not theme accent): green inside, amber near, red beyond the 1g ring.
        var dotBrush = mag > 1.0 ? FallbackDanger
                     : mag > 0.85 ? FallbackWarn
                     : FallbackGood;
        Dot.Fill      = dotBrush;
        _dotGlow.Color = dotBrush.Color;

        // ── Tether ────────────────────────────────────────────────────────────
        if (_tether is not null)
        {
            _tether.X1 = cx;
            _tether.Y1 = cy;
            _tether.X2 = dotCanvasX;
            _tether.Y2 = dotCanvasY;
        }

        // ── Trail ─────────────────────────────────────────────────────────────
        // Push current dot canvas position into ring buffer.
        _trailX[_trailHead] = dotCanvasX;
        _trailY[_trailHead] = dotCanvasY;
        _trailHead = (_trailHead + 1) % TrailLength;
        if (_trailCount < TrailLength) _trailCount++;

        UpdateTrailVisual();

        // ── Peak marker ───────────────────────────────────────────────────────
        UpdatePeak(mag, dotCanvasX, dotCanvasY);
    }

    // ── Trail visual ─────────────────────────────────────────────────────────

    private void UpdateTrailVisual()
    {
        if (_trail is null || _trailCount == 0) return;

        // Rebuild PointCollection in oldest-to-newest order.
        // Oldest entry: (_trailHead) when buffer is full, or 0 when not yet full.
        int oldest = _trailCount < TrailLength ? 0 : _trailHead;

        _trailPoints.Clear();
        for (int i = 0; i < _trailCount; i++)
        {
            int idx = (oldest + i) % TrailLength;
            _trailPoints.Add(new Point(_trailX[idx], _trailY[idx]));
        }

        // Build a stroke with fading opacity — newest point = full; oldest = low.
        // We use a single brush at moderate opacity; for true per-segment fade we'd
        // need per-segment polylines, but that's expensive. Instead use a LinearGradientBrush
        // along the length. Approximate with fixed moderate alpha for visual warmth.
        // Real per-vertex alpha requires Geometry + GradientBrush along path.
        // We'll use a simple opacity on the whole polyline scaled to trail fill.
        _trail.Opacity = 0.55;
    }

    // ── Peak marker ──────────────────────────────────────────────────────────

    private void UpdatePeak(double mag, double canvasX, double canvasY)
    {
        if (_peakMarker is null) return;

        double nowMs = Environment.TickCount64;

        if (mag >= _peakMag)
        {
            _peakMag        = mag;
            _peakCanvasX    = canvasX;
            _peakCanvasY    = canvasY;
            _peakLastBeatTime = nowMs;
        }
        else if (_peakMag > 0.1 && (nowMs - _peakLastBeatTime) > PeakHoldMs)
        {
            // Decay: nudge peak toward current position after hold expires.
            _peakMag *= 0.92;
            _peakCanvasX = (_peakCanvasX * 3 + canvasX) / 4.0;
            _peakCanvasY = (_peakCanvasY * 3 + canvasY) / 4.0;
            if (_peakMag < 0.05)
            {
                _peakMag = 0;
                _peakMarker.Visibility = Visibility.Collapsed;
                return;
            }
        }

        if (_peakMag < 0.05)
        {
            _peakMarker.Visibility = Visibility.Collapsed;
            return;
        }

        double pmHalf = _peakMarker.Width / 2.0;
        Canvas.SetLeft(_peakMarker, _peakCanvasX - pmHalf);
        Canvas.SetTop(_peakMarker,  _peakCanvasY - pmHalf);
        _peakMarker.Visibility = Visibility.Visible;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SolidColorBrush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
