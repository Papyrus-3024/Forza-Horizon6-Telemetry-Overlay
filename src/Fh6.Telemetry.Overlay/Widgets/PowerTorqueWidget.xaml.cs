using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Animated power/torque widget. Values animate via the VM's eased displayed values;
/// each row shows the number and an eased horizontal fill bar.
/// Ranges: power 0..1500 hp, torque 0..1200 lb·ft (consistent with chart widget).
/// </summary>
public partial class PowerTorqueWidget : UserControl
{
    private const double MaxHp   = 1500.0;
    private const double MaxLbFt = 1200.0;

    // ── Dependency properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty DisplayedPowerRawProperty =
        DependencyProperty.Register(
            nameof(DisplayedPowerRaw),
            typeof(double),
            typeof(PowerTorqueWidget),
            new PropertyMetadata(0.0, OnChanged));

    public static readonly DependencyProperty DisplayedTorqueRawProperty =
        DependencyProperty.Register(
            nameof(DisplayedTorqueRaw),
            typeof(double),
            typeof(PowerTorqueWidget),
            new PropertyMetadata(0.0, OnChanged));

    public double DisplayedPowerRaw
    {
        get => (double)GetValue(DisplayedPowerRawProperty);
        set => SetValue(DisplayedPowerRawProperty, value);
    }

    public double DisplayedTorqueRaw
    {
        get => (double)GetValue(DisplayedTorqueRawProperty);
        set => SetValue(DisplayedTorqueRawProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public PowerTorqueWidget()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
    }

    // ── Property change ───────────────────────────────────────────────────────

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PowerTorqueWidget)d).Redraw();

    // ── Redraw ────────────────────────────────────────────────────────────────

    private void Redraw()
    {
        double hp   = double.IsNaN(DisplayedPowerRaw)  ? 0.0 : DisplayedPowerRaw;
        double lbft = double.IsNaN(DisplayedTorqueRaw) ? 0.0 : DisplayedTorqueRaw;

        hp   = Math.Clamp(hp,   0.0, MaxHp);
        lbft = Math.Clamp(lbft, 0.0, MaxLbFt);

        PowerValue.Text  = $"{hp:F0}";
        TorqueValue.Text = $"{lbft:F0}";

        // Bar widths are fractions of the widget's available width.
        // The bars are inside a border that fills horizontally, so use ActualWidth of the UserControl
        // minus padding (12*2 = 24) minus border (2). Fall back to 176 before first layout.
        double availW = ActualWidth > 0 ? ActualWidth - 26.0 : 174.0;
        availW = Math.Max(availW, 0);

        PowerBar.Width  = hp   / MaxHp   * availW;
        TorqueBar.Width = lbft / MaxLbFt * availW;
    }
}
