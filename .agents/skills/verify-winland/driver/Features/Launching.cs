using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "launching")]
public class Launching(ITestOutputHelper output)
{
    [VerifyFact]
    public void RunsExecOnceAtStartAndExecOnRequest()
    {
        var folder = Path.Combine(Path.GetTempPath(), "winland-launch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var once = Path.Combine(folder, "once.txt");
        var exec = Path.Combine(folder, "exec.txt");
        try
        {
            VerifyRun.Drive(output, $"exec-once = echo started> \"{once}\"\nexec-once = [float] echo skipped> \"{once}.block\"\n", run =>
            {
                run.Until("exec-once ran its command through cmd when winland started", () => Read(once) == "started");
                run.Holds("a launch line with a rule block does not launch", () => !File.Exists(once + ".block"), 500);

                Assert.Equal("ok", run.Ask("dispatch", "exec", $"echo first&& echo second> \"{exec}\""));
                run.Until("dispatch exec ran the command line through cmd, with && and redirection", () => Read(exec) == "second");
            });
        }
        finally
        {
            VerifyDaemon.DeleteTree(folder);
        }
    }

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
