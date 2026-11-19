using System.Collections.Frozen;

namespace Winland.Application;

public sealed partial class WindowManager
{
    private sealed record Dispatcher(
        Func<WindowManager, string, Outcome> Run,
        bool AllowsComma = false);

    private static readonly FrozenDictionary<string, Dispatcher> Table = BuildTable();

    public static IReadOnlyCollection<string> DispatcherNames => Table.Keys;

    internal static bool IsDispatcher(string name) => Table.ContainsKey(name);

    private static FrozenDictionary<string, Dispatcher> BuildTable()
    {
        var table = new Dictionary<string, Dispatcher>(StringComparer.OrdinalIgnoreCase)
        {
            ["exec"] = new(static (m, a) => m.Exec(a), AllowsComma: true),
            ["exit"] = new(static (m, _) => m.ExitDaemon()),
            ["reload"] = new(static (m, a) => a.Length == 0 ? m.Reload() : m.Rejected("error: reload takes no arguments")),

            ["workspace"] = new(static (m, a) => m.WorkspaceDispatch(a), AllowsComma: true),
            ["scratchpad"] = new(static (m, a) => m.ToggleSpecial(a), AllowsComma: true),
            ["focus-monitor"] = new(static (m, a) => m.FocusMonitorDispatch(a), AllowsComma: true),
            ["move-workspace"] = new(static (m, a) => m.MoveCurrentWorkspace(a), AllowsComma: true),

            ["focus"] = new(static (m, a) => m.FocusDispatch(a), AllowsComma: true),
            ["close"] = new(static (m, a) => m.NoArgument("close", a) ?? m.CloseActive()),
            ["move"] = new(static (m, a) => m.MoveDispatch(a)),
            ["swap"] = new(static (m, a) => m.SwapWindow(a)),
            ["resize"] = new(static (m, a) => a.Trim().Length == 0 ? m.Drag("resize") : m.ResizeActive(a)),
            ["float"] = new(static (m, a) => m.FloatWindow(a)),
            ["pin"] = new(static (m, a) => m.PinWindow(a)),
            ["fullscreen"] = new(static (m, a) => m.FullscreenDispatcher(FullscreenMode.Fullscreen, a)),
            ["maximize"] = new(static (m, a) => m.FullscreenDispatcher(FullscreenMode.Maximized, a)),

            ["layout"] = new(static (m, a) => m.SelectLayout(a)),
            ["split"] = new(static (m, a) => m.NoArgument("split", a) ?? m.Split()),
            ["layoutmsg"] = new(static (m, a) => m.LayoutMessage(a)),

            ["group"] = new(static (m, a) => m.NoArgument("group", a) ?? m.ToggleGroup()),
            ["tab"] = new(static (m, a) => m.ShowTab(a)),
            ["join"] = new(static (m, a) => m.JoinGroup(a)),
            ["leave-group"] = new(static (m, a) => m.NoArgument("leave-group", a) ?? m.LeaveGroup()),
        };

        return table.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private Outcome? NoArgument(string name, string arguments) =>
        arguments.Trim().Length == 0 ? null : Rejected("error: " + name + " takes no argument");

    private Outcome Drag(string name) =>
        Rejected("error: " + name + " is a drag; bind it to a mouse button");
}
