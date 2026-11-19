namespace Winland.Domain;

public readonly record struct BindDecision(bool Consumed, string? Dispatcher, string Arguments = "")
{
    public IReadOnlyList<BindDispatch> Dispatches { get; init; } = [];

    public bool Mask { get; init; }
}

public readonly record struct BindDispatch(string Dispatcher, string Arguments)
{
    public bool IsDrag => Arguments.Trim().Length == 0 && (OrdinalText.Is(Dispatcher, "move") || OrdinalText.Is(Dispatcher, "resize"));

    public bool IsResizeDrag => IsDrag && OrdinalText.Is(Dispatcher, "resize");
}
