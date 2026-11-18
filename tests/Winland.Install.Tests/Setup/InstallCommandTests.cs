namespace Winland.Install.Tests;

[TestClass]
public class InstallCommandTests
{
    [TestMethod]
    public void InstallAsksForAnAdministratorAndCarriesTheAccount()
    {
        var first = Assert.IsExactInstanceOfType<InstallCommand.Machine>(InstallCommand.Parse(["install"]));
        var relayed = Assert.IsExactInstanceOfType<InstallCommand.Machine>(InstallCommand.Parse(["INSTALL", @"DESKTOP\dante"]));

        Assert.IsNull(first.Account);
        Assert.AreEqual(@"DESKTOP\dante", relayed.Account);
        Assert.IsTrue(first.RequiresAdministrator(machineInstallPresent: false));
    }

    [TestMethod]
    public void UserInstallAsksForNoAdministrator()
    {
        var command = Assert.IsExactInstanceOfType<InstallCommand.ForUser>(InstallCommand.Parse(["install", "--user"]));

        Assert.IsFalse(command.RequiresAdministrator(machineInstallPresent: true));
    }

    [TestMethod]
    public void InstallCanLeaveOutTheLogonStartAndTheStartNow()
    {
        var plain = Assert.IsExactInstanceOfType<InstallCommand.Machine>(InstallCommand.Parse(["install"]));
        var both = Assert.IsExactInstanceOfType<InstallCommand.Machine>(InstallCommand.Parse(["install", "--NO-START", "--no-logon"]));
        var user = Assert.IsExactInstanceOfType<InstallCommand.ForUser>(InstallCommand.Parse(["install", "--no-logon", "--user"]));

        Assert.IsTrue(plain.Logon);
        Assert.IsTrue(plain.Start);
        Assert.IsFalse(both.Logon);
        Assert.IsFalse(both.Start);
        Assert.IsNull(both.Account);
        Assert.IsFalse(user.Logon);
        Assert.IsTrue(user.Start);
    }

    [TestMethod]
    public void TheRelaunchCarriesTheAccountAndTheFlags()
    {
        var command = Assert.IsExactInstanceOfType<InstallCommand.Machine>(InstallCommand.Parse(["install", "--no-start"]));

        var relay = command.Relay(@"DESKTOP\dante");
        var relayed = Assert.IsExactInstanceOfType<InstallCommand.Machine>(InstallCommand.Parse(relay));

        Assert.AreSequenceEqual(["install", @"DESKTOP\dante", "--no-start"], relay);
        Assert.AreEqual(@"DESKTOP\dante", relayed.Account);
        Assert.IsTrue(relayed.Logon);
        Assert.IsFalse(relayed.Start);
        Assert.AreSequenceEqual(["install", @"DESKTOP\dante"], new InstallCommand.Machine(null).Relay(@"DESKTOP\dante"));
    }

    [TestMethod]
    public void UninstallAsksForAnAdministratorOnlyWhenAMachineInstallIsPresent()
    {
        var command = Assert.IsExactInstanceOfType<InstallCommand.Remove>(InstallCommand.Parse(["uninstall"]));
        var purge = Assert.IsExactInstanceOfType<InstallCommand.Remove>(InstallCommand.Parse(["uninstall", "--purge"]));

        Assert.IsFalse(command.Purge);
        Assert.IsTrue(purge.Purge);
        Assert.IsFalse(command.RequiresAdministrator(machineInstallPresent: false));
        Assert.IsTrue(purge.RequiresAdministrator(machineInstallPresent: true));
    }

    [TestMethod]
    public void UpdateAsksForNoAdministratorUntilAReplaceNeedsOne()
    {
        var command = Assert.IsExactInstanceOfType<InstallCommand.Update>(InstallCommand.Parse(["update"]));
        var upper = Assert.IsExactInstanceOfType<InstallCommand.Update>(InstallCommand.Parse(["UPDATE"]));

        Assert.IsFalse(command.RequiresAdministrator(machineInstallPresent: true));
        Assert.IsFalse(upper.RequiresAdministrator(machineInstallPresent: false));
        Assert.IsNull(command.Channel);
        Assert.IsNull(upper.Channel);
    }

    [TestMethod]
    public void UpdateCanChooseStableOrDev()
    {
        var dev = Assert.IsExactInstanceOfType<InstallCommand.Update>(InstallCommand.Parse(["update", "--channel", "dev"]));
        var stable = Assert.IsExactInstanceOfType<InstallCommand.Update>(InstallCommand.Parse(["UPDATE", "--CHANNEL", "STABLE"]));

        Assert.AreEqual(UpdateChannel.Dev, dev.Channel);
        Assert.AreEqual(UpdateChannel.Stable, stable.Channel);
        Assert.IsFalse(dev.RequiresAdministrator(machineInstallPresent: true));
    }

    [TestMethod]
    public void OtherArgumentsAreNotAnInstall()
    {
        Assert.IsNull(InstallCommand.Parse([]));
        Assert.IsNull(InstallCommand.Parse(["--config", "winland.conf"]));
    }

    [TestMethod]
    public void TweaksTurnsOffSnapAndAsksForNoAdministrator()
    {
        var snap = Assert.IsExactInstanceOfType<InstallCommand.Tweaks>(InstallCommand.Parse(["tweaks", "--off"]));
        var swipes = Assert.IsExactInstanceOfType<InstallCommand.Tweaks>(InstallCommand.Parse(["TWEAKS", "--OFF", "--touchpad"]));

        Assert.IsFalse(snap.Touchpad);
        Assert.IsTrue(swipes.Touchpad);
        Assert.IsFalse(snap.RequiresAdministrator(machineInstallPresent: true));
        Assert.IsFalse(swipes.RequiresAdministrator(machineInstallPresent: false));
    }

    [TestMethod]
    public void TweaksRestorePutsBackWhatWasRecordedAndAsksForNoAdministrator()
    {
        var restore = Assert.IsExactInstanceOfType<InstallCommand.RestoreTweaks>(InstallCommand.Parse(["Tweaks", "--RESTORE"]));

        Assert.IsFalse(restore.RequiresAdministrator(machineInstallPresent: true));
    }

    [TestMethod]
    public void AnElevatedProcessRefusesToRestoreTweaksOrCleanUpTheUser()
    {
        var restore = new InstallCommand.RestoreTweaks().ElevatedRefusal;
        var cleanup = new InstallCommand.UserCleanup().ElevatedRefusal;

        Assert.Contains("winland tweaks --restore", restore!, StringComparison.Ordinal);
        Assert.Contains("normal terminal", restore!, StringComparison.Ordinal);
        Assert.Contains("normal terminal", cleanup!, StringComparison.Ordinal);
        Assert.IsNull(new InstallCommand.Tweaks(true).ElevatedRefusal);
        Assert.IsNull(new InstallCommand.Remove(false).ElevatedRefusal);
    }

    [TestMethod]
    [DataRow("install", "--purge")]
    [DataRow("uninstall", "--user")]
    [DataRow("install", "--user", "extra")]
    [DataRow("install", "--no-start", "--no-start")]
    [DataRow("install", "--user", @"DESKTOP\dante")]
    [DataRow("install", "--no-logon", @"DESKTOP\dante")]
    [DataRow("update", "--user")]
    [DataRow("update", "extra")]
    [DataRow("update", "--channel")]
    [DataRow("update", "--channel", "nightly")]
    [DataRow("update", "--channel", "dev", "extra")]
    [DataRow("tweaks")]
    [DataRow("tweaks", "--touchpad")]
    [DataRow("tweaks", "--off", "--user")]
    [DataRow("tweaks", "--restore", "--off")]
    [DataRow("tweaks", "--off", "--restore")]
    [DataRow("tweaks", "--restore", "--touchpad")]
    [DataRow("tweaks", "--off", "--touchpad", "--restore")]
    public void AMisusedVerbIsRefused(params string[] args)
    {
        Assert.ThrowsExactly<ArgumentException>(() => InstallCommand.Parse(args));
    }
}
