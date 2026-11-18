namespace Winland.Application.Tests;

[TestClass]
public class BindDecisionTests
{
    [TestMethod]
    public void DecisionDoesNotWaitForTheCoreLock()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, Q, killactive")));
        var session = new BindSession();
        core.Decide(new KeyPressed("Super_L"), session);

        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var held = new Thread(() => core.WhileLocked(() =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
        }));
        held.Start();
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(2)));

        Exception? failure = null;
        BindDecision decision = default;
        var decided = new Thread(() =>
        {
            try
            {
                decision = core.Decide(new KeyPressed("Q"), session);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        decided.Start();
        try
        {
            Assert.IsTrue(decided.Join(TimeSpan.FromMilliseconds(500)), "The bind decision waited on the core lock.");
        }
        finally
        {
            release.Set();
            held.Join();
        }

        Assert.IsNull(failure);
        Assert.IsTrue(decision.Consumed);
        Assert.AreEqual("killactive", decision.Dispatcher);
    }

    [TestMethod]
    public void DecisionSwallowsTheReleaseOfABindItRanAndPassesTheModifierRelease()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, Q, killactive")));
        var session = new BindSession();
        core.Decide(new KeyPressed("Super_L"), session);

        var ran = core.Decide(new KeyPressed("Q"), session);
        var keyUp = core.Decide(new KeyReleased("Q"), session);
        var superUp = core.Decide(new KeyReleased("Super_L"), session);

        Assert.AreEqual("killactive", ran.Dispatcher);
        Assert.IsTrue(ran.Consumed);
        Assert.IsTrue(ran.Mask);
        Assert.IsTrue(keyUp.Consumed);
        Assert.IsNull(keyUp.Dispatcher);
        Assert.IsFalse(superUp.Consumed);
        Assert.IsNull(superUp.Dispatcher);
        Assert.IsFalse(core.Decide(new KeyPressed("E"), session).Consumed);
    }

    [TestMethod]
    public void NoBindRunsWhileBindsArePaused()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = , Q, killactive\n")));
        var session = new BindSession();
        session.Inhibit(true);

        var paused = core.Decide(new KeyPressed("Q"), session);

        Assert.IsFalse(paused.Consumed);
        Assert.IsNull(paused.Dispatcher);
    }

    [TestMethod]
    public void ReloadReplacesTheBindTableAtomically()
    {
        var core = new WindowManager();
        var killQ = ConfigParser.Parse("bind = SUPER, Q, killactive");
        var killW = ConfigParser.Parse("bind = SUPER, W, killactive");
        core.Handle(new Configure(killQ));

        var stop = 0;
        var torn = 0;
        var samples = 0;
        var reader = new Thread(() =>
        {
            while (Volatile.Read(ref stop) == 0)
            {
                var entries = core.Binds.Binds;
                var q = false;
                var w = false;
                foreach (var bind in entries)
                {
                    if (bind.Key == "Q")
                        q = true;
                    if (bind.Key == "W")
                        w = true;
                }

                if (q == w)
                    Interlocked.Increment(ref torn);

                Interlocked.Increment(ref samples);
            }
        });
        reader.Start();
        SpinWait.SpinUntil(() => Volatile.Read(ref samples) > 0, TimeSpan.FromSeconds(10));
        for (var i = 0; i < 200; i++)
            core.Handle(new Configure(i % 2 == 0 ? killW : killQ));

        Volatile.Write(ref stop, 1);
        reader.Join();
        Assert.IsTrue(samples > 0);
        Assert.AreEqual(0, torn);

        core.Handle(new Configure(killW));
        var session = new BindSession();
        core.Decide(new KeyPressed("Super_L"), session);
        Assert.IsNull(core.Decide(new KeyPressed("Q"), session).Dispatcher);
        Assert.AreEqual("killactive", core.Decide(new KeyPressed("W"), session).Dispatcher);
    }

    [TestMethod]
    public void EveryMatchingBindIsReturnedInConfigOrder()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = , Q, killactive\nbind = , Q, workspace, 2\n")));
        var session = new BindSession();

        var decision = core.Decide(new KeyPressed("Q"), session);

        Assert.IsTrue(decision.Consumed);
        Assert.AreEqual("killactive", decision.Dispatcher);
        Assert.AreEqual("", decision.Arguments);
        Assert.AreSequenceEqual(
            new BindDispatch[]
            {
                new("killactive", ""),
                new("workspace", "2"),
            },
            decision.Dispatches);
    }

    [TestMethod]
    [DataRow("Super_L")]
    [DataRow("Super_R")]
    [DataRow("Alt_L")]
    [DataRow("Alt_R")]
    [DataRow("Control_L")]
    [DataRow("Control_R")]
    [DataRow("Shift_L")]
    [DataRow("Shift_R")]
    public void ModifierKeyBindMatchesBeforeThatKeyJoinsTheMask(string key)
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse($"bind = , {key}, killactive\n")));
        var session = new BindSession();

        var decision = core.Decide(new KeyPressed(key), session);

        Assert.IsTrue(decision.Consumed);
        Assert.AreEqual("killactive", decision.Dispatcher);
        Assert.AreSequenceEqual(new BindDispatch[] { new("killactive", "") }, decision.Dispatches);
    }

    [TestMethod]
    public void ModifierKeyBindMatchesTheOtherModifiersThatAreHeld()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, Alt_L, killactive\n")));
        var session = new BindSession();

        Assert.IsFalse(core.Decide(new KeyPressed("Alt_L"), session).Consumed);
        core.Decide(new KeyReleased("Alt_L"), session);
        core.Decide(new KeyPressed("Super_L"), session);

        var decision = core.Decide(new KeyPressed("Alt_L"), session);

        Assert.IsTrue(decision.Consumed);
        Assert.AreEqual("killactive", decision.Dispatcher);
    }

    [TestMethod]
    public void ModifierKeyBindKeepsTheModifierHeldForTheNextKey()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = , Super_L, exec, menu\nbind = SUPER, Q, killactive\n")));
        var session = new BindSession();

        var super = core.Decide(new KeyPressed("Super_L"), session);
        var next = core.Decide(new KeyPressed("Q"), session);

        Assert.AreEqual("exec", super.Dispatcher);
        Assert.AreEqual("killactive", next.Dispatcher);
        Assert.IsTrue(next.Consumed);
    }

    [TestMethod]
    public void OtherSideOfAModifierStaysInTheMask()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = , Super_L, killactive\nbind = SUPER, Super_L, exec, menu\n")));
        var session = new BindSession();
        core.Decide(new KeyPressed("Super_R"), session);

        var decision = core.Decide(new KeyPressed("Super_L"), session);

        Assert.AreEqual("exec", decision.Dispatcher);
        Assert.AreSequenceEqual(new BindDispatch[] { new("exec", "menu") }, decision.Dispatches);
    }

    [TestMethod]
    public void EmptyDispatcherConsumesTheKeyAndRunsNothing()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = , Q,\n")));
        var session = new BindSession();

        var decision = core.Decide(new KeyPressed("Q"), session);

        Assert.IsTrue(decision.Consumed);
        Assert.IsNull(decision.Dispatcher);
        Assert.IsEmpty(decision.Dispatches);
        Assert.IsTrue(core.Decide(new KeyReleased("Q"), session).Consumed);
    }

    [TestMethod]
    public void EmptyDispatcherDoesNotReplaceAnotherBindOnTheSameChord()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = , Q,\nbind = , Q, killactive\n")));
        var session = new BindSession();

        var decision = core.Decide(new KeyPressed("Q"), session);

        Assert.IsTrue(decision.Consumed);
        Assert.AreEqual("killactive", decision.Dispatcher);
        Assert.AreSequenceEqual(new BindDispatch[] { new("killactive", "") }, decision.Dispatches);
    }
}
