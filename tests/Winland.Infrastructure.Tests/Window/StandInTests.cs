namespace Winland.Infrastructure.Tests;

[TestClass]
public class StandInTests
{
    private const string Config = "";
    private const uint Layered = 0x00080000;

    private static TestWindows Grow(out Rectangle before, Action<FakeDesktop>? arrange = null)
    {
        var windows = new TestWindows(Config);
        windows.Open(1);
        var second = windows.Open(2);
        windows.Settle();
        before = windows.Desktop[1].Visible;
        arrange?.Invoke(windows.Desktop);
        windows.Desktop.Calls.Clear();
        windows.Apply(windows.Core.Handle(new WindowClosed(second)));
        return windows;
    }

    private static int Posted(TestWindows windows) =>
        windows.Desktop.Calls.Count(call => call.StartsWith("bounds 1 ", StringComparison.Ordinal) && call.EndsWith(" Post", StringComparison.Ordinal));

    [TestMethod]
    public void AGrowingWindowShowsOnlyWhatItPaintedUntilItHasPaintedTheRest()
    {
        using var windows = Grow(out var before);
        var standIn = windows.Desktop.StandInOf(1);

        windows.Advance(16);
        Assert.IsTrue(standIn.Picture!.Value.Source.Width <= before.Width);
        Assert.IsNotNull(standIn.Right);

        windows.Advance(32);
        Assert.IsTrue(standIn.Picture!.Value.Source.Width <= before.Width);
        Assert.IsNotNull(standIn.Right);

        windows.Advance(16);
        Assert.IsTrue(standIn.Picture!.Value.Source.Width > before.Width);
        Assert.IsNull(standIn.Right);
    }

    [TestMethod]
    public void AGrowingWindowThatSaysItIsStillPaintingStaysCutOffPast50Milliseconds()
    {
        using var windows = Grow(out var before, desktop => desktop.PaintTime = 1000);
        var standIn = windows.Desktop.StandInOf(1);

        windows.Advance(32);
        windows.Advance(32);

        Assert.IsTrue(standIn.Picture!.Value.Source.Width <= before.Width);
        Assert.IsNotNull(standIn.Right);
    }

    [TestMethod]
    public void AStandInIsPlacedBeforeItsPictureIsFitted()
    {
        using var windows = Grow(out _);
        windows.Settle();
        var calls = windows.Desktop.Calls;

        var fits = 0;
        for (var i = 0; i < calls.Count; i++)
        {
            if (!calls[i].StartsWith("standin show 1 Picture", StringComparison.Ordinal))
                continue;

            Assert.StartsWith("standin place 1 ", calls[i - 1], StringComparison.Ordinal);
            fits++;
        }

        Assert.IsTrue(fits > 3, $"{fits} frames were fitted.");
    }

    [TestMethod]
    public void AWindowThatShrinksKeepsItsLargerSizeUntilTheMotionEnds()
    {
        using var windows = new TestWindows(Config);
        windows.Open(1);
        windows.Settle();
        var shrinking = windows.Desktop[1];
        var wide = shrinking.Visible.Width;

        windows.Open(2);
        windows.Advance(32);
        windows.Advance(32);
        Assert.AreEqual(wide, shrinking.Visible.Width);

        windows.Settle();
        Assert.IsTrue(shrinking.Visible.Width < wide);
    }

    [TestMethod]
    public void AWindowThatChangesSizeIsGivenItsSizeOnceAndNotOnEveryFrame()
    {
        using var windows = new TestWindows(Config);
        windows.Open(1);
        windows.Settle();
        windows.Desktop.Calls.Clear();

        windows.Open(2);
        windows.Settle();

        var calls = windows.Desktop.Calls;
        Assert.AreEqual(1, Posted(windows));
        var placed = calls.FindIndex(call => call.StartsWith("bounds 1 ", StringComparison.Ordinal) && call.EndsWith(" Wait", StringComparison.Ordinal));
        Assert.IsTrue(placed > 0, "The window was not placed.");
        Assert.IsTrue(calls.IndexOf("standin close 1") > placed);
    }

    [TestMethod]
    public void AStandInGivesTheWindowItsAlphaBackAndNoBorderWhenTheMotionEnds()
    {
        using var windows = Grow(out _);
        var window = windows.Desktop[1];
        Assert.AreEqual(0, window.Alpha);
        Assert.AreEqual(0xFFFFFFFEu, window.Attributes[34]);

        windows.Settle();

        Assert.IsTrue(windows.Desktop.StandInOf(1).Closed);
        Assert.AreEqual(0u, window.ExStyle & Layered);
        Assert.AreEqual(0xFFFFFFFEu, window.Attributes[34]);
    }

    [TestMethod]
    public void AWindowThatOpensKeepsTheBorderWinlandTookOffWhenItsStandInEnds()
    {
        using var windows = new TestWindows(Config);
        windows.Open(1);
        Assert.Contains("standin open 1", windows.Desktop.Calls);

        windows.Settle();

        Assert.IsTrue(windows.Desktop.StandInOf(1).Closed);
        Assert.AreEqual(0xFFFFFFFEu, windows.Desktop[1].Attributes[34]);
    }

    [TestMethod]
    public void AWindowItsProgramMadeLayeredIsResizedOnEveryFrameInsteadOfStoodInFor()
    {
        using var windows = Grow(out _, desktop => desktop[1].ExStyle = Layered);
        Assert.IsTrue(windows.Desktop.StandInOf(1).Closed);
        windows.Desktop.Calls.Clear();

        windows.Advance(16);
        windows.Advance(16);
        windows.Advance(16);

        Assert.IsTrue(Posted(windows) >= 3, $"{Posted(windows)} frames were sent.");
    }

    [TestMethod]
    public void AWindowWithNoPictureToShowIsResizedOnEveryFrame()
    {
        using var windows = Grow(out _, desktop => desktop.CanStandIn = false);
        Assert.DoesNotContain("standin open 1", windows.Desktop.Calls);
        windows.Desktop.Calls.Clear();

        windows.Advance(16);
        windows.Advance(16);
        windows.Advance(16);

        Assert.IsTrue(Posted(windows) >= 3, $"{Posted(windows)} frames were sent.");
    }

}
