namespace Winland.Infrastructure.Tests;

[TestClass]
public class PointerSourceTests
{
    [TestMethod]
    [DataRow(0u, 0ul, PointerSource.Mouse)]
    [DataRow(1u, 0ul, PointerSource.Injected)]
    [DataRow(1u, 0xFF515780ul, PointerSource.Touch)]
    [DataRow(1u, 0xFF515700ul, PointerSource.Pen)]
    [DataRow(0u, 0xFF515701ul, PointerSource.Pen)]
    [DataRow(0u, 0xFF515600ul, PointerSource.Mouse)]
    public void TheSignatureTellsTouchAndPensApart(uint flags, ulong extra, PointerSource expected) =>
        Assert.AreEqual(expected, PointerSources.Of(flags, extra));
}

public partial class CursorIdleTests
{
    [TestMethod]
    public void TouchHidesTheCursorUntilTheMouseMoves()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(false));

        idle.Touched();
        Assert.IsTrue(cursor.Hidden);

        idle.Moved();
        Assert.IsFalse(cursor.Hidden);
    }
}
