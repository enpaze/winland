namespace Winland.Domain;

internal static class MonitorRules
{
    internal static bool Matches(Monitor monitor, string selector)
    {
        const string Prefix = "desc:";
        if (selector.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var description = selector[Prefix.Length..].Trim();
            return monitor.Description.StartsWith(description, StringComparison.Ordinal);
        }

        return string.Equals(monitor.Name, selector.Trim(), StringComparison.OrdinalIgnoreCase);
    }

}
