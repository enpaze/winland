namespace Winland.Daemon.Tests;

[TestClass]
public class StateLocationTests
{
    private const string User = "S-1-5-21-9";
    private const string Standard = @"C:\Users\me\.winland\state.json";

    [TestMethod]
    public void WithoutElevationTheVariableIsFollowedWithoutAsking()
    {
        var asked = new List<string>();

        var chosen = StateLocation.Choose(@"D:\session\state.json", Standard, elevated: false, folder => Ask(asked, folder, "no"));
        var unset = StateLocation.Choose(" ", Standard, elevated: true, folder => Ask(asked, folder, "no"));

        Assert.AreEqual((@"D:\session\state.json", (string?)null), chosen);
        Assert.AreEqual((Standard, (string?)null), unset);
        Assert.IsEmpty(asked);
    }

    [TestMethod]
    public void ElevatedTheVariableIsFollowedOnlyToAFolderThatPasses()
    {
        var asked = new List<string>();

        var passed = StateLocation.Choose(@"D:\session\state.json", Standard, elevated: true, folder => Ask(asked, folder, null));
        var refused = StateLocation.Choose(@"C:\Windows\System32\drivers\etc\hosts", Standard, elevated: true, folder => Ask(asked, folder, "only administrators can write it"));

        Assert.AreEqual((@"D:\session\state.json", (string?)null), passed);
        Assert.AreEqual(Standard, refused.Path);
        Assert.IsNotNull(refused.Ignored);
        Assert.Contains(StateLocation.Variable, refused.Ignored, StringComparison.Ordinal);
        Assert.Contains("only administrators can write it", refused.Ignored, StringComparison.Ordinal);
        Assert.AreSequenceEqual([@"D:\session", @"C:\Windows\System32\drivers\etc"], asked);
    }

    [TestMethod]
    public void AFolderTheUserOwnsOrCanEmptyMayHoldTheStateFile()
    {
        var owned = new AccessList(User, []);
        var granted = new AccessList(
            AdministratorOnly.AdministratorsSid,
            [new PrincipalAccess(User, CanWrite: true, AppliesToTarget: true, CanRemove: true)]);

        Assert.IsNull(StateLocation.Refusal(@"C:\Users\me\.winland", owned, User));
        Assert.IsNull(StateLocation.Refusal(@"C:\Users\me\.winland", granted, User));
    }

    [TestMethod]
    public void AFolderOnlyAdministratorsCanEmptyIsRefused()
    {
        var system = new AccessList(
            AdministratorOnly.TrustedInstallerSid,
            [
                new PrincipalAccess(AdministratorOnly.AdministratorsSid, CanWrite: true, AppliesToTarget: true, CanRemove: true),
                new PrincipalAccess("S-1-5-32-545", CanWrite: false, AppliesToTarget: true),
            ]);
        var addOnly = new AccessList(
            AdministratorOnly.SystemSid,
            [
                new PrincipalAccess(User, CanWrite: true, AppliesToTarget: true),
                new PrincipalAccess(User, CanWrite: true, AppliesToTarget: false, CanRemove: true),
            ]);

        Assert.IsNotNull(StateLocation.Refusal(@"C:\Windows\System32\drivers\etc", system, User));
        Assert.IsNotNull(StateLocation.Refusal(@"C:\Shared", addOnly, User));
        Assert.IsNotNull(StateLocation.Refusal(@"C:\Unreadable", null, User));
    }

    [TestMethod]
    public void AVariableThatNamesNoFolderIsIgnoredElevated()
    {
        var asked = new List<string>();

        var chosen = StateLocation.Choose(@"C:\", Standard, elevated: true, folder => Ask(asked, folder, null));

        Assert.AreEqual(Standard, chosen.Path);
        Assert.IsNotNull(chosen.Ignored);
        Assert.Contains(@"'C:\' names no folder", chosen.Ignored, StringComparison.Ordinal);
        Assert.IsEmpty(asked);
    }

    [TestMethod]
    public void ARefusalNamesTheFolder()
    {
        var other = new AccessList(AdministratorOnly.SystemSid, []);

        Assert.Contains(@"C:\Shared", StateLocation.Refusal(@"C:\Shared", other, User)!, StringComparison.Ordinal);
        Assert.Contains(@"C:\Unreadable", StateLocation.Refusal(@"C:\Unreadable", null, User)!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void MissingArgumentsAreRefused()
    {
        var owned = new AccessList(User, []);

        Assert.ThrowsExactly<ArgumentException>(() => StateLocation.Choose(null, " ", elevated: false, _ => null));
        Assert.ThrowsExactly<ArgumentNullException>(() => StateLocation.Choose(null, Standard, elevated: false, null!));
        Assert.ThrowsExactly<ArgumentException>(() => StateLocation.Refusal(" ", owned, User));
        Assert.ThrowsExactly<ArgumentException>(() => StateLocation.Refusal(@"C:\Shared", owned, " "));
    }

    private static string? Ask(List<string> asked, string folder, string? answer)
    {
        asked.Add(folder);
        return answer;
    }
}
