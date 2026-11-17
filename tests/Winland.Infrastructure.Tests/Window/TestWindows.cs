namespace Winland.Infrastructure.Tests;

internal sealed class TestWindows : IDisposable
{
    internal static readonly Rectangle Area = new(0, 0, 1920, 1080);

    private double _now;

    internal TestWindows(string config = "", bool reducedMotion = false, Action? changed = null)
    {
        Desktop = new FakeDesktop(() => _now);
        Core = new WindowManager();
        Core.Handle(new ConfigLoaded(ConfigParser.Parse("general:border_size = 0\n" + config)));
        Adapter = new WindowAdapter(Core, Desktop, changed, () => _now, threaded: false);
        Core.ReducedMotion = () => reducedMotion;
    }

    internal WindowManager Core { get; }

    internal FakeDesktop Desktop { get; }

    internal WindowAdapter Adapter { get; }

    internal double Now => _now;

    internal WindowId Open(long id)
    {
        Desktop.Add(id, Area);
        var window = new WindowId(id);
        Apply(Core.Handle(new WindowAppeared(window, Area) { Traits = WindowTraits.Normal }));
        return window;
    }

    internal Outcome Dispatch(string name, string arguments = "")
    {
        var outcome = Core.Handle(new Dispatch(name, arguments));
        Apply(outcome);
        return outcome;
    }

    internal void Apply(Outcome outcome)
    {
        Assert.All(outcome.Decorations, decoration => Assert.Null(decoration.Ring));
        Adapter.Apply(outcome);
    }

    internal bool Advance(double milliseconds)
    {
        _now += milliseconds;
        return Adapter.Tick();
    }

    internal void Settle()
    {
        for (var frame = 0; frame < 1000; frame++)
        {
            if (!Advance(16))
                return;
        }

        throw new InvalidOperationException("The motions did not end.");
    }

    public void Dispose() => Adapter.Dispose();
}
