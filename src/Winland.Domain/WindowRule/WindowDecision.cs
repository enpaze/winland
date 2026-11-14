namespace Winland.Domain;

internal readonly record struct WindowDecision(
    bool Adopt,
    bool Floating,
    string? Workspace,
    bool NoInitialFocus = false,
    bool NoFocus = false,
    Size? MinSize = null,
    Size? MaxSize = null,
    double? ScrollingWidth = null,
    OpacityRule? Opacity = null,
    bool Opaque = false,
    FullscreenRule? FullscreenRequest = null,
    bool Pin = false,
    MapPlacement Placement = default,
    SizeRule Sizes = default,
    IReadOnlyDictionary<string, object>? Props = null,
    ExpressionPair? MinSizeExpression = null,
    ExpressionPair? MaxSizeExpression = null,
    Admission Admission = Admission.Default,
    FloatReason FloatReason = FloatReason.Default);

/// <summary>Why a window is adopted or left alone.</summary>
internal enum Admission
{
    /// <summary>Winland adopts a window like this.</summary>
    Default,

    /// <summary>A <c>float</c>, <c>tile</c> or <c>workspace</c> rule adopted a window Winland would leave alone.</summary>
    Rule,

    /// <summary>An <c>ignore</c> rule left it alone.</summary>
    Ignored,

    // Windows' own facts, which no rule overrides.
    Hidden,
    Child,
    UntitledTool,
    Tooltip,
    Cloaked,
    Overlay,

    // Left alone unless a rule adopts it.
    Untitled,
    Tool,
    Shell,
    ExclusiveFullscreen,
}

/// <summary>Why an adopted window floats or tiles.</summary>
internal enum FloatReason
{
    /// <summary>A window like this tiles.</summary>
    Default,

    /// <summary>A <c>float</c> or <c>tile</c> rule.</summary>
    Rule,
    Topmost,
    Dialog,
    FixedSize,

    /// <summary>Winland gave it back the state it had before Winland restarted.</summary>
    Restored,
}
