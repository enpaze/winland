namespace Winland.Domain;

public sealed record WindowRule(
    IReadOnlyList<RuleEffect> Effects,
    string? ClassPattern,
    string? TitlePattern,
    string? ExePattern);

public sealed record RuleEffect(
    WindowRuleEffect Kind,
    string? Workspace = null,
    bool Enabled = true,
    OpacityRule? Opacity = null,
    FullscreenRule? Fullscreen = null,
    RulePlacement? Placement = null);

public sealed record RulePlacement(RulePoint? Point = null, string? Monitor = null);

internal readonly record struct MapPlacement(
    RulePoint? Move = null,
    RulePoint? Size = null,
    bool Center = false,
    string? Monitor = null);

public readonly record struct FullscreenRule(FullscreenMode Mode);
