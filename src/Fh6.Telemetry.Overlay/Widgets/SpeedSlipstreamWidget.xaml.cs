using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Horizontal motion-streak backdrop that conveys vehicle speed.
/// A fixed pool of <see cref="Line"/> elements is recycled each frame —
/// no per-frame heap allocation occurs in the hot path once the pool is built.
/// Streaks scroll left at a rate proportional to SpeedMph; at zero speed they are
/// nearly invisible (low opacity, short length); at ~200 mph they are full intensity.
/// </summary>
public partial class SpeedSlipstreamWidget : UserControl
{
    // ── Pool constants ────────────────────────────────────────────────────────

    // Number of streak lines in the pre-built pool.
    private const int PoolSize = 28;

    // Maximum speed for full-intensity effect (mph).
    private const double MaxSpeedMph = 200.0;

    // Pixel/second speed of streaks at MaxSpeedMph.
    private const double MaxPixelSpeed = 520.0;

    // Streak length range (pixels, short at rest → long at speed).
    private const double MinStreakLen = 10.0;
    private const double MaxStreakLen = 90.0;

    // Opacity range.
    private const double MinOpacity = 0.04;
    private const double MaxOpacity = 0.70;

    // ── Dependency property ───────────────────────────────────────────────────

    public static readonly DependencyProperty SpeedMphProperty =
        DependencyProperty.Register(
            nameof(SpeedMph),
            typeof(double),
            typeof(SpeedSlipstreamWidget),
            new PropertyMetadata(0.0));

    /// <summary>Vehicle speed in mph (0–200+). Drives streak intensity.</summary>
    public double SpeedMph
    {
        get => (double)GetValue(SpeedMphProperty);
        set => SetValue(SpeedMphProperty, value);
    }

    // ── Streak state ──────────────────────────────────────────────────────────

    // Each streak tracks its right-edge X position and assigned canvas row Y.
    private readonly double[] _streakX   = new double[PoolSize];
    private readonly double[] _streakY   = new double[PoolSize];
    private readonly double[] _streakLen = new double[PoolSize];
    private readonly Line[]   _lines     = new Line[PoolSize];

    // Reuse one frozen accent brush (tinted blue-white for speed feel).
    private static readonly SolidColorBrush StreakBrush = FreezeS(Color.FromRgb(0x6A, 0xA8, 0xE0));

    private long _lastTickMs;
    private bool _rendering;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SpeedSlipstreamWidget()
    {
        InitializeComponent();
        BuildPool();
    }

    // ── Pool build (once, on construction) ───────────────────────────────────

    private void BuildPool()
    {
        var rng = new Random(42);
        for (int i = 0; i < PoolSize; i++)
        {
            var line = new Line
            {
                Stroke          = StreakBrush,
                StrokeThickness = 1.0 + rng.NextDouble() * 0.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round,
                Opacity = 0,
            };
            _lines[i]     = line;
            _streakLen[i] = MinStreakLen + rng.NextDouble() * (MaxStreakLen - MinStreakLen);
            _streakX[i]   = rng.NextDouble();  // [0,1] — scaled to width on first frame
            _streakY[i]   = rng.NextDouble();  // [0,1] — scaled to height on first frame
            StreamCanvas.Children.Add(line);
        }
    }

    // ── Loaded / Unloaded — hook rendering ───────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_rendering) return;
        _rendering = true;
        _lastTickMs = Environment.TickCount64;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _rendering = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Re-scatter streaks across new dimensions so they don't cluster at old positions.
        double w = StreamCanvas.ActualWidth;
        double h = StreamCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;
        var rng = new Random(Environment.TickCount);
        for (int i = 0; i < PoolSize; i++)
        {
            _streakX[i] = rng.NextDouble() * w;
            _streakY[i] = rng.NextDouble() * h;
        }
    }

    // ── Per-frame hot path (no allocation) ───────────────────────────────────

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsVisible) return;

        double w = StreamCanvas.ActualWidth;
        double h = StreamCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        long nowMs = Environment.TickCount64;
        double dt  = Math.Clamp((nowMs - _lastTickMs) / 1000.0, 0.0, 0.1);
        _lastTickMs = nowMs;

        double speed   = Math.Clamp(SpeedMph, 0.0, MaxSpeedMph);
        double t       = speed / MaxSpeedMph;               // 0..1 intensity
        double pixSec  = t * MaxPixelSpeed;                 // px/s scroll rate
        double opacity = MinOpacity + t * (MaxOpacity - MinOpacity);
        double lenMul  = MinStreakLen + t * (MaxStreakLen - MinStreakLen);

        double dx = pixSec * dt;

        for (int i = 0; i < PoolSize; i++)
        {
            // Scroll left.
            _streakX[i] -= dx;

            // Wrap: when the right edge passes off the left, respawn at right edge.
            if (_streakX[i] + _streakLen[i] < 0)
            {
                _streakX[i] = w + _streakLen[i] * 0.5;
                _streakY[i] = (i * (h / PoolSize) + (h / PoolSize) * 0.3)
                              % h;  // distribute evenly with slight jitter
            }

            double len   = _streakLen[i] * (0.5 + 0.5 * t);
            double rightX = _streakX[i];
            double leftX  = rightX - len;
            double y      = _streakY[i];

            var ln = _lines[i];
            ln.X1      = leftX;
            ln.Y1      = y;
            ln.X2      = rightX;
            ln.Y2      = y;
            ln.Opacity = opacity * (0.6 + 0.4 * ((double)(i % 7) / 6.0));
        }
    }

    private static SolidColorBrush FreezeS(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
