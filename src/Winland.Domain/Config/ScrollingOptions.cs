using System.Globalization;

namespace Winland.Domain;

public sealed record ScrollingOptions
{
    public const double MinColumnWidth = 0.05;

    public const double MaxColumnWidth = 1;

    public double ColumnWidth { get; init; } = 0.5;

    public static IReadOnlyList<double> StepWidths { get; } = [0.333, 0.5, 0.667, 1.0];

    public static ScrollingOptions Documented { get; } = new();

    internal static OptionSet<ScrollingOptions> Options { get; } = new(
    [
        Option<ScrollingOptions>.Of("column_width", OptionValue.Number(0.1, 1), (o, v) => o with { ColumnWidth = v }),
    ]);

    public static ScrollingOptions From(ConfigDocument document) => Options.From(Documented, document);
}
