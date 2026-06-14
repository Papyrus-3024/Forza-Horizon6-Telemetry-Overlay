using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Artificial-horizon indicator mapped to steering.
/// The horizon plane tilts ±25° at full lock (DisplayedSteer = ±1).
/// A fixed centre reticle and degree readout stay on top.
/// </summary>
public partial class SteeringHorizonWidget : UserControl
{
    // Maximum visual tilt in degrees at full lock.
    private const double MaxTiltDeg = 25.0;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(SteeringHorizonWidget),
            new PropertyMetadata(0.0, OnValueChanged));

    /// <summary>Eased steering fraction −1 (full left) to +1 (full right).</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public SteeringHorizonWidget()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SteeringHorizonWidget)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Clip to rounded border interior.
        ClipRect.Rect = new Rect(1, 1, w - 2, h - 2);

        // Centre the rotation pivot on the visible area.
        double cx = w / 2.0;
        double cy = h / 2.0;
        PlaneRotation.CenterX = cx;
        PlaneRotation.CenterY = cy;
        // Also keep the plane canvas centred.
        HorizonPlane.RenderTransformOrigin = new Point(0.5, 0.5);

        double steer = Math.Clamp(Value, -1.0, 1.0);
        double tiltDeg = steer * MaxTiltDeg;
        PlaneRotation.Angle = tiltDeg;

        // Update degree label.
        int displayDeg = (int)Math.Round(Math.Abs(tiltDeg));
        DegreeLabel.Text = displayDeg == 0
            ? "0°"
            : (tiltDeg > 0 ? $"{displayDeg}° R" : $"{displayDeg}° L");

        // Reference mark width (±30% of widget width, with a gap around centre).
        double refLen = w * 0.22;
        double gap    = 14.0;
        double midY   = cy;

        RefLeft.X1  = cx - gap - refLen;
        RefLeft.Y1  = midY;
        RefLeft.X2  = cx - gap;
        RefLeft.Y2  = midY;
        Canvas.SetTop(RefLeft, 0);

        RefRight.X1 = cx + gap;
        RefRight.Y1 = midY;
        RefRight.X2 = cx + gap + refLen;
        RefRight.Y2 = midY;
        Canvas.SetTop(RefRight, 0);

        // Centre pip.
        Canvas.SetLeft(CenterPip, cx - 2.5);
        Canvas.SetTop(CenterPip,  midY - 2.5);
    }
}
