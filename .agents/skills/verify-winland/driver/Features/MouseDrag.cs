using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "mouse-drag")]
[TestClass]
public class MouseDrag
{

    public TestContext TestContext { get; set; } = null!;
        private const string Config =
        """
        bind = CTRL ALT SHIFT, mouse:left, movewindow
        bind = CTRL ALT SHIFT, mouse:right, resizewindow
        windowrule = float, class:WinlandVerifyWindow, title:WinlandDrag

        """;

    private static readonly VIRTUAL_KEY[] Modifiers = [VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_MENU, VIRTUAL_KEY.VK_SHIFT];

    [VerifyFact]
    public void MovesAndResizesAFloatingWindowWithTheMouse() => VerifyRun.Drive(TestContext, Config, run =>
    {
        var binds = run.Ask("binds");
        Assert.Contains("bind = SHIFT CTRL ALT, mouse:left, movewindow", binds, StringComparison.Ordinal);
        Assert.Contains("bind = SHIFT CTRL ALT, mouse:right, resizewindow", binds, StringComparison.Ordinal);
        run.Say("saw: binds echoes both drags with the plain bind keyword");

        var window = run.OpenTitled("WinlandDrag", 400, 300);
        run.Until("the window floats", () => run.Client(window)?.GetProperty("floating").GetBoolean() == true);
        run.Focus(window);
        var start = VerifyRun.Frame(window);
        var from = ((start.Left + start.Right) / 2, (start.Top + start.Bottom) / 2);

        Drag(right: false, from, (from.Item1 + 160, from.Item2 + 90));
        run.Say("dragged with Ctrl+Alt+Shift and the left button by 160,90");
        run.Until("movewindow moved the window with the pointer", () =>
        {
            var moved = VerifyRun.Frame(window);
            return Math.Abs(moved.Left - start.Left - 160) <= 12 && Math.Abs(moved.Top - start.Top - 90) <= 12 && Math.Abs(moved.Width - start.Width) <= 4;
        });

        var placed = VerifyRun.Frame(window);
        var corner = (placed.Right - 20, placed.Bottom - 20);
        Drag(right: true, corner, (corner.Item1 + 120, corner.Item2 + 70));
        run.Say("dragged with Ctrl+Alt+Shift and the right button from the bottom right corner by 120,70");
        run.Until("resizewindow grew the window from the grabbed corner", () =>
        {
            var resized = VerifyRun.Frame(window);
            return resized.Width >= placed.Width + 90 && resized.Height >= placed.Height + 50 && Math.Abs(resized.Left - placed.Left) <= 6;
        });
    });

    [VerifyFact]
    public void AMoveKeepsTheSizeAResizeGave() => VerifyRun.Drive(TestContext, Config, run =>
    {
        var window = run.OpenTitled("WinlandDrag", 400, 300);
        run.Until("the window floats", () => run.Client(window)?.GetProperty("floating").GetBoolean() == true);
        run.Focus(window);

        var start = VerifyRun.Frame(window);
        var corner = (start.Right - 20, start.Bottom - 20);
        Drag(right: true, corner, (corner.Item1 + 120, corner.Item2 + 70));
        run.Say("dragged with Ctrl+Alt+Shift and the right button from the bottom right corner by 120,70");
        run.Until("resizewindow grew the window", () => VerifyRun.Frame(window).Width >= start.Width + 90);
        KeepsSizeWhileMoved(run, window, "after resizewindow");

        var framed = VerifyRun.Frame(window);
        var edge = (framed.Right + 2, framed.Bottom + 2);
        Desktop.Glide(edge.Item1 - 30, edge.Item2 - 30, edge.Item1, edge.Item2, 6);
        Desktop.Button(right: false, up: false);
        Desktop.Glide(edge.Item1, edge.Item2, edge.Item1 + 90, edge.Item2 + 60);
        Thread.Sleep(150);
        Desktop.Button(right: false, up: true);
        run.Say("dragged the bottom right sizing border with the left button by 90,60");
        run.Until("the border drag grew the window", () => VerifyRun.Frame(window).Width >= framed.Width + 60, detail: () => VerifyRun.Frame(window).ToString());
        KeepsSizeWhileMoved(run, window, "after a border drag");

        var own = Desktop.Rect(window);
        PInvoke.SetWindowPos(window, HWND.Null, own.Left, own.Top, own.Width + 80, own.Height + 50, SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        run.Say("the window resized itself by 80,50");
        run.Until("winland reports the size the window gave itself", () =>
        {
            var size = run.ClientOf(window).GetProperty("size");
            var frame = VerifyRun.Frame(window);
            return frame.Width >= own.Width + 60 && size[0].GetInt32() == frame.Width && size[1].GetInt32() == frame.Height;
        }, detail: () => $"{VerifyRun.Frame(window)} winland {run.ClientOf(window).GetProperty("size")}");
        KeepsSizeWhileMoved(run, window, "after the window resized itself");
    });

    [VerifyFact]
    public void ALiftedTileKeepsTheSizeAResizeGave() => VerifyRun.Drive(TestContext, Config, run =>
    {
        var (left, right) = run.OpenPair();
        var split = VerifyRun.Frame(left);
        var border = (split.Right, (split.Top + split.Bottom) / 2);
        Drag(right: true, (border.Item1 - 20, border.Item2), (border.Item1 + 100, border.Item2));
        run.Say("dragged with Ctrl+Alt+Shift and the right button across the split by 120");
        run.Until("resizewindow widened the left tile", () => VerifyRun.Frame(left).Width >= split.Width + 80, detail: () => VerifyRun.Frame(left).ToString());

        var tile = VerifyRun.Frame(left);
        var from = ((tile.Left + tile.Right) / 2, (tile.Top + tile.Bottom) / 2);
        Hold(right: false, from, (from.Item1 + 60, from.Item2 + 40), () =>
            run.Until($"the lifted tile keeps its resized size {tile.Width}x{tile.Height}", () =>
            {
                var lifted = VerifyRun.Frame(left);
                return lifted.Left != tile.Left && Math.Abs(lifted.Width - tile.Width) <= 4 && Math.Abs(lifted.Height - tile.Height) <= 4;
            }, detail: () => VerifyRun.Frame(left).ToString()));
        run.Until("the tile drops back beside the other", () => VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left))
            || VerifyRun.SideBySide(VerifyRun.Frame(right), VerifyRun.Frame(left), VerifyRun.WorkArea(left)));
    });

    private static void KeepsSizeWhileMoved(VerifyRun run, HWND window, string after)
    {
        var sized = VerifyRun.Frame(window);
        var from = ((sized.Left + sized.Right) / 2, (sized.Top + sized.Bottom) / 2);
        Drag(right: false, from, (from.Item1 - 100, from.Item2 - 60));
        run.Say($"dragged with Ctrl+Alt+Shift and the left button by -100,-60 {after}");
        run.Until($"movewindow moved the window and kept its {sized.Width}x{sized.Height} size {after}", () =>
        {
            var moved = VerifyRun.Frame(window);
            return Math.Abs(moved.Left - sized.Left + 100) <= 12 && Math.Abs(moved.Width - sized.Width) <= 4 && Math.Abs(moved.Height - sized.Height) <= 4;
        }, detail: () => VerifyRun.Frame(window).ToString());
    }

    private static void Drag(bool right, (int X, int Y) from, (int X, int Y) to) => Hold(right, from, to, () => Thread.Sleep(150));

    private static void Hold(bool right, (int X, int Y) from, (int X, int Y) to, Action held)
    {
        Desktop.Glide(from.X - 30, from.Y - 30, from.X, from.Y, 6);
        foreach (var modifier in Modifiers)
            Desktop.Key(modifier, up: false);

        try
        {
            Desktop.Button(right, up: false);
            try
            {
                Desktop.Glide(from.X, from.Y, to.X, to.Y);
                held();
            }
            finally
            {
                Desktop.Button(right, up: true);
            }
        }
        finally
        {
            foreach (var modifier in Modifiers.Reverse())
                Desktop.Key(modifier, up: true);
        }
    }
}
