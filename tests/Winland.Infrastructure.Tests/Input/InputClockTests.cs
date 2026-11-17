namespace Winland.Infrastructure.Tests;

[Collection(DesktopCollection.Name)]
public class InputClockTests
{
    [Theory]
    [InlineData(0x41, true, true)]
    [InlineData(0x09, true, true)]
    [InlineData(0x12, true, false)]
    [InlineData(0xA4, true, false)]
    [InlineData(0xA5, true, false)]
    [InlineData(0x10, true, false)]
    [InlineData(0xA2, true, false)]
    [InlineData(0x5B, true, false)]
    [InlineData(0x12, false, true)]
    public void InjectedKeysCountAsInputExceptModifiers(int key, bool injected, bool counts)
    {
        InputClock.Watch();
        InputClock.NoteClickOrKey(1_000);

        InputClock.NoteKey(5_000, key, injected);

        Assert.Equal(counts, InputClock.Recent(5_100));
    }

    [Fact]
    public void AClickIsJudgedByTheEventTimeNotTheHandlingTime()
    {
        InputClock.Watch();
        InputClock.NoteClickOrKey(5_000);

        Assert.True(InputClock.Recent(5_100));
        Assert.False(InputClock.Recent(5_000 + InputClock.RecentMilliseconds + 1));
    }

    [Fact]
    public void AClickJustBeforeTheTickCountWrapsIsStillRecent()
    {
        InputClock.Watch();
        InputClock.NoteClickOrKey(uint.MaxValue - 50);

        Assert.True(InputClock.Recent(50));
    }
}
