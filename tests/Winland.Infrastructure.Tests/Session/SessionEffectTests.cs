namespace Winland.Infrastructure.Tests;

public class SessionEffectTests
{
    [Fact]
    public void HaltInvokesTheInstalledStop()
    {
        var stopped = 0;
        SessionHalt.Stop = () => stopped++;
        try
        {
            SessionHalt.Request();
            Assert.Equal(1, stopped);
        }
        finally
        {
            SessionHalt.Stop = null;
        }
    }
}
