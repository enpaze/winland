namespace Winland.Application.Tests;

[TestClass]
public class LaunchTests
{
    [TestMethod]
    public void ExecLaunchesTheCommand()
    {
        var outcome = new WindowManager().Handle(new Dispatch("exec", "notepad --title hello"));

        Assert.AreEqual(new Launch("notepad --title hello", null) { InitialWorkspace = WorkspaceId.Initial }, outcome.Launch);
    }

    [TestMethod]
    public void AWordNamedElevatedIsPartOfTheCommand()
    {
        var outcome = new WindowManager().Handle(new Dispatch("exec", "elevated notepad"));

        Assert.AreEqual(new Launch("elevated notepad", null) { InitialWorkspace = WorkspaceId.Initial }, outcome.Launch);
    }

    [TestMethod]
    public void BoundExecLaunchesTheCommand()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, E, exec, notepad")));
        core.Handle(new KeyPressed("Super_L"));

        var plain = core.Handle(new KeyPressed("E"));

        Assert.AreEqual(new Launch("notepad", null) { InitialWorkspace = WorkspaceId.Initial }, plain.Launch);
    }

    [TestMethod]
    public void EmptyExecDoesNotLaunch()
    {
        Assert.IsNull(new WindowManager().Handle(new Dispatch("exec", "   ")).Launch);
    }

    [TestMethod]
    public void ARuleBlockWarnsAndDoesNotLaunch()
    {
        var dispatched = new WindowManager().Handle(new Dispatch("exec", "[float; size 800 600] kitty"));
        Assert.IsNull(dispatched.Launch);
        Assert.AreSequenceEqual([new ConfigWarned(0, "Unsupported rule block.", null)], dispatched.Events.OfType<ConfigWarned>());

        var document = ConfigParser.Parse("exec-once = [float; size 800 600] app\nexec-once = notepad\n");
        Assert.AreSequenceEqual([new ConfigWarning(1, "Unsupported rule block.")], document.Warnings);
        Assert.AreEqual("notepad", Assert.ContainsSingle(new WindowManager().StartupLaunches(document)).CommandLine);
    }
}
