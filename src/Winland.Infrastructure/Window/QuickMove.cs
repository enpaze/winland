namespace Winland.Infrastructure;

internal sealed class QuickMove
{
    internal const int MaxSkips = 3;

    internal const long Patience = 500;

    private const int Remembered = 8;

    private readonly Lock _gate = new();
    private readonly Dictionary<WindowId, Pending> _pending = [];
    private readonly Dictionary<WindowId, Queue<Placed>> _recent = [];
    private readonly Func<long> _clock;

    internal QuickMove(Func<long>? clock = null) => _clock = clock ?? (() => Environment.TickCount64);

    internal readonly record struct Decision(bool Send, bool KeepSize);

    internal static Decision Decide(Rectangle target, Rectangle current, Rectangle? requested, int skipped)
    {
        if (requested is { } last && !SamePlace(current, last) && skipped < MaxSkips)
            return new Decision(false, false);

        if (target == current)
            return new Decision(false, false);

        return new Decision(true, target.Width == current.Width && target.Height == current.Height);
    }

    internal bool Request(WindowId window, Rectangle target, Rectangle current, Action<Rectangle, bool> send)
    {
        ArgumentNullException.ThrowIfNull(send);
        Decision decision;
        lock (_gate)
        {
            var pending = Waiting(window);
            decision = Decide(target, current, pending?.Requested, pending?.Skipped ?? 0);
            if (!decision.Send)
            {
                if (pending is { } waiting && target != current)
                    _pending[window] = waiting with { Skipped = waiting.Skipped + 1 };
                return false;
            }

            _pending[window] = new Pending(target, 0, _clock());
        }

        send(target, decision.KeepSize);
        return true;
    }

    internal void Note(WindowId window, Rectangle placed, bool waiting)
    {
        lock (_gate)
        {
            var now = _clock();
            if (waiting)
                _pending[window] = new Pending(placed, 0, now);
            else
                _pending.Remove(window);

            if (!_recent.TryGetValue(window, out var recent))
                _recent[window] = recent = new Queue<Placed>(Remembered);
            if (recent.Count == Remembered)
                recent.Dequeue();
            recent.Enqueue(new Placed(placed.X, placed.Y, now));
        }
    }

    internal bool Trails(WindowId window, Rectangle current)
    {
        lock (_gate)
            return Waiting(window) is { } pending && !SamePlace(current, pending.Requested);
    }

    internal bool Ours(WindowId window, Rectangle current)
    {
        lock (_gate)
        {
            if (!_recent.TryGetValue(window, out var recent))
                return false;

            var now = _clock();
            foreach (var placed in recent)
            {
                if (now - placed.Since <= Patience && placed.X == current.X && placed.Y == current.Y)
                    return true;
            }

            return false;
        }
    }

    internal void Forget(WindowId window)
    {
        lock (_gate)
        {
            _pending.Remove(window);
            _recent.Remove(window);
        }
    }

    private Pending? Waiting(WindowId window)
    {
        if (!_pending.TryGetValue(window, out var pending))
            return null;

        if (_clock() - pending.Since <= Patience)
            return pending;

        _pending.Remove(window);
        return null;
    }

    private static bool SamePlace(Rectangle a, Rectangle b) => a == b;

    private readonly record struct Pending(Rectangle Requested, int Skipped, long Since);

    private readonly record struct Placed(int X, int Y, long Since);
}
