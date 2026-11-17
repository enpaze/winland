namespace Winland.Domain.Tests;

public class DomainDependencyTests
{
    [Fact]
    public void TheDomainReferencesOnlyTheBaseClassLibrary()
    {
        var references = typeof(WindowId).Assembly.GetReferencedAssemblies().Select(name => name.Name!);

        Assert.All(references, name => Assert.True(name.StartsWith("System", StringComparison.Ordinal), name));
    }
}
