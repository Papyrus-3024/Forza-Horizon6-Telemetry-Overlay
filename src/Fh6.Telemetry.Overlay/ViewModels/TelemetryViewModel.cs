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

    // Ring-buffer capacity: 120 s max window × 60 Hz + headroom (~384 KB at ~48 B/sample).
    private const int ChartCapacity = 8000;

    private int? _prevGear;
    private bool _prevIsRaceOn;

    /// <summary>Ring-buffer chart history, owned by the VM and fed on each <see cref="Update"/> call.</summary>
    public ChartHistory History { get; } = new(ChartCapacity);

    // ── Discrete text / status — explicit properties backed by private fields ─
    // Each only raises PropertyChanged when the value actually changes.
    private string _speed      = "0";
    private string _speedMph   = "0";
    private string _gear       = "N";
    private string _rpm        = "0 / 0";
    private string _throttlePct = "0";
    private string _brakePct   = "0";
    private string _clutchPct  = "0";
    private string _boost      = "0.0";
    private string _gText      = "0.0g";
    private string _powerHp    = "0";
    private string _torqueLbFt = "0";
    private string _fuel       = "0%";
    private string _lapNumber  = "0";
    private string _position   = "-";
    private string _currentLap = "--:--.---";
    private string _lastLap    = "--:--.---";
    private string _bestLap    = "--:--.---";
    private string _lastShift  = "-";
    private string _status     = "";

    // Public setters exist only so WPF's TwoWay-default bindings (Run.Text, RangeBase.Value)
    // are legal; display controls never write back. Change-notification happens in the Set* helpers.
    public string Speed       { get => _speed;      set => _speed = value; }
    public string SpeedMph    { get => _speedMph;   set => _speedMph = value; }
    public string Gear        { get => _gear;       set => _gear = value; }
    public string Rpm         { get => _rpm;        set => _rpm = value; }
    public string ThrottlePct { get => _throttlePct; set => _throttlePct = value; }
    public string BrakePct    { get => _brakePct;   set => _brakePct = value; }
    public string ClutchPct   { get => _clutchPct;  set => _clutchPct = value; }
    public string Boost       { get => _boost;      set => _boost = value; }
    public string GText       { get => _gText;      set => _gText = value; }
    public string PowerHp     { get => _powerHp;    set => _powerHp = value; }
    public string TorqueLbFt  { get => _torqueLbFt; set => _torqueLbFt = value; }
    public string Fuel        { get => _fuel;       set => _fuel = value; }
    public string LapNumber   { get => _lapNumber;  set => _lapNumber = value; }
    public string Position    { get => _position;   set => _position = value; }
    public string CurrentLap  { get => _currentLap; set => _currentLap = value; }
    public string LastLap     { get => _lastLap;    set => _lastLap = value; }
    public string BestLap     { get => _bestLap;    set => _bestLap = value; }
    public string LastShift   { get => _lastShift;  set => _lastShift = value; }
    public string Status      { get => _status;     set => _status = value; }

    // ── World position (ground plane) ────────────────────────────────────────
    private double _worldX;
    private double _worldZ;
    public double WorldX { get => _worldX; set => _worldX = value; }
    public double WorldZ { get => _worldZ; set => _worldZ = value; }

    // ── Per-corner tire telemetry (for tire widget) ──────────────────────────
    private double _tireTempFL; private double _tireTempFR;
    private double _tireTempRL; private double _tireTempRR;
    private double _tireSlipFL; private double _tireSlipFR;
    private double _tireSlipRL; private double _tireSlipRR;

    // Tire temperatures (°F, one per corner)
    public double TireTempFL { get => _tireTempFL; set => _tireTempFL = value; }
    public double TireTempFR { get => _tireTempFR; set => _tireTempFR = value; }
    public double TireTempRL { get => _tireTempRL; set => _tireTempRL = value; }
    public double TireTempRR { get => _tireTempRR; set => _tireTempRR = value; }

    // Combined slip per corner (0 = full grip; higher = more slip)
    public double TireSlipFL { get => _tireSlipFL; set => _tireSlipFL = value; }
    public double TireSlipFR { get => _tireSlipFR; set => _tireSlipFR = value; }
    public double TireSlipRL { get => _tireSlipRL; set => _tireSlipRL = value; }
    public double TireSlipRR { get => _tireSlipRR; set => _tireSlipRR = value; }

    // ── Shift lights ─────────────────────────────────────────────────────────
    private Brush _light1 = Off;
    private Brush _light2 = Off;
    private Brush _light3 = Off;
    private Brush _light4 = Off;
    private Brush _light5 = Off;
    public Brush Light1 { get => _light1; set => _light1 = value; }
    public Brush Light2 { get => _light2; set => _light2 = value; }
    public Brush Light3 { get => _light3; set => _light3 = value; }
    public Brush Light4 { get => _light4; set => _light4 = value; }
    public Brush Light5 { get => _light5; set => _light5 = value; }

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
    private double _displayedRpmFraction;
    private double _displayedThrottle;
    private double _displayedBrake;
    private double _displayedClutch;
    private double _displayedSteer;
    private double _displayedGLat;
    private double _displayedGLong;

    public double DisplayedRpmFraction { get => _displayedRpmFraction; set => _displayedRpmFraction = value; }
    public double DisplayedThrottle    { get => _displayedThrottle;    set => _displayedThrottle = value; }
    public double DisplayedBrake       { get => _displayedBrake;       set => _displayedBrake = value; }
    public double DisplayedClutch      { get => _displayedClutch;      set => _displayedClutch = value; }
    public double DisplayedSteer       { get => _displayedSteer;       set => _displayedSteer = value; }
    public double DisplayedGLat        { get => _displayedGLat;        set => _displayedGLat = value; }
    public double DisplayedGLong       { get => _displayedGLong;       set => _displayedGLong = value; }

    // Legacy read-only pass-throughs kept so GForceWidget DP bindings still compile.
    // The code-behind that drives the dot should bind to DisplayedGLat/GLong instead.
    public double GLat  => _displayedGLat;
    public double GLong => _displayedGLong;

    // Easing rate: displayed value moves (rate * dt) of the way to target each frame.
    // rate=12 means ~63% of the gap closes in 1/12 s ≈ 83 ms — feels fluid at 60 Hz.
    private const double EaseRate = 12.0;

    // Epsilon for eased-double change detection: below this the visual delta is imperceptible.
    private const double SmoothedEpsilon = 1e-3;

    public void Update(in TelemetryReadout r)
    {
        SetStr(ref _speed,      $"{r.SpeedKmh:F0}",   nameof(Speed));
        SetStr(ref _speedMph,   $"{r.SpeedMph:F0}",   nameof(SpeedMph));
        SetStr(ref _gear,       r.Gear == 0 ? "R" : r.Gear.ToString(), nameof(Gear));
        SetStr(ref _rpm,        $"{r.Rpm:F0} / {r.MaxRpm:F0}", nameof(Rpm));

        // Set animation targets (bars will ease toward these)
        _targetRpmFraction = r.RpmFraction;
        _targetThrottle    = r.ThrottleFraction;
        _targetBrake       = r.BrakeFraction;
        _targetClutch      = r.ClutchFraction;
        _targetSteer       = r.SteerFraction;
        _targetGLat        = r.LatG;
        _targetGLong       = r.LongG;

        SetStr(ref _throttlePct, $"{r.ThrottleFraction * 100:F0}", nameof(ThrottlePct));
        SetStr(ref _brakePct,    $"{r.BrakeFraction * 100:F0}",    nameof(BrakePct));
        SetStr(ref _clutchPct,   $"{r.ClutchFraction * 100:F0}",   nameof(ClutchPct));
        SetStr(ref _boost,       $"{r.Boost:F1}",                  nameof(Boost));
        SetStr(ref _gText,       $"{Math.Sqrt(r.LatG * r.LatG + r.LongG * r.LongG):F1}g", nameof(GText));
        SetStr(ref _powerHp,     $"{r.PowerHp:F0}",                nameof(PowerHp));
        SetStr(ref _torqueLbFt,  $"{r.TorqueLbFt:F0}",            nameof(TorqueLbFt));
        SetStr(ref _fuel,        $"{r.FuelPercent:F0}%",           nameof(Fuel));
        SetStr(ref _lapNumber,   r.LapNumber.ToString(),           nameof(LapNumber));
        SetStr(ref _position,    r.RacePosition.ToString(),        nameof(Position));
        SetStr(ref _currentLap,  LapTime.Format(r.CurrentLap),     nameof(CurrentLap));
        SetStr(ref _lastLap,     LapTime.Format(r.LastLap),        nameof(LastLap));
        SetStr(ref _bestLap,     LapTime.Format(r.BestLap),        nameof(BestLap));

        // World position: any change in the raw float is meaningful (minimap accuracy).
        SetDouble(ref _worldX, r.PositionX, nameof(WorldX));
        SetDouble(ref _worldZ, r.PositionZ, nameof(WorldZ));

        // Per-corner tire telemetry
        SetDouble(ref _tireTempFL, r.TireTemp.FrontLeft,        nameof(TireTempFL));
        SetDouble(ref _tireTempFR, r.TireTemp.FrontRight,       nameof(TireTempFR));
        SetDouble(ref _tireTempRL, r.TireTemp.RearLeft,         nameof(TireTempRL));
        SetDouble(ref _tireTempRR, r.TireTemp.RearRight,        nameof(TireTempRR));
        SetDouble(ref _tireSlipFL, r.TireCombinedSlip.FrontLeft,  nameof(TireSlipFL));
        SetDouble(ref _tireSlipFR, r.TireCombinedSlip.FrontRight, nameof(TireSlipFR));
        SetDouble(ref _tireSlipRL, r.TireCombinedSlip.RearLeft,   nameof(TireSlipRL));
        SetDouble(ref _tireSlipRR, r.TireCombinedSlip.RearRight,  nameof(TireSlipRR));

        var shift = GearShifts.Detect(_prevGear, r.Gear);
        if (shift == ShiftDirection.Up)        SetStr(ref _lastShift, $"up -> {r.Gear}",   nameof(LastShift));
        else if (shift == ShiftDirection.Down) SetStr(ref _lastShift, $"down -> {r.Gear}", nameof(LastShift));
        _prevGear = r.Gear;

        int stage = r.ShiftLightStage;
        SetBrush(ref _light1, stage >= 1 ? OnLight1 : Off, nameof(Light1));
        SetBrush(ref _light2, stage >= 2 ? OnLight2 : Off, nameof(Light2));
        SetBrush(ref _light3, stage >= 3 ? OnLight3 : Off, nameof(Light3));
        SetBrush(ref _light4, stage >= 4 ? OnLight4 : Off, nameof(Light4));
        SetBrush(ref _light5, stage >= 5 ? OnLight5 : Off, nameof(Light5));

        // Chart history: reset on the falling edge of IsRaceOn (menu/rewind), then sample.
        if (_prevIsRaceOn && !r.IsRaceOn)
            History.Reset();
        if (r.IsRaceOn)
            History.Add(r, r.TimestampMs);
        _prevIsRaceOn = r.IsRaceOn;
    }

    /// <summary>
    /// Called once per rendered frame from CompositionTarget.Rendering in OverlayWindow.
    /// Eases each displayed value toward its target and fires PropertyChanged only when a
    /// value has meaningfully changed. Allocation-free hot path.
    /// </summary>
    public void Tick(double dtSeconds)
    {
        // alpha = fraction of gap to close this frame; clamped to [0,1]
        double alpha = Math.Clamp(dtSeconds * EaseRate, 0.0, 1.0);

        EaseAndRaise(ref _displayedRpmFraction, _targetRpmFraction, alpha, nameof(DisplayedRpmFraction));
        EaseAndRaise(ref _displayedThrottle,    _targetThrottle,    alpha, nameof(DisplayedThrottle));
        EaseAndRaise(ref _displayedBrake,       _targetBrake,       alpha, nameof(DisplayedBrake));
        EaseAndRaise(ref _displayedClutch,      _targetClutch,      alpha, nameof(DisplayedClutch));
        EaseAndRaise(ref _displayedSteer,       _targetSteer,       alpha, nameof(DisplayedSteer));

        bool gLatChanged  = EaseAndRaise(ref _displayedGLat,  _targetGLat,  alpha, nameof(DisplayedGLat));
        bool gLongChanged = EaseAndRaise(ref _displayedGLong, _targetGLong, alpha, nameof(DisplayedGLong));

        // GLat/GLong are pass-through aliases; notify only if the underlying values changed.
        if (gLatChanged)  PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GLat)));
        if (gLongChanged) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GLong)));
    }

    /// <summary>Sets a status line (e.g. a telemetry-source error) and notifies the UI.</summary>
    public void SetStatus(string status)
    {
        SetStr(ref _status, status, nameof(Status));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Change-guarded helpers ───────────────────────────────────────────────

    // Writes the value to the backing field and raises PropertyChanged only when changed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetStr(ref string field, string value, string propName)
    {
        if (value == field) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetDouble(ref double field, double value, string propName)
    {
        if (value == field) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    // Brushes are frozen singletons — reference equality is the right comparison.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetBrush(ref Brush field, Brush value, string propName)
    {
        if (ReferenceEquals(value, field)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    // Eases current toward target, writes back, raises PropertyChanged if the
    // delta exceeds SmoothedEpsilon. Returns true if it raised.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EaseAndRaise(ref double field, double target, double alpha, string propName)
    {
        double next = field + (target - field) * alpha;
        if (Math.Abs(next - field) < SmoothedEpsilon) return false;
        field = next;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        return true;
    }
}
