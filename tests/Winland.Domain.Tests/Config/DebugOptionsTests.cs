namespace Winland.Domain.Tests;

[TestClass]
public class DebugOptionsTests
{
    [TestMethod]
    public void LogsAreOffByDefault()
    {
        Assert.IsTrue(DebugOptions.From(ConfigParser.Parse("")).DisableLogs);
    }

    [TestMethod]
    public void DisableLogsIsReadWithoutAWarningAndTheOtherDebugOptionsWarn()
    {
        var document = ConfigParser.Parse("""
            debug:disable_logs = false
            debug:disable_time = false
            debug:enable_stdout_logs = true
            debug:colored_stdout_logs = false
            debug:overlay = true
            """);
        var options = DebugOptions.From(document);

        Assert.IsFalse(options.DisableLogs);
        Assert.Contains(warning => warning.Message == "Unsupported keyword 'debug:disable_time'.", document.Warnings);
        Assert.Contains(warning => warning.Message == "Unsupported keyword 'debug:enable_stdout_logs'.", document.Warnings);
        Assert.Contains(warning => warning.Message == "Unsupported keyword 'debug:colored_stdout_logs'.", document.Warnings);
        Assert.Contains(warning => warning.Message == "Unsupported keyword 'debug:overlay'.", document.Warnings);
    }
}
