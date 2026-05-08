using System.IO.Compression;
using Zeven.Core;
using Zeven.Core.Interop;

namespace Zeven.Tests;

public class PpmdCodecTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

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
        Assert.Equal("ZVN\x01"u8.ToArray(), magic);
    }

    [Fact]
    public void Compress_EmptyInput_WritesHeaderAndEndMarker()
    {
        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream([]), compressed);

        // Header: 4 magic + 4 codecId + 2 propLen + 6 reserved + 5 props + 4 CRC = 25
        // EndMarker: 16 zeros = total 41
        Assert.Equal(41, compressed.Length);

        compressed.Position = 0;
        byte[] magic = new byte[4];
        compressed.ReadExactly(magic);
        Assert.Equal("ZVN\x01"u8.ToArray(), magic);
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

    [Theory]
    [InlineData(1 << 20)]       // 1 MB
    [InlineData(4 * 1024 * 1024)]  // 4 MB
    [InlineData(16 * 1024 * 1024)] // 16 MB
    public void PpmdOptions_MemorySize_ValidValues(long memorySize)
    {
        var original = new byte[200];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(original), compressed,
            new PpmdOptions { MemorySize = memorySize });

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        PpmdCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }
}

public class PpmdStreamTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

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

    [Fact]
    public void MultipleChunks_RoundTrip()
    {
        var original = new byte[5000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        var options = new PpmdOptions { ChunkSize = 1024 };
        using (var compressor = new PpmdStream(compressed, CompressionMode.Compress, options,
                leaveOpen: true))
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
    public void CrossApiCompat_CodecWriteStreamRead()
    {
        var original = new byte[2000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressor = new PpmdStream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void CrossApiCompat_StreamWriteCodecRead()
    {
        var original = new byte[2000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        using (var compressor = new PpmdStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var result = new MemoryStream();
        PpmdCodec.Decompress(compressed, result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void EmptyInput_ProducesValidStream()
    {
        using var compressed = new MemoryStream();
        using (var compressor = new PpmdStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            // Write nothing
        }

        compressed.Position = 0;
        using var decompressor = new PpmdStream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        Assert.Empty(result.ToArray());
    }

    [Fact]
    public void Flush_EmitsPartialChunk()
    {
        var data = new byte[100];
        new Random(42).NextBytes(data);

        using var compressed = new MemoryStream();
        var options = new PpmdOptions { ChunkSize = 1024 };
        using (var compressor = new PpmdStream(compressed, CompressionMode.Compress, options,
                leaveOpen: true))
        {
            compressor.Write(data);
            compressor.Flush();

            // After Flush, some compressed data should be in the output
            Assert.True(compressed.Length > 0, "Flush should emit data to output stream");
        }
    }

    [Fact]
    public void Length_Throws_NotSupportedException()
    {
        using var stream = new PpmdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Length);
    }

    [Fact]
    public void Position_Throws_NotSupportedException()
    {
        using var stream = new PpmdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }

    [Fact]
    public void Seek_Throws_NotSupportedException()
    {
        using var stream = new PpmdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void SetLength_Throws_NotSupportedException()
    {
        using var stream = new PpmdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [Fact]
    public void Read_InCompressMode_Throws()
    {
        using var stream = new PpmdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<InvalidOperationException>(() => stream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Write_InDecompressMode_Throws()
    {
        using var compressed = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using var stream = new PpmdStream(compressed, CompressionMode.Decompress);
        Assert.Throws<InvalidOperationException>(() => stream.Write(new byte[1], 0, 1));
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var stream = new PpmdStream(new MemoryStream(), CompressionMode.Compress);
        stream.Dispose();
        stream.Dispose(); // should not throw
    }

    [Fact]
    public void Constructor_ZeroChunkSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PpmdStream(new MemoryStream(), CompressionMode.Compress,
                new PpmdOptions { ChunkSize = 0 }));
    }

    [Fact]
    public void Constructor_NegativeChunkSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PpmdStream(new MemoryStream(), CompressionMode.Compress,
                new PpmdOptions { ChunkSize = -1 }));
    }
}

public class ZevenFormatTests
{
    [Fact]
    public void WriteHeader_ReadHeader_RoundTrips()
    {
        byte[] props = [0x05, 0x18, 0x00, 0x00, 0x10];

        using var ms = new MemoryStream();
        ZevenFormat.WriteHeader(ms, 0x030401, props);

        ms.Position = 0;
        var header = ZevenFormat.ReadHeader(ms);

        Assert.Equal(props, header.PropertyHeader);
    }

    [Fact]
    public void ReadHeader_BadMagic_Throws()
    {
        using var ms = new MemoryStream();
        ms.Write([0xFF, 0xFE, 0xFD, 0xFC]);
        ms.Write(new byte[12]);
        ms.Write(new byte[4]);
        ms.Position = 0;

        Assert.Throws<InvalidDataException>(() => ZevenFormat.ReadHeader(ms));
    }

    [Fact]
    public void ReadHeader_BadCrc_Throws()
    {
        byte[] props = [0x05, 0x18, 0x00, 0x00, 0x10];

        using var ms = new MemoryStream();
        ZevenFormat.WriteHeader(ms, 0x030401, props);

        // Corrupt the last CRC byte
        ms.Position = ms.Length - 1;
        byte last = (byte)ms.ReadByte();
        ms.Position = ms.Length - 1;
        ms.WriteByte((byte)(last ^ 0xFF));

        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => ZevenFormat.ReadHeader(ms));
    }

    [Fact]
    public void ReadHeaderAndValidateCodec_Mismatch_Throws()
    {
        var props = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        using var ms = new MemoryStream();
        ZevenFormat.WriteHeader(ms, CodecId.Brotli, props);

        ms.Position = 0;
        var ex = Assert.Throws<InvalidDataException>(
            () => ZevenFormat.ReadHeaderAndValidateCodec(ms, CodecId.Lzma2));
        Assert.Contains("Brotli", ex.Message);
        Assert.Contains("LZMA2", ex.Message);
    }

    [Fact]
    public void WriteChunk_ReadChunk_RoundTrips()
    {
        byte[] data = [0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE];
        long uncompressedSize = 1024;

        using var ms = new MemoryStream();
        ZevenFormat.WriteChunk(ms, uncompressedSize, data);

        ms.Position = 0;
        ChunkData? chunk = ZevenFormat.ReadChunk(ms);

        Assert.NotNull(chunk);
        Assert.Equal(uncompressedSize, chunk.Value.UncompressedSize);
        Assert.Equal(data, chunk.Value.CompressedData);
    }

    [Fact]
    public void ReadChunk_CorruptCrc_Throws()
    {
        byte[] data = [0xDE, 0xAD, 0xBE, 0xEF];

        using var ms = new MemoryStream();
        ZevenFormat.WriteChunk(ms, 100, data);

        // Corrupt a byte in the compressed data region (starts at offset 16)
        ms.Position = 16;
        byte orig = (byte)ms.ReadByte();
        ms.Position = 16;
        ms.WriteByte((byte)(orig ^ 0xFF));

        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => ZevenFormat.ReadChunk(ms));
    }

    [Fact]
    public void WriteEndMarker_ReadChunk_ReturnsNull()
    {
        using var ms = new MemoryStream();
        ZevenFormat.WriteEndMarker(ms);

        ms.Position = 0;
        ChunkData? result = ZevenFormat.ReadChunk(ms);

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
        Assert.Throws<InvalidDataException>(() => ZevenFormat.ReadChunk(ms));
    }

    [Fact]
    public void ReadChunk_CorruptEndMarker_NonZeroCompressedSize_Throws()
    {
        using var ms = new MemoryStream();

        // uncompressedSize = 0 (end marker signal) but compressedSize != 0 (corrupt)
        Span<byte> header = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header[8..], 42);
        ms.Write(header);

        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => ZevenFormat.ReadChunk(ms));
    }

    [Fact]
    public void ReadChunk_NegativeUncompressedSize_Throws()
    {
        using var ms = new MemoryStream();

        Span<byte> header = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header, -1);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(header[8..], 10);
        ms.Write(header);

        ms.Position = 0;
        Assert.Throws<InvalidDataException>(() => ZevenFormat.ReadChunk(ms));
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
        Assert.Throws<InvalidDataException>(() => ZevenFormat.ReadChunk(ms));
    }

    [Fact]
    public void ReadHeader_ReturnsCorrectCodecId()
    {
        ulong codecId = 0x04F71101; // Zstd

        using var ms = new MemoryStream();
        ZevenFormat.WriteHeader(ms, codecId, [0xAA, 0xBB]);

        ms.Position = 0;
        var header = ZevenFormat.ReadHeader(ms);

        Assert.Equal(codecId, header.CodecId);
    }

    [Fact]
    public void WriteHeader_VariablePropertyLength()
    {
        byte[] props = [0x01, 0x02, 0x03]; // 3-byte Brotli-size props

        using var ms = new MemoryStream();
        ZevenFormat.WriteHeader(ms, 0x04F71102, props);

        ms.Position = 0;
        var header = ZevenFormat.ReadHeader(ms);

        Assert.Equal((ulong)0x04F71102, header.CodecId);
        Assert.Equal(props, header.PropertyHeader);
    }

    [Fact]
    public void CapturePropertyHeader_UnknownCodec_Throws()
    {
        ZevenLibrary.Load(@"q:\Zeven\bin\7z.dll");

        var bogusOptions = new BogusCodecOptions();
        var ex = Assert.Throws<InvalidOperationException>(
            () => Codec.CapturePropertyHeader(bogusOptions));
        Assert.Contains("Unknown", ex.Message);
        Assert.Contains("0xDEADBEEF", ex.Message);
    }

    [Fact]
    public void DecompressBlock_UnknownCodec_Throws()
    {
        ZevenLibrary.Load(@"q:\Zeven\bin\7z.dll");

        var ex = Assert.Throws<InvalidOperationException>(
            () => Codec.DecompressBlock(new byte[5], 0xDEADBEEF,
                    new MemoryStream(), new MemoryStream(), 0));
        Assert.Contains("Unknown", ex.Message);
        Assert.Contains("0xDEADBEEF", ex.Message);
    }

    [Fact]
    public void CompressBlock_UnknownCodec_Throws()
    {
        ZevenLibrary.Load(@"q:\Zeven\bin\7z.dll");

        var bogusOptions = new BogusCodecOptions();
        var ex = Assert.Throws<InvalidOperationException>(
            () => Codec.CompressBlock(bogusOptions, new byte[5],
                    new MemoryStream(), new MemoryStream()));
        Assert.Contains("Unknown", ex.Message);
        Assert.Contains("0xDEADBEEF", ex.Message);
    }

    private class BogusCodecOptions : ICodecOptions
    {
        public ulong CodecId => 0xDEADBEEF;
        public int ChunkSize => 16 * 1024 * 1024;
        public Dictionary<uint, object> GetProperties() => new();
    }
}
