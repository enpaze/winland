namespace Winland.Application.Tests;

[TestClass]
public class BorderRingTests
{
    private static readonly BorderColor Cyan = new(0xFF, 0x33, 0xCC, 0xFF);
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void TheRingCoversOnlyTheBandAroundTheWindow()
    {
        var ring = new BorderRing(Cyan, 4, 0, 255);

        Assert.AreEqual(1, BorderPainter.Coverage(0, 50, 100, 100, ring));
        Assert.AreEqual(1, BorderPainter.Coverage(3, 50, 100, 100, ring));
        Assert.AreEqual(0, BorderPainter.Coverage(4, 50, 100, 100, ring));
        Assert.AreEqual(0, BorderPainter.Coverage(50, 50, 100, 100, ring));
        Assert.AreEqual(1, BorderPainter.Coverage(99, 99, 100, 100, ring));
    }

    [TestMethod]
    public void RoundCornersLeaveTheOuterCornerClear()
    {
        var ring = new BorderRing(Cyan, 4, 10, 255);

        Assert.AreEqual(0, BorderPainter.Coverage(0, 0, 100, 100, ring));
        Assert.AreEqual(1, BorderPainter.Coverage(50, 1, 100, 100, ring));
        Assert.AreEqual(14, ring.OuterRadius, 5E-07);
    }

    [TestMethod]
    public void PaintingWritesPremultipliedPixelsOnTheRingOnly()
    {
        var ring = new BorderRing(new BorderColor(0x80, 0xFF, 0, 0), 2, 0, 255);
        var pixels = new uint[10 * 10];

        BorderPainter.Paint(pixels, ring, 10, 10, new Rectangle(0, 0, 10, 10));

        Assert.AreEqual(0x80800000u, pixels[0]);
        Assert.AreEqual(0u, pixels[(5 * 10) + 5]);
    }

    [TestMethod]
    public void EachClientGetsARingFromItsBorderSizeAndColors()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("general:border_size = 3\ngeneral:col.active_border = rgba(33ccffff)\n")));
        core.Handle(new MonitorsChanged([new Monitor("DISPLAY1", "", 0, 0, 1920, 1080, 1, true, Area)]));

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area));

        var ring = Assert.ContainsSingle(outcome.Decorations).Ring;
        Assert.IsNotNull(ring);
        Assert.AreEqual(3, ring.Thickness);
        Assert.AreEqual(Cyan, ring.Color);
    }

    [TestMethod]
    public void NoRingWithoutABorderOrInFullscreen()
    {
        var none = new WindowManager(ConfigParser.Parse("general:border_size = 0\n"));
        none.Handle(new MonitorsChanged([new Monitor("DISPLAY1", "", 0, 0, 1920, 1080, 1, true, Area)]));
        Assert.IsNull(Assert.ContainsSingle(none.Handle(new WindowAppeared(new WindowId(1), Area)).Decorations).Ring);

        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("general:border_size = 2\n")));
        core.Handle(new MonitorsChanged([new Monitor("DISPLAY1", "", 0, 0, 1920, 1080, 1, true, Area)]));
        Assert.IsNotNull(Assert.ContainsSingle(core.Handle(new WindowAppeared(new WindowId(1), Area)).Decorations).Ring);

        Assert.IsNull(Assert.ContainsSingle(core.Execute("fullscreen").Effect.Decorations).Ring);
    }

}
