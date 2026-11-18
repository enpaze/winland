namespace Winland.Domain;

internal abstract record WindowProp(string Name)
{
    public static readonly WindowProp<BorderColor?> ActiveBorderColor = new("active_border_color");
    public static readonly WindowProp<BorderColor?> InactiveBorderColor = new("inactive_border_color");
    public static readonly WindowProp<bool?> Decorate = new("decorate");
    public static readonly WindowProp<bool?> FocusOnActivate = new("focus_on_activate");
    public static readonly WindowProp<bool?> NoAnim = new("no_anim");
    public static readonly WindowProp<bool?> NoBlur = new("no_blur");
    public static readonly WindowProp<long?> BorderSize = new("border_size");
    public static readonly WindowProp<long?> Rounding = new("rounding");
}

internal sealed record WindowProp<T>(string Name) : WindowProp(Name);
