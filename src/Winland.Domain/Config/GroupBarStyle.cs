namespace Winland.Domain;

/// <summary>
/// How the tab bar over a Group's tile is drawn. The font, the height of the title row and the colors come from the
/// config. The gaps, the indicator and its rounding are fixed.
/// </summary>
public sealed record GroupBarStyle
{
    public static GroupBarStyle Documented { get; } = new();

    public string FontFamily { get; init; } = "";

    public int FontSize { get; init; } = 8;

    /// <summary>The height of the title row.</summary>
    public int Height { get; init; } = 14;

    public BorderColor TextColor { get; init; } = new(0xFF, 0xFF, 0xFF, 0xFF);

    /// <summary>The indicator of the focused window's tab.</summary>
    public BorderColor Active { get; init; } = new(0x66, 0xFF, 0xFF, 0x00);

    /// <summary>The indicator of every other tab.</summary>
    public BorderColor Inactive { get; init; } = new(0x66, 0x77, 0x77, 0x00);

    /// <summary>The height of the indicator under each title.</summary>
    public int IndicatorHeight { get; init; } = 3;

    /// <summary>The gap above the title row, and between the indicator and the window.</summary>
    public int GapsOut { get; init; } = 2;

    /// <summary>The gap between two tabs.</summary>
    public int GapsIn { get; init; } = 2;

    /// <summary>The corner radius of the indicators. Only the outer corners of the first and last are round.</summary>
    public int Rounding { get; init; } = 1;

    /// <summary>
    /// Multiplies the sizes, which the config gives in logical pixels, by a Monitor's scale. The font and the title row
    /// also grow by <paramref name="text"/>, the text size Windows' accessibility settings ask for.
    /// </summary>
    public GroupBarStyle ScaledBy(double scale, double text = 1)
    {
        if (scale is <= 0 or >= 1000 || !double.IsFinite(scale))
            scale = 1;
        if (text is < 1 or > 10 || !double.IsFinite(text))
            text = 1;
        if (scale == 1 && text == 1)
            return this;

        int Scale(int value) => (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);
        int ScaleText(int value) => (int)Math.Round(value * scale * text, MidpointRounding.AwayFromZero);
        return this with
        {
            FontSize = ScaleText(FontSize),
            Height = ScaleText(Height),
            IndicatorHeight = Scale(IndicatorHeight),
            GapsOut = Scale(GapsOut),
            GapsIn = Scale(GapsIn),
            Rounding = Scale(Rounding),
        };
    }

    /// <summary>The strip the bar takes from the top of the tile: the gap above, the title row, the indicator and the gap below.</summary>
    public int Reserved => (GapsOut * 2) + IndicatorHeight + Height;

    /// <summary>The indicator color of one tab.</summary>
    public BorderColor IndicatorColor(bool active) => active ? Active : Inactive;
}
