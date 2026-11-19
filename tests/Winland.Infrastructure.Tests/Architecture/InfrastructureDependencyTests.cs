namespace Winland.Infrastructure.Tests;

[TestClass]
public class InfrastructureDependencyTests
{
    private static readonly string[] InnerLayers = ["Winland.Domain", "Winland.Application"];

    [TestMethod]
    public void TheInfrastructureReferencesOnlyTheInnerLayers()
    {
        var winland = typeof(SessionLoop).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("Winland", StringComparison.Ordinal) || name.StartsWith("winland", StringComparison.Ordinal));

        foreach (var name in winland)
{
    Assert.Contains(name, InnerLayers);
}
    }
}
