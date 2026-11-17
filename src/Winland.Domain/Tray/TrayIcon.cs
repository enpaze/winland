using System.Globalization;

namespace Winland.Domain;

public sealed record TrayIcon(string Number)
{
    public static IReadOnlyList<TrayMenuItem> MenuFor(bool logExists)
    {
        List<TrayMenuItem> menu =
        [
            new(TrayCommand.Reload, "&Reload"),
            new(TrayCommand.EditConfig, "&Edit config"),
        ];
        if (logExists)
            menu.Add(new(TrayCommand.OpenLog, "Open &log"));

        menu.Add(new(TrayCommand.Quit, "&Quit"));
        return menu;
    }

    public string Tip => "Winland, Workspace " + Number;

    public static TrayIcon For(WorkspaceId workspace) => new(Text(workspace));

    public static string Text(WorkspaceId workspace)
    {
        if (workspace.TryGetNumber(out var number))
            return number.ToString(CultureInfo.InvariantCulture);

        const string Prefix = "special:";
        var name = workspace.Name;
        if (name.StartsWith(Prefix, StringComparison.Ordinal))
            name = name[Prefix.Length..];

        return name.Length == 0 ? "special" : name;
    }
}

public readonly record struct TrayMenuItem(TrayCommand Command, string Label);

public enum TrayCommand
{
    Reload,
    EditConfig,
    OpenLog,
    Quit,
}
