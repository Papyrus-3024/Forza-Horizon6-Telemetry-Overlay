using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Four-corner suspension widget. Each corner shows normalized travel (0 = full droop,
/// 1 = full compression) as a bottom-anchored bar against a neutral mid reference line.
/// Colour codes the load: blue when light/airborne, green mid, amber high, red bottomed.
/// </summary>
public partial class SuspensionWidget : UserControl
{
    private const double TrackHeight = 34.0;

    public static readonly DependencyProperty SuspFLProperty = Reg(nameof(SuspFL));
    public static readonly DependencyProperty SuspFRProperty = Reg(nameof(SuspFR));
    public static readonly DependencyProperty SuspRLProperty = Reg(nameof(SuspRL));
    public static readonly DependencyProperty SuspRRProperty = Reg(nameof(SuspRR));

    public double SuspFL { get => (double)GetValue(SuspFLProperty); set => SetValue(SuspFLProperty, value); }
    public double SuspFR { get => (double)GetValue(SuspFRProperty); set => SetValue(SuspFRProperty, value); }
    public double SuspRL { get => (double)GetValue(SuspRLProperty); set => SetValue(SuspRLProperty, value); }
    public double SuspRR { get => (double)GetValue(SuspRRProperty); set => SetValue(SuspRRProperty, value); }

    // Semantic load colours (not theme-derived).
    private static readonly Color ColLight = Color.FromRgb(0x4F, 0x95, 0xE0); // light / airborne
    private static readonly Color ColMid   = Color.FromRgb(0x2B, 0xBD, 0x74); // normal
    private static readonly Color ColHigh  = Color.FromRgb(0xE0, 0x9C, 0x3A); // high load
    private static readonly Color ColBottom= Color.FromRgb(0xEF, 0x43, 0x32); // bottomed out

    public SuspensionWidget() => InitializeComponent();

    private static DependencyProperty Reg(string name) =>
        DependencyProperty.Register(name, typeof(double), typeof(SuspensionWidget),
            new PropertyMetadata(0.0, OnChanged));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SuspensionWidget)d).Redraw();

    private void Redraw()
    {
        Apply(BarFL, SuspFL);
        Apply(BarFR, SuspFR);
        Apply(BarRL, SuspRL);
        Apply(BarRR, SuspRR);
    }

    private static void Apply(Border bar, double value)
    {
        double v = Math.Clamp(double.IsNaN(value) ? 0.0 : value, 0.0, 1.0);
        bar.Height = v * TrackHeight;
        bar.Background = new SolidColorBrush(LoadColor(v));
    }

    private static Color LoadColor(double v) =>
        v < 0.15 ? ColLight
      : v < 0.80 ? ColMid
      : v < 0.93 ? ColHigh
      : ColBottom;
}
