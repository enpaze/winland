namespace Winland.Infrastructure;

internal static class TrayBalloon
{
    internal readonly record struct Plan(bool ShowIcon, bool Temporary, bool ShowBalloon, bool ClearBalloon);

    internal static Plan For(bool trayShown, ConfigNotice? notice, int shownGeneration, int closedGeneration)
    {
        if (trayShown)
        {
            if (notice is null)
                return new(true, false, false, shownGeneration != 0);

            if (notice.Generation == shownGeneration || notice.Generation == closedGeneration)
                return new(true, false, false, false);

            return new(true, false, true, false);
        }

        if (notice is null || notice.Generation == closedGeneration)
            return new(false, false, false, false);

        if (notice.Generation == shownGeneration)
            return new(true, true, false, false);

        return new(true, true, true, false);
    }
}
