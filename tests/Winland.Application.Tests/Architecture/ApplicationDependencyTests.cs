namespace Winland.Application.Tests;

public class ApplicationDependencyTests
{
    [Fact]
    public void TheApplicationReferencesOnlyTheDomainAndTheBaseClassLibrary()
    {
        var references = typeof(WindowManager).Assembly.GetReferencedAssemblies().Select(name => name.Name!);

        Assert.All(references, name => Assert.True(name.StartsWith("System", StringComparison.Ordinal) || name == "Winland.Domain", name));
        Assert.DoesNotContain("System.IO.Pipes", references);
    }
}
