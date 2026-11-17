namespace Winland.Application.Tests;

internal sealed class MemorySource(IReadOnlyDictionary<string, string> files) : IConfigSource
{
    public string? Read(string path) => files.TryGetValue(path, out var text) ? text : null;
}
