using System.IO.Compression;
using Zeven.Core;
using Zeven.Core.Interop;
using BrotliStream = Zeven.Core.BrotliStream;

namespace Zeven.Tests;

public class BrotliCodecTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

    static BrotliCodecTests() => ZevenLibrary.Load(DllPath);

    [Fact]
    public void RoundTrip_SmallData()
    {
        var original = new byte[100];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        BrotliCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        BrotliCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void RoundTrip_TextData()
    {
        var text = string.Join("\n", Enumerable.Range(0, 1000).Select(i => $"Line {i}: The quick brown fox jumps over the lazy dog."));
        var original = System.Text.Encoding.UTF8.GetBytes(text);

        using var compressed = new MemoryStream();
        BrotliCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        BrotliCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void Compression_ReducesSize_OnText()
    {
        var text = string.Concat(Enumerable.Repeat("Hello World! ", 10000));
        var original = System.Text.Encoding.UTF8.GetBytes(text);

        using var compressed = new MemoryStream();
        BrotliCodec.Compress(new MemoryStream(original), compressed);

        Assert.True(compressed.Length < original.Length,
            $"Compressed {compressed.Length} should be < original {original.Length}");
    }

    [Fact]
    public void Compress_WritesZevenHeader()
    {
        var original = new byte[100];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        BrotliCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        byte[] magic = new byte[4];
        compressed.ReadExactly(magic);
        Assert.Equal("ZVN\x01"u8.ToArray(), magic);
    }
}

public class BrotliStreamTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

    static BrotliStreamTests() => ZevenLibrary.Load(DllPath);

    [Fact]
    public void WriteRead_RoundTrip()
    {
        var original = new byte[2000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        using (var compressor = new BrotliStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
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
        using (var compressor = new BrotliStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            for (int i = 0; i < original.Length; i += 100)
            {
                int len = Math.Min(100, original.Length - i);
                compressor.Write(original, i, len);
            }
        }

        compressed.Position = 0;
        using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void MultipleChunks_RoundTrip()
    {
        var original = new byte[5000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        var options = new BrotliOptions { ChunkSize = 1024 };
        using (var compressor = new BrotliStream(compressed, CompressionMode.Compress, options,
                leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
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
        BrotliCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
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
        using (var compressor = new BrotliStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var result = new MemoryStream();
        BrotliCodec.Decompress(compressed, result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void EmptyInput_ProducesValidStream()
    {
        using var compressed = new MemoryStream();
        using (var compressor = new BrotliStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            // Write nothing
        }

        compressed.Position = 0;
        using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        Assert.Empty(result.ToArray());
    }

    [Fact]
    public void CanRead_CanWrite_Correctness()
    {
        using var compressed = new MemoryStream();
        BrotliCodec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using (var compressor = new BrotliStream(new MemoryStream(), CompressionMode.Compress))
        {
            Assert.True(compressor.CanWrite);
            Assert.False(compressor.CanRead);
        }

        using (var decompressor = new BrotliStream(compressed, CompressionMode.Decompress))
        {
            Assert.True(decompressor.CanRead);
            Assert.False(decompressor.CanWrite);
        }
    }

    [Fact]
    public void Length_Throws_NotSupportedException()
    {
        using var stream = new BrotliStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Length);
    }

    [Fact]
    public void Position_Throws_NotSupportedException()
    {
        using var stream = new BrotliStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }

    [Fact]
    public void Seek_Throws_NotSupportedException()
    {
        using var stream = new BrotliStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void SetLength_Throws_NotSupportedException()
    {
        using var stream = new BrotliStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [Fact]
    public void Read_InCompressMode_Throws()
    {
        using var stream = new BrotliStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<InvalidOperationException>(() => stream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Write_InDecompressMode_Throws()
    {
        using var compressed = new MemoryStream();
        BrotliCodec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using var stream = new BrotliStream(compressed, CompressionMode.Decompress);
        Assert.Throws<InvalidOperationException>(() => stream.Write(new byte[1], 0, 1));
    }
}
