using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Compression options for the Brotli codec.
/// All properties except Level are optional — null means let 7-Zip decide.
/// </summary>
public class BrotliOptions : ICodecOptions
{
    public const int DefaultChunkSize = 16 * 1024 * 1024;

    public ulong CodecId => Interop.CodecId.Brotli;

    /// <summary>Compression level 0-11. Default: 3.</summary>
    public int Level { get; set; } = 3;

    /// <summary>Maximum uncompressed bytes per chunk for streaming compression. Default: 16 MB. Only used by ZevenBrotliStream; BrotliCodec writes a single chunk.</summary>
    public int ChunkSize { get; set; } = DefaultChunkSize;

    public Dictionary<uint, object> GetProperties()
    {
        var props = new Dictionary<uint, object>
        {
            [CoderPropId.Level] = (uint)this.Level
        };

        return props;
    }
}
