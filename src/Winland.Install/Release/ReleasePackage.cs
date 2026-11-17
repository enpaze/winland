using System.IO.Compression;

namespace Winland.Install;

public interface IReleasePackage
{
    string Extract(byte[] archive, string directory);
}

public sealed class ZipReleasePackage : IReleasePackage
{
    public string Extract(byte[] archive, string directory)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;

        using var stream = new MemoryStream(archive, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.Length == 0 || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                continue;

            var destination = Path.GetFullPath(Path.Combine(directory, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Refusing to unpack '{entry.FullName}'.");

            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            entry.ExtractToFile(destination);
        }

        var executable = InstallLocations.Executable(Path.Combine(directory, "winland"));
        if (!File.Exists(executable))
            throw new InvalidOperationException("The release zip has no winland\\winland.exe.");

        return executable;
    }
}
