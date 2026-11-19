namespace Winland.Infrastructure.Tests;

[TestClass]
public class ProcessFactsTests
{
    [TestMethod]
    public void AProcessIsReadOnceWhileItKeepsItsStartTime()
    {
        var started = 100L;
        var reads = 0;
        var facts = new ProcessFacts(_ => started, _ =>
        {
            reads++;
            return "app.exe";
        }, _ => [1, 2]);

        Assert.AreEqual("app.exe", facts.For(42).Exe);
        TestSequence.Equal([1, 2], facts.For(42).Ancestors);
        Assert.AreEqual(1, reads);

        started = 200;
        facts.For(42);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void AProcessWithNoStartTimeIsReadEveryTime()
    {
        var reads = 0;
        var facts = new ProcessFacts(_ => 0, _ =>
        {
            reads++;
            return "app.exe";
        }, _ => []);

        facts.For(42);
        facts.For(42);

        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void ThisProcessIsReadFromTheSystem()
    {
        var facts = ProcessFacts.Shared.For((uint)Environment.ProcessId);

        Assert.AreNotEqual(0, facts.Started);
        Assert.EndsWith(".exe", facts.Exe, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotEmpty(facts.Ancestors);
    }
}
