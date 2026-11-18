using System.Diagnostics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Winland.Verify.Tests;

public sealed class VerifyFactAttribute : FactAttribute
{
    public VerifyFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WINLAND_VERIFY"), "1", StringComparison.Ordinal))
        {
            Skip = "Set WINLAND_VERIFY=1 to run the feature drivers in a desktop session.";
            return;
        }

        var running = Process.GetProcessesByName("winland");
        try
        {
            if (running.Length > 0)
            {
                Skip = "A Winland process is running. Stop it before running the feature drivers.";
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

public sealed class SystemVerifyFactAttribute : FactAttribute
{
    public SystemVerifyFactAttribute()
    {
        var verify = new VerifyFactAttribute();
        if (verify.Skip is not null)
        {
            Skip = verify.Skip;
            return;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable("WINLAND_VERIFY_SYSTEM"), "1", StringComparison.Ordinal))
            Skip = "This driver installs Winland or changes Windows settings for this user. Set WINLAND_VERIFY_SYSTEM=1 to let it, as CI does.";
    }
}
