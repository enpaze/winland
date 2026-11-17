namespace Winland.Domain;

public static class FontNames
{
    public const string Default = "Segoe UI";

    private static readonly Dictionary<string, string> Generic = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sans"] = Default,
        ["sans-serif"] = Default,
        ["sans serif"] = Default,
        ["system-ui"] = Default,
        ["serif"] = "Cambria",
        ["monospace"] = "Consolas",
        ["mono"] = "Consolas",
    };

    public static string Windows(string? family)
    {
        var name = family?.Trim() ?? "";
        if (name.Length == 0)
            return Default;

        return Generic.TryGetValue(name, out var mapped) ? mapped : name;
    }
}
