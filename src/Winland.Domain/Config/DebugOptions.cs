namespace Winland.Domain;

public sealed record DebugOptions
{
    public static DebugOptions Documented { get; } = new();

    public bool DisableLogs { get; init; } = true;

    internal static OptionSet<DebugOptions> Options { get; } = new(
    [
        Of("debug:disable_logs", OptionValue.Bool, o => o.DisableLogs, (o, v) => o with { DisableLogs = v }),
    ]);

    public static DebugOptions From(ConfigDocument document) => Options.From(Documented, document);

    private static Option<DebugOptions> Of<T>(string name, OptionValue<T> value, Func<DebugOptions, T> get, Func<DebugOptions, T, DebugOptions> set) =>
        Option<DebugOptions>.Of(name, value, get, set);
}
