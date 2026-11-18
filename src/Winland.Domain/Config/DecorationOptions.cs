namespace Winland.Domain;

public sealed record DecorationOptions
{
    public static DecorationOptions Documented { get; } = new()
    {
        ActiveBorder = new BorderColor(0xFF, 0xFF, 0xFF, 0xFF),
        InactiveBorder = new BorderColor(0xFF, 0x40, 0x40, 0x40),
        Rounding = 8,
    };

    public BorderColor ActiveBorder { get; init; }

    public BorderColor InactiveBorder { get; init; }

    public bool ActiveBorderSet { get; init; }

    public DecorationOptions WithAccent(BorderColor? accent) =>
        ActiveBorderSet || accent is not BorderColor color ? this : this with { ActiveBorder = color };

    public int Rounding { get; init; }

    public bool Blur { get; init; } = true;

    public float ActiveOpacity { get; init; } = 1;

    public float InactiveOpacity { get; init; } = 1;

    private static readonly OptionValue<int> Radius =
        (string text, out int radius) => OptionValue.Int()(text, out radius) && radius >= 0;

    internal static OptionSet<DecorationOptions> Options { get; } = new(
    [
        Of("general:col.active_border", OptionValue.Color, (o, v) => o with { ActiveBorder = v, ActiveBorderSet = true }),
        Of("general:col.inactive_border", OptionValue.Color, (o, v) => o with { InactiveBorder = v }),
        Of("decoration:rounding", Radius, (o, v) => o with { Rounding = v }),
        Of("decoration:blur:enabled", OptionValue.Bool, (o, v) => o with { Blur = v }),
        Of("decoration:active_opacity", OptionValue.Single(0f, 1f), (o, v) => o with { ActiveOpacity = v }),
        Of("decoration:inactive_opacity", OptionValue.Single(0f, 1f), (o, v) => o with { InactiveOpacity = v }),
    ]);

    public static DecorationOptions From(ConfigDocument document, HighContrast? contrast = null) =>
        Options.From(
            contrast is HighContrast colors ? Documented with { ActiveBorder = colors.Highlight, InactiveBorder = colors.GrayText } : Documented,
            document);

    private static Option<DecorationOptions> Of<T>(string name, OptionValue<T> value, Func<DecorationOptions, T, DecorationOptions> set) =>
        Option<DecorationOptions>.Of(name, value, set);
}
