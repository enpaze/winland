namespace Winland.Infrastructure.Tests;

public class ShellFlyoutsTests
{
    [Theory]
    [InlineData("StartMenuExperienceHost.exe")]
    [InlineData("searchhost.exe")]
    [InlineData("ShellExperienceHost.exe")]
    [InlineData("TextInputHost.exe")]
    [InlineData("Widgets.exe")]
    [InlineData("LockApp.exe")]
    public void TheShellFlyoutHostsAreFlyouts(string exe) => Assert.True(ShellFlyouts.Is(exe, "Windows.UI.Core.CoreWindow"));

    [Theory]
    [InlineData("")]
    [InlineData("Cursor.exe")]
    [InlineData("explorer.exe")]
    public void OtherProgramsAreNot(string exe) => Assert.False(ShellFlyouts.Is(exe, "Windows.UI.Core.CoreWindow"));

    [Fact]
    public void ExplorersPopupsAreFlyouts()
    {
        Assert.True(ShellFlyouts.Is("explorer.exe", "Xaml_WindowedPopupClass"));
        Assert.True(ShellFlyouts.Is("explorer.exe", "TopLevelWindowForOverflowXamlIsland"));
        Assert.True(ShellFlyouts.Is("explorer.exe", "Shell_InputSwitchTopLevelWindow"));
        Assert.False(ShellFlyouts.Is("explorer.exe", "CabinetWClass"));
        Assert.False(ShellFlyouts.Is("Notepad.exe", "Xaml_WindowedPopupClass"));
    }
}
