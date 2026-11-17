using System.Text;

namespace Winland.Daemon;

static class DefaultConfig
{
    public const string ResourceName = "winland.default.conf";

    public static string Text { get; } = Read();

    public static bool WriteIfMissing(string path) => UserAccess.Run(() => Write(path));

    private static bool Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        if (new FileInfo(full) is { Exists: true, Length: > 0 })
            return false;
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        try
        {
            using var stream = new FileStream(full, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            if (stream.Length != 0)
                return false;
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(Text);
            return true;
        }
        catch (IOException) when (File.Exists(full))
        {
            return false;
        }
    }

    private static string Read()
    {
        var stream = typeof(DefaultConfig).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The default config is not embedded.");
        using (stream)
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            return reader.ReadToEnd();
    }
}
