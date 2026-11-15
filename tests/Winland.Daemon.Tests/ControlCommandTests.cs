namespace Winland.Daemon.Tests;

[CollectionDefinition("Control output", DisableParallelization = true)]
public class ControlOutputCollection;

[Collection("Control output")]
public class ControlCommandTests
{
    [Theory]
    [InlineData("error: unknown dispatcher 'bogus'")]
    [InlineData("error: no such workspace")]
    [InlineData("")]
    public void ASingleFailureWritesToStandardError(string reply)
    {
        var result = WriteReply("dispatch bogus", reply);

        Assert.Equal(1, result.Exit);
        Assert.Equal("", result.Output);
        Assert.Equal((reply.Length == 0 ? "error: no reply" : reply) + Environment.NewLine, result.Error);
    }

    // A malformed selector fails the command. A well-formed one that matches nothing is an answer, not a failure.
    [Theory]
    [InlineData("inspect class:[", 1)]
    [InlineData("j/inspect pid:12a", 1)]
    [InlineData("inspect class:nothing", 0)]
    [InlineData("j/inspect class:nothing", 0)]
    public void InspectFailsOnlyForAMalformedSelector(string line, int expectedExit)
    {
        var reply = new WindowManager().Execute(line).Text;

        var result = WriteReply(line, reply);

        Assert.Equal(expectedExit, result.Exit);
        Assert.Equal(expectedExit == 1 ? reply + Environment.NewLine : "", result.Error);
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
