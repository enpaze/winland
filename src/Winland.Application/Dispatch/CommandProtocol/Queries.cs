using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winland.Application;

public static partial class CommandProtocol
{
    internal static string FormatClients(State state) =>
        JsonSerializer.Serialize(ListedClients(state));

    internal static string FormatWorkspaces(State state) =>
        JsonSerializer.Serialize(Index(state).Workspaces);

    internal static string FormatActiveWorkspace(State state) =>
        ActiveWorkspaceEntries(state).FirstOrDefault() is { } entry ? JsonSerializer.Serialize(entry) : "{}";

    internal static string FormatMonitors(State state, bool includeDisabled = false)
    {
        var indexed = Index(state);
        if (state.Screens.Count == 0)
        {
            var active = indexed.ByWorkspace[state.ActiveWorkspace];
            var synthetic = new Monitor("", "", 0, 0, 0, 0, 1, true, default);
            return JsonSerializer.Serialize<MonitorJson[]>([
                DescribeMonitor(state.ActiveMonitor.Value, synthetic, true, active, SpecialJson(state.SpecialWorkspace, indexed), "none"),
            ]);
        }

        var views = new List<MonitorJson>(state.Screens.Count);
        foreach (var screen in state.Screens)
        {
            if (!includeDisabled && !screen.Monitor.Enabled)
                continue;

            views.Add(DescribeMonitor(
                screen.Id.Value,
                screen.Monitor,
                screen.Focused,
                indexed.ByWorkspace[screen.ActiveWorkspace],
                SpecialJson(screen.SpecialWorkspace, indexed),
                MirrorOf(state, screen.Monitor)));
        }

        return JsonSerializer.Serialize(views);
    }

    internal static string FormatActiveWindow(State state, WindowId? active) =>
        ActiveClient(state, active) is { } client ? JsonSerializer.Serialize(client) : "{}";

    private static List<ClientJson> ListedClients(State state)
    {
        var indexed = Index(state);
        var stacked = new Dictionary<WindowId, int>(state.Stacking.Count);
        for (var i = 0; i < state.Stacking.Count; i++)
            stacked[state.Stacking[i]] = i;

        var views = new List<ClientJson>(state.Clients.Count);
        foreach (var client in state.Clients.OrderBy(client => stacked.TryGetValue(client.Window, out var at) ? at : -1))
        {
            if (!client.Minimized)
                views.Add(ClientOf(client, state, indexed));
        }

        return views;
    }

    private static ClientJson? ActiveClient(State state, WindowId? active) =>
        active is WindowId window && state.ClientOf(window) is Client client ? ClientOf(client, state, Index(state)) : null;

    private static IEnumerable<WorkspaceEntryJson> ActiveWorkspaceEntries(State state)
    {
        var indexed = Index(state);
        var active = indexed.ByWorkspace[state.ActiveWorkspace];
        return indexed.Workspaces.Where(entry => entry.Address == active.Address && entry.Monitor == active.Monitor && entry.Id == active.Id);
    }

    private static string[] GroupedOf(State state, WindowId window)
    {
        foreach (var group in state.Groups)
        {
            if (!group.Contains(window))
                continue;

            var addresses = new string[group.Count];
            for (var i = 0; i < group.Count; i++)
                addresses[i] = group[i].Address;
            return addresses;
        }

        return NoStrings;
    }

    private static ClientJson ClientOf(Client client, State state, Indexed indexed)
    {
        var hidden = client.Minimized || ListedHidden(state, client.Window);
        var shown = !hidden;
        var reachable = shown && !client.BelowFullscreen;
        return new(
            client.Window.Address,
            shown,
            hidden,
            reachable,
            reachable,
            [client.Bounds.X, client.Bounds.Y],
            [client.Bounds.Width, client.Bounds.Height],
            indexed.ByWorkspace[client.Workspace],
            client.Floating,
            MonitorOf(state, client.Workspace).Value,
            client.Class,
            client.Title,
            client.InitialClass,
            client.InitialTitle,
            client.ProcessId,
            client.Pinned,
            (int)client.Fullscreen,
            (int)client.Fullscreen,
            FullscreenHandlerOf(state, client),
            client.AllowedOverFullscreen,
            GroupedOf(state, client.Window),
            client.Id.Value.ToString("x", CultureInfo.InvariantCulture));
    }

    private static WorkspaceJson SpecialJson(WorkspaceId? workspace, Indexed indexed) =>
        workspace is WorkspaceId id && indexed.ByWorkspace.TryGetValue(id, out var described) ? described : NoSpecial;

    private static string MirrorOf(State state, Monitor monitor)
    {
        if (monitor.MirrorOf.Length == 0)
            return "none";

        foreach (var screen in state.Screens)
        {
            if (string.Equals(screen.Monitor.Name, monitor.MirrorOf, StringComparison.OrdinalIgnoreCase))
                return screen.Id.Value.ToString(CultureInfo.InvariantCulture);
        }

        return "none";
    }

    private static MonitorJson DescribeMonitor(int id, Monitor monitor, bool focused, WorkspaceJson active, WorkspaceJson special, string mirrorOf) =>
        new(
            id,
            monitor.Name,
            monitor.Description,
            monitor.Make,
            monitor.Model,
            monitor.Serial,
            monitor.Width,
            monitor.Height,
            monitor.PhysicalWidth,
            monitor.PhysicalHeight,
            monitor.RefreshRate,
            monitor.X,
            monitor.Y,
            active,
            special,
            Reserved(monitor),
            monitor.Scale,
            0,
            focused,
            monitor.Enabled,
            false,
            "0",
            null,
            false,
            null,
            "0",
            null,
            !monitor.Enabled && monitor.MirrorOf.Length == 0,
            DisplayColors.Format(monitor.BitsPerChannel),
            mirrorOf,
            Modes(monitor),
            monitor.ColorPreset.Length == 0 ? DisplayColors.StandardPreset : monitor.ColorPreset,
            monitor.SdrBrightness,
            monitor.SdrSaturation,
            monitor.SdrMinLuminance,
            monitor.SdrMaxLuminance,
            false,
            new HardwareDetailsJson("windows", monitor.HdrCapable, monitor.Chroma, monitor.WideCapable, false));

    private static int[] Reserved(Monitor monitor)
    {
        if (monitor.Width == 0 && monitor.Height == 0)
            return [0, 0, 0, 0];

        var work = monitor.WorkArea;
        return
        [
            work.X - monitor.X,
            work.Y - monitor.Y,
            monitor.X + monitor.Width - (work.X + work.Width),
            monitor.Y + monitor.Height - (work.Y + work.Height),
        ];
    }

    private static string[] Modes(Monitor monitor)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0 || !(monitor.RefreshRate > 0) || !double.IsFinite(monitor.RefreshRate))
            return NoStrings;

        return [FormattableString.Invariant($"{monitor.Width}x{monitor.Height}@{monitor.RefreshRate:0.00}Hz")];
    }

    internal static long WorkspaceNumber(State state, WorkspaceId id) => WorkspaceNumber(id, Index(state));

    private static long WorkspaceNumber(WorkspaceId id, Indexed indexed)
    {
        if (indexed.ByWorkspace.TryGetValue(id, out var described))
            return described.Order;

        if (id.TryGetNumber(out var number))
            return number;

        return 0;
    }

    private static string WorkspaceIdText(WorkspaceId id, Indexed indexed) =>
        WorkspaceNumber(id, indexed).ToString(CultureInfo.InvariantCulture);

    private static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
    }

    private static bool ListedHidden(State state, WindowId window)
    {
        foreach (var client in state.Hidden)
        {
            if (client.Window == window)
                return true;
        }

        return false;
    }

    private static Indexed Index(State state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var ids = new List<WorkspaceId>();
        var seen = new HashSet<WorkspaceId>();
        Add(state.ActiveWorkspace);
        foreach (var workspace in state.Workspaces)
            Add(workspace.Id);

        foreach (var client in state.Clients)
            Add(client.Workspace);

        foreach (var screen in state.Screens)
        {
            Add(screen.ActiveWorkspace);
            if (screen.SpecialWorkspace is WorkspaceId special)
                Add(special);
        }

        if (state.SpecialWorkspace is WorkspaceId focusedSpecial)
            Add(focusedSpecial);

        var named = new List<string>();
        var specials = new List<string>();
        foreach (var id in ids)
        {
            if (id.IsSpecial)
                specials.Add(id.Name);
            else if (!id.TryGetNumber(out _))
                named.Add(id.Name);
        }

        named.Sort(StringComparer.Ordinal);
        var namedIds = new Dictionary<string, int>(named.Count, StringComparer.Ordinal);
        var next = -1;
        foreach (var name in named)
            namedIds[name] = next--;

        specials.Sort(StringComparer.Ordinal);
        var specialIds = new Dictionary<string, int>(specials.Count, StringComparer.Ordinal);
        var nextSpecial = WorkspaceId.SpecialStart + 1;
        foreach (var name in specials)
        {
            if (name == WorkspaceId.DefaultSpecial.Name)
            {
                specialIds[name] = WorkspaceId.SpecialStart;
                continue;
            }

            specialIds[name] = nextSpecial;
            nextSpecial++;
        }

        var windows = new Dictionary<WorkspaceId, int>();
        var fullscreen = new HashSet<WorkspaceId>();
        foreach (var client in state.Clients)
        {
            windows[client.Workspace] = windows.GetValueOrDefault(client.Workspace) + 1;
            if (client.Fullscreen != FullscreenMode.None)
                fullscreen.Add(client.Workspace);
        }

        var workspaces = new Dictionary<WorkspaceId, Workspace>();
        foreach (var workspace in state.Workspaces)
            workspaces[workspace.Id] = workspace;

        var byWorkspace = new Dictionary<WorkspaceId, WorkspaceJson>(ids.Count);
        var counts = new List<WorkspaceEntryJson>(ids.Count);
        foreach (var id in ids)
        {
            var json = Describe(id, namedIds, specialIds, MonitorName(state, id));
            byWorkspace[id] = json;
            var known = workspaces.GetValueOrDefault(id);
            var lastAddress = "0x0";
            var lastTitle = "";
            if (known?.LastWindow is WindowId window)
            {
                lastAddress = window.Address;
                lastTitle = state.ClientOf(window)?.Title ?? "";
            }

            counts.Add(new WorkspaceEntryJson(
                json.Address,
                json.Id,
                json.Type,
                json.Name,
                MonitorName(state, id),
                MonitorOf(state, id).Value,
                windows.GetValueOrDefault(id),
                fullscreen.Contains(id),
                lastAddress,
                lastTitle,
                false,
                known is null ? "dwindle" : known.TiledLayout));
        }

        var keys = new (long Order, int Monitor)[counts.Count];
        for (var i = 0; i < counts.Count; i++)
            keys[i] = (byWorkspace[ids[i]].Order, counts[i].MonitorId);
        var entries = counts.ToArray();
        Array.Sort(keys, entries);
        return new Indexed(byWorkspace, [.. entries]);

        void Add(WorkspaceId id)
        {
            if (seen.Add(id))
                ids.Add(id);
        }
    }

    private static string FullscreenHandlerOf(State state, Client client)
    {
        if (client.LayoutFullscreen)
            return "scrolling";

        if (client.Floating || client.Fullscreen != FullscreenMode.None)
            return "default";

        foreach (var workspace in state.Workspaces)
        {
            if (workspace.Id == client.Workspace)
                return workspace.TiledLayout == "scrolling" ? "scrolling" : "default";
        }

        return "default";
    }

    private static MonitorId MonitorOf(State state, WorkspaceId id)
    {
        foreach (var workspace in state.Workspaces)
        {
            if (workspace.Id == id)
                return workspace.Monitor;
        }

        return state.ActiveMonitor;
    }

    private static string MonitorName(State state, WorkspaceId id) =>
        state.ScreenOf(MonitorOf(state, id))?.Monitor.Name ?? "";

    private static WorkspaceJson Describe(
        WorkspaceId id,
        Dictionary<string, int> namedIds,
        Dictionary<string, int> specialIds,
        string monitor)
    {
        var name = id.Name;
        if (id.IsSpecial)
            return new WorkspaceJson(id.Name, null, "special", name, monitor) { Order = specialIds[id.Name] };

        if (id.TryGetNumber(out var number))
            return new WorkspaceJson(id.Name, number, "normal", name, monitor) { Order = number };

        return new WorkspaceJson(id.Name, null, "normal", name, monitor) { Order = namedIds[id.Name] };
    }

    private static int IndexOfWhitespace(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
                return i;
        }

        return -1;
    }

    private sealed record Indexed(Dictionary<WorkspaceId, WorkspaceJson> ByWorkspace, List<WorkspaceEntryJson> Workspaces);

    private sealed record ClientJson(
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("mapped")] bool Mapped,
        [property: JsonPropertyName("hidden")] bool Hidden,
        [property: JsonPropertyName("visible")] bool Visible,
        [property: JsonPropertyName("acceptsInput")] bool AcceptsInput,
        [property: JsonPropertyName("at")] int[] At,
        [property: JsonPropertyName("size")] int[] Size,
        [property: JsonPropertyName("workspace")] WorkspaceJson Workspace,
        [property: JsonPropertyName("floating")] bool Floating,
        [property: JsonPropertyName("monitor")] int Monitor,
        [property: JsonPropertyName("class")] string Class,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("initialClass")] string InitialClass,
        [property: JsonPropertyName("initialTitle")] string InitialTitle,
        [property: JsonPropertyName("pid")] long Pid,
        [property: JsonPropertyName("pinned")] bool Pinned,
        [property: JsonPropertyName("fullscreen")] int Fullscreen,
        [property: JsonPropertyName("fullscreenClient")] int FullscreenClient,
        [property: JsonPropertyName("fullscreenHandler")] string FullscreenHandler,
        [property: JsonPropertyName("allowedOverFullscreen")] bool AllowedOverFullscreen,
        [property: JsonPropertyName("grouped")] string[] Grouped,
        [property: JsonPropertyName("stableId")] string StableId);

    private sealed record WorkspaceJson(
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("monitor")] string Monitor)
    {
        [JsonIgnore]
        public long Order { get; init; }
    }

    private sealed record WorkspaceEntryJson(
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("monitor")] string Monitor,
        [property: JsonPropertyName("monitorID")] int MonitorId,
        [property: JsonPropertyName("windows")] int Windows,
        [property: JsonPropertyName("hasfullscreen")] bool HasFullscreen,
        [property: JsonPropertyName("lastwindow")] string LastWindow,
        [property: JsonPropertyName("lastwindowtitle")] string LastWindowTitle,
        [property: JsonPropertyName("ispersistent")] bool IsPersistent,
        [property: JsonPropertyName("tiledLayout")] string TiledLayout);

    private sealed record MonitorJson(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("make")] string Make,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("serial")] string Serial,
        [property: JsonPropertyName("width")] int Width,
        [property: JsonPropertyName("height")] int Height,
        [property: JsonPropertyName("physicalWidth")] int PhysicalWidth,
        [property: JsonPropertyName("physicalHeight")] int PhysicalHeight,
        [property: JsonPropertyName("refreshRate")] double RefreshRate,
        [property: JsonPropertyName("x")] int X,
        [property: JsonPropertyName("y")] int Y,
        [property: JsonPropertyName("activeWorkspace")] WorkspaceJson ActiveWorkspace,
        [property: JsonPropertyName("specialWorkspace")] WorkspaceJson SpecialWorkspace,
        [property: JsonPropertyName("reserved")] int[] Reserved,
        [property: JsonPropertyName("scale")] double Scale,
        [property: JsonPropertyName("transform")] int Transform,
        [property: JsonPropertyName("focused")] bool Focused,
        [property: JsonPropertyName("dpmsStatus")] bool DpmsStatus,
        [property: JsonPropertyName("vrr")] bool Vrr,
        [property: JsonPropertyName("solitary")] string Solitary,
        [property: JsonPropertyName("solitaryBlockedBy")] string[]? SolitaryBlockedBy,
        [property: JsonPropertyName("activelyTearing")] bool ActivelyTearing,
        [property: JsonPropertyName("tearingBlockedBy")] string[]? TearingBlockedBy,
        [property: JsonPropertyName("directScanoutTo")] string DirectScanoutTo,
        [property: JsonPropertyName("directScanoutBlockedBy")] string[]? DirectScanoutBlockedBy,
        [property: JsonPropertyName("disabled")] bool Disabled,
        [property: JsonPropertyName("currentFormat")] string CurrentFormat,
        [property: JsonPropertyName("mirrorOf")] string MirrorOf,
        [property: JsonPropertyName("availableModes")] string[] AvailableModes,
        [property: JsonPropertyName("colorManagementPreset")] string ColorManagementPreset,
        [property: JsonPropertyName("sdrBrightness")] double SdrBrightness,
        [property: JsonPropertyName("sdrSaturation")] double SdrSaturation,
        [property: JsonPropertyName("sdrMinLuminance")] double SdrMinLuminance,
        [property: JsonPropertyName("sdrMaxLuminance")] int SdrMaxLuminance,
        [property: JsonPropertyName("hardwareCursorsInUse")] bool HardwareCursorsInUse,
        [property: JsonPropertyName("hardwareDetails")] HardwareDetailsJson HardwareDetails);

    private sealed record HardwareDetailsJson(
        [property: JsonPropertyName("backend")] string Backend,
        [property: JsonPropertyName("hdr")] bool Hdr,
        [property: JsonPropertyName("chroma")] bool Chroma,
        [property: JsonPropertyName("bt2020")] bool Bt2020,
        [property: JsonPropertyName("vrrCapable")] bool VrrCapable);
}
