namespace Winland.Daemon.Tests;

[TestClass]
public class SchtasksArgumentsTests
{
    [TestMethod]
    public void CreateRegistersTheTaskXmlWithoutReplacing()
    {
        Assert.AreSequenceEqual(
            ["/Create", "/TN", "Winland", "/XML", @"C:\temp\winland.xml"],
            SchtasksCatalog.Arguments.Create("Winland", @"C:\temp\winland.xml"));
    }

    [TestMethod]
    public void UpdateForcesTheSameTaskXmlSoASecondInstallReplacesIt()
    {
        Assert.AreSequenceEqual(
            ["/Create", "/TN", "Winland", "/XML", @"C:\temp\winland.xml", "/F"],
            SchtasksCatalog.Arguments.Update("Winland", @"C:\temp\winland.xml"));
    }

    [TestMethod]
    public void DeleteRemovesTheTaskWithoutAPrompt()
    {
        Assert.AreSequenceEqual(["/Delete", "/TN", "Winland", "/F"], SchtasksCatalog.Arguments.Delete("Winland"));
    }

    [TestMethod]
    public void QueryLooksUpTheTaskByName()
    {
        Assert.AreSequenceEqual(["/Query", "/TN", "Winland"], SchtasksCatalog.Arguments.Query("Winland"));
    }
}
