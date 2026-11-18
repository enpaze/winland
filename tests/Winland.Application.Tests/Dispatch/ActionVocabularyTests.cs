namespace Winland.Application.Tests;

[TestClass]
public class ActionVocabularyTests
{
    [TestMethod]
    [DataRow("left", "a")]
    [DataRow("l", "a")]
    [DataRow("LEFT", "a")]
    public void FocusTakesAFullDirectionOrItsLetter(string direction, string expected)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");

        desk.Ok("focus", direction);

        Assert.AreEqual(expected, desk.ActiveClass());
    }

    [TestMethod]
    [DataRow("t")]
    [DataRow("b")]
    [DataRow("top")]
    [DataRow("bottom")]
    public void ARemovedDirectionWordIsNotADirection(string word)
    {
        var desk = new TestDesktop();
        desk.Spawn("one");
        desk.Spawn("two");

        Assert.AreEqual("error: no such window", desk.Dispatch("focus", word));
        Assert.AreEqual("error: No window to swap with in that direction", desk.Dispatch("swap", word));
        Assert.AreEqual("two", desk.ActiveClass());
    }

    [TestMethod]
    public void FocusWithNothingIsAnError()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        Assert.AreEqual("error: focus takes a direction or a window", desk.Dispatch("focus"));
    }

    [TestMethod]
    [DataRow("move")]
    [DataRow("resize")]
    public void AMoveOrResizeWithNoArgumentIsADragAndRefusedOffTheMouse(string action)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        Assert.AreEqual($"error: {action} is a drag; bind it to a mouse button", desk.Dispatch(action));

        var document = ConfigParser.Parse($"bind = Win+R, {action}\nbind = Win+mouse:left, {action}\n");
        Assert.AreEqual($"{action} is a drag; bind it to a mouse button.", Assert.ContainsSingle(document.Warnings).Message);
        Assert.AreEqual("mouse:left", Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds)).Key);
    }

    [TestMethod]
    public void MoveToAWorkspaceFollowsAndStayKeepsTheWorkspace()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");

        desk.Ok("move", "2 stay");
        Assert.AreEqual(1, desk.ActiveWorkspace().GetProperty("id").GetInt32());
        Assert.AreEqual(2, TestDesktop.WorkspaceId(desk.Client("b")));

        desk.Ok("move", "3");
        Assert.AreEqual(3, desk.ActiveWorkspace().GetProperty("id").GetInt32());
        Assert.AreEqual(3, TestDesktop.WorkspaceId(desk.Client("a")));
    }

    [TestMethod]
    [DataRow("left stay", "error: stay goes with a workspace or a monitor")]
    [DataRow("+1", "error: no such workspace")]
    [DataRow("2 silent", "error: no such workspace")]
    [DataRow("mon:nowhere", "error: monitor not found")]
    public void AMoveThatDoesNotReadIsRefused(string arguments, string error)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");

        Assert.AreEqual(error, desk.Dispatch("move", arguments));
        Assert.AreEqual(1, TestDesktop.WorkspaceId(desk.Client("b")));
    }

    [TestMethod]
    public void FloatAndPinTakeOnAndOff()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        desk.Ok("float", "off");
        Assert.IsFalse(TestDesktop.Floating(desk.ActiveWindow()));
        desk.Ok("float", "on");
        Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));
        desk.Ok("float", "on");
        Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));

        desk.Ok("pin", "on");
        Assert.IsTrue(TestDesktop.Pinned(desk.ActiveWindow()));
        desk.Ok("pin", "off");
        Assert.IsFalse(TestDesktop.Pinned(desk.ActiveWindow()));

        Assert.AreEqual("error: float takes on or off", desk.Dispatch("float", "toggle"));
        Assert.AreEqual("error: pin takes on or off", desk.Dispatch("pin", "enable"));
    }

    [TestMethod]
    [DataRow("close")]
    [DataRow("split")]
    [DataRow("group")]
    [DataRow("leave-group")]
    public void AnActionWithoutArgumentsRefusesOne(string action)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        Assert.AreEqual($"error: {action} takes no argument", desk.Dispatch(action, "now"));
        Assert.ContainsSingle(desk.Clients());
    }

    [TestMethod]
    public void SplitWorksOnlyInDwindle()
    {
        var desk = new TestDesktop("general:layout = scrolling\n");
        desk.Spawn("a");
        desk.Spawn("b");

        Assert.AreEqual("error: split works in the dwindle layout", desk.Dispatch("split"));
        Assert.AreEqual("error: no such layoutmsg for scrolling", desk.Dispatch("layoutmsg", "togglesplit"));
    }
}
