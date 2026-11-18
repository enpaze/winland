using System.Globalization;

namespace Winland.Domain;

internal static class WindowSelector
{
    internal static WindowId? Find(string selector, WindowId? active, IReadOnlyList<Client> clients)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(clients);

        var (kind, argument) = Read(selector);
        return kind switch
        {
            Kind.Active => active,
            Kind.Floating => First(floating: true, active, clients),
            Kind.Tiled => First(floating: false, active, clients),
            Kind.Address => TryAddress(argument, out var window) ? FirstMatch(active, clients, client => client.Window == window) : null,
            Kind.Pid => FirstMatch(active, clients, client => client.ProcessId.ToString(CultureInfo.InvariantCulture) == argument),
            Kind.Title => FirstMatch(active, clients, client => FieldMatch.Is(argument, client.Title)),
            Kind.Exe => FirstMatch(active, clients, client => FieldMatch.Is(argument, client.Exe)),
            _ => FirstMatch(active, clients, client => FieldMatch.Is(argument, client.Class)),
        };
    }

    internal static string? Malformed(string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var (kind, argument) = Read(selector);
        return kind switch
        {
            Kind.Address when !TryAddress(argument, out _) => "address: takes a window address, such as address:0x2a",
            Kind.Pid when argument.Length == 0 || !argument.All(char.IsAsciiDigit) => "pid: takes a process id, such as pid:1234",
            _ => null,
        };
    }

    private enum Kind { Active, Floating, Tiled, Address, Pid, Class, Title, Exe }

    private static (Kind Kind, string Argument) Read(string selector)
    {
        var text = selector.Trim();
        switch (text)
        {
            case "" or "active":
                return (Kind.Active, "");
            case "floating":
                return (Kind.Floating, "");
            case "tiled":
                return (Kind.Tiled, "");
        }

        (string Prefix, Kind Kind)[] prefixed =
        [
            ("address:", Kind.Address),
            ("pid:", Kind.Pid),
            ("class:", Kind.Class),
            ("title:", Kind.Title),
            ("exe:", Kind.Exe),
        ];
        foreach (var (prefix, kind) in prefixed)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return (kind, text[prefix.Length..]);
        }

        return (Kind.Class, text);
    }

    private static bool TryAddress(string text, out WindowId window)
    {
        window = default;
        if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;

        window = new WindowId(value);
        return true;
    }

    private static WindowId? FirstMatch(WindowId? active, IReadOnlyList<Client> clients, Func<Client, bool> match)
    {
        WorkspaceId? home = null;
        if (active is WindowId focused)
        {
            foreach (var client in clients)
            {
                if (client.Window != focused)
                    continue;

                home = client.Workspace;
                break;
            }
        }

        WindowId? elsewhere = null;
        foreach (var client in clients)
        {
            if (!match(client))
                continue;

            if (home is WorkspaceId workspace && client.Workspace == workspace)
                return client.Window;

            elsewhere ??= client.Window;
        }

        return elsewhere;
    }

    private static WindowId? First(bool floating, WindowId? active, IReadOnlyList<Client> clients)
    {
        if (active is not WindowId focused)
            return null;

        WorkspaceId? workspace = null;
        foreach (var client in clients)
        {
            if (client.Window == focused)
            {
                workspace = client.Workspace;
                break;
            }
        }

        if (workspace is not WorkspaceId home)
            return null;

        foreach (var client in clients)
        {
            if (!client.Minimized && client.Floating == floating && client.Workspace == home)
                return client.Window;
        }

        return null;
    }
}
