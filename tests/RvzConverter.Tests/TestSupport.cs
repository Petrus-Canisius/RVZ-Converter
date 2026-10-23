using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using RvzConverter.Core;

namespace RvzConverter.Tests;
internal static class Assert
{
    public static void True(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    public static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}"); }
    public static T Throws<T>(Action action) where T : Exception { try { action(); } catch (T ex) { return ex; } throw new Exception($"Expected {typeof(T).Name}"); }
}
internal sealed class Fixture : IDisposable
{
    private static readonly string PhysicalTempDirectory = ResolvePhysicalDirectory(Path.GetTempPath());
    public string Root { get; } = Path.Combine(PhysicalTempDirectory, "rvz-tests-" + Guid.NewGuid().ToString("N"));
    public string Destination => Path.Combine(Root, "output [été] & 100% #");
    public string Temporary => Path.Combine(Root, "temporary [日本語] & %");
    public string Engine => Environment.ProcessPath!;
    public Fixture() { Directory.CreateDirectory(Destination); Directory.CreateDirectory(Temporary); }
    public ConversionOptions Options(OutputFormat format = OutputFormat.ISO) => new(Destination, Temporary, format, true, Engine, Engine);
    public string Source(ImageFormat format = ImageFormat.RVZ, DiscPlatform platform = DiscPlatform.Wii, string? name = null)
    {
        string path = Path.Combine(Root, name ?? $"source {Guid.NewGuid():N} [é 日本語] & %.{format.ToString().ToLowerInvariant()}");
        Write(path, format, platform); return path;
    }
    public static byte[] Header(DiscPlatform platform)
    {
        byte[] bytes = new byte[0x80]; Encoding.ASCII.GetBytes(platform == DiscPlatform.Wii ? "STEST1" : "GTEST1").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(platform == DiscPlatform.Wii ? 0x18 : 0x1c), platform == DiscPlatform.Wii ? 0x5D1C9EA3 : 0xC2339F3D);
        "Synthetic test fixture"u8.CopyTo(bytes.AsSpan(0x20)); return bytes;
    }
    public static void Write(string path, ImageFormat format, DiscPlatform platform, long size = 0x80000, bool split = false)
    {
        byte[] data;
        switch (format)
        {
            case ImageFormat.RVZ: case ImageFormat.WIA:
                data = new byte[0x48 + 0xdc]; Encoding.ASCII.GetBytes(format.ToString() + "\x01").CopyTo(data, 0);
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), 0x01000000); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), format == ImageFormat.RVZ ? 0x00030000u : 0x00090000u);
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0xc), 0xdc); BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(0x24), (ulong)size); BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(0x2c), (ulong)data.Length);
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x48), platform == DiscPlatform.Wii ? 2u : 1u);
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x4c), 5); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x50), 5); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x54), 131072);
                Header(platform).CopyTo(data, 0x58); SHA1.HashData(data.AsSpan(0x48)).CopyTo(data, 0x10); SHA1.HashData(data.AsSpan(0, 0x34)).CopyTo(data, 0x34); break;
            case ImageFormat.GCZ:
                data = new byte[0x80000 + 44]; BinaryPrimitives.WriteUInt32LittleEndian(data, 0xB10BC001); BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(16), 0x80000);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(24), 0x80000); BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(28), 1); BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(32), 1UL << 63); Header(platform).CopyTo(data, 44); break;
            case ImageFormat.CISO:
                data = new byte[0x10000]; "CISO"u8.CopyTo(data); BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 32768); data[8] = 1; Header(platform).CopyTo(data, 0x8000); break;
            case ImageFormat.WBFS:
                data = new byte[4096]; "WBFS"u8.CopyTo(data); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), split ? 16u : 8u); data[8] = 9; data[9] = 21; data[12] = 1; Header(platform).CopyTo(data, 512);
                if (split) File.WriteAllBytes(Path.ChangeExtension(path, ".wbf1"), new byte[4096]); break;
            default:
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) { stream.SetLength(size); stream.Write(Header(platform)); } return;
        }
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write); file.Write(data);
    }
    public void Clean() { Assert.Equal(0, Directory.GetFileSystemEntries(Temporary).Length); Assert.True(!Directory.GetDirectories(Destination).Any(p => Path.GetFileName(p).StartsWith(".rvz-stage-"))); }
    public void Dispose()
    {
        string full = Path.GetFullPath(Root); Assert.True(Path.GetFileName(full).StartsWith("rvz-tests-") && full.StartsWith(PhysicalTempDirectory, StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    private static string ResolvePhysicalDirectory(string path)
    {
        string full = Path.GetFullPath(path);
        if (!OperatingSystem.IsMacOS()) return full;

        string root = Path.GetPathRoot(full)!;
        var current = new DirectoryInfo(root);
        foreach (string segment in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = new DirectoryInfo(Path.Combine(current.FullName, segment));
            if (next.Exists && (next.Attributes & FileAttributes.ReparsePoint) != 0)
                current = next.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo
                    ?? throw new IOException($"Could not resolve temporary directory link '{next.FullName}'.");
            else
                current = next;
        }

        return Path.GetFullPath(current.FullName);
    }
}
internal sealed class FakeVolume(bool shared = true, string fs = "NTFS", long free = long.MaxValue) : IVolumeProbe
{ public VolumeSpace Get(string path) => new(shared ? "shared" : path, fs, free); }
internal sealed class FakeRunner : IProcessRunner
{
    public List<ProcessRequest> Calls { get; } = [];
    public Func<ProcessRequest, CancellationToken, Task<ProcessResult?>>? Intercept { get; set; }
    public bool Split { get; set; }
    public bool Preset { get; set; } = true;
    public async Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? log, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Calls.Add(request); var args = request.Arguments;
        if (Intercept is not null && await Intercept(request, token) is { } injected) return injected;
        if (args.Contains("--help") || args[0] is "HELP" or "TEST") return new(0, "--input --output --format --user --json --iso --ciso --wbfs --raw --psel --split-size --progress --sections --compression --compression_level --block_size", "");
        if (args[0] == "header") { var info = ImageInspector.Read(After(args, "--input")); return new(0, $"{{\"game_id\":\"{info.Disc.GameId}\"}}", ""); }
        if (args[0] == "convert")
        {
            string source = After(args, "--input"), output = After(args, "--output"); var image = ImageInspector.Read(source);
            if (!Preset && source.EndsWith("probe.iso")) return new(3, "", "Preset unsupported");
            Fixture.Write(output, Enum.Parse<ImageFormat>(After(args, "--format"), true), image.Disc.Platform, image.Disc.IsoSize);
            return new(0, "Simulated conversion", "");
        }
        if (args[0] == "COPY")
        {
            string source = Path.Combine(request.WorkingDirectory, args[1]); var image = ImageInspector.Read(source);
            string output = Path.Combine(request.WorkingDirectory, After(args, "--dest"));
            Fixture.Write(output, args.Contains("--iso") ? ImageFormat.ISO : args.Contains("--ciso") ? ImageFormat.CISO : ImageFormat.WBFS, image.Disc.Platform, 0x80000, Split && args.Contains("--wbfs"));
            foreach (string line in new[] { "[progress]", "verb=copied", "percent=42", "percent=100" }) request.StandardOutputLine?.Invoke(line);
            return new(0, "Simulated conversion", "");
        }
        return new(0, "WIT disc STEST1 GTEST1", "");
    }
    public static string After(IReadOnlyList<string> args, string name) => args[args.ToList().IndexOf(name) + 1];
}
internal sealed class SyncProgress(Action<ConversionUpdate> action) : IProgress<ConversionUpdate> { public void Report(ConversionUpdate value) => action(value); }
internal sealed class InjectedMover(Action<int, string, string> action) : IFileMover
{ private int count; public void Move(string source, string destination) => action(++count, source, destination); }
