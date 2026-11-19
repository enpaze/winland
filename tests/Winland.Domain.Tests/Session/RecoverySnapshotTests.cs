namespace Winland.Domain.Tests;

[TestClass]
public class RecoverySnapshotTests
{
    [TestMethod]
    public void AHeldWindowIsWrittenAndReadBack()
    {
        var snapshot = new RecoverySnapshot([]) { Held = [new HeldWindow(new WindowId(7), "Chrome_WidgetWin_1", 42)] };

        Assert.IsTrue(RecoverySnapshot.TryParse(snapshot.ToJson(), out var read));

        Assert.AreEqual(new HeldWindow(new WindowId(7), "Chrome_WidgetWin_1", 42), Assert.ContainsSingle(read.Held));
        Assert.DoesNotContain("held", new RecoverySnapshot([]).ToJson(), StringComparison.Ordinal);
    }
}
