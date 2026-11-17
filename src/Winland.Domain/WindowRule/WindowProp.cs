namespace Winland.Domain;

internal abstract record WindowProp(string Name, bool IsRuleEffect)
{
    public static readonly WindowProp<BorderColor?> ActiveBorderColor = new("active_border_color");
    public static readonly WindowProp<BorderColor?> InactiveBorderColor = new("inactive_border_color");
    public static readonly WindowProp<bool?> Decorate = new("decorate", IsRuleEffect: true);
    public static readonly WindowProp<bool?> FocusOnActivate = new("focus_on_activate", IsRuleEffect: true);
    public static readonly WindowProp<bool?> NoAnim = new("no_anim", IsRuleEffect: true);
    public static readonly WindowProp<bool?> NoBlur = new("no_blur", IsRuleEffect: true);
    public static readonly WindowProp<long?> BorderSize = new("border_size");
    public static readonly WindowProp<long?> Rounding = new("rounding");

    public static IReadOnlyList<WindowProp> All { get; } =
    [
        ActiveBorderColor, InactiveBorderColor, Decorate, FocusOnActivate, NoAnim, NoBlur, BorderSize, Rounding,
    ];

    public static bool IsOnOffEffect(string name) =>
        All.Any(prop => prop.IsRuleEffect && prop.Name == name);
}

internal sealed record WindowProp<T>(string Name, bool IsRuleEffect = false) : WindowProp(Name, IsRuleEffect);
