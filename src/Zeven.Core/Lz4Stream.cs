using System.IO.Compression;

namespace Zeven.Core;

/// <summary>Incremental LZ4 compression/decompression using the Zeven chunked format.</summary>
public sealed class Lz4Stream : ZevenStream<Lz4Options>
{
    public Lz4Stream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : base(stream, mode, leaveOpen) { }

    public Lz4Stream(Stream stream, CompressionMode mode, Lz4Options? options,
            bool leaveOpen = false)
        : base(stream, mode, options, leaveOpen) { }
}
