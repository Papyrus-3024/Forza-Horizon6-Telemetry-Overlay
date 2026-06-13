using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Fh6.Telemetry.Core;

namespace Fh6.Telemetry.Overlay.ViewModels;

public sealed class TelemetryViewModel : INotifyPropertyChanged
{
    private static readonly Brush Off = Frozen(0x3a, 0x4a, 0x32);
    private static readonly Brush Green = Frozen(0x5a, 0xd1, 0x5a);
    private static readonly Brush Amber = Frozen(0xe0, 0xc9, 0x3a);
    private static readonly Brush Red = Frozen(0xe0, 0x5a, 0x5a);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private int? _prevGear;

    public string Speed { get; private set; } = "0";
    public string Gear { get; private set; } = "N";
    public string Rpm { get; private set; } = "0 / 0";
    public double RpmFraction { get; private set; }
    public double Throttle { get; private set; }
    public double Brake { get; private set; }
    public double Clutch { get; private set; }
    public double Steer { get; private set; }       // -1..1
    public string Boost { get; private set; } = "0.0";
    public string Fuel { get; private set; } = "0%";
    public string LapNumber { get; private set; } = "0";
    public string Position { get; private set; } = "-";
    public string CurrentLap { get; private set; } = "--:--.---";
    public string LastLap { get; private set; } = "--:--.---";
    public string BestLap { get; private set; } = "--:--.---";
    public string LastShift { get; private set; } = "-";
    public string Status { get; private set; } = "";

    public Brush Light1 { get; private set; } = Off;
    public Brush Light2 { get; private set; } = Off;
    public Brush Light3 { get; private set; } = Off;
    public Brush Light4 { get; private set; } = Off;
    public Brush Light5 { get; private set; } = Off;

    public void Update(in TelemetryReadout r)
    {
        Speed = $"{r.SpeedKmh:F0}";
        Gear = r.Gear == 0 ? "R" : r.Gear.ToString();
        Rpm = $"{r.Rpm:F0} / {r.MaxRpm:F0}";
        RpmFraction = r.RpmFraction;
        Throttle = r.ThrottleFraction;
        Brake = r.BrakeFraction;
        Clutch = r.ClutchFraction;
        Steer = r.SteerFraction;
        Boost = $"{r.Boost:F1}";
        Fuel = $"{r.FuelPercent:F0}%";
        LapNumber = r.LapNumber.ToString();
        Position = r.RacePosition.ToString();
        CurrentLap = LapTime.Format(r.CurrentLap);
        LastLap = LapTime.Format(r.LastLap);
        BestLap = LapTime.Format(r.BestLap);

        var shift = GearShifts.Detect(_prevGear, r.Gear);
        if (shift == ShiftDirection.Up) LastShift = $"up -> {r.Gear}";
        else if (shift == ShiftDirection.Down) LastShift = $"down -> {r.Gear}";
        _prevGear = r.Gear;

        Light1 = r.ShiftLightStage >= 1 ? Green : Off;
        Light2 = r.ShiftLightStage >= 2 ? Green : Off;
        Light3 = r.ShiftLightStage >= 3 ? Green : Off;
        Light4 = r.ShiftLightStage >= 4 ? Amber : Off;
        Light5 = r.ShiftLightStage >= 5 ? Red : Off;

        RaiseAll();
    }

    /// <summary>Sets a status line (e.g. a telemetry-source error) and notifies the UI.</summary>
    public void SetStatus(string status)
    {
        Status = status;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseAll()
    {
        // Simple and sufficient for ~60Hz: notify every bound property each frame.
        foreach (var name in BoundNames)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static readonly string[] BoundNames =
    {
        nameof(Speed), nameof(Gear), nameof(Rpm), nameof(RpmFraction),
        nameof(Throttle), nameof(Brake), nameof(Clutch), nameof(Steer),
        nameof(Boost), nameof(Fuel), nameof(LapNumber), nameof(Position),
        nameof(CurrentLap), nameof(LastLap), nameof(BestLap), nameof(LastShift),
        nameof(Light1), nameof(Light2), nameof(Light3), nameof(Light4), nameof(Light5),
    };
}
