namespace Winland.Domain;

public sealed record ConfigDocument(
    IReadOnlyList<Setting> Settings,
    IReadOnlyList<BindDirective> Binds,
    IReadOnlyList<ConfigWarning> Warnings)
{
    /// <summary>A document with no lines, as when no config was read.</summary>
    public static ConfigDocument Empty { get; } = new([], [], []);

    public IReadOnlyList<WindowRule> WindowRules { get; init; } = [];

    public IReadOnlyList<WorkspaceRule> WorkspaceRules { get; init; } = WorkspaceRule.None;

    public IReadOnlyList<string> Sources { get; init; } = [];

    public IReadOnlyList<TrackpadGesture> Gestures { get; init; } = [];

    /// <summary>The path the config was read from, when it was read from a file. A sourced warning names its own file.</summary>
    public string? Path { get; init; }

    /// <summary>The keyboard layout the bind names were resolved in.</summary>
    public KeyLayout KeyLayout { get; init; } = KeyLayout.UnitedStates;

}

public sealed record Setting(string Keyword, string Value);

public sealed record ConfigWarning(int Line, string Message, string? Source = null);

public sealed record ConfigRead(ConfigDocument? Document, ConfigWarning? Failure = null);
