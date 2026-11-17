using System.Text;

namespace Winland.Install;

public closed record UpdateResult
{
    private UpdateResult()
    {
    }

    public sealed record NotInstalled : UpdateResult;

    public sealed record Current : UpdateResult;

    public sealed record NeedsAdministrator : UpdateResult;

    public sealed record Rejected : UpdateResult;

    public sealed record Updated(string Version) : UpdateResult;
}

public sealed class ReleaseUpdate
{
    private readonly IReleaseFeed feed;
    private readonly IReleasePackage package;
    private readonly AppInstall install;
    private readonly Func<string> stageDirectory;
    private readonly Action<string> removeStage;

    public ReleaseUpdate(
        IReleaseFeed feed,
        IReleasePackage package,
        AppInstall install,
        Func<string> stageDirectory,
        Action<string> removeStage)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(stageDirectory);
        ArgumentNullException.ThrowIfNull(removeStage);
        this.feed = feed;
        this.package = package;
        this.install = install;
        this.stageDirectory = stageDirectory;
        this.removeStage = removeStage;
    }

    public UpdateResult Run(
        string runningVersion,
        string executable,
        string machineDirectory,
        string userDirectory,
        bool elevated,
        UpdateChannel channel = UpdateChannel.Stable,
        string runningCommit = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runningVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDirectory);
        ArgumentNullException.ThrowIfNull(runningCommit);

        var kind = InstalledCopy.Of(executable, machineDirectory, userDirectory);
        if (kind == InstallKind.None)
            return new UpdateResult.NotInstalled();

        var listing = feed.Latest(channel);
        var current = channel switch
        {
            UpdateChannel.Stable => ReleaseVersion.IsCurrent(runningVersion, listing.Version),
            UpdateChannel.Dev => ReleaseVersion.IsSameCommit(runningCommit, listing.Commit),
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };
        if (current)
            return new UpdateResult.Current();

        var archive = feed.Download(listing.ArchiveUrl);
        var sums = Encoding.UTF8.GetString(feed.Download(listing.ChecksumsUrl));
        if (!ReleaseChecksum.Matches(archive, sums))
            return new UpdateResult.Rejected();

        if (kind == InstallKind.Machine && !elevated)
            return new UpdateResult.NeedsAdministrator();

        var directory = stageDirectory();
        try
        {
            var source = package.Extract(archive, directory);
            var perUser = kind == InstallKind.User;
            var destination = perUser ? userDirectory : machineDirectory;
            install.ReplaceInstalled(source, destination, perUser, listing.Version);
            return new UpdateResult.Updated(listing.Version);
        }
        finally
        {
            try
            {
                removeStage(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
