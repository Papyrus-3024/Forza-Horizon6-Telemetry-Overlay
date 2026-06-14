using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Vertical RPM bar with a green→amber→red gradient fill, plus a column of gear labels
/// (R N 1–7) that highlights the current gear. Flashes the redline hatch at high RPM.
/// Width is governed by the per-widget Scale in the customisation system.
/// </summary>
public partial class ShiftLadderWidget : UserControl
{
    private const double BarMaxHeight = 140.0;
    // Fraction above which the redline flash triggers.
    private const double RedlineThreshold = 0.88;

    // gear label → pip TextBlock, ordered 7 down to R so index[0] = top.
    private readonly List<(string Label, TextBlock Block)> _pips = new();
    private string? _lastGear;
    private bool _flashing;

    private readonly Storyboard _flash;

    private static readonly Brush PipNormal  = Brushes.Transparent;
    private static readonly Brush PipActive  = new SolidColorBrush(Color.FromRgb(0x46, 0xE0, 0x8A));
    private static readonly Brush PipTextDim = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
    private static readonly Brush PipTextOn  = new SolidColorBrush(Color.FromRgb(0x04, 0x14, 0x0F));

    static ShiftLadderWidget()
    {
        PipActive.Freeze();
        PipTextDim.Freeze();
        PipTextOn.Freeze();
    }

    public ShiftLadderWidget()
    {
        InitializeComponent();
        _flash = (Storyboard)Resources["RedlineFlash"];

        BuildGearPips();
        DataContextChanged += OnDataContextChanged;
    }

    // ── Build gear pip column (top = 7, bottom = R) ──────────────────────────

    private void BuildGearPips()
    {
        // Labels from top of column downward: highest gear at top, R at bottom.
        string[] labels = ["7", "6", "5", "4", "3", "2", "1", "N", "R"];
        foreach (var lbl in labels)
        {
            var tb = new TextBlock
            {
                Text            = lbl,
                FontFamily      = new FontFamily("Consolas"),
                FontSize        = 11,
                FontWeight      = FontWeights.Bold,
                Width           = 22,
                TextAlignment   = TextAlignment.Center,
                Padding         = new Thickness(2, 1, 2, 1),
                Foreground      = PipTextDim,
                Background      = PipNormal,
            };
            _pips.Add((lbl, tb));
            GearPips.Items.Add(tb);
        }
    }

    // ── DataContext wiring ────────────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is INotifyPropertyChanged newVm)
        {
            newVm.PropertyChanged += OnVmPropertyChanged;
            UpdateAll(e.NewValue as ViewModels.TelemetryViewModel);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "DisplayedRpmFraction" or "Gear")
            UpdateAll(sender as ViewModels.TelemetryViewModel);
    }

    private void UpdateAll(ViewModels.TelemetryViewModel? vm)
    {
        if (vm is null) return;

        double fraction = Math.Clamp(vm.DisplayedRpmFraction, 0.0, 1.0);

        // Bar fill height.
        FillBorder.Height = Math.Max(0, fraction * BarMaxHeight);

        // Gear pips.
        var gear = vm.Gear;
        if (gear != _lastGear)
        {
            _lastGear = gear;
            GearLabel.Text = gear;
            UpdatePips(gear);
        }

        // Redline flash.
        bool shouldFlash = fraction >= RedlineThreshold;
        if (shouldFlash && !_flashing)
        {
            _flashing = true;
            _flash.Begin(this, true);
        }
        else if (!shouldFlash && _flashing)
        {
            _flashing = false;
            _flash.Stop(this);
            RedlineOverlay.Opacity = 0;
        }
    }

    private void UpdatePips(string gear)
    {
        // Normalise: "0" comes from telemetry for reverse, displayed as "R".
        string normalised = gear == "0" ? "R" : gear;

        foreach (var (lbl, tb) in _pips)
        {
            bool active = lbl == normalised;
            tb.Background  = active ? PipActive : PipNormal;
            tb.Foreground  = active ? PipTextOn : PipTextDim;
            tb.FontSize    = active ? 14 : 11;
        }
    }
}
