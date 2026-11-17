namespace Winland.Domain;

public sealed class DiscreteScroll
{
    public const long IdleMs = 500;

    private int _sum;
    private int _sign;
    private bool _horizontal;
    private long _timeMs = long.MinValue;
    private bool _started;

    public static bool Emulates(int delta) => delta % TouchpadPointer.Notch != 0;

    public int Emulate(int delta, bool horizontal, long timeMs)
    {
        var sign = Math.Sign(delta);
        var notches = 0;
        var fresh = !_started || sign != _sign || horizontal != _horizontal || timeMs - _timeMs > IdleMs;
        if (fresh)
        {
            _sum = 0;
            notches = 1;
        }

        while (_sum >= TouchpadPointer.Notch)
        {
            notches++;
            _sum -= TouchpadPointer.Notch;
        }

        _started = true;
        _sign = sign;
        _horizontal = horizontal;
        _timeMs = timeMs;
        _sum += Math.Abs(delta);
        return Math.Clamp(notches, 0, short.MaxValue / TouchpadPointer.Notch) * TouchpadPointer.Notch * sign;
    }
}
