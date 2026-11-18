namespace Winland.Infrastructure;

internal interface IWindowFrame
{
    bool TryReadStyle(WindowId window, out uint style);

    bool TryWriteStyle(WindowId window, uint style);
}

internal static class BorderlessStyle
{
    internal const uint Popup = 0x80000000;
    internal const uint Caption = 0x00C00000;
    internal const uint ThickFrame = 0x00040000;
    internal const uint Border = 0x00800000;
    internal const uint DialogFrame = 0x00400000;
    internal const uint Frame = Caption | ThickFrame | Border | DialogFrame;

    internal static uint Strip(uint style) => (style & ~Frame) | Popup;

    internal static uint Restore(uint current, uint original) => (current & ~(Frame | Popup)) | (original & (Frame | Popup));
}

internal sealed class BorderlessFrame
{
    private readonly IWindowFrame _frames;
    private readonly Func<WindowId, bool> _hung;
    private readonly Dictionary<WindowId, uint> _original = [];
    private readonly Lock _gate = new();
    private bool _closed;

    internal BorderlessFrame(IWindowFrame frames, Func<WindowId, bool>? hung = null)
    {
        ArgumentNullException.ThrowIfNull(frames);
        _frames = frames;
        _hung = hung ?? (static _ => false);
    }

    internal bool Owns(WindowId window)
    {
        lock (_gate)
            return _original.ContainsKey(window);
    }

    internal void Apply(WindowId window, bool borderless)
    {
        lock (_gate)
        {
            if (!borderless)
            {
                if (_original.Remove(window, out var original))
                    WriteBack(window, original);
                return;
            }

            if (_closed || _original.ContainsKey(window) || _hung(window))
                return;

            if (!_frames.TryReadStyle(window, out var style))
                return;

            var stripped = BorderlessStyle.Strip(style);
            _original[window] = style;
            if (stripped == style)
                return;

            if (!_frames.TryWriteStyle(window, stripped))
                _original.Remove(window);
        }
    }

    internal void Release(WindowId window) => Apply(window, false);

    internal RecoverySnapshot Stamp(RecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            var clients = new RecoveredClient[snapshot.Clients.Count];
            var changed = false;
            for (var i = 0; i < snapshot.Clients.Count; i++)
            {
                var client = snapshot.Clients[i];
                uint? style = _original.TryGetValue(client.Window, out var original) ? original : null;
                clients[i] = client.FrameStyle == style ? client : client with { FrameStyle = style };
                changed |= client.FrameStyle != style;
            }

            return changed ? snapshot with { Clients = clients } : snapshot;
        }
    }

    internal void PutBack(WindowId window, uint style)
    {
        lock (_gate)
        {
            _original.Remove(window);
            WriteBack(window, style);
        }
    }

    private void WriteBack(WindowId window, uint original)
    {
        if (_frames.TryReadStyle(window, out var current))
            _frames.TryWriteStyle(window, BorderlessStyle.Restore(current, original));
    }

    internal void ReleaseAll()
    {
        WindowId[] windows;
        lock (_gate)
        {
            _closed = true;
            windows = [.. _original.Keys];
        }

        foreach (var window in windows)
        {
            if (_hung(window))
                Console.Error.WriteLine($"Window 0x{window.Value:x} is not answering. Its frame is put back at the next start.");
            else
                Release(window);
        }
    }
}
