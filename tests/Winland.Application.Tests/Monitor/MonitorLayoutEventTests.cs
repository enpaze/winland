namespace Winland.Application.Tests;

[TestClass]
public class MonitorLayoutEventTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1040);
    private static readonly Monitor One = new("DISPLAY1", "", 0, 0, 1920, 1080, 1, true, Area);

    [TestMethod]
    public void AMonitorThatComesOrChangesItsModeLaysTheMonitorsOutAgain()
    {
        var core = new WindowManager();

        Assert.ContainsSingle(e => e is MonitorLayoutChanged, core.Handle(new MonitorsChanged([One])).Events);
        Assert.ContainsSingle(e => e is MonitorLayoutChanged, core.Handle(new MonitorsChanged([One with { Scale = 1.25 }])).Events);
        Assert.ContainsSingle(e => e is MonitorLayoutChanged, core.Handle(new MonitorsChanged([One with { Scale = 1.25, RefreshRate = 144 }])).Events);
    }

    [TestMethod]
    public void AWorkAreaChangeOrTheSameMonitorsAgainIsNoLayoutChange()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([One]));

        Assert.DoesNotContain(e => e is MonitorLayoutChanged, core.Handle(new MonitorsChanged([One])).Events);
        Assert.DoesNotContain(e => e is MonitorLayoutChanged, core.Handle(new MonitorsChanged([One with { WorkArea = new Rectangle(0, 0, 1920, 1000) }])).Events);
    }

    [TestMethod]
    public void AReloadLaysTheMonitorsOutAfterItIsReadAndTheStreamSaysNothing()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([One]));

        var reloaded = core.Handle(new ConfigLoaded(ConfigParser.Parse("gaps_in = 3\n")));

        var events = reloaded.Events.ToList();
        Assert.IsTrue(events.FindIndex(e => e is MonitorLayoutChanged) > events.FindIndex(e => e is ConfigReloaded));
        Assert.DoesNotContain(line => line.Contains("layout", StringComparison.Ordinal), CommandProtocol.FormatStream(reloaded.Events, reloaded.State));
    }
}
