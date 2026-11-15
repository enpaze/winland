namespace Winland.Domain;

/// <summary>
/// What each Client's window rules give it: the rules it was adopted with and the effects they have now. Where a rule
/// gives nothing, the config's value is the caller's.
/// </summary>
internal sealed class ClientProps
{
    private readonly Dictionary<WindowId, Entry> _entries = [];

    /// <summary>The exec rules each adopted window follows, for the windows that follow any.</summary>
    public IEnumerable<IReadOnlyList<WindowRule>> Followed =>
        _entries.Values.Select(entry => entry.Subject?.Followed).OfType<IReadOnlyList<WindowRule>>();

    /// <summary>A window was adopted with <paramref name="decision"/>, from <paramref name="traits"/> and the exec rules it follows.</summary>
    public void Opened(WindowId window, WindowTraits traits, IReadOnlyList<WindowRule> followed, WindowDecision decision)
    {
        ArgumentNullException.ThrowIfNull(followed);
        var entry = Of(window);
        entry.Focus = new FocusRule(decision.NoInitialFocus, decision.NoFocus);
        entry.Opacity = new OpacityChoice(decision.Opacity, decision.Opaque);
        entry.RuleProps = RulePropsOf(decision);
        entry.Subject = (traits.WithInitialNames(), followed);
        entry.Admitted = new Admitted(decision.Admission, decision.Floating, decision.FloatReason);
    }

    /// <summary>The window's rules were matched again after its class or title changed: its property effects follow.</summary>
    public void Rematched(WindowId window, WindowDecision decision)
    {
        var entry = Of(window);
        entry.RuleProps = RulePropsOf(decision);
        entry.Opacity = new OpacityChoice(decision.Opacity, decision.Opaque);
        if (entry.Focus is FocusRule focus)
            entry.Focus = focus with { NoFocus = decision.NoFocus };
    }

    /// <summary>The size rules a window opened with and its own limits, so its limits can be worked out again.</summary>
    public void SetSizes(WindowId window, SizeRule rule, Size? ownMin, Size? ownMax) => Of(window).Sizes = (rule, ownMin, ownMax);

    public void Forget(WindowId window) => _entries.Remove(window);

    /// <summary>What the property effects of the window's rules give.</summary>
    public object? RuleProp(WindowId window, string key) =>
        _entries.TryGetValue(window, out var entry) && entry.RuleProps is { } props && props.TryGetValue(key, out var value) ? value : null;

    /// <summary>What a window rule gives, typed, which wins over the Workspace's rule and the config. Null when none does.</summary>
    public T Own<T>(WindowId window, WindowProp<T> prop)
    {
        ArgumentNullException.ThrowIfNull(prop);
        return RuleProp(window, prop.Name) is T value ? value : default!;
    }

    /// <summary>The focus effects the window's rules gave it, or null for a window that was never adopted.</summary>
    public FocusRule? FocusRuleOf(WindowId window) => _entries.TryGetValue(window, out var entry) ? entry.Focus : null;

    /// <summary>Whether a rule gives the window no_focus.</summary>
    public bool NoFocus(WindowId window) => FocusRuleOf(window)?.NoFocus ?? false;

    /// <summary>The opacity the window's rules ask for.</summary>
    public OpacityChoice Opacity(WindowId window) =>
        _entries.TryGetValue(window, out var entry) ? entry.Opacity : default;

    /// <summary>The size rules a window opened with and its own limits, or null when none are recorded.</summary>
    public (SizeRule Rule, Size? OwnMin, Size? OwnMax)? Sizes(WindowId window) =>
        _entries.TryGetValue(window, out var entry) ? entry.Sizes : null;

    /// <summary>The traits and exec rules a window was adopted with, which its rules are matched against again.</summary>
    public (WindowTraits Traits, IReadOnlyList<WindowRule> Followed)? Subject(WindowId window) =>
        _entries.TryGetValue(window, out var entry) ? entry.Subject : null;

    /// <summary>Why the window was adopted and whether it floated then, or null for a window never adopted.</summary>
    public Admitted? AdmittedOf(WindowId window) =>
        _entries.TryGetValue(window, out var entry) ? entry.Admitted : null;

    private Entry Of(WindowId window)
    {
        if (!_entries.TryGetValue(window, out var entry))
            _entries[window] = entry = new Entry();

        return entry;
    }

    private static IReadOnlyDictionary<string, object>? RulePropsOf(WindowDecision decision) =>
        decision.Props is { Count: > 0 } props ? props : null;

    private sealed class Entry
    {
        public IReadOnlyDictionary<string, object>? RuleProps { get; set; }

        public FocusRule? Focus { get; set; }

        public OpacityChoice Opacity { get; set; }

        public (SizeRule Rule, Size? OwnMin, Size? OwnMax)? Sizes { get; set; }

        public (WindowTraits Traits, IReadOnlyList<WindowRule> Followed)? Subject { get; set; }

        public Admitted? Admitted { get; set; }
    }
}

/// <summary>Why a window was adopted, and whether it floated when it was and why. Later changes leave it as it was.</summary>
internal readonly record struct Admitted(Admission Reason, bool Floating, FloatReason FloatReason);

/// <summary>The focus effects a window's rules give it.</summary>
internal readonly record struct FocusRule(bool NoInitialFocus, bool NoFocus);

/// <summary>An opacity rule and whether the window is opaque.</summary>
internal readonly record struct OpacityChoice(OpacityRule? Rule, bool Opaque);
