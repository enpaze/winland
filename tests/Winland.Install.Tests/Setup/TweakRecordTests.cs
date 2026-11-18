using System.Globalization;
using System.Text.Json;

namespace Winland.Install.Tests;

[TestClass]
public class TweakRecordTests
{
    private readonly FakeTweaks windows = new();
    private readonly MemoryTextFile file = new();
    private readonly TweakRecord settings;

    public TweakRecordTests()
    {
        settings = new TweakRecord(windows, file);
    }

    [TestMethod]
    public void TurnOffReadsSnapOnceThenWritesIt()
    {
        windows.Arranging = true;

        settings.TurnOff(touchpad: false);

        Assert.AreSequenceEqual(["get arranging", "set arranging False"], windows.Log);
        Assert.IsFalse(windows.Arranging);
        Assert.AreEqual(true, RecordedArranging());
        Assert.IsFalse(RecordedProperty(TweakRecord.ThreeFingerName));
        Assert.IsFalse(RecordedProperty(TweakRecord.FourFingerName));
    }

    [TestMethod]
    public void TurnOffReadsEverySwipeOnceThenWritesThem()
    {
        windows.Arranging = true;
        windows.Three = 1;
        windows.Four = 2;

        settings.TurnOff(touchpad: true);

        Assert.AreSequenceEqual(
            [
                "get arranging",
                "get three",
                "get four",
                "set arranging False",
                "set three 0",
                "set four 0",
            ],
            windows.Log);
        Assert.AreEqual(true, RecordedArranging());
        Assert.AreEqual(1, RecordedSlide(TweakRecord.ThreeFingerName));
        Assert.AreEqual(2, RecordedSlide(TweakRecord.FourFingerName));
    }

    [TestMethod]
    public void AnAbsentSwipeIsRecordedAsAbsentAndThenSetOff()
    {
        windows.Three = null;
        windows.Four = 3;

        settings.TurnOff(touchpad: true);

        Assert.AreEqual(JsonValueKind.Null, SlideKind(TweakRecord.ThreeFingerName));
        Assert.AreEqual(3, RecordedSlide(TweakRecord.FourFingerName));
        Assert.AreEqual(0, windows.Three);
        Assert.AreEqual(0, windows.Four);
    }

    [TestMethod]
    public void ASecondTurnOffKeepsTheOriginalValues()
    {
        windows.Arranging = true;
        windows.Three = 1;
        windows.Four = 2;
        settings.TurnOff(touchpad: true);
        windows.Log.Clear();

        settings.TurnOff(touchpad: true);

        Assert.AreEqual(true, RecordedArranging());
        Assert.AreEqual(1, RecordedSlide(TweakRecord.ThreeFingerName));
        Assert.AreEqual(2, RecordedSlide(TweakRecord.FourFingerName));
        Assert.AreSequenceEqual(
            ["get arranging", "get three", "get four", "set arranging False", "set three 0", "set four 0"],
            windows.Log);
    }

    [TestMethod]
    public void RestorePutsTheRecordedValuesBackAndDeletesTheRecord()
    {
        windows.Arranging = true;
        windows.Three = 1;
        windows.Four = null;
        settings.TurnOff(touchpad: true);
        windows.Log.Clear();

        Assert.IsTrue(settings.Restore());

        Assert.IsTrue(windows.Arranging);
        Assert.AreEqual(1, windows.Three);
        Assert.IsNull(windows.Four);
        Assert.IsTrue(file.Deleted);
        Assert.IsNull(file.Text);
        Assert.AreSequenceEqual(["set arranging True", "set three 1", "set four "], windows.Log);
    }

    [TestMethod]
    public void RestoreWithNoRecordChangesNothing()
    {
        Assert.IsFalse(settings.Restore());

        Assert.IsEmpty(windows.Log);
        Assert.IsFalse(file.Deleted);
    }

    [TestMethod]
    public void ACorruptRecordIsLeftAlone()
    {
        file.Text = "{";
        windows.Arranging = true;

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => settings.TurnOff(touchpad: false));

        Assert.Contains(".winland", error.Message, StringComparison.Ordinal);
        Assert.AreSequenceEqual(["get arranging"], windows.Log);
        Assert.IsTrue(windows.Arranging);
        Assert.AreEqual("{", file.Text);
    }

    [TestMethod]
    public void RestoreLeavesACorruptRecordInPlace()
    {
        file.Text = "not json";

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => settings.Restore());

        Assert.Contains(".winland", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("uninstall", error.Message, StringComparison.Ordinal);
        Assert.IsEmpty(windows.Log);
        Assert.AreEqual("not json", file.Text);
        Assert.IsFalse(file.Deleted);
    }

    [TestMethod]
    public void ARestoreThatFailsPartWayKeepsTheWholeRecordForARetry()
    {
        windows.Arranging = true;
        windows.Three = 1;
        windows.Four = 2;
        settings.TurnOff(touchpad: true);
        var record = file.Text;
        windows.FailingSet = "set three 1";

        Assert.ThrowsExactly<IOException>(() => settings.Restore());

        Assert.IsTrue(windows.Arranging);
        Assert.AreEqual(record, file.Text);
        Assert.IsFalse(file.Deleted);

        windows.FailingSet = null;
        Assert.IsTrue(settings.Restore());

        Assert.IsTrue(windows.Arranging);
        Assert.AreEqual(1, windows.Three);
        Assert.AreEqual(2, windows.Four);
        Assert.IsTrue(file.Deleted);
    }

    private bool? RecordedArranging() =>
        Root().GetProperty(TweakRecord.WindowArrangingName).GetBoolean();

    private bool RecordedProperty(string name) =>
        Root().TryGetProperty(name, out _);

    private int RecordedSlide(string name) =>
        Root().GetProperty(name).GetInt32();

    private JsonValueKind SlideKind(string name) =>
        Root().GetProperty(name).ValueKind;

    private JsonElement Root()
    {
        using var document = JsonDocument.Parse(file.Text ?? throw new InvalidOperationException("no record"));
        return document.RootElement.Clone();
    }

    private sealed class FakeTweaks : IWindowsTweaks
    {
        public bool Arranging { get; set; } = true;

        public int? Three { get; set; }

        public int? Four { get; set; }

        public List<string> Log { get; } = [];

        public string? FailingSet { get; set; }

        public bool WindowArranging
        {
            get
            {
                Log.Add("get arranging");
                return Arranging;
            }
            set
            {
                Set("set arranging " + value);
                Arranging = value;
            }
        }

        public int? ThreeFingerSlide
        {
            get
            {
                Log.Add("get three");
                return Three;
            }
            set
            {
                Set("set three " + value);
                Three = value;
            }
        }

        public int? FourFingerSlide
        {
            get
            {
                Log.Add("get four");
                return Four;
            }
            set
            {
                Set("set four " + (value?.ToString(CultureInfo.InvariantCulture) ?? ""));
                Four = value;
            }
        }

        private void Set(string entry)
        {
            if (entry == FailingSet)
                throw new IOException("The write failed.");

            Log.Add(entry);
        }
    }

    private sealed class MemoryTextFile : ITextFile
    {
        public string? Text { get; set; }

        public bool Deleted { get; private set; }

        public string? Read() => Text;

        public void Write(string text)
        {
            Text = text;
            Deleted = false;
        }

        public void Delete()
        {
            Text = null;
            Deleted = true;
        }
    }
}
