namespace Winland.Domain;

public interface IConfigSource
{
    string? Read(string path);

    IReadOnlyList<string> Match(string pattern) => [];

    string? Variable(string name) => null;
}
