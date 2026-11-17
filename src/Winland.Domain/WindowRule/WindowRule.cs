namespace Winland.Domain;

public sealed record WindowRule(
    WindowRuleEffect Effect,
    string? Workspace,
    string? ClassPattern,
    string? TitlePattern,
    string? ExePattern,
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
