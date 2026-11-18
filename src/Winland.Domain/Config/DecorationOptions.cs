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

    public BorderColor? ChosenBorder { get; init; }

    public bool AccentBorder => ChosenBorder is null;

    public DecorationOptions WithAccent(BorderColor? accent) =>
        AccentBorder && accent is BorderColor color ? this with { ActiveBorder = color } : this;

    public int Rounding { get; init; }

    public bool Acrylic { get; init; } = true;

    public float ActiveOpacity { get; init; } = 1;

    public float InactiveOpacity { get; init; } = 1;

    private static readonly OptionValue<int> Radius =
        (string text, out int radius) => OptionValue.Int()(text, out radius) && radius >= 0;

    private static readonly OptionValue<BorderColor?> ColorOrAccent = (string text, out BorderColor? color) =>
    {
        color = null;
        if (text.Trim().Equals("accent", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!OptionValue.Color(text, out var parsed))
            return false;

        color = parsed;
        return true;
    };

    internal static OptionSet<DecorationOptions> Options { get; } = new(
    [
        Option<DecorationOptions>.Of("border_color", ColorOrAccent, (o, v) => o with { ChosenBorder = v }),
        Option<DecorationOptions>.Of("border_color_inactive", OptionValue.Color, (o, v) => o with { InactiveBorder = v }),
        Option<DecorationOptions>.Of("rounding", Radius, (o, v) => o with { Rounding = v }),
        Option<DecorationOptions>.Of("backdrop", OptionValue.OneOf("acrylic", "none"), (o, v) => o with { Acrylic = v == "acrylic" }),
        Option<DecorationOptions>.Of("active_opacity", OptionValue.Single(0f, 1f), (o, v) => o with { ActiveOpacity = v }),
        Option<DecorationOptions>.Of("inactive_opacity", OptionValue.Single(0f, 1f), (o, v) => o with { InactiveOpacity = v }),
    ]);

    public static DecorationOptions From(ConfigDocument document, HighContrast? contrast = null)
    {
        var options = Options.From(
            contrast is HighContrast colors ? Documented with { ActiveBorder = colors.Highlight, InactiveBorder = colors.GrayText } : Documented,
            document);
        return options.ChosenBorder is BorderColor chosen ? options with { ActiveBorder = chosen } : options;
    }
}
