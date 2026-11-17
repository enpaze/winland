using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    public void AdoptExisting() => AdoptExisting(ExistingWindows());

    internal void AdoptExisting(IEnumerable<nint> windows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var described = new List<WindowAppeared>();
        foreach (var handle in windows)
        {
            try
            {
                if (TryDescribe(new HWND(handle), new WindowId(handle), out var appeared))
                    described.Add(appeared);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine(exception);
            }
        }

        try
        {
            var outcome = _core.Adopt(described);
            foreach (var appeared in described)
                NoteManaged(appeared.Window, outcome, appeared);

            Apply(outcome);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(exception);
        }
    }

    internal static IReadOnlyList<nint> ExistingWindows()
    {
        var found = new List<nint>();
        _ = PInvoke.EnumWindows((hwnd, _) =>
        {
            if (PInvoke.IsWindowVisible(hwnd) && !PInvoke.IsIconic(hwnd))
                found.Add((nint)hwnd);

            return true;
        }, default);
        return found;
    }
}
