namespace Winland.Infrastructure.Tests;

[TestClass]
public class DwmEffectsTests
{
    private static readonly WindowId Window = new(42);

    [TestMethod]
    public void BlurSetsTheBlurBackdropAndResetRestoresTheOriginalOne()
    {
        var surface = new FakeSurface();
        surface.Backdrops[Window] = 2;
        var effects = new DwmEffects(surface);

        effects.Apply(Window, Decorated(blur: true));
        Assert.AreEqual(DwmEffects.BlurBackdrop, surface.Backdrops[Window]);

        effects.Apply(Window, Decorated(blur: false));
        Assert.AreEqual(DwmEffects.NoBackdrop, surface.Backdrops[Window]);

        effects.Apply(Window, Decorated(blur: false));
        Assert.AreEqual(2, surface.BackdropWrites);

        effects.Reset(Window);
        Assert.AreEqual(2u, surface.Backdrops[Window]);
    }

    [TestMethod]
    public void AnUnreadableBackdropIsRestoredToTheSystemChoice()
    {
        var surface = new FakeSurface { ReadsBackdrop = false };
        var effects = new DwmEffects(surface);

        effects.Apply(Window, Decorated(blur: true));
        effects.Reset(Window);

        Assert.AreEqual(DwmEffects.AutoBackdrop, surface.Backdrops[Window]);
    }

    [TestMethod]
    public void ARegionIsCutInsideTheVisibleFrameAndClearedOnReset()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(93, 100, 214, 207), new Rectangle(100, 100, 200, 200));
        var effects = new DwmEffects(surface);

        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        var outline = surface.Regions[Window];
        Assert.AreEqual(new Point(7, 10), outline[0]);
        foreach (var point in outline)
{
    Assert.IsInRange(7, 207, point.X);
}
        foreach (var point in outline)
{
    Assert.IsInRange(0, 200, point.Y);
}
        Assert.IsTrue(effects.CutsCorners(Window));

        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        Assert.AreEqual(1, surface.RegionWrites);

        effects.Reset(Window);
        Assert.IsEmpty(surface.Regions[Window]);
        Assert.IsFalse(effects.CutsCorners(Window));
    }

    [TestMethod]
    public void RefitFollowsANewSizeAndADwmCornerClearsTheRegion()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var effects = new DwmEffects(surface);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        effects.Refit(Window);
        Assert.AreEqual(1, surface.RegionWrites);

        surface.Frames[Window] = (new Rectangle(0, 0, 300, 100), new Rectangle(0, 0, 300, 100));
        effects.Refit(Window);
        Assert.AreEqual(2, surface.RegionWrites);
        Assert.Contains(new Point(300, 90), surface.Regions[Window]);

        effects.Apply(Window, Decorated(corners: CornerPreference.Round));
        Assert.IsEmpty(surface.Regions[Window]);
        Assert.IsFalse(effects.CutsCorners(Window));
    }

    [TestMethod]
    public void AWindowWithItsOwnRegionKeepsItAndBorderlessCutsNothing()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        surface.Owned.Add(Window);
        var effects = new DwmEffects(surface);

        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        effects.Reset(Window);
        Assert.AreEqual(0, surface.RegionWrites);

        var other = new WindowId(7);
        surface.Frames[other] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        effects.Apply(other, Decorated(region: new CornerRegion(10)) with { Borderless = true });
        Assert.AreEqual(0, surface.RegionWrites);
    }

    [TestMethod]
    public void ResettingAllPutsBackEveryWindowItChanged()
    {
        var surface = new FakeSurface();
        surface.Backdrops[Window] = 2;
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var other = new WindowId(8);
        var effects = new DwmEffects(surface);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        effects.Apply(other, Decorated(blur: false));

        effects.ResetAll();

        Assert.AreEqual(2u, surface.Backdrops[Window]);
        Assert.IsEmpty(surface.Regions[Window]);
        Assert.IsFalse(effects.CutsCorners(Window));
        Assert.AreEqual(DwmEffects.AutoBackdrop, surface.Backdrops[other]);
    }

    [TestMethod]
    public void AfterResettingAllNothingIsChanged()
    {
        var surface = new FakeSurface();
        surface.Backdrops[Window] = 2;
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var effects = new DwmEffects(surface);
        effects.ResetAll();

        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        Assert.AreEqual(2u, surface.Backdrops[Window]);
        Assert.AreEqual(0, surface.RegionWrites);
        Assert.IsFalse(effects.CutsCorners(Window));
    }

    [TestMethod]
    public void ACutRegionIsRecordedAndAfterACrashRemovedSoItIsNotTakenAsTheWindowsOwn()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var effects = new DwmEffects(surface);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        var stamped = effects.Stamp(new RecoverySnapshot(
        [
            new RecoveredClient(Window, WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1),
            new RecoveredClient(new WindowId(9), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1),
        ]));
        Assert.IsTrue(stamped.Clients[0].Region);
        Assert.IsFalse(stamped.Clients[1].Region);

        var restarted = new DwmEffects(surface);
        restarted.DropLeftoverRegion(Window);
        Assert.IsEmpty(surface.Regions[Window]);

        restarted.Apply(Window, Decorated(region: new CornerRegion(10)));
        Assert.IsTrue(restarted.CutsCorners(Window));
    }

    [TestMethod]
    public void AWindowThatStoppedAnsweringKeepsItsCutRecorded()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var stopped = false;
        var effects = new DwmEffects(surface, _ => stopped);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        stopped = true;

        effects.ResetAll();

        Assert.IsNotEmpty(surface.Regions[Window]);
        var stamped = effects.Stamp(new RecoverySnapshot(
        [
            new RecoveredClient(Window, WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1),
        ]));
        Assert.IsTrue(Assert.ContainsSingle(stamped.Clients).Region);
    }

    [TestMethod]
    public void TheRegionIsShiftedByTheInvisibleFrameOnEachSide()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(90, 80, 220, 230), new Rectangle(100, 100, 200, 200));
        var effects = new DwmEffects(surface);

        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        Assert.AreEqual(new Point(10, 30), surface.Regions[Window][0]);
    }

    [TestMethod]
    [DataRow(0, 200)]
    [DataRow(200, 0)]
    public void AFrameWithNothingVisibleIsNotCut(int width, int height)
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, width, height));
        var effects = new DwmEffects(surface);

        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        Assert.AreEqual(0, surface.RegionWrites);
        Assert.IsFalse(effects.CutsCorners(Window));
    }

    [TestMethod]
    public void ANewRadiusCutsTheWindowAgain()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var effects = new DwmEffects(surface);

        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        effects.Apply(Window, Decorated(region: new CornerRegion(20)));
        effects.Apply(Window, Decorated(region: new CornerRegion(30)));

        Assert.AreEqual(3, surface.RegionWrites);
        Assert.AreEqual(1, surface.RegionChecks);
        Assert.IsTrue(effects.CutsCorners(Window));
    }

    [TestMethod]
    public void AWindowsOwnRegionIsAskedAboutOnceUntilReset()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        surface.Owned.Add(Window);
        var effects = new DwmEffects(surface);

        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        var checks = surface.RegionChecks;
        effects.Reset(Window);
        surface.Owned.Remove(Window);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        Assert.AreEqual(1, checks);
        Assert.IsTrue(effects.CutsCorners(Window));
    }

    [TestMethod]
    public void ResettingAWindowForgetsItsOwnRegion()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        surface.Owned.Add(Window);
        var effects = new DwmEffects(surface);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        effects.Reset(Window);
        surface.Owned.Remove(Window);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        Assert.IsTrue(effects.CutsCorners(Window));
    }

    [TestMethod]
    public void ResettingAllReachesAWindowWhoseBackdropWasNeverSet()
    {
        var surface = new FakeSurface { SetsBackdrop = false };
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var effects = new DwmEffects(surface);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));

        effects.ResetAll();

        Assert.IsEmpty(surface.Regions[Window]);
        Assert.IsFalse(effects.CutsCorners(Window));
    }

    [TestMethod]
    public void DroppingALeftoverRegionForgetsWhatThisRunKnew()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var owned = new WindowId(5);
        surface.Frames[owned] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        surface.Owned.Add(owned);
        var effects = new DwmEffects(surface);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        effects.Apply(owned, Decorated(region: new CornerRegion(10)));

        effects.DropLeftoverRegion(Window);
        effects.DropLeftoverRegion(owned);
        surface.Owned.Remove(owned);
        effects.Apply(owned, Decorated(region: new CornerRegion(10)));

        Assert.IsFalse(effects.CutsCorners(Window));
        Assert.IsEmpty(surface.Regions[Window]);
        Assert.IsTrue(effects.CutsCorners(owned));
    }

    [TestMethod]
    public void StampChangesOnlyTheMarksThatAreWrong()
    {
        var surface = new FakeSurface();
        surface.Frames[Window] = (new Rectangle(0, 0, 200, 200), new Rectangle(0, 0, 200, 200));
        var effects = new DwmEffects(surface);
        effects.Apply(Window, Decorated(region: new CornerRegion(10)));
        var right = new RecoverySnapshot(
        [
            new RecoveredClient(Window, WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { Region = true },
            new RecoveredClient(new WindowId(9), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1),
        ]);
        var wrong = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(10), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { Region = true },
            new RecoveredClient(new WindowId(11), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { Region = true },
        ]);

        var unchanged = effects.Stamp(right);
        var corrected = effects.Stamp(wrong);

        Assert.AreSame(right, unchanged);
        foreach (var client in corrected.Clients)
{
    Assert.IsFalse(client.Region);
}
        Assert.ThrowsExactly<ArgumentNullException>(() => effects.Stamp(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DwmEffects(null!));
    }

    private static Decoration Decorated(
        bool blur = true,
        CornerRegion? region = null,
        CornerPreference corners = CornerPreference.DoNotRound) =>
        new(new ClientId(1), new BorderColor(0xFF, 0, 0, 0), corners)
        {
            Blur = blur,
            Region = region,
        };

    private sealed class FakeSurface : IDwmSurface
    {
        public Dictionary<WindowId, uint> Backdrops { get; } = [];

        public Dictionary<WindowId, (Rectangle Outer, Rectangle Visible)> Frames { get; } = [];

        public Dictionary<WindowId, IReadOnlyList<Point>> Regions { get; } = [];

        public HashSet<WindowId> Owned { get; } = [];

        public bool ReadsBackdrop { get; init; } = true;

        public bool SetsBackdrop { get; init; } = true;

        public int RegionChecks { get; private set; }

        public int BackdropWrites { get; private set; }

        public int RegionWrites { get; private set; }

        public bool TryReadBackdrop(WindowId window, out uint backdrop)
        {
            backdrop = Backdrops.GetValueOrDefault(window);
            return ReadsBackdrop;
        }

        public bool TrySetBackdrop(WindowId window, uint backdrop)
        {
            if (!SetsBackdrop)
                return false;

            Backdrops[window] = backdrop;
            BackdropWrites++;
            return true;
        }

        public bool HasRegion(WindowId window)
        {
            RegionChecks++;
            return Owned.Contains(window) || Regions.GetValueOrDefault(window) is { Count: > 0 };
        }

        public bool TryFrame(WindowId window, out Rectangle outer, out Rectangle visible)
        {
            (outer, visible) = Frames.GetValueOrDefault(window);
            return Frames.ContainsKey(window);
        }

        public bool TrySetRegion(WindowId window, IReadOnlyList<Point> outline)
        {
            Regions[window] = outline;
            if (outline.Count > 0)
                RegionWrites++;
            return true;
        }
    }
}
