using Zeven.Interop;

namespace Zeven;

/// <summary>Batch LZ4 compression/decompression using the Zeven chunked format.</summary>
public static class Lz4Codec
{
    public static void Compress(Stream input, Stream output, Lz4Options? options = null)
        => ZevenCodec.Compress(input, output, options ?? new Lz4Options());

    public static void Decompress(Stream input, Stream output)
        => ZevenCodec.Decompress(input, output, CodecId.Lz4);
}
