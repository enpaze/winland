namespace Winland.Domain.Tests;

[TestClass]
public class WindowGroupTests
{
    [TestMethod]
    [DataRow(0, 0, 0, 2)]
    [DataRow(1, 1, 0, 1)]
    [DataRow(2, 2, 1, 2)]
    [DataRow(2, 0, 1, 3)]
    [DataRow(0, 2, 0, 1)]
    public void RemovingAMemberKeepsTheShownMemberOrChoosesItsPredecessor(int current, int removed, int expected, int shown)
    {
        var group = new WindowGroup(new WindowId(1));
        group.Insert(1, new WindowId(2));
        group.Insert(2, new WindowId(3));
        group.ShowAt(current);

        group.RemoveAt(removed);

        Assert.AreEqual(new WindowId(shown), group.Shown);
        Assert.AreEqual(expected, group.Current);
    }
}
