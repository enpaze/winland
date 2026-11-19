namespace Winland.Infrastructure.Tests;

[TestClass]
public class BindDragTests
{
    [TestMethod]
    public void TheDragEndsOnlyWhenItsOwnButtonComesUp()
    {
        var drag = new BindDrag();
        drag.Begin("mouse:left", "move", "");

        Assert.IsNull(drag.Release("mouse:right"));
        Assert.AreEqual(new BindDrag.Active("mouse:left", "move", ""), drag.Release("mouse:left"));
        Assert.IsNull(drag.Current);
    }

    [TestMethod]
    public void ASecondDragWhileOneIsHeldIsIgnored()
    {
        var drag = new BindDrag();
        drag.Begin("mouse:left", "move", "");

        Assert.IsFalse(drag.Begin("mouse:right", "resize", ""));
        Assert.IsNull(drag.Release("mouse:right"));
        Assert.AreEqual(new BindDrag.Active("mouse:left", "move", ""), drag.Release("mouse:left"));
    }

    [TestMethod]
    public void AnAbandonedDragIsHandedBackOnceSoItsEndCanBePosted()
    {
        var drag = new BindDrag();
        drag.Begin("mouse:right", "resize", "");

        Assert.AreEqual(new BindDrag.Active("mouse:right", "resize", ""), drag.Abandon());
        Assert.IsNull(drag.Current);
        Assert.IsNull(drag.Abandon());
        Assert.IsNull(drag.Release("mouse:right"));
    }

    [TestMethod]
    public void NoDragHasNothingToAbandon() => Assert.IsNull(new BindDrag().Abandon());
}
