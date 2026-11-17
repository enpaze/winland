using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Winland.Domain;

internal static class WholeMatch
{
    private const int Limit = 512;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);
    private static readonly ConcurrentDictionary<string, Regex> Built = new(StringComparer.Ordinal);

    public static Regex Of(string pattern)
    {
        if (Built.TryGetValue(pattern, out var regex))
            return regex;

        _ = new Regex(pattern, RegexOptions.CultureInvariant);
        regex = Build($"\\A(?:{pattern})\\z");
        if (Built.Count >= Limit)
            Built.Clear();

        return Built.GetOrAdd(pattern, regex);
    }

    public static bool IsValid(string pattern)
    {
        try
        {
            _ = Of(pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool IsMatch(string pattern, string text)
    {
        try
        {
            return Of(pattern).IsMatch(text);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static Regex Build(string anchored)
    {
        try
        {
            return new Regex(anchored, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, Timeout);
        }
        catch (NotSupportedException)
        {
            return new Regex(anchored, RegexOptions.CultureInvariant, Timeout);
        }
    }
}
