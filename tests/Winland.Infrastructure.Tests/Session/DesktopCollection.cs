using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Winland.Infrastructure.Tests;

public class DesktopFactAttribute : TestMethodAttribute
{
    public DesktopFactAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }

    public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) =>
        DaemonRunning() ? Task.FromResult(Skipped()) : base.ExecuteAsync(testMethod);

    private static TestResult[] Skipped() =>
    [
        new TestResult
        {
            Outcome = UnitTestOutcome.Ignored,
            TestFailureException = new AssertInconclusiveException("A Winland process is running. It adopts and restores the windows this test opens. Stop it to run the test."),
        },
    ];

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
