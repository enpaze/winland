namespace Winland.Domain.Tests;

public class ClientPropsTests
{
    private static readonly WindowId Window = new(1);
    private static readonly WindowTraits App = WindowTraits.Normal with { Class = "app" };

    [Fact]
    public void ARematchReplacesTheRulePropsAndKeepsWhatOnlyAppliesAtOpen()
    {
        var props = new ClientProps();
        props.Opened(Window, App, [], Decide("windowrule = no_initial_focus, class:^(app)$\nwindowrule = border_size 3, class:^(app)$\n"));

        props.Rematched(Window, Decide("windowrule = no_focus, class:^(app)$\n"));

        Assert.Null(props.RuleProp(Window, "border_size"));
        Assert.True(props.NoFocus(Window));
        Assert.True(props.FocusRuleOf(Window)?.NoInitialFocus);
    }

    [Fact]
    public void AForgottenWindowHasNoProps()
    {
        var props = new ClientProps();
        props.Opened(Window, App, [], Decide("windowrule = border_size 3, class:^(app)$\n"));
        props.SetSizes(Window, default, null, null);

        props.Forget(Window);

        Assert.Null(props.RuleProp(Window, "border_size"));
        Assert.Null(props.Sizes(Window));
    }

    private static WindowDecision Decide(string config) =>
        WindowRuleParser.Decide(App, ConfigParser.Parse(config).WindowRules);
}
