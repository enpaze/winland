using System.Xml.Linq;

namespace Winland.Install.Tests;

[TestClass]
public class LogonTaskDefinitionTests
{
    [TestMethod]
    public void LogonTaskRunsAtLogonWithHighestPrivilegesForTheUser()
    {
        var task = LogonTaskDefinition.Build(@"C:\Tools & Games\winland.exe", @"DESKTOP\dante");

        Assert.AreEqual("Winland", task.Name);
        Assert.AreEqual(@"C:\Tools & Games\winland.exe", task.Executable);
        Assert.AreEqual(@"DESKTOP\dante", task.User);

        var root = XDocument.Parse(task.Xml).Root;
        Assert.IsNotNull(root);
        Assert.AreEqual("Task", root.Name.LocalName);
        Assert.ContainsSingle(element => element.Name.LocalName == "LogonTrigger", root.Descendants());
        Assert.ContainsSingle(element => element.Name.LocalName == "Exec", root.Descendants());

        var trigger = root.Descendants().Single(element => element.Name.LocalName == "LogonTrigger");
        Assert.AreEqual("true", Child(trigger, "Enabled"));
        Assert.AreEqual(@"DESKTOP\dante", Child(trigger, "UserId"));

        var principal = root.Descendants().Single(element => element.Name.LocalName == "Principal");
        Assert.AreEqual(@"DESKTOP\dante", Child(principal, "UserId"));
        Assert.AreEqual("InteractiveToken", Child(principal, "LogonType"));
        Assert.AreEqual("HighestAvailable", Child(principal, "RunLevel"));

        var command = root.Descendants().Single(element => element.Name.LocalName == "Command");
        Assert.AreEqual(@"C:\Tools & Games\winland-launcher.exe", command.Value);

        Assert.AreEqual("Start Winland at logon.", Text(root, "Description"));
        Assert.AreEqual("IgnoreNew", Text(root, "MultipleInstancesPolicy"));
        Assert.AreEqual("false", Text(root, "DisallowStartIfOnBatteries"));
        Assert.AreEqual("false", Text(root, "StopIfGoingOnBatteries"));
        Assert.AreEqual("PT0S", Text(root, "ExecutionTimeLimit"));
    }

    private static string Child(XElement parent, string localName) =>
        parent.Elements().Single(element => element.Name.LocalName == localName).Value;

    private static string Text(XElement root, string localName) =>
        root.Descendants().Single(element => element.Name.LocalName == localName).Value;
}
