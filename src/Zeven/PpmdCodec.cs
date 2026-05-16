using Zeven.Interop;

namespace Zeven;

/// <summary>Batch PPMd compression/decompression using the Zeven chunked format.</summary>
public static class PpmdCodec
{
    public static void Compress(Stream input, Stream output, PpmdOptions? options = null)
        => ZevenCodec.Compress(input, output, options ?? new PpmdOptions());

    public static void Decompress(Stream input, Stream output)
        => ZevenCodec.Decompress(input, output, CodecId.Ppmd);
}
