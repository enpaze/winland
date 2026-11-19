using System.Text.Json;

namespace Winland.Application.Tests;

internal static class TestOutcomes
{
    public static Client Client(Outcome outcome, long window) =>
        Assert.ContainsSingle(candidate => candidate.Window == new WindowId(window), outcome.State.Clients);

    public static Rectangle BoundsOf(Outcome outcome, long window)
    {
        var client = Client(outcome, window);
        return Assert.ContainsSingle(placement => placement.Client == client.Id, outcome.Placements).Bounds;
    }

    public static Outcome Place(WindowManager core, long window, Rectangle bounds)
    {
        var id = new WindowId(window);
        var pointer = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        core.Handle(new WindowMoveStarted(id, pointer));
        return core.Handle(new WindowMoveEnded(id, pointer, bounds));
    }

    public static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    public static WindowManager Load(string text)
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse(text)));
        return core;
    }

    public static WindowManager Configured(string text)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(text)));
        return core;
    }

    private static readonly Rectangle FullHd = new(0, 0, 1920, 1080);

    public static string FlatConfig(string extra = "") =>
        "gaps_in = 0\ngaps_out = 0\nborder_size = 0\n" + extra;

    public static WindowManager Flat(string extra = "") => Configured(FlatConfig(extra));

    public static WindowManager TwoMonitors(string? config = null)
    {
        var core = config is null ? new WindowManager() : new WindowManager(ConfigParser.Parse(config));
        var left = Display(@"\\.\DISPLAY1", "Dell", FullHd);
        var right = Display(@"\\.\DISPLAY2", "Samsung", new Rectangle(1920, 0, 1920, 1080));
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        return core;
    }

    public static WindowAppeared Appeared(
        long window,
        Rectangle? area = null,
        Point? pointer = null,
        Rectangle bounds = default,
        string? windowClass = null) =>
        new(new WindowId(window), area ?? FullHd)
        {
            Pointer = pointer,
            Bounds = bounds,
            Traits = windowClass is null ? WindowTraits.Normal : WindowTraits.Normal with { Class = windowClass },
        };

    public static Outcome Appear(WindowManager core, long window, Rectangle? area = null) =>
        core.Handle(Appeared(window, area));

    public static Outcome Press(WindowManager core, string key, int scanCode = 0) =>
        core.Handle(new KeyPressed(key) { ScanCode = scanCode });

    public static Outcome Release(WindowManager core, string key) =>
        core.Handle(new KeyReleased(key));

    public static Outcome Tap(WindowManager core, string key)
    {
        var outcome = Press(core, key);
        Release(core, key);
        return outcome;
    }
}
