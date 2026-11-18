using System.Globalization;

namespace Winland.Domain;

public sealed record ScrollingOptions
{
    public const double MinColumnWidth = 0.05;

    public const double MaxColumnWidth = 1;

    public double ColumnWidth { get; init; } = 0.5;

    public static IReadOnlyList<double> StepWidths { get; } = [0.333, 0.5, 0.667, 1.0];

    public static ScrollingOptions Defaults { get; } = new();

    internal static OptionSet<ScrollingOptions> Options { get; } = new(
    [
        Of("scrolling:column_width", OptionValue.Number(0.1, 1), (o, v) => o with { ColumnWidth = v }),
    ]);

    public static ScrollingOptions From(ConfigDocument document) => Options.From(Defaults, document);

    private static Option<ScrollingOptions> Of<T>(string name, OptionValue<T> value, Func<ScrollingOptions, T, ScrollingOptions> set) =>
        Option<ScrollingOptions>.Of(name, value, set);
}
