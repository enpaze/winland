namespace Winland.Infrastructure;

internal static class SessionHalt
{
    internal static Action? Stop { get; set; }

    internal static void Request() => Stop?.Invoke();
}
