namespace Winland.Domain;

public sealed partial class BindSession
{
    private readonly Lock _gate = new();
    private readonly HashSet<string> _downKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _downButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _caughtKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _caughtButtons = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _passedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _passedButtons = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _maskedKeys = new(StringComparer.OrdinalIgnoreCase);

    public Modifier Held { get; private set; }
    private volatile bool _inhibited;
    private volatile int _buttonsHeld;
    public Point Pointer { get; private set; }
    private bool _scrollSeen;
    private long _scrollAtMs;

    public bool Inhibited => _inhibited;

    public bool ButtonsHeld => _buttonsHeld > 0;

    public void Inhibit(bool inhibited) => _inhibited = inhibited;

    public void ForgetCaught(string key)
    {
        lock (_gate)
            _caughtKeys.Remove(key);
    }
}
