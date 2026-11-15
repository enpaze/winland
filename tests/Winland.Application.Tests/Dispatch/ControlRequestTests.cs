using System.Text.Json;

namespace Winland.Application.Tests;

/// <summary>The control requests: the Dispatcher name list, JSON errors and the binds reply.</summary>
public class ControlRequestTests
{
    [Fact]
    public void JsonErrors()
    {
        var desk = new TestDesktop();

    }

    [Fact]
    public void ABareDispatchListsInOrderEveryDispatcherWinlandRuns()
    {
        Assert.True(ControlInvocation.TryListNames(["dispatch"], out var list));

        var names = list.Split('\n');
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Contains("killactive", names);
        var core = new WindowManager();
        Assert.All(names, name => Assert.DoesNotContain("unknown dispatcher", core.Execute("dispatch " + name).Text, StringComparison.Ordinal));
    }

    [Fact]
    public void TheDispatcherListWithJIsAJsonArrayOfTheSameNames()
    {
        Assert.True(ControlInvocation.TryListNames(["dispatch"], out var plain));
        Assert.True(ControlInvocation.TryListNames(["-j", "dispatch"], out var json));

        Assert.Equal(plain.Split('\n'), JsonSerializer.Deserialize<string[]>(json));
    }

    [Theory]
    [InlineData("dispatch killactive")]
    [InlineData("clients")]
    public void ANamedRequestStillGoesToTheRunningWinland(string request)
    {
        var args = request.Split(' ');

        Assert.False(ControlInvocation.TryListNames(args, out _));
        Assert.True(ControlInvocation.TryCreate(args, out var line, out _));
        Assert.Equal(request, line);
    }

    [Fact]
    public void BindsJson()
    {
        const string description = "hyprctl binds JSON regression";
        var desk = new TestDesktop();
        desk.Set("bindd", $"SUPER, F12, {description}, exec, true");

        var binds = Json(desk.Send("j/binds")).EnumerateArray().ToArray();
        Assert.Single(binds, bind => bind.GetProperty("key").GetString() == "F12");
        Assert.Contains(binds, bind =>
            bind.GetProperty("key").GetString() == "F12"
            && bind.GetProperty("has_description").GetBoolean()
            && bind.GetProperty("description").GetString() == description);

        Assert.Contains($"bindd = SUPER, F12, {description}, exec, true", desk.Send("binds").Split('\n'));

        desk.Set("unbind", "SUPER, F12");
    }

    private static void ExpectConfigured(TestDesktop desk)
    {
    }

    private static void ExpectNormal(JsonElement workspace, int id, string name)
    {
        Assert.Equal(id, workspace.GetProperty("id").GetInt32());
        Assert.Equal(name, Text(workspace, "name"));
        Assert.Equal("normal", Text(workspace, "type"));
    }

    private static void ExpectSpecial(JsonElement workspace, string name)
    {
        Assert.False(workspace.TryGetProperty("id", out _));
        Assert.Equal(name, Text(workspace, "name"));
        Assert.Equal("special", Text(workspace, "type"));
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Compact(string json) => JsonSerializer.Serialize(Json(json));
}
