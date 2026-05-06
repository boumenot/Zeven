using System.Buffers.Binary;
using System.IO.Hashing;

namespace Zeven.Core;

/// <summary>
/// Reads and writes the Zeven chunked PPMd wire format with CRC32 integrity checks.
/// </summary>
internal static class PpmdFormat
{
    internal const int PropertyHeaderSize = 5;

    static readonly byte[] Magic = "ZPM\x01"u8.ToArray();

    /// <summary>Writes stream header: magic + property header + CRC32.</summary>
    public static void WriteHeader(Stream output, ReadOnlySpan<byte> propertyHeader)
    {
        if (propertyHeader.Length != PropertyHeaderSize)
        {
            throw new ArgumentException(
                $"Property header must be {PropertyHeaderSize} bytes.",
                nameof(propertyHeader));
        }

        output.Write(Magic);
        output.Write(propertyHeader);

        Span<byte> crcBuf = stackalloc byte[4];
        uint crc = Crc32.HashToUInt32(propertyHeader);
        BinaryPrimitives.WriteUInt32LittleEndian(crcBuf, crc);
        output.Write(crcBuf);
    }

    /// <summary>
    /// Reads and validates stream header. Returns the 5-byte property header.
    /// </summary>
    public static byte[] ReadHeader(Stream input)
    {
        Span<byte> magicBuf = stackalloc byte[4];
        ReadExactly(input, magicBuf);

        if (!magicBuf.SequenceEqual(Magic))
        {
            throw new InvalidDataException("Invalid PPMd format magic bytes.");
        }

        byte[] propertyHeader = new byte[PropertyHeaderSize];
        ReadExactly(input, propertyHeader);

        Span<byte> crcBuf = stackalloc byte[4];
        ReadExactly(input, crcBuf);
        uint storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(crcBuf);

        uint computedCrc = Crc32.HashToUInt32(propertyHeader);
        if (storedCrc != computedCrc)
        {
            throw new InvalidDataException("Property header CRC32 mismatch.");
        }

        return propertyHeader;
    }

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
        if (uncompressedSize == 0)
        {
            return null;
        }

        long compressedSize = BinaryPrimitives.ReadInt64LittleEndian(header[8..]);
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
