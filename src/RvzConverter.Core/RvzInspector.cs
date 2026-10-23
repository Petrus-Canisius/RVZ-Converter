using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RvzConverter.Core;

/// <summary>Reads only RVZ metadata, not the compressed payload. See Dolphin docs/WiaAndRvz.md.</summary>
public static class RvzInspector
{
    public static DiscInfo Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream);
    }

    public static DiscInfo Read(Stream stream)
        => ReadContainer(stream, ImageFormat.RVZ);

    public static DiscInfo ReadContainer(Stream stream, ImageFormat format)
    {
        try
        {
            stream.Position = 0;
            Span<byte> head = stackalloc byte[0x48];
            stream.ReadExactly(head);
            if (!head[..4].SequenceEqual(format == ImageFormat.WIA ? "WIA\x01"u8 : "RVZ\x01"u8))
                throw new ConversionException($"This file is not a recognized {format} container.");
            var compatible = BinaryPrimitives.ReadUInt32BigEndian(head[8..]);
            if (compatible > 0x01000000)
                throw new ConversionException("The image format version is too new. Update the application.");
            if (!SHA1.HashData(head[..0x34]).AsSpan().SequenceEqual(head[0x34..]))
                throw new ConversionException("Damaged image header: checksum mismatch.");
            var headerSize = BinaryPrimitives.ReadUInt32BigEndian(head[0x0c..]);
            if (headerSize < 0x90 || headerSize > 65536)
                throw new ConversionException("Unsupported image header size.");
            var isoSize = checked((long)BinaryPrimitives.ReadUInt64BigEndian(head[0x24..]));
            var storedSize = checked((long)BinaryPrimitives.ReadUInt64BigEndian(head[0x2c..]));
            if (storedSize != stream.Length || isoSize < 0x50000)
                throw new ConversionException("Incomplete image or inconsistent disc size.");
            byte[] disc = new byte[headerSize];
            stream.ReadExactly(disc);
            if (!SHA1.HashData(disc).AsSpan().SequenceEqual(head.Slice(0x10, 20)))
                throw new ConversionException("Damaged disc header: checksum mismatch.");
            var platform = DetectPlatform(disc.AsSpan(0x10, 0x80));
            var discType = BinaryPrimitives.ReadUInt32BigEndian(disc);
            if (discType != (platform == DiscPlatform.Wii ? 2u : 1u))
                throw new ConversionException("Contradictory disc platform information in the image header.");
            var title = Encoding.UTF8.GetString(disc, 0x30, 0x60).Split('\0')[0].Trim();
            var id = Encoding.ASCII.GetString(disc, 0x10, 6);
            if (id.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new ConversionException("Invalid disc identifier in image data.");
            return new(platform, isoSize, storedSize, title, id);
        }
        catch (Exception ex) when (ex is EndOfStreamException or OverflowException)
        {
            throw new ConversionException("Truncated image or invalid header.", ex);
        }
    }

    public static DiscPlatform DetectPlatform(ReadOnlySpan<byte> discHeader)
    {
        if (discHeader.Length >= 0x20)
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(discHeader[0x18..]) == 0x5D1C9EA3) return DiscPlatform.Wii;
            if (BinaryPrimitives.ReadUInt32BigEndian(discHeader[0x1c..]) == 0xC2339F3D) return DiscPlatform.GameCube;
        }
        throw new ConversionException("Unknown platform: no Wii or GameCube signature in the disc data.");
    }
}
