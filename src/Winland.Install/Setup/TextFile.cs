namespace Winland.Install;

public interface ITextFile
{
    string? Read();

    void Write(string text);

    void Delete();
}

public sealed class TextFile : ITextFile
{
    private readonly string path;

    public TextFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;
    }

    public string? Read() => File.Exists(path) ? File.ReadAllText(path) : null;

    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, text);
    }

    public void Delete()
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
