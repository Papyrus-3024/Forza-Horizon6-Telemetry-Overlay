using System.Windows.Threading;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.ViewModels;

namespace Fh6.Telemetry.Overlay.Telemetry;

/// <summary>
/// Reads a telemetry source on a background thread, parses each frame, and pushes the
/// readout to the view model on the UI thread. Updates are coalesced so at most one
/// dispatcher update is queued at a time (no backlog if the UI stalls). Replay honors timing.
/// </summary>
public sealed class TelemetryPump : IDisposable
{
    private readonly ITelemetrySource _source;
    private readonly TelemetryViewModel _viewModel;
    private readonly Dispatcher _dispatcher;
    private readonly bool _honorTiming;
    private readonly double _speed;
    private readonly Thread _thread;
    private readonly object _gate = new();
    private TelemetryReadout _latest;
    private bool _pending;
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

                // Coalesce: always record the newest readout, but only queue one UI update
                // at a time. A queued update publishes whatever the latest is when it runs.
                bool post;
                lock (_gate)
                {
                    _latest = readout;
                    post = !_pending;
                    if (post) _pending = true;
                }
                if (post)
                    _dispatcher.BeginInvoke(PublishLatest);
            }
        }
        catch (Exception)
        {
            // Source closed/disposed during shutdown; end the loop quietly.
        }
    }

    private void PublishLatest()
    {
        TelemetryReadout readout;
        lock (_gate)
        {
            readout = _latest;
            _pending = false;
        }
        _viewModel.Update(readout);
    }

    public void Dispose()
    {
        _running = false;
        (_source as IDisposable)?.Dispose();
        if (_thread.IsAlive)
            _thread.Join(TimeSpan.FromSeconds(1));
    }
}
