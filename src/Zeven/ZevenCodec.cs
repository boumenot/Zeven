using Zeven.Interop;

namespace Zeven;

/// <summary>Shared batch compress/decompress using the Zeven chunked wire format.</summary>
public static class ZevenCodec
{
    public static void Compress(Stream input, Stream output, ICodecOptions options)
    {
        if (!input.CanSeek)
        {
            throw new ArgumentException("Input stream must be seekable.", nameof(input));
        }

        if (options.ChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ChunkSize must be positive.");
        }

        byte[] propertyHeader = Codec.CapturePropertyHeader(options);
        ZevenFormat.WriteHeader(output, options.CodecId, propertyHeader);

        int chunkSize = options.ChunkSize;
        byte[] buffer = new byte[chunkSize];
        long remaining = input.Length - input.Position;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(remaining, chunkSize);
            int totalRead = 0;
            while (totalRead < toRead)
            {
                int n = input.Read(buffer, totalRead, toRead - totalRead);
                if (n == 0) { break; }
                totalRead += n;
            }

            if (totalRead == 0) { break; }

            using var inputChunk = new MemoryStream(buffer, 0, totalRead, writable: false);
            using var compressed = new MemoryStream();
            Codec.CompressBlock(options, propertyHeader, inputChunk, compressed);
            ZevenFormat.WriteChunk(output, totalRead,
                    compressed.GetBuffer().AsSpan(0, (int)compressed.Length));

            remaining -= totalRead;
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
