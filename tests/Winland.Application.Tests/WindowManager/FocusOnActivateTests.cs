namespace Winland.Application.Tests;

[TestClass]
public class FocusOnActivateTests
{
    private const string Source = "xdg-activation-source";
    private const string Target = "xdg-activation-target";

    [TestMethod]
    public void ActivationIsHonoredOnlyAfterUserInput()
    {
        var desk = new TestDesktop("misc:focus_on_activate = true\ninput:follow_mouse = 0\n");
        desk.Spawn(Source);
        var target = desk.Spawn(Target);
        Assert.AreEqual(2, desk.Clients().Length);
        FocusSource(desk);

        desk.Handle(new WindowFocused(target, FocusCause.Activated));
        Assert.AreEqual(Source, desk.ActiveClass());
        Assert.AreEqual(2, desk.Clients().Length);

        FocusSource(desk);
        MoveCursorToSource(desk);
        desk.Click();
        Assert.AreEqual(Source, desk.ActiveClass());

        desk.Handle(new WindowFocused(target, FocusCause.User));
        Assert.AreEqual(Target, desk.ActiveClass());

        FocusSource(desk);
        desk.Handle(new WindowFocused(target, FocusCause.Activated));
        Assert.AreEqual(Source, desk.ActiveClass());
        Assert.AreEqual(2, desk.Clients().Length);
    }

    private static void FocusSource(TestDesktop desk)
    {
        desk.Ok("focuswindow", $"class:^{Source}$");
        Assert.AreEqual(Source, desk.ActiveClass());
    }

    private static void MoveCursorToSource(TestDesktop desk)
    {
        var bounds = desk.Placed(desk.ActiveId());
        desk.MovePointer(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }
}
