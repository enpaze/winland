namespace Winland.Domain.Tests;

public class RecoverySnapshotTests
{
    [Fact]
    public void AHeldWindowIsWrittenAndReadBack()
    {
        var snapshot = new RecoverySnapshot([]) { Held = [new HeldWindow(new WindowId(7), "Chrome_WidgetWin_1", 42)] };

        Assert.True(RecoverySnapshot.TryParse(snapshot.ToJson(), out var read));

        Assert.Equal(new HeldWindow(new WindowId(7), "Chrome_WidgetWin_1", 42), Assert.Single(read.Held));
        Assert.DoesNotContain("held", new RecoverySnapshot([]).ToJson(), StringComparison.Ordinal);
    }
}
