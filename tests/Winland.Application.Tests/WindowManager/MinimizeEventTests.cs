namespace Winland.Application.Tests;

[TestClass]
public class MinimizeEventTests
{
    [TestMethod]
    public void MinimizeAndRestoreAreMinimizedEvents()
    {
        var desk = new TestDesktop();
        var events = new List<string>();
        var kitty = desk.Spawn("hyprtester-wlr-minimize");
        var pid = desk.Client("hyprtester-wlr-minimize").GetProperty("pid").GetInt64();

        Record(events, desk.Handle(new WindowMinimized(kitty)));
        Assert.AreEqual($"{pid}:true", string.Join(',', events));

        Record(events, desk.Handle(new WindowRestored(kitty)));
        Assert.AreEqual($"{pid}:true,{pid}:false", string.Join(',', events));
    }

    private static void Record(List<string> events, Outcome outcome)
    {
        foreach (var minimized in outcome.Events.OfType<ClientMinimized>())
        {
            var pid = outcome.State.Clients.Concat(outcome.State.Hidden).First(client => client.Window == minimized.Window).ProcessId;
            events.Add($"{pid}:{(minimized.Minimized ? "true" : "false")}");
        }
    }
}
