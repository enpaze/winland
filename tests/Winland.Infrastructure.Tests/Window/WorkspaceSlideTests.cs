namespace Winland.Infrastructure.Tests;

[TestClass]
public class WorkspaceSlideTests
{
    private static readonly Rectangle Left = new(0, 0, 1920, 1080);
    private static readonly Rectangle Right = new(1920, 0, 1920, 1080);

    [TestMethod]
    [DataRow(0, "1", "2")]
    [DataRow(0, "2", "1")]
    [DataRow(1920, "1", "2")]
    [DataRow(1920, "2", "1")]
    public void AWorkspaceSlideIsDrawnOnlyInsideItsMonitor(int monitorX, string from, string to)
    {
        var own = monitorX == 0 ? Left : Right;
        var other = monitorX == 0 ? Right : Left;
        var name = monitorX == 0 ? "LEFT" : "RIGHT";
        using var windows = new TestWindows("gaps_in = 0\ngaps_out = 0\n");
        windows.Apply(windows.Core.Handle(new MonitorsChanged(
        [
            new Monitor("LEFT", "Left", Left.X, Left.Y, Left.Width, Left.Height, 1, true, Left),
            new Monitor("RIGHT", "Right", Right.X, Right.Y, Right.Width, Right.Height, 1, true, Right),
        ])));
        windows.Dispatch("focus-monitor", name);
        Show(windows, from);
        Open(windows, 1, own);
        windows.Settle();
        Show(windows, to);
        Open(windows, 2, own);
        windows.Settle();
        Show(windows, from);

        string? landedOutside = null;
        windows.Desktop.OnSetBounds = id =>
        {
            var window = windows.Desktop[id.Value];
            if (landedOutside is null && window.Shown && Overlaps(Drawn(window), other))
                landedOutside = $"{name} {from}->{to}: window {id.Value} landed on the other monitor at {Text(Drawn(window))} before its clip followed.";
        };

        windows.Dispatch("workspace", to);

        var leaving = windows.Desktop[1];
        var start = leaving.Visible.X;
        var min = start;
        var max = start;
        var sawOutside = false;
        var drawnFrames = 0;
        for (var frame = 0; frame < 40; frame++)
        {
            windows.Advance(20);
            foreach (var id in new long[] { 1, 2 })
            {
                var window = windows.Desktop[id];
                if (!window.Shown)
                    continue;

                var drawn = Drawn(window);
                drawnFrames++;
                Assert.IsFalse(
                    Overlaps(drawn, other),
                    $"{name} {from}->{to}: window {id} was drawn on the other monitor at {Text(drawn)}. Its frame was {Text(window.Visible)}.");
                Assert.IsTrue(
                    Covers(own, drawn),
                    $"{name} {from}->{to}: window {id} was drawn outside its monitor at {Text(drawn)}. Its frame was {Text(window.Visible)}.");
            }

            min = Math.Min(min, leaving.Visible.X);
            max = Math.Max(max, leaving.Visible.X);
            if (leaving.Shown && !Covers(own, leaving.Visible))
                sawOutside = true;
        }

        Assert.IsNull(landedOutside, landedOutside);
        Assert.IsTrue(drawnFrames > 0, $"{name} {from}->{to}: the slide was never drawn.");
        Assert.IsTrue(sawOutside, $"{name} {from}->{to}: the window never slid past the monitor edge. Its left edge stayed at {start}.");
        var higher = int.Parse(to, System.Globalization.CultureInfo.InvariantCulture) > int.Parse(from, System.Globalization.CultureInfo.InvariantCulture);
        if (higher)
            Assert.IsTrue(min < start, $"{name} {from}->{to}: a higher workspace did not slide the old window left.");
        else
            Assert.IsTrue(max > start, $"{name} {from}->{to}: a lower workspace did not slide the old window right.");
    }

    private static void Show(TestWindows windows, string workspace)
    {
        windows.Dispatch("workspace", workspace);
        windows.Settle();
    }

    private static void Open(TestWindows windows, long id, Rectangle area)
    {
        windows.Desktop.Add(id, area);
        windows.Apply(windows.Core.Handle(new WindowAppeared(new WindowId(id), area) { Traits = WindowTraits.Normal }));
    }

    private static Rectangle Drawn(FakeWindow window)
    {
        var outline = window.RegionOutline;
        if (outline.Length == 0)
            return window.Visible;

        if (outline.Length == 1)
            return default;

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        foreach (var point in outline)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        var outer = window.Outer;
        return new Rectangle(outer.X + minX, outer.Y + minY, maxX - minX, maxY - minY);
    }

    private static bool Overlaps(Rectangle one, Rectangle other) =>
        one.Width > 0
        && one.Height > 0
        && other.Width > 0
        && other.Height > 0
        && one.X < other.X + other.Width
        && other.X < one.X + one.Width
        && one.Y < other.Y + other.Height
        && other.Y < one.Y + one.Height;

    private static bool Covers(Rectangle monitor, Rectangle drawn) =>
        drawn.Width <= 0
        || drawn.Height <= 0
        || (drawn.X >= monitor.X
            && drawn.Y >= monitor.Y
            && drawn.X + drawn.Width <= monitor.X + monitor.Width
            && drawn.Y + drawn.Height <= monitor.Y + monitor.Height);

    private static string Text(Rectangle rectangle) => $"{rectangle.X},{rectangle.Y} {rectangle.Width}x{rectangle.Height}";
}
