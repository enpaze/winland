namespace Winland.Domain;

/// <summary>The <c>group:groupbar:*</c> options: the font, title row and colors of a Group's tab bar.</summary>
public sealed record GroupOptions
{
    public static GroupOptions Documented { get; } = new();

    public GroupBarStyle Bar { get; init; } = GroupBarStyle.Documented;

    internal static OptionSet<GroupOptions> Options { get; } = new(
    [
        OfBar("font_family", OptionValue.Text, b => b.FontFamily, (b, v) => b with { FontFamily = v }),
        OfBar("font_size", OptionValue.Int(2, 64), b => b.FontSize, (b, v) => b with { FontSize = v }),
        OfBar("height", OptionValue.Int(1, 64), b => b.Height, (b, v) => b with { Height = v }),
        OfBar("text_color", OptionValue.Color, b => b.TextColor, (b, v) => b with { TextColor = v }),
        OfBar("col.active", OptionValue.Color, b => b.Active, (b, v) => b with { Active = v }),
        OfBar("col.inactive", OptionValue.Color, b => b.Inactive, (b, v) => b with { Inactive = v }),
    ]);

    /// <summary>The options <paramref name="document"/> sets, over the High Contrast colors while <paramref name="contrast"/> is on.</summary>
    public static GroupOptions From(ConfigDocument document, HighContrast? contrast = null) =>
        Options.From(contrast is HighContrast colors ? InContrast(colors) : Documented, document);

    private static GroupOptions InContrast(HighContrast colors) => Documented with
    {
        Bar = Documented.Bar with { TextColor = colors.Text, Active = colors.Highlight, Inactive = colors.GrayText },
    };

    // An Option of the tab bar, under group:groupbar.
    private static Option<GroupOptions> OfBar<T>(string key, OptionValue<T> value, Func<GroupBarStyle, T> get, Func<GroupBarStyle, T, GroupBarStyle> set) =>
        Option<GroupOptions>.Of("group:groupbar:" + key, value, o => get(o.Bar), (o, v) => o with { Bar = set(o.Bar, v) });
}
