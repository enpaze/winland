namespace Winland.Domain;

public sealed record BorderRing(BorderColor Color, int Thickness, int Radius, byte Alpha)
{
    public double OuterRadius => Radius <= 0 ? 0 : Radius + Thickness;
}
