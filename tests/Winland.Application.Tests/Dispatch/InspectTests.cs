using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class InspectTests
{
    [TestMethod]
    public void ATiledClientUnderThePointerIsExplainedWithARuleToFloatIt()
    {
        var desktop = new TestDesktop();
        desktop.UnderPointer = desktop.Spawn("term", "shell", WindowTraits.Normal with { Exe = "term.exe" });

        const string Expected =
            """
            {"status":"found","target":"pointer","address":"0x1","class":"term","title":"shell","exe":"term.exe","aumid":null,"pid":1001,"managed":true,"floating":false,"reason":"default","reasonAt":"adoption","floatingReason":"default","rule":"windowrule = float, exe:term.exe","ruleNote":null}
            """;
        Assert.AreEqual(Expected, desktop.Send("j/inspect"));
    }

    [TestMethod]
    public void AClientOnAnotherWorkspaceIsInspectedBySelectorWithoutChangingAnything()
    {
        var desktop = new TestDesktop();
        desktop.Ok("workspace", "2");
        desktop.Spawn("b");
        desktop.Ok("workspace", "1");
        desktop.Spawn("a");
        var clients = desktop.Send("j/get windows");

        var reply = desktop.WindowManager.Execute("inspect class:b");

        Assert.AreEqual(
            "Window 1 -> b:\n\tclass: b\n\ttitle: b\n\texe: unavailable\n\taumid: unavailable\n\tpid: 1001\n"
            + "\tmanaged: yes (when adopted: Winland manages a window like this)\n"
            + "\tfloating: no (when adopted: a window like this tiles)\n"
            + "\trule: windowrule = float, class:b\n",
            reply.Text);
        Assert.IsEmpty(reply.Effect.Placements);
        Assert.IsNull(reply.Effect.Focus);
        Assert.AreEqual("a", desktop.ActiveClass());
        Assert.AreEqual(clients, desktop.Send("j/get windows"));
    }

    [TestMethod]
    public void ADialogFloatsOnItsOwnAndARuleFloatsKitty()
    {
        var desktop = new TestDesktop();
        desktop.Spawn("save", traits: WindowTraits.Normal with { Dialog = true });
        desktop.Spawn("wr_kitty");

        var dialog = Inspect(desktop, "class:save");
        var kitty = Inspect(desktop, "class:wr_kitty");

        Assert.IsTrue(dialog.GetProperty("floating").GetBoolean());
        Assert.AreEqual("dialog", dialog.GetProperty("floatingReason").GetString());
        Assert.AreEqual("windowrule = tile, class:save", dialog.GetProperty("rule").GetString());
        Assert.AreEqual("rule", kitty.GetProperty("floatingReason").GetString());
        Assert.AreEqual("default", kitty.GetProperty("reason").GetString());
    }

    [TestMethod]
    public void AFloatToggledAfterAdoptionHasNoReasonItCannotKnow()
    {
        var desktop = new TestDesktop();
        desktop.Spawn("term");
        desktop.Ok("float");

        var inspected = Inspect(desktop, "class:term");

        Assert.IsTrue(inspected.GetProperty("floating").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, inspected.GetProperty("floatingReason").ValueKind);
        Assert.AreEqual("default", inspected.GetProperty("reason").GetString());
        Assert.Contains("\tfloating: yes (changed since it was adopted; the cause is not recorded)\n", desktop.Send("inspect class:term"), StringComparison.Ordinal);
    }

    [TestMethod]
    public void AnOverlayUnderThePointerIsLeftAloneAndNoRuleIsOffered()
    {
        var desktop = new TestDesktop();
        desktop.UnderPointer = desktop.Spawn("osd", traits: WindowTraits.Normal with { Overlay = true });

        var inspected = Inspect(desktop);

        Assert.IsEmpty(desktop.Clients());
        Assert.IsFalse(inspected.GetProperty("managed").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, inspected.GetProperty("floating").ValueKind);
        Assert.AreEqual("overlay", inspected.GetProperty("reason").GetString());
        Assert.AreEqual("now", inspected.GetProperty("reasonAt").GetString());
        Assert.AreEqual(JsonValueKind.Null, inspected.GetProperty("rule").ValueKind);
        Assert.AreEqual("native", inspected.GetProperty("ruleNote").GetString());
        var text = desktop.Send("inspect");
        Assert.Contains("\tmanaged: no (now: it is an overlay that takes no input or activation)\n", text, StringComparison.Ordinal);
        Assert.EndsWith("\trule: none (no window rule can make it a Client)\n", text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AShellWindowHasNoDraftWithoutClaimingARuleCannotAdoptIt()
    {
        var desktop = new TestDesktop();
        desktop.UnderPointer = desktop.Spawn("shell", traits: WindowTraits.Normal with { Taskbar = true });

        var inspected = Inspect(desktop);

        Assert.AreEqual("shell", inspected.GetProperty("reason").GetString());
        Assert.AreEqual(JsonValueKind.Null, inspected.GetProperty("rule").ValueKind);
        Assert.AreEqual("shell", inspected.GetProperty("ruleNote").GetString());
        Assert.EndsWith("\trule: none (it is part of the Windows shell)\n", desktop.Send("inspect"), StringComparison.Ordinal);
    }

    [TestMethod]
    public void AToolWindowGetsARuleThatAdoptsTheNextOneLikeIt()
    {
        var desktop = new TestDesktop();
        var tool = WindowTraits.Normal with { Tool = true, Exe = "paint.exe" };
        desktop.UnderPointer = desktop.Spawn("palette", traits: tool);

        var inspected = Inspect(desktop);

        Assert.AreEqual("tool", inspected.GetProperty("reason").GetString());
        var rule = inspected.GetProperty("rule").GetString()!;
        Assert.AreEqual("windowrule = float, exe:paint.exe", rule);
        desktop.Set("windowrule", rule["windowrule = ".Length..]);
        desktop.Spawn("palette", traits: tool);
        Assert.IsTrue(TestDesktop.Floating(desktop.Client("palette")));
        Assert.AreEqual("rule", Inspect(desktop, "class:palette").GetProperty("reason").GetString());
    }

    [TestMethod]
    public void WindowTextCannotForgeLinesInTheTextReply()
    {
        var desktop = new TestDesktop();
        desktop.UnderPointer = desktop.Spawn("app\nrule: forged", "title\r\n\tmanaged: no");

        var lines = desktop.Send("inspect").Split('\n');

        Assert.AreEqual("Window 1 -> title \tmanaged: no:", lines[0]);
        Assert.ContainsSingle(line => line.StartsWith("\tmanaged:", StringComparison.Ordinal), lines);
        Assert.AreEqual("\trule: none (none of its executable, class, or title can be written as a rule)", lines[^2]);
    }

    [TestMethod]
    public void AnIgnoredWindowIsLeftAloneByARule()
    {
        var desktop = new TestDesktop("windowrule = ignore, class:pip");
        desktop.UnderPointer = desktop.Spawn("pip");

        var inspected = Inspect(desktop);

        Assert.IsFalse(inspected.GetProperty("managed").GetBoolean());
        Assert.AreEqual("rule", inspected.GetProperty("reason").GetString());
        Assert.AreEqual("windowrule = float, class:pip", inspected.GetProperty("rule").GetString());
    }

    [TestMethod]
    public void AWindowTheRulesNowAdoptIsNotClaimedToBeManaged()
    {
        var desktop = new TestDesktop("windowrule = ignore, class:pip");
        desktop.UnderPointer = desktop.Spawn("pip");
        desktop.ReloadWithoutExtra();
        Assert.AreEqual("ok", desktop.Send("reload"));

        var inspected = Inspect(desktop);

        Assert.IsFalse(inspected.GetProperty("managed").GetBoolean());
        Assert.AreEqual("not_adopted", inspected.GetProperty("reason").GetString());
        Assert.AreEqual(JsonValueKind.Null, inspected.GetProperty("rule").ValueKind);
    }

    [TestMethod]
    public void AFloatGivenBackAfterARestartSaysSo()
    {
        var before = new TestDesktop();
        before.Spawn("term");
        before.Ok("float");
        var after = new TestDesktop();
        after.WindowManager.Restore(before.WindowManager.Recovery());
        after.Spawn("term");

        var inspected = Inspect(after, "class:term");

        Assert.IsTrue(inspected.GetProperty("floating").GetBoolean());
        Assert.AreEqual("restored", inspected.GetProperty("floatingReason").GetString());
    }

    [TestMethod]
    [DataRow("pid:12a", "pid: takes a process id, such as pid:1234")]
    [DataRow("address:zz", "address: takes a window address, such as address:0x2a")]
    public void AMalformedSelectorIsAnErrorEvenWithNoClients(string selector, string problem)
    {
        var desktop = new TestDesktop();

        Assert.AreEqual("error: invalid selector: " + problem, desktop.Send("inspect " + selector));
        Assert.AreEqual("error: invalid selector: " + problem, desktop.Send("j/inspect " + selector));
        desktop.Spawn("term");
        Assert.AreEqual("error: invalid selector: " + problem, desktop.Send("j/inspect " + selector));
    }

    [TestMethod]
    [DataRow("class:nothing")]
    [DataRow("nothing")]
    [DataRow("[")]
    [DataRow("class:[")]
    [DataRow("title:(a")]
    [DataRow("exe:nothing.exe")]
    [DataRow("pid:999")]
    [DataRow("address:0x999")]
    [DataRow("0x999")]
    public void AWellFormedSelectorThatMatchesNoClientIsNotFound(string selector)
    {
        var desktop = new TestDesktop();

        Assert.AreEqual("window not found: no Client matches", desktop.Send("inspect " + selector));
        Assert.AreEqual("no_client", Inspect(desktop, selector).GetProperty("reason").GetString());
        desktop.Spawn("term");
        Assert.AreEqual("window not found: no Client matches", desktop.Send("inspect " + selector));
        Assert.AreEqual("no_client", Inspect(desktop, selector).GetProperty("reason").GetString());
    }

    [TestMethod]
    [DataRow("term")]
    [DataRow("TERM")]
    [DataRow("class:t*")]
    [DataRow("title:My*")]
    [DataRow("exe:term.EXE")]
    [DataRow("ADDRESS:0x1")]
    public void APatternOrAddressInspectsTheClientItMatches(string selector)
    {
        var desktop = new TestDesktop();
        desktop.Spawn("term", "My Doc", WindowTraits.Normal with { Exe = "term.exe" });

        var inspected = Inspect(desktop, selector);

        Assert.AreEqual("found", inspected.GetProperty("status").GetString());
        Assert.AreEqual("0x1", inspected.GetProperty("address").GetString());
    }

    [TestMethod]
    [DataRow("", "pointer", "no_window", "window not found: no window under the pointer")]
    [DataRow("class:nothing", "selector", "no_client", "window not found: no Client matches")]
    [DataRow("class:gone", "selector", "closed", "window not found: the window closed")]
    public void AWindowThatIsNotThereIsNotFoundInTextAndJson(string selector, string target, string reason, string text)
    {
        var desktop = new TestDesktop();
        desktop.Vanish(desktop.Spawn("gone"));

        var inspected = Inspect(desktop, selector);

        Assert.AreEqual("not_found", inspected.GetProperty("status").GetString());
        Assert.AreEqual(target, inspected.GetProperty("target").GetString());
        Assert.AreEqual(reason, inspected.GetProperty("reason").GetString());
        Assert.AreEqual(JsonValueKind.Null, inspected.GetProperty("address").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, inspected.GetProperty("managed").ValueKind);
        Assert.AreEqual(text, desktop.Send(("inspect " + selector).Trim()));
    }

    [TestMethod]
    public void TheCommandLineSendsInspectWithItsWholeSelector()
    {
        var desktop = new TestDesktop();
        desktop.Spawn("doc", "My Doc");

        Assert.IsTrue(ControlInvocation.TryCreate(["-j", "inspect", "title:My Doc"], out var line, out var events));

        Assert.AreEqual("j/inspect title:My Doc", line);
        Assert.IsFalse(events);
        Assert.AreEqual("found", JsonElement.Parse(desktop.Send(line)).GetProperty("status").GetString());
        Assert.IsTrue(ControlInvocation.TryCreate(["inspect"], out var pointer, out _));
        Assert.AreEqual("inspect", pointer);
        Assert.IsFalse(ControlInvocation.TryCreate(["inspect", "-x"], out _, out _));
        Assert.Contains("winland [-j] inspect [selector]\n", ControlInvocation.Usage, StringComparison.Ordinal);
    }

    private static JsonElement Inspect(TestDesktop desktop, string selector = "") =>
        JsonElement.Parse(desktop.Send(("j/inspect " + selector).Trim()));
}
