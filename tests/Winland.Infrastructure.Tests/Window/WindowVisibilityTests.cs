using Events = Windows.Win32.PInvoke;

namespace Winland.Infrastructure.Tests;

[TestClass]
public class WindowVisibilityTests
{
    private static readonly WindowFacts ShownClient = new(Minimized: false, Visible: true, Managed: true, Concealed: false);

    [TestMethod]
    public void AMinimizeWinlandDidNotAskForIsTakenBack()
    {
        Assert.AreEqual(VisibilityAction.UndoMinimize, WindowVisibility.Decide(Events.EVENT_OBJECT_HIDE, ShownClient with { Minimized = true }));
    }

    [TestMethod]
    public void AMinimizedClientHiddenToTheTrayStopsBeingAClient()
    {
        var tray = ShownClient with { Minimized = true, Visible = false };

        Assert.AreEqual(VisibilityAction.Close, WindowVisibility.Decide(Events.EVENT_OBJECT_HIDE, tray));
        Assert.AreEqual(VisibilityAction.None, WindowVisibility.Decide(Events.EVENT_OBJECT_HIDE, tray with { Concealed = true }));
    }

    [TestMethod]
    public void AClientItsApplicationHidesStopsBeingAClientUnlessWinlandHidIt()
    {
        var hidden = ShownClient with { Visible = false };

        Assert.AreEqual(VisibilityAction.Close, WindowVisibility.Decide(Events.EVENT_OBJECT_HIDE, hidden));
        Assert.AreEqual(VisibilityAction.None, WindowVisibility.Decide(Events.EVENT_OBJECT_HIDE, hidden with { Concealed = true }));
        Assert.AreEqual(VisibilityAction.None, WindowVisibility.Decide(Events.EVENT_OBJECT_HIDE, ShownClient));
        Assert.AreEqual(VisibilityAction.None, WindowVisibility.Decide(Events.EVENT_OBJECT_HIDE, hidden with { Managed = false }));
    }

    [TestMethod]
    public void AHiddenClientThatIsCloakedStaysAClient()
    {
        Assert.AreEqual(VisibilityAction.None, WindowVisibility.Decide(Events.EVENT_OBJECT_CLOAKED, ShownClient with { Visible = false }));
        Assert.AreEqual(VisibilityAction.Close, WindowVisibility.Decide(Events.EVENT_OBJECT_CLOAKED, ShownClient));
    }

    [TestMethod]
    [DataRow(Events.EVENT_OBJECT_SHOW)]
    [DataRow(Events.EVENT_OBJECT_UNCLOAKED)]
    [DataRow(Events.EVENT_OBJECT_CREATE)]
    public void AShownClientIsRestoredAndAnyOtherWindowOffered(uint @event)
    {
        Assert.AreEqual(VisibilityAction.Restore, WindowVisibility.Decide(@event, ShownClient));
        Assert.AreEqual(VisibilityAction.Adopt, WindowVisibility.Decide(@event, ShownClient with { Managed = false }));
        Assert.AreEqual(VisibilityAction.None, WindowVisibility.Decide(@event, ShownClient with { Minimized = true }));
    }

    [TestMethod]
    public void ARenamedClientIsRetitledAndARenamedWindowOffered()
    {
        Assert.AreEqual(VisibilityAction.Retitle, WindowVisibility.Decide(Events.EVENT_OBJECT_NAMECHANGE, ShownClient));
        Assert.AreEqual(VisibilityAction.Adopt, WindowVisibility.Decide(Events.EVENT_OBJECT_NAMECHANGE, ShownClient with { Managed = false }));
    }

    [TestMethod]
    public void ADeclinedWindowIsOfferedAgainOnlyWhenTheRenameCouldChangeTheAnswer()
    {
        var declined = ShownClient with { Managed = false, Declined = true };

        Assert.AreEqual(VisibilityAction.None, WindowVisibility.Decide(Events.EVENT_OBJECT_NAMECHANGE, declined));
        Assert.AreEqual(VisibilityAction.Adopt, WindowVisibility.Decide(Events.EVENT_OBJECT_NAMECHANGE, declined with { Declined = false }));
        Assert.AreEqual(VisibilityAction.Adopt, WindowVisibility.Decide(Events.EVENT_OBJECT_SHOW, declined));
    }
}
