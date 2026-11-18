namespace Winland.Application.Tests;

[TestClass]
public class TouchpadFingerTests
{
    [TestMethod]
    [DataRow(1, (long)0, true)]
    [DataRow(2, (long)100, true)]
    [DataRow(0, (long)0, false)]
    [DataRow(1, TouchpadPointer.StaleMs + 1, false)]
    public void ACursorMoveIsTheTouchpadsWhileAFingerIsOnIt(int fingers, long since, bool moves)
    {
        Assert.AreEqual(moves, TouchpadPointer.Moves(fingers, since));
    }

    [TestMethod]
    [DataRow(2, (long)0, (long)0, 120, true)]
    [DataRow(1, (long)0, (long)5000, 30, false)]
    [DataRow(0, (long)900, (long)900, 30, true)]
    [DataRow(0, (long)900, (long)900, 120, false)]
    [DataRow(0, (long)3000, TouchpadPointer.CoastMs + 1, 30, false)]
    [DataRow(2, TouchpadPointer.StaleMs + 1, (long)5000, 120, false)]
    public void AWheelTurnIsTheTouchpadsWhileTwoFingersScrollOrCoastAfter(int fingers, long sinceReport, long sinceTwo, int delta, bool scrolls)
    {
        Assert.AreEqual(scrolls, TouchpadPointer.Scrolls(fingers, sinceReport, sinceTwo, delta));
    }
}
