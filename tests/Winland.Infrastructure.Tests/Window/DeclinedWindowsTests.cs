namespace Winland.Infrastructure.Tests;

[TestClass]
public class DeclinedWindowsTests
{
    private static readonly WindowId Player = new(1);
    private static readonly WindowTraits Declined = WindowTraits.Normal with { Class = "Player", Title = "Song" };

    [TestMethod]
    public void OnlyAWindowDeclinedWhileFullscreenNeedsAnotherOfferOnAMove()
    {
        var declined = new DeclinedWindows();
        declined.Note(Player, Declined with { ExclusiveFullscreen = true }, revision: 1);
        Assert.IsTrue(declined.Fullscreen(Player));

        declined.Note(Player, Declined, revision: 1);
        Assert.IsFalse(declined.Fullscreen(Player));
        declined.Note(Player, Declined with { ExclusiveFullscreen = true }, revision: 1);
        declined.Forget(Player);
        Assert.IsFalse(declined.Fullscreen(Player));
    }

    private static bool Unchanged(DeclinedWindows declined, bool titleRules = false, int revision = 1, string current = "Player") =>
        declined.Unchanged(Player, titleRules, revision, () => current);

    [TestMethod]
    public void ADeclinedWindowsNewTitleChangesNothingWhenNoRuleReadsTitles()
    {
        var declined = new DeclinedWindows();
        declined.Note(Player, Declined, revision: 1);

        Assert.IsTrue(Unchanged(declined));
    }

    [TestMethod]
    public void ADeclinedWindowIsOfferedAgainWhenARuleReadsTitles()
    {
        var declined = new DeclinedWindows();
        declined.Note(Player, Declined, revision: 1);

        Assert.IsFalse(Unchanged(declined, titleRules: true));
    }

    [TestMethod]
    public void ADeclinedWindowIsOfferedAgainWhenItsClassChanged()
    {
        var declined = new DeclinedWindows();
        declined.Note(Player, Declined, revision: 1);

        Assert.IsFalse(Unchanged(declined, current: "Windows.UI.Core.CoreWindow"));
    }

    [TestMethod]
    public void AReloadMakesEveryDeclinedWindowOfferedAgain()
    {
        var declined = new DeclinedWindows();
        declined.Note(Player, Declined, revision: 1);

        Assert.IsFalse(Unchanged(declined, revision: 2));
    }

    [TestMethod]
    public void AWindowNobodyDeclinedIsOffered()
    {
        Assert.IsFalse(Unchanged(new DeclinedWindows()));
    }

    [TestMethod]
    public void AWindowWithNoTitleIsOfferedAgainForItsTitleIsWhatItLacks()
    {
        var declined = new DeclinedWindows();
        declined.Note(Player, Declined, revision: 1);
        declined.Note(Player, Declined with { Titled = false, Title = "" }, revision: 1);

        Assert.IsFalse(Unchanged(declined));
    }

    [TestMethod]
    public void AForgottenWindowIsOffered()
    {
        var declined = new DeclinedWindows();
        declined.Note(Player, Declined, revision: 1);
        declined.Forget(Player);

        Assert.IsFalse(Unchanged(declined));
    }

    [TestMethod]
    public void TheClassIsReadOnlyWhenTheWindowCouldStayDeclined()
    {
        var declined = new DeclinedWindows();
        declined.Note(Player, Declined, revision: 1);
        var reads = 0;

        Assert.IsFalse(declined.Unchanged(Player, titleRules: true, revision: 1, () => { reads++; return "Player"; }));
        Assert.IsFalse(declined.Unchanged(Player, titleRules: false, revision: 2, () => { reads++; return "Player"; }));
        Assert.IsFalse(declined.Unchanged(new WindowId(2), titleRules: false, revision: 1, () => { reads++; return "Player"; }));
        Assert.AreEqual(0, reads);
    }
}
