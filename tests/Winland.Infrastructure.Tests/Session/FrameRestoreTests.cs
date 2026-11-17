namespace Winland.Infrastructure.Tests;

public class FrameRestoreTests
{
    [Fact]
    public void ResettingDecorationsPutsBackTheFrameOfAFullscreenClient()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        var original = windows.Desktop[1].Style;

        windows.Dispatch("fullscreen", "2");
        Assert.NotEqual(original, windows.Desktop[1].Style);

        windows.Adapter.ResetDecorations();

        Assert.Equal(original, windows.Desktop[1].Style);
    }

    [Fact]
    public void AnOutcomeAfterTheCleanupDoesNotStripTheFrameAgain()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        var original = windows.Desktop[1].Style;
        windows.Dispatch("fullscreen", "2");
        windows.Dispatch("fullscreen", "2");
        Assert.Equal(original, windows.Desktop[1].Style);

        windows.Adapter.RevealHidden();
        windows.Adapter.ResetDecorations();
        windows.Dispatch("fullscreen", "2");

        Assert.Equal(original, windows.Desktop[1].Style);
    }
}
