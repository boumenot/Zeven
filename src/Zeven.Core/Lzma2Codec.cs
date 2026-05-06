using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch LZMA2 compression/decompression using the Zeven chunked wire format.
/// Processes entire streams in one call — for incremental streaming, use Lzma2Stream.
/// </summary>
public static class Lzma2Codec
{
    /// <summary>Compress a stream using LZMA2 with chunked framing.</summary>
    public static void Compress(Stream input, Stream output, Lzma2Options? options = null)
    {
        var opts = options ?? new Lzma2Options();
        byte[] propertyHeader = Codec.CapturePropertyHeader(opts);

        ZevenFormat.WriteHeader(output, CodecId.Lzma2, propertyHeader);

        long inputLength = input.Length - input.Position;
        if (inputLength > 0)
        {
            using var compressed = new MemoryStream();
            Codec.CompressBlock(opts, propertyHeader, input, compressed);

            ZevenFormat.WriteChunk(output, inputLength,
                    compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        }

        ZevenFormat.WriteEndMarker(output);
    }

    /// <summary>Decompress an LZMA2 stream in chunked format.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        byte[] propertyHeader = ZevenFormat.ReadHeaderAndValidateCodec(input, CodecId.Lzma2);

        while (true)
        {
            var chunk = ZevenFormat.ReadChunk(input);
            if (chunk == null)
            {
                break;
            }

            using var compressedStream = new MemoryStream(chunk.Value.CompressedData);
            Codec.DecompressBlock(propertyHeader, CodecId.Lzma2,
                    compressedStream, output, chunk.Value.UncompressedSize);
        }
    }
}
