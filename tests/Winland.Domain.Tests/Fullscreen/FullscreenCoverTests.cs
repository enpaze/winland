namespace Winland.Domain.Tests;

[TestClass]
public class FullscreenCoverTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void AFloatingWindowIsLoweredUnderAFullscreenTileAndRaisedWhenItEnds()
    {
        var cover = new FullscreenCover(_ => false);
        var desk = Desk();
        Tile(desk, 1);
        Float(desk, 2);
        cover.Sync([desk], [Window(2)], _ => true);

        desk.Layout.SetFullscreen(Window(1), FullscreenMode.Fullscreen);
        var lowered = cover.Sync([desk], [Window(2)], _ => true);

        Assert.AreSequenceEqual([(Window(2), false)], lowered);
        Assert.IsTrue(cover.IsBelow(Window(2)));
        Assert.IsFalse(cover.AllowedOver(desk, desk.ClientOf(Window(2))!));

        desk.Layout.SetFullscreen(Window(1), FullscreenMode.None);
        var raised = cover.Sync([desk], [Window(2)], _ => true);

        Assert.AreSequenceEqual([(Window(2), true)], raised);
        Assert.IsFalse(cover.IsBelow(Window(2)));
    }

    [TestMethod]
    public void APinnedWindowStaysOverAFullscreenTile()
    {
        var cover = new FullscreenCover(window => window == Window(2));
        var desk = Desk();
        Tile(desk, 1);
        Float(desk, 2);
        desk.Layout.SetFullscreen(Window(1), FullscreenMode.Fullscreen);

        var requests = cover.Sync([desk], [Window(2)], _ => true);

        Assert.IsEmpty(requests);
        Assert.IsTrue(cover.AllowedOver(desk, desk.ClientOf(Window(2))!));
    }

    [TestMethod]
    public void TheFlagLetsAWindowOverAndOnlyARestackableWindowIsMoved()
    {
        var cover = new FullscreenCover(_ => false);
        var desk = Desk();
        Tile(desk, 1);
        Float(desk, 2);
        Float(desk, 3);
        desk.Layout.SetFullscreen(Window(1), FullscreenMode.Fullscreen);
        cover.Sync([desk], [], _ => true);
        cover.SetFlag(Window(2), true);

        var requests = cover.Sync([desk], [Window(2), Window(3)], window => window != Window(3));

        Assert.IsTrue(cover.Flag(Window(2)));
        Assert.IsFalse(cover.Flag(Window(3)));
        Assert.DoesNotContain(request => request.Window == Window(3), requests);
    }

    [TestMethod]
    public void TheCoveringClientIsAFloatingFullscreenOneFirst()
    {
        var desk = Desk();
        Tile(desk, 1);
        Float(desk, 2);
        desk.Layout.SetFullscreen(Window(1), FullscreenMode.Fullscreen);
        desk.Layout.SetFullscreen(Window(2), FullscreenMode.Fullscreen);

        Assert.AreEqual(Window(2), FullscreenCover.Covering(desk)?.Window);
    }

    [TestMethod]
    public void AClosedWindowIsForgotten()
    {
        var cover = new FullscreenCover(_ => false);
        cover.SetFlag(Window(1), false);

        cover.Forget(Window(1));

        Assert.IsTrue(cover.Flag(Window(1)));
    }

    [TestMethod]
    public void AClosedWindowIsNoLongerBelow()
    {
        var cover = new FullscreenCover(_ => false);
        var desk = Desk();
        Tile(desk, 1);
        Float(desk, 2);
        cover.Sync([desk], [], _ => true);
        desk.Layout.SetFullscreen(Window(1), FullscreenMode.Fullscreen);
        cover.Sync([desk], [], _ => true);
        Assert.IsTrue(cover.IsBelow(Window(2)));

        cover.Forget(Window(2));

        Assert.IsFalse(cover.IsBelow(Window(2)));
    }

    [TestMethod]
    public void AFloatingFullscreenThereAtFirstSightKeepsEveryOtherWindowUnder()
    {
        var cover = new FullscreenCover(_ => false);
        var desk = Desk();
        Float(desk, 1);
        Tile(desk, 2);
        Float(desk, 3);
        desk.Layout.SetFullscreen(Window(1), FullscreenMode.Fullscreen);

        var lowered = cover.Sync([desk], [], window => window == Window(3));

        Assert.IsFalse(cover.Flag(Window(2)));
        Assert.IsFalse(cover.Flag(Window(3)));
        Assert.IsTrue(cover.AllowedOver(desk, desk.ClientOf(Window(1))!));
        Assert.AreSequenceEqual([(Window(3), false)], lowered);
    }

    [TestMethod]
    public void ScrollingHidesTheFloatsOpenWhenATileWentFullscreenAndLetsLaterOnesOver()
    {
        var cover = new FullscreenCover(_ => false);
        var layout = new ScrollingLayout();
        layout.Fit(Area, Area);
        var desk = new LiveWorkspace(WorkspaceId.Initial, MonitorId.Primary, layout);
        Tile(desk, 1);
        Float(desk, 2);
        cover.Sync([desk], [], _ => true);

        layout.SetFullscreen(Window(1), FullscreenMode.Fullscreen, layoutAware: true);
        cover.Sync([desk], [], _ => true);
        Assert.IsFalse(cover.Flag(Window(2)));

        Float(desk, 3);
        cover.Sync([desk], [], _ => true);
        Assert.IsTrue(cover.Flag(Window(3)));
        Assert.IsFalse(cover.Flag(Window(2)));

        layout.SetFullscreen(Window(1), FullscreenMode.None, layoutAware: true);
        cover.Sync([desk], [], _ => true);
        Assert.IsTrue(cover.Flag(Window(2)));
        Assert.IsTrue(cover.Flag(Window(3)));
    }

    private static LiveWorkspace Desk()
    {
        var layout = new DwindleLayout();
        layout.Fit(Area, Area);
        return new LiveWorkspace(WorkspaceId.Initial, MonitorId.Primary, layout);
    }

    private static void Tile(LiveWorkspace desk, long window) =>
        desk.Layout.Add(Window(window), Area, null, false, new Rectangle(0, 0, 400, 300), WorkspaceId.Initial, "app", "title", monitor: Area);

    private static void Float(LiveWorkspace desk, long window) =>
        desk.Layout.Add(Window(window), Area, null, true, new Rectangle(100, 100, 400, 300), WorkspaceId.Initial, "app", "title", monitor: Area);

    private static WindowId Window(long id) => new(id);
}
