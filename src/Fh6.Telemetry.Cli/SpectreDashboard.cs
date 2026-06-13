using Fh6.Telemetry.Core;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Fh6.Telemetry.Cli;

public sealed class SpectreDashboard : IDashboard
{
    public void Run(Action<Action<TelemetryPacket>> driver)
    {
        AnsiConsole.Live(Render(default))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
                driver(packet =>
                {
                    ctx.UpdateTarget(Render(packet));
                    ctx.Refresh();
                });
            });
    }

    private static IRenderable Render(TelemetryPacket p)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("Race", p.IsRaceOn == 1 ? "[green]ON[/]" : "[grey]off[/]");
        grid.AddRow("Speed", $"{p.Speed * 3.6f:F1} km/h  ({p.Speed:F1} m/s)");
        grid.AddRow("Gear", p.Gear.ToString());
        grid.AddRow("RPM", $"{p.CurrentEngineRpm:F0} / {p.EngineMaxRpm:F0}");
        grid.AddRow("Throttle / Brake", $"{p.Accel} / {p.Brake}");
        grid.AddRow("Clutch / Handbrake", $"{p.Clutch} / {p.HandBrake}");
        grid.AddRow("Steer", p.Steer.ToString());
        grid.AddRow("Boost", $"{p.Boost:F1} psi");
        grid.AddRow("Fuel", $"{p.Fuel * 100f:F0}%");
        grid.AddRow("Combined slip (FL FR RL RR)",
            $"{p.TireCombinedSlip.FrontLeft:F2} {p.TireCombinedSlip.FrontRight:F2} " +
            $"{p.TireCombinedSlip.RearLeft:F2} {p.TireCombinedSlip.RearRight:F2}");
        grid.AddRow("Tire temp (FL FR RL RR)",
            $"{p.TireTemp.FrontLeft:F0} {p.TireTemp.FrontRight:F0} " +
            $"{p.TireTemp.RearLeft:F0} {p.TireTemp.RearRight:F0}");
        grid.AddRow("Lap", $"{p.LapNumber}  cur {p.CurrentLap:F2}s  last {p.LastLap:F2}s  best {p.BestLap:F2}s");
        grid.AddRow("Position", p.RacePosition.ToString());
        grid.AddRow("Car", $"ordinal {p.CarOrdinal}  class {p.CarClass}  PI {p.CarPerformanceIndex}");

        return new Panel(grid).Header("FH6 Telemetry").Expand();
    }
}
