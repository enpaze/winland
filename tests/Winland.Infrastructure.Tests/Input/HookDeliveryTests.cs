namespace Winland.Infrastructure.Tests;

public class HookDeliveryTests
{
    [Fact]
    public void RepeatsAndReleaseKeepTheNameFromThePress()
    {
        using var keys = new KeyboardHook(new WindowManager(), _ => { });

        Assert.Equal("Q", keys.KeyName(0x51, false, false, () => "Q"));
        Assert.Equal("Q", keys.KeyName(0x51, false, false, () => "й"));
        Assert.Equal("Q", keys.KeyName(0x51, false, true, () => "й"));
        Assert.Equal("й", keys.KeyName(0x51, false, false, () => "й"));
    }
}
