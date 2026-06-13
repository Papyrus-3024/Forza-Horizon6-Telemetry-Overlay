using System.Windows;

namespace Fh6.Telemetry.Overlay.Settings;

public partial class SettingsWindow : Window
{
    private readonly OverlayConfig _config;

    public SettingsWindow(OverlayConfig config)
    {
        InitializeComponent();
        _config = config;

        PortBox.Text = config.Port.ToString();
        AddressBox.Text = config.ListenAddress;
        LayoutBox.ItemsSource = Enum.GetValues(typeof(OverlayLayout));
        LayoutBox.SelectedItem = config.Layout;
        OpacitySlider.Value = config.Opacity;
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(PortBox.Text, out var port))
            _config.Port = port;
        _config.ListenAddress = AddressBox.Text.Trim();
        if (LayoutBox.SelectedItem is OverlayLayout layout)
            _config.Layout = layout;
        _config.Opacity = OpacitySlider.Value;

        DialogResult = true;
        Close();
    }
}
