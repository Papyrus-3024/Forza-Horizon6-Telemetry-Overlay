using System.Windows;
using System.Windows.Controls;
using Fh6.Telemetry.Overlay.Theming;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Settings;

/// <summary>
/// In-overlay settings panel (replaces the old modal SettingsWindow). Hosts the full
/// settings, reorganized into sections. Raises <see cref="ApplyRequested"/> when the user
/// applies (the host writes config + re-applies live) and <see cref="CloseRequested"/> when
/// dismissed. Logic mirrors the former SettingsWindow; only the host plumbing changed.
/// </summary>
public partial class SettingsFlyout : UserControl
{
    private OverlayConfig _config = null!;

    private sealed record WidgetRow(CheckBox VisibleBox, Slider ScaleSlider, WidgetId Id);
    private readonly List<WidgetRow> _widgetRows = new();

    private sealed record ChartSeriesRow(CheckBox EnabledBox, ChartSeriesId Id);
    private readonly List<ChartSeriesRow> _chartSeriesRows = new();

    /// <summary>Raised after the form is written into the config; host applies it live.</summary>
    public event EventHandler? ApplyRequested;
    /// <summary>Raised when the user dismisses the panel.</summary>
    public event EventHandler? CloseRequested;

    public SettingsFlyout() => InitializeComponent();

    /// <summary>Populates the controls from <paramref name="config"/>. Call once after construction.</summary>
    public void Load(OverlayConfig config)
    {
        _config = config;

        PortBox.Text = config.Port.ToString();
        AddressBox.Text = config.ListenAddress;
        LayoutBox.ItemsSource = Enum.GetValues(typeof(OverlayLayout));
        LayoutBox.SelectedItem = config.Layout;
        OpacitySlider.Value = config.Opacity;
        HudScaleSlider.Value = config.Scale;

        ThemePresetBox.ItemsSource = ThemePalette.PresetNames;
        ThemePresetBox.SelectedItem =
            ThemePalette.PresetNames.Any(n => n.Equals(config.ThemePreset, StringComparison.OrdinalIgnoreCase))
            ? config.ThemePreset
            : "DarkGlass";
        CustomAccentBox.Text = config.CustomAccent ?? string.Empty;

        config.Normalize(config.Layout);

        WidgetRows.Children.Clear();
        _widgetRows.Clear();
        foreach (WidgetId id in Enum.GetValues<WidgetId>())
        {
            var wc = _config.Widgets[id.ToString()];

            var nameBlock = new TextBlock { Text = id.ToString(), Width = 110, VerticalAlignment = VerticalAlignment.Center };
            var visibleBox = new CheckBox { IsChecked = wc.Visible, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var scaleSlider = new Slider
            {
                Minimum = 0.5, Maximum = 2.5, TickFrequency = 0.05, IsSnapToTickEnabled = true,
                Value = wc.Scale, Width = 120, VerticalAlignment = VerticalAlignment.Center,
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
            row.Children.Add(visibleBox);
            row.Children.Add(nameBlock);
            row.Children.Add(scaleSlider);

            WidgetRows.Children.Add(row);
            _widgetRows.Add(new WidgetRow(visibleBox, scaleSlider, id));
        }

        ChartWindowBox.ItemsSource = ChartConfig.SupportedWindows.Select(w => $"{(int)w} s").ToList();
        var selectedWindowIdx = Array.IndexOf(ChartConfig.SupportedWindows, config.Chart.WindowSeconds);
        ChartWindowBox.SelectedIndex = selectedWindowIdx >= 0 ? selectedWindowIdx : 1;

        ChartSeriesRows.Children.Clear();
        _chartSeriesRows.Clear();
        foreach (var def in ChartSeriesCatalog.All)
        {
            var cb = new CheckBox
            {
                Content = def.Name,
                IsChecked = ChartSeriesCatalog.IsEnabled(config.Chart, def.Id),
                Width = 95, Margin = new Thickness(0, 0, 0, 2),
            };
            ChartSeriesRows.Children.Add(cb);
            _chartSeriesRows.Add(new ChartSeriesRow(cb, def.Id));
        }

        RefreshSavedLayoutList();
    }

    private void WriteFormToConfig()
    {
        if (int.TryParse(PortBox.Text, out var port))
            _config.Port = port;
        _config.ListenAddress = AddressBox.Text.Trim();
        if (LayoutBox.SelectedItem is OverlayLayout layout)
            _config.Layout = layout;
        _config.Opacity = OpacitySlider.Value;
        _config.Scale = HudScaleSlider.Value;

        if (ThemePresetBox.SelectedItem is string preset)
            _config.ThemePreset = preset;
        var accent = CustomAccentBox.Text.Trim();
        _config.CustomAccent = string.IsNullOrEmpty(accent) ? null : accent;

        foreach (var row in _widgetRows)
        {
            var wc = _config.Widgets[row.Id.ToString()];
            wc.Visible = row.VisibleBox.IsChecked == true;
            wc.Scale = Math.Clamp(row.ScaleSlider.Value, 0.5, 2.5);
        }

        int winIdx = ChartWindowBox.SelectedIndex;
        if (winIdx >= 0 && winIdx < ChartConfig.SupportedWindows.Length)
            _config.Chart.WindowSeconds = ChartConfig.SupportedWindows[winIdx];

        foreach (var row in _chartSeriesRows)
            _config.Chart.Series[row.Id.ToString()] = row.EnabledBox.IsChecked == true;
    }

    private void RefreshSavedLayoutList()
    {
        var selected = SavedLayoutBox.SelectedItem as string;
        SavedLayoutBox.ItemsSource = null;
        SavedLayoutBox.ItemsSource = _config.SavedLayouts.Keys.ToList();
        if (selected != null && _config.SavedLayouts.ContainsKey(selected))
            SavedLayoutBox.SelectedItem = selected;
    }

    // ─── Actions ──────────────────────────────────────────────────────────────
    // Decision: Apply keeps the flyout open (unlike the old modal which closed) so the
    // user can keep tweaking live; dismissal is explicit via the close button / F9 / hover-out.

    private void OnApply(object sender, RoutedEventArgs e)
    {
        WriteFormToConfig();
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnSaveLayout(object sender, RoutedEventArgs e)
    {
        var name = LayoutNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        WriteFormToConfig();
        _config.SaveLayoutAs(name);
        RefreshSavedLayoutList();
        SavedLayoutBox.SelectedItem = name;
    }

    private void OnLoadLayout(object sender, RoutedEventArgs e)
    {
        if (SavedLayoutBox.SelectedItem is not string name) return;
        if (!_config.LoadLayout(name)) return;

        LayoutBox.SelectedItem = _config.Layout;
        HudScaleSlider.Value = _config.Scale;
        foreach (var row in _widgetRows)
        {
            var wc = _config.Widgets[row.Id.ToString()];
            row.VisibleBox.IsChecked = wc.Visible;
            row.ScaleSlider.Value = Math.Clamp(wc.Scale, 0.5, 2.5);
        }
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRenameLayout(object sender, RoutedEventArgs e)
    {
        if (SavedLayoutBox.SelectedItem is not string from) return;
        var to = LayoutNameBox.Text.Trim();
        if (string.IsNullOrEmpty(to)) return;

        _config.RenameLayout(from, to);
        RefreshSavedLayoutList();
        SavedLayoutBox.SelectedItem = to;
    }

    private void OnDeleteLayout(object sender, RoutedEventArgs e)
    {
        if (SavedLayoutBox.SelectedItem is not string name) return;
        _config.DeleteLayout(name);
        RefreshSavedLayoutList();
        LayoutNameBox.Text = string.Empty;
    }
}
