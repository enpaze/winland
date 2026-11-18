namespace Winland.Application;

public sealed partial class WindowManager
{
    private const int DirectionLockThreshold = 10;
    private const int MinSpeedToForce = 30;

    private const int SwipeDistance = 300;

    private readonly SwipeRecognizer _swipeGesture = new();
    private WorkspaceSwipe? _workspaceSwipe;

    private Outcome BeginSwipe()
    {
        _swipeGesture.Begin();
        return View([]);
    }

    private Outcome UpdateSwipe(SwipeUpdated updated)
    {
        var step = _swipeGesture.Update(updated, _config.Gestures);
        if (step.Picked)
            StartWorkspaceSwipe();

        if (step.Along is not double along || _swipeGesture.Active is null || _workspaceSwipe is not { } swipe)
            return View([]);

        return StepWorkspaceSwipe(swipe, swipe.Delta - along, Math.Max(1, updated.Steps));
    }

    private Outcome EndSwipe() =>
        _swipeGesture.End() is not null && _workspaceSwipe is { } swipe ? FinishWorkspaceSwipe(swipe) : View([]);

    private void StartWorkspaceSwipe()
    {
        if (SessionLock?.Locked == true || _keyState.Inhibited || _workspaceSwipe is not null)
            return;

        var monitor = FocusedId();
        _workspaceSwipe = new WorkspaceSwipe(_workspaces.Shown.ActiveOn(monitor), monitor);
    }

    private Outcome StepWorkspaceSwipe(WorkspaceSwipe swipe, double delta, int steps)
    {
        const int distance = SwipeDistance;
        var step = swipe.Delta - delta;
        swipe.Delta = delta;
        swipe.AverageSpeed = ((swipe.AverageSpeed * swipe.SpeedPoints) + Math.Abs(step)) / (swipe.SpeedPoints + steps);
        swipe.SpeedPoints += steps;
        if (SwipeOvertaken(swipe))
        {
            _workspaceSwipe = null;
            return ReturnSwipe(swipe);
        }

        var (left, right) = Neighbours(swipe.Monitor);
        var begin = swipe.Begin;
        swipe.Delta = Math.Clamp(swipe.Delta, -distance, distance);
        var beginRank = SwipeRank(begin);
        var emptyBegin = _workspaces.Find(begin) is not { } beginDesk || beginDesk.Layout.Clients.Count == 0;
        if ((left == begin && swipe.Delta < 0)
            || (swipe.Delta > 0 && emptyBegin && SwipeRank(right) <= beginRank)
            || (swipe.Delta < 0 && beginRank <= SwipeRank(left)))
        {
            swipe.Delta = 0;
            return PreviewSwipe(swipe, null);
        }

        var sign = swipe.Delta < 0 ? -1 : 1;
        if (swipe.LockedSign != 0 && swipe.LockedSign != sign)
            swipe.Delta = 0;
        else if (swipe.LockedSign == 0 && Math.Abs(swipe.Delta) > DirectionLockThreshold)
            swipe.LockedSign = sign;

        var toward = swipe.Delta < 0 ? left : right;
        var away = swipe.Delta < 0 ? SwipeRank(left) > beginRank : SwipeRank(right) < beginRank;
        if (away || toward is not WorkspaceId target || _workspaces.Find(target) is null)
            return PreviewSwipe(swipe, null);

        return PreviewSwipe(swipe, target);
    }

    private bool SwipeOvertaken(WorkspaceSwipe swipe) =>
        _workspaces.Shown.ActiveOn(swipe.Monitor) != swipe.Begin || SessionLock?.Locked == true || _keyState.Inhibited;

    private Outcome FinishWorkspaceSwipe(WorkspaceSwipe swipe)
    {
        const int distance = SwipeDistance;
        var (left, right) = Neighbours(swipe.Monitor);
        var begin = swipe.Begin;

        if (SwipeRank(right) <= SwipeRank(begin))
            right = NumberAfter(swipe.Monitor);

        _workspaceSwipe = null;
        if (SwipeOvertaken(swipe))
            return ReturnSwipe(swipe);

        var magnitude = Math.Abs(swipe.Delta);
        var slow = swipe.AverageSpeed < MinSpeedToForce;
        if ((magnitude < distance * 0.5 && slow) || magnitude < 2)
            return ReturnSwipe(swipe);

        if ((swipe.Delta < 0 ? left : right) is not WorkspaceId target)
            return ReturnSwipe(swipe);

        if (FocusedId() != swipe.Monitor && MonitorById(swipe.Monitor) is not null)
            Focus(swipe.Monitor);

        LandSwipe();
        return SwitchTo(target, focus: null, consumed: false);
    }

    private (WorkspaceId? Left, WorkspaceId? Right) Neighbours(MonitorId monitor)
    {
        var context = Context(monitor);
        return (new WorkspaceAddress(WorkspaceStep.Prev).Resolve(context), new WorkspaceAddress(WorkspaceStep.Next).Resolve(context));
    }

    private WorkspaceId? NumberAfter(MonitorId monitor)
    {
        var active = Context(monitor).Active;
        return active.TryGetNumber(out var number) && number < WorkspaceId.MaxNumber
            ? WorkspaceId.FromNumber(number + 1L, new MonitorId(active.Monitor))
            : null;
    }

    private long SwipeRank(WorkspaceId? id)
    {
        if (id is not WorkspaceId workspace)
            return -1;

        if (workspace.TryGetNumber(out var number))
            return number;

        var listed = _workspaces.Listed(null);
        var named = 0;
        foreach (var each in listed)
        {
            if (!each.TryGetNumber(out _))
                named++;
        }

        var index = -1;
        for (var i = 0; i < listed.Count; i++)
        {
            if (listed[i] == workspace)
            {
                index = i;
                break;
            }
        }

        return index < 0 ? -1 : -2L - named + index;
    }

    private sealed class WorkspaceSwipe(WorkspaceId begin, MonitorId monitor)
    {
        public WorkspaceId Begin { get; } = begin;

        public MonitorId Monitor { get; } = monitor;

        public double Delta { get; set; }

        public double AverageSpeed { get; set; }

        public int SpeedPoints { get; set; }

        public int LockedSign { get; set; }
    }
}
