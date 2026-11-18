namespace Winland.Domain;

public readonly record struct BindDecision(bool Consumed, string? Dispatcher, string Arguments = "")
{
    public IReadOnlyList<BindDispatch> Dispatches { get; init; } = [];

    public bool Mask { get; init; }
}

public readonly record struct BindDispatch(string Dispatcher, string Arguments);
