using System.Net;
using System.Windows;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.Diagnostics;
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

        OverlayLog.Reset();
        var config = ConfigStore.Load(ConfigStore.DefaultPath);
        ParseArgs(e.Args, config, out var replayFile, out var speed);
        OverlayLog.Write($"config: port={config.Port} listen={config.ListenAddress} layout={config.Layout} " +
                         $"mode={(replayFile is null ? "live-UDP" : "replay")} replay={replayFile ?? "-"}");

        // Ensure every WidgetId has a config entry (fills missing keys from the active seed).
        config.Normalize(config.Layout);

        var viewModel = new TelemetryViewModel();
        var window = new OverlayWindow(viewModel, config);

        StartPump(viewModel, config, replayFile, speed, window);

        window.SettingsApplied += (_, _) =>
        {
            _pump?.Dispose();
            StartPump(viewModel, config, replayFile, speed, window);
        };

        window.Show();
    }

    private void StartPump(
        TelemetryViewModel vm, OverlayConfig config,
        string? replayFile, double speed, OverlayWindow window)
    {
        try
        {
            ITelemetrySource source;
            if (replayFile is not null)
            {
                source = new JsonlReplaySource(replayFile);
            }
            else
            {
                var address = IPAddress.TryParse(config.ListenAddress, out var addr)
                    ? addr
                    : IPAddress.Any;
                source = new UdpTelemetrySource(address, config.Port);
                OverlayLog.Write($"listening for UDP on {address}:{config.Port} " +
                                 "(game Data Out IP must point at THIS pc, e.g. 127.0.0.1, same port)");
            }

            var honorTiming = replayFile is not null;
            _pump = new TelemetryPump(source, vm, window.Dispatcher, honorTiming, speed);
            _pump.Start();
            vm.SetStatus("");
        }
        catch (Exception ex)
        {
            // Keep the overlay alive (e.g. UDP port in use); surface the error instead of crashing.
            _pump = null;
            OverlayLog.Write($"SOURCE ERROR: {ex.GetType().Name}: {ex.Message}");
            vm.SetStatus($"Telemetry source error: {ex.Message}");
        }
    }

    private static void ParseArgs(
        string[] argv, OverlayConfig config,
        out string? replayFile, out double speed)
    {
        replayFile = null; speed = 1.0;
        for (var i = 0; i < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--replay" when i + 1 < argv.Length: replayFile = argv[++i]; break;
                case "--speed" when i + 1 < argv.Length: double.TryParse(argv[++i], out speed); break;
                case "--port" when i + 1 < argv.Length:
                    if (int.TryParse(argv[++i], out var p)) config.Port = p; break;
                case "--opacity" when i + 1 < argv.Length:
                    if (double.TryParse(argv[++i], out var o)) config.Opacity = Math.Clamp(o, 0.2, 1.0); break;
                case "--layout" when i + 1 < argv.Length:
                    if (Enum.TryParse<OverlayLayout>(argv[++i], true, out var lay)) config.Layout = lay; break;
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pump?.Dispose();
        base.OnExit(e);
    }
}
