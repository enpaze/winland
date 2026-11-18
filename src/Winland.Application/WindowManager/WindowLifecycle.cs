namespace Winland.Application;

public sealed partial class WindowManager
{
    private static IReadOnlyList<WindowRule> JoinRules(IReadOnlyList<WindowRule> rules, IReadOnlyList<WindowRule> followed)
    {
        if (followed.Count == 0)
            return rules;
        if (rules.Count == 0)
            return followed;

        var combined = new WindowRule[rules.Count + followed.Count];
        for (var i = 0; i < rules.Count; i++)
            combined[i] = rules[i];
        for (var i = 0; i < followed.Count; i++)
            combined[rules.Count + i] = followed[i];
        return combined;
    }

    private WindowId? ActiveWindow()
    {
        if (_focus.Focused is not WindowId active)
            return null;

        if (ClientOn(ActiveId(), active) is not null)
            return active;

        var desk = WorkspaceContaining(active);
        if (desk is null || !_workspaces.Shown.IsShown(desk.Id))
            return null;

        return desk.ClientOf(active) is { Minimized: false } ? active : null;
    }

    private void Note(Input input, bool wasClient)
    {
        switch (input)
        {
            case WindowAppeared appeared when !wasClient && WorkspaceContaining(appeared.Window) is { } appearedWorkspace
                && _workspaces.Shown.IsShown(appearedWorkspace.Id)
                && (!appeared.Traits.Topmost || appeared.Foreground)
                && TakesMapFocus(appeared)
                && _restoring != appeared.Window
                && !_awaitingMapFocus.Contains(appeared.Window):
                var current = _workspaces.Shown.SpecialOn(appearedWorkspace.Monitor) ?? _workspaces.Shown.ActiveOn(appearedWorkspace.Monitor);
                if (appearedWorkspace.Id == current)
                {
                    if (_monitors.Count == 0 || appearedWorkspace.Monitor == FocusedId())
                        Give(appeared.Window, appearedWorkspace);
                    else
                        FocusState.Remember(appearedWorkspace, appeared.Window);
                }

                break;
        }
    }

    private bool TakesMapFocus(WindowAppeared appeared) =>
        _config.Input.FollowMouse || appeared.Foreground || _focus.Focused is null;

    private Outcome Appear(WindowAppeared appeared)
    {
        var processId = appeared.ProcessId is > 0 and <= int.MaxValue ? (int)appeared.ProcessId : 0;
        if (processId > 0)
            NoteLineage(processId, appeared.AncestorProcessIds);

        if (Find(appeared.Window) is { } existing)
        {
            var home = WorkspaceContaining(appeared.Window)!;
            var kept = !Concealed(home, existing) && !home.Covered().Contains(appeared.Window);
            return kept ? View([]) : View([], hide: [appeared.Window]);
        }

        var followed = _follow.TryTake(processId, Clock(), out var rules) ? rules : [];
        var traits = appeared.Traits.WithInitialNames();
        var decision = WindowRuleParser.Decide(traits, JoinRules(_config.WindowRules, followed));
        if (!decision.Adopt)
            return View([]);

        var restored = FindRestored(appeared.Window);
        decision = Recovered(decision, restored);
        if (restored?.Floating is not null)
            decision = decision with { FloatReason = FloatReason.Restored };
        var screen = MonitorFor(appeared);
        if (_monitors.Count > 0 && screen is null && !RestoredMonitorExists(restored))
            return View([]);

        if (restored is null)
            screen = RuleMonitor(decision.Placement.Monitor, screen);

        var monitor = RestoredMonitor(restored, screen);
        var active = _workspaces.Shown.ActiveOn(monitor);
        var current = _workspaces.Shown.SpecialOn(monitor) ?? active;
        var rule = WorkspaceAddress.ParseRule(decision.Workspace, Context());

        if (restored is null && processId > 0)
        {
            if (_initialWorkspaces.TryTake(processId, Clock(), out var origin) && rule.Id is null && origin != current)
                rule = new WorkspaceAddress.Rule(origin, Silent: true);
        }
        var owned = appeared.Owner is WindowId ownerWindow ? WorkspaceContaining(ownerWindow) : null;
        if (restored is null && rule.Id is null && owned is not null)
            rule = new WorkspaceAddress.Rule(owned.Id, Silent: true);

        var target = restored?.Workspace ?? rule.Id ?? current;
        ForgetRestored(appeared.Window);
        var ruledHere = restored is null && rule.Id is WorkspaceId ruled && !ruled.IsSpecial;
        var desk = WorkspaceFor(target, ruledHere ? null : target.IsSpecial ? FocusedId() : monitor);
        target = desk.Id;
        if (restored is { Hidden: false } && !desk.Id.IsSpecial)
            _restoredShown.Add(desk.Id);
        var area = WorkArea(desk, appeared.WorkArea);
        _area = area;
        var bounds = restored is null ? PlaceOnMap(appeared, decision, desk, area) : restored.Bounds ?? appeared.Bounds;
        var covered = desk.Covered();
        if (decision.ScrollingWidth is double scrollingWidth)
            desk.Layout.NoteScrollingWidth(appeared.Window, scrollingWidth);
        var placements = desk.Layout.Add(
            appeared.Window,
            area,
            appeared.Pointer,
            decision.Floating,
            bounds,
            target,
            appeared.Traits.Class,
            appeared.Traits.Title,
            MonitorById(desk.Monitor)?.Bounds ?? default,
            appeared.ProcessId,
            minSize: decision.MinSize,
            maxSize: decision.MaxSize,
            tiledLimits: RememberSizes(appeared.Window, decision.Sizes, appeared.Traits.MinSize, appeared.Traits.MaxSize));
        if (restored is not null && appeared.Opened)
        {
            _restoring = appeared.Window;
            var arranged = desk.Layout.Arrange(_restoredTiles);
            if (arranged.Count > 0)
                placements = arranged;
            if (_restored.Count == 0)
                _restoredTiles.Clear();
        }
        if (decision.FullscreenRequest is { } fullscreen)
            placements = desk.Layout.SetFullscreen(appeared.Window, fullscreen.Mode);
        else
            placements = OpenUnderFullscreen(desk, appeared.Window, placements);

        var focused = restored is null ? ActiveWindow() : null;
        if (!appeared.Dragging && GroupOnOpen(desk, appeared.Window, decision, appeared.Traits.Dialog, focused, TakesMapFocus(appeared)))
            placements = desk.Layout.Visible();

        placements = OpenDragged(appeared, desk, placements);

        NotePlaced(appeared.Window, desk, decision, restored is not null || traits.Topmost || appeared.Dragging);
        _props.Opened(appeared.Window, traits, followed, decision);
        RememberFocus(appeared.Window, decision);
        NoteOwner(appeared);
        Stack(appeared.Window);
        _pins.PinAtOpen(appeared.Window, decision.Floating, decision.Pin, appeared.Owner);
        var restore = _awaitingMapFocus.Contains(appeared.Window) ? Kept() : null;
        if (restore == appeared.Window)
            restore = null;

        if (restored is null && rule.Id is not null && !rule.Silent && !_workspaces.Shown.IsShown(target) && !_awaitingMapFocus.Contains(appeared.Window))
        {
            if (MonitorById(desk.Monitor) is { } owner)
                Focus(owner.Id);

            if (target.IsSpecial)
                return OpenSpecial(target, appeared.Window);

            return SwitchTo(target, appeared.Window, consumed: false);
        }

        if (!_workspaces.Shown.IsShown(target))
        {
            if (owned is not null)
                MarkUrgent(appeared.Window);

            if (StaysVisible(desk, appeared.Window, minimized: false))
                return Offering(View(placements, show: [appeared.Window]), restore);

            return Offering(View([], hide: [appeared.Window]), restore);
        }

        return Offering(Present(desk, covered, placements), restore);
    }

    private Outcome ChangeLimits(WindowLimitsChanged changed)
    {
        var desk = WorkspaceContaining(changed.Window);
        if (desk is null)
            return View([]);

        var rule = _props.Sizes(changed.Window)?.Rule ?? default;
        var tiled = RememberSizes(changed.Window, rule, changed.MinSize, changed.MaxSize);
        var floating = rule.Floating(changed.MinSize, changed.MaxSize);
        var placements = desk.Layout.UpdateLimits(changed.Window, floating.Min, floating.Max, tiled);
        return _workspaces.Shown.IsShown(desk.Id) ? View(placements) : View([]);
    }

    private TiledLimits RememberSizes(WindowId window, SizeRule rule, Size? ownMin, Size? ownMax)
    {
        _props.SetSizes(window, rule, ownMin, ownMax);
        return new TiledLimits(ownMin, ownMax);
    }

    private Outcome Close(WindowId window, bool cloaked = false)
    {
        var remembered = cloaked ? RememberCloaked(window) : null;
        var tile = _restoredTiles.GetValueOrDefault(window);
        _windowState.Forget(window);
        if (remembered is not null)
        {
            _restored[window] = remembered;
            if (remembered.Floating == false)
                _restoredTiles[window] = tile;
        }
        var desk = WorkspaceContaining(window);
        if (desk is null)
            return View([]);

        var covered = desk.Covered();
        var wasActive = _focus.Focused == window;
        var mode = desk.ClientOf(window)?.Fullscreen ?? FullscreenMode.None;

        if (CloseGroupMember(window, desk, covered, mode) is Outcome grouped)
            return grouped;

        var monitor = desk.Monitor;
        var special = desk.Id.IsSpecial && _workspaces.Shown.SpecialOn(monitor) == desk.Id;
        var floating = desk.ClientOf(window)?.Floating == true;
        var hadBounds = desk.Layout.TryBounds(window, out var bounds);
        var placements = Restamp(desk, desk.Layout.Remove(window));
        if (!_workspaces.Shown.IsShown(desk.Id))
            placements = [];

        if (special && desk.Layout.Clients.Count == 0)
        {
            _workspaces.Shown.ShowSpecial(monitor, null);
            DropIfEmpty(desk.Id);
            var normal = _workspaces.Find(_workspaces.Shown.ActiveOn(monitor));
            var specialFocus = normal is null ? null : _focus.Remembered(normal);
            if (wasActive)
                Give(specialFocus, normal);
            return View(normal?.Layout.Visible() ?? [], focus: wasActive ? specialFocus : null);
        }

        DropIfEmpty(desk.Id);
        if (!_workspaces.Shown.IsShown(desk.Id))
        {
            if (wasActive)
                Give(null, null);

            return View(placements);
        }

        WindowId? next = null;
        var reveal = false;
        if (wasActive)
        {
            next = FocusAfter(desk, floating, hadBounds ? bounds : default, hadBounds);

            if (_workspaces.Shown.SpecialOn(monitor) is WorkspaceId open && open != desk.Id && _workspaces.Find(open) is LiveWorkspace shownSpecial
                && (next is not WindowId candidate || WorkspaceContaining(candidate)?.Id != open)
                && _focus.Remembered(shownSpecial) is WindowId onSpecial)
            {
                Give(onSpecial, shownSpecial);
                return Present(desk, covered, placements, focus: onSpecial);
            }

            if (next is WindowId chosen)
                Give(chosen, desk);
            else
                reveal = true;
        }

        var outcome = Present(desk, covered, placements, focus: next, focusRevealed: reveal);
        if (wasActive && reveal && _focus.Focused == window)
        {
            var fallback = LastAccepted(desk);
            Give(fallback, desk);
            outcome = outcome with { Focus = fallback };
        }

        return outcome;
    }

    private Outcome Minimize(WindowId window)
    {
        var desk = WorkspaceContaining(window);
        if (desk is null)
            return View([]);

        var covered = desk.Covered();
        var wasActive = _focus.Focused == window;
        var placements = desk.Layout.Minimize(window);
        return PresentIfShown(desk, covered, placements, focusRevealed: wasActive);
    }

    private Outcome Unminimize(WindowRestored restored)
    {
        var desk = WorkspaceContaining(restored.Window);
        if (desk is null)
            return View([]);

        var covered = desk.Covered();
        var placements = desk.Layout.Restore(restored.Window);
        if (!_workspaces.Shown.IsShown(desk.Id))
            return View([], hide: [restored.Window]);

        return Present(desk, covered, placements);
    }

    private Outcome Reclass(WindowClassChanged reclassed)
    {
        if (WorkspaceContaining(reclassed.Window) is not { } desk || desk.ClientOf(reclassed.Window) is not { } client || client.Class == reclassed.Class)
            return View([]);

        desk.Reclass(reclassed.Window, reclassed.Class);

        _renamed.Add(reclassed.Window);
        return View([]);
    }

    private Outcome Retitle(WindowTitleChanged retitled)
    {
        if (WorkspaceContaining(retitled.Window) is not { } desk || desk.ClientOf(retitled.Window) is not { } client || client.Title == retitled.Title)
            return View([]);

        desk.Retitle(retitled.Window, retitled.Title);

        if (_config.TitleRules || _props.Subject(retitled.Window)?.Followed.Any(rule => rule.TitlePattern is not null) == true)
            _renamed.Add(retitled.Window);
        return View([]);
    }
}
