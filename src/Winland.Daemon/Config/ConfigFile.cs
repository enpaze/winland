namespace Winland.Daemon;

/// <summary>Reads the config file, and every file it sources, into a document.</summary>
static class ConfigFile
{
    public static ConfigDocument Read(string path)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full) ?? ".";
        var source = new FileConfigSource(directory);
        var text = source.Read(full)
            ?? throw new InvalidOperationException($"Could not read config '{full}'.");

        return ConfigParser.Parse(text, source, KeyboardLayout.Active(), full, WindowManager.DispatcherNames);
    }

    /// <summary>
    /// <c>winland check</c>: reads <paramref name="path"/> as a start does and writes each warning. 0 when there are
    /// none, and 1 for a warning or a config that does not read. It writes no file and runs nothing.
    /// </summary>
    public static int Check(string path, TextWriter error)
    {
        ConfigDocument document;
        try
        {
            document = Read(path);
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }

        WriteWarnings(document.Warnings, path, error);
        return document.Warnings.Count == 0 ? 0 : 1;
    }

    /// <summary>Each warning as <c>file:line: message</c>, where the file is <paramref name="path"/> for a line of the config itself.</summary>
    public static void WriteWarnings(IEnumerable<ConfigWarning> warnings, string path, TextWriter error)
    {
        foreach (var warning in warnings)
            error.WriteLine($"{warning.Source ?? path}:{warning.Line}: {warning.Message}");
    }

    /// <summary>A reload's read. A file that is gone or does not read keeps the config in force, and the warning says why.</summary>
    public static ConfigRead TryRead(string path)
    {
        try
        {
            return new ConfigRead(Read(path));
        }
        catch (InvalidOperationException exception)
        {
            return new ConfigRead(null, new ConfigWarning(0, exception.Message, Path.GetFullPath(path)));
        }
    }
}
