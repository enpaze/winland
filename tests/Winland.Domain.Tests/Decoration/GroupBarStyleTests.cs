namespace Winland.Domain.Tests;

[TestClass]
public class GroupBarStyleTests
{
    [TestMethod]
    public void TheCornersGrowWithTheMonitorScaleAndNotWithTheTextSize()
    {
        var style = GroupBarStyle.Documented with { Rounding = 4 };

        var scaled = style.ScaledBy(1.5, text: 2);

        Assert.AreEqual(6, scaled.Rounding);
    }
}
