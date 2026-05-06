using System.IO.Compression;
using Zeven.Core;

namespace Zeven.Tests;

public class Lzma2StreamTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

    // Ensure library is loaded
    static Lzma2StreamTests() => ZevenLibrary.Load(DllPath);

    [Fact]
    public void WriteRead_RoundTrip()
    {
        var original = new byte[1000];
        new Random(42).NextBytes(original);

        // Compress
        using var compressed = new MemoryStream();
        using (var compressor = new Lzma2Stream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(original);
        }

        // Decompress
        compressed.Position = 0;
        using var decompressor = new Lzma2Stream(compressed, CompressionMode.Decompress);
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
        using (var compressor = new Lzma2Stream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            // Write in small chunks
            for (int i = 0; i < original.Length; i += 100)
            {
                int len = Math.Min(100, original.Length - i);
                compressor.Write(original, i, len);
            }
        }

        compressed.Position = 0;
        using var decompressor = new Lzma2Stream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void IncrementalReads()
    {
        var original = new byte[3000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        using (var compressor = new Lzma2Stream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(original);
        }

        compressed.Position = 0;
        using var decompressor = new Lzma2Stream(compressed, CompressionMode.Decompress);
        using var result = new MemoryStream();

        // Read in small chunks
        var buffer = new byte[37]; // odd size to test partial reads
        int bytesRead;
        while ((bytesRead = decompressor.Read(buffer, 0, buffer.Length)) > 0)
        {
            result.Write(buffer, 0, bytesRead);
        }

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void CanRead_CanWrite_Correctness()
    {
        using var ms = new MemoryStream();

        using (var compressor = new Lzma2Stream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            Assert.True(compressor.CanWrite);
            Assert.False(compressor.CanRead);
            Assert.False(compressor.CanSeek);
        }

        ms.Position = 0;
        // Write a valid compressed stream first
        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using (var decompressor = new Lzma2Stream(compressed, CompressionMode.Decompress))
        {
            Assert.True(decompressor.CanRead);
            Assert.False(decompressor.CanWrite);
            Assert.False(decompressor.CanSeek);
        }
    }

    [Fact]
    public async Task Dispose_CompletesWithinTimeout()
    {
        var original = new byte[10000];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        var compressor = new Lzma2Stream(compressed, CompressionMode.Compress, leaveOpen: true);
        compressor.Write(original);

        // Dispose should complete within 5 seconds (no hang)
        var disposeTask = Task.Run(() => compressor.Dispose());
        bool completed = await disposeTask.WaitAsync(TimeSpan.FromSeconds(5))
            .ContinueWith(t => !t.IsFaulted && !t.IsCanceled);

        Assert.True(completed, "Dispose should complete within 5 seconds");
    }

    [Fact]
    public void LeaveOpen_True()
    {
        var ms = new MemoryStream();

        using (var compressor = new Lzma2Stream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            compressor.Write(new byte[] { 1, 2, 3 });
        }

        // Stream should still be usable after dispose
        Assert.True(ms.CanRead, "Inner stream should remain open when leaveOpen=true");
        Assert.True(ms.Length > 0);
    }

    [Fact]
    public void LeaveOpen_False()
    {
        var ms = new MemoryStream();

        using (var compressor = new Lzma2Stream(ms, CompressionMode.Compress, leaveOpen: false))
        {
            compressor.Write(new byte[] { 1, 2, 3 });
        }

        // Stream should be disposed
        Assert.False(ms.CanRead, "Inner stream should be closed when leaveOpen=false");
    }

    [Fact]
    public void BackgroundError_PropagatesOnDispose()
    {
        // Create a stream that will fail during Code()
        var brokenStream = new BrokenWriteStream();

        var compressor = new Lzma2Stream(brokenStream, CompressionMode.Compress, leaveOpen: true);
        compressor.Write(new byte[1000]);

        // Dispose should surface the error from the background task
        Assert.ThrowsAny<Exception>(() => compressor.Dispose());
    }

    [Fact]
    public void Length_Throws_NotSupportedException()
    {
        using var stream = new Lzma2Stream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Length);
    }

    [Fact]
    public void Position_Throws_NotSupportedException()
    {
        using var stream = new Lzma2Stream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }

    [Fact]
    public void Seek_Throws_NotSupportedException()
    {
        using var stream = new Lzma2Stream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void SetLength_Throws_NotSupportedException()
    {
        using var stream = new Lzma2Stream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [Fact]
    public void Read_InCompressMode_Throws()
    {
        using var stream = new Lzma2Stream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<InvalidOperationException>(() => stream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Write_InDecompressMode_Throws()
    {
        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(new byte[1]), compressed);
        compressed.Position = 0;

        using var stream = new Lzma2Stream(compressed, CompressionMode.Decompress);
        Assert.Throws<InvalidOperationException>(() => stream.Write(new byte[1], 0, 1));
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var stream = new Lzma2Stream(new MemoryStream(), CompressionMode.Compress);
        stream.Dispose();
        stream.Dispose(); // should not throw
    }

    /// <summary>A stream that throws on Write — used to test error propagation.</summary>
    private class BrokenWriteStream : MemoryStream
    {
        private int writeCount;

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            this.writeCount++;
            // Allow first write (property header), fail on subsequent writes
            if (this.writeCount > 1)
            {
                throw new IOException("Simulated write failure");
            }
            base.Write(buffer);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.Write(buffer.AsSpan(offset, count));
        }
    }
}
