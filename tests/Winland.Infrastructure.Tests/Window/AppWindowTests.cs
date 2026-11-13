using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure.Tests;

[Collection(DesktopCollection.Name)]
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

        Assert.True(Assert.Single(core.Recovery().Clients).Floating);
    }

    [DesktopFact]
    public void AFloatIsAdoptedAtItsVisibleSizeWithoutGrowingOnReadoption()
    {
        using var window = new Probe();
        var expected = ProbeNative.Visible((nint)window.Handle);
        var core = new WindowManager(ConfigParser.Parse("general:border_size = 0\nwindowrule = float, class:.*"));
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        adapter.AdoptExisting([(nint)window.Handle]);
        var first = Assert.Single(core.Recovery().Clients).Bounds!.Value;
        Assert.Equal(expected.Width, first.Width);
        Assert.Equal(expected.Height, first.Height);
        core.Handle(new WindowClosed(window.Id));
        adapter.AdoptExisting([(nint)window.Handle]);
        Assert.Equal(first, Assert.Single(core.Recovery().Clients).Bounds);
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

        var client = Assert.Single(core.Recovery().Clients, client => client.Window == dialog.Id);
        Assert.True(client.Floating);
        Assert.True(client.Hidden);
        Assert.Equal(new WorkspaceId("1"), client.Workspace);
    }

    [DesktopFact]
    public void ANoActivateWindowWithATaskbarButtonIsLeftUnmanaged()
    {
        using var window = new Probe(extended: WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_APPWINDOW | WINDOW_EX_STYLE.WS_EX_TOPMOST);
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, class:.*"));
        using var adapter = new WindowAdapter(core);

        adapter.AdoptExisting([(nint)window.Handle]);
        adapter.Apply(core.Handle(new Dispatch("workspace", "2")));

        Assert.False(core.Manages(window.Id));
        Assert.True(PInvoke.IsWindowVisible(window.Handle));
    }

    // Superwhisper's transparent indicator is topmost, but is not WS_EX_LAYERED until Winland animates it.
    [DesktopFact]
    public void ATransparentTopmostIndicatorIsLeftUnmanaged()
    {
        using var window = new Probe(resizable: false, extended:
            WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_APPWINDOW);
        var expected = ProbeNative.Visible((nint)window.Handle);
        var core = new WindowManager(ConfigParser.Parse("windowrule = tile, class:.*"));
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        adapter.AdoptExisting([(nint)window.Handle]);
        adapter.Notice(PInvoke.EVENT_OBJECT_SHOW, (nint)window.Handle);
        adapter.Apply(core.Handle(new Dispatch("workspace", "2")));

        Assert.False(core.Manages(window.Id));
        Assert.True(PInvoke.IsWindowVisible(window.Handle));
        Assert.Equal(expected, ProbeNative.Visible((nint)window.Handle));
        Assert.False(((WINDOW_EX_STYLE)PInvoke.GetWindowLong(window.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE))
            .HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED));

        // A newly created hidden indicator must not be held transparent for an opening animation either.
        PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_HIDE);
        adapter.Notice(PInvoke.EVENT_OBJECT_CREATE, (nint)window.Handle);
        Assert.False(((WINDOW_EX_STYLE)PInvoke.GetWindowLong(window.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE))
            .HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED));
        PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        adapter.Notice(PInvoke.EVENT_OBJECT_SHOW, (nint)window.Handle);
        Assert.False(core.Manages(window.Id));
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
            Assert.Single(snapshot.Clients);
        }

        var extended = PInvoke.GetWindowLong(window.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        Assert.NotEqual(0, PInvoke.SetWindowLong(window.Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            extended | (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT));
        var expected = ProbeNative.Visible((nint)window.Handle);
        var core = new WindowManager();
        using var restarted = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        restarted.Recover(snapshot);

        Assert.False(core.Manages(window.Id));
        Assert.True(PInvoke.IsWindowVisible(window.Handle));
        Assert.Equal(expected, ProbeNative.Visible((nint)window.Handle));

        PInvoke.ShowWindow(window.Handle, SHOW_WINDOW_CMD.SW_HIDE);
        restarted.Recover(snapshot with { Clients = [snapshot.Clients[0] with { Hidden = true }] });

        Assert.False(core.Manages(window.Id));
        Assert.True(PumpUntil(() => PInvoke.IsWindowVisible(window.Handle)));
    }

    // A tool window is adopted when it has a title and a rule asks for it. One without a title, such as a menu, never is.
    [DesktopFact]
    public void AShownToolWindowWithATitleIsAdoptedByARule() => ShowToolWindow("Winland tool window probe", adopted: true);

    [DesktopFact]
    public void AShownToolWindowWithoutATitleIsNotAdoptedByARule() => ShowToolWindow("", adopted: false);

    private static void ShowToolWindow(string title, bool adopted)
    {
        using var window = new Probe(extended: WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, title: title);
        var core = new WindowManager(ConfigParser.Parse("general:border_size = 0\nwindowrule = float, class:.*"));
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;

        adapter.Notice(PInvoke.EVENT_OBJECT_SHOW, (nint)window.Handle);

        Assert.Equal(adopted, core.Manages(window.Id));
    }

    // The test thread owns the probe windows and pumps no messages. Once it has not for 5 seconds, Windows calls its windows
    // hung, and Winland posts a show to a hung window instead of waiting for it. The wait pumps, as the window's program would.
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
            Assert.False(Handle.IsNull);
            PInvoke.ShowWindow(Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }

        public HWND Handle { get; }

        public WindowId Id => new((nint)Handle);

        public void Dispose() => PInvoke.DestroyWindow(Handle);
    }
}
