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
    // Approximate rendered widths (theme paddings included):
    //   Gear ~68, Speed ~90, RpmShift ~176, PedalsSteer ~80, Boost ~96, LapTiming ~156, GForce ~96, PowerTorque ~102
    // MiniMap (~240x240) placed top-right area, away from the strip.
    // Chart (~320x180) placed below MiniMap, hidden by default.
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> BottomStrip =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.Gear]        = new(  8,  8),
            [WidgetId.Speed]       = new( 84,  8),
            [WidgetId.RpmShift]    = new(182,  8),
            [WidgetId.PedalsSteer] = new(366,  8),
            [WidgetId.Boost]       = new(454,  8),
            [WidgetId.LapTiming]   = new(558,  8),
            [WidgetId.GForce]      = new(722,  8),
            [WidgetId.PowerTorque] = new(826,  8),
            [WidgetId.MiniMap]     = new(936,  8),
            [WidgetId.Chart]       = new(936,  8, Visible: false),
        };

    // CornerPanel: clean stacked cluster near top-left, ~10px gaps, all on-screen.
    // Row 1 (Y=8):   Gear | Speed  (side by side)
    // Row 2 (Y=84):  RpmShift      (spans the full cluster width ~168px)
    // Row 3 (Y=148): PedalsSteer | Boost (side by side)
    // Row 4 (Y=246): LapTiming
    // Row 5 (Y=320): GForce | PowerTorque
    // MiniMap (~240x240) below the cluster. Chart (~320x180) below MiniMap, hidden.
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> CornerPanel =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.Gear]        = new(  8,   8),
            [WidgetId.Speed]       = new( 84,   8),
            [WidgetId.RpmShift]    = new(  8,  84),
            [WidgetId.PedalsSteer] = new(  8, 150),
            [WidgetId.Boost]       = new( 96, 150),
            [WidgetId.LapTiming]   = new(  8, 248),
            [WidgetId.GForce]      = new(  8, 330),
            [WidgetId.PowerTorque] = new(112, 330),
            [WidgetId.MiniMap]     = new(  8, 444),
            [WidgetId.Chart]       = new(  8, 700, Visible: false),
        };

    // CenterDash: key widgets centered at ~960px on a 1920-wide screen.
    // PedalsSteer left of center, RpmShift above Gear/Speed, LapTiming right of center.
    // Non-essential widgets hidden. MiniMap visible, placed top-right corner.
    // Chart placed below the main cluster, hidden by default.
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> CenterDash =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.PedalsSteer] = new(690, 816),
            [WidgetId.RpmShift]    = new(786, 768),
            [WidgetId.Gear]        = new(786, 840),
            [WidgetId.Speed]       = new(862, 840),
            [WidgetId.LapTiming]   = new(970, 816),
            [WidgetId.Boost]       = new(690, 900, Visible: false),
            [WidgetId.GForce]      = new(690, 984, Visible: false),
            [WidgetId.PowerTorque] = new(790, 984, Visible: false),
            [WidgetId.MiniMap]     = new(1680,  8),
            [WidgetId.Chart]       = new(640, 900, Visible: false),
        };
}
