using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter : IDisposable
{
    internal void Apply(Outcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.IsEmpty || _disposed)
            return;

        if (_applyGate.IsHeldByCurrentThread)
            return;

        lock (_applyGate)
        {
            while (_decided.TryDequeue(out var next))
                ApplyCore(next);
        }
    }

    private void Front(Outcome outcome)
    {
        List<WindowId> held = [];
        if (outcome.Focus is WindowId focus)
        {
            held = outcome.ZOrder
                .Where(request => request.Top && request.Window != focus && !_desktop.Hung(request.Window))
                .Select(request => request.Window)
                .Where(_desktop.HoldOnTop)
                .ToList();
            _ = _desktop.Foreground(focus);
            foreach (var window in held)
            {
                _desktop.LetGo(window);
                BorderHost.Follow(window);
            }
        }

        Restack(held.Count == 0 ? outcome.ZOrder : [.. outcome.ZOrder.Where(request => !(request.Top && held.Contains(request.Window)))]);
    }

    internal void ApplyCore(Outcome outcome)
    {
        var windows = new Dictionary<ClientId, WindowId>(outcome.State.Clients.Count);
        foreach (var client in outcome.State.Clients)
            windows[client.Id] = client.Window;

        var motions = _releasing ? [] : MotionsOf(outcome);

        var placed = new Dictionary<WindowId, Rectangle>();
        foreach (var placement in outcome.Placements)
        {
            if (windows.TryGetValue(placement.Client, out var window))
                placed[window] = placement.Bounds;
        }

        var shownAt = new Dictionary<Rectangle, WindowId>();
        foreach (var window in outcome.Show)
        {
            if (placed.TryGetValue(window, out var bounds))
                shownAt[bounds] = window;
        }

        if (outcome.Focus is null && outcome.Hide.Any(_desktop.IsForeground))
            _desktop.ForegroundDesktop();

        var concealLater = new List<WindowId>();
        var under = new List<(WindowId Window, WindowId Above)>();
        foreach (var window in outcome.Hide)
        {
            var at = placed.TryGetValue(window, out var bounds);
            var replacing = at && shownAt.TryGetValue(bounds, out var shown) ? shown : (WindowId?)null;
            if (outcome.Show.Count > 0 && !motions.ContainsKey(window) && (!at || replacing is not null))
            {
                concealLater.Add(window);
                if (replacing is WindowId next && !_desktop.Hung(next) && !_desktop.Hung(window))
                    under.Add((next, window));
            }
            else
            {
                ConcealOrLeave(window, motions);
            }
        }

        foreach (var (window, above) in under)
            _desktop.Under(window, above);

        DropClosedFrames(outcome.State.Clients);

        var borderless = new HashSet<WindowId>();
        foreach (var client in outcome.State.Clients)
        {
            if (client.Fullscreen == FullscreenMode.Fullscreen)
                borderless.Add(client.Window);
        }

        foreach (var placement in outcome.Placements)
        {
            if (!windows.TryGetValue(placement.Client, out var window))
                continue;

            lock (_motions.For(window))
            {
                var fills = borderless.Contains(window);
                if (!(fills && _ownFullscreen.ContainsKey(window)))
                    _borderless.Apply(window, fills);
                if (outcome.Live && !motions.ContainsKey(window))
                    PlaceLive(window, placement.Bounds, fills, motions);
                else
                    PlaceMoving(window, placement.Bounds, fills, motions);
            }
        }

        foreach (var closing in outcome.Closes)
            CloseOrLeave(closing, motions);

        BeginRest(motions);

        foreach (var window in outcome.Show)
            Reveal(window);

        foreach (var window in outcome.ShowMinimized)
            Reveal(window);

        var focusFirst = outcome.Focus is WindowId early && !under.Exists(pair => pair.Window == early);
        if (focusFirst)
            Front(outcome);

        var rings = new Dictionary<WindowId, BorderRing>();
        foreach (var decoration in outcome.Decorations)
        {
            if (decoration.Ring is BorderRing ring && windows.TryGetValue(decoration.Client, out var window))
                rings[window] = ring;
        }

        if (concealLater.Count > 0)
        {
            if (!_releasing)
                BorderHost.Update(rings, concealLater);

            var painting = under.Select(pair => pair.Window)
                .Concat(outcome.Show.Where(window => !_desktop.Hung(window)))
                .Distinct()
                .ToList();
            for (var frame = 0; frame < PaintFrames && !painting.TrueForAll(_desktop.Painted); frame++)
                _desktop.Pace();

            _desktop.Pace();
            if (under.Count > 0)
                _desktop.Pace();
            foreach (var window in concealLater)
                ConcealOrLeave(window, motions);
        }

        if (!focusFirst)
            Front(outcome);

        lock (_paintGate)
        {
            if (!_releasing)
            {
                foreach (var window in outcome.ResetDecorations)
                {
                    DropMotion(window);
                    _borderless.Release(window);
                    ResetDecoration(window);
                    _painted.Remove(window);
                }

                foreach (var decoration in outcome.Decorations)
                {
                    if (!windows.TryGetValue(decoration.Client, out var window))
                        continue;

                    if (!_painted.TryGetValue(window, out var last) || !decoration.Equals(last) || !_dwmPainted.ContainsKey(window) || _motions.Running(window))
                        _painted[window] = Paint(window, decoration) ? decoration : null;
                }

                ForgetDwmPainted(windows.Values);
            }
        }

        if (!_releasing)
        {
            if (outcome.Decorations.Count > 0 || outcome.ResetDecorations.Count > 0)
                BorderHost.Update(rings);

            GroupBarHost.Update(outcome.GroupBars);
            TrayHost.Update(outcome.Tray, outcome.ConfigNotice);
        }

        SessionEffects.Apply(outcome);

        if (!_disposed)
            Publish();

        foreach (var launch in outcome.Launches)
        {
            var pid = ProcessLauncher.TryLaunch(launch);
            if (pid is int id)
                _core.NoteLaunched(id, launch);
        }

        if (outcome.Exit)
            SessionHalt.Request();
    }

    private void Conceal(WindowId window)
    {
        lock (_showGate)
        {
            if (_releasing)
                return;

            _concealed[window] = 0;
            ShowOrPost(window, ShowAs.Hide);
        }
    }

    private void Reveal(WindowId window)
    {
        lock (_showGate)
        {
            if (_releasing)
                return;

            ShowConcealed(window);
        }
    }

    private void ShowConcealed(WindowId window)
    {
        if (_concealed.TryRemove(window, out _) && !_desktop.Shown(window))
            _revealed[window] = 0;
        ShowOrPost(window, _desktop.Minimized(window) ? ShowAs.ShowMinimized : ShowAs.Show);
    }

    private void ShowOrPost(WindowId window, ShowAs show)
    {
        if (_desktop.Hung(window))
            _desktop.ShowAsync(window, show);
        else
            _desktop.Show(window, show);
    }

    internal static string WindowTitle(HWND hwnd)
    {
        var length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
            return "";

        Span<char> buffer = length <= 512 ? stackalloc char[length + 1] : new char[length + 1];
        var written = PInvoke.GetWindowText(hwnd, buffer);
        return written <= 0 ? "" : new string(buffer[..written]);
    }

    internal static bool IsShellTaskbar(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[32];
        var name = buffer[..Math.Max(PInvoke.GetClassName(hwnd, buffer), 0)];
        return name.SequenceEqual("Shell_TrayWnd") || name.SequenceEqual("Shell_SecondaryTrayWnd");
    }

    internal static string ClassName(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        var length = PInvoke.GetClassName(hwnd, buffer);
        return length <= 0 ? "" : new string(buffer[..length]);
    }

    internal static bool IsOverlay(int extended) =>
        ((extended & (LayeredStyle | TopmostStyle)) != 0 && (extended & ClickThroughStyle) != 0)
        || (extended & NoActivateStyle) != 0;

    internal static bool IsCloaked(HWND hwnd)
    {
        Span<byte> value = stackalloc byte[sizeof(int)];
        var result = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, value);
        return result.Succeeded && System.Runtime.InteropServices.MemoryMarshal.Read<int>(value) != 0;
    }
}
