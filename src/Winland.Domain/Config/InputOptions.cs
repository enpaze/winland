namespace Winland.Domain;

public sealed record InputOptions
{
    public bool FollowMouse { get; init; } = true;

    public static InputOptions Documented { get; } = new();

    internal static OptionSet<InputOptions> Options { get; } = new(
    [
        Of("input:follow_mouse", OptionValue.Bool, o => o.FollowMouse, (o, v) => o with { FollowMouse = v }),
    ]);

    public static InputOptions From(ConfigDocument document) => Options.From(Documented, document);

    private static Option<InputOptions> Of<T>(string name, OptionValue<T> value, Func<InputOptions, T> get, Func<InputOptions, T, InputOptions> set) =>
        Option<InputOptions>.Of(name, value, get, set);
}
