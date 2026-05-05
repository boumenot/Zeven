using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch PPMd compression/decompression using 7-Zip's ICompressCoder.
/// Processes entire streams in one call — for incremental streaming, use PpmdStream.
/// PPMd excels at compressing text and structured data.
/// </summary>
public static class PpmdCodec
{
    /// <summary>Property header size for PPMd: 5 bytes (order + memory size).</summary>
    public const int PropertyHeaderSize = 5;

    /// <summary>Compress a stream using PPMd. Writes a 5-byte property header then compressed data.</summary>
    public static void Compress(Stream input, Stream output, int level = 5, bool writeSizePrefix = true)
    {
        CodecHelper.Compress(CodecId.Ppmd, input, output, level, writeSizePrefix);
    }

    /// <summary>Decompress a PPMd stream. Reads the 5-byte property header then decompresses.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        CodecHelper.Decompress(CodecId.Ppmd, PropertyHeaderSize, input, output);
    }
}
