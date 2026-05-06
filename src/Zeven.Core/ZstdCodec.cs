using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Batch Zstd compression/decompression using the Zeven chunked wire format.
/// Processes entire streams in one call — for incremental streaming, use ZstdStream.
/// </summary>
public static class ZstdCodec
{
    /// <summary>Compress a stream using Zstd with chunked framing.</summary>
    public static void Compress(Stream input, Stream output, ZstdOptions? options = null)
    {
        var opts = options ?? new ZstdOptions();
        byte[] propertyHeader = Codec.CapturePropertyHeader(opts);

        ZevenFormat.WriteHeader(output, CodecId.Zstd, propertyHeader);

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

    /// <summary>Decompress a Zstd stream in chunked format.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        byte[] propertyHeader = ZevenFormat.ReadHeaderAndValidateCodec(input, CodecId.Zstd);

        while (true)
        {
            var chunk = ZevenFormat.ReadChunk(input);
            if (chunk == null)
            {
                break;
            }

            using var compressedStream = new MemoryStream(chunk.Value.CompressedData);
            Codec.DecompressBlock(propertyHeader, CodecId.Zstd,
                    compressedStream, output, chunk.Value.UncompressedSize);
        }
    }
}
