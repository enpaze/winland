namespace Winland.Domain;

internal static class Tokens
{
    public static string[] Of(string text) =>
        text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

    public static string First(string text)
    {
        var tokens = Of(text);
        return tokens.Length == 0 ? "" : tokens[0];
    }
}
