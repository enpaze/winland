using System.Globalization;

namespace Winland.Domain;

/// <summary>
/// The properties a window rule can give a window, and how a rule's flag is read.
/// </summary>
internal static class WindowProps
{
    private static readonly Dictionary<string, WindowProp> ByName =
        WindowProp.All.ToDictionary(prop => prop.Name, StringComparer.Ordinal);

    /// <summary>Whether a window rule named <paramref name="name"/> only sets that <paramref name="kind"/> of property.</summary>
    public static bool IsRuleEffect(string name, PropKind kind) =>
        ByName.TryGetValue(name, out var prop) && prop.IsRuleEffect && prop.Kind == kind;

    /// <summary>Winland's truthy: <c>1</c>, or text starting with <c>true</c>, <c>yes</c> or <c>on</c>.</summary>
    public static bool Truthy(string text)
    {
        if (text == "1")
            return true;

        var lower = text.ToLowerInvariant();
        return lower.StartsWith("true", StringComparison.Ordinal)
            || lower.StartsWith("yes", StringComparison.Ordinal)
            || lower.StartsWith("on", StringComparison.Ordinal);
    }
}

/// <summary>One opacity fraction, as a window's opacity props hold it.</summary>
internal readonly record struct PropAlpha(float Value)
{
    public static PropAlpha Identity { get; } = new(1f);
}

/// <summary>A min_size or max_size: a width and height in pixels, either of them infinite.</summary>
internal readonly record struct PropSize(double Width, double Height)
{
    public static PropSize Unbounded { get; } = new(double.PositiveInfinity, double.PositiveInfinity);

    /// <summary>The size as a Layout limit. An infinite or out of range side is no limit.</summary>
    public Size? ToLimit() =>
        double.IsFinite(Width) && double.IsFinite(Height) && Width is >= 0 and <= int.MaxValue && Height is >= 0 and <= int.MaxValue
            ? new Size((int)Math.Round(Width), (int)Math.Round(Height))
            : null;
}
