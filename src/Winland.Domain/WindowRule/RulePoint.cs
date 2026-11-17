namespace Winland.Domain;

public readonly record struct RuleLength(double Amount, bool Percent)
{
    public int Of(int span)
    {
        var value = Percent ? span * (Amount / 100d) : Amount;
        return (int)Math.Round(Math.Clamp(value, int.MinValue / 2d, int.MaxValue / 2d), MidpointRounding.AwayFromZero);
    }
}

public readonly record struct RulePoint(RuleLength X, RuleLength Y);
