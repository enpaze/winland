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

        var clients = core.Execute("get windows").Text;

        Assert.StartsWith("Window 2a -> shell:\n\thidden: 0\n\tvisible: 1\n\tat: 0,0\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tworkspace: 1\n\tfloating: 0\n\tmonitor: \n\tclass: term\n\ttitle: shell\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tfullscreen: 0\n\tfullscreenHandler: default\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tgrouped: 0\n\n", clients, StringComparison.Ordinal);
        Assert.EndsWith("\n\n", clients, StringComparison.Ordinal);
        Assert.AreEqual(clients, core.Execute("get window").Text);
        Assert.StartsWith("[", core.Execute("j/get windows").Text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void WindowTextCannotForgeLinesInQueries()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(42), Area)
        {
            Traits = WindowTraits.Normal with { Class = "app\rclass", Title = "title\r\nWindow 99 -> forged:\nline" },
        });

        var clients = core.Execute("get windows").Text;

        Assert.StartsWith("Window 2a -> title Window 99 -> forged: line:\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tclass: app class\n\ttitle: title Window 99 -> forged: line\n", clients, StringComparison.Ordinal);
        Assert.Contains("\n\tinitialTitle: title Window 99 -> forged: line\n", clients, StringComparison.Ordinal);
        Assert.AreEqual(clients, core.Execute("get window").Text);
        Assert.Contains("\n\tlastWindowTitle: title Window 99 -> forged: line\n", core.Execute("get workspaces").Text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void WorkspacesAndActiveWorkspaceAnswerInText()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var workspaces = core.Execute("get workspaces").Text;

        Assert.StartsWith("workspace 1 on monitor :\n\tmonitorId: 0\n", workspaces, StringComparison.Ordinal);
        Assert.Contains("\n\twindows: 1\n\thasFullscreen: 0\n\tlastWindow: 0x1\n", workspaces, StringComparison.Ordinal);
        Assert.AreEqual(workspaces, core.Execute("get workspace").Text);
    }

    [TestMethod]
    public void MonitorsAnswerInText()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, Area)]));

        var monitors = core.Execute("get monitors").Text;

        Assert.StartsWith("Monitor " + @"\\.\DISPLAY1" + " (ID 0):\n\t1920x1080@", monitors, StringComparison.Ordinal);
        Assert.Contains("\n\tactive workspace: 1\n\tspecial workspace: \n", monitors, StringComparison.Ordinal);
        Assert.Contains("\n\tfocused: yes\n\tdisabled: false\n\tcurrentFormat: \n", monitors, StringComparison.Ordinal);
        Assert.EndsWith("\t\tbt2020: no\n\n", monitors, StringComparison.Ordinal);
    }

    [TestMethod]
    public void BindsAnswerOneConfigLineEach()
    {
        var core = new WindowManager(ConfigParser.Parse(
            """
            bind = Win+W, close
            bind = Win+Shift+1, workspace 1
            bind = Win+minus, resize -40 0
            bind = Win+Q, "Close the window", close
            bind = Win+mouse:right, resize
            """));

        var binds = core.Execute("get binds").Text;

        Assert.AreEqual(
            "bind = Win+W, close\n"
            + "bind = Win+Shift+1, workspace 1\n"
            + "bind = Win+minus, resize -40 0\n"
            + "bind = Win+Q, \"Close the window\", close\n"
            + "bind = Win+mouse:right, resize",
            binds);
        Assert.StartsWith("[{", core.Execute("j/get binds").Text, StringComparison.Ordinal);
        Assert.AreEqual("no binds", new WindowManager().Execute("get binds").Text);
        Assert.AreEqual("[]", new WindowManager().Execute("j/get binds").Text);
    }

    [TestMethod]
    public void EachBindReadsBackFromItsLineAsTheSameBind()
    {
        var document = ConfigParser.Parse(
            """
            bind = Win+W, close
            bind = Print, exec screenshot
            bind = Win+Ctrl+Alt+Shift+Left, move mon:l
            bind = Ctrl+Alt+F5, close
            bind = Win+1, workspace 1
            bind = Win+minus, resize -40 0
            bind = Win+equal, resize 40 0
            bind = Win+grave, scratchpad
            bind = Win+comma, exec one, two
            bind = Win+Return, exec wt
            bind = Win+code:50, close
            bind = Win+25, close
            bind = Win+Super_L, exec start menu
            bind = Win+N, exec echo ## literal
            bind = Win+E, exec [float; tile] notepad
            bind = Win+Q, "Close the window", close
            bind = Win+mouse:left, move
            bind = Win+mouse:right, resize
            bind = Win+mouse:middle, "Move it, then drop it", move
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
        Assert.AreEqual("no config errors", core.Execute("get errors").Text);
        Assert.AreEqual("[]", core.Execute("j/get errors").Text);

        core.Handle(new Configure(new ConfigDocument(
            [],
            [],
            [new ConfigWarning(1, "Unsupported keyword 'blur'."), new ConfigWarning(3, "Invalid modifier 'X'.", colors), new ConfigWarning(0, "Could not read config.")])
        {
            Path = path,
        }));

        Assert.AreEqual(
            path + ":1: Unsupported keyword 'blur'.\n" + colors + ":3: Invalid modifier 'X'.\n" + path + ": Could not read config.",
            core.Execute("get errors").Text);
        Assert.AreEqual(
            """[{"file":"C:\\winland\\winland.conf","line":1,"message":"Unsupported keyword 'blur'."},{"file":"C:\\winland\\colors.conf","line":3,"message":"Invalid modifier 'X'."},{"file":"C:\\winland\\winland.conf","line":0,"message":"Could not read config."}]""",
            core.Execute("j/get errors").Text);

        core.Handle(new Configure(new ConfigDocument([], [], [new ConfigWarning(2, "Unsupported keyword 'blur'.")])));

        Assert.AreEqual("""[{"file":null,"line":2,"message":"Unsupported keyword 'blur'."}]""", core.Execute("j/get errors").Text);
    }

    [TestMethod]
    public void FilesAnswerTheConfigReadAndTheLogWritten()
    {
        const string path = @"C:\winland\work.conf";
        const string log = @"C:\winland\state\winland.log";
        var core = new WindowManager();
        Assert.AreEqual("config: none\nlog: none", core.Execute("get files").Text);
        Assert.AreEqual("""{"config":null,"log":null}""", core.Execute("j/get files").Text);

        string? writing = log;
        core.LogFile = () => writing;
        core.Handle(new Configure(new ConfigDocument([], [], []) { Path = path }));

        Assert.AreEqual("config: " + path + "\nlog: " + log, core.Execute("get files").Text);
        Assert.AreEqual("""{"config":"C:\\winland\\work.conf","log":"C:\\winland\\state\\winland.log"}""", core.Execute("j/get files").Text);

        writing = null;
        Assert.AreEqual("""{"config":"C:\\winland\\work.conf","log":null}""", core.Execute("j/get files").Text);
    }
}
