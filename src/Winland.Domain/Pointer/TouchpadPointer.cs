namespace Winland.Domain;

public static class TouchpadPointer
{
    public const long StaleMs = 250;

    public const long CoastMs = 2000;

    public const int Notch = 120;

    public static bool Moves(int fingers, long sinceReportMs) => fingers >= 1 && sinceReportMs <= StaleMs;

    public static bool Scrolls(int fingers, long sinceReportMs, long sinceTwoFingersMs, int delta)
    {
        if (fingers >= 2 && sinceReportMs <= StaleMs)
            return true;

        return sinceTwoFingersMs <= CoastMs && delta % Notch != 0;
    }
}
