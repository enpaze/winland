namespace Winland.Domain;

public sealed record SessionOptions
{
    public bool HideTaskbar { get; init; }

    public static SessionOptions Documented { get; } = new();

    internal static OptionSet<SessionOptions> Options { get; } = new(
    [
        Option<SessionOptions>.Of("hide_taskbar", OptionValue.Bool, (o, v) => o with { HideTaskbar = v }),
    ]);

    public static SessionOptions From(ConfigDocument document) => Options.From(Documented, document);
}
