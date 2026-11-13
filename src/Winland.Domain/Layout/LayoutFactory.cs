namespace Winland.Domain;

/// <summary>Picks the Layout a Workspace uses by name, from <c>general:layout</c>, a workspace rule or the <c>layout</c> dispatcher.</summary>
internal static class LayoutFactory
{
    /// <summary>Every Layout name, in the order <c>cyclelayout</c> walks them.</summary>
    internal static IReadOnlyList<string> Names { get; } = ["dwindle", "scrolling", "floating"];

    /// <summary>
    /// Creates the Layout for a Workspace whose rule may name one, as Winland picks it: the rule's
    /// <c>layout</c>, or else <c>general:layout</c>. A name that is no Layout uses dwindle.
    /// </summary>
    public static Layout Create(ConfigDocument? config, string? ruleLayout) =>
        Fallback(ruleLayout ?? (config is null ? null : LayoutOptions.From(config).Layout)).Layout;

    /// <summary>A new Layout of the kind <paramref name="name"/> names, in any case, or null when it names none.</summary>
    public static Layout? Named(string name) => name.Trim() switch
    {
        var n when n.Equals("dwindle", StringComparison.OrdinalIgnoreCase) => new DwindleLayout(),
        var n when n.Equals("scrolling", StringComparison.OrdinalIgnoreCase) => new ScrollingLayout(),
        var n when n.Equals("floating", StringComparison.OrdinalIgnoreCase) => new FloatingLayout(),
        _ => null,
    };

    /// <summary>
    /// Resolves a configured Layout name. One of <see cref="Names"/> or a missing name selects a Layout and returns no
    /// warning; a missing name selects dwindle. Any other value keeps the dwindle Layout and returns a warning.
    /// </summary>
    public static LayoutFallback Fallback(string? name)
    {
        var trimmed = name?.Trim();
        if (trimmed is null)
            return new LayoutFallback(new DwindleLayout(), null);

        return Named(trimmed) is Layout named
            ? new LayoutFallback(named, null)
            : new LayoutFallback(new DwindleLayout(), $"Layout '{trimmed}' is not available; using dwindle.");
    }

    /// <summary>The name reported for <paramref name="layout"/>, one of <see cref="Names"/>.</summary>
    public static string NameOf(Layout layout) => layout switch
    {
        ScrollingLayout => "scrolling",
        FloatingLayout => "floating",
        _ => "dwindle",
    };
}

/// <summary>
/// The Layout a Workspace gets for a configured Layout name, and the warning when that name fell back to dwindle.
/// </summary>
internal readonly record struct LayoutFallback(Layout Layout, string? Warning);
