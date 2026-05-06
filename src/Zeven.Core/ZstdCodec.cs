using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>Batch Zstd compression/decompression using the Zeven chunked format.</summary>
public static class ZstdCodec
{
    public static void Compress(Stream input, Stream output, ZstdOptions? options = null)
        => ZevenCodec.Compress(input, output, options ?? new ZstdOptions());

    public static void Decompress(Stream input, Stream output)
        => ZevenCodec.Decompress(input, output, CodecId.Zstd);
}
