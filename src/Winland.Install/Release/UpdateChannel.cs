namespace Winland.Install;

public enum UpdateChannel
{
    Stable,
    Dev,
}

public static class UpdateChannels
{
    public static UpdateChannel? Parse(string word) =>
        word.Equals("stable", StringComparison.OrdinalIgnoreCase) ? UpdateChannel.Stable
        : word.Equals("dev", StringComparison.OrdinalIgnoreCase) ? UpdateChannel.Dev
        : null;

    public static string Name(UpdateChannel channel) => channel switch
    {
        UpdateChannel.Stable => "stable",
        UpdateChannel.Dev => "dev",
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };
}

public static class UpdateChannelFile
{
    public const string Name = "channel";

    public static string PathFor(string userProfile) => Path.Combine(InstallLocations.Config(userProfile), Name);

    public static UpdateChannel Choose(UpdateChannel? requested, string? saved) =>
        requested ?? Read(saved);

    public static UpdateChannel Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return UpdateChannel.Stable;

        var word = text.Trim().TrimStart('\uFEFF').Trim();
        if (word.Length == 0)
            return UpdateChannel.Stable;

        return UpdateChannels.Parse(word)
            ?? throw new InvalidOperationException("The saved update channel is not stable or dev.");
    }

    public static string Text(UpdateChannel channel) => UpdateChannels.Name(channel) + "\n";
}
