namespace Winland.Daemon.Tests;

public class SchtasksArgumentsTests
{
    [Fact]
    public void CreateRegistersTheTaskXmlWithoutReplacing()
    {
        Assert.Equal(
            ["/Create", "/TN", "Winland", "/XML", @"C:\temp\winland.xml"],
            SchtasksCatalog.Arguments.Create("Winland", @"C:\temp\winland.xml"));
    }

    [Fact]
    public void UpdateForcesTheSameTaskXmlSoASecondInstallReplacesIt()
    {
        Assert.Equal(
            ["/Create", "/TN", "Winland", "/XML", @"C:\temp\winland.xml", "/F"],
            SchtasksCatalog.Arguments.Update("Winland", @"C:\temp\winland.xml"));
    }

    [Fact]
    public void DeleteRemovesTheTaskWithoutAPrompt()
    {
        Assert.Equal(["/Delete", "/TN", "Winland", "/F"], SchtasksCatalog.Arguments.Delete("Winland"));
    }

    [Fact]
    public void QueryLooksUpTheTaskByName()
    {
        Assert.Equal(["/Query", "/TN", "Winland"], SchtasksCatalog.Arguments.Query("Winland"));
    }
}
