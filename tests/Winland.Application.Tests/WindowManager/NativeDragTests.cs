namespace Winland.Application.Tests;

[TestClass]
public class NativeDragTests
{
    [TestMethod]
    public void NativeMoveAndResizeFollowThePointer()
    {
        var desk = new TestDesktop();
        var client = desk.Spawn("xdg-interactive");
        var pid = desk.Client("xdg-interactive").GetProperty("pid").GetInt64();
        desk.Ok("focuswindow", $"pid:{pid}");

        SetClientGeometry(desk, pid, new Rectangle(300, 300, 500, 400));

        var startMove = ActiveGeometry(desk);
        desk.MovePointer(startMove.X + 100, startMove.Y + 100);
        Press(desk);
        desk.Handle(new WindowMoveStarted(client, desk.Pointer));
        desk.MovePointer(startMove.X + 220, startMove.Y + 180);
        Release(desk);
        desk.Handle(new WindowMoveEnded(client, desk.Pointer, startMove with { X = startMove.X + 120, Y = startMove.Y + 80 }));

        var endMove = ActiveGeometry(desk);
        Near(startMove.X + 120, endMove.X, 5);
        Near(startMove.Y + 80, endMove.Y, 5);
        Near(startMove.Width, endMove.Width, 2);
        Near(startMove.Height, endMove.Height, 2);

        SetClientGeometry(desk, pid, new Rectangle(400, 300, 500, 400));
        Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));
        var startResize = ActiveGeometry(desk);
        desk.MovePointer(startResize.X + 250, startResize.Y + 200);
        Press(desk);
        desk.Handle(new WindowSizeStarted(client, desk.Pointer));
        desk.MovePointer(startResize.X + 410, startResize.Y + 200);
        desk.Handle(new WindowSizing(client, desk.Pointer, startResize with { Width = startResize.Width + 160 }));
        Assert.IsTrue(ActiveGeometry(desk).Width >= startResize.Width + 150, "The resize did not change the frame before the release.");

        Release(desk);
        desk.Handle(new WindowSizeEnded(client, desk.Pointer, startResize with { Width = startResize.Width + 160 }));

        var endResize = ActiveGeometry(desk);
        Near(startResize.X, endResize.X, 2);
        Near(startResize.Y, endResize.Y, 2);
        Near(startResize.Width + 160, endResize.Width, 5);
        Near(startResize.Height, endResize.Height, 2);

        SetClientGeometry(desk, pid, new Rectangle(500, 300, 500, 400));
        var stale = ActiveGeometry(desk);
        desk.MovePointer(stale.X + 250, stale.Y + 200);
        desk.Click();
        desk.MovePointer(stale.X + 380, stale.Y + 200);

        var afterStale = ActiveGeometry(desk);
        Near(stale.X, afterStale.X, 2);
        Near(stale.Y, afterStale.Y, 2);
        Near(stale.Width, afterStale.Width, 2);
        Near(stale.Height, afterStale.Height, 2);

        desk.MovePointer(afterStale.X + 250, afterStale.Y + 200);
        desk.Click();
        Assert.AreNotEqual("", desk.Send("clients"));
    }

    private static void SetClientGeometry(TestDesktop desk, long pid, Rectangle geometry)
    {
        desk.Ok("focuswindow", $"pid:{pid}");
        if (!TestDesktop.Floating(desk.ActiveWindow()))
            desk.Ok("togglefloating");

        desk.Place(desk.ActiveId(), geometry.X, geometry.Y, geometry.Width, geometry.Height);
        desk.Ok("focuswindow", $"pid:{pid}");
        Assert.AreEqual(geometry, ActiveGeometry(desk));
    }

    private static Rectangle ActiveGeometry(TestDesktop desk)
    {
        var active = desk.ActiveWindow();
        var at = active.GetProperty("at");
        var size = active.GetProperty("size");
        return new Rectangle(at[0].GetInt32(), at[1].GetInt32(), size[0].GetInt32(), size[1].GetInt32());
    }

    private static void Press(TestDesktop desk) =>
        desk.Handle(new PointerButtonPressed("mouse:left") { Position = desk.Pointer });

    private static void Release(TestDesktop desk) =>
        desk.Handle(new PointerButtonReleased("mouse:left") { Position = desk.Pointer });

    private static void Near(int expected, int actual, int delta) =>
        Assert.IsInRange(expected - delta, expected + delta, actual);
}
