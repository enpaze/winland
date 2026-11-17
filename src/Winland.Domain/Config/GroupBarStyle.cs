namespace Winland.Domain;

public sealed record GroupBarStyle
{
    public static GroupBarStyle Documented { get; } = new();

    public string FontFamily { get; init; } = "";

    public int FontSize { get; init; } = 8;

    public int Height { get; init; } = 14;

    public BorderColor TextColor { get; init; } = new(0xFF, 0xFF, 0xFF, 0xFF);

    public BorderColor Active { get; init; } = new(0x66, 0xFF, 0xFF, 0x00);

    public BorderColor Inactive { get; init; } = new(0x66, 0x77, 0x77, 0x00);

    public int IndicatorHeight { get; init; } = 3;

    public int GapsOut { get; init; } = 2;

    public int GapsIn { get; init; } = 2;

    public int Rounding { get; init; } = 1;

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

    public int Reserved => (GapsOut * 2) + IndicatorHeight + Height;

    public BorderColor IndicatorColor(bool active) => active ? Active : Inactive;
}
