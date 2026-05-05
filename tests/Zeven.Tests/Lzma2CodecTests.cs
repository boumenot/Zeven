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
}
