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
        var text = new StringBuilder();
        foreach (var monitor in ListedMonitors(state, includeDisabled))
            AppendMonitor(text, monitor);

        return text.ToString();
    }

    private static void AppendClient(StringBuilder text, ClientJson client)
    {
        var grouped = client.Grouped.Length == 0 ? "0" : string.Join(',', client.Grouped.Select(Bare));
        text.Append(CultureInfo.InvariantCulture, $"Window {Bare(client.Address)} -> {Clean(client.Title)}:\n")
            .Append(CultureInfo.InvariantCulture, $"\thidden: {Bit(client.Hidden)}\n\tvisible: {Bit(client.Visible)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tat: {client.At[0]},{client.At[1]}\n\tsize: {client.Size[0]},{client.Size[1]}\n")
            .Append(CultureInfo.InvariantCulture, $"\tworkspace: {client.Workspace.Name}\n\tfloating: {Bit(client.Floating)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tmonitor: {client.Monitor}\n\tclass: {Clean(client.Class)}\n\ttitle: {Clean(client.Title)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tinitialClass: {Clean(client.InitialClass)}\n\tinitialTitle: {Clean(client.InitialTitle)}\n\tpid: {client.Pid}\n")
            .Append(CultureInfo.InvariantCulture, $"\tpinned: {Bit(client.Pinned)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tfullscreen: {client.Fullscreen}\n\tfullscreenHandler: {client.FullscreenHandler}\n")
            .Append(CultureInfo.InvariantCulture, $"\tallowedOverFullscreen: {Bit(client.AllowedOverFullscreen)}\n\tgrouped: {grouped}\n\n");
    }

    private static void AppendWorkspace(StringBuilder text, WorkspaceEntryJson workspace) =>
        text.Append(CultureInfo.InvariantCulture, $"workspace {workspace.Name} on monitor {workspace.Monitor}:\n")
            .Append(CultureInfo.InvariantCulture, $"\tmonitorId: {workspace.MonitorId}\n\twindows: {workspace.Windows}\n\thasFullscreen: {Bit(workspace.HasFullscreen)}\n")
            .Append(CultureInfo.InvariantCulture, $"\tlastWindow: {workspace.LastWindow}\n\tlastWindowTitle: {Clean(workspace.LastWindowTitle)}\n")
            .Append(CultureInfo.InvariantCulture, $"\ttiledLayout: {workspace.TiledLayout}\n\n");

    private static void AppendMonitor(StringBuilder text, MonitorJson monitor)
    {
        var reserved = monitor.Reserved;
        text.Append(CultureInfo.InvariantCulture, $"Monitor {monitor.Name} (ID {monitor.Id}):\n")
            .Append(CultureInfo.InvariantCulture, $"\t{monitor.Width}x{monitor.Height}@{monitor.RefreshRate:F5} at {monitor.X}x{monitor.Y}\n")
            .Append(CultureInfo.InvariantCulture, $"\tdescription: {monitor.Description}\n\tmake: {monitor.Make}\n\tmodel: {monitor.Model}\n\tserial: {monitor.Serial}\n")
            .Append(CultureInfo.InvariantCulture, $"\tactive workspace: {monitor.ActiveWorkspace.Name}\n\tspecial workspace: {monitor.SpecialWorkspace.Name}\n")
            .Append(CultureInfo.InvariantCulture, $"\treserved: {reserved[0]} {reserved[1]} {reserved[2]} {reserved[3]}\n\tscale: {monitor.Scale}\n")
            .Append(CultureInfo.InvariantCulture, $"\tfocused: {YesNo(monitor.Focused)}\n\tdisabled: {Word(monitor.Disabled)}\n\tcurrentFormat: {monitor.CurrentFormat}\n")
            .Append(CultureInfo.InvariantCulture, $"\thardwareDetails:\n\t\thdr: {YesNo(monitor.HardwareDetails.Hdr)}\n")
            .Append(CultureInfo.InvariantCulture, $"\t\tchroma: {YesNo(monitor.HardwareDetails.Chroma)}\n\t\tbt2020: {YesNo(monitor.HardwareDetails.Bt2020)}\n\n");
    }

    private static string Bare(string address) =>
        address.StartsWith("0x", StringComparison.Ordinal) ? address[2..] : address;

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Word(bool value) => value ? "true" : "false";

    private static string YesNo(bool value) => value ? "yes" : "no";}
