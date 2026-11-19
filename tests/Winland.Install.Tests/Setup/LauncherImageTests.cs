namespace Winland.Install.Tests;

[TestClass]
public class LauncherImageTests
{
    [TestMethod]
    public void TheLauncherImageIsAConsoleSubsystemWithADetachedConsoleAllocationPolicy()
    {
        const string policy = "<consoleAllocationPolicy xmlns=\"http://schemas.microsoft.com/SMI/2024/WindowsSettings\">detached</consoleAllocationPolicy>";
        var image = File.ReadAllBytes(LauncherExecutable());

        Assert.AreEqual(3, Subsystem(image));
        Assert.IsTrue(Contains(image, policy), "consoleAllocationPolicy detached is absent");
    }

    private static string LauncherExecutable()
    {
        var output = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var configuration = new DirectoryInfo(output).Parent!.Name;
        var root = output;
        while (!File.Exists(Path.Combine(root, "Winland.slnx")))
        {
            var parent = Path.GetDirectoryName(root);
            if (parent is null)
                throw new InvalidOperationException(output);
            root = parent;
        }

        var path = Path.Combine(root, "src", "Winland.Launcher", "bin", configuration, "net11.0-windows", "winland-launcher.exe");
        Assert.IsTrue(File.Exists(path), path);
        return path;
    }

    private static int Subsystem(byte[] image)
    {
        var optionalHeader = BitConverter.ToInt32(image, 0x3C) + 24;
        return BitConverter.ToUInt16(image, optionalHeader + 68);
    }

    private static bool Contains(byte[] image, string text)
    {
        var bytes = image.AsSpan();
        return bytes.IndexOf(System.Text.Encoding.UTF8.GetBytes(text)) >= 0
            || bytes.IndexOf(System.Text.Encoding.Unicode.GetBytes(text)) >= 0;
    }
}
