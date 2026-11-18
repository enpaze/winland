using System.IO.Pipes;

namespace Winland.Infrastructure.Tests;

public class CommandClientTests
{
    [Fact]
    public void TheDefaultPipeNameBelongsToTheCurrentSession()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();

        Assert.Equal("winland-" + process.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture), CommandClient.DefaultName);
    }

    [Fact]
    public void NothingAnsweringIsNoProcess()
    {
        var missing = "winland-missing-" + Guid.NewGuid().ToString("N");

        Assert.Null(CommandClient.ProcessId(missing, 200));
    }

    [Fact]
    public async Task TheProcessIsTheOneServingThePipe()
    {
        var name = "winland-test-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connected = server.WaitForConnectionAsync();

        var id = CommandClient.ProcessId(name);

        await connected.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Environment.ProcessId, id);
    }
}
