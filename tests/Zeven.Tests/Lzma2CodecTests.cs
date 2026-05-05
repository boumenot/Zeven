using Zeven.Core;

namespace Zeven.Tests;

public class Lzma2CodecTests
{
    const string DllPath = @"q:\7z2601-bin\x64\7z.dll";

    [Fact]
    public void FindCodecIndex_Lzma2_ReturnsValidIndex()
    {
        var lib = ZevenLibrary.Load(DllPath);

        int index = lib.FindCodecIndex(0x21); // LZMA2 codec ID

        Assert.True(index >= 0, "LZMA2 codec should be found");
    }

    [Fact]
    public void CreateEncoderObject_Lzma2_ReturnsNonNull()
    {
        var lib = ZevenLibrary.Load(DllPath);
        int index = lib.FindCodecIndex(0x21);

        nint encoder = lib.CreateEncoderObject((uint)index);

        Assert.NotEqual(nint.Zero, encoder);
    }

    [Fact]
    public void CreateDecoderObject_Lzma2_ReturnsNonNull()
    {
        var lib = ZevenLibrary.Load(DllPath);
        int index = lib.FindCodecIndex(0x21);

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
        Lzma2Codec.Compress(new MemoryStream(data), fast, level: 1);

        using var ultra = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(data), ultra, level: 9);

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
}
