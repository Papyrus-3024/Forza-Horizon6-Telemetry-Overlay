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

    // BottomStrip: horizontal row, all at Y=6, left-to-right in the StackPanel order.
    // Approximate widget widths (including Margin=3 each side):
    //   Gear ~66, Speed ~66, RpmShift ~162, PedalsSteer ~76, Boost ~86, LapTiming ~148
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> BottomStrip =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.Gear]        = new(  6,  6),
            [WidgetId.Speed]       = new( 76,  6),
            [WidgetId.RpmShift]    = new(146,  6),
            [WidgetId.PedalsSteer] = new(314,  6),
            [WidgetId.Boost]       = new(396,  6),
            [WidgetId.LapTiming]   = new(488,  6),
            [WidgetId.GForce]      = new(640,  6),
            [WidgetId.PowerTorque] = new(736,  6),
        };

    // CornerPanel: compact stacked block near origin (top-left corner of screen).
    // Row 1 (Y=6):   Gear | Speed (horizontal)
    // Row 2 (Y=82):  RpmShift (full width)
    // Row 3 (Y=138): PedalsSteer | Boost (horizontal)
    // Row 4 (Y=236): LapTiming (full width)
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> CornerPanel =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.Gear]        = new(  6,   6),
            [WidgetId.Speed]       = new( 76,   6),
            [WidgetId.RpmShift]    = new(  6,  82),
            [WidgetId.PedalsSteer] = new(  6, 138),
            [WidgetId.Boost]       = new( 88, 138),
            [WidgetId.LapTiming]   = new(  6, 236),
            [WidgetId.GForce]      = new(  6, 396),
            [WidgetId.PowerTorque] = new(102, 396),
        };

    // CenterDash: spread across the bottom-center of the screen.
    // PedalsSteer on far left, RpmShift+Gear+Speed in center column, LapTiming on far right.
    // Gear and Speed are in a horizontal sub-row beneath RpmShift.
    // Using a 1920-wide primary screen: center at ~960.
    // Center block width ~300, so starts at ~810.
    private static readonly IReadOnlyDictionary<WidgetId, WidgetSeed> CenterDash =
        new Dictionary<WidgetId, WidgetSeed>
        {
            [WidgetId.PedalsSteer] = new(700, 820),
            [WidgetId.RpmShift]    = new(822, 774),
            [WidgetId.Gear]        = new(822, 832),
            [WidgetId.Speed]       = new(892, 832),
            [WidgetId.LapTiming]   = new(996, 820),
            [WidgetId.Boost]       = new(700, 900, Visible: false),
            [WidgetId.GForce]      = new(700, 980, Visible: false),
            [WidgetId.PowerTorque] = new(800, 980, Visible: false),
        };
}
