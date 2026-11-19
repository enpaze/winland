namespace Winland.Domain;

public sealed record DebugOptions
{
    public static DebugOptions Documented { get; } = new();

    public bool LogFile { get; init; }

    internal static OptionSet<DebugOptions> Options { get; } = new(
    [
        Option<DebugOptions>.Of("log_file", OptionValue.Bool, (o, v) => o with { LogFile = v }),
    ]);

    public static DebugOptions From(ConfigDocument document) => Options.From(Documented, document);
}
