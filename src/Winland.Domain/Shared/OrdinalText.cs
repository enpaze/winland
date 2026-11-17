namespace Winland.Domain;

internal static class OrdinalText
{
    public static bool Is(string text, string expected) =>
        text.Equals(expected, StringComparison.OrdinalIgnoreCase);
}
