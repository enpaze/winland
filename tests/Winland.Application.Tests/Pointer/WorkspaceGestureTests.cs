namespace Winland.Application.Tests;

public class WorkspaceGestureTests
{
    private const string ConfigGestures = """
        gesture = 3, horizontal, workspace
        """;

    private static string Config(string extra = "") =>
        ConfigGestures + "\n" + extra;

    [Fact]
    public void Gestures()
    {
        var desk = new TestDesktop(Config());
        var touchpad = new Touchpad(desk);
        desk.Spawn("a");
        Assert.Single(desk.Clients());


        touchpad.Swipe("left", 3);
        Assert.True(HasWorkspace(desk, 2));

        touchpad.Swipe("right", 3);
        Assert.False(HasWorkspace(desk, 2));

        touchpad.Swipe("right", 3);
        Assert.False(HasWorkspace(desk, 2));

        touchpad.Swipe("left", 3);
        Assert.True(HasWorkspace(desk, 2));

        touchpad.Swipe("right", 3);
        Assert.False(HasWorkspace(desk, 2));

        desk.Ok("workspace", "3");

        desk.Ok("workspace", "5");
        desk.Spawn("a");

        desk.Ok("workspace", "1");
        touchpad.Swipe("left", 3);
        Assert.Equal(5, desk.ActiveWorkspace().GetProperty("id").GetInt32());

        desk.Ok("workspace", "previous");
        Assert.Equal(1, desk.ActiveWorkspace().GetProperty("id").GetInt32());

        desk.Ok("workspace", "previous");
        Assert.Equal(5, desk.ActiveWorkspace().GetProperty("id").GetInt32());

        desk.Ok("workspace", "1");
    }

    private static bool HasWorkspace(TestDesktop desk, int id) =>
        desk.Workspaces().Any(workspace => workspace.GetProperty("id").GetInt32() == id && workspace.GetProperty("name").GetString() == $"{id}");

    private sealed class Touchpad(TestDesktop desk)
    {
        internal void Swipe(string direction, int fingers)
        {
            var (x, y) = direction switch
            {
                "down" => (0, 300),
                "up" => (0, -300),
                "left" => (-300, 0),
                _ => (300, 0),
            };

            _ = desk.Handle(new SwipeBegan(fingers));
            _ = desk.Handle(new SwipeUpdated(fingers, x, y));
            _ = desk.Handle(new SwipeEnded());
        }
    }
}
