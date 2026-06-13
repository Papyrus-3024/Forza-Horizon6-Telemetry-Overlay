using System.Windows;
using System.Windows.Controls;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class SteeringBar : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(SteeringBar),
            new PropertyMetadata(0.0, OnValueChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public SteeringBar() => InitializeComponent();

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SteeringBar)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        double w = Root.ActualWidth;
        if (w <= 0) return;
        double half = w / 2.0;
        Canvas.SetLeft(CenterTick, half - 0.5);
        double v = Math.Clamp(Value, -1.0, 1.0);
        double ext = Math.Abs(v) * half;
        Fill.Width = ext;
        Canvas.SetTop(Fill, 0);
        Canvas.SetLeft(Fill, v >= 0 ? half : half - ext);
    }
}
