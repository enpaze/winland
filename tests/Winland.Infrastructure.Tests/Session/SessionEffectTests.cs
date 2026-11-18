namespace Winland.Infrastructure.Tests;

[TestClass]
public class SessionEffectTests
{
    [TestMethod]
    public void HaltInvokesTheInstalledStop()
    {
        var stopped = 0;
        SessionHalt.Stop = () => stopped++;
        try
        {
            SessionHalt.Request();
            Assert.AreEqual(1, stopped);
        }
        finally
        {
            SessionHalt.Stop = null;
        }
    }
}
