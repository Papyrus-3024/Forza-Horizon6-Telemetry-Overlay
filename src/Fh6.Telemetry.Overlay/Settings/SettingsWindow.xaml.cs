using System.Windows;
using System.Windows.Controls;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Settings;

public partial class SettingsWindow : Window
{
    private readonly OverlayConfig _config;

    private sealed record WidgetRow(CheckBox VisibleBox, Slider ScaleSlider, WidgetId Id);
    private readonly List<WidgetRow> _widgetRows = new();

    private sealed record ChartSeriesRow(CheckBox EnabledBox, ChartSeriesId Id);
    private readonly List<ChartSeriesRow> _chartSeriesRows = new();

    public SettingsWindow(OverlayConfig config)
    {
        InitializeComponent();
        _config = config;

        PortBox.Text = config.Port.ToString();
        AddressBox.Text = config.ListenAddress;
        LayoutBox.ItemsSource = Enum.GetValues(typeof(OverlayLayout));
        LayoutBox.SelectedItem = config.Layout;
        OpacitySlider.Value = config.Opacity;
        HudScaleSlider.Value = config.Scale;
        MapSeasonBox.ItemsSource = Enum.GetValues(typeof(MapSeason));
        MapSeasonBox.SelectedItem = config.Season;

        // Ensure all widget keys exist before reading them.
        config.Normalize(config.Layout);

        foreach (WidgetId id in Enum.GetValues<WidgetId>())
        {
            var wc = _config.Widgets[id.ToString()];

            var nameBlock = new TextBlock
            {
                Text = id.ToString(),
                Width = 90,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var visibleBox = new CheckBox
            {
                IsChecked = wc.Visible,
                Content = "Visible",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0),
            };

            var scaleLabel = new TextBlock
            {
                Text = "Scale",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };

            var scaleSlider = new Slider
            {
                Minimum = 0.5,
                Maximum = 2.5,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true,
                Value = wc.Scale,
                Width = 90,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4),
            };
            row.Children.Add(nameBlock);
            row.Children.Add(visibleBox);
            row.Children.Add(scaleLabel);
            row.Children.Add(scaleSlider);

            WidgetRows.Children.Add(row);
            _widgetRows.Add(new WidgetRow(visibleBox, scaleSlider, id));
        }

        // Chart: time-window combo
        ChartWindowBox.ItemsSource = ChartConfig.SupportedWindows.Select(w => $"{(int)w} s").ToList();
        var selectedWindowIdx = Array.IndexOf(ChartConfig.SupportedWindows, config.Chart.WindowSeconds);
        ChartWindowBox.SelectedIndex = selectedWindowIdx >= 0 ? selectedWindowIdx : 1; // default 60 s

        // Chart: per-series checkboxes
        foreach (var def in ChartSeriesCatalog.All)
        {
            var cb = new CheckBox
            {
                Content = def.Name,
                IsChecked = ChartSeriesCatalog.IsEnabled(config.Chart, def.Id),
                Margin = new Thickness(0, 0, 0, 2),
            };
            ChartSeriesRows.Children.Add(cb);
            _chartSeriesRows.Add(new ChartSeriesRow(cb, def.Id));
        }

        RefreshSavedLayoutList();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes form controls back to <see cref="_config"/> without closing the dialog.
    /// Extracted so Save can capture in-progress edits before snapshotting.
    /// </summary>
    private void WriteFormToConfig()
    {
        if (int.TryParse(PortBox.Text, out var port))
            _config.Port = port;
        _config.ListenAddress = AddressBox.Text.Trim();
        if (LayoutBox.SelectedItem is OverlayLayout layout)
            _config.Layout = layout;
        _config.Opacity = OpacitySlider.Value;
        _config.Scale = HudScaleSlider.Value;
        if (MapSeasonBox.SelectedItem is MapSeason season)
            _config.Season = season;

        foreach (var row in _widgetRows)
        {
            var wc = _config.Widgets[row.Id.ToString()];
            wc.Visible = row.VisibleBox.IsChecked == true;
            wc.Scale = Math.Clamp(row.ScaleSlider.Value, 0.5, 2.5);
        }

        // Chart: write window selection back to config.
        int winIdx = ChartWindowBox.SelectedIndex;
        if (winIdx >= 0 && winIdx < ChartConfig.SupportedWindows.Length)
            _config.Chart.WindowSeconds = ChartConfig.SupportedWindows[winIdx];

        // Chart: write per-series enabled flags.
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

    // ─── Apply ──────────────────────────────────────────────────────────────

    private void OnApply(object sender, RoutedEventArgs e)
    {
        WriteFormToConfig();
        DialogResult = true;
        Close();
    }

    // ─── Named layout actions ────────────────────────────────────────────────

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

        // Reflect the loaded state in form controls.
        LayoutBox.SelectedItem = _config.Layout;
        HudScaleSlider.Value = _config.Scale;
        foreach (var row in _widgetRows)
        {
            var wc = _config.Widgets[row.Id.ToString()];
            row.VisibleBox.IsChecked = wc.Visible;
            row.ScaleSlider.Value = Math.Clamp(wc.Scale, 0.5, 2.5);
        }

        // Apply and close so OverlayWindow picks up the new layout via its existing path.
        DialogResult = true;
        Close();
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
