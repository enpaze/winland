namespace Winland.Domain;

internal readonly record struct SizeRule(Size? Min, Size? Max)
{
    public SizeLimits Floating(Size? ownMin, Size? ownMax) =>
        new(Min ?? ownMin, Max ?? ownMax);
}
