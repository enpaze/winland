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
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = Win+E, exec notepad")));
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
    public void AWorkspaceBlockHoldsTheWindowOnThatWorkspace()
    {
        var dispatched = new WindowManager().Handle(new Dispatch("exec", "[workspace 3] kitty --hold"));
        Assert.AreEqual(new Launch("kitty --hold", null) { InitialWorkspace = new WorkspaceId("3") }, dispatched.Launch);

        var document = ConfigParser.Parse("exec-once = [ workspace  3 ]app\nexec-once = notepad\n");
        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            [new Launch("app", null) { InitialWorkspace = new WorkspaceId("3") }, new Launch("notepad", null)],
            new WindowManager().StartupLaunches(document));
    }

    [TestMethod]
    [DataRow("[float; size 800 600] kitty")]
    [DataRow("[workspace 3; float] kitty")]
    [DataRow("[workspace 3 stay] kitty")]
    [DataRow("[workspace special:scratch] kitty")]
    [DataRow("[workspace 0] kitty")]
    [DataRow("[workspace 3 kitty")]
    public void AnyOtherRuleBlockWarnsAndDoesNotLaunch(string line)
    {
        const string Warning = "Unsupported rule block. Only [workspace N] is supported.";
        var dispatched = new WindowManager().Handle(new Dispatch("exec", line));
        Assert.IsNull(dispatched.Launch);
        Assert.AreSequenceEqual([new ConfigWarned(0, Warning, null)], dispatched.Events.OfType<ConfigWarned>());

        var document = ConfigParser.Parse($"exec-once = {line}\nexec-once = notepad\n");
        Assert.AreSequenceEqual([new ConfigWarning(1, Warning)], document.Warnings);
        Assert.AreEqual("notepad", Assert.ContainsSingle(new WindowManager().StartupLaunches(document)).CommandLine);
    }
}
