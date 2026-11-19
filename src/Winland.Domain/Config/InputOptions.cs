namespace Winland.Domain;

public sealed record InputOptions
{
    public bool FollowMouse { get; init; } = true;

    public static InputOptions Documented { get; } = new();

    internal static OptionSet<InputOptions> Options { get; } = new(
    [
        Option<InputOptions>.Of("focus_follows_mouse", OptionValue.Bool, (o, v) => o with { FollowMouse = v }),
    ]);

    public static InputOptions From(ConfigDocument document) => Options.From(Documented, document);
}
