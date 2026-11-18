using System.Diagnostics;
using Winland.Install;

namespace Winland.Daemon;

sealed class WindowsInstallFiles : IFileTree
{
    private int retirementScheduled;

    private string? imageDirectory;

    public bool Exists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<ListedFile> List(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        var root = Path.GetFullPath(directory);
        var listed = new List<ListedFile>();
        Walk(root, root, listed);
        return listed;
    }

    public void PrepareDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException($"Refusing to install into '{directory}': it is a link.");
    }

    public void Copy(string source, string destination)
    {
        var handle = WindowsFileHandles.Open(source, WindowsFileHandles.GenericRead, FileShare.Read, followReparse: false);
        var owned = true;
        string? aside = null;
        try
        {
            if (WindowsFileHandles.Attributes(handle).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException($"Refusing to install '{source}': it is a link.");

            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
                PrepareDirectory(parent);

            if (File.Exists(destination) || Directory.Exists(destination))
            {
                var attributes = File.GetAttributes(destination);
                if (attributes.HasFlag(FileAttributes.ReparsePoint) || attributes.HasFlag(FileAttributes.Directory))
                    throw new InvalidOperationException($"Refusing to install over '{destination}': it is a link or a folder.");

                try
                {
                    File.Delete(destination);
                }
                catch (Exception exception) when (exception is (IOException or UnauthorizedAccessException) && ImageDirectoryHolds(destination))
                {
                    aside = MoveAside(destination);
                }
            }

            using var input = new FileStream(handle, FileAccess.Read);
            owned = false;
            try
            {
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
            }
            catch when (aside is not null)
            {
                try
                {
                    File.Delete(destination);
                    File.Move(aside, destination);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }

                throw;
            }
        }
        finally
        {
            if (owned)
                handle.Dispose();
        }
    }

    public void DeleteFile(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when ((exception is IOException or UnauthorizedAccessException) && ImageDirectoryHolds(path))
        {
            MoveAside(path);
        }
    }

    public void DeleteTree(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
        {
            Directory.Delete(directory);
            return;
        }

        DeleteContents(directory, directory);
        try
        {
            Directory.Delete(directory, recursive: false);
        }
        catch (Exception exception) when (ImageIsInside(directory) && exception is (IOException or UnauthorizedAccessException))
        {
            ScheduleDelete(directory);
        }
    }

    private static void Walk(string root, string current, List<ListedFile> listed)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(current))
        {
            var relative = Path.GetRelativePath(root, entry);
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                listed.Add(new ListedFile(relative, true));
                continue;
            }

            if (attributes.HasFlag(FileAttributes.Directory))
                Walk(root, entry, listed);
            else
                listed.Add(new ListedFile(relative, false));
        }
    }

    private static void DeleteContents(string directory, string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                if (attributes.HasFlag(FileAttributes.Directory))
                    Directory.Delete(entry);
                else
                    File.Delete(entry);

                continue;
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                DeleteContents(entry, root);
                Directory.Delete(entry, recursive: false);
                continue;
            }

            try
            {
                File.Delete(entry);
            }
            catch (Exception exception) when (ImageIsInside(root) && exception is (IOException or UnauthorizedAccessException))
            {
            }
        }
    }

    private bool ImageDirectoryHolds(string path)
    {
        var root = ImageDirectory();
        if (root is null)
            return false;

        var full = Path.GetFullPath(path);
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private string? ImageDirectory()
    {
        if (imageDirectory is not null)
            return imageDirectory;

        var image = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(image))
            return null;

        imageDirectory = Path.GetDirectoryName(Path.GetFullPath(image));
        return imageDirectory;
    }

    private string MoveAside(string destination)
    {
        var aside = destination + ".winland-old";
        if (File.Exists(aside) || Directory.Exists(aside))
        {
            var attributes = File.GetAttributes(aside);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException($"Refusing to install over '{aside}': it is a link.");

            var removed = false;
            if (!attributes.HasFlag(FileAttributes.Directory))
            {
                try
                {
                    File.Delete(aside);
                    removed = true;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }
            }

            if (!removed)
                aside = destination + ".winland-old-" + Guid.NewGuid().ToString("N");
        }

        File.Move(destination, aside);
        var directory = ImageDirectory();
        if (directory is not null)
            ScheduleRetirement(directory);

        return aside;
    }

    private void ScheduleRetirement(string directory)
    {
        if (Interlocked.Exchange(ref retirementScheduled, 1) != 0)
            return;

        StartCleanupScript(RetirementScript(Environment.ProcessId, directory));
    }

    internal static string RetirementScript(int processId, string directory) =>
        "Wait-Process -Id " + processId + " -ErrorAction SilentlyContinue\r\n"
        + "function Test-RetiredName([string]$Name) {\r\n"
        + "  $marker = '.winland-old'\r\n"
        + "  $at = $Name.LastIndexOf($marker)\r\n"
        + "  if ($at -lt 0) { return $false }\r\n"
        + "  $rest = $Name.Substring($at + $marker.Length)\r\n"
        + "  if ($rest.Length -eq 0) { return $true }\r\n"
        + "  if ($rest.Length -ne 33 -or -not $rest.StartsWith('-')) { return $false }\r\n"
        + "  foreach ($c in $rest.Substring(1).ToCharArray()) {\r\n"
        + "    $hex = ($c -ge '0' -and $c -le '9') -or ($c -ge 'a' -and $c -le 'f') -or ($c -ge 'A' -and $c -le 'F')\r\n"
        + "    if (-not $hex) { return $false }\r\n"
        + "  }\r\n"
        + "  return $true\r\n"
        + "}\r\n"
        + "function Remove-Retired([string]$Path) {\r\n"
        + "  if (-not (Test-Path -LiteralPath $Path)) { return }\r\n"
        + "  $attributes = [IO.File]::GetAttributes($Path)\r\n"
        + "  $link = ($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0\r\n"
        + "  $container = ($attributes -band [IO.FileAttributes]::Directory) -ne 0\r\n"
        + "  if ($link) { return }\r\n"
        + "  if ($container) {\r\n"
        + "    foreach ($child in [IO.Directory]::EnumerateFileSystemEntries($Path)) { Remove-Retired $child }\r\n"
        + "    return\r\n"
        + "  }\r\n"
        + "  if (Test-RetiredName([IO.Path]::GetFileName($Path))) {\r\n"
        + "    try {\r\n"
        + "      [IO.File]::SetAttributes($Path, [IO.FileAttributes]::Normal)\r\n"
        + "      [IO.File]::Delete($Path)\r\n"
        + "    } catch {\r\n"
        + "      $reason = $_.Exception\r\n"
        + "      while ($null -ne $reason.InnerException) { $reason = $reason.InnerException }\r\n"
        + "      if (($reason -is [System.IO.IOException]) -or ($reason -is [System.UnauthorizedAccessException])) { return }\r\n"
        + "      throw\r\n"
        + "    }\r\n"
        + "  }\r\n"
        + "}\r\n"
        + "Remove-Retired " + Quote(directory) + "\r\n"
        + "Remove-Item -LiteralPath $PSCommandPath -Force\r\n"
        + "Remove-Item -LiteralPath $PSScriptRoot -Force\r\n";

    internal static void DeleteRetired(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        try
        {
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
                return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        DeleteRetiredEntries(directory);
    }

    private static void DeleteRetiredEntries(string directory)
    {
        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(entry);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                DeleteRetiredEntries(entry);
                continue;
            }

            if (!IsRetiredName(Path.GetFileName(entry)))
                continue;

            try
            {
                File.SetAttributes(entry, FileAttributes.Normal);
                File.Delete(entry);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool IsRetiredName(string fileName)
    {
        const string marker = ".winland-old";
        var at = fileName.LastIndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
            return false;

        var rest = fileName.AsSpan(at + marker.Length);
        if (rest.IsEmpty)
            return true;

        if (rest.Length != 33 || rest[0] != '-')
            return false;

        for (var i = 1; i < rest.Length; i++)
        {
            var c = rest[i];
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F')))
                return false;
        }

        return true;
    }

    private static bool ImageIsInside(string directory)
    {
        var image = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(image))
            return false;

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(image).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static void ScheduleDelete(string directory)
    {
        var script = "$root = " + Quote(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar) + "\r\n"
            + "Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) } | Wait-Process\r\n"
            + "function Remove-Tree([string]$Path) {\r\n"
            + "  if (-not (Test-Path -LiteralPath $Path)) { return }\r\n"
            + "  $attributes = [IO.File]::GetAttributes($Path)\r\n"
            + "  $link = ($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0\r\n"
            + "  $container = ($attributes -band [IO.FileAttributes]::Directory) -ne 0\r\n"
            + "  if ($link) {\r\n"
            + "    if ($container) { [IO.Directory]::Delete($Path) } else { [IO.File]::Delete($Path) }\r\n"
            + "    return\r\n"
            + "  }\r\n"
            + "  if ($container) {\r\n"
            + "    foreach ($child in [IO.Directory]::EnumerateFileSystemEntries($Path)) { Remove-Tree $child }\r\n"
            + "    [IO.Directory]::Delete($Path)\r\n"
            + "  } else { [IO.File]::Delete($Path) }\r\n"
            + "}\r\n"
            + "Remove-Tree " + Quote(directory) + "\r\n"
            + "Remove-Item -LiteralPath $PSCommandPath -Force\r\n"
            + "Remove-Item -LiteralPath $PSScriptRoot -Force\r\n";

        StartCleanupScript(script);
    }

    private static void StartCleanupScript(string script)
    {
        var folder = AdministratorFolder.CreateTemp("winland-");
        var path = Path.Combine(folder, "remove.ps1");
        File.WriteAllText(path, script);
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = powershell,
            ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", path },
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        if (process is null)
            throw new InvalidOperationException("Could not remove the install folder after Winland exits.");
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
