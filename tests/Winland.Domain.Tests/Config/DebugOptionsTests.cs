namespace Winland.Domain.Tests;

[TestClass]
public class DebugOptionsTests
{
    [TestMethod]
    public void LogsAreOffByDefault()
    {
        Assert.IsFalse(DebugOptions.From(ConfigParser.Parse("")).LogFile);
    }

    [TestMethod]
    public void LogFileIsReadWithoutAWarningAndTheOtherDebugOptionsWarn()
    {
        var document = ConfigParser.Parse("""
            log_file = true
            debug:disable_time = false
            debug:enable_stdout_logs = true
            debug:colored_stdout_logs = false
            debug:overlay = true
            """);
        var options = DebugOptions.From(document);

        Assert.IsTrue(options.LogFile);
        Assert.Contains(warning => warning.Message == "Unsupported keyword 'debug:disable_time'.", document.Warnings);
        Assert.Contains(warning => warning.Message == "Unsupported keyword 'debug:enable_stdout_logs'.", document.Warnings);
        Assert.Contains(warning => warning.Message == "Unsupported keyword 'debug:colored_stdout_logs'.", document.Warnings);
        Assert.Contains(warning => warning.Message == "Unsupported keyword 'debug:overlay'.", document.Warnings);
    }
}
