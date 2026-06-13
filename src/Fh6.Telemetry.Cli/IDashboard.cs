using Fh6.Telemetry.Core;

namespace Fh6.Telemetry.Cli;

public interface IDashboard
{
    /// <summary>
    /// Runs a live refreshing display. The driver is invoked with a callback that should be
    /// called once per parsed packet to update the screen; the driver controls iteration/timing.
    /// </summary>
    void Run(Action<Action<TelemetryPacket>> driver);
}
