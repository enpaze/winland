namespace Winland.Daemon.Tests;
[TestClass]
public class ControlCommandTests
{
    [TestMethod]
    [DataRow("error: unknown dispatcher 'bogus'")]
    [DataRow("error: no such workspace")]
    [DataRow("")]
    public void ASingleFailureWritesToStandardError(string reply)
    {
        var result = WriteReply("dispatch bogus", reply);

        Assert.AreEqual(1, result.Exit);
        Assert.AreEqual("", result.Output);
        Assert.AreEqual((reply.Length == 0 ? "error: no reply" : reply) + Environment.NewLine, result.Error);
    }

    [TestMethod]
    [DataRow("inspect address:zz", 1)]
    [DataRow("j/inspect pid:12a", 1)]
    [DataRow("inspect class:nothing", 0)]
    [DataRow("j/inspect class:nothing", 0)]
    public void InspectFailsOnlyForAMalformedSelector(string line, int expectedExit)
    {
        var reply = new WindowManager().Execute(line).Text;

        var result = WriteReply(line, reply);

        Assert.AreEqual(expectedExit, result.Exit);
        Assert.AreEqual(expectedExit == 1 ? reply + Environment.NewLine : "", result.Error);
    }

    private static (int Exit, string Output, string Error) WriteReply(string line, string reply)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exit = ControlCommand.WriteReply(line, reply);
            return (exit, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }
}
