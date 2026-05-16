using System.IO.Compression;

namespace Zeven;

/// <summary>Incremental PPMd compression/decompression using the Zeven chunked format.</summary>
public sealed class PpmdStream : ZevenStream<PpmdOptions>
{
    public PpmdStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : base(stream, mode, leaveOpen) { }

    public PpmdStream(Stream stream, CompressionMode mode, PpmdOptions? options,
            bool leaveOpen = false)
        : base(stream, mode, options, leaveOpen) { }
}
