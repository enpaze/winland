using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Winland.Verify.Tests;

internal sealed class VerifyRun
{
    public const string Title = "WinlandVerify";

    private const string BaseConfig =
        """
        gaps_in = 0
        gaps_out = 0
        border_size = 0
        animations = false
        windowrule = ignore, class:*
        windowrule = tile, class:WinlandVerifyWindow, title:WinlandVerify *
        windowrule = workspace 1, class:WinlandVerifyWindow, title:WinlandVerify *

        """;

    private readonly TestContext _output;
    private readonly List<VerifyDaemon> _daemons = [];
    private VerifyWindows? _windows;
    private EventLog? _events;
    private bool _taskbarsShown = true;

    private VerifyRun(TestContext output, VerifyPaths paths, string dotnetRoot, VerifyDaemon daemon)
    {
        _output = output;
        Paths = paths;
        DotnetRoot = dotnetRoot;
        Daemon = daemon;
        _daemons.Add(daemon);
    }

    public VerifyPaths Paths { get; }

    public string DotnetRoot { get; }

    public VerifyDaemon Daemon { get; private set; }

    public VerifyWindows Windows => _windows ??= new VerifyWindows();

    public static void Drive(TestContext output, Action<VerifyRun> body) => Drive(output, "", body);

    public static void Drive(TestContext output, string config, Action<VerifyRun> body)
    {
        _ = PInvoke.SetProcessDpiAwarenessContext((DPI_AWARENESS_CONTEXT)(nint)(-4));
        AssertNoneRunning("before the driver started");
        var userState = UserState.Stamp();
        var taskbarsShown = Taskbars.AllVisible(out _);
        var paths = VerifyPaths.Read();
        var dotnetRoot = VerifyDaemon.ResolveDotnetRoot();
        VerifyRun? run = null;
        Exception? testError = null;
        try
        {
            var daemon = VerifyDaemon.Start(paths, dotnetRoot, BaseConfig + config);
            run = new VerifyRun(output, paths, dotnetRoot, daemon) { _taskbarsShown = taskbarsShown };
            run.Say("config: " + string.Join(" | ", (BaseConfig + config).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
            daemon.WaitUntilReady();
            run.Say("winland is answering on " + daemon.Pipe);
            body(run);
            run.StopEvents();
            if (!run.Daemon.Exited)
            {
                Assert.IsTrue(run.Daemon.TryStopClean(), "winland did not stop cleanly. " + run.Daemon.Logs());
                run.Say("winland stopped cleanly");
            }

            run.AssertOpenWindowsVisible();
            if (taskbarsShown)
                run.Until("the taskbars are visible again", () => Taskbars.AllVisible(out _), 5_000, () => Taskbars.AllVisible(out var taskbars) ? "" : taskbars);
            AssertNoneRunning("after the driver stopped winland");
            Assert.AreEqual(userState, UserState.Stamp());
            run.Say("every test window is visible again, the taskbars are visible, and no winland process remains");
        }
        catch (Exception exception)
        {
            testError = exception;
        }

        var cleanupError = run is null ? null : run.CleanUp();
        if (testError is not null && cleanupError is not null)
            throw new AggregateException(testError, cleanupError);

        if (testError is not null)
            ExceptionDispatchInfo.Capture(testError).Throw();

        if (cleanupError is not null)
            ExceptionDispatchInfo.Capture(cleanupError).Throw();
    }

    public void Say(string text) => _output.WriteLine(text);

    public CliResult Cli(params string[] arguments)
    {
        var result = Daemon.Cli(arguments);
        Say($"winland {string.Join(' ', arguments)} -> {Shorten(result.ToString())}");
        return result;
    }

    public string Ask(params string[] arguments)
    {
        var result = Cli(arguments);
        Assert.IsTrue(result.ExitCode == 0, $"winland {string.Join(' ', arguments)} failed: {result} {Daemon.Logs()}");
        return result.Output.Trim();
    }

    public void Dispatch(string arguments)
    {
        Assert.AreEqual("ok", Ask(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    public JsonElement Json(params string[] arguments)
    {
        var result = Daemon.Cli(["-j", .. arguments]);
        Assert.IsTrue(result.ExitCode == 0, $"winland -j {string.Join(' ', arguments)} failed: {result} {Daemon.Logs()}");
        using var document = JsonDocument.Parse(result.Output);
        return document.RootElement.Clone();
    }

    public JsonElement? Client(HWND hwnd)
    {
        var address = Address(hwnd);
        foreach (var client in Json("get", "windows").EnumerateArray())
        {
            if (string.Equals(client.GetProperty("address").GetString(), address, StringComparison.OrdinalIgnoreCase))
                return client;
        }

        return null;
    }

    public JsonElement ClientOf(HWND hwnd) =>
        Client(hwnd) ?? throw new InvalidOperationException($"{Address(hwnd)} is not a Client. {Json("get", "windows")}");

    public HWND Open(string name, int width = 200, int height = 150, HWND owner = default) =>
        OpenTitled(Title + " " + name, width, height, owner);

    public HWND OpenTitled(string title, int width = 200, int height = 150, HWND owner = default)
    {
        var hwnd = Windows.Open(title, width, height, owner);
        Say($"opened test window '{title}' {Address(hwnd)}");
        return hwnd;
    }

    public HWND OpenManaged(string name, int width = 200, int height = 150)
    {
        var hwnd = Open(name, width, height);
        Until($"'{Title} {name}' is a Client", () => Client(hwnd) is not null);
        return hwnd;
    }

    public (HWND Left, HWND Right) OpenPair()
    {
        var first = OpenManaged("1");
        var second = OpenManaged("2");
        Until("the two windows tile side by side", () => SideBySide(Frame(first), Frame(second), WorkArea(first)));
        return Frame(first).Left <= Frame(second).Left ? (first, second) : (second, first);
    }

    public string Active() => Json("get", "window") is { ValueKind: JsonValueKind.Object } active && active.TryGetProperty("address", out var address) ? address.GetString() ?? "" : "";

    public void Focus(HWND hwnd)
    {
        Dispatch($"focus address:{Address(hwnd)}");
        Until($"{Address(hwnd)} is the active window", () => Active() == Address(hwnd));
    }

    public int ActiveWorkspace() => Json("get", "workspace").GetProperty("id").GetInt32();

    public void Until(string what, Func<bool> condition, int timeoutMs = 10_000, Func<string>? detail = null)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                Say("saw: " + what);
                return;
            }

            Thread.Sleep(50);
        }

        if (condition())
        {
            Say("saw: " + what);
            return;
        }

        Assert.Fail($"Timed out waiting for: {what}. {detail?.Invoke()} clients: {SafeClients()} {Daemon.Logs()}");
    }

    public void Holds(string what, Func<bool> condition, int durationMs = 1_000)
    {
        var deadline = Environment.TickCount64 + durationMs;
        while (Environment.TickCount64 < deadline)
        {
            Assert.IsTrue(condition(), $"This stopped holding: {what}. clients: {SafeClients()} {Daemon.Logs()}");
            Thread.Sleep(50);
        }

        Say("held: " + what);
    }

    public IReadOnlyList<string> Events => _events?.Lines ?? throw new InvalidOperationException("Call FollowEvents first.");

    public void FollowEvents(string command = "events")
    {
        _events ??= EventLog.Follow(Paths, Daemon.Pipe, DotnetRoot, command);
        Say("following winland " + command);
    }

    public void UntilEvent(string prefix, string? contains = null) =>
        Until(
            $"event {prefix}" + (contains is null ? "" : $" with '{contains}'"),
            () => Events.Any(line => line.StartsWith(prefix + ">>", StringComparison.Ordinal) && (contains is null || line.Contains(contains, StringComparison.Ordinal))),
            detail: () => "events: " + string.Join(" | ", Events));

    public void UntilJsonEvent(string name, Func<JsonElement, bool> matches) =>
        Until(
            $"JSON event {name}",
            () => Events.Any(line =>
            {
                using var parsed = JsonDocument.Parse(line);
                var root = parsed.RootElement;
                return root.GetProperty("event").GetString() == name && matches(root);
            }),
            detail: () => "events: " + string.Join(" | ", Events));

    public void StopEvents()
    {
        _events?.Dispose();
        _events = null;
    }

    public void Restart(string? config = null)
    {
        Daemon = Daemon.Restart(config);
        _daemons.Add(Daemon);
        Say("winland started again on " + Daemon.Pipe);
    }

    public void Kill()
    {
        Daemon.Kill();
        Say("killed winland");
    }

    public static string EventAddress(HWND hwnd) => Address(hwnd)[2..];

    public static string Address(HWND hwnd) =>
        "0x" + ((IntPtr)hwnd).ToInt64().ToString("x", CultureInfo.InvariantCulture);

    public static bool Visible(HWND hwnd) => PInvoke.IsWindow(hwnd) && PInvoke.IsWindowVisible(hwnd);

    public static unsafe Box Frame(HWND hwnd)
    {
        Span<byte> value = stackalloc byte[sizeof(RECT)];
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, value).Failed)
            return default;

        return Box.From(MemoryMarshal.Read<RECT>(value));
    }

    public static unsafe Box WorkArea(HWND hwnd)
    {
        var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, ref info))
            return default;

        return Box.From(info.rcWork);
    }

    public static unsafe Box MonitorArea(HWND hwnd)
    {
        var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, ref info))
            return default;

        return Box.From(info.rcMonitor);
    }

    public static bool SideBySide(Box first, Box second, Box work)
    {
        if (!work.Contains(first) || !work.Contains(second))
            return false;

        if (first.Width < 80 || second.Width < 80 || first.Height < 80 || second.Height < 80)
            return false;

        var left = first.Left <= second.Left ? first : second;
        var right = first.Left <= second.Left ? second : first;
        var overlapY = Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Top, right.Top);
        if (overlapY < Math.Min(left.Height, right.Height) * 8 / 10)
            return false;

        var gap = right.Left - left.Right;
        if (gap is < -8 or > 24)
            return false;

        return right.Right - left.Left >= work.Width * 7 / 10 && Math.Max(left.Height, right.Height) >= work.Height * 7 / 10;
    }

    public static bool Stacked(Box first, Box second, Box work)
    {
        if (!work.Contains(first) || !work.Contains(second))
            return false;

        var top = first.Top <= second.Top ? first : second;
        var bottom = first.Top <= second.Top ? second : first;
        var overlapX = Math.Min(top.Right, bottom.Right) - Math.Max(top.Left, bottom.Left);
        if (overlapX < Math.Min(top.Width, bottom.Width) * 8 / 10)
            return false;

        var gap = bottom.Top - top.Bottom;
        return gap is >= -8 and <= 24;
    }

    private void AssertOpenWindowsVisible()
    {
        if (_windows is null)
            return;

        foreach (var hwnd in _windows.Opened)
        {
            if (PInvoke.IsWindow(hwnd))
                Assert.IsTrue(PInvoke.IsWindowVisible(hwnd), $"Test window {Address(hwnd)} stayed hidden after winland stopped.");
        }
    }

    private string SafeClients()
    {
        try
        {
            var result = Daemon.Cli("-j", "get", "windows");
            return Shorten(result.Output.Trim(), 1500);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            return exception.Message;
        }
    }

    private Exception? CleanUp()
    {
        try
        {
            StopEvents();
            foreach (var daemon in _daemons.Where(daemon => !daemon.Exited))
            {
                if (!daemon.TryStopClean())
                {
                    daemon.Kill();
                    daemon.Recover();
                }
            }

            var hidden = _windows?.Opened.Where(hwnd => PInvoke.IsWindow(hwnd) && !PInvoke.IsWindowVisible(hwnd)).ToList() ?? [];
            if (hidden.Count > 0 || !TaskbarsBack(_taskbarsShown))
                _daemons[^1].Recover();

            _windows?.Close();
            if (!TaskbarsBack(_taskbarsShown))
                throw new InvalidOperationException(Taskbars.AllVisible(out var taskbars) ? "" : taskbars);

            var folder = _daemons[0].Folder;
            foreach (var daemon in _daemons)
                daemon.Dispose();

            VerifyDaemon.DeleteTree(folder);
            if (Directory.Exists(folder))
                throw new InvalidOperationException($"Temporary files remain at {folder}.");

            AssertNoneRunning("after cleanup");
            if (hidden.Count > 0)
                throw new InvalidOperationException("Test windows were left hidden: " + string.Join(", ", hidden.Select(Address)));

            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static bool TaskbarsBack(bool shown)
    {
        if (!shown)
            return true;

        var deadline = Environment.TickCount64 + 5_000;
        while (!Taskbars.AllVisible(out _))
        {
            if (Environment.TickCount64 >= deadline)
                return false;

            Thread.Sleep(100);
        }

        return true;
    }

    private static void AssertNoneRunning(string when)
    {
        var deadline = Environment.TickCount64 + 5_000;
        Process[] running = [];
        while (true)
        {
            foreach (var process in running)
                process.Dispose();

            running = Process.GetProcessesByName("winland");
            if (running.Length == 0)
                return;

            if (Environment.TickCount64 >= deadline)
                break;

            Thread.Sleep(100);
        }

        try
        {
            Assert.Fail($"A winland process is running {when}: " + string.Join(", ", running.Select(process => process.Id.ToString(CultureInfo.InvariantCulture))));
        }
        finally
        {
            foreach (var process in running)
                process.Dispose();
        }
    }

    private static string Shorten(string text, int length = 600) =>
        text.Length <= length ? text : text[..length] + "...";
}

internal readonly record struct Box(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public bool Contains(Box window) =>
        window.Left >= Left - 4
        && window.Top >= Top - 4
        && window.Right <= Right + 4
        && window.Bottom <= Bottom + 4;

    public bool Near(Box other, int slack = 8) =>
        Math.Abs(Left - other.Left) <= slack
        && Math.Abs(Top - other.Top) <= slack
        && Math.Abs(Right - other.Right) <= slack
        && Math.Abs(Bottom - other.Bottom) <= slack;

    public static Box From(RECT rect) => new(rect.left, rect.top, rect.right, rect.bottom);

    public override string ToString() => $"{Left},{Top} {Width}x{Height}";
}
