namespace Winland.Infrastructure.Tests;

[TestClass]
public class PresentClockTests
{
    [TestMethod]
    public void AVblankThatPassedGivesTheOneAfterNow() => Assert.AreEqual(130, PresentClock.Next(125, 100, 10));

    [TestMethod]
    public void AComingVblankIsTheNextOne() => Assert.AreEqual(100, PresentClock.Next(94, 100, 10));

    [TestMethod]
    public void NowOnAVblankGivesTheFollowingOne() => Assert.AreEqual(110, PresentClock.Next(100, 100, 10));

    [TestMethod]
    public void WithoutTimingItIsNow() => Assert.AreEqual(94, PresentClock.Next(94, 0, 10));
}
