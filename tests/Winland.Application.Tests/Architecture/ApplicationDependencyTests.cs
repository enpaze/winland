namespace Winland.Application.Tests;

[TestClass]
public class ApplicationDependencyTests
{
    [TestMethod]
    public void TheApplicationReferencesOnlyTheDomainAndTheBaseClassLibrary()
    {
        var references = typeof(WindowManager).Assembly.GetReferencedAssemblies().Select(name => name.Name!);

        foreach (var name in references)
{
    Assert.IsTrue(name.StartsWith("System", StringComparison.Ordinal) || name == "Winland.Domain", name);
}
        Assert.DoesNotContain("System.IO.Pipes", references);
    }
}
