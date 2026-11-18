namespace Winland.Daemon.Tests;

[TestClass]
public class ConfigPathsTests
{
    [TestMethod]
    public void TheDefaultConfigIsWinlandConfInTheDotWinlandFolder()
    {
        var path = ConfigPaths.Default(@"C:\Users\me");

        Assert.AreEqual(@"C:\Users\me\.winland\winland.conf", path);
    }

    [TestMethod]
    public void ALuaFileBesideTheConfigIsNotRead()
    {
        var data = Path.Combine(Path.GetTempPath(), "winland-config-paths-" + Guid.NewGuid().ToString("N"));
        var lua = Path.Combine(data, ".winland", "winland.lua");
        Directory.CreateDirectory(Path.GetDirectoryName(lua)!);
        try
        {
            File.WriteAllText(lua, "hl.config({})\n");
            Assert.AreEqual(Path.Combine(data, ".winland", "winland.conf"), ConfigPaths.Default(data));
        }
        finally
        {
            Directory.Delete(data, recursive: true);
        }
    }

    [TestMethod]
    public void TheConfigAndEverySourceAreWatchedAtTheirFullPaths()
    {
        var watched = ConfigPaths.Watched(
            @"D:\config\winland.conf",
            ["colors.conf", @"binds\..\keys.conf", @"E:\shared\theme.conf"]);

        Assert.AreSequenceEqual(
            [@"D:\config\winland.conf", @"D:\config\colors.conf", @"D:\config\keys.conf", @"E:\shared\theme.conf"],
            watched);
    }

    [TestMethod]
    public void ARelativeConfigIsWatchedWhereItIsReadFrom()
    {
        var watched = ConfigPaths.Watched("winland.conf", ["colors.conf"]);

        Assert.AreSequenceEqual(
            [Path.GetFullPath("winland.conf"), Path.Combine(Environment.CurrentDirectory, "colors.conf")],
            watched);
    }

    [TestMethod]
    public void AConfigWithoutSourcesWatchesOnlyItself()
    {
        Assert.AreSequenceEqual([@"D:\config\winland.conf"], ConfigPaths.Watched(@"D:\config\winland.conf", []));
    }

    [TestMethod]
    public void ASourceWindowsCannotNameIsNotWatched()
    {
        Assert.AreSequenceEqual(
            [@"D:\config\winland.conf", @"D:\config\colors.conf"],
            ConfigPaths.Watched(@"D:\config\winland.conf", ["bad\0name.conf", "colors.conf"]));
    }

    [TestMethod]
    public void NoConfigOrNoSourceListIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ConfigPaths.Watched(" ", []));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfigPaths.Watched(@"D:\config\winland.conf", null!));
    }
}
