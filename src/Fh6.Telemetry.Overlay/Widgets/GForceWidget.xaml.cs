using System.Windows;
using System.Windows.Controls;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class GForceWidget : UserControl
{
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

    public GForceWidget() => InitializeComponent();

    private static void OnGChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((GForceWidget)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        double w = Field.ActualWidth;
        double h = Field.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double cx = w / 2.0;
        double cy = h / 2.0;

        // Ring: the 1g ring sits at 1/1.5 of the half-width
        double ringRadius = cx / 1.5;
        double ringDiam   = ringRadius * 2.0;
        Ring.Width  = ringDiam;
        Ring.Height = ringDiam;
        Canvas.SetLeft(Ring, cx - ringRadius);
        Canvas.SetTop(Ring,  cy - ringRadius);

        // Crosshair lines
        HLine.X1 = 0;  HLine.Y1 = cy; HLine.X2 = w; HLine.Y2 = cy;
        VLine.X1 = cx; VLine.Y1 = 0;  VLine.X2 = cx; VLine.Y2 = h;

        // Dot: ±1.5g maps to ring edge (cx from centre)
        double dotOffX = Math.Clamp(LatG  / 1.5, -1.0, 1.0) * cx;
        double dotOffY = Math.Clamp(LongG / 1.5, -1.0, 1.0) * cy;

        // Clamp to circle (so diagonal G beyond 1.5g still stays inside)
        double magnitude = Math.Sqrt(dotOffX * dotOffX + dotOffY * dotOffY);
        if (magnitude > cx)
        {
            double scale = cx / magnitude;
            dotOffX *= scale;
            dotOffY *= scale;
        }

        double dotHalf = Dot.Width / 2.0;
        Canvas.SetLeft(Dot, cx + dotOffX - dotHalf);
        Canvas.SetTop(Dot,  cy - dotOffY - dotHalf);   // -dotOffY because screen Y is inverted
    }
}
