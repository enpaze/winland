namespace Winland.Domain.Tests;

[TestClass]
public class DisplayColorTests
{
    [TestMethod]
    public void TheReplyNamesTheDepthWindowsReports()
    {
        Assert.AreEqual("XRGB2101010", DisplayColors.Format(10));
        Assert.AreEqual("XRGB8888", DisplayColors.Format(8));
        Assert.AreEqual("", DisplayColors.Format(0));
    }
}
