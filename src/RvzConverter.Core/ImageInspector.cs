using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace RvzConverter.Core;

/// <summary>Container/header inspection, not full disc verification. No filename-based format/platform guesses.</summary>
public static class ImageInspector
{
    public const long WbfsVirtualSize = 143432L * 2 * 32768;
    public static ImageInfo Read(string path)
    {
        path = Path.GetFullPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] head = new byte[0x80];
        try
        {
            stream.ReadExactly(head);
            ImageFormat format;
            DiscInfo disc;
            IReadOnlyList<string> files = [path];
            if (head.AsSpan(0, 4).SequenceEqual("RVZ\x01"u8) || head.AsSpan(0, 4).SequenceEqual("WIA\x01"u8))
            {
                format = head[0] == 'R' ? ImageFormat.RVZ : ImageFormat.WIA;
                stream.Position = 0; disc = RvzInspector.ReadContainer(stream, format);
            }
            else if (BinaryPrimitives.ReadUInt32LittleEndian(head) == 0xB10BC001)
            {
                format = ImageFormat.GCZ;
                long size = checked((long)BinaryPrimitives.ReadUInt64LittleEndian(head.AsSpan(16)));
                uint block = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(24)), count = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(28));
                if (size < 0x50000 || block < 0x80 || block > 32 * 1024 * 1024 || count == 0 || count != (size + block - 1) / block)
                    throw new ConversionException("Invalid GCZ size or block table.");
                long dataOffset = checked(32L + count * 12L);
                ulong pointer = BinaryPrimitives.ReadUInt64LittleEndian(head.AsSpan(32));
                long offset = checked(dataOffset + (long)(pointer & ~(1UL << 63)));
                if (offset >= stream.Length) throw new ConversionException("The GCZ block table points outside the file.");
                stream.Position = offset;
                if ((pointer & (1UL << 63)) != 0) stream.ReadExactly(head);
                else { using var zlib = new ZLibStream(stream, CompressionMode.Decompress, leaveOpen: true); zlib.ReadExactly(head); }
                disc = FromHeader(head, size, stream.Length);
            }
            else if (head.AsSpan(0, 4).SequenceEqual("CISO"u8))
            {
                format = ImageFormat.CISO;
                uint block = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(4));
                if (block < 32768 || block > 16 * 1024 * 1024 || (block & (block - 1)) != 0)
                    throw new ConversionException("Unsupported CISO header. Wii/GameCube CISO is required; PSP CSO is not supported.");
                byte[] table = new byte[0x8000 - 8]; stream.Position = 8; stream.ReadExactly(table);
                if (table[0] != 1 || table.Any(v => v > 1) || 0x8000L + table.Count(v => v == 1) * (long)block > stream.Length)
                    throw new ConversionException("The Wii/GameCube CISO block map is invalid or incomplete.");
                stream.Position = 0x8000; stream.ReadExactly(head);
                // CISO carries no original length. Dolphin's reader exposes this conservative upper bound.
                disc = FromHeader(head, checked((long)table.Length * block), stream.Length, false) with
                {
                    // WIT 3.05 SetupCISO/SetupReadCISO uses the last mapped
                    // block and a single-layer Wii minimum. This is its
                    // materialized ISO length, not the original disc length.
                    WitIsoSize = Math.Max(checked((Array.FindLastIndex(table, value => value == 1) + 1L) * block), WbfsVirtualSize / 2)
                };
            }
            else if (head.AsSpan(0, 4).SequenceEqual("WBFS"u8))
            {
                format = ImageFormat.WBFS;
                int shift = head[8], clusterShift = head[9];
                if (shift is < 9 or > 20 || clusterShift is < 15 or > 31 || head[12] == 0)
                    throw new ConversionException("Invalid WBFS header or empty first disc slot.");
                int sector = 1 << shift;
                byte[] table = new byte[sector - 12]; stream.Position = 12; stream.ReadExactly(table);
                if (table.Skip(1).Any(v => v != 0)) throw new ConversionException("Multi-disc WBFS volumes are not supported. Select a single-disc WBFS image.");
                files = WbfsFiles(path);
                long sourceSize = files.Sum(f => new FileInfo(f).Length);
                if (sourceSize != BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(4)) * (long)sector)
                    throw new ConversionException("Incomplete WBFS image: its size does not match the header. Check all split parts.");
                stream.Position = sector; stream.ReadExactly(head);
                disc = FromHeader(head, WbfsVirtualSize, sourceSize, false);
            }
            else
            {
                format = ImageFormat.ISO; disc = FromHeader(head, stream.Length, stream.Length);
            }
            return new(format, disc, files);
        }
        catch (Exception ex) when (ex is EndOfStreamException or OverflowException or InvalidDataException)
        { throw new ConversionException("Truncated or invalid disc image header.", ex); }
    }
    public static DiscInfo FromHeader(ReadOnlySpan<byte> header, long isoSize, long sourceSize, bool exact = true)
    {
        if (isoSize < 0x50000) throw new ConversionException("The disc image is too small.");
        var platform = RvzInspector.DetectPlatform(header);
        string id = Encoding.ASCII.GetString(header[..6]);
        if (id.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new ConversionException("Invalid disc identifier in image data.");
        string title = Encoding.UTF8.GetString(header.Slice(0x20, Math.Min(0x60, header.Length - 0x20))).Split('\0')[0].Trim();
        return new(platform, isoSize, sourceSize, title, id, exact);
    }
    public static IReadOnlyList<string> WbfsFiles(string path)
    {
        var files = new List<string> { path };
        string stem = Path.GetFileNameWithoutExtension(path), directory = Path.GetDirectoryName(path)!;
        var parts = Directory.EnumerateFiles(directory).Where(f => SafeFiles.IsWbfsPart(Path.GetFileName(f), stem) && !PlatformPaths.PathComparer.Equals(f, path)).ToArray();
        for (int i = 1; i <= parts.Length; i++)
        {
            string expected = Path.Combine(directory, stem + $".wbf{i}");
            var found = parts.FirstOrDefault(f => Path.GetFileName(f).Equals(Path.GetFileName(expected), StringComparison.OrdinalIgnoreCase));
            if (found is null) throw new ConversionException("Missing or unexpectedly named WBFS split part.");
            files.Add(found);
        }
        if (parts.Length > 0 && !Path.GetExtension(path).Equals(".wbfs", StringComparison.OrdinalIgnoreCase))
            throw new ConversionException("Select a split WBFS image through its main .wbfs file.");
        return files;
    }
}
