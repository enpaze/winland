namespace Winland.Domain;

internal static class OptionTable
{
    private static readonly Dictionary<string, IOption> ByName = Build(
        LayoutOptions.Options.All,
        ScrollingOptions.Options.All,
        DecorationOptions.Options.All,
        GroupOptions.Options.All,
        SessionOptions.Options.All,
        CursorOptions.Options.All,
        InputOptions.Options.All,
        AnimationOptions.Options.All,
        DebugOptions.Options.All);

    internal static IEnumerable<string> Names => ByName.Keys;

    internal static IOption? Find(string name) => ByName.GetValueOrDefault(name);

    private static Dictionary<string, IOption> Build(params IEnumerable<IOption>[] sections)
    {
        var table = new Dictionary<string, IOption>(StringComparer.Ordinal);
        foreach (var option in sections.SelectMany(section => section))
            table.Add(option.Name, option);

        return table;
    }
}
