using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Fh6.Telemetry.Overlay.Widgets;

public partial class GearWidget : UserControl
{
    private string? _lastGear;
    private readonly Storyboard _drumRoll;

    public GearWidget()
    {
        InitializeComponent();
        _drumRoll = (Storyboard)Resources["DrumRoll"];
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is System.ComponentModel.INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is System.ComponentModel.INotifyPropertyChanged newVm)
            newVm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Gear") return;

        var current = (sender as ViewModels.TelemetryViewModel)?.Gear;
        if (current is null || current == _lastGear) return;

        _lastGear = current;
        // Stop any in-progress animation, then restart from below.
        _drumRoll.Stop(this);
        _drumRoll.Begin(this, true);
    }
}
