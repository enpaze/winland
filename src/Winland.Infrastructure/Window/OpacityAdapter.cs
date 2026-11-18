using System.Globalization;

namespace Winland.Infrastructure;

internal readonly record struct WindowChrome(uint ExStyle, bool ColorKey, byte Alpha);

internal interface IWindowStyles
{
    bool TryRead(WindowId window, out WindowChrome chrome);

    bool TryWriteExStyle(WindowId window, uint exStyle);

    bool TrySetAlpha(WindowId window, byte alpha);
}

internal sealed class OpacityAdapter
{
    internal const uint LayeredStyle = 0x00080000;

    private readonly IWindowStyles _styles;
    private readonly Func<WindowId, bool> _hung;
    private readonly Dictionary<WindowId, Original> _owned = [];
    private readonly HashSet<WindowId> _failed = [];
    private readonly HashSet<WindowId> _leftAlone = [];
    private readonly Dictionary<WindowId, byte> _alphas = [];
    private readonly Dictionary<WindowId, byte> _veiled = [];
    private readonly Lock _gate = new();
    private bool _closed;

    internal OpacityAdapter(IWindowStyles styles, Func<WindowId, bool>? hung = null)
    {
        ArgumentNullException.ThrowIfNull(styles);
        _styles = styles;
        _hung = hung ?? (static _ => false);
    }

    internal bool Owns(WindowId window)
    {
        lock (_gate)
            return _owned.ContainsKey(window);
    }

    internal bool Failed(WindowId window)
    {
        lock (_gate)
            return _failed.Contains(window);
    }

    internal void Apply(WindowId window, byte alpha)
    {
        lock (_gate)
        {
            if (_veiled.ContainsKey(window))
            {
                _veiled[window] = alpha;
                return;
            }

            Set(window, alpha);
        }
    }

    internal byte Wanted(WindowId window)
    {
        lock (_gate)
            return _veiled.TryGetValue(window, out var alpha) ? alpha : _alphas.GetValueOrDefault(window, (byte)255);
    }

    internal bool Veil(WindowId window)
    {
        lock (_gate)
        {
            if (_veiled.ContainsKey(window))
                return true;

            var alpha = _alphas.GetValueOrDefault(window, (byte)255);
            if (_closed || !Set(window, 0) || !_owned.ContainsKey(window))
            {
                Set(window, alpha);
                return false;
            }

            _veiled[window] = alpha;
            return true;
        }
    }

    internal void Unveil(WindowId window)
    {
        lock (_gate)
        {
            if (_veiled.Remove(window, out var alpha))
                Set(window, alpha);
        }
    }

    private bool Set(WindowId window, byte alpha)
    {
        _alphas[window] = alpha;
        if (_failed.Contains(window) || _leftAlone.Contains(window))
            return false;

        if (_owned.TryGetValue(window, out var owned))
        {
            if (alpha == 255 && _hung(window))
                return _styles.TrySetAlpha(window, alpha);

            if (alpha == 255)
            {
                Restore(window, owned);
                _owned.Remove(window);
                return true;
            }

            if (_styles.TrySetAlpha(window, alpha))
                return true;

            Fail(window);
            return false;
        }

        if (_closed)
            return alpha == 255;

        if (!_styles.TryRead(window, out var chrome))
        {
            Fail(window);
            return false;
        }

        if ((chrome.ExStyle & LayeredStyle) != 0)
        {
            _leftAlone.Add(window);
            Report(window, "was already layered and was left alone.");
            return false;
        }

        if (alpha == 255)
            return true;

        if (_hung(window))
            return false;

        var updated = chrome.ExStyle | LayeredStyle;
        _owned[window] = new Original(chrome.ExStyle, chrome.Alpha);
        if (!_styles.TryWriteExStyle(window, updated))
        {
            _owned.Remove(window);
            Fail(window);
            return false;
        }

        if (_styles.TrySetAlpha(window, alpha))
            return true;

        if (_styles.TryWriteExStyle(window, chrome.ExStyle))
            _owned.Remove(window);

        Fail(window);
        return false;
    }

    internal void Reset(WindowId window)
    {
        lock (_gate)
        {
            if (_owned.TryGetValue(window, out var original) && _hung(window))
            {
                _styles.TrySetAlpha(window, 255);
                Report(window, "is not answering. It stays layered until the next start.");
            }
            else if (_owned.Remove(window))
            {
                Restore(window, original);
            }

            _failed.Remove(window);
            _leftAlone.Remove(window);
            _alphas.Remove(window);
            _veiled.Remove(window);
        }
    }

    internal void Forget(WindowId window)
    {
        lock (_gate)
        {
            _owned.Remove(window);
            _failed.Remove(window);
            _leftAlone.Remove(window);
            _alphas.Remove(window);
            _veiled.Remove(window);
        }
    }

    internal void ResetAll()
    {
        WindowId[] windows;
        lock (_gate)
        {
            _closed = true;
            windows = [.. _owned.Keys];
        }

        foreach (var window in windows)
            Reset(window);
    }

    internal RecoverySnapshot Stamp(RecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (_owned.Count == 0)
                return snapshot;

            var clients = new RecoveredClient[snapshot.Clients.Count];
            var changed = false;
            var others = new HashSet<WindowId>(_owned.Keys);
            for (var i = 0; i < snapshot.Clients.Count; i++)
            {
                var client = snapshot.Clients[i];
                var layered = _owned.ContainsKey(client.Window);
                clients[i] = client.Layered == layered ? client : client with { Layered = layered };
                changed |= client.Layered != layered;
                others.Remove(client.Window);
            }

            if (others.Count > 0)
                return snapshot with { Clients = clients, Held = [.. others.Select(window => new HeldWindow(window, "", 0))] };

            return changed ? snapshot with { Clients = clients } : snapshot;
        }
    }

    internal void ReleaseAdded(WindowId window)
    {
        lock (_gate)
        {
            _owned.Remove(window);
            _failed.Remove(window);
            _leftAlone.Remove(window);

            if (!_styles.TryRead(window, out var chrome) || (chrome.ExStyle & LayeredStyle) == 0)
                return;

            _owned[window] = new Original(chrome.ExStyle & ~LayeredStyle, 255);
            Reset(window);
        }
    }

    private void Restore(WindowId window, Original original)
    {
        if (_styles.TryRead(window, out var now))
            _styles.TryWriteExStyle(window, (now.ExStyle & ~LayeredStyle) | (original.ExStyle & LayeredStyle));
        if ((original.ExStyle & LayeredStyle) != 0)
            _styles.TrySetAlpha(window, original.Alpha);
    }

    private void Fail(WindowId window)
    {
        _failed.Add(window);
        Report(window, "could not be layered.");
    }

    private static void Report(WindowId window, string reason)
    {
        var message = "Window 0x" + window.Value.ToString("x", CultureInfo.InvariantCulture) + " " + reason;

        Console.Error.WriteLine(message);
    }

    private readonly record struct Original(uint ExStyle, byte Alpha);
}
