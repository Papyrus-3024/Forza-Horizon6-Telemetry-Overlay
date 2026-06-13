using Fh6.Telemetry.Core;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Fh6.Telemetry.Cli;

public sealed class SpectreDashboard : IDashboard
{
    private int? _prevGear;
    private ShiftDirection _lastShift = ShiftDirection.None;
    private byte _lastShiftToGear;

    public void Run(Action<Action<TelemetryPacket>> driver)
    {
        AnsiConsole.Live(Render(default))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Start(ctx =>
            {
                driver(packet =>
                {
                    Observe(packet);
                    ctx.UpdateTarget(Render(packet));
                    ctx.Refresh();
                });
            });
    }

    // Derives shift events (FH6 only reports the current gear) and remembers the last one.
    private void Observe(in TelemetryPacket p)
    {
        var direction = GearShifts.Detect(_prevGear, p.Gear);
        if (direction != ShiftDirection.None)
        {
            _lastShift = direction;
            _lastShiftToGear = p.Gear;
        }
        _prevGear = p.Gear;
    }

    private IRenderable Render(TelemetryPacket p)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("Race", p.IsRaceOn == 1 ? "[green]ON[/]" : "[grey]off[/]");
        grid.AddRow("Speed", $"{p.Speed * 3.6f:F1} km/h  ({p.Speed:F1} m/s)");
        grid.AddRow("Gear", p.Gear.ToString());
        grid.AddRow("Last shift", FormatShift());
        grid.AddRow("RPM", $"{p.CurrentEngineRpm:F0} / {p.EngineMaxRpm:F0}");
        grid.AddRow("Throttle / Brake", $"{p.Accel} / {p.Brake}");
        grid.AddRow("Clutch", FormatClutch(p.Clutch));
        grid.AddRow("Handbrake", p.HandBrake.ToString());
        grid.AddRow("Steer", p.Steer.ToString());
        grid.AddRow("Boost", $"{p.Boost:F1} psi");
        grid.AddRow("Fuel", $"{p.Fuel * 100f:F0}%");
        grid.AddRow("On rumble strip", FormatRumble(p.WheelOnRumbleStrip));
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

    private string FormatShift() => _lastShift switch
    {
        ShiftDirection.Up => $"[green]up -> {_lastShiftToGear}[/]",
        ShiftDirection.Down => $"[blue]down -> {_lastShiftToGear}[/]",
        _ => "[grey]-[/]",
    };

    private static string FormatClutch(byte clutch) =>
        clutch > 0 ? $"[yellow]{clutch} (engaged)[/]" : "0";

    private static string FormatRumble(WheelsInt onStrip)
    {
        var wheels = new List<string>(4);
        if (onStrip.FrontLeft == 1) wheels.Add("FL");
        if (onStrip.FrontRight == 1) wheels.Add("FR");
        if (onStrip.RearLeft == 1) wheels.Add("RL");
        if (onStrip.RearRight == 1) wheels.Add("RR");
        return wheels.Count == 0 ? "[grey]none[/]" : $"[yellow]{string.Join(" ", wheels)}[/]";
    }
}
