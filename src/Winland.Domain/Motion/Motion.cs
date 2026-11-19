namespace Winland.Domain;

public sealed record Motion(WindowId Window, Rectangle To)
{
    public Rectangle? From { get; init; }

    public bool Warp { get; init; }

    public Timing? Move { get; init; }

    public double? FadeFrom { get; init; }

    public double FadeTo { get; init; } = 1;

    public Timing? Fade { get; init; }

    public byte AlphaTo { get; init; } = 255;

    public byte? AlphaFrom { get; init; }

    public Timing? Alpha { get; init; }

    public BorderColor? BorderTo { get; init; }

    public BorderColor? BorderFrom { get; init; }

    public Timing? Border { get; init; }

    public MotionEnd End { get; init; }

    public Rectangle? Within { get; init; }

    public int? CloseGraceMilliseconds { get; init; }

    public Rectangle Rest { get; init; } = To;
}

public enum MotionEnd
{
    None,

    Hide,

    Close,
}
