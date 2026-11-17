using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winland.Infrastructure;

public sealed partial class KeyboardHook : IDisposable
{
    private void OnSession(object? sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason is not (Microsoft.Win32.SessionSwitchReason.SessionLock or Microsoft.Win32.SessionSwitchReason.SessionUnlock))
            return;

        var inhibited = e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock;
        _session.Inhibit(inhibited);

        if (!inhibited)
            Post(ReconcileMessage);
    }

    private void Reconcile()
    {
        _session.Forget(IsDown);
        for (var slot = 0; slot < _pressedNames.Length; slot++)
        {
            if (!Down(slot & 0xFF))
                _pressedNames[slot] = null;
        }

        if (Drag.Current is BindDrag.Active dragging && !IsDown(dragging.Button) && Drag.Abandon() is BindDrag.Active drag)
            PostGesture(drag.Dispatcher, drag.Arguments, CursorPoint(), PointerDragPhase.End, drag.Button);

        var held = Volatile.Read(ref _held);
        for (var i = 0; i < Modifiers.Length; i++)
        {
            if (!Down(Modifiers[i].Vk))
                held &= ~(1 << i);
        }

        Volatile.Write(ref _held, held);
        ArmSweep();
    }

    internal void Watch()
    {
        if (_disposed || _session.Inhibited || Environment.TickCount64 - _startedAt < SweepDelayMs || !Current())
            return;

        ReleaseStale();
    }

    private bool Current()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!PInvoke.GetLastInputInfo(ref info))
            return false;

        return unchecked((int)info.dwTime - Volatile.Read(ref _lastSeen)) <= CurrentSlackMs;
    }

    private static void ReleaseStale()
    {
        var kept = Volatile.Read(ref _held);
        for (var i = 0; i < Modifiers.Length; i++)
        {
            var (name, vk) = Modifiers[i];
            if ((kept & (1 << i)) != 0 || !Down(vk))
                continue;

            Console.Error.WriteLine($"Releasing {name}, which was left down.");

            if (vk is 0x5B or 0x5C or 0xA4 or 0xA5)
                InputInjector.Post(Key(MaskKey, up: false), Key(MaskKey, up: true), Key(vk, up: true));
            else
                InputInjector.Post(Key(vk, up: true));
        }
    }

    private static void NoteHeld(uint vk, bool up)
    {
        var bit = ModifierBit((int)vk);
        if (bit == 0)
            return;

        var held = Volatile.Read(ref _held);
        Volatile.Write(ref _held, up ? held & ~bit : held | bit);
    }

    private static int ModifierBit(int vk)
    {
        for (var i = 0; i < Modifiers.Length; i++)
        {
            if (Modifiers[i].Vk == vk)
                return 1 << i;
        }

        return 0;
    }

    internal static void Mask() => InputInjector.Post(Key(MaskKey, up: false), Key(MaskKey, up: true));

    internal static void Send(params ReadOnlySpan<INPUT> inputs)
    {
        if (PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>()) != inputs.Length)
            Console.Error.WriteLine($"Input was not sent ({Marshal.GetLastPInvokeError()}).");
    }

    internal static INPUT Key(ushort vk, bool up, ushort scan = 0, bool extended = false)
    {
        if (scan == 0)
            scan = (ushort)PInvoke.MapVirtualKey(vk, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);

        var flags = up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0;
        if (extended || vk is 0x5B or 0x5C or 0xA3 or 0xA5 or 0x90)
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY;

        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = (VIRTUAL_KEY)vk,
                    wScan = scan,
                    dwFlags = flags,
                    dwExtraInfo = InjectedTag,
                },
            },
        };
    }

    private static bool Down(int vk) => (PInvoke.GetAsyncKeyState(vk) & 0x8000) != 0;
}
