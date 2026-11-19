namespace Winland.Application.Tests;

[TestClass]
public class GestureTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    private const string Horizontal = "gesture = 3, horizontal, workspace\n";

    [TestMethod]
    public void WorkspaceGestureParsesWithoutWarnings()
    {
        var document = ConfigParser.Parse("gesture = 3, horizontal, workspace\ngesture = 4, vertical, workspace\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            [new TrackpadGesture(3, GestureAxis.Horizontal), new TrackpadGesture(4, GestureAxis.Vertical)],
            document.Gestures);
    }

    [TestMethod]
    [DataRow("gesture = 1, horizontal, workspace", "Invalid value 1 for finger count.")]
    [DataRow("gesture = 10, horizontal, workspace", "Invalid value 10 for finger count.")]
    [DataRow("gesture = 3, sideways, workspace", "Invalid direction: sideways.")]
    [DataRow("gesture = 3, horizontal, teleport", "Invalid gesture: teleport.")]
    [DataRow("gesture = 3, pinch, workspace", "Invalid direction: pinch.")]
    [DataRow("gesture = 3, pinchin, workspace", "Invalid direction: pinchin.")]
    [DataRow("gesture = 3, pinchout, workspace", "Invalid direction: pinchout.")]
    [DataRow("gestureq = 3, horizontal, workspace", "Invalid gesture flag 'q'.")]
    [DataRow("gesture = 3, swipe, workspace", "Invalid direction: swipe.")]
    [DataRow("gesture = 3, left, workspace", "Invalid direction: left.")]
    [DataRow("gesture = 3, d, workspace", "Invalid direction: d.")]
    [DataRow("gesture = 3, horizontal, mod: Win, workspace", "A gesture is <fingers>, horizontal or vertical, workspace.")]
    [DataRow("gesture = 3, horizontal, scale: 2, workspace", "A gesture is <fingers>, horizontal or vertical, workspace.")]
    [DataRow("gesture = 3, horizontal, unset", "Invalid gesture: unset.")]
    [DataRow("gesturep = 3, horizontal, workspace", "Invalid gesture flag 'p'.")]
    public void MalformedGestureWarnsAndIsSkipped(string line, string warning)
    {
        var document = ConfigParser.Parse(line + "\n");

        Assert.IsEmpty(document.Gestures);
        Assert.AreEqual(new ConfigWarning(1, warning), Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    [DataRow("close")]
    [DataRow("special")]
    [DataRow("dispatcher")]
    [DataRow("float")]
    [DataRow("fullscreen")]
    [DataRow("move")]
    [DataRow("resize")]
    [DataRow("cursorZoom")]
    [DataRow("scrollMove")]
    [DataRow("")]
    public void OtherGestureActionsWarnAndAreSkipped(string action)
    {
        var document = ConfigParser.Parse($"gesture = 3, horizontal, {action}\n");

        Assert.IsEmpty(document.Gestures);
        Assert.AreEqual(new ConfigWarning(1, $"Invalid gesture: {action}."), Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void FingersLeftPastTheCancelRatioSwitchesToTheNextWorkspace()
    {
        var core = Twin(Horizontal);

        var ended = Swipe(core, 3, Steps(20, -10, 0));

        Assert.AreEqual(new WorkspaceId("2"), ended.State.ActiveWorkspace);
    }

    [TestMethod]
    public void ShortSlowSwipeReverts()
    {
        var core = Twin(Horizontal);

        var ended = Swipe(core, 3, Steps(14, -10, 0));

        Assert.AreEqual(WorkspaceId.Initial, ended.State.ActiveWorkspace);
    }

    [TestMethod]
    public void FastSwipeForcesTheChange()
    {
        var core = Twin(Horizontal);

        Assert.AreEqual(new WorkspaceId("2"), Swipe(core, 3, Steps(2, -60, 0)).State.ActiveWorkspace);
    }

    [TestMethod]
    public void FingersRightGoToThePreviousWorkspace()
    {
        var core = Twin(Horizontal);
        core.Handle(new Dispatch("workspace", "2"));

        var ended = Swipe(core, 3, Steps(20, 10, 0));

        Assert.AreEqual(WorkspaceId.Initial, ended.State.ActiveWorkspace);
    }

    [TestMethod]
    public void ThereIsNoPreviousWorkspaceBeforeTheFirst()
    {
        var core = Twin(Horizontal);

        var ended = Swipe(core, 3, Steps(20, 10, 0));

        Assert.AreEqual(WorkspaceId.Initial, ended.State.ActiveWorkspace);
    }

    [TestMethod]
    public void VerticalGestureReadsVerticalMovement()
    {
        var core = Twin("gesture = 3, vertical, workspace\n");

        Assert.AreEqual(WorkspaceId.Initial, Swipe(core, 3, Steps(20, -10, 0)).State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("2"), Swipe(core, 3, Steps(20, 0, -10)).State.ActiveWorkspace);
    }

    [TestMethod]
    public void FingerCountMustMatch()
    {
        var core = Twin("gesture = 4, horizontal, workspace\n");

        Assert.AreEqual(WorkspaceId.Initial, Swipe(core, 3, Steps(20, -10, 0)).State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("2"), Swipe(core, 4, Steps(20, -10, 0)).State.ActiveWorkspace);
    }

    [TestMethod]
    public void SwipingPastTheLastWorkspaceCreatesOne()
    {
        var core = Twin(Horizontal);
        core.Handle(new Dispatch("workspace", "2"));

        Assert.AreEqual(new WorkspaceId("3"), Swipe(core, 3, Steps(20, -10, 0)).State.ActiveWorkspace);
    }

    [TestMethod]
    public void EmptyWorkspaceDoesNotSwipeOnToANewOne()
    {
        var core = Load(Horizontal);

        var ended = Swipe(core, 3, Steps(20, -10, 0));

        Assert.AreEqual(WorkspaceId.Initial, ended.State.ActiveWorkspace);
    }

    [TestMethod]
    public void DirectionLockHoldsTheFirstDirection()
    {
        var locked = Twin(Horizontal);
        locked.Handle(new Dispatch("workspace", "2"));
        (double, double)[] steps = [(-20, 0), (200, 0)];

        Assert.AreEqual(new WorkspaceId("2"), Swipe(locked, 3, steps).State.ActiveWorkspace);
    }

    [TestMethod]
    public void ASwipeSkipsEmptyWorkspaces()
    {
        var core = Load(Horizontal);
        Open(core, 1, "1");
        Open(core, 3, "3");
        core.Handle(new Dispatch("workspace", "1"));

        Assert.AreEqual(new WorkspaceId("3"), Swipe(core, 3, Steps(20, -10, 0)).State.ActiveWorkspace);
    }

    [TestMethod]
    public void LockedSessionDoesNotSwipe()
    {
        var core = Twin(Horizontal);
        core.KeyState.Inhibit(true);

        var ended = Swipe(core, 3, Steps(20, -10, 0));

        Assert.AreEqual(WorkspaceId.Initial, ended.State.ActiveWorkspace);
    }

    [TestMethod]
    public void ReloadReplacesTheGestures()
    {
        var core = Twin(Horizontal);
        core.Handle(new Configure(ConfigParser.Parse("gesture = 4, horizontal, workspace\n")));

        Assert.AreEqual(WorkspaceId.Initial, Swipe(core, 3, Steps(20, -10, 0)).State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("2"), Swipe(core, 4, Steps(20, -10, 0)).State.ActiveWorkspace);
    }

    private static WindowManager Twin(string config)
    {
        var core = Load(config);
        Open(core, 1, "1");
        Open(core, 2, "2");
        core.Handle(new Dispatch("workspace", "1"));
        return core;
    }

    private static WindowManager Load(string config)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(config)));
        return core;
    }

    private static void Open(WindowManager core, long window, string workspace)
    {
        core.Handle(new Dispatch("workspace", workspace));
        core.Handle(new WindowAppeared(new WindowId(window), Area));
    }

    private static (double X, double Y)[] Steps(int count, double x, double y) =>
        Enumerable.Repeat((x, y), count).ToArray();

    private static Outcome Swipe(WindowManager core, int fingers, IEnumerable<(double X, double Y)> steps)
    {
        core.Handle(new SwipeBegan(fingers));
        foreach (var (x, y) in steps)
            core.Handle(new SwipeUpdated(fingers, x, y));

        return core.Handle(new SwipeEnded());
    }
}
