using System.IO.Compression;

namespace Zeven.Core;

/// <summary>Incremental Zstd compression/decompression using the Zeven chunked format.</summary>
public sealed class ZstdStream : ZevenStream<ZstdOptions>
{
    public ZstdStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : base(stream, mode, leaveOpen) { }

    public ZstdStream(Stream stream, CompressionMode mode, ZstdOptions? options,
            bool leaveOpen = false)
        : base(stream, mode, options, leaveOpen) { }
}
