namespace Winland.Domain.Tests;

[TestClass]
public class DiscreteScrollTests
{
    [TestMethod]
    [DataRow(12, true)]
    [DataRow(-12, true)]
    [DataRow(120, false)]
    [DataRow(-240, false)]
    [DataRow(0, false)]
    public void OnlyTurnsThatAreNotWholeNotchesAreEmulated(int delta, bool emulated) =>
        Assert.AreEqual(emulated, DiscreteScroll.Emulates(delta));

    [TestMethod]
    public void ASmallTurnsSendsANotchAtOnceThenOneForEveryNotchItAddsUpTo()
    {
        var scroll = new DiscreteScroll();
        var sent = new List<int> { scroll.Emulate(30, false, 1000) };
        for (var i = 1; i <= 10; i++)
            sent.Add(scroll.Emulate(30, false, 1000 + (i * 10)));

        TestSequence.Equal([120, 0, 0, 0, 120, 0, 0, 0, 120, 0, 0], sent);
    }

    [TestMethod]
    public void AWholeNotchIsOneNotch()
    {
        var scroll = new DiscreteScroll();

        Assert.AreEqual(120, scroll.Emulate(120, false, 1000));
        Assert.AreEqual(120, scroll.Emulate(120, false, 1010));
        Assert.AreEqual(120, scroll.Emulate(120, false, 1020));
        Assert.AreEqual(-120, scroll.Emulate(-120, false, 1030));
    }

    [TestMethod]
    public void AReversalAnotherAxisOrAPauseStartsAgainWithANotchAtOnce()
    {
        var scroll = new DiscreteScroll();
        _ = scroll.Emulate(30, false, 1000);

        Assert.AreEqual(-120, scroll.Emulate(-30, false, 1010));
        Assert.AreEqual(-120, scroll.Emulate(-30, true, 1020));
        Assert.AreEqual(120, scroll.Emulate(30, true, 1020 + DiscreteScroll.IdleMs + 1));
    }

    [TestMethod]
    public void AnHorizontalTurnSendsNotchesOfItsOwnSign()
    {
        var scroll = new DiscreteScroll();

        Assert.AreEqual(-120, scroll.Emulate(-12, true, 1000));
    }
}
