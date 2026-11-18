namespace Winland.Infrastructure.Tests;

[TestClass]
public class WindowAdapterTests
{
    private const string Config = "";

    private static bool Posted(string call, long window) =>
        call.StartsWith($"bounds {window} ", StringComparison.Ordinal) && call.EndsWith(" Post", StringComparison.Ordinal);

    [TestMethod]
    public void InspectingAClientWhoseWindowIsGoneFindsNothingAndChangesNothing()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(0x7FFEFFFE);
        windows.Desktop.Calls.Clear();

        var reply = windows.Core.Execute("j/inspect address:0x7ffefffe");

        Assert.StartsWith("""{"status":"not_found","target":"selector",""", reply.Text, StringComparison.Ordinal);
        Assert.Contains("\"reason\":\"closed\"", reply.Text, StringComparison.Ordinal);
        Assert.IsEmpty(windows.Desktop.Calls);
        Assert.IsEmpty(reply.Effect.Placements);
    }

    [TestMethod]
    public void AWindowRevealedOnExitStaysShownWhenItsWorkspaceIsHiddenAgain()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Dispatch("workspace", "2");
        windows.Open(2);
        windows.Dispatch("workspace", "1");
        Assert.IsFalse(windows.Desktop[2].Shown);

        windows.Adapter.RevealHidden();

        Assert.IsTrue(windows.Desktop[2].Shown);
        windows.Dispatch("workspace", "2");
        Assert.IsTrue(windows.Desktop[1].Shown);
        Assert.IsTrue(windows.Desktop[2].Shown);
    }

    [TestMethod]
    public void AnOutcomeAppliedAfterTheExitCleanupHidesNothing()
    {
        var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Dispatch("workspace", "2");
        windows.Open(2);
        windows.Adapter.RevealHidden();
        windows.Dispose();
        windows.Desktop.Calls.Clear();

        windows.Dispatch("workspace", "1");

        Assert.IsTrue(windows.Desktop[1].Shown);
        Assert.IsTrue(windows.Desktop[2].Shown);
        Assert.IsEmpty(windows.Desktop.Calls);
    }

    [TestMethod]
    public void ADestroyedConcealedWindowIsForgottenBeforeItsHandleIsReused()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Open(2);
        windows.Dispatch("workspace", "2");
        Assert.IsFalse(windows.Desktop[2].Shown);
        windows.Desktop.Remove(2);

        windows.Adapter.Notice(Windows.Win32.PInvoke.EVENT_OBJECT_DESTROY, (nint)2);
        Assert.IsFalse(windows.Core.Manages(new WindowId(2)));
        windows.Desktop.Add(2, TestWindows.Area);
        windows.Desktop.Calls.Clear();
        windows.Adapter.Salvage();

        Assert.DoesNotContain(call => call.StartsWith("show 2 ", StringComparison.Ordinal), windows.Desktop.Calls);
    }

    [TestMethod]
    public void DropGoneClosesOnlyClientsWhoseWindowsNoLongerExist()
    {
        using var windows = new TestWindows(reducedMotion: true);
        var first = windows.Open(1);
        var second = windows.Open(2);
        windows.Desktop.Remove(2);

        windows.Adapter.DropGone();

        Assert.IsTrue(windows.Core.Manages(first));
        Assert.IsFalse(windows.Core.Manages(second));
        Assert.AreEqual(first, Assert.ContainsSingle(windows.Core.Recovery().Clients).Window);
    }

    [TestMethod]
    public void AnOutcomeIsCarriedOutInTheOrderOfItsParts()
    {
        using var windows = new TestWindows(reducedMotion: true);
        var first = windows.Open(1);
        var second = windows.Open(2);
        windows.Dispatch("workspace", "2");
        windows.Open(3);
        windows.Desktop.Add(9, TestWindows.Area);
        var back = windows.Core.Handle(new Dispatch("workspace", "1"));
        Assert.Contains(new WindowId(3), back.Hide);
        Assert.Contains(first, back.Show);
        Assert.Contains(second, back.Show);
        windows.Desktop[1].Visible = new Rectangle(100, 100, 300, 300);
        windows.Desktop[2].Visible = new Rectangle(100, 100, 300, 300);
        windows.Desktop.Calls.Clear();

        windows.Adapter.ApplyCore(back with
        {
            Closes = [new WindowId(9)],
            Focus = first,
            ZOrder = [new ZOrderRequest(second, true)],
        });

        var desktop = windows.Desktop;
        var order = new[]
        {
            desktop.IndexOf("bounds 1 "),
            desktop.IndexOf("close 9"),
            desktop.IndexOf("show 1 Show"),
            desktop.IndexOf("foreground 1"),
            desktop.IndexOf("let-go 2"),
            desktop.IndexOf("show 3 Hide"),
        };
        Assert.DoesNotContain(-1, order);
        Assert.AreSequenceEqual(order.Order(), order);

        Assert.AreEqual(-1, desktop.IndexOf("raise 2 top"));
    }

    [TestMethod]
    public void AnOutcomeDecidedWhileAnotherWaitsToPaintLandsAfterIt()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Dispatch("workspace", "2");
        windows.Open(2);
        Thread? other = null;
        using var decided = new ManualResetEventSlim();
        windows.Desktop.OnPace = () =>
        {
            if (other is not null)
                return;

            other = new Thread(() =>
            {
                var back = windows.Core.Handle(new Dispatch("workspace", "2"));
                decided.Set();
                windows.Adapter.Apply(back);
            });
            other.Start();
            Assert.IsTrue(decided.Wait(TimeSpan.FromSeconds(5)));

            var deadline = Environment.TickCount64 + 5000;
            while ((other.ThreadState & (ThreadState.Stopped | ThreadState.WaitSleepJoin)) == 0 && Environment.TickCount64 < deadline)
                Thread.Yield();
        };

        windows.Dispatch("workspace", "1");
        Assert.IsTrue(other!.Join(TimeSpan.FromSeconds(5)));

        Assert.IsTrue(windows.Desktop[2].Shown);
        Assert.IsFalse(windows.Desktop[1].Shown);
    }

    [TestMethod]
    public void HidingTheForegroundWindowWithNothingFocusedGivesTheDesktopTheKeyboardFirst()
    {
        using var windows = new TestWindows(reducedMotion: true);
        var first = windows.Open(1);
        windows.Desktop.ForegroundWindow = first;
        var away = windows.Core.Handle(new Dispatch("workspace", "2"));
        Assert.IsNull(away.Focus);
        windows.Desktop.Calls.Clear();

        windows.Apply(away);

        var desktop = windows.Desktop;
        Assert.IsTrue(desktop.IndexOf("foreground desktop") >= 0);
        Assert.IsTrue(desktop.IndexOf("foreground desktop") < desktop.IndexOf("show 1 Hide"));
    }

    [TestMethod]
    public void AGroupsNextMemberIsShownUnderTheLastBeforeItIsHidden()
    {
        using var windows = new TestWindows(reducedMotion: true);
        var first = windows.Open(1);
        windows.Dispatch("group");
        var second = windows.Open(2);
        windows.Desktop.Calls.Clear();

        windows.Dispatch("tab", "prev");

        var desktop = windows.Desktop;
        var order = new[]
        {
            desktop.IndexOf($"under {first.Value} {second.Value}"),
            desktop.IndexOf($"show {first.Value} Show"),
            desktop.IndexOf($"show {second.Value} Hide"),
        };
        Assert.DoesNotContain(-1, order);
        Assert.AreSequenceEqual(order.Order(), order);
    }

    [TestMethod]
    public void TransitionsAreTurnedOffOncePerWindowBeforeItsFirstHide()
    {
        using var windows = new TestWindows(Config);
        windows.Open(1);
        windows.Open(2);
        windows.Dispatch("workspace", "2");
        windows.Settle();
        windows.Dispatch("workspace", "1");
        windows.Settle();

        var calls = windows.Desktop.Calls;
        Assert.AreEqual(1, calls.Count(call => call == "attr 1 3=1"));
        Assert.AreEqual(1, calls.Count(call => call == "attr 2 3=1"));
        Assert.IsTrue(calls.IndexOf("attr 1 3=1") < windows.Desktop.IndexOf("show 1 Hide"));
        Assert.IsTrue(calls.IndexOf("attr 2 3=1") < windows.Desktop.IndexOf("show 2 Hide"));
    }

    [TestMethod]
    public void AWindowAWorkspaceSlidesInTakesItsFirstFrameBeforeItShows()
    {
        using var windows = new TestWindows(Config);
        windows.Open(1);
        windows.Settle();
        windows.Dispatch("workspace", "2");
        windows.Settle();
        windows.Desktop.Calls.Clear();

        windows.Dispatch("workspace", "1");

        var calls = windows.Desktop.Calls;
        var first = calls.FindIndex(call => call.StartsWith("bounds 1 ", StringComparison.Ordinal));
        Assert.EndsWith(" Wait", calls[first], StringComparison.Ordinal);
        Assert.StartsWith("bounds 1 -", calls[first], StringComparison.Ordinal);
        Assert.IsTrue(first < windows.Desktop.IndexOf("show 1 Show"));
    }

    [TestMethod]
    public void AClientClosedAndOpenedAgainOnTheSameHandleIsPaintedAgain()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        var second = windows.Open(2);
        windows.Apply(windows.Core.Handle(new WindowClosed(second)));
        Assert.AreEqual(0u, windows.Desktop[2].Attributes[3]);
        windows.Desktop.Remove(2);

        windows.Open(2);

        Assert.AreEqual(1u, windows.Desktop[2].Attributes[3]);
    }

    [TestMethod]
    public void TheClientsWhoseDecorationChangedArePaintedAgain()
    {
        using var windows = new TestWindows("decoration:inactive_opacity = 0.5\n", reducedMotion: true);
        windows.Open(1);
        windows.Open(2);
        windows.Open(3);
        Assert.AreEqual(0x80000u, windows.Desktop[1].ExStyle & 0x80000);
        windows.Desktop.Calls.Clear();

        windows.Dispatch("focus", "address:0x1");

        Assert.AreEqual(0u, windows.Desktop[1].ExStyle & 0x80000);
        Assert.AreEqual(128, windows.Desktop[3].Alpha);
        Assert.DoesNotContain(call => call.EndsWith(" 2", StringComparison.Ordinal) || call.Contains(" 2 ", StringComparison.Ordinal), windows.Desktop.Calls);
    }

    [TestMethod]
    public void TransitionsAreTurnedOnAgainWhenADecorationIsReset()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        var second = windows.Open(2);
        Assert.AreEqual(1u, windows.Desktop[2].Attributes[3]);

        windows.Apply(windows.Core.Handle(new WindowClosed(second)));

        Assert.AreEqual(0u, windows.Desktop[2].Attributes[3]);
        Assert.AreEqual(1u, windows.Desktop[1].Attributes[3]);
    }

    [TestMethod]
    public void SalvageShowsEveryWindowItHidAndEveryOneTheSnapshotHolds()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Dispatch("workspace", "2");
        var second = windows.Open(2);
        windows.Dispatch("workspace", "1");
        var snapshot = windows.Adapter.Capture();
        Assert.AreSequenceEqual([second], snapshot!.Clients.Where(client => client.Hidden).Select(client => client.Window));

        windows.Dispatch("workspace", "2");
        Assert.IsFalse(windows.Desktop[1].Shown);
        windows.Desktop.Calls.Clear();

        windows.Adapter.Salvage();

        Assert.IsTrue(windows.Desktop[1].Shown);
        Assert.IsTrue(windows.Desktop[2].Shown);
        Assert.Contains("show 1 Show", windows.Desktop.Calls);
        Assert.Contains("show 2 Show", windows.Desktop.Calls);
    }

    [TestMethod]
    public void SalvageSkipsAWindowThatIsGone()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Dispatch("workspace", "2");
        windows.Open(2);
        windows.Dispatch("workspace", "1");
        windows.Desktop.Remove(2);
        windows.Desktop.Calls.Clear();

        windows.Adapter.Salvage();

        Assert.DoesNotContain(call => call.StartsWith("show 2 ", StringComparison.Ordinal), windows.Desktop.Calls);
    }

    [TestMethod]
    public void AfterSalvageNothingIsCaptured()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        Assert.IsNotNull(windows.Adapter.Capture());

        windows.Adapter.Salvage();

        Assert.IsNull(windows.Adapter.Capture());
    }

    [TestMethod]
    public void AnOutcomeThatAsksForNothingTouchesNothing()
    {
        var published = 0;
        using var windows = new TestWindows(reducedMotion: true, changed: () => published++);
        windows.Open(1);
        Assert.AreEqual(1, published);
        windows.Desktop.Calls.Clear();

        windows.Adapter.Apply(new Outcome([], new State([])) { IsEmpty = true });

        Assert.IsEmpty(windows.Desktop.Calls);
        Assert.AreEqual(1, published);
    }

    [TestMethod]
    public void AHungWindowIsHiddenWithoutWaitingForIt()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Open(2);
        windows.Desktop[1].Hung = true;
        windows.Desktop.Calls.Clear();

        windows.Dispatch("workspace", "2");

        Assert.Contains("show-async 1 Hide", windows.Desktop.Calls);
        Assert.Contains("show 2 Hide", windows.Desktop.Calls);
        Assert.DoesNotContain("show 1 Hide", windows.Desktop.Calls);
    }

    [TestMethod]
    public void AHungWindowIsPlacedWithoutWaitingForIt()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Desktop[1].Hung = true;
        windows.Desktop.Calls.Clear();

        windows.Open(2);

        Assert.Contains(call => Posted(call, 1), windows.Desktop.Calls);
        Assert.DoesNotContain(call => call.StartsWith("bounds 1 ", StringComparison.Ordinal) && call.EndsWith(" Wait", StringComparison.Ordinal), windows.Desktop.Calls);
        Assert.Contains(call => call.StartsWith("bounds 2 ", StringComparison.Ordinal) && call.EndsWith(" Wait", StringComparison.Ordinal), windows.Desktop.Calls);
    }

    [TestMethod]
    public void AHungWindowIsRestackedWithoutWaitingForIt()
    {
        using var windows = new TestWindows();
        var first = windows.Open(1);
        var second = windows.Open(2);
        windows.Desktop[1].Hung = true;
        windows.Desktop.Calls.Clear();

        windows.Adapter.ApplyCore(new Outcome([], new State([])) { ZOrder = [new ZOrderRequest(first, true), new ZOrderRequest(second, false)] });

        Assert.AreSequenceEqual(["raise-async 1 top", "raise 2 bottom"], windows.Desktop.Calls);
    }

    [TestMethod]
    public void AFloatingWindowIsHeldOnTopWhileATileTakesTheFocus()
    {
        using var windows = new TestWindows();
        windows.Open(1);
        windows.Open(2);
        windows.Dispatch("float");
        windows.Desktop.Calls.Clear();

        windows.Dispatch("focus", "address:0x1");

        var calls = windows.Desktop.Calls;
        Assert.IsTrue(calls.IndexOf("hold 2") >= 0, string.Join(" | ", calls));
        Assert.IsTrue(calls.IndexOf("hold 2") < calls.IndexOf("foreground 1"), string.Join(" | ", calls));
        Assert.IsTrue(calls.IndexOf("foreground 1") < calls.IndexOf("let-go 2"), string.Join(" | ", calls));
        Assert.AreEqual(0u, windows.Desktop[2].ExStyle & 0x8);
    }

    [TestMethod]
    public void AWindowItsProgramKeepsOnTopStaysTopmost()
    {
        using var windows = new TestWindows();
        windows.Open(1);
        windows.Open(2);
        windows.Dispatch("float");
        windows.Desktop[2].ExStyle |= 0x8;
        windows.Desktop.Calls.Clear();

        windows.Dispatch("focus", "address:0x1");

        Assert.DoesNotContain("let-go 2", windows.Desktop.Calls);
        Assert.AreEqual(0x8u, windows.Desktop[2].ExStyle & 0x8);
    }

    [TestMethod]
    public void AWindowClosedWithoutAMotionIsTransparentAsItGoesAndComesBackIfItStays()
    {
        using var windows = new TestWindows(reducedMotion: true);
        windows.Open(1);
        windows.Open(2);
        windows.Desktop.Calls.Clear();

        windows.Dispatch("close");

        var desktop = windows.Desktop;
        Assert.IsTrue(desktop.IndexOf("alpha 2 0") >= 0, "The window was not made transparent.");
        Assert.IsTrue(desktop.IndexOf("alpha 2 0") < desktop.IndexOf("close 2"));

        Assert.AreEqual(0, desktop[2].Alpha);
        windows.Settle();
        Assert.AreEqual(0u, desktop[2].ExStyle & 0x00080000);
    }
}
