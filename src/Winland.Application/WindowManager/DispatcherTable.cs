using System.Collections.Frozen;

namespace Winland.Application;

public sealed partial class WindowManager
{
    private sealed record Dispatcher(
        Func<WindowManager, string, string, Outcome> Run,
        bool AllowsComma = false,
        DispatcherTraits Traits = DispatcherTraits.None);

    private static readonly FrozenDictionary<string, Dispatcher> Table = BuildTable();

    public static IReadOnlyCollection<string> DispatcherNames => Table.Keys;

    private static FrozenDictionary<string, Dispatcher> BuildTable()
    {
        var table = new Dictionary<string, Dispatcher>(StringComparer.OrdinalIgnoreCase)
        {
            ["exec"] = new(static (m, _, a) => m.Exec(a), AllowsComma: true),
            ["exit"] = new(static (m, _, _) => m.ExitDaemon()),

            ["workspace"] = new(static (m, _, a) => m.WorkspaceDispatch(a), AllowsComma: true),
            ["togglespecialworkspace"] = new(static (m, _, a) => m.ToggleSpecial(a), AllowsComma: true),
            ["movetoworkspace"] = new(static (m, _, a) => m.MoveTo(a, silent: false, consumed: false)),
            ["movetoworkspacesilent"] = new(static (m, _, a) => m.MoveTo(a, silent: true, consumed: false)),
            ["focusmonitor"] = new(static (m, _, a) => m.FocusMonitorDispatch(a), AllowsComma: true, Traits: DispatcherTraits.WarpsToEmptyMonitor),
            ["movecurrentworkspacetomonitor"] = new(static (m, _, a) => m.MoveCurrentWorkspace(a), AllowsComma: true),

            ["movefocus"] = new(static (m, _, a) => m.MoveFocusDirection(a), Traits: DispatcherTraits.WarpsToFocused | DispatcherTraits.WarpsToEmptyMonitor),
            ["focuswindow"] = new(static (m, _, a) => m.FocusWindow(a), AllowsComma: true),

            ["killactive"] = new(static (m, n, a) => m.LayoutDispatch(n, a)),
            ["movewindow"] = new(static (m, _, a) => m.MoveWindowDirection(a)),
            ["swapwindow"] = new(static (m, _, a) => m.SwapWindow(a)),
            ["togglefloating"] = new(static (m, _, a) => m.FloatWindow(a)),
            ["pin"] = new(static (m, _, a) => m.PinWindow(a)),
            ["fullscreen"] = new(static (m, _, a) => m.FullscreenDispatcher(a)),
            ["resizeactive"] = new(static (m, n, a) => m.LayoutDispatch(n, a)),

            ["layoutmsg"] = new(static (m, n, a) => m.LayoutDispatch(n, a)),
            ["togglesplit"] = new(static (m, n, a) => m.LayoutDispatch(n, a)),
            ["layout"] = new(static (m, _, a) => m.SelectLayout(a)),
            ["cyclelayout"] = new(static (m, _, a) => m.CycleWorkspaceLayout(a)),

            ["togglegroup"] = new(static (m, _, _) => m.ToggleGroup()),
            ["changegroupactive"] = new(static (m, _, a) => m.ChangeGroupActive(a.Trim())),
            ["moveintogroup"] = new(static (m, _, a) => m.MoveIntoGroup(a.Trim())),
            ["moveoutofgroup"] = new(static (m, _, a) => m.MoveOutOfGroup(a.Trim())),

            ["resizewindow"] = new(static (m, _, _) => m.View([])),
        };

        return table.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
