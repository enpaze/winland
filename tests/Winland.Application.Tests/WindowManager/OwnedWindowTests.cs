using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class OwnedWindowTests
{
    private static readonly WindowTraits Child = WindowTraits.Normal with { Dialog = true };

    [TestMethod]
    public void OwnedWindowFloatsAndStaysOutOfGroups()
    {
        var desk = new TestDesktop();
        var parent = desk.Spawn("child-test-parent");

        desk.Ok("focus", "class:child-test-parent");
        desk.Ok("float");
        desk.Ok("focus", "class:child-test-parent");
        desk.Ok("pin", "on");

        desk.Spawn("child-test-child", traits: Child, owner: parent);
        desk.Ok("focus", "class:child-test-child");
        Assert.AreEqual(2, desk.Clients().Length);
        Assert.AreEqual(2, desk.Clients().Count(TestDesktop.Pinned));

        Assert.AreEqual("ok", desk.Send("reload"));
        desk.Close("child-test-child");
        desk.Close("child-test-parent");
        Assert.IsEmpty(desk.Clients());

        desk.Spawn("a");
        desk.Ok("group");

        var second = desk.Spawn("child-test-parent");
        Assert.AreEqual(2, desk.Clients().Length);
        desk.Spawn("child-test-child", traits: Child, owner: second);
        desk.Ok("focus", "class:child-test-child");
        Assert.AreEqual(3, desk.Clients().Length);

        Assert.AreEqual(1, desk.Clients().Count(Ungrouped));
    }

    private static bool Ungrouped(JsonElement client) => client.GetProperty("grouped").GetArrayLength() == 0;
}
