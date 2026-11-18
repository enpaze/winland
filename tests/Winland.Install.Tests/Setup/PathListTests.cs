namespace Winland.Install.Tests;

[TestClass]
public class PathListTests
{
    [TestMethod]
    public void AddAppendsTheFolderAndLeavesTheOtherEntriesUnexpanded()
    {
        var path = PathList.Add(@"%SystemRoot%\system32;C:\Tools", @"C:\Program Files\Winland");

        Assert.AreEqual(@"%SystemRoot%\system32;C:\Tools;C:\Program Files\Winland", path);
    }

    [TestMethod]
    public void AddDoesNothingWhenTheFolderIsAlreadyThere()
    {
        const string current = @"C:\Tools;C:\Program Files\Winland\;C:\Other";

        Assert.IsNull(PathList.Add(current, @"C:\Program Files\Winland"));
    }

    [TestMethod]
    public void RemoveDropsOnlyThatFolder()
    {
        var path = PathList.Remove(@"C:\Tools;C:\Program Files\Winland;C:\Other", @"C:\Program Files\Winland");

        Assert.AreEqual(@"C:\Tools;C:\Other", path);
    }

    [TestMethod]
    public void RemoveDoesNothingWhenTheFolderIsAbsent()
    {
        Assert.IsNull(PathList.Remove(@"%SystemRoot%\system32", @"C:\Program Files\Winland"));
        Assert.IsNull(PathList.Remove(null, @"C:\Program Files\Winland"));
    }
}
