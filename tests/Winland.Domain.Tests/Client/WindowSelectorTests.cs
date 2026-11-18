namespace Winland.Domain.Tests;

[TestClass]
public class WindowSelectorTests
{
    private static readonly WorkspaceId Home = WorkspaceId.FromNumber(1, MonitorId.Primary);
    private static readonly WorkspaceId Away = WorkspaceId.FromNumber(2, MonitorId.Primary);

    [TestMethod]
    public void EmptyAndActiveNamesResolveTheActiveClient()
    {
        var clients = new[] { Client(1, "kitty", workspace: Home) };
        var active = new WindowId(1);

        Assert.AreEqual(active, WindowSelector.Find("", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("active", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("activewindow", active, clients));
    }

    [TestMethod]
    public void ABarePatternMatchesTheClassAndAPrefixSelectsTheField()
    {
        var clients = new[]
        {
            Client(1, "kitty", "term", initialClass: "old", initialTitle: "first", workspace: Home),
        };
        var active = new WindowId(1);

        Assert.AreEqual(active, WindowSelector.Find("kitty", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("class:^kitty$", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("title:term", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("initialclass:old", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("initialtitle:first", active, clients));
        Assert.IsNull(WindowSelector.Find("title:kitty", active, clients));
        Assert.IsNull(WindowSelector.Find("class:kit", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("class:kit.*", active, clients));
    }

    [TestMethod]
    public void AddressAndPidMatchTheClientListingExactly()
    {
        var clients = new[] { Client(42, "app", processId: 4242, workspace: Home) };
        var active = new WindowId(42);
        var address = active.Address;

        Assert.AreEqual("0x2a", address);
        Assert.AreEqual(active, WindowSelector.Find("address:" + address, active, clients));
        Assert.AreEqual(active, WindowSelector.Find("pid:4242", active, clients));
        Assert.IsNull(WindowSelector.Find("address:0x2A", active, clients));
        Assert.IsNull(WindowSelector.Find("pid:04242", active, clients));
    }

    [TestMethod]
    public void StableIdMatchesTheHexId()
    {
        var clients = new[]
        {
            Client(1, "name", workspace: Home),
            Client(26, "other", workspace: Home),
        };
        var active = new WindowId(1);

        Assert.AreEqual(new WindowId(26), WindowSelector.Find("stableid:1a", active, clients));
        Assert.IsNull(WindowSelector.Find("stableid:26", active, clients));
    }

    [TestMethod]
    public void TheActiveWorkspaceWinsWhenSeveralClientsMatch()
    {
        var clients = new[]
        {
            Client(2, "kitty", workspace: Away),
            Client(1, "other", workspace: Home),
            Client(3, "kitty", workspace: Home),
            Client(4, "kitty", workspace: Home),
        };

        Assert.AreEqual(new WindowId(3), WindowSelector.Find("class:kitty", new WindowId(1), clients));
        Assert.AreEqual(new WindowId(2), WindowSelector.Find("class:kitty", new WindowId(9), clients));
    }

    [TestMethod]
    public void FloatingAndTiledStayOnTheActiveClientsWorkspace()
    {
        var clients = new[]
        {
            Client(2, "away", workspace: Away, floating: true),
            Client(1, "here", workspace: Home),
            Client(3, "float", workspace: Home, floating: true),
        };

        Assert.AreEqual(new WindowId(3), WindowSelector.Find("floating", new WindowId(1), clients));
        Assert.AreEqual(new WindowId(1), WindowSelector.Find("tiled", new WindowId(1), clients));
        Assert.IsNull(WindowSelector.Find("floating", null, clients));
    }

    [TestMethod]
    public void ABrokenPatternMatchesNothing()
    {
        var clients = new[] { Client(1, "kitty", workspace: Home) };

        Assert.IsNull(WindowSelector.Find("class:(", new WindowId(1), clients));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("active")]
    [DataRow("activewindow")]
    [DataRow("floating")]
    [DataRow("tiled")]
    [DataRow("kitty")]
    [DataRow("0x2a")]
    [DataRow("class:^kitty$")]
    [DataRow("class:")]
    [DataRow("initialclass:old")]
    [DataRow("title:a{1,2} b")]
    [DataRow("initialtitle:first")]
    [DataRow("pid:42")]
    [DataRow("address:0x2a")]
    [DataRow("address:0X2A")]
    [DataRow("stableid:2a")]
    public void EverySelectorFindReadsIsWellFormed(string selector) =>
        Assert.IsNull(WindowSelector.Malformed(selector));

    [TestMethod]
    [DataRow("[", "'[' is not a valid pattern")]
    [DataRow("class:[", "'[' is not a valid pattern")]
    [DataRow("initialclass:*", "'*' is not a valid pattern")]
    [DataRow("title:(a", "'(a' is not a valid pattern")]
    [DataRow("initialtitle:a)|(b", "'a)|(b' is not a valid pattern")]
    [DataRow("pid:", "pid: takes a process id, such as pid:1234")]
    [DataRow("pid:12a", "pid: takes a process id, such as pid:1234")]
    [DataRow("address:", "address: takes a window address, such as address:0x2a")]
    [DataRow("address:2a", "address: takes a window address, such as address:0x2a")]
    [DataRow("address:0xzz", "address: takes a window address, such as address:0x2a")]
    [DataRow("stableid:", "stableid: takes the stableID clients reports, such as stableid:2a")]
    [DataRow("stableid:xyz", "stableid: takes the stableID clients reports, such as stableid:2a")]
    public void AMalformedSelectorSaysWhatIsWrong(string selector, string problem) =>
        Assert.AreEqual(problem, WindowSelector.Malformed(selector));

    private static Client Client(
        long window,
        string windowClass,
        string title = "",
        string initialClass = "",
        string initialTitle = "",
        long processId = 0,
        WorkspaceId workspace = default,
        bool floating = false) =>
        new(
            new ClientId(window),
            new WindowId(window),
            Floating: floating,
            Workspace: workspace,
            Class: windowClass,
            Title: title,
            ProcessId: processId,
            InitialClass: initialClass,
            InitialTitle: initialTitle);
}
