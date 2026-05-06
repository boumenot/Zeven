using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Compression options for the PPMd codec.
/// PPMd excels at compressing text and structured data.
/// All properties except Level are optional — null means let 7-Zip decide.
/// </summary>
public class PpmdOptions : ICodecOptions
{
    public const int DefaultChunkSize = 16 * 1024 * 1024;

    public ulong CodecId => Interop.CodecId.Ppmd;

    /// <summary>Compression level 0-9. Default: 5.</summary>
    public int Level { get; set; } = 5;

    /// <summary>Model order (2-32). Higher = better compression, more memory.</summary>
    public int? Order { get; set; }

    /// <summary>Memory size in bytes for the PPMd model.</summary>
    public long? MemorySize { get; set; }

    /// <summary>Maximum uncompressed bytes per chunk for streaming compression. Default: 16 MB. Only used by PpmdStream; PpmdCodec writes a single chunk.</summary>
    public int ChunkSize { get; set; } = DefaultChunkSize;

    public Dictionary<uint, object> GetProperties()
    {
        var props = new Dictionary<uint, object>
        {
            [CoderPropId.Level] = (uint)this.Level
        };

        if (this.Order.HasValue)
        {
            props[CoderPropId.Order] = (uint)this.Order.Value;
        }
        if (this.MemorySize.HasValue)
        {
            props[CoderPropId.UsedMemorySize] = this.MemorySize.Value <= uint.MaxValue
                ? (object)(uint)this.MemorySize.Value
                : (object)(ulong)this.MemorySize.Value;
        }

        return props;
    }
}
