namespace Winland.Daemon.Tests;

[TestClass]
public class DefaultConfigTests
{
    private const string Binds = """
        SUPER|W|close|
        SUPER|Q|close|
        SUPER|J|split|
        SUPER|T|float|
        SUPER|F|fullscreen|
        SUPER ALT|F|maximize|
        SUPER|O|float|
        SUPER|O|pin|
        SUPER|K|layout|next
        SUPER ALT|K|layout|prev
        SUPER|Left|focus|left
        SUPER|Right|focus|right
        SUPER|Up|focus|up
        SUPER|Down|focus|down
        SUPER SHIFT|Left|swap|left
        SUPER SHIFT|Right|swap|right
        SUPER SHIFT|Up|swap|up
        SUPER SHIFT|Down|swap|down
        SUPER CTRL SHIFT|Left|move|mon:left
        SUPER CTRL SHIFT|Right|move|mon:right
        SUPER CTRL SHIFT|Up|move|mon:up
        SUPER CTRL SHIFT|Down|move|mon:down
        SUPER ALT SHIFT|Left|move-workspace|left
        SUPER ALT SHIFT|Right|move-workspace|right
        SUPER ALT SHIFT|Up|move-workspace|up
        SUPER ALT SHIFT|Down|move-workspace|down
        SUPER|1|workspace|1
        SUPER|2|workspace|2
        SUPER|3|workspace|3
        SUPER|4|workspace|4
        SUPER|5|workspace|5
        SUPER|6|workspace|6
        SUPER|7|workspace|7
        SUPER|8|workspace|8
        SUPER|9|workspace|9
        SUPER|0|workspace|10
        SUPER SHIFT|1|move|1
        SUPER SHIFT|2|move|2
        SUPER SHIFT|3|move|3
        SUPER SHIFT|4|move|4
        SUPER SHIFT|5|move|5
        SUPER SHIFT|6|move|6
        SUPER SHIFT|7|move|7
        SUPER SHIFT|8|move|8
        SUPER SHIFT|9|move|9
        SUPER SHIFT|0|move|10
        SUPER ALT SHIFT|1|move|1 stay
        SUPER ALT SHIFT|2|move|2 stay
        SUPER ALT SHIFT|3|move|3 stay
        SUPER ALT SHIFT|4|move|4 stay
        SUPER ALT SHIFT|5|move|5 stay
        SUPER ALT SHIFT|6|move|6 stay
        SUPER ALT SHIFT|7|move|7 stay
        SUPER ALT SHIFT|8|move|8 stay
        SUPER ALT SHIFT|9|move|9 stay
        SUPER ALT SHIFT|0|move|10 stay
        SUPER|S|scratchpad|
        SUPER|grave|scratchpad|
        SUPER ALT|S|move|special stay
        SUPER SHIFT|grave|move|special stay
        SUPER|Tab|workspace|next
        SUPER SHIFT|Tab|workspace|prev
        SUPER CTRL|Tab|workspace|last
        ALT CTRL SHIFT|Tab|focus-monitor|prev
        SUPER SHIFT|minus|resize|0 -100
        SUPER SHIFT|equal|resize|0 100
        SUPER ALT|minus|resize|-25 0
        SUPER ALT|equal|resize|25 0
        SUPER CTRL|minus|resize|-300 0
        SUPER CTRL|equal|resize|300 0
        SUPER ALT SHIFT|minus|resize|0 -25
        SUPER ALT SHIFT|equal|resize|0 25
        SUPER CTRL SHIFT|minus|resize|0 -300
        SUPER CTRL SHIFT|equal|resize|0 300
        SUPER|mouse_down|workspace|next
        SUPER|mouse_up|workspace|prev
        SUPER|mouse:left|move|
        SUPER|mouse:right|resize|
        SUPER|G|group|
        SUPER ALT|G|leave-group|
        SUPER ALT|Left|join|left
        SUPER ALT|Right|join|right
        SUPER ALT|Up|join|up
        SUPER ALT|Down|join|down
        SUPER ALT|Tab|tab|next
        SUPER ALT SHIFT|Tab|tab|prev
        SUPER CTRL|Left|tab|prev
        SUPER CTRL|Right|tab|next
        SUPER ALT|mouse_down|tab|next
        SUPER ALT|mouse_up|tab|prev
        SUPER ALT|1|tab|1
        SUPER ALT|2|tab|2
        SUPER ALT|3|tab|3
        SUPER ALT|4|tab|4
        SUPER ALT|5|tab|5
        SUPER|Return|exec|wt
        SUPER SHIFT|Return|exec|start https://
        SUPER SHIFT|B|exec|start https://
        SUPER SHIFT|F|exec|explorer
        SUPER SHIFT|N|exec|notepad
        """;

    [TestMethod]
    public void TheEmbeddedDefaultParsesWithNoWarnings()
    {
        var document = ConfigParser.Parse(DefaultConfig.Text, dispatchers: WindowManager.DispatcherNames);

        Assert.IsTrue(
            document.Warnings.Count == 0,
            string.Join('\n', document.Warnings.Select(warning => $"{warning.Line}: {warning.Message}")));
        Assert.AreSequenceEqual(Lines(Binds), document.Binds.Select(Describe));
        var gesture = Assert.ContainsSingle(document.Gestures);
        Assert.AreEqual(new TrackpadGesture(3, GestureAxis.Horizontal), gesture);
    }

    [TestMethod]
    public void EveryDefaultBindReadsBackFromTheLineBindsPrints()
    {
        var binds = ConfigParser.Parse(DefaultConfig.Text).Binds.OfType<Bind>().ToArray();

        var again = ConfigParser.Parse(CommandProtocol.FormatBindsText(binds));

        Assert.IsEmpty(again.Warnings);
        Assert.AreSequenceEqual(binds, again.Binds);
    }

    [TestMethod]
    public void EveryDefaultBindSaysWhatItDoes()
    {
        var binds = ConfigParser.Parse(DefaultConfig.Text).Binds.OfType<Bind>();

        foreach (var bind in binds)
{
    Assert.IsFalse(string.IsNullOrWhiteSpace(bind.Description), BindText.Line(bind));
}
    }

    [TestMethod]
    public void OneAssignmentChangesTheMainModifierOfEveryDefaultBind()
    {
        const string assignment = "$mainMod = SUPER";
        Assert.Contains(assignment, DefaultConfig.Text, StringComparison.Ordinal);

        var binds = ConfigParser.Parse(DefaultConfig.Text.Replace(assignment, "$mainMod = CTRL", StringComparison.Ordinal))
            .Binds.OfType<Bind>();

        foreach (var bind in binds)
{
    Assert.IsFalse(bind.Modifiers.HasFlag(Modifier.Super), BindText.Line(bind));
}
    }

    [TestMethod]
    public void OnlyWinOHasTwoDefaultBinds()
    {
        var shared = ConfigParser.Parse(DefaultConfig.Text).Binds.OfType<Bind>()
            .GroupBy(bind => (bind.Modifiers, bind.Key))
            .Where(chord => chord.Count() > 1)
            .Select(chord => $"{chord.Key.Modifiers}|{chord.Key.Key}|{string.Join(' ', chord.Select(bind => bind.Dispatcher))}");

        Assert.AreSequenceEqual(["Super|O|float pin"], shared);
    }

    [TestMethod]
    public void TheDefaultParsesWithNoWarningsOnAGermanLayout()
    {
        var german = KeyLayout.From(
            new Dictionary<int, string>
            {
                [0x30] = "0",
                [0x51] = "q",
                [0x59] = "z",
                [0x5A] = "y",
                [0xBA] = "ö",
                [0xBC] = ",",
                [0xBD] = "ß",
                [0xBF] = "-",
                [0xDB] = "ü",
                [0xDC] = "#",
                [0xDE] = "ä",
            },
            hasAltGr: true,
            deadKeys: [0xBB, 0xC0]);

        var document = ConfigParser.Parse(DefaultConfig.Text, layout: german, dispatchers: WindowManager.DispatcherNames);

        Assert.IsTrue(
            document.Warnings.Count == 0,
            string.Join('\n', document.Warnings.Select(warning => $"{warning.Line}: {warning.Message}")));
        Assert.AreEqual(Lines(Binds).Count(), document.Binds.Count);
    }

    [TestMethod]
    public void TheDefaultIsWrittenOnlyWhenTheFileIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "winland-default-config-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "winland", "winland.conf");
        try
        {
            Assert.IsTrue(DefaultConfig.WriteIfMissing(path));
            Assert.AreEqual(DefaultConfig.Text, File.ReadAllText(path));

            File.WriteAllText(path, "");
            Assert.IsTrue(DefaultConfig.WriteIfMissing(path));
            Assert.AreEqual(DefaultConfig.Text, File.ReadAllText(path));

            File.WriteAllText(path, "bind = SUPER, Q, close\n");
            Assert.IsFalse(DefaultConfig.WriteIfMissing(path));
            Assert.AreEqual("bind = SUPER, Q, close\n", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Describe(BindDirective directive)
    {
        var bind = Assert.IsExactInstanceOfType<Bind>(directive);
        Assert.Contains(bind.Dispatcher, WindowManager.DispatcherNames, StringComparer.OrdinalIgnoreCase);
        return $"{Modifiers(bind.Modifiers)}|{bind.Key}|{bind.Dispatcher}|{bind.Arguments}";
    }

    private static string Modifiers(Modifier modifiers)
    {
        var names = new List<string>();
        if (modifiers.HasFlag(Modifier.Super))
            names.Add("SUPER");
        if (modifiers.HasFlag(Modifier.Alt))
            names.Add("ALT");
        if (modifiers.HasFlag(Modifier.Ctrl))
            names.Add("CTRL");
        if (modifiers.HasFlag(Modifier.Shift))
            names.Add("SHIFT");
        return string.Join(' ', names);
    }
}
