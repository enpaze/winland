namespace Winland.Domain;

public sealed record Monitor(
    string Name,
    string Description,
    int X,
    int Y,
    int Width,
    int Height,
    double Scale,
    bool Enabled,
    Rectangle WorkArea,
    string Make = "",
    string Model = "",
    string Serial = "",
    double RefreshRate = 0)
{
    public int BitsPerChannel { get; init; }

    public bool HdrCapable { get; init; }

    public bool WideCapable { get; init; }

    public bool AdvancedCapable { get; init; }

    public bool SeparateColor { get; init; }

    public bool Chroma { get; init; }

    public bool HdrActive { get; init; }

    public bool WideActive { get; init; }

    public float MinLuminance { get; init; } = -1;

    public float MaxLuminance { get; init; } = -1;

    public float MaxAvgLuminance { get; init; } = -1;
}

public readonly record struct MonitorId(int Value)
{
    public static MonitorId Primary { get; } = new(0);
}
