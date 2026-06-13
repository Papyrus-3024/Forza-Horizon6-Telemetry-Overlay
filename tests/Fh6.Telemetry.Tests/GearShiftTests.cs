using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class GearShiftTests
{
    [Fact]
    public void No_previous_gear_is_no_shift()
    {
        Assert.Equal(ShiftDirection.None, GearShifts.Detect(null, 3));
    }

    [Fact]
    public void Same_gear_is_no_shift()
    {
        Assert.Equal(ShiftDirection.None, GearShifts.Detect(3, 3));
    }

    [Fact]
    public void Higher_gear_is_upshift()
    {
        Assert.Equal(ShiftDirection.Up, GearShifts.Detect(2, 3));
    }

    [Fact]
    public void Lower_gear_is_downshift()
    {
        Assert.Equal(ShiftDirection.Down, GearShifts.Detect(4, 3));
    }
}
