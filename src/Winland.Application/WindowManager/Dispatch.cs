namespace Winland.Application;

public sealed partial class WindowManager
{
    private Outcome RunBind(BindDecision match)
    {
        var dispatches = DispatchesOf(match);
        Outcome? outcome = null;
        foreach (var dispatch in dispatches)
        {
            if (dispatch.Dispatcher.Length == 0)
                continue;

            var next = Execute(dispatch.Dispatcher, dispatch.Arguments);
            outcome = outcome is null ? next : outcome.Then(next);
        }

        if (outcome is null)
            return View([], consumed: match.Consumed);

        return outcome with { Consumed = match.Consumed };
    }

    private static IReadOnlyList<BindDispatch> DispatchesOf(BindDecision match)
    {
        if (match.Dispatches.Count > 0)
            return match.Dispatches;
        if (string.IsNullOrEmpty(match.Dispatcher))
            return [];

        return [new BindDispatch(match.Dispatcher, match.Arguments)];
    }

    private Outcome Execute(string name, string arguments)
    {
        ArgumentNullException.ThrowIfNull(name);
        arguments ??= "";

        if (!Table.TryGetValue(name, out var dispatcher))
            return Rejected(DidYouMean.After("error: unknown dispatcher '" + name + "'", name, Table.Keys));

        if (!dispatcher.AllowsComma && arguments.Contains(',', StringComparison.Ordinal))
            return Rejected("error: " + name + " does not take a comma");

        return dispatcher.Run(this, name, arguments) with { Traits = dispatcher.Traits };
    }

    private Outcome Rejected(string error) =>
        View([]) with { Error = error };

    private Outcome WorkspaceDispatch(string arguments)
    {
        if (!WorkspaceAddress.TryResolve(arguments, Context(), out var target))
            return View([]) with { Error = WorkspaceAddress.IsNamed(arguments) ? "error: no such workspace" : null };

        if (target.IsSpecial)
            return OpenSpecial(target, null);

        var from = FocusedId();
        var switched = SwitchTo(target, focus: null, consumed: false);
        NoteCrossMonitorWarp(from, switched);
        return switched;
    }

    private Outcome Exec(string arguments)
    {
        var parsed = LaunchParser.Parse(arguments);
        if (parsed.Warnings.Count > 0)
        {
            var warnings = new List<StreamEvent>(parsed.Warnings.Count);
            foreach (var warning in parsed.Warnings)
                warnings.Add(new ConfigWarned(0, warning, null));
            (_configEvents ??= []).AddRange(warnings);
        }

        var launched = View([]);
        return parsed.Launch is Launch launch
            ? launched with { Launches = [.. launched.Launches, launch with { InitialWorkspace = LaunchWorkspace() }] }
            : launched;
    }

    private Outcome KillActive()
    {
        var active = ActiveWindow();
        var desk = CurrentWorkspace();
        return Present(desk, desk.Covered(), [], close: active);
    }

    private Outcome ResizeActive(string arguments)
    {
        var active = ActiveWindow();
        var desk = CurrentWorkspace();
        var covered = desk.Covered();
        IReadOnlyList<Placement> placements = active is WindowId window ? desk.Layout.ResizeActive(window, arguments) : [];
        return Present(desk, covered, placements);
    }

    private Outcome LayoutMessage(string message)
    {
        var active = ActiveWindow();
        var desk = CurrentWorkspace();
        var layout = desk.Layout;
        var covered = desk.Covered();
        var placements = layout.LayoutMessage(active, message);
        if (layout.LayoutMessageError is string layoutError)
            return View(placements) with { Error = layoutError };

        var focus = layout.LayoutMessageFocus;
        if (focus is WindowId next && HasNoFocus(next))
            focus = null;
        else if (focus is WindowId accepted)
            Give(accepted, desk);

        return Present(desk, covered, placements, focus: focus, focusRevealed: focus is null);
    }

    private static string Join(string name, string arguments) =>
        arguments.Length == 0 ? name : string.Concat(name, " ", arguments);

}
