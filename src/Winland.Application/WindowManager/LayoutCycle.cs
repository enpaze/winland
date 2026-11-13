using static Winland.Domain.OrdinalText;

namespace Winland.Application;

/// <summary>The <c>layout</c> and <c>cyclelayout</c> dispatchers.</summary>
public sealed partial class WindowManager
{
    // Selects the named Layout on the current Workspace. The running Layout is kept as it is, and a name that is no
    // Layout changes nothing. The choice lives on the Workspace until the config is applied again.
    private Outcome SelectLayout(string arguments)
    {
        var name = arguments.Trim();
        if (LayoutFactory.Named(name) is not Layout created)
            return View([]) with { Error = DidYouMean.After($"error: unknown layout '{name}'", name, LayoutFactory.Names) };

        var desk = CurrentWorkspace();
        return LayoutFactory.NameOf(created) == LayoutFactory.NameOf(desk.Layout)
            ? View([])
            : ChangeLayout(desk, created);
    }

    // dwindle, scrolling, then floating. The choice lives on the Workspace until the config is applied again.
    private Outcome CycleWorkspaceLayout(string arguments)
    {
        var desk = CurrentWorkspace();
        var next = NextLayout(LayoutFactory.NameOf(desk.Layout), GoesBack(arguments));
        return ChangeLayout(desk, LayoutFactory.Named(next)!);
    }

    private Outcome ChangeLayout(LiveWorkspace desk, Layout created)
    {
        var covered = desk.Covered();
        AdoptLayout(desk, created);
        Refit(desk);
        return _workspaces.Shown.IsShown(desk.Id)
            ? Present(desk, covered, desk.Layout.Visible())
            : View([]);
    }

    private static bool GoesBack(string arguments)
    {
        var word = arguments.Trim();
        var space = word.IndexOf(' ', StringComparison.Ordinal);
        if (space >= 0)
            word = word[..space];

        return Is(word, "back");
    }

    private static string NextLayout(string current, bool back)
    {
        var cycle = LayoutFactory.Names;
        var index = -1;
        for (var i = 0; i < cycle.Count; i++)
        {
            if (Is(cycle[i], current))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return "dwindle";

        var step = back ? -1 : 1;
        return cycle[((index + step) % cycle.Count + cycle.Count) % cycle.Count];
    }
}
