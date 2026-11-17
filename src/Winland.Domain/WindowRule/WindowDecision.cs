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
    Admission Admission = Admission.Default,
    FloatReason FloatReason = FloatReason.Default);

internal enum Admission
{
    Default,

    Rule,

    Ignored,

    Hidden,
    Child,
    UntitledTool,
    Tooltip,
    Cloaked,
    Overlay,

    Untitled,
    Tool,
    Shell,
    ExclusiveFullscreen,
}

internal enum FloatReason
{
    Default,

    Rule,
    Topmost,
    Dialog,
    FixedSize,

    Restored,
}
