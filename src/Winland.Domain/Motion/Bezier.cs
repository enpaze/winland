namespace Winland.Domain;

public sealed record Bezier(double X1, double Y1, double X2, double Y2)
{
    public static Bezier Default { get; } = new(0, 0.75, 0.15, 1);

    public static Bezier Linear { get; } = new(0, 0, 1, 1);

    public double Ease(double x)
    {
        if (double.IsNaN(x) || x <= 0)
            return 0;
        if (x >= 1)
            return 1;

        return Component(Solve(x), Y1, Y2);
    }

    private double Solve(double x)
    {
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var error = Component(t, X1, X2) - x;
            if (Math.Abs(error) < 1e-7)
                return t;

            var slope = Slope(t, X1, X2);
            if (Math.Abs(slope) < 1e-6)
                break;

            t -= error / slope;
            if (t is < 0 or > 1)
                break;
        }

        var low = 0.0;
        var high = 1.0;
        t = x;
        for (var i = 0; i < 64; i++)
        {
            var value = Component(t, X1, X2);
            if (Math.Abs(value - x) < 1e-7)
                return t;

            if (value < x)
                low = t;
            else
                high = t;

            t = (low + high) / 2;
        }

        return t;
    }

    private static double Component(double t, double first, double second)
    {
        var inverse = 1 - t;
        return (3 * inverse * inverse * t * first) + (3 * inverse * t * t * second) + (t * t * t);
    }

    private static double Slope(double t, double first, double second)
    {
        var inverse = 1 - t;
        return (3 * inverse * inverse * first) + (6 * inverse * t * (second - first)) + (3 * t * t * (1 - second));
    }
}
