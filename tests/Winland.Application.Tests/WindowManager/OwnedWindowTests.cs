using System.Text.Json;

namespace Winland.Application.Tests;

public class OwnedWindowTests
{
    private static readonly WindowTraits Child = WindowTraits.Normal with { Dialog = true };

    [Fact]
    public void OwnedWindowFloatsAndStaysOutOfGroups()
    {
        var desk = new TestDesktop();
        var parent = desk.Spawn("child-test-parent");

        desk.Ok("focuswindow", "class:child-test-parent");
        desk.Ok("togglefloating");
        desk.Ok("focuswindow", "class:child-test-parent");
        desk.Ok("pin", "enable");

        desk.Spawn("child-test-child", traits: Child, owner: parent);
        desk.Ok("focuswindow", "class:child-test-child");
        Assert.Equal(2, desk.Clients().Length);
        Assert.Equal(2, desk.Clients().Count(TestDesktop.Pinned));

        Assert.Equal("ok", desk.Send("reload"));
        desk.Close("child-test-child");
        desk.Close("child-test-parent");
        Assert.Empty(desk.Clients());

        desk.Spawn("a");
        desk.Ok("togglegroup");

        var second = desk.Spawn("child-test-parent");
        Assert.Equal(2, desk.Clients().Length);
        desk.Spawn("child-test-child", traits: Child, owner: second);
        desk.Ok("focuswindow", "class:child-test-child");
        Assert.Equal(3, desk.Clients().Length);

        Assert.Equal(1, desk.Clients().Count(Ungrouped));
    }

    private static bool Ungrouped(JsonElement client) => client.GetProperty("grouped").GetArrayLength() == 0;
}
