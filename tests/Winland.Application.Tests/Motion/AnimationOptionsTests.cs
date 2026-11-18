namespace Winland.Application.Tests;

[TestClass]
public class AnimationOptionsTests
{
    [TestMethod]
    public void OpeningAndMovingTakeThreeHundredMilliseconds()
    {
        var core = new WindowManager();
        var first = core.Handle(new WindowAppeared(new WindowId(1), Area));
        var second = core.Handle(new WindowAppeared(new WindowId(2), Area));

        Assert.AreEqual(new Timing(Bezier.Default, 300), Assert.ContainsSingle(first.Motions).Move);
        Assert.AreEqual(new Timing(Bezier.Default, 800), Assert.ContainsSingle(first.Motions).Fade);
        Assert.AreEqual(new Timing(Bezier.Default, 300), Assert.ContainsSingle(motion => motion.Window == new WindowId(1), second.Motions).Move);
    }

    [TestMethod]
    public void WorkspaceTransitionsKeepTheirEightHundredMilliseconds()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var switched = core.Handle(new Dispatch("workspace", "2"));

        Assert.AreEqual(new Timing(Bezier.Default, 800), Assert.ContainsSingle(switched.Motions).Move);
    }

    [TestMethod]
    public void ClosingIsImmediate()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var closed = core.Handle(new Dispatch("killactive"));

        Assert.AreEqual(new WindowId(1), closed.Close);
        Assert.DoesNotContain(motion => motion.Window == new WindowId(1), closed.Motions);
    }

    [TestMethod]
    public void ReloadAppliesStartupLinesWithoutLaunchingThem()
    {
        var document = ConfigParser.Parse("exec-once = notepad\nexec-once = explorer\n");
        var core = new WindowManager { ReadConfig = () => new ConfigRead(document) };

        Assert.AreSequenceEqual([new Launch("notepad", null), new Launch("explorer", null)], core.StartupLaunches(document));
        var reloaded = core.Execute("reload");

        Assert.AreEqual("ok", reloaded.Text);
        Assert.IsNull(reloaded.Effect.Launch);
    }

    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
}
