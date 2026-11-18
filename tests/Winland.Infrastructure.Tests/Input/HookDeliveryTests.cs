namespace Winland.Infrastructure.Tests;

[TestClass]
public class HookDeliveryTests
{
    [TestMethod]
    public void RepeatsAndReleaseKeepTheNameFromThePress()
    {
        using var keys = new KeyboardHook(new WindowManager(), _ => { });

        Assert.AreEqual("Q", keys.KeyName(0x51, false, false, () => "Q"));
        Assert.AreEqual("Q", keys.KeyName(0x51, false, false, () => "й"));
        Assert.AreEqual("Q", keys.KeyName(0x51, false, true, () => "й"));
        Assert.AreEqual("й", keys.KeyName(0x51, false, false, () => "й"));
    }
}
