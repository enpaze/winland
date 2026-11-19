using Windows.Win32;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "decoration")]
[TestClass]
public class Decoration
{

    public TestContext TestContext { get; set; } = null!;
        private const string Config =
        """
        border_size = 6
        border_color = 0xffff0000
        border_color_inactive = 0xff0000ff
        inactive_opacity = 0.5

        """;

    [VerifyFact]
    public void DrawsBordersAndDimsInactiveWindows() => VerifyRun.Drive(TestContext, Config, run =>
    {
        var (left, right) = run.OpenPair();
        run.Focus(left);

        run.Until("the inactive window is half transparent and the active one opaque", () =>
            Desktop.Alpha(right) is >= 120 and <= 135 && Desktop.Alpha(left) is null or 255);
        run.Until("a border window surrounds the focused window", () => BorderAround(run, left), detail: () => Describe(run, left));

        run.Focus(right);
        run.Until("focus moves the transparency to the other window", () =>
            Desktop.Alpha(left) is >= 120 and <= 135 && Desktop.Alpha(right) is null or 255);
        run.Until("a border window surrounds the newly focused window", () => BorderAround(run, right), detail: () => Describe(run, right));
    });

    private static string Describe(VerifyRun run, Windows.Win32.Foundation.HWND hwnd) =>
        $"frame {VerifyRun.Frame(hwnd)}, border windows: "
        + string.Join("; ", Desktop.WindowsOf(run.Daemon.Id, "WinlandBorder").Select(border => $"{Desktop.Rect(border)} visible={PInvoke.IsWindowVisible(border)}"));

    private static bool BorderAround(VerifyRun run, Windows.Win32.Foundation.HWND hwnd)
    {
        var frame = VerifyRun.Frame(hwnd);
        var near = new Box(frame.Left - 16, frame.Top - 16, frame.Right + 16, frame.Bottom + 16);
        var pieces = Desktop.WindowsOf(run.Daemon.Id, "WinlandBorder")
            .Where(border => PInvoke.IsWindowVisible(border))
            .Select(Desktop.Rect)
            .Where(piece => piece.Width > 0 && piece.Height > 0 && piece.Left >= near.Left && piece.Top >= near.Top && piece.Right <= near.Right && piece.Bottom <= near.Bottom)
            .ToList();
        return pieces.Count >= 4
            && pieces.Min(piece => piece.Left) <= frame.Left - 4
            && pieces.Min(piece => piece.Top) <= frame.Top - 4
            && pieces.Max(piece => piece.Right) >= frame.Right + 4
            && pieces.Max(piece => piece.Bottom) >= frame.Bottom + 4;
    }
}
