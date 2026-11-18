using System.Diagnostics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Winland.Smoke.Tests;

public sealed class SmokeFactAttribute : FactAttribute
{
    public SmokeFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WINLAND_SMOKE"), "1", StringComparison.Ordinal))
        {
            Skip = "Set WINLAND_SMOKE=1 to run the end-to-end smoke test in a desktop session.";
            return;
        }

        var running = Process.GetProcessesByName("winland");
        try
        {
            if (running.Length > 0)
            {
                Skip = "A Winland process is running. Stop it before running the smoke tests.";
                return;
            }
        }
        finally
        {
            foreach (var process in running)
                process.Dispose();
        }

        if (DesktopSession.SkipReason() is string reason)
            Skip = reason;
    }
}

public sealed class SystemSmokeFactAttribute : FactAttribute
{
    public SystemSmokeFactAttribute()
    {
        var smoke = new SmokeFactAttribute();
        if (smoke.Skip is not null)
        {
            Skip = smoke.Skip;
            return;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable("WINLAND_SMOKE_SYSTEM"), "1", StringComparison.Ordinal))
            Skip = "This driver installs Winland or changes Windows settings for this user. Set WINLAND_SMOKE_SYSTEM=1 to let it, as CI does.";
    }
}
