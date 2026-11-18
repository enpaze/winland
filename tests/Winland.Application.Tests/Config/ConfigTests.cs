namespace Winland.Application.Tests;

[TestClass]
public class ConfigTests
{
    [TestMethod]
    public void EmptyTextLoads()
    {
        var document = ConfigParser.Parse("");

        Assert.IsEmpty(document.Settings);
        Assert.IsEmpty(document.Binds);
        Assert.IsEmpty(document.Warnings);
    }

    [TestMethod]
    public void NullTextIsRejected()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfigParser.Parse(null!));
    }

    [TestMethod]
    public void AssignmentStoresTheKeywordAndValue()
    {
        var document = ConfigParser.Parse("  general:gaps_in   =   5  \nexec-once = notepad\nexec-once = explorer\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new[]
            {
                new Setting("general:gaps_in", "5"),
                new Setting("exec-once", "notepad"),
                new Setting("exec-once", "explorer"),
            },
            document.Settings);
    }

    [TestMethod]
    [DataRow("exec-once")]
    public void HandlersCanRepeat(string keyword)
    {
        var document = ConfigParser.Parse($"{keyword} = one\n{keyword} = two\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(new[] { new Setting(keyword, "one"), new Setting(keyword, "two") }, document.Settings);
    }

    [TestMethod]
    [DataRow("windowrule", "float, class:one", "tile, class:two")]
    public void WindowRulesCanRepeat(string keyword, string first, string second)
    {
        var document = ConfigParser.Parse($"{keyword} = {first}\n{keyword} = {second}\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(new[] { new Setting(keyword, first), new Setting(keyword, second) }, document.Settings);
        Assert.AreEqual(2, document.WindowRules.Count);
    }

    [TestMethod]
    public void MalformedWindowRuleWarnsAndTheConfigStillLoads()
    {
        var document = ConfigParser.Parse("windowrule = confine_pointer, class:kitty\nwindowrule = size 10, class:box\ngeneral:gaps_in = 2\n");

        Assert.AreSequenceEqual(
            new[]
            {
                new Setting("windowrule", "confine_pointer, class:kitty"),
                new Setting("windowrule", "size 10, class:box"),
                new Setting("general:gaps_in", "2"),
            },
            document.Settings);
        Assert.IsEmpty(document.WindowRules);
        Assert.AreSequenceEqual(
            new[]
            {
                new ConfigWarning(1, "Unsupported window rule 'confine_pointer'."),
                new ConfigWarning(2, "Malformed window rule."),
            },
            document.Warnings);
    }

    [TestMethod]
    public void XwaylandWindowRuleMatchIsAnUnsupportedMatch()
    {
        var document = ConfigParser.Parse("windowrule = float, xwayland:1, class:kitty\n");

        Assert.IsEmpty(document.WindowRules);
        Assert.AreEqual(
            new ConfigWarning(1, "Unsupported window rule match 'xwayland'."),
            Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("windowrule", "float, xwayland:1, class:kitty"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void UnsupportedWindowRuleMatchDropsTheRule()
    {
        var document = ConfigParser.Parse("windowrule = float, class:kitty, xdg_tag:foo\n");

        Assert.IsEmpty(document.WindowRules);
        Assert.AreEqual(
            new ConfigWarning(1, "Unsupported window rule match 'xdg_tag'."),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void AWindowRuleWithNoMatchFieldsIsDropped()
    {
        var document = ConfigParser.Parse("windowrule = float, ,\n");

        Assert.IsEmpty(document.WindowRules);
        Assert.AreSequenceEqual([new ConfigWarning(1, "Malformed window rule.")], document.Warnings);
        var core = new WindowManager(document);
        var opened = core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 800, 600)));
        Assert.IsFalse(Assert.ContainsSingle(opened.State.Clients).Floating);
    }

    [TestMethod]
    public void CommentTextIsIgnored()
    {
        var text = "# full line\ngeneral:gaps_in = 5 # trailing\ngeneral:layout = dwindle ## hash\n";

        var document = ConfigParser.Parse(text);

        Assert.AreEqual(
            new ConfigWarning(3, "Invalid value 'dwindle # hash' for general:layout."),
            Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("general:gaps_in", "5"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void CategoryBracesPrefixNestedKeywords()
    {
        var braced = ConfigParser.Parse("general {\n    gaps_in = 5\n    col.active_border = rgba(33ccffee)\n}\n");
        var flat = ConfigParser.Parse("general:gaps_in = 5\ngeneral:col.active_border = rgba(33ccffee)\n");

        Assert.IsEmpty(braced.Warnings);
        Assert.IsEmpty(flat.Warnings);
        Assert.AreSequenceEqual(flat.Settings, braced.Settings);
    }

    [TestMethod]
    public void NestedGapKeywordWarnsAndSiblingSettingsRemain()
    {
        var text = "decoration {\n    rounding = 10\n    blur {\n        popups = true\n    }\n}\n";

        var document = ConfigParser.Parse(text);

        Assert.AreEqual(new Setting("decoration:rounding", "10"), Assert.ContainsSingle(document.Settings));
        Assert.AreEqual(
            new ConfigWarning(4, "Unsupported keyword 'decoration:blur:popups'."),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    [DataRow("general")]
    [DataRow("decoration")]
    [DataRow("dwindle")]
    [DataRow("binds")]
    [DataRow("windowrule")]
    public void UnhonoredCategoryKeywordWarnsAndIsSkipped(string category)
    {
        var document = ConfigParser.Parse($"{category} {{\n    sample = 1\n}}\n");

        Assert.IsEmpty(document.Settings);
        Assert.AreEqual(
            new ConfigWarning(2, $"Unsupported keyword '{category}:sample'."),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void KeptKeywordsLoadWithoutAWarning()
    {
        var text = """
            exec-once = app
            windowrule = float, class:kitty
            windowrule = tile, class:kitty
            """;

        var document = ConfigParser.Parse(text);

        Assert.IsEmpty(document.Warnings);
    }

    [TestMethod]
    public void UnhonoredKeywordInASourceWarnsWithThatSource()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            ["extra.conf"] = "general:not_a_thing = 1\n",
        });

        var document = ConfigParser.Parse("source = extra.conf\n", source);

        Assert.IsEmpty(document.Settings);
        Assert.AreEqual(
            new ConfigWarning(1, "Unsupported keyword 'general:not_a_thing'.", "extra.conf"),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void VariablesExpandWhereTheyAreUsed()
    {
        var text = "$b = 1\n$a = $b\ngeneral:gaps_in = $a\n$a = 2\ngeneral:gaps_out = $a\n$name = gaps_in\ngeneral:$name = 9\n";

        var document = ConfigParser.Parse(text);

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new[]
            {
                new Setting("general:gaps_in", "1"),
                new Setting("general:gaps_out", "2"),
                new Setting("general:gaps_in", "9"),
            },
            document.Settings);
    }

    [TestMethod]
    public void LongerVariableNamesExpandBeforeShorterOnes()
    {
        var text = "$main = SUPER\n$mainMod = ALT\nbind = $mainMod, Q, close\n";

        var document = ConfigParser.Parse(text);

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(
            new Bind(Modifier.Alt, "Q", "close", "", null),
            Assert.ContainsSingle(document.Binds));
    }

    [TestMethod]
    public void CyclicVariablesWarnAndTheConfigStillLoads()
    {
        var document = ConfigParser.Parse("$a = $b\n$b = $a\ngeneral:layout = $a\ngeneral:gaps_in = 1\n");

        Assert.AreEqual(new ConfigWarning(3, "Variable expansion did not finish."), Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("general:gaps_in", "1"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void UndefinedVariablesStayInTheValue()
    {
        var document = ConfigParser.Parse("general:layout = $PATH\n");

        Assert.IsEmpty(document.Settings);
        Assert.AreEqual(new ConfigWarning(1, "Invalid value '$PATH' for general:layout."), Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void SourcedFileIsParsedInPlace()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            ["colors.conf"] = "general:gaps_in = $gap\n",
        });

        var document = ConfigParser.Parse("$gap = 9\n$file = colors.conf\nsource=$file\n", source);

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(new Setting("general:gaps_in", "9"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void SourceInsideACategoryInheritsIt()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            ["inner.conf"] = "gaps_in = 4\n",
        });

        var document = ConfigParser.Parse("general {\n    source = inner.conf\n    gaps_out = 8\n}\n", source);

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new[] { new Setting("general:gaps_in", "4"), new Setting("general:gaps_out", "8") },
            document.Settings);
    }

    [TestMethod]
    public void MissingSourceWarnsAndTheConfigStillLoads()
    {
        var document = ConfigParser.Parse("source = missing.conf\ngeneral:gaps_in = 4\n");

        Assert.AreEqual(new ConfigWarning(1, "Could not read source 'missing.conf'."), Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("general:gaps_in", "4"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void MalformedLineInASourceKeepsItsOwnLineNumber()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            ["bad.conf"] = "general:gaps_in = 1\nnot a line\n",
        });

        var document = ConfigParser.Parse("source = bad.conf\n", source);

        Assert.AreEqual(new Setting("general:gaps_in", "1"), Assert.ContainsSingle(document.Settings));
        Assert.AreEqual(new ConfigWarning(2, "Malformed line.", "bad.conf"), Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void SourceCycleWarnsAndDoesNotLoop()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            ["a.conf"] = "source = b.conf\n",
            ["b.conf"] = "source = a.conf\ngeneral:gaps_in = 3\n",
        });

        var document = ConfigParser.Parse("source = a.conf\n", source);

        Assert.AreEqual(new ConfigWarning(1, "Source 'a.conf' includes itself.", "b.conf"), Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("general:gaps_in", "3"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void MalformedLineWarnsWithItsLineNumber()
    {
        var document = ConfigParser.Parse("general:gaps_in = 5\r\nthis is bad\r\nexec-once = notepad\r\n}\n");

        Assert.AreSequenceEqual(
            new[]
            {
                new ConfigWarning(2, "Malformed line."),
                new ConfigWarning(4, "Malformed line."),
            },
            document.Warnings);
        Assert.AreSequenceEqual(
            new[] { new Setting("general:gaps_in", "5"), new Setting("exec-once", "notepad") },
            document.Settings);
    }

    [TestMethod]
    public void UnsupportedKeywordWarnsAndIsSkipped()
    {
        var text = "general:gaps_in = 5\nghostkeyword = path\nxwayland:enabled = true\nexecr = app\ngeneral:border_size = 2\n";

        var document = ConfigParser.Parse(text);

        Assert.AreSequenceEqual(
            new[]
            {
                new ConfigWarning(2, "Unsupported keyword 'ghostkeyword'."),
                new ConfigWarning(3, "Unsupported keyword 'xwayland:enabled'."),
                new ConfigWarning(4, "Unsupported keyword 'execr'."),
            },
            document.Warnings);
        Assert.AreSequenceEqual(
            new[] { new Setting("general:gaps_in", "5"), new Setting("general:border_size", "2") },
            document.Settings);
    }

    [TestMethod]
    public void MistypedOptionWarnsWithTheOneOptionItIsCloseTo()
    {
        var document = ConfigParser.Parse("general:gap_in = 5\n");

        Assert.AreEqual(
            new ConfigWarning(1, "Unsupported keyword 'general:gap_in'. Did you mean 'general:gaps_in'?"),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void MistypedKeywordWarnsWithTheOneKeywordItIsCloseTo()
    {
        var document = ConfigParser.Parse("windowrul = float, class:kitty\n");

        Assert.AreEqual(
            new ConfigWarning(1, "Unsupported keyword 'windowrul'. Did you mean 'windowrule'?"),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void MistypedOptionInACategoryBlockWarnsWithTheQualifiedOption()
    {
        var document = ConfigParser.Parse("general {\n    gaps_outt = 5\n}\n");

        Assert.AreEqual(
            new ConfigWarning(2, "Unsupported keyword 'general:gaps_outt'. Did you mean 'general:gaps_out'?"),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void MistypedDispatcherWarnsWithTheOneDispatcherItIsCloseTo()
    {
        var document = ConfigParser.Parse("bind = SUPER, Q, closs\n", dispatchers: WindowManager.DispatcherNames);

        Assert.AreEqual(
            new ConfigWarning(1, "Unknown dispatcher 'closs'. Did you mean 'close'?"),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void MistypedWindowRuleEffectWarnsWithTheOneEffectItIsCloseTo()
    {
        var document = ConfigParser.Parse("windowrule = floot, class:kitty\n");

        Assert.AreEqual(
            new ConfigWarning(1, "Unsupported window rule 'floot'. Did you mean 'float'?"),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void MistypedWindowRuleMatchFieldWarnsWithTheOneFieldItIsCloseTo()
    {
        var document = ConfigParser.Parse("windowrule = float, clas:^(kitty)$\n");

        Assert.AreEqual(
            new ConfigWarning(1, "Unsupported window rule match 'clas'. Did you mean 'class'?"),
            Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void EveryWindowRuleEffectTheHintOffersParses()
    {
        foreach (var name in WindowRuleParser.EffectNames)
        {
            var document = ConfigParser.Parse($"windowrule = {name}, class:kitty\n");
            Assert.DoesNotContain(warning => warning.Message.StartsWith("Unsupported window rule '", StringComparison.Ordinal), document.Warnings);
        }
    }

    [TestMethod]
    public void ANameCloseToTwoNamesGetsNoHint()
    {
        var document = ConfigParser.Parse("general:gaps_ou = 5\nbind = SUPER, Q, ex\nwindowrule = pize, class:kitty\n", dispatchers: WindowManager.DispatcherNames);

        Assert.AreSequenceEqual(
            new[]
            {
                new ConfigWarning(1, "Unsupported keyword 'general:gaps_ou'."),
                new ConfigWarning(2, "Unknown dispatcher 'ex'."),
                new ConfigWarning(3, "Unsupported window rule 'pize'."),
            },
            document.Warnings);
    }

    [TestMethod]
    public void ANameFarFromEveryNameGetsNoHint()
    {
        var document = ConfigParser.Parse("ghostkeyword = path\nbind = SUPER, Q, teleportwindow\n", dispatchers: WindowManager.DispatcherNames);

        Assert.AreSequenceEqual(
            new[]
            {
                new ConfigWarning(1, "Unsupported keyword 'ghostkeyword'."),
                new ConfigWarning(2, "Unknown dispatcher 'teleportwindow'."),
            },
            document.Warnings);
    }

    [TestMethod]
    public void PluginKeywordWarnsAndIsSkipped()
    {
        var document = ConfigParser.Parse("plugin = plugins\\sample.dll\ngeneral:gaps_in = 2\n");

        Assert.AreEqual(new ConfigWarning(1, "Unsupported keyword 'plugin'."), Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual(new Setting("general:gaps_in", "2"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void UnclosedCategoryWarnsAndTheSettingRemains()
    {
        var document = ConfigParser.Parse("general {\n    gaps_in = 5\n");

        Assert.AreEqual(new Setting("general:gaps_in", "5"), Assert.ContainsSingle(document.Settings));
        Assert.AreEqual(new ConfigWarning(1, "Unclosed category 'general'."), Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void UnclosedCategoryInASourceDoesNotLeak()
    {
        var source = new MemorySource(new Dictionary<string, string>
        {
            ["inner.conf"] = "general {\n    border_size = 3\n",
        });

        var document = ConfigParser.Parse("source = inner.conf\ngeneral:gaps_in = 1\n", source);

        Assert.AreEqual(new ConfigWarning(1, "Unclosed category 'general'.", "inner.conf"), Assert.ContainsSingle(document.Warnings));
        Assert.AreSequenceEqual(
            new[] { new Setting("general:border_size", "3"), new Setting("general:gaps_in", "1") },
            document.Settings);
    }

    [TestMethod]
    public void BackslashJoinsTheNextLine()
    {
        var document = ConfigParser.Parse("general:gaps_in = \\\n    15\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(new Setting("general:gaps_in", "15"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void ACommentEndingInABackslashDoesNotSwallowTheNextLine()
    {
        var document = ConfigParser.Parse("# path C:\\dir\\\ngeneral:gaps_in = 15\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(new Setting("general:gaps_in", "15"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void AnInlineCommentEndingInABackslashWarnsAndDoesNotSwallowTheNextLine()
    {
        var document = ConfigParser.Parse("general:gaps_out = 4 # C:\\dir\\\ngeneral:gaps_in = 15\n");

        Assert.AreEqual(new ConfigWarning(1, "A backslash at the end of a comment does not join the next line."), Assert.ContainsSingle(document.Warnings));
        Assert.AreSequenceEqual(
            new[] { new Setting("general:gaps_out", "4"), new Setting("general:gaps_in", "15") },
            document.Settings);
    }

    [TestMethod]
    public void AnEscapedHashBeforeABackslashStillJoinsTheNextLine()
    {
        var document = ConfigParser.Parse("exec-once = echo ## \\\n    done\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(new Setting("exec-once", "echo #    done"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void DanglingBackslashWarns()
    {
        var document = ConfigParser.Parse("general:gaps_in = 5\\");

        Assert.AreEqual(new ConfigWarning(1, "Line ends with a backslash."), Assert.ContainsSingle(document.Warnings));
        Assert.IsEmpty(document.Settings);
    }

    [TestMethod]
    public void EmptyDispatcherParsesWithoutAWarning()
    {
        var document = ConfigParser.Parse("bind = SUPER, Q,\nbindd = , Super_L, Menu, \n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new BindDirective[]
            {
                new Bind(Modifier.Super, "Q", "", "", null),
                new Bind(Modifier.None, "Super_L", "", "", "Menu"),
            },
            document.Binds);
    }

    [TestMethod]
    public void BindIsParsedIntoModifiersKeyAndDispatcher()
    {
        var text = "$mainMod = SUPER\nbind = $mainMod, Q, close\nbind = WIN SHIFT, code:28, exec, notepad --title hello\n";

        var document = ConfigParser.Parse(text);

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new BindDirective[]
            {
                new Bind(Modifier.Super, "Q", "close", "", null),
                new Bind(Modifier.Super | Modifier.Shift, "code:28", "exec", "notepad --title hello", null),
            },
            document.Binds);
    }

    [TestMethod]
    public void AMouseKeyBindAndADescriptionAreParsed()
    {
        var text =
            "bind = SUPER, mouse:left, move\n" +
            "bindd = SUPER, Return, Open a terminal, exec, kitty\n";

        var document = ConfigParser.Parse(text);

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new BindDirective[]
            {
                new Bind(Modifier.Super, "mouse:left", "move", "", null),
                new Bind(Modifier.Super, "Return", "exec", "kitty", "Open a terminal"),
            },
            document.Binds);
    }

    [TestMethod]
    [DataRow("SUPER", Modifier.Super)]
    [DataRow("WIN", Modifier.Super)]
    [DataRow("LOGO", Modifier.Super)]
    [DataRow("MOD4", Modifier.Super)]
    [DataRow("ALT", Modifier.Alt)]
    [DataRow("MOD1", Modifier.Alt)]
    [DataRow("CTRL", Modifier.Ctrl)]
    [DataRow("CONTROL", Modifier.Ctrl)]
    [DataRow("SHIFT", Modifier.Shift)]
    [DataRow("META", Modifier.Super)]
    [DataRow("super", Modifier.Super)]
    [DataRow("SUPER_SHIFT", Modifier.Super | Modifier.Shift)]
    [DataRow("SUPER+SHIFT", Modifier.Super | Modifier.Shift)]
    [DataRow("SUPER + SHIFT", Modifier.Super | Modifier.Shift)]
    [DataRow("SUPERSHIFT", Modifier.Super | Modifier.Shift)]
    [DataRow("Ctrl_Alt", Modifier.Ctrl | Modifier.Alt)]
    [DataRow("SUPER SHIFT CTRL ALT", Modifier.Super | Modifier.Shift | Modifier.Ctrl | Modifier.Alt)]
    public void ModifierNamesAreParsed(string name, Modifier expected)
    {
        var document = ConfigParser.Parse($"bind = {name}, Q, close\n");

        Assert.AreEqual(
            new Bind(expected, "Q", "close", "", null),
            Assert.ContainsSingle(document.Binds));
    }

    [TestMethod]
    public void AModifierVariableJoinedWithAnUnderscoreIsParsed()
    {
        var document = ConfigParser.Parse("$mainMod = SUPER\nbind = $mainMod_SHIFT, Q, close\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(
            new Bind(Modifier.Super | Modifier.Shift, "Q", "close", "", null),
            Assert.ContainsSingle(document.Binds));
    }

    [TestMethod]
    [DataRow("CAPS", Modifier.Caps, 2)]
    [DataRow("SUPER_MOD5", Modifier.Super | Modifier.Mod5, 64 | 128)]
    [DataRow("MOD2", Modifier.Mod2, 16)]
    [DataRow("MOD3 SHIFT", Modifier.Mod3 | Modifier.Shift, 32 | 1)]
    public void TheLockAndLevelModifiersAreRead(string name, Modifier modifiers, int mask)
    {
        var document = ConfigParser.Parse($"bind = {name}, Q, close\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(modifiers, Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds)).Modifiers);
        Assert.AreEqual(mask, CommandProtocol.ModMask(modifiers));
    }

    [TestMethod]
    public void ABindOnCapsFiresWhileCapsLockIsHeldAndAnotherDoesNot()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("bind = CAPS, Q, exec, held\nbind = SUPER, Q, exec, super\nbind = , Num_Lock, exec, lock\n")));

        core.Handle(new KeyPressed("Caps_Lock"));
        var held = core.Handle(new KeyPressed("q"));
        core.Handle(new KeyReleased("q"));
        core.Handle(new KeyReleased("Caps_Lock"));
        core.Handle(new KeyPressed("Super_L"));
        core.Handle(new KeyPressed("Caps_Lock"));
        var both = core.Handle(new KeyPressed("q"));

        Assert.AreSequenceEqual(["held"], held.Launches.Select(launch => launch.CommandLine));
        Assert.IsEmpty(both.Launches);
        core.Handle(new KeyReleased("q"));
        core.Handle(new KeyReleased("Caps_Lock"));
        core.Handle(new KeyReleased("Super_L"));
        Assert.AreSequenceEqual(["lock"], core.Handle(new KeyPressed("Num_Lock")).Launches.Select(launch => launch.CommandLine));
    }

    [TestMethod]
    public void InvalidBindIsSkippedAndALaterBindLoads()
    {
        var text =
            "bindz = SUPER, Q, close\n" +
            "bind = SUPER, Q\n" +
            "bindzz = SUPER, Q, close\n" +
            "bind = FOO, Q, close\n" +
            "bind = SUPER, Q, close\n";

        var document = ConfigParser.Parse(text);

        Assert.AreSequenceEqual(
            new[]
            {
                new ConfigWarning(1, "Bind flag 'z' is not supported."),
                new ConfigWarning(2, "Bind has too few arguments."),
                new ConfigWarning(3, "Bind flag 'z' is not supported."),
                new ConfigWarning(4, "Invalid modifier 'FOO'."),
            },
            document.Warnings);
        Assert.AreEqual(
            new Bind(Modifier.Super, "Q", "close", "", null),
            Assert.ContainsSingle(document.Binds));
    }

    [TestMethod]
    public void UnbindIsParsedInOrder()
    {
        var text =
            "bind = SUPER, Q, close\n" +
            "unbind = SUPER, Q\n" +
            "bind = SUPER, R, close\n" +
            "unbind = all\n";

        var document = ConfigParser.Parse(text);

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new BindDirective[]
            {
                new Bind(Modifier.Super, "Q", "close", "", null),
                new Unbind(Modifier.Super, "Q"),
                new Bind(Modifier.Super, "R", "close", "", null),
                new UnbindAll(),
            },
            document.Binds);
    }

    [TestMethod]
    public void DwindleLayoutDoesNotWarn()
    {
        var document = ConfigParser.Parse("general:layout = dwindle\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(new Setting("general:layout", "dwindle"), Assert.ContainsSingle(document.Settings));
    }

    [TestMethod]
    public void ScrollingLayoutDoesNotWarn()
    {
        var document = ConfigParser.Parse("general:layout = scrolling\nscrolling:column_width = 0.4\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreSequenceEqual(
            new[] { new Setting("general:layout", "scrolling"), new Setting("scrolling:column_width", "0.4") },
            document.Settings);
    }

    [TestMethod]
    public void FloatingLayoutDoesNotWarn()
    {
        var document = ConfigParser.Parse("general:layout = floating\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual("floating", LayoutOptions.From(document).Layout);
    }

    [TestMethod]
    [DataRow("tabbed")]
    [DataRow("master")]
    public void OtherLayoutsWarnAndAreSkipped(string name)
    {
        var document = ConfigParser.Parse($"general {{\n    layout = scrolling\n    layout = {name}\n}}\n");

        Assert.AreEqual(new Setting("general:layout", "scrolling"), Assert.ContainsSingle(document.Settings));
        Assert.AreEqual(new ConfigWarning(3, $"Invalid value '{name}' for general:layout."), Assert.ContainsSingle(document.Warnings));
        Assert.AreEqual("scrolling", LayoutOptions.From(document).Layout);
    }

    [TestMethod]
    public void ALayoutNameReadsInAnyCase()
    {
        var document = ConfigParser.Parse("general:layout = Scrolling\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual("scrolling", LayoutOptions.From(document).Layout);
    }

    [TestMethod]
    public void ExpandedLayoutNameUsesTheSameRule()
    {
        var honored = ConfigParser.Parse("$name = dwindle\ngeneral:layout = $name\n");
        var rejected = ConfigParser.Parse("$name = tabbed\ngeneral:layout = $name\n");

        Assert.IsEmpty(honored.Warnings);
        Assert.AreEqual(new Setting("general:layout", "dwindle"), Assert.ContainsSingle(honored.Settings));
        Assert.AreEqual(new ConfigWarning(2, "Invalid value 'tabbed' for general:layout."), Assert.ContainsSingle(rejected.Warnings));
    }

    [TestMethod]
    public void AWorkspaceLineWarnsAndIsSkipped()
    {
        var document = ConfigParser.Parse("workspace = 1, monitor:DP-1, default:true\ngeneral:gaps_in = 2\n");

        Assert.AreSequenceEqual([new ConfigWarning(1, "Unsupported keyword 'workspace'.")], document.Warnings);
        Assert.AreEqual(new Setting("general:gaps_in", "2"), Assert.ContainsSingle(document.Settings));
    }
}
