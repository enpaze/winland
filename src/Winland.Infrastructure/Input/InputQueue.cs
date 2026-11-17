namespace Winland.Infrastructure;

internal sealed class InputQueue<T>
{
    private readonly object _gate = new();
    private readonly Func<T, T, bool> _replaces;
    private readonly Func<T, T, (bool Joined, T Item)>? _join;
    private List<T> _waiting = [];
    private List<T> _taken = [];
    private bool _completed;

    internal InputQueue(Func<T, T, bool> replaces, Func<T, T, (bool Joined, T Item)>? join = null)
    {
        ArgumentNullException.ThrowIfNull(replaces);
        _replaces = replaces;
        _join = join;
    }

    internal bool Post(T item)
    {
        lock (_gate)
        {
            if (_completed)
                return false;

            if (_waiting.Count > 0 && _replaces(_waiting[^1], item))
                _waiting[^1] = item;
            else if (_waiting.Count > 0 && _join?.Invoke(_waiting[^1], item) is (true, var joined))
                _waiting[^1] = joined;
            else
                _waiting.Add(item);

            System.Threading.Monitor.Pulse(_gate);
            return true;
        }
    }

    internal void Complete()
    {
        lock (_gate)
        {
            _completed = true;
            System.Threading.Monitor.PulseAll(_gate);
        }
    }

    internal IReadOnlyList<T> Take()
    {
        lock (_gate)
        {
            while (_waiting.Count == 0 && !_completed)
                System.Threading.Monitor.Wait(_gate);

            (_waiting, _taken) = (_taken, _waiting);
            _waiting.Clear();
            return _taken;
        }
    }
}
