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

    // ── Discrete text / status (updated immediately in Update) ──────────────
    public string Speed { get; set; } = "0";
    public string SpeedMph { get; set; } = "0";
    public string Gear { get; set; } = "N";
    public string Rpm { get; set; } = "0 / 0";
    public string ThrottlePct { get; set; } = "0";
    public string BrakePct { get; set; } = "0";
    public string ClutchPct { get; set; } = "0";
    public string Boost { get; set; } = "0.0";
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

    // ── World position (ground plane) ────────────────────────────────────────
    public double WorldX { get; set; }
    public double WorldZ { get; set; }

    public Brush Light1 { get; set; } = Off;
    public Brush Light2 { get; set; } = Off;
    public Brush Light3 { get; set; } = Off;
    public Brush Light4 { get; set; } = Off;
    public Brush Light5 { get; set; } = Off;

    // ── Animation targets (set by Update, not directly bound) ───────────────
    // These are the raw values coming off the wire at packet rate.
    private double _targetRpmFraction;
    private double _targetThrottle;
    private double _targetBrake;
    private double _targetClutch;
    private double _targetSteer;
    private double _targetGLat;
    private double _targetGLong;

    // ── Displayed (smoothed) values — bound by widgets ──────────────────────
    // Bars and dot read these; they lag the targets by the easing factor.
    public double DisplayedRpmFraction { get; private set; }
    public double DisplayedThrottle    { get; private set; }
    public double DisplayedBrake       { get; private set; }
    public double DisplayedClutch      { get; private set; }
    public double DisplayedSteer       { get; private set; }
    public double DisplayedGLat        { get; private set; }
    public double DisplayedGLong       { get; private set; }

    // Legacy read-only pass-throughs kept so GForceWidget DP bindings still compile.
    // The code-behind that drives the dot should bind to DisplayedGLat/GLong instead.
    public double GLat  => DisplayedGLat;
    public double GLong => DisplayedGLong;

    // Easing rate: displayed value moves (rate * dt) of the way to target each frame.
    // rate=12 means ~63% of the gap closes in 1/12 s ≈ 83 ms — feels fluid at 60 Hz.
    private const double EaseRate = 12.0;

    public void Update(in TelemetryReadout r)
    {
        Speed = $"{r.SpeedKmh:F0}";
        SpeedMph = $"{r.SpeedMph:F0}";
        Gear = r.Gear == 0 ? "R" : r.Gear.ToString();
        Rpm = $"{r.Rpm:F0} / {r.MaxRpm:F0}";

        // Set animation targets (bars will ease toward these)
        _targetRpmFraction = r.RpmFraction;
        _targetThrottle    = r.ThrottleFraction;
        _targetBrake       = r.BrakeFraction;
        _targetClutch      = r.ClutchFraction;
        _targetSteer       = r.SteerFraction;
        _targetGLat        = r.LatG;
        _targetGLong       = r.LongG;

        ThrottlePct = $"{r.ThrottleFraction * 100:F0}";
        BrakePct = $"{r.BrakeFraction * 100:F0}";
        ClutchPct = $"{r.ClutchFraction * 100:F0}";
        Boost = $"{r.Boost:F1}";
        GText = $"{Math.Sqrt(r.LatG * r.LatG + r.LongG * r.LongG):F1}g";
        PowerHp     = $"{r.PowerHp:F0}";
        TorqueLbFt  = $"{r.TorqueLbFt:F0}";
        Fuel = $"{r.FuelPercent:F0}%";
        LapNumber = r.LapNumber.ToString();
        Position = r.RacePosition.ToString();
        CurrentLap = LapTime.Format(r.CurrentLap);
        LastLap = LapTime.Format(r.LastLap);
        BestLap = LapTime.Format(r.BestLap);

        WorldX = r.PositionX;
        WorldZ = r.PositionZ;

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

        RaiseDiscrete();
    }

    /// <summary>
    /// Called once per rendered frame from CompositionTarget.Rendering in OverlayWindow.
    /// Eases each displayed value toward its target and fires PropertyChanged for the
    /// smoothed props so widgets re-render. Allocation-free hot path.
    /// </summary>
    public void Tick(double dtSeconds)
    {
        // alpha = fraction of gap to close this frame; clamped to [0,1]
        double alpha = Math.Clamp(dtSeconds * EaseRate, 0.0, 1.0);

        DisplayedRpmFraction = Ease(DisplayedRpmFraction, _targetRpmFraction, alpha);
        DisplayedThrottle    = Ease(DisplayedThrottle,    _targetThrottle,    alpha);
        DisplayedBrake       = Ease(DisplayedBrake,       _targetBrake,       alpha);
        DisplayedClutch      = Ease(DisplayedClutch,      _targetClutch,      alpha);
        DisplayedSteer       = Ease(DisplayedSteer,       _targetSteer,       alpha);
        DisplayedGLat        = Ease(DisplayedGLat,        _targetGLat,        alpha);
        DisplayedGLong       = Ease(DisplayedGLong,       _targetGLong,       alpha);

        RaiseSmoothed();
    }

    /// <summary>Sets a status line (e.g. a telemetry-source error) and notifies the UI.</summary>
    public void SetStatus(string status)
    {
        Status = status;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Ease(double current, double target, double alpha)
        => current + (target - current) * alpha;

    // Raise for all discrete (text / brush) props — called from Update at packet rate.
    private void RaiseDiscrete()
    {
        foreach (var name in DiscreteNames)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Raise for the smoothed display props — called from Tick at render rate.
    private void RaiseSmoothed()
    {
        foreach (var name in SmoothedNames)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static readonly string[] DiscreteNames =
    {
        nameof(Speed), nameof(SpeedMph), nameof(Gear), nameof(Rpm),
        nameof(ThrottlePct), nameof(BrakePct), nameof(ClutchPct),
        nameof(Boost), nameof(Fuel), nameof(LapNumber), nameof(Position),
        nameof(CurrentLap), nameof(LastLap), nameof(BestLap), nameof(LastShift),
        nameof(Light1), nameof(Light2), nameof(Light3), nameof(Light4), nameof(Light5),
        nameof(GText), nameof(PowerHp), nameof(TorqueLbFt),
        nameof(WorldX), nameof(WorldZ),
    };

    private static readonly string[] SmoothedNames =
    {
        nameof(DisplayedRpmFraction),
        nameof(DisplayedThrottle),
        nameof(DisplayedBrake),
        nameof(DisplayedClutch),
        nameof(DisplayedSteer),
        nameof(DisplayedGLat),
        nameof(DisplayedGLong),
        // Also raise GLat/GLong so GForceWidget DP binding (which targets these) stays live.
        nameof(GLat),
        nameof(GLong),
    };
}
