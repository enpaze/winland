namespace Winland.Application.Tests;

// The word elevated inside a rule block is an unknown window rule. The command still launches.
public class ElevatedWordTests
{
    private static readonly ConfigWarned Warned = new(0, "Unsupported window rule 'elevated'.", null);

    [Fact]
    public void AnExecLineWarnsAndLaunchesTheCommand()
    {
        var document = ConfigParser.Parse(
            "exec-once = [elevated] every\n" +
            "exec-once = [elevated] once\n");

        Assert.Equal(
            [
                new ConfigWarning(1, "Unsupported window rule 'elevated'."),
                new ConfigWarning(2, "Unsupported window rule 'elevated'."),
            ],
            document.Warnings);

        var core = new WindowManager();
        Assert.Equal(
            [
                new Launch("every", null),
                new Launch("once", null),
            ],
            core.StartupLaunches(document));
    }

    [Fact]
    public void ABindWarnsAndLaunchesTheCommand()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, E, exec, [elevated] bound\n")));
        core.Handle(new KeyPressed("Super_L"));

        var bound = core.Handle(new KeyPressed("E"));

        Assert.Equal(new Launch("bound", null) { InitialWorkspace = WorkspaceId.Initial }, bound.Launch);
        Assert.Equal([Warned], bound.Events.OfType<ConfigWarned>());
    }

    [Fact]
    public void ElevatedInsideARuleBlockWarnsAndKeepsTheOtherRules()
    {
        var inside = new WindowManager().Handle(new Dispatch("exec", "[float; elevated; workspace 2 silent] kitty"));

        Assert.Equal("kitty", inside.Launch?.CommandLine);
        Assert.Equal(
            [
                new WindowRule(WindowRuleEffect.Float, null, null, null, null),
                new WindowRule(WindowRuleEffect.Workspace, "2 silent", null, null, null),
            ],
            inside.Launch?.Rules);
        Assert.Equal([Warned], inside.Events.OfType<ConfigWarned>());
    }

    [Fact]
    public void ALeadingElevatedBlockIsTheRuleBlock()
    {
        var leading = new WindowManager().Handle(new Dispatch("exec", "[elevated] [tile] notepad"));

        Assert.Equal("[tile] notepad", leading.Launch?.CommandLine);
        Assert.Empty(leading.Launch!.Rules);
        Assert.Equal([Warned], leading.Events.OfType<ConfigWarned>());
    }

    [Fact]
    public void TheSameWordOverThePipeWarnsAndLaunches()
    {
        var reply = new WindowManager().Execute("dispatch exec [elevated; float] notepad");

        Assert.Equal("ok", reply.Text);
        Assert.Equal("notepad", reply.Effect.Launch?.CommandLine);
        Assert.Equal(
            [new WindowRule(WindowRuleEffect.Float, null, null, null, null)],
            reply.Effect.Launch?.Rules);
        Assert.Equal([Warned], reply.Effect.Events.OfType<ConfigWarned>());
    }
}
