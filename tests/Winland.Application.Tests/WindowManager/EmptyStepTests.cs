using System.Reflection;

namespace Winland.Application.Tests;

[TestClass]
public class EmptyStepTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    private static readonly ClientId One = new(1);

    private static readonly WindowId OneWindow = new(1);

    private static readonly Rectangle Tile = new(0, 0, 100, 100);

    private static readonly Outcome Applied = new([], new State([]))
    {
        Decorations = [new Decoration(One, new BorderColor(255, 0, 0, 0), CornerPreference.Round)],
        GroupBars = [new GroupBar(OneWindow, Tile, [new GroupTab(OneWindow, "a", true)], GroupBarStyle.Documented)],
        ConfigNotice = new ConfigNotice("title", "text", null, 1),
        Tray = new TrayIcon("1"),
        Cursor = new CursorBehavior(true),
        ResizeCursor = "right_side",
    };

    private static readonly Dictionary<string, (Func<Outcome, Outcome> Change, bool StaysEmpty)> Rules = new()
    {
        [nameof(Outcome.Placements)] = (o => o with { Placements = [new Placement(One, Tile)] }, false),
        [nameof(Outcome.State)] = (o => o with { State = new State([]) { ActiveWorkspace = new WorkspaceId("2") } }, true),
        [nameof(Outcome.Consumed)] = (o => o with { Consumed = true }, false),
        [nameof(Outcome.Traits)] = (o => o with { Traits = DispatcherTraits.None }, false),
        [nameof(Outcome.Focus)] = (o => o with { Focus = OneWindow }, false),
        [nameof(Outcome.Closes)] = (o => o with { Closes = [OneWindow] }, false),
        [nameof(Outcome.Close)] = (o => o with { Close = OneWindow }, false),
        [nameof(Outcome.Show)] = (o => o with { Show = [OneWindow] }, false),
        [nameof(Outcome.Hide)] = (o => o with { Hide = [OneWindow] }, false),
        [nameof(Outcome.ShowMinimized)] = (o => o with { ShowMinimized = [OneWindow] }, false),
        [nameof(Outcome.Launches)] = (o => o with { Launches = [new Launch("notepad", null)] }, false),
        [nameof(Outcome.Launch)] = (o => o with { Launch = new Launch("notepad", null) }, false),
        [nameof(Outcome.Events)] = (o => o with { Events = [new ClientClosed(OneWindow)] }, false),
        [nameof(Outcome.Decorations)] = (o => o with { Decorations = [new Decoration(One, new BorderColor(255, 1, 1, 1), CornerPreference.Round)] }, false),
        [nameof(Outcome.Motions)] = (o => o with { Motions = [new Motion(OneWindow, Tile)] }, false),
        [nameof(Outcome.Live)] = (o => o with { Live = true }, false),
        [nameof(Outcome.ResetDecorations)] = (o => o with { ResetDecorations = [OneWindow] }, false),
        [nameof(Outcome.GroupBars)] = (o => o with { GroupBars = [] }, false),
        [nameof(Outcome.ConfigNotice)] = (o => o with { ConfigNotice = null }, false),
        [nameof(Outcome.Tray)] = (o => o with { Tray = new TrayIcon("2") }, false),
        [nameof(Outcome.Warp)] = (o => o with { Warp = new WarpTo(new Point(5, 5)) }, false),
        [nameof(Outcome.Cursor)] = (o => o with { Cursor = new CursorBehavior(false) }, false),
        [nameof(Outcome.ResizeCursor)] = (o => o with { ResizeCursor = null }, false),
        [nameof(Outcome.Error)] = (o => o with { Error = "error: no such workspace" }, false),
        [nameof(Outcome.Exit)] = (o => o with { Exit = true }, false),
        [nameof(Outcome.ZOrder)] = (o => o with { ZOrder = [new ZOrderRequest(OneWindow, Top: true)] }, false),
        [nameof(Outcome.IsEmpty)] = (o => o with { IsEmpty = true }, true),
    };

    [TestMethod]
    public void EveryPropertyHasARuleInIsEmptyAfter()
    {
        var properties = typeof(Outcome)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name);

        Assert.AreSequenceEqual(Rules.Keys.Order(StringComparer.Ordinal), properties.Order(StringComparer.Ordinal));
    }

    [TestMethod]
    public void AnOutcomeLikeTheLastAppliedOneIsEmpty()
    {
        Assert.IsTrue((Applied with { }).IsEmptyAfter(Applied));
    }

    [TestMethod]
    public void NothingIsEmptyBeforeAnythingWasApplied()
    {
        Assert.IsFalse((Applied with { }).IsEmptyAfter(null));
    }

    [TestMethod]
    public void EachPropertyIsSeenByTheRuleOrLeftOutOnPurpose()
    {
        foreach (var (name, rule) in Rules)
            Assert.IsTrue(rule.Change(Applied with { }).IsEmptyAfter(Applied) == rule.StaysEmpty, name);
    }

    [TestMethod]
    public void ThenIsEmptyOnlyWhenBothAre()
    {
        var empty = Outcome.Empty(new State([]));
        var busy = new Outcome([], new State([])) { Closes = [OneWindow] };

        Assert.IsTrue(empty.Then(empty).IsEmpty);
        Assert.IsFalse(empty.Then(busy).IsEmpty);
        Assert.IsFalse(busy.Then(empty).IsEmpty);
    }

    [TestMethod]
    public void TheFirstStepIsNotEmpty()
    {
        var core = new WindowManager(ConfigParser.Parse(FlatConfig("")));

        Assert.IsFalse(core.Handle(new PointerMoved(new Point(10, 10)) { TimeMs = 0 }).IsEmpty);
    }

    [TestMethod]
    public void AMoveOverTheSameWindowChangesNothing()
    {
        var (core, laid) = Tiled();
        Move(core, Center(laid, 2), 1_000);

        var moved = Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 10 }, 1_100);

        Assert.IsTrue(moved.IsEmpty);
        Assert.IsEmpty(moved.Placements);
        Assert.AreEqual(2, moved.State.Clients.Count);
    }

    [TestMethod]
    public void AMoveThatChangesNothingRunsNoStep()
    {
        var (core, laid) = Tiled();
        Outcome? decided = null;
        core.Decided = outcome => decided = outcome;
        Move(core, Center(laid, 2), 1_000);

        var moved = Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 10 }, 1_100);

        Assert.IsTrue(moved.IsEmpty);
        Assert.AreSame((decided ?? laid).State, moved.State);
    }

    [TestMethod]
    public void AMoveWithoutAStepStillMovesThePointerATileReturnsTo()
    {
        var core = new WindowManager(ConfigParser.Parse(FlatConfig("input:follow_mouse = 0\n")));
        core.Handle(new WindowAppeared(OneWindow, Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        core.Handle(new Dispatch("togglefloating"));
        Move(core, new Point(1700, 1000), 1_000);

        var moved = Move(core, new Point(200, 1000), 1_100);
        var tiled = core.Handle(new Dispatch("togglefloating"));

        Assert.IsTrue(moved.IsEmpty);
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), Client(tiled, 1).Bounds);
    }

    [TestMethod]
    public void AMoveThatChangesTheFocusIsNotEmpty()
    {
        var (core, laid) = Tiled();
        Move(core, Center(laid, 2), 1_000);
        Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 10 }, 1_050);

        var followed = Move(core, Center(laid, 1), 1_100);

        Assert.IsFalse(followed.IsEmpty);
        Assert.AreEqual(OneWindow, followed.Focus);
    }

    [TestMethod]
    public void AStepThatChangesTheDecorationsIsNotEmpty()
    {
        var (core, laid) = Tiled();
        Move(core, Center(laid, 2), 1_000);
        Assert.IsTrue(Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 10 }, 1_050).IsEmpty);

        var focused = core.Handle(new WindowFocused(OneWindow, FocusCause.User));

        Assert.IsFalse(focused.IsEmpty);
        Assert.IsNotEmpty(focused.Decorations);
    }

    [TestMethod]
    [DataRow("clients")]
    [DataRow("j/clients")]
    [DataRow("workspaces")]
    [DataRow("monitors")]
    [DataRow("monitors all")]
    [DataRow("activewindow")]
    [DataRow("activeworkspace")]
    [DataRow("binds")]
    [DataRow("version")]
    [DataRow("configerrors")]
    [DataRow("files")]
    public void AQueryAnswersAndChangesNothing(string query)
    {
        var (core, laid) = Tiled();
        Move(core, Center(laid, 2), 1_000);
        var before = core.Execute("j/clients").Text;

        var reply = core.Execute(query);

        Assert.IsNotEmpty(reply.Text);
        Assert.IsTrue(reply.Effect.IsEmpty);
        Assert.AreEqual(2, reply.Effect.State.Clients.Count);
        Assert.AreEqual(before, core.Execute("j/clients").Text);
        Assert.IsTrue(Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 10 }, 1_100).IsEmpty);
    }

    [TestMethod]
    public void ACommandThatChangesTheStateIsNotAQuery()
    {
        var (core, _) = Tiled();

        var reply = core.Execute("dispatch togglefloating");

        Assert.IsFalse(reply.Effect.IsEmpty);
    }

    [TestMethod]
    public void TheFirstStepAfterAConfigReloadIsNotEmpty()
    {
        var (core, laid) = Tiled();
        var document = ConfigParser.Parse(FlatConfig(""));
        core.ReadConfig = () => new ConfigRead(document);
        Move(core, Center(laid, 2), 1_000);
        Assert.IsTrue(Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 10 }, 1_050).IsEmpty);

        var reloaded = core.Execute("reload");
        var after = Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 20 }, 1_100);
        var then = Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 30 }, 1_150);

        Assert.IsFalse(reloaded.Effect.IsEmpty);
        Assert.IsFalse(after.IsEmpty);
        Assert.IsNotEmpty(after.Decorations);
        Assert.IsTrue(then.IsEmpty);
    }

    [TestMethod]
    public void TheFirstStepAfterAConfigIsConfiguredIsNotEmpty()
    {
        var (core, laid) = Tiled();
        Move(core, Center(laid, 2), 1_000);
        Assert.IsTrue(Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 10 }, 1_050).IsEmpty);

        var configured = core.Handle(new Configure(ConfigParser.Parse(FlatConfig(""))));
        var after = Move(core, Center(laid, 2) with { X = Center(laid, 2).X + 20 }, 1_100);

        Assert.IsFalse(configured.IsEmpty);
        Assert.IsFalse(after.IsEmpty);
    }

    private static (WindowManager WindowManager, Outcome Laid) Tiled()
    {
        var core = new WindowManager(ConfigParser.Parse(FlatConfig("")));
        core.Handle(new WindowAppeared(OneWindow, Area));
        return (core, core.Handle(new WindowAppeared(new WindowId(2), Area)));
    }

    private static Outcome Move(WindowManager core, Point point, long time) =>
        core.Handle(new PointerMoved(point) { TimeMs = time });

    private static Point Center(Outcome outcome, long window)
    {
        var bounds = Client(outcome, window).Bounds;
        return new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }
}
