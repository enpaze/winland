namespace Winland.Application.Tests;

[TestClass]
public class FloatingLayerTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    private static WindowManager Desk()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, title:float*\n"));
        core.Handle(new MonitorsChanged([new Monitor("DISPLAY1", "", 0, 0, 1920, 1080, 1, true, Area)]));
        return core;
    }

    private static void Open(WindowManager core, long window, string title) =>
        core.Handle(new WindowAppeared(new WindowId(window), new Rectangle(100, 100, 400, 300))
        {
            Traits = WindowTraits.Normal with { Class = "app", Title = title },
        });

    [TestMethod]
    public void FocusingATileKeepsTheFloatingClientsAboveItInTheirOrder()
    {
        var core = Desk();
        Open(core, 1, "tile");
        Open(core, 2, "float a");
        Open(core, 3, "float b");

        var focused = core.Handle(new WindowFocused(new WindowId(1)));

        Assert.AreSequenceEqual([new ZOrderRequest(new WindowId(2), true), new ZOrderRequest(new WindowId(3), true)], focused.ZOrder);
    }

    [TestMethod]
    public void AFocusedFloatingClientComesToTheTopOfTheFloatingOnes()
    {
        var core = Desk();
        Open(core, 1, "tile");
        Open(core, 2, "float a");
        Open(core, 3, "float b");

        Assert.IsEmpty(core.Handle(new WindowFocused(new WindowId(2))).ZOrder);
        var focused = core.Handle(new WindowFocused(new WindowId(1)));

        Assert.AreSequenceEqual([new ZOrderRequest(new WindowId(3), true), new ZOrderRequest(new WindowId(2), true)], focused.ZOrder);
    }

    [TestMethod]
    public void NothingIsRaisedWhileTheActiveWindowStaysTheSame()
    {
        var core = Desk();
        Open(core, 2, "float a");
        Open(core, 1, "tile");
        core.Handle(new WindowFocused(new WindowId(1)));

        Assert.IsEmpty(core.Handle(new WindowFocused(new WindowId(1))).ZOrder);
        Assert.IsEmpty(core.Execute("resize").Effect.ZOrder);
    }

    [TestMethod]
    public void TheFloatingClientsAboveATileAreTheShownOnesBottomFirst()
    {
        var core = Desk();
        Open(core, 1, "tile");
        Open(core, 2, "float a");
        Open(core, 3, "float b");
        Open(core, 4, "float c");
        core.Execute("focus address:0x4");
        core.Execute("move 2 stay");

        Assert.AreSequenceEqual([new WindowId(2), new WindowId(3)], core.FloatingAbove(new WindowId(1)));
        Assert.IsEmpty(core.FloatingAbove(new WindowId(2)));
        Assert.IsEmpty(core.FloatingAbove(new WindowId(99)));
    }

    [TestMethod]
    public void AFloatingClientOnAHiddenWorkspaceIsLeftAlone()
    {
        var core = Desk();
        Open(core, 2, "float a");
        core.Execute("focus address:0x2");
        core.Execute("move 2 stay");
        Open(core, 1, "tile");
        core.Handle(new WindowFocused(new WindowId(2)));

        Assert.IsEmpty(core.Handle(new WindowFocused(new WindowId(1))).ZOrder);
    }
}
