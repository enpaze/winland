using System.Text;

namespace Winland.Application.Tests;

[TestClass]
public class PipeFrameTests
{
    [TestMethod]
    [DataRow("ok")]
    [DataRow("")]
    [DataRow("first\nsecond\n\nfourth")]
    [DataRow("#42")]
    public void AFramedReplyReadsBackAsItWas(string reply)
    {
        using var stream = new MemoryStream(PipeFrame.Frame(reply));

        Assert.AreEqual(reply, PipeFrame.ReadReply(stream));
    }

    [TestMethod]
    public void AClosedPipeIsNoReply()
    {
        using var stream = new MemoryStream();

        Assert.IsNull(PipeFrame.ReadReply(stream));
    }

    [TestMethod]
    public void AReplyWithANewlineIsSentWithItsLength()
    {
        var framed = Encoding.UTF8.GetString(PipeFrame.Frame("a\nb"));

        Assert.AreEqual("#3\na\nb", framed);
    }

    [TestMethod]
    public void AReplyCutShortIsAnError()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("#10\nabc"));

        Assert.ThrowsExactly<EndOfStreamException>(() => PipeFrame.ReadReply(stream));
    }

    [TestMethod]
    public void AReplyOverTheLimitIsAnError()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"#{PipeFrame.ReplyLimit + 1}\nabc"));

        Assert.ThrowsExactly<InvalidDataException>(() => PipeFrame.ReadReply(stream));
    }

    [TestMethod]
    public async Task ALineDropsItsCarriageReturn()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("clients\r\nnext\n"));

        Assert.AreEqual("clients", await PipeFrame.ReadLineAsync(stream, PipeFrame.RequestLimit));
        Assert.AreEqual("next", await PipeFrame.ReadLineAsync(stream, PipeFrame.RequestLimit));
        Assert.IsNull(await PipeFrame.ReadLineAsync(stream, PipeFrame.RequestLimit));
    }

    [TestMethod]
    public async Task ALineOverTheLimitIsRefused()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 11) + "\n"));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => PipeFrame.ReadLineAsync(stream, 10));
    }
}
