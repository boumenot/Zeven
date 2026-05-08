using System.IO.Compression;
using Zeven.Core;
using Zeven.Core.Interop;

namespace Zeven.Tests;

public class ZstdCodecTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

    static ZstdCodecTests() => ZevenLibrary.Load(DllPath);

    [Fact]
    public void RoundTrip_SmallData()
    {
        var original = new byte[100];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        ZstdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        ZstdCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void RoundTrip_TextData()
    {
        var text = string.Join("\n", Enumerable.Range(0, 1000).Select(i => $"Line {i}: The quick brown fox jumps over the lazy dog."));
        var original = System.Text.Encoding.UTF8.GetBytes(text);

        using var compressed = new MemoryStream();
        ZstdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        ZstdCodec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void Compression_ReducesSize_OnText()
    {
        var text = string.Concat(Enumerable.Repeat("Hello World! ", 10000));
        var original = System.Text.Encoding.UTF8.GetBytes(text);

        using var compressed = new MemoryStream();
        ZstdCodec.Compress(new MemoryStream(original), compressed);

        Assert.True(compressed.Length < original.Length,
            $"Compressed {compressed.Length} should be < original {original.Length}");
    }

    [Fact]
    public void Compress_WritesZevenHeader()
    {
        var original = new byte[100];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        ZstdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        byte[] magic = new byte[4];
        compressed.ReadExactly(magic);
        Assert.Equal("ZVN\x01"u8.ToArray(), magic);
    }

    [Fact]
    public void Compress_NonSeekableInput_ThrowsArgumentException()
    {
        var data = new MemoryStream(new byte[100]);
        var nonSeekable = new NonSeekableStream(data);

        Assert.Throws<ArgumentException>(
            () => ZstdCodec.Compress(nonSeekable, new MemoryStream()));
    }

    private class NonSeekableStream : Stream
    {
        private readonly Stream inner;
        public NonSeekableStream(Stream inner) => this.inner = inner;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public class ZstdStreamTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

    static ZstdStreamTests() => ZevenLibrary.Load(DllPath);

    [Fact]
    public void WriteRead_RoundTrip()
    {
        var original = new byte[2000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        using (var compressor = new ZstdStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var decompressor = new ZstdStream(compressed, CompressionMode.Decompress);
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
        using (var compressor = new ZstdStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            for (int i = 0; i < original.Length; i += 100)
            {
                int len = Math.Min(100, original.Length - i);
                compressor.Write(original, i, len);
            }
        }

        compressed.Position = 0;
        using var decompressor = new ZstdStream(compressed, CompressionMode.Decompress);
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
        var options = new ZstdOptions { ChunkSize = 1024 };
        using (var compressor = new ZstdStream(compressed, CompressionMode.Compress, options,
                leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var decompressor = new ZstdStream(compressed, CompressionMode.Decompress);
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
        ZstdCodec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressor = new ZstdStream(compressed, CompressionMode.Decompress);
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
        using (var compressor = new ZstdStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var result = new MemoryStream();
        ZstdCodec.Decompress(compressed, result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void EmptyInput_ProducesValidStream()
    {
        using var compressed = new MemoryStream();
        using (var compressor = new ZstdStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            // Write nothing
        }

        compressed.Position = 0;
        using var decompressor = new ZstdStream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        Assert.Empty(result.ToArray());
    }

    [Fact]
    public void CanRead_CanWrite_Correctness()
    {
        using var compressed = new MemoryStream();
        ZstdCodec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using (var compressor = new ZstdStream(new MemoryStream(), CompressionMode.Compress))
        {
            Assert.True(compressor.CanWrite);
            Assert.False(compressor.CanRead);
        }

        using (var decompressor = new ZstdStream(compressed, CompressionMode.Decompress))
        {
            Assert.True(decompressor.CanRead);
            Assert.False(decompressor.CanWrite);
        }
    }

    [Fact]
    public void Length_Throws_NotSupportedException()
    {
        using var stream = new ZstdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Length);
    }

    [Fact]
    public void Position_Throws_NotSupportedException()
    {
        using var stream = new ZstdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }

    [Fact]
    public void Seek_Throws_NotSupportedException()
    {
        using var stream = new ZstdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void SetLength_Throws_NotSupportedException()
    {
        using var stream = new ZstdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [Fact]
    public void Read_InCompressMode_Throws()
    {
        using var stream = new ZstdStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<InvalidOperationException>(() => stream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Write_InDecompressMode_Throws()
    {
        using var compressed = new MemoryStream();
        ZstdCodec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using var stream = new ZstdStream(compressed, CompressionMode.Decompress);
        Assert.Throws<InvalidOperationException>(() => stream.Write(new byte[1], 0, 1));
    }

    [Fact]
    public void Flush_EmitsPartialChunk()
    {
        var data = new byte[100];
        new Random(42).NextBytes(data);

        using var compressed = new MemoryStream();
        var options = new ZstdOptions { ChunkSize = 1024 };
        using (var compressor = new ZstdStream(compressed, CompressionMode.Compress, options,
                leaveOpen: true))
        {
            compressor.Write(data);
            compressor.Flush();

            Assert.True(compressed.Length > 0, "Flush should emit data to output stream");
        }
    }

    [Fact]
    public void Flush_DataIsDecompressible()
    {
        var data = new byte[500];
        new Random(42).NextBytes(data);

        using var compressed = new MemoryStream();
        var options = new ZstdOptions { ChunkSize = 1024 };
        using (var compressor = new ZstdStream(compressed, CompressionMode.Compress, options,
                leaveOpen: true))
        {
            compressor.Write(data);
            compressor.Flush();
            // Write more after flush
            compressor.Write(data);
        }

        compressed.Position = 0;
        using var decompressor = new ZstdStream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        var expected = new byte[data.Length * 2];
        Buffer.BlockCopy(data, 0, expected, 0, data.Length);
        Buffer.BlockCopy(data, 0, expected, data.Length, data.Length);
        Assert.Equal(expected, result.ToArray());
    }

    [Fact]
    public void Flush_NoData_DoesNotThrow()
    {
        using var compressed = new MemoryStream();
        using var compressor = new ZstdStream(compressed, CompressionMode.Compress, leaveOpen: true);
        compressor.Flush(); // flush with empty buffer — should not throw or emit data
        Assert.Equal(0, compressed.Length);
    }

    [Fact]
    public void Flush_InDecompressMode_DoesNotThrow()
    {
        using var compressed = new MemoryStream();
        ZstdCodec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using var decompressor = new ZstdStream(compressed, CompressionMode.Decompress);
        decompressor.Flush(); // should be a no-op, not throw
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var stream = new ZstdStream(new MemoryStream(), CompressionMode.Compress);
        stream.Dispose();
        stream.Dispose(); // should not throw
    }
}
