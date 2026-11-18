using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter : IDisposable
{
    private const int ChildStyle = 0x40000000;
    private const int CaptionStyle = 0x00C00000;
    private const int SizingStyle = 0x00040000;
    private const int ToolStyle = 0x00000080;
    private const int DialogStyle = 0x00000001;
    private const int ClickThroughStyle = 0x00000020;
    private const int NoActivateStyle = 0x08000000;

    private readonly Lock _showGate = new();
    private volatile bool _releasing;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<WindowId, byte> _concealed = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<WindowId, byte> _revealed = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<WindowId, byte> _disabled = new();

    private readonly WindowManager _core;
    private readonly IDesktop _desktop;
    private readonly Action? _changed;
    private readonly WINEVENTPROC _callback;

    internal static readonly (uint First, uint Last)[] ObjectEvents =
    [
        (PInvoke.EVENT_OBJECT_CREATE, PInvoke.EVENT_OBJECT_REORDER),
        (PInvoke.EVENT_OBJECT_STATECHANGE, PInvoke.EVENT_OBJECT_NAMECHANGE),
        (PInvoke.EVENT_OBJECT_CLOAKED, PInvoke.EVENT_OBJECT_UNCLOAKED),
    ];

    private const long ProbeMilliseconds = 2000;
    private const int ProbeLimit = 1024;

    private readonly Dictionary<WindowId, (Size? Min, Size? Max)> _limits = [];
    private readonly Dictionary<WindowId, (Size? Min, Size? Max, long At)> _probed = [];
    private readonly Lock _limitGate = new();
    private readonly DeclinedWindows _declined = new();
    private HWINEVENTHOOK[] _objects = [];
    private HWINEVENTHOOK _minimize;
    private HWINEVENTHOOK _foreground;
    private HWINEVENTHOOK _moveSize;
    private volatile RecoverySnapshot? _published;
    private volatile bool _disposed;

    private readonly Dictionary<WindowId, Decoration?> _painted = [];
    private readonly WindowOpacity _opacity;
    private readonly BorderlessFrame _borderless;
    private readonly DwmEffects _effects;
    private readonly Lock _paintGate = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<Outcome> _decided = new();
    private readonly Lock _applyGate = new();
    private readonly Dictionary<WindowId, CachedFrame> _frames = [];
    private readonly Lock _frameGate = new();
    private readonly Func<double> _clock;
    private readonly MotionPlayer _motions;

    private readonly QuickMove _quick = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<WindowId, byte> _placing = new();

    private const long SettleMilliseconds = 1500;

    private const int PaintFrames = 4;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<WindowId, long> _settling = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<WindowId, byte> _ownFullscreen = new();

    public WindowAdapter(WindowManager core, Action? changed = null)
        : this(core, new Win32Desktop(), changed, null)
    {
    }

    internal WindowAdapter(WindowManager core, IDesktop desktop, Action? changed, Func<double>? clock, bool threaded = true)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(desktop);
        _core = core;
        _desktop = desktop;
        _clock = clock ?? PresentClock.Now;
        _opacity = new WindowOpacity(desktop, desktop.Hung);
        _borderless = new BorderlessFrame(desktop, desktop.Hung);
        _effects = new DwmEffects(desktop, desktop.Hung);
        _motions = new MotionPlayer(new MotionTarget(this), _clock, threaded);
        _core.ReducedMotion = ReducedMotion;
        _core.Decided = _decided.Enqueue;
        _core.DescribeWindow = Inspect;
        GroupBarHost.Dispatch = (name, arguments) =>
        {
            if (!_disposed)
                Apply(_core.Handle(new Dispatch(name, arguments)));
        };
        GroupBarHost.Close = window =>
        {
            if (!_disposed)
                Apply(_core.Handle(new GroupTabClosed(window)));
        };
        GroupBarHost.Scroll = (shown, forward) =>
        {
            if (!_disposed)
                Apply(_core.Handle(new GroupBarScrolled(shown, forward)));
        };
        _callback = OnEvent;
        _changed = changed;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_objects.Length > 0)
            return;

        var flags = PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS;
        _objects = [.. ObjectEvents.Select(range => PInvoke.SetWinEventHook(
            range.First,
            range.Last,
            default(HMODULE),
            _callback,
            0,
            0,
            flags))];
        _minimize = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_MINIMIZESTART,
            PInvoke.EVENT_SYSTEM_MINIMIZEEND,
            default(HMODULE),
            _callback,
            0,
            0,
            flags);

        _foreground = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            default(HMODULE),
            _callback,
            0,
            0,
            flags);

        _moveSize = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_MOVESIZESTART,
            PInvoke.EVENT_SYSTEM_MOVESIZEEND,
            default(HMODULE),
            _callback,
            0,
            0,
            flags);

        if (_objects.Any(hook => hook.IsNull) || _minimize.IsNull || _foreground.IsNull || _moveSize.IsNull)
        {
            Release();
            throw new InvalidOperationException("Window events were not hooked.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Release();
        LandMotions();
        ClearDecorations();
    }

    private void OnEvent(
        HWINEVENTHOOK hook,
        uint @event,
        HWND hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
    {
        if (@event == PInvoke.EVENT_OBJECT_REORDER)
        {
            if (hwnd == PInvoke.GetDesktopWindow())
            {
                BorderHost.Restack();
                GroupBarHost.Restack();
                KeepFloatingAbove();
            }

            return;
        }

        if (idObject != 0 || idChild != 0 || hwnd.IsNull)
            return;

        if (@event != PInvoke.EVENT_OBJECT_DESTROY
            && ((WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE)).HasFlag(WINDOW_STYLE.WS_CHILD))
            return;

        Notice(@event, hwnd, dwmsEventTime);
    }

    internal void Notice(uint @event, nint handle, uint time = 0) => Notice(@event, new HWND(handle), time);

    private void Notice(uint @event, HWND hwnd, uint dwmsEventTime)
    {
        if (IsShellTaskbar(hwnd))
        {
            Taskbar.Conceal();
            return;
        }

        try
        {
            var window = new WindowId(((IntPtr)hwnd).ToInt64());

            if (@event is PInvoke.EVENT_OBJECT_LOCATIONCHANGE or PInvoke.EVENT_OBJECT_SHOW or PInvoke.EVENT_OBJECT_HIDE
                or PInvoke.EVENT_OBJECT_CLOAKED or PInvoke.EVENT_OBJECT_UNCLOAKED or PInvoke.EVENT_SYSTEM_MINIMIZESTART
                or PInvoke.EVENT_SYSTEM_MINIMIZEEND or PInvoke.EVENT_SYSTEM_FOREGROUND)
            {
                BorderHost.Follow(window);
            }

            if (@event is PInvoke.EVENT_SYSTEM_FOREGROUND or PInvoke.EVENT_OBJECT_SHOW)
                GroupBarHost.Follow(window);

            switch (@event)
            {
                case PInvoke.EVENT_SYSTEM_FOREGROUND:
                    var cause = Foreground.Classify(window, dwmsEventTime);
                    Apply(_core.Handle(new WindowFocused(window, cause) { Clicked = cause == FocusCause.User ? InputClock.Clicked : null }));
                    return;
                case PInvoke.EVENT_OBJECT_DESTROY:
                    _concealed.TryRemove(window, out _);
                    _revealed.TryRemove(window, out _);
                    _disabled.TryRemove(window, out _);
                    _settling.TryRemove(window, out _);
                    _ownFullscreen.TryRemove(window, out _);
                    _quick.Forget(window);
                    AbandonDrag(window);
                    ForgetLimits(window);
                    ForgetProbe(window);
                    _declined.Forget(window);
                    Apply(_core.Handle(new WindowClosed(window)));
                    _opacity.Forget(window);
                    BorderHost.Forget(window);
                    _motions.Forget(window);
                    return;
                case PInvoke.EVENT_OBJECT_LOCATIONCHANGE:
                    if (_declined.Fullscreen(window) && TryDescribe(hwnd, window, out var windowed) && !windowed.Traits.ExclusiveFullscreen)
                    {
                        AdoptShown(hwnd, window);
                        return;
                    }

                    if (Decide(hwnd, window))
                        return;

                    if (_sizing == window)
                    {
                        FollowSizing(window);
                        _effects.Refit(window);
                        return;
                    }

                    if (PInvoke.IsZoomed(hwnd) && _core.Manages(window))
                    {
                        RequestMaximize((nint)window.Value);
                        return;
                    }

                    if (!_placing.ContainsKey(window))
                    {
                        var ours = TryOuter(window, out var now) && _quick.Ours(window, now);
                        if (ours)
                            CatchUp(window, now);

                        if (!ours && _moving != window)
                            RereadLimits(hwnd, window);

                        if (!ours)
                        {
                            ReportResize(hwnd, window);
                            ReportFullscreen(hwnd, window);
                            PutBackIfDrifted(hwnd, window);
                        }
                    }

                    _effects.Refit(window);
                    return;
                case PInvoke.EVENT_SYSTEM_MOVESIZESTART:
                    BeginMove(hwnd, window);
                    return;
                case PInvoke.EVENT_OBJECT_STATECHANGE:
                    if (_core.Manages(window))
                        NoteEnabled(hwnd, window);

                    return;
                case PInvoke.EVENT_SYSTEM_MOVESIZEEND:
                    EndMove(window);
                    return;
                case PInvoke.EVENT_SYSTEM_MINIMIZESTART:
                    UndoMinimize((nint)window.Value);
                    return;
                case PInvoke.EVENT_SYSTEM_MINIMIZEEND:
                    Apply(_core.Handle(new WindowRestored(window)));
                    AdoptShown(hwnd, window);
                    return;
                case PInvoke.EVENT_OBJECT_HIDE:
                case PInvoke.EVENT_OBJECT_CLOAKED:
                case PInvoke.EVENT_OBJECT_SHOW:
                case PInvoke.EVENT_OBJECT_UNCLOAKED:
                case PInvoke.EVENT_OBJECT_NAMECHANGE:
                case PInvoke.EVENT_OBJECT_CREATE:
                    if (@event == PInvoke.EVENT_OBJECT_SHOW && _revealed.TryRemove(window, out _))
                        return;

                    var facts = new WindowFacts(
                        Minimized: PInvoke.IsIconic(hwnd),
                        Visible: PInvoke.IsWindowVisible(hwnd),
                        Managed: _core.Manages(window),
                        Concealed: _concealed.ContainsKey(window),
                        Declined: @event == PInvoke.EVENT_OBJECT_NAMECHANGE
                            && _declined.Unchanged(window, _core.RulesMatchTitles, _core.RuleRevision, () => RuleClass(hwnd)));
                    switch (WindowVisibility.Decide(@event, facts))
                    {
                        case VisibilityAction.UndoMinimize:
                            UndoMinimize((nint)window.Value);
                            break;
                        case VisibilityAction.Close:
                            AbandonDrag(window);
                            ForgetLimits(window);
                            Apply(_core.Handle(new WindowClosed(window) { Cloaked = @event == PInvoke.EVENT_OBJECT_CLOAKED }));
                            break;
                        case VisibilityAction.Retitle:
                            Apply(_core.Handle(new WindowTitleChanged(window, WindowTitle(hwnd))));
                            break;
                        case VisibilityAction.Restore:
                            Apply(_core.Handle(new WindowRestored(window)));
                            AdoptShown(hwnd, window);
                            break;
                        case VisibilityAction.Adopt when PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) is var root && root != hwnd:
                            var frame = new WindowId(((IntPtr)root).ToInt64());
                            if (@event == PInvoke.EVENT_OBJECT_SHOW && PackagedIdentity.IsHostedApp(hwnd, root) && !_core.Manages(frame))
                                AdoptHidden(root, frame);
                            break;
                        case VisibilityAction.Adopt when !facts.Visible:
                            if (@event == PInvoke.EVENT_OBJECT_CREATE && Holds(hwnd))
                                _opacity.Apply(window, 0);
                            break;
                        case VisibilityAction.Adopt:
                            if (@event is PInvoke.EVENT_OBJECT_SHOW or PInvoke.EVENT_OBJECT_UNCLOAKED or PInvoke.EVENT_OBJECT_CREATE)
                                AdoptHidden(hwnd, window);
                            else
                                AdoptShown(hwnd, window);
                            break;
                    }

                    return;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(exception);
        }
    }

    private void AdoptHidden(HWND hwnd, WindowId window)
    {
        var held = _opacity.Wanted(window) == 0 || Holds(hwnd);
        if (held)
            _opacity.Apply(window, 0);

        try
        {
            AdoptShown(hwnd, window);
        }
        finally
        {
            if (held)
            {
                lock (_motions.For(window))
                {
                    if (!_motions.Running(window) && _opacity.Wanted(window) == 0)
                        _opacity.Apply(window, 255);
                }
            }
        }
    }

    private static bool Holds(HWND hwnd)
    {
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var extended = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        return ((style & CaptionStyle) == CaptionStyle || (style & SizingStyle) != 0)
            && (style & ChildStyle) == 0
            && (extended & ToolStyle) == 0
            && !IsOverlay(extended)
            && PInvoke.GetWindowTextLength(hwnd) > 0;
    }

    private static bool CanBeClient(HWND hwnd)
    {
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var extended = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        return (style & ChildStyle) == 0
            && !IsOverlay(extended)
            && ((extended & ToolStyle) == 0 || PInvoke.GetWindowTextLength(hwnd) > 0)
            && PInvoke.IsWindowVisible(hwnd)
            && PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd;
    }

    private void NoteEnabled(HWND hwnd, WindowId window)
    {
        var disabled = !PInvoke.IsWindowEnabled(hwnd);
        if (disabled ? !_disabled.TryAdd(window, 0) : !_disabled.TryRemove(window, out _))
            return;

        Apply(_core.Handle(new WindowEnabledChanged(window, !disabled)));
    }

    private bool Note(WindowId window, bool disabled)
    {
        if (disabled)
            _disabled[window] = 0;
        else
            _disabled.TryRemove(window, out _);

        return disabled;
    }

    private bool TryDescribe(HWND hwnd, WindowId window, out WindowAppeared appeared, bool inspecting = false)
    {
        appeared = null!;
        var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, ref info))
            return false;

        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var extended = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var className = ClassName(hwnd);
        var title = WindowTitle(hwnd);
        var owner = PInvoke.GetWindowThreadProcessId(hwnd, out var pid) == 0 ? 0 : pid;
        var identity = PackagedIdentity.Resolve(hwnd, className, ProcessFacts.Shared.For(owner).Exe);
        var topLevel = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd && (style & ChildStyle) == 0;
        Point? pointer = PInvoke.GetCursorPos(out var cursor) ? new Point(cursor.X, cursor.Y) : null;
        var outer = PInvoke.GetWindowRect(hwnd, out var rect) ? WindowRect.From(rect) : default;
        var bounds = TryVisible(window, out var visible) ? visible : outer;
        var monitorRect = WindowRect.From(info.rcMonitor);
        long processId = identity.ProcessId;
        var facts = ProcessFacts.Shared.For(identity.ProcessId);
        var (min, max) = inspecting ? default : LimitsOf(hwnd, window);

        var ownerWindow = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER);
        appeared = new WindowAppeared(window, WindowRect.From(info.rcWork))
        {
            Foreground = PInvoke.GetForegroundWindow() == hwnd,
            Pointer = pointer,
            Owner = ownerWindow.IsNull ? null : new WindowId(((IntPtr)ownerWindow).ToInt64()),
            Disabled = inspecting ? !PInvoke.IsWindowEnabled(hwnd) : Note(window, !PInvoke.IsWindowEnabled(hwnd)),
            Bounds = bounds,
            ProcessId = processId,
            AncestorProcessIds = facts.Ancestors,
            Executable = facts.ImagePath,
            Traits = new WindowTraits(
                Visible: PInvoke.IsWindowVisible(hwnd) && !identity.UnresolvedFrame,
                Titled: title.Length > 0,
                TopLevel: topLevel,
                Tool: (extended & ToolStyle) != 0,
                Tooltip: className == "tooltips_class32",
                Taskbar: className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" || ShellFlyouts.Is(identity.Exe, className),
                Cloaked: IsCloaked(hwnd),
                Dialog: Dialogish(className, extended, ownerWindow),
                Elevated: false,
                Class: identity.Class,
                Title: title,
                Exe: identity.Exe,
                Aumid: identity.Aumid,
                MinSize: min,
                MaxSize: max,
                ExclusiveFullscreen: ExclusiveFullscreenWindow.Is((uint)style, outer, monitorRect),
                Overlay: IsOverlay(extended),
                Resizable: (style & SizingStyle) != 0,
                Topmost: (extended & (int)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0,
                Frameless: (style & CaptionStyle) != CaptionStyle && (style & SizingStyle) == 0),
        };
        return true;
    }

    private bool Dialogish(string className, int extended, HWND owner) =>
        className == "#32770"
        || (extended & DialogStyle) != 0
        || (extended & (int)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0
        || (!owner.IsNull && (PInvoke.IsWindowVisible(owner) || _concealed.ContainsKey(new WindowId((nint)owner))));

    private static string RuleClass(HWND hwnd)
    {
        var owner = PInvoke.GetWindowThreadProcessId(hwnd, out var pid) == 0 ? 0 : pid;
        return PackagedIdentity.Resolve(hwnd, ClassName(hwnd), ProcessFacts.Shared.For(owner).Exe).Class;
    }

    private void NoteManaged(WindowId window, Outcome outcome, WindowAppeared appeared)
    {
        var adopted = false;
        foreach (var client in outcome.State.Clients)
        {
            if (client.Window != window)
                continue;

            adopted = true;
            break;
        }

        if (!adopted)
        {
            ForgetLimits(window);
            _declined.Note(window, appeared.Traits, _core.RuleRevision);
            return;
        }

        _declined.Forget(window);

        var min = appeared.Traits.MinSize;
        var max = appeared.Traits.MaxSize;
        var changed = false;
        lock (_limitGate)
        {
            if (_limits.TryGetValue(window, out var prior))
                changed = prior.Min != min || prior.Max != max;

            _limits[window] = (min, max);
        }

        if (changed)
            Apply(_core.Handle(new WindowLimitsChanged(window, min, max)));
    }

    private void Release()
    {
        foreach (var hook in _objects)
        {
            if (!hook.IsNull)
                PInvoke.UnhookWinEvent(hook);
        }

        if (!_minimize.IsNull)
            PInvoke.UnhookWinEvent(_minimize);
        if (!_foreground.IsNull)
            PInvoke.UnhookWinEvent(_foreground);

        if (!_moveSize.IsNull)
            PInvoke.UnhookWinEvent(_moveSize);

        _objects = [];
        _moveSize = default;
        _minimize = default;
        _foreground = default;
    }
}
