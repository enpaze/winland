namespace Winland.Domain;

public sealed record DebugOptions
{
    public static DebugOptions Documented { get; } = new();

    public bool DisableLogs { get; init; } = true;

    internal static OptionSet<DebugOptions> Options { get; } = new(
    [
        Option<DebugOptions>.Of("debug:disable_logs", OptionValue.Bool, (o, v) => o with { DisableLogs = v }),
    ]);

    public static DebugOptions From(ConfigDocument document) => Options.From(Documented, document);
}
