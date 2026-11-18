namespace Winland.Infrastructure.Tests;

[TestClass]
public class TaskbarMarkerTests
{
    [TestMethod]
    public void TheCrashMarkerIsInTheDotWinlandFolder()
    {
        Assert.AreEqual(@"C:\Users\me\.winland\taskbar.marker", Taskbar.MarkerFile(@"C:\Users\me"));
    }

    [TestMethod]
    public void AWinlandOnItsOwnPipeKeepsAMarkerOfItsOwn()
    {
        Assert.AreEqual(@"C:\Users\me\.winland\taskbar.winland-test.marker", Taskbar.MarkerFile(@"C:\Users\me", "winland-test"));
    }
}
