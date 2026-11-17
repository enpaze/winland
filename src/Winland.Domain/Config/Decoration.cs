namespace Winland.Domain;

public readonly record struct Decoration(
    ClientId Client,
    BorderColor Border,
    CornerPreference Corners,
    byte Alpha = 255,
    bool Borderless = false)
{
    public CornerRegion? Region { get; init; }

    public byte Opacity { get; init; } = Alpha;

    public bool Blur { get; init; }

    public BorderRing? Ring { get; init; }
}

public enum CornerPreference
{
    DoNotRound,
    Round,
    RoundSmall,
}
