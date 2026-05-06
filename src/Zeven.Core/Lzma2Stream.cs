using System.IO.Compression;

namespace Zeven.Core;

/// <summary>Incremental LZMA2 compression/decompression using the Zeven chunked format.</summary>
public sealed class Lzma2Stream : ZevenStream<Lzma2Options>
{
    public Lzma2Stream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : base(stream, mode, leaveOpen) { }

    public Lzma2Stream(Stream stream, CompressionMode mode, Lzma2Options? options,
            bool leaveOpen = false)
        : base(stream, mode, options, leaveOpen) { }
}
