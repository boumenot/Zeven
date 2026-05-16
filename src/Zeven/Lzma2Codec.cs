using Zeven.Interop;

namespace Zeven;

/// <summary>Batch LZMA2 compression/decompression using the Zeven chunked format.</summary>
public static class Lzma2Codec
{
    public static void Compress(Stream input, Stream output, Lzma2Options? options = null)
        => ZevenCodec.Compress(input, output, options ?? new Lzma2Options());

    public static void Decompress(Stream input, Stream output)
        => ZevenCodec.Decompress(input, output, CodecId.Lzma2);
}
