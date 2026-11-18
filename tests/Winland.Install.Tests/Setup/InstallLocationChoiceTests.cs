namespace Winland.Install.Tests;

[TestClass]
public class InstallLocationChoiceTests
{
    [TestMethod]
    public void ThePerUserCopyIsRecognizedWhateverItsCase()
    {
        var directory = @"C:\Users\me\AppData\Local\Programs\Winland";

        Assert.IsTrue(InstallLocations.IsPerUserInstall(Path.Combine(directory, "winland.exe"), directory));
        Assert.IsTrue(InstallLocations.IsPerUserInstall(Path.Combine(directory.ToLowerInvariant(), "winland.exe"), directory + @"\"));
    }

    [TestMethod]
    public void AnotherFolderIsNotThePerUserCopy()
    {
        Assert.IsFalse(InstallLocations.IsPerUserInstall(
            @"C:\Program Files\Winland\winland.exe",
            @"C:\Users\me\AppData\Local\Programs\Winland"));
        Assert.IsFalse(InstallLocations.IsPerUserInstall(null, @"C:\Users\me\AppData\Local\Programs\Winland"));
    }

    [TestMethod]
    public void TheConfigIsTheDotWinlandFolder()
    {
        Assert.AreEqual(@"C:\Users\me\.winland", InstallLocations.Config(@"C:\Users\me"));
    }

    [TestMethod]
    public void TheInstalledProgramFoldersStayWhereTheyAre()
    {
        Assert.AreEqual(@"C:\Program Files\Winland", InstallLocations.Machine(@"C:\Program Files"));
        Assert.AreEqual(
            @"C:\Users\me\AppData\Local\Programs\Winland",
            InstallLocations.User(@"C:\Users\me\AppData\Local"));
    }

    [TestMethod]
    public void TheRunningFolderSelectsTheInstall()
    {
        var machine = @"C:\Program Files\Winland";
        var user = @"C:\Users\me\AppData\Local\Programs\Winland";

        Assert.AreEqual(InstallKind.Machine, InstalledCopy.Of(Path.Combine(machine, "winland.exe"), machine, user));
        Assert.AreEqual(InstallKind.Machine, InstalledCopy.Of(Path.Combine(machine.ToLowerInvariant(), "winland.exe"), machine + @"\", user));
        Assert.AreEqual(InstallKind.User, InstalledCopy.Of(Path.Combine(user, "winland.exe"), machine, user + @"\"));
        Assert.AreEqual(InstallKind.None, InstalledCopy.Of(@"C:\Dev\winland\winland.exe", machine, user));
    }
}
