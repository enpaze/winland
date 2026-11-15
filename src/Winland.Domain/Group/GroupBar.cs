namespace Winland.Domain;

/// <summary>
/// The tab bar Winland draws over a Group's tile. <see cref="Bounds"/> is the strip in screen pixels.
/// Each tab names one member, in Group order, side by side. From the bottom of the strip up: the gap to the window,
/// the indicator, the title row and a top gap.
/// </summary>
public sealed record GroupBar(WindowId Shown, Rectangle Bounds, IReadOnlyList<GroupTab> Tabs, GroupBarStyle Style)
{
    /// <summary>
    /// Where a window held by a drag over this bar would join if dropped now, as a <see cref="DropIndex"/>, or null.
    /// The bar marks it.
    /// </summary>
    public int? Insertion { get; init; }

    /// <summary>True when both bars draw the same pixels.</summary>
    public bool SameAs(GroupBar? other) =>
        other is not null
        && other.Shown == Shown
        && other.Bounds == Bounds
        && other.Style == Style
        && other.Insertion == Insertion
        && other.Tabs.SequenceEqual(Tabs);

    /// <summary>The width of one tab. Tabs share the strip with a gap between them.</summary>
    public int TabWidth =>
        Tabs.Count == 0 ? 0 : Math.Max(0, (Bounds.Width - (Style.GapsIn * (Tabs.Count - 1))) / Tabs.Count);

    /// <summary>The indicator of tab <paramref name="index"/>, relative to the strip.</summary>
    public Rectangle IndicatorBounds(int index)
    {
        var y = Bounds.Height - Style.GapsOut - Style.IndicatorHeight;
        return new Rectangle(TabX(index), y, TabWidth, Style.IndicatorHeight);
    }

    /// <summary>The title row of tab <paramref name="index"/>, relative to the strip.</summary>
    public Rectangle TitleBounds(int index)
    {
        var y = Bounds.Height - Style.GapsOut - Style.IndicatorHeight - Style.Height;
        return new Rectangle(TabX(index), y, TabWidth, Style.Height);
    }

    /// <summary>The tab under a point relative to the strip. -1 on the gap between tabs or outside the strip.</summary>
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

    /// <summary>
    /// The place a window dropped at <paramref name="pointer"/>, in screen pixels, joins: the tab after the one whose
    /// middle it passed.
    /// </summary>
    public int DropIndex(Point pointer)
    {
        double step = TabWidth + Style.GapsIn;
        double relative = pointer.X - Bounds.X - (TabWidth / 2);
        return (relative < 0 || step <= 0 ? -1 : (int)(relative / step)) + 1;
    }

    /// <summary>
    /// The mark of <see cref="Insertion"/>, relative to the strip: a line as thick as the indicator down the bar, centred
    /// where the tabs it falls between meet and kept inside the strip. Empty with no insertion.
    /// </summary>
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

/// <summary>One member's tab. <see cref="Focused"/> marks the focused window, which the bar colors as active.</summary>
public readonly record struct GroupTab(WindowId Window, string Title, bool Focused);
