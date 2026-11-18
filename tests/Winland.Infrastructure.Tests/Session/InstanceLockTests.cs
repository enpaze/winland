namespace Winland.Infrastructure.Tests;

[TestClass]
public class InstanceLockTests
{
    [TestMethod]
    public void ASecondTakeFailsUntilTheFirstIsReleased()
    {
        var name = InstanceLock.NameFor("winland-test-" + Guid.NewGuid().ToString("N"));

        var first = InstanceLock.TryTake(name);
        Assert.IsNotNull(first);
        Assert.IsNull(InstanceLock.TryTake(name));

        first.Dispose();
        using var again = InstanceLock.TryTake(name);
        Assert.IsNotNull(again);
    }

    [TestMethod]
    public void TheNameIsPerSessionAndPerPipe()
    {
        Assert.AreEqual(@"Local\Winland-winland", InstanceLock.NameFor("winland"));
        Assert.AreEqual(@"Local\Winland-a_b", InstanceLock.NameFor(@"a\b"));
    }
}
