namespace Winland.Daemon.Tests;

/// <summary><c>winland check [file]</c>: reads a config as a start would, and changes nothing.</summary>
public sealed class CheckCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "winland-check-" + Guid.NewGuid().ToString("N"));

    public CheckCommandTests() => Directory.CreateDirectory(_root);

    private string Default => Path.Combine(_root, "default", "winland.conf");

    [Fact]
    public void AConfigAndItsSourcesWithNoWarningsPass()
    {
        var path = Write("winland.conf", "source = binds.conf\ngeneral:gaps_in = 4\n");
        Write("binds.conf", "bind = SUPER, Q, killactive\n");

        var (exit, error) = Check(path);

        Assert.Equal(0, exit);
        Assert.Equal("", error);
    }

    [Fact]
    public void EachWarningIsPrintedWithItsFileAndLineAndTheCheckFails()
    {
        var marker = Path.Combine(_root, "launched");
        var path = Write("winland.conf", $"""
            source = binds.conf
            # hyprlang noerror true
            ghostkeyword = 1
            exec-once = cmd.exe /c type nul > "{marker}"
            """);
        var binds = Write("binds.conf", "bind = SUPER, Q, killactiv\nbind = SUPER, W, killactive\n");

        var (exit, error) = Check(path);

        Assert.Equal(1, exit);
        Assert.Equal(
            $"{binds}:1: Unknown dispatcher 'killactiv'. Did you mean 'killactive'?\n{path}:3: Unsupported keyword 'ghostkeyword'.\n",
            error);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void AMissingSourceFailsTheCheck()
    {
        var path = Write("winland.conf", "source = absent.conf\n");

        var (exit, error) = Check(path);

        Assert.Equal(1, exit);
        Assert.Equal($"{path}:1: Could not read source '{Path.Combine(_root, "absent.conf")}'.\n", error);
    }

    [Fact]
    public void ASourceWindowsCannotNameWarnsOnItsLineAndTheRestIsStillRead()
    {
        Directory.CreateDirectory(Path.Combine(_root, "conf"));
        var binds = Write(Path.Combine("conf", "binds.conf"), "bind = SUPER, Q, killactiv\n");
        var path = Write("winland.conf", $"""
            source = bad{'\0'}name.conf
            source = bad{'\0'}folder/*.conf
            source = conf/*{'\0'}.conf
            source = conf/*.conf
            ghostkeyword = 1
            """);
        var before = File.ReadAllBytes(path);

        var (exit, error) = Check(path);

        Assert.Equal(1, exit);
        Assert.Equal(
            $"""
            {path}:1: Could not read source '{_root}\bad{'\0'}name.conf'.
            {path}:2: Could not read source '{_root}\bad{'\0'}folder/*.conf'.
            {path}:3: Could not read source '{_root}\conf/*{'\0'}.conf'.
            {binds}:1: Unknown dispatcher 'killactiv'. Did you mean 'killactive'?
            {path}:5: Unsupported keyword 'ghostkeyword'.

            """.ReplaceLineEndings("\n"),
            error);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void AMissingConfigFailsWithoutWritingOne()
    {
        var named = Path.Combine(_root, "absent.conf");

        Assert.Equal((1, $"Could not read config '{named}'.\n"), Check(named));
        Assert.Equal((1, $"Could not read config '{Default}'.\n"), Check());
        Assert.False(File.Exists(named));
        Assert.False(File.Exists(Default));
    }

    [Fact]
    public void AnEmptyDefaultConfigPassesAndStaysEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Default)!);
        File.WriteAllBytes(Default, []);

        Assert.Equal((0, ""), Check());
        Assert.Empty(File.ReadAllBytes(Default));
    }

    [Theory]
    [InlineData("a.conf", "b.conf")]
    [InlineData("-j")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void AnythingButOneFileIsAUsageError(params string[] arguments)
    {
        var (exit, error) = Check(arguments);

        Assert.Equal(2, exit);
        Assert.Equal("usage: winland check [file]\n", error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void APathWindowsCannotNameFailsTheCheckWithoutWritingAnything()
    {
        var invalid = Path.Combine(_root, "bad\0name.conf");

        var (exit, error) = Check(invalid);

        Assert.Equal(1, exit);
        Assert.StartsWith($"Could not read config '{invalid}': ", error, StringComparison.Ordinal);
        Assert.EndsWith("\n", error, StringComparison.Ordinal);
        Assert.Single(error.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
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
