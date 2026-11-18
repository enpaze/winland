namespace Winland.Infrastructure;

public sealed partial class KeyboardHook : IDisposable
{
    internal (bool Passes, BindDecision Decision) Deliver(KeyEdge edge)
    {
        Input input = edge.Up
            ? new KeyReleased(edge.Name) { ScanCode = edge.Xkb }
            : new KeyPressed(edge.Name) { ScanCode = edge.Xkb };

        var decision = _core.Decide(input, _session);
        Enqueue(decision);
        if (decision.Mask)
            Mask();

        return (!decision.Consumed, decision);
    }
}
