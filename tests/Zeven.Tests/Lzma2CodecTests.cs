using Zeven.Core;
using Zeven.Core.Interop;

namespace Zeven.Tests;

public class Lzma2CodecTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

    [Fact]
    public void FindCodecIndex_Lzma2_ReturnsValidIndex()
    {
        var lib = ZevenLibrary.Load(DllPath);

        int index = lib.FindCodecIndex(CodecId.Lzma2); // LZMA2 codec ID

        Assert.True(index >= 0, "LZMA2 codec should be found");
    }

    [Fact]
    public void CreateEncoderObject_Lzma2_ReturnsNonNull()
    {
        var lib = ZevenLibrary.Load(DllPath);
        int index = lib.FindCodecIndex(CodecId.Lzma2);

        nint encoder = lib.CreateEncoderObject((uint)index);

        Assert.NotEqual(nint.Zero, encoder);
    }

    [Fact]
    public void CreateDecoderObject_Lzma2_ReturnsNonNull()
    {
        var lib = ZevenLibrary.Load(DllPath);
        int index = lib.FindCodecIndex(CodecId.Lzma2);

        nint decoder = lib.CreateDecoderObject((uint)index);

        Assert.NotEqual(nint.Zero, decoder);
    }

    [Fact]
    public void RoundTrip_SmallData()
    {
        var original = new byte[100];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        Lzma2Codec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void RoundTrip_EmptyData()
    {
        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream([]), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        Lzma2Codec.Decompress(compressed, decompressed);

        Assert.Empty(decompressed.ToArray());
    }

    [Fact]
    public void RoundTrip_LargeData()
    {
        var original = new byte[1024 * 1024];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        Lzma2Codec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Fact]
    public void Compression_ReducesSize()
    {
        var zeros = new byte[1024 * 1024]; // highly compressible

        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(zeros), compressed);

        Assert.True(compressed.Length < zeros.Length,
            $"Compressed size {compressed.Length} should be less than {zeros.Length}");
    }

    [Fact]
    public void HigherLevel_ProducesSmallerOutput()
    {
        // Compressible but not trivially so
        var data = new byte[256 * 1024];
        var rng = new Random(42);
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(rng.Next(10)); // low entropy
        }

        using var fast = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(data), fast, new Lzma2Options { Level = 1 });

        using var ultra = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(data), ultra, new Lzma2Options { Level = 9 });

        Assert.True(ultra.Length <= fast.Length,
            $"Level 9 ({ultra.Length}) should produce output <= level 1 ({fast.Length})");
    }

    [Fact]
    public void Decompress_InvalidHeader_Throws()
    {
        // First byte is LZMA2 property byte; 0xFF is invalid
        var garbage = new byte[] { 0xFF, 0x00, 0x00 };

        Assert.ThrowsAny<Exception>(() =>
        {
            Lzma2Codec.Decompress(new MemoryStream(garbage), new MemoryStream());
        });
    }

    [Fact]
    public void Lzma2Options_Default_RoundTrips()
    {
        var original = new byte[500];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(original), compressed);

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        Lzma2Codec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public void Lzma2Options_CustomLevel_RoundTrips(int level)
    {
        var original = new byte[500];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(original), compressed, new Lzma2Options { Level = level });

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        Lzma2Codec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Theory]
    [InlineData(1 << 16)]   // 64KB
    [InlineData(1 << 20)]   // 1MB
    [InlineData(1 << 24)]   // 16MB
    public void Lzma2Options_DictionarySize_ValidValues(long dictSize)
    {
        var original = new byte[200];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(original), compressed,
            new Lzma2Options { DictionarySize = dictSize });

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        Lzma2Codec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(64)]
    [InlineData(273)]
    public void Lzma2Options_NumFastBytes_ValidValues(int numFastBytes)
    {
        var original = new byte[200];
        new Random(42).NextBytes(original);

        using var compressed = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(original), compressed,
            new Lzma2Options { NumFastBytes = numFastBytes });

        compressed.Position = 0;
        using var decompressed = new MemoryStream();
        Lzma2Codec.Decompress(compressed, decompressed);

        Assert.Equal(original, decompressed.ToArray());
    }
}
