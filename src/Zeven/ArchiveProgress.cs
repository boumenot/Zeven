namespace Zeven;

/// <summary>
/// Progress information reported during archive operations.
/// </summary>
public readonly record struct ArchiveProgress(
    ulong TotalBytes,
    ulong CompletedBytes,
    string? CurrentPath = null,
    uint? CurrentIndex = null);
