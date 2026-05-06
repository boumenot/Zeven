using System.IO.Compression;

namespace Zeven.Core;

/// <summary>Incremental Brotli compression/decompression using the Zeven chunked format.</summary>
public sealed class BrotliStream : ZevenStream<BrotliOptions>
{
    public BrotliStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : base(stream, mode, leaveOpen) { }

    public BrotliStream(Stream stream, CompressionMode mode, BrotliOptions? options,
            bool leaveOpen = false)
        : base(stream, mode, options, leaveOpen) { }
}
