namespace Winland.Domain;

/// <summary>The switch for Winland's fixed animations.</summary>
public sealed record AnimationOptions
{
    public static AnimationOptions Documented { get; } = new();

    public bool Enabled { get; init; } = true;

    public Timing? WindowTiming => Enabled ? new(Bezier.Default, 300) : null;

    public Timing? TransitionTiming => Enabled ? new(Bezier.Default, 800) : null;

    internal static OptionSet<AnimationOptions> Options { get; } = new(
    [
        Option<AnimationOptions>.Of("animations:enabled", OptionValue.Bool, o => o.Enabled, (o, v) => o with { Enabled = v }),
    ]);

    public static AnimationOptions From(ConfigDocument document) => Options.From(Documented, document);
}

/// <summary>How long one animation runs and how it eases.</summary>
public readonly record struct Timing(Bezier Curve, double Milliseconds);
