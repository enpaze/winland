namespace Winland.Domain;

public readonly record struct BindDecision(bool Consumed, string? Dispatcher, string Arguments = "")
{
    public IReadOnlyList<BindDispatch> Dispatches { get; init; } = [];

    /// <summary>
    /// Inject a mask key now, while a Super or Alt the OS saw is still held. This chord was
    /// swallowed, so without it the shell takes the modifier's release as a lone tap and
    /// opens Start or a menu bar. The release itself always reaches the OS.
    /// </summary>
    public bool Mask { get; init; }
}

public readonly record struct BindDispatch(string Dispatcher, string Arguments)
{
    /// <summary>
    /// A mouse bind's drag, from <c>bindm</c> or the <c>mouse</c> dispatcher: it moves or resizes the window under the
    /// pointer until the button or key that started it comes up.
    /// </summary>
    public bool Drag { get; init; }
}
