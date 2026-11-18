using System.Globalization;

namespace Winland.Application;

public sealed partial class WindowManager
{
    private ConfigDocument? _loaded;

    private CommandReply ExecuteLine(CommandParse parse) =>
        parse switch
        {
            CommandRejected rejected => new CommandReply(rejected.Error, View([])),
            CommandParsed parsed => Reply(parsed.Request),
        };

    private string? Read(CommandRequest request) =>
        request switch
        {
            ClientsRequest clients => Describe(state => Json(clients)
                ? CommandProtocol.FormatClients(state)
                : Text(CommandProtocol.FormatClientsText(state), "no open windows")),
            WorkspacesRequest workspaces => Describe(state => Json(workspaces) ? CommandProtocol.FormatWorkspaces(state) : CommandProtocol.FormatWorkspacesText(state)),
            MonitorsRequest monitors => Describe(state => Json(monitors)
                ? CommandProtocol.FormatMonitors(state, monitors.All)
                : CommandProtocol.FormatMonitorsText(state, monitors.All)),
            ActiveWindowRequest active => Describe(state => Json(active)
                ? CommandProtocol.FormatActiveWindow(state, ActiveWindow())
                : Text(CommandProtocol.FormatActiveWindowText(state, ActiveWindow()), "Invalid")),
            ActiveWorkspaceRequest activeWorkspace => Describe(state => Json(activeWorkspace)
                ? CommandProtocol.FormatActiveWorkspace(state)
                : CommandProtocol.FormatActiveWorkspaceText(state)),
            BindsRequest binds => Json(binds)
                ? CommandProtocol.FormatBinds(_config.Binds.Binds)
                : Text(CommandProtocol.FormatBindsText(_config.Binds.Binds), "no binds"),
            ConfigErrorsRequest errors => Json(errors)
                ? CommandProtocol.FormatErrors(_loaded?.Warnings ?? [])
                : Text(CommandProtocol.FormatErrorsText(_loaded?.Warnings ?? [], _loaded?.Path), "no config errors"),
            FilesRequest files => Json(files)
                ? CommandProtocol.FormatFiles(_loaded?.Path, LogFile?.Invoke())
                : CommandProtocol.FormatFilesText(_loaded?.Path, LogFile?.Invoke()),
            InspectRequest inspect => Inspect(inspect),
            _ => null,
        };

    private CommandReply Reply(CommandRequest request)
    {
        if (Read(request) is { } text)
            return Answer(text);

        return request switch
        {
            DispatchRequest dispatch => DispatchReply(dispatch),
            EventsRequest => Answer("") with { Stream = true },
            ReloadRequest => ReloadCommand(),
            _ => throw new InvalidOperationException("No handler for this command."),
        };
    }

    private CommandReply DispatchReply(DispatchRequest dispatch)
    {
        var effect = Execute(dispatch.Name, dispatch.Arguments);
        return new CommandReply(effect.Error ?? "ok", effect);
    }

    private static bool Json(CommandRequest request) => request.Flags.HasFlag(CommandFlags.Json);

    private static string Text(string reply, string empty) => reply.Length == 0 ? empty : reply;

    private CommandReply Answer(string text) => new(text, View([]));

    private string Describe(Func<State, string> format) => format(Snapshot());

    private bool TrySelect(string selector, out Client client)
    {
        client = null!;
        WindowId? window;
        if (TryAddress(selector, out var addressed))
            window = addressed;
        else
            window = WindowSelector.Find(selector, ActiveWindow(), Snapshot().Clients);

        if (window is not WindowId id || Find(id) is not Client found)
            return false;

        client = found;
        return true;
    }

    private static bool TryAddress(string text, out WindowId window)
    {
        window = default;
        var hex = text.Trim();
        const string prefix = "address:";
        if (hex.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            hex = hex[prefix.Length..];

        if (!hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!long.TryParse(hex[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;

        window = new WindowId(value);
        return true;
    }
}
