using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace RvzConverter.Core;

public static class ExecutableCompatibility
{
    public static string? Problem(string path, DesktopPlatform? platform = null, Architecture? architecture = null, bool checkPermission = true)
    {
        try
        {
            if (!File.Exists(path)) return "Executable not found.";
            var expectedPlatform = platform ?? PlatformPaths.Current;
            var expectedArchitecture = architecture ?? RuntimeInformation.OSArchitecture;
            using var stream = File.OpenRead(path);
            byte[] head = new byte[64]; stream.ReadExactly(head);
            var architectures = new HashSet<Architecture>();
            DesktopPlatform actualPlatform;
            if (head[0] == 'M' && head[1] == 'Z')
            {
                actualPlatform = DesktopPlatform.Windows;
                stream.Position = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(60));
                byte[] pe = new byte[6]; stream.ReadExactly(pe);
                if (!pe.AsSpan(0, 4).SequenceEqual("PE\0\0"u8)) return "Invalid PE executable.";
                architectures.Add(BinaryPrimitives.ReadUInt16LittleEndian(pe.AsSpan(4)) switch { 0x8664 => Architecture.X64, 0xaa64 => Architecture.Arm64, _ => Architecture.X86 });
            }
            else if (head.AsSpan(0, 4).SequenceEqual(new byte[] { 0x7f, 69, 76, 70 }))
            {
                actualPlatform = DesktopPlatform.Linux;
                ushort machine = head[5] == 1 ? BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(18)) : BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(18));
                architectures.Add(machine switch { 62 => Architecture.X64, 183 => Architecture.Arm64, _ => Architecture.X86 });
            }
            else
            {
                actualPlatform = DesktopPlatform.MacOS;
                uint magic = BinaryPrimitives.ReadUInt32BigEndian(head);
                if (magic is 0xcafebabe or 0xcafebabf)
                {
                    uint count = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(4));
                    if (count > 64) return "Invalid universal Mach-O header.";
                    for (int i = 0; i < count; i++)
                    {
                        stream.Position = 8 + i * (magic == 0xcafebabe ? 20 : 32);
                        byte[] cpu = new byte[4]; stream.ReadExactly(cpu);
                        AddCpu(BinaryPrimitives.ReadUInt32BigEndian(cpu));
                    }
                }
                else if (magic is 0xcffaedfe or 0xcefaedfe) AddCpu(BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(4)));
                else if (magic is 0xfeedfacf or 0xfeedface) AddCpu(BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(4)));
                else return "A native PE, ELF, or Mach-O executable is required; script wrappers are not supported.";
                void AddCpu(uint cpu) { if (cpu == 0x01000007) architectures.Add(Architecture.X64); if (cpu == 0x0100000c) architectures.Add(Architecture.Arm64); }
            }
            if (actualPlatform != expectedPlatform) return $"Executable is for {actualPlatform}, not {expectedPlatform}.";
            if (!architectures.Contains(expectedArchitecture)) return $"No native {expectedArchitecture} slice. Emulation is not enabled.";
            if (checkPermission && !OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
                return "Execute permission is missing. Install the engine with executable permissions.";
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { return $"Cannot inspect the executable ({ex.GetType().Name})."; }
    }
}
