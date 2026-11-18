namespace Winland.Domain.Tests;

[TestClass]
public class ClientPropsTests
{
    private static readonly WindowId Window = new(1);
    private static readonly WindowTraits App = WindowTraits.Normal with { Class = "app" };

    [TestMethod]
    public void AForgottenWindowHasNoProps()
    {
        var props = new ClientProps();
        props.Opened(Window, App, [], Decide("windowrule = border_size 3, class:app\n"));
        props.SetSizes(Window, default, null, null);

        props.Forget(Window);

        Assert.IsNull(props.RuleProp(Window, "border_size"));
        Assert.IsNull(props.Sizes(Window));
    }

    private static WindowDecision Decide(string config) =>
        WindowRuleParser.Decide(App, ConfigParser.Parse(config).WindowRules);
}
