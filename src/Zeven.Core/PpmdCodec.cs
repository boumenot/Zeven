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

        ZevenFormat.WriteHeader(output, CodecId.Ppmd, propertyHeader);

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

    /// <summary>Decompress a PPMd stream in chunked format.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        byte[] propertyHeader = ZevenFormat.ReadHeaderAndValidateCodec(input, CodecId.Ppmd);

        while (true)
        {
            var chunk = ZevenFormat.ReadChunk(input);
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
