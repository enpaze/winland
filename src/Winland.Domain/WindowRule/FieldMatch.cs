namespace Winland.Domain;

internal static class FieldMatch
{
    public static bool Is(string pattern, string text) => Match(pattern.AsSpan(), text.AsSpan());

    private static bool Match(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text)
    {
        var star = pattern.IndexOf('*');
        if (star < 0)
            return pattern.Equals(text, StringComparison.OrdinalIgnoreCase);

        if (!text.StartsWith(pattern[..star], StringComparison.OrdinalIgnoreCase))
            return false;

        text = text[star..];
        pattern = pattern[(star + 1)..];
        while (pattern.Length > 0 && pattern[0] == '*')
            pattern = pattern[1..];

        if (pattern.Length == 0)
            return true;

        for (var i = 0; i <= text.Length; i++)
        {
            if (Match(pattern, text[i..]))
                return true;
        }

        return false;
    }
}
