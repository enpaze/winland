namespace Winland.Daemon.Tests;

[TestClass]
public class UsageTests
{
    [TestMethod]
    public void UsageNamesTheTweaksSubcommandsAndHidesUserCleanup()
    {
        using var writer = new StringWriter();
        Program.WriteUsage(writer);
        var usage = writer.ToString();

        Assert.Contains("winland tweaks off [--touchpad]", usage, StringComparison.Ordinal);
        Assert.Contains("winland tweaks restore", usage, StringComparison.Ordinal);
        Assert.DoesNotContain("--off", usage, StringComparison.Ordinal);
        Assert.DoesNotContain("--restore", usage, StringComparison.Ordinal);
        Assert.DoesNotContain("--winland-cleanup-user", usage, StringComparison.Ordinal);
    }
}
