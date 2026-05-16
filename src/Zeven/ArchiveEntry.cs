namespace Zeven;

/// <summary>
/// Metadata for a single item in an archive.
/// </summary>
public record ArchiveEntry
{
    /// <summary>Zero-based index within the archive.</summary>
    public required uint Index { get; init; }

    /// <summary>Path of the entry within the archive.</summary>
    public required string Path { get; init; }

    /// <summary>Whether this entry is a directory.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Uncompressed size in bytes.</summary>
    public ulong Size { get; init; }

    /// <summary>Compressed size in bytes.</summary>
    public ulong PackedSize { get; init; }

    /// <summary>File creation time.</summary>
    public DateTime? CreatedTime { get; init; }

    /// <summary>File last modification time.</summary>
    public DateTime? ModifiedTime { get; init; }

    /// <summary>File last access time.</summary>
    public DateTime? AccessedTime { get; init; }

    /// <summary>Whether the entry is encrypted.</summary>
    public bool IsEncrypted { get; init; }

    /// <summary>CRC-32 checksum of the uncompressed data.</summary>
    public uint? Crc { get; init; }

    /// <summary>Compression method name (e.g., "LZMA2", "PPMd").</summary>
    public string? Method { get; init; }
}
