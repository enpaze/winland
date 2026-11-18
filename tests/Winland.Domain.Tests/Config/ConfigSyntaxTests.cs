namespace Winland.Domain.Tests;

[TestClass]
public class ConfigSyntaxTests
{

    [TestMethod]
    public void AnimationOptionsAcceptABooleanPrefix()
    {
        var document = ConfigParser.Parse("animations = yes, please :)\n");

        Assert.IsEmpty(document.Warnings);
        Assert.IsTrue(AnimationOptions.From(document).Enabled);
    }

    [TestMethod]
    public void AnEscapedHashAtTheStartStillCommentsOutATrailingBackslash()
    {
        var document = ConfigParser.Parse("  ## path\\\ngaps_in = 4\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(new Setting("gaps_in", "4"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    [DataRow("Win+Shft")]
    [DataRow("Winunknown")]
    public void UnrecognisedLettersInModifiersDropTheBind(string modifiers)
    {
        var document = ConfigParser.Parse($"bind = {modifiers}+Q, close\n");

        Assert.ContainsSingle(document.Warnings);
        Assert.IsEmpty(document.Binds);
    }

    [TestMethod]
    [DataRow("t")]
    [DataRow("i")]
    [DataRow("s")]
    [DataRow("o")]
    [DataRow("c")]
    [DataRow("g")]
    [DataRow("u")]
    [DataRow("d")]
    [DataRow("m")]
    public void ABindKeywordWithAFlagIsAnUnsupportedKeyword(string flag)
    {
        var document = ConfigParser.Parse($"bind{flag} = Win+Q, close\n");

        Assert.StartsWith($"Unsupported keyword 'bind{flag}'.", Assert.ContainsSingle(document.Warnings).Message, StringComparison.Ordinal);
        Assert.IsEmpty(document.Binds);
    }

    [TestMethod]
    public void AnUnknownBindKeywordWarnsAndTheBindBesideItStillApplies()
    {
        var document = ConfigParser.Parse("bindp = Win+Q, close\nbind = Win+L, resize 10 0\n");

        var warning = Assert.ContainsSingle(document.Warnings);
        Assert.AreEqual(1, warning.Line);
        Assert.AreEqual("Unsupported keyword 'bindp'. Did you mean 'bind'?", warning.Message);
        Assert.AreEqual("L", Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds)).Key);
    }

    [TestMethod]
    public void VariableExpansionThatExceeds64KWarnsAndSkipsTheLine()
    {
        var document = ConfigParser.Parse($"$large = {new string('x', 32768)}\nexec-once = $large$large$large\ngaps_in = 4\n");

        Assert.ContainsSingle(document.Warnings);
        Assert.DoesNotContain(setting => setting.Keyword == "exec", document.Settings);
        Assert.Contains(new Setting("gaps_in", "4"), document.Settings);
    }

    [TestMethod]
    public void ADoublingVariableWarnsWithoutExhaustingMemory()
    {
        var document = ConfigParser.Parse("$a = $a$a\nexec-once = $a\ngaps_in = 4\n");

        Assert.ContainsSingle(document.Warnings);
        Assert.AreEqual(new Setting("gaps_in", "4"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void AConditionMarkerIsACommentAndEveryLineApplies()
    {
        var document = ConfigParser.Parse("""
            # hyprlang if MISSING
            gaps_in = 3
            #hyprlang else # the other branch
            gaps_out = 5
            # hyprlang endif
            """);

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual([new Setting("gaps_in", "3"), new Setting("gaps_out", "5")], document.Settings);
    }

    [TestMethod]
    [DataRow("general {", "Malformed line.")]
    [DataRow("}", "Malformed line.")]
    [DataRow("gaps_in = {{2 + 3}}", "Invalid value '{{2 + 3}}' for gaps_in.")]
    [DataRow("device[mouse]:sensitivity = 1", "Unsupported keyword 'device[mouse]:sensitivity'.")]
    public void AHyprlandOnlyLineWarnsAndTheNextLineStillApplies(string line, string warning)
    {
        var document = ConfigParser.Parse($"{line}\ngaps_out = 4\n");

        Assert.AreEqual(new ConfigWarning(1, warning), Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("gaps_out", "4"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void TheLinesInsideABraceBlockAreOrdinaryLines()
    {
        var document = ConfigParser.Parse("general {\n    gaps_in = 5\n}\n");

        Assert.AreSequenceEqual([new ConfigWarning(1, "Malformed line."), new ConfigWarning(3, "Malformed line.")], document.Warnings);
        Assert.AreEqual(new Setting("gaps_in", "5"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("yes")]
    [DataRow("enable")]
    [DataRow("enabled")]
    [DataRow("set")]
    public void ANoErrorCommentHidesNoWarning(string word)
    {
        var document = ConfigParser.Parse($"""
            # hyprlang noerror {word}
            ghostkeyword = 1
            gaps_in = 3
            # hyprlang noerror false
            ghostkeyword = 2
            """);

        Assert.AreSequenceEqual(
            [new ConfigWarning(2, "Unsupported keyword 'ghostkeyword'."), new ConfigWarning(5, "Unsupported keyword 'ghostkeyword'.")],
            document.Warnings);
        Assert.AreEqual(new Setting("gaps_in", "3"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void SourceExpandsTildeAndEnvironmentVariables()
    {
        var source = new MemorySource(
            new Dictionary<string, string>
            {
                [@"C:\winland-home\colors.conf"] = "gaps_in = 4\n",
                ["colors.conf"] = "gaps_out = 8\n",
            },
            new Dictionary<string, string>
            {
                ["HOME"] = @"C:\winland-home",
                ["WINLAND_SRC"] = "colors.conf",
            });

        var document = ConfigParser.Parse("source = ~/colors.conf\nsource = $WINLAND_SRC\n", source);

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new[]
            {
                new Setting("gaps_in", "4"),
                new Setting("gaps_out", "8"),
            },
            document.Settings);
        Assert.AreSequenceEqual([@"C:\winland-home\colors.conf", "colors.conf"], document.Sources);
    }

    [TestMethod]
    public void SourceGlobReadsEachMatch()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            ["a.conf"] = "gaps_in = 1\n",
            ["b.conf"] = "gaps_out = 2\n",
            ["c.txt"] = "border_size = 9\n",
        });

        var document = ConfigParser.Parse("source = *.conf\n", source);

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new[]
            {
                new Setting("gaps_in", "1"),
                new Setting("gaps_out", "2"),
            },
            document.Settings);
    }

    [TestMethod]
    public void SourceGlobInTheConfigFolderDoesNotReadTheConfigAgain()
    {
        const string root = "bind = Win+Q, close\nsource = C:/cfg/*.conf\n";
        var source = new MemorySource(new Dictionary<string, string>
        {
            [@"C:\cfg\colors.conf"] = "gaps_in = 1\n",
            [@"C:\cfg\winland.conf"] = root,
        });

        var document = ConfigParser.Parse(root, source, path: @"C:\cfg\winland.conf");

        Assert.IsEmpty(document.Warnings);
        Assert.ContainsSingle(document.Binds);
        Assert.AreEqual(new Setting("gaps_in", "1"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    [DataRow("winland.conf")]
    [DataRow(@"C:\cfg\winland.conf")]
    [DataRow("C:/CFG/Winland.conf")]
    [DataRow(@"C:\cfg\sub\..\winland.conf")]
    public void SourceNamingTheConfigItselfWarns(string named)
    {
        var root = $"bind = Win+Q, close\nsource = {named}\n";
        var source = new MemorySource(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetFullPath(Path.Combine(@"C:\cfg", named))] = root,
        });

        var document = ConfigParser.Parse(root, source, path: @"C:\cfg\winland.conf");

        Assert.AreEqual(new ConfigWarning(2, $"Source '{Path.GetFullPath(Path.Combine(@"C:\cfg", named))}' includes itself."), Assert.ContainsSingle(document.Warnings));
        Assert.ContainsSingle(document.Binds);
    }

    [TestMethod]
    public void SourceCycleIsFoundWhateverTheCaseOfThePath()
    {
        var source = new MemorySource(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a.conf"] = "source = B.CONF\n",
            ["b.conf"] = "source = A.conf\ngaps_in = 3\n",
        });

        var document = ConfigParser.Parse("source = a.conf\n", source);

        Assert.AreEqual(new ConfigWarning(1, "Source 'A.conf' includes itself.", "B.CONF"), Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("gaps_in", "3"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void SourceGlobWithNoMatchWarns()
    {
        var document = ConfigParser.Parse("source = *.missing\ngaps_in = 2\n", new MemorySource(new Dictionary<string, string>()));

        Assert.AreEqual(new ConfigWarning(1, "Could not read source '*.missing'."), Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("gaps_in", "2"), Assert.ContainsSingle(document.Settings));
        Assert.AreSequenceEqual(["*.missing"], document.Sources);
    }

    [TestMethod]
    public void NestedSourcesResolveAgainstTheirOwnFileAndWarningsNameItsFullPath()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            [@"C:\cfg\parts\extra.conf"] = "source = colors.conf\n",
            [@"C:\cfg\parts\colors.conf"] = "gaps_in = 4\nunknown = 1\n",
        });

        var document = ConfigParser.Parse("source = parts/extra.conf\n", source, path: @"C:\cfg\winland.conf");

        Assert.AreEqual(new Setting("gaps_in", "4"), Assert.ContainsSingle(document.Settings));
        Assert.AreEqual(@"C:\cfg\parts\colors.conf", Assert.ContainsSingle(document.Warnings).Source);
        Assert.AreSequenceEqual([@"C:\cfg\parts\extra.conf", @"C:\cfg\parts\colors.conf"], document.Sources);
    }

    [TestMethod]
    public void EmptyConfigUsesTheSourceDefaults()
    {
        var document = ConfigParser.Parse("");
        var layout = LayoutOptions.From(document);
        var decoration = DecorationOptions.From(document);

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(EdgeInsets.Uniform(4), layout.GapsIn);
        Assert.AreEqual(EdgeInsets.Uniform(8), layout.GapsOut);
        Assert.AreEqual(2, layout.BorderSize);
        Assert.IsTrue(decoration.AccentBorder);
        Assert.AreEqual(new BorderColor(0xFF, 0xFF, 0xFF, 0xFF), decoration.ActiveBorder);
        Assert.AreEqual(new BorderColor(0xFF, 0x40, 0x40, 0x40), decoration.InactiveBorder);
        Assert.AreEqual(8, decoration.Rounding);
        Assert.IsTrue(decoration.Acrylic);
    }

    private sealed class MemorySource(IReadOnlyDictionary<string, string> files, IReadOnlyDictionary<string, string>? variables = null) : IConfigSource
    {
        public string? Read(string path) => files.TryGetValue(path, out var text) ? text : null;

        public string? Variable(string name) => variables is not null && variables.TryGetValue(name, out var value) ? value : null;

        public IReadOnlyList<string> Match(string pattern) =>
            files.Keys.Where(path => SourcePath.Matches(pattern, path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }
}
