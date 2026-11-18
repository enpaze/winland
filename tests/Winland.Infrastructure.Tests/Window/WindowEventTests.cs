using Events = Windows.Win32.PInvoke;

namespace Winland.Infrastructure.Tests;

[TestClass]
public class WindowEventTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void AClientThatMovesItselfAfterOpeningIsPutBackOnItsTile()
    {
        using var windows = new TestWindows(reducedMotion: true);
        var window = windows.Open(0x7021);
        var tile = windows.Desktop[window.Value].Visible;
        windows.Advance(5000);
        windows.Desktop[window.Value].Visible = new Rectangle(50, 60, 1000, 700);

        windows.Adapter.Notice(Events.EVENT_OBJECT_LOCATIONCHANGE, (nint)window.Value);

        Assert.AreEqual(tile, windows.Desktop[window.Value].Visible);
    }

    [TestMethod]
    public void AWindowThatMovesItselfWhileAnotherIsPlacedIsPutBack()
    {
        using var windows = new TestWindows(reducedMotion: true);
        var floating = windows.Open(0x7021);
        var tiled = windows.Open(0x7022);
        windows.Dispatch("focus", "address:0x7021");
        windows.Dispatch("float");
        var tile = windows.Desktop[tiled.Value].Visible;
        windows.Advance(5000);
        windows.Desktop[tiled.Value].Visible = new Rectangle(50, 60, 1000, 700);
        windows.Desktop.OnSetBounds = window =>
        {
            if (window != floating)
                return;

            windows.Desktop.OnSetBounds = null;
            windows.Adapter.Notice(Events.EVENT_OBJECT_LOCATIONCHANGE, (nint)tiled.Value);
        };

        windows.Dispatch("resize", "10 10");

        Assert.IsNull(windows.Desktop.OnSetBounds);
        Assert.AreEqual(tile, windows.Desktop[tiled.Value].Visible);
    }

    [TestMethod]
    public void TheObjectEventHooksHoldTheEventsTheAdapterReadsAndNoOthers()
    {
        uint[] read =
        [
            Events.EVENT_OBJECT_CREATE, Events.EVENT_OBJECT_DESTROY, Events.EVENT_OBJECT_SHOW, Events.EVENT_OBJECT_HIDE,
            Events.EVENT_OBJECT_REORDER, Events.EVENT_OBJECT_STATECHANGE, Events.EVENT_OBJECT_LOCATIONCHANGE,
            Events.EVENT_OBJECT_NAMECHANGE, Events.EVENT_OBJECT_CLOAKED, Events.EVENT_OBJECT_UNCLOAKED,
        ];

        var hooked = new List<uint>();
        foreach (var (first, last) in WindowAdapter.ObjectEvents)
        {
            for (var @event = first; @event <= last; @event++)
                hooked.Add(@event);
        }

        Assert.AreSequenceEqual(read.Order(), hooked.Order());
    }

    [TestMethod]
    public void AWindowDestroyedMidDragLetsTheNextDragStart()
    {
        var core = new WindowManager();
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;
        var dragged = new WindowId(0x7001);
        var next = new WindowId(0x7003);

        Assert.IsTrue(adapter.StartDrag(dragged, sizing: false, new Point(10, 10)));
        Assert.IsFalse(adapter.StartDrag(next, sizing: true, new Point(10, 10)));

        adapter.Notice(Events.EVENT_OBJECT_DESTROY, (nint)dragged.Value);

        Assert.IsTrue(adapter.StartDrag(next, sizing: true, new Point(10, 10)));
    }

    [TestMethod]
    public void AClientsTitleChangeGoesToTheCore()
    {
        var core = new WindowManager();
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;
        var window = new WindowId(0x7009);
        adapter.Apply(core.Handle(new WindowAppeared(window, Area) { Traits = WindowTraits.Normal with { Title = "before" } }));
        var decided = new List<Outcome>();
        core.Decided = decided.Add;

        adapter.Notice(Events.EVENT_OBJECT_NAMECHANGE, (nint)window.Value);

        Assert.AreEqual("", Assert.ContainsSingle(decided).State.Clients.Single().Title);
    }

    [TestMethod]
    public void AHiddenClientThatIsCloakedStaysAClient()
    {
        var core = new WindowManager();
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;
        var window = new WindowId(0x7005);
        core.Handle(new WindowAppeared(window, Area) { Traits = WindowTraits.Normal });
        var away = core.Handle(new Dispatch("workspace", "2"));
        Assert.Contains(window, away.Hide);

        adapter.Notice(Events.EVENT_OBJECT_CLOAKED, (nint)window.Value);

        Assert.IsTrue(core.Manages(window));
    }

    [TestMethod]
    public void AClientItsApplicationHidesStopsBeingAClient()
    {
        var core = new WindowManager();
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;
        var window = new WindowId(0x700B);
        core.Handle(new WindowAppeared(window, Area) { Traits = WindowTraits.Normal });

        adapter.Notice(Events.EVENT_OBJECT_HIDE, (nint)window.Value);

        Assert.IsFalse(core.Manages(window));
    }

    [TestMethod]
    [DataRow(0x08280028, true)]
    [DataRow(0x00080020, true)]
    [DataRow(0x08280000, true)]
    [DataRow(0x08000000, true)]
    [DataRow(0x08040000, true)]
    [DataRow(0x00040138, true)]
    [DataRow(0x00000028, true)]
    [DataRow(0x00040008, false)]
    [DataRow(0x00080000, false)]
    [DataRow(0x00000020, false)]
    [DataRow(0x00000100, false)]
    public void AWindowThatCannotBeFocusedIsAnOverlay(int extended, bool overlay)
    {
        Assert.AreEqual(overlay, WindowAdapter.IsOverlay(extended));
    }

    [TestMethod]
    [DataRow("Shell_SecondaryTrayWnd", true)]
    [DataRow("Shell_SecondaryTrayWndProbe", false)]
    [DataRow("Shell_Secondary", false)]
    public void TheTaskbarIsKnownByItsClass(string className, bool taskbar)
    {
        ProbeNative.Register(className, ProbeNative.DefWindowProc);
        var hwnd = ProbeNative.CreateWindowEx(0, className, "", 0, 0, 0, 10, 10, 0, 0, ProbeNative.Module, 0);
        Assert.AreNotEqual(0, hwnd);
        try
        {
            Assert.AreEqual(taskbar, WindowAdapter.IsShellTaskbar(new Windows.Win32.Foundation.HWND(hwnd)));
        }
        finally
        {
            ProbeNative.DestroyWindow(hwnd);
        }
    }

    [TestMethod]
    public void AClientWinlandHidStaysAClient()
    {
        var core = new WindowManager();
        using var adapter = new WindowAdapter(core);
        core.ReducedMotion = () => true;
        var window = new WindowId(0x700D);
        core.Handle(new WindowAppeared(window, Area) { Traits = WindowTraits.Normal });
        var away = core.Handle(new Dispatch("workspace", "2"));
        Assert.Contains(window, away.Hide);
        adapter.Apply(away);

        adapter.Notice(Events.EVENT_OBJECT_HIDE, (nint)window.Value);

        Assert.IsTrue(core.Manages(window));
    }
}
