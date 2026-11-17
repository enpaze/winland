namespace Winland.Infrastructure;

internal static class AccentNotices
{
    internal const uint SettingChange = 0x001A;
    internal const uint SystemColorChange = 0x0015;
    internal const uint ColorizationChanged = 0x0320;

    internal static bool Changed(uint message) =>
        message is ColorizationChanged or SystemColorChange or SettingChange;
}
