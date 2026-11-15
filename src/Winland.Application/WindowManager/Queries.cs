using System.Globalization;

namespace Winland.Application;

/// <summary>The pipe's requests: the queries, <c>dispatch</c> and <c>reload</c>.</summary>
public sealed partial class WindowManager
{
    // The last config Winland read, whose errors configerrors lists.
    private ConfigDocument? _loaded;

    private CommandReply ExecuteLine(CommandParse parse) =>
        parse switch
        {
            CommandRejected rejected => new CommandReply(rejected.Error, View([])),
            CommandParsed parsed => Reply(parsed.Request),
        };

    // A query's reply, or null for a request that is not one. A query answers from the current State and changes nothing.
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
            VersionRequest version => Json(version) ? CommandProtocol.FormatVersion() : CommandProtocol.FormatVersionText(),
            ConfigErrorsRequest errors => Json(errors)
                ? CommandProtocol.FormatErrors(_loaded?.Warnings ?? [])
                : Text(CommandProtocol.FormatErrorsText(_loaded?.Warnings ?? [], _loaded?.Path), "no config errors"),
            FilesRequest files => Json(files)
                ? CommandProtocol.FormatFiles(_loaded?.Path, LogFile?.Invoke())
                : CommandProtocol.FormatFilesText(_loaded?.Path, LogFile?.Invoke()),
            InspectRequest inspect => Inspect(inspect),
            _ => null,
        };

    // Each request's reply.
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

    // Winland answers in JSON with j/ and in text without it.
    private static bool Json(CommandRequest request) => request.Flags.HasFlag(CommandFlags.Json);

    // Winland's text reply for a query that finds nothing, such as "no open windows". With j/ it stays the empty JSON.
    private static string Text(string reply, string empty) => reply.Length == 0 ? empty : reply;

    private CommandReply Answer(string text) => new(text, View([]));

    private string Describe(Func<State, string> format) => format(Snapshot());

    private bool TrySelect(string selector, out Client client, out LiveWorkspace? desk)
    {
        client = null!;
        desk = null;
        WindowId? window;
        if (TryAddress(selector, out var addressed))
            window = addressed;
        else
            window = WindowSelector.Find(selector, ActiveWindow(), Snapshot().Clients);

        if (window is not WindowId id)
            return false;

        foreach (var candidate in _workspaces)
        {
            foreach (var item in candidate.Layout.Clients.Concat(candidate.WaitingClients))
            {
                if (item.Window != id)
                    continue;

                client = item;
                desk = candidate;
                return true;
            }
        }

        return false;
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
