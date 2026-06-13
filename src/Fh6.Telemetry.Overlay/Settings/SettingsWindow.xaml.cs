using System.Windows;
using System.Windows.Controls;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Settings;

public partial class SettingsWindow : Window
{
    private readonly OverlayConfig _config;

    private sealed record WidgetRow(CheckBox VisibleBox, Slider ScaleSlider, WidgetId Id);
    private readonly List<WidgetRow> _widgetRows = new();

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
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(PortBox.Text, out var port))
            _config.Port = port;
        _config.ListenAddress = AddressBox.Text.Trim();
        if (LayoutBox.SelectedItem is OverlayLayout layout)
            _config.Layout = layout;
        _config.Opacity = OpacitySlider.Value;
        _config.Scale = HudScaleSlider.Value;

        foreach (var row in _widgetRows)
        {
            var wc = _config.Widgets[row.Id.ToString()];
            wc.Visible = row.VisibleBox.IsChecked == true;
            wc.Scale = Math.Clamp(row.ScaleSlider.Value, 0.5, 2.5);
        }

        DialogResult = true;
        Close();
    }
}
