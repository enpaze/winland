namespace Winland.Infrastructure.Tests;

[TestClass]
public class AccentNoticeTests
{
    [TestMethod]
    public void ColorizationSystemColorsAndSettingsAskForANewAccent()
    {
        Assert.IsTrue(AccentNotices.Changed(AccentNotices.ColorizationChanged));
        Assert.IsTrue(AccentNotices.Changed(AccentNotices.SystemColorChange));
        Assert.IsTrue(AccentNotices.Changed(AccentNotices.SettingChange));
        Assert.IsFalse(AccentNotices.Changed(0x0010));
    }

    [TestMethod]
    public void AReadAccentIsOpaque()
    {
        var color = SystemAccent.ReadWindows();
        if (color is BorderColor read)
            Assert.AreEqual(0xFF, read.A);
    }

    [TestMethod]
    public void TheHostFeedsTheAccentIntoTheCoreAndSkipsAnUnchangedRead()
    {
        var previous = SystemAccent.Read;
        var reads = 0;
        SystemAccent.Read = () =>
        {
            reads++;
            return new BorderColor(0xFF, 0x11, 0x22, 0x33);
        };
        try
        {
            var core = new WindowManager();
            core.Handle(new Configure(ConfigParser.Parse("")));
            using var host = new SessionLoop(core);

            host.NoteAccent();
            host.NoteAccent();

            Assert.AreEqual(2, reads);
        }
        finally
        {
            SystemAccent.Read = previous;
        }
    }

    [TestMethod]
    public void AConfiguredBorderWinsOverTheAccentTheHostReads()
    {
        var previous = SystemAccent.Read;
        SystemAccent.Read = () => new BorderColor(0xFF, 0x11, 0x22, 0x33);
        try
        {
            var core = new WindowManager();
            core.Handle(new Configure(ConfigParser.Parse("border_color = rgba(aabbccff)\n")));
            using var host = new SessionLoop(core);

            host.NoteAccent();


            core.Handle(new Configure(ConfigParser.Parse("")));
        }
        finally
        {
            SystemAccent.Read = previous;
        }
    }
}
