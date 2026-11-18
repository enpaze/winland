using System.Collections;
using System.Reflection;

namespace Winland.Application.Tests;

[TestClass]
public class WindowStateTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void ClosingAWindowLeavesNoPerWindowStateBehind()
    {
        var core = new WindowManager(ConfigParser.Parse("""
            windowrule = float, class:stick
            windowrule = pin, class:stick
            windowrule = opacity 0.8, class:stick
            windowrule = no_focus, class:quiet
            """));
        core.Restore(new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(9), new WorkspaceId("4"), MonitorId.Primary, true, "gone", "", 0),
        ]));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Executable = @"C:\apps\stick.exe",
            Traits = WindowTraits.Normal with { Class = "stick" },
        });
        core.Handle(new WindowAppeared(new WindowId(3), Area) { Traits = WindowTraits.Normal with { Class = "quiet" } });
        core.Handle(new WindowAppeared(new WindowId(4), Area) { ProcessId = 100, Traits = WindowTraits.Normal with { Class = "kitty" } });
        core.Handle(new WindowAppeared(new WindowId(5), Area)
        {
            ProcessId = 205,
            AncestorProcessIds = [100],
            Traits = WindowTraits.Normal with { Class = "mpv" },
        });
        core.Handle(new WindowAppeared(new WindowId(6), Area));
        core.Handle(new WindowAppeared(new WindowId(7), Area) { Owner = new WindowId(6) });
        core.Handle(new WindowEnabledChanged(new WindowId(6), Enabled: false));
        core.Handle(new WindowAppeared(new WindowId(8), Area));
        core.Handle(new WindowAppeared(new WindowId(10), Area));
        core.Handle(new WindowAppeared(new WindowId(11), Area));
        core.Handle(new Dispatch("focus", "address:0xa"));
        core.Handle(new Dispatch("group"));
        core.Handle(new Dispatch("focus", "address:0xb"));
        core.Handle(new Dispatch("join", "u"));
        core.Handle(new WindowAppeared(new WindowId(12), Area));
        core.Handle(new WindowAppeared(new WindowId(13), Area) { ShownMaximized = true });
        core.Handle(new WindowTitleChanged(new WindowId(13), "renamed"));
        core.Handle(new Dispatch("focus", "address:0xc"));
        core.Handle(new Dispatch("move", "special:magic"));
        core.Handle(new WindowFlashed(new WindowId(3)));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new Dispatch("workspace", "1"));
        core.Handle(new Dispatch("focus", "address:0x8"));
        core.Handle(new Dispatch("fullscreen"));
        core.Handle(new Configure(ConfigParser.Parse("layout = scrolling\nwindowrule = float, class:stick")));
        core.Handle(new WindowAppeared(new WindowId(14), Area) { Traits = WindowTraits.Normal with { Dialog = true } });
        core.Handle(new Dispatch("focus", "address:0x8"));
        core.Handle(new Dispatch("fullscreen"));
        core.Handle(new Dispatch("fullscreen", "on"));
        core.Handle(new PointerMoved(new Point(100, 100)) { TimeMs = 1_000 });
        core.Handle(new PointerMoved(new Point(1800, 900)) { TimeMs = 1_100 });
        core.Handle(new WindowMoveStarted(new WindowId(1), new Point(100, 100)));
        core.Handle(new WindowFocused(new WindowId(77), FocusCause.User));

        WindowId[] closed = [new(1), new(2), new(3), new(5), new(4), new(6), new(14), new(8), new(10), new(11), new(12), new(13), new(9), new(77)];
        var forgotten = new List<WindowId>();
        foreach (var window in closed)
        {
            core.Handle(new WindowClosed(window));
            forgotten.Add(window);
            var left = Leftovers(core, [.. forgotten]);
            Assert.IsTrue(left.Count == 0, "Per-window state kept a closed window: " + string.Join(", ", left));
        }

        var kept = Leftovers(core, closed);
        Assert.IsTrue(kept.Count == 0, "Per-window state kept a closed window: " + string.Join(", ", kept));
    }

    private static List<string> Leftovers(WindowManager core, WindowId[] closed)
    {
        var kept = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Walk("core", core, closed, seen, kept);
        return [.. kept.Select(path => path["core.".Length..])];
    }

    private static void Walk(string path, object? value, WindowId[] closed, HashSet<object> seen, List<string> kept)
    {
        if (value is null or string or Delegate or Type)
            return;

        if (value is WindowId window)
        {
            if (closed.Contains(window))
                kept.Add($"{path} holds {window.Value}");

            return;
        }

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || (!type.IsValueType && !seen.Add(value)))
            return;

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                Walk($"{path}.Keys", entry.Key, closed, seen, kept);
                Walk($"{path}[{(entry.Key is WindowId id ? id.Value : entry.Key)}]", entry.Value, closed, seen, kept);
            }

            return;
        }

        if (!IsWinland(type) && !type.FullName!.StartsWith("System.ValueTuple", StringComparison.Ordinal))
        {
            if (value is IEnumerable items)
            {
                foreach (var item in items)
                    Walk($"{path}[]", item, closed, seen, kept);
            }

            return;
        }

        for (var owner = type; owner is not null && (IsWinland(owner) || owner == type); owner = owner.BaseType)
        {
            foreach (var field in owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var name = field.Name.StartsWith('<') ? field.Name[1..field.Name.IndexOf('>')] : field.Name;
                Walk($"{path}.{name}", field.GetValue(value), closed, seen, kept);
            }
        }
    }

    private static bool IsWinland(Type type) => type.Assembly.GetName().Name!.StartsWith("Winland", StringComparison.Ordinal);
}
