using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Compression options for the LZ4 codec.
/// LZ4 provides extremely fast compression and decompression.
/// </summary>
public class Lz4Options : ICodecOptions
{
    public const int DefaultChunkSize = 16 * 1024 * 1024;

    public ulong CodecId => Interop.CodecId.Lz4;

    /// <summary>Compression level 0-9. Default: 3.</summary>
    public int Level { get; init; } = 3;

    /// <summary>Maximum uncompressed bytes per chunk for streaming compression. Default: 16 MB. Only used by Lz4Stream; Lz4Codec writes a single chunk.</summary>
    public int ChunkSize { get; init; } = DefaultChunkSize;

    public Dictionary<uint, object> GetProperties()
    {
        var props = new Dictionary<uint, object>
        {
            [CoderPropId.Level] = (uint)this.Level
        };

        return props;
    }
}
