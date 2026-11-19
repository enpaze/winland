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

        return dispatcher.Run(this, arguments);
    }

    private Outcome Rejected(string error) =>
        View([]) with { Error = error };

    private Outcome WorkspaceDispatch(string arguments)
    {
        if (!WorkspaceAddress.TryParse(arguments, out var address))
            return Rejected("error: no such workspace");

        if (address.Resolve(Context()) is not WorkspaceId target)
            return View([]);

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
            ? launched with { Launches = [.. launched.Launches, launch with { InitialWorkspace = parsed.Workspace is long number ? NumberedWorkspace(number) : LaunchWorkspace() }] }
            : launched;
    }

    private Outcome CloseActive()
    {
        var active = ActiveWindow();
        var desk = TopWorkspace();
        return Present(desk, desk.Covered(), [], close: active);
    }

    private Outcome ResizeActive(string arguments)
    {
        var active = ActiveWindow();
        var desk = TopWorkspace();
        var covered = desk.Covered();
        IReadOnlyList<Placement> placements = active is WindowId window ? desk.Layout.ResizeActive(window, arguments) : [];
        return Present(desk, covered, placements);
    }

    private Outcome LayoutMessage(string message) =>
        LayoutChange(layout => layout.LayoutMessage(ActiveWindow(), message));

    private Outcome Split() =>
        TopWorkspace().Layout is DwindleLayout dwindle
            ? LayoutChange(_ => dwindle.ToggleSplit(ActiveWindow()))
            : Rejected("error: split works in the dwindle layout");

    private Outcome LayoutChange(Func<Layout, IReadOnlyList<Placement>> change)
    {
        var desk = TopWorkspace();
        var layout = desk.Layout;
        var covered = desk.Covered();
        var placements = change(layout);
        if (layout.LayoutMessageError is string layoutError)
            return View(placements) with { Error = layoutError };

        var focus = layout.LayoutMessageFocus;
        if (focus is WindowId next && HasNoFocus(next))
            focus = null;
        else if (focus is WindowId accepted)
            Give(accepted, desk);

        return Present(desk, covered, placements, focus: focus, focusRevealed: focus is null);
    }

}
