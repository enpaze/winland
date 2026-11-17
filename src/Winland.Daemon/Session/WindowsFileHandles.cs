using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Winland.Daemon;

[SupportedOSPlatform("windows")]
public static class WindowsFileHandles
{
    public const uint ReadAttributes = 0x0080;

    public const uint ReadControl = 0x00020000;

    public const uint Delete = 0x00010000;

    public const uint GenericRead = 0x80000000;

    public const uint GenericWrite = 0x40000000;

    private const uint OpenExisting = 3;
    private const uint CreateNew = 1;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint OwnerSecurity = 0x00000001;
    private const uint DaclSecurity = 0x00000004;
    private const int FileRenameInformation = 10;
    private const int FileDispositionInfo = 4;

    public static SafeFileHandle Open(string path, uint access, FileShare share, bool followReparse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Create(path, access, share, OpenExisting, BackupSemantics | (followReparse ? 0 : OpenReparsePoint));
    }

    public static SafeFileHandle CreateNewFile(string path, FileShare share)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Create(path, GenericRead | GenericWrite | Delete, share, CreateNew, OpenReparsePoint);
    }

    public static FileAttributes Attributes(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!GetFileInformationByHandle(handle, out var information))
            throw Failure(Marshal.GetLastPInvokeError(), "the open file");

        return (FileAttributes)information.FileAttributes;
    }

    public static string FinalPath(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var buffer = new char[512];
        while (true)
        {
            var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
            if (length == 0)
                throw Failure(Marshal.GetLastPInvokeError(), "the open file");

            if (length < buffer.Length)
            {
                var path = new string(buffer, 0, (int)length);
                return path.StartsWith(@"\\?\", StringComparison.Ordinal) && !path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)
                    ? path[4..]
                    : path;
            }

            buffer = new char[length + 1];
        }
    }

    public static byte[] ReadSecurity(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        const uint wanted = OwnerSecurity | DaclSecurity;
        _ = GetKernelObjectSecurity(handle, wanted, null, 0, out var needed);
        var error = Marshal.GetLastPInvokeError();
        if (needed == 0)
            throw Failure(error, "the open file");

        var descriptor = new byte[needed];
        if (!GetKernelObjectSecurity(handle, wanted, descriptor, needed, out _))
            throw Failure(Marshal.GetLastPInvokeError(), "the open file");

        return descriptor;
    }

    public static void Rename(SafeFileHandle file, SafeFileHandle directory, string name)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.AsSpan().ContainsAny(@"\/:"))
            throw new ArgumentException("The new name must be a single name.", nameof(name));

        var nameOffset = (IntPtr.Size * 2) + sizeof(uint);
        var size = nameOffset + ((name.Length + 1) * sizeof(char));
        var buffer = Marshal.AllocHGlobal(size);
        var status = new IntPtr[2];
        var added = false;
        try
        {
            directory.DangerousAddRef(ref added);
            for (var i = 0; i < size; i++)
                Marshal.WriteByte(buffer, i, 0);

            Marshal.WriteByte(buffer, 0, 1);
            Marshal.WriteIntPtr(buffer, IntPtr.Size, directory.DangerousGetHandle());
            Marshal.WriteInt32(buffer, IntPtr.Size * 2, name.Length * sizeof(char));
            Marshal.Copy(name.ToCharArray(), 0, buffer + nameOffset, name.Length);
            var result = NtSetInformationFile(file, status, buffer, (uint)size, FileRenameInformation);
            if (result < 0)
                throw Failure((int)RtlNtStatusToDosError(result), name);
        }
        finally
        {
            if (added)
                directory.DangerousRelease();

            Marshal.FreeHGlobal(buffer);
        }
    }

    public static void MarkForDeletion(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(buffer, 1);
            if (!SetFileInformationByHandle(handle, FileDispositionInfo, buffer, sizeof(int)))
                throw Failure(Marshal.GetLastPInvokeError(), "the open file");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static SafeFileHandle Create(string path, uint access, FileShare share, uint disposition, uint flags)
    {
        var handle = CreateFileW(path, access, (uint)share, IntPtr.Zero, disposition, flags, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw Failure(error, path);
        }

        return handle;
    }

    private static Exception Failure(int error, string what) => error switch
    {
        2 => new FileNotFoundException($"'{what}' was not found.", what),
        3 => new DirectoryNotFoundException($"A folder on the way to '{what}' was not found."),
        5 => new UnauthorizedAccessException($"Access to '{what}' was denied."),
        _ => new IOException($"'{what}': {Marshal.GetPInvokeErrorMessage(error)}", unchecked((int)0x80070000 | error)),
    };

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string name,
        uint access,
        uint share,
        IntPtr security,
        uint disposition,
        uint flags,
        IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, [Out] char[] path, uint length, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKernelObjectSecurity(SafeFileHandle handle, uint requested, [Out] byte[]? descriptor, uint length, out uint needed);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, IntPtr information, uint size);

    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationFile(SafeFileHandle handle, [Out] IntPtr[] status, IntPtr information, uint length, int informationClass);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);
}
