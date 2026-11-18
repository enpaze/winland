namespace Winland.Application.Tests;

[TestClass]
public class OffWindowAxisTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [TestMethod]
    [DataRow(0, 0, 0, 0)]
    [DataRow(100, 50, 100, 50)]
    [DataRow(-30, 50, 0, 50)]
    [DataRow(130, 50, 109, 50)]
    [DataRow(50, -5, 50, 0)]
    [DataRow(50, 700, 50, 59)]
    [DataRow(500, 700, 109, 59)]
    public void ThePointIsClampedOnePixelInFromTheFarEdges(int x, int y, int clampedX, int clampedY) =>
        Assert.AreEqual(new Point(clampedX, clampedY), BorderPointer.Clamp(new Rectangle(0, 0, 110, 60), new Point(x, y)));

    [TestMethod]
    public void ABoxWithNoSizeClampsToItsCorner() =>
        Assert.AreEqual(new Point(5, 6), BorderPointer.Clamp(new Rectangle(5, 6, 0, 0), new Point(50, 60)));

    [TestMethod]
    public void AFloatingWindowsRingIsItsOffWindowTargetAndTheNearestPointInsideIsClamped()
    {
        var core = CoreWith("general:border_size = 4\n");
        Float(core, 1);

        var target = core.OffWindowTarget(new Point(1282, 500));

        Assert.AreEqual(new OffWindowScroll(new WindowId(1), new Point(1279, 500)), target);
        Assert.AreEqual(new OffWindowScroll(new WindowId(1), new Point(1279, 739)), core.OffWindowTarget(new Point(1283, 743)));
    }

    [TestMethod]
    public void InsideTheWindowBeyondTheRingOrWithNoBorderThereIsNoTarget()
    {
        var core = CoreWith("general:border_size = 4\n");
        Float(core, 1);

        Assert.IsNull(core.OffWindowTarget(new Point(900, 500)));
        Assert.IsNull(core.OffWindowTarget(new Point(1290, 500)));
        Assert.IsNull(core.OffWindowTarget(new Point(10, 10)));

        var bare = CoreWith("general:border_size = 0\n");
        Float(bare, 1);
        Assert.IsNull(bare.OffWindowTarget(new Point(1280, 500)));
    }

    private static WindowManager CoreWith(string config)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\n" + config)));
        return core;
    }

    private static void Float(WindowManager core, long window)
    {
        core.Handle(new WindowAppeared(new WindowId(window), Wide));
        core.Handle(new Dispatch("float"));
    }
}
