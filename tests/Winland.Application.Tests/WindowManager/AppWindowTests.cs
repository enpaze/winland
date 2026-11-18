namespace Winland.Application.Tests;

[TestClass]
public class AppWindowTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    [DataRow("dwindle")]
    [DataRow("scrolling")]
    public void AWindowAdoptedUnderADragKeepsItsFrameUntilTheButtonIsReleased(string layout)
    {
        var core = new WindowManager(ConfigParser.Parse($"general:layout = {layout}"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var bounds = new Rectangle(200, 250, 500, 350);
        var opened = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Opened = true,
            Dragging = true,
            Pointer = new Point(250, 260),
            Bounds = bounds,
        });

        var dragged = Assert.ContainsSingle(client => client.Window == new WindowId(2), opened.State.Clients);
        Assert.IsTrue(dragged.Floating);
        Assert.AreEqual(bounds, dragged.Bounds);
        Assert.DoesNotContain(placement => placement.Client == dragged.Id, opened.Placements);
        Assert.DoesNotContain(motion => motion.Window == dragged.Window, opened.Motions);

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(1500, 500) });
        Assert.IsFalse(Assert.ContainsSingle(client => client.Window == dragged.Window, dropped.State.Clients).Floating);
    }

    [TestMethod]
    public void AGridSnappingTileIsPutBackThreeTimesThenCenteredWithoutResizing()
    {
        long now = 0;
        var core = new WindowManager { Clock = () => now };
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var smaller = new Rectangle(10, 20, 1900, 1000);
        for (var i = 0; i < 3; i++)
            Assert.AreEqual(Area, Assert.ContainsSingle(core.Handle(new WindowDrifted(new WindowId(1), smaller)).Placements).Bounds);

        var centered = core.Handle(new WindowDrifted(new WindowId(1), smaller));
        var box = Assert.ContainsSingle(centered.Placements).Bounds;
        Assert.AreEqual(new Rectangle(10, 40, 1900, 1000), box);
        Assert.IsEmpty(core.Handle(new WindowDrifted(new WindowId(1), box)).Placements);
        now = 1000;
        Assert.AreEqual(Area, Assert.ContainsSingle(core.Handle(new WindowDrifted(new WindowId(1), box)).Placements).Bounds);
    }

    [TestMethod]
    public void AWindowWithoutASizingFrameFloatsUnlessARuleTilesIt()
    {
        var core = new WindowManager();
        var appeared = new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Resizable = false },
            Bounds = new Rectangle(10, 20, 400, 300),
        };
        Assert.IsTrue(Assert.ContainsSingle(core.Handle(appeared).State.Clients).Floating);

        var ruled = new WindowManager(ConfigParser.Parse("windowrule = tile, class:*"));
        Assert.IsFalse(Assert.ContainsSingle(ruled.Handle(appeared).State.Clients).Floating);
    }

    [TestMethod]
    public void AnOwnedWindowOpensSilentlyOnItsHiddenOwnersWorkspaceAndIsUrgent()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var opened = core.Handle(new WindowAppeared(new WindowId(3), Area)
        {
            Owner = new WindowId(1),
            Opened = true,
            Traits = WindowTraits.Normal with { Dialog = true },
        });

        Assert.AreEqual(new WorkspaceId("2"), opened.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("1"), Assert.ContainsSingle(client => client.Window == new WindowId(3), opened.State.Clients).Workspace);
        Assert.Contains(new WindowId(3), opened.Hide);
        Assert.Contains(item => item is ClientUrgent, opened.Events);
        Assert.AreEqual(new WindowId(2), core.Handle(new Dispatch("close")).Close);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ATopmostWindowKeepsItsBoundsAndPinAndOnlyTakesFocusItAlreadyHas(bool foreground)
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var bounds = new Rectangle(1400, 700, 400, 250);

        var opened = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { Topmost = true, Dialog = true },
            Bounds = bounds,
            Foreground = foreground,
        });

        var client = Assert.ContainsSingle(client => client.Window == new WindowId(2), opened.State.Clients);
        Assert.IsTrue(client.Floating);
        Assert.IsTrue(client.Pinned);
        Assert.AreEqual(bounds, client.Bounds);
        Assert.AreEqual(new WindowId(foreground ? 2 : 1), core.Handle(new Dispatch("close")).Close);
        var switched = core.Handle(new Dispatch("workspace", "2"));
        Assert.DoesNotContain(new WindowId(2), switched.Hide);
        var focused = core.Handle(new WindowFocused(new WindowId(2), FocusCause.User));
        Assert.AreNotEqual(new WindowId(1), focused.Focus);
    }
}



