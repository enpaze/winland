namespace Winland.Application.Tests;

public class AnimationOptionsTests
{
    [Fact]
    public void OpeningAndMovingTakeThreeHundredMilliseconds()
    {
        var core = new WindowManager();
        var first = core.Handle(new WindowAppeared(new WindowId(1), Area));
        var second = core.Handle(new WindowAppeared(new WindowId(2), Area));

        Assert.Equal(new Timing(Bezier.Default, 300), Assert.Single(first.Motions).Move);
        Assert.Equal(new Timing(Bezier.Default, 300), Assert.Single(second.Motions, motion => motion.Window == new WindowId(1)).Move);
    }

    [Fact]
    public void ClosingIsImmediate()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var closed = core.Handle(new Dispatch("killactive"));

        Assert.Equal(new WindowId(1), closed.Close);
        Assert.DoesNotContain(closed.Motions, motion => motion.Window == new WindowId(1));
    }

    [Fact]
    public void ReloadAppliesStartupLinesWithoutLaunchingThem()
    {
        var document = ConfigParser.Parse("exec-once = notepad\nexec-once = explorer\n");
        var core = new WindowManager { ReadConfig = () => new ConfigRead(document) };

        Assert.Equal([new Launch("notepad", null), new Launch("explorer", null)], core.StartupLaunches(document));
        var reloaded = core.Execute("reload");

        Assert.Equal("ok", reloaded.Text);
        Assert.Null(reloaded.Effect.Launch);
    }

    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
}
