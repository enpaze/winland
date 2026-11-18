namespace Winland.Application;

public sealed partial class WindowManager
{
    private readonly WorkspaceSet _workspaces = new();
    private readonly List<LiveMonitor> _monitors = [];
    private readonly ProcessFamilies<IReadOnlyList<WindowRule>> _follow = new();
    private readonly ProcessFamilies<WorkspaceId> _initialWorkspaces = new();
    private readonly BindSession _keyState = new();

    private readonly Lock _gate = new();

    private volatile AppliedConfig _config = AppliedConfig.Unconfigured;

    private readonly Func<ClientId, int?> _clientBorder;
    private BorderColor? _accent;
    private HighContrast? _contrast;
    private double _textScale = 1;
    private Rectangle _area;
    private readonly FocusState _focus;
    private readonly ClientProps _props = new();
    private List<StreamEvent>? _configEvents;
    private readonly HashSet<WindowId> _concealedMinimized = [];

    private Outcome? _applied;

    public event Action<Outcome>? Noticed;

    public Action<Outcome>? Decided { get; set; }

    public Func<ConfigRead>? ReadConfig { get; set; }

    public Func<string?>? LogFile { get; set; }

    public bool HideTaskbar => _config.Session.HideTaskbar;

    public SessionPhase? SessionLock { get; set; }

    public Action<ConfigDocument>? AfterReload { get; set; }

    public WindowManager(ConfigDocument? config = null)
    {
        _clientBorder = ClientBorderOf;
        _focus = new FocusState(window => !HasNoFocus(window));
        _cover = new FullscreenCover(_pins.IsPinned);
        TrackWindowState();
        if (config is not null)
            Use(AppliedConfig.From(config, accent: null));
        _loaded = config;
        _workspaces.Shown.Show(MonitorId.Primary, WorkspaceId.Initial);
        WorkspaceFor(WorkspaceId.Initial);
        Remember(WorkspaceId.Initial, MonitorId.Primary);
    }

    public BindTable Binds => _config.Binds;

    public BindSession KeyState => _keyState;

    public BindDecision Decide(Input input, BindSession session)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(session);
        var decision = session.Decide(_config.Binds, input);

        return decision;
    }

    internal void WhileLocked(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
            action();
    }

    public Outcome Handle(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (_gate)
        {
            if (input is WindowClosed closed && _applied is { } last && WorkspaceContaining(closed.Window) is null)
            {
                _windowState.Forget(closed.Window);
                return Outcome.Empty(last.State);
            }

            if (input is PointerMoved moved && _interaction is null && _applied is { } applied)
            {
                if (Glides(moved, applied, out var aim))
                    return Outcome.Empty(applied.State);

                return Step(input, () => View([], focus: Follow(moved, aim)));
            }

            return Step(input, () => HandleInput(input));
        }
    }

    public Outcome Adopt(IReadOnlyList<WindowAppeared> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        lock (_gate)
        {
            return Step(null, () =>
            {
                var outcome = new Outcome([], Snapshot());
                foreach (var appeared in windows)
                {
                    var wasClient = Find(appeared.Window) is not null;
                    outcome = outcome.Then(AppearMaybeMaximized(appeared));
                    Note(appeared, wasClient);
                }

                return outcome;
            });
        }
    }

    public IReadOnlyList<Launch> StartupLaunches(ConfigDocument config)
    {
        lock (_gate)
            return LaunchParser.FromConfig(config);
    }

    public void NoteLaunched(int processId, Launch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (processId <= 0)
            return;

        lock (_gate)
        {
            if (launch.Rules.Count > 0)
                _follow.Track(processId, launch.Rules, Clock() + 60_000);

            if (launch.InitialWorkspace is WorkspaceId workspace)
                _initialWorkspaces.Track(processId, workspace, Clock() + 10_000);
        }
    }

    internal void NoteLineage(int processId, IReadOnlyList<int> ancestors)
    {
        lock (_gate)
        {
            _follow.NoteLineage(processId, ancestors);
            _initialWorkspaces.NoteLineage(processId, ancestors);
        }
    }

    public CommandReply Execute(string? line)
    {
        var parsed = CommandProtocol.Parse(line);
        CommandReply result;
        ConfigDocument? reloaded;
        lock (_gate)
        {
            if (parsed is CommandParsed { Request: var request } && Read(request) is { } text)
                return new CommandReply(text, Outcome.Empty(Snapshot()));

            var reply = default(CommandReply);
            var outcome = Step(null, () => (reply = ExecuteLine(parsed)).Effect);
            result = reply with { Effect = outcome };
            reloaded = _reloaded;
            _reloaded = null;
        }

        if (reloaded is not null)
        {
            try
            {
                AfterReload?.Invoke(reloaded);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine(exception);
            }
        }

        return result;
    }

    private ConfigDocument? _reloaded;

    private Outcome Step(Input? input, Func<Outcome> run)
    {
        var before = Capture();
        var wasClient = input is WindowAppeared appeared && Find(appeared.Window) is not null;
        var config = _config;
        try
        {
            var outcome = run();
            CheckShownWorkspaces();
            if (input is not null)
                Note(input, wasClient);

            outcome = WithEvents(Decorate(outcome, before.State), before, input);

            var loaded = !ReferenceEquals(config, _config);
            if (!loaded && outcome.IsEmptyAfter(_applied))
                return Outcome.Empty(outcome.State);

            _applied = loaded ? null : outcome.Lasting();
            Decided?.Invoke(outcome);
            Emit(outcome);
            return outcome;
        }
        finally
        {
            _configEvents = null;
            _extraEvents = null;
            _activated = null;
            _readdressed = null;
            _crossMonitorWarp = null;
            _movedItself = null;
            _restoring = null;
        }
    }

    private Outcome HandleInput(Input input) =>
        input switch
        {
            WindowAppeared appeared => AppearMaybeMaximized(appeared),
            ConfigLoaded loaded => Load(loaded),
            KeyPressed or KeyReleased => RunKey(input),
            PointerButtonPressed pressed => PressPointer(pressed),
            PointerButtonReleased released => ReleasePointer(released),
            PointerMoved moved => MovePointer(moved),
            WheelTurned wheel => RunKey(wheel),
            PointerGesture gesture => ApplyGesture(gesture),
            GroupBarScrolled scrolled => ScrollGroupBar(scrolled),
            GroupTabClosed closed => View([], close: closed.Window),
            WindowMaximizeRequested requested => MaximizeRequest(requested.Window),
            WindowFullscreenRequested requested => FullscreenRequest(requested),
            WindowDrifted drifted => PutBack(drifted),
            WindowSizeStarted sizing => BeginNativeSize(sizing),
            WindowSizeEnded sized => EndNativeSize(sized),
            WindowSizing sizing => ContinueNativeSize(sizing),
            WindowMoveStarted started => BeginNativeMove(started),
            WindowMoveEnded ended => EndNativeMove(ended),
            SwipeBegan => BeginSwipe(),
            SwipeUpdated updated => UpdateSwipe(updated),
            SwipeEnded => EndSwipe(),
            Dispatch dispatched => Execute(dispatched.Name, dispatched.Arguments),
            WindowFlashed flashed => Flash(flashed.Window),
            WindowFocused focused => Consider(focused),
            WindowClosed closed => Close(closed.Window, closed.Cloaked),
            WindowEnabledChanged changed => ChangeEnabled(changed),
            WindowLimitsChanged limits => ChangeLimits(limits),
            WindowMinimized minimized => Minimize(minimized.Window),
            WindowRestored restored => Unminimize(restored),
            WindowResized resized => Reapply(resized),
            WindowTitleChanged retitled => Retitle(retitled),
            WindowClassChanged reclassed => Reclass(reclassed),
            Configure configure => Apply(configure.Document),
            AccentChanged accent => NoteAccent(accent),
            RestoredAdopted => ArrangeRestored(),
            MonitorsChanged changed => SetMonitors(changed.Monitors),
        };

    private WorkspaceId LaunchWorkspace() => TopOn(FocusedId());

    internal Func<long> Clock { get; set; } = static () => Environment.TickCount64;

    private void Emit(Outcome outcome)
    {
        if (outcome.Events.Count == 0)
            return;

        var handlers = Noticed;
        if (handlers is null)
            return;

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<Outcome>)handler).Invoke(outcome);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine(exception);
            }
        }
    }
}
