namespace Winland.Application.Tests;

[TestClass]
public class WindowDragTests
{
    private const string DragOnLeftButton = """
        unbind = SUPER, mouse:left
        bind = , mouse:left, move
        """;

    [TestMethod]
    public void DragTiledCentered()
    {
        var desk = new TestDesktop(DragOnLeftButton);
        desk.Spawn("kitty");
        Assert.ContainsSingle(desk.Clients());
        desk.Ok("focus", "class:kitty");

        var (grabX, grabY) = Grab(desk);
        MoveCursor(desk, grabX, grabY);
        Click(desk, pressed: true);
        MoveCursor(desk, 700, 500);

        var moved = Active(desk);
        Assert.IsTrue(Nearly(moved.X + (moved.Width / 2), 700));
        Assert.IsTrue(Nearly(moved.Y + (moved.Height / 2), 500));

        Click(desk, pressed: false);
    }

    [TestMethod]
    public void DragFullscreenCentered()
    {
        var desk = new TestDesktop(DragOnLeftButton);
        desk.Spawn("kitty");
        Assert.ContainsSingle(desk.Clients());
        desk.Ok("focus", "class:kitty");
        desk.Ok("float");
        desk.Place("kitty", 100, 100, 600, 400);
        desk.Ok("fullscreen", "on");

        var (grabX, grabY) = Grab(desk);
        MoveCursor(desk, grabX, grabY);
        Click(desk, pressed: true);
        MoveCursor(desk, 700, 500);

        var moved = Active(desk);
        Assert.AreEqual(0, TestDesktop.Fullscreen(desk.ActiveWindow()));
        Assert.IsTrue(Nearly(moved.X + (moved.Width / 2), 700));
        Assert.IsTrue(Nearly(moved.Y + (moved.Height / 2), 500));

        Click(desk, pressed: false);
    }

    private static Rectangle Active(TestDesktop desk) => desk.Placed(desk.ActiveId());

    private static (int X, int Y) Grab(TestDesktop desk)
    {
        var bounds = Active(desk);
        return (bounds.X + (bounds.Width * 3 / 4), bounds.Y + (bounds.Height * 3 / 4));
    }

    private static void MoveCursor(TestDesktop desk, int x, int y)
    {
        desk.MovePointer(x, y);
    }

    private static void Click(TestDesktop desk, bool pressed)
    {
        if (pressed)
            desk.Handle(new PointerButtonPressed("mouse:left") { Position = desk.Pointer });
        else
            desk.Handle(new PointerButtonReleased("mouse:left") { Position = desk.Pointer });
    }

    private static void Scroll(TestDesktop desk, int delta) =>
        desk.Handle(new WheelTurned(delta > 0 ? "mouse_down" : "mouse_up", Environment.TickCount64));

    private static bool Nearly(int a, int b, int tolerance = 4) => Math.Abs(a - b) <= tolerance;
}
