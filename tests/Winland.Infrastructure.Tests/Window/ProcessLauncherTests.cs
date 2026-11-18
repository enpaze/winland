namespace Winland.Infrastructure.Tests;

[TestClass]
public class ProcessLauncherTests
{
    [TestMethod]
    public void AShellCommandIsQuotedWholeForCmd()
    {
        var shell = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var launch = new Launch("\"C:\\Program Files\\App\\app.exe\" \"a b\" && echo done", null);

        Assert.AreEqual(
            "\"" + shell + "\" /d /s /c \"\"C:\\Program Files\\App\\app.exe\" \"a b\" && echo done\"",
            ProcessLauncher.CommandLine(launch));
    }

    [TestMethod]
    public void AShellCommandThatStartsWithAQuoteAndHasMoreQuotesRuns()
    {
        var directory = Path.Combine(Path.GetTempPath(), "winland-quote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "out file.txt");
        var where = Path.Combine(Environment.SystemDirectory, "where.exe");
        var launch = new Launch("\"" + where + "\" \"cmd.exe\" > \"out file.txt\"", directory);

        try
        {
            var pid = ProcessLauncher.LaunchHere(launch);
            WaitForExit(pid);
            Assert.IsTrue(File.Exists(file), "The command did not run as written.");
            Assert.Contains("cmd.exe", File.ReadAllText(file), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void AncestorsStartWithTheParent()
    {
        using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /c ping -n 30 127.0.0.1 >nul")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        try
        {
            var ancestors = ProcessLauncher.Ancestors(child.Id);

            Assert.AreEqual(Environment.ProcessId, ancestors[0]);
            Assert.DoesNotContain(child.Id, ancestors);
            foreach (var id in ancestors)
{
    Assert.IsTrue(id > 0);
}
            Assert.AreEqual(ancestors.Count, ancestors.Distinct().Count());
            Assert.IsInRange(1, 25, ancestors.Count);
            Assert.AreSequenceEqual(ancestors.Skip(1), ProcessLauncher.Ancestors(Environment.ProcessId).Take(ancestors.Count - 1));
        }
        finally
        {
            child.Kill(entireProcessTree: true);
        }
    }

    [TestMethod]
    public void NoProcessHasNoAncestors()
    {
        Assert.IsEmpty(ProcessLauncher.Ancestors(0));
        Assert.IsEmpty(ProcessLauncher.Ancestors(-4));
        Assert.ThrowsExactly<ArgumentNullException>(() => ProcessLauncher.LaunchHere(null!));
    }

    private static void WaitForExit(int pid, int milliseconds = 10_000)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            if (!process.WaitForExit(milliseconds))
                throw new TimeoutException("The child did not exit.");
        }
        catch (ArgumentException)
        {
        }
    }
}
