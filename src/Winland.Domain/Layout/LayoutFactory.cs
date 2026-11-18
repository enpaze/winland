namespace Winland.Domain;

internal static class LayoutFactory
{
    internal static IReadOnlyList<string> Names { get; } = ["dwindle", "scrolling", "floating"];

    public static Layout Create(ConfigDocument? config) =>
        Fallback(config is null ? null : LayoutOptions.From(config).Layout);

    public static Layout? Named(string name) => name.Trim() switch
    {
        var n when n.Equals("dwindle", StringComparison.OrdinalIgnoreCase) => new DwindleLayout(),
        var n when n.Equals("scrolling", StringComparison.OrdinalIgnoreCase) => new ScrollingLayout(),
        var n when n.Equals("floating", StringComparison.OrdinalIgnoreCase) => new FloatingLayout(),
        _ => null,
    };

    public static Layout Fallback(string? name) =>
        (name is null ? null : Named(name)) ?? new DwindleLayout();

    public static string NameOf(Layout layout) => layout switch
    {
        ScrollingLayout => "scrolling",
        FloatingLayout => "floating",
        _ => "dwindle",
    };
}
