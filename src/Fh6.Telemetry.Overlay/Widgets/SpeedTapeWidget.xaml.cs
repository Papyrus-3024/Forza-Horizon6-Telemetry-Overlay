using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Fh6.Telemetry.Overlay.Widgets;

/// <summary>
/// Vertical speed tape (altimeter style): tick marks scroll so the current MPH value
/// is always centred; ±2 major increments (each 20 mph) visible above and below.
/// </summary>
public partial class SpeedTapeWidget : UserControl
{
    // Pixels per 1 mph on the tape.
    private const double PixelsPerMph = 2.0;
    // Centre Y of the visible 160 px window (where the current speed is pinned).
    private const double CentreY = 80.0;
    // Tick spacing: minor every 5 mph, major every 20 mph.
    private const int MinorStep = 5;
    private const int MajorStep = 20;
    // Range to pre-render: 0..300 mph covers any Forza scenario.
    private const int TapeMin = 0;
    private const int TapeMax = 300;
    // Full canvas height holding the whole range with all-positive coords:
    // v=TapeMax sits at y=0 (top), v=TapeMin at y=TapeHeightPx (bottom).
    private const double TapeHeightPx = (TapeMax - TapeMin) * PixelsPerMph;

    public SpeedTapeWidget()
    {
        InitializeComponent();
        BuildTape();
        DataContextChanged += OnDataContextChanged;
    }

    // ── Build static tick marks ──────────────────────────────────────────────

    private void BuildTape()
    {
        var labelBrush   = (Brush)Application.Current.Resources["Fh6.TextSecondary"];
        var dimBrush     = (Brush)Application.Current.Resources["Fh6.TextLabel"];

        TapeCanvas.Height = TapeHeightPx;

        for (int v = TapeMin; v <= TapeMax; v += MinorStep)
        {
            bool major = v % MajorStep == 0;
            // All-positive canvas Y: highest speed at the top, 0 mph at the bottom.
            double y = (TapeMax - v) * PixelsPerMph;

            // Tick mark (right-aligned in the 90px canvas)
            var tick = new Rectangle
            {
                Width  = major ? 20 : 10,
                Height = major ? 2 : 1,
                Fill   = major ? labelBrush : dimBrush,
            };
            Canvas.SetLeft(tick, major ? 20.0 : 30.0);
            Canvas.SetTop(tick,  y - tick.Height / 2.0);
            TapeCanvas.Children.Add(tick);

            // Label on major ticks
            if (major && v > TapeMin)
            {
                var lbl = new TextBlock
                {
                    Text       = v.ToString(),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 10,
                    Foreground = labelBrush,
                };
                Canvas.SetLeft(lbl, 0.0);
                Canvas.SetTop(lbl,  y - 7.0);
                TapeCanvas.Children.Add(lbl);
            }
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
            // Sync immediately if the vm already has a value.
            UpdateTape(e.NewValue as ViewModels.TelemetryViewModel);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SpeedMph" or "DisplayedRpmFraction")
            UpdateTape(sender as ViewModels.TelemetryViewModel);
    }

    private void UpdateTape(ViewModels.TelemetryViewModel? vm)
    {
        if (vm is null) return;

        if (!double.TryParse(vm.SpeedMph, out double mph))
            mph = 0.0;
        mph = Math.Clamp(mph, TapeMin, TapeMax);

        // Slide the tall canvas so the current-speed tick lands on the window centre.
        // tickY(mph) = (TapeMax - mph) * PixelsPerMph; screenY = tickY + translateY = CentreY.
        TapeTranslate.Y = CentreY - (TapeMax - mph) * PixelsPerMph;

        CursorText.Text = ((int)Math.Round(mph)).ToString();
    }
}
