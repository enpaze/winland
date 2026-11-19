namespace Winland.Domain;

public sealed record GroupOptions
{
    public static GroupOptions Documented { get; } = new();

    public GroupBarStyle Bar { get; init; } = GroupBarStyle.Documented;

    public BorderColor ActiveBorder { get; private init; } = new(0x66, 0xFF, 0xFF, 0x00);

    public BorderColor InactiveBorder { get; private init; } = new(0x66, 0x77, 0x77, 0x00);

    internal static OptionSet<GroupOptions> Options { get; } = new(
    [
        OfBar("tab_font", OptionValue.Text, (b, v) => b with { FontFamily = v }),
        OfBar("tab_font_size", OptionValue.Int(2, 64), (b, v) => b with { FontSize = v }),
        OfBar("tab_height", OptionValue.Int(1, 64), (b, v) => b with { Height = v }),
        OfBar("tab_text_color", OptionValue.Color, (b, v) => b with { TextColor = v }),
        OfBar("tab_color", OptionValue.Color, (b, v) => b with { Active = v }),
        OfBar("tab_color_inactive", OptionValue.Color, (b, v) => b with { Inactive = v }),
    ]);

    public static GroupOptions From(ConfigDocument document, HighContrast? contrast = null) =>
        Options.From(contrast is HighContrast colors ? InContrast(colors) : Documented, document);

    private static GroupOptions InContrast(HighContrast colors) => Documented with
    {
        ActiveBorder = colors.Highlight,
        InactiveBorder = colors.GrayText,
        Bar = Documented.Bar with { TextColor = colors.Text, Active = colors.Highlight, Inactive = colors.GrayText },
    };

    private static Option<GroupOptions> OfBar<T>(string name, OptionValue<T> value, Func<GroupBarStyle, T, GroupBarStyle> set) =>
        Option<GroupOptions>.Of(name, value, (o, v) => o with { Bar = set(o.Bar, v) });
}
