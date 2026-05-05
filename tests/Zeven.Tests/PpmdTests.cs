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
