namespace Winland.Domain;

public sealed record LayoutOptions
{
    public EdgeInsets GapsIn { get; init; } = EdgeInsets.Zero;

    public EdgeInsets GapsOut { get; init; } = EdgeInsets.Zero;

    public int BorderSize { get; init; }

    /// <summary>
    /// The border a Client's window properties give it, in logical pixels, or null to use <see cref="BorderSize"/>.
    /// The core sets one lookup for every Workspace, so options still compare equal.
    /// </summary>
    public Func<ClientId, int?>? ClientBorder { get; init; }

    /// <summary>The Monitor scale <see cref="ScaledBy"/> applied, which a Client's own border is multiplied by too.</summary>
    public double BorderScale { get; init; } = 1;

    /// <summary>The border around this Client, in pixels.</summary>
    internal int BorderFor(ClientId client) =>
        ClientBorder?.Invoke(client) is int own ? Scale(Math.Max(0, own), BorderScale) : BorderSize;

    public bool ResizeOnBorder { get; init; }

    /// <summary>How far past a window's border the pointer still grabs it, in pixels: 15 logical pixels.</summary>
    internal int GrabArea => Scale(15, BorderScale);

    public bool PreserveSplit { get; init; }

    /// <summary><c>general:layout</c>: the Layout a Workspace uses when no workspace rule names one.</summary>
    public string Layout { get; init; } = "dwindle";

    /// <summary>Before the config is read: no gaps and no border.</summary>
    public static LayoutOptions Unconfigured { get; } = new();

    /// <summary>The options a config that sets none of them gives.</summary>
    public static LayoutOptions Documented { get; } = new()
    {
        GapsIn = EdgeInsets.Uniform(4),
        GapsOut = EdgeInsets.Uniform(8),
        BorderSize = 2,
    };

    internal static OptionSet<LayoutOptions> Options { get; } = new(
    [
        Of("general:layout", OptionValue.OneOf([.. LayoutFactory.Names]), o => o.Layout, (o, v) => o with { Layout = v }),
        Of("general:gaps_in", OptionValue.Gaps, o => o.GapsIn, (o, v) => o with { GapsIn = v }),
        Of("general:gaps_out", OptionValue.Gaps, o => o.GapsOut, (o, v) => o with { GapsOut = v }),
        Of("general:border_size", OptionValue.Int(0, 20), o => o.BorderSize, (o, v) => o with { BorderSize = v }),
        Of("general:resize_on_border", OptionValue.Bool, o => o.ResizeOnBorder, (o, v) => o with { ResizeOnBorder = v }),
        Of("dwindle:preserve_split", OptionValue.Bool, o => o.PreserveSplit, (o, v) => o with { PreserveSplit = v }),
    ]);

    public static LayoutOptions From(ConfigDocument document) => Options.From(Documented, document);

    /// <summary>How a floating Client on this area is kept on screen when it moves, resizes or starts floating.</summary>
    internal OnScreen FloatingOnScreen(Rectangle area) => new(area, BorderSize);

    /// <summary>
    /// How a Client that opens floating on this area is kept on screen. Its border is left out, as in the pinned
    /// release, which keeps it on screen before the border is known.
    /// </summary>
    internal static OnScreen NewFloatingOnScreen(Rectangle area) => new(area, 0);

    /// <summary>Multiplies the gaps and borders, which the config gives in logical pixels, by a Monitor's scale.</summary>
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

    private static Option<LayoutOptions> Of<T>(string name, OptionValue<T> value, Func<LayoutOptions, T> get, Func<LayoutOptions, T, LayoutOptions> set) =>
        Option<LayoutOptions>.Of(name, value, get, set);
}
