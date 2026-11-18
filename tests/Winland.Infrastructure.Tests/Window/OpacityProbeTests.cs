using System.Runtime.InteropServices;
using static Winland.Infrastructure.Tests.ProbeNative;

namespace Winland.Infrastructure.Tests;
[TestClass]
public class OpacityProbeTests
{
    private const uint Layered = 0x00080000;
    private const uint ToolWindow = 0x00000080;
    private const uint NoActivate = 0x08000000;
    private const uint Popup = 0x80000000;
    private const int ExStyleIndex = -20;

    [TestMethod]
    public void ProbeWindowTakesTheLayeredAlphaAndGivesTheStyleBack()
    {
        using var probe = Probe.Open();
        var original = ExStyle(probe.Handle);
        Assert.AreEqual(0u, original & Layered);

        var adapter = new WindowOpacity(new Win32Desktop());
        var window = new WindowId(probe.Handle);
        adapter.Apply(window, 128);

        Assert.AreEqual(Layered, ExStyle(probe.Handle) & Layered);
        Assert.AreEqual(128, LayeredAlpha(probe.Handle));
        Assert.IsTrue(adapter.Owns(window));

        adapter.Reset(window);

        var restored = ExStyle(probe.Handle);
        Assert.AreEqual(0u, restored & Layered);
        Assert.AreEqual(original, restored);
        Assert.IsFalse(adapter.Owns(window));
    }

    [TestMethod]
    public void ProbeWindowThatWasAlreadyLayeredIsLeftAlone()
    {
        using var probe = Probe.Open();
        var withLayer = ExStyle(probe.Handle) | Layered;
        _ = SetWindowLongPtr(probe.Handle, ExStyleIndex, (nint)withLayer);
        Assert.IsTrue(SetLayeredWindowAttributes(probe.Handle, 0, 200, 2));

        var adapter = new WindowOpacity(new Win32Desktop());
        adapter.Apply(new WindowId(probe.Handle), 100);

        Assert.AreEqual(200, LayeredAlpha(probe.Handle));
        Assert.IsFalse(adapter.Owns(new WindowId(probe.Handle)));
    }

    private static uint ExStyle(nint window) => (uint)GetWindowLongPtr(window, ExStyleIndex);

    private static byte LayeredAlpha(nint window)
    {
        Assert.IsTrue(GetLayeredWindowAttributes(window, out _, out var alpha, out var flags));
        Assert.AreEqual(2u, flags & 2u);
        return alpha;
    }

    private sealed class Probe : IDisposable
    {
        private const string ClassName = "WinlandOpacityProbe";

        private static readonly Lock Gate = new();
        private static readonly ProbeNative.WndProc Proc = WindowProc;
        private static bool _registered;

        private Probe(nint handle) => Handle = handle;

        public nint Handle { get; }

        public static Probe Open()
        {
            lock (Gate)
            {
                if (!_registered)
                {
                    ProbeNative.Register(ClassName, Proc);

                    _registered = true;
                }
            }

            var handle = CreateWindowEx(
                ToolWindow | NoActivate,
                ClassName,
                "WinlandOpacityProbe",
                Popup,
                -2000,
                -2000,
                120,
                80,
                IntPtr.Zero,
                IntPtr.Zero,
                ProbeNative.Module,
                IntPtr.Zero);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"The probe window was not created ({Marshal.GetLastWin32Error()}).");

            return new Probe(handle);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
                _ = DestroyWindow(Handle);
        }

        private static IntPtr WindowProc(IntPtr hwnd, uint message, nint wParam, nint lParam) =>
            DefWindowProc(hwnd, message, wParam, lParam);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetLayeredWindowAttributes(nint hwnd, out uint colorKey, out byte alpha, out uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint colorKey, byte alpha, uint flags);
}
