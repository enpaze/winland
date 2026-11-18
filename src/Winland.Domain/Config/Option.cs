namespace Winland.Domain;

internal sealed class Option<TOptions> : IOption
    where TOptions : class
{
    private readonly Func<string, bool> _reads;
    private readonly Func<TOptions, string, TOptions?> _apply;

    private Option(string name, Func<string, bool> reads, Func<TOptions, string, TOptions?> apply)
    {
        Name = name;
        _reads = reads;
        _apply = apply;
    }

    public string Name { get; }

    public static Option<TOptions> Of<T>(string name, OptionValue<T> value, Func<TOptions, T, TOptions> set)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Option<TOptions>(
            name,
            text => value(text, out _),
            (options, text) => value(text, out var read) ? set(options, read) : null);
    }

    public bool Reads(string text) => _reads(text);

    public TOptions? Apply(TOptions options, string text) => _apply(options, text);
}

internal interface IOption
{
    string Name { get; }

    bool Reads(string text);
}
