namespace Winland.Application.Tests;

[TestClass]
public class LiveSwipeTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    private const string Horizontal = "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\ngesture = 3, horizontal, workspace\n";

    [TestMethod]
    public void TheWorkspaceFollowsTheFingersAndItsNeighbourComesInBeside()
    {
        var core = Twin(Horizontal);
        core.Handle(new SwipeBegan(3));

        var first = Move(core, 1, -10);
        var halfway = Move(core, 14, -10);

        Assert.Contains(new WindowId(2), first.Show);
        Assert.DoesNotContain(new WindowId(2), halfway.Show);

        var leaving = Assert.ContainsSingle(motion => motion.Window == new WindowId(1), halfway.Motions);
        var coming = Assert.ContainsSingle(motion => motion.Window == new WindowId(2), halfway.Motions);
        Assert.AreEqual(-960, leaving.To.X);
        Assert.AreEqual(960, coming.To.X);
        Assert.IsNull(leaving.Move);
    }

    [TestMethod]
    public void ASwipeThatFallsShortSlidesBackAndHidesTheNeighbourAgain()
    {
        var core = Twin(Horizontal);
        core.Handle(new SwipeBegan(3));
        Move(core, 5, -10);

        var ended = core.Handle(new SwipeEnded());

        Assert.AreEqual(WorkspaceId.Initial, ended.State.ActiveWorkspace);
        var back = Assert.ContainsSingle(motion => motion.Window == new WindowId(1), ended.Motions);
        Assert.AreEqual(new Rectangle(-320, 0, 1920, 1080), back.From);
        Assert.AreEqual(Area, back.To);
        var away = Assert.ContainsSingle(motion => motion.Window == new WindowId(2), ended.Motions);
        Assert.AreEqual(MotionEnd.Hide, away.End);
        Assert.AreEqual(1920, away.To.X);
        Assert.Contains(new WindowId(2), ended.Hide);
    }

    [TestMethod]
    public void ASwitchStartsFromWhereTheSwipeLeftTheWindows()
    {
        var core = Twin(Horizontal);
        core.Handle(new SwipeBegan(3));
        Move(core, 20, -10);

        var ended = core.Handle(new SwipeEnded());

        Assert.AreEqual(new WorkspaceId("2"), ended.State.ActiveWorkspace);
        var coming = Assert.ContainsSingle(motion => motion.Window == new WindowId(2), ended.Motions);
        Assert.AreEqual(new Rectangle(640, 0, 1920, 1080), coming.From);
        Assert.AreEqual(Area, coming.To);
        Assert.IsNotNull(coming.Move);
        var leaving = Assert.ContainsSingle(motion => motion.Window == new WindowId(1), ended.Motions);
        Assert.AreEqual(new Rectangle(-1280, 0, 1920, 1080), leaving.From);
    }


    private static Outcome Move(WindowManager core, int count, double x)
    {
        Outcome last = null!;
        for (var i = 0; i < count; i++)
            last = core.Handle(new SwipeUpdated(3, x, 0));
        return last;
    }

    private static WindowManager Twin(string config)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(config)));
        Open(core, 1, "1");
        Open(core, 2, "2");
        core.Handle(new Dispatch("workspace", "1"));
        return core;
    }

    private static void Open(WindowManager core, long window, string workspace)
    {
        core.Handle(new Dispatch("workspace", workspace));
        core.Handle(new WindowAppeared(new WindowId(window), Area));
    }
}
