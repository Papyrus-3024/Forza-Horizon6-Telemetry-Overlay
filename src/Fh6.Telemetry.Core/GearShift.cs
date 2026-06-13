namespace Fh6.Telemetry.Core;

public enum ShiftDirection
{
    None,
    Up,
    Down,
}

/// <summary>
/// FH6 Data Out reports only the current gear, not shift events. These are derived by
/// comparing the gear between consecutive frames.
/// </summary>
public static class GearShifts
{
    /// <summary>Direction of a gear change between two consecutive frames.</summary>
    public static ShiftDirection Detect(int? previousGear, int currentGear)
    {
        if (previousGear is not int previous || previous == currentGear)
            return ShiftDirection.None;

        return currentGear > previous ? ShiftDirection.Up : ShiftDirection.Down;
    }
}
