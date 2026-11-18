using System.Globalization;
using System.Text;

namespace Winland.Application;

public static partial class CommandProtocol
{
    internal static string FormatClientsText(State state)
    {
        var text = new StringBuilder();
        foreach (var client in ListedClients(state))
            AppendClient(text, client);

        return text.ToString();
    }

    internal static string FormatActiveWindowText(State state, WindowId? active)
    {
        if (ActiveClient(state, active) is not { } client)
            return "";

        var text = new StringBuilder();
        AppendClient(text, client);
        return text.ToString();
    }

    internal static string FormatWorkspacesText(State state)
    {
        var text = new StringBuilder();
        foreach (var workspace in Index(state).Workspaces)
            AppendWorkspace(text, workspace);

        return text.ToString();
    }

    internal static string FormatActiveWorkspaceText(State state)
    {
        var text = new StringBuilder();
        foreach (var entry in ActiveWorkspaceEntries(state))
            AppendWorkspace(text, entry);

        return text.ToString();
    }

    internal static string FormatMonitorsText(State state, bool includeDisabled = false)
    {
        var monitors = System.Text.Json.JsonSerializer.Deserialize<MonitorJson[]>(FormatMonitors(state, includeDisabled)) ?? [];
        var text = new StringBuilder();
        foreach (var monitor in monitors)
            AppendMonitor(text, monitor);

        return text.ToString();
    }

    private static void AppendClient(StringBuilder text, ClientJson client)
    {
        var grouped = client.Grouped.Length == 0 ? "0" : string.Join(',', client.Grouped.Select(Bare));
        text.Append(CultureInfo.InvariantCulture, $"Window {Bare(client.Address)} -> {Clean(client.Title)}:\n")
            .Append(CultureInfo.InvariantCulture, $"\tmapped: {Bit(client.Mapped)}\n\thidden: {Bit(client.Hidden)}\n\tvisible: {Bit(client.Visible)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tacceptsInput: {Bit(client.AcceptsInput)}\n\tat: {client.At[0]},{client.At[1]}\n\tsize: {client.Size[0]},{client.Size[1]}\n")
            .Append(CultureInfo.InvariantCulture, $"\tworkspace: {client.Workspace.Address} ({client.Workspace.Name}) on monitor {client.Workspace.Monitor}\n\tfloating: {Bit(client.Floating)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tmonitor: {client.Monitor}\n\tclass: {Clean(client.Class)}\n\ttitle: {Clean(client.Title)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tinitialClass: {Clean(client.InitialClass)}\n\tinitialTitle: {Clean(client.InitialTitle)}\n\tpid: {client.Pid}\n")
            .Append(CultureInfo.InvariantCulture, $"\tpinned: {Bit(client.Pinned)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tfullscreen: {client.Fullscreen}\n\tfullscreenClient: {client.FullscreenClient}\n\tfullscreenHandler: {client.FullscreenHandler}\n")
            .Append(CultureInfo.InvariantCulture, $"\tallowedOverFullscreen: {Bit(client.AllowedOverFullscreen)}\n\tgrouped: {grouped}\n")
            .Append(CultureInfo.InvariantCulture, $"\tstableID: {client.StableId}\n\n");
    }

    private static void AppendWorkspace(StringBuilder text, WorkspaceEntryJson workspace) =>
        text.Append(CultureInfo.InvariantCulture, $"workspace {workspace.Address} ({workspace.Name}) on monitor {workspace.Monitor}:\n")
            .Append(CultureInfo.InvariantCulture, $"\tmonitorID: {workspace.MonitorId}\n\twindows: {workspace.Windows}\n\thasfullscreen: {Bit(workspace.HasFullscreen)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tlastwindow: {workspace.LastWindow}\n\tlastwindowtitle: {Clean(workspace.LastWindowTitle)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tispersistent: {Bit(workspace.IsPersistent)}\n\ttiledLayout: {workspace.TiledLayout}\n\n");

    private static void AppendMonitor(StringBuilder text, MonitorJson monitor)
    {
        var reserved = monitor.Reserved;
        text.Append(CultureInfo.InvariantCulture, $"Monitor {monitor.Name} (ID {monitor.Id}):\n")
            .Append(CultureInfo.InvariantCulture, $"\t{monitor.Width}x{monitor.Height}@{monitor.RefreshRate:F5} at {monitor.X}x{monitor.Y}\n")
            .Append(CultureInfo.InvariantCulture, $"\tdescription: {monitor.Description}\n\tmake: {monitor.Make}\n\tmodel: {monitor.Model}\n")
            .Append(CultureInfo.InvariantCulture, $"\tphysical size (mm): {monitor.PhysicalWidth}x{monitor.PhysicalHeight}\n\tserial: {monitor.Serial}\n")
            .Append(CultureInfo.InvariantCulture, $"\tactive workspace: {monitor.ActiveWorkspace.Address} ({monitor.ActiveWorkspace.Name})\n")
            .Append(CultureInfo.InvariantCulture, $"\tspecial workspace: {monitor.SpecialWorkspace.Address} ({monitor.SpecialWorkspace.Name})\n")
            .Append(CultureInfo.InvariantCulture, $"\treserved: {reserved[0]} {reserved[1]} {reserved[2]} {reserved[3]}\n\tscale: {monitor.Scale}\n")
            .Append(CultureInfo.InvariantCulture, $"\ttransform: {monitor.Transform}\n\tfocused: {YesNo(monitor.Focused)}\n\tdpmsStatus: {Bit(monitor.DpmsStatus)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tvrr: {Word(monitor.Vrr)}\n\tsolitary: {monitor.Solitary}\n\tsolitaryBlockedBy: {Reasons(monitor.SolitaryBlockedBy)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tactivelyTearing: {Word(monitor.ActivelyTearing)}\n\ttearingBlockedBy: {Reasons(monitor.TearingBlockedBy)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tdirectScanoutTo: {monitor.DirectScanoutTo}\n\tdirectScanoutBlockedBy: {Reasons(monitor.DirectScanoutBlockedBy)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tdisabled: {Word(monitor.Disabled)}\n\tcurrentFormat: {monitor.CurrentFormat}\n\tmirrorOf: {monitor.MirrorOf}\n")
            .Append(CultureInfo.InvariantCulture, $"\tavailableModes: {string.Join(' ', monitor.AvailableModes)}\n\tcolorManagementPreset: {monitor.ColorManagementPreset}\n")
            .Append(CultureInfo.InvariantCulture, $"\tsdrBrightness: {monitor.SdrBrightness}\n\tsdrSaturation: {monitor.SdrSaturation}\n")
            .Append(CultureInfo.InvariantCulture, $"\tsdrMinLuminance: {monitor.SdrMinLuminance}\n\tsdrMaxLuminance: {monitor.SdrMaxLuminance}\n")
            .Append(CultureInfo.InvariantCulture, $"\thardwareCursorsInUse: {Word(monitor.HardwareCursorsInUse)}\n")
            .Append(CultureInfo.InvariantCulture, $"\thardwareDetails:\n\t\tbackend: {monitor.HardwareDetails.Backend}\n\t\thdr: {YesNo(monitor.HardwareDetails.Hdr)}\n")
            .Append(CultureInfo.InvariantCulture, $"\t\tchroma: {YesNo(monitor.HardwareDetails.Chroma)}\n\t\tbt2020: {YesNo(monitor.HardwareDetails.Bt2020)}\n")
            .Append(CultureInfo.InvariantCulture, $"\t\tvrrCapable: {YesNo(monitor.HardwareDetails.VrrCapable)}\n\n");
    }

    private static string Bare(string address) =>
        address.StartsWith("0x", StringComparison.Ordinal) ? address[2..] : address;

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Word(bool value) => value ? "true" : "false";

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string Reasons(string[]? reasons) => reasons is null ? "null" : string.Join(',', reasons);
}
