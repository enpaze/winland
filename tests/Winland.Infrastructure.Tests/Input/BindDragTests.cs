namespace Winland.Infrastructure.Tests;

[TestClass]
public class BindDragTests
{
    [TestMethod]
    public void TheDragEndsOnlyWhenItsOwnButtonComesUp()
    {
        var drag = new BindDrag();
        drag.Begin("mouse:272", "movewindow", "");

        Assert.IsNull(drag.Release("mouse:273"));
        Assert.AreEqual(new BindDrag.Active("mouse:272", "movewindow", ""), drag.Release("mouse:272"));
        Assert.IsNull(drag.Current);
    }

    [TestMethod]
    public void ASecondDragWhileOneIsHeldIsIgnored()
    {
        var drag = new BindDrag();
        drag.Begin("mouse:272", "movewindow", "");

        Assert.IsFalse(drag.Begin("mouse:273", "resizewindow", ""));
        Assert.IsNull(drag.Release("mouse:273"));
        Assert.AreEqual(new BindDrag.Active("mouse:272", "movewindow", ""), drag.Release("mouse:272"));
    }

    [TestMethod]
    public void AnAbandonedDragIsHandedBackOnceSoItsEndCanBePosted()
    {
        var drag = new BindDrag();
        drag.Begin("mouse:273", "resizewindow", "1");

        Assert.AreEqual(new BindDrag.Active("mouse:273", "resizewindow", "1"), drag.Abandon());
        Assert.IsNull(drag.Current);
        Assert.IsNull(drag.Abandon());
        Assert.IsNull(drag.Release("mouse:273"));
    }

    [TestMethod]
    public void NoDragHasNothingToAbandon() => Assert.IsNull(new BindDrag().Abandon());
}
