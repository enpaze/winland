using System.Diagnostics;

namespace Winland.Infrastructure.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopCollection
{
    public const string Name = "Desktop";
}

public class DesktopFactAttribute : FactAttribute
{
    public DesktopFactAttribute()
    {
        if (DaemonRunning())
            Skip = "A Winland process is running. It adopts and restores the windows this test opens. Stop it to run the test.";
    }

    private static bool DaemonRunning()
    {
        var running = Process.GetProcessesByName("winland");
        try
        {
            return running.Length > 0;
        }
        finally
        {
            foreach (var process in running)
                process.Dispose();
        }
    }
}
