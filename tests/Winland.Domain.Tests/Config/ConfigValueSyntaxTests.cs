namespace Winland.Domain.Tests;

[TestClass]
public class ConfigValueSyntaxTests
{
    [TestMethod]
    [DataRow("true", true)]
    [DataRow("false", false)]
    [DataRow("yes", true)]
    [DataRow("no", false)]
    [DataRow("on", true)]
    [DataRow("off", false)]
    [DataRow("1", true)]
    [DataRow("0", false)]
    [DataRow("YES", true)]
    [DataRow("Off", false)]
    [DataRow("yes, please :)", true)]
    [DataRow("trueish", true)]
    public void BoolWordsParse(string text, bool expected)
    {
        Assert.IsTrue(ConfigValueSyntax.TryParseBool(text, out var value));
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("2")]
    [DataRow("nope")]
    public void OtherBoolTextIsRejected(string text)
    {
        Assert.IsFalse(ConfigValueSyntax.TryParseBool(text, out _));
    }

    [TestMethod]
    public void GradientKeepsTheFirstColorAndAnOptionalAngle()
    {
        Assert.IsTrue(ConfigValueSyntax.TryParseGradient("rgba(33ccffee)", out var plain));
        Assert.AreEqual(new BorderColor(0xEE, 0x33, 0xCC, 0xFF), plain.Color);
        Assert.IsFalse(plain.HasAngle);
        Assert.AreEqual(1, plain.Colors);

        Assert.IsTrue(ConfigValueSyntax.TryParseGradient("rgba(33ccffee) rgba(00ff99ee) 45deg", out var angled));
        Assert.AreEqual(new BorderColor(0xEE, 0x33, 0xCC, 0xFF), angled.Color);
        Assert.IsTrue(angled.HasAngle);
        Assert.AreEqual(45, angled.AngleDegrees);
        Assert.AreEqual(2, angled.Colors);

        Assert.IsTrue(ConfigValueSyntax.TryParseGradient("rgba(ff00ffff) 90deg", out var oneAngle));
        Assert.IsTrue(oneAngle.HasAngle);
        Assert.AreEqual(90, oneAngle.AngleDegrees);
        Assert.AreEqual(1, oneAngle.Colors);
    }
}
