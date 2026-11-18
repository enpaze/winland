namespace Winland.Infrastructure.Tests;

[TestClass]
public class FrameRestoreTests
{
    [TestMethod]
    public void ResettingDecorationsPutsBackTheFrameOfAFullscreenClient()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        var original = windows.Desktop[1].Style;

        windows.Dispatch("fullscreen");
        Assert.AreNotEqual(original, windows.Desktop[1].Style);

        windows.Adapter.ResetDecorations();

        Assert.AreEqual(original, windows.Desktop[1].Style);
    }

    [TestMethod]
    public void AnOutcomeAfterTheCleanupDoesNotStripTheFrameAgain()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        var original = windows.Desktop[1].Style;
        windows.Dispatch("fullscreen");
        windows.Dispatch("fullscreen");
        Assert.AreEqual(original, windows.Desktop[1].Style);

        windows.Adapter.RevealHidden();
        windows.Adapter.ResetDecorations();
        windows.Dispatch("fullscreen");

        Assert.AreEqual(original, windows.Desktop[1].Style);
    }
}
