using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    private static Dictionary<WindowId, Motion> MotionsOf(Outcome outcome)
    {
        var motions = new Dictionary<WindowId, Motion>(outcome.Motions.Count);
        foreach (var motion in outcome.Motions)
            motions[motion.Window] = motion;
        return motions;
    }

    internal bool Tick() => _motions.Tick();

    private void ConcealOrLeave(WindowId window, Dictionary<WindowId, Motion> motions)
    {
        lock (_motions.For(window))
        {
            if (motions.Remove(window, out var motion) && motion.End == MotionEnd.Hide && _motions.Begin(motion))
                return;

            _motions.Halt(window);
            Conceal(window);
        }
    }

    private void PlaceMoving(WindowId window, Rectangle bounds, bool borderless, Dictionary<WindowId, Motion> motions)
    {
        lock (_motions.For(window))
        {
            if (motions.Remove(window, out var motion))
            {
                if (motion.FadeFrom == 0 && (motion.Warp || !_motions.Running(window)))
                    PlaceFrame(window, motion.From ?? bounds, borderless);

                if (_motions.Begin(motion) && (motion.Move is not null || _motions.Settle(window, bounds)))
                    return;

                PlaceFrame(window, bounds, borderless);
                EndStandIn(window, null);
                return;
            }

            if (_motions.Settle(window, bounds))
                return;

            PlaceFrame(window, bounds, borderless);
            EndStandIn(window, null);
        }
    }

    private void PlaceLive(WindowId window, Rectangle bounds, bool borderless, Dictionary<WindowId, Motion> motions)
    {
        lock (_motions.For(window))
        {
            motions.Remove(window);
            if (_motions.Settle(window, bounds))
                return;

            EndStandIn(window, null);
            QuickPlace(window, bounds, borderless);
        }
    }

    private void PlaceFrame(WindowId window, Rectangle bounds, bool borderless)
    {
        _placing[window] = 0;
        try
        {
            if (!borderless)
            {
                Place(window, bounds);
                return;
            }

            var trails = TryOuter(window, out var before) && _quick.Trails(window, before);
            if (!trails && before == bounds)
                return;

            if (Move(window, bounds) && trails)
                SendQuick(window, bounds, keepSize: false);
        }
        finally
        {
            _placing.TryRemove(window, out _);
        }
    }

    private void BeginRest(Dictionary<WindowId, Motion> motions)
    {
        foreach (var motion in motions.Values)
        {
            lock (_motions.For(motion.Window))
                _ = _motions.Begin(motion);
        }

        motions.Clear();
    }

    private void CloseOrLeave(WindowId window, Dictionary<WindowId, Motion> motions)
    {
        lock (_motions.For(window))
        {
            if (motions.Remove(window, out var motion) && motion.End == MotionEnd.Close && _motions.Begin(motion))
                return;

            if (TryVisible(window, out var rest))
                _motions.Close(window, rest, _opacity.Wanted(window));
            else
                _desktop.Close(window);
        }
    }

    private void LandMotions() => _motions.Land();

    private void DropMotion(WindowId window) => _motions.Drop(window);

    private static readonly Lock SettingsGate = new();
    private static bool? _reducedMotion;

    private static bool ReducedMotion()
    {
        lock (SettingsGate)
            return _reducedMotion ??= ReadReducedMotion();
    }

    internal static void ForgetSettings()
    {
        lock (SettingsGate)
            _reducedMotion = null;
    }

    private static unsafe bool ReadReducedMotion()
    {
        BOOL animate = true;
        return PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION, 0, &animate, 0)
            && !animate;
    }

    private sealed class MotionTarget(WindowAdapter adapter) : IMotionTarget
    {
        public bool Hung(WindowId window) => adapter._desktop.Hung(window);

        public void Move(WindowId window, Rectangle bounds, Rectangle content)
        {
            GroupBarHost.Moves(window, true);
            if (!adapter._desktop.Shown(window))
            {
                adapter.PlaceFrame(window, bounds, adapter._borderless.Owns(window));
                return;
            }

            if (!adapter.StandFor(window, bounds, content))
                adapter.QuickPlace(window, bounds, adapter._borderless.Owns(window));
        }

        public void PlaceAsync(WindowId window, Rectangle bounds)
        {
            adapter.PlaceAsync(window, bounds, adapter._borderless.Owns(window));
            adapter.EndStandIn(window, null);
            GroupBarHost.Moves(window, false);
        }

        public void Place(WindowId window, Rectangle bounds)
        {
            adapter.PlaceFrame(window, bounds, adapter._borderless.Owns(window));
            adapter.EndStandIn(window, null);
            GroupBarHost.Moves(window, false);
        }

        public void Alpha(WindowId window, byte alpha)
        {
            adapter._opacity.Apply(window, alpha);
            adapter.StandInAlpha(window, alpha);

            MotionAlpha.Set(window, adapter._motions.Running(window) ? alpha : null);
        }

        public void Border(WindowId window, BorderColor color)
        {
            if (adapter._releasing)
                return;

            BorderHost.Tint(window, color);
        }

        public void Hide(WindowId window)
        {
            adapter.Conceal(window);
            adapter.EndStandIn(window, null);
            GroupBarHost.Moves(window, false);
        }

        public void Release(WindowId window)
        {
            adapter.EndStandIn(window, null);
            GroupBarHost.Moves(window, false);
            MotionAlpha.Set(window, null);
        }

        public void Close(WindowId window)
        {
            adapter.HideStandIn(window);
            adapter._desktop.Close(window);
        }

        public bool Shown(WindowId window) => adapter._desktop.Exists(window) && adapter._desktop.Shown(window);

        public void Pace() => adapter._desktop.Pace();
    }
}
