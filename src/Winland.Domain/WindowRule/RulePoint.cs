namespace Winland.Domain;

/// <summary>One side of a <c>move</c> or <c>size</c> rule: a number of pixels, or a percentage of the work area.</summary>
public readonly record struct RuleLength(double Amount, bool Percent)
{
    /// <summary>Pixels, or <paramref name="span"/> times the percentage.</summary>
    public int Of(int span)
    {
        var value = Percent ? span * (Amount / 100d) : Amount;
        return (int)Math.Round(Math.Clamp(value, int.MinValue / 2d, int.MaxValue / 2d), MidpointRounding.AwayFromZero);
    }
}

/// <summary>The two lengths of a <c>move</c> or <c>size</c> rule, x then y.</summary>
public readonly record struct RulePoint(RuleLength X, RuleLength Y);
