using System.Text.Json;

namespace Winland.Application.Tests;

/// <summary><c>inspect</c>: what Winland reads of a Window, whether it manages it and why, and a rule to change that.</summary>
public class InspectTests
{
    [Fact]
    public void ATiledClientUnderThePointerIsExplainedWithARuleToFloatIt()
    {
        var desktop = new TestDesktop();
        desktop.UnderPointer = desktop.Spawn("term", "shell", WindowTraits.Normal with { Exe = "term.exe" });

        const string Expected =
            """
            {"status":"found","target":"pointer","address":"0x1","class":"term","title":"shell","exe":"term.exe","aumid":null,"pid":1001,"managed":true,"floating":false,"reason":"default","reasonAt":"adoption","floatingReason":"default","rule":"windowrule = float, class:term, exe:term\\.exe","ruleNote":null}
            """;
        Assert.Equal(Expected, desktop.Send("j/inspect"));
    }

    [Fact]
    public void AClientOnAnotherWorkspaceIsInspectedBySelectorWithoutChangingAnything()
    {
        var desktop = new TestDesktop();
        desktop.Ok("workspace", "2");
        desktop.Spawn("b");
        desktop.Ok("workspace", "1");
        desktop.Spawn("a");
        var clients = desktop.Send("j/clients");

        var reply = desktop.WindowManager.Execute("inspect class:b");

        Assert.Equal(
            "Window 1 -> b:\n\tclass: b\n\ttitle: b\n\texe: unavailable\n\taumid: unavailable\n\tpid: 1001\n"
            + "\tmanaged: yes (when adopted: Winland manages a window like this)\n"
            + "\tfloating: no (when adopted: a window like this tiles)\n"
            + "\trule: windowrule = float, class:b\n",
            reply.Text);
        Assert.Empty(reply.Effect.Placements);
        Assert.Null(reply.Effect.Focus);
        Assert.Equal("a", desktop.ActiveClass());
        Assert.Equal(clients, desktop.Send("j/clients"));
    }

    [Fact]
    public void ADialogFloatsOnItsOwnAndARuleFloatsKitty()
    {
        var desktop = new TestDesktop();
        desktop.Spawn("save", traits: WindowTraits.Normal with { Dialog = true });
        desktop.Spawn("wr_kitty");

        var dialog = Inspect(desktop, "class:save");
        var kitty = Inspect(desktop, "class:wr_kitty");

        Assert.True(dialog.GetProperty("floating").GetBoolean());
        Assert.Equal("dialog", dialog.GetProperty("floatingReason").GetString());
        Assert.Equal("windowrule = tile, class:save", dialog.GetProperty("rule").GetString());
        Assert.Equal("rule", kitty.GetProperty("floatingReason").GetString());
        Assert.Equal("default", kitty.GetProperty("reason").GetString());
    }

    [Fact]
    public void AFloatToggledAfterAdoptionHasNoReasonItCannotKnow()
    {
        var desktop = new TestDesktop();
        desktop.Spawn("term");
        desktop.Ok("togglefloating");

        var inspected = Inspect(desktop, "class:term");

        Assert.True(inspected.GetProperty("floating").GetBoolean());
        Assert.Equal(JsonValueKind.Null, inspected.GetProperty("floatingReason").ValueKind);
        Assert.Equal("default", inspected.GetProperty("reason").GetString());
        Assert.Contains("\tfloating: yes (changed since it was adopted; the cause is not recorded)\n", desktop.Send("inspect class:term"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOverlayUnderThePointerIsLeftAloneAndNoRuleIsOffered()
    {
        var desktop = new TestDesktop();
        desktop.UnderPointer = desktop.Spawn("osd", traits: WindowTraits.Normal with { Overlay = true });

        var inspected = Inspect(desktop);

        Assert.Empty(desktop.Clients());
        Assert.False(inspected.GetProperty("managed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, inspected.GetProperty("floating").ValueKind);
        Assert.Equal("overlay", inspected.GetProperty("reason").GetString());
        Assert.Equal("now", inspected.GetProperty("reasonAt").GetString());
        Assert.Equal(JsonValueKind.Null, inspected.GetProperty("rule").ValueKind);
        Assert.Equal("native", inspected.GetProperty("ruleNote").GetString());
        var text = desktop.Send("inspect");
        Assert.Contains("\tmanaged: no (now: it is an overlay that takes no input or activation)\n", text, StringComparison.Ordinal);
        Assert.EndsWith("\trule: none (no window rule can make it a Client)\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AShellWindowHasNoDraftWithoutClaimingARuleCannotAdoptIt()
    {
        var desktop = new TestDesktop();
        desktop.UnderPointer = desktop.Spawn("shell", traits: WindowTraits.Normal with { Taskbar = true });

        var inspected = Inspect(desktop);

        Assert.Equal("shell", inspected.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, inspected.GetProperty("rule").ValueKind);
        Assert.Equal("shell", inspected.GetProperty("ruleNote").GetString());
        Assert.EndsWith("\trule: none (it is part of the Windows shell)\n", desktop.Send("inspect"), StringComparison.Ordinal);
    }

    [Fact]
    public void AToolWindowGetsARuleThatAdoptsTheNextOneLikeIt()
    {
        var desktop = new TestDesktop();
        var tool = WindowTraits.Normal with { Tool = true, Exe = "paint.exe" };
        desktop.UnderPointer = desktop.Spawn("palette", traits: tool);

        var inspected = Inspect(desktop);

        Assert.Equal("tool", inspected.GetProperty("reason").GetString());
        var rule = inspected.GetProperty("rule").GetString()!;
        Assert.Equal(@"windowrule = float, class:palette, exe:paint\.exe", rule);
        Assert.Equal("ok", desktop.Send("keyword windowrule " + rule["windowrule = ".Length..]));
        desktop.Spawn("palette", traits: tool);
        Assert.True(TestDesktop.Floating(desktop.Client("palette")));
        Assert.Equal("rule", Inspect(desktop, "class:palette").GetProperty("reason").GetString());
    }

    [Fact]
    public void WindowTextCannotForgeLinesInTheTextReply()
    {
        var desktop = new TestDesktop();
        desktop.UnderPointer = desktop.Spawn("app\nrule: forged", "title\r\n\tmanaged: no");

        var lines = desktop.Send("inspect").Split('\n');

        Assert.Equal("Window 1 -> title \tmanaged: no:", lines[0]);
        Assert.Single(lines, line => line.StartsWith("\tmanaged:", StringComparison.Ordinal));
        Assert.Equal(@"	rule: windowrule = float, class:app\x0Arule\x3A forged", lines[^2]);
    }

    [Fact]
    public void AnIgnoredWindowIsLeftAloneByARule()
    {
        var desktop = new TestDesktop("windowrule = ignore, class:pip");
        desktop.UnderPointer = desktop.Spawn("pip");

        var inspected = Inspect(desktop);

        Assert.False(inspected.GetProperty("managed").GetBoolean());
        Assert.Equal("rule", inspected.GetProperty("reason").GetString());
        Assert.Equal("windowrule = float, class:pip", inspected.GetProperty("rule").GetString());
    }

    [Fact]
    public void AWindowTheRulesNowAdoptIsNotClaimedToBeManaged()
    {
        var desktop = new TestDesktop("windowrule = ignore, class:pip");
        desktop.UnderPointer = desktop.Spawn("pip");
        desktop.ReloadWithoutExtra();
        Assert.Equal("ok", desktop.Send("reload"));

        var inspected = Inspect(desktop);

        Assert.False(inspected.GetProperty("managed").GetBoolean());
        Assert.Equal("not_adopted", inspected.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, inspected.GetProperty("rule").ValueKind);
    }

    [Fact]
    public void AFloatGivenBackAfterARestartSaysSo()
    {
        var before = new TestDesktop();
        before.Spawn("term");
        before.Ok("togglefloating");
        var after = new TestDesktop();
        after.WindowManager.Restore(before.WindowManager.Recovery());
        after.Spawn("term");

        var inspected = Inspect(after, "class:term");

        Assert.True(inspected.GetProperty("floating").GetBoolean());
        Assert.Equal("restored", inspected.GetProperty("floatingReason").GetString());
    }

    // With no Clients there is nothing for a bad selector to fail to match, so only its grammar can reject it.
    [Theory]
    [InlineData("class:[", "'[' is not a valid pattern")]
    [InlineData("[", "'[' is not a valid pattern")]
    [InlineData("title:(a", "'(a' is not a valid pattern")]
    [InlineData("pid:12a", "pid: takes a process id, such as pid:1234")]
    [InlineData("address:zz", "address: takes a window address, such as address:0x2a")]
    [InlineData("stableid:xyz", "stableid: takes the stableID clients reports, such as stableid:2a")]
    public void AMalformedSelectorIsAnErrorEvenWithNoClients(string selector, string problem)
    {
        var desktop = new TestDesktop();

        Assert.Equal("error: invalid selector: " + problem, desktop.Send("inspect " + selector));
        Assert.Equal("error: invalid selector: " + problem, desktop.Send("j/inspect " + selector));
        desktop.Spawn("term");
        Assert.Equal("error: invalid selector: " + problem, desktop.Send("j/inspect " + selector));
    }

    [Theory]
    [InlineData("class:nothing")]
    [InlineData("nothing")]
    [InlineData("pid:999")]
    [InlineData("address:0x999")]
    [InlineData("0x999")]
    [InlineData("stableid:ff")]
    public void AWellFormedSelectorThatMatchesNoClientIsNotFound(string selector)
    {
        var desktop = new TestDesktop();

        Assert.Equal("window not found: no Client matches", desktop.Send("inspect " + selector));
        Assert.Equal("no_client", Inspect(desktop, selector).GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("", "pointer", "no_window", "window not found: no window under the pointer")]
    [InlineData("class:nothing", "selector", "no_client", "window not found: no Client matches")]
    [InlineData("class:gone", "selector", "closed", "window not found: the window closed")]
    public void AWindowThatIsNotThereIsNotFoundInTextAndJson(string selector, string target, string reason, string text)
    {
        var desktop = new TestDesktop();
        desktop.Vanish(desktop.Spawn("gone"));

        var inspected = Inspect(desktop, selector);

        Assert.Equal("not_found", inspected.GetProperty("status").GetString());
        Assert.Equal(target, inspected.GetProperty("target").GetString());
        Assert.Equal(reason, inspected.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, inspected.GetProperty("address").ValueKind);
        Assert.Equal(JsonValueKind.Null, inspected.GetProperty("managed").ValueKind);
        Assert.Equal(text, desktop.Send(("inspect " + selector).Trim()));
    }

    [Fact]
    public void TheCommandLineSendsInspectWithItsWholeSelector()
    {
        var desktop = new TestDesktop();
        desktop.Spawn("doc", "My Doc");

        Assert.True(ControlInvocation.TryCreate(["-j", "inspect", "title:My Doc"], out var line, out var events));

        Assert.Equal("j/inspect title:My Doc", line);
        Assert.False(events);
        Assert.Equal("found", JsonElement.Parse(desktop.Send(line)).GetProperty("status").GetString());
        Assert.True(ControlInvocation.TryCreate(["inspect"], out var pointer, out _));
        Assert.Equal("inspect", pointer);
        Assert.False(ControlInvocation.TryCreate(["inspect", "-x"], out _, out _));
        Assert.Contains("winland [-j] inspect [selector]\n", ControlInvocation.Usage, StringComparison.Ordinal);
    }

    private static JsonElement Inspect(TestDesktop desktop, string selector = "") =>
        JsonElement.Parse(desktop.Send(("j/inspect " + selector).Trim()));
}
