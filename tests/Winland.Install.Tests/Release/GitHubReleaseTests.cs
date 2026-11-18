using System.Net;
using System.Text;

namespace Winland.Install.Tests;

[TestClass]
public class GitHubReleaseTests
{
    private const string ZipUrl = "https://github.com/enpaze/winland/releases/download/v0.1.0/Winland-win-x64.zip";

    private const string SumsUrl = "https://github.com/enpaze/winland/releases/download/v0.1.0/SHA256SUMS";

    private const string VersionUrl = "https://github.com/enpaze/winland/releases/download/v0.1.0/version.json";

    [TestMethod]
    public void TheReleaseJsonNamesTheThreeAssets()
    {
        var parsed = GitHubReleaseParser.ReadRelease(ReleaseJson());

        Assert.AreEqual(ZipUrl, parsed.ArchiveUrl);
        Assert.AreEqual(SumsUrl, parsed.ChecksumsUrl);
        Assert.AreEqual(VersionUrl, parsed.VersionUrl);
        Assert.AreEqual("0.1.0", GitHubReleaseParser.ReadVersion("{\"commit\":\"abc\",\"date\":\"2026-10-21T00:00:00Z\",\"version\":\"0.1.0\"}\n"));
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void ADraftOrPrereleaseIsRefused(bool draft, bool prerelease)
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => GitHubReleaseParser.ReadRelease(ReleaseJson(draft, prerelease)));

        Assert.Contains("not a published release", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AnHttpDownloadIsRefused()
    {
        var json = ReleaseJson(zip: "http://github.com/enpaze/winland/releases/download/v0.1.0/Winland-win-x64.zip");

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => GitHubReleaseParser.ReadRelease(json));

        Assert.Contains("unsupported download", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AReleaseWithoutTheZipIsRefused()
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => GitHubReleaseParser.ReadRelease(ReleaseJson(zip: null)));

        Assert.Contains("Winland-win-x64.zip", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void VersionJsonWithoutAVersionIsRefused()
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => GitHubReleaseParser.ReadVersion("{\"commit\":\"abc\"}"));

        Assert.Contains("no version", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void LatestReadsTheApiThenVersionJsonAndNamesNoToken()
    {
        var handler = new RecordingHandler(url =>
        {
            if (url == ReleaseAssets.LatestUrl)
                return Text(ReleaseJson());

            if (url == VersionUrl)
                return Text("{\"commit\":\"abc\",\"date\":\"2026-10-21T00:00:00Z\",\"version\":\"0.2.0\"}\n");

            throw new InvalidOperationException(url);
        });
        using var http = new HttpClient(handler);

        var listing = new GitHubReleaseFeed(http).Latest(UpdateChannel.Stable);

        Assert.AreEqual("0.2.0", listing.Version);
        Assert.AreEqual(ZipUrl, listing.ArchiveUrl);
        Assert.AreEqual(SumsUrl, listing.ChecksumsUrl);
        Assert.AreSequenceEqual([ReleaseAssets.LatestUrl, VersionUrl], handler.Calls.Select(call => call.Url).ToArray());
        foreach (var call in handler.Calls)
{
    {
            Assert.AreEqual(ReleaseAssets.UserAgent, call.UserAgent);
            Assert.IsFalse(call.Authorized);
        }
}
        Assert.AreEqual("application/vnd.github+json", handler.Calls[0].Accept);
    }

    [TestMethod]
    public void DownloadFetchesTheNamedUrl()
    {
        var handler = new RecordingHandler(url =>
        {
            if (url == ZipUrl)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };

            throw new InvalidOperationException(url);
        });
        using var http = new HttpClient(handler);

        var bytes = new GitHubReleaseFeed(http).Download(ZipUrl);

        TestSequence.Equal((byte[])[1, 2, 3], bytes);
        var call = Assert.ContainsSingle(handler.Calls);
        Assert.AreEqual(ReleaseAssets.UserAgent, call.UserAgent);
        Assert.IsFalse(call.Authorized);
        Assert.AreEqual("application/octet-stream", call.Accept);
    }

    [TestMethod]
    public void AMissingReleaseIsRefusedBeforeAnyFileIsRead()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new GitHubReleaseFeed(http).Latest(UpdateChannel.Stable));

        Assert.Contains("No published release", error.Message, StringComparison.Ordinal);
        Assert.AreEqual(ReleaseAssets.LatestUrl, Assert.ContainsSingle(handler.Calls).Url);
    }

    [TestMethod]
    public void APrereleaseResponseDoesNotDownloadTheZip()
    {
        var handler = new RecordingHandler(url =>
        {
            if (url == ReleaseAssets.LatestUrl)
                return Text(ReleaseJson(prerelease: true));

            throw new InvalidOperationException(url);
        });
        using var http = new HttpClient(handler);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new GitHubReleaseFeed(http).Latest(UpdateChannel.Stable));

        Assert.Contains("not a published release", error.Message, StringComparison.Ordinal);
        Assert.ContainsSingle(handler.Calls);
    }

    [TestMethod]
    public void MalformedReleaseJsonIsRefused()
    {
        var handler = new RecordingHandler(_ => Text("not-json"));
        using var http = new HttpClient(handler);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new GitHubReleaseFeed(http).Latest(UpdateChannel.Stable));

        Assert.Contains("could not be read", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void DevReadsTheTagReleaseAndItsCommit()
    {
        var handler = new RecordingHandler(url =>
        {
            if (url == ReleaseAssets.DevUrl)
                return Text(ReleaseJson(prerelease: true));

            if (url == VersionUrl)
                return Text("{\"commit\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"date\":\"2026-10-21T00:00:00Z\",\"version\":\"0.1.0-dev.42+aaaaaaa\"}\n");

            throw new InvalidOperationException(url);
        });
        using var http = new HttpClient(handler);

        var listing = new GitHubReleaseFeed(http).Latest(UpdateChannel.Dev);

        Assert.AreEqual("0.1.0-dev.42+aaaaaaa", listing.Version);
        Assert.AreEqual("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", listing.Commit);
        Assert.AreSequenceEqual([ReleaseAssets.DevUrl, VersionUrl], handler.Calls.Select(call => call.Url).ToArray());
        foreach (var call in handler.Calls)
{
    Assert.IsFalse(call.Authorized);
}
    }

    [TestMethod]
    public void ADevReleaseWithoutACommitDoesNotDownloadTheZip()
    {
        var handler = new RecordingHandler(url =>
        {
            if (url == ReleaseAssets.DevUrl)
                return Text(ReleaseJson(prerelease: true));

            if (url == VersionUrl)
                return Text("{\"date\":\"2026-10-21T00:00:00Z\",\"version\":\"0.1.0-dev.1+aaaaaaa\"}\n");

            throw new InvalidOperationException(url);
        });
        using var http = new HttpClient(handler);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new GitHubReleaseFeed(http).Latest(UpdateChannel.Dev));

        Assert.Contains("no commit", error.Message, StringComparison.Ordinal);
        Assert.AreSequenceEqual([ReleaseAssets.DevUrl, VersionUrl], handler.Calls.Select(call => call.Url).ToArray());
    }

    [TestMethod]
    public void AnExhaustedRateLimitNamesItsResetTime()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("x-ratelimit-remaining", "0");
            response.Headers.Add("x-ratelimit-reset", "1793145600");
            return response;
        });
        using var http = new HttpClient(handler);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new GitHubReleaseFeed(http).Latest(UpdateChannel.Stable));

        Assert.Contains("GitHub API rate limit is used up", error.Message, StringComparison.Ordinal);
        Assert.Contains("2026-10-28 00:00:00 UTC", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AMissingDevReleaseIsRefused()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new GitHubReleaseFeed(http).Latest(UpdateChannel.Dev));

        Assert.Contains("No dev release", error.Message, StringComparison.Ordinal);
        Assert.AreEqual(ReleaseAssets.DevUrl, Assert.ContainsSingle(handler.Calls).Url);
    }

    [TestMethod]
    public void APrereleaseOnTheDevTagIsAcceptedAndADraftIsNot()
    {
        var parsed = GitHubReleaseParser.ReadRelease(ReleaseJson(prerelease: true), allowPrerelease: true);

        Assert.AreEqual(ZipUrl, parsed.ArchiveUrl);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            GitHubReleaseParser.ReadRelease(ReleaseJson(draft: true), allowPrerelease: true));
        Assert.Contains("draft", error.Message, StringComparison.Ordinal);
    }

    private static string ReleaseJson(bool draft = false, bool prerelease = false, string? zip = ZipUrl)
    {
        var assets = new List<string>
        {
            Asset(ReleaseAssets.ChecksumsName, SumsUrl),
            Asset(ReleaseAssets.VersionName, VersionUrl),
        };
        if (zip is not null)
            assets.Insert(0, Asset(ReleaseAssets.ArchiveName, zip));

        return "{\"draft\":" + (draft ? "true" : "false")
            + ",\"prerelease\":" + (prerelease ? "true" : "false")
            + ",\"assets\":[" + string.Join(',', assets) + "]}";
    }

    private static string Asset(string name, string url) =>
        "{\"name\":\"" + name + "\",\"browser_download_url\":\"" + url + "\"}";

    private static HttpResponseMessage Text(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<string, HttpResponseMessage> respond;

        public RecordingHandler(Func<string, HttpResponseMessage> respond)
        {
            this.respond = respond;
        }

        public List<Call> Calls { get; } = [];

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Answer(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Answer(request));

        private HttpResponseMessage Answer(HttpRequestMessage request)
        {
            var url = request.RequestUri?.AbsoluteUri ?? "";
            Calls.Add(new Call(
                url,
                request.Headers.TryGetValues("User-Agent", out var agents) ? string.Join(' ', agents) : null,
                request.Headers.Contains("Authorization"),
                request.Headers.TryGetValues("Accept", out var accept) ? string.Join(' ', accept) : null));
            return respond(url);
        }
    }

    private readonly record struct Call(string Url, string? UserAgent, bool Authorized, string? Accept);
}
