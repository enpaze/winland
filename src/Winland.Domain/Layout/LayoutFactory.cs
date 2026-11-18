namespace Winland.Domain;

internal static class LayoutFactory
{
    private static readonly (string Name, Type Type, Func<Layout> Create)[] Layouts =
    [
        ("dwindle", typeof(DwindleLayout), () => new DwindleLayout()),
        ("scrolling", typeof(ScrollingLayout), () => new ScrollingLayout()),
        ("floating", typeof(FloatingLayout), () => new FloatingLayout()),
    ];

    internal static IReadOnlyList<string> Names { get; } = [.. Layouts.Select(layout => layout.Name)];

    public static Layout Create(ConfigDocument? config) =>
        Fallback(config is null ? null : LayoutOptions.From(config).Layout);

    public static Layout? Named(string name)
    {
        var trimmed = name.Trim();
        foreach (var layout in Layouts)
        {
            if (trimmed.Equals(layout.Name, StringComparison.OrdinalIgnoreCase))
                return layout.Create();
        }

        return null;
    }

    public static Layout Fallback(string? name) =>
        (name is null ? null : Named(name)) ?? new DwindleLayout();

    public static string NameOf(Layout layout) =>
        Array.Find(Layouts, entry => entry.Type == layout.GetType()).Name ?? "dwindle";
}
