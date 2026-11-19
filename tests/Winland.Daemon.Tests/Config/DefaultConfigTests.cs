namespace Winland.Daemon.Tests;

[TestClass]
public class DefaultConfigTests
{
    private const string Binds = """
        Win|W|close|
        Win|Q|close|
        Win|J|split|
        Win|T|float|
        Win|F|fullscreen|
        Win Alt|F|maximize|
        Win|O|float|
        Win|O|pin|
        Win|K|layout|next
        Win Alt|K|layout|prev
        Win|Left|focus|left
        Win|Right|focus|right
        Win|Up|focus|up
        Win|Down|focus|down
        Win Shift|Left|swap|left
        Win Shift|Right|swap|right
        Win Shift|Up|swap|up
        Win Shift|Down|swap|down
        Win Ctrl Shift|Left|move|mon:left
        Win Ctrl Shift|Right|move|mon:right
        Win Ctrl Shift|Up|move|mon:up
        Win Ctrl Shift|Down|move|mon:down
        Win Alt Shift|Left|move-workspace|left
        Win Alt Shift|Right|move-workspace|right
        Win Alt Shift|Up|move-workspace|up
        Win Alt Shift|Down|move-workspace|down
        Win|1|workspace|1
        Win|2|workspace|2
        Win|3|workspace|3
        Win|4|workspace|4
        Win|5|workspace|5
        Win|6|workspace|6
        Win|7|workspace|7
        Win|8|workspace|8
        Win|9|workspace|9
        Win|0|workspace|10
        Win Shift|1|move|1
        Win Shift|2|move|2
        Win Shift|3|move|3
        Win Shift|4|move|4
        Win Shift|5|move|5
        Win Shift|6|move|6
        Win Shift|7|move|7
        Win Shift|8|move|8
        Win Shift|9|move|9
        Win Shift|0|move|10
        Win Alt Shift|1|move|1 stay
        Win Alt Shift|2|move|2 stay
        Win Alt Shift|3|move|3 stay
        Win Alt Shift|4|move|4 stay
        Win Alt Shift|5|move|5 stay
        Win Alt Shift|6|move|6 stay
        Win Alt Shift|7|move|7 stay
        Win Alt Shift|8|move|8 stay
        Win Alt Shift|9|move|9 stay
        Win Alt Shift|0|move|10 stay
        Win|S|scratchpad|
        Win|grave|scratchpad|
        Win Alt|S|move|special stay
        Win Shift|grave|move|special stay
        Win|Tab|workspace|next
        Win Shift|Tab|workspace|prev
        Win Ctrl|Tab|workspace|last
        Alt Ctrl Shift|Tab|focus-monitor|prev
        Win Shift|minus|resize|0 -100
        Win Shift|equal|resize|0 100
        Win Alt|minus|resize|-25 0
        Win Alt|equal|resize|25 0
        Win Ctrl|minus|resize|-300 0
        Win Ctrl|equal|resize|300 0
        Win Alt Shift|minus|resize|0 -25
        Win Alt Shift|equal|resize|0 25
        Win Ctrl Shift|minus|resize|0 -300
        Win Ctrl Shift|equal|resize|0 300
        Win|mouse_down|workspace|next
        Win|mouse_up|workspace|prev
        Win|mouse:left|move|
        Win|mouse:right|resize|
        Win|G|group|
        Win Alt|G|leave-group|
        Win Alt|Left|join|left
        Win Alt|Right|join|right
        Win Alt|Up|join|up
        Win Alt|Down|join|down
        Win Alt|Tab|tab|next
        Win Alt Shift|Tab|tab|prev
        Win Ctrl|Left|tab|prev
        Win Ctrl|Right|tab|next
        Win Alt|mouse_down|tab|next
        Win Alt|mouse_up|tab|prev
        Win Alt|1|tab|1
        Win Alt|2|tab|2
        Win Alt|3|tab|3
        Win Alt|4|tab|4
        Win Alt|5|tab|5
        Win|Return|exec|wt
        Win Shift|Return|exec|start https://
        Win Shift|B|exec|start https://
        Win Shift|F|exec|explorer
        Win Shift|N|exec|notepad
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
        const string assignment = "$mod = Win";
        Assert.Contains(assignment, DefaultConfig.Text, StringComparison.Ordinal);

        var binds = ConfigParser.Parse(DefaultConfig.Text.Replace(assignment, "$mod = Ctrl", StringComparison.Ordinal))
            .Binds.OfType<Bind>();

        foreach (var bind in binds)
{
    Assert.IsFalse(bind.Modifiers.HasFlag(Modifier.Win), BindText.Line(bind));
}
    }

    [TestMethod]
    public void OnlyWinOHasTwoDefaultBinds()
    {
        var shared = ConfigParser.Parse(DefaultConfig.Text).Binds.OfType<Bind>()
            .GroupBy(bind => (bind.Modifiers, bind.Key))
            .Where(chord => chord.Count() > 1)
            .Select(chord => $"{chord.Key.Modifiers}|{chord.Key.Key}|{string.Join(' ', chord.Select(bind => bind.Dispatcher))}");

        Assert.AreSequenceEqual(["Win|O|float pin"], shared);
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

            File.WriteAllText(path, "bind = Win+Q, close\n");
            Assert.IsFalse(DefaultConfig.WriteIfMissing(path));
            Assert.AreEqual("bind = Win+Q, close\n", File.ReadAllText(path));
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
        if (modifiers.HasFlag(Modifier.Win))
            names.Add("Win");
        if (modifiers.HasFlag(Modifier.Alt))
            names.Add("Alt");
        if (modifiers.HasFlag(Modifier.Ctrl))
            names.Add("Ctrl");
        if (modifiers.HasFlag(Modifier.Shift))
            names.Add("Shift");
        return string.Join(' ', names);
    }
}
