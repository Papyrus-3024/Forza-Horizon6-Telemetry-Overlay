using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Fh6.Telemetry.Overlay.Interop;
using Fh6.Telemetry.Overlay.Layouts;
using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.ViewModels;
using Fh6.Telemetry.Overlay.Widgets;

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

    // Animation tick — driven by CompositionTarget.Rendering
    private readonly Stopwatch _renderClock = new();
    private long _lastRenderTicks;

    public OverlayWindow(TelemetryViewModel viewModel, OverlayConfig config)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _config = config;
        DataContext = viewModel;

        Opacity = Math.Clamp(config.Opacity, 0.2, 1.0);

        // Cover the primary screen working area — drop SizeToContent.
        var workArea = SystemParameters.WorkArea;
        Left   = workArea.Left;
        Top    = workArea.Top;
        Width  = workArea.Width;
        Height = workArea.Height;

        // Wire up the FreeLayout.
        FreeLayoutHost.SetViewModel(viewModel);
        FreeLayoutHost.ApplyConfig(config);

        SourceInitialized += OnSourceInitialized;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;

        // Whole-window DragMove fallback: fires when the mouse-down is on empty canvas,
        // i.e. when FreeLayout did NOT capture the mouse (no widget was hit in edit mode).
        MouseLeftButtonDown += (_, e) =>
        {
            if (_editMode && !FreeLayoutHost.IsMouseCaptureWithin)
                DragMove();
        };
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _renderClock.Start();
        _lastRenderTicks = _renderClock.ElapsedTicks;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _renderClock.ElapsedTicks;
        var dtSeconds = (now - _lastRenderTicks) / (double)Stopwatch.Frequency;
        _lastRenderTicks = now;

        // Guard against first-frame spike or system sleep resumption.
        if (dtSeconds > 0.25) dtSeconds = 0.25;

        _viewModel.Tick(dtSeconds);
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

        FreeLayoutHost.SetEditMode(_editMode, _config);

        if (!_editMode)
        {
            // Flush widget positions and save.
            FreeLayoutHost.FlushPositions(_config);
            _config.WindowLeft = Left;
            _config.WindowTop  = Top;
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
                    _config.Normalize(_config.Layout);
                    FreeLayoutHost.ApplyConfig(_config);
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
        _config.ApplySeed(LayoutSeeds.For(_config.Layout));
        FreeLayoutHost.ApplyConfig(_config);
        ConfigStore.Save(ConfigStore.DefaultPath, _config);
    }

    private void Root_MouseEnter(object sender, MouseEventArgs e)
        => QuitButton.Opacity = 1;

    private void Root_MouseLeave(object sender, MouseEventArgs e)
        => QuitButton.Opacity = 0;

    private void QuitButton_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();

    /// <summary>Raised after settings change so the host can restart the source if port/address changed.</summary>
    public event EventHandler? SettingsApplied;

    protected override void OnClosed(EventArgs e)
    {
        _hotkeys?.Dispose();
        base.OnClosed(e);
    }
}
