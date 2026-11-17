using System.Collections.Concurrent;
using System.Diagnostics;

namespace Winland.Infrastructure;

internal interface IMotionTarget
{
    bool Hung(WindowId window);

    void Move(WindowId window, Rectangle bounds, Rectangle content);

    void Release(WindowId window);

    void Place(WindowId window, Rectangle bounds);

    void PlaceAsync(WindowId window, Rectangle bounds);

    void Alpha(WindowId window, byte alpha);

    void Border(WindowId window, BorderColor color);

    void Hide(WindowId window);

    void Close(WindowId window);

    bool Shown(WindowId window);

    void Pace();
}

internal sealed class MotionPlayer : IDisposable
{
    internal static readonly TimeSpan CloseGrace = TimeSpan.FromSeconds(1.5);

    private readonly IMotionTarget _target;
    private readonly Func<double> _clock;
    private readonly Animator _animator = new();
    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<WindowId, Lock> _windows = new();
    private readonly Dictionary<WindowId, long> _stamps = [];
    private readonly Dictionary<WindowId, Closing> _closing = [];
    private readonly Dictionary<WindowId, double> _graces = [];
    private readonly AutoResetEvent _wake = new(false);
    private readonly bool _threaded;
    private Thread? _thread;
    private volatile bool _stopped;
    private int _epoch;
    private long _nextError = long.MinValue;

    internal MotionPlayer(IMotionTarget target, Func<double>? clock = null, bool threaded = true)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        _clock = clock ?? (() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
        _threaded = threaded;
    }

    internal Lock For(WindowId window) => _windows.GetOrAdd(window, _ => new Lock());

    internal void Forget(WindowId window)
    {
        lock (_gate)
        {
            if (_animator.Running(window) || _closing.ContainsKey(window))
                return;

            _stamps.Remove(window);
            _graces.Remove(window);
        }

        _windows.TryRemove(window, out _);
    }

    internal bool Running(WindowId window)
    {
        lock (_gate)
            return _animator.Running(window) || _closing.ContainsKey(window);
    }

    internal bool Begin(Motion motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if (_stopped)
            return false;

        Frame? first;
        lock (_gate)
        {
            var now = _clock();
            Touch(motion.Window);
            _closing.Remove(motion.Window);
            if (motion.End == MotionEnd.Close && motion.CloseGraceMilliseconds is int grace)
                _graces[motion.Window] = grace;
            else
                _graces.Remove(motion.Window);
            _animator.Start(motion, now);
            first = _animator.Peek(motion.Window, now);
        }

        if (first?.Bounds is null)
            _target.Release(motion.Window);

        if (first is not null)
            Show(first);

        Wake();
        return true;
    }

    internal void Close(WindowId window, Rectangle rest, byte alpha)
    {
        if (_stopped)
        {
            _target.Close(window);
            return;
        }

        lock (_gate)
        {
            Touch(window);
            _animator.Forget(window);
            _graces.Remove(window);
            _closing[window] = new Closing(_clock() + CloseGrace.TotalMilliseconds, rest, alpha, Stamp(window));
        }

        _target.Alpha(window, 0);
        _target.Close(window);
        Wake();
    }

    internal bool Settle(WindowId window, Rectangle bounds)
    {
        lock (_gate)
        {
            if (_animator.Settle(window, bounds))
                return true;

            Touch(window);
        }

        _target.Release(window);
        return false;
    }

    internal PaintHold Paint(WindowId window, byte alpha, BorderColor border)
    {
        lock (_gate)
        {
            var hold = _animator.Running(window) ? _animator.Paint(window, alpha, border, _clock()) : default;
            return _closing.ContainsKey(window) ? hold with { Alpha = true } : hold;
        }
    }

    internal void Halt(WindowId window)
    {
        lock (_gate)
        {
            Touch(window);
            _animator.Forget(window);
            _closing.Remove(window);
        }

        _target.Release(window);
    }

    internal void Drop(WindowId window)
    {
        lock (For(window))
            Halt(window);
    }

    internal bool Tick()
    {
        List<(Frame Frame, long Stamp)> frames;
        List<(WindowId Window, Closing Closing)> due = [];
        lock (_gate)
        {
            var now = _clock();
            var stepped = _animator.Step(now);
            frames = new List<(Frame, long)>(stepped.Count);
            foreach (var frame in stepped)
                frames.Add((frame, Stamp(frame.Window)));

            foreach (var (window, closing) in _closing)
            {
                if (closing.Due <= now)
                    due.Add((window, closing));
            }

            foreach (var (window, _) in due)
                _closing.Remove(window);
        }

        foreach (var (frame, stamp) in frames.OrderBy(pair => pair.Item1.Done))
        {
            var window = For(frame.Window);
            if (frame.Done)
                window.Enter();
            else if (!window.TryEnter())
                continue;

            try
            {
                lock (_gate)
                {
                    if (Stamp(frame.Window) != stamp)
                        continue;
                }

                if (frame.Done)
                    Rest(frame);
                else if (!_target.Hung(frame.Window))
                    Show(frame);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Report(exception);
            }
            finally
            {
                window.Exit();
            }
        }

        foreach (var (window, closing) in due)
        {
            lock (For(window))
            {
                bool placedSince;
                lock (_gate)
                {
                    placedSince = Stamp(window) != closing.Stamp;
                    if (placedSince && _animator.Running(window))
                        continue;
                }

                if (_target.Shown(window))
                {
                    if (!placedSince)
                        Place(window, closing.Rest);
                    _target.Alpha(window, closing.Alpha);
                }
            }
        }

        lock (_gate)
            return _animator.Count > 0 || _closing.Count > 0;
    }

    internal void Land()
    {
        _stopped = true;
        _wake.Set();
        var thread = _thread;
        if (thread is not null && thread != Thread.CurrentThread)
            thread.Join(TimeSpan.FromSeconds(2));

        IReadOnlyList<Frame> frames;
        List<(WindowId Window, Closing Closing)> closing;
        lock (_gate)
        {
            frames = _animator.Land();
            closing = [.. _closing.Select(pair => (pair.Key, pair.Value))];
            _closing.Clear();

            foreach (var frame in frames)
                Touch(frame.Window);
            foreach (var (window, _) in closing)
                Touch(window);
        }

        foreach (var frame in frames)
        {
            lock (For(frame.Window))
            {
                if (frame.End == MotionEnd.Close)
                {
                    Rest(frame);
                    continue;
                }

                if (frame.Border is BorderColor border)
                    _target.Border(frame.Window, border);
                Place(frame.Window, frame.Rest);
                _target.Alpha(frame.Window, frame.RestAlpha);
            }
        }

        foreach (var (window, pending) in closing)
        {
            lock (For(window))
            {
                if (_target.Shown(window))
                {
                    Place(window, pending.Rest);
                    _target.Alpha(window, pending.Alpha);
                }
            }
        }

        lock (_gate)
        {
            _epoch++;
            _thread = null;
            _stopped = false;
        }
    }

    public void Dispose()
    {
        Land();
        _wake.Dispose();
    }

    private void Show(Frame frame)
    {
        if (frame.Bounds is Rectangle bounds)
            _target.Move(frame.Window, bounds, frame.Content ?? frame.Goal ?? bounds);
        if (frame.Alpha is byte alpha)
            _target.Alpha(frame.Window, alpha);
        if (frame.Border is BorderColor border)
            _target.Border(frame.Window, border);
    }

    private void Place(WindowId window, Rectangle bounds)
    {
        if (_target.Hung(window))
            _target.PlaceAsync(window, bounds);
        else
            _target.Place(window, bounds);
    }

    private void Rest(Frame frame)
    {
        if (frame.Border is BorderColor border)
            _target.Border(frame.Window, border);

        switch (frame.End)
        {
            case MotionEnd.Hide:
                _target.Hide(frame.Window);
                Place(frame.Window, frame.Rest);
                _target.Alpha(frame.Window, frame.RestAlpha);
                return;
            case MotionEnd.Close:
                if (!_stopped)
                {
                    lock (_gate)
                    {
                        var grace = _graces.Remove(frame.Window, out var own) ? own : CloseGrace.TotalMilliseconds;
                        _closing[frame.Window] = new Closing(_clock() + grace, frame.Rest, frame.RestAlpha, Stamp(frame.Window));
                    }
                }

                if (frame.Alpha is byte faded)
                    _target.Alpha(frame.Window, faded);
                _target.Close(frame.Window);
                return;
            default:
                if (frame.Bounds is Rectangle bounds)
                    Place(frame.Window, bounds);
                else
                    _target.Release(frame.Window);
                if (frame.Alpha is byte alpha)
                    _target.Alpha(frame.Window, alpha);
                return;
        }
    }

    private void Touch(WindowId window) => _stamps[window] = Stamp(window) + 1;

    private long Stamp(WindowId window) => _stamps.GetValueOrDefault(window);

    private void Wake()
    {
        if (!_threaded)
            return;

        lock (_gate)
        {
            if (_thread is null && !_stopped)
            {
                var epoch = _epoch;
                _thread = new Thread(() => Run(epoch)) { IsBackground = true, Name = "Winland motions", Priority = ThreadPriority.AboveNormal };
                _thread.Start();
            }
        }

        _wake.Set();
    }

    private void Report(Exception exception)
    {
        var now = Environment.TickCount64;
        if (now < _nextError)
            return;

        _nextError = now + 60_000;
        Console.Error.WriteLine(exception);
    }

    private void Run(int epoch)
    {
        while (!_stopped && Volatile.Read(ref _epoch) == epoch)
        {
            bool more;
            try
            {
                more = Tick();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Report(exception);
                more = false;
            }

            if (_stopped || Volatile.Read(ref _epoch) != epoch)
                return;

            if (!more)
                _wake.WaitOne();
            _target.Pace();
        }
    }

    private readonly record struct Closing(double Due, Rectangle Rest, byte Alpha, long Stamp);
}
