using System.Collections.Concurrent;
using Windows.Win32;

namespace Winland.Infrastructure;

internal sealed class LoopInvoker
{
    internal const uint Message = 0x8000 + 0x48;

    private readonly ConcurrentQueue<Once> _queue = new();
    private uint _thread;

    internal void Attach(uint thread) => Volatile.Write(ref _thread, thread);

    internal void Detach() => Volatile.Write(ref _thread, 0);

    internal void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var thread = Volatile.Read(ref _thread);
        if (thread == 0)
        {
            action();
            return;
        }

        var once = new Once(action, posted: true);
        _queue.Enqueue(once);

        if (!PInvoke.PostThreadMessage(thread, Message, default, default) || Volatile.Read(ref _thread) == 0)
            once.Run();
    }

    internal void Drain()
    {
        while (_queue.TryDequeue(out var once))
            once.Run();
    }

    internal void Invoke(Action action, TimeSpan answer, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(action);
        var thread = Volatile.Read(ref _thread);
        if (thread == 0 || thread == PInvoke.GetCurrentThreadId())
        {
            action();
            return;
        }

        var started = Environment.TickCount64;
        var once = new Once(action);
        _queue.Enqueue(once);
        if (!PInvoke.PostThreadMessage(thread, Message, default, default) || !once.Wait(answer))
        {
            if (!once.Run())
            {
                var left = limit - TimeSpan.FromMilliseconds(Environment.TickCount64 - started);
                if (left > TimeSpan.Zero)
                    once.Wait(left);
            }
        }

        once.Rethrow();
    }

    private sealed class Once(Action action, bool posted = false)
    {
        private readonly TaskCompletionSource _done = new();
        private int _claimed;
        private Exception? _error;

        internal bool Run()
        {
            if (Interlocked.Exchange(ref _claimed, 1) != 0)
                return false;

            try
            {
                action();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                if (posted)
                    Console.Error.WriteLine(exception);
                else
                    _error = exception;
            }
            finally
            {
                _done.TrySetResult();
            }

            return true;
        }

        internal bool Wait(TimeSpan wait) => _done.Task.Wait(wait);

        internal void Rethrow()
        {
            if (_error is Exception error)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(error);
        }
    }
}
