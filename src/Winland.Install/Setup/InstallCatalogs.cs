namespace Winland.Install;

public sealed record ListedFile(string RelativePath, bool ReparsePoint);

public sealed record Shortcut(string LinkPath, string Target, string Arguments, string AppUserModelId);

public enum RegistryRoot
{
    Machine,
    User,
}

public interface IFileTree
{
    bool Exists(string path);

    bool DirectoryExists(string path);

    IReadOnlyList<ListedFile> List(string directory);

    void PrepareDirectory(string directory);

    void Copy(string source, string destination);

    void DeleteFile(string path);

    void DeleteTree(string directory);
}

public interface IRegistry
{
    string? GetString(RegistryRoot root, string key, string name);

    void SetString(RegistryRoot root, string key, string name, string value, bool expandable);

    void SetDword(RegistryRoot root, string key, string name, int value);

    void DeleteKey(RegistryRoot root, string key);

    void DeleteValue(RegistryRoot root, string key, string name);
}

public interface IStartMenu
{
    void Place(Shortcut shortcut);

    void Remove(string linkPath);
}

public interface IDaemonSession
{
    bool Stop();

    void Start(string executable, IReadOnlyList<string> arguments);
}
