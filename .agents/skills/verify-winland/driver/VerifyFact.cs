using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Winland.Verify.Tests;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraitAttribute : TestPropertyAttribute
{
    public TraitAttribute(string name, string value)
        : base(name, value)
    {
    }
}

public sealed class VerifyFactAttribute : TestMethodAttribute
{
    public VerifyFactAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }

    public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) =>
        VerifySkip.Reason(system: false) is string reason
            ? Task.FromResult(VerifySkip.Skipped(reason))
            : base.ExecuteAsync(testMethod);
}

public sealed class SystemVerifyFactAttribute : TestMethodAttribute
{
    public SystemVerifyFactAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }

    public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) =>
        VerifySkip.Reason(system: true) is string reason
            ? Task.FromResult(VerifySkip.Skipped(reason))
            : base.ExecuteAsync(testMethod);
}

internal static class VerifySkip
{
    public static string? Reason(bool system)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WINLAND_VERIFY"), "1", StringComparison.Ordinal))
            return "Set WINLAND_VERIFY=1 to run the feature drivers in a desktop session.";

        var running = Process.GetProcessesByName("winland");
        try
        {
            if (running.Length > 0)
                return "A Winland process is running. Stop it before running the feature drivers.";
        }
        finally
        {
            foreach (var process in running)
                process.Dispose();
        }

        if (DesktopSession.SkipReason() is string reason)
            return reason;

        if (system && !string.Equals(Environment.GetEnvironmentVariable("WINLAND_VERIFY_SYSTEM"), "1", StringComparison.Ordinal))
            return "This driver installs Winland or changes Windows settings for this user. Set WINLAND_VERIFY_SYSTEM=1 to let it, as CI does.";

        return null;
    }

    public static TestResult[] Skipped(string reason) =>
    [
        new TestResult
        {
            Outcome = UnitTestOutcome.Ignored,
            TestFailureException = new AssertInconclusiveException(reason),
        },
    ];
}
