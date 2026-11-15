using System.Collections.Frozen;

namespace Winland.Application;

/// <summary>
/// Every dispatcher Winland runs, in one table: what it runs, whether its arguments may hold a comma, and its
/// <see cref="DispatcherTraits"/>. The table is also the list of names the config parser accepts.
/// To add a dispatcher, add its entry here.
/// </summary>
public sealed partial class WindowManager
{
    /// <summary>
    /// What a dispatcher runs, given the Window Manager, the name it was called by and its arguments.
    /// <paramref name="AllowsComma"/> when the argument is free text or a name, which may hold a comma: a command
    /// line, a window selector, or the name of a Workspace or Monitor. Any other dispatcher refuses a comma.
    /// </summary>
    private sealed record Dispatcher(
        Func<WindowManager, string, string, Outcome> Run,
        bool AllowsComma = false,
        DispatcherTraits Traits = DispatcherTraits.None);

    private static readonly FrozenDictionary<string, Dispatcher> Table = BuildTable();

    /// <summary>The names the table runs, which the config parser accepts for a bind.</summary>
    public static IReadOnlyCollection<string> DispatcherNames => Table.Keys;

    private static FrozenDictionary<string, Dispatcher> BuildTable()
    {
        var table = new Dictionary<string, Dispatcher>(StringComparer.OrdinalIgnoreCase)
        {
            // Programs and the daemon
            ["exec"] = new(static (m, _, a) => m.Exec(a), AllowsComma: true),
            ["exit"] = new(static (m, _, _) => m.ExitDaemon()),

            // Workspaces and Monitors
            ["workspace"] = new(static (m, _, a) => m.WorkspaceDispatch(a), AllowsComma: true),
            ["togglespecialworkspace"] = new(static (m, _, a) => m.ToggleSpecial(a), AllowsComma: true),
            ["movetoworkspace"] = new(static (m, _, a) => m.MoveTo(a, silent: false, consumed: false)),
            ["movetoworkspacesilent"] = new(static (m, _, a) => m.MoveTo(a, silent: true, consumed: false)),
            ["focusmonitor"] = new(static (m, _, a) => m.FocusMonitorDispatch(a), AllowsComma: true, Traits: DispatcherTraits.WarpsToEmptyMonitor),
            ["movecurrentworkspacetomonitor"] = new(static (m, _, a) => m.MoveCurrentWorkspace(a), AllowsComma: true),

            // Focus
            ["movefocus"] = new(static (m, _, a) => m.MoveFocusDirection(a), Traits: DispatcherTraits.WarpsToFocused | DispatcherTraits.WarpsToEmptyMonitor),
            ["focuswindow"] = new(static (m, _, a) => m.FocusWindow(a), AllowsComma: true),

            // Windows
            ["killactive"] = new(static (m, n, a) => m.LayoutDispatch(n, a)),
            ["movewindow"] = new(static (m, _, a) => m.MoveWindowDirection(a)),
            ["swapwindow"] = new(static (m, _, a) => m.SwapWindow(a)),
            ["togglefloating"] = new(static (m, _, a) => m.FloatWindow(a)),
            ["pin"] = new(static (m, _, a) => m.PinWindow(a)),
            ["fullscreen"] = new(static (m, _, a) => m.FullscreenDispatcher(a)),
            ["resizeactive"] = new(static (m, n, a) => m.LayoutDispatch(n, a)),
            ["setprop"] = new(static (m, _, a) => m.SetPropDispatch(a), AllowsComma: true),

            // Layouts
            ["layoutmsg"] = new(static (m, n, a) => m.LayoutDispatch(n, a)),
            ["togglesplit"] = new(static (m, n, a) => m.LayoutDispatch(n, a)),
            ["layout"] = new(static (m, _, a) => m.SelectLayout(a)),
            ["cyclelayout"] = new(static (m, _, a) => m.CycleWorkspaceLayout(a)),

            // Groups
            ["togglegroup"] = new(static (m, _, _) => m.ToggleGroup()),
            ["changegroupactive"] = new(static (m, _, a) => m.ChangeGroupActive(a.Trim())),
            ["moveintogroup"] = new(static (m, _, a) => m.MoveIntoGroup(a.Trim())),
            ["moveoutofgroup"] = new(static (m, _, a) => m.MoveOutOfGroup(a.Trim())),

            // Accepted with no effect. A mouse bind's resizewindow runs in the drag code, not here.
            ["resizewindow"] = new(static (m, _, _) => m.View([])),
        };

        return table.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
