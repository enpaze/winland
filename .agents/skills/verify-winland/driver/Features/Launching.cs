using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "launching")]
[TestClass]
public class Launching
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void RunsExecOnceAtStartAndExecOnRequest()
    {
        var folder = Path.Combine(Path.GetTempPath(), "winland-launch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var once = Path.Combine(folder, "once.txt");
        var exec = Path.Combine(folder, "exec.txt");
        var formPid = Path.Combine(folder, "form.pid");
        var title = "WinlandLaunched " + Guid.NewGuid().ToString("N");
        var form = Convert.ToBase64String(Encoding.Unicode.GetBytes(
            $"Set-Content -LiteralPath '{formPid}' -Value $PID; Add-Type -AssemblyName System.Windows.Forms; $form = New-Object System.Windows.Forms.Form; $form.Text = '{title}'; [System.Windows.Forms.Application]::Run($form)"));
        try
        {
            var config =
                $"""
                exec-once = echo started> "{once}"
                exec-once = [float] echo skipped> "{once}.block"
                exec-once = [workspace 3] powershell -NoProfile -EncodedCommand {form}
                windowrule = tile, title:{title}

                """;
            VerifyRun.Drive(TestContext, config, run =>
            {
                run.Until("exec-once ran its command through cmd when winland started", () => Read(once) == "started");
                run.Holds("a launch line with an unsupported rule block does not launch", () => !File.Exists(once + ".block"), 500);

                run.Until("the [workspace 3] exec-once line opened its window on workspace 3", () => Launched(run, title) is { } client
                    && client.GetProperty("workspace").GetProperty("name").GetString() == "3", 30_000,
                    () => $"form.pid {Read(formPid) ?? "missing"}.");
                Assert.AreEqual(1, run.ActiveWorkspace(), "the [workspace 3] block switched away from workspace 1");
                run.Say("the launched window is on workspace 3 and workspace 1 is still active");
                CloseLaunched(formPid);
                run.Until("the launched window closed", () => Launched(run, title) is null);

                Assert.AreEqual("ok", run.Ask("dispatch", "exec", $"echo first&& echo second> \"{exec}\""));
                run.Until("dispatch exec ran the command line through cmd, with && and redirection", () => Read(exec) == "second");
            });
        }
        finally
        {
            CloseLaunched(formPid);
            VerifyDaemon.DeleteTree(folder);
        }
    }

    private static JsonElement? Launched(VerifyRun run, string title)
    {
        foreach (var client in run.Json("clients").EnumerateArray())
        {
            if (client.GetProperty("title").GetString() == title)
                return client;
        }

        return null;
    }

    private static void CloseLaunched(string pidFile)
    {
        if (!int.TryParse(Read(pidFile), out var pid))
            return;

        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
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
