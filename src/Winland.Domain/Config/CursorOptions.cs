namespace Winland.Domain;

public sealed record CursorOptions
{
    public bool WarpCursor { get; init; } = true;

    public bool HideOnKeyPress { get; init; } = true;

    public static CursorOptions Documented { get; } = new();

    public CursorBehavior Behavior() => new(HideOnKeyPress);

    internal static OptionSet<CursorOptions> Options { get; } = new(
    [
        Option<CursorOptions>.Of("warp_cursor", OptionValue.Bool, (o, v) => o with { WarpCursor = v }),
        Option<CursorOptions>.Of("hide_cursor_while_typing", OptionValue.Bool, (o, v) => o with { HideOnKeyPress = v }),
    ]);

    public static CursorOptions From(ConfigDocument document) => Options.From(Documented, document);
}
