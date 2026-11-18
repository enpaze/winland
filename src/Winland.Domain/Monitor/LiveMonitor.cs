namespace Winland.Domain;

internal sealed class LiveMonitor(MonitorId id, Monitor reported, Monitor effective)
{
    public MonitorId Id { get; } = id;

    public Monitor Reported { get; set; } = reported;

    public Monitor Arranged { get; set; } = effective;

    public bool Focused { get; set; }

    public Rectangle Bounds => new(Arranged.X, Arranged.Y, Arranged.Width, Arranged.Height);

    public bool Covers(Point point) => new Rectangle(Reported.X, Reported.Y, Reported.Width, Reported.Height).Contains(point);
}
