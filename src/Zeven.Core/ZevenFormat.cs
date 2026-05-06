using System.Buffers.Binary;
using System.IO.Hashing;

namespace Zeven.Core;

/// <summary>
/// Reads and writes the Zeven chunked wire format with CRC32 integrity checks.
/// </summary>
internal static class ZevenFormat
{
    static readonly byte[] Magic = "ZVN\x01"u8.ToArray();

    /// <summary>
    /// Writes the 16-byte stream header: magic + codec ID + property length + reserved +
    /// property header + CRC32.
    /// </summary>
    public static void WriteHeader(Stream output, ulong codecId,
            ReadOnlySpan<byte> propertyHeader)
    {
        output.Write(Magic);

        Span<byte> buf = stackalloc byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)codecId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[4..], (ushort)propertyHeader.Length);
        buf[6..].Clear();
        output.Write(buf);

        output.Write(propertyHeader);

        Span<byte> crcBuf = stackalloc byte[4];
        uint crc = Crc32.HashToUInt32(propertyHeader);
        BinaryPrimitives.WriteUInt32LittleEndian(crcBuf, crc);
        output.Write(crcBuf);
    }

    /// <summary>
    /// Reads and validates the stream header. Returns a <see cref="ZevenHeader"/> with
    /// the codec ID and property header bytes.
    /// </summary>
    public static ZevenHeader ReadHeader(Stream input)
    {
        Span<byte> magicBuf = stackalloc byte[4];
        ReadExactly(input, magicBuf);

        if (!magicBuf.SequenceEqual(Magic))
        {
            throw new InvalidDataException("Invalid Zeven format magic bytes.");
        }

        Span<byte> buf = stackalloc byte[12];
        ReadExactly(input, buf);
        ulong codecId = BinaryPrimitives.ReadUInt32LittleEndian(buf);
        ushort propLen = BinaryPrimitives.ReadUInt16LittleEndian(buf[4..]);

        byte[] propertyHeader = new byte[propLen];
        ReadExactly(input, propertyHeader);

        Span<byte> crcBuf = stackalloc byte[4];
        ReadExactly(input, crcBuf);
        uint storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(crcBuf);

        uint computedCrc = Crc32.HashToUInt32(propertyHeader);
        if (storedCrc != computedCrc)
        {
            throw new InvalidDataException("Property header CRC32 mismatch.");
        }

        return new ZevenHeader(codecId, propertyHeader);
    }

    /// <summary>
    /// Reads the header and validates that the codec ID matches the expected value.
    /// Throws <see cref="InvalidDataException"/> on mismatch.
    /// </summary>
    public static byte[] ReadHeaderAndValidateCodec(Stream input, ulong expectedCodecId)
    {
        var header = ReadHeader(input);
        if (header.CodecId != expectedCodecId)
        {
            throw new InvalidDataException(
                $"Codec mismatch: expected {CodecName(expectedCodecId)}, got {CodecName(header.CodecId)}.");
        }
        return header.PropertyHeader;
    }

    private static string CodecName(ulong codecId) => codecId switch
    {
        Interop.CodecId.Lzma2   => $"LZMA2 (0x{codecId:X})",
        Interop.CodecId.Lzma    => $"LZMA (0x{codecId:X})",
        Interop.CodecId.Ppmd    => $"PPMd (0x{codecId:X})",
        Interop.CodecId.Zstd    => $"Zstd (0x{codecId:X})",
        Interop.CodecId.Brotli  => $"Brotli (0x{codecId:X})",
        Interop.CodecId.Lz4     => $"LZ4 (0x{codecId:X})",
        Interop.CodecId.Lz5     => $"LZ5 (0x{codecId:X})",
        Interop.CodecId.Lizard  => $"Lizard (0x{codecId:X})",
        Interop.CodecId.Deflate => $"Deflate (0x{codecId:X})",
        Interop.CodecId.BZip2   => $"BZip2 (0x{codecId:X})",
        _                       => $"Unknown (0x{codecId:X})",
    };

    /// <summary>Writes a data chunk: sizes + compressed data + CRC32.</summary>
    public static void WriteChunk(Stream output, long uncompressedSize,
            ReadOnlySpan<byte> compressedData)
    {
        Span<byte> header = stackalloc byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(header, uncompressedSize);
        BinaryPrimitives.WriteInt64LittleEndian(header[8..], compressedData.Length);
        output.Write(header);
        output.Write(compressedData);

        uint crc = ComputeChunkCrc(header, compressedData);
        Span<byte> crcBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(crcBuf, crc);
        output.Write(crcBuf);
    }

    /// <summary>
    /// Reads the next chunk. Returns null on end marker (uncompressed size = 0).
    /// </summary>
    public static ChunkData? ReadChunk(Stream input)
    {
        Span<byte> header = stackalloc byte[16];
        ReadExactly(input, header);

        long uncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(header);
        long compressedSize = BinaryPrimitives.ReadInt64LittleEndian(header[8..]);

        if (uncompressedSize == 0)
        {
            if (compressedSize != 0)
            {
                throw new InvalidDataException(
                    "Invalid end marker: uncompressed size is 0 but compressed size is non-zero.");
            }
            return null;
        }

        if (uncompressedSize < 0)
        {
            throw new InvalidDataException("Uncompressed size is negative.");
        }

        if (compressedSize < 0)
        {
            throw new InvalidDataException("Compressed size is negative.");
        }

        if (compressedSize > int.MaxValue)
        {
            throw new InvalidDataException(
                "Compressed size exceeds maximum allowed allocation.");
        }

        byte[] compressedData = new byte[(int)compressedSize];
        ReadExactly(input, compressedData);

        Span<byte> crcBuf = stackalloc byte[4];
        ReadExactly(input, crcBuf);
        uint storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(crcBuf);

        uint computedCrc = ComputeChunkCrc(header, compressedData);
        if (storedCrc != computedCrc)
        {
            throw new InvalidDataException("Chunk CRC32 mismatch.");
        }

        return new ChunkData(uncompressedSize, compressedData);
    }

    /// <summary>Writes 16 zero bytes as the end-of-stream marker (matching the chunk header size).</summary>
    public static void WriteEndMarker(Stream output)
    {
        Span<byte> marker = stackalloc byte[16];
        marker.Clear();
        output.Write(marker);
    }

    static uint ComputeChunkCrc(ReadOnlySpan<byte> header, ReadOnlySpan<byte> compressedData)
    {
        var crc = new Crc32();
        crc.Append(header);
        crc.Append(compressedData);

        Span<byte> hashBuf = stackalloc byte[4];
        crc.GetHashAndReset(hashBuf);
        return BinaryPrimitives.ReadUInt32LittleEndian(hashBuf);
    }

    static void ReadExactly(Stream input, Span<byte> buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = input.Read(buffer[totalRead..]);
            if (read == 0)
            {
                throw new InvalidDataException("Unexpected end of stream.");
            }
            totalRead += read;
        }
    }
}

internal readonly record struct ChunkData(long UncompressedSize, byte[] CompressedData);

internal readonly record struct ZevenHeader(ulong CodecId, byte[] PropertyHeader);
