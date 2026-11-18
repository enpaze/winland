using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "tweaks")]
public class Tweaks(ITestOutputHelper output)
{
    private const string TouchpadKey = @"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad";

    [SystemVerifyFact]
    public void TurnsSnapAndSwipesOffAndPutsThemBack()
    {
        var install = UserInstall.Begin(output);
        var original = Read();
        SetSnap(true);
        var before = Read();
        install.Say($"before, with Snap turned on so restore must turn it back on: {before}");
        try
        {
            var off = install.Run(install.Paths.Daemon, "tweaks", "--off", "--touchpad");
            Assert.Equal(0, off.ExitCode);
            install.Until("Snap and both swipes are off", () => Read() == new Settings(false, 0, 0), 5_000);
            install.Say($"after tweaks --off --touchpad: {Read()}");
            Assert.True(File.Exists(UserInstall.SettingsRecord), "tweaks --off recorded nothing to restore.");

            var restore = install.Run(install.Paths.Daemon, "tweaks", "--restore");
            Assert.Equal(0, restore.ExitCode);
            install.Until("Snap and both swipes are back as they were", () => Read() == before, 5_000);
            install.Say($"after tweaks --restore: {Read()}");
            Assert.False(File.Exists(UserInstall.SettingsRecord), "tweaks --restore kept its record.");
            install.Say("saw: Snap and both swipes went off, then came back exactly as they were");
        }
        finally
        {
            if (File.Exists(UserInstall.SettingsRecord))
                _ = install.Run(install.Paths.Daemon, "tweaks", "--restore");

            SetSnap(original.Snap);
        }
    }

    private static unsafe void SetSnap(bool on) =>
        _ = PInvoke.SystemParametersInfo(
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWINARRANGING,
            on ? 1u : 0u,
            null,
            SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_UPDATEINIFILE | SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_SENDCHANGE);

    private static unsafe Settings Read()
    {
        int arranging = 0;
        _ = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWINARRANGING, 0, &arranging, 0);
        using var key = Registry.CurrentUser.OpenSubKey(TouchpadKey);
        return new Settings(arranging != 0, key?.GetValue("ThreeFingerSlideEnabled") as int?, key?.GetValue("FourFingerSlideEnabled") as int?);
    }

    private readonly record struct Settings(bool Snap, int? ThreeFingerSlide, int? FourFingerSlide);
}
