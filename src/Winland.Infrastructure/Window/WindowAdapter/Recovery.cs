using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter : IDisposable
{
    public void Recover(RecoverySnapshot? snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (snapshot is null)
            return;

        foreach (var held in snapshot.Held)
        {
            if (!_opacity.Owns(held.Window) && WindowIdentity.Recognize(held))
                _opacity.ReleaseAdded(held.Window);
        }

        if (snapshot.Clients.Count == 0)
            return;

        var kept = new List<RecoveredClient>(snapshot.Clients.Count);
        foreach (var client in snapshot.Clients)
        {
            var recognized = WindowIdentity.Recognize(client);
            if (recognized == WindowIdentity.Recognition.Other)
                continue;

            Repair(client);
            if (recognized != WindowIdentity.Recognition.SameClient)
                continue;

            if (IsOverlay(PInvoke.GetWindowLong(new HWND((nint)client.Window.Value), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE)))
            {
                if (!snapshot.Clean && client.Hidden)
                    Reveal(client.Window);

                continue;
            }

            if (snapshot.Clean && client.Hidden && !_desktop.Shown(client.Window))
                continue;

            if (_desktop.Minimized(client.Window))
            {
                if (client.Hidden)
                    Reveal(client.Window);

                continue;
            }

            kept.Add(client);
        }

        if (kept.Count == 0)
            return;

        _core.Restore(new RecoverySnapshot(kept));
        foreach (var client in kept)
        {
            if (!client.Hidden)
                continue;

            if (_desktop.Exists(client.Window))
                Reveal(client.Window);
        }

        foreach (var client in kept)
        {
            var hwnd = new HWND((nint)client.Window.Value);
            if (TryDescribe(hwnd, client.Window, out var appeared))
            {
                var outcome = _core.Handle(appeared);
                NoteManaged(client.Window, outcome, appeared);
                Apply(outcome);
            }
        }

        Apply(_core.Handle(new RestoredAdopted()));
    }

    private void Repair(RecoveredClient client)
    {
        if (client.FrameStyle is uint style)
            _borderless.PutBack(client.Window, style);

        if (client.Region)
            _effects.DropLeftoverRegion(client.Window);

        if (client.Layered)
            _opacity.ReleaseAdded(client.Window);
    }

    public void DropGone()
    {
        foreach (var client in _core.Recovery().Clients)
        {
            if (!_desktop.Exists(client.Window))
                Apply(_core.Handle(new WindowClosed(client.Window)));
        }
    }

    public void RevealHidden()
    {
        lock (_showGate)
            _releasing = true;

        LandMotions();
        var hidden = new List<WindowId>();
        foreach (var client in _core.Recovery().Clients)
        {
            if (client.Hidden)
                hidden.Add(client.Window);
        }

        lock (_showGate)
        {
            foreach (var window in hidden)
            {
                if (_desktop.Exists(window))
                    ShowConcealed(window);
            }
        }
    }

    internal void Publish()
    {
        if (_releasing)
            return;

        _changed?.Invoke();
    }

    internal RecoverySnapshot? Capture()
    {
        if (_releasing)
            return null;

        var snapshot = Stamp(_core.Recovery());
        _published = snapshot;
        return snapshot;
    }

    internal void Salvage()
    {
        _releasing = true;
        var hidden = new HashSet<WindowId>(_concealed.Keys);
        if (_published is RecoverySnapshot snapshot)
        {
            foreach (var client in snapshot.Clients)
            {
                if (client.Hidden)
                    hidden.Add(client.Window);
            }
        }

        foreach (var window in hidden)
        {
            if (_desktop.Exists(window))
                ShowConcealed(window);
        }

        PutBackFrames();
    }

    internal RecoverySnapshot Stamp(RecoverySnapshot snapshot) =>
        _effects.Stamp(_borderless.Stamp(_opacity.Stamp(snapshot)));
}
