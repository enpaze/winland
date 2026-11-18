namespace Winland.Domain.Tests;

[TestClass]
public class WindowSelectorTests
{
    private static readonly WorkspaceId Home = WorkspaceId.FromNumber(1, MonitorId.Primary);
    private static readonly WorkspaceId Away = WorkspaceId.FromNumber(2, MonitorId.Primary);

    [TestMethod]
    public void EmptyAndActiveResolveTheActiveClient()
    {
        var clients = new[] { Client(1, "kitty", workspace: Home) };
        var active = new WindowId(1);

        Assert.AreEqual(active, WindowSelector.Find("", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("active", active, clients));
    }

    [TestMethod]
    public void AKeywordIsTheWholeSelectorAndAnyLongerWordIsAClass()
    {
        var clients = new[]
        {
            Client(1, "kitty", workspace: Home),
            Client(2, "activewindow", workspace: Home),
            Client(3, "floatingapp", workspace: Home, floating: true),
        };
        var active = new WindowId(1);

        Assert.AreEqual(new WindowId(2), WindowSelector.Find("activewindow", active, clients));
        Assert.AreEqual(new WindowId(3), WindowSelector.Find("floatingapp", active, clients));
        Assert.IsNull(WindowSelector.Find("tiledone", active, clients));
    }

    [TestMethod]
    public void ABarePatternMatchesTheClassAndAPrefixSelectsTheField()
    {
        var clients = new[] { Client(1, "kitty", "term", "kitty.exe", workspace: Home) };
        var active = new WindowId(1);

        Assert.AreEqual(active, WindowSelector.Find("kitty", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("class:kitty", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("title:term", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("exe:kitty.exe", active, clients));
        Assert.IsNull(WindowSelector.Find("title:kitty", active, clients));
        Assert.IsNull(WindowSelector.Find("exe:kitty", active, clients));
        Assert.IsNull(WindowSelector.Find("class:kitty.exe", active, clients));
    }

    [TestMethod]
    public void APatternMatchesTheWholeFieldWithoutCaseAndAStarMatchesAnyRun()
    {
        var clients = new[] { Client(1, "Kitty", "Release Notes", "Kitty.exe", workspace: Home) };
        var active = new WindowId(1);

        Assert.AreEqual(active, WindowSelector.Find("KITTY", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("title:*notes", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("title:release*", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("title:*", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("exe:kitty.EXE", active, clients));
        Assert.IsNull(WindowSelector.Find("title:Notes", active, clients));
        Assert.IsNull(WindowSelector.Find("kit", active, clients));
        Assert.IsNull(WindowSelector.Find("class:kit.*", active, clients));
        Assert.IsNull(WindowSelector.Find("class:^kitty$", active, clients));
    }

    [TestMethod]
    public void AddressAndPidMatchTheClientListingExactly()
    {
        var clients = new[] { Client(42, "app", processId: 4242, workspace: Home) };
        var active = new WindowId(42);
        var address = active.Address;

        Assert.AreEqual("0x2a", address);
        Assert.AreEqual(active, WindowSelector.Find("address:" + address, active, clients));
        Assert.AreEqual(active, WindowSelector.Find("address:0X2A", active, clients));
        Assert.AreEqual(active, WindowSelector.Find("pid:4242", active, clients));
        Assert.IsNull(WindowSelector.Find("address:0x2b", active, clients));
        Assert.IsNull(WindowSelector.Find("0x2a", active, clients));
        Assert.IsNull(WindowSelector.Find("pid:04242", active, clients));
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
    public void RegexCharactersAreLiteral()
    {
        var clients = new[] { Client(1, "kitty", workspace: Home), Client(2, "a(b)[c]", workspace: Home) };

        Assert.IsNull(WindowSelector.Find("class:(", new WindowId(1), clients));
        Assert.AreEqual(new WindowId(2), WindowSelector.Find("class:a(b)[c]", new WindowId(1), clients));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("active")]
    [DataRow("floating")]
    [DataRow("tiled")]
    [DataRow("kitty")]
    [DataRow("0x2a")]
    [DataRow("class:")]
    [DataRow("[")]
    [DataRow("class:[")]
    [DataRow("title:(a")]
    [DataRow("title:a)|(b")]
    [DataRow("exe:*")]
    [DataRow("pid:42")]
    [DataRow("address:0x2a")]
    [DataRow("address:0X2A")]
    public void EverySelectorFindReadsIsWellFormed(string selector) =>
        Assert.IsNull(WindowSelector.Malformed(selector));

    [TestMethod]
    [DataRow("pid:", "pid: takes a process id, such as pid:1234")]
    [DataRow("pid:12a", "pid: takes a process id, such as pid:1234")]
    [DataRow("address:", "address: takes a window address, such as address:0x2a")]
    [DataRow("address:2a", "address: takes a window address, such as address:0x2a")]
    [DataRow("address:0xzz", "address: takes a window address, such as address:0x2a")]
    public void AMalformedSelectorSaysWhatIsWrong(string selector, string problem) =>
        Assert.AreEqual(problem, WindowSelector.Malformed(selector));

    private static Client Client(
        long window,
        string windowClass,
        string title = "",
        string exe = "",
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
            Exe: exe,
            ProcessId: processId);
}
