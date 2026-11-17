namespace Winland.Domain;

internal static class LayoutFactory
{
    internal static IReadOnlyList<string> Names { get; } = ["dwindle", "scrolling", "floating"];

    public static Layout Create(ConfigDocument? config, string? ruleLayout) =>
        Fallback(ruleLayout ?? (config is null ? null : LayoutOptions.From(config).Layout)).Layout;

    public static Layout? Named(string name) => name.Trim() switch
    {
        var n when n.Equals("dwindle", StringComparison.OrdinalIgnoreCase) => new DwindleLayout(),
        var n when n.Equals("scrolling", StringComparison.OrdinalIgnoreCase) => new ScrollingLayout(),
        var n when n.Equals("floating", StringComparison.OrdinalIgnoreCase) => new FloatingLayout(),
        _ => null,
    };

    public static LayoutFallback Fallback(string? name)
    {
        var trimmed = name?.Trim();
        if (trimmed is null)
            return new LayoutFallback(new DwindleLayout(), null);

        return Named(trimmed) is Layout named
            ? new LayoutFallback(named, null)
            : new LayoutFallback(new DwindleLayout(), $"Layout '{trimmed}' is not available; using dwindle.");
    }

    public static string NameOf(Layout layout) => layout switch
    {
        ScrollingLayout => "scrolling",
        FloatingLayout => "floating",
        _ => "dwindle",
    };
}

internal readonly record struct LayoutFallback(Layout Layout, string? Warning);
