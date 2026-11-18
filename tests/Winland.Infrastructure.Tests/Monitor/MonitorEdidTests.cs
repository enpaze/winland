namespace Winland.Infrastructure.Tests;

[TestClass]
public class MonitorEdidTests
{
    [TestMethod]
    public void AnEdidRegistryKeyFollowsTheDisplayConfigTargetPath()
    {
        var key = MonitorEdid.RegistryKey(@"\\?\DISPLAY#DEL40C4#5&2a8c2b4&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}");

        Assert.AreEqual(
            @"SYSTEM\CurrentControlSet\Enum\DISPLAY\DEL40C4\5&2a8c2b4&0&UID4352\Device Parameters",
            key);
    }

    [TestMethod]
    public void ADottedDevicePathNamesTheSameKey()
    {
        var key = MonitorEdid.RegistryKey(@"\\.\DISPLAY#SAM0302#4&1a2b3c4d&0&UID12345#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}");

        Assert.AreEqual(
            @"SYSTEM\CurrentControlSet\Enum\DISPLAY\SAM0302\4&1a2b3c4d&0&UID12345\Device Parameters",
            key);
    }

    [TestMethod]
    public void APathThatIsNotADisplayHasNoEdidKey()
    {
        Assert.IsNull(MonitorEdid.RegistryKey(@"\\?\USB#VID_1234#5678#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}"));
        Assert.IsNull(MonitorEdid.RegistryKey(""));
        Assert.IsNull(MonitorEdid.RegistryKey(null));
        Assert.IsNull(MonitorEdid.RegistryKey("   "));
    }
}
