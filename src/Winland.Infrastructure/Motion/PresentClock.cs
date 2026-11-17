using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Threading;

namespace Winland.Infrastructure;

internal static class PresentClock
{
    private static readonly double TicksPerMillisecond = Stopwatch.Frequency / 1000.0;

    private static readonly long Offset = Stopwatch.Frequency / 2000;

    [ThreadStatic]
    private static Microsoft.Win32.SafeHandles.SafeFileHandle? _timer;

    internal static unsafe bool WaitForFrame()
    {
        var info = new DWM_TIMING_INFO { cbSize = (uint)sizeof(DWM_TIMING_INFO) };
        if (PInvoke.DwmGetCompositionTimingInfo(HWND.Null, &info).Failed || info.qpcRefreshPeriod == 0)
            return false;

        if (_timer is null)
        {
            var created = PInvoke.CreateWaitableTimerEx(null, (string?)null, PInvoke.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, 0x1F0003);
            if (created.IsInvalid)
                return false;

            _timer = created;
        }

        var now = Stopwatch.GetTimestamp();
        var due = Next(now, (long)info.qpcVBlank, (long)info.qpcRefreshPeriod) + Offset - now;
        var wait = -Math.Max(1, due * 10_000_000 / Stopwatch.Frequency);
        if (!PInvoke.SetWaitableTimer(_timer, wait, 0, null, null, false))
            return false;

        return PInvoke.WaitForSingleObject(_timer, 100) == WAIT_EVENT.WAIT_OBJECT_0;
    }

    internal static unsafe double Now()
    {
        var now = Stopwatch.GetTimestamp();
        var info = new DWM_TIMING_INFO { cbSize = (uint)sizeof(DWM_TIMING_INFO) };
        if (PInvoke.DwmGetCompositionTimingInfo(HWND.Null, &info).Failed)
            return now / TicksPerMillisecond;

        return Next(now, (long)info.qpcVBlank, (long)info.qpcRefreshPeriod) / TicksPerMillisecond;
    }

    internal static long Next(long now, long vblank, long period)
    {
        if (period <= 0 || vblank <= 0)
            return now;

        var since = now - vblank;
        var frames = since >= 0 ? since / period : -((-since + period - 1) / period);
        return vblank + ((frames + 1) * period);
    }
}
