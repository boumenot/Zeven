using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Compression options for the LZMA2 codec.
/// All properties except Level are optional — null means let 7-Zip decide.
/// </summary>
public class Lzma2Options : ICodecOptions
{
    public const int DefaultChunkSize = 16 * 1024 * 1024;

    public ulong CodecId => Interop.CodecId.Lzma2;

    /// <summary>Compression level 0-9. Default: 5.</summary>
    public int Level { get; init; } = 5;

    /// <summary>Maximum uncompressed bytes per chunk for streaming compression. Default: 16 MB. Only used by Lzma2Stream; Lzma2Codec writes a single chunk.</summary>
    public int ChunkSize { get; init; } = DefaultChunkSize;

    /// <summary>Dictionary size in bytes (e.g., 64*1024*1024 for 64MB). Up to 4GB.</summary>
    public long? DictionarySize { get; init; }

    /// <summary>Number of fast bytes for match finder (5-273).</summary>
    public int? NumFastBytes { get; init; }

    /// <summary>Number of threads. null = let 7-Zip decide (~2 threads).</summary>
    public int? NumThreads { get; init; }

    /// <summary>LZMA2 block size in bytes for multi-threaded encoding.</summary>
    public long? BlockSize { get; init; }

    /// <summary>Algorithm: 0=fast, 1=normal.</summary>
    public int? Algorithm { get; init; }

    public Dictionary<uint, object> GetProperties()
    {
        var props = new Dictionary<uint, object>
        {
            [CoderPropId.Level] = (uint)this.Level
        };

        if (this.DictionarySize.HasValue)
        {
            props[CoderPropId.DictionarySize] = this.DictionarySize.Value <= uint.MaxValue
                ? (object)(uint)this.DictionarySize.Value
                : (object)(ulong)this.DictionarySize.Value;
        }
        if (this.NumFastBytes.HasValue)
        {
            props[CoderPropId.NumFastBytes] = (uint)this.NumFastBytes.Value;
        }
        if (this.NumThreads.HasValue)
        {
            props[CoderPropId.NumThreads] = (uint)this.NumThreads.Value;
        }
        if (this.BlockSize.HasValue)
        {
            props[CoderPropId.BlockSize] = this.BlockSize.Value <= uint.MaxValue
                ? (object)(uint)this.BlockSize.Value
                : (object)(ulong)this.BlockSize.Value;
        }
        if (this.Algorithm.HasValue)
        {
            props[CoderPropId.Algorithm] = (uint)this.Algorithm.Value;
        }

        return props;
    }
}
