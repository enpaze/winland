using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winland.Infrastructure.Tests;

[TestClass]
public class InjectedKeyTests
{
    [TestMethod]
    [DataRow((ushort)0xA2, (ushort)0x1D)]
    [DataRow((ushort)0xA3, (ushort)0x1D)]
    [DataRow((ushort)0xA4, (ushort)0x38)]
    [DataRow((ushort)0xA5, (ushort)0x38)]
    [DataRow((ushort)0xA0, (ushort)0x2A)]
    [DataRow((ushort)0xA1, (ushort)0x36)]
    [DataRow((ushort)0x5B, (ushort)0x5B)]
    public void InjectedModifiersCarryTheirScanCodes(ushort virtualKey, ushort scan)
    {
        Assert.AreEqual(scan, KeyboardHook.Key(virtualKey, up: false).Anonymous.ki.wScan);
        Assert.AreEqual(scan, KeyboardHook.Key(virtualKey, up: true).Anonymous.ki.wScan);
    }

    [TestMethod]
    public void AnExplicitScanCodeIsPreserved()
    {
        Assert.AreEqual(0x56, KeyboardHook.Key(0, up: false, scan: 0x56).Anonymous.ki.wScan);
    }

    [TestMethod]
    public void TheMenuTapIsATaggedLeftAltAroundTheMaskKey()
    {
        var inputs = Foreground.MenuTap(altHeld: false);

        TestSequence.Equal([0x12, KeyboardHook.MaskKey, KeyboardHook.MaskKey, 0x12], inputs.Select(input => (int)input.Anonymous.ki.wVk));
        foreach (var input in inputs)
{
    {
            Assert.AreEqual(KeyboardHook.InjectedTag, input.Anonymous.ki.dwExtraInfo);
            Assert.IsFalse(input.Anonymous.ki.dwFlags.HasFlag(KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY));
        }
}
        Assert.AreSequenceEqual([false, false, true, true], inputs.Select(input => input.Anonymous.ki.dwFlags.HasFlag(KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP)));
    }

    [TestMethod]
    public void AnAltTheUserHoldsIsLeftDown()
    {
        var inputs = Foreground.MenuTap(altHeld: true);

        foreach (var input in inputs)
{
    Assert.AreEqual(KeyboardHook.MaskKey, (ushort)input.Anonymous.ki.wVk);
}
    }
}
