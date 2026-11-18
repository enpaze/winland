using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure.Tests;
[TestClass]
public class AppWindowTests
{
    [DesktopFact]
    public void AWindowWithoutASizingFrameIsAdoptedFloating()
    {
        using var window = new Probe(resizable: false);
        var core = new WindowManager(ConfigParser.Parse("general:border_size = 0"));
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        adapter.AdoptExisting([(nint)window.Handle]);

        Assert.IsTrue(Assert.ContainsSingle(core.Recovery().Clients).Floating);
    }

    [DesktopFact]
    public void AFloatIsAdoptedAtItsVisibleSizeWithoutGrowingOnReadoption()
    {
        using var window = new Probe();
        var expected = ProbeNative.Visible((nint)window.Handle);
        var core = new WindowManager(ConfigParser.Parse("general:border_size = 0\nwindowrule = float, class:*"));
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        adapter.AdoptExisting([(nint)window.Handle]);
        var first = Assert.ContainsSingle(core.Recovery().Clients).Bounds!.Value;
        Assert.AreEqual(expected.Width, first.Width);
        Assert.AreEqual(expected.Height, first.Height);
        core.Handle(new WindowClosed(window.Id));
        adapter.AdoptExisting([(nint)window.Handle]);
        Assert.AreEqual(first, Assert.ContainsSingle(core.Recovery().Clients).Bounds);
    }

    [DesktopFact]
    public void ADialogWhoseOwnerWinlandHidStillFloatsOnItsOwnersWorkspace()
    {
        using var owner = new Probe();
        var core = new WindowManager(ConfigParser.Parse("general:border_size = 0"));
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;
        adapter.AdoptExisting([(nint)owner.Handle]);
        adapter.Apply(core.Handle(new Dispatch("workspace", "2")));
        using var dialog = new Probe(owner: owner.Handle);

        adapter.AdoptExisting([(nint)dialog.Handle]);

        var client = Assert.ContainsSingle(client => client.Window == dialog.Id, core.Recovery().Clients);
        Assert.IsTrue(client.Floating);
        Assert.IsTrue(client.Hidden);
        Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
    }

    [DesktopFact]
    public void ANoActivateWindowWithATaskbarButtonIsLeftUnmanaged()
    {
        using var window = new Probe(extended: WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_APPWINDOW | WINDOW_EX_STYLE.WS_EX_TOPMOST);
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, class:*"));
        using var adapter = new WindowAdapter(core);

        adapter.AdoptExisting([(nint)window.Handle]);
        adapter.Apply(core.Handle(new Dispatch("workspace", "2")));

        Assert.IsFalse(core.Manages(window.Id));
        Assert.IsTrue(PInvoke.IsWindowVisible(window.Handle));
    }

    [DesktopFact]
    public void ATransparentTopmostIndicatorIsLeftUnmanaged()
    {
        using var window = new Probe(resizable: false, extended:
            WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_APPWINDOW);
        var expected = ProbeNative.Visible((nint)window.Handle);
        var core = new WindowManager(ConfigParser.Parse("windowrule = tile, class:*"));
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        adapter.AdoptExisting([(nint)window.Handle]);
        adapter.Notice(PInvoke.EVENT_OBJECT_SHOW, (nint)window.Handle);
        adapter.Apply(core.Handle(new Dispatch("workspace", "2")));

        Assert.IsFalse(core.Manages(window.Id));
        Assert.IsTrue(PInvoke.IsWindowVisible(window.Handle));
        Assert.AreEqual(expected, ProbeNative.Visible((nint)window.Handle));
        Assert.IsFalse(((WINDOW_EX_STYLE)PInvoke.GetWindowLong(window.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE))
            .HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED));

        PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_HIDE);
        adapter.Notice(PInvoke.EVENT_OBJECT_CREATE, (nint)window.Handle);
        Assert.IsFalse(((WINDOW_EX_STYLE)PInvoke.GetWindowLong(window.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE))
            .HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED));
        PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        adapter.Notice(PInvoke.EVENT_OBJECT_SHOW, (nint)window.Handle);
        Assert.IsFalse(core.Manages(window.Id));
    }

    [DesktopFact]
    public void ARestartDoesNotRestoreAnOverlayAsAClient()
    {
        using var window = new Probe(extended: WINDOW_EX_STYLE.WS_EX_TOPMOST);
        var previous = new WindowManager();
        RecoverySnapshot snapshot;
        using (var adapter = new WindowAdapter(previous))
        {
            previous.ReducedMotion = () => true;
            adapter.AdoptExisting([(nint)window.Handle]);
            snapshot = WindowIdentity.Annotate(previous.Recovery());
            Assert.ContainsSingle(snapshot.Clients);
        }

        var extended = PInvoke.GetWindowLong(window.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        Assert.AreNotEqual(0, PInvoke.SetWindowLong(window.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            extended | (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT));
        var expected = ProbeNative.Visible((nint)window.Handle);
        var core = new WindowManager();
        using var restarted = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        restarted.Recover(snapshot);

        Assert.IsFalse(core.Manages(window.Id));
        Assert.IsTrue(PInvoke.IsWindowVisible(window.Handle));
        Assert.AreEqual(expected, ProbeNative.Visible((nint)window.Handle));

        PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_HIDE);
        restarted.Recover(snapshot with { Clients = [snapshot.Clients[0] with { Hidden = true }] });

        Assert.IsFalse(core.Manages(window.Id));
        Assert.IsTrue(PumpUntil(() => PInvoke.IsWindowVisible(window.Handle)));
    }

    [DesktopFact]
    public void AShownToolWindowWithATitleIsAdoptedByARule() => ShowToolWindow("Winland tool window probe", adopted: true);

    [DesktopFact]
    public void AShownToolWindowWithoutATitleIsNotAdoptedByARule() => ShowToolWindow("", adopted: false);

    private static void ShowToolWindow(string title, bool adopted)
    {
        using var window = new Probe(extended: WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, title: title);
        var core = new WindowManager(ConfigParser.Parse("general:border_size = 0\nwindowrule = float, class:*"));
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        adapter.Notice(PInvoke.EVENT_OBJECT_SHOW, (nint)window.Handle);

        Assert.AreEqual(adopted, core.Manages(window.Id));
    }

    private static bool PumpUntil(Func<bool> condition) =>
        SpinWait.SpinUntil(() =>
        {
            while (ProbeNative.PeekMessage(out var message, IntPtr.Zero, 0, 0, 1))
            {
                ProbeNative.TranslateMessage(ref message);
                ProbeNative.DispatchMessage(ref message);
            }

            return condition();
        }, TimeSpan.FromSeconds(3));

    private sealed class Probe : IDisposable
    {
        public unsafe Probe(bool resizable = true, HWND owner = default, WINDOW_EX_STYLE extended = 0, string title = "Winland app compatibility probe")
        {
            var style = WINDOW_STYLE.WS_OVERLAPPEDWINDOW;
            if (!resizable)
                style &= ~WINDOW_STYLE.WS_THICKFRAME;
            Handle = PInvoke.CreateWindowEx(extended, "STATIC", title, style,
                100, 100, 450, 300, owner, null, null, null);
            Assert.IsFalse(Handle.IsNull);
            PInvoke.ShowWindow(Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }

        public HWND Handle { get; }

        public WindowId Id => new((nint)Handle);

        public void Dispose() => PInvoke.DestroyWindow(Handle);
    }
}
