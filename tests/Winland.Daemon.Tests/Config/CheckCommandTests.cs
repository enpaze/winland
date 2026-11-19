namespace Winland.Daemon.Tests;

[TestClass]
public sealed class CheckCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "winland-check-" + Guid.NewGuid().ToString("N"));

    public CheckCommandTests() => Directory.CreateDirectory(_root);

    private string Default => Path.Combine(_root, "default", "winland.conf");

    [TestMethod]
    public void AConfigAndItsSourcesWithNoWarningsPass()
    {
        var path = Write("winland.conf", "source = binds.conf\ngaps_in = 4\n");
        Write("binds.conf", "bind = Win+Q, close\n");

        var (exit, error) = Check(path);

        Assert.AreEqual(0, exit);
        Assert.AreEqual("", error);
    }

    [TestMethod]
    public void EachWarningIsPrintedWithItsFileAndLineAndTheCheckFails()
    {
        var marker = Path.Combine(_root, "launched");
        var path = Write("winland.conf", $"""
            source = binds.conf
            # hyprlang noerror true
            ghostkeyword = 1
            exec-once = cmd.exe /c type nul > "{marker}"
            """);
        var binds = Write("binds.conf", "bind = Win+Q, closs\nbind = Win+W, close\n");

        var (exit, error) = Check(path);

        Assert.AreEqual(1, exit);
        Assert.AreEqual(
            $"{binds}:1: Unknown dispatcher 'closs'. Did you mean 'close'?\n{path}:3: Unsupported keyword 'ghostkeyword'.\n",
            error);
        Assert.IsFalse(File.Exists(marker));
    }

    [TestMethod]
    public void AMissingSourceFailsTheCheck()
    {
        var path = Write("winland.conf", "source = absent.conf\n");

        var (exit, error) = Check(path);

        Assert.AreEqual(1, exit);
        Assert.AreEqual($"{path}:1: Could not read source '{Path.Combine(_root, "absent.conf")}'.\n", error);
    }

    [TestMethod]
    public void ASourceWindowsCannotNameWarnsOnItsLineAndTheRestIsStillRead()
    {
        Directory.CreateDirectory(Path.Combine(_root, "conf"));
        var binds = Write(Path.Combine("conf", "binds.conf"), "bind = Win+Q, closs\n");
        var path = Write("winland.conf", $"""
            source = bad{'\0'}name.conf
            source = bad{'\0'}folder/*.conf
            source = conf/*{'\0'}.conf
            source = conf/*.conf
            ghostkeyword = 1
            """);
        var before = File.ReadAllBytes(path);

        var (exit, error) = Check(path);

        Assert.AreEqual(1, exit);
        Assert.AreEqual(
            $"""
            {path}:1: Could not read source '{_root}\bad{'\0'}name.conf'.
            {path}:2: Could not read source '{_root}\bad{'\0'}folder/*.conf'.
            {path}:3: Could not read source '{_root}\conf/*{'\0'}.conf'.
            {binds}:1: Unknown dispatcher 'closs'. Did you mean 'close'?
            {path}:5: Unsupported keyword 'ghostkeyword'.

            """.ReplaceLineEndings("\n"),
            error);
        Assert.AreSequenceEqual(before, File.ReadAllBytes(path));
    }

    [TestMethod]
    public void AMissingConfigFailsWithoutWritingOne()
    {
        var named = Path.Combine(_root, "absent.conf");

        Assert.AreEqual((1, $"Could not read config '{named}'.\n"), Check(named));
        Assert.AreEqual((1, $"Could not read config '{Default}'.\n"), Check());
        Assert.IsFalse(File.Exists(named));
        Assert.IsFalse(File.Exists(Default));
    }

    [TestMethod]
    public void AnEmptyDefaultConfigPassesAndStaysEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Default)!);
        File.WriteAllBytes(Default, []);

        Assert.AreEqual((0, ""), Check());
        Assert.IsEmpty(File.ReadAllBytes(Default));
    }

    [TestMethod]
    [DataRow("a.conf", "b.conf")]
    [DataRow("-j")]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\t")]
    public void AnythingButOneFileIsAUsageError(params string[] arguments)
    {
        var (exit, error) = Check(arguments);

        Assert.AreEqual(2, exit);
        Assert.AreEqual("usage: winland check [file]\n", error);
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_root));
    }

    [TestMethod]
    public void APathWindowsCannotNameFailsTheCheckWithoutWritingAnything()
    {
        var invalid = Path.Combine(_root, "bad\0name.conf");

        var (exit, error) = Check(invalid);

        Assert.AreEqual(1, exit);
        Assert.StartsWith($"Could not read config '{invalid}': ", error, StringComparison.Ordinal);
        Assert.EndsWith("\n", error, StringComparison.Ordinal);
        Assert.ContainsSingle(error.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(_root));
    }

    private (int Exit, string Error) Check(params string[] arguments)
    {
        using var error = new StringWriter { NewLine = "\n" };
        var exit = Program.Check(arguments, Default, error);
        return (exit, error.ToString());
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
