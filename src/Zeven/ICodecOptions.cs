namespace Zeven;

/// <summary>
/// Defines codec identity and compression properties for a 7-Zip codec.
/// Implementations return a dictionary of property IDs to values which
/// CodecHelper marshals into COM calls. No COM types are exposed.
/// </summary>
public interface ICodecOptions
{
    /// <summary>7-Zip codec ID (e.g., 0x21 for LZMA2).</summary>
    ulong CodecId { get; }

    /// <summary>Maximum uncompressed bytes per chunk for streaming compression.</summary>
    int ChunkSize { get; }

    /// <summary>
    /// Returns the codec properties to set before encoding.
    /// Keys are CoderPropId constants, values are uint or ulong.
    /// Only non-null/non-default properties should be included.
    /// </summary>
    Dictionary<uint, object> GetProperties();
}
