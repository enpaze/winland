namespace Winland.Infrastructure.Tests;

public class OutcomeConsumerTests
{
    [Fact]
    public void EveryOutcomeFieldHasAKnownConsumer()
    {
        string[] known =
        [
            nameof(Outcome.Placements),
            nameof(Outcome.State),
            nameof(Outcome.Consumed),
            nameof(Outcome.Traits),
            nameof(Outcome.Closes),
            nameof(Outcome.Close),
            nameof(Outcome.Focus),
            nameof(Outcome.Show),
            nameof(Outcome.Hide),
            nameof(Outcome.ShowMinimized),
            nameof(Outcome.Launches),
            nameof(Outcome.Launch),
            nameof(Outcome.Events),
            nameof(Outcome.Decorations),
            nameof(Outcome.Motions),
            nameof(Outcome.Live),
            nameof(Outcome.ResetDecorations),
            nameof(Outcome.GroupBars),
            nameof(Outcome.ConfigNotice),
            nameof(Outcome.Tray),
            nameof(Outcome.Warp),
            nameof(Outcome.Cursor),
            nameof(Outcome.ResizeCursor),
            nameof(Outcome.Error),
            nameof(Outcome.Exit),
            nameof(Outcome.ZOrder),
            nameof(Outcome.IsEmpty),
        ];

        var fields = typeof(Outcome)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(known.Order(StringComparer.Ordinal), fields);
    }
}
