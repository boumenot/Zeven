using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>Shared batch compress/decompress using the Zeven chunked wire format.</summary>
public static class ZevenCodec
{
    public static void Compress(Stream input, Stream output, ICodecOptions options)
    {
        if (!input.CanSeek)
        {
            throw new ArgumentException("Input stream must be seekable.", nameof(input));
        }

        byte[] propertyHeader = Codec.CapturePropertyHeader(options);
        ZevenFormat.WriteHeader(output, options.CodecId, propertyHeader);

        long inputLength = input.Length - input.Position;
        if (inputLength > 0)
        {
            using var compressed = new MemoryStream();
            Codec.CompressBlock(options, propertyHeader, input, compressed);
            ZevenFormat.WriteChunk(output, inputLength,
                    compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        }

        ZevenFormat.WriteEndMarker(output);
    }

    public static void Decompress(Stream input, Stream output, ulong expectedCodecId)
    {
        byte[] propertyHeader = ZevenFormat.ReadHeaderAndValidateCodec(input, expectedCodecId);

        while (true)
        {
            var chunk = ZevenFormat.ReadChunk(input);
            if (chunk == null)
            {
                break;
            }

            using var compressedStream = new MemoryStream(chunk.Value.CompressedData);
            Codec.DecompressBlock(propertyHeader, expectedCodecId,
                    compressedStream, output, chunk.Value.UncompressedSize);
        }
    }
}
