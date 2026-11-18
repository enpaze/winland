using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class ControlRequestTests
{
    [TestMethod]
    public void JsonErrors()
    {
        var desk = new TestDesktop();

    }

    [TestMethod]
    public void ABareDispatchListsInOrderEveryDispatcherWinlandRuns()
    {
        Assert.IsTrue(ControlInvocation.TryAnswerLocally(["dispatch"], out var list));

        var names = list.Split('\n');
        Assert.AreSequenceEqual(names.Order(StringComparer.Ordinal), names);
        Assert.Contains("killactive", names);
        var core = new WindowManager();
        foreach (var name in names)
{
    Assert.DoesNotContain("unknown dispatcher", core.Execute("dispatch " + name).Text, StringComparison.Ordinal);
}
    }

    [TestMethod]
    public void TheDispatcherListWithJIsAJsonArrayOfTheSameNames()
    {
        Assert.IsTrue(ControlInvocation.TryAnswerLocally(["dispatch"], out var plain));
        Assert.IsTrue(ControlInvocation.TryAnswerLocally(["-j", "dispatch"], out var json));

        Assert.AreSequenceEqual(plain.Split('\n'), JsonSerializer.Deserialize<string[]>(json));
    }

    [TestMethod]
    [DataRow("dispatch killactive")]
    [DataRow("clients")]
    public void ANamedRequestStillGoesToTheRunningWinland(string request)
    {
        var args = request.Split(' ');

        Assert.IsFalse(ControlInvocation.TryAnswerLocally(args, out _));
        Assert.IsTrue(ControlInvocation.TryCreate(args, out var line, out _));
        Assert.AreEqual(request, line);
    }

    [TestMethod]
    public void BindsJson()
    {
        const string description = "hyprctl binds JSON regression";
        var desk = new TestDesktop();
        desk.Set("bindd", $"SUPER, F12, {description}, exec, true");

        var binds = Json(desk.Send("j/binds")).EnumerateArray().ToArray();
        Assert.ContainsSingle(bind => bind.GetProperty("key").GetString() == "F12", binds);
        Assert.Contains(bind =>
            bind.GetProperty("key").GetString() == "F12"
            && bind.GetProperty("has_description").GetBoolean()
            && bind.GetProperty("description").GetString() == description, binds);

        Assert.Contains($"bindd = SUPER, F12, {description}, exec, true", desk.Send("binds").Split('\n'));

        desk.Set("unbind", "SUPER, F12");
    }

    private static void ExpectConfigured(TestDesktop desk)
    {
    }

    private static void ExpectNormal(JsonElement workspace, int id, string name)
    {
        Assert.AreEqual(id, workspace.GetProperty("id").GetInt32());
        Assert.AreEqual(name, Text(workspace, "name"));
        Assert.AreEqual("normal", Text(workspace, "type"));
    }

    private static void ExpectSpecial(JsonElement workspace, string name)
    {
        Assert.IsFalse(workspace.TryGetProperty("id", out _));
        Assert.AreEqual(name, Text(workspace, "name"));
        Assert.AreEqual("special", Text(workspace, "type"));
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Compact(string json) => JsonSerializer.Serialize(Json(json));
}
