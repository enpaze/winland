using System.Collections.Concurrent;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winland.Infrastructure;

internal static class InputInjector
{
    private static readonly BlockingCollection<INPUT[]> Pending = new();
    private static readonly Lock Gate = new();
    private static Thread? _worker;

    internal static void Post(params INPUT[] inputs)
    {
        if (inputs.Length == 0)
            return;

        lock (Gate)
        {
            if (_worker is null)
            {
                _worker = new Thread(Run) { IsBackground = true, Name = "Winland input injector", Priority = ThreadPriority.Highest };
                _worker.Start();
            }
        }

        Pending.Add(inputs);
    }

    private static void Run()
    {
        foreach (var inputs in Pending.GetConsumingEnumerable())
        {
            try
            {
                KeyboardHook.Send(inputs);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine(exception);
            }
        }
    }
}
