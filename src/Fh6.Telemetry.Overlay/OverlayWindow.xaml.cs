using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Fh6.Telemetry.Overlay.Interop;
using Fh6.Telemetry.Overlay.Layouts;
using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.ViewModels;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay;

public partial class OverlayWindow : Window
{
    private const uint VK_F7  = 0x76;
    private const uint VK_F8  = 0x77;
    private const uint VK_F9  = 0x78;
    private const uint VK_F10 = 0x79;

    private readonly TelemetryViewModel _viewModel;
    private readonly OverlayConfig _config;
    private GlobalHotkey? _hotkeys;
    private IntPtr _hwnd;
    private bool _editMode;

    // Settings flyout state. Peeking = shown via hover (auto-hides); pinned = stays until dismissed.
    // Both require click-through OFF so the panel is interactive.
    private bool _settingsPinned;
    private bool _settingsPeeking;
    private DispatcherTimer? _cursorPoll;

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

        // Wire up the in-overlay settings flyout (replaces the old modal window).
        Settings.Load(config);
        Settings.ApplyRequested += (_, _) => ApplyFromFlyout();
        Settings.CloseRequested += (_, _) => SetSettingsPinned(false);

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

        // Poll the cursor (~60 ms) to detect hover over the gear hotspot — a click-through
        // window receives no mouse-move, so events alone can't drive hover-peek.
        _cursorPoll = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(60) };
        _cursorPoll.Tick += CursorPollTick;
        _cursorPoll.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _cursorPoll?.Stop();
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
        UpdateClickThrough(); // start click-through (no modes active yet)

        _hotkeys = new GlobalHotkey(_hwnd);
        // F7: quit from gameplay without needing to focus the overlay
        _hotkeys.Register(VK_F7, () => Application.Current.Shutdown());
        _hotkeys.Register(VK_F8, ToggleEditMode);
        _hotkeys.Register(VK_F9, () => SetSettingsPinned(!_settingsPinned));
        _hotkeys.Register(VK_F10, CycleLayout);
    }

    private void ToggleEditMode()
    {
        _editMode = !_editMode;
        // Entering edit mode dismisses a transient peek (a pinned panel stays).
        if (_editMode) _settingsPeeking = false;
        Settings.Visibility = (_settingsPinned) ? Visibility.Visible : Visibility.Collapsed;
        UpdateClickThrough();

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

    // ─── Settings flyout (gear hover-peek / click-pin) ──────────────────────────

    /// <summary>Click-through is OFF whenever the overlay needs to be interactive:
    /// edit mode, or the settings flyout being peeked/pinned.</summary>
    private void UpdateClickThrough()
        => ClickThrough.SetClickThrough(_hwnd, !(_editMode || _settingsPinned || _settingsPeeking));

    private void SetSettingsPeeking(bool peeking)
    {
        if (_settingsPeeking == peeking || _settingsPinned) return;
        _settingsPeeking = peeking;
        Settings.Visibility = peeking ? Visibility.Visible : Visibility.Collapsed;
        UpdateClickThrough();
    }

    private void SetSettingsPinned(bool pinned)
    {
        _settingsPinned = pinned;
        _settingsPeeking = false;
        Settings.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
        UpdateClickThrough();
    }

    private void GearButton_Click(object sender, RoutedEventArgs e)
        => SetSettingsPinned(!_settingsPinned);

    /// <summary>Applies the flyout's edits live (same path the old modal Apply used).</summary>
    private void ApplyFromFlyout()
    {
        Opacity = Math.Clamp(_config.Opacity, 0.2, 1.0);
        _config.Normalize(_config.Layout);
        FreeLayoutHost.ApplyConfig(_config);
        ConfigStore.Save(ConfigStore.DefaultPath, _config);
        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private void CursorPollTick(object? sender, EventArgs e)
    {
        // Pinned panels stay; edit mode owns click-through. Only hover-peek auto-toggles.
        if (_settingsPinned || _editMode) return;
        if (!GetCursorPos(out var p)) return;

        bool overGear = InScreenRect(GearButton, p, pad: 4);
        bool overPanel = _settingsPeeking && InScreenRect(Settings, p, pad: 6);

        if ((overGear || overPanel) && !_settingsPeeking)
            SetSettingsPeeking(true);
        else if (!overGear && !overPanel && _settingsPeeking)
            SetSettingsPeeking(false);
    }

    /// <summary>True if the device-pixel cursor point is within the element's screen rect (+pad).</summary>
    private bool InScreenRect(FrameworkElement el, POINT p, double pad)
    {
        if (el.ActualWidth <= 0 || el.Visibility != Visibility.Visible && !ReferenceEquals(el, GearButton))
            return false;
        try
        {
            var tl = el.PointToScreen(new Point(0, 0));
            var br = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
            return p.X >= tl.X - pad && p.X <= br.X + pad && p.Y >= tl.Y - pad && p.Y <= br.Y + pad;
        }
        catch
        {
            return false; // element not yet connected to a presentation source
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

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
    {
        QuitButton.Opacity = 1;
        GearButton.Opacity = 1;
    }

    private void Root_MouseLeave(object sender, MouseEventArgs e)
    {
        QuitButton.Opacity = 0;
        GearButton.Opacity = 0.55;
    }

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
