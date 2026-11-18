namespace Winland.Install.Tests;

[TestClass]
public class RelaunchPlanTests
{
    [TestMethod]
    public void RelaunchSetsDotnetRootFromTheRuntimeDirectoryAndKeepsTheWorkingDirectory()
    {
        var spec = RelaunchPlan.Create(
            @"C:\Tools\winland.exe",
            ["--winland-elevated", "--config", @"C:\temp\winland.conf"],
            @"C:\temp\session",
            runtimeDirectory: @"C:\Users\dante\AppData\Local\mise\dotnet-root\shared\Microsoft.NETCore.App\10.0.12\",
            dotnetRoot: null,
            pipe: "winland-hand",
            state: @"C:\temp\session\state.json");

        Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "cmd.exe"), spec.FileName);
        Assert.AreEqual(@"C:\temp\session", spec.WorkingDirectory);
        Assert.AreEqual(
            "/d /v:off /s /c \"cd /d \"C:\\temp\\session\" && set \"DOTNET_ROOT=C:\\Users\\dante\\AppData\\Local\\mise\\dotnet-root\" && set \"WINLAND_PIPE=winland-hand\" && set \"WINLAND_STATE=C:\\temp\\session\\state.json\" && \"C:\\Tools\\winland.exe\" --winland-elevated --config C:\\temp\\winland.conf\"",
            spec.Arguments);
    }

    [TestMethod]
    public void ExplicitDotnetRootWinsOverTheRuntimeDirectory()
    {
        var spec = RelaunchPlan.Create(
            @"C:\Tools\winland.exe",
            ["install", @"DESKTOP\dante"],
            @"C:\Tools",
            runtimeDirectory: @"C:\Users\dante\AppData\Local\mise\dotnet-root\shared\Microsoft.NETCore.App\10.0.12\",
            dotnetRoot: @"D:\dotnet sdk",
            pipe: null,
            state: null);

        Assert.AreEqual(
            "/d /v:off /s /c \"cd /d \"C:\\Tools\" && set \"DOTNET_ROOT=D:\\dotnet sdk\" && \"C:\\Tools\\winland.exe\" install DESKTOP\\dante\"",
            spec.Arguments);
    }

    [TestMethod]
    public void RelaunchQuotesPathsThatContainSpaces()
    {
        var spec = RelaunchPlan.Create(
            @"C:\Program Files\Winland\winland.exe",
            ["--config", @"C:\My Config\winland.conf"],
            @"C:\My Config",
            runtimeDirectory: null,
            dotnetRoot: @"D:\dotnet",
            pipe: null,
            state: @"C:\My Config\state.json");

        Assert.AreEqual(@"C:\My Config", spec.WorkingDirectory);
        Assert.AreEqual(
            "/d /v:off /s /c \"cd /d \"C:\\My Config\" && set \"WINLAND_STATE=C:\\My Config\\state.json\" && \"C:\\Program Files\\Winland\\winland.exe\" --config \"C:\\My Config\\winland.conf\"\"",
            spec.Arguments);
    }

    [TestMethod]
    [DataRow(@"C:\Program Files\Winland\winland\")]
    [DataRow(null)]
    public void SelfContainedOrUnknownBuildDoesNotSetDotnetRoot(string? runtimeDirectory)
    {
        var spec = RelaunchPlan.Create(
            @"C:\Program Files\Winland\winland\winland.exe",
            ["install"],
            @"C:\work",
            runtimeDirectory,
            dotnetRoot: @"D:\dotnet",
            pipe: null,
            state: null);

        Assert.DoesNotContain("DOTNET_ROOT", spec.Arguments, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(@"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.12\", true)]
    [DataRow(@"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.12", true)]
    [DataRow(@"C:\Program Files\Winland\winland\", false)]
    [DataRow(@"C:\shared\Other.App\10.0.12\", false)]
    [DataRow(null, false)]
    public void FrameworkDependentIsReadFromTheRuntimeDirectory(string? runtimeDirectory, bool frameworkDependent)
    {
        Assert.AreEqual(frameworkDependent, RelaunchPlan.IsFrameworkDependent(runtimeDirectory));
    }

    [TestMethod]
    public void TrailingBackslashesAreDoubledBeforeTheClosingQuote()
    {
        var spec = RelaunchPlan.Create(
            @"C:\Tools\winland.exe",
            ["--config", @"C:\My Config\", "next"],
            @"C:\work",
            runtimeDirectory: null,
            dotnetRoot: null,
            pipe: null,
            state: null);

        Assert.EndsWith(" --config \"C:\\My Config\\\\\" next\"", spec.Arguments, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("a\"b")]
    [DataRow("%PATH%")]
    [DataRow("50%")]
    [DataRow("two\nlines")]
    [DataRow("two\rlines")]
    public void AnArgumentThatCmdWouldReadIsRefused(string value)
    {
        Assert.ThrowsExactly<ArgumentException>(() => Plan(@"C:\Tools\winland.exe", [value], @"C:\work", null, null));
        Assert.ThrowsExactly<ArgumentException>(() => Plan(@"C:\Tools\" + value + ".exe", [], @"C:\work", null, null));
        Assert.ThrowsExactly<ArgumentException>(() => Plan(@"C:\Tools\winland.exe", [], @"C:\work\" + value, null, null));
        Assert.ThrowsExactly<ArgumentException>(() => Plan(@"C:\Tools\winland.exe", [], @"C:\work", value, null));
        Assert.ThrowsExactly<ArgumentException>(() => Plan(@"C:\Tools\winland.exe", [], @"C:\work", null, value));
    }

    [TestMethod]
    public void SpecialCharactersThatAreSafeInsideQuotesAreQuoted()
    {
        var spec = Plan(@"C:\Tools\winland.exe", ["a&b", "c|d", "e^f", "g!h"], @"C:\work", null, null);

        Assert.EndsWith(" \"a&b\" \"c|d\" \"e^f\" \"g!h\"\"", spec.Arguments, StringComparison.Ordinal);
    }

    private static RelaunchSpec Plan(string executable, string[] arguments, string workingDirectory, string? pipe, string? state) =>
        RelaunchPlan.Create(executable, arguments, workingDirectory, runtimeDirectory: null, dotnetRoot: null, pipe, state);

    [TestMethod]
    public void RelaunchWithoutARuntimeStillEntersTheWorkingDirectory()
    {
        var spec = RelaunchPlan.Create(
            @"C:\Tools\winland.exe",
            ["uninstall"],
            @"C:\work",
            runtimeDirectory: null,
            dotnetRoot: null,
            pipe: null,
            state: " ");

        Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "cmd.exe"), spec.FileName);
        Assert.AreEqual(
            "/d /v:off /s /c \"cd /d \"C:\\work\" && \"C:\\Tools\\winland.exe\" uninstall\"",
            spec.Arguments);
        Assert.AreEqual(@"C:\work", spec.WorkingDirectory);
    }
}
