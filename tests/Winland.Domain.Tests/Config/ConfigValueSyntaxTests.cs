namespace Winland.Domain.Tests;

public class ConfigValueSyntaxTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("YES", true)]
    [InlineData("Off", false)]
    [InlineData("yes, please :)", true)]
    [InlineData("trueish", true)]
    public void BoolWordsParse(string text, bool expected)
    {
        Assert.True(ConfigValueSyntax.TryParseBool(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2")]
    [InlineData("nope")]
    public void OtherBoolTextIsRejected(string text)
    {
        Assert.False(ConfigValueSyntax.TryParseBool(text, out _));
    }

    [Fact]
    public void GradientKeepsTheFirstColorAndAnOptionalAngle()
    {
        Assert.True(ConfigValueSyntax.TryParseGradient("rgba(33ccffee)", out var plain));
        Assert.Equal(new BorderColor(0xEE, 0x33, 0xCC, 0xFF), plain.Color);
        Assert.False(plain.HasAngle);
        Assert.Equal(1, plain.Colors);

        Assert.True(ConfigValueSyntax.TryParseGradient("rgba(33ccffee) rgba(00ff99ee) 45deg", out var angled));
        Assert.Equal(new BorderColor(0xEE, 0x33, 0xCC, 0xFF), angled.Color);
        Assert.True(angled.HasAngle);
        Assert.Equal(45, angled.AngleDegrees);
        Assert.Equal(2, angled.Colors);

        Assert.True(ConfigValueSyntax.TryParseGradient("rgba(ff00ffff) 90deg", out var oneAngle));
        Assert.True(oneAngle.HasAngle);
        Assert.Equal(90, oneAngle.AngleDegrees);
        Assert.Equal(1, oneAngle.Colors);
    }
}
