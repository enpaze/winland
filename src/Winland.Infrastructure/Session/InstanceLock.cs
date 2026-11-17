namespace Winland.Infrastructure;

public sealed class InstanceLock : IDisposable
{
    private readonly Mutex _mutex;

    private InstanceLock(Mutex mutex) => _mutex = mutex;

    public static string NameFor(string pipe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipe);
        return @"Local\Winland-" + pipe.Replace('\\', '_');
    }

    public static InstanceLock? TryTake(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Mutex mutex;
        bool created;
        try
        {
            mutex = new Mutex(false, name, out created);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (created)
            return new InstanceLock(mutex);

        mutex.Dispose();
        return null;
    }

    public void Dispose() => _mutex.Dispose();
}
