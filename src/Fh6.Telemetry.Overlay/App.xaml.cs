using System.Windows;

namespace Fh6.Telemetry.Overlay;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new OverlayWindow();
        window.Show();
    }
}
