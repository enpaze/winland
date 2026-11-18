namespace Winland.Infrastructure.Tests;
[TestClass]
public class InputClockTests
{
    [TestMethod]
    [DataRow(0x41, true, true)]
    [DataRow(0x09, true, true)]
    [DataRow(0x12, true, false)]
    [DataRow(0xA4, true, false)]
    [DataRow(0xA5, true, false)]
    [DataRow(0x10, true, false)]
    [DataRow(0xA2, true, false)]
    [DataRow(0x5B, true, false)]
    [DataRow(0x12, false, true)]
    public void InjectedKeysCountAsInputExceptModifiers(int key, bool injected, bool counts)
    {
        InputClock.Watch();
        InputClock.NoteClickOrKey(1_000);

        InputClock.NoteKey(5_000, key, injected);

        Assert.AreEqual(counts, InputClock.Recent(5_100));
    }

    [TestMethod]
    public void AClickIsJudgedByTheEventTimeNotTheHandlingTime()
    {
        InputClock.Watch();
        InputClock.NoteClickOrKey(5_000);

        Assert.IsTrue(InputClock.Recent(5_100));
        Assert.IsFalse(InputClock.Recent(5_000 + InputClock.RecentMilliseconds + 1));
    }

    [TestMethod]
    public void AClickJustBeforeTheTickCountWrapsIsStillRecent()
    {
        InputClock.Watch();
        InputClock.NoteClickOrKey(uint.MaxValue - 50);

        Assert.IsTrue(InputClock.Recent(50));
    }
}
