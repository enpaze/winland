namespace Winland.Domain.Tests;

[TestClass]
public class DomainDependencyTests
{
    [TestMethod]
    public void TheDomainReferencesOnlyTheBaseClassLibrary()
    {
        var references = typeof(WindowId).Assembly.GetReferencedAssemblies().Select(name => name.Name!);

        foreach (var name in references)
{
    Assert.IsTrue(name.StartsWith("System", StringComparison.Ordinal), name);
}
    }
}
