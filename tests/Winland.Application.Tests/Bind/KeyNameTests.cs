namespace Winland.Application.Tests;

[TestClass]
public class KeyNameTests
{
    [TestMethod]
    public void GermanLayoutNamesTheUnshiftedCharacter()
    {
        var german = German();

        Assert.AreEqual("adiaeresis", KeyNames.Name(0xDE, 0x28, false, german));
        Assert.AreEqual("odiaeresis", KeyNames.Name(0xBA, 0x27, false, german));
        Assert.AreEqual("udiaeresis", KeyNames.Name(0xDB, 0x1A, false, german));
        Assert.AreEqual("ssharp", KeyNames.Name(0xBD, 0x0C, false, german));
        Assert.AreEqual("Q", KeyNames.Name(0x51, 0x10, false, german));
    }

    [TestMethod]
    [DataRow("ä")]
    [DataRow("Ä")]
    [DataRow("adiaeresis")]
    [DataRow("Adiaeresis")]
    [DataRow("U+00E4")]
    [DataRow("u+00e4")]
    public void ConfigNamesForAUmlautResolveToTheLayoutSymbol(string written)
    {
        var document = ConfigParser.Parse($"bind = SUPER, {written}, killactive\n", layout: German());

        var bind = Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds));
        Assert.AreEqual("adiaeresis", bind.Key);
    }

    [TestMethod]
    public void OumlautConfigNamesResolveToOdiaeresis()
    {
        var german = German();

        Assert.AreEqual("odiaeresis", KeyNames.Resolve("ö", german));
        Assert.AreEqual("odiaeresis", KeyNames.Resolve("odiaeresis", german));
        Assert.AreEqual("odiaeresis", KeyNames.Resolve("U+00F6", german));
    }

    [TestMethod]
    public void ListedNonAsciiLettersResolveToTheirSymbols()
    {
        var layout = KeyLayout.From(new Dictionary<int, string>
        {
            [0x41] = "à",
            [0x45] = "é",
            [0xBA] = "è",
            [0xBC] = "ç",
            [0xBD] = "ñ",
            [0xBE] = "å",
            [0xBF] = "ø",
        });

        Assert.AreEqual("agrave", KeyNames.Resolve("à", layout));
        Assert.AreEqual("eacute", KeyNames.Resolve("é", layout));
        Assert.AreEqual("egrave", KeyNames.Resolve("è", layout));
        Assert.AreEqual("ccedilla", KeyNames.Resolve("ç", layout));
        Assert.AreEqual("ntilde", KeyNames.Resolve("ñ", layout));
        Assert.AreEqual("aring", KeyNames.Resolve("å", layout));
        Assert.AreEqual("oslash", KeyNames.Resolve("ø", layout));
    }

    [TestMethod]
    public void UsPunctuationNamesStayOnTheUsLayout()
    {
        var layout = KeyLayout.UnitedStates;

        Assert.AreEqual("Q", KeyNames.Name(0x51, 0x10, false, layout));
        Assert.AreEqual("slash", KeyNames.Name(0xBF, 0x35, false, layout));
        Assert.AreEqual("period", KeyNames.Name(0xBE, 0x34, false, layout));
        Assert.AreEqual("comma", KeyNames.Name(0xBC, 0x33, false, layout));
        Assert.AreEqual("bracketleft", KeyNames.Name(0xDB, 0x1A, false, layout));
        Assert.AreEqual("Q", KeyNames.Resolve("Q", layout));
        Assert.AreEqual("slash", KeyNames.Resolve("slash", layout));
        Assert.AreEqual("bracketleft", KeyNames.Resolve("bracketleft", layout));
    }

    [TestMethod]
    public void VkNameIsUsedWhenTheLayoutDoesNotProduceThatSymbol()
    {
        var german = German();

        Assert.AreEqual("udiaeresis", KeyNames.Resolve("bracketleft", german));
        Assert.AreEqual("minus", KeyNames.Resolve("slash", german));
        Assert.AreEqual("comma", KeyNames.Resolve("comma", german));
        Assert.AreEqual("Q", KeyNames.Resolve("Q", german));
    }

    [TestMethod]
    public void KeysymWinsWhenItAndAVkNameWouldChooseDifferentKeys()
    {
        var layout = KeyLayout.From(new Dictionary<int, string>
        {
            [0xBF] = "#",
            [0xBD] = "/",
        });

        Assert.AreEqual("slash", KeyNames.Resolve("slash", layout));
        Assert.AreEqual("numbersign", KeyNames.Name(0xBF, 0x35, false, layout));
        Assert.AreEqual("slash", KeyNames.Name(0xBD, 0x0C, false, layout));
    }

    [TestMethod]
    public void DeadKeysFallBackToTheVkName()
    {
        var german = German();

        Assert.AreEqual("equal", KeyNames.Name(0xBB, 0x0D, false, german));
        Assert.AreEqual("grave", KeyNames.Name(0xC0, 0x29, false, german));
    }

    [TestMethod]
    public void SyntheticAltGrControlIsNotACtrlKey()
    {
        var german = German();

        Assert.AreEqual("", KeyNames.Name(0xA2, 0x21D, false, german));
        Assert.AreEqual("", KeyNames.Name(0xA2, 0x1D, true, german));
        Assert.AreEqual("Control_L", KeyNames.Name(0xA2, 0x1D, false, german));
        Assert.AreEqual("Control_R", KeyNames.Name(0xA3, 0x1D, true, german));
    }

    [TestMethod]
    public void RightAltIsLevel3ShiftOnlyWhenTheLayoutHasAltGr()
    {
        Assert.AreEqual("ISO_Level3_Shift", KeyNames.Name(0xA5, 0x38, true, German()));
        Assert.AreEqual("Alt_R", KeyNames.Name(0xA5, 0x38, true, KeyLayout.UnitedStates));
    }

    [TestMethod]
    public void AltGrChordDoesNotRunACtrlAltBind()
    {
        var german = German();
        var core = Load("bind = CTRL ALT, Q, killactive\n", german);
        Appear(core, 1, new Rectangle(0, 0, 800, 600));

        Press(core, KeyNames.Name(0xA2, 0x21D, false, german));
        Press(core, KeyNames.Name(0xA5, 0x38, true, german));

        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    public void RealCtrlAltStillRunsACtrlAltBind()
    {
        var core = Load("bind = CTRL ALT, Q, killactive\n", KeyLayout.UnitedStates);
        Appear(core, 1, new Rectangle(0, 0, 800, 600));
        Press(core, "Control_L");
        Press(core, "Alt_L");

        AssertRan(Press(core, "Q"));
    }

    [TestMethod]
    public void ImeKeysHaveNoName()
    {
        Assert.IsTrue(KeyNames.PassesThrough(0xE5));
        Assert.IsTrue(KeyNames.PassesThrough(0xE7));
        Assert.AreEqual("", KeyNames.Name(0xE5, 0, false, KeyLayout.UnitedStates));
        Assert.AreEqual("", KeyNames.Name(0xE7, 0, false, KeyLayout.UnitedStates));
        Assert.IsFalse(KeyNames.PassesThrough(0x51));
    }

    [TestMethod]
    [DataRow(0x51, 0x10, false, 24)]
    [DataRow(0x54, 0x14, false, 28)]
    [DataRow(0x41, 0x1E, false, 38)]
    [DataRow(0x5A, 0x2C, false, 52)]
    [DataRow(0x5A, 0x15, false, 29)]
    [DataRow(0x31, 0x02, false, 10)]
    [DataRow(0x30, 0x0B, false, 19)]
    [DataRow(0x1B, 0x01, false, 9)]
    [DataRow(0x0D, 0x1C, false, 36)]
    [DataRow(0x0D, 0x1C, true, 104)]
    [DataRow(0x20, 0x39, false, 65)]
    [DataRow(0x09, 0x0F, false, 23)]
    [DataRow(0x08, 0x0E, false, 22)]
    [DataRow(0x26, 0x48, true, 111)]
    [DataRow(0x28, 0x50, true, 116)]
    [DataRow(0x25, 0x4B, true, 113)]
    [DataRow(0x27, 0x4D, true, 114)]
    [DataRow(0x24, 0x47, true, 110)]
    [DataRow(0x23, 0x4F, true, 115)]
    [DataRow(0x21, 0x49, true, 112)]
    [DataRow(0x22, 0x51, true, 117)]
    [DataRow(0x2D, 0x52, true, 118)]
    [DataRow(0x2E, 0x53, true, 119)]
    [DataRow(0x2C, 0x37, true, 107)]
    [DataRow(0x91, 0x46, false, 78)]
    [DataRow(0x13, 0x45, false, 127)]
    [DataRow(0x90, 0x45, false, 77)]
    [DataRow(0x5D, 0x5D, true, 135)]
    [DataRow(0x70, 0x3B, false, 67)]
    [DataRow(0x7B, 0x58, false, 96)]
    [DataRow(0x60, 0x52, false, 90)]
    [DataRow(0x61, 0x4F, false, 87)]
    [DataRow(0x67, 0x47, false, 79)]
    [DataRow(0x6F, 0x35, true, 106)]
    [DataRow(0x6A, 0x37, false, 63)]
    [DataRow(0x6B, 0x4E, false, 86)]
    [DataRow(0x6D, 0x4A, false, 82)]
    [DataRow(0x6E, 0x53, false, 91)]
    [DataRow(0xA0, 0x2A, false, 50)]
    [DataRow(0xA1, 0x36, false, 62)]
    [DataRow(0xA2, 0x1D, false, 37)]
    [DataRow(0xA3, 0x1D, true, 105)]
    [DataRow(0xA4, 0x38, false, 64)]
    [DataRow(0xA5, 0x38, true, 108)]
    [DataRow(0x5B, 0x5B, true, 133)]
    [DataRow(0x5C, 0x5C, true, 134)]
    [DataRow(0xA2, 0x21D, false, 0)]
    public void CodeNumberIsTheXkbKeycode(int virtualKey, int scanCode, bool extended, int code)
    {
        Assert.AreEqual(code, KeyNames.XkbCode(virtualKey, scanCode, extended));
    }

    [TestMethod]
    public void CodeBindUsesTheXkbNumber()
    {
        var core = Load("bind = SUPER, code:36, killactive\n", KeyLayout.UnitedStates);
        Appear(core, 1, new Rectangle(0, 0, 800, 600));
        Press(core, "Super_L");

        AssertPassed(Press(core, "Return"));
        core.Handle(new KeyReleased("Return"));
        AssertRan(Press(core, "Return", 36));
    }

    [TestMethod]
    public void ABareNumberAboveNineBindsThatKeycode()
    {
        var core = Load("bind = SUPER, 36, killactive\n", KeyLayout.UnitedStates);
        Appear(core, 1, new Rectangle(0, 0, 800, 600));
        Press(core, "Super_L");

        AssertRan(Press(core, "Return", 36));
    }

    [TestMethod]
    [DataRow("10", "code:10")]
    [DataRow("36", "code:36")]
    [DataRow("9", "9")]
    [DataRow("0", "0")]
    public void ABareNumberAboveNineIsAKeycodeAndADigitIsAKey(string text, string expected)
    {
        Assert.AreEqual(expected, KeyNames.Resolve(text, KeyLayout.UnitedStates));
    }

    [TestMethod]
    public void CodeTextIsKeptAsACode()
    {
        Assert.AreEqual("code:36", KeyNames.Resolve("code:36", German()));
        Assert.AreEqual("code:36", KeyNames.Resolve("CODE:36", KeyLayout.UnitedStates));
    }

    [TestMethod]
    public void MouseNamesAndCatchallAreNotRenamed()
    {
        var german = German();

        Assert.AreEqual("mouse:272", KeyNames.Resolve("mouse:272", german));
        Assert.AreEqual("catchall", KeyNames.Resolve("catchall", german));

        var document = ConfigParser.Parse("bindm = SUPER, mouse:272, movewindow\n", layout: german);
        var bind = Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds));
        Assert.AreEqual("mouse:272", bind.Key);
    }

    [TestMethod]
    [DataRow("mouse:left", "mouse:272")]
    [DataRow("mouse:right", "mouse:273")]
    [DataRow("mouse:middle", "mouse:274")]
    [DataRow("mouse:x1", "mouse:275")]
    [DataRow("Mouse:X2", "mouse:276")]
    public void AMouseButtonNameBindsTheSameButtonAsItsNumber(string written, string number)
    {
        var document = ConfigParser.Parse($"bindm = SUPER, {written}, movewindow\n", layout: German());

        Assert.IsEmpty(document.Warnings);
        var bind = Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds));
        Assert.AreEqual(number, bind.Key);
    }

    [TestMethod]
    public void AnUnknownMouseButtonNameIsAnUnknownKey()
    {
        var document = ConfigParser.Parse("bindm = SUPER, mouse:thumb, movewindow\n");

        Assert.IsEmpty(document.Binds);
        Assert.Contains(warning => warning.Message.Contains("mouse:thumb", StringComparison.Ordinal), document.Warnings);
    }

    [TestMethod]
    public void MultiKeyPartsResolveSeparately()
    {
        Assert.AreEqual("adiaeresis&odiaeresis", KeyNames.Resolve("ä&ö", German()));
        Assert.AreEqual("A&B", KeyNames.Resolve("A&B", KeyLayout.UnitedStates));
    }

    [TestMethod]
    public void GermanUmlautBindMatchesTheNameTheLayoutProduces()
    {
        var german = German();
        var core = Load("bind = SUPER, odiaeresis, killactive\n", german);
        Appear(core, 1, new Rectangle(0, 0, 800, 600));
        Press(core, "Super_L");

        var name = KeyNames.Name(0xBA, 0x27, false, german);
        AssertRan(Press(core, name));
    }

    private static KeyLayout German() => KeyLayout.From(
        new Dictionary<int, string>
        {
            [0x30] = "0",
            [0x51] = "q",
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

    private static WindowManager Load(string text, KeyLayout layout)
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse(text, layout: layout)));
        return core;
    }

    private static void AssertPassed(Outcome outcome)
    {
        Assert.IsFalse(outcome.Consumed);
    }

    private static void AssertRan(Outcome outcome)
    {
        Assert.IsTrue(outcome.Consumed);
        Assert.AreEqual(new WindowId(1), outcome.Close);
    }
}
