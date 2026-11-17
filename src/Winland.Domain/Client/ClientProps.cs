namespace Winland.Domain;

internal sealed class ClientProps
{
    private readonly Dictionary<WindowId, Entry> _entries = [];

    public IEnumerable<IReadOnlyList<WindowRule>> Followed =>
        _entries.Values.Select(entry => entry.Subject?.Followed).OfType<IReadOnlyList<WindowRule>>();

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

    public void Rematched(WindowId window, WindowDecision decision)
    {
        var entry = Of(window);
        entry.RuleProps = RulePropsOf(decision);
        entry.Opacity = new OpacityChoice(decision.Opacity, decision.Opaque);
        if (entry.Focus is FocusRule focus)
            entry.Focus = focus with { NoFocus = decision.NoFocus };
    }

    public void SetSizes(WindowId window, SizeRule rule, Size? ownMin, Size? ownMax) => Of(window).Sizes = (rule, ownMin, ownMax);

    public void Forget(WindowId window) => _entries.Remove(window);

    public object? RuleProp(WindowId window, string key) =>
        _entries.TryGetValue(window, out var entry) && entry.RuleProps is { } props && props.TryGetValue(key, out var value) ? value : null;

    public T Own<T>(WindowId window, WindowProp<T> prop)
    {
        ArgumentNullException.ThrowIfNull(prop);
        return RuleProp(window, prop.Name) is T value ? value : default!;
    }

    public FocusRule? FocusRuleOf(WindowId window) => _entries.TryGetValue(window, out var entry) ? entry.Focus : null;

    public bool NoFocus(WindowId window) => FocusRuleOf(window)?.NoFocus ?? false;

    public OpacityChoice Opacity(WindowId window) =>
        _entries.TryGetValue(window, out var entry) ? entry.Opacity : default;

    public (SizeRule Rule, Size? OwnMin, Size? OwnMax)? Sizes(WindowId window) =>
        _entries.TryGetValue(window, out var entry) ? entry.Sizes : null;

    public (WindowTraits Traits, IReadOnlyList<WindowRule> Followed)? Subject(WindowId window) =>
        _entries.TryGetValue(window, out var entry) ? entry.Subject : null;

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

internal readonly record struct Admitted(Admission Reason, bool Floating, FloatReason FloatReason);

internal readonly record struct FocusRule(bool NoInitialFocus, bool NoFocus);

internal readonly record struct OpacityChoice(OpacityRule? Rule, bool Opaque);
