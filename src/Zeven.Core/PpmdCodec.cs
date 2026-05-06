using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch PPMd compression/decompression using the Zeven chunked wire format.
/// Processes entire streams in one call — for incremental streaming, use PpmdStream.
/// PPMd excels at compressing text and structured data.
/// </summary>
public static class PpmdCodec
{
    /// <summary>Compress a stream using PPMd with chunked framing.</summary>
    public static void Compress(Stream input, Stream output, PpmdOptions? options = null)
    {
        var opts = options ?? new PpmdOptions();
        byte[] propertyHeader = Codec.CapturePropertyHeader(opts);

        PpmdFormat.WriteHeader(output, propertyHeader);

        long inputLength = input.Length - input.Position;
        if (inputLength > 0)
        {
            using var compressed = new MemoryStream();
            Codec.CompressBlock(opts, propertyHeader, input, compressed);

            PpmdFormat.WriteChunk(output, inputLength,
                    compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        }

        PpmdFormat.WriteEndMarker(output);
    }

    /// <summary>Decompress a PPMd stream in chunked format.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        byte[] propertyHeader = PpmdFormat.ReadHeader(input);

        while (true)
        {
            var chunk = PpmdFormat.ReadChunk(input);
            if (chunk == null)
            {
                break;
            }

            using var compressedStream = new MemoryStream(chunk.Value.CompressedData);
            Codec.DecompressBlock(propertyHeader, CodecId.Ppmd,
                    compressedStream, output, chunk.Value.UncompressedSize);
        }
    }
}
