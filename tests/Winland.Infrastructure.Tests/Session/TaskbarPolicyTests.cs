namespace Winland.Infrastructure.Tests;

[TestClass]
public class TaskbarPolicyTests
{
    [TestMethod]
    public void TheTaskbarStaysAsItIsWhenTheOptionIsOff()
    {
        var choice = TaskbarPolicy.Decide(hide: false, owns: false, marker: false, open: true);

        Assert.AreEqual(new TaskbarChoice(false, false, false), choice);
    }

    [TestMethod]
    public void TurningTheOptionOnHidesTheBars()
    {
        Assert.AreEqual(
            new TaskbarChoice(Hide: true, Restore: false, Relayout: false),
            TaskbarPolicy.Decide(hide: true, owns: false, marker: false, open: false));
    }

    [TestMethod]
    public void TurningTheOptionOnLaysTheMonitorsOutOnceTheSessionIsOpen()
    {
        Assert.AreEqual(
            new TaskbarChoice(Hide: true, Restore: false, Relayout: true),
            TaskbarPolicy.Decide(hide: true, owns: false, marker: false, open: true));
    }

    [TestMethod]
    public void HidingAgainDoesNothing()
    {
        Assert.AreEqual(
            new TaskbarChoice(false, false, false),
            TaskbarPolicy.Decide(hide: true, owns: true, marker: true, open: true));
    }

    [TestMethod]
    public void TurningTheOptionOffRestoresTheBarsAndLaysTheMonitorsOut()
    {
        Assert.AreEqual(
            new TaskbarChoice(Hide: false, Restore: true, Relayout: true),
            TaskbarPolicy.Decide(hide: false, owns: true, marker: false, open: true));
    }

    [TestMethod]
    public void ACrashMarkerIsRestoredWhenTheOptionIsOff()
    {
        Assert.AreEqual(
            new TaskbarChoice(Hide: false, Restore: true, Relayout: false),
            TaskbarPolicy.Decide(hide: false, owns: false, marker: true, open: false));
        Assert.AreEqual(
            new TaskbarChoice(Hide: false, Restore: true, Relayout: true),
            TaskbarPolicy.Decide(hide: false, owns: false, marker: true, open: true));
    }
}
