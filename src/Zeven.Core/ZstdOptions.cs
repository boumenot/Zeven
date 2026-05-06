using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Compression options for the Zstd codec.
/// All properties except Level are optional — null means let 7-Zip decide.
/// </summary>
public class ZstdOptions : ICodecOptions
{
    public const int DefaultChunkSize = 16 * 1024 * 1024;

    public ulong CodecId => Interop.CodecId.Zstd;

    /// <summary>Compression level. Default: 3.</summary>
    public int Level { get; init; } = 3;

    /// <summary>Maximum uncompressed bytes per chunk for streaming compression. Default: 16 MB. Only used by ZstdStream; ZstdCodec writes a single chunk.</summary>
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
