using System.Text.Json;

namespace Winland.Application.Tests;

public class GroupTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
    private static readonly WindowId First = new(1);
    private static readonly WindowId Second = new(2);
    private static readonly WindowId Third = new(3);

    // gaps_out 2 above and below, indicator 3, title row 14.
    private const int BarHeight = 21;

    [Fact]
    public void ABackgroundGroupMemberLeavesTheShownMemberFocusedWhenFollowingIsOff()
    {
        var core = Opened("input:follow_mouse = false\n", First);
        core.Execute("dispatch togglegroup");

        var opened = core.Handle(Appear(Second, "two"));

        Assert.Equal(First, Assert.Single(opened.GroupBars).Shown);
        Assert.Contains(Second, opened.State.Hidden.Select(client => client.Window));
        Assert.Contains("\"address\":\"0x1\"", core.Execute("j/activewindow").Text, StringComparison.Ordinal);
        var clicked = core.Handle(new WindowFocused(Second, FocusCause.User));
        Assert.Equal(Second, clicked.Focus);
    }

    [Fact]
    public void GroupBordersKeepTheirFixedColorsWhenOtherColorsChange()
    {
        var core = Opened("""
            general:col.active_border = 0xffff0000
            general:col.inactive_border = 0xff0000ff
            group:groupbar:col.active = 0xff00ff00
            group:groupbar:col.inactive = 0xffffffff
            """, First);
        var grouped = core.Execute("dispatch togglegroup").Effect;
        Assert.Equal(new BorderColor(0x66, 0xFF, 0xFF, 0), Decoration(grouped, First).Border);

        var otherWorkspace = core.Handle(new WindowAppeared(Second, Area)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
        });

        Assert.Equal(new BorderColor(0x66, 0x77, 0x77, 0), Decoration(otherWorkspace, First).Border);
        Assert.Equal(new BorderColor(0xFF, 0xFF, 0, 0), Decoration(otherWorkspace, Second).Border);
    }

    [Fact]
    public void ToggleGroupMakesAGroupOfOneWithABar()
    {
        var core = Opened(First);

        var made = core.Execute("dispatch togglegroup");

        Assert.Equal("ok", made.Text);
        Assert.Equal(["togglegroup>>1,1"], Lines(made));
        Assert.Equal([First], Assert.Single(made.Effect.State.Groups));
        var bar = Assert.Single(made.Effect.GroupBars);
        Assert.Equal(First, bar.Shown);
        Assert.Equal(new GroupTab(First, "one", true), Assert.Single(bar.Tabs));
        var tile = ClientOf(made.Effect, First).Bounds;
        Assert.Equal(new Rectangle(tile.X, tile.Y, tile.Width, BarHeight), bar.Bounds);
        Assert.Equal(
            new Rectangle(tile.X, tile.Y + BarHeight, tile.Width, tile.Height - BarHeight),
            Assert.Single(made.Effect.Placements).Bounds);
    }

    [Fact]
    public void AGroupWhoseShownMemberIsNotDecoratedHasNoBarAndKeepsItsTile()
    {
        var core = Opened(First);
        var tile = ClientOf(core.Execute("dispatch resizewindow").Effect, First).Bounds;
        core.Execute("dispatch togglegroup");

        var plain = core.Handle(new Configure(ConfigParser.Parse("windowrule = decorate 0, class:.*\n")));

        Assert.Empty(plain.GroupBars);
        Assert.Equal(tile, ClientOf(plain, First).Bounds);
        Assert.Single(core.Handle(new Configure(ConfigParser.Parse(""))).GroupBars);
    }

    [Fact]
    public void AGroupIsOneTileWithSeveralClients()
    {
        var core = Opened(First);
        var alone = ClientOf(core.Execute("dispatch togglegroup").Effect, First).Bounds;

        var joined = core.Handle(Appear(Second, "two"));

        Assert.Equal([First, Second], Assert.Single(joined.State.Groups));
        Assert.Contains(First, joined.Hide);
        Assert.Contains(joined.State.Hidden, client => client.Window == First);
        Assert.DoesNotContain(joined.State.Hidden, client => client.Window == Second);
        // The hidden member takes the shown member's frame.
        var placement = Assert.Single(joined.Placements, candidate => candidate.Client == new ClientId(Second.Value));
        Assert.Equal(placement.Bounds, Assert.Single(joined.Placements, candidate => candidate.Client == new ClientId(First.Value)).Bounds);
        Assert.Equal(alone, ClientOf(joined, Second).Bounds);
        Assert.Equal(alone, ClientOf(joined, First).Bounds);
        Assert.Equal(["one", "two"], Assert.Single(joined.GroupBars).Tabs.Select(tab => tab.Title));
    }

    [Fact]
    public void ChangeGroupActiveShowsTheNextMemberAndKeepsFocus()
    {
        var core = Grouped();

        var back = core.Execute("dispatch changegroupactive b");

        Assert.Equal("ok", back.Text);
        Assert.Equal(First, back.Effect.Focus);
        Assert.Contains(Second, back.Effect.Hide);
        Assert.Contains(First, back.Effect.Show);
        var shown = Assert.Single(back.Effect.Placements, placement => placement.Client == new ClientId(First.Value));
        Assert.Equal(shown.Bounds, Assert.Single(back.Effect.Placements, placement => placement.Client == new ClientId(Second.Value)).Bounds);

        var forward = core.Execute("dispatch changegroupactive f");
        Assert.Equal(Second, forward.Effect.Focus);

        var indexed = core.Execute("dispatch changegroupactive 1");
        Assert.Equal(First, indexed.Effect.Focus);

        var last = core.Execute("dispatch changegroupactive 0");
        Assert.Equal(Second, last.Effect.Focus);
    }

    [Fact]
    public void GroupDispatchersRejectAWindowThatIsNotGrouped()
    {
        var core = Opened(First);

        Assert.Equal("error: window is not in a group", core.Execute("dispatch changegroupactive f").Text);
        Assert.Equal("error: window not in a group", core.Execute("dispatch moveoutofgroup").Text);

        core.Execute("dispatch togglegroup");
        Assert.Equal("error: only one window in group", core.Execute("dispatch changegroupactive f").Text);
    }

    [Fact]
    public void GroupDirectionsAreWholeWordsNotTheirFirstLetter()
    {
        var core = Opened(First, Second);
        core.Execute("dispatch focuswindow address:0x1");
        core.Execute("dispatch togglegroup");
        core.Execute("dispatch focuswindow address:0x2");

        // A word that is not a direction is refused, and so is its first letter.
        Assert.Equal("error: unsupported direction: leftover", core.Execute("dispatch moveintogroup leftover").Text);
        Assert.Equal("error: unsupported direction: ", core.Execute("dispatch moveintogroup").Text);
        Assert.Equal([First], Assert.Single(core.Execute("dispatch moveintogroup x").Effect.State.Groups));
    }

    [Fact]
    public void MoveOutOfGroupGivesTheWindowItsOwnTile()
    {
        var core = Grouped();

        var left = core.Execute("dispatch moveoutofgroup");

        Assert.Equal("ok", left.Text);
        Assert.Equal(["moveoutofgroup>>2"], Lines(left));
        Assert.Equal([First], Assert.Single(left.Effect.State.Groups));
        Assert.Contains(First, left.Effect.Show);
        Assert.Empty(left.Effect.State.Hidden);
        Assert.Equal(Second, left.Effect.Focus);
        Assert.NotEqual(ClientOf(left.Effect, First).Bounds, ClientOf(left.Effect, Second).Bounds);
    }

    [Fact]
    public void MoveIntoGroupJoinsTheGroupInThatDirection()
    {
        var core = Opened(First, Second);
        core.Execute("dispatch focuswindow address:0x1");
        core.Execute("dispatch togglegroup");
        core.Execute("dispatch focuswindow address:0x2");

        var moved = core.Execute("dispatch moveintogroup l");

        Assert.Equal("ok", moved.Text);
        Assert.Equal(["moveintogroup>>2"], Lines(moved));
        Assert.Equal([First, Second], Assert.Single(moved.Effect.State.Groups));
        Assert.Equal(Second, moved.Effect.Focus);
        Assert.Contains(First, moved.Effect.Hide);
        Assert.Equal(2, moved.Effect.Placements.Count);
        Assert.Single(moved.Effect.Placements.Select(placement => placement.Bounds).Distinct());
    }

    [Fact]
    public void MoveIntoGroupInADirectionLeavesAPlainNeighbourAlone()
    {
        var core = Opened(First, Second);
        core.Execute("dispatch focuswindow address:0x2");

        var moved = core.Execute("dispatch moveintogroup l");

        Assert.Equal("ok", moved.Text);
        Assert.Empty(moved.Effect.State.Groups);
        Assert.Empty(Lines(moved));
    }

    [Fact]
    public void ClosingTheShownMemberShowsTheNextOne()
    {
        var core = Grouped();

        var closed = core.Handle(new WindowClosed(Second));

        Assert.Equal(First, closed.Focus);
        Assert.Contains(First, closed.Show);
        Assert.Equal([First], Assert.Single(closed.State.Groups));
        Assert.Empty(closed.State.Hidden);
    }

    [Fact]
    public void ClosingAHiddenMemberLeavesTheTileAlone()
    {
        var core = Grouped();

        var closed = core.Handle(new WindowClosed(First));

        Assert.Empty(closed.Show);
        Assert.Empty(closed.Hide);
        Assert.Equal([Second], Assert.Single(closed.State.Groups));
        Assert.Contains(closed.Events, stream => stream is ClientClosed { Window.Value: 1 });
    }

    [Fact]
    public void ClosingAHiddenMemberBeforeTheShownOneKeepsTheShownOneShown()
    {
        var core = Grouped();
        core.Handle(Appear(Third, "three"));
        core.Execute("dispatch changegroupactive 2");

        var closed = core.Handle(new WindowClosed(First));

        Assert.Empty(closed.Show);
        Assert.Empty(closed.Hide);
        Assert.Equal([Second, Third], Assert.Single(closed.State.Groups));
        Assert.Equal(Second, Assert.Single(closed.GroupBars).Shown);
    }

    [Fact]
    public void ClosingAMinimizedShownMemberShowsTheNextOne()
    {
        var core = Grouped();
        core.Handle(new WindowMinimized(Second));

        var closed = core.Handle(new WindowClosed(Second));

        Assert.Contains(First, closed.Show);
        Assert.False(ClientOf(closed, First).Minimized);
    }

    [Fact]
    public void FocusingAHiddenMemberWhileTheShownOneIsMinimizedShowsIt()
    {
        var core = Grouped();
        core.Handle(new WindowMinimized(Second));

        var focused = core.Execute("dispatch focuswindow address:0x1").Effect;

        Assert.Contains(First, focused.Show);
        Assert.False(ClientOf(focused, First).Minimized);
        Assert.Equal(First, focused.Focus);
    }

    [Fact]
    public void AHiddenMemberTheUserFocusesIsShown()
    {
        var core = Grouped();

        var focused = core.Handle(new WindowFocused(First, FocusCause.User));

        Assert.Contains(First, focused.Show);
        Assert.Equal(First, Assert.Single(focused.GroupBars).Shown);
        Assert.Equal(First, focused.Focus);
    }

    [Fact]
    public void HiddenMembersAreRecordedForRecovery()
    {
        var core = Grouped();

        var recovered = core.Recovery();

        Assert.True(recovered.Clients.Single(client => client.Window == First).Hidden);
        Assert.False(recovered.Clients.Single(client => client.Window == Second).Hidden);
    }

    [Fact]
    public void ToggleGroupAgainEndsTheGroup()
    {
        var core = Grouped();
        core.Handle(Appear(Third, "three"));

        var ended = core.Execute("dispatch togglegroup");

        Assert.Equal(["togglegroup>>0,1"], Lines(ended));
        Assert.Empty(ended.Effect.State.Groups);
        Assert.Empty(ended.Effect.GroupBars);
        Assert.Empty(ended.Effect.State.Hidden);
        Assert.Equal(3, ended.Effect.Placements.Count);
        Assert.Equal(3, ended.Effect.Placements.Select(placement => placement.Bounds).Distinct().Count());
    }

    [Fact]
    public void FocusWindowShowsAHiddenMember()
    {
        var core = Grouped();

        var focused = core.Execute("dispatch focuswindow address:0x1");

        Assert.Equal(First, focused.Effect.Focus);
        Assert.Contains(First, focused.Effect.Show);
        Assert.Contains(Second, focused.Effect.Hide);
        Assert.Contains(focused.Effect.State.Hidden, client => client.Window == Second);
    }

    [Fact]
    public void MovingTheShownMemberCarriesTheGroup()
    {
        var core = Grouped();

        var moved = core.Execute("dispatch movetoworkspacesilent 2");

        Assert.All(moved.Effect.State.Clients, client => Assert.Equal(new WorkspaceId("2"), client.Workspace));
        Assert.Equal([First, Second], Assert.Single(moved.Effect.State.Groups));
        Assert.Empty(moved.Effect.GroupBars);

        var back = core.Execute("dispatch workspace 2");
        Assert.Contains(Second, back.Effect.Show);
        Assert.DoesNotContain(First, back.Effect.Show);
        Assert.Single(back.Effect.GroupBars);
    }

    [Fact]
    public void FollowingTheGroupToAnotherWorkspaceKeepsItBelowItsBar()
    {
        var core = Grouped();

        var moved = core.Execute("dispatch movetoworkspace 2");

        var bar = Assert.Single(moved.Effect.GroupBars);
        var placement = moved.Effect.Placements.Single(placement => placement.Client == new ClientId(Second.Value));
        Assert.Equal(bar.Bounds.Y + BarHeight, placement.Bounds.Y);
        Assert.Equal(new WorkspaceId("2"), moved.Effect.State.ActiveWorkspace);
    }

    [Fact]
    public void TheTitleRowHeightShapesTheBar()
    {
        var core = Opened("group:groupbar:height = 20\n", First);

        var bar = Assert.Single(core.Execute("dispatch togglegroup").Effect.GroupBars);

        // The gap above, the title row, the indicator and the gap below.
        Assert.Equal(2 + 20 + 3 + 2, bar.Bounds.Height);
    }

    [Fact]
    public void GroupBarBlurIsAnUnsupportedKeyword()
    {
        var document = ConfigParser.Parse("group:groupbar:blur = true\n");

        Assert.Equal([new ConfigWarning(1, "Unsupported keyword 'group:groupbar:blur'.")], document.Warnings);
    }

    [Fact]
    public void TabAtFindsTheTabUnderThePointer()
    {
        var bar = new GroupBar(
            First,
            new Rectangle(100, 100, 302, 21),
            [new GroupTab(First, "a", false), new GroupTab(Second, "b", true)],
            GroupBarStyle.Documented);

        Assert.Equal(150, bar.TabWidth);
        Assert.Equal(0, bar.TabAt(0, 10));
        Assert.Equal(-1, bar.TabAt(151, 10));
        Assert.Equal(1, bar.TabAt(152, 10));
        Assert.Equal(-1, bar.TabAt(400, 10));
        Assert.Equal(new Rectangle(152, 16, 150, 3), bar.IndicatorBounds(1));
        Assert.Equal(new Rectangle(152, 2, 150, 14), bar.TitleBounds(1));
    }

    [Fact]
    public void ClientsQueryListsTheGroup()
    {
        var core = Grouped();

        using var clients = JsonDocument.Parse(core.Execute("j/clients").Text);

        foreach (var client in clients.RootElement.EnumerateArray())
            Assert.Equal(["0x1", "0x2"], client.GetProperty("grouped").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void GroupKeywordsAreHonored()
    {
        var document = ConfigParser.Parse("""
            group:groupbar:font_size = 10
            group:groupbar:col.active = rgba(ff0000ff)
            """);

        Assert.Empty(document.Warnings);
        var options = GroupOptions.From(document);
        Assert.Equal(10, options.Bar.FontSize);
        Assert.Equal(new BorderColor(0xFF, 0xFF, 0x00, 0x00), options.Bar.Active);
    }

    [Theory]
    [InlineData("dwindle")]
    [InlineData("scrolling")]
    public void SubstituteKeepsTheSlot(string name)
    {
        var layout = LayoutFactory.Fallback(name).Layout;
        layout.Add(First, Area, null, false, default, WorkspaceId.Initial, "one", "one");
        layout.Add(Second, Area, null, false, default, WorkspaceId.Initial, "two", "two");
        Assert.True(layout.TryBounds(Second, out var slot));

        var handed = layout.Substitute(Second, Third, new Taken(false, false, default, "three", "three"), out var outgoing);

        Assert.True(handed);
        Assert.Equal("two", outgoing.Class);
        Assert.Equal([First, Third], layout.Clients.Select(client => client.Window));
        Assert.Equal("three", layout.Clients[1].Class);
        Assert.True(layout.TryBounds(Third, out var kept));
        Assert.Equal(slot, kept);
        Assert.False(layout.TryBounds(Second, out _));
        Assert.False(layout.Substitute(First, Third, default, out _));
    }

    [Fact]
    public void AMiddleClickOnATabClosesItsWindow()
    {
        var core = Grouped();

        Assert.Equal(Second, core.Handle(new GroupTabClosed(Second)).Close);
    }

    [Fact]
    public void TheWheelOverTheBarShowsTheNextMemberAndMovesTheFocusOnlyFromIt()
    {
        var focused = Grouped();

        var followed = focused.Handle(new GroupBarScrolled(Second, Forward: true));

        Assert.Equal(First, Assert.Single(followed.GroupBars).Shown);
        Assert.Equal(First, followed.Focus);

        var core = Opened(First, Second, Third);
        core.Execute("dispatch focuswindow address:0x1");
        core.Execute("dispatch togglegroup");
        core.Execute("dispatch focuswindow address:0x2");
        core.Execute("dispatch moveintogroup l");
        core.Execute("dispatch focuswindow address:0x3");

        var shown = core.Handle(new GroupBarScrolled(Second, Forward: false));

        Assert.Equal(First, Assert.Single(shown.GroupBars).Shown);
        Assert.Null(shown.Focus);
        Assert.Equal("0x3", core.Execute("j/activewindow").Text.Split("\"address\":\"")[1][..3]);
        Assert.Empty(core.Handle(new GroupBarScrolled(Third, Forward: true)).Placements);
    }

    private static WindowManager Grouped(string config = "")
    {
        var core = Opened(config, First);
        core.Execute("dispatch togglegroup");
        core.Handle(Appear(Second, "two"));
        return core;
    }

    private static WindowManager Opened(params WindowId[] windows) => Opened("", windows);

    private static WindowManager Opened(string config, params WindowId[] windows)
    {
        var core = new WindowManager(ConfigParser.Parse(config));
        string[] titles = ["", "one", "two", "three"];
        foreach (var window in windows)
            core.Handle(Appear(window, titles[window.Value]));

        return core;
    }

    private static WindowAppeared Appear(WindowId window, string title) =>
        new(window, Area) { Traits = WindowTraits.Normal with { Title = title } };

    private static Client ClientOf(Outcome outcome, WindowId window) =>
        outcome.State.Clients.Single(client => client.Window == window);

    private static Decoration Decoration(Outcome outcome, WindowId window) =>
        outcome.Decorations.Single(decoration => decoration.Client == new ClientId(window.Value));

    private static IReadOnlyList<string> Lines(CommandReply reply) =>
        CommandProtocol.FormatStream(reply.Effect.Events, reply.Effect.State)
            .Where(line => !line.StartsWith("activewindow", StringComparison.Ordinal))
            .ToArray();
}
