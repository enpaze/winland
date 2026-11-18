namespace Winland.Domain;

public sealed record CursorOptions
{
    public bool NoWarps { get; init; }

    public bool HideOnKeyPress { get; init; } = true;

    public static CursorOptions Documented { get; } = new();

    public CursorBehavior Behavior() => new(HideOnKeyPress);

    internal static OptionSet<CursorOptions> Options { get; } = new(
    [
        Option<CursorOptions>.Of("cursor:no_warps", OptionValue.Bool, (o, v) => o with { NoWarps = v }),
        Option<CursorOptions>.Of("cursor:hide_on_key_press", OptionValue.Bool, (o, v) => o with { HideOnKeyPress = v }),
    ]);

    public static CursorOptions From(ConfigDocument document) => Options.From(Documented, document);
}
