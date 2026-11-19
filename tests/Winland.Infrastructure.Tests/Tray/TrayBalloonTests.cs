namespace Winland.Infrastructure.Tests;

[TestClass]
public class TrayBalloonTests
{
    private static ConfigNotice Notice(int generation) =>
        new("Winland: 1 config error", @"C:\winland\winland.conf:1: Unsupported keyword 'blur'.", @"C:\winland\winland.conf", generation);

    [TestMethod]
    public void AShownIconWithNoNoticeDrawsTheIconAndNoBalloon() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, false, false, false), TrayBalloon.For(true, null, 0, 0));

    [TestMethod]
    public void ANewNoticeOnTheShownIconShowsTheBalloon() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, false, true, false), TrayBalloon.For(true, Notice(1), 0, 0));

    [TestMethod]
    public void TheSameGenerationDoesNotShowTheBalloonAgain() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, false, false, false), TrayBalloon.For(true, Notice(3), 3, 0));

    [TestMethod]
    public void ADismissedGenerationDoesNotShowTheBalloonAgain() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, false, false, false), TrayBalloon.For(true, Notice(3), 3, 3));

    [TestMethod]
    public void ALaterGenerationReplacesTheBalloon() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, false, true, false), TrayBalloon.For(true, Notice(4), 3, 3));

    [TestMethod]
    public void ACleanLoadClearsTheBalloonAndKeepsTheIcon() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, false, false, true), TrayBalloon.For(true, null, 3, 0));

    [TestMethod]
    public void AHiddenIconAddsATemporaryIconForANewNotice() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, true, true, false), TrayBalloon.For(false, Notice(1), 0, 0));

    [TestMethod]
    public void ATemporaryIconStaysUpWithoutShowingTheBalloonAgain() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, true, false, false), TrayBalloon.For(false, Notice(1), 1, 0));

    [TestMethod]
    public void ADismissedNoticeOnAHiddenIconRemovesTheTemporaryIcon() =>
        Assert.AreEqual(new TrayBalloon.Plan(false, false, false, false), TrayBalloon.For(false, Notice(1), 1, 1));

    [TestMethod]
    public void ACleanLoadOnAHiddenIconRemovesTheTemporaryIcon() =>
        Assert.AreEqual(new TrayBalloon.Plan(false, false, false, false), TrayBalloon.For(false, null, 1, 0));

    [TestMethod]
    public void ALaterGenerationAfterDismissShowsATemporaryIconAgain() =>
        Assert.AreEqual(new TrayBalloon.Plan(true, true, true, false), TrayBalloon.For(false, Notice(2), 1, 1));
}
