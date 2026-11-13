namespace Winland.Domain;

internal static class ConfigDirectives
{
    /// <summary>
    /// True for a <c># hyprlang if</c>, <c>else</c> or <c>endif</c> comment. Winland does not evaluate the condition, so
    /// the lines inside apply, and the marker warns.
    /// </summary>
    public static bool IsCondition(string raw)
    {
        var line = raw.Trim();
        if (line.Length == 0 || line[0] != '#')
            return false;

        var comment = line[1..].Trim();
        const string marker = "hyprlang";
        if (!comment.StartsWith(marker, StringComparison.Ordinal))
            return false;

        if (comment.Length > marker.Length && !char.IsWhiteSpace(comment[marker.Length]))
            return false;

        var parts = comment[marker.Length..].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts[0] is "if" or "else" or "endif";
    }
}
