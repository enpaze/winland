namespace Winland.Application.Tests;

[TestClass]
public class TextRepliesTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void ClientsAndActiveWindowAnswerInThePinnedReleasesTextForm()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(42), Area) { Traits = WindowTraits.Normal with { Class = "term", Title = "shell" } });

        var clients = core.Execute("clients").Text;

        Assert.StartsWith("Window 2a -> shell:\n\tmapped: 1\n\thidden: 0\n\tvisible: 1\n\tacceptsInput: 1\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tworkspace: 1 (1) on monitor \n\tfloating: 0\n\tmonitor: 0\n\tclass: term\n\ttitle: shell\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tgrouped: 0\n\tstableID: 2a\n\n", clients, StringComparison.Ordinal);
        Assert.EndsWith("\n\n", clients, StringComparison.Ordinal);
        Assert.AreEqual(clients, core.Execute("activewindow").Text);
        Assert.StartsWith("[", core.Execute("j/clients").Text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void WindowTextCannotForgeLinesInQueries()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(42), Area)
        {
            Traits = WindowTraits.Normal with { Class = "app\rclass", Title = "title\r\nWindow 99 -> forged:\nline" },
        });

        var clients = core.Execute("clients").Text;

        Assert.StartsWith("Window 2a -> title Window 99 -> forged: line:\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tclass: app class\n\ttitle: title Window 99 -> forged: line\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tinitialTitle: title Window 99 -> forged: line\n", clients, StringComparison.Ordinal);
        Assert.AreEqual(clients, core.Execute("activewindow").Text);
        Assert.Contains("\n\tlastwindowtitle: title Window 99 -> forged: line\n", core.Execute("workspaces").Text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void WorkspacesAndActiveWorkspaceAnswerInText()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var workspaces = core.Execute("workspaces").Text;

        Assert.StartsWith("workspace 1 (1) on monitor ", workspaces, StringComparison.Ordinal);
        Assert.Contains("\n\twindows: 1\n\thasfullscreen: 0\n\tlastwindow: 0x1\n", workspaces, StringComparison.Ordinal);
        Assert.AreEqual(workspaces, core.Execute("activeworkspace").Text);
    }

    [TestMethod]
    public void MonitorsAnswerInText()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, Area)]));

        var monitors = core.Execute("monitors").Text;

        Assert.StartsWith("Monitor " + @"\\.\DISPLAY1" + " (ID 0):\n\t1920x1080@", monitors, StringComparison.Ordinal);
        Assert.Contains("\n\tfocused: yes\n\tdpmsStatus: 1\n\tvrr: false\n", monitors, StringComparison.Ordinal);
        Assert.Contains("\n\tsolitaryBlockedBy: null\n", monitors, StringComparison.Ordinal);
        Assert.EndsWith("\t\tvrrCapable: no\n\n", monitors, StringComparison.Ordinal);
    }

    [TestMethod]
    public void BindsAnswerOneConfigLineEach()
    {
        var core = new WindowManager(ConfigParser.Parse(
            """
            bind = SUPER, W, killactive
            bind = SUPER SHIFT, 1, workspace, 1
            bind = SUPER, minus, resizeactive, -40 0
            bindd = SUPER, Q, Close the window, killactive
            bind = SUPER, mouse:right, resizewindow, 1
            """));

        var binds = core.Execute("binds").Text;

        Assert.AreEqual(
            "bind = SUPER, W, killactive\n"
            + "bind = SUPER SHIFT, 1, workspace, 1\n"
            + "bind = SUPER, minus, resizeactive, -40 0\n"
            + "bindd = SUPER, Q, Close the window, killactive\n"
            + "bind = SUPER, mouse:right, resizewindow, 1",
            binds);
        Assert.StartsWith("[{", core.Execute("j/binds").Text, StringComparison.Ordinal);
        Assert.AreEqual("no binds", new WindowManager().Execute("binds").Text);
        Assert.AreEqual("[]", new WindowManager().Execute("j/binds").Text);
    }

    [TestMethod]
    public void EachBindReadsBackFromItsLineAsTheSameBind()
    {
        var document = ConfigParser.Parse(
            """
            bind = SUPER, W, killactive
            bind = , Print, exec, screenshot
            bind = SUPER SHIFT CTRL ALT, Left, movewindow, mon:l
            bind = SUPER CAPS MOD2 MOD3 MOD5, F5, killactive
            bind = SUPER, 1, workspace, 1
            bind = SUPER, minus, resizeactive, -40 0
            bind = SUPER, equal, resizeactive, 40 0
            bind = SUPER, grave, togglespecialworkspace
            bind = SUPER, comma, exec, one, two
            bind = SUPER, Return, exec, wt
            bind = SUPER, code:50, killactive
            bind = SUPER, 25, killactive
            bind = SUPER, Super_L, exec, start menu
            bind = SUPER, N, exec, echo ## literal
            bind = SUPER, E, exec, [float; tile] notepad
            bindd = SUPER, Q, Close the window, killactive
            bind = SUPER, mouse:left, movewindow
            bind = SUPER, mouse:right, resizewindow, 1
            bindd = SUPER, mouse:middle, Move, movewindow
            """);
        Assert.IsEmpty(document.Warnings);
        var binds = document.Binds.OfType<Bind>().ToArray();
        Assert.AreEqual(19, binds.Length);

        var text = CommandProtocol.FormatBindsText(binds);
        var again = ConfigParser.Parse(text);

        Assert.IsEmpty(again.Warnings);
        Assert.AreSequenceEqual(binds, again.Binds);
    }

    [TestMethod]
    public void VersionAnswersOneLine()
    {
        Assert.IsTrue(ControlInvocation.TryAnswerLocally(["version"], out var version));

        Assert.StartsWith("winland " + BuildStamp.Version, version, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', version);
        if (BuildStamp.Commit.Length > 0)
            Assert.Contains(BuildStamp.Commit[..Math.Min(7, BuildStamp.Commit.Length)], version, StringComparison.Ordinal);
        Assert.IsTrue(ControlInvocation.TryAnswerLocally(["version", "-j"], out var json));
        Assert.StartsWith("{", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void ConfigErrorsAnswerOneFileLineAndMessageEach()
    {
        const string path = @"C:\winland\winland.conf";
        const string colors = @"C:\winland\colors.conf";
        var core = new WindowManager();
        Assert.AreEqual("no config errors", core.Execute("configerrors").Text);
        Assert.AreEqual("[]", core.Execute("j/configerrors").Text);

        core.Handle(new Configure(new ConfigDocument(
            [],
            [],
            [new ConfigWarning(1, "Unsupported keyword 'blur'."), new ConfigWarning(3, "Invalid modifier 'X'.", colors), new ConfigWarning(0, "Could not read config.")])
        {
            Path = path,
        }));

        Assert.AreEqual(
            path + ":1: Unsupported keyword 'blur'.\n" + colors + ":3: Invalid modifier 'X'.\n" + path + ": Could not read config.",
            core.Execute("configerrors").Text);
        Assert.AreEqual(
            "[\"line 1: Unsupported keyword 'blur'.\",\"line 3: Invalid modifier 'X'.\",\"Could not read config.\"]",
            core.Execute("j/configerrors").Text);
    }

    [TestMethod]
    public void FilesAnswerTheConfigReadAndTheLogWritten()
    {
        const string path = @"C:\winland\work.conf";
        const string log = @"C:\winland\state\winland.log";
        var core = new WindowManager();
        Assert.AreEqual("config: none\nlog: none", core.Execute("files").Text);
        Assert.AreEqual("""{"config":null,"log":null}""", core.Execute("j/files").Text);

        string? writing = log;
        core.LogFile = () => writing;
        core.Handle(new Configure(new ConfigDocument([], [], []) { Path = path }));

        Assert.AreEqual("config: " + path + "\nlog: " + log, core.Execute("files").Text);
        Assert.AreEqual("""{"config":"C:\\winland\\work.conf","log":"C:\\winland\\state\\winland.log"}""", core.Execute("j/files").Text);

        writing = null;
        Assert.AreEqual("""{"config":"C:\\winland\\work.conf","log":null}""", core.Execute("j/files").Text);
    }
}
