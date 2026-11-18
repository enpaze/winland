using System.Diagnostics;

namespace Winland.Daemon.Tests;

public sealed class InstallFilesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "winland-install-" + Guid.NewGuid().ToString("N"));
    private readonly WindowsInstallFiles files = new();

    public InstallFilesTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
            files.DeleteTree(root);
    }

    [Fact]
    public void ALinkIsListedAndNotFollowed()
    {
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        var linked = Path.Combine(source, "linked");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "winland.exe"), "daemon");
        File.WriteAllText(Path.Combine(target, "secret.txt"), "secret");
        Junction(linked, target);

        var listed = files.List(source);

        Assert.Contains(listed, file => file.RelativePath == "winland.exe" && !file.ReparsePoint);
        Assert.Contains(listed, file => file.RelativePath == "linked" && file.ReparsePoint);
        Assert.DoesNotContain(listed, file => file.RelativePath.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void CopyRefusesALink()
    {
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "winland.exe"), "daemon");
        Junction(source, target);

        var error = Assert.Throws<InvalidOperationException>(() => files.Copy(source, Path.Combine(root, "copied.exe")));

        Assert.Contains("it is a link", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "copied.exe")));
    }

    [Fact]
    public void DeleteRemovesALinkAndLeavesItsTarget()
    {
        var tree = Path.Combine(root, "tree");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(tree);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(tree, "winland.exe"), "daemon");
        File.WriteAllText(Path.Combine(target, "secret.txt"), "secret");
        Junction(Path.Combine(tree, "linked"), target);

        files.DeleteTree(tree);

        Assert.False(Directory.Exists(tree));
        Assert.Equal("secret", File.ReadAllText(Path.Combine(target, "secret.txt")));
    }

    [Fact]
    public void CopyWritesTheFileItself()
    {
        var source = Path.Combine(root, "winland.exe");
        var destination = Path.Combine(root, "out", "winland.exe");
        File.WriteAllText(source, "daemon");

        files.Copy(source, destination);

        Assert.Equal("daemon", File.ReadAllText(destination));
    }

    [Fact]
    public void CopyReplacesAnExistingFile()
    {
        var source = Path.Combine(root, "new.exe");
        var destination = Path.Combine(root, "old.exe");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");

        files.Copy(source, destination);

        Assert.Equal("new", File.ReadAllText(destination));
        Assert.False(File.Exists(destination + ".winland-old"));
    }

    [Fact]
    public void CopyRefusesALockedFileOutsideTheRunningFolder()
    {
        var source = Path.Combine(root, "new.exe");
        var destination = Path.Combine(root, "locked.exe");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");
        using var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None);

        var error = Record.Exception(() => files.Copy(source, destination));

        Assert.True(error is IOException or UnauthorizedAccessException);
        locked.Dispose();
        Assert.Equal("old", File.ReadAllText(destination));
        Assert.False(File.Exists(destination + ".winland-old"));
    }

    [Fact]
    public void RetirementStaysQuietWhileFilesAreLockedAndRemovesThemOnceUnlocked()
    {
        var directory = Path.Combine(root, "install");
        Directory.CreateDirectory(directory);
        var kept = Path.Combine(directory, "winland.exe");
        var free = Path.Combine(directory, "free.dll.winland-old");
        var held = Path.Combine(directory, "held.dll.winland-old");
        File.WriteAllText(kept, "current");
        File.WriteAllText(free, "old");
        File.WriteAllText(held, "old");
        var holder = Path.Combine(directory, "holder.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), holder);

        var mapped = Path.Combine(directory, "mapped.exe.winland-old");
        var nested = Path.Combine(directory, "native", "helper.dll.winland-old");
        var stamped = Path.Combine(directory, "plugin.dll.winland-old-" + Guid.NewGuid().ToString("N"));
        var decoy = Path.Combine(directory, "notes.winland-old.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(nested, "old");
        File.WriteAllText(stamped, "old");
        File.WriteAllText(decoy, "keep");
        Process? running = null;
        FileStream? stream = null;
        try
        {
            running = Process.Start(new ProcessStartInfo
            {
                FileName = holder,
                ArgumentList = { "/c", "ping", "-n", "30", "127.0.0.1" },
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            Assert.NotNull(running);
            Assert.False(running.WaitForExit(300));
            File.Move(holder, mapped);
            stream = new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None);
            Assert.IsType<IOException>(Record.Exception(() => File.Delete(held)));
            Assert.IsType<UnauthorizedAccessException>(Record.Exception(() => File.Delete(mapped)));

            using var exited = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                ArgumentList = { "/c", "exit" },
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            Assert.NotNull(exited);
            exited.WaitForExit();

            var output = RunRetirementScript(WindowsInstallFiles.RetirementScript(exited.Id, directory));

            Assert.Equal("", output);
            Assert.Equal("current", File.ReadAllText(kept));
            Assert.Equal("keep", File.ReadAllText(decoy));
            Assert.False(File.Exists(free));
            Assert.False(File.Exists(nested));
            Assert.False(File.Exists(stamped));
            Assert.True(File.Exists(held));
            Assert.True(File.Exists(mapped));
        }
        finally
        {
            stream?.Dispose();
            if (running is { HasExited: false })
            {
                running.Kill(entireProcessTree: true);
                running.WaitForExit();
            }

            running?.Dispose();
        }

        WindowsInstallFiles.DeleteRetired(directory);
        Assert.False(File.Exists(held));
        Assert.False(File.Exists(mapped));
        Assert.Equal("current", File.ReadAllText(kept));
        Assert.Equal("keep", File.ReadAllText(decoy));
        WindowsInstallFiles.DeleteRetired(directory);
        Assert.False(File.Exists(held));
        Assert.False(File.Exists(mapped));
        Assert.Equal("current", File.ReadAllText(kept));
        Assert.Equal("keep", File.ReadAllText(decoy));
    }

    [Fact]
    public void TheNextLaunchRemovesAnUnlockedRetiredFileAndLeavesALockedOne()
    {
        var host = Path.GetDirectoryName(typeof(WindowsInstallFiles).Assembly.Location)!;
        var install = Path.Combine(root, "launched");
        CopyHost(host, install);
        var retired = Path.Combine(install, "native", "helper.dll.winland-old");
        var held = Path.Combine(install, "held.dll.winland-old");
        var decoy = Path.Combine(install, "notes.winland-old.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(retired)!);
        File.WriteAllText(retired, "old");
        File.WriteAllText(held, "old");
        File.WriteAllText(decoy, "keep");
        using var stream = new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None);

        var first = RunInstalled(install, Path.Combine(install, "absent.conf"));

        Assert.Contains("Could not read config", first, StringComparison.Ordinal);
        Assert.DoesNotContain("UnauthorizedAccessException", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception calling \"Delete\"", first, StringComparison.Ordinal);
        Assert.False(File.Exists(retired));
        Assert.True(File.Exists(held));
        Assert.Equal("keep", File.ReadAllText(decoy));

        stream.Dispose();
        var second = RunInstalled(install, Path.Combine(install, "absent.conf"));

        Assert.Contains("Could not read config", second, StringComparison.Ordinal);
        Assert.DoesNotContain("UnauthorizedAccessException", second, StringComparison.Ordinal);
        Assert.False(File.Exists(held));
        Assert.Equal("keep", File.ReadAllText(decoy));
    }

    private static string RunInstalled(string install, string config)
    {
        var start = new ProcessStartInfo(Path.Combine(install, "winland.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = install,
        };
        start.ArgumentList.Add("check");
        start.ArgumentList.Add(config);
        start.Environment["WINLAND_NO_ELEVATE"] = "1";
        using var process = Process.Start(start);
        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(30_000));
        return stdout + stderr;
    }

    private static void CopyHost(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var name in new[]
        {
            "winland.exe",
            "winland.dll",
            "winland.deps.json",
            "winland.runtimeconfig.json",
            "Winland.Application.dll",
            "Winland.Domain.dll",
            "Winland.Infrastructure.dll",
            "Winland.Install.dll",
            "Microsoft.Win32.SystemEvents.dll",
            "Microsoft.Windows.SDK.NET.dll",
            "WinRT.Runtime.dll",
        })
        {
            var from = Path.Combine(source, name);
            Assert.True(File.Exists(from), from);
            using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var output = File.Create(Path.Combine(destination, name));
            input.CopyTo(output);
        }

        CopyTree(Path.Combine(source, "runtimes"), Path.Combine(destination, "runtimes"));
    }

    private static void CopyTree(string source, string destination)
    {
        Assert.True(Directory.Exists(source), source);
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));

        foreach (var file in Directory.EnumerateFiles(source))
        {
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var output = File.Create(Path.Combine(destination, Path.GetFileName(file)));
            input.CopyTo(output);
        }
    }

    private static string RunRetirementScript(string script)
    {
        var folder = Path.Combine(Path.GetTempPath(), "winland-remove-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "remove.ps1");
        File.WriteAllText(path, script);
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = powershell,
            ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", path },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return stdout + stderr;
    }

    private static void Junction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, target })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
