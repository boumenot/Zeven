using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>Batch Brotli compression/decompression using the Zeven chunked format.</summary>
public static class BrotliCodec
{
    public static void Compress(Stream input, Stream output, BrotliOptions? options = null)
        => ZevenCodec.Compress(input, output, options ?? new BrotliOptions());

    public static void Decompress(Stream input, Stream output)
        => ZevenCodec.Decompress(input, output, CodecId.Brotli);
}
