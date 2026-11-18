using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

public sealed partial class KeyboardHook : IDisposable
{

    internal const nuint InjectedTag = 0x574C4E44;

    internal const ushort MaskKey = 0xE8;

    private const int SweepDelayMs = 1500;
    private const int WatchPeriodMs = 2000;
    private const int UnseenInputMs = 1000;

    private const int CurrentSlackMs = 100;

    private static readonly (string Name, ushort Vk)[] Modifiers =
    [
        ("Super_L", 0x5B), ("Super_R", 0x5C),
        ("Control_L", 0xA2), ("Control_R", 0xA3),
        ("Alt_L", 0xA4), ("Alt_R", 0xA5),
        ("Shift_L", 0xA0), ("Shift_R", 0xA1),
    ];

    private static int _held;

    private readonly WindowManager _core;
    private readonly Action<Outcome> _apply;
    private readonly BindSession _session;
    private readonly HOOKPROC _callback;
    private readonly WINEVENTPROC _desktopCallback;
    private readonly InputQueue<Queued> _queued = new(Replaces, Join);

    private readonly object _timerGate = new();
    private HHOOK _hook;
    private HWINEVENTHOOK _desktopSwitch;
    private Thread? _worker;
    private Timer? _sweepTimer;
    private Timer? _watchTimer;
    private int _lastSeen;
    private long _startedAt;
    private uint _hookThread;
    private volatile KeyLayout _layout = KeyLayout.UnitedStates;
    private volatile nint _layoutHandle = unchecked((nint)(-1));
    private bool _listening;
    private volatile bool _disposed;

    internal const uint SweepMessage = 0x8000 + 25;
    internal const uint WatchMessage = 0x8000 + 26;
    internal const uint ReconcileMessage = 0x8000 + 27;
    internal const uint InputLanguageMessage = 0x0051;

    internal BindSession Session => _session;

    public KeyboardHook(WindowManager core, Action<Outcome> apply)
        : this(core, core.KeyState, apply)
    {
    }

    public KeyboardHook(WindowManager core, BindSession session, Action<Outcome> apply)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(apply);
        _core = core;
        _apply = apply;
        _session = session;
        _callback = OnKeyboard;
        _desktopCallback = (_, _, _, _, _, _, _) => Post(ReconcileMessage);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_hook.IsNull)
            return;

        _hookThread = PInvoke.GetCurrentThreadId();
        _worker = new Thread(Pump)
        {
            IsBackground = true,
            Name = "Winland binds",
            Priority = ThreadPriority.AboveNormal,
        };
        _worker.Start();

        _ = PInvoke.PeekMessage(out _, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        _hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _callback, default(HINSTANCE), 0);
        if (_hook.IsNull)
        {
            StopWorker();
            throw new InvalidOperationException($"The keyboard hook was not installed ({Marshal.GetLastPInvokeError()}).");
        }

        InputClock.Watch();
        _desktopSwitch = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_DESKTOPSWITCH,
            PInvoke.EVENT_SYSTEM_DESKTOPSWITCH,
            default(HMODULE),
            _desktopCallback,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT);
        if (_desktopSwitch.IsNull)
            throw new InvalidOperationException($"Desktop switches were not hooked ({Marshal.GetLastPInvokeError()}).");

        Saw();
        _startedAt = Environment.TickCount64;
        ArmSweep();
        _watchTimer = new Timer(_ => Post(WatchMessage), null, WatchPeriodMs, WatchPeriodMs);

        try
        {
            Microsoft.Win32.SystemEvents.SessionSwitch += OnSession;
            _listening = true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException or NotSupportedException)
        {
            Console.Error.WriteLine(exception.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_listening)
        {
            Microsoft.Win32.SystemEvents.SessionSwitch -= OnSession;
            _listening = false;
        }

        _watchTimer?.Dispose();
        _watchTimer = null;
        if (!_desktopSwitch.IsNull)
        {
            _ = PInvoke.UnhookWinEvent(_desktopSwitch);
            _desktopSwitch = default;
        }

        if (!_hook.IsNull)
        {
            _ = PInvoke.UnhookWindowsHookEx(_hook);
            _hook = default;

            if (Environment.TickCount64 - _startedAt >= SweepDelayMs)
                ReleaseStale();

            Volatile.Write(ref _held, 0);
        }

        DisarmSweep();
        StopWorker();
    }

    internal bool Service(uint message)
    {
        switch (message)
        {
            case SweepMessage:
                if (!_disposed)
                    ReleaseStale();
                return true;
            case ReconcileMessage:
                if (!_disposed)
                    Reconcile();
                return true;
            default:
                return false;
        }
    }

    internal void ForgetLayout()
    {
        _layoutsStale = true;
        _layoutHandle = unchecked((nint)(-1));
    }

    private volatile bool _layoutsStale;

    internal bool Lost()
    {
        if (_disposed || _hook.IsNull || _session.Inhibited || TouchpadFingers.Touched(Environment.TickCount64))
            return false;

        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!PInvoke.GetLastInputInfo(ref info))
            return false;

        return unchecked((int)info.dwTime - Volatile.Read(ref _lastSeen)) > UnseenInputMs;
    }

    internal void Reinstall()
    {
        if (_disposed)
            return;

        if (!LowLevelHook.Reinstall(ref _hook, WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _callback, "keyboard"))
            return;

        Console.Error.WriteLine("The keyboard hook was reinstalled.");
        Saw();
        Reconcile();
    }

    internal void Saw() => Volatile.Write(ref _lastSeen, Environment.TickCount);

    private unsafe LRESULT OnKeyboard(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code < 0 || _disposed)
            return Next(code, wParam, lParam);

        try
        {
            Saw();
            var info = *(KBDLLHOOKSTRUCT*)lParam.Value;
            if (info.dwExtraInfo == InjectedTag)
                return Next(code, wParam, lParam);

            var up = info.flags.HasFlag(KBDLLHOOKSTRUCT_FLAGS.LLKHF_UP);
            var vk = (int)info.vkCode;
            var extended = info.flags.HasFlag(KBDLLHOOKSTRUCT_FLAGS.LLKHF_EXTENDED);
            var scan = info.scanCode > int.MaxValue ? 0 : (int)info.scanCode;

            NoteHeld(info.vkCode, up);

            if (!up)
                InputClock.NoteKey(info.time, vk, info.flags.HasFlag(KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED));

            if (KeyNames.PassesThrough(vk))
                return Next(code, wParam, lParam);

            var name = KeyName(vk, extended, up, () => KeyNames.Name(vk, scan, extended, ActiveLayout()));
            var xkb = KeyNames.XkbCode(vk, scan, extended);
            if (!up && !info.flags.HasFlag(KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED) && !IsModifier(vk))
                SessionEffects.NoteKey();

            var edge = new KeyEdge(vk, scan, extended, up, name, xkb);
            if (!Deliver(edge).Passes)
                return new LRESULT(1);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ThreadPool.QueueUserWorkItem(static error => Console.Error.WriteLine(error), exception, preferLocal: false);
        }

        return Next(code, wParam, lParam);
    }

    internal readonly record struct KeyEdge(int VirtualKey, int Scan, bool Extended, bool Up, string Name, int Xkb);

    private Point CursorPoint() => PInvoke.GetCursorPos(out var point) ? new Point(point.X, point.Y) : _session.Pointer;

    private bool IsDown(string key)
    {
        if (key.StartsWith("mouse:", StringComparison.Ordinal))
        {
            return key switch
            {
                "mouse:left" or "mouse:right" => Down(0x01) || Down(0x02),
                "mouse:middle" => Down(0x04),
                "mouse:x1" => Down(0x05),
                "mouse:x2" => Down(0x06),
                _ => false,
            };
        }

        if (KeyNames.TryVirtualKey(key, out var vk, out _) && Down(vk))
            return true;

        return _layout.TryVirtualKey(key, out vk) && Down(vk);
    }

    private readonly string?[] _pressedNames = new string?[512];

    internal string KeyName(int vk, bool extended, bool up, Func<string> name)
    {
        var slot = (vk & 0xFF) | (extended ? 0x100 : 0);
        if (up)
        {
            var pressed = _pressedNames[slot];
            _pressedNames[slot] = null;
            return pressed ?? name();
        }

        return _pressedNames[slot] ??= name();
    }

    private static bool IsModifier(int vk) =>
        vk is 0x10 or 0x11 or 0x12 or 0x14 or 0x5B or 0x5C or 0x90 or >= 0xA0 and <= 0xA5;

    private readonly Dictionary<nint, KeyLayout> _layouts = [];

    private KeyLayout ActiveLayout()
    {
        nint handle;
        try
        {
            handle = KeyboardLayout.ForegroundHandle();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return _layout;
        }

        if (handle == _layoutHandle)
            return _layout;

        if (_layoutsStale)
        {
            _layoutsStale = false;
            _layouts.Clear();
        }

        if (_layouts.TryGetValue(handle, out var known))
        {
            _layout = known;
            _layoutHandle = handle;
            return _layout;
        }

        try
        {
            var layout = KeyboardLayout.Read(handle);
            _layouts[handle] = layout;
            _layout = layout;
            _layoutHandle = handle;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }

        return _layout;
    }

    private void ArmSweep()
    {
        lock (_timerGate)
        {
            _sweepTimer?.Dispose();
            _sweepTimer = new Timer(_ => Post(SweepMessage), null, SweepDelayMs, Timeout.Infinite);
        }
    }

    private void DisarmSweep()
    {
        lock (_timerGate)
        {
            _sweepTimer?.Dispose();
            _sweepTimer = null;
        }
    }
}
