namespace Winland.Domain;

internal sealed class LiveMonitor(MonitorId id, Monitor reported, Monitor effective)
{
    public MonitorId Id { get; } = id;

    public Monitor Reported { get; set; } = reported;

    public Monitor Arranged { get; set; } = effective;

    public bool Focused { get; set; }

    public Rectangle Bounds => new(Arranged.X, Arranged.Y, Arranged.Width, Arranged.Height);

    public bool Covers(Point point) =>
        point.X >= Reported.X
        && point.Y >= Reported.Y
        && point.X < Reported.X + Reported.Width
        && point.Y < Reported.Y + Reported.Height;
}
