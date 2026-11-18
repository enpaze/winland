namespace Winland.Infrastructure.Tests;

[TestClass]
public class TaskbarHostTests
{
    [TestMethod]
    public void ReloadLeavesTheTaskbarAloneUntilTheOptionTurnsOn()
    {
        using var files = new ConfigDir("\n");
        var bars = new RecordingTaskbar();
        using var host = Host(files, bars);

        Assert.AreEqual("ok", host.Reload().Text);
        Assert.AreEqual(0, bars.Hides);
        Assert.AreEqual(0, bars.Restores);

        files.Write("general:hide_taskbar = true\n");
        Assert.AreEqual("ok", host.Reload().Text);
        Assert.AreEqual(1, bars.Hides);

        Assert.AreEqual("ok", host.Reload().Text);
        Assert.AreEqual(1, bars.Hides);

        files.Write("general:hide_taskbar = false\n");
        Assert.AreEqual("ok", host.Reload().Text);
        Assert.AreEqual(1, bars.Restores);
    }

    [TestMethod]
    public void AKeyBoundToReloadHidesTheTaskbarTheConfigNowHides()
    {
        using var files = new ConfigDir("bind = Win+R, reload\n");
        var bars = new RecordingTaskbar();
        var core = Core(files);
        using var host = new SessionLoop(core, bars);
        Assert.AreEqual("ok", host.Reload().Text);
        Assert.AreEqual(0, bars.Hides);

        files.Write("general:hide_taskbar = true\nbind = Win+R, reload\n");
        core.Handle(new KeyPressed("Super_L"));
        core.Handle(new KeyPressed("R"));

        Assert.AreEqual(1, bars.Hides);
    }

    [TestMethod]
    public void ACrashMarkerIsPutBackWhenTheOptionIsOff()
    {
        using var files = new ConfigDir("\n");
        var bars = new RecordingTaskbar { Marker = true };
        using var host = Host(files, bars);

        Assert.AreEqual("ok", host.Reload().Text);
        Assert.AreEqual(0, bars.Hides);
        Assert.AreEqual(1, bars.Restores);

        Assert.AreEqual("ok", host.Reload().Text);
        Assert.AreEqual(1, bars.Restores);
    }

    [TestMethod]
    public void ACleanExitRestoresATaskbarThisRunHidAndLeavesOneItDidNot()
    {
        using var files = new ConfigDir("general:hide_taskbar = true\n");
        var hidden = new RecordingTaskbar();
        var host = Host(files, hidden);
        Assert.AreEqual("ok", host.Reload().Text);
        host.Dispose();
        Assert.AreEqual(1, hidden.Hides);
        Assert.AreEqual(1, hidden.Restores);

        files.Write("\n");
        var left = new RecordingTaskbar();
        var untouched = Host(files, left);
        Assert.AreEqual("ok", untouched.Reload().Text);
        untouched.Dispose();
        Assert.AreEqual(0, left.Hides);
        Assert.AreEqual(0, left.Restores);
    }

    private static SessionLoop Host(ConfigDir files, RecordingTaskbar bars) => new(Core(files), bars);

    private static WindowManager Core(ConfigDir files) => new()
    {
        ReadConfig = () => new ConfigRead(ConfigParser.Parse(File.ReadAllText(files.Path))),
    };

    private sealed class ConfigDir : IDisposable
    {
        private readonly string _root;

        public ConfigDir(string text)
        {
            _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winland-taskbar-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Path = System.IO.Path.Combine(_root, "winland.conf");
            Write(text);
        }

        public string Path { get; }

        public void Write(string text) => File.WriteAllText(Path, text);

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingTaskbar : ITaskbarSession
    {
        public bool Marker { get; set; }

        public bool ChangesWorkArea => false;

        public int Hides { get; private set; }

        public int Restores { get; private set; }

        public void Hide() => Hides++;

        public void Restore() => Restores++;
    }
}
