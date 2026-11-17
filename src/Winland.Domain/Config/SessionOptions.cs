namespace Winland.Domain;

public sealed record SessionOptions
{
    public bool HideTaskbar { get; init; }

    public static SessionOptions Documented { get; } = new();

    internal static OptionSet<SessionOptions> Options { get; } = new(
    [
        Of("general:hide_taskbar", OptionValue.Bool, o => o.HideTaskbar, (o, v) => o with { HideTaskbar = v }),
    ]);

    public static SessionOptions From(ConfigDocument document) => Options.From(Documented, document);

    private static Option<SessionOptions> Of<T>(string name, OptionValue<T> value, Func<SessionOptions, T> get, Func<SessionOptions, T, SessionOptions> set) =>
        Option<SessionOptions>.Of(name, value, get, set);
}
