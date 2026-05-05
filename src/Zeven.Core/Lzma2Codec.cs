using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch LZMA2 compression/decompression using 7-Zip's ICompressCoder.
/// Processes entire streams in one call — for incremental streaming, use Lzma2Stream.
/// </summary>
public static class Lzma2Codec
{
    /// <summary>Property header size for LZMA2: 1 byte encoding dictionary size.</summary>
    public const int PropertyHeaderSize = 1;

    /// <summary>Compress a stream using LZMA2. Writes a 1-byte property header then compressed data.</summary>
    public static void Compress(Stream input, Stream output, int level = 5, bool writeSizePrefix = true)
    {
        CodecHelper.Compress(CodecId.Lzma2, input, output, level, writeSizePrefix);
    }

    /// <summary>Decompress an LZMA2 stream. Reads the 1-byte property header then decompresses.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        CodecHelper.Decompress(CodecId.Lzma2, PropertyHeaderSize, input, output);
    }
}
