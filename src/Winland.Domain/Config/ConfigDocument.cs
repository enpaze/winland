namespace Winland.Domain;

public sealed record ConfigDocument(
    IReadOnlyList<Setting> Settings,
    IReadOnlyList<BindDirective> Binds,
    IReadOnlyList<ConfigWarning> Warnings)
{
    public static ConfigDocument Empty { get; } = new([], [], []);

    public IReadOnlyList<WindowRule> WindowRules { get; init; } = [];


    public IReadOnlyList<string> Sources { get; init; } = [];

    public IReadOnlyList<TrackpadGesture> Gestures { get; init; } = [];

    public string? Path { get; init; }

    public KeyLayout KeyLayout { get; init; } = KeyLayout.UnitedStates;

}

public sealed record Setting(string Keyword, string Value);

public sealed record ConfigWarning(int Line, string Message, string? Source = null);

public sealed record ConfigRead(ConfigDocument? Document, ConfigWarning? Failure = null);
