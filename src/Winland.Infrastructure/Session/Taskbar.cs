using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal static class Taskbar
{
    private const uint QueryStateMessage = 0x00000004;
    private const uint ChangeStateMessage = 0x0000000A;
    private const int AutoHide = 0x1;
    private const int AlwaysOnTop = 0x2;

    private static readonly Lock Gate = new();
    private static int? _savedState;

    internal static bool SessionHidden { get; private set; }

    internal static bool Present() => !Primary().IsNull;

    public static void Hide()
    {
        lock (Gate)
        {
            if (_savedState is null && Present())
                Save();

            SetState(AutoHide);
            SessionHidden = true;
            ConcealCore();
        }
    }

    internal static void Rehide()
    {
        lock (Gate)
        {
            if (!SessionHidden)
                return;

            if (Present())
            {
                if (_savedState is null)
                    Save();

                SetState(AutoHide);
            }

            ConcealCore();
        }
    }

    private static void Save()
    {
        if (MarkerPresent())
            Show(TryReadMarker(out var crashed) ? crashed : AlwaysOnTop);

        var state = ReadState();
        _savedState = state;
        WriteMarker(state);
    }

    internal static void Conceal()
    {
        lock (Gate)
            ConcealCore();
    }

    public static void Restore()
    {
        lock (Gate)
        {
            SessionHidden = false;
            var state = _savedState ?? (TryReadMarker(out var saved) ? saved : AlwaysOnTop);
            Show(state);
            _savedState = null;

            if (!Primary().IsNull)
                TryDeleteMarker();
        }
    }

    private static void ConcealCore()
    {
        if (!SessionHidden)
            return;

        foreach (var window in Windows())
            _ = PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_HIDE);
    }

    private static void Show(int state)
    {
        foreach (var window in Windows())
            _ = PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_SHOW);
        SetState(state);
    }

    private static int ReadState()
    {
        var data = NewData(Primary());
        return (int)Message(QueryStateMessage, ref data);
    }

    private static void SetState(int state)
    {
        var data = NewData(Primary());
        data.lParam = new LPARAM(state);
        _ = Message(ChangeStateMessage, ref data);
    }

    private static unsafe nuint Message(uint message, ref APPBARDATA data)
    {
        fixed (APPBARDATA* pointer = &data)
            return PInvoke.SHAppBarMessage(message, pointer);
    }

    private static APPBARDATA NewData(HWND window) => new()
    {
        cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
        hWnd = window,
    };

    private static List<HWND> Windows()
    {
        var windows = new List<HWND>();
        var primary = Primary();
        if (!primary.IsNull)
            windows.Add(primary);

        var after = HWND.Null;
        while (true)
        {
            var next = Find("Shell_SecondaryTrayWnd", after, child: true);
            if (next.IsNull)
                break;

            windows.Add(next);
            after = next;
        }

        return windows;
    }

    private static HWND Primary() => Find("Shell_TrayWnd", HWND.Null, child: false);

    private static unsafe HWND Find(string className, HWND after, bool child)
    {
        fixed (char* name = className)
        {
            return child
                ? PInvoke.FindWindowEx(HWND.Null, after, name, null)
                : PInvoke.FindWindow(name, null);
        }
    }

    internal static bool MarkerPresent()
    {
        try
        {
            return UserAccess.Run(() => File.Exists(MarkerPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static string MarkerFile(string userProfile, string? pipe = null)
    {
        var name = string.IsNullOrWhiteSpace(pipe)
            ? "taskbar.marker"
            : $"taskbar.{string.Join('_', pipe.Split(Path.GetInvalidFileNameChars()))}.marker";
        return Path.Combine(userProfile, ".winland", name);
    }

    private static string MarkerPath =>
        MarkerFile(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CommandClient.ConfiguredName());

    private static void WriteMarker(int state) => UserAccess.Run(() => WriteMarkerFile(state));

    private static void WriteMarkerFile(int state)
    {
        var path = MarkerPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (Redirected(path))
            return;

        TryDeleteMarker();
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(System.Text.Encoding.ASCII.GetBytes(state.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static bool Redirected(string path)
    {
        var folder = new DirectoryInfo(Path.GetDirectoryName(path)!);
        var marker = new FileInfo(path);
        return (folder.Exists && folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
            || (marker.Exists && marker.Attributes.HasFlag(FileAttributes.ReparsePoint));
    }

    private static bool TryReadMarker(out int state)
    {
        state = AlwaysOnTop;
        try
        {
            var text = UserAccess.Run(() => File.ReadAllText(MarkerPath)).Trim();
            return int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out state);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static void TryDeleteMarker()
    {
        try
        {
            UserAccess.Run(() =>
            {
                if (!Redirected(MarkerPath))
                    File.Delete(MarkerPath);
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
