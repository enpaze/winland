using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

public sealed class WindowHost : IDisposable
{
    private const uint QuitMessage = 0x0012;

    private readonly WindowManager _core;
    private readonly WindowAdapter _windows;
    private readonly KeyboardHook _keys;
    private readonly MouseHook _mouse;
    private readonly InputThread _input;
    private readonly TouchpadInput _touchpad;
    private readonly CommandServer _commands;
    private readonly SessionPhase _phase = new();

    private int _absences;
    private readonly Lock _memory = new();
    private readonly Queue<SessionSignal> _pending = new();
    private readonly LoopInvoker _loop = new();
    private static readonly object Changed = new();

    private readonly Coalescer<object> _saver;
    private readonly Action<RecoverySnapshot>? _onRecovery;

    private RecoverySnapshot? _recovered;
    private RecoverySnapshot? _clients;
    private Action? _whenReady;
    private SessionEffect _deferred = SessionEffect.None;
    private bool _started;
    private bool _opened;
    private AccentChanged? _accent;
    private bool _draining;
    private bool _ownsTaskbar;

    private volatile bool _looping;

    private volatile bool _taskbarReleased;
    private bool _markerSeen;
    private bool _disposed;
    private ITaskbarSession _taskbar = NullTaskbarSession.Instance;

    public WindowHost(WindowManager core, Action<RecoverySnapshot>? onRecovery = null, string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(core);
        _core = core;
        _onRecovery = onRecovery;
        _saver = new Coalescer<object>(_ => Save(), TimeSpan.FromMilliseconds(250), "Winland recovery");
        _windows = new WindowAdapter(core, () => _saver.Post(Changed));
        _keys = new KeyboardHook(core, _windows.Apply);
        _mouse = new MouseHook(core, _keys);
        _input = new InputThread(StartHooks, ServiceHooks, StopHooks);
        _touchpad = new TouchpadInput(_keys.PostInput);
        _core.SessionLock = _phase;
        _commands = new CommandServer(core, _windows.Apply, pipeName);
        _commands.AfterCommand = () => _loop.Post(() =>
        {
            if (_looping)
                FollowTaskbar();
        });
        _taskbar = TaskbarControl.Current;
    }

    internal ITaskbarSession TaskbarSession
    {
        get => _taskbar;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _taskbar = value;
        }
    }

    public Action? SessionEnding { get; set; }

    public Action<TrayCommand>? OnTray { get; set; }

    public Action<string>? OnOpenFile { get; set; }

    public Func<bool>? LogExists { get; set; }

    public void OnLoop(Action action) => _loop.Invoke(action, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4.5));

    public string Command(string line)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var reply = _core.Execute(line);
        _windows.Apply(reply.Effect);
        FollowTaskbar();
        return reply.Text;
    }

    public CommandReply Reload()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var reply = _core.Execute("reload");
        _windows.Apply(reply.Effect);
        FollowTaskbar();
        return reply;
    }

    public void PostReload() => _loop.Post(() =>
    {
        if (_looping)
            Reload();
    });

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
            return;

        _started = true;
        NoteAccent();
        FollowTaskbar();
        _windows.Start();
        _input.Start();
        _commands.Start();
    }

    private void StartHooks()
    {
        _keys.Start();
        _mouse.Start();
        _touchpad.Start();
    }

    private bool ServiceHooks(uint message)
    {
        if (message != KeyboardHook.WatchMessage)
            return _keys.Service(message);

        if (_keys.Lost())
        {
            _mouse.Reinstall();
            _keys.Reinstall();
        }
        else
        {
            _keys.Watch();
        }

        return true;
    }

    private void StopHooks()
    {
        _touchpad.Dispose();
        _mouse.Dispose();
        _keys.Dispose();
    }

    public void Run(RecoverySnapshot? recovered = null, Action? whenReady = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _recovered = recovered;
        _whenReady = whenReady;

        using var session = new SessionWindow(
            signal => _loop.Post(() => OnSignal(signal)),
            window => _loop.Post(() => OnFlash(window)),
            () => SessionEnding?.Invoke(),
            () => _loop.Post(NoteAccent),
            WindowAdapter.ForgetSettings);
        var shell = WaitForShell();
        var thread = PInvoke.GetCurrentThreadId();
        SessionHalt.Stop = () => _ = PInvoke.PostThreadMessage(thread, QuitMessage, default, default);
        _looping = true;
        _loop.Attach(thread);
        TrayHost.Start(
            command => _loop.Post(() =>
            {
                try
                {
                    OnTray?.Invoke(command);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    Console.Error.WriteLine(exception);
                }
            }),
            path => _loop.Post(() =>
            {
                try
                {
                    OnOpenFile?.Invoke(path);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    Console.Error.WriteLine(exception);
                }
            }),
            () => LogExists?.Invoke() ?? false);
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true;
            _ = PInvoke.PostThreadMessage(thread, QuitMessage, default, default);
        };

        Console.CancelKeyPress += onCancel;
        try
        {
            Start();
            FlushDeferred();
            if (shell == SessionSignal.ShellTimedOut)
                session.WatchForShell();

            foreach (var signal in SessionNotices.AtStart(PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_REMOTESESSION) != 0))
                OnSignal(signal);

            OnSignal(shell);

            WindowAdapter.Pump(message =>
            {
                if (message.message == LoopInvoker.Message)
                {
                    _loop.Drain();
                    return true;
                }

                if (message.message == KeyboardHook.InputLanguageMessage)
                    _keys.ForgetLayout();

                return false;
            });
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
            _looping = false;
            _loop.Detach();
            _loop.Drain();
            SessionHalt.Stop = null;
        }
    }

    public void RevealHidden()
    {
        _taskbarReleased = true;
        _windows.RevealHidden();
    }

    public void Salvage()
    {
        _windows.Salvage();
        TrayHost.Salvage();
        RestoreTaskbar();
    }

    private void OnFlash(WindowId window) => _windows.Apply(_core.Handle(new WindowFlashed(window)));

    public void ResetDecorations() => _windows.ResetDecorations();

    public RecoverySnapshot Stamp(RecoverySnapshot snapshot) => _windows.Stamp(snapshot);

    public void RestoreTaskbar()
    {
        _taskbarReleased = true;
        if (!_ownsTaskbar)
            return;

        _ownsTaskbar = false;
        _taskbar.Restore();
    }

    private void FollowTaskbar()
    {
        if (_taskbarReleased)
            return;

        var choice = TaskbarPolicy.Decide(_core.HideTaskbar, _ownsTaskbar, PendingMarker(), _opened);
        if (choice.Hide)
        {
            _ownsTaskbar = true;
            _taskbar.Hide();
        }
        else if (choice.Restore)
        {
            _ownsTaskbar = false;
            _taskbar.Restore();
        }

        if (choice.Relayout && _taskbar.ChangesWorkArea)
            ApplyMonitors();
    }

    private bool PendingMarker()
    {
        if (_markerSeen)
            return false;

        _markerSeen = true;
        return _taskbar.Marker;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _commands.Dispose();
            _input.Dispose();

            StopHooks();
            _windows.Dispose();
            _saver.Dispose();
        }
        finally
        {
            RestoreTaskbar();
        }
    }

    private static SessionSignal WaitForShell()
    {
        if (Taskbar.Present())
        {
            Console.Error.WriteLine("The shell is ready.");
            return SessionSignal.ShellReady;
        }

        Console.Error.WriteLine("Waiting for the shell.");
        var deadline = Environment.TickCount64 + (long)SessionPhase.ShellWait.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            PumpPending();
            if (Taskbar.Present())
            {
                Console.Error.WriteLine("The shell is ready.");
                return SessionSignal.ShellReady;
            }

            Thread.Sleep(50);
        }

        PumpPending();
        if (Taskbar.Present())
        {
            Console.Error.WriteLine("The shell is ready.");
            return SessionSignal.ShellReady;
        }

        var seconds = (int)SessionPhase.ShellWait.TotalSeconds;
        Console.Error.WriteLine($"The shell was not ready after {seconds} seconds.");
        return SessionSignal.ShellTimedOut;
    }

    internal void NoteAccent()
    {
        if (_disposed)
            return;

        AccentChanged next;
        try
        {
            if (SystemAccent.Read() is not BorderColor color)
                return;

            next = new AccentChanged(color) { Contrast = SystemAccent.Contrast(), TextScale = SystemAccent.TextScale() };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return;
        }

        if (next == _accent)
            return;

        _accent = next;
        _windows.Apply(_core.Handle(next));
    }

    private void OnSignal(SessionSignal signal)
    {
        _pending.Enqueue(signal);
        if (_draining)
            return;

        _draining = true;
        try
        {
            while (_pending.Count > 0)
            {
                var wasLive = _phase.Live;
                var effect = _phase.Notice(_pending.Dequeue());
                if (wasLive && !_phase.Live)
                    Interlocked.Increment(ref _absences);

                if (!_started)
                {
                    if (effect != SessionEffect.None)
                        _deferred = effect;

                    continue;
                }

                Perform(effect);
            }
        }
        finally
        {
            _draining = false;
        }
    }

    private void FlushDeferred()
    {
        var effect = _deferred;
        _deferred = SessionEffect.None;
        if (_started)
            Perform(effect);
    }

    private void Perform(SessionEffect effect)
    {
        switch (effect)
        {
            case SessionEffect.None:
                return;
            case SessionEffect.LayOut:
                Open();
                return;
            case SessionEffect.Reflow:
                Console.Error.WriteLine("Laying out again.");
                Reflow();
                return;
            case SessionEffect.Recover:
                Console.Error.WriteLine("Laying out again after the session changed.");
                RecoverSession();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect, "No handler for this effect.");
        }
    }

    private void Open()
    {
        if (_opened)
            return;

        _opened = true;
        Taskbar.Rehide();
        ApplyMonitors();
        _windows.Recover(_recovered);
        _windows.AdoptExisting();
        _whenReady?.Invoke();
        _windows.Publish();
        Taskbar.Conceal();
    }

    private void Reflow()
    {
        Taskbar.Rehide();
        ApplyMonitors();
    }

    private void RecoverSession()
    {
        _windows.DropGone();
        Taskbar.Rehide();
        var snapshot = Recall();
        if (snapshot is not null)
            SessionRecovery.Restore(_windows, snapshot);

        ApplyMonitors();
    }

    private void Save()
    {
        var absences = Volatile.Read(ref _absences);
        var live = _phase.Live;
        if (_windows.Capture() is not RecoverySnapshot snapshot)
            return;

        if (Remember(snapshot, live, absences) is { } captured)
            _onRecovery?.Invoke(captured);
    }

    private RecoverySnapshot? Remember(RecoverySnapshot snapshot, bool live, int absences)
    {
        var captured = WindowIdentity.Annotate(snapshot);
        lock (_memory)
        {
            var present = live && _phase.Live && Volatile.Read(ref _absences) == absences;
            if (_clients is null || present)
                _clients = captured;

            return present ? captured : null;
        }
    }

    private RecoverySnapshot? Recall()
    {
        lock (_memory)
            return _clients;
    }

    private void ApplyMonitors()
    {
        _windows.Apply(_core.Handle(new MonitorsChanged(MonitorAdapter.Read())));
        Taskbar.Conceal();
    }

    private static void PumpPending()
    {
        while (PInvoke.PeekMessage(out var message, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
        {
            if (message.message == QuitMessage)
            {
                PInvoke.PostQuitMessage(0);
                return;
            }

            PInvoke.TranslateMessage(in message);
            PInvoke.DispatchMessage(in message);
        }
    }
}
