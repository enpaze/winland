using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Winland.Daemon.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[TestClass]
public class AdministratorFolderTests
{
    [ElevatedFact(true)]
    public void AnElevatedRunGetsAProtectedFolder()
    {
        InParent(parent =>
        {
            var folder = AdministratorFolder.CreateNew(parent, "task-");

            Assert.StartsWith(Path.Combine(parent, "task-"), folder, StringComparison.OrdinalIgnoreCase);
            Assert.IsNull(WindowsAccessReader.FirstNonAdministrator(folder));
        });
    }

    [ElevatedFact(false)]
    public void AnUnelevatedRunIsRefused()
    {
        InParent(parent =>
        {
            var refused = Assert.ThrowsExactly<InvalidOperationException>(() => AdministratorFolder.CreateNew(parent, "task-"));

            Assert.Contains("non-administrator", refused.Message, StringComparison.Ordinal);
        });
    }

    private static void InParent(Action<string> test)
    {
        var parent = Path.Combine(Path.GetTempPath(), "winland-admin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try
        {
            test(parent);
        }
        finally
        {
            foreach (var folder in Directory.GetDirectories(parent))
                Open(folder);

            Directory.Delete(parent, recursive: true);
        }
    }

    private sealed class ElevatedFactAttribute : TestMethodAttribute
    {
        private readonly bool _elevated;

        public ElevatedFactAttribute(bool elevated, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
            : base(callerFilePath, callerLineNumber)
        {
            _elevated = elevated;
        }

        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator) == _elevated)
                return base.ExecuteAsync(testMethod);

            var message = _elevated ? "Needs an elevated test run." : "Needs a test run without elevation.";
            return Task.FromResult<TestResult[]>(
            [
                new TestResult
                {
                    Outcome = UnitTestOutcome.Ignored,
                    TestFailureException = new AssertInconclusiveException(message),
                },
            ]);
        }
    }

    private static void Open(string folder)
    {
        var open = new DirectorySecurity();
        using var identity = WindowsIdentity.GetCurrent();
        open.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
        new DirectoryInfo(folder).SetAccessControl(open);
    }
}
