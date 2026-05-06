using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch Brotli compression/decompression using the Zeven chunked wire format.
/// Processes entire streams in one call — for incremental streaming, use BrotliStream.
/// </summary>
public static class BrotliCodec
{
    /// <summary>Compress a stream using Brotli with chunked framing.</summary>
    public static void Compress(Stream input, Stream output, BrotliOptions? options = null)
    {
        var opts = options ?? new BrotliOptions();
        byte[] propertyHeader = Codec.CapturePropertyHeader(opts);

        ZevenFormat.WriteHeader(output, CodecId.Brotli, propertyHeader);

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

    /// <summary>Decompress a Brotli stream in chunked format.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        byte[] propertyHeader = ZevenFormat.ReadHeader(input).PropertyHeader;

        while (true)
        {
            var chunk = ZevenFormat.ReadChunk(input);
            if (chunk == null)
            {
                break;
            }

            using var compressedStream = new MemoryStream(chunk.Value.CompressedData);
            Codec.DecompressBlock(propertyHeader, CodecId.Brotli,
                    compressedStream, output, chunk.Value.UncompressedSize);
        }
    }
}
