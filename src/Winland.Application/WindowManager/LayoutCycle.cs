using static Winland.Domain.OrdinalText;

namespace Winland.Application;

public sealed partial class WindowManager
{
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
        return PresentIfShown(desk, covered, desk.Layout.Visible());
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
