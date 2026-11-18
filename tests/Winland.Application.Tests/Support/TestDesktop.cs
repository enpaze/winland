using System.Text.Json;

namespace Winland.Application.Tests;

internal sealed class TestDesktop
{
    internal const string Config = """
        general {
            gaps_in = 5
            gaps_out = 20
            border_size = 2
            col.active_border = rgba(33ccffee)
            col.inactive_border = rgba(595959aa)
            layout = dwindle
        }
        decoration {
            rounding = 10
        }
        animations {
            enabled = false
        }
        master {
            new_status = master
        }
        scrolling {
            column_width = 0.5
            explicit_column_widths = 0.25, 0.333, 0.5, 0.667, 0.75, 1.0
        }
        input {
            follow_mouse = 1
        }
        windowrule = float, class:wr_kitty
        windowrule = size 200 200, class:wr_kitty
        """;

    internal static readonly Rectangle First = new(0, 0, 1920, 1080);

    internal static readonly Rectangle Second = new(1920, 0, 1920, 1080);

    private readonly Dictionary<string, WindowId> _windows = new(StringComparer.Ordinal);
    private long _next = 1;

    private readonly Dictionary<WindowId, Rectangle> _placed = [];
    private readonly Dictionary<ClientId, WindowId> _clients = [];
    private readonly List<Monitor> _monitors = [];
    private State _state = new([]);
    private string _text;

    internal TestDesktop(string extra = "")
    {
        _text = Config + "\n" + extra;
        WindowManager = new WindowManager();
        WindowManager.ReadConfig = () => new ConfigRead(ConfigParser.Parse(_text));
        WindowManager.DescribeWindow = window =>
            (window ?? UnderPointer) is WindowId read && _described.TryGetValue(read, out var described) ? described : null;
        Handle(new Configure(ConfigParser.Parse(_text)));
        Pointer = new Point(960, 540);
        AddMonitor("HEADLESS-2", First);
        Handle(new PointerMoved(Pointer));
    }

    internal WindowManager WindowManager { get; }

    internal void ReloadWithoutExtra() => _text = Config;

    internal void Set(string keyword, string value)
    {
        var warnings = ConfigParser.Parse(_text).Warnings.Count;
        _text += "\n" + keyword + " = " + value + "\n";
        var document = ConfigParser.Parse(_text);
        Assert.AreEqual(warnings, document.Warnings.Count);
        Handle(new Configure(document));
    }

    internal Point Pointer { get; private set; }

    private readonly Dictionary<WindowId, WindowAppeared> _described = [];

    internal WindowId? UnderPointer { get; set; }

    internal void Vanish(WindowId window) => _described.Remove(window);

    internal WindowId Spawn(string windowClass, string title = "", WindowTraits? traits = null, bool shownMaximized = false, WindowId? owner = null)
    {
        var id = new WindowId(_next++);
        _windows[windowClass] = id;
        var area = FocusedArea();
        Handle(new WindowAppeared(id, area)
        {
            Pointer = Pointer,
            Traits = (traits ?? WindowTraits.Normal) with { Class = windowClass, Title = title.Length == 0 ? windowClass : title },
            Bounds = new Rectangle(area.X + (area.Width / 2) - 320, area.Y + (area.Height / 2) - 240, 640, 480),
            ProcessId = 1000 + id.Value,
            ShownMaximized = shownMaximized,
            Owner = owner,
        });
        return id;
    }

    internal WindowId Window(string windowClass) => _windows[windowClass];

    internal void Close(string windowClass) => Handle(new WindowClosed(_windows[windowClass]));

    internal void CloseAll()
    {
        foreach (var client in Clients())
            Handle(new WindowClosed(IdOf(client)));
    }

    internal void CloseActive() => Handle(new WindowClosed(ActiveId()));

    internal void MovePointer(int x, int y)
    {
        Pointer = new Point(x, y);
        Handle(new PointerMoved(Pointer));
    }

    internal void Place(string windowClass, int x, int y, int width, int height) =>
        Place(_windows[windowClass], x, y, width, height);

    internal void Place(WindowId window, int x, int y, int width, int height)
    {
        var bounds = new Rectangle(x, y, width, height);
        Handle(new WindowMoveStarted(window, Pointer));
        Handle(new WindowMoveEnded(window, Pointer, bounds));

        var client = Clients().Single(candidate => IdOf(candidate) == window);
        var at = client.GetProperty("at");
        var size = client.GetProperty("size");
        _placed[window] = new Rectangle(at[0].GetInt32(), at[1].GetInt32(), size[0].GetInt32(), size[1].GetInt32());
    }

    internal void MoveTo(WindowId window, int x, int y)
    {
        var size = Placed(window);
        Place(window, x, y, size.Width, size.Height);
    }

    internal void Click(string button = "mouse:272")
    {
        Handle(new PointerButtonPressed(button) { Position = Pointer });
        Handle(new PointerButtonReleased(button) { Position = Pointer });
    }

    internal void AddMonitor(string name, Rectangle bounds)
    {
        _monitors.Add(Display(name, name, bounds));
        Handle(new MonitorsChanged([.. _monitors]));
    }

    internal void ReportEnabled(bool enabled)
    {
        for (var i = 0; i < _monitors.Count; i++)
            _monitors[i] = _monitors[i] with { Enabled = enabled };

        Handle(new MonitorsChanged([.. _monitors]));
    }

    internal void RemoveMonitor(string name)
    {
        _monitors.RemoveAll(monitor => monitor.Name == name);
        Handle(new MonitorsChanged([.. _monitors]));
    }

    internal Outcome Handle(Input input)
    {
        if (input is WindowAppeared appeared)
            _described[appeared.Window] = appeared;
        else if (input is WindowClosed closed)
            _described.Remove(closed.Window);

        var outcome = WindowManager.Handle(input);
        Follow(outcome);
        return outcome;
    }

    internal string Send(string line)
    {
        var reply = WindowManager.Execute(line);
        Follow(reply.Effect);
        return reply.Text;
    }

    private void Follow(Outcome outcome, WindowId? echoed = null)
    {
        Keep(outcome);
        if (outcome.Warp is WarpTo warp)
            Pointer = warp.Position;

        if (outcome.Focus is WindowId focus && focus != echoed)
            Follow(WindowManager.Handle(new WindowFocused(focus, FocusCause.Requested)), focus);
    }

    internal Rectangle Placed(string windowClass) => Placed(_windows[windowClass]);

    internal Rectangle Placed(WindowId window) =>
        _placed.TryGetValue(window, out var bounds) ? bounds : throw new InvalidOperationException($"Window {window.Value} was never placed.");

    internal string Dispatch(string dispatcher, string arguments = "") =>
        Send(arguments.Length == 0 ? $"dispatch {dispatcher}" : $"dispatch {dispatcher} {arguments}");

    internal void Ok(string dispatcher, string arguments = "") =>
        Assert.AreEqual("ok", Dispatch(dispatcher, arguments));

    internal JsonElement ActiveWindow() => Json(Send("j/activewindow"));

    internal JsonElement[] Clients() => [.. Json(Send("j/clients")).EnumerateArray()];

    internal JsonElement Client(string windowClass) =>
        Clients().Single(client => client.GetProperty("class").GetString() == windowClass);

    internal JsonElement ActiveWorkspace() => Json(Send("j/activeworkspace"));

    internal JsonElement[] Workspaces() => [.. Json(Send("j/workspaces")).EnumerateArray()];

    internal static string At(JsonElement client) => Pair(client.GetProperty("at"));

    internal static string SizeOf(JsonElement client) => Pair(client.GetProperty("size"));

    internal static string ClassOf(JsonElement client) => client.GetProperty("class").GetString() ?? "";

    internal static int Fullscreen(JsonElement client) => client.GetProperty("fullscreen").GetInt32();

    internal static int FullscreenClient(JsonElement client) => client.GetProperty("fullscreenClient").GetInt32();

    internal static bool Floating(JsonElement client) => client.GetProperty("floating").GetBoolean();

    internal static bool Pinned(JsonElement client) => client.GetProperty("pinned").GetBoolean();

    internal static int WorkspaceId(JsonElement client) =>
        client.GetProperty("workspace").TryGetProperty("id", out var id) ? id.GetInt32() : 0;

    internal string ActiveClass() => ClassOf(ActiveWindow());

    internal (string At, string Size) ActivePlace()
    {
        var bounds = Placed(ActiveId());
        return ($"{bounds.X},{bounds.Y}", $"{bounds.Width},{bounds.Height}");
    }

    internal (string At, string Size) PlaceOf(string windowClass)
    {
        var bounds = Placed(windowClass);
        return ($"{bounds.X},{bounds.Y}", $"{bounds.Width},{bounds.Height}");
    }

    internal void ExpectActive(string at, string size) => Assert.AreEqual((at, size), ActivePlace());

    internal WindowId ActiveId() => IdOf(ActiveWindow());

    internal static WindowId IdOf(JsonElement client)
    {
        var address = client.GetProperty("address").GetString() ?? "";
        return new WindowId(Convert.ToInt64(address[2..], 16));
    }

    private void Keep(Outcome outcome)
    {
        foreach (var client in outcome.State.Clients)
            _clients[client.Id] = client.Window;
        foreach (var client in outcome.State.Hidden)
            _clients[client.Id] = client.Window;
        foreach (var placement in outcome.Placements)
        {
            if (_clients.TryGetValue(placement.Client, out var window))
                _placed[window] = placement.Bounds;
        }

        _state = outcome.State;
    }

    internal Rectangle FocusedArea()
    {
        foreach (var screen in _state.Screens)
        {
            if (screen.Focused)
                return screen.Monitor.WorkArea;
        }

        return First;
    }

    private static string Pair(JsonElement pair) =>
        $"{pair[0].GetInt32()},{pair[1].GetInt32()}";
}
