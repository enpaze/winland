namespace Winland.Infrastructure.Tests;

[TestClass]
public class MouseHookTests
{
    private const uint XDown = 0x020B;
    private const uint XUp = 0x020C;

    [TestMethod]
    [DataRow(0x0201u, "mouse:left", false)]
    [DataRow(0x0202u, "mouse:left", true)]
    [DataRow(0x0204u, "mouse:right", false)]
    [DataRow(0x0205u, "mouse:right", true)]
    [DataRow(0x0207u, "mouse:middle", false)]
    [DataRow(0x0208u, "mouse:middle", true)]
    public void MainButtonsAreNamedByMessage(uint message, string expected, bool expectedUp)
    {
        Assert.IsTrue(MouseHook.TryButton(message, 0, out var button, out var up));
        Assert.AreEqual(expected, button);
        Assert.AreEqual(expectedUp, up);
    }

    [TestMethod]
    [DataRow(XDown, 0x0001_0000u, "mouse:x1", false)]
    [DataRow(XUp, 0x0001_0000u, "mouse:x1", true)]
    [DataRow(XDown, 0x0002_0000u, "mouse:x2", false)]
    [DataRow(XUp, 0x0002_0000u, "mouse:x2", true)]
    [DataRow(XDown, 0x0001_0002u, "mouse:x1", false)]
    [DataRow(XDown, 0x0002_0001u, "mouse:x2", false)]
    public void SideButtonsAreReadFromTheHighWord(uint message, uint mouseData, string expected, bool expectedUp)
    {
        Assert.IsTrue(MouseHook.TryButton(message, mouseData, out var button, out var up));
        Assert.AreEqual(expected, button);
        Assert.AreEqual(expectedUp, up);
    }

    [TestMethod]
    [DataRow(0x0200u)]
    [DataRow(0x020Au)]
    [DataRow(0x020Eu)]
    public void OtherMessagesAreNotButtons(uint message) =>
        Assert.IsFalse(MouseHook.TryButton(message, 0x0001_0000u, out _, out _));

    [TestMethod]
    [DataRow(0x020Au, 120, "mouse_up")]
    [DataRow(0x020Au, -120, "mouse_down")]
    [DataRow(0x020Eu, 120, "mouse_right")]
    [DataRow(0x020Eu, -120, "mouse_left")]
    [DataRow(0x020Au, 1, "mouse_up")]
    [DataRow(0x020Eu, -1, "mouse_left")]
    public void WheelDirectionIsTheSignOfTheHighWord(uint message, int delta, string expected)
    {
        var mouseData = (uint)(ushort)(short)delta << 16;

        Assert.IsTrue(MouseHook.TryWheel(message, mouseData, out var key));
        Assert.AreEqual(expected, key);
    }

    [TestMethod]
    public void AStillWheelOrAnotherMessageIsNotATurn()
    {
        Assert.IsFalse(MouseHook.TryWheel(0x020A, 0x0000_FFFF, out var still));
        Assert.IsFalse(MouseHook.TryWheel(0x0201, 120u << 16, out var other));
        Assert.IsFalse(MouseHook.TryButton(0x0200, 0, out var moved, out _));

        Assert.AreEqual("", still);
        Assert.AreEqual("", other);
        Assert.AreEqual("", moved);
    }

    [TestMethod]
    [DataRow("movewindow", "", true)]
    [DataRow("MoveWindow", "", true)]
    [DataRow("movewindow", "l", false)]
    [DataRow("resizewindow", "", true)]
    [DataRow("ResizeWindow", "1", true)]
    [DataRow("killactive", "", false)]
    public void OnlyAWindowDragIsADrag(string dispatcher, string arguments, bool drag)
    {
        Assert.AreEqual(drag, MouseHook.IsDrag(dispatcher, arguments));
    }

    [TestMethod]
    public void ASingleDragDispatcherIsTheDrag()
    {
        Assert.IsTrue(MouseHook.TryDrag(new BindDecision(true, "resizewindow", "1"), out var dispatcher, out var arguments));
        Assert.AreEqual(("resizewindow", "1"), (dispatcher, arguments));

        Assert.IsFalse(MouseHook.TryDrag(new BindDecision(true, "killactive"), out dispatcher, out arguments));
        Assert.AreEqual(("", ""), (dispatcher, arguments));
        Assert.IsFalse(MouseHook.TryDrag(new BindDecision(false, null), out dispatcher, out arguments));
        Assert.AreEqual(("", ""), (dispatcher, arguments));
        Assert.IsFalse(MouseHook.TryDrag(new BindDecision(false, ""), out _, out _));
    }

    [TestMethod]
    public void APressIsOnlyADragWhenEveryDispatcherDrags()
    {
        Assert.IsTrue(MouseHook.OnlyDrag(new BindDecision(true, "movewindow")));
        Assert.IsTrue(MouseHook.OnlyDrag(new BindDecision(true, "movewindow") { Dispatches = [new("movewindow", ""), new("", "")] }));
        Assert.IsFalse(MouseHook.OnlyDrag(new BindDecision(true, "movewindow") { Dispatches = [new("movewindow", ""), new("killactive", "")] }));
        Assert.IsFalse(MouseHook.OnlyDrag(new BindDecision(true, "killactive")));
    }

    [TestMethod]
    public void TheFirstDragAmongSeveralDispatchersIsTheDrag()
    {
        var several = new BindDecision(true, "killactive")
        {
            Dispatches = [new("killactive", ""), new("movewindow", "r"), new("movewindow", ""), new("resizewindow", "")],
        };
        var none = new BindDecision(true, "killactive")
        {
            Dispatches = [new("killactive", ""), new("movewindow", "r")],
        };

        Assert.IsTrue(MouseHook.TryDrag(several, out var dispatcher, out var arguments));
        Assert.AreEqual(("movewindow", ""), (dispatcher, arguments));
        Assert.IsFalse(MouseHook.TryDrag(none, out dispatcher, out arguments));
        Assert.AreEqual(("", ""), (dispatcher, arguments));
    }
}
