namespace Winland.Daemon;

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

    public static int Check(string path, TextWriter error)
    {
        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            error.WriteLine($"Could not read config '{path}': {exception.Message}");
            return 1;
        }

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

    public static void WriteWarnings(IEnumerable<ConfigWarning> warnings, string path, TextWriter error)
    {
        foreach (var warning in warnings)
            error.WriteLine($"{warning.Source ?? path}:{warning.Line}: {warning.Message}");
    }

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
