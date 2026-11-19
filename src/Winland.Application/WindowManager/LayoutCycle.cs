using static Winland.Domain.OrdinalText;

namespace Winland.Application;

public sealed partial class WindowManager
{
    private Outcome SelectLayout(string arguments)
    {
        var name = arguments.Trim();
        var desk = TopWorkspace();
        if (Is(name, "next") || Is(name, "prev"))
            return ChangeLayout(desk, LayoutFactory.Named(NextLayout(LayoutFactory.NameOf(desk.Layout), back: Is(name, "prev")))!);

        if (LayoutFactory.Named(name) is not Layout created)
            return Rejected(DidYouMean.After($"error: unknown layout '{name}'", name, [.. LayoutFactory.Names, "next", "prev"]));

        return LayoutFactory.NameOf(created) == LayoutFactory.NameOf(desk.Layout)
            ? View([])
            : ChangeLayout(desk, created);
    }

    private Outcome ChangeLayout(LiveWorkspace desk, Layout created)
    {
        var covered = desk.Covered();
        AdoptLayout(desk, created);
        Refit(desk);
        return PresentIfShown(desk, covered, desk.Layout.Visible());
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
