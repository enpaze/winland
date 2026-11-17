using System.Collections.Concurrent;

namespace Winland.Infrastructure;

internal sealed class DeclinedWindows
{
    private readonly ConcurrentDictionary<WindowId, (string Class, int Revision, bool Fullscreen)> _declined = new();

    public bool Fullscreen(WindowId window) => _declined.TryGetValue(window, out var seen) && seen.Fullscreen;

    public void Note(WindowId window, WindowTraits traits, int revision)
    {
        if (traits.Titled)
            _declined[window] = (traits.Class, revision, traits.ExclusiveFullscreen);
        else
            Forget(window);
    }

    public void Forget(WindowId window) => _declined.TryRemove(window, out _);

    public bool Unchanged(WindowId window, bool titleRules, int revision, Func<string> classOf) =>
        !titleRules
        && _declined.TryGetValue(window, out var seen)
        && seen.Revision == revision
        && seen.Class == classOf();
}
