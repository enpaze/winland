namespace Winland.Infrastructure.Tests;

[TestClass]
public class ShellFlyoutsTests
{
    [TestMethod]
    [DataRow("StartMenuExperienceHost.exe")]
    [DataRow("searchhost.exe")]
    [DataRow("ShellExperienceHost.exe")]
    [DataRow("TextInputHost.exe")]
    [DataRow("Widgets.exe")]
    [DataRow("LockApp.exe")]
    public void TheShellFlyoutHostsAreFlyouts(string exe) => Assert.IsTrue(ShellFlyouts.Is(exe, "Windows.UI.Core.CoreWindow"));

    [TestMethod]
    [DataRow("")]
    [DataRow("Cursor.exe")]
    [DataRow("explorer.exe")]
    public void OtherProgramsAreNot(string exe) => Assert.IsFalse(ShellFlyouts.Is(exe, "Windows.UI.Core.CoreWindow"));

    [TestMethod]
    public void ExplorersPopupsAreFlyouts()
    {
        Assert.IsTrue(ShellFlyouts.Is("explorer.exe", "Xaml_WindowedPopupClass"));
        Assert.IsTrue(ShellFlyouts.Is("explorer.exe", "TopLevelWindowForOverflowXamlIsland"));
        Assert.IsTrue(ShellFlyouts.Is("explorer.exe", "Shell_InputSwitchTopLevelWindow"));
        Assert.IsFalse(ShellFlyouts.Is("explorer.exe", "CabinetWClass"));
        Assert.IsFalse(ShellFlyouts.Is("Notepad.exe", "Xaml_WindowedPopupClass"));
    }
}
