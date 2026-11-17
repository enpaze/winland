namespace Winland.Domain;

public static class MonitorPixels
{
    public static int Logical(int physical, double scale) =>
        Round(physical / Factor(scale));

    public static int Physical(int logical, double scale) =>
        Round(logical * Factor(scale));

    private static double Factor(double scale) =>
        scale is > 0 and < 1000 && double.IsFinite(scale) ? scale : 1;

    private static int Round(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded >= int.MaxValue)
            return int.MaxValue;
        if (rounded <= int.MinValue)
            return int.MinValue;

        return (int)rounded;
    }
}
