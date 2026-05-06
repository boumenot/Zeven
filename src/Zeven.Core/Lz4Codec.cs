using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch LZ4 compression/decompression using the Zeven chunked wire format.
/// Processes entire streams in one call — for incremental streaming, use Lz4Stream.
/// LZ4 provides extremely fast compression and decompression.
/// </summary>
public static class Lz4Codec
{
    /// <summary>Compress a stream using LZ4 with chunked framing.</summary>
    public static void Compress(Stream input, Stream output, Lz4Options? options = null)
    {
        var opts = options ?? new Lz4Options();
        byte[] propertyHeader = Codec.CapturePropertyHeader(opts);

        ZevenFormat.WriteHeader(output, CodecId.Lz4, propertyHeader);

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

    /// <summary>Decompress an LZ4 stream in chunked format.</summary>
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
            Codec.DecompressBlock(propertyHeader, CodecId.Lz4,
                    compressedStream, output, chunk.Value.UncompressedSize);
        }
    }
}
