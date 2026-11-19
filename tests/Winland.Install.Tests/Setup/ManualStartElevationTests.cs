namespace Winland.Install.Tests;

[TestClass]
public class ManualStartElevationTests
{
    [TestMethod]
    public void AStandardUserContinuesWithoutAskingForAnotherAccountsCredentials()
    {
        var choice = ManualStartElevation.Decide(false, null, [], limitedToken: false);

        Assert.IsExactInstanceOfType<ElevationChoice.Continue>(choice);
    }

    [TestMethod]
    public void UnelevatedStartRelaunchesOnceWithTheSameArgumentsAndALoopGuard()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: false,
            optOutVariable: null,
            arguments: ["--config", @"C:\temp\winland.conf"]);

        var relaunch = Assert.IsExactInstanceOfType<ElevationChoice.Relaunch>(choice);
        Assert.AreSequenceEqual(
            ["--winland-elevated", "--config", @"C:\temp\winland.conf"],
            relaunch.Arguments);
    }

    [TestMethod]
    public void ElevatedStartRunsAndDoesNotRelaunch()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: true,
            optOutVariable: null,
            arguments: ["--config", @"C:\temp\winland.conf"]);

        var run = Assert.IsExactInstanceOfType<ElevationChoice.Continue>(choice);
        Assert.AreSequenceEqual(["--config", @"C:\temp\winland.conf"], run.Arguments);
    }

    [TestMethod]
    public void LoopGuardStopsASecondRelaunchWhenElevationDidNotStick()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: false,
            optOutVariable: null,
            arguments: ["--winland-elevated", "--config", @"C:\temp\winland.conf"]);

        var refused = Assert.IsExactInstanceOfType<ElevationChoice.Refuse>(choice);
        Assert.AreEqual("Winland could not restart with administrator rights.", refused.Message);
    }

    [TestMethod]
    public void ElevatedRelaunchDropsTheLoopGuardAndRunsTheOriginalArguments()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: true,
            optOutVariable: null,
            arguments: ["--WINLAND-ELEVATED", "--config", @"C:\temp\winland.conf"]);

        var run = Assert.IsExactInstanceOfType<ElevationChoice.Continue>(choice);
        Assert.AreSequenceEqual(["--config", @"C:\temp\winland.conf"], run.Arguments);
    }

    [TestMethod]
    public void OptOutVariableStartsWithoutElevation()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: false,
            optOutVariable: "1",
            arguments: ["--config", @"C:\temp\winland.conf"]);

        var run = Assert.IsExactInstanceOfType<ElevationChoice.Continue>(choice);
        Assert.AreSequenceEqual(["--config", @"C:\temp\winland.conf"], run.Arguments);
    }

    [TestMethod]
    public void OptOutFlagStartsWithoutElevationAndIsNotPassedOn()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: false,
            optOutVariable: null,
            arguments: ["--no-elevate", "--config", @"C:\temp\winland.conf"]);

        var run = Assert.IsExactInstanceOfType<ElevationChoice.Continue>(choice);
        Assert.AreSequenceEqual(["--config", @"C:\temp\winland.conf"], run.Arguments);
    }

    [TestMethod]
    public void APerUserInstallNeverRelaunchesThroughUac()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: false,
            optOutVariable: null,
            arguments: [],
            userInstall: true);

        var run = Assert.IsExactInstanceOfType<ElevationChoice.Continue>(choice);
        Assert.IsEmpty(run.Arguments);
    }

    [TestMethod]
    public void ThePerUserFlagIsNotPassedOnToTheDaemon()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: false,
            optOutVariable: null,
            arguments: ["--no-elevate"],
            userInstall: true);

        var run = Assert.IsExactInstanceOfType<ElevationChoice.Continue>(choice);
        Assert.IsEmpty(run.Arguments);
    }

    [TestMethod]
    public void OtherOptOutValuesStillRelaunch()
    {
        var choice = ManualStartElevation.Decide(
            isElevated: false,
            optOutVariable: "true",
            arguments: []);

        var relaunch = Assert.IsExactInstanceOfType<ElevationChoice.Relaunch>(choice);
        Assert.AreSequenceEqual(["--winland-elevated"], relaunch.Arguments);
    }
}
