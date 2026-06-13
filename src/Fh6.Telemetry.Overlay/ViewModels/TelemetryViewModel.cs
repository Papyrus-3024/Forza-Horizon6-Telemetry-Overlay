using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Fh6.Telemetry.Core;

namespace Fh6.Telemetry.Overlay.ViewModels;

public sealed class TelemetryViewModel : INotifyPropertyChanged
{
    // Shift-light "off" dim color (same for all dots)
    private static readonly Brush Off = Frozen(0x24, 0x30, 0x18);

    // Shift-light "on" colors: green → green-yellow → yellow → orange → red
    private static readonly Brush OnLight1 = Frozen(0x3F, 0xBF, 0x3F);
    private static readonly Brush OnLight2 = Frozen(0x86, 0xC5, 0x3A);
    private static readonly Brush OnLight3 = Frozen(0xE0, 0xC9, 0x3A);
    private static readonly Brush OnLight4 = Frozen(0xE0, 0x8A, 0x3A);
    private static readonly Brush OnLight5 = Frozen(0xE0, 0x5A, 0x5A);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private int? _prevGear;

    public string Speed { get; set; } = "0";
    public string SpeedMph { get; set; } = "0";
    public string Gear { get; set; } = "N";
    public string Rpm { get; set; } = "0 / 0";
    public double RpmFraction { get; set; }
    public double Throttle { get; set; }
    public double Brake { get; set; }
    public double Clutch { get; set; }
    public double Steer { get; set; }       // -1..1
    public string ThrottlePct { get; set; } = "0";
    public string BrakePct { get; set; } = "0";
    public string ClutchPct { get; set; } = "0";
    public string Boost { get; set; } = "0.0";
    public double GLat { get; set; }
    public double GLong { get; set; }
    public string GText { get; set; } = "0.0g";
    public string PowerHp { get; set; } = "0";
    public string TorqueLbFt { get; set; } = "0";
    public string Fuel { get; set; } = "0%";
    public string LapNumber { get; set; } = "0";
    public string Position { get; set; } = "-";
    public string CurrentLap { get; set; } = "--:--.---";
    public string LastLap { get; set; } = "--:--.---";
    public string BestLap { get; set; } = "--:--.---";
    public string LastShift { get; set; } = "-";
    public string Status { get; set; } = "";

    public Brush Light1 { get; set; } = Off;
    public Brush Light2 { get; set; } = Off;
    public Brush Light3 { get; set; } = Off;
    public Brush Light4 { get; set; } = Off;
    public Brush Light5 { get; set; } = Off;

    public void Update(in TelemetryReadout r)
    {
        Speed = $"{r.SpeedKmh:F0}";
        SpeedMph = $"{r.SpeedMph:F0}";
        Gear = r.Gear == 0 ? "R" : r.Gear.ToString();
        Rpm = $"{r.Rpm:F0} / {r.MaxRpm:F0}";
        RpmFraction = r.RpmFraction;
        Throttle = r.ThrottleFraction;
        Brake = r.BrakeFraction;
        Clutch = r.ClutchFraction;
        Steer = r.SteerFraction;
        ThrottlePct = $"{r.ThrottleFraction * 100:F0}";
        BrakePct = $"{r.BrakeFraction * 100:F0}";
        ClutchPct = $"{r.ClutchFraction * 100:F0}";
        Boost = $"{r.Boost:F1}";
        GLat        = r.LatG;
        GLong       = r.LongG;
        GText       = $"{Math.Sqrt(r.LatG * r.LatG + r.LongG * r.LongG):F1}g";
        PowerHp     = $"{r.PowerHp:F0}";
        TorqueLbFt  = $"{r.TorqueLbFt:F0}";
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

        int stage = r.ShiftLightStage;
        Light1 = stage >= 1 ? OnLight1 : Off;
        Light2 = stage >= 2 ? OnLight2 : Off;
        Light3 = stage >= 3 ? OnLight3 : Off;
        Light4 = stage >= 4 ? OnLight4 : Off;
        Light5 = stage >= 5 ? OnLight5 : Off;

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
        nameof(ThrottlePct), nameof(BrakePct), nameof(ClutchPct),
        nameof(Boost), nameof(Fuel), nameof(LapNumber), nameof(Position),
        nameof(CurrentLap), nameof(LastLap), nameof(BestLap), nameof(LastShift),
        nameof(Light1), nameof(Light2), nameof(Light3), nameof(Light4), nameof(Light5),
        nameof(SpeedMph), nameof(GLat), nameof(GLong), nameof(GText),
        nameof(PowerHp), nameof(TorqueLbFt),
    };
}
