using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Fh6.Telemetry.Overlay.Interop;
using Fh6.Telemetry.Overlay.Layouts;
using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.ViewModels;

namespace Fh6.Telemetry.Overlay;

public partial class OverlayWindow : Window
{
    private const uint VK_F7 = 0x76;
    private const uint VK_F8 = 0x77;
    private const uint VK_F9 = 0x78;
    private const uint VK_F10 = 0x79;

    private readonly TelemetryViewModel _viewModel;
    private readonly OverlayConfig _config;
    private GlobalHotkey? _hotkeys;
    private IntPtr _hwnd;
    private bool _editMode;
    private bool _settingsOpen;

    public OverlayWindow(TelemetryViewModel viewModel, OverlayConfig config)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _config = config;
        DataContext = viewModel;

        Opacity = Math.Clamp(config.Opacity, 0.2, 1.0);
        if (config.WindowLeft is double l) Left = l;
        if (config.WindowTop is double t) Top = t;

        var scale = Math.Clamp(config.Scale, 0.5, 3.0);
        LayoutHost.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);

        ApplyLayout(config.Layout);
        SourceInitialized += OnSourceInitialized;
        MouseLeftButtonDown += (_, _) => { if (_editMode) DragMove(); };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        ClickThrough.SetClickThrough(_hwnd, true); // start click-through

        _hotkeys = new GlobalHotkey(_hwnd);
        // F7: quit from gameplay without needing to focus the overlay
        _hotkeys.Register(VK_F7, () => Application.Current.Shutdown());
        _hotkeys.Register(VK_F8, ToggleEditMode);
        _hotkeys.Register(VK_F9, OpenSettings);
        _hotkeys.Register(VK_F10, CycleLayout);
    }

    private void ToggleEditMode()
    {
        _editMode = !_editMode;
        ClickThrough.SetClickThrough(_hwnd, !_editMode);
        Root.BorderBrush = _editMode
            ? System.Windows.Media.Brushes.Yellow
            : System.Windows.Media.Brushes.Transparent;
        Root.BorderThickness = new Thickness(_editMode ? 2 : 0);
        ResizeGrip.Visibility = _editMode ? Visibility.Visible : Visibility.Collapsed;

        if (!_editMode)
        {
            _config.WindowLeft = Left;
            _config.WindowTop = Top;
            ConfigStore.Save(ConfigStore.DefaultPath, _config);
        }
    }

    private void OpenSettings()
    {
        if (_settingsOpen) return;

        // Open off the hotkey hook's WndProc to avoid running a modal loop reentrantly.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _settingsOpen = true;
            try
            {
                var dialog = new SettingsWindow(_config) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    Opacity = Math.Clamp(_config.Opacity, 0.2, 1.0);
                    ApplyLayout(_config.Layout);
                    var s = Math.Clamp(_config.Scale, 0.5, 3.0);
                    LayoutHost.LayoutTransform = new System.Windows.Media.ScaleTransform(s, s);
                    ConfigStore.Save(ConfigStore.DefaultPath, _config);
                    SettingsApplied?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                _settingsOpen = false;
            }
        }));
    }

    private void CycleLayout()
    {
        _config.Layout = _config.Layout switch
        {
            OverlayLayout.BottomStrip => OverlayLayout.CornerPanel,
            OverlayLayout.CornerPanel => OverlayLayout.CenterDash,
            _ => OverlayLayout.BottomStrip,
        };
        ApplyLayout(_config.Layout);
        ConfigStore.Save(ConfigStore.DefaultPath, _config);
    }

    private void ApplyLayout(OverlayLayout layout)
    {
        Control view = layout switch
        {
            OverlayLayout.CornerPanel => new CornerPanelLayout(),
            OverlayLayout.CenterDash => new CenterDashLayout(),
            _ => new BottomStripLayout(),
        };
        view.DataContext = _viewModel;
        LayoutHost.Content = view;
    }

    private void Root_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        => QuitButton.Opacity = 1;

    private void Root_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => QuitButton.Opacity = 0;

    private void QuitButton_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();

    private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        var s = Math.Clamp(_config.Scale + e.VerticalChange / 200.0, 0.5, 3.0);
        _config.Scale = s;
        LayoutHost.LayoutTransform = new System.Windows.Media.ScaleTransform(s, s);
    }

    /// <summary>Raised after settings change so the host can restart the source if port/address changed.</summary>
    public event EventHandler? SettingsApplied;

    protected override void OnClosed(EventArgs e)
    {
        _hotkeys?.Dispose();
        base.OnClosed(e);
    }
}
