using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "mouse-drag")]
[TestClass]
public class MouseDrag
{

    public TestContext TestContext { get; set; } = null!;
        private const string Config =
        """
        bind = Ctrl+Alt+Shift+mouse:left, move
        bind = Ctrl+Alt+Shift+mouse:right, resize
        windowrule = float, class:WinlandVerifyWindow, title:WinlandDrag

        """;

    private static readonly VIRTUAL_KEY[] Modifiers = [VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_MENU, VIRTUAL_KEY.VK_SHIFT];

    private const int NeighbourLayoutMs = 40;
    private const int SizeDistance = 320;
    private const int SizeSteps = 16;
    private const int HoldMs = 600;
    private const int CatchUpMs = 250;
    private const uint HitTest = 0x0084;
    private const int HitLeft = 10;

    [VerifyFact]
    public void MovesAndResizesAFloatingWindowWithTheMouse() => VerifyRun.Drive(TestContext, Config, run =>
    {
        var binds = run.Ask("get", "binds");
        Assert.Contains("bind = Ctrl+Alt+Shift+mouse:left, move", binds, StringComparison.Ordinal);
        Assert.Contains("bind = Ctrl+Alt+Shift+mouse:right, resize", binds, StringComparison.Ordinal);
        run.Say("saw: binds echoes both drags with the plain bind keyword");

        var window = run.OpenTitled("WinlandDrag", 400, 300);
        run.Until("the window floats", () => run.Client(window)?.GetProperty("floating").GetBoolean() == true);
        run.Focus(window);
        var start = VerifyRun.Frame(window);
        var from = ((start.Left + start.Right) / 2, (start.Top + start.Bottom) / 2);

        Drag(right: false, from, (from.Item1 + 160, from.Item2 + 90));
        run.Say("dragged with Ctrl+Alt+Shift and the left button by 160,90");
        run.Until("a move drag moved the window with the pointer", () =>
        {
            var moved = VerifyRun.Frame(window);
            return Math.Abs(moved.Left - start.Left - 160) <= 12 && Math.Abs(moved.Top - start.Top - 90) <= 12 && Math.Abs(moved.Width - start.Width) <= 4;
        });

        var placed = VerifyRun.Frame(window);
        var corner = (placed.Right - 20, placed.Bottom - 20);
        Drag(right: true, corner, (corner.Item1 + 120, corner.Item2 + 70));
        run.Say("dragged with Ctrl+Alt+Shift and the right button from the bottom right corner by 120,70");
        run.Until("a resize drag grew the window from the grabbed corner", () =>
        {
            var resized = VerifyRun.Frame(window);
            return resized.Width >= placed.Width + 90 && resized.Height >= placed.Height + 50 && Math.Abs(resized.Left - placed.Left) <= 6;
        });
    });

    [VerifyFact]
    public void ATiledNeighbourFollowsABorderDragLive() => VerifyRun.Drive(TestContext, Config + "gaps_in = 8\n", run =>
    {
        using var slow = new VerifyWindows(NeighbourLayoutMs);
        var left = slow.Open(VerifyRun.Title + " 1");
        run.Say($"opened test window '{VerifyRun.Title} 1' {VerifyRun.Address(left)}, which takes {NeighbourLayoutMs} ms to take each move");
        run.Until("the slow window is a Client", () => run.Client(left) is not null);
        var tile = VerifyRun.Frame(left);
        Desktop.MoveMouse(tile.Left + (tile.Width * 3 / 4), (tile.Top + tile.Bottom) / 2);
        var right = run.OpenManaged("2");
        run.Until("the two windows tile side by side with the slow one on the left", () =>
            VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left))
            && VerifyRun.Frame(left).Left < VerifyRun.Frame(right).Left);
        run.Focus(right);

        var start = VerifyRun.Frame(right);
        var rest = Gap(left, right);
        var grab = (X: start.Left - 3, Y: (start.Top + start.Bottom) / 2);
        var leftFrame = VerifyRun.Frame(left);
        Assert.IsTrue(grab.X >= leftFrame.Right, $"the grab {grab} is inside the left window {leftFrame}");
        var hit = (int)PInvoke.SendMessage(right, HitTest, default, new LPARAM(((grab.Y & 0xFFFF) << 16) | (grab.X & 0xFFFF))).Value;
        Assert.AreEqual(HitLeft, hit, $"{grab} is not on the left sizing border of {VerifyRun.Address(right)}");
        run.Say($"saw: the windows meet with a {rest} px gap, and {grab} is on the right window's left sizing border");

        var samples = new List<(long At, int Gap)>();
        var clock = Stopwatch.StartNew();
        var sampling = true;
        var sampler = new Thread(() =>
        {
            while (Volatile.Read(ref sampling))
            {
                var gap = Gap(left, right);
                lock (samples)
                    samples.Add((clock.ElapsedMilliseconds, gap));
                Thread.Sleep(0);
            }
        })
        { IsBackground = true, Name = "WinlandVerifyGap" };

        long stopped;
        long released;
        Desktop.Glide(grab.X - 30, grab.Y, grab.X, grab.Y, 6);
        Desktop.Button(right: false, up: false);
        try
        {
            clock.Restart();
            sampler.Start();
            Desktop.Glide(grab.X, grab.Y, grab.X + SizeDistance, grab.Y, SizeSteps);
            stopped = clock.ElapsedMilliseconds;
            Thread.Sleep(HoldMs);
            released = clock.ElapsedMilliseconds;
            var sized = VerifyRun.Frame(right);
            Assert.IsTrue(sized.Left >= start.Left + SizeDistance - 12, $"the border drag left the right window's left edge at {sized.Left}, from {start.Left}");
        }
        finally
        {
            Desktop.Button(right: false, up: true);
            Volatile.Write(ref sampling, false);
            sampler.Join(TimeSpan.FromSeconds(2));
        }

        List<(long At, int Gap)> seen;
        lock (samples)
            seen = [.. samples];

        var during = seen.Where(sample => sample.At < stopped).ToList();
        var held = seen.Where(sample => sample.At >= stopped && sample.At < released).ToList();
        Assert.IsNotEmpty(during, "no gap was sampled during the drag");
        Assert.IsNotEmpty(held, "no gap was sampled while the border was held still");
        run.Say($"saw: during the {SizeSteps}-step, {SizeDistance} px drag over {stopped} ms the gap was {rest} px in {during.Count(sample => sample.Gap == rest)} of {during.Count} samples, and at most {during.Max(sample => Math.Abs(sample.Gap - rest))} px off");

        var open = held.FindLastIndex(sample => sample.Gap != rest);
        var closed = open < 0 ? stopped : held[Math.Min(open + 1, held.Count - 1)].At;
        Assert.IsTrue(open < held.Count - 1 && closed - stopped <= CatchUpMs,
            $"with the border held still the gap was still {held[^1].Gap} px after {held[^1].At - stopped} ms, closing at {(open < held.Count - 1 ? closed - stopped : -1)} ms, over a budget of {CatchUpMs} ms");
        run.Say($"saw: with the border held still the gap was back to {rest} px {closed - stopped} ms after the last step and stayed there for the rest of the {HoldMs} ms hold, over {held.Count} samples");

        run.Until("the windows still meet with no gap after the border is let go", () => Gap(left, right) == rest);
    });

    private static int Gap(HWND left, HWND right) => VerifyRun.Frame(right).Left - VerifyRun.Frame(left).Right;

    private static void Drag(bool right, (int X, int Y) from, (int X, int Y) to)
    {
        Desktop.Glide(from.X - 30, from.Y - 30, from.X, from.Y, 6);
        foreach (var modifier in Modifiers)
            Desktop.Key(modifier, up: false);

        try
        {
            Desktop.Button(right, up: false);
            Desktop.Glide(from.X, from.Y, to.X, to.Y);
            Thread.Sleep(150);
            Desktop.Button(right, up: true);
        }
        finally
        {
            foreach (var modifier in Modifiers.Reverse())
                Desktop.Key(modifier, up: true);
        }
    }
}
