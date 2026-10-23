using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace RvzConverter.Core;

public sealed record VolumeSpace(string Id, string FileSystem, long AvailableBytes);
public interface IVolumeProbe { VolumeSpace Get(string existingDirectory); }

public sealed class WindowsVolumeProbe : IVolumeProbe
{
    public VolumeSpace Get(string existingDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return UnixVolume(existingDirectory);
        }
        var mount = new StringBuilder(1024);
        var volume = new StringBuilder(1024);
        var fs = new StringBuilder(64);
        if (!GetVolumePathName(existingDirectory, mount, mount.Capacity) ||
            !GetDiskFreeSpaceEx(existingDirectory, out ulong available, out _, out _) ||
            !GetVolumeInformation(mount.ToString(), null, 0, out _, out _, out _, fs, fs.Capacity))
            throw new ConversionException($"Cannot inspect volume '{existingDirectory}' (native error {Marshal.GetLastPInvokeError()}).");
        string id = GetVolumeNameForVolumeMountPoint(mount.ToString(), volume, volume.Capacity)
            ? volume.ToString() : mount.ToString();
        return new(id, fs.ToString(), checked((long)Math.Min(available, long.MaxValue)));
    }

    private static VolumeSpace UnixVolume(string path)
    {
        path = Path.GetFullPath(path);
        var mount = DriveInfo.GetDrives().Where(d => path == d.Name.TrimEnd('/') || path.StartsWith(d.Name.TrimEnd('/') + "/", StringComparison.Ordinal))
            .OrderByDescending(d => d.Name.Length).FirstOrDefault() ?? throw new ConversionException("Cannot identify the destination mount.");
        nint buffer = Marshal.AllocHGlobal(512);
        try
        {
            if (Stat(path, buffer) != 0) throw new ConversionException($"Cannot identify the filesystem device (native error {Marshal.GetLastPInvokeError()}).");
            ulong device = OperatingSystem.IsMacOS() ? unchecked((uint)Marshal.ReadInt32(buffer)) : unchecked((ulong)Marshal.ReadInt64(buffer));
            string format = mount.DriveFormat;
            if (format is "vfat" or "msdos" or "msdosfs") format = "FAT32";
            return new("dev:" + device, format, mount.AvailableFreeSpace);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int Stat([MarshalAs(UnmanagedType.LPUTF8Str)] string path, nint buffer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumePathNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathName(string path, StringBuilder volume, int length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumeNameForVolumeMountPointW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mount, StringBuilder volume, int length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetDiskFreeSpaceExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string path, out ulong available, out ulong total, out ulong free);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumeInformationW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(string path, StringBuilder? name, int nameSize,
        out uint serial, out uint maxLength, out uint flags, StringBuilder fs, int fsSize);
}

public sealed record SpacePlan(long TemporaryBytes, long OutputBytes, long SharedVolumeBytes);

public static class DiskSpace
{
    public const long Fat32MaxFile = 4L * 1024 * 1024 * 1024 - 1;
    public const long WbfsPartSize = 4000L * 1024 * 1024; // 4,194,304,000 < 2^32
    public const long Reserve = 64L * 1024 * 1024;

    public static SpacePlan Calculate(ImageInfo image, ConversionPlan plan, bool sameVolume)
    {
        checked
        {
            long output = image.Disc.IsoSize + 2 * Reserve;
            long temp = Reserve + (plan.CopySource ? image.Disc.SourceSize : 0) + (plan.IntermediateIso ? image.Disc.IsoSize : 0);
            if (plan.TemporaryOutput && !sameVolume) temp += output;
            return new(temp, output, temp + output);
        }
    }

    public static void Ensure(ImageInfo image, ConversionPlan route, ConversionOptions options, VolumeSpace temp, VolumeSpace output)
    {
        bool shared = temp.Id.Equals(output.Id, StringComparison.OrdinalIgnoreCase);
        var plan = Calculate(image, route, shared);
        bool outputFat = output.FileSystem.Equals("FAT32", StringComparison.OrdinalIgnoreCase);
        bool tempFat = temp.FileSystem.Equals("FAT32", StringComparison.OrdinalIgnoreCase);
        if (outputFat && options.Format == OutputFormat.ISO && image.Disc.IsoSize > Fat32MaxFile)
            throw new ConversionException("The ISO exceeds FAT32's 4 GiB minus 1 byte limit. Select NTFS/exFAT or split WBFS for Wii.");
        if (outputFat && options.Format is OutputFormat.CISO or OutputFormat.RVZ && plan.OutputBytes > Fat32MaxFile)
            throw new ConversionException("The potential unsplit output exceeds FAT32's file-size limit. Select NTFS/exFAT.");
        if (outputFat && options.Format == OutputFormat.WBFS && !options.SplitWbfs)
            throw new ConversionException("Enable WBFS splitting for FAT32 destinations.");
        if (tempFat && route.IntermediateIso && image.Disc.IsoSize > Fat32MaxFile)
            throw new ConversionException("The intermediate ISO exceeds the temporary volume's FAT32 limit.");
        if (tempFat && route.CopySource && image.Files.Any(f => new FileInfo(f).Length > Fat32MaxFile))
            throw new ConversionException("A source snapshot file exceeds the temporary volume's FAT32 limit.");
        if (tempFat && route.TemporaryOutput && options.Format != OutputFormat.WBFS && plan.OutputBytes > Fat32MaxFile)
            throw new ConversionException("The temporary output could exceed FAT32's file-size limit. Select an NTFS/exFAT temporary folder.");
        if (shared && Math.Min(temp.AvailableBytes, output.AvailableBytes) < plan.SharedVolumeBytes)
            throw new ConversionException($"Insufficient space on the shared volume: {Display.Bytes(plan.SharedVolumeBytes)} required for snapshots, intermediates, staging, and reserve.");
        if (!shared && temp.AvailableBytes < plan.TemporaryBytes)
            throw new ConversionException($"Insufficient temporary space: {Display.Bytes(plan.TemporaryBytes)} required.");
        if (!shared && output.AvailableBytes < plan.OutputBytes)
            throw new ConversionException($"Insufficient destination space: {Display.Bytes(plan.OutputBytes)} required.");
    }

}
