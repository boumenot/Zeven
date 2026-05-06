using System.IO.Compression;
using Zeven.Core;
using Zeven.Core.Interop;

namespace Zeven.Tests;

public class PpmdCodecTests
{
    const string DllPath = @"q:\7z2601-bin\x64\7z.dll";

    static PpmdCodecTests() => ZevenLibrary.Load(DllPath);

    [Fact]
    public void RoundTrip_SmallData()
    {
        var original = new byte[100];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        PpmdCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void RoundTrip_TextData()
    {
        // PPMd excels at text
        var text = string.Join("\n", Enumerable.Range(0, 1000).Select(i => $"Line {i}: The quick brown fox jumps over the lazy dog."));
        var original = System.Text.Encoding.UTF8.GetBytes(text);

        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        PpmdCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void Compression_ReducesSize_OnText()
    {
        var text = string.Concat(Enumerable.Repeat("Hello World! ", 10000));
        var original = System.Text.Encoding.UTF8.GetBytes(text);

        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(original), compressed);

        Assert.True(compressed.Length < original.Length,
            $"Compressed {compressed.Length} should be < original {original.Length}");
    }

    [Fact]
    public void PpmdOptions_Default_RoundTrips()
    {
        var original = new byte[500];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        PpmdCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void Compress_WritesChunkedFormatHeader()
    {
        var original = new byte[100];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        byte[] magic = new byte[4];
        compressed.ReadExactly(magic);
        Assert.Equal("ZPM\x01"u8.ToArray(), magic);
    }

    [Fact]
    public void Compress_EmptyInput_WritesHeaderAndEndMarker()
    {
        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream([]), compressed);

        // Header: 4 magic + 5 props + 4 CRC = 13, EndMarker: 16 zeros = total 29
        Assert.Equal(29, compressed.Length);

        compressed.Position = 0;
        byte[] magic = new byte[4];
        compressed.ReadExactly(magic);
        Assert.Equal("ZPM\x01"u8.ToArray(), magic);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(16)]
    public void PpmdOptions_Order_ValidValues(int order)
    {
        var original = new byte[200];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(original), compressed,
            new PpmdOptions { Order = order });

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        PpmdCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }
}

public class PpmdStreamTests
{
    const string DllPath = @"q:\7z2601-bin\x64\7z.dll";

    static PpmdStreamTests() => ZevenLibrary.Load(DllPath);

    [Fact]
    public void WriteRead_RoundTrip()
    {
        var original = new byte[2000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        using (var compressor = new PpmdStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var decompressor = new PpmdStream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void IncrementalWrites_RoundTrip()
    {
        var original = new byte[5000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        using (var compressor = new PpmdStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            for (int i = 0; i < original.Length; i += 100)
            {
                int len = Math.Min(100, original.Length - i);
                compressor.Write(original, i, len);
            }
        }

        compressed.Position = 0;
        using var decompressor = new PpmdStream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void CanRead_CanWrite_Correctness()
    {
        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using (var compressor = new PpmdStream(new MemoryStream(), CompressionMode.Compress))
        {
            Assert.True(compressor.CanWrite);
            Assert.False(compressor.CanRead);
        }

        using (var decompressor = new PpmdStream(compressed, CompressionMode.Decompress))
        {
            Assert.True(decompressor.CanRead);
            Assert.False(decompressor.CanWrite);
        }
    }
}

public class PpmdFormatTests
{
    [Fact]
    public void WriteHeader_ReadHeader_RoundTrips()
    {
        byte[] props = [0x05, 0x18, 0x00, 0x00, 0x10];

        using var ms = new MemoryStream();
        PpmdFormat.WriteHeader(ms, props);

        ms.Position = 0;
        byte[] result = PpmdFormat.ReadHeader(ms);

        Assert.Equal(props, result);
    }

    [Fact]
    public void ReadHeader_BadMagic_Throws()
    {
        using var ms = new MemoryStream();
        ms.Write([0x00, 0x00, 0x00, 0x00]);
        ms.Write(new byte[5]);
        ms.Write(new byte[4]);
        ms.Position = 0;

        Assert.Throws<InvalidDataException>(() => PpmdFormat.ReadHeader(ms));
    }

    [Fact]
    public void ReadHeader_BadCrc_Throws()
    {
        byte[] props = [0x05, 0x18, 0x00, 0x00, 0x10];

        using var ms = new MemoryStream();
        PpmdFormat.WriteHeader(ms, props);

        // Corrupt the last CRC byte
        ms.Position = ms.Length - 1;
        byte last = (byte)ms.ReadByte();
        ms.Position = ms.Length - 1;
        ms.WriteByte((byte)(last ^ 0xFF));

        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => PpmdFormat.ReadHeader(ms));
    }

    [Fact]
    public void WriteChunk_ReadChunk_RoundTrips()
    {
        byte[] data = [0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE];
        long uncompressedSize = 1024;

        using var ms = new MemoryStream();
        PpmdFormat.WriteChunk(ms, uncompressedSize, data);

        ms.Position = 0;
        ChunkData? chunk = PpmdFormat.ReadChunk(ms);

        Assert.NotNull(chunk);
        Assert.Equal(uncompressedSize, chunk.Value.UncompressedSize);
        Assert.Equal(data, chunk.Value.CompressedData);
    }

    [Fact]
    public void ReadChunk_CorruptCrc_Throws()
    {
        byte[] data = [0xDE, 0xAD, 0xBE, 0xEF];

        using var ms = new MemoryStream();
        PpmdFormat.WriteChunk(ms, 100, data);

        // Corrupt a byte in the compressed data region (starts at offset 16)
        ms.Position = 16;
        byte orig = (byte)ms.ReadByte();
        ms.Position = 16;
        ms.WriteByte((byte)(orig ^ 0xFF));

        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => PpmdFormat.ReadChunk(ms));
    }

    [Fact]
    public void WriteEndMarker_ReadChunk_ReturnsNull()
    {
        using var ms = new MemoryStream();
        PpmdFormat.WriteEndMarker(ms);

        ms.Position = 0;
        ChunkData? result = PpmdFormat.ReadChunk(ms);

        Assert.Null(result);
    }

    [Fact]
    public void ReadChunk_NegativeCompressedSize_Throws()
    {
        using var ms = new MemoryStream();

        // Write uncompressed size > 0
        Span<byte> header = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header, 100);
        // Write negative compressed size
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header[8..], -1);
        ms.Write(header);

        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => PpmdFormat.ReadChunk(ms));
    }

    [Fact]
    public void ReadChunk_TruncatedData_Throws()
    {
        using var ms = new MemoryStream();

        // Write header claiming 1000 bytes of compressed data
        Span<byte> header = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header, 500);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header[8..], 1000);
        ms.Write(header);
        // Only write 10 bytes instead of 1000
        ms.Write(new byte[10]);

        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => PpmdFormat.ReadChunk(ms));
    }
}
