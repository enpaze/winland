namespace Winland.Application.Tests;

[TestClass]
public class LayoutRegressionTests
{
    private static readonly Rectangle Area = new(0, 0, 1000, 800);

    [TestMethod]
    [DataRow("dwindle")]
    [DataRow("scrolling")]
    public void FloatingResizeAndPlacementKeepTheClientsLimits(string name)
    {
        var core = Configured($"general:layout = {name}");
        core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Dialog = true, MinSize = new Size(200, 100), MaxSize = new Size(400, 300) },
            Bounds = new Rectangle(100, 100, 300, 200),
        });

        var resized = core.Handle(new Dispatch("resize", "1000 1000"));
        Assert.AreEqual(new Size(400, 300), SizeOf(BoundsOf(resized, 1)));

        var placed = Place(core, 1, new Rectangle(100, 100, 50, 50));
        Assert.AreEqual(new Size(200, 100), SizeOf(Client(placed, 1).Bounds));
    }

    private static Size SizeOf(Rectangle box) => new(box.Width, box.Height);

    [TestMethod]
    public void AScrollingColumnCannotCoverTheNeighbouringMonitorsClients()
    {
        var core = Configured("general:layout = scrolling");
        core.Handle(new MonitorsChanged([Display("left", 0, 1000, 800), Display("right", 1000, 1000, 800)]));
        for (var id = 1; id <= 3; id++)
            core.Handle(new WindowAppeared(new WindowId(id), Area));

        var moved = core.Handle(new Dispatch("layoutmsg", "move +100"));

        Assert.DoesNotContain(item => item.Bounds.X + item.Bounds.Width > 1000, moved.Placements);
        Assert.Contains(new WindowId(3), moved.Hide);
    }

    [TestMethod]
    public void ScrollingFullscreenKeepsTheWholeMonitorAfterItsWorkAreaChanges()
    {
        var core = Configured("general:layout = scrolling");
        var screen = Display("screen", "screen", 0, 0, 1000, 800, workArea: new Rectangle(0, 0, 1000, 760));
        core.Handle(new MonitorsChanged([screen]));
        core.Handle(new WindowAppeared(new WindowId(1), screen.WorkArea));
        core.Handle(new Dispatch("fullscreen"));

        var changed = core.Handle(new MonitorsChanged([screen with { WorkArea = new Rectangle(0, 0, 1000, 740) }]));

        Assert.AreEqual(Area, BoundsOf(changed, 1));
    }

    [TestMethod]
    public void ExpandingTheFirstColumnKeepsThatColumnInView()
    {
        var layout = Tape();
        layout.Add(new WindowId(4), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        layout.NoteFocus(new WindowId(1));
        layout.LayoutMessage(new WindowId(1), "colresize 0.75");

        var expanded = layout.LayoutMessage(new WindowId(1), "fit expand");

        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Assert.ContainsSingle(item => item.Client == new ClientId(1), expanded).Bounds);
    }

    [TestMethod]
    public void GrowingAColumnPastTheViewportDoesNotShrinkIt()
    {
        var layout = Tape();
        layout.LayoutMessage(new WindowId(3), "move +100");
        layout.TryBounds(new WindowId(3), out var before);

        layout.ResizeActive(new WindowId(3), "100 0");
        layout.TryBounds(new WindowId(3), out var after);

        Assert.AreEqual(before.Width, after.Width);
    }

    private static ScrollingLayout Tape()
    {
        var layout = new ScrollingLayout();
        for (var id = 1; id <= 3; id++)
            layout.Add(new WindowId(id), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        return layout;
    }

    [TestMethod]
    public void DwindleAdoptionWithoutAMonitorKeepsTheFullMonitorForSizePrediction()
    {
        var layout = new DwindleLayout();
        var work = new Rectangle(0, 0, 1000, 760);
        layout.Add(new WindowId(1), work, null, true, new Rectangle(100, 100, 300, 200), WorkspaceId.Initial, monitor: Area);

        layout.Add(new WindowId(2), work, null, false, default, WorkspaceId.Initial, maxSize: new Size(1000, 770));

        Assert.IsTrue(Assert.ContainsSingle(client => client.Window == new WindowId(2), layout.Clients).Floating);
    }

    [TestMethod]
    [DataRow("dwindle")]
    [DataRow("scrolling")]
    public void FloatingPointerResizeKeepsTheClientsLimits(string name)
    {
        var core = Configured($"general:layout = {name}\n");
        core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Dialog = true, MinSize = new Size(200, 100), MaxSize = new Size(400, 300) },
            Bounds = new Rectangle(0, 0, 300, 200),
        });
        core.Handle(new PointerGesture("resize", new Point(640, 490), PointerDragPhase.Begin, "mouse:right"));

        var grown = core.Handle(new PointerGesture("resize", new Point(900, 700), PointerDragPhase.Move, "mouse:right"));

        Assert.AreEqual(new Size(400, 300), SizeOf(Client(grown, 1).Bounds));
        var shrunk = core.Handle(new PointerGesture("resize", new Point(400, 350), PointerDragPhase.Move, "mouse:right"));
        Assert.AreEqual(new Size(200, 100), SizeOf(Client(shrunk, 1).Bounds));
    }
}
