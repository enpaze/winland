using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class WorkspaceSwipeTests
{
    [TestMethod]
    public void WorkspaceSwipePreexistingForce()
    {
        var swipe = Fixture.Run();
        swipe.Begin();
        swipe.Update(-90);
        swipe.Preview(swipe.Left);
        swipe.Baseline(swipe.Right, swipe.R);
        swipe.Update(180);
        swipe.Preview(swipe.Right);
        swipe.Baseline(swipe.Left, swipe.L);
        swipe.Cancel();
        swipe.Cleaned();
        swipe.End(false);
        swipe.Cleaned();

        foreach (var distance in new[] { 90, 180 })
        {
            swipe = Fixture.Run();
            swipe.Begin();
            swipe.Update(distance);
            swipe.Preview(swipe.Right);
            swipe.End(false);
            swipe.Neutral(swipe.Right);
            swipe.Baseline(swipe.Left, swipe.L);
        }
    }

    private const string OriginOutput = "HYPRTEST-SWIPE-A";

    private const string PeerOutput = "HYPRTEST-SWIPE-B";

    private readonly record struct Shot(bool Exists, bool Active, string Monitor);

    private sealed class Fixture
    {
        private const string Options = """
            gesture = 9, horizontal, workspace

            """;

        private Fixture(TestDesktop desk, string left, string origin, string right)
        {
            TestDesktop = desk;
            Left = left;
            Origin = origin;
            Right = right;
        }

        internal TestDesktop TestDesktop { get; }

        internal string Left { get; }

        internal string Origin { get; set; }

        internal string Right { get; }

        internal Shot L { get; private set; }

        internal Shot O { get; set; }

        internal Shot R { get; private set; }

        internal static Fixture Run()
        {
            var desk = new TestDesktop(Options);
            foreach (var name in new[] { "9901", "9902", "9903" })
            {
                desk.Ok("workspace", name);
                desk.Spawn("hold" + name);
            }

            var swipe = new Fixture(desk, "9901", "9902", "9903");
            swipe.Prepare();
            return swipe;
        }

        internal static Fixture RunOnOutputs()
        {
            var desk = new TestDesktop(Options + $"""
                monitor = {OriginOutput}, 1920x1080@60, 20000x0, 1
                monitor = {PeerOutput}, 1920x1080@60, 21920x0, 1

                """);
            desk.AddMonitor(OriginOutput, new Rectangle(20000, 0, 1920, 1080));
            desk.AddMonitor(PeerOutput, new Rectangle(21920, 0, 1920, 1080));
            desk.Ok("focusmonitor", OriginOutput);
            foreach (var name in new[] { "9901", "9902", "9903" })
            {
                desk.Ok("workspace", name);
                desk.Spawn("hold" + name);
            }

            var swipe = new Fixture(desk, "9901", "9902", "9903");
            swipe.Prepare();
            return swipe;
        }

        internal void Begin() => TestDesktop.Handle(new SwipeBegan(9));

        internal void Update(double delta) => TestDesktop.Handle(new SwipeUpdated(9, -delta, 0));

        internal void End(bool cancelled) => TestDesktop.Handle(new SwipeEnded(cancelled));

        internal void Cancel() => TestDesktop.Handle(new SwipeEnded(Cancelled: true));

        internal Shot Snapshot(string name)
        {
            var workspace = TestDesktop.Workspaces().FirstOrDefault(each => each.GetProperty("name").GetString() == name);
            if (workspace.ValueKind == JsonValueKind.Undefined)
                return new Shot(false, false, "");

            var monitor = workspace.GetProperty("monitor").GetString() ?? "";
            using var monitors = JsonDocument.Parse(TestDesktop.Send("j/monitors"));
            var active = monitors.RootElement.EnumerateArray().Any(each =>
                each.GetProperty("name").GetString() == monitor
                && each.GetProperty("activeWorkspace").GetProperty("name").GetString() == name);
            return new Shot(true, active, monitor);
        }

        internal void Neutral(string name) => Assert.AreEqual(new Shot(true, true, O.Monitor), Snapshot(name));

        internal void Baseline(string name, Shot expected) => Assert.AreEqual(expected, Snapshot(name));

        internal void Preview(string name)
        {
            Assert.IsFalse(Snapshot(name).Active);
            Assert.IsTrue(Snapshot(Origin).Active);
        }

        internal void Cleaned()
        {
            Baseline(Left, L);
            Baseline(Right, R);
            Neutral(Origin);
        }

        private void Prepare()
        {
            TestDesktop.Ok("workspace", Origin);
            L = Snapshot(Left);
            O = Snapshot(Origin);
            R = Snapshot(Right);
            Assert.IsTrue(L.Exists);
            Assert.IsTrue(R.Exists);
            Assert.IsFalse(L.Active);
            Assert.IsFalse(R.Active);
            Neutral(Origin);
        }
    }
}
