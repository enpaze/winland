using System.IO.Pipes;

namespace Winland.Infrastructure.Tests;

[TestClass]
public class CommandClientTests
{
    [TestMethod]
    public void TheDefaultPipeNameBelongsToTheCurrentSession()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();

        Assert.AreEqual("winland-" + process.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture), CommandClient.DefaultName);
    }

    [TestMethod]
    public void NothingAnsweringIsNoProcess()
    {
        var missing = "winland-missing-" + Guid.NewGuid().ToString("N");

        Assert.IsNull(CommandClient.ProcessId(missing, 200));
    }

    [TestMethod]
    public async Task TheProcessIsTheOneServingThePipe()
    {
        var name = "winland-test-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connected = server.WaitForConnectionAsync();

        var id = CommandClient.ProcessId(name);

        await connected.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(Environment.ProcessId, id);
    }
}
