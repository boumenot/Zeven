using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch LZMA2 compression/decompression using 7-Zip's ICompressCoder.
/// Processes entire streams in one call — for incremental streaming, use Lzma2Stream.
/// </summary>
public static class Lzma2Codec
{
    /// <summary>Compress a stream using LZMA2.</summary>
    public static void Compress(Stream input, Stream output, Lzma2Options? options = null)
    {
        Codec.Compress(options ?? new Lzma2Options(), input, output);
    }

    /// <summary>Decompress an LZMA2 stream.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        Codec.Decompress(CodecId.Lzma2, input, output);
    }
}
