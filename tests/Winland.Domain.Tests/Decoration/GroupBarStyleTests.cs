namespace Winland.Domain.Tests;

public class GroupBarStyleTests
{
    [Fact]
    public void TheCornersGrowWithTheMonitorScaleAndNotWithTheTextSize()
    {
        var style = GroupBarStyle.Documented with { Rounding = 4 };

        var scaled = style.ScaledBy(1.5, text: 2);

        Assert.Equal(6, scaled.Rounding);
    }
}
