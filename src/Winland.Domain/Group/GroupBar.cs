namespace Winland.Domain;

public sealed record GroupBar(WindowId Shown, Rectangle Bounds, IReadOnlyList<GroupTab> Tabs, GroupBarStyle Style)
{
    public int? Insertion { get; init; }

    public bool SameAs(GroupBar? other) =>
        other is not null
        && other.Shown == Shown
        && other.Bounds == Bounds
        && other.Style == Style
        && other.Insertion == Insertion
        && other.Tabs.SequenceEqual(Tabs);

    public int TabWidth =>
        Tabs.Count == 0 ? 0 : Math.Max(0, (Bounds.Width - (Style.GapsIn * (Tabs.Count - 1))) / Tabs.Count);

    public Rectangle IndicatorBounds(int index)
    {
        var y = Bounds.Height - Style.GapsOut - Style.IndicatorHeight;
        return new Rectangle(TabX(index), y, TabWidth, Style.IndicatorHeight);
    }

    public Rectangle TitleBounds(int index)
    {
        var y = Bounds.Height - Style.GapsOut - Style.IndicatorHeight - Style.Height;
        return new Rectangle(TabX(index), y, TabWidth, Style.Height);
    }

    public int TabAt(int x, int y)
    {
        if (Tabs.Count == 0 || x < 0 || y < 0 || x >= Bounds.Width || y >= Bounds.Height)
            return -1;

        var step = TabWidth + Style.GapsIn;
        if (step <= 0)
            return -1;

        var index = x / step;
        if (index >= Tabs.Count || x - (index * step) >= TabWidth)
            return -1;

        return index;
    }

    public int DropIndex(Point pointer)
    {
        double step = TabWidth + Style.GapsIn;
        double relative = pointer.X - Bounds.X - (TabWidth / 2);
        return (relative < 0 || step <= 0 ? -1 : (int)(relative / step)) + 1;
    }

    public Rectangle InsertionBounds()
    {
        if (Insertion is not int index || Tabs.Count == 0)
            return default;

        var thickness = Math.Max(1, Style.IndicatorHeight);
        var meet = index == 0 ? 0 : (index * (TabWidth + Style.GapsIn)) - (Style.GapsIn / 2);
        return new Rectangle(Math.Max(0, Math.Min(meet - (thickness / 2), Bounds.Width - thickness)), 0, thickness, Bounds.Height);
    }

    private int TabX(int index) => index * (TabWidth + Style.GapsIn);
}

public readonly record struct GroupTab(WindowId Window, string Title, bool Focused);
