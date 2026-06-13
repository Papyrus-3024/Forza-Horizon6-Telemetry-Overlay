using System.Windows.Threading;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.ViewModels;

namespace Fh6.Telemetry.Overlay.Telemetry;

/// <summary>
/// Reads a telemetry source on a background thread, parses each frame, and pushes the
/// readout to the view model on the UI thread. Replay honors inter-frame timing.
/// </summary>
public sealed class TelemetryPump : IDisposable
{
    private readonly ITelemetrySource _source;
    private readonly TelemetryViewModel _viewModel;
    private readonly Dispatcher _dispatcher;
    private readonly bool _honorTiming;
    private readonly double _speed;
    private readonly Thread _thread;
    private volatile bool _running = true;

    public TelemetryPump(
        ITelemetrySource source,
        TelemetryViewModel viewModel,
        Dispatcher dispatcher,
        bool honorTiming = false,
        double speed = 1.0)
    {
        _source = source;
        _viewModel = viewModel;
        _dispatcher = dispatcher;
        _honorTiming = honorTiming;
        _speed = speed <= 0 ? 1.0 : speed;
        _thread = new Thread(Run) { IsBackground = true, Name = "TelemetryPump" };
    }

    public void Start() => _thread.Start();

    private void Run()
    {
        double? prevT = null;
        try
        {
            foreach (var frame in _source.Frames())
            {
                if (!_running) break;

                if (_honorTiming && prevT is double previous)
                {
                    var waitMs = (frame.TimestampMs - previous) / _speed;
                    if (waitMs > 0) Thread.Sleep(TimeSpan.FromMilliseconds(waitMs));
                }
                prevT = frame.TimestampMs;

                if (!PacketParser.TryParse(frame.Data, out var packet)) continue;
                var readout = new TelemetryReadout(packet);
                _dispatcher.BeginInvoke(() => _viewModel.Update(readout));
            }
        }
        catch (Exception)
        {
            // Source closed/disposed during shutdown; end the loop quietly.
        }
    }

    public void Dispose()
    {
        _running = false;
        (_source as IDisposable)?.Dispose();
    }
}
