namespace Winland.Application.Tests;

[TestClass]
public class ConfigNoticeTests
{
    private const string Path = @"C:\winland\winland.conf";

    [TestMethod]
    public void OneErrorNamesTheFileAndTheLine()
    {
        var notice = Load("blur = on\n");

        Assert.IsNotNull(notice);
        Assert.AreEqual("Winland: 1 config error", notice.Title);
        Assert.AreEqual(@"C:\winland\winland.conf:1: Unsupported keyword 'blur'.", notice.Text);
        Assert.AreEqual(Path, notice.File);
        Assert.IsTrue(notice.Generation > 0);
    }

    [TestMethod]
    public void TwoErrorsNameTheFirstAndALaterLoadReplacesTheNotice()
    {
        var core = Loaded("blur = on\n");
        var first = Notice(core);

        core.Handle(new Configure(ConfigParser.Parse("ghostkeyword = 1\nblur = on\n", path: Path)));
        var second = Notice(core);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual("Winland: 2 config errors", second.Title);
        Assert.AreEqual(@"C:\winland\winland.conf:1: Unsupported keyword 'ghostkeyword'.", second.Text);
        Assert.IsTrue(second.Generation > first.Generation);
        Assert.DoesNotContain("blur", second.Text);
    }

    [TestMethod]
    public void TheSameErrorsAreShownAgain()
    {
        var core = Loaded("blur = on\n");
        var first = Notice(core);

        core.Handle(new Configure(ConfigParser.Parse("blur = on\n", path: Path)));
        var second = Notice(core);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual(first.Text, second.Text);
        Assert.AreNotEqual(first.Generation, second.Generation);
    }

    [TestMethod]
    public void ACleanLoadTakesTheNoticeAway()
    {
        var core = Loaded("blur = on\n");

        core.Handle(new Configure(ConfigParser.Parse("general:gaps_in = 4\n", path: Path)));

        Assert.IsNull(Notice(core));
    }



    [TestMethod]
    public void ASourcedErrorNamesThatFile()
    {
        var source = new MemorySource(new Dictionary<string, string> { [@"C:\winland\extra.conf"] = "blur = on\n" });
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("source = extra.conf\n", source, path: Path)));
        var notice = Notice(core);

        Assert.IsNotNull(notice);
        Assert.AreEqual("Winland: 1 config error", notice.Title);
        Assert.AreEqual(@"C:\winland\extra.conf:1: Unsupported keyword 'blur'.", notice.Text);
        Assert.AreEqual(@"C:\winland\extra.conf", notice.File);
    }

    [TestMethod]
    public void AFailedReloadLeavesTheNotice()
    {
        var core = Loaded("blur = on\n");
        var before = Notice(core);
        core.ReadConfig = () => new ConfigRead(null, new ConfigWarning(0, "Could not read config.", null));

        Assert.StartsWith("error:", core.Execute("reload").Text, StringComparison.Ordinal);
        Assert.AreEqual(before, Notice(core));
    }

    [TestMethod]
    public void ANoticeWithoutAFileOrALineStillSaysWhatItCan()
    {
        var noFile = ConfigNotice.For([new ConfigWarning(3, "Unsupported keyword 'blur'.")], null, 1);
        var noLine = ConfigNotice.For([new ConfigWarning(0, "Could not read config.")], Path, 2);
        var bare = ConfigNotice.For([new ConfigWarning(0, "Could not read config.")], null, 3);

        Assert.AreEqual("line 3: Unsupported keyword 'blur'.", noFile!.Text);
        Assert.IsNull(noFile.File);
        Assert.AreEqual(@"C:\winland\winland.conf: Could not read config.", noLine!.Text);
        Assert.AreEqual(Path, noLine.File);
        Assert.AreEqual("Could not read config.", bare!.Text);
        Assert.IsNull(bare.File);
    }

    [TestMethod]
    public void TheFirstStartWelcomesTheUserAndAClickOpensTheConfig()
    {
        var core = new WindowManager { FirstStart = true };
        core.Handle(new Configure(ConfigParser.Parse("general:gaps_in = 4\n", path: Path)));
        var notice = Notice(core);

        Assert.IsNotNull(notice);
        Assert.AreEqual("Winland", notice.Title);
        Assert.AreEqual(
            "Winland is tiling your windows. Run winland get binds to list the hotkeys. Right-click the Winland icon to edit the config or quit.",
            notice.Text);
        Assert.IsInRange(1, 255, notice.Text.Length);
        Assert.AreEqual(Path, notice.File);
        Assert.IsTrue(notice.Informational);
        Assert.IsTrue(notice.Generation > 0);
    }

    [TestMethod]
    public void AStartThatWroteNoConfigShowsNoWelcome() =>
        Assert.IsNull(Load("general:gaps_in = 4\n"));

    [TestMethod]
    public void ALaterLoadTakesTheWelcomeAwayAndDoesNotShowItAgain()
    {
        var core = new WindowManager { FirstStart = true };
        core.Handle(new Configure(ConfigParser.Parse("general:gaps_in = 4\n", path: Path)));

        core.Handle(new Configure(ConfigParser.Parse("general:gaps_in = 5\n", path: Path)));

        Assert.IsNull(Notice(core));
    }

    [TestMethod]
    public void ErrorsOnTheFirstStartAreShownInsteadOfTheWelcome()
    {
        var core = new WindowManager { FirstStart = true };
        core.Handle(new Configure(ConfigParser.Parse("blur = on\n", path: Path)));
        var notice = Notice(core);

        Assert.IsNotNull(notice);
        Assert.AreEqual("Winland: 1 config error", notice.Title);
        Assert.IsFalse(notice.Informational);
    }

    [TestMethod]
    public void SetErrorIsNotARequest() =>
        Assert.AreEqual("error: unknown command 'seterror'", new WindowManager().Execute("seterror rgb(ff0000) broke").Text);

    private static ConfigNotice? Load(string text) => Notice(Loaded(text));

    private static WindowManager Loaded(string text)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(text, path: Path)));
        return core;
    }

    private static ConfigNotice? Notice(WindowManager core) => core.Execute("resizewindow").Effect.ConfigNotice;
}
