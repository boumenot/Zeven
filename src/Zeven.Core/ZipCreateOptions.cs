namespace Zeven.Core;

/// <summary>Options for .zip archive creation.</summary>
public class ZipCreateOptions : IArchiveCreateOptions
{
    /// <summary>Compression level 0-9.</summary>
    public int? Level { get; init; }

    /// <summary>Compression method: "Deflate", "Deflate64", "BZip2", "LZMA", "PPMd", "Copy".</summary>
    public string? Method { get; init; }

    /// <summary>Number of CPU threads.</summary>
    public int? NumThreads { get; init; }

    public IEnumerable<(string Name, object Value)> GetProperties()
    {
        if (this.Level.HasValue) { yield return (ArchivePropName.Level, (uint)this.Level.Value); }
        if (this.Method != null) { yield return (ArchivePropName.Method, this.Method); }
        if (this.NumThreads.HasValue) { yield return (ArchivePropName.NumThreads, (uint)this.NumThreads.Value); }
    }
}
