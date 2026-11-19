namespace Winland.Domain;

public sealed record LayoutOptions
{
    public EdgeInsets GapsIn { get; init; } = EdgeInsets.Zero;

    public EdgeInsets GapsOut { get; init; } = EdgeInsets.Zero;

    public int BorderSize { get; init; }

    public Func<ClientId, int?>? ClientBorder { get; init; }

    public double BorderScale { get; init; } = 1;

    internal int BorderFor(ClientId client) =>
        ClientBorder?.Invoke(client) is int own ? Scale(Math.Max(0, own), BorderScale) : BorderSize;

    public string Layout { get; init; } = "dwindle";

    public static LayoutOptions Unconfigured { get; } = new();

    public static LayoutOptions Documented { get; } = new()
    {
        GapsIn = EdgeInsets.Uniform(4),
        GapsOut = EdgeInsets.Uniform(8),
        BorderSize = 2,
    };

    internal static OptionSet<LayoutOptions> Options { get; } = new(
    [
        Option<LayoutOptions>.Of("layout", OptionValue.OneOf([.. LayoutFactory.Names]), (o, v) => o with { Layout = v }),
        Option<LayoutOptions>.Of("gaps_in", OptionValue.Gaps, (o, v) => o with { GapsIn = v }),
        Option<LayoutOptions>.Of("gaps_out", OptionValue.Gaps, (o, v) => o with { GapsOut = v }),
        Option<LayoutOptions>.Of("border_size", OptionValue.Int(0, 20), (o, v) => o with { BorderSize = v }),
    ]);

    public static LayoutOptions From(ConfigDocument document) => Options.From(Documented, document);

    internal OnScreen FloatingOnScreen(Rectangle area) => new(area, BorderSize);

    internal static OnScreen NewFloatingOnScreen(Rectangle area) => new(area, 0);

    internal LayoutOptions ScaledBy(double scale)
    {
        if (scale is <= 0 or >= 1000 || !double.IsFinite(scale) || scale == 1)
            return this;

        return this with
        {
            GapsIn = Scale(GapsIn, scale),
            GapsOut = Scale(GapsOut, scale),
            BorderSize = Scale(BorderSize, scale),
            BorderScale = scale,
        };
    }

    private static EdgeInsets Scale(EdgeInsets insets, double scale) =>
        new(Scale(insets.Top, scale), Scale(insets.Right, scale), Scale(insets.Bottom, scale), Scale(insets.Left, scale));

    private static int Scale(int value, double scale) =>
        (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);
}
