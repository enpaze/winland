namespace Winland.Domain;

public sealed record Frame(WindowId Window)
{
    public Rectangle? Bounds { get; init; }

    public Rectangle? Goal { get; init; }

    public Rectangle? Content { get; init; }

    public byte? Alpha { get; init; }

    public BorderColor? Border { get; init; }

    public bool Done { get; init; }

    public MotionEnd End { get; init; }

    public Rectangle Rest { get; init; }

    public byte RestAlpha { get; init; } = 255;
}
