namespace Winland.Infrastructure.Tests;
[TestClass]
public class MinimizeMaximizeTests
{
    private const int Minimize = 6;
    private const int Maximize = 3;

    [DesktopFact]
    public void AMinimizedWindowIsRestoredAndAMaximizedOneBecomesMaximizeMode()
    {
        _ = ProbeNative.SetProcessDpiAwarenessContext(new IntPtr(-4));
        using var probe = new LimitProbe();
        probe.Open();
        try
        {
            var core = new WindowManager();
            using var adapter = new WindowAdapter(core);
            core.ReducedMotion = () => true;
            adapter.AdoptExisting([probe.Anchor, probe.Wide]);

            _ = ProbeNative.ShowWindow(probe.Anchor, Minimize);
            Assert.IsTrue(ProbeNative.IsIconic(probe.Anchor));
            adapter.UndoMinimize(probe.Anchor);
            Assert.IsFalse(ProbeNative.IsIconic(probe.Anchor));

            _ = ProbeNative.ShowWindow(probe.Wide, Maximize);
            Assert.IsTrue(ProbeNative.IsZoomed(probe.Wide));
            var outcome = adapter.RequestMaximize(probe.Wide);
            Assert.IsFalse(ProbeNative.IsZoomed(probe.Wide));
            Assert.AreEqual(FullscreenMode.Maximized, Assert.ContainsSingle(candidate => candidate.Window == new WindowId(probe.Wide.ToInt64()), outcome.State.Clients).Fullscreen);
        }
        finally
        {
            probe.Close();
        }
    }
}
