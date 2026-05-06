using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch PPMd compression/decompression using 7-Zip's ICompressCoder.
/// Processes entire streams in one call — for incremental streaming, use PpmdStream.
/// PPMd excels at compressing text and structured data.
/// </summary>
public static class PpmdCodec
{
    /// <summary>Compress a stream using PPMd.</summary>
    public static void Compress(Stream input, Stream output, PpmdOptions? options = null)
    {
        Codec.Compress(options ?? new PpmdOptions(), input, output);
    }

    /// <summary>Decompress a PPMd stream.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        Codec.Decompress(CodecId.Ppmd, input, output);
    }
}
