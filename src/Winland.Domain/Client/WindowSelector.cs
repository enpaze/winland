using System.Globalization;

namespace Winland.Domain;

/// <summary>
/// Resolves a window selector against Clients on any Workspace.
/// A match on the active window's Workspace wins when several Clients match.
/// </summary>
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

            // The stable id clients reports, in hex, compared exactly.
            Kind.StableId => FirstMatch(active, clients, client => client.Id.Value.ToString("x", CultureInfo.InvariantCulture) == argument),
            Kind.Address => FirstMatch(active, clients, client => client.Window.Address == argument),
            Kind.Pid => FirstMatch(active, clients, client => client.ProcessId.ToString(CultureInfo.InvariantCulture) == argument),
            _ => FirstMatch(active, clients, client => Matches(Field(client, kind), argument)),
        };
    }

    /// <summary>
    /// Why <paramref name="selector"/> is not one Find can read, such as a pattern that is not a valid regular
    /// expression or a pid that is not a number, or null when it is. A well-formed selector may still match nothing.
    /// </summary>
    internal static string? Malformed(string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var (kind, argument) = Read(selector);
        return kind switch
        {
            Kind.StableId when argument.Length == 0 || !argument.All(char.IsAsciiHexDigit) =>
                "stableid: takes the stableID clients reports, such as stableid:2a",
            Kind.Address when !argument.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                || !long.TryParse(argument.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) =>
                "address: takes a window address, such as address:0x2a",
            Kind.Pid when argument.Length == 0 || !argument.All(char.IsAsciiDigit) => "pid: takes a process id, such as pid:1234",
            Kind.Class or Kind.InitialClass or Kind.Title or Kind.InitialTitle when !WholeMatch.IsValid(argument) =>
                $"'{argument}' is not a valid pattern",
            _ => null,
        };
    }

    private enum Kind { Active, Floating, Tiled, StableId, Address, Pid, Class, InitialClass, Title, InitialTitle }

    // What a selector asks for and the text after its prefix. Find and Malformed read selectors only through this.
    private static (Kind Kind, string Argument) Read(string selector)
    {
        var text = selector.Trim();
        if (text.Length == 0 || text.StartsWith("active", StringComparison.Ordinal))
            return (Kind.Active, "");

        if (text.StartsWith("floating", StringComparison.Ordinal))
            return (Kind.Floating, "");

        if (text.StartsWith("tiled", StringComparison.Ordinal))
            return (Kind.Tiled, "");

        (string Prefix, Kind Kind)[] prefixed =
        [
            ("stableid:", Kind.StableId),
            ("address:", Kind.Address),
            ("pid:", Kind.Pid),
            ("initialtitle:", Kind.InitialTitle),
            ("title:", Kind.Title),
            ("initialclass:", Kind.InitialClass),
            ("class:", Kind.Class),
        ];
        foreach (var (prefix, kind) in prefixed)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
                return (kind, text[prefix.Length..]);
        }

        // A bare pattern matches the class.
        return (Kind.Class, text);
    }

    private static string Field(Client client, Kind kind) => kind switch
    {
        Kind.Title => client.Title,
        Kind.InitialTitle => client.InitialTitle,
        Kind.InitialClass => client.InitialClass,
        _ => client.Class,
    };

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

    private static bool Matches(string text, string pattern) => WholeMatch.IsMatch(pattern, text);
}
