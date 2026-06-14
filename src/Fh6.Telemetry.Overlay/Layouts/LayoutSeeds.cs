using Fh6.Telemetry.Overlay.Settings;
using Fh6.Telemetry.Overlay.Widgets;

namespace Fh6.Telemetry.Overlay.Layouts;

/// <summary>Starting X/Y/Scale/Visible for a widget within a preset arrangement.</summary>
public readonly record struct WidgetSeed(double X, double Y, double Scale = 1.0, bool Visible = true);

/// <summary>
/// The three preset arrangements expressed as seed coordinates.
/// X/Y are absolute DIPs inside the overlay Canvas.
/// These are derived from the *Layout.xaml StackPanel arrangements and serve as the
/// user's starting point — they can drag widgets freely after a preset is applied.
/// </summary>
public static class LayoutSeeds
{
    public static IReadOnlyDictionary<WidgetId, WidgetSeed> For(OverlayLayout layout) => layout switch
    {
        OverlayLayout.CornerPanel => CornerPanel,
        OverlayLayout.CenterDash  => CenterDash,
        _                          => BottomStrip,
    };

    // BottomStrip: compact horizontal row at Y=8, ~8px gaps between widgets.
    // Approximate rendered widths after v2 size bump (CornerRadius=10, larger fonts):
    //   Gear ~82, Speed ~116, RpmShift ~194, PedalsSteer ~98, Boost ~130, LapTiming ~166, GForce ~104, PowerTorque ~200, Tire ~154
    // Chart (~320x180) placed off to the right, hidden by default.
    // Tire placed immediately after the chart, hidden by default (shown in CornerPanel).
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> BottomStrip =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.Gear]        = new(  8,   8),
            [WidgetId.Speed]       = new( 98,   8),
            [WidgetId.RpmShift]    = new(222,   8),
            [WidgetId.PedalsSteer] = new(424,   8),
            [WidgetId.Boost]       = new(530,   8),
            [WidgetId.LapTiming]   = new(668,   8),
            [WidgetId.GForce]      = new(842,   8),
            [WidgetId.PowerTorque] = new(954,   8),
            [WidgetId.Chart]       = new(1162,  8, Visible: false),
            [WidgetId.Tire]        = new(1162,  8, Visible: false),
        };

    // CornerPanel: clean stacked cluster near top-left, ~10px gaps, all on-screen.
    // Row 1 (Y=8):   Gear | Speed  (side by side; Gear ~82px wide)
    // Row 2 (Y=106): RpmShift      (spans the full cluster width ~194px)
    // Row 3 (Y=196): PedalsSteer | Boost (side by side; PedalsSteer ~98px wide)
    // Row 4 (Y=316): LapTiming
    // Row 5 (Y=448): GForce | PowerTorque
    // Row 6 (Y=570): Tire
    // Chart below the cluster, hidden by default.
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> CornerPanel =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.Gear]        = new(  8,   8),
            [WidgetId.Speed]       = new( 98,   8),
            [WidgetId.RpmShift]    = new(  8, 106),
            [WidgetId.PedalsSteer] = new(  8, 196),
            [WidgetId.Boost]       = new(114, 196),
            [WidgetId.LapTiming]   = new(  8, 316),
            [WidgetId.GForce]      = new(  8, 448),
            [WidgetId.PowerTorque] = new(120, 448),
            [WidgetId.Tire]        = new(  8, 570),
            [WidgetId.Chart]       = new(  8, 720, Visible: false),
        };

    // CenterDash: key widgets centered at ~960px on a 1920-wide screen.
    // PedalsSteer left of center, RpmShift above Gear/Speed, LapTiming right of center.
    // Non-essential widgets hidden. Chart and Tire placed below, hidden by default.
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> CenterDash =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.PedalsSteer] = new(860, 816),
            [WidgetId.RpmShift]    = new(966, 764),
            [WidgetId.Gear]        = new(966, 840),
            [WidgetId.Speed]       = new(1056, 840),
            [WidgetId.LapTiming]   = new(1180, 816),
            [WidgetId.Boost]       = new(860,  900, Visible: false),
            [WidgetId.GForce]      = new(860,  990, Visible: false),
            [WidgetId.PowerTorque] = new(972,  990, Visible: false),
            [WidgetId.Tire]        = new(1180, 900, Visible: false),
            [WidgetId.Chart]       = new(640,  900, Visible: false),
        };
}
