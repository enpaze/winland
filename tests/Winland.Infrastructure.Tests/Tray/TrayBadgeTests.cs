namespace Winland.Infrastructure.Tests;

[TestClass]
public class TrayBadgeTests
{
    private const int Size = 16;

    [TestMethod]
    public void TheLabelIsCenteredByItsInk()
    {
        var pixels = new uint[Size * Size];
        for (var y = 2; y < 8; y++)
            pixels[(y * Size) + 3] = 0xFFFFFF;

        TrayBadge.Compose(pixels, Size);

        Assert.AreEqual(0xFFFFFFFFu, pixels[(5 * Size) + 8]);
        Assert.AreEqual(0xFFFFFFFFu, pixels[(10 * Size) + 8]);
        Assert.AreNotEqual(0xFFFFFFFFu, pixels[(4 * Size) + 8]);
        Assert.AreNotEqual(0xFFFFFFFFu, pixels[(11 * Size) + 8]);
        Assert.AreNotEqual(0xFFFFFFFFu, pixels[(7 * Size) + 3]);
    }

    [TestMethod]
    public void TheDiscIsOpaqueInsideClearOutsideAndPartlyCoveredOnItsEdge()
    {
        var pixels = new uint[Size * Size];

        TrayBadge.Compose(pixels, Size);

        Assert.AreEqual(0xFF2F6FEDu, pixels[(8 * Size) + 8]);
        Assert.IsTrue(TrayBadge.Clear(pixels[0]));
        Assert.Contains(pixel => pixel >> 24 is > 0 and < 255, pixels);
    }
}
