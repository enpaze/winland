namespace Winland.Application.Tests;

[TestClass]
public class ReloadTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void ALoadedConfigAppliesWindowRulesLayoutOptionsAndTheLayout()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 10
            general:border_size = 0
            general:layout = scrolling
            windowrule = float, class:kitty
            """)));

        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var opened = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });

        var third = core.Handle(new WindowAppeared(new WindowId(3), Area));

        Assert.IsTrue(Client(opened, 2).Floating);
        Assert.AreEqual("scrolling", Assert.ContainsSingle(third.State.Workspaces).TiledLayout);
        Assert.AreEqual(10, Client(third, 3).Bounds.Y);
        Assert.AreEqual(1060, Client(third, 3).Bounds.Height);
    }

    [TestMethod]
    public void ReloadSwapsSettingsKeepsClientsAndLaysOut()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 0
            general:border_size = 0
            bind = SUPER, Q, killactive
            """)));
        core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var reloaded = core.Handle(new Configure(ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 10
            general:border_size = 0
            bind = SUPER, W, killactive
            windowrule = float, class:kitty
            """)));

        Assert.AreEqual(new WorkspaceId("2"), reloaded.State.ActiveWorkspace);
        Assert.AreEqual(2, reloaded.State.Clients.Count);
        Assert.AreEqual(2, reloaded.State.Workspaces.Count);
        Assert.IsFalse(Client(reloaded, 1).Floating);
        Assert.AreEqual(new Rectangle(10, 10, 1900, 1060), BoundsOf(reloaded, 2));
        Assert.AreSequenceEqual(
            ["configreloaded>>"],
            Lines(reloaded));

        core.Handle(new KeyPressed("Super_L"));
        Assert.IsNull(core.Handle(new KeyPressed("Q")).Close);
        Assert.AreEqual(new WindowId(2), core.Handle(new KeyPressed("W")).Close);

        var opened = core.Handle(new WindowAppeared(new WindowId(3), Area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });
        Assert.IsTrue(Client(opened, 3).Floating);
        Assert.IsFalse(Client(opened, 1).Floating);
    }

    [TestMethod]
    public void WarningsStillApplyAndAreReportedBeforeReload()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new Configure(ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 10
            general:border_size = 0
            blur = yes
            """)));

        Assert.AreEqual(new Rectangle(10, 10, 1900, 1060), BoundsOf(outcome, 1));
        Assert.AreSequenceEqual(
            [
                "warning>>4,,Unsupported keyword 'blur'.",
                "configreloaded>>",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void AMissingSourceStillAppliesTheRest()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var document = ConfigParser.Parse("""
            source = missing.conf
            general:gaps_in = 0
            general:gaps_out = 10
            general:border_size = 0
            """);
        core.ReadConfig = () => new ConfigRead(document);

        var reply = core.Execute("reload");

        Assert.AreEqual("ok", reply.Text);
        Assert.AreEqual(new Rectangle(10, 10, 1900, 1060), BoundsOf(reply.Effect, 1));
        Assert.AreSequenceEqual(
            [
                "warning>>1,,Could not read source 'missing.conf'.",
                "configreloaded>>",
            ],
            Lines(reply));
    }

    [TestMethod]
    public void AnUnreadableConfigKeepsTheWorkingConfigAndWarns()
    {
        var core = new WindowManager();
        var called = false;
        core.Handle(new Configure(ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 0
            general:border_size = 0
            bind = SUPER, Q, killactive
            """)));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.ReadConfig = () => new ConfigRead(
            null,
            new ConfigWarning(0, "Could not read config 'winland.conf'.", "winland.conf"));
        core.AfterReload = _ => called = true;

        var reply = core.Execute("reload");

        Assert.IsFalse(called);
        Assert.AreEqual("error: Could not read config 'winland.conf'.", reply.Text);
        Assert.AreSequenceEqual(["warning>>0,winland.conf,Could not read config 'winland.conf'."], Lines(reply));
        Assert.ContainsSingle(reply.Effect.State.Clients);

        core.Handle(new KeyPressed("Super_L"));
        Assert.AreEqual(new WindowId(1), core.Handle(new KeyPressed("Q")).Close);
    }

    [TestMethod]
    public void ReloadCommandAppliesTheDocumentAndNotifiesOnce()
    {
        var core = new WindowManager();
        ConfigDocument? seen = null;
        var calls = 0;
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var document = ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 10
            general:border_size = 0
            exec-once = once
            """);
        core.ReadConfig = () => new ConfigRead(document);
        core.AfterReload = loaded =>
        {
            calls++;
            seen = loaded;
        };

        var reply = core.Execute("reload");

        Assert.AreEqual("ok", reply.Text);
        Assert.AreEqual(1, calls);
        Assert.AreSame(document, seen);
        Assert.IsNotNull(seen);
        Assert.IsNull(reply.Effect.Launch);
        Assert.AreEqual(new Rectangle(10, 10, 1900, 1060), BoundsOf(reply.Effect, 1));
        Assert.Contains("configreloaded>>", Lines(reply));
    }

    [TestMethod]
    public void ApplyingADocumentDirectlyDoesNotNotify()
    {
        var called = false;
        var core = new WindowManager { AfterReload = _ => called = true };

        core.Handle(new Configure(ConfigParser.Parse("exec-once = notepad")));

        Assert.IsFalse(called);
    }

    [TestMethod]
    public void ReloadWithoutAConfigSourceReportsAnError()
    {
        var reply = new WindowManager().Execute("reload");

        Assert.AreEqual("error: config is not available", reply.Text);
        Assert.IsEmpty(reply.Effect.Events);
    }

    [TestMethod]
    public void ReloadWithArgumentsIsRejected()
    {
        var parsed = CommandProtocol.Parse("reload");
        Assert.IsExactInstanceOfType<ReloadRequest>(Assert.IsExactInstanceOfType<CommandParsed>(parsed).Request);

        var reply = new WindowManager().Execute("reload now");

        Assert.AreEqual("error: unknown request", reply.Text);
    }

    [TestMethod]
    public void ReloadSwitchesEveryLayoutAndKeepsTheClients()
    {
        var area = new Rectangle(0, 0, 2000, 1000);
        var floated = new Rectangle(40, 50, 320, 200);
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(LayoutText("dwindle"))));
        core.Handle(Appeared(1, area));
        core.Handle(Appeared(2, area));
        core.Handle(Appeared(3, area, new Size(1, 700)));
        core.Handle(new WindowAppeared(new WindowId(4), area)
        {
            Traits = WindowTraits.Normal with { Class = "c4", Title = "t4", Dialog = true },
            Bounds = floated,
            ProcessId = 1004,
        });
        floated = NewFloat.Centered(area, floated);

        var scrolling = Reload(core, "scrolling");
        AssertKept(scrolling, floated);
        Assert.DoesNotContain(placement => placement.Client == Client(scrolling, 1).Id, scrolling.Placements);
        Assert.AreEqual(new Rectangle(0, 0, 1000, 1000), BoundsOf(scrolling, 2));
        Assert.AreEqual(new Rectangle(1000, 0, 1000, 1000), BoundsOf(scrolling, 3));

        var dwindle = Reload(core, "dwindle");
        AssertKept(dwindle, floated);
        Assert.AreEqual(3, dwindle.Placements.Count);
        Assert.AreNotEqual(new Rectangle(0, 0, 1100, 1000), BoundsOf(dwindle, 1));
    }

    [TestMethod]
    public void SourcesAreRecordedInOrder()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            ["colors.conf"] = "general:gaps_in = 1\n",
        });

        var document = ConfigParser.Parse("source = colors.conf\nsource = missing.conf\n", source);

        Assert.AreSequenceEqual(["colors.conf", "missing.conf"], document.Sources);
    }

    private static string LayoutText(string name) =>
        "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\ngeneral:layout = " + name + "\n";

    private static Outcome Reload(WindowManager core, string layout) =>
        core.Handle(new Configure(ConfigParser.Parse(LayoutText(layout))));

    private static WindowAppeared Appeared(long window, Rectangle area, Size? minSize = null) =>
        new(new WindowId(window), area)
        {
            Traits = WindowTraits.Normal with
            {
                Class = "c" + window,
                Title = "t" + window,
                MinSize = minSize,
            },
            ProcessId = 1000 + window,
        };

    private static void AssertKept(Outcome outcome, Rectangle floated)
    {
        Assert.AreEqual(4, outcome.State.Clients.Count);
        for (var window = 1L; window <= 4; window++)
        {
            var client = Client(outcome, window);
            Assert.AreEqual("c" + window, client.Class);
            Assert.AreEqual("t" + window, client.Title);
            Assert.AreEqual(1000 + window, client.ProcessId);
        }

        var dialog = Client(outcome, 4);
        Assert.IsTrue(dialog.Floating);
        Assert.AreEqual(floated, dialog.Bounds);
    }

    private static IReadOnlyList<string> Lines(Outcome outcome) =>
        CommandProtocol.FormatStream(outcome.Events, outcome.State);

    private static IReadOnlyList<string> Lines(CommandReply reply) =>
        Lines(reply.Effect);
}
