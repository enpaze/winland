namespace Winland.Domain.Tests;

[TestClass]
public class LayoutEngineTests
{
    private static readonly Rectangle Area = new(0, 0, 1000, 800);

    [TestMethod]
    [DataRow("dwindle"), DataRow("scrolling")]
    public void ClosingALiftedTileLeavesNoHoleAndNoGhost(string name)
    {
        var layout = Open(name);
        Tiled(layout, 1);
        Tiled(layout, 2);
        Tiled(layout, 3);

        layout.Drag(Window(1), new PointerDrag(false, PointerDragPhase.Begin, new Point(100, 100)));
        layout.Drag(Window(1), new PointerDrag(false, PointerDragPhase.Move, new Point(160, 140)));
        layout.Remove(Window(1));
        var removed = layout.Placed();
        var dropped = layout.Drag(Window(1), new PointerDrag(false, PointerDragPhase.End, new Point(160, 140)));

        Assert.DoesNotContain(client => client.Window == Window(1), layout.Clients);
        Assert.DoesNotContain(placement => placement.Client == new ClientId(1), dropped);
        Assert.AreSequenceEqual(removed, layout.Placed());
        Assert.AreEqual(2, layout.Clients.Count);
    }

    [TestMethod]
    [DataRow("dwindle"), DataRow("scrolling")]
    public void ClosingAGrabbedTileBeforeItLiftsEndsTheDrag(string name)
    {
        var layout = Open(name);
        Tiled(layout, 1);
        Tiled(layout, 2);

        layout.Drag(Window(1), new PointerDrag(false, PointerDragPhase.Begin, new Point(100, 100)));
        var removed = layout.Remove(Window(1));
        var moved = layout.Drag(Window(1), new PointerDrag(false, PointerDragPhase.Move, new Point(160, 140)));
        var dropped = layout.Drag(Window(1), new PointerDrag(false, PointerDragPhase.End, new Point(160, 140)));

        Assert.IsEmpty(moved);
        Assert.DoesNotContain(placement => placement.Client == new ClientId(1), dropped);
        Assert.AreEqual(Area, Bounds(layout.Placed(), 2));
        Assert.AreEqual(Area, Bounds(removed, 2));
        Assert.ContainsSingle(layout.Clients);
    }

    [TestMethod]
    [DataRow("dwindle"), DataRow("scrolling")]
    public void SubstituteHandsNaturalSizeAndLimitsToTheNewWindow(string name)
    {
        var layout = Open(name);
        Tiled(layout, 1);
        layout.Add(Window(2), Area, null, false, new Rectangle(0, 0, 400, 300), WorkspaceId.Initial, "app", "title", monitor: Area,
            minSize: new Size(100, 50), maxSize: new Size(2000, 2000));

        var incoming = new Taken(false, false, new Rectangle(0, 0, 200, 100), "new", "new", MinSize: new Size(10, 20), MaxSize: new Size(1500, 1600));
        Assert.IsTrue(layout.Substitute(Window(2), Window(3), incoming, out _));

        Assert.IsTrue(layout.TryTake(Window(3), out var taken));
        Assert.AreEqual(incoming.Bounds, taken.Bounds);
        Assert.AreEqual(incoming.MinSize, taken.MinSize);
        Assert.AreEqual(incoming.MaxSize, taken.MaxSize);

        Assert.IsFalse(layout.TryTake(Window(2), out _));
        Tiled(layout, 2);
        Assert.IsTrue(layout.TryTake(Window(2), out var fresh));
        Assert.AreEqual(new Rectangle(0, 0, 400, 300), fresh.Bounds);
        Assert.IsNull(fresh.MinSize);
        Assert.IsNull(fresh.MaxSize);
    }

    [TestMethod]
    [DataRow("dwindle"), DataRow("scrolling")]
    public void SubstituteHandsAFloatingSlotToTheNewWindow(string name)
    {
        var layout = Open(name);
        Tiled(layout, 1);
        var box = new Rectangle(100, 100, 300, 200);
        layout.Add(Window(2), Area, null, true, box, WorkspaceId.Initial, "app", "title", monitor: Area);

        Assert.IsTrue(layout.Substitute(Window(2), Window(3), new Taken(false, false, new Rectangle(0, 0, 50, 50), "new", "new"), out _));

        Assert.AreEqual(box, Bounds(layout.Visible(), 3));
        Assert.DoesNotContain(placement => placement.Client == new ClientId(2), layout.Visible());
        Assert.IsTrue(layout.TryTake(Window(3), out var taken));
        Assert.IsTrue(taken.Floating);
        Assert.AreEqual(box, taken.Bounds);
        Assert.IsFalse(layout.TryTake(Window(2), out _));
    }

    [TestMethod]
    public void ScrollingMovesTheCameraAgainAfterTheResizedTileIsHandedOver()
    {
        var layout = Open("scrolling");
        Tiled(layout, 1);
        Tiled(layout, 2);

        layout.Drag(Window(2), new PointerDrag(true, PointerDragPhase.Begin, new Point(700, 400)));
        layout.Drag(Window(2), new PointerDrag(true, PointerDragPhase.Move, new Point(720, 400)));
        Assert.IsTrue(layout.Substitute(Window(2), Window(9), new Taken(false, false, default, "nine", "nine"), out _));
        layout.Remove(Window(1));

        var lone = Bounds(layout.Placed(), 9);
        Assert.AreEqual((Area.Width - lone.Width) / 2, lone.X);
    }

    [TestMethod]
    [DataRow("scrolling", "colresize nan")]
    [DataRow("scrolling", "colresize +nan")]
    public void ALayoutMessageWithANumberThatIsNotFiniteChangesNothing(string name, string message)
    {
        var layout = Open(name);
        Tiled(layout, 1);
        Tiled(layout, 2);
        var before = layout.Placed();

        layout.LayoutMessage(Window(2), message);

        Assert.AreSequenceEqual(before, layout.Placed());
    }

    [TestMethod]
    public void DwindleHonorsAMinimumInsideASplitWhoseParentDoesNotChange()
    {
        var layout = Open("dwindle");
        Tiled(layout, 1);
        Tiled(layout, 2);

        var placements = layout.Add(Window(3), Area, null, false, new Rectangle(0, 0, 400, 300), WorkspaceId.Initial, monitor: Area, minSize: new Size(100, 600));

        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Bounds(placements, 1));
        Assert.AreEqual(new Rectangle(500, 0, 500, 200), Bounds(placements, 2));
        Assert.AreEqual(new Rectangle(500, 200, 500, 600), Bounds(placements, 3));
    }

    [TestMethod]
    public void DwindleHonorsANestedMinimumThatArrivesLater()
    {
        var layout = Open("dwindle");
        Tiled(layout, 1);
        Tiled(layout, 2);
        Tiled(layout, 3);

        var placements = layout.UpdateLimits(Window(2), new Size(100, 700), null);

        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Bounds(placements, 1));
        Assert.AreEqual(new Rectangle(500, 0, 500, 700), Bounds(placements, 2));
        Assert.AreEqual(new Rectangle(500, 700, 500, 100), Bounds(placements, 3));
    }

    [TestMethod]
    [DataRow("0 50")]
    [DataRow("0 -50")]
    public void ScrollingResizesARowInAColumnOfElevenWithoutThrowing(string delta)
    {
        var layout = Open("scrolling");
        for (var window = 1; window <= 11; window++)
            Tiled(layout, window);
        for (var step = 0; step < 10; step++)
            layout.LayoutMessage(Window(1), "consume");
        var before = Bounds(layout.Placed(), 1);

        var placements = layout.ResizeActive(Window(1), delta);

        Assert.AreEqual(before.Height, Bounds(placements, 1).Height);
        Assert.AreEqual(11, layout.Placed().Count);
    }

    [TestMethod]
    [DataRow("dwindle"), DataRow("scrolling")]
    public void ExactResizeSizesTheWindowNotItsTile(string name)
    {
        var layout = Open(name, LayoutOptions.Unconfigured with { GapsIn = EdgeInsets.Uniform(5), BorderSize = 1 });
        Tiled(layout, 1);
        Tiled(layout, 2);

        var placements = layout.ResizeActive(Window(1), "exact 800 600");

        Assert.AreEqual(800, Bounds(placements, 1).Width);
    }

    [TestMethod]
    public void ScrollingKeyboardResizeKeepsTheCamera()
    {
        var layout = ThreeColumns();

        var placements = layout.ResizeActive(Window(1), "100 0");

        Assert.AreEqual(new Rectangle(-500, 0, 600, 800), Bounds(placements, 1));
        Assert.AreEqual(new Rectangle(100, 0, 500, 800), Bounds(placements, 2));
    }

    [TestMethod]
    public void ScrollingKeyboardResizeKeepsTheColumnInsideTheViewport()
    {
        var layout = ThreeColumns();

        var placements = layout.ResizeActive(Window(3), "200 0");

        Assert.AreEqual(new Rectangle(500, 0, 500, 800), Bounds(placements, 3));
    }

    [TestMethod]
    public void ScrollingLeftEdgeDragKeepsTheRightEdge()
    {
        var layout = ThreeColumns();

        layout.Drag(Window(3), new PointerDrag(true, PointerDragPhase.Begin, new Point(520, 100)));
        var placements = layout.Drag(Window(3), new PointerDrag(true, PointerDragPhase.Move, new Point(420, 100)));
        layout.Drag(Window(3), new PointerDrag(true, PointerDragPhase.End, new Point(420, 100)));

        Assert.AreEqual(new Rectangle(400, 0, 600, 800), Bounds(placements, 3));
        Assert.AreEqual(new Rectangle(400, 0, 600, 800), Bounds(layout.Placed(), 3));
    }

    [TestMethod]
    public void ScrollingKeyboardRowResizeTakesFromThePreviousRow()
    {
        var layout = OneColumnOfThree();
        var before = layout.Placed();

        var placements = layout.ResizeActive(Window(2), "0 90");

        Assert.AreEqual(Bounds(before, 1).Height - 90, Bounds(placements, 1).Height);
        Assert.AreEqual(Bounds(before, 2).Y - 90, Bounds(placements, 2).Y);
        Assert.AreEqual(Bounds(before, 3), Bounds(placements, 3));
    }

    [TestMethod]
    public void ScrollingMouseRowResizeMovesTheGrabbedEdge()
    {
        var layout = OneColumnOfThree();
        var before = layout.Placed();
        var middle = Bounds(before, 2);

        layout.Drag(Window(2), new PointerDrag(true, PointerDragPhase.Begin, new Point(middle.X + 10, middle.Y + 10)));
        var placements = layout.Drag(Window(2), new PointerDrag(true, PointerDragPhase.Move, new Point(middle.X + 10, middle.Y + 60)));

        Assert.AreEqual(middle.Y + 50, Bounds(placements, 2).Y);
        Assert.AreEqual(Bounds(before, 3), Bounds(placements, 3));
    }

    [TestMethod]
    [DataRow("dwindle", false)]
    [DataRow("scrolling", false)]
    [DataRow("dwindle", true)]
    [DataRow("scrolling", true)]
    public void AnAddedClientStartsWithItsClassAndTitleAsTheInitialOnes(string name, bool floating)
    {
        var layout = Open(name);

        layout.Add(Window(1), Area, null, floating, new Rectangle(0, 0, 400, 300), WorkspaceId.Initial, "app", "title", monitor: Area);

        var client = Assert.ContainsSingle(layout.Clients);
        Assert.AreEqual("app", client.InitialClass);
        Assert.AreEqual("title", client.InitialTitle);
    }

    [TestMethod]
    [DataRow("dwindle")]
    [DataRow("scrolling")]
    public void FloatingAgainRestoresTheLastFloatingSize(string name)
    {
        var layout = Open(name);
        Tiled(layout, 1);
        Tiled(layout, 2);

        var first = Bounds(layout.ToggleFloating(Window(1)), 1);
        layout.ResizeActive(Window(1), "exact 500 350");
        layout.ToggleFloating(Window(1));
        var again = Bounds(layout.ToggleFloating(Window(1)), 1);

        Assert.AreEqual((400, 300), (first.Width, first.Height));
        Assert.AreEqual((500, 350), (again.Width, again.Height));
    }

    [TestMethod]
    [DataRow("dwindle")]
    [DataRow("scrolling")]
    public void FloatingClientsKeepToTheirSizeLimits(string name)
    {
        var layout = Open(name);

        var opened = layout.Add(Window(1), Area, null, true, new Rectangle(10, 20, 400, 300), WorkspaceId.Initial, monitor: Area, minSize: new Size(500, 350));
        var limited = layout.UpdateLimits(Window(1), null, new Size(300, 200));

        Assert.AreEqual(new Rectangle(10, 20, 500, 350), Bounds(opened, 1));
        Assert.AreEqual(new Rectangle(10, 20, 300, 200), Bounds(limited, 1));
    }

    [TestMethod]
    [DataRow("dwindle", false)]
    [DataRow("scrolling", false)]
    [DataRow("dwindle", true)]
    [DataRow("scrolling", true)]
    public void AFloatingDragIgnoresAClientTiledMidDrag(string name, bool resize)
    {
        var layout = Open(name);
        Tiled(layout, 1);
        Tiled(layout, 2);
        var floated = Bounds(layout.ToggleFloating(Window(1)), 1);
        var inside = new Point(floated.X + 10, floated.Y + 10);
        layout.Drag(Window(1), new PointerDrag(resize, PointerDragPhase.Begin, inside));
        layout.ToggleFloating(Window(1));
        var tiled = layout.Placed();

        var moved = layout.Drag(Window(1), new PointerDrag(resize, PointerDragPhase.Move, new Point(inside.X + 40, inside.Y + 30)));
        layout.Drag(Window(1), new PointerDrag(resize, PointerDragPhase.End, new Point(inside.X + 40, inside.Y + 30)));

        Assert.IsEmpty(moved);
        Assert.AreSequenceEqual(tiled, layout.Placed());
        Assert.IsFalse(Assert.ContainsSingle(client => client.Window == Window(1), layout.Clients).Floating);
        Assert.IsTrue(layout.TryBounds(Window(1), out var bounds));
        Assert.AreEqual(Bounds(tiled, 1), bounds);
    }

    [TestMethod]
    public void ScrollingDropsAMinimizedTileThatCloses()
    {
        var layout = Open("scrolling");
        Tiled(layout, 1);
        Tiled(layout, 2);
        layout.Minimize(Window(1));

        layout.Remove(Window(1));

        Assert.DoesNotContain(client => client.Window == Window(1), layout.Clients);
        Assert.IsFalse(layout.TryTake(Window(1), out _));
    }

    [TestMethod]
    public void ANeighborTwoPixelsAwayDoesNotTouch()
    {
        var origin = new Rectangle(0, 0, 100, 100);

        Assert.IsNull(NeighborAt(origin, new Rectangle(102, 0, 100, 100)));
        Assert.AreEqual(Window(2), NeighborAt(origin, new Rectangle(101, 0, 100, 100)));
    }

    private static WindowId? NeighborAt(Rectangle origin, Rectangle other)
    {
        Client[] clients = [new(new ClientId(1), Window(1)), new(new ClientId(2), Window(2))];
        return DirectionalNeighbor.Find(clients, origin, Side.Right, Window(1), floating: false, (WindowId window, out Rectangle box) =>
        {
            box = window == Window(1) ? origin : other;
            return true;
        }, _ => 0);
    }

    private static Layout ThreeColumns()
    {
        var layout = Open("scrolling");
        Tiled(layout, 1);
        Tiled(layout, 2);
        var placements = Tiled(layout, 3);
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), Bounds(placements, 3));
        return layout;
    }

    private static Layout OneColumnOfThree()
    {
        var layout = Open("scrolling");
        Tiled(layout, 1);
        Tiled(layout, 2);
        Tiled(layout, 3);
        layout.LayoutMessage(Window(1), "consume");
        layout.LayoutMessage(Window(1), "consume");
        Assert.AreEqual(3, layout.Placed().Count);
        return layout;
    }

    private static List<(WindowId Window, Rectangle Bounds)> Stack(IReadOnlyList<Placement> placements)
    {
        var master = placements.Min(placement => placement.Bounds.X);
        return placements
            .Where(placement => placement.Bounds.X != master)
            .OrderBy(placement => placement.Bounds.Y)
            .Select(placement => (new WindowId(placement.Client.Value), placement.Bounds))
            .ToList();
    }

    [TestMethod]
    public void DwindleArrangesItsTilesOnTheRectanglesTheyHad()
    {
        var layout = Open("dwindle");
        Tiled(layout, 1);
        Tiled(layout, 2);
        Tiled(layout, 3);

        var arranged = layout.Arrange(new Dictionary<WindowId, Rectangle>
        {
            [Window(1)] = new(0, 0, 300, 800),
            [Window(2)] = new(300, 0, 700, 200),
            [Window(3)] = new(300, 200, 700, 600),
        });

        Assert.AreEqual(new Rectangle(0, 0, 300, 800), Bounds(arranged, 1));
        Assert.AreEqual(new Rectangle(300, 0, 700, 200), Bounds(arranged, 2));
        Assert.AreEqual(new Rectangle(300, 200, 700, 600), Bounds(arranged, 3));
    }

    [TestMethod]
    public void DwindleArrangedFromItsOwnPlacementsStaysAsItIs()
    {
        var layout = Open("dwindle", LayoutOptions.Unconfigured with { GapsIn = EdgeInsets.Uniform(5), GapsOut = EdgeInsets.Uniform(10) });
        for (var window = 1; window <= 5; window++)
            Tiled(layout, window);

        var before = layout.Placed();
        layout.Arrange(before.ToDictionary(placement => new WindowId(placement.Client.Value), placement => placement.Bounds));

        Assert.AreSequenceEqual(before.OrderBy(placement => placement.Client.Value), layout.Placed().OrderBy(placement => placement.Client.Value));
    }

    [TestMethod]
    public void DwindleKeepsItsTreeWhenATileHasNoRectangle()
    {
        var layout = Open("dwindle");
        Tiled(layout, 1);
        Tiled(layout, 2);
        var before = layout.Placed();

        var arranged = layout.Arrange(new Dictionary<WindowId, Rectangle> { [Window(1)] = new(0, 0, 300, 800) });

        Assert.IsEmpty(arranged);
        Assert.AreSequenceEqual(before, layout.Placed());
    }

    private static Layout Open(string name) => Open(name, LayoutOptions.Unconfigured);

    private static Layout Open(string name, LayoutOptions options)
    {
        Layout layout = name switch
        {
            "scrolling" => new ScrollingLayout(),
            _ => new DwindleLayout(),
        };
        layout.Apply(options);
        layout.Fit(Area, Area);
        return layout;
    }

    private static IReadOnlyList<Placement> Tiled(Layout layout, long window) =>
        layout.Add(Window(window), Area, null, false, new Rectangle(0, 0, 400, 300), WorkspaceId.Initial, "app", "title", monitor: Area);

    private static WindowId Window(long id) => new(id);

    private static (int Width, int Height) SizeOf(Rectangle bounds) => (bounds.Width, bounds.Height);

    private static Rectangle Bounds(IReadOnlyList<Placement> placements, long window) =>
        Assert.ContainsSingle(placement => placement.Client == new ClientId(window), placements).Bounds;
}
