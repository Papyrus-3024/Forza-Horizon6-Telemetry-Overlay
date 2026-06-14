using System.Windows.Threading;
using Fh6.Telemetry.Core;
using Fh6.Telemetry.Overlay.Diagnostics;
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
    private readonly string _diagLabel;
    private readonly Thread _thread;
    private readonly object _gate = new();
    private TelemetryReadout _latest;
    private bool _pending;
    private volatile bool _running = true;
    private long _received;
    private Timer? _watchdog;

    // Rolling packets/sec window (computed on the pump thread, posted to the VM ~1 Hz).
    private long _ppsWindowStartMs;
    private int _ppsCount;

    public TelemetryPump(
        ITelemetrySource source,
        TelemetryViewModel viewModel,
        Dispatcher dispatcher,
        bool honorTiming = false,
        double speed = 1.0,
        string diagnosticsLabel = "")
    {
        _source = source;
        _viewModel = viewModel;
        _dispatcher = dispatcher;
        _honorTiming = honorTiming;
        _speed = speed <= 0 ? 1.0 : speed;
        _diagLabel = diagnosticsLabel;
        _thread = new Thread(Run) { IsBackground = true, Name = "TelemetryPump" };
    }

    public void Start()
    {
        _thread.Start();
        // One-shot watchdog: if nothing arrived after 6s, log a hint (the common cause is the
        // game's Data Out IP not pointing at this PC, a port mismatch, or a second listener).
        _watchdog = new Timer(_ =>
        {
            if (Interlocked.Read(ref _received) == 0)
                OverlayLog.Write("WATCHDOG: no packets after 6s. Check FH6 Data Out is ON, its IP " +
                                 "points at THIS pc (127.0.0.1 if same machine), the port matches, " +
                                 "and no other app is bound to the port.");
        }, null, 6000, Timeout.Infinite);
    }

    private void Run()
    {
        double? prevT = null;
        try
        {
            foreach (var frame in _source.Frames())
            {
                if (!_running) break;

                var count = Interlocked.Increment(ref _received);
                if (count == 1)
                {
                    OverlayLog.Write($"first packet received ({frame.Data.Length} bytes)");
                    _ppsWindowStartMs = Environment.TickCount64;
                }
                else if (count % 600 == 0)
                    OverlayLog.Write($"received {count} packets");

                // Rolling packets/sec, posted to the VM about once a second.
                _ppsCount++;
                long nowMs = Environment.TickCount64;
                long elapsed = nowMs - _ppsWindowStartMs;
                if (elapsed >= 1000)
                {
                    double pps = _ppsCount * 1000.0 / elapsed;
                    _ppsCount = 0;
                    _ppsWindowStartMs = nowMs;
                    var line = $"{pps:F0} pkt/s · {_diagLabel}";
                    _dispatcher.BeginInvoke(() => _viewModel.SetDiagnostics(line));
                }

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
        catch (Exception ex)
        {
            // Source closed/disposed during shutdown; end the loop quietly.
            if (_running) OverlayLog.Write($"pump loop ended: {ex.GetType().Name}: {ex.Message}");
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
        _watchdog?.Dispose();
        (_source as IDisposable)?.Dispose();
        if (_thread.IsAlive)
            _thread.Join(TimeSpan.FromSeconds(1));
    }
}
