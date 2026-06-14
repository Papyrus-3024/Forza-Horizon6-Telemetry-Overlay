using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class PedalsSteerWidget : UserControl
{
    private const double BarHeight = 56.0;
    private const double PeakHoldMs = 900.0;    // hold the ghost, then let it fall
    private const double PeakFallPerSec = 1.2;  // fraction/sec decay after hold

    // ── Dependency properties (eased pedal fractions 0..1) ───────────────────────

    public static readonly DependencyProperty ThrottleProperty = Reg(nameof(Throttle));
    public static readonly DependencyProperty BrakeProperty    = Reg(nameof(Brake));
    public static readonly DependencyProperty ClutchProperty   = Reg(nameof(Clutch));

    public double Throttle { get => (double)GetValue(ThrottleProperty); set => SetValue(ThrottleProperty, value); }
    public double Brake    { get => (double)GetValue(BrakeProperty);    set => SetValue(BrakeProperty, value); }
    public double Clutch   { get => (double)GetValue(ClutchProperty);   set => SetValue(ClutchProperty, value); }

    private static DependencyProperty Reg(string name) =>
        DependencyProperty.Register(name, typeof(double), typeof(PedalsSteerWidget),
            new PropertyMetadata(0.0, OnChanged));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PedalsSteerWidget)d).UpdatePedals();

    // ── Per-pedal peak-hold state ────────────────────────────────────────────────

    private double _thrPeak, _brkPeak, _cltPeak;
    private long _thrPeakMs, _brkPeakMs, _cltPeakMs;

    // Glow halos on the fills (fixed semantic colour, opacity tracks pressure).
    private readonly DropShadowEffect _thrGlow = Glow(0x5A, 0xD1, 0x5A);
    private readonly DropShadowEffect _brkGlow = Glow(0xE0, 0x5A, 0x5A);
    private readonly DropShadowEffect _cltGlow = Glow(0x6A, 0xA8, 0xE0);

    public PedalsSteerWidget()
    {
        InitializeComponent();
        ThrFill.Effect = _thrGlow;
        BrkFill.Effect = _brkGlow;
        CltFill.Effect = _cltGlow;
    }

    private void UpdatePedals()
    {
        long now = Environment.TickCount64;
        Apply(ThrFill, ThrGhost, _thrGlow, Throttle, ref _thrPeak, ref _thrPeakMs, now);
        Apply(BrkFill, BrkGhost, _brkGlow, Brake,    ref _brkPeak, ref _brkPeakMs, now);
        Apply(CltFill, CltGhost, _cltGlow, Clutch,   ref _cltPeak, ref _cltPeakMs, now);
    }

    private static void Apply(Border fill, Border ghost, DropShadowEffect glow,
                              double value, ref double peak, ref long peakMs, long now)
    {
        double v = Math.Clamp(double.IsNaN(value) ? 0.0 : value, 0.0, 1.0);
        fill.Height = v * BarHeight;
        glow.Opacity = v > 0.02 ? 0.8 : 0.0;

        // Peak-hold ghost: jump up to new peaks, hold briefly, then fall.
        if (v >= peak)
        {
            peak = v;
            peakMs = now;
        }
        else if (now - peakMs > PeakHoldMs)
        {
            // Decay assumes ~60 Hz updates; small, stable step.
            peak = Math.Max(v, peak - PeakFallPerSec / 60.0);
        }

        if (peak > 0.05 && peak > v + 0.02)
        {
            ghost.Visibility = Visibility.Visible;
            ghost.Margin = new Thickness(0, 0, 0, peak * BarHeight);
        }
        else
        {
            ghost.Visibility = Visibility.Collapsed;
        }
    }

    private static DropShadowEffect Glow(byte r, byte g, byte b) =>
        new() { ShadowDepth = 0, BlurRadius = 9, Opacity = 0, Color = Color.FromRgb(r, g, b) };
}
