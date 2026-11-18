using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Winland.Application;

public static partial class CommandProtocol
{
    public static IReadOnlyList<string> FormatStream(IReadOnlyList<StreamEvent> events, State state, CommandFlags flags = CommandFlags.None)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(state);
        if (events.Count == 0)
            return [];

        var indexed = Index(state);
        var json = flags.HasFlag(CommandFlags.Json);
        var lines = new List<string>(events.Count);
        foreach (var streamEvent in events)
        {
            if (streamEvent is MonitorLayoutChanged)
                continue;

            var line = new EventLine(json);
            Write(line, streamEvent, state, indexed);
            lines.Add(line.Finish());
        }

        return lines;
    }

    private static void Write(EventLine line, StreamEvent streamEvent, State state, Indexed indexed)
    {
        switch (streamEvent)
        {
            case ClientOpened opened:
                line.Name("openwindow").Address(opened.Window).Workspace(opened.Workspace, indexed)
                    .Text("monitor", MonitorName(state, opened.Workspace)).Text("class", opened.Class).Text("title", opened.Title);
                break;
            case ClientClosed closed:
                line.Name("closewindow").Address(closed.Window);
                break;
            case FocusChanged focused:
                line.Name("focus").Text("monitor", focused.Monitor).Workspace(focused.Workspace, indexed)
                    .Window(focused.Window is WindowId window ? state.ClientOf(window) ?? new Client(default, window) : null);
                break;
            case ClientRetitled retitled:
                line.Name("windowtitle").Address(retitled.Window).Text("title", retitled.Title);
                break;
            case ClientUrgent urgent:
                line.Name("urgent").Address(urgent.Window);
                break;
            case ClientMoved moved:
                line.Name("movewindow").Address(moved.Window).Workspace(moved.Workspace, indexed)
                    .Text("monitor", MonitorName(state, moved.Workspace));
                break;
            case FloatingChanged floating:
                line.Name("changefloatingmode").Address(floating.Window).Flag("floating", floating.Floating);
                break;
            case ClientMinimized minimized:
                line.Name("minimized").Address(minimized.Window).Flag("minimized", minimized.Minimized);
                break;
            case ClientPinned pinned:
                line.Name("pin").Address(pinned.Window).Flag("pinned", pinned.Pinned);
                break;
            case GroupToggled toggled:
                line.Name("togglegroup").Flag("created", toggled.Created).Address(toggled.Window);
                break;
            case ClientIntoGroup into:
                line.Name("moveintogroup").Address(into.Window);
                break;
            case ClientOutOfGroup outOf:
                line.Name("moveoutofgroup").Address(outOf.Window);
                break;
            case WorkspaceActivated activated:
                line.Name("workspace").Workspace(activated.Workspace, indexed).Text("monitor", MonitorName(state, activated.Workspace));
                break;
            case WorkspaceCreated created:
                line.Name("createworkspace").Workspace(created.Workspace, indexed).Text("monitor", MonitorName(state, created.Workspace));
                break;
            case WorkspaceDestroyed destroyed:
                line.Name("destroyworkspace").Workspace(destroyed.Id, destroyed.Name).Text("monitor", destroyed.Monitor);
                break;
            case WorkspaceMoved movedWorkspace:
                line.Name("moveworkspace").Workspace(movedWorkspace.Workspace, indexed).Text("monitor", movedWorkspace.Monitor);
                break;
            case FullscreenChanged fullscreen:
                line.Name("fullscreen").Flag("fullscreen", fullscreen.Entered);
                break;
            case ActiveSpecial special:
                line.Name("activespecial");
                if (special.Workspace is WorkspaceId specialWorkspace)
                    line.Workspace(specialWorkspace, indexed);
                else
                    line.NoWorkspace();

                line.Text("monitor", special.Monitor);
                break;
            case ConfigWarned warned:
                line.Name("warning").Number("line", warned.Line).Text("file", warned.Source).Text("message", warned.Message);
                break;
            case ConfigReloaded:
                line.Name("configreloaded");
                break;
            case MonitorAdded added:
                line.Name("monitoradded").Number("id", added.Id).Text("name", added.Name).Text("description", added.Description);
                break;
            case MonitorRemoved removed:
                line.Name("monitorremoved").Number("id", removed.Id).Text("name", removed.Name).Text("description", removed.Description);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(streamEvent), streamEvent, "No format for this event.");
        }
    }

    internal static string EventAddress(WindowId window) =>
        window.Value.ToString("x", CultureInfo.InvariantCulture);

    private sealed class EventLine(bool json)
    {
        private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private readonly StringBuilder _text = new();
        private readonly JsonObject? _json = json ? [] : null;
        private bool _first = true;

        public EventLine Name(string name)
        {
            if (_json is null)
                _text.Append(name).Append(">>");
            else
                _json["event"] = name;

            return this;
        }

        public EventLine Text(string key, string? value) =>
            _json is null ? Field(Clean(value)) : Set(key, value);

        public EventLine Number(string key, long value) =>
            _json is null ? Field(value.ToString(CultureInfo.InvariantCulture)) : Set(key, value);

        public EventLine Flag(string key, bool value) =>
            _json is null ? Field(value ? "1" : "0") : Set(key, value);

        public EventLine Address(WindowId window) =>
            _json is null ? Field(EventAddress(window)) : Set("address", window.Address);

        public EventLine Workspace(WorkspaceId workspace, Indexed indexed) =>
            Workspace(WorkspaceNumber(workspace, indexed), workspace.Name);

        public EventLine Workspace(long id, string name) =>
            _json is null
                ? Field(id.ToString(CultureInfo.InvariantCulture)).Field(Clean(name))
                : Set("workspace", new JsonObject { ["id"] = id, ["name"] = name });

        public EventLine NoWorkspace() =>
            _json is null ? Field("").Field("") : Set("workspace", null);

        public EventLine Window(Client? client)
        {
            if (_json is not null)
            {
                return Set("window", client is null ? null : new JsonObject
                {
                    ["address"] = client.Window.Address,
                    ["title"] = client.Title,
                    ["class"] = client.Class,
                    ["exe"] = client.Exe,
                });
            }

            return client is null ? Field("").Field("") : Field(EventAddress(client.Window)).Field(Clean(client.Title));
        }

        public string Finish() => _json?.ToJsonString(Relaxed) ?? _text.ToString();

        private EventLine Set(string key, JsonNode? value)
        {
            _json![key] = value;
            return this;
        }

        private EventLine Field(string text)
        {
            if (!_first)
                _text.Append(',');

            _first = false;
            _text.Append(text);
            return this;
        }
    }
}
