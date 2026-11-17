namespace Winland.Infrastructure;

internal static class TrayMessages
{
    internal const uint Callback = 0x8000 + 0x71;

    internal static uint[] Allowed(uint taskbarCreated) =>
        taskbarCreated == 0 ? [Callback] : [Callback, taskbarCreated];
}
