using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class GroupTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
    private static readonly WindowId First = new(1);
    private static readonly WindowId Second = new(2);
    private static readonly WindowId Third = new(3);

    private const int BarHeight = 21;

    [TestMethod]
    public void ABackgroundGroupMemberLeavesTheShownMemberFocusedWhenFollowingIsOff()
    {
        var core = Opened("focus_follows_mouse = false\n", First);
        core.Execute("group");

        var opened = core.Handle(Appear(Second, "two"));

        Assert.AreEqual(First, Assert.ContainsSingle(opened.GroupBars).Shown);
        Assert.Contains(Second, opened.State.Hidden.Select(client => client.Window));
        Assert.Contains("\"address\":\"0x1\"", core.Execute("j/get window").Text, StringComparison.Ordinal);
        var clicked = core.Handle(new WindowFocused(Second, FocusCause.User));
        Assert.AreEqual(Second, clicked.Focus);
    }

    [TestMethod]
    public void GroupBordersKeepTheirFixedColorsWhenOtherColorsChange()
    {
        var core = Opened("""
            border_color = 0xffff0000
            border_color_inactive = 0xff0000ff
            tab_color = 0xff00ff00
            tab_color_inactive = 0xffffffff
            """, First);
        var grouped = core.Execute("group").Effect;
        Assert.AreEqual(new BorderColor(0x66, 0xFF, 0xFF, 0), Decoration(grouped, First).Border);

        var otherWorkspace = core.Handle(new WindowAppeared(Second, Area)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
        });

        Assert.AreEqual(new BorderColor(0x66, 0x77, 0x77, 0), Decoration(otherWorkspace, First).Border);
        Assert.AreEqual(new BorderColor(0xFF, 0xFF, 0, 0), Decoration(otherWorkspace, Second).Border);
    }

    [TestMethod]
    public void GroupMakesAGroupOfOneWithABar()
    {
        var core = Opened(First);

        var made = core.Execute("group");

        Assert.AreEqual("ok", made.Text);
        Assert.AreSequenceEqual(["togglegroup>>1,1"], Lines(made));
        Assert.AreSequenceEqual([First], Assert.ContainsSingle(made.Effect.State.Groups));
        var bar = Assert.ContainsSingle(made.Effect.GroupBars);
        Assert.AreEqual(First, bar.Shown);
        Assert.AreEqual(new GroupTab(First, "one", true), Assert.ContainsSingle(bar.Tabs));
        var tile = ClientOf(made.Effect, First).Bounds;
        Assert.AreEqual(new Rectangle(tile.X, tile.Y, tile.Width, BarHeight), bar.Bounds);
        Assert.AreEqual(
            new Rectangle(tile.X, tile.Y + BarHeight, tile.Width, tile.Height - BarHeight),
            Assert.ContainsSingle(made.Effect.Placements).Bounds);
    }

    [TestMethod]
    public void AGroupIsOneTileWithSeveralClients()
    {
        var core = Opened(First);
        var alone = ClientOf(core.Execute("group").Effect, First).Bounds;

        var joined = core.Handle(Appear(Second, "two"));

        Assert.AreSequenceEqual([First, Second], Assert.ContainsSingle(joined.State.Groups));
        Assert.Contains(First, joined.Hide);
        Assert.Contains(client => client.Window == First, joined.State.Hidden);
        Assert.DoesNotContain(client => client.Window == Second, joined.State.Hidden);
        var placement = Assert.ContainsSingle(candidate => candidate.Client == new ClientId(Second.Value), joined.Placements);
        Assert.AreEqual(placement.Bounds, Assert.ContainsSingle(candidate => candidate.Client == new ClientId(First.Value), joined.Placements).Bounds);
        Assert.AreEqual(alone, ClientOf(joined, Second).Bounds);
        Assert.AreEqual(alone, ClientOf(joined, First).Bounds);
        Assert.AreSequenceEqual(["one", "two"], Assert.ContainsSingle(joined.GroupBars).Tabs.Select(tab => tab.Title));
    }

    [TestMethod]
    public void TabShowsTheNextMemberAndKeepsFocus()
    {
        var core = Grouped();

        var back = core.Execute("tab prev");

        Assert.AreEqual("ok", back.Text);
        Assert.AreEqual(First, back.Effect.Focus);
        Assert.Contains(Second, back.Effect.Hide);
        Assert.Contains(First, back.Effect.Show);
        var shown = Assert.ContainsSingle(placement => placement.Client == new ClientId(First.Value), back.Effect.Placements);
        Assert.AreEqual(shown.Bounds, Assert.ContainsSingle(placement => placement.Client == new ClientId(Second.Value), back.Effect.Placements).Bounds);

        var forward = core.Execute("tab next");
        Assert.AreEqual(Second, forward.Effect.Focus);

        var indexed = core.Execute("tab 1");
        Assert.AreEqual(First, indexed.Effect.Focus);

        var last = core.Execute("tab 9");
        Assert.AreEqual(Second, last.Effect.Focus);

        Assert.AreEqual("error: tab takes next, prev or a number", core.Execute("tab 0").Text);
    }

    [TestMethod]
    public void GroupDispatchersRejectAWindowThatIsNotGrouped()
    {
        var core = Opened(First);

        Assert.AreEqual("error: window is not in a group", core.Execute("tab next").Text);
        Assert.AreEqual("error: window not in a group", core.Execute("leave-group").Text);

        core.Execute("group");
        Assert.AreEqual("error: only one window in group", core.Execute("tab next").Text);
    }

    [TestMethod]
    public void GroupDirectionsAreWholeWordsNotTheirFirstLetter()
    {
        var core = Opened(First, Second);
        core.Execute("focus address:0x1");
        core.Execute("group");
        core.Execute("focus address:0x2");

        Assert.AreEqual("error: unsupported direction: leftover", core.Execute("join leftover").Text);
        Assert.AreEqual("error: unsupported direction: ", core.Execute("join").Text);
        Assert.AreSequenceEqual([First], Assert.ContainsSingle(core.Execute("join x").Effect.State.Groups));
    }

    [TestMethod]
    public void LeaveGroupGivesTheWindowItsOwnTile()
    {
        var core = Grouped();

        var left = core.Execute("leave-group");

        Assert.AreEqual("ok", left.Text);
        Assert.AreSequenceEqual(["moveoutofgroup>>2"], Lines(left));
        Assert.AreSequenceEqual([First], Assert.ContainsSingle(left.Effect.State.Groups));
        Assert.Contains(First, left.Effect.Show);
        Assert.IsEmpty(left.Effect.State.Hidden);
        Assert.AreEqual(Second, left.Effect.Focus);
        Assert.AreNotEqual(ClientOf(left.Effect, First).Bounds, ClientOf(left.Effect, Second).Bounds);
    }

    [TestMethod]
    public void JoinJoinsTheGroupInThatDirection()
    {
        var core = Opened(First, Second);
        core.Execute("focus address:0x1");
        core.Execute("group");
        core.Execute("focus address:0x2");

        var moved = core.Execute("join l");

        Assert.AreEqual("ok", moved.Text);
        Assert.AreSequenceEqual(["moveintogroup>>2"], Lines(moved));
        Assert.AreSequenceEqual([First, Second], Assert.ContainsSingle(moved.Effect.State.Groups));
        Assert.AreEqual(Second, moved.Effect.Focus);
        Assert.Contains(First, moved.Effect.Hide);
        Assert.AreEqual(2, moved.Effect.Placements.Count);
        Assert.ContainsSingle(moved.Effect.Placements.Select(placement => placement.Bounds).Distinct());
    }

    [TestMethod]
    public void JoinInADirectionLeavesAPlainNeighbourAlone()
    {
        var core = Opened(First, Second);
        core.Execute("focus address:0x2");

        var moved = core.Execute("join l");

        Assert.AreEqual("ok", moved.Text);
        Assert.IsEmpty(moved.Effect.State.Groups);
        Assert.IsEmpty(Lines(moved));
    }

    [TestMethod]
    public void ClosingTheShownMemberShowsTheNextOne()
    {
        var core = Grouped();

        var closed = core.Handle(new WindowClosed(Second));

        Assert.AreEqual(First, closed.Focus);
        Assert.Contains(First, closed.Show);
        Assert.AreSequenceEqual([First], Assert.ContainsSingle(closed.State.Groups));
        Assert.IsEmpty(closed.State.Hidden);
    }

    [TestMethod]
    public void ClosingAHiddenMemberLeavesTheTileAlone()
    {
        var core = Grouped();

        var closed = core.Handle(new WindowClosed(First));

        Assert.IsEmpty(closed.Show);
        Assert.IsEmpty(closed.Hide);
        Assert.AreSequenceEqual([Second], Assert.ContainsSingle(closed.State.Groups));
        Assert.Contains(stream => stream is ClientClosed { Window.Value: 1 }, closed.Events);
    }

    [TestMethod]
    public void ClosingAHiddenMemberBeforeTheShownOneKeepsTheShownOneShown()
    {
        var core = Grouped();
        core.Handle(Appear(Third, "three"));
        core.Execute("tab 2");

        var closed = core.Handle(new WindowClosed(First));

        Assert.IsEmpty(closed.Show);
        Assert.IsEmpty(closed.Hide);
        Assert.AreSequenceEqual([Second, Third], Assert.ContainsSingle(closed.State.Groups));
        Assert.AreEqual(Second, Assert.ContainsSingle(closed.GroupBars).Shown);
    }

    [TestMethod]
    public void ClosingAMinimizedShownMemberShowsTheNextOne()
    {
        var core = Grouped();
        core.Handle(new WindowMinimized(Second));

        var closed = core.Handle(new WindowClosed(Second));

        Assert.Contains(First, closed.Show);
        Assert.IsFalse(ClientOf(closed, First).Minimized);
    }

    [TestMethod]
    public void FocusingAHiddenMemberWhileTheShownOneIsMinimizedShowsIt()
    {
        var core = Grouped();
        core.Handle(new WindowMinimized(Second));

        var focused = core.Execute("focus address:0x1").Effect;

        Assert.Contains(First, focused.Show);
        Assert.IsFalse(ClientOf(focused, First).Minimized);
        Assert.AreEqual(First, focused.Focus);
    }

    [TestMethod]
    public void AHiddenMemberTheUserFocusesIsShown()
    {
        var core = Grouped();

        var focused = core.Handle(new WindowFocused(First, FocusCause.User));

        Assert.Contains(First, focused.Show);
        Assert.AreEqual(First, Assert.ContainsSingle(focused.GroupBars).Shown);
        Assert.AreEqual(First, focused.Focus);
    }

    [TestMethod]
    public void HiddenMembersAreRecordedForRecovery()
    {
        var core = Grouped();

        var recovered = core.Recovery();

        Assert.IsTrue(recovered.Clients.Single(client => client.Window == First).Hidden);
        Assert.IsFalse(recovered.Clients.Single(client => client.Window == Second).Hidden);
    }

    [TestMethod]
    public void GroupAgainEndsTheGroup()
    {
        var core = Grouped();
        core.Handle(Appear(Third, "three"));

        var ended = core.Execute("group");

        Assert.AreSequenceEqual(["togglegroup>>0,1"], Lines(ended));
        Assert.IsEmpty(ended.Effect.State.Groups);
        Assert.IsEmpty(ended.Effect.GroupBars);
        Assert.IsEmpty(ended.Effect.State.Hidden);
        Assert.AreEqual(3, ended.Effect.Placements.Count);
        Assert.AreEqual(3, ended.Effect.Placements.Select(placement => placement.Bounds).Distinct().Count());
    }

    [TestMethod]
    public void FocusSelectorShowsAHiddenMember()
    {
        var core = Grouped();

        var focused = core.Execute("focus address:0x1");

        Assert.AreEqual(First, focused.Effect.Focus);
        Assert.Contains(First, focused.Effect.Show);
        Assert.Contains(Second, focused.Effect.Hide);
        Assert.Contains(client => client.Window == Second, focused.Effect.State.Hidden);
    }

    [TestMethod]
    public void MovingTheShownMemberCarriesTheGroup()
    {
        var core = Grouped();

        var moved = core.Execute("move 2 stay");

        foreach (var client in moved.Effect.State.Clients)
{
    Assert.AreEqual(new WorkspaceId("2"), client.Workspace);
}
        Assert.AreSequenceEqual([First, Second], Assert.ContainsSingle(moved.Effect.State.Groups));
        Assert.IsEmpty(moved.Effect.GroupBars);

        var back = core.Execute("workspace 2");
        Assert.Contains(Second, back.Effect.Show);
        Assert.DoesNotContain(First, back.Effect.Show);
        Assert.ContainsSingle(back.Effect.GroupBars);
    }

    [TestMethod]
    public void FollowingTheGroupToAnotherWorkspaceKeepsItBelowItsBar()
    {
        var core = Grouped();

        var moved = core.Execute("move 2");

        var bar = Assert.ContainsSingle(moved.Effect.GroupBars);
        var placement = moved.Effect.Placements.Single(placement => placement.Client == new ClientId(Second.Value));
        Assert.AreEqual(bar.Bounds.Y + BarHeight, placement.Bounds.Y);
        Assert.AreEqual(new WorkspaceId("2"), moved.Effect.State.ActiveWorkspace);
    }

    [TestMethod]
    public void TheTitleRowHeightShapesTheBar()
    {
        var core = Opened("tab_height = 20\n", First);

        var bar = Assert.ContainsSingle(core.Execute("group").Effect.GroupBars);

        Assert.AreEqual(2 + 20 + 3 + 2, bar.Bounds.Height);
    }

    [TestMethod]
    public void GroupBarBlurIsAnUnsupportedKeyword()
    {
        var document = ConfigParser.Parse("group:groupbar:blur = true\n");

        Assert.AreSequenceEqual([new ConfigWarning(1, "Unsupported keyword 'group:groupbar:blur'.")], document.Warnings);
    }

    [TestMethod]
    public void TabAtFindsTheTabUnderThePointer()
    {
        var bar = new GroupBar(
            First,
            new Rectangle(100, 100, 302, 21),
            [new GroupTab(First, "a", false), new GroupTab(Second, "b", true)],
            GroupBarStyle.Documented);

        Assert.AreEqual(150, bar.TabWidth);
        Assert.AreEqual(0, bar.TabAt(0, 10));
        Assert.AreEqual(-1, bar.TabAt(151, 10));
        Assert.AreEqual(1, bar.TabAt(152, 10));
        Assert.AreEqual(-1, bar.TabAt(400, 10));
        Assert.AreEqual(new Rectangle(152, 16, 150, 3), bar.IndicatorBounds(1));
        Assert.AreEqual(new Rectangle(152, 2, 150, 14), bar.TitleBounds(1));
    }

    [TestMethod]
    public void ClientsQueryListsTheGroup()
    {
        var core = Grouped();

        using var clients = JsonDocument.Parse(core.Execute("j/get windows").Text);

        foreach (var client in clients.RootElement.EnumerateArray())
            Assert.AreSequenceEqual(["0x1", "0x2"], client.GetProperty("grouped").EnumerateArray().Select(item => item.GetString()));
    }

    [TestMethod]
    public void GroupKeywordsAreHonored()
    {
        var document = ConfigParser.Parse("""
            tab_font_size = 10
            tab_color = rgba(ff0000ff)
            """);

        Assert.IsEmpty(document.Warnings);
        var options = GroupOptions.From(document);
        Assert.AreEqual(10, options.Bar.FontSize);
        Assert.AreEqual(new BorderColor(0xFF, 0xFF, 0x00, 0x00), options.Bar.Active);
    }

    [TestMethod]
    [DataRow("dwindle")]
    [DataRow("scrolling")]
    public void SubstituteKeepsTheSlot(string name)
    {
        var layout = LayoutFactory.Fallback(name);
        layout.Add(First, Area, null, false, default, WorkspaceId.Initial, "one", "one");
        layout.Add(Second, Area, null, false, default, WorkspaceId.Initial, "two", "two");
        Assert.IsTrue(layout.TryBounds(Second, out var slot));

        var handed = layout.Substitute(Second, Third, new Taken(false, false, default, "three", "three"), out var outgoing);

        Assert.IsTrue(handed);
        Assert.AreEqual("two", outgoing.Class);
        Assert.AreSequenceEqual([First, Third], layout.Clients.Select(client => client.Window));
        Assert.AreEqual("three", layout.Clients[1].Class);
        Assert.IsTrue(layout.TryBounds(Third, out var kept));
        Assert.AreEqual(slot, kept);
        Assert.IsFalse(layout.TryBounds(Second, out _));
        Assert.IsFalse(layout.Substitute(First, Third, default, out _));
    }

    [TestMethod]
    public void AMiddleClickOnATabClosesItsWindow()
    {
        var core = Grouped();

        Assert.AreEqual(Second, core.Handle(new GroupTabClosed(Second)).Close);
    }

    [TestMethod]
    public void TheWheelOverTheBarShowsTheNextMemberAndMovesTheFocusOnlyFromIt()
    {
        var focused = Grouped();

        var followed = focused.Handle(new GroupBarScrolled(Second, Forward: true));

        Assert.AreEqual(First, Assert.ContainsSingle(followed.GroupBars).Shown);
        Assert.AreEqual(First, followed.Focus);

        var core = Opened(First, Second, Third);
        core.Execute("focus address:0x1");
        core.Execute("group");
        core.Execute("focus address:0x2");
        core.Execute("join l");
        core.Execute("focus address:0x3");

        var shown = core.Handle(new GroupBarScrolled(Second, Forward: false));

        Assert.AreEqual(First, Assert.ContainsSingle(shown.GroupBars).Shown);
        Assert.IsNull(shown.Focus);
        Assert.AreEqual("0x3", core.Execute("j/get window").Text.Split("\"address\":\"")[1][..3]);
        Assert.IsEmpty(core.Handle(new GroupBarScrolled(Third, Forward: true)).Placements);
    }

    private static WindowManager Grouped(string config = "")
    {
        var core = Opened(config, First);
        core.Execute("group");
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
