internal static class TestSequence
{
    public static void Equal<T>(T[] expected, System.Collections.Generic.IEnumerable<T> actual) =>
        Assert.AreSequenceEqual(expected, actual);
}
