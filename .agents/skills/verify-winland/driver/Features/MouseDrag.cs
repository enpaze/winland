using Windows.Win32.UI.Input.KeyboardAndMouse;

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
