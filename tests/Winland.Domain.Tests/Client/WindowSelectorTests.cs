namespace Winland.Domain.Tests;

public class WindowSelectorTests
{
    private static readonly WorkspaceId Home = WorkspaceId.FromNumber(1);
    private static readonly WorkspaceId Away = WorkspaceId.FromNumber(2);

    [Fact]
    public void EmptyAndActiveNamesResolveTheActiveClient()
    {
        var clients = new[] { Client(1, "kitty", workspace: Home) };
        var active = new WindowId(1);

        Assert.Equal(active, WindowSelector.Find("", active, clients));
        Assert.Equal(active, WindowSelector.Find("active", active, clients));
        Assert.Equal(active, WindowSelector.Find("activewindow", active, clients));
    }

    [Fact]
    public void ABarePatternMatchesTheClassAndAPrefixSelectsTheField()
    {
        var clients = new[]
        {
            Client(1, "kitty", "term", initialClass: "old", initialTitle: "first", workspace: Home),
        };
        var active = new WindowId(1);

        Assert.Equal(active, WindowSelector.Find("kitty", active, clients));
        Assert.Equal(active, WindowSelector.Find("class:^kitty$", active, clients));
        Assert.Equal(active, WindowSelector.Find("title:term", active, clients));
        Assert.Equal(active, WindowSelector.Find("initialclass:old", active, clients));
        Assert.Equal(active, WindowSelector.Find("initialtitle:first", active, clients));
        Assert.Null(WindowSelector.Find("title:kitty", active, clients));
        Assert.Null(WindowSelector.Find("class:kit", active, clients));
        Assert.Equal(active, WindowSelector.Find("class:kit.*", active, clients));
    }

    [Fact]
    public void AddressAndPidMatchTheClientListingExactly()
    {
        var clients = new[] { Client(42, "app", processId: 4242, workspace: Home) };
        var active = new WindowId(42);
        var address = active.Address;

        Assert.Equal("0x2a", address);
        Assert.Equal(active, WindowSelector.Find("address:" + address, active, clients));
        Assert.Equal(active, WindowSelector.Find("pid:4242", active, clients));
        Assert.Null(WindowSelector.Find("address:0x2A", active, clients));
        Assert.Null(WindowSelector.Find("pid:04242", active, clients));
    }

    [Fact]
    public void StableIdMatchesTheHexId()
    {
        var clients = new[]
        {
            Client(1, "name", workspace: Home),
            Client(26, "other", workspace: Home),
        };
        var active = new WindowId(1);

        Assert.Equal(new WindowId(26), WindowSelector.Find("stableid:1a", active, clients));
        Assert.Null(WindowSelector.Find("stableid:26", active, clients));
    }

    [Fact]
    public void TheActiveWorkspaceWinsWhenSeveralClientsMatch()
    {
        var clients = new[]
        {
            Client(2, "kitty", workspace: Away),
            Client(1, "other", workspace: Home),
            Client(3, "kitty", workspace: Home),
            Client(4, "kitty", workspace: Home),
        };

        Assert.Equal(new WindowId(3), WindowSelector.Find("class:kitty", new WindowId(1), clients));
        Assert.Equal(new WindowId(2), WindowSelector.Find("class:kitty", new WindowId(9), clients));
    }

    [Fact]
    public void FloatingAndTiledStayOnTheActiveClientsWorkspace()
    {
        var clients = new[]
        {
            Client(2, "away", workspace: Away, floating: true),
            Client(1, "here", workspace: Home),
            Client(3, "float", workspace: Home, floating: true),
        };

        Assert.Equal(new WindowId(3), WindowSelector.Find("floating", new WindowId(1), clients));
        Assert.Equal(new WindowId(1), WindowSelector.Find("tiled", new WindowId(1), clients));
        Assert.Null(WindowSelector.Find("floating", null, clients));
    }

    [Fact]
    public void ABrokenPatternMatchesNothing()
    {
        var clients = new[] { Client(1, "kitty", workspace: Home) };

        Assert.Null(WindowSelector.Find("class:(", new WindowId(1), clients));
    }

    [Theory]
    [InlineData("")]
    [InlineData("active")]
    [InlineData("activewindow")]
    [InlineData("floating")]
    [InlineData("tiled")]
    [InlineData("kitty")]
    [InlineData("0x2a")]
    [InlineData("class:^kitty$")]
    [InlineData("class:")]
    [InlineData("initialclass:old")]
    [InlineData("title:a{1,2} b")]
    [InlineData("initialtitle:first")]
    [InlineData("pid:42")]
    [InlineData("address:0x2a")]
    [InlineData("address:0X2A")]
    [InlineData("stableid:2a")]
    public void EverySelectorFindReadsIsWellFormed(string selector) =>
        Assert.Null(WindowSelector.Malformed(selector));

    [Theory]
    [InlineData("[", "'[' is not a valid pattern")]
    [InlineData("class:[", "'[' is not a valid pattern")]
    [InlineData("initialclass:*", "'*' is not a valid pattern")]
    [InlineData("title:(a", "'(a' is not a valid pattern")]
    [InlineData("initialtitle:a)|(b", "'a)|(b' is not a valid pattern")]
    [InlineData("pid:", "pid: takes a process id, such as pid:1234")]
    [InlineData("pid:12a", "pid: takes a process id, such as pid:1234")]
    [InlineData("address:", "address: takes a window address, such as address:0x2a")]
    [InlineData("address:2a", "address: takes a window address, such as address:0x2a")]
    [InlineData("address:0xzz", "address: takes a window address, such as address:0x2a")]
    [InlineData("stableid:", "stableid: takes the stableID clients reports, such as stableid:2a")]
    [InlineData("stableid:xyz", "stableid: takes the stableID clients reports, such as stableid:2a")]
    public void AMalformedSelectorSaysWhatIsWrong(string selector, string problem) =>
        Assert.Equal(problem, WindowSelector.Malformed(selector));

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
