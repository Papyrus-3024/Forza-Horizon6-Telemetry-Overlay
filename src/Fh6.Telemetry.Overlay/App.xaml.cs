using System.Windows;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.Telemetry;
using Fh6.Telemetry.Overlay.ViewModels;

namespace Fh6.Telemetry.Overlay;

public partial class App : Application
{
    private TelemetryPump? _pump;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = ConfigStore.Load(ConfigStore.DefaultPath);
        var args = ParseArgs(e.Args, config, out var replayFile, out var speed, out var loop);

        var viewModel = new TelemetryViewModel();
        var window = new OverlayWindow(viewModel, config);

        StartPump(viewModel, config, replayFile, speed, loop, window);

        window.SettingsApplied += (_, _) =>
        {
            _pump?.Dispose();
            StartPump(viewModel, config, replayFile, speed, loop, window);
        };

        window.Show();
        _ = args; // reserved
    }

    private void StartPump(
        TelemetryViewModel vm, OverlayConfig config,
        string? replayFile, double speed, bool loop, OverlayWindow window)
    {
        ITelemetrySource source = replayFile is not null
            ? new JsonlReplaySource(replayFile)
            : new UdpTelemetrySource(config.Port);
        var honorTiming = replayFile is not null;
        _pump = new TelemetryPump(source, vm, window.Dispatcher, honorTiming, speed);
        _pump.Start();
    }

    private static bool ParseArgs(
        string[] argv, OverlayConfig config,
        out string? replayFile, out double speed, out bool loop)
    {
        replayFile = null; speed = 1.0; loop = false;
        for (var i = 0; i < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--replay" when i + 1 < argv.Length: replayFile = argv[++i]; break;
                case "--speed" when i + 1 < argv.Length: double.TryParse(argv[++i], out speed); break;
                case "--loop": loop = true; break;
                case "--port" when i + 1 < argv.Length:
                    if (int.TryParse(argv[++i], out var p)) config.Port = p; break;
                case "--opacity" when i + 1 < argv.Length:
                    if (double.TryParse(argv[++i], out var o)) config.Opacity = o; break;
                case "--layout" when i + 1 < argv.Length:
                    if (Enum.TryParse<OverlayLayout>(argv[++i], true, out var lay)) config.Layout = lay; break;
            }
        }
        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pump?.Dispose();
        base.OnExit(e);
    }
}
