namespace Winland.Domain;

internal sealed class OptionSet<TOptions>
    where TOptions : class
{
    private readonly Dictionary<string, Option<TOptions>> _byName;

    public OptionSet(IEnumerable<Option<TOptions>> options) =>
        _byName = options.ToDictionary(option => option.Name, StringComparer.Ordinal);

    public IEnumerable<Option<TOptions>> All => _byName.Values;

    public TOptions From(TOptions options, ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (var setting in document.Settings)
        {
            if (_byName.TryGetValue(setting.Keyword, out var option))
                options = option.Apply(options, setting.Value) ?? options;
        }

        return options;
    }
}
