namespace Winland.Application.Tests;

public class LaunchTests
{
    [Fact]
    public void ExecLaunchesTheCommand()
    {
        var outcome = new WindowManager().Handle(new Dispatch("exec", "notepad --title hello"));

        Assert.Equal(new Launch("notepad --title hello", null) { InitialWorkspace = WorkspaceId.Initial }, outcome.Launch);
    }

    [Fact]
    public void AWordNamedElevatedIsPartOfTheCommand()
    {
        var outcome = new WindowManager().Handle(new Dispatch("exec", "elevated notepad"));

        Assert.Equal(new Launch("elevated notepad", null) { InitialWorkspace = WorkspaceId.Initial }, outcome.Launch);
    }

    [Fact]
    public void BoundExecLaunchesTheCommand()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, E, exec, notepad")));
        core.Handle(new KeyPressed("Super_L"));

        var plain = core.Handle(new KeyPressed("E"));

        Assert.Equal(new Launch("notepad", null) { InitialWorkspace = WorkspaceId.Initial }, plain.Launch);
    }

    [Fact]
    public void EmptyExecDoesNotLaunch()
    {
        Assert.Null(new WindowManager().Handle(new Dispatch("exec", "   ")).Launch);
    }

    [Fact]
    public void ARuleBlockWarnsAndDoesNotLaunch()
    {
        var dispatched = new WindowManager().Handle(new Dispatch("exec", "[float; size 800 600] kitty"));
        Assert.Null(dispatched.Launch);
        Assert.Equal([new ConfigWarned(0, "Unsupported rule block.", null)], dispatched.Events.OfType<ConfigWarned>());

        var document = ConfigParser.Parse("exec-once = [float; size 800 600] app\nexec-once = notepad\n");
        Assert.Equal([new ConfigWarning(1, "Unsupported rule block.")], document.Warnings);
        Assert.Equal("notepad", Assert.Single(new WindowManager().StartupLaunches(document)).CommandLine);
    }
}
